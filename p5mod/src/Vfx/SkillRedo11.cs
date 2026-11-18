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
		cards["BladeOfInk"] = BladeOfInk;
	}

	// ═══════════════════════════ 墨之刃 ═══════════════════════════

	private static Shader? _inkStroke;

	/// <summary>
	/// 墨笔触（Codex 的 ink_stroke_0x，本身就是墨黑 + 红边 + 白高光）：reveal 0→1 从左往右「写」出来（笔锋前沿带噪声），
	/// erode 0→1 被噪声一块块蚀掉（散成墨点的时候）。笔画在图里横跨 0.13～0.87。
	/// </summary>
	private static Shader InkStrokeShader => _inkStroke ??= new Shader { Code = @"
shader_type canvas_item;
uniform float reveal = 1.0;
uniform float erode = 0.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y); }
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	float n = noise(UV * vec2(18.0, 6.0) + seed) * 0.6 + noise(UV * vec2(50.0, 16.0) - seed) * 0.4;
	float front = mix(0.1, 0.9, reveal);
	float shown = smoothstep(front + 0.02, front - 0.04, UV.x + (n - 0.5) * 0.06);
	float kept = smoothstep(erode - 0.05, erode + 0.05, n * 0.85 + (1.0 - UV.x) * 0.15);
	COLOR = vec4(c.rgb, c.a * shown * kept * alpha);
}" };

	/// <summary>
	/// 墨之刃（稀有：生成带「墨」附魔的小刀）：「笔墨出刃」（Codex 的墨贴图，用户 2026-11-18：只是简单装弹、没有特色）。
	/// 慢：四周压暗，他一挥手，身前上方空中「唰」地从左往右写出一笔浓墨（墨后面一层红光托着，暗背景上也看得清），墨从笔画上滴下来；
	/// 快：子弹从墨里被拔出来（笔画上一闪），翻着落进他手里（共用装弹，子弹从笔画出发、落到手上）；同时笔画「啪」地炸成墨点（墨溅序列帧）散掉。
	/// </summary>
	public static void BladeOfInk(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = BackOf(c), front = c.Front;
		int n = c.Count > 0 ? c.Count : 2;
		const float write = 0.2f, pull = 0.42f, burst = 0.5f, life = 1.0f;
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Vector2 strokeAt = c.Feet + new Vector2(f * h * 1.1f, -h * 1.0f);    // 身前上方，起笔在他手前面（别压在他头上）
		float strokeW = h * 1.5f;
		S("gx_whoosh", 0.16f, 0.02f);
		S("gx_slice", 0.12f, write * 0.6f);
		S("gx_pop", 0.18f, burst);
		Dim(back, mid + new Vector2(f * h * 0.4f, -h * 0.3f), new Vector2(h * 3.8f, h * 3f), life + 0.4, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.6f);
		// 笔画后面托一层红光（墨是黑的，暗背景上要靠它衬出来）
		Glow.Flash(back, strokeAt, new Color(0.85f, 0.04f, 0.13f), strokeW * 0.9f, burst + 0.3f);
		Node2D sr = Fx.Root(front, strokeAt, "P5InkStroke", life + 0.1);
		ShaderMaterial m = new() { Shader = InkStrokeShader };
		m.SetShaderParameter("seed", Fx.Rand(0f, 20f));
		m.SetShaderParameter("reveal", 0f);
		Texture2D? tex = Fx.Tex("ink_stroke_0" + (Fx.Rng.Next(3) + 1));
		if (tex != null)
		{
			var sp = new Sprite2D { Texture = tex, Material = m, Centered = true };
			sr.AddChild(sp);
			float k = strokeW / (tex.GetWidth() * 0.74f);
			sr.Scale = new Vector2(k * f, k * 1.45f);
			sr.Rotation = -0.18f * f;
		}
		Fx.Animate(sr, 0, life, t =>
		{
			float s = t * life;
			m.SetShaderParameter("reveal", Ease5.OutQuint(Mathf.Min(1f, s / write)));
			m.SetShaderParameter("erode", Fx.OutCubic(Ease5.Seg(s, burst, 0.3f)));
			m.SetShaderParameter("alpha", 1f - Ease5.Seg(s, burst + 0.2f, 0.25f));
		});
		// 写的时候笔锋一路一点红光
		Vector2 dirL = new Vector2(f, 0f).Rotated(-0.18f * f);
		Vector2 sA = strokeAt - dirL * strokeW * 0.5f, sB = strokeAt + dirL * strokeW * 0.5f;
		for (int i = 0; i < 4; i++)
		{
			float x = (i + 0.5f) / 4f;
			Glow.Flash(front, sA.Lerp(sB, x), new Color(1f, 0.25f, 0.3f), h * 0.3f, 0.2f, write * Ease5.OutQuint(x) * 0.8f);
		}
		// 墨从笔画上滴下来（墨滴贴图）
		if (Fx.Tex("ink_drops_sheet") is Texture2D drops)
			for (int i = 0; i < 5; i++)
			{
				Vector2 from = sA.Lerp(sB, Fx.Rand(0.15f, 0.85f)) + new Vector2(0f, h * 0.05f);
				float d = write + Fx.Rand(0.02f, 0.3f), fall = Fx.Rand(0.35f, 0.5f);
				Node2D dr = Fx.Root(front, from, "P5InkDrop", d + fall + 0.05);
				var dsp = new Sprite2D { Texture = drops, Hframes = 4, Vframes = 4, Frame = Fx.Rng.Next(16) };
				dr.AddChild(dsp);
				float sc = h * Fx.Rand(0.07f, 0.12f) / (drops.GetWidth() / 4f);
				dr.Scale = Vector2.One * sc;
				dr.Visible = false;
				Fx.Animate(dr, d, fall, t =>
				{
					dr.Visible = true;
					dr.GlobalPosition = from + new Vector2(0f, h * 0.9f * t * t);
					dr.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.7f, 0.3f));
				});
			}
		// 子弹从墨里拔出来，落进手里
		Fx.After(front, pull, () =>
		{
			Glow.Flash(front, strokeAt, Hot, h * 0.3f, 0.1f);
			Glow.Glint(front, strokeAt, 0.8f);
			SkillFx.Load(front, strokeAt + new Vector2(0f, h * 0.05f), Mathf.Min(n, 6), f, true, c.Hand);
		});
		// 笔画炸成墨点
		Fx.After(front, burst, () =>
		{
			HitStop.Request(0.03);
			InkSplash(front, strokeAt + dirL * strokeW * 0.1f, h * 1.1f, f);
			Glow.Flash(back, strokeAt, Red, h * 1.4f, 0.35f);
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
		// 风暴：交错乱窜的疾光，越到后面越密
		int streaks = 16 + 2 * n;
		for (int i = 0; i < streaks; i++)
		{
			float d = storm * Mathf.Pow(Fx.Rand(0f, 1f), 0.7f) * 0.9f;
			float ang = Fx.Rand(0f, Mathf.Tau);
			Vector2 dir = Vector2.Right.Rotated(ang);
			Vector2 center = mid + new Vector2(Fx.Rand(-0.35f, 0.35f) * h, Fx.Rand(-0.45f, 0.4f) * h);
			Vector2 a = center - dir * h * Fx.Rand(0.7f, 1.1f), b = center + dir * h * Fx.Rand(0.7f, 1.1f);
			Color col = i % 3 == 0 ? new Color(1f, 0.15f, 0.22f) : new Color(1f, 0.85f, 0.85f);
			StreakSharp(i % 2 == 0 ? front : back, e => a.Lerp(b, e), d, Fx.Rand(0.13f, 0.2f), h * Fx.Rand(0.45f, 0.7f), Fx.Rand(18f, 30f) * u, col, 1.6f);
			if (i % 3 == 0)
			{
				Vector2 m = a.Lerp(b, Fx.Rand(0.4f, 0.6f));
				Fx.After(front, d + 0.07f, () =>
				{
					Glow.Glint(front, m, 0.5f);
					Glow.Flash(front, m, new Color(1f, 0.35f, 0.4f), h * 0.25f, 0.15f);
				});
				Phantom.Shards(front, m, 4, 250f * u, 600f * u, dir, 60f, 0.25f, 400f * u, 12f * u, d + 0.07f);   // 交错处迸的金属火花
			}
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
