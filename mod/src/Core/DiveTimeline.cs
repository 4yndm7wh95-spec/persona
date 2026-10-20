using System;

namespace P3Makoto.Core;

/// <summary>可按真实时间直接取样的入水时间轴；运行时与离线预览共用，不补播过期水花。</summary>
internal static class DiveTimeline
{
	// 原版 roundf(elapsed * 30)：前段 /2，n>=15 后 (n-14)/3+7；TopInMenuFrame=24。
	internal const int SplashFrames = 10;
	private const double WarpFps = 30.0, EnterFrames = 75.0, LoopFrames = 210.0;

	internal readonly record struct Frame(int Splash, float Alpha, int Mask, float Reveal, float Warp);

	internal static Frame At(double seconds)
	{
		seconds = Math.Max(0, seconds);
		// 保留原生 float 精度和半数取整；长时间直接结束，避免整数转换溢出。
		int tick = seconds >= 1 ? 30 : (int)MathF.Round((float)seconds * 30f, MidpointRounding.AwayFromZero);
		int splash = tick is > 0 and < 24 ? Math.Min(9, tick < 15 ? tick / 2 : 7 + (tick - 14) / 3) : SplashFrames;
		int mask = tick == 0 ? -1 : Math.Min(tick / 2, 4);
		float reveal = tick >= 10 ? 1f : 0f;
		double warp = seconds * WarpFps;
		if (warp >= EnterFrames) warp = EnterFrames + (warp - EnterFrames) % (LoopFrames - 1);
		return new Frame(splash, splash < SplashFrames ? 1f : 0f, mask, reveal, (float)warp);
	}
}
