using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 增益牌第一批（2026-11-15 第二版）：预判、侧步、磨蚀、速行者、触媒。
/// 用户：第一版「太简单、没创意、不好看」；特效好看最重要，不用非得表达牌意；稀有度低的不用那么华丽但不能差；通用特效可以用。
/// → 主体换成手绘贴图（art/p5/fx：slash_cross / slash_thrust / slash_tear / slash_flurry / chain、碎玻璃）和认可过的 P5 月牙刀光（Blade.Arc），
///   代码画的光线只做衬托。
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

	/// <summary>光线往一点收（蓄力）：n 道，在 arrive 秒收到。</summary>
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

	/// <summary>手绘「slash_thrust」从 from 扫到 to（宽头在 from、尖在 to），travel 秒扫满；thick = 粗细（像素）。</summary>
	private static void Thrust(Control layer, Vector2 from, Vector2 to, float travel, float thick, Texture2D lut, float delay = 0f, float hold = 0.03f, float erode = 0.2f)
	{
		Vector2 d = to - from;
		float len = d.Length();
		if (len < 20f) return;
		float sx = len / 940f;
		Vector2 center = from + d * 0.5f + d.Normalized() * (512f - 530f) * sx;
		Paint.Sweep(layer, "slash_thrust", center, d.Angle(), new Vector2(sx, thick / 256f), lut, travel, hold, erode, 0f, delay, 1.5f);
	}

	/// <summary>手绘贴图整张横向扫出（slash_cross / slash_tear / slash_flurry 这类 1024×512 的），宽 width 像素。</summary>
	private static void Painted(Control layer, string tex, Vector2 at, float rot, float width, Texture2D lut, float flipX = 1f,
		float reveal = 0.07f, float hold = 0.1f, float erode = 0.28f, float delay = 0f, float glow = 1.25f, float squash = 1f)
	{
		float s = width / 1024f;
		Paint.Sweep(layer, tex, at, rot, new Vector2(s * flipX, s * squash), lut, reveal, hold, erode, 0f, delay, glow);
	}

	/// <summary>
	/// 一把碎玻璃（防御那面玻璃墙的同一套贴图和折射着色器，GuardFx）：从 at 朝 dir 迸开、边飞边转、往下掉，转到某个角度闪一下。
	/// </summary>
	private static void Glass(Control layer, Vector2 at, int n, float size, Vector2 dir, float spreadDeg, float speed, float life, float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			if (Fx.Tex("glass_shards_sheet") is not Texture2D sheet) return;
			float cell = sheet.GetWidth() / 4f;
			layer.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport, Name = "P5GlassCopy" });
			Node2D root = Fx.Root(layer, at, "P5Glass", life + 0.1);
			for (int i = 0; i < n; i++)
			{
				ShaderMaterial m = new() { Shader = GuardFx.ShardShader };
				m.SetShaderParameter("tilt", new Vector2(Fx.Rand(-1f, 1f), Fx.Rand(-1f, 1f)));
				int idx = (int)Fx.Rand(0f, 15.99f);
				Sprite2D sp = new()
				{
					Texture = sheet, RegionEnabled = true, RegionRect = new Rect2(idx % 4 * cell, idx / 4 * cell, cell, cell),
					Material = m, FlipH = Fx.Rand(0f, 1f) < 0.5f,
				};
				root.AddChild(sp);
				float ang = dir.Angle() + Mathf.DegToRad(Fx.Rand(-spreadDeg, spreadDeg) * 0.5f);
				Vector2 v = Vector2.FromAngle(ang) * speed * Fx.Rand(0.45f, 1f);
				float sc = size * Fx.Rand(0.5f, 1.1f) / cell, spin = Fx.Rand(-9f, 9f), r0 = Fx.Rand(-3f, 3f), ph = Fx.Rand(0f, 6f);
				float g = speed * 1.6f;
				Fx.Animate(sp, 0, life, t =>
				{
					float s = t * life;
					float k = 1f - Mathf.Exp(-s * 5f);
					sp.Position = v * k / 5f * 2.2f + new Vector2(0f, g * s * s * 0.5f);
					sp.Rotation = r0 + spin * k;
					sp.Scale = Vector2.One * sc * Mathf.Lerp(0.5f, 1f, Mathf.Min(1f, t * 8f));
					m.SetShaderParameter("opacity", Mathf.Min(1f, t * 10f) * (1f - Ease5.Seg(t, 0.6f, 0.4f)));
					m.SetShaderParameter("glint", Mathf.Pow(Mathf.Max(0f, Mathf.Sin(s * 14f + ph)), 10f));
				});
			}
		});
	}

	/// <summary>敌人身上闪一下颜色（tint 峰值，dur 秒回原色）。</summary>
	private static void Flick(SkillFx.Victim v, Control layer, Color peak, float dur, float delay = 0f)
	{
		if (v.Tint is not { } tint) return;
		Fx.After(layer, delay, () => Fx.Animate(layer, 0, dur, t => tint(Colors.White.Lerp(peak, Mathf.Pow(1f - t, 2f)))));
	}

	private static Vector2 Mid(SkillFx.Victim v) => v.Feet + (v.Top - v.Feet) * 0.5f;

	// ═══════════════════════════ 预判（普通） ═══════════════════════════

	/// <summary>
	/// 预判约 0.75s：面具上一点星光；他身前一道手绘的 X 字斩「唰」地交叉划开，交点一白，迸出一把会折射背景的碎玻璃（往敌人那边飞、边转边闪）。
	/// </summary>
	public static void Anticipate(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float cut = 0.1f, burst = 0.17f;
		S("gx_ping", 0.12f);
		S("gx_slice", 0.22f, cut);
		S("gx_pop", 0.14f, burst);
		Glow.Glint(front, c.Mask + new Vector2(f * h * 0.02f, 0f), 0.55f);
		Flare(front, c.Mask, h * 0.5f, h * 0.012f, new Color(1f, 0.9f, 0.9f), 0.16f);
		Vector2 at = c.Feet + new Vector2(f * h * 0.44f, -h * 0.58f);
		Glow.Flash(back, at, new Color(1f, 0.1f, 0.18f), h * 1.6f, 0.4f, cut);
		Painted(front, "slash_cross", at, f * -0.08f, h * 1.15f, Lut5.Blood, f, 0.07f, 0.12f, 0.3f, cut);
		Fx.After(front, burst, () =>
		{
			HitStop.Request(0.03);
			Glow.Flash(front, at, Hot, h * 0.45f, 0.1f);
			Glow.Glint(front, at, 1.1f);
			Glass(front, at, 18, h * 0.2f, new Vector2(f, -0.25f), 110f, h * 2.6f, 0.75f);
			Glow.Sparks(front, at, 18, 160f * u, 560f * u, 0.45f, new[] { Hot, Pink, Deep }, 0f, 0.01f * u, 0.03f * u, new Vector2(f, 0f), 120f);
		});
	}

	// ═══════════════════════════ 侧步（罕见） ═══════════════════════════

	/// <summary>
	/// 侧步约 0.95s：身后两道 P5 月牙刀光一前一后横着甩过（闪身留下的刀光），碎光往后溅；
	/// 同时一张预告信从指间弹到他头顶后方、翻着停住、翻正一亮（十字光），然后拉着光折进胸口收起。
	/// </summary>
	public static void Sidestep(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = SkillRedo.CardW(c);
		Control back = c.Back ?? c.Front, front = c.Front;
		S("gx_whoosh", 0.24f);
		S("gx_card_slide", 0.14f, 0.1f);
		S("gx_ping", 0.13f, 0.46f);
		S("gx_card_stack", 0.15f, 0.8f);
		float ang = f > 0f ? Mathf.Pi : 0f;
		global::P5Joker.Blade.Arc(back, c.Feet + new Vector2(-f * h * 0.12f, -h * 0.5f), ang + f * 0.1f, h * 1.6f, h * 0.24f, h * 0.14f, f > 0f, 0.07f, 0.04f, 0.06f, 0.18f);
		global::P5Joker.Blade.Arc(front, c.Feet + new Vector2(-f * h * 0.05f, -h * 0.82f), ang - f * 0.14f, h * 1.2f, h * 0.16f, h * 0.09f, f > 0f, 0.07f, 0.04f, 0.05f, 0.16f, true, 0.06f);
		Phantom.Shards(front, c.Feet + new Vector2(0f, -h * 0.4f), 10, 400f * u, 900f * u, new Vector2(-f, -0.1f), 20f, 0.3f, 0f, 24f * u, 0.05f);

		Vector2 top = c.Feet + new Vector2(-f * h * 0.32f, -h * 1.22f);
		Phantom.P5Card card = Phantom.Card(front, c.Hand, w * 1.5f, 0.95, 0.5f);
		card.Root.Visible = false;
		const float t0 = 0.1f, park = 0.44f, dive = 0.74f, end = 0.85f;
		Fx.Animate(card.Root, t0, end - t0, t =>
		{
			card.Root.Visible = true;
			float s = t0 + t * (end - t0);
			if (s < park)
			{
				float e = Ease5.OutQuint((s - t0) / (park - t0));
				Vector2 ctrl = c.Hand.Lerp(top, 0.5f) + new Vector2(0f, -h * 0.25f);
				card.Place(c.Hand.Lerp(ctrl, e).Lerp(ctrl.Lerp(top, e), e), -f * 0.25f * e, Mathf.Pi + e * Mathf.Tau * 2f, 0.7f + 0.3f * e);
			}
			else if (s < dive)
			{
				float k = (s - park) / (dive - park);
				card.Place(top + new Vector2(0f, -Mathf.Sin(k * Mathf.Pi) * h * 0.02f), -f * 0.25f, Mathf.Tau * 3f, 1f);
				card.Set("sheen", k);
				card.Set("glow", 0.6f * Mathf.Exp(-k * 8f));
			}
			else
			{
				float e = Fx.InCubic((s - dive) / (end - dive));
				card.Place(top.Lerp(c.Chest, e), -f * 0.25f + e * f, Mathf.Tau * 3f, 1f - 0.7f * e);
				card.Set("glow", e);
			}
		});
		Fx.After(front, park, () =>
		{
			Glow.Flash(front, top, Hot, w * 2.4f, 0.12f);
			Cross(back, top, h * 0.55f);
			Glow.Glint(front, top + new Vector2(f * w * 0.5f, -w * 0.6f), 0.7f);
		});
		Fx.After(front, end + 0.01f, () =>
		{
			card.Trail?.Kill();
			card.Root.Visible = false;
			Glow.Flash(front, c.Chest, Hot, h * 0.3f, 0.1f);
			Glow.Flash(back, c.Chest, new Color(1f, 0.12f, 0.2f), h * 1.1f, 0.3f);
			Phantom.Confetti(front, c.Chest, 14, 420f * u, Vector2.Up, 120f, 0.7f, 8f * u);
		});
	}

	// ═══════════════════════════ 磨蚀（稀有能力） ═══════════════════════════

	/// <summary>一条带刃的锁链：从 root 朝 dir 甩出 len（shoot 秒开始、outDur 秒甩满），停住、轻摆，retract 秒时收回。</summary>
	private static void Chain(Control back, Control front, Vector2 root, Vector2 dir, float len, float width, float shoot, float outDur, float retract, float u)
	{
		if (Fx.Tex("chain") is not Texture2D tex) return;
		float th = tex.GetHeight(), tw = tex.GetWidth();
		float life = retract + 0.3f;
		Paint.Stroke? st = Paint.Make(back, "chain", root, dir.Angle() - Mathf.Pi * 0.5f, new Vector2(width / tw, len / th), Lut5.Blood, 0.7f, 1.25f);
		if (st == null) return;
		st.Set("glow", 1.35f);
		st.Root.Modulate = new Color(1f, 0.42f, 0.48f);
		var sprites = new List<Sprite2D>();
		foreach (Node n in st.Root.GetChildren())
			if (n is Sprite2D sp) { sp.Centered = false; sp.Offset = new Vector2(-tw * 0.5f, 0f); sp.RegionEnabled = true; sp.RegionRect = new Rect2(0f, 0f, tw, 1f); sprites.Add(sp); }
		st.Root.Visible = false;
		float baseRot = st.Root.Rotation, ph = Fx.Rand(0f, 6f);
		Fx.Animate(st.Root, shoot, life - shoot, t =>
		{
			st.Root.Visible = true;
			float s = shoot + t * (life - shoot);
			float ext = s < retract ? Ease5.OutQuint(Mathf.Min(1f, (s - shoot) / outDur)) : 1f - Fx.InCubic(Mathf.Min(1f, (s - retract) / 0.16f));
			// 链从根部往外长：显示贴图从上端（根）开始的 ext 段
			foreach (Sprite2D sp in sprites) sp.RegionRect = new Rect2(0f, 0f, tw, Mathf.Max(1f, th * ext));
			float sway = s > shoot + outDur ? Mathf.Sin((s - shoot) * 9f + ph) * 0.035f * (1f - Ease5.Seg(s, retract, 0.1f)) : 0f;
			st.Root.Rotation = baseRot + sway;
		});
		Fx.FreeAfter(st.Root, life + 0.1);
		// 刃尖：链头一枚手绘尖刺 + 星光，甩到头时亮一下
		Fx.After(front, shoot + outDur * 0.7f, () =>
		{
			Vector2 tip = root + dir * len;
			Glow.Glint(front, tip, 0.7f);
			Glow.Flash(front, tip, Hot, width * 3.5f, 0.1f);
			Glow.Sparks(front, tip, 8, 80f * u, 300f * u, 0.35f, new[] { Hot, Pink, Deep }, 0f, 0.008f * u, 0.02f * u, dir, 60f);
			Thrust(front, tip - dir * width * 4.5f, tip + dir * width * 1.6f, 0.05f, width * 2.4f, Lut5.Blood, 0f, Mathf.Max(0.05f, retract - shoot - outDur * 0.7f - 0.05f), 0.14f);
		});
	}

	/// <summary>
	/// 磨蚀约 1.6s：四周暗下来、光线往他身上收；主拍时六条带刃的红锁链从他身后朝四面「唰」地甩出、绷住，链头的刃尖逐个亮起；
	/// 同一刻身前一团手绘乱斩（slash_flurry）炸开、十字光、重震。锁链轻轻摆着停一会儿，再一齐收回，碎光和纸屑。
	/// </summary>
	public static void Abrasive(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float beat = 0.36f, retract = 1.15f;
		S("gx_whoosh", 0.16f, 0.05f);
		S("gx_slice", 0.26f, beat);
		S("gx_pop", 0.2f, beat + 0.02f);
		S("gx_ping", 0.12f, beat + 0.12f);
		S("gx_whoosh", 0.16f, retract);
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid, new Vector2(h * 3.2f, h * 2.6f), 1.7, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.75f, 0.25f)) * 0.75f);
		Gather(front, c.Chest, h, u, beat, 20, 0.75f);
		Glow.Flash(back, mid, new Color(1f, 0.1f, 0.18f), h * 1.4f, 0.4f, beat - 0.2f);

		// 六条锁链：从背后（胸口略后）朝四面甩出，上半多、下半少，长短错开
		float[] angs = { -2.55f, -1.95f, -1.2f, -0.55f, 0.35f, 2.75f };
		for (int i = 0; i < angs.Length; i++)
		{
			float a = angs[i];
			Vector2 dir = new(Mathf.Cos(a) * f, Mathf.Sin(a));
			float len = h * (i % 2 == 0 ? 1.0f : 0.78f);
			Vector2 root = c.Chest + new Vector2(-f * h * 0.04f, h * 0.02f) + dir * h * 0.08f;
			Chain(back, front, root, dir, len, h * 0.075f, beat - 0.02f + 0.025f * i, 0.13f, retract + 0.02f * i, u);
		}
		Fx.After(front, beat, () =>
		{
			HitStop.Request(0.05);
			Fx.Shake(2);
			Glow.Flash(front, mid, Hot, h * 0.5f, 0.12f);
			Glow.Flash(back, mid, new Color(1f, 0.12f, 0.2f), h * 2.4f, 0.4f);
			Cross(back, c.Chest, h * 1.1f);
			Painted(front, "slash_flurry", mid + new Vector2(f * h * 0.05f, 0f), f * 0.1f, h * 1.6f, Lut5.Blood, f, 0.08f, 0.1f, 0.32f);
			Glow.Sparks(front, mid, 30, 160f * u, 700f * u, 0.6f, new[] { Hot, Pink, Deep }, -40f * u, 0.008f * u, 0.03f * u, Vector2.Up, 180f);
		});
		Fx.After(front, retract + 0.16f, () =>
		{
			Glow.Flash(front, c.Chest, Hot, h * 0.35f, 0.1f);
			Glow.Flash(back, c.Chest, new Color(1f, 0.12f, 0.2f), h * 1.3f, 0.3f);
			Phantom.Confetti(front, c.Chest, 26, 560f * u, Vector2.Up, 160f, 0.9f, 9f * u);
			Phantom.Shards(front, c.Chest, 12, 300f * u, 800f * u, Vector2.Up, 180f, 0.3f, 0f, 22f * u);
		});
	}

	// ═══════════════════════════ 速行者（罕见能力） ═══════════════════════════

	/// <summary>
	/// 速行者约 1.3s：手边五张预告信一张张弹起、翻正，排成一小把扇；然后一张接一张射出 —— 每张拉出一道手绘的红白长枪光（slash_thrust）
	/// 钉到一个敌人身上（轮流），钉中处两道细刀口交叉、白闪、碎光，敌人一白；最后一张最粗、轻震。
	/// </summary>
	public static void Speedster(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = SkillRedo.CardW(c);
		Control back = c.Back ?? c.Front, front = c.Front;
		const int n = 5;
		S("gx_fan_open", 0.18f, 0.02f);
		Vector2 hand = c.Hand, mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid + new Vector2(f * h * 0.8f, 0f), new Vector2(h * 4.6f, h * 2.4f), 1.4, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.85f, 0.15f)) * 0.65f);
		var foes = new List<SkillFx.Victim>(c.Enemies);
		foes.Sort((a, b) => (f * a.Feet.X).CompareTo(f * b.Feet.X));
		Vector2 fan = hand + new Vector2(f * h * 0.3f, -h * 0.16f);
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float pop = 0.04f + 0.06f * k, go = 0.5f + 0.1f * k;
			bool last = k == n - 1;
			float a = (k - (n - 1) * 0.5f) * 0.36f;
			Vector2 slot = fan + new Vector2(Mathf.Sin(a) * f, -Mathf.Cos(a)) * h * 0.3f;
			float tilt = a * f;
			Phantom.P5Card card = Phantom.Card(front, hand, w * 1.5f, go + 0.1, 0f);
			card.Root.Visible = false;
			Fx.Animate(card.Root, pop, go - pop, t =>
			{
				card.Root.Visible = true;
				float s = t * (go - pop);
				float e = Ease5.OutQuint(Mathf.Min(1f, s / 0.2f));
				Vector2 shiver = s > 0.2f ? new Vector2(0f, Mathf.Sin(s * 30f + k) * h * 0.004f) : Vector2.Zero;
				card.Place(hand.Lerp(slot, e) + shiver, tilt * e, Mathf.Pi * (1f - e) * 3f, 0.6f + 0.4f * e);
				card.Set("glow", 0.6f * Mathf.Exp(-s * 10f) + (s > go - pop - 0.08f ? 0.8f : 0f));
			});
			Fx.After(front, pop + 0.18f, () => Glow.Glint(front, slot + new Vector2(0f, -w * 0.7f), 0.4f));
			S("gx_slice", last ? 0.24f : 0.15f, go);
			Fx.After(front, go, () =>
			{
				card.Root.Visible = false;
				Vector2 tg = foes.Count > 0 ? Mid(foes[k % foes.Count]) + new Vector2(0f, Fx.Rand(-0.12f, 0.08f) * h) : c.Chest + new Vector2(f * h * 1.8f, 0f);
				float travel = last ? 0.08f : 0.06f, thick = h * (last ? 0.3f : 0.2f);
				Glow.Flash(front, slot, Hot, w * 1.6f, 0.1f);
				Thrust(back, slot, tg, travel, thick, Lut5.Blood, 0f, 0.04f, 0.22f);
				Fx.After(front, travel, () =>
				{
					float ang = (tg - slot).Angle();
					Cut.Razor(front, tg, ang + 0.9f, h * (last ? 0.7f : 0.45f), 6f * u, 0.22f);
					Cut.Razor(front, tg, ang - 0.9f, h * (last ? 0.7f : 0.45f), 6f * u, 0.22f, 0.03f);
					Glow.Flash(front, tg, Hot, h * (last ? 0.6f : 0.35f), 0.1f);
					Glow.Flash(back, tg, new Color(1f, 0.12f, 0.2f), h * (last ? 1.4f : 0.9f), 0.25f);
					Glow.Sparks(front, tg, last ? 24 : 12, 120f * u, 520f * u, 0.4f, new[] { Hot, Pink, Deep }, 0f, 0.008f * u, 0.025f * u, (tg - slot).Normalized(), 70f);
					if (foes.Count > 0) Flick(foes[k % foes.Count], front, new Color(1.8f, 1.5f, 1.5f), 0.22f);
					if (last) { HitStop.Request(0.04); Fx.Shake(1); }
				});
			});
		}
	}

	// ═══════════════════════════ 触媒（罕见能力） ═══════════════════════════

	/// <summary>
	/// 触媒约 1.2s：他手心聚起一点黑红光（光线往里收）；一握，咒毒长枪光（slash_thrust，咒怨黑红 LUT）分头扎向每个敌人；
	/// 扎中后每个敌人身上连着撕开两道黑红裂口（slash_tear）—— 第一道斜、第二道反斜更大，第二下敌人一暗、碎光往外喷。
	/// </summary>
	public static void Accelerant(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float fire = 0.32f;
		S("gx_whoosh", 0.14f, 0.02f);
		S("gx_slice", 0.2f, fire);
		Vector2 palm = c.Hand + new Vector2(f * h * 0.05f, -h * 0.04f), mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid + new Vector2(f * h * 0.8f, 0f), new Vector2(h * 4.6f, h * 2.4f), 1.3, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.8f, 0.2f)) * 0.7f);
		Gather(front, palm, h, u, fire, 14, 0.5f);
		Glow.Flash(back, palm, new Color(0.7f, 0.02f, 0.1f), h * 0.9f, fire + 0.1f);
		Fx.After(front, fire, () =>
		{
			Glow.Flash(front, palm, new Color(1f, 0.6f, 0.62f), h * 0.4f, 0.1f);
			Glow.Sparks(front, palm, 12, 100f * u, 400f * u, 0.4f, new[] { Pink, Deep, new Color(0.15f, 0f, 0.04f) }, 0f, 0.01f * u, 0.025f * u, new Vector2(f, 0f), 90f);
		});
		var foes = new List<SkillFx.Victim>(c.Enemies);
		for (int i = 0; i < foes.Count; i++)
		{
			var v = foes[i];
			bool first = i == 0;
			Vector2 tg = Mid(v);
			float travel = 0.09f + 0.02f * i, hit1 = fire + travel, hit2 = hit1 + 0.2f;
			float ew = Mathf.Max(v.Width, h * 0.5f);
			Thrust(back, palm, tg, travel, h * 0.22f, Lut5.Curse, fire, 0.03f, 0.22f);
			if (first) { S("gx_pop", 0.16f, hit1); S("gx_slice", 0.24f, hit2); }
			Painted(front, "slash_tear", tg + new Vector2(0f, -h * 0.05f), -0.5f * f, ew * 1.7f, Lut5.Blood, f, 0.05f, 0.08f, 0.28f, hit1, 1.4f, 0.75f);
			Painted(front, "slash_tear", tg + new Vector2(0f, h * 0.02f), 0.45f * f, ew * 2.2f, Lut5.Blood, -f, 0.05f, 0.12f, 0.34f, hit2, 1.5f, 0.9f);
			Fx.After(front, hit1, () =>
			{
				Glow.Flash(front, tg, new Color(1f, 0.7f, 0.72f), h * 0.3f, 0.08f);
				Flick(v, front, new Color(1.7f, 1.55f, 1.55f), 0.18f);
			});
			Fx.After(front, hit2, () =>
			{
				Glow.Flash(back, tg, new Color(0.75f, 0.02f, 0.12f), ew * 1.6f, 0.28f);
				Glow.Flash(front, tg, Hot, h * 0.35f, 0.08f);
				Glow.Sparks(front, tg, 20, 140f * u, 560f * u, 0.5f, new[] { Pink, Deep, new Color(0.12f, 0f, 0.03f) }, 60f * u, 0.01f * u, 0.03f * u, Vector2.Up, 200f);
				Flick(v, front, new Color(0.45f, 0.38f, 0.4f), 0.32f);
				if (first) { HitStop.Request(0.035); Fx.Shake(1); }
			});
		}
	}
}
