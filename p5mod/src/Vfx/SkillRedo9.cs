using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 增益牌第一批（2026-11-15 第三版）：预判、侧步、磨蚀、速行者、触媒。
/// 用户：不要一直复用刀光（刀光是攻击的画面）→ Codex 给增益牌生了专用贴图（art/p5/fx/v4/buff：荆棘、镜面裂纹、丝带、牌流、毒液、毒纹、光点）。
/// 用法照打击牌（KnifeFx.Hit）那套（第三版用户：「贴图感都太强了」——贴图停留太久、太大、单独一张摆着）：
/// · 每张贴图「一闪而过」：扫出 / 长出 ≤0.08s、停 ≤0.06s、0.2~0.3s 溶解，全程沿走向滑动；
/// · 一律过 P5 红黑 LUT（白芯红边，和刀光同一套颜色），不用贴图原色；
/// · 埋进别的东西里：落点空间扭曲（Glow.Ripple）、原版火星 / 尖刺粒子（Vanilla，换 LUT）、闪光；
/// · 要持续的感觉就多来几下短的（一波接一波），不让一张图停着。
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
		float delay = 0f, float glow = 1.3f, bool mirror = false, float drift = 0f)
	{
		Vector2 d = to - from;
		float sc = d.Length() / TexW(tex);
		return Paint.Sweep(layer, tex, from + d * 0.5f, d.Angle(), new Vector2(sc, sc * squash * (mirror ? -1f : 1f)), lut, reveal, hold, erode, drift, delay, glow);
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

	/// <summary>落点「埋」贴图用：空间扭曲 + 原版火星 / 尖刺粒子（P5 红 LUT）。</summary>
	private static void Embed(Control layer, Vector2 at, float h, float size, float rot = 0f, int specks = 18, float delay = 0f)
	{
		Glow.Ripple(layer, at, h * 1.3f * size, 0.4f, 0.03f, 1f, delay);
		Vanilla.Burst(layer, Vanilla.Specks, at, rot, size, Lut5.Blood, amount: specks, delay: delay);
	}

	/// <summary>
	/// 预判约 0.6s：面具上一点星光；他身前的空气里「叮」地被打出一道镜面裂纹（一闪就碎），裂纹那一下空间一扭、火星迸开；
	/// 紧接着整面看不见的镜子碎掉 —— 会折射背景的碎玻璃迸开、边飞边闪。
	/// </summary>
	public static void Anticipate(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float crack = 0.08f, burst = 0.24f;
		S("gx_ping", 0.16f, crack);
		S("gx_pop", 0.16f, burst);
		S("gx_slice", 0.12f, burst);
		Glow.Glint(front, c.Mask + new Vector2(f * h * 0.02f, 0f), 0.55f);
		Flare(front, c.Mask, h * 0.5f, h * 0.012f, new Color(1f, 0.9f, 0.9f), 0.16f);
		Vector2 at = c.Feet + new Vector2(f * h * 0.42f, -h * 0.6f);
		Fx.After(front, crack, () =>
		{
			Glow.Flash(front, at, Hot, h * 0.3f, 0.08f);
			Glow.Flash(back, at, new Color(1f, 0.1f, 0.18f), h * 1.0f, 0.25f);
		});
		Embed(front, at, h, 0.8f, 0f, 16, crack);
		Bloom(front, "mirror_crack_02", at, Fx.Rand(-0.3f, 0.3f), h * 1.1f, Lut5.Blood, crack, 0.06f, burst - crack - 0.06f, 0.16f, 0.75f, 1.6f, f);
		Fx.After(front, burst, () =>
		{
			HitStop.Request(0.03);
			Glow.Flash(front, at, Hot, h * 0.45f, 0.1f);
			Glow.Glint(front, at + new Vector2(0f, -h * 0.05f), 1f);
			Glass(front, at, 22, h * 0.2f, new Vector2(f, -0.2f), 300f, h * 2.2f, 0.8f);
			Glow.Ripple(front, at, h * 1.6f, 0.45f, 0.035f);
		});
	}

	// ═══════════════════════════ 侧步（罕见） ═══════════════════════════

	/// <summary>
	/// 侧步约 1s：他身上一扭（空间扭曲），两缕红色残光一高一低从身前往身后「唰」地扫过去、一闪即溶（像高速闪身拖出的轨迹），原版尖刺粒子往后甩；
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
		Vector2 body = c.Feet + new Vector2(0f, -h * 0.6f);
		Glow.Ripple(back, body, h * 1.4f, 0.4f, 0.03f);
		Vector2 lowA = c.Feet + new Vector2(f * h * 0.3f, -h * 0.45f), lowB = c.Feet + new Vector2(-f * h * 1.1f, -h * 0.6f);
		Vector2 hiA = c.Feet + new Vector2(f * h * 0.15f, -h * 0.85f), hiB = c.Feet + new Vector2(-f * h * 0.85f, -h * 0.98f);
		Lay(back, "ribbon_02", lowA, lowB, 0.5f, Lut5.Blood, 0.08f, 0.04f, 0.24f, 0f, 1.4f, f < 0f, 60f * u);
		Lay(back, "ribbon_01", hiA, hiB, 0.42f, Lut5.Blood, 0.07f, 0.03f, 0.22f, 0.05f, 1.4f, f > 0f, 50f * u);
		Vanilla.Burst(front, Vanilla.Specks, body, f > 0f ? Mathf.Pi : 0f, 0.9f, Lut5.Blood, amount: 16);

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
	/// 一根荆棘藤：根在 root，从 ang0 一边「甩」到 ang1 一边长出去（贴图左端是根，沿走向扫出），长满后继续顺着同一方向慢慢转（spin 弧度 / 秒），
	/// 停 hold 后从末梢往回带噪声溶解。不是一下子整根冒出来。
	/// </summary>
	private static void Vine(Control layer, string tex, Vector2 root, float ang0, float ang1, float len, float squash, float delay, float grow, float hold, float fade,
		float spin, bool mirror)
	{
		if (Fx.Tex(tex) is not Texture2D t) return;
		float tw = t.GetWidth(), th = t.GetHeight(), sc = len / tw;
		Paint.Stroke? st = Paint.Make(layer, tex, root, ang0, new Vector2(sc, sc * squash * (mirror ? -1f : 1f)), Lut5.Blood, 0.55f, 1.06f);
		if (st == null) return;
		foreach (Node n in st.Root.GetChildren())
			if (n is Sprite2D sp) { sp.Centered = false; sp.Offset = new Vector2(0f, -th * 0.5f); }
		st.Set("glow", 1.45f);
		st.Set("reveal", 0f);
		st.Root.Visible = false;
		float total = delay + grow + hold + fade;
		Fx.Animate(st.Root, 0, total, k =>
		{
			float s = k * total - delay;
			if (s < 0f) return;
			st.Root.Visible = true;
			float g = Ease5.OutQuint(Mathf.Min(1f, s / grow));
			st.Set("reveal", g * 1.12f);
			st.Root.Rotation = Mathf.Lerp(ang0, ang1, Fx.OutCubic(Mathf.Min(1f, s / (grow * 1.6f)))) + spin * Mathf.Max(0f, s - grow);
			// 长的时候整根从细变粗一点（像抽出来），溶解时稍微再伸长
			float e = Mathf.Clamp((s - grow - hold) / fade, 0f, 1f);
			st.Root.Scale = new Vector2(sc * (1f + 0.08f * e), sc * squash * (mirror ? -1f : 1f) * Mathf.Lerp(0.6f, 1f, g));
			st.Set("erode", e <= 0f ? 0f : Mathf.Lerp(0.05f, 1.15f, Mathf.Pow(e, 0.8f)));
		});
		Fx.FreeAfter(st.Root, total + 0.05);
	}

	/// <summary>
	/// 磨蚀约 1.4s：四周暗下来、光线往他身上收；主拍时荆棘从他身上「甩」出来 —— 每根一边绕着他旋开一边长出去（像拧开的一圈），
	/// 长满后整圈继续慢慢转；身后的荆棘丛也是边转边放大刺出来；空间一扭、火星迸开、重震。紧跟着反方向再旋出一圈短的，
	/// 两圈交错着转一会儿，从末梢溶掉，光点往上飘。
	/// </summary>
	public static void Abrasive(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float beat = 0.36f, wave2 = 0.5f;
		S("gx_whoosh", 0.16f, 0.05f);
		S("gx_pop", 0.24f, beat);
		S("gx_whoosh", 0.2f, beat + 0.02f);
		S("gx_slice", 0.14f, wave2);
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f), hub = c.Chest + new Vector2(0f, h * 0.04f);
		Dim(back, mid, new Vector2(h * 3.2f, h * 2.6f), 1.5, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.75f);
		Gather(front, hub, h, u, beat, 20, 0.75f);
		Glow.Flash(back, mid, new Color(1f, 0.1f, 0.18f), h * 1.4f, 0.4f, beat - 0.2f);

		// 身后的荆棘丛：边转边从中心放大刺出，停一下溶掉
		Paint.Stroke? burst = Bloom(back, "thorn_burst", hub, 0f, h * 1.6f, Lut5.Blood, beat, 0.12f, 0.35f, 0.3f, 0.3f, 1.4f, f);
		if (burst != null)
		{
			float r0 = Fx.Rand(-0.2f, 0.2f);
			Fx.Animate(burst.Root, beat, 0.8, t => burst.Root.Rotation = r0 - f * (0.9f * (1f - Mathf.Pow(1f - Mathf.Min(1f, t * 2.5f), 3f)) + 0.35f * t));
		}
		string[] vines = { "thorn_vine_01", "thorn_vine_02", "thorn_vine_03" };
		// 第一圈：6 根，绕着他顺时针（按朝向）旋开
		for (int i = 0; i < 6; i++)
		{
			float a1 = -Mathf.Pi * 0.5f + Mathf.Tau * i / 6f + 0.2f;
			float a0 = a1 + f * 1.4f;   // 从「后面」甩过来
			Vine(back, vines[i % 3], hub, a0, a1, h * (i % 2 == 0 ? 1.05f : 0.82f), 0.5f, beat - 0.02f + 0.015f * i, 0.16f, 0.38f, 0.32f, -f * 0.5f, i % 2 == 1);
		}
		// 第二圈：5 根短的，反方向旋开，夹在第一圈的空隙里
		for (int i = 0; i < 5; i++)
		{
			float a1 = -Mathf.Pi * 0.5f + Mathf.Tau * (i + 0.5f) / 5f;
			float a0 = a1 - f * 1.2f;
			Vine(front, vines[(i + 1) % 3], hub, a0, a1, h * 0.62f, 0.42f, wave2 + 0.015f * i, 0.14f, 0.3f, 0.3f, f * 0.6f, i % 2 == 0);
		}
		Fx.After(front, beat, () =>
		{
			HitStop.Request(0.05);
			Fx.Shake(2);
			Glow.Flash(front, hub, Hot, h * 0.45f, 0.12f);
			Glow.Flash(back, mid, new Color(1f, 0.12f, 0.2f), h * 2.4f, 0.45f);
			Glow.Sparks(front, hub, 22, 160f * u, 700f * u, 0.6f, new[] { Hot, Pink, Deep }, -40f * u, 0.008f * u, 0.03f * u, Vector2.Up, 180f);
		});
		Embed(front, hub, h, 1.4f, 0f, 36, beat);
		Embed(front, hub, h, 0.9f, 0f, 18, wave2);
		Motes(front, mid, new Vector2(h * 0.6f, h * 0.5f), h, 12, wave2 + 0.3f, 0.8f);
	}

	// ═══════════════════════════ 速行者（罕见能力） ═══════════════════════════

	/// <summary>
	/// 速行者约 1.5s「牌即子弹」：他肩后浮现一个左轮弹巢（ammo_cylinder，转着、放大着现身）；六张预告信从指间一张接一张弹出，
	/// 划着弧线飞向弹巢、边飞边缩，「咔」地塞进正上方的弹膛变成一发子弹（弹膛一亮），弹巢随即转一格；六发装满后弹巢飞快地转两圈、
	/// 「锵」地锁死（闪光、星光、空间一扭），然后淡进他身后。意思是：之后每抽一张牌，就是一发。
	/// </summary>
	public static void Speedster(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = SkillRedo.CardW(c);
		Control back = c.Back ?? c.Front, front = c.Front;
		const int n = 6;
		const float appear = 0.2f, first = 0.38f, gap = 0.1f, spinDur = 0.26f, fadeDur = 0.3f;
		float lockAt = first + gap * (n - 1) + 0.1f, endAt = lockAt + spinDur;
		float size = h * 0.62f, step = Mathf.Tau / n * f;
		Vector2 at = c.Feet + new Vector2(-f * h * 0.42f, -h * 1.08f);
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.6f);
		Dim(back, mid, new Vector2(h * 3.2f, h * 2.8f), endAt + fadeDur + 0.2f, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.12f)) * (1f - Ease5.Seg(s, 0.8f, 0.2f)) * 0.65f);
		S("gx_whoosh", 0.14f, 0.02f);
		S("gx_fan_open", 0.14f, first - 0.2f);
		Node2D root = Fx.Root(back, at, "P5Cylinder", endAt + fadeDur + 0.1);
		Sprite2D halo = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(1f, 0.15f, 0.22f, 0f), Scale = Vector2.One * size / 64f * 1.5f };
		root.AddChild(halo);
		Node2D wheel = new();
		root.AddChild(wheel);
		if (Fx.Tex("ammo_cylinder") is Texture2D cyl) wheel.AddChild(new Sprite2D { Texture = cyl, Scale = Vector2.One * size / cyl.GetWidth() });
		Texture2D? roundTex = Fx.Tex("ammo_round");
		float chamberR = size * 0.275f, roundD = size * 0.2f;
		var rounds = new Sprite2D?[n];
		for (int i = 0; i < n; i++)
		{
			// 第 i 发装填时弹巢已经转了 i 格，所以它的弹膛在本地 -i 格的位置（装的时候正好在正上方）
			Node2D slot = new() { Position = Vector2.Up.Rotated(-step * i) * chamberR };
			wheel.AddChild(slot);
			if (roundTex != null)
			{
				rounds[i] = new Sprite2D { Texture = roundTex, Scale = Vector2.One * roundD / (roundTex.GetWidth() * 0.64f), Visible = false };
				slot.AddChild(rounds[i]);
			}
		}
		float turn = 0f;
		// 现身：转着放大、淡入；装填时每格「咔」地转一下（OutBack）；锁死前飞快转两圈；最后淡出
		Fx.Animate(root, 0, endAt + fadeDur, t =>
		{
			float s = t * (endAt + fadeDur);
			float a = Fx.OutCubic(Mathf.Min(1f, s / appear));
			float o = 1f - Ease5.Seg(s, endAt + 0.05f, fadeDur - 0.05f);
			root.Modulate = new Color(1f, 1f, 1f, a * o);
			root.Scale = Vector2.One * (Mathf.Lerp(0.55f, 1f, a) * (1f + 0.12f * Ease5.Seg(s, endAt, fadeDur)));
			float intro = -f * 1.6f * (1f - a);
			float loaded = 0f;
			for (int i = 0; i < n; i++)
			{
				float l = first + gap * i;
				loaded += Ease5.OutBack(Mathf.Clamp((s - l - 0.01f) / 0.07f, 0f, 1f));
			}
			float spin = Fx.OutCubic(Ease5.Seg(s, lockAt, spinDur)) * Mathf.Tau * 2f * f;
			wheel.Rotation = intro + step * loaded + spin + turn;
			halo.Modulate = new Color(1f, 0.15f, 0.22f, (0.25f * a + 0.6f * Mathf.Exp(-Mathf.Pow((s - endAt) / 0.06f, 2f))) * o);
		});
		Fx.After(back, appear * 0.6f, () => Glow.Glint(back, at + new Vector2(size * 0.3f, -size * 0.3f), 0.6f));
		Vector2 top = at + Vector2.Up * chamberR;
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float l = first + gap * k, fly = 0.22f, d = l - fly;
			Vector2 from = c.Hand + new Vector2(f * h * 0.04f, -h * 0.04f);
			Vector2 ctrl = from.Lerp(top, 0.5f) + new Vector2(f * h * (0.35f - 0.05f * k), -h * (0.45f + 0.04f * k));
			Vector2 Path(float t) => from.Lerp(ctrl, t).Lerp(ctrl.Lerp(top, t), t);
			Control layer = k % 2 == 0 ? front : back;
			Phantom.P5Card card = Phantom.Card(layer, from, w * 1.25f, l + 0.05, 0f);
			card.Root.Visible = false;
			Fx.Animate(card.Root, d, fly, t =>
			{
				card.Root.Visible = true;
				float e = Ease5.OutQuint(t) * 0.35f + Fx.InCubic(t) * 0.65f;
				Vector2 p = Path(e), q = Path(Mathf.Min(1f, e + 0.03f));
				card.Place(p, (q - p).Angle() + Mathf.Pi * 0.5f, e * Mathf.Tau * 2.5f, Mathf.Lerp(1f, roundD / (w * 1.25f) * 1.2f, Fx.InCubic(t)));
				card.Set("glow", Mathf.Max(0f, e - 0.6f) * 2.2f);
			});
			Streak(layer, Path, d, fly, h * 0.55f, h * 0.045f, k % 3 == 1 ? new Color(1f, 0.9f, 0.9f) : Red, 1.25f);
			S("gx_tick", 0.16f, l);
			Fx.After(back, l, () =>
			{
				card.Root.Visible = false;
				if (rounds[k] is Sprite2D r)
				{
					r.Visible = true;
					Fx.Animate(r, 0, 0.12, t => r.Modulate = new Color(1f + 2f * (1f - t), 1f + 1.6f * (1f - t), 1f + 1.6f * (1f - t), 1f));
				}
				Glow.Flash(back, top, Hot, roundD * 2.2f, 0.1f);
				Glow.Sparks(back, top, 8, 60f * u, 260f * u, 0.3f, new[] { Hot, Pink, Deep }, 0f, 0.006f * u, 0.016f * u, Vector2.Up, 120f);
			});
		}
		S("gx_whoosh", 0.16f, lockAt);
		S("gx_tock", 0.22f, endAt);
		S("gx_ping", 0.14f, endAt + 0.02f);
		Fx.After(back, endAt, () =>
		{
			HitStop.Request(0.04);
			Fx.Shake(1);
			Glow.Flash(back, at, Hot, size * 0.8f, 0.12f);
			Glow.Flash(back, at, new Color(1f, 0.12f, 0.2f), size * 2.6f, 0.35f);
			Glow.Glint(front, at + new Vector2(size * 0.32f, -size * 0.34f), 1.1f);
			Glow.Ripple(back, at, size * 2.2f, 0.4f, 0.035f);
			Vanilla.Burst(back, Vanilla.Specks, at, 0f, 1f, Lut5.Blood, amount: 24);
		});
		Motes(back, at, new Vector2(size * 0.35f, size * 0.25f), h, 10, endAt + 0.05f, 0.7f, 0.35f);
	}

	// ═══════════════════════════ 触媒（罕见能力） ═══════════════════════════

	/// <summary>小毒瓶：圆底长颈的玻璃瓶（折射背后的画面、亮边、一道高光），里面黑红毒液会晃（slosh）、冒泡（heat 越高泡越多越快、越亮），level = 液面高度。</summary>
	private static Shader? _vial;
	private static Shader VialShader => _vial ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float clock = 0.0;
uniform float heat = 0.0;
uniform float level = 0.15;
uniform float slosh = 0.0;
uniform float tilt = 0.0;
uniform float alpha = 1.0;
uniform float aspect = 0.7;
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float sdBox(vec2 p, vec2 b) { vec2 d = abs(p) - b; return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0); }
float smin(float a, float b, float k) { float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0); return mix(b, a, h) - k * h * (1.0 - h); }
float shape(vec2 p) {
	float body = length(p - vec2(0.0, 0.38)) - 0.55;
	float neck = sdBox(p - vec2(0.0, -0.42), vec2(0.15, 0.36));
	float lip = sdBox(p - vec2(0.0, -0.8), vec2(0.21, 0.05)) - 0.02;
	return min(smin(body, neck, 0.12), lip);
}
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	p.x *= aspect;
	float d = shape(p);
	float aa = fwidth(d) * 1.2;
	float inside = 1.0 - smoothstep(-aa, aa, d);
	if (inside <= 0.0) discard;
	// 液面：level 是相对瓶身（-1 顶 → 1 底）的高度；晃动 = 斜一下 + 波浪
	float surf = level + p.x * tilt + sin(p.x * 7.0 + clock * 9.0) * 0.04 * slosh + sin(p.x * 13.0 - clock * 14.0) * 0.015 * (slosh + heat);
	float liquid = smoothstep(surf - 0.01, surf + 0.01, p.y) * step(d, -0.035);
	// 折射的背景（玻璃身）
	vec2 off = vec2(-p.x, p.y) * 0.012;
	vec3 bg = textureLod(screen_tex, SCREEN_UV + off, 0.0).rgb * vec3(0.92, 0.9, 0.95);
	vec3 col = bg;
	// 毒液：深红 → 绯红，越热越亮；底部更深
	float depth = clamp((p.y - surf) / 0.9, 0.0, 1.0);
	vec3 deep = vec3(0.16, 0.0, 0.03), hot = vec3(0.95, 0.06, 0.16);
	vec3 liq = mix(hot * 0.8, deep, 0.35 + depth * 0.6) * (0.8 + heat * 0.35);
	// 气泡：一格一格往上冒
	vec2 q = vec2(p.x * 6.0, p.y * 6.0 + clock * (2.0 + heat * 7.0));
	vec2 cell = floor(q), fr = fract(q) - 0.5;
	float rnd = h21(cell);
	float r = mix(0.06, 0.2, h21(cell + 7.0)) * step(1.0 - heat * 0.85 - 0.08, rnd);
	float bub = 1.0 - smoothstep(r - 0.04, r, length(fr - vec2(rnd - 0.5, 0.0) * 0.5));
	float ring = bub * smoothstep(r * 0.4, r, length(fr - vec2(rnd - 0.5, 0.0) * 0.5));
	liq += vec3(1.0, 0.75, 0.75) * ring * (0.5 + 0.6 * heat) + vec3(1.0, 0.3, 0.35) * bub * 0.2;
	// 液面一道亮线
	liq += vec3(1.0, 0.6, 0.6) * exp(-pow((p.y - surf) / 0.025, 2.0)) * (0.5 + heat);
	col = mix(col, liq, liquid * 0.92);
	// 玻璃：亮边（内外两道）+ 左侧一道竖高光 + 一点红色反光
	float rim = exp(-pow((d + 0.02) / 0.018, 2.0));
	col += vec3(1.0, 0.95, 0.95) * rim * 0.9;
	float spec = exp(-pow((p.x + 0.3 + p.y * 0.15) / 0.035, 2.0)) * smoothstep(-0.2, 0.3, p.y) * smoothstep(0.85, 0.5, p.y);
	col += vec3(1.0) * spec * 0.7;
	col += vec3(1.0, 0.1, 0.18) * exp(-pow((d + 0.05) / 0.03, 2.0)) * 0.25 * (0.5 + heat);
	COLOR = vec4(col, inside * alpha);
}" };

	/// <summary>
	/// 触媒约 1.3s「毒瓶沸腾」：他手里冒出一个小毒瓶（转着放大、星光一闪）；他晃两下 —— 瓶里的黑红毒液跟着晃、开始冒泡，越来越热、越来越亮，
	/// 背后的红光一下比一下大；第三下毒液「噗」地涌出瓶口：毒液往上喷、溢出来顺着瓶身往下淌、空间一扭、毒纹在瓶后一闪、轻震；然后瓶子淡掉，光点往上飘。
	/// 意思是：毒被催化了，更烈（额外触发）。
	/// </summary>
	public static void Accelerant(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float appear = 0.16f, shake1 = 0.24f, shake2 = 0.46f, boil = 0.7f, fadeAt = 1.05f, end = 1.3f;
		Vector2 at = c.Hand + new Vector2(f * h * 0.05f, -h * 0.17f), mid = c.Feet + new Vector2(0f, -h * 0.55f);
		float vw = h * 0.3f, vh = h * 0.42f;
		Dim(back, mid, new Vector2(h * 3f, h * 2.4f), end + 0.1f, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.75f, 0.25f)) * 0.7f);
		S("gx_card_slide", 0.12f, 0.02f);
		S("gx_whoosh", 0.12f, shake1);
		S("gx_whoosh", 0.14f, shake2);
		S("gx_pop", 0.24f, boil);
		front.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport, Name = "P5VialCopy" });
		Node2D root = Fx.Root(front, at, "P5Vial", end + 0.05);
		ShaderMaterial m = new() { Shader = VialShader };
		m.SetShaderParameter("aspect", vw / vh);
		ColorRect quad = new() { Size = new Vector2(vw, vh), Position = new Vector2(-vw * 0.5f, -vh * 0.62f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(quad);
		// 瓶后一团会跟着热度变大的红光
		Sprite2D halo = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(0.9f, 0.05f, 0.15f, 0f) };
		back.AddChild(halo);
		halo.GlobalPosition = at;
		Fx.FreeAfter(halo, end + 0.05);
		float Bump(float s, float c0, float wd) => Mathf.Exp(-Mathf.Pow((s - c0) / wd, 2f));
		Fx.Animate(root, 0, end, t =>
		{
			float s = t * end;
			float a = Fx.OutCubic(Mathf.Min(1f, s / appear)), o = 1f - Ease5.Seg(s, fadeAt, end - fadeAt);
			// 晃：两下左右摆（每下来回），第三下（沸腾）往上一顶
			float wob = Mathf.Sin(Mathf.Clamp((s - shake1) / 0.16f, 0f, 1f) * Mathf.Tau) * 0.38f + Mathf.Sin(Mathf.Clamp((s - shake2) / 0.16f, 0f, 1f) * Mathf.Tau) * 0.45f;
			root.Rotation = f * (wob - 0.6f * (1f - a)) ;
			float jolt = Bump(s, boil, 0.04f);
			root.Scale = new Vector2(1f - 0.08f * jolt, 1f + 0.1f * jolt) * Mathf.Lerp(0.5f, 1f, a);
			root.GlobalPosition = at + new Vector2(0f, -h * 0.04f * jolt);
			float heat = Mathf.Clamp(Ease5.Seg(s, shake1, boil - shake1) + 0.25f * Ease5.Seg(s, 0f, shake1), 0f, 1f);
			float slosh = Mathf.Min(1.4f, Mathf.Abs(wob) * 2.2f + heat * 0.6f);
			m.SetShaderParameter("clock", s);
			m.SetShaderParameter("heat", heat);
			m.SetShaderParameter("slosh", slosh);
			m.SetShaderParameter("tilt", -wob * 0.6f);
			m.SetShaderParameter("level", Mathf.Lerp(0.05f, -0.75f, Fx.OutCubic(Ease5.Seg(s, boil - 0.06f, 0.1f))) + 0.6f * Ease5.Seg(s, boil + 0.1f, 0.25f));
			m.SetShaderParameter("alpha", a * o);
			float pulse = 0.25f + 0.5f * heat + 0.5f * Bump(s, shake1 + 0.08f, 0.06f) + 0.7f * Bump(s, shake2 + 0.08f, 0.06f) + 1.2f * Bump(s, boil, 0.08f);
			halo.Modulate = new Color(0.9f, 0.05f, 0.15f, Mathf.Min(0.55f, pulse * 0.5f) * a * o);
			halo.Scale = Vector2.One * h * (0.6f + 0.4f * heat + 0.6f * Bump(s, boil, 0.1f)) / 64f;
		});
		Fx.After(front, appear * 0.7f, () => Glow.Glint(front, at + new Vector2(-f * vw * 0.25f, -vh * 0.35f), 0.6f));
		foreach (float sh in new[] { shake1, shake2 })
			Fx.After(front, sh + 0.08f, () => Glow.Sparks(front, at, 8, 60f * u, 240f * u, 0.35f, new[] { Pink, Deep }, 60f * u, 0.006f * u, 0.016f * u, Vector2.Up, 160f));
		Vector2 mouth = at + new Vector2(0f, -vh * 0.62f + vh * 0.06f);
		Fx.After(front, boil, () =>
		{
			HitStop.Request(0.035);
			Fx.Shake(1);
			Glow.Flash(front, mouth, Hot, h * 0.35f, 0.1f);
			Glow.Flash(back, at, new Color(0.85f, 0.03f, 0.14f), h * 1.8f, 0.35f);
			Glow.Ripple(front, at, h * 1.4f, 0.4f, 0.035f);
			Vanilla.Burst(front, Vanilla.Specks, mouth, -Mathf.Pi * 0.5f, 0.9f, Lut5.Blood, amount: 20);
			// 往上喷
			for (int j = 0; j < 9; j++)
			{
				Vector2 vel = Vector2.FromAngle(-Mathf.Pi * 0.5f + Fx.Rand(-0.6f, 0.6f)) * h * Fx.Rand(1.0f, 2.0f);
				float life = Fx.Rand(0.4f, 0.6f), sz = h * Fx.Rand(0.05f, 0.1f);
				int cell = (int)Fx.Rand(0f, 15.99f);
				Node2D r = Fx.Root(front, mouth, "P5Spurt", life + 0.05);
				if (Cell(r, "venom_drops_sheet", cell, sz, false) is not Sprite2D sp) continue;
				Fx.Animate(r, 0, life, t =>
				{
					float s = t * life;
					r.GlobalPosition = mouth + vel * s + new Vector2(0f, h * 4f * s * s);
					r.Rotation = (vel + new Vector2(0f, h * 8f * s)).Angle() - Mathf.Pi * 0.5f;
					sp.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.55f, 0.45f));
				});
			}
		});
		Bloom(back, "venom_veins_01", at, Fx.Rand(-0.6f, 0.6f), h * 1.3f, Lut5.Blood, boil, 0.05f, 0.05f, 0.24f, 0.55f, 1.6f, f);
		// 溢出来顺着瓶身往下淌：几滴从瓶口两侧滑下去，到瓶底再落下
		for (int j = 0; j < 4; j++)
		{
			float side = j % 2 == 0 ? -1f : 1f, d = boil + 0.04f + 0.06f * j, life = 0.45f;
			float sz = h * Fx.Rand(0.05f, 0.075f);
			int cell = 4 + j;
			Fx.After(front, d, () =>
			{
				Node2D r = Fx.Root(front, mouth, "P5Drip", life + 0.05);
				if (Cell(r, "venom_drops_sheet", cell, sz, false) is not Sprite2D sp) return;
				Fx.Animate(r, 0, life, t =>
				{
					// 沿瓶颈 → 瓶身外沿（圆）→ 落下
					float y = Fx.InCubic(t);
					float xo = side * vw * (t < 0.35f ? 0.2f : 0.2f + 0.3f * Mathf.Sin(Mathf.Min(1f, (t - 0.35f) / 0.4f) * Mathf.Pi * 0.6f));
					r.GlobalPosition = mouth + new Vector2(xo, vh * 1.25f * y);
					r.Rotation = 0f;
					sp.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.75f, 0.25f));
				});
			});
		}
		Motes(front, at, new Vector2(h * 0.15f, h * 0.12f), h, 8, boil + 0.1f, 0.8f);
	}
}
