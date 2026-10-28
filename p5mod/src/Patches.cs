using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>Joker 的攻击牌：关掉原版命中特效（静默猎手的刀光、飞刀），换成我们自己的；原版命中音效保留。</summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
public static class AttackHitVfxPatch
{
	private static readonly PropertyInfo HitVfx = AccessTools.Property(typeof(AttackCommand), "HitVfx");
	private static readonly FieldInfo CustomHitNodes = AccessTools.Field(typeof(AttackCommand), "_customHitVfxNodes");

	public static void Prefix(AttackCommand __instance)
	{
		ModEntry.Safe("AttackHitVfx", () =>
		{
			if (__instance.ModelSource is CardModel card && Joker.OwnsCard(card))
			{
				HitVfx.SetValue(__instance, null);
				(CustomHitNodes.GetValue(__instance) as IList)?.Clear();     // 小刀牌原版的飞刀（NShivThrowVfx）
			}
		});
	}
}

/// <summary>每一击伤害（所有 Damage 重载最终汇到参数最多的那个）：在这一刻放命中特效。</summary>
[HarmonyPatch]
public static class DamagePatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "Damage" && m.GetParameters().Any(p => p.Name == "cardSource") && m.GetParameters().Any(p => p.Name == "targets"))
			.OrderByDescending(m => m.GetParameters().Length).Take(1);

	public static void Prefix(IEnumerable<Creature>? targets, CardModel? cardSource)
	{
		ModEntry.Safe("Damage", () =>
		{
			if (targets == null || cardSource == null || !Joker.OwnsCard(cardSource))
			{
				return;
			}
			NCombatRoom? room = NCombatRoom.Instance;
			NCreature? joker = Joker.Node(cardSource.Owner);
			if (room == null || joker == null)
			{
				return;
			}
			List<NCreature> nodes = targets.Select(room.GetCreatureNode).OfType<NCreature>().Where(n => n != joker).ToList();
			if (nodes.Count == 0)
			{
				return;
			}
			if (Joker.IsShot(cardSource))
			{
				Gunshot.Play(joker, nodes);
			}
			else if (cardSource.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack)
			{
				Slash.Play(joker, nodes);
			}
		});
	}
}
