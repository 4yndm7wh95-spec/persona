using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace P3Makoto.Scenes;

/// <summary>仅接入明确认可的插画；独立于已停用的卡框换肤，不改卡框、材质、字体和布局。</summary>
internal static class ApprovedCardArt
{
	private const string Path = "res://p3makoto/approved_cards/gather_light.png";
	private static readonly AccessTools.FieldRef<NCard, TextureRect> Portrait = AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");
	private static Texture2D? _gatherLight;

	public static void Apply(NCard card)
	{
		// NCard 由原版 Reload 还原，再按当前模型判断，避免池化卡牌串图。
		if (card.Model is not GatherLight || Portrait(card) is not TextureRect portrait)
		{
			return;
		}
		_gatherLight ??= ResourceLoader.Load<Texture2D>(Path);
		if (_gatherLight != null)
		{
			portrait.Texture = _gatherLight;
		}
	}
}

[HarmonyPatch(typeof(NCard), "Reload")]
internal static class ApprovedCardReloadPatch
{
	public static void Postfix(NCard __instance) => ModEntry.Safe("approved card art", () => ApprovedCardArt.Apply(__instance));
}

[HarmonyPatch]
internal static class ApprovedCardPortraitPatch
{
	private static MethodBase? Target => AccessTools.Method(typeof(NCard), "UpdatePortrait");
	public static bool Prepare() => Target != null;
	public static MethodBase TargetMethod() => Target!;
	public static void Postfix(NCard __instance) => ModEntry.Safe("approved card art", () => ApprovedCardArt.Apply(__instance));
}
