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
