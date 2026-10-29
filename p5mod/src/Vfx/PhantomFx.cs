using System;
using Godot;

namespace P5Joker;

/// <summary>Joker's summon lighting and fire, rendered at runtime from shaders and original P5R sprites.</summary>
public static class PhantomFx
{

	/// <summary>One backdrop per layer; repeated casts replace it instead of multiplying darkness.</summary>
	public static void Focus(Control back, float duration)
	{
		foreach (Node child in back.GetChildren())
			if (child is CanvasItem old && child.Name == "P5FocusDim")
			{
				old.Visible = false;
				old.QueueFree();
			}
		Node2D root = Fx.Root(back, Vector2.Zero, "P5FocusDim", duration + 0.08);
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		// Behind the actors and all existing VFX; no input interception or global time changes.
		back.MoveChild(root, 0);
		ColorRect dim = new() { Size = Fx.Screen(back), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(dim);
		Fx.Animate(root, 0, duration, t =>
		{
			float s = t * duration;
			float fadeIn = Fx.OutCubic(Mathf.Clamp(s / 0.065f, 0f, 1f));
			float fadeOut = 1f - Fx.OutCubic(Mathf.Clamp((s - duration + 0.26f) / 0.26f, 0f, 1f));
			dim.Color = new Color(0.012f, 0.007f, 0.02f, 0.64f * fadeIn * fadeOut);
		});
	}

	/// <summary>Release moment, also for direct casts with no targeting/ReadyFx instance.</summary>
	public static void Summon(Control front, Control back, Vector2 face, Vector2 feet, float facing = 1f)
	{
		float height = Mathf.Clamp(feet.DistanceTo(face), 140f, 560f);
		float k = height / 330f;
		Vector2 center = face.Lerp(feet, 0.36f);
		Node2D root = Fx.Root(back, center, "P5SummonFire", 1.05);
		root.Scale = new Vector2(k * facing, k);
		foreach (Node child in back.GetChildren())
			if (child.Name.ToString().StartsWith("P5CutIn", StringComparison.Ordinal))
			{
				back.MoveChild(root, child.GetIndex());
				break;
			}
		// Flame shape and detail are evaluated on the GPU; no authored polygon cutouts.
		for (int i = 0; i < 3; i++)
		{
			ColorRect flame = Flame.Rect(root, new Vector2((i - 1) * 40, 190), i == 1 ? 170 : 115, i == 1 ? 500 : 360);
			Flame.Palette(flame, PersonaMask.Dark, PersonaMask.Mid, PersonaMask.Hi, PersonaMask.Hot);
			Flame.Set(flame, "seed", 3.7f + i * 5.1f);
			Fx.Animate(flame, i * 0.025, 0.82, t =>
			{
				float open = Ease5.OutExpo(Mathf.Clamp(t / 0.15f, 0, 1));
				Flame.Set(flame, "alpha", open * (1f - Fx.OutCubic(Mathf.Clamp((t - 0.38f) / 0.62f, 0, 1))));
				flame.Scale = new Vector2(0.6f + 0.4f * open, 0.2f + 0.8f * open);
			});
		}
		Glow.Flash(front, face, PersonaMask.Hi, height * 0.75f, 0.2f);
		// Glow's flash/refract helpers are the existing P3Makoto toolchain port.
		Glow.Ripple(back, face, height * 1.05f, 0.34f, 0.010f, 1f, 0.035f);
		TextureBurst(front, back, face, center, k, facing);
		Fx.Shake(2);
	}
	// Original P5R flame, smoke and glint textures; sprites are animated in-engine.
	private static void TextureBurst(Control front, Control back, Vector2 face, Vector2 center, float k, float facing)
	{
		Node2D root = Fx.Root(front, face, "P5SummonEmbers", 1.0);
		Texture2D? ember = Fx.Tex("blue_flame"), glint = Fx.Tex("star4");
		for (int i = 0; i < 20; i++)
		{
			bool star = i % 5 == 0;
			Texture2D? tex = star ? glint : ember;
			if (tex == null) continue;
			int n = i;
			float angle = -Mathf.Pi + (i * 2.39996f) % Mathf.Tau;
			Vector2 dir = new(Mathf.Cos(angle) * facing, Mathf.Sin(angle) * 0.65f - 0.45f);
			float reach = (65f + i % 5 * 32f) * k;
			Sprite2D sprite = Fx.Sprite(root, tex);
			float size = (star ? 23f : 13f + i % 3 * 5f) * k / tex.GetWidth();
			sprite.SelfModulate = star ? PersonaMask.Hot : PersonaMask.Hi;
			Fx.Animate(sprite, i % 3 * 0.012, 0.72, t =>
			{
				sprite.Position = dir * reach * Fx.OutCubic(t) + new Vector2(0, -55f * k * t * t);
				sprite.Rotation = star ? t * 1.2f : dir.Angle() + Mathf.Pi / 2f + Mathf.Sin(t * 4f + n) * 0.25f;
				sprite.Scale = Vector2.One * size * (0.8f + 0.4f * Mathf.Sin(t * Mathf.Pi)) * (1f - Fx.InCubic(t));
				sprite.Modulate = new Color(1, 1, 1, 1f - Fx.InCubic(t));
			});
		}
		if (Fx.Tex("smoke") is Texture2D smoke)
		{
			Node2D vapor = Fx.Root(back, center, "P5SummonVapor", 0.95);
			back.MoveChild(vapor, 1);
			for (int i = 0; i < 3; i++)
			{
				int n = i;
				Sprite2D sprite = Fx.Sprite(vapor, smoke, additive: false);
				sprite.SelfModulate = new Color("14213e");
				Fx.Animate(sprite, i * 0.035, 0.78, t =>
				{
					sprite.Position = new Vector2((n - 1) * 65f * k * Fx.OutCubic(t), -70f * k * t);
					sprite.Scale = Vector2.One * (125f + 95f * Fx.OutCubic(t)) * k / smoke.GetWidth();
					sprite.Modulate = new Color(1, 1, 1, 0.35f * Mathf.Sin(t * Mathf.Pi));
				});
			}
		}
	}

}
