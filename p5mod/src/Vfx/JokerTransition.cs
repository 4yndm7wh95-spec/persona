using Godot;

namespace P5Joker;

/// <summary>
/// 选人界面点「开始」→ 进游戏的过场（整个替换原版静默猎手的过场）。
/// 用户 2026-11-17：体现怪盗主题、简单、干脆利落不拖沓、拒绝塑料感；不要图案；黑色剪影的动画、和原版差不多，可以适当用红色。
/// 否掉的：Joker 全身剪影、红黑直斜条 + 眼罩、预告信甩进来、斗篷一甩 / 衣摆黑影（太粗糙：几何形状、平涂硬边）。
/// 现在照原版的做法（research/sts2_transitions：一张「每个像素什么时候变黑」的时间图，阈值 0 → 1）：
/// 原版静默猎手是几道细刀痕先划过屏幕，再把刀痕围出的块一块块抹黑。Joker 版同一个节奏，加红：
///  ① 0.04～0.44 七道细刀痕一道道划过屏幕（两头尖、带一点弯），刚划开的地方白热、随即变红；
///  ② 0.42～0.83 刀痕围出的块从外往里一块块抹黑，每块从靠外的一侧扫过去，刚盖住的边先是暗红再变黑；
///     红色的刀痕留在黑块之间（屏幕被切开的缝）；
///  ③ 0.84 起刀痕暗下去，0.9 起整屏收黑。
/// 时间图：res://p5joker/fx/joker_transition.png（tools/p5/build_transition.py 生成；R 变黑时间、G 刀痕划到的时间、B 刀痕覆盖度）。
/// </summary>
public static class JokerTransition
{
	public const float Time = 0.8f;

	private static Shader? _shader;

	public static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform float threshold : hint_range(0, 1) = 0.0;
uniform sampler2D map : filter_linear, repeat_disable;
const vec3 RED = vec3(0.86, 0.04, 0.11);
const vec3 HOT = vec3(1.0, 0.85, 0.8);
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
void over(inout vec3 p, inout float A, vec3 c, float a) { p = c * a + p * (1.0 - a); A = a + A * (1.0 - a); }
void fragment() {
	float t = threshold;
	vec3 m = texture(map, UV).rgb;
	vec3 col = vec3(0.0);
	float A = 0.0;
	// 抹黑（抖一点，避免 8 位时间值的色带）
	float tb = m.r + (h21(FRAGCOORD.xy) - 0.5) * 0.007;
	float blk = clamp((t - tb) / max(fwidth(m.r), 0.0015) + 0.5, 0.0, 1.0);
	float fresh = clamp(1.0 - (t - tb) / 0.015, 0.0, 1.0) * blk;          // 刚盖住的边先是暗红
	over(col, A, RED * 0.35 * fresh, blk);
	// 刀痕：划到的地方白热 → 红；留在黑块之间，最后暗下去
	float drawn = clamp((t - m.g) / max(fwidth(m.g), 0.0015) + 0.5, 0.0, 1.0) * m.b;
	vec3 lc = mix(HOT, RED, smoothstep(0.0, 0.08, t - m.g));
	over(col, A, lc, drawn * (1.0 - smoothstep(0.84, 0.93, t)));
	// 收尾整屏黑
	over(col, A, vec3(0.0), smoothstep(0.9, 0.97, t));
	COLOR = vec4(col / max(A, 1e-4), A);
}" };

	/// <summary>过场材质（threshold 0 → 1）。没有时间图就返回 null（用原版过场）。</summary>
	public static ShaderMaterial? Make()
	{
		Texture2D? tex = Fx.Tex("joker_transition");
		if (tex == null) return null;
		ShaderMaterial m = new() { Shader = Shader, ResourceLocalToScene = true };
		m.SetShaderParameter("map", tex);
		m.SetShaderParameter("threshold", 0f);
		return m;
	}
}
