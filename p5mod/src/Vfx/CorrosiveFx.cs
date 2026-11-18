using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 腐蚀波（稀有，毒流重点牌：开了之后这回合每抽一张牌都给全体上毒）。
/// 2026-11-18 第五版（审阅：重做；同天用户拉黑火焰）：原来全是原版火焰 / 烟序列帧（咒怨 LUT）→ 全部换掉，改成「咒毒潮」，用涂毒那团翻滚咒毒的画法（用户认可）。
/// · 打出（Plunge）：亚森出手，一道粗红疾光从爪尖扎进 Joker 身前的地面 —— 顿帧、震屏，地上「啪」地溅开一大滩翻滚的咒毒（压扁贴地），往上溅起碎光，两侧贴地甩出几道短疾光；
/// · 持续（Aura）：Joker 脚下一滩咒毒一直在翻（一层层压扁的咒毒交替长出来、淡掉），偶尔冒一点往上溅的碎光；
/// · 每抽一张牌（Wave）：脚下咒毒一亮，一股咒毒潮贴着地面飞快涌向敌人（潮头一团咒毒 + 红光，身后留下一路渐淡的毒迹，贴地一道疾光），
///   潮头经过每个敌人：咒毒从它脚下涌起来一下、往上溅碎光、身体暗一下（= 这次上毒）。连抽时一道接一道，很轻、只有第一下有一点顿挫。
/// </summary>
public static class CorrosiveFx
{
	/// <summary>一个敌人：中心、头顶、宽度、本体（染色用）。</summary>
	public record struct Target(Vector2 At, Vector2 Top, float Width, Node2D? Body);

	private static ulong _suppressUntil, _nextWaveAt, _auraNext;

	/// <summary>抽牌带来的上毒正在由咒毒潮表现：PoisonFx.Apply 这段时间不放普通落点。</summary>
	public static bool Suppressing => Stage.NowMs() < _suppressUntil;

	private static readonly Color Hot = new(1f, 0.95f, 0.94f), Red = new(1f, 0.1f, 0.18f);

	private static float U(Control layer) => Fx.Screen(layer).Y / 1080f;

	/// <summary>
	/// 一滩贴地的咒毒（涂毒那团咒毒压扁）：at 为中心，size 宽，squash 压扁比例；grow 秒长出来（带一点回弹）、life 秒后淡掉。
	/// rise = 往上涌（不压扁、从脚下冒起来再落回去）。
	/// </summary>
	private static void Pool(Control layer, Vector2 at, float size, float squash, float delay, float grow, float life, float alpha = 1f, bool rise = false)
	{
		Node2D root = Fx.Root(layer, at, "P5VenomPool", delay + life + 0.1);
		root.Scale = new Vector2(1f, squash);
		ColorRect q = CardSkillFx.VenomBlob(root, size);
		ShaderMaterial m = (ShaderMaterial)q.Material;
		m.SetShaderParameter("rimk", 0.3f);
		m.SetShaderParameter("soft", 0.07f);
		Glow.SoftSprite(root, Vector2.Zero, new Color(0.8f, 0.04f, 0.12f, 0.55f), size / 32f * 0.9f);   // 底下一层柔红光，边不硬
		float speed = Fx.Rand(1.2f, 1.8f), seed = Fx.Rand(0f, 30f);
		root.Visible = false;
		Fx.Animate(root, delay, life, t =>
		{
			root.Visible = true;
			float s = t * life;
			float g = Ease5.OutBack(Mathf.Min(1f, s / grow));
			if (rise) root.Scale = new Vector2(1f, squash * (0.4f + 0.6f * g) * (1f - 0.5f * Ease5.Seg(t, 0.5f, 0.5f)));
			m.SetShaderParameter("clock", seed + s * speed);
			m.SetShaderParameter("grow", g * (1f - 0.3f * Ease5.Seg(t, 0.6f, 0.4f)));
			m.SetShaderParameter("alpha", alpha * (1f - Fx.InCubic(Ease5.Seg(t, 0.55f, 0.45f))));
		});
	}

	/// <summary>打出：一道粗红疾光从 claw（亚森爪尖；没有就从空中）扎进 ground（Joker 身前的地面），地上溅开一大滩咒毒。</summary>
	public static void Plunge(Control layer, Vector2 claw, Vector2 ground)
	{
		float u = U(layer);
		const float fly = 0.07f;
		SkillRedo5.StreakSharp(layer, e => claw.Lerp(ground, e), 0f, fly, 420f * u, 60f * u, Red, 1.5f);
		SkillRedo5.StreakSharp(layer, e => claw.Lerp(ground, e) + new Vector2(14f * u, 0f), 0.015f, fly, 260f * u, 9f * u, new Color(1f, 0.6f, 0.6f), 1.8f);
		Fx.After(layer, fly, () =>
		{
			Sfx.Play(Sfx.Slam, 0.65f);
			Sfx.Play(Sfx.SubBoom, 0.7f);
			Sfx.Play(Sfx.Curse, 0.5f);
			HitStop.Request(0.07);
			Stage.ShakeDir?.Invoke(3, 90f);
			Glow.Flash(layer, ground, Hot, 200f * u, 0.12f);
			Glow.Flash(layer, ground + new Vector2(0f, -40f * u), new Color(0.85f, 0.04f, 0.13f), 620f * u, 0.5f);
			SkillRedo5.Cross(layer, ground + new Vector2(0f, -10f * u), 260f * u);
			// 溅开：一大滩 + 两侧两小滩
			Pool(layer, ground, 560f * u, 0.3f, 0f, 0.14f, 1.3f);
			Pool(layer, ground + new Vector2(-170f * u, 6f * u), 280f * u, 0.3f, 0.05f, 0.12f, 1.0f, 0.85f);
			Pool(layer, ground + new Vector2(170f * u, 6f * u), 280f * u, 0.3f, 0.07f, 0.12f, 1.0f, 0.85f);
			// 往上溅的碎光、两侧贴地甩出去的短疾光
			Phantom.Shards(layer, ground, 22, 420f * u, 1000f * u, Vector2.Up, 70f, 0.5f, 1600f * u, 20f * u);
			for (int i = 0; i < 4; i++)
			{
				float side = i % 2 == 0 ? 1f : -1f;
				Vector2 to = ground + new Vector2(side * Fx.Rand(260f, 420f) * u, Fx.Rand(-6f, 10f) * u);
				SkillRedo5.StreakSharp(layer, e => ground.Lerp(to, e), 0.01f * i, 0.16f, 160f * u, 8f * u, Red, 1.4f);
			}
			for (int i = 0; i < 6; i++)
				CardSkillFx.Drop(layer, ground + new Vector2(Fx.Rand(-180f, 180f) * u, -Fx.Rand(160f, 300f) * u), ground.Y + Fx.Rand(-4f, 8f) * u, 0.12f + 0.05f * i, 300f * u, u);
		});
	}

	/// <summary>持续：每帧调用（腐蚀波生效时）；约每 0.4s 在 Joker 脚下长出一层压扁的咒毒（交替叠着 = 一直在翻），偶尔往上溅一点碎光。</summary>
	public static void Aura(Control layer, Vector2 feet)
	{
		ulong now = Stage.NowMs();
		if (now < _auraNext)
		{
			return;
		}
		_auraNext = now + 400;
		float u = U(layer);
		Vector2 at = feet + new Vector2(Fx.Rand(-40f, 40f) * u, Fx.Rand(-2f, 6f) * u);
		Pool(layer, at, Fx.Rand(300f, 380f) * u, 0.26f, 0f, 0.3f, 1.2f, 0.75f);
		Glow.Flash(layer, feet + new Vector2(0f, -20f * u), new Color(0.6f, 0.02f, 0.1f), 280f * u, 0.6f);
		if (Fx.Rng.Next(3) == 0)
		{
			Phantom.Shards(layer, at, 3, 140f * u, 320f * u, Vector2.Up, 30f, 0.35f, 900f * u, 10f * u);
		}
	}

	/// <summary>
	/// 抽到一张牌（CorrosiveDrawPatch，在原版给全体上毒之前）：排一道咒毒潮，从 feet 贴地涌向 targets。
	/// 连抽时一道接一道（间隔 0.12s）；之后 0.35s 内（加上排队时间）的上毒都由潮表现。
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

	private const float Speed = 2400f;    // 潮头每秒走多少像素（1080p）

	private static void Wave(Control layer, Vector2 feet, float facing, IReadOnlyList<Target> targets, bool first)
	{
		if (targets.Count == 0)
		{
			return;
		}
		float u = U(layer), speed = Speed * u;
		// 声音（用户：腐蚀波刺耳）：每道潮只有一声低沉的挥动 + 低频，碰到第一个敌人时一声柔和的咒怨闷响
		Sfx.Play(Sfx.SwishLow, first ? 0.5f : 0.35f);
		Sfx.Play(Sfx.SubBoom, first ? 0.35f : 0.22f);
		Glow.Flash(layer, feet + new Vector2(0f, -15f * u), Red, (first ? 300f : 220f) * u, 0.25f);
		float far = 0f;
		foreach (Target t in targets)
		{
			far = Mathf.Max(far, (t.At.X - feet.X) * facing);
		}
		far += 80f * u;
		float y0 = feet.Y, yEnd = y0;
		foreach (Target t in targets)
		{
			yEnd = Mathf.Max(yEnd, t.At.Y + Mathf.Max(80f, (t.At - t.Top).Length()) * 0.75f);
		}
		Vector2 a = new(feet.X + 50f * u * facing, y0 + 4f * u), b = new(feet.X + far * facing, yEnd + 4f * u);
		float dur = (far - 50f * u) / speed;
		float k = first ? 1f : 0.8f;
		// 潮头：一道贴地的粗疾光 + 跟着跑的一团压扁的红光；后面一道细的副疾光
		SkillRedo5.StreakSharp(layer, e => a.Lerp(b, e) + new Vector2(0f, -10f * u), 0f, dur + 0.05f, 460f * u, 46f * u * k, Red, 1.5f);
		SkillRedo5.StreakSharp(layer, e => a.Lerp(b, e) + new Vector2(0f, 6f * u), 0.03f, dur + 0.05f, 280f * u, 7f * u, new Color(1f, 0.55f, 0.55f), 1.8f);
		Node2D crest = Fx.Root(layer, a, "P5VenomCrest", dur + 0.1);
		crest.Scale = new Vector2(1.8f, 0.5f);
		Glow.SoftSprite(crest, Vector2.Zero, new Color(1f, 0.08f, 0.16f, 0.9f), 7f * u * k);
		Glow.SoftSprite(crest, Vector2.Zero, new Color(1f, 0.8f, 0.8f, 0.7f), 2f * u * k);
		Fx.Animate(crest, 0, dur, t =>
		{
			crest.GlobalPosition = a.Lerp(b, t) + new Vector2(0f, -10f * u);
			crest.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 10f) * (1f - Ease5.Seg(t, 0.9f, 0.1f)));
		});
		// 沿路：潮头经过的地方一簇簇往上溅（带下坠的碎光）+ 一点贴地红光
		int i = 0;
		for (float x = 110f * u; x <= far; x += 120f * u, i++)
		{
			float v = (x - 50f * u) / Mathf.Max(1f, far - 50f * u);
			Vector2 p = a.Lerp(b, Mathf.Clamp(v, 0f, 1f));
			float d = v * dur;
			Phantom.Shards(layer, p, 3, 160f * u, 420f * u, new Vector2(facing * 0.3f, -1f), 30f, 0.3f, 1500f * u, 11f * u * k, d);
			Glow.Flash(layer, p + new Vector2(0f, -6f * u), new Color(0.9f, 0.05f, 0.14f), 120f * u * k, 0.28f, d);
		}
		// 潮头经过每个敌人：咒毒从脚下往上窜（几道往上的细疾光 + 一簇带下坠的碎光）、身体暗一下（= 这次上毒）
		foreach (Target t in targets)
		{
			Target tt = t;
			float h = Mathf.Max(80f, (t.At - t.Top).Length());
			Vector2 foot = t.At + new Vector2(0f, h * 0.75f);
			float kk = Mathf.Clamp(t.Width / 240f, 0.55f, 1.3f);
			float delay = Mathf.Clamp(((t.At.X - feet.X) * facing - 50f * u) / speed, 0f, dur);
			bool firstHit = first && ReferenceEquals(t.Body, targets[0].Body);
			bool lead = ReferenceEquals(t.Body, targets[0].Body);
			Fx.After(layer, delay, () =>
			{
				if (lead)
				{
					Sfx.Play(Sfx.CurseSoft, first ? 0.6f : 0.42f);
				}
				if (firstHit)
				{
					HitStop.Request(0.03);                               // 每批连抽只有第一道潮的第一下有一点顿挫
					Stage.ShakeDir?.Invoke(1, 0f);
				}
				for (int j = 0; j < 3; j++)
				{
					Vector2 s0 = foot + new Vector2((j - 1) * tt.Width * 0.22f + Fx.Rand(-10f, 10f) * u, 0f);
					Vector2 s1 = s0 + new Vector2(Fx.Rand(-0.08f, 0.08f) * h, -h * Fx.Rand(0.9f, 1.4f));
					SkillRedo5.StreakSharp(layer, e => s0.Lerp(s1, e), 0.02f * j, 0.22f, h * 0.8f, 30f * u * kk, Red, 1.5f);
				}
				Phantom.Shards(layer, foot + new Vector2(0f, -10f * u), first ? 10 : 7, 300f * u, 720f * u, Vector2.Up, 35f, 0.45f, 1400f * u, 15f * u * kk);
				Glow.Flash(layer, foot + new Vector2(0f, -40f * kk * u), new Color(1f, 0.12f, 0.18f), 300f * kk * u, 0.32f);
				Glow.Flash(layer, foot, Hot, 90f * kk * u, 0.1f);
				BodyTint.Pulse(tt.Body, new Color(0.78f, 0.52f, 0.6f), 0.08, 0.3);
			});
		}
	}
}
