using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace P3CardArt;

[ModInitializer("Init")]
public static class ModEntry
{
    public const string Tag = "[P3CardArt]";

    public static void Init()
    {
        GD.Print($"{Tag} init v1.0.2 (portraits at the vanilla 250x190 size, 2x for full-size view)");
        try
        {
            Harmony harmony = new("brooks.p3cardart");
            harmony.CreateClassProcessor(typeof(CardArtPortraitPatch)).Patch();
            harmony.CreateClassProcessor(typeof(CardArtRefreshPatch)).Patch();
            GD.Print($"{Tag} portrait and card refresh patches installed");
            CardArt.Watch();
        }
        catch (Exception e)
        {
            GD.PrintErr($"{Tag} initialization failed; keeping original card art: {e}");
        }
    }
}

[HarmonyPatch]
public static class CardArtPortraitPatch
{
    private static bool warned;

    // BigPortrait is used by the full-size inspection view in 0.113; older versions omit it.
    public static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.PropertyGetter(typeof(CardModel), nameof(CardModel.Portrait));
        if (AccessTools.PropertyGetter(typeof(CardModel), "BigPortrait") is MethodInfo big) yield return big;
    }

    public static void Postfix(CardModel __instance, MethodBase __originalMethod, ref Texture2D __result)
    {
        try
        {
            if (CardArt.For(__instance, __originalMethod.Name.Contains("BigPortrait")) is Texture2D art) __result = art;
        }
        catch (Exception e)
        {
            if (warned) return;
            warned = true;
            GD.PrintErr($"{ModEntry.Tag} portrait replacement failed; keeping original art: {e}");
        }
    }
}

// Hook the rendering consumer as well: a small property getter may already have been inlined
// by the runtime before Harmony patches it. This covers hand, rewards, deck and pooled cards.
[HarmonyPatch(typeof(NCard), "UpdatePortrait")]
public static class CardArtRefreshPatch
{
    private static readonly FieldInfo Portrait = AccessTools.Field(typeof(NCard), "_portrait");
    private static readonly FieldInfo AncientPortrait = AccessTools.Field(typeof(NCard), "_ancientPortrait");
    private static readonly MethodInfo? RefreshBlur = AccessTools.Method(typeof(NCard), "UpdatePortraitBlurParams");
    private static bool warned;
    private static bool applied;

    public static void Postfix(NCard __instance)
    {
        try
        {
            if (__instance.Model is not CardModel model) return;
            FieldInfo field = model.Rarity == MegaCrit.Sts2.Core.Entities.Cards.CardRarity.Ancient ? AncientPortrait : Portrait;
            if (field.GetValue(__instance) is not TextureRect rect) return;
            // A vanilla texture wider than the 250 px atlas region means the game put the big portrait here
            // (0.113 inspection): keep it big so the full-size view is not upscaled from the small file.
            bool big = rect.Texture is Texture2D current && current.GetWidth() > 300;
            if (CardArt.For(model, big) is not Texture2D art) return;
            rect.Texture = art;
            RefreshBlur?.Invoke(__instance, null);
            if (applied) return;
            applied = true;
            GD.Print($"{ModEntry.Tag} card refresh replaced illustration: {model.Id.Entry}");
        }
        catch (Exception e)
        {
            if (warned) return;
            warned = true;
            GD.PrintErr($"{ModEntry.Tag} card refresh replacement failed: {e}");
        }
    }
}
