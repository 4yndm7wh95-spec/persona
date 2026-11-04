using System;
using Godot;

namespace P3Makoto.Vfx;

internal static class VelvetShopLife
{
	public static void Attach(Control background, ShaderMaterial material)
	{
		var half = ResourceLoader.Load<Texture2D>("res://p3makoto/shop/blink_half.png");
		var closed = ResourceLoader.Load<Texture2D>("res://p3makoto/shop/blink_closed.png");
		if (half == null || closed == null) return;
		material.SetShaderParameter("blink_half", half);
		material.SetShaderParameter("blink_closed", closed);
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
		}
		tree.ProcessFrame += Animate;
		background.TreeExiting += () => tree.ProcessFrame -= Animate;
	}
}
