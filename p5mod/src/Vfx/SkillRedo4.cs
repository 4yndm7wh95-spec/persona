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
	internal static (Node2D Root, Texture2D Tex)? Freeze(SkillCtx c, Control layer, double life)
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
uniform float red = 1.0;      // 红边 / 红脉络 / 红芯墨点有多红（0 = 纯黑烟）
uniform float dots_on = 1.0;  // 脚下红网点
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
	col += vec3(0.78, 0.04, 0.1) * rim * (0.5 + 0.9 * flare) * red;
	float vein = smoothstep(0.68, 0.9, fbm(vec2(x * 9.0 * aspect, y * 5.0 - clock * 1.8) + seed * 2.3));
	col += vec3(0.55, 0.02, 0.07) * vein * body * (0.35 + flare) * red;
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
	col = mix(col, mix(vec3(0.08, 0.02, 0.03), vec3(1.0, 0.22, 0.26), red), mote);
	a = max(a, max(mote, halo * 0.6));
	// 脚下的红网点
	vec2 hp = vec2(uv.x * aspect, uv.y) * 44.0;
	float hr = 0.4 * smoothstep(1.0, 0.25, length(vec2(x * 1.15, (y - 0.05) / 0.11)));
	float dots = smoothstep(hr, hr - 0.07, length(fract(hp) - 0.5)) * step(0.02, hr) * dots_on;
	col = mix(col, vec3(0.82, 0.04, 0.1), dots * (1.0 - body * 0.8));
	a = max(a, dots * 0.45 * smoothstep(0.0, 0.4, height));
	COLOR = vec4(col, a * amt);
}" };

	private static Shader? _cast;

	/// <summary>
	/// 投在地上的影子 / 立起来的影子（剪影贴图）：边缘按离脚的远近变软（贴脚的地方实、越远半影越宽、越淡），像真的影子；
	/// reach = 从脚往外「拉长」显出，erode = 被烟蚀掉（噪声撕开），redness = 立起来以后透出的暗红（只在烟的亮处、往上更红）。
	/// </summary>
	private static Shader CastShader => _cast ??= new Shader { Code = @"
shader_type canvas_item;
uniform float alpha = 1.0;
uniform float reach = 1.0;
uniform float erode = 0.0;
uniform float soft = 1.0;
uniform float redness = 0.0;
uniform float feet_v = 0.885;
uniform float top_v = 0.09;
uniform float clock = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; } return v; }
void fragment() {
	float hgt = clamp((feet_v - UV.y) / (feet_v - top_v), 0.0, 1.2);
	vec2 px = TEXTURE_PIXEL_SIZE * (1.5 + 7.0 * hgt * soft);
	float a = texture(TEXTURE, UV).a * 0.2;
	for (int i = 0; i < 8; i++) {
		float an = float(i) * 0.7854;
		a += texture(TEXTURE, UV + vec2(cos(an), sin(an)) * px).a * 0.1;
	}
	a = smoothstep(0.12, 0.8, a);
	float n = fbm(UV * vec2(9.0, 13.0) + vec2(0.0, -clock * 0.7));
	float m = a * smoothstep(0.0, 0.1, reach * 1.15 - hgt + (n - 0.5) * 0.18);
	m *= smoothstep(0.0, 0.22, n + 0.32 - erode * 1.3);
	float dens = mix(0.92, 0.55, clamp(hgt, 0.0, 1.0));
	float glow = redness * (0.35 + 0.65 * hgt) * smoothstep(0.42, 0.8, n + 0.15);
	vec3 col = mix(vec3(0.018, 0.005, 0.01), vec3(0.42, 0.015, 0.06), glow);
	COLOR = vec4(col, m * mix(dens, 0.95, redness * 0.7) * alpha);
}" };

	/// <summary>剪影贴图做成一个以脚为轴的影子精灵（back 层），返回精灵和材质。</summary>
	private static (Sprite2D Sp, ShaderMaterial M) CastSprite(SkillCtx c, (Node2D Root, Texture2D Tex) fr)
	{
		ShaderMaterial m = new() { Shader = CastShader };
		m.SetShaderParameter("feet_v", 1.15f / 1.3f);
		m.SetShaderParameter("top_v", 0.12f / 1.3f);
		Vector2 feetLocal = c.Feet - c.Region.Position;
		Sprite2D sp = new() { Texture = fr.Tex, Centered = false, Material = m, Offset = -feetLocal, Position = feetLocal };
		fr.Root.AddChild(sp);
		Fx.Clock(sp);
		return (sp, m);
	}

	/// <summary>
	/// 融入暗影（本回合获得的格挡翻倍）（用户 2026-11-12：上一版火舌、红剪影塑料感强——换思路，按我的审美来）：讲「他的影子」。
	/// ① 他脚下的影子顺着地面往身后「拉长」出去（真影子的样子：贴脚实、越远越软越淡），他身上暗下去；
	/// ② 影子一分为二，一道往身后、一道往身前转开（翻倍）——「嗒」；
	/// ③ 主拍：两道影子从地上立起来，站在他身前身后，烟一样的软边、里面透一点暗红，面具一点红光；
	/// ④ 两道影子收回他身上，被烟蚀散；他整个人沉成一道黑影、一闪暗红，再慢慢回来。脚下一层很低的黑烟。约 1.3s。
	/// 拍子：拉长（很轻的风）→ 分开（嗒）→ 立起（噗，主拍）→ 收拢（叮，很轻）。
	/// </summary>
	public static void Shadowmeld(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		const float split = 0.28f, rise = 0.52f, fold = 0.86f, life = 1.3f;
		Control back = c.Back ?? c.Front;
		S("gx_whoosh", 0.1f);
		S("gx_tock", 0.12f, split);
		S("gx_pop", 0.24f, rise);
		S("gx_ping", 0.1f, fold);
		// 脚下很低的黑烟
		float fw = h * 1.3f, fh = h * 0.7f;
		Node2D smokeRoot = Fx.Root(back, c.Feet + new Vector2(0f, h * 0.06f), "P5MeldSmoke", life + 0.1);
		ShaderMaterial sm = new() { Shader = FlameShader };
		sm.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		sm.SetShaderParameter("aspect", fw / fh);
		sm.SetShaderParameter("red", 0.35f);
		sm.SetShaderParameter("dots_on", 0f);
		var smoke = new ColorRect { Size = new Vector2(fw, fh), Position = new Vector2(-fw / 2f, -fh), Material = sm, MouseFilter = Control.MouseFilterEnum.Ignore };
		smokeRoot.AddChild(smoke);
		Fx.Clock(smoke);
		Fx.Animate(smoke, 0, life, t =>
		{
			float s = t * life;
			float up = Fx.OutCubic(Ease5.Seg(s, 0.1f, 0.5f)) * (1f - Ease5.Seg(s, 1.0f, 0.3f));
			float fl = s >= rise ? Mathf.Exp(-(s - rise) * 5f) : 0f;
			sm.SetShaderParameter("amt", up * 0.85f);
			sm.SetShaderParameter("height", 0.22f + 0.18f * fl);
			sm.SetShaderParameter("flare", fl * 0.6f);
		});
		// 他沉进影子里
		if (c.SelfTint is { } tint)
		{
			Node2D tk = Fx.Root(c.Front, c.Feet, "P5MeldTint", life + 0.1);
			Color dim = new(0.4f, 0.34f, 0.42f), black = new(0.1f, 0.06f, 0.09f), ember = new(0.9f, 0.18f, 0.22f);
			Fx.Animate(tk, 0, life, t =>
			{
				float s = t * life;
				float k = Ease5.Seg(s, 0.05f, 0.3f) * 0.7f;
				Color col = Colors.White.Lerp(dim, k);
				float sink = Ease5.Seg(s, fold - 0.04f, 0.08f) * (1f - Ease5.Seg(s, fold + 0.12f, 0.32f));
				col = col.Lerp(black, sink * 0.9f);
				float em = s >= fold ? Mathf.Exp(-(s - fold) * 7f) * 0.5f : 0f;
				col = col.Lerp(ember, em);
				col = col.Lerp(Colors.White, Ease5.Seg(s, 1.0f, 0.3f));
				tint(col);
			});
			tk.TreeExiting += () => tint(Colors.White);
		}
		if (Freeze(c, back, life) is not { } fr) return;
		// 两道影子：一开始叠在一起顺着地面往身后拉长，分开（一道往身后、一道往身前），立起来，收回身上
		for (int i = 0; i < 2; i++)
		{
			int k = i;
			var (sp, m) = CastSprite(c, fr);
			float flatLean = k == 0 ? -f * 1.3f : f * 1.22f;      // 分开后的倾斜（影子倒在地上的方向）
			float standDx = (k == 0 ? -f : f) * h * 0.09f;        // 立起来时站在他身后 / 身前
			Fx.Animate(sp, 0, life, t =>
			{
				float s = t * life;
				float reach = Fx.OutCubic(Ease5.Seg(s, 0f, 0.26f));
				float sp1 = Ease5.OutQuint(Ease5.Seg(s, split, 0.18f));
				float up = Fx.OutCubic(Ease5.Seg(s, rise, 0.2f));
				float home = Fx.InCubic(Ease5.Seg(s, fold - 0.1f, 0.12f));
				float lean = Mathf.Lerp(-f * 1.3f, flatLean, sp1);
				float flatY = 0.42f;
				float sy = Mathf.Lerp(flatY, 1.02f, up);
				float skew = Mathf.Lerp(lean, (k == 0 ? -f : f) * 0.1f, up) * (1f - home);
				float dx = standDx * up * (1f - home) + Mathf.Sin(s * 5f + k * 2f) * h * 0.004f * up;
				sp.Scale = new Vector2(1f + 0.04f * up * (1f - home), sy);
				sp.Skew = skew;
				sp.Position = c.Feet - c.Region.Position + new Vector2(dx, 0f);
				m.SetShaderParameter("reach", reach);
				m.SetShaderParameter("soft", Mathf.Lerp(1.0f, 0.35f, up));
				m.SetShaderParameter("redness", 0.65f * up * (1f - home * 0.5f));
				m.SetShaderParameter("erode", Ease5.Seg(s, fold - 0.02f, 0.3f));
				m.SetShaderParameter("alpha", (k == 1 ? Ease5.Seg(s, split, 0.06f) : 1f) * (1f - Ease5.Seg(s, fold + 0.1f, 0.3f)));
			});
		}
		// 主拍：立起来
		Fx.After(c.Front, rise + 0.08f, () =>
		{
			Glow.Glint(c.Front, c.Mask + new Vector2(f * h * 0.03f, 0f), 0.75f);
			Glow.Flash(c.Front, c.Chest, new Color(0.5f, 0.02f, 0.07f), h * 0.7f, 0.25f);
			Fx.Shake(1);
		});
		// 收拢
		Fx.After(c.Front, fold, () =>
		{
			Glow.Flash(c.Front, c.Chest, new Color(0.7f, 0.04f, 0.1f), h * 0.55f, 0.2f);
			Glow.Glint(c.Front, c.Mask + new Vector2(f * h * 0.03f, 0f), 0.55f);
		});
	}

	// ═══════════════════════════ 余像 ═══════════════════════════

	private static Shader? _dotEcho;

	/// <summary>
	/// 网点残影（余像）：Joker 剪影做成 P5 的红网点——实的时候是一片红，dissolve 往上走，网点从身后那一侧开始变小、散成一颗颗点；
	/// smear = 往身后拖的动态模糊（沿 x 采样）。back = 身后在贴图里的 x 方向（-1 左 / 1 右）。
	/// </summary>
	private static Shader DotEchoShader => _dotEcho ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec4 tint : source_color = vec4(0.93, 0.05, 0.12, 1.0);
uniform float alpha = 1.0;
uniform float dissolve = 0.0;
uniform float smear = 0.0;
uniform float back = -1.0;
void fragment() {
	float a = 0.0, ws = 0.0;
	for (int i = 0; i < 8; i++) {
		float fi = float(i) / 7.0, w = 1.0 - fi * 0.7;
		a += smoothstep(0.45, 0.6, texture(TEXTURE, UV - vec2(back * smear * fi, 0.0)).a) * w;
		ws += w;
	}
	a /= ws;
	float side = back < 0.0 ? 1.0 - UV.x : UV.x;
	float d = clamp(dissolve * 1.6 - 0.3 + (side - 0.5) * 1.2, 0.0, 1.0);
	vec2 q = UV / TEXTURE_PIXEL_SIZE;
	q = mat2(vec2(0.7071, 0.7071), vec2(-0.7071, 0.7071)) * q / 7.0;
	float dr = length(fract(q) - 0.5);
	float r = mix(0.75, 0.0, d);
	float fill = smoothstep(r + 0.04, r - 0.04, dr) * step(0.001, r);
	vec3 col = mix(tint.rgb, tint.rgb * vec3(1.15, 0.6, 0.6), d);
	COLOR = vec4(col, a * fill * alpha);
}" };

	private static (Sprite2D Sp, ShaderMaterial M) DotEcho(SkillCtx c, (Node2D Root, Texture2D Tex) fr)
	{
		ShaderMaterial m = new() { Shader = DotEchoShader };
		m.SetShaderParameter("back", -c.Face);
		Sprite2D sp = new() { Texture = fr.Tex, Centered = false, Material = m };
		fr.Root.AddChild(sp);
		return (sp, m);
	}

	/// <summary>动态模糊一下（读屏幕、在他身上往身后拖），模糊 / 余像用。</summary>
	private static void Smear(SkillCtx c, float amount, float life)
	{
		float h = c.Tall, f = c.Face;
		Node2D root = Fx.ScreenRoot(c.Front, "P5Smear");
		Fx.FreeAfter(root, life + 0.05);
		root.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		ShaderMaterial m = new() { Shader = SmearShader };
		var rect = new ColorRect { Size = Fx.Screen(c.Front), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(rect);
		Vector2 scr = Fx.Screen(c.Front);
		Vector2 cs = Fx.ScreenPoint(c.Front, c.Feet + new Vector2(0f, -h * 0.5f));
		float tallS = h * c.Front.GetCanvasTransform().Scale.Y;
		m.SetShaderParameter("center", new Vector2(cs.X / scr.X, cs.Y / scr.Y));
		m.SetShaderParameter("radius", new Vector2(tallS * 0.5f / scr.X, tallS * 0.66f / scr.Y));
		m.SetShaderParameter("dir", new Vector2(-f * tallS * amount / scr.X, 0f));
		Fx.Animate(rect, 0, life, t =>
		{
			float s = t * life;
			m.SetShaderParameter("strength", Mathf.Min(1f, s / 0.04f) * Mathf.Exp(-Mathf.Max(0f, s - 0.05f) * 9f));
		});
	}

	/// <summary>
	/// 余像（能力：之后每打出一张牌获得 1 点格挡）（用户 2026-11-12：残影太大、打出太拉——只要贴身的一层，做得精致）：
	/// 他身上一下动态模糊，一层红网点残影从身上「撕」下来，贴着身后错开一点、拖着模糊；停一下，身后那一侧化成一颗颗网点往后散；
	/// 主拍：剩下的残影「啪」地弹回他身上——胸口一点白红的闪、身上一闪红，几颗网点碎光迸开。约 0.8s。
	/// 拍子：撕下（风）→ 弹回（叮，主拍）。
	/// </summary>
	public static void Afterimage(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		const float snap = 0.56f, life = 0.8f;
		S("gx_whoosh", 0.15f);
		S("gx_ping", 0.2f, snap);
		Smear(c, 0.12f, 0.35f);
		if (Freeze(c, c.Back ?? c.Front, life) is { } fr)
		{
			var (sp, m) = DotEcho(c, fr);
			float dx = -f * h * 0.028f, dy = h * 0.006f;
			Fx.Animate(sp, 0, life, t =>
			{
				float s = t * life;
				float eo = Ease5.OutQuint(Ease5.Seg(s, 0f, 0.12f));
				float drift = Ease5.Seg(s, 0.12f, snap - 0.12f);
				float eb = Fx.InCubic(Ease5.Seg(s, snap - 0.09f, 0.09f));
				sp.Position = new Vector2(dx * (eo + 0.5f * drift) * (1f - eb), dy * eo * (1f - eb));
				m.SetShaderParameter("smear", 0.035f * (1f - eo * 0.7f) * (1f - eb));
				m.SetShaderParameter("dissolve", 0.55f * Ease5.Seg(s, 0.18f, snap - 0.2f) + (s > snap ? Ease5.Seg(s, snap, 0.2f) : 0f));
				m.SetShaderParameter("alpha", Mathf.Min(1f, s / 0.03f) * (s > snap ? 1f - Ease5.Seg(s, snap, 0.2f) : 1f));
			});
		}
		Fx.After(c.Front, snap, () =>
		{
			Glow.Glint(c.Front, c.Chest + new Vector2(f * h * 0.04f, -h * 0.05f), 0.7f);
			Glow.Flash(c.Front, c.Chest, new Color(0.85f, 0.08f, 0.14f), h * 0.45f, 0.14f);
			Phantom.Shards(c.Front, c.Chest, 7, 250f * u, 600f * u, new Vector2(-f, -0.2f), 60f, 0.25f, 150f * u, 10f * u);
		});
		SkillRedo.Pulse(c, new Color(1.4f, 0.72f, 0.74f), snap, 0.28f);
	}

	/// <summary>
	/// 余像生效（之后每打出一张牌、获得那 1 点格挡的一刻）：身上剥下一层很淡的红网点残影，贴着往身后滑开一点，化成网点散掉。0.38s，很轻（出牌很频繁）。
	/// </summary>
	public static void AfterimageTick(SkillCtx c)
	{
		float h = c.Tall, f = c.Face;
		const float life = 0.38f;
		S("gx_tick", 0.05f);
		if (Freeze(c, c.Back ?? c.Front, life) is { } fr)
		{
			var (sp, m) = DotEcho(c, fr);
			Fx.Animate(sp, 0, life, t =>
			{
				float e = Ease5.OutQuint(t);
				sp.Position = new Vector2(-f * h * 0.035f * e, h * 0.005f * e);
				m.SetShaderParameter("smear", 0.02f * (1f - e));
				m.SetShaderParameter("dissolve", 0.15f + 0.85f * t);
				m.SetShaderParameter("alpha", 0.7f * Mathf.Min(1f, t * life / 0.03f) * (1f - Ease5.Seg(t, 0.7f, 0.3f)));
			});
		}
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

	private static Shader? _streak, _sag;

	/// <summary>
	/// 贴着地面爬过去的影子（萎靡）：一条软边的烟影带（沿长度 x、横向 y），宽窄被噪声揉开、往前流；head / tail = 沿长度显出的范围；
	/// flow = 一道道暗红的光沿着它往回（x→0）流。aspect = 长 / 宽。
	/// </summary>
	private static Shader StreakShader => _streak ??= new Shader { Code = @"
shader_type canvas_item;
uniform float head = 0.0;
uniform float tail = 0.0;
uniform float flow = 0.0;
uniform float amt = 1.0;
uniform float aspect = 8.0;
uniform float seed = 0.0;
uniform float clock = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; } return v; }
void fragment() {
	float x = UV.x, y = (UV.y - 0.5) * 2.0;
	float n = fbm(vec2(x * aspect * 0.5 - clock * 0.7, y * 1.3) + seed);
	float n2 = fbm(vec2(x * aspect * 1.6 - clock * 1.5, y * 2.6) + seed * 1.3);
	float w = 0.3 + 0.75 * n;
	float m = smoothstep(w, w * 0.12, abs(y) + (n2 - 0.5) * 0.85);
	m *= smoothstep(tail, tail + 0.08, x) * (1.0 - smoothstep(head - 0.1, head, x));
	m *= smoothstep(0.0, 0.05, x) * smoothstep(1.0, 0.95, x);
	float core = smoothstep(0.7, 0.0, abs(y));
	vec3 col = vec3(0.02, 0.005, 0.012) + vec3(0.18, 0.008, 0.03) * core * smoothstep(0.55, 0.85, n2);
	float pl = pow(0.5 + 0.5 * sin((x * aspect * 0.3 + clock * 2.6) * 6.2832), 10.0) * flow;
	col += vec3(0.9, 0.07, 0.13) * pl * core * smoothstep(0.35, 0.75, n2 + 0.1);
	COLOR = vec4(col, m * 0.88 * amt);
}" };

	/// <summary>
	/// 往下塌（萎靡的主拍，读屏幕）：box（屏幕 uv：左上、右下）里的画面被一列列往下拽（下半更多、每列长短不一，像融化往下淌），
	/// 同时压暗、褪一点色。amount = 最多往下拽多少（屏幕 uv）。
	/// </summary>
	private static Shader SagShader => _sag ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec4 box = vec4(0.4, 0.3, 0.6, 0.8);
uniform float amount = 0.0;
uniform float dark = 0.0;
uniform float clock = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 c = (box.xy + box.zw) * 0.5, hs = (box.zw - box.xy) * 0.5;
	vec2 d = (uv - c) / hs;
	float mask = smoothstep(1.0, 0.55, length(d * vec2(1.0, 0.9)));
	if (mask <= 0.001) discard;
	float cn = noise(vec2(uv.x / hs.x * 9.0, 0.5)) * 0.65 + noise(vec2(uv.x / hs.x * 3.0, clock * 0.4)) * 0.35;
	float down = smoothstep(-1.0, 0.5, d.y);
	float off = amount * mask * (0.25 + 0.75 * cn * cn) * (0.25 + 0.75 * down);
	vec3 s = texture(screen_tex, uv - vec2(0.0, off)).rgb;
	float g = dot(s, vec3(0.3, 0.59, 0.11));
	s = mix(s, vec3(g) * vec3(0.85, 0.78, 0.92), dark * 0.25 * mask * mask);
	COLOR = vec4(s, 1.0);
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
	/// 萎靡（失去力量 + 虚弱）（用户 2026-11-12：还是不好看、塑料感强——换思路）：他的影子去够它的影子。
	/// ① 他手一沉，脚下的影子贴着地面「爬」过去，一路爬到目标脚下，在它脚下化开一滩，一层黑烟从它脚边缠上来；
	/// ② 主拍：它整个画面被往下一拽——一列列往下塌（像融化）、压暗褪色；红光从它身上沉进影子里，顺着地上那条影子流回他脚下、升进他身体（力量被抽走）；
	/// ③ 第二拍（虚弱）：黑烟往下一压，它又塌一下，然后慢慢回弹；影子退回他脚下。约 1.3s。
	/// 拍子：爬过去（很轻的风）→ 塌（噗，主拍）→ 压（嗒）。
	/// </summary>
	public static void Malaise(SkillCtx c)
	{
		if (c.Target is not SkillFx.Victim v) return;
		float h = Mathf.Max(120f, (v.Feet - v.Top).Length()), u = c.Unit, jh = c.Tall;
		Vector2 tc = v.Feet.Lerp(v.Top, 0.5f);
		float wid = Mathf.Max(v.Width, h * 0.6f);
		const float crawl = 0.3f, sag = 0.42f, press = 0.78f, life = 1.3f;
		Control back = c.Back ?? c.Front;
		S("gx_whoosh", 0.1f);
		S("gx_pop", 0.26f, sag);
		S("gx_tock", 0.12f, press);
		// 地上的影子：从他脚下爬到目标脚下
		Vector2 from = c.Feet + new Vector2(0f, jh * 0.01f), to = v.Feet + new Vector2(0f, h * 0.01f);
		float len = from.DistanceTo(to) + wid * 0.3f, thick = Mathf.Max(jh, h) * 0.3f;
		Node2D sRoot = Fx.Root(back, from, "P5ShadowCrawl", life + 0.1);
		sRoot.Rotation = (to - from).Angle();
		ShaderMaterial st = new() { Shader = StreakShader };
		st.SetShaderParameter("aspect", len / thick);
		st.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		var streak = new ColorRect { Size = new Vector2(len, thick), Position = new Vector2(-wid * 0.05f, -thick / 2f), Material = st, MouseFilter = Control.MouseFilterEnum.Ignore };
		sRoot.AddChild(streak);
		Fx.Clock(streak);
		Fx.Animate(streak, 0, life, t =>
		{
			float s = t * life;
			st.SetShaderParameter("head", Fx.OutCubic(Ease5.Seg(s, 0.02f, crawl)) * 1.1f);
			st.SetShaderParameter("tail", Fx.InCubic(Ease5.Seg(s, press + 0.05f, 0.4f)) * 1.1f);
			st.SetShaderParameter("flow", Ease5.Seg(s, sag, 0.08f) * (1f - Ease5.Seg(s, press + 0.1f, 0.2f)));
			st.SetShaderParameter("amt", Mathf.Min(1f, s / 0.06f));
		});
		// 目标脚下化开一滩影子 + 缠上来的黑烟
		ShaderMaterial pool = Pool(back, to, wid * 0.75f, h * 0.15f, out ColorRect poolRect, life + 0.1);
		poolRect.Modulate = new Color(1f, 1f, 1f, 0.85f);
		float fw = wid * 1.35f, fh = h * 1.1f;
		Node2D smokeRoot = Fx.Root(c.Front, to + new Vector2(0f, h * 0.05f), "P5MalaiseSmoke", life + 0.1);
		ShaderMaterial sm = new() { Shader = FlameShader };
		sm.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		sm.SetShaderParameter("aspect", fw / fh);
		sm.SetShaderParameter("red", 0.2f);
		sm.SetShaderParameter("dots_on", 0f);
		var smoke = new ColorRect { Size = new Vector2(fw, fh), Position = new Vector2(-fw / 2f, -fh), Material = sm, MouseFilter = Control.MouseFilterEnum.Ignore };
		smokeRoot.AddChild(smoke);
		Fx.Clock(smoke);
		Fx.Animate(smoke, 0, life, t =>
		{
			float s = t * life;
			float arrive = Fx.OutCubic(Ease5.Seg(s, crawl - 0.06f, 0.25f));
			float pr = s >= press ? Mathf.Exp(-(s - press) * 6f) : 0f;
			float end = Ease5.Seg(s, press + 0.15f, 0.35f);
			pool.SetShaderParameter("spread", arrive * (1f - end * end));
			pool.SetShaderParameter("sheen", s >= sag ? 0.6f * Mathf.Exp(-(s - sag) * 4f) : 0f);
			sm.SetShaderParameter("amt", arrive * (1f - end) * 0.7f);
			sm.SetShaderParameter("height", (0.32f + 0.12f * (s >= sag ? Mathf.Exp(-(s - sag) * 5f) : 0f)) * (1f - 0.55f * pr) * (1f - 0.6f * end));
			sm.SetShaderParameter("flare", s >= sag ? Mathf.Exp(-(s - sag) * 5f) * 0.5f : 0f);
		});
		// 主拍：往下塌（读屏幕）
		Fx.After(c.Front, sag, () =>
		{
			Node2D root = Fx.ScreenRoot(c.Front, "P5Sag");
			Fx.FreeAfter(root, life - sag + 0.05);
			root.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
			ShaderMaterial m = new() { Shader = SagShader };
			var rect = new ColorRect { Size = Fx.Screen(c.Front), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(rect);
			Fx.Clock(rect);
			Vector2 scr = Fx.Screen(c.Front);
			Vector2 a = Fx.ScreenPoint(c.Front, v.Top + new Vector2(-wid * 0.62f, -h * 0.15f)), b = Fx.ScreenPoint(c.Front, v.Feet + new Vector2(wid * 0.62f, h * 0.08f));
			m.SetShaderParameter("box", new Vector4(a.X / scr.X, a.Y / scr.Y, b.X / scr.X, b.Y / scr.Y));
			float drop = (b.Y - a.Y) / scr.Y * 0.17f;
			float dur = life - sag;
			Fx.Animate(rect, 0, dur, t =>
			{
				float s = t * dur, s2 = s - (press - sag);
				float k = Fx.OutCubic(Mathf.Clamp(s / 0.1f, 0f, 1f)) * Mathf.Exp(-Mathf.Max(0f, s - 0.12f) * 2.2f);
				float k2 = s2 > 0f ? Fx.OutCubic(Mathf.Clamp(s2 / 0.06f, 0f, 1f)) * Mathf.Exp(-Mathf.Max(0f, s2 - 0.06f) * 3.5f) * 0.6f : 0f;
				float sagK = Mathf.Max(k, k2 + k * 0.3f);
				m.SetShaderParameter("amount", drop * sagK);
				m.SetShaderParameter("dark", Mathf.Min(1f, 0.9f * sagK + 0.3f * Ease5.Seg(s, 0f, 0.1f) * (1f - Ease5.Seg(s, 0.5f, 0.35f))));
			});
			Fx.Shake(1);
			// 红光：从它身上沉进影子 → 顺着地面流回他脚下 → 升进他胸口
			Vector2 chest = c.Chest;
			Vector2 Path(float t)
			{
				if (t > 0.78f) return tc.Lerp(to, (1f - t) / 0.22f);
				if (t > 0.18f) return to.Lerp(from, (0.78f - t) / 0.6f);
				return from.Lerp(chest, (0.18f - t) / 0.18f);
			}
			Essence(c.Front, Path, 9, 0.02f, 0.035f, 0.42f, 18f * u, k =>
			{
				if (k % 3 == 2) Glow.Flash(c.Front, chest, new Color(0.8f, 0.06f, 0.12f), jh * 0.25f, 0.12f);
			});
		});
		// 目标发暗（跟着塌的节奏）
		if (v.Tint is { } tint)
		{
			Node2D tk = Fx.Root(c.Front, tc, "P5MalaiseTint", life + 0.1);
			Color dark = new(0.32f, 0.26f, 0.36f);
			Fx.Animate(tk, 0, life, t =>
			{
				float s = t * life;
				float k = Ease5.Seg(s, sag, 0.08f) * (1f - Ease5.Seg(s, press + 0.15f, 0.35f) * 0.85f);
				tint(Colors.White.Lerp(dark, k));
			});
			tk.TreeExiting += () => tint(Colors.White);
		}
		// 第二拍：黑烟往下一压
		Fx.After(c.Front, press, () =>
		{
			Glow.Flash(c.Front, to + new Vector2(0f, -h * 0.05f), new Color(0.45f, 0.02f, 0.07f), wid * 0.9f, 0.2f);
		});
		SkillRedo.Pulse(c, new Color(1.35f, 0.7f, 0.72f), sag + 0.5f, 0.3f);
	}
}
