using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 牌的特效（2026-10-29 第三版，用发光积木 Glow.cs）：主体是光，P5 的味道靠锐利的形状和干脆的节奏。
/// 每个都按"预备 → 爆发 → 余韵"编排，元素错开几十毫秒，运动 OutExpo，见 docs/p5/FX_PLAN.md。
/// </summary>
public static class CardFx
{
	// ───────────────────────── 防御：六边形光盾阵 ─────────────────────────

	/// <summary>
	/// 防御（Joker 获得格挡时）：
	/// 预备 —— 身前一圈红光往里收；
	/// 构建 —— 一面由十几块六边形光板拼成的竖直盾，从中心一块往外一圈圈"点亮"：每块先白闪、OutBack 弹出，
	///         然后变成半透明红光面 + 白热亮边；外面一圈红晕轮廓；同时折射波 + 光环；
	/// 停留 —— 一道斜的亮光从上往下扫过盾面（经过的光板一亮），边角星光，背后几颗慢慢上浮的光粒；
	/// 碎裂 —— 光板从外往里一块块碎掉，发光碎片往前飞散，中心一团闪光。
	/// chest = 胸口，facing = 敌人方向。
	/// </summary>
	public static void Guard(Control layer, Vector2 chest, float facing = 1f)
	{
		const float R = 25f;                                    // 六边形外接圆半径
		Vector2 center = chest + new Vector2(150f * facing, 30f);
		Glow.Gather(layer, center, Pal5.Bright, 300f, 0.12f);

		// 蜂窝格子：竖直的盾形（椭圆里的格子），尖顶六边形，列间距 √3R、行间距 1.5R
		var cells = new List<Vector2>();
		float w = Mathf.Sqrt(3f) * R;
		for (int row = -4; row <= 4; row++)
		{
			for (int col = -2; col <= 2; col++)
			{
				Vector2 p = new(col * w + (row % 2 != 0 ? w / 2f : 0f), row * 1.5f * R);
				if (p.X * p.X / (70f * 70f) + p.Y * p.Y / (150f * 150f) <= 1f)
				{
					cells.Add(p);
				}
			}
		}

		Node2D root = Fx.Root(layer, center, "P5Guard", 1.2);
		root.Scale = new Vector2(facing * 0.82f, 1f);           // 横向压一点：盾面略微侧对着敌人
		root.Skew = -0.12f * facing;
		// 盾后面一层红色柔光，给光板一个"发光体"的底
		Sprite2D bloom = new() { Texture = Glow.Soft, Material = Fx.Additive, Scale = Vector2.Zero, Modulate = new Color(Pal5.Bright, 0.6f) };
		root.AddChild(bloom);
		var panels = new List<(Node2D N, Polygon2D Fill, Line2D Rim, Polygon2D Flash, float Delay, Vector2 P)>();
		Vector2[] hex = Hex(R - 2.5f);
		foreach (Vector2 p in cells)
		{
			Node2D n = new() { Position = p, Scale = Vector2.Zero };
			root.AddChild(n);
			Polygon2D fill = Fx.Poly(n, hex, new Color(Pal5.Bright, 0.34f), additive: true);
			Line2D rim = new() { Points = hex, Closed = true, Width = 2.6f, DefaultColor = Pal5.Hot, Material = Fx.Additive, Antialiased = true };
			n.AddChild(rim);
			Polygon2D flash = Fx.Poly(n, hex, Pal5.Hot, additive: true);
			float delay = 0.12f + p.Length() / 150f * 0.12f + Fx.Rand(0f, 0.015f);
			panels.Add((n, fill, rim, flash, delay, p));
		}
		// 外框：一圈粗一点的红晕描边（勾出盾的轮廓）
		Line2D outline = new() { Points = Ellipse(92f, 172f, 48), Closed = true, Width = 8f, DefaultColor = new Color(Pal5.Bright, 0.55f),
			Material = Fx.Additive, Antialiased = true };
		root.AddChild(outline);
		outline.Scale = Vector2.Zero;

		const float total = 0.9f, breakAt = 0.62f;
		Fx.Animate(root, 0, total, t =>
		{
			float time = t * total;
			// 扫光位置：0.34~0.56s 一条斜线从上往下
			float sw = Ease5.Seg(time, 0.34f, 0.22f), sweepY = Mathf.Lerp(-190f, 190f, Fx.OutCubic(sw));
			foreach (var (n, fill, rim, flash, delay, p) in panels)
			{
				float u = Ease5.Seg(time, delay, 0.12f);
				float brk = Ease5.Seg(time, breakAt + (1f - p.Length() / 160f) * 0.06f, 0.05f);   // 外圈先碎
				n.Visible = u > 0f && brk < 1f;
				n.Scale = Vector2.One * Ease5.OutBack(u) * (1f - 0.3f * brk);
				flash.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(time, delay, 0.1f));
				float lit = sw > 0f && sw < 1f ? Mathf.Exp(-Mathf.Pow((p.Y + p.X * 0.6f - sweepY) / 28f, 2f)) : 0f;
				fill.Modulate = new Color(1f + lit * 2f, 1f + lit, 1f + lit, 1f + lit * 2f);
				rim.Modulate = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(time * 40f + p.Y * 0.05f) + lit);
			}
			float o = Ease5.OutExpo(Ease5.Seg(time, 0.12f, 0.2f)) * (1f - Ease5.Seg(time, breakAt, 0.08f));
			outline.Scale = Vector2.One * Mathf.Max(o, 0.01f);
			bloom.Scale = new Vector2(7f, 12f) * o;
			outline.Modulate = new Color(1f, 1f, 1f, o * (0.7f + 0.3f * Mathf.Sin(time * 30f)));
		});
		Fx.After(root, 0.12, () =>
		{
			Glow.Ripple(layer, center, 360f, 0.5f, 0.03f, 1.6f);
			Glow.Shockwave(layer, center, new Color(0.95f, 0.06f, 0.1f), 260f, 0.4f, 1.7f);
			Fx.Shake(1);
		});
		Glow.Sparks(layer, center, 10, 15f, 50f, 0.8f, new[] { Pal5.Hot, Pal5.Bright }, -50f, 0.12f, 0.3f, Vector2.Up, 60f);
		Glow.Glint(layer, center + new Vector2(55f * facing, -125f), 0.9f, 0.3f);
		Glow.Glint(layer, center + new Vector2(-45f * facing, 110f), 0.7f, 0.42f);
		Glow.Glint(layer, center + new Vector2(70f * facing, 40f), 0.6f, 0.5f);
		Fx.After(root, breakAt, () =>
		{
			Glow.Flash(layer, center, new Color(1f, 0.55f, 0.5f), 260f, 0.2f);
			Glow.Shards(layer, center, 16, 1.0f, new Vector2(facing, -0.15f).Normalized(), 1.2f);
			Glow.Sparks(layer, center, 30, 150f, 520f, 0.55f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 180f, 0.1f, 0.3f,
				new Vector2(facing, -0.1f).Normalized(), 100f);
		});
	}

	private static Vector2[] Ellipse(float rx, float ry, int n)
	{
		var pts = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			float a = Mathf.Tau * i / n;
			pts[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
		}
		return pts;
	}

	/// <summary>尖顶六边形。</summary>
	private static Vector2[] Hex(float r)
	{
		var pts = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			pts[i] = Vector2.Up.Rotated(Mathf.Tau * i / 6) * r;
		}
		return pts;
	}

	// ───────────────────────── 中和：锁定 + 预告信 ─────────────────────────

	/// <summary>
	/// 预告信（"中和"施加虚弱时，敌人身上）：
	/// 锁定 —— 四个发光的角括号从远处转着收拢到敌人身上（像准星锁定），中间一圈蓄力环；
	/// 显形 —— 锁定那一刻白闪 + 光环，一张预告信从一条竖直的光线"翻"开成形（横向 0 → 1，OutBack），
	///         先是一层白光盖着、再褪出卡面，身后一圈脉动的红晕，四角星光；
	/// 削弱 —— 卡片下方一圈暗红光波往下压，红色光粒往下坠落（力量被抽走）；
	/// 退场 —— 卡片再翻回成一条光线，化成光粒往上散。at = 敌人的 VfxSpawnPosition。
	/// </summary>
	public static void CallingCard(Control layer, Vector2 at)
	{
		Vector2 home = at + new Vector2(0f, -40f);
		Lock(layer, home);
		const float lockT = 0.2f;
		Fx.After(layer, lockT, () =>
		{
			Glow.Flash(layer, home, new Color(1f, 0.6f, 0.55f), 280f, 0.2f);
			Glow.Shockwave(layer, home, new Color(0.95f, 0.06f, 0.1f), 280f, 0.42f);
			Fx.Shake(1);
		});
		if (Fx.Tex("calling_card") is not Texture2D tex)
		{
			return;
		}
		float k = 120f / tex.GetHeight();
		Node2D root = Fx.Root(layer, home, "P5CallingCard", 1.6);
		root.Rotation = -0.08f;
		Sprite2D halo = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(Pal5.Bright, 0f) };
		root.AddChild(halo);
		Sprite2D card = Fx.Sprite(root, tex, additive: false);
		Sprite2D white = Fx.Sprite(root, tex, additive: true);
		Polygon2D slit = Glow.TaperPoly(root, Pal5.Hot);

		const float total = 1.3f;
		Fx.Animate(root, lockT, total, t =>
		{
			float time = t * total;
			float open = Ease5.OutBack(Ease5.Seg(time, 0.04f, 0.16f));
			float shut = Fx.InCubic(Ease5.Seg(time, 1.05f, 0.14f));
			float sx = Mathf.Max(0f, open * (1f - shut));
			card.Scale = new Vector2(k * sx, k);
			white.Scale = card.Scale;
			white.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(1f - Ease5.Seg(time, 0.12f, 0.2f) + shut, 0f, 1f));
			// 竖直光线：卡片从它翻开、最后又收回成它
			float line = Mathf.Clamp(1f - Ease5.Seg(time, 0.06f, 0.1f) + Ease5.Seg(time, 1.0f, 0.1f), 0f, 1f);
			slit.Polygon = Glow.TaperPts(new Vector2(0f, -110f), new Vector2(0f, 110f), Mathf.Max(0.5f, 14f * line));
			slit.Visible = line > 0.02f;
			float pulse = 0.55f + 0.15f * Mathf.Sin(time * 9f);
			halo.Scale = new Vector2(3.4f, 5.2f) * Mathf.Max(sx, 0.2f);
			halo.Modulate = new Color(Pal5.Bright, pulse * Ease5.Seg(time, 0.05f, 0.15f) * (1f - shut));
			root.GlobalPosition = home + new Vector2(0f, -6f * Mathf.Sin(time * 3f));
		});
		for (int i = 0; i < 4; i++)
		{
			Vector2 c = home + new Vector2(i % 2 == 0 ? -46f : 46f, i < 2 ? -62f : 62f);
			Glow.Glint(layer, c, 0.7f, lockT + 0.18f + 0.04f * i);
		}
		// 削弱：往下压的暗红光波 + 往下坠的光粒
		Fx.After(layer, lockT + 0.3, () =>
		{
			Glow.Shockwave(layer, at + new Vector2(0f, 70f), new Color(0.6f, 0.02f, 0.06f), 320f, 0.6f, 0.35f);
			Glow.Sparks(layer, home + new Vector2(0f, 40f), 22, 40f, 160f, 0.8f, new[] { Pal5.Bright, Pal5.Deep }, 260f, 0.12f, 0.3f,
				Vector2.Down, 50f);
		});
		Fx.After(layer, lockT + 1.18, () =>
		{
			Glow.Sparks(layer, home, 24, 40f, 200f, 0.7f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, -120f, 0.1f, 0.3f, Vector2.Up, 50f);
			Glow.Flash(layer, home, new Color(1f, 0.5f, 0.45f), 160f, 0.16f);
		});
	}

	/// <summary>锁定准星：四个发光的角括号从大到小转着收拢，中间一圈蓄力环。</summary>
	private static void Lock(Control layer, Vector2 at)
	{
		Glow.Gather(layer, at, Pal5.Bright, 300f, 0.2f);
		Node2D root = Fx.Root(layer, at, "P5Lock", 0.5);
		var brackets = new List<Line2D>();
		for (int i = 0; i < 4; i++)
		{
			Line2D l = new() { Points = new[] { new Vector2(0f, 18f), Vector2.Zero, new Vector2(18f, 0f) }, Width = 4f,
				DefaultColor = i % 2 == 0 ? Pal5.Hot : Pal5.Bright, Material = Fx.Additive, Antialiased = true,
				JointMode = Line2D.LineJointMode.Sharp };
			root.AddChild(l);
			brackets.Add(l);
		}
		Fx.Animate(root, 0, 0.45, t =>
		{
			float time = t * 0.45f;
			float c = Ease5.OutExpo(Ease5.Seg(time, 0f, 0.2f));
			float d = Mathf.Lerp(150f, 62f, c);
			root.Rotation = Mathf.Lerp(-1.2f, 0f, c);
			for (int i = 0; i < 4; i++)
			{
				// 四个角：左上 / 右上 / 右下 / 左下，括号的开口朝里
				brackets[i].Position = Vector2.Right.Rotated(Mathf.Pi * 1.25f + Mathf.Pi / 2f * i) * d;
				brackets[i].Rotation = Mathf.Pi / 2f * i;
			}
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(time, 0.24f, 0.2f));
		});
	}

	// ───────────────────────── 减益：锁定 + 下箭头 ─────────────────────────

	/// <summary>
	/// 减益（中和、扫腿、刺耳尖啸……施加虚弱 / 易伤）：P5R 的"↓"。
	/// 锁定 —— 四个发光角括号转着收拢到敌人身上；
	/// 压下 —— 锁定那一刻白闪，三层发光的红色"V"字箭头从敌人头顶一层接一层往下砸（越往下越大、越淡），
	///         脚下一圈暗红光波压扁扩散，红色光粒往下坠。at = 敌人的 VfxSpawnPosition。
	/// </summary>
	public static void Debuff(Control layer, Vector2 at)
	{
		Lock(layer, at);
		const float lockT = 0.2f;
		Fx.After(layer, lockT, () =>
		{
			Glow.Flash(layer, at, new Color(1f, 0.5f, 0.45f), 240f, 0.18f);
			Fx.Shake(1);
		});
		for (int i = 0; i < 3; i++)
		{
			float d = lockT + 0.07f * i;
			Chevron(layer, at + new Vector2(0f, -110f + 10f * i), at + new Vector2(0f, 20f + 30f * i), 70f + 18f * i, false, d);
		}
		Fx.After(layer, lockT + 0.2, () =>
		{
			Glow.Shockwave(layer, at + new Vector2(0f, 110f), new Color(0.65f, 0.02f, 0.06f), 360f, 0.55f, 0.3f);
			Glow.Sparks(layer, at, 20, 40f, 140f, 0.8f, new[] { Pal5.Bright, Pal5.Deep }, 300f, 0.12f, 0.3f, Vector2.Down, 60f);
		});
	}

	/// <summary>
	/// 一个发光的"V"字箭头（down = true 朝下的"V"、false 朝上的"Λ"……这里 up 参数为 true 时箭头朝上）：
	/// 从 from 飞到 to，过程中先弹出（OutBack）再淡掉。w = 箭头宽度。
	/// </summary>
	private static void Chevron(Control layer, Vector2 from, Vector2 to, float w, bool up, float delay)
	{
		Node2D root = Fx.Root(layer, from, "P5Chevron", delay + 0.6);
		float s = up ? -1f : 1f;                     // 朝下的 V：尖在下面
		Vector2[] shape =
		{
			new(-w / 2f, -w * 0.32f * s), new(0f, w * 0.18f * s), new(w / 2f, -w * 0.32f * s),
			new(w / 2f, -w * 0.08f * s), new(0f, w * 0.42f * s), new(-w / 2f, -w * 0.08f * s),
		};
		Polygon2D glow = Fx.Poly(root, Grow(shape, 1.35f), new Color(Pal5.Bright, 0.7f), additive: true);
		Polygon2D core = Fx.Poly(root, shape, Pal5.Hot, additive: true);
		Polygon2D red = Fx.Poly(root, Grow(shape, 0.7f), new Color(Pal5.Crimson, 0.9f), additive: true);
		root.Visible = false;
		Fx.Animate(root, delay, 0.5, t =>
		{
			root.Visible = true;
			float m = Ease5.OutExpo(Mathf.Min(1f, t * 2.2f));
			root.GlobalPosition = from.Lerp(to, m);
			root.Scale = Vector2.One * Ease5.OutBack(Mathf.Min(1f, t * 3f));
			float a = 1f - Fx.InCubic(Ease5.Seg(t, 0.4f, 0.6f));
			root.Modulate = new Color(1f, 1f, 1f, a);
			core.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.1f, 0.4f) * 0.6f);
		});
	}

	/// <summary>多边形绕中心缩放（做光晕层）。</summary>
	private static Vector2[] Grow(Vector2[] pts, float k)
	{
		Vector2 c = Vector2.Zero;
		foreach (Vector2 p in pts)
		{
			c += p;
		}
		c /= pts.Length;
		var o = new Vector2[pts.Length];
		for (int i = 0; i < pts.Length; i++)
		{
			o[i] = c + (pts[i] - c) * k;
		}
		return o;
	}

	// ───────────────────────── 增益：上箭头 + 光柱 ─────────────────────────

	/// <summary>
	/// 增益（能力牌）：P5R 的"↑"。脚下一圈红光收拢 → 扁的光环扩散 + 一道从脚下升起的红色光柱（白芯），
	/// 一串发光的"Λ"箭头从脚往头一个接一个窜上去，光粒上浮，头顶星光。feet / head = Joker 的脚底 / 头顶；
	/// 光柱画在人物后面（back 层），不盖住人。
	/// </summary>
	public static void Buff(Control layer, Control back, Vector2 feet, Vector2 head)
	{
		Glow.Gather(layer, feet, Pal5.Bright, 320f, 0.12f, 0.3f);
		Fx.After(layer, 0.12, () =>
		{
			Glow.Shockwave(layer, feet, new Color(0.95f, 0.06f, 0.1f), 420f, 0.55f, 0.28f);
			Glow.Ripple(layer, feet, 360f, 0.5f, 0.025f, 0.35f);
			Pillar(back, feet, head);
			Fx.Shake(1);
		});
		float h = feet.Y - head.Y;
		for (int i = 0; i < 4; i++)
		{
			float x = (i % 2 == 0 ? -1f : 1f) * (30f + 25f * (i / 2));
			Vector2 from = feet + new Vector2(x, -h * 0.15f), to = feet + new Vector2(x, -h * (0.75f + 0.12f * (i % 2)));
			Chevron(layer, from, to, 46f, true, 0.16f + 0.06f * i);
		}
		Glow.Sparks(layer, feet + new Vector2(0f, -20f), 26, 60f, 260f, 0.9f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, -140f, 0.1f, 0.3f,
			Vector2.Up, 25f);
		Glow.Glint(layer, head + new Vector2(30f, -10f), 0.9f, 0.4f);
	}

	/// <summary>竖直光柱：一根柔光贴图拉成细长条，0.1s 窜到头顶、停一下、从下往上收掉。</summary>
	private static void Pillar(Control layer, Vector2 feet, Vector2 head)
	{
		float h = feet.Y - head.Y + 80f;
		Node2D root = Fx.Root(layer, feet, "P5Pillar", 0.8);
		Sprite2D glow = new() { Texture = Glow.Soft, Material = Fx.Additive, Centered = true, Modulate = new Color(Pal5.Bright, 0.75f) };
		Sprite2D core = new() { Texture = Glow.Soft, Material = Fx.Additive, Centered = true, Modulate = Pal5.Hot };
		root.AddChild(glow);
		root.AddChild(core);
		Fx.Animate(root, 0, 0.7, t =>
		{
			float up = Ease5.OutExpo(Mathf.Min(1f, t * 5f)), off = Fx.InCubic(Ease5.Seg(t, 0.45f, 0.55f));
			float len = h * up * (1f - off);
			glow.Scale = new Vector2(5.5f * (1f - 0.5f * t), len / 32f * 1.2f);
			core.Scale = new Vector2(1.2f * (1f - 0.6f * t), len / 32f);
			glow.Position = core.Position = new Vector2(0f, -h * up + len / 2f);
		});
	}

	// ───────────────────────── 毒：暗红诅咒旋涡 ─────────────────────────

	/// <summary>
	/// 毒（剧毒、毒刃、弹跳药瓶……敌人身上）：暗红偏紫的诅咒旋涡。
	/// 六道暗紫红的光丝从外面旋着往中心收（像被吸进去），中心一点暗红核越来越亮 → "噗"地炸开：
	/// 暗色闪光 + 折射波 + 紫红光粒往上冒，几颗暗红"泡"慢慢上浮破掉。at = 敌人的 VfxSpawnPosition。
	/// </summary>
	public static void Curse(Control layer, Vector2 at)
	{
		Color violet = new("a01cff"), plum = new("6a0a52");
		Node2D root = Fx.Root(layer, at, "P5Curse", 0.6);
		var strands = new List<(Polygon2D G, Polygon2D C, float A0)>();
		for (int i = 0; i < 6; i++)
		{
			strands.Add((Glow.TaperPoly(root, new Color(i % 2 == 0 ? violet : Pal5.Crimson, 0.75f)), Glow.TaperPoly(root, new Color(1f, 0.75f, 0.9f)),
				Mathf.Tau * i / 6f));
		}
		Sprite2D core = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(plum, 0f) };
		root.AddChild(core);
		const float spin = 0.34f;
		Fx.Animate(root, 0, spin, t =>
		{
			foreach (var (g, c, a0) in strands)
			{
				// 一段螺旋：外端在半径 150、角度 a0；内端跟着 t 往中心收，整段边收边转
				float rOut = Mathf.Lerp(150f, 30f, Fx.InCubic(t)), rIn = Mathf.Lerp(90f, 4f, Ease5.OutQuint(t));
				float a = a0 + t * 3.2f;
				Vector2 p0 = Vector2.Right.Rotated(a) * rOut, p1 = Vector2.Right.Rotated(a + 1.1f) * rIn;
				g.Polygon = Glow.TaperPts(p0, p1, 12f * (1f - 0.5f * t));
				c.Polygon = Glow.TaperPts(p0, p1, 3f);
			}
			core.Scale = Vector2.One * (1f + 3f * t);
			core.Modulate = new Color(new Color("ff2a8a"), 0.9f * t);
		});
		Fx.After(layer, spin, () =>
		{
			Glow.Flash(layer, at, new Color(0.75f, 0.1f, 0.5f), 300f, 0.25f);
			Glow.Ripple(layer, at, 380f, 0.5f, 0.035f);
			Glow.Shockwave(layer, at, new Color(0.6f, 0.05f, 0.7f), 300f, 0.45f, 0.8f);
			Glow.Sparks(layer, at, 30, 80f, 320f, 0.9f, new[] { new Color(1f, 0.6f, 0.9f), violet, plum }, -160f, 0.12f, 0.4f, Vector2.Up, 70f);
			Fx.Shake(1);
			for (int i = 0; i < 5; i++)
			{
				Bubble(layer, at + new Vector2(Fx.Rand(-60f, 60f), Fx.Rand(-20f, 40f)), Fx.Rand(0f, 0.3f), violet);
			}
		});
	}

	/// <summary>一个发光的泡：上浮、胀大、最后"啵"地一闪消失。</summary>
	private static void Bubble(Control layer, Vector2 at, float delay, Color tint)
	{
		Node2D root = Fx.Root(layer, at, "P5Bubble", delay + 0.9);
		float r = Fx.Rand(7f, 13f);
		Line2D ring = new() { Points = Fx.Circle(r, 20), Closed = true, Width = 2.2f, DefaultColor = new Color(tint, 0.9f), Material = Fx.Additive, Antialiased = true };
		Polygon2D fill = Fx.Poly(root, Fx.Circle(r, 20), new Color(tint, 0.18f), additive: true);
		root.AddChild(ring);
		root.Visible = false;
		float rise = Fx.Rand(70f, 130f), sway = Fx.Rand(4f, 8f);
		Fx.Animate(root, delay, 0.8, t =>
		{
			root.Visible = true;
			root.GlobalPosition = at + new Vector2(8f * Mathf.Sin(t * sway), -rise * Fx.OutCubic(t));
			float pop = Ease5.Seg(t, 0.85f, 0.15f);
			root.Scale = Vector2.One * (0.4f + 0.8f * t + 0.6f * pop);
			root.Modulate = new Color(1f, 1f, 1f, (1f - pop) * Mathf.Min(1f, t * 6f));
		});
	}
}
