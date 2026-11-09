using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace P5Joker;

/// <summary>
/// 华丽收场 = 撒旦耶尔的大罪穿甲弹（用户 2026-11-08，参考 art/p5/fx/v4/satanael/ref_*.png）。全部是着色器实时渲染，不画贴图：
/// ① 蓝焰从 Joker 脚下往外烧，烧到哪里战斗背景就被烧穿，露出夜空场景（NightScene：夜空、多层翻滚的乌云、云底红光、远处闪电、两边断崖、中间深渊雾）；
/// ② （撒旦耶尔从断崖下升起——等第 2 版立绘，这里先空着）；
/// ③ 举枪蓄力：枪口一团往里吸的能量漩涡（Vortex），场景压暗；
/// ④ 开枪：全屏白闪 + 顿帧 + 枪口一串冲击环（Rings）+ 大罪穿甲弹（Bullet：刺眼的弹芯 + 双螺旋气流 + 往后喷的等离子尾），
///    一路贯穿每个敌人（每穿过一个就在那里炸开、结算一次），从屏幕右边飞出去；
/// ⑤ 最后一个敌人被贯穿：P5 红黑白冲击帧；⑥ 场景从远处往回烧掉，露出原来的战场。
/// </summary>
public static class SinfulShell
{
	// ───────────────────────── 场景 ─────────────────────────

	private const string Noise = @"
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; }
	return v;
}
";

	private static Shader? _scene, _vortex, _bullet, _ring;

	/// <summary>夜空场景。part 0 = 天空 + 深渊（最底下），part 1 = 两边断崖（盖在人格面具前面、人物后面）。</summary>
	private static Shader Scene => _scene ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 sres = vec2(1920.0, 1080.0);
uniform vec2 origin = vec2(430.0, 830.0);
uniform float reveal = 0.0;
uniform int part = 0;
uniform float clock = 0.0;
uniform float dim = 0.0;
uniform float flash = 0.0;
uniform float feet_y = 830.0;
uniform float cliff_x = 640.0;
uniform float floor_y = 830.0;
uniform float cliff2_x = 1000.0;
uniform vec3 moon = vec3(1570.0, 170.0, 58.0);   // 月亮：屏幕位置 + 半径（像素）
// Codex 画的平台（有就用它，没有就用下面程序画的岩石）：贴图左上角在屏幕上的位置、在屏幕上的大小
uniform sampler2D plat : filter_linear;
uniform vec2 plat_origin = vec2(0.0);
uniform vec2 plat_size = vec2(0.0);
" + Noise + @"
// 蓝焰边：烧穿的边界（d = 到边界的有向距离，>0 在场景里）
vec4 flameEdge(vec2 px, float s) {
	float t = clock;
	float w = 26.0 + 22.0 * noise(vec2(px.x * 0.02, t * 2.0));
	float tongue = fbm(vec2(px.x * 0.018, px.y * 0.012 + t * 2.4));
	float e = exp(-max(-s, 0.0) / w) * step(-w * 3.0, s) * (1.0 - smoothstep(0.0, w * 0.8, s));
	e *= 0.6 + 0.9 * tongue;
	vec3 c = e > 1.0 ? vec3(0.94, 0.98, 1.0) : e > 0.62 ? vec3(0.4, 0.8, 1.0) : e > 0.3 ? vec3(0.1, 0.38, 0.92) : vec3(0.03, 0.09, 0.32);
	float a = smoothstep(0.12, 0.16, e);
	return vec4(c * 1.2, a);
}
float revealDist(vec2 px) {
	float n = (fbm(px / 110.0 + vec2(0.0, -clock * 0.5)) - 0.5) * 160.0;
	float d = length((px - origin) * vec2(1.0, 1.25)) + n;
	return reveal - d;                                     // 场景 = 以 Joker 脚下为圆心的一个圈：展开（reveal 变大）、收回（reveal 变小）
}
// 平台（用户 2026-11-08）：从屏幕右边伸出来的一整块，敌人和 Joker 都站在上面；到 Joker 脚下左边就是悬崖，崖面往下、往里收。<0 在岩石里
float rock(vec2 px) {
	float top = feet_y - 3.0 - 9.0 * fbm(vec2(px.x * 0.012, 3.1)) + (px.x - cliff_x) * (floor_y - feet_y) / max(sres.x - cliff_x, 1.0);
	float edge = cliff_x + (px.y - feet_y) * 0.28 + (fbm(vec2(px.y * 0.02, 7.7)) - 0.5) * 60.0
		+ smoothstep(feet_y + 40.0, sres.y, px.y) * 120.0 * fbm(vec2(px.y * 0.006, 1.3));
	return max(top - px.y, edge - px.x);
}
void fragment() {
	vec2 px = UV * sres;
	vec2 uv = UV;
	float s = revealDist(px);
	float shown = smoothstep(-2.0, 2.0, s);
	vec4 edge = flameEdge(px, s);
	vec3 col;
	float a;
	if (part == 0) {
		// 天空：深蓝 → 地平线一圈青蓝的亮
		col = mix(vec3(0.012, 0.025, 0.07), vec3(0.04, 0.12, 0.22), smoothstep(0.0, 0.62, uv.y));
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
		a = shown;
		col = mix(col, edge.rgb, edge.a);
		a = max(a, edge.a);
	} else {
		float d = rock(px);
		float inside = 1.0 - smoothstep(-1.0, 1.0, d);
		vec4 pt = vec4(0.0);
		if (plat_size.x > 0.0) {
			vec2 puv = (px - plat_origin) / plat_size;
			pt = (puv.x >= 0.0 && puv.x <= 1.0 && puv.y >= 0.0 && puv.y <= 1.0) ? texture(plat, puv) : vec4(0.0);
			inside = pt.a;
		}
		// 岩石：暗蓝黑，层理 + 裂纹；顶面一道被天光照亮的冷色边；崖面靠深渊那侧被底下的红雾映红
		float strata = fbm(vec2(px.x * 0.01, px.y * 0.05));
		float crack = smoothstep(0.02, 0.0, abs(fbm(px * 0.02) - 0.5));
		col = mix(vec3(0.02, 0.025, 0.045), vec3(0.06, 0.07, 0.1), strata) * (1.0 - 0.5 * crack);
		float topLit = smoothstep(14.0, 0.0, abs(px.y - (feet_y - 6.0))) * step(d, 0.0);
		col += vec3(0.25, 0.5, 0.75) * topLit * 0.5;
		float rim = smoothstep(-16.0, 0.0, d);
		col += vec3(0.45, 0.05, 0.08) * rim * smoothstep(feet_y, sres.y, px.y) * 1.2;
		col += vec3(0.3, 0.55, 0.8) * rim * smoothstep(feet_y + 60.0, feet_y, px.y) * 0.4;
		if (plat_size.x > 0.0) {
			col = pt.rgb * 0.8;                                             // 画的平台：整体压暗一点，跟夜景一致
		}
		col *= 1.0 - 0.45 * dim;
		col += vec3(0.85, 0.95, 1.0) * flash * 0.6;
		a = inside * shown;
		col = mix(col, edge.rgb, edge.a * inside);
	}
	COLOR = vec4(col, a);
}" };

	// ───────────────────────── 蓄力漩涡 ─────────────────────────

	private static Shader Vortex => _vortex ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float power = 0.0;
uniform float clock = 0.0;
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), ang = atan(p.y, p.x);
	// 几条往里旋进去的螺旋臂（越近中心越亮越细），外面一层往里吸的放射线
	float arms = pow(0.5 + 0.5 * sin(ang * 4.0 + log(r + 0.02) * 9.0 + clock * 26.0), 14.0);
	float streak = pow(noise(vec2(ang * 6.0, r * 3.0 + clock * 9.0)), 6.0) * smoothstep(1.0, 0.35, r);
	float body = arms * smoothstep(1.0, 0.15, r) * smoothstep(0.0, 0.12, r);
	float core = exp(-r * 9.0) * 1.6 + exp(-r * 3.0) * 0.4;
	float a = (body * 0.9 + streak * 0.9 + core) * power * smoothstep(1.0, 0.8, r);
	vec3 c = mix(vec3(0.15, 0.45, 1.0), vec3(0.85, 0.95, 1.0), clamp(core + arms * 0.4, 0.0, 1.0));
	c += vec3(0.5, 0.2, 0.9) * streak * 0.5;
	COLOR = vec4(c * a, 1.0);
}" };

	// ───────────────────────── 子弹 ─────────────────────────

	/// <summary>沿 x 从尾（0）到头（1）。head = 弹头在四边形里的位置（尾巴比弹头先出现在枪口那头）。</summary>
	private static Shader BulletShader => _bullet ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float clock = 0.0;
uniform float len_px = 1000.0;
uniform float thick_px = 120.0;
uniform float fade = 1.0;
uniform float tail = 1.0;
" + Noise + @"
void fragment() {
	float x = UV.x, y = (UV.y - 0.5) * 2.0;
	float xp = x * len_px;
	// 尾巴：越往后越细越淡；tail 控制尾巴长度（0～1）
	float taper = smoothstep(1.0 - tail, 1.0, x);
	float w = mix(0.08, 0.42, taper);
	// 弹芯：一条刺眼的细线，弹头是一颗拉长的亮核
	float core = exp(-pow(y / (w * 0.35), 2.0)) * taper;
	vec2 hp = vec2((x - 0.985) * len_px / thick_px * 1.4, y);
	float head = exp(-dot(hp, hp) * 3.0) * 2.2;
	// 双螺旋：两股气流绕着弹芯转（3D 螺旋投影成两条交织的正弦）
	float ph = xp / (thick_px * 0.9) - clock * 34.0;
	float s1 = abs(y - w * 0.85 * sin(ph)), s2 = abs(y - w * 0.85 * sin(ph + 3.14159));
	float front1 = 0.6 + 0.4 * cos(ph), front2 = 0.6 + 0.4 * cos(ph + 3.14159);
	float helix = (exp(-s1 / 0.035) * front1 + exp(-s2 / 0.035) * front2) * taper;
	// 往后喷的等离子：拉长的噪声，越往尾越散
	float plasma = fbm(vec2(xp * 0.012 - clock * 22.0, y * 3.0)) * exp(-pow(y / (w * 1.3), 2.0)) * taper;
	plasma = smoothstep(0.35, 0.9, plasma);
	float glow = exp(-abs(y) / (w * 0.9)) * taper * 0.35;
	vec3 white = vec3(0.95, 0.98, 1.0), cyan = vec3(0.25, 0.75, 1.0), violet = vec3(0.65, 0.3, 1.0);
	vec3 col = white * (core * 1.6 + head) + cyan * (helix * 1.3 + glow) + mix(cyan, violet, 0.5 + 0.5 * y) * plasma * 1.1;
	// 四边形边缘一律收到 0（不然弹头光晕被切成方块）
	float edge = (1.0 - smoothstep(0.75, 1.0, abs(y))) * smoothstep(0.0, 0.04, x) * (1.0 - smoothstep(0.985, 1.0, x));
	COLOR = vec4(col * fade * edge, 1.0);
}" };

	private static Shader RingShader => _ring ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(0.4, 0.8, 1.0, 1.0);
uniform float radius = 0.1;
uniform float width = 0.05;
uniform float alpha = 1.0;
void fragment() {
	float d = length(UV * 2.0 - 1.0);
	float core = 1.0 - smoothstep(0.0, width, abs(d - radius));
	float glow = (1.0 - smoothstep(0.0, width * 4.0, abs(d - radius))) * 0.4;
	COLOR = vec4(tint.rgb * (1.0 + core) * (core + glow) * alpha * (1.0 - smoothstep(0.85, 1.0, d)), 1.0);
}" };

	// ───────────────────────── 时序 ─────────────────────────

	/// <summary>各拍的时刻（从出牌算起，秒）。</summary>
	public const float RevealAt = 0.05f, RevealTime = 0.7f, AimAt = 0.45f, BulletSpeed = 5200f;

	/// <summary>
	/// 退场（用户 2026-11-09：收枪结束时场景起码已经开始收回 20% 左右）：枪落回手里（撒旦耶尔开枪 FireAt+0.12 之后 PutAwayAfter + 转两圈 + 一顿）那一刻劈退场的雷；
	/// 场景圈提前 ConcealAfter 开始收、用 ExitTime 收完：半径从画面最远的角开始按 smoothstep 收，收枪结束时（走了 40% 的时间）
	/// 半径收了三成多、画面上明显在收（用户说「起码两成」）；撒旦耶尔从上往下烧（雷后 ~0.58s）的同时圈从外往脚下收，两个一起走完。
	/// </summary>
	public const float ExitAfter = 0.12f + SatanaelPuppet.PutAwayAfter + 0.02f + SatanaelPuppet.SpinTime + 0.04f, ExitTime = 1.0f,
		ConcealAfter = ExitAfter - 0.4f * ExitTime;

	/// <summary>开枪（用户 2026-11-08：不蓄力，转完枪停一小下就开）：撒旦耶尔接住枪后 0.2s。</summary>
	public const float FireAt = AimAt + SatanaelPuppet.CatchAfter + 0.2f;

	/// <summary>接住枪的时刻。</summary>
	public const float CatchAt = AimAt + SatanaelPuppet.CatchAfter;

	/// <summary>Joker 开始转枪的时刻（从出牌、也就是 cast_sinful_from_mask 开始算）：攥面具 .02+.14、放手 .04+.05、插兜 .06、抬手 .05 之后（tools/p5/build_rig_p5.py）。</summary>

	/// <summary>召唤撒旦耶尔的雷劈下的时刻（用户 2026-11-08：场景还没烧完雷就劈下来，前半段不空）。</summary>
	public const float AppearAt = 0.12f;   // 交接待办 C（用户 2026-11-09：前半段空）：召唤的雷和场景刚烧开同时劈下

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
		PointLight2D l = new() { Texture = LightTex, TextureScale = radiusPx / 128f, Color = c, Energy = 0f };
		parent.AddChild(l);
		l.GlobalPosition = at;
		return l;
	}

	/// <summary>
	/// 预热（用户 2026-11-09：东西加多了，第一次打出肯定会卡）：Warmup.Run 每场战斗开始时在几乎透明的层里调一次。
	/// 着色器本身 Warmup 已经按反射全编译过；这里补上它照顾不到的：撒旦耶尔的大贴图第一次画时才上传显卡（整套部件建一个很小的、摆一帧）、
	/// 场景着色器带平台贴图 + 被引擎 2D 灯照着画一次、余烬 / 火花粒子的处理材质、命中区那一套（冲击波 + 粒子 + 灯），顺带把这些 C# 方法 JIT 掉。不出声。
	/// </summary>
	public static void Warm(Control hidden, Vector2 c)
	{
		Node2D slot = new() { Name = "SinfulWarm" };
		hidden.AddChild(slot);
		if (SatanaelPuppet.Available)
		{
			SatanaelPuppet pp = new(slot, c, 0.05f, c + new Vector2(100f, 100f));
			pp.Update(1f, 0f, 0.5f, 1.5f, 9f);
			pp.SceneMask(c, 1e5f);
			pp.LightFrom(c + new Vector2(100f, -100f));
		}
		ShaderMaterial m = new() { Shader = Scene };
		m.SetShaderParameter("reveal", 1e5f);
		if (Fx.Tex("sinful_platform") is Texture2D plat)
		{
			m.SetShaderParameter("plat", plat);
			m.SetShaderParameter("plat_size", new Vector2(8f, 8f));
		}
		ColorRect r = new() { Size = new Vector2(8f, 8f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		slot.AddChild(r);
		r.GlobalPosition = c;
		PointLight2D l = Light(slot, c, Colors.White, 64f);
		l.Energy = 0.05f;
		GpuParticles2D embers = ReadyFx.Embers(slot, c, new[] { Colors.White, Colors.White }, 4, 8f, 0.3f, 10f, 20f, 5f);
		embers.Material = MaskMaterial(Fx.Screen(hidden), c);
		HitZone(hidden, c, Vector2.Right, c.Y, 0f, 0.02f);
	}

	/// <summary>放整段大罪穿甲弹。back = 背景层（场景、人格面具），front = 前景层（子弹、命中）。返回子弹穿过每个敌人的时刻（秒，从现在算）。</summary>
	/// <summary>打中的那一声（用户 2026-11-09 对比后选定 sinful_pierce_last）。</summary>
	public static string HitSound = Sfx.SinfulPierceLast;

	public static float[] Play(Control back, Control front, Setup s)
	{
		Vector2 screen = Fx.Screen(back);
		float unit = screen.Y / 1080f;
		Func<Vector2, Vector2> toScreen = w => Fx.ScreenPoint(back, w);
		Vector2 feetS = toScreen(s.JokerFeet), floorS = toScreen(new Vector2(0f, s.FloorY));
		var enemiesOrdered = s.Enemies.Select((e, i) => (e, i)).OrderBy(t => t.e.DistanceSquaredTo(s.PersonaMuzzle)).ToList();
		float firstEnemyX = enemiesOrdered.Count > 0 ? enemiesOrdered.Min(t => toScreen(t.e).X) : screen.X * 0.6f;

		// ── 场景：天空层（最底下）+ 断崖层（盖在人格面具前面）
		Node2D root = Fx.ScreenRoot(back, "P5SinfulShellScene");
		back.MoveChild(root, 0);
		ShaderMaterial sky = new() { Shader = Scene }, rocks = new() { Shader = Scene };
		float total = FireAt + Mathf.Max(ConcealAfter + ExitTime, ExitAfter + 0.6f) + 0.15f;
		foreach (ShaderMaterial m in new[] { sky, rocks })
		{
			m.SetShaderParameter("sres", screen);
			m.SetShaderParameter("origin", feetS);
			m.SetShaderParameter("feet_y", feetS.Y);
			m.SetShaderParameter("floor_y", floorS.Y);
			m.SetShaderParameter("cliff_x", feetS.X - 95f * unit);           // Joker 站在悬崖边上
			m.SetShaderParameter("cliff2_x", Mathf.Max(feetS.X + 420f * unit, firstEnemyX - 160f * unit));
		}
		sky.SetShaderParameter("part", 0);
		Vector2 moonS = new(screen.X * 0.82f, screen.Y * 0.16f);              // 右上的空天：敌人头上、撒旦耶尔翅膀够不到的地方
		sky.SetShaderParameter("moon", new Vector3(moonS.X, moonS.Y, 58f * unit));
		rocks.SetShaderParameter("part", 1);
		if (Fx.Tex("sinful_platform") is Texture2D plat)
		{
			// Codex 的平台：原图 3840×1100（×0.52），顶面 y=140、悬崖边 x=760 → 悬崖边对到 Joker 脚边、顶面对到脚底
			float k = unit;
			Vector2 size = plat.GetSize() * k;
			Vector2 org = new(feetS.X - 95f * unit - 760f * 0.52f * k, feetS.Y - 140f * 0.52f * k);
			rocks.SetShaderParameter("plat", plat);
			rocks.SetShaderParameter("plat_origin", org);
			rocks.SetShaderParameter("plat_size", size);
		}
		ColorRect skyRect = new() { Size = screen, Material = sky, MouseFilter = Control.MouseFilterEnum.Ignore, LightMask = 0 };   // 天空不吃引擎灯光（不然整片被照白），只有自己的闪光
		Node2D personaSlot = new() { Name = "PersonaSlot" };          // 撒旦耶尔第 2 版立绘放这里（天空和断崖之间）
		ColorRect rockRect = new() { Size = screen, Material = rocks, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(skyRect);
		root.AddChild(personaSlot);
		root.AddChild(rockRect);
		Fx.Clock(skyRect);
		Fx.Clock(rockRect);
		// ── 撒旦耶尔（部件动画）：先摆到开枪那一刻量出枪口，再回到登场姿势
		const float RiseAt = AppearAt;
		float exitAt = FireAt + ExitAfter;
		Vector2 aimScreen = s.Enemies.Count > 0 ? toScreen(s.Enemies.Aggregate(Vector2.Zero, (a, b) => a + b) / s.Enemies.Count) : screen * 0.7f;
		SatanaelPuppet? puppet = SatanaelPuppet.Available ? new SatanaelPuppet(personaSlot, s.PersonaOrigin * unit, s.PersonaScale * unit, aimScreen) : null;
		if (puppet != null)
		{
			puppet.Update(FireAt - 0.0005f, RiseAt, AimAt, FireAt, exitAt);
			s.PersonaMuzzle = puppet.MuzzleGlobal;
			puppet.Update(0f, RiseAt, AimAt, FireAt, exitAt);
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
			float rv = Mathf.Clamp((time - RevealAt) / RevealTime, 0f, 1f);
			float ex = Mathf.Clamp((time - FireAt - ConcealAfter) / ExitTime, 0f, 1f);       // 收枪快结束时场景开始收回（和撒旦耶尔退场重叠）
			// 用户 2026-11-09：切入和消失都以 Joker 脚下为圆心——展开时圈往外烧，收回时圈原路缩回脚下（和展开镜像：先快后慢）；
			// 云海、余烬这些画在场景上的东西都用同一个圈遮住，一起收回
			// 收到最后半径压到 -160：蓝焰边整圈烧没，不在脚下留一摊火
			float reveal = ex <= 0f ? far * (1f - Mathf.Pow(1f - rv, 2.2f))
				: visR * (1f - ex * ex * (3f - 2f * ex)) - 160f * unit * ex;   // 收回：从画面最远的角开始（马上看得出在收），先慢后快再慢，合到脚下
			emberMask.SetShaderParameter("reveal", reveal);
			const float dim = 0f;
			float flash = time >= FireAt ? Mathf.Exp(-(time - FireAt) / 0.05f) : 0f;
			// 不蓄力：接住枪时枪口一闪，开枪时一闪
			float charge = time < CatchAt ? 0f : time < FireAt ? 0.9f * Mathf.Exp(-(time - CatchAt) / 0.08f) : 1.7f * Mathf.Exp(-(time - FireAt) / 0.12f);
			float thunder = 0f;
			if (puppet != null)
			{
				puppet.Update(time, RiseAt, AimAt, FireAt + 0.12f, exitAt, puppet.MuzzleLocal, charge, flash * 0.6f, dim);
				puppet.SceneMask(feetS, reveal);
				thunder = puppet.Lightning;
				muzzleLight.GlobalPosition = puppet.MuzzleGlobal;
			}
			foreach (ShaderMaterial m in new[] { sky, rocks })
			{
				m.SetShaderParameter("reveal", reveal);
				m.SetShaderParameter("dim", dim);
				m.SetShaderParameter("flash", flash * 0.5f + thunder * 0.12f);
			}
			// 灯：枪口蓄力光、开枪全场白光、跟着子弹飞的光、雷光、深渊往上打的暗红光
			float scene = rv * (1f - ex);
			muzzleLight.Energy = charge * 1.1f;
			flashLight.Energy = 1.5f * flash;
			float fly = time - FireAt;
			bulletLight.Energy = fly >= 0f && fly < 0.6f ? 2.6f * (1f - fly / 0.6f) : 0f;
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
				if (GodotObject.IsInstanceValid(a)) a.Modulate = c * tint;
			}
		};
		Fx.Animate(root, 0, total, step);
		Fx.FreeAfter(root, total + 0.1);
		// 开场：Joker 面具上的蓝焰一炸（烧穿从这里开始）
		Glow.Flash(front, s.JokerHead, new Color(0.4f, 0.8f, 1f), 260f * unit, 0.3f);
		Glow.Sparks(front, s.JokerFeet, 40, 200f, 700f, 0.7f, new[] { new Color(0.9f, 0.97f, 1f), new Color(0.3f, 0.7f, 1f), new Color(0.1f, 0.3f, 0.9f) },
			-200f, 0.06f, 0.2f, Vector2.Up, 70f);
		// 召唤 / 退场的闪雷声；接住枪的金属声
		// 场景圈展开 / 收回的轻声（压在雷和开枪声下面）：收回那条 0.7s 的卷入正好在圈缩到脚下时收住
		Fx.After(front, RevealAt, () => Sfx.Play(Sfx.SinfulSweepOpen, 0.95f));
		Fx.After(front, FireAt + ConcealAfter + ExitTime - 0.7f, () => Sfx.Play(Sfx.SinfulSweepClose, 0.85f));
		Fx.After(front, AppearAt, () => { Sfx.Play(Sfx.SinfulThunder, 1.0f); Fx.Shake(3); });
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

		// ── 开枪
		Fx.After(front, FireAt - 0.18f, () => s.OnJokerShoot?.Invoke());
		// 用户 2026-11-09：不做时停；震撼感一半交给声音。开枪声整条重做：倒吸 → 一瞬静音 → 爆裂 + 低频下坠 → 云层滚回的雷声 → 耳鸣（冲击在文件 0.30s）
		Fx.After(front, FireAt - 0.30f, () => Sfx.Play(Sfx.SinfulShot, 1.3f));
		// 开枪前「吸一口气」：画面压暗、游戏其他声音压下去，开枪那一下一起放开
		Fx.After(front, FireAt - 0.26f, () => Inhale(front, screen, 0.26f));
		// Joker 同步开枪，但大罪穿甲弹是撒旦耶尔打出来的：Joker 枪口只有一小点蓝光，不抢戏（用户 2026-11-08）
		Fx.After(front, FireAt, () => Glow.Flash(front, s.JokerMuzzle, new Color(0.5f, 0.85f, 1f), 70f * unit, 0.1f));
		var pierce = new float[s.Enemies.Count];
		var orderIndex = enemiesOrdered.Select(t => t.i).ToList();
		var travel = new float[enemiesOrdered.Count];
		for (int k = 0; k < enemiesOrdered.Count; k++)
		{
			travel[k] = Mathf.Max(0.03f, (enemiesOrdered[k].e - muzzle).Dot(dir) / (BulletSpeed * unit));
			pierce[orderIndex[k]] = FireAt + travel[k];
		}
		var stopHits = new List<Vector2>();
		for (int k = 0; k < enemiesOrdered.Count; k++)
		{
			Vector2 e = enemiesOrdered[k].e;
			stopHits.Add((muzzle + dir * (e - muzzle).Dot(dir)).Lerp(e, 0.6f));
		}
		Fx.After(front, FireAt, () =>
		{
			Fx.Shake(7);
			ImpactFrames(front, muzzle, dir, screen, unit);                // 两帧黑白冲击帧（动画打击帧，不停顿）
			Glow.Flash(front, muzzle, new Color(0.85f, 0.95f, 1f), 380f * unit, 0.16f);
			puppet?.Blast();                                               // 远处云海被枪口冲击波压开、翻卷
			Rings(front, muzzle, dir, unit * 0.45f);                       // 远处的枪口：冲击环小
			Bullet(front, muzzle, dir, screen, unit);
			BulletDetails(front, muzzle, dir, screen, unit);              // 弹道上的小光点、几道发丝一样的气流线
			Glow.Sparks(front, muzzle, 50, 600f, 1800f, 0.4f, new[] { new Color(0.95f, 0.98f, 1f), new Color(0.35f, 0.75f, 1f), new Color(0.6f, 0.3f, 1f) }, 0f,
				0.05f, 0.16f, dir, 22f);
		});
		for (int k = 0; k < enemiesOrdered.Count; k++)
		{
			int kk = k, idx = orderIndex[k];
			Vector2 e = enemiesOrdered[k].e;
			// 弹道上离这个敌人最近的那一点（子弹从这里穿过去）
			Vector2 hit = muzzle + dir * (e - muzzle).Dot(dir);
			hit = hit.Lerp(e, 0.6f);
			bool last = k == enemiesOrdered.Count - 1;
			Fx.After(front, FireAt + travel[k], () =>
			{
				Pierce(front, hit, dir, unit, last);
				s.OnPierce?.Invoke(idx);
			});
		}
		// 打到敌人那边（用户 2026-11-09）：不是每个敌人各一套，而是整片命中区一起——几圈冲击波沿弹道往外扩、地面一圈波纹贴着平台扫开、尘土和碎光
		if (stopHits.Count > 0)
		{
			Vector2 zone = stopHits.Aggregate(Vector2.Zero, (a, b) => a + b) / stopHits.Count;
			float span = stopHits.Count > 1 ? stopHits.Max(h => h.DistanceTo(zone)) : 0f;
			Fx.After(front, FireAt + travel[0], () => { HitZone(front, zone, dir, s.FloorY, span, unit); HitDetails(front, zone, dir, span, unit); });
		}
		// 打中：一发子弹，只响一声（用户 2026-11-09：不是打中几个敌人就放几次），落在子弹碰到第一个敌人那一刻。
		// 飞过镜头的呼啸去掉了（和开枪的冲击叠在一起）
		if (travel.Length > 0)
		{
			Fx.After(front, FireAt + travel[0], () => Sfx.Play(HitSound, 1.3f));
		}
		// 余韵：弹道在夜空里留下一道细光痕，慢慢散掉
		Fx.After(front, FireAt + 0.25f, () => Afterglow(front, muzzle, dir, screen, unit));
		return pierce;
	}

	// ───────────────────────── 开枪的冲击 ─────────────────────────

	/// <summary>开枪前「吸一口气」：画面慢慢压暗（深蓝黑，半透明），游戏音乐 / 环境声压低；时间到瞬间撤掉（开枪那一帧）。</summary>
	private static void Inhale(Control layer, Vector2 screen, float sec)
	{
		Node2D root = Fx.ScreenRoot(layer, "P5SinfulInhale");
		ColorRect dark = new() { Size = screen, Color = new Color(0.01f, 0.02f, 0.05f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(dark);
		Fx.Animate(dark, 0, sec, t => dark.Color = new Color(0.01f, 0.02f, 0.05f, 0.42f * t * t));
		Fx.FreeAfter(root, sec);
		Sfx.Duck(-16f, sec, 0.9f);
	}

	private static Shader? _impact;

	/// <summary>
	/// 冲击帧着色器（用户 2026-11-09：多边形拼的放射线太粗糙）：以枪口为中心的集中线——极坐标下按角度分 160 格，
	/// 每格随机决定有没有线、多粗、从多远开始；线从内到外由细变粗、两侧抗锯齿；沿弹道方向一道锥形的光（越远越宽、边缘有细碎抖动），
	/// 枪口一团白热。invert=0：黑底白线；invert=1：白底黑线。
	/// </summary>
	private static Shader ImpactShader => _impact ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 sres = vec2(1920.0, 1080.0);
uniform vec2 center = vec2(900.0, 400.0);
uniform float dir_ang = 0.4;
uniform float invert = 0.0;
uniform float seed = 0.0;
uniform float unit = 1.0;
uniform float bg_alpha = 0.9;
float h(float n) { return fract(sin(n * 127.1 + seed * 31.7) * 43758.5453); }
void fragment() {
	vec2 p = UV * sres - center;
	float r = length(p), ang = atan(p.y, p.x);
	// 集中线
	const float N = 160.0;
	float k = (ang + 3.14159265) / 6.2831853 * N, cell = floor(k), f = k - cell - 0.5;
	float on = step(0.52, h(cell));
	float r0 = mix(90.0, 380.0, h(cell + 7.0)) * unit;
	float w = mix(0.06, 0.34, h(cell + 13.0)) * smoothstep(r0, r0 + 500.0 * unit, r);
	float px = abs(f) * (6.2831853 / N) * r;                         // 到线中心的像素距离
	float halfw = w * (6.2831853 / N) * r * 0.5;
	float line = on * (1.0 - smoothstep(halfw - 0.8, halfw + 0.8, px)) * step(r0, r);
	// 弹道光锥：沿 dir 方向，越远越宽，边缘按距离抖动
	vec2 d = vec2(cos(dir_ang), sin(dir_ang));
	float along = dot(p, d), side = abs(dot(p, vec2(-d.y, d.x)));
	float u0 = along / (30.0 * unit), i0 = floor(u0), fr = u0 - i0;
	float jag = (mix(h(i0), h(i0 + 1.0), fr * fr * (3.0 - 2.0 * fr)) - 0.5) * 7.0 * unit + (h(floor(along / (6.0 * unit)) + 50.0) - 0.5) * 1.5 * unit;
	float cone = (8.0 + along * 0.12) * unit + jag;
	float beam = step(0.0, along) * (1.0 - smoothstep(cone - 1.0, cone + 1.0, side));
	float core = step(0.0, along) * (1.0 - smoothstep(cone * 0.35 - 1.0, cone * 0.35 + 1.0, side));
	// 枪口白热：小而亮的核 + 八道细星芒（不是一整块圆盘）
	float star = pow(abs(cos(ang * 4.0 + seed)), 40.0) * exp(-r / (160.0 * unit));
	float hot = exp(-r / (26.0 * unit)) * 1.2 + star * 0.9;
	float fg = clamp(max(max(line, beam), hot * 1.4), 0.0, 1.0);
	vec3 ink = vec3(0.02, 0.02, 0.035), paper = vec3(0.97, 0.98, 1.0);
	vec3 bgc = mix(ink, paper, invert), fgc = mix(paper, ink, invert);
	// 正片里光锥中心再亮一点偏青
	fgc = mix(fgc, vec3(0.85, 0.95, 1.0), core * (1.0 - invert) * 0.6);
	vec3 col = mix(bgc, fgc, fg);
	COLOR = vec4(col, mix(bg_alpha, 1.0, fg));
}" };

	/// <summary>
	/// 冲击帧（动画里的打击帧）：开枪那一下连续两帧——黑底白集中线 + 白色弹道光锥，再一帧反相（白底黑线）——然后恢复。
	/// 只有 2 帧（约 0.07s），不停顿；不读屏幕（游戏里读不到画面）。
	/// </summary>
	private static void ImpactFrames(Control layer, Vector2 muzzle, Vector2 dir, Vector2 screen, float unit)
	{
		Node2D root = Fx.ScreenRoot(layer, "P5SinfulImpactFrame");
		Vector2 a = Fx.ScreenPoint(layer, muzzle);
		Vector2 sdir = (Fx.ScreenPoint(layer, muzzle + dir * 100f) - a).Normalized();
		float seed = Fx.Rand(0f, 100f);
		ColorRect frame(float invert, float bg)
		{
			ShaderMaterial m = new() { Shader = ImpactShader };
			m.SetShaderParameter("sres", screen);
			m.SetShaderParameter("center", a);
			m.SetShaderParameter("dir_ang", sdir.Angle());
			m.SetShaderParameter("invert", invert);
			m.SetShaderParameter("seed", seed);
			m.SetShaderParameter("unit", unit);
			m.SetShaderParameter("bg_alpha", bg);
			ColorRect q = new() { Size = screen, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(q);
			return q;
		}
		ColorRect f1 = frame(0f, 0.9f), f2 = frame(1f, 0.82f);
		f2.Visible = false;
		Fx.After(root, 0.03, () => { f1.Visible = false; f2.Visible = true; });
		Fx.After(root, 0.064, () => root.QueueFree());
	}

	private static Shader? _after;

	/// <summary>
	/// 余韵光痕（用户 2026-11-09：之前是白条垫一层淡蓝条，两头被截平，像贴图）：
	/// 弹芯是一根两头收尖的细光（宽度沿长度起伏），外面两股还在慢慢拧的螺旋残丝，边上飘着往外散的火星；
	/// 散去时不是整体变淡，而是从枪口那端开始被噪声「烧断」，断口边缘先亮一下再灭。decay 0→1。
	/// </summary>
	private static Shader AfterShader => _after ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float clock = 0.0;
uniform float len_px = 2000.0;
uniform float thick_px = 120.0;
uniform float decay = 0.0;
" + Noise + @"
float h1(float n) { return fract(sin(n * 91.7) * 43758.5453); }
void fragment() {
	float x = UV.x, y = (UV.y - 0.5) * 2.0, xp = x * len_px;
	// 两头收尖：枪口端 6% 内、末端 12% 内收成尖
	float ends = smoothstep(0.0, 0.06, x) * (1.0 - smoothstep(0.88, 1.0, x));
	float wob = 0.75 + 0.25 * fbm(vec2(xp * 0.006, clock * 0.4));
	float w = 0.07 * ends * wob;
	// 烧蚀：阈值从枪口端往前推，噪声决定断口形状；断口附近一圈亮边
	float n = fbm(vec2(xp * 0.01, y * 2.0 + clock * 0.3));
	float front = decay * 1.35 - 0.2;
	float m = x * 0.8 + n * 0.45 - front;
	float alive = smoothstep(0.0, 0.05, m);
	float burn = exp(-abs(m) / 0.025) * step(-0.02, m) * (1.0 - decay * 0.5);
	float core = exp(-pow(y / max(w * 0.45, 0.004), 2.0)) * ends;
	float glow = exp(-abs(y) / max(w * 2.4, 0.01)) * ends * 0.35;
	// 螺旋残丝：越来越松、慢慢拧
	float ph = xp / (thick_px * 1.6) - clock * 3.0;
	float amp = (0.18 + 0.25 * decay) * ends;
	float s1 = abs(y - amp * sin(ph)), s2 = abs(y - amp * sin(ph + 3.14159));
	float strands = (exp(-s1 / 0.02) * (0.5 + 0.5 * cos(ph)) + exp(-s2 / 0.02) * (0.5 + 0.5 * cos(ph + 3.14159))) * 0.55;
	// 火星：沿长度分格，每格一粒，往两侧飘
	float cell = floor(xp / 22.0), fx = fract(xp / 22.0) - 0.5;
	float drift = (h1(cell) - 0.5) * 2.0 * (0.15 + decay * 0.7);
	float mote = step(0.72, h1(cell + 3.0)) * exp(-(fx * fx * 60.0 + pow((y - drift) * 9.0, 2.0)));
	vec3 white = vec3(0.95, 0.98, 1.0), cyan = vec3(0.3, 0.75, 1.0), violet = vec3(0.6, 0.35, 1.0);
	vec3 col = white * core * 1.3 + cyan * (glow + strands * (1.0 - decay * 0.6)) + mix(cyan, violet, h1(cell + 9.0)) * mote * 0.9;
	col *= alive;
	col += vec3(0.7, 0.92, 1.0) * burn * (core * 2.0 + glow * 2.0 + strands);
	float edge = 1.0 - smoothstep(0.8, 1.0, abs(y));
	COLOR = vec4(col * edge * (1.0 - smoothstep(0.85, 1.0, decay)), 1.0);
}" };

	/// <summary>余韵：弹道留下的光痕（AfterShader），1.4s 里从枪口那端开始烧断、散掉。</summary>
	private static void Afterglow(Control layer, Vector2 muzzle, Vector2 dir, Vector2 screen, float unit)
	{
		const float life = 1.4f;
		Node2D root = Fx.Root(layer, muzzle, "P5SinfulAfterglow", life + 0.1);
		root.Rotation = dir.Angle();
		float far = (screen * 1.3f).Length(), thick = 160f * unit;
		ShaderMaterial m = new() { Shader = AfterShader };
		m.SetShaderParameter("len_px", far);
		m.SetShaderParameter("thick_px", thick);
		ColorRect q = new() { Size = new Vector2(far, thick), Position = new Vector2(0f, -thick / 2f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		Fx.Clock(q);
		Fx.Animate(root, 0, life, t => m.SetShaderParameter("decay", t));
	}

	/// <summary>枪口外一串冲击环：沿枪管方向往前推，压扁成椭圆（垂直于枪管），一个接一个。</summary>
	private static void Rings(Control layer, Vector2 at, Vector2 dir, float unit)
	{
		Node2D root = Fx.Root(layer, at, "P5SinfulRings", 1.0);
		root.Rotation = dir.Angle();
		for (int i = 0; i < 5; i++)
		{
			float size = (200f + 70f * i) * unit;
			ShaderMaterial m = new() { Shader = RingShader };
			m.SetShaderParameter("tint", i % 2 == 0 ? new Color(0.5f, 0.85f, 1f) : new Color(0.75f, 0.55f, 1f));
			Node2D holder = new() { Scale = new Vector2(0.28f, 1f) };
			root.AddChild(holder);
			ColorRect q = new() { Size = Vector2.One * size, Position = -Vector2.One * size / 2f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			holder.AddChild(q);
			float delay = 0.025f * i, travel = (60f + 90f * i) * unit;
			Fx.Animate(holder, 0, 0.55f, t =>
			{
				float time = t * 0.55f - delay;
				if (time < 0f)
				{
					m.SetShaderParameter("alpha", 0f);
					return;
				}
				float k = Mathf.Clamp(time / 0.45f, 0f, 1f), e = 1f - Mathf.Pow(1f - k, 3f);
				holder.Position = new Vector2(travel * e, 0f);
				m.SetShaderParameter("radius", Mathf.Lerp(0.15f, 0.92f, e));
				m.SetShaderParameter("width", Mathf.Lerp(0.06f, 0.012f, e));
				m.SetShaderParameter("alpha", 1f - k * k);
			});
		}
	}

	/// <summary>大罪穿甲弹：一个沿弹道拉长的四边形，弹头以 BulletSpeed 飞出屏幕，尾巴跟着拉长，飞出去后尾巴还亮一下再散。</summary>
	private static void Bullet(Control layer, Vector2 muzzle, Vector2 dir, Vector2 screen, float unit)
	{
		float thick = 230f * unit;
		float reach = screen.Length() * 1.2f;
		float fly = reach / (BulletSpeed * unit), linger = 0.5f;
		Node2D root = Fx.Root(layer, muzzle, "P5SinfulBullet", fly + linger + 0.1);
		root.Rotation = dir.Angle();
		ShaderMaterial m = new() { Shader = BulletShader };
		ColorRect q = new() { Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		Fx.Clock(q);
		Fx.Animate(root, 0, fly + linger, t =>
		{
			float time = t * (fly + linger);
			float headDist = Mathf.Min(time, fly) * BulletSpeed * unit;
			float len = Mathf.Max(headDist, thick);
			q.Size = new Vector2(len + thick * 0.8f, thick);
			q.Position = new Vector2(-thick * 0.1f, -thick / 2f);
			m.SetShaderParameter("len_px", len + thick * 0.8f);
			// 从远处射过来（用户 2026-11-08）：弹头越飞越近、越来越粗（透视放大）
			float near = Mathf.Clamp(headDist / (screen.Length() * 0.55f), 0f, 1f);
			float th = thick * Mathf.Lerp(0.18f, 1.25f, near * near);
			q.Size = new Vector2(len + th * 0.8f, th);
			q.Position = new Vector2(-th * 0.1f, -th / 2f);
			m.SetShaderParameter("thick_px", th);
			m.SetShaderParameter("thick_px", thick);
			m.SetShaderParameter("tail", Mathf.Clamp(0.15f + time * 2.5f, 0f, 1f));
			m.SetShaderParameter("fade", time < fly ? 1f : Mathf.Max(0f, 1f - (time - fly) / linger));
		});
	}

	private static Shader? _shock;

	/// <summary>
	/// 命中区的冲击波（用户 2026-11-09：打到敌人那边要有命中感，波纹往外散，不要做得太简单）。一张四边形上画一圈：
	/// 前沿是一道刺亮的细边（按角度用噪声断开成几段弧、粗细起伏，外侧一层紫色色散、内侧一层青色），
	/// 紧跟着一条压暗的「折射带」（像空气被挤开），再往里是几道越来越淡的余波；前沿附近有细碎的放射短线往外甩；
	/// 起手中心一团白热。prog 0→1：半径 easeOutExpo 冲出去，越扩越细越淡。premul 混合：亮的部分是加法，暗带是真的压暗。
	/// </summary>
	private static Shader ShockShader => _shock ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float prog = 0.0;
uniform float seed = 0.0;
uniform float power = 1.0;
uniform float dark = 1.0;
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), ang = atan(p.y, p.x);
	float e = 1.0 - pow(2.0, -9.0 * prog);
	float R = 0.06 + 0.88 * e;
	float fade = pow(1.0 - prog, 1.4);
	// 前沿：断成几段弧、粗细起伏
	float a = ang / 6.2831853 + 0.5;
	float seg = smoothstep(0.28, 0.42, noise(vec2(a * 7.0 + seed, seed * 3.1)));
	float thick = (0.010 + 0.022 * (1.0 - e)) * (0.55 + 0.9 * noise(vec2(a * 23.0 + seed * 1.7, prog * 3.0)));
	float d = r - R;
	float edge = exp(-pow(d / thick, 2.0)) * mix(0.25, 1.0, seg);
	float fringeOut = exp(-pow((d - thick * 2.2) / (thick * 1.2), 2.0)) * mix(0.2, 1.0, seg);
	float fringeIn = exp(-pow((d + thick * 2.0) / (thick * 1.4), 2.0));
	// 折射带：前沿后面一条暗带；再往里几道余波
	float behind = clamp(-d, 0.0, 1.0);
	float band = smoothstep(0.0, 0.02, behind) * (1.0 - smoothstep(0.03, 0.14 + 0.1 * e, behind));
	float rip = pow(0.5 + 0.5 * cos(behind * 70.0 - prog * 18.0), 6.0) * exp(-behind * 9.0) * step(0.03, behind) * step(d, 0.0);
	// 放射短线：前沿外侧往外甩的细线
	float lane = pow(noise(vec2(a * 160.0, seed)), 10.0) * 4.0;
	float streak = lane * smoothstep(0.0, 0.03, d) * (1.0 - smoothstep(0.03, 0.03 + 0.18 * e, d));
	// 起手白热
	float hot = exp(-r * 14.0) * pow(1.0 - prog, 8.0) * 0.9;
	vec3 white = vec3(0.95, 0.98, 1.0), cyan = vec3(0.3, 0.8, 1.0), violet = vec3(0.62, 0.32, 1.0);
	vec3 light = (white * edge * 1.4 + violet * fringeOut * 0.8 + cyan * (fringeIn * 0.7 + rip * 0.45 + streak * 0.8) + white * hot) * fade * power;
	float shade = band * 0.45 * dark * fade;                          // 暗带：premul 下 alpha>0、颜色 0 = 压暗
	float clip = 1.0 - smoothstep(0.92, 1.0, r);
	COLOR = vec4(light * clip, shade * clip);
}" };

	/// <summary>一圈冲击波：size 是直径（像素），squash 是沿 rot 方向压扁的比例，life 秒内从中心冲出去。</summary>
	private static void Shock(Control layer, Vector2 at, float rot, float size, float squash, float life, float delay, float power, float dark)
	{
		Node2D root = Fx.Root(layer, at, "P5SinfulShock", life + delay + 0.1);
		root.Rotation = rot;
		Node2D holder = new() { Scale = new Vector2(squash, 1f) };
		root.AddChild(holder);
		ShaderMaterial m = new() { Shader = ShockShader };
		m.SetShaderParameter("seed", Fx.Rand(0f, 50f));
		m.SetShaderParameter("power", power);
		m.SetShaderParameter("dark", dark);
		m.SetShaderParameter("prog", 0f);
		ColorRect q = new() { Size = Vector2.One * size, Position = -Vector2.One * size / 2f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = delay <= 0f };
		holder.AddChild(q);
		Fx.Animate(root, delay, life, t =>
		{
			q.Visible = true;
			m.SetShaderParameter("prog", t);
		});
	}

	/// <summary>
	/// 命中区：子弹打进敌人那一片的时候整片一起反馈（不是每个敌人各一套）。zone = 几个穿透点的中心，span = 穿透点离中心最远多少。
	/// ① 三圈冲击波，圈面垂直于弹道（沿弹道压扁成椭圆，像子弹推开的空气环），一圈比一圈大、晚、淡；
	/// ② 地面波纹：贴着平台顶面的一圈很扁的椭圆，往两边扫得很远，带一层压暗；
	/// ③ 沿地面往两边喷的尘土和碎光，往前（弹道方向）的一束碎光；④ 一下冷白的闪光 + 一盏点光照亮敌人。
	/// </summary>
	private static void HitZone(Control layer, Vector2 zone, Vector2 dir, float floorY, float span, float unit)
	{
		float rot = dir.Angle(), big = 1f + span / (420f * unit);
		Shock(layer, zone, rot, 620f * unit * big, 0.42f, 0.55f, 0f, 1.2f, 1f);
		Shock(layer, zone + dir * 40f * unit, rot, 900f * unit * big, 0.38f, 0.7f, 0.06f, 0.9f, 0.8f);
		Shock(layer, zone + dir * 90f * unit, rot, 1250f * unit * big, 0.34f, 0.85f, 0.15f, 0.6f, 0.55f);
		// 用户 2026-11-09（画了示意图）：大圈前面沿弹道再排一串光圈，一圈比一圈小、一圈比一圈稍晚，像子弹往前推出去的一串空气环，越远越小
		// 都排在命中区以内（不跑出屏幕）；圈细一点、亮度适中，才看得出是一圈一圈的
		float[] trainSize = { 0.62f, 0.44f, 0.3f, 0.2f };
		float[] trainAt = { 95f, 180f, 250f, 305f };
		for (int i = 0; i < trainSize.Length; i++)
		{
			Shock(layer, zone + dir * trainAt[i] * unit * big, rot, 620f * unit * big * trainSize[i], 0.4f, 0.5f - 0.03f * i, 0.04f * (i + 1), 0.85f - 0.06f * i, 0.55f);
		}
		// 地面波纹（平台顶面上）
		Vector2 ground = new(zone.X, floorY);
		Shock(layer, ground, Mathf.Pi / 2f, 1250f * unit * big, 0.13f, 0.85f, 0.03f, 0.75f, 0.9f);   // 转 90°：沿竖直方向压扁，贴着地面
		Color[] dust = { new(0.75f, 0.85f, 1f), new(0.35f, 0.55f, 0.9f), new(0.15f, 0.2f, 0.35f) };
		Glow.Sparks(layer, ground, 40, 500f, 1500f, 0.55f, dust, 900f, 0.06f, 0.18f, Vector2.Right, 12f);
		Glow.Sparks(layer, ground, 40, 500f, 1500f, 0.55f, dust, 900f, 0.06f, 0.18f, Vector2.Left, 12f);
		Glow.Sparks(layer, zone, 60, 900f, 2600f, 0.45f, new[] { new Color(0.95f, 0.98f, 1f), new Color(0.4f, 0.8f, 1f), new Color(0.6f, 0.3f, 1f) },
			300f, 0.04f, 0.14f, dir, 35f);
		Glow.Flash(layer, zone, new Color(0.8f, 0.92f, 1f), 240f * unit, 0.1f);           // 小而短：不把敌人整片糊白
		PointLight2D pl = Light(layer, zone, new Color(0.65f, 0.85f, 1f), 1100f * unit * big);
		Fx.Animate(pl, 0, 0.6, k => pl.Energy = 1.3f * Mathf.Pow(1f - k, 2f));
		Fx.FreeAfter(pl, 0.65);
	}

	// ───────────────────────── 小细节（用户 2026-11-09：光波之外再加一点层次，不要太多） ─────────────────────────

	private static readonly Color[] Motes = { new(0.95f, 0.98f, 1f), new(0.5f, 0.85f, 1f), new(0.35f, 0.6f, 1f), new(0.65f, 0.45f, 1f) };

	/// <summary>一粒小光点：delay 秒后出现在 at，往 vel 方向慢慢漂、闪烁、缩小消失。</summary>
	private static void Mote(Control layer, Vector2 at, Vector2 vel, float size, float delay, float life, Color tint, float rise = 0f)
	{
		Sprite2D d = Glow.SoftSprite(layer, at, tint, size);
		d.Material = Fx.Additive;
		d.Visible = false;
		float s0 = size, ph = Fx.Rand(0f, Mathf.Tau), tw = Fx.Rand(14f, 26f);
		Fx.Animate(d, delay, life, t =>
		{
			d.Visible = true;
			float sec = t * life;
			d.Position = at + vel * (1f - Mathf.Exp(-3f * sec)) / 3f + new Vector2(Mathf.Sin(sec * 5f + ph) * 6f * rise, -rise * 60f * sec);
			float tw2 = 0.65f + 0.35f * Mathf.Sin(sec * tw + ph);
			d.Scale = Vector2.One * s0 * (1f - t * 0.6f);
			d.Modulate = new Color(1f, 1f, 1f, tw2 * (1f - Fx.InCubic(t)) * Mathf.Clamp(t * 12f, 0f, 1f));
		});
		Fx.FreeAfter(d, delay + life + 0.05);
	}

	/// <summary>
	/// 子弹的小细节：弹头经过的地方掉下一串小光点（往弹道两侧慢慢飘开、闪烁）；
	/// 弹道两侧几道发丝细的气流线，弹头经过时一下拉出来又收掉。
	/// </summary>
	private static void BulletDetails(Control layer, Vector2 muzzle, Vector2 dir, Vector2 screen, float unit)
	{
		float reach = screen.Length() * 1.1f, v = BulletSpeed * unit;
		Vector2 nrm = dir.Orthogonal();
		for (int i = 0; i < 34; i++)
		{
			float d = Fx.Rand(0.05f, 1f) * reach, side = Fx.Rand(-1f, 1f);
			float near = Mathf.Clamp(d / (screen.Length() * 0.55f), 0f, 1f);            // 越近越大（子弹朝我们来）
			Vector2 at = muzzle + dir * d + nrm * side * Mathf.Lerp(6f, 45f, near) * unit;
			Vector2 vel = (nrm * Mathf.Sign(side) * Fx.Rand(30f, 110f) - dir * Fx.Rand(10f, 60f)) * unit;
			Mote(layer, at, vel, Fx.Rand(0.1f, 0.3f) * Mathf.Lerp(0.6f, 1.5f, near) * unit, d / v, Fx.Rand(0.45f, 0.9f), Motes[Fx.Rng.Next(Motes.Length)]);
		}
		for (int i = 0; i < 5; i++)
		{
			float off = (i % 2 == 0 ? 1f : -1f) * Fx.Rand(28f, 95f) * unit, d0 = Fx.Rand(0.1f, 0.7f) * reach, len = Fx.Rand(260f, 620f) * unit;
			float delay = d0 / v, life = 0.2f, w = Fx.Rand(2f, 4f) * unit;
			Node2D holder = new() { Visible = false };
			layer.AddChild(holder);
			Polygon2D line = Fx.Poly(holder, new Vector2[3], new Color(0.75f, 0.92f, 1f, 0.75f), additive: true);
			Vector2 a = muzzle + dir * d0 + nrm * off;
			Fx.Animate(holder, delay, life, t =>
			{
				holder.Visible = true;
				float head = Ease5.OutExpo(Mathf.Min(1f, t * 2.5f)), tail = Fx.InCubic(t);
				line.Polygon = Glow.TaperPts(a + dir * len * tail, a + dir * len * Mathf.Max(head, tail + 0.01f), w * (1f - t * 0.5f));
			});
			Fx.FreeAfter(holder, delay + life + 0.05);
		}
	}

	/// <summary>
	/// 命中区的小细节：几颗四角星光在敌人身上此起彼伏地闪一下；一把蓝色碎片顺着弹道迸出去、带一点下坠；
	/// 一些小余烬从敌人那里慢慢往上飘、轻轻摆动。
	/// </summary>
	private static void HitDetails(Control layer, Vector2 zone, Vector2 dir, float span, float unit)
	{
		float rx = span + 120f * unit, ry = 110f * unit;
		for (int i = 0; i < 7; i++)
		{
			Vector2 at = zone + new Vector2(Fx.Rand(-rx, rx), Fx.Rand(-ry, ry));
			BlueGlint(layer, at, Fx.Rand(0.9f, 1.8f) * unit, Fx.Rand(0.05f, 0.5f));
		}
		for (int i = 0; i < 12; i++)
		{
			Vector2 at = zone + new Vector2(Fx.Rand(-rx, rx) * 0.7f, Fx.Rand(-ry, ry) * 0.6f);
			BlueShard(layer, at, dir.Rotated(Fx.Rand(-0.7f, 0.7f)), unit);
		}
		for (int i = 0; i < 26; i++)
		{
			Vector2 at = zone + new Vector2(Fx.Rand(-rx, rx), Fx.Rand(-ry * 0.5f, ry * 1.4f));
			Mote(layer, at, new Vector2(Fx.Rand(-20f, 20f), Fx.Rand(-40f, -10f)) * unit, Fx.Rand(0.08f, 0.22f) * unit, Fx.Rand(0.05f, 0.5f), Fx.Rand(0.8f, 1.4f),
				Motes[Fx.Rng.Next(Motes.Length)], 1f);
		}
	}

	/// <summary>蓝白的四角星光：一闪即逝、转一点。</summary>
	private static void BlueGlint(Control layer, Vector2 at, float size, float delay)
	{
		Node2D root = Fx.Root(layer, at, "P5SinfulGlint", delay + 0.35);
		Sprite2D halo = new() { Texture = Glow.Soft, Material = Fx.Additive };
		root.AddChild(halo);
		Polygon2D star = Fx.Poly(root, Glow.Star4(30f * size, 0.12f), new Color(0.92f, 0.97f, 1f), additive: true);
		Polygon2D star2 = Fx.Poly(root, Glow.Star4(16f * size, 0.18f), new Color(0.5f, 0.8f, 1f, 0.8f), additive: true);
		star2.Rotation = Mathf.Pi / 4f;
		root.Visible = false;
		Fx.Animate(root, delay, 0.26, t =>
		{
			root.Visible = true;
			float k = Mathf.Sin(Mathf.Min(t * 1.4f, 1f) * Mathf.Pi);
			star.Scale = Vector2.One * k;
			star2.Scale = Vector2.One * k * 0.9f;
			root.Rotation = t * 0.6f;
			halo.Scale = Vector2.One * 1.6f * size * k;
			halo.Modulate = new Color(0.4f, 0.75f, 1f, 0.6f * k);
		});
	}

	/// <summary>一片蓝色碎片：细长的四边形、亮边，顺着 dir 迸出去，阻尼减速、带下坠、转着闪光。</summary>
	private static void BlueShard(Control layer, Vector2 at, Vector2 dir, float unit)
	{
		float len = Fx.Rand(14f, 32f) * unit, wid = len * Fx.Rand(0.25f, 0.45f);
		Vector2[] shape = { new(-len * 0.5f, 0f), new(-len * 0.1f, -wid * 0.5f), new(len * 0.5f, Fx.Rand(-wid * 0.2f, wid * 0.2f)), new(len * 0.05f, wid * 0.5f) };
		Node2D root = Fx.Root(layer, at, "P5SinfulShard", 1.0);
		Polygon2D body = Fx.Poly(root, shape, new Color(0.35f, 0.65f, 1f, 0.6f), additive: true);
		Line2D edge = new() { Points = shape, Closed = true, Width = 2f * unit, DefaultColor = new Color(0.9f, 0.97f, 1f), Material = Fx.Additive, Antialiased = true };
		root.AddChild(edge);
		float speed = Fx.Rand(300f, 900f) * unit, spin = Fx.Rand(-12f, 12f), life = Fx.Rand(0.45f, 0.85f), ph = Fx.Rand(0f, Mathf.Tau);
		Fx.Animate(root, 0, life, t =>
		{
			float sec = t * life, travel = speed * (1f - Mathf.Exp(-4.5f * sec)) / 4.5f;
			root.Position = at + dir * travel + new Vector2(0f, 260f * unit * sec * sec);
			root.Rotation = dir.Angle() + spin * (1f - Mathf.Exp(-2.5f * sec)) / 2.5f;
			float glint = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(root.Rotation * 3f + ph)), 8f);
			edge.Modulate = new Color(1f + glint, 1f + glint, 1f + glint, 1f);
			root.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t));
		});
	}

	/// <summary>贯穿一个敌人：沿弹道方向喷出的火花 + 一圈冲击环 + 白闪 + 命中确认；最后一个敌人加 P5 红黑白冲击帧和横扫全场的冲击波。</summary>
	private static void Pierce(Control layer, Vector2 at, Vector2 dir, float unit, bool last)
	{
		// 用户 2026-11-09：是一发巨大的穿甲弹把所有人一起打穿，不是每人一发——每个敌人身上不做单独的圈、准星、冲击帧，
		// 只有子弹穿过时顺着弹道喷出去的火花和一下亮光；最后一个之后整条弹道再统一一震
		PointLight2D pl = Light(layer, at, new Color(0.7f, 0.9f, 1f), 600f * unit);
		Fx.Animate(pl, 0, 0.35, k => pl.Energy = 2f * (1f - k) * (1f - k));
		Fx.FreeAfter(pl, 0.4);
		Glow.Sparks(layer, at, 36, 600f, 1800f, 0.4f, new[] { new Color(0.95f, 0.98f, 1f), new Color(0.35f, 0.75f, 1f), new Color(0.65f, 0.3f, 1f) },
			200f, 0.05f, 0.14f, dir, 22f);
		// 用户 2026-11-09：原来用的是普通受击声（damage_enemy），太平淡——换成合成的贯穿声，最后一个更重
		if (last)
		{
			Fx.Shake(5);
			HitStop.Request(0.07);                                        // 打穿最后一个：顿一下
		}
	}
}
