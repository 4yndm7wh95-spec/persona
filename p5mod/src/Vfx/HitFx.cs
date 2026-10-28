using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 打击 / 开枪的代码特效（用户 2026-10-28：加一些特效，掩盖帧动画的不足）。都很短、很干脆，不挡敌人：
///  · Swing —— 挥刀那一下沿刀尖的真实轨迹扫出一道白色刀光弧（红边），补上准备帧和挥出帧之间缺的动作；
///  · Slash —— 命中：敌人身上一道细长白色斜斩痕 + 红色错位重影 + 小白闪 + 几片碎片；
///  · Muzzle —— 开火：枪口白色十字枪焰 + 红环 + 一小股烟；
///  · Shot —— 一道细白弹道线打到敌人，命中处小白闪 + 扩散红环 + 碎片。
/// 只用全局坐标，游戏里由 JokerFx 按姿势图换算好位置传进来，预览工具直接传。
/// </summary>
public static class HitFx
{
	private static float Wrap(float a) => Mathf.PosMod(a + Mathf.Pi, Mathf.Tau) - Mathf.Pi;

	/// <summary>刀光：以 center 为圆心，从刀尖起点 from 扫到终点 to（走较短的那一侧），0.2 秒。</summary>
	public static void Swing(Control layer, Vector2 center, Vector2 from, Vector2 to)
	{
		float r = (from.DistanceTo(center) + to.DistanceTo(center)) / 2f;
		float a0 = (from - center).Angle(), sweep = Wrap((to - center).Angle() - a0);
		Node2D root = Fx.Root(layer, center, "P5Swing", 0.25);
		Polygon2D red = Fx.Poly(root, new Vector2[3], Popups.Red);
		Polygon2D white = Fx.Poly(root, new Vector2[3], Colors.White);
		const float total = 0.2f;
		Fx.Animate(root, 0, total, t =>
		{
			// 刀光头部飞快扫到终点（前 20%），尾巴随后追上收掉
			float head = Fx.OutCubic(Mathf.Clamp(t / 0.2f, 0f, 1f));
			float tail = Fx.InCubic(Mathf.Clamp((t - 0.1f) / 0.9f, 0f, 1f));
			red.Polygon = Band(r + 3f, a0, sweep, tail, head, 19f);
			white.Polygon = Band(r, a0, sweep, tail, head, 14f);
			root.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((t - 0.7f) / 0.3f, 0f, 1f));
		});
	}

	/// <summary>弧形带：沿圆弧从 tail 到 head（0~1 的扫过比例），头部最厚 width，往尾巴收成尖。</summary>
	private static Vector2[] Band(float r, float a0, float sweep, float tail, float head, float width)
	{
		const int n = 20;
		if (head - tail < 0.01f)
		{
			return new Vector2[3];
		}
		var pts = new Vector2[n * 2];
		for (int i = 0; i < n; i++)
		{
			float u = (float)i / (n - 1);
			float a = a0 + sweep * Mathf.Lerp(tail, head, u);
			float w = width * Mathf.Pow(u, 1.4f);
			Vector2 d = Vector2.Right.Rotated(a);
			pts[i] = d * r;
			pts[n * 2 - 1 - i] = d * (r - w);
		}
		return pts;
	}

	/// <summary>命中斩痕：在 at 处一道斜向上（和上撩方向一致）的细长白痕 + 红色错位重影，facing = 刀从哪边来。</summary>
	public static void Slash(Control layer, Vector2 at, float facing = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P5Slash", 0.35);
		root.Rotation = -0.45f * facing;
		Polygon2D red = Fx.Poly(root, new Vector2[3], Popups.Red);
		Polygon2D white = Fx.Poly(root, new Vector2[3], Colors.White);
		red.Position = new Vector2(6f, 7f);
		Polygon2D flash = Fx.Poly(root, Fx.Circle(34f, 24), new Color(1f, 1f, 1f, 0.9f), additive: true);
		Fx.Animate(root, 0, 0.18, t =>
		{
			float len = 130f * Mathf.Lerp(0.55f, 1f, Fx.OutCubic(Mathf.Clamp(t / 0.25f, 0f, 1f)));
			float w = 9f * (1f - Fx.InCubic(t));
			white.Polygon = Streak(len, w);
			red.Polygon = Streak(len * 0.92f, w * 1.1f);
			flash.Scale = Vector2.One * (0.6f + 0.6f * t);
			flash.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp(t / 0.45f, 0f, 1f));
		});
		Shards(layer, at, facing, 4, 70f, 115f);
		Fx.Shake(2);
	}

	/// <summary>两头尖、中间宽 w 的长条（总长 2·len，沿 x 轴）。</summary>
	private static Vector2[] Streak(float len, float w) =>
		new[] { new Vector2(-len, 0f), new Vector2(-len * 0.3f, -w), new Vector2(len * 0.4f, -w * 0.8f), new Vector2(len, 0f),
			new Vector2(len * 0.4f, w * 0.8f), new Vector2(-len * 0.3f, w) };

	/// <summary>几片小三角碎片（白 / 红交替）往 facing 一侧崩开。</summary>
	private static void Shards(Control layer, Vector2 at, float facing, int count, float d0, float d1)
	{
		for (int i = 0; i < count; i++)
		{
			Node2D shard = Fx.Root(layer, at, "P5Shard", 0.35);
			float sz = Fx.Rand(6f, 11f);
			Fx.Poly(shard, new[] { new Vector2(-sz, -sz * 0.4f), new Vector2(sz, 0f), new Vector2(-sz * 0.5f, sz * 0.6f) },
				i % 2 == 0 ? Colors.White : Popups.Red);
			Vector2 dir = new Vector2(facing, 0f).Rotated(Fx.Rand(-1.2f, 1.2f));
			float dist = Fx.Rand(d0, d1), spin = Fx.Rand(-10f, 10f);
			Fx.Animate(shard, 0, 0.3, t =>
			{
				shard.GlobalPosition = at + dir * dist * Fx.OutCubic(t) + new Vector2(0f, 50f * t * t);
				shard.Rotation = spin * t;
				shard.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t));
			});
		}
	}

	/// <summary>枪焰：枪口 muzzle 处，枪管朝 facing 方向。</summary>
	public static void Muzzle(Control layer, Vector2 muzzle, float facing = 1f)
	{
		Node2D root = Fx.Root(layer, muzzle, "P5Muzzle", 0.12);
		root.Scale = new Vector2(facing, 1f);
		Polygon2D ring = Fx.Poly(root, Fx.Circle(18f, 24), new Color(Popups.Red, 0.85f));
		Polygon2D along = Fx.Poly(root, new[] { new Vector2(-6f, 0f), new Vector2(20f, -11f), new Vector2(78f, 0f), new Vector2(20f, 11f) }, Colors.White);
		Polygon2D cross = Fx.Poly(root, new[] { new Vector2(8f, -30f), new Vector2(14f, 0f), new Vector2(8f, 30f), new Vector2(2f, 0f) }, Colors.White);
		Polygon2D core = Fx.Poly(root, Fx.Circle(11f, 16), new Color(1f, 0.92f, 0.55f), additive: true);
		core.Position = along.Position + new Vector2(10f, 0f);
		Fx.Animate(root, 0, 0.09, t =>
		{
			float s = 1f + 0.35f * t;
			along.Scale = new Vector2(s, 1f - 0.7f * t);
			cross.Scale = new Vector2(1f, 1f - t);
			ring.Scale = Vector2.One * (1f + 1.3f * Fx.OutCubic(t));
			ring.Modulate = new Color(1f, 1f, 1f, 1f - t);
			core.Modulate = new Color(1f, 1f, 1f, 1f - t);
			along.Modulate = cross.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t));
		});
		for (int i = 0; i < 3; i++)
		{
			Node2D puff = Fx.Root(layer, muzzle + new Vector2(8f * facing, 0f), "P5Smoke", 0.6);
			Polygon2D c = Fx.Poly(puff, Fx.Circle(Fx.Rand(7f, 12f), 14), new Color(0.75f, 0.75f, 0.78f, 0.35f));
			Vector2 drift = new(Fx.Rand(10f, 30f) * facing, Fx.Rand(-45f, -25f));
			float d = 0.03f * i;
			Fx.Animate(puff, d, 0.5, t =>
			{
				puff.GlobalPosition = muzzle + new Vector2(8f * facing, 0f) + drift * Fx.OutCubic(t);
				puff.Scale = Vector2.One * (1f + 1.2f * t);
				c.Modulate = new Color(1f, 1f, 1f, 1f - t);
			});
		}
	}

	/// <summary>弹道 + 命中：一道细白线从枪口打到 hit，命中处小白闪 + 扩散红环 + 碎片。</summary>
	public static void Shot(Control layer, Vector2 muzzle, Vector2 hit, float facing = 1f)
	{
		Node2D tracer = Fx.Root(layer, Vector2.Zero, "P5Tracer", 0.12);
		Line2D line = new() { Points = new[] { muzzle, hit }, Width = 4f, DefaultColor = Colors.White, Antialiased = true };
		tracer.AddChild(line);
		tracer.GlobalPosition = Vector2.Zero;
		Fx.Animate(tracer, 0, 0.09, t =>
		{
			line.Points = new[] { muzzle.Lerp(hit, Fx.OutCubic(t)), hit };        // 尾巴追向命中点
			line.Width = 4f * (1f - t) + 1f;
		});
		Node2D root = Fx.Root(layer, hit, "P5ShotHit", 0.3);
		Line2D ring = new() { Points = Fx.Circle(20f, 28), Closed = true, Width = 5f, DefaultColor = Popups.Red, Antialiased = true };
		root.AddChild(ring);
		Polygon2D flash = Fx.Poly(root, Fx.Circle(24f, 20), new Color(1f, 1f, 1f, 0.95f), additive: true);
		Fx.Animate(root, 0, 0.22, t =>
		{
			ring.Scale = Vector2.One * (1f + 2.2f * Fx.OutCubic(t));
			ring.Width = 5f * (1f - t) + 1f;
			ring.Modulate = new Color(1f, 1f, 1f, 1f - t);
			flash.Scale = Vector2.One * (0.8f + 0.5f * t);
			flash.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp(t / 0.4f, 0f, 1f));
		});
		Shards(layer, hit, facing, 3, 45f, 80f);
		Fx.Shake(1);
	}
}
