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

	private static Shader? _underwater;

	/// <summary>
	/// v0.14.49 牌堆界面（P3R 游戏内菜单）：深蓝水下 —— 上亮下暗的渐变、顶部缓慢流动的焦散网、斜射的光柱。
	/// 圆形开孔：center（UV，0~1）/ radius（以画面高度为单位，0 = 全关）只显示圆内，边缘一道青白亮环。
	/// </summary>
	public static Shader Underwater => _underwater ??= Make(@"
shader_type canvas_item;
uniform float aspect = 1.777;
uniform vec2 center = vec2(0.1, 0.9);
uniform float radius = 3.0;
void fragment() {
	vec2 uv = UV;
	vec3 top = vec3(0.16, 0.47, 0.90), bot = vec3(0.02, 0.08, 0.36);
	vec3 c = mix(top, bot, clamp(uv.y * 1.1, 0.0, 1.0));
	float u = uv.x * aspect, t = TIME * 0.35;
	float a = sin(u * 22.0 + sin(uv.y * 9.0 + u * 3.5 + t) * 2.2 + t * 1.3);
	float b = sin(u * 15.0 - uv.y * 25.0 + sin(u * 7.0 - t) * 1.7 - t);
	float net = pow(clamp(1.0 - abs(a + b) * 2.4, 0.0, 1.0), 3.0);
	c += vec3(0.45, 0.75, 1.0) * net * clamp(1.0 - uv.y * 1.8, 0.0, 1.0) * 0.38;
	float rays = pow(clamp(sin(u * 7.0 + uv.y * 2.0 + sin(t * 0.5) * 0.6) * 0.5 + 0.5, 0.0, 1.0), 8.0);
	c += vec3(0.6, 0.85, 1.0) * rays * clamp(1.0 - uv.y * 1.3, 0.0, 1.0) * 0.14;
	vec2 d = (uv - center) * vec2(aspect, 1.0);
	float r = length(d);
	float inside = smoothstep(radius, radius - 0.01, r);
	float rim = smoothstep(0.02, 0.0, abs(r - radius)) * step(0.001, radius);
	COLOR = vec4(c + vec3(0.7, 0.95, 1.0) * rim * 0.9, max(inside, rim * 0.9));
}");

	private static Shader? _zoneTail, _campPanel, _campWater, _campPortrait, _gradientMap, _blobWipe, _diveSplash, _diveMask;

	/// <summary>
	/// v0.14.67 设置界面（P3R 主菜单 52s）的水下背景：上方是很亮的青色水面（流动的亮斑焦散），往下变钴蓝、深蓝；斜射光柱；中段若隐若现的暗色块（远处的水底建筑）。
	/// </summary>
	/// <summary>
	/// v0.14.74 设置界面左边的白色面板（屏幕上固定，不跟人物动）：右沿 = 人物右轮廓往左收 140px 再抹平，
	/// 120px 宽的过渡带：白 → 浅青 → 透明（下面的水），没有硬边。
	/// </summary>
	/// <summary>
	/// v0.14.75 设置界面左边的白色面板（屏幕上固定，不跟人物动）：右沿就是人物和水的分界线（同立绘着色器），左白右水，
	/// 分界只有 2px 的抗锯齿，不加浅青过渡（原版就是干净的两块）。reveal：入场时从画面左外侧扫到位。
	/// </summary>
	/// <summary>v0.14.77：立绘画布下沿以下的白区延伸（入场时人物还在上方）；和白区一样按屏幕坐标从左往右扫出来。</summary>
	public static Shader ZoneTail => _zoneTail ??= Make(@"
shader_type canvas_item;
uniform float zone_reveal = 1.0;
void fragment() {
	float rx = mix(-0.05, 0.75, zone_reveal) - SCREEN_UV.y * 0.12;
	COLOR = vec4(1.0, 1.0, 1.0, 1.0 - smoothstep(rx - 0.002, rx + 0.002, SCREEN_UV.x));
}");

	public static Shader CampPanel => _campPanel ??= Make(@"
shader_type canvas_item;
uniform vec2 edge[13];
uniform float reveal = 1.0;
float edge_x(float y) {
	for (int i = 0; i < 12; i++) {
		if (y <= edge[i + 1].y) {
			float k = (y - edge[i].y) / max(edge[i + 1].y - edge[i].y, 0.0001);
			return mix(edge[i].x, edge[i + 1].x, clamp(k, 0.0, 1.0));
		}
	}
	return edge[12].x;
}
void fragment() {
	float ex = mix(-0.15, edge_x(UV.y), reveal);
	float px = 1.0 / 1920.0;
	float a = 1.0 - smoothstep(ex - 1.5 * px, ex + 1.5 * px, UV.x);
	COLOR = vec4(1.0, 1.0, 1.0, a);
}");

	// v0.14.77：水的颜色抽成函数（背景水和立绘里「变成水」的那部分共用，保证一模一样、看不出接缝）
	private const string WaterFn = @"
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vnoise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y);
}
vec3 water_at(vec2 wuv, float aspect, float sun_x, float wt) {

	vec2 uv = wuv;
	float t = wt;
	float y = uv.y, x = uv.x * aspect;
	// v0.14.77（用户：深水处还太浅）：按原版 #90 逐行取色拟合 —— 顶部亮青，往下很快变深，下半屏是很深的群青
	vec3 c0 = vec3(0.12, 0.86, 1.0), c1 = vec3(0.11, 0.53, 0.91), c2 = vec3(0.05, 0.27, 0.71), c3 = vec3(0.02, 0.08, 0.70);
	vec3 c = y < 0.25 ? mix(c0, c1, smoothstep(0.0, 0.25, y)) : y < 0.55 ? mix(c1, c2, smoothstep(0.25, 0.55, y)) : mix(c2, c3, smoothstep(0.55, 1.0, y));
	// 焦散光斑
	vec2 q = vec2(x * 13.0 + sin(y * 30.0 + t * 0.9) * 0.5, y * 42.0 - t * 0.5);
	float n = vnoise(q) * 0.62 + vnoise(q * 2.3 + vec2(t * 0.35, 1.7)) * 0.38;
	float dx = abs(x - sun_x * aspect);
	float cone = (1.0 - smoothstep(0.0, 0.5 * aspect * max(0.0, 1.0 - y / 0.62), dx)) * (1.0 - smoothstep(0.08, 0.6, y));
	float band = 1.0 - smoothstep(0.02, 0.16, y);
	float region = clamp(max(cone, band * 0.75), 0.0, 1.0);
	float th = mix(0.78, 0.44, region);
	float p1 = smoothstep(th, th + 0.015, n);
	float p2 = smoothstep(th + 0.11, th + 0.125, n);
	c = mix(c, vec3(0.40, 0.88, 1.0), p1 * smoothstep(0.02, 0.2, region) * 0.85);
	c = mix(c, vec3(0.72, 0.97, 1.0), p2 * smoothstep(0.1, 0.4, region) * 0.8);
	// 斜射光柱（很淡）
	float rays = pow(clamp(sin(x * 4.6 + y * 1.6 + sin(t * 0.4) * 0.7) * 0.5 + 0.5, 0.0, 1.0), 10.0);
	c += vec3(0.55, 0.88, 1.0) * rays * clamp(1.0 - y * 1.3, 0.0, 1.0) * 0.12;
	// 远处的暗色块（模糊的水底建筑）
	float bx = smoothstep(0.08, 0.0, abs(fract(x * 1.6 + 0.3) - 0.5) - 0.32) * smoothstep(0.30, 0.45, y) * smoothstep(0.75, 0.55, y);
	c = mix(c, c * 0.86, bx * 0.5);
	return c;
}
";

	public static Shader CampWater => _campWater ??= Make(@"
shader_type canvas_item;
// v0.14.72：四段渐变 + 水面焦散光斑（见 WaterFn）
uniform float aspect = 1.777;
uniform float sun_x = 0.64;
" + WaterFn + @"
void fragment() {
	COLOR = vec4(water_at(UV, aspect, sun_x, TIME), 1.0);
}");

	/// <summary>
	/// v0.14.67 设置界面倒挂立绘的"融进水里"：沿人物右侧轮廓的分界线（edge，UV 坐标，按 y 递增），
	/// 线左边保持原样（白色和白底面板连成一片）；线右边白色变透明透出水（留一点浅青的釉色），蓝色部分随水微微闪，深色线条像沉在水里一样变淡。
	/// 白色按容差判断（生图的白不是精确 #FFFFFF）：最小通道 0.80~0.93 渐入、饱和度低。头发 / 飘带区域有轻微的水波扭动。
	/// </summary>
	public static Shader CampPortrait => _campPortrait ??= Make(@"
shader_type canvas_item;
// v0.14.72（用户：人物和水有明显分界线、人物没有动作、动画像低帧率）：
// · 白色面板并进这个着色器：轮廓线左边是白，右边是水，和人物白色透水用同一条羽化带 —— 不再是两层各自的硬边；
// · head_angle：头部绕脖子转（入水时先低着头，再抬起来），从衣领往下渐变生效，身体不动；
// · 飘带 / 头发分区摆动（幅度比以前大，一直在动）。
uniform vec2 edge[13];
uniform float sway = 1.0;
uniform float panel = 1.0;
uniform float head_angle = 0.0;
uniform vec2 head_pivot = vec2(0.215, 0.53);
uniform float bottom_fade = 0.0;   // v0.14.74：入场时人物在上方，立绘画布下沿（头发被裁掉的直线）露在画面里 —— 下沿 8% 渐隐
uniform float aspect = 1.7778;
// v0.14.77：头发「现场渲染」成水（用户：不要画头发，全部用水来模拟）—— 头发像素按它在屏幕上的位置取背景水的颜色（WaterFn，和背景同一个函数），
// 浅色发束只稍微提亮一点留出走向；细的耳机线 / 线条不算头发。白色面板也在这里画：分界线左边、人物上半截的空白处填白，跟着人物一起沉下来
uniform float screen_aspect = 1.7778;
uniform float sun_x = 0.64;
uniform float panel_zone = 1.0;
uniform float zone_reveal = 1.0;   // 入场：白区从屏幕左边往右扫出来（屏幕坐标，不跟人物转）
uniform int layer = 0;   // 0 = 人物 + 白区，1 = 只画白区，2 = 只画人物
uniform sampler2D zone_tex : filter_linear;   // 白色区域蒙版（tools/ui/build_system_portrait.py：分界线左边、头发以上），跟着立绘一起动
float edge_x(float y) {
	for (int i = 0; i < 12; i++) {
		if (y <= edge[i + 1].y) {
			float k = (y - edge[i].y) / max(edge[i + 1].y - edge[i].y, 0.0001);
			return mix(edge[i].x, edge[i + 1].x, clamp(k, 0.0, 1.0));
		}
	}
	return edge[12].x;
}
" + WaterFn + @"
float hairness(vec4 p) {
	// 头发：钴蓝 / 天青（蓝色为主、不白、不是深色线条）
	// v0.14.77：头发里的浅青碎片也算头发（以前 r > 0.5 的浅青块漏掉，留在水里像单独画上去的）—— 偏蓝、不是近白
	float mn = min(p.r, min(p.g, p.b));
	// 不乘透明度：头发边缘的半透明像素也要整个变成水，否则会混进一点原来的颜色，留下一圈细线
	return step(0.02, p.a) * step(0.6, p.b) * step(0.25, p.b - p.r) * (1.0 - step(0.82, mn));
}
vec2 rot(vec2 p, float a) {
	float c = cos(a), s = sin(a);
	return vec2(c * p.x - s * p.y, s * p.x + c * p.y);
}
void fragment() {
	// v0.14.77（用户：入场时左下有一条直线边界）：白区那层（layer 1）是一张比立绘画布大的矩形（宽 2 倍、高 3 倍，画布在中间偏上），
	// 画布外面也按规则铺白：左边整片白、下面只到最左 235px（画布最下一行头发从这里开始）、上面到分界线顶端 —— 白区不再在画布边上断成一条直线
	if (layer == 1) {
		vec2 cuv = UV * vec2(2.0, 3.0) - vec2(0.5, 1.0);
		// 画布里：头发上沿以上是白（zone_tex，头发以下是水）；画布左边按最左一列、下面按最下一行延伸；画布上面到分界线顶端是白
		float exz = cuv.y < 0.0 ? edge[0].x : edge_x(clamp(cuv.y, 0.0, 1.0));
		float pxz = 1.0 / 1920.0;
		// 画布下面（人物还没落定时露出来）：按画布最下一行往下延伸 —— 头发那几列是水，最左白条那几列是白（不再整条变水，出一道直线）
		// 画布下面（入场时人物还在上方）：只有最左边大字那一条是白，其余是水（不再把画布最下一行往下拉成一块白色矩形）
		float zt = cuv.y < 0.0 ? 1.0 : cuv.y > 1.0 ? 1.0 - smoothstep(0.06, 0.065, SCREEN_UV.x) : texture(zone_tex, clamp(cuv, vec2(0.0005), vec2(0.9995))).r;
		float rx = mix(-0.05, 0.75, zone_reveal) - SCREEN_UV.y * 0.12;
		float z = zt * (1.0 - smoothstep(exz - 1.5 * pxz, exz + 1.5 * pxz, cuv.x)) * panel_zone * panel
			* (1.0 - smoothstep(rx - 0.002, rx + 0.002, SCREEN_UV.x));
		COLOR = vec4(1.0, 1.0, 1.0, z);
		return;
	}
	vec2 uv = UV;
	// 头部旋转（反向采样）：衣领以下逐渐生效，离脖子很远的地方（画面右侧的水里）减弱
	vec2 q = (uv - head_pivot) * vec2(aspect, 1.0);
	float hw = smoothstep(-0.03, 0.12, q.y) * (1.0 - smoothstep(0.75, 1.05, length(q)));
	q = rot(q, -head_angle * hw);
	uv = head_pivot + q / vec2(aspect, 1.0);
	// 飘带（画面上方中间，越往上端摆得越大）
	float rib = smoothstep(0.20, 0.27, uv.x) * smoothstep(0.50, 0.43, uv.x) * smoothstep(0.46, 0.36, uv.y);
	float tip = clamp((0.46 - uv.y) / 0.46, 0.0, 1.0);
	uv.x += sin(TIME * 1.7 + uv.y * 9.0) * 0.007 * rib * tip * sway;
	uv.y += sin(TIME * 1.3 + uv.x * 7.0) * 0.003 * rib * tip * sway;
	// 右侧飘进水里的头发 / 衣服
	float hr = smoothstep(0.34, 0.52, uv.x) * smoothstep(0.55, 0.75, uv.y);
	uv.x += sin(TIME * 1.25 + uv.y * 8.0) * 0.0045 * hr * sway;
	uv.y += sin(TIME * 1.05 + uv.x * 6.0) * 0.0035 * hr * sway;
	// 画面最下边的刘海
	float fr = smoothstep(0.86, 0.98, uv.y) * (1.0 - smoothstep(0.25, 0.32, abs(uv.x - 0.26)));
	uv.x += sin(TIME * 1.5 + uv.x * 10.0) * 0.003 * fr * sway;
	vec4 c = texture(TEXTURE, clamp(uv, vec2(0.0), vec2(1.0)));
	float mn = min(c.r, min(c.g, c.b)), mx = max(c.r, max(c.g, c.b));
	float white = smoothstep(0.80, 0.93, mn) * (1.0 - smoothstep(0.08, 0.22, mx - mn));
	// 分界线右边的人物（伸进水里的头发 / 袖口）也变成水
	float ex = edge_x(UV.y);
	float px = TEXTURE_PIXEL_SIZE.x;
	float side = smoothstep(ex - 1.5 * px, ex + 1.5 * px, UV.x);
	// 头发判定：周围 8 个点（半径 4px）里至少一半也是头发 —— 大块头发算，细耳机线不算
	vec2 tp = TEXTURE_PIXEL_SIZE * 4.0;
	float hn = 0.0;
	for (int k = 0; k < 8; k++) {
		float an = float(k) * 0.7854;
		hn += hairness(texture(TEXTURE, clamp(uv + vec2(cos(an), sin(an)) * tp, vec2(0.0), vec2(1.0))));
	}
	// 只在头部（立绘下半部，衣领以下）认头发 —— 衬衫上的浅青阴影不算
	float hair = hairness(c) * smoothstep(3.0, 4.5, hn) * smoothstep(0.52, 0.58, uv.y);
	// 用户：眼睛颜色不对 —— 两只眼的虹膜也是青蓝，被当成头发画成了水；眼睛范围内不转（立绘像素坐标）
	vec2 pp = uv * vec2(1920.0, 1080.0);
	float eyes = max(1.0 - smoothstep(62.0, 72.0, length((pp - vec2(497.0, 925.0)) * vec2(0.85, 1.25))),
		1.0 - smoothstep(30.0, 38.0, length((pp - vec2(357.0, 845.0)) * vec2(1.3, 0.9))));
	hair *= 1.0 - eyes;
	// 分界线右边整片都是水：不乘透明度（以前乘了 c.a，线条 / 发丝的半透明边缘只变一半，留下一条条细接缝）
	float toWater = max(hair, side);
	vec3 w = water_at(SCREEN_UV, screen_aspect, sun_x, TIME);
	// 用户：不要浅色反光，那样反而有边界 —— 头发就是水本身的颜色（不压暗：压暗一点在头发外沿、立绘下沿都会出一条色差分界）
	vec3 wc = w;
	vec3 fc = mix(c.rgb, wc, toWater);
	float fa = c.a;
	// 白色面板：分界线左边、人物上半截（左下角留给水和头发）
	// 白色区域：蒙版（人物轮廓以上）× 分界线左边；画布外（人物还在上方时）是水
	float inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
	float rx = mix(-0.05, 0.75, zone_reveal) - SCREEN_UV.y * 0.12;
	float zone = texture(zone_tex, clamp(uv, vec2(0.0), vec2(1.0))).r * inside * (1.0 - side) * panel_zone * panel
		* (1.0 - smoothstep(rx - 0.002, rx + 0.002, SCREEN_UV.x));
	if (layer == 1) { fa = 0.0; }
	if (layer == 2) { zone = 0.0; }
	float fa2 = fa + zone * (1.0 - fa);
	fc = (fc * fa + vec3(1.0) * zone * (1.0 - fa)) / max(fa2, 0.0001);
	fa = fa2;
	if (layer != 1) { fa *= 1.0 - bottom_fade * smoothstep(0.90, 0.995, UV.y); }   // 白区不渐隐（下面接着铺白）
	COLOR = vec4(fc, fa);
}");

	/// <summary>v0.14.67 设置界面：把原版控件的贴图（米黄 / 棕色系）按亮度映射到 P3R 的深蓝 → 钴蓝 → 冰白，保留透明度。</summary>
	public static Shader GradientMap => _gradientMap ??= Make(@"
shader_type canvas_item;
void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float l = dot(c.rgb, vec3(0.3, 0.55, 0.15));
	vec3 navy = vec3(0.03, 0.08, 0.30), royal = vec3(0.10, 0.42, 1.0), ice = vec3(0.86, 0.97, 1.0);
	vec3 m = l < 0.5 ? mix(navy, royal, l * 2.0) : mix(royal, ice, (l - 0.5) * 2.0);
	COLOR = vec4(m, c.a);
}");

	/// <summary>
	/// v0.14.70 设置界面切分页（P3R 53.33s 进子菜单）：从选中的字涌出一团边缘起伏的深蓝水团（半透明，下面的人物线条隐约可见），
	/// 盖满后中间再撑开一个带白边的洞露出新的一页。r_out = 水团外半径，r_in = 洞的半径（像素）。
	/// </summary>
	public static Shader BlobWipe => _blobWipe ??= Make(@"
shader_type canvas_item;
uniform vec2 center;
uniform vec2 size = vec2(1920.0, 1080.0);
uniform float r_out = 0.0;
uniform float r_in = 0.0;
uniform float seed = 0.0;
uniform float rim = 7.0;
uniform vec4 fill : source_color = vec4(0.05, 0.20, 0.78, 0.86);
float wob(float a, float s) {
	return 1.0 + 0.09 * sin(a * 3.0 + s + TIME * 2.2) + 0.05 * sin(a * 5.0 - s * 1.7 - TIME * 1.3) + 0.035 * sin(a * 9.0 + s * 3.1);
}
void fragment() {
	vec2 p = UV * size - center;
	float d = length(p);
	float a = atan(p.y, p.x);
	float ro = r_out * wob(a, seed);
	float ri = r_in * wob(a + 1.3, seed + 4.0);
	float outer = 1.0 - smoothstep(ro - 1.5, ro + 1.5, d);
	float hole = r_in > 0.5 ? smoothstep(ri - 1.5, ri + 1.5, d) : 1.0;
	float ring = r_in > 0.5 ? 1.0 - smoothstep(ri + rim - 1.5, ri + rim + 1.5, d) : 0.0;
	float body = outer * hole;
	float shade = 0.85 + 0.15 * smoothstep(ro * 0.4, ro, d);
	vec3 col = mix(fill.rgb * shade, vec3(1.0), ring);
	COLOR = vec4(col, body * mix(fill.a, 1.0, ring));
}");

	/// <summary>
	/// v0.14.73 入水转场遮罩：设置界面本体只在 V 形水团（逐帧图的 B 通道）里显示，水团外还是打开前的画面。full = 1 时整屏显示。
	/// </summary>
	public static Shader DiveMask => _diveMask ??= Make(@"
shader_type canvas_item;
uniform sampler2D frame_tex : filter_linear;
uniform float full = 0.0;
void fragment() {
	float b = texture(frame_tex, UV).b;
	COLOR = vec4(1.0, 1.0, 1.0, max(full, smoothstep(0.35, 0.65, b)));
}");

	/// <summary>
	/// v0.14.73 入水转场的水（用户：自己拼的水柱不自然 —— 照原版画）：播放从原版 60fps 分镜里抠出来的逐帧图（tools/ui/build_dive_flipbook.py）。
	/// R = 水的覆盖（锐化成干净的平涂边），G = 色调（天青半透明 / 浅青 → 近白），B = V 形水团（上浅下深的半透明蓝）。
	/// </summary>
	public static Shader DiveSplash => _diveSplash ??= Make(@"
shader_type canvas_item;
uniform sampler2D frame_tex : filter_linear;
uniform float alpha = 1.0;
uniform float tint = 1.0;
vec4 over(vec4 dst, vec3 c, float a) {
	float oa = a + dst.a * (1.0 - a);
	vec3 oc = (c * a + dst.rgb * dst.a * (1.0 - a)) / max(oa, 0.0001);
	return vec4(oc, oa);
}
void fragment() {
	vec3 m = texture(frame_tex, UV).rgb;
	vec4 o = vec4(0.0);
	float body = smoothstep(0.35, 0.65, m.b);
	o = over(o, mix(vec3(0.22, 0.60, 1.0), vec3(0.05, 0.24, 0.86), UV.y), body * tint * 0.72);
	float w = fwidth(m.r) * 0.75 + 0.02;
	float cov = smoothstep(0.5 - w, 0.5 + w, m.r);
	// 色调（G / R 还原成覆盖内的值）：< 0.28 天青半透明（泡沫暗面、气泡），之上浅青 → 近白
	float tone = clamp(m.g / max(m.r, 0.05), 0.0, 1.0);
	float isMid = 1.0 - smoothstep(0.24, 0.32, tone);
	vec3 paleC = mix(vec3(0.64, 0.89, 1.0), vec3(0.95, 0.99, 1.0), smoothstep(0.55, 0.95, tone));
	vec3 col = mix(paleC, vec3(0.36, 0.74, 1.0), isMid);
	o = over(o, col, cov * mix(0.95, 0.78, isMid));
	COLOR = vec4(o.rgb, o.a * alpha);
}");
}
