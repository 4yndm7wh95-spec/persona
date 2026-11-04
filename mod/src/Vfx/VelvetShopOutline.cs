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
		[new(923,345), new(916,350), new(935,355), new(923,358), new(934,366),
		 new(921,366), new(926,377), new(916,375), new(916,387), new(943,397),
		 new(946,416), new(943,435), new(950,446), new(950,466), new(961,493),
		 new(973,519), new(973,532)],
		[new(700,532), new(705,513), new(715,491), new(722,469), new(725,451),
		 new(720,438), new(725,425), new(726,398), new(747,390), new(738,382),
		 new(744,373), new(732,376), new(741,365), new(735,366), new(750,354),
		 new(744,352), new(761,343), new(747,339), new(745,332), new(756,336),
		 new(770,336), new(784,331), new(771,325), new(771,316), new(778,322),
		 new(793,322), new(806,316), new(789,313), new(782,309), new(796,311),
		 new(809,306), new(817,305), new(828,304), new(836,300), new(844,301),
		 new(855,307), new(863,310), new(878,312), new(888,313), new(884,318),
		 new(875,319), new(889,325), new(902,324), new(911,320), new(908,327),
		 new(898,334), new(913,335), new(921,330), new(918,338), new(909,342), new(923,345)]
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
