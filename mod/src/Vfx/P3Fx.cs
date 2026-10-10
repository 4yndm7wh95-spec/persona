using Godot;

namespace P3Makoto.Vfx;

public static class Pal
{
	public static readonly Color Blue = new("4fb8ff");
	public static readonly Color DeepBlue = new("1e4bff");
	public static readonly Color Ice = new("bfefff");
	public static readonly Color DarkHour = new("7dffa0");
	public static readonly Color White = new(1f, 1f, 1f);
}

/// <summary>P3 主题特效积木。每个方法都会自动释放自己创建的节点。</summary>
public static class P3Fx
{
	/// <summary>召唤器扣下扳机时的玻璃碎裂：碎片从头部向外迸射并受重力下落。</summary>
	public static void GlassShatter(Control layer, Vector2 at, int count = 26, float power = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3GlassShatter");
		const float duration = 1.1f;
		for (int i = 0; i < count; i++)
		{
			float size = Fx.Rand(6f, 22f) * power;
			Vector2[] tri =
			{
				Fx.RandDir() * size,
				Fx.RandDir() * size * Fx.Rand(0.4f, 1f),
				Fx.RandDir() * size * Fx.Rand(0.2f, 0.7f)
			};
			Node2D shard = new Node2D();
			root.AddChild(shard);
			Fx.Poly(shard, tri, new Color(Pal.Ice, 0.55f));
			Fx.Line(shard, tri, new Color(Pal.White, 0.9f), 1.5f, closed: true);

			Vector2 dir = Fx.RandDir();
			if (dir.X < 0f)
			{
				dir.X *= 0.5f; // 碎片主要飞向角色身后反方向之外，避免糊脸
			}
			Vector2 velocity = dir * Fx.Rand(260f, 620f) * power + new Vector2(0f, -120f);
			float spin = Fx.Rand(-12f, 12f);
			float life = duration * Fx.Rand(0.65f, 1f);
			Fx.Animate(shard, life, t =>
			{
				float s = t * life;
				shard.Position = velocity * s + new Vector2(0f, 900f) * s * s * 0.5f;
				shard.Rotation = spin * s;
				shard.Modulate = new Color(1f, 1f, 1f, t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f);
			});
		}
		Fx.Sparks(root, Pal.Ice, 40, 120f, 520f, 0.7f, gravity: 300f);
		Fx.FreeAfter(root, duration + 0.4);
	}

	/// <summary>冲击波圆环。</summary>
	public static void Shockwave(Control layer, Vector2 at, Color tint, float size = 520f, float duration = 0.55f, float squashY = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P3Shockwave");
		ColorRect quad = Fx.ShaderQuad(root, Shaders.Ring, size, squashY);
		Fx.SetParam(quad, "tint", tint);
		Fx.Animate(quad, duration, t =>
		{
			float e = 1f - Mathf.Pow(1f - t, 3f);
			Fx.SetParam(quad, "radius", Mathf.Lerp(0.05f, 0.95f, e));
			Fx.SetParam(quad, "width", Mathf.Lerp(0.08f, 0.015f, e));
			Fx.SetParam(quad, "alpha", 1f - t);
		});
		Fx.FreeAfter(root, duration + 0.1);
	}

	/// <summary>径向闪光。</summary>
	public static void Flash(Control layer, Vector2 at, Color tint, float size = 300f, float duration = 0.25f)
	{
		Node2D root = Fx.Root(layer, at, "P3Flash");
		ColorRect quad = Fx.ShaderQuad(root, Shaders.Flash, size);
		Fx.SetParam(quad, "tint", tint);
		Fx.Animate(quad, duration, t =>
		{
			Fx.SetParam(quad, "alpha", 1f - t * t);
			quad.Scale = Vector2.One * Mathf.Lerp(0.6f, 1.2f, t);
		});
		Fx.FreeAfter(root, duration + 0.1);
	}

	/// <summary>
	/// 一只蓝色蝴蝶：沿带横向摆动的路径从 from 飞到 to，振翅、拖光尾，到达后淡出。
	/// </summary>
	public static void Butterfly(Control layer, Vector2 from, Vector2 to, float duration = 1.6f, float scale = 1f, Color? tint = null)
	{
		Color c = tint ?? Pal.Blue;
		Node2D root = Fx.Root(layer, from, "P3Butterfly");
		root.Scale = Vector2.One * scale;

		Node2D wings = new Node2D();
		root.AddChild(wings);
		Vector2[] upper = { new(0, 0), new(6, -14), new(16, -20), new(22, -12), new(16, -2) };
		Vector2[] lower = { new(0, 0), new(14, 2), new(16, 12), new(8, 15), new(3, 8) };
		foreach (int side in new[] { 1, -1 })
		{
			Node2D wing = new Node2D { Scale = new Vector2(side, 1f) };
			wings.AddChild(wing);
			Fx.Poly(wing, upper, new Color(c, 0.85f), additive: true);
			Fx.Poly(wing, lower, new Color(c.Lightened(0.2f), 0.7f), additive: true);
			Fx.Line(wing, upper, new Color(Pal.White, 0.7f), 1f, closed: true);
		}
		Fx.Poly(root, new Vector2[] { new(-1.5f, -6), new(1.5f, -6), new(1, 10), new(-1, 10) }, Pal.White);

		Tween flap = wings.CreateTween().SetLoops();
		flap.TweenProperty(wings, "scale:x", 0.15f, 0.11f).SetTrans(Tween.TransitionType.Sine);
		flap.TweenProperty(wings, "scale:x", 1f, 0.11f).SetTrans(Tween.TransitionType.Sine);

		GpuParticles2D trail = Fx.Sparks(root, c, 24, 5f, 25f, 0.6f, gravity: -20f, scaleMin: 0.08f, scaleMax: 0.2f, oneShot: false);

		float phase = Fx.Rand(0f, Mathf.Tau);
		float sway = Fx.Rand(18f, 40f);
		Vector2 path = to - from;
		Vector2 normal = path.Normalized().Orthogonal();
		Fx.Animate(root, duration, t =>
		{
			float e = Mathf.SmoothStep(0f, 1f, t);
			Vector2 p = from + path * e + normal * Mathf.Sin(t * Mathf.Tau * 1.5f + phase) * sway;
			root.Rotation = Mathf.LerpAngle(root.Rotation, (p - root.GlobalPosition).Angle() + Mathf.Pi / 2f, 0.2f);
			root.GlobalPosition = p;
			float a = t < 0.15f ? t / 0.15f : t > 0.75f ? 1f - (t - 0.75f) / 0.25f : 1f;
			root.Modulate = new Color(1f, 1f, 1f, a);
			if (t > 0.8f)
			{
				trail.Emitting = false;
			}
		});
		Fx.FreeAfter(root, duration + 0.7);
	}

	/// <summary>从中心向四周放飞一群蝴蝶。</summary>
	public static void ButterflyBurst(Control layer, Vector2 at, int count = 6, float radius = 220f)
	{
		for (int i = 0; i < count; i++)
		{
			Vector2 to = at + Fx.RandDir() * Fx.Rand(radius * 0.5f, radius) + new Vector2(0f, -radius * 0.4f);
			Butterfly(layer, at, to, Fx.Rand(1.1f, 1.8f), Fx.Rand(0.7f, 1.2f));
		}
	}

	/// <summary>
	/// 召唤器发动：闪光 + 玻璃碎裂 + 冲击波 + 蝴蝶。所有出牌的基础层。
	/// </summary>
	public static void EvokerBurst(Control layer, Vector2 head, float power = 1f)
	{
		Flash(layer, head, Pal.Ice, 260f * power, 0.22f);
		GlassShatter(layer, head, (int)(22 * power), power);
		Shockwave(layer, head, Pal.Blue, 480f * power, 0.5f);
		ButterflyBurst(layer, head, Mathf.Max(2, (int)(4 * power)), 200f * power);
	}

	/// <summary>攻击牌命中：交叉斩击 + 碎光。</summary>
	public static void CrossSlash(Control layer, Vector2 at, Color tint, float size = 190f)
	{
		Node2D root = Fx.Root(layer, at, "P3CrossSlash");
		Curve taper = new Curve();
		taper.AddPoint(new Vector2(0f, 0f));
		taper.AddPoint(new Vector2(0.5f, 1f));
		taper.AddPoint(new Vector2(1f, 0f));
		for (int i = 0; i < 2; i++)
		{
			float ang = i == 0 ? -0.65f : 0.65f;
			Vector2 dir = Vector2.Right.Rotated(ang);
			Line2D blade = Fx.Line(root, new[] { -dir * size, dir * size }, tint.Lightened(0.3f), 26f);
			blade.WidthCurve = taper;
			Line2D core = Fx.Line(root, new[] { -dir * size, dir * size }, Pal.White, 8f);
			core.WidthCurve = taper;
			float delay = i * 0.07f;
			blade.Modulate = core.Modulate = new Color(1f, 1f, 1f, 0f);
			Fx.Animate(blade, 0.4f + delay, t =>
			{
				float local = Mathf.Clamp((t * (0.4f + delay) - delay) / 0.4f, 0f, 1f);
				float reveal = Mathf.Min(1f, local * 4f);
				Vector2 start = -dir * size;
				Vector2 end = start + dir * size * 2f * reveal;
				blade.Points = core.Points = new[] { start, end };
				float a = local < 0.25f ? 1f : 1f - (local - 0.25f) / 0.75f;
				blade.Modulate = core.Modulate = new Color(1f, 1f, 1f, local <= 0f ? 0f : a);
			});
		}
		Fx.Sparks(root, tint, 30, 150f, 450f, 0.5f);
		Fx.FreeAfter(root, 0.8);
	}

	/// <summary>
	/// P3 风格锁定框：四角括号 + 旋转菱形。返回根节点，由调用方负责释放（Dismiss）。
	/// </summary>
	public static Node2D Reticle(Control layer, Vector2 at, float radius = 95f)
	{
		Node2D root = Fx.Root(layer, at, "P3Reticle");
		float r = radius;
		float l = radius * 0.35f;
		for (int i = 0; i < 4; i++)
		{
			Node2D corner = new Node2D { Rotation = Mathf.Pi / 2f * i };
			root.AddChild(corner);
			Fx.Line(corner, new[] { new Vector2(r - l, -r), new Vector2(r, -r), new Vector2(r, -r + l) }, Pal.Ice, 4f);
		}
		Node2D diamond = new Node2D();
		root.AddChild(diamond);
		float dr = radius * 0.55f;
		Fx.Line(diamond, new[] { new Vector2(0, -dr), new Vector2(dr, 0), new Vector2(0, dr), new Vector2(-dr, 0) }, new Color(Pal.Blue, 0.9f), 2.5f, closed: true);
		Fx.Line(diamond, Fx.Circle(dr * 0.25f, 16), new Color(Pal.White, 0.8f), 2f, closed: true);

		root.Scale = Vector2.One * 1.8f;
		root.Modulate = new Color(1f, 1f, 1f, 0f);
		Tween appear = root.CreateTween().SetParallel();
		appear.TweenProperty(root, "scale", Vector2.One, 0.22f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		appear.TweenProperty(root, "modulate:a", 1f, 0.15f);

		Tween spin = diamond.CreateTween().SetLoops();
		spin.TweenProperty(diamond, "rotation", Mathf.Tau, 2.4f).AsRelative();

		Tween breathe = root.CreateTween().SetLoops();
		breathe.TweenInterval(0.22);
		breathe.TweenProperty(root, "scale", Vector2.One * 1.06f, 0.5f).SetTrans(Tween.TransitionType.Sine);
		breathe.TweenProperty(root, "scale", Vector2.One, 0.5f).SetTrans(Tween.TransitionType.Sine);
		return root;
	}

	/// <summary>淡出并释放一个常驻特效节点。</summary>
	public static void Dismiss(Node2D? node, float duration = 0.2f)
	{
		if (node == null || !GodotObject.IsInstanceValid(node))
		{
			return;
		}
		Tween t = node.CreateTween();
		t.TweenProperty(node, "modulate:a", 0f, duration);
		t.TweenCallback(Callable.From(node.QueueFree));
	}
}
