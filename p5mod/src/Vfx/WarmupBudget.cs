using System;

namespace P5Joker;

/// <summary>决定何时可开始下一项。单次驱动编译不能被脚本时间预算抢占。</summary>
internal sealed class WarmupBudget
{
	internal const double CpuBudgetMs = 2.0;
	private double _quietSince, _nextAt;
	private int _stableFrames;
	private bool _allowed;

	internal void Observe(double now, double frameMs, double frameLimit, bool allowed, bool input)
	{
		_allowed = allowed && !input;
		if (!_allowed || frameMs <= 0 || frameMs > frameLimit)
		{
			_quietSince = now;
			_stableFrames = 0;
			return;
		}
		_stableFrames++;
	}

	internal bool CanStart(double now, double cpuUsedMs) => _allowed && _stableFrames >= 30
		&& now - _quietSince >= 1200 && now >= _nextAt && cpuUsedMs < CpuBudgetMs;

	internal void Submitted(double now, double cpuMs) =>
		_nextAt = now + Math.Clamp(120 + cpuMs * 24, 120, 1500);

	internal void SlowDraw(double now, double frameMs, double frameLimit = 24)
	{
		if (frameMs <= frameLimit) return;
		_quietSince = now;
		_stableFrames = 0;
		_nextAt = Math.Max(_nextAt, now + Math.Clamp(frameMs * 6, 800, 3000));
	}
}
