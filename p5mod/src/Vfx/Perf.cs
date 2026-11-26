using Godot;

namespace P5Joker;

/// <summary>
/// 手机端降档（用户 2026-11-26：部分玩家玩 Joker 掉帧卡顿，尤其是手机）。只在手机（Android / iOS，或用 mobile 渲染器）上生效，PC 画面不变。
/// 手机屏幕像素多（2400×1080 一类）、GPU 带宽小、内存紧：全屏的离屏渲染降分辨率、关掉多重采样。
/// </summary>
public static class Perf
{
	private static bool? _mobile;

	public static bool Mobile => _mobile ??= Detect();

	private static bool Detect()
	{
		try
		{
			string os = OS.GetName();
			return os is "Android" or "iOS" || OS.HasFeature("mobile") || RenderingServer.GetCurrentRenderingMethod() == "mobile";
		}
		catch (System.Exception)
		{
			return false;
		}
	}

	/// <summary>全屏离屏渲染（3D 菜单 Joker 等）的分辨率倍数：手机 0.6，PC 1。</summary>
	public static float OffscreenScale => Mobile ? 0.6f : 1f;
}
