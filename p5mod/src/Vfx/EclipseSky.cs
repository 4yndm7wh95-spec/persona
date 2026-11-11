using Godot;

namespace P5Joker;

/// <summary>
/// 日蚀天色（另一个会话为八岐大蛇做的第 6 版，2026-11-10；群蛇形态的场景也用它，用户 2026-11-11：那个日蚀还可以）。
/// 整屏压暗偏暗红，身后高处一轮黑日 + 暗红日冕，光束穿过飘动的云，贴地一层没有边界的薄红雾，蛇身没入的地方一大片翻涌的暗红雾。
/// 群蛇形态额外用到：reveal / from_uv（出场时从 Joker 身边漫开）、desat（压一点饱和度）、cover（底色盖实一点）。
/// 用法：ColorRect 铺满屏幕（Fx.ScreenRoot），设 aspect / sun / ground / pool / pool_w，每帧设 clock（或 Fx.Clock）/ amount。
/// </summary>
public static class EclipseSky
{
	private static Shader? _shader;

	public static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec2 sun = vec2(0.22, 0.2);      // 日蚀在屏幕上的位置（0～1）
uniform float aspect = 1.7778;
uniform float clock = 0.0;
uniform float lightning = 0.0;
uniform float amount = 1.0;
uniform float ground = 0.84;            // 地面的高度（0～1，从上往下）
uniform vec2 pool = vec2(0.2, 0.8);     // 蛇身没入地面的地方（0～1）
uniform float pool_w = 0.25;
uniform float reveal = 1.0;            // 0 → 1：从 from_uv 往外漫开（边是翻滚的暗烟）
uniform vec2 from_uv = vec2(0.25, 0.7);
uniform float desat = 0.0;             // 压饱和度（群蛇形态：用户说原来的太艳）
uniform float cover = 0.4;             // 底色的最低覆盖（群蛇形态：盖实一点，不透出原来的背景）
uniform float disk_r = 0.082;          // 黑日半径
uniform float bleed = 0.0;             // 群蛇形态（用户：太阳上黑的面积太多不好看）：黑只留在中心一小块，往外过渡成暗红，日冕的光往里渗，盘面上慢慢流动的暗红纹
uniform float blend = 0.0;             // 天到地的过渡（群蛇形态：用户说中间光影断层 → 光束和泛光一直柔和地落到地面，云没有硬的下边）
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.02 + vec2(3.1, 7.7); a *= 0.5; } return v; }
void fragment() {
	vec2 uv = UV;
	vec2 d = (uv - sun) * vec2(aspect, 1.0);
	float r = length(d), ang = atan(d.y, d.x);
	float R = disk_r;
	// 底色：整屏压暗偏暗红（盖在战斗背景上，不是不透明的）
	vec3 col = mix(vec3(0.16, 0.02, 0.04), vec3(0.07, 0.0, 0.02), uv.y);
	float a = cover;
	// 云：两层往右飘，日冕附近的云边被照红，雷光时整片云亮一下
	float c1 = fbm(vec2(uv.x * 2.2 - clock * 0.012, uv.y * 4.0));
	float c2 = fbm(vec2(uv.x * 4.5 - clock * 0.03, uv.y * 7.0 + 3.0));
	float cloud = smoothstep(0.45, 0.75, c1 * 0.7 + c2 * 0.45) * mix(1.0 - smoothstep(0.55, 0.8, uv.y), 1.0 - smoothstep(0.3, 0.95, uv.y), blend);
	float near = exp(-max(r - R, 0.0) / 0.35);
	// 日冕
	float corona = exp(-max(r - R, 0.0) / mix(0.014, 0.022, blend)) * step(R, r) * 0.8;
	float halo = exp(-max(r - R, 0.0) / mix(0.2, 0.26, blend));
	float rays = pow(noise(vec2(ang * 9.0, clock * 0.05)), 3.0) * exp(-max(r - R, 0.0) / 0.7) * (1.0 - cloud * 0.8) * mix(smoothstep(ground - 0.02, ground - 0.25, uv.y), smoothstep(ground + 0.2, ground - 0.4, uv.y), blend);
	vec3 light = vec3(1.0, 0.85, 0.75) * corona * 1.4 + vec3(0.9, 0.08, 0.06) * (halo * 0.9 + rays * 0.55);
	light *= 1.0 - cloud * 0.6;
	col += light;
	// 日冕的光洒满整个战场（用户：光影在中间断层）：从日蚀往下、往两边一大片暗红的泛光，地面被照出一大片暖红
	float wash = exp(-r / 0.75);
	col += vec3(0.5, 0.05, 0.06) * wash * 0.45;
	float floorLit = smoothstep(ground - 0.15, ground + 0.05, uv.y) * exp(-pow((uv.x - sun.x) / 0.7, 2.0));
	col += vec3(0.45, 0.05, 0.05) * floorLit * 0.3;
	// 日蚀正下方一道柔和的光柱一直落到地面（接住天和地，不在中间断开）
	float column = exp(-pow((uv.x - sun.x) * aspect / 0.45, 2.0)) * smoothstep(sun.y, sun.y + 0.15, uv.y) * smoothstep(ground + 0.2, ground - 0.1, uv.y);
	col += vec3(0.45, 0.05, 0.06) * column * 0.22 * blend;
	float floorWide = smoothstep(ground - 0.3, ground + 0.08, uv.y) * exp(-pow((uv.x - sun.x) / 0.9, 2.0));
	col += vec3(0.4, 0.05, 0.05) * floorWide * 0.15 * blend;
	a = max(a, clamp(corona + halo * 0.4, 0.0, 1.0));
	// 云体：暗，靠日冕和雷光照出边
	// 黑日（用户 2026-11-10：太突兀 → 融进天色）：边缘柔一点，不是纯黑而是被暗红的天光蒙了一层，环是暗红偏亮而不是白热
	float disk = 1.0 - smoothstep(R - 0.006 * (R / 0.082 + 0.3), R + 0.004, r);
	vec3 face = mix(vec3(0.05, 0.0, 0.012), vec3(0.2, 0.02, 0.03), smoothstep(R * 0.3, R, r));
	float swirl = noise(vec2(ang * 3.0 + clock * 0.06, r / R * 3.0 - clock * 0.1)) * 0.6 + noise(d / R * 4.0 + clock * 0.05) * 0.4;
	float lit = pow(smoothstep(R * 0.1, R * 0.95, r + (swirl - 0.5) * R * 0.3), 1.15);
	vec3 bled = mix(vec3(0.03, 0.0, 0.008), vec3(0.62, 0.07, 0.07), lit) + vec3(0.9, 0.25, 0.15) * pow(lit, 4.0) * 0.5;
	face = mix(face, bled, bleed);
	col = mix(col, face, disk * mix(0.82, 1.0, bleed));
	a = max(a, disk * 0.82);
	float rim = exp(-pow((r - R) / 0.006, 2.0));
	float bead = pow(max(0.0, cos(ang - 0.7)), 24.0) * exp(-pow((r - R) / 0.01, 2.0));
	float streamer = pow(noise(vec2(ang * 5.0, 1.0 + clock * 0.03)), 2.0) * exp(-max(r - R, 0.0) / 0.07) * step(R, r);
	col += vec3(1.0, 0.45, 0.38) * rim * 0.75 + vec3(1.0, 0.75, 0.65) * bead * 0.9 + vec3(1.0, 0.3, 0.2) * streamer * 0.45;
	// 云在日蚀前面飘过：一部分云遮住黑日和日冕，被背后的光照红了边
	vec3 cc = vec3(0.06, 0.004, 0.014) + vec3(0.75, 0.08, 0.07) * near * smoothstep(0.3, 0.9, c2) * 0.7 + vec3(0.7, 0.3, 0.35) * lightning * smoothstep(0.35, 0.9, c2) * 0.3;
	// 横过日蚀的一缕薄云
	float veil = smoothstep(0.45, 0.8, fbm(vec2(uv.x * 3.0 - clock * 0.02, (uv.y - sun.y) * 14.0 + 2.0))) * exp(-pow((uv.y - sun.y - 0.01) / 0.05, 2.0));
	cloud = max(cloud, veil * 0.8);
	col = mix(col, cc, cloud * 0.75);
	a = max(a, cloud * 0.75);
	// 一层很淡的天雾盖在最上面，把日蚀和天色揉在一起
	float haze = fbm(vec2(uv.x * 1.5 + clock * 0.01, uv.y * 3.0));
	col = mix(col, vec3(0.3, 0.03, 0.05), 0.18 * haze * (1.0 - smoothstep(0.4, 0.75, uv.y)));
	// 贴地的薄红雾：没有边界，往上淡掉
	float gy = (uv.y - ground + 0.25) / 0.35;
	float mist = smoothstep(0.0, 1.0, gy) * (0.5 + 0.5 * fbm(vec2(uv.x * 3.0 - clock * 0.04, uv.y * 6.0)));
	col += vec3(0.35, 0.02, 0.04) * mist * 0.35;
	// 雷光：整屏轻轻提亮
	col += vec3(0.4, 0.12, 0.15) * lightning * 0.08;
	// 暗角
	float vig = smoothstep(0.45, 1.05, length((uv - 0.5) * vec2(aspect * 0.6, 1.0)));
	col *= 1.0 - vig * 0.3;
	a = max(a, vig * 0.3);
	// 蛇身没入的地方：一大片翻涌的暗红雾，中间亮、往四周淡开，往上飘起几缕
	vec2 pd = (uv - pool) / vec2(pool_w, pool_w * 0.35);
	float pn = fbm(vec2(uv.x * 5.0 - clock * 0.08, uv.y * 9.0 + clock * 0.05));
	float pm = exp(-dot(pd, pd) * 1.2) * (0.55 + 0.6 * pn);
	float wisp = smoothstep(0.55, 0.85, fbm(vec2(uv.x * 7.0, uv.y * 3.0 + clock * 0.25))) * exp(-pow((uv.x - pool.x) / pool_w, 2.0)) * smoothstep(pool.y + 0.05, pool.y - 0.35, uv.y) * smoothstep(pool.y - 0.5, pool.y - 0.1, uv.y);
	col += vec3(0.75, 0.08, 0.1) * pm * 0.55 + vec3(0.6, 0.05, 0.08) * wisp * 0.25;
	a = max(a, clamp(pm * 0.6, 0.0, 0.85));
	col = mix(col, vec3(dot(col, vec3(0.3, 0.5, 0.2))) * vec3(1.15, 0.85, 0.9), desat);
	// 漫开：从 from_uv 往外，边上一圈翻滚的暗烟
	vec2 asp = vec2(aspect, 1.0);
	float rd = length((uv - from_uv) * asp);
	float en = noise(uv * asp * 7.0 + vec2(clock * 0.3, 0.0)) * 0.6 + noise(uv * asp * 15.0) * 0.4;
	float R0 = reveal * 2.3;
	float spread = smoothstep(R0, R0 - 0.3, rd + (en - 0.5) * 0.35);
	float edge = smoothstep(0.16, 0.0, abs(rd + (en - 0.5) * 0.35 - (R0 - 0.15))) * step(0.001, reveal) * (1.0 - step(0.999, reveal));
	col = mix(col, vec3(0.02, 0.003, 0.008), edge * 0.7);
	a = max(a * spread, edge * 0.85 * step(rd, R0));
	COLOR = vec4(col * spread * amount, a * amount);
}" };
}
