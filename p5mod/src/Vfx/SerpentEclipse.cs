using Godot;

namespace P5Joker;

/// <summary>
/// 群蛇形态：Joker 身后的日食（用户 2026-11-13：之前的日食挂在场景天空里，像贴上去的、太亮 → 这次只放在他身后，直径比他身高大一点点，做细节和光效）。
/// 一个着色器画全部：近黑的日轮（边缘透一点暗红、中心微微发紫），一圈很细的亮红白光环（亮度沿圈起伏，带一颗慢慢绕行的「钻石环」亮点），
/// 往外丝缕状的暗红日冕（极坐标噪声拉出的长短不一的光丝，慢慢转、呼吸），最外一层柔的红晕。没有贴图，没有硬边。
/// grow 0 → 1 出场（光环先亮、日冕往外长）；pulse 一下变亮（蛇咬时）。
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
uniform float disk = 0.42;     // 日轮半径（quad 半边长 = 1）；用户 2026-11-13：亮的面积要比黑的大很多
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
// 角度方向循环的噪声：把角度放到圆上取样，接缝处连续
float anoise(float a, float r, float k) { return noise(vec2(cos(a), sin(a)) * k + vec2(r, r * 0.7)); }
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float a = atan(p.y, p.x);
	float R = disk;
	float g = grow;
	float rr = r / R;                       // 1 = 日轮边
	// 日冕：长短不一的光丝，慢慢转、呼吸
	float rot = clock * 0.05;
	float s1 = anoise(a + rot, clock * 0.15, 3.0);
	float s2 = anoise(a * 1.0 - rot * 1.7, 4.0 + clock * 0.1, 9.0);
	float s3 = anoise(a + rot * 0.6, 9.0 + clock * 0.2, 22.0);
	float reach = (0.5 + 0.8 * s1 + 0.35 * s2) * g;               // 光丝能伸多远（日轮半径的倍数）
	float out_ = max(rr - 1.0, 0.0);
	float strand = exp(-out_ / max(reach * 0.5, 0.02)) * (0.6 + 0.4 * s3);
	strand *= smoothstep(2.35, 1.6, rr);   // 到 quad 边之前淡完
	float breathe = 0.85 + 0.15 * sin(clock * 1.3);
	float corona = strand * step(1.0, rr) * breathe;
	// 柔的外晕
	float halo = exp(-out_ * 1.4 / max(g, 0.05)) * 0.3 * step(1.0, rr) * smoothstep(2.35, 1.7, rr);
	// 细光环：亮度沿圈起伏 + 一颗绕行的钻石环亮点
	float ring = exp(-pow((rr - 1.0) / 0.035, 2.0));
	float ringVar = 0.55 + 0.45 * anoise(a - clock * 0.3, 2.0, 5.0);
	float bead = exp(-pow(mod(a - clock * 0.35 + 3.14159, 6.28318) - 3.14159, 2.0) / 0.02) * exp(-pow((rr - 1.0) / 0.06, 2.0));
	// 日轮：近黑，边上一点暗红，中心微微发紫
	float inDisk = smoothstep(1.0, 0.985, rr);
	// 日轮不是纯黑（用户看的是 Joker 的黑大衣站在它前面：纯黑会糊成一片）：深暗红，中间稍亮、往边上暗下去，边上再透一圈红
	// 用户 2026-11-13：黑的面积太大 → 光从边缘往里渗，只在正中留一小块黑（约日轮直径的一半）
	vec3 diskCol = mix(vec3(0.03, 0.0, 0.015), vec3(0.6, 0.06, 0.12), smoothstep(0.45, 0.98, rr));
	diskCol += vec3(0.25, 0.02, 0.05) * (0.5 + 0.5 * anoise(a + clock * 0.03, rr * 2.0 + clock * 0.05, 4.0)) * smoothstep(0.85, 0.2, rr) * 0.5;
	diskCol += vec3(0.45, 0.03, 0.08) * smoothstep(0.8, 1.0, rr) * 0.55;
	float lit = (1.0 + 1.4 * pulse);
	vec3 crimson = vec3(0.85, 0.05, 0.12);
	vec3 hot = vec3(1.0, 0.75, 0.7);
	vec3 col = vec3(0.0);
	float al = 0.0;
	// 日冕和外晕（加在后面）
	// 光芒压暗（用户：太耀眼）
	vec3 cc = mix(crimson * 0.55, mix(crimson, hot, 0.25), exp(-out_ * 1.5));
	col += cc * corona * 0.65 * lit + crimson * halo * 0.8 * lit;
	al = max(al, clamp(corona * 0.6 + halo, 0.0, 1.0));
	// 光环
	vec3 rc = mix(crimson, hot, 0.55 + 0.45 * ringVar);
	col += rc * ring * ringVar * 0.85 * lit * g;
	col += vec3(1.0, 0.92, 0.9) * bead * 0.9 * g * lit;
	al = max(al, clamp(ring * ringVar * g + bead * g, 0.0, 1.0));
	// 日轮盖在最上（不透明的黑）
	col = mix(col, diskCol, inDisk * g);
	al = max(al, inDisk * g);
	al *= alpha;
	col *= alpha;
	if (al <= 0.003) discard;
	COLOR = vec4(col, al);
}" };
}
