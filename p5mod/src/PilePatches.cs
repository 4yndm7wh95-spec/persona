using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace P5Joker;

/// <summary>抽牌堆界面打开时换成 P5R 暂停菜单风格（PileScreen.cs）。出任何错都只记日志，原版界面照常（P3 PilePatches 同款）。</summary>
[HarmonyPatch(typeof(NCardPileScreen), nameof(NCardPileScreen._Ready))]
public static class PileScreenPatch
{
	public static void Postfix(NCardPileScreen __instance)
	{
		ModEntry.Safe("pile screen", () =>
		{
			if (!PileScreen.Enabled(__instance.Pile)) return;
			try
			{
				PileScreen.Build(__instance, __instance.Pile);
			}
			catch
			{
				PileScreen.Rollback(__instance);           // 搭到一半出错：拆掉，原版网格重新显示
				throw;
			}
		});
	}
}

/// <summary>关闭牌堆界面时先播退场动画（PileScreen.TryExit 接管原版的「藏起来 + 删掉」，播完再做）。</summary>
[HarmonyPatch(typeof(NCardPileScreen), nameof(NCardPileScreen.AfterCapstoneClosed))]
public static class PileScreenClosePatch
{
	public static bool Prefix(NCardPileScreen __instance)
	{
		bool handled = false;
		ModEntry.Safe("pile screen exit", () => handled = PileScreen.TryExit(__instance));
		return !handled;
	}
}

/// <summary>牌组界面（地图 / 顶栏的牌组按钮，NDeckViewScreen）= P5R EQUIP 页（Vfx/PileDeck.cs），交互同另外三个牌堆。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.NDeckViewScreen), nameof(MegaCrit.Sts2.Core.Nodes.Screens.NDeckViewScreen._Ready))]
public static class DeckScreenPatch
{
	public static void Postfix(MegaCrit.Sts2.Core.Nodes.Screens.NDeckViewScreen __instance)
	{
		ModEntry.Safe("deck screen", () =>
		{
			if (Joker.LocalPlayer is not { } player) return;
			var pile = MegaCrit.Sts2.Core.Entities.Cards.PileTypeExtensions.GetPile(MegaCrit.Sts2.Core.Entities.Cards.PileType.Deck, player);
			if (!PileScreen.Enabled(pile)) return;
			try
			{
				PileScreen.Build(__instance, pile);
			}
			catch
			{
				PileScreen.Rollback(__instance);
				throw;
			}
		});
	}
}

/// <summary>牌组界面关闭：先播退场，播完再做原版的收尾（藏起来 + 删掉 + 顶栏牌组按钮动画）。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.NDeckViewScreen), nameof(MegaCrit.Sts2.Core.Nodes.Screens.NDeckViewScreen.AfterCapstoneClosed))]
public static class DeckScreenClosePatch
{
	public static bool Prefix(MegaCrit.Sts2.Core.Nodes.Screens.NDeckViewScreen __instance)
	{
		bool handled = false;
		ModEntry.Safe("deck screen exit", () => handled = PileScreen.TryExit(__instance));
		if (handled) ModEntry.Safe("deck top bar", () => MegaCrit.Sts2.Core.Nodes.NRun.Instance?.GlobalUi.TopBar.Deck.ToggleAnimState());
		return !handled;
	}
}
