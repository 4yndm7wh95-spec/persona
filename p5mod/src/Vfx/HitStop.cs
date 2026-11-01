using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 顿帧（照结城理 mod 的 EvokerShot.HitStop / TimeScale）：物理命中那一下把时间流速压到 0.08 停一小会儿，再恢复。
/// · 到期按「不受时间流速影响」的时钟判断（游戏里是真实毫秒，预览里是录像帧数），每帧在 SceneTree.ProcessFrame 里检查；
/// · 多段攻击只有第一下顿帧：一次顿帧之后 0.35s 内不再顿；
/// · 开始时记下原来的流速，结束写回它（不硬写 1）；只在主线程改 Engine.TimeScale。
/// </summary>
public static class HitStop
{
	/// <summary>不受时间流速影响的秒数。预览工具换成录像帧数 / 30。</summary>
	public static Func<double> RealSeconds = () => Time.GetTicksMsec() / 1000.0;

	private static double _until, _blockUntil, _prev = 1.0;
	private static bool _on, _hooked;

	public static void Request(double seconds, double scale = 0.08)
	{
		if (OS.GetThreadCallerId() != OS.GetMainThreadId())
		{
			Callable.From(() => Request(seconds, scale)).CallDeferred();
			return;
		}
		Install();
		double now = RealSeconds();
		if (now < _blockUntil)
		{
			return;
		}
		_blockUntil = now + seconds + 0.35;
		if (!_on)
		{
			_prev = Engine.TimeScale;
		}
		_on = true;
		_until = now + seconds;
		Engine.TimeScale = scale;
	}

	private static void Install()
	{
		if (_hooked || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		_hooked = true;
		tree.ProcessFrame += () => Stage.Safe("hitstop", Watch);
	}

	private static void Watch()
	{
		if (_on && RealSeconds() >= _until)
		{
			_on = false;
			Engine.TimeScale = _prev < 0.05 ? 1.0 : _prev;
		}
	}
}
