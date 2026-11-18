using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 墨之刃第三版「甩墨出刃」（2026-11-18，用户：炸墨太差 → 换创意：三维空间里从手里朝镜头甩墨，然后出 2/3 颗子弹；
/// 要做好拼接、不要有贴图感、不要只靠贴图，要像腐蚀波那样和着色器特效组合）。Codex 的四套序列帧在 art/p5/fx/v4/ink_fling/。
/// 序列帧统一走 InkFlipShader：相邻两帧交叉淡入（不一跳一跳）、每格边缘羽化（甩墨那几帧生成时有硬截边）、噪声蚀散、半透明边缘透红光。
/// </summary>
public static partial class SkillRedo5
{
	private static Shader? _inkFlip;

	private static Shader InkFlipShader => _inkFlip ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D sheet : filter_linear;
uniform float frame = 0.0;      // 0～15，小数部分 = 和下一帧交叉淡入
uniform float erode = 0.0;      // 0→1 被噪声一块块蚀掉
uniform float alpha = 1.0;
uniform float rim = 0.45;       // 半透明边缘透的红光（太重墨就变粉了）
uniform float seed = 0.0;
uniform float flip = 0.0;       // 1 = 水平翻转
uniform float clock = 0.0;      // 液面流动（暗纹在里面涌、轻轻起伏）
uniform float dark = 0.0;       // 0～1 整体压暗（贴图本身偏亮偏塑料，当辅助层时压下去）
uniform float veins = 1.0;      // 里面流动的红色暗纹强度（手绘流纹的图本身就有纹，压低）
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y); }
vec4 cell(float f, vec2 cu) {
	float fi = clamp(floor(f), 0.0, 15.0);
	vec2 o = vec2(mod(fi, 4.0), floor(fi / 4.0));
	return texture(sheet, (o + clamp(cu, 0.004, 0.996)) / 4.0);
}
void fragment() {
	vec2 cu = vec2(flip > 0.5 ? 1.0 - UV.x : UV.x, UV.y);
	float k = fract(frame);
	// 液面轻轻起伏（贴图本身是静的，让它像在流）
	vec2 wob = vec2(noise(vec2(cu.y * 6.0 + clock * 2.0, seed)) - 0.5, noise(vec2(cu.x * 6.0 - clock * 2.3, seed + 3.0)) - 0.5) * 0.012;
	vec4 a = cell(frame, cu + wob), b = cell(min(frame + 1.0, 15.0), cu + wob);
	vec4 c = mix(a, b, smoothstep(0.0, 1.0, k));
	// 格子边缘羽化（生成图里有几帧墨团碰到格子边、被切出硬边）
	float n = noise(cu * 9.0 + seed) * 0.6 + noise(cu * 31.0 - seed) * 0.4;
	// 格子边缘：按离边的距离 + 噪声不规则地淡掉（只做直线羽化的话，淡出的边界本身还是一条直线）
	float dEdge = min(min(cu.x, 1.0 - cu.x), min(cu.y, 1.0 - cu.y));
	float edge = smoothstep(0.02, 0.24, dEdge + (n - 0.5) * 0.16);
	float kept = smoothstep(erode - 0.06, erode + 0.06, n);
	vec3 col = c.rgb;
	float lum = dot(col, vec3(0.3, 0.5, 0.2));
	col = mix(col, col * vec3(0.9, 0.55, 0.6), smoothstep(0.55, 1.0, lum) * 0.55);              // 塑料感的白高光压下去
	float vein = smoothstep(0.55, 0.85, noise(vec2(cu.x * 14.0 - clock * 3.0, cu.y * 5.0 + seed)) * 0.7 + noise(cu * 28.0 + clock * 1.5) * 0.3);
	col += vec3(0.9, 0.06, 0.14) * vein * 0.45 * c.a * veins;                                          // 里面流动的红色暗纹
	col += vec3(0.85, 0.05, 0.12) * clamp(c.a * (1.0 - c.a) * 4.0, 0.0, 1.0) * rim * (0.5 + 0.5 * n);
	col *= mix(1.0, 0.45, dark);
	COLOR = vec4(col, c.a * edge * kept * alpha);
}" };

	private static Shader? _whip;

	/// <summary>
	/// 甩出去的能量鞭（Line2D：UV.x 0 = 尾 → 1 = 头，UV.y 横跨）：实时渲染的主体 —— 翻滚流动的暗红黑，外沿一道亮红、浪头泛白，尾巴被噪声蚀散。预乘混合。
	/// </summary>
	private static Shader WhipShader => _whip ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float clock = 0.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; } return v; }
void fragment() {
	float u = UV.x, v = UV.y * 2.0 - 1.0;
	// 噪声沿鞭身方向拉长（横向频率高、纵向低）= 一条条流纹，不是一颗颗斑点
	float n = fbm(vec2(u * 5.0 - clock * 4.0 + seed, v * 1.6));
	float n2 = noise(vec2(u * 60.0 - clock * 14.0, v * 2.2 + seed));
	float prof = 1.0 - abs(v);
	float body = smoothstep(0.1, 0.55, prof + (n - 0.5) * 0.6) * smoothstep(0.25, 0.65, n + u * 0.7 - 0.05);
	float rim = smoothstep(0.55, 0.95, abs(v) + (n2 - 0.5) * 0.25) * smoothstep(0.0, 0.3, prof + 0.3) * smoothstep(0.1, 0.5, u) * body;   // 两侧亮边
	float crest = smoothstep(0.85, 1.0, u) * smoothstep(0.3, 0.8, prof);
	float st = smoothstep(0.62, 0.9, n2) * prof * smoothstep(0.2, 0.8, u);
	vec3 col = vec3(0.07, 0.0, 0.015) * body + vec3(0.95, 0.06, 0.16) * (rim * 1.4 + st * 0.7) + vec3(1.0, 0.82, 0.8) * (crest * 0.8 + st * st * 0.25);
	float a = clamp(body * 0.9 + rim * 0.4 + crest * 0.6, 0.0, 1.0) * alpha;
	COLOR = vec4(col * alpha, a);
}" };

	/// <summary>能量鞭：沿 path(0→1) 抽出去，t0 起、dur 秒抽到头，之后 fade 秒尾巴追上去收掉；越往头越粗（透视：往镜头那边）。返回驱动节点。</summary>
	private static void Whip(Control layer, Control back, Func<float, Vector2> path, float t0, float dur, float fade, float width)
	{
		Node2D root = Fx.Root(layer, Vector2.Zero, "P5Whip", t0 + dur + fade + 0.1);
		ShaderMaterial m = new() { Shader = WhipShader };
		m.SetShaderParameter("seed", Fx.Rand(0f, 20f));
		var curve = new Curve();
		curve.AddPoint(new Vector2(0f, 0.04f));
		curve.AddPoint(new Vector2(0.6f, 0.6f));
		curve.AddPoint(new Vector2(0.94f, 1f));
		curve.AddPoint(new Vector2(1f, 0.6f));
		var line = new Line2D { Width = width, Texture = Glow.Soft, TextureMode = Line2D.LineTextureMode.Stretch, Material = m, JointMode = Line2D.LineJointMode.Round, WidthCurve = curve };
		var halo = new Line2D { Width = width * 1.5f, Texture = Glow.Soft, TextureMode = Line2D.LineTextureMode.Stretch, Material = Fx.Additive, JointMode = Line2D.LineJointMode.Round, WidthCurve = curve,
			Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { new Color(0.8f, 0.03f, 0.12f, 0f), new Color(1f, 0.1f, 0.2f, 0.45f) } } };
		root.AddChild(halo);
		root.AddChild(line);
		root.GlobalPosition = Vector2.Zero;
		root.Visible = false;
		const int n = 30;
		var pts = new Vector2[n];
		Fx.Animate(root, t0, dur + fade, t =>
		{
			root.Visible = true;
			float s = t * (dur + fade);
			float head = Ease5.OutQuint(Mathf.Min(1f, s / dur));
			float tailK = s < dur ? 0f : Fx.InCubic((s - dur) / fade);
			float tail = Mathf.Max(0f, head - 0.55f) + (head - Mathf.Max(0f, head - 0.55f)) * tailK * 0.98f;
			for (int i = 0; i < n; i++) pts[i] = path(Mathf.Lerp(tail, head, i / (n - 1f)));
			line.Points = pts;
			halo.Points = pts;
			m.SetShaderParameter("clock", s * 2f);
			m.SetShaderParameter("alpha", Mathf.Min(1f, s / 0.03f) * (1f - Ease5.Seg(s, dur + fade * 0.5f, fade * 0.5f)));
			halo.Modulate = new Color(1f, 1f, 1f, 1f - tailK);
		});
	}

	/// <summary>一块序列帧（ColorRect 居中在 root 上）：返回 root（位置 / 缩放 / 旋转自己动）和材质（frame / erode / alpha 自己驱动）。size = 一格画多大。</summary>
	private static (Node2D Root, ShaderMaterial M)? InkFlip(Control layer, string sheet, Vector2 at, float size, bool flipX, double life)
	{
		if (Fx.Tex(sheet) is not Texture2D tex) return null;
		Node2D root = Fx.Root(layer, at, "P5InkFlip", life);
		ShaderMaterial m = new() { Shader = InkFlipShader };
		m.SetShaderParameter("sheet", tex);
		m.SetShaderParameter("seed", Fx.Rand(0f, 20f));
		m.SetShaderParameter("flip", flipX ? 1f : 0f);
		ColorRect r = new() { Size = Vector2.One * size, Position = -Vector2.One * size * 0.5f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(r);
		return (root, m);
	}

	/// <summary>
	/// 墨之刃（稀有：生成 2/3 张带「墨」附魔的小刀）「甩出刃弧」（第四版，Codex v3 手绘序列帧：art/p5/fx/v4/ink_fling_v3）。
	/// v2 图被否（像果冻、鞭子轮廓畸形、没有空间力量感）→ 换成刀光一路的手绘流纹：蓄力速度纹、透视的新月弧、撕开空气迸出的子弹。
	/// 慢（蓄力 0～0.22）：四周压暗，几道锐利的速度纹旋着收进他手里拧成一个光结（序列帧）+ 细光线往手里收 + 红光、身上淡红；
	/// 快（甩 0.2～0.55，主拍）：一道新月形的刃弧从他手边甩出、扫向镜头（序列帧整体放大并往右下推 = 近大远小），
	///     沿弧一道白热疾光、一道红疾光抽过去（运动模糊感），沿路洒出实时碎光；弧最宽那一刻顿帧、震屏、白闪、十字光；
	/// 慢（出弹 0.45～0.8）：弧上 2/3 处空气被撕开、「啪」地迸出子弹（出弹序列帧），每颗一亮；
	/// 收（0.8～1.05）：子弹转向他手，拖着红疾光一颗颗飞回来入膛（「嗒」×n、「咔」），手上白闪、身上红残影一胀。
	/// </summary>
	public static void BladeOfInk3(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = BackOf(c), front = c.Front;
		int n = Mathf.Clamp(c.Count > 0 ? c.Count : 2, 1, 4);
		const float gatherEnd = 0.22f, fling0 = 0.2f, flingDur = 0.42f, burst0 = 0.46f, burstDur = 0.26f, home0 = 0.8f;
		float peak = fling0 + 0.11f;                            // 横扫到头（swing 0.13s 快到底）
		Vector2 hand = c.Hand, mid = c.Feet + new Vector2(0f, -h * 0.55f);
		bool flip = f < 0f;
		S("gx_whoosh", 0.1f, 0.02f);
		S("gx_whoosh", 0.28f, fling0 + 0.04f);
		S("gx_slice", 0.22f, peak);
		Dim(back, mid + new Vector2(f * h * 0.5f, 0f), new Vector2(h * 4f, h * 3f), home0 + 0.6, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.12f)) * (1f - Ease5.Seg(s, 0.72f, 0.28f)) * 0.62f);

		// ── 蓄力
		Gather(front, hand, h, u, gatherEnd, 14, 0.55f);
		Glow.Flash(back, hand, new Color(0.85f, 0.04f, 0.13f), h * 0.9f, gatherEnd + 0.15f);
		if (InkFlip(front, "gather_streak_sheet", hand, h * 0.7f, flip, gatherEnd + 0.1) is { } g)
		{
			g.M.SetShaderParameter("veins", 0.2f);
			g.M.SetShaderParameter("rim", 0.2f);
			Fx.Animate(g.Root, 0, gatherEnd + 0.06, t =>
			{
				float s = t * (gatherEnd + 0.06f);
				g.M.SetShaderParameter("frame", Mathf.Min(15f, 15f * s / gatherEnd));
				g.M.SetShaderParameter("clock", s * 2f);
				g.Root.Rotation = -s * 3f * f;
				g.M.SetShaderParameter("alpha", 1f - Ease5.Seg(s, gatherEnd, 0.06f));
			});
		}
		Fx.After(front, gatherEnd - 0.02f, () =>
		{
			Glow.Flash(front, hand, Hot, h * 0.25f, 0.08f);
			Glow.Glint(front, hand, 0.7f);
		});

		// ── 甩：横劈（用户 2026-11-18：要横劈，不要刀刃从小放大的感觉；削弱塑料感、丰富细节）
		// 刃弧一出来就是完整大小，转平、压扁成横向的 ⌣（凸面朝镜头），从他身后左侧横扫过身前到右前方。
		float arcSize = h * 1.4f;
		Vector2 arcAt = hand + new Vector2(f * h * 0.5f, -h * 0.02f);
		Vector2 squash = new(1.4f, 0.62f);                    // 屏幕方向压扁：横着的一刀
		float arcRot = -0.65f * f;                             // 图里凸面朝左下 → 转到朝下（朝镜头）
		float travel = h * 0.35f;                              // 一刀横扫过去的位移
		Vector2[] arcPts = { new(-0.22f, -0.3f), new(-0.2f, 0.02f), new(-0.05f, 0.2f), new(0.2f, 0.27f), new(0.42f, 0.2f) };
		Func<float, Vector2> local = e =>
		{
			float x = e * (arcPts.Length - 1);
			int i0 = Mathf.Min((int)x, arcPts.Length - 2);
			Vector2 q = arcPts[i0].Lerp(arcPts[i0 + 1], x - i0);
			return new Vector2(q.X * f, q.Y).Rotated(arcRot) * arcSize * squash;
		};
		const float swing = 0.13f;                             // 横扫要多久
		Func<float, float> slide = s => Ease5.OutQuint(Mathf.Clamp((s - fling0) / swing, 0f, 1f));
		Func<float, float, Vector2> arcAtT = (e, s) => arcAt + new Vector2(f * travel * (slide(s) - 0.7f), 0f) + local(e);
		// 刀身后压一条暗带（让刃更锐、不浮在画面上）
		Dim(back, arcAt + new Vector2(0f, h * 0.05f), new Vector2(arcSize * 2.2f, h * 0.9f), flingDur + 0.3,
			s => Ease5.Seg(s, 0f, 0.1f) * (1f - Ease5.Seg(s, 0.4f, 0.6f)) * 0.5f);
		// 主刃 + 一层延迟半拍的淡红残影
		for (int layer = 0; layer < 2; layer++)
		{
			bool ghost = layer == 1;
			float lag = ghost ? 0.045f : 0f;
			if (InkFlip(front, "fling_arc_sheet", arcAt, arcSize, flip, fling0 + flingDur + 0.15) is not { } fl) continue;
			Node2D outer = Fx.Root(front, arcAt, "P5InkArc", fling0 + flingDur + 0.15);
			fl.Root.GetParent().RemoveChild(fl.Root);
			outer.AddChild(fl.Root);
			fl.Root.Position = Vector2.Zero;
			fl.Root.Rotation = arcRot;
			outer.Scale = squash;
			fl.M.SetShaderParameter("veins", 0.1f);
			fl.M.SetShaderParameter("rim", 0.15f);
			fl.M.SetShaderParameter("dark", ghost ? 0.3f : 0.1f);
			outer.Visible = false;
			Fx.Animate(outer, fling0 + lag, flingDur, t =>
			{
				outer.Visible = true;
				float s = fling0 + lag + t * flingDur;
				// 一出来就是完整的刃（从第 7 帧开始），扫过去之后沿流向变细、从尾巴蚀散
				fl.M.SetShaderParameter("frame", 7f + 8f * Ease5.Seg(t, 0.25f, 0.75f));
				fl.M.SetShaderParameter("clock", t * 3f);
				fl.M.SetShaderParameter("erode", Ease5.Seg(t, 0.45f, 0.55f) * 0.85f);
				fl.M.SetShaderParameter("alpha", (ghost ? 0.4f : 1f) * Mathf.Min(1f, t * 12f) * (1f - Ease5.Seg(t, 0.75f, 0.25f)));
				outer.GlobalPosition = arcAt + new Vector2(f * travel * (slide(s - lag) - 0.7f), 0f) - (ghost ? new Vector2(f * h * 0.06f, 0f) : Vector2.Zero);
				outer.Scale = squash * new Vector2(1f + 0.08f * Mathf.Exp(-t * 10f), 1f);
			});
		}
		// 拖影：几道沿刃平行的细疾光一起横扫过去（运动模糊）；前锋一道很细的白热线
		for (int k2 = 0; k2 < 4; k2++)
		{
			float off = (k2 - 1.5f) * h * 0.035f;
			Color col = k2 == 1 ? new Color(1f, 0.92f, 0.9f) : new Color(1f, 0.15f, 0.22f, 0.9f);
			StreakSharp(front, e => arcAtT(e, fling0 + swing * e) + new Vector2(0f, off), fling0 + 0.01f * k2, swing + 0.06f, h * (0.5f + 0.1f * k2), (k2 == 1 ? 6f : 14f) * u, col, k2 == 1 ? 2f : 1.5f);
		}
		// 刃的尾缘一路迸实时碎光；结束时一缕缕速度纹散掉
		for (int i = 0; i < 7; i++)
		{
			float x = (i + 0.5f) / 7f, d = fling0 + swing * x;
			Vector2 at = arcAtT(x, d);
			Phantom.Shards(front, at, 4, 220f * u, 640f * u, new Vector2(-f * 0.3f, 0.6f), 60f, 0.34f, 900f * u, 13f * u, d);
			if (i % 2 == 0) Glow.Sparks(front, at, 5, 60f * u, 220f * u, 0.45f, new[] { new Color(1f, 0.6f, 0.6f), new Color(0.8f, 0.05f, 0.14f) }, 300f * u, 0.03f * u, 0.08f * u, new Vector2(f, 0f), 50f);
		}
		for (int i = 0; i < 5; i++)
		{
			float x = Fx.Rand(0.1f, 0.9f), d = fling0 + flingDur * 0.55f + 0.03f * i;
			Vector2 a = arcAtT(x, d), b = a + new Vector2(f * h * Fx.Rand(0.4f, 0.7f), Fx.Rand(-0.05f, 0.08f) * h);
			StreakSharp(front, e => a.Lerp(b, e), d, 0.22f, h * 0.35f, 5f * u, new Color(1f, 0.3f, 0.35f), 1.4f);
		}
		Fx.After(front, peak, () =>
		{
			HitStop.Request(0.06);
			Fx.Shake(2);
			Vector2 near = arcAtT(0.8f, peak);
			Glow.Flash(front, near, Hot, h * 0.45f, 0.1f);
			Glow.Flash(back, near, Red, h * 2.0f, 0.4f);
			Cross(front, near, h * 0.5f);
			Phantom.Shards(front, near, 12, 500f * u, 1100f * u, new Vector2(f, 0.2f), 50f, 0.36f, 800f * u, 18f * u);
		});

		// ── 出弹：弧上 n 处空气被撕开、迸出子弹，再一颗颗飞回手里
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float x = n == 1 ? 0.5f : k / (n - 1f);
			Vector2 at = arcAtT(0.3f + 0.55f * x, fling0 + flingDur) + new Vector2(0f, -h * 0.1f);
			float t0 = burst0 + 0.05f * k, back0 = home0 + 0.06f * k, fly = 0.17f;
			if (InkFlip(front, "bullet_burst_sheet", at, h * 0.6f, flip, back0 + fly + 0.05) is not { } bf) continue;
			bf.M.SetShaderParameter("veins", 0f);
			bf.M.SetShaderParameter("rim", 0.15f);
			bf.Root.Visible = false;
			Fx.Animate(bf.Root, t0, back0 + fly - t0, t =>
			{
				bf.Root.Visible = true;
				float s = t0 + t * (back0 + fly - t0);
				if (s < back0)
				{
					bf.M.SetShaderParameter("frame", Mathf.Min(15f, 15f * (s - t0) / burstDur));
					bf.Root.GlobalPosition = at;
					bf.Root.Rotation = 0f;
					bf.Root.Scale = Vector2.One;
				}
				else
				{
					float e = Fx.InCubic((s - back0) / fly);
					bf.M.SetShaderParameter("frame", 15f);
					bf.Root.GlobalPosition = at.Lerp(hand, e);
					Vector2 dv = hand - at;
					float ang = f > 0 ? dv.Angle() : dv.Angle() - Mathf.Pi;
					bf.Root.Rotation = Mathf.LerpAngle(0f, ang, Mathf.Min(1f, (s - back0) / 0.06f));
					bf.Root.Scale = Vector2.One * (1f - 0.4f * e);
				}
			});
			Fx.After(front, t0 + 0.02f, () =>
			{
				Sfx.Take("gx_ping", 0.12f);
				Glow.Flash(front, at, Hot, h * 0.22f, 0.08f);
				Glow.Flash(front, at, new Color(1f, 0.25f, 0.3f), h * 0.45f, 0.25f);
				Phantom.Shards(front, at, 6, 240f * u, 600f * u, new Vector2(f, 0f), 70f, 0.28f, 300f * u, 12f * u);
			});
			StreakSharp(front, e => at.Lerp(hand, Fx.InCubic(e)), back0, fly, h * 0.45f, 14f * u, Red, 1.5f);
			Fx.After(front, back0 + fly, () =>
			{
				Sfx.Take("gx_tick", 0.14f);
				Glow.Flash(front, hand, Hot, h * 0.18f, 0.08f);
			});
		}
		float done = home0 + 0.06f * (n - 1) + 0.17f;
		Fx.After(front, done + 0.02f, () =>
		{
			Sfx.Take("gx_tock", 0.16f);
			Glow.Flash(back, hand, Red, h * 1.0f, 0.3f);
			Cross(front, hand, h * 0.35f);
			EchoPulse(c, 0.05f, 0.3f, 0.6f);
		});
		if (c.SelfTint is { } tint)
			Fx.Animate(front, 0, done + 0.5, t =>
			{
				float s = t * (done + 0.5f);
				float kk = 0.25f * Ease5.Seg(s, 0f, gatherEnd) * (1f - Ease5.Seg(s, peak, 0.2f)) + 0.8f * Mathf.Exp(-Mathf.Pow((s - peak) / 0.07f, 2f));
				tint(Colors.White.Lerp(new Color(1.5f, 0.75f, 0.8f), kk));
			});
	}
}
