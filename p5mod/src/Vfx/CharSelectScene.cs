using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 选人界面（静默猎手的 char_select_bg_silent 场景，设计见 docs/p5/CHARSELECT_PLAN.md）。构图照结城理 mod 的选人界面：
/// 立绘是贴着右边、占满屏幕高度的大特写，左边一道斜切口把它收进一块面板里（切口上压一组 P5 的黑 / 红 / 白斜条 + 滚动小字），
/// 面板是暗红黑底，上面压着红色锯齿爆炸形（用户 2026-11-17：锯齿球要有层次、黑底加粗）；左边是干净的场景：深红夜空（只有很淡的云）、压低的东京天际线黑剪影（窗灯明灭）、
/// 面板旁边楼顶水塔上坐着摩尔加纳；左上角是 TAKE YOUR HEART 剪贴字标题（P5 菜单的标题位置）；偶尔一张预告信飘过。
/// 立绘范围按用户 2026-11-17 的图 2：画布 x≈896 落在屏幕右边、画布 y≈25 落在屏幕顶边（模板匹配出来的），缩放 0.92。
/// 露出来的那只眼睛泛金光（虹膜染金 + 亮芯 + 柔光 + 横向光条 + 偶尔一闪的四角星，跟着呼吸起伏、眨眼时熄灭；
/// 用户 2026-11-17：不要蓝焰面具、蓝色不搭 → 金色：P5 觉醒时的眼睛，暖色和红黑白协调，也不会被旁边的红手套吞掉）。
/// 选中：立绘滑入、斜条切入 → 原版眼神特写 → 碎开那一下眼睛的金光猛地一亮、光条拉长 → 常驻。
/// 只用 Godot 类型（预览工具 tools/p5/fxpreview 直接编译这个文件）；挂到游戏里的补丁在 CharSelectPatches.cs。
/// 坐标：下面的布局全是 1080p 屏幕坐标，挂在场景根里一个下移 ScreenTop 的容器上（tscn offset -960,-521：屏幕中心 = 本地 (960,521)）。
/// </summary>
public static class CharSelectScene
{
	public const float W = 2560f, H = 1200f;
	/// <summary>场景根本地坐标里 1080p 屏幕顶边的位置。</summary>
	public const float ScreenTop = -19f;
	/// <summary>设置里关掉"选人动画"时为 false：不播切入，直接常驻。</summary>
	public static Func<bool> IntroEnabled = () => true;

	private const float SW = 1920f, SH = 1080f;
	// 立绘：图 2 的范围（画布 x -6..896、y 25..1174），右边贴屏幕右边、顶边贴屏幕顶边，往下比图 2 多露一点
	private const float PScale = 0.92f;
	private static readonly Vector2 POrigin = new(SW - 896f * PScale, -25f * PScale);
	private static readonly Vector2 Pivot = new(512f, 1536f);                 // 呼吸 / 微摆的支点：画布底边中点
	private static readonly Vector2 ArmPivot = new(240f, 900f);               // 手（连前臂袖子）忽远忽近的缩放支点：手掌下方（袖口底端几乎不动、指尖动得最多）
	private static readonly Vector2 BlinkAt = new(330f, 320f);                // tools/p5/charselect_assets.py BLINK_BOX 左上角
	private static readonly Vector2 Eye = new(608f, 421f);                    // 露出来的那只眼睛的虹膜中心（画面右边那只；另一只被刘海和阴影盖住了）
	private const float EyeRoll = -0.1f;                                      // 眼睛的倾斜（光条跟着眼线走）
	private static readonly Vector2 NeckPivot = new(540f, 640f);              // 头部轻摆的支点（脖子）
	private const float LidTop = 372f, LidBottom = 470f;                     // 眨眼：眼皮从这条线合到那条线（画布 y，以近处那只眼为准），闭眼贴片只画在眼皮上面
	private const float LidTilt = 0.111f;                                     // 眼皮线顺着两眼连线倾斜（远处那只眼高 19px）：两只眼同一刻合上、睁开
	// 眼神特写往左挪多少（1080p 屏幕像素，按横幅缩放换算）：主眼从 x≈1124（压在斜条下）挪到 x≈760
	private const float CutInShift = 364f;
	/// <summary>原版左侧信息面板等眼神特写碎开之后再淡入（同 P3 mod 的介绍板），秒。</summary>
	public const float InfoPanelDelay = 0.6f;
	private static readonly Color EyeTint = new(1f, 0.7f, 0.18f), EyeHot = new(1f, 0.95f, 0.78f);   // 金色光 / 白热的芯
	// 斜切口（屏幕坐标，上端 → 下端）：切口左边的立绘不画，斜条压在切口上
	private static readonly Vector2 CutA = new(1150f, 0f), CutB = new(1330f, 1080f);
	// 锯齿球（charselect_assets.py：贴图尖角外半径 400 → 画布 700）：中心在画布 (520,460)
	private static readonly Vector2 BurstAt = new(520f, 460f);
	private const float BurstK = 700f / 400f;
	private static readonly Color PanelInk = new("22040a");                   // 立绘面板底色：比斜板的纯黑暖一点，两者分得开
	// 天际线顶边（屏幕 y）、远景再高一点
	private const float Horizon = 700f;
	// TAKE YOUR HEART 拆块（charselect_assets.py banner_parts 的输出）：在整条里的左上角
	private static readonly Vector2[] BannerParts = { new(0, 0), new(228, 8), new(492, 8), new(958, 0), new(1164, 16), new(1276, 14) };
	private static readonly Color Crimson = new("de101e"), Ink = new("0a0608");

	private sealed class Rig
	{
		public Control Root = null!, Screen = null!, Back = null!, Front = null!;
		public Node2D Group = null!, Chest = null!, Burst = null!, Header = null!;
		public ColorRect Panel = null!;
		public Node2D[] Arms = Array.Empty<Node2D>();
		public Sprite2D Blink = null!, EyeHalo = null!, EyeIris = null!, EyeCore = null!, EyeStreak = null!;
		public ShaderMaterial Warp = null!, BlinkMat = null!;
		public float Open = 1f;                                                // 眼睛睁开的程度（眨眼时 1 → 0 → 1）
		public Node2D Eye = null!;
		public float Flare;                                                    // 选中那一下的眼睛闪光 0..1
		public List<Sprite2D> Letters = new();
		public List<ShaderMaterial> Bands = new();
		public Node2D SlabText = null!;
		public ShaderMaterial Clip = null!;
		public double T;
	}

	/// <summary>搭好整个场景。bg = 原版场景根；全部搭完才藏原版（出错时拆掉自己的，原版保持原样）。</summary>
	public static void Build(Control bg, bool intro)
	{
		Control root = new() { Name = "P5Select", MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(W, H) };
		bg.AddChild(root);
		bg.MoveChild(root, 0);
		try
		{
			BuildInto(root, intro && IntroEnabled());
		}
		catch (Exception)
		{
			if (GodotObject.IsInstanceValid(root))
			{
				root.QueueFree();
			}
			throw;
		}
		foreach (Node c in bg.GetChildren())
		{
			if (c != root && c is CanvasItem ci)
			{
				ci.Visible = false;                   // 原版背景图、臭气线、静默猎手骨骼
			}
		}
	}

	private static Texture2D Need(string name) => Fx.Tex(name) ?? throw new InvalidOperationException($"missing texture {name}");

	private static Control Layer(Control parent, string name)
	{
		Control c = new() { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(W, H) };
		parent.AddChild(c);
		return c;
	}

	private static void BuildInto(Control root, bool intro)
	{
		Rig r = new() { Root = root };
		r.Screen = Layer(root, "Screen");
		r.Screen.Position = new Vector2(0f, ScreenTop);
		Sky(r.Screen);
		City(r.Screen);
		Cards(r.Screen);
		Rooftop(r.Screen);
		Shade(r.Screen);
		Header(r);
		r.Back = Layer(r.Screen, "Back");
		Portrait(r);
		Stripes(r);
		r.Front = Layer(r.Screen, "Front");
		Animate(r);
		Entrance(r, intro);
	}

	private static Vector2 G(Rig r, Vector2 screen) => r.Screen.GetGlobalTransform() * screen;

	// ───────────────────────── 背景 ─────────────────────────

	private static Shader? _sky;

	/// <summary>上暗下亮的深红夜空（城市那条地平线后面最亮，像城市灯光照上来）+ 很淡、很慢的云。没有放射线 / 光束（用户 2026-11-17：太乱）。</summary>
	private static Shader SkyShader => _sky ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 res = vec2(2520.0, 1680.0);
uniform vec2 pad = vec2(300.0, 300.0);
uniform float horizon = 700.0;
uniform vec3 c_top : source_color = vec3(0.275, 0.016, 0.047);
uniform vec3 c_mid : source_color = vec3(0.53, 0.031, 0.078);
uniform vec3 c_glow : source_color = vec3(0.77, 0.063, 0.11);
uniform float clock = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * noise(p); p = p * 2.03 + 5.3; a *= 0.5; } return v; }
void fragment() {
	vec2 px = UV * res - pad;                       // 屏幕坐标
	float t = clamp(px.y / horizon, 0.0, 1.0);
	vec3 col = mix(c_top, c_mid, clamp(t / 0.75, 0.0, 1.0));
	col = mix(col, c_glow, pow(clamp((t - 0.6) / 0.4, 0.0, 1.0), 1.5));
	// 很淡的云：大块、横向慢漂，只在上半部
	vec2 q = px / vec2(760.0, 300.0) + vec2(clock * 0.006, 0.0);
	float cloud = smoothstep(0.45, 0.85, fbm(q + vec2(0.0, fbm(q * 1.3) * 0.7)));
	col *= 1.0 - 0.28 * cloud * pow(1.0 - clamp((px.y - 100.0) / (horizon - 100.0), 0.0, 1.0), 0.7);
	COLOR = vec4(col, 1.0);
}" };

	private static void Sky(Control screen)
	{
		Vector2 pad = new(300f, 300f), size = new(SW + 600f, SH + 600f);
		ColorRect sky = new() { Name = "Sky", Position = -pad, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = SkyShader } };
		ShaderMaterial m = (ShaderMaterial)sky.Material;
		m.SetShaderParameter("res", size);
		m.SetShaderParameter("pad", pad);
		m.SetShaderParameter("horizon", Horizon);
		screen.AddChild(sky);
		Fx.Clock(sky);
	}

	private static Shader? _city;

	/// <summary>
	/// 天际线（charselect_assets.py 已压成黑剪影 + 小窗灯）：亮的像素（窗灯）按小格子各自明灭；
	/// haze_k > 0 时剪影换成 haze 色、窗灯变淡（远景那层，用同一张图翻过来）。
	/// </summary>
	private static Shader CityShader => _city ??= new Shader { Code = @"
shader_type canvas_item;
uniform float clock = 0.0;
uniform vec3 haze : source_color = vec3(0.38, 0.03, 0.07);
uniform float haze_k = 0.0;
uniform float win_k = 1.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	float lum = max(c.r, max(c.g, c.b));
	float win = smoothstep(0.16, 0.32, lum);
	vec2 cell = floor(UV * vec2(180.0, 80.0));
	float h = hash(cell);
	float t = clock * (0.04 + 0.1 * h) + h * 17.0;
	float on = step(0.14, fract(t));
	float k = mix(0.2, 1.1, on) * (0.85 + 0.15 * sin(clock * (0.5 + h) + h * 40.0));
	vec3 sil = mix(c.rgb, haze, haze_k);
	vec3 lit = mix(sil, c.rgb * k, win_k);
	COLOR = vec4(mix(sil, lit, win), c.a * COLOR.a);
}" };

	private static void City(Control screen)
	{
		Texture2D tex = Need("cs_skyline");
		// 远景：同一张图翻过来、小一点、高一点，剪影染成接近天色的暗红
		Sprite2D far = new() { Name = "CityFar", Texture = tex, Centered = false, FlipH = true, Scale = Vector2.One * 0.64f,
			Position = new Vector2(-200f, Horizon - 60f), Material = new ShaderMaterial { Shader = CityShader } };
		((ShaderMaterial)far.Material).SetShaderParameter("haze_k", 1f);
		((ShaderMaterial)far.Material).SetShaderParameter("win_k", 0.3f);
		screen.AddChild(far);
		Fx.Clock(far);
		Sprite2D near = new() { Name = "City", Texture = tex, Centered = false, Scale = Vector2.One * 0.8f, Position = new Vector2(-40f, Horizon),
			Material = new ShaderMaterial { Shader = CityShader } };
		screen.AddChild(near);
		Fx.Clock(near);
	}

	/// <summary>
	/// 楼顶前景（左右翻过来：水塔在最右、挨着立绘面板，栏杆 / 空调 / 广告架往左铺到底）+ 水塔顶上的摩尔加纳（面朝 Joker）：
	/// 尾巴慢摆，隔一阵整只轻轻一抖。
	/// </summary>
	private static void Rooftop(Control screen)
	{
		Texture2D tex = Need("cs_rooftop");
		const float k = 0.72f, tankRight = 1010f;
		Vector2 at = new(tankRight - 1506f * k, SH + 40f - tex.GetSize().Y * k);
		screen.AddChild(new Sprite2D { Name = "Rooftop", Texture = tex, Centered = false, FlipH = true, Position = at, Scale = Vector2.One * k });
		if (Fx.Tex("cs_morgana") is Texture2D mt && Fx.Tex("cs_morgana_tail") is Texture2D tt)
		{
			const float mk = 0.42f;
			// 水塔顶（翻过来之后在贴图 x≈1351, y≈45），摩尔加纳贴图 314×314：屁股在 (176, 300)
			Node2D cat = new() { Name = "Morgana", Position = at + new Vector2(1536f - 185f, 75f) * k, Scale = new Vector2(-mk, mk) };
			screen.AddChild(cat);
			Vector2 tailRoot = new(116f, 265f);                                // 贴图里尾巴根（charselect_assets 量的）
			Node2D tail = new() { Position = tailRoot - new Vector2(176f, 300f) };
			cat.AddChild(tail);
			tail.AddChild(new Sprite2D { Texture = tt, Centered = false, Position = -tailRoot });
			cat.AddChild(new Sprite2D { Texture = mt, Centered = false, Position = new Vector2(-176f, -300f) });
			Tween sway = tail.CreateTween().SetLoops();
			sway.TweenProperty(tail, "rotation", 0.16f, 1.6).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			sway.TweenProperty(tail, "rotation", -0.1f, 1.9).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			Tween twitch = cat.CreateTween().SetLoops();
			twitch.TweenInterval(9.0);
			twitch.TweenProperty(cat, "scale", new Vector2(-mk * 1.03f, mk * 0.97f), 0.08);
			twitch.TweenProperty(cat, "scale", new Vector2(-mk, mk), 0.22).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}
	}

	/// <summary>预告信偶尔一张：从左上被风吹着往右下飘，边飘边翻（横向缩放模拟翻面）；飘到面板那里被立绘 / 斜条挡住。</summary>
	private static void Cards(Control screen)
	{
		if (Fx.Tex("cs_card") is not Texture2D tex) return;
		Node2D layer = new() { Name = "Cards" };
		screen.AddChild(layer);
		void Spawn(float start)
		{
			if (!GodotObject.IsInstanceValid(layer)) return;
			float size = Fx.Rand(0.17f, 0.24f);
			Node2D card = new() { Position = new Vector2(Fx.Rand(-150f, 300f), Fx.Rand(120f, 420f)) };
			layer.AddChild(card);
			Sprite2D s = new() { Texture = tex, Scale = Vector2.One * size };
			card.AddChild(s);
			float dur = Fx.Rand(10f, 13f), spin = Fx.Rand(-1f, 1f), flip = Fx.Rand(1.4f, 2.2f), sway = Fx.Rand(30f, 60f), ph = Fx.Rand(0f, Mathf.Tau);
			Vector2 from = card.Position, vel = new(Fx.Rand(110f, 150f), Fx.Rand(18f, 40f));
			Fx.Animate(card, 0, dur * (1f - start), u =>
			{
				float t = (start + u * (1f - start)) * dur;
				card.Position = from + vel * t + new Vector2(0f, sway * Mathf.Sin(t * 0.8f + ph));
				card.Rotation = spin * t * 0.25f + 0.25f * Mathf.Sin(t * 1.2f + ph);
				s.Scale = new Vector2(size * Mathf.Cos(t * flip + ph), size);
				s.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t / 0.8f) * Mathf.Min(1f, (dur - t) / 1.2f));
			});
			Fx.FreeAfter(card, dur + 0.1);
		}
		Spawn(Fx.Rand(0.3f, 0.5f));
		Tween loop = layer.CreateTween().SetLoops();
		loop.TweenInterval(7.0);
		loop.TweenCallback(Callable.From(() => Spawn(0f)));
	}

	private static Shader? _shade;

	/// <summary>左侧（信息面板那边）和底部（角色按钮那边）压暗一点，衬字。</summary>
	private static Shader ShadeShader => _shade ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 res = vec2(2520.0, 1680.0);
uniform vec2 pad = vec2(300.0, 300.0);
void fragment() {
	vec2 px = UV * res - pad;
	float left = 0.42 * clamp(1.0 - px.x / 860.0, 0.0, 1.0);
	float bottom = 0.45 * clamp((px.y - 880.0) / 200.0, 0.0, 1.0);
	COLOR = vec4(0.0, 0.0, 0.0, clamp(left + bottom, 0.0, 0.75));
}" };

	private static void Shade(Control screen)
	{
		Vector2 pad = new(300f, 300f), size = new(SW + 600f, SH + 600f);
		ColorRect c = new() { Name = "Shade", Position = -pad, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = ShadeShader } };
		((ShaderMaterial)c.Material).SetShaderParameter("res", size);
		((ShaderMaterial)c.Material).SetShaderParameter("pad", pad);
		screen.AddChild(c);
	}

	/// <summary>左上角标题：斜的黑带 + TAKE YOUR HEART 剪贴字（P5 菜单标题的位置），纸片隔几秒随机一块跳一下、歪一点。</summary>
	private static void Header(Rig r)
	{
		Node2D header = new() { Name = "Header", Position = new Vector2(-146f, 112f), Rotation = Mathf.DegToRad(-4f) };
		r.Screen.AddChild(header);
		r.Header = header;
		header.AddChild(new Sprite2D { Texture = Need("cs_header_band"), Centered = false, Position = new Vector2(-4f, -4f) });
		const float bk = 0.52f;
		for (int i = 0; i < BannerParts.Length; i++)
		{
			if (Fx.Tex($"cs_banner_{i}") is not Texture2D tex) continue;
			Sprite2D s = new() { Texture = tex, Scale = Vector2.One * bk, Position = new Vector2(170f, 16f) + (BannerParts[i] + tex.GetSize() * 0.5f) * bk };
			header.AddChild(s);
			r.Letters.Add(s);
		}
		Tween hop = header.CreateTween().SetLoops();
		hop.TweenInterval(2.8);
		hop.TweenCallback(Callable.From(() =>
		{
			if (r.Letters.Count == 0) return;
			Sprite2D s = r.Letters[Fx.Rng.Next(r.Letters.Count)];
			if (!GodotObject.IsInstanceValid(s)) return;
			float rot = Fx.Rand(-0.08f, 0.08f);
			Vector2 home = s.Position;
			Tween t = s.CreateTween();
			t.TweenProperty(s, "position", home + new Vector2(0f, -10f), 0.07).SetEase(Tween.EaseType.Out);
			t.Parallel().TweenProperty(s, "rotation", rot, 0.07);
			t.TweenProperty(s, "position", home, 0.18).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}));
	}

	// ───────────────────────── 立绘面板 ─────────────────────────

	private static Shader? _clip;

	/// <summary>
	/// 立绘面板里所有东西共用的着色器：
	/// · 斜切口裁剪：只画切口右边（面板里）的像素，按全局坐标算（立绘滑入 / 手浮动时切口不跟着动）；
	/// · warp = 1：立绘变形（cs_warp 权重图：R 头以脖子为支点轻摆、G 外圈发梢随风飘、B 左边领尖抖动；呼吸让上半身起伏、胸口微鼓）——
	///   描边、身体、手、眨眼贴片用同一套参数，所以永远对得上；rect = 这张贴图在立绘画布里的位置；
	/// · lid：眨眼贴片只画在眼皮这条线上面（眼皮往下合 / 往上睁）；
	/// · mode 1 = 剪影：只用贴图透明度，颜色 = tint（入场残影用）。
	/// </summary>
	private static Shader ClipShader => _clip ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 cut_a;
uniform vec2 cut_b;
uniform float mode = 0.0;
uniform vec4 tint : source_color = vec4(1.0);
uniform float warp = 0.0;
uniform sampler2D warp_tex : filter_linear;
uniform vec4 rect = vec4(0.0, 0.0, 1024.0, 1536.0);
uniform vec2 canvas = vec2(1024.0, 1536.0);
uniform vec2 pivot = vec2(540.0, 640.0);
uniform float head_rot = 0.0;
uniform vec2 head_off = vec2(0.0);
uniform float tip_t = 0.0;
uniform vec2 collar = vec2(0.0);
uniform float breath = 0.0;
uniform float lid = 100000.0;
uniform float lid_tilt = 0.0;
varying vec2 world;
varying vec4 vmod;
void vertex() {
	world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
	vmod = COLOR;
}
void fragment() {
	vec2 p = rect.xy + UV * rect.zw;
	vec2 q = p;
	vec2 uv = UV;
	if (warp > 0.5) {
		vec3 w = texture(warp_tex, p / canvas).rgb;
		vec2 rel = p - pivot;
		vec2 dh = head_rot * vec2(-rel.y, rel.x) + head_off;
		vec2 dt = vec2(6.0 * sin(tip_t * 2.73 + p.y * 0.02 + p.x * 0.005), 2.5 * sin(tip_t * 2.17 + p.x * 0.02));
		float up = clamp((1450.0 - p.y) / 750.0, 0.0, 1.0);
		float chest = clamp((p.y - 560.0) / 160.0, 0.0, 1.0) * clamp((1450.0 - p.y) / 300.0, 0.0, 1.0);
		vec2 db = vec2((p.x - 512.0) * 0.008 * chest, -8.0 * up) * breath;
		q = p - (w.r * dh + w.g * dt + w.b * collar + db);
		uv = (q - rect.xy) / rect.zw;
	}
	vec4 c = texture(TEXTURE, uv);
	if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0) {
		c.a = 0.0;
	}
	c *= vmod;
	if (mode > 0.5) {
		c = vec4(tint.rgb, tint.a * c.a);
	}
	vec2 d = cut_b - cut_a;
	float side = (d.y * (world.x - cut_a.x) - d.x * (world.y - cut_a.y)) / length(d);
	c.a *= clamp(side + 0.5, 0.0, 1.0);
	float lid_y = lid + (q.x - 608.0) * lid_tilt;
	c.a *= smoothstep(lid_y + 7.0, lid_y - 7.0, q.y);
	COLOR = c;
}" };

	private static ShaderMaterial ClipMaterial(Rig r, float mode = 0f, Color? tint = null, Rect2? warpRect = null)
	{
		ShaderMaterial m = new() { Shader = ClipShader };
		m.SetShaderParameter("cut_a", G(r, CutA));
		m.SetShaderParameter("cut_b", G(r, CutB));
		m.SetShaderParameter("mode", mode);
		m.SetShaderParameter("tint", tint ?? Colors.White);
		if (warpRect is Rect2 wr)
		{
			m.SetShaderParameter("warp", 1f);
			m.SetShaderParameter("warp_tex", Need("cs_warp"));
			m.SetShaderParameter("rect", new Vector4(wr.Position.X, wr.Position.Y, wr.Size.X, wr.Size.Y));
			m.SetShaderParameter("pivot", NeckPivot);
		}
		return m;
	}

	private static Sprite2D Part(Rig r, Node2D parent, Texture2D tex, Vector2 at, ShaderMaterial? material = null)
	{
		Sprite2D s = new() { Texture = tex, Centered = false, Position = at, Material = material ?? r.Clip };
		parent.AddChild(s);
		return s;
	}

	private static void Portrait(Rig r)
	{
		r.Clip = ClipMaterial(r);
		// 面板底：切口右边整块暗红黑（锯齿之间、人物四周露出来的都是它）
		r.Panel = new ColorRect { Name = "Panel", Position = new Vector2(CutA.X - 40f, -60f), Size = new Vector2(SW - CutA.X + 700f, SH + 120f),
			Color = PanelInk, MouseFilter = Control.MouseFilterEnum.Ignore, Material = r.Clip };
		r.Screen.AddChild(r.Panel);
		Node2D group = new() { Name = "Joker", Position = POrigin, Scale = Vector2.One * PScale };
		r.Screen.AddChild(group);
		r.Group = group;
		// 面板里垫着的锯齿球：红色，压在暗红黑底上，慢慢呼吸
		Node2D burst = new() { Name = "Burst", Position = BurstAt };
		group.AddChild(burst);
		r.Burst = burst;
		burst.AddChild(new Sprite2D { Texture = Need("cs_burst"), Modulate = Crimson, Scale = Vector2.One * BurstK, Material = r.Clip });
		Tween breathe = burst.CreateTween().SetLoops();
		breathe.TweenProperty(burst, "scale", Vector2.One * 1.025f, 2.4).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		breathe.TweenProperty(burst, "scale", Vector2.One, 2.4).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		// 身体（呼吸）：chest 支点在画布底边中点。描边（离线烘焙的白 + 黑）画在人物后面，手的那份跟着手动
		Node2D chest = new() { Position = Pivot };
		group.AddChild(chest);
		r.Chest = chest;
		Node2D Arm()
		{
			Node2D a = new() { Position = ArmPivot - Pivot };
			chest.AddChild(a);
			return a;
		}
		Texture2D blinkTex = Need("cs_blink");
		r.Warp = ClipMaterial(r, warpRect: new Rect2(0f, 0f, 1024f, 1536f));
		r.BlinkMat = ClipMaterial(r, warpRect: new Rect2(BlinkAt, blinkTex.GetSize()));
		r.BlinkMat.SetShaderParameter("lid", LidTop);
		r.BlinkMat.SetShaderParameter("lid_tilt", LidTilt);
		Part(r, chest, Need("cs_body_line"), -Pivot, r.Warp);
		Node2D armLine = Arm();
		Part(r, armLine, Need("cs_hand_line"), -ArmPivot, r.Warp);
		Part(r, chest, Need("cs_body"), -Pivot, r.Warp);
		r.Blink = Part(r, chest, blinkTex, BlinkAt - Pivot, r.BlinkMat);
		Node2D armFront = Arm();
		Part(r, armFront, Need("cs_hand"), -ArmPivot, r.Warp);
		r.Arms = new[] { armLine, armFront };
		// 眼睛的金光（跟着胸口一起呼吸摆动；顺着眼线倾斜）：大柔光 → 虹膜染金 → 亮芯 → 横向光条
		Node2D eye = new() { Name = "Eye", Position = Eye - Pivot, Rotation = EyeRoll };
		chest.AddChild(eye);
		r.Eye = eye;
		r.EyeHalo = Glow.SoftSprite(eye, Vector2.Zero, new Color(EyeTint, 0.22f), 4.6f);
		r.EyeIris = Glow.SoftSprite(eye, Vector2.Zero, new Color(EyeTint, 0.55f), 1.9f);
		r.EyeCore = Glow.SoftSprite(eye, new Vector2(1f, -1f), new Color(EyeHot, 0.85f), 0.7f);
		r.EyeStreak = new Sprite2D { Texture = Need("cs_eye_streak"), Material = Fx.Additive, Modulate = new Color(EyeTint, 0.55f) };
		eye.AddChild(r.EyeStreak);
		// 隔几秒虹膜右上角闪一下四角星（P5R 原版星光贴图）
		Texture2D? star = Fx.Tex("star4");
		Tween glint = eye.CreateTween().SetLoops();
		glint.TweenInterval(4.2);
		glint.TweenCallback(Callable.From(() =>
		{
			if (star == null || r.Open < 0.95f || Fx.Rng.NextDouble() < 0.25) return;
			Sprite2D s = new() { Texture = star, Material = Fx.Additive, Modulate = EyeHot, Position = new Vector2(11f, -10f), Scale = Vector2.Zero };
			eye.AddChild(s);
			float k = 34f / Mathf.Max(1f, star.GetSize().X);
			Fx.Animate(s, 0, 0.55, t =>
			{
				s.Scale = Vector2.One * k * Mathf.Sin(t * Mathf.Pi);
				s.Rotation = 0.6f * t;
			});
			Fx.FreeAfter(s, 0.6);
		}));
	}

	private static Shader? _band;

	/// <summary>
	/// 斜条：切口左边 o0..o1（屏幕像素 × 全局缩放）的一条带子，边缘抗锯齿（原生 Polygon2D 的斜边有锯齿）。
	/// reveal 0..1：沿切口方向从上（from_top = 1）或从下画出来（入场）。
	/// </summary>
	private static Shader BandShader => _band ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 cut_a;
uniform vec2 cut_b;
uniform float o0 = 0.0;
uniform float o1 = 10.0;
uniform vec4 color : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float reveal = 1.0;
uniform float from_top = 1.0;
varying vec2 world;
void vertex() { world = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy; }
void fragment() {
	vec2 d = cut_b - cut_a;
	float L = length(d);
	vec2 rel = world - cut_a;
	float left = (d.x * rel.y - d.y * rel.x) / L;
	float along = dot(rel, d) / (L * L);
	float a = smoothstep(o0 - 0.6, o0 + 0.6, left) * (1.0 - smoothstep(o1 - 0.6, o1 + 0.6, left));
	float edge = mix(-0.08, 1.08, reveal);
	float shown = from_top > 0.5 ? 1.0 - smoothstep(edge - 0.01, edge, along) : smoothstep(1.0 - edge, 1.0 - edge + 0.01, along);
	COLOR = vec4(color.rgb, color.a * a * shown);
}" };

	/// <summary>切口上的斜条组：黑色实心斜板（压住切口）+ 红条 + 白色细线 + 外侧半透明宽带；斜板上滚动竖排小字。</summary>
	private static void Stripes(Rig r)
	{
		Control at = Layer(r.Screen, "Stripes");
		float gs = r.Screen.GetGlobalTransform().Scale.X;
		(float o0, float o1, Color c)[] bands =
		{
			(-10f, 74f, Ink),                               // 黑色斜板（往面板里多压 10px，盖住裁剪的边）
			(74f, 90f, Crimson),                            // 红条
			(102f, 107f, Colors.White),                     // 白色细线
			(150f, 236f, new Color(Ink, 0.32f)),            // 外侧半透明宽带
		};
		foreach (var (o0, o1, c) in bands)
		{
			ColorRect rect = new() { Position = new Vector2(CutA.X - 360f, -60f), Size = new Vector2(CutB.X - CutA.X + 420f, SH + 120f),
				MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = BandShader } };
			ShaderMaterial m = (ShaderMaterial)rect.Material;
			m.SetShaderParameter("cut_a", G(r, CutA));
			m.SetShaderParameter("cut_b", G(r, CutB));
			m.SetShaderParameter("o0", o0 * gs);
			m.SetShaderParameter("o1", o1 * gs);
			m.SetShaderParameter("color", c);
			at.AddChild(rect);
			r.Bands.Add(m);
		}
		// 竖排小字：沿斜板中线（切口左边 32px）往下读，字头朝面板；一个循环单元宽正好是贴图宽，滚动首尾相接
		Texture2D text = Need("cs_slab_text");
		Vector2 u = (CutB - CutA).Normalized();
		Vector2 leftN = new(-u.Y, u.X);
		float unit = text.GetSize().X * 0.5f;
		Node2D lane = new() { Name = "SlabText", Position = CutA + leftN * 32f, Rotation = Mathf.Atan2(u.Y, u.X) };
		at.AddChild(lane);
		Node2D scroll = new() { Position = new Vector2(-unit - 120f, 0f) };
		lane.AddChild(scroll);
		for (int i = 0; i < 3; i++)
		{
			scroll.AddChild(new Sprite2D { Texture = text, Centered = false, Scale = Vector2.One * 0.5f, Position = new Vector2(i * unit, -24f) });
		}
		Tween roll = scroll.CreateTween().SetLoops();
		roll.TweenProperty(scroll, "position:x", -120f, unit / 28.0).From(-unit - 120f);
		r.SlabText = lane;
	}

	// ───────────────────────── 动起来 ─────────────────────────

	/// <summary>常驻：呼吸、微摆、手浮动、眨眼、眼睛金光呼吸（闭眼时熄掉）、选中那一下的眼睛闪光。</summary>
	private static void Animate(Rig r)
	{
		double nextBlink = 2.2;
		float blinkAt = -10f;
		ulong last = Stage.NowMs();
		void Step()
		{
			ulong now = Stage.NowMs();
			float dt = Mathf.Min(0.1f, (now - last) / 1000f);
			last = now;
			r.T += dt;
			float t = (float)r.T;
			// 立绘变形参数（着色器 ClipShader 里同一套公式）：头以脖子为支点轻摆 ±0.8°（发顶约 ±9px）+ 轻轻左右挪；
			// 领尖随风抖；呼吸 4.2s 一次，上半身起伏 8px、胸口微鼓；发梢的波在着色器里按 tip_t 走
			float rot = 0.014f * Mathf.Sin(t * Mathf.Tau / 6f);
			Vector2 headOff = new(2f * Mathf.Sin(t * Mathf.Tau / 6f + 0.7f), 0f);
			Vector2 collar = new(7f * Mathf.Sin(t * Mathf.Tau / 1.9f) + 2f * Mathf.Sin(t * Mathf.Tau / 0.83f), -4f * Mathf.Sin(t * Mathf.Tau / 1.9f + 0.6f));
			float breath = 0.5f - 0.5f * Mathf.Cos(t * Mathf.Tau / 4.2f);
			// 眨眼：眼皮 0.07s 合上（越合越快）→ 停 0.04s → 0.13s 睁开（越睁越慢）
			float b = t - blinkAt;
			float close = b < 0f ? 0f : b < 0.07f ? (b / 0.07f) * (b / 0.07f) : b < 0.11f ? 1f : b < 0.24f ? 1f - Fx.OutCubic((b - 0.11f) / 0.13f) : 0f;
			if (b >= 0.24f && t >= nextBlink)
			{
				blinkAt = t;
				nextBlink = t + (Fx.Rng.NextDouble() < 0.2 ? 0.32 : Fx.Rand(2.8f, 5.2f));   // 偶尔连眨两下
			}
			r.Open = 1f - close;
			foreach (ShaderMaterial m in new[] { r.Warp, r.BlinkMat })
			{
				m.SetShaderParameter("head_rot", rot);
				m.SetShaderParameter("head_off", headOff);
				m.SetShaderParameter("tip_t", t);
				m.SetShaderParameter("collar", collar);
				m.SetShaderParameter("breath", breath);
			}
			r.BlinkMat.SetShaderParameter("lid", Mathf.Lerp(LidTop, LidBottom, close));
			// 手：忽远忽近（用户 2026-11-17）—— 以手掌下方为支点放大 / 缩小 ±3.5%（近大远小，指尖来回约 17px），3.2s 一个来回；
			// 指尖在头的范围里，还会跟着脸一起动；整只手再轻轻浮一点
			float near = Mathf.Sin(t * Mathf.Tau / 3.2f);
			Vector2 armOff = new(1.2f * Mathf.Sin(t * 1.1f) - 3f * near, 1.6f * Mathf.Sin(t * 0.83f + 1.2f) + 2f * near);
			foreach (Node2D a in r.Arms)
			{
				a.Position = ArmPivot - Pivot + armOff;
				a.Scale = Vector2.One * (1f + 0.035f * near);
			}
			// 眼睛的光跟着头动（同一套位移，眼睛在头部权重 = 1 的地方、在胸口以上）
			Vector2 rel = Eye - NeckPivot;
			Vector2 eyeMove = rot * new Vector2(-rel.Y, rel.X) + headOff + new Vector2(0f, -8f * breath);
			r.Eye.Position = Eye - Pivot + eyeMove;
			r.Eye.Rotation = EyeRoll + rot;
			// 眼睛金光：慢慢呼吸（2.6s 一次），眼皮合上时跟着暗掉；选中那一下（Flare）猛地一亮、光条拉长
			float f = r.Flare;
			float lit = Mathf.SmoothStep(0.55f, 1f, 1f - close);                 // 眼睛睁开过半才亮回来（不会亮在合着的眼皮上）
			float k = lit * (0.82f + 0.18f * Mathf.Sin(t * Mathf.Tau / 2.6f) + 1.4f * f);
			r.EyeHalo.Modulate = new Color(EyeTint, Mathf.Min(1f, 0.26f * k));
			r.EyeIris.Modulate = new Color(EyeTint, Mathf.Min(1f, 0.75f * k));
			r.EyeCore.Modulate = new Color(EyeHot, Mathf.Min(1f, 0.85f * k));
			r.EyeCore.Scale = Vector2.One * (0.7f + 0.9f * f);
			r.EyeStreak.Modulate = new Color(EyeTint, Mathf.Min(1f, 0.55f * k));
			r.EyeStreak.Scale = new Vector2((420f + 50f * Mathf.Sin(t * 1.9f) + 700f * f) / 512f, (22f + 10f * f) / 64f);
		}
		// 逐帧：SceneTree.ProcessFrame（场景被原版换掉后自动解绑）
		SceneTree? tree = r.Root.GetTree();
		if (tree != null)
		{
			void OnFrame()
			{
				if (!GodotObject.IsInstanceValid(r.Group))
				{
					tree.ProcessFrame -= OnFrame;
					return;
				}
				Stage.Safe("charselect frame", Step);
			}
			tree.ProcessFrame += OnFrame;
		}
	}

	/// <summary>
	/// 入场：白闪一下；斜条沿切口方向错开着从上 / 下画出来；立绘从右边带红 / 黑两道残影滑入（残影也按切口裁）；
	/// 左上标题从左边滑进来。intro：同一时刻原版眼神特写切入 + 召唤舞台（横幅往左挪、看得见主眼），碎开那一下眼睛的金光猛地一亮（光条拉长），再退回常驻。
	/// </summary>
	private static void Entrance(Rig r, bool intro)
	{
		Node2D g = r.Group;
		Vector2 home = g.Position;
		const float slide = 0.38f;
		ColorRect flash = new() { Position = new Vector2(-300f, -300f), Size = new Vector2(SW + 600f, SH + 600f), Color = new Color(1f, 0.96f, 0.96f, 0.5f),
			MouseFilter = Control.MouseFilterEnum.Ignore };
		r.Screen.AddChild(flash);
		Fx.Animate(flash, 0, 0.16, t => flash.Color = new Color(1f, 0.96f, 0.96f, 0.5f * (1f - t)));
		Fx.FreeAfter(flash, 0.2);
		// 斜条：错开 0.05s，一上一下
		for (int i = 0; i < r.Bands.Count; i++)
		{
			ShaderMaterial m = r.Bands[i];
			m.SetShaderParameter("from_top", i % 2 == 0 ? 1f : 0f);
			m.SetShaderParameter("reveal", 0f);
			Fx.Animate(r.SlabText, 0.04 * i, 0.32, t => m.SetShaderParameter("reveal", Ease5.OutExpo(t)));
		}
		r.SlabText.Modulate = new Color(1f, 1f, 1f, 0f);
		Fx.Animate(r.SlabText, 0.22, 0.3, t => r.SlabText.Modulate = new Color(1f, 1f, 1f, t));
		// 面板底跟着斜条一起亮出来
		r.Panel.Modulate = new Color(1f, 1f, 1f, 0f);
		Fx.Animate(r.Panel, 0, 0.18, t => r.Panel.Modulate = new Color(1f, 1f, 1f, t));
		// 标题从左边滑进来
		Vector2 hHome = r.Header.Position;
		r.Header.Position = hHome + new Vector2(-900f, 0f);
		Fx.Animate(r.Header, 0.06, 0.36, t => r.Header.Position = hHome + new Vector2(-900f * (1f - Ease5.OutExpo(t)), 0f));
		// 残影：同一张身体图的红 / 黑剪影，落后一点，也按切口裁
		var echoes = new List<(Sprite2D s, float lag)>();
		Texture2D body = Need("cs_body");
		foreach ((Color c, float lag) in new[] { (Ink, 0.09f), (Crimson, 0.05f) })
		{
			Sprite2D e = new() { Texture = body, Centered = false, Scale = Vector2.One * PScale, Material = ClipMaterial(r, 1f, c) };
			r.Screen.AddChild(e);
			r.Screen.MoveChild(e, g.GetIndex());
			echoes.Add((e, lag));
		}
		float X(float t) => home.X + 560f * (1f - Ease5.OutExpo(Mathf.Clamp(t / slide, 0f, 1f)));
		g.Position = new Vector2(X(0f), home.Y);
		g.Modulate = new Color(1f, 1f, 1f, 0f);
		Fx.Animate(g, 0, slide + 0.2, u =>
		{
			float t = u * (slide + 0.2f);
			g.Position = new Vector2(X(t), home.Y);
			g.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(t / 0.12f, 0f, 1f));
			foreach (var (s, lag) in echoes)
			{
				if (!GodotObject.IsInstanceValid(s)) continue;
				s.Position = new Vector2(X(t - lag), home.Y);
				s.Modulate = new Color(1f, 1f, 1f, 0.75f * (1f - Mathf.Clamp((t - slide * 0.6f) / 0.25f, 0f, 1f)) * Mathf.Clamp(t / 0.06f, 0f, 1f));
			}
		});
		foreach (var (s, _) in echoes) Fx.FreeAfter(s, slide + 0.3);
		if (!intro)
		{
			Sfx.Play(Sfx.CutIn, 0.6f);                      // 不播切入时只有立绘 / 斜条进场，一声轻的切入
			return;
		}
		// 眼神特写和入场同时切进来（用户 2026-11-17：不要后入），整条往左挪，主眼落在斜条左边看得见的地方，碎裂也从主眼开始。
		// 声音分三层，各管一件事、不叠在一起：0.03s 切入「嗖」（铺垫，SummonStage 里）→ 0.2s 原版眼神特写的那一下（主音，CutIn 里）
		// → 0.52s 碎裂（高潮，CutIn 里）+ 一层低频垫底，和眼睛闪光同一刻；之后安静。原版静默猎手的选中音效在 CharSelectPatches 里拦掉了。
		Vector2 screen = Fx.Screen(r.Back);
		CutInPlayback clip = new(SummonStage.EvokeAt - 0.05f) { Offset = new Vector2(-CutInShift * CutIn.BandScale(screen) / 1.23f, 0f) };
		Vector2 eyeAt = CutIn.MainEye(screen) + clip.Offset;
		clip.ShatterFrom = eyeAt;
		SummonStage.Play(r.Back, r.Front, r.Back.GetCanvasTransform().AffineInverse() * eyeAt, maskFlame: false, playback: clip);
		CutIn.Eyes(r.Back, clip);
		Fx.After(g, SummonStage.EvokeAt, () =>
		{
			Sfx.Play(Sfx.SubBoom, 0.45f, 0.03f);              // 和 CutIn 里的碎裂声同一刻（碎裂在裂纹定格之后）
			Glow.Flash(r.Front, r.Eye.GlobalPosition, EyeTint, 300f * g.GetGlobalTransform().Scale.X, 0.35f);
			Tween t = r.Group.CreateTween();
			t.TweenMethod(Callable.From<float>(v => r.Flare = v), 0f, 1f, 0.08);
			t.TweenMethod(Callable.From<float>(v => r.Flare = v), 1f, 0f, 1.0).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		});
	}
}
