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

/// <summary>原版中和出牌时在敌人身上放的细斩（NThinSliceVfx）：Joker 出的牌不要，刀光由 HitFx / CardFx 放。</summary>
[HarmonyPatch(typeof(NThinSliceVfx), nameof(NThinSliceVfx.Create))]
public static class ThinSlicePatch
{
	public static bool Prefix(ref NThinSliceVfx? __result)
	{
		if (Godot.Time.GetTicksMsec() - JokerFx.LastPlayMs < 250)
		{
			__result = null;
			return false;
		}
		return true;
	}
}

/// <summary>小刀弹药（Vfx/Ammo.cs）：Joker 的能量球每帧更新旁边的左轮弹巢 = 手牌里的小刀牌张数。</summary>
[HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter._Process))]
public static class AmmoPatch
{
	private static readonly FieldInfo PlayerField = AccessTools.Field(typeof(NEnergyCounter), "_player");
	private static readonly Dictionary<ulong, AmmoView> Views = new();

	/// <summary>本地 Joker 的弹巢（开火时 JokerFx 让它转一格）。</summary>
	public static AmmoView? Local;

	public static void Postfix(NEnergyCounter __instance, double delta)
	{
		ModEntry.Safe("Ammo", () =>
		{
			if (PlayerField.GetValue(__instance) is not MegaCrit.Sts2.Core.Entities.Players.Player player || !Joker.Is(player))
			{
				return;
			}
			ulong id = __instance.GetInstanceId();
			if (!Views.TryGetValue(id, out AmmoView? view) || !Godot.GodotObject.IsInstanceValid(view.Root))
			{
				view = new AmmoView(__instance);
				Views[id] = view;
			}
			if (MegaCrit.Sts2.Core.Context.LocalContext.IsMe(player))
			{
				Local = view;
			}
			// 能量球右上方
			view.Root.Position = __instance.Size / 2f + new Godot.Vector2(110f, -70f);
			int shivs = player.PlayerCombatState?.Hand.Cards.Count(Joker.IsShot) ?? 0;
			view.Set(shivs);
			view.Tick((float)delta);
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
				List<NCreature> hit = targets.Where(c => !c.IsPlayer).Select(room.GetCreatureNode).OfType<NCreature>().ToList();
				if (gun && hit.Count > 1)
				{
					JokerFx.OnRicochet(joker, hit);                // 刀扇：一发子弹弹过所有敌人
					return;
				}
				foreach (NCreature n in hit)
				{
					JokerFx.OnHit(joker, n, gun);
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
			float wait = JokerFx.DamageNumberDelay(node);
			if (wait > 0f)
			{
				Fx.After(layer, wait, () => Popups.Damage(layer, node.GetInstanceId(), node.VfxSpawnPosition, amount, gun));
			}
			else
			{
				Popups.Damage(layer, node.GetInstanceId(), node.VfxSpawnPosition, amount, gun);
			}
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

/// <summary>Joker 用牌获得格挡（所有 GainBlock 重载都汇到带 cardPlay 的那个）：身前张开光盾（JokerFx.OnBlock）。</summary>
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
			if (amount > 0m && cardPlay != null && Joker.OwnsCard(cardPlay.Card) && creature.Player is { } p && Joker.Is(p)
				&& NCombatRoom.Instance?.GetCreatureNode(creature) is NCreature node)
			{
				JokerFx.OnBlock(node);
			}
		});
	}
}

/// <summary>原版致命毒药等出牌时在敌人身上放的毒雾（NPoisonImpactVfx）：Joker 出的牌不要，换成咒怨（CardFx.Eiha）。</summary>
[HarmonyPatch(typeof(NPoisonImpactVfx), nameof(NPoisonImpactVfx.Create), typeof(Creature))]
public static class PoisonImpactPatch
{
	public static bool Prefix(ref NPoisonImpactVfx? __result)
	{
		if (Godot.Time.GetTicksMsec() - JokerFx.LastPlayMs < 250)
		{
			__result = null;
			return false;
		}
		return true;
	}
}

/// <summary>
/// Joker 的刀砍下去和伤害同时到：原版中和等牌在普通速度下把出刀到结算拉长到 0.35s，Joker 的 attack 0.14s 就出刀了，
/// 命中特效和数字晚一截 —— Joker 的牌一律按静默猎手的 AttackAnimDelay（0.15s）结算。只改等待时间，不改结果。
/// </summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.WithAttackerAnim))]
public static class AttackDelayPatch
{
	public static void Prefix(AttackCommand __instance, string? animName, ref float delay)
	{
		if (animName == "Attack" && delay > 0.15f && __instance.ModelSource is CardModel card && Joker.OwnsCard(card))
		{
			delay = 0.15f;
		}
	}
}

/// <summary>出牌那一刻（结算之前）：技能 / 能力牌 Joker 自己施法（Aim.PlayCast），放出牌特效；记下施法动作（Aim 改动画名时用）。</summary>
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
				if (cardPlay.Card.Type != CardType.Attack)
				{
					Aim.PlayCast(node, cardPlay.Card);
				}
				JokerFx.OnPlay(cardPlay.Card, node, cardPlay.Target);
			}
		});
	}
}
