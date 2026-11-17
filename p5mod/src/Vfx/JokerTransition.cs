using Godot;

namespace P5Joker;

/// <summary>
/// 选人界面点「开始」→ 进游戏的过场（整个替换原版静默猎手那一堆黑色斜条切进来）。
/// 用户 2026-11-17：体现怪盗主题、简单、干脆利落不拖沓、拒绝塑料感；不要图案（预告信、面具都不要），
/// 做黑色剪影的动画、和原版差不多，可以适当用红色。否掉的：Joker 全身剪影、红黑直斜条 + 眼罩、预告信甩进来。
/// 两个方案（variant），一个着色器画完（NTransition 的材质，TransitionPatch 换上；threshold 0 → 1 线性走，0.8s）：
///  0「斗篷一甩」：一张巨大的黑斗篷从左上角甩过整个屏幕盖下来 —— 边是弯的（外沿落后、布在抖），外沿一圈圆润的下摆，
///     紧贴着边一道红色的里衬，边前面一点点红光；甩完剩下的角一下收黑。
///  1「衣摆黑影」：像原版一样几道黑影交替从左右甩进来，但每道是一条弯的衣摆（中间宽、头部收尖），一侧一道红里衬，
///     中间一道是红的；最后收黑。
/// </summary>
public static class JokerTransition
{
	public const float Time = 0.8f;
	/// <summary>0 斗篷一甩，1 衣摆黑影（等用户挑）。</summary>
	public static int Variant = 0;

	private static Shader? _shader;

	public static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform float threshold : hint_range(0, 1) = 0.0;
uniform int variant = 0;
const vec3 RED = vec3(0.86, 0.04, 0.11);
float outCubic(float x) { x = clamp(x, 0.0, 1.0); return 1.0 - pow(1.0 - x, 3.0); }
void over(inout vec3 p, inout float A, vec3 c, float a) { p = c * a + p * (1.0 - a); A = a + A * (1.0 - a); }

// 0：斗篷一甩（支点在左上角外面，顺时针甩下来）
void cape(vec2 p, float aspect, float t, float px, inout vec3 col, inout float A) {
	vec2 P0 = vec2(-aspect * 0.5 - 0.15, -0.65);
	vec2 v = p - P0;
	float rho = length(v);
	float phi = atan(v.y, v.x);
	float k = outCubic((t - 0.03) / 0.62);
	float th = mix(-0.4, 1.9, k);
	float lag = 0.45 * pow(rho / 2.2, 1.6) * (1.0 - 0.7 * k);          // 外沿落后 → 边是弯的
	float flut = 0.03 * sin(rho * 8.0 - t * 26.0) * smoothstep(0.5, 2.0, rho) * (1.0 - k);
	float edge = th - lag + flut;
	float behind = (edge - phi) * rho;                                  // 到边的弧长（屏高单位，+ 已盖住）
	// 下摆：外沿一圈圆润的起伏（跟着斗篷走）
	float lobes = pow(0.5 + 0.5 * cos((edge - phi) * 11.0), 2.0);
	float hem = length(vec2(aspect + 0.15, 1.15)) + 0.05 + 0.22 * lobes;   // 下摆在屏幕外：整屏都盖得到
	float cover = smoothstep(-px, px, behind) * smoothstep(px, -px, rho - hem);
	// 斗篷里一点点暗红的褶（很淡）
	float fold = 0.5 + 0.5 * sin(phi * 24.0 + rho * 2.5 - k * 3.0);
	vec3 cloth = mix(vec3(0.0), vec3(0.09, 0.0, 0.02), fold * 0.6);
	over(col, A, cloth, cover);
	// 红里衬：紧贴边的一条（越靠外越宽），从亮红渐到暗红
	float lw = 0.025 + 0.06 * clamp(rho / 2.2, 0.0, 1.0);
	float lining = smoothstep(-px, px, behind) * smoothstep(lw + px, lw - px, behind) * smoothstep(px, -px, rho - hem);
	vec3 lc = mix(RED * 1.1, RED * 0.45, clamp(behind / lw, 0.0, 1.0));
	over(col, A, lc, lining * (1.0 - smoothstep(0.75, 0.95, k)));
	// 边前面一点红光（甩动时）
	float ahead = exp(-max(-behind, 0.0) / 0.03) * step(behind, 0.0) * smoothstep(px, -px, rho - hem) * (1.0 - k) * 0.45;
	over(col, A, RED, ahead);
}

// 1：衣摆黑影。一条：中线 y = c0 + c1 x + c2 x²，从 dir 那边甩进来（-1 左 / 1 右），宽 w，lining：红里衬在哪一侧（±1，0 = 整条红）
void tail(vec2 p, float aspect, float t, float px, float c0, float c1, float c2, float w, float dir, float t0, float lining,
		inout vec3 col, inout float A) {
	float k = outCubic((t - t0) / 0.3);
	if (k <= 0.0) return;
	float L = aspect * 0.5 + 0.3;
	float xh = mix(L, -L, k) * dir;                    // 头的位置（从 dir 那边进来）
	float along = (p.x - xh) * dir;                     // + 在头后面（已经甩过）
	float f = c0 + c1 * p.x + c2 * p.x * p.x;
	float slope = c1 + 2.0 * c2 * p.x;
	float e = (p.y - f) / sqrt(1.0 + slope * slope);    // 到中线的带符号距离
	float taper = smoothstep(0.0, 0.35, along);         // 头部收尖
	float ww = w * taper * (0.85 + 0.15 * smoothstep(1.4, 0.4, along));
	float body = smoothstep(ww + px, ww - px, abs(e)) * step(0.0, along);
	if (lining == 0.0) {
		over(col, A, RED, body);
	} else {
		over(col, A, vec3(0.0), body);
		float lw = 0.018 + 0.012 * taper;
		float side = e * lining;
		float lin = body * smoothstep(ww - lw - px, ww - lw + px, side);
		over(col, A, RED, lin);
	}
	// 头部一点红光（甩动时）
	float hx = xh, hy = c0 + c1 * hx + c2 * hx * hx;
	float glow = exp(-length(p - vec2(hx, hy)) / 0.05) * (1.0 - k) * 0.6;
	over(col, A, RED, glow);
}

void fragment() {
	float t = threshold;
	float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
	vec2 p = vec2((UV.x - 0.5) * aspect, UV.y - 0.5);
	float px = SCREEN_PIXEL_SIZE.y;
	vec3 col = vec3(0.0);
	float A = 0.0;
	if (variant == 0) {
		cape(p, aspect, t, px, col, A);
	} else {
		tail(p, aspect, t, px, -0.38, 0.28, -0.10, 0.17, -1.0, 0.00, 1.0, col, A);
		tail(p, aspect, t, px, 0.34, 0.22, 0.10, 0.19, 1.0, 0.08, -1.0, col, A);
		tail(p, aspect, t, px, -0.02, 0.30, -0.08, 0.07, -1.0, 0.16, 0.0, col, A);
		tail(p, aspect, t, px, -0.16, 0.26, 0.10, 0.13, 1.0, 0.24, 1.0, col, A);
		tail(p, aspect, t, px, 0.16, 0.30, -0.08, 0.12, -1.0, 0.32, -1.0, col, A);
	}
	// 收尾：剩下的一下收黑（斗篷甩完就收，不留静止的一段）
	over(col, A, vec3(0.0), variant == 0 ? smoothstep(0.58, 0.72, t) : smoothstep(0.78, 0.93, t));
	COLOR = vec4(col / max(A, 1e-4), A);
}" };

	/// <summary>过场材质（threshold 0 → 1）。</summary>
	public static ShaderMaterial Make(int variant = -1)
	{
		ShaderMaterial m = new() { Shader = Shader, ResourceLocalToScene = true };
		m.SetShaderParameter("variant", variant < 0 ? Variant : variant);
		m.SetShaderParameter("threshold", 0f);
		return m;
	}
}
