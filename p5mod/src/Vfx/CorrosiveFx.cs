using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 腐蚀波（稀有，毒流重点牌：开了之后这回合每抽一张牌都给全体上毒）。
/// 2026-11-18 第八版「咒怨卷浪」（用户画了路线草图）。之前被否：贴地毒潮、咒爪齐撕、横扫的浪墙。
/// · 每抽一张牌（OnDraw）：每个敌人身上同一刻卷起一道咒怨浪 —— 像过山车：从它左下方（Joker 那边）往右上爬起、翻过头顶，
///   在最高点放慢、顿一小下，然后猛地加速斜着往右下拍到它身上；拍中那一刻 = 这次上毒（CorrosiveDrawPatch 把原版上毒推迟到这一刻，确认感同步、不滞后）：
///   顿帧、震屏、白闪 + 暗红、咒毒在它身上一胀炸开、碎光顺着拍下去的方向溅出去、身体暗一下。
///   浪身是着色器画的（翻滚的暗红黑、外沿一道被啃得坑坑洼洼的亮边、浪头最亮、尾巴蚀成碎块散掉）；Codex 生的浪身纹理到了以后只当着色器的输入，不直接贴。
/// · 打出（Plunge）：亚森出手的一刻，Joker 身前一团咒毒炸开 + 十字光。
/// · 持续（Aura）：Joker 身上隔一会儿泛一下很淡的暗红（不放在地上）。
/// </summary>
public static class CorrosiveFx
{
	/// <summary>一个敌人：中心、头顶、宽度、本体（染色用）。</summary>
	public record struct Target(Vector2 At, Vector2 Top, float Width, Node2D? Body);

	private static ulong _suppressUntil, _auraNext;

	/// <summary>抽牌带来的上毒正在由卷浪表现：PoisonFx.Apply 这段时间不放普通落点。</summary>
	public static bool Suppressing => Stage.NowMs() < _suppressUntil;

	private static readonly Color Hot = new(1f, 0.95f, 0.94f), Red = new(1f, 0.1f, 0.18f), Deep = new(0.45f, 0.02f, 0.08f);

	private static float U(Control layer) => Fx.Screen(layer).Y / 1080f;

	// ───────────────────────── 卷浪的路线（照用户的草图，单位 = 敌人尺度 R，x 朝远离 Joker，y 朝下）─────────────────────────

	private static readonly Vector2[] Route =
	{
		new(-0.95f, 0.75f), new(-0.35f, 0.5f), new(0.3f, 0.2f), new(0.8f, -0.25f), new(0.62f, -0.66f),   // 红：从左下往右上爬起，拐上去
		new(0.05f, -0.86f),                                                                            // 最高点（红蓝交界）：在这里放慢、顿一下
		new(-0.3f, -0.76f), new(-0.5f, -0.42f), new(-0.42f, 0.0f), new(-0.08f, 0.38f), new(0.45f, 0.66f), new(0.95f, 0.88f),   // 蓝：翻下来，斜着拍下去
	};
	private const int TopIndex = 5, HitIndex = 9;   // 最高点、拍中敌人身体的那一点

	/// <summary>节奏（秒）：爬升（越往上越慢）→ 顶上顿一下 → 拍下去（越来越快）。</summary>
	private const float Climb = 0.2f, Hang = 0.08f, Slam = 0.13f;

	private static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
	{
		float t2 = t * t, t3 = t2 * t;
		return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
	}

	/// <summary>把路线按弧长采样成 n 段：返回点、每个控制点对应的弧长比例。</summary>
	private static (List<Vector2> Pts, List<float> Len, float[] At) Sample(int per = 10)
	{
		var pts = new List<Vector2>();
		var at = new float[Route.Length];
		for (int i = 0; i < Route.Length - 1; i++)
		{
			Vector2 p0 = Route[Mathf.Max(0, i - 1)], p1 = Route[i], p2 = Route[i + 1], p3 = Route[Mathf.Min(Route.Length - 1, i + 2)];
			at[i] = pts.Count;
			for (int k = 0; k < per; k++) pts.Add(CatmullRom(p0, p1, p2, p3, k / (float)per));
		}
		at[^1] = pts.Count;
		pts.Add(Route[^1]);
		var len = new List<float> { 0f };
		for (int i = 1; i < pts.Count; i++) len.Add(len[i - 1] + pts[i].DistanceTo(pts[i - 1]));
		float total = len[^1];
		for (int i = 0; i < len.Count; i++) len[i] /= total;
		for (int i = 0; i < at.Length; i++) at[i] = len[(int)at[i]];
		return (pts, len, at);
	}

	private static readonly (List<Vector2> Pts, List<float> Len, float[] At) Path = Sample();

	/// <summary>弧长比例 p 处的点（0～1）。</summary>
	private static Vector2 PointAt(float p)
	{
		var (pts, len, _) = Path;
		p = Mathf.Clamp(p, 0f, 1f);
		int i = 1;
		while (i < len.Count - 1 && len[i] < p) i++;
		float k = (p - len[i - 1]) / Mathf.Max(1e-5f, len[i] - len[i - 1]);
		return pts[i - 1].Lerp(pts[i], k);
	}

	/// <summary>浪头在第 s 秒走到哪（弧长比例）。爬升 ease-out（越往上越慢），顶上慢慢挪一点，拍下去 ease-in（越来越快）。</summary>
	private static float HeadAt(float s)
	{
		float top = Path.At[TopIndex];
		const float creep = 0.035f;
		if (s < Climb) { float e = s / Climb; return top * (1f - (1f - e) * (1f - e)); }
		if (s < Climb + Hang) return top + creep * ((s - Climb) / Hang);
		float x = Mathf.Min(1f, (s - Climb - Hang) / Slam);
		return top + creep + (1f - top - creep) * x * x;
	}

	/// <summary>浪头拍到敌人身体（HitIndex）的时刻（秒）。</summary>
	public static float HitTime
	{
		get
		{
			float top = Path.At[TopIndex], hit = Path.At[HitIndex];
			const float creep = 0.035f;
			float x = Mathf.Sqrt(Mathf.Clamp((hit - top - creep) / (1f - top - creep), 0f, 1f));
			return Climb + Hang + Slam * x;
		}
	}

	private static Shader? _curl;

	/// <summary>
	/// 浪身（Line2D：UV.x 沿长度 0 = 尾 → 1 = 头，UV.y 横跨）：翻滚的暗红黑，外沿（v &lt; 0 那侧）一道被啃得坑坑洼洼的亮边，浪头最亮（白），
	/// 尾巴蚀成碎块散掉。预乘混合：暗的部分真的压暗。body_tex：Codex 生的浪身纹理（亮度当密度用），没有就纯噪声。
	/// </summary>
	private static Shader CurlShader => _curl ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float clock = 0.0;
uniform float seed = 0.0;
uniform float alpha = 1.0;
uniform float use_tex = 0.0;
uniform sampler2D body_tex : repeat_enable, filter_linear;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; } return v; }
void fragment() {
	float u = UV.x, v = UV.y * 2.0 - 1.0;
	float n = fbm(vec2(u * 8.0 - clock * 3.0 + seed, v * 3.5));
	float n2 = noise(vec2(u * 16.0 - clock * 5.0, v * 4.0 + seed));
	float prof = 1.0 - abs(v);
	float crest = smoothstep(0.82, 1.0, u) * smoothstep(0.1, 0.7, prof + (n2 - 0.5) * 0.4);
	if (use_tex > 0.5) {
		// Codex 的浪身（本身就是红白黑上色好的）：沿浪身往后滚动，尾巴被噪声蚀散、浪头泛白
		vec4 tx = texture(body_tex, vec2(u * 1.3 - clock * 0.55 + seed + (n - 0.5) * 0.08, mix(0.2, 0.84, UV.y) + (n2 - 0.5) * 0.04));   // 浪身在图里只占中间 0.25～0.8
		float keep = smoothstep(0.2, 0.6, n * 1.1 + u * 0.75 - 0.1) * smoothstep(0.0, 0.12, u);
		keep *= smoothstep(0.0, 0.35, prof + (n2 - 0.5) * 0.35);                   // 两侧边缘羽化 + 被噪声啃掉，不留贴图的硬边
		float ta = tx.a * keep * alpha;
		float lum = dot(tx.rgb, vec3(0.3, 0.5, 0.2));
		vec3 tc = mix(tx.rgb, vec3(1.0, 0.55, 0.58), smoothstep(0.75, 1.0, lum) * 0.35);   // 纯白的高光收一点，往粉红里压
		tc += vec3(0.5, 0.02, 0.06) * smoothstep(0.15, 0.5, lum) * 0.5;               // 里面一点红的自发光
		tc = mix(tc, vec3(1.0, 0.92, 0.9), crest * 0.5);
		float ca = crest * 0.35 * alpha;
		COLOR = vec4(tc * ta + vec3(1.0, 0.85, 0.82) * ca * (1.0 - ta), clamp(ta + ca * (1.0 - ta), 0.0, 1.0));
	} else {
	float body = smoothstep(0.1, 0.5, prof + (n - 0.5) * 0.9);
	body *= smoothstep(0.3, 0.7, n * 1.2 + u * 0.55 - 0.1);            // 被蚀得一块一块的，尾巴散掉
	float st = smoothstep(0.55, 0.85, noise(vec2(u * 30.0 - clock * 7.0, v * 6.0 + seed))) * prof * smoothstep(0.1, 0.5, u);   // 顺着流向的亮丝
	float rim = smoothstep(0.45, 0.9, -v + (n2 - 0.5) * 0.5) * smoothstep(0.0, 0.3, u);   // 外沿亮边
	vec3 col = vec3(0.13, 0.0, 0.03) * body + vec3(0.95, 0.06, 0.16) * (rim * 1.1 + body * 0.3 * u + st * 0.8)
		+ vec3(1.0, 0.82, 0.8) * (crest * 0.85 + pow(rim, 3.0) * 0.6);
	float a = clamp(body * 0.8 + rim * 0.55 + crest * 0.6 + st * 0.4, 0.0, 1.0) * alpha;
	COLOR = vec4(col * alpha, a);
	}
}" };

	private static Shader? _halo;

	/// <summary>浪身后面垫的一层柔红辉光（加法）：横向高斯、沿长度尾淡头亮、噪声起伏。</summary>
	private static Shader HaloShader => _halo ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float clock = 0.0;
uniform float alpha = 1.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y); }
void fragment() {
	float u = UV.x, v = UV.y * 2.0 - 1.0;
	float g = exp(-v * v * 3.0) * smoothstep(0.0, 0.5, u) * (0.75 + 0.5 * noise(vec2(u * 10.0 - clock * 4.0, v * 2.0)));
	COLOR = vec4(vec3(0.95, 0.08, 0.18) * g * 0.55 * alpha, 1.0);
}" };

	/// <summary>Codex 生的浪身纹理（art/p5/fx/v4/corrosive/curse_wave_body_01 / 02，左右无缝），两张随机用。</summary>
	private static Texture2D? BodyTex => Fx.Tex(Fx.Rng.Next(2) == 0 ? "curse_wave_body_01" : "curse_wave_body_02");

	/// <summary>
	/// 拍击溅射（Codex 的 16 帧序列 curse_splash_sheet：从左上往右下拍、往右下呈扇形溅开）：at 为中心，size 边长，facing &lt; 0 时水平翻转。
	/// 本身就是红白黑上色好的；放的时候一边播一边微微放大。
	/// </summary>
	private static void Splash(Control layer, Vector2 at, float size, float facing, float dur = 0.42f)
	{
		if (Fx.Tex("curse_splash_sheet") is not Texture2D sheet) return;
		Node2D root = Fx.Root(layer, at, "P5CurseSplash", dur + 0.05);
		var sp = new Sprite2D { Texture = sheet, Hframes = 4, Vframes = 4, Frame = 0, Centered = true };
		float cell = sheet.GetWidth() / 4f;
		Node2D under = Fx.Root(layer, at, "P5CurseSplashGlow", dur + 0.05);
		Glow.SoftSprite(under, Vector2.Zero, new Color(0.95f, 0.08f, 0.16f, 0.8f), size * 0.9f / 32f);
		root.AddChild(sp);
		Fx.Animate(under, 0, dur, t => under.Modulate = new Color(1f, 1f, 1f, (1f - t) * (1f - t)));
		Fx.Animate(root, 0, dur, t =>
		{
			sp.Frame = Mathf.Min(15, (int)(t * 16f));
			sp.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.5f, 0.5f));
			float k = size / cell * (0.9f + 0.25f * Fx.OutCubic(t));
			root.Scale = new Vector2(k * (facing < 0 ? -1f : 1f), k);
		});
	}

	/// <summary>
	/// 一个敌人身上的卷浪。center = 敌人中心，h / w = 高 / 宽，facing = 远离 Joker 的方向。s 秒时浪头在 HeadAt(s)，浪身拖在后面 TailLen。
	/// 拍中（HitTime）的打击由调用方放。
	/// </summary>
	private static void Curl(Control layer, Vector2 center, float h, float w, float facing, float power)
	{
		float u = U(layer);
		float R = Mathf.Max(h * 1.05f, w * 0.85f) * Mathf.Clamp(power, 0.6f, 1.4f);
		Vector2 hit = Route[HitIndex];
		Vector2 origin = center - new Vector2(hit.X * facing, hit.Y) * R + new Vector2(0f, h * 0.05f);
		Vector2 Map(Vector2 q) => origin + new Vector2(q.X * facing, q.Y) * R;
		float total = Climb + Hang + Slam, fade = 0.16f;
		const float tail = 0.42f;
		Node2D root = Fx.Root(layer, origin, "P5CorrodeCurl", total + fade + 0.1);
		ShaderMaterial m = new() { Shader = CurlShader };
		m.SetShaderParameter("seed", Fx.Rand(0f, 20f));
		if (BodyTex is Texture2D tex) { m.SetShaderParameter("body_tex", tex); m.SetShaderParameter("use_tex", 1f); }
		var line = new Line2D
		{
			Width = R * 0.78f, Texture = Glow.Soft, TextureMode = Line2D.LineTextureMode.Stretch, Material = m,
			JointMode = Line2D.LineJointMode.Round, Antialiased = true,
			WidthCurve = new Curve(),
		};
		line.WidthCurve.AddPoint(new Vector2(0f, 0.05f));
		line.WidthCurve.AddPoint(new Vector2(0.55f, 0.75f));
		line.WidthCurve.AddPoint(new Vector2(0.92f, 1f));
		line.WidthCurve.AddPoint(new Vector2(1f, 0.55f));
		// 后面垫一层更宽的柔红辉光（让浪有体积、会发光，不是一条平的带子）
		ShaderMaterial hm = new() { Shader = HaloShader };
		var halo = new Line2D
		{
			Width = R * 1.35f, Texture = Glow.Soft, TextureMode = Line2D.LineTextureMode.Stretch, Material = hm,
			JointMode = Line2D.LineJointMode.Round, WidthCurve = line.WidthCurve,
		};
		root.AddChild(halo);
		root.AddChild(line);
		root.GlobalPosition = Vector2.Zero;
		root.Position = Vector2.Zero;
		line.GlobalPosition = Vector2.Zero;
		// 浪头一团跟着走的红光
		Node2D glow = Fx.Root(layer, Map(Route[0]), "P5CorrodeGlow", total + 0.1);
		Glow.SoftSprite(glow, Vector2.Zero, new Color(1f, 0.1f, 0.18f, 0.7f), R * 0.7f / 32f);
		Glow.SoftSprite(glow, Vector2.Zero, new Color(1f, 0.85f, 0.85f, 0.6f), R * 0.18f / 32f);
		// 一路上浪头往后甩火星 / 毒滴、沿路留一串淡红光点；拍下去那段甩得更多
		for (int k = 0; k < 9; k++)
		{
			float sAt = total * (0.08f + 0.1f * k);
			Vector2 at = Map(PointAt(HeadAt(sAt)));
			Vector2 prev = Map(PointAt(HeadAt(Mathf.Max(0f, sAt - 0.02f))));
			Vector2 back = (prev - at).LengthSquared() > 1f ? (prev - at).Normalized() : new Vector2(-facing, 0f);
			bool slamming = sAt > Climb + Hang;
			Phantom.Shards(layer, at, slamming ? 6 : 3, 160f * u, 460f * u, back + new Vector2(0f, -0.3f), 45f, 0.32f, 900f * u, (slamming ? 14f : 10f) * u, sAt);
			Glow.Flash(layer, at, new Color(0.9f, 0.06f, 0.15f), R * 0.45f, 0.3f, sAt);
			if (k % 3 == 1) CardSkillFx.Drop(layer, at, center.Y + h * 0.75f, sAt, h * 2f, u);
		}
		// 顶上顿住那一下：浪头一记星光
		Fx.After(layer, Climb + Hang * 0.3f, () =>
		{
			Vector2 top = Map(PointAt(HeadAt(Climb + Hang * 0.3f)));
			Glow.Glint(layer, top, 0.8f);
			Glow.Flash(layer, top, new Color(1f, 0.75f, 0.75f), R * 0.35f, 0.12f);
		});
		const int n = 34;
		var pts = new Vector2[n];
		Fx.Animate(root, 0, total + fade, t =>
		{
			float s = t * (total + fade);
			float head = HeadAt(Mathf.Min(s, total));
			float back = Mathf.Max(0f, head - tail * Mathf.Min(1f, 0.35f + s / Climb)) + (s > total ? (s - total) / fade * tail : 0f);
			back = Mathf.Min(back, head - 0.01f);
			for (int i = 0; i < n; i++) pts[i] = Map(PointAt(Mathf.Lerp(back, head, i / (float)(n - 1))));
			line.Points = pts;
			halo.Points = pts;
			hm.SetShaderParameter("clock", s * 2.2f);
			hm.SetShaderParameter("alpha", Mathf.Min(1f, s / 0.05f) * (1f - Ease5.Seg(s, total - 0.02f, fade + 0.02f)));
			m.SetShaderParameter("clock", s * 2.2f);
			m.SetShaderParameter("alpha", Mathf.Min(1f, s / 0.04f) * (1f - Ease5.Seg(s, total, fade)));
			if (GodotObject.IsInstanceValid(glow))
			{
				glow.GlobalPosition = pts[n - 1];
				glow.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, s / 0.05f) * (1f - Ease5.Seg(s, total - 0.03f, 0.06f)));
			}
		});
	}

	/// <summary>咒毒炸开：涂毒那团翻滚的咒毒在 at 一下胀起来（0.07s）再炸开散掉；碎光往外迸、毒液往下滴到 groundY。size = 直径。</summary>
	private static void Burst(Control layer, Vector2 at, float size, float groundY, float power)
	{
		float u = U(layer);
		Node2D root = Fx.Root(layer, at, "P5VenomBurst", 0.5);
		ColorRect q = CardSkillFx.VenomBlob(root, size);
		ShaderMaterial m = (ShaderMaterial)q.Material;
		float seed = Fx.Rand(0f, 30f);
		Fx.Animate(root, 0, 0.42, t =>
		{
			float s = t * 0.42f;
			float swell = Ease5.OutExpo(Mathf.Min(1f, s / 0.07f));
			float pop = Ease5.Seg(s, 0.07f, 0.3f);
			root.Scale = Vector2.One * (0.6f + 0.5f * swell + 0.7f * Fx.OutCubic(pop));
			m.SetShaderParameter("clock", seed + s * 4f);
			m.SetShaderParameter("grow", swell);
			m.SetShaderParameter("alpha", 1f - Fx.OutCubic(pop));
		});
		Glow.Flash(layer, at, Hot, size * 0.55f, 0.1f);
		Glow.Flash(layer, at, new Color(0.9f, 0.05f, 0.14f), size * 1.5f, 0.35f);
		Phantom.Shards(layer, at, (int)(12 * power), 380f * u, 1000f * u * power, Vector2.Up, 180f, 0.4f, 900f * u, 18f * u * power, 0.06f);
		for (int i = 0; i < 3; i++)
			CardSkillFx.Drop(layer, at + new Vector2(Fx.Rand(-0.25f, 0.25f) * size, Fx.Rand(0f, 0.2f) * size), groundY, 0.1f + 0.08f * i, size * 2f, u);
	}

	/// <summary>打出：亚森出手的一刻，Joker 身前一团咒毒炸开 + 十字光。</summary>
	public static void Plunge(Control layer, Vector2 claw, Vector2 ground)
	{
		float u = U(layer);
		float facing = ground.X >= claw.X ? 1f : -1f;
		Vector2 at = ground + new Vector2(facing * 120f * u, -280f * u);
		Sfx.Play(Sfx.Slam, 0.6f);
		Sfx.Play(Sfx.SubBoom, 0.6f);
		Sfx.Play(Sfx.Curse, 0.45f);
		HitStop.Request(0.07);
		Stage.ShakeDir?.Invoke(3, 90f);
		Burst(layer, at, 320f * u, ground.Y, 1.4f);
		SkillRedo5.Cross(layer, at, 340f * u);
	}

	/// <summary>持续：每帧调用（腐蚀波生效时）；约每 0.8s 在 Joker 身上泛一下很淡的暗红（不放在地上）。</summary>
	public static void Aura(Control layer, Vector2 feet)
	{
		ulong now = Stage.NowMs();
		if (now < _auraNext)
		{
			return;
		}
		_auraNext = now + 800;
		float u = U(layer);
		Glow.Flash(layer, feet + new Vector2(0f, -260f * u), Deep, 420f * u, 0.7f);
	}

	/// <summary>
	/// 抽到一张牌（CorrosiveDrawPatch）：每个敌人身上同时卷起一道浪，HitTime 秒后拍下去。返回拍中要等几秒 ——
	/// 调用方把原版上毒推迟这么久，让数字 / 毒层和拍击同一刻出现。first = 这批连抽的第一下（更重）。
	/// </summary>
	/// <summary>
	/// 连抽里第 2 张起（用户 2026-11-18：一次抽好几张不要每张都放一整道浪 → 只放一次完整的浪，浪快结束的尾巴上变成快速的侵蚀，
	/// 但每抽一张挂一次毒的节奏要明显）：每个敌人身上一下短促的「咬蚀」——暗红一口从边上啃进去（小一号的溅射 + 往里收的碎光）、身体一暗、
	/// 轻顿帧、一声闷的「噗」，越到后面越快越密。返回挂毒要等几秒（很短）。index = 这是连抽里第几张（1 = 第二张）。
	/// </summary>
	public static float Erode(Control layer, float facing, IReadOnlyList<Target> targets, int index)
	{
		const float bite = 0.1f;
		_suppressUntil = Stage.NowMs() + (ulong)((bite + 0.3f) * 1000f);
		if (targets.Count == 0) return 0f;
		float u = U(layer);
		float k = Mathf.Max(0.55f, 1f - 0.08f * (index - 1));
		Fx.After(layer, bite, () =>
		{
			Sfx.Play(Sfx.CurseSoft, 0.32f * k);
			Sfx.Play(Sfx.SubBoom, 0.2f * k);
			HitStop.Request(0.02);
			foreach (Target tt in targets)
			{
				float h = Mathf.Max(80f, (tt.At - tt.Top).Length());
				// 每一口咬在身上不同的地方（边上往里啃）
				float a = index * 2.4f + tt.At.X * 0.01f;
				Vector2 off = new Vector2(Mathf.Cos(a) * tt.Width * 0.3f, Mathf.Sin(a) * h * 0.35f);
				Vector2 at = tt.At + off;
				Glow.Flash(layer, at, new Color(0.9f, 0.05f, 0.14f), Mathf.Max(tt.Width, h) * 0.8f * k, 0.22f);
				Splash(layer, at, Mathf.Max(tt.Width, h) * 0.75f * k, off.X >= 0f ? facing : -facing, 0.3f);
				Phantom.Shards(layer, at - off.Normalized() * h * 0.25f, 6, 300f * u, 700f * u, -off.Normalized(), 50f, 0.25f, 300f * u, 14f * u);
				BodyTint.Pulse(tt.Body, new Color(0.62f, 0.38f, 0.48f), 0.06, 0.25);
			}
		});
		return bite;
	}

	public static float OnDraw(Control layer, Vector2 feet, float facing, IReadOnlyList<Target> targets, bool first = true)
	{
		float hitAt = HitTime;
		_suppressUntil = Stage.NowMs() + (ulong)((hitAt + 0.35f) * 1000f);
		if (targets.Count == 0)
		{
			return 0f;
		}
		float u = U(layer);
		Sfx.Play(Sfx.SwishLow, first ? 0.45f : 0.32f);
		Fx.After(layer, hitAt - 0.06f, () => Sfx.Play(Sfx.SwishLow, first ? 0.35f : 0.25f));
		foreach (Target t in targets)
		{
			Target tt = t;
			float h = Mathf.Max(80f, (t.At - t.Top).Length());
			Curl(layer, t.At, h, t.Width, facing, first ? 1f : 0.85f);
		}
		Fx.After(layer, hitAt, () =>
		{
			// 拍中：敌人那一片一下压暗（分量），再慢慢回来
			float minX = float.MaxValue, maxX = float.MinValue, cy = 0f;
			foreach (Target tt in targets) { minX = Mathf.Min(minX, tt.At.X - tt.Width); maxX = Mathf.Max(maxX, tt.At.X + tt.Width); cy += tt.At.Y; }
			cy /= targets.Count;
			SkillRedo5.Dim(layer, new Vector2((minX + maxX) * 0.5f, cy), new Vector2(maxX - minX + 300f * u, 700f * u), 0.5,
				s => (1f - Ease5.Seg(s, 0.05f, 0.95f)) * (first ? 0.4f : 0.25f));
			Sfx.Play(Sfx.SubBoom, first ? 0.5f : 0.32f);
			Sfx.Play(Sfx.CurseSoft, first ? 0.6f : 0.42f);
			HitStop.Request(first ? 0.07 : 0.035);
			Stage.ShakeDir?.Invoke(first ? 2 : 1, 0f);
			Vector2 slamDir = new Vector2(facing * 0.85f, 0.5f).Normalized();
			foreach (Target tt in targets)
			{
				float h = Mathf.Max(80f, (tt.At - tt.Top).Length());
				Glow.Flash(layer, tt.At, Hot, Mathf.Max(tt.Width, h) * 0.6f, 0.1f);
				Glow.Flash(layer, tt.At, new Color(0.9f, 0.05f, 0.14f), Mathf.Max(tt.Width, h) * 1.4f, 0.35f);
				Splash(layer, tt.At + slamDir * h * 0.15f, Mathf.Max(tt.Width, h) * (first ? 1.7f : 1.45f), facing);
				for (int d = 0; d < 3; d++)
					CardSkillFx.Drop(layer, tt.At + new Vector2(Fx.Rand(-0.25f, 0.25f) * tt.Width, Fx.Rand(0f, 0.2f) * h), tt.At.Y + h * 0.75f, 0.1f + 0.08f * d, h * 2f, u);
				Phantom.Shards(layer, tt.At, first ? 14 : 9, 500f * u, 1200f * u, slamDir, 40f, 0.4f, 900f * u, 22f * u);
				BodyTint.Pulse(tt.Body, new Color(0.7f, 0.42f, 0.52f), 0.12, 0.4);
			}
		});
		return hitAt;
	}
}
