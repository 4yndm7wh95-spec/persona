using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
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
	private static readonly Vector2 MoonPos = new(720f, 150f);      // 立绘贴右边后，月亮挪到中上方的空天上（水面倒影跟着走）

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
		sky.SetShaderParameter("moon", new Vector2((MoonPos.X + pad) / full.X, (MoonPos.Y + pad) / full.Y));
		sky.SetShaderParameter("horizon", (HorizonY + pad) / full.Y);
		root.AddChild(new ColorRect { Position = new Vector2(-pad, -pad), Size = full, Material = sky, MouseFilter = Control.MouseFilterEnum.Ignore });

		Band(root);
		Motes(root);
		Portrait(root);
		Butterflies(root);
	}

	// 特写立绘（阶段 11：手持碎镜片，art/v11）：会动的 Spine 立绘（tools/ui/build_portrait.py），静态图只给残影 / 光晕 / 兜底用。
	// 两者同一画布 1024×1536：静态图以中心为原点，Spine 以底边中点为原点。
	private const string PortraitPath = "res://p3makoto/ui/charselect_portrait.png";
	private const string PortraitSpine = "res://p3makoto/portrait/portrait_skel_data.tres";
	private static readonly Vector2 PortraitPos = new(1560f, 575f);          // 画布右边出屏：身体贴着右墙，不在半空截断
	private const float PortraitScale = 0.75f;
	private static readonly Vector2 PortraitHead = new(620f - 512f, 320f - 768f);       // 镜片中心：切入横幅从这里碎开
	private static readonly Vector2 CutTop = new(20f - 512f, -768f), CutBottom = new(330f - 512f, 768f);   // 左侧斜切线（同 build_portrait.py 的 CUT）
	private static readonly Vector2 PortraitEye = new(425f - 512f, 645f - 768f);        // 露出的那只眼睛

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
	/// 立绘本身是 Spine 动画（呼吸、发梢飘、手和镜片浮动、眨眼、轮廓光、眼中蓝光、镜面扫光），身后一层同形的青色光晕缓慢明暗。
	/// </summary>
	private static void Portrait(Control root)
	{
		if (ResourceLoader.Load(PortraitPath) is not Texture2D tex)
		{
			return;
		}
		Node2D group = new() { Name = "Portrait" };                 // 立绘（含残影、光晕）一组，切入横幅和碎片都插在它下面
		root.AddChild(group);
		// 沿左侧切线方向从下往上升入：切口始终落在同一条斜线上（斜条压在那里），入场过程中也不会露出画布边缘
		Vector2 cutDir = (CutBottom - CutTop).Normalized();
		Vector2 from = PortraitPos + cutDir * 520f;
		Node2D Holder(Color modulate, bool additive, float extraScale, bool live)
		{
			Node2D h = new() { Position = from, Scale = Vector2.One * PortraitScale * extraScale, Modulate = modulate };
			group.AddChild(h);
			Node2D? spine = live && ResourceLoader.Load(PortraitSpine) is Resource data ? ClassDB.Instantiate("SpineSprite").AsGodotObject() as Node2D : null;
			if (spine != null)
			{
				spine.Position = new Vector2(0f, 768f);
				h.AddChild(spine);
				spine.Set("skeleton_data_res", ResourceLoader.Load(PortraitSpine));
				PlayIdle(spine, 30);
			}
			else
			{
				h.AddChild(new Sprite2D { Texture = tex, Material = additive ? Fx.Additive : null });
			}
			return h;
		}
		Node2D ghost2 = Holder(new Color(Pal.Cyan, 0.0f), true, 1.0f, false);
		Node2D ghost1 = Holder(new Color(Pal.Cyan, 0.0f), true, 1.0f, false);
		Node2D glow = Holder(new Color(Pal.Cyan, 0.0f), true, 1.012f, false);
		Node2D main = Holder(new Color(1f, 1f, 1f, 0f), false, 1.0f, true);
		Slash(group);

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
			glow.Modulate = new Color(Pal.Cyan, 0.22f * u);
		});
		Fx.After(main, 0.45, "portrait landed", () => Selected(root, group));
		Fx.FreeAfter(ghost1, 0.7);
		Fx.FreeAfter(ghost2, 0.7);

		// 身后光晕呼吸（立绘自己的微动在 Spine 动画里）
		Fx.After(main, 0.6, "portrait idle", () =>
		{
			Tween breathe = glow.CreateTween().SetLoops().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			breathe.TweenProperty(glow, "modulate:a", 0.12f, 3.0);
			breathe.TweenProperty(glow, "modulate:a", 0.24f, 3.0);
		});
	}

	/// <summary>
	/// 立绘左侧切口上的 P3R 式斜条组（不跟立绘绑定，单独入场）：
	/// 深蓝实心斜板（压住切口）+ 贴切口的亮青细线 + 宝蓝细条 + 外侧白条 + 更外侧的半透明宽带 + 沿斜板滚动的竖排小字 + 红色标签块。
	/// 各条沿斜线方向分别从上方 / 下方错开切入（OutExpo），随后小字缓慢滚动。
	/// </summary>
	private static void Slash(Node2D parent)
	{
		Node2D layer = new() { Name = "Slash" };
		parent.AddChild(layer);
		// 切线在屏幕坐标里（立绘最终位置）
		Vector2 top = PortraitPos + CutTop * PortraitScale, bottom = PortraitPos + CutBottom * PortraitScale;
		Vector2 dir = (bottom - top).Normalized();
		Vector2 left = new(-dir.Y, dir.X);                          // 法向，指向切口外侧（左）
		Vector2 a = top - dir * 700f, b = bottom + dir * 500f;
		float travel = 1500f;

		Node2D Strip(float from, float to, Color color, bool additive, int enterFrom, float delay)
		{
			Node2D n = new() { Position = dir * travel * enterFrom };
			layer.AddChild(n);
			Fx.Poly(n, new[] { a + left * from, b + left * from, b + left * to, a + left * to }, color, additive);
			Fx.Animate(n, delay + 0.4f, t =>
			{
				float u = Ease.OutExpo(Mathf.Clamp((t * (delay + 0.4f) - delay) / 0.4f, 0f, 1f));
				n.Position = dir * travel * enterFrom * (1f - u);
			});
			return n;
		}
		// 斜板往立绘里多压 ~9px：头、手臂的微动（最多约 7px）不会在切口露出缝
		Strip(232f, 300f, new Color(Pal.Royal, 0.22f), false, +1, 0.12f);         // 外侧半透明宽带（从下）
		Strip(-9f, 92f, new Color(0.02f, 0.07f, 0.25f, 0.97f), false, -1, 0.0f);  // 深蓝实心斜板（从上）
		Strip(6f, 12f, new Color(Pal.Royal, 0.95f), false, +1, 0.05f);            // 宝蓝细条（从下）
		Strip(-12f, -6f, new Color(Pal.Cyan, 0.95f), true, -1, 0.04f);            // 贴切口的亮青细线（从上）
		Strip(108f, 116f, new Color(1f, 1f, 1f, 0.9f), false, -1, 0.1f);          // 白条（从上）
		Strip(134f, 137f, new Color(Pal.Cyan, 0.6f), true, +1, 0.16f);            // 青色细线（从下）
		Strip(206f, 208f, new Color(1f, 1f, 1f, 0.35f), false, -1, 0.2f);         // 最外侧发丝线（从上）

		// 沿斜板滚动的竖排小字（读法从上往下）
		Node2D tickerRoot = new() { Position = a + left * 66f, Rotation = dir.Angle(), Modulate = new Color(1f, 1f, 1f, 0f) };
		layer.AddChild(tickerRoot);
		Node2D tickerClip = new();
		tickerRoot.AddChild(tickerClip);
		const string unit = "THE FOOL  //  ARCANA 0  //  MEMENTO MORI  //  ";
		Label ticker = Typo.Text(tickerClip, string.Concat(System.Linq.Enumerable.Repeat(unit, 8)), Typo.Latin, 30, new Color(Pal.Ice, 0.55f), new Vector2(0f, -20f));
		float period = Typo.Latin.GetStringSize(unit, HorizontalAlignment.Left, -1, 30).X;   // 滚动正好一个循环单元，回绕无跳变
		Fx.After(layer, 0.3, "slash ticker", () =>
		{
			Tween fade = tickerRoot.CreateTween();
			fade.TweenProperty(tickerRoot, "modulate:a", 1f, 0.4);
			Tween scroll = ticker.CreateTween().SetLoops();
			scroll.TweenProperty(ticker, "position:x", -period, period / 26.0).From(0f);
		});

		// 红色标签块：贴在深蓝斜板外沿，弹出
		Vector2 tagAt = top + (bottom - top) * 0.72f + left * 100f;
		Node2D tag = new() { Position = tagAt, Rotation = dir.Angle() - Mathf.Pi / 2f, Scale = Vector2.Zero };
		layer.AddChild(tag);
		Fx.Poly(tag, new[] { new Vector2(-8f, -70f), new Vector2(8f, -70f), new Vector2(8f, 70f), new Vector2(-8f, 70f) }, new Color(Pal.Blood, 0.95f));
		Fx.Poly(tag, new[] { new Vector2(-8f, 76f), new Vector2(8f, 76f), new Vector2(8f, 90f), new Vector2(-8f, 90f) }, new Color(1f, 1f, 1f, 0.9f));
		Fx.After(layer, 0.35, "slash tag", () => Fx.Animate(tag, 0.3, t => tag.Scale = Vector2.One * Mathf.Lerp(1.6f, 1f, Ease.OutBack(t)) * Mathf.Clamp(t * 3f, 0f, 1f)));
	}

	/// <summary>SpineSprite 加进场景树后下一帧才有动画状态：取不到就稍后再试。</summary>
	private static void PlayIdle(Node2D spine, int triesLeft)
	{
		if (!GodotObject.IsInstanceValid(spine))
		{
			return;
		}
		MegaSprite sprite = new(spine);
		if (sprite.TryGetAnimationState() is MegaAnimationState state)
		{
			state.SetAnimation("idle_loop", loop: true);
			return;
		}
		if (triesLeft > 0)
		{
			Fx.After(spine, 0.016, "portrait spine", () => PlayIdle(spine, triesLeft - 1));
		}
	}

	/// <summary>
	/// 选中结城理的强反馈（这个背景场景每次选中都会重新创建，所以 Build 就是"刚被选中"）：
	/// 他抬手用召唤器对太阳穴开一枪（cast 动画）→ 开枪那一帧：召唤器爆发 + 玻璃碎片 + 蓝蝶群 + 枪声，
	/// 同时上方切入 P3R 色带"WILD CARD / PERSONA ▸ ORPHEUS"。之后回到待机。
	/// </summary>
	private static void Selected(Control root, Node2D portrait)
	{
		// v0.9.2：选中时也播一次召唤切入 —— 眼神特写横幅（立绘仍在最前面）→ 0.55s 扣扳机 → 横幅碎裂 → 召唤器爆发、蓝蝶、枪声、眼中蓝光
		Vector2 temple = root.GlobalPosition + PortraitPos + PortraitHead * PortraitScale;
		CutInFx.BeginScreen(root, temple, "結城 理", "ORPHEUS", _ =>
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
			Antialiased = true,
			Polygon = new[] { new Vector2(0, 0), new Vector2(1900, 0), new Vector2(1820, 150), new Vector2(-80, 150) },
			Color = new Color(Pal.Royal, 0.22f),
		});
		band.AddChild(new Polygon2D
		{
			Antialiased = true,
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
