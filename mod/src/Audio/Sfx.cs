using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace P3Makoto.Audio;

public enum Snd
{
	Gunshot,
}

/// <summary>
/// 程序合成音效（不需要音频素材）：启动时在后台线程合成 PCM，播放走 Godot 的 "SFX" 总线 —— 和游戏自己的
/// 临时音效（NDebugAudioManager）同一条总线，跟随游戏音量设置。
/// 目前只有召唤器枪声一个（其余音效都用游戏原版）。合成手法：N 波尖峰 + 滤波噪声（状态变量滤波器）+ 指数包络。
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
	private static float Drive(Snd s) => s == Snd.Gunshot ? 2.6f : 1.6f;

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
		}
		return new float[1];
	}
}
