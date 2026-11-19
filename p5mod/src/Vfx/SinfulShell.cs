using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace P5Joker;

/// <summary>
/// 华丽收场 = 撒旦耶尔的大罪穿甲弹（用户 2026-11-08，参考 art/p5/fx/v4/satanael/ref_*.png）。全部是着色器实时渲染，不画贴图：
/// ① 开场（第二版，用户 2026-11-19：从脚下慢慢扩开太柔弱）：打出 → 背景压暗、面具蓝焰往里收 → 顿帧里两帧冲击帧（白底黑剪影 → 黑底亮剪影）
///    → 蓝焰从 Joker 身上整团炸开，场景（NightScene：夜空、乌云、云底红光、远处闪电、两边断崖、深渊雾）被这一炸 0.1s 掀开，边上一道很厚的蓝焰浪；
///    召唤撒旦耶尔的雷、低频重击、重震同一拍；
/// ② 撒旦耶尔从断崖下烧出来、转枪、接枪；③ 举枪蓄力：枪口往里吸的光、场景压暗 → 时停；
/// ④ 开枪（第二版：要特别爽、有冲击力）：两帧强对比冲击帧（场景刷白、人物和撒旦耶尔全黑 → 场景全黑、人物被枪光打亮）、后坐定向震屏、
///    粗重的炮口焰 + 一大团往前推的炮口烟、一颗看得见的重弹（白热弹头 + 气浪 + 等离子尾）、身后翻起的烟道（Vfx/SinfulImpact.cs）；
/// ⑤ 打中：第一下晚一帧顿住，每个敌人被打透（过曝）、往后一震，身后顺着弹道炸出一大股贯穿的蓝焰和一团烟；
/// ⑥ 场景从远处往回烧掉，露出原来的战场。原来的细光圈、集中线、发丝气流线、螺旋细丝、天空细缝、星光碎片都去掉了（用户的雷点：细线、光圈、放射线、小点）。
/// </summary>
public static partial class SinfulShell
{
	// ───────────────────────── 场景 ─────────────────────────

	private const string NoiseFunctions = @"
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}";

	private const string Noise = NoiseFunctions + @"
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; }
	return v;
}
";

	private static Shader? _sky, _platform, _bullet;

	private const string SceneCommon = @"
uniform vec2 sres = vec2(1920.0, 1080.0);
uniform vec2 origin = vec2(430.0, 830.0);
uniform float reveal = 0.0;
uniform float clock = 0.0;
uniform float dim = 0.0;
uniform float flash = 0.0;
uniform float feet_y = 830.0;
uniform int scene_octaves = 5;
uniform float surge = 0.0;       // 开场炸开那一下：烧穿的边变成一道很厚的蓝焰浪，1 → 0（第二版，用户 2026-11-19：切换场景太柔弱）
uniform vec4 paint = vec4(0.0);  // 冲击帧：整片刷成 paint.rgb（a = 程度）
" + NoiseFunctions + @"
// Keep the same five octaves without unrolling the nested cloud noise in the driver compiler.
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < scene_octaves; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; }
	return v;
}
// 烧穿的边界（用户 2026-11-09：四档硬边的蓝焰太卡通、太漫画）：改成一道能量缝——
// 极细的热白芯（亮度闪烁）+ 平滑衰减的青色辉光 + 更宽一层淡淡的深蓝雾；辉光被顺着边走的细丝噪声撕开，往外飘出几缕；边上零星的亮点。没有色阶硬边。
// s = 到边界的有向距离（>0 在场景里）
vec4 flameEdge(vec2 px, float s) {
	float t = clock;
	float d = abs(s);
	float flick = 0.75 + 0.25 * noise(vec2(px.x * 0.05 + px.y * 0.03, t * 9.0));
	float sw = 1.0 + 4.0 * surge;                                      // 炸开时整道边变厚
	float core = exp(-pow(s / (2.2 * sw), 2.0)) * flick;
	float wisp = fbm(vec2(px.x * 0.03 + px.y * 0.02, t * 1.6 - s * 0.02 / sw));
	float glow = exp(-d / (16.0 * sw)) * (0.55 + 0.6 * wisp);
	float out_side = 1.0 - smoothstep(-90.0, 0.0, s);                 // 场景外那一侧：辉光往外飘得更远、更碎
	float haze = exp(-d / (70.0 * (1.0 + 1.5 * surge))) * (0.25 + 0.5 * smoothstep(0.45, 0.8, wisp)) * (0.4 + 0.6 * out_side);
	vec2 cell = floor(px / 9.0);
	float spark = step(0.985, fract(sin(dot(cell, vec2(12.9898, 78.233)) + floor(t * 12.0)) * 43758.5453)) * exp(-d / 10.0);
	// 冲击浪：边后面（场景这一侧）一大片被炸开的蓝焰，越靠边越亮，被噪声撕成火舌；surge 退掉就没了
	float wave = surge * exp(-max(s, 0.0) / 170.0) * smoothstep(-8.0, 6.0, s) * (0.35 + 0.95 * smoothstep(0.35, 0.8, wisp));
	vec3 c = vec3(0.95, 0.98, 1.0) * (core * 1.4 + spark + wave * wave * 0.6) + vec3(0.35, 0.75, 1.0) * (glow + wave * 0.9) + vec3(0.08, 0.22, 0.7) * haze;
	float a = clamp(core * 1.2 + glow * 0.9 + haze * 0.7 + spark + wave, 0.0, 1.0);
	return vec4(c / max(a, 0.001), a);
}
float revealDist(vec2 px) {
	float n = (fbm(px / 110.0 + vec2(0.0, -clock * 0.5)) - 0.5) * 160.0;
	float d = length((px - origin) * vec2(1.0, 1.25)) + n;
	return reveal - d;                                     // 场景 = 以 Joker 脚下为圆心的一个圈：展开（reveal 变大）、收回（reveal 变小）
}
";

	// The sky never receives a point light. Keep cloud noise out of the platform's lit pipeline.
	internal static Shader SkyShader => _sky ??= new Shader { Code = "shader_type canvas_item;\nrender_mode unshaded;\n" + SceneCommon + @"
uniform vec3 moon = vec3(1570.0, 170.0, 58.0);
void fragment() {
	vec2 px = UV * sres;
	vec2 uv = UV;
	float s = revealDist(px);
	float shown = smoothstep(-2.0, 2.0, s);
	vec4 edge = flameEdge(px, s);
	// 天空：深蓝 → 地平线一圈青蓝的亮
	vec3 col = mix(vec3(0.012, 0.025, 0.07), vec3(0.04, 0.12, 0.22), smoothstep(0.0, 0.62, uv.y));
	col += vec3(0.06, 0.32, 0.42) * exp(-abs(uv.y - 0.6) * 9.0);
	// 星星（云少的上空）
	vec2 sg = floor(px / 3.0);
	col += vec3(0.7, 0.85, 1.0) * step(0.9985, hash(sg)) * smoothstep(0.45, 0.0, uv.y) * (0.6 + 0.4 * sin(clock * 3.0 + hash(sg) * 30.0));
	// 月亮（用户 2026-11-09：着色器画、当光源）：略带残缺的满月——灰白的月面、暗色月海、小环形山、边缘变暗；外面两层月晕
	vec2 mp = (px - moon.xy) / moon.z;
	float md = length(mp);
	col += vec3(0.32, 0.42, 0.58) * exp(-md * 0.45) * 0.28 + vec3(0.55, 0.64, 0.78) * exp(-md * 1.8) * 0.3;
	col += vec3(0.5, 0.6, 0.75) * exp(-pow((md - 2.6) / 0.35, 2.0)) * 0.05;          // 一圈很淡的月晕环
	if (md < 1.05) {
		float z = sqrt(max(1.0 - md * md, 0.0));
		vec3 nrm = vec3(mp, z);
		float sun = clamp(dot(nrm, normalize(vec3(-0.35, -0.15, 1.0))) * 1.2 + 0.15, 0.0, 1.0);   // 受光面偏左上，右下边缘一点点阴影
		float maria = fbm(mp * 1.7 + vec2(5.2, 1.3));
		float crater = smoothstep(0.62, 0.8, noise(mp * 7.0 + 11.0)) * 0.5 + smoothstep(0.7, 0.85, noise(mp * 16.0 + 3.0)) * 0.3;
		vec3 mc = mix(vec3(0.5, 0.54, 0.62), vec3(0.86, 0.89, 0.95), smoothstep(0.38, 0.62, maria));
		mc *= (1.0 - 0.25 * crater) * (0.35 + 0.65 * sun) * (0.82 + 0.18 * z);
		col = mix(col, mc, 1.0 - smoothstep(0.97, 1.03, md));
	}
	float moonNear = exp(-md / 7.0);                                     // 月光照到的云
	// 乌云：两层域扭曲噪声，横向拉长，慢慢翻滚；亮边偏蓝，云底透红光
	vec2 p = vec2(px.x / sres.y, px.y / sres.y);
	for (int k = 0; k < 2; k++) {
		float fk = float(k);
		vec2 q = vec2(p.x * (1.1 + fk * 0.5) + clock * (0.012 + fk * 0.01), p.y * (2.8 + fk));
		vec2 warp = vec2(fbm(q + vec2(clock * 0.05, 0.0)), fbm(q + vec2(4.3, clock * 0.04)));
		float c = fbm(q + warp * 1.4 + fk * 3.7);
		float band = smoothstep(0.78 - fk * 0.18, 0.12, uv.y) * smoothstep(-0.05, 0.1, uv.y);
		float dens = smoothstep(0.44, 0.78, c) * band;
		float lit = clamp((c - fbm(q + warp * 1.4 + fk * 3.7 + vec2(0.0, 0.05))) * 6.0 + 0.5, 0.0, 1.0);
		vec3 cc = mix(vec3(0.02, 0.04, 0.09), vec3(0.18, 0.34, 0.5), lit);
		cc += vec3(0.55, 0.04, 0.08) * (1.0 - lit) * smoothstep(0.15, 0.6, uv.y) * (0.6 + 0.4 * fk);
		// 月光：月亮附近的云头镶一道银边，挡在月亮前面的薄云被从后面透亮
		cc += vec3(0.5, 0.58, 0.72) * lit * moonNear * 0.9 + vec3(0.3, 0.36, 0.46) * exp(-md * 0.9) * (1.0 - dens * 0.5);
		col = mix(col, cc, dens * (0.85 - fk * 0.15));
	}
	// 远处闪电：偶尔在一片云里亮一下
	float slot = floor(clock * 2.5);
	float fl = step(0.82, hash(vec2(slot, 1.7))) * exp(-fract(clock * 2.5) * 9.0);
	vec2 lpos = vec2(hash(vec2(slot, 3.1)) * sres.x, (0.1 + 0.3 * hash(vec2(slot, 5.9))) * sres.y);
	col += vec3(0.5, 0.75, 1.0) * fl * exp(-length((px - lpos) / vec2(420.0, 220.0)));
	// 深渊：地面线以下越来越暗，底下翻上来暗红的雾
	float below = smoothstep(feet_y - 40.0, sres.y, px.y);
	float mist = fbm(vec2(px.x * 0.004 + clock * 0.03, px.y * 0.01 - clock * 0.25));
	col = mix(col, vec3(0.01, 0.015, 0.03), below * 0.85);
	col += vec3(0.5, 0.05, 0.08) * smoothstep(0.45, 0.85, mist) * below * 0.6;
	col *= 1.0 - 0.5 * dim;
	col += vec3(0.85, 0.95, 1.0) * flash;
	COLOR = vec4(mix(mix(col, edge.rgb, edge.a), paint.rgb, paint.a), max(shown, edge.a));
}" };

	internal static Shader PlatformShader => _platform ??= new Shader { Code = "shader_type canvas_item;\n" + SceneCommon + @"
uniform float cliff_x = 640.0;
uniform float floor_y = 830.0;
uniform sampler2D plat : filter_linear;
uniform vec2 plat_origin = vec2(0.0);
uniform vec2 plat_size = vec2(0.0);
// 贴图不存在时保留程序生成的断崖。负值在岩石内。
float rock(vec2 px) {
	float top = feet_y - 12.0 - 9.0 * fbm(vec2(px.x * 0.012, 3.1)) + (px.x - cliff_x) * (floor_y - feet_y) / max(sres.x - cliff_x, 1.0);
	float edge = cliff_x + (px.y - feet_y) * 0.28 + (fbm(vec2(px.y * 0.02, 7.7)) - 0.5) * 60.0
		+ smoothstep(feet_y + 40.0, sres.y, px.y) * 120.0 * fbm(vec2(px.y * 0.006, 1.3));
	return max(top - px.y, edge - px.x);
}
void fragment() {
	vec2 px = UV * sres;
	vec3 col;
	float inside;
	if (plat_size.x > 0.0) {
		vec2 puv = (px - plat_origin) / plat_size;
		if (puv.x < 0.0 || puv.x > 1.0 || puv.y < 0.0 || puv.y > 1.0) discard;
		vec4 pt = texture(plat, puv);
		if (pt.a <= 0.0) discard;
		inside = pt.a;
		col = pt.rgb * 0.8;
	} else {
		float d = rock(px);
		inside = 1.0 - smoothstep(-1.0, 1.0, d);
		if (inside <= 0.0) discard;
		float strata = fbm(vec2(px.x * 0.01, px.y * 0.05));
		float crack = smoothstep(0.02, 0.0, abs(fbm(px * 0.02) - 0.5));
		col = mix(vec3(0.02, 0.025, 0.045), vec3(0.06, 0.07, 0.1), strata) * (1.0 - 0.5 * crack);
		float topLit = smoothstep(14.0, 0.0, abs(px.y - (feet_y - 15.0))) * step(d, 0.0);
		col += vec3(0.25, 0.5, 0.75) * topLit * 0.5;
		float rim = smoothstep(-16.0, 0.0, d);
		col += vec3(0.45, 0.05, 0.08) * rim * smoothstep(feet_y, sres.y, px.y) * 1.2;
		col += vec3(0.3, 0.55, 0.8) * rim * smoothstep(feet_y + 60.0, feet_y, px.y) * 0.4;
	}
	col *= 1.0 - 0.45 * dim;
	col += vec3(0.85, 0.95, 1.0) * flash * 0.6;
	float s = revealDist(px);
	vec4 edge = flameEdge(px, s);
	COLOR = vec4(mix(mix(col, edge.rgb, edge.a * inside), paint.rgb, paint.a), inside * smoothstep(-2.0, 2.0, s));
}" };

	// ───────────────────────── 子弹 ─────────────────────────

	/// <summary>
	/// 大罪穿甲弹（第四版，用户 2026-11-19：上一版开枪的特效根本看不出来，而且命中是一个白圆片 + 一个白楔形，几何感太重）：
	/// 弹头是一颗大的能量弹（直径约 0.3 × 四边形高），表面翻滚的等离子把边缘撕得不规则，中间白热；身后拖一股拧着的等离子尾——
	/// 一团越往后越细的等离子 + 两条绕着弹道拧的粗光带（前半圈亮后半圈暗，有立体感），尾巴上每一处按「被弹头经过了多久」变暗、散掉，
	/// 弹道在画面上留得住 0.3s 左右、看得清子弹是从哪飞到哪的。沿 x 从尾到头；head_px = 弹头中心在四边形里的位置。加法。
	/// </summary>
	private static Shader BulletShader => _bullet ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float clock = 0.0;
uniform float len_px = 1000.0;
uniform float thick_px = 420.0;
uniform float head_px = 900.0;
uniform float speed_px = 2800.0;
uniform float extra_age = 0.0;
uniform float fade = 1.0;
" + Noise + @"
void fragment() {
	float x = UV.x, y = (UV.y - 0.5) * 2.0;
	float xp = x * len_px, yp = y * thick_px * 0.5;
	float dx = xp - head_px;
	float R = thick_px * 0.15;
	// 弹头
	vec2 hp = vec2(dx * 0.8, yp);
	float rr = length(hp), ang = atan(hp.y, hp.x);
	float swirl = fbm(vec2(ang * 1.6 + clock * 9.0, rr / R * 1.5 - clock * 14.0));
	float Re = R * (0.85 + 0.35 * swirl);
	float orb = smoothstep(Re + R * 0.25, Re - R * 0.15, rr);
	float core = smoothstep(R * 0.55, R * 0.12, rr + (swirl - 0.5) * R * 0.3);
	float halo = exp(-max(rr - Re, 0.0) / (R * 0.55)) * 0.55;
	// 尾
	float behind = max(-dx, 0.0);
	float age = behind / speed_px + extra_age;
	float life = exp(-age / 0.16);
	float w = R * mix(0.85, 0.3, clamp(behind / (thick_px * 2.2), 0.0, 1.0)) * (0.8 + 0.5 * min(age * 4.0, 1.0));
	float tn = fbm(vec2(xp * 0.012 - clock * 20.0, y * 3.0));
	float on = step(dx, 0.0);
	float body = exp(-pow(yp / w, 2.0) * (1.2 - 0.6 * tn)) * on;
	float ph = xp / (R * 2.4) - clock * 22.0;
	float r1 = abs(yp - w * 0.7 * sin(ph)), r2 = abs(yp - w * 0.7 * sin(ph + 3.14159));
	float rib = (exp(-pow(r1 / (w * 0.32), 2.0)) * (0.55 + 0.45 * cos(ph)) + exp(-pow(r2 / (w * 0.32), 2.0)) * (0.55 + 0.45 * cos(ph + 3.14159))) * on;
	float trail = (body * (0.45 + 0.55 * tn) * 0.9 + rib * 0.9) * life;
	vec3 white = vec3(0.97, 0.99, 1.0), cyan = vec3(0.32, 0.78, 1.0), deep = vec3(0.12, 0.35, 1.0);
	vec3 col = white * (core * 2.4 + orb * 0.9) + mix(cyan, white, 0.3) * orb * swirl * 0.8 + deep * halo
		+ mix(deep, cyan, life) * trail * 1.1 + white * rib * life * 1.1;   // 拧着的两条光带亮一些：看得出是一股在旋的能量，不是一条软光带
	float edge = (1.0 - smoothstep(0.85, 1.0, abs(y))) * smoothstep(0.0, 0.02, x) * (1.0 - smoothstep(0.985, 1.0, x));
	COLOR = vec4(col * fade * edge, 1.0);
}" };

	// ───────────────────────── 时序 ─────────────────────────

	/// <summary>各拍的时刻（从出牌算起，秒）。</summary>
	public const float RevealAt = 0.05f, RevealTime = 0.7f, AimAt = 0.45f, BulletSpeed = 2800f;   // 第四版 → 2800：能量弹在画面里飞 6～7 帧才到第一个敌人，看得见

	/// <summary>
	/// 退场（用户 2026-11-09：收枪结束时场景起码已经开始收回 20% 左右）：枪落回手里（撒旦耶尔开枪 FireAt+0.12 之后 PutAwayAfter + 转两圈 + 一顿）那一刻劈退场的雷；
	/// 场景圈提前 ConcealAfter 开始收、用 ExitTime 收完：半径从画面最远的角开始按 smoothstep 收，收枪结束时（走了 40% 的时间）
	/// 半径收了三成多、画面上明显在收（用户说「起码两成」）；撒旦耶尔从上往下烧（雷后 ~0.58s）的同时圈从外往脚下收，两个一起走完。
	/// </summary>
	public const float ExitAfter = 0.12f + SatanaelPuppet.PutAwayAfter + 0.02f + SatanaelPuppet.SpinTime + 0.04f + ShotHold, ExitTime = 1.0f,
		ConcealAfter = ExitAfter - 0.4f * ExitTime;

	/// <summary>
	/// 蓄力 → 时停 → 开枪（用户 2026-11-10：转完枪要先蓄力「滋——」，马上要打出去那一刻时停住，然后「啪」一枪，特别炸裂）。
	/// 时停要有来由才不显得无厘头：蓄力期间光被一点点吸进枪口、画面跟着暗下去；吸到头一下闪白，全场静止、无声，
	/// 只剩枪口一个极亮的小点（停住时不往画面上加任何新图案）；FreezeTime 之后这一枪把静止打破。
	/// 用户 2026-11-10 看第一版：停着像卡住了，不像停顿，也太久——整张画面一动不动又没声音，和视频卡顿没区别。
	/// 所以世界停住、但枪口的能量不停：悬着的细光以极慢的速度继续往里爬，一圈光从外面收向枪口、正好在开枪那一刻合上（倒计时），
	/// 画面在整段时停里持续变暗，声音是「滋」被拖慢掉下去（像磁带停转）再接一小段往上提的气声；时长也从 0.42 缩到 0.3。
	/// </summary>
	public const float ChargeAt = CatchAt + 0.06f, ChargeTime = 0.95f, FreezeAt = ChargeAt + ChargeTime, FreezeTime = 0.3f;

	/// <summary>第四版：开枪后多留这么久再收场（子弹穿过以后的连环引爆要演完）。</summary>
	public const float ShotHold = 0.3f;

	/// <summary>开枪：时停结束的那一刻。</summary>
	public const float FireAt = FreezeAt + FreezeTime;

	/// <summary>接住枪的时刻。</summary>
	public const float CatchAt = AimAt + SatanaelPuppet.CatchAfter;

	/// <summary>Joker 开始转枪的时刻（从出牌、也就是 cast_sinful_from_mask 开始算）：攥面具 .02+.14、放手 .04+.05、插兜 .06、抬手 .05 之后（tools/p5/build_rig_p5.py）。</summary>

	/// <summary>召唤撒旦耶尔的雷劈下的时刻（用户 2026-11-08：场景还没烧完雷就劈下来，前半段不空）。</summary>
	public const float AppearAt = 0.12f;   // 交接待办 C（用户 2026-11-09：前半段空）：召唤的雷和场景刚烧开同时劈下

	/// <summary>开场炸开的时刻（第二版）：打出后先压暗、往里收这么久，和召唤的雷同一拍。</summary>
	public const float BurstAt = AppearAt;

	/// <summary>整段演出的时长（出牌算起，到场景收回完）。</summary>
	public static float Duration => FireAt + Mathf.Max(ConcealAfter + ExitTime, ExitAfter + 0.6f) + 0.15f;

	public sealed class Setup
	{
		public Vector2 JokerFeet, JokerHead, JokerMuzzle, PersonaMuzzle;
		/// <summary>撒旦耶尔图层画布左上角在屏幕上的位置（1080p 像素）：身体大半在左边外面，只露出翅膀、一部分身体和伸进来的持枪手臂（用户 2026-11-08）。</summary>
		/// 用户 2026-11-09：整个往下挪 150px，头顶的光环才进画面（原来在屏幕上缘外面）；枪臂按枪管线重新对准敌人（构造时自动算），云海跟着一起往下。
		public Vector2 PersonaOrigin = new(-297f, -52f);   // 肩膀约在 (420,230)：枪口停在敌人前面一大段   // 肩膀约在 (520,336)：远处高处、偏左
		public float PersonaScale = 0.7f;   // 第 10 版：远处的巨人，头顶接近屏幕上缘、大腿以下埋进云海（用户 2026-11-08）
		public float FloorY;
		public List<Vector2> Enemies = new();
		/// <summary>穿过第 i 个敌人（按离枪口由近到远）的那一刻：游戏在这里结算这个敌人的伤害。</summary>
		public Action<int>? OnPierce;
		/// <summary>Joker 该开枪的时刻（游戏里播 shiv_from_aim）：FireAt - 0.18。</summary>
		public Action? OnJokerAim, OnJokerShoot;
		/// <summary>场景里的人物（Joker、敌人的 Visuals）：场景期间统一染上夜色，结束时还原。</summary>
		public List<CanvasItem> Actors = new();
		/// <summary>和 Enemies 一一对应的敌人 Visuals（子弹穿过时被光打透、过曝一下）；可以为空。</summary>
		public List<CanvasItem?> EnemyVisuals = new();
		/// <summary>场景里每个人物脚下的接触阴影（脚底世界坐标, 宽度）：用户 2026-11-13：脚和平台贴不实、像两个图层。</summary>
		public List<(Vector2 Feet, float Width)> Shadows = new();
	}

	private static Shader? _mask;

	/// <summary>
	/// 场景圈的遮罩（用户 2026-11-09：场景收回时云和特效要一起收回）：以 origin 为圆心、半径 reveal 的圈（纵向压 1.25，和场景的烧穿圈一样），
	/// 圈外透明，边上往里收 30px 软边（藏在场景的蓝焰边底下）。给场景里的余烬粒子用（场景的余烬、撒旦耶尔烧线上的余烬；加法混合，和 Fx.Additive 一样）；云海在自己的着色器里遮。
	/// </summary>
	private static Shader MaskShader => _mask ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec2 sres = vec2(1920.0, 1080.0);
uniform vec2 origin = vec2(430.0, 830.0);
uniform float reveal = 0.0;
void fragment() {
	float d = length((SCREEN_UV * sres - origin) * vec2(1.0, 1.25));
	COLOR.a *= 1.0 - smoothstep(reveal - 70.0, reveal - 30.0, d);
}" };

	/// <summary>一份场景圈遮罩材质（圆心 = 屏幕坐标），每帧设 reveal。</summary>
	internal static ShaderMaterial MaskMaterial(Vector2 screen, Vector2 origin)
	{
		ShaderMaterial m = new() { Shader = MaskShader };
		m.SetShaderParameter("sres", screen);
		m.SetShaderParameter("origin", origin);
		return m;
	}

	private static GradientTexture2D? _lightTex;

	/// <summary>引擎 2D 灯光用的径向渐变（中心白、边缘透明）。</summary>
	private static Texture2D LightTex => _lightTex ??= new GradientTexture2D
	{
		Width = 256, Height = 256, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f),
		Gradient = new Gradient { Offsets = new[] { 0f, 0.35f, 1f }, Colors = new[] { Colors.White, new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0f) } },
	};

	/// <summary>一盏引擎 2D 点光（PointLight2D，加法）：照亮它范围里的人物、怪物、人格面具和场景。</summary>
	private static PointLight2D Light(Node parent, Vector2 at, Color c, float radiusPx)
	{
		PointLight2D l = new() { Texture = LightTex, TextureScale = radiusPx / 128f, Color = c, Energy = 0f, RangeItemCullMask = SinfulRender.ActorLightMask };
		parent.AddChild(l);
		l.GlobalPosition = at;
		return l;
	}

	// 分帧画出实机对应的着色器变体：独立天空视口、带灯的平台和人物、粒子及命中区。
	internal static void WarmSky(Control slot)
	{
		Vector2 screen = Fx.Screen(slot), c = screen * 0.5f;
		ShaderMaterial sky = new() { Shader = SkyShader };
		sky.SetShaderParameter("sres", screen);
		sky.SetShaderParameter("origin", c);
		sky.SetShaderParameter("reveal", 1e5f);
		SinfulRender.Sky(slot, screen, sky);
	}

	internal static void WarmPlatform(Control slot)
	{
		Vector2 screen = Fx.Screen(slot), c = screen * 0.5f;
		ShaderMaterial rocks = new() { Shader = PlatformShader };
		rocks.SetShaderParameter("sres", screen);
		rocks.SetShaderParameter("origin", c);
		rocks.SetShaderParameter("reveal", 1e5f);
		if (Fx.Tex("sinful_platform") is Texture2D platform)
		{
			rocks.SetShaderParameter("plat", platform);
			rocks.SetShaderParameter("plat_origin", c);
			rocks.SetShaderParameter("plat_size", new Vector2(36f, 18f));
		}
		slot.AddChild(new ColorRect
		{
			Size = screen, Material = rocks, LightMask = SinfulRender.ActorLightMask,
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
		Light(slot, c, Colors.White, 64f).Energy = 1f;
	}

	internal static void WarmPuppet(Control slot)
	{
		if (!SatanaelPuppet.Available) return;
		Vector2 c = Fx.Screen(slot) * 0.5f;
		Node2D puppetRoot = new();
		slot.AddChild(puppetRoot);
		SatanaelPuppet puppet = new(puppetRoot, new Vector2(6f, 0f), 0.05f, c + new Vector2(48f, 24f));
		puppet.Update(1f, 0f, 0.5f, 1.5f, 9f);
		puppet.SceneMask(c, 1e5f);
		puppet.LightFrom(c + new Vector2(32f, -32f));
		SinfulRender.LightActors(slot, new[] { puppetRoot });
		Light(slot, c, Colors.White, 64f).Energy = 1f;
	}

	internal static void WarmImpact(Control slot)
	{
		Vector2 c = Fx.Screen(slot) * 0.5f;
		GpuParticles2D embers = ReadyFx.Embers(slot, c, new[] { Colors.White, Colors.White }, 4, 8f, 0.3f, 10f, 20f, 5f);
		embers.Material = MaskMaterial(Fx.Screen(slot), c);
		HitZone(slot, c, Vector2.Right, c.Y, 0f, 0.02f);
	}

	/// <summary>放整段大罪穿甲弹。back = 背景层（场景、人格面具），front = 前景层（子弹、命中）。返回子弹穿过每个敌人的时刻（秒，从现在算）。</summary>
	/// <summary>打中的那一声（用户 2026-11-09 对比后选定 sinful_pierce_last）。</summary>
	public static string HitSound = Sfx.SinfulPierceLast;

	public static float[] Play(Control back, Control front, Setup s)
	{
		ulong started = Time.GetTicksUsec();
		Vector2 screen = Fx.Screen(back);
		float unit = screen.Y / 1080f;
		Func<Vector2, Vector2> toScreen = w => Fx.ScreenPoint(back, w);
		Vector2 feetS = toScreen(s.JokerFeet), floorS = toScreen(new Vector2(0f, s.FloorY));
		var enemiesOrdered = s.Enemies.Select((e, i) => (e, i)).OrderBy(t => t.e.DistanceSquaredTo(s.PersonaMuzzle)).ToList();
		// ── 场景：天空层（最底下）+ 断崖层（盖在人格面具前面）
		Node2D root = Fx.ScreenRoot(back, "P5SinfulShellScene");
		// 场景切换，不叠（用户 2026-11-13）：群蛇形态的咒雾 / 蛇、毒雾在华丽收场期间整个藏起来，演完放回来
		root.TreeExiting += () => { SerpentMantle.Suspend(false); FumesField.Suspend(false); };
		back.MoveChild(root, 0);
		ShaderMaterial sky = new() { Shader = SkyShader }, rocks = new() { Shader = PlatformShader };
		ShaderMaterial[] scenery = { sky, rocks };
		float total = FireAt + Mathf.Max(ConcealAfter + ExitTime, ExitAfter + 0.6f) + 0.15f;
		foreach (ShaderMaterial m in scenery)
		{
			m.SetShaderParameter("sres", screen);
			m.SetShaderParameter("origin", feetS);
			m.SetShaderParameter("feet_y", feetS.Y);
		}
		rocks.SetShaderParameter("floor_y", floorS.Y);
		rocks.SetShaderParameter("cliff_x", feetS.X - 95f * unit);
		Vector2 moonS = new(screen.X * 0.82f, screen.Y * 0.16f);              // 右上的空天：敌人头上、撒旦耶尔翅膀够不到的地方
		sky.SetShaderParameter("moon", new Vector3(moonS.X, moonS.Y, 58f * unit));
		if (Fx.Tex("sinful_platform") is Texture2D plat)
		{
			// Codex 的平台：原图 3840×1100（×0.52），顶面 y=140、悬崖边 x=760 → 悬崖边对到 Joker 脚边、顶面对到脚底
			float k = unit;
			Vector2 size = plat.GetSize() * k;
			Vector2 org = new(feetS.X - 95f * unit - 760f * 0.52f * k, feetS.Y - 140f * 0.52f * k - 9f * unit);   // 2026-11-13：顶面往上 9px，脚踩在顶面上（原来脚悬在边线上面）
			rocks.SetShaderParameter("plat", plat);
			rocks.SetShaderParameter("plat_origin", org);
			rocks.SetShaderParameter("plat_size", size);
		}
		ColorRect skyRect = SinfulRender.Sky(root, screen, sky);
		Node2D personaSlot = new() { Name = "PersonaSlot" };          // 撒旦耶尔第 2 版立绘放这里（天空和断崖之间）
		ColorRect rockRect = new() { Size = screen, Material = rocks, MouseFilter = Control.MouseFilterEnum.Ignore, LightMask = SinfulRender.ActorLightMask };
		root.AddChild(personaSlot);
		root.AddChild(rockRect);
		Fx.Clock(skyRect);
		Fx.Clock(rockRect);
		// ── 撒旦耶尔（部件动画）：先摆到开枪那一刻量出枪口，再回到登场姿势
		const float RiseAt = AppearAt;
		float exitAt = FireAt + ExitAfter;
		// 撒旦耶尔自己的时钟：时停期间不走（姿势、翅膀、雷都停在那一刻），之后接着走
		const float pFire = FireAt - FreezeTime;
		float pExit = exitAt - FreezeTime;
		Vector2 aimScreen = s.Enemies.Count > 0 ? toScreen(s.Enemies.Aggregate(Vector2.Zero, (a, b) => a + b) / s.Enemies.Count) : screen * 0.7f;
		SatanaelPuppet? puppet = SatanaelPuppet.Available ? new SatanaelPuppet(personaSlot, s.PersonaOrigin * unit, s.PersonaScale * unit, aimScreen) : null;
		if (puppet != null)
		{
			puppet.Update(pFire - 0.0005f, RiseAt, AimAt, pFire, pExit);
			s.PersonaMuzzle = puppet.MuzzleGlobal;
			puppet.Update(0f, RiseAt, AimAt, pFire, pExit);
		}
		Vector2 aimDir = ((enemiesOrdered.Count > 0 ? enemiesOrdered[^1].e : s.PersonaMuzzle + new Vector2(1000f, 200f)) - s.PersonaMuzzle).Normalized();
		float far = (screen * 1.3f).Length();
		// ── 光影（用户 2026-11-08：要照到人格面具、人物和怪物身上）：引擎 2D 点光 + 场景里的人物统一染上夜色
		Node2D lights = new() { Name = "P5SinfulLights" };
		front.AddChild(lights);
		Vector2 world(Vector2 sp) => back.GetCanvasTransform().AffineInverse() * sp;
		PointLight2D muzzleLight = Light(lights, s.PersonaMuzzle, new Color(0.5f, 0.82f, 1f), 520f * unit);
		PointLight2D flashLight = Light(lights, s.PersonaMuzzle, new Color(0.85f, 0.93f, 1f), 2600f * unit);
		PointLight2D bulletLight = Light(lights, s.PersonaMuzzle, new Color(0.55f, 0.85f, 1f), 560f * unit);
		PointLight2D thunderLight = Light(lights, world(new Vector2(screen.X * 0.12f, screen.Y * 0.3f)), new Color(0.7f, 0.88f, 1f), 1900f * unit);
		PointLight2D abyssLight = Light(lights, world(new Vector2(screen.X * 0.08f, screen.Y * 1.05f)), new Color(0.85f, 0.12f, 0.18f), 1100f * unit);
		// 月光：从月亮那里打下来的一盏大范围冷白光，照场景里的人物、怪物、撒旦耶尔和平台（天空自己不吃灯光）
		PointLight2D moonLight = Light(lights, world(moonS), new Color(0.7f, 0.78f, 0.95f), 2600f * unit);
		puppet?.LightFrom(moonS);
		var actorRest = s.Actors.Where(GodotObject.IsInstanceValid).Select(a => (a, a.Modulate)).ToList();
		SinfulRender.LightActors(root, s.Actors.Append(personaSlot));
		// 脚下的接触阴影：一大片软的冷黑影（往月亮反方向略拖长）+ 脚底一道更实的接触暗线，让人物「踩」在平台上
		Node2D shadowRoot = new() { Name = "P5SinfulShadows" };
		back.AddChild(shadowRoot);
		shadowRoot.GlobalPosition = Vector2.Zero;   // 子节点直接用世界坐标
		Fx.FreeAfter(shadowRoot, total + 0.1);
		var shadows = new List<(Sprite2D Soft, Sprite2D Core)>();
		foreach ((Vector2 sf, float sw) in s.Shadows)
		{
			Sprite2D soft = new() { Texture = Glow.Soft, Position = sf + new Vector2(-sw * 0.12f, -sw * 0.01f), Scale = new Vector2(sw * 1.35f, sw * 0.22f) / 32f };
			Sprite2D core = new() { Texture = Glow.Soft, Position = sf + new Vector2(0f, -sw * 0.005f), Scale = new Vector2(sw * 0.8f, sw * 0.08f) / 32f };
			soft.Modulate = core.Modulate = new Color(0f, 0f, 0f, 0f);
			shadowRoot.AddChild(soft);
			shadowRoot.AddChild(core);
			shadows.Add((soft, core));
		}
		float now = 0f, shotAt = 1e9f;                              // 当前时刻；开枪回调执行的时刻
		// 第二版（用户 2026-11-19）：开场炸开 / 开枪的冲击帧按真实时间算（顿帧期间时间流速很慢，帧数照样要对）
		double burstReal = -1.0, shotReal = -1.0;
		static int Beat(double since) => since < 0.0 ? 0 : since < 0.045 ? 1 : since < 0.09 ? 2 : 0;
		// 开场的幕（在人物后面）：打出时先把背景压暗，炸开那一下刷白 → 刷黑，再退掉露出烧穿的场景
		Node2D veilRoot = Fx.ScreenRoot(back, "P5SinfulVeil");
		ColorRect veil = new() { Size = screen, Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		veilRoot.AddChild(veil);
		Fx.FreeAfter(veilRoot, BurstAt + 1.0);
		var pierceOf = new Dictionary<CanvasItem, float>();        // 敌人 → 子弹穿过它的时刻（下面算出 pierce 后填）
		var pierceReal = new Dictionary<CanvasItem, double>();     // 敌人 → 子弹真的穿过它那一刻的真实时间（发白按真实时间退）
		Color grade = new(0.6f, 0.68f, 0.95f);
		Fx.FreeAfter(lights, total + 0.1);
		root.TreeExiting += () =>
		{
			foreach ((CanvasItem a, Color c) in actorRest)
			{
				if (GodotObject.IsInstanceValid(a)) a.Modulate = c;
			}
		};
		// 收回的起始半径：圈心到画面四个角里最远的那个（纵向 ×1.25，和场景圈一致）+ 噪声边的余量
		float visR = new[] { Vector2.Zero, new Vector2(screen.X, 0f), screen, new Vector2(0f, screen.Y) }
			.Max(k => ((k - feetS) * new Vector2(1f, 1.25f)).Length()) + 90f * unit;
		ShaderMaterial emberMask = MaskMaterial(screen, feetS);                   // 场景里的余烬也只在圈里面
		Action<float> step = t =>
		{
			float time = t * total;
			float rv = time < BurstAt ? 0f : 1f - Mathf.Exp(-(time - BurstAt) / 0.12f);
			float surge = time < BurstAt ? 0f : Mathf.Exp(-(time - BurstAt) / 0.22f);
			double real = HitStop.RealSeconds();
			int gBurst = burstReal < 0.0 ? 0 : Beat(real - burstReal), gShot = shotReal < 0.0 ? 0 : Beat(real - shotReal);
			int beat = Math.Max(gBurst, gShot);
			if (GodotObject.IsInstanceValid(veil))
			{
				float sinceB = burstReal < 0.0 ? -1f : (float)(real - burstReal);
				veil.Color = time < BurstAt ? new Color(0.008f, 0.016f, 0.045f, 0.66f * Mathf.SmoothStep(0f, 1f, time / 0.09f))
					: gBurst == 1 ? new Color(0.96f, 0.97f, 1f, 1f)
					: gBurst == 2 ? new Color(0.004f, 0.008f, 0.025f, 1f)
					: new Color(0.004f, 0.008f, 0.025f, sinceB < 0f ? 0.66f : Mathf.Exp(-Mathf.Max(0f, sinceB - 0.09f) / 0.05f));
			}
			float ex = Mathf.Clamp((time - FireAt - ConcealAfter) / ExitTime, 0f, 1f);       // 收枪快结束时场景开始收回（和撒旦耶尔退场重叠）
			// 用户 2026-11-09：切入和消失都以 Joker 脚下为圆心——展开时圈往外烧，收回时圈原路缩回脚下（和展开镜像：先快后慢）；
			// 云海、余烬这些画在场景上的东西都用同一个圈遮住，一起收回
			// 收到最后半径压到 -160：蓝焰边整圈烧没，不在脚下留一摊火
			// 第二版：不再从脚下慢慢烧开——炸开那一下 0.1s 里就铺满屏幕（先快后慢），边上一道很厚的蓝焰浪（surge）
			float reveal = ex <= 0f ? (time < BurstAt ? -300f * unit : far * (1f - Mathf.Exp(-(time - BurstAt) / 0.075f)))
				: visR * (1f - ex * ex * (3f - 2f * ex)) - 160f * unit * ex;   // 收回：从画面最远的角开始（马上看得出在收），先慢后快再慢，合到脚下
			emberMask.SetShaderParameter("reveal", reveal);
			const float dim = 0f;
			now = time;
			float fly = time - shotAt;                                    // 开枪回调真正执行的那一帧算起（定时器和补间差一帧，闪光不能抢在特效前面）
			const float flash = 0f;   // 第二版：开枪的亮由两帧冲击帧负责；之后不再整片刷白（打中的顿帧会把白闪冻住，画面洗成一片灰白）
			// 开枪后云层里还滚着几下余光（不规则地闪几下，1 秒内灭掉）
			float after = fly >= 0.1f ? 0.5f * Mathf.Exp(-(fly - 0.1f) / 0.45f) * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(fly * 23f) * Mathf.Sin(fly * 37f + 1.3f)), 2f) : 0f;
			// 接住枪时枪口一闪 → 蓄力：枪口的光越来越亮、带一点抖 → 时停：收成稳稳的一点 → 开枪：全放出来
			float ck = Mathf.Clamp((time - ChargeAt) / ChargeTime, 0f, 1f);
			bool frozen = time >= FreezeAt && fly < 0f;
			float charge = time < CatchAt ? 0f
				: time < FreezeAt ? 0.9f * Mathf.Exp(-(time - CatchAt) / 0.08f) + (time >= ChargeAt ? (0.15f + 1.0f * ck * ck) * (0.88f + 0.12f * Mathf.Sin(time * 71f)) : 0f)
				: frozen ? 0.6f : 2.0f * Mathf.Exp(-fly / 0.1f);
			float pg = time < FreezeAt ? time : frozen ? FreezeAt : FreezeAt + fly;
			float thunder = 0f;
			if (puppet != null)
			{
				puppet.Update(pg, RiseAt, AimAt, pFire + 0.12f, pExit, puppet.MuzzleLocal, charge, flash * 0.6f, dim);
				puppet.SceneMask(feetS, reveal);
				thunder = puppet.Lightning;
				muzzleLight.GlobalPosition = puppet.MuzzleGlobal;
			}
			foreach (ShaderMaterial m in scenery)
			{
				m.SetShaderParameter("reveal", reveal);
				m.SetShaderParameter("dim", dim);
				m.SetShaderParameter("flash", flash * 0.5f + after * 0.3f + thunder * 0.12f);   // 云里的余光弱一点（整片天一闪一闪像闪屏）
				m.SetShaderParameter("surge", surge);
			}
			// 开枪的冲击帧：第一帧场景刷白、人物全黑（剪影），第二帧场景全黑、人物被枪光打亮
			Color paintSky = gShot == 1 ? new Color(0.96f, 0.97f, 1f, 1f) : gShot == 2 ? new Color(0.004f, 0.008f, 0.022f, 1f) : new Color(0f, 0f, 0f, 0f);
			sky.SetShaderParameter("paint", paintSky);
			rocks.SetShaderParameter("paint", paintSky);
			puppet?.Paint(gShot == 1 ? new Color(0f, 0f, 0f, 1f) : gShot == 2 ? new Color(0.78f, 0.9f, 1f, 0.72f) : new Color(0f, 0f, 0f, 0f), paintSky);
			// 灯：枪口蓄力光、开枪全场白光、跟着子弹飞的光、雷光、深渊往上打的暗红光
			float scene = rv * (1f - ex);
			// 群蛇形态 / 毒雾的场景跟着断崖场景交叉淡化（断崖出来多少，它们就淡掉多少）
			float other = 1f - Mathf.Clamp(scene * 1.6f, 0f, 1f);
			SerpentMantle.Fade(other);
			FumesField.Fade(other);
			foreach ((Sprite2D soft, Sprite2D core) in shadows)
			{
				float sh = beat > 0 ? 0f : scene;
				soft.Modulate = new Color(0.01f, 0.02f, 0.06f, 0.62f * sh);
				core.Modulate = new Color(0f, 0f, 0.02f, 0.8f * sh);
			}
			muzzleLight.Energy = charge * 1.1f;
			flashLight.Energy = 2.2f * flash + 1.2f * after;
			bulletLight.Energy = fly >= 0f && fly < 0.6f ? 1.3f * (1f - fly / 0.6f) : 0f;
			if (fly >= 0f)
			{
				bulletLight.GlobalPosition = s.PersonaMuzzle + (aimDir * BulletSpeed * unit * fly);
			}
			thunderLight.Energy = thunder * 1.8f;
			abyssLight.Energy = 0.9f * scene;
			moonLight.Energy = 0.55f * scene;
			// 场景里的人物染上夜色（冷蓝、压暗）
			Color tint = Colors.White.Lerp(grade, scene * (1f - 0.3f * dim) + 0.15f * dim);
			foreach ((CanvasItem a, Color c) in actorRest)
			{
				if (!GodotObject.IsInstanceValid(a)) continue;
				// 用户 2026-11-09：子弹穿过敌人的一瞬间被光从里面打透——不是描边，而是整只过曝成冷白再退回去（modulate 超过 1 就是提亮）
				// 第四版：被打透的发白按真实时间 0.06s 退掉（顿帧里不会一直白着、把命中特效盖住），也不再白到纯白
				float ox = pierceReal.TryGetValue(a, out double pr) ? Mathf.Exp(-(float)(real - pr) / 0.06f) : 0f;
				a.Modulate = beat == 1 ? new Color(0f, 0f, 0f, c.A)
					: beat == 2 ? new Color(2.3f, 2.6f, 3.1f, c.A)
					: c * tint * new Color(1f + 1.4f * ox, 1f + 1.7f * ox, 1f + 2.2f * ox, 1f);
			}
		};
		Fx.Animate(root, 0, total, step);
		Fx.FreeAfter(root, total + 0.1);
		// 开场（第二版，用户 2026-11-19：从脚下慢慢扩开太柔弱、没分量）：
		// 打出 → 背景压暗、面具上的蓝焰往里一收（BurstAt 秒）→ 顿帧里两帧冲击帧（白底黑剪影 → 黑底亮剪影）→ 蓝焰从他身上整团炸开，
		// 场景被这一炸 0.1s 掀开、边上一道很厚的蓝焰浪；同一拍劈下召唤撒旦耶尔的雷、低频重击、重震
		Vector2 chest = s.JokerFeet.Lerp(s.JokerHead, 0.55f);
		Sprite2D gather = Glow.SoftSprite(front, s.JokerHead, new Color(0.45f, 0.8f, 1f), 10f * unit);
		gather.Material = Fx.Additive;
		Fx.Animate(gather, 0, BurstAt, t =>
		{
			gather.Scale = Vector2.One * Mathf.Lerp(10f, 2f, t * t) * unit;
			gather.Modulate = new Color(1f, 1f, 1f, 0.2f + 0.8f * t);
		});
		Fx.FreeAfter(gather, BurstAt + 0.02);
		Fx.After(front, BurstAt, () =>
		{
			burstReal = HitStop.RealSeconds();
			HitStop.Request(0.08);
			// 炸开的火、光、烟等两帧冲击帧过去再出来（顿帧 0.08s 真实时间 ≈ 0.0064s 游戏时间，再过 0.01s 冲击帧结束）
			const float afterBeat = 0.016f;
			FireBurst(front, chest, 2000f * unit, 0.45f, 1.3f, afterBeat);
			Glow.Flash(front, s.JokerHead, new Color(0.6f, 0.88f, 1f), 520f * unit, 0.2f, afterBeat);
			Fx.After(front, afterBeat, () => Glow.Sparks(front, chest, 30, 800f, 2200f, 0.45f,
				new[] { new Color(0.92f, 0.97f, 1f), new Color(0.35f, 0.75f, 1f), new Color(0.1f, 0.3f, 0.9f) }, 400f, 0.08f, 0.24f, Vector2.Up, 180f));
			Smoke(back, s.JokerFeet + new Vector2(0f, -60f * unit), 1100f * unit, 1.3f, new Vector2(0f, -0.2f), 1.4f, 0.55f, afterBeat);
			Sfx.Play(Sfx.SubBoom, 1.0f);
			Sfx.Play(Sfx.Crush, 0.75f);
			Fx.Shake(9);
			Stage.ShakeDir?.Invoke(4, 90f);
		});
		Fx.After(front, BurstAt + 0.16f, () => Fx.Shake(5));
		// 召唤 / 退场的闪雷声；接住枪的金属声
		// 场景圈展开 / 收回的轻声（压在雷和开枪声下面）：收回那条 0.7s 的卷入正好在圈缩到脚下时收住
		Sfx.Play(Sfx.SinfulSweepOpen, 0.8f);                         // 压暗、往里收的那一下
		Fx.After(front, FireAt + ConcealAfter + ExitTime - 0.7f, () => Sfx.Play(Sfx.SinfulSweepClose, 0.85f));
		Fx.After(front, AppearAt, () => Sfx.Play(Sfx.SinfulThunder, 1.0f));   // 和炸开同一拍（震屏在炸开里）
		Fx.After(front, exitAt, () => { Sfx.Play(Sfx.SinfulThunder, 0.85f); Fx.Shake(2); });
		if (puppet != null)
		{
			// 转枪的破风声：只留撒旦耶尔的（用户 2026-11-09：Joker 的转枪声去掉；接枪的金属声也去掉——它正好落在开枪的倒吸里，叠在一起）
			Fx.After(front, AimAt + SatanaelPuppet.Anticipation, () => Sfx.Play(Sfx.SinfulTwirl, 0.3f));
			// 收枪的转枪声（轻一点）；撒旦耶尔的开枪时刻是 FireAt + 0.12（Update 里传的）
			Fx.After(front, FireAt + 0.12f + SatanaelPuppet.PutAwayAfter + 0.02f, () => Sfx.Play(Sfx.SinfulTwirl, 0.25f));
		}
		// 场景里飘的蓝色余烬
		Fx.After(back, RevealAt + RevealTime * 0.6f, () =>
		{
			GpuParticles2D embers = ReadyFx.Embers(root, new Vector2(screen.X * 0.5f, screen.Y * 0.9f), new[] { new Color(0.8f, 0.95f, 1f), new Color(0.3f, 0.7f, 1f), new Color(0.1f, 0.3f, 0.9f) },
				26, screen.X * 0.5f, 3.2f, 30f, 110f, 25f);
			Fx.After(root, FireAt + ExitAfter - (RevealAt + RevealTime * 0.6f), () => embers.Emitting = false);
			embers.Material = emberMask;
		});

		// ── 举枪、蓄力
		// Joker 同步转枪：他的转枪（aim_in）约 0.12s 接住，对到撒旦耶尔接住枪的那一刻
		Fx.After(front, puppet != null ? CatchAt - 0.12f : AimAt, () => s.OnJokerAim?.Invoke());
		Vector2 muzzle = s.PersonaMuzzle;
		Vector2 aimEnd = enemiesOrdered.Count > 0 ? enemiesOrdered[^1].e : muzzle + new Vector2(1000f, 200f);
		Vector2 dir = (aimEnd - muzzle).Normalized();
		// 接住枪那一下：枪口一点亮光（不蓄力）
		if (puppet != null)
		{
			Fx.After(front, CatchAt, () => Glow.Flash(front, puppet.MuzzleGlobal, new Color(0.7f, 0.9f, 1f), 160f * unit, 0.12f));
		}

		// ── 蓄力 → 时停（用户 2026-11-10）
		// 蓄力：「滋——」越来越尖，到时停那一刻戛然而止（同一条声音文件里后面接 FreezeTime 的静默，只剩一丝极轻的高音）；游戏其他声音一起压下去
		Action? endCharge = null;
		Fx.After(front, ChargeAt, () =>
		{
			Sfx.Play(Sfx.SinfulCharge, 1.0f);
			Sfx.Duck(-20f, ChargeTime + FreezeTime, 1.2f);
			endCharge = Charge(front, () => puppet?.MuzzleGlobal ?? muzzle, dir, screen, unit);
		});
		// 蓄到后半段地面开始发抖（越来越密），时停前收住
		Fx.After(front, ChargeAt + 0.45f, () => Fx.Shake(1));
		Fx.After(front, ChargeAt + 0.62f, () => Fx.Shake(1));
		Fx.After(front, ChargeAt + 0.74f, () => Fx.Shake(2));
		// 时停：场景里自己会动的东西（火焰、云、余烬、撒旦耶尔的翅膀）全部停住；敌人的待机动画也停住（Joker 这时本来就举着枪不动）
		var held = new List<(Node n, Node.ProcessModeEnum mode)>();
		void release()
		{
			foreach ((Node n, Node.ProcessModeEnum mode) in held)
			{
				if (GodotObject.IsInstanceValid(n)) n.ProcessMode = mode;
			}
			held.Clear();
			Fx.HoldClocks(root, false);
		}
		Fx.After(front, FreezeAt, () =>
		{
			Fx.HoldClocks(root, true);
			foreach (CanvasItem? ev in s.EnemyVisuals)
			{
				if (ev == null || !GodotObject.IsInstanceValid(ev)) continue;
				held.Add((ev, ev.ProcessMode));
				ev.ProcessMode = Node.ProcessModeEnum.Disabled;
			}
		});
		Fx.After(front, FireAt, release);
		root.TreeExiting += release;

		// ── 开枪
		Fx.After(front, FireAt - 0.18f, () => s.OnJokerShoot?.Invoke());       // shiv_from_aim 前 0.18s 是举枪不动，落在时停里看不出来
		// 开枪声（第三版，用户 2026-11-10：要特别响亮、特别炸裂）：从静默里直接炸开，冲击在文件 0.02s
		Fx.After(front, FireAt - 0.02f, () => Sfx.Play(Sfx.SinfulShot, 1.4f));
		// Joker 同步开枪，但大罪穿甲弹是撒旦耶尔打出来的：Joker 枪口只有一小点蓝光，不抢戏（用户 2026-11-08）
		Fx.After(front, FireAt, () => Glow.Flash(front, s.JokerMuzzle, new Color(0.5f, 0.85f, 1f), 70f * unit, 0.1f));
		var pierce = new float[s.Enemies.Count];
		var orderIndex = enemiesOrdered.Select(t => t.i).ToList();
		var travel = new float[enemiesOrdered.Count];
		for (int k = 0; k < enemiesOrdered.Count; k++)
		{
			travel[k] = Mathf.Max(0.03f, (enemiesOrdered[k].e - muzzle).Dot(dir) / (BulletSpeed * unit));
			pierce[orderIndex[k]] = FireAt + travel[k];
			if (orderIndex[k] < s.EnemyVisuals.Count && s.EnemyVisuals[orderIndex[k]] is CanvasItem ev) pierceOf[ev] = FireAt + travel[k];
		}
		var stopHits = new List<Vector2>();
		for (int k = 0; k < enemiesOrdered.Count; k++)
		{
			Vector2 e = enemiesOrdered[k].e;
			stopHits.Add((muzzle + dir * (e - muzzle).Dot(dir)).Lerp(e, 0.6f));
		}
		Fx.After(front, FireAt, () =>
		{
			shotAt = now;
			shotReal = HitStop.RealSeconds();                              // 冲击帧（在 step 里按真实时间画两帧）
			endCharge?.Invoke();
			Fx.Shake(9);
			Stage.ShakeDir?.Invoke(4, Mathf.RadToDeg((-dir).Angle()));      // 后坐：往枪的反方向一推
			Glow.Flash(front, muzzle, new Color(0.85f, 0.95f, 1f), 260f * unit, 0.08f);
			puppet?.Blast();                                               // 远处云海被枪口冲击波压开、翻卷
			FireBurstDir(front, muzzle, dir, 440f * unit, 0.2f, 1.25f, new Vector2(1.6f, 0.72f));   // 枪口炸开的火舌（开场那种炸开，朝前拉长；小而短，不糊住弹道）
			// 炮口烟：一大团往前推、往上飘的烟（画在人物后面、撒旦耶尔前面），一开始被枪火照亮
			Bullet(front, muzzle, dir, screen, unit);                      // 看得见的重弹
			Tunnel(back, muzzle, dir, screen.Length() * 1.2f, BulletSpeed * unit, 280f * unit, 1.3f);   // 身后留下的一条暗烟
			Glow.Sparks(front, muzzle, 40, 900f, 2600f, 0.42f, new[] { new Color(0.95f, 0.98f, 1f), new Color(0.35f, 0.75f, 1f), new Color(0.6f, 0.3f, 1f) }, 0f,
				0.06f, 0.18f, dir, 26f);
		});
		Fx.After(front, FireAt, () => Sfx.Play(Sfx.SubBoom, 1.0f));
		// 后坐的余震：一下不够，接着再震两下（越来越轻）
		Fx.After(front, FireAt + 0.12f, () => Fx.Shake(8));
		Fx.After(front, FireAt + 0.26f, () => Fx.Shake(5));
		for (int k = 0; k < enemiesOrdered.Count; k++)
		{
			int kk = k, idx = orderIndex[k];
			Vector2 e = enemiesOrdered[k].e;
			// 弹道上离这个敌人最近的那一点（子弹从这里穿过去）
			Vector2 hit = muzzle + dir * (e - muzzle).Dot(dir);
			hit = hit.Lerp(e, 0.6f);
			bool first = k == 0, last = k == enemiesOrdered.Count - 1;
			Node2D? body = idx < s.EnemyVisuals.Count ? s.EnemyVisuals[idx] as Node2D : null;
			Fx.After(front, FireAt + travel[k], () =>
			{
				if (body != null) pierceReal[body] = HitStop.RealSeconds();
				Pierce(front, back, hit, dir, unit, first, last, body, root);
				s.OnPierce?.Invoke(idx);
			});
		}
		// 打到敌人那边（用户 2026-11-09）：不是每个敌人各一套，而是整片命中区一起——几圈冲击波沿弹道往外扩、地面一圈波纹贴着平台扫开、尘土和碎光
		if (stopHits.Count > 0)
		{
			Vector2 zone = stopHits.Aggregate(Vector2.Zero, (a, b) => a + b) / stopHits.Count;
			float span = stopHits.Count > 1 ? stopHits.Max(h => h.DistanceTo(zone)) : 0f;
			Fx.After(front, FireAt + travel[0], () => HitZone(front, zone, dir, s.FloorY, span, unit));
		}
		// 打中：一发子弹，只响一声（用户 2026-11-09：不是打中几个敌人就放几次），落在子弹碰到第一个敌人那一刻。
		// 飞过镜头的呼啸去掉了（和开枪的冲击叠在一起）
		if (travel.Length > 0)
		{
			Fx.After(front, FireAt + travel[0], () => Sfx.Play(HitSound, 1.3f));
			// 第四版（用户：打到敌人那会儿炸开要更明显、更华丽）：子弹一路穿过所有敌人以后，沿弹道从第一个起一个接一个连环引爆——
			// 每处一团大的蓝焰炸开 + 冲起一根蓝焰火柱，最后一个最大；第一下引爆顿住、低频重击、重震（原来打中那一下的顿帧挪到这里）
			float boomAt = FireAt + travel[^1] + 0.07f;
			for (int k = 0; k < stopHits.Count; k++)
			{
				int kk = k;
				bool lastBoom = k == stopHits.Count - 1;
				Fx.After(front, boomAt + 0.08f * k, () =>
				{
					Detonate(front, stopHits[kk], unit, lastBoom);
					if (enemiesOrdered[kk].i < s.EnemyVisuals.Count && s.EnemyVisuals[enemiesOrdered[kk].i] is CanvasItem ev2) pierceReal[ev2] = HitStop.RealSeconds();
					if (kk == 0)
					{
						HitStop.Request(0.1);
						Sfx.Play(Sfx.SubBoom, 1.1f);
						Sfx.Play(Sfx.Crush, 0.95f);
						Fx.Shake(9);
						Stage.ShakeDir?.Invoke(4, -90f);
					}
					else if (lastBoom)
					{
						Fx.Shake(7);
					}
				});
			}
		}
		SinfulRender.Observe(root, started);
		return pierce;
	}

	// ───────────────────────── 开枪的冲击 ─────────────────────────

	private static Shader? _hush, _charge;

	/// <summary>蓄力 / 时停时盖在画面上的一层暗（不读屏幕）：深蓝黑，枪口周围留一圈稍亮的地方——像是只有枪口那点光还照得到。</summary>
	private static Shader HushShader => _hush ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 sres = vec2(1920.0, 1080.0);
uniform vec2 center = vec2(900.0, 400.0);
uniform float alpha = 0.0;
uniform float hole = 520.0;
void fragment() {
	float d = length(UV * sres - center);
	COLOR = vec4(0.006, 0.012, 0.035, alpha * mix(0.4, 1.0, smoothstep(0.0, hole, d)));
}" };

	/// <summary>
	/// 蓄力（用户 2026-11-10）：一张以枪口为中心的加法四边形，全部过程式——
	/// ① 从四周被吸进枪口的发丝细光（按角度分格，每格一道，越靠近越快、拉得越长，越蓄越多）；
	/// ② 套在枪管上的光圈一圈圈往枪口收（垂直于枪管的椭圆）；③ 枪口周围乱窜的细电弧（「滋」）；④ 枪口越来越亮的核。
	/// t 是蓄力自己的时钟：时停时不再走，所有东西原地悬住、暗下去；fz 0→1：核收成一个极亮的小点，带一道很短的横向细光（不做十字——十字像准心）。只有冷白和青，不加别的颜色。
	/// </summary>
	private static Shader ChargeShader => _charge ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float t = 0.0;
uniform float k = 0.0;
uniform float fz = 0.0;
uniform float close = 0.0;
uniform float rt = 0.0;
uniform float size_px = 1600.0;
uniform float unit = 1.0;
uniform float dir_ang = 0.0;
float h(float n) { return fract(sin(n * 127.1) * 43758.5453); }
float vn(float x) { float i = floor(x), f = x - i; f = f * f * (3.0 - 2.0 * f); return mix(h(i), h(i + 1.0), f); }
void fragment() {
	vec2 p = (UV - 0.5) * size_px;
	float r = length(p), ang = atan(p.y, p.x);
	float edge = 1.0 - smoothstep(0.4, 0.5, length(UV - 0.5));
	vec3 white = vec3(0.95, 0.98, 1.0), cyan = vec3(0.3, 0.74, 1.0);
	vec3 col = vec3(0.0);
	// ① 被吸进来的发丝细光
	const float N = 110.0;
	float c = (ang + PI) / TAU * N, cell = floor(c), f = c - cell - 0.5;
	float wpx = abs(f) * (TAU / N) * r;
	for (int l = 0; l < 2; l++) {
		float fl = float(l);
		float sp = mix(0.7, 1.5, h(cell * 1.7 + fl * 31.0));
		float cyc = t * sp + h(cell * 3.3 + fl * 11.0);
		float ph = fract(cyc), id = floor(cyc);
		float on = step(1.0 - (0.1 + 0.5 * k), h(cell + id * 7.3 + fl * 5.0));
		float r_out = mix(340.0, 760.0, h(cell * 2.1 + id + fl)) * unit;
		float head = mix(r_out, 14.0 * unit, ph * ph);
		float len = (24.0 + 150.0 * ph) * unit;
		float a = (r - head) / len;
		float body = step(0.0, a) * (1.0 - smoothstep(0.0, 1.0, a));
		float w = (0.9 + 0.8 * ph) * unit;
		float s = body * exp(-pow(wpx / w, 2.0)) * on * smoothstep(0.0, 0.12, ph);
		col += mix(cyan, white, ph) * s * (0.5 + 0.9 * ph);
	}
	// ② 往枪口收的光圈（垂直于枪管 → 沿枪管方向压扁）
	vec2 d = vec2(cos(dir_ang), sin(dir_ang));
	float rq = length(vec2(dot(p, d) / 0.34, dot(p, vec2(-d.y, d.x))));
	for (int i = 0; i < 3; i++) {
		float ph = fract(t * 0.55 + float(i) / 3.0);
		float R = mix(330.0, 26.0, ph * ph) * unit;
		float wd = mix(1.3, 2.8, ph) * unit;
		float ring = exp(-pow((rq - R) / wd, 2.0)) + 0.22 * exp(-abs(rq - R) / (10.0 * unit));
		col += mix(cyan, white, ph) * ring * smoothstep(0.0, 0.2, ph) * (1.0 - smoothstep(0.9, 1.0, ph)) * k * 0.9 * (1.0 - 0.85 * fz);
	}
	// ③ 细电弧：每隔一小会儿换一批位置，沿半径方向抖
	for (int i = 0; i < 4; i++) {
		float fi = float(i);
		float tick = floor(t * 9.0 + fi * 0.37);
		float a0 = h(fi * 9.1 + tick * 3.7) * TAU - PI;
		float L = mix(70.0, 230.0, h(fi + tick)) * unit * (0.4 + 0.6 * k);
		float da = ang - a0; da = atan(sin(da), cos(da));
		float wob = (vn(r * 0.06 / unit + tick * 5.0 + fi * 13.0) - 0.5) * 38.0 * unit * smoothstep(0.0, 40.0 * unit, r)
			+ (vn(r * 0.25 / unit + tick) - 0.5) * 8.0 * unit;
		float dist = abs(da * r - wob);
		float gate = step(abs(da), 1.2) * (1.0 - smoothstep(L * 0.6, L, r)) * step(0.3, h(fi * 2.0 + tick * 1.3)) * k;
		col += (white * exp(-pow(dist / (1.1 * unit), 2.0)) * 1.1 + cyan * exp(-dist / (5.0 * unit)) * 0.25) * gate;
	}
	// 时停里的倒计时：一圈光从外面收向枪口，越收越快、越亮，开枪那一刻正好合上（世界停着，能量没停）
	float cr = 440.0 * (1.0 - close * close) * unit;
	float cring = exp(-pow((rq - cr) / ((1.6 + 1.6 * close) * unit), 2.0)) + 0.3 * exp(-abs(rq - cr) / (12.0 * unit));
	col += white * cring * fz * (0.8 + 1.2 * close);
	// ④ 枪口的核；时停时收成一个小点 + 一道很短的横向细光，还在不停地颤、越来越亮
	float flick = mix(0.85 + 0.15 * vn(t * 40.0), 0.8 + 0.2 * vn(rt * 55.0), fz) * (1.0 + 0.6 * close);
	float rc = mix((7.0 + 20.0 * k) * unit, 4.5 * unit, fz);
	float core = exp(-r / rc) * (0.6 + 1.6 * k) * flick * (1.0 + 2.0 * fz);
	float halo = exp(-r / (rc * 5.0)) * 0.35 * k * (1.0 - 0.7 * fz);
	float fl_len = (50.0 * k * k + (60.0 + 110.0 * close) * fz) * unit;
	float flare = exp(-abs(p.y) / ((1.0 + 1.6 * exp(-abs(p.x) / (40.0 * unit))) * unit)) * exp(-abs(p.x) / max(fl_len, 1.0));
	col *= 1.0 - 0.45 * fz;
	col += white * (core + flare * (0.5 * k + 1.2 * fz)) + cyan * halo;
	COLOR = vec4(col * edge, 1.0);
}" };

	/// <summary>
	/// 蓄力 → 时停的画面（声音在 Play 里）：ChargeTime 秒里光往枪口收、画面一点点暗下去；到头一下闪白，
	/// 然后 FreezeTime 秒全场静止：暗、无声，蓄力的细光和电弧原地悬住，只有枪口一个极亮的小点。开枪那一刻整套撤掉。
	/// </summary>
	private static Action Charge(Control layer, Func<Vector2> muzzleAt, Vector2 dir, Vector2 screen, float unit)
	{
		const float total = ChargeTime + FreezeTime;
		Node2D hushRoot = Fx.ScreenRoot(layer, "P5SinfulHush");
		ShaderMaterial hm = new() { Shader = HushShader };
		hm.SetShaderParameter("sres", screen);
		hm.SetShaderParameter("hole", 560f * unit);
		ColorRect hush = new() { Size = screen, Material = hm, MouseFilter = Control.MouseFilterEnum.Ignore };
		ColorRect white = new() { Size = screen, Color = new Color(0.94f, 0.97f, 1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		hushRoot.AddChild(hush);
		hushRoot.AddChild(white);
		Fx.FreeAfter(hushRoot, total + 0.2);                           // 正常由开枪那一刻撤掉（返回的委托）；这里只是兜底

		float size = 1600f * unit;
		Node2D root = Fx.Root(layer, muzzleAt(), "P5SinfulCharge", total + 0.2);
		ShaderMaterial m = new() { Shader = ChargeShader };
		m.SetShaderParameter("size_px", size);
		m.SetShaderParameter("unit", unit);
		Vector2 a = Fx.ScreenPoint(layer, muzzleAt());
		m.SetShaderParameter("dir_ang", (Fx.ScreenPoint(layer, muzzleAt() + dir * 100f) - a).Angle());
		ColorRect q = new() { Size = Vector2.One * size, Position = -Vector2.One * size / 2f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		float clock = 0f, last = 0f;
		Fx.Animate(root, 0, total, t =>
		{
			float time = t * total, dt = time - last;
			last = time;
			float k = Mathf.Clamp(time / ChargeTime, 0f, 1f);
			float z = time - ChargeTime;                                  // 时停开始后过了多久
			if (z < 0f)
			{
				clock += dt * (0.5f + 1.7f * k);                          // 越蓄越快
				root.GlobalPosition = muzzleAt();
			}
			else
			{
				clock += dt * 0.3f;                                      // 时停：没有完全停死，极慢地继续往里爬（一动不动会像卡住）
			}
			float close = Mathf.Clamp(z / FreezeTime, 0f, 1f);
			float fz = z < 0f ? 0f : 1f - Mathf.Pow(1f - Mathf.Clamp(z / 0.07f, 0f, 1f), 3f);
			m.SetShaderParameter("t", clock);
			m.SetShaderParameter("k", k);
			m.SetShaderParameter("fz", fz);
			m.SetShaderParameter("close", close);
			m.SetShaderParameter("rt", time);
			hm.SetShaderParameter("center", Fx.ScreenPoint(layer, root.GlobalPosition));
			hm.SetShaderParameter("alpha", z < 0f ? 0.5f * Mathf.Pow(k, 1.6f) : Mathf.Lerp(0.56f, 0.82f, close));   // 时停里一路暗下去，不是一下暗到底就不动了
			// 时停那一下闪白（君王之剑那种闪白，不是彩色的光）
			white.Color = new Color(0.94f, 0.97f, 1f, z < 0f ? 0f : 0.65f * Mathf.Pow(Mathf.Max(0f, 1f - z / 0.08f), 2f));
		});
		return () =>
		{
			if (GodotObject.IsInstanceValid(hushRoot)) hushRoot.QueueFree();
			if (GodotObject.IsInstanceValid(root)) root.QueueFree();
		};
	}


	/// <summary>大罪穿甲弹：一个沿弹道拉长的四边形，弹头以 BulletSpeed 飞出屏幕，尾巴跟着拉长，飞出去后尾巴还亮一下再散。</summary>
	private static void Bullet(Control layer, Vector2 muzzle, Vector2 dir, Vector2 screen, float unit)
	{
		float thick = 420f * unit;
		float reach = screen.Length() * 1.2f;
		float speed = BulletSpeed * unit;
		float fly = reach / speed, linger = 0.45f;
		Node2D root = Fx.Root(layer, muzzle, "P5SinfulBullet", fly + linger + 0.1);
		root.Rotation = dir.Angle();
		ShaderMaterial m = new() { Shader = BulletShader };
		ColorRect q = new() { Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		Fx.Clock(q);
		m.SetShaderParameter("thick_px", thick);
		m.SetShaderParameter("speed_px", speed);
		Fx.Animate(root, 0, fly + linger, t =>
		{
			float time = t * (fly + linger);
			float headDist = Mathf.Min(time, fly) * speed;
			float len = headDist + thick * 0.5f;
			q.Size = new Vector2(len, thick);
			q.Position = new Vector2(-thick * 0.1f, -thick / 2f);
			m.SetShaderParameter("len_px", len);
			m.SetShaderParameter("head_px", headDist + thick * 0.1f);
			m.SetShaderParameter("extra_age", Mathf.Max(0f, time - fly));
			m.SetShaderParameter("fade", time < fly ? 1f : Mathf.Max(0f, 1f - (time - fly) / linger));
		});
	}

	/// <summary>命中区：子弹打进敌人那一片的时候整片一起亮一下（一盏点光照亮敌人 + 短促的冷白闪光）。贯穿的火、烟、震退在 Pierce 里。</summary>
	private static void HitZone(Control layer, Vector2 zone, Vector2 dir, float floorY, float span, float unit)
	{
		float big = 1f + span / (420f * unit);
		PointLight2D pl = Light(layer, zone, new Color(0.65f, 0.85f, 1f), 1200f * unit * big);
		Fx.Animate(pl, 0, 0.7, k => pl.Energy = 0.8f * Mathf.Pow(1f - k, 2f));
		Fx.FreeAfter(pl, 0.75);
	}

	/// <summary>
	/// 贯穿一个敌人（第二版，用户 2026-11-19：打中要有冲击力）：第一下顿帧；身后顺着弹道炸出一大股蓝焰、留一团烟；
	/// 身体被打得往后一震（过曝在 Play 的 step 里）；最后一个更大、再沿弹道震一下。
	/// </summary>
	private static void Pierce(Control front, Control back, Vector2 at, Vector2 dir, float unit, bool first, bool last, Node2D? body, Node lifetime)
	{
		PointLight2D pl = Light(front, at, new Color(0.7f, 0.9f, 1f), 700f * unit);
		Fx.Animate(pl, 0, 0.4, k => pl.Energy = 1.1f * (1f - k) * (1f - k));   // 第四版：灯光减半（把敌人照成一片白，子弹混在里面看不出）
		Fx.FreeAfter(pl, 0.45);
		// 第四版：打中 = 一团顺着弹道拉长的火舌炸开（开场那种），不再是白圆片 + 白楔形
		FireBurstDir(front, at, dir, (last ? 620f : 480f) * unit, 0.3f, last ? 1.3f : 1.15f, new Vector2(1.5f, 0.85f));
		if (body != null)
		{
			KnockBack(body, dir * (last ? 90f : 72f) * unit, lifetime);
		}
		Glow.Sparks(front, at, 16, 900f, 2400f, 0.36f, new[] { new Color(0.95f, 0.98f, 1f), new Color(0.35f, 0.75f, 1f), new Color(0.65f, 0.3f, 1f) },
			300f, 0.06f, 0.16f, dir, 26f);
		if (last)
		{
			Stage.ShakeDir?.Invoke(4, Mathf.RadToDeg(dir.Angle()));
		}
		else if (!first)
		{
			Fx.Shake(4);
		}
	}
}
