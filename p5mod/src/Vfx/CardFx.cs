using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 牌的特效（2026-10-29 重做第一批）。不照搬 P5R 原版的某个技能，而是用 Joker 的气质自由发挥：
/// 红黑白三色、斜切的 UI 色块、漫画网点、怪盗的预告信。每个都按"起手 → 爆发 → 余韵"编排，元素错开几十毫秒依次出现，
/// 运动一律"快出手、长尾减速"。都不挡敌人。
/// </summary>
public static class CardFx
{
	private static readonly Color Red = Popups.Red, Ink = Popups.Ink;

	/// <summary>
	/// 防御（Joker 获得格挡时），发光积木版：
	/// 预备 —— 身前一圈红光往里收（0.12s）；
	/// 爆发 —— 一道往敌人方向鼓起的弧形能量屏障从中间向上下"唰"地张开：白热的亮边 + 红晕 + 半透明的红色光面，
	///         同时一圈折射波、扁的光环；
	/// 停留 —— 一道亮光沿屏障边从上扫到下，几颗星光，光面微微闪烁；
	/// 余韵 —— 屏障从两头往中间收拢成一点，炸成一蓬红色火花。chest = 胸口，facing = 敌人方向。
	/// </summary>
	public static void Guard(Control layer, Vector2 chest, float facing = 1f)
	{
		const float R = 170f, A = 0.78f;                          // 弧半径、上下张角
		Vector2 center = chest + new Vector2(-35f * facing, 25f);
		Vector2 front = center + new Vector2(R * facing, 0f);
		Glow.Gather(layer, front, Pal5.Bright, 260f, 0.12f);

		Node2D root = Fx.Root(layer, center, "P5Guard", 1.0);
		root.Scale = new Vector2(facing, 1f);
		Polygon2D fill = Fx.Poly(root, new Vector2[3], new Color(Pal5.Crimson, 0.16f), additive: true);
		Polygon2D glow = Fx.Poly(root, new Vector2[3], new Color(Pal5.Bright, 0.85f), additive: true);
		Polygon2D core = Fx.Poly(root, new Vector2[3], Pal5.Hot, additive: true);
		Polygon2D sweep = Fx.Poly(root, new Vector2[3], Pal5.Hot, additive: true);
		root.Visible = false;

		const float open0 = 0.12f, total = 0.72f;
		Fx.Animate(root, 0, total, t =>
		{
			float time = t * total;
			root.Visible = time >= open0;
			float open = Ease5.OutExpo(Ease5.Seg(time, open0, 0.14f));
			float close = Fx.InCubic(Ease5.Seg(time, 0.56f, 0.14f));
			float half = A * open * (1f - close);
			float flick = 1f + 0.08f * Mathf.Sin(time * 55f);
			fill.Polygon = Crescent(R - 2f, R - 34f, -half, half);
			glow.Polygon = Crescent(R + 9f, R - 13f, -half, half);
			core.Polygon = Crescent(R + 2.5f, R - 3.5f, -half, half);
			fill.Modulate = new Color(1f, 1f, 1f, flick);
			// 扫光：0.3~0.5s 一小段亮弧从上往下走
			float sw = Ease5.Seg(time, 0.3f, 0.2f);
			sweep.Visible = sw > 0f && sw < 1f;
			float a = Mathf.Lerp(-half, half, Fx.OutCubic(sw));
			sweep.Polygon = Crescent(R + 5f, R - 5f, a - 0.2f, a + 0.2f);
		});
		Fx.After(root, open0, () =>
		{
			Glow.Ripple(layer, front, 260f, 0.45f, 0.03f, 1.5f);
			Glow.Shockwave(layer, front, new Color(0.95f, 0.06f, 0.1f), 200f, 0.36f, 1.5f);
			Glow.Flash(layer, front, new Color(1f, 0.5f, 0.45f), 220f, 0.18f);
			Fx.Shake(1);
		});
		for (int i = 0; i < 3; i++)
		{
			float a = Mathf.Lerp(-A * 0.75f, A * 0.75f, i / 2f);
			Vector2 p = center + new Vector2(Mathf.Cos(a) * facing, Mathf.Sin(a)) * R;
			Glow.Glint(layer, p, 0.8f, 0.32f + 0.06f * i);
		}
		Fx.After(root, 0.68, () =>
		{
			Glow.Flash(layer, front, new Color(1f, 0.55f, 0.5f), 160f, 0.16f);
			Glow.Sparks(layer, front, 26, 120f, 420f, 0.55f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 160f, 0.1f, 0.32f,
				new Vector2(facing, -0.2f).Normalized(), 110f);
		});
	}

	/// <summary>弧形光带：内外半径 rIn / rOut，角度 a0 到 a1（以 +x 为 0），两头收尖。</summary>
	private static Vector2[] Crescent(float rOut, float rIn, float a0, float a1)
	{
		const int n = 22;
		if (a1 - a0 < 0.005f)
		{
			return new[] { Vector2.Zero, new Vector2(0.1f, 0f), new Vector2(0f, 0.1f) };
		}
		var pts = new Vector2[n * 2];
		for (int i = 0; i < n; i++)
		{
			float u = (float)i / (n - 1), a = Mathf.Lerp(a0, a1, u);
			float k = Mathf.Max(0.06f, Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(u * Mathf.Pi))));   // sin(π) 会是很小的负数 → NaN  // 两头收尖（不收到 0：重复点会让三角剖分失败）
			float mid = (rOut + rIn) / 2f, hw = (rOut - rIn) / 2f * k;
			Vector2 d = Vector2.Right.Rotated(a);
			pts[i] = d * (mid + hw);
			pts[n * 2 - 1 - i] = d * (mid - hw);
		}
		return pts;
	}

	/// <summary>
	/// 一片黑羽：细长的黑色羽面，下沿带两道缺口（羽枝分叉），只有上沿一道细细的红色反光（不是整圈描边），
	/// 一道深红羽轴。羽根在原点、羽尖朝 +x。
	/// </summary>
	private static Node2D Feather(Node parent, float len, float w)
	{
		Node2D f = new();
		parent.AddChild(f);
		float l = len, h = w / 2f;
		var body = new[]
		{
			new Vector2(0f, 0f), new Vector2(l * 0.18f, -h * 0.9f), new Vector2(l * 0.55f, -h), new Vector2(l * 0.85f, -h * 0.6f),
			new Vector2(l, -h * 0.1f), new Vector2(l * 0.9f, h * 0.35f), new Vector2(l * 0.74f, h * 0.55f),
			new Vector2(l * 0.66f, h * 0.15f), new Vector2(l * 0.58f, h * 0.8f),                      // 缺口 1
			new Vector2(l * 0.4f, h * 0.85f), new Vector2(l * 0.33f, h * 0.3f), new Vector2(l * 0.26f, h * 0.8f),   // 缺口 2
			new Vector2(l * 0.1f, h * 0.55f),
		};
		Fx.Poly(f, body, new Color(0.05f, 0.04f, 0.06f));
		// 上沿的红色反光：贴着上沿的一条月牙
		Fx.Poly(f, new[]
		{
			new Vector2(l * 0.12f, -h * 0.75f), new Vector2(l * 0.55f, -h * 1.05f), new Vector2(l * 0.86f, -h * 0.66f), new Vector2(l * 0.98f, -h * 0.12f),
			new Vector2(l * 0.82f, -h * 0.4f), new Vector2(l * 0.52f, -h * 0.62f), new Vector2(l * 0.18f, -h * 0.4f),
		}, Red);
		Fx.Poly(f, new[] { new Vector2(0f, -1f), new Vector2(l * 0.9f, -0.3f), new Vector2(l * 0.9f, 0.3f), new Vector2(0f, 1f) }, new Color(0.5f, 0.03f, 0.07f));
		return f;
	}

	/// <summary>四角星光（P5R 原版星形贴图），一闪即逝。</summary>
	private static void Sparkle(Control layer, Vector2 at, float size)
	{
		if (Fx.Tex("star4") is not Texture2D tex)
		{
			return;
		}
		Node2D st = Fx.Root(layer, at, "P5Sparkle", 0.3);
		Sprite2D s = Fx.Sprite(st, tex);
		Sprite2D g = Fx.Tex("glow") is Texture2D glow ? Fx.Sprite(st, glow) : s;
		Fx.Animate(st, 0, 0.26, t =>
		{
			float k = Mathf.Sin(Mathf.Min(t * 1.6f, 1f) * Mathf.Pi);
			s.Scale = Vector2.One * size * 1.8f * k;
			s.Rotation = t * 0.8f;
			s.Modulate = new Color(1f, 0.92f, 0.92f, k);
			if (g != s)
			{
				g.Scale = Vector2.One * size * 0.9f * k;
				g.Modulate = new Color(1f, 0.2f, 0.2f, 0.7f * k);
			}
		});
	}

	/// <summary>羽盾散开：每片羽毛从自己的位置往外甩出去，打着旋飘落；带几颗红色火星。</summary>
	private static void Scatter(Control layer, Vector2 center, float facing, float r, float a0, float a1, int n)
	{
		for (int i = 0; i < n; i++)
		{
			float a = Mathf.Lerp(a0, a1, (float)i / (n - 1));
			Vector2 dir = new Vector2(Mathf.Cos(a) * facing, Mathf.Sin(a));
			Vector2 start = center + dir * r;
			Node2D holder = Fx.Root(layer, start, "P5Feather", 0.9);
			Node2D f = Feather(holder, Fx.Rand(60f, 76f), 13f);
			holder.Scale = new Vector2(facing, 1f);
			float rot0 = a + 0.35f, spin = Fx.Rand(-7f, 7f), sway = Fx.Rand(5f, 9f);
			Vector2 v = dir * Fx.Rand(90f, 170f) + new Vector2(0f, -Fx.Rand(40f, 90f));
			Fx.Animate(holder, 0, 0.8, t =>
			{
				holder.GlobalPosition = start + v * Fx.OutCubic(t) + new Vector2(18f * Mathf.Sin(t * sway), 120f * t * t);
				f.Rotation = rot0 + spin * t * (1f - 0.5f * t);
				f.Scale = new Vector2(Mathf.Cos(t * sway * 1.3f), 1f) * (1f - 0.3f * t);
				holder.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t));
			});
		}
		Embers(layer, center + new Vector2(r * 0.8f * facing, 0f));
	}

	/// <summary>
	/// 预告信（"中和"等施加虚弱的牌命中时）：一张红色预告信拖着三道残影、带着红光从右上方旋转着飞来，
	/// "啪"地钉在敌人身上——身后炸开一团圆润的黑墨（红边），白闪 + 四颗星光；
	/// 卡片轻轻晃动一会儿，然后一翻面、化作几片黑羽散开。at = 敌人的 VfxSpawnPosition。
	/// </summary>
	public static void CallingCard(Control layer, Vector2 at)
	{
		if (Fx.Tex("calling_card") is not Texture2D tex)
		{
			return;
		}
		Vector2 home = at + new Vector2(-20f, -70f);
		Vector2 start = home + new Vector2(260f, -230f);
		float k = 112f / tex.GetHeight(), tilt = Fx.Rand(-0.24f, -0.1f);
		const float fly = 0.16f;

		// 残影：三张淡红的卡片跟在后面
		for (int g = 3; g >= 1; g--)
		{
			Node2D ghost = Fx.Root(layer, start, "P5CardGhost", 0.3);
			Sprite2D gs = Fx.Sprite(ghost, tex);
			gs.Scale = Vector2.One * k;
			float lag = 0.025f * g;
			Fx.Animate(ghost, 0, fly + lag, t =>
			{
				float u = Mathf.Clamp((t * (fly + lag) - lag) / fly, 0f, 1f);
				ghost.Visible = u > 0f && u < 1f;
				ghost.GlobalPosition = start.Lerp(home, Fx.InCubic(u) * 0.6f + u * 0.4f);
				ghost.Rotation = tilt + (1f - u) * Mathf.Tau * 1.5f;
				gs.Modulate = new Color(1f, 0.2f, 0.2f, 0.45f - 0.12f * g);
			});
		}

		Node2D root = Fx.Root(layer, start, "P5CallingCard", 1.4);
		Node2D splash = new() { Visible = false };
		root.AddChild(splash);
		Blobs(splash, 6f, Red);                         // 黑墨红边：红色的预告信在黑墨上才跳得出来
		Blobs(splash, 0f, Ink);
		Sprite2D? glowS = Fx.Tex("glow") is Texture2D glow ? Fx.Sprite(root, glow) : null;
		Sprite2D card = Fx.Sprite(root, tex, additive: false);
		Sprite2D flash = Fx.Sprite(root, tex, additive: true);
		card.Scale = flash.Scale = Vector2.One * k;

		const float total = 1.0f;
		Fx.Animate(root, 0, total, t =>
		{
			float time = t * total;
			float u = Mathf.Clamp(time / fly, 0f, 1f);
			float land = Mathf.Clamp((time - fly) / 0.2f, 0f, 1f);
			float sq = land <= 0f ? 0f : Mathf.Exp(-6f * land) * Mathf.Cos(land * 16f);
			float sway = land > 0f ? 0.05f * Mathf.Sin((time - fly) * 7f) * (1f - land * 0.5f) : 0f;
			root.GlobalPosition = start.Lerp(home, Fx.InCubic(u) * 0.6f + u * 0.4f);
			root.Rotation = tilt + (1f - u) * Mathf.Tau * 1.5f + sway;
			// 离场：0.78s 起卡片绕竖轴翻过去（x 缩到 0）
			float flip = Mathf.Clamp((time - 0.78f) / 0.12f, 0f, 1f);
			float s = Mathf.Lerp(0.7f, 1f, u);
			card.Scale = flash.Scale = new Vector2(k * s * (1f + 0.25f * sq) * (1f - flip), k * s * (1f - 0.2f * sq));
			flash.Modulate = new Color(1f, 1f, 1f, u < 1f ? 0f : Mathf.Clamp(1f - land / 0.45f, 0f, 1f));
			if (glowS != null)
			{
				glowS.Scale = Vector2.One * (1.4f + 1.2f * land);
				glowS.Modulate = new Color(1f, 0.15f, 0.15f, (u < 1f ? 0.5f : 0.9f * (1f - land)) * (1f - flip));
			}
			// 墨溅：钉上去那一刻在卡片背后炸开，0.12s 胀到满，慢慢淡掉
			splash.Visible = u >= 1f && time < 0.95f;
			float sp = Mathf.Clamp((time - fly) / 0.12f, 0f, 1f);
			float shrink = Fx.InCubic(Mathf.Clamp((time - 0.62f) / 0.28f, 0f, 1f));       // 退场靠缩小（半透明时叠圆会露馅）
			splash.Scale = Vector2.One * (0.25f + 0.55f * Fx.OutCubic(sp)) * (1f - shrink);
			splash.Rotation = -tilt;
		});
		Fx.After(root, fly, () =>
		{
			Fx.Shake(2);
			for (int i = 0; i < 4; i++)
			{
				Vector2 p = home + Vector2.Right.Rotated(Mathf.Tau * i / 4 + 0.6f) * Fx.Rand(60f, 85f);
				Fx.After(root, 0.03 * i, () => Sparkle(layer, p, Fx.Rand(0.4f, 0.6f)));
			}
		});
		Fx.After(root, 0.86, () =>
		{
			for (int i = 0; i < 6; i++)
			{
				Vector2 p0 = home + new Vector2(Fx.Rand(-25f, 25f), Fx.Rand(-40f, 40f));
				Node2D holder = Fx.Root(layer, p0, "P5CardFeather", 0.8);
				Node2D f = Feather(holder, Fx.Rand(38f, 50f), 11f);
				float rot0 = Fx.Rand(-Mathf.Pi, Mathf.Pi), spin = Fx.Rand(-6f, 6f), sway2 = Fx.Rand(5f, 9f);
				Vector2 v = new(Fx.Rand(-90f, 90f), Fx.Rand(-130f, -60f));
				Fx.Animate(holder, 0, 0.7, t =>
				{
					holder.GlobalPosition = p0 + v * Fx.OutCubic(t) + new Vector2(14f * Mathf.Sin(t * sway2), 110f * t * t);
					f.Rotation = rot0 + spin * t;
					f.Scale = new Vector2(Mathf.Cos(t * sway2), 1f);
					holder.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t));
				});
			}
			Embers(layer, home);
		});
	}

	/// <summary>墨溅：几个大小不一的圆叠成的一团圆润墨迹 + 几颗甩出去的小墨点（不要尖刺），grow 是描边层多出来的半径。</summary>
	private static void Blobs(Node parent, float grow, Color c)
	{
		(float X, float Y, float R)[] blobs =
		{
			(0f, 0f, 62f), (-38f, 22f, 40f), (40f, 18f, 38f), (12f, -40f, 36f), (-30f, -30f, 30f), (34f, -28f, 26f),
			(-64f, -6f, 14f), (70f, 46f, 12f), (-14f, 70f, 13f), (62f, -54f, 9f), (-56f, 54f, 8f),
		};
		foreach (var (x, y, r) in blobs)
		{
			Fx.Poly(parent, Fx.Circle(r + grow, 24), c).Position = new Vector2(x, y);
		}
	}

	/// <summary>卡片烧掉时飘起来的红色碎屑。</summary>
	private static void Embers(Control layer, Vector2 at)
	{
		for (int i = 0; i < 12; i++)
		{
			Vector2 start = at + new Vector2(Fx.Rand(-45f, 45f), Fx.Rand(-65f, 65f));
			Node2D e = Fx.Root(layer, start, "P5Ember", 0.7);
			float sz = Fx.Rand(3f, 7f);
			Fx.Poly(e, new[] { new Vector2(-sz, 0f), new Vector2(0f, -sz), new Vector2(sz, 0f), new Vector2(0f, sz) }, i % 3 == 0 ? Colors.White : Red);
			Vector2 v = new(Fx.Rand(-40f, 40f), Fx.Rand(-140f, -60f));
			float d = Fx.Rand(0f, 0.15f);
			Fx.Animate(e, d, 0.5, t =>
			{
				e.GlobalPosition = start + v * Fx.OutCubic(t);
				e.Rotation = t * 6f;
				e.Modulate = new Color(1f, 1f, 1f, 1f - t);
			});
		}
	}
}
