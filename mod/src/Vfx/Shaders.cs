using Godot;

namespace P3Makoto.Vfx;

/// <summary>代码内联的 canvas_item 着色器。所有着色器都用 ColorRect 的 UV（0~1）作画。</summary>
public static class Shaders
{
	private static Shader Make(string code) => new Shader { Code = code };

	private static Shader? _ring;

	/// <summary>冲击波圆环：radius 0~1，width 环宽，alpha 整体透明度。</summary>
	public static Shader Ring => _ring ??= Make(@"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(0.45, 0.8, 1.0, 1.0);
uniform float radius = 0.3;
uniform float width = 0.05;
uniform float alpha = 1.0;
void fragment() {
	float d = length(UV * 2.0 - 1.0);
	float core = smoothstep(width, 0.0, abs(d - radius));
	float glow = smoothstep(width * 5.0, 0.0, abs(d - radius)) * 0.35;
	float edge = smoothstep(1.0, 0.8, d);          // 扩到边缘前淡掉，不被方框裁出硬边
	COLOR = vec4(tint.rgb * (1.0 + core), (core + glow) * alpha * tint.a * edge);
}");

	private static Shader? _flash;

	/// <summary>径向闪光。</summary>
	public static Shader Flash => _flash ??= Make(@"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(0.8, 0.95, 1.0, 1.0);
uniform float alpha = 1.0;
void fragment() {
	float d = length(UV * 2.0 - 1.0);
	float a = pow(clamp(1.0 - d, 0.0, 1.0), 2.2);
	COLOR = vec4(tint.rgb, a * alpha * tint.a);
}");

	private static Shader? _refract;

	/// <summary>
	/// 水面折射冲击波：扭曲身后画面并带 RGB 色散，环上叠一层冷色高光。
	/// radius 0~1，width 环宽，strength 扭曲强度（屏幕 UV），alpha 整体强度。
	/// </summary>
	public static Shader Refract => _refract ??= Make(@"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec4 tint : source_color = vec4(0.7, 0.92, 1.0, 1.0);
uniform float radius = 0.3;
uniform float width = 0.12;
uniform float strength = 0.025;
uniform float alpha = 1.0;
void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float d = length(uv);
	// 高斯环形剖面（无硬边）+ 越靠近四边越淡（扩到边缘时自然消散，不会被方框裁掉）
	float x = (d - radius) / max(width, 0.0001);
	float edge = smoothstep(1.0, 0.72, d);
	float k = exp(-x * x * 2.5) * edge;
	vec2 dir = d > 0.0001 ? uv / d : vec2(0.0);
	// 折射方向随环的内外侧反向（像真正的水波：一侧压缩一侧拉伸）
	vec2 off = dir * (-x * exp(-x * x * 2.5)) * edge * strength * alpha;
	float r = texture(screen_tex, SCREEN_UV - off * 1.12).r;
	float g = texture(screen_tex, SCREEN_UV - off).g;
	float b = texture(screen_tex, SCREEN_UV - off * 0.88).b;
	float rim = exp(-x * x * 9.0) * edge;
	vec3 col = vec3(r, g, b) + tint.rgb * rim * 0.22 * alpha;
	COLOR = vec4(col, clamp(k * 3.0, 0.0, 1.0) * clamp(alpha * 4.0, 0.0, 1.0));
}");

	private static Shader? _pillar;

	/// <summary>竖直光柱：中心亮、两侧柔和衰减，顶部渐隐，带向上流动的条纹。</summary>
	public static Shader Pillar => _pillar ??= Make(@"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(0.25, 0.55, 1.0, 1.0);
uniform float alpha = 1.0;
void fragment() {
	float x = abs(UV.x * 2.0 - 1.0);
	float core = pow(clamp(1.0 - x, 0.0, 1.0), 3.0);
	float halo = pow(clamp(1.0 - x, 0.0, 1.0), 1.2) * 0.35;
	float fadeTop = smoothstep(0.0, 0.6, UV.y);
	float fadeBottom = smoothstep(1.0, 0.92, UV.y);
	float streak = 0.75 + 0.25 * sin(UV.y * 40.0 + TIME * 14.0 + UV.x * 6.0);
	float a = (core + halo) * fadeTop * fadeBottom * streak * alpha;
	COLOR = vec4(tint.rgb + vec3(core * 0.6), a * tint.a);
}");

	private static Shader? _magicCircle;

	/// <summary>
	/// 预备出牌时脚下的法阵：外双环 + 旋转刻度 + 内圈虚线环 + 中心柔光。
	/// 用 ColorRect 压扁 Y 轴得到透视感。
	/// </summary>
	public static Shader MagicCircle => _magicCircle ??= Make(@"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(0.35, 0.7, 1.0, 1.0);
uniform float alpha = 1.0;
uniform float spin = 0.35;
float ring(float d, float r, float w) { return smoothstep(w, 0.0, abs(d - r)); }
void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float d = length(uv);
	float a = atan(uv.y, uv.x);
	float c = 0.0;
	c += ring(d, 0.96, 0.012);
	c += ring(d, 0.86, 0.008) * 0.8;
	float ticks = step(fract((a + TIME * spin) / 6.2831853 * 48.0), 0.18);
	c += ticks * step(0.87, d) * step(d, 0.95) * 0.9;
	float dash = step(fract((a - TIME * spin * 1.6) / 6.2831853 * 12.0), 0.6);
	c += ring(d, 0.62, 0.01) * dash;
	float hex = 0.0;
	for (int i = 0; i < 6; i++) {
		float ang = float(i) * 1.0471976 + TIME * spin * 0.5;
		vec2 n = vec2(cos(ang), sin(ang));
		hex += smoothstep(0.012, 0.0, abs(dot(uv, n) - 0.54)) * step(d, 0.62);
	}
	c += hex * 0.6;
	c += pow(clamp(1.0 - d, 0.0, 1.0), 3.0) * 0.35;
	c *= step(d, 1.0);
	COLOR = vec4(tint.rgb * (0.8 + c * 0.6), clamp(c, 0.0, 1.0) * alpha * tint.a);
}");

	private const string HueShiftBody = @"
uniform float shift = 3.0;      // 绕灰轴旋转的弧度：橙（约 30°）→ 蓝青（约 205°）
uniform float boost = 1.1;
vec3 hue(vec3 c, float a) {
	const vec3 k = vec3(0.57735);
	float ca = cos(a);
	return c * ca + cross(k, c) * sin(a) + k * dot(k, c) * (1.0 - ca);
}
void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	c.rgb = clamp(hue(c.rgb, shift) * boost, 0.0, 1.0);
	COLOR = c;
}";

	private static Shader? _hueMix, _hueAdd;

	/// <summary>色相旋转（普通混合）：把原版橙色的君王之刃整体换成 P3 的蓝。COLOR 已含 modulate，所以发光 tween 也会被换色。</summary>
	public static Shader HueShift => _hueMix ??= Make("shader_type canvas_item;\n" + HueShiftBody);

	/// <summary>色相旋转（叠加混合），用于原本就是 Add 混合的发光 / 粒子。</summary>
	public static Shader HueShiftAdd => _hueAdd ??= Make("shader_type canvas_item;\nrender_mode blend_add;\n" + HueShiftBody);

	private static Shader? _menu;

	/// <summary>
	/// P3R 风格背景：深蓝夜空 + 巨大的月亮与光晕 + 地平线下的水面（月光倒影碎成水平光带、细波纹缓慢流动）+ 暗角。
	/// aspect = 宽 / 高；moon = 月亮中心（UV）；horizon = 地平线高度（UV.y）。
	/// </summary>
	public static Shader Menu => _menu ??= Make(@"
shader_type canvas_item;
uniform float aspect = 2.13;
uniform vec2 moon = vec2(0.6, 0.2);
uniform float moon_r = 0.085;
uniform float horizon = 0.7;

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 5; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; }
	return v;
}
vec3 sky(vec2 uv) {
	vec3 top = vec3(0.005, 0.015, 0.06), low = vec3(0.03, 0.11, 0.30);
	vec3 c = mix(top, low, smoothstep(0.0, horizon, uv.y));
	vec2 d = (uv - moon) * vec2(aspect, 1.0);
	float r = length(d);
	float disc = smoothstep(moon_r, moon_r - 0.004, r);
	float craters = fbm(d * 18.0 + 3.0);
	c = mix(c, vec3(0.78, 0.9, 1.0) * (0.82 + 0.25 * craters), disc);
	c += vec3(0.25, 0.55, 1.0) * exp(-max(r - moon_r, 0.0) * 9.0) * 0.55 * (1.0 - disc);
	float cloud = fbm(vec2(uv.x * 2.4 + TIME * 0.006, uv.y * 7.0)) * smoothstep(horizon, 0.15, uv.y);
	c += vec3(0.06, 0.14, 0.3) * cloud * 0.6;
	float stars = step(0.9975, hash(floor(uv * vec2(900.0, 420.0)))) * smoothstep(horizon, 0.0, uv.y);
	c += stars * (0.4 + 0.6 * sin(TIME * 2.0 + hash(floor(uv * 300.0)) * 6.28)) * 0.5;
	return c;
}
void fragment() {
	vec2 uv = UV;
	vec3 c;
	if (uv.y < horizon) {
		c = sky(uv);
	} else {
		float k = (uv.y - horizon) / (1.0 - horizon);
		float wave = sin(uv.y * 260.0 / (0.25 + k) - TIME * 1.4) * 0.5 + fbm(vec2(uv.x * 9.0, uv.y * 60.0 / (0.3 + k) - TIME * 0.6)) - 0.5;
		vec2 ruv = vec2(uv.x + wave * 0.006 * (0.4 + k), 2.0 * horizon - uv.y);
		c = sky(ruv) * 0.55;
		float col = exp(-abs((uv.x - moon.x) * aspect) * (14.0 / (0.6 + k * 2.5)));
		float bands = smoothstep(0.35, 0.95, fbm(vec2(uv.x * 30.0, uv.y * 200.0 / (0.3 + k) - TIME * 0.9)));
		c += vec3(0.6, 0.85, 1.0) * col * bands * 0.9;
		c += vec3(0.1, 0.4, 0.9) * smoothstep(0.55, 0.9, wave + 0.5) * 0.06;
		c += vec3(0.3, 0.6, 1.0) * exp(-k * 40.0) * 0.35;           // 地平线的一道亮线
	}
	vec2 v = uv - 0.5;
	c *= 1.0 - dot(v, v) * 0.9;
	COLOR = vec4(c, 1.0);
}");

	private static Shader? _darkSky;

	/// <summary>
	/// 影时间的天色（虚空形态常驻）：盖在战斗背景上的半透明层，按屏幕坐标作画。
	/// 上深下浅的暗绿；地面附近两层缓慢流动的绿雾；月亮方向斜射下来的几道淡光柱；四角压暗。alpha 整体强度。
	/// moon = 月亮在屏幕上的位置（SCREEN_UV）。
	/// </summary>
	public static Shader DarkSky => _darkSky ??= Make(@"
shader_type canvas_item;
uniform float alpha = 1.0;
uniform vec2 moon = vec2(0.25, 0.2);
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; mat2 r = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)); for (int i = 0; i < 5; i++) { v += a * noise(p); p = r * p * 2.03 + 17.0; a *= 0.5; } return v; }
void fragment() {
	vec2 uv = SCREEN_UV;
	vec3 top = vec3(0.0, 0.05, 0.03), low = vec3(0.05, 0.24, 0.11);
	vec3 c = mix(top, low, smoothstep(0.0, 0.95, uv.y));
	float a = mix(0.62, 0.42, uv.y);
	// 地面附近的绿雾：两层不同速度的横向噪声带
	float fog1 = fbm(vec2(uv.x * 3.0 + TIME * 0.03, uv.y * 9.0)) * smoothstep(0.45, 0.85, uv.y);
	float fog2 = fbm(vec2(uv.x * 6.0 - TIME * 0.05, uv.y * 18.0 + 4.0)) * smoothstep(0.6, 0.95, uv.y);
	c += vec3(0.15, 0.55, 0.28) * (fog1 * 0.35 + fog2 * 0.25);
	// 从月亮斜射下来的淡光柱（角度噪声分条，缓慢摆动）
	vec2 d = uv - moon;
	float ang = atan(d.x, d.y);
	float shafts = smoothstep(0.55, 0.9, noise(vec2(ang * 9.0 + TIME * 0.08, 0.0))) * smoothstep(0.0, 0.25, length(d)) * smoothstep(1.1, 0.2, length(d));
	c += vec3(0.5, 0.9, 0.55) * shafts * 0.08;
	// 月亮周围一圈天光
	c += vec3(0.45, 0.75, 0.35) * exp(-length(d * vec2(1.78, 1.0)) * 4.0) * 0.18;
	// 暗角
	vec2 v = uv - 0.5;
	a += dot(v, v) * 0.6;
	COLOR = vec4(c, clamp(a, 0.0, 0.85) * alpha);
}");

	private static Shader? _darkMoon;

	/// <summary>
	/// 影时间的月亮（ColorRect 正方形）：黄绿色满月，fbm 环形山与明暗、边缘亮边、两圈细光环和柔光；
	/// 前面有缓慢飘过的暗绿色云带遮住月亮下半部。alpha 整体透明度。
	/// </summary>
	public static Shader DarkMoon => _darkMoon ??= Make(@"
shader_type canvas_item;
uniform float alpha = 1.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; mat2 r = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)); for (int i = 0; i < 5; i++) { v += a * noise(p); p = r * p * 2.03 + 17.0; a *= 0.5; } return v; }
void fragment() {
	vec2 p = UV - 0.5;
	float r = length(p);
	float R = 0.24;
	float disc = smoothstep(R, R - 0.004, r);
	// 月面：底色 + 大块暗斑（月海）+ 细环形山 + 球面明暗（右上亮）
	float maria = smoothstep(0.45, 0.7, fbm(p * 7.0 + 2.0));
	float craters = fbm(p * 30.0 + 7.0);
	vec3 surf = vec3(0.95, 0.98, 0.62) * (0.88 + 0.18 * craters) - vec3(0.2, 0.18, 0.12) * maria;
	float sphere = sqrt(max(0.0, 1.0 - (r * r) / (R * R)));
	surf *= 0.72 + 0.28 * sphere + 0.12 * dot(normalize(vec3(p, sphere * R)), normalize(vec3(0.4, -0.5, 0.6)));
	float rim = smoothstep(R - 0.03, R, r) * disc;
	surf += vec3(0.6, 1.0, 0.6) * rim * 0.5;
	// 光晕：柔光 + 两圈细环
	float glow = exp(-max(r - R, 0.0) * 14.0) * (1.0 - disc);
	float ring1 = smoothstep(0.004, 0.0, abs(r - 0.32)) * 0.35;
	float ring2 = smoothstep(0.003, 0.0, abs(r - 0.41)) * 0.2;
	vec3 halo = vec3(0.55, 0.95, 0.5) * (glow * 0.7 + ring1 + ring2);
	float ha = glow * 0.75 + ring1 + ring2;
	// 云带：横向拉长的噪声，缓慢向左飘，主要遮住月亮下半部
	float band = smoothstep(0.02, 0.18, p.y + 0.05) * smoothstep(0.42, 0.12, p.y);
	float cloud = smoothstep(0.42, 0.78, fbm(vec2(p.x * 3.0 + TIME * 0.025, p.y * 9.0))) * band * 0.7;
	vec3 cloudCol = vec3(0.02, 0.12, 0.06);
	vec3 col = mix(halo, surf, disc);
	float a = max(disc, ha);
	col = mix(col, cloudCol, cloud * 0.85);
	a = max(a, cloud * 0.8 * smoothstep(0.5, 0.1, r));
	// 云的上缘被月光照亮
	float lit = cloud * (1.0 - smoothstep(0.42, 0.8, fbm(vec2(p.x * 3.0 + TIME * 0.025, p.y * 9.0 - 0.4)))) * disc;
	col += vec3(0.5, 0.9, 0.4) * lit * 0.4;
	COLOR = vec4(col, a * alpha);
}");

	private static Shader? _moonPhase;

	/// <summary>
	/// 月相（月面射击）：冷白色的月亮，phase 0 = 新月（只剩一道亮边）→ 1 = 满月；被照亮部分有环形山与球面明暗，
	/// 暗面留一点"地照"的深蓝；外圈一层青色柔光（满月时更亮）。alpha 整体透明度，flash 让月面整体发白。
	/// </summary>
	public static Shader MoonPhase => _moonPhase ??= Make(@"
shader_type canvas_item;
render_mode blend_mix;
uniform float phase = 0.25;
uniform float alpha = 1.0;
uniform float flash = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; mat2 r = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)); for (int i = 0; i < 5; i++) { v += a * noise(p); p = r * p * 2.03 + 17.0; a *= 0.5; } return v; }
void fragment() {
	vec2 p = UV - 0.5;
	float R = 0.3;
	float r = length(p);
	float disc = smoothstep(R, R - 0.005, r);
	// 明暗交界线：x > cos(phase·π)·√(R²−y²) 的部分被照亮（光从右边来）
	float half_w = sqrt(max(R * R - p.y * p.y, 0.0));
	float term = cos(clamp(phase, 0.0, 1.0) * 3.14159) * half_w;
	float lit = smoothstep(term - 0.012, term + 0.012, p.x) * disc;
	float maria = smoothstep(0.45, 0.72, fbm(p * 6.0 + 3.0));
	float craters = fbm(p * 26.0 + 9.0);
	float sphere = sqrt(max(0.0, 1.0 - r * r / (R * R)));
	vec3 surf = vec3(0.86, 0.94, 1.0) * (0.86 + 0.2 * craters) - vec3(0.16, 0.12, 0.05) * maria;
	surf *= 0.75 + 0.25 * sphere;
	vec3 dark = vec3(0.04, 0.09, 0.26) * (0.8 + 0.3 * craters);
	vec3 col = mix(dark, surf, lit);
	col = mix(col, vec3(1.0), flash * disc);
	float rim = smoothstep(R - 0.025, R, r) * disc * lit;
	col += vec3(0.6, 0.9, 1.0) * rim * 0.6;
	float glow = exp(-max(r - R, 0.0) * 12.0) * (1.0 - disc) * (0.3 + 0.7 * clamp(phase, 0.0, 1.0));
	vec3 outCol = mix(vec3(0.45, 0.8, 1.0), col, disc);
	float a = max(disc, glow * 0.8);
	COLOR = vec4(outCol, a * alpha);
}");

	private static Shader? _forgeFire;

	/// <summary>
	/// 熔炉之火（ColorRect，底边是火源）：fbm 噪声扭动的火舌，白热的芯 → 金黄 → 橙 → 暗红的外焰，火舌尖端带一点青蓝（灵魂之火）。
	/// height 0~1 火焰升起的高度；alpha 整体透明度。加色混合。
	/// </summary>
	public static Shader ForgeFire => _forgeFire ??= Make(@"
shader_type canvas_item;
render_mode blend_add;
uniform float height = 1.0;
uniform float alpha = 1.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; mat2 r = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)); for (int i = 0; i < 5; i++) { v += a * noise(p); p = r * p * 2.03 + 17.0; a *= 0.5; } return v; }
void fragment() {
	vec2 uv = UV;
	float y = 1.0 - uv.y;                       // 0 = 火源（底边） → 1 = 顶
	float x = uv.x - 0.5;
	float t = TIME;
	float yy = y / max(height, 0.01);
	float n = fbm(vec2(x * 3.5, y * 2.5 - t * 2.2));
	float n2 = fbm(vec2(x * 8.0 + 3.0, y * 5.0 - t * 3.8));
	float n3 = fbm(vec2(x * 16.0 + 7.0, y * 10.0 - t * 6.0));
	// 火舌随高度左右摆动、变窄，边缘被细噪声撕碎
	float sway = (n - 0.5) * 0.45 * yy;
	float w = 0.3 * pow(max(1.0 - yy, 0.0), 0.7) + 0.01;
	float d = abs(x - sway + (n2 - 0.5) * 0.16 * yy);
	float body = smoothstep(w, w * 0.2, d);
	float tongues = smoothstep(0.28, 0.62, n2 * 0.7 + n3 * 0.3 + (1.0 - yy) * 0.55);
	float f = body * tongues * smoothstep(1.05, 0.75, yy + (n3 - 0.5) * 0.3);
	float heat = clamp(f * (1.05 - yy) * (0.55 + 0.8 * n3), 0.0, 1.0);
	vec3 c = mix(vec3(0.55, 0.05, 0.02), vec3(1.0, 0.38, 0.06), smoothstep(0.05, 0.3, heat));
	c = mix(c, vec3(1.0, 0.75, 0.25), smoothstep(0.3, 0.6, heat));
	c = mix(c, vec3(1.0, 0.97, 0.85), smoothstep(0.65, 0.95, heat));
	// 火舌尖端泛青蓝（灵魂之火）
	float tip = smoothstep(0.6, 0.95, yy) * (1.0 - smoothstep(0.2, 0.5, heat));
	c = mix(c, vec3(0.3, 0.75, 1.0), tip * 0.6);
	f = clamp(f * 1.4, 0.0, 1.0);
	COLOR = vec4(c * f, f) * alpha;
}");

	private static Shader? _galaxy;

	/// <summary>
	/// 创世纪的星系（ColorRect 正方形，加色）：白金色的核 + 两条旋转的旋臂（蓝 → 青），旋臂上散落闪烁的星点。
	/// spin 旋转角（由代码推进），birth 0~1 从中心向外长出，alpha 整体透明度。
	/// </summary>
	public static Shader Galaxy => _galaxy ??= Make(@"
shader_type canvas_item;
render_mode blend_add;
uniform float spin = 0.0;
uniform float birth = 1.0;
uniform float alpha = 1.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
void fragment() {
	vec2 p = (UV - 0.5) * vec2(1.0, 2.2);          // 压扁：斜着看的星系盘
	float r = length(p) * 2.0;
	float th = atan(p.y, p.x);
	float reach = birth * 1.05;
	float fade = smoothstep(reach, reach * 0.6, r);
	float arms = pow(0.5 + 0.5 * cos(2.0 * (th - spin) + log(r + 0.05) * 5.5), 3.0);
	float dust = noise(vec2(th * 6.0 + spin, r * 14.0)) * 0.6 + 0.4;
	float disk = exp(-r * 2.6) * arms * dust * fade;
	float core = exp(-r * 11.0) * 1.6 * smoothstep(0.0, 0.25, birth);
	float sparkle = step(0.985, hash(floor(UV * 160.0))) * arms * fade * (0.5 + 0.5 * sin(TIME * 6.0 + hash(floor(UV * 80.0)) * 6.28));
	vec3 c = vec3(1.0, 0.92, 0.7) * core + mix(vec3(0.35, 0.6, 1.0), vec3(0.4, 0.95, 1.0), r) * disk * 1.4 + vec3(1.0) * sparkle;
	float a = clamp(core + disk + sparkle, 0.0, 1.0);
	COLOR = vec4(c, a) * alpha;
}");

	private static Shader? _drill;

	/// <summary>
	/// 天际钻头（ColorRect，上边是锥底、下边是锥尖）：按圆柱展开坐标画的螺旋棱纹（随 spin 转动），
	/// 左上方打光的金属明暗 + 一条锐利高光、边缘青色轮廓光、锥尖白热；锥体外一圈柔和的青色辉光。alpha 整体透明度。
	/// </summary>
	public static Shader Drill => _drill ??= Make(@"
shader_type canvas_item;
uniform float spin = 0.0;
uniform float alpha = 1.0;
uniform float heat = 0.0;
void fragment() {
	float y = UV.y;                                   // 0 = 锥底 → 1 = 锥尖
	float x = UV.x - 0.5;
	float half_w = 0.33 * (1.0 - y) + 0.004;
	float ax = abs(x);
	float inside = smoothstep(half_w + 0.004, half_w - 0.004, ax);
	float u = asin(clamp(x / half_w, -1.0, 1.0));     // 圆柱展开角：-π/2 … π/2
	// 螺旋棱：三条，沿锥身向下缠绕
	float band = fract((u + spin) / 6.28318 * 3.0 + y * 7.0);
	float ridge = smoothstep(0.0, 0.12, band) * smoothstep(0.55, 0.4, band);
	float groove = 1.0 - smoothstep(0.55, 0.7, band) * smoothstep(1.0, 0.85, band);
	float light = clamp(cos(u + 0.55) * 0.5 + 0.5, 0.0, 1.0);
	vec3 dark = vec3(0.03, 0.08, 0.26), mid = vec3(0.18, 0.4, 0.85), hi = vec3(0.82, 0.93, 1.0);
	vec3 c = mix(dark, mid, light);
	c = mix(c, hi, ridge * light * 0.8);
	c *= 0.55 + 0.45 * groove;
	float spec = pow(max(cos(u + 0.75), 0.0), 18.0) * (0.6 + 0.4 * ridge);
	c += vec3(1.0) * spec * 0.9;
	float rim = smoothstep(half_w - 0.02, half_w, ax) * inside;
	c += vec3(0.25, 0.85, 1.0) * rim * 0.9;
	// 锥尖白热（钻得越久越亮）
	float tip = exp(-(1.0 - y) * (14.0 - 6.0 * heat));
	c = mix(c, vec3(1.0, 0.98, 0.9), tip * inside * (0.6 + 0.4 * heat));
	// 锥体外的辉光
	float glow = exp(-max(ax - half_w, 0.0) * 30.0) * (1.0 - inside) * (0.35 + 0.4 * heat) * smoothstep(0.0, 0.1, y);
	vec3 outc = mix(vec3(0.3, 0.85, 1.0), c, inside);
	COLOR = vec4(outc, clamp(inside + glow, 0.0, 1.0) * alpha);
}");

	private static Shader? _paleRays;

	/// <summary>
	/// 暗淡蓝点（屏幕坐标作画，盖满屏幕的 ColorRect）：画面压暗 + 胶片颗粒；从画外左上方的"太阳"斜射进来几道散射光带，
	/// 光带宽窄不一、边缘柔和、带一点彩色色散（红 / 绿 / 蓝分量错开），其中最亮的一道正好穿过蓝点所在位置。
	/// dot = 蓝点（SCREEN_UV），alpha 整体强度。
	/// </summary>
	public static Shader PaleRays => _paleRays ??= Make(@"
shader_type canvas_item;
uniform vec2 dot_pos = vec2(0.4, 0.3);
uniform float alpha = 1.0;
uniform float aspect = 1.78;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float band(vec2 p, vec2 o, float ang, float w) {
	vec2 d = vec2(cos(ang), sin(ang));
	vec2 n = vec2(-d.y, d.x);
	float dist = abs(dot(p - o, n));
	return exp(-dist * dist / (w * w));
}
void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 p = vec2(uv.x * aspect, uv.y);
	vec2 o = vec2(dot_pos.x * aspect, dot_pos.y);
	float main_ang = 0.5;
	// 几道光带：都经过太阳方向（画外左上），彼此张开一点角度；最亮的一道穿过蓝点
	vec3 c = vec3(0.0);
	for (int i = 0; i < 5; i++) {
		float fi = float(i);
		float ang = main_ang + (fi - 1.0) * 0.045;
		vec2 oo = o + vec2(-0.02, 0.06) * (fi - 1.0);
		float w = (fi == 1.0) ? 0.022 : 0.008 + 0.006 * fract(fi * 0.37);
		float k = (fi == 1.0) ? 1.0 : 0.35;
		// 色散：三个分量的光带位置略微错开
		float r = band(p, oo + vec2(0.0, 0.004), ang, w);
		float g = band(p, oo, ang, w);
		float b = band(p, oo - vec2(0.0, 0.004), ang, w);
		c += vec3(r * 1.0, g * 0.88, b * 0.7) * k;
	}
	// 光带沿长度方向：靠近太阳（左上）更亮
	float along = clamp(1.2 - (p.x + p.y) * 0.45, 0.25, 1.0);
	c *= along * 0.35;
	// 胶片颗粒与少量尘点
	float grain = hash(floor(uv * vec2(640.0, 360.0)) + floor(TIME * 24.0)) - 0.5;
	float speck = step(0.9993, hash(floor(uv * vec2(900.0, 500.0)))) * 0.5;
	vec3 col = c + vec3(grain * 0.04) + vec3(speck);
	float dim = 0.62;
	COLOR = vec4(col, clamp(dim + length(c) * 0.5, 0.0, 1.0) * alpha);
}");

	private static Shader? _spotlight;

	/// <summary>
	/// 舞台聚光灯（屏幕坐标作画，盖满屏幕）：灯外整片压暗；锥形光柱从顶部灯口打到地面，里面有缓慢翻涌的烟雾（fbm）和被照亮的浮尘，
	/// 越靠近灯口越亮、两侧边缘柔和；地面一圈椭圆光斑（中心最亮、边缘渐隐）。
	/// lamp / floor = 灯口和光斑中心（SCREEN_UV），w_top / w_floor = 两端半宽（UV），open 0~1 灯开合，alpha 整体强度。
	/// </summary>
	public static Shader Spotlight => _spotlight ??= Make(@"
shader_type canvas_item;
uniform vec2 lamp = vec2(0.3, 0.0);
uniform vec2 floor_pos = vec2(0.3, 0.7);
uniform float w_top = 0.02;
uniform float w_floor = 0.12;
uniform float open = 1.0;
uniform float alpha = 1.0;
uniform float aspect = 1.78;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; mat2 r = mat2(vec2(0.8, 0.6), vec2(-0.6, 0.8)); for (int i = 0; i < 4; i++) { v += a * noise(p); p = r * p * 2.03 + 17.0; a *= 0.5; } return v; }
void fragment() {
	vec2 uv = SCREEN_UV;
	float t = clamp((uv.y - lamp.y) / max(floor_pos.y - lamp.y, 0.001), 0.0, 1.2);
	float cx = mix(lamp.x, floor_pos.x, t);
	float hw = mix(w_top, w_floor, t) * open;
	float dx = abs(uv.x - cx) * aspect;
	float hwa = hw * aspect;
	float cone = smoothstep(hwa, hwa * 0.55, dx) * step(lamp.y, uv.y) * step(t, 1.02);
	// 光柱里的烟雾和浮尘
	float haze = fbm(vec2(uv.x * aspect * 6.0, uv.y * 4.0 - TIME * 0.12) + TIME * 0.05);
	float motes = step(0.996, hash(floor(vec2(uv.x * aspect * 420.0, uv.y * 420.0 + TIME * 9.0)))) * cone;
	float beam = cone * (0.35 + 0.65 * (1.0 - t)) * (0.6 + 0.6 * haze);
	// 地面光斑
	vec2 fp = (uv - floor_pos) * vec2(aspect, 1.0) / vec2(w_floor * aspect * 1.25, w_floor * 0.32) / max(open, 0.01);
	float pool = smoothstep(1.0, 0.0, length(fp));
	vec3 warm = vec3(1.0, 0.96, 0.86);
	vec3 col = warm * (beam * 0.32 + pool * 0.45 + motes * 0.9);
	float dark = 0.62 * (1.0 - clamp(beam * 1.4 + pool, 0.0, 1.0));
	COLOR = vec4(col, clamp(dark + beam * 0.3 + pool * 0.4 + motes, 0.0, 1.0) * alpha);
}");

	private static Shader? _curtain;

	/// <summary>
	/// 天鹅绒幕布（ColorRect，竖长一片）：深蓝丝绒的竖向褶皱（明暗交替 + 褶皱边的亮线），下摆有金色流苏边，
	/// 靠舞台内侧的一边带一点光；side = 1 左幕（内侧在右）/ -1 右幕。alpha 整体透明度。
	/// </summary>
	public static Shader Curtain => _curtain ??= Make(@"
shader_type canvas_item;
uniform float side = 1.0;
uniform float alpha = 1.0;
void fragment() {
	vec2 uv = UV;
	float inner = side > 0.0 ? uv.x : 1.0 - uv.x;
	float sway = sin(TIME * 1.3 + uv.y * 3.0) * 0.01;
	float f = uv.x * 9.0 + sway * 9.0 + sin(uv.y * 2.0) * 0.15;
	float fold = 0.5 + 0.5 * sin(f * 6.2832);
	float ridge = pow(fold, 6.0);
	vec3 deep = vec3(0.02, 0.04, 0.16), velvet = vec3(0.07, 0.14, 0.45);
	vec3 c = mix(deep, velvet, fold * 0.85) + vec3(0.25, 0.35, 0.7) * ridge * 0.35;
	c += vec3(1.0, 0.95, 0.85) * smoothstep(0.75, 1.0, inner) * 0.12 * fold;
	// 下摆金色流苏
	float hem = smoothstep(0.93, 0.94, uv.y) * (1.0 - smoothstep(0.985, 0.99, uv.y));
	float tassel = step(0.5, fract(uv.x * 40.0)) * smoothstep(0.99, 0.995, uv.y) * (1.0 - step(1.0, uv.y));
	c = mix(c, vec3(0.95, 0.78, 0.4), hem * (0.7 + 0.3 * fold));
	c = mix(c, vec3(0.9, 0.7, 0.35), tassel);
	COLOR = vec4(c, alpha);
}");

	private static Shader? _waterDisc;

	/// <summary>
	/// P3R 菜单的"水之圆"：大圆形里是深蓝渐变 + 缓慢流动的水下焦散光纹（经典迭代 sin 焦散），边缘一圈柔和亮边，圆外透明。
	/// 参数：alpha 整体透明度；reveal 0~1 圆从中心张开的半径比例。
	/// </summary>
	public static Shader WaterDisc => _waterDisc ??= Make(@"
shader_type canvas_item;
uniform float alpha = 1.0;
uniform float reveal = 1.0;
uniform float caustics = 1.0;
uniform vec4 deep : source_color = vec4(0.02, 0.08, 0.42, 1.0);
uniform vec4 shallow : source_color = vec4(0.1, 0.45, 0.95, 1.0);
float caustic(vec2 p, float t) {
	vec2 i = p;
	float c = 1.0;
	float inten = 0.005;
	for (int n = 0; n < 4; n++) {
		float tt = t * (1.0 - (3.5 / float(n + 1)));
		i = p + vec2(cos(tt - i.x) + sin(tt + i.y), sin(tt - i.y) + cos(tt + i.x));
		// 分母保护：sin / cos 接近 0 时原式会除以 0 得到 NaN，画面上出现黑色坏点
		float sx = sin(i.x + tt), cy = cos(i.y + tt);
		sx = (sx < 0.0 ? -1.0 : 1.0) * max(abs(sx), 1e-3);
		cy = (cy < 0.0 ? -1.0 : 1.0) * max(abs(cy), 1e-3);
		c += 1.0 / max(length(vec2(p.x / (sx / inten), p.y / (cy / inten))), 1e-4);
	}
	c /= 4.0;
	c = 1.17 - pow(clamp(c, 0.0, 50.0), 1.4);
	return clamp(pow(abs(c), 8.0), 0.0, 4.0);
}
void fragment() {
	vec4 mod_col = COLOR;                                            // v0.14.27：节点的 modulate（之前直接覆盖 COLOR，父节点淡出对水之圆无效）
	vec2 d = UV * 2.0 - 1.0;
	float r = length(d);
	float R = max(reveal, 0.001);
	float inside = smoothstep(R, R - 0.008, r);
	vec3 col = mix(shallow.rgb, deep.rgb, smoothstep(-0.9, 0.9, d.y));
	// caustics = 1：焦散纹（胜利 / 切入用）；0：能力牌月盘用干净水面（焦散的暗部会在盘面上形成不规则深蓝斑块，v0.13.9）
	float c = caustic(UV * 7.0 + vec2(0.0, TIME * 0.05), TIME * 0.35);
	col += vec3(0.55, 0.85, 1.0) * clamp(c, 0.0, 1.0) * (0.55 - 0.35 * UV.y) * caustics;
	float hi = smoothstep(0.9, -0.2, length(d - vec2(-0.25, -0.45)));
	float wave = 0.5 + 0.5 * sin(r * 26.0 - TIME * 2.2);
	col += (vec3(0.45, 0.8, 1.0) * hi * 0.35 + vec3(0.4, 0.8, 1.0) * pow(wave, 6.0) * 0.12 * (1.0 - r)) * (1.0 - caustics);
	float rim = smoothstep(R - 0.06, R - 0.004, r) * inside;
	col += vec3(0.35, 0.8, 1.0) * rim * 0.6;
	COLOR = vec4(col * mod_col.rgb, inside * alpha * mod_col.a);
}");

	private static Shader? _drown;

	/// <summary>低血量"沉入水中"：画面边缘深蓝暗角 + 焦散光纹 + 缓慢的水波起伏；intensity 0~1，pulse 心跳增亮。中间完全透明，不挡视线。</summary>
	public static Shader Drown => _drown ??= Make(@"
shader_type canvas_item;
uniform float intensity = 0.0;
uniform float pulse = 0.0;
uniform float aspect = 1.78;
float caustic(vec2 p, float t) {
	vec2 i = p;
	float c = 1.0;
	for (int n = 0; n < 4; n++) {
		float tt = t * (1.0 - (3.5 / float(n + 1)));
		i = p + vec2(cos(tt - i.x) + sin(tt + i.y), sin(tt - i.y) + cos(tt + i.x));
		float sx = sin(i.x + tt), cy = cos(i.y + tt);
		sx = (sx < 0.0 ? -1.0 : 1.0) * max(abs(sx), 1e-3);
		cy = (cy < 0.0 ? -1.0 : 1.0) * max(abs(cy), 1e-3);
		c += 1.0 / max(length(vec2(p.x / (sx / 0.005), p.y / (cy / 0.005))), 1e-4);
	}
	c /= 4.0;
	c = 1.17 - pow(clamp(c, 0.0, 50.0), 1.4);
	return clamp(pow(abs(c), 8.0), 0.0, 4.0);
}
void fragment() {
	vec2 d = (UV - 0.5) * vec2(aspect, 1.0);
	float wave = 0.03 * sin(UV.x * 9.0 + TIME * 0.8) + 0.02 * sin(UV.y * 7.0 - TIME * 0.6);
	float r = length(d) / (0.5 * aspect) + wave;
	float edge = smoothstep(0.55 - 0.12 * pulse, 1.05, r);
	float c = clamp(caustic(UV * vec2(aspect, 1.0) * 4.0, TIME * 0.3), 0.0, 1.0);
	vec3 col = mix(vec3(0.01, 0.04, 0.2), vec3(0.2, 0.55, 1.0), c * 0.7);
	COLOR = vec4(col, edge * intensity * (0.75 + 0.25 * c));
}");

	private static Shader? _flatTint;

	/// <summary>纯色剪影：只取贴图的 alpha，颜色整体换成 tint（P3R 立绘后面错开的蓝色剪影）。</summary>
	public static Shader FlatTint => _flatTint ??= Make(@"
shader_type canvas_item;
uniform vec4 tint : source_color = vec4(0.22, 0.85, 1.0, 1.0);
void fragment() {
	COLOR = vec4(tint.rgb, texture(TEXTURE, UV).a * tint.a);
}");
}
