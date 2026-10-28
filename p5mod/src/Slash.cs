using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P5Joker;

/// <summary>近战命中的几种样子（每张攻击牌在 CardFx 里挑一种，参数也各不相同）。</summary>
public enum Blade
{
	Cut,        // 反手上撩的一道细斩痕（打击）
	Cross,      // 两道交叉（掠夺者、背刺）
	Heavy,      // 粗一号的斩痕 + 白闪 + 中震（暗杀、猛扑）
	Thrust,     // 横向一条直的刺线 + 一个穿透的小环（精准切割、穿刺）
	Flurry,     // 每一击换一个角度的小斩痕（终结技、连斩）
	Low,        // 贴地扫过的弧（扫腿）
	Blunt,      // 拳脚：一圈放射状的速度线 + 冲击环，没有刀痕（偷袭拳）
	Echo        // 横贯画面的一道长斩，所有敌人一起（回响斩、空翻）
}

/// <summary>
/// 近战命中（docs/p5/FX_PLAN.md 第 1 节）。用户 2026-10-28：不要刺猬球一样的爆点，特效不能挡住敌人。
/// 前景只有细线（斩痕、速度线），冲击环和红黑碎片都在敌人身后从身体边缘往外飞。
/// </summary>
public static class Slash
{
	private static readonly Color Red = new(0.92f, 0.05f, 0.12f), Black = new(0.04f, 0.02f, 0.03f);
	private static int _flurry;

	public static void Play(NCreature joker, List<NCreature> targets, Blade blade = Blade.Cut, float size = 1f)
	{
		Control? front = Joker.FxLayer();
		Control? back = Joker.FxLayer(back: true) ?? front;
		if (front == null || back == null)
		{
			return;
		}
		float face = Joker.Facing(joker);
		float s = size * (targets.Count > 1 ? 0.85f : 1f);
		if (blade == Blade.Echo)
		{
			Echo(front, back, targets, face);
			return;
		}
		foreach (NCreature t in targets)
		{
			Vector2 at = t.VfxSpawnPosition;
			switch (blade)
			{
				case Blade.Cross:
					Cut(front, at, face, s, -0.55f, 12f);
					Fx.After(front, 0.06, () => Cut(front, at, face, s, 0.55f, 12f, flipArc: true));
					break;
				case Blade.Heavy:
					Cut(front, at, face, s * 1.25f, -0.62f, 20f);
					Flash(front, at, 0.5f);
					break;
				case Blade.Thrust:
					Thrust(front, at, face, s);
					break;
				case Blade.Flurry:
					_flurry++;
					Cut(front, at, face, s * 0.8f, (_flurry % 3 - 1) * 0.7f + Fx.Rand(-0.15f, 0.15f), 9f, flipArc: _flurry % 2 == 0);
					break;
				case Blade.Low:
					Cut(front, at + new Vector2(0f, (t.Hitbox?.Size.Y ?? 200f) * 0.3f), face, s * 1.1f, -0.08f, 11f);
					break;
				case Blade.Blunt:
					Blunt(front, at, face, s);
					break;
				default:
					Cut(front, at, face, s, -0.55f, 12f);
					break;
			}
			if (blade != Blade.Blunt)
			{
				Behind(back, at, face, s * (blade == Blade.Heavy ? 1.3f : 1f), blade == Blade.Flurry ? 5 : 9);
			}
		}
		Fx.Shake(blade == Blade.Heavy ? ShakeStrength.Medium : ShakeStrength.Weak);
	}

	/// <summary>一道细斩痕：白芯红边，0.06 秒扫过，两端甩出原版「効果線」速度线。angle 是刀路方向（弧度，负 = 往右上）。</summary>
	private static void Cut(Control layer, Vector2 at, float face, float s, float angle, float width, bool flipArc = false)
	{
		Node2D root = Fx.Root(layer, at, "P5Slash", 0.5);
		root.Scale = new Vector2(face * s, s);
		root.Rotation = face * (angle + Fx.Rand(-0.08f, 0.08f));
		Polygon2D red = Fx.Poly(root, new Vector2[3], Red);
		Polygon2D white = Fx.Poly(root, new Vector2[3], Colors.White);
		float bend = flipArc ? -24f : 24f;
		Fx.Animate(root, 0, 0.26, t =>
		{
			float head = Fx.OutCubic(Mathf.Clamp(t / 0.23f, 0f, 1f));
			float tail = Fx.OutCubic(Mathf.Clamp((t - 0.2f) / 0.55f, 0f, 1f));
			red.Polygon = Streak(head, tail, width, bend);
			white.Polygon = Streak(head, tail, width * 0.42f, bend);
			red.Modulate = white.Modulate = new Color(1, 1, 1, 1f - Mathf.Clamp((t - 0.6f) / 0.4f, 0f, 1f));
		});
		SpeedLines(root, 0.04);
	}

	private static void SpeedLines(Node2D root, double delay)
	{
		if (Fx.Tex("speed_line") is not Texture2D line)
		{
			return;
		}
		foreach (float side in new[] { 1f, -1f })
		{
			Sprite2D sp = Fx.Sprite(root, line, additive: false);
			sp.Rotation = side < 0 ? Mathf.Pi : 0f;
			sp.Modulate = new Color(1, 1, 1, 0);
			Fx.Animate(sp, delay, 0.2, t =>
			{
				sp.Position = Vector2.Right * side * (170f + 120f * Fx.OutCubic(t));
				sp.Scale = new Vector2(0.45f * (1f - 0.5f * t), 0.5f);
				sp.Modulate = new Color(1, 1, 1, 1f - t);
			});
		}
	}

	private static void Thrust(Control layer, Vector2 at, float face, float s)
	{
		Node2D root = Fx.Root(layer, at, "P5Thrust", 0.5);
		root.Scale = new Vector2(face * s, s);
		Polygon2D red = Fx.Poly(root, new Vector2[3], Red);
		Polygon2D white = Fx.Poly(root, new Vector2[3], Colors.White);
		Fx.Animate(root, 0, 0.22, t =>
		{
			float head = Fx.OutCubic(Mathf.Clamp(t / 0.25f, 0f, 1f)), tail = Fx.OutCubic(Mathf.Clamp((t - 0.25f) / 0.6f, 0f, 1f));
			float x0 = -320f + 640f * tail, x1 = -320f + 640f * head;
			red.Polygon = new[] { new Vector2(x0, 0f), new Vector2(x1 - 30f, -7f), new Vector2(x1, 0f), new Vector2(x1 - 30f, 7f) };
			white.Polygon = new[] { new Vector2(x0, 0f), new Vector2(x1 - 30f, -3f), new Vector2(x1, 0f), new Vector2(x1 - 30f, 3f) };
			red.Modulate = white.Modulate = new Color(1, 1, 1, 1f - Mathf.Clamp((t - 0.6f) / 0.4f, 0f, 1f));
		});
		Line2D pierce = new() { Closed = true, Width = 4f, DefaultColor = Colors.White };
		root.AddChild(pierce);
		Fx.Animate(pierce, 0.05, 0.25, t =>
		{
			Vector2[] c = Fx.Circle(30f + 70f * Fx.OutCubic(t), 28);
			for (int i = 0; i < c.Length; i++)
			{
				c[i] = new Vector2(c[i].X * 0.3f, c[i].Y);           // 正对刺线的一个扁环
			}
			pierce.Points = c;
			pierce.Position = new Vector2(120f, 0f);
			pierce.Modulate = new Color(1, 1, 1, 1f - t);
		});
	}

	/// <summary>拳脚：命中点外圈一圈往里收的放射速度线（漫画的冲击线），中心不画东西，再一圈白环往外扩。</summary>
	private static void Blunt(Control layer, Vector2 at, float face, float s)
	{
		Node2D root = Fx.Root(layer, at, "P5Blunt", 0.5);
		root.Scale = Vector2.One * s;
		for (int i = 0; i < 14; i++)
		{
			float a = Mathf.Tau * i / 14f + Fx.Rand(-0.1f, 0.1f);
			Polygon2D l = Fx.Poly(root, new[] { new Vector2(0f, -4f), new Vector2(70f, 0f), new Vector2(0f, 4f) }, Colors.White);
			l.Rotation = a + Mathf.Pi;
			float r0 = Fx.Rand(190f, 240f);
			Fx.Animate(l, 0, 0.2, t =>
			{
				l.Position = Vector2.Right.Rotated(a) * (r0 - 70f * Fx.OutCubic(t));
				l.Modulate = new Color(1, 1, 1, 1f - t);
			});
		}
		Line2D ring = new() { Closed = true, DefaultColor = Red, Width = 10f };
		root.AddChild(ring);
		Fx.Animate(ring, 0.03, 0.28, t =>
		{
			ring.Points = Fx.Circle(120f + 110f * Fx.OutCubic(t));
			ring.Width = 10f * (1f - t) + 1f;
			ring.Modulate = new Color(1, 1, 1, 1f - t);
		});
		Fx.Shake(ShakeStrength.Medium);
	}

	/// <summary>回响斩：一道横贯整个敌人区域的长斩痕（屏幕宽度），所有敌人身后同时爆碎片。</summary>
	private static void Echo(Control front, Control back, List<NCreature> targets, float face)
	{
		Vector2 screen = Fx.Screen(front);
		float y = 0f;
		foreach (NCreature t in targets)
		{
			y += t.VfxSpawnPosition.Y;
		}
		y /= Mathf.Max(1, targets.Count);
		Node2D root = Fx.Root(front, new Vector2(screen.X * 0.68f, y), "P5Echo", 0.6);
		root.Scale = new Vector2(face * screen.X / 900f, 1f);
		root.Rotation = -0.08f * face;
		Polygon2D red = Fx.Poly(root, new Vector2[3], Red);
		Polygon2D white = Fx.Poly(root, new Vector2[3], Colors.White);
		Fx.Animate(root, 0, 0.32, t =>
		{
			float head = Fx.OutCubic(Mathf.Clamp(t / 0.3f, 0f, 1f)), tail = Fx.OutCubic(Mathf.Clamp((t - 0.25f) / 0.6f, 0f, 1f));
			red.Polygon = Streak(head, tail, 22f, 10f);
			white.Polygon = Streak(head, tail, 9f, 10f);
			red.Modulate = white.Modulate = new Color(1, 1, 1, 1f - Mathf.Clamp((t - 0.6f) / 0.4f, 0f, 1f));
		});
		foreach (NCreature t in targets)
		{
			Behind(back, t.VfxSpawnPosition, face, 0.9f, 7);
		}
		Fx.Shake(ShakeStrength.Medium);
	}

	private static void Flash(Control layer, Vector2 at, float alpha)
	{
		if (Fx.Tex("glow") is not Texture2D glow)
		{
			return;
		}
		Node2D root = Fx.Root(layer, at, "P5Flash", 0.3);
		Sprite2D g = Fx.Sprite(root, glow);
		Fx.Animate(g, 0.04, 0.18, t =>
		{
			g.Scale = Vector2.One * (2f + 4f * t);
			g.Modulate = new Color(1f, 1f, 1f, alpha * (1f - t));
		});
	}

	/// <summary>后景（敌人身后）：白色冲击环 + 从身体边缘往外飞的红黑碎片。</summary>
	private static void Behind(Control layer, Vector2 at, float face, float s, int shards)
	{
		Node2D root = Fx.Root(layer, at, "P5SlashBack", 0.7);
		root.Scale = new Vector2(face * s, s);
		Line2D ring = new() { Closed = true, DefaultColor = Colors.White, Visible = false };
		root.AddChild(ring);
		Fx.Animate(ring, 0.04, 0.3, t =>
		{
			ring.Visible = true;
			ring.Points = Fx.Circle(110f + 120f * Fx.OutCubic(t));
			ring.Width = 8f * (1f - t) + 1f;
			ring.Modulate = new Color(1, 1, 1, 1f - t);
		});
		for (int i = 0; i < shards; i++)
		{
			Color c = i % 2 == 0 ? Red : Black;
			float size = Fx.Rand(10f, 20f);
			Polygon2D shard = Fx.Poly(root, new[] { new Vector2(-size, -size * 0.3f), new Vector2(size, 0f), new Vector2(-size * 0.6f, size * 0.45f) }, c);
			float ang = Fx.Rand(-1.5f, 0.4f);
			Vector2 dir = Vector2.Right.Rotated(ang);
			float start = Fx.Rand(110f, 150f), dist = Fx.Rand(90f, 170f), spin = Fx.Rand(-8f, 8f);
			shard.Visible = false;
			Fx.Animate(shard, 0.04, Fx.Rand(0.3f, 0.42f), t =>
			{
				shard.Visible = true;
				shard.Position = dir * (start + dist * Fx.OutCubic(t)) + new Vector2(0f, 50f * t * t);
				shard.Rotation = spin * t;
				shard.Modulate = new Color(1, 1, 1, 1f - Fx.InCubic(t));
			});
		}
	}

	/// <summary>一条略弯的细刀痕，沿 x 轴长 ±230：head / tail 是 0→1 的起止，中间最粗、两头尖。</summary>
	private static Vector2[] Streak(float head, float tail, float width, float bend)
	{
		const int n = 16;
		if (head <= tail + 0.01f)
		{
			return new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero };
		}
		Vector2[] pts = new Vector2[(n + 1) * 2];
		for (int i = 0; i <= n; i++)
		{
			float u = tail + (head - tail) * i / n;
			Vector2 c = new(-230f + 460f * u, bend * Mathf.Sin(Mathf.Pi * u));
			float w = width * Mathf.Sin(Mathf.Pi * (float)i / n);
			pts[i] = c + new Vector2(0f, w * 0.5f);
			pts[(n + 1) * 2 - 1 - i] = c - new Vector2(0f, w * 0.5f);
		}
		return pts;
	}
}
