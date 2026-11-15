using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 增益牌第一批（2026-11-15 第三版）：预判、侧步、磨蚀、速行者、触媒。
/// 用户：不要一直复用刀光（刀光是攻击的画面）→ Codex 给增益牌生了专用贴图（art/p5/fx/v4/buff：荆棘、镜面裂纹、丝带、牌流、毒液、毒纹、光点）。
/// 用法照打击牌（KnifeFx.Hit）那套：贴图只当「材料」——沿走向扫出来、从最亮的笔触长出来（erode 倒放）、带噪声溶解、溶解边亮一圈、
/// 加法发光层、整体在动（扫、摆、飘、缩放），落点再配闪光 / 碎光 / 粒子；不整张「啪」地贴上去。
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

	private static float TexW(string tex) => Fx.Tex(tex) is Texture2D t ? t.GetWidth() : 1024f;

	/// <summary>
	/// 从最亮的笔触「长」出来（溶解倒放，长的那一圈边是亮的）、同时从 s0 放大到 1；停 hold；再带噪声溶解掉、微微再放大。
	/// pulse(秒) 乘在亮度上（毒纹跳两下用）。
	/// </summary>
	private static Paint.Stroke? Bloom(Control layer, string tex, Vector2 at, float rot, float width, Texture2D? lut, float delay, float grow, float hold,
		float fade, float s0 = 0.8f, float glow = 1.3f, float flipX = 1f, float squash = 1f, Func<float, float>? pulse = null)
	{
		float sc = width / TexW(tex);
		Paint.Stroke? st = Paint.Make(layer, tex, at, rot, new Vector2(sc * flipX, sc * squash), lut, 0.55f, 1.08f);
		if (st == null) return null;
		st.Set("glow", glow);
		st.Set("erode", 1.15f);
		st.Root.Visible = false;
		float total = delay + grow + hold + fade;
		Fx.Animate(st.Root, 0, total, t =>
		{
			float s = t * total - delay;
			if (s < 0f) return;
			st.Root.Visible = true;
			float g = Fx.OutCubic(Mathf.Min(1f, s / grow));
			float e = Mathf.Clamp((s - grow - hold) / fade, 0f, 1f);
			st.Set("erode", e > 0f ? Mathf.Lerp(0.05f, 1.15f, Mathf.Pow(e, 0.8f)) : Mathf.Lerp(1.15f, 0f, g));
			st.Root.Scale = new Vector2(sc * flipX, sc * squash) * Mathf.Lerp(s0, 1f, g) * (1f + 0.06f * e);
			if (pulse != null) st.Set("glow", glow * pulse(s));
		});
		Fx.FreeAfter(st.Root, total + 0.05);
		return st;
	}

	/// <summary>横图（荆棘 / 丝带 / 牌流）从 from 沿走向扫到 to（贴图左端在 from），reveal 秒扫满，停 hold，溶解 erode 秒。</summary>
	private static Paint.Stroke? Lay(Control layer, string tex, Vector2 from, Vector2 to, float squash, Texture2D? lut, float reveal, float hold, float erode,
		float delay = 0f, float glow = 1.3f, bool mirror = false)
	{
		Vector2 d = to - from;
		float sc = d.Length() / TexW(tex);
		return Paint.Sweep(layer, tex, from + d * 0.5f, d.Angle(), new Vector2(sc, sc * squash * (mirror ? -1f : 1f)), lut, reveal, hold, erode, 0f, delay, glow);
	}

	/// <summary>图集里的一格（4×4）。add = 加法混合（光点）。</summary>
	private static Sprite2D? Cell(Node parent, string tex, int idx, float size, bool add)
	{
		if (Fx.Tex(tex) is not Texture2D t) return null;
		float cw = t.GetWidth() / 4f;
		Sprite2D sp = new() { Texture = t, RegionEnabled = true, RegionRect = new Rect2(idx % 4 * cw, idx / 4 * cw, cw, cw), Scale = Vector2.One * size / cw };
		if (add) sp.Material = Fx.Additive;
		parent.AddChild(sp);
		return sp;
	}

	/// <summary>往上飘的光点（buff_motes_sheet）：在 at 附近 spread 范围内冒出 n 颗，飘 rise、边飘边转、淡出。</summary>
	private static void Motes(Control layer, Vector2 at, Vector2 spread, float h, int n, float delay, float life, float rise = 0.5f)
	{
		for (int i = 0; i < n; i++)
		{
			float d = delay + Fx.Rand(0f, life * 0.4f), l = life * Fx.Rand(0.6f, 1f);
			Vector2 p0 = at + new Vector2(Fx.Rand(-1f, 1f) * spread.X, Fx.Rand(-1f, 1f) * spread.Y);
			float sway = Fx.Rand(-0.08f, 0.08f) * h, up = h * rise * Fx.Rand(0.5f, 1f), sz = h * Fx.Rand(0.05f, 0.1f), ph = Fx.Rand(0f, 6f);
			Fx.After(layer, d, () =>
			{
				Node2D r = Fx.Root(layer, p0, "P5Mote", l + 0.05);
				if (Cell(r, "buff_motes_sheet", (int)Fx.Rand(0f, 15.99f), sz, true) is not Sprite2D sp) return;
				Fx.Animate(r, 0, l, t =>
				{
					r.GlobalPosition = p0 + new Vector2(Mathf.Sin(t * 4f + ph) * sway, -up * Fx.OutCubic(t));
					sp.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 6f) * (1f - t) * (0.7f + 0.3f * Mathf.Sin(t * 30f + ph)));
				});
			});
		}
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
	/// 预判约 0.85s：面具上一点星光；他身前的空气里「叮」地裂开一面看不见的镜子（镜面裂纹从击中点长出去，裂线亮白带红）；
	/// 停一瞬，整面镜子碎掉 —— 一把会折射背景的碎玻璃迸开、星光闪烁、几颗光点往上飘。
	/// </summary>
	public static void Anticipate(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float crack = 0.08f, burst = 0.36f;
		S("gx_ping", 0.16f, crack);
		S("gx_pop", 0.16f, burst);
		S("gx_slice", 0.12f, burst);
		Glow.Glint(front, c.Mask + new Vector2(f * h * 0.02f, 0f), 0.55f);
		Flare(front, c.Mask, h * 0.5f, h * 0.012f, new Color(1f, 0.9f, 0.9f), 0.16f);
		Vector2 at = c.Feet + new Vector2(f * h * 0.42f, -h * 0.6f);
		Fx.After(front, crack, () =>
		{
			Glow.Flash(front, at, Hot, h * 0.3f, 0.08f);
			Glow.Flash(back, at, new Color(1f, 0.1f, 0.18f), h * 1.2f, 0.3f);
			Glow.Sparks(front, at, 10, 80f * u, 300f * u, 0.3f, new[] { Hot, Pink }, 0f, 0.008f * u, 0.02f * u, Vector2.Up, 180f);
		});
		Bloom(front, "mirror_crack_02", at, Fx.Rand(-0.3f, 0.3f), h * 1.35f, null, crack, 0.12f, burst - crack - 0.12f, 0.1f, 0.7f, 2.4f, f);
		Bloom(back, "mirror_crack_01", at, Fx.Rand(1.2f, 1.9f), h * 1.1f, Lut5.Blood, crack + 0.03f, 0.12f, burst - crack - 0.15f, 0.1f, 0.6f, 1.8f, -f);
		Fx.After(front, burst, () =>
		{
			HitStop.Request(0.03);
			Glow.Flash(front, at, Hot, h * 0.5f, 0.1f);
			Glow.Glint(front, at + new Vector2(0f, -h * 0.05f), 1.1f);
			Glass(front, at, 22, h * 0.2f, new Vector2(f, -0.2f), 300f, h * 2.2f, 0.8f);
			Motes(front, at, new Vector2(h * 0.3f, h * 0.2f), h, 8, 0.02f, 0.7f);
		});
	}

	// ═══════════════════════════ 侧步（罕见） ═══════════════════════════

	/// <summary>
	/// 侧步约 1s：他身后两缕红丝带一高一低从身前往身后扫开、在空中飘一下再溶掉（闪身留下的残光），光点顺着飘；
	/// 同时一张预告信从指间弹到他头顶后方、翻着停住、翻正一亮，然后拉着光折进胸口收起。
	/// </summary>
	public static void Sidestep(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = SkillRedo.CardW(c);
		Control back = c.Back ?? c.Front, front = c.Front;
		S("gx_whoosh", 0.24f);
		S("gx_card_slide", 0.14f, 0.1f);
		S("gx_ping", 0.13f, 0.46f);
		S("gx_card_stack", 0.15f, 0.8f);
		Vector2 lowA = c.Feet + new Vector2(f * h * 0.35f, -h * 0.42f), lowB = c.Feet + new Vector2(-f * h * 1.35f, -h * 0.62f);
		Vector2 hiA = c.Feet + new Vector2(f * h * 0.2f, -h * 0.85f), hiB = c.Feet + new Vector2(-f * h * 1.0f, -h * 1.0f);
		foreach (var (st, k) in new[] { (Lay(back, "ribbon_02", lowA, lowB, 0.75f, null, 0.16f, 0.2f, 0.35f, 0f, 1.25f, f < 0f), 0), (Lay(back, "ribbon_01", hiA, hiB, 0.6f, null, 0.14f, 0.18f, 0.32f, 0.06f, 1.25f, f > 0f), 1) })
		{
			if (st == null) continue;
			st.Set("wobble", 0.02f);
			float r0 = st.Root.Rotation, ph = k * 1.7f;
			Fx.Animate(st.Root, 0, 0.75, t =>
			{
				st.Set("scroll", t * 1.4f);
				st.Root.Rotation = r0 + Mathf.Sin(t * 6f + ph) * 0.03f;
			});
		}
		Motes(back, c.Feet + new Vector2(-f * h * 0.55f, -h * 0.7f), new Vector2(h * 0.5f, h * 0.2f), h, 10, 0.1f, 0.8f, 0.35f);

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
			Glow.Flash(back, top, new Color(1f, 0.12f, 0.2f), h * 0.8f, 0.25f);
			Glow.Glint(front, top + new Vector2(f * w * 0.5f, -w * 0.6f), 0.7f);
		});
		Fx.After(front, end + 0.01f, () =>
		{
			card.Trail?.Kill();
			card.Root.Visible = false;
			Glow.Flash(front, c.Chest, Hot, h * 0.3f, 0.1f);
			Glow.Flash(back, c.Chest, new Color(1f, 0.12f, 0.2f), h * 1.1f, 0.3f);
			Motes(front, c.Chest, new Vector2(h * 0.12f, h * 0.1f), h, 6, 0f, 0.6f);
		});
	}

	// ═══════════════════════════ 磨蚀（稀有能力） ═══════════════════════════

	/// <summary>
	/// 磨蚀约 1.7s：四周暗下来、光线往他身上收；主拍时他身后「轰」地刺出一丛荆棘（从中心往外长），同时六根荆棘藤从他身上朝四面飞快地长出去、
	/// 尖刺一路亮起；重震、闪光。荆棘轻轻摆着停一会儿，然后从末梢溶解（溶解边一圈亮红），光点往上飘。
	/// </summary>
	public static void Abrasive(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float beat = 0.36f, hold = 0.62f;
		S("gx_whoosh", 0.16f, 0.05f);
		S("gx_pop", 0.24f, beat);
		S("gx_slice", 0.18f, beat + 0.03f);
		S("gx_ping", 0.12f, beat + 0.14f);
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid, new Vector2(h * 3.2f, h * 2.6f), 1.8, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.72f, 0.28f)) * 0.75f);
		Gather(front, c.Chest, h, u, beat, 20, 0.75f);
		Glow.Flash(back, mid, new Color(1f, 0.1f, 0.18f), h * 1.4f, 0.4f, beat - 0.2f);

		// 身后一丛荆棘：从中心刺出（长出来的时候放大），停住，最后溶掉
		Bloom(back, "thorn_burst", c.Chest + new Vector2(-f * h * 0.04f, -h * 0.02f), Fx.Rand(-0.2f, 0.2f), h * 1.75f, null, beat, 0.1f, hold + 0.05f, 0.4f, 0.45f, 1.35f, f);
		// 六根荆棘藤：从胸口朝四面长出去（上半多、下半少，长短错开），轻轻摆
		float[] angs = { -2.6f, -1.95f, -1.25f, -0.5f, 0.45f, 2.7f };
		string[] vines = { "thorn_vine_02", "thorn_vine_01", "thorn_vine_03", "thorn_vine_02", "thorn_vine_01", "thorn_vine_03" };
		for (int i = 0; i < angs.Length; i++)
		{
			Vector2 dir = new(Mathf.Cos(angs[i]) * f, Mathf.Sin(angs[i]));
			float len = h * (i % 2 == 0 ? 1.15f : 0.9f), d = beat - 0.02f + 0.03f * i;
			Vector2 a = c.Chest + dir * h * 0.06f;
			Paint.Stroke? st = Lay(back, vines[i], a, a + dir * len, 0.55f, null, 0.14f, hold - 0.03f * i, 0.4f, d, 1.4f, i % 2 == 1);
			if (st == null) continue;
			float r0 = st.Root.Rotation, ph = Fx.Rand(0f, 6f);
			Fx.Animate(st.Root, d, hold + 0.6f, t => st.Root.Rotation = r0 + Mathf.Sin(t * 7f + ph) * 0.025f * Mathf.Min(1f, t * 4f));
			Fx.After(front, d + 0.12f, () => Glow.Glint(front, a + dir * len * 0.92f, 0.55f));
		}
		Fx.After(front, beat, () =>
		{
			HitStop.Request(0.05);
			Fx.Shake(2);
			Glow.Flash(front, c.Chest, Hot, h * 0.45f, 0.12f);
			Glow.Flash(back, mid, new Color(1f, 0.12f, 0.2f), h * 2.4f, 0.45f);
			Glow.Sparks(front, c.Chest, 26, 160f * u, 700f * u, 0.6f, new[] { Hot, Pink, Deep }, -40f * u, 0.008f * u, 0.03f * u, Vector2.Up, 180f);
			Phantom.Shards(front, c.Chest, 14, 300f * u, 900f * u, Vector2.Up, 180f, 0.3f, 0f, 24f * u);
		});
		Motes(front, mid, new Vector2(h * 0.6f, h * 0.5f), h, 14, beat + hold * 0.6f, 0.9f);
	}

	// ═══════════════════════════ 速行者（罕见能力） ═══════════════════════════

	/// <summary>
	/// 速行者约 1.3s：手边五张预告信一张张弹起、翻正，排成一小把扇；然后一张接一张甩出去 —— 每张拉成一串飞散的牌流（card_stream）扫到一个敌人身上（轮流），
	/// 扫到处一下白闪、星光、碎光、纸屑，敌人一白；最后一张最大、定格一下、轻震。
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
		Vector2 fan = hand + new Vector2(f * h * 0.42f, h * 0.02f);
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float pop = 0.04f + 0.06f * k, go = 0.5f + 0.1f * k;
			bool last = k == n - 1;
			float a = (k - (n - 1) * 0.5f) * 0.36f;
			Vector2 slot = fan + new Vector2(Mathf.Sin(a) * f, -Mathf.Cos(a)) * h * 0.26f;
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
			S("gx_whoosh", last ? 0.22f : 0.13f, go);
			Fx.After(front, go, () =>
			{
				card.Root.Visible = false;
				Vector2 tg = foes.Count > 0 ? Mid(foes[k % foes.Count]) + new Vector2(0f, Fx.Rand(-0.12f, 0.08f) * h) : c.Chest + new Vector2(f * h * 1.8f, 0f);
				float travel = last ? 0.1f : 0.08f;
				Glow.Flash(front, slot, Hot, w * 1.6f, 0.1f);
				Lay(front, k % 2 == 0 ? "card_stream_01" : "card_stream_02", slot, tg + (tg - slot).Normalized() * w, last ? 0.7f : 0.55f, Lut5.Blood, travel, 0.04f, 0.22f, 0f, 1.3f, f < 0f);
				Fx.After(front, travel, () =>
				{
					Vector2 dn = (tg - slot).Normalized();
					Glow.Flash(front, tg, Hot, h * (last ? 0.6f : 0.35f), 0.1f);
					Glow.Flash(back, tg, new Color(1f, 0.12f, 0.2f), h * (last ? 1.4f : 0.9f), 0.25f);
					Glow.Glint(front, tg + new Vector2(0f, -h * 0.05f), last ? 1.1f : 0.7f);
					Glow.Sparks(front, tg, last ? 24 : 12, 120f * u, 520f * u, 0.4f, new[] { Hot, Pink, Deep }, 0f, 0.008f * u, 0.025f * u, dn, 70f);
					Phantom.Confetti(front, tg, last ? 22 : 10, 480f * u, dn, 70f, 0.7f, 8f * u);
					if (foes.Count > 0) Flick(foes[k % foes.Count], front, new Color(1.8f, 1.5f, 1.5f), 0.22f);
					if (last) { HitStop.Request(0.04); Fx.Shake(1); S("gx_slice", 0.2f); }
				});
			});
		}
	}

	// ═══════════════════════════ 触媒（罕见能力） ═══════════════════════════

	/// <summary>
	/// 触媒约 1.3s：他手心聚起一点黑红光；一弹，几颗黑红毒液划着弧飞向每个敌人；打中处毒纹从中心往外爬开（像毒在身体里扩散），
	/// 毒纹连着亮两下 —— 每亮一下溅起一圈毒液、敌人一闪，第二下更重、敌人一暗；然后毒纹溶掉。
	/// </summary>
	public static void Accelerant(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float fire = 0.3f;
		S("gx_whoosh", 0.14f, 0.02f);
		S("gx_card_slide", 0.14f, fire);
		Vector2 palm = c.Hand + new Vector2(f * h * 0.05f, -h * 0.04f), mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid + new Vector2(f * h * 0.8f, 0f), new Vector2(h * 4.6f, h * 2.4f), 1.4, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.8f, 0.2f)) * 0.7f);
		Gather(front, palm, h, u, fire, 14, 0.5f);
		Glow.Flash(back, palm, new Color(0.7f, 0.02f, 0.1f), h * 0.9f, fire + 0.1f);
		Fx.After(front, fire, () => Glow.Flash(front, palm, new Color(1f, 0.6f, 0.62f), h * 0.35f, 0.1f));
		var foes = new List<SkillFx.Victim>(c.Enemies);
		for (int i = 0; i < foes.Count; i++)
		{
			var v = foes[i];
			bool first = i == 0;
			Vector2 tg = Mid(v);
			float fly = 0.2f + 0.03f * i, hit1 = fire + fly, hit2 = hit1 + 0.24f;
			float ew = Mathf.Max(v.Width, h * 0.5f);
			// 毒液：三颗划着弧飞过去
			for (int j = 0; j < 3; j++)
			{
				float dj = fire + 0.03f * j, arc = h * Fx.Rand(0.25f, 0.45f);
				Vector2 aim = tg + new Vector2(Fx.Rand(-0.1f, 0.1f) * ew, Fx.Rand(-0.1f, 0.1f) * h);
				int cell = (int)Fx.Rand(0f, 7.99f);
				Fx.After(front, dj, () =>
				{
					Node2D r = Fx.Root(front, palm, "P5Venom", fly + 0.05);
					if (Cell(r, "venom_drops_sheet", cell, h * 0.2f, false) is not Sprite2D sp) return;
					Vector2 prev = palm;
					Fx.Animate(r, 0, fly, t =>
					{
						Vector2 p = palm.Lerp(aim, t) + new Vector2(0f, -arc * 4f * t * (1f - t));
						r.GlobalPosition = p;
						if ((p - prev).LengthSquared() > 1f) r.Rotation = (p - prev).Angle() - Mathf.Pi * 0.5f;
						prev = p;
					});
				});
			}
			float h1 = hit1, h2 = hit2;
			Func<float, float> pulse = s =>
			{
				float p1 = Mathf.Exp(-Mathf.Pow((s - 0.02f) / 0.06f, 2f)), p2 = Mathf.Exp(-Mathf.Pow((s - (h2 - h1)) / 0.07f, 2f));
				return 1f + 0.8f * p1 + 1.4f * p2;
			};
			Bloom(front, i % 2 == 0 ? "venom_veins_01" : "venom_veins_02", tg, Fx.Rand(-0.5f, 0.5f), ew * 2.0f, null, hit1, 0.14f, 0.36f, 0.35f, 0.55f, 1.35f, f, 1f, pulse);
			if (first) { S("gx_pop", 0.16f, hit1); S("gx_pop", 0.22f, hit2); }
			foreach (float when in new[] { hit1, hit2 })
			{
				bool heavy = when == hit2;
				Fx.After(front, when, () =>
				{
					Glow.Flash(front, tg, heavy ? Hot : new Color(1f, 0.7f, 0.72f), h * (heavy ? 0.4f : 0.28f), 0.08f);
					if (heavy) Glow.Flash(back, tg, new Color(0.75f, 0.02f, 0.12f), ew * 1.6f, 0.28f);
					for (int j = 0; j < (heavy ? 9 : 5); j++)
					{
						Vector2 vel = Vector2.FromAngle(Fx.Rand(-Mathf.Pi * 0.9f, -Mathf.Pi * 0.1f)) * h * Fx.Rand(0.9f, 1.8f);
						float life = Fx.Rand(0.35f, 0.55f), sz = h * Fx.Rand(0.07f, 0.13f);
						int cell = 4 + (int)Fx.Rand(0f, 11.99f);
						Node2D r = Fx.Root(front, tg, "P5Splash", life + 0.05);
						if (Cell(r, "venom_drops_sheet", cell, sz, false) is not Sprite2D sp) continue;
						Fx.Animate(r, 0, life, t =>
						{
							float s = t * life;
							r.GlobalPosition = tg + vel * s + new Vector2(0f, h * 3f * s * s);
							r.Rotation = (vel + new Vector2(0f, h * 6f * s)).Angle() - Mathf.Pi * 0.5f;
							sp.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.6f, 0.4f));
						});
					}
					Flick(v, front, heavy ? new Color(0.45f, 0.38f, 0.4f) : new Color(1.7f, 1.55f, 1.55f), heavy ? 0.32f : 0.18f);
					if (heavy && first) { HitStop.Request(0.035); Fx.Shake(1); }
				});
			}
		}
	}
}
