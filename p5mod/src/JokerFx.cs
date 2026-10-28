using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace P5Joker;

/// <summary>
/// 把 Joker 的骨骼动画和 HitFx 对上：在姿势图（art/p5/poses/final）上量好的点换算到屏幕，按动画里的时刻放特效。
/// 时刻见 tools/p5/build_rig_p5.py：打击 attack / attack_chain 在 0.14s 出刀（刀光 0.09s 起扫），
/// 开枪 shiv / shiv_from_aim 在 0.18s 开火（转枪中途接上的 shiv 要减去跳过的时间）。
/// </summary>
public static class JokerFx
{
	// 姿势图上的点：上撩时刀尖从腰后（准备帧，身体后撤 ~28px）扫到右上方（出刀帧，身体前冲 ~126px）
	private static readonly Vector2 SwingFrom = new(532f, 860f), SwingTo = new(1376f, 450f), SwingCenter = new(930f, 640f);

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
				Fx.After(layer, 0.09, () => HitFx.Swing(layer, Joker.PosePoint(node, SwingCenter), Joker.PosePoint(node, SwingFrom),
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
}
