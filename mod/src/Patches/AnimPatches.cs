using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using P3Makoto.Core;

namespace P3Makoto.Patches;

/// <summary>
/// 游戏切换 Spine 动画的唯一入口。角色处于预备姿势时，把 cast / attack 换成"从预备姿势接招"的版本，
/// 避免先垂手再重新抬起。只有结城理骨骼里存在这些动画，其他角色不受影响。
/// </summary>
[HarmonyPatch(typeof(MegaAnimationState), nameof(MegaAnimationState.SetAnimation))]
public static class AnimRenamePatch
{
	public static void Prefix(MegaAnimationState __instance, ref string animationName)
	{
		string requested = animationName;
		string? replacement = null;
		ModEntry.Safe("AnimRename", () => replacement = Pose.Light(__instance, requested) ?? Pose.FromHolding(__instance.GetCurrentAnimationName(), requested));
		if (replacement != null)
		{
			animationName = replacement;
		}
	}
}
