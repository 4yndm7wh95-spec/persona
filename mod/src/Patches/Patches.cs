using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Vfx;

namespace P3Makoto.Patches;

/// <summary>每次出牌（包括多次打出的每一次）执行 OnPlay 之前触发。</summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed))]
public static class CardPlayedPatch
{
	public static void Prefix(CardPlay cardPlay)
	{
		ModEntry.Safe("CardPlayed", () =>
		{
			TargetingFx.End();
			CardFx.OnPlay(cardPlay);
		});
	}
}

/// <summary>单体目标牌：开始选目标（以卡牌节点为起点的那个重载）。</summary>
[HarmonyPatch]
public static class StartTargetingPatch
{
	public static IEnumerable<MethodBase> TargetMethods()
	{
		return typeof(NTargetManager).GetMethods(BindingFlags.Instance | BindingFlags.Public)
			.Where(m => m.Name == "StartTargeting" && m.GetParameters().Length > 1 && m.GetParameters()[1].ParameterType == typeof(Control));
	}

	public static void Postfix(object[] __args)
	{
		ModEntry.Safe("StartTargeting", () =>
		{
			if (__args[1] is NCard { Model: CardModel card } && LocalPlayer.Owns(card))
			{
				TargetingFx.Begin(card);
			}
		});
	}
}

/// <summary>群体 / 无目标牌：拖进出牌区时触发。</summary>
[HarmonyPatch(typeof(NCardPlay), "ShowMultiCreatureTargetingVisuals")]
public static class MultiTargetingPatch
{
	public static void Postfix(NCardPlay __instance)
	{
		ModEntry.Safe("ShowMultiTargeting", () =>
		{
			if (Traverse.Create(__instance).Property("Card").GetValue() is CardModel card && LocalPlayer.Owns(card))
			{
				TargetingFx.Begin(card);
			}
		});
	}
}

/// <summary>群体牌拖回中间区域、或出牌流程清理时，收起预备表现。</summary>
[HarmonyPatch(typeof(NCardPlay), "HideTargetingVisuals")]
public static class HideTargetingPatch
{
	public static void Postfix()
	{
		ModEntry.Safe("HideTargeting", TargetingFx.End);
	}
}

[HarmonyPatch(typeof(NTargetManager), "FinishTargeting")]
public static class FinishTargetingPatch
{
	public static void Postfix(bool cancel)
	{
		ModEntry.Safe("FinishTargeting", () =>
		{
			if (cancel)
			{
				TargetingFx.End();
			}
			else
			{
				TargetingFx.Unhover();
			}
		});
	}
}

[HarmonyPatch(typeof(NTargetManager), "OnCreatureHovered")]
public static class CreatureHoveredPatch
{
	public static void Postfix(NTargetManager __instance, NCreature creature)
	{
		ModEntry.Safe("CreatureHovered", () =>
		{
			// 只有原版也认可这次悬停（HoveredNode 已设置）时才显示锁定框
			if (Traverse.Create(__instance).Property("HoveredNode").GetValue() == creature)
			{
				TargetingFx.Hover(creature);
			}
		});
	}
}

[HarmonyPatch(typeof(NTargetManager), "OnCreatureUnhovered")]
public static class CreatureUnhoveredPatch
{
	public static void Postfix()
	{
		ModEntry.Safe("CreatureUnhovered", TargetingFx.Unhover);
	}
}

internal static class LocalPlayer
{
	public static bool Owns(CardModel card) => MegaCrit.Sts2.Core.Context.LocalContext.IsMe(card.Owner);
}
