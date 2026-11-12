using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 技能牌重做第四批（2026-11-12）：融入暗影、余像、模糊、夹击、萎靡。其中四张金色牌（融入暗影、余像、夹击、萎靡）按「每张一个和牌效最贴的
/// 主瞬间、做得更华丽、但不拖节奏」来做。积木：Vfx/Phantom.cs（预告信卡、拖尾、火花、纸屑）+ 这里的几个着色器（地上的墨池、暗影火舌、长出来的影子、
/// 局部的动态模糊、墨的触手）。音效按拍子配，每张两三拍、主拍最清楚。
/// </summary>
public static class SkillRedo4
{
	public static void Register(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["Shadowmeld"] = Shadowmeld;
		cards["Afterimage"] = Afterimage;
		cards["Blur"] = Blur;
		cards["Flanking"] = Flanking;
		cards["Malaise"] = Malaise;
	}

	private static void S(string name, float vol, float at = 0f) => Sfx.Take(name, vol * 0.8f, at);
	private static float CardW(SkillCtx c) => c.Tall * 0.16f;

	/// <summary>冲击：中心一团柔光（不要任何圈）。</summary>
	private static void Wave(Control layer, Vector2 at, Color tint, float size, float life, float squash)
	{
		Glow.Flash(layer, at, tint, size * 0.45f, life * 0.5f);   // 只留一团柔光（扭曲波在这个尺寸上也看得出一圈边，去掉）
	}

	// ═══════════════════════════ 积木 ═══════════════════════════

	/// <summary>
	/// 冻住的一帧 Joker 剪影（同 SkillFx.Ghosts 的做法：复制此刻的 Joker 画进离屏画面、定格、填纯色），返回剪影贴图的父节点（放在 back 层、区域左上角）。
	/// 拿到以后自己加若干 Sprite2D（Silhouette 材质）摆位置、淡入淡出。
	/// </summary>
	private static (Node2D Root, Texture2D Tex)? Freeze(SkillCtx c, Control layer, double life)
	{
		if (c.CopyBody == null || !GodotObject.IsInstanceValid(layer)) return null;
		Rect2 region = c.Region;
		Vector2I size = new(Mathf.CeilToInt(region.Size.X), Mathf.CeilToInt(region.Size.Y));
		SubViewport view = new() { Size = size, TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Disable3D = true };
		layer.AddChild(view);
		if (c.CopyBody(view) is not { } made)
		{
			view.QueueFree();
			return null;
		}
		made.Clone.Transform = new Transform2D(0f, -region.Position) * made.Body;
		Node2D root = Fx.Root(layer, region.Position, "P5Echo", life + 0.1);
		Fx.FreeAfter(view, life + 0.15);
		Fx.After(layer, 0.06, () => { if (GodotObject.IsInstanceValid(view)) view.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled; });
		return (root, view.GetTexture());
	}

	private static (Sprite2D Sp, ShaderMaterial M) Echo(Node2D root, Texture2D tex, Color tint)
	{
		ShaderMaterial m = new() { Shader = SkillFx.Silhouette };
		m.SetShaderParameter("tint", tint);
		m.SetShaderParameter("alpha", 0f);
		Sprite2D sp = new() { Texture = tex, Centered = false, Material = m };
		root.AddChild(sp);
		return (sp, m);
	}

	private static Shader? _pool;

	/// <summary>地上的墨池：软边的扁椭圆，边缘被噪声撕碎（没有轮廓线），里面近黑、往里一点更深，边上零星透一点暗红的光。</summary>
	private static Shader PoolShader => _pool ??= new Shader { Code = @"
shader_type canvas_item;
uniform float spread = 0.0;   // 0 → 1 铺开
uniform float sheen = 0.0;    // 墨面泛一下红光
uniform float seed = 0.0;
uniform float clock = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; } return v; }
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p);
	float ang = atan(p.y, p.x);
	float edgeN = fbm(vec2(ang * 2.2 + seed, clock * 0.6)) * 0.35 + fbm(p * 5.0 + seed + clock * 0.3) * 0.18;
	float R = spread * (0.78 + edgeN);
	float m = smoothstep(R, R - 0.12, r);
	if (m <= 0.002) discard;
	float depth = smoothstep(R, R * 0.2, r);
	vec3 col = mix(vec3(0.08, 0.015, 0.03), vec3(0.015, 0.004, 0.008), depth);
	float glints = smoothstep(0.62, 0.8, fbm(p * 7.0 + vec2(clock * 0.5, seed))) * (1.0 - depth * 0.7);
	col += vec3(0.55, 0.03, 0.08) * glints * (0.35 + 1.2 * sheen);
	col += vec3(0.4, 0.02, 0.06) * smoothstep(R - 0.25, R - 0.05, r) * m * 0.4 * (0.4 + sheen);
	COLOR = vec4(col, m * 0.92);
}" };

	private static ShaderMaterial Pool(Control layer, Vector2 at, float rx, float ry, out ColorRect rect, double life)
	{
		Node2D root = Fx.Root(layer, at, "P5InkPool", life);
		ShaderMaterial m = new() { Shader = PoolShader };
		m.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		rect = new ColorRect { Size = new Vector2(rx * 2f, ry * 2f), Position = new Vector2(-rx, -ry), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(rect);
		Fx.Clock(rect);
		return m;
	}

	private static Shader? _smear;

	/// <summary>局部的动态模糊（模糊）：读屏幕，在他身边沿 dir 往身后拖出一串残影（越远越淡），带一点红蓝错开；软边椭圆范围。</summary>
	private static Shader SmearShader => _smear ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec2 center = vec2(0.25, 0.6);
uniform vec2 radius = vec2(0.12, 0.3);
uniform vec2 dir = vec2(-0.03, 0.0);   // 往身后拖多远（屏幕 uv）
uniform float strength = 0.0;
void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 d = (uv - center) / radius;
	float m = smoothstep(1.0, 0.45, length(d)) * strength;
	if (m <= 0.002) discard;
	vec3 acc = vec3(0.0); float wsum = 0.0;
	for (int i = 0; i < 14; i++) {
		float f = float(i) / 13.0;
		float w = 1.0 - f * 0.85;
		vec2 o = -dir * f * m;
		acc.r += texture(screen_tex, uv + o * 1.08).r * w;
		acc.g += texture(screen_tex, uv + o).g * w;
		acc.b += texture(screen_tex, uv + o * 0.92).b * w;
		wsum += w;
	}
	vec3 col = acc / wsum;
	col += vec3(0.08, 0.0, 0.015) * m;
	COLOR = vec4(col, smoothstep(0.0, 0.25, m));
}" };

	// ═══════════════════════════ 融入暗影 ═══════════════════════════

	private static Shader? _flame;

	/// <summary>
	/// 暗影的火舌（融入暗影，画在人物后面）：从脚下往上舔的墨色火舌（往上拉长的噪声、越高越碎），边上一道暗红的光、芯里暗红的脉络；
	/// 往上飘的墨点（红芯，一闪一闪）；脚下一片 P5 的红网点（往上越稀）。amt = 整体，height = 火舌能到多高（占画面高），flare = 主拍窜高、红边变亮。
	/// </summary>
	private static Shader FlameShader => _flame ??= new Shader { Code = @"
shader_type canvas_item;
uniform float amt = 0.0;
uniform float height = 0.0;
uniform float flare = 0.0;
uniform float seed = 0.0;
uniform float clock = 0.0;
uniform float aspect = 1.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; } return v; }
void fragment() {
	vec2 uv = UV;
	float x = (uv.x - 0.5) * 2.0;
	float y = 1.0 - uv.y;
	float env = smoothstep(1.0, 0.25, abs(x));
	float n = fbm(vec2(x * 2.4 * aspect, y * 1.5 - clock * 1.15) + seed);
	float n2 = fbm(vec2(x * 6.0 * aspect, y * 3.2 - clock * 2.3) + seed * 1.7);
	float H = max(height, 0.001) * (0.7 + 0.6 * n2);
	float v = n * 0.95 + 0.32 * env - y / H - (1.0 - env) * 0.7;
	float body = smoothstep(0.0, 0.16, v);
	float rim = smoothstep(-0.03, 0.05, v) * (1.0 - smoothstep(0.05, 0.26, v));
	vec3 col = mix(vec3(0.06, 0.012, 0.028), vec3(0.014, 0.003, 0.008), smoothstep(0.1, 0.55, v));
	col += vec3(0.78, 0.04, 0.1) * rim * (0.5 + 0.9 * flare);
	float vein = smoothstep(0.68, 0.9, fbm(vec2(x * 9.0 * aspect, y * 5.0 - clock * 1.8) + seed * 2.3));
	col += vec3(0.55, 0.02, 0.07) * vein * body * (0.35 + flare);
	float a = max(body, rim * 0.85) * 0.9 * smoothstep(0.0, 0.07, y);
	// 往上飘的墨点
	vec2 g = vec2(x * 6.0 * aspect, y * 8.0 - clock * 1.3);
	vec2 cell = floor(g), fr = fract(g) - 0.5;
	float rnd = hash(cell + seed);
	vec2 off = vec2(hash(cell + 3.1) - 0.5, hash(cell + 7.7) - 0.5) * 0.55;
	float sz = 0.06 + 0.08 * hash(cell + 11.0);
	float dm = length(fr - off);
	float mote = smoothstep(sz, sz * 0.35, dm) * step(0.74, rnd) * env * smoothstep(height * 1.35 + 0.05, height * 0.25, y) * smoothstep(0.0, 0.06, y);
	mote *= 0.55 + 0.45 * sin(clock * 6.0 + rnd * 30.0);
	float halo = smoothstep(sz * 2.4, sz, dm) * step(0.74, rnd) * env * smoothstep(height * 1.35 + 0.05, height * 0.25, y) * 0.5;
	col = mix(col, vec3(0.05, 0.01, 0.02), halo * (1.0 - body));
	col = mix(col, vec3(1.0, 0.22, 0.26), mote);
	a = max(a, max(mote, halo * 0.6));
	// 脚下的红网点
	vec2 hp = vec2(uv.x * aspect, uv.y) * 44.0;
	float hr = 0.4 * smoothstep(1.0, 0.25, length(vec2(x * 1.15, (y - 0.05) / 0.11)));
	float dots = smoothstep(hr, hr - 0.07, length(fract(hp) - 0.5)) * step(0.02, hr);
	col = mix(col, vec3(0.82, 0.04, 0.1), dots * (1.0 - body * 0.8));
	a = max(a, dots * 0.45 * smoothstep(0.0, 0.4, height));
	COLOR = vec4(col, a * amt);
}" };

	private static Shader? _rise;

	/// <summary>
	/// 从地上长出来的影子（剪影贴图）：rise 0→1 从脚往头显出来，显出的那条边被噪声撕碎、烧着一道红光；里面脚下深、往上亮，带一点墨的明暗。
	/// feet_v / top_v = 剪影贴图里脚、头的 v 坐标。
	/// </summary>
	private static Shader RiseShader => _rise ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec4 tint : source_color = vec4(0.92, 0.05, 0.12, 1.0);
uniform vec4 deep : source_color = vec4(0.2, 0.0, 0.03, 1.0);
uniform float alpha = 1.0;
uniform float rise = 1.0;
uniform float feet_v = 0.885;
uniform float top_v = 0.1;
uniform float clock = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; } return v; }
void fragment() {
	float a = smoothstep(0.45, 0.6, texture(TEXTURE, UV).a);
	float hgt = (feet_v - UV.y) / (feet_v - top_v);
	float n = fbm(vec2(UV.x * 16.0, UV.y * 9.0 - clock * 1.4));
	float edge = rise * 1.2 - hgt + (n - 0.5) * 0.2;
	float m = smoothstep(0.0, 0.03, edge);
	float burn = smoothstep(-0.005, 0.015, edge) * (1.0 - smoothstep(0.015, 0.08, edge)) * step(rise, 0.999);
	vec3 col = mix(deep.rgb, tint.rgb, smoothstep(-0.05, 0.85, hgt));
	col *= 0.78 + 0.4 * fbm(vec2(UV.x * 7.0, UV.y * 5.0 + clock * 0.5));
	col = mix(col, vec3(1.0, 0.42, 0.38), burn);
	COLOR = vec4(col, a * alpha * max(m, burn));
}" };

	/// <summary>溅起的墨珠：近黑的圆珠（顺着速度拉长）、一侧一点暗红的反光，抛物线落下、越落越小。</summary>
	private static void InkSplash(Control layer, Vector2 at, int n, float speedMin, float speedMax, Vector2 dir, float spread, float g, float size, float life, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5InkSplash", delay + life + 0.1);
		var parts = new List<(Node2D N, Vector2 V, float S)>();
		for (int i = 0; i < n; i++)
		{
			float a = dir.Angle() + Mathf.DegToRad(Fx.Rand(-spread, spread));
			Node2D d = new();
			root.AddChild(d);
			Fx.Poly(d, Fx.Circle(1f, 14), new Color(0.045f, 0.01f, 0.018f, 0.95f));
			Polygon2D hl = Fx.Poly(d, Fx.Circle(0.38f, 10), new Color(0.78f, 0.05f, 0.11f, 0.85f));
			hl.Position = new Vector2(0.22f, -0.38f);
			parts.Add((d, Vector2.Right.Rotated(a) * Fx.Rand(speedMin, speedMax), size * Fx.Rand(0.45f, 1.2f)));
		}
		root.Visible = false;
		Fx.Animate(root, delay, life, t =>
		{
			root.Visible = true;
			float s = t * life;
			foreach (var (d, v0, sz) in parts)
			{
				Vector2 vel = v0 + new Vector2(0f, g * s);
				d.Position = v0 * s + new Vector2(0f, 0.5f * g * s * s);
				d.Rotation = vel.Angle();
				float st = 1f + Mathf.Clamp(vel.Length() / 900f, 0f, 1.4f);
				d.Scale = new Vector2(sz * st, sz / Mathf.Sqrt(st)) * (1f - 0.55f * t);
			}
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.7f, 0.3f));
		});
	}

	/// <summary>
	/// 融入暗影（本回合获得的格挡翻倍）（用户 2026-11-12：太简单，要更丰富）：
	/// ① 脚下「哗」地铺开一池墨（边被撕碎、零星透红光），身后从墨池里舔起一片墨色火舌（红边、红脉络、往上飘的红芯墨点、脚下红网点），
	///    六缕墨从池边盘着他往上升；人沉成一道暗影。
	/// ② 主拍：他的影子从墨池里「长」出来——身后两层剪影从脚往头显出（显出的边烧着红光，脚下深、往上亮），面具一点红光，火舌一下窜高，墨池溅起墨珠。
	/// ③ 第二拍（翻倍）：两层影子「啪」地撞回他身上——白闪、红光、他整个人亮一下，碎光和纸屑迸开、墨珠往四周溅；
	/// ④ 火舌沉回地面、墨池缩回脚下。约 1.4s。拍子：墨铺开（风）→ 影子长出来（噗，主拍）→ 撞回来翻倍（叮）。
	/// </summary>
	public static void Shadowmeld(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		const float rise = 0.4f, twice = 0.72f, life = 1.4f;
		Control back = c.Back ?? c.Front;
		S("gx_whoosh", 0.16f);
		S("gx_pop", 0.28f, rise);
		S("gx_ping", 0.16f, twice);
		// 墨池
		ShaderMaterial pool = Pool(back, c.Feet + new Vector2(0f, h * 0.01f), h * 0.9f, h * 0.21f, out ColorRect poolRect, life + 0.1);
		Fx.Animate(poolRect, 0, life, t =>
		{
			float s = t * life;
			float sp = Ease5.OutQuint(Mathf.Clamp(s / 0.3f, 0f, 1f)) * (1f - Mathf.Pow(Ease5.Seg(s, 1.0f, 0.38f), 2f));
			pool.SetShaderParameter("spread", sp * (1f + 0.08f * Mathf.Exp(-Mathf.Max(0f, s - twice) * 8f) * (s >= twice ? 1f : 0f)));
			float sh = Mathf.Exp(-Mathf.Max(0f, s - rise) * 6f) * (s >= rise ? 1f : 0f) + 0.8f * Mathf.Exp(-Mathf.Max(0f, s - twice) * 7f) * (s >= twice ? 1f : 0f);
			pool.SetShaderParameter("sheen", sh);
		});
		// 身后的墨色火舌
		float fw = h * 1.6f, fh = h * 1.3f;
		Node2D flameRoot = Fx.Root(back, c.Feet + new Vector2(0f, h * 0.09f), "P5MeldFlame", life + 0.1);
		ShaderMaterial fm = new() { Shader = FlameShader };
		fm.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		fm.SetShaderParameter("aspect", fw / fh);
		var flame = new ColorRect { Size = new Vector2(fw, fh), Position = new Vector2(-fw / 2f, -fh), Material = fm, MouseFilter = Control.MouseFilterEnum.Ignore };
		flameRoot.AddChild(flame);
		Fx.Clock(flame);
		Fx.Animate(flame, 0, life, t =>
		{
			float s = t * life;
			float grow = Ease5.OutQuint(Ease5.Seg(s, 0.02f, 0.4f));
			float sink = Ease5.Seg(s, 0.95f, 0.42f);
			sink *= sink;
			float fl = (s >= rise ? Mathf.Exp(-(s - rise) * 4.5f) : 0f) + 0.75f * (s >= twice ? Mathf.Exp(-(s - twice) * 6f) : 0f);
			fm.SetShaderParameter("amt", Mathf.Min(1f, s / 0.08f) * (1f - sink));
			fm.SetShaderParameter("height", (0.4f * grow + 0.32f * fl) * (1f - 0.85f * sink));
			fm.SetShaderParameter("flare", fl);
		});
		// 墨缕：从池边盘着往上升（螺旋），升到头顶附近散掉
		for (int i = 0; i < 6; i++)
		{
			int k = i;
			float a0 = Mathf.Tau * k / 6f + Fx.Rand(-0.3f, 0.3f), turns = Fx.Rand(0.7f, 1.1f) * (k % 2 == 0 ? 1f : -1f);
			float top = h * Fx.Rand(0.75f, 1.05f), rad = h * Fx.Rand(0.26f, 0.36f), d = 0.04f + 0.03f * k;
			var rib = new Phantom.Ribbon(c.Front, h * Fx.Rand(0.05f, 0.07f), d + 0.75, 14, ink: true);
			Node2D tick = Fx.Root(c.Front, c.Feet, "P5InkWisp", d + 0.75);
			Fx.Animate(tick, d, 0.6, t =>
			{
				float e = Ease5.OutQuint(t);
				float a = a0 + turns * Mathf.Tau * e;
				float rr = rad * (1f - 0.55f * e);
				Vector2 p = c.Feet + new Vector2(Mathf.Cos(a) * rr, -top * e + Mathf.Sin(a) * rr * 0.22f);
				rib.Fade = 1f - Ease5.Seg(t, 0.65f, 0.35f);
				rib.Push(p);
			});
			Fx.After(c.Front, d + 0.6f, rib.Kill);
		}
		// 人沉成暗影；翻倍那一下整个人亮一下
		if (c.SelfTint is { } tint)
		{
			Node2D tk = Fx.Root(c.Front, c.Feet, "P5MeldTint", life + 0.1);
			Color dark = new(0.16f, 0.1f, 0.16f), bright = new(1.7f, 0.75f, 0.78f);
			Fx.Animate(tk, 0, life, t =>
			{
				float s = t * life;
				float k = Ease5.Seg(s, 0.08f, 0.22f) * (1f - Ease5.Seg(s, 0.95f, 0.4f));
				float flash = s >= twice ? Mathf.Exp(-(s - twice) * 9f) : 0f;
				tint(Colors.White.Lerp(dark, k * 0.9f).Lerp(bright, flash * 0.85f));
			});
			tk.TreeExiting += () => tint(Colors.White);
		}
		// 主拍：影子从墨池里长出来
		Fx.After(c.Front, rise, () =>
		{
			Glow.Glint(c.Front, c.Mask + new Vector2(f * h * 0.03f, 0f), 0.9f);
			Glow.Flash(c.Front, c.Feet + new Vector2(0f, -h * 0.05f), new Color(0.75f, 0.04f, 0.1f), h * 0.9f, 0.22f);
			Fx.Shake(1);
			InkSplash(c.Front, c.Feet + new Vector2(0f, -h * 0.02f), 14, 450f * u, 950f * u, Vector2.Up, 65f, 2400f * u, 7f * u, 0.65f);
			Phantom.Shards(c.Front, c.Feet + new Vector2(0f, -h * 0.05f), 10, 250f * u, 650f * u, Vector2.Up, 70f, 0.35f, 200f * u, 16f * u);
			if (Freeze(c, back, life - rise) is { } fr)
			{
				float fv = 1.15f / 1.3f, tv = 0.12f / 1.3f;
				var layers = new (Color Tint, Color Deep, float Dx, float Dy, float A)[]
				{
					(new Color(0.42f, 0.02f, 0.06f), new Color(0.05f, 0f, 0.01f), -f * h * 0.1f, h * 0.022f, 0.9f),
					(new Color(0.95f, 0.06f, 0.12f), new Color(0.22f, 0f, 0.03f), -f * h * 0.05f, h * 0.01f, 1f),
				};
				foreach (var (lt, ld, dx, dy, a0) in layers)
				{
					ShaderMaterial m = new() { Shader = RiseShader };
					m.SetShaderParameter("tint", lt);
					m.SetShaderParameter("deep", ld);
					m.SetShaderParameter("feet_v", fv);
					m.SetShaderParameter("top_v", tv);
					m.SetShaderParameter("rise", 0f);
					Sprite2D sp = new() { Texture = fr.Tex, Centered = false, Material = m };
					fr.Root.AddChild(sp);
					Fx.Clock(sp);
					float slam = twice - rise;
					Fx.Animate(sp, 0, life - rise, t =>
					{
						float s = t * (life - rise);
						m.SetShaderParameter("rise", Fx.OutCubic(Mathf.Clamp(s / 0.2f, 0f, 1f)));
						float e = Ease5.OutQuint(Mathf.Clamp(s / 0.16f, 0f, 1f));
						float back2 = Fx.InCubic(Ease5.Seg(s, slam - 0.08f, 0.08f));
						float sway = Mathf.Sin(s * 7f) * h * 0.004f;
						sp.Position = new Vector2((dx + sway) * e * (1f - back2), dy * e * (1f - back2));
						m.SetShaderParameter("alpha", a0 * (s >= slam ? Mathf.Max(0f, 1f - (s - slam) / 0.1f) : 1f));
					});
				}
			}
		});
		// 第二拍：两层影子撞回他身上——翻倍
		Fx.After(c.Front, twice, () =>
		{
			Glow.Flash(c.Front, c.Chest, Phantom.Hot, h * 0.75f, 0.12f);
			Glow.Flash(c.Front, c.Chest, Phantom.Red, h * 1.1f, 0.22f);
			Glow.Glint(c.Front, c.Chest + new Vector2(f * h * 0.04f, -h * 0.06f), 0.8f);
			Phantom.Shards(c.Front, c.Chest, 16, 400f * u, 1000f * u, Vector2.Up, 180f, 0.35f, 250f * u, 18f * u);
			Phantom.Confetti(c.Front, c.Chest, 14, 320f * u, Vector2.Up, 160f, 0.8f, 6f * u);
			InkSplash(c.Front, c.Feet + new Vector2(0f, -h * 0.02f), 16, 350f * u, 800f * u, Vector2.Up, 85f, 2400f * u, 6f * u, 0.6f);
			Fx.Shake(1);
		});
	}

	// ═══════════════════════════ 余像 ═══════════════════════════

	/// <summary>
	/// 余像（能力：之后每打出一张牌获得 1 点格挡）（用户 2026-11-12：重影太多，只要残影那种一层红色剪影；关键是打出以后每张牌的表现）：
	/// 打出：他身后错开一层纯红剪影（贴着往后滑出一段、轻轻浮动），停一下，「嗖」地收回身上，合上的瞬间胸口一闪、几点碎光。约 0.9s。
	/// 之后每张牌：AfterimageTick。拍子：滑出（风）→ 合上（叮，主拍）。
	/// </summary>
	public static void Afterimage(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		const float outT = 0.14f, merge = 0.7f, life = 0.9f;
		S("gx_whoosh", 0.16f);
		S("gx_ping", 0.2f, merge);
		if (Freeze(c, c.Back ?? c.Front, life) is { } fr)
		{
			var (sp, m) = Echo(fr.Root, fr.Tex, new Color(0.95f, 0.04f, 0.1f));
			float dx = -f * h * 0.075f, dy = h * 0.015f;
			Fx.Animate(sp, 0, life, t =>
			{
				float s = t * life;
				float eo = Ease5.OutQuint(Mathf.Clamp(s / outT, 0f, 1f));
				float eb = Fx.InCubic(Ease5.Seg(s, merge - 0.13f, 0.13f));
				float bob = Mathf.Sin(s * 6f) * h * 0.004f;
				sp.Position = new Vector2(dx * eo * (1f - eb), (dy + bob) * eo * (1f - eb));
				m.SetShaderParameter("alpha", 0.95f * Mathf.Min(1f, s / 0.04f) * (s > merge ? Mathf.Max(0f, 1f - (s - merge) / 0.08f) : 1f));
			});
		}
		Fx.After(c.Front, merge, () =>
		{
			Glow.Flash(c.Front, c.Chest, Phantom.Red, h * 0.7f, 0.18f);
			Glow.Flash(c.Front, c.Chest, Phantom.Hot, h * 0.32f, 0.1f);
			Phantom.Shards(c.Front, c.Chest, 8, 300f * u, 700f * u, new Vector2(-f, -0.3f), 70f, 0.28f, 200f * u, 14f * u);
		});
		SkillRedo.Pulse(c, new Color(1.45f, 0.7f, 0.72f), merge, 0.3f);
	}

	/// <summary>
	/// 余像生效（之后每打出一张牌、获得那 1 点格挡的一刻）：身上剥下一层很淡的红色剪影，往身后滑开一点就散掉，胸口一点很小的红光。0.4s，很轻（出牌很频繁）。
	/// </summary>
	public static void AfterimageTick(SkillCtx c)
	{
		float h = c.Tall, f = c.Face;
		const float life = 0.4f;
		S("gx_tick", 0.06f);
		if (Freeze(c, c.Back ?? c.Front, life) is { } fr)
		{
			var (sp, m) = Echo(fr.Root, fr.Tex, new Color(0.9f, 0.04f, 0.1f));
			Fx.Animate(sp, 0, life, t =>
			{
				float e = Ease5.OutQuint(t);
				sp.Position = new Vector2(-f * h * 0.055f * e, h * 0.008f * e);
				m.SetShaderParameter("alpha", 0.75f * Mathf.Min(1f, t * life / 0.03f) * (1f - t) * (1f - t));
			});
		}
		Glow.Flash(c.Front, c.Chest, new Color(0.9f, 0.08f, 0.14f), h * 0.32f, 0.14f);
	}

	// ═══════════════════════════ 模糊 ═══════════════════════════

	/// <summary>
	/// 模糊（格挡 + 下回合格挡不消失；格挡本身是蜂巢护盾）：他身上一下「糊」开——读屏幕把他往身后拖出一串动态模糊（带一点红蓝错开），
	/// 身后两层贴身的暗红剪影一闪即逝，几道白色速度线往身后掠过；0.35s 时胸口一下「叮」的闪光（格挡留住）。约 0.7s。
	/// 拍子：糊开（风，主拍）→ 留住（叮）。
	/// </summary>
	public static void Blur(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		S("gx_whoosh", 0.24f);
		S("gx_ping", 0.12f, 0.36f);
		// 局部动态模糊（前景层，先重新拷一次屏幕带上人物）
		Node2D root = Fx.ScreenRoot(c.Front, "P5BlurSmear");
		Fx.FreeAfter(root, 0.6);
		root.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		ShaderMaterial m = new() { Shader = SmearShader };
		var rect = new ColorRect { Size = Fx.Screen(c.Front), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(rect);
		Vector2 scr = Fx.Screen(c.Front);
		Vector2 cs = Fx.ScreenPoint(c.Front, c.Feet + new Vector2(0f, -h * 0.5f));
		float tallS = h * c.Front.GetCanvasTransform().Scale.Y;
		m.SetShaderParameter("center", new Vector2(cs.X / scr.X, cs.Y / scr.Y));
		m.SetShaderParameter("radius", new Vector2(tallS * 0.55f / scr.X, tallS * 0.68f / scr.Y));
		m.SetShaderParameter("dir", new Vector2(-f * tallS * 0.22f / scr.X, 0f));
		Fx.Animate(rect, 0, 0.55, t =>
		{
			float s = t * 0.55f;
			m.SetShaderParameter("strength", Mathf.Min(1f, s / 0.05f) * Mathf.Exp(-Mathf.Max(0f, s - 0.08f) * 6f));
		});
		// 两层贴身剪影
		if (Freeze(c, c.Back ?? c.Front, 0.45) is { } fr)
		{
			for (int i = 1; i >= 0; i--)
			{
				int k = i;
				var (sp, em) = Echo(fr.Root, fr.Tex, k == 0 ? new Color(0.8f, 0.03f, 0.09f) : new Color(0.35f, 0f, 0.05f));
				Fx.Animate(sp, 0, 0.42, t =>
				{
					float e = Ease5.OutQuint(Mathf.Min(1f, t * 3f));
					sp.Position = new Vector2(-f * h * (0.05f + 0.06f * k) * e, 0f);
					em.SetShaderParameter("alpha", (0.8f - 0.2f * k) * (1f - t));
				});
			}
		}
		// 往身后掠过的速度线（白芯红晕，很细、很快）
		for (int i = 0; i < 6; i++)
		{
			Vector2 at = c.Feet + new Vector2(f * h * Fx.Rand(-0.05f, 0.15f), -h * Fx.Rand(0.15f, 0.95f));
			Phantom.Shards(c.Front, at, 1, 1200f * u, 1700f * u, new Vector2(-f, 0f), 2f, 0.18f, 0f, Fx.Rand(30f, 55f) * u, 0.02f * i);
		}
		Fx.After(c.Front, 0.36f, () =>
		{
			Glow.Glint(c.Front, c.Chest + new Vector2(f * h * 0.05f, -h * 0.05f), 0.8f);
			Wave(c.Front, c.Chest, Phantom.Hot, h * 1.2f, 0.3f, 0.85f);
		});
	}

	// ═══════════════════════════ 夹击 ═══════════════════════════

	/// <summary>
	/// 夹击（本回合其他玩家对它造成双倍伤害）：两张预告信卡从画面左右两边打着转飞来，「啪」地停在目标两侧；主拍：两张卡同时朝对角斩出一道刀光，
	/// 在目标身上交叉成 X（顿帧、白闪、目标泛红、碎光往两边迸）；四个角括号扣上锁住目标，两张卡随即烧掉碎成纸屑。约 1.0s。
	/// 拍子：飞来（滑动）→ 交叉斩（刀，主拍）→ 卡烧掉（很轻的烧）。
	/// </summary>
	public static void Flanking(SkillCtx c)
	{
		if (c.Target is not SkillFx.Victim v) return;
		float h = Mathf.Max(120f, (v.Feet - v.Top).Length()), u = c.Unit, w = CardW(c) * 1.15f;
		Vector2 at = v.Feet.Lerp(v.Top, 0.5f);
		float half = Mathf.Max(v.Width * 0.5f, h * 0.35f) * 1.25f;
		const float fly = 0.26f, cross = 0.32f;
		S("gx_card_slide", 0.2f);
		S("gx_slice", 0.32f, cross);
		S("gx_burn", 0.14f, cross + 0.2f);
		var cards = new List<(Phantom.P5Card C, Vector2 Stop, float Side)>();
		for (int i = 0; i < 2; i++)
		{
			float side = i == 0 ? -1f : 1f;
			Vector2 from = at + new Vector2(side * half * 3.2f, -h * 0.55f), stop = at + new Vector2(side * half, -h * 0.28f);
			Phantom.P5Card card = Phantom.Card(c.Front, from, w, cross + 0.8, 0.7f);
			float sd = side;
			Fx.Animate(card.Root, 0, fly, t =>
			{
				float e = Ease5.OutQuint(t);
				card.Place(from.Lerp(stop, e), -sd * 0.35f * e, Mathf.Pi * (1f - e) * 5f, 1f);
			});
			Fx.Animate(card.Root, fly, cross - fly, t => card.Place(stop + new Vector2(0f, Mathf.Sin(t * 9f) * h * 0.01f), -sd * 0.35f, 0f, 1f + 0.06f * t));
			cards.Add((card, stop, side));
		}
		Fx.After(c.Front, fly, () => { foreach (var cc in cards) cc.C.Trail?.Kill(); });
		Fx.After(c.Front, cross, () =>
		{
			foreach (var (card, stop, side) in cards)
			{
				Vector2 to = at + new Vector2(-side * half, h * 0.32f);
				float ang = (to - stop).Angle();
				Cut.Razor(c.Front, stop.Lerp(to, 0.5f), ang, stop.DistanceTo(to) * 1.25f, 6f * u, 0.22f);
				Glow.Blade(c.Front, stop.Lerp(to, 0.5f), ang, stop.DistanceTo(to) * 1.1f, 20f * u, 0.3f);
				Phantom.Shards(c.Front, at, 8, 300f * u, 900f * u, new Vector2(-side, -0.3f), 35f, 0.32f, 300f * u, 22f * u);
				card.Set("glow", 1f);
			}
			HitStop.Request(0.05);
			Fx.Shake(1);
			Glow.Flash(c.Front, at, Phantom.Hot, h * 0.9f, 0.12f);
			Glow.Glint(c.Front, at, 1f);
			if (v.Tint is { } tint)
			{
				Node2D tk = Fx.Root(c.Front, at, "P5FlankTint", 0.6);
				Fx.Animate(tk, 0, 0.5, t => tint(Colors.White.Lerp(new Color(1.7f, 0.45f, 0.5f), Mathf.Exp(-t * 6f))));
				tk.TreeExiting += () => tint(Colors.White);
			}
			for (int k = 0; k < 4; k++)
			{
				float ang = Mathf.Pi * 0.25f + Mathf.Pi * 0.5f * k;
				SkillRedo.Brackets(c.Front, at + new Vector2(Mathf.Cos(ang) * half * 0.75f, Mathf.Sin(ang) * h * 0.42f), ang, w * 0.5f, u);
			}
		});
		Fx.After(c.Front, cross + 0.12f, () =>
		{
			foreach (var (card, stop, side) in cards)
				SkillRedo.BurnAway(c, card, stop, -side * 0.35f, new Vector2(side * h * 0.12f, -h * 0.08f));
		});
	}

	// ═══════════════════════════ 萎靡 ═══════════════════════════

	private static Shader? _siphon;

	/// <summary>
	/// 墨的触手（萎靡）：从 Joker 手上（a）沿一条弧线（控制点 cp）伸到目标身上（b），在目标身上分出四根爪（cc 控制点、ce 末端）扣住它。
	/// 触手：墨色、边被噪声撕碎、越近目标越粗；芯是暗红，flow 时一串红光沿着它从目标往手那边流；伸出去时头上一点红光。手上一团翻卷的墨（knot）。
	/// head / tail = 触手沿长度显出的范围（tail 往前推 = 从手那头散掉），grip = 爪子伸出多少。坐标都是这块画面里的像素。
	/// </summary>
	private static Shader SiphonShader => _siphon ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 size = vec2(100.0);
uniform vec2 a = vec2(0.0);
uniform vec2 b = vec2(0.0);
uniform vec2 cp = vec2(0.0);
uniform vec2 ce[4];
uniform vec2 cc[4];
uniform float wid = 10.0;
uniform float head = 0.0;
uniform float tail = 0.0;
uniform float grip = 0.0;
uniform float flow = 0.0;
uniform float knot = 0.0;
uniform float fade = 1.0;
uniform float seed = 0.0;
uniform float clock = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, w = 0.5; for (int i = 0; i < 4; i++) { v += w * noise(p); p *= 2.03; w *= 0.5; } return v; }
vec2 bez(vec2 p0, vec2 c, vec2 p1, float t) { float u = 1.0 - t; return u * u * p0 + 2.0 * u * t * c + t * t * p1; }
// 到二次贝塞尔的距离（分段近似），返回 (距离, 沿长度 t)
vec2 dist_main(vec2 p, vec2 p0, vec2 c, vec2 p1) {
	float best = 1e9, bt = 0.0; vec2 prev = p0;
	for (int i = 1; i <= 28; i++) {
		float t = float(i) / 28.0; vec2 cur = bez(p0, c, p1, t); vec2 e = cur - prev;
		float hh = clamp(dot(p - prev, e) / max(dot(e, e), 1e-4), 0.0, 1.0); float d = length(p - prev - e * hh);
		if (d < best) { best = d; bt = (float(i - 1) + hh) / 28.0; } prev = cur;
	}
	return vec2(best, bt);
}
vec2 dist_claw(vec2 p, vec2 p0, vec2 c, vec2 p1) {
	float best = 1e9, bt = 0.0; vec2 prev = p0;
	for (int i = 1; i <= 10; i++) {
		float t = float(i) / 10.0; vec2 cur = bez(p0, c, p1, t); vec2 e = cur - prev;
		float hh = clamp(dot(p - prev, e) / max(dot(e, e), 1e-4), 0.0, 1.0); float d = length(p - prev - e * hh);
		if (d < best) { best = d; bt = (float(i - 1) + hh) / 10.0; } prev = cur;
	}
	return vec2(best, bt);
}
void fragment() {
	vec2 p = UV * size;
	vec3 inkc = vec3(0.035, 0.008, 0.016);
	// 主触手
	vec2 st = dist_main(p, a, cp, b);
	float t = st.y;
	float r = wid * (0.45 + 0.55 * smoothstep(0.0, 0.85, t)) * (0.85 + 0.3 * noise(vec2(t * 14.0 - clock * 3.0, seed)));
	float rag = (fbm(vec2(t * 24.0 - clock * 2.5, seed + 3.0)) - 0.5) * 0.75;
	float vis = smoothstep(tail, tail + 0.06, t) * (1.0 - smoothstep(head - 0.015, head, t));
	float m = smoothstep(1.2, -1.2, st.x - r * (1.0 + rag)) * vis;
	float k = clamp(1.0 - st.x / max(r, 1.0), 0.0, 1.0);
	float pl = pow(0.5 + 0.5 * sin((t * 4.5 + clock * 2.8) * 6.2832), 5.0) * flow;
	vec3 col = inkc + vec3(0.5, 0.02, 0.07) * smoothstep(0.45, 1.0, k);
	col += vec3(1.0, 0.18, 0.22) * pl * smoothstep(0.2, 1.0, k);
	col += vec3(1.0, 0.25, 0.3) * exp(-abs(t - head) * 50.0) * (1.0 - grip) * smoothstep(0.25, 1.0, k);
	// 四根爪
	for (int i = 0; i < 4; i++) {
		vec2 s2 = dist_claw(p, b, cc[i], ce[i]);
		float tt = s2.y;
		float rr = wid * 0.7 * (1.0 - tt * 0.8);
		float rg = (fbm(vec2(tt * 12.0 + float(i) * 7.0, clock * 1.5 + seed)) - 0.5) * 0.6;
		float mc = smoothstep(1.2, -1.2, s2.x - rr * (1.0 + rg)) * (1.0 - smoothstep(grip - 0.05, grip, tt)) * step(0.001, grip);
		float kk = clamp(1.0 - s2.x / max(rr, 1.0), 0.0, 1.0);
		vec3 c2 = inkc + vec3(0.55, 0.02, 0.07) * smoothstep(0.4, 1.0, kk)
			+ vec3(1.0, 0.2, 0.25) * flow * 0.6 * smoothstep(0.45, 1.0, kk) * (0.5 + 0.5 * sin(clock * 14.0 + float(i) * 1.7 - tt * 8.0));
		col = mix(col, c2, step(m, mc));
		m = max(m, mc);
	}
	// 手上翻卷的一团墨
	vec2 dk = p - a;
	float ang = atan(dk.y, dk.x);
	float rk = wid * 1.7 * knot * (0.7 + 0.6 * fbm(vec2(ang * 1.6 + clock * 4.0, length(dk) * 0.06 - clock * 2.0) + seed));
	float mk = smoothstep(rk, rk * 0.5, length(dk)) * step(0.001, knot);
	vec3 kc = inkc + vec3(0.75, 0.04, 0.1) * smoothstep(rk * 0.55, 0.0, length(dk)) * (0.5 + flow);
	col = mix(col, kc, step(m, mk));
	m = max(m, mk);
	if (m <= 0.003) discard;
	COLOR = vec4(col, m * 0.95 * fade);
}" };

	/// <summary>被抽走的红光：沿 path(1→0) 从目标飞回 Joker 手上的小光点（白芯红晕、顺着走向拉长），一颗接一颗；到了手上 arrive(i)。</summary>
	private static void Essence(Control layer, Func<float, Vector2> path, int n, float start, float gap, float travel, float len, Action<int>? arrive)
	{
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float delay = start + gap * k, jit = Fx.Rand(-1f, 1f), sz = len * Fx.Rand(0.7f, 1.2f);
			Node2D r = Fx.Root(layer, path(1f), "P5Essence", delay + travel + 0.1);
			Polygon2D glow = Fx.Poly(r, new[] { new Vector2(0f, -1f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0f) }, new Color(1f, 0.15f, 0.2f, 0.85f), additive: true);
			Polygon2D core = Fx.Poly(r, new[] { new Vector2(0f, -1f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0f) }, Phantom.Hot, additive: true);
			r.Visible = false;
			Fx.Animate(r, delay, travel, t =>
			{
				r.Visible = true;
				float e = t * t * (3f - 2f * t) * 0.6f + t * t * 0.4f;
				Vector2 p = path(1f - e), q = path(Mathf.Max(0f, 1f - e - 0.04f));
				Vector2 d = (q - p).Normalized();
				r.GlobalPosition = p + new Vector2(-d.Y, d.X) * jit * sz * 1.2f * Mathf.Sin(t * Mathf.Pi);
				r.Rotation = d.Angle();
				float l = sz * (0.6f + 1.2f * Mathf.Sin(t * Mathf.Pi));
				glow.Scale = new Vector2(l, sz * 0.28f);
				core.Scale = new Vector2(l * 0.7f, sz * 0.1f);
				r.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t / 0.1f) * (1f - Ease5.Seg(t, 0.85f, 0.15f)));
			});
			if (arrive != null) Fx.After(layer, delay + travel, () => arrive(k));
		}
	}

	/// <summary>
	/// 萎靡（失去力量 + 虚弱）（用户 2026-11-12：第一版太差、看不出和人物的关系——重做）：
	/// ① 他手一甩，手上翻起一团墨，一根墨的触手沿着弧线「嗖」地伸向目标（头上一点红光），在目标身上分出四根爪扣住它；
	/// ② 主拍：他一握——触手绷直，目标一下发暗，一串红光（它的力量）沿着触手从目标身上被抽回他手里，芯里的红光一路往回流；
	/// ③ 第二拍（虚弱）：他一扯，触手从手那头散掉，爪子顺着目标往下滑进它脚下摊开的一滩墨里、墨珠溅起，目标被压暗一阵；他身上一闪红（力量收下了）。约 1.3s。
	/// 拍子：伸出去（风）→ 抽走（噗，主拍）→ 压下（叮）。
	/// </summary>
	public static void Malaise(SkillCtx c)
	{
		if (c.Target is not SkillFx.Victim v) return;
		float h = Mathf.Max(120f, (v.Feet - v.Top).Length()), u = c.Unit, jh = c.Tall;
		Vector2 tc = v.Feet.Lerp(v.Top, 0.52f), hand = c.Hand;
		float wid = Mathf.Max(v.Width, h * 0.6f);
		const float reach = 0.2f, drain = 0.36f, snap = 0.8f, life = 1.3f;
		S("gx_whoosh", 0.15f);
		S("gx_pop", 0.26f, drain);
		S("gx_ping", 0.13f, snap);
		float dist = hand.DistanceTo(tc);
		Vector2 mid = hand.Lerp(tc, 0.5f), ctrl = mid + new Vector2(0f, -dist * 0.3f);
		var claws = new (Vector2 End, Vector2 Bend)[]
		{
			(new Vector2(-wid * 0.42f, -h * 0.4f), new Vector2(-wid * 0.3f, -h * 0.08f)),
			(new Vector2(wid * 0.44f, -h * 0.32f), new Vector2(wid * 0.12f, -h * 0.3f)),
			(new Vector2(-wid * 0.38f, h * 0.34f), new Vector2(-wid * 0.08f, h * 0.3f)),
			(new Vector2(wid * 0.36f, h * 0.42f), new Vector2(wid * 0.34f, h * 0.06f)),
		};
		Rect2 box = new Rect2(hand, Vector2.Zero).Expand(tc).Expand(ctrl).Grow(Mathf.Max(h * 0.65f, jh * 0.25f));
		Node2D root = Fx.Root(c.Front, box.Position, "P5Siphon", life + 0.1);
		ShaderMaterial m = new() { Shader = SiphonShader };
		var rect = new ColorRect { Size = box.Size, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(rect);
		Fx.Clock(rect);
		m.SetShaderParameter("size", box.Size);
		m.SetShaderParameter("a", hand - box.Position);
		m.SetShaderParameter("wid", jh * 0.065f);
		m.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		Vector2 cur = ctrl, body = tc;
		Vector2 Path(float t)
		{
			float w = 1f - t;
			return w * w * hand + 2f * w * t * cur + t * t * body;
		}
		Fx.Animate(rect, 0, life, t =>
		{
			float s = t * life;
			float taut = Ease5.Seg(s, drain, 0.07f) * (1f - Ease5.Seg(s, snap, 0.06f));
			float slack = Fx.OutCubic(Ease5.Seg(s, snap, 0.3f));
			float drop = Fx.OutCubic(Ease5.Seg(s, snap, 0.32f));
			float tighten = Fx.OutCubic(Ease5.Seg(s, drain, 0.12f));
			cur = ctrl.Lerp(mid, 0.7f * taut) + new Vector2(0f, Mathf.Sin(s * 9f) * dist * 0.02f * (1f - taut) + dist * 0.25f * slack);
			body = tc + new Vector2(0f, h * 0.45f * drop);
			m.SetShaderParameter("cp", cur - box.Position);
			m.SetShaderParameter("b", body - box.Position);
			var ce = new Vector2[4];
			var cc = new Vector2[4];
			for (int i = 0; i < 4; i++)
			{
				float pull = 1f - 0.2f * tighten;
				Vector2 e = claws[i].End * pull, bend = claws[i].Bend * pull;
				e.Y = Mathf.Lerp(e.Y, h * 0.5f - h * 0.45f * drop, drop * 0.7f);
				ce[i] = tc + e - box.Position + new Vector2(0f, h * 0.45f * drop * 0.3f);
				cc[i] = tc + bend - box.Position + new Vector2(0f, h * 0.45f * drop * 0.5f);
			}
			m.SetShaderParameter("ce", ce);
			m.SetShaderParameter("cc", cc);
			m.SetShaderParameter("head", Fx.OutCubic(Ease5.Seg(s, 0f, reach)));
			m.SetShaderParameter("tail", Fx.OutCubic(Ease5.Seg(s, snap, 0.24f)) * 1.06f);
			m.SetShaderParameter("grip", Ease5.OutQuint(Ease5.Seg(s, reach - 0.04f, 0.16f)));
			m.SetShaderParameter("flow", Ease5.Seg(s, drain - 0.03f, 0.06f) * (1f - Ease5.Seg(s, snap, 0.12f)));
			m.SetShaderParameter("knot", Ease5.OutQuint(Ease5.Seg(s, 0f, 0.12f)) * (1f - Ease5.Seg(s, snap, 0.18f)));
			m.SetShaderParameter("fade", Mathf.Min(1f, s / 0.04f) * (1f - Ease5.Seg(s, snap + 0.22f, 0.26f)));
		});
		// 目标发暗：主拍压暗，抽的时候一下下泛红，第二拍再压一下，最后恢复
		if (v.Tint is { } tint)
		{
			Node2D tk = Fx.Root(c.Front, tc, "P5MalaiseTint", life + 0.1);
			Color dark = new(0.36f, 0.28f, 0.4f), hurt = new(1.25f, 0.42f, 0.48f);
			Fx.Animate(tk, 0, life, t =>
			{
				float s = t * life;
				float k = Ease5.Seg(s, drain, 0.1f) * (1f - Ease5.Seg(s, snap + 0.2f, 0.3f)) * 0.75f + (s >= snap ? 0.25f * Mathf.Exp(-(s - snap) * 3f) : 0f);
				float pulse = s >= drain && s < snap ? Mathf.Pow(0.5f + 0.5f * Mathf.Sin((s - drain) * 40f), 4f) * 0.35f : 0f;
				tint(Colors.White.Lerp(dark, Mathf.Min(1f, k)).Lerp(hurt, pulse));
			});
			tk.TreeExiting += () => tint(Colors.White);
		}
		// 主拍：一握——红光被抽回手里
		Fx.After(c.Front, drain, () =>
		{
			Glow.Glint(c.Front, hand, 0.75f);
			Glow.Flash(c.Front, tc, new Color(0.55f, 0.02f, 0.08f), h * 0.85f, 0.18f);
			Fx.Shake(1);
			Phantom.Shards(c.Front, tc, 10, 200f * u, 520f * u, (hand - tc).Normalized(), 60f, 0.3f, 0f, 14f * u);
			Essence(c.Front, Path, 12, 0f, 0.032f, 0.3f, 22f * u, k =>
			{
				if (k % 3 == 2) Glow.Flash(c.Front, hand, Phantom.Red, jh * 0.22f, 0.1f);
			});
		});
		// 第二拍：一扯——触手散掉，爪子滑进脚下一滩墨，墨珠溅起；他身上一闪红
		Fx.After(c.Front, snap, () =>
		{
			Glow.Flash(c.Front, hand, Phantom.Red, jh * 0.45f, 0.18f);
			Glow.Flash(c.Front, v.Feet + new Vector2(0f, -h * 0.04f), new Color(0.55f, 0.02f, 0.08f), wid * 1.1f, 0.22f);
			Fx.Shake(1);
			InkSplash(c.Front, v.Feet + new Vector2(0f, -h * 0.03f), 14, 300f * u, 750f * u, Vector2.Up, 70f, 2200f * u, 7f * u, 0.55f, 0.1f);
			ShaderMaterial pool = Pool(c.Back ?? c.Front, v.Feet + new Vector2(0f, h * 0.01f), wid * 0.8f, h * 0.17f, out ColorRect poolRect, 0.6);
			Fx.Animate(poolRect, 0, 0.5, t =>
			{
				float s = t * 0.5f;
				pool.SetShaderParameter("spread", Ease5.OutQuint(Ease5.Seg(s, 0.05f, 0.18f)) * (1f - Mathf.Pow(Ease5.Seg(s, 0.25f, 0.25f), 2f)));
				pool.SetShaderParameter("sheen", Mathf.Exp(-Mathf.Max(0f, s - 0.08f) * 6f));
			});
		});
		SkillRedo.Pulse(c, new Color(1.5f, 0.6f, 0.62f), snap, 0.3f);
	}
}
