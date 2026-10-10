using Godot;

namespace P3Makoto.Vfx;

/// <summary>技能、能力、资源类特效。</summary>
public static class Extras
{
	/// <summary>
	/// 六边形蜂巢护盾（Tetrakarn 风格）：在角色前方的椭圆区域内，六边形从中心向外波浪式点亮，
	/// 停留片刻后自外向内熄灭。reflect = true 时为 Makarakarn 的紫粉色。
	/// </summary>
	public static void HexShield(Control layer, Vector2 center, float scale = 1f, bool reflect = false, float hold = 0.45f)
	{
		Color tint = reflect ? new Color("e07bff") : Pal.Cyan;
		Node2D root = Fx.Root(layer, center + new Vector2(95f * scale, 0f), "P3HexShield");
		float r = 24f * scale;
		float w = Mathf.Sqrt(3f) * r;
		float total = 0.35f + hold + 0.35f;
		for (int q = -4; q <= 4; q++)
		{
			for (int row = -6; row <= 6; row++)
			{
				Vector2 p = new Vector2(q * w + (row % 2 != 0 ? w / 2f : 0f), row * r * 1.5f);
				// 椭圆范围：竖长，模拟曲面护盾的正面投影
				float ex = p.X / (95f * scale);
				float ey = p.Y / (175f * scale);
				float d = ex * ex + ey * ey;
				if (d > 1f)
				{
					continue;
				}
				Node2D hex = new Node2D { Position = new Vector2(p.X * (1f - 0.25f * ey * ey), p.Y) };
				root.AddChild(hex);
				Fx.Poly(hex, Fx.Hexagon(r * 0.92f), new Color(tint, 0.12f + 0.12f * (1f - d)), additive: true);
				Fx.Line(hex, Fx.Hexagon(r * 0.92f), new Color(Pal.Ice, 0.8f), 1.4f * scale, closed: true);
				float dist = Mathf.Sqrt(d);
				float on = 0.35f * dist;
				float off = 0.35f + hold + 0.35f * (1f - dist);
				hex.Scale = Vector2.Zero;
				Fx.Animate(hex, total, t =>
				{
					float time = t * total;
					float appear = Ease.OutBack(Mathf.Clamp((time - on) / 0.2f, 0f, 1f));
					float vanish = Ease.InCubic(Mathf.Clamp((time - off) / 0.2f, 0f, 1f));
					hex.Scale = Vector2.One * appear * (1f - vanish * 0.5f);
					float flash = Mathf.Max(0f, 1f - Mathf.Abs(time - on - 0.1f) * 8f);
					hex.Modulate = new Color(1f + flash, 1f + flash, 1f + flash, 1f - vanish);
				});
			}
		}
		P3Fx.Ripple(layer, center + new Vector2(95f * scale, 0f), 420f * scale, 0.6f, 0.025f);
		P3Fx.Flash(layer, center + new Vector2(95f * scale, 0f), tint, 300f * scale, 0.25f);
		Fx.FreeAfter(root, total + 0.1);
	}

	/// <summary>获得辉星：四角星从身边浮现，旋转上升并闪烁。</summary>
	public static void StarGain(Control layer, Vector2 head, int count = 3)
	{
		count = Mathf.Clamp(count, 1, 8);
		for (int i = 0; i < count; i++)
		{
			Vector2 from = head + new Vector2(Fx.Rand(-90f, 90f), Fx.Rand(40f, 160f));
			Vector2 to = from + new Vector2(Fx.Rand(-30f, 30f), -Fx.Rand(140f, 220f));
			float delay = i * 0.07f;
			Fx.After(layer, delay, "star gain", () =>
			{
				Node2D star = Fx.Root(layer, from, "P3StarGain");
				Fx.Poly(star, Fx.Star4(22f), Pal.White, additive: true);
				Fx.Poly(star, Fx.Star4(40f, 0.1f), new Color(Pal.Cyan, 0.6f), additive: true);
				Sprite2D glow = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0.5f), Scale = Vector2.One * 2.2f };
				star.AddChild(glow);
				Fx.Animate(star, 1.0f, t =>
				{
					star.GlobalPosition = from.Lerp(to, Ease.OutCubic(t));
					float pop = Ease.OutBack(Mathf.Min(1f, t * 4f));
					float twinkle = 0.8f + 0.2f * Mathf.Sin(t * 30f);
					star.Scale = Vector2.One * pop * twinkle * Mathf.Lerp(1f, 0.6f, t);
					star.Rotation = t * 2.5f;
					star.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
				});
				Fx.FreeAfter(star, 1.1);
			});
		}
	}

	/// <summary>
	/// 塔罗牌：从角色身后翻转出现（宽度 0→1），上浮发光，停留后碎成玻璃。用于能力牌。
	/// </summary>
	public static void TarotFlip(Control layer, Vector2 at, Color tint, float hold = 0.35f)
	{
		Node2D root = Fx.Root(layer, at, "P3Tarot");
		Node2D card = new Node2D();
		root.AddChild(card);
		Vector2 half = new Vector2(62f, 100f);
		Vector2[] rect = { new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y) };
		Fx.Poly(card, rect, new Color(Pal.Abyss, 0.92f));
		Fx.Line(card, rect, tint, 3f, closed: true);
		Vector2[] inner = { new(-half.X + 10, -half.Y + 10), new(half.X - 10, -half.Y + 10), new(half.X - 10, half.Y - 10), new(-half.X + 10, half.Y - 10) };
		Fx.Line(card, inner, new Color(Pal.Ice, 0.7f), 1.2f, closed: true);
		// 牌面：圆环 + 十字 + 中心星，简化的阿尔卡纳纹样
		Fx.Line(card, Fx.Circle(34f, 32), tint, 2f, closed: true);
		Fx.Line(card, new[] { new Vector2(0f, -58f), new Vector2(0f, 58f) }, new Color(tint, 0.8f), 1.5f);
		Fx.Line(card, new[] { new Vector2(-40f, 0f), new Vector2(40f, 0f) }, new Color(tint, 0.8f), 1.5f);
		Fx.Poly(card, Fx.Star4(16f), Pal.White, additive: true);
		Sprite2D glow = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(tint, 0.55f), Scale = new Vector2(6f, 8f) };
		root.AddChild(glow);
		root.MoveChild(glow, 0);

		float total = 0.35f + hold;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float flip = Ease.OutExpo(Mathf.Min(1f, time / 0.35f));
			card.Scale = new Vector2(Mathf.Cos((1f - flip) * Mathf.Pi * 0.5f), 1f) * Mathf.Lerp(0.8f, 1f, flip);
			root.Position = new Vector2(0f, -40f * Ease.OutCubic(t));
			glow.Modulate = new Color(tint, 0.35f + 0.25f * Mathf.Sin(time * 12f));
		});
		Fx.After(layer, total, "tarot shatter", () =>
		{
			Vector2 p = root.GlobalPosition;
			root.QueueFree();
			P3Fx.Flash(layer, p, Pal.Ice, 320f, 0.2f);
			P3Fx.GlassShatter(layer, p, 26, 0.9f, tint.Lightened(0.4f));
			P3Fx.Ripple(layer, p, 480f, 0.6f, 0.03f);
		});
	}

	/// <summary>增益（箭头上升，Sukukaja 风格）或减益（箭头下压，Tarunda 风格）。</summary>
	public static void BuffArrows(Control layer, Vector2 center, Vector2 feet, bool up, Color? tint = null)
	{
		Color c = tint ?? (up ? new Color("ffb347") : new Color("ff4060"));
		Node2D root = Fx.Root(layer, feet, "P3Buff");
		for (int i = 0; i < 3; i++)
		{
			Node2D ring = new Node2D();
			root.AddChild(ring);
			Fx.Line(ring, Fx.Circle(110f, 40, 0.3f), new Color(c, 0.85f), 2.5f, closed: true);
			for (int k = 0; k < 4; k++)
			{
				float x = (k - 1.5f) * 50f;
				float dir = up ? -1f : 1f;
				Fx.Line(ring, new[] { new Vector2(x - 12f, 8f * -dir), new Vector2(x, -8f * -dir), new Vector2(x + 12f, 8f * -dir) }, Pal.White, 3f);
			}
			float delay = i * 0.12f;
			float span = feet.Y - center.Y + 80f;
			Fx.Animate(ring, 0.8f + delay, t =>
			{
				float local = Ease.Segment(t, delay / (0.8f + delay), 0.8f / (0.8f + delay));
				float y = up ? -span * Ease.OutCubic(local) : -span * (1f - Ease.OutCubic(local));
				ring.Position = new Vector2(0f, y);
				ring.Scale = Vector2.One * Mathf.Lerp(0.7f, 1.1f, local);
				ring.Modulate = new Color(1f, 1f, 1f, local <= 0f ? 0f : Ease.Envelope(local, 0.15f, 0.6f));
			});
		}
		P3Fx.Flash(layer, center, c, 260f, 0.25f);
		Fx.FreeAfter(root, 1.3);
	}

	/// <summary>君王之刃锻造：碎光从四周收拢，拼成一把幽蓝光剑后闪光消散。</summary>
	public static void ForgeBlade(Control layer, Vector2 at)
	{
		Node2D root = Fx.Root(layer, at, "P3Forge");
		root.Rotation = -0.35f;
		Vector2[] blade = { new(0f, -170f), new(14f, -120f), new(10f, 60f), new(26f, 70f), new(26f, 80f), new(6f, 82f), new(6f, 120f), new(-6f, 120f), new(-6f, 82f), new(-26f, 80f), new(-26f, 70f), new(-10f, 60f), new(-14f, -120f) };
		const float gather = 0.3f;
		for (int i = 0; i < 16; i++)
		{
			Vector2 target = blade[i % blade.Length] * 0.9f;
			Vector2 start = Fx.RandDir() * Fx.Rand(160f, 260f);
			Node2D shard = new Node2D { Position = start };
			root.AddChild(shard);
			Fx.Poly(shard, new Vector2[] { new(-8, 0), new(0, -14), new(8, 0), new(0, 6) }, new Color(Pal.Ice, 0.8f), additive: true);
			float spin = Fx.Rand(-8f, 8f);
			Fx.Animate(shard, gather, t =>
			{
				float e = Ease.InCubic(t);
				shard.Position = start.Lerp(target, e);
				shard.Rotation = spin * (1f - e);
			}).TweenCallback(Callable.From(shard.QueueFree));
		}
		Fx.After(layer, gather, "forge blade", () =>
		{
			Polygon2D glow = Fx.Poly(root, blade, new Color(Pal.Royal, 0.7f), additive: true);
			Line2D edge = Fx.Line(root, blade, Pal.Ice, 2.5f, closed: true);
			glow.Scale = edge.Scale = new Vector2(1.25f, 1.25f);
			Fx.Animate(glow, 0.65f, t =>
			{
				float s = Mathf.Lerp(1.25f, 1f, Ease.OutExpo(Mathf.Min(1f, t * 3f)));
				glow.Scale = edge.Scale = Vector2.One * s;
				float a = 1f - Ease.InCubic(t);
				glow.Modulate = edge.Modulate = new Color(1f + (1f - t), 1f + (1f - t), 1f + (1f - t), a);
			});
			P3Fx.Flash(layer, at, Pal.Ice, 340f, 0.2f);
			P3Fx.Ripple(layer, at, 460f, 0.55f, 0.03f);
			Fx.Sparks(root, Pal.Cyan, 30, 150f, 450f, 0.5f);
		});
		Fx.FreeAfter(root, gather + 0.8);
	}

	/// <summary>
	/// 影时间降临（VoidForm 等）：全屏染上暗绿，天空出现巨大满月，水面般的波纹扩散，随后褪去。
	/// </summary>
	public static void DarkHour(Control layer, float duration = 2.2f)
	{
		Vector2 size = layer.GetViewportRect().Size;
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3DarkHour");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;
		ColorRect tintRect = new ColorRect { Size = size, Color = new Color(0.05f, 0.25f, 0.12f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(tintRect);
		Vector2 moonPos = new Vector2(size.X * 0.78f, size.Y * 0.2f);
		Sprite2D moonGlow = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Position = moonPos, Scale = Vector2.One * 16f, Modulate = new Color(0.75f, 1f, 0.6f, 0f) };
		Polygon2D moon = new Polygon2D { Polygon = Fx.Circle(70f, 48), Position = moonPos, Color = new Color(0.92f, 1f, 0.75f, 0f), Material = Fx.Additive };
		root.AddChild(moonGlow);
		root.AddChild(moon);
		Fx.Animate(root, duration, t =>
		{
			float a = Ease.Envelope(t, 0.18f, 0.65f);
			tintRect.Color = new Color(0.05f, 0.25f, 0.12f, 0.38f * a);
			moon.Color = new Color(0.92f, 1f, 0.75f, 0.9f * a);
			moonGlow.Modulate = new Color(0.75f, 1f, 0.6f, 0.45f * a);
			moon.Scale = Vector2.One * Mathf.Lerp(0.85f, 1.05f, Ease.OutCubic(t));
		});
		P3Fx.Ripple(layer, size / 2f, size.X * 1.4f, 1.2f, 0.02f);
		P3Fx.Shockwave(layer, size / 2f, Pal.DarkHour, size.X * 1.2f, 1.1f, delay: 0.1f);
		Fx.FreeAfter(root, duration + 0.1);
	}

	/// <summary>
	/// P3R 风格斜切色带：深蓝平行四边形从左侧高速切入，停留，再向右切出；带青色描边和速度条纹。
	/// </summary>
	public static void SkewBand(Control layer, float yRatio = 0.42f, float hold = 0.45f, Color? tint = null)
	{
		Color c = tint ?? Pal.Royal;
		Vector2 size = layer.GetViewportRect().Size;
		float h = size.Y * 0.2f;
		float skew = h * 0.6f;
		float w = size.X + skew * 2f;
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3SkewBand");
		root.TopLevel = true;
		Node2D band = new Node2D();
		root.AddChild(band);
		float y = size.Y * yRatio;
		Vector2[] shape = { new(skew, y - h / 2f), new(w, y - h / 2f), new(w - skew, y + h / 2f), new(0f, y + h / 2f) };
		Fx.Poly(band, shape, new Color(Pal.Abyss, 0.88f));
		Fx.Poly(band, new Vector2[] { new(skew + 30f, y - h / 2f), new(w, y - h / 2f), new(w - 6f, y - h / 2f + 10f), new(skew + 24f, y - h / 2f + 10f) }, new Color(c, 0.9f), additive: true);
		Fx.Line(band, new[] { new Vector2(0f, y + h / 2f), new Vector2(w - skew, y + h / 2f) }, Pal.Cyan, 3f);
		for (int i = 0; i < 6; i++)
		{
			float yy = y - h / 2f + h * (0.25f + 0.1f * i);
			float x0 = Fx.Rand(0.1f, 0.6f) * w;
			Fx.Line(band, new[] { new Vector2(x0, yy), new Vector2(x0 + Fx.Rand(120f, 380f), yy) }, new Color(Pal.Ice, 0.25f), 2f);
		}
		float total = 0.22f + hold + 0.2f;
		Fx.Animate(band, total, t =>
		{
			float time = t * total;
			float x;
			if (time < 0.22f)
			{
				x = Mathf.Lerp(-w, -skew, Ease.OutExpo(time / 0.22f));
			}
			else if (time < 0.22f + hold)
			{
				x = Mathf.Lerp(-skew, -skew + 40f, (time - 0.22f) / hold);
			}
			else
			{
				x = Mathf.Lerp(-skew + 40f, size.X + skew, Ease.InExpo((time - 0.22f - hold) / 0.2f));
			}
			band.Position = new Vector2(x, 0f);
		});
		Fx.FreeAfter(root, total + 0.1);
	}

	/// <summary>黑洞：中心暗核 + 向内旋转吸入的光粒 + 旋转吸积环。</summary>
	public static void BlackHole(Control layer, Vector2 at, float duration = 1.4f)
	{
		Node2D root = Fx.Root(layer, at, "P3BlackHole");
		Node2D disk = new Node2D { Scale = new Vector2(1f, 0.35f) };
		root.AddChild(disk);
		for (int i = 0; i < 3; i++)
		{
			float a0 = Mathf.Tau * i / 3f;
			Line2D arc = Fx.Line(disk, Arc(130f + i * 18f, a0, a0 + 2.2f), i == 1 ? Pal.Ice : Pal.Cyan, 4f - i);
		}
		Polygon2D core = Fx.Poly(root, Fx.Circle(60f, 40), new Color(0f, 0f, 0.02f, 0.95f));
		Line2D rim = Fx.Line(root, Fx.Circle(62f, 40), Pal.Ice, 2.5f, closed: true);
		for (int i = 0; i < 24; i++)
		{
			float a = Fx.Rand(0f, Mathf.Tau);
			float r0 = Fx.Rand(220f, 340f);
			float delay = Fx.Rand(0f, duration * 0.5f);
			Sprite2D mote = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = Pal.Cyan, Scale = Vector2.One * 0.5f };
			root.AddChild(mote);
			Fx.Animate(mote, duration, t =>
			{
				float local = Ease.Segment(t, delay / duration, 0.45f);
				float r = Mathf.Lerp(r0, 60f, Ease.InCubic(local));
				float ang = a + local * 4f;
				mote.Position = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.5f) * r;
				mote.Modulate = new Color(Pal.Cyan, local <= 0f || local >= 1f ? 0f : 1f);
			});
		}
		Fx.Animate(root, duration, t =>
		{
			float grow = Ease.OutBack(Mathf.Min(1f, t * 3f));
			float end = Ease.InExpo(Ease.Segment(t, 0.8f, 0.2f));
			core.Scale = rim.Scale = Vector2.One * grow * (1f - end);
			disk.Rotation = t * 5f;
			disk.Modulate = new Color(1f, 1f, 1f, 1f - end);
		});
		P3Fx.Ripple(layer, at, 520f, duration * 0.8f, 0.04f);
		Fx.After(layer, duration * 0.95, "black hole collapse", () =>
		{
			P3Fx.Flash(layer, at, Pal.Ice, 380f, 0.25f);
			P3Fx.Shockwave(layer, at, Pal.Cyan, 520f, 0.5f);
		});
		Fx.FreeAfter(root, duration + 0.1);
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
}
