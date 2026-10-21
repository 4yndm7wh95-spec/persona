using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using P3Makoto.Core;
using MegaCrit.Sts2.Core.Nodes.Cards;

namespace P3Makoto.Scenes;

/// <summary>用户选择的整套 v4 插画；保持原版卡框、材质和布局。</summary>
internal static class ApprovedCardArt
{
	private const string Directory = "res://p3makoto/card_art_v4/";
	private static readonly HashSet<string> Cards = new(StringComparer.Ordinal)
	{
		"alignment",
		"arsenal",
		"astral_pulse",
		"beat_into_shape",
		"begone",
		"big_bang",
		"black_hole",
		"bombardment",
		"bulwark",
		"bundle_of_joy",
		"celestial_might",
		"charge",
		"child_of_the_stars",
		"cloak_of_stars",
		"collision_course",
		"comet",
		"conqueror",
		"constellation",
		"convergence",
		"cosmic_indifference",
		"crash_landing",
		"crescent_spear",
		"crush_under",
		"decisions_decisions",
		"defend_regent",
		"devastate",
		"dying_star",
		"falling_star",
		"foregone_conclusion",
		"furnace",
		"gamma_blast",
		"gather_light",
		"genesis",
		"glimmer",
		"glitterstream",
		"glow",
		"guards",
		"guiding_star",
		"hammer_time",
		"heavenly_drill",
		"hegemony",
		"heirloom_hammer",
		"hidden_cache",
		"i_am_invincible",
		"kingly_kick",
		"kingly_punch",
		"knockout_blow",
		"know_thy_place",
		"largesse",
		"lunar_blast",
		"make_it_so",
		"manifest_authority",
		"meteor_shower",
		"monarchs_gaze",
		"monologue",
		"neutron_aegis",
		"orbit",
		"pale_blue_dot",
		"parry",
		"particle_wall",
		"patter",
		"photon_cut",
		"pillar_of_creation",
		"plot",
		"prophesize",
		"quasar",
		"radiate",
		"refine_blade",
		"reflect",
		"reserves",
		"resonance",
		"royal_corona",
		"royal_gamble",
		"royalties",
		"seeking_edge",
		"seven_stars",
		"shining_strike",
		"solar_strike",
		"sovereign_blade",
		"spectrum_shift",
		"spoils_of_battle",
		"stardust",
		"strike_regent",
		"summon_forth",
		"supermassive",
		"sword_sage",
		"terraforming",
		"the_sealed_throne",
		"the_smith",
		"tutor",
		"tyranny",
		"venerate",
		"void_form",
		"wrought_in_war",
	};
	private static readonly AccessTools.FieldRef<NCard, TextureRect> Portrait = AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");
	private static readonly Dictionary<string, Texture2D> Cache = new(StringComparer.Ordinal);

	public static void Apply(NCard card)
	{
		if (card.Model == null || Portrait(card) is not TextureRect portrait) return;
		string id = card.Model.Id.Entry.ToLowerInvariant();
		if (!Cards.Contains(id)) return;
		if (!P3Settings.Current.CardArt)
		{
			portrait.Texture = card.Model.Portrait;
			return;
		}
		if (!Cache.TryGetValue(id, out Texture2D? art))
		{
			art = ResourceLoader.Load<Texture2D>(Directory + id + ".png");
			if (art == null) return; // 缺图保留原版；不缓存失败，后续仍可重试。
			Cache[id] = art;
		}
		portrait.Texture = art;
	}

	public static void RefreshExisting()
	{
		if (Engine.GetMainLoop() is SceneTree tree) Visit(tree.Root);
	}

	private static void Visit(Node node)
	{
		if (node is NCard card) ModEntry.Safe("refresh card art", () => Apply(card));
		foreach (Node child in node.GetChildren()) Visit(child);
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
