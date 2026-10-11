using Godot;

namespace P3Makoto.Vfx;

/// <summary>命中元素。参照 P3 技能体系。</summary>
public enum Element
{
	None,
	Slash,
	Pierce,
	Strike,
	Agi,
	Bufu,
	Zio,
	Hama,
	Mudo,
	Megido,
	Meteor,
	Star,
	Royal
}

public static class ElementPal
{
	public static readonly Color FireCore = new("fff3b0");
	public static readonly Color FireMid = new("ff8a2a");
	public static readonly Color FireOuter = new("ff3b1f");
	public static readonly Color Frost = new("9fe6ff");
	public static readonly Color Volt = new("fff26b");
	public static readonly Color VoltGlow = new("7fb8ff");
	public static readonly Color Holy = new("fff2c4");
	public static readonly Color HolyGold = new("ffd36b");
	public static readonly Color Curse = new("b02bff");
	public static readonly Color CurseRed = new("ff2a4a");
	public static readonly Color Almighty = new("c9a8ff");
}

/// <summary>
/// 命中特效：每一击伤害时在目标身上播放。scale 用于多段攻击时缩小单次表现。
/// center = 目标中心，feet = 目标脚底。
/// </summary>
public static class Elements
{
	public static void Hit(Element e, Control layer, Vector2 center, Vector2 feet, float scale = 1f)
	{
		// v0.9：全部换成"大胆版"（Bold / Motifs）—— 尺寸大、层次多、自带确认感
		switch (e)
		{
			case Element.Slash: Motifs.Cleave(layer, center, Pal.Cyan, 0.85f * scale, Fx.Rand(-0.75f, -0.4f)); break;
			case Element.Royal: Motifs.Cleave(layer, center, Pal.Royal, 1.15f * scale, -0.6f); break;
			case Element.Pierce: Motifs.Thrust(layer, center, Pal.Cyan, scale); break;
			case Element.Strike: Motifs.Smash(layer, center, feet, Pal.Royal, scale, crack: scale >= 0.9f); break;
			case Element.Agi: Bold.Agidyne(layer, center, feet, scale); break;
			case Element.Bufu: Bold.Bufudyne(layer, center, feet, scale); break;
			case Element.Zio: Bold.Ziodyne(layer, center, scale); break;
			case Element.Hama: Bold.Hamaon(layer, center, feet, scale); break;
			case Element.Mudo: Bold.Mudoon(layer, center, scale); break;
			case Element.Megido: Bold.Megidolaon(layer, center, scale); break;
			case Element.Meteor: Motifs.Meteor(layer, center, Pal.Cyan, scale); break;
			case Element.Star:
				Star(layer, center, scale * 1.3f);
				Bold.Impact(layer, center, Pal.Ice, 0.7f * scale, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Weak);
				break;
		}
	}

	// ───────── 物理 ─────────

	/// <summary>新月形斩痕：弧形扫过，粗中间细两端，带冷色辉光和白色刃芯。</summary>
	public static void Slash(Control layer, Vector2 at, Color tint, float scale = 1f, bool heavy = false)
	{
		Node2D root = Fx.Root(layer, at, "P3Slash");
		root.Rotation = Fx.Rand(-0.5f, 0.3f) + (Fx.Rng.Next(2) == 0 ? 0f : Mathf.Pi);
		float r = 170f * scale;
		Polygon2D glow = Fx.Poly(root, new Vector2[3], new Color(tint, 0.85f), additive: true);
		Polygon2D core = Fx.Poly(root, new Vector2[3], Pal.White, additive: true);
		const float a0 = -1.9f;
		const float a1 = 1.1f;
		float dur = heavy ? 0.5f : 0.38f;
		Fx.Animate(root, dur, t =>
		{
			float head = Ease.OutExpo(Mathf.Min(1f, t * 3.2f));
			float tail = Ease.InCubic(t);
			float from = Mathf.Lerp(a0, a1, tail);
			float to = Mathf.Lerp(a0, a1, head);
			Fx.Reshape(glow, Crescent(r, r * 0.16f * (heavy ? 1.6f : 1f), from, to, a0, a1));
			Fx.Reshape(core, Crescent(r * 0.985f, r * 0.05f, from, to, a0, a1));
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
		});
		P3Fx.Flash(layer, at, Pal.Ice, 220f * scale, 0.16f, delay: 0.03f);
		Fx.Sparks(root, tint, (int)(24 * scale) + 6, 220f * scale, 640f * scale, 0.45f, gravity: 220f, scaleMin: 0.1f, scaleMax: 0.3f);
		if (heavy)
		{
			P3Fx.Ripple(layer, at, 520f * scale, 0.55f, 0.03f, delay: 0.04f);
			P3Fx.SpeedLines(layer, at, tint, 10, 60f * scale, 300f * scale, 0.3f, delay: 0.03f);
		}
		Fx.FreeAfter(root, dur + 0.5);
	}

	/// <summary>
	/// 斩痕：一道贯穿目标的细长白线，先极快地亮起，再沿垂直方向裂成两条向两侧滑开并淡去（"已经被砍了"）。
	/// </summary>
	public static void SlashScar(Control layer, Vector2 at, float angle, float scale = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3SlashScar");
		root.Rotation = angle;
		float half = 230f * scale;
		Curve taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0f));
		taper.AddPoint(new Vector2(0.5f, 1f));
		taper.AddPoint(new Vector2(1f, 0f));
		Line2D[] halves = new Line2D[2];
		for (int i = 0; i < 2; i++)
		{
			halves[i] = Fx.Line(root, new[] { new Vector2(-half, 0f), new Vector2(half, 0f) }, Pal.White, 7f * scale);
			halves[i].WidthCurve = taper;
		}
		Line2D glow = Fx.Line(root, new[] { new Vector2(-half, 0f), new Vector2(half, 0f) }, new Color(Pal.Cyan, 0.6f), 26f * scale);
		glow.WidthCurve = taper;
		Fx.Animate(root, 0.42f, t =>
		{
			float reveal = Ease.OutExpo(Mathf.Min(1f, t * 6f));
			float split = Ease.OutCubic(Ease.Segment(t, 0.2f, 0.8f));
			float fade = 1f - Ease.InCubic(Ease.Segment(t, 0.25f, 0.75f));
			for (int i = 0; i < 2; i++)
			{
				float off = (i == 0 ? -1f : 1f) * split * 10f * scale;
				halves[i].Points = new[] { new Vector2(-half * reveal, off), new Vector2(half * reveal, off) };
				halves[i].Modulate = new Color(1f, 1f, 1f, fade);
			}
			glow.Points = new[] { new Vector2(-half * reveal, 0f), new Vector2(half * reveal, 0f) };
			glow.Modulate = new Color(1f, 1f, 1f, fade * (1f - split));
		});
		Fx.FreeAfter(root, 0.5);
	}

	/// <summary>月牙多边形：沿圆弧，厚度在整条弧的中点最大、两端为零。</summary>
	private static Vector2[] Crescent(float radius, float thickness, float from, float to, float full0, float full1, int seg = 20)
	{
		if (to - from < 0.01f)
		{
			return new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero };
		}
		Vector2[] pts = new Vector2[(seg + 1) * 2];
		for (int i = 0; i <= seg; i++)
		{
			float a = Mathf.Lerp(from, to, (float)i / seg);
			float u = (a - full0) / (full1 - full0);
			float th = Mathf.Sin(Mathf.Clamp(u, 0f, 1f) * Mathf.Pi) * thickness;
			Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
			pts[i] = dir * radius;
			pts[pts.Length - 1 - i] = dir * (radius - th);
		}
		return pts;
	}

	/// <summary>贯穿：数道平行光矛从左向右穿透目标。</summary>
	public static void Pierce(Control layer, Vector2 at, Color tint, float scale = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3Pierce");
		root.Rotation = Fx.Rand(-0.25f, 0.1f);
		int lanes = 5;
		for (int i = 0; i < lanes; i++)
		{
			float y = (i - (lanes - 1) / 2f) * 16f * scale + Fx.Rand(-4f, 4f);
			float len = Fx.Rand(260f, 380f) * scale;
			float delay = Mathf.Abs(i - (lanes - 1) / 2f) * 0.025f;
			Line2D line = Fx.Line(root, new[] { new Vector2(-len, y), new Vector2(-len, y) }, i == lanes / 2 ? Pal.White : tint, i == lanes / 2 ? 6f * scale : 3f * scale);
			Fx.Animate(line, 0.3f + delay, t =>
			{
				float local = Ease.Segment(t, delay / (0.3f + delay), 0.3f / (0.3f + delay));
				float head = Ease.OutExpo(Mathf.Min(1f, local * 2.2f));
				float tail = Ease.InCubic(local);
				line.Points = new[] { new Vector2(Mathf.Lerp(-len, len, tail), y), new Vector2(Mathf.Lerp(-len, len, head), y) };
				line.Modulate = new Color(1f, 1f, 1f, local <= 0f ? 0f : 1f);
			});
		}
		P3Fx.Flash(layer, at, Pal.Ice, 200f * scale, 0.15f, delay: 0.04f);
		P3Fx.Shockwave(layer, at, tint, 260f * scale, 0.35f, delay: 0.04f);
		Fx.Sparks(root, tint, 20, 300f * scale, 700f * scale, 0.35f);
		Fx.FreeAfter(root, 0.8);
	}

	/// <summary>重击：厚重冲击环 + 放射裂纹 + 强折射。</summary>
	public static void Strike(Control layer, Vector2 at, float scale = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3Strike");
		P3Fx.Flash(layer, at, Pal.White, 300f * scale, 0.14f);
		P3Fx.Ripple(layer, at, 620f * scale, 0.6f, 0.045f);
		P3Fx.Shockwave(layer, at, Pal.Cyan, 380f * scale, 0.4f);
		P3Fx.Shockwave(layer, at, Pal.Ice, 240f * scale, 0.3f, delay: 0.05f);
		for (int i = 0; i < 7; i++)
		{
			Vector2 dir = Vector2.Right.Rotated(Mathf.Tau * i / 7f + Fx.Rand(-0.3f, 0.3f));
			Vector2[] crack = new Vector2[4];
			float len = Fx.Rand(70f, 130f) * scale;
			for (int k = 0; k < 4; k++)
			{
				crack[k] = dir * len * (k + 1) / 4f + dir.Orthogonal() * Fx.Rand(-10f, 10f) * scale;
			}
			Line2D line = Fx.Line(root, crack, Pal.Ice, 3f * scale);
			Fx.Animate(line, 0.45f, t =>
			{
				line.Scale = Vector2.One * Ease.OutExpo(Mathf.Min(1f, t * 4f));
				line.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
			});
		}
		Fx.Sparks(root, Pal.Ice, 30, 200f * scale, 520f * scale, 0.5f, gravity: 500f);
		Fx.FreeAfter(root, 0.9);
	}

	// ───────── 魔法 ─────────

	/// <summary>Agi：脚下喷涌的火焰 + 热浪折射 + 火星。</summary>
	public static void Agi(Control layer, Vector2 center, Vector2 feet, float scale = 1f)
	{
		Node2D root = Fx.Root(layer, feet, "P3Agi");
		Color[] ramp = { ElementPal.FireCore, ElementPal.FireMid, ElementPal.FireOuter };
		GpuParticles2D flame = Fx.Sparks(root, Colors.White, 60, 80f * scale, 320f * scale, 0.7f, gravity: -700f * scale, scaleMin: 0.9f * scale, scaleMax: 2.2f * scale, ramp: ramp);
		ParticleProcessMaterial pm = (ParticleProcessMaterial)flame.ProcessMaterial;
		pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		pm.EmissionBoxExtents = new Vector3(70f * scale, 6f, 0f);
		pm.Spread = 20f;
		flame.Explosiveness = 0.75f;
		Fx.Sparks(root, ElementPal.FireMid, 30, 150f * scale, 500f * scale, 0.8f, gravity: -200f, scaleMin: 0.08f, scaleMax: 0.2f);
		P3Fx.Flash(layer, center, ElementPal.FireMid, 340f * scale, 0.25f);
		P3Fx.Ripple(layer, center, 480f * scale, 0.7f, 0.025f);
		P3Fx.Shockwave(layer, feet, ElementPal.FireOuter, 420f * scale, 0.5f, squashY: 0.3f);
		Fx.FreeAfter(root, 1.2);
	}

	/// <summary>Bufu：冰晶从地面依次刺出，停顿后碎裂成冰屑。</summary>
	public static void Bufu(Control layer, Vector2 center, Vector2 feet, float scale = 1f)
	{
		Node2D root = Fx.Root(layer, feet, "P3Bufu");
		int count = 7;
		for (int i = 0; i < count; i++)
		{
			float x = (i - (count - 1) / 2f) * 30f * scale + Fx.Rand(-8f, 8f);
			float h = (150f - Mathf.Abs(i - (count - 1) / 2f) * 28f) * scale * Fx.Rand(0.8f, 1.15f);
			float w = h * 0.22f;
			Node2D spike = new Node2D { Position = new Vector2(x, 0f), Rotation = x * 0.004f / scale };
			root.AddChild(spike);
			Vector2[] shape = { new(-w * 0.5f, 0f), new(-w * 0.15f, -h * 0.75f), new(0f, -h), new(w * 0.2f, -h * 0.7f), new(w * 0.5f, 0f) };
			Fx.Poly(spike, shape, new Color(ElementPal.Frost, 0.45f), additive: true);
			Fx.Line(spike, shape, Pal.White, 1.6f, closed: true);
			Fx.Line(spike, new[] { new Vector2(0f, -h), new Vector2(0f, 0f) }, new Color(Pal.White, 0.6f), 1f);
			float delay = Mathf.Abs(i - (count - 1) / 2f) * 0.03f;
			spike.Scale = new Vector2(1f, 0f);
			Fx.Animate(spike, 0.75f, t =>
			{
				float grow = Ease.OutBack(Ease.Segment(t, delay / 0.75f, 0.18f / 0.75f));
				spike.Scale = new Vector2(1f, grow);
				spike.Modulate = new Color(1f, 1f, 1f, t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f);
			});
		}
		P3Fx.Flash(layer, center, ElementPal.Frost, 300f * scale, 0.2f);
		P3Fx.Ripple(layer, center, 460f * scale, 0.6f, 0.03f);
		Fx.After(layer, 0.5, "bufu shatter", () => P3Fx.GlassShatter(layer, feet + new Vector2(0f, -70f * scale), (int)(18 * scale) + 6, 0.7f * scale, ElementPal.Frost));
		Fx.Sparks(root, ElementPal.Frost, 40, 100f * scale, 400f * scale, 0.9f, gravity: 80f, scaleMin: 0.08f, scaleMax: 0.22f);
		Fx.FreeAfter(root, 1.2);
	}

	/// <summary>Zio：从天而降的锯齿闪电，闪烁三次并分叉。</summary>
	public static void Zio(Control layer, Vector2 at, float scale = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3Zio");
		float top = -at.Y - 40f;
		Line2D glow = Fx.Line(root, new Vector2[2], new Color(ElementPal.VoltGlow, 0.6f), 22f * scale);
		Line2D core = Fx.Line(root, new Vector2[2], Pal.White, 5f * scale);
		Line2D branch = Fx.Line(root, new Vector2[2], new Color(ElementPal.Volt, 0.9f), 3f * scale);
		int lastStep = -1;
		Fx.Animate(root, 0.42f, t =>
		{
			int step = (int)(t * 7f);
			if (step != lastStep)
			{
				lastStep = step;
				Vector2[] bolt = Bolt(new Vector2(Fx.Rand(-60f, 60f), top), Vector2.Zero, 6, 70f * scale);
				glow.Points = core.Points = bolt;
				int from = Fx.Rng.Next(2, bolt.Length - 2);
				branch.Points = Bolt(bolt[from], bolt[from] + new Vector2(Fx.Rand(-140f, 140f), Fx.Rand(60f, 160f)) * scale, 3, 30f * scale);
			}
			float flicker = (lastStep % 2 == 0 ? 1f : 0.45f) * (1f - Ease.InCubic(t));
			root.Modulate = new Color(1f, 1f, 1f, flicker);
		});
		P3Fx.Flash(layer, at, ElementPal.Volt, 360f * scale, 0.2f);
		P3Fx.Shockwave(layer, at, ElementPal.VoltGlow, 360f * scale, 0.4f);
		P3Fx.Ripple(layer, at, 420f * scale, 0.5f, 0.03f);
		Fx.Sparks(root, ElementPal.Volt, 34, 300f * scale, 800f * scale, 0.35f, gravity: 600f, scaleMin: 0.08f, scaleMax: 0.2f);
		Fx.FreeAfter(root, 0.8);
	}

	/// <summary>中点位移法生成锯齿折线。</summary>
	private static Vector2[] Bolt(Vector2 from, Vector2 to, int depth, float jitter)
	{
		System.Collections.Generic.List<Vector2> pts = new() { from, to };
		for (int d = 0; d < depth && pts.Count < 64; d++)
		{
			System.Collections.Generic.List<Vector2> next = new();
			for (int i = 0; i < pts.Count - 1; i++)
			{
				Vector2 a = pts[i];
				Vector2 b = pts[i + 1];
				Vector2 mid = (a + b) / 2f + (b - a).Normalized().Orthogonal() * Fx.Rand(-jitter, jitter);
				next.Add(a);
				next.Add(mid);
			}
			next.Add(pts[^1]);
			pts = next;
			jitter *= 0.55f;
		}
		return pts.ToArray();
	}

	/// <summary>Hama：六芒星印 + 金白光柱 + 旋转光芒。</summary>
	public static void Hama(Control layer, Vector2 center, Vector2 feet, float scale = 1f)
	{
		Node2D root = Fx.Root(layer, center, "P3Hama");
		Node2D sigil = new Node2D();
		root.AddChild(sigil);
		float r = 110f * scale;
		for (int k = 0; k < 2; k++)
		{
			Vector2[] tri = new Vector2[3];
			for (int i = 0; i < 3; i++)
			{
				float a = Mathf.Tau * i / 3f - Mathf.Pi / 2f + k * Mathf.Pi;
				tri[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
			}
			Fx.Line(sigil, tri, ElementPal.HolyGold, 2.5f * scale, closed: true);
		}
		Fx.Line(sigil, Fx.Circle(r * 1.08f, 40), ElementPal.Holy, 2f * scale, closed: true);
		Node2D rays = new Node2D();
		root.AddChild(rays);
		for (int i = 0; i < 12; i++)
		{
			float a = Mathf.Tau * i / 12f;
			Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
			Fx.Poly(rays, new[] { d * 30f * scale + d.Orthogonal() * 4f * scale, d * 260f * scale, d * 30f * scale - d.Orthogonal() * 4f * scale }, new Color(ElementPal.Holy, 0.5f), additive: true);
		}
		Fx.Animate(root, 0.8f, t =>
		{
			float e = Ease.OutExpo(Mathf.Min(1f, t * 2.5f));
			sigil.Scale = Vector2.One * Mathf.Lerp(1.8f, 1f, e);
			sigil.Rotation = Mathf.Lerp(-0.8f, 0.2f, Ease.OutCubic(t));
			rays.Rotation = t * 0.6f;
			rays.Scale = Vector2.One * Mathf.Lerp(0.3f, 1.2f, e);
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
		});
		P3Fx.LightPillar(layer, feet, ElementPal.HolyGold, 160f * scale, (feet.Y - center.Y) * 3f, 0.8f);
		P3Fx.Flash(layer, center, ElementPal.Holy, 380f * scale, 0.3f);
		Fx.Sparks(root, ElementPal.HolyGold, 30, 60f, 220f * scale, 1.0f, gravity: -200f, scaleMin: 0.1f, scaleMax: 0.3f);
		Fx.FreeAfter(root, 1.1);
	}

	/// <summary>Mudo：暗球吸入坍缩，随后爆出紫红碎光。</summary>
	public static void Mudo(Control layer, Vector2 at, float scale = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3Mudo");
		Polygon2D orb = Fx.Poly(root, Fx.Circle(90f * scale, 40), new Color(0.02f, 0f, 0.05f, 0.85f));
		Line2D rim = Fx.Line(root, Fx.Circle(90f * scale, 40), ElementPal.Curse, 3f * scale, closed: true);
		P3Fx.Gather(layer, at, ElementPal.CurseRed, 420f * scale, 0.22f);
		Fx.Animate(root, 0.55f, t =>
		{
			float grow = Ease.OutExpo(Mathf.Min(1f, t * 4f));
			float collapse = Ease.InExpo(Ease.Segment(t, 0.25f, 0.2f));
			float s = grow * (1f - collapse);
			orb.Scale = rim.Scale = Vector2.One * Mathf.Max(0.001f, s);
			rim.Rotation = t * 4f;
		});
		Fx.After(layer, 0.25, "mudo burst", () =>
		{
			P3Fx.Flash(layer, at, ElementPal.CurseRed, 340f * scale, 0.22f);
			P3Fx.Ripple(layer, at, 600f * scale, 0.65f, 0.05f);
			P3Fx.Shockwave(layer, at, ElementPal.Curse, 460f * scale, 0.5f);
			P3Fx.Shockwave(layer, at, ElementPal.CurseRed, 300f * scale, 0.45f, delay: 0.06f);
			Fx.Sparks(root, ElementPal.Curse, 40, 250f * scale, 700f * scale, 0.6f, gravity: 0f, scaleMin: 0.12f, scaleMax: 0.35f);
		});
		Fx.FreeAfter(root, 1.1);
	}

	/// <summary>Megido：光球从四周汇聚，收束后爆发万能属性的白紫光。</summary>
	public static void Megido(Control layer, Vector2 at, float scale = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3Megido");
		int orbs = 6;
		const float gather = 0.26f;
		for (int i = 0; i < orbs; i++)
		{
			Vector2 start = Vector2.Right.Rotated(Mathf.Tau * i / orbs + Fx.Rand(-0.2f, 0.2f)) * Fx.Rand(170f, 230f) * scale;
			Sprite2D orb = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = ElementPal.Almighty, Position = start, Scale = Vector2.One * 1.8f * scale };
			root.AddChild(orb);
			Fx.Animate(orb, gather, t =>
			{
				float e = Ease.InExpo(t);
				orb.Position = start.Lerp(Vector2.Zero, e).Rotated(t * 0.8f);
				orb.Scale = Vector2.One * Mathf.Lerp(1.8f, 0.9f, e) * scale;
			}).TweenCallback(Callable.From(orb.QueueFree));
		}
		Fx.After(layer, gather, "megido burst", () =>
		{
			P3Fx.Flash(layer, at, Pal.White, 520f * scale, 0.3f);
			P3Fx.Ripple(layer, at, 820f * scale, 0.8f, 0.055f);
			P3Fx.Shockwave(layer, at, ElementPal.Almighty, 640f * scale, 0.6f);
			P3Fx.Shockwave(layer, at, Pal.Cyan, 420f * scale, 0.5f, delay: 0.07f);
			P3Fx.SpeedLines(layer, at, ElementPal.Almighty, 16, 80f * scale, 460f * scale, 0.4f);
			Fx.Sparks(root, ElementPal.Almighty, 50, 300f * scale, 900f * scale, 0.6f, scaleMin: 0.1f, scaleMax: 0.35f);
		});
		Fx.FreeAfter(root, gather + 1.0);
	}

	/// <summary>流星：从左上天空斜坠的光核 + 拖尾，落点爆炸。</summary>
	public static void Meteor(Control layer, Vector2 at, float scale = 1f)
	{
		Vector2 from = at + new Vector2(-380f, -760f) * Mathf.Max(0.6f, scale);
		Node2D root = Fx.Root(layer, from, "P3Meteor");
		Sprite2D headGlow = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = Pal.Cyan, Scale = Vector2.One * 3.2f * scale };
		Sprite2D headCore = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = Pal.White, Scale = Vector2.One * 1.3f * scale };
		root.AddChild(headGlow);
		root.AddChild(headCore);
		Line2D trail = new Line2D
		{
			Width = 26f * scale,
			Material = Fx.Additive,
			Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { new Color(Pal.Royal, 0f), new Color(Pal.Ice, 1f) } },
			TopLevel = true
		};
		layer.AddChild(trail);
		Curve taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0f));
		taper.AddPoint(new Vector2(1f, 1f));
		trail.WidthCurve = taper;
		const float travel = 0.16f;
		Fx.Animate(root, travel, t =>
		{
			Vector2 p = from.Lerp(at, Ease.InCubic(t));
			root.GlobalPosition = p;
			trail.Points = new[] { from.Lerp(at, Ease.InCubic(Mathf.Max(0f, t - 0.45f))), p };
		});
		Fx.After(layer, travel, "meteor impact", () =>
		{
			root.Visible = false;
			Fx.Animate(trail, 0.18f, t => trail.Modulate = new Color(1f, 1f, 1f, 1f - t)).TweenCallback(Callable.From(trail.QueueFree));
			P3Fx.Flash(layer, at, Pal.Ice, 380f * scale, 0.22f);
			P3Fx.Ripple(layer, at, 600f * scale, 0.6f, 0.045f);
			P3Fx.Shockwave(layer, at, Pal.Cyan, 460f * scale, 0.45f);
			Node2D burst = Fx.Root(layer, at, "P3MeteorBurst");
			Fx.Sparks(burst, Pal.Cyan, 46, 250f * scale, 800f * scale, 0.6f, gravity: 500f, scaleMin: 0.1f, scaleMax: 0.35f);
			Fx.FreeAfter(burst, 1.0);
		});
		Fx.FreeAfter(root, travel + 0.1);
	}

	/// <summary>星辉：四角星闪现旋转，向外迸出小星。</summary>
	public static void Star(Control layer, Vector2 at, float scale = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3Star");
		Polygon2D big = Fx.Poly(root, Fx.Star4(90f * scale), new Color(Pal.Ice, 0.95f), additive: true);
		Polygon2D halo = Fx.Poly(root, Fx.Star4(150f * scale, 0.12f), new Color(Pal.Cyan, 0.5f), additive: true);
		Fx.Animate(root, 0.5f, t =>
		{
			float e = Ease.OutBack(Mathf.Min(1f, t * 3f));
			big.Scale = Vector2.One * e * (1f - Ease.InCubic(t));
			halo.Scale = Vector2.One * Mathf.Lerp(0.5f, 1.4f, Ease.OutExpo(t));
			halo.Modulate = new Color(1f, 1f, 1f, 1f - t);
			root.Rotation = Ease.OutCubic(t) * 0.8f;
		});
		for (int i = 0; i < 6; i++)
		{
			Vector2 dir = Vector2.Right.Rotated(Mathf.Tau * i / 6f + Fx.Rand(-0.3f, 0.3f));
			Node2D mini = new Node2D();
			root.AddChild(mini);
			Fx.Poly(mini, Fx.Star4(16f * scale), Pal.White, additive: true);
			float dist = Fx.Rand(110f, 190f) * scale;
			Fx.Animate(mini, 0.55f, t =>
			{
				mini.Position = dir * dist * Ease.OutExpo(t);
				mini.Rotation = t * 3f;
				mini.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
			});
		}
		P3Fx.Shockwave(layer, at, Pal.Cyan, 300f * scale, 0.4f);
		P3Fx.Ripple(layer, at, 360f * scale, 0.45f, 0.025f);
		Fx.FreeAfter(root, 0.8);
	}
}
