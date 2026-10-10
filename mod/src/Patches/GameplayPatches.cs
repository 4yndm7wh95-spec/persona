using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using P3Makoto.Core;
using P3Makoto.Vfx;

namespace P3Makoto.Patches;

/// <summary>
/// 与游戏事件同步的特效钩子：每一击伤害、获得格挡、获得辉星、锻造君王之刃。
/// 只对结城理（储君）打出的牌生效。
/// </summary>
internal static class Sig
{
	public static bool Params(MethodInfo m, params string[] names) =>
		m.GetParameters().Select(p => p.Name).SequenceEqual(names);
}

/// <summary>结城理的攻击牌使用自定义命中特效时，关掉原版命中特效，避免两套叠在一起。</summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
public static class AttackHitVfxPatch
{
	private static readonly PropertyInfo HitVfx = AccessTools.Property(typeof(AttackCommand), "HitVfx");

	public static void Prefix(AttackCommand __instance)
	{
		ModEntry.Safe("AttackHitVfx", () =>
		{
			if (__instance.ModelSource is CardModel card && CardFx.ReplacesHitVfx(card))
			{
				HitVfx.SetValue(__instance, null);
			}
		});
	}
}

/// <summary>每一击伤害（所有 Damage 重载最终都汇到这个签名）。</summary>
[HarmonyPatch]
public static class DamagePatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "Damage" && Sig.Params(m, "choiceContext", "targets", "amount", "props", "dealer", "cardSource", "cardPlay"));

	public static void Prefix(IEnumerable<Creature>? targets, CardModel? cardSource)
	{
		ModEntry.Safe("Damage", () =>
		{
			if (targets != null && CardFx.IsMakotoCard(cardSource))
			{
				CardFx.OnHit(cardSource!, targets);
			}
		});
	}
}

/// <summary>获得格挡（所有 GainBlock 重载最终都汇到这个签名）。</summary>
[HarmonyPatch]
public static class GainBlockPatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "GainBlock" && Sig.Params(m, "creature", "amount", "props", "cardPlay", "fast"));

	public static void Prefix(Creature creature, decimal amount, CardPlay? cardPlay)
	{
		ModEntry.Safe("GainBlock", () =>
		{
			if (amount <= 0m || cardPlay == null || !CardFx.IsMakotoCard(cardPlay.Card))
			{
				return;
			}
			NCreature? node = NCombatRoom.Instance?.GetCreatureNode(creature);
			if (node != null)
			{
				CardFx.OnBlock(cardPlay.Card, node);
			}
		});
	}
}

[HarmonyPatch(typeof(PlayerCmd), nameof(PlayerCmd.GainStars))]
public static class GainStarsPatch
{
	public static void Prefix(decimal amount, Player player)
	{
		ModEntry.Safe("GainStars", () =>
		{
			if (amount > 0m && Makoto.Is(player) && Makoto.Node(player) is NCreature node)
			{
				CardFx.OnStars(node, (int)amount);
			}
		});
	}
}

[HarmonyPatch(typeof(ForgeCmd), nameof(ForgeCmd.Forge))]
public static class ForgePatch
{
	public static void Prefix(decimal amount, Player player)
	{
		ModEntry.Safe("Forge", () =>
		{
			if (amount > 0m && Makoto.Is(player) && Makoto.Node(player) is NCreature node)
			{
				CardFx.OnForge(node);
			}
		});
	}
}
