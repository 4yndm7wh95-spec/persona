using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 迷雾（给所有敌人中毒 + 虚弱）（用户 2026-11-13：从人物那里吹出一团迷雾，给敌人上毒，马上就散；不要包裹敌人、不要矩形边界、不要粉；
/// 第二次：不要简单平移过去，要真的有「吹出来」的感觉；音效不合适就不要）。约 1.25s：
/// ① 0～0.45 他身前半个身位的空中先鼓起一团迷雾，从那里吹出去，越往外越宽（喇叭口），里面的雾纹和风丝顺着方向往外急流，前沿往前冲（先快后慢）；
///    几团雾团从那里吹出去，边飞边胀开、慢下来；
/// ② 前沿扫过哪个敌人，它一闪、冒毒的光粒、染暗红（中毒 + 虚弱）；
/// ③ 0.65 起这股气流和起点断开、整股往前慢慢散，2.1s 散完，什么都不留下。音效：原版活雾的吹气攻击。
/// </summary>
public static class HazeFx
{
	private static Shader? _plume;

	/// <summary>
	/// 屏幕空间的一股喷出的迷雾：src 喷口（屏幕 UV），dir 方向（按屏幕高度为单位的坐标），reach 前沿离喷口多远，tail_d 尾巴（喷完后和喷口断开），
	/// w0 喷口半宽、spread 往外张开的斜率；雾纹沿方向往外流（flow）；clear 0 → 1 变稀散掉。
	/// </summary>
	private static Shader Plume => _plume ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec2 src = vec2(0.2, 0.6);
uniform vec2 dir = vec2(1.0, 0.0);
uniform float reach = 0.5;
uniform float tail_d = 0.0;
uniform float w0 = 0.03;
uniform float spread = 0.4;
uniform float flow = 0.0;
uniform float clear = 0.0;
uniform float alpha = 1.0;
uniform float aspect = 1.78;
uniform float seed = 0.0;
uniform float clock = 0.0;
uniform float dens = 1.0;
uniform vec3 deep = vec3(0.22, 0.05, 0.06);
uniform vec3 mid = vec3(0.66, 0.16, 0.17);
uniform vec3 hot = vec3(1.0, 0.5, 0.42);
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; } return v; }
void fragment() {
	vec2 p = vec2(UV.x * aspect, UV.y);
	vec2 s = vec2(src.x * aspect, src.y);
	vec2 dv = p - s;
	float a = dot(dv, dir);
	float l = dot(dv, vec2(-dir.y, dir.x));
	float wid = w0 + max(a, 0.0) * spread;
	// 雾纹往外流：沿方向的坐标减去 flow（越往外拉得越长）
	vec2 q = vec2(a * 2.0 - flow * 0.7, l / wid * 1.1 + seed);
	vec2 w = vec2(fbm(q + seed), fbm(q + vec2(5.2, 1.3)));
	float n = fbm(q + w * 1.3 + vec2(0.0, clock * 0.1));
	float n2 = fbm(q * 2.1 + w + seed * 1.7);
	// 横向：中轴最浓，边缘被噪声撕开（喇叭口）
	float lat = 1.0 - smoothstep(0.2, 1.0, abs(l) / wid + (n - 0.5) * 0.7);
	// 前沿 / 尾巴 / 喷口：都是带噪声的软边
	float front = 1.0 - smoothstep(reach - 0.14, reach + 0.02, a + (n2 - 0.5) * 0.16);
	float back = smoothstep(tail_d - 0.02, tail_d + 0.16, a + (n2 - 0.5) * 0.12);
	float mouth = smoothstep(-0.14, 0.08, a + (n2 - 0.5) * 0.1);   // 起点是一团（往后也鼓一点），不是一个尖点
	// 风丝：沿方向急流的细丝
	float st = 1.0 - abs(noise(vec2(a * 1.6 - flow * 1.7, l / wid * 9.0 + seed)) * 2.0 - 1.0);
	st = pow(st, 7.0);
	float keep = smoothstep(clear * 1.1 - 0.15, clear * 1.1 + 0.25, n);
	float shape = lat * front * back * mouth * keep;
	float d = shape * (0.5 + 0.85 * n) * dens;
	vec3 col = mix(deep, mid, smoothstep(0.35, 0.9, n * n2 * 1.9));
	col = mix(col, hot, smoothstep(0.72, 0.95, n2) * 0.22);
	float lit = smoothstep(0.0, 0.08, fbm(q + w * 1.3 + vec2(0.06, 0.0)) - n + 0.03);
	col *= mix(0.65, 1.3, lit);
	col += hot * st * shape * 0.1;
	float aa = clamp(d * 0.9 + st * shape * 0.04 * dens, 0.0, 0.88) * alpha;
	col *= aa;
	if (aa <= 0.003) discard;
	COLOR = vec4(col, aa);
}" };

	/// <summary>出手：手上一闪、疾光射到起点的时间；雾从这之后才吹出去。</summary>
	private const float Pre = 0.11f;

	public static void Play(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		const float blast = 0.5f, cut = 0.65f, clearAt = 0.95f, life = 2.1f;   // 2026-11-13 用户：散得太快 → 停久一点
		Control back = c.Back ?? c.Front;
		// 音效：原版活雾的吹气攻击（用户 2026-11-13 指定）
		Stage.PlayEvent?.Invoke("event:/sfx/enemy/enemy_attacks/living_fog/living_fog_attack_blow", 0.7f);
		Vector2 scr = Fx.Screen(c.Front);
		float tallS = h * c.Front.GetCanvasTransform().Scale.Y / scr.Y;
		Vector2 ToS(Vector2 world) { Vector2 sp = Fx.ScreenPoint(c.Front, world); return new Vector2(sp.X / scr.X, sp.Y / scr.Y); }
		// 起点不在手上（用户 2026-11-13：毒雾不会真的从手里喷出来）：他身前半个身位的空中先鼓起一团，再从那里吹出去
		Vector2 origin = c.Hand + new Vector2(f * h * 0.5f, h * 0.12f);
		Vector2 srcUV = ToS(origin);
		// 瞄准敌人身体中间的平均位置；喷到最远那个再过去一点
		Vector2 aimW = Vector2.Zero;
		foreach (SkillFx.Victim v in c.Enemies) aimW += v.Feet.Lerp(v.Top, 0.4f);
		aimW = c.Enemies.Count > 0 ? aimW / c.Enemies.Count : c.Hand + new Vector2(f * h * 2f, h * 0.3f);
		float aspect = scr.X / scr.Y;
		Vector2 A(Vector2 uv) => new(uv.X * aspect, uv.Y);
		Vector2 dir = (A(ToS(aimW)) - A(srcUV)).Normalized();
		float reachMax = 0.15f;
		var proj = new List<float>();
		foreach (SkillFx.Victim v in c.Enemies)
		{
			float pa = (A(ToS(v.Feet.Lerp(v.Top, 0.4f))) - A(srcUV)).Dot(dir);
			proj.Add(pa);
			reachMax = Mathf.Max(reachMax, pa + 0.18f);
		}
		float w0 = tallS * 0.2f, spread = Mathf.Max(0.05f, (tallS * 1.05f - w0) / Mathf.Max(0.1f, reachMax));

		float seed = Fx.Rand(0f, 50f);
		var mats = new List<ShaderMaterial>();
		foreach ((Control layer, float dens) in new[] { (back, 1f), (c.Front, 0.4f) })
		{
			Node2D root = Fx.ScreenRoot(layer, "P5Haze");
			Fx.FreeAfter(root, life + Pre + 0.1);
			ShaderMaterial m = new() { Shader = Plume };
			ColorRect q = new() { Size = scr, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(q);
			Fx.Clock(q);
			m.SetShaderParameter("aspect", aspect);
			m.SetShaderParameter("seed", seed);
			m.SetShaderParameter("dens", dens);
			m.SetShaderParameter("src", srcUV);
			m.SetShaderParameter("dir", dir);
			m.SetShaderParameter("w0", w0);
			m.SetShaderParameter("spread", spread);
			m.SetShaderParameter("alpha", 0f);   // 出手的疾光到之前不显示
			mats.Add(m);
		}
		Node2D driver = Fx.Root(c.Front, c.Feet, "P5HazeDriver", life + Pre + 0.05);
		float flow = 0f, last = 0f;
		Fx.Animate(driver, Pre, life, t =>   // 疾光到达起点（Pre）以后才开始吹
		{
			float s = t * life;
			float dt = Mathf.Max(0f, s - last);
			last = s;
			// 喷的时候雾纹往外急流，喷完慢下来
			flow += dt * Mathf.Lerp(9f, 0.8f, Fx.OutCubic(Ease5.Seg(s, 0f, 0.8f)));
			float reach = reachMax * Fx.OutCubic(Ease5.Seg(s, 0f, blast));
			float tail = reachMax * 0.45f * Fx.OutCubic(Ease5.Seg(s, cut, life - cut));
			float clear = Fx.OutCubic(Ease5.Seg(s, clearAt, life - clearAt - 0.05f));
			float a = Mathf.Min(1f, s / 0.04f) * (1f - Ease5.Seg(s, life - 0.35f, 0.35f));
			foreach (ShaderMaterial m in mats)
			{
				m.SetShaderParameter("reach", reach);
				m.SetShaderParameter("tail_d", tail);
				m.SetShaderParameter("flow", flow);
				m.SetShaderParameter("clear", clear);
				m.SetShaderParameter("alpha", a);
			}
		});
		// 出手（用户 2026-11-13：手和雾之间要有打出的特效，不然像凭空冒出来的雾）：
		// 手上一闪、迸一小撮碎光 → 两道暗红疾光从手里射向身前的起点（一道带弧）→ 起点鼓起一团网点光晕、迸开一圈碎光，雾从那里吹开
		Glow.Flash(c.Front, c.Hand, new Color(1f, 0.88f, 0.85f), h * 0.28f, 0.14f);
		Glow.Sparks(c.Front, c.Hand, 10, 80f * u, 260f * u, 0.35f, new[] { new Color(1f, 0.9f, 0.88f), new Color(0.9f, 0.12f, 0.16f) }, 0f,
			0.015f * u, 0.035f * u, new Vector2(f, -0.1f), 40f);
		Vector2 hand = c.Hand, mid = (c.Hand + origin) * 0.5f + new Vector2(0f, -h * 0.22f);
		SkillRedo5.Streak(c.Front, t => hand.Lerp(origin, t), 0f, 0.12f, h * 0.5f, h * 0.11f, new Color(0.9f, 0.08f, 0.14f));
		SkillRedo5.Streak(c.Front, t => hand.Lerp(mid, t).Lerp(mid.Lerp(origin, t), t), 0.02f, 0.13f, h * 0.4f, h * 0.045f, new Color(1f, 0.85f, 0.85f), 1.5f);
		Fx.After(c.Front, 0.1f, () =>
		{
			Glow.Flash(back, origin, new Color(0.9f, 0.15f, 0.18f), h * 0.9f, 0.3f);
			Glow.Flash(c.Front, origin, new Color(1f, 0.88f, 0.86f), h * 0.3f, 0.12f);
			SkillRedo5.SoftBurst(back, origin, h * 0.9f, 0.4f, new Color(0.85f, 0.06f, 0.12f));
			Glow.Sparks(c.Front, origin, 18, 120f * u, 380f * u, 0.45f, new[] { new Color(1f, 0.9f, 0.88f), new Color(0.9f, 0.12f, 0.16f), new Color(0.3f, 0.02f, 0.04f) },
				60f * u, 0.02f * u, 0.04f * u, Vector2.Up, 180f);
		});
		// 雾团：从手边喷出去，边飞边胀开、慢下来（普通混合的柔光团，暗红偏灰）
		Vector2 dirW = (aimW - origin).Normalized();
		float distW = origin.DistanceTo(aimW);
		for (int i = 0; i < 10; i++)
		{
			float d0 = Pre + 0.018f * i;
			Vector2 off = dirW.Rotated(Fx.Rand(-0.22f, 0.22f));
			float go = distW * Fx.Rand(0.55f, 1.15f), size = h * Fx.Rand(0.5f, 0.85f);
			Node2D pr = Fx.Root(i % 2 == 0 ? back : c.Front, origin, "P5HazePuff", d0 + 1.45);
			Sprite2D sp = new() { Texture = Glow.Soft };
			pr.AddChild(sp);
			Color pc = i % 3 == 0 ? new Color(0.75f, 0.2f, 0.2f) : new Color(0.42f, 0.09f, 0.1f);
			pr.Visible = false;
			Fx.Animate(pr, d0, 1.4, t =>
			{
				pr.Visible = true;
				float e = 1f - Mathf.Exp(-t * 4.5f);
				pr.GlobalPosition = origin + off * go * e + new Vector2(0f, -h * 0.08f * t);
				sp.Scale = Vector2.One * (size * (0.25f + 1.6f * e)) / 32f;
				sp.Modulate = new Color(pc, (i % 2 == 0 ? 0.55f : 0.32f) * Mathf.Min(1f, t * 8f) * (1f - Fx.InCubic(t)));
			});
		}

		// 前沿扫过哪个敌人：它一闪、冒毒的光粒、染暗红
		for (int i = 0; i < c.Enemies.Count; i++)
		{
			SkillFx.Victim v = c.Enemies[i];
			float kk = Mathf.Clamp(proj[i] / reachMax, 0f, 0.999f);
			float hitAt = blast * (1f - Mathf.Pow(1f - kk, 1f / 3f)) + 0.03f + Pre;   // OutCubic 反解；出手之后才吹
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
				Node2D tk = Fx.Root(c.Front, v.Feet, "P5HazeTint", life + Pre + 0.1);
				Color dark = new(0.62f, 0.4f, 0.42f);
				Fx.Animate(tk, 0, life, t =>
				{
					float s = t * life - Pre;
					float d = Fx.OutCubic(Ease5.Seg(s, hitAt - 0.03f, 0.1f)) * (1f - Ease5.Seg(s, clearAt + 0.1f, 0.8f));
					float flash = s >= hitAt ? Mathf.Exp(-(s - hitAt) * 14f) * 0.6f : 0f;
					tint(Colors.White.Lerp(dark, d) + new Color(flash, flash * 0.8f, flash * 0.8f, 0f));
				});
				tk.TreeExiting += () => tint(Colors.White);
			}
		}
	}
}
