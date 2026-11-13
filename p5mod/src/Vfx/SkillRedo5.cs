using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 技能牌重做第五批（2026-11-13，会话 C / D）。融入暗影前后被否的：影子 / 墨 / 烟（会话 A 三版）、斗篷戏法、红黑暗焰（火焰很 low、塑料感）、
/// 水晶环、网点融影 v1 / v2、蝶翼（用户：太娘，水晶 / 蝴蝶这种都太「实体化」）。
/// 用户 2026-11-13：要泛化的特效，纯炫技、好看、够帅就行，不用表达具体形状。
/// → 「影刃旋风」：周围压暗透红（调色，不是烟），光线往他身上收；一圈红 / 白 / 黑的能量刀光绕身高速旋转（前半圈在身前、后半圈在身后），
/// 主拍胸口一道横向光芒 + 碎光迸开；最后刀光一下收进身体，再闪一下。全部着色器，不用贴图。
/// 登记在 CardSkillFx 最后，覆盖第四批的同名牌。旧版本（水晶、网点剪影）在 git 历史里。
/// </summary>
public static class SkillRedo5
{
	public static void Register(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["Shadowmeld"] = Shadowmeld;
	}

	private static void S(string name, float vol, float at = 0f) => Sfx.Take(name, vol * 0.8f, at);

	private static Shader? _arc;

	/// <summary>
	/// 一道绕身旋转的能量刀光：ColorRect 是压扁的椭圆轨道（UV 中心为轴，+y 朝下 = 身前）。head：刀光头的角度，len：拖尾的角长度；
	/// 头尖、中段最粗、尾巴收细淡掉，往尾巴略往里卷；白芯 + 外层颜色，里面是沿弧线拉长的细速度纹（不是平涂）。
	/// side：1 只画前半圈 / -1 只画后半圈（同一道刀光在两层各画一半，前后遮挡就对了）。ink：黑芯红边的「暗刃」。
	/// </summary>
	private static Shader Arc => _arc ??= new Shader { Code = @"
shader_type canvas_item;
uniform float head = 0.0;
uniform float len = 2.5;
uniform float width = 0.08;
uniform float side = 1.0;
uniform float alpha = 1.0;
uniform float dir = 1.0;
uniform float hot = 1.0;
uniform float ink = 0.0;
uniform float flow = 0.0;
uniform float seed = 0.0;
uniform vec4 tint : source_color = vec4(1.0, 0.1, 0.18, 1.0);
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), f.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), f.x), f.y); }
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float th = atan(p.y, p.x);
	float mask = smoothstep(-0.06, 0.06, sin(th) * side);
	if (mask <= 0.0) discard;
	float dth = mod((head - th) * dir, 6.2831853);
	float s = dth / len;
	if (s > 1.0) discard;
	float R = 0.88 - 0.1 * s;
	float w = width * pow(1.0 - s, 0.9) * smoothstep(0.0, 0.07, s);
	float dr = r - R;
	float wl = max(w, 1e-4);
	float body = exp(-pow(dr / wl, 2.0));
	float core = exp(-pow(dr / (wl * 0.3), 2.0));
	// 沿弧线拉长的速度纹：横向密、纵向长
	float fib = vn(vec2(dth * 3.0 - flow, dr / width * 7.0 + seed * 13.0)) * 0.65 + vn(vec2(dth * 9.0 - flow * 2.0, dr / width * 18.0 + seed)) * 0.35;
	fib = smoothstep(0.25, 0.85, fib);
	float fade = pow(1.0 - s, 1.4);
	vec3 white = vec3(1.0, 0.96, 0.95);
	vec3 col;
	float a;
	if (ink > 0.5) {
		// 暗刃：芯是黑的，外面一层红、最边上一丝亮
		col = mix(tint.rgb, vec3(0.03, 0.005, 0.02), smoothstep(0.25, 0.8, core));
		col = mix(col, white, smoothstep(0.55, 0.0, body) * body * 1.2 * hot);
		a = smoothstep(0.08, 0.5, body) * (0.75 + 0.25 * fib) * fade;
	} else {
		col = mix(tint.rgb, white, core * hot * (1.0 - s * 0.6));
		a = (body * (0.35 + 0.65 * fib) * 0.85 + core * 0.6) * fade;
	}
	// 刀光头：一点亮
	float hd = exp(-pow(dth / 0.07, 2.0)) * exp(-pow(dr / (width * 1.2), 2.0));
	col = mix(col, white, hd * 0.8);
	a = max(a, hd * 0.9);
	a *= mask * alpha;
	if (a <= 0.003) discard;
	COLOR = vec4(col, clamp(a, 0.0, 1.0));
}" };

	private static Shader? _grade;

	/// <summary>他周围压暗透红：读屏、按亮度往深红黑压（红的地方留着），椭圆软边。k：强度。不是烟，是调色。</summary>
	private static Shader Grade => _grade ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D scr : hint_screen_texture, filter_linear;
uniform float k = 0.0;
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float m = smoothstep(1.0, 0.25, length(p)) * k;
	vec3 bg = texture(scr, SCREEN_UV).rgb;
	float l = dot(bg, vec3(0.3, 0.55, 0.15));
	vec3 g = mix(vec3(0.02, 0.0, 0.015), vec3(0.55, 0.05, 0.12), smoothstep(0.02, 0.5, l));
	g = mix(g, bg, smoothstep(0.35, 0.8, bg.r - bg.g) * 0.8);
	COLOR = vec4(mix(bg, g, m * 0.62), 1.0);
}" };

	private sealed class Blade
	{
		public Node2D B = null!, F = null!;
		public ShaderMaterial MB = null!, MF = null!;
		public float Y, R, Tilt, W, Spin, Len, Phase, Delay;
		public void Set(string n, Variant v) { MB.SetShaderParameter(n, v); MF.SetShaderParameter(n, v); }
	}

	/// <summary>
	/// 融入暗影（本回合获得的格挡翻倍），「影刃旋风」，约 1.5s：
	/// ① 0～0.3 周围压暗透红，二十来道光线从四面往他胸口收；
	/// ② 0.2 起一圈刀光（红 / 白热 / 黑芯暗刃）从外面扫进来、绕身高速旋转，前半圈在身前、后半圈在身后，边转边往上卷；
	/// ③ 0.42 主拍：胸口白红一闪 + 一道横向光芒 + 碎光迸开、轻震（斩）；
	/// ④ 1.0 第二拍（翻倍）：刀光一下收进身体，白闪、碎光往外迸，周围亮回来。
	/// </summary>
	public static void Shadowmeld(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, fc = c.Face;
		const float hit = 0.42f, snap = 1.0f, life = 1.5f;
		Control back = c.Back ?? c.Front;
		Vector2 feet = c.Feet, chest = c.Chest;
		S("gx_whoosh", 0.14f, 0.18f);
		S("gx_slice", 0.24f, hit);
		S("gx_whoosh", 0.16f, snap - 0.04f);
		S("gx_ping", 0.12f, snap + 0.02f);

		// ── 压暗透红（最先加，在所有后层特效下面）──
		{
			Vector2 sz = new(h * 2.6f, h * 1.7f);
			Node2D gr = Fx.Root(back, chest, "P5MeldGrade", life + 0.1);
			ShaderMaterial gm = new() { Shader = Grade };
			gr.AddChild(new ColorRect { Size = sz, Position = -sz / 2f, Material = gm, MouseFilter = Control.MouseFilterEnum.Ignore });
			Fx.Animate(gr, 0, life, t =>
			{
				float s = t * life;
				float k = Fx.OutCubic(Ease5.Seg(s, 0f, 0.3f)) * (1f - Fx.OutCubic(Ease5.Seg(s, snap, 0.4f)));
				gm.SetShaderParameter("k", k);
			});
		}

		// ── 往里收的光线 ──
		for (int i = 0; i < 22; i++)
		{
			float ang = Mathf.Tau * i / 22f + Fx.Rand(-0.1f, 0.1f);
			Vector2 dir = new(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f);
			float r0 = h * Fx.Rand(0.75f, 1.15f), r1 = h * Fx.Rand(0.08f, 0.16f);
			float delay = Fx.Rand(0f, 0.16f), dur = Fx.Rand(0.16f, 0.24f);
			Node2D lr = Fx.Root(c.Front, chest, "P5MeldIn", delay + dur + 0.1);
			Polygon2D line = Glow.TaperPoly(lr, i % 3 == 0 ? Pal5.Hot : new Color(1f, 0.16f, 0.24f));
			float w = Fx.Rand(2f, 4.5f) * u;
			line.Visible = false;
			Fx.Animate(line, delay, dur, t =>
			{
				line.Visible = true;
				float hd = Fx.InCubic(Mathf.Min(1f, t * 1.3f)), tl = Fx.InCubic(t);
				Vector2 a = dir * Mathf.Lerp(r0, r1, hd), b = dir * Mathf.Lerp(r0 * 1.25f, r1, tl);
				line.Polygon = Glow.TaperPts(b, a, w);
				line.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 4f) * (1f - Ease5.Seg(t, 0.8f, 0.2f)));
			});
		}

		// ── 刀光 ──
		var blades = new List<Blade>();
		var spec = new (float y, float r, float w, Color tint, float hot, float ink)[]
		{
			(0.12f, 0.50f, 0.10f, new Color(0.95f, 0.06f, 0.15f), 0.9f, 0f),
			(0.26f, 0.56f, 0.12f, new Color(0.9f, 0.05f, 0.12f), 0.6f, 1f),
			(0.38f, 0.46f, 0.07f, new Color(1f, 0.5f, 0.52f), 1.2f, 0f),
			(0.50f, 0.58f, 0.11f, new Color(0.95f, 0.06f, 0.15f), 1f, 0f),
			(0.62f, 0.50f, 0.12f, new Color(0.9f, 0.05f, 0.12f), 0.6f, 1f),
			(0.74f, 0.44f, 0.08f, new Color(1f, 0.2f, 0.28f), 1.1f, 0f),
			(0.88f, 0.40f, 0.09f, new Color(0.95f, 0.06f, 0.15f), 0.9f, 0f),
			(0.44f, 0.66f, 0.06f, new Color(1f, 0.7f, 0.7f), 1.3f, 0f),
			(0.20f, 0.62f, 0.025f, new Color(1f, 0.85f, 0.85f), 1.5f, 0f),
			(0.56f, 0.70f, 0.022f, new Color(1f, 0.85f, 0.85f), 1.5f, 0f),
			(0.80f, 0.56f, 0.025f, new Color(1f, 0.6f, 0.62f), 1.5f, 0f),
			(0.34f, 0.74f, 0.02f, new Color(1f, 0.85f, 0.85f), 1.5f, 0f),
		};
		for (int i = 0; i < spec.Length; i++)
		{
			var sp = spec[i];
			Blade b = new()
			{
				Y = sp.y, R = h * sp.r, W = sp.w, Tilt = Fx.Rand(-0.22f, 0.22f) + (i % 2 == 0 ? 0.08f : -0.08f) * fc,
				Spin = Fx.Rand(10f, 14f) * (i >= 8 ? 1.4f : 1f) * (i == 7 || i == 9 ? -1f : 1f) * fc, Len = i >= 8 ? Fx.Rand(1.6f, 2.2f) : Fx.Rand(2.2f, 3.2f), Phase = Fx.Rand(0f, Mathf.Tau),
				Delay = 0.2f + 0.012f * i,
			};
			foreach (bool front in new[] { false, true })
			{
				Node2D n = Fx.Root(front ? c.Front : back, feet, front ? "P5BladeF" : "P5BladeB", life + 0.1);
				ShaderMaterial m = new() { Shader = Arc };
				m.SetShaderParameter("side", front ? 1f : -1f);
				m.SetShaderParameter("tint", sp.tint);
				m.SetShaderParameter("hot", sp.hot);
				m.SetShaderParameter("ink", sp.ink);
				m.SetShaderParameter("width", sp.w);
				m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
				m.SetShaderParameter("dir", Mathf.Sign(b.Spin));
				m.SetShaderParameter("len", b.Len);
				n.AddChild(new ColorRect { Size = new Vector2(2f, 2f), Position = new Vector2(-1f, -1f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
				n.Visible = false;
				if (front) { b.F = n; b.MF = m; } else { b.B = n; b.MB = m; }
			}
			blades.Add(b);
		}
		const float squash = 0.3f;
		Vector2 HeadPos(Blade b, float rr, float yy, float ang) =>
			feet + new Vector2(0f, -h * yy) + new Vector2(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr * squash).Rotated(b.Tilt);
		Node2D driver = Fx.Root(c.Front, feet, "P5BladeDriver", life + 0.05);
		Fx.Animate(driver, 0, life, t =>
		{
			float s = t * life;
			foreach (Blade b in blades)
			{
				float a = s - b.Delay;
				bool on = a > 0f && s < snap + 0.2f;
				b.B.Visible = on;
				b.F.Visible = on;
				if (!on) continue;
				float inK = Fx.OutCubic(Mathf.Min(1f, a / 0.2f));
				float boost = s > hit ? 1f + 0.6f * Mathf.Exp(-(s - hit) * 6f) : 1f;
				float c0 = Fx.InCubic(Ease5.Seg(s, snap - 0.08f, 0.16f));
				float rr = b.R * Mathf.Lerp(1.5f, 1f, inK) * (1f - 0.85f * c0);
				float yy = Mathf.Lerp(b.Y, 0.55f, c0) + 0.08f * Ease5.Seg(s, b.Delay, snap - b.Delay);
				float ang = b.Phase + b.Spin * (a * boost + c0 * 0.4f);
				Vector2 at = feet + new Vector2(0f, -h * yy);
				Vector2 sz = new(rr * 2f, rr * 2f * squash);
				foreach (Node2D n in new[] { b.B, b.F })
				{
					n.GlobalPosition = at;
					n.Rotation = b.Tilt;
					n.Scale = sz / 2f;
				}
				b.Set("head", Mathf.PosMod(ang, Mathf.Tau));
				b.Set("flow", a * 14f);
				b.Set("alpha", Mathf.Min(1f, a / 0.08f) * (1f - Ease5.Seg(s, snap + 0.04f, 0.14f)));
				b.Set("width", b.W * (1f + 0.35f * (boost - 1f)) * (1f + 0.8f * c0));
			}
		});
		// 刀光头上掉下的碎光（按头在前在后放到对应层）
		for (int k = 0; k < 9; k++)
		{
			float d = 0.3f + 0.075f * k;
			Fx.After(c.Front, d, () =>
			{
				foreach (Blade b in blades)
				{
					if (Fx.Rand(0f, 1f) > 0.45f) continue;
					float a = d - b.Delay;
					if (a <= 0f) continue;
					float ang = b.Phase + b.Spin * a;
					Vector2 at = HeadPos(b, b.R, b.Y + 0.08f * Ease5.Seg(d, b.Delay, snap - b.Delay), ang);
					bool fr = Mathf.Sin(ang) > 0f;
					Glow.Sparks(fr ? c.Front : back, at, 5, 30f * u, 120f * u, 0.45f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 60f * u,
						0.015f * u, 0.035f * u, new Vector2(-Mathf.Sin(ang) * Mathf.Sign(b.Spin), 0.3f), 40f);
				}
			});
		}

		// ── 主拍：胸口白红一闪 + 横向光芒 + 碎光迸开 ──
		void Streak(Control layer, Vector2 at, float len, float thick, Color col, float dur)
		{
			Node2D r = Fx.Root(layer, at, "P5MeldStreak", dur + 0.1);
			Sprite2D sp = Glow.SoftSprite(r, Vector2.Zero, col, 1f);
			Fx.Animate(sp, 0, dur, t =>
			{
				float e = Ease5.OutExpo(t);
				sp.Scale = new Vector2(len * (0.4f + 0.6f * e) / 32f, thick * (1f - t * 0.6f) / 32f);
				sp.Modulate = new Color(col, col.A * (1f - Fx.OutCubic(t)));
			});
		}
		Fx.After(c.Front, hit, () =>
		{
			Fx.Shake(1);
			Glow.Flash(back, chest, new Color(1f, 0.15f, 0.25f), h * 1.3f, 0.3f);
			Glow.Flash(c.Front, chest, new Color(1f, 0.92f, 0.9f), h * 0.45f, 0.16f);
			Streak(c.Front, chest, h * 1.8f, h * 0.05f, new Color(1f, 0.15f, 0.25f, 0.85f), 0.32f);
			Streak(c.Front, chest, h * 1.5f, h * 0.012f, new Color(1f, 0.97f, 0.95f, 1f), 0.26f);
			Glow.Sparks(c.Front, chest, 26, 160f * u, 520f * u, 0.5f, new[] { new Color(1f, 0.97f, 0.95f), new Color(1f, 0.25f, 0.32f) }, 80f * u,
				0.02f * u, 0.05f * u, Vector2.Up, 180f);
			Glow.Glint(c.Front, c.Mask, 0.7f * u);
		});

		// ── 第二拍：刀光收进身体，再从他身上爆开一圈放射斩线 + 碎光 ──
		Fx.After(c.Front, snap + 0.06, () =>
		{
			Fx.Shake(1);
			Glow.Flash(c.Front, chest, new Color(1f, 0.94f, 0.92f), h * 0.9f, 0.22f);
			Glow.Flash(back, chest, new Color(1f, 0.15f, 0.25f), h * 1.9f, 0.4f);
			Streak(c.Front, chest, h * 0.08f, h * 1.6f, new Color(1f, 0.2f, 0.3f, 0.8f), 0.34f);
			Streak(c.Front, chest, h * 0.02f, h * 1.3f, new Color(1f, 0.97f, 0.95f, 1f), 0.26f);
			Streak(c.Front, chest, h * 2.0f, h * 0.05f, new Color(1f, 0.15f, 0.25f, 0.8f), 0.34f);
			Streak(c.Front, chest, h * 1.6f, h * 0.012f, new Color(1f, 0.97f, 0.95f, 1f), 0.26f);
			Glow.SpeedLines(back, chest, new Color(1f, 0.18f, 0.26f), 22, h * 0.25f, h * 1.05f, 0.34f);
			Glow.SpeedLines(c.Front, chest, Pal5.Hot, 8, h * 0.3f, h * 0.8f, 0.28f, 0.02f);
			Glow.Sparks(c.Front, chest, 40, 220f * u, 700f * u, 0.6f, new[] { new Color(1f, 0.97f, 0.95f), new Color(1f, 0.22f, 0.3f), new Color(0.6f, 0.02f, 0.1f) },
				160f * u, 0.025f * u, 0.06f * u, Vector2.Up, 180f);
			Glow.Sparks(back, chest + new Vector2(0f, h * 0.2f), 24, 80f * u, 260f * u, 0.9f, new[] { new Color(1f, 0.9f, 0.88f), new Color(1f, 0.2f, 0.28f) },
				-90f * u, 0.02f * u, 0.045f * u, Vector2.Up, 120f);
		});

		// ── 他：压暗时一起沉一点（不透明、看得清），收刀时白闪一下 ──
		if (c.SelfTint is { } tint)
		{
			Node2D tk = Fx.Root(c.Front, feet, "P5MeldTint", life + 0.1);
			Fx.Animate(tk, 0, life, t =>
			{
				float s = t * life;
				float dark = Fx.OutCubic(Ease5.Seg(s, 0.05f, 0.3f)) * (1f - Ease5.Seg(s, snap + 0.04f, 0.2f));
				float wf = s >= snap + 0.04f ? Mathf.Exp(-(s - snap - 0.04f) * 10f) * 0.7f : 0f;
				Color col = new Color(1f, 1f, 1f).Lerp(new Color(0.72f, 0.5f, 0.56f), dark) + new Color(wf, wf * 0.9f, wf * 0.9f, 0f);
				col.A = 1f;
				tint(col);
			});
			tk.TreeExiting += () => tint(Colors.White);
		}
	}
}
