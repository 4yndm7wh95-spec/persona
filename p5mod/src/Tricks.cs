using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P5Joker;

/// <summary>
/// Joker 自己的花活（不召唤人格面具的技能牌）：格挡、闪避残影、装填子弹、预告信、子弹时间、准星、烟雾、锁链、大衣翻卷。
/// 每种都按牌给不同的参数 / 组合（CardFx 的表），尽量不让两张牌长得一样。
/// </summary>
public static class Tricks
{
	private static readonly Color Red = new(0.92f, 0.05f, 0.12f), Brass = new(0.95f, 0.75f, 0.3f), Ink = new(0.06f, 0.02f, 0.1f);

	private static Vector2 Chest(NCreature joker) => Joker.PosePoint(joker, new Vector2(800f, 600f));

	/// <summary>格挡：原版 DAMAGE_DATA 的「B L O C K」五个字母依次砸在 Joker 身前斜着排开 + 一道白色斜切护盾线。
	/// style：0 普通，1 招架（多一个白色 X 闪），2 硬挡（字母更大、震屏）。</summary>
	public static void Guard(NCreature joker, int style = 0)
	{
		Control? front = Joker.FxLayer();
		if (front == null)
		{
			return;
		}
		float side = Joker.Facing(joker);
		Node2D root = Fx.Root(front, Chest(joker) + new Vector2(side * 120f, -60f), "P5Guard", 1.0);
		float k = style == 2 ? 1.25f : 1f;
		string[] letters = { "block_b", "block_l", "block_o", "block_c", "block_k" };
		for (int i = 0; i < letters.Length; i++)
		{
			if (Fx.Tex(letters[i]) is not Texture2D tex)
			{
				continue;
			}
			Sprite2D s = Fx.Sprite(root, tex, additive: false);
			Vector2 p = new(side * (-110f + 52f * i) * k, (i % 2 == 0 ? -6f : 8f) - 14f * i);
			float rot = (i % 2 == 0 ? -0.12f : 0.1f);
			s.Visible = false;
			Fx.Animate(s, i * 0.035, 0.75, t =>
			{
				s.Visible = true;
				float pop = Mathf.Clamp(t / 0.12f, 0f, 1f);
				s.Scale = Vector2.One * 0.3f * k * (1f + 0.8f * (1f - Fx.OutCubic(pop)));
				s.Position = p + new Vector2(0f, -20f * Fx.InCubic(Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f)));
				s.Rotation = rot;
				s.Modulate = new Color(1, 1, 1, 1f - Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f));
			});
		}
		// 护盾线：一道斜的白色硬边线，从下往上扫出
		Line2D shield = new() { Width = 7f, DefaultColor = Colors.White };
		root.AddChild(shield);
		Fx.Animate(shield, 0, 0.4, t =>
		{
			Vector2 a = new(side * 20f, 190f), b = new(side * 70f, -170f);
			shield.Points = new[] { a, a.Lerp(b, Fx.OutCubic(Mathf.Min(1f, t * 3f))) };
			shield.Width = 9f * (1f - t) + 1f;
			shield.Modulate = new Color(1, 1, 1, 1f - Fx.InCubic(t));
		});
		if (style == 1)
		{
			foreach (float a in new[] { 0.8f, -0.8f })
			{
				Polygon2D x = Fx.Poly(root, new[] { new Vector2(-90f, -6f), new Vector2(90f, -2f), new Vector2(90f, 2f), new Vector2(-90f, 6f) }, Colors.White, additive: true);
				x.Rotation = a;
				Fx.Animate(x, 0, 0.22, t =>
				{
					x.Scale = new Vector2(0.4f + 0.9f * Fx.OutCubic(t), 1f + 1.5f * (1f - t));
					x.Modulate = new Color(1, 1, 1, 1f - t);
				});
			}
		}
		if (style == 2)
		{
			Fx.Shake(ShakeStrength.Weak);
		}
	}

	/// <summary>残影：Joker 的半透明影子沿 offsets 排开、依次淡出（闪避、后空翻、疾冲、模糊）。</summary>
	public static void Afterimages(NCreature joker, IList<Vector2> offsets, Color tint, double life = 0.45, float delayStep = 0.03f)
	{
		Control? back = Joker.FxLayer(back: true);
		if (back == null || Fx.Tex("joker_ghost") is not Texture2D ghost)
		{
			return;
		}
		float side = Joker.Facing(joker);
		// joker_ghost.png = idle_a3 最终画布裁到 alpha 范围（tools/p5/build_fx_assets.py 的 ghost_box）再缩到 0.3
		Vector2 boxCenterFromFeet = new((312f + 1246f) / 2f - 815f, (96f + 1632f) / 2f - 1622f);
		Vector2 feetOnScreen = joker.GlobalPosition;
		Vector2 bodyScale = (Joker.PosePoint(joker, new Vector2(815f, 0f)) - Joker.PosePoint(joker, new Vector2(815f, 1622f))).Abs() / 1622f;
		float px = bodyScale.Y / 0.3f;
		for (int i = 0; i < offsets.Count; i++)
		{
			Node2D root = Fx.Root(back, feetOnScreen + new Vector2(offsets[i].X * side, offsets[i].Y), "P5Ghost", life + i * delayStep + 0.1);
			Sprite2D s = Fx.Sprite(root, ghost, additive: false);
			s.Position = new Vector2(boxCenterFromFeet.X * side, boxCenterFromFeet.Y) * bodyScale.Y;
			s.Scale = new Vector2(side, 1f) * px;
			s.Visible = false;
			float a0 = 0.65f * (1f - (float)i / (offsets.Count + 1));
			Fx.Animate(s, i * delayStep, life, t =>
			{
				s.Visible = true;
				s.Modulate = new Color(tint.R, tint.G, tint.B, a0 * (1f - t));
			});
		}
	}

	/// <summary>装填：count 发黄铜子弹从 Joker 身边绕一圈飞进持枪手里，每进一发闪一下。
	/// style：0 普通，1 墨黑（墨刃），2 幽蓝（幻影刃），3 从卡牌变成子弹（钢铁风暴）。</summary>
	public static void Reload(NCreature joker, int count, int style = 0)
	{
		Control? front = Joker.FxLayer();
		if (front == null)
		{
			return;
		}
		float side = Joker.Facing(joker);
		Vector2 hand = Joker.PosePoint(joker, new Vector2(700f, 900f));
		Node2D root = Fx.Root(front, hand, "P5Reload", 1.4);
		Color body = style switch { 1 => Ink, 2 => new Color(0.5f, 0.8f, 1f, 0.8f), _ => Brass };
		Color tip = style == 1 ? new Color(0.6f, 0.2f, 1f) : style == 2 ? Colors.White : new Color(0.75f, 0.45f, 0.2f);
		count = Mathf.Clamp(count, 1, 8);
		for (int i = 0; i < count; i++)
		{
			Node2D b = new();
			root.AddChild(b);
			if (style == 3 && Fx.Tex("calling_card") is Texture2D card)
			{
				Sprite2D c = Fx.Sprite(b, card, additive: false);
				c.Scale = Vector2.One * 0.22f;
				c.Name = "card";
			}
			Fx.Poly(b, new[] { new Vector2(-14f, -5f), new Vector2(6f, -5f), new Vector2(6f, 5f), new Vector2(-14f, 5f) }, body);
			Fx.Poly(b, new[] { new Vector2(6f, -5f), new Vector2(14f, 0f), new Vector2(6f, 5f) }, tip);
			float a0 = Mathf.Tau * i / count + Fx.Rand(-0.2f, 0.2f);
			float r0 = Fx.Rand(130f, 190f);
			b.Visible = false;
			Fx.Animate(b, i * 0.06, 0.42, t =>
			{
				b.Visible = true;
				float e = Fx.InCubic(t);
				float a = a0 + 3.5f * t * side;
				b.Position = Vector2.Right.Rotated(a) * r0 * (1f - e);
				b.Rotation = a + Mathf.Pi / 2f * side;
				b.Scale = Vector2.One * (1.4f - 0.6f * e);
				if (b.GetNodeOrNull<Sprite2D>("card") is Sprite2D c)
				{
					c.Modulate = new Color(1, 1, 1, 1f - Mathf.Clamp(t * 2f, 0f, 1f));       // 卡牌先烧成子弹
				}
			});
			Fx.After(root, i * 0.06 + 0.42, () => Click(root, style == 1 ? new Color(0.6f, 0.2f, 1f) : Colors.White));
		}
	}

	private static void Click(Node2D root, Color color)
	{
		Polygon2D star = Fx.Poly(root, Star(26f), color, additive: true);
		Fx.Animate(star, 0, 0.12, t =>
		{
			star.Scale = Vector2.One * (0.6f + 0.8f * t);
			star.Modulate = new Color(1, 1, 1, 1f - t);
		});
	}

	public static Vector2[] Star(float r)
	{
		Vector2[] pts = new Vector2[8];
		for (int i = 0; i < 8; i++)
		{
			pts[i] = Vector2.Right.Rotated(Mathf.Tau * i / 8f) * (i % 2 == 0 ? r : r * 0.18f);
		}
		return pts;
	}

	/// <summary>预告信：P5 的红色预告信从 Joker 手里甩出去（转着飞），to 为空就往身前上方飞；fan 张的话排成扇形。</summary>
	public static void CallingCards(NCreature joker, int count, Vector2? to = null, float spread = 0.5f)
	{
		Control? front = Joker.FxLayer();
		if (front == null || Fx.Tex("calling_card") is not Texture2D card)
		{
			return;
		}
		float side = Joker.Facing(joker);
		Vector2 from = Joker.PosePoint(joker, new Vector2(700f, 800f));
		Node2D root = Fx.Root(front, from, "P5CallingCard", 1.2);
		count = Mathf.Clamp(count, 1, 6);
		for (int i = 0; i < count; i++)
		{
			Sprite2D c = Fx.Sprite(root, card, additive: false);
			float a = -0.6f + (count == 1 ? 0f : spread * ((float)i / (count - 1) - 0.5f));
			Vector2 end = to.HasValue ? root.ToLocal(to.Value) + new Vector2(Fx.Rand(-30f, 30f), Fx.Rand(-40f, 40f))
				: new Vector2(side * Mathf.Cos(a), Mathf.Sin(a)) * 330f;
			float spin = Fx.Rand(8f, 14f) * side;
			c.Visible = false;
			Fx.Animate(c, i * 0.05, 0.6, t =>
			{
				c.Visible = true;
				float move = Fx.OutCubic(Mathf.Min(1f, t / 0.5f));
				c.Position = end * move;
				c.Rotation = spin * (1f - move) * 0.3f + (to.HasValue ? 0.3f * side : 0.15f * side);
				c.Scale = new Vector2(Mathf.Cos(t * 18f) * 0.32f, 0.32f);
				c.Modulate = new Color(1, 1, 1, 1f - Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f));
			});
		}
	}

	/// <summary>子弹时间：整个画面罩一层冷灰蓝、中间一圈大钟面和指针转一圈，四周白色速度线往里收。</summary>
	public static void TimeStop(Control layer, double seconds = 0.9)
	{
		Vector2 screen = Fx.Screen(layer);
		ColorRect tint = new() { Color = new Color(0.55f, 0.7f, 0.85f, 0f), Size = screen * 1.2f, MouseFilter = Control.MouseFilterEnum.Ignore };
		layer.AddChild(tint);
		tint.GlobalPosition = -screen * 0.1f;
		Fx.FreeAfter(tint, seconds + 0.1);
		Node2D root = Fx.Root(layer, screen * 0.5f, "P5TimeStop", seconds + 0.1);
		Line2D face = new() { Closed = true, Width = 6f, DefaultColor = Colors.White, Points = Fx.Circle(screen.Y * 0.32f, 64) };
		Line2D hand = new() { Width = 8f, DefaultColor = Red };
		root.AddChild(face);
		root.AddChild(hand);
		for (int i = 0; i < 12; i++)
		{
			Vector2 d = Vector2.Right.Rotated(Mathf.Tau * i / 12f);
			root.AddChild(new Line2D { Points = new[] { d * screen.Y * 0.28f, d * screen.Y * 0.32f }, Width = i % 3 == 0 ? 8f : 4f, DefaultColor = Colors.White });
		}
		Fx.Animate(root, 0, seconds, t =>
		{
			float a = t < 0.15f ? t / 0.15f : t > 0.75f ? 1f - (t - 0.75f) / 0.25f : 1f;
			tint.Color = new Color(0.55f, 0.7f, 0.85f, 0.35f * a);
			root.Modulate = new Color(1, 1, 1, 0.8f * a);
			root.Scale = Vector2.One * (1.15f - 0.15f * Fx.OutCubic(Mathf.Min(1f, t * 4f)));
			float ang = -Mathf.Pi / 2f + Mathf.Tau * Fx.OutCubic(t);
			hand.Points = new[] { Vector2.Zero, Vector2.Right.Rotated(ang) * screen.Y * 0.26f };
		});
	}

	/// <summary>准星：P5 的锁定框（四个红色直角 + 一圈细线），从大往目标身上收；label 不为空时上面弹出原版 WEAK 字样。</summary>
	public static void Reticle(NCreature target, bool weak = false, float delay = 0f)
	{
		Control? front = Joker.FxLayer();
		if (front == null)
		{
			return;
		}
		Node2D root = Fx.Root(front, target.VfxSpawnPosition, "P5Reticle", delay + 1.0);
		List<Line2D> corners = new();
		for (int i = 0; i < 4; i++)
		{
			Line2D c = new() { Width = 8f, DefaultColor = Red, Visible = false };
			root.AddChild(c);
			corners.Add(c);
		}
		Line2D ring = new() { Closed = true, Width = 3f, DefaultColor = Colors.White, Visible = false };
		root.AddChild(ring);
		Fx.Animate(root, delay, 0.7, t =>
		{
			float r = 70f + 160f * (1f - Fx.OutCubic(Mathf.Min(1f, t * 2.5f)));
			float a = 1f - Mathf.Clamp((t - 0.75f) / 0.25f, 0f, 1f);
			for (int i = 0; i < 4; i++)
			{
				Vector2 d = new(i % 2 == 0 ? -1f : 1f, i < 2 ? -1f : 1f);
				Vector2 p = d * r;
				corners[i].Points = new[] { p - new Vector2(d.X * 40f, 0f), p, p - new Vector2(0f, d.Y * 40f) };
				corners[i].Visible = true;
				corners[i].Modulate = new Color(1, 1, 1, a);
			}
			ring.Visible = true;
			ring.Points = Fx.Circle(r * 0.75f);
			ring.Rotation = t * 3f;
			ring.Modulate = new Color(1, 1, 1, a * 0.8f);
			root.Rotation = 0.6f * (1f - Fx.OutCubic(Mathf.Min(1f, t * 2.5f)));
		});
		if (weak && Fx.Tex("weak") is Texture2D w)
		{
			Sprite2D s = Fx.Sprite(root, w, additive: false);
			s.Visible = false;
			Fx.Animate(s, delay + 0.3, 0.6, t =>
			{
				s.Visible = true;
				s.Position = new Vector2(0f, -150f);
				s.Scale = Vector2.One * 0.2f * (1f + 0.6f * (1f - Fx.OutCubic(Mathf.Min(1f, t * 6f))));
				s.Rotation = -0.08f;
				s.Modulate = new Color(1, 1, 1, 1f - Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f));
			});
		}
	}

	/// <summary>烟幕：Joker 脚下炸开一团灰黑色烟（原版的云团贴图），往两边散。</summary>
	public static void Smoke(NCreature joker, Color tint, int puffs = 7, float spread = 1f)
	{
		Control? front = Joker.FxLayer();
		if (front == null || Fx.Tex("smoke") is not Texture2D smoke)
		{
			return;
		}
		Node2D root = Fx.Root(front, joker.GlobalPosition + new Vector2(0f, -60f), "P5Smoke", 1.1);
		for (int i = 0; i < puffs; i++)
		{
			Sprite2D s = Fx.Sprite(root, smoke, additive: false);
			Vector2 dir = new Vector2(Fx.Rand(-1f, 1f), Fx.Rand(-0.6f, 0.1f)).Normalized();
			float dist = Fx.Rand(60f, 170f) * spread;
			Fx.Animate(s, i * 0.02, 0.9, t =>
			{
				s.Position = dir * dist * Fx.OutCubic(t) + new Vector2(0f, -60f * t);
				s.Scale = Vector2.One * (1.4f + 1.6f * Fx.OutCubic(t)) * spread;
				s.Rotation = t * 0.8f;
				s.Modulate = new Color(tint.R, tint.G, tint.B, 0.85f * (1f - Fx.InCubic(t)));
			});
		}
	}

	/// <summary>锁链：原版召唤用的蓝色锁链贴图染成红黑，从四周甩到目标身上缠一下。</summary>
	public static void Chains(NCreature target, int count = 3)
	{
		Control? front = Joker.FxLayer();
		if (front == null || Fx.Tex("chain") is not Texture2D chain)
		{
			return;
		}
		Node2D root = Fx.Root(front, target.VfxSpawnPosition, "P5Chains", 0.9);
		for (int i = 0; i < count; i++)
		{
			Sprite2D c = Fx.Sprite(root, chain, additive: false);
			float a = Mathf.Tau * i / count + 0.4f;
			c.Rotation = a + Mathf.Pi / 2f;
			Fx.Animate(c, i * 0.05, 0.7, t =>
			{
				float e = Fx.OutCubic(Mathf.Min(1f, t * 2.5f));
				c.Position = Vector2.Right.Rotated(a) * 260f * (1f - e) + Vector2.Right.Rotated(a) * 40f;
				c.Scale = new Vector2(1.2f, 1.0f);
				c.Modulate = new Color(0.9f, 0.15f, 0.2f, 1f - Mathf.Clamp((t - 0.6f) / 0.4f, 0f, 1f));
			});
		}
	}

	/// <summary>大衣翻卷：两片黑色的弧从 Joker 身后卷过身前（斗篷与匕首、潜行），带红色里衬的边。</summary>
	public static void CoatSwirl(NCreature joker)
	{
		Control? front = Joker.FxLayer();
		if (front == null)
		{
			return;
		}
		Node2D root = Fx.Root(front, Chest(joker), "P5Coat", 0.7);
		root.Scale = new Vector2(Joker.Facing(joker), 1f);
		for (int k = 0; k < 2; k++)
		{
			Polygon2D cloth = Fx.Poly(root, new Vector2[3], new Color(0.05f, 0.04f, 0.06f));
			Line2D lining = new() { Width = 5f, DefaultColor = Red };
			root.AddChild(lining);
			float r = 150f + 40f * k, dir = k == 0 ? 1f : -1f;
			Fx.Animate(cloth, k * 0.08, 0.45, t =>
			{
				float head = Fx.OutCubic(t), tail = Fx.InCubic(t);
				List<Vector2> outer = new(), inner = new();
				for (int i = 0; i <= 14; i++)
				{
					float u = tail + (head - tail) * i / 14f;
					float a = dir * (-2.6f + 4.2f * u);
					outer.Add(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.55f));
					inner.Add(new Vector2(Mathf.Cos(a) * (r - 46f * Mathf.Sin(Mathf.Pi * i / 14f)), Mathf.Sin(a) * (r - 46f * Mathf.Sin(Mathf.Pi * i / 14f)) * 0.55f));
				}
				inner.Reverse();
				List<Vector2> poly = new(outer);
				poly.AddRange(inner);
				cloth.Polygon = poly.ToArray();
				lining.Points = outer.ToArray();
			});
		}
	}

	/// <summary>阴影：Joker 脚下摊开一片黑紫色的影子（潜影、暗影步、阴影融合），几缕黑烟往上飘。</summary>
	public static void Shadow(NCreature joker, double seconds = 0.8)
	{
		Control? back = Joker.FxLayer(back: true);
		if (back == null)
		{
			return;
		}
		Node2D root = Fx.Root(back, joker.GlobalPosition, "P5Shadow", seconds + 0.1);
		Polygon2D pool = Fx.Poly(root, Fx.Circle(1f, 32), Ink);
		Fx.Animate(pool, 0, seconds, t =>
		{
			Vector2[] c = Fx.Circle(200f * Fx.OutCubic(Mathf.Min(1f, t * 3f)), 32);
			for (int i = 0; i < c.Length; i++)
			{
				c[i] = new Vector2(c[i].X, c[i].Y * 0.22f);
			}
			pool.Polygon = c;
			pool.Modulate = new Color(1, 1, 1, 0.8f * (1f - Fx.InCubic(t)));
		});
		if (Fx.Tex("curse_ghost") is Texture2D wisp)
		{
			for (int i = 0; i < 5; i++)
			{
				Sprite2D w = Fx.Sprite(root, wisp, additive: false);
				float x = Fx.Rand(-150f, 150f);
				Fx.Animate(w, i * 0.07, seconds * 0.8, t =>
				{
					w.Position = new Vector2(x, -300f * Fx.OutCubic(t));
					w.Modulate = new Color(0.1f, 0.05f, 0.15f, 0.8f * Mathf.Sin(Mathf.Pi * t));
					w.Scale = Vector2.One * 1.2f;
				});
			}
		}
	}
}
