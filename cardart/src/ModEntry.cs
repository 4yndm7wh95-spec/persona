using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

namespace P3CardArt;

[ModInitializer("Init")]
public static class ModEntry
{
    public const string Tag = "[P3CardArt]";

    public static void Init()
    {
        GD.Print($"{Tag} init v1.0.0 (standalone card illustrations)");
        try
        {
            new Harmony("brooks.p3cardart").CreateClassProcessor(typeof(CardArtPortraitPatch)).Patch();
            CardArt.Watch();
        }
        catch (Exception e)
        {
            GD.PrintErr($"{Tag} initialization failed; keeping original card art: {e}");
        }
    }
}

[HarmonyPatch(typeof(CardModel), nameof(CardModel.Portrait), MethodType.Getter)]
public static class CardArtPortraitPatch
{
    private static bool warned;

    public static void Postfix(CardModel __instance, ref Texture2D __result)
    {
        try
        {
            if (CardArt.For(__instance) is Texture2D art) __result = art;
        }
        catch (Exception e)
        {
            if (warned) return;
            warned = true;
            GD.PrintErr($"{ModEntry.Tag} portrait replacement failed; keeping original art: {e}");
        }
    }
}
