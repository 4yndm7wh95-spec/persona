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
	/// 防御（Joker 获得格挡时）："撕开空间"的盾。身前先闪一道竖直白线（空间被划开），
	/// 接着沿这条线横向撕开成一面细长的菱形红光盾：边缘是锯齿状的撕纸边（粗黑描线 + 白色内沿），里面是半透明的红，
	/// 一道白光自下而上扫过，身后网点一圈圈扩开；停一拍后盾"啪"地合回成一条线、白闪、崩出几片三角碎片。
	/// 盾在 Joker 身前约 1 个身位，不挡人也不挡敌人。chest = 胸口，facing = 敌人方向。
	/// </summary>
	public static void Guard(Control layer, Vector2 chest, float facing = 1f)
	{
		Vector2 at = chest + new Vector2(150f * facing, 40f);
		Node2D root = Fx.Root(layer, at, "P5Guard", 1.0);
		root.Scale = new Vector2(facing, 1f);
		root.Rotation = -0.12f * facing;

		// 网点圈
		Node2D dots = new();
		root.AddChild(dots);
		var dotList = new List<(Polygon2D P, float R, float A)>();
		for (int ring = 0; ring < 3; ring++)
		{
			int n = 12 + ring * 6;
			for (int i = 0; i < n; i++)
			{
				Polygon2D d = Fx.Poly(dots, Fx.Circle(4f - ring, 8), new Color(Red, 0.8f));
				dotList.Add((d, 70f + ring * 26f, Mathf.Tau * i / n + ring * 0.25f));
			}
		}

		// 盾：锯齿撕边的细长菱形，三层（黑描线 / 白内沿 / 半透明红芯），横向从 0 撕开
		const float H = 250f, W = 64f;
		Vector2[] edge = TornDiamond(W, H, 22);
		Node2D shield = new();
		root.AddChild(shield);
		Fx.Poly(shield, Grow(edge, 9f), Ink);
		Fx.Poly(shield, Grow(edge, 4f), Colors.White);
		Polygon2D core = Fx.Poly(shield, edge, new Color(Red, 0.78f));
		// 盾面上两道斜的深红条纹（P5 的斜切质感）
		Fx.Poly(shield, new[] { new Vector2(-W * 0.5f, 40f), new Vector2(W * 0.2f, -30f), new Vector2(W * 0.35f, -10f), new Vector2(-W * 0.35f, 60f) }, new Color(0.45f, 0.02f, 0.05f, 0.7f));
		Fx.Poly(shield, new[] { new Vector2(-W * 0.32f, -30f), new Vector2(W * 0.22f, -80f), new Vector2(W * 0.28f, -64f), new Vector2(-W * 0.3f, -14f) }, new Color(0.45f, 0.02f, 0.05f, 0.7f));

		// 划开空间的白线 + 扫光
		Polygon2D slit = Fx.Poly(root, new[] { new Vector2(0f, -H * 0.62f), new Vector2(3f, 0f), new Vector2(0f, H * 0.62f), new Vector2(-3f, 0f) }, Colors.White, additive: true);
		Polygon2D sweep = Fx.Poly(shield, new[] { new Vector2(-W, -6f), new Vector2(W, -16f), new Vector2(W, -4f), new Vector2(-W, 6f) }, new Color(1f, 1f, 1f, 0.9f), additive: true);

		const float total = 0.66f;
		Fx.Animate(root, 0, total, t =>
		{
			float time = t * total;
			// 0~0.07 白线从中间往上下伸开
			float s0 = Fx.OutCubic(Mathf.Clamp(time / 0.07f, 0f, 1f));
			slit.Scale = new Vector2(1f, s0);
			slit.Modulate = new Color(1f, 1f, 1f, time < 0.12f ? 1f : Mathf.Clamp(1f - (time - 0.12f) / 0.08f, 0f, 1f));
			// 0.06~0.18 横向撕开（过冲一点再回来）；0.52~0.6 合回成线
			float open = Mathf.Clamp((time - 0.06f) / 0.12f, 0f, 1f);
			float over = open >= 1f ? Mathf.Exp(-8f * (time - 0.18f)) * Mathf.Sin((time - 0.18f) * 30f) * 0.15f : 0f;
			float close = Fx.InCubic(Mathf.Clamp((time - 0.52f) / 0.08f, 0f, 1f));
			shield.Visible = open > 0f && close < 1f;
			shield.Scale = new Vector2(Mathf.Max(0.02f, Fx.OutCubic(open) * (1f + over) * (1f - close)), 1f + 0.06f * close);
			core.Color = new Color(Red, 0.78f + 0.12f * Mathf.Sin(time * 40f));          // 轻微闪烁
			// 0.16~0.34 扫光自下而上
			float sw = Mathf.Clamp((time - 0.16f) / 0.18f, 0f, 1f);
			sweep.Visible = sw > 0f && sw < 1f;
			float sy = Mathf.Lerp(H * 0.4f, -H * 0.4f, Fx.OutCubic(sw));
			float hw = W / 2f * Mathf.Sin((sy / H + 0.5f) * Mathf.Pi) - 3f;             // 只在盾里面：宽度跟着菱形走
			sweep.Polygon = new[] { new Vector2(-hw, 5f), new Vector2(hw, -9f), new Vector2(hw, 1f), new Vector2(-hw, 13f) };
			sweep.Position = new Vector2(0f, sy);
			// 网点
			float dt = Mathf.Clamp((time - 0.1f) / 0.5f, 0f, 1f);
			foreach (var (d, r, a) in dotList)
			{
				d.Position = new Vector2(Mathf.Cos(a) * r * 0.55f, Mathf.Sin(a) * r * 1.25f) * (0.6f + 0.7f * Fx.OutCubic(dt));
				d.Modulate = new Color(1f, 1f, 1f, dt <= 0f ? 0f : 1f - dt);
			}
		});
		Fx.After(root, 0.58, () =>
		{
			Node2D f = Fx.Root(layer, at, "P5GuardFlash", 0.2);
			Polygon2D line = Fx.Poly(f, new[] { new Vector2(0f, -H * 0.6f), new Vector2(7f, 0f), new Vector2(0f, H * 0.6f), new Vector2(-7f, 0f) }, Colors.White, additive: true);
			f.Rotation = -0.12f * facing;
			Fx.Animate(f, 0, 0.14, t => { line.Scale = new Vector2(1f + 3f * t, 1f - 0.3f * t); line.Modulate = new Color(1f, 1f, 1f, 1f - t); });
			Shatter(layer, at, facing);
		});
		Fx.After(root, 0.12, () => Fx.Shake(1));
	}

	/// <summary>细长菱形，边上带锯齿（撕纸边）：上下两个尖，n 个锯齿点每边。</summary>
	private static Vector2[] TornDiamond(float w, float h, int n)
	{
		var pts = new List<Vector2>();
		for (int side = 0; side < 2; side++)
		{
			for (int i = side == 0 ? 0 : 1; i <= (side == 0 ? n : n - 1); i++)       // 两个尖只出现一次（重复点会让三角剖分失败）
			{
				float u = (float)i / n;                                   // 从上尖到下尖
				float y = Mathf.Lerp(-h / 2f, h / 2f, side == 0 ? u : 1f - u);
				float x = w / 2f * Mathf.Sin(u * Mathf.Pi) * (side == 0 ? 1f : -1f);
				if (i > 0 && i < n)
				{
					x += (i % 2 == 0 ? 1f : -1f) * Fx.Rand(2f, 7f) * (side == 0 ? 1f : -1f) * Mathf.Sin(u * Mathf.Pi);
				}
				pts.Add(new Vector2(x, y));
			}
		}
		return pts.ToArray();
	}

	/// <summary>多边形往外扩 d（按到中心的方向，形状近似时够用）。</summary>
	private static Vector2[] Grow(Vector2[] pts, float d)
	{
		var o = new Vector2[pts.Length];
		for (int i = 0; i < pts.Length; i++)
		{
			Vector2 n = pts[i].Length() < 0.01f ? Vector2.Zero : pts[i].Normalized();
			o[i] = pts[i] + new Vector2(n.X * d * 1.4f, n.Y * d * 0.5f);
		}
		return o;
	}

	/// <summary>平行四边形，中心在原点：宽 w、高 h，顶边往右错开 skew。</summary>
	private static Vector2[] Para(float w, float h, float skew) =>
		new[] { new Vector2(-w / 2f - skew / 2f, h / 2f), new Vector2(w / 2f - skew / 2f, h / 2f),
			new Vector2(w / 2f + skew / 2f, -h / 2f), new Vector2(-w / 2f + skew / 2f, -h / 2f) };

	/// <summary>盾合上时：十几片红 / 黑 / 白三角片从那条线上往敌人方向崩开，转着落下。</summary>
	private static void Shatter(Control layer, Vector2 at, float facing)
	{
		Color[] cols = { Ink, Red, Colors.White, Red };
		for (int i = 0; i < 14; i++)
		{
			Vector2 start = at + new Vector2(Fx.Rand(-15f, 25f) * facing, Fx.Rand(-110f, 110f));
			Node2D shard = Fx.Root(layer, start, "P5GuardShard", 0.6);
			float sz = Fx.Rand(10f, 22f);
			Fx.Poly(shard, new[] { new Vector2(-sz, -sz * 0.5f), new Vector2(sz, -sz * 0.2f), new Vector2(-sz * 0.2f, sz * 0.7f) }, cols[i % cols.Length]);
			Vector2 v = new Vector2(facing * Fx.Rand(60f, 220f), Fx.Rand(-180f, -40f));
			float spin = Fx.Rand(-12f, 12f);
			Fx.Animate(shard, 0, 0.5, t =>
			{
				shard.GlobalPosition = start + v * Fx.OutCubic(t) + new Vector2(0f, 260f * t * t);
				shard.Rotation = spin * t;
				shard.Scale = Vector2.One * (1f - 0.5f * t);
				shard.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t));
			});
		}
	}

	/// <summary>
	/// 预告信（"中和"等施加虚弱的牌命中时）：一张红色预告信从右上方旋转着飞下来，"啪"地钉在敌人身上（压扁回弹 + 白闪 + 一圈速度线），
	/// 微微晃一会儿，然后从边缘燃成红色碎屑消失。at = 敌人的 VfxSpawnPosition。
	/// </summary>
	public static void CallingCard(Control layer, Vector2 at)
	{
		if (Fx.Tex("calling_card") is not Texture2D tex)
		{
			return;
		}
		Vector2 home = at + new Vector2(-30f, -70f);
		Node2D root = Fx.Root(layer, home, "P5CallingCard", 1.3);
		Sprite2D card = Fx.Sprite(root, tex, additive: false);
		Sprite2D flash = Fx.Sprite(root, tex, additive: true);
		float k = 118f / tex.GetHeight();
		card.Scale = flash.Scale = Vector2.One * k;
		float tilt = Fx.Rand(-0.22f, -0.08f);

		// 钉上去那一下的速度线：几条白色短线从卡片往外射
		Node2D lines = new();
		root.AddChild(lines);
		var rays = new List<(Polygon2D P, Vector2 D)>();
		for (int i = 0; i < 8; i++)
		{
			Vector2 d = Vector2.Right.Rotated(Mathf.Tau * i / 8 + Fx.Rand(-0.2f, 0.2f));
			rays.Add((Fx.Poly(lines, new Vector2[3], Colors.White), d));
		}

		const float total = 1.2f;
		Fx.Animate(root, 0, total, t =>
		{
			float time = t * total;
			// 飞入 0.14s：从右上方 260px 外转着飞来（转 1.5 圈），越近越快
			float fly = Mathf.Clamp(time / 0.14f, 0f, 1f);
			float land = Mathf.Clamp((time - 0.14f) / 0.2f, 0f, 1f);
			float sq = land <= 0f ? 0f : Mathf.Exp(-6f * land) * Mathf.Cos(land * 16f);
			root.GlobalPosition = home + new Vector2(240f, -200f) * (1f - Fx.InCubic(fly)) + new Vector2(0f, 6f * Mathf.Sin(time * 5f) * land);
			root.Rotation = tilt + (1f - Fx.InCubic(fly)) * Mathf.Tau * 1.5f + 0.05f * Mathf.Sin(time * 6f) * land;
			float s = Mathf.Lerp(0.6f, 1f, fly);
			root.Scale = new Vector2(s * (1f + 0.25f * sq), s * (1f - 0.2f * sq));
			flash.Modulate = new Color(1f, 1f, 1f, fly < 1f ? 0f : Mathf.Clamp(1f - land / 0.5f, 0f, 1f));
			foreach (var (p, d) in rays)
			{
				float u = Mathf.Clamp((time - 0.14f) / 0.16f, 0f, 1f);
				p.Visible = u > 0f && u < 1f;
				float a = 60f + 70f * Fx.OutCubic(u), b = a + 50f * (1f - u);
				Vector2 n = new Vector2(-d.Y, d.X) * 3f * (1f - u);
				p.Polygon = new[] { d * a + n, d * b, d * a - n };
			}
			// 余韵：0.85s 起从边缘烧掉（整体缩小、发红、透明）
			float burn = Mathf.Clamp((time - 0.85f) / 0.3f, 0f, 1f);
			card.Modulate = new Color(1f, 1f - 0.6f * burn, 1f - 0.6f * burn, 1f - Fx.InCubic(burn));
			if (burn > 0f)
			{
				root.Scale *= 1f - 0.3f * burn;
			}
		});
		Fx.After(root, 0.14, () => Fx.Shake(2));
		Fx.After(root, 0.9, () => Embers(layer, home));
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
