using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P3Makoto.Patches;

/// <summary>Only the local Makoto run's merchant room receives the Velvet Room presentation.</summary>
[HarmonyPatch]
public static class VelvetShopPatch
{
	// 0.113's base _Ready runs before NStandardMerchantRoom initializes MerchantButton.
	public static MethodBase TargetMethod() => AccessTools.DeclaredMethod(
		AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.Rooms.NStandardMerchantRoom") ?? typeof(NMerchantRoom), "_Ready");

	public static void Postfix(NMerchantRoom __instance) =>
		ModEntry.Safe("velvet shop", () => Install(__instance));

	/// <summary>Velvet Rooms currently in the tree (native merchant sounds are muted while > 0).</summary>
	internal static int ActiveRooms;

	private static void Install(NMerchantRoom room)
	{
		if (!Stage.MakotoRun || room.HasNode("P3VelvetShop")) return;
		ulong installStart = Time.GetTicksUsec();
		// Load before hiding anything: missing artwork leaves the native room usable.
		// Kept for the session (0.15.30): reloading them on every visit was part of the entry stall.
		var texture = Vfx.ShopAssets.Keep<Texture2D>("res://p3makoto/shop/velvet_room.png");
		var shader = Vfx.ShopAssets.Keep<Shader>("res://p3makoto/shaders/velvet_shop.gdshader");
		if (texture == null || shader == null) return;
		var scenery = room.GetNode<CanvasItem>("SceneContainer");
		var merchant = room.MerchantButton ?? throw new InvalidOperationException("MerchantButton is not initialized; Velvet Shop was not installed");
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
		ActiveRooms++;
		background.TreeExiting += () => ActiveRooms = Math.Max(0, ActiveRooms - 1);
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
		// Deferred: during _Ready the scene tree may still be adding this branch,
		// and a player added to a busy parent never enters the tree (silent).
		Callable.From(()=>ModEntry.Safe("velvet room bgm",()=>
		{
			if(!GodotObject.IsInstanceValid(room) || !room.IsInsideTree()) return;
			Audio.VelvetShopMusic.Attach(room);
			Audio.VelvetShopMusic.AttachHum(room);
			// "Welcome to the Velvet Room", once the room is on screen.
			var welcome = room.CreateTween();
			welcome.TweenInterval(0.6);
			welcome.TweenCallback(Callable.From(() =>
				Audio.Sfx.Clip($"igor_welcome_{GD.RandRange(1, 3)}", Audio.Sfx.IgorVoice)));
		})).CallDeferred();
		// Build the goods-page stage only after the room has faded in (0.15.30): doing it in the
		// frame after entry stacked its model/texture/shader work onto the room transition.
		// Opening the goods page earlier still builds it on demand.
		room.GetTree().CreateTimer(0.7, processAlways: false).Timeout += () => ModEntry.Safe("shop preload", () =>
		{
			if(!GodotObject.IsInstanceValid(room) || !room.IsInsideTree()) return;
			var inventory=room.GetNodeOrNull<MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantInventory>("Inventory");
			if(inventory==null)
				foreach(var child in room.GetChildren())
					if(child is MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantInventory found) {inventory=found;break;}
			if(inventory!=null) ShopInventorySkin.GetOrCreateLayer(inventory)?.Preload();
		});
		GD.Print($"{ModEntry.Tag} perf velvet shop install {(Time.GetTicksUsec() - installStart) / 1000f:0.0}ms");
		GD.Print($"{ModEntry.Tag} velvet shop installed: native merchant interaction, all room player visuals hidden");
	}
}
