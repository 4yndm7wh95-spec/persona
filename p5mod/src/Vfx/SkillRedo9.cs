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
	/// 磨蚀约 1.3s：四周暗下来、光线往他身上收；主拍时身后一丛荆棘「轰」地刺出又碎掉，同时一圈荆棘藤从他身上朝四面抽出去、一闪即溶；
	/// 空间一扭、火星和尖刺粒子迸开、重震。紧跟着第二波短一点的荆棘换个角度再抽一次，最后光点往上飘。
	/// </summary>
	public static void Abrasive(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float beat = 0.36f, wave2 = 0.62f;
		S("gx_whoosh", 0.16f, 0.05f);
		S("gx_pop", 0.24f, beat);
		S("gx_slice", 0.18f, beat + 0.03f);
		S("gx_slice", 0.14f, wave2);
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid, new Vector2(h * 3.2f, h * 2.6f), 1.3, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.65f, 0.35f)) * 0.75f);
		Gather(front, c.Chest, h, u, beat, 20, 0.75f);
		Glow.Flash(back, mid, new Color(1f, 0.1f, 0.18f), h * 1.4f, 0.4f, beat - 0.2f);

		Bloom(back, "thorn_burst", c.Chest + new Vector2(-f * h * 0.04f, -h * 0.02f), Fx.Rand(-0.2f, 0.2f), h * 1.7f, Lut5.Blood, beat, 0.06f, 0.05f, 0.26f, 0.45f, 1.5f, f);
		void Wave(float at, float[] angs, float lenK, int seed)
		{
			string[] vines = { "thorn_vine_01", "thorn_vine_02", "thorn_vine_03" };
			for (int i = 0; i < angs.Length; i++)
			{
				Vector2 dir = new(Mathf.Cos(angs[i]) * f, Mathf.Sin(angs[i]));
				float len = h * lenK * (i % 2 == 0 ? 1.1f : 0.85f), d = at + 0.02f * i;
				Vector2 a = c.Chest + dir * h * 0.06f;
				Lay(back, vines[(i + seed) % 3], a, a + dir * len, 0.5f, Lut5.Blood, 0.08f, 0.05f, 0.26f, d, 1.45f, (i + seed) % 2 == 1, 50f * u);
				Fx.After(front, d + 0.07f, () => Glow.Glint(front, a + dir * len * 0.9f, 0.5f));
			}
		}
		Wave(beat - 0.02f, new[] { -2.6f, -1.95f, -1.25f, -0.5f, 0.45f, 2.7f }, 1.0f, 0);
		Wave(wave2, new[] { -2.25f, -1.6f, -0.85f, 0.1f, 2.3f }, 0.75f, 1);
		Fx.After(front, beat, () =>
		{
			HitStop.Request(0.05);
			Fx.Shake(2);
			Glow.Flash(front, c.Chest, Hot, h * 0.45f, 0.12f);
			Glow.Flash(back, mid, new Color(1f, 0.12f, 0.2f), h * 2.4f, 0.45f);
			Glow.Sparks(front, c.Chest, 22, 160f * u, 700f * u, 0.6f, new[] { Hot, Pink, Deep }, -40f * u, 0.008f * u, 0.03f * u, Vector2.Up, 180f);
		});
		Embed(front, c.Chest, h, 1.4f, 0f, 36, beat);
		Embed(front, c.Chest, h, 1.0f, 0f, 20, wave2);
		Motes(front, mid, new Vector2(h * 0.6f, h * 0.5f), h, 12, wave2 + 0.15f, 0.8f);
	}

	// ═══════════════════════════ 速行者（罕见能力） ═══════════════════════════

	/// <summary>
	/// 速行者约 1.3s：手边五张预告信一张张弹起、翻正，排成一小把扇；然后一张接一张射出去 —— 牌本身飞过去，身后拖一道一闪即溶的牌流残光；
	/// 打中处空间一扭、白闪、火星、纸屑，敌人一白；最后一张最大、定格一下、轻震。
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
			Vector2 tg = foes.Count > 0 ? Mid(foes[k % foes.Count]) + new Vector2(0f, Fx.Rand(-0.12f, 0.08f) * h) : c.Chest + new Vector2(f * h * 1.8f, 0f);
			float travel = last ? 0.11f : 0.09f;
			Phantom.P5Card card = Phantom.Card(front, hand, w * (last ? 1.7f : 1.5f), go + travel + 0.05, 0f);
			card.Root.Visible = false;
			float spinTo = (tg - slot).Angle() + Mathf.Pi * 0.5f;
			Fx.Animate(card.Root, pop, go + travel - pop, t =>
			{
				card.Root.Visible = true;
				float s = pop + t * (go + travel - pop);
				if (s < go)
				{
					float l = s - pop, e = Ease5.OutQuint(Mathf.Min(1f, l / 0.2f));
					Vector2 shiver = l > 0.2f ? new Vector2(0f, Mathf.Sin(l * 30f + k) * h * 0.004f) : Vector2.Zero;
					card.Place(hand.Lerp(slot, e) + shiver, tilt * e, Mathf.Pi * (1f - e) * 3f, 0.6f + 0.4f * e);
					card.Set("glow", 0.6f * Mathf.Exp(-l * 10f) + (s > go - 0.08f ? 0.8f : 0f));
				}
				else
				{
					float e = Fx.InCubic((s - go) / travel) * 0.6f + (s - go) / travel * 0.4f;
					card.Place(slot.Lerp(tg, e), Mathf.Lerp(tilt, spinTo, e), Mathf.Tau * 3f + e * 6f, 1f);
					card.Set("glow", 0.5f);
				}
			});
			Fx.After(front, pop + 0.18f, () => Glow.Glint(front, slot + new Vector2(0f, -w * 0.7f), 0.4f));
			S("gx_whoosh", last ? 0.22f : 0.13f, go);
			Streak(back, t => slot.Lerp(tg, t), go, travel + 0.06f, h * (last ? 1.1f : 0.8f), h * (last ? 0.1f : 0.07f), Red, 1.3f);   // 牌流贴图是平涂的红牌，贴图感重 → 只用一道疾光拖尾
			Fx.After(front, go + travel, () =>
			{
				card.Trail?.Kill();
				card.Root.Visible = false;
				Vector2 dn = (tg - slot).Normalized();
				Glow.Flash(front, tg, Hot, h * (last ? 0.6f : 0.35f), 0.1f);
				Glow.Flash(back, tg, new Color(1f, 0.12f, 0.2f), h * (last ? 1.4f : 0.9f), 0.25f);
				Embed(front, tg, h, last ? 1.2f : 0.8f, dn.Angle(), last ? 30 : 14);
				Phantom.Confetti(front, tg, last ? 22 : 10, 480f * u, dn, 70f, 0.7f, 8f * u);
				if (foes.Count > 0) Flick(foes[k % foes.Count], front, new Color(1.8f, 1.5f, 1.5f), 0.22f);
				if (last) { HitStop.Request(0.04); Fx.Shake(1); S("gx_slice", 0.2f); }
			});
		}
	}

	// ═══════════════════════════ 触媒（罕见能力） ═══════════════════════════

	/// <summary>
	/// 触媒约 1.2s：他手心聚起一点黑红光；一弹，几颗毒液划着弧飞向每个敌人；打中后敌人身上「跳」两下 ——
	/// 每一下毒纹从中心闪着爬开又立刻溶掉、溅起一圈毒液、空间一扭、敌人一闪；第二下更大更重、敌人一暗。
	/// </summary>
	public static void Accelerant(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float fire = 0.3f;
		S("gx_whoosh", 0.14f, 0.02f);
		S("gx_card_slide", 0.14f, fire);
		Vector2 palm = c.Hand + new Vector2(f * h * 0.05f, -h * 0.04f), mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid + new Vector2(f * h * 0.8f, 0f), new Vector2(h * 4.6f, h * 2.4f), 1.3, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.8f, 0.2f)) * 0.7f);
		Gather(front, palm, h, u, fire, 14, 0.5f);
		Glow.Flash(back, palm, new Color(0.7f, 0.02f, 0.1f), h * 0.9f, fire + 0.1f);
		Fx.After(front, fire, () => Glow.Flash(front, palm, new Color(1f, 0.6f, 0.62f), h * 0.35f, 0.1f));
		var foes = new List<SkillFx.Victim>(c.Enemies);
		for (int i = 0; i < foes.Count; i++)
		{
			var v = foes[i];
			bool first = i == 0;
			Vector2 tg = Mid(v);
			float fly = 0.18f + 0.03f * i, hit1 = fire + fly, hit2 = hit1 + 0.24f;
			float ew = Mathf.Max(v.Width, h * 0.5f);
			for (int j = 0; j < 3; j++)
			{
				float dj = fire + 0.03f * j, arc = h * Fx.Rand(0.25f, 0.45f);
				Vector2 aim = tg + new Vector2(Fx.Rand(-0.1f, 0.1f) * ew, Fx.Rand(-0.1f, 0.1f) * h);
				int cell = (int)Fx.Rand(0f, 7.99f);
				Fx.After(front, dj, () =>
				{
					Node2D r = Fx.Root(front, palm, "P5Venom", fly + 0.05);
					if (Cell(r, "venom_drops_sheet", cell, h * 0.14f, false) is not Sprite2D sp) return;
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
			if (first) { S("gx_pop", 0.16f, hit1); S("gx_pop", 0.22f, hit2); }
			foreach (float when in new[] { hit1, hit2 })
			{
				bool heavy = when == hit2;
				Bloom(front, (i + (heavy ? 1 : 0)) % 2 == 0 ? "venom_veins_01" : "venom_veins_02", tg, Fx.Rand(-0.6f, 0.6f), ew * (heavy ? 2.0f : 1.4f), Lut5.Blood,
					when, 0.05f, 0.04f, 0.22f, 0.6f, heavy ? 1.6f : 1.35f, f);
				Embed(front, tg, h, heavy ? 1.1f : 0.7f, -Mathf.Pi * 0.5f, heavy ? 24 : 12, when);
				Fx.After(front, when, () =>
				{
					Glow.Flash(front, tg, heavy ? Hot : new Color(1f, 0.7f, 0.72f), h * (heavy ? 0.4f : 0.28f), 0.08f);
					if (heavy) Glow.Flash(back, tg, new Color(0.75f, 0.02f, 0.12f), ew * 1.6f, 0.28f);
					for (int j = 0; j < (heavy ? 8 : 4); j++)
					{
						Vector2 vel = Vector2.FromAngle(Fx.Rand(-Mathf.Pi * 0.9f, -Mathf.Pi * 0.1f)) * h * Fx.Rand(0.9f, 1.8f);
						float life = Fx.Rand(0.35f, 0.5f), sz = h * Fx.Rand(0.06f, 0.1f);
						int cell = 4 + (int)Fx.Rand(0f, 11.99f);
						Node2D r = Fx.Root(front, tg, "P5Splash", life + 0.05);
						if (Cell(r, "venom_drops_sheet", cell, sz, false) is not Sprite2D sp) continue;
						Fx.Animate(r, 0, life, t =>
						{
							float s = t * life;
							r.GlobalPosition = tg + vel * s + new Vector2(0f, h * 3f * s * s);
							r.Rotation = (vel + new Vector2(0f, h * 6f * s)).Angle() - Mathf.Pi * 0.5f;
							sp.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.5f, 0.5f));
						});
					}
					Flick(v, front, heavy ? new Color(0.45f, 0.38f, 0.4f) : new Color(1.7f, 1.55f, 1.55f), heavy ? 0.32f : 0.18f);
					if (heavy && first) { HitStop.Request(0.035); Fx.Shake(1); }
				});
			}
		}
	}
}
