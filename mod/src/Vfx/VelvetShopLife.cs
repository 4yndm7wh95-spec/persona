using System;
using Godot;

namespace P3Makoto.Vfx;

internal static class VelvetShopLife
{
	// Room pixel space of velvet_room.png. Each car is centred in one side opening
	// (x 0-584 / 1087-1670, y 0-600) with ~60px of still wall on both sides, so the
	// rise reads against the room; both sides pass together, mirrored.
	private static readonly Vector2 CarSize = new(460, 765);
	private const float CarLeftX = 62f, CarRightX = 1148f;
	// Start just below the openings, finish with the trail above the screen.
	private const float CarFromY = 600f, CarToY = -765f - 300f;
	private const double PassDur = 0.42;

	private static Texture2D? RawPng(string file)
	{
		string path = "res://p3makoto/shop/" + file;
		if (!FileAccess.FileExists(path)) return null;
		using var image = new Image();
		return image.LoadPngFromBuffer(FileAccess.GetFileAsBytes(path)) == Error.Ok ? ImageTexture.CreateFromImage(image) : null;
	}

	/// <summary>Loads the elevator textures; any missing file keeps the room static.</summary>
	private static bool AttachLayers(ShaderMaterial material)
	{
		var room = RawPng("room_lattice.png");
		var mask = RawPng("lattice_mask.png");
		var lanternLeft = RawPng("lantern_left.png");
		var lanternRight = RawPng("lantern_right.png");
		var car = RawPng("elevator_car.png");
		var glow = RawPng("elevator_car_glow.png");
		if (room == null || mask == null || lanternLeft == null || lanternRight == null || car == null || glow == null)
		{
			GD.Print($"{ModEntry.Tag} velvet elevator textures missing; painted room kept");
			return false;
		}
		// The lattice room replaces the painted one (same 1672×941 frame; Igor, table
		// and window are pixel-identical, so blink, outline and hotspot still line up).
		material.SetShaderParameter("room_tex", room);
		material.SetShaderParameter("lattice_mask_tex", mask);
		material.SetShaderParameter("lantern_left_tex", lanternLeft);
		material.SetShaderParameter("lantern_right_tex", lanternRight);
		material.SetShaderParameter("car_tex", car);
		material.SetShaderParameter("car_glow_tex", glow);
		material.SetShaderParameter("car_size", CarSize);
		material.SetShaderParameter("car_left_x", CarLeftX);
		material.SetShaderParameter("car_right_x", CarRightX);
		material.SetShaderParameter("car_on", 0f);
		material.SetShaderParameter("layered", true);
		GD.Print($"{ModEntry.Tag} velvet lattice room and elevator ready");
		return true;
	}

	public static void Attach(Control background, ShaderMaterial material)
	{
		var half = ResourceLoader.Load<Texture2D>("res://p3makoto/shop/blink_half.png");
		var closed = ResourceLoader.Load<Texture2D>("res://p3makoto/shop/blink_closed.png");
		if (half == null || closed == null) return;
		material.SetShaderParameter("blink_half", half);
		material.SetShaderParameter("blink_closed", closed);
		bool layered = AttachLayers(material);
		// Each chain has its own pivot bone. The material samples the painted
		// lantern through these rotations, keeping the surrounding room stationary.
		var skeleton = new Skeleton2D { Name = "LanternSkeleton" };
		background.AddChild(skeleton);
		var left = new Bone2D { Name = "LeftChain", Position = new Vector2(192, 75) };
		var right = new Bone2D { Name = "RightChain", Position = new Vector2(1474, 75) };
		left.SetAutocalculateLengthAndAngle(false);
		left.SetLength(260);
		left.Rest = left.Transform;
		right.SetAutocalculateLengthAndAngle(false);
		right.SetLength(260);
		right.Rest = right.Transform;
		skeleton.AddChild(left);
		skeleton.AddChild(right);
		var tree = background.GetTree();
		double time = 0, nextBlink = GD.RandRange(2.8, 5.5);
		double nextCar = GD.RandRange(3.0, 5.0), carStart = -1;
		CanvasItem? shopPage = null;
		bool ShopPageOpen()
		{
			if (shopPage == null || !GodotObject.IsInstanceValid(shopPage))
				shopPage = background.GetParent()?.FindChild("P3ShopWater", true, false) as CanvasItem;
			return shopPage != null && shopPage.IsVisibleInTree();
		}
		void Animate()
		{
			time += background.GetProcessDeltaTime();
			float t = (float)time;
			left.Rotation = Mathf.Sin(t * 0.78f) * 0.022f;
			right.Rotation = Mathf.Sin(t * 0.67f + 1.7f) * 0.019f;
			material.SetShaderParameter("left_sway", left.Rotation);
			material.SetShaderParameter("right_sway", right.Rotation);
			float blink = 0;
			double elapsed = time - nextBlink;
			if (elapsed >= 0 && elapsed < 0.055) blink = (float)(elapsed / 0.055);
			else if (elapsed >= 0.055 && elapsed < 0.10) blink = 1;
			else if (elapsed >= 0.10 && elapsed < 0.18) blink = 1 - (float)((elapsed - 0.10) / 0.08);
			else if (elapsed >= 0.18) nextBlink = time + GD.RandRange(3.4, 7.2);
			material.SetShaderParameter("blink_amount", blink);
			if (layered) AnimateCar();
		}
		void AnimateCar()
		{
			if (carStart < 0)
			{
				if (time < nextCar) return;
				// Hidden behind the goods page: wait instead of passing (and whooshing) unseen.
				if (ShopPageOpen()) { nextCar = time + GD.RandRange(2.0, 4.0); return; }
				carStart = time;
				Audio.Sfx.Clip("swish_large_1", 0.32f, pitch: 0.55f);
			}
			float p = Mathf.Clamp((float)((time - carStart) / PassDur), 0f, 1f);
			// Gentle acceleration through the opening, then full speed off the top.
			float eased = p * p * (1.6f - 0.6f * p);
			float y = Mathf.Lerp(CarFromY, CarToY, eased);
			float speed = (CarToY - CarFromY) * (3.2f * p - 1.8f * p * p) / (float)PassDur; // d(y)/dt, px/s (negative)
			material.SetShaderParameter("car_y", y);
			// About 1/22 s of shutter: long streak at peak speed, short while entering.
			material.SetShaderParameter("car_blur", Mathf.Min(Mathf.Abs(speed) / 22f, 300f));
			material.SetShaderParameter("car_on", 1f);
			if (p >= 1f)
			{
				material.SetShaderParameter("car_on", 0f);
				carStart = -1;
				nextCar = time + GD.RandRange(8.0, 15.0);
			}
		}
		tree.ProcessFrame += Animate;
		background.TreeExiting += () => tree.ProcessFrame -= Animate;
	}
}
