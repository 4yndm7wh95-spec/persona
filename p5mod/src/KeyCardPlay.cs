using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>
/// 关键牌出牌不在屏幕中央停（用户 2026-11-03：原版出牌后牌先在屏幕中央挡一下，召唤这些带动画的牌要看动画）。
/// 原版：出牌时 NCard.AnimCardToPlayPile 把牌移到屏幕中央（出牌堆），等整张牌结算完，CardPileCmd 再让它飞进弃牌堆。
/// 我们的召唤把结算推迟到人格面具出手 / 捏碎那一刻，牌就在屏幕中央挡了一两秒。
/// 这里：Joker 的关键牌（罕见及以上会召唤人格面具的牌、伸手抓人的毒牌）移到中央那一下，改成直接缩小飞向弃牌堆
/// （会消耗的飞向消耗堆、能力牌原地收掉）并淡出；之后游戏真正把牌放进弃牌堆时它已经是透明的，不会再飞一次。
/// 牌节点用的是对象池，放回池里时 Modulate 会被原版重置，不影响下次。
/// </summary>
[HarmonyPatch(typeof(NCard), nameof(NCard.AnimCardToPlayPile))]
public static class KeyCardPlayPatch
{
	public static void Postfix(NCard __instance) => ModEntry.Safe("KeyCardPlay", () =>
	{
		if (__instance.Model is not CardModel card || card.Owner is not { } p || !Joker.Is(p) || !LocalContext.IsMe(p)
			|| NCombatRoom.Instance == null || !IsKey(card))
		{
			return;
		}
		PileType dest = card.Type == CardType.Power ? PileType.None
			: card.Keywords.Contains(CardKeyword.Exhaust) ? PileType.Exhaust : PileType.Discard;
		__instance.PlayPileTween?.Kill();
		Tween tw = __instance.CreateTween().SetParallel();
		__instance.PlayPileTween = tw;
		if (dest == PileType.None)
		{
			// 能力牌：原地稍微放大一点、淡掉（原版能力牌也没有飞进牌堆）
			tw.TweenProperty(__instance, "scale", __instance.Scale * 1.08f, 0.25).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			tw.TweenProperty(__instance, "modulate:a", 0f, 0.25).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
			return;
		}
		// 飞进牌堆：先往上一挑、再加速扎进去，边飞边缩小、转一点、快到时淡掉（和原版进弃牌堆的感觉一样，但不在中央停）
		Vector2 target = dest.GetTargetPosition(__instance) - __instance.Size * 0.5f * 0.2f;
		const double fly = 0.38;
		tw.TweenProperty(__instance, "global_position", target, fly).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
		tw.TweenProperty(__instance, "scale", Vector2.One * 0.2f, fly).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		tw.TweenProperty(__instance, "rotation", __instance.Rotation + 0.35f, fly).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
		tw.TweenProperty(__instance, "modulate:a", 0f, 0.12).SetDelay(fly - 0.1);
	});

	/// <summary>要看动画的关键牌：会召唤人格面具（罕见及以上）或亚森伸手抓人的毒牌。</summary>
	private static bool IsKey(CardModel card) => Tiers.For(card) >= Tier.Uncommon || Aim.Grips(card.GetType().Name);
}
