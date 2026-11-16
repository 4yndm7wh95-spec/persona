using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 选人界面（静默猎手的 char_select_bg_silent 场景，设计见 docs/p5/CHARSELECT_PLAN.md）：原版画面藏起来，换成 P5R 风格的楼顶夜景 ——
/// 深红天空 + 缓慢旋转的黑色放射线 + 漂移的黑云；东京天际线剪影（窗灯明灭）、高架上偶尔驶过的电车、楼下扫上来的探照灯；
/// 斜的 TAKE YOUR HEART 剪贴字色带（纸片不时跳一下）；楼顶前景 + 坐在水塔上的摩尔加纳（尾巴摆）；风里翻飞的预告信、一闪的星光。
/// 右边是 Joker 立绘（art/p5/charselect，分层：身体 / 眨眼 / 手），身后红色锯齿爆炸形 + 白色描边；
/// 眼睛泛蓝光，手和脸前方一层淡淡的蓝焰面具渐隐渐现（局内召唤的 PersonaMask，透明度压低）。
/// 选中时：从右侧带红黑残影滑入 → 原版眼神特写切入 + 召唤舞台 → 横幅碎开那一下蓝焰面具亮起、亚森在身后现身 → 退回常驻。
/// 只用 Godot 类型（预览工具 tools/p5/fxpreview 直接编译这个文件）；挂到游戏里的补丁在 Patches.cs 的 CharSelectBgPatch。
/// 坐标：场景根 Control 的本地坐标（2560×1200，tscn 的 offset -960,-521），本地 (0..1920, 0..1080) 大致就是 1080p 屏幕。
/// </summary>
public static class CharSelectScene
{
	public const float W = 2560f, H = 1200f;
	/// <summary>设置里关掉"选人动画"时为 false：不播切入 / 亚森，直接常驻。</summary>
	public static Func<bool> IntroEnabled = () => true;

	// 立绘：原画布 1024×1536（贴图按 0.75 存）。显示范围按用户 2026-11-16 的截图：宽度用满、右边贴屏幕右边，
	// 画布 y 0..1400 落在屏幕 6%..100%（再往下的伸出屏幕）。
	private const float CanvasW = 1024f, CanvasH = 1536f, TexK = 0.75f;
	private const float ScreenW = 1920f, ScreenH = 1080f;
	private static readonly float PScale = ScreenH * 0.94f / 1400f;
	private static readonly Vector2 POrigin = new(ScreenW - CanvasW * PScale, ScreenH * 0.06f);
	private static readonly Vector2 Pivot = new(512f, 1536f);                 // 呼吸 / 微摆的支点：画布底边中点
	private static readonly Vector2 ArmPivot = new(150f, 1230f);              // 手（连前臂袖子）浮动的支点：袖口根部
	private static readonly Vector2 BlinkAt = new(330f, 320f);                // tools/p5/charselect_assets.py BLINK_BOX 左上角
	private static readonly Vector2 Eye = new(578f, 430f);                    // 露出来的那只眼睛（画面右边那只）
	private static readonly Vector2 EyeHidden = new(432f, 452f);              // 刘海 / 手指下面那只
	private static readonly Vector2 MaskAt = new(505f, 440f);                 // 蓝焰面具中心：两眼之间
	private const float MaskHalf = 150f;                                      // 面具半宽（画布像素）
	private const float MaskFacing = -1f, MaskRoll = -0.08f;
	private const float MaskSoft = 0.36f, MaskIntro = 0.85f;                  // 常驻 / 选中那一下的透明度
	// TAKE YOUR HEART 拆块（charselect_assets.py banner_parts 的输出）：在整条里的左上角
	private static readonly Vector2[] BannerParts = { new(0, 0), new(228, 8), new(492, 8), new(958, 0), new(1164, 16), new(1276, 14) };
	private static readonly Color Crimson = new("e0101e"), Ink = new("0a0708"), Ice = new("bfe9ff");

	private sealed class Rig
	{
		public Control Root = null!, Back = null!, Front = null!;
		public Node2D Group = null!, Chest = null!, ArmBack = null!, ArmFront = null!;
		public Sprite2D Blink = null!, EyeDot = null!, EyeStreak = null!, EyeHiddenDot = null!, Glow = null!;
		public PersonaMask? Mask;
		public float MaskAlpha;
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

	private static void BuildInto(Control root, bool intro)
	{
		Sky(root);
		Searchlights(root);
		Skyline(root);
		Train(root);
		Banner(root);
		Rooftop(root);
		Cards(root);
		Stars(root);
		Rig r = new() { Root = root };
		r.Back = Layer(root, "Back");
		Portrait(r);
		r.Front = Layer(root, "Front");
		Animate(r);
		Entrance(r, intro);
	}

	private static Control Layer(Control root, string name)
	{
		Control c = new() { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(W, H) };
		root.AddChild(c);
		return c;
	}

	// ───────────────────────── 背景 ─────────────────────────

	private static Shader? _sky;

	/// <summary>深红 → 近黑；黑色放射线绕 center 慢转（越往外越淡）；上半部几缕黑云横向漂；每隔 9 秒一道很淡的白光斜扫过去。</summary>
	private static Shader SkyShader => _sky ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 res = vec2(3160.0, 1800.0);
uniform vec2 center = vec2(1060.0, 600.0);
uniform float clock = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.02 + 7.1; a *= 0.5; } return v; }
void fragment() {
	vec2 px = UV * res;
	float y = UV.y;
	vec3 top = vec3(0.62, 0.03, 0.07), mid = vec3(0.36, 0.01, 0.04), bot = vec3(0.06, 0.0, 0.015);
	vec3 col = mix(top, mid, smoothstep(0.0, 0.55, y));
	col = mix(col, bot, smoothstep(0.45, 1.0, y));
	// 中心一团亮红
	float d = length((px - center) / vec2(1.0, 0.9));
	col += vec3(0.35, 0.02, 0.04) * exp(-d / 420.0);
	// 放射线：14 道，边缘稍软，绕中心慢转（60 秒一圈）
	float ang = atan(px.y - center.y, px.x - center.x) + clock * 6.2831 / 60.0;
	float ray = smoothstep(0.42, 0.62, sin(ang * 14.0) * 0.5 + 0.5);
	float rayFade = smoothstep(120.0, 360.0, d) * (1.0 - smoothstep(900.0, 2200.0, d) * 0.6);
	col *= 1.0 - 0.5 * ray * rayFade;
	// 黑云：上半部横向漂
	vec2 q = px / vec2(900.0, 260.0) + vec2(clock * 0.012, 0.0);
	float cloud = smoothstep(0.52, 0.8, fbm(q + vec2(0.0, fbm(q * 1.7) * 0.6)));
	col *= 1.0 - 0.55 * cloud * (1.0 - smoothstep(0.25, 0.7, y));
	// 白光斜扫
	float cyc = mod(clock, 9.0) / 1.4;
	float band = (px.x + px.y * 0.6) / (res.x + res.y * 0.6);
	float sweep = cyc < 1.0 ? exp(-pow((band - (cyc * 1.4 - 0.2)) / 0.04, 2.0)) : 0.0;
	col += vec3(1.0, 0.85, 0.85) * sweep * 0.06;
	COLOR = vec4(col, 1.0);
}" };

	private static void Sky(Control root)
	{
		const float pad = 300f;
		Vector2 size = new(W + 2 * pad, H + 2 * pad);
		ColorRect sky = new() { Name = "Sky", Position = new Vector2(-pad, -pad), Size = size, MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = SkyShader } };
		ShaderMaterial m = (ShaderMaterial)sky.Material;
		m.SetShaderParameter("res", size);
		m.SetShaderParameter("center", new Vector2(760f + pad, 300f + pad));
		root.AddChild(sky);
		Fx.Clock(sky);
	}

	/// <summary>楼下两道探照灯：淡红白的锥形光，从城市后面扫上来，各自慢慢左右摆。</summary>
	private static void Searchlights(Control root)
	{
		Node2D at = new() { Name = "Searchlights" };
		root.AddChild(at);
		(Vector2 from, float a0, float amp, float period)[] beams = { (new(380f, 1000f), -0.32f, 0.22f, 11f), (new(1020f, 1000f), 0.18f, 0.26f, 14f) };
		foreach (var (from, a0, amp, period) in beams)
		{
			Polygon2D beam = new()
			{
				Polygon = new[] { new Vector2(-6f, 0f), new Vector2(6f, 0f), new Vector2(150f, -1250f), new Vector2(-150f, -1250f) },
				VertexColors = new[] { new Color(1f, 0.8f, 0.8f, 0.22f), new Color(1f, 0.8f, 0.8f, 0.22f), new Color(1f, 0.6f, 0.6f, 0f), new Color(1f, 0.6f, 0.6f, 0f) },
				Material = Fx.Additive, Position = from, Rotation = a0,
			};
			at.AddChild(beam);
			float ph = Fx.Rand(0f, Mathf.Tau);
			Tween t = beam.CreateTween().SetLoops();
			t.TweenMethod(Callable.From<float>(v => beam.Rotation = a0 + amp * Mathf.Sin(v + ph)), 0f, Mathf.Tau, period);
		}
	}

	private static Shader? _windows;

	/// <summary>天际线：黑剪影不动，亮的像素（窗灯、电波塔的灯）按小格子各自明灭。</summary>
	private static Shader WindowShader => _windows ??= new Shader { Code = @"
shader_type canvas_item;
uniform float clock = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	float lum = max(c.r, max(c.g, c.b));
	vec2 cell = floor(UV * vec2(180.0, 80.0));
	float h = hash(cell);
	// 每格自己的节奏：大多数时间亮着，偶尔灭一会儿；少数格子缓慢呼吸
	float t = clock * (0.05 + 0.12 * h) + h * 17.0;
	float on = step(0.16, fract(t));
	float breathe = 0.8 + 0.2 * sin(clock * (0.6 + h) + h * 40.0);
	float k = mix(1.0, mix(0.18, 1.15, on) * breathe, smoothstep(0.18, 0.35, lum));
	COLOR = vec4(c.rgb * k, c.a);
}" };

	private static void Skyline(Control root)
	{
		Texture2D tex = Need("cs_skyline");
		Sprite2D s = new() { Name = "Skyline", Texture = tex, Centered = false, Position = new Vector2(-20f, 1030f - tex.GetSize().Y),
			Material = new ShaderMaterial { Shader = WindowShader } };
		root.AddChild(s);
		Fx.Clock(s);
	}

	/// <summary>高架电车：一串亮着窗的车厢从左往右驶过天际线中段，每 13~18 秒一趟。</summary>
	private static void Train(Control root)
	{
		Node2D line = new() { Name = "Train", Position = new Vector2(0f, 872f) };
		root.AddChild(line);
		Node2D train = new() { Position = new Vector2(-900f, 0f) };
		line.AddChild(train);
		const float car = 92f, gap = 6f;
		for (int i = 0; i < 7; i++)
		{
			float x = i * (car + gap);
			Fx.Poly(train, new[] { new Vector2(x, -15f), new Vector2(x + car, -15f), new Vector2(x + car, 0f), new Vector2(x, 0f) }, Ink);
			for (int w = 0; w < 6; w++)
			{
				float wx = x + 8f + w * 14f;
				Fx.Poly(train, new[] { new Vector2(wx, -11f), new Vector2(wx + 9f, -11f), new Vector2(wx + 9f, -5f), new Vector2(wx, -5f) }, new Color(1f, 0.93f, 0.8f, 0.95f));
			}
			Glow.SoftSprite(train, new Vector2(x + car * 0.5f, -8f), new Color(1f, 0.85f, 0.7f, 0.18f), 3.2f).Scale = new Vector2(4.2f, 0.9f);
		}
		Glow.SoftSprite(train, new Vector2(7 * (car + gap) + 4f, -7f), new Color(1f, 0.95f, 0.85f, 0.7f), 1.2f);   // 车头灯
		void Run()
		{
			if (!GodotObject.IsInstanceValid(train)) return;
			train.Position = new Vector2(-900f, 0f);
			Tween t = train.CreateTween();
			t.TweenProperty(train, "position:x", 1750f, 6.5);
			t.TweenInterval(Fx.Rand(7f, 12f));
			t.TweenCallback(Callable.From(Run));
		}
		Fx.After(train, 1.5, Run);
	}

	/// <summary>斜色带：半透明红带 + TAKE YOUR HEART 剪贴字（拆成几块），整条慢慢左右漂，隔几秒随机一块纸片抖一下、歪一点。</summary>
	private static void Banner(Control root)
	{
		Node2D band = new() { Name = "Banner", Position = new Vector2(-260f, 760f), Rotation = Mathf.DegToRad(-8f) };
		root.AddChild(band);
		Fx.Poly(band, new[] { new Vector2(0f, -70f), new Vector2(2700f, -70f), new Vector2(2700f, 70f), new Vector2(0f, 70f) }, new Color(0.72f, 0f, 0.06f, 0.5f));
		Fx.Poly(band, new[] { new Vector2(0f, 76f), new Vector2(2700f, 76f), new Vector2(2700f, 80f), new Vector2(0f, 80f) }, new Color(0f, 0f, 0f, 0.7f));
		Node2D words = new() { Position = new Vector2(420f, -68f), Scale = Vector2.One * 0.7f };
		band.AddChild(words);
		var parts = new List<Sprite2D>();
		for (int i = 0; i < BannerParts.Length; i++)
		{
			if (Fx.Tex($"cs_banner_{i}") is not Texture2D tex) continue;
			Vector2 half = tex.GetSize() * 0.5f;
			Sprite2D s = new() { Texture = tex, Position = BannerParts[i] + half };
			words.AddChild(s);
			parts.Add(s);
		}
		Tween drift = words.CreateTween().SetLoops();
		drift.TweenProperty(words, "position:x", 520f, 14.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		drift.TweenProperty(words, "position:x", 420f, 14.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		Tween hop = words.CreateTween().SetLoops();
		hop.TweenInterval(2.6);
		hop.TweenCallback(Callable.From(() =>
		{
			if (parts.Count == 0) return;
			Sprite2D s = parts[Fx.Rng.Next(parts.Count)];
			if (!GodotObject.IsInstanceValid(s)) return;
			float rot = Fx.Rand(-0.09f, 0.09f);
			Vector2 home = s.Position;
			Tween t = s.CreateTween();
			t.TweenProperty(s, "position", home + new Vector2(0f, -14f), 0.07).SetEase(Tween.EaseType.Out);
			t.Parallel().TweenProperty(s, "rotation", rot, 0.07);
			t.TweenProperty(s, "position", home, 0.18).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}));
	}

	/// <summary>楼顶前景（黑剪影 + 红轮廓光）+ 水塔上的摩尔加纳：尾巴慢摆，隔一阵耳朵（整只）轻轻一抖。天线顶的红灯慢闪。</summary>
	private static void Rooftop(Control root)
	{
		Texture2D tex = Need("cs_rooftop");
		const float k = 0.95f;
		Vector2 at = new(60f, 1110f - tex.GetSize().Y * k);
		Sprite2D roof = new() { Name = "Rooftop", Texture = tex, Centered = false, Position = at, Scale = Vector2.One * k };
		root.AddChild(roof);
		// 天线顶（贴图里 x≈195, y≈38）
		Sprite2D lamp = Glow.SoftSprite(root, at + new Vector2(197f, 36f) * k, new Color(1f, 0.1f, 0.12f, 0.9f), 0.7f);
		Tween blink = lamp.CreateTween().SetLoops();
		blink.TweenProperty(lamp, "modulate:a", 0.05f, 0.9).SetDelay(0.6);
		blink.TweenProperty(lamp, "modulate:a", 0.9f, 0.15);
		// 摩尔加纳坐在水塔顶（贴图里水塔顶 x 40..165, y≈80；摩尔加纳贴图 314×314，屁股在 y≈300）
		if (Fx.Tex("cs_morgana") is Texture2D mt && Fx.Tex("cs_morgana_tail") is Texture2D tt)
		{
			const float mk = 0.36f;
			Node2D cat = new() { Name = "Morgana", Position = at + new Vector2(102f, 82f) * k, Scale = Vector2.One * mk };
			root.AddChild(cat);
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
			twitch.TweenProperty(cat, "scale", new Vector2(mk * 1.03f, mk * 0.97f), 0.08);
			twitch.TweenProperty(cat, "scale", Vector2.One * mk, 0.22).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		}
	}

	/// <summary>预告信被风吹着从左上往右飘过，边飘边翻（横向缩放模拟翻面）、打转；同时最多 3 张。</summary>
	private static void Cards(Control root)
	{
		if (Fx.Tex("cs_card") is not Texture2D tex) return;
		Node2D layer = new() { Name = "Cards" };
		root.AddChild(layer);
		void Spawn(bool warm)
		{
			if (!GodotObject.IsInstanceValid(layer)) return;
			float size = Fx.Rand(0.16f, 0.26f);
			Node2D card = new() { Position = new Vector2(Fx.Rand(-200f, 500f), Fx.Rand(-120f, 380f)) };
			layer.AddChild(card);
			Sprite2D s = new() { Texture = tex, Scale = Vector2.One * size };
			card.AddChild(s);
			float dur = Fx.Rand(9f, 13f), spin = Fx.Rand(-1.2f, 1.2f), flip = Fx.Rand(1.6f, 2.6f), sway = Fx.Rand(30f, 70f), ph = Fx.Rand(0f, Mathf.Tau);
			Vector2 from = card.Position, vel = new(Fx.Rand(150f, 230f), Fx.Rand(30f, 80f));
			float start = warm ? Fx.Rand(0.2f, 0.7f) : 0f;
			Fx.Animate(card, 0, dur * (1f - start), u =>
			{
				float t = (start + u * (1f - start)) * dur;
				card.Position = from + vel * t + new Vector2(0f, sway * Mathf.Sin(t * 0.9f + ph));
				card.Rotation = spin * t * 0.3f + 0.3f * Mathf.Sin(t * 1.3f + ph);
				s.Scale = new Vector2(size * Mathf.Cos(t * flip + ph), size);
				s.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t / 0.8f) * Mathf.Min(1f, (dur - t) / 1.2f));
			});
			Fx.FreeAfter(card, dur + 0.1);
		}
		for (int i = 0; i < 2; i++) Spawn(true);
		Tween loop = layer.CreateTween().SetLoops();
		loop.TweenInterval(4.2);
		loop.TweenCallback(Callable.From(() => Spawn(false)));
	}

	/// <summary>天上不时一闪的白色四角星（星光贴图 star4，原版 P5R 的）。</summary>
	private static void Stars(Control root)
	{
		Texture2D? star = Fx.Tex("star4");
		Node2D layer = new() { Name = "Stars" };
		root.AddChild(layer);
		Tween loop = layer.CreateTween().SetLoops();
		loop.TweenInterval(0.7);
		loop.TweenCallback(Callable.From(() =>
		{
			Vector2 at = new(Fx.Rand(80f, 1250f), Fx.Rand(40f, 560f));
			Node2D s = star != null
				? new Sprite2D { Texture = star, Material = Fx.Additive, Position = at, Scale = Vector2.Zero }
				: Glow.SoftSprite(layer, at, new Color(1f, 1f, 1f, 0.8f), 0f);
			if (s.GetParent() == null) layer.AddChild(s);
			float k = Fx.Rand(0.25f, 0.55f) * (star != null ? 64f / star.GetSize().X : 1f);
			Fx.Animate(s, 0, 0.9, t =>
			{
				float e = Mathf.Sin(t * Mathf.Pi);
				s.Scale = Vector2.One * k * e;
				s.Rotation = t * 0.6f;
			});
			Fx.FreeAfter(s, 1.0);
		}));
	}

	// ───────────────────────── 立绘 ─────────────────────────

	private static Shader? _outline;

	/// <summary>剪影描边：周围一圈（radius 像素）里只要有不透明的就画成 tint。画在立绘后面 = 外描边。</summary>
	private static Shader OutlineShader => _outline ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec4 tint : source_color = vec4(1.0);
uniform float radius = 6.0;
void fragment() {
	float a = 0.0;
	for (int i = 0; i < 16; i++) {
		float ang = 6.2831853 * float(i) / 16.0;
		vec2 o = vec2(cos(ang), sin(ang)) * radius * TEXTURE_PIXEL_SIZE;
		a = max(a, texture(TEXTURE, UV + o).a);
		a = max(a, texture(TEXTURE, UV + o * 0.5).a);
	}
	COLOR = vec4(tint.rgb, tint.a * a);
}" };

	private static Sprite2D Layer(Node2D parent, Texture2D tex, Vector2 canvasPos, Shader? shader = null)
	{
		Sprite2D s = new() { Texture = tex, Centered = false, Position = canvasPos, Scale = Vector2.One / TexK };
		if (shader != null) s.Material = new ShaderMaterial { Shader = shader };
		parent.AddChild(s);
		return s;
	}

	private static void Portrait(Rig r)
	{
		Texture2D body = Need("cs_body"), hand = Need("cs_hand"), blink = Need("cs_blink");
		Node2D group = new() { Name = "Joker", Position = POrigin, Scale = Vector2.One * PScale };
		r.Root.AddChild(group);
		r.Group = group;
		// 身后：红色锯齿爆炸形 + 错开的黑色同形（慢慢呼吸、极慢地转）
		if (Fx.Tex("cs_burst") is Texture2D burst)
		{
			Node2D b = new() { Position = new Vector2(560f, 520f) };
			group.AddChild(b);
			Sprite2D shadow = new() { Texture = burst, Modulate = Ink, Position = new Vector2(26f, 26f), Scale = Vector2.One * 1.45f };
			Sprite2D red = new() { Texture = burst, Modulate = Crimson, Scale = Vector2.One * 1.45f };
			b.AddChild(shadow);
			b.AddChild(red);
			Tween t = b.CreateTween().SetLoops();
			t.TweenProperty(b, "scale", Vector2.One * 1.03f, 2.2).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			t.TweenProperty(b, "scale", Vector2.One, 2.2).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			Tween spin = b.CreateTween().SetLoops();
			spin.TweenProperty(b, "rotation", Mathf.Tau, 240.0).AsRelative();
		}
		// 身体（呼吸）：chest 支点在画布底边中点
		Node2D chest = new() { Position = Pivot };
		group.AddChild(chest);
		r.Chest = chest;
		Node2D Arm()
		{
			Node2D a = new() { Position = ArmPivot - Pivot };
			return a;
		}
		Vector2 o = -Pivot;
		// 描边（红色宽光晕 → 白色细边），身体和手各一份；手的那份放在跟手同步的节点里
		Sprite2D glowBody = Layer(chest, body, o, OutlineShader);
		r.ArmBack = Arm();
		chest.AddChild(r.ArmBack);
		Sprite2D glowHand = Layer(r.ArmBack, hand, -ArmPivot, OutlineShader);
		Sprite2D lineBody = Layer(chest, body, o, OutlineShader);
		Node2D armLine = Arm();
		chest.AddChild(armLine);
		Sprite2D lineHand = Layer(armLine, hand, -ArmPivot, OutlineShader);
		foreach (Sprite2D g in new[] { glowBody, glowHand })
		{
			((ShaderMaterial)g.Material).SetShaderParameter("tint", new Color(1f, 0.05f, 0.1f, 0.45f));
			((ShaderMaterial)g.Material).SetShaderParameter("radius", 22f * TexK);
		}
		foreach (Sprite2D l in new[] { lineBody, lineHand })
		{
			((ShaderMaterial)l.Material).SetShaderParameter("tint", new Color(1f, 1f, 1f, 1f));
			((ShaderMaterial)l.Material).SetShaderParameter("radius", 7f * TexK);
		}
		r.Glow = glowBody;
		Layer(chest, body, o);
		r.Blink = Layer(chest, blink, BlinkAt - Pivot);
		r.Blink.Visible = false;
		// 眼睛蓝光（在手下面：手指挡住的那只只透一点）
		r.EyeHiddenDot = Glow.SoftSprite(chest, EyeHidden - Pivot, new Color(Ice, 0.25f), 1.6f);
		r.ArmFront = Arm();
		chest.AddChild(r.ArmFront);
		Layer(r.ArmFront, hand, -ArmPivot);
		r.EyeDot = Glow.SoftSprite(chest, Eye - Pivot, new Color(PersonaMask.Hi, 0.6f), 1.5f);
		r.EyeStreak = Glow.SoftSprite(chest, Eye - Pivot + new Vector2(6f, 0f), new Color(PersonaMask.Hi, 0.5f), 1f);
		r.EyeStreak.Scale = new Vector2(5.5f, 0.32f);
		// 手的三个节点同步
		r.Group.SetMeta("arm_line", armLine);
	}

	/// <summary>常驻：呼吸、微摆、手浮动、眨眼、红光晕呼吸、眼睛蓝光跟着面具亮暗；蓝焰面具渐现 → 烧一会儿 → 渐隐 → 歇一会儿 → 循环。</summary>
	private static void Animate(Rig r)
	{
		Node2D armLine = (Node2D)r.Group.GetMeta("arm_line");
		double nextBlink = 2.5;
		ulong last = Stage.NowMs();
		Callable step = Callable.From(() =>
		{
			ulong now = Stage.NowMs();
			float dt = Mathf.Min(0.1f, (now - last) / 1000f);
			last = now;
			r.T += dt;
			float t = (float)r.T;
			r.Chest.Scale = new Vector2(1f, 1f + 0.0045f * Mathf.Sin(t * Mathf.Tau / 4.2f));
			r.Chest.Rotation = 0.0035f * Mathf.Sin(t * Mathf.Tau / 7.3f);
			Vector2 armOff = new(2.2f * Mathf.Sin(t * 1.1f), 3.2f * Mathf.Sin(t * 0.83f + 1.2f));
			float armRot = 0.006f * Mathf.Sin(t * 0.7f + 0.4f);
			foreach (Node2D a in new[] { r.ArmBack, armLine, r.ArmFront })
			{
				a.Position = ArmPivot - Pivot + armOff;
				a.Rotation = armRot;
			}
			if (t >= nextBlink)
			{
				r.Blink.Visible = true;
				Fx.After(r.Blink, 0.11, () => r.Blink.Visible = false);
				nextBlink = t + (Fx.Rng.NextDouble() < 0.2 ? 0.25 : Fx.Rand(2.8f, 5.2f));   // 偶尔连眨两下
			}
			float m = r.MaskAlpha;
			float eye = 0.45f + 0.18f * Mathf.Sin(t * 1.7f) + 0.9f * m;
			r.EyeDot.Modulate = new Color(PersonaMask.Hi, Mathf.Min(1f, eye));
			r.EyeStreak.Modulate = new Color(PersonaMask.Hi, Mathf.Min(1f, eye * 0.8f));
			r.EyeStreak.Scale = new Vector2(5.5f + 2.5f * m + 0.4f * Mathf.Sin(t * 2.3f), 0.32f);
			r.EyeHiddenDot.Modulate = new Color(Ice, 0.12f + 0.25f * m);
			r.Glow.Modulate = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(t * Mathf.Tau / 3.3f));
			if (r.Mask != null)
			{
				r.Mask.Follow(r.Chest.ToGlobal(MaskAt - Pivot));
				r.Mask.SetOpacity(r.MaskAlpha);
			}
		});
		// 逐帧：用 SceneTree 的 process_frame（Node2D 没法在 C# 里动态挂 _Process）
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
				Stage.Safe("charselect frame", () => step.Call());
			}
			tree.ProcessFrame += OnFrame;
		}
	}

	/// <summary>点燃一张淡面具：alpha 从 0 升到 peak（rise 秒）→ 烧 hold 秒 → 降到 0（fall 秒）→ 收掉；然后歇 2~3 秒再来一张。</summary>
	private static void MaskCycle(Rig r, float peak, float rise, float hold, float fall, float settleTo = -1f)
	{
		if (!GodotObject.IsInstanceValid(r.Group)) return;
		Vector2 c = r.Chest.ToGlobal(MaskAt - Pivot);
		float half = MaskHalf * r.Group.GetGlobalTransform().Scale.X;
		PersonaMask mask = new(r.Back, r.Front, c, half, MaskFacing, MaskRoll);
		r.Mask = mask;
		r.MaskAlpha = 0f;
		mask.SetOpacity(0f);
		Tween t = r.Group.CreateTween();
		t.TweenMethod(Callable.From<float>(v => r.MaskAlpha = v), 0f, peak, rise).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
		if (settleTo >= 0f)
		{
			t.TweenMethod(Callable.From<float>(v => r.MaskAlpha = v), peak, settleTo, 0.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
			peak = settleTo;
		}
		t.TweenInterval(hold);
		t.TweenMethod(Callable.From<float>(v => r.MaskAlpha = v), peak, 0f, fall).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.In);
		t.TweenCallback(Callable.From(() =>
		{
			mask.End(false);
			if (r.Mask == mask) r.Mask = null;
			r.MaskAlpha = 0f;
		}));
		t.TweenInterval(Fx.Rand(2f, 3f));
		t.TweenCallback(Callable.From(() => MaskCycle(r, MaskSoft, 1.2f, 2.5f, 1.2f)));
	}

	/// <summary>
	/// 入场：白闪一下，立绘从右侧带红 / 黑两道残影滑入（0.35s）。intro：随后原版眼神特写切入 + 召唤舞台，
	/// 横幅碎开那一下面具蓝焰炸亮（之后退到常驻的淡）、亚森在身后现身；否则直接开始常驻的面具循环。
	/// </summary>
	private static void Entrance(Rig r, bool intro)
	{
		Node2D g = r.Group;
		Vector2 home = g.Position;
		const float slide = 0.35f;
		ColorRect flash = new() { Position = new Vector2(-300f, -300f), Size = new Vector2(W + 600f, H + 600f), Color = new Color(1f, 0.96f, 0.96f, 0.5f),
			MouseFilter = Control.MouseFilterEnum.Ignore };
		r.Root.AddChild(flash);
		r.Root.MoveChild(flash, r.Root.GetChildCount() - 1);
		Fx.Animate(flash, 0, 0.16, t => flash.Color = new Color(1f, 0.96f, 0.96f, 0.5f * (1f - t)));
		Fx.FreeAfter(flash, 0.2);
		// 残影：同一张身体图，红 / 黑剪影，落后一点
		var echoes = new List<(Sprite2D s, float lag)>();
		Texture2D body = Need("cs_body");
		foreach ((Color c, float lag) in new[] { (Ink, 0.09f), (Crimson, 0.05f) })
		{
			Sprite2D e = new() { Texture = body, Centered = false, Scale = Vector2.One * PScale / TexK, Modulate = c,
				Material = new ShaderMaterial { Shader = OutlineShader } };
			((ShaderMaterial)e.Material).SetShaderParameter("tint", c);
			((ShaderMaterial)e.Material).SetShaderParameter("radius", 0f);
			r.Root.AddChild(e);
			r.Root.MoveChild(e, g.GetIndex());
			echoes.Add((e, lag));
		}
		float X(float t) => home.X + 520f * (1f - Ease5.OutExpo(Mathf.Clamp(t / slide, 0f, 1f)));
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
				s.Modulate = new Color(s.Modulate, 0.75f * (1f - Mathf.Clamp((t - slide * 0.6f) / 0.25f, 0f, 1f)) * Mathf.Clamp(t / 0.06f, 0f, 1f));
			}
		});
		foreach (var (s, _) in echoes) Fx.FreeAfter(s, slide + 0.3);
		Sfx.Play(Sfx.CutIn, 0.6f, 0.03f);
		if (!intro)
		{
			Fx.After(g, slide, () => MaskCycle(r, MaskSoft, 1.2f, 2.5f, 1.2f));
			return;
		}
		Fx.After(g, slide + 0.05, () =>
		{
			Vector2 head = r.Chest.ToGlobal(MaskAt - Pivot);
			CutInPlayback clip = new(SummonStage.EvokeAt - 0.05f);
			SummonStage.Play(r.Back, r.Front, head, maskFlame: false, playback: clip);
			CutIn.Eyes(r.Back, clip);
			Fx.After(g, SummonStage.EvokeAt, () =>
			{
				Vector2 c = r.Chest.ToGlobal(MaskAt - Pivot);
				Sfx.Play(Sfx.PersonaFlame, 0.85f);
				Glow.Flash(r.Front, c, PersonaMask.Hi, 700f * g.GetGlobalTransform().Scale.X, 0.4f);
				MaskCycle(r, MaskIntro, 0.12f, 1.1f, 1.2f, settleTo: MaskSoft);
				// 亚森在身后（立绘左后方）从蓝焰里现身，不出手，停一会儿退场
				Vector2 feet = g.ToGlobal(new Vector2(380f, 1500f));
				float tall = 1150f * g.GetGlobalTransform().Scale.X;
				ArseneFx.Show(r.Back, feet, tall, 1f, 9f, 1.9f);
			});
		});
	}
}
