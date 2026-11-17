using Godot;

namespace P5Joker;

/// <summary>
/// 选人界面点「开始」→ 进游戏的过场（替换原版静默猎手的剪影擦除，用户 2026-11-17：体现怪盗主题、简单、不要花哨贴图、拒绝塑料感）。
/// 原版：NTransition.FadeOut(0.8s, silent_transition_mat) 把 threshold 0 → 1，材质按一张灰度图从剪影往外抹黑。
/// 我们换成自己的材质（TransitionPatch 在 FadeOut 前把缓存里那条路径换掉，时长拉到 Time），同一个 threshold：
///  ① 0～0.25 画面压暗透深红；Joker 的全身剪影（纯黑、贴边一圈柔和的红光）从略大缩到原大、淡入，站在画面中间；
///  ② 0.3 红光「咚」地亮一下（心跳），面具眼位一点红光一闪；
///  ③ 0.42 起黑色从剪影边缘往外漫开（距离场），前沿一道柔和的红光、边缘略带起伏，直到整屏黑。
/// 只用一张距离场贴图（tools/p5/build_transition.py 生成），没有别的贴图和粒子。
/// </summary>
public static class JokerTransition
{
	public const float Time = 1.3f;

	private static Shader? _shader;

	public static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D sdf : filter_linear, repeat_disable;
uniform float threshold : hint_range(0, 1) = 0.0;
uniform vec2 eye = vec2(0.518, 0.140);       // 面具眼位（贴图 UV）
const float TEX_ASPECT = 1.7777778;
const float EDGE_PX = 28.0;                   // G 通道 ±0.5 对应的像素（贴图 720 高）
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), f.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), f.x), f.y); }
void over(inout vec3 p, inout float A, vec3 c, float a) { p = c * a + p * (1.0 - a); A = a + A * (1.0 - a); }
void fragment() {
	float t = threshold;
	float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
	vec2 uv = vec2(0.5 + (UV.x - 0.5) * aspect / TEX_ASPECT, UV.y);
	// 剪影入场：从 1.06 倍缩到原大
	float sil = smoothstep(0.02, 0.2, t);
	float sc = mix(1.06, 1.0, 1.0 - pow(1.0 - sil, 3.0));
	vec2 suv = vec2(0.5, 0.55) + (uv - vec2(0.5, 0.55)) / sc;
	bool outTex = suv.x < 0.0 || suv.x > 1.0 || suv.y < 0.0 || suv.y > 1.0;
	vec3 tx = outTex ? vec3(1.0, 1.0, 0.0) : texture(sdf, suv).rgb;
	float R = outTex ? 1.0 + max(abs(suv.x - 0.5) - 0.5, abs(suv.y - 0.5) - 0.5) : tx.r;
	float px = 1.0 / (SCREEN_PIXEL_SIZE.y * 720.0);           // 贴图像素 → 屏幕像素
	float ed = (tx.g - 0.5) * 2.0 * EDGE_PX;                 // 到剪影边的带符号距离（贴图像素，+ 外）
	float aa = 0.8 / max(px, 0.01);
	float body = smoothstep(aa, -aa, ed);
	float pulse = exp(-pow((t - 0.31) / 0.045, 2.0));
	float glow = exp(-max(ed, 0.0) / (7.0 + 5.0 * pulse)) * (1.0 - body);
	// 往外漫的黑（前沿略起伏）
	float fl = clamp((t - 0.42) / 0.56, 0.0, 1.0);
	fl = fl * fl * (3.0 - 2.0 * fl);
	float k = fl * 1.12;
	float wob = (vn(UV * vec2(7.0, 4.0) + t * 3.0) - 0.5) * 0.05 * fl;
	float front = R - k + wob;
	float covered = step(0.001, fl) * smoothstep(0.012, -0.004, front);
	float band = step(0.001, fl) * exp(-abs(front) / 0.022) * (1.0 - fl * 0.5);
	// 面具眼位一点红光
	vec2 ed2 = (uv - eye) * vec2(TEX_ASPECT, 1.0);
	float eyeL = exp(-dot(ed2, ed2) / 0.00004) * pulse;
	vec3 crimson = vec3(0.16, 0.0, 0.03);
	vec3 red = vec3(0.92, 0.06, 0.14);
	vec3 p = vec3(0.0);
	float A = 0.0;
	over(p, A, crimson, 0.55 * smoothstep(0.0, 0.25, t));
	over(p, A, vec3(0.0), body * sil);
	over(p, A, red, clamp(glow * sil * (0.75 + 0.9 * pulse), 0.0, 1.0));
	over(p, A, vec3(0.0), covered);
	over(p, A, mix(red, vec3(1.0, 0.55, 0.55), 0.25), clamp(band * 0.85, 0.0, 1.0) * (1.0 - covered * 0.5));
	over(p, A, vec3(1.0, 0.35, 0.4), clamp(eyeL, 0.0, 1.0) * (1.0 - covered));
	COLOR = vec4(p / max(A, 1e-4), A);
}" };

	/// <summary>过场材质（threshold 0 → 1）。sdf：res://p5joker/fx/joker_transition.png。</summary>
	public static ShaderMaterial? Make()
	{
		Texture2D? tex = Fx.Tex("joker_transition");
		if (tex == null) return null;
		ShaderMaterial m = new() { Shader = Shader, ResourceLocalToScene = true };
		m.SetShaderParameter("sdf", tex);
		m.SetShaderParameter("threshold", 0f);
		return m;
	}
}
