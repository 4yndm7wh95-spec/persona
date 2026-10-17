using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;
using P3Makoto.Vfx;

namespace P3Makoto.Patches;

/// <summary>v0.14.49：抽牌堆 / 弃牌堆 / 消耗堆界面打开时换成 P3R 菜单风格（Vfx/PileFx.cs）。出任何错都只记日志，原版界面照常。</summary>
[HarmonyPatch(typeof(NCardPileScreen), nameof(NCardPileScreen._Ready))]
public static class PileScreenPatch
{
	public static void Postfix(NCardPileScreen __instance)
	{
		ModEntry.Safe("pile screen", () =>
		{
			if (!PileFx.Enabled(__instance))
			{
				return;
			}
			try
			{
				PileFx.Build(__instance);
			}
			catch
			{
				PileFx.Rollback(__instance);           // 搭到一半出错：拆掉，原版网格重新显示，不能留一个空屏
				throw;
			}
		});
	}
}

/// <summary>v0.14.55：关闭牌堆界面时先播退场动画（PileFx.TryExit 接管原版的"藏起来 + 删掉"，播完再做）。出错就走原版。</summary>
[HarmonyPatch(typeof(NCardPileScreen), nameof(NCardPileScreen.AfterCapstoneClosed))]
public static class PileScreenClosePatch
{
	public static bool Prefix(NCardPileScreen __instance)
	{
		bool handled = false;
		ModEntry.Safe("pile screen exit", () => handled = PileFx.TryExit(__instance));
		return !handled;
	}
}
