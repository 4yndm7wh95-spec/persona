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
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace P5Joker;

/// <summary>Joker 的攻击牌：关掉原版命中特效（刀光、飞刀），换成 HitFx（JokerFx.OnHit）；原版命中音效保留。</summary>
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

/// <summary>每一击伤害（所有 Damage 重载最终汇到参数最多的那个）：放命中特效，记下 Joker 打中了谁、是不是枪击（伤害数字据此换成 P5 样式）。</summary>
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
			bool gun = Joker.IsShot(cardSource);
			Hits.Mark(targets, gun);
			if (Joker.Node(cardSource.Owner) is NCreature joker && NCombatRoom.Instance is NCombatRoom room)
			{
				foreach (Creature c in targets)
				{
					if (!c.IsPlayer && room.GetCreatureNode(c) is NCreature n)
					{
						JokerFx.OnHit(joker, n, gun);
					}
				}
			}
		});
	}
}

/// <summary>Joker 最近 1.5 秒内打中的目标（多段 / 延迟结算都在窗口内）。</summary>
public static class Hits
{
	private static readonly Dictionary<Creature, (ulong Time, bool Gun)> Marked = new();

	public static void Mark(IEnumerable<Creature> targets, bool gun)
	{
		ulong now = Godot.Time.GetTicksMsec();
		if (Marked.Count > 32)
		{
			foreach (Creature old in Marked.Where(kv => now - kv.Value.Time > 1500).Select(kv => kv.Key).ToList())
			{
				Marked.Remove(old);
			}
		}
		foreach (Creature c in targets)
		{
			if (!c.IsPlayer)
			{
				Marked[c] = (now, gun);
			}
		}
	}

	public static bool Recent(Creature target, out bool gun)
	{
		gun = false;
		if (!Marked.TryGetValue(target, out var m) || Godot.Time.GetTicksMsec() - m.Time > 1500)
		{
			return false;
		}
		gun = m.Gun;
		return true;
	}
}

/// <summary>伤害数字：Joker 打中的敌人，原版数字换成 P5R 样式（Popups.Damage）。全被格挡（0 点）时保留原版。</summary>
[HarmonyPatch(typeof(NDamageNumVfx), nameof(NDamageNumVfx.Create), typeof(Creature), typeof(DamageResult))]
public static class DamageNumberPatch
{
	public static void Postfix(Creature target, DamageResult result, ref NDamageNumVfx? __result)
	{
		NDamageNumVfx? original = __result;
		bool replaced = false;
		ModEntry.Safe("DamageNumber", () =>
		{
			if (original == null || target.IsPlayer || !Hits.Recent(target, out bool gun)
				|| NCombatRoom.Instance?.GetCreatureNode(target) is not NCreature node || Joker.FxLayer() is not Godot.Control layer)
			{
				return;
			}
			int amount = result.UnblockedDamage + (target.Monster is MegaCrit.Sts2.Core.Models.Monsters.Osty ? 0 : result.OverkillDamage);
			if (amount <= 0)
			{
				return;
			}
			Popups.Damage(layer, node.GetInstanceId(), node.VfxSpawnPosition, amount, gun);
			original.QueueFree();
			replaced = true;
		});
		if (replaced)
		{
			__result = null;                                   // 调用方拿到 null 就不显示原版数字
		}
	}
}

/// <summary>
/// 「BLOCK」：Joker 的格挡完全挡住一次攻击时（原版 CreatureCmd.Damage 里 WasFullyBlocked → NDamageBlockedVfx.Create），
/// 原版的"格挡"字样换成 P5 的 BLOCK（Popups.Block），照 P3 mod 的做法。原版音效保留。
/// </summary>
[HarmonyPatch(typeof(NDamageBlockedVfx), nameof(NDamageBlockedVfx.Create))]
public static class BlockPopupPatch
{
	public static bool Prefix(Creature target, ref NDamageBlockedVfx? __result)
	{
		bool replaced = false;
		ModEntry.Safe("BlockPopup", () =>
		{
			if (target.IsPlayer && Joker.Is(target.Player) && NCombatRoom.Instance?.GetCreatureNode(target) is NCreature node
				&& Joker.FxLayer() is Godot.Control front)
			{
				Popups.Block(front, Joker.Chest(node), Joker.Facing(node));
				replaced = true;
			}
		});
		if (replaced)
		{
			__result = null;
			return false;
		}
		return true;
	}
}

/// <summary>Joker 格挡时原版的蓝色格挡火花也不要（换成 Popups.Block 的冲击）。</summary>
[HarmonyPatch(typeof(NBlockSparkVfx), nameof(NBlockSparkVfx.Create))]
public static class BlockSparkPatch
{
	public static bool Prefix(Creature target, ref NBlockSparkVfx? __result)
	{
		bool skip = false;
		ModEntry.Safe("BlockSpark", () => skip = target.IsPlayer && Joker.Is(target.Player));
		if (skip)
		{
			__result = null;
			return false;
		}
		return true;
	}
}

/// <summary>出牌那一刻（结算之前）：记下这张牌要的施法动作（Aim 改动画名时用）。</summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed))]
public static class CardPlayedPatch
{
	public static void Prefix(CardPlay cardPlay)
	{
		ModEntry.Safe("CardPlayed", () =>
		{
			if (Joker.OwnsCard(cardPlay.Card) && Joker.Node(cardPlay.Card.Owner) is NCreature node)
			{
				Aim.PendingCast[node.GetInstanceId()] = Casts.For(cardPlay.Card);
			}
		});
	}
}
