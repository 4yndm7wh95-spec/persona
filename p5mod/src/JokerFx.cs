using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>
/// 把 Joker 的骨骼动画和 HitFx 对上：在姿势图（art/p5/poses/final）上量好的点换算到屏幕，按动画里的时刻放特效。
/// 时刻见 tools/p5/build_rig_p5.py：打击 attack / attack_chain / attack_from_ready 在 0.14s 出刀（刀光 0.13s 起扫，和出刀帧同一帧；
/// 转刀中途接上的 attack 要减去跳过的时间），
/// 开枪 shiv / shiv_from_aim 在 0.18s 开火（转枪中途接上的 shiv 要减去跳过的时间）。
/// </summary>
public static class JokerFx
{
	// 姿势图上的点：上撩时刀尖从左下（腰前）扫到右上方（出刀帧，身体前冲 ~126px）；圆心放在左上方远处，
	// 刀光是一道微微外凸的斜线，不绕着身体转半圈（用户：刀光太大）
	private static readonly Vector2 SwingFrom = new(600f, 850f), SwingTo = new(1376f, 450f), SwingCenter = new(760f, 260f);

	public static void OnAnim(NCreature node, string anim, float skipped)
	{
		if (Joker.FxLayer() is not Control layer)
		{
			return;
		}
		switch (anim)
		{
			case "attack":
			case "attack_chain":
			case "attack_from_ready":
				Fx.After(layer, Mathf.Max(0f, 0.13f - skipped), () => HitFx.Swing(layer, Joker.PosePoint(node, SwingCenter), Joker.PosePoint(node, SwingFrom),
					Joker.PosePoint(node, SwingTo)));
				break;
			case "shiv":
			case "shiv_from_aim":
				Fx.After(layer, Mathf.Max(0f, 0.18f - skipped), () => HitFx.Muzzle(layer, Joker.Muzzle(node), Joker.Facing(node)));
				break;
		}
	}

	/// <summary>伤害结算那一刻（DamagePatch）：敌人身上的命中特效。</summary>
	public static void OnHit(NCreature joker, NCreature target, bool gun)
	{
		if (Joker.FxLayer() is not Control layer)
		{
			return;
		}
		if (gun)
		{
			HitFx.Shot(layer, Joker.Muzzle(joker), target.VfxSpawnPosition, Joker.Facing(joker));
		}
		else
		{
			HitFx.Slash(layer, target.VfxSpawnPosition, Joker.Facing(joker));
		}
	}

	// 牌名 → 出牌特效（docs/p5/FX_PLAN.md）；能力牌默认是增益，防御类的格挡特效在 GainBlockPatch 里放
	private static readonly HashSet<string> Debuffs = new() { "Neutralize", "LegSweep", "PiercingWail", "Malaise", "SuckerPunch", "Expose", "Tracking" };
	private static readonly HashSet<string> Poisons = new() { "DeadlyPoison", "PoisonedStab", "BouncingFlask", "Catalyst", "Envenom", "Outbreak",
		"Concoct", "CorpseExplosion", "Bane" };

	/// <summary>出牌那一刻（CardPlayedPatch）：按牌型放特效。target = 选中的目标（群体牌为 null → 所有敌人）。</summary>
	public static void OnPlay(CardModel card, NCreature joker, Creature? target)
	{
		if (Joker.FxLayer() is not Control layer || NCombatRoom.Instance is not NCombatRoom room)
		{
			return;
		}
		string name = card.GetType().Name;
		List<NCreature> enemies = target != null && !target.IsPlayer && room.GetCreatureNode(target) is NCreature t
			? new List<NCreature> { t }
			: (card.CombatState?.HittableEnemies ?? Array.Empty<Creature>()).Select(room.GetCreatureNode).OfType<NCreature>().ToList();
		if (Debuffs.Contains(name))
		{
			// 有伤害的（中和 / 偷袭）等斩击打中再锁定：稍晚一点
			foreach (NCreature e in enemies)
			{
				Fx.After(layer, card.Type == CardType.Attack ? 0.2 : 0.05, () => CardFx.Debuff(layer, e.VfxSpawnPosition));
			}
		}
		else if (Poisons.Contains(name))
		{
			foreach (NCreature e in enemies)
			{
				Fx.After(layer, card.Type == CardType.Attack ? 0.2 : 0.08, () => CardFx.Curse(layer, e.VfxSpawnPosition));
			}
		}
		else if (card.Type == CardType.Power && Joker.FxLayer(back: true) is Control back)
		{
			Fx.After(layer, 0.1, () => CardFx.Buff(layer, back, Joker.PosePoint(joker, new Vector2(815f, 1622f)), Joker.PosePoint(joker, new Vector2(815f, 120f))));
		}
	}
}
