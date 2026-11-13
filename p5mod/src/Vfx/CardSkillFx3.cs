using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 每张牌的技能特效，第三批（用户 2026-11-10）：过牌 / 弃牌 / 能量——杂技、早有准备、独门技术、本能反应、后空翻、逃脱计划、
/// 生存者、计算下注、战术大师、肾上腺素。牌用 SkillFx.NewCard（发光的红牌），每张牌的轨迹自己编排。音效：tools/p5/sfx/synth_card_fx3.py，
/// 抽牌声统一用 P3 mod 的 card_draw（用户指定）。
/// </summary>
public static class CardSkillFx3
{
	public static void Register(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["Acrobatics"] = Acrobatics;
		cards["Prepared"] = Prepared;
		cards["Expertise"] = Expertise;
		cards["Reflex"] = Reflex;
		cards["Backflip"] = Backflip;
		cards["EscapePlan"] = EscapePlan;
		cards["Survivor"] = Survivor;
		cards["CalculatedGamble"] = CalculatedGamble;
		cards["Tactician"] = Tactician;
		cards["Adrenaline"] = Adrenaline;
	}

	private static readonly Color Hot = new(1f, 0.96f, 0.94f), Red = new(1f, 0.2f, 0.27f), Crimson = new(0.85f, 0.04f, 0.13f),
		Deep = new(0.3f, 0f, 0.07f), Ice = new(0.94f, 0.98f, 1f), Sky = new(0.4f, 0.8f, 1f), Gold = new(1f, 0.85f, 0.55f), Smoke = new(0.25f, 0.22f, 0.24f);

	private static bool Ok(Control? c) => c != null && GodotObject.IsInstanceValid(c);
	private static float CardSize(SkillCtx c) => 42f * c.Unit;

	private static void DrawSound(int k, float delay = 0f) => Sfx.Play(Sfx.CardDraw, 0.3f - 0.03f * Mathf.Min(k, 4), delay + 0.06f * k);

	/// <summary>一张牌沿着 pos(t) 飞，tilt / flip 由调用方给；最后落进手牌区时一点闪光。返回牌。</summary>
	private static SkillFx.LightCard Fly(Control layer, Vector2 start, float size, float delay, float life, Func<float, Vector2> pos, Func<float, float> tilt,
		Func<float, float> flip, Func<float, float>? scale = null, Func<float, float>? burn = null)
	{
		SkillFx.LightCard card = SkillFx.NewCard(layer, start, size, delay + life + 0.1);
		card.Root.Visible = false;
		Fx.Animate(card.Root, delay, life, t =>
		{
			card.Root.Visible = true;
			card.Place(pos(t), tilt(t), flip(t), scale?.Invoke(t) ?? 1f);
			if (burn != null) card.Burn(burn(t));
		});
		return card;
	}

	/// <summary>从 a 飞进手牌区：先快后慢地往下、带一点弧，越飞越小。</summary>
	private static Func<float, Vector2> ToHand(Control layer, Vector2 a, float arc)
	{
		Vector2 b = SkillFx.HandArea(layer), ctrl = a.Lerp(b, 0.4f) + new Vector2(0f, -arc);
		return t =>
		{
			float k = Fx.InCubic(t) * 0.6f + t * 0.4f;
			return a.Lerp(ctrl, k).Lerp(ctrl.Lerp(b, k), k);
		};
	}

	// ═══════════════════════════ 杂技：头顶一圈飞轮 ═══════════════════════════

	/// <summary>
	/// 杂技（抽 3 弃 1）：三张牌从手里甩上去，在他头顶连成一个竖着转的飞轮（一道红色弧光跟着转一圈），转完两张甩进手牌区、
	/// 最后一张在半空被烧掉。1.1s。
	/// </summary>
	public static void Acrobatics(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, size = CardSize(c);
		Vector2 hub = c.Chest + new Vector2(c.Face * h * 0.1f, -h * 0.45f);
		float R = h * 0.42f;
		const float spinT = 0.55f;
		Paint.Sweep(c.Front, "slash_thin", hub, -1.2f, new Vector2(0.55f, 0.55f) * u * h / 300f, Lut5.Blood, 0.06f, 0.03f, 0.25f, 30f, 0.02f, 1.1f);
		Fx.After(c.Front, 0.18f, () => Paint.Sweep(c.Front, "slash_thin", hub, 1.9f, new Vector2(0.55f, 0.55f) * u * h / 300f, Lut5.Blood, 0.06f, 0.03f, 0.25f, 30f, 0.02f, 1.1f));
		for (int i = 0; i < 3; i++)
		{
			int k = i;
			float ph = -Mathf.Pi / 2f + Mathf.Tau * k / 3f;
			DrawSound(k);
			bool burnIt = k == 2;
			Vector2 exitAt = hub + new Vector2(Mathf.Cos(ph + Mathf.Tau * 1.2f) * R, Mathf.Sin(ph + Mathf.Tau * 1.2f) * R);
			Func<float, Vector2> home = burnIt ? (t => exitAt + new Vector2(c.Face * h * 0.4f * t, -h * 0.15f * t)) : ToHand(c.Front, exitAt, h * 0.2f);
			float total = spinT + 0.4f;
			Fly(c.Front, c.Hand, size, 0.03f * k, total, t =>
			{
				float s = t * total;
				if (s < 0.12f) return c.Hand.Lerp(hub + new Vector2(Mathf.Cos(ph) * R, Mathf.Sin(ph) * R), Ease5.OutQuint(s / 0.12f));
				if (s < spinT)
				{
					float a = ph + Mathf.Tau * 1.2f * Ease5.OutQuint((s - 0.12f) / (spinT - 0.12f));
					return hub + new Vector2(Mathf.Cos(a) * R, Mathf.Sin(a) * R);
				}
				return home((s - spinT) / 0.4f);
			}, t => t * 9f, t => t * 14f, t => t < 0.85f ? 1f : 1f - (t - 0.85f) * 3f,
			burnIt ? t => Mathf.Clamp((t * total - spinT) / 0.3f, 0f, 1f) : null);
			if (burnIt)
			{
				Fx.After(c.Front, 0.06f + spinT + 0.1f, () =>
				{
					Sfx.Play(Sfx.CardBurn, 0.3f);
					Glow.Sparks(c.Front, exitAt, 10, 40f, 180f, 0.5f, new[] { Hot, Red, Deep }, -120f, 0.03f * u, 0.07f * u, Vector2.Up, 90f);
				});
			}
		}
		Fx.After(c.Front, 0.15f, () => Glow.Flash(c.Front, hub, Red, h * 0.9f, 0.25f));
	}

	// ═══════════════════════════ 早有准备：一换一 ═══════════════════════════

	/// <summary>
	/// 早有准备（抽 N 弃 N）：每一对牌交错着来——新牌从手牌区那边飞上来、旧牌从他手里甩出去，两张在他身前错身而过的一刹那「啪」一闪，
	/// 新牌飞回手牌、旧牌烧掉。0.9s。
	/// </summary>
	public static void Prepared(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, size = CardSize(c);
		int n = 2;
		Vector2 meet = c.Hand + new Vector2(c.Face * h * 0.35f, -h * 0.2f);
		for (int i = 0; i < n; i++)
		{
			float d = 0.14f * i;
			Vector2 m = meet + new Vector2(0f, -h * 0.16f * i);
			DrawSound(i);
			// 旧牌：手 → 交汇点 → 甩远、烧掉
			Vector2 far = m + new Vector2(c.Face * h * 0.55f, -h * 0.25f);
			Fly(c.Front, c.Hand, size, d, 0.55f, t => t < 0.35f ? c.Hand.Lerp(m, Ease5.OutQuint(t / 0.35f)) : m.Lerp(far, Ease5.OutQuint((t - 0.35f) / 0.65f)),
				t => c.Face * t * 4f, t => t * 6f, null, t => Mathf.Clamp((t - 0.4f) / 0.5f, 0f, 1f));
			// 新牌：手牌区 → 交汇点 → 回手牌区
			Vector2 hand = SkillFx.HandArea(c.Front);
			Func<float, Vector2> back = ToHand(c.Front, m, h * 0.3f);
			Fly(c.Front, hand, size, d, 0.75f, t => t < 0.26f ? hand.Lerp(m, Ease5.OutQuint(t / 0.26f)) : back((t - 0.26f) / 0.74f),
				t => -c.Face * t * 3f, t => Mathf.Pi + t * 7f, t => t > 0.8f ? 1f - (t - 0.8f) * 2.5f : 1f);
			Fx.After(c.Front, d + 0.19f, () =>
			{
				Glow.Flash(c.Front, m, Hot, h * 0.4f, 0.12f);
				Glow.Glint(c.Front, m, 0.8f);
				Glow.Sparks(c.Front, m, 8, 100f, 320f, 0.3f, new[] { Hot, Red }, 200f, 0.02f * u, 0.05f * u, Vector2.Up, 180f);
			});
		}
		Fx.After(c.Front, 0.5f, () => Sfx.Play(Sfx.CardBurn, 0.28f));
	}

	// ═══════════════════════════ 独门技术：一字排开、上锁 ═══════════════════════════

	/// <summary>
	/// 独门技术（抽牌，本回合保留）：抽到的牌在他身前一字排成弧形悬停，每张牌依次被一道金白的光扫过、四角扣上一圈细光环（保留），
	/// 然后一齐收进手牌区。1.2s。
	/// </summary>
	public static void Expertise(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, size = CardSize(c);
		int n = 4;
		const float outT = 0.22f, hold = 0.5f, inT = 0.32f, total = outT + hold + inT;
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float f = (k - (n - 1) / 2f) / ((n - 1) / 2f);
			Vector2 slot = c.Chest + new Vector2(c.Face * (h * 0.5f + f * h * 0.32f), -h * 0.38f - (1f - f * f) * h * 0.08f);
			DrawSound(k);
			Func<float, Vector2> home = ToHand(c.Front, slot, h * 0.15f);
			SkillFx.LightCard card = Fly(c.Front, c.Hand, size, 0.04f * k, total, t =>
			{
				float s = t * total;
				if (s < outT) return c.Hand.Lerp(slot, Ease5.OutQuint(s / outT));
				if (s < outT + hold) return slot + new Vector2(0f, Mathf.Sin(s * 6f + k) * h * 0.01f);
				return home((s - outT - hold) / inT);
			}, t => f * 0.18f, t => Mathf.Max(0f, (1f - t * total / outT)) * Mathf.Tau, t => t * total > outT + hold + inT * 0.7f ? 1f - (t * total - outT - hold - inT * 0.7f) * 2.5f : 1f);
			float sweepAt = 0.04f * k + outT + 0.06f + 0.08f * k;
			Fx.Animate(card.Root, sweepAt, 0.2, t => card.Sheen(t));
			Fx.After(c.Front, sweepAt + 0.12f, () =>
			{
				Node2D rr = Fx.Root(c.Front, slot, "P5Retain", 0.5);
				ColorRect ring = Glow.RingRect(rr, size * 3.2f, 1.25f);
				Glow.SetParam(ring, "tint", Gold);
				Fx.Animate(ring, 0, 0.4, t =>
				{
					Glow.SetParam(ring, "radius", Mathf.Lerp(0.95f, 0.55f, Ease5.OutBack(Mathf.Min(1f, t * 2.5f))));
					Glow.SetParam(ring, "width", 0.025f);
					Glow.SetParam(ring, "alpha", 1f - Ease5.Seg(t, 0.5f, 0.5f));
				});
				Glow.Glint(c.Front, slot + new Vector2(size * 0.5f, -size * 0.7f), 0.55f);
				if (k == 0) Sfx.Play(Sfx.RetainLock, 0.3f);
			});
		}
	}

	// ═══════════════════════════ 本能反应：快 ═══════════════════════════

	/// <summary>
	/// 本能反应（抽牌）：快——手边一圈速度线一炸，牌几乎是「射」出去的：一张紧跟一张，拖着长长的光尾直线飞进手牌区，沿途掠起风线。0.5s。
	/// </summary>
	public static void Reflex(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, size = CardSize(c);
		int n = 3;
		Glow.SpeedLines(c.Front, c.Hand, Hot, 12, h * 0.06f, h * 0.35f, 0.22f);
		Glow.Flash(c.Front, c.Hand, Red, h * 0.4f, 0.12f);
		Vector2 hand = SkillFx.HandArea(c.Front);
		for (int i = 0; i < n; i++)
		{
			int k = i;
			DrawSound(k);
			Vector2 up = c.Hand + new Vector2(c.Face * h * (0.25f + 0.08f * k), -h * (0.28f - 0.06f * k));
			Fly(c.Front, c.Hand, size, 0.05f * k, 0.36f, t => t < 0.25f ? c.Hand.Lerp(up, Ease5.OutQuint(t / 0.25f)) : up.Lerp(hand, Fx.InCubic((t - 0.25f) / 0.75f)),
				t => c.Face * 0.4f, t => t * 10f, t => 1f - Ease5.Seg(t, 0.8f, 0.2f));
			Fx.After(c.Front, 0.05f * k + 0.09f, () => Glow.Glint(c.Front, up, 0.5f));
		}
	}

	// ═══════════════════════════ 后空翻：身后一道竖着的弧光 ═══════════════════════════

	/// <summary>
	/// 后空翻（格挡 + 抽 2）：身后甩出一道竖着翻过去的蓝白弧光（像他整个人翻了一圈留下的轨迹），落地一圈蓝波、扬起一点尘，
	/// 两张牌从弧顶甩出来飞进手牌区。0.9s。
	/// </summary>
	public static void Backflip(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, size = CardSize(c);
		Control back = Ok(c.Back) ? c.Back! : c.Front;
		Vector2 hub = c.Feet + new Vector2(-c.Face * h * 0.15f, -h * 0.6f);
		Paint.Sweep(back, "slash_thin", hub, c.Face > 0f ? Mathf.Pi * 0.5f : Mathf.Pi * 0.5f, new Vector2(0.7f, 0.7f) * u * h / 300f, Lut5.Persona, 0.05f, 0.04f, 0.3f, -40f * c.Face, 0.02f, 1.2f);
		Glow.Flash(back, hub, Sky, h * 1.1f, 0.25f);
		Fx.After(c.Front, 0.16f, () =>
		{
			Glow.Shockwave(c.Front, c.Feet, Sky, h * 1.5f, 0.4f, 0.22f);
			Glow.Sparks(c.Front, c.Feet, 14, 80f, 260f, 0.5f, new[] { Ice, Sky, Smoke }, 500f, 0.02f * u, 0.06f * u, Vector2.Up, 70f);
		});
		Vector2 top = hub + new Vector2(0f, -h * 0.55f);
		for (int i = 0; i < 2; i++)
		{
			int k = i;
			DrawSound(k, 0.12f);
			Func<float, Vector2> home = ToHand(c.Front, top, h * 0.25f);
			Fly(c.Front, top, size, 0.12f + 0.07f * k, 0.6f, t => t < 0.2f ? top + new Vector2(c.Face * h * 0.15f * (k + 1) * Ease5.OutQuint(t / 0.2f), -h * 0.1f * Ease5.OutQuint(t / 0.2f)) : home((t - 0.2f) / 0.8f),
				t => t * 6f, t => t * 9f, t => 1f - Ease5.Seg(t, 0.82f, 0.18f));
		}
	}

	// ═══════════════════════════ 逃脱计划：一张牌 + 一团烟 ═══════════════════════════

	/// <summary>
	/// 逃脱计划（抽 1，抽到技能就加格挡）：一张牌从指间弹起、在头顶翻着停住一瞬——「噗」地一团烟弹在脚下炸开（灰黑的烟粒往四周涌），
	/// 烟里掠出几道蓝色的风线，牌翻回来飞进手牌区。0.9s。
	/// </summary>
	public static void EscapePlan(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, size = CardSize(c);
		DrawSound(0);
		Vector2 top = c.Hand + new Vector2(c.Face * h * 0.1f, -h * 0.55f);
		Func<float, Vector2> home = ToHand(c.Front, top, h * 0.2f);
		Fly(c.Front, c.Hand, size, 0f, 0.85f, t => t < 0.25f ? c.Hand.Lerp(top, Ease5.OutQuint(t / 0.25f)) : t < 0.5f ? top : home((t - 0.5f) / 0.5f),
			t => 0f, t => t < 0.5f ? t * 16f : Mathf.Tau * 4f, t => 1f - Ease5.Seg(t, 0.85f, 0.15f));
		Fx.After(c.Front, 0.25f, () => Glow.Glint(c.Front, top, 0.7f));
		Fx.After(c.Front, 0.3f, () =>
		{
			Sfx.Play(Sfx.SmokePuff, 0.4f);
			Glow.Flash(c.Front, c.Feet + new Vector2(0f, -h * 0.1f), new Color(0.7f, 0.68f, 0.7f), h * 0.6f, 0.15f);
			Glow.Sparks(c.Front, c.Feet + new Vector2(0f, -h * 0.1f), 40, 80f, 380f, 0.7f, new[] { new Color(0.55f, 0.52f, 0.55f), Smoke, new Color(0.08f, 0.06f, 0.08f) },
				-60f, 0.12f * u, 0.32f * u, Vector2.Up, 180f);
			Glow.Shockwave(c.Front, c.Feet, new Color(0.7f, 0.7f, 0.75f), h * 1.4f, 0.35f, 0.25f);
			for (int i = 0; i < 4; i++)
			{
				float side = i % 2 == 0 ? -1f : 1f;
				Line(c.Front, c.Feet + new Vector2(side * h * 0.1f, -h * Fx.Rand(0.1f, 0.6f)), new Vector2(side, -0.15f).Normalized(), h * Fx.Rand(0.4f, 0.7f), 0.28f, Ice, Sky, 2.5f * u, 0.05f * i);
			}
		});
	}

	private static void Line(Control layer, Vector2 at, Vector2 dir, float len, float life, Color core, Color glow, float width, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5Streak", delay + life + 0.1);
		Line2D g = Glow.Line(root, new[] { Vector2.Zero, dir }, new Color(glow, 0.8f), width * 3f, false);
		Line2D k = Glow.Line(root, new[] { Vector2.Zero, dir }, core, width, false);
		root.Visible = false;
		Fx.Animate(root, delay, life, t =>
		{
			root.Visible = true;
			Vector2 a = dir * len * Ease5.OutQuint(t), b = dir * len * Fx.InCubic(t);
			if (a.DistanceTo(b) < 1f) b = a - dir;
			g.Points = k.Points = new[] { a, b };
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.6f, 0.4f));
		});
	}

	// ═══════════════════════════ 生存者：一刀两断 ═══════════════════════════

	/// <summary>
	/// 生存者（格挡 + 弃 1）：一张牌被甩到身前半空，一道白刃「唰」地把它斩成两半（斩线 + 白闪 + 短顿帧），两半往两边翻开、从切口烧掉。0.8s。
	/// </summary>
	public static void Survivor(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, size = CardSize(c);
		Vector2 at = c.Hand + new Vector2(c.Face * h * 0.42f, -h * 0.28f);
		const float cutAt = 0.22f;
		Fly(c.Front, c.Hand, size, 0f, cutAt + 0.02f, t => c.Hand.Lerp(at, Ease5.OutQuint(t)), t => c.Face * t * 3f, t => t * 6f, t => 1.2f);
		Fx.After(c.Front, cutAt, () =>
		{
			Sfx.Play(Sfx.CardCut, 0.4f);
			float ang = c.Face > 0f ? -0.6f : Mathf.Pi + 0.6f;
			Cut.Razor(c.Front, at, ang, h * 0.7f, 4f * u, 0.12f);
			Glow.Flash(c.Front, at, Hot, h * 0.4f, 0.12f);
			HitStop.Request(0.035);
			Vector2 n = Vector2.Right.Rotated(ang + Mathf.Pi / 2f);
			for (int i = 0; i < 2; i++)
			{
				float sgn = i == 0 ? 1f : -1f;
				Vector2 away = at + n * sgn * h * 0.22f + new Vector2(0f, h * 0.15f);
				Fly(c.Front, at, size, 0f, 0.5f, t => at.Lerp(away, Ease5.OutQuint(t)) + new Vector2(0f, h * 0.1f * t * t), t => c.Face * 3f + sgn * t * 2.5f, t => 0.6f + t,
					t => 1.2f * (1f - 0.3f * t), t => Mathf.Clamp(t * 1.4f, 0f, 1f));
			}
			Glow.Sparks(c.Front, at, 12, 60f, 220f, 0.5f, new[] { Hot, Red, Deep }, -100f, 0.02f * u, 0.06f * u, Vector2.Up, 120f);
		});
	}

	// ═══════════════════════════ 计算下注：轮盘 ═══════════════════════════

	private static Shader? _wheel;

	/// <summary>
	/// 轮盘（贴地，正方形四边形压扁）：外圈细环 + 一圈交替的红 / 暗格子 + 内圈刻度 + 中心一点，spin 转；alpha 淡出。加法。
	/// </summary>
	private static Shader Wheel => _wheel ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float spin = 0.0;
uniform float alpha = 1.0;
uniform float flare = 0.0;
float ring(float r, float R, float w) { return exp(-pow((r - R) / w, 2.0)); }
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), a = atan(p.y, p.x) + spin;
	float v = ring(r, 0.95, 0.012) + ring(r, 0.72, 0.01) * 0.8 + ring(r, 0.3, 0.01) * 0.7;
	float cell = step(0.72, r) * step(r, 0.95) * step(0.0, sin(a * 18.0));
	float tick = step(0.3, r) * step(r, 0.7) * pow(abs(cos(a * 9.0)), 80.0);
	vec3 col = vec3(1.0, 0.2, 0.27) * (v + cell * 0.35 + tick * 0.6) + vec3(1.0, 0.95, 0.92) * (v * 0.4 + flare * exp(-r * r * 8.0));
	COLOR = vec4(col * alpha * (1.0 - smoothstep(0.96, 1.0, r)), 1.0);
}" };

	/// <summary>
	/// 计算下注（弃掉全部手牌，再抽同样多）：脚下亮起一个转动的红色轮盘，五张牌绕着他横着飞转（转到身后被挡住），越转越慢——
	/// 停住的一刻全部「轰」地烧掉（一圈火星 + 白闪），紧接着同样多的新牌从胸口炸开、飞进手牌区。1.5s。
	/// </summary>
	public static void CalculatedGamble(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, size = CardSize(c);
		const int n = 5;
		const float spinT = 0.7f;
		Sfx.Play(Sfx.GambleSpin, 0.4f);
		Control back = Ok(c.Back) ? c.Back! : c.Front;
		Node2D wr = Fx.Root(back, c.Feet, "P5Wheel", 1.3);
		ColorRect wq = new() { Size = Vector2.One * h * 1.8f, Position = -Vector2.One * h * 0.9f, PivotOffset = Vector2.One * h * 0.9f, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Wheel } };
		wr.AddChild(wq);
		wq.Scale = new Vector2(1f, 0.3f);
		Fx.Animate(wq, 0, 1.2, t =>
		{
			float s = t * 1.2f;
			var m = (ShaderMaterial)wq.Material;
			m.SetShaderParameter("spin", 9f * (1f - Mathf.Pow(1f - Mathf.Min(1f, s / spinT), 3f)));
			m.SetShaderParameter("alpha", Mathf.Min(1f, s / 0.1f) * (1f - Mathf.Clamp((s - 0.9f) / 0.3f, 0f, 1f)));
			m.SetShaderParameter("flare", s > spinT ? Mathf.Exp(-(s - spinT) * 8f) * 2f : 0f);
		});
		Vector2 center = c.Feet + new Vector2(0f, -h * 0.5f);
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float ph = Mathf.Tau * k / n;
			SkillFx.LightCard card = SkillFx.NewCard(c.Front, center, size, spinT + 0.2);
			SkillFx.LightCard cardB = SkillFx.NewCard(back, center, size * 0.9f, spinT + 0.2);
			Fx.Animate(card.Root, 0, spinT, t =>
			{
				float a = ph + Mathf.Tau * 2.2f * (1f - Mathf.Pow(1f - t, 3f));
				float rad = h * 0.55f * Ease5.OutQuint(Mathf.Min(1f, t * 5f));
				Vector2 p = center + new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad * 0.25f);
				bool front = Mathf.Sin(a) > 0f;
				card.Root.Visible = front; cardB.Root.Visible = !front;
				card.Place(p, -Mathf.Cos(a) * 0.3f, a + Mathf.Pi / 2f, 0.9f + 0.1f * Mathf.Sin(a));
				cardB.Place(p, -Mathf.Cos(a) * 0.3f, a + Mathf.Pi / 2f, 0.8f);
				float burn = Mathf.Clamp((t - 0.96f) / 0.04f, 0f, 1f);
				card.Burn(burn); cardB.Burn(burn);
			});
			Fx.Animate(card.Root, spinT, 0.18, t => { card.Burn(t); cardB.Burn(t); });
		}
		Fx.After(c.Front, spinT, () =>
		{
			Sfx.Play(Sfx.CardBurn, 0.32f);
			Glow.Flash(c.Front, center, Hot, h * 0.9f, 0.18f);
			Glow.Shockwave(c.Front, center, Red, h * 1.8f, 0.4f, 0.3f);
			Glow.Sparks(c.Front, center, 36, 120f, 420f, 0.6f, new[] { Hot, Red, Deep }, -80f, 0.03f * u, 0.07f * u, Vector2.Up, 180f);
			HitStop.Request(0.04);
		});
		Fx.After(c.Front, spinT + 0.12f, () =>
		{
			for (int i = 0; i < n; i++)
			{
				int k = i;
				DrawSound(k);
				float ang = -Mathf.Pi / 2f + (k - (n - 1) / 2f) * 0.45f;
				Vector2 apex = c.Chest + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * h * 0.5f;
				Func<float, Vector2> home = ToHand(c.Front, apex, h * 0.1f);
				Fly(c.Front, c.Chest, size, 0.04f * k, 0.6f, t => t < 0.25f ? c.Chest.Lerp(apex, Ease5.OutQuint(t / 0.25f)) : home((t - 0.25f) / 0.75f),
					t => (k - 2) * 0.2f, t => t * 8f, t => 1f - Ease5.Seg(t, 0.82f, 0.18f));
			}
		});
	}

	// ═══════════════════════════ 战术大师：能量核心 ═══════════════════════════

	/// <summary>
	/// 战术大师（获得能量）：四周的光丝吸进胸口（沿用能量的吸入），吸满后胸口凝出一颗红白的能量核心——一圈圈细环往里收、四角星闪，
	/// 核心「嗡」地一胀、炸成一圈光环和往上蹿的火星，一道光柱短促地冲上去。1s。
	/// </summary>
	public static void Tactician(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit;
		SkillFx.Surge(c.Front, c.Chest, h, SkillFx.Tone.Red, sound: true);
		Node2D core = Fx.Root(c.Front, c.Chest, "P5Core", 0.9);
		Sprite2D glow = Glow.SoftSprite(core, Vector2.Zero, new Color(1f, 0.25f, 0.3f, 0.9f), 0.1f);
		Sprite2D hot = Glow.SoftSprite(core, Vector2.Zero, Hot, 0.05f);
		Fx.Animate(core, 0.2f, 0.45, t =>
		{
			float k = Ease5.OutBack(Mathf.Min(1f, t * 2.2f));
			float pulse = 1f + 0.12f * Mathf.Sin(t * 40f);
			glow.Scale = Vector2.One * h * 0.009f * k * pulse;
			hot.Scale = Vector2.One * h * 0.0035f * k * pulse;
			core.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.85f, 0.15f));
		});
		for (int i = 0; i < 3; i++)
		{
			Node2D rr = Fx.Root(c.Front, c.Chest, "P5CoreRing", 0.6);
			ColorRect ring = Glow.RingRect(rr, h * 0.9f, 1f);
			Glow.SetParam(ring, "tint", i == 1 ? Hot : Red);
			Glow.SetParam(ring, "alpha", 0f);
			Fx.Animate(ring, 0.22f + 0.08f * i, 0.25, t =>
			{
				Glow.SetParam(ring, "radius", Mathf.Lerp(0.9f, 0.1f, Fx.InCubic(t)));
				Glow.SetParam(ring, "width", 0.012f);
				Glow.SetParam(ring, "alpha", Mathf.Min(1f, t * 4f) * (1f - Ease5.Seg(t, 0.8f, 0.2f)));
			});
		}
		Fx.After(c.Front, 0.62f, () =>
		{
			Glow.Glint(c.Front, c.Chest, 1.1f);
			Glow.Flash(c.Front, c.Chest, Hot, h * 0.6f, 0.15f);
			Glow.Shockwave(c.Front, c.Chest, Red, h * 1.8f, 0.45f, 0.55f);
			SkillFx.ColumnFx(c.Front, c.Back, c.Feet, h * 0.9f, SkillFx.Tone.Red, 0.45f);
			Glow.Sparks(c.Front, c.Chest, 20, 200f, 600f, 0.45f, new[] { Hot, Red }, -200f, 0.02f * u, 0.06f * u, Vector2.Up, 50f);
		});
	}

	// ═══════════════════════════ 肾上腺素：心跳 ═══════════════════════════

	/// <summary>
	/// 肾上腺素（获得能量，抽 2）：「咚、咚」两下心跳——胸口两下红闪、每下推出一圈扁扁的脉冲波，第二下更重（短震）；
	/// 心跳间一道道红色的「血管」光丝从胸口往四肢窜出去（折线闪电那样分叉），身上红光一泛；然后两张牌从胸口甩出去飞进手牌区。1.1s。
	/// </summary>
	public static void Adrenaline(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, size = CardSize(c);
		Sfx.Play(Sfx.Heartbeat, 0.5f);
		for (int b = 0; b < 2; b++)
		{
			float d = 0.24f * b;
			int bb = b;
			Fx.After(c.Front, d, () =>
			{
				Glow.Flash(c.Front, c.Chest, bb == 0 ? Red : Hot, h * (0.6f + 0.25f * bb), 0.16f);
				Glow.Shockwave(c.Front, c.Chest, Red, h * (1.3f + 0.6f * bb), 0.38f, 0.8f);
				if (Ok(c.Back)) Glow.Flash(c.Back!, c.Chest, Crimson, h * (1.4f + 0.5f * bb), 0.3f);
				if (bb == 1) Fx.Shake(1);
				for (int i = 0; i < 6; i++)
				{
					float ang = Mathf.Tau * i / 6f + Fx.Rand(-0.3f, 0.3f);
					Vein(c.Front, c.Chest, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 1.3f).Normalized(), h * Fx.Rand(0.35f, 0.55f), u);
				}
			});
		}
		if (c.SelfTint is { } tint)
		{
			Node2D tick = Fx.Root(c.Front, c.Feet, "P5AdrTint", 0.9);
			Fx.Animate(tick, 0, 0.8, t =>
			{
				float k = Mathf.Exp(-Mathf.Pow((t * 0.8f - 0.05f) / 0.06f, 2f)) * 0.5f + Mathf.Exp(-Mathf.Pow((t * 0.8f - 0.29f) / 0.07f, 2f)) * 0.8f;
				tint(Colors.White.Lerp(new Color(1.5f, 0.6f, 0.62f), k));
			});
			tick.TreeExiting += () => tint(Colors.White);
		}
		Fx.After(c.Front, 0.45f, () =>
		{
			Sfx.Play(Sfx.EnergyPull, 0.3f);
			for (int i = 0; i < 2; i++)
			{
				int k = i;
				DrawSound(k);
				Vector2 apex = c.Chest + new Vector2(c.Face * h * (0.3f + 0.15f * k), -h * (0.35f - 0.1f * k));
				Func<float, Vector2> home = ToHand(c.Front, apex, h * 0.15f);
				Fly(c.Front, c.Chest, size, 0.06f * k, 0.55f, t => t < 0.25f ? c.Chest.Lerp(apex, Ease5.OutQuint(t / 0.25f)) : home((t - 0.25f) / 0.75f),
					t => c.Face * 0.3f, t => t * 9f, t => 1f - Ease5.Seg(t, 0.82f, 0.18f));
			}
		});
	}

	/// <summary>一道「血管」光丝：从 from 沿 dir 折线窜出去 len（每段随机偏一点），白芯红晕，0.25s 窜完、0.2s 褪掉。</summary>
	private static void Vein(Control layer, Vector2 from, Vector2 dir, float len, float u)
	{
		Node2D root = Fx.Root(layer, from, "P5Vein", 0.5);
		var pts = new List<Vector2> { Vector2.Zero };
		Vector2 p = Vector2.Zero, d = dir;
		const int segs = 6;
		for (int i = 0; i < segs; i++)
		{
			d = d.Rotated(Fx.Rand(-0.5f, 0.5f));
			p += d * len / segs;
			pts.Add(p);
		}
		Line2D glow = Glow.Line(root, new[] { Vector2.Zero, Vector2.Zero + dir }, new Color(1f, 0.15f, 0.22f, 0.7f), 6f * u, false);
		Line2D core = Glow.Line(root, new[] { Vector2.Zero, Vector2.Zero + dir }, new Color(1f, 0.8f, 0.78f), 1.8f * u, false);
		Fx.Animate(root, 0, 0.45, t =>
		{
			float grow = Mathf.Min(1f, t / 0.5f);
			int n = Mathf.Clamp(1 + (int)(grow * segs), 2, pts.Count);
			var seg = pts.GetRange(0, n).ToArray();
			glow.Points = core.Points = seg;
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.55f, 0.45f));
		});
	}
}
