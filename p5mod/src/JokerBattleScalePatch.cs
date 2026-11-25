using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace P5Joker;

[HarmonyPatch(typeof(NCreature), nameof(NCreature._Ready))]
public static class JokerBattleScalePatch
{
	public static void Postfix(NCreature __instance)
	{
		if (__instance.Entity?.Player?.Character is not Silent) return;
		// SkinChanger can replay Ready while still applying the saved visual transforms.
		Callable.From(() => Stage.Safe("Joker combat body scale", () =>
		{
			if (!Fx.Active(__instance) || !JokerCostume.Selected
				|| __instance.Visuals?.GetCurrentBody() is not Node2D body || !Fx.Active(body)
				|| JokerCostume.OwnsBody(body) != true) return;
			if (RestoreTinyBody(__instance, body))
				GD.Print("[P5Joker] restored undersized combat body to the base Silent scale");
		})).CallDeferred();
	}

	internal static bool RestoreTinyBody(CanvasItem creature, Node2D body)
	{
		// Measure relative to the creature so encounter zoom and multiplayer layout
		// aren't counted as a second scale on the character model.
		Transform2D local = creature.GetGlobalTransform().AffineInverse() * body.GlobalTransform;
		float x = local.X.Length(), y = local.Y.Length();
		const float tooSmall = Joker.BattleBodyScale * 0.45f;
		if (!float.IsFinite(x) || !float.IsFinite(y) || x <= 0.0001f || y <= 0.0001f
			|| x >= tooSmall || y >= tooSmall) return false;
		body.Scale *= Joker.BattleBodyScale / y;
		return true;
	}
}
