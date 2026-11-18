using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 0.9.49 审阅第五批（2026-11-18，多段攻击）：终结技、飞镖、连续反弹、回响斩击。
/// 共同点：原版每一下都重放一次攻击动作（终结技 / 飞镖 / 连续反弹）或者根本不放（回响斩击，人物停在举刀的架势上）。
/// 改成：攻击动作只挥一次（AttackHitVfxPatch 关掉 _playOnEveryHit、拉开每下的间隔），后面每一下由这里的特效表现。
/// 材质都用用户认可的：打击牌命中的手绘刀光（slash_thin / slash_heavy）、疾光、原版火星。
/// </summary>
public static class Batch5Fx
{
	private static readonly Color Hot = new(1f, 0.95f, 0.94f), Red = new(1f, 0.12f, 0.2f);

	/// <summary>这几张牌：动作只挥一次，每下之间隔多少秒（AttackHitVfxPatch 用）。</summary>
	public static readonly Dictionary<string, float> OneSwing = new() { ["Finisher"] = 0.13f, ["Flechettes"] = 0.09f, ["Ricochet"] = 0.17f };

	private static string? _card;
	private static int _index, _total;
	private static Vector2 _last;
	private static ulong _until;

	/// <summary>一张多段攻击牌开始结算：记下牌名、总段数。</summary>
	public static void Begin(string card, int total)
	{
		_card = card;
		_index = 0;
		_total = Mathf.Max(1, total);
		_until = Stage.NowMs() + 4000;
	}

	/// <summary>DamagePatch：这一下是不是由这里表现（是就放特效，返回 true，调用方不再放普通命中）。</summary>
	public static bool Hit(Control layer, string card, NCreatureView joker, Vector2 target, float targetH, System.Action<float>? delayNumber)
	{
		if (card != _card || Stage.NowMs() > _until || !GodotObject.IsInstanceValid(layer))
		{
			return false;
		}
		int i = _index++;
		bool last = i == _total - 1;
		switch (card)
		{
			case "Finisher": Finisher(layer, joker, target, targetH, i, last); return true;
			case "Flechettes": delayNumber?.Invoke(0.07f); Flechette(layer, joker, target, targetH, i, last); return true;
			case "Ricochet": delayNumber?.Invoke(0.08f); Ricochet(layer, joker, target, targetH, i, last); return true;
			case "EchoingSlash": return false;
		}
		return false;
	}

	/// <summary>Joker 的几个位置（只取一次，不依赖节点类型）。</summary>
	public readonly record struct NCreatureView(Vector2 Hand, Vector2 Chest, float Tall, float Face);

	// ───────────────────────── 终结技 ─────────────────────────

	/// <summary>
	/// 终结技（这回合每打过一张攻击牌就再打一下）：第一下就是他自己那一刀（普通命中）；之后每一下是「这回合打过的那些刀」的残影追斩 ——
	/// 一道暗红的残刀疾光从他那边斜着飞到目标、目标身上一道手绘刀光（每刀换个角度），越往后越快越密；最后一下重斩 + 顿帧 + 震屏。
	/// </summary>
	private static void Finisher(Control layer, NCreatureView j, Vector2 at, float h, int i, bool last)
	{
		float u = Fx.Screen(layer).Y / 1080f, f = j.Face;
		if (i == 0)
		{
			KnifeFx.Hit(layer, at, f, last);
			return;
		}
		float ang = (i % 2 == 0 ? -0.6f : 0.55f) + Fx.Rand(-0.25f, 0.25f);
		Vector2 from = j.Chest + new Vector2(f * j.Tall * 0.3f, -j.Tall * Fx.Rand(-0.15f, 0.25f));
		SkillRedo5.StreakSharp(layer, e => from.Lerp(at, e), 0f, 0.07f, j.Tall * 0.5f, 18f * u, new Color(0.85f, 0.06f, 0.14f), 1.3f);
		float w = (last ? 0.62f : 0.46f) * f;
		Paint.Sweep(layer, last ? "slash_heavy" : "slash_thin", at + new Vector2(Fx.Rand(-0.15f, 0.15f) * h, Fx.Rand(-0.2f, 0.2f) * h), ang * f,
			new Vector2(w, Mathf.Abs(w) * 0.75f), Lut5.Blood, 0.05f, 0.04f, last ? 0.3f : 0.22f, 36f, 0.05f, 1.2f);
		Fx.After(layer, 0.05f, () =>
		{
			Glow.Flash(layer, at, new Color(1f, 0.55f, 0.5f), (last ? 320f : 200f) * u, 0.14f);
			Vanilla.Burst(layer, Vanilla.Specks, at, ang, last ? 1.1f : 0.7f, Lut5.Blood, amount: last ? 18 : 6);
			if (last)
			{
				HitStop.Request(0.07);
				Fx.Shake(2);
				Glow.Flash(layer, at, Red, 520f * u, 0.4f);
			}
		});
	}

	// ───────────────────────── 飞镖 ─────────────────────────

	/// <summary>
	/// 飞镖（手里每有一张技能牌就打一下）：他只甩一次手，一把一把飞刀（两头尖的白热红疾光）接连从手边射出去，
	/// 每把的出手点和弧度错开一点，扎进目标时一点白闪 + 少量火星；最后一把更大、扎进去顿一下。
	/// </summary>
	private static void Flechette(Control layer, NCreatureView j, Vector2 at, float h, int i, bool last)
	{
		float u = Fx.Screen(layer).Y / 1080f, f = j.Face;
		Vector2 from = j.Hand + new Vector2(0f, Fx.Rand(-0.08f, 0.08f) * j.Tall);
		Vector2 to = at + new Vector2(Fx.Rand(-0.2f, 0.2f) * h, Fx.Rand(-0.3f, 0.25f) * h);
		Vector2 bow = new Vector2(0f, -j.Tall * Fx.Rand(0.05f, 0.18f));
		SkillRedo5.StreakSharp(layer, e => from.Lerp(to, e) + bow * 4f * e * (1f - e), 0f, 0.1f, j.Tall * 0.6f, (last ? 52f : 40f) * u, Red, 1.6f);
		SkillRedo5.StreakSharp(layer, e => from.Lerp(to, e) + bow * 4f * e * (1f - e), 0.01f, 0.1f, j.Tall * 0.4f, (last ? 16f : 12f) * u, Hot, 2f);
		Glow.Flash(layer, from, new Color(1f, 0.4f, 0.4f), j.Tall * 0.18f, 0.1f);
		Fx.After(layer, 0.09f, () =>
		{
			Glow.Flash(layer, to, Hot, (last ? 200f : 140f) * u, 0.08f);
			Glow.Flash(layer, to, Red, (last ? 300f : 180f) * u, 0.2f);
			Phantom.Shards(layer, to, last ? 10 : 5, 220f * u, 560f * u, new Vector2(f, -0.2f), 55f, 0.28f, 600f * u, 12f * u);
			if (last)
			{
				HitStop.Request(0.05);
				Fx.Shake(1);
				Vanilla.Burst(layer, Vanilla.Specks, to, 0f, 0.9f, Lut5.Blood, amount: 10);
			}
		});
	}

	// ───────────────────────── 连续反弹 ─────────────────────────

	/// <summary>
	/// 连续反弹（随机敌人打 N 下；用户：不是枪击）：他甩出一把刀（旋转的红刃 + 拖尾），从他手边飞到第一个目标、弹开、带一道弧飞向下一个目标……
	/// 每次弹到一个敌人：刀光一划（手绘刀光）、白闪、火星、一声金属响；最后一下弹完刀碎成碎光。
	/// </summary>
	private static void Ricochet(Control layer, NCreatureView j, Vector2 at, float h, int i, bool last)
	{
		float u = Fx.Screen(layer).Y / 1080f, f = j.Face;
		Vector2 from = i == 0 ? j.Hand : _last;
		_last = at;
		Vector2 mid = from.Lerp(at, 0.5f) + new Vector2(0f, -Mathf.Max(60f * u, from.DistanceTo(at) * 0.25f));
		System.Func<float, Vector2> path = e => from.Lerp(mid, e).Lerp(mid.Lerp(at, e), e);
		SkillRedo5.StreakSharp(layer, path, 0f, 0.09f, j.Tall * 0.45f, 22f * u, Red, 1.5f);
		// 旋转的刃（小的手绘刀光一直在转）
		Node2D blade = Fx.Root(layer, from, "P5RicoBlade", 0.12);
		Polygon2D g = Glow.TaperPoly(blade, new Color(1f, 0.15f, 0.22f, 0.9f));
		Polygon2D c = Glow.TaperPoly(blade, Hot);
		float r = 26f * u;
		g.Polygon = Glow.TaperPts(new Vector2(-r, 0f), new Vector2(r, 0f), 12f * u);
		c.Polygon = Glow.TaperPts(new Vector2(-r * 0.8f, 0f), new Vector2(r * 0.8f, 0f), 4f * u);
		Fx.Animate(blade, 0, 0.09, t =>
		{
			blade.GlobalPosition = path(Ease5.OutQuint(t));
			blade.Rotation = t * 18f * f;
		});
		Fx.After(layer, 0.08f, () =>
		{
			Sfx.Take("gx_slice", last ? 0.18f : 0.12f);
			float ang = Fx.Rand(-0.8f, 0.8f);
			Paint.Sweep(layer, "slash_thin", at, ang, new Vector2(0.4f * f, 0.3f), Lut5.Blood, 0.04f, 0.03f, 0.2f, 30f, 0f, 1.2f);
			Glow.Flash(layer, at, Hot, 140f * u, 0.08f);
			Vanilla.Burst(layer, Vanilla.Specks, at, ang, 0.7f, Lut5.Blood, amount: 6);
			if (last)
			{
				HitStop.Request(0.04);
				Phantom.Shards(layer, at, 12, 300f * u, 800f * u, Vector2.Up, 120f, 0.35f, 700f * u, 14f * u);
			}
		});
	}

	// ───────────────────────── 回响斩击 ─────────────────────────

	private static int _echoWave;
	private static ulong _echoUntil;

	/// <summary>回响斩击开始结算（EchoingSlashPatch）。</summary>
	public static void BeginEcho()
	{
		_echoWave = 0;
		_echoUntil = Stage.NowMs() + 4000;
	}

	/// <summary>
	/// 回响斩击（对所有敌人造成伤害；每杀死一个敌人就再来一次）：每一波 = 一道横着切过整排敌人的长刀光（Glow.Blade + 手绘刀光 + 每个敌人身上的命中）；
	/// 第二波起是「回响」：晚一拍（这一波的伤害数字也一起推迟），刀光是暗红的残影、从反方向切回来，每波更快。返回这一波推迟多少秒（数字用）。
	/// </summary>
	public static float EchoWave(Control layer, IReadOnlyList<(Vector2 At, Vector2 Top, float W)> foes, float face)
	{
		if (Stage.NowMs() > _echoUntil || !GodotObject.IsInstanceValid(layer) || foes.Count == 0)
		{
			return 0f;
		}
		int wave = _echoWave++;
		float delay = wave == 0 ? 0f : 0.32f * wave;
		float u = Fx.Screen(layer).Y / 1080f, f = wave % 2 == 0 ? face : -face;
		float minX = float.MaxValue, maxX = float.MinValue, line = 0f, tall = 0f;
		foreach (var e in foes)
		{
			minX = Mathf.Min(minX, e.At.X - e.W * 0.5f);
			maxX = Mathf.Max(maxX, e.At.X + e.W * 0.5f);
			line += e.At.Y;
			tall = Mathf.Max(tall, (e.At - e.Top).Length());
		}
		line /= foes.Count;
		float y = line - tall * (0.1f + 0.12f * (wave % 3));
		Vector2 a = new(f > 0 ? minX - 80f * u : maxX + 80f * u, y + 30f * u), b = new(f > 0 ? maxX + 100f * u : minX - 100f * u, y - 40f * u);
		bool echo = wave > 0;
		Fx.After(layer, delay, () =>
		{
			Sfx.Take(echo ? "gx_whoosh" : "gx_slice", echo ? 0.16f : 0.2f);
			Color tint = echo ? new Color(0.7f, 0.04f, 0.12f) : Red;
			Glow.Blade(layer, a.Lerp(b, 0.5f), (b - a).Angle(), (b - a).Length(), (echo ? 52f : 64f) * u, 0.34f, 0f, tint);
			Cut.Razor(layer, a.Lerp(b, 0.5f), (b - a).Angle(), (b - a).Length() * 0.9f, (echo ? 5f : 7f) * u, 0.2f);
			if (echo)
			{
				// 回响：同一刀的两道残影，一前一后错开
				Glow.Blade(layer, a.Lerp(b, 0.5f) + new Vector2(0f, 26f * u), (b - a).Angle(), (b - a).Length() * 0.85f, 26f * u, 0.3f, 0.05f, new Color(0.5f, 0.02f, 0.08f));
			}
			HitStop.Request(echo ? 0.05 : 0.04);
			Fx.Shake(echo ? 2 : 1);
			foreach (var e in foes)
			{
				KnifeFx.Hit(layer, e.At, f, echo);
			}
		});
		return delay;
	}
}
