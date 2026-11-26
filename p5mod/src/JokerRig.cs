using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace P5Joker;

internal static class JokerRig
{
	private static readonly (string GamePath, string PrivatePath)[] Paths =
	[
		("res://animations/characters/silent/silent_skel_data.tres", "res://p5joker/rig/combat/silent_skel_data.tres"),
		("res://animations/merchant/silent/silent_merchant_skel_data.tres", "res://p5joker/rig/merchant/silent_merchant_skel_data.tres"),
		("res://animations/rest_site/silent/rest_site_silent_skel_data.tres", "res://p5joker/rig/rest_site/rest_site_silent_skel_data.tres"),
	];

	private static Resource[]? _rigs;
	private static bool _reportedMissingRig;
	internal static bool Standalone { get; set; }
	internal static bool Registered => _rigs != null;

	internal static void Register()
	{
		if (!Standalone || JokerCostume.ManagedSelection.HasValue || _reportedMissingRig) return;
		if (_rigs == null)
		{
			var rigs = new Resource[Paths.Length];
			for (int i = 0; i < Paths.Length; i++)
			{
				string privatePath = Paths[i].PrivatePath;
				if (!ResourceLoader.Exists(privatePath) || ResourceLoader.Load(privatePath) is not Resource rig)
				{
					_reportedMissingRig = true;
					GD.PushWarning($"{ModEntry.Tag} Could not load {privatePath}; install the PCK matching the DLL.");
					return;
				}
				rigs[i] = rig;
			}
			_rigs = rigs;
			GD.Print($"{ModEntry.Tag} Registered private Silent Spine resources.");
		}
		for (int i = 0; i < Paths.Length; i++)
		{
			string gamePath = Paths[i].GamePath;
			if (!ResourceLoader.HasCached(gamePath) || ResourceLoader.GetCachedRef(gamePath) != _rigs[i])
				_rigs[i].TakeOverPath(gamePath);
		}
	}
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.AnimShake))]
internal static class JokerAnimationReadinessPatch
{
	// A visual shake may run inside PowerCmd.Apply. Skip the shake when no
	// animation state exists so the card can still apply its power and finish.
	private static bool Prefix(NCreature __instance)
	{
		if (!JokerRig.Standalone || __instance.Entity?.Player?.Character is not Silent)
			return true;
		if (__instance.Visuals?.SpineBody is not { } spine) return true;
		GodotObject sprite = spine.BoundObject;
		return GodotObject.IsInstanceValid(sprite) && sprite.HasMethod("get_animation_state")
			&& sprite.Call("get_animation_state").AsGodotObject() != null;
	}
}
