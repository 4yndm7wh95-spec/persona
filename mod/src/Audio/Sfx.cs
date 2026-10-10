using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace P3Makoto.Audio;

public enum Snd
{
	Gunshot, Shatter, Cock, SwordDraw, Swing, SlashHit, Blunt, Dissolve, Shield, Sparkle, Summon,
	Fire, Ice, Zap, Holy, Curse, Boom, Swipe,
}

/// <summary>
/// 程序合成音效（不需要音频素材）：启动时在后台线程合成 PCM，播放走 Godot 的 "SFX" 总线 —— 和游戏自己的
/// 临时音效（NDebugAudioManager）同一条总线，跟随游戏音量设置。
/// 合成手法：滤波噪声（状态变量滤波器）+ 正弦分音 + 指数包络；玻璃碎裂 = 大量随机高频"叮"颗粒。
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
		foreach (Snd s in Enum.GetValues<Snd>())
		{
			try
			{
				Pcm.TryAdd(s, Encode(Synth(s)));
			}
			catch (Exception e)
			{
				GD.PrintErr($"{ModEntry.Tag} synth {s} failed: {e}");
			}
		}
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
		if (LastPlay.TryGetValue(s, out ulong last) && now - last < 45)
		{
			return;
		}
		LastPlay[s] = now;
		_bus ??= AudioServer.GetBusIndex("SFX") >= 0 ? new StringName("SFX") : new StringName("Master");
		AudioStreamPlayer p = new()
		{
			Stream = Get(s),
			Bus = _bus,
			VolumeDb = Mathf.LinearToDb(Mathf.Max(volume * Master, 0.0001f)),
			PitchScale = pitch * (float)GD.RandRange(0.97, 1.03),
			ProcessMode = Node.ProcessModeEnum.Always,
		};
		tree.Root.AddChild(p);
		p.Finished += p.QueueFree;
		p.Play();
	});

	private static AudioStreamWav Get(Snd s)
	{
		if (!Streams.TryGetValue(s, out AudioStreamWav? w))
		{
			byte[] data = Pcm.TryGetValue(s, out byte[]? d) ? d : Pcm.GetOrAdd(s, Encode(Synth(s)));
			w = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = SR, Stereo = false, Data = data };
			Streams[s] = w;
		}
		return w;
	}

	private static byte[] Encode(float[] x)
	{
		float peak = 1e-6f;
		foreach (float v in x)
		{
			peak = MathF.Max(peak, MathF.Abs(v));
		}
		float gain = 1.6f / peak;
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

		public float Rnd(float a, float b) => a + (float)_r.NextDouble() * (b - a);

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

		public void Ping(float t0, float f, float tau, float amp, float bright = 1f)
		{
			Tone(t0, tau * 5f, _ => f, Atk(0.0006f, tau), amp);
			Tone(t0, tau * 3f, _ => f * Rnd(1.48f, 1.56f), Atk(0.0006f, tau * 0.6f), amp * 0.45f * bright);
			Tone(t0, tau * 2f, _ => f * Rnd(2.6f, 2.9f), Atk(0.0006f, tau * 0.35f), amp * 0.25f * bright);
		}

		/// <summary>玻璃碎裂颗粒：count 个随机高频"叮"，越往后越稀疏越轻。</summary>
		public void Glass(float t0, float spread, int count, float fLo, float fHi, float amp)
		{
			for (int i = 0; i < count; i++)
			{
				float u = Rnd(0f, 1f);
				float t = t0 + spread * u * u;
				Ping(t, Rnd(fLo, fHi), Rnd(0.012f, 0.06f), amp * Rnd(0.35f, 1f) * (1f - 0.7f * u));
			}
		}
	}

	private static Func<float, float> Exp(float tau) => t => MathF.Exp(-t / tau);
	private static Func<float, float> Atk(float a, float tau) => t => t < a ? t / a : MathF.Exp(-(t - a) / tau);
	private static Func<float, float> Bell(float dur) => t => { float u = Math.Clamp(t / dur, 0f, 1f); float s = MathF.Sin(MathF.PI * u); return s * s; };
	private static Func<float, float> K(float v) => _ => v;

	private static float[] Synth(Snd s)
	{
		Gen g;
		switch (s)
		{
			case Snd.Gunshot:      // 召唤器：干脆的爆裂 + 低频"咚" + 房间尾音
				g = new Gen(0.9f, 1);
				g.Noise(0, 0.06f, Atk(0.0006f, 0.01f), K(6000), 0.7f, F.High, 1.0f);
				g.Noise(0, 0.25f, Atk(0.001f, 0.045f), K(1400), 0.8f, F.Low, 0.8f);
				g.Tone(0, 0.45f, t => 150f * MathF.Exp(-t / 0.04f) + 46f, Atk(0.002f, 0.1f), 1.0f);
				g.Noise(0.01f, 0.88f, Atk(0.02f, 0.28f), K(800), 1f, F.Low, 0.2f);
				return g.B;
			case Snd.Shatter:      // 玻璃 / 灵魂碎裂：一声脆响 + 一大片叮叮声 + 微光余韵
				g = new Gen(1.4f, 2);
				g.Noise(0, 0.3f, Atk(0.0008f, 0.06f), K(5000), 0.35f, F.Band, 0.7f);
				g.Glass(0.005f, 0.55f, 110, 2400f, 7200f, 0.22f);
				g.Noise(0.03f, 1.3f, Atk(0.02f, 0.3f), K(6500), 0.4f, F.Band, 0.035f);
				return g.B;
			case Snd.Cock:         // 召唤器举起：两下机械咔哒
				g = new Gen(0.2f, 3);
				foreach (float t in new[] { 0f, 0.065f })
				{
					g.Noise(t, 0.03f, Atk(0.0003f, 0.004f), K(3800), 0.3f, F.Band, 0.8f);
					g.Tone(t, 0.04f, K(1900), Exp(0.006f), 0.25f);
				}
				return g.B;
			case Snd.SwordDraw:    // 剑凝成：上扬的刮擦 → 金属长鸣（非谐分音）
				g = new Gen(1.0f, 4);
				g.Noise(0, 0.2f, t => t < 0.16f ? t / 0.16f : MathF.Exp(-(t - 0.16f) / 0.015f), t => 2500f + 30000f * t, 0.35f, F.Band, 0.45f);
				float[] fr = { 2093f, 3141f, 4407f, 5870f, 7240f };
				float[] am = { 0.3f, 0.22f, 0.15f, 0.1f, 0.06f };
				float[] ta = { 0.55f, 0.42f, 0.32f, 0.24f, 0.16f };
				for (int i = 0; i < fr.Length; i++)
				{
					float f0 = fr[i];
					g.Tone(0.16f, 0.84f, t => f0 * (1f + 0.0015f * MathF.Sin(t * 37f)), Atk(0.002f, ta[i]), am[i]);
				}
				return g.B;
			case Snd.Swing:        // 挥剑破风：中心频率扫过的带通噪声
				g = new Gen(0.32f, 5);
				g.Noise(0, 0.3f, t => MathF.Pow(Bell(0.26f)(t), 0.8f), t => 450f + 2000f * MathF.Sin(MathF.PI * Math.Clamp(t / 0.26f, 0f, 1f)), 0.28f, F.Band, 1f);
				return g.B;
			case Snd.SlashHit:     // 斩中：低频冲击 + 撕裂噪声 + 一点刃鸣
				g = new Gen(0.55f, 6);
				g.Tone(0, 0.35f, t => 120f * MathF.Exp(-t / 0.035f) + 52f, Atk(0.001f, 0.075f), 1.0f);
				g.Noise(0, 0.15f, Atk(0.0005f, 0.032f), K(2600), 0.7f, F.Low, 0.8f);
				g.Noise(0, 0.1f, Atk(0.0004f, 0.014f), K(6500), 0.6f, F.High, 0.5f);
				g.Tone(0.002f, 0.4f, K(3350f), Exp(0.09f), 0.07f);
				return g.B;
			case Snd.Blunt:
				g = new Gen(0.5f, 7);
				g.Tone(0, 0.45f, t => 95f * MathF.Exp(-t / 0.05f) + 42f, Atk(0.001f, 0.12f), 1.0f);
				g.Noise(0, 0.2f, Atk(0.001f, 0.055f), K(900), 0.8f, F.Low, 0.9f);
				return g.B;
			case Snd.Dissolve:     // 武器消散：稀疏的细碎闪光声
				g = new Gen(0.9f, 8);
				g.Glass(0.0f, 0.5f, 26, 3500f, 8000f, 0.07f);
				g.Noise(0, 0.7f, Bell(0.6f), K(5200), 0.5f, F.Band, 0.08f);
				return g.B;
			case Snd.Shield:       // 护盾：玻璃和弦
				g = new Gen(1.0f, 9);
				foreach ((float f, float a) in new[] { (1318.5f, 0.3f), (1975.5f, 0.24f), (2637f, 0.18f), (3951f, 0.1f) })
				{
					g.Tone(0, 1.0f, K(f), Atk(0.003f, 0.38f), a);
				}
				g.Noise(0, 0.08f, Atk(0.0005f, 0.012f), K(5000), 0.4f, F.Band, 0.3f);
				return g.B;
			case Snd.Sparkle:      // 星光：上行琶音
				g = new Gen(1.0f, 10);
				float[] notes = { 2637f, 3136f, 3951f, 5274f };
				for (int i = 0; i < notes.Length; i++)
				{
					g.Ping(i * 0.06f, notes[i], 0.22f, 0.22f, 0.5f);
				}
				return g.B;
			case Snd.Summon:       // 人格面具降临：倒吸的风声 → 钟鸣和弦（E 小调）
				g = new Gen(2.2f, 11);
				g.Noise(0, 0.4f, t => t < 0.34f ? MathF.Pow(t / 0.34f, 2.5f) : MathF.Exp(-(t - 0.34f) / 0.03f), t => 800f + 6000f * t, 0.5f, F.Band, 0.5f);
				foreach ((float f, float a, float tau) in new[] { (329.6f, 0.3f, 1.2f), (659.3f, 0.3f, 1.0f), (987.8f, 0.2f, 0.8f), (1318.5f, 0.14f, 0.6f), (1819.6f, 0.08f, 0.35f), (3560f, 0.05f, 0.2f) })
				{
					g.Tone(0.34f, 1.86f, t => f * (1f + 0.002f * MathF.Sin(t * 31f)), Atk(0.003f, tau), a);
					g.Tone(0.34f, 1.86f, K(f * 1.003f), Atk(0.003f, tau), a * 0.5f);
				}
				g.Glass(0.36f, 0.6f, 24, 3000f, 7000f, 0.05f);
				return g.B;
			case Snd.Fire:         // Agi：火焰轰燃 + 噼啪
				g = new Gen(1.0f, 12);
				g.Noise(0, 0.95f, Atk(0.03f, 0.3f), t => 500f + 2500f * MathF.Exp(-t / 0.18f), 0.9f, F.Low, 0.9f);
				for (int i = 0; i < 40; i++)
				{
					g.Noise(g.Rnd(0f, 0.7f), 0.01f, Exp(0.0025f), K(g.Rnd(2500f, 6000f)), 0.5f, F.Band, g.Rnd(0.1f, 0.35f));
				}
				return g.B;
			case Snd.Ice:          // Bufu：冰裂 + 冰晶叮当
				g = new Gen(1.0f, 13);
				g.Noise(0, 0.05f, Atk(0.0004f, 0.012f), K(5000), 0.6f, F.High, 0.9f);
				g.Tone(0, 0.3f, t => 170f * MathF.Exp(-t / 0.05f) + 80f, Atk(0.001f, 0.07f), 0.6f);
				g.Glass(0.01f, 0.4f, 50, 3000f, 7000f, 0.18f);
				return g.B;
			case Snd.Zap:          // Zio：电流噼啦（随机跳相的锯齿波）
			{
				g = new Gen(0.7f, 14);
				float ph = 0f, f = 110f;
				for (int i = 0; i < g.B.Length; i++)
				{
					float t = i / (float)SR;
					if (i % 180 == 0)
					{
						f = g.Rnd(70f, 260f);
						ph = g.Rnd(0f, 1f);
					}
					ph = (ph + f / SR) % 1f;
					g.B[i] += (2f * ph - 1f) * Atk(0.002f, 0.16f)(t) * 0.5f;
				}
				g.Noise(0, 0.05f, Atk(0.0004f, 0.01f), K(6000), 0.5f, F.High, 0.9f);
				g.Noise(0, 0.6f, Atk(0.002f, 0.12f), K(4500), 0.4f, F.Band, 0.35f);
				return g.B;
			}
			case Snd.Holy:         // Hama：明亮的和声 + 光点
				g = new Gen(1.4f, 15);
				foreach ((float f, float a) in new[] { (880f, 0.25f), (1318.5f, 0.2f), (1760f, 0.16f), (2637f, 0.1f) })
				{
					g.Tone(0, 1.4f, t => f * (1f + 0.003f * MathF.Sin(t * 33f)), t => MathF.Min(t / 0.08f, 1f) * MathF.Exp(-t / 0.5f), a);
				}
				g.Glass(0.05f, 0.6f, 20, 3500f, 7500f, 0.06f);
				return g.B;
			case Snd.Curse:        // Mudo：低沉拍频嗡鸣 + 不协和
				g = new Gen(1.4f, 16);
				foreach ((float f, float a) in new[] { (55f, 0.5f), (58.3f, 0.45f), (110f, 0.25f), (233f, 0.08f), (247f, 0.08f) })
				{
					g.Tone(0, 1.4f, K(f), t => MathF.Min(t / 0.12f, 1f) * MathF.Exp(-t / 0.45f), a);
				}
				g.Noise(0, 1.0f, t => MathF.Min(t / 0.25f, 1f) * MathF.Exp(-t / 0.3f), K(380), 0.9f, F.Low, 0.5f);
				return g.B;
			case Snd.Boom:         // Megido / 陨石：大爆炸
				g = new Gen(1.8f, 17);
				g.Tone(0, 1.2f, t => 75f * MathF.Exp(-t / 0.2f) + 30f, Atk(0.003f, 0.4f), 1.0f);
				g.Noise(0, 1.0f, Atk(0.002f, 0.3f), t => 3200f * MathF.Exp(-t / 0.08f) + 280f, 0.8f, F.Low, 0.9f);
				g.Noise(0.05f, 1.7f, Atk(0.05f, 0.6f), K(180), 1f, F.Low, 0.4f);
				return g.B;
			case Snd.Swipe:        // UI 斜带划入
				g = new Gen(0.4f, 18);
				g.Noise(0, 0.3f, Bell(0.24f), t => 1200f + 16000f * t, 0.45f, F.Band, 0.6f);
				g.Tone(0.2f, 0.2f, K(1760f), Atk(0.002f, 0.05f), 0.08f);
				return g.B;
		}
		return new float[1];
	}
}
