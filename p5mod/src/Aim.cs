using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>
/// 骨骼动画的接续（用户 2026-10-28）：
///  · 拿起小刀牌就举枪瞄准（aim_in → aim_loop），打出去直接开火（shiv 换成 shiv_from_aim），放回去收枪（aim_out）；
///  · 连续出攻击牌时，上一刀还在收势就接下一刀（attack 换成 attack_chain，从手里握着刀直接再蓄力），不再从拔刀重播；
///    连续开枪同理（shiv 换成 shiv_from_aim）。
/// 只动 Joker 的骨骼（原版静默猎手被我们的骨骼替换了，这些动画名只在 art/p5/rig/joker.spine-json 里有）。
/// </summary>
public static class Aim
{
	private static readonly HashSet<string> Raised = new() { "aim_in", "aim_loop", "shiv", "shiv_from_aim" };
	private static readonly HashSet<string> Slashing = new() { "attack", "attack_chain" };

	/// <summary>这个动画状态是不是本地 Joker 的（换动画的入口所有生物都会经过）。</summary>
	private static NCreature? Owner(MegaAnimationState state)
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null || state.BoundObject is not GodotObject native)
		{
			return null;
		}
		ulong id = native.GetInstanceId();
		foreach (NCreature n in room.CreatureNodes)
		{
			if (n.Entity?.Player is { } p && Joker.Is(p) && n.Visuals?.SpineBody?.TryGetAnimationState()?.BoundObject is GodotObject o && o.GetInstanceId() == id)
			{
				return n;
			}
		}
		return null;
	}

	public static string? Rename(MegaAnimationState state, string requested)
	{
		if (Owner(state) is not NCreature node || node.Visuals.SpineBody is not MegaSprite body)
		{
			return null;
		}
		string? current = Current(state);
		if (requested == "shiv" && current != null && Raised.Contains(current) && body.HasAnimation("shiv_from_aim"))
		{
			return "shiv_from_aim";
		}
		if (requested == "attack" && current != null && Slashing.Contains(current) && body.HasAnimation("attack_chain"))
		{
			return "attack_chain";
		}
		return null;
	}

	public static void Raise(CardModel card)
	{
		if (!Joker.OwnsCard(card) || !Joker.IsShot(card) || !LocalContext.IsMe(card.Owner))
		{
			return;
		}
		NCreature? node = Joker.Node(card.Owner);
		MegaSprite? body = node?.Visuals?.SpineBody;
		if (body == null || !body.HasAnimation("aim_loop") || body.TryGetAnimationState() is not MegaAnimationState state)
		{
			return;
		}
		string? current = Current(state);
		if (current != null && Raised.Contains(current))
		{
			if (current is "shiv" or "shiv_from_aim")
			{
				state.SetAnimation("aim_loop", true, 0);          // 上一枪还没收完：直接回到瞄准
			}
			return;
		}
		if (current is not ("idle_loop" or "low_health_loop"))
		{
			return;                                               // 正在出别的牌 / 受击：不打断
		}
		state.SetAnimation("aim_in", false, 0);
		state.AddAnimation("aim_loop", 0f, true, 0);
	}

	public static void Lower(NCreature? node)
	{
		MegaSprite? body = node?.Visuals?.SpineBody;
		if (body == null || body.TryGetAnimationState() is not MegaAnimationState state || Current(state) is not ("aim_in" or "aim_loop"))
		{
			return;
		}
		bool low = node!.Entity.GetHpPercentRemaining() <= 0.25 && body.HasAnimation("low_health_loop");
		state.SetAnimation("aim_out", false, 0);
		state.AddAnimation(low ? "low_health_loop" : "idle_loop", 0f, true, 0);
	}

	public static void LowerLocal()
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null)
		{
			return;
		}
		foreach (NCreature n in room.CreatureNodes.Where(n => n.Entity?.Player is { } p && Joker.Is(p) && LocalContext.IsMe(p)))
		{
			Lower(n);
		}
	}

	private static string? Current(MegaAnimationState state)
	{
		try
		{
			return state.GetCurrentAnimationName(0);
		}
		catch (System.Exception)
		{
			return null;
		}
	}
}

/// <summary>换动画的唯一入口：Joker 正举着枪 / 正在收刀时，把 shiv / attack 换成接续版本。</summary>
[HarmonyPatch(typeof(MegaAnimationState), nameof(MegaAnimationState.SetAnimation))]
public static class AnimRenamePatch
{
	public static void Prefix(MegaAnimationState __instance, ref string animationName)
	{
		if (animationName is not ("shiv" or "attack"))
		{
			return;
		}
		string requested = animationName;
		string? replacement = null;
		ModEntry.Safe("AnimRename", () => replacement = Aim.Rename(__instance, requested));
		if (replacement != null)
		{
			animationName = replacement;
		}
	}
}

/// <summary>单体牌开始选目标（拿起小刀牌）：举枪。</summary>
[HarmonyPatch]
public static class StartTargetingPatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(NTargetManager).GetMethods(BindingFlags.Instance | BindingFlags.Public)
			.Where(m => m.Name == "StartTargeting" && m.GetParameters().Length > 1 && m.GetParameters()[1].ParameterType == typeof(Control));

	public static void Postfix(object[] __args)
	{
		ModEntry.Safe("StartTargeting", () =>
		{
			if (__args[1] is NCard { Model: CardModel card })
			{
				Aim.Raise(card);
			}
		});
	}
}

/// <summary>群体牌（刀扇下的小刀）拖进出牌区：举枪。</summary>
[HarmonyPatch(typeof(NCardPlay), "ShowMultiCreatureTargetingVisuals")]
public static class MultiTargetingPatch
{
	public static void Postfix(NCardPlay __instance)
	{
		ModEntry.Safe("MultiTargeting", () =>
		{
			if (Traverse.Create(__instance).Property("Card").GetValue() is CardModel card)
			{
				Aim.Raise(card);
			}
		});
	}
}

/// <summary>出牌流程收尾：取消（isFinished = false）就收枪；打出则交给 shiv_from_aim 接着开火。</summary>
[HarmonyPatch(typeof(NCardPlay), "Cleanup")]
public static class CardPlayCleanupPatch
{
	public static void Prefix(bool isFinished)
	{
		if (!isFinished)
		{
			ModEntry.Safe("CardPlayCleanup", Aim.LowerLocal);
		}
	}
}

[HarmonyPatch(typeof(NTargetManager), "FinishTargeting")]
public static class FinishTargetingPatch
{
	public static void Postfix(bool cancel)
	{
		if (cancel)
		{
			ModEntry.Safe("FinishTargeting", Aim.LowerLocal);
		}
	}
}
