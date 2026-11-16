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
		SkillRedo5.Streak(back, t => Path(t, 0.5f), Coil, Strike, h * 1.2f, h * 0.16f, deep, 0.9f);
		SkillRedo5.Streak(front, t => Path(t, 0f), Coil, Strike, h * 1.3f, h * 0.13f, red, 1.4f);
		SkillRedo5.Streak(front, t => Path(t, 0f), Coil + 0.01f, Strike, h * 0.9f, h * 0.05f, hot, 1.6f);
		Body(front, back, t => Path(t, 0f), Coil, Strike, h, red, hot);

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

	/// <summary>蛇身光痕：扑过去的同时沿路径长出来（红晕 + 白芯，头宽尾细），到头后停一下、慢慢淡掉。</summary>
	private static void Body(Control front, Control back, System.Func<float, Vector2> path, float t0, float dur, float h, Color red, Color hot)
	{
		const int n = 36;
		float life = dur + 0.45f;
		Node2D root = Fx.Root(back, Vector2.Zero, "P5SnakeBody", t0 + life + 0.05);
		root.GlobalPosition = Vector2.Zero;
		Curve taper = new();
		taper.AddPoint(new Vector2(0f, 0.2f));
		taper.AddPoint(new Vector2(0.7f, 0.9f));
		taper.AddPoint(new Vector2(1f, 0.6f));
		Line2D glow = new() { Width = h * 0.16f, DefaultColor = new Color(red, 0.45f), WidthCurve = taper, Material = Fx.Additive, Antialiased = true,
			JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round };
		Line2D core = new() { Width = h * 0.035f, DefaultColor = new Color(hot, 0.85f), WidthCurve = taper, Material = Fx.Additive, Antialiased = true,
			JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round };
		root.AddChild(glow);
		root.AddChild(core);
		root.Visible = false;
		Fx.Animate(root, t0, life, k =>
		{
			root.Visible = true;
			float s = k * life;
			float head = Ease5.OutQuint(Mathf.Min(1f, s / dur));
			float tail = Fx.InCubic(Ease5.Seg(s, dur + 0.1f, life - dur - 0.1f));
			var pts = new Vector2[n];
			for (int i = 0; i < n; i++) pts[i] = path(Mathf.Lerp(tail, head, i / (n - 1f)));
			glow.Points = pts;
			core.Points = pts;
			float o = 1f - Ease5.Seg(s, dur + 0.15f, life - dur - 0.15f);
			glow.Modulate = new Color(1f, 1f, 1f, o);
			core.Modulate = new Color(1f, 1f, 1f, o);
		});
	}
}
