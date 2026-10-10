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
	COLOR = vec4(tint.rgb * (1.0 + core), (core + glow) * alpha * tint.a);
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
}
