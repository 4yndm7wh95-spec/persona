using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace P5Joker;

/// <summary>
/// 关键牌出牌时别挡住动画（用户 2026-11-03 / 11-04）。原版：打出的牌先飞到屏幕中央（出牌堆），整张牌结算完再飞进弃牌堆；
/// 我们的召唤把结算推迟到人格面具出手 / 捏碎那一刻，牌就在屏幕中央挡了一两秒。
/// 第一版让牌直接飞进弃牌堆，结果蛇咬这类牌的动画特效全没了（用户 11-04 报的 bug）→ 现在照原版逻辑走，只改透明度：
/// 飞到中央时牌明显变透明（不挡动画），飞进弃牌堆时再逐渐恢复不透明。原版的补间一个都不动，只在牌节点上另加一个透明度补间。
/// 牌节点用的是对象池，放回池里时 Modulate 会被原版重置，不影响下次。
/// </summary>
public static class KeyCardPlay
{
	/// <summary>到中央时的透明度。</summary>
	private const float CenterAlpha = 0.3f;

	/// <summary>Joker 的关键牌飞向屏幕中央：同时淡到 CenterAlpha。</summary>
	public static void Fade(NCard node)
	{
		if (node.Model is not CardModel card || card.Owner is not { } p || !Joker.Is(p) || !LocalContext.IsMe(p) || !IsKey(card))
		{
			return;
		}
		node.CreateTween().TweenProperty(node, "modulate:a", CenterAlpha, 0.22).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
	}

	/// <summary>牌飞向弃牌堆 / 消耗堆（NCardFlyVfx）：之前被淡掉的牌逐渐恢复不透明。</summary>
	public static void Restore(NCard node)
	{
		if (node.Modulate.A < 0.99f)
		{
			node.CreateTween().TweenProperty(node, "modulate:a", 1f, 0.45).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
		}
	}

	/// <summary>要看动画的关键牌：会召唤人格面具（罕见及以上）或亚森伸手抓人的毒牌。</summary>
	private static bool IsKey(CardModel card) => Tiers.For(card) >= Tier.Uncommon || Aim.Grips(card.GetType().Name);
}

/// <summary>远程 / 选择后重新回到出牌堆的牌（NCard.AnimCardToPlayPile）：淡到透明。</summary>
[HarmonyPatch(typeof(NCard), nameof(NCard.AnimCardToPlayPile))]
public static class KeyCardAnimToPlayPatch
{
	public static void Postfix(NCard __instance) => ModEntry.Safe("KeyCardAnimToPlay", () => KeyCardPlay.Fade(__instance));
}

/// <summary>
/// 本地玩家手动出牌：不走 AnimCardToPlayPile，而是 CardPileCmd.AddDuringManualCardPlay 自己建飞到中央的补间
/// （同步部分在第一个 await 之前就建好了）→ 挂在它后面加一个透明度补间。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Commands.CardPileCmd), nameof(MegaCrit.Sts2.Core.Commands.CardPileCmd.AddDuringManualCardPlay))]
public static class KeyCardManualPlayPatch
{
	public static void Postfix(CardModel card) => ModEntry.Safe("KeyCardManualPlay", () =>
	{
		if (NCard.FindOnTable(card) is NCard node)
		{
			KeyCardPlay.Fade(node);
		}
	});
}

/// <summary>牌飞进牌堆（结算完以后）：淡掉的关键牌逐渐恢复不透明。</summary>
[HarmonyPatch(typeof(NCardFlyVfx), nameof(NCardFlyVfx.Create), typeof(NCard), typeof(PileType), typeof(bool), typeof(string))]
public static class KeyCardFlyRestorePatch
{
	public static void Postfix(NCard card) => ModEntry.Safe("KeyCardFlyRestore", () => KeyCardPlay.Restore(card));
}
