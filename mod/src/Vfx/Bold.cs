using System;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// v0.9 "大胆版"特效库（第一部分：元素命中，P3 的 -dyne 级魔法）。
/// 原则：尺寸大（通常是目标高度的 1.5~3 倍）、层次多（预兆 → 爆发 → 余韵）、必有冲击确认（闪光 / 冲击波 / 震屏中的至少两样），
/// 色彩贴合 P3R：冷蓝为底，火冰雷光暗各自鲜明。center = 目标中心，feet = 目标脚底，s = 规模（多段攻击时缩小）。
/// </summary>
public static class Bold
{
	// ───────── 工具 ─────────
	private static Vector2[] Spike(float length, float width) => new[]
	{
		new Vector2(0f, 0f), new Vector2(-width * 0.5f, -length * 0.25f), new Vector2(0f, -length), new Vector2(width * 0.5f, -length * 0.25f)
	};

	private static Vector2[] Teardrop(float r, float tail)
	{
		var pts = new Vector2[20];
		for (int i = 0; i < 20; i++)
		{
			float a = Mathf.Tau * i / 20f;
			float y = Mathf.Sin(a);
			float stretch = y < 0f ? tail : 1f;
			pts[i] = new Vector2(Mathf.Cos(a) * r * (y < 0f ? 1f + y * 0.6f : 1f), y * r * stretch);
		}
		return pts;
	}

	private static Vector2[] Jagged(Vector2 from, Vector2 to, int segments, float jitter)
	{
		var pts = new Vector2[segments + 1];
		Vector2 n = (to - from).Orthogonal().Normalized();
		for (int i = 0; i <= segments; i++)
		{
			float u = i / (float)segments;
			float j = (i == 0 || i == segments) ? 0f : Fx.Rand(-jitter, jitter) * Mathf.Sin(u * Mathf.Pi);
			pts[i] = from.Lerp(to, u) + n * j;
		}
		return pts;
	}

	/// <summary>"啪"的一下：大光斑 + 冲击波 + 震屏（所有大招的确认感来源）。</summary>
	public static void Impact(Control layer, Vector2 at, Color tint, float s, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength shake)
	{
		P3Fx.Flash(layer, at, tint, 420f * s, 0.28f);
		P3Fx.Shockwave(layer, at, tint, 700f * s, 0.5f);
		MegaCrit.Sts2.Core.Nodes.NGame.Instance?.ScreenShake(shake, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeDuration.Short);
	}

	// ───────── 火：Agidyne —— 脚下炸开的火柱 + 火舌 + 余烬 ─────────
	public static void Agidyne(Control layer, Vector2 center, Vector2 feet, float s = 1f)
	{
		Node2D root = Fx.Root(layer, feet, "P3Agidyne");
		// 预兆：脚下一圈橙色火环迅速收缩
		Line2D ring = Fx.Line(root, Fx.Circle(160f * s, 40, 0.3f), new Color(ElementPal.FireMid, 0.9f), 6f * s, closed: true);
		Fx.Animate(ring, 0.12f, t => ring.Scale = Vector2.One * Mathf.Lerp(1.6f, 0.6f, Ease.OutCubic(t)));
		Fx.After(root, 0.1, "agi burst", () =>
		{
			ring.Visible = false;
			// 火舌：7 道泪滴形火焰从脚下向上窜起、摇摆、熄灭
			for (int i = 0; i < 7; i++)
			{
				float x = (i - 3) * 38f * s + Fx.Rand(-10f, 10f);
				float h = Fx.Rand(1.6f, 2.6f) * s * (1f - Mathf.Abs(i - 3) * 0.12f);
				Node2D tongue = new() { Position = new Vector2(x, 0f), Rotation = Fx.Rand(-0.15f, 0.15f) };
				root.AddChild(tongue);
				Polygon2D outer = Fx.Poly(tongue, Teardrop(46f, 3.2f), new Color(ElementPal.FireOuter, 0.85f), additive: true);
				Polygon2D mid = Fx.Poly(tongue, Teardrop(30f, 3f), new Color(ElementPal.FireMid, 0.95f), additive: true);
				Polygon2D core = Fx.Poly(tongue, Teardrop(14f, 2.6f), ElementPal.FireCore, additive: true);
				outer.Position = mid.Position = core.Position = new Vector2(0f, -40f);
				float delay = Mathf.Abs(i - 3) * 0.025f;
				Fx.Animate(tongue, 0.75f, t =>
				{
					float time = t * 0.75f - delay;
					float grow = Ease.OutBack(Mathf.Clamp(time / 0.18f, 0f, 1f));
					float fade = 1f - Ease.InCubic(Mathf.Clamp((time - 0.35f) / 0.4f, 0f, 1f));
					tongue.Scale = new Vector2(Mathf.Max(grow, 0.01f) * (0.9f + 0.1f * Mathf.Sin(time * 40f)), Mathf.Max(grow * h, 0.01f) * (0.8f + 0.2f * fade));
					tongue.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(fade, 0f, 1f) * Mathf.Clamp(time * 20f, 0f, 1f));
				});
			}
			// 火柱核心 + 余烬
			Node2D emberAt = new() { Position = new Vector2(0f, -40f * s) };
			root.AddChild(emberAt);
			GpuParticles2D embers = Fx.Sparks(emberAt, ElementPal.FireMid, (int)(90 * s), 200f * s, 700f * s, 1.1f, gravity: -260f,
				scaleMin: 0.15f, scaleMax: 0.55f, ramp: new[] { ElementPal.FireCore, ElementPal.FireMid, ElementPal.FireOuter });
			((ParticleProcessMaterial)embers.ProcessMaterial).Spread = 35f;
			Impact(layer, center, ElementPal.FireMid, s, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Medium);
			P3Fx.Ripple(layer, center, 620f * s, 0.6f, 0.035f);
		});
		Fx.FreeAfter(root, 1.6);
	}

	// ───────── 冰：Bufudyne —— 冰柱从地面破土而出、冻结、碎裂 ─────────
	public static void Bufudyne(Control layer, Vector2 center, Vector2 feet, float s = 1f)
	{
		Node2D root = Fx.Root(layer, feet, "P3Bufudyne");
		// 地面霜环
		Polygon2D frost = Fx.Poly(root, Fx.Circle(170f * s, 40, 0.3f), new Color(ElementPal.Frost, 0.35f), additive: true);
		Fx.Animate(frost, 0.2f, t => frost.Scale = Vector2.One * Ease.OutExpo(t));
		int n = 9;
		for (int i = 0; i < n; i++)
		{
			float u = i / (float)(n - 1) - 0.5f;
			float len = (260f - Mathf.Abs(u) * 260f + Fx.Rand(-20f, 30f)) * s + 90f * s;
			Node2D spike = new() { Position = new Vector2(u * 260f * s, Fx.Rand(-6f, 10f)), Rotation = u * 0.9f + Fx.Rand(-0.1f, 0.1f) };
			root.AddChild(spike);
			Fx.Poly(spike, Spike(len, 46f * s), new Color(ElementPal.Frost, 0.75f), additive: true);
			Fx.Poly(spike, Spike(len * 0.92f, 20f * s), new Color(Pal.Ice, 0.95f), additive: true);
			Fx.Line(spike, new[] { Vector2.Zero, new Vector2(0f, -len) }, Pal.White, 2f);
			float delay = 0.03f * Mathf.Abs(i - n / 2);
			spike.Scale = new Vector2(1f, 0.01f);
			Fx.Animate(spike, 0.9f, t =>
			{
				float time = t * 0.9f - delay;
				float grow = Ease.OutBack(Mathf.Clamp(time / 0.14f, 0f, 1f));
				spike.Scale = new Vector2(1f, Mathf.Max(grow, 0.01f));
				spike.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((time - 0.55f) / 0.25f, 0f, 1f)));
			});
		}
		Fx.After(root, 0.12, "bufu impact", () =>
		{
			Impact(layer, center, ElementPal.Frost, s, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Medium);
			Node2D mist = Fx.Root(layer, center, "P3FrostMist");
			Fx.Sparks(mist, Pal.Ice, (int)(50 * s), 80f * s, 380f * s, 0.9f, gravity: 60f, scaleMin: 0.3f, scaleMax: 0.9f);
			Fx.FreeAfter(mist, 1.2);
		});
		Fx.After(root, 0.62, "bufu shatter", () => P3Fx.GlassShatter(layer, center + new Vector2(0f, 40f * s), (int)(40 * s), 1.2f * s, ElementPal.Frost));
		Fx.FreeAfter(root, 1.1);
	}

	// ───────── 雷：Ziodyne —— 屏幕上方劈下的分叉闪电，闪三下 ─────────
	public static void Ziodyne(Control layer, Vector2 center, float s = 1f)
	{
		Vector2 sky = new(center.X + Fx.Rand(-80f, 80f), -40f);
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3Ziodyne");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		for (int flash = 0; flash < 3; flash++)
		{
			int k = flash;
			Fx.After(root, 0.07 * k, "zio bolt", () =>
			{
				Node2D bolt = new();
				root.AddChild(bolt);
				Vector2[] main = Jagged(sky, center, 14, 70f * s);
				Fx.Line(bolt, main, new Color(ElementPal.VoltGlow, 0.7f), 26f * s);
				Fx.Line(bolt, main, ElementPal.Volt, 9f * s);
				Fx.Line(bolt, main, Pal.White, 3f * s);
				for (int b = 0; b < 3; b++)
				{
					int idx = (int)Fx.Rand(3, 11);
					Vector2 start = main[idx];
					Vector2 end = start + new Vector2(Fx.Rand(-160f, 160f), Fx.Rand(60f, 180f)) * s;
					Fx.Line(bolt, Jagged(start, end, 6, 30f * s), new Color(ElementPal.Volt, 0.8f), 4f * s);
				}
				Fx.Animate(bolt, 0.12f, t => bolt.Modulate = new Color(1f, 1f, 1f, 1f - t));
				Fx.FreeAfter(bolt, 0.14);
				if (k == 0)
				{
					Impact(layer, center, ElementPal.Volt, s, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Strong);
					Node2D sparks = Fx.Root(layer, center, "P3ZioSparks");
					Fx.Sparks(sparks, ElementPal.Volt, (int)(60 * s), 300f * s, 900f * s, 0.45f, gravity: 600f, scaleMin: 0.1f, scaleMax: 0.3f);
					Fx.FreeAfter(sparks, 0.8);
				}
				else
				{
					P3Fx.Flash(layer, center, ElementPal.VoltGlow, 260f * s, 0.12f);
				}
			});
		}
		Fx.FreeAfter(root, 0.5);
	}

	// ───────── 光：Hamaon —— 脚下六芒法阵 + 光柱 + 升起的符纸 ─────────
	public static void Hamaon(Control layer, Vector2 center, Vector2 feet, float s = 1f)
	{
		Node2D root = Fx.Root(layer, feet, "P3Hamaon");
		Node2D seal = new() { Scale = new Vector2(1f, 0.32f) };
		root.AddChild(seal);
		float r = 190f * s;
		Fx.Line(seal, Fx.Circle(r, 64), ElementPal.HolyGold, 4f, closed: true);
		Fx.Line(seal, Fx.Circle(r * 0.86f, 64), new Color(ElementPal.HolyGold, 0.6f), 2f, closed: true);
		for (int tri = 0; tri < 2; tri++)
		{
			var pts = new Vector2[3];
			for (int i = 0; i < 3; i++)
			{
				pts[i] = Vector2.Up.Rotated(Mathf.Tau * i / 3f + tri * Mathf.Pi) * r * 0.84f;
			}
			Fx.Line(seal, pts, ElementPal.Holy, 3f, closed: true);
		}
		seal.Modulate = new Color(1f, 1f, 1f, 0f);
		Fx.Animate(seal, 1.0f, t =>
		{
			seal.Rotation = t * 1.2f;
			seal.Modulate = new Color(1f, 1f, 1f, Ease.Envelope(t, 0.12f, 0.7f));
		});
		Fx.After(root, 0.15, "hama pillar", () =>
		{
			P3Fx.LightPillar(layer, feet, ElementPal.HolyGold, 210f * s, 900f * s, 0.7f);
			Impact(layer, center, ElementPal.Holy, s, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Medium);
			for (int i = 0; i < 8; i++)
			{
				// 符纸：细长的白色长方形，旋转着上升
				Node2D ofuda = Fx.Root(layer, feet + new Vector2(Fx.Rand(-170f, 170f) * s, Fx.Rand(-20f, 10f)), "P3Ofuda");
				Fx.Poly(ofuda, new[] { new Vector2(-9f, -26f), new Vector2(9f, -26f), new Vector2(9f, 26f), new Vector2(-9f, 26f) } , new Color(Pal.White, 0.95f));
				Fx.Line(ofuda, new[] { new Vector2(0f, -18f), new Vector2(0f, 18f) }, new Color(ElementPal.CurseRed, 0.9f), 2f, additive: false);
				Vector2 start = ofuda.GlobalPosition;
				float rise = Fx.Rand(260f, 520f) * s, spin = Fx.Rand(-4f, 4f), d = Fx.Rand(0f, 0.15f);
				Fx.Animate(ofuda, 0.9f, t =>
				{
					float u = Mathf.Clamp(t * 0.9f - d, 0f, 1f);
					ofuda.GlobalPosition = start + new Vector2(Mathf.Sin(u * 9f) * 18f, -rise * Ease.OutCubic(u));
					ofuda.Rotation = spin * u;
					ofuda.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(u));
				});
				Fx.FreeAfter(ofuda, 1.0);
			}
		});
		Fx.FreeAfter(root, 1.1);
	}

	// ───────── 暗：Mudoon —— 血红咒阵 + 黑雾向内坍缩 + 十字血光 ─────────
	public static void Mudoon(Control layer, Vector2 center, float s = 1f)
	{
		Node2D root = Fx.Root(layer, center, "P3Mudoon");
		float r = 200f * s;
		Polygon2D dark = Fx.Poly(root, Fx.Circle(r, 48), new Color(0.03f, 0f, 0.06f, 0.75f));
		Line2D ring = Fx.Line(root, Fx.Circle(r, 48), new Color(ElementPal.CurseRed, 0.95f), 4f, closed: true);
		Line2D ring2 = Fx.Line(root, Fx.Circle(r * 0.7f, 6), new Color(ElementPal.Curse, 0.9f), 3f, closed: true);
		Fx.Animate(root, 0.9f, t =>
		{
			float time = t * 0.9f;
			float open = Ease.OutBack(Mathf.Clamp(time / 0.2f, 0f, 1f));
			float close = Ease.InCubic(Mathf.Clamp((time - 0.45f) / 0.25f, 0f, 1f));
			root.Scale = Vector2.One * Mathf.Max(open * (1f - close), 0.01f);
			ring2.Rotation = -time * 3f;
			ring.Rotation = time * 1.5f;
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((time - 0.6f) / 0.3f, 0f, 1f)));
		});
		// 黑雾坍缩：粒子从外圈吸向中心
		GpuParticles2D smoke = Fx.Sparks(root, new Color(0.15f, 0f, 0.2f), (int)(60 * s), 0f, 0f, 0.5f, scaleMin: 0.6f, scaleMax: 1.4f);
		smoke.Material = null;
		ParticleProcessMaterial pm = (ParticleProcessMaterial)smoke.ProcessMaterial;
		pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Ring;
		pm.EmissionRingRadius = r;
		pm.EmissionRingInnerRadius = r * 0.9f;
		pm.EmissionRingAxis = new Vector3(0f, 0f, 1f);
		pm.InitialVelocityMin = pm.InitialVelocityMax = 0f;
		pm.RadialVelocityMin = -460f * s;                   // 向圆心坍缩
		pm.RadialVelocityMax = -260f * s;
		Fx.After(root, 0.68, "mudo strike", () =>
		{
			Node2D cross = Fx.Root(layer, center, "P3MudoCross");
			Fx.Poly(cross, new[] { new Vector2(-14f, -260f), new Vector2(14f, -260f), new Vector2(14f, 260f), new Vector2(-14f, 260f) }, new Color(ElementPal.CurseRed, 0.9f), additive: true);
			Fx.Poly(cross, new[] { new Vector2(-150f, -80f), new Vector2(150f, -80f), new Vector2(150f, -52f), new Vector2(-150f, -52f) }, new Color(ElementPal.CurseRed, 0.9f), additive: true);
			cross.Scale = Vector2.One * s;
			Fx.Animate(cross, 0.4f, t =>
			{
				cross.Scale = new Vector2(s * (1f - 0.6f * t), s * (1f + 0.4f * Ease.OutExpo(t)));
				cross.Modulate = new Color(1f, 1f, 1f, 1f - t);
			});
			Fx.FreeAfter(cross, 0.45);
			Impact(layer, center, ElementPal.CurseRed, s, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Strong);
		});
		Fx.FreeAfter(root, 1.0);
	}

	// ───────── 万能：Megidolaon —— 收缩的白点 → 膨胀的穹顶 → 三层冲击波 ─────────
	public static void Megidolaon(Control layer, Vector2 center, float s = 1f)
	{
		Node2D root = Fx.Root(layer, center, "P3Megido");
		Polygon2D core = Fx.Poly(root, Fx.Circle(60f, 40), Pal.White, additive: true);
		Polygon2D dome = Fx.Poly(root, Fx.Circle(60f, 48), new Color(ElementPal.Almighty, 0.55f), additive: true);
		Line2D rim = Fx.Line(root, Fx.Circle(60f, 48), new Color(Pal.White, 0.9f), 5f, closed: true);
		Fx.Animate(root, 0.9f, t =>
		{
			float time = t * 0.9f;
			float charge = 1f - Ease.InCubic(Mathf.Clamp(time / 0.15f, 0f, 1f)) * 0.7f;   // 先收缩
			float blast = Ease.OutExpo(Mathf.Clamp((time - 0.15f) / 0.35f, 0f, 1f));
			core.Scale = Vector2.One * s * (time < 0.15f ? charge : 0.3f + 2.2f * blast);
			dome.Scale = rim.Scale = Vector2.One * s * (time < 0.15f ? 0.2f : 0.2f + 5.5f * blast);
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((time - 0.45f) / 0.45f, 0f, 1f)));
		});
		Fx.After(root, 0.15, "megido blast", () =>
		{
			Impact(layer, center, ElementPal.Almighty, s * 1.4f, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Strong);
			P3Fx.Shockwave(layer, center, Pal.White, 1100f * s, 0.7f, delay: 0.08f);
			P3Fx.Shockwave(layer, center, Pal.Cyan, 1400f * s, 0.85f, delay: 0.16f);
			P3Fx.Ripple(layer, center, 1200f * s, 0.9f, 0.05f);
			P3Fx.SpeedLines(layer, center, Pal.White, 26, 120f * s, 900f * s, 0.45f);
		});
		Fx.FreeAfter(root, 1.0);
	}
}
