using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 0.9.49 审阅第四批（2026-11-18，「装弹」一组）：刀刃交响曲、钢铁风暴（新做，原来走通用积木）。
/// 刀刃之舞（音效）、隐秘匕首（弃牌音效）改在共用的 SkillFx.Load / Discard 里；墨之刃等 Codex 的墨贴图。
/// </summary>
public static partial class SkillRedo5
{
	public static void RegisterBatch11(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["BladeSymphony"] = BladeSymphony;
		cards["StormOfSteel"] = StormOfSteel;
		cards["BladeOfInk"] = BladeOfInk3;   // 第三版「甩墨出刃」（SkillRedo12.cs）；第二版 BladeOfInk（写墨 + 白刃劈墨）被否
	}

	// ═══════════════════════════ 墨之刃 ═══════════════════════════

	private static Shader? _inkStroke;

	/// <summary>
	/// 墨笔触（Codex 的 ink_stroke_0x，本身就是墨黑 + 红边 + 白高光）。笔画在图里横跨 0.13～0.87。
	/// reveal 0→1：从左往右「写」出来，写的前沿一道烧红的边；wet：墨还湿着 —— 纹理沿笔画方向流动、上下轻轻起伏，湿亮的高光顺着笔画游走；
	/// erode 0→1：被噪声一块块蚀掉（炸成墨点的时候）。
	/// </summary>
	private static Shader InkStrokeShader => _inkStroke ??= new Shader { Code = @"
shader_type canvas_item;
uniform float reveal = 1.0;
uniform float erode = 0.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
uniform float clock = 0.0;
uniform float wet = 1.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y); }
void fragment() {
	vec2 uv = UV;
	float w1 = noise(vec2(UV.x * 7.0 - clock * 2.5, seed + clock * 0.7)) - 0.5;
	float w2 = noise(vec2(UV.x * 22.0 - clock * 6.0, UV.y * 4.0 + seed)) - 0.5;
	uv.y += (w1 * 0.07 + w2 * 0.018) * wet;
	uv.x += w2 * 0.012 * wet - clock * 0.004 * wet;
	vec4 c = texture(TEXTURE, uv);
	float n = noise(UV * vec2(18.0, 6.0) + seed) * 0.6 + noise(UV * vec2(50.0, 16.0) - seed) * 0.4;
	float front = mix(0.1, 0.9, reveal);
	float d = UV.x + (n - 0.5) * 0.06 - front;
	float shown = smoothstep(0.02, -0.04, d);
	float kept = smoothstep(erode - 0.05, erode + 0.05, n * 0.85 + (1.0 - UV.x) * 0.15);
	float lum = dot(c.rgb, vec3(0.3, 0.5, 0.2));
	vec3 col = c.rgb;
	// 湿亮的高光顺着笔画游走
	col += vec3(1.0, 0.75, 0.75) * smoothstep(0.35, 1.0, lum) * smoothstep(0.75, 1.0, sin(UV.x * 18.0 - clock * 9.0 + seed) * 0.5 + 0.5) * 0.6 * wet;
	// 边缘透的红随墨流闪一闪
	col += vec3(0.75, 0.04, 0.1) * clamp(c.a * (1.0 - c.a) * 4.0, 0.0, 1.0) * (0.3 + 0.5 * noise(vec2(UV.x * 30.0 - clock * 5.0, UV.y * 8.0))) * wet;   // 只在半透明的笔画边缘
	// 写的前沿：一道烧红的边
	float edge = exp(-abs(d) / 0.018) * step(reveal, 0.999);
	col = mix(col, vec3(1.0, 0.35, 0.38), edge * 0.85);
	COLOR = vec4(col, c.a * shown * kept * alpha);
}" };

	/// <summary>一笔墨：返回驱动用的材质。at 中心，w 宽，rot 角度，hk 高度倍数，t0 开始写，write 写多久，burst 开始蚀散，alpha 整体透明度。</summary>
	private static ShaderMaterial? InkStroke(Control layer, Vector2 at, float w, float rot, float f, float hk, float t0, float write, float burst, float life, float alpha = 1f)
	{
		Texture2D? tex = Fx.Tex("ink_stroke_0" + (Fx.Rng.Next(3) + 1));
		if (tex == null) return null;
		Node2D sr = Fx.Root(layer, at, "P5InkStroke", life + 0.1);
		ShaderMaterial m = new() { Shader = InkStrokeShader };
		m.SetShaderParameter("seed", Fx.Rand(0f, 20f));
		m.SetShaderParameter("reveal", 0f);
		var sp = new Sprite2D { Texture = tex, Material = m, Centered = true };
		sr.AddChild(sp);
		float k = w / (tex.GetWidth() * 0.74f);
		sr.Rotation = rot;
		sr.Visible = false;
		Fx.Animate(sr, t0, life - t0, t =>
		{
			sr.Visible = true;
			float s = t * (life - t0);
			float wr = Mathf.Min(1f, s / write);
			// 写到头冲过一点再弹回来
			float over = Ease5.OutBack(Mathf.Min(1f, s / (write + 0.12f)));
			sr.Scale = new Vector2(k * f * (0.88f + 0.12f * over), k * hk * (1f + 0.15f * Mathf.Exp(-s * 8f)));
			m.SetShaderParameter("reveal", Ease5.OutQuint(wr));
			m.SetShaderParameter("clock", s);
			m.SetShaderParameter("wet", 1f - 0.6f * Ease5.Seg(s, burst - t0 + 0.2f, 0.3f));
			m.SetShaderParameter("erode", Fx.OutCubic(Ease5.Seg(s, burst - t0, 0.3f)));
			m.SetShaderParameter("alpha", alpha * (1f - Ease5.Seg(s, burst - t0 + 0.2f, 0.25f)));
		});
		return m;
	}

	/// <summary>
	/// 墨之刃（稀有：生成带「墨」附魔的小刀）：「笔墨出刃」（Codex 的墨贴图）。第一版：笔画像整张贴图慢慢露出来、没有动画感，后半段一般 → 第二版：
	/// 慢：四周压暗，他一挥手，身前上方空中「唰」地写出一笔浓墨 —— 墨是湿的、在流（纹理沿笔画流动起伏、湿亮的高光游走），写的前沿一道烧红的边，
	///     笔尖一路甩出墨点、墨往下滴；写到头冲过一点再弹回来；一笔细的副笔画晚半拍跟着写出来；墨后面一层红光托着（暗背景上看得清）。
	/// 快：一道白刃斜着劈开这笔墨（「刃」）—— 白闪、十字光、顿帧，墨从劈开的两处炸开（墨溅序列帧 ×2），子弹从切口里飞出来、翻着落进他手里；
	/// 慢：手上溅开一小团墨、一下红光，剩下的墨蚀成碎块散掉。
	/// </summary>
	public static void BladeOfInk(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = BackOf(c), front = c.Front;
		int n = c.Count > 0 ? c.Count : 2;
		const float write = 0.22f, cut = 0.46f, life = 1.1f;
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Vector2 strokeAt = c.Feet + new Vector2(f * h * 1.1f, -h * 1.0f);    // 身前上方，起笔在他手前面（别压在他头上）
		float strokeW = h * 1.55f, rot = -0.18f * f;
		Vector2 dirL = new Vector2(f, 0f).Rotated(rot);
		Vector2 sA = strokeAt - dirL * strokeW * 0.5f, sB = strokeAt + dirL * strokeW * 0.5f;
		S("gx_whoosh", 0.16f, 0.02f);
		S("gx_slice", 0.1f, write * 0.6f);
		S("gx_slice", 0.24f, cut);
		S("gx_pop", 0.16f, cut + 0.02f);
		Dim(back, mid + new Vector2(f * h * 0.4f, -h * 0.3f), new Vector2(h * 3.8f, h * 3f), life + 0.3, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.6f);
		// 墨后面托一层红光（跟着写的进度从起笔亮到收笔）
		for (int i = 0; i < 3; i++)
		{
			float x = (i + 0.5f) / 3f;
			Glow.Flash(back, sA.Lerp(sB, x), new Color(0.85f, 0.04f, 0.13f), strokeW * 0.55f, cut + 0.3f - write * x, write * x);
		}
		InkStroke(front, strokeAt, strokeW, rot, f, 1.5f, 0f, write, cut, life);
		InkStroke(front, strokeAt + new Vector2(-f * h * 0.08f, h * 0.16f), strokeW * 0.8f, rot + 0.08f * f, f, 0.7f, 0.06f, write * 0.9f, cut, life, 0.75f);
		// 笔尖：一团红光跟着写的前沿走，一路甩出墨点
		Node2D tip = Fx.Root(front, sA, "P5InkTip", write + 0.05);
		Glow.SoftSprite(tip, Vector2.Zero, new Color(1f, 0.25f, 0.3f, 0.8f), h * 0.35f / 32f);
		Glow.SoftSprite(tip, Vector2.Zero, new Color(1f, 0.9f, 0.88f, 0.7f), h * 0.1f / 32f);
		Fx.Animate(tip, 0, write, t =>
		{
			tip.GlobalPosition = sA.Lerp(sB, 0.05f + 0.9f * Ease5.OutQuint(t));
			tip.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.8f, 0.2f));
		});
		Texture2D? drops = Fx.Tex("ink_drops_sheet");
		for (int i = 0; i < 6; i++)
		{
			float x = (i + 0.5f) / 6f, d = write * Mathf.Pow(x, 0.4f) * 0.9f;
			Vector2 from = sA.Lerp(sB, 0.05f + 0.9f * x);
			Phantom.Shards(front, from, 3, 200f * u, 520f * u, new Vector2(-f * 0.6f, -0.5f), 50f, 0.3f, 900f * u, 12f * u, d);
			if (drops != null) InkDrop(front, drops, from + new Vector2(0f, h * 0.05f), h, d + Fx.Rand(0.02f, 0.2f));
		}
		// 刃：一道白刃斜劈开这笔墨
		Vector2 cutAt = strokeAt + dirL * strokeW * 0.08f;
		float cutAng = rot + (f > 0 ? -1.25f : Mathf.Pi + 1.25f);
		Fx.After(front, cut, () =>
		{
			HitStop.Request(0.05);
			Fx.Shake(1);
			Glow.Blade(front, cutAt, cutAng, h * 1.1f, 46f * u, 0.3f, 0f, Red);
			Cut.Razor(front, cutAt, cutAng, h * 1.0f, 7f * u, 0.18f);
			Glow.Flash(front, cutAt, Hot, h * 0.45f, 0.12f);
			Glow.Flash(back, cutAt, Red, h * 1.6f, 0.4f);
			Cross(front, cutAt, h * 0.5f);
			InkSplash(front, cutAt - dirL * strokeW * 0.18f, h * 1.0f, -f);
			InkSplash(front, cutAt + dirL * strokeW * 0.2f, h * 1.15f, f);
			Phantom.Shards(front, cutAt, 14, 400f * u, 1000f * u, Vector2.Up, 120f, 0.4f, 900f * u, 18f * u);
			SkillFx.Load(front, cutAt, Mathf.Min(n, 6), f, true, c.Hand);
		});
		// 子弹落进手里：手上溅一小团墨、一下红光
		Fx.After(front, cut + 0.5f, () =>
		{
			InkSplash(front, c.Hand, h * 0.45f, f, 0.4f);
			Glow.Flash(back, c.Hand, Red, h * 0.9f, 0.3f);
			EchoPulse(c, 0.05f, 0.3f, 0.6f);
		});
	}

	/// <summary>一滴墨（墨滴贴图随机一格）：从 from 往下落、慢慢转、落地前淡掉。</summary>
	private static void InkDrop(Control layer, Texture2D drops, Vector2 from, float h, float delay)
	{
		float fall = Fx.Rand(0.35f, 0.5f);
		Node2D dr = Fx.Root(layer, from, "P5InkDrop", delay + fall + 0.05);
		var dsp = new Sprite2D { Texture = drops, Hframes = 4, Vframes = 4, Frame = Fx.Rng.Next(16) };
		dr.AddChild(dsp);
		float sc = h * Fx.Rand(0.07f, 0.12f) / (drops.GetWidth() / 4f), spin = Fx.Rand(-2f, 2f);
		dr.Scale = Vector2.One * sc;
		dr.Visible = false;
		Fx.Animate(dr, delay, fall, t =>
		{
			dr.Visible = true;
			dr.GlobalPosition = from + new Vector2(0f, h * 0.9f * t * t);
			dr.Rotation = spin * t;
			dr.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.7f, 0.3f));
		});
	}

	/// <summary>墨溅序列帧（Codex 的 ink_splash_sheet：从中心炸开、往右上甩墨点、散掉），facing &lt; 0 水平翻转。</summary>
	private static void InkSplash(Control layer, Vector2 at, float size, float facing, float dur = 0.5f)
	{
		if (Fx.Tex("ink_splash_sheet") is not Texture2D sheet) return;
		Node2D root = Fx.Root(layer, at, "P5InkSplash", dur + 0.05);
		var sp = new Sprite2D { Texture = sheet, Hframes = 4, Vframes = 4, Frame = 0 };
		root.AddChild(sp);
		float cell = sheet.GetWidth() / 4f;
		Fx.Animate(root, 0, dur, t =>
		{
			sp.Frame = Mathf.Min(15, (int)(t * 16f));
			float k = size / cell * (0.9f + 0.2f * Fx.OutCubic(t));
			root.Scale = new Vector2(k * (facing < 0 ? -1f : 1f), k);
		});
	}

	/// <summary>
	/// 刀刃交响曲（给所有队友生成小刀）：「一串音符」。他一挥手，四周稍暗，子弹按节拍一发一发弹起、排成一道往上扬的弧（每发「叮」、星光），
	/// 停一拍，一齐俯冲进弹巢，收在一记「和弦」上（白闪 + 十字光 + 碎光 + 「咔」），身上红残影一胀。
	/// </summary>
	public static void BladeSymphony(SkillCtx c)
	{
		float h = c.Tall;
		Control back = BackOf(c);
		int n = Mathf.Clamp(c.Count > 0 ? c.Count : 2, 1, 4);
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		float end = SkillFx.LoadArc(c.Front, c.Hand, n, c.Face);
		Dim(back, mid + new Vector2(c.Face * h * 0.3f, -h * 0.2f), new Vector2(h * 3.4f, h * 3f), end + 0.5, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.5f);
		Glow.Flash(back, c.Hand + new Vector2(c.Face * h * 0.3f, -h * 0.4f), new Color(0.7f, 0.03f, 0.12f), h * 1.6f, end + 0.2f);
		Fx.After(c.Front, end, () =>
		{
			HitStop.Request(0.03);
			EchoPulse(c, 0.05f, 0.3f, 0.6f);
		});
	}

	/// <summary>
	/// 钢铁风暴（弃掉整手，每张换一把小刀）：「钢铁风暴」。四周压暗，他身边一下刮起一阵交错乱窜的银白 / 红疾光（不绕圈，每道从一个方向斜穿过他身边），
	/// 越刮越密；一顿 —— 所有光往他手里一收（光线往手上收、白闪、十字光、身上红残影一胀、顿帧轻震），然后按弃掉的张数装弹（c.Count）。
	/// </summary>
	public static void StormOfSteel(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = BackOf(c), front = c.Front;
		int n = c.Count >= 0 ? Mathf.Clamp(c.Count, 0, 10) : 5;
		const float storm = 0.5f;
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		S("gx_whoosh", 0.16f, 0.02f);
		S("gx_whoosh", 0.12f, 0.2f);
		S("gx_slice", 0.2f, storm);
		Dim(back, mid, new Vector2(h * 3.8f, h * 3f), storm + 0.9, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.12f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.65f);
		Glow.Flash(back, mid, new Color(0.7f, 0.03f, 0.12f), h * 2f, storm + 0.3f);
		// 弃掉的牌：从手里甩出去烧掉（最多画 4 张）
		if (n > 0) SkillFx.Discard(front, c.Hand, Mathf.Min(n, 4), f);
		// 风暴（用户 2026-11-18：钢铁风暴纯表达、没什么特效 → 主体改成刀光风暴）：用着色器横劈在他周围的三维空间里连劈六刀，
		// 角度 / 倾斜 / 方向 / 大小各不相同、一刀接一刀，钢铁的银白带红边；每刀扫到最近处迸火星和金属碎光
		for (int i = 0; i < 6; i++)
		{
			int k = i;
			float d = 0.03f + 0.065f * k;
			float dir = k % 2 == 0 ? f : -f;
			Vector2 ctr = mid + new Vector2(Fx.Rand(-0.25f, 0.25f) * h, Fx.Rand(-0.3f, 0.2f) * h);
			float rx = h * Fx.Rand(0.55f, 0.85f), ry = rx * Fx.Rand(0.22f, 0.42f), tilt = Fx.Rand(-0.75f, 0.75f);
			SlashArc(k % 3 == 1 ? back : front, ctr, rx, ry, tilt, dir, d, 0.13f, 0.2f, h, k % 3 == 2 ? 0.35f : 0.8f, k % 3 == 1 ? 0.6f : 0.8f);
			Fx.After(front, d + 0.035f, () =>
			{
				Vector2 near = ctr + new Vector2(0f, ry).Rotated(tilt * dir);
				Phantom.Shards(front, near, 6, 300f * u, 800f * u, new Vector2(dir, 0.2f), 60f, 0.3f, 600f * u, 14f * u);
				Glow.Sparks(front, near, 8, 100f * u, 380f * u, 0.4f, new[] { new Color(0.95f, 0.95f, 1f), new Color(0.9f, 0.1f, 0.18f) }, 500f * u, 0.03f * u, 0.07f * u, new Vector2(dir, 0f), 60f);
				Glow.Glint(front, near, 0.45f);
				if (k % 2 == 0) S("gx_slice", 0.1f);
			});
		}
		Gather(front, c.Hand, h, u, storm, 18, 0.8f);
		Fx.After(front, storm, () =>
		{
			HitStop.Request(0.05);
			Fx.Shake(1);
			Glow.Flash(front, c.Hand, Hot, h * 0.4f, 0.12f);
			Glow.Flash(back, mid, Red, h * 2.4f, 0.4f);
			Cross(front, c.Hand, h * 0.5f);
			EchoPulse(c, 0.07f);
			if (n > 0) SkillFx.Load(front, c.Hand, Mathf.Min(n, 6), f);
		});
		if (c.SelfTint is { } tint)
			Fx.Animate(front, 0, storm + 0.6, t =>
			{
				float s = t * (storm + 0.6f);
				float k = 0.25f * Ease5.Seg(s, 0f, storm) + (s > storm ? 0.9f * Mathf.Exp(-(s - storm) * 5f) : 0f);
				tint(Colors.White.Lerp(new Color(1.5f, 0.75f, 0.8f), k));
			});
	}
}
