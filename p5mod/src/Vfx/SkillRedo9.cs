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

	/// <summary>光线往一点收（给别的文件用，比如蛇咬定住时往蛇嘴里收）。</summary>
	public static void GatherAt(Control layer, Vector2 at, float h, float u, float arrive, int n = 18, float r0 = 0.6f) => Gather(layer, at, h, u, arrive, n, r0);

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

	/// <summary>两头尖的疾光（粗的疾光头不平）。</summary>
	private static void Sharp(Control layer, Func<float, Vector2> path, float t0, float dur, float len, float thick, Color tint, float hot = 1.3f) =>
		DashFly(layer, path, t0, dur, len, thick, tint, hot, Ease5.OutQuint, 0.7f, 1f);

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
		Phantom.P5Card card = Phantom.Card(front, c.Hand, w * 1.5f, 0.95, 0f);   // 2026-11-16：牌的拖尾带子两头是平的 → 不要带子，收进胸口时用两头尖的疾光
		card.Root.Visible = false;
		const float t0 = 0.1f, park = 0.44f, dive = 0.74f, end = 0.85f;
		DashFly(front, t => top.Lerp(c.Chest, t), dive, end - dive, h * 0.55f, h * 0.05f, Red, 1.3f, Fx.InCubic, 0.8f, 1f);
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
	/// 磨蚀约 1s「月牙反斩」（用户 2026-11-16：荆棘、三角光刺是棱角分明的贴图；五道 + 三道月牙又太乱 → 只留两道）：四周暗下来、光线往胸口收；
	/// 主拍时左右对称的两道大月牙刀光同时从他身上斩出（弧背朝外、略往上扬），重震、白闪、空间一扭、火星；最后光点往上飘。
	/// </summary>
	public static void Abrasive(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float beat = 0.2f;   // 2026-11-16 用户：节奏收紧
		S("gx_whoosh", 0.14f, 0.04f);
		S("gx_slice", 0.28f, beat);
		S("gx_pop", 0.18f, beat + 0.01f);
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.58f);
		Dim(back, mid, new Vector2(h * 3.4f, h * 2.8f), 0.85, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.75f);
		Gather(front, c.Chest, h, u, beat, 14, 0.55f);
		Glow.Flash(back, mid, new Color(1f, 0.1f, 0.18f), h * 1.3f, 0.25f, beat - 0.1f);
		void Crescent(float deg, float dist, float len, float bow, float thick, float d, bool fr, float skew)
		{
			Vector2 dir = Vector2.FromAngle(Mathf.DegToRad(deg));
			dir.X *= f;
			// 弦垂直于径向、弧背朝外（Blade.Arc 往局部 -y 拱）
			global::P5Joker.Blade.Arc(fr ? front : back, mid + dir * dist, dir.Angle() + Mathf.Pi * 0.5f + skew * f, len, bow, thick, false, 0.05f, 0.03f, 0.05f, 0.16f, true, d);
		}
		// 用户：五道 + 三道太乱 → 只要左右对称的两道大月牙，同时斩出（弧背朝外、略往上扬），干净
		// 2026-11-16：一个规整的半圆太死板 → 左右不对称（左大右小、高低错开），每道外侧再跟一道细的副刃，再点缀两道小月牙
		Crescent(-162f, h * 0.38f, h * 1.4f, h * 0.32f, h * 0.18f, beat, false, 0.3f);
		Crescent(-162f + 6f, h * 0.5f, h * 0.95f, h * 0.2f, h * 0.06f, beat + 0.025f, true, 0.42f);
		Crescent(-28f, h * 0.34f, h * 1.15f, h * 0.24f, h * 0.15f, beat + 0.012f, true, -0.45f);
		Crescent(-28f - 8f, h * 0.46f, h * 0.8f, h * 0.16f, h * 0.05f, beat + 0.035f, false, -0.6f);
		Crescent(-95f, h * 0.62f, h * 0.45f, h * 0.08f, h * 0.05f, beat + 0.03f, true, 0.9f);
		Crescent(150f, h * 0.48f, h * 0.5f, h * 0.09f, h * 0.05f, beat + 0.045f, false, -0.8f);
		Fx.After(front, beat, () =>
		{
			HitStop.Request(0.05);
			Fx.Shake(2);
			Glow.Flash(front, mid, Hot, h * 0.5f, 0.12f);
			Glow.Flash(back, mid, new Color(1f, 0.12f, 0.2f), h * 2.4f, 0.45f);
			Glow.Sparks(front, mid, 26, 160f * u, 700f * u, 0.6f, new[] { Hot, Pink, Deep }, -40f * u, 0.008f * u, 0.03f * u, Vector2.Up, 180f);
		});
		Embed(front, mid, h, 1.4f, 0f, 30, beat);
		Motes(front, mid, new Vector2(h * 0.4f, h * 0.45f), h, 10, beat + 0.12f, 0.6f);
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
			Sharp(front, t => a0.Lerp(b0, t), b, 0.12f, h * (0.5f + 0.3f * k), h * (0.06f + 0.05f * k), i % 3 == 1 ? new Color(1f, 0.9f, 0.9f) : Red, 1.35f);
		}
		// 连成一片：最后 0.2s 疾光贴着他身上来回刷（前后两层）
		for (int j = 0; j < 9; j++)
		{
			float d = 0.86f + 0.025f * j;
			float y = -h * Fx.Rand(0.15f, 1.4f), dirX = j % 2 == 0 ? 1f : -1f, cx = f * h * Fx.Rand(-0.5f, 0.9f);
			Vector2 a0 = c.Feet + new Vector2(cx - dirX * h * 0.5f, y + Fx.Rand(-0.08f, 0.08f) * h), b0 = c.Feet + new Vector2(cx + dirX * h * 0.5f, y - Fx.Rand(-0.08f, 0.08f) * h);
			Sharp(j % 2 == 0 ? front : back, t => a0.Lerp(b0, t), d, 0.1f, h * 0.55f, h * 0.07f, j % 3 == 0 ? new Color(1f, 0.92f, 0.92f) : Red, 1.4f);
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

	/// <summary>
	/// 速行者每抽一张牌造成伤害的那一下（每回合可能很多次 → 轻、短，约 0.25s）：敌人身上一道斜着划过的尖头红白疾光、一点白闪、几粒火星。
	/// at = 敌人身体中间，h = 敌人高度。
	/// </summary>
	public static void SpeedsterTick(Control layer, Vector2 at, float h, float delay = 0f)
	{
		float ang = Fx.Rand(-0.5f, -0.25f) + (Fx.Rand(0f, 1f) < 0.5f ? 0f : Mathf.Pi);
		Vector2 d = Vector2.FromAngle(ang);
		float L = Mathf.Clamp(h * 0.9f, 120f, 320f);
		Vector2 a0 = at - d * L * 0.6f, b0 = at + d * L * 0.6f;
		Sharp(layer, t => a0.Lerp(b0, t), delay, 0.12f, L, Mathf.Clamp(h * 0.14f, 22f, 40f), Red, 1.4f);
		Sharp(layer, t => a0.Lerp(b0, t), delay + 0.01f, 0.12f, L * 0.7f, Mathf.Clamp(h * 0.05f, 8f, 14f), new Color(1f, 0.92f, 0.92f), 1.7f);
		Fx.After(layer, delay + 0.05f, () =>
		{
			Glow.Flash(layer, at, Hot, L * 0.45f, 0.08f);
			Glow.Flash(layer, at, new Color(1f, 0.12f, 0.2f), L * 1.0f, 0.2f);
			Glow.Sparks(layer, at, 6, 60f, 260f, 0.25f, new[] { Hot, Pink, Deep }, 0f, 0.08f, 0.2f, d, 60f);
		});
	}

	// ═══════════════════════════ 触媒（罕见能力） ═══════════════════════════

	/// <summary>
	/// 触媒约 0.7s（用户 2026-11-16：前几版太尬，做简单点）：他挥手，掌心「咚、咚」两下暗红脉冲 —— 第一下小，第二下更大更亮（多触发一次），
	/// 每下一圈火星和一点空间扭曲，第二下轻震、一点星光，余下几颗光点往上飘。
	/// </summary>
	public static void Accelerant(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		// 挥手（cast_wave）伸直的手在脚前上方这一点；c.Hand 是出牌那一刻的手（还没甩出去），用它会落在前臂上（用户 2026-11-16）
		Vector2 palm = c.Feet + new Vector2(f * h * 0.28f, -h * 0.83f);
		Color crimson = new(0.85f, 0.03f, 0.14f);
		const float p1 = 0.05f, p2 = 0.22f;   // 挥手的手只伸出去约 0.3s
		S("gx_pop", 0.14f, p1);
		S("gx_pop", 0.22f, p2);
		foreach (float when in new[] { p1, p2 })
		{
			bool big = when == p2;
			Fx.After(front, when, () =>
			{
				Glow.Flash(front, palm, big ? Hot : new Color(1f, 0.7f, 0.72f), h * (big ? 0.38f : 0.24f), 0.09f);
				Glow.Flash(back, palm, crimson, h * (big ? 0.75f : 0.45f), big ? 0.28f : 0.2f);
				Glow.Sparks(front, palm, big ? 20 : 10, 100f * u, (big ? 520f : 340f) * u, 0.45f, new[] { Hot, Pink, Deep }, 30f * u, 0.007f * u, 0.02f * u, Vector2.Up, 200f);
				if (big)
				{
					HitStop.Request(0.03);
					Fx.Shake(1);
					Glow.Glint(front, palm + new Vector2(0f, -h * 0.04f), 0.9f);
					Flare(front, palm, h * 0.9f, h * 0.014f, new Color(1f, 0.9f, 0.9f), 0.16f, f * -0.12f);
					Phantom.Shards(front, palm, 12, 300f * u, 800f * u, new Vector2(f, -0.2f), 120f, 0.3f, 0f, 20f * u);
				}
				else Glow.Glint(front, palm, 0.5f);
			});
			Glow.Ripple(front, palm, h * (big ? 1.1f : 0.7f), 0.35f, big ? 0.03f : 0.018f, 1f, when);
		}
		// 第二下：几道暗红疾光从掌心往前甩开，原版火星（咒怨色）
		for (int j = 0; j < 4; j++)
		{
			float ang = (f > 0f ? 0f : Mathf.Pi) + (j - 1.5f) * 0.32f * f + Fx.Rand(-0.08f, 0.08f);
			Vector2 a0 = palm, b0 = palm + Vector2.FromAngle(ang) * h * Fx.Rand(0.45f, 0.65f);
			Streak(front, t => a0.Lerp(b0, t), p2 + 0.01f * j, 0.16f, h * 0.35f, h * (j % 2 == 0 ? 0.05f : 0.03f), j % 2 == 0 ? Red : new Color(1f, 0.88f, 0.88f), 1.3f);
		}
		Vanilla.Burst(front, Vanilla.Specks, palm, f > 0f ? 0f : Mathf.Pi, 0.9f, Lut5.Curse, amount: 20, delay: p2);
		Motes(front, palm, new Vector2(h * 0.12f, h * 0.08f), h, 8, p2 + 0.05f, 0.6f);
	}
}
