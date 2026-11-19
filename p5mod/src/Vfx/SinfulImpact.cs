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
	private static Shader? _burst, _smoke, _tunnel, _exit;

	/// <summary>开场的蓝焰爆发：整团往外炸的火（火舌被噪声撕开、外沿一层更亮的壳），白热的芯，prog 0→1 炸开再散掉。加法。</summary>
	private static Shader BurstShader => _burst ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float prog = 0.0;
uniform float seed = 0.0;
uniform float power = 1.0;
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), ang = atan(p.y, p.x);
	float e = 1.0 - pow(1.0 - prog, 3.0);
	// 火舌：角度方向的噪声顺着半径拉长（往外窜），外沿参差不齐
	float tongue = fbm(vec2(ang * 3.2 + seed, r * 1.4 - prog * 5.0));
	float R = (0.08 + 0.86 * e) * (0.5 + 0.95 * tongue);
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
	COLOR = vec4(col * a * (1.0 - smoothstep(0.88, 1.0, r)), 1.0);
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
	float a = clamp(dens * thick, 0.0, 0.92) * (1.0 - smoothstep(0.85, 1.0, length(p)));
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
	float a = clamp(prof * (0.5 + 0.6 * n) * dissolve * on * 1.3, 0.0, 0.9) * (1.0 - smoothstep(0.85, 1.0, abs(y)));
	vec3 smoke = mix(vec3(0.15, 0.19, 0.28), vec3(0.33, 0.4, 0.54), smoothstep(0.3, 0.8, n2));   // 被月光照着的烟，比夜空亮
	vec3 lit = vec3(0.3, 0.7, 1.0) * exp(-age / 0.5) * prof * 1.3;
	// 刚被子弹烧过的那一下：一层宽而软的余热（不是一根细亮线），很快冷掉
	float core = exp(-pow(y / (0.18 + 0.2 * k), 2.0)) * exp(-age / 0.18) * on * (0.6 + 0.4 * n);
	vec3 light = vec3(0.3, 0.72, 1.0) * core * 0.55;
	COLOR = vec4((smoke + lit) * a + light, a);
}" };

	/// <summary>
	/// 贯穿：子弹打穿敌人，从它身后顺着弹道炸出去的一大股蓝焰（锥形，越往前越宽，前沿被噪声撕开），根部白热。
	/// 四边形从命中点往弹道方向铺（UV.x 0.08 = 命中点），prog 0→1 冲出去再散掉。加法。
	/// </summary>
	private static Shader ExitShader => _exit ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float prog = 0.0;
uniform float seed = 0.0;
uniform float power = 1.0;
" + Noise + @"
void fragment() {
	float x = UV.x * 1.08 - 0.08, y = (UV.y - 0.5) * 2.0;
	float e = 1.0 - pow(1.0 - prog, 3.0);
	float reach = 0.12 + 0.88 * e;
	float n = fbm(vec2(x * 3.6 - prog * 6.0 + seed, y * 2.4));
	float n2 = fbm(vec2(x * 8.0 - prog * 10.0, y * 5.0 + seed));
	float hw = 0.1 + 0.62 * max(x, 0.0) + 0.12 * (n - 0.5);
	float inCone = 1.0 - smoothstep(hw * 0.45, hw, abs(y));
	float front = 1.0 - smoothstep(reach * (0.75 + 0.3 * n), reach * (1.0 + 0.25 * n), x);
	float body = inCone * front * (0.35 + 0.65 * n2) * step(0.0, x);
	float hot = exp(-length(vec2(x / (0.05 + 0.08 * e), y / 0.22))) * (1.0 - 0.6 * e);
	float fade = pow(1.0 - prog, 1.3);
	float heat = body * (1.25 - x) + hot * 1.8;
	vec3 deep = vec3(0.07, 0.2, 0.7), cyan = vec3(0.3, 0.76, 1.0), white = vec3(0.95, 0.98, 1.0);
	vec3 col = mix(deep, cyan, smoothstep(0.1, 0.6, heat));
	col = mix(col, white, smoothstep(0.8, 1.4, heat));
	float a = (body * (1.0 - 0.45 * x) + hot) * fade * power;
	float edge = smoothstep(0.0, 0.04, UV.x) * (1.0 - smoothstep(0.9, 1.0, UV.x)) * (1.0 - smoothstep(0.82, 1.0, abs(y)));
	COLOR = vec4(col * a * edge, 1.0);
}" };

	private static ShaderMaterial Quad(Node2D root, Shader shader, Vector2 size, Vector2 offset)
	{
		ShaderMaterial m = new() { Shader = shader };
		m.SetShaderParameter("seed", Fx.Rand(0f, 50f));
		root.AddChild(new ColorRect { Size = size, Position = offset, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
		return m;
	}

	/// <summary>开场：蓝焰从 at 整团炸开（直径 size）。</summary>
	private static void FireBurst(Control layer, Vector2 at, float size, float life, float power, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5SinfulBurst", delay + life + 0.05);
		ShaderMaterial m = Quad(root, BurstShader, Vector2.One * size, -Vector2.One * size / 2f);
		m.SetShaderParameter("power", power);
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

	/// <summary>贯穿一个敌人：从 at 顺着 dir 炸出去的一股蓝焰（长 length）。</summary>
	private static void ExitBurst(Control layer, Vector2 at, Vector2 dir, float length, float life, float power)
	{
		Node2D root = Fx.Root(layer, at, "P5SinfulExit", life + 0.05);
		root.Rotation = dir.Angle();
		float h = length * 0.95f;
		ShaderMaterial m = Quad(root, ExitShader, new Vector2(length, h), new Vector2(-length * 0.08f, -h / 2f));
		m.SetShaderParameter("power", power);
		m.SetShaderParameter("prog", 0f);
		Fx.Animate(root, 0, life, t => m.SetShaderParameter("prog", t));
	}

	/// <summary>
	/// 打中往后一震：先瞬间推出去一截（顿帧里就看得到身体被打歪），顿帧过后推满，停一下，再慢慢回到原位。
	/// 只动 Position，动画完 / 场景提前结束都会放回原位（游戏的 AnimShake 也是从 0 开始写 Position）。
	/// </summary>
	private static void KnockBack(Node2D body, Vector2 offset, Node lifetime)
	{
		if (!GodotObject.IsInstanceValid(body)) return;
		Vector2 home = body.Position;
		body.Position = home + offset * 0.55f;
		Tween tw = body.CreateTween();
		tw.TweenProperty(body, "position", home + offset, 0.06).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		tw.TweenInterval(0.07);
		tw.TweenProperty(body, "position", home, 0.55).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
		lifetime.TreeExiting += () =>
		{
			if (GodotObject.IsInstanceValid(body) && tw.IsValid() && tw.IsRunning())
			{
				tw.Kill();
				body.Position = home;
			}
		};
	}
}
