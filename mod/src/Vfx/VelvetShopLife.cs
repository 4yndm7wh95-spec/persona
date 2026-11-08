using System;
using Godot;

namespace P3Makoto.Vfx;

internal static class VelvetShopLife
{
	// Room pixel space of velvet_room.png. One wide procedural car (drawn in the
	// shader) passes behind the whole room and shows through both lattice walls,
	// leaving still wall at the outer edges so the rise reads.
	private const float CarX0 = 110f, CarX1 = 1562f, CarH = 540f;
	// Start just below the walls, finish with the trail above the screen.
	private const float CarFromY = 630f, CarToY = -CarH - 220f;
	private const double PassDur = 0.60;
	// velvet_elevator_pass.ogg peaks at 1.30 s; the car's middle crosses the walls
	// ~0.34 s into the pass, so the sound starts this long before the car.
	private const double SoundLead = 1.30 - 0.34;

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
		if (room == null || mask == null || lanternLeft == null || lanternRight == null)
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
		material.SetShaderParameter("car_x0", CarX0);
		material.SetShaderParameter("car_x1", CarX1);
		material.SetShaderParameter("car_h", CarH);
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
		double nextCar = GD.RandRange(3.5, 5.5), carStart = -1;
		bool soundPlayed = false;
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
				// The approach is heard before the car shows: it swells, passes, fades away.
				if (!soundPlayed && time >= nextCar - SoundLead)
				{
					// Hidden behind the goods page: wait instead of passing unseen.
					if (ShopPageOpen()) { nextCar = time + SoundLead + GD.RandRange(2.0, 4.0); return; }
					soundPlayed = true;
					Audio.Sfx.Clip("velvet_elevator_pass", 0.9f);
				}
				if (time < nextCar) return;
				carStart = time;
			}
			float p = Mathf.Clamp((float)((time - carStart) / PassDur), 0f, 1f);
			// Gentle acceleration through the opening, then full speed off the top.
			float eased = p * p * (1.6f - 0.6f * p);
			float y = Mathf.Lerp(CarFromY, CarToY, eased);
			float speed = (CarToY - CarFromY) * (3.2f * p - 1.8f * p * p) / (float)PassDur; // d(y)/dt, px/s (negative)
			material.SetShaderParameter("car_y", y);
			// Short shutter (~1/45 s): enough streak to feel fast, edges stay readable.
			material.SetShaderParameter("car_blur", Mathf.Min(Mathf.Abs(speed) / 45f, 90f));
			material.SetShaderParameter("car_on", 1f);
			if (p >= 1f)
			{
				material.SetShaderParameter("car_on", 0f);
				carStart = -1;
				soundPlayed = false;
				nextCar = time + GD.RandRange(8.0, 15.0);
			}
		}
		tree.ProcessFrame += Animate;
		background.TreeExiting += () => tree.ProcessFrame -= Animate;
	}
}
