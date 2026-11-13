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
	public static void OnHit(NCreature joker, NCreature target, bool gun, bool final = false)
	{
		if (Joker.FxLayer() is not Control layer)
		{
			return;
		}
		if (gun)
		{
			GunFx.Shot(layer, Joker.Muzzle(joker), target.VfxSpawnPosition, final);                                       // 第四版弹道 + 命中
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

	/// <summary>这个敌人接下来的伤害数字晚 delay 秒再跳（群蛇形态：等蛇咬到它）。</summary>
	public static void DelayNumber(NCreature target, float delay) => NumberDelay[target.GetInstanceId()] = (Time.GetTicksMsec(), delay);

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

	/// <summary>蛇咬打出后这段时间里，原版的獠牙特效（vfx/vfx_bite）不显示。</summary>
	public static ulong HideBiteUntil;

	/// <summary>出牌那一刻（CardPlayedPatch）：按牌放出牌特效。target = 选中的目标（群体牌为 null）。</summary>
	public static void OnPlay(CardModel card, NCreature joker, Creature? target)
	{
		LastPlayMs = Time.GetTicksMsec();
		if (Joker.FxLayer() is not Control layer || NCombatRoom.Instance is not NCombatRoom room)
		{
			return;
		}
		NCreature? t = target != null && !target.IsPlayer ? room.GetCreatureNode(target) : null;
		if (card.GetType().Name != "BulletTime")
		{
			TimeStop.Current?.Card(0f);   // 子弹时间里每出一张牌：打出那一刻就给反馈（和人物动作同时起步，用户 2026-11-12：不要等出手、不要延迟）
		}
		switch (card.GetType().Name)
		{
			case "Neutralize" when t != null:
				// 刀落下（attack_from_ready 0.14s、伤害 0.15s）之后锁环收紧
				CardFx.Weaken(layer, t.VfxSpawnPosition, t.GetTopOfHitbox(), 0.12f);
				break;
			case "Outbreak":
			{
				// 第四版：稀有召唤（眼神特写 + 舞台 + 新亚森，Aim）→ 亚森出手，咒怨光束扇形射向每个敌人（PoisonApplyPatch）
				// → 牌效果「立即触发中毒」时所有敌人的咒火一起引爆（PoisonFx.Blast）。0.7 的深处之手（DepthGrip）停用。
				PoisonFx.BeginOutbreak(Aim.HitFor(card) + 1.5f);
				break;
			}
			case "CorrosiveWave":
			{
				// 第四版：亚森直接现身（按罕见召唤，Tiers.PlainRare）→ 亚森出手时把咒怨砸进 Joker 身前的地面（CorrosiveFx.Plunge）；
				// 之后这回合每抽一张牌，咒火浪贴地涌向敌人（CorrosiveDrawPatch → CorrosiveFx.OnDraw），脚下咒怨一直翻腾（AmmoPatch → Aura）
				Vector2 feet = Joker.PosePoint(joker, ReadyFx.Feet);
				float face = Joker.Facing(joker);
				Vector2 ground = feet + new Vector2(130f * face, 0f);
				Fx.After(layer, Aim.HitFor(card) + 0.01f, () =>
					CorrosiveFx.Plunge(layer, ArseneFx.TryClaw(out Vector2 claw) ? claw : ground + new Vector2(-200f * face, -420f), ground));
				break;
			}
			case "FanOfKnives":
			{
				// 刀扇：转一下枪（Aim.PlayTwirl 播 aim_out），指尖一圈红光环，枪停下所有敌人闪锁定框
				var enemies = new List<ShivFx.Target>();
				if (joker.Entity?.CombatState is { } cs)
				{
					foreach (Creature c in cs.HittableEnemies)
					{
						if (room.GetCreatureNode(c) is NCreature n)
						{
							enemies.Add(new ShivFx.Target(n.VfxSpawnPosition, Mathf.Clamp((n.VfxSpawnPosition - n.GetTopOfHitbox()).Length() * 2.4f, 160f, 340f)));
						}
					}
				}
				ShivFx.FanOfKnives(layer, Joker.PosePoint(joker, ReadyFx.GunGrip), enemies);
				break;
			}
			case "BouncingFlask":
				SnakeFx.BeginFlask(card.DynamicVars.Repeat.IntValue);
				break;
			case var n when Aim.Grips(n) && t != null:
			{
				// 第四版：0.7 的亚森之手（Grip）停用；施毒特效走 PoisonApplyPatch → PoisonFx
				break;
			}
			case "Snakebite" when t != null:
			{
				// 用户 2026-11-13（B 方案）：召唤亚森（Aim.Commands）→ 亚森出手时一挥手，蛇形咒怨光带游到敌人斜上方，咒烟鼓起，
				// 蛇从烟里窜出来咬（群蛇形态的扑咬，原来的毒的落点不变：不从亚森爪尖甩光束）；上毒等到咬合那一刻
				HideBiteUntil = Time.GetTicksMsec() + 4000;
				Control back = Joker.FxLayer(back: true) ?? layer;
				float tall = (Joker.PosePoint(joker, ReadyFx.Feet) - Joker.PosePoint(joker, new Vector2(815f, 120f))).Length();
				float face = Joker.Facing(joker);
				float hit = Aim.HitFor(card);
				float biteIn = hit + SnakeSummonFx.Lag + SerpentMantle.BiteDelay;
				Aim.HoldCast(joker, biteIn);
				PoisonFx.NoBeamUntil = Stage.NowMs() + (ulong)((biteIn + 0.6f) * 1000f);
				NCreature tn = t;
				Vector2 fallback = Joker.Chest(joker) + new Vector2(-face * tall * 0.3f, -tall * 0.6f);
				Fx.After(layer, hit, () =>
				{
					if (!GodotObject.IsInstanceValid(tn)) return;
					Vector2 at = tn.Hitbox.GetGlobalRect().GetCenter();
					Node2D? vis = tn.Visuals;
					// 从亚森抬到下巴的那只手前面放出来（用户 2026-11-13：不要接到他头上；爪尖记的是伸臂出手姿势的点，这个姿势下落在帽子后面）
					Vector2 jf = Joker.PosePoint(joker, ReadyFx.Feet);
					Vector2 hand = Persona.ChestOf(Persona.ArseneSpec, jf, tall * 1.35f, face) + new Vector2(face * tall * 0.37f, -tall * 0.41f);
					ArseneFx.WhenClaw(layer, fallback, _ =>
					{
						Vector2 claw = hand;
						SnakeSummonFx.Play(layer, back, claw, at, tall, face);
						Fx.After(layer, SnakeSummonFx.Lag, () => { if (GodotObject.IsInstanceValid(tn)) SerpentMantle.Bite(layer, back, at, vis, tall, face); });
					});
				});
				break;
			}
			case "DeadlyPoison" when t != null:
			{
				// 第四版：0.7 的精简咒怨（CardFx.CurseLight / Eiha）停用；施毒特效走 PoisonApplyPatch → PoisonFx
				break;
			}
			default:
				SkillPlay.OnPlay(card, joker, t, layer, room);       // 其余技能 / 能力牌（用户 2026-11-10）：按牌拼 SkillFx 的积木
				break;
		}
	}

	// cast_guard 的定格帧 guard_a2：竖起的拳头、身前盾的中心和上下端（姿势图像素）
	private static readonly Vector2 GuardFist = new(885f, 310f), ShieldMid = new(1060f, 470f), ShieldTop = new(1060f, -170f), ShieldBottom = new(1060f, 1110f);

	/// <summary>用牌获得格挡（GainBlockPatch）：身前张开光盾。</summary>
	private static ulong _lastGuardMs;

	public static void OnBlock(NCreature joker, int amount = 5)
	{
		if (Joker.FxLayer() is not Control layer)
		{
			return;
		}
		// 用户 2026-11-08 指定照结城理 mod 的六边形蜂巢护盾（着色器版，Vfx/HexGuard.cs）；2026-11-10 接进游戏。
		// 一张牌里连着几次获得格挡（比如带额外格挡的遗物）只立一次盾
		ulong now = Time.GetTicksMsec();
		if (now - _lastGuardMs < 350)
		{
			return;
		}
		_lastGuardMs = now;
		Vector2 feet = Joker.PosePoint(joker, ReadyFx.Feet);
		float tall = (feet - Joker.PosePoint(joker, new Vector2(815f, 120f))).Length();
		bool guarding = joker.Visuals?.SpineBody?.TryGetAnimationState() is { } st && Aim.Current(st) is "cast_guard" or "cast_guard_from_ready";
		HexGuard.Play(layer, Joker.Chest(joker), tall, Joker.Facing(joker), amount, guarding ? Joker.PosePoint(joker, GuardFist) : null);
	}
}
