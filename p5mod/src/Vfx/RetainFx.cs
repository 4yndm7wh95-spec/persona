using Godot;

namespace P5Joker;

/// <summary>
/// 回合结束时被保留在手里的牌（计划妥当、带保留的牌）：怪盗给这张牌「做记号」（2026-11-15 重做；用户：从左到右闪白高光太 low、没特色）。
/// 每张牌：一张小预告信从上面转着飞下来，斜着「钉」在牌的右上角 —— 钉上的一下牌边亮起一圈红光、角上一闪、几粒碎光；
/// 小卡钉着停一会儿，然后从切口烧掉成纸屑，牌边的红光跟着收。每回合都有，所以小、快（约 1s，每张错开 0.08s）。
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
	// 牌面里一层淡淡的红 + 一道斜着扫过的白高光
	float inside = smoothstep(1.0, -2.0, d);
	float x = (p.x / card.x + p.y / card.y * 0.45);
	float sweep = mix(-0.9, 0.9, smoothstep(0.05, 0.55, t));
	float sheen = exp(-pow((x - sweep) / 0.08, 2.0)) * inside;
	vec3 col = red * outer * 0.7 + mix(red, vec3(1.0, 0.85, 0.85), 0.25 + 0.6 * flash) * edge * 0.7
		+ red * inside * 0.1;
	float a = clamp(outer * 0.55 + edge * 0.6 + inside * 0.08, 0.0, 1.0);
	col *= env;
	a *= env;
	if (a <= 0.003) discard;
	COLOR = vec4(col, a);
}" };

	/// <summary>在一张牌的牌身（body）上做记号：delay 秒后开始。</summary>
	public static void Play(Control body, float delay)
	{
		if (!GodotObject.IsInstanceValid(body)) return;
		Vector2 size = body.Size.X > 10f ? body.Size : new Vector2(300f, 422f);
		const float margin = 40f, fly = 0.2f, hold = 0.38f, burn = 0.3f;
		float impact = delay + fly, glowLife = hold + burn + 0.2f;
		// 牌边的红光：钉上那一下亮起，跟着小卡烧掉收掉
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
		Fx.FreeAfter(r, impact + glowLife + 0.1);
		Fx.Animate(r, impact, glowLife, t => m.SetShaderParameter("t", t));
		// 小预告信（坐标都按牌身的本地像素算，再换成全局）
		Transform2D xf = body.GetGlobalTransform();
		float cw = size.X * 0.36f;
		Vector2 pinL = new(size.X * 0.84f, size.Y * 0.1f), fromL = pinL + new Vector2(-size.X * 0.55f, -size.Y * 0.75f);
		float rot0 = body.GetGlobalTransform().Rotation, tilt = 0.55f;
		Phantom.P5Card card = Phantom.Card(body, xf * fromL, cw, delay + fly + hold + burn + 0.2, 0.32f);
		card.Rect.ZIndex = 2;
		card.Root.Visible = false;
		Vector2 ctlL = (fromL + pinL) * 0.5f + new Vector2(size.X * 0.3f, -size.Y * 0.1f);
		Fx.Animate(card.Root, delay, fly, t =>
		{
			if (!GodotObject.IsInstanceValid(body)) return;
			card.Root.Visible = true;
			Transform2D x = body.GetGlobalTransform();
			float e = t * t * (3f - 2f * t);
			Vector2 l = fromL.Lerp(ctlL, e).Lerp(ctlL.Lerp(pinL, e), e);
			card.Place(x * l, x.Rotation + Mathf.Lerp(-2.4f, tilt, e), Mathf.Pi + (1f - e) * 9f, 1f);
		});
		Fx.After(body, impact, () =>
		{
			if (!GodotObject.IsInstanceValid(body)) return;
			Transform2D x = body.GetGlobalTransform();
			Vector2 pin = x * pinL;
			card.Trail?.Kill();
			// 钉着：轻轻颤一下，停住
			Fx.Animate(card.Root, 0, hold, t =>
			{
				if (!GodotObject.IsInstanceValid(body)) return;
				Transform2D x2 = body.GetGlobalTransform();
				float wob = Mathf.Sin(t * 30f) * Mathf.Exp(-t * 9f) * 0.12f;
				card.Place(x2 * pinL, x2.Rotation + tilt + wob, Mathf.Pi, 1f);
				card.Set("glow", 0.6f * Mathf.Exp(-t * 8f));
			});
			Glow.Flash(body, pin, new Color(1f, 0.93f, 0.92f), size.X * 0.55f, 0.12f);
			Glow.Glint(body, pin + x.BasisXform(new Vector2(-size.X * 0.1f, size.Y * 0.05f)), 0.5f);
			Glow.Sparks(body, pin, 9, 60f, 200f, 0.45f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.25f, 0.32f) }, 80f, 0.12f, 0.26f, Vector2.Up, 90f);
		});
		// 烧掉成纸屑
		Fx.After(body, impact + hold, () =>
		{
			if (!GodotObject.IsInstanceValid(body)) return;
			card.Set("clip", new Vector3(0.6f, 0.8f, -1.6f));
			Fx.Animate(card.Root, 0, burn, t =>
			{
				if (!GodotObject.IsInstanceValid(body)) return;
				Transform2D x2 = body.GetGlobalTransform();
				card.Place(x2 * (pinL + new Vector2(size.X * 0.04f, -size.Y * 0.05f) * t), x2.Rotation + tilt + t * 0.3f, Mathf.Pi, 1f);
				card.Set("burn", t * 1.3f);
			});
			Transform2D x = body.GetGlobalTransform();
			Phantom.Confetti(body, x * pinL, 7, 160f, Vector2.Up, 70f, 0.7f, 5f);
		});
		Fx.After(body, impact + hold + burn, () => { if (GodotObject.IsInstanceValid(card.Root)) card.Root.Visible = false; });
	}
}
