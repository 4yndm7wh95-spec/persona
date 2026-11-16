using Godot;

namespace P5Joker;

/// <summary>
/// 蛇咬（2026-11-16 用户：原来亚森甩光带 + 敌人那里烟里冒小蛇，两段配合不好 → 推倒重做；不召唤亚森、不扶面具，直接挥手）。
/// 第三版（用户：创意不错但太快、太淡看不清；要有节奏，慢 → 快 → 慢）：
/// ① 慢：他挥手，手前一点红光亮起，一道细光绕着手盘两圈、越盘越亮（蛇蓄势），手边红晕慢慢涨；
/// ② 快：「唰」地扑出去 —— 红黑疾光沿 S 形的路窜向敌人，身后留下一整条 S 形的蛇身光痕（红晕 + 白芯，慢慢淡掉，看得清是条蛇）；
/// ③ 慢：两道月牙毒牙在敌人身上上下合拢「咔」地咬住（顿帧、白闪、空间一扭、火星），咬住停一会儿、红光一下一下往里渗，再慢慢松开散掉。
/// 敌人身上的上毒落点照旧（游戏的上毒等到咬合那一刻）。返回从现在到咬合的秒数。
/// </summary>
public static class SnakebiteFx
{
	private const float Coil = 0.32f, Strike = 0.14f;

	/// <summary>咬合在出手后多少秒。</summary>
	public const float BiteDelay = Coil + Strike;

	public static float Play(Control front, Control back, Vector2 feet, float tall, float face, Vector2 at, Vector2 enemyFeet)
	{
		float h = tall, f = face, u = front.GetCanvasTransform().Scale.Y;
		Vector2 hand = feet + new Vector2(f * h * 0.3f, -h * 0.83f);   // 挥手伸直的手（同触媒、迷雾）
		Color red = new(0.92f, 0.06f, 0.16f), hot = new(1f, 0.95f, 0.94f);
		Color pink = new(1f, 0.32f, 0.38f), deep = new(0.4f, 0.01f, 0.06f);
		Sfx.Take("gx_whoosh", 0.12f, 0.05f);
		Sfx.Take("gx_whoosh", 0.2f, Coil);
		Sfx.Take("gx_slice", 0.26f, BiteDelay);                       // 咬合声（用户：这个不错，保留）；毒的音效走原来的落点（PoisonFx），这里不再加

		// ① 慢：蓄势 —— 手边红晕慢慢涨，一道细光绕手盘两圈
		Sprite2D halo = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(red, 0f) };
		back.AddChild(halo);
		halo.GlobalPosition = hand;
		Fx.Animate(halo, 0, Coil + 0.12, t =>
		{
			float s = t * (Coil + 0.12f);
			float k = Ease5.Seg(s, 0f, Coil);
			halo.Modulate = new Color(1f, 0.15f, 0.22f, (0.25f + 0.55f * k) * (1f - Ease5.Seg(s, Coil, 0.12f)));
			halo.Scale = Vector2.One * h * (0.25f + 0.35f * k) / 32f;
		});
		Fx.FreeAfter(halo, Coil + 0.15);
		for (int j = 0; j < 2; j++)
		{
			float ph = j * Mathf.Pi;
			SkillRedo5.Streak(front, t =>
			{
				float a = ph + f * t * Mathf.Tau * 1.6f;
				float r = h * Mathf.Lerp(0.2f, 0.06f, t);
				return hand + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.55f) * r;
			}, 0.02f + 0.04f * j, Coil - 0.04f, h * 0.35f, h * 0.03f, j == 0 ? red : hot, 1.4f);
		}
		Glow.Glint(front, hand, 0.5f, 0.05f);
		Fx.After(front, Coil, () =>
		{
			Glow.Flash(front, hand, hot, h * 0.35f, 0.1f);
			Glow.Flash(back, hand, red, h * 1.0f, 0.24f);
			Glow.Glint(front, hand, 0.9f);
		});

		// ② 快：扑出去，沿 S 形的路；身后留一整条蛇身光痕
		Vector2 dv = at - hand, nrm = dv.Orthogonal().Normalized();
		float amp = Mathf.Min(h * 0.26f, dv.Length() * 0.13f);
		Vector2 Path(float t, float ph) => hand.Lerp(at, t) + nrm * Mathf.Sin(t * Mathf.Pi * 2f + ph) * amp * (1f - t * 0.8f);
		// 2026-11-16 第四版：蛇身不用光带（塑料、像贴图）→ 光头沿 S 形窜过去，身后洒一路柔光粒（像蛇身散成的光尘）
		EmberTrail.Run(back, t => Path(t, 0.4f), Coil, Strike, h * 0.3f, 70, new Color(0.55f, 0.02f, 0.1f, 0.7f), 0.55f);
		EmberTrail.Run(front, t => Path(t, 0f), Coil, Strike, h * 0.2f, 90, new Color(0.95f, 0.08f, 0.18f, 0.8f), 0.5f, null, 1.3f);
		// ③ 慢：毒牙合拢、咬住停一下、红光往里渗，再松开
		float ang = f > 0f ? 0f : Mathf.Pi;
		float jaw = h * 0.85f;
		global::P5Joker.Blade.Arc(front, at + new Vector2(0f, -h * 0.1f), ang, jaw, h * 0.2f, h * 0.15f, f < 0f, 0.05f, 0.05f, 0.2f, 0.28f, true, BiteDelay - 0.04f);
		global::P5Joker.Blade.Arc(front, at + new Vector2(0f, h * 0.1f), ang, jaw * 0.9f, h * 0.18f, h * 0.13f, f > 0f, 0.05f, 0.05f, 0.2f, 0.28f, true, BiteDelay - 0.03f);
		Fx.After(front, BiteDelay, () =>
		{
			HitStop.Request(0.06);
			Fx.Shake(1);
			Glow.Flash(front, at, hot, h * 0.45f, 0.12f);
			Glow.Flash(back, at, red, h * 1.4f, 0.36f);
			Glow.Ripple(front, at, h * 1.2f, 0.4f, 0.03f);
			Glow.Sparks(front, at, 22, 140f * u, 560f * u, 0.45f, new[] { hot, pink, deep }, 0f, 0.008f * u, 0.024f * u, new Vector2(f, -0.2f), 120f);
			Vanilla.Burst(front, Vanilla.Specks, at, 0f, 0.9f, Lut5.Blood, amount: 16);
		});
		// 咬住时红光一下一下往里渗
		for (int k = 0; k < 3; k++)
		{
			float d = BiteDelay + 0.12f + 0.11f * k;
			Fx.After(back, d, () => Glow.Flash(back, at, new Color(0.8f, 0.03f, 0.13f), h * (0.9f - 0.15f * k), 0.2f));
		}
		return BiteDelay;
	}
}

/// <summary>
/// 光尘轨迹（2026-11-16 用户：光束 / 光带塑料感太强、像贴图）：一颗亮的光头沿 path 跑，身后一路洒下柔光粒（大小不一、各自往外漂一点、慢慢淡掉），
/// 读起来是一股「光尘」流过去，不是一条实心的带子。蛇咬的蛇身、致命毒药的咒怨都用这个。
/// </summary>
public static class EmberTrail
{
	public static void Run(Control layer, System.Func<float, Vector2> path, float t0, float dur, float size, int n, Color col, float linger = 0.4f,
		System.Func<float, float>? ease = null, float headSize = 1f)
	{
		System.Func<float, float> ez = ease ?? Ease5.OutQuint;
		// 光头
		Node2D head = Fx.Root(layer, path(0f), "P5EmberHead", t0 + dur + 0.1);
		Sprite2D hg = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = col, Scale = Vector2.One * size * 2.2f * headSize / 32f };
		Sprite2D hc = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(1f, 0.95f, 0.93f), Scale = Vector2.One * size * 0.8f * headSize / 32f };
		head.AddChild(hg);
		head.AddChild(hc);
		head.Visible = false;
		Fx.Animate(head, t0, dur, t =>
		{
			head.Visible = true;
			head.GlobalPosition = path(ez(t));
			head.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.85f, 0.15f));
		});
		// 一路洒下的光粒
		for (int i = 0; i < n; i++)
		{
			float k = (i + Fx.Rand(0f, 1f)) / n;
			float when = t0 + dur * InvEase(ez, k);
			float sz = size * Fx.Rand(0.45f, 1.15f), life = linger * Fx.Rand(0.6f, 1.2f);
			Vector2 drift = Vector2.FromAngle(Fx.Rand(0f, Mathf.Tau)) * size * Fx.Rand(0.3f, 1.2f) + new Vector2(0f, -size * 0.6f);
			bool hot = i % 4 == 0;
			Color c = hot ? new Color(1f, 0.85f, 0.85f) : new Color(col.R * Fx.Rand(0.6f, 1f), col.G, col.B, col.A);
			Fx.After(layer, when, () =>
			{
				Vector2 p0 = path(k);
				Node2D r = Fx.Root(layer, p0, "P5Ember", life + 0.05);
				Sprite2D sp = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = c };
				r.AddChild(sp);
				Fx.Animate(r, 0, life, t =>
				{
					r.GlobalPosition = p0 + drift * Fx.OutCubic(t);
					sp.Scale = Vector2.One * sz * (hot ? 0.6f : 1f) * Mathf.Lerp(1.1f, 0.4f, t) / 32f;
					sp.Modulate = new Color(c, c.A * (1f - t) * Mathf.Min(1f, t * 10f + 0.3f));
				});
			});
		}
	}

	private static float InvEase(System.Func<float, float> ez, float y)
	{
		float lo = 0f, hi = 1f;
		for (int i = 0; i < 14; i++) { float m = (lo + hi) * 0.5f; if (ez(m) < y) lo = m; else hi = m; }
		return (lo + hi) * 0.5f;
	}
}
