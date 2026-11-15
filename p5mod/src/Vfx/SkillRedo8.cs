using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 计划妥当第四版「牌阵」（2026-11-15；用户：打出时可以再华丽一些）。约 1.6s：
/// ① 0 五张大牌从手里甩出、带弧线飞到他身前上方，排成一道整齐的弧，落位各闪一下（牌背朝外）；
/// ② 0.36 起五张牌从左到右依次翻面（亮出牌面 = 计划摊开），翻到侧面时一道高光；
/// ③ 0.78 一道疾光沿着牌阵扫过去，牌依次亮起、角上一点闪光 —— 计划定了；
/// ④ 0.98 牌一齐收拢成一摞，光线往那一摞收；1.16 封印：闪光、十字光、纸屑、轻震；
/// ⑤ 1.22 这一摞一张张飞进手牌区（保留手牌）。
/// 牌阵、收拢点都不跟手走（cast_glove 的手 0.7s 就放下了）。
/// </summary>
public static partial class SkillRedo5
{
	public static void WellLaidPlansV4(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = SkillRedo.CardW(c) * 1.6f;
		Control back = c.Back ?? c.Front, front = c.Front;
		const int n = 5;
		const float reveal = 0.36f, lockAt = 0.78f, gather = 0.98f, seal = 1.16f, send = 1.22f, life = 1.7f;
		S("gx_fan_open", 0.22f, 0.02f);
		S("gx_slice", 0.18f, lockAt);
		S("gx_card_stack", 0.24f, seal);
		// 牌阵：他身前（朝敌人那边）一把像拿在手里一样的扇形牌，互相压着；扇心在下
		Vector2 pivot = c.Feet + new Vector2(f * h * 0.58f, -h * 0.42f);
		float rad = h * 0.5f;
		Vector2 center = pivot + new Vector2(0f, -rad);
		Vector2 stack = pivot + new Vector2(0f, -rad * 0.75f);
		Dim(back, center + new Vector2(0f, h * 0.3f), new Vector2(h * 3.4f, h * 2.6f), life,
			s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.2f)) * (1f - Ease5.Seg(s, life - 0.4f, 0.4f)) * 0.85f);
		// 牌阵后面的背光：落位时亮起，定下来的那一下再涨一下，收拢时跟着收
		Node2D glow = Fx.Root(back, center, "P5PlanGlow", life);
		Sprite2D gs = Glow.SoftSprite(glow, Vector2.Zero, new Color(1f, 0.1f, 0.18f, 0f), 1f);
		Fx.Animate(glow, 0, life - 0.05, t =>
		{
			float s = t * (life - 0.05f);
			float pulse = s > lockAt ? Mathf.Exp(-(s - lockAt) * 5f) : 0f;
			float a = Fx.OutCubic(Ease5.Seg(s, 0.15f, 0.3f)) * (1f - Ease5.Seg(s, gather, 0.25f));
			glow.GlobalPosition = center.Lerp(stack, Ease5.Seg(s, gather, 0.2f));
			gs.Scale = new Vector2(h * (1.4f + 0.3f * pulse), h * (1.0f + 0.25f * pulse)) / 32f;
			gs.Modulate = new Color(1f, 0.1f, 0.18f, (0.4f + 0.4f * pulse) * a);
		});
		var slots = new Vector2[n];
		var tilts = new float[n];
		for (int i = 0; i < n; i++)
		{
			float k = i / (n - 1f) * 2f - 1f;   // -1 … 1（左 → 右）
			float a = k * 0.58f;
			slots[i] = pivot + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * rad;
			tilts[i] = a;
		}
		for (int i = 0; i < n; i++)
		{
			int k = i;
			// 从中间往两边落（离中间越远越晚）
			float order = Mathf.Abs(k - (n - 1) / 2f) / ((n - 1) / 2f);
			float d = 0.02f + 0.07f * order, land = d + 0.26f;
			float flipAt = reveal + 0.07f * k;
			float lit = lockAt + 0.035f + 0.05f * k;
			Vector2 slot = slots[k], dst = stack + new Vector2((k - 2f) * f * h * 0.022f, -(k - 2f) * h * 0.012f);
			float tilt = tilts[k];
			float side = slot.X < center.X ? -1f : 1f;
			Vector2 ctl = (c.Hand + slot) * 0.5f + new Vector2(side * h * 0.3f, -h * 0.1f);
			Phantom.P5Card card = Phantom.Card(front, c.Hand, w, send + 0.1, 0.55f);
			card.Root.Visible = false;
			bool trailOff = false;
			Fx.Animate(card.Root, d, send - d, t =>
			{
				float s = d + t * (send - d);
				card.Root.Visible = true;
				Vector2 pos;
				float rot, flip, sc = 1f, glowK = 0f;
				if (s < gather)
				{
					float e = Ease5.OutQuint(Mathf.Min(1f, (s - d) / (land - d)));
					pos = c.Hand.Lerp(ctl, e).Lerp(ctl.Lerp(slot, e), e);
					float settle = Mathf.Max(0f, s - land);
					pos += new Vector2(0f, Mathf.Sin(settle * 10f) * Mathf.Exp(-settle * 9f) * h * 0.02f + Mathf.Sin(s * 2.4f + k) * h * 0.006f);
					rot = Mathf.Lerp(-side * 1.6f, tilt, e);
					// 飞的时候转着、落位露牌背；到点翻面
					float fe = Ease5.OutQuint(Mathf.Clamp((s - flipAt) / 0.18f, 0f, 1f));
					flip = Mathf.Pi + (1f - e) * 8f * side + fe * Mathf.Pi;
					sc = 0.55f + 0.45f * e;
					glowK = 0.5f * Mathf.Exp(-Mathf.Max(0f, s - land) * 12f) + (s > lit ? 0.7f * Mathf.Exp(-(s - lit) * 7f) : 0f);
					if (s > land + 0.03f && !trailOff) { trailOff = true; card.Trail?.Kill(); }
				}
				else
				{
					float e = Mathf.SmoothStep(0f, 1f, Mathf.Min(1f, (s - gather) / 0.18f));
					pos = slot.Lerp(dst, e);
					rot = Mathf.Lerp(tilt, f * (0.12f + (k - 2f) * 0.07f), e);
					flip = Mathf.Tau;
					glowK = 0.12f * e + (s > seal ? 0.7f * Mathf.Exp(-(s - seal) * 12f) : 0f);
				}
				card.Place(pos, rot, flip, sc);
				card.Set("glow", glowK);
				card.Set("sheen", s > lit ? Mathf.Min(1f, (s - lit) * 4f) * 3f - 1f : -1f);
			});
			Fx.After(front, land, () =>
			{
				Glow.Flash(front, slot, new Color(1f, 0.93f, 0.92f), w * 1.5f, 0.11f);
				Glow.Flash(back, slot, new Color(0.9f, 0.08f, 0.15f), w * 2.8f, 0.3f);
				Glow.Sparks(front, slot, 8, 60f * u, 240f * u, 0.4f, new[] { Phantom.Hot, Phantom.Red }, 40f * u, 0.01f * u, 0.028f * u, (slot - c.Chest).Normalized(), 60f);
			});
			// 翻到侧面那一下：一道细高光
			Fx.After(front, flipAt + 0.05f, () => Glow.Glint(front, slot + new Vector2(0f, -w * 0.3f), 0.6f));
			// 定下来：角上一点闪光
			Fx.After(front, lit, () =>
			{
				Glow.Glint(front, slot + new Vector2(w * 0.4f, -w * 0.62f).Rotated(tilt), 0.9f);
				Glow.Flash(back, slot, new Color(0.95f, 0.1f, 0.16f), w * 2.2f, 0.2f);
			});
		}
		// ③ 疾光沿着牌阵扫过去（红 + 白芯，略高于牌阵、跟着弧）
		Vector2 Arc(float t)
		{
			float a = Mathf.Lerp(-0.9f, 0.9f, t);
			return pivot + new Vector2(Mathf.Sin(a), -Mathf.Cos(a)) * rad * 1.08f;
		}
		DashFly(front, Arc, lockAt, 0.3f, h * 0.9f, h * 0.16f, new Color(0.95f, 0.06f, 0.15f), 1.3f, Ease5.OutQuint, 0.7f);
		DashFly(front, Arc, lockAt + 0.01f, 0.3f, h * 0.7f, h * 0.055f, new Color(1f, 0.9f, 0.9f), 1.8f, Ease5.OutQuint, 0.7f);
		Fx.After(front, lockAt + 0.02f, () =>
			Glow.Sparks(front, center, 30, 80f * u, 320f * u, 0.8f, new[] { Phantom.Hot, new Color(1f, 0.25f, 0.32f) }, 120f * u, 0.01f * u, 0.032f * u, Vector2.Up, 110f));
		// ④ 光线往那一摞收
		for (int i = 0; i < 18; i++)
		{
			float ang = Mathf.Tau * i / 18f + Fx.Rand(-0.12f, 0.12f);
			Vector2 dir = new(Mathf.Cos(ang), Mathf.Sin(ang) * 0.85f);
			float r0 = h * Fx.Rand(0.45f, 0.7f), r1 = h * Fx.Rand(0.05f, 0.1f);
			float dur = Fx.Rand(0.15f, 0.2f), delay = seal - dur - Fx.Rand(0f, 0.08f);
			Node2D lr = Fx.Root(front, stack, "P5PlanIn", delay + dur + 0.1);
			Polygon2D ln = Glow.TaperPoly(lr, i % 3 == 0 ? Pal5.Hot : new Color(1f, 0.16f, 0.24f));
			float lw = Fx.Rand(2f, 4f) * u;
			ln.Visible = false;
			Fx.Animate(ln, delay, dur, t =>
			{
				ln.Visible = true;
				float hd = Fx.InCubic(Mathf.Min(1f, t * 1.3f)), tl = Fx.InCubic(t);
				ln.Polygon = Glow.TaperPts(dir * Mathf.Lerp(r0 * 1.25f, r1, tl), dir * Mathf.Lerp(r0, r1, hd), lw);
				ln.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 4f) * (1f - Ease5.Seg(t, 0.8f, 0.2f)));
			});
		}
		// 封印
		Fx.After(front, seal, () =>
		{
			HitStop.Request(0.04);
			Fx.Shake(1);
			Glow.Flash(front, stack, new Color(1f, 0.95f, 0.92f), h * 0.5f, 0.13f);
			Glow.Flash(back, stack, new Color(0.9f, 0.08f, 0.15f), h * 1.8f, 0.36f);
			Cross(front, stack, h * 0.95f);
			Phantom.Confetti(front, stack, 18, 400f * u, Vector2.Up, 160f, 0.9f, 7f * u);
			Glow.Sparks(front, stack, 22, 120f * u, 480f * u, 0.5f, new[] { Phantom.Hot, new Color(1f, 0.22f, 0.3f) }, -40f * u, 0.012f * u, 0.036f * u, Vector2.Up, 180f);
		});
		// ⑤ 飞进手牌区
		for (int j = 0; j < n; j++)
			SkillRedo.ToHand(c, stack + new Vector2((j - 2f) * f * h * 0.022f, -(j - 2f) * h * 0.012f), send + 0.06f * j, 0.34f, f * (0.12f + (j - 2f) * 0.07f), h * 0.12f, 0f, 6.3f);
		Glitter(front, c.Feet, h, u, 0.2f, 1.1f, 6, 0.45f);
	}
}
