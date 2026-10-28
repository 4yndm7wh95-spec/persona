using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P5Joker;

/// <summary>
/// 小刀牌 = 开枪（docs/p5/FX_PLAN.md 第 2 节）：用 P5R 原版 BES_GUN1 的贴图。
/// 枪口火光（firetip 火舌 + 柔光 + 四角白星，3 帧闪完）→ 一条很细的白色弹道瞬间连到目标 →
/// 目标身上原版的命中火光序列（gun_hit_light_anim，2x2 四帧）+ 红色火星。
/// 用户 2026-10-28：特效不能挡住敌人 —— 命中火光缩小、只闪 0.1 秒，打在身体朝枪口那一侧的边上；火星往外溅。
/// 伤害结算那一刻（出牌后 0.2 秒）Joker 的动画正好在开火帧上。
/// </summary>
public static class Gunshot
{
	private static readonly Color Red = new(0.95f, 0.08f, 0.1f), Hot = new(1f, 0.93f, 0.7f);

	public static void Play(NCreature joker, List<NCreature> targets)
	{
		Control? layer = Joker.FxLayer();
		if (layer == null)
		{
			return;
		}
		float face = Joker.Facing(joker);
		Vector2 muzzle = Joker.Muzzle(joker);
		Flash(layer, muzzle, face);
		foreach (NCreature t in targets)
		{
			Vector2 hit = t.VfxSpawnPosition + new Vector2(-face * Fx.Rand(40f, 70f), Fx.Rand(-30f, 20f));   // 身体朝枪口一侧的边
			Tracer(layer, muzzle, hit);
			Impact(layer, hit, face);
		}
		Fx.Shake(ShakeStrength.Weak);
	}

	private static void Flash(Control layer, Vector2 at, float face)
	{
		Node2D root = Fx.Root(layer, at, "P5Muzzle", 0.4);
		root.Scale = new Vector2(face, 1f);
		if (Fx.Tex("firetip") is Texture2D fire)
		{
			// 贴图是竖的、火舌朝上：转 90° 朝右，原点挪到火舌根部
			Sprite2D tip = Fx.Sprite(root, fire);
			tip.Rotation = Mathf.Pi / 2f;
			tip.Offset = new Vector2(0f, -fire.GetHeight() * 0.38f);
			Fx.Animate(tip, 0, 0.1, t =>
			{
				tip.Scale = new Vector2(1.6f, 1.2f + 0.8f * t);
				tip.Modulate = new Color(Hot.R, Hot.G, Hot.B, 1f - t);
			});
		}
		if (Fx.Tex("glow") is Texture2D glow)
		{
			Sprite2D g = Fx.Sprite(root, glow);
			Fx.Animate(g, 0, 0.14, t =>
			{
				g.Scale = Vector2.One * (0.7f + 0.9f * t);
				g.Modulate = new Color(1f, 0.85f, 0.6f, 1f - t);
			});
		}
		Polygon2D star = Fx.Poly(root, Star4(48f), Colors.White, additive: true);
		Fx.Animate(star, 0, 0.08, t =>
		{
			star.Scale = Vector2.One * (1f + 0.6f * t);
			star.Modulate = new Color(1, 1, 1, 1f - t);
		});
	}

	private static void Tracer(Control layer, Vector2 from, Vector2 to)
	{
		Node2D root = Fx.Root(layer, from, "P5Tracer", 0.2);
		from = root.ToLocal(from);
		to = root.ToLocal(to);
		Line2D glow = new() { Points = new[] { from, to }, Width = 9f, DefaultColor = new Color(Red, 0.6f), Material = Fx.Additive };
		Line2D core = new() { Points = new[] { from, to }, Width = 3f, DefaultColor = Colors.White, Material = Fx.Additive };
		root.AddChild(glow);
		root.AddChild(core);
		Fx.Animate(root, 0, 0.09, t =>
		{
			// 弹道从枪口一侧先收：起点沿线往目标走
			Vector2 start = from.Lerp(to, Fx.OutCubic(t));
			glow.Points = core.Points = new[] { start, to };
			core.Width = 3f * (1f - t);
			glow.Width = 9f * (1f - t);
		});
	}

	private static void Impact(Control layer, Vector2 at, float face)
	{
		Node2D root = Fx.Root(layer, at, "P5GunHit", 0.6);
		root.Scale = new Vector2(face, 1f);
		if (Fx.Tex("gun_hit") is Texture2D sheet)
		{
			// 原版命中火光：2x2 四帧，每帧 0.035 秒
			Sprite2D s = Fx.Sprite(root, sheet);
			s.RegionEnabled = true;
			float w = sheet.GetWidth() / 2f, h = sheet.GetHeight() / 2f;
			s.Rotation = Fx.Rand(-0.4f, 0.4f);
			Fx.Animate(s, 0, 0.1, t =>
			{
				int f = Mathf.Min(3, (int)(t * 4f));
				s.RegionRect = new Rect2(f % 2 * w, f / 2 * h, w, h);
				s.Scale = Vector2.One * (0.75f + 0.25f * t);
				s.Modulate = new Color(1f, 0.95f, 0.85f, f < 3 ? 1f : 1f - (t * 4f - 3f));
			});
		}
		for (int i = 0; i < 7; i++)
		{
			float size = Fx.Rand(6f, 13f);
			Polygon2D spark = Fx.Poly(root, new[] { new Vector2(-size, -size * 0.3f), new Vector2(size * 1.4f, 0f), new Vector2(-size, size * 0.3f) },
				i % 2 == 0 ? Red : Hot, additive: true);
			float ang = Fx.Rand(-2.4f, 2.4f) + Mathf.Pi;                // 往枪口方向（左）溅回来
			float dist = Fx.Rand(60f, 140f);
			spark.Rotation = ang;
			Vector2 dir = Vector2.Right.Rotated(ang);
			Fx.Animate(spark, 0, Fx.Rand(0.18f, 0.28f), t =>
			{
				spark.Position = dir * dist * Fx.OutCubic(t);
				spark.Modulate = new Color(1, 1, 1, 1f - t);
			});
		}
	}

	private static Vector2[] Star4(float r)
	{
		Vector2[] pts = new Vector2[8];
		for (int i = 0; i < 8; i++)
		{
			float rad = i % 2 == 0 ? r * (i % 4 == 0 ? 1f : 0.55f) : r * 0.12f;
			pts[i] = Vector2.Right.Rotated(Mathf.Tau * i / 8f) * rad;
		}
		return pts;
	}
}
