using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 腐蚀波（稀有，毒流重点牌，用户 2026-11-03：关键在「开了之后每次抽牌都会挂毒」）。
/// 创意：亚森把咒怨种进 Joker 脚下，之后每抽一张牌，咒怨就贴着地面朝敌人涌过去。
/// · 打出（Plunge）：稀有召唤（眼神特写 + 亚森）→ 亚森出手时把咒怨砸进 Joker 身前的地面：咒怨光束从爪尖砸到地上，顿帧、震屏，黑红咒火在地上炸开；
/// · 持续（Aura，这回合一直在，回合结束原版移除效果时消失）：Joker 脚下一滩咒怨翻腾，冒暗红烟和火星；
/// · 每抽一张牌（Wave，CorrosiveDrawPatch）：脚下咒怨一亮，一道低矮的黑红咒火浪贴着地面飞快涌向敌人，浪头经过每个敌人时咒火从脚下舔上来、身体暗一下
///   ——这就是这次上毒（这段时间里 PoisonFx.Apply 让给这里，不放普通施毒的大落点）；连抽时一道接一道，间隔 0.12s，很轻、不顿帧。
/// 全是原版火焰 / 烟序列帧（咒怨 LUT）+ P5R 怨灵 + 加法光，不画形状。
/// </summary>
public static class CorrosiveFx
{
	/// <summary>一个敌人：中心、头顶、宽度、本体（染色用）。</summary>
	public record struct Target(Vector2 At, Vector2 Top, float Width, Node2D? Body);

	private static ulong _suppressUntil, _nextWaveAt, _auraNext;

	/// <summary>抽牌带来的上毒正在由咒火浪表现：PoisonFx.Apply 这段时间不放普通落点。</summary>
	public static bool Suppressing => Stage.NowMs() < _suppressUntil;

	/// <summary>打出：咒怨从 claw（亚森爪尖；没有就从空中）砸进 ground（Joker 身前的地面）。</summary>
	public static void Plunge(Control layer, Vector2 claw, Vector2 ground)
	{
		Vector2 d = ground - claw;
		float len = d.Length();
		if (len > 40f)
		{
			float sx = len / 940f;
			Vector2 center = claw + d * 0.5f + d.Normalized() * (512f - 530f) * sx;
			Paint.Sweep(layer, "slash_thrust", center, d.Angle(), new Vector2(sx, 0.5f), Lut5.Curse, 0.07f, 0.04f, 0.25f, 0f, 0f, 1.4f);
		}
		Fx.After(layer, 0.07, () =>
		{
			Sfx.Play(Sfx.Slam, 0.65f);
			Sfx.Play(Sfx.SubBoom, 0.7f);
			Sfx.Play(Sfx.Curse, 0.5f);
			HitStop.Request(0.07);
			Stage.ShakeDir?.Invoke(3, 90f);
			Glow.Flash(layer, ground, new Color(1f, 0.25f, 0.3f), 420f, 0.25f);
			Glow.Ripple(layer, ground, 700f, 0.5f, 0.035f);
			// 地上炸开：中间一团大的、两边往外铺
			Vanilla.Burst(layer, Vanilla.FireCenter, ground, 0f, 1.2f, Lut5.CurseInk);
			Vanilla.Burst(layer, Vanilla.FireLeft, ground + new Vector2(-110f, 4f), 0f, 0.9f, Lut5.CurseInk, delay: 0.04f);
			Vanilla.Burst(layer, Vanilla.FireRight, ground + new Vector2(110f, 4f), 0f, 0.9f, Lut5.CurseInk, delay: 0.06f);
			Vanilla.Burst(layer, Vanilla.BigSlashSmoke, ground, 0f, 1.4f, Lut5.Smoke);
			Vanilla.Burst(layer, Vanilla.Embers, ground, 0f, 1.4f, Lut5.Curse);
			Paint.Rise(layer, "curse_ghost", ground + new Vector2(0f, -40f), 220f, 0.7f, Lut5.Curse, 0.9f, 0.08f);
		});
	}

	/// <summary>持续：每帧调用（腐蚀波生效时）；约每 0.32s 在 Joker 脚下翻起一股暗红烟 / 火星 / 一小簇咒火。</summary>
	public static void Aura(Control layer, Vector2 feet)
	{
		ulong now = Stage.NowMs();
		if (now < _auraNext)
		{
			return;
		}
		_auraNext = now + 320;
		Vector2 at = feet + new Vector2(Fx.Rand(-60f, 60f), Fx.Rand(-6f, 6f));
		Glow.Flash(layer, feet + new Vector2(0f, -20f), new Color(0.7f, 0.03f, 0.1f), 260f, 0.6f);
		Vanilla.Burst(layer, Vanilla.BigSlashSmoke, at, Fx.Rand(-0.4f, 0.4f), 0.7f, Lut5.Smoke);
		if (Fx.Rng.Next(2) == 0)
		{
			Vanilla.Burst(layer, Vanilla.FireCenter, at, 0f, 0.35f, Lut5.CurseInk);
		}
		Vanilla.Burst(layer, Vanilla.Embers, feet, 0f, 0.6f, Lut5.Curse);
	}

	/// <summary>
	/// 抽到一张牌（CorrosiveDrawPatch，在原版给全体上毒之前）：排一道咒火浪，从 feet 贴地涌向 targets。
	/// 连抽时一道接一道（间隔 0.12s）；之后 0.35s 内（加上排队时间）的上毒都由浪表现。
	/// </summary>
	public static void OnDraw(Control layer, Vector2 feet, float facing, IReadOnlyList<Target> targets)
	{
		ulong now = Stage.NowMs();
		ulong start = _nextWaveAt > now ? _nextWaveAt : now;
		_nextWaveAt = start + 120;
		_suppressUntil = System.Math.Max(_suppressUntil, start + 350);
		bool first = start == now;
		Fx.After(layer, (start - now) / 1000.0, () => Wave(layer, feet, facing, targets, first));
	}

	private const float Speed = 2600f;    // 浪头每秒走多少像素

	private static void Wave(Control layer, Vector2 feet, float facing, IReadOnlyList<Target> targets, bool first)
	{
		if (targets.Count == 0)
		{
			return;
		}
		// 脚下咒怨一亮
		Sfx.Play(Sfx.SwishLow, first ? 0.5f : 0.35f);
		Glow.Flash(layer, feet + new Vector2(0f, -15f), new Color(1f, 0.15f, 0.2f), first ? 320f : 240f, 0.25f);
		Vanilla.Burst(layer, Vanilla.FireCenter, feet + new Vector2(40f * facing, 0f), 0f, first ? 0.6f : 0.45f, Lut5.CurseInk);
		// 浪：从 Joker 身前到最远的敌人后面一点，每 95px 一簇贴地的黑红咒火 + 一点红光，按浪头速度错开
		float far = 0f;
		foreach (Target t in targets)
		{
			far = Mathf.Max(far, (t.At.X - feet.X) * facing);
		}
		far += 80f;
		float y0 = feet.Y;
		float yEnd = y0;
		foreach (Target t in targets)
		{
			yEnd = Mathf.Max(yEnd, t.At.Y + Mathf.Max(80f, (t.At - t.Top).Length()) * 0.75f);
		}
		// 浪头：地面被腐蚀撕开一道裂缝（Codex 的 slash_tear：黑色裂口 + 红色发光锯齿边），跟着浪头的速度从 Joker 身前撕到最远的敌人后面
		{
			Vector2 a = new(feet.X + 50f * facing, y0 + 6f), b = new(feet.X + far * facing, yEnd + 6f);
			Vector2 d = b - a;
			float sx = d.Length() / 940f;
			Paint.Sweep(layer, "slash_tear", a + d * 0.5f, facing > 0f ? d.Angle() : d.Angle() + Mathf.Pi,
				new Vector2(sx * facing, first ? 0.3f : 0.24f), Lut5.Curse, far / Speed, 0.12f, 0.4f, 0f, 0f, 1.35f);
		}
		int i = 0;
		for (float x = 70f; x <= far; x += 72f, i++)
		{
			float u = x / far;
			Vector2 p = new(feet.X + x * facing, Mathf.Lerp(y0, yEnd, u) + Fx.Rand(-5f, 5f));
			float delay = x / Speed;
			float s = (first ? 0.62f : 0.5f) * (0.85f + 0.3f * Mathf.Sin(u * Mathf.Pi));
			Vanilla.Burst(layer, i % 2 == 0 ? Vanilla.FireLeft : Vanilla.FireRight, p, 0f, s, Lut5.CurseInk, delay: delay);
			Glow.Flash(layer, p + new Vector2(0f, -24f), new Color(1f, 0.06f, 0.14f), 170f, 0.25f, delay);
		}
		// 浪头经过每个敌人：咒火从脚下舔上来 + 一缕怨灵 + 身体暗一下（= 这次上毒）
		foreach (Target t in targets)
		{
			Target tt = t;
			float h = Mathf.Max(80f, (t.At - t.Top).Length());
			Vector2 foot = t.At + new Vector2(0f, h * 0.75f);
			float k = Mathf.Clamp(t.Width / 240f, 0.55f, 1.3f);
			float delay = Mathf.Max(0f, (t.At.X - feet.X) * facing) / Speed;
			bool firstHit = first && ReferenceEquals(t.Body, targets[0].Body);
			Fx.After(layer, delay, () =>
			{
				Sfx.Play(Sfx.PoisonTick, first ? 0.55f : 0.38f);
				if (firstHit)
				{
					HitStop.Request(0.03);                               // 每批连抽只有第一道浪的第一下有一点顿挫
					Stage.ShakeDir?.Invoke(1, 0f);
				}
				Vanilla.Burst(layer, Vanilla.FireCenter, foot, 0f, (first ? 0.95f : 0.8f) * k, Lut5.CurseInk);
				Vanilla.Burst(layer, Vanilla.Embers, foot, 0f, 0.8f * k, Lut5.Curse);
				Glow.Flash(layer, foot + new Vector2(0f, -50f * k), new Color(1f, 0.12f, 0.18f), 260f * k, 0.3f);
				Paint.Rise(layer, "curse_soul", tt.At + new Vector2(Fx.Rand(-0.2f, 0.2f) * tt.Width, 0.2f * h), 110f * k, 0.6f * k, Lut5.Curse, 0.55f);
				BodyTint.Pulse(tt.Body, new Color(0.78f, 0.52f, 0.6f), 0.08, 0.3);
			});
		}
	}
}
