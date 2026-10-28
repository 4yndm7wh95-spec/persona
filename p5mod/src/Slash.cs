using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P5Joker;

/// <summary>
/// 小刀斩的命中（docs/p5/FX_PLAN.md 第 1 节）：P5 的红 / 黑 / 白三色。
/// 一道白芯红边的月牙斩痕从左下往右上划过（0.07 秒，跟 Joker 反手上撩的刀路一致）→ 命中点一个红黑锯齿爆点 +
/// 白色冲击环 + 往右上飞散的红黑白碎片，配一下轻震屏。
/// </summary>
public static class Slash
{
	private static readonly Color Red = new(0.92f, 0.05f, 0.12f), Black = new(0.04f, 0.02f, 0.03f), White = new(1f, 1f, 1f);

	public static void Play(NCreature joker, List<NCreature> targets)
	{
		Control? layer = Joker.FxLayer();
		if (layer == null)
		{
			return;
		}
		float face = Joker.Facing(joker);
		float scale = targets.Count > 1 ? 0.8f : 1f;
		foreach (NCreature t in targets)
		{
			Hit(layer, t.VfxSpawnPosition, face, scale);
		}
		Fx.Shake(ShakeStrength.Weak);
	}

	private static void Hit(Control layer, Vector2 at, float face, float s)
	{
		Node2D root = Fx.Root(layer, at, "P5Slash", 0.8);
		root.Scale = new Vector2(face * s, s);
		float tilt = Fx.Rand(-0.12f, 0.12f);

		// 1. 斩痕：沿一段圆弧的月牙，头部 0.07 秒扫过去，尾巴随后收掉，整条再淡出
		Polygon2D redArc = Fx.Poly(root, new Vector2[3], Red);
		Polygon2D whiteArc = Fx.Poly(root, new Vector2[3], White, additive: false);
		Fx.Animate(root, 0, 0.32, t =>
		{
			float head = Mathf.Clamp(t / 0.22f, 0f, 1f);                 // 0.07 s
			float tail = Mathf.Clamp((t - 0.22f) / 0.5f, 0f, 1f);
			redArc.Polygon = Crescent(Fx.OutCubic(head), Fx.OutCubic(tail) * 0.92f, 240f, 34f, tilt);
			whiteArc.Polygon = Crescent(Fx.OutCubic(head), Fx.OutCubic(tail) * 0.92f, 236f, 15f, tilt);
			float fade = 1f - Mathf.Clamp((t - 0.55f) / 0.45f, 0f, 1f);
			redArc.Modulate = whiteArc.Modulate = new Color(1, 1, 1, fade);
		});

		// 2. 命中闪光：原版的柔光贴图，加色
		if (Fx.Tex("glow") is Texture2D glow)
		{
			Sprite2D flash = Fx.Sprite(root, glow);
			Fx.Animate(flash, 0.05, 0.2, t =>
			{
				flash.Scale = Vector2.One * (1.2f + 2.6f * Fx.OutCubic(t));
				flash.Modulate = new Color(1f, 0.95f, 0.95f, 1f - t);
			});
		}

		// 3. 红黑锯齿爆点：黑底在下、红在上，弹出后收缩
		Polygon2D burstBlack = Fx.Poly(root, Fx.Burst(130f, 46f, 9), Black);
		Polygon2D burstRed = Fx.Poly(root, Fx.Burst(104f, 34f, 9), Red);
		burstBlack.Visible = burstRed.Visible = false;
		Fx.Animate(root, 0.05, 0.26, t =>
		{
			burstBlack.Visible = burstRed.Visible = t > 0f;
			float k = t < 0.25f ? Fx.OutCubic(t / 0.25f) * 1.1f : 1.1f - 0.5f * Fx.InCubic((t - 0.25f) / 0.75f);
			burstBlack.Scale = burstRed.Scale = Vector2.One * k;
			burstBlack.Rotation = burstRed.Rotation = 0.35f * t;
			float a = 1f - Mathf.Clamp((t - 0.55f) / 0.45f, 0f, 1f);
			burstBlack.Modulate = burstRed.Modulate = new Color(1, 1, 1, a);
		});

		// 4. 白色冲击环
		Line2D ring = new() { Points = Fx.Circle(1f), Closed = true, Width = 9f, DefaultColor = White, Visible = false };
		root.AddChild(ring);
		Fx.Animate(ring, 0.05, 0.3, t =>
		{
			ring.Visible = t > 0f;
			float r = 40f + 170f * Fx.OutCubic(t);
			ring.Points = Fx.Circle(r);
			ring.Width = 10f * (1f - t) + 1f;
			ring.Modulate = new Color(1, 1, 1, 1f - t);
		});

		// 5. 碎片：往右上（Joker 的刀路方向）飞散，旋转、减速、淡出
		for (int i = 0; i < 11; i++)
		{
			Color c = i % 3 == 0 ? White : i % 3 == 1 ? Red : Black;
			float size = Fx.Rand(14f, 30f);
			Vector2[] tri = { new(-size, -size * 0.35f), new(size, 0f), new(-size * 0.6f, size * 0.5f) };
			Polygon2D shard = Fx.Poly(root, tri, c);
			float ang = Fx.Rand(-1.45f, 0.35f);                          // 大致右上
			float dist = Fx.Rand(130f, 260f);
			float spin = Fx.Rand(-9f, 9f);
			Vector2 dir = Vector2.Right.Rotated(ang);
			shard.Visible = false;
			Fx.Animate(shard, 0.05, Fx.Rand(0.32f, 0.45f), t =>
			{
				shard.Visible = t > 0f;
				shard.Position = dir * dist * Fx.OutCubic(t) + new Vector2(0f, 60f * t * t);
				shard.Rotation = spin * t;
				shard.Modulate = new Color(1, 1, 1, 1f - Fx.InCubic(t));
			});
		}
	}

	/// <summary>月牙：圆心在命中点左下方的一段弧，head / tail 是沿弧 0→1 的起止，中间最粗、两端尖。</summary>
	private static Vector2[] Crescent(float head, float tail, float radius, float width, float tilt)
	{
		const int n = 22;
		Vector2 center = new Vector2(-radius * 0.55f, radius * 0.75f);
		float a0 = -2.35f + tilt, a1 = -0.25f + tilt;                    // 从左下扫到右上
		if (head <= tail + 0.01f)
		{
			return new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero };
		}
		Vector2[] outer = new Vector2[n + 1], inner = new Vector2[n + 1];
		for (int i = 0; i <= n; i++)
		{
			float u = tail + (head - tail) * i / n;
			float a = a0 + (a1 - a0) * u;
			float w = width * Mathf.Sin(Mathf.Pi * Mathf.Clamp((float)i / n, 0f, 1f));
			Vector2 dir = Vector2.Right.Rotated(a);
			outer[i] = center + dir * (radius + w * 0.5f);
			inner[i] = center + dir * (radius - w * 0.5f);
		}
		Vector2[] pts = new Vector2[(n + 1) * 2];
		for (int i = 0; i <= n; i++)
		{
			pts[i] = outer[i];
			pts[(n + 1) * 2 - 1 - i] = inner[i];
		}
		return pts;
	}
}
