using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace P3Makoto.Audio;

public enum Snd
{
	Gunshot,
	TarotReveal,    // 塔罗牌浮现：上扬的气流 + 翻牌 + 钟琴琶音 + 低音垫
	TarotShatter,   // 塔罗牌碎裂：脆响 + 几十片玻璃叮当 + 低频冲击 + 确认和弦 + 空间尾音
	StarGain,       // 辉星凝聚（轻）
	StarSpend,      // 辉星消耗（小碎裂）
	CutIn,          // "CRITICAL" / "ONE MORE!" 切入：风声冲入 + 重击 + 和弦重音
	Weak,           // WEAK!：金属高音"叮"+ 上扬滑音 + 微光
	ThanatosSlash,  // 塔纳托斯斩击：低沉风啸 + 巨响 + 金属刮擦 + 不协和低音
}

/// <summary>
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
		foreach (Snd s in Enum.GetValues<Snd>())
		{
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
			byte[] data = Pcm.TryGetValue(s, out byte[]? d) ? d : Pcm.GetOrAdd(s, Encode(Synth(s), Drive(s)));
			w = new AudioStreamWav { Format = AudioStreamWav.FormatEnum.Format16Bits, MixRate = SR, Stereo = false, Data = data };
			Streams[s] = w;
		}
		return w;
	}

	/// <summary>软削波推动量：越大越"满"越响（压缩感更强）。</summary>
	private static float Drive(Snd s) => s switch
	{
		Snd.Gunshot => 2.6f,
		Snd.TarotShatter or Snd.CutIn or Snd.ThanatosSlash => 2.2f,
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
		/// 玻璃碎裂（按 P3R 原声测量：research/P3R_SFX_ANALYSIS.md）：
		/// 开头 ~130ms 内 cracks 次宽频碎裂冲击（每次 3~8ms 的高通噪声，越往后越弱）；
		/// 同时 lines 条共鸣音（fLo~fHi；原声中位约 10kHz，游戏内反馈太刺耳，降到约 1.6~8kHz）在 0~100ms 内先后响起（大多在开头），各自衰减 70~280ms。
		/// 必须在整体 Lowpass 之后叠加，否则会被闷掉。
		/// </summary>
		public void Glass(float t0, int cracks, int lines, float fLo, float fHi, float amp, float ring = 1f)
		{
			for (int i = 0; i < cracks; i++)
			{
				float u = i / (float)Math.Max(cracks - 1, 1);
				float at = t0 + 0.13f * u + 0.012f * (float)_r.NextDouble();
				float a = amp * (1f - 0.65f * u) * (0.7f + 0.3f * (float)_r.NextDouble());
				float len = 0.003f + 0.005f * (float)_r.NextDouble();
				Noise(at, len * 4f, Atk(0.0002f, len), K(1500f + 2000f * (float)_r.NextDouble()), 0.7f, F.High, a);
			}
			for (int i = 0; i < lines; i++)
			{
				float f = fLo * MathF.Pow(fHi / fLo, (float)_r.NextDouble());
				float on = (float)_r.NextDouble();
				float at = t0 + 0.1f * on * on;                       // 多数集中在开头
				float tau = (0.03f + 0.09f * (float)_r.NextDouble()) * ring;
				float a = amp * 0.32f * (0.4f + 0.6f * (float)_r.NextDouble());
				Tone(at, tau * 6f, _ => f, Atk(0.0004f, tau), a);
			}
			Noise(t0, 0.12f, Atk(0.0005f, 0.02f * ring), K(4000), 0.5f, F.High, amp * 0.1f);    // 碎屑沙沙声（很轻：原声的碎裂是一下一下分开的）
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

	private static Func<float, float> Atk(float a, float tau) => t => t < a ? t / a : MathF.Exp(-(t - a) / tau);
	private static Func<float, float> K(float v) => _ => v;

	private static float[] Synth(Snd s)
	{
		Gen g;
		switch (s)
		{
			case Snd.Gunshot:      // 召唤器枪声：击锤 + 高频"啪"（不是鼓）+ 噪声冲击 + 次低频 + 回声 + 长尾；随后灵魂冲出的风声、E 小调共鸣、微光
				g = new Gen(1.6f, 1);
				g.Snap(0.0f, 1.0f);
				g.Noise(0, 0.05f, Atk(0.0002f, 0.005f), K(900), 0.6f, F.High, 0.75f);          // 宽频爆裂
				g.Noise(0, 0.14f, Atk(0.0003f, 0.026f), K(1700), 0.8f, F.Band, 1.1f);           // 中频"啪"的身体
				g.Noise(0, 0.25f, Atk(0.0005f, 0.05f), K(480), 0.9f, F.Band, 0.8f);             // 中低频冲击（噪声，不成音高）
				g.Noise(0, 0.5f, Atk(0.002f, 0.1f), K(170), 1f, F.Low, 0.9f);                   // 次低频轰鸣
				g.Tone(0, 0.2f, t => 85f * MathF.Exp(-t / 0.03f) + 48f, Atk(0.001f, 0.05f), 0.25f);
				foreach ((float d, float a) in new[] { (0.021f, 0.45f), (0.043f, 0.3f), (0.077f, 0.2f), (0.13f, 0.13f), (0.21f, 0.07f) })   // 早期反射
				{
					g.Noise(d, 0.05f, Atk(0.0004f, 0.01f), K(1300), 0.7f, F.High, a);
				}
				g.Noise(0.005f, 1.55f, Atk(0.01f, 0.42f), t => 3000f * MathF.Exp(-t / 0.35f) + 450f, 0.9f, F.Low, 0.32f);   // 空间长尾
				g.Noise(0, 0.02f, Atk(0.0002f, 0.003f), K(4500), 0.25f, F.Band, 0.5f);           // 击锤的金属"咔"
				g.Tone(0, 0.15f, _ => 2830f, Atk(0.0004f, 0.03f), 0.06f);
				g.Noise(0.08f, 0.6f, t => { float u = Math.Clamp(t / 0.55f, 0f, 1f); return MathF.Pow(MathF.Sin(MathF.PI * u), 2f) * (1f - 0.4f * u); },
					t => 600f + 3200f * Math.Clamp(t / 0.45f, 0f, 1f), 0.4f, F.Band, 0.22f);                 // 灵魂冲出的风声（向上扫）
				foreach ((float f, float a) in new[] { (164.8f, 0.07f), (246.9f, 0.05f), (329.6f, 0.05f), (493.9f, 0.025f) })   // E 小调共鸣：Persona 现身
				{
					g.Tone(0.06f, 1.5f, t => f * (1f + 0.003f * MathF.Sin(t * 29f)), t => MathF.Min(t / 0.18f, 1f) * MathF.Exp(-t / 0.55f), a);
					g.Tone(0.06f, 1.5f, _ => f * 1.004f, t => MathF.Min(t / 0.18f, 1f) * MathF.Exp(-t / 0.55f), a * 0.6f);
				}
				g.Shimmer(0.04f, 0.7f, 34, 3200f, 7500f, 0.05f);                                  // 微光余韵
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
			case Snd.TarotShatter:     // 塔罗牌碎裂：玻璃碎裂为主（两波：主碎裂 + 0.09s 后碎片再碎一次）+ 闷重的底 + 下坠颤音
				g = new Gen(1.4f, 3);
				g.Snap(0f, 0.6f);
				g.Tone(0, 0.4f, t => 100f * MathF.Exp(-t / 0.06f) + 45f, Atk(0.001f, 0.1f), 0.3f);
				g.Noise(0, 0.3f, Atk(0.0005f, 0.08f), K(170), 1f, F.Low, 0.35f);
				g.Noise(0, 0.2f, Atk(0.0005f, 0.05f), K(900), 0.8f, F.Band, 0.5f);
				g.Warble(0.04f, 0.6f, 840f, 320f, 14f, 0.6f, t => MathF.Min(t / 0.03f, 1f) * MathF.Exp(-t / 0.25f), 0.08f);
				g.Echo(0.12f, 0.25f);
				g.Lowpass(6000f);
				g.Glass(0f, 8, 24, 1600f, 7500f, 0.9f, ring: 1.3f);
				g.Glass(0.09f, 4, 12, 2000f, 7000f, 0.45f);
				g.Lowpass(9000f);
				return g.B;
			case Snd.StarGain:         // 辉星凝聚：短促上扬的颤音（原声 #47：495→1380Hz、27 次/秒）
				g = new Gen(0.7f, 4);
				g.Warble(0, 0.28f, 495f, 1380f, 27f, 0.3f, t => MathF.Min(t / 0.03f, 1f) * MathF.Exp(-t / 0.12f), 0.3f);
				g.Arp(0.12f, 0.07f, new[] { 1318.5f, 1975.5f }, 0.06f, 0.12f);
				g.Echo(0.1f, 0.3f);
				g.Lowpass(7000f);
				return g.B;
			case Snd.StarSpend:        // 辉星消耗：小块玻璃碎裂 + 一点下坠颤音
				g = new Gen(0.7f, 5);
				g.Tone(0, 0.2f, t => 150f * MathF.Exp(-t / 0.04f) + 70f, Atk(0.001f, 0.05f), 0.15f);
				g.Warble(0.01f, 0.3f, 1100f, 420f, 16f, 0.45f, t => MathF.Min(t / 0.02f, 1f) * MathF.Exp(-t / 0.1f), 0.1f);
				g.Lowpass(6000f);
				g.Glass(0f, 4, 12, 1800f, 7000f, 0.7f, ring: 0.8f);
				g.Lowpass(9000f);
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
			case Snd.Weak:             // WEAK!：P3R 原版里以玻璃碎裂为主 —— 一记短促重击 + 大片玻璃碎裂（两波）+ 轻的上扬颤音
				g = new Gen(1.0f, 7);
				g.Snap(0f, 0.7f);
				g.Tone(0, 0.25f, t => 130f * MathF.Exp(-t / 0.035f) + 55f, Atk(0.001f, 0.06f), 0.3f);
				g.Noise(0, 0.08f, Atk(0.0003f, 0.02f), K(1500), 0.5f, F.Band, 0.45f);
				g.Warble(0, 0.16f, 300f, 1100f, 22f, 0.25f, t => MathF.Min(t / 0.01f, 1f) * MathF.Exp(-t / 0.07f), 0.12f);
				g.Echo(0.1f, 0.22f);
				g.Lowpass(7000f);
				g.Glass(0f, 9, 28, 1600f, 8000f, 1.0f, ring: 1.2f);
				g.Glass(0.07f, 4, 12, 2200f, 7500f, 0.45f);
				g.Lowpass(9000f);
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
