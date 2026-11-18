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

	private static Shader? _slashArc;

	/// <summary>
	/// 由里向外的横劈（着色器画的刀光）：刀走在一个倾斜的水平椭圆上（rx / ry，tilt），刀头角度 head 沿椭圆从左后方扫到右前方（角度递减，y 朝下 π/2 = 正前方）。
	/// 越靠近镜头（椭圆下沿）刀身越宽越亮，远处细而暗；刀身白热的芯 + 红边 + 墨黑的外缘，噪声沿刀的走向拉成速度纹；
	/// 刀头一团亮，尾巴（tail 弧度）越往后越淡、被噪声蚀掉；刀弧内侧刚扫过的地方一层淡红拖影。flip = 朝左时水平镜像。
	/// </summary>
	private static Shader SlashArcShader => _slashArc ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec2 size = vec2(1000.0, 600.0);
uniform float rx = 300.0;
uniform float ry = 100.0;
uniform float tilt = 0.0;
uniform float head = 3.6;
uniform float tail = 2.2;
uniform float alpha = 1.0;
uniform float clock = 0.0;
uniform float seed = 0.0;
uniform float flip = 0.0;
uniform float steel = 0.0;      // 0 = 暗红刀光，1 = 钢铁（铁黑底 + 红边，钢铁风暴）
uniform float nscale = 1.0;     // 噪声密度倍数：小刀要更稀，不然同样多的噪声挤在小面积里就是一粒粒颗粒
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y); }
void fragment() {
	vec2 px = (UV - 0.5) * size;
	if (flip > 0.5) px.x = -px.x;
	float c = cos(-tilt), s = sin(-tilt);
	vec2 p = vec2(c * px.x - s * px.y, s * px.x + c * px.y);
	vec2 q = vec2(p.x / rx, p.y / ry);
	float r = length(q);
	float th = atan(q.y, q.x);
	float behind = mod(th - head + 12.566370, 6.283185);        // 刀头已经扫过这里多少弧度
	float k = clamp(behind / max(tail, 0.001), 0.0, 1.0);
	float near = clamp((sin(th) + 1.0) * 0.5, 0.0, 1.0);         // 1 = 离镜头最近（椭圆下沿）；钳住，不然 pow 遇到 -0.0000001 会算出 NaN
	// 沿刀走向拉长的速度纹（角度方向频率高、径向低）
	float n1 = noise(vec2(th * 22.0 * nscale + clock * 3.0, (r - 1.0) * 18.0 + seed));
	float n2 = noise(vec2(th * 60.0 * nscale - clock * 6.0, (r - 1.0) * 40.0 - seed));
	float w = mix(0.035, 0.2, pow(near, 1.4)) * (1.0 - 0.55 * k);   // 半宽（单位 = 椭圆半径）
	w *= smoothstep(0.0, 0.35, behind) * 0.85 + 0.15 * smoothstep(0.0, 0.08, behind);   // 刀头收成尖锋（用户 2026-11-18：末端太平）
	float dr = r - 1.0 - (n1 - 0.5) * w * 0.5 * (1.0 - 0.85 * steel);   // 钢铁：边缘平滑，不被噪声扰成毛边
	float prof = 1.0 - clamp(abs(dr) / max(w, 0.001), 0.0, 1.0);
	float on = step(behind, tail);
	float chew = smoothstep(0.15, 0.55, n1 * 0.6 + n2 * 0.4 + (1.0 - k) * 0.8 - 0.2);
	float fade = on * (1.0 - smoothstep(0.35, 1.0, k)) * mix(chew, mix(1.0, chew, k * k), steel);   // 钢铁：整片变淡，只在尾端才被啃
	// 亮边在刀的前锋（外沿）、只在刀头附近一段（用户 2026-11-18：刀身正中一道白线，多刀叠起来很脏 → 不要中线白芯）
	float lead = smoothstep(0.15, 0.85, dr / max(w, 0.001)) * smoothstep(1.0, 0.6, dr / max(w, 0.001));
	float core = lead * exp(-behind / 0.45) * (0.3 + 0.7 * near) * fade;   // 一定要乘 fade：不然整个椭圆一圈都亮，远侧那段在画面上就是一条直白线
	float bodyA = smoothstep(0.0, 0.25, prof) * fade;
	float streak = smoothstep(0.6, 0.92, n2) * smoothstep(0.0, 0.6, prof) * fade * 0.7 * (1.0 - steel);
	float headGlow = exp(-behind / 0.08) * exp(-abs(r - 1.0) / max(w * 1.5, 0.02)) * on;   // 只在刀身附近亮（不限径向的话整条径向线都会亮成一道直光）
	float inner = 0.0;   // 刀弧内侧的扇形拖影去掉了（压扁的椭圆上扇形边会变成一道直线，用户：不要这些白线）
	// 所有东西只留在刀身附近（硬遮罩，防任何一项漏出来画成大片）
	float ring = 1.0 - smoothstep(0.0, 0.25, abs(r - 1.0));
	bodyA *= ring; core *= ring; streak *= ring; headGlow *= ring;
	// 细节（用户 2026-11-18：太亮太卡通 → 压暗、丰富细节）：
	// 顺着刀走的几道细刻痕、被噪声打出的暗纹、沿刃忽闪的细小亮点
	float across = dr / max(w, 0.001);
	float grooves = 0.0;   // 刻痕去掉（平行细线在多刀时也显脏）
	float darkVein = smoothstep(0.5, 0.85, noise(vec2(th * 35.0 - clock * 2.0, across * 2.5 + seed))) * (1.0 - steel);
	vec2 cellId = floor(vec2(th * 90.0, across * 3.0));
	float glint = step(0.988, hash(cellId + floor(clock * 6.0))) * prof * fade * ring * near * exp(-behind / 0.6) * (1.0 - steel);
	vec3 deep = mix(vec3(0.05, 0.0, 0.012), vec3(0.025, 0.02, 0.025), steel);
	vec3 mid = mix(vec3(0.55, 0.03, 0.09), vec3(0.32, 0.3, 0.33), steel);
	vec3 edge = mix(vec3(0.85, 0.08, 0.16), vec3(0.95, 0.07, 0.15), steel);
	vec3 hot = mix(vec3(1.0, 0.8, 0.78), vec3(1.0, 0.92, 0.9), steel);
	vec3 col = deep * bodyA
		+ mid * bodyA * (0.35 + 0.35 * near) * (1.0 - 0.55 * darkVein)
		+ edge * (streak * 0.45 + grooves * 0.35 + smoothstep(0.75, 0.0, prof) * bodyA * (0.35 + 0.45 * steel))
		+ hot * (core * 0.6 + headGlow * 0.55 + glint * 0.7)
		+ mix(mid, hot, 0.35) * smoothstep(-0.7, 1.0, across) * bodyA * (1.0 - smoothstep(0.3, 1.0, k)) * (0.25 + 0.25 * steel);   // 整片的柔和渐变：前锋那侧亮一点（不是一条线）
	// 钢铁：一片打磨过的金属刃 —— 背侧铁黑、往前锋渐亮到冷白的反光，前锋一道红边，越靠近刀头越亮
	float sheenS = smoothstep(-0.9, 0.85, across) * (1.0 - smoothstep(0.85, 1.0, across));
	float rimS = exp(-abs(across - 0.9) / 0.12);
	col = mix(col, deep * bodyA + vec3(0.42, 0.42, 0.46) * sheenS * bodyA * (0.4 + 0.6 * near) * (1.0 - 0.6 * k)
		+ edge * rimS * bodyA * (0.7 + 0.3 * near) + hot * (headGlow * 0.6 + core * 0.5), steel);
	float a = clamp(bodyA * 0.95 + headGlow * 0.5 + glint * 0.5, 0.0, 1.0) * alpha;
	COLOR = vec4(col * alpha, a);
}" };

	/// <summary>放一道由里向外的横劈：t0 起，swing 秒刀头从 3.6 扫到 0.1 弧度（ease out），之后 fade 秒尾巴追上去收掉。</summary>
	internal static void SlashArc(Control layer, Vector2 ctr, float rx, float ry, float tilt, float f, float t0, float swing, float fade, float h, float steel = 0f, float gain = 1f)
	{
		Vector2 size = new(rx * 2.8f, ry * 2.8f + h * 0.5f);
		Node2D root = Fx.Root(layer, ctr, "P5SlashArc", t0 + swing + fade + 0.1);
		ShaderMaterial m = new() { Shader = SlashArcShader };
		m.SetShaderParameter("size", size);
		m.SetShaderParameter("rx", rx);
		m.SetShaderParameter("ry", ry);
		m.SetShaderParameter("tilt", tilt);
		m.SetShaderParameter("flip", f < 0f ? 1f : 0f);
		m.SetShaderParameter("seed", Fx.Rand(0f, 20f));
		m.SetShaderParameter("steel", steel);
		m.SetShaderParameter("nscale", Mathf.Clamp(rx / (h * 0.88f), 0.4f, 1.2f));   // 按刀的大小换算噪声密度
		ColorRect r = new() { Size = size, Position = -size * 0.5f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(r);
		root.Visible = false;
		Fx.Animate(root, t0, swing + fade, t =>
		{
			root.Visible = true;
			float s = t * (swing + fade);
			float e = Fx.OutCubic(Mathf.Min(1f, s / swing));
			m.SetShaderParameter("head", Mathf.Lerp(3.6f, -0.35f, e));   // 扫过右侧再往上挑一点收住
			// 尾巴：扫的时候拖得长，扫完往刀头收
			float tail = Mathf.Lerp(0.6f, 2.4f, Mathf.Min(1f, s / (swing * 0.6f))) * (1f - Fx.InCubic(Ease5.Seg(s, swing, fade)));
			m.SetShaderParameter("tail", Mathf.Max(0.02f, tail));
			m.SetShaderParameter("clock", s * 4f);
			m.SetShaderParameter("alpha", gain * Mathf.Min(1f, s / 0.02f) * (1f - Ease5.Seg(s, swing + fade * 0.6f, fade * 0.4f)));
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
		float peak = fling0 + 0.045f;                           // 刀头扫到正前方最近处（OutCubic 0.25 处）
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

		// ── 甩：由里向外的横劈（用户 2026-11-18：屏幕由里向外的横劈、带一点倾斜，用着色器做）
		// 刀走在一个倾斜的水平椭圆上（透视下的一圈）：从他左后方（远、细）扫过身前最近的一点（近、粗、亮）到右前方。
		float rx = h * 0.88f, ry = h * 0.32f, tilt = -0.2f;
		Vector2 ctr = hand + new Vector2(f * h * 0.45f, h * 0.06f);
		const float th0 = 3.6f, th1 = -0.35f, swing = 0.17f;   // 刀头的角度（y 朝下，π/2 = 正前方最近处），从左后方扫到右前方
		Func<float, Vector2> onArc = th => ctr + new Vector2(Mathf.Cos(th) * rx * f, Mathf.Sin(th) * ry).Rotated(tilt * f);
		Func<float, float> headAt = s => Mathf.Lerp(th0, th1, Fx.OutCubic(Mathf.Clamp((s - fling0) / swing, 0f, 1f)));
		SlashArc(front, ctr, rx * 1.04f, ry * 1.06f, tilt + 0.04f, f, fling0 + 0.04f, swing, 0.28f, h, 0f, 0.45f);   // 延迟半拍、更暗的残刀（层次）
		SlashArc(front, ctr, rx, ry, tilt, f, fling0, swing, 0.32f, h);
		// 刀头一路往外迸碎光、火星（实时）
		for (int i = 0; i < 8; i++)
		{
			float x = (i + 0.5f) / 8f, d = fling0 + swing * x;
			float th = Mathf.Lerp(th0, th1, Fx.OutCubic(x));
			Vector2 at = onArc(th);
			float near = (Mathf.Sin(th) + 1f) * 0.5f;
			Vector2 tangent = (onArc(th - 0.05f) - at).Normalized();
			Phantom.Shards(front, at, 2 + (int)(4 * near), 200f * u, (400f + 700f * near) * u, tangent + new Vector2(0f, 0.3f), 45f, 0.32f, 800f * u, (8f + 12f * near) * u, d);
			if (near > 0.5f) Glow.Sparks(front, at, 5, 80f * u, 300f * u, 0.4f, new[] { new Color(1f, 0.7f, 0.7f), new Color(0.85f, 0.05f, 0.14f) }, 500f * u, 0.03f * u, 0.08f * u, tangent, 40f);
		}
		Fx.After(front, peak, () =>
		{
			HitStop.Request(0.06);
			Fx.Shake(2);
			Vector2 near = onArc(1.57f);
			Glow.Flash(front, near, new Color(1f, 0.75f, 0.72f), h * 0.3f, 0.08f);
			Glow.Flash(back, near, new Color(0.45f, 0.02f, 0.07f), h * 1.8f, 0.4f);
			// 不放十字光：它的横向光芒会留成一道从刀尖往外伸的白线（用户 2026-11-18：不要这些白线）
		});
		Func<float, float, Vector2> arcAtT = (e, s) => onArc(Mathf.Lerp(2.4f, 0.6f, e));   // 出弹用：刀扫过的身前那一段

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
					bf.Root.Rotation = Mathf.LerpAngle(0f, ang, Mathf.Min(1f, (s - back0) / 0.06f)) + Mathf.Tau * f * Ease5.OutQuint((s - back0) / fly);   // 飞回来时翻一整圈
					bf.Root.Scale = Vector2.One * (1f - 0.4f * e);
				}
			});
			// 出现：空气撕开那一下一个极短的顿帧、红色柔光 + 小星光、迸一把火星
			Fx.After(front, t0 + 0.02f, () =>
			{
				Sfx.Take("gx_ping", 0.12f);
				if (k == 0) HitStop.Request(0.025);
				Glow.Flash(front, at, Hot, h * 0.22f, 0.08f);
				Glow.Flash(back, at, new Color(1f, 0.15f, 0.22f), h * 0.6f, 0.35f);
				Glow.Glint(front, at + new Vector2(f * h * 0.08f, 0f), 0.55f);
				Phantom.Shards(front, at, 8, 240f * u, 640f * u, new Vector2(f, -0.1f), 75f, 0.3f, 400f * u, 12f * u);
				Glow.Sparks(front, at, 10, 80f * u, 320f * u, 0.45f, new[] { new Color(1f, 0.75f, 0.7f), new Color(0.9f, 0.08f, 0.16f) }, 400f * u, 0.03f * u, 0.07f * u, Vector2.Up, 120f);
			});
			// 子弹身上一团红光（出现到落手一直跟着）
			Node2D aura = Fx.Root(front, at, "P5BulletAura", back0 + fly + 0.05);
			Glow.SoftSprite(aura, Vector2.Zero, new Color(1f, 0.12f, 0.2f, 0.55f), h * 0.35f / 32f);
			aura.Visible = false;
			Fx.Animate(aura, t0, back0 + fly - t0, t =>
			{
				aura.Visible = true;
				float s = t0 + t * (back0 + fly - t0);
				aura.GlobalPosition = s < back0 ? at : at.Lerp(hand, Fx.InCubic((s - back0) / fly));
				aura.Modulate = new Color(1f, 1f, 1f, (0.6f + 0.4f * Mathf.Sin(s * 30f + k)) * (1f - Ease5.Seg(s, back0 + fly - 0.04f, 0.04f)));
			});
			// 收回：飞的时候翻一整圈、沿途洒火星，拖红疾光
			StreakSharp(front, e => at.Lerp(hand, Fx.InCubic(e)), back0, fly, h * 0.45f, 16f * u, Red, 1.5f);
			for (int sIdx = 0; sIdx < 3; sIdx++)
			{
				float e = 0.25f + 0.25f * sIdx;
				Vector2 pp = at.Lerp(hand, Fx.InCubic(e));
				Glow.Sparks(front, pp, 4, 40f * u, 160f * u, 0.3f, new[] { new Color(1f, 0.6f, 0.6f), new Color(0.8f, 0.05f, 0.14f) }, 300f * u, 0.025f * u, 0.06f * u, Vector2.Up, 160f);
			}
			Fx.After(front, back0 + fly, () =>
			{
				Sfx.Take("gx_tick", 0.14f);
				Glow.Flash(front, hand, Hot, h * 0.2f, 0.08f);
				Glow.Flash(back, hand, new Color(1f, 0.15f, 0.22f), h * 0.5f, 0.22f);
				Phantom.Shards(front, hand, 6, 200f * u, 480f * u, new Vector2(-f, -0.4f), 70f, 0.26f, 400f * u, 11f * u);
				if (c.SelfTint is { } st) Fx.Animate(front, 0, 0.18, tt => st(Colors.White.Lerp(new Color(1.45f, 0.8f, 0.85f), (1f - tt) * 0.8f)));
			});
		}
		float done = home0 + 0.06f * (n - 1) + 0.17f;
		Fx.After(front, done + 0.02f, () =>
		{
			Sfx.Take("gx_tock", 0.16f);
			Glow.Flash(front, hand, Hot, h * 0.3f, 0.1f);
			Glow.Flash(back, hand, Red, h * 1.2f, 0.35f);
			Glow.Sparks(front, hand, 16, 120f * u, 480f * u, 0.5f, new[] { new Color(1f, 0.8f, 0.75f), new Color(0.9f, 0.08f, 0.16f) }, 500f * u, 0.03f * u, 0.08f * u, Vector2.Up, 140f);
			Glow.Glint(front, hand, 0.8f);   // 不用十字光（横竖光芒会拉成白线）
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
