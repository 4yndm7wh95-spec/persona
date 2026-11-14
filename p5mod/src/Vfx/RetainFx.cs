using Godot;

namespace P5Joker;

/// <summary>
/// 回合结束时被保留在手里的牌（计划妥当、带保留的牌）：一张张从左到右「封」一下（用户 2026-11-14：每回合都会出现，不要太花哨，但要好看）。
/// 每张牌：一圈红色柔光描边从牌边亮起来、边上一层网点光晕，一道白色高光斜着扫过牌面，然后柔柔地收掉；牌顶上冒几粒光点。
/// 整次只响一声很轻的叠牌声。约 0.9s，每张错开 0.07s。
/// </summary>
public static class RetainFx
{
	private static Shader? _shader;

	private static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec2 card = vec2(300.0, 422.0);   // 牌身像素
uniform float margin = 40.0;
uniform float t = 0.0;                     // 0 → 1
uniform float seed = 0.0;
float box(vec2 p, vec2 b, float r) { vec2 q = abs(p) - b + r; return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r; }
void fragment() {
	vec2 full = card + 2.0 * margin;
	vec2 p = UV * full - full * 0.5;
	float d = box(p, card * 0.5, 22.0);
	float env = smoothstep(0.0, 0.18, t) * (1.0 - smoothstep(0.55, 1.0, t));
	float flash = exp(-max(t - 0.08, 0.0) * 9.0) * smoothstep(0.0, 0.08, t);
	vec3 red = vec3(0.95, 0.08, 0.16);
	// 外圈柔光 + 贴边亮线（柔的，不是细线）
	float ang = atan(p.y, p.x);
	float vary = 0.55 + 0.45 * sin(ang * 3.0 + seed * 6.0 + t * 5.0) * sin(ang * 5.0 - seed * 3.0);
	float outer = exp(-max(d, 0.0) / 20.0) * step(-1.0, d) * (0.6 + 0.4 * vary);
	float edge = exp(-abs(d + 3.0) / (7.0 + 4.0 * flash)) * (0.45 + 0.55 * vary);
	// 外圈网点：越靠边越大
	vec2 g = p / 9.0;
	vec2 cell = fract(g) - 0.5;
	float rr = 0.42 * exp(-max(d, 0.0) / 18.0) * step(0.0, d);
	float dots = smoothstep(rr, rr - 0.12, length(cell)) * step(0.0, d);
	// 牌面里一层淡淡的红 + 一道斜着扫过的白高光
	float inside = smoothstep(1.0, -2.0, d);
	float x = (p.x / card.x + p.y / card.y * 0.45);
	float sweep = mix(-0.9, 0.9, smoothstep(0.05, 0.55, t));
	float sheen = exp(-pow((x - sweep) / 0.08, 2.0)) * inside;
	vec3 col = red * (outer * 0.55 + dots * 0.5) + mix(red, vec3(1.0, 0.85, 0.85), 0.25 + 0.6 * flash) * edge * 0.7
		+ red * inside * 0.12 + vec3(1.0, 0.95, 0.93) * sheen * 0.45;
	float a = clamp(outer * 0.45 + dots * 0.4 + edge * 0.6 + inside * 0.1 + sheen * 0.35, 0.0, 1.0);
	col *= env;
	a *= env;
	if (a <= 0.003) discard;
	COLOR = vec4(col, a);
}" };

	/// <summary>在一张牌的牌身（body）上「封」一下：delay 秒后开始。</summary>
	public static void Play(Control body, float delay)
	{
		if (!GodotObject.IsInstanceValid(body)) return;
		Vector2 size = body.Size.X > 10f ? body.Size : new Vector2(300f, 422f);
		const float margin = 40f, life = 0.9f;
		ShaderMaterial m = new() { Shader = Shader };
		m.SetShaderParameter("card", size);
		m.SetShaderParameter("margin", margin);
		m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
		m.SetShaderParameter("t", 0f);
		ColorRect r = new()
		{
			Name = "P5Retain", Size = size + Vector2.One * margin * 2f, Position = -Vector2.One * margin,
			Material = m, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 1,
		};
		body.AddChild(r);
		Fx.FreeAfter(r, delay + life + 0.1);
		Fx.Animate(r, delay, life, t => m.SetShaderParameter("t", t));
		Fx.After(body, delay + 0.1f, () =>
		{
			if (!GodotObject.IsInstanceValid(body)) return;
			Glow.Glint(body, body.GetGlobalTransform() * new Vector2(size.X * 0.88f, size.Y * 0.06f), 0.5f);
			Glow.Sparks(body, body.GetGlobalTransform() * new Vector2(size.X * 0.5f, size.Y * 0.1f), 7, 40f, 130f, 0.6f,
				new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.25f, 0.32f) }, -60f, 0.1f, 0.22f, Vector2.Up, 70f);
		});
	}
}
