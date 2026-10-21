using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P3Makoto.Vfx;

/// <summary>
/// v0.9 "大胆版"特效库（第二部分：物理命中 + P3R 主题元素）。每个效果都自带"确认感"（闪光 / 冲击 / 震屏 / 大字）。
/// </summary>
public static class Motifs
{
	// ═════════ 物理 ═════════

	/// <summary>巨型新月斩痕（颜色可选），停留后碎成玻璃。</summary>
	public static void Cleave(Control layer, Vector2 at, Color tint, float s = 1f, float angle = -0.55f, float facing = 1f)
	{
		ArcSlash(layer, at, tint, 620f * s, 50f * s, 40f * s, angle, s, facing: facing);
		Fx.After(layer, 0.2, "cleave shatter", () => P3Fx.GlassShatter(layer, at, (int)(18 * s), 0.9f * s, tint));
		Bold.Impact(layer, at, tint, 0.8f * s, ShakeStrength.Medium);
	}

	/// <summary>
	/// 弧形斩痕的完整出入动画（普通攻击 / 塔纳托斯共用）：
	/// 入 —— 刀光从弧线一端"划"到另一端（0.07s，OutExpo），最前端带一颗白色星光头；
	/// 满 —— 整条弧闪一下白（0.05s），并略微向外膨胀；
	/// 出 —— 尾端追着头端收拢（0.2s），弧线同时变细、沿切线方向继续滑出一点，最后在头端碎成光点。
	/// darkCore：塔纳托斯用的黑色刀身层。
	/// </summary>
	public static void ArcSlash(Control layer, Vector2 at, Color tint, float length, float bow, float thick, float angle, float s = 1f,
		bool darkCore = false, float facing = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3ArcSlash");
		root.Rotation = angle * facing;
		root.Scale = new Vector2(facing, 1f);
		const int n = 32;
		const float tIn = 0.07f, tFlash = 0.05f, tHold = 0.06f, tOut = 0.2f;
		const float total = tIn + tFlash + tHold + tOut;
		Polygon2D glow = Fx.Poly(root, new Vector2[3], new Color(tint, 0.55f), additive: true);
		Polygon2D band = darkCore
			? Fx.Poly(root, new Vector2[3], new Color(0.02f, 0.03f, 0.06f, 0.92f))
			: Fx.Poly(root, new Vector2[3], new Color(tint.Lightened(0.4f), 0.95f), additive: true);
		Polygon2D core = Fx.Poly(root, new Vector2[3], Pal.White, additive: true);
		Node2D head = new();
		root.AddChild(head);
		Fx.Poly(head, Fx.Star4(46f * s, 0.12f), Pal.White, additive: true);
		head.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(tint, 0.9f), Scale = Vector2.One * 2.2f * s });

		Vector2 Point(float u, float off) =>
			new((u - 0.5f) * length, -Mathf.Sin(u * Mathf.Pi) * bow + off);

		// 只画 [a, b] 这一段；两端各自收尖，整体厚度乘 k
		Vector2[] Crescent(float a, float b, float th, float k)
		{
			var pts = new Vector2[n * 2];
			for (int i = 0; i < n; i++)
			{
				float u = Mathf.Lerp(a, b, i / (float)(n - 1));
				float local = (u - a) / Mathf.Max(b - a, 1e-4f);
				float w = Mathf.Sin(u * Mathf.Pi) * Mathf.Pow(Mathf.Sin(local * Mathf.Pi * 0.5f + 0.0001f), 0.6f) * th * k;
				// 头端（b）更粗更亮，尾端拖细
				w *= 0.35f + 0.65f * local;
				pts[i] = Point(u, -w);
				pts[n * 2 - 1 - i] = Point(u, w * 0.3f);
			}
			return pts;
		}

		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float inU = Ease.OutExpo(Mathf.Clamp(time / tIn, 0f, 1f));
			float flash = Mathf.Clamp((time - tIn) / tFlash, 0f, 1f);
			float outU = Ease.InOutCubic(Mathf.Clamp((time - tIn - tFlash - tHold) / tOut, 0f, 1f));
			float b = Mathf.Lerp(0f, 1f, inU) + 0.06f * outU;              // 出场时头端继续往前滑一点
			float a = Mathf.Lerp(0f, Mathf.Min(b, 1.04f) - 0.02f, outU);
			float swell = 1f + 0.18f * Mathf.Sin(flash * Mathf.Pi) + 0.1f * outU;
			float thin = 1f - 0.55f * outU;
			Fx.Reshape(glow, Crescent(a, b, thick * 1.0f, swell * thin));
			Fx.Reshape(band, Crescent(a, b, thick * 0.45f, swell * thin));
			Fx.Reshape(core, Crescent(a, b, thick * 0.13f, (1f + 0.8f * Mathf.Sin(flash * Mathf.Pi)) * thin));
			core.Modulate = new Color(1f, 1f, 1f, 1f);
			glow.Modulate = new Color(1f + Mathf.Sin(flash * Mathf.Pi), 1f + Mathf.Sin(flash * Mathf.Pi), 1f + Mathf.Sin(flash * Mathf.Pi), 1f);
			head.Position = Point(Mathf.Min(b, 1f), -thick * 0.2f);
			float headPop = time < tIn ? 1f : 1f - Ease.InCubic(Mathf.Clamp((time - tIn) / (tFlash + tHold + tOut * 0.6f), 0f, 1f)) * 0.7f;
			head.Scale = Vector2.One * headPop * (1f + 0.25f * Mathf.Sin(time * 60f));
			head.Rotation = time * 4f;
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((outU - 0.6f) / 0.4f, 0f, 1f)));
		});
		// 收尾：头端炸成一簇光点
		Fx.After(root, tIn + tFlash + tHold + tOut * 0.8f, "arc slash end", () =>
		{
			Vector2 end = root.ToGlobal(Point(1f, 0f));
			GpuParticles2D sp = Fx.Sparks(Fx.Root(layer, end, "P3ArcSparks"), tint, (int)(16 * s), 80f * s, 320f * s, 0.4f, gravity: 0f);
			Fx.FreeAfter(sp.GetParent(), 0.6);
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	/// <summary>连续乱斩：n 道交叉斩痕依次闪过，最后一记爆开。</summary>
	public static void MultiCut(Control layer, Vector2 at, int n, Color tint, float s = 1f)
	{
		for (int i = 0; i < n; i++)
		{
			int k = i;
			Fx.After(layer, 0.045 * k, "multicut", () =>
			{
				float ang = (k % 2 == 0 ? -0.7f : 0.6f) + Fx.Rand(-0.25f, 0.25f);
				Vector2 off = new(Fx.Rand(-40f, 40f) * s, Fx.Rand(-50f, 50f) * s);
				Elements.Slash(layer, at + off, tint, 0.7f * s);
				P3Fx.Flash(layer, at + off, tint, 160f * s, 0.1f);
				if (k == n - 1)
				{
					Bold.Impact(layer, at, tint, s, ShakeStrength.Medium);
					P3Fx.SpeedLines(layer, at, Pal.Ice, 18, 60f * s, 520f * s, 0.35f);
				}
			});
		}
	}

	/// <summary>贯穿突刺：一道极细极亮的光枪从左侧刺穿目标，出口喷出光屑。</summary>
	public static void Thrust(Control layer, Vector2 at, Color tint, float s = 1f, float facing = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3Thrust");
		root.Scale = new Vector2(facing, 1f);
		float len = 900f * s;
		Line2D glow = Fx.Line(root, new[] { new Vector2(-len * 0.6f, 0f), new Vector2(len * 0.4f, 0f) }, new Color(tint, 0.7f), 34f * s);
		Line2D core = Fx.Line(root, new[] { new Vector2(-len * 0.6f, 0f), new Vector2(len * 0.4f, 0f) }, Pal.White, 7f * s);
		root.Rotation = Fx.Rand(-0.12f, 0.08f) * facing;
		Fx.Animate(root, 0.35f, t =>
		{
			float grow = Ease.OutExpo(Mathf.Clamp(t / 0.25f, 0f, 1f));
			glow.Scale = core.Scale = new Vector2(grow, 1f - 0.8f * t);
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
		});
		Line2D ring = Fx.Line(root, Fx.Circle(70f * s, 32, 2.4f), new Color(Pal.Ice, 0.9f), 4f, closed: true);
		ring.Rotation = Mathf.Pi / 2f;
		Fx.Animate(ring, 0.3f, t => ring.Scale = Vector2.One * (0.4f + 1.4f * Ease.OutExpo(t)));
		Node2D exit = Fx.Root(layer, at + new Vector2(60f * s * facing, 0f), "P3ThrustExit");
		GpuParticles2D spray = Fx.Sparks(exit, tint, (int)(50 * s), 400f * s, 1100f * s, 0.4f, scaleMin: 0.12f, scaleMax: 0.35f);
		ParticleProcessMaterial pm = (ParticleProcessMaterial)spray.ProcessMaterial;
		pm.Direction = new Vector3(facing, 0f, 0f);
		pm.Spread = 22f;
		Fx.FreeAfter(exit, 0.7);
		Bold.Impact(layer, at, tint, 0.7f * s, ShakeStrength.Medium);
		Fx.FreeAfter(root, 0.4);
	}

	/// <summary>钻头：旋转的螺旋锥体顶在目标上，火花四溅。</summary>
	public static void Drill(Control layer, Vector2 at, float s = 1f, float duration = 0.5f)
	{
		Node2D root = Fx.Root(layer, at + new Vector2(-170f * s, 0f), "P3Drill");
		Polygon2D cone = Fx.Poly(root, new[] { new Vector2(0f, -70f * s), new Vector2(240f * s, 0f), new Vector2(0f, 70f * s) }, new Color(Pal.Royal, 0.8f), additive: true);
		Node2D stripes = new();
		root.AddChild(stripes);
		for (int i = 0; i < 6; i++)
		{
			Fx.Line(stripes, new[] { new Vector2(i * 40f * s, -70f * s * (1f - i / 6f)), new Vector2((i * 40f + 30f) * s, 70f * s * (1f - (i + 0.75f) / 6f)) }, new Color(Pal.Ice, 0.9f), 4f);
		}
		Fx.Animate(root, duration, t =>
		{
			stripes.Position = new Vector2(0f, Mathf.Sin(t * 80f) * 6f);
			root.Scale = new Vector2(1f, 0.85f + 0.15f * Mathf.Sin(t * 60f));
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f)));
		});
		Node2D sparkAt = Fx.Root(layer, at, "P3DrillSparks");
		GpuParticles2D sp = Fx.Sparks(sparkAt, Pal.Ice, (int)(80 * s), 300f, 800f, 0.35f, gravity: 500f, scaleMin: 0.1f, scaleMax: 0.3f, oneShot: false);
		Fx.After(sparkAt, duration, "drill stop", () => sp.Emitting = false);
		Fx.FreeAfter(sparkAt, duration + 0.5);
		Fx.FreeAfter(root, duration + 0.05);
		Bold.Impact(layer, at, Pal.Cyan, 0.6f * s, ShakeStrength.Weak);
	}

	/// <summary>漫画式重击：尖刺星爆 + 放射线 + 地面龟裂（拳 / 踢 / 锤）。</summary>
	public static void Smash(Control layer, Vector2 at, Vector2 feet, Color tint, float s = 1f, bool crack = true)
	{
		Node2D root = Fx.Root(layer, at, "P3Smash");
		var star = new Vector2[24];
		for (int i = 0; i < 24; i++)
		{
			float r = (i % 2 == 0 ? 170f : 70f) * s * Fx.Rand(0.8f, 1.15f);
			star[i] = Vector2.Right.Rotated(Mathf.Tau * i / 24f) * r;
		}
		// v0.14.22（用户：碰撞轨迹的特效挡住人物）：实心星形只在命中瞬间闪一下（约 0.06s 内淡掉），
		// 之后只剩星形描边继续扩散，不再整块盖住目标
		Polygon2D fill = Fx.Poly(root, star, new Color(tint, 0.85f), additive: true);
		var inner = new Vector2[24];
		for (int i = 0; i < 24; i++)
		{
			inner[i] = star[i] * 0.55f;
		}
		Polygon2D core = Fx.Poly(root, inner, Pal.White, additive: true);
		Line2D edge = Fx.Line(root, star, new Color(tint, 0.95f), 5f * s, closed: true);
		Line2D edgeIn = Fx.Line(root, inner, new Color(Pal.White, 0.7f), 2.5f * s, closed: true);
		Fx.Animate(root, 0.35f, t =>
		{
			root.Scale = Vector2.One * (0.3f + 1.0f * Ease.OutExpo(Mathf.Clamp(t / 0.3f, 0f, 1f)));
			root.Rotation = t * 0.3f;
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
			float flash = 1f - Ease.OutCubic(Mathf.Clamp((t - 0.06f) / 0.14f, 0f, 1f));
			fill.Modulate = new Color(1f, 1f, 1f, 0.15f + 0.85f * flash);
			core.Modulate = new Color(1f, 1f, 1f, flash);
			edgeIn.Modulate = new Color(1f, 1f, 1f, 1f - flash * 0.5f);
		});
		Fx.FreeAfter(root, 0.4);
		P3Fx.SpeedLines(layer, at, Pal.White, 20, 90f * s, 560f * s, 0.3f);
		Bold.Impact(layer, at, tint, s, ShakeStrength.Strong);
		EvokerShot.HitStop(0.06);
		if (crack)
		{
			GroundCrack(layer, feet, s);
		}
	}

	public static void GroundCrack(Control layer, Vector2 feet, float s = 1f)
	{
		Node2D root = Fx.Root(layer, feet, "P3Crack");
		Node2D flat = new() { Scale = new Vector2(1f, 0.3f) };
		root.AddChild(flat);
		for (int i = 0; i < 9; i++)
		{
			Vector2 dir = Vector2.Right.Rotated(Mathf.Tau * i / 9f + Fx.Rand(-0.2f, 0.2f));
			var pts = new Vector2[5];
			for (int k = 0; k < 5; k++)
			{
				pts[k] = dir * (k * 55f * s) + dir.Orthogonal() * (k == 0 ? 0f : Fx.Rand(-14f, 14f));
			}
			Fx.Line(flat, pts, new Color(0.05f, 0.06f, 0.1f, 0.9f), 6f * s, additive: false);
			Fx.Line(flat, pts, new Color(Pal.Cyan, 0.6f), 2f);
		}
		Fx.Animate(root, 0.9f, t => root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((t - 0.5f) / 0.5f, 0f, 1f))));
		Node2D dust = Fx.Root(layer, feet, "P3Dust");
		Fx.Sparks(dust, new Color(0.7f, 0.75f, 0.85f), (int)(30 * s), 150f, 380f, 0.6f, gravity: 300f, scaleMin: 0.4f, scaleMax: 0.9f).Material = null;
		Fx.FreeAfter(dust, 0.9);
		Fx.FreeAfter(root, 1.0);
	}

	// ═════════ 天象 ═════════

	/// <summary>一颗陨石：从左上方斜坠，长尾 → 撞击闪光 + 冲击 + 碎屑。</summary>
	public static void Meteor(Control layer, Vector2 at, Color tint, float s = 1f, float delay = 0f)
	{
		Fx.After(layer, delay, "meteor", () =>
		{
			Vector2 from = at + new Vector2(-520f, -760f) * s;
			Node2D head = Fx.Root(layer, from, "P3Meteor");
			Sprite2D glow = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(tint, 0.9f), Scale = Vector2.One * 4f * s };
			head.AddChild(glow);
			Polygon2D core = Fx.Poly(head, Fx.Circle(16f * s, 16), Pal.White, additive: true);
			Line2D tail = new() { Width = 46f * s, Material = Fx.Additive, Gradient = new Gradient { Colors = new[] { new Color(tint, 0f), new Color(tint, 0.95f) } } };
			Node2D tailRoot = Fx.Root(layer, Vector2.Zero, "P3MeteorTail");
			tailRoot.TopLevel = true;
			tailRoot.GlobalPosition = Vector2.Zero;
			tailRoot.AddChild(tail);
			Fx.Animate(head, 0.22f, t =>
			{
				Vector2 p = from.Lerp(at, Ease.InCubic(t));
				head.GlobalPosition = p;
				tail.Points = new[] { from.Lerp(p, 0.35f), p };
			});
			Fx.After(head, 0.22, "meteor hit", () =>
			{
				head.QueueFree();
				tailRoot.QueueFree();
				Bold.Impact(layer, at, tint, 0.8f * s, ShakeStrength.Medium);
				Node2D debris = Fx.Root(layer, at, "P3MeteorDebris");
				Fx.Sparks(debris, tint, (int)(40 * s), 300f * s, 800f * s, 0.6f, gravity: 900f, scaleMin: 0.15f, scaleMax: 0.4f);
				Fx.FreeAfter(debris, 0.9);
			});
		});
	}

	/// <summary>流星群：count 颗陨石错落砸向各个目标。</summary>
	/// <summary>
	/// 流星雨。shells = false：P3R「明けの明星」（Morning Star，skill_0082 第 3 段）一整段连串坠落（流星雨；v0.14.7 起 1.5s 后缓出）；
	/// shells = true（轰击，v0.14.7）：不放整段长音，每颗落地时各响一声短促的冲击（bomb_hit，0.45s，音高随机），声音和落点一一对应，整串约 1s。
	/// </summary>
	public static void MeteorRain(Control layer, Vector2[] targets, int count, Color tint, float s = 1f, bool shells = false)
	{
		if (!shells)
		{
			Audio.Sfx.Clip("morning_star_rain", 0.85f);
		}
		for (int i = 0; i < count; i++)
		{
			Vector2 t = targets[i % targets.Length] + new Vector2(Fx.Rand(-90f, 90f), Fx.Rand(-60f, 60f));
			float delay = 0.07f * i + Fx.Rand(0f, 0.04f);
			Meteor(layer, t, tint, s * Fx.Rand(0.7f, 1.1f), delay);
			if (shells)
			{
				Audio.Sfx.Clip("bomb_hit", i == 0 ? 0.75f : 0.5f, delay + 0.22f, Fx.Rand(0.9f, 1.15f));   // 0.22s = 流星落地
			}
		}
	}

	/// <summary>巨型彗星：从屏幕左上角横越到目标，尾巴极长。</summary>
	public static void Comet(Control layer, Vector2 at, Color tint, float s = 1f)
	{
		Audio.Sfx.Clip("morning_star_fall", 0.9f);              // P3R「明けの明星」第 2 段：单颗巨大陨星坠落
		Vector2 size = layer.GetViewportRect().Size;
		Vector2 from = new(-200f, size.Y * 0.05f);
		Node2D head = Fx.Root(layer, from, "P3Comet");
		Sprite2D glow = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(tint, 0.95f), Scale = Vector2.One * 9f * s };
		head.AddChild(glow);
		Fx.Poly(head, Fx.Circle(30f * s, 20), Pal.White, additive: true);
		Node2D tailRoot = Fx.Root(layer, Vector2.Zero, "P3CometTail");
		tailRoot.TopLevel = true;
		tailRoot.GlobalPosition = Vector2.Zero;
		Line2D tail = new() { Width = 110f * s, Material = Fx.Additive, Gradient = new Gradient { Colors = new[] { new Color(tint, 0f), new Color(tint, 0.7f), new Color(Pal.White, 1f) }, Offsets = new[] { 0f, 0.7f, 1f } } };
		tailRoot.AddChild(tail);
		Fx.Animate(head, 0.4f, t =>
		{
			Vector2 p = from.Lerp(at, Ease.InCubic(t));
			head.GlobalPosition = p;
			tail.Points = new[] { from.Lerp(p, 0.15f), p };
		});
		Fx.After(head, 0.4, "comet hit", () =>
		{
			head.QueueFree();
			Fx.Animate(tail, 0.3f, t => tail.Modulate = new Color(1f, 1f, 1f, 1f - t));
			Fx.FreeAfter(tailRoot, 0.32);
			Bold.Megidolaon(layer, at, 0.7f * s);
		});
	}

	/// <summary>星座：星点依次亮起并连线，最后整体一闪。pattern 为相对坐标（单位：像素）。</summary>
	public static void Constellation(Control layer, Vector2 at, Vector2[] pattern, Color tint, float s = 1f)
	{
		Audio.Sfx.Clip("Theurgia_Max", 0.6f);                 // 星座连线：P3R 的"灵能槽满"上扬声
		Node2D root = Fx.Root(layer, at, "P3Constellation");
		for (int i = 0; i < pattern.Length; i++)
		{
			int k = i;
			Vector2 p = pattern[i] * s;
			Fx.After(root, 0.06 * k, "star point", () =>
			{
				Node2D st = new() { Position = p };
				root.AddChild(st);
				Fx.Poly(st, Fx.Star4(22f * s, 0.18f), Pal.White, additive: true);
				Sprite2D g = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(tint, 0.8f), Scale = Vector2.One * 1.6f * s };
				st.AddChild(g);
				Fx.Animate(st, 0.25f, t => st.Scale = Vector2.One * Ease.OutBack(t));
				if (k > 0)
				{
					Line2D line = Fx.Line(root, new[] { pattern[k - 1] * s, pattern[k - 1] * s }, new Color(tint, 0.85f), 3f);
					Vector2 a = pattern[k - 1] * s;
					Fx.Animate(line, 0.12f, t => line.Points = new[] { a, a.Lerp(p, Ease.OutCubic(t)) });
				}
			});
		}
		float done = 0.06f * pattern.Length + 0.15f;
		Fx.After(root, done, "constellation flash", () => P3Fx.Flash(layer, at, tint, 500f * s, 0.3f));
		Fx.Animate(root, done + 0.6f, t => root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((t * (done + 0.6f) - done) / 0.6f, 0f, 1f))));
		Fx.FreeAfter(root, done + 0.65);
	}

	/// <summary>北斗七星（七星）。</summary>
	public static readonly Vector2[] BigDipper =
	{
		new(-300f, -40f), new(-190f, -70f), new(-90f, -60f), new(0f, -20f), new(40f, 70f), new(170f, 90f), new(200f, -10f),
	};

	/// <summary>满月：目标身后升起巨大的月亮，一道月光从上方照下。</summary>
	public static void FullMoon(Control layer, Vector2 at, float s = 1f)
	{
		Node2D root = Fx.Root(layer, at + new Vector2(0f, -40f), "P3FullMoon");
		Sprite2D halo = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0.6f), Scale = Vector2.One * 16f * s };
		root.AddChild(halo);
		Fx.Poly(root, Fx.Circle(150f * s, 64), new Color(0.88f, 0.95f, 1f, 0.85f), additive: true);
		root.Scale = Vector2.One * 0.2f;
		Fx.Animate(root, 0.8f, t =>
		{
			root.Scale = Vector2.One * (0.2f + 0.8f * Ease.OutBack(Mathf.Clamp(t / 0.35f, 0f, 1f)));
			root.Modulate = new Color(1f, 1f, 1f, Ease.Envelope(t, 0.1f, 0.6f));
		});
		Fx.After(root, 0.2, "moon beam", () =>
		{
			P3Fx.LightPillar(layer, at + new Vector2(0f, 180f * s), Pal.Ice, 150f * s, 1000f * s, 0.5f);
			Bold.Impact(layer, at, Pal.Cyan, 0.8f * s, ShakeStrength.Medium);
		});
		Fx.FreeAfter(root, 0.85);
	}

	/// <summary>太阳耀斑：日轮带旋转的日冕尖刺，爆开一圈烈光。</summary>
	public static void SolarFlare(Control layer, Vector2 at, float s = 1f) => SolarFlare(layer, layer, at, at, s);

	/// <summary>
	/// 太阳打击（v0.13.10 重做，不再盖住敌人）：小太阳在敌人斜上方、画在身后层升起 —— 空心细光芒旋转 + 白热小核 + 柔光；
	/// 0.18s 后一道细光束从太阳射到目标，命中点只有小闪光和细冲击环。
	/// </summary>
	public static void SolarFlare(Control back, Control front, Vector2 sunAt, Vector2 target, float s = 1f)
	{
		Node2D root = Fx.Root(back, sunAt, "P3Solar");
		root.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 5.5f * s, Modulate = new Color(ElementPal.FireMid, 0.55f) });
		Node2D rays = new();
		root.AddChild(rays);
		for (int i = 0; i < 16; i++)
		{
			Vector2 d = Vector2.Right.Rotated(Mathf.Tau * i / 16f);
			float len = (i % 2 == 0 ? 120f : 82f) * s;
			Fx.Stroke(rays, new[] { d * 62f * s, d * len }, new Color(i % 2 == 0 ? ElementPal.FireCore : ElementPal.FireMid, 0.9f), (i % 2 == 0 ? 5f : 3f) * s);
		}
		Fx.StrokeClosed(root, Fx.Circle(58f * s, 40), new Color(ElementPal.FireMid, 0.9f), 3f * s);
		Fx.Poly(root, Fx.Circle(46f * s, 40), new Color(ElementPal.FireCore, 0.95f), additive: true);
		Fx.Poly(root, Fx.Circle(26f * s, 32), Pal.White, additive: true);
		const float total = 0.75f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			rays.Rotation = time * 1.4f;
			float up = Ease.OutBack(Mathf.Clamp(time / 0.2f, 0f, 1f));
			root.Scale = Vector2.One * Mathf.Max(0.05f, up);
			root.GlobalPosition = sunAt + FxSpace.Vector(back, new Vector2(0f, 40f * (1f - Ease.OutCubic(Mathf.Clamp(time / 0.2f, 0f, 1f)))));
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((time - 0.5f) / 0.25f, 0f, 1f)));
		});
		Fx.FreeAfter(root, total + 0.05);
		if (sunAt == target)
		{
			return;
		}
		Fx.After(root, 0.18, "solar beam", () =>
		{
			Node2D b = Fx.Root(front, Vector2.Zero, "P3SolarBeam");
			Vector2 dir = (target - sunAt).Normalized();
			Polygon2D beam = Fx.Stroke(b, new[] { sunAt + dir * 50f * s, target }, new Color(ElementPal.FireCore, 0.85f), 16f * s);
			Polygon2D core = Fx.Stroke(b, new[] { sunAt + dir * 50f * s, target }, Pal.White, 5f * s);
			Fx.Animate(b, 0.28f, t =>
			{
				b.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
				float w = 1f - 0.7f * t;
				Fx.Reshape(beam, Fx.StrokePts(new[] { sunAt + dir * 50f * s, target }, 16f * s * w));
				Fx.Reshape(core, Fx.StrokePts(new[] { sunAt + dir * 50f * s, target }, 5f * s * w));
			});
			Fx.FreeAfter(b, 0.3);
			P3Fx.Flash(front, target, ElementPal.FireMid, 150f * s, 0.15f);
			P3Fx.Shockwave(front, target, ElementPal.FireMid, 320f * s, 0.35f);
			MegaCrit.Sts2.Core.Nodes.NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
		});
	}

	// ═════════ P3R 主题 ═════════

	/// <summary>大塔罗牌：翻面登场（罗马数字 + 牌名 + 中央纹章），停留，然后碎成玻璃。</summary>
	public static void BigTarot(Control layer, Vector2 at, string numeral, string name, Color tint, float hold = 0.45f, float s = 1f, Control? fxLayer = null)
	{
		Control fx = fxLayer ?? layer;
		Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.95f);
		Node2D root = Fx.Root(layer, at, "P3BigTarot");
		const float w = 95f, h = 160f;
		Node2D card = new() { Scale = Vector2.One * s };
		root.AddChild(card);
		Vector2[] rect = { new(-w, -h), new(w, -h), new(w, h), new(-w, h) };
		Sprite2D glow = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(tint, 0.55f), Scale = new Vector2(9f, 13f) };
		card.AddChild(glow);
		Fx.Poly(card, rect, new Color(Pal.Abyss, 0.96f));
		Fx.Line(card, rect, tint, 5f, closed: true, additive: false);
		Fx.Line(card, new[] { new Vector2(-w + 12f, -h + 12f), new Vector2(w - 12f, -h + 12f), new Vector2(w - 12f, h - 12f), new Vector2(-w + 12f, h - 12f) }, new Color(Pal.Ice, 0.7f), 2f, closed: true, additive: false);
		Fx.Poly(card, Fx.Star4(62f, 0.22f), new Color(tint, 0.95f), additive: true);
		Fx.Line(card, Fx.Circle(54f, 40), new Color(Pal.Ice, 0.8f), 2f, closed: true);
		Typo.Text(card, numeral, Typo.Latin, 40, Pal.Ice, new Vector2(-w + 14f, -h + 10f), outline: 2, outlineColor: Pal.Abyss);
		Typo.Text(card, name, Typo.Latin, 26, Pal.White, new Vector2(-w + 16f, h - 48f), outline: 2, outlineColor: Pal.Abyss);
		float total = 0.22f + hold + 0.2f;
		Fx.Animate(card, total, t =>
		{
			float time = t * total;
			float flip = Mathf.Clamp(time / 0.22f, 0f, 1f);
			card.Scale = new Vector2(s * Mathf.Abs(Mathf.Cos((1f - Ease.OutCubic(flip)) * Mathf.Pi * 1.5f)), s * (0.9f + 0.1f * Ease.OutBack(flip)));
			card.Position = new Vector2(0f, -30f * Ease.OutCubic(Mathf.Clamp(time / 0.6f, 0f, 1f)));
		});
		Fx.After(root, total - 0.05, "tarot shatter", () =>
		{
			P3Fx.GlassShatter(fx, root.GlobalPosition, (int)(30 * s), 1.1f * s, tint);
			Audio.Sfx.Play(Audio.Snd.TarotShatter, 1.0f);
			P3Fx.Flash(layer, root.GlobalPosition, tint, 360f * s, 0.25f);
			root.QueueFree();
		});
	}

	/// <summary>Shuffle Time：一圈牌在角色身边旋转洗牌，然后一张张飞向手牌区。</summary>
	public static void ShuffleTime(Control layer, Vector2 at, int cards, Color tint)
	{
		Vector2 hand = layer.GetViewportRect().Size * new Vector2(0.5f, 0.92f);
		for (int i = 0; i < cards; i++)
		{
			int k = i;
			Node2D c = Fx.Root(layer, at, "P3ShuffleCard");
			Vector2[] rect = { new(-22f, -34f), new(22f, -34f), new(22f, 34f), new(-22f, 34f) };
			Fx.Poly(c, rect, new Color(Pal.Abyss, 0.95f));
			Fx.Line(c, rect, tint, 3f, closed: true, additive: false);
			Fx.Poly(c, Fx.Star4(14f, 0.3f), Pal.Ice, additive: true);
			float phase = Mathf.Tau * k / cards;
			Fx.Animate(c, 0.9f, t =>
			{
				float spin = Mathf.Clamp(t / 0.55f, 0f, 1f);
				Vector2 orbit = at + new Vector2(Mathf.Cos(phase + spin * 6f) * 150f, Mathf.Sin(phase + spin * 6f) * 60f - 80f);
				float fly = Ease.InCubic(Mathf.Clamp((t - 0.55f - 0.04f * k) / 0.3f, 0f, 1f));
				c.GlobalPosition = orbit.Lerp(hand, fly);
				c.Rotation = spin * 6f + fly * 3f;
				c.Scale = new Vector2(Mathf.Cos(spin * 12f) * 1.1f, 1.1f);
				c.Modulate = new Color(1f, 1f, 1f, 1f - fly * 0.8f);
			});
			Fx.FreeAfter(c, 0.95);
		}
		P3Fx.Flash(layer, at + new Vector2(0f, -80f), tint, 320f, 0.3f);
	}

	/// <summary>P3R 风格大字切入（"CRITICAL" / "ONE MORE!" / 技能名）：斜切色带 + 粗体描边大字，猛地砸入，略停后滑出。</summary>
	public static void CutIn(Control layer, Vector2 at, string text, Color tint, float size = 92f)
	{
		Audio.Sfx.Play(text.StartsWith("ONE MORE") ? Audio.Snd.OneMore : Audio.Snd.SkillCutIn, 1.0f);
		Node2D root = Fx.Root(layer, at + new Vector2(-40f, -260f), "P3CutIn");
		root.Skew = -0.25f;
		float w = text.Length * size * 0.62f + 80f;
		Fx.Poly(root, new[] { new Vector2(-30f, -10f), new Vector2(w, -10f), new Vector2(w - 30f, size * 1.05f), new Vector2(-60f, size * 1.05f) }, new Color(0.02f, 0.03f, 0.08f, 0.88f));
		Fx.Poly(root, new[] { new Vector2(-30f, -10f), new Vector2(w, -10f), new Vector2(w - 3f, -2f), new Vector2(-33f, -2f) }, tint, additive: true);
		Typo.Text(root, text, Typo.Latin, (int)size, Pal.White, new Vector2(0f, -6f), outline: 4, outlineColor: tint, shadow: new Color(tint, 0.9f), shadowOffset: new Vector2(7f, 6f));
		Vector2 home = root.GlobalPosition;
		Fx.Animate(root, 0.75f, t =>
		{
			float time = t * 0.75f;
			float slam = Ease.OutBack(Mathf.Clamp(time / 0.12f, 0f, 1f));
			root.Scale = Vector2.One * Mathf.Lerp(1.8f, 1f, slam);
			float exit = Ease.InCubic(Mathf.Clamp((time - 0.5f) / 0.25f, 0f, 1f));
			root.GlobalPosition = home + new Vector2(260f * exit, 0f);
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.05f, 0f, 1f) * (1f - exit));
		});
		Fx.FreeAfter(root, 0.8);
	}

	/// <summary>加 / 减益法阵（Tarukaja / Debilitate 风格）：旋转的六边形符环 + 光柱 + 箭头。</summary>
	public static void Seal(Control layer, Vector2 center, Vector2 feet, Color tint, bool up, float s = 1f)
	{
		Node2D root = Fx.Root(layer, feet, "P3Seal");
		Node2D ring = new() { Scale = new Vector2(1f, 0.32f) };
		root.AddChild(ring);
		Fx.Line(ring, Fx.Hexagon(180f * s), tint, 4f, closed: true);
		Fx.Line(ring, Fx.Circle(210f * s, 48), new Color(tint, 0.6f), 2f, closed: true);
		Fx.Animate(root, 0.8f, t =>
		{
			ring.Rotation = (up ? 1f : -1f) * t * 2.5f;
			root.Modulate = new Color(1f, 1f, 1f, Ease.Envelope(t, 0.1f, 0.6f));
		});
		P3Fx.LightPillar(layer, feet, tint, 170f * s, 520f * s, 0.6f);
		Extras.BuffArrows(layer, center, feet, up);
		P3Fx.Flash(layer, center, tint, 300f * s, 0.25f);
		Fx.FreeAfter(root, 0.85);
	}

	/// <summary>锻造：铁砧火花 —— 白橙星爆 + 火花喷泉 + 青色淬火环。</summary>
	/// <summary>锻造 / 精炼的那一刻（原版 regent_forge / regent_refine 事件，见 Sfx.ReplaceRegent）：在结城理身前爆一团锻造火花，和打铁声同步。</summary>
	public static void ForgeAtMakoto(float s = 1f)
	{
		if (MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance is not { } room || P3Makoto.Core.Makoto.FxLayer() is not Control front)
		{
			return;
		}
		foreach (MegaCrit.Sts2.Core.Nodes.Combat.NCreature n in room.CreatureNodes)
		{
			if (GodotObject.IsInstanceValid(n) && n.Entity.IsPlayer && P3Makoto.Core.Makoto.Is(n.Entity.Player))
			{
				ForgeSpark(front, P3Makoto.Core.Makoto.Center(n) + P3Makoto.Core.Makoto.Offset(n, new Vector2(100f, -20f)), 0.8f * s);
				return;
			}
		}
	}

	public static void ForgeSpark(Control layer, Vector2 at, float s = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3Forge");
		Fx.Poly(root, Fx.Star4(120f * s, 0.12f), ElementPal.FireCore, additive: true);
		Fx.Animate(root, 0.25f, t =>
		{
			root.Scale = Vector2.One * (0.4f + 1.1f * Ease.OutExpo(t));
			root.Modulate = new Color(1f, 1f, 1f, 1f - t);
		});
		Fx.FreeAfter(root, 0.3);
		Node2D fountain = Fx.Root(layer, at, "P3ForgeSparks");
		GpuParticles2D sp = Fx.Sparks(fountain, ElementPal.FireMid, (int)(70 * s), 300f, 900f, 0.7f, gravity: 1200f, scaleMin: 0.08f, scaleMax: 0.22f,
			ramp: new[] { ElementPal.FireCore, ElementPal.FireMid, Pal.Cyan });
		((ParticleProcessMaterial)sp.ProcessMaterial).Spread = 70f;
		Fx.FreeAfter(fountain, 1.0);
		P3Fx.Shockwave(layer, at, Pal.Cyan, 380f * s, 0.4f);
		NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short);
	}

	/// <summary>影时间时钟：表盘浮现，指针飞转到 XII，墨绿一闪（预知 / 时间相关的牌）。</summary>
	public static void DarkClock(Control layer, Vector2 at, float s = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3DarkClock");
		Audio.Sfx.Clip("clock_spin", 0.6f);                   // 指针飞转（P3R 轮盘的棘轮声）
		Audio.Sfx.Clip("clock_midnight", 0.75f, 0.5f);         // 0.5s 指针到 XII：P3R 报时长音
		Fx.Line(root, Fx.Circle(150f * s, 60), new Color(Pal.DarkHour, 0.85f), 4f, closed: true);
		for (int i = 0; i < 12; i++)
		{
			Vector2 d = Vector2.Up.Rotated(Mathf.Tau * i / 12f);
			Fx.Line(root, new[] { d * 136f * s, d * (i % 3 == 0 ? 112f : 124f) * s }, new Color(Pal.DarkHour, 0.9f), i % 3 == 0 ? 4f : 2f);
		}
		Line2D hour = Fx.Line(root, new[] { Vector2.Zero, Vector2.Up * 80f * s }, Pal.Ice, 7f);
		Line2D minute = Fx.Line(root, new[] { Vector2.Zero, Vector2.Up * 120f * s }, Pal.Ice, 4f);
		Fx.Animate(root, 0.9f, t =>
		{
			float spin = Ease.OutExpo(Mathf.Clamp(t / 0.55f, 0f, 1f));
			minute.Rotation = (1f - spin) * Mathf.Tau * 3f;
			hour.Rotation = (1f - spin) * Mathf.Tau * 0.75f;
			root.Scale = Vector2.One * (0.6f + 0.4f * Ease.OutBack(Mathf.Clamp(t / 0.25f, 0f, 1f)));
			root.Modulate = new Color(1f, 1f, 1f, Ease.Envelope(t, 0.12f, 0.75f));
		});
		Fx.After(root, 0.5, "clock midnight", () =>
		{
			P3Fx.Flash(layer, at, Pal.DarkHour, 520f * s, 0.35f);
			P3Fx.Ripple(layer, at, 700f * s, 0.7f, 0.03f);
		});
		Fx.FreeAfter(root, 0.95);
	}
}
