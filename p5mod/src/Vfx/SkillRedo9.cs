using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 增益牌第一批（2026-11-15）：预判、侧步、磨蚀、速行者、触媒。
/// 照幽魂形态 / 计划妥当被认可的做法：每张一个看得懂的主意象，排得整齐，每一步都配特效（闪光、碎光、疾光、十字光、纸屑）；
/// 普通牌短而利落，能力牌更足。不用网点、不用环绕光圈当主体、不用火焰和黑烟。
/// </summary>
public static partial class SkillRedo5
{
	public static void RegisterBuffs(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["Anticipate"] = Anticipate;
		cards["Sidestep"] = Sidestep;
		cards["Abrasive"] = Abrasive;
		cards["Speedster"] = Speedster;
		cards["Accelerant"] = Accelerant;
	}

	private static readonly Color Hot = new(1f, 0.96f, 0.95f), Red = new(0.95f, 0.07f, 0.16f), Pink = new(1f, 0.3f, 0.38f), Deep = new(0.55f, 0.02f, 0.08f);

	/// <summary>光线往一点收（蓄力）：n 道，在 at 秒前收到。</summary>
	private static void Gather(Control layer, Vector2 at, float h, float u, float arrive, int n = 18, float r0 = 0.6f)
	{
		for (int i = 0; i < n; i++)
		{
			float ang = Mathf.Tau * i / n + Fx.Rand(-0.12f, 0.12f);
			Vector2 dir = new(Mathf.Cos(ang), Mathf.Sin(ang) * 0.85f);
			float ra = h * r0 * Fx.Rand(0.8f, 1.2f), rb = h * Fx.Rand(0.04f, 0.1f);
			float dur = Fx.Rand(0.15f, 0.22f), delay = Mathf.Max(0f, arrive - dur - Fx.Rand(0f, 0.1f));
			Node2D lr = Fx.Root(layer, at, "P5Gather", delay + dur + 0.1);
			Polygon2D ln = Glow.TaperPoly(lr, i % 3 == 0 ? Pal5.Hot : new Color(1f, 0.16f, 0.24f));
			float lw = Fx.Rand(2f, 4f) * u;
			ln.Visible = false;
			Fx.Animate(ln, delay, dur, t =>
			{
				ln.Visible = true;
				float hd = Fx.InCubic(Mathf.Min(1f, t * 1.3f)), tl = Fx.InCubic(t);
				ln.Polygon = Glow.TaperPts(dir * Mathf.Lerp(ra * 1.25f, rb, tl), dir * Mathf.Lerp(ra, rb, hd), lw);
				ln.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 4f) * (1f - Ease5.Seg(t, 0.8f, 0.2f)));
			});
		}
	}

	/// <summary>一下「主拍」：闪光、十字光、碎光、可选的放射疾光。</summary>
	private static void Beat(Control front, Control back, Vector2 at, float h, float u, float size = 1f, int streaks = 0, bool cross = true)
	{
		Glow.Flash(back, at, new Color(1f, 0.12f, 0.2f), h * 2f * size, 0.36f);
		Glow.Flash(front, at, Hot, h * 0.35f * size, 0.1f);
		if (cross) Cross(back, at, h * 0.9f * size);
		Glow.Sparks(front, at, (int)(26 * size), 140f * u, 620f * u * size, 0.6f, new[] { Hot, Pink, Deep }, -40f * u, 0.008f * u, 0.03f * u, Vector2.Up, 180f);
		for (int k = 0; k < streaks; k++)
		{
			float ang = Mathf.Tau * k / streaks + Fx.Rand(-0.12f, 0.12f);
			Vector2 d = new(Mathf.Cos(ang), Mathf.Sin(ang) * 0.75f);
			Vector2 a0 = at + d * h * 0.3f * size, b0 = at + d * h * Fx.Rand(0.9f, 1.25f) * size;
			bool white = k % 3 == 1;
			Streak(back, t => a0.Lerp(b0, t), Fx.Rand(0f, 0.04f), 0.22f, h * 0.45f * size, h * (white ? 0.05f : 0.1f) * size, white ? new Color(1f, 0.9f, 0.9f) : Red, white ? 1.6f : 1.2f);
		}
	}

	// ═══════════════════════════ 预判（本回合 +敏捷，普通） ═══════════════════════════

	/// <summary>
	/// 预判「看穿」约 0.8s：面具上一道横向的光一闪（眼神）；他身上闪过两道红残影 —— 一左一右，是他「看见」的两个闪避位置，
	/// 残影出现时各带一道短疾光，随后贴回身上；回到身上那一下主拍（闪光、十字光、碎光）。
	/// </summary>
	public static void Anticipate(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float eye = 0.04f, peek = 0.16f, snap = 0.5f;
		S("gx_ping", 0.12f, eye);
		S("gx_whoosh", 0.18f, peek);
		S("gx_slice", 0.16f, snap);
		Vector2 face = c.Mask, mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid, new Vector2(h * 2.6f, h * 2.2f), 0.9, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.55f, 0.35f)) * 0.7f);
		// 眼神：一道横向的光从面具上一闪
		Fx.After(front, eye, () =>
		{
			Flare(front, face, h * 1.2f, h * 0.022f, new Color(1f, 0.92f, 0.92f, 1f), 0.28f);
			Flare(front, face, h * 0.8f, h * 0.06f, new Color(1f, 0.15f, 0.22f, 0.9f), 0.3f);
			Glow.Glint(front, face + new Vector2(f * h * 0.03f, 0f), 0.8f);
		});
		// 看穿的三刀：三道刀光从敌人那边斩过来，停在他身前半拍，然后一齐碎掉（他早就看见了）
		float[] angs = { -0.55f, 0.15f, 0.7f };
		for (int i = 0; i < 3; i++)
		{
			int k = i;
			float t0 = peek + 0.06f * k;
			float ang = (f > 0f ? Mathf.Pi : 0f) + angs[k] * f;
			Vector2 dir = Vector2.Right.Rotated(ang);
			Vector2 stop = c.Chest + new Vector2(f * h * (0.28f + 0.04f * k), (k - 1) * h * 0.16f);
			Vector2 from = stop - dir * h * 0.9f;
			var (node, m) = Needle(front, k == 1 ? new Color(1f, 0.92f, 0.92f) : Red, k == 1 ? 1.7f : 1.3f, snap + 0.2);
			float thick = h * (k == 1 ? 0.06f : 0.1f), len = h * 0.75f;
			Fx.Animate(node, t0, snap - t0, t =>
			{
				float s = t0 + t * (snap - t0);
				float e = Ease5.OutQuint(Mathf.Min(1f, (s - t0) / 0.14f));
				Vector2 head = from.Lerp(stop, e);
				node.GlobalPosition = head;
				node.Rotation = dir.Angle();
				float shiver = s > t0 + 0.14f ? Mathf.Sin(s * 90f) * 0.08f : 0f;
				node.Scale = new Vector2(len * (0.3f + 0.7f * e), thick * (1f + shiver));
				m.SetShaderParameter("alpha", Mathf.Min(1f, (s - t0) * 25f));
				m.SetShaderParameter("flow", s * 6f);
			});
			Fx.After(front, snap - 0.01f * k, () =>
			{
				node.Visible = false;
				Phantom.Shards(front, stop, 9, 220f * u, 620f * u, -dir, 70f, 0.4f, 500f * u, 16f * u);
				Glow.Flash(front, stop, Hot, h * 0.3f, 0.1f);
			});
		}
		Fx.After(front, snap, () =>
		{
			Fx.Shake(1);
			Beat(front, back, c.Chest, h, u, 0.8f, 8);
		});
	}

	// ═══════════════════════════ 侧步（下回合 +能量，罕见） ═══════════════════════════

	/// <summary>
	/// 侧步「留一手」约 0.9s：他往后一闪（一层红残影拖着横向疾光往身后掠开再弹回）；
	/// 弹回时从胸口弹出一张小预告信，转着飞到他头顶后方停住、牌面一亮、角上闪光，然后折成一道光收进胸口（存起来，下回合用）。
	/// </summary>
	public static void Sidestep(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = SkillRedo.CardW(c);
		Control back = c.Back ?? c.Front, front = c.Front;
		const float step = 0.02f, ret = 0.3f, hang = 0.55f, stow = 0.78f;
		S("gx_whoosh", 0.2f, step);
		S("gx_ping", 0.12f, hang);
		S("gx_card_stack", 0.14f, stow);
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f), chest = c.Chest;
		Dim(back, mid, new Vector2(h * 2.8f, h * 2.2f), 1.0, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.7f);
		// 往后一闪
		Fx.After(front, step, () =>
		{
			if (RedEcho(c, 0.45) is not { } echo) return;
			var (sp, m) = echo;
			Vector2 off = new(-f * h * 0.2f, 0f);
			Fx.Animate(sp, 0, ret + 0.1, t =>
			{
				float s = t * (ret + 0.1f);
				float e = s < 0.12f ? Ease5.OutQuint(s / 0.12f) : 1f - Fx.InCubic(Ease5.Seg(s, 0.12f, ret - 0.12f));
				sp.Position = off * e;
				m.SetShaderParameter("smear", f * 0.08f * e);
				m.SetShaderParameter("flow", s * 8f);
				m.SetShaderParameter("fill", 0.45f);
				m.SetShaderParameter("alpha", 0.8f * Mathf.Min(1f, s * 20f) * (1f - Ease5.Seg(s, ret - 0.02f, 0.1f)));
			});
			for (int k = 0; k < 5; k++)
			{
				Vector2 a0 = c.Feet + new Vector2(f * h * 0.15f, -h * (0.2f + 0.16f * k)), b0 = a0 + new Vector2(-f * h * 0.8f, 0f);
				bool white = k % 2 == 1;
				Streak(back, t => a0.Lerp(b0, t), 0.02f * k, 0.2f, h * 0.5f, h * (white ? 0.035f : 0.08f), white ? new Color(1f, 0.9f, 0.9f) : Red, white ? 1.6f : 1.2f);
			}
		});
		// 弹回：小卡弹出、停在头顶后方、收进胸口
		Vector2 park = c.Feet + new Vector2(-f * h * 0.3f, -h * 1.18f);
		Phantom.P5Card card = Phantom.Card(front, chest, w * 1.5f, stow + 0.2, 0.5f);
		card.Root.Visible = false;
		bool off_ = false;
		Fx.Animate(card.Root, ret, stow - ret + 0.05, t =>
		{
			float s = ret + t * (stow - ret + 0.05f);
			card.Root.Visible = true;
			Vector2 pos;
			float rot, flip, sc = 1f, glow = 0f;
			if (s < stow)
			{
				float e = Ease5.OutQuint(Mathf.Min(1f, (s - ret) / 0.22f));
				Vector2 ctl = (chest + park) * 0.5f + new Vector2(-f * h * 0.25f, -h * 0.1f);
				pos = chest.Lerp(ctl, e).Lerp(ctl.Lerp(park, e), e) + new Vector2(0f, Mathf.Sin(s * 6f) * h * 0.01f);
				rot = (1f - e) * f * 3f - f * 0.15f;
				flip = Mathf.Pi * (1f - e) * 3f;
				glow = s > hang ? 0.8f * Mathf.Exp(-(s - hang) * 8f) : 0f;
				card.Set("sheen", Mathf.Lerp(-1f, 2f, Mathf.Clamp((s - hang) / 0.2f, 0f, 1f)));
				if (s > ret + 0.24f && !off_) { off_ = true; card.Trail?.Kill(); }
			}
			else
			{
				pos = park;
				rot = -f * 0.15f;
				flip = 0f;
			}
			card.Place(pos, rot, flip, sc);
			card.Set("glow", glow);
		});
		Fx.After(front, ret, () => { Fx.Shake(1); Glow.Flash(front, chest, Hot, h * 0.3f, 0.1f); Glow.Flash(back, chest, new Color(1f, 0.12f, 0.2f), h * 1.4f, 0.3f); });
		Fx.After(front, hang, () =>
		{
			Glow.Glint(front, park + new Vector2(w * 0.4f, -w * 0.6f), 0.9f);
			Glow.Flash(back, park, new Color(1f, 0.12f, 0.2f), h * 0.9f, 0.3f);
			Glow.Sparks(front, park, 12, 60f * u, 260f * u, 0.5f, new[] { Hot, Pink }, 40f * u, 0.01f * u, 0.028f * u, Vector2.Up, 160f);
		});
		Fx.After(front, stow, () =>
		{
			card.Root.Visible = false;
			Streak(back, t => park.Lerp(chest, t * t), 0f, 0.16f, h * 0.55f, h * 0.14f, Red, 1.25f);
			Streak(front, t => park.Lerp(chest, t * t), 0.01f, 0.16f, h * 0.45f, h * 0.05f, new Color(1f, 0.9f, 0.9f), 1.7f);
			Fx.After(front, 0.15f, () =>
			{
				Glow.Flash(front, chest, Hot, h * 0.35f, 0.12f);
				Glow.Flash(back, chest, new Color(1f, 0.12f, 0.2f), h * 1.2f, 0.3f);
				Glow.Sparks(front, chest, 14, 80f * u, 300f * u, 0.5f, new[] { Hot, Pink }, -30f * u, 0.01f * u, 0.028f * u, Vector2.Up, 180f);
			});
		});
	}

	// ═══════════════════════════ 磨蚀（能力：+敏捷 +荆棘，稀有） ═══════════════════════════

	/// <summary>
	/// 磨蚀「刃衣」约 1.2s：光线往他身上收；主拍时一圈短刀光从他身上朝四面「刺」出去（荆棘），停半拍、刀尖一齐闪一下，
	/// 再一齐收回贴到身上；他身边留一层很淡的红光边（一下就收），碎光往上飘。
	/// </summary>
	public static void Abrasive(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float beat = 0.36f, glint = 0.62f, pull = 0.8f;
		S("gx_slice", 0.24f, beat);
		S("gx_ping", 0.12f, glint);
		S("gx_whoosh", 0.16f, pull);
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid, new Vector2(h * 3f, h * 2.6f), 1.3, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.2f)) * (1f - Ease5.Seg(s, 0.9f, 0.4f)) * 0.8f);
		Gather(front, mid, h, u, beat, 20, 0.7f);
		const int n = 16;
		for (int i = 0; i < n; i++)
		{
			float ang = Mathf.Tau * i / n + Fx.Rand(-0.08f, 0.08f);
			Vector2 d = new(Mathf.Cos(ang), Mathf.Sin(ang) * 1.25f);
			d = d.Normalized();
			// 刀根贴着身体轮廓（椭圆），刀尖朝外
			Vector2 root = mid + new Vector2(Mathf.Cos(ang) * h * 0.2f, Mathf.Sin(ang) * h * 0.45f);
			float len = h * (i % 2 == 0 ? Fx.Rand(0.48f, 0.6f) : Fx.Rand(0.3f, 0.38f));
			bool inFront = Mathf.Sin(ang) > 0.2f;
			var (node, m) = Needle(inFront ? front : back, i % 3 == 1 ? new Color(1f, 0.9f, 0.9f) : Red, i % 3 == 1 ? 1.7f : 1.25f, pull + 0.3);
			float thick = h * (i % 3 == 1 ? 0.05f : 0.11f);
			Fx.Animate(node, beat, pull - beat + 0.25, t =>
			{
				float s = beat + t * (pull - beat + 0.25f);
				float outE = Ease5.OutQuint(Mathf.Min(1f, (s - beat) / 0.09f));
				float inE = Fx.InCubic(Ease5.Seg(s, pull, 0.2f));
				float l = len * outE * (1f - inE);
				node.GlobalPosition = root + d * (l + h * 0.02f);
				node.Rotation = d.Angle();
				node.Scale = new Vector2(Mathf.Max(1f, l), thick * (1f + 0.8f * Mathf.Exp(-Mathf.Max(0f, s - glint) * 12f) * (s > glint ? 1f : 0f)));
				m.SetShaderParameter("alpha", Mathf.Min(1f, (s - beat) * 30f) * (1f - inE));
				m.SetShaderParameter("flow", s * 6f);
			});
			Vector2 tip = root + d * (len + h * 0.02f);
			Fx.After(front, glint + 0.01f * i, () => Glow.Glint(front, tip, 0.45f));
		}
		Fx.After(front, beat, () =>
		{
			HitStop.Request(0.05);
			Fx.Shake(2);
			Beat(front, back, mid, h, u, 1.1f, 0);
		});
		Fx.After(front, pull + 0.18f, () =>
		{
			Glow.Flash(front, mid, Hot, h * 0.4f, 0.12f);
			Glow.Flash(back, mid, new Color(1f, 0.12f, 0.2f), h * 1.6f, 0.36f);
			Phantom.Confetti(front, mid, 12, 300f * u, Vector2.Up, 160f, 0.8f, 6f * u);
		});
		Glitter(front, c.Feet, h, u, beat + 0.1f, 0.9f, 7, 0.45f);
	}

	// ═══════════════════════════ 速行者（能力：回合中抽牌 → 对所有敌人造成伤害，罕见） ═══════════════════════════

	/// <summary>
	/// 速行者「疾风牌」约 1.1s：他手边一叠牌被飞快地捻开（一张接一张弹出、每张一道疾光），
	/// 每张牌化成一道红白疾光掠过整排敌人（每个敌人身上一下小闪光 + 碎光），最后一道最亮、轻震。
	/// </summary>
	public static void Speedster(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = SkillRedo.CardW(c);
		Control back = c.Back ?? c.Front, front = c.Front;
		const int n = 5;
		S("gx_fan_open", 0.18f, 0.02f);
		S("gx_whoosh", 0.22f, 0.3f);
		S("gx_slice", 0.2f, 0.72f);
		Vector2 hand = c.Hand, mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid + new Vector2(f * h * 0.8f, 0f), new Vector2(h * 4.6f, h * 2.4f), 1.2, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.85f, 0.35f)) * 0.7f);
		var targets = new List<Vector2>();
		foreach (var v in c.Enemies) targets.Add(v.Feet + (v.Top - v.Feet) * 0.5f);
		if (targets.Count == 0) targets.Add(c.Chest + new Vector2(f * h * 1.6f, 0f));
		targets.Sort((a, b) => (f * a.X).CompareTo(f * b.X));
		Vector2 far = targets[^1] + new Vector2(f * h * 0.6f, 0f);
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float pop = 0.04f + 0.07f * k, go = 0.28f + 0.1f * k;
			bool last = k == n - 1;
			// 弹出：从手边一张张弹起排成一小把扇
			Vector2 slot = hand + new Vector2(f * h * (0.16f + 0.07f * k), -h * (0.3f + 0.05f * Mathf.Sin(k * 1.3f)));
			float tilt = f * (-0.5f + 0.25f * k);
			Phantom.P5Card card = Phantom.Card(front, hand, w * 1.25f, go + 0.1, 0.4f);
			card.Root.Visible = false;
			Fx.Animate(card.Root, pop, go - pop, t =>
			{
				card.Root.Visible = true;
				float e = Ease5.OutQuint(Mathf.Min(1f, t * 2.2f));
				card.Place(hand.Lerp(slot, e), tilt * e, Mathf.Pi * (1f - e) * 2f, 0.6f + 0.4f * e);
				card.Set("glow", 0.5f * Mathf.Exp(-t * 6f));
			});
			// 化成疾光，掠过整排敌人
			Fx.After(front, go, () =>
			{
				card.Trail?.Kill();
				card.Root.Visible = false;
				Glow.Flash(front, slot, Hot, w * 1.2f, 0.1f);
				float y = Fx.Rand(-0.12f, 0.12f) * h;
				Vector2 a0 = slot, b0 = far + new Vector2(0f, y);
				float thick = h * (last ? 0.24f : 0.15f);
				Streak(back, t => a0.Lerp(b0, t), 0f, last ? 0.32f : 0.26f, h * (last ? 1.8f : 1.3f), thick, Red, 1.25f);
				Streak(front, t => a0.Lerp(b0, t), 0.01f, last ? 0.32f : 0.26f, h * (last ? 1.5f : 1.1f), thick * 0.35f, new Color(1f, 0.92f, 0.92f), 1.7f);
				foreach (Vector2 tg in targets)
				{
					float when = (tg.X - a0.X) / (b0.X - a0.X) * (last ? 0.32f : 0.26f) * 0.7f;
					Vector2 hit = new(tg.X, Mathf.Lerp(a0.Y, b0.Y, (tg.X - a0.X) / (b0.X - a0.X)));
					Fx.After(front, Mathf.Max(0f, when), () =>
					{
						Glow.Flash(front, hit, Hot, h * (last ? 0.4f : 0.22f), 0.1f);
						Glow.Sparks(front, hit, last ? 16 : 7, 100f * u, 420f * u, 0.4f, new[] { Hot, Pink }, 120f * u, 0.008f * u, 0.026f * u, new Vector2(f, -0.2f), 50f);
					});
				}
				if (last)
				{
					Fx.Shake(2);
					foreach (var v in c.Enemies)
						if (v.Tint is { } tint)
							Fx.Animate(front, 0.04, 0.25, t => tint(Colors.White.Lerp(new Color(1.7f, 1.45f, 1.45f), Mathf.Sin(t * Mathf.Pi))));
				}
			});
		}
		Fx.After(front, 0.02f, () => Glow.Flash(back, hand, new Color(1f, 0.12f, 0.2f), h * 1.2f, 0.3f));
		Glitter(front, c.Feet, h, u, 0.4f, 0.7f, 5, 0.4f);
	}

	// ═══════════════════════════ 触媒（能力：中毒额外触发，罕见） ═══════════════════════════

	/// <summary>
	/// 触媒「催化」约 1.2s：他胸前聚起一颗暗红的光核（光线往里收、越聚越亮）；主拍时光核炸开，
	/// 一道道暗红疾光分头射向每个敌人；打中时敌人身上暗红一闪，紧跟着又一闪（毒被催了两下），碎光往上冒。
	/// </summary>
	public static void Accelerant(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float burst = 0.42f, land = 0.66f;
		S("gx_freeze", 0.16f, 0.02f);
		S("gx_pop", 0.2f, burst);
		S("gx_ping", 0.12f, land + 0.16f);
		Vector2 core = c.Chest + new Vector2(f * h * 0.32f, -h * 0.05f), mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid + new Vector2(f * h * 0.8f, 0f), new Vector2(h * 4.6f, h * 2.6f), 1.3, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.2f)) * (1f - Ease5.Seg(s, 0.9f, 0.4f)) * 0.8f);
		Gather(front, core, h, u, burst, 18, 0.55f);
		// 光核：越聚越亮
		Node2D cr = Fx.Root(front, core, "P5Catalyst", burst + 0.1);
		Sprite2D outer = Glow.SoftSprite(cr, Vector2.Zero, new Color(0.8f, 0.04f, 0.12f, 0f), 1f);
		Sprite2D inner = Glow.SoftSprite(cr, Vector2.Zero, new Color(1f, 0.85f, 0.85f, 0f), 1f);
		Fx.Animate(cr, 0, burst, t =>
		{
			float e = t * t;
			float wob = 1f + 0.08f * Mathf.Sin(t * 40f) * t;
			outer.Scale = Vector2.One * h * (0.3f + 0.6f * e) * wob / 32f;
			inner.Scale = Vector2.One * h * (0.08f + 0.16f * e) * wob / 32f;
			outer.Modulate = new Color(0.8f, 0.04f, 0.12f, 0.9f * Mathf.Min(1f, t * 3f));
			inner.Modulate = new Color(1f, 0.85f, 0.85f, Mathf.Min(1f, t * 2f));
		});
		Fx.After(front, burst, () =>
		{
			HitStop.Request(0.04);
			Fx.Shake(1);
			Beat(front, back, core, h, u, 0.8f, 0, cross: true);
			foreach (var v in c.Enemies)
			{
				Vector2 tg = v.Feet + (v.Top - v.Feet) * 0.45f;
				Vector2 a0 = core, ctl = (core + tg) * 0.5f + new Vector2(0f, -h * Fx.Rand(0.2f, 0.45f));
				Vector2 Path(float t) => a0.Lerp(ctl, t).Lerp(ctl.Lerp(tg, t), t);
				Streak(back, Path, 0f, land - burst, h * 0.8f, h * 0.17f, new Color(0.8f, 0.03f, 0.13f), 1.15f);
				Streak(front, Path, 0.01f, land - burst, h * 0.6f, h * 0.055f, new Color(1f, 0.85f, 0.85f), 1.7f);
				var vv = v;
				for (int k = 0; k < 2; k++)
				{
					float d = land - burst + 0.16f * k;
					Fx.After(front, d, () =>
					{
						Glow.Flash(back, tg, new Color(0.85f, 0.05f, 0.15f), vv.Width * 2.4f, 0.34f);
						Glow.Flash(front, tg, Hot, vv.Width * 0.4f, 0.1f);
						Glow.Sparks(front, tg, 22, 80f * u, 420f * u, 0.8f, new[] { Hot, new Color(0.9f, 0.1f, 0.2f), Deep }, -80f * u, 0.01f * u, 0.03f * u, Vector2.Up, 70f);
						if (vv.Tint is { } tint) Fx.Animate(front, 0, 0.2, t => tint(Colors.White.Lerp(new Color(1.5f, 1.0f, 1.1f), Mathf.Sin(t * Mathf.Pi))));
					});
				}
			}
		});
		Glitter(front, c.Feet, h, u, 0.1f, 0.4f, 4, 0.35f);
	}
}
