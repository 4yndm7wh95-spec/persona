using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Audio;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace P5Joker;

[HarmonyPatch(typeof(NMerchantInventory), "OnPurchaseCompleted")]
internal static class MorganaShopPurchaseVoice
{
    private static readonly string[] Lines = { "shop_buy", "shop_buy_04", "shop_buy_05" };

    internal static void Postfix(NMerchantInventory __instance, PurchaseStatus status)
    {
        if (status == PurchaseStatus.Success && __instance.IsOpen && Joker.LocalRun)
            Sfx.Play(Lines[GD.RandRange(0, Lines.Length - 1)]);
    }
}

[HarmonyPatch(typeof(NMerchantInventory), "Close")]
internal static class MorganaShopExitVoice
{
    internal static void Prefix(NMerchantInventory __instance, out bool __state) =>
        __state = __instance.IsOpen && Joker.LocalRun;

    internal static void Postfix(NMerchantInventory __instance, bool __state)
    {
        if (__state && !__instance.IsOpen)
            Sfx.Play("shop_exit");
    }
}

[HarmonyPatch(typeof(NAudioManager), nameof(NAudioManager.PlayOneShot),
    new Type[] { typeof(string), typeof(Dictionary<string, float>), typeof(float) })]
internal static class MorganaShopNativeVoice
{
    internal static bool Prefix(string path) =>
        !path.StartsWith("event:/sfx/npcs/merchant/", StringComparison.Ordinal) || !Joker.LocalRun;
}
