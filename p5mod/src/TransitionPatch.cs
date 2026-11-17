using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes;

namespace P5Joker;

/// <summary>
/// 选人界面点「开始」后的过场（NCharacterSelectScreen → NTransition.FadeOut(0.8, silent_transition_mat.tres)）：
/// 换成 Joker 的「刀痕切屏」过场（Vfx/JokerTransition.cs，做法和原版一样是时间图；音效照旧用原版的 wipe）。做法：FadeOut 开头把资源缓存里这条路径换成我们的材质、时长改成 JokerTransition.Time；
/// 原版 FadeOut 里另有一层黑色 SimpleTransition 跟着变黑（画在材质上面，会把剪影的红光盖掉）→ 这段时间先藏起来，演完再放回。
/// 不改原版文件；只认静默猎手的过场路径（Joker 就是换皮的静默猎手）。
/// </summary>
// 2026-11-17 已被出发过场取代（EmbarkPatches + Vfx/EmbarkTransition.cs：门合拢 + 飞刀钉预告信，用户定稿）。
// 两个都拦同一个 NTransition.FadeOut，不要和 EmbarkPatches 同时启用。
// 等用户看「刀痕切屏」预览，先不启用：[HarmonyPatch(typeof(NTransition), nameof(NTransition.FadeOut))]
public static class TransitionPatch
{
	private const string SilentPath = "silent_transition_mat.tres";

	public static void Prefix(NTransition __instance, ref float time, string transitionPath)
	{
		if (transitionPath == null || !transitionPath.EndsWith(SilentPath)) return;
		bool ok = false;
		ModEntry.Safe("JokerTransition", () =>
		{
			if (JokerTransition.Make() is not { } mat) return;
			PreloadManager.Cache.SetAsset(transitionPath, mat);
			ok = true;
		});
		if (!ok) return;
		time = JokerTransition.Time;
		float hold = time;
		ModEntry.Safe("JokerTransition.Simple", () =>
		{
			if (__instance.GetNodeOrNull<Control>("SimpleTransition") is not { } simple) return;
			simple.Visible = false;
			__instance.GetTree().CreateTimer(hold + 0.4).Connect("timeout", Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(simple)) simple.Visible = true;
			}));
		});
	}
}
