using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
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
			bool gun = Joker.IsShot(cardSource) || CardFx.For(cardSource)?.Shot != null;
			foreach (NCreature n in nodes)
			{
				Damage.Mark(n, gun);
			}
			CardFx.OnHit(cardSource, joker, nodes);
			// 打中易伤的敌人 = P5 的打中弱点：WEAK + 眼神特写
			foreach (Creature c in targets)
			{
				if (!c.IsPlayer && c.HasPower<MegaCrit.Sts2.Core.Models.Powers.VulnerablePower>() && room.GetCreatureNode(c) is NCreature n)
				{
					Eyes.OnWeakHit(n);
				}
			}
		});
	}
}

/// <summary>伤害数字：Joker 打中的敌人，原版数字换成 P5R 样式（Damage.Show）。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Vfx.NDamageNumVfx), nameof(MegaCrit.Sts2.Core.Nodes.Vfx.NDamageNumVfx.Create),
	typeof(Creature), typeof(MegaCrit.Sts2.Core.Entities.Creatures.DamageResult))]
public static class DamageNumberPatch
{
	public static void Postfix(Creature target, MegaCrit.Sts2.Core.Entities.Creatures.DamageResult result,
		ref MegaCrit.Sts2.Core.Nodes.Vfx.NDamageNumVfx? __result)
	{
		MegaCrit.Sts2.Core.Nodes.Vfx.NDamageNumVfx? original = __result;
		bool replaced = false;
		ModEntry.Safe("DamageNumber", () =>
		{
			if (original == null || target.IsPlayer || NCombatRoom.Instance?.GetCreatureNode(target) is not NCreature node
				|| Joker.FxLayer() is not Godot.Control layer || !Damage.TakeMark(node, out bool gun))
			{
				return;
			}
			int amount = result.UnblockedDamage + result.OverkillDamage;
			if (amount <= 0)
			{
				return;                                        // 全被格挡：保留原版的显示
			}
			Damage.Show(layer, node.VfxSpawnPosition, amount, gun);
			original.QueueFree();
			replaced = true;
		});
		if (replaced)
		{
			__result = null;                                   // 调用方拿到 null 就不显示原版数字
		}
	}
}

/// <summary>出牌那一刻（结算之前）：记下这张牌要的施法动作，放出牌演出（召唤亚森、预告信、装填……）。</summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed))]
public static class CardPlayedPatch
{
	public static void Prefix(CardPlay cardPlay)
	{
		ModEntry.Safe("CardPlayed", () =>
		{
			if (!Joker.OwnsCard(cardPlay.Card) || Joker.Node(cardPlay.Card.Owner) is not NCreature node)
			{
				return;
			}
			Aim.PendingCast[node.GetInstanceId()] = CardFx.CastFor(cardPlay.Card);
			CardFx.OnPlay(cardPlay.Card, cardPlay.Target);
		});
	}
}

/// <summary>获得格挡（所有 GainBlock 重载都汇到带 cardPlay 的那个）。</summary>
[HarmonyPatch]
public static class GainBlockPatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "GainBlock" && m.GetParameters().Any(p => p.Name == "cardPlay") && m.GetParameters().Any(p => p.Name == "creature"))
			.OrderByDescending(m => m.GetParameters().Length).Take(1);

	public static void Prefix(Creature creature, decimal amount, CardPlay? cardPlay)
	{
		ModEntry.Safe("GainBlock", () =>
		{
			if (amount > 0m && cardPlay != null && Joker.OwnsCard(cardPlay.Card) && NCombatRoom.Instance?.GetCreatureNode(creature) is NCreature node
				&& creature.Player is { } p && Joker.Is(p))
			{
				CardFx.OnBlock(cardPlay.Card, node);
			}
		});
	}
}
