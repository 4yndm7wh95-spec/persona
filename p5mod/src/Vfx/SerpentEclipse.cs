using Godot;

namespace P5Joker;

/// <summary>
/// 群蛇形态：Joker 身后的红日（用户 2026-11-13：放在他身后、比身高大一点；之后又说黑的面积太大、太暗；
/// 第三次：一圈细亮环 + 平的暗盘像荧光灯带，没有细节 → 重写）。一个着色器画全部，没有贴图、没有硬边：
/// · 日面：缓慢翻涌的等离子（域扭曲的 fbm，慢慢转），暗红到绯红，有颗粒和暗色细丝，往边上稍暗（临边昏暗）；
/// · 边缘：没有细亮环，是一层宽而柔、沿圈亮度不均的边缘光，加上一簇簇不规则的日珥火舌往外卷（长短不一、慢慢变）；
/// · 日冕：长短不一的柔光丝缕，慢慢转、呼吸，最外一层柔晕。
/// grow 0 → 1 出场（大小不变，慢慢淡入）；pulse 一下变亮（蛇咬时）。
/// </summary>
public static class SerpentEclipse
{
	private static Shader? _shader;

	public static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float clock = 0.0;
uniform float grow = 1.0;
uniform float pulse = 0.0;
uniform float alpha = 1.0;
uniform float disk = 0.42;     // 日面半径（quad 半边长 = 1）
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; } return v; }
float anoise(float a, float r, float k) { return noise(vec2(cos(a), sin(a)) * k + vec2(r, r * 0.7)); }
mat2 rot(float a) { float c = cos(a), s = sin(a); return mat2(vec2(c, s), vec2(-s, c)); }
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float a = atan(p.y, p.x);
	float R = disk;   // 2026-11-13 用户：别放大出场 → 大小不变，只淡入
	float rr = r / R;
	float lit = 1.0 + 0.9 * pulse;
	vec3 deep = vec3(0.32, 0.03, 0.05);
	vec3 crimson = vec3(0.8, 0.08, 0.1);
	vec3 orange = vec3(0.95, 0.32, 0.18);
	vec3 col = vec3(0.0);
	float al = 0.0;

	// ── 日冕：柔光丝缕（慢慢转、呼吸）+ 外晕 ──
	float out_ = max(rr - 1.0, 0.0);
	float s1 = anoise(a + clock * 0.04, clock * 0.12, 3.0);
	float s2 = anoise(a - clock * 0.07, 4.0 + clock * 0.1, 8.0);
	float reach = (0.45 + 0.75 * s1 + 0.3 * s2);
	float strand = exp(-out_ / max(reach * 0.5, 0.02)) * (0.6 + 0.4 * anoise(a, 9.0 + clock * 0.2, 20.0));
	strand *= smoothstep(2.35, 1.6, rr) * step(1.0, rr) * (0.85 + 0.15 * sin(clock * 1.2));
	float halo = exp(-out_ * 1.6) * 0.28 * step(1.0, rr) * smoothstep(2.35, 1.7, rr);
	col += mix(deep, crimson, exp(-out_ * 2.0)) * strand * 0.55 * lit + crimson * halo * 0.7 * lit;
	al = max(al, clamp(strand * 0.5 + halo, 0.0, 1.0));

	// ── 日珥：边上一簇簇不规则的火舌往外卷 ──
	float pa = a + 0.25 * sin(out_ * 9.0 - clock * 0.8) * smoothstep(0.0, 0.2, out_);   // 往外卷
	float tongue = anoise(pa * 1.0 + clock * 0.03, clock * 0.25, 6.0);
	tongue = smoothstep(0.45, 0.85, tongue) * (0.5 + 0.5 * anoise(pa, 3.0 + clock * 0.3, 14.0));
	float tLen = 0.04 + 0.22 * tongue;
	float prom = smoothstep(tLen, 0.0, out_) * step(1.0, rr) * smoothstep(0.0, 0.02, tongue);
	float promN = fbm(vec2(pa * 6.0, out_ * 18.0 - clock * 0.6));
	prom *= smoothstep(0.25, 0.65, promN);
	col += mix(crimson, orange, 0.5 * (1.0 - out_ / max(tLen, 0.01))) * prom * 0.75 * lit;
	al = max(al, prom * 0.8);

	// ── 日面：翻涌的等离子 ──
	if (rr < 1.03) {
		vec2 q = rot(clock * 0.03) * p / R;
		vec2 w = vec2(fbm(q * 2.2 + clock * 0.06), fbm(q * 2.2 + vec2(5.2, 1.3) - clock * 0.05));
		float n = fbm(q * 3.0 + w * 1.8);
		float gran = fbm(q * 11.0 + w * 2.0 + clock * 0.1);
		float fil = 1.0 - abs(fbm(q * 5.0 - w * 1.5) * 2.0 - 1.0);
		fil = pow(fil, 6.0);
		vec3 sc = mix(deep * 1.5, crimson * 1.15, smoothstep(0.25, 0.7, n));
		sc = mix(sc, orange, smoothstep(0.55, 0.85, n * 0.7 + gran * 0.3) * 0.55);
		sc *= 0.75 + 0.35 * gran;
		sc = mix(sc, deep * 0.7, fil * 0.45);                          // 暗色细丝
		// 临边：往边上暗一点，但贴边一层宽而柔、亮度不均的边缘光（不是细亮环）
		float limb = mix(1.0, 0.7, smoothstep(0.55, 0.98, rr));
		float edgeLight = smoothstep(0.78, 1.0, rr) * (0.35 + 0.65 * anoise(a - clock * 0.05, 1.5, 4.0));
		sc = sc * limb + crimson * edgeLight * 0.35;
		sc *= lit;
		float inDisk = smoothstep(1.03, 0.98, rr);
		col = mix(col, sc, inDisk);
		al = max(al, inDisk);
	}
	float fin = smoothstep(0.0, 1.0, grow);
	al *= alpha * fin;
	col *= alpha * fin;
	if (al <= 0.003) discard;
	COLOR = vec4(col, al);
}" };
}
