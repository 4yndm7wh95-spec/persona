using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace P3Makoto.Audio;

public enum Snd
{
	Gunshot,
	TarotReveal,    // 塔罗牌浮现：上扬的气流 + 翻牌 + 钟琴琶音 + 低音垫
	GlassOnly,      // 只有玻璃层（校准用，游戏里不播放）
	TarotShatter,   // 塔罗牌碎裂：脆响 + 几十片玻璃叮当 + 低频冲击 + 确认和弦 + 空间尾音
	StarGain,       // 辉星凝聚（轻）
	StarSpend,      // 辉星消耗（小碎裂）
	CutIn,          // "CRITICAL" / "ONE MORE!" 切入：风声冲入 + 重击 + 和弦重音
	Weak,           // WEAK!：金属高音"叮"+ 上扬滑音 + 微光
	ThanatosSlash,  // 塔纳托斯斩击：低沉风啸 + 巨响 + 金属刮擦 + 不协和低音
	// v0.13.5 起只有 P3R 原声（没有合成版本，缺文件时退回到上面对应的合成音）
	Summon,         // 选人界面的召唤（召唤器 + 闪光的完整版）
	OneMore,        // "ONE MORE!"
	SkillCutIn,     // 技能名大字切入（Persona 光环切入）
	Down,           // WEAK! 弹字（v0.13.7 起用暴击冲击声；名字沿用）
	CardDraw,       // 抽到一张牌（只用于真实抽牌，不用在特效里）
	Anvil,          // v0.13.11 锻打（合成）：锤子砸在铁砧上 —— 清脆的金属"铛"+ 短促的击打 + 一点火星，不沉
	Cock,           // v0.13.11 召唤器抵住太阳穴时的"咔嚓"上膛（P3R em0131_gun）
}

/// <summary>
/// v0.13.5 起优先播放 P3R 原声（见 P3r 表）；下面的程序合成只作缺文件时的最后退路。
/// 程序合成音效（不需要音频素材）：启动时在后台线程合成 PCM，播放走 Godot 的 "SFX" 总线 —— 和游戏自己的
/// 临时音效（NDebugAudioManager）同一条总线，跟随游戏音量设置。
/// 召唤器枪声 + 几个"仪式感"特效的音效（塔罗牌、辉星、切入大字、WEAK!、塔纳托斯斩击），普通命中仍用游戏原版。
/// 每个音效都分层设计：起音（冲击 / 脆响）+ 身体（中频噪声 / 和弦）+ 低频（重量）+ 尾音（空间 / 微光），让反馈有层次、有确认感。
/// 合成手法：N 波尖峰 + 滤波噪声（状态变量滤波器）+ 非谐波钟声 + 指数包络。
/// </summary>
public static class Sfx
{
	private const int SR = 44100;
	private const float Master = 0.8f;
	private static readonly ConcurrentDictionary<Snd, byte[]> Pcm = new();
	private static readonly Dictionary<Snd, AudioStreamWav> Streams = new();
	private static readonly Dictionary<Snd, ulong> LastPlay = new();
	private static StringName? _bus;

	public static void Prewarm() => Task.Run(() =>
	{
		try
		{
			PrewarmSamples();
		}
		catch (Exception e)
		{
			GD.PrintErr($"{ModEntry.Tag} sample prewarm failed: {e}");
		}
		foreach (Snd s in Enum.GetValues<Snd>())
		{
			if (SynthFallback(s) != s || (P3r.TryGetValue(s, out var vs) && vs.All(v => v.All(l => Sample(l.File) != null))))
			{
				continue;
			}
			try
			{
				Pcm.TryAdd(s, Encode(Synth(s), Drive(s)));
			}
			catch (Exception e)
			{
				GD.PrintErr($"{ModEntry.Tag} synth {s} failed: {e}");
			}
		}
	});

	/// <summary>
	/// v0.13.5：P3R 原声（art/sfx_p3r → pck 里的 res://p3makoto/sfx/）。每个音效可有多个变体（随机挑一个），
	/// 每个变体可由多层组成（文件, 延迟秒, 音量倍率）。文件缺失或解码失败时退回：Vanilla 原版 FMOD → 合成。
	/// </summary>
	private static readonly Dictionary<Snd, (string File, float Delay, float Gain)[][]> P3r = new()
	{
		// 召唤器开枪（Gunshot）和选人召唤（Summon）：v0.13.13 起用回 v0.13.4 之前的合成枪声（用户没有要求换开枪声），不在这张表里
		[Snd.TarotReveal] = new[] { new[] { ("Snd_FX_Btl_PersonaAura_00", 0f, 0.6f) } },   // 特效里的"浮现"：Persona 光环（翻牌声只留给真实抽牌）
		[Snd.CardDraw] = new[] { new[] { ("card_draw", 0f, 1f) } },
		[Snd.Cock] = new[] { new[] { ("sc0255_btl_gun_reload", 0f, 0.8f) } },   // P3R 装弹声（干脆的"咔嚓"）；之前截的 em0131 片段是一小团噪声，听着像"噗"       // Velvet_Tarot_Flip 原声里有两次翻牌，只截前 0.3 秒（一张）
		[Snd.TarotShatter] = new[] { new[] { ("ShuffleTime_ArcanaBurst", 0f, 0.9f) } },
		// StarGain：v0.13.12 起用回合成版（上扬颤音 + 琶音，用户对比后选的），不在这张表里
		[Snd.StarSpend] = new[] { new[] { ("Sys_Theurgia_Open", 0f, 0.8f) } },
		[Snd.CutIn] = new[] { new[] { ("flash_cutin", 0f, 0.9f) } },
		[Snd.Weak] = new[] { new[] { ("ShuffleTime_Select", 0f, 0.8f) } },
		[Snd.ThanatosSlash] = new[] { new[] { ("swish_heavy_1", 0f, 0.9f), ("atk_hit_enemy_slash", 0.18f, 1f), ("sword_finish", 0.18f, 0.6f) } },
		[Snd.OneMore] = new[] { new[] { ("Btl_1more_pc", 0f, 1f) } },
		[Snd.SkillCutIn] = new[] { new[] { ("Snd_FX_Btl_PersonaAura_00_Cutin", 0f, 0.9f) } },
		[Snd.Down] = new[] { new[] { ("Btl_Critical", 0f, 0.8f) } },
	};

	/// <summary>只有原声、没有合成版本的音效缺文件时，用哪个合成音代替。</summary>
	private static Snd SynthFallback(Snd s) => s switch
	{
		Snd.Summon => Snd.Gunshot,
		Snd.OneMore or Snd.SkillCutIn => Snd.CutIn,
		Snd.Down => Snd.Weak,
		Snd.CardDraw => Snd.TarotReveal,
		Snd.Cock => Snd.Weak,
		_ => s,
	};

	/// <summary>元素命中的 P3R 音效变体。有原声的元素，原版卡牌的命中音效会被关掉（见 AttackHitVfxPatch），避免两套叠在一起。</summary>
	private static readonly Dictionary<P3Makoto.Vfx.Element, string[]> ElementFiles = new()
	{
		[P3Makoto.Vfx.Element.Slash] = new[] { "atk_hit_slash_1", "atk_hit_slash_2", "atk_hit_slash_3" },
		[P3Makoto.Vfx.Element.Royal] = new[] { "atk_hit_slash_1", "atk_hit_slash_2", "atk_hit_slash_3" },
		[P3Makoto.Vfx.Element.Pierce] = new[] { "atk_hit_stab_1", "atk_hit_stab_2", "atk_hit_stab_3" },
		[P3Makoto.Vfx.Element.Strike] = new[] { "atk_hit_strike_1", "atk_hit_strike_2", "atk_hit_strike_3" },
		[P3Makoto.Vfx.Element.Megido] = new[] { "megido_hit" },      // P3R skill_0078 = Megido（技能名表确认；之前误用的 0070 其实是 Eiha），去掉开头 0.95s 空白
		[P3Makoto.Vfx.Element.Meteor] = new[] { "evt_impact_01" },     // v0.13.11：用户选的 P3R 冲击声（原来的打击声像拳击）
		[P3Makoto.Vfx.Element.Star] = new[] { "atk_light_1", "atk_light_2", "atk_light_3" },      // P3R skill_0070（推测为 Megido），去掉开头 0.17s 空白
		[P3Makoto.Vfx.Element.Agi] = new[] { "atk_fire_1", "atk_fire_2", "atk_fire_3" },
		[P3Makoto.Vfx.Element.Bufu] = new[] { "atk_ice_1", "atk_ice_2", "atk_ice_3" },
		[P3Makoto.Vfx.Element.Zio] = new[] { "atk_elec_1", "atk_elec_2", "atk_elec_3" },
		[P3Makoto.Vfx.Element.Hama] = new[] { "atk_light_1", "atk_light_2", "atk_light_3" },
		[P3Makoto.Vfx.Element.Mudo] = new[] { "atk_dark_1", "atk_dark_2", "atk_dark_3" },
	};

	/// <summary>这个元素的命中有可用的 P3R 原声（文件都在且能解码）。</summary>
	public static bool HasElementSound(P3Makoto.Vfx.Element e) =>
		ElementFiles.TryGetValue(e, out string[]? files) && files.All(f => Sample(f) != null);

	/// <summary>
	/// 单独的原声片段（特效里直接按文件名播放，v0.13.9）。列在这里只是为了启动时预热、统计。
	/// wheel_click / clock_tick / clock_midnight / clock_spin 是从 art/sfx_p3r_clock 截出来的。
	/// </summary>
	private static readonly string[] Clips =
	{
		"vorpal_blade", "Snd_FX_Btl_AlloutUI_00", "Btl_Allout_Wipe", "allout_brawl", "Snd_FX_Btl_Allout_Kime01",
		"ShuffleTime_Start", "ShuffleTime_Whoosh", "ShuffleTime_CardSet", "wheel_click", "clock_tick", "clock_midnight", "clock_spin", "evt_eft_timespin_01",
		"em0561_sp_roulette_01", "em0561_sp_roulette_03", "ShuffleTime_Result", "Snd_FX_Btl_Summon_00",
		"ShuffleTime_PowerUp", "Theurgia_Max", "Snd_FX_Btl_StReflection_00_1", "Snd_FX_Btl_StAbsorption_00_1",
		"swish_large_1", "swish_large_2",
		"swish_mid_1", "swish_mid_2", "swish_mid_3", "swish_mid_4",
	};

	/// <summary>按文件名播放一个原声片段（缺文件就不响）。</summary>
	public static void Clip(string file, float volume = 1f, float delay = 0f, float pitch = 1f) => ModEntry.Safe("clip " + file, () =>
	{
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		if (delay > 0f)
		{
			tree.CreateTimer(delay, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () => Clip(file, volume, 0f, pitch);
			return;
		}
		if (Sample(file) is AudioStreamOggVorbis st)
		{
			Spawn(tree, st, volume * SampleMaster, pitch * (float)GD.RandRange(0.98, 1.02));
		}
	});

	private static readonly string[] Swings = { "swish_mid_1", "swish_mid_2", "swish_mid_3", "swish_mid_4" };

	/// <summary>
	/// 原版储君角色音效的替换表（RegentSfxMutePatch 调用）。返回 true = 原版照常播放（只有死亡音效），false = 已拦截。
	/// 施法 / 能力 / 君王之刃 / 引导之星 / 选人：本 mod 的特效自带音效，原版直接静音。
	/// </summary>
	public static bool ReplaceRegent(string name)
	{
		switch (name)
		{
			case "regent_die":
				return true;
			case "regent_attack":
				if (!P3Makoto.Core.Pose.SummonActive())           // 召唤开枪不是挥动：不播破空声（之前在召唤动画最前面形成一声"噗"）
				{
					ClipAny(Swings, 0.7f);
				}
				break;
			case "regent_forge":                                   // v0.14.0：锻造改成 Persona 蓄力增益，声音和画面都在 ForgePatch（Extras.PersonaCharge）
			case "regent_refine":
				break;
		}
		return false;
	}

	/// <summary>随机挑一个片段播放。</summary>
	public static void ClipAny(string[] files, float volume = 1f, float delay = 0f) => Clip(files[(int)(GD.Randi() % (uint)files.Length)], volume, delay);

	private const string SfxDir = "res://p3makoto/sfx/";
	private const float SampleMaster = 0.7f;
	private static readonly ConcurrentDictionary<string, AudioStreamOggVorbis?> Samples = new();

	/// <summary>读 pck 里的原始 .ogg 字节再解码（不走导入系统，所以不需要 .import 文件）。失败返回 null 并记住。</summary>
	private static AudioStreamOggVorbis? Sample(string name) => Samples.GetOrAdd(name, n =>
	{
		try
		{
			string path = SfxDir + n + ".ogg";
			if (!FileAccess.FileExists(path))
			{
				return null;
			}
			byte[] bytes = FileAccess.GetFileAsBytes(path);
			return bytes.Length > 0 ? AudioStreamOggVorbis.LoadFromBuffer(bytes) : null;
		}
		catch (Exception e)
		{
			GD.PrintErr($"{ModEntry.Tag} ogg {n} failed: {e.Message}");
			return null;
		}
	});

	private static void PrewarmSamples()
	{
		int ok = 0, all = 0;
		IEnumerable<string> names = P3r.Values.SelectMany(v => v).SelectMany(l => l).Select(l => l.File).Concat(ElementFiles.Values.SelectMany(f => f)).Concat(Clips).Distinct();
		foreach (string n in names)
		{
			all++;
			if (Sample(n) != null)
			{
				ok++;
			}
		}
		GD.Print($"{ModEntry.Tag} P3R sfx loaded {ok}/{all}");
	}

	/// <summary>播放一个原声变体（全部层都在才播）；成功返回 true。</summary>
	private static bool PlaySample(SceneTree tree, (string File, float Delay, float Gain)[][] variants, float volume, float pitch)
	{
		var layers = variants[variants.Length == 1 ? 0 : (int)(GD.Randi() % (uint)variants.Length)];
		if (layers.Any(l => Sample(l.File) == null))
		{
			return false;
		}
		// 调用方的 pitch 是给合成音调的（0.45~1.8），真实录音变调太多会失真：只取一半幅度，限制在 0.8~1.25
		float ps = Mathf.Clamp(1f + (pitch - 1f) * 0.5f, 0.8f, 1.25f) * (float)GD.RandRange(0.98, 1.02);
		foreach ((string file, float delay, float gain) in layers)
		{
			AudioStreamOggVorbis stream = Sample(file)!;
			void Go() => Spawn(tree, stream, volume * gain * SampleMaster, ps);
			if (delay > 0f)
			{
				tree.CreateTimer(delay, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += Go;
			}
			else
			{
				Go();
			}
		}
		return true;
	}

	private static void Spawn(SceneTree tree, AudioStream stream, float volume, float pitch)
	{
		_bus ??= AudioServer.GetBusIndex("SFX") >= 0 ? new StringName("SFX") : new StringName("Master");
		AudioStreamPlayer p = new()
		{
			Stream = stream,
			Bus = _bus,
			VolumeDb = Mathf.LinearToDb(Mathf.Clamp(volume, 0.0001f, 1.4f)),
			PitchScale = pitch,
			ProcessMode = Node.ProcessModeEnum.Always,
		};
		tree.Root.AddChild(p);
		p.Finished += p.QueueFree;
		p.Play();
	}

	/// <summary>v0.13.3 的原版 FMOD 映射：现在只在 P3R 原声缺失时作为第二级退路。</summary>
	private static readonly Dictionary<Snd, (string Event, float Volume)?> Vanilla = new()
	{
		[Snd.TarotReveal] = ("event:/sfx/ui/cards/card_transform", 0.9f),
		[Snd.TarotShatter] = ("event:/sfx/characters/defect/defect_glass_channel", 1f),
		[Snd.StarGain] = null,                                      // 合成
		[Snd.StarSpend] = ("event:/sfx/ui/enchant_simple", 0.8f),
		[Snd.Weak] = ("event:/sfx/characters/defect/defect_glass_channel", 0.8f),
		[Snd.CutIn] = ("event:/sfx/ui/wipe_ironclad", 0.9f),
		[Snd.Gunshot] = null,
		[Snd.ThanatosSlash] = null,
	};

	private static readonly Dictionary<P3Makoto.Vfx.Element, ulong> LastElement = new();

	/// <summary>元素命中音效：优先 P3R 原声（随机变体），否则原版 FMOD。</summary>
	public static void Element(P3Makoto.Vfx.Element e, float scale = 1f)
	{
		float vol = scale < 0.9f ? 0.55f : 0.8f;
		if (ElementFiles.TryGetValue(e, out string[]? files) && Engine.GetMainLoop() is SceneTree tree)
		{
			ulong now = Time.GetTicksMsec();
			if (LastElement.TryGetValue(e, out ulong last) && now - last < 60)
			{
				return;
			}
			LastElement[e] = now;
			AudioStreamOggVorbis? st = Sample(files[(int)(GD.Randi() % (uint)files.Length)]);
			if (st != null)
			{
				ModEntry.Safe("sfx element", () => Spawn(tree, st, vol * SampleMaster, (float)GD.RandRange(0.97, 1.03)));
				return;
			}
		}
		string? ev = e switch
		{
			P3Makoto.Vfx.Element.Agi => "event:/sfx/characters/attack_fire",
			P3Makoto.Vfx.Element.Bufu => "event:/sfx/characters/defect/defect_frost_channel",
			P3Makoto.Vfx.Element.Zio => "event:/sfx/characters/defect/defect_lightning_evoke",
			P3Makoto.Vfx.Element.Hama => "event:/sfx/characters/defect/defect_plasma_channel",
			P3Makoto.Vfx.Element.Mudo => "event:/sfx/characters/defect/defect_dark_channel",
			P3Makoto.Vfx.Element.Megido => "event:/sfx/characters/defect/defect_hyperbeam",
			P3Makoto.Vfx.Element.Meteor or P3Makoto.Vfx.Element.Star => "event:/sfx/characters/regent/regent_guiding_star",
			_ => null
		};
		if (ev != null)
		{
			PlayEvent(ev, vol);
		}
	}

	private static double _nextDraw;
	private const double DrawGap = 0.09;

	/// <summary>抽到一张牌：每张都响一次。几乎同时抽到的多张牌按 90ms 间隔排开（不会被 45ms 防叠音吞掉）。</summary>
	public static void CardDrawn()
	{
		double now = Time.GetTicksMsec() / 1000.0;
		double at = Math.Max(now, _nextDraw);
		_nextDraw = at + DrawGap;
		Play(Snd.CardDraw, 0.55f, (float)(at - now));
	}

	private static readonly Dictionary<string, ulong> LastEvent = new();

	/// <summary>播放原版 FMOD 事件（同一事件 60ms 内不叠加）。</summary>
	public static void PlayEvent(string ev, float volume = 1f) => ModEntry.Safe("fmod " + ev, () =>
	{
		ulong now = Time.GetTicksMsec();
		if (LastEvent.TryGetValue(ev, out ulong last) && now - last < 60)
		{
			return;
		}
		LastEvent[ev] = now;
		MegaCrit.Sts2.Core.Nodes.Audio.NAudioManager.Instance?.PlayOneShot(ev, volume);
	});

	/// <summary>播放；同一音效 45ms 内不重复叠加（多段攻击、群体命中时避免爆音）。</summary>
	public static void Play(Snd s, float volume = 1f, float delay = 0f, float pitch = 1f) => ModEntry.Safe("sfx " + s, () =>
	{
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		if (delay > 0f)
		{
			tree.CreateTimer(delay, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () => Play(s, volume, 0f, pitch);
			return;
		}
		ulong now = Time.GetTicksMsec();
		if (s != Snd.CardDraw && LastPlay.TryGetValue(s, out ulong last) && now - last < 45)
		{
			return;
		}
		LastPlay[s] = now;
		if (P3r.TryGetValue(s, out var variants) && PlaySample(tree, variants, volume, pitch))
		{
			return;
		}
		s = SynthFallback(s);
		if (Vanilla.TryGetValue(s, out var v) && v is (string ev, float vv))
		{
			PlayEvent(ev, Mathf.Clamp(volume * vv, 0f, 1.5f));
			return;
		}
		Spawn(tree, Get(s), volume * Master, pitch * (float)GD.RandRange(0.97, 1.03));
	});

	private static AudioStreamWav Get(Snd s)
	{
		if (!Streams.TryGetValue(s, out AudioStreamWav? w))
		{
			byte[] data = Pcm.TryGetValue(s, out byte[]? d) ? d : Pcm.GetOrAdd(s, Encode(Synth(s), Drive(s)));
			w = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = SR, Stereo = false, Data = data };
			Streams[s] = w;
		}
		return w;
	}

	/// <summary>软削波推动量：越大越"满"越响（压缩感更强）。</summary>
	private static float Drive(Snd s) => s switch
	{
		Snd.Gunshot => 1.3f,                      // 以前 2.6：削波太重、太亮（重心 7.3kHz）
		Snd.CutIn or Snd.ThanatosSlash => 2.2f,
		Snd.Anvil => 1.25f,
		Snd.Weak or Snd.TarotShatter or Snd.StarSpend or Snd.GlassOnly => 1.1f,   // 玻璃为主的音效几乎不削波：削波产生的高次谐波会让玻璃声刺耳
		Snd.StarGain or Snd.StarSpend => 1.3f,
		_ => 1.7f,
	};

	private static byte[] Encode(float[] x, float drive)
	{
		float peak = 1e-6f;
		foreach (float v in x)
		{
			peak = MathF.Max(peak, MathF.Abs(v));
		}
		float gain = drive / peak;
		byte[] b = new byte[x.Length * 2];
		for (int i = 0; i < x.Length; i++)
		{
			float v = MathF.Tanh(x[i] * gain) * 0.9f;              // 软削波：瞬态更"冲"而不爆
			short q = (short)Math.Clamp((int)(v * 32767f), short.MinValue, short.MaxValue);
			b[2 * i] = (byte)(q & 0xff);
			b[2 * i + 1] = (byte)((q >> 8) & 0xff);
		}
		return b;
	}

	// ───────────── 合成器 ─────────────
	private enum F { Low, Band, High }

	private sealed class Gen
	{
		public readonly float[] B;
		private readonly Random _r;

		public Gen(float seconds, int seed)
		{
			B = new float[(int)(seconds * SR)];
			_r = new Random(seed);
		}

		/// <summary>滤波噪声：fc(t) 截止 / 中心频率，damp 越小越尖（共振越强），env(t) 包络。</summary>
		public void Noise(float t0, float dur, Func<float, float> env, Func<float, float> fc, float damp, F mode, float amp)
		{
			float low = 0f, band = 0f;
			int s0 = (int)(t0 * SR), n = (int)(dur * SR);
			for (int i = 0; i < n && s0 + i < B.Length; i++)
			{
				float t = i / (float)SR;
				float f = 2f * MathF.Sin(MathF.PI * Math.Clamp(fc(t), 20f, 7500f) / (2f * SR));   // 2 倍过采样，保持稳定
				float x = (float)(_r.NextDouble() * 2 - 1);
				float y = 0f;
				for (int k = 0; k < 2; k++)
				{
					low += f * band;
					float high = x - low - damp * band;
					band += f * high;
					y = mode switch { F.Low => low, F.Band => band, _ => high };
				}
				B[s0 + i] += y * env(t) * amp;
			}
		}

		/// <summary>正弦音：freq(t) 可变（下坠的"咚"），env(t) 包络。</summary>
		public void Tone(float t0, float dur, Func<float, float> freq, Func<float, float> env, float amp)
		{
			double ph = _r.NextDouble() * Math.Tau;
			int s0 = (int)(t0 * SR), n = (int)(dur * SR);
			for (int i = 0; i < n && s0 + i < B.Length; i++)
			{
				float t = i / (float)SR;
				ph += Math.Tau * freq(t) / SR;
				B[s0 + i] += (float)Math.Sin(ph) * env(t) * amp;
			}
		}

		/// <summary>稀疏的高频"叮"颗粒，越往后越稀越轻（召唤器打碎灵魂的玻璃感余韵）。</summary>
		public void Shimmer(float t0, float spread, int count, float fLo, float fHi, float amp)
		{
			for (int i = 0; i < count; i++)
			{
				float u = (float)_r.NextDouble();
				float f = fLo + (float)_r.NextDouble() * (fHi - fLo);
				float tau = 0.02f + 0.05f * (float)_r.NextDouble();
				float a = amp * (0.4f + 0.6f * (float)_r.NextDouble()) * (1f - 0.7f * u);
				Tone(t0 + spread * u * u, tau * 5f, _ => f, Atk(0.0006f, tau), a);
				Tone(t0 + spread * u * u, tau * 3f, _ => f * 1.52f, Atk(0.0006f, tau * 0.6f), a * 0.4f);
			}
		}

		/// <summary>钟琴 / 铃声：非谐波泛音（1, 2, 2.76, 5.4, 8.9 倍），高泛音衰减更快。parts 限制泛音个数（越少越闷）。</summary>
		public void Bell(float t0, float f, float decay, float amp, int partCount = 5)
		{
			(float r, float a, float d)[] parts = { (1f, 1f, 1f), (2f, 0.5f, 0.7f), (2.76f, 0.35f, 0.45f), (5.4f, 0.18f, 0.25f), (8.93f, 0.08f, 0.15f) };
			foreach ((float r, float a, float d) in parts[..Math.Min(partCount, parts.Length)])
			{
				if (f * r < 11000f)
				{
					Tone(t0, decay * d * 5f, _ => f * r, Atk(0.0015f, decay * d), amp * a);
				}
			}
		}

		/// <summary>玻璃碎片叮当：count 片，时间按 u^1.6 分布（开头密、后面稀），每片两个泛音。</summary>
		public void Pings(float t0, float spread, int count, float fLo, float fHi, float amp)
		{
			for (int i = 0; i < count; i++)
			{
				float u = (float)_r.NextDouble();
				float at = t0 + spread * MathF.Pow(u, 1.6f);
				float f = fLo + (float)_r.NextDouble() * (fHi - fLo);
				float tau = 0.012f + 0.04f * (float)_r.NextDouble();
				float a = amp * (0.5f + 0.5f * (float)_r.NextDouble()) * (1f - 0.75f * u);
				Tone(at, tau * 6f, _ => f, Atk(0.0003f, tau), a);
				Tone(at, tau * 4f, _ => f * 2.71f, Atk(0.0003f, tau * 0.5f), a * 0.35f);
				Noise(at, 0.006f, Atk(0.0002f, 0.0015f), _ => f, 0.3f, F.Band, a * 0.6f);
			}
		}

		/// <summary>合成器音色（锯齿感）：基频 + 若干谐波，轻微失谐的两层叠出厚度；env 包络。</summary>
		public void Pad(float t0, float dur, float f, Func<float, float> env, float amp, int harmonics = 3)
		{
			for (int h = 1; h <= harmonics; h++)
			{
				float fh = f * h;
				Tone(t0, dur, _ => fh, env, amp / h);
				Tone(t0, dur, _ => fh * 1.006f, env, amp / h * 0.7f);
			}
		}

		/// <summary>
		/// P3R 式"颤音合成音"：频率从 f0 指数滑到 f1，同时被快速大幅度的颤音调制（rate 次/秒，depth 为相对幅度，
		/// 原声测得 14~28 次/秒、±15%~70%）。音色是三角波（基频 + 1/9 的 3 次谐波 + 1/25 的 5 次谐波），干净不刺耳。
		/// </summary>
		public void Warble(float t0, float dur, float f0, float f1, float rate, float depth, Func<float, float> env, float amp)
		{
			double ph = 0;
			int s0 = (int)(t0 * SR), n = (int)(dur * SR);
			for (int i = 0; i < n && s0 + i < B.Length; i++)
			{
				float t = i / (float)SR;
				float glide = f0 * MathF.Pow(f1 / f0, Math.Clamp(t / dur, 0f, 1f));
				float f = glide * (1f + depth * MathF.Sin(2f * MathF.PI * rate * t));
				ph += Math.Tau * f / SR;
				float v = (float)(Math.Sin(ph) - Math.Sin(3 * ph) / 9 + Math.Sin(5 * ph) / 25);
				B[s0 + i] += v * env(t) * amp;
			}
		}

		/// <summary>琶音：notes 依次每 step 秒一个（原声 75~85ms），三角波短音。</summary>
		public void Arp(float t0, float step, float[] notes, float noteDecay, float amp)
		{
			for (int k = 0; k < notes.Length; k++)
			{
				float f = notes[k];
				Warble(t0 + k * step, noteDecay * 5f, f, f, 6f, 0.004f, Atk(0.003f, noteDecay), amp);
			}
		}

		/// <summary>回声（反馈延迟，带一点低通让回声越来越暗）。原声的琶音和提示音都带这种回声。</summary>
		public void Echo(float delay, float feedback, float damp = 0.35f)
		{
			int d = (int)(delay * SR);
			float lp = 0f;
			for (int i = d; i < B.Length; i++)
			{
				lp += (1f - damp) * (B[i - d] - lp);
				B[i] += lp * feedback;
			}
		}

		/// <summary>
		/// 玻璃碎裂 —— 按 P3R 原声 9 段玻璃碎裂的统计重建（research/P3R_SFX_ANALYSIS.md "玻璃碎裂（重做）"）：
		/// · 时间：30ms 涨起 → 约 0.6s 维持在峰值附近、缓慢下降（一串持续闪烁，而不是"一下全响完"）；
		/// · 碎裂冲击：8~12 次，开头 0.02~0.2s 内每 10~25ms 一次，强度 0.5~1 随机；
		/// · 共鸣音：约 25 条，频率按原声分位数抽样（10/25/50/75/90% = 3.3/6.3/9.8/13.5/16.2kHz），
		///   起始时间分散在 0~0.65s（中位 0.24s：碎片不断落下），衰减到 -20dB 50~380ms（中位 120ms），软起音；
		/// · 其余一半能量是 3kHz 以上的噪声底（随整体包络）；1kHz 以下几乎没有。
		/// 最后用 GlassEq（倍频程均衡，离线对原声频谱自动校准得到）把整体频率分布对齐原声。
		/// size：0~1，整体时长 / 密度缩放（辉星消耗这种小碎裂用 0.5）。
		/// </summary>
		public void Glass(float t0, float amp, float size = 1f)
		{
			// 噪声底（3kHz 以上）
			Noise(t0, 0.35f * size + 0.05f, t => t < 0.02f ? t / 0.02f : MathF.Exp(-(t - 0.02f) / 0.08f), K(3200), 0.7f, F.High, amp * 0.12f);   // 很薄的噪声底（原声几乎全是共鸣线）
			// 碎裂冲击
			int cracks = 8 + (int)(4 * size * _r.NextDouble());
			float at = t0 + 0.015f;
			for (int i = 0; i < cracks && at < t0 + 0.21f * size + 0.02f; i++)
			{
				float a = amp * (0.5f + 0.5f * (float)_r.NextDouble());
				Noise(at, 0.02f, Atk(0.0008f, 0.004f), K(1500f + 1500f * (float)_r.NextDouble()), 0.8f, F.High, a * 0.7f);
				Noise(at, 0.035f, Atk(0.0008f, 0.009f), K(1100f + 600f * (float)_r.NextDouble()), 0.9f, F.Band, a * 0.45f);   // 碎裂的中频"身体"
				at += 0.01f + 0.015f * (float)_r.NextDouble();
			}
			// 共鸣音
			float[] q = { 1800f, 3298f, 6266f, 9786f, 13469f, 16220f, 17500f };
			float[] qp = { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 1f };
			int lines = (int)(60 * (0.4f + 0.6f * size));       // 原声里强线 ~25 条，加上弱线总共 ~60 条
			for (int i = 0; i < lines; i++)
			{
				float u = (float)_r.NextDouble();
				int k = 1;
				while (k < qp.Length - 1 && u > qp[k]) k++;
				float f = q[k - 1] * MathF.Pow(q[k] / q[k - 1], (u - qp[k - 1]) / (qp[k] - qp[k - 1]));
				float o = (float)_r.NextDouble();
				float on = t0 + (o < 0.6f ? 0.15f * size * o / 0.6f : 0.15f * size + 0.45f * size * (o - 0.6f) / 0.4f);   // 六成集中在开头 0.15s
				float d20 = 0.05f + 0.33f * MathF.Pow((float)_r.NextDouble(), 1.6f);            // -20dB 衰减时间
				float tau = d20 / 2.3f;
				float a = amp * 0.3f * MathF.Pow((float)_r.NextDouble(), 1.5f) * (on - t0 < 0.15f ? 1f : 0.6f);   // 强弱悬殊，后落下的碎片更轻
				Tone(on, tau * 6f, _ => f, Atk(0.002f, tau), a);
			}
		}

		/// <summary>倍频程峰值均衡（RBJ peaking biquad，Q≈1.4），gains 对应 63/125/250/500/1k/2k/4k/8k/16k Hz（dB）。</summary>
		public void Eq(float[] gains)
		{
			float[] centers = { 63f, 125f, 250f, 500f, 1000f, 2000f, 4000f, 8000f, 16000f };
			for (int c = 0; c < centers.Length; c++)
			{
				if (MathF.Abs(gains[c]) < 0.05f) continue;
				float A = MathF.Pow(10f, gains[c] / 40f), w = 2f * MathF.PI * MathF.Min(centers[c], SR * 0.45f) / SR, al = MathF.Sin(w) / (2f * 1.4f);
				float b0 = 1 + al * A, b1 = -2 * MathF.Cos(w), b2 = 1 - al * A, a0 = 1 + al / A, a1 = -2 * MathF.Cos(w), a2 = 1 - al / A;
				float x1 = 0, x2 = 0, y1 = 0, y2 = 0;
				for (int i = 0; i < B.Length; i++)
				{
					float x0 = B[i];
					float y0 = (b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2) / a0;
					x2 = x1; x1 = x0; y2 = y1; y1 = y0;
					B[i] = y0;
				}
			}
		}

		/// <summary>整体低通（两级一阶），把高频"亮"的部分压下去，声音更闷更厚。</summary>
		public void Lowpass(float fc)
		{
			float a = 1f - MathF.Exp(-2f * MathF.PI * fc / SR);
			float y1 = 0f, y2 = 0f;
			for (int i = 0; i < B.Length; i++)
			{
				y1 += a * (B[i] - y1);
				y2 += a * (y1 - y2);
				B[i] = y2;
			}
		}

		/// <summary>N 波：枪口冲击波的正负尖峰（几个采样宽），给出"啪"的锐利起头。</summary>
		public void Snap(float t0, float amp)
		{
			int s0 = (int)(t0 * SR);
			float[] w = { 0.6f, 1f, 0.8f, 0.3f, -0.4f, -0.9f, -1f, -0.6f, -0.2f };
			for (int i = 0; i < w.Length && s0 + i < B.Length; i++)
			{
				B[s0 + i] += w[i] * amp;
			}
		}
	}

	/// <summary>玻璃层的倍频程校准（tools/sfx/calibrate_glass.py 自动生成：让 Glass 的频率分布对齐 P3R 原声）。</summary>
	private static readonly float[] GlassEq = { 0.0f, 0.0f, 0.0f, 0.0f, 8.0f, 2.0f, 5.9f, 0.2f, -7.1f };

	/// <summary>只含玻璃层的独立缓冲（叠加到主缓冲之前先做校准均衡）。</summary>
	private static void AddGlass(Gen g, float t0, float amp, float size, int seed)
	{
		var glass = new Gen(g.B.Length / (float)SR, seed);
		glass.Glass(t0, amp, size);
		glass.Lowpass(14000f);                              // 压住 16kHz 以上（高通噪声一直延伸到 22kHz）
		glass.Eq(GlassEq);
		for (int i = 0; i < g.B.Length; i++)
		{
			g.B[i] += glass.B[i];
		}
	}

	private static Func<float, float> Atk(float a, float tau) => t => t < a ? t / a : MathF.Exp(-(t - a) / tau);
	private static Func<float, float> K(float v) => _ => v;

	private static float[] Synth(Snd s)
	{
		Gen g;
		switch (s)
		{
			case Snd.Gunshot:      // 召唤器：枪声（闷而有空间感，不是写实枪声）→ 紧接着"心灵碎裂"的玻璃声（主体，复用按原声校准的玻璃层）
				                       // → Persona 冲出的涌起风声 + Em 垫音 → 长混响。结构依据 research/P3R_SFX_ANALYSIS.md
				g = new Gen(1.8f, 1);
				g.Snap(0f, 0.8f);
				g.Noise(0, 0.04f, Atk(0.0002f, 0.008f), K(2200), 0.7f, F.Band, 0.7f);                                     // 枪声的"啪"（中频，不用高通）
				g.Noise(0, 0.18f, Atk(0.0004f, 0.035f), K(520), 0.85f, F.Band, 0.75f);                                    // 中低频冲击
				g.Tone(0, 0.35f, t => 95f * MathF.Exp(-t / 0.04f) + 42f, Atk(0.001f, 0.09f), 0.45f);                      // 低频重量
				g.Noise(0, 0.3f, Atk(0.001f, 0.07f), K(150), 1f, F.Low, 0.45f);
				foreach ((float d, float am) in new[] { (0.023f, 0.35f), (0.047f, 0.22f), (0.083f, 0.13f) })              // 早期反射（闷）
				{
					g.Noise(d, 0.05f, Atk(0.0005f, 0.012f), K(900), 0.8f, F.Band, am);
				}
				g.Noise(0.03f, 1.0f, t => t < 0.2f ? MathF.Pow(t / 0.2f, 1.5f) : MathF.Exp(-(t - 0.2f) / 0.18f),
					t => 400f + 1600f * MathF.Min(t / 0.2f, 1f), 0.8f, F.Band, 0.35f);                                   // Persona 冲出的风声（200ms 涌起）
				g.Noise(0.03f, 1.0f, t => t < 0.2f ? t / 0.2f : MathF.Exp(-(t - 0.2f) / 0.25f), K(170), 1f, F.Low, 0.4f);
				g.Warble(0.05f, 0.45f, 330f, 990f, 18f, 0.25f, t => MathF.Sin(MathF.PI * MathF.Min(t / 0.45f, 1f)), 0.06f);
				foreach (float f in new[] { 164.8f, 246.9f, 329.6f })                                                     // Em 垫音（Persona 现身）
				{
					g.Pad(0.08f, 1.6f, f, t => MathF.Min(t / 0.15f, 1f) * MathF.Exp(-t / 0.5f), 0.035f);
				}
				g.Noise(0.01f, 1.7f, Atk(0.02f, 0.5f), t => 1800f * MathF.Exp(-t / 0.5f) + 250f, 0.9f, F.Low, 0.22f);   // 空间尾音
				g.Lowpass(9000f);
				AddGlass(g, 0.025f, 1.15f, 1f, 21);                                                                      // 心灵碎裂（主体）
				return g.B;
			// ───── P3R 风格（依据 research/P3R_SFX_ANALYSIS.md 对原版系统音效的测量）─────
			// 词汇：快速大幅颤音的合成音 + 音高滑动、干净的三角波琶音（75~85ms 一个音）+ 回声、
			// 慢慢涌起（200~370ms）的宽频风声且低频占 ~25%。不用非谐波铃声和高频闪光颗粒（那种"亮"很廉价）。
			case Snd.TarotReveal:      // 塔罗牌浮现：涌起的风声 + 上扬颤音 + Em 琶音（带回声）
				g = new Gen(1.4f, 2);
				g.Noise(0, 0.5f, t => t < 0.2f ? MathF.Pow(t / 0.2f, 1.5f) : MathF.Exp(-(t - 0.2f) / 0.12f), t => 500f + 1800f * MathF.Min(t / 0.2f, 1f), 0.8f, F.Band, 0.45f);
				g.Noise(0, 0.5f, t => t < 0.2f ? t / 0.2f : MathF.Exp(-(t - 0.2f) / 0.15f), K(160), 1f, F.Low, 1.3f);
				g.Warble(0.02f, 0.3f, 330f, 990f, 18f, 0.3f, t => MathF.Sin(MathF.PI * MathF.Min(t / 0.3f, 1f)), 0.18f);
				g.Noise(0.12f, 0.04f, Atk(0.0006f, 0.012f), K(1600), 0.6f, F.Band, 0.35f);                               // 翻牌
				g.Arp(0.16f, 0.08f, new[] { 659.3f, 784f, 987.8f, 1318.5f }, 0.09f, 0.16f);                               // E5 G5 B5 E6
				g.Echo(0.14f, 0.38f);
				g.Lowpass(7000f);
				return g.B;
			case Snd.GlassOnly:
				g = new Gen(1.2f, 11);
				AddGlass(g, 0f, 1f, 1f, 12);
				return g.B;
			case Snd.TarotShatter:     // 塔罗牌碎裂：P3R 式玻璃碎裂为主体 + 一记不重的冲击 + 很轻的下坠颤音
				g = new Gen(1.3f, 3);
				g.Snap(0f, 0.4f);
				g.Tone(0, 0.3f, t => 110f * MathF.Exp(-t / 0.05f) + 55f, Atk(0.001f, 0.07f), 0.12f);
				g.Noise(0, 0.15f, Atk(0.0005f, 0.04f), K(900), 0.8f, F.Band, 0.15f);
				g.Warble(0.05f, 0.5f, 840f, 320f, 14f, 0.6f, t => MathF.Min(t / 0.03f, 1f) * MathF.Exp(-t / 0.2f), 0.04f);
				AddGlass(g, 0f, 1f, 1f, 13);
				return g.B;
			case Snd.StarGain:         // 辉星凝聚：短促上扬的颤音（原声 #47：495→1380Hz、27 次/秒）
				g = new Gen(0.7f, 4);
				g.Warble(0, 0.28f, 495f, 1380f, 27f, 0.3f, t => MathF.Min(t / 0.03f, 1f) * MathF.Exp(-t / 0.12f), 0.3f);
				g.Arp(0.12f, 0.07f, new[] { 1318.5f, 1975.5f }, 0.06f, 0.12f);
				g.Echo(0.1f, 0.3f);
				g.Lowpass(7000f);
				return g.B;
			case Snd.StarSpend:        // 辉星消耗：小块玻璃碎裂
				g = new Gen(0.7f, 5);
				g.Tone(0, 0.15f, t => 160f * MathF.Exp(-t / 0.03f) + 80f, Atk(0.001f, 0.04f), 0.06f);
				AddGlass(g, 0f, 0.8f, 0.45f, 14);
				return g.B;
			case Snd.CutIn:            // 切入大字：短风声 → 重击 + 三角波和弦重音 + 上冲颤音 → 回声
				g = new Gen(1.4f, 6);
				g.Noise(0, 0.07f, t => MathF.Pow(t / 0.07f, 2f), t => 400f + 2500f * (t / 0.07f), 0.6f, F.Band, 0.55f);
				g.Snap(0.06f, 0.8f);
				g.Noise(0.06f, 0.3f, Atk(0.0005f, 0.08f), K(140), 1f, F.Low, 0.42f);
				g.Tone(0.06f, 0.5f, t => 85f * MathF.Exp(-t / 0.06f) + 38f, Atk(0.001f, 0.15f), 0.32f);
				g.Noise(0.06f, 0.08f, Atk(0.0003f, 0.02f), K(1500), 0.6f, F.Band, 0.85f);
				foreach (float f in new[] { 329.6f, 392f, 493.9f, 659.3f })                                               // Em（三角波）
				{
					g.Warble(0.06f, 0.9f, f, f, 5f, 0.004f, Atk(0.003f, 0.26f), 0.17f);
				}
				g.Warble(0.06f, 0.25f, 185f, 676f, 19f, 0.16f, t => MathF.Min(t / 0.02f, 1f) * MathF.Exp(-t / 0.1f), 0.18f);   // 原声 #44
				g.Echo(0.13f, 0.32f);
				g.Lowpass(7500f);
				return g.B;
			case Snd.Weak:             // WEAK!：P3R 原版以玻璃碎裂为主 —— 玻璃碎裂主体 + 一记短促的"嗒"（落字）+ 很轻的上扬颤音
				g = new Gen(1.2f, 7);
				g.Snap(0f, 0.5f);
				g.Tone(0, 0.2f, t => 140f * MathF.Exp(-t / 0.03f) + 60f, Atk(0.001f, 0.045f), 0.12f);
				g.Noise(0, 0.05f, Atk(0.0003f, 0.012f), K(1500), 0.5f, F.Band, 0.2f);
				g.Warble(0, 0.15f, 300f, 1100f, 22f, 0.25f, t => MathF.Min(t / 0.01f, 1f) * MathF.Exp(-t / 0.06f), 0.05f);
				AddGlass(g, 0f, 1f, 1f, 15);
				return g.B;
			case Snd.Anvil:            // 锤击铁砧：非谐波金属泛音（铁砧常见比例 1 / 1.58 / 2.31 / 2.98 / 3.93）+ 两两轻微失谐产生拍音；
				                       // 起音是很短的亮噪声"咔"，加一点 200Hz 的击打感（不沉）和几颗高频火星，整体 0.6s 内衰减完
				g = new Gen(0.75f, 9);
				g.Snap(0f, 0.55f);
				g.Noise(0, 0.02f, Atk(0.0002f, 0.004f), K(5200), 0.5f, F.Band, 0.7f);
				g.Noise(0, 0.05f, Atk(0.0004f, 0.012f), K(1800), 0.7f, F.Band, 0.35f);
				g.Tone(0, 0.12f, t => 230f * MathF.Exp(-t / 0.02f) + 170f, Atk(0.0008f, 0.03f), 0.22f);
				foreach ((float r, float a, float d) in new[] { (1f, 1f, 0.32f), (1.58f, 0.6f, 0.24f), (2.31f, 0.45f, 0.18f), (2.98f, 0.3f, 0.12f), (3.93f, 0.18f, 0.08f) })
				{
					float f = 1180f * r;
					g.Tone(0, d * 5f, _ => f, Atk(0.0008f, d), 0.16f * a);
					g.Tone(0, d * 5f, _ => f * 1.004f, Atk(0.0008f, d * 0.9f), 0.1f * a);
				}
				g.Pings(0.01f, 0.25f, 7, 5500f, 9000f, 0.05f);
				g.Echo(0.07f, 0.18f, 0.5f);
				g.Lowpass(11000f);
				return g.B;
			case Snd.ThanatosSlash:    // 塔纳托斯斩击：涌起的低沉风声 → 巨响 + 金属刮擦 + 低沉下坠颤音 + 不协和低音 → 长回声
				g = new Gen(2.0f, 8);
				g.Noise(0, 0.2f, t => MathF.Pow(t / 0.2f, 1.6f), t => 200f + 1600f * (t / 0.2f), 0.6f, F.Band, 0.6f);
				g.Noise(0, 0.2f, t => t / 0.2f, K(140), 1f, F.Low, 0.3f);
				g.Snap(0.18f, 0.9f);
				g.Noise(0.18f, 0.08f, Atk(0.0002f, 0.02f), K(1300), 0.6f, F.Band, 0.95f);
				g.Noise(0.18f, 0.7f, Atk(0.001f, 0.18f), K(110), 1f, F.Low, 0.3f);
				g.Tone(0.18f, 1.0f, t => 70f * MathF.Exp(-t / 0.09f) + 28f, Atk(0.002f, 0.28f), 0.24f);
				g.Noise(0.18f, 0.5f, Atk(0.003f, 0.13f), t => 2400f - 1200f * MathF.Min(t / 0.45f, 1f), 0.2f, F.Band, 0.42f);
				g.Warble(0.2f, 1.0f, 300f, 95f, 14f, 0.6f, t => MathF.Min(t / 0.03f, 1f) * MathF.Exp(-t / 0.35f), 0.18f);
				g.Pad(0.18f, 1.7f, 82.4f, Atk(0.03f, 0.6f), 0.06f, harmonics: 4);
				g.Pad(0.18f, 1.7f, 116.5f, Atk(0.04f, 0.55f), 0.05f, harmonics: 4);
				g.Echo(0.17f, 0.3f);
				g.Lowpass(6000f);
				return g.B;
		}
		return new float[1];
	}
}
