using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 预备出牌（拖牌选目标）期间的常驻表现：脚下法阵 + 光点 + 蓝蝶 + 预备姿势；悬停敌人时显示锁定框。
/// 全局只存在一份，Begin/End 都是幂等的。
/// </summary>
public static class TargetingFx
{
	private static Node2D? _aura;
	private static Node2D? _reticle;
	private static NCreature? _caster;
	private static NCreature? _hovered;

	public static bool Active => _aura != null && GodotObject.IsInstanceValid(_aura);

	public static void Begin(CardModel card)
	{
		if (Active || !Makoto.Is(card.Owner))
		{
			return;
		}
		NCreature? node = Makoto.Node(card.Owner);
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (node == null || back == null || front == null)
		{
			return;
		}
		_caster = node;
		bool attack = card.Type == CardType.Attack;
		Color tint = card.Type == CardType.Power ? Pal.DarkHour : Pal.Cyan;
		_aura = P3Fx.Aura(back, front, Makoto.Feet(node), Makoto.Head(node), tint, attack ? 0.7f : 0.35f);
		if (Pose.Enter(node, attack ? Pose.ReadySword : Pose.Aim))
		{
			SpineHooks.Hands hands = SpineHooks.Of(node);
			if (attack && !hands.SwordOut)
			{
				// 等姿势切换后剑到位再显现（下一帧骨骼才更新）
				Fx.After(front, 0.05, "sword form", () => WeaponFx.SwordMaterialize(front, node));
				hands.SwordOut = true;
			}
			else if (!attack && !hands.EvokerOut)
			{
				Fx.After(front, 0.08, "evoker glint", () => WeaponFx.EvokerGlint(front, node));
				hands.EvokerOut = true;
			}
		}
	}

	public static void Hover(NCreature target)
	{
		if (!Active || target == _hovered)
		{
			return;
		}
		Unhover();
		Control? front = Makoto.FxLayer();
		if (front == null)
		{
			return;
		}
		_hovered = target;
		_reticle = P3Fx.Reticle(front, target.VfxSpawnPosition);
	}

	public static void Unhover()
	{
		P3Fx.Dismiss(_reticle, 0.15f);
		_reticle = null;
		_hovered = null;
	}

	/// <summary>取消出牌：收起法阵，退出预备姿势，武器消散。</summary>
	public static void End()
	{
		Unhover();
		P3Fx.Dismiss(_aura, 0.25f);
		_aura = null;
		if (_caster != null && GodotObject.IsInstanceValid(_caster))
		{
			Release(_caster);
		}
		_caster = null;
	}

	/// <summary>
	/// 牌已打出：只收法阵，不动姿势和武器（接下来的攻击 / 施法动画会接管）。
	/// 如果这张牌没有触发任何动画，0.6 秒后仍停在预备姿势，就按取消处理。
	/// </summary>
	public static void EndPlayed()
	{
		Unhover();
		P3Fx.Dismiss(_aura, 0.25f);
		_aura = null;
		NCreature? caster = _caster;
		_caster = null;
		if (caster == null || !GodotObject.IsInstanceValid(caster))
		{
			return;
		}
		Fx.After(caster, 0.6, "pose fallback", () =>
		{
			if (GodotObject.IsInstanceValid(caster) && Pose.IsHolding(caster))
			{
				Release(caster);
			}
		});
	}

	private static void Release(NCreature caster)
	{
		if (!Pose.Exit(caster))
		{
			return;
		}
		SpineHooks.Hands hands = SpineHooks.Of(caster);
		Control? front = Makoto.FxLayer();
		if (front != null && hands.SwordOut)
		{
			WeaponFx.SwordDissolve(front, caster);
		}
		else if (front != null && hands.EvokerOut)
		{
			WeaponFx.EvokerGlint(front, caster);
		}
		hands.SwordOut = false;
		hands.EvokerOut = false;
	}
}
