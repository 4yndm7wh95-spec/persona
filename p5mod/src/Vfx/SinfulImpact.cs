using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 华丽收场第二版的冲击（用户 2026-11-19：打出那一下太柔弱、开枪不够爽）。全部着色器实时渲染，没有细线、光圈、放射线：
/// · 开场：打出时背景先压暗、面具上的蓝焰往里一收，接着顿一下（白底黑剪影 → 黑底亮剪影两帧），蓝焰从 Joker 身上整团炸开（FireBurst），
///   场景被这一炸一下掀开（烧穿圈 0.1s 里铺满屏幕，边上是一道很厚的蓝焰浪，surge 控制）；
/// · 开枪：两帧强对比冲击帧（场景刷白、人物和撒旦耶尔全黑 → 场景全黑、人物被枪光打亮），粗重的炮口焰 + 一大团往前推的炮口烟（Smoke），
///   一颗看得见的重弹（BulletShader：白热弹头 + 前面一层被挤开的气浪 + 身后翻滚的等离子），身后留一条翻滚着散掉的烟道（Tunnel）；
/// · 打中：第一下顿帧，每个敌人被打透（过曝）、往后一震（KnockBack），身后顺着弹道炸出一大股贯穿的蓝焰（ExitBurst）和一团烟。
/// </summary>
public static partial class SinfulShell
{
	private static Shader? _burst, _smoke, _tunnel, _flood, _ripple;

	/// <summary>开场的蓝焰爆发：整团往外炸的火（火舌被噪声撕开、外沿一层更亮的壳），白热的芯，prog 0→1 炸开再散掉。加法。</summary>
	private static Shader BurstShader => _burst ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float prog = 0.0;
uniform float seed = 0.0;
uniform float power = 1.0;
uniform float spike = 1.0;   // 火舌多尖：开场 1；引爆 0.35（更圆、翻滚的一团，不是尖刺星形）
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), ang = atan(p.y, p.x);
	float e = 1.0 - pow(1.0 - prog, 3.0);
	// 火舌：角度方向的噪声顺着半径拉长（往外窜），外沿参差不齐
	float tongue = fbm(vec2(ang * mix(1.6, 3.2, spike) + seed, r * 1.4 - prog * 5.0));
	float R = (0.08 + 0.86 * e) * (1.0 - 0.5 * spike + 0.95 * spike * tongue + (1.0 - spike) * 0.3 * (tongue - 0.5));
	float inside = clamp(1.0 - r / max(R, 0.001), 0.0, 1.0);
	float turb = fbm(p * 3.2 + vec2(seed, -prog * 3.5));
	float shell = exp(-pow((r - R * 0.8) / (0.05 + 0.12 * e), 2.0)) * (0.45 + 0.55 * turb) * (1.0 - 0.5 * e);
	float body = pow(inside, 0.9) * (0.25 + 0.75 * turb) * (1.0 - 0.88 * e);   // 中间很快烧空，只剩往外冲的火舌
	float hot = exp(-r / (0.035 + 0.1 * e)) * (1.0 - 0.95 * e);
	float fade = pow(1.0 - prog, 1.8);
	float heat = body * 1.3 + shell * 0.9 + hot * 1.6;
	vec3 deep = vec3(0.05, 0.16, 0.62), cyan = vec3(0.3, 0.75, 1.0), white = vec3(0.95, 0.98, 1.0);
	vec3 col = mix(deep, cyan, smoothstep(0.1, 0.65, heat));
	col = mix(col, white, smoothstep(0.75, 1.4, heat));
	float a = (body + shell + hot * 1.4) * fade * power;
	COLOR = vec4(col * a * pow(1.0 - smoothstep(0.35, 1.0, r), 1.4), 1.0);   // 同上：软淡出，不留硬边
}" };

	/// <summary>
	/// 一团烟（炮口烟、命中的烟）：前 25% 的时间猛地胀开，之后慢慢胀大、顺着 drift 漂、被噪声啃散；
	/// 一开始被里面的火光照亮（heat），很快冷成蓝灰。premul：烟是真的压暗背景，里面的光是加上去的。
	/// </summary>
	private static Shader SmokeShader => _smoke ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float prog = 0.0;
uniform float seed = 0.0;
uniform float heat = 1.0;
uniform float thick = 0.8;
uniform vec2 drift = vec2(0.25, -0.2);
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float e = 1.0 - pow(1.0 - min(prog * 4.0, 1.0), 3.0);
	float grow = 0.22 + 0.4 * e + 0.22 * prog;
	vec2 q = p - drift * prog;
	float n = fbm(q * 2.4 + vec2(seed, -prog * 1.4));
	float n2 = fbm(q * 5.2 + vec2(-prog * 2.2, seed * 1.3));
	float d = length(q) / grow + (n - 0.5) * 0.75 + (n2 - 0.5) * 0.3;
	float dens = smoothstep(1.0, 0.3, d) * (1.0 - smoothstep(0.3, 1.0, prog)) * (0.5 + 0.5 * n);
	float glowk = heat * exp(-prog * 7.0) * smoothstep(1.0, 0.0, d);
	vec3 smoke = mix(vec3(0.13, 0.16, 0.24), vec3(0.32, 0.38, 0.52), smoothstep(0.3, 0.8, n2));   // 比夜空亮一点才看得出是烟
	float a = clamp(dens * thick, 0.0, 0.92) * (1.0 - smoothstep(0.55, 1.0, length(p)));
	// 里面的火光只在烟浓的地方（被照亮的是烟，不是一整块发光的圆）
	vec3 light = (vec3(0.35, 0.72, 1.0) * glowk + vec3(0.9, 0.97, 1.0) * glowk * glowk * 0.25) * smoothstep(0.05, 0.6, dens);
	COLOR = vec4(smoke * a + light, a);
}" };

	/// <summary>
	/// 弹道留下的烟道（替换原来的一根细光痕 + 螺旋细丝 + 天空细缝）：子弹经过的地方翻起一条粗烟柱，芯里一道很快冷掉的青白热光，
	/// 烟一边变宽、往上飘，一边从噪声里一块块散掉。head = 弹头走到哪（0～1），age = 开枪后几秒。premul。
	/// </summary>
	private static Shader TunnelShader => _tunnel ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float len_px = 2000.0;
uniform float head = 0.0;
uniform float age = 0.0;
uniform float life = 1.6;
uniform float seed = 0.0;
" + Noise + @"
void fragment() {
	float x = UV.x, y = (UV.y - 0.5) * 2.0, xp = x * len_px;
	float on = step(x, head) * smoothstep(0.0, 0.025, x) * (1.0 - smoothstep(0.96, 1.0, x));
	float k = clamp(age / life, 0.0, 1.0);
	float yy = y + 0.3 * k * k;
	float w = 0.22 + 0.7 * k;
	float n = fbm(vec2(xp * 0.0035 - age * 0.5 + seed, yy * 1.4 - age * 0.7));
	float n2 = fbm(vec2(xp * 0.01 + seed * 2.0, yy * 3.2 + age * 0.6));
	float prof = exp(-pow(yy / w, 2.0) * (1.3 - 0.7 * n));
	float dissolve = smoothstep(k * 1.3 - 0.15, k * 1.3 + 0.15, n * 0.75 + n2 * 0.45);
	// 第三版（用户：偏软、像打出了一团烟雾）：烟道不再发光，只是子弹过去以后留下的一条暗烟，薄、很快散
	float a = clamp(prof * (0.45 + 0.6 * n) * dissolve * on * 0.75, 0.0, 0.7) * (1.0 - smoothstep(0.85, 1.0, abs(y)));
	vec3 smoke = mix(vec3(0.1, 0.12, 0.18), vec3(0.24, 0.28, 0.38), smoothstep(0.3, 0.8, n2));
	COLOR = vec4(smoke * a, a);
}" };

	/// <summary>
	/// 打中那一下的光（第五版，用户 2026-11-19：不要连环引爆，火舌很塑料，只要打到敌人那一下更华丽、更有视觉冲击）：
	/// 命中点爆出一大片刺眼的光往外淹——中间白热，往外青、蓝，最外圈带一点紫；外沿按角度被低频噪声推得一边远一边近
	/// （宽而软的几束光，不是细线），没有任何硬边形状。画在人物后面就是逆光：敌人被照成剪影。power / spread 由调用方每帧给（按真实时间走，顿帧里照样亮）。加法。
	/// </summary>
	private static Shader FloodShader => _flood ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float power = 0.0;
uniform float spread = 1.0;
uniform float seed = 0.0;
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), ang = atan(p.y, p.x);
	float beams = 0.6 + 0.6 * fbm(vec2(ang * 2.0 + seed, seed * 0.37));
	float reach = spread * beams;
	float glow = exp(-r / max(0.06 + 0.38 * reach, 0.01));
	float wide = exp(-r / max(0.2 + 0.5 * reach, 0.01)) * 0.35;
	float core = exp(-r / 0.035);
	vec3 white = vec3(1.0), cyan = vec3(0.45, 0.85, 1.0), blue = vec3(0.12, 0.38, 1.0), violet = vec3(0.42, 0.22, 0.95);
	vec3 col = mix(violet, blue, smoothstep(0.02, 0.2, glow));
	col = mix(col, cyan, smoothstep(0.2, 0.55, glow));
	col = mix(col, white, smoothstep(0.6, 1.0, glow));
	vec3 c = col * (glow * 1.7 + wide) + white * core * 2.0;
	// 2026-11-19 用户：实机里光有很硬的边框 → 外圈的光在画布边上还很亮、被一刀切掉。改成从中间就开始软软地淡出，到边上已经是 0
	COLOR = vec4(c * power * pow(1.0 - smoothstep(0.2, 1.0, r), 1.6), 1.0);
}" };

	/// <summary>一片命中的光（直径 size），返回根节点和材质（调用方每帧设 power / spread）。</summary>
	private static (Node2D Root, ShaderMaterial Mat) Flood(Control layer, Vector2 at, float size, double life)
	{
		Node2D root = Fx.Root(layer, at, "P5SinfulFlood", life);
		ShaderMaterial m = Quad(root, FloodShader, Vector2.One * size, -Vector2.One * size / 2f);
		return (root, m);
	}

	/// <summary>后面几个敌人被穿过时各亮一下（游戏时间里自己走完）：一片小一些的光，0.25s 退掉。</summary>
	private static void FloodPulse(Control layer, Vector2 at, float size, float peak, float life)
	{
		(Node2D root, ShaderMaterial m) = Flood(layer, at, size, life + 0.05);
		Fx.Animate(root, 0, life, t =>
		{
			m.SetShaderParameter("power", peak * Mathf.Pow(1f - t, 1.6f));
			m.SetShaderParameter("spread", 0.5f + 0.5f * Mathf.Sqrt(t));
		});
	}

	/// <summary>
	/// 打中的冲击波纹（用户 2026-11-19：可以加一些波纹）：一圈有厚度的冲击波——前沿亮（白），外沿一层紫色色散、里面一层青，
	/// 后面跟一条压暗的折射带，再拖两圈越来越淡的余波；亮度沿圈被噪声分成明暗不一的几段，粗细也起伏（不是一条均匀的细线）。
	/// prog 0→1 先猛后缓地扩开、越扩越细越淡；squash 竖向压扁（1 = 正圆，贴地的那道很扁）。premul：亮的是加上去的，暗带真的压暗。
	/// </summary>
	private static Shader RippleShader => _ripple ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float prog = 0.0;
uniform float seed = 0.0;
uniform float power = 1.0;
uniform float squash = 1.0;
uniform float drift = 0.0;   // 圈心沿局部 -y 移动多少（四边形半边长为单位）：立在弹道上的圈被子弹往前带
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float e = 1.0 - pow(2.0, -8.0 * prog);
	p.y += drift * e;
	p.y /= max(squash, 0.05);
	float r = length(p), ang = atan(p.y, p.x);
	float R = 0.05 + 0.88 * e;
	float fade = pow(1.0 - prog, 1.3);
	float a = ang / 6.2831853 + 0.5;
	float seg = 0.5 + 0.5 * smoothstep(0.25, 0.75, noise(vec2(a * 9.0 + seed, prog * 2.0)));
	float w = (0.012 + 0.03 * (1.0 - e)) * (0.7 + 0.6 * noise(vec2(a * 21.0 + seed * 1.7, 0.0)));
	float d = r - R;
	float front = exp(-pow(d / w, 2.0)) * seg;
	float fringe = exp(-pow((d - w * 1.8) / (w * 1.1), 2.0)) * seg;
	float inner = exp(-pow((d + w * 1.6) / (w * 1.3), 2.0));
	float echo = 0.0;
	for (int i = 1; i <= 2; i++) {
		float Ri = R * (1.0 - 0.14 * float(i));
		echo += exp(-pow((r - Ri) / (w * 0.8), 2.0)) * (0.45 / float(i)) * seg;
	}
	float behind = clamp(-d, 0.0, 1.0);
	float band = smoothstep(0.0, 0.015, behind) * (1.0 - smoothstep(0.03, 0.12, behind));
	vec3 white = vec3(0.95, 0.98, 1.0), cyan = vec3(0.35, 0.8, 1.0), violet = vec3(0.6, 0.32, 1.0);
	vec3 light = (white * front * 1.5 + violet * fringe * 0.8 + cyan * (inner * 0.7 + echo)) * fade * power;
	float shade = band * 0.35 * fade * power;
	float clip = 1.0 - smoothstep(0.7, 1.0, r);
	COLOR = vec4(light * clip, shade * clip);
}" };

	/// <summary>
	/// 一道波纹（直径 size），返回根节点和材质（调用方每帧设 prog）。
	/// axis = null：圈在地面上（竖向压扁 squash，贴地的那道）；axis = 弹道方向：圈立在垂直于弹道的平面上
	/// （用户 2026-11-19：三维方向上的子弹怎么打出二维平面上的波纹）——从侧面看是一个顺着弹道压扁的窄椭圆，扩开的同时被子弹往前带 drift。
	/// </summary>
	private static (Node2D Root, ShaderMaterial Mat) Ripple(Control layer, Vector2 at, float size, float squash, float power, double life, Vector2? axis = null, float drift = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5SinfulRipple", life);
		if (axis is Vector2 ax) root.Rotation = ax.Angle() + Mathf.Pi / 2f;   // 局部 y 轴对着弹道（局部 -y = 子弹前进的方向）
		ShaderMaterial m = Quad(root, RippleShader, Vector2.One * size, -Vector2.One * size / 2f);
		m.SetShaderParameter("squash", squash);
		m.SetShaderParameter("power", power);
		m.SetShaderParameter("drift", drift);
		m.SetShaderParameter("prog", 0f);
		return (root, m);
	}

	/// <summary>后面几个敌人被穿过时的一道立在弹道上的波纹（游戏时间里自己走完）。</summary>
	private static void RipplePulse(Control layer, Vector2 at, Vector2 axis, float size, float power, float life)
	{
		(Node2D root, ShaderMaterial m) = Ripple(layer, at, size, 0.3f, power, life + 0.05, axis, 0.18f);
		Fx.Animate(root, 0, life, t => m.SetShaderParameter("prog", t));
	}

	private static ShaderMaterial Quad(Node2D root, Shader shader, Vector2 size, Vector2 offset)
	{
		ShaderMaterial m = new() { Shader = shader };
		m.SetShaderParameter("seed", Fx.Rand(0f, 50f));
		root.AddChild(new ColorRect { Size = size, Position = offset, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
		return m;
	}

	/// <summary>开场：蓝焰从 at 整团炸开（直径 size）。</summary>
	private static void FireBurst(Control layer, Vector2 at, float size, float life, float power, float delay = 0f, float spike = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P5SinfulBurst", delay + life + 0.05);
		ShaderMaterial m = Quad(root, BurstShader, Vector2.One * size, -Vector2.One * size / 2f);
		m.SetShaderParameter("power", power);
		m.SetShaderParameter("spike", spike);
		m.SetShaderParameter("prog", 0f);
		Fx.Animate(root, delay, life, t => m.SetShaderParameter("prog", t));
	}

	/// <summary>一团烟：直径 size，life 秒，drift 是整团漂移（四边形半宽为单位），heat 里面的火光。</summary>
	private static void Smoke(Control layer, Vector2 at, float size, float life, Vector2 drift, float heat, float thick = 0.8f, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5SinfulSmoke", delay + life + 0.05);
		ShaderMaterial m = Quad(root, SmokeShader, Vector2.One * size, -Vector2.One * size / 2f);
		m.SetShaderParameter("drift", drift);
		m.SetShaderParameter("heat", heat);
		m.SetShaderParameter("thick", thick);
		m.SetShaderParameter("prog", 0f);
		root.Visible = delay <= 0f;
		Fx.Animate(root, delay, life, t => { root.Visible = true; m.SetShaderParameter("prog", t); });
	}

	/// <summary>弹道的烟道：跟着弹头（速度 v 像素 / 秒）铺开，life 秒里散掉。</summary>
	private static void Tunnel(Control layer, Vector2 muzzle, Vector2 dir, float reach, float v, float thick, float life)
	{
		Node2D root = Fx.Root(layer, muzzle, "P5SinfulTunnel", life + 0.1);
		root.Rotation = dir.Angle();
		ShaderMaterial m = Quad(root, TunnelShader, new Vector2(reach, thick), new Vector2(0f, -thick / 2f));
		m.SetShaderParameter("len_px", reach);
		m.SetShaderParameter("life", life);
		Fx.Animate(root, 0, life, t =>
		{
			float age = t * life;
			m.SetShaderParameter("age", age);
			m.SetShaderParameter("head", Mathf.Min(1.05f, age * v / reach));
		});
	}


	/// <summary>
	/// 打中往后一震：先瞬间推出去一截（顿帧里就看得到身体被打歪），顿帧过后推满，停一下，再慢慢回到原位。
	/// 只动 Position，动画完 / 场景提前结束都会放回原位（游戏的 AnimShake 也是从 0 开始写 Position）。
	/// </summary>
	private static void KnockBack(Node2D body, Vector2 offset, Node lifetime)
	{
		if (!GodotObject.IsInstanceValid(body)) return;
		Tween tw = BodyRest.Knock(body, out Vector2 home);
		body.Position = home + offset * 0.55f;
		tw.TweenProperty(body, "position", home + offset, 0.06).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tw.TweenInterval(0.07);
		tw.TweenProperty(body, "position", home, 0.55).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		BodyRest.EndKnock(tw, body);
		lifetime.TreeExiting += () =>
		{
			if (GodotObject.IsInstanceValid(body)) BodyRest.Abort(tw, body);
		};
	}
}
