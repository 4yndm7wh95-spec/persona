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
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; } return v; }
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
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), u.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), u.x), u.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; } return v; }
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
	float cloud = smoothstep(0.5, 0.75, fbm(vec2(p.x * 4.0 + TIME * 0.025, p.y * 14.0))) * band;
	vec3 cloudCol = vec3(0.02, 0.12, 0.06);
	vec3 col = mix(halo, surf, disc);
	float a = max(disc, ha);
	col = mix(col, cloudCol, cloud * 0.85);
	a = max(a, cloud * 0.8 * smoothstep(0.5, 0.1, r));
	// 云的上缘被月光照亮
	float lit = cloud * (1.0 - smoothstep(0.5, 0.8, fbm(vec2(p.x * 4.0 + TIME * 0.025, p.y * 14.0 - 0.6)))) * disc;
	col += vec3(0.5, 0.9, 0.4) * lit * 0.4;
	COLOR = vec4(col, a * alpha);
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
	vec2 d = UV * 2.0 - 1.0;
	float r = length(d);
	float R = max(reveal, 0.001);
	float inside = smoothstep(R, R - 0.008, r);
	vec3 col = mix(shallow.rgb, deep.rgb, smoothstep(-0.9, 0.9, d.y));
	float c = caustic(UV * 7.0 + vec2(0.0, TIME * 0.05), TIME * 0.35);
	col += vec3(0.55, 0.85, 1.0) * clamp(c, 0.0, 1.0) * (0.55 - 0.35 * UV.y);
	float rim = smoothstep(R - 0.06, R - 0.004, r) * inside;
	col += vec3(0.35, 0.8, 1.0) * rim * 0.6;
	COLOR = vec4(col, inside * alpha);
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
}
