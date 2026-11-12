using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 技能牌重做第四批（2026-11-12）：融入暗影、余像、模糊、夹击、萎靡。其中四张金色牌（融入暗影、余像、夹击、萎靡）按「每张一个和牌效最贴的
/// 主瞬间、做得更华丽、但不拖节奏」来做。积木：Vfx/Phantom.cs（预告信卡、拖尾、火花、纸屑）+ 这里的几个着色器（地上的墨池、往下淌的墨、
/// 局部的动态模糊）。音效按拍子配，每张两三拍、主拍最清楚。
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

	private static Shader? _drip;

	/// <summary>往下淌的墨（萎靡）：从上沿一道道往下流的墨痕，长短不一、头上鼓一个墨珠；墨里零星暗红的反光。</summary>
	private static Shader DripShader => _drip ??= new Shader { Code = @"
shader_type canvas_item;
uniform float grow = 0.0;     // 0 → 1：墨云聚起、墨痕往下挂
uniform float fade = 1.0;
uniform float seed = 0.0;
uniform float clock = 0.0;
float h1(float x) { return fract(sin(x * 91.17 + seed * 1.7) * 43758.5453); }
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031 + seed * 0.013); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; } return v; }
void fragment() {
	vec2 uv = UV;
	float aa = 0.012;
	// 头顶的墨云：噪声撕碎的软边一团，翻滚
	vec2 cp = (uv - vec2(0.5, 0.1)) / vec2(0.46, 0.11 + 0.05 * grow);
	float cn = fbm(uv * vec2(5.0, 9.0) + vec2(clock * 0.25, -clock * 0.15)) - 0.5;
	float cloud = smoothstep(1.0, 0.75, length(cp) + cn * 0.6) * smoothstep(0.0, 0.35, grow);
	// 墨痕：7 道，位置、粗细、长短都不一样，越往下越细、轻轻弯，头上一颗墨珠
	float drip = 0.0, bead = 0.0;
	for (int i = 0; i < 7; i++) {
		float fi = float(i);
		float x0 = 0.12 + 0.76 * (fi + 0.2 + 0.6 * h1(fi)) / 7.0;
		float len = 0.1 + (0.3 + 0.55 * h1(fi + 4.0)) * pow(clamp(grow * 1.15 - 0.12 * h1(fi + 9.0), 0.0, 1.0), 1.3);
		float y = uv.y - 0.12;
		float k = clamp(y / max(len, 1e-3), 0.0, 1.0);
		float wob = sin(y * 16.0 + fi * 2.1) * 0.006 + (noise(vec2(fi * 3.0, y * 9.0)) - 0.5) * 0.012;
		float w = (0.012 + 0.016 * h1(fi + 2.0)) * (1.0 - 0.6 * k);
		float dx = abs(uv.x - x0 - wob);
		drip = max(drip, smoothstep(w + aa, w - aa * 0.5, dx) * step(0.0, y) * step(y, len));
		float br = w * 1.9;
		bead = max(bead, smoothstep(br + aa, br - aa * 0.5, length(vec2(uv.x - x0 - wob, (y - len) * 0.8))) * step(0.02, len));
	}
	float m = max(cloud, max(drip, bead));
	if (m <= 0.002) discard;
	vec3 col = vec3(0.03, 0.008, 0.015);
	// 墨的反光：沿墨痕往下走的一点暗红高光，墨珠上一点亮
	float shine = smoothstep(0.65, 0.9, noise(vec2(uv.x * 30.0, uv.y * 10.0 - clock * 2.0)));
	col += vec3(0.6, 0.04, 0.09) * shine * (drip + bead) * 0.5;
	col += vec3(0.7, 0.08, 0.12) * bead * 0.25;
	col += vec3(0.35, 0.02, 0.05) * smoothstep(0.55, 0.85, fbm(uv * 12.0 + clock * 0.3)) * cloud * 0.5;
	COLOR = vec4(col, m * 0.92 * fade);
}" };

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

	/// <summary>
	/// 融入暗影（本回合获得的格挡翻倍）：脚下「哗」地铺开一池墨（边被噪声撕碎、零星透红光），六缕墨从池边盘着他往上升；人沉成一道暗影，
	/// 面具上一点红光——主拍：身后立起一道纯红的剪影（他的影子站了起来），胸口一圈红的冲击带；第二拍再一圈白的（翻倍），墨缕散开；最后墨池缩回脚下。约 1.2s。
	/// 拍子：墨铺开（风）→ 影子立起（噗，主拍）→ 翻倍（叮）。
	/// </summary>
	public static void Shadowmeld(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		const float rise = 0.42f, twice = 0.66f, life = 1.25f;
		Control back = c.Back ?? c.Front;
		S("gx_whoosh", 0.16f);
		S("gx_pop", 0.28f, rise);
		S("gx_ping", 0.15f, twice);
		// 墨池
		ShaderMaterial pool = Pool(back, c.Feet + new Vector2(0f, h * 0.01f), h * 0.85f, h * 0.2f, out ColorRect poolRect, life + 0.1);
		Fx.Animate(poolRect, 0, life, t =>
		{
			float s = t * life;
			float sp = Ease5.OutQuint(Mathf.Clamp(s / 0.3f, 0f, 1f)) * (1f - Mathf.Pow(Ease5.Seg(s, 0.9f, 0.35f), 2f));
			pool.SetShaderParameter("spread", sp);
			float sh = Mathf.Exp(-Mathf.Max(0f, s - rise) * 6f) * (s >= rise ? 1f : 0f) + 0.6f * Mathf.Exp(-Mathf.Max(0f, s - twice) * 7f) * (s >= twice ? 1f : 0f);
			pool.SetShaderParameter("sheen", sh);
		});
		// 墨缕：从池边盘着往上升（螺旋），升到头顶附近散掉
		for (int i = 0; i < 6; i++)
		{
			int k = i;
			float a0 = Mathf.Tau * k / 6f + Fx.Rand(-0.3f, 0.3f), turns = Fx.Rand(0.7f, 1.1f) * (k % 2 == 0 ? 1f : -1f);
			float top = h * Fx.Rand(0.75f, 1.05f), rad = h * Fx.Rand(0.26f, 0.36f), d = 0.04f + 0.03f * k;
			var rib = new Phantom.Ribbon(c.Front, h * Fx.Rand(0.08f, 0.11f), d + 0.75, 14, ink: true);
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
		// 人沉成暗影，面具一点红光
		if (c.SelfTint is { } tint)
		{
			Node2D tk = Fx.Root(c.Front, c.Feet, "P5MeldTint", life + 0.1);
			Fx.Animate(tk, 0, life, t =>
			{
				float s = t * life;
				float k = Ease5.Seg(s, 0.1f, 0.22f) * (1f - Ease5.Seg(s, 0.85f, 0.35f));
				tint(Colors.White.Lerp(new Color(0.16f, 0.1f, 0.16f), k * 0.9f));
			});
			tk.TreeExiting += () => tint(Colors.White);
		}
		// 主拍：影子立起来（纯红剪影，从脚下往上「长」出来、往身后错开一点），胸口一圈红
		Fx.After(c.Front, rise, () =>
		{
			Glow.Glint(c.Front, c.Mask + new Vector2(f * h * 0.03f, 0f), 0.9f);
			Wave(c.Front, c.Chest, Phantom.Red, h * 1.5f, 0.42f, 0.8f);
			Fx.Shake(1);
			if (Freeze(c, back, 0.8) is { } fr)
			{
				var (sp, m) = Echo(fr.Root, fr.Tex, new Color(0.9f, 0.04f, 0.1f));
				var (sp2, m2) = Echo(fr.Root, fr.Tex, new Color(0.35f, 0f, 0.05f));
				fr.Root.MoveChild(sp2, 0);
				Fx.Animate(sp, 0, 0.75, t =>
				{
					float s = t * 0.75f, e = Ease5.OutQuint(Mathf.Clamp(s / 0.16f, 0f, 1f));
					sp.Position = new Vector2(-f * h * 0.035f * e, h * 0.012f * e);
					sp2.Position = new Vector2(-f * h * 0.075f * e, h * 0.025f * e);
					float a = Mathf.Min(1f, s / 0.05f) * (1f - Ease5.Seg(s, 0.45f, 0.3f));
					m.SetShaderParameter("alpha", a);
					m2.SetShaderParameter("alpha", a * 0.85f);
				});
			}
			Phantom.Shards(c.Front, c.Feet + new Vector2(0f, -h * 0.05f), 12, 250f * u, 650f * u, Vector2.Up, 70f, 0.35f, 200f * u, 18f * u);
		});
		// 第二拍：翻倍——再一圈白的，墨缕散开
		Fx.After(c.Front, twice, () =>
		{
			Wave(c.Front, c.Chest, Phantom.Hot, h * 2.0f, 0.38f, 0.8f);
			Glow.Flash(c.Front, c.Chest, Phantom.Red, h * 0.6f, 0.12f);
			Phantom.Confetti(c.Front, c.Chest, 14, 300f * u, Vector2.Up, 160f, 0.8f, 6f * u);
		});
	}

	// ═══════════════════════════ 余像 ═══════════════════════════

	/// <summary>
	/// 余像（能力：之后每打出一张牌获得 1 点格挡）：定格他此刻的姿势——五层剪影一层接一层从他身上「唰」地往身后扇开（白、红、红、暗红、墨），
	/// 停一下、微微浮动；然后从最外一层开始一层层收回他身上，最后一层合上的瞬间胸口一闪、一圈红，碎光迸开（之后每张牌都有一层残影护着他）。约 1.1s。
	/// 拍子：扇开（风）→ 收拢（收牌声）→ 合上（叮，主拍）。
	/// </summary>
	public static void Afterimage(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		const float outT = 0.22f, back0 = 0.52f, merge = 0.86f, life = 1.0f;
		Control back = c.Back ?? c.Front;
		S("gx_whoosh", 0.2f);
		S("gx_fan_close", 0.16f, back0);
		S("gx_ping", 0.2f, merge);
		if (Freeze(c, back, life) is { } fr)
		{
			Color[] tints = { new(0.98f, 0.92f, 0.92f), Phantom.Red, new(0.8f, 0.03f, 0.09f), new(0.45f, 0.01f, 0.05f), new(0.06f, 0.02f, 0.03f) };
			for (int i = tints.Length - 1; i >= 0; i--)
			{
				int k = i;
				var (sp, m) = Echo(fr.Root, fr.Tex, tints[k]);
				float dx = -f * h * (0.13f + 0.12f * k), dy = Mathf.Sin(k * 1.7f) * h * 0.025f;
				float a0 = k == 0 ? 0.7f : 0.92f - 0.1f * k;
				float inAt = 0.03f * k, backAt = back0 + (tints.Length - 1 - k) * 0.065f, backDur = merge - backAt;
				Fx.Animate(sp, 0, life, t =>
				{
					float s = t * life;
					float eo = Ease5.OutQuint(Mathf.Clamp((s - inAt) / outT, 0f, 1f));
					float eb = Mathf.Clamp((s - backAt) / Mathf.Max(0.05f, backDur), 0f, 1f);
					eb = eb * eb * (3f - 2f * eb);
					float bob = Mathf.Sin(s * 7f + k) * h * 0.006f;
					sp.Position = new Vector2(dx * eo * (1f - eb), (dy + bob) * eo * (1f - eb));
					float a = a0 * Mathf.Min(1f, Mathf.Max(0f, s - inAt) / 0.05f) * (1f - eb * 0.85f);
					m.SetShaderParameter("alpha", s > merge + 0.02f ? 0f : a);
				});
			}
		}
		Fx.After(c.Front, merge, () =>
		{
			Glow.Flash(c.Front, c.Chest, Phantom.Hot, h * 0.8f, 0.14f);
			Wave(c.Front, c.Chest, Phantom.Red, h * 1.8f, 0.4f, 0.85f);
			Phantom.Shards(c.Front, c.Chest, 16, 350f * u, 900f * u, new Vector2(-f, -0.2f), 80f, 0.32f, 200f * u, 22f * u);
			Fx.Shake(1);
		});
		SkillRedo.Pulse(c, new Color(1.5f, 0.7f, 0.72f), merge, 0.35f);
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

	/// <summary>
	/// 萎靡（失去力量 + 虚弱）：目标头顶罩下一片墨，一道道往下淌（长短不一、头上鼓一颗墨珠、零星暗红反光）；主拍：墨淌满的一刻它一下发暗、往下一沉，
	/// 一串红光从它身上被抽出来、往 Joker 这边飘散（力量被抽走）；第二拍脚下一圈压扁的暗红冲击带（虚弱）。墨随后淡掉。约 1.1s。
	/// 拍子：墨罩下（风，很轻）→ 抽走（噗，主拍）→ 压下（叮）。
	/// </summary>
	public static void Malaise(SkillCtx c)
	{
		if (c.Target is not SkillFx.Victim v) return;
		float h = Mathf.Max(120f, (v.Feet - v.Top).Length()), u = c.Unit;
		Vector2 at = v.Feet.Lerp(v.Top, 0.5f);
		float wid = Mathf.Max(v.Width, h * 0.6f) * 1.15f;
		const float drain = 0.4f, press = 0.66f, life = 1.15f;
		S("gx_whoosh", 0.12f);
		S("gx_pop", 0.26f, drain);
		S("gx_ping", 0.12f, press);
		Node2D root = Fx.Root(c.Front, v.Top + new Vector2(-wid * 0.5f, -h * 0.12f), "P5Drip", life + 0.1);
		ShaderMaterial m = new() { Shader = DripShader };
		m.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		var rect = new ColorRect { Size = new Vector2(wid, h * 1.15f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(rect);
		Fx.Clock(rect);
		Fx.Animate(rect, 0, life, t =>
		{
			float s = t * life;
			m.SetShaderParameter("grow", Ease5.OutQuint(Mathf.Clamp(s / 0.45f, 0f, 1f)) * (1f + 0.15f * Ease5.Seg(s, 0.45f, 0.6f)));
			m.SetShaderParameter("fade", Mathf.Min(1f, s / 0.06f) * (1f - Ease5.Seg(s, 0.75f, 0.4f)));
		});
		Fx.After(c.Front, drain, () =>
		{
			if (v.Tint is { } tint)
			{
				Node2D tk = Fx.Root(c.Front, at, "P5MalaiseTint", 0.8);
				Fx.Animate(tk, 0, 0.7, t => tint(Colors.White.Lerp(new Color(0.42f, 0.34f, 0.44f), Mathf.Exp(-t * 3f))));
				tk.TreeExiting += () => tint(Colors.White);
			}
			Fx.Shake(1);
			Glow.Flash(c.Front, at, new Color(0.5f, 0.02f, 0.08f), h * 0.9f, 0.18f);
			Vector2 toJoker = (c.Chest - at).Normalized();
			for (int i = 0; i < 3; i++)
				Phantom.Shards(c.Front, at + new Vector2(Fx.Rand(-0.25f, 0.25f) * wid, Fx.Rand(-0.25f, 0.25f) * h), 6, 120f * u, 380f * u, toJoker + Vector2.Up * 0.6f, 40f, 0.7f, -260f * u, 14f * u, 0.06f * i);
		});
		Fx.After(c.Front, press, () =>
		{
			Wave(c.Front, v.Feet + new Vector2(0f, -h * 0.03f), new Color(0.6f, 0.03f, 0.09f), wid * 1.8f, 0.4f, 0.28f);
			Phantom.Shards(c.Front, v.Feet, 10, 200f * u, 500f * u, Vector2.Up, 75f, 0.3f, 1100f * u, 14f * u);
		});
	}
}
