using System;
using Godot;

namespace P3Makoto.Vfx;

internal static class VelvetShopLife
{
	// Room pixel space of velvet_room.png / the elevator layers.
	private const float RoomW = 1672f;
	private static readonly Vector2 CarSize = new(392, 652);
	// Left opening placement measured by the art pass; the right one mirrors it.
	private const float CarLeftX = 62f, CarRightX = RoomW - 62f - 392f;
	// The openings end at y≈600: start just below, finish with the trail above the screen.
	private const float CarFromY = 610f, CarToY = -652f - 260f;
	private const double PassDur = 0.42;

	private static Texture2D? RawPng(string file)
	{
		string path = "res://p3makoto/shop/" + file;
		if (!FileAccess.FileExists(path)) return null;
		using var image = new Image();
		return image.LoadPngFromBuffer(FileAccess.GetFileAsBytes(path)) == Error.Ok ? ImageTexture.CreateFromImage(image) : null;
	}

	/// <summary>Loads the elevator layers; any missing file keeps the single painted room.</summary>
	private static bool AttachLayers(ShaderMaterial material)
	{
		var backdrop = RawPng("elevator_backdrop.png");
		var front = RawPng("elevator_front.png");
		var lanternLeft = RawPng("lantern_left.png");
		var lanternRight = RawPng("lantern_right.png");
		var car = RawPng("elevator_car.png");
		var glow = RawPng("elevator_car_glow.png");
		if (backdrop == null || front == null || lanternLeft == null || lanternRight == null || car == null || glow == null)
		{
			GD.Print($"{ModEntry.Tag} velvet elevator layers missing; painted room kept");
			return false;
		}
		material.SetShaderParameter("backdrop_tex", backdrop);
		material.SetShaderParameter("front_tex", front);
		material.SetShaderParameter("lantern_left_tex", lanternLeft);
		material.SetShaderParameter("lantern_right_tex", lanternRight);
		material.SetShaderParameter("car_tex", car);
		material.SetShaderParameter("car_glow_tex", glow);
		material.SetShaderParameter("car_size", CarSize);
		material.SetShaderParameter("car_on", 0f);
		material.SetShaderParameter("layered", true);
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
				bool rightSide = GD.Randf() < 0.5f;
				material.SetShaderParameter("car_flip", rightSide ? 1f : 0f);
				material.SetShaderParameter("car_pos", new Vector2(rightSide ? CarRightX : CarLeftX, CarFromY));
				Audio.Sfx.Clip("swish_large_1", 0.32f, pitch: 0.55f);
			}
			float p = Mathf.Clamp((float)((time - carStart) / PassDur), 0f, 1f);
			// Gentle acceleration through the opening, then full speed off the top.
			float eased = p * p * (1.6f - 0.6f * p);
			float y = Mathf.Lerp(CarFromY, CarToY, eased);
			float speed = (CarToY - CarFromY) * (3.2f * p - 1.8f * p * p) / (float)PassDur; // d(y)/dt, px/s (negative)
			material.SetShaderParameter("car_pos", new Vector2(((Vector2)material.GetShaderParameter("car_pos")).X, y));
			// About 1/22 s of shutter: long streak at peak speed, short while entering.
			material.SetShaderParameter("car_blur", Mathf.Min(Mathf.Abs(speed) / 22f, 240f));
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
