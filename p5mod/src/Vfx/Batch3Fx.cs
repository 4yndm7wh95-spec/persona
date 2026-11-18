using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 0.9.49 审阅第三批（2026-11-18）：匕首雨（群体攻击的感觉）、刀扇（转枪之外加一个主体）。腐蚀波在 CorrosiveFx（整套重做）。
/// 雷点照旧：不用火、烟、贴图拼形状、光环当主体、细线圈；主体用疾光 / 光刃 / 碎光 / 柔光这些认可过的东西。
/// </summary>
public static class Batch3Fx
{
	public readonly record struct Foe(Vector2 At, Vector2 Top, float Width);

	private static readonly Color Hot = new(1f, 0.96f, 0.95f), Red = new(1f, 0.12f, 0.2f), Pink = new(1f, 0.45f, 0.5f);

	// ═══════════════════════════ 匕首雨 ═══════════════════════════

	/// <summary>
	/// 匕首雨（对所有敌人造成伤害两次；审阅：没有群体攻击的感觉）：「一扇 + 一场雨」。
	/// 第一下：他挥刀的同时，一大扇红白飞刀（两头尖的疾光）从刀口呈扇形泼出去，盖住整排敌人，每个敌人身上都扎进好几把；
	///         同一瞬间一道很长的横向刀光从第一个敌人一直切到最后一个（一刀切全体）。
	/// 第二下：整排敌人头顶斜着落下一阵刀雨（每个敌人四五把，前后错开），落点碎光、轻震。
	/// 每个敌人身上的刀命中（KnifeFx.Hit）照旧由每次伤害放。t1 / t2 = 两次伤害离现在几秒。
	/// </summary>
	public static void DaggerSpray(Control layer, Vector2 blade, IReadOnlyList<Foe> foes, float facing, float t1 = 0.15f, float t2 = 0.27f)
	{
		if (foes.Count == 0) return;
		float u = Fx.Screen(layer).Y / 1080f, f = facing;
		float minX = float.MaxValue, maxX = float.MinValue, line = 0f, tallest = 0f;
		foreach (Foe e in foes)
		{
			minX = Mathf.Min(minX, e.At.X - e.Width * 0.5f);
			maxX = Mathf.Max(maxX, e.At.X + e.Width * 0.5f);
			line += e.At.Y;
			tallest = Mathf.Max(tallest, (e.At - e.Top).Length());
		}
		line /= foes.Count;
		tallest = Mathf.Max(120f * u, tallest);
		Sfx.Take("gx_whoosh", 0.18f, t1 - 0.08f);
		Sfx.Take("gx_slice", 0.2f, t1);
		Sfx.Take("gx_whoosh", 0.14f, t2 - 0.1f);
		// ── 第一下：一扇飞刀
		float fly1 = 0.12f;
		foreach (Foe e in foes)
		{
			float h = Mathf.Max(100f * u, (e.At - e.Top).Length());
			for (int i = 0; i < 3; i++)
			{
				Vector2 hit = e.At + new Vector2(Fx.Rand(-0.35f, 0.35f) * e.Width, Fx.Rand(-0.6f, 0.4f) * h);
				Vector2 from = blade + new Vector2(0f, Fx.Rand(-30f, 30f) * u);
				float d = t1 - fly1 + Fx.Rand(-0.03f, 0.02f);
				Dagger(layer, from, hit, d, fly1, u, 1f);
			}
		}
		// 扇子的边：几把飞过头的（没扎中，往敌人后上方 / 后下方飞出去），扇形更开
		for (int i = 0; i < 6; i++)
		{
			float ang = Fx.Rand(-0.55f, 0.35f);
			Vector2 dir = new Vector2(f, 0f).Rotated(ang * f);
			Vector2 to = blade + dir * (Mathf.Abs(maxX - minX) + Mathf.Abs(minX - blade.X) + 300f * u);
			DashFly(layer, blade, to, t1 - fly1, fly1 * 2.2f, 200f * u, 24f * u, Pink, 1.3f);
		}
		// 一刀切全体：横向长刀光，从最近的敌人前面切到最远的后面
		float x0 = f > 0 ? minX - 60f * u : maxX + 60f * u, x1 = f > 0 ? maxX + 80f * u : minX - 80f * u;
		float y = line - tallest * 0.15f;
		Fx.After(layer, t1, () =>
		{
			Vector2 a = new(x0, y + 40f * u), b = new(x1, y - 50f * u);
			Glow.Blade(layer, a.Lerp(b, 0.5f), (b - a).Angle(), (b - a).Length(), 70f * u, 0.36f, 0f, Red);
			Cut.Razor(layer, a.Lerp(b, 0.5f), (b - a).Angle(), (b - a).Length() * 0.9f, 8f * u, 0.2f);
			Glow.Flash(layer, a.Lerp(b, 0.5f), new Color(1f, 0.3f, 0.35f), (b - a).Length() * 0.9f, 0.18f);
			HitStop.Request(0.04);
			Fx.Shake(1);
		});
		// ── 第二下：头顶落下的刀雨
		foreach (Foe e in foes)
		{
			float h = Mathf.Max(100f * u, (e.At - e.Top).Length());
			for (int i = 0; i < 4; i++)
			{
				Vector2 hit = e.At + new Vector2(Fx.Rand(-0.45f, 0.45f) * e.Width, Fx.Rand(-0.3f, 0.5f) * h);
				Vector2 from = hit + new Vector2(-f * h * 0.55f, -h * 1.9f) + new Vector2(Fx.Rand(-40f, 40f) * u, 0f);
				float d = t2 - 0.1f + Fx.Rand(-0.04f, 0.05f);
				Dagger(layer, from, hit, d, 0.1f, u, 0.85f);
			}
		}
		Fx.After(layer, t2 + 0.02f, () => Fx.Shake(1));
	}

	/// <summary>一把飞刀：两头尖的疾光从 from 扎到 to（delay 起飞、fly 秒到），落点白闪 + 几根碎光。</summary>
	private static void Dagger(Control layer, Vector2 from, Vector2 to, float delay, float fly, float u, float k)
	{
		DashFly(layer, from, to, Mathf.Max(0f, delay), fly, 230f * u * k, 38f * u * k, Red, 1.6f);
		float at = Mathf.Max(0f, delay) + fly;
		Fx.After(layer, at, () =>
		{
			Glow.Flash(layer, to, Hot, 70f * u * k, 0.08f);
			Glow.Flash(layer, to, Red, 150f * u * k, 0.2f);
		});
	}

	// ═══════════════════════════ 刀扇 ═══════════════════════════

	/// <summary>
	/// 刀扇（之后小刀打所有敌人；金卡；审阅：转枪不错，但一点特效也没有）：转枪照旧（ShivFx），加主体「刀扇开合」。
	/// 慢：转枪的同时，他持枪的手后面「唰」地一把一把展开七道红白光刃，排成一把半开的扇子（一道接一道，像折扇被甩开），扇面上一道高光扫过；
	/// 快：枪停住的一刻，扇子「啪」地合拢、所有光刃收进枪里 —— 枪口十字光、白闪、顿帧轻震；
	/// 慢：枪口朝敌人那边喷出一小扇碎光（之后每一枪都会散开打全体），身上一层淡红慢慢退掉。
	/// grip = 转枪的手，tall = 人物身高（像素），spin = 转一圈多久（ShivFx 用的那个）。
	/// </summary>
	public static void FanOfKnives(Control layer, Control back, Vector2 grip, float tall, float facing, float spin = 0.22f)
	{
		float u = Fx.Screen(layer).Y / 1080f, f = facing, h = tall;
		const int n = 7;
		float open = 0.05f, step = 0.03f, shut = spin + 0.1f, life = shut + 0.12f;
		Vector2 pivot = grip + new Vector2(-f * h * 0.04f, h * 0.02f);
		float len = h * 0.62f;
		Sfx.Take("gx_fan_open", 0.24f, open);
		Sfx.Take("gx_fan_close", 0.3f, shut);
		Sfx.Take("gx_ping", 0.14f, shut + 0.02f);
		Glow.Flash(back, pivot + new Vector2(-f * len * 0.2f, -len * 0.5f), new Color(0.7f, 0.03f, 0.12f), h * 1.6f, life + 0.3f);
		var blades = new List<(Polygon2D Glow, Polygon2D Core, float Ang, float At)>();
		Node2D root = Fx.Root(layer, pivot, "P5KnifeFan", life + 0.1);
		for (int i = 0; i < n; i++)
		{
			// 扇面朝他身后上方展开：从「朝前上」到「朝后上」
			float ang = -Mathf.Pi / 2f + f * Mathf.Lerp(0.95f, -1.05f, i / (float)(n - 1));
			Polygon2D g = Glow.TaperPoly(root, new Color(1f, 0.12f, 0.2f, 0.85f));
			Polygon2D c = Glow.TaperPoly(root, Hot);
			g.Visible = c.Visible = false;
			blades.Add((g, c, ang, open + step * i));
		}
		float aimAng = f > 0 ? 0f : Mathf.Pi;
		Fx.Animate(root, 0, life, t =>
		{
			float s = t * life;
			for (int i = 0; i < blades.Count; i++)
			{
				var (g, c, ang, at) = blades[i];
				if (s < at) continue;
				g.Visible = c.Visible = true;
				float e = Ease5.OutBack(Mathf.Min(1f, (s - at) / 0.1f));
				// 合拢：所有光刃转到枪口方向、缩短收进枪里
				float k = Fx.InCubic(Mathf.Clamp((s - shut + 0.06f) / 0.08f, 0f, 1f));
				float a = Mathf.LerpAngle(-Mathf.Pi / 2f + f * 0.95f, ang, e);
				a = Mathf.LerpAngle(a, aimAng, k);
				float l = len * Mathf.Min(1f, e) * (1f - 0.95f * k) * (0.85f + 0.15f * Mathf.Sin(i * 1.7f));
				// 扫过的高光：从第一把扫到最后一把
				float sweep = Mathf.Exp(-Mathf.Pow((s - (open + step * n + 0.04f + 0.02f * i)) / 0.035f, 2f));
				Vector2 dir = Vector2.Right.Rotated(a);
				Vector2 p0 = dir * len * 0.12f * (1f - k), p1 = p0 + dir * l;
				g.Polygon = Glow.TaperPts(p0, p1, 22f * u * (1f + 0.4f * sweep));
				c.Polygon = Glow.TaperPts(p0, p1, 6f * u * (1f + sweep));
				g.Modulate = new Color(1f, 1f, 1f, 0.9f);
			}
		});
		// 每把展开时尖上一点星光
		for (int i = 0; i < n; i++)
		{
			var (_, _, ang, at) = blades[i];
			Vector2 tip = pivot + Vector2.Right.Rotated(ang) * len * 0.95f;
			Fx.After(layer, at + 0.08f, () => Glow.Glint(layer, tip, 0.45f));
		}
		Fx.After(layer, shut + 0.02f, () =>
		{
			HitStop.Request(0.04);
			Fx.Shake(1);
			Glow.Flash(layer, grip, Hot, h * 0.35f, 0.12f);
			Glow.Flash(back, grip, Red, h * 1.4f, 0.4f);
			SkillRedo5.Cross(layer, grip, h * 0.4f);
			Phantom.Shards(layer, grip, 14, 420f * u, 900f * u, new Vector2(f, -0.08f), 28f, 0.38f, 200f * u, 20f * u);
		});
	}

	// ═══════════════════════════ 小件 ═══════════════════════════

	/// <summary>两头尖的疾光从 a 直线飞到 b（delay 起飞，dur 秒到）。</summary>
	internal static void DashFly(Control layer, Vector2 a, Vector2 b, float delay, float dur, float len, float thick, Color tint, float hot) =>
		SkillRedo5.StreakSharp(layer, e => a.Lerp(b, e), delay, dur, len, thick, tint, hot);
}
