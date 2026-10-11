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
	GlassOnly,      // 只有玻璃层（校准用，游戏里不播放）
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
		Snd.CutIn or Snd.ThanatosSlash => 2.2f,
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
