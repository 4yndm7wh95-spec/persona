using Godot;

namespace P3Makoto.Vfx;

internal static class VelvetShopOutline
{
    private static readonly Vector2[][] Paths =
	[
		[new(700,532), new(705,513), new(715,491), new(722,469), new(725,451),
		 new(720,438), new(725,425), new(726,398), new(747,390), new(738,382),
		 new(744,373), new(732,376), new(741,365), new(735,366), new(750,354),
		 new(744,352), new(761,343), new(747,339), new(745,332), new(756,336),
		 new(770,336), new(784,331), new(771,325), new(771,316), new(778,322),
		 new(793,322), new(806,316), new(789,313), new(782,309), new(796,311),
		 new(809,306), new(817,305), new(828,304), new(836,300), new(844,301),
		 new(855,307), new(863,310), new(878,312), new(888,313), new(884,318),
		 new(875,319), new(889,325), new(902,324), new(911,320), new(908,327),
		 new(898,334), new(913,335), new(921,330), new(918,338), new(909,342), new(923,345)],
		[new(923,345), new(916,350), new(935,355), new(923,358), new(934,366),
		 new(921,366), new(926,377), new(916,375), new(916,387), new(943,397),
		 new(946,416), new(943,435), new(950,446), new(950,466), new(961,493),
		 new(973,519), new(973,532)]
	];

	public static void Attach(Control background, Control merchant, ShaderMaterial material)
    {
        material.SetShaderParameter("hover_glow", 0f);
        var contour = new Node2D { Name = "IgorHoverContour", Modulate = Colors.Transparent };
        background.AddChild(contour);
        var lines = new Line2D[Paths.Length];
        for (int i = 0; i < Paths.Length; i++)
        {
            lines[i] = new Line2D
            {
                DefaultColor = Colors.White, Antialiased = true,
                JointMode = Line2D.LineJointMode.Round,
                BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round
            };
            contour.AddChild(lines[i]);
        }
        void Resize()
        {
            Vector2 scale = background.Size / new Vector2(1672, 941);
            for (int i = 0; i < Paths.Length; i++)
            {
                var points = new Vector2[Paths[i].Length];
                for (int j = 0; j < points.Length; j++) points[j] = Paths[i][j] * scale;
                lines[i].Points = points;
                lines[i].Width = 1.6f * Mathf.Min(scale.X, scale.Y);
            }
        }
        Resize();
        background.Resized += Resize;
        bool pointer = false, focus = false;
        Tween? tween = null;
        void Refresh()
        {
            tween?.Kill();
            tween = background.CreateTween();
            tween.TweenProperty(contour, "modulate:a", pointer || focus ? .7f : 0f, .2)
                .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        merchant.MouseEntered += () => { pointer = true; Refresh(); };
        merchant.MouseExited += () => { pointer = false; Refresh(); };
        merchant.FocusEntered += () => { focus = true; Refresh(); };
        merchant.FocusExited += () => { focus = false; Refresh(); };
        merchant.Connect("Released", Callable.From<Control>(_ => { pointer = focus = false; Refresh(); }));
    }
}
