using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 顿帧（照结城理 mod 的 EvokerShot.HitStop / TimeScale，0.9.58 原样）：物理命中那一下把时间流速压到 0.08 停一小会儿，再恢复。
/// · 到期按「不受时间流速影响」的时钟判断（游戏里是真实毫秒，预览里是录像帧数），每帧在 SceneTree.ProcessFrame 里检查；
/// · 多段攻击只有第一下顿帧：一次顿帧之后 0.35s 内不再顿；
/// · 开始时记下原来的流速，结束写回它（不硬写 1）；只在主线程改 Engine.TimeScale。
/// 只改本地的画面流速（和游戏自己的快速模式一样只影响表现快慢），不改任何结算。
/// VisualSeconds = 跟着流速走的画面时钟（排在 Fx.After 上的特效按它对时）。
/// </summary>
public static class HitStop
{
	/// <summary>不受时间流速影响的秒数。预览工具换成录像帧数 / 30。</summary>
	public static Func<double> RealSeconds = () => Time.GetTicksMsec() / 1000.0;

	private static double _began, _until, _blockUntil, _lostTime, _scale = 1.0, _prev = 1.0;
	private static bool _on, _hooked;

	/// <summary>画面时钟：真实时间减去顿帧里少走的那部分。</summary>
	internal static double VisualSeconds()
	{
		double now = RealSeconds();
		double pending = _on ? Math.Clamp(now - _began, 0, _until - _began) * (1 - _scale) : 0;
		return now - _lostTime - pending;
	}

	/// <summary>兼容入口：流速由 Engine.TimeScale 统一压，补间不用单独登记。</summary>
	internal static Tween Track(Tween tween) => tween;

	public static void Request(double seconds, double scale = 0.08)
	{
		if (OS.GetThreadCallerId() != OS.GetMainThreadId())
		{
			Callable.From(() => Request(seconds, scale)).CallDeferred();
			return;
		}
		if (AllOut.Playing) return;

		Install();
		double now = RealSeconds();
		if (now < _blockUntil)
		{
			return;
		}
		Finish();
		_blockUntil = now + seconds + 0.35;
		_prev = Engine.TimeScale;
		_on = true;
		_began = now;
		_until = now + seconds;
		_scale = scale;
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
		if (_on && RealSeconds() >= _until) Finish();
	}

	// End a pending impact pause before the finale takes ownership of TimeScale.
	internal static void Finish()
	{
		if (!_on) return;

		_on = false;
		double now = Math.Min(RealSeconds(), _until);
		_lostTime += Math.Max(0, now - _began) * (1 - _scale);
		Engine.TimeScale = _prev < 0.05 ? 1.0 : _prev;
	}
}
