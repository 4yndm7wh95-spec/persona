using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 迷雾（给所有敌人中毒 + 虚弱）（用户 2026-11-13：就是从人物那里吹过去一团迷雾，给敌人上毒，马上就散；
/// 不要包裹敌人；之前左边有明显的边界、太像矩形；颜色不要粉）。约 1.2s：
/// ① 0 Joker 一挥手，手边一团暗红迷雾吹出去（前后两头都是带噪声的宽软边，雾顶高低起伏，整团往右飘，不是从他身前一刀切铺开的墙）；
/// ② 雾飘到哪个敌人，那个敌人身上一闪、冒一点毒的光粒、染暗红（中毒 + 虚弱）；
/// ③ 0.5 起雾边飘边散，1.2s 散完，什么都不留下（毒雾那张能力牌才是常驻的雾）。
/// </summary>
public static class HazeFx
{
	private static Shader? _wall;

	/// <summary>
	/// 屏幕空间的一团迷雾：floor_y 地面（屏幕 UV）、band 雾的高度；head / tail 雾团的前后两头（屏幕 x），两头都是宽的噪声软边；
	/// flow 横向流动；clear 0 → 1 变稀散掉（由薄处先化开）。
	/// </summary>
	private static Shader Wall => _wall ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float floor_y = 0.75;
uniform float band = 0.4;
uniform float head = 0.3;
uniform float tail = 0.1;
uniform float flow = 0.0;
uniform float clear = 0.0;
uniform float alpha = 1.0;
uniform float aspect = 1.78;
uniform float seed = 0.0;
uniform float clock = 0.0;
uniform float dens = 1.0;
uniform vec3 deep = vec3(0.22, 0.05, 0.06);
uniform vec3 mid = vec3(0.66, 0.16, 0.17);
uniform vec3 hot = vec3(1.0, 0.45, 0.35);
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; } return v; }
void fragment() {
	vec2 uv = UV;
	float x = uv.x * aspect;
	float h = (floor_y - uv.y) / band;
	vec2 q = vec2(x * 2.2 - flow, h * 1.6 - clock * 0.12);
	vec2 w = vec2(fbm(q + seed), fbm(q + vec2(5.2, 1.3) + seed));
	float n = fbm(q + w * 1.6 + clock * 0.05);
	float n2 = fbm(q * 2.3 + w + seed * 1.7);
	// 雾顶按大尺度噪声高低起伏；上下都软
	float big = fbm(vec2(x * 0.9 - flow * 0.4, 7.0 + seed));
	float top = 0.6 + 0.5 * big;
	float prof = smoothstep(-0.4, 0.05, h) * (1.0 - smoothstep(top * 0.3, top, h + (n - 0.5) * 0.3));
	// 前后两头：宽的噪声软边（看不出直线），中间最浓
	float e = uv.x + (n2 - 0.5) * 0.2 + (big - 0.5) * 0.12;
	float len = max(head - tail, 0.05);
	float span = smoothstep(tail - 0.02, tail + len * 0.45, e) * (1.0 - smoothstep(head - len * 0.3, head + 0.04, e));
	// 散：由薄处先化开
	float keep = smoothstep(clear * 1.1 - 0.15, clear * 1.1 + 0.25, n);
	float d = prof * span * (0.6 + 0.85 * n) * dens;
	vec3 col = mix(deep, mid, smoothstep(0.35, 0.9, n * n2 * 1.9));
	col = mix(col, hot, smoothstep(0.72, 0.95, n2) * 0.25);
	float lit = smoothstep(0.0, 0.08, fbm(q + w * 1.6 + vec2(0.0, -0.06)) - n + 0.03);
	col *= mix(0.65, 1.3, lit);
	float a = clamp(d * keep * 0.85, 0.0, 0.85) * alpha;
	col = col * a;
	if (a <= 0.003) discard;
	COLOR = vec4(col, a);
}" };

	private static void S(string name, float vol, float at = 0f) => Sfx.Take(name, vol * 0.8f, at);

	public static void Play(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		const float travel = 0.6f, clearAt = 0.5f, life = 1.2f;
		Control back = c.Back ?? c.Front;
		Vector2 scr = Fx.Screen(c.Front);
		float floorS = Fx.ScreenPoint(c.Front, c.Feet).Y / scr.Y;
		float tallS = h * c.Front.GetCanvasTransform().Scale.Y / scr.Y;
		float hx = Fx.ScreenPoint(c.Front, c.Hand).X / scr.X;
		// 雾飘到敌人那边为止：最远的敌人再过去一点
		float far = hx + 0.3f * f;
		foreach (SkillFx.Victim v in c.Enemies)
		{
			float vx = Fx.ScreenPoint(c.Front, v.Feet).X / scr.X;
			far = f > 0f ? Mathf.Max(far, vx + 0.12f) : Mathf.Min(far, vx - 0.12f);
		}
		S("gx_whoosh", 0.22f, 0f);
		S("gx_pop", 0.16f, 0.3f);

		float seed = Fx.Rand(0f, 50f);
		var mats = new List<ShaderMaterial>();
		foreach ((Control layer, float dens) in new[] { (back, 1f), (c.Front, 0.4f) })
		{
			Node2D root = Fx.ScreenRoot(layer, "P5Haze");
			Fx.FreeAfter(root, life + 0.1);
			ShaderMaterial m = new() { Shader = Wall };
			ColorRect q = new() { Size = scr, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			if (f < 0f) { q.Scale = new Vector2(-1f, 1f); q.Position = new Vector2(scr.X, 0f); }   // 朝左时整个镜像
			root.AddChild(q);
			Fx.Clock(q);
			m.SetShaderParameter("aspect", scr.X / scr.Y);
			m.SetShaderParameter("seed", seed);
			m.SetShaderParameter("dens", dens);
			m.SetShaderParameter("floor_y", floorS + tallS * 0.04f);
			m.SetShaderParameter("band", tallS * 1.35f);
			mats.Add(m);
		}
		// 镜像后用的横坐标：朝右就是本身，朝左是 1 - x
		float X(float sx) => f > 0f ? sx : 1f - sx;
		float startX = X(hx), endX = X(far);
		Node2D driver = Fx.Root(c.Front, c.Feet, "P5HazeDriver", life + 0.05);
		float flow = 0f, last = 0f;
		Fx.Animate(driver, 0, life, t =>
		{
			float s = t * life;
			float dt = Mathf.Max(0f, s - last);
			last = s;
			flow += dt * Mathf.Lerp(2.4f, 0.4f, Ease5.Seg(s, 0f, 0.8f));
			float k = Ease5.OutQuint(Ease5.Seg(s, 0f, travel));
			// 雾团：头从手边飘到最远的敌人，尾巴跟在后面（一开始挤在手边，飘开后拉长）
			float headX = Mathf.Lerp(startX + 0.04f, endX, k);
			float tailX = Mathf.Lerp(startX - 0.02f, startX + (endX - startX) * 0.35f, Ease5.OutQuint(Ease5.Seg(s, 0.05f, travel + 0.2f)));
			float clear = Fx.OutCubic(Ease5.Seg(s, clearAt, life - clearAt - 0.05f));
			float a = Mathf.Min(1f, s / 0.05f) * (1f - Ease5.Seg(s, life - 0.2f, 0.2f));
			foreach (ShaderMaterial m in mats)
			{
				m.SetShaderParameter("head", headX);
				m.SetShaderParameter("tail", tailX);
				m.SetShaderParameter("flow", flow);
				m.SetShaderParameter("clear", clear);
				m.SetShaderParameter("alpha", a);
			}
		});
		Glow.Sparks(c.Front, c.Hand, 18, 200f * u, 560f * u, 0.45f,
			new[] { new Color(1f, 0.88f, 0.85f), new Color(0.9f, 0.12f, 0.16f), new Color(0.3f, 0.02f, 0.04f) }, 0f, 0.02f * u, 0.045f * u, new Vector2(f, -0.1f), 22f);

		// 雾飘到哪个敌人：它身上一闪、冒毒的光粒、染暗红
		foreach (SkillFx.Victim v in c.Enemies)
		{
			float vx = X(Fx.ScreenPoint(c.Front, v.Feet).X / scr.X);
			float kk = Mathf.Clamp((vx - startX - 0.04f) / Mathf.Max(0.01f, endX - startX - 0.04f), 0f, 0.999f);
			// OutQuint 反解：1 - (1 - t)^5 = kk
			float hitAt = travel * (1f - Mathf.Pow(1f - kk, 0.2f)) + 0.02f;
			float th = Mathf.Max(120f, (v.Feet - v.Top).Length());
			float wid = Mathf.Max(v.Width, th * 0.6f);
			Vector2 tc = v.Feet.Lerp(v.Top, 0.45f);
			Fx.After(c.Front, hitAt, () =>
			{
				Glow.Flash(back, tc, new Color(0.9f, 0.1f, 0.14f), wid * 1.5f, 0.28f);
				Glow.Flash(c.Front, tc, new Color(1f, 0.9f, 0.88f), wid * 0.45f, 0.12f);
				Glow.Sparks(c.Front, tc, 16, 30f * u, 120f * u, 0.8f, new[] { new Color(1f, 0.5f, 0.42f), new Color(0.85f, 0.08f, 0.12f), new Color(0.35f, 0.02f, 0.05f) },
					-110f * u, 0.02f * u, 0.045f * u, Vector2.Up, 70f);
			});
			if (v.Tint is { } tint)
			{
				Node2D tk = Fx.Root(c.Front, v.Feet, "P5HazeTint", life + 0.1);
				Color dark = new(0.62f, 0.4f, 0.42f);
				Fx.Animate(tk, 0, life, t =>
				{
					float s = t * life;
					float d = Fx.OutCubic(Ease5.Seg(s, hitAt - 0.03f, 0.1f)) * (1f - Ease5.Seg(s, clearAt + 0.1f, 0.5f));
					float flash = s >= hitAt ? Mathf.Exp(-(s - hitAt) * 14f) * 0.6f : 0f;
					tint(Colors.White.Lerp(dark, d) + new Color(flash, flash * 0.8f, flash * 0.8f, 0f));
				});
				tk.TreeExiting += () => tint(Colors.White);
			}
		}
	}
}
