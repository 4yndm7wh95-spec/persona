using System;
using System.Collections.Generic;
using Godot;

namespace P3Makoto.Vfx;

internal static class VelvetShopOutline
{
	// Visible silhouette only. The gap between these paths is hidden by the table;
	// never close it with a horizontal line across the tabletop.
	private static readonly Vector2[][] Paths =
	[
		[new(926,356), new(919,360), new(935,371), new(925,371), new(939,381),
		 new(922,379), new(923,390), new(943,397), new(946,416), new(943,435),
		 new(950,446), new(950,466), new(961,493), new(973,519), new(973,532)],
		[new(700,532), new(705,513), new(715,491), new(722,469), new(725,451),
		 new(720,438), new(725,425), new(726,398), new(747,390), new(739,383),
		 new(754,378), new(737,375), new(749,365), new(741,364), new(761,357),
		 new(752,354), new(769,350), new(760,345), new(758,338), new(765,343),
		 new(780,345), new(775,338), new(780,330), new(781,337), new(792,340),
		 new(807,333), new(822,329), new(836,327), new(849,329), new(863,334),
		 new(876,341), new(888,337), new(891,330), new(894,337), new(889,344),
		 new(904,339), new(904,332), new(909,339), new(903,347), new(914,347),
		 new(919,342), new(915,351), new(926,356)]
	];

	public static void Attach(Control background, Control merchant, ShaderMaterial material)
	{
		var lines = new Line2D[Paths.Length];
		for (int i = 0; i < lines.Length; i++)
		{
			lines[i] = new Line2D
			{
				Name = "IgorOutline" + i, DefaultColor = Colors.White,
				Antialiased = true, JointMode = Line2D.LineJointMode.Round,
				BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round
			};
			background.AddChild(lines[i]);
		}
		float progress = 0f;
		bool pointer = false, focus = false, selected = false;
		Tween? tween = null;
		void Draw(float amount)
		{
			progress = amount;
			float total = 0;
			foreach (var path in Paths)
				for (int i = 1; i < path.Length; i++) total += path[i - 1].DistanceTo(path[i]);
			float remaining = total * amount;
			Vector2 scale = background.Size / new Vector2(1672, 941);
			for (int p = 0; p < Paths.Length; p++)
			{
				var points = new List<Vector2>();
				var path = Paths[p];
				if (remaining > 0) points.Add(path[0] * scale);
				for (int i = 1; i < path.Length && remaining > 0; i++)
				{
					float length = path[i - 1].DistanceTo(path[i]);
					points.Add(path[i - 1].Lerp(path[i], Mathf.Min(remaining / length, 1f)) * scale);
					remaining -= length;
				}
				lines[p].Width = 4.5f * Mathf.Min(scale.X, scale.Y);
				lines[p].Points = points.ToArray();
			}
		}
		void Select(bool value)
		{
			if (selected == value) return;
			selected = value;
			tween?.Kill();
			material.SetShaderParameter("hover_glow", value ? 1f : 0f);
			Draw(0);
			if (value)
			{
				tween = background.CreateTween();
				tween.TweenMethod(Callable.From<float>(Draw), 0f, 1f, 0.32);
			}
		}
		merchant.MouseEntered += () => { pointer = true; Select(pointer || focus); };
		merchant.MouseExited += () => { pointer = false; Select(pointer || focus); };
		merchant.FocusEntered += () => { focus = true; Select(pointer || focus); };
		merchant.FocusExited += () => { focus = false; Select(pointer || focus); };
		merchant.Connect("Released", Callable.From<Control>(_ => { pointer = focus = false; Select(false); }));
		background.Resized += () => Draw(progress);
	}
}
