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
				float g = speed * 0.25f, tumble = Fx.Rand(10f, 22f) * (Fx.Rand(0f, 1f) < 0.5f ? -1f : 1f);
				Fx.Animate(sp, 0, life, t =>
				{
					float s = t * life;
					float k = 1f - Mathf.Exp(-s * 7f);
					// 往外迸、很快被空气拖住（不直直往下掉），边飞边翻（横向缩放 = 绕竖轴翻转），越来越小、化成一点星光消失
					sp.Position = v * k / 7f * 2.4f + new Vector2(0f, g * s * s * 0.5f);
					sp.Rotation = r0 + spin * k;
					float shrink = 1f - Fx.InCubic(Ease5.Seg(t, 0.35f, 0.65f));
					sp.Scale = new Vector2(Mathf.Cos(s * tumble + ph), 1f) * sc * Mathf.Lerp(0.5f, 1f, Mathf.Min(1f, t * 8f)) * shrink;
					m.SetShaderParameter("opacity", Mathf.Min(1f, t * 10f) * (1f - Ease5.Seg(t, 0.75f, 0.25f)));
					m.SetShaderParameter("glint", Mathf.Pow(Mathf.Abs(Mathf.Cos(s * tumble + ph)), 16f) + Fx.InCubic(Ease5.Seg(t, 0.4f, 0.5f)) * 0.8f);
				});
				if (i % 3 == 0)
				{
					Vector2 end = at + v / 7f * 2.4f * 0.92f;
					Glow.Glint(layer, end, Fx.Rand(0.3f, 0.5f), life * Fx.Rand(0.55f, 0.8f));
				}
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
			Glass(front, at, 18, h * 0.15f, new Vector2(f, -0.2f), 360f, h * 1.8f, 0.6f);
			Motes(front, at, new Vector2(h * 0.25f, h * 0.2f), h, 8, 0.15f, 0.5f, 0.25f);
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
		Paint.Stroke? burst = Bloom(back, "thorn_burst", hub, 0f, h * 1.3f, Lut5.Blood, beat, 0.1f, 0.2f, 0.26f, 0.3f, 1.3f, f);
		if (burst != null)
		{
			float r0 = Fx.Rand(-0.2f, 0.2f);
			Fx.Animate(burst.Root, beat, 0.8, t => burst.Root.Rotation = r0 - f * (0.9f * (1f - Mathf.Pow(1f - Mathf.Min(1f, t * 2.5f), 3f)) + 0.35f * t));
		}
		string[] vines = { "thorn_vine_01", "thorn_vine_02", "thorn_vine_03" };
		// 第一圈：6 根，绕着他顺时针（按朝向）旋开
		for (int i = 0; i < 5; i++)
		{
			float a1 = -Mathf.Pi * 0.5f + Mathf.Tau * i / 5f + 0.2f;
			float a0 = a1 + f * 1.4f;   // 从「后面」甩过来
			Vine(back, vines[i % 3], hub, a0, a1, h * (i % 2 == 0 ? 1.05f : 0.82f), 0.5f, beat - 0.02f + 0.015f * i, 0.16f, 0.38f, 0.32f, -f * 0.5f, i % 2 == 1);
		}
		// 第二圈：5 根短的，反方向旋开，夹在第一圈的空隙里
		for (int i = 0; i < 0; i++)   // 用户：荆棘太多 → 去掉第二圈
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
	/// 速行者约 1.4s「节奏加速」：不用道具，只靠节奏 —— 他身上一下一下爆开红白闪光，每下一道贴着身体划过的短疾光；
	/// 间隔越来越短（嗒……嗒…嗒.嗒嗒嗒），最后几下密到连成一片，疾光贴着他身上来回刷；最后一下最亮（十字光、空间一扭、轻震），
	/// 余下的电光在他手边噼啪闪几下才散。意思是「快」，而且每一下都是一击。
	/// </summary>
	public static void Speedster(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		float[] beats = { 0.08f, 0.36f, 0.56f, 0.7f, 0.8f, 0.87f, 0.92f, 0.96f, 0.99f, 1.02f };
		const float climax = 1.08f;
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid, new Vector2(h * 3f, h * 2.6f), climax + 0.5f, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.8f, 0.2f)) * 0.7f);
		for (int i = 0; i < beats.Length; i++)
		{
			float b = beats[i], k = i / (float)(beats.Length - 1);   // 0 → 1：越往后越亮、越密
			bool left = i % 2 == 0;
			S("gx_tick", 0.1f + 0.1f * k, b);
			// 落在身上的一点（胸口到大腿之间，左右错开）
			Vector2 at = c.Feet + new Vector2(f * h * Fx.Rand(-0.9f, 1.3f), -h * Fx.Rand(0.2f, 1.35f));   // 用户：不用紧贴人物 → 散在他周围的空中
			float ang = (left ? 0f : Mathf.Pi) + Fx.Rand(-0.45f, 0.45f);
			Vector2 dir = Vector2.FromAngle(ang);
			Vector2 a0 = at - dir * h * 0.4f, b0 = at + dir * h * 0.4f;
			Fx.After(front, b, () =>
			{
				Glow.Flash(front, at, Hot, h * (0.3f + 0.25f * k), 0.08f);
				Glow.Flash(back, at, new Color(1f, 0.12f, 0.2f), h * (0.9f + 0.8f * k), 0.2f);
				Flare(front, at, h * (0.5f + 0.4f * k), h * (0.012f + 0.01f * k), new Color(1f, 0.92f, 0.92f), 0.1f, ang);
				// 他整个人跟着节拍闪一下（越往后越亮）
				Glow.Sparks(front, at, 6 + (int)(8 * k), 80f * u, 380f * u, 0.3f, new[] { Hot, Pink, Deep }, 0f, 0.006f * u, 0.018f * u, dir, 70f);
			});
			Streak(front, t => a0.Lerp(b0, t), b, 0.12f, h * (0.5f + 0.3f * k), h * (0.06f + 0.05f * k), i % 3 == 1 ? new Color(1f, 0.9f, 0.9f) : Red, 1.35f);
		}
		// 连成一片：最后 0.2s 疾光贴着他身上来回刷（前后两层）
		for (int j = 0; j < 9; j++)
		{
			float d = 0.86f + 0.025f * j;
			float y = -h * Fx.Rand(0.15f, 1.4f), dirX = j % 2 == 0 ? 1f : -1f, cx = f * h * Fx.Rand(-0.5f, 0.9f);
			Vector2 a0 = c.Feet + new Vector2(cx - dirX * h * 0.5f, y + Fx.Rand(-0.08f, 0.08f) * h), b0 = c.Feet + new Vector2(cx + dirX * h * 0.5f, y - Fx.Rand(-0.08f, 0.08f) * h);
			Streak(j % 2 == 0 ? front : back, t => a0.Lerp(b0, t), d, 0.1f, h * 0.55f, h * 0.07f, j % 3 == 0 ? new Color(1f, 0.92f, 0.92f) : Red, 1.4f);
		}
		S("gx_slice", 0.22f, climax);
		S("gx_pop", 0.16f, climax);
		Fx.After(front, climax, () =>
		{
			HitStop.Request(0.04);
			Fx.Shake(1);
			Glow.Flash(front, c.Chest, Hot, h * 0.5f, 0.1f);
			Glow.Flash(back, mid, new Color(1f, 0.12f, 0.2f), h * 2.2f, 0.4f);
			Cross(back, c.Chest, h * 1.0f);
			Glow.Sparks(front, c.Chest, 26, 160f * u, 700f * u, 0.55f, new[] { Hot, Pink, Deep }, -30f * u, 0.008f * u, 0.028f * u, Vector2.Up, 180f);
		});
		Embed(front, c.Chest, h, 1.2f, 0f, 28, climax);
		// 余下的电光：手边噼啪闪几下
		for (int j = 0; j < 7; j++)
		{
			float d = climax + 0.08f + 0.06f * j + Fx.Rand(0f, 0.03f);
			Vector2 p = c.Hand + new Vector2(Fx.Rand(-0.12f, 0.12f) * h, Fx.Rand(-0.15f, 0.08f) * h);
			Vector2 q = p + Vector2.FromAngle(Fx.Rand(0f, Mathf.Tau)) * h * 0.1f;
			Fx.After(front, d, () =>
			{
				Glow.Glint(front, p, 0.35f);
				Glow.Sparks(front, p, 4, 40f * u, 180f * u, 0.22f, new[] { Hot, Pink }, 0f, 0.005f * u, 0.012f * u, Vector2.Up, 180f);
			});
			Streak(front, t => p.Lerp(q, t), d, 0.06f, h * 0.1f, h * 0.012f, new Color(1f, 0.9f, 0.9f), 1.5f);
		}
	}

	// ═══════════════════════════ 触媒（罕见能力） ═══════════════════════════

	/// <summary>
	/// 一股咒怨：沿着绕他身体的螺旋线往上涌的一条黑红光流（头先冲上去、尾巴追着收），绕到身后的那段画在人物后面、绕到身前的画在前面；
	/// 三层：外面一层红色辉光（加法）、中间墨黑偏暗红的身子、里面一道粉白的亮芯。turns 圈数，phase 起始角，height 冲多高。
	/// </summary>
	private static void CurseStream(Control front, Control back, Vector2 feet, float h, float rx, float height, float turns, float phase, float dir, float width,
		float delay, float rise, float trail, float bright)
	{
		const int n = 40;
		float total = delay + rise + trail;
		Node2D rb = Fx.Root(back, Vector2.Zero, "P5CurseB", total + 0.05), rf = Fx.Root(front, Vector2.Zero, "P5CurseF", total + 0.05);
		rb.GlobalPosition = Vector2.Zero;
		rf.GlobalPosition = Vector2.Zero;
		Curve taper = new();
		taper.AddPoint(new Vector2(0f, 0.15f));
		taper.AddPoint(new Vector2(0.55f, 1f));
		taper.AddPoint(new Vector2(1f, 0.35f));
		Line2D Mk(Node parent, float w, Color col, bool add)
		{
			Line2D l = new() { Width = w, DefaultColor = col, WidthCurve = taper, Antialiased = true, JointMode = Line2D.LineJointMode.Round,
				BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round };
			if (add) l.Material = Fx.Additive;
			l.SetMeta("col", col);
			parent.AddChild(l);
			return l;
		}
		var layers = new[] { rb, rf };
		var lines = new List<(Line2D L, bool Front)>();
		foreach (Node2D r in layers)
		{
			bool fr = r == rf;
			lines.Add((Mk(r, width * 1.9f, new Color(0.9f, 0.04f, 0.14f, 0.32f * bright), true), fr));
			lines.Add((Mk(r, width, new Color(0.16f, 0f, 0.03f, 0.92f), false), fr));
			lines.Add((Mk(r, width * 0.18f, new Color(1f, 0.5f, 0.52f, 0.85f * bright), true), fr));
		}
		Vector2 P(float t)
		{
			float a = phase + dir * t * turns * Mathf.Tau;
			float r = rx * (1f - 0.55f * t * t);                    // 越往上越收
			return feet + new Vector2(Mathf.Sin(a) * r, -t * height + Mathf.Cos(a) * r * 0.18f);
		}
		bool InFront(float t) => Mathf.Cos(phase + dir * t * turns * Mathf.Tau) > 0f;
		Fx.Animate(rb, 0, total, k =>
		{
			float s = k * total - delay;
			if (s < 0f) return;
			float head = Fx.OutCubic(Mathf.Min(1f, s / rise));
			float tail = Fx.InCubic(Mathf.Clamp((s - rise * 0.35f) / (rise * 0.65f + trail), 0f, 1f));
			var pts = new Vector2[n];
			float[] offs = new float[n];
			bool[] isFront = new bool[n];
			for (int i = 0; i < n; i++)
			{
				float u = i / (n - 1f), t = Mathf.Lerp(tail, head, u);
				pts[i] = P(t) + new Vector2(Mathf.Sin(t * 23f + s * 9f) * width * 0.25f, 0f);
				isFront[i] = InFront(t);
				offs[i] = u;
			}
			float fade = 1f - Ease5.Seg(s, rise + trail * 0.5f, trail * 0.5f);
			foreach (var (l, fr) in lines)
			{
				// Line2D 有 Gradient 时不用 DefaultColor → 每条线用自己的颜色，按前后把不该出现的那半段 alpha 置 0
				Color baseCol = (Color)l.GetMeta("col");
				Color[] cs = new Color[n];
				for (int i = 0; i < n; i++) cs[i] = new Color(baseCol, isFront[i] == fr ? baseCol.A : 0f);
				l.Points = pts;
				l.Gradient = new Gradient { Offsets = offs, Colors = cs };
				l.Modulate = new Color(1f, 1f, 1f, fade * Mathf.Min(1f, s / 0.05f));
			}
		});
	}

	/// <summary>
	/// 触媒约 1.3s「咒怨涌动」（和毒性爆发、萎靡同一套黑红咒怨质感）：他一挥手，脚下涌起一股黑红咒怨，绕着他螺旋往上卷（身后的半圈在他后面、
	/// 身前的半圈在他前面），咒怨里飘起几缕怨魂；紧接着第二股 —— 更快、更高、更亮、股数更多，怨魂也更多；两股在他头顶汇成一点，
	/// 暗红一闪，顺着手臂收进他手里（手心一跳、空间一扭、轻震）。两股、一股比一股强 = 毒多触发一次。
	/// </summary>
	public static void Accelerant(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float surge1 = 0.06f, surge2 = 0.4f, top = 0.8f, home = 0.94f;
		Vector2 feet = c.Feet + new Vector2(0f, -h * 0.02f), mid = c.Feet + new Vector2(0f, -h * 0.55f), crown = c.Feet + new Vector2(0f, -h * 1.42f);
		Vector2 palm = c.Hand + new Vector2(f * h * 0.04f, -h * 0.02f);
		Color crimson = new(0.85f, 0.03f, 0.14f);
		Dim(back, mid, new Vector2(h * 3.2f, h * 2.8f), home + 0.5f, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.8f, 0.2f)) * 0.75f);
		S("gx_whoosh", 0.16f, surge1);
		S("gx_whoosh", 0.22f, surge2);
		S("gx_pop", 0.16f, surge2);
		// 第一股：3 条，慢一点、到胸口以上
		for (int i = 0; i < 2; i++)
			CurseStream(front, back, feet, h, h * 0.5f, h * 1.15f, 0.75f, Mathf.Pi * i, f, h * 0.06f, surge1 + 0.04f * i, 0.4f, 0.16f, 0.8f);
		// 第二股：5 条，更快、更高、更亮
		for (int i = 0; i < 3; i++)
			CurseStream(front, back, feet, h, h * 0.58f, h * 1.42f, 0.9f, Mathf.Tau * i / 3f + 0.5f, f, h * 0.075f, surge2 + 0.03f * i, 0.32f, 0.14f, 1.2f);
		// 脚下涌起的那一下
		foreach (float t0 in new[] { surge1, surge2 })
		{
			bool second = t0 == surge2;
			Fx.After(front, t0, () =>
			{
				Glow.Flash(back, feet + new Vector2(0f, -h * 0.1f), crimson, h * (second ? 1.6f : 1.1f), 0.35f);
				Glow.Sparks(front, feet, second ? 22 : 12, 120f * u, 520f * u, 0.5f, new[] { Pink, Deep, new Color(0.12f, 0f, 0.03f) }, -120f * u, 0.008f * u, 0.022f * u, Vector2.Up, 60f);
			});
			Vanilla.Burst(front, Vanilla.Specks, feet, -Mathf.Pi * 0.5f, second ? 1.1f : 0.8f, Lut5.Curse, amount: second ? 24 : 12, delay: t0);
			// 怨魂：从咒怨里飘起
			for (int j = 0; j < (second ? 5 : 3); j++)
			{
				Vector2 at = c.Feet + new Vector2(f * Fx.Rand(-0.45f, 0.45f) * h, -h * Fx.Rand(0.25f, 0.6f));
				Paint.Rise(j % 2 == 0 ? back : front, "curse_soul", at, h * Fx.Rand(0.5f, 0.8f), h * Fx.Rand(0.22f, 0.32f) / 256f, Lut5.Curse, 0.7f, t0 + 0.08f + 0.06f * j);
			}
		}
		// 头顶汇成一点 → 收进手里
		Fx.After(front, top, () =>
		{
			HitStop.Request(0.03);
			Glow.Flash(front, crown, Hot, h * 0.35f, 0.1f);
			Glow.Flash(back, crown, crimson, h * 1.4f, 0.32f);
			Glow.Ripple(front, crown, h * 1.3f, 0.4f, 0.03f);
		});
		Streak(front, t => crown.Lerp(palm, t) + new Vector2(f * h * 0.25f, 0f) * 4f * t * (1f - t), top + 0.02f, home - top, h * 0.6f, h * 0.06f, Red, 1.3f);
		S("gx_slice", 0.2f, home);
		Fx.After(front, home, () =>
		{
			HitStop.Request(0.035);
			Fx.Shake(1);
			Glow.Flash(front, palm, Hot, h * 0.35f, 0.1f);
			Glow.Flash(back, palm, crimson, h * 1.2f, 0.3f);
			Glow.Sparks(front, palm, 18, 120f * u, 520f * u, 0.45f, new[] { Hot, Pink, Deep }, 40f * u, 0.008f * u, 0.022f * u, Vector2.Up, 200f);
		});
		Embed(front, palm, h, 0.9f, 0f, 18, home);
		Motes(front, mid, new Vector2(h * 0.4f, h * 0.5f), h, 10, home, 0.7f);
	}
}
