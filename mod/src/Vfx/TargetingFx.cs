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
		Pose.Enter(node, attack ? Pose.ReadySword : Pose.Aim);
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

	public static void End()
	{
		Unhover();
		P3Fx.Dismiss(_aura, 0.25f);
		_aura = null;
		if (_caster != null && GodotObject.IsInstanceValid(_caster))
		{
			Pose.Exit(_caster);
		}
		_caster = null;
	}
}
