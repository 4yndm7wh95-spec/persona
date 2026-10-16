using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;

namespace P3Makoto.Patches;

/// <summary>
/// v0.14.45（用户：0.107.1 上用打击 / 仆从打击击杀怪物后整局卡住，日志无报错）：
/// 0.107.1 的 MegaAnimationState.GetCurrent 在轨道上"有条目但条目为空对象"时，仍返回一个包着 null 的 MegaTrackEntry，
/// 之后读动画名 / 时长就抛空引用。怪物死亡时游戏用 GetCurrentAnimationLength（→ GetCurrent）量死亡动画时长，
/// 在这里抛出的异常会让击杀这一步的异步流程静默失败，后面排队的出牌永远等不到（截图里卡在屏幕中间的那张牌）。
/// 0.111 的游戏代码已经补了这个判断（GetCurrent 里多了 AsGodotObject() == null 的检查）；这里给 0.107.1 补上同样的检查：
/// 包着 null 的条目一律当作"没有条目"返回 null。0.111 上 GetCurrent 不会返回这种条目，补丁什么也不做。
/// </summary>
[HarmonyPatch(typeof(MegaAnimationState), nameof(MegaAnimationState.GetCurrent))]
public static class SpineEmptyTrackPatch
{
	public static void Postfix(ref MegaTrackEntry? __result)
	{
		if (__result != null && (__result.BoundObject == null || !Godot.GodotObject.IsInstanceValid(__result.BoundObject)))
		{
			__result = null;
		}
	}
}

/// <summary>同上的第二道保险（GetCurrent 太小、可能被 JIT 内联进调用方而绕过上面的补丁）：读当前动画时长遇到空引用时当作"没有动画"。</summary>
[HarmonyPatch(typeof(MegaAnimationState), nameof(MegaAnimationState.GetCurrentAnimationDuration))]
public static class SpineEmptyDurationPatch
{
	public static System.Exception? Finalizer(System.Exception? __exception, ref float? __result)
	{
		if (__exception is System.NullReferenceException)
		{
			__result = null;
			return null;
		}
		return __exception;
	}
}

[HarmonyPatch(typeof(MegaAnimationState), nameof(MegaAnimationState.GetCurrentAnimationName))]
public static class SpineEmptyNamePatch
{
	public static System.Exception? Finalizer(System.Exception? __exception, ref string? __result)
	{
		if (__exception is System.NullReferenceException)
		{
			__result = null;
			return null;
		}
		return __exception;
	}
}
