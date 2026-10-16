using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using P3Makoto.Core;

namespace P3Makoto.Patches;

/// <summary>
/// v0.14.48：在游戏加载储君相关资源之前，把原版路径指回本 mod 的资源（见 Core/AssetGuard.cs）。
/// 优先级最低（最后执行）：别的 mod 在同一处换资源的前缀先跑，我们最后盖回来。
/// </summary>
[HarmonyPatch]
public static class AssetGuardPreloadPatch
{
	public static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
	{
		foreach (string name in new[] { "LoadMainMenuAssets", "LoadRunAssets", "LoadRoomCombatAssets", "LoadRoomMerchantAssets", "LoadRoomRestSite", "LoadRoomEventAssets" })
		{
			if (AccessTools.Method(typeof(PreloadManager), name) is System.Reflection.MethodBase m)
			{
				yield return m;
			}
		}
	}

	[HarmonyPriority(Priority.Last)]
	public static void Prefix(System.Reflection.MethodBase __originalMethod)
	{
		ModEntry.Safe("asset guard", () => AssetGuard.Apply(__originalMethod.Name));
	}
}

[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
public static class AssetGuardSelectPatch
{
	[HarmonyPriority(Priority.First)]
	public static void Prefix()
	{
		ModEntry.Safe("asset guard", () => AssetGuard.Apply("select character"));
	}
}
