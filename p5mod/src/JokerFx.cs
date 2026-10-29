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
		if (OwnSlashUntil.TryGetValue(target.GetInstanceId(), out ulong until) && Time.GetTicksMsec() < until)
		{
			return;                                               // 中和：这一刀的命中特效 CardFx.Neutralize 自己放（和斩预告函是同一刀）
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

	/// <summary>中和比原版多蓄这么久：Joker 握刀停住、头顶预告函转出来，再一刀劈下（伤害一起推迟）。</summary>
	public const float NeutralizeLead = 0.3f;     // 用户：0.6s 拖节奏，转快点、看得出在转就行

	/// <summary>这些敌人的命中特效由出牌特效自己放（到这个时刻为止，毫秒）。</summary>
	private static readonly Dictionary<ulong, ulong> OwnSlashUntil = new();

	/// <summary>最近一次 Joker 出牌的时刻（毫秒）：原版跟着出牌放的细斩特效（NThinSliceVfx）据此关掉。</summary>
	public static ulong LastPlayMs;

	/// <summary>
	/// 中和的攻击指令定下出刀延迟时（NeutralizeLeadPatch）：delay = 原版从播动画到结算伤害的等待。
	/// 是 Joker 的中和就：让 Joker 握刀停住、到点再出刀（Aim.HoldCut），头顶预告函转出、到点被这一刀斩开；返回 true 表示伤害要多等 NeutralizeLead。
	/// </summary>
	public static bool NeutralizeStart(CardModel? card, Creature? target, float delay)
	{
		if (card is not MegaCrit.Sts2.Core.Models.Cards.Neutralize || !Joker.OwnsCard(card) || target == null || Joker.FxLayer() is not Control layer
			|| Joker.Node(card.Owner) is not NCreature joker || NCombatRoom.Instance?.GetCreatureNode(target) is not NCreature t)
		{
			return false;
		}
		float cut = delay + NeutralizeLead;
		Aim.HoldCut[joker.GetInstanceId()] = cut;
		OwnSlashUntil[t.GetInstanceId()] = Time.GetTicksMsec() + (ulong)((cut + 0.4f) * 1000f);
		CardFx.Neutralize(layer, t.GetTopOfHitbox(), t.VfxSpawnPosition, Joker.Facing(joker), cut);
		return true;
	}

	/// <summary>爆发：复制 Joker 的骨骼节点放到本体后面，同一个动作同一时刻，交给 SkillFx.Burst 变成剪影分身。</summary>
	private static void BurstClone(Control layer, NCreature joker)
	{
		if (joker.Visuals?.Body is not Node2D body || body.GetParent() is not Node parent || joker.Visuals.SpineBody?.TryGetAnimationState() is not MegaAnimationState src)
		{
			return;
		}
		if (body.Duplicate() is not Node2D clone)
		{
			return;
		}
		parent.AddChild(clone);
		parent.MoveChild(clone, body.GetIndex());
		clone.Position = body.Position;
		var sprite = new MegaSprite(clone);
		if (sprite.TryGetAnimationState() is MegaAnimationState st)
		{
			string? anim = null;
			float time = 0f;
			try
			{
				anim = src.GetCurrentAnimationName(0);
				using MegaTrackEntry? e = src.GetCurrent(0);
				time = e?.GetTrackTime() ?? 0f;
			}
			catch (Exception)
			{
			}
			if (anim != null && sprite.HasAnimation(anim))
			{
				st.SetAnimation(anim, false, 0);
				using MegaTrackEntry? ce = st.GetCurrent(0);
				ce?.SetTrackTime(time);
				ce?.SetMixDuration(0f);
			}
		}
		SkillFx.Burst(layer, clone, m => sprite.SetNormalMaterial(m), Joker.Chest(joker), Joker.Facing(joker));
	}

	/// <summary>小刀打全体（刀扇）：按离 Joker 由近到远排，一发子弹依次弹过去。</summary>
	public static void OnRicochet(NCreature joker, List<NCreature> targets)
	{
		if (Joker.FxLayer() is not Control layer)
		{
			return;
		}
		Vector2 from = joker.GlobalPosition;
		HitFx.Ricochet(layer, Joker.Muzzle(joker), targets.Select(t => t.VfxSpawnPosition).OrderBy(p => p.DistanceSquaredTo(from)).ToList(),
			Joker.Facing(joker));
	}

	// 牌名 → 出牌特效（docs/p5/FX_PLAN.md）；能力牌默认是增益，防御类的格挡特效在 GainBlockPatch 里放
	private static readonly HashSet<string> Debuffs = new() { "LegSweep", "PiercingWail", "Malaise", "SuckerPunch", "Expose", "Tracking" };
	private static readonly HashSet<string> Poisons = new() { "DeadlyPoison", "PoisonedStab", "BouncingFlask", "Catalyst", "Envenom", "Outbreak",
		"Concoct", "CorpseExplosion", "Bane" };

	// 抽牌 / 生成小刀：张数；能量类
	private static readonly Dictionary<string, int> Draws = new()
	{
		["Acrobatics"] = 3, ["Backflip"] = 2, ["Prepared"] = 1, ["Expertise"] = 3, ["EscapePlan"] = 1, ["CalculatedGamble"] = 4,
	};
	private static readonly Dictionary<string, int> ShivMakers = new()
	{
		["BladeDance"] = 3, ["CloakAndDagger"] = 1, ["HiddenDaggers"] = 2, ["UpMySleeve"] = 3, ["StormOfSteel"] = 4,
	};

	// 姿势图 cast_glove 举起的手套
	private static readonly Vector2 GlovePx = new(989f, 400f);

	/// <summary>出牌那一刻（CardPlayedPatch）：按牌型放特效。target = 选中的目标（群体牌为 null → 所有敌人）。</summary>
	public static void OnPlay(CardModel card, NCreature joker, Creature? target)
	{
		LastPlayMs = Time.GetTicksMsec();
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
			// 有伤害的（偷袭）等斩击打中再锁定：稍晚一点
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
		else if (Draws.TryGetValue(name, out int draws))
		{
			Fx.After(layer, 0.08, () => CardFx.Draw(layer, Joker.PosePoint(joker, GlovePx), draws, Joker.Facing(joker)));
		}
		else if (ShivMakers.TryGetValue(name, out int shivs))
		{
			Fx.After(layer, 0.05, () => CardFx.Shivs(layer, Joker.Chest(joker), Joker.PosePoint(joker, GlovePx), shivs, Joker.Facing(joker)));
		}
		else if (name == "Adrenaline")
		{
			SkillFx.Adrenaline(layer, Joker.PosePoint(joker, new Vector2(815f, 1622f)), Joker.Chest(joker), Joker.Facing(joker));
		}
		else if (name == "BulletTime" && Joker.FxLayer(back: true) is Control btBack)
		{
			SkillFx.BulletTime(layer, btBack, Joker.Chest(joker), Joker.Facing(joker));
		}
		else if (name == "Burst")
		{
			Fx.After(layer, 0.06, () => BurstClone(layer, joker));
		}
		else if (card.Type == CardType.Power && Joker.FxLayer(back: true) is Control back)
		{
			Fx.After(layer, 0.1, () => CardFx.Buff(layer, back, Joker.PosePoint(joker, new Vector2(815f, 1622f)), Joker.PosePoint(joker, new Vector2(815f, 120f))));
		}
	}
}
