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

		/// <summary>钟琴 / 铃声：非谐波泛音（1, 2, 2.76, 5.4, 8.9 倍），高泛音衰减更快。</summary>
		public void Bell(float t0, float f, float decay, float amp)
		{
			(float r, float a, float d)[] parts = { (1f, 1f, 1f), (2f, 0.5f, 0.7f), (2.76f, 0.35f, 0.45f), (5.4f, 0.18f, 0.25f), (8.93f, 0.08f, 0.15f) };
			foreach ((float r, float a, float d) in parts)
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
			case Snd.TarotReveal:
				g = new Gen(1.0f, 2);
				g.Noise(0, 0.12f, t => MathF.Pow(t / 0.12f, 2.2f), t => 600f + 4400f * (t / 0.12f), 0.5f, F.Band, 0.35f);   // 上扬的气流
				g.Snap(0.115f, 0.35f);
				g.Noise(0.115f, 0.035f, Atk(0.0003f, 0.008f), K(3200), 0.5f, F.High, 0.45f);                              // 翻牌"啪"
				foreach ((float d, float f) in new[] { (0.12f, 659.3f), (0.16f, 987.8f), (0.2f, 1318.5f), (0.25f, 1568f) })   // E 小调琶音
				{
					g.Bell(d, f, 0.32f, 0.16f);
				}
				g.Tone(0.1f, 0.85f, _ => 82.4f, Atk(0.04f, 0.32f), 0.13f);                                              // 低音垫 E2
				g.Tone(0.1f, 0.85f, _ => 164.8f, Atk(0.05f, 0.3f), 0.06f);
				g.Shimmer(0.12f, 0.55f, 22, 3500f, 7500f, 0.045f);
				g.Noise(0.12f, 0.8f, Atk(0.01f, 0.25f), t => 2600f * MathF.Exp(-t / 0.3f) + 500f, 0.9f, F.Low, 0.08f);  // 空间
				return g.B;
			case Snd.TarotShatter:
				g = new Gen(1.4f, 3);
				g.Snap(0f, 0.9f);
				g.Noise(0, 0.06f, Atk(0.0002f, 0.012f), K(2600), 0.6f, F.High, 0.9f);                                    // 脆响
				g.Noise(0, 0.26f, Atk(0.0005f, 0.06f), K(4200), 0.3f, F.Band, 0.45f);                                     // 玻璃本体
				g.Noise(0, 0.16f, Atk(0.0005f, 0.035f), K(900), 0.7f, F.Band, 0.55f);                                     // 中频碎裂
				g.Pings(0.004f, 0.5f, 46, 2400f, 7200f, 0.11f);                                                          // 碎片叮当
				g.Tone(0, 0.35f, t => 110f * MathF.Exp(-t / 0.05f) + 44f, Atk(0.001f, 0.09f), 0.55f);                    // 低频冲击
				g.Noise(0, 0.3f, Atk(0.001f, 0.07f), K(150), 1f, F.Low, 0.7f);
				g.Bell(0.03f, 659.3f, 0.5f, 0.09f);                                                                       // 确认和弦 E5 / B5
				g.Bell(0.05f, 987.8f, 0.45f, 0.07f);
				g.Noise(0.01f, 1.3f, Atk(0.01f, 0.35f), t => 4500f * MathF.Exp(-t / 0.3f) + 600f, 0.9f, F.Low, 0.16f);   // 尾音
				g.Shimmer(0.1f, 0.9f, 26, 4000f, 7500f, 0.035f);
				return g.B;
			case Snd.StarGain:
				g = new Gen(0.7f, 4);
				g.Noise(0, 0.12f, t => MathF.Pow(t / 0.12f, 2f), t => 1200f + 4000f * (t / 0.12f), 0.5f, F.Band, 0.2f);
				g.Bell(0.11f, 1318.5f, 0.22f, 0.14f);
				g.Bell(0.14f, 1975.5f, 0.18f, 0.09f);
				g.Shimmer(0.11f, 0.35f, 10, 4000f, 7500f, 0.04f);
				return g.B;
			case Snd.StarSpend:
				g = new Gen(0.8f, 5);
				g.Snap(0f, 0.4f);
				g.Noise(0, 0.04f, Atk(0.0002f, 0.01f), K(3000), 0.6f, F.High, 0.5f);
				g.Pings(0.003f, 0.3f, 16, 3000f, 7200f, 0.09f);
				g.Tone(0, 0.2f, t => 160f * MathF.Exp(-t / 0.04f) + 70f, Atk(0.001f, 0.05f), 0.3f);
				g.Bell(0.02f, 987.8f, 0.3f, 0.07f);
				return g.B;
			case Snd.CutIn:
				g = new Gen(1.2f, 6);
				g.Noise(0, 0.06f, t => MathF.Pow(t / 0.06f, 2f), t => 500f + 5000f * (t / 0.06f), 0.45f, F.Band, 0.45f);      // 风声冲入
				g.Snap(0.06f, 1f);
				g.Noise(0.06f, 0.05f, Atk(0.0002f, 0.01f), K(1800), 0.6f, F.High, 0.9f);                                   // 重击
				g.Noise(0.06f, 0.2f, Atk(0.0004f, 0.045f), K(600), 0.8f, F.Band, 0.7f);
				g.Tone(0.06f, 0.45f, t => 90f * MathF.Exp(-t / 0.05f) + 40f, Atk(0.001f, 0.12f), 0.7f);                    // 次低频
				foreach (float f in new[] { 329.6f, 392f, 493.9f, 587.3f })                                               // Em7 和弦重音（锯齿感：叠 3 个谐波）
				{
					for (int h = 1; h <= 3; h++)
					{
						g.Tone(0.06f, 0.9f, _ => f * h, Atk(0.004f, 0.28f / h), 0.07f / h);
					}
				}
				g.Noise(0.06f, 1.0f, Atk(0.01f, 0.3f), t => 3500f * MathF.Exp(-t / 0.3f) + 500f, 0.9f, F.Low, 0.18f);
				g.Shimmer(0.12f, 0.5f, 14, 3500f, 7500f, 0.04f);
				return g.B;
			case Snd.Weak:
				g = new Gen(0.9f, 7);
				g.Snap(0f, 0.6f);
				g.Noise(0, 0.03f, Atk(0.0002f, 0.006f), K(5000), 0.4f, F.High, 0.6f);
				g.Tone(0, 0.08f, t => 900f + 1800f * MathF.Min(t / 0.06f, 1f), Atk(0.002f, 0.04f), 0.12f);               // 上扬滑音
				g.Bell(0.01f, 1760f, 0.28f, 0.2f);                                                                        // 金属"叮"
				g.Bell(0.03f, 2637f, 0.22f, 0.12f);
				g.Tone(0, 0.3f, t => 140f * MathF.Exp(-t / 0.04f) + 60f, Atk(0.001f, 0.07f), 0.35f);                     // 重量
				g.Shimmer(0.04f, 0.5f, 18, 4000f, 7500f, 0.045f);
				return g.B;
			case Snd.ThanatosSlash:
				g = new Gen(1.8f, 8);
				g.Noise(0, 0.18f, t => MathF.Pow(t / 0.18f, 2f), t => 250f + 2600f * (t / 0.18f), 0.5f, F.Band, 0.5f);    // 低沉风啸
				g.Snap(0.18f, 1f);
				g.Noise(0.18f, 0.08f, Atk(0.0002f, 0.02f), K(1400), 0.6f, F.High, 0.9f);
				g.Noise(0.18f, 0.6f, Atk(0.001f, 0.16f), K(120), 1f, F.Low, 1.1f);                                        // 巨响
				g.Tone(0.18f, 0.9f, t => 75f * MathF.Exp(-t / 0.08f) + 30f, Atk(0.002f, 0.25f), 0.8f);
				g.Noise(0.18f, 0.5f, Atk(0.002f, 0.12f), t => 3200f - 1600f * MathF.Min(t / 0.4f, 1f), 0.2f, F.Band, 0.22f);   // 金属刮擦
				g.Tone(0.18f, 1.5f, _ => 82.4f, Atk(0.02f, 0.6f), 0.2f);                                                  // 不协和低音（三全音）
				g.Tone(0.18f, 1.5f, _ => 116.5f, Atk(0.03f, 0.55f), 0.16f);
				g.Pings(0.2f, 0.5f, 20, 2500f, 6500f, 0.07f);
				g.Noise(0.18f, 1.6f, Atk(0.01f, 0.5f), t => 2500f * MathF.Exp(-t / 0.4f) + 300f, 0.9f, F.Low, 0.25f);
				return g.B;
		}
		return new float[1];
	}
}
