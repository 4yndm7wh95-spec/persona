using Godot;
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
				// 第四版：手绘刀光扫过（Vfx/KnifeFx.cs）
				Fx.After(layer, Mathf.Max(0f, 0.13f - skipped), () => KnifeFx.Swing(layer, Joker.PosePoint(node, SwingFrom),
					Joker.PosePoint(node, SwingTo), Joker.Facing(node)));
				break;
			case "shiv":
			case "shiv_from_aim":
				Fx.After(layer, Mathf.Max(0f, 0.18f - skipped), () =>
				{
					GunFx.Muzzle(layer, Joker.Muzzle(node), new Vector2(Joker.Facing(node), 0.06f), Joker.Facing(node));   // 第四版枪口（Vfx/GunFx.cs）
					if (node.Entity?.Player is { } p && MegaCrit.Sts2.Core.Context.LocalContext.IsMe(p) && AmmoPatch.Local is AmmoView ammo
						&& GodotObject.IsInstanceValid(ammo.Root))
					{
						ammo.Fire();                              // 弹巢跟着开枪转一格
					}
				});
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
			GunFx.Shot(layer, Joker.Muzzle(joker), target.VfxSpawnPosition);                                       // 第四版弹道 + 命中
		}
		else
		{
			// 第四版刀命中：大招类攻击牌（HitWeights 定的重档）用重斩
			bool heavy = target.Entity != null && Hits.FloorOf(target.Entity).Floor == Weight.Heavy;
			KnifeFx.Hit(layer, target.VfxSpawnPosition, Joker.Facing(joker), heavy);
		}
	}

	/// <summary>最近一次 Joker 出牌的时刻（毫秒）：原版跟着出牌放的细斩特效（NThinSliceVfx）据此关掉。</summary>
	public static ulong LastPlayMs;

	/// <summary>弹射：敌人节点 → (记下的时刻, 伤害数字要晚几秒)。</summary>
	private static readonly Dictionary<ulong, (ulong At, float Delay)> NumberDelay = new();

	/// <summary>这个敌人的伤害数字要晚几秒（刀扇弹射时，等子弹弹到它）。</summary>
	public static float DamageNumberDelay(NCreature target) =>
		NumberDelay.TryGetValue(target.GetInstanceId(), out var d) && Time.GetTicksMsec() - d.At < 1000 ? d.Delay : 0f;

	/// <summary>小刀打全体（刀扇）：按离 Joker 由近到远排，一发子弹依次弹过去。</summary>
	public static void OnRicochet(NCreature joker, List<NCreature> targets)
	{
		if (Joker.FxLayer() is not Control layer)
		{
			return;
		}
		Vector2 from = joker.GlobalPosition;
		List<NCreature> order = targets.OrderBy(t => t.VfxSpawnPosition.DistanceSquaredTo(from)).ToList();
		ulong now = Time.GetTicksMsec();
		for (int i = 0; i < order.Count; i++)
		{
			NumberDelay[order[i].GetInstanceId()] = (now, HitFx.RicochetArrival(i));   // 伤害数字等子弹弹到它再跳
		}
		HitFx.Ricochet(layer, Joker.Muzzle(joker), order.Select(t => t.VfxSpawnPosition).ToList(), Joker.Facing(joker));
	}

	/// <summary>出牌那一刻（CardPlayedPatch）：按牌放出牌特效。target = 选中的目标（群体牌为 null）。</summary>
	public static void OnPlay(CardModel card, NCreature joker, Creature? target)
	{
		LastPlayMs = Time.GetTicksMsec();
		if (Joker.FxLayer() is not Control layer || NCombatRoom.Instance is not NCombatRoom room)
		{
			return;
		}
		NCreature? t = target != null && !target.IsPlayer ? room.GetCreatureNode(target) : null;
		switch (card.GetType().Name)
		{
			case "Neutralize" when t != null:
				// 刀落下（attack_from_ready 0.14s、伤害 0.15s）之后锁环收紧
				CardFx.Weaken(layer, t.VfxSpawnPosition, t.GetTopOfHitbox(), 0.12f);
				break;
			case "Outbreak":
			{
				// 第四版：0.7 的深处之手（DepthGrip）停用（用户 2026-11-02：0.7 只保留刀扇弹射）；施毒特效走 PoisonApplyPatch → PoisonFx，毒性爆发的专属演出另做
				break;
			}
			case var n when Aim.Grips(n) && t != null:
			{
				// 第四版：0.7 的亚森之手（Grip）停用；施毒特效走 PoisonApplyPatch → PoisonFx
				break;
			}
			case "DeadlyPoison" when t != null:
			{
				// 第四版：0.7 的精简咒怨（CardFx.CurseLight / Eiha）停用；施毒特效走 PoisonApplyPatch → PoisonFx
				break;
			}
		}
	}

	// cast_guard 的定格帧 guard_a2：竖起的拳头、身前盾的中心和上下端（姿势图像素）
	private static readonly Vector2 GuardFist = new(885f, 310f), ShieldMid = new(1060f, 470f), ShieldTop = new(1060f, -170f), ShieldBottom = new(1060f, 1110f);

	/// <summary>用牌获得格挡（GainBlockPatch）：身前张开光盾。</summary>
	public static void OnBlock(NCreature joker)
	{
		if (Joker.FxLayer() is not Control layer)
		{
			return;
		}
		float h = (Joker.PosePoint(joker, ShieldBottom) - Joker.PosePoint(joker, ShieldTop)).Length();
		Fx.After(layer, 0.03, () => CardFx.Guard(layer, Joker.PosePoint(joker, GuardFist), Joker.PosePoint(joker, ShieldMid), h, Joker.Facing(joker)));
	}
}
