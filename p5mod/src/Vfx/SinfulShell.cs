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
uniform vec2 origin2 = vec2(1920.0, 600.0);
uniform float conceal = 0.0;
uniform int part = 0;
uniform float clock = 0.0;
uniform float dim = 0.0;
uniform float flash = 0.0;
uniform float feet_y = 830.0;
uniform float cliff_x = 640.0;
uniform float floor_y = 830.0;
uniform float cliff2_x = 1000.0;
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
	float s = reveal - d;
	if (conceal > 0.0) {
		float d2 = length((px - origin2) * vec2(1.0, 1.25)) + n;
		s = min(s, d2 - conceal);
	}
	return s;
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

	// ───────────────────────── 面具蓝焰冲天 ─────────────────────────

	private static Shader? _jet;

	/// <summary>
	/// 冲天的面具蓝焰（用户 2026-11-08：之前那两道是粗光柱、挡脸、不像火）：和面具蓝焰同一套画法——四档硬边配色、往上窜的火舌、
	/// 上半截一块块断开飘走；形状是一条细长、底窄顶尖、一路弯曲摆动的火舌。x：-1～1 横向，y：0 底 → 1 顶。
	/// grow = 火舌冲到多高，thin = 收细、从底部断开往上散。
	/// </summary>
	private static Shader JetShader => _jet ??= new Shader { Code = @"
shader_type canvas_item;
uniform float grow = 0.0;
uniform float thin = 0.0;
uniform float clock = 0.0;
uniform float seed = 0.0;
uniform float bend = 0.3;
" + Noise + @"
void fragment() {
	float t = clock + seed * 7.0;
	float y = 1.0 - UV.y;
	// 火舌的中心线：一路弯曲（整体弯向 bend 那边），细节摆动往上流
	float cx = bend * y * y + 0.3 * (noise(vec2(y * 4.0 - t * 6.0, seed)) - 0.5) * (0.3 + y);
	float x = (UV.x - 0.5) * 2.0 - cx;
	float w = pow(max(1.0 - y / max(grow, 0.01), 0.0), 0.7) * 0.5 * (1.0 - 0.7 * thin);
	w *= 0.7 + 0.6 * noise(vec2(y * 16.0 - t * 22.0, seed * 2.0));
	float body = clamp(1.0 - abs(x) / max(w, 0.001), 0.0, 1.0);
	// 火舌：两层往上窜的噪声 + 细丝，亮度分层明显（四档颜色都看得到），不是一根光滑的管子
	float tongue = fbm(vec2(x * 5.0 + seed, y * 9.0 - t * 16.0));
	float fil = pow(1.0 - abs(noise(vec2(x * 14.0 + seed, y * 3.0 - t * 9.0)) * 2.0 - 1.0), 6.0);
	float e = pow(body, 0.8) * (0.15 + 1.15 * tongue) + fil * body * 0.5;
	// 上半截一块块断开飘走；收细时从底部断开
	float gap = smoothstep(0.55, 0.8, noise(vec2(x * 5.0 + seed * 3.0, y * 5.0 - t * 12.0)));
	e *= 1.0 - gap * smoothstep(0.45, 0.85, y / max(grow, 0.01)) * 0.95;
	e *= smoothstep(thin * 0.9, thin * 0.9 + 0.08, y);
	e = max(e, 0.0);
	vec3 col = e > 1.0 ? vec3(0.94, 0.98, 1.0) : e > 0.62 ? vec3(0.4, 0.8, 1.0) : e > 0.3 ? vec3(0.1, 0.38, 0.92) : vec3(0.03, 0.09, 0.32);
	COLOR = vec4(col, smoothstep(0.2, 0.24, e) * step(y, grow));
}" };

	/// <summary>
	/// Joker 攥面具那一下：两道面具蓝焰从面具火的两只尖角冲上天（往两边斜着、不经过脸），0.08s 冲到顶 → 跳动 → 收细往上散；召唤的雷紧接着劈下。
	/// mask = 面具中心，half = 面具半宽（屏幕像素），facing = Joker 朝向。
	/// </summary>
	public static void FlameJets(Control layer, Vector2 mask, float half, float facing, float unit)
	{
		Sfx.Play(Sfx.PersonaFlame, 1f);
		Sfx.Play(Sfx.SwishHeavy, 0.6f);
		// 两只尖角（面具火的形状：两边往上翘，远的那边被透视压窄）
		(Vector2 tip, float lean, float delay, float w, float h)[] jets =
		{
			(mask + new Vector2(-0.95f * facing, -0.55f) * half, -0.22f * facing, 0f, 70f, 860f),
			(mask + new Vector2(0.62f * facing, -0.6f) * half, 0.16f * facing, 0.03f, 56f, 760f),
		};
		foreach ((Vector2 tip, float lean, float delay, float w, float h) in jets)
		{
			Glow.Flash(layer, tip, new Color(0.4f, 0.78f, 1f), 120f * unit, 0.18f, delay);
			Glow.Sparks(layer, tip, 14, 300f, 900f, 0.35f, new[] { new Color(0.9f, 0.97f, 1f), new Color(0.3f, 0.7f, 1f) }, -200f, 0.04f, 0.12f,
				Vector2.Up.Rotated(lean), 18f);
			Node2D root = Fx.Root(layer, tip, "P5SinfulJet", delay + 0.55);
			root.Rotation = lean;
			ShaderMaterial m = new() { Shader = JetShader };
			m.SetShaderParameter("seed", Fx.Rand(0f, 10f));
			m.SetShaderParameter("bend", 0.25f * Mathf.Sign(lean));
			ColorRect r = new() { Size = new Vector2(w * 2.4f, h) * unit, Position = new Vector2(-w * 1.2f, -h) * unit, Material = m,
				MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(r);
			Fx.Clock(r);
			Fx.Animate(root, delay, 0.45, t =>
			{
				float s = t * 0.45f;
				m.SetShaderParameter("grow", 1f - Mathf.Pow(1f - Mathf.Clamp(s / 0.08f, 0f, 1f), 3f));
				m.SetShaderParameter("thin", Mathf.Clamp((s - 0.18f) / 0.27f, 0f, 1f));
			});
		}
	}

	// ───────────────────────── 时序 ─────────────────────────

	/// <summary>各拍的时刻（从出牌算起，秒）。</summary>
	public const float RevealAt = 0.05f, RevealTime = 0.7f, AimAt = 0.45f, BulletSpeed = 5200f, ExitAfter = 1.1f, ExitTime = 0.6f;

	/// <summary>开枪（用户 2026-11-08：不蓄力，转完枪停一小下就开）：撒旦耶尔接住枪后 0.2s。</summary>
	public const float FireAt = AimAt + SatanaelPuppet.CatchAfter + 0.2f;

	/// <summary>接住枪的时刻。</summary>
	public const float CatchAt = AimAt + SatanaelPuppet.CatchAfter;

	/// <summary>召唤撒旦耶尔的雷劈下的时刻（用户 2026-11-08：场景还没烧完雷就劈下来，前半段不空）。</summary>
	public const float AppearAt = 0.3f;

	public sealed class Setup
	{
		public Vector2 JokerFeet, JokerHead, JokerMuzzle, PersonaMuzzle;
		/// <summary>撒旦耶尔图层画布左上角在屏幕上的位置（1080p 像素）：脸推出屏幕上方、身体大半在左边外面，只露出翅膀、一部分身体和伸进来的持枪手臂（用户 2026-11-08）。</summary>
		public Vector2 PersonaOrigin = new(-297f, -202f);   // 肩膀约在 (420,230)：枪口停在敌人前面一大段   // 肩膀约在 (520,336)：远处高处、偏左
		public float PersonaScale = 0.7f;   // 第 10 版：远处的巨人，头顶接近屏幕上缘、大腿以下埋进云海（用户 2026-11-08）
		public float FloorY;
		public List<Vector2> Enemies = new();
		/// <summary>穿过第 i 个敌人（按离枪口由近到远）的那一刻：游戏在这里结算这个敌人的伤害。</summary>
		public Action<int>? OnPierce;
		/// <summary>Joker 该开枪的时刻（游戏里播 shiv_from_aim）：FireAt - 0.18。</summary>
		public Action? OnJokerAim, OnJokerShoot;
		/// <summary>Joker 攥面具的时刻（从出牌算起）：两道蓝焰从面具冲上天。null = 不放（没有扶面具的架势时）。</summary>
		public float? MaskSqueezeAt = 0.08f;
		public Vector2 JokerMask;
		/// <summary>面具蓝焰的半宽（屏幕像素，ReadyFx.BuildMask 里是 Joker 身高 × 0.24）。</summary>
		public float JokerMaskHalf = 60f;
		/// <summary>场景里的人物（Joker、敌人的 Visuals）：场景期间统一染上夜色，结束时还原。</summary>
		public List<CanvasItem> Actors = new();
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

	/// <summary>放整段大罪穿甲弹。back = 背景层（场景、人格面具），front = 前景层（子弹、命中）。返回子弹穿过每个敌人的时刻（秒，从现在算）。</summary>
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
		float total = FireAt + ExitAfter + ExitTime + 0.2f;
		foreach (ShaderMaterial m in new[] { sky, rocks })
		{
			m.SetShaderParameter("sres", screen);
			m.SetShaderParameter("origin", feetS);
			m.SetShaderParameter("origin2", new Vector2(screen.X * 1.05f, screen.Y * 0.55f));
			m.SetShaderParameter("feet_y", feetS.Y);
			m.SetShaderParameter("floor_y", floorS.Y);
			m.SetShaderParameter("cliff_x", feetS.X - 95f * unit);           // Joker 站在悬崖边上
			m.SetShaderParameter("cliff2_x", Mathf.Max(feetS.X + 420f * unit, firstEnemyX - 160f * unit));
		}
		sky.SetShaderParameter("part", 0);
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
		// 时停中（stopping）：开枪的闪光、枪口光压到很低（定格那一帧不要一大团白光），放开后再炸
		bool stopping = false;
		Action<float> step = t =>
		{
			float time = t * total;
			float rv = Mathf.Clamp((time - RevealAt) / RevealTime, 0f, 1f);
			float reveal = far * (1f - Mathf.Pow(1f - rv, 2.2f));
			float ex = Mathf.Clamp((time - FireAt - ExitAfter - 0.1f) / ExitTime, 0f, 1f);
			float conceal = ex <= 0f ? 0f : far * ex * ex;
			const float dim = 0f;
			float flash = time >= FireAt ? Mathf.Exp(-(time - FireAt) / 0.05f) : 0f;
			// 不蓄力：接住枪时枪口一闪，开枪时一闪
			float charge = time < CatchAt ? 0f : time < FireAt ? 0.9f * Mathf.Exp(-(time - CatchAt) / 0.08f) : 1.7f * Mathf.Exp(-(time - FireAt) / 0.12f);
			if (stopping)
			{
				flash *= 0.12f;
				charge = Mathf.Min(charge, 0.35f);
			}
			float thunder = 0f;
			if (puppet != null)
			{
				puppet.Update(time, RiseAt, AimAt, FireAt + 0.12f, exitAt, puppet.MuzzleLocal, charge, flash * 0.6f, dim);
				thunder = puppet.Lightning;
				muzzleLight.GlobalPosition = puppet.MuzzleGlobal;
			}
			foreach (ShaderMaterial m in new[] { sky, rocks })
			{
				m.SetShaderParameter("reveal", reveal);
				m.SetShaderParameter("conceal", conceal);
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
		if (s.MaskSqueezeAt is float squeeze)
		{
			Fx.After(front, squeeze, () => FlameJets(front, s.JokerMask, s.JokerMaskHalf, 1f, unit));
		}
		// 召唤 / 退场的闪雷声；接住枪的金属声
		Fx.After(front, AppearAt, () => { Sfx.Play(Sfx.SinfulThunder, 1.1f); Fx.Shake(3); });
		Fx.After(front, exitAt, () => { Sfx.Play(Sfx.SinfulThunder, 0.9f); Fx.Shake(2); });
		if (puppet != null)
		{
			Fx.After(front, CatchAt, () => Sfx.Play(Sfx.SinfulCatch, 1f));
		}
		// 场景里飘的蓝色余烬
		Fx.After(back, RevealAt + RevealTime * 0.6f, () =>
		{
			GpuParticles2D embers = ReadyFx.Embers(root, new Vector2(screen.X * 0.5f, screen.Y * 0.9f), new[] { new Color(0.8f, 0.95f, 1f), new Color(0.3f, 0.7f, 1f), new Color(0.1f, 0.3f, 0.9f) },
				26, screen.X * 0.5f, 3.2f, 30f, 110f, 25f);
			Fx.After(root, FireAt + ExitAfter - (RevealAt + RevealTime * 0.6f), () => embers.Emitting = false);
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
		Fx.After(front, FireAt - 0.12f, () => Sfx.Play(Sfx.SinfulFire, 1.25f));          // 合成的开枪声，开头 0.12s 是倒吸，主冲击落在开枪那一帧
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
			// 用户 2026-11-09：开枪那一帧时停。定格时枪口不留大光球（之前冻住的闪光 / 波纹就是那团光污染），冲击都放到解除那一下
			stopping = true;
			step(Mathf.Min(FireAt / total, 1f));
			TimeStop(front, muzzle, dir, stopHits, screen, unit, () =>
			{
				stopping = false;
				Fx.Shake(5);
				Glow.Flash(front, muzzle, new Color(0.85f, 0.95f, 1f), 260f * unit, 0.15f);
			});
			Rings(front, muzzle, dir, unit * 0.45f);                       // 远处的枪口：冲击环小
			Bullet(front, muzzle, dir, screen, unit);
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
		return pierce;
	}

	// ───────────────────────── 时停 ─────────────────────────

	/// <summary>时停总时长（真实时间，秒）：定格 → 弹道刻印 → 一枪贯穿 → 解除。</summary>
	public const float StopTime = 0.62f;

	/// <summary>刻印阶段结束、贯穿那一笔落下的时刻（真实时间，从定格算起）。</summary>
	private const float ThrustAt = 0.5f, ThrustTime = 0.07f;

	private static bool _frozen;

	private static void Freeze(bool on)
	{
		if (on == _frozen) return;
		_frozen = on;
		Engine.TimeScale = on ? 0.0 : 1.0;
		if (on && Engine.GetMainLoop() is SceneTree tree)
		{
			tree.CreateTimer(2.5, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () => { if (_frozen) Freeze(false); };
		}
	}

	private static void AfterRT(Node anchor, double sec, Action a) =>
		anchor.GetTree()?.CreateTimer(sec, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Connect("timeout",
			Callable.From(() => { if (GodotObject.IsInstanceValid(anchor)) Stage.Safe("sinful stop", a); }));

	private static void AnimRT(Node owner, double sec, Action<float> step)
	{
		Tween tw = owner.CreateTween().SetIgnoreTimeScale(true);
		tw.SetPauseMode(Tween.TweenPauseMode.Process);
		step(0f);
		tw.TweenMethod(Callable.From<float>(t => { if (GodotObject.IsInstanceValid(owner)) step(t); }), 0f, 1f, sec);
	}

	/// <summary>折线展开成多边形；taper 时两端收尖（毛笔笔锋）。</summary>
	private static Vector2[] StrokePts(Vector2[] points, float width, bool taper)
	{
		int n = points.Length;
		var poly = new Vector2[n * 2];
		for (int i = 0; i < n; i++)
		{
			Vector2 d = (points[Math.Min(i + 1, n - 1)] - points[Math.Max(i - 1, 0)]).Normalized();
			float w = taper && n > 1 ? width * Mathf.Max(Mathf.Sin(Mathf.Pi * i / (n - 1)), 0.12f) : width;
			Vector2 nrm = new Vector2(-d.Y, d.X) * (w * 0.5f);
			poly[i] = points[i] + nrm;
			poly[n * 2 - 1 - i] = points[i] - nrm;
		}
		return poly;
	}

	/// <summary>
	/// 时停（用户 2026-11-09）。逻辑照君王之剑的结构（刻满 → 定住 → 一刀打开），但内容是大罪穿甲弹自己的：
	/// ① 扣扳机的一瞬硬停：一帧闪白，画面蒙上米白高调色层（半透明，不读屏幕——游戏里读屏幕会变成一整块不透明），世界定格，子弹刚出膛；
	/// ② 弹道刻印：子弹未来要走的路被一圈圈墨黑的膛线环沿弹道「印」出去（越往前越大——子弹朝我们飞来，越来越快），
	///    几道细墨线顺着弹道划过；印到哪个敌人，那里就炸开一团墨黑的弹孔裂痕；
	/// ③ 一枪贯穿：一笔又粗又尖的墨黑刺击沿整条弹道捅穿（近大远小），笔锋带一道白刃；
	/// ④ 解除：蓝白闪，米白层撤掉，刚才所有墨痕瞬间变成发光的青白光痕后散去；时间恢复，子弹一口气飞完、依次贯穿结算。
	/// </summary>
	private static void TimeStop(Control layer, Vector2 muzzle, Vector2 dir, List<Vector2> hits, Vector2 screen, float unit, Action onRelease)
	{
		Freeze(true);
		Node2D root = Fx.ScreenRoot(layer, "P5SinfulTimeStop");
		Color paper = new(0.97f, 0.94f, 0.88f), ink = new(0.05f, 0.04f, 0.05f, 0.94f);
		ColorRect grade = new() { Size = screen, Color = new Color(paper, 0.6f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(grade);
		Node2D inkLayer = new(), glowLayer = new();
		root.AddChild(inkLayer);
		root.AddChild(glowLayer);
		// 解除时要变成光痕的形状：给一个线宽倍数，返回多边形
		var shapes = new List<Func<float, Vector2[]>>();
		Func<Vector2, Vector2> sp = w => Fx.ScreenPoint(layer, w);
		Vector2 a = sp(muzzle);
		Vector2 sdir = (sp(muzzle + dir * 100f) - a).Normalized(), nrm = sdir.Orthogonal();
		float far = (screen * 1.25f).Length();
		float Along(Vector2 w) => (sp(w) - a).Dot(sdir);
		float lastHit = hits.Count > 0 ? hits.Max(Along) : screen.X * 0.6f;
		float span = lastHit + 200f * unit;

		// ① 硬停：一帧闪白
		ColorRect white = new() { Size = screen, Color = new Color(1f, 1f, 1f, 0.95f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(white);
		AnimRT(white, 0.1, t => white.Color = new Color(1f, 1f, 1f, 0.95f * (1f - Fx.OutCubic(t))));
		Sfx.Play(Sfx.SinfulStop, 1.1f);                                     // 整段时停一条音轨（节拍和下面对齐）

		// ② 膛线环：沿弹道印出去，越往前越大、越来越密
		const int rings = 8;
		const float stampFrom = 0.08f, stampTo = 0.44f;
		float StampTime(float dist) => stampFrom + (stampTo - stampFrom) * Mathf.Pow(Mathf.Clamp(dist / span, 0f, 1f), 0.65f);
		for (int i = 0; i < rings; i++)
		{
			float u = (i + 0.6f) / rings, dist = span * u;
			Vector2 c = a + sdir * dist;
			float r = Mathf.Lerp(45f, 190f, u) * unit, squash = 0.3f, w = Mathf.Lerp(8f, 22f, u) * unit;
			float start = Fx.Rand(0f, Mathf.Tau), sweep = Fx.Rand(4.4f, 5.4f);
			Vector2[] Arc(float k)
			{
				int m = 22;
				var pts = new Vector2[m];
				for (int q = 0; q < m; q++)
				{
					float ang = start + sweep * k * q / (m - 1);
					pts[q] = c + sdir * (Mathf.Cos(ang) * r * squash) + nrm * (Mathf.Sin(ang) * r);
				}
				return pts;
			}
			shapes.Add(s => StrokePts(Arc(1f), w * s, true));
			AfterRT(root, StampTime(dist), () =>
			{
				Polygon2D poly = Fx.Poly(inkLayer, new Vector2[3], ink);
				AnimRT(poly, 0.06, t => poly.Polygon = StrokePts(Arc(Mathf.Max(Fx.OutCubic(t), 0.08f)), w, true));
			});
		}
		// 顺着弹道划过的细墨线（略偏开弹道，长短不一）
		for (int i = 0; i < 4; i++)
		{
			float off = (i % 2 == 0 ? 1f : -1f) * Fx.Rand(30f, 110f) * unit, w = Fx.Rand(2.5f, 6f) * unit;
			Vector2 p0 = a + sdir * Fx.Rand(-80f, 120f) * unit + nrm * off * 0.3f, p1 = a + sdir * far + nrm * off * 2.2f;
			shapes.Add(s => StrokePts(new[] { p0, p1 }, w * s, true));
			AfterRT(root, 0.12f + 0.08f * i, () =>
			{
				Polygon2D poly = Fx.Poly(inkLayer, new Vector2[3], ink);
				AnimRT(poly, 0.07, t => poly.Polygon = StrokePts(new[] { p0, p0.Lerp(p1, Mathf.Max(Fx.OutCubic(t), 0.02f)) }, w, true));
			});
		}
		// ③ 一枪贯穿：一笔墨黑刺击沿整条弹道捅穿（枪口细、往前越来越粗——朝我们飞来），笔锋一道白刃
		Vector2 tipEnd = a + sdir * far;
		Vector2[] Wedge(float k, float s)
		{
			Vector2 tip = a.Lerp(tipEnd, k);
			float wEnd = Mathf.Lerp(10f, 120f, k) * unit * s, wBase = 7f * unit * s;
			Vector2 neck = a.Lerp(tip, 0.9f);
			return new[] { a + nrm * wBase, neck + nrm * wEnd * 0.5f, tip, neck - nrm * wEnd * 0.5f, a - nrm * wBase };
		}
		shapes.Add(s => Wedge(1f, s * 1.1f));
		AfterRT(root, ThrustAt, () =>
		{
			Polygon2D thrust = Fx.Poly(inkLayer, new Vector2[3], new Color(0.03f, 0.02f, 0.03f, 0.97f));
			Polygon2D edge = Fx.Poly(inkLayer, new Vector2[3], new Color(1f, 1f, 1f, 0.9f));
			AnimRT(thrust, ThrustTime, t =>
			{
				float k = Mathf.Max(Fx.OutCubic(t), 0.02f);
				thrust.Polygon = Wedge(k, 1f);
				Vector2 tip = a.Lerp(tipEnd, k), back = a.Lerp(tip, 0.75f);
				edge.Polygon = StrokePts(new[] { back + nrm * 6f * unit, tip }, 4f * unit, true);
			});
		});

		// ④ 解除
		AfterRT(root, StopTime, () =>
		{
			Freeze(false);
			grade.Visible = false;
			inkLayer.Visible = false;
			white.Color = new Color(0.85f, 0.93f, 1f, 0.9f);
			white.MoveToFront();
			AnimRT(white, 0.16, t => white.Color = new Color(0.85f, 0.93f, 1f, 0.9f * (1f - Fx.OutCubic(t))));
			foreach (Func<float, Vector2[]> shape in shapes)
			{
				Fx.Poly(glowLayer, shape(2.4f), new Color(0.3f, 0.7f, 1f, 0.22f), additive: true);
				Fx.Poly(glowLayer, shape(0.55f), new Color(0.9f, 0.97f, 1f, 0.95f), additive: true);
			}
			AnimRT(glowLayer, 0.55, t => glowLayer.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(Mathf.Clamp((t - 0.25f) / 0.75f, 0f, 1f))));
			onRelease();
			AfterRT(root, 0.6, () => root.QueueFree());
		});
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

	/// <summary>贯穿一个敌人：沿弹道方向喷出的火花 + 一圈冲击环 + 白闪 + 命中确认；最后一个敌人加 P5 红黑白冲击帧和横扫全场的冲击波。</summary>
	private static void Pierce(Control layer, Vector2 at, Vector2 dir, float unit, bool last)
	{
		// 用户 2026-11-09：是一发巨大的穿甲弹把所有人一起打穿，不是每人一发——每个敌人身上不做单独的圈、准星、冲击帧，
		// 只有子弹穿过时顺着弹道喷出去的火花和一下亮光；最后一个之后整条弹道再统一一震
		PointLight2D pl = Light(layer, at, new Color(0.7f, 0.9f, 1f), 600f * unit);
		Fx.Animate(pl, 0, 0.35, k => pl.Energy = 3f * (1f - k) * (1f - k));
		Fx.FreeAfter(pl, 0.4);
		Glow.Sparks(layer, at, 36, 600f, 1800f, 0.4f, new[] { new Color(0.95f, 0.98f, 1f), new Color(0.35f, 0.75f, 1f), new Color(0.65f, 0.3f, 1f) },
			200f, 0.05f, 0.14f, dir, 22f);
		Sfx.Play(Sfx.Hit, last ? 0.9f : 0.6f);
		if (last)
		{
			Fx.Shake(4);
		}
	}
}
