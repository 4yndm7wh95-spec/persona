using Godot;

namespace P3Makoto.Vfx;

/// <summary>P3R 调色板，见 docs/STYLE_P3R.md。</summary>
public static class Pal
{
	public static readonly Color Abyss = new("071a4a");
	public static readonly Color Royal = new("1f5cff");
	public static readonly Color Cyan = new("39d8ff");
	public static readonly Color Ice = new("d8f4ff");
	public static readonly Color DarkHour = new("6dff9e");
	public static readonly Color Blood = new("ff2a4a");
	public static readonly Color White = new(1f, 1f, 1f);

	// 旧名字保留给其他文件使用
	public static Color Blue => Cyan;
}

/// <summary>
/// P3R 风格特效积木。所有方法都自动释放自己创建的节点，返回节点的方法除外（由调用方 Dismiss）。
/// 时间轴约定：预备 → 爆发 → 余韵，缓动默认 OutExpo。
/// </summary>
public static class P3Fx
{
	// ───────────────────────── 基础元素 ─────────────────────────

	/// <summary>水面折射冲击波。</summary>
	public static void Ripple(Control layer, Vector2 at, float size = 560f, float duration = 0.7f, float strength = 0.03f, float squashY = 1f, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P3Ripple");
		ColorRect quad = Fx.ShaderQuad(root, Shaders.Refract, size, squashY);
		Fx.SetParam(quad, "strength", strength);
		Fx.SetParam(quad, "alpha", 0f);
		Fx.Animate(quad, duration + delay, t =>
		{
			float local = Ease.Segment(t, delay / (duration + delay), duration / (duration + delay));
			float e = Ease.OutCubic(local);
			Fx.SetParam(quad, "radius", Mathf.Lerp(0.02f, 0.9f, e));
			Fx.SetParam(quad, "width", Mathf.Lerp(0.16f, 0.07f, e));
			Fx.SetParam(quad, "alpha", local <= 0f ? 0f : (1f - local) * (1f - local));
		});
		Fx.FreeAfter(root, duration + delay + 0.1);
	}

	/// <summary>细光环冲击波（加法发光）。</summary>
	public static void Shockwave(Control layer, Vector2 at, Color tint, float size = 520f, float duration = 0.55f, float squashY = 1f, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P3Shockwave");
		ColorRect quad = Fx.ShaderQuad(root, Shaders.Ring, size, squashY);
		Fx.SetParam(quad, "tint", tint);
		Fx.SetParam(quad, "alpha", 0f);
		float total = duration + delay;
		Fx.Animate(quad, total, t =>
		{
			float local = Ease.Segment(t, delay / total, duration / total);
			float e = Ease.OutExpo(local);
			Fx.SetParam(quad, "radius", Mathf.Lerp(0.04f, 0.95f, e));
			Fx.SetParam(quad, "width", Mathf.Lerp(0.05f, 0.008f, e));
			Fx.SetParam(quad, "alpha", local <= 0f ? 0f : 1f - Ease.InCubic(local));
		});
		Fx.FreeAfter(root, total + 0.1);
	}

	/// <summary>向内收缩的蓄力环（预备段）。</summary>
	public static void Gather(Control layer, Vector2 at, Color tint, float size = 300f, float duration = 0.14f)
	{
		Node2D root = Fx.Root(layer, at, "P3Gather");
		ColorRect quad = Fx.ShaderQuad(root, Shaders.Ring, size);
		Fx.SetParam(quad, "tint", tint);
		Fx.Animate(quad, duration, t =>
		{
			float e = Ease.InCubic(t);
			Fx.SetParam(quad, "radius", Mathf.Lerp(0.9f, 0.05f, e));
			Fx.SetParam(quad, "width", 0.02f);
			Fx.SetParam(quad, "alpha", Mathf.Lerp(0.2f, 1f, t));
		});
		Fx.FreeAfter(root, duration + 0.05);
	}

	/// <summary>径向闪光。</summary>
	public static void Flash(Control layer, Vector2 at, Color tint, float size = 300f, float duration = 0.25f, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P3Flash");
		ColorRect quad = Fx.ShaderQuad(root, Shaders.Flash, size);
		Fx.SetParam(quad, "tint", tint);
		Fx.SetParam(quad, "alpha", 0f);
		float total = duration + delay;
		Fx.Animate(quad, total, t =>
		{
			float local = Ease.Segment(t, delay / total, duration / total);
			Fx.SetParam(quad, "alpha", local <= 0f ? 0f : 1f - Ease.OutCubic(local));
			quad.Scale = Vector2.One * Mathf.Lerp(0.5f, 1.25f, Ease.OutExpo(local));
		});
		Fx.FreeAfter(root, total + 0.1);
	}

	private static Curve? _taper;

	/// <summary>两头尖、中间粗的线宽曲线。</summary>
	private static Curve Taper
	{
		get
		{
			if (_taper == null)
			{
				_taper = new Curve();
				_taper.AddPoint(new Vector2(0f, 0f));
				_taper.AddPoint(new Vector2(0.35f, 1f));
				_taper.AddPoint(new Vector2(1f, 0f));
			}
			return _taper;
		}
	}

	/// <summary>放射状速度线：外端快速伸出，内端随后追上，整条线收束消失。</summary>
	public static void SpeedLines(Control layer, Vector2 at, Color tint, int count = 14, float inner = 50f, float outer = 380f, float duration = 0.38f, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P3SpeedLines");
		float total = duration + delay + 0.08f;
		for (int i = 0; i < count; i++)
		{
			float ang = Mathf.Tau * i / count + Fx.Rand(-0.12f, 0.12f);
			Vector2 dir = Vector2.Right.Rotated(ang);
			float reach = outer * Fx.Rand(0.6f, 1.1f);
			float lag = Fx.Rand(0f, 0.08f);
			Line2D line = Fx.Line(root, new[] { dir * inner, dir * inner }, i % 3 == 0 ? Pal.White : tint, Fx.Rand(2f, 5f));
			line.WidthCurve = Taper;
			Fx.Animate(line, total, t =>
			{
				float local = Ease.Segment(t, (delay + lag) / total, duration / total);
				float head = Ease.OutExpo(Mathf.Min(1f, local * 1.6f));
				float tail = Ease.InCubic(local);
				line.Points = new[] { dir * Mathf.Lerp(inner, reach, tail), dir * Mathf.Lerp(inner, reach, head) };
				line.Modulate = new Color(1f, 1f, 1f, local <= 0f ? 0f : 1f - tail);
			});
		}
		Fx.FreeAfter(root, total + 0.1);
	}

	/// <summary>
	/// 玻璃碎裂：细长碎片带阻尼迸射，旋转时随角度闪烁棱光。
	/// </summary>
	public static void GlassShatter(Control layer, Vector2 at, int count = 30, float power = 1f, Color? tint = null)
	{
		Color glass = tint ?? Pal.Ice;
		Node2D root = Fx.Root(layer, at, "P3GlassShatter");
		const float duration = 1.0f;
		for (int i = 0; i < count; i++)
		{
			float len = Fx.Rand(10f, 30f) * power;
			float wid = len * Fx.Rand(0.25f, 0.55f);
			Vector2[] shape =
			{
				new(-len * 0.5f, 0f),
				new(-len * 0.1f, -wid * 0.5f),
				new(len * 0.5f, Fx.Rand(-wid * 0.2f, wid * 0.2f)),
				new(len * 0.05f, wid * 0.5f)
			};
			Node2D shard = new Node2D();
			root.AddChild(shard);
			Polygon2D body = Fx.Poly(shard, shape, new Color(glass, 0.35f), additive: true);
			Line2D edge = Fx.Line(shard, shape, new Color(Pal.White, 0.95f), 1.4f, closed: true);

			Vector2 dir = Fx.RandDir();
			float speed = Fx.Rand(500f, 1300f) * power;
			const float drag = 4.5f;
			float spin0 = Fx.Rand(-14f, 14f);
			float life = duration * Fx.Rand(0.55f, 1f);
			float glintPhase = Fx.Rand(0f, Mathf.Tau);
			shard.Rotation = dir.Angle();
			Fx.Animate(shard, life, t =>
			{
				float s = t * life;
				float travel = speed * (1f - Mathf.Exp(-drag * s)) / drag;
				shard.Position = dir * travel + new Vector2(0f, 260f * s * s);
				shard.Rotation = dir.Angle() + spin0 * (1f - Mathf.Exp(-2.5f * s)) / 2.5f;
				float glint = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(shard.Rotation * 3f + glintPhase)), 8f);
				edge.Modulate = new Color(1f + glint, 1f + glint, 1f + glint, 1f);
				body.Modulate = new Color(1f, 1f, 1f, 0.6f + glint * 0.8f);
				shard.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
				shard.Scale = Vector2.One * Mathf.Lerp(1f, 0.6f, t);
			});
		}
		Fx.Sparks(root, glass, 46, 200f, 900f, 0.6f, gravity: 120f, scaleMin: 0.1f, scaleMax: 0.35f);
		Fx.FreeAfter(root, duration + 0.4);
	}

	/// <summary>向上燃起的蓝色灵焰（Persona 出现的气场）。</summary>
	public static void SpiritFlare(Control layer, Vector2 at, Color tint, float height = 340f, float duration = 0.9f)
	{
		Node2D root = Fx.Root(layer, at, "P3SpiritFlare");
		GpuParticles2D flame = Fx.Sparks(root, tint, 70, 80f, 260f, duration * 0.8f, gravity: -height * 1.4f, scaleMin: 0.6f, scaleMax: 1.6f);
		ParticleProcessMaterial pm = (ParticleProcessMaterial)flame.ProcessMaterial;
		pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		pm.EmissionBoxExtents = new Vector3(70f, 12f, 0f);
		pm.Spread = 25f;
		flame.Explosiveness = 0.6f;
		Fx.FreeAfter(root, duration + 0.4);
	}

	// ───────────────────────── 蓝蝶 ─────────────────────────

	/// <summary>
	/// 一只蓝蝶：三次贝塞尔曲线路径，振翅由正弦驱动，身后拖光尾，首尾淡入淡出。
	/// </summary>
	public static void Butterfly(Control layer, Vector2 from, Vector2 to, float duration = 1.6f, float scale = 1f, Color? tint = null)
	{
		Color c = tint ?? Pal.Cyan;
		Node2D root = Fx.Root(layer, from, "P3Butterfly");
		root.Scale = Vector2.One * scale;

		Sprite2D glow = new Sprite2D { Texture = Fx.SoftDot, Scale = Vector2.One * 1.6f, Modulate = new Color(c, 0.6f), Material = Fx.Additive };
		root.AddChild(glow);

		Vector2[] upper = { new(0, 0), new(5, -12), new(14, -20), new(22, -16), new(20, -6), new(10, -1) };
		Vector2[] lower = { new(0, 1), new(11, 2), new(16, 9), new(11, 16), new(4, 10) };
		Node2D[] wings = new Node2D[2];
		for (int i = 0; i < 2; i++)
		{
			int side = i == 0 ? 1 : -1;
			Node2D wing = new Node2D();
			root.AddChild(wing);
			Fx.Poly(wing, upper, new Color(c, 0.9f), additive: true);
			Fx.Poly(wing, lower, new Color(c.Lightened(0.25f), 0.75f), additive: true);
			Fx.Line(wing, upper, new Color(Pal.White, 0.8f), 1f, closed: true);
			wing.Scale = new Vector2(side, 1f);
			wings[i] = wing;
		}
		Fx.Poly(root, new Vector2[] { new(-1.5f, -7), new(1.5f, -7), new(1f, 11), new(-1f, 11) }, Pal.White);

		GpuParticles2D trail = Fx.Sparks(root, c, 26, 4f, 20f, 0.55f, gravity: -15f, scaleMin: 0.06f, scaleMax: 0.18f, oneShot: false);

		Vector2 path = to - from;
		Vector2 normal = path.Normalized().Orthogonal();
		float bend = Fx.Rand(-0.6f, 0.6f) * path.Length();
		Vector2 c1 = from + path * 0.3f + normal * bend;
		Vector2 c2 = from + path * 0.7f - normal * bend * 0.6f;
		float flapRate = Fx.Rand(5.5f, 7f);
		float phase = Fx.Rand(0f, Mathf.Tau);
		Vector2 prev = from;
		Fx.Animate(root, duration, t =>
		{
			float e = Ease.InOutCubic(t);
			float u = 1f - e;
			Vector2 p = u * u * u * from + 3f * u * u * e * c1 + 3f * u * e * e * c2 + e * e * e * to;
			Vector2 vel = p - prev;
			prev = p;
			if (vel.LengthSquared() > 0.01f)
			{
				root.Rotation = Mathf.LerpAngle(root.Rotation, vel.Angle() + Mathf.Pi / 2f, 0.15f);
			}
			root.GlobalPosition = p;
			float flap = 0.2f + 0.8f * Mathf.Abs(Mathf.Cos(t * duration * flapRate * Mathf.Pi + phase));
			wings[0].Scale = new Vector2(flap, 1f);
			wings[1].Scale = new Vector2(-flap, 1f);
			root.Modulate = new Color(1f, 1f, 1f, Ease.Envelope(t, 0.12f, 0.72f));
			trail.Emitting = t < 0.8f;
		});
		Fx.FreeAfter(root, duration + 0.7);
	}

	/// <summary>从中心向外放飞一群蝴蝶，起飞时间错开。</summary>
	public static void ButterflyBurst(Control layer, Vector2 at, int count = 4, float radius = 240f, Color? tint = null)
	{
		for (int i = 0; i < count; i++)
		{
			Vector2 to = at + Vector2.Right.Rotated(Fx.Rand(-Mathf.Pi * 0.9f, -Mathf.Pi * 0.1f)) * Fx.Rand(radius * 0.6f, radius);
			float delay = i * 0.05f;
			float dur = Fx.Rand(1.2f, 1.8f);
			float scale = Fx.Rand(0.75f, 1.15f);
			if (delay <= 0f)
			{
				Butterfly(layer, at, to, dur, scale, tint);
			}
			else
			{
				layer.GetTree().CreateTimer(delay).Timeout += () => ModEntry.Safe("butterfly", () => Butterfly(layer, at, to, dur, scale, tint));
			}
		}
	}

	// ───────────────────────── 组合特效 ─────────────────────────

	/// <summary>
	/// 召唤器发动（所有出牌的基础层）：
	/// 0.00 蓄力环收缩 → 0.13 扣扳机：闪光 + 折射波 + 玻璃碎裂 + 速度线 → 灵焰升腾 + 蓝蝶散开。
	/// </summary>
	public static void EvokerBurst(Control layer, Vector2 head, float power = 1f, Color? tint = null, bool skipGather = false)
	{
		Color c = tint ?? Pal.Cyan;
		if (!skipGather)
		{
			Gather(layer, head, c, 260f * power, 0.13f);
		}
		layer.GetTree().CreateTimer(skipGather ? 0.0 : 0.13).Timeout += () => ModEntry.Safe("evoker trigger", () =>
		{
			Flash(layer, head, Pal.Ice, 320f * power, 0.22f);
			Ripple(layer, head, 640f * power, 0.75f, 0.035f);
			Shockwave(layer, head, c, 520f * power, 0.5f, delay: 0.03f);
			GlassShatter(layer, head, (int)(30 * power), power);
			SpeedLines(layer, head, c, (int)(14 * power), 46f, 380f * power, 0.36f, delay: 0.02f);
			SpiritFlare(layer, head + new Vector2(0f, 120f), Pal.Royal, 360f * power, 0.9f);
			ButterflyBurst(layer, head, Mathf.Max(2, (int)(3 * power)), 240f * power, c);
		});
	}

	/// <summary>攻击命中：斜十字斩，第二刀错后 70ms；命中点折射 + 碎光。</summary>
	public static void CrossSlash(Control layer, Vector2 at, Color tint, float size = 210f)
	{
		Node2D root = Fx.Root(layer, at, "P3CrossSlash");
		for (int i = 0; i < 2; i++)
		{
			float ang = i == 0 ? -0.62f : 0.7f;
			Vector2 dir = Vector2.Right.Rotated(ang);
			float delay = i * 0.07f;
			const float dur = 0.42f;
			float total = dur + delay;
			Line2D glow = Fx.Line(root, new[] { -dir * size, -dir * size }, new Color(tint, 0.8f), 34f);
			Line2D core = Fx.Line(root, new[] { -dir * size, -dir * size }, Pal.White, 9f);
			glow.WidthCurve = core.WidthCurve = Taper;
			glow.Modulate = core.Modulate = new Color(1f, 1f, 1f, 0f);
			Fx.Animate(glow, total, t =>
			{
				float local = Ease.Segment(t, delay / total, dur / total);
				float head = Ease.OutExpo(Mathf.Min(1f, local * 3f));
				float tail = Ease.InCubic(local);
				Vector2 a = -dir * size + dir * size * 2f * tail;
				Vector2 b = -dir * size + dir * size * 2f * head;
				glow.Points = core.Points = new[] { a, b };
				float alpha = local <= 0f ? 0f : 1f;
				glow.Modulate = core.Modulate = new Color(1f, 1f, 1f, alpha);
				float w = Mathf.Lerp(1f, 0.4f, local);
				glow.Width = 34f * w;
				core.Width = 9f * w;
			});
		}
		Flash(layer, at, Pal.Ice, 260f, 0.18f, delay: 0.04f);
		Ripple(layer, at, 420f, 0.5f, 0.028f, delay: 0.04f);
		Fx.Sparks(root, tint, 36, 250f, 700f, 0.5f, gravity: 200f);
		Fx.FreeAfter(root, 0.9);
	}

	/// <summary>能力牌：脚下影时间绿扁平波纹 + 光柱升起。</summary>
	private static readonly string[] Arcana = { "0", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII", "XIII", "XIV", "XV", "XVI", "XVII", "XVIII", "XIX", "XX", "XXI" };

	/// <summary>同一张牌总是同一个大阿卡纳编号（按牌名哈希）。</summary>
	public static string ArcanaFor(string key)
	{
		int h = 17;
		foreach (char ch in key) h = h * 31 + ch;
		return Arcana[(h & 0x7fffffff) % Arcana.Length];
	}

	/// <summary>
	/// 能力牌：人物身后张开一个"水之圆"（焦散），圆里浮现超大的大阿卡纳罗马数字，两道细圆弧反向转动；
	/// 入：0.3s 从中心张开（OutExpo）、数字从放大 + 透明压回；停 0.7s 缓慢漂移；出：0.35s 收拢（InCubic）、数字上浮淡出。
	/// </summary>
	public static void ArcanaDisc(Control backLayer, Vector2 feet, Vector2 head, string numeral)
	{
		float bodyH = Mathf.Max(200f, feet.Y - head.Y);
		Vector2 c = head + new Vector2(-bodyH * 0.05f, bodyH * 0.3f);
		float r = bodyH * 0.72f;
		Node2D root = Fx.Root(backLayer, c, "P3ArcanaDisc");
		ColorRect disc = P3Ui.WaterDisc(root, Vector2.Zero, r);
		Fx.SetParam(disc, "caustics", 0f);
		Node2D arcs = new();
		root.AddChild(arcs);
		P3Ui.Arc(arcs, Vector2.Zero, r * 1.08f, 0f, Mathf.Pi * 1.2f, new Color(Pal.Cyan, 0.6f), 3f);
		Node2D arcs2 = new();
		root.AddChild(arcs2);
		P3Ui.Arc(arcs2, Vector2.Zero, r * 1.18f, Mathf.Pi, Mathf.Pi * 1.6f, new Color(1f, 1f, 1f, 0.5f), 2f);
		Node2D num = new() { Skew = -0.22f };
		root.AddChild(num);
		int fs = (int)(r * 0.9f);
		Typo.Text(num, numeral, Typo.Latin, fs, new Color(1f, 1f, 1f, 0.5f), new Vector2(-fs * 0.3f * numeral.Length, -fs * 0.62f),
			outline: 4, outlineColor: new Color(0.3f, 0.85f, 1f, 0.7f));
		const float tIn = 0.3f, hold = 0.7f, tOut = 0.35f, total = tIn + hold + tOut;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float i = Ease.OutExpo(Mathf.Clamp(time / tIn, 0f, 1f));
			float o = Ease.InCubic(Mathf.Clamp((time - tIn - hold) / tOut, 0f, 1f));
			Fx.SetParam(disc, "reveal", i * (1f - o));
			arcs.Rotation = 0.5f * time;
			arcs2.Rotation = -0.35f * time;
			arcs.Modulate = arcs2.Modulate = new Color(1f, 1f, 1f, i * (1f - o));
			float n = Ease.OutCubic(Mathf.Clamp((time - 0.08f) / 0.3f, 0f, 1f));
			num.Scale = Vector2.One * Mathf.Lerp(1.6f, 1f, n);
			num.Position = new Vector2(0f, -40f * o - 10f * Mathf.Clamp((time - tIn) / hold, 0f, 1f));
			num.Modulate = new Color(1f, 1f, 1f, n * (1f - o));
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	public static void PowerUp(Control frontLayer, Control backLayer, Vector2 feet, Vector2 head, string? numeral = null)
	{
		if (numeral != null)
		{
			ArcanaDisc(backLayer, feet, head, numeral);
		}
		Ripple(backLayer, feet, 900f, 1.0f, 0.03f, squashY: 0.3f);
		Shockwave(backLayer, feet, Pal.DarkHour, 760f, 0.8f, squashY: 0.3f);
		Shockwave(backLayer, feet, Pal.DarkHour, 520f, 0.8f, squashY: 0.3f, delay: 0.12f);

		Node2D root = LightPillar(backLayer, feet, Pal.DarkHour, 220f, (feet.Y - head.Y) * 2.2f, 1.1f);
		GpuParticles2D motes = Fx.Sparks(root, Pal.DarkHour, 40, 60f, 200f, 1.0f, gravity: -300f, scaleMin: 0.15f, scaleMax: 0.4f);
		((ParticleProcessMaterial)motes.ProcessMaterial).EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		((ParticleProcessMaterial)motes.ProcessMaterial).EmissionBoxExtents = new Vector3(90f, 8f, 0f);
	}

	/// <summary>竖直光柱：底部在 feet，先横向展开再纵向冲起，随后收窄淡出。返回根节点（已自动释放）。</summary>
	public static Node2D LightPillar(Control layer, Vector2 feet, Color tint, float width, float height, float duration)
	{
		Node2D root = Fx.Root(layer, feet, "P3Pillar");
		ColorRect pillar = new ColorRect
		{
			Size = new Vector2(width, height),
			Position = new Vector2(-width / 2f, -height),
			PivotOffset = new Vector2(width / 2f, height),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = Shaders.Pillar }
		};
		root.AddChild(pillar);
		Fx.SetParam(pillar, "tint", tint);
		Fx.Animate(pillar, duration, t =>
		{
			float sx = Mathf.Lerp(0.2f, 1f, Ease.OutExpo(Mathf.Min(1f, t * 3f))) * Mathf.Lerp(1f, 0.4f, Ease.InCubic(t));
			pillar.Scale = new Vector2(sx, Ease.OutExpo(Mathf.Min(1f, t * 2.5f)));
			Fx.SetParam(pillar, "alpha", 1f - Ease.InCubic(t));
		});
		Fx.FreeAfter(root, duration + 0.1);
		return root;
	}

	// ───────────────────────── 预备出牌 ─────────────────────────

	/// <summary>
	/// 脚下法阵：OutExpo 展开，周期性向外扩散扁平波纹，持续升起光点，并定时放飞蓝蝶。
	/// 返回根节点，由调用方 Dismiss。
	/// </summary>
	public static Node2D Aura(Control back, Control front, Vector2 feet, Vector2 head, Color tint, float spin = 0.35f)
	{
		Node2D root = Fx.Root(back, feet, "P3Aura");
		ColorRect circle = Fx.ShaderQuad(root, Shaders.MagicCircle, 380f, 0.3f);
		Fx.SetParam(circle, "tint", tint);
		Fx.SetParam(circle, "spin", spin);
		Fx.SetParam(circle, "alpha", 0f);
		Fx.Animate(circle, 0.45f, t =>
		{
			float e = Ease.OutExpo(t);
			circle.Scale = new Vector2(Mathf.Lerp(0.3f, 1f, e), Mathf.Lerp(0.3f, 1f, e) * 0.3f);
			Fx.SetParam(circle, "alpha", Mathf.Min(1f, t * 2.5f) * 0.85f);
		});

		GpuParticles2D motes = Fx.Sparks(root, tint, 34, 15f, 50f, 1.6f, gravity: -60f, scaleMin: 0.08f, scaleMax: 0.25f, oneShot: false);
		ParticleProcessMaterial pm = (ParticleProcessMaterial)motes.ProcessMaterial;
		pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		pm.EmissionBoxExtents = new Vector3(120f, 10f, 0f);

		Timer pulse = new Timer { WaitTime = 1.1, Autostart = true };
		root.AddChild(pulse);
		pulse.Timeout += () => ModEntry.Safe("aura pulse", () => Shockwave(back, feet, tint, 420f, 0.9f, squashY: 0.3f));

		Timer flutter = new Timer { WaitTime = 0.5, Autostart = true };
		root.AddChild(flutter);
		flutter.Timeout += () => ModEntry.Safe("aura butterfly", () =>
		{
			Vector2 from = feet + new Vector2(Fx.Rand(-100f, 100f), Fx.Rand(-8f, 0f));
			Vector2 to = new Vector2(from.X + Fx.Rand(-50f, 70f), head.Y - Fx.Rand(30f, 140f));
			Butterfly(front, from, to, Fx.Rand(1.6f, 2.2f), Fx.Rand(0.5f, 0.8f), tint);
		});
		return root;
	}

	private static Vector2[] Arc(float radius, float from, float to, int segments = 24)
	{
		Vector2[] pts = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float a = Mathf.Lerp(from, to, (float)i / segments);
			pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
		}
		return pts;
	}

	/// <summary>
	/// 锁定框：双层反向旋转圆弧 + 三个向内的三角指示 + 四角括号，OutExpo 收拢入场。
	/// 返回根节点，由调用方 Dismiss。
	/// </summary>
	public static Node2D Reticle(Control layer, Vector2 at, float radius = 100f, Color? tint = null)
	{
		Color c = tint ?? Pal.Cyan;
		Node2D root = Fx.Root(layer, at, "P3Reticle");

		Node2D outer = new Node2D();
		root.AddChild(outer);
		for (int i = 0; i < 3; i++)
		{
			float a0 = Mathf.Tau * i / 3f;
			Fx.Line(outer, Arc(radius, a0, a0 + 1.4f), new Color(c, 0.9f), 3f);
		}
		Node2D inner = new Node2D();
		root.AddChild(inner);
		for (int i = 0; i < 4; i++)
		{
			float a0 = Mathf.Tau * i / 4f + 0.3f;
			Fx.Line(inner, Arc(radius * 0.68f, a0, a0 + 0.9f, 12), new Color(Pal.Ice, 0.8f), 1.6f);
		}
		Node2D pointers = new Node2D();
		root.AddChild(pointers);
		for (int i = 0; i < 3; i++)
		{
			Node2D p = new Node2D { Rotation = Mathf.Tau * i / 3f - Mathf.Pi / 2f };
			pointers.AddChild(p);
			float r = radius * 1.18f;
			Fx.Poly(p, new Vector2[] { new(r, 0f), new(r + 16f, -8f), new(r + 16f, 8f) }, Pal.White, additive: true);
		}
		Node2D brackets = new Node2D();
		root.AddChild(brackets);
		float br = radius * 1.4f;
		float bl = radius * 0.32f;
		for (int i = 0; i < 4; i++)
		{
			Node2D corner = new Node2D { Rotation = Mathf.Pi / 2f * i };
			brackets.AddChild(corner);
			Fx.Line(corner, new[] { new Vector2(br - bl, -br), new Vector2(br, -br), new Vector2(br, -br + bl) }, new Color(c, 0.85f), 2.5f);
		}

		root.Modulate = new Color(1f, 1f, 1f, 0f);
		Fx.Animate(root, 0.3f, t =>
		{
			float e = Ease.OutExpo(t);
			root.Scale = Vector2.One * Mathf.Lerp(1.7f, 1f, e);
			root.Rotation = Mathf.Lerp(-0.5f, 0f, e);
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 3f));
			brackets.Scale = Vector2.One * Mathf.Lerp(1.4f, 1f, Ease.OutExpo(Ease.Segment(t, 0.15f, 0.85f)));
		});

		Tween spinOuter = outer.CreateTween().SetLoops();
		spinOuter.TweenProperty(outer, "rotation", Mathf.Tau, 3.2f).AsRelative();
		Tween spinInner = inner.CreateTween().SetLoops();
		spinInner.TweenProperty(inner, "rotation", -Mathf.Tau, 2.2f).AsRelative();
		Tween breathe = pointers.CreateTween().SetLoops();
		breathe.TweenProperty(pointers, "scale", Vector2.One * 0.92f, 0.45f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		breathe.TweenProperty(pointers, "scale", Vector2.One, 0.45f).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

		Ripple(layer, at, 340f, 0.5f, 0.02f);
		return root;
	}

	/// <summary>淡出 + 轻微放大后释放一个常驻特效节点。</summary>
	public static void Dismiss(Node2D? node, float duration = 0.2f)
	{
		if (node == null || !GodotObject.IsInstanceValid(node))
		{
			return;
		}
		Vector2 from = node.Scale;
		foreach (Node child in node.GetChildren())
		{
			if (child is Timer timer)
			{
				timer.Stop();
			}
			else if (child is GpuParticles2D particles)
			{
				particles.Emitting = false;
			}
		}
		Fx.Animate(node, duration, t =>
		{
			node.Modulate = new Color(1f, 1f, 1f, 1f - Ease.OutCubic(t));
			node.Scale = from * Mathf.Lerp(1f, 1.15f, Ease.OutExpo(t));
		}).TweenCallback(Callable.From(node.QueueFree));
	}
}
