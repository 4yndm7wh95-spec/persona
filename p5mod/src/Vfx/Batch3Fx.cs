using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 0.9.49 审阅第三批（2026-11-18）：匕首雨（群体攻击的感觉）。刀扇在 SkillRedo10（FanOfKnives11），腐蚀波在 CorrosiveFx。
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

	// ═══════════════════════════ 小件 ═══════════════════════════

	/// <summary>两头尖的疾光从 a 直线飞到 b（delay 起飞，dur 秒到）。</summary>
	internal static void DashFly(Control layer, Vector2 a, Vector2 b, float delay, float dur, float len, float thick, Color tint, float hot) =>
		SkillRedo5.StreakSharp(layer, e => a.Lerp(b, e), delay, dur, len, thick, tint, hot);
}
