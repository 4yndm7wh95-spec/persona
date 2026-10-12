using System;
using System.Diagnostics;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// 卡顿定位用的耗时日志（写进游戏日志 godot.log，前缀 "[P3Makoto] perf"）：
/// · Time(label, action)：记录一段代码花了多少毫秒；
/// · WatchFrames(label, seconds)：之后若干秒里，凡是超过 25ms 的帧都记一笔（标出距开始多少毫秒），用来找"选中时卡一下"的那一帧。
/// 只记日志，不影响游戏。
/// </summary>
public static class Perf
{
	private static ulong _watchStart, _watchEnd, _last;
	private static string _watchLabel = "";
	private static bool _hooked;

	public static void Time(string label, Action action)
	{
		Stopwatch sw = Stopwatch.StartNew();
		action();
		GD.Print($"{ModEntry.Tag} perf {label} {sw.Elapsed.TotalMilliseconds:F1}ms");
	}

	public static void WatchFrames(string label, double seconds)
	{
		ulong now = Godot.Time.GetTicksUsec();
		_watchStart = now;
		_watchEnd = now + (ulong)(seconds * 1e6);
		_last = now;
		_watchLabel = label;
		if (!_hooked && Engine.GetMainLoop() is SceneTree tree)
		{
			_hooked = true;
			tree.ProcessFrame += OnFrame;
		}
	}

	private static void OnFrame()
	{
		ulong now = Godot.Time.GetTicksUsec();
		if (now > _watchEnd)
		{
			_last = now;
			return;
		}
		double ms = (now - _last) / 1000.0;
		if (ms > 25.0)
		{
			GD.Print($"{ModEntry.Tag} perf frame {_watchLabel} +{(now - _watchStart) / 1000.0:F0}ms took {ms:F1}ms");
		}
		_last = now;
	}
}
