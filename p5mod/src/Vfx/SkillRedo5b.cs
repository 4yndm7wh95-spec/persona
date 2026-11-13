using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 第五批续：余像、萎靡（2026-11-13，会话 D）。和「影刃旋风」（融入暗影，用户认可）同一套语言：带速度纹的能量刀光 / 疾光、白芯红边、
/// 柔光光芒、碎光；不做具体的形状（用户：水晶 / 蝴蝶太实体化，要泛化的、纯炫技、够帅）。
/// </summary>
public static partial class SkillRedo5
{
	private static Shader? _dash;

	/// <summary>
	/// 一道直的疾光：UV.x 0 尾 → 1 头，UV.y 横向。尾巴细、往头越粗、头圆；白芯 + 外层颜色，里面是顺着方向拉长的速度纹（和刀光同一种）。
	/// </summary>
	private static Shader Dash => _dash ??= new Shader { Code = @"
shader_type canvas_item;
uniform float alpha = 1.0;
uniform float hot = 1.0;
uniform float flow = 0.0;
uniform float seed = 0.0;
uniform vec4 tint : source_color = vec4(1.0, 0.1, 0.18, 1.0);
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), f.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), f.x), f.y); }
void fragment() {
	float x = UV.x;
	float y = UV.y * 2.0 - 1.0;
	float w = mix(0.06, 1.0, pow(x, 1.2)) * sqrt(max(0.0, 1.0 - pow(max(x - 0.88, 0.0) / 0.12, 2.0)));
	float wl = max(w, 1e-3);
	float body = exp(-pow(y / wl, 2.0) * 3.0);
	float core = exp(-pow(y / (wl * 0.3), 2.0) * 3.0);
	float fib = vn(vec2(x * 5.0 - flow, y / wl * 4.0 + seed * 13.0)) * 0.65 + vn(vec2(x * 15.0 - flow * 2.0, y / wl * 10.0 + seed)) * 0.35;
	fib = smoothstep(0.25, 0.85, fib);
	vec3 col = mix(tint.rgb, vec3(1.0, 0.96, 0.95), clamp(core * hot * (0.3 + 0.7 * x), 0.0, 1.0));
	float a = (body * (0.35 + 0.65 * fib) * 0.85 + core * 0.6) * pow(x, 0.7) * alpha;
	if (a <= 0.003) discard;
	COLOR = vec4(col, clamp(a, 0.0, 1.0));
}" };

	/// <summary>
	/// 一道疾光沿 path(0→1) 飞：t0 起飞，dur 飞完；头在 path(e)，长度随飞过的距离长到 len，最后一段淡掉。
	/// </summary>
	private static void DashFly(Control layer, Func<float, Vector2> path, float t0, float dur, float len, float thick, Color tint, float hot = 1f,
		Func<float, float>? ease = null, float fadeFrom = 0.75f)
	{
		Node2D root = Fx.Root(layer, path(0f), "P5Dash", t0 + dur + 0.1);
		ShaderMaterial m = new() { Shader = Dash };
		m.SetShaderParameter("tint", tint);
		m.SetShaderParameter("hot", hot);
		m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
		ColorRect rect = new() { Size = new Vector2(1f, 1f), Position = new Vector2(-1f, -0.5f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(rect);
		root.Visible = false;
		Func<float, float> ez = ease ?? Ease5.OutQuint;
		Fx.Animate(root, t0, dur, t =>
		{
			root.Visible = true;
			float e = ez(t);
			Vector2 hd = path(e), back = path(Mathf.Max(0f, e - 0.03f));
			Vector2 d = hd - back;
			if (d.LengthSquared() > 0.01f) root.Rotation = d.Angle();
			root.GlobalPosition = hd;
			float travelled = path(0f).DistanceTo(hd);
			root.Scale = new Vector2(Mathf.Max(1f, Mathf.Min(len, travelled + len * 0.15f)), thick);
			m.SetShaderParameter("flow", t * 10f);
			m.SetShaderParameter("alpha", Mathf.Min(1f, t * 8f) * (1f - Ease5.Seg(t, fadeFrom, 1f - fadeFrom)));
		});
	}

	private static Shader? _echo;

	/// <summary>
	/// 余像的残影：TEXTURE = 冻住的 Joker 剪影。剪影填红（中间深一点），边上一圈白粉的亮边；smear（UV，带方向）方向拖出带横向速度纹的拖影。
	/// </summary>
	private static Shader EchoSmear => _echo ??= new Shader { Code = @"
shader_type canvas_item;
uniform float smear = 0.0;
uniform float alpha = 1.0;
uniform float flow = 0.0;
uniform float seed = 0.0;
uniform float fill = 0.75;
uniform vec4 tint : source_color = vec4(0.92, 0.06, 0.14, 1.0);
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), f.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), f.x), f.y); }
float sil(sampler2D tx, vec2 uv) { return smoothstep(0.3, 0.6, texture(tx, uv).a); }
void fragment() {
	float base = sil(TEXTURE, UV);
	float acc = 0.0, wsum = 0.0;
	for (int i = 1; i <= 14; i++) {
		float k = float(i) / 14.0;
		float wt = 1.0 - k * 0.8;
		acc += sil(TEXTURE, UV + vec2(smear * k, 0.0)) * wt;
		wsum += wt;
	}
	float trail = acc / wsum;
	float fib = vn(vec2(UV.x * 6.0 - flow, UV.y / TEXTURE_PIXEL_SIZE.y * 0.22 + seed * 17.0));
	fib = smoothstep(0.3, 0.8, fib);
	// 亮边
	float nb = 0.0;
	for (int i = 0; i < 8; i++) {
		float a = 6.2831853 * float(i) / 8.0;
		nb += sil(TEXTURE, UV + vec2(cos(a), sin(a)) * TEXTURE_PIXEL_SIZE * 2.0);
	}
	float rim = clamp(base - nb / 8.0, 0.0, 1.0) * 3.0 + clamp(nb / 8.0 - base, 0.0, 1.0) * 1.5;
	vec3 deep = vec3(0.45, 0.0, 0.06);
	float fibIn = smoothstep(0.35, 0.75, vn(vec2(UV.x * 3.0 - flow * 0.6, UV.y / TEXTURE_PIXEL_SIZE.y * 0.12 + seed * 5.0)));
	vec3 col = mix(tint.rgb, deep, 0.5 * base * (1.0 - fibIn));
	col = mix(col, tint.rgb * 1.15 + vec3(0.12, 0.04, 0.05), fib * 0.5 * (1.0 - base));
	float rr = clamp(rim, 0.0, 1.0);
	col = mix(col, vec3(1.0, 0.4, 0.45), rr * 0.8);
	float a = max(base * fill * (0.55 + 0.45 * fibIn) * mix(0.6, 1.0, UV.y), trail * (0.2 + 0.8 * fib) * 0.8);
	a = max(a, rr * 0.75);
	a *= alpha;
	if (a <= 0.003) discard;
	COLOR = vec4(col, clamp(a, 0.0, 1.0));
}" };

	/// <summary>冻住此刻的他，挂一层红残影（back 层），返回 (节点, 材质)；没有 CopyBody 时 null。</summary>
	private static (Sprite2D Sp, ShaderMaterial M)? RedEcho(SkillCtx c, double life)
	{
		if (SkillRedo4.Freeze(c, c.Back ?? c.Front, life) is not { } fr) return null;
		ShaderMaterial m = new() { Shader = EchoSmear };
		m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
		m.SetShaderParameter("alpha", 0f);
		Sprite2D sp = new() { Texture = fr.Tex, Centered = false, Material = m };
		fr.Root.AddChild(sp);
		return (sp, m);
	}

	private static Shader? _press;

	/// <summary>
	/// 把一块画面往下压扁（读屏）：box 里以 floor（脚下那条线，屏幕 UV）为基准纵向压缩 amt，同时往深红黑里压暗 dark，边上软过渡。
	/// </summary>
	private static Shader Press => _press ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D scr : hint_screen_texture, filter_linear;
uniform vec4 box = vec4(0.0, 0.0, 1.0, 1.0);
uniform float floor_y = 1.0;
uniform float amt = 0.0;
uniform float dark = 0.0;
void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 c = (box.xy + box.zw) * 0.5, hs = (box.zw - box.xy) * 0.5;
	vec2 q = (uv - c) / hs;
	float m = smoothstep(1.0, 0.45, length(q));
	vec2 src = vec2(uv.x, floor_y + (uv.y - floor_y) * (1.0 + amt * m));
	vec3 col = texture(scr, src).rgb;
	COLOR = vec4(col, 1.0);
}" };

	/// <summary>一道绕轴旋转的刀光（Arc 着色器，前后两层各画一半）；位置 / 大小 / 角度每帧自己设。</summary>
	private static Blade MakeBlade(Control front, Control back, Vector2 at, Color tint, float hot, float ink, float width, float len, float spin, float life)
	{
		Blade b = new() { W = width, Len = len, Spin = spin };
		foreach (bool fr in new[] { false, true })
		{
			Node2D n = Fx.Root(fr ? front : back, at, fr ? "P5BladeF" : "P5BladeB", life + 0.1);
			ShaderMaterial m = new() { Shader = Arc };
			m.SetShaderParameter("side", fr ? 1f : -1f);
			m.SetShaderParameter("tint", tint);
			m.SetShaderParameter("hot", hot);
			m.SetShaderParameter("ink", ink);
			m.SetShaderParameter("width", width);
			m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
			m.SetShaderParameter("dir", Mathf.Sign(spin));
			m.SetShaderParameter("len", len);
			n.AddChild(new ColorRect { Size = new Vector2(2f, 2f), Position = new Vector2(-1f, -1f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
			n.Visible = false;
			if (fr) { b.F = n; b.MF = m; } else { b.B = n; b.MB = m; }
		}
		return b;
	}
}
