using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 0.9.49 审阅第四批（2026-11-18，「装弹」一组）：刀刃交响曲、钢铁风暴（新做，原来走通用积木）。
/// 刀刃之舞（音效）、隐秘匕首（弃牌音效）改在共用的 SkillFx.Load / Discard 里；墨之刃等 Codex 的墨贴图。
/// </summary>
public static partial class SkillRedo5
{
	public static void RegisterBatch11(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["BladeSymphony"] = BladeSymphony;
		cards["StormOfSteel"] = StormOfSteel;
	}

	/// <summary>
	/// 刀刃交响曲（给所有队友生成小刀）：「一串音符」。他一挥手，四周稍暗，子弹按节拍一发一发弹起、排成一道往上扬的弧（每发「叮」、星光），
	/// 停一拍，一齐俯冲进弹巢，收在一记「和弦」上（白闪 + 十字光 + 碎光 + 「咔」），身上红残影一胀。
	/// </summary>
	public static void BladeSymphony(SkillCtx c)
	{
		float h = c.Tall;
		Control back = BackOf(c);
		int n = Mathf.Clamp(c.Count > 0 ? c.Count : 2, 1, 4);
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		float end = SkillFx.LoadArc(c.Front, c.Hand, n, c.Face);
		Dim(back, mid + new Vector2(c.Face * h * 0.3f, -h * 0.2f), new Vector2(h * 3.4f, h * 3f), end + 0.5, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.5f);
		Glow.Flash(back, c.Hand + new Vector2(c.Face * h * 0.3f, -h * 0.4f), new Color(0.7f, 0.03f, 0.12f), h * 1.6f, end + 0.2f);
		Fx.After(c.Front, end, () =>
		{
			HitStop.Request(0.03);
			EchoPulse(c, 0.05f, 0.3f, 0.6f);
		});
	}

	/// <summary>
	/// 钢铁风暴（弃掉整手，每张换一把小刀）：「钢铁风暴」。四周压暗，他身边一下刮起一阵交错乱窜的银白 / 红疾光（不绕圈，每道从一个方向斜穿过他身边），
	/// 越刮越密；一顿 —— 所有光往他手里一收（光线往手上收、白闪、十字光、身上红残影一胀、顿帧轻震），然后按弃掉的张数装弹（c.Count）。
	/// </summary>
	public static void StormOfSteel(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = BackOf(c), front = c.Front;
		int n = c.Count >= 0 ? Mathf.Clamp(c.Count, 0, 10) : 5;
		const float storm = 0.5f;
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		S("gx_whoosh", 0.16f, 0.02f);
		S("gx_whoosh", 0.12f, 0.2f);
		S("gx_slice", 0.2f, storm);
		Dim(back, mid, new Vector2(h * 3.8f, h * 3f), storm + 0.9, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.12f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.65f);
		Glow.Flash(back, mid, new Color(0.7f, 0.03f, 0.12f), h * 2f, storm + 0.3f);
		// 弃掉的牌：从手里甩出去烧掉（最多画 4 张）
		if (n > 0) SkillFx.Discard(front, c.Hand, Mathf.Min(n, 4), f);
		// 风暴：交错乱窜的疾光，越到后面越密
		int streaks = 16 + 2 * n;
		for (int i = 0; i < streaks; i++)
		{
			float d = storm * Mathf.Pow(Fx.Rand(0f, 1f), 0.7f) * 0.9f;
			float ang = Fx.Rand(0f, Mathf.Tau);
			Vector2 dir = Vector2.Right.Rotated(ang);
			Vector2 center = mid + new Vector2(Fx.Rand(-0.35f, 0.35f) * h, Fx.Rand(-0.45f, 0.4f) * h);
			Vector2 a = center - dir * h * Fx.Rand(0.7f, 1.1f), b = center + dir * h * Fx.Rand(0.7f, 1.1f);
			Color col = i % 3 == 0 ? new Color(1f, 0.15f, 0.22f) : new Color(1f, 0.85f, 0.85f);
			StreakSharp(i % 2 == 0 ? front : back, e => a.Lerp(b, e), d, Fx.Rand(0.13f, 0.2f), h * Fx.Rand(0.45f, 0.7f), Fx.Rand(18f, 30f) * u, col, 1.6f);
			if (i % 3 == 0)
			{
				Vector2 m = a.Lerp(b, Fx.Rand(0.4f, 0.6f));
				Fx.After(front, d + 0.07f, () =>
				{
					Glow.Glint(front, m, 0.5f);
					Glow.Flash(front, m, new Color(1f, 0.35f, 0.4f), h * 0.25f, 0.15f);
				});
				Phantom.Shards(front, m, 4, 250f * u, 600f * u, dir, 60f, 0.25f, 400f * u, 12f * u, d + 0.07f);   // 交错处迸的金属火花
			}
		}
		Gather(front, c.Hand, h, u, storm, 18, 0.8f);
		Fx.After(front, storm, () =>
		{
			HitStop.Request(0.05);
			Fx.Shake(1);
			Glow.Flash(front, c.Hand, Hot, h * 0.4f, 0.12f);
			Glow.Flash(back, mid, Red, h * 2.4f, 0.4f);
			Cross(front, c.Hand, h * 0.5f);
			EchoPulse(c, 0.07f);
			if (n > 0) SkillFx.Load(front, c.Hand, Mathf.Min(n, 6), f);
		});
		if (c.SelfTint is { } tint)
			Fx.Animate(front, 0, storm + 0.6, t =>
			{
				float s = t * (storm + 0.6f);
				float k = 0.25f * Ease5.Seg(s, 0f, storm) + (s > storm ? 0.9f * Mathf.Exp(-(s - storm) * 5f) : 0f);
				tint(Colors.White.Lerp(new Color(1.5f, 0.75f, 0.8f), k));
			});
	}
}
