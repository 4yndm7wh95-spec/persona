using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;

namespace P3Makoto.Patches;

/// <summary>0.15.31: CardModel.Portrait returns the approved P3R illustration (see Vfx/CardArt.cs); NCard.UpdatePortrait is its only reader.</summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.Portrait), MethodType.Getter)]
public static class CardArtPortraitPatch
{
	public static void Postfix(CardModel __instance, ref Texture2D __result)
	{
		Texture2D? art = null;
		CardModel card = __instance;
		ModEntry.Safe("card art", () => art = Vfx.CardArt.For(card));
		if (art != null) __result = art;
	}
}
