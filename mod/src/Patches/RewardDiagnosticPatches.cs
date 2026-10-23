using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Rewards;
using P3Makoto.Core;

namespace P3Makoto.Patches;

[HarmonyPatch]
public static class KaleidoscopeDiagnosticPatch
{
	private static MethodBase? Find()
	{
		var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.Relics.Kaleidoscope");
		return type == null ? null : AccessTools.Method(type, "AfterObtained");
	}
	public static bool Prepare() => Find() != null;
	public static MethodBase? TargetMethod() => Find();
	public static void Prefix(RelicModel __instance, out bool __state)
	{
		bool enabled = false;
		ModEntry.Safe("kaleidoscope trace", () => enabled = Makoto.Is(__instance.Owner));
		__state = enabled;
		if (enabled) RewardDiagnostics.Begin();
	}
	public static void Postfix(bool __state, ref Task __result)
	{
		if (__state) __result = RewardDiagnostics.Observe(__result, "relic obtained", finishRelic: true);
	}
}

[HarmonyPatch(typeof(RewardsSet), nameof(RewardsSet.Offer))]
public static class RewardsOfferDiagnosticPatch
{
	public static void Postfix(RewardsSet __instance, ref Task __result)
	{
		Task original = __result;
		Task? traced = null;
		ModEntry.Safe("kaleidoscope offer trace", () =>
		{
			if (!RewardDiagnostics.Active || !Makoto.Is(__instance.Player)) return;
			RewardDiagnostics.Write("offer: " + RewardDiagnostics.Describe(__instance));
			traced = RewardDiagnostics.Observe(original, "offer", __instance);
		});
		if (traced != null) __result = traced;
	}
}

[HarmonyPatch(typeof(NRewardsScreen), nameof(NRewardsScreen.ShowScreen))]
public static class RewardsScreenDiagnosticPatch
{
	public static void Postfix(RewardsSet set, NRewardsScreen __result)
	{
		ModEntry.Safe("kaleidoscope screen trace", () =>
		{
			if (!RewardDiagnostics.Active || !Makoto.Is(set.Player)) return;
			RewardDiagnostics.Write($"rewards screen created: visible={__result.IsVisibleInTree()}; {RewardDiagnostics.Describe(set)}");
		});
	}
}
