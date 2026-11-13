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

	/// <summary>
	/// 余像（能力：之后每打出一张牌获得 1 点格挡），约 0.9s：
	/// ① 0～0.15 一串水平疾光（白热 / 红）从他身前往身后掠过，一半在身前一半在身后；他身上一层贴身的红残影被「拽」到身后一点、拖出横向速度纹；
	/// ② 0.5 主拍：残影「啪」地弹回他身上——胸口白红一闪、横向光芒、碎光往身后迸，他身上一亮（叮）。
	/// 拍子：掠过（风）→ 弹回（叮）。
	/// </summary>
	public static void Afterimage(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		const float snap = 0.5f, life = 0.9f;
		Control back = c.Back ?? c.Front;
		S("gx_whoosh", 0.2f, 0.02f);
		S("gx_ping", 0.2f, snap);
		// 水平疾光：从身前往身后
		for (int i = 0; i < 18; i++)
		{
			float y = c.Feet.Y - h * Fx.Rand(0.05f, 1.0f);
			float x0 = c.Feet.X + f * h * Fx.Rand(0.5f, 0.9f), x1 = c.Feet.X - f * h * Fx.Rand(0.7f, 1.2f);
			bool fr = i % 3 == 0;
			bool white = i % 4 == 1;
			Vector2 a = new(x0, y), b = new(x1, y + Fx.Rand(-0.02f, 0.02f) * h);
			DashFly(fr ? c.Front : back, t => a.Lerp(b, t), Fx.Rand(0f, 0.14f), Fx.Rand(0.22f, 0.32f), h * Fx.Rand(0.45f, 0.8f),
				h * (white ? Fx.Rand(0.018f, 0.03f) : Fx.Rand(0.04f, 0.07f)), white ? new Color(1f, 0.8f, 0.8f) : new Color(0.95f, 0.06f, 0.15f), white ? 1.4f : 0.9f);
		}
		for (int k = 0; k < 4; k++)
		{
			float d = 0.03f + 0.05f * k;
			Vector2 at = c.Feet + new Vector2(-f * h * 0.2f, -h * Fx.Rand(0.2f, 0.85f));
			Fx.After(c.Front, d, () => Glow.Sparks(back, at, 10, 200f * u, 520f * u, 0.4f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 0f,
				0.015f * u, 0.035f * u, new Vector2(-f, 0f), 12f));
		}
		if (RedEcho(c, life) is { } echo)
		{
			var (sp, m) = echo;
			float off = -f * h * 0.075f;
			Fx.Animate(sp, 0, life, t =>
			{
				float s = t * life;
				float pull = Ease5.OutQuint(Ease5.Seg(s, 0.02f, 0.16f));
				float drift = Ease5.Seg(s, 0.18f, snap - 0.18f);
				float back0 = Fx.InCubic(Ease5.Seg(s, snap - 0.08f, 0.08f));
				float k = pull * (1f + 0.2f * drift) * (1f - back0);
				sp.Position = new Vector2(off * k, 0f);
				float sm = f * 0.16f * (pull * (1f - Ease5.Seg(s, 0.12f, 0.25f) * 0.7f)) * (1f - back0);
				if (s > snap - 0.08f && s < snap) sm = -f * 0.1f * back0;
				m.SetShaderParameter("smear", sm);
				m.SetShaderParameter("flow", s * 6f);
				m.SetShaderParameter("fill", 0.5f);
				m.SetShaderParameter("alpha", Mathf.Min(1f, s / 0.04f) * (s > snap ? 1f - Ease5.Seg(s, snap, 0.14f) : 0.9f));
			});
		}
		Fx.After(c.Front, snap, () =>
		{
			Glow.Flash(c.Front, c.Chest, new Color(1f, 0.93f, 0.9f), h * 0.4f, 0.14f);
			Glow.Flash(back, c.Chest, new Color(1f, 0.15f, 0.25f), h * 1.1f, 0.26f);
			Flare(c.Front, c.Chest, h * 1.4f, h * 0.04f, new Color(1f, 0.15f, 0.25f, 0.85f), 0.28f);
			Flare(c.Front, c.Chest, h * 1.1f, h * 0.01f, new Color(1f, 0.97f, 0.95f, 1f), 0.22f);
			Glow.Sparks(c.Front, c.Chest, 22, 200f * u, 560f * u, 0.45f, new[] { new Color(1f, 0.97f, 0.95f), new Color(1f, 0.25f, 0.32f) }, 60f * u,
				0.02f * u, 0.045f * u, new Vector2(-f, -0.15f), 40f);
			Glow.Glint(c.Front, c.Mask, 0.6f * u);
		});
		SkillRedo.Pulse(c, new Color(1.4f, 0.72f, 0.74f), snap, 0.26f);
	}

	/// <summary>
	/// 余像生效（之后每打出一张牌、获得那 1 点格挡的一刻）：贴身一层很淡的红残影往身后滑开一点、拖着速度纹淡掉，身后掠过两三道细疾光。0.4s，很轻。
	/// </summary>
	public static void AfterimageTick(SkillCtx c)
	{
		float h = c.Tall, f = c.Face;
		const float life = 0.4f;
		Control back = c.Back ?? c.Front;
		S("gx_tick", 0.05f);
		if (RedEcho(c, life) is { } echo)
		{
			var (sp, m) = echo;
			Fx.Animate(sp, 0, life, t =>
			{
				float e = Ease5.OutQuint(t);
				sp.Position = new Vector2(-f * h * 0.05f * e, 0f);
				m.SetShaderParameter("smear", f * 0.1f * (1f - e * 0.6f));
				m.SetShaderParameter("flow", t * 3f);
				m.SetShaderParameter("fill", 0.4f);
				m.SetShaderParameter("alpha", 0.6f * Mathf.Min(1f, t * life / 0.03f) * (1f - Ease5.Seg(t, 0.45f, 0.55f)));
			});
		}
		for (int i = 0; i < 3; i++)
		{
			float y = c.Feet.Y - h * (0.25f + 0.25f * i + Fx.Rand(-0.06f, 0.06f));
			Vector2 a = new(c.Feet.X - f * h * 0.1f, y), b = new(c.Feet.X - f * h * Fx.Rand(0.55f, 0.8f), y);
			DashFly(back, t => a.Lerp(b, t), 0.02f * i, 0.26f, h * 0.35f, h * 0.014f, i == 1 ? new Color(1f, 0.85f, 0.85f) : new Color(0.95f, 0.08f, 0.16f), 1.2f);
		}
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

	/// <summary>
	/// 萎靡（失去力量 + 虚弱），约 1.4s，要看得出是他干的：
	/// ① 他手一挥，一道红白的刀光从手里甩出去，划着弧飞到它头顶；
	/// ② 0.28 刀光在它头顶散成四道刀光，绕着它螺旋往下压、越收越紧（前半圈在它身前、后半圈在身后）；
	/// ③ 0.52 主拍（力量）：刀光扣到它脚边，一排疾光从上往下砸在它身上，它被往下压扁、压暗；红光顺着来时那道弧一串串流回他手里，他身上一亮；
	/// ④ 0.86 第二拍（虚弱）：再往下砸一下、再压一下，然后慢慢回弹，刀光散掉。
	/// 拍子：甩出（风）→ 压（斩，主拍）→ 再压（咔）。
	/// </summary>
	public static void Malaise(SkillCtx c)
	{
		if (c.Target is not SkillFx.Victim v) return;
		float th = Mathf.Max(120f, (v.Feet - v.Top).Length()), u = c.Unit, jh = c.Tall;
		float wid = Mathf.Max(v.Width, th * 0.6f);
		const float land = 0.26f, hit = 0.62f, beat2 = 0.96f, life = 1.5f;
		Control back = c.Back ?? c.Front;
		S("gx_whoosh", 0.16f, 0.02f);
		S("gx_slice", 0.24f, hit);
		S("gx_tock", 0.14f, beat2);

		Vector2 hand = c.Hand, top = v.Top + new Vector2(0f, -th * 0.15f);
		Vector2 ctl = (hand + top) * 0.5f + new Vector2(0f, -Mathf.Max(jh, th) * 0.55f);
		Vector2 Bez(float t) => hand.Lerp(ctl, t).Lerp(ctl.Lerp(top, t), t);

		// ① 甩出
		Glow.Flash(c.Front, hand, new Color(1f, 0.9f, 0.88f), jh * 0.2f, 0.12f);
		DashFly(c.Front, Bez, 0.02f, land - 0.02f, jh * 0.9f, jh * 0.14f, new Color(0.95f, 0.06f, 0.15f), 1.1f, t => Mathf.Pow(t, 0.8f), 0.9f);
		DashFly(c.Front, Bez, 0.0f, land, jh * 1.1f, jh * 0.08f, new Color(0.6f, 0.0f, 0.08f), 0.5f, t => Mathf.Pow(t, 0.85f), 0.85f);
		DashFly(c.Front, Bez, 0.04f, land - 0.04f, jh * 0.7f, jh * 0.035f, new Color(1f, 0.85f, 0.85f), 1.5f, t => Mathf.Pow(t, 0.8f), 0.9f);
		for (int k = 0; k < 5; k++)
		{
			float d = 0.05f + 0.04f * k;
			Fx.After(c.Front, d, () => Glow.Sparks(c.Front, Bez(Mathf.Pow((d - 0.02f) / (land - 0.02f), 0.8f)), 12, 30f * u, 140f * u, 0.45f,
				new[] { new Color(1f, 0.95f, 0.93f), new Color(1f, 0.22f, 0.3f) }, 80f * u, 0.015f * u, 0.035f * u, Vector2.Down, 90f));
		}
		Fx.After(c.Front, land, () =>
		{
			Glow.Flash(c.Front, top, new Color(1f, 0.9f, 0.88f), th * 0.6f, 0.16f);
			Glow.Flash(back, top, new Color(1f, 0.15f, 0.25f), th * 1.0f, 0.24f);
			Glow.Sparks(c.Front, top, 18, 120f * u, 360f * u, 0.45f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 120f * u, 0.02f * u, 0.04f * u, Vector2.Up, 180f);
		});

		// ② ③ 四道刀光绕着它螺旋往下压
		var spec = new (float r, float w, Color tint, float hot, float ink, float y0, float y1)[]
		{
			(0.85f, 0.18f, new Color(0.95f, 0.06f, 0.15f), 0.9f, 0f, 1.1f, 0.3f),
			(0.95f, 0.18f, new Color(0.9f, 0.05f, 0.12f), 0.6f, 1f, 0.95f, 0.16f),
			(0.78f, 0.09f, new Color(1f, 0.75f, 0.75f), 1.4f, 0f, 1.2f, 0.5f),
			(1.0f, 0.06f, new Color(1f, 0.85f, 0.85f), 1.5f, 0f, 0.85f, 0.08f),
			(0.9f, 0.15f, new Color(1f, 0.2f, 0.28f), 1.1f, 0f, 1.25f, 0.62f),
			(1.05f, 0.05f, new Color(1f, 0.85f, 0.85f), 1.5f, 0f, 0.7f, 0.04f),
		};
		const float squash = 0.34f;
		float side = Mathf.Sign(c.Feet.X - v.Feet.X);
		var blades = new List<(Blade b, float r, float w, float y0, float y1, float spin, float ph)>();
		foreach (var sp in spec)
		{
			Blade b = MakeBlade(c.Front, back, v.Feet, sp.tint, sp.hot, sp.ink, sp.w, Fx.Rand(2.2f, 3.0f), -side, life);
			blades.Add((b, wid * sp.r, sp.w, sp.y0, sp.y1, -side * Fx.Rand(8f, 11f), Fx.Rand(0f, Mathf.Tau)));
		}
		Node2D driver = Fx.Root(c.Front, v.Feet, "P5MalaiseDriver", life + 0.05);
		Fx.Animate(driver, 0, life, t =>
		{
			float s = t * life;
			foreach (var x in blades)
			{
				float a = s - land;
				bool on = a > 0f;
				x.b.B.Visible = on;
				x.b.F.Visible = on;
				if (!on) continue;
				float down = Mathf.Pow(Ease5.Seg(s, land, hit - land), 1.6f);
				float bump = s > beat2 ? Mathf.Sin(Mathf.Pi * Ease5.Seg(s, beat2, 0.14f)) : 0f;
				float rr = x.r * Mathf.Lerp(0.6f, 1.15f, 1f - down) * (1f - 0.12f * bump) * (0.85f + 0.15f * Fx.OutCubic(Mathf.Min(1f, a / 0.08f)));
				float yy = Mathf.Lerp(x.y0, x.y1, down) - 0.04f * bump;
				float ang = x.ph + x.spin * a * (1f - 0.5f * down);
				Vector2 at = v.Feet + new Vector2(0f, -th * yy);
				Vector2 sz = new(rr * 2f, rr * 2f * squash);
				foreach (Node2D n in new[] { x.b.B, x.b.F })
				{
					n.GlobalPosition = at;
					n.Rotation = 0f;
					n.Scale = sz / 2f;
				}
				x.b.Set("head", Mathf.PosMod(ang, Mathf.Tau));
				x.b.Set("flow", a * 14f);
				x.b.Set("alpha", Mathf.Min(1f, a / 0.06f) * (1f - Ease5.Seg(s, beat2 + 0.18f, 0.3f)));
				x.b.Set("width", x.w * (1f + 0.6f * down * Mathf.Exp(-Mathf.Max(0f, s - hit) * 5f)));
			}
		});

		// 往下砸的疾光 + 压扁压暗
		void Rain(float at0, int n, float power)
		{
			for (int i = 0; i < n; i++)
			{
				float x = v.Feet.X + wid * Fx.Rand(-0.55f, 0.55f);
				float y0 = v.Top.Y - th * Fx.Rand(0.5f, 0.9f), y1 = v.Feet.Y - th * Fx.Rand(0.0f, 0.3f);
				bool fr = i % 2 == 0;
				bool white = i % 3 == 1;
				Vector2 a = new(x, y0), b = new(x + Fx.Rand(-0.04f, 0.04f) * th, y1);
				DashFly(fr ? c.Front : back, t => a.Lerp(b, t), at0 - 0.08f + Fx.Rand(0f, 0.05f), 0.16f, th * Fx.Rand(0.6f, 0.95f) * power,
					th * (white ? 0.03f : 0.07f) * power, white ? new Color(1f, 0.85f, 0.85f) : new Color(0.95f, 0.06f, 0.15f), white ? 1.4f : 0.9f,
					Fx.InCubic, 0.85f);
			}
		}
		Rain(hit, 14, 1f);
		Rain(beat2, 7, 0.8f);
		Fx.After(c.Front, hit - 0.02, () =>
		{
			Node2D root = Fx.ScreenRoot(c.Front, "P5MalaisePress");
			Fx.FreeAfter(root, life - hit + 0.1);
			root.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
			ShaderMaterial m = new() { Shader = Press };
			var rect = new ColorRect { Size = Fx.Screen(c.Front), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(rect);
			Vector2 scr = Fx.Screen(c.Front);
			Vector2 a = Fx.ScreenPoint(c.Front, v.Top + new Vector2(-wid * 0.9f, -th * 0.5f)), b = Fx.ScreenPoint(c.Front, v.Feet + new Vector2(wid * 0.9f, th * 0.5f));
			Vector2 fl = Fx.ScreenPoint(c.Front, v.Feet);
			m.SetShaderParameter("box", new Vector4(a.X / scr.X, a.Y / scr.Y, b.X / scr.X, b.Y / scr.Y));
			m.SetShaderParameter("floor_y", fl.Y / scr.Y);
			float dur = life - hit + 0.02f;
			Fx.Animate(rect, 0, dur, t =>
			{
				float s = t * dur, s2 = s - (beat2 - hit + 0.02f);
				float k1 = Fx.OutCubic(Mathf.Clamp(s / 0.06f, 0f, 1f)) * Mathf.Exp(-Mathf.Max(0f, s - 0.08f) * 3f);
				float k2 = s2 > 0f ? Fx.OutCubic(Mathf.Clamp(s2 / 0.05f, 0f, 1f)) * Mathf.Exp(-Mathf.Max(0f, s2 - 0.06f) * 4f) * 0.7f : 0f;
				m.SetShaderParameter("amt", 0.18f * Mathf.Max(k1, k2 + 0.2f * k1));
			});
			Fx.Shake(1);
			Glow.Flash(back, v.Feet + new Vector2(0f, -th * 0.35f), new Color(1f, 0.15f, 0.25f), wid * 2.2f, 0.32f);
			Glow.Flash(c.Front, v.Feet + new Vector2(0f, -th * 0.3f), new Color(1f, 0.92f, 0.9f), wid * 0.8f, 0.16f);
			Flare(c.Front, v.Feet + new Vector2(0f, -th * 0.45f), th * 0.07f, th * 1.6f, new Color(1f, 0.15f, 0.25f, 0.8f), 0.3f);
			Flare(c.Front, v.Feet + new Vector2(0f, -th * 0.45f), th * 0.02f, th * 1.3f, new Color(1f, 0.97f, 0.95f, 1f), 0.24f);
			Glow.SpeedLines(back, v.Feet + new Vector2(0f, -th * 0.3f), new Color(1f, 0.18f, 0.26f), 16, th * 0.35f, th * 1.0f, 0.3f);
			Flare(c.Front, v.Feet + new Vector2(0f, -th * 0.04f), wid * 2.2f, th * 0.05f, new Color(1f, 0.15f, 0.25f, 0.85f), 0.3f);
			Flare(c.Front, v.Feet + new Vector2(0f, -th * 0.04f), wid * 1.7f, th * 0.012f, new Color(1f, 0.97f, 0.95f, 1f), 0.24f);
			Glow.Sparks(c.Front, v.Feet, 24, 150f * u, 480f * u, 0.45f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 300f * u,
				0.02f * u, 0.045f * u, Vector2.Up, 75f);
		});
		Fx.After(c.Front, beat2, () =>
		{
			Flare(c.Front, v.Feet + new Vector2(0f, -th * 0.04f), wid * 1.6f, th * 0.04f, new Color(1f, 0.15f, 0.25f, 0.7f), 0.26f);
			Glow.Sparks(c.Front, v.Feet, 14, 100f * u, 320f * u, 0.4f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 300f * u,
				0.02f * u, 0.04f * u, Vector2.Up, 75f);
		});

		// 红光顺着来时那道弧流回他手里（力量被他抽走）
		for (int i = 0; i < 7; i++)
		{
			float d = hit + 0.04f + 0.05f * i;
			bool white = i % 3 == 1;
			DashFly(c.Front, t => Bez(1f - t), d, 0.3f, jh * 0.5f, jh * (white ? 0.035f : 0.08f), white ? new Color(1f, 0.85f, 0.85f) : new Color(0.95f, 0.06f, 0.15f),
				1.2f, t => Fx.InCubic(t) * 0.4f + t * 0.6f, 0.85f);
			if (i % 2 == 0)
				Fx.After(c.Front, d + 0.29f, () =>
				{
					Glow.Flash(c.Front, hand, new Color(1f, 0.25f, 0.32f), jh * 0.4f, 0.16f);
					Glow.Sparks(c.Front, hand, 8, 40f * u, 160f * u, 0.35f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 0f, 0.015f * u, 0.035f * u, Vector2.Up, 180f);
				});
		}
		SkillRedo.Pulse(c, new Color(1.4f, 0.7f, 0.72f), hit + 0.32f, 0.3f);

		// 它压暗（跟着压的节奏）
		if (v.Tint is { } tint)
		{
			Node2D tk = Fx.Root(c.Front, v.Feet, "P5MalaiseTint", life + 0.1);
			Color dark = new(0.5f, 0.36f, 0.42f);
			Fx.Animate(tk, 0, life, t =>
			{
				float s = t * life;
				float k = Ease5.Seg(s, hit, 0.05f) * (1f - Ease5.Seg(s, beat2 + 0.15f, 0.35f) * 0.85f);
				tint(Colors.White.Lerp(dark, k));
			});
			tk.TreeExiting += () => tint(Colors.White);
		}
	}

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
