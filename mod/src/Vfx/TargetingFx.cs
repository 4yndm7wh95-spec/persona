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
	// 每次进入预备姿势 +1：上一张牌打出后留下的"0.6 秒仍在预备就放下"兜底，只对它自己那次预备生效。
	// 出牌太快时（打击刚出手就拖下一张召唤牌），旧兜底不能把新举起的召唤器放下。
	private static int _poseToken;

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
		_poseToken++;
		// 要花辉星的牌：对应张数的塔罗牌从环绕轨道里抽出，排在身前待命
		// 不花辉星的牌 GetStarCostWithModifiers() 返回 -1，不能当成 X 费
		int starCost = card.HasStarCostX ? -1 : card.GetStarCostWithModifiers();
		if (card.HasStarCostX || starCost > 0)
		{
			StarOrbit.Preview(node, starCost);
		}
		if (card.GetType().Name == "Stardust")
		{
			StarOrbit.ArmVolley(node);
		}
		bool attack = card.Type == CardType.Attack;
		Color tint = card.Type == CardType.Power ? Pal.DarkHour : Pal.Cyan;
		_aura = P3Fx.Aura(back, front, Makoto.Feet(node), Makoto.Head(node), tint, attack ? 0.7f : 0.35f);
		// 只有挥剑的攻击牌和召唤 Persona 的大招才做预备动作；其他牌只显示法阵
		CardStyles.CardAnim anim = CardStyles.AnimFor(card, card.Type);
		attack = anim == CardStyles.CardAnim.Sword;
		if (anim is CardStyles.CardAnim.Sword or CardStyles.CardAnim.Evoker && Pose.Enter(node, attack ? Pose.ReadySword : Pose.Aim))
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
				Audio.Sfx.Play(Audio.Snd.Cock, 0.8f, delay: 0.12f);     // 选中召唤器牌、举起召唤器后稍停一下：上膛"咔嚓"
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
		StarOrbit.ClearPreview();
		StarOrbit.DisarmVolley();
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
		StarOrbit.ClearPreview();
		Unhover();
		P3Fx.Dismiss(_aura, 0.25f);
		_aura = null;
		NCreature? caster = _caster;
		_caster = null;
		if (caster == null || !GodotObject.IsInstanceValid(caster))
		{
			return;
		}
		int token = _poseToken;
		Fx.After(caster, 0.6, "pose fallback", () =>
		{
			if (token == _poseToken && !Active && GodotObject.IsInstanceValid(caster) && Pose.IsHolding(caster))
			{
				Release(caster);
			}
		});
	}

	/// <summary>播放放下动画；武器的消散特效由放下动画里的 weapon_sheath / evoker_sheath 事件在正确的那一帧触发。</summary>
	private static void Release(NCreature caster)
	{
		Pose.Exit(caster);
	}
}
