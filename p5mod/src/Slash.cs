using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P5Joker;

/// <summary>
/// 小刀斩的命中（docs/p5/FX_PLAN.md 第 1 节）。用户 2026-10-28：不要刺猬球一样的爆点，特效不能挡住敌人。
/// 所以前景只有细线：一道细的白芯红边斩痕从左下往右上划过敌人（0.06 秒，跟 Joker 反手上撩的刀路一致），
/// 原版「効果線」两条速度线从斩痕两端往外甩出；冲击环和红黑碎片都画在敌人身后（后景特效层），从身体边缘往外飞。
/// </summary>
public static class Slash
{
	private static readonly Color Red = new(0.92f, 0.05f, 0.12f), Black = new(0.04f, 0.02f, 0.03f);

	public static void Play(NCreature joker, List<NCreature> targets)
	{
		Control? front = Joker.FxLayer();
		Control? back = Joker.FxLayer(back: true) ?? front;
		if (front == null || back == null)
		{
			return;
		}
		float face = Joker.Facing(joker);
		float scale = targets.Count > 1 ? 0.85f : 1f;
		foreach (NCreature t in targets)
		{
			Cut(front, t.VfxSpawnPosition, face, scale);
			Behind(back, t.VfxSpawnPosition, face, scale);
		}
		Fx.Shake(ShakeStrength.Weak);
	}

	/// <summary>前景：细斩痕 + 两条速度线，很快划过、很快消失。</summary>
	private static void Cut(Control layer, Vector2 at, float face, float s)
	{
		Node2D root = Fx.Root(layer, at, "P5Slash", 0.5);
		root.Scale = new Vector2(face * s, s);
		root.Rotation = face * Fx.Rand(-0.1f, 0.1f);
		Polygon2D red = Fx.Poly(root, new Vector2[3], Red);
		Polygon2D white = Fx.Poly(root, new Vector2[3], Colors.White);
		Fx.Animate(root, 0, 0.26, t =>
		{
			float head = Fx.OutCubic(Mathf.Clamp(t / 0.23f, 0f, 1f));          // 0.06 s
			float tail = Fx.OutCubic(Mathf.Clamp((t - 0.2f) / 0.55f, 0f, 1f));
			red.Polygon = Streak(head, tail, 12f);
			white.Polygon = Streak(head, tail, 5f);
			red.Modulate = white.Modulate = new Color(1, 1, 1, 1f - Mathf.Clamp((t - 0.6f) / 0.4f, 0f, 1f));
		});
		if (Fx.Tex("speed_line") is Texture2D line)
		{
			// 原版「効果線」（细长、一头尖）：沿刀路两端往外甩出去
			foreach (float side in new[] { 1f, -1f })
			{
				Sprite2D sp = Fx.Sprite(root, line, additive: false);
				sp.Rotation = Dir.Angle() + (side < 0 ? Mathf.Pi : 0f);
				sp.Modulate = new Color(1, 1, 1, 0);
				Fx.Animate(sp, 0.04, 0.2, t =>
				{
					sp.Position = Dir * side * (170f + 120f * Fx.OutCubic(t));
					sp.Scale = new Vector2(0.45f * (1f - 0.5f * t), 0.5f);
					sp.Modulate = new Color(1, 1, 1, 1f - t);
				});
			}
		}
	}

	/// <summary>后景（敌人身后）：白色冲击环 + 从身体边缘往外飞的红黑碎片。</summary>
	private static void Behind(Control layer, Vector2 at, float face, float s)
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
		for (int i = 0; i < 9; i++)
		{
			Color c = i % 2 == 0 ? Red : Black;
			float size = Fx.Rand(10f, 20f);
			Polygon2D shard = Fx.Poly(root, new[] { new Vector2(-size, -size * 0.3f), new Vector2(size, 0f), new Vector2(-size * 0.6f, size * 0.45f) }, c);
			float ang = Fx.Rand(-1.5f, 0.4f);                                  // 大致右上（刀路方向）
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

	private static readonly Vector2 Dir = new Vector2(1f, -0.62f).Normalized();     // 左下 → 右上

	/// <summary>一条略弯的细刀痕，长 ±230：head / tail 是沿刀路 0→1 的起止，中间最粗、两头尖。</summary>
	private static Vector2[] Streak(float head, float tail, float width)
	{
		const int n = 16;
		if (head <= tail + 0.01f)
		{
			return new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero };
		}
		Vector2 normal = new(-Dir.Y, Dir.X);
		Vector2[] pts = new Vector2[(n + 1) * 2];
		for (int i = 0; i <= n; i++)
		{
			float u = tail + (head - tail) * i / n;
			Vector2 c = Dir * (-230f + 460f * u) + normal * (24f * Mathf.Sin(Mathf.Pi * u));   // 微弯
			float w = width * Mathf.Sin(Mathf.Pi * (float)i / n);
			pts[i] = c + normal * w * 0.5f;
			pts[(n + 1) * 2 - 1 - i] = c - normal * w * 0.5f;
		}
		return pts;
	}
}
