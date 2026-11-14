using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 幽魂形态第五版「水中月」（2026-11-14，用户挑的方案 A，要更华丽）。
/// 主意象：他成了水里的倒影，刀砍过去只荡开波纹。
/// ① 0 四周压暗，身后一轮红白的月从脚下的「水面」升起，脚下是它被水波揉碎的倒影；人褪成半透明的月白；
/// ② 0.34 / 0.46 / 0.6 三道弯刀光从敌人那边砍来、直接穿过他：每一刀他整个人像水面一样横向荡开（读屏），水珠顺着刀势溅出去；
/// ③ 0.62 主拍：第三刀最大，月亮亮一下、倒影碎成一片粼光、水珠往上扬；
/// ④ 之后他带着残余的微波站着，光粒往上飘，月亮慢慢淡掉。
/// </summary>
public static partial class SkillRedo5
{
	private static Shader? _moon, _moonRefl, _waterBody, _slash;

	private const string Noise = @"
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), f.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), f.x), f.y); }
float fbm(vec2 p) { float a = 0.5, s = 0.0; for (int i = 0; i < 4; i++) { s += a * vn(p); p = p * 2.03 + 7.1; a *= 0.5; } return s; }
";

	/// <summary>月：p 以半径为单位。waterline（半径单位，相对月心往下为正）以下不画，水线上一道亮边。</summary>
	private static Shader Moon => _moon ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float alpha = 1.0;
uniform float pulse = 0.0;
uniform float waterline = 9.0;
uniform float clock = 0.0;
uniform float seed = 0.0;
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 3.2;
	float d = length(p);
	float disc = smoothstep(1.0, 0.9, d);
	float mott = fbm(p * 3.4 + seed * 3.0 + vec2(clock * 0.03, 0.0));
	float mare = smoothstep(0.46, 0.78, mott) * 0.6 + smoothstep(0.5, 0.62, fbm(p * 7.0 + seed)) * 0.25;
	vec3 pale = vec3(1.0, 0.9, 0.88), rose = vec3(1.0, 0.3, 0.36), deep = vec3(0.75, 0.04, 0.12);
	vec3 col = mix(pale, rose, mare * 0.6);
	float limb = smoothstep(0.35, 1.0, d);
	col = mix(col, deep, limb * 0.55);
	col *= 0.62 + 0.38 * smoothstep(1.2, -0.6, p.x * 0.6 + p.y);   // 左上亮、右下暗一点，有体积
	float halo = exp(-max(0.0, d - 0.9) * 3.0) * (1.0 - disc);
	float rays = 0.75 + 0.25 * vn(vec2(atan(p.y, p.x) * 5.0, clock * 0.4 + seed));
	vec3 outc = col * disc * (1.0 + 0.22 * pulse) + vec3(1.0, 0.1, 0.18) * halo * rays * (0.5 + 0.7 * pulse);
	float wl = p.y - waterline;
	float wob = sin(p.x * 14.0 + clock * 5.0) * 0.012;
	float above = smoothstep(0.02, -0.03, wl + wob);
	float lip = exp(-abs(wl + wob) * 14.0) * smoothstep(1.1, 0.3, abs(p.x));
	outc = outc * above + vec3(1.0, 0.45, 0.5) * lip * 0.22;
	COLOR = vec4(outc * alpha, 1.0);
}" };

	/// <summary>月的倒影：矩形顶边 = 水线。moon_h：月心离水线的高度（半径单位，可以为负 = 还没出水）。</summary>
	private static Shader MoonRefl => _moonRefl ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float alpha = 1.0;
uniform float pulse = 0.0;
uniform float moon_h = 1.0;
uniform float clock = 0.0;
uniform float stir = 0.0;     // 被搅动：越大越碎
uniform float seed = 0.0;
uniform vec2 span = vec2(3.2, 1.0);   // 矩形宽、高（半径单位）
" + Noise + @"
void fragment() {
	float depth = UV.y * span.y;
	float x = (UV.x - 0.5) * span.x;
	float squash = 0.34;
	float wob = sin(depth * 46.0 - clock * 5.0 + seed) * (0.035 + 0.11 * depth) * (1.0 + 2.2 * stir)
		+ sin(depth * 19.0 + clock * 3.1) * 0.03 * (1.0 + stir);
	vec2 p = vec2(x + wob, depth / squash - moon_h);
	float d = length(p);
	float disc = smoothstep(1.0, 0.9, d);
	float band = vn(vec2(x * 1.6 + seed * 7.0, depth * 54.0 - clock * 2.2));
	float broken = smoothstep(0.28 + 0.3 * stir, 0.72 + 0.2 * stir, band);
	vec3 col = mix(vec3(1.0, 0.82, 0.82), vec3(1.0, 0.16, 0.24), smoothstep(0.2, 1.0, d));
	float body = disc * (0.25 + 0.75 * broken);
	// 粼光：稀疏的横向亮点
	float gx = vn(vec2(x * 9.0 + clock * 0.7, depth * 120.0 + seed * 3.0));
	float glint = smoothstep(0.82 - 0.2 * pulse, 0.95, gx) * smoothstep(1.5, 0.4, abs(x)) * smoothstep(0.0, 0.06, depth);
	float halo = exp(-max(0.0, d - 0.9) * 2.6) * (1.0 - disc) * (0.3 + 0.7 * broken);
	float fade = smoothstep(span.y, span.y * 0.35, depth) * smoothstep(0.0, 0.025, depth);
	vec3 outc = col * body * (0.55 + 0.6 * pulse) + vec3(1.0, 0.08, 0.16) * halo * 0.4 + vec3(1.0, 0.9, 0.9) * glint * (0.35 + 0.9 * pulse);
	COLOR = vec4(outc * fade * alpha, 1.0);
}" };

	/// <summary>把他那一块画面当水面揉：按行横向错开（读屏），波峰上带一点月白的高光。box：屏幕 UV 的范围（椭圆软边）。</summary>
	private static Shader WaterBody => _waterBody ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D scr : hint_screen_texture, filter_linear;
uniform vec4 box = vec4(0.0, 0.0, 1.0, 1.0);
uniform float amp = 0.0;      // 屏幕 UV
uniform float clock = 0.0;
uniform float cut_y = 0.5;    // 刀过的那一行（屏幕 UV）
uniform float cut_k = 0.0;    // 刀痕处额外的一圈扩散波
uniform float cut_t = 0.0;
void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 c = (box.xy + box.zw) * 0.5, hs = (box.zw - box.xy) * 0.5;
	vec2 q = (uv - c) / hs;
	float m = smoothstep(1.0, 0.55, length(q));
	if (m <= 0.001) discard;
	float yy = uv.y / hs.y;
	float w1 = sin(yy * 21.0 - clock * 9.0), w2 = sin(yy * 9.0 + clock * 5.3);
	float dist = abs(uv.y - cut_y) / hs.y;
	float ring = sin((dist - cut_t * 1.6) * 26.0) * exp(-dist * 2.2) * cut_k;
	float dx = (w1 * 0.65 + w2 * 0.35 + ring * 1.4) * amp * m;
	vec3 col = texture(scr, uv + vec2(dx, 0.0)).rgb;
	float crest = pow(max(0.0, w1 * 0.6 + ring * 0.8 + 0.2), 6.0) * clamp(amp * 180.0, 0.0, 1.0) * m;
	col += vec3(1.0, 0.62, 0.66) * crest * 0.22;
	COLOR = vec4(col, 1.0);
}" };

	/// <summary>一道弯刀光：沿 UV.x 走，head / tail 控制露出的一段；中间厚两头尖，白芯红边。</summary>
	private static Shader Slash => _slash ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float head = 0.0;
uniform float tail = 0.0;
uniform float alpha = 1.0;
uniform float bend = 0.5;
uniform float seed = 0.0;
" + Noise + @"
void fragment() {
	float x = UV.x, yy = (UV.y - 0.5) * 2.0;
	float arc = bend * (1.0 - 4.0 * (x - 0.5) * (x - 0.5)) - bend * 0.5;
	float e = yy - arc;
	float span = max(0.001, head - tail);
	float k = clamp((x - tail) / span, 0.0, 1.0);
	float vis = step(tail, x) * step(x, head);
	float w = 0.34 * pow(sin(3.14159 * k), 0.75) * (0.55 + 0.45 * k);
	float side = e < 0.0 ? 1.0 : 0.45;     // 外沿利、内沿拖一点
	float body = smoothstep(w, w * 0.25, abs(e) * side);
	float core = smoothstep(w * 0.32, 0.0, abs(e));
	float fib = 0.7 + 0.3 * vn(vec2(x * 30.0 + seed * 9.0, e * 9.0));
	float soft = exp(-abs(e) / max(0.02, w) * 1.6) * 0.35;
	vec3 col = mix(vec3(1.0, 0.1, 0.18), vec3(1.0, 0.96, 0.94), core);
	float a = (body * fib + soft) * vis * alpha;
	COLOR = vec4(col * a, 1.0);
}" };

	public static void WraithFormV3(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float life = 2.1f, beat = 0.62f;
		float[] cuts = { 0.34f, 0.46f, 0.6f };
		Sfx.Play(Sfx.WraithForm, 0.55f);
		S("gx_slice", 0.16f, cuts[0] - 0.02f);
		S("gx_slice", 0.24f, cuts[2] - 0.03f);
		S("gx_ping", 0.12f, beat + 0.03f);
		Vector2 feet = c.Feet, chest = c.Chest;
		float R = h * 0.62f;
		Vector2 water = feet + new Vector2(0f, h * 0.015f);
		Vector2 moonAt = feet + new Vector2(-f * h * 0.1f, -h * 0.82f);

		Dim(back, chest + new Vector2(0f, -h * 0.1f), new Vector2(h * 4.2f, h * 3f), life,
			s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.3f)) * (1f - Ease5.Seg(s, life - 0.6f, 0.6f)));

		float Pulse(float s) => s > beat ? Mathf.Exp(-(s - beat) * 3.2f) : 0f;
		float Fade(float s) => Fx.OutCubic(Ease5.Seg(s, 0.02f, 0.35f)) * (1f - Ease5.Seg(s, life - 0.75f, 0.7f));
		float Rise(float s) => Ease5.OutQuint(Ease5.Seg(s, 0.02f, 0.62f));
		// 月心离水线的高度（半径单位）：从水下升到位
		float MoonH(float s) => Mathf.Lerp(-0.9f, (water.Y - moonAt.Y) / R, Rise(s));
		float Stir(float s)
		{
			float k = 0f;
			foreach (float t0 in cuts) if (s > t0) k = Mathf.Max(k, Mathf.Exp(-(s - t0) * 2.6f));
			return k;
		}

		// ① 月
		{
			float size = R * 3.2f;
			Node2D root = Fx.Root(back, water, "P5WraithMoon", life + 0.05);
			ShaderMaterial m = new() { Shader = Moon };
			m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
			ColorRect rect = new() { Size = new Vector2(size, size), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(rect);
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life, mh = MoonH(s);
				rect.Position = new Vector2(moonAt.X - water.X - size / 2f, -mh * R - size / 2f);
				m.SetShaderParameter("waterline", mh);
				m.SetShaderParameter("alpha", Fade(s) * 0.92f);
				m.SetShaderParameter("pulse", Pulse(s));
				m.SetShaderParameter("clock", s);
			});
		}
		// ① 倒影
		{
			Vector2 span = new(4.2f, 0.95f);
			Node2D root = Fx.Root(back, water, "P5WraithRefl", life + 0.05);
			ShaderMaterial m = new() { Shader = MoonRefl };
			m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
			m.SetShaderParameter("span", span);
			ColorRect rect = new() { Size = span * R, Position = new Vector2(moonAt.X - water.X - span.X * R / 2f, 0f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(rect);
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				m.SetShaderParameter("moon_h", MoonH(s));
				m.SetShaderParameter("alpha", Fade(s));
				m.SetShaderParameter("pulse", Pulse(s));
				m.SetShaderParameter("stir", Stir(s));
				m.SetShaderParameter("clock", s);
			});
		}
		// 人：褪成半透明的月白
		if (c.SelfTint is { } tint)
		{
			Node2D tick = Fx.Root(front, feet, "P5WraithTint", life + 0.05);
			Color ghost = new(1f, 0.84f, 0.88f, 0.6f);
			Fx.Animate(tick, 0, life, t =>
			{
				float s = t * life;
				float k = Ease5.Seg(s, 0.1f, 0.3f) * (1f - Ease5.Seg(s, life - 0.7f, 0.6f));
				float flash = Stir(s) * 0.25f;
				Color col = Colors.White.Lerp(ghost, k);
				col.A = Mathf.Min(1f, col.A + flash * k);
				tint(col);
			});
			tick.TreeExiting += () => tint(Colors.White);
		}
		// ② 水面一样的身体（读屏）
		{
			Node2D root = Fx.ScreenRoot(front, "P5WraithWater");
			Fx.FreeAfter(root, life + 0.05);
			root.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
			ShaderMaterial m = new() { Shader = WaterBody };
			Vector2 scr = Fx.Screen(front);
			ColorRect rect = new() { Size = scr, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(rect);
			Vector2 a = Fx.ScreenPoint(front, feet + new Vector2(-h * 0.62f, -h * 1.22f)), b = Fx.ScreenPoint(front, feet + new Vector2(h * 0.62f, h * 0.12f));
			m.SetShaderParameter("box", new Vector4(a.X / scr.X, a.Y / scr.Y, b.X / scr.X, b.Y / scr.Y));
			float px = 1f / scr.X;
			float[] cutY = { chest.Y + h * 0.05f, chest.Y + h * 0.3f, chest.Y + h * 0.16f };
			Fx.Animate(rect, 0, life, t =>
			{
				float s = t * life;
				float baseAmp = 1.3f * Ease5.Seg(s, 0.12f, 0.3f) * (1f - Ease5.Seg(s, life - 0.7f, 0.6f));
				float hit = 0f, since = 0f, cy = cutY[0];
				for (int i = 0; i < cuts.Length; i++)
				{
					if (s <= cuts[i]) continue;
					float e = Mathf.Exp(-(s - cuts[i]) * (i == 2 ? 2.4f : 4.5f)) * (i == 2 ? 1f : 0.7f);
					if (e >= hit * 0.6f) { since = s - cuts[i]; cy = cutY[i]; }
					hit = Mathf.Max(hit, e);
				}
				m.SetShaderParameter("amp", (baseAmp + 9f * hit) * u * px);
				m.SetShaderParameter("clock", s);
				m.SetShaderParameter("cut_y", Fx.ScreenPoint(front, new Vector2(0f, cy)).Y / scr.Y);
				m.SetShaderParameter("cut_k", hit);
				m.SetShaderParameter("cut_t", since);
			});
		}
		// ② 三道刀光穿过他
		float[] angs = { -0.42f, 0.36f, -0.06f };
		for (int i = 0; i < cuts.Length; i++)
		{
			int k = i;
			bool big = i == 2;
			Vector2 mid = chest + new Vector2(0f, big ? h * 0.16f : (i == 0 ? h * 0.05f : h * 0.3f));
			// 从敌人那边来：方向朝 -f
			float ang = (f > 0 ? Mathf.Pi : 0f) + angs[i] * f;
			float len = h * (big ? 2.3f : 1.8f), thick = h * (big ? 0.42f : 0.3f);
			float dur = big ? 0.3f : 0.24f, t0 = cuts[i] - dur * 0.42f;
			Node2D root = Fx.Root(front, mid, "P5WraithSlash", t0 + dur + 0.15);
			root.Rotation = ang;
			ShaderMaterial m = new() { Shader = Slash };
			m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
			m.SetShaderParameter("bend", (i == 1 ? -0.55f : 0.55f) * f);
			m.SetShaderParameter("alpha", 0f);
			root.AddChild(new ColorRect { Size = new Vector2(len, thick), Position = new Vector2(-len / 2f, -thick / 2f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
			Fx.Animate(root, t0, dur, t =>
			{
				m.SetShaderParameter("head", Ease5.OutQuint(Mathf.Min(1f, t * 1.7f)));
				m.SetShaderParameter("tail", Fx.InCubic(Mathf.Clamp((t - 0.3f) / 0.7f, 0f, 1f)));
				m.SetShaderParameter("alpha", Mathf.Min(1f, t * 8f) * (big ? 1.15f : 0.9f));
			});
			Vector2 dir = Vector2.Right.Rotated(ang);
			Fx.After(front, cuts[i], () =>
			{
				// 刀过身：不是命中的火花，而是水珠顺着刀势溅出去
				Glow.Sparks(front, mid, big ? 46 : 22, 140f * u, (big ? 780f : 520f) * u, big ? 0.8f : 0.55f,
					new[] { new Color(1f, 0.97f, 0.96f), new Color(1f, 0.5f, 0.56f), new Color(0.8f, 0.06f, 0.14f) },
					520f * u, 0.014f * u, (big ? 0.05f : 0.036f) * u, dir + new Vector2(0f, -0.25f), big ? 46f : 30f);
				Glow.Flash(back, mid, new Color(1f, 0.9f, 0.9f), h * (big ? 0.5f : 0.36f), 0.12f);
				for (int j = 0; j < (big ? 5 : 3); j++)
				{
					Vector2 at = water + new Vector2(Fx.Rand(-0.9f, 0.9f) * h, h * Fx.Rand(0.03f, 0.3f));
					Flare(back, at, h * Fx.Rand(0.25f, 0.6f), h * 0.012f, new Color(1f, 0.6f, 0.64f, 0.8f), Fx.Rand(0.18f, 0.32f));
				}
			});
		}
		// ③ 主拍
		Fx.After(front, beat, () =>
		{
			Glow.Flash(back, moonAt, new Color(1f, 0.16f, 0.24f), R * 3.4f, 0.5f);
						HitStop.Request(0.05);
			Fx.Shake(2);
			// 水珠往上扬、慢慢落回
			Glow.Sparks(front, water, 54, 220f * u, 760f * u, 1.1f,
				new[] { new Color(1f, 0.97f, 0.96f), new Color(1f, 0.4f, 0.48f), new Color(0.7f, 0.04f, 0.12f) },
				900f * u, 0.014f * u, 0.046f * u, Vector2.Up, 62f);
		});
		// ④ 余韵：往上飘的光粒
		Glitter(front, feet, h, u, 0.3f, 1.3f, 14, 0.6f);
	}
}
