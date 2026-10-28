using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 特效和外界的接口：在哪一层画、怎么震屏、贴图从哪来、出错怎么记。
/// 游戏里由 Patches 按战斗房间填（StageGame），预览工具（tools/p5/fxpreview）自己填。
/// </summary>
public static class Stage
{
	/// <summary>人物前面 / 后面的特效层（全局坐标）。</summary>
	public static Control? Front, Back;

	/// <summary>震屏：1 很弱、2 弱、3 中、4 强。</summary>
	public static Action<int>? Shake;

	/// <summary>现在是第几毫秒（判断连击 / 连续格挡用）。预览录像时比真实时间慢，所以预览换成自己的录像时钟。</summary>
	public static Func<ulong> NowMs = () => Time.GetTicksMsec();

	public static Func<string, Texture2D?> LoadTexture = name =>
	{
		string path = $"res://p5joker/fx/{name}.png";
		return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
	};

	public static Action<string, Exception> Report = (where, e) => GD.PrintErr($"[P5Joker] {where}: {e}");

	public static void Safe(string where, Action action)
	{
		try
		{
			action();
		}
		catch (Exception e)
		{
			Report(where, e);
		}
	}
}
