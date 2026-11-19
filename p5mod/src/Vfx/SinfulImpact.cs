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
	private static Shader? _burst, _smoke, _tunnel, _pillar;

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
	// 第三版（用户：偏软、像打出了一团烟雾）：烟道不再发光，只是子弹过去以后留下的一条暗烟，薄、很快散
	float a = clamp(prof * (0.45 + 0.6 * n) * dissolve * on * 0.75, 0.0, 0.7) * (1.0 - smoothstep(0.85, 1.0, abs(y)));
	vec3 smoke = mix(vec3(0.1, 0.12, 0.18), vec3(0.24, 0.28, 0.38), smoothstep(0.3, 0.8, n2));
	COLOR = vec4(smoke * a, a);
}" };

	/// <summary>
	/// 引爆时冲起来的一根蓝焰火柱（用户 2026-11-19：能量弹打到敌人那会儿炸开要更明显、更华丽）：
	/// 前 45% 的时间从底下猛地冲上去，火身越往上越细、被低频噪声推得左右摆，中间白热、往外是青和深蓝，底下一圈亮；
	/// 用低频噪声和平滑过渡（大火柱用高频硬边会碎成一条条细刺）。UV.y = 1 是底。加法。
	/// </summary>
	private static Shader PillarShader => _pillar ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float prog = 0.0;
uniform float seed = 0.0;
uniform float power = 1.0;
" + Noise + @"
void fragment() {
	// 第二版（自查：第一版在画面上是一根光滑的高三角锥，几何感重）：底宽、身上一鼓一鼓、顶上碎成一缕缕往上窜的火舌，里面有往上走的亮暗
	vec2 p = vec2((UV.x - 0.5) * 2.0, 1.0 - UV.y);
	float rise = 1.0 - pow(1.0 - min(prog * 2.2, 1.0), 3.0);
	float hgt = 0.2 + 0.8 * rise;
	float top = p.y / max(hgt, 0.01);
	float n = fbm(vec2(p.x * 1.3 + seed, p.y * 1.8 - prog * 5.0));
	float n2 = fbm(vec2(p.x * 2.6 - seed, p.y * 3.0 - prog * 7.0));
	float n3 = fbm(vec2(p.x * 4.0 + seed * 2.0, p.y * 2.2 - prog * 9.0));
	float wob = (n - 0.5) * 0.55 * p.y;
	float wdt = mix(0.78, 0.26, clamp(top, 0.0, 1.0)) * (0.65 + 0.7 * n2);
	float d = abs(p.x - wob) / max(wdt, 0.01);
	float body = 1.0 - smoothstep(0.3, 1.0, d);
	float cap = 1.0 - smoothstep(0.55, 1.0, top + (n2 - 0.5) * 0.55);
	float licks = smoothstep(0.32, 0.62, n3 + (1.0 - top) * 0.9);        // 越往上越碎成一缕缕
	float inside = body * cap * licks;
	float bands = 0.7 + 0.5 * fbm(vec2(p.x * 2.0, p.y * 5.0 - prog * 14.0));
	float core = (1.0 - smoothstep(0.0, 0.5, d)) * (1.0 - smoothstep(0.25, 0.8, top)) * licks;
	float base = exp(-p.y * 6.0) * (1.0 - smoothstep(0.35, 1.0, abs(p.x)));
	float fade = 1.0 - smoothstep(0.5, 1.0, prog);
	float heat = inside * (0.5 + 0.5 * n2) * bands + core * 0.8;
	vec3 deep = vec3(0.06, 0.18, 0.75), cyan = vec3(0.3, 0.75, 1.0), white = vec3(0.95, 0.98, 1.0);
	vec3 col = mix(deep, cyan, smoothstep(0.15, 0.65, heat));
	col = mix(col, white, smoothstep(0.85, 1.4, heat));
	float a = (inside * (0.5 + 0.5 * n2) * bands + core * 0.7 + base * 0.6) * fade * power;
	COLOR = vec4(col * a * (1.0 - smoothstep(0.85, 1.0, abs(p.x))), 1.0);
}" };

	/// <summary>一根蓝焰火柱：底在 basePt，宽 w、高 h（像素），life 秒。</summary>
	private static void Pillar(Control layer, Vector2 basePt, float w, float h, float life, float power, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, basePt, "P5SinfulPillar", delay + life + 0.05);
		ShaderMaterial m = Quad(root, PillarShader, new Vector2(w, h), new Vector2(-w / 2f, -h));
		m.SetShaderParameter("power", power);
		m.SetShaderParameter("prog", 0f);
		root.Visible = delay <= 0f;
		Fx.Animate(root, delay, life, t => { root.Visible = true; m.SetShaderParameter("prog", t); });
	}

	/// <summary>
	/// 连环引爆的一处（华丽收场第四版）：子弹穿过所有敌人以后，沿弹道一个接一个炸开——一团大的蓝焰炸开 + 往上冲起一根蓝焰火柱 + 往上喷的火星。
	/// big = 最后一个（最大）。
	/// </summary>
	private static void Detonate(Control layer, Vector2 at, float unit, bool big)
	{
		float k = big ? 1.3f : 1f;
		FireBurst(layer, at, 1000f * unit * k, 0.5f, 1.35f, 0f, 0.35f);
		Pillar(layer, at + new Vector2(0f, 70f * unit), 430f * unit * k, 1050f * unit * k, 0.75f, 1.2f, 0.03f);
		Glow.Flash(layer, at, new Color(0.7f, 0.9f, 1f), 420f * unit * k, 0.18f);
		Glow.Sparks(layer, at, big ? 30 : 20, 700f, 1900f, 0.6f, new[] { new Color(0.95f, 0.98f, 1f), new Color(0.35f, 0.75f, 1f), new Color(0.15f, 0.35f, 0.95f) },
			500f, 0.1f, 0.3f, Vector2.Up, 70f);
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

	/// <summary>朝 dir 拉长的一团火舌炸开（枪口、打中）：开场那种炸开，按 stretch 拉长、往前偏一点。</summary>
	private static void FireBurstDir(Control layer, Vector2 at, Vector2 dir, float size, float life, float power, Vector2 stretch, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at + dir * size * stretch.X * 0.18f, "P5SinfulBurstDir", delay + life + 0.05);
		root.Rotation = dir.Angle();
		root.Scale = stretch;
		ShaderMaterial m = Quad(root, BurstShader, Vector2.One * size, -Vector2.One * size / 2f);
		m.SetShaderParameter("power", power);
		m.SetShaderParameter("prog", 0f);
		Fx.Animate(root, delay, life, t => m.SetShaderParameter("prog", t));
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
