using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P3Makoto.Patches;

/// <summary>Only the local Makoto run's merchant room receives the Velvet Room presentation.</summary>
[HarmonyPatch(typeof(NMerchantRoom), nameof(NMerchantRoom._Ready))]
public static class VelvetShopPatch
{
	public static void Postfix(NMerchantRoom __instance) =>
		ModEntry.Safe("velvet shop", () => Install(__instance));

	private static void Install(NMerchantRoom room)
	{
		if (!Stage.MakotoRun || room.HasNode("P3VelvetShop")) return;
		// Load before hiding anything: missing artwork leaves the native room usable.
		var texture = ResourceLoader.Load<Texture2D>("res://p3makoto/shop/velvet_room.png");
		var shader = ResourceLoader.Load<Shader>("res://p3makoto/shaders/velvet_shop.gdshader");
		if (texture == null || shader == null) return;
		var scenery = room.GetNode<CanvasItem>("SceneContainer");
		var merchant = room.MerchantButton;
		var material = new ShaderMaterial { Shader = shader };
		var background = new TextureRect
		{
			Name = "P3VelvetShop",
			Texture = texture,
			Material = material,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		room.AddChild(background);
		background.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		room.MoveChild(background, 0);

		// Keep the native button and its release/targeting/dead-player handling.
		// It must leave SceneContainer before we hide that container (all players,
		// including multiplayer teammates, plus the old room and merchant visuals).
		merchant.Reparent(room, false);
		room.MoveChild(merchant, background.GetIndex() + 1);
		merchant.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		merchant.AnchorLeft = 0.38f;
		merchant.AnchorTop = 0.28f;
		merchant.AnchorRight = 0.62f;
		merchant.AnchorBottom = 0.65f;
		merchant.Scale = Vector2.One;
		merchant.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		foreach (Node child in merchant.GetChildren())
			if (child is CanvasItem visual) visual.Visible = false;
		scenery.Visible = false;

		// Pointer and controller focus share the same subtle light cue. No overlay
		// intercepts input; Inventory and ProceedButton keep their original order.
		Vfx.VelvetShopOutline.Attach(background, merchant, material);
		Vfx.VelvetShopLife.Attach(background, material);
		GD.Print($"{ModEntry.Tag} velvet shop installed: native merchant interaction, all room player visuals hidden");
	}
}
