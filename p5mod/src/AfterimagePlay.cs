using System;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>
/// 余像能力（之后每打出一张牌获得 1 点格挡）的持续表现（用户 2026-11-12：能力牌更关键的是打出之后的表现，每次获得格挡都要有体现、但不用太明显）。
/// 原版在 AfterimagePower.AfterCardPlayed 里给格挡：这里在它开头放一层很淡的红色残影（SkillRedo4.AfterimageTick）；
/// 这次格挡不再弹蜂巢护盾（GainBlockPatch 看 Depth），不然每张牌都立一次大盾太吵。只改表现。
/// </summary>
[HarmonyPatch(typeof(AfterimagePower), nameof(AfterimagePower.AfterCardPlayed))]
public static class AfterimageTickPatch
{
	/// <summary>正在 AfterCardPlayed 的同步部分里（GainBlock 在这段里被调用）。</summary>
	internal static int Depth;

	public static void Prefix(AfterimagePower __instance, object[] __args)
	{
		Depth++;
		Stage.Safe("afterimage tick", () =>
		{
			CardPlay? play = __args.OfType<CardPlay>().FirstOrDefault();
			if (play?.Card?.Owner?.Creature is not Creature cr || cr != __instance.Owner || __instance.Amount <= 0 || cr.Player is not { } p || !Joker.Is(p))
			{
				return;
			}
			if (NCombatRoom.Instance?.GetCreatureNode(cr) is NCreature node)
			{
				SkillPlay.AfterimageTick(node);
			}
		});
	}

	public static void Finalizer() => Depth = Math.Max(0, Depth - 1);
}
