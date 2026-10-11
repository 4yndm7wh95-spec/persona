using Godot;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// 选人界面背景（储君的 RegentBg 场景）：隐藏原版的星座王座，换成游戏内实时渲染的 P3R 风格画面 ——
/// 深蓝夜空与巨大的月亮、月光碎在水面上、斜切的 MEMENTO MORI 色带、上升的光点和蓝蝶，
/// 结城理是 P3R 结算界面式的特写立绘（从右侧带残影滑入，呼吸般轻动）。
/// 原版脚本按名字取的 7 个悬停区域保留不动（它们只切换已隐藏的原版骨骼皮肤）。
/// </summary>
public static class CharSelectBg
{
	// 场景根 Control 的尺寸（tscn：offset -960,-521 .. 1600,679），本地 (960,521) 是屏幕中心
	private const float W = 2560f, H = 1200f;
	private const float HorizonY = 830f;

	public static void Build(Control bg)
	{
		foreach (Node c in bg.GetChildren())
		{
			if (c is Node2D n2)
			{
				n2.Visible = false;                     // 原版 SpineSprite、星星粒子
			}
		}

		Control root = new() { Name = "P3RBackground", MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(W, H) };
		bg.AddChild(root);
		bg.MoveChild(root, 0);

		const float pad = 300f;
		ShaderMaterial sky = new() { Shader = Shaders.Menu };
		Vector2 full = new(W + 2 * pad, H + 2 * pad);
		sky.SetShaderParameter("aspect", full.X / full.Y);
		sky.SetShaderParameter("moon", new Vector2((1640f + pad) / full.X, (250f + pad) / full.Y));
		sky.SetShaderParameter("horizon", (HorizonY + pad) / full.Y);
		root.AddChild(new ColorRect { Position = new Vector2(-pad, -pad), Size = full, Material = sky, MouseFilter = Control.MouseFilterEnum.Ignore });

		Band(root);
		Motes(root);
		Portrait(root);
		Butterflies(root);
	}

	// 特写立绘（art/v6/portrait_a.png → tools/ui/build_ui_assets.py）：922×1400，头部约在 (480, 213)
	private const string PortraitPath = "res://p3makoto/ui/charselect_portrait.png";
	private static readonly Vector2 PortraitPos = new(1270f, 526f);
	private const float PortraitScale = 0.82f;
	private static readonly Vector2 PortraitHead = new(480f - 461f, 213f - 700f);
	private static readonly Vector2 PortraitEye = new(489f - 461f, 256f - 700f);        // 露出的那只眼睛

	/// <summary>眼睛里泛起的蓝光：加色光点迅速亮起、横向拉长成一道细光，再慢慢退去。</summary>
	private static void EyeGlow(Control layer, Vector2 at)
	{
		Node2D root = Fx.Root(layer, at, "P3EyeGlow");
		Sprite2D dot = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0f), Scale = new Vector2(1.6f, 1.0f) };
		Sprite2D streak = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Ice, 0f), Scale = new Vector2(0.2f, 0.18f) };
		root.AddChild(dot);
		root.AddChild(streak);
		Fx.Animate(root, 1.1f, t =>
		{
			float time = t * 1.1f;
			float a = time < 0.08f ? time / 0.08f : 1f - Ease.InCubic(Mathf.Clamp((time - 0.3f) / 0.8f, 0f, 1f));
			dot.Modulate = new Color(Pal.Cyan, 0.9f * a);
			streak.Modulate = new Color(Pal.Ice, a);
			streak.Scale = new Vector2(0.2f + 3.2f * Ease.OutExpo(Mathf.Clamp(time / 0.25f, 0f, 1f)), 0.16f);
		});
		Fx.FreeAfter(root, 1.2);
	}

	/// <summary>
	/// P3R 结算界面式的特写：从右侧带两道青色残影滑入（0.45s）→ 入场完成时触发选中反馈；
	/// 之后呼吸般轻微缩放与摇摆，身后一层同形的青色加色光晕缓慢明暗（边缘光）。
	/// </summary>
	private static void Portrait(Control root)
	{
		if (ResourceLoader.Load(PortraitPath) is not Texture2D tex)
		{
			return;
		}
		Node2D group = new() { Name = "Portrait" };                 // 立绘（含残影、光晕）一组，切入横幅和碎片都插在它下面
		root.AddChild(group);
		Sprite2D Make(Color modulate, bool additive, float extraScale)
		{
			Sprite2D sp = new() { Texture = tex, Position = PortraitPos + new Vector2(420f, 0f), Scale = Vector2.One * PortraitScale * extraScale, Modulate = modulate };
			if (additive)
			{
				sp.Material = Fx.Additive;
			}
			group.AddChild(sp);
			return sp;
		}
		Sprite2D ghost2 = Make(new Color(Pal.Cyan, 0.0f), true, 1.0f);
		Sprite2D ghost1 = Make(new Color(Pal.Cyan, 0.0f), true, 1.0f);
		Sprite2D glow = Make(new Color(Pal.Cyan, 0.0f), true, 1.012f);
		Sprite2D main = Make(new Color(1f, 1f, 1f, 0f), false, 1.0f);

		Vector2 from = PortraitPos + new Vector2(420f, 0f);
		Fx.Animate(main, 0.6f, t =>
		{
			float time = t * 0.6f;
			float u = Ease.OutExpo(Mathf.Clamp(time / 0.45f, 0f, 1f));
			main.Position = from.Lerp(PortraitPos, u);
			main.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.15f, 0f, 1f));
			// 残影：落后一点、更透明，入场结束时消失
			float u1 = Ease.OutExpo(Mathf.Clamp((time - 0.05f) / 0.45f, 0f, 1f));
			float u2 = Ease.OutExpo(Mathf.Clamp((time - 0.1f) / 0.45f, 0f, 1f));
			ghost1.Position = from.Lerp(PortraitPos, u1);
			ghost2.Position = from.Lerp(PortraitPos, u2);
			float g = (1f - Ease.InCubic(Mathf.Clamp((time - 0.3f) / 0.3f, 0f, 1f))) * Mathf.Clamp(time / 0.1f, 0f, 1f);
			ghost1.Modulate = new Color(Pal.Cyan, 0.45f * g);
			ghost2.Modulate = new Color(Pal.Royal, 0.35f * g);
			glow.Position = main.Position;
			glow.Modulate = new Color(Pal.Cyan, 0.3f * u);
		});
		Fx.After(main, 0.45, "portrait landed", () => Selected(root, group));
		Fx.FreeAfter(ghost1, 0.7);
		Fx.FreeAfter(ghost2, 0.7);

		// 呼吸：极轻的缩放与摇摆；光晕跟着明暗
		Fx.After(main, 0.6, "portrait idle", () =>
		{
			Tween breathe = main.CreateTween().SetLoops().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			breathe.TweenProperty(main, "scale", Vector2.One * PortraitScale * 1.006f, 2.2);
			breathe.Parallel().TweenProperty(main, "rotation", 0.004f, 2.2);
			breathe.Parallel().TweenProperty(glow, "modulate:a", 0.18f, 2.2);
			breathe.TweenProperty(main, "scale", Vector2.One * PortraitScale, 2.2);
			breathe.Parallel().TweenProperty(main, "rotation", -0.003f, 2.2);
			breathe.Parallel().TweenProperty(glow, "modulate:a", 0.32f, 2.2);
		});
	}

	/// <summary>
	/// 选中结城理的强反馈（这个背景场景每次选中都会重新创建，所以 Build 就是"刚被选中"）：
	/// 他抬手用召唤器对太阳穴开一枪（cast 动画）→ 开枪那一帧：召唤器爆发 + 玻璃碎片 + 蓝蝶群 + 枪声，
	/// 同时上方切入 P3R 色带"WILD CARD / PERSONA ▸ ORPHEUS"。之后回到待机。
	/// </summary>
	private static void Selected(Control root, Node2D portrait)
	{
		// v0.9.2：选中时也播一次召唤切入 —— 眼神特写横幅（立绘仍在最前面）→ 0.55s 扣扳机 → 横幅碎裂 → 召唤器爆发、蓝蝶、枪声、眼中蓝光
		Vector2 temple = root.GlobalPosition + PortraitPos + PortraitHead * PortraitScale + new Vector2(-30f, 0f);
		CutInFx.BeginScreen(root, temple, "结城理", "ORPHEUS", _ =>
		{
			P3Fx.EvokerBurst(root, temple, 1.25f);
			P3Fx.ButterflyBurst(root, temple, 7, 380f);
			WaterRipple(root, root.GlobalPosition + new Vector2(PortraitPos.X - 60f, HorizonY + 150f));
			Audio.Sfx.Play(Audio.Snd.Gunshot, 1.0f);
			EyeGlow(root, root.GlobalPosition + PortraitPos + PortraitEye * PortraitScale);
		}, 0.7, below: portrait);
	}

	/// <summary>
	/// 开枪那一刻脚下水面荡开的涟漪：透视压扁成椭圆贴在水面上，三圈先后扩散、越往外越淡越宽，
	/// 最后一圈完全淡出后才回收（不会在边缘被裁掉）。另加一圈细细的青色高光描出波峰。
	/// </summary>
	private static void WaterRipple(Control root, Vector2 at)
	{
		for (int i = 0; i < 3; i++)
		{
			float size = 1300f + 260f * i;
			P3Fx.Ripple(root, at, size, 1.6f + 0.25f * i, 0.018f - 0.004f * i, squashY: 0.2f, delay: 0.16f * i);
			P3Fx.Shockwave(root, at, new Color(Pal.Cyan, 0.35f - 0.09f * i), size * 0.95f, 1.5f + 0.25f * i, squashY: 0.2f, delay: 0.16f * i + 0.03f);
		}
	}

	/// <summary>P3R 标志性的斜切色带 + 超大号描边字缓慢漂移。</summary>
	private static void Band(Control root)
	{
		Node2D band = new() { Name = "Band", Position = new Vector2(-200f, 700f), Rotation = Mathf.DegToRad(-9f) };
		root.AddChild(band);
		band.AddChild(new Polygon2D
		{
			Polygon = new[] { new Vector2(0, 0), new Vector2(1900, 0), new Vector2(1820, 150), new Vector2(-80, 150) },
			Color = new Color(Pal.Royal, 0.22f),
		});
		band.AddChild(new Polygon2D
		{
			Polygon = new[] { new Vector2(0, 160), new Vector2(1900, 160), new Vector2(1897, 166), new Vector2(-3, 166) },
			Color = new Color(Pal.Cyan, 0.5f),
			Material = Fx.Additive,
		});
		Label text = Typo.Text(band, "MEMENTO MORI   MEMENTO MORI   MEMENTO MORI", Typo.Latin, 130, new Color(Pal.Ice, 0.13f), new Vector2(-300f, -6f));
		Tween drift = text.CreateTween().SetLoops();
		drift.TweenProperty(text, "position:x", 0f, 26.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		drift.TweenProperty(text, "position:x", -300f, 26.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	/// <summary>水面上缓缓上升的光点。</summary>
	private static void Motes(Control root)
	{
		Node2D at = new() { Position = new Vector2(1250f, HorizonY + 120f) };
		root.AddChild(at);
		GpuParticles2D p = Fx.Sparks(at, Pal.Cyan, 70, 8f, 30f, 7f, gravity: -6f, scaleMin: 0.12f, scaleMax: 0.4f, oneShot: false);
		p.LocalCoords = true;
		ParticleProcessMaterial pm = (ParticleProcessMaterial)p.ProcessMaterial;
		pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		pm.EmissionBoxExtents = new Vector3(1100f, 160f, 0f);
		pm.Direction = new Vector3(0f, -1f, 0f);
		pm.Spread = 25f;
		pm.DampingMin = 0f;
		pm.DampingMax = 0f;
		p.Preprocess = 6.0;
	}

	/// <summary>蓝蝶不时从水面飞起。</summary>
	private static void Butterflies(Control root)
	{
		Timer timer = new() { WaitTime = 2.4, Autostart = true };
		root.AddChild(timer);
		timer.Timeout += () => ModEntry.Safe("char select butterfly", () =>
		{
			if (!root.IsVisibleInTree())
			{
				return;
			}
			Vector2 origin = root.GlobalPosition;
			Vector2 from = origin + new Vector2(Fx.Rand(800f, 1600f), HorizonY + Fx.Rand(40f, 160f));
			Vector2 to = origin + new Vector2(Fx.Rand(900f, 1700f), Fx.Rand(120f, 520f));
			P3Fx.Butterfly(root, from, to, Fx.Rand(3.5f, 5f), Fx.Rand(0.6f, 1f));
		});
	}
}
