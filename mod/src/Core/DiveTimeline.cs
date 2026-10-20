using System;

namespace P3Makoto.Core;

/// <summary>可按真实时间直接取样的入水时间轴；运行时与离线预览共用，不补播过期水花。</summary>
internal static class DiveTimeline
{
	// 原作录像 #22..41：每张形态保持 4 个 60 Hz 采样帧，再直接切换。
	internal const double SplashFps = 15.0;
	internal const int SplashFrames = 10;
	private const double Full = 6.0 / SplashFps, RevealTail = 1.7 / SplashFps;
	private const double WarpFps = 30.0, EnterFrames = 75.0, LoopFrames = 210.0;

	internal readonly record struct Frame(int Splash, float Alpha, int MaskNext, float MaskMix, float Reveal, float Warp);

	internal static Frame At(double seconds)
	{
		seconds = Math.Max(0, seconds);
		double shape = seconds * SplashFps;
		int splash = (int)Math.Min(SplashFrames, Math.Floor(shape));
		// 淡出只衰减当前一张图；同一时刻永远不显示下一张水花。
		float alpha = (float)Math.Clamp((SplashFrames - shape) / 4.0, 0, 1);
		int next = Math.Min(splash + 1, SplashFrames - 1);
		float maskMix = splash < SplashFrames ? (float)(shape - splash) : 1f;
		double reveal = Math.Clamp((seconds - (Full - RevealTail)) / RevealTail, 0, 1);
		reveal = reveal * reveal * (3 - 2 * reveal);
		double warp = seconds * WarpFps;
		if (warp >= EnterFrames) warp = EnterFrames + (warp - EnterFrames) % (LoopFrames - 1);
		return new Frame(splash, alpha, next, maskMix, (float)reveal, (float)warp);
	}
}
