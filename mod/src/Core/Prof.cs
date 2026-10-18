using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace P3Makoto.Core;

/// <summary>
/// v0.14.64 本 mod 自己的耗时统计（只记数，不改行为）：ModEntry.Safe 包住的所有补丁 / 每帧逻辑、Fx.Animate 的每帧回调都计时。
/// Health 每 60 秒和低帧率时把"本 mod 每帧平均花了多少毫秒、花在哪"写进日志 —— 用来区分卡顿是不是我们造成的。
/// 嵌套调用只把最外层计入总时间（各项是含嵌套的）。只在主线程统计。
/// </summary>
public static class Prof
{
	private sealed class Acc
	{
		internal readonly Dictionary<string, (long Ticks, int Calls, long Max)> Items = new();
		internal long Top;
		internal int Frames;

		internal void Clear()
		{
			Items.Clear();
			Top = 0;
			Frames = 0;
		}
	}

	private static readonly Acc Minute = new(), Window = new(), LastWindow = new();
	private static int _depth;
	private static readonly int MainThread = System.Environment.CurrentManagedThreadId;

	public static long Begin()
	{
		if (System.Environment.CurrentManagedThreadId != MainThread)
		{
			return 0;
		}
		if (_depth == 0)
		{
			_polys0 = Polys;
			_labels0 = Labels;
			_anims0 = Anims;
		}
		_depth++;
		return Stopwatch.GetTimestamp();
	}

	/// <summary>Fx.Poly / Typo.Text / Fx.Animate 各自 +1，用来看一次尖峰里新建了多少东西。</summary>
	public static int Polys, Labels, Anims;
	private static int _polys0, _labels0, _anims0, _spikes;
	private static ulong _spikeMinute;

	public static void End(string key, long t0)
	{
		if (t0 == 0)
		{
			return;
		}
		long dt = Stopwatch.GetTimestamp() - t0;
		_depth = System.Math.Max(0, _depth - 1);       // Begin/End 都在 finally 里成对调用；保险不让深度变负
		Add(Minute, key, dt);
		Add(Window, key, dt);
		// 单次（最外层）超过 6ms：立刻记一行，带上这次新建的多边形 / 文字 / 动画数量（每分钟最多 40 行）
		double ms = dt * 1000.0 / Stopwatch.Frequency;
		if (_depth == 0 && ms > 6.0)
		{
			ulong minute = Godot.Time.GetTicksMsec() / 60000;
			if (minute != _spikeMinute)
			{
				_spikeMinute = minute;
				_spikes = 0;
			}
			if (++_spikes <= 40)
			{
				Godot.GD.Print($"{ModEntry.Tag} health spike {key} {ms:0.0}ms polys+{Polys - _polys0} labels+{Labels - _labels0} anims+{Anims - _anims0}");
			}
		}
	}

	private static void Add(Acc a, string key, long dt)
	{
		a.Items[key] = a.Items.TryGetValue(key, out var v) ? (v.Ticks + dt, v.Calls + 1, System.Math.Max(v.Max, dt)) : (dt, 1, dt);
		if (_depth == 0)
		{
			a.Top += dt;
		}
	}

	/// <summary>每帧调一次（Health）；每 2 秒把窗口存成 LastWindow。</summary>
	internal static void Frame(bool rollWindow)
	{
		Minute.Frames++;
		Window.Frames++;
		if (rollWindow)
		{
			LastWindow.Clear();
			foreach (var kv in Window.Items) LastWindow.Items[kv.Key] = kv.Value;
			LastWindow.Top = Window.Top;
			LastWindow.Frames = Window.Frames;
			Window.Clear();
		}
	}

	internal static string MinuteReport(bool reset)
	{
		string s = Report(Minute);
		if (reset)
		{
			Minute.Clear();
		}
		return s;
	}

	internal static string WindowReport() => Report(LastWindow);

	private static string Report(Acc a)
	{
		double ms(long t) => t * 1000.0 / Stopwatch.Frequency;
		int f = System.Math.Max(1, a.Frames);
		string top = string.Join(", ", a.Items.OrderByDescending(kv => kv.Value.Ticks).Take(8)
			.Select(kv => $"{kv.Key} {ms(kv.Value.Ticks) / f:0.00}ms×{kv.Value.Calls} (max {ms(kv.Value.Max):0.0})"));
		return $"p3_ms_per_frame={ms(a.Top) / f:0.00} over {a.Frames} frames; top: {top}";
	}
}
