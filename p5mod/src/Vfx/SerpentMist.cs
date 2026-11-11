using Godot;

namespace P5Joker;

/// <summary>
/// 群蛇形态的场景：咒雾漫地（2026-11-11 用户：日蚀不搭、像硬放上去的，颜色太耀眼；但没有这类东西蛇又显得孤单 → 选了「咒雾漫地」）。
/// 不在天上挂东西，场景和蛇同一个来源：从 Joker 脚下、身后的蛇巢涌出一层贴地翻滚的近黑咒雾，往敌人那边漫过去越来越薄；
/// 只有雾的上沿和最深处透一点暗红（和蛇身边缘同一种光）。雾里偶尔浮起一截很大、很模糊的蛇背，翻一下又沉下去。
/// 背景整体压暗、降饱和（读屏幕纹理调色，不是刷一层红），四角更暗。
/// side：-1 背景层（调色 + 雾 + 蛇背 + 蛇巢往上飘的烟），1 前景层（只有贴地一薄层雾丝，盖住脚踝）。
/// 用法：ColorRect 铺满屏幕（Fx.ScreenRoot），每帧设 aspect / feet / tall / face / reveal / from_uv / amount，clock 用 Fx.Clock。
/// </summary>
public static class SerpentMist
{
	private static Shader? _shader;

	public static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float side = -1.0;
uniform float aspect = 1.7778;
uniform float clock = 0.0;
uniform float amount = 1.0;
uniform vec2 feet = vec2(0.22, 0.76);    // Joker 脚底（0～1）
uniform float tall = 0.3;               // Joker 身高（屏幕高度的比例）
uniform float face = 1.0;               // 朝敌人的方向
uniform float reveal = 1.0;             // 0 → 1：从 from_uv 往外漫开
uniform vec2 from_uv = vec2(0.22, 0.6);

float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.02 + vec2(3.1, 7.7); a *= 0.5; } return v; }

void fragment() {
	vec2 uv = UV;
	vec2 asp = vec2(aspect, 1.0);
	// 漫开：从 from_uv 往外，边上一圈翻滚的暗烟
	float rd = length((uv - from_uv) * asp);
	float en = noise(uv * asp * 7.0 + vec2(clock * 0.3, 0.0)) * 0.6 + noise(uv * asp * 15.0) * 0.4;
	float R0 = reveal * 2.3;
	float spread = smoothstep(R0, R0 - 0.3, rd + (en - 0.5) * 0.35);
	float edge = smoothstep(0.16, 0.0, abs(rd + (en - 0.5) * 0.35 - (R0 - 0.15))) * step(0.001, reveal) * (1.0 - step(0.999, reveal)) * step(rd, R0);

	// 离 Joker 多远（朝敌人为正，单位 = 身高）；雾在他脚下最厚，往敌人那边漫过去慢慢变薄，往身后很快收掉
	float dx = (uv.x - feet.x) * aspect / tall * face;
	float reach = exp(-pow(max(dx, 0.0) / 4.5, 2.0)) * exp(-pow(min(dx, 0.0) / 1.8, 2.0));
	reach = 0.45 + 0.55 * reach;
	// 雾往敌人那边慢慢流（翻滚：两层不同速度的噪声互相扭）
	vec2 fp = vec2(uv.x * aspect * 2.2 - clock * 0.05 * face, uv.y * 6.0);
	vec2 warp = vec2(fbm(fp * 0.7 + vec2(0.0, clock * 0.04)), fbm(fp * 0.7 + 5.2 - vec2(clock * 0.03, 0.0)));
	float fn = fbm(fp + warp * 1.6);
	float fine = noise(fp * 3.1 + warp * 2.0 - vec2(clock * 0.2 * face, 0.0));
	// 雾顶的高度（从脚底往上，单位 = 身高）：脚下高到膝盖以上，远处低；顶边是一团团翻起来的
	float top = (0.16 + 0.36 * exp(-pow(dx / 1.5, 2.0))) * (0.75 + 0.55 * fbm(vec2(uv.x * aspect * 1.6 - clock * 0.04 * face, clock * 0.05)));
	float hgt = (feet.y - uv.y) / tall;                         // 脚底以上多高（负 = 脚底以下）
	float fogEdge = top + (fn - 0.5) * 0.18 - hgt;              // > 0 在雾里
	float fog = smoothstep(-0.03, 0.12, fogEdge) * reach;
	// 一团团翻滚：细一档的噪声决定浓淡，往上挪一点再取一次，差值当作团的受光面（上面亮、下面暗）
	vec2 bp = vec2(uv.x * aspect / tall * 2.6 - clock * 0.12 * face, hgt * 5.0 - clock * 0.08) + warp * 1.8;
	float bl = fbm(bp);
	float blUp = fbm(bp + vec2(0.0, 0.35));
	float billowLit = clamp((bl - blUp) * 4.0, -1.0, 1.0);
	fog *= 0.5 + 0.7 * smoothstep(0.32, 0.72, bl);

	// 雾里浮起的巨蛇背：一条很长的正弦曲线，只有拱起来的那几段露出雾面，整条慢慢往前游；包络让它隔一阵才出现一次
	float hump = 0.0, humpRim = 0.0;
	for (int k = 0; k < 2; k++) {
		float fk = float(k);
		float sx = uv.x * aspect / tall;
		float ph = sx * (2.3 + 0.5 * fk) - clock * (0.55 + 0.15 * fk) * face + fk * 2.4;
		float arch = sin(ph);
		// 包络：沿 x 慢慢移动的一段（大约两三个拱），并且随时间浮起、沉下
		float cx = feet.x * aspect / tall + face * (0.6 + 1.6 * fk) + sin(clock * (0.11 + 0.05 * fk) + fk * 3.0) * 1.4;
		float env = exp(-pow((sx - cx) / 1.0, 2.0)) * smoothstep(0.15, 0.7, sin(clock * (0.23 + 0.07 * fk) + fk * 1.7) * 0.5 + 0.5);
		// 波谷沉到地面以下（只有拱起的几段露出来），露出来的部分底下埋进雾里
		float yc = -0.12 + (0.34 + 0.06 * fk) * arch * env;          // 背脊中线离地多高（身高为单位）
		float th = (0.07 + 0.02 * fk) * env;                          // 背的半厚
		float d = abs(hgt - yc + (fine - 0.5) * 0.03) - th;
		float body = smoothstep(0.035, -0.02, d) * smoothstep(0.0, 0.25, arch * env) * smoothstep(-0.02, 0.06, hgt);
		// 远处那条更淡，像隔着雾
		hump = max(hump, body * (k == 0 ? 0.85 : 0.55));
		// 背脊最上沿一道暗红的光（和蛇身的边光一样，只在上沿）
		humpRim = max(humpRim, smoothstep(0.03, 0.0, abs(hgt - (yc + th) + 0.012)) * smoothstep(0.45, 0.8, arch * env) * (k == 0 ? 1.0 : 0.6));
	}

	if (side < 0.0) {
		// ── 背景层 ──
		// 背景压暗、降饱和，偏一点冷的暗红；上面和四角更暗
		vec3 b = texture(screen_tex, SCREEN_UV).rgb;
		float lum = dot(b, vec3(0.3, 0.55, 0.15));
		vec3 g = mix(b, vec3(lum) * vec3(1.1, 0.82, 0.86), 0.75) * 0.55;
		g += vec3(0.05, 0.013, 0.018);
		// 对比（用户嫌日蚀耀眼，但黑蛇要有亮一点的底才看得清）：Joker 身后、雾里透出一大片很柔、低饱和的暗红背光，不是一个光源
		vec2 bd = (uv - feet + vec2(0.1 * face * tall / aspect, 0.85 * tall)) * asp / tall;
		float back = exp(-dot(bd * vec2(0.75, 1.0), bd * vec2(0.75, 1.0)) / 2.2);
		back *= 0.8 + 0.4 * fbm(bd * 0.8 + vec2(clock * 0.03, -clock * 0.05));
		g += vec3(0.34, 0.08, 0.08) * back * 0.95;
		float vig = smoothstep(0.35, 1.0, length((uv - vec2(0.5, 0.55)) * vec2(aspect * 0.55, 1.0)));
		g *= (1.0 - 0.5 * vig) * mix(0.75, 1.0, smoothstep(0.0, 0.5, uv.y));
		vec3 col = g;
		// 蛇巢往上飘的烟：Joker 身后一股往上散的暗烟（接住五条蛇的尾巴）
		float nx = (uv.x - feet.x) * aspect / tall + 0.12 * face;
		float rise = smoothstep(0.5, 0.85, fbm(vec2(nx * 2.2, hgt * 1.4 - clock * 0.35) + 3.0))
			* exp(-pow(nx / (0.45 + 0.25 * hgt), 2.0)) * smoothstep(1.9, 0.6, hgt) * smoothstep(0.1, 0.5, hgt);
		col = mix(col, vec3(0.02, 0.004, 0.008), rise * 0.45);
		// 雾：近黑，雾顶一层暗红，深处零星透一点暗红的光
		// 雾：暗淡的灰红，比背景亮一点（看得出一团团翻滚），越近 Joker 越被背光照亮；深处零星一点暗红
		vec3 fc = mix(vec3(0.07, 0.022, 0.028), vec3(0.19, 0.06, 0.066), smoothstep(0.3, 0.85, fn));
		fc *= 0.8 + 0.6 * exp(-pow(dx / 1.6, 2.0));
		// 上亮下暗：雾顶被背光照着，越往下越沉，一团团的体积感
		fc *= mix(0.55, 1.25, smoothstep(top * 0.2, top * 0.95, hgt + (fn - 0.5) * 0.1));
		float topGlow = smoothstep(0.2, 0.0, fogEdge) * smoothstep(-0.04, 0.06, fogEdge) * smoothstep(0.5, 0.8, fine);
		fc += vec3(0.2, 0.045, 0.05) * topGlow * 0.4;
		fc *= 1.0 + 0.55 * billowLit;
		fc += vec3(0.3, 0.04, 0.05) * smoothstep(0.72, 0.9, fine * fn * 1.6) * smoothstep(0.1, 0.4, fogEdge) * 0.2;
		col = mix(col, fc, fog * 0.8);
		// 巨蛇背：比雾更黑一点，上沿一道暗红
		col = mix(col, vec3(0.012, 0.003, 0.005), hump * 0.95);
		col += vec3(0.32, 0.045, 0.055) * humpRim * 0.22;
		// 漫开的边：一圈暗烟
		col = mix(col, vec3(0.02, 0.003, 0.008), edge * 0.7);
		// 没漫到的地方保持原背景；整个场景跟着 amount 淡入淡出（读的是原背景，所以输出不透明）
		float k = clamp(max(spread, edge * 0.85) * amount, 0.0, 1.0);
		if (k <= 0.002) discard;
		COLOR = vec4(mix(b, col, k), 1.0);
	} else {
		// ── 前景层：贴地一薄层雾丝，盖住脚踝和鞋（也盖住敌人的脚），淡、不压暗角色 ──
		float low = smoothstep(0.1, -0.02, hgt - (fn - 0.5) * 0.1) * smoothstep(-0.12, 0.0, hgt);
		float wisp = smoothstep(0.45, 0.8, fbm(vec2(uv.x * aspect * 3.5 - clock * 0.09 * face, hgt * 9.0 + clock * 0.1) + warp));
		float af = low * wisp * reach * 0.6 * spread * amount;
		vec3 c = mix(vec3(0.1, 0.03, 0.035), vec3(0.24, 0.075, 0.075), smoothstep(0.3, 0.8, bl)) * (1.0 + 0.4 * billowLit);
		if (af <= 0.002) discard;
		COLOR = vec4(c * af, af);
	}
}" };
}
