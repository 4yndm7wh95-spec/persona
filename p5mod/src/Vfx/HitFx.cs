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
		// 发光的刀光（加法）：红晕在外、白芯在里
		Polygon2D red = Fx.Poly(root, new Vector2[3], new Color(Pal5.Bright, 0.9f), additive: true);
		Polygon2D white = Fx.Poly(root, new Vector2[3], Pal5.Hot, additive: true);
		const float total = 0.2f;
		Fx.Animate(root, 0, total, t =>
		{
			// 刀光头部飞快扫到终点（前 20%），尾巴随后追上收掉
			float head = Fx.OutCubic(Mathf.Clamp(t / 0.2f, 0f, 1f));
			float tail = Fx.InCubic(Mathf.Clamp((t - 0.1f) / 0.9f, 0f, 1f));
			red.Polygon = Band(r + 6f, a0, sweep, tail, head, 26f);
			white.Polygon = Band(r, a0, sweep, tail, head, 9f);
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

	/// <summary>
	/// 打击命中（2026-10-29 用发光积木重做，Glow.cs）：
	/// 爆发 —— 径向白闪 + 屏幕折射波，一道沿上撩方向的长光刃划过，60ms 后第二道短光刃交叉；
	/// 余韵 —— 扁的红色光环往外扩、几十颗红色火花往刀的去向喷、几片带棱光的碎片、两颗星光；轻震屏。
	/// </summary>
	public static void Slash(Control layer, Vector2 at, float facing = 1f)
	{
		float up = -0.55f;                                           // 上撩：从左下到右上
		float a1 = facing > 0f ? up : Mathf.Pi - up;
		Glow.Flash(layer, at, new Color(1f, 0.55f, 0.5f), 260f, 0.2f);
		Glow.Ripple(layer, at, 440f, 0.5f, 0.03f);
		Glow.Blade(layer, at, a1, 300f, 34f, 0.34f);
		Glow.Blade(layer, at + new Vector2(10f * facing, 6f), a1 + 1.35f * facing, 190f, 22f, 0.3f, 0.06f);
		Glow.Shockwave(layer, at, new Color(0.9f, 0.05f, 0.08f), 300f, 0.42f, 0.5f, 0.03f);
		Glow.Sparks(layer, at, 34, 260f, 720f, 0.5f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 260f, 0.12f, 0.4f,
			new Vector2(facing, -0.6f).Normalized(), 70f);
		Glow.Shards(layer, at, 7, 0.9f, new Vector2(facing, -0.3f).Normalized(), 1.0f);
		Glow.Glint(layer, at + new Vector2(70f * facing, -60f), 0.9f, 0.06f);
		Glow.Glint(layer, at + new Vector2(-40f * facing, 50f), 0.6f, 0.12f);
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
