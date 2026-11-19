using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Godot;

namespace P5Joker;

/// <summary>
/// 选人界面（静默猎手的 char_select_bg_silent 场景，设计见 docs/p5/CHARSELECT_PLAN.md）。构图照结城理 mod 的选人界面：
/// 立绘是贴着右边、占满屏幕高度的大特写，左边一道斜切口把它收进一块面板里（切口上压一组 P5 的黑 / 红 / 白斜条 + 滚动小字），
/// 面板是暗红黑底，上面压着红色锯齿爆炸形（用户 2026-11-17：锯齿球要有层次、黑底加粗）；左边是干净的场景：深红夜空（只有很淡的云）、压低的东京天际线黑剪影（窗灯明灭）、
/// 面板旁边楼顶上一座水塔；左上角是 TAKE YOUR HEART 剪贴字标题（P5 菜单的标题位置）；偶尔一张预告信飘过。
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
	public const float InfoPanelDelay = Rise + CutInSplit + 0.15f;
	private static readonly Color EyeTint = new(1f, 0.7f, 0.18f), EyeHot = new(1f, 0.95f, 0.78f);   // 金色光 / 白热的芯
	// 斜切口（屏幕坐标，上端 → 下端）：切口左边的立绘不画，斜条压在切口上。斜度照 P3（约 11°），位置贴着手套左边（手套不被切）
	private static readonly Vector2 CutA = new(1133f, 0f), CutB = new(1349f, 1080f);
	// 入场节奏（照 P3）：立绘沿切口从下往上升入 0.45s → 落定后眼神特写切入 → 停 0.65s 后裂开（裂纹定格 0.05s）→ 碎片炸开 → 信息面板淡入
	private const float Rise = 0.45f, CutInSplit = 0.65f;
	private const float ShatterAt = Rise + CutInSplit + 0.05f;
	// 天际线顶边（屏幕 y）、远景再高一点
	private const float Horizon = 700f;
	private static readonly Color Crimson = new("de101e"), Ink = new("0a0608");
	private static readonly string[] CityTextures = { "cs_skyline", "cs_card", "cs_rooftop" };
	private static readonly string[] PortraitTextures =
	{
		"cs_body", "cs_hand", "cs_body_line", "cs_hand_line", "cs_blink", "cs_warp", "cs_eye_streak", "star4", "cs_slab_text",
	};
	private static readonly string[] IntroTextures = { "eyes", "cutin_tail_left", "cutin_tail_right" };
	private static readonly string[] InfoTextures = { "cs_info_head", "cs_info_ghost" };

	private sealed class Rig
	{
		public Control Root = null!, Screen = null!, Back = null!, Front = null!;
		public Node2D Group = null!, Chest = null!;
		public Node2D[] Arms = Array.Empty<Node2D>();
		public Sprite2D Blink = null!, EyeHalo = null!, EyeIris = null!, EyeCore = null!, EyeStreak = null!;
		public ShaderMaterial Warp = null!, BlinkMat = null!;
		public float Open = 1f;                                                // 眼睛睁开的程度（眨眼时 1 → 0 → 1）
		public Node2D Eye = null!;
		public float Flare;                                                    // 选中那一下的眼睛闪光 0..1
		public List<(Polygon2D strip, bool fromTop, float delay)> Bands = new();
		public Node2D Tag = null!;
		public Node2D SlabText = null!;
		public ShaderMaterial Clip = null!;
		public double T;
	}

	/// <summary>搭好整个场景。bg = 原版场景根；全部搭完才藏原版（出错时拆掉自己的，原版保持原样）。</summary>
	public static void Build(Control bg, bool intro)
	{
		Control root = CreateRoot(bg);
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
		HideOriginal(bg, root);
	}

	// The real screen can accept input while its textures load. Draw the background,
	// portrait and trim on separate frames, keeping their first draws apart.
	internal static async Task<bool> BuildAsync(Control bg, bool intro, Func<bool> current,
		Func<bool>? canDraw = null, bool prepareInfo = true)
	{
		if (!Fx.Active(bg)) return false;
		SceneTree tree = bg.GetTree();
		async Task<bool> NextFrame()
		{
			do
				await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			while (current() && canDraw?.Invoke() == false);
			return current();
		}

		long began = Stopwatch.GetTimestamp();
		Control? root = null;
		bool shown = false;
		try
		{
			if (!await NextFrame()) return false;
			root = CreateRoot(bg);
			Rig r = CreateRig(root);
			Sky(r.Screen);
			await Fx.PrepareTextures(tree, CityTextures, current);
			if (!await NextFrame()) return false;
			BuildCity(r.Screen);

			await Fx.PrepareTextures(tree, PortraitTextures, current);
			if (!await NextFrame()) return false;
			if (intro) CutIn.PollPreload();
			r.Back = Layer(r.Screen, "Back");
			Portrait(r);
			r.Group.Modulate = new Color(1f, 1f, 1f, 0f);
			if (!await NextFrame()) return false;
			Stripes(r, entering: true);
			if (intro)
			{
				await Fx.PrepareTextures(tree, IntroTextures, current);
				if (!await NextFrame()) return false;
				while (!CutIn.PollPreload())
					if (!await NextFrame()) return false;
			}
			if (prepareInfo)
				await Fx.PrepareTextures(tree, InfoTextures, current);
			if (!await NextFrame()) return false;
			if (intro || prepareInfo)
			{
				while (!P5Ui.PollFont(P5Ui.CjkBold))
					if (!await NextFrame()) return false;
			}
			if (prepareInfo)
			{
				while (!P5Ui.PollFont(P5Ui.CjkMedium))
					if (!await NextFrame()) return false;
			}
			r.Group.Modulate = Colors.White;
			Finish(r, intro, canDraw);
			HideOriginal(bg, root);
			shown = true;
			GD.Print($"[P5Joker] character select ready in {Stopwatch.GetElapsedTime(began).TotalMilliseconds:F1}ms");
			return true;
		}
		finally
		{
			if (!shown && root != null && GodotObject.IsInstanceValid(root))
			{
				root.GetParent()?.RemoveChild(root);
				root.QueueFree();
			}
		}
	}

	private static Control CreateRoot(Control bg)
	{
		Control root = new() { Name = "P5Select", MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(W, H) };
		bg.AddChild(root);
		bg.MoveChild(root, 0);
		return root;
	}

	private static void HideOriginal(Control bg, Control root)
	{
		foreach (Node c in bg.GetChildren())
		{
			if (c != root && c is CanvasItem ci)
			{
				ci.Visible = false;                   // 原版背景图、臭气线、静默猎手骨骼
			}
		}
	}

	internal static void Warm(Control slot)
	{
		Control sample = new()
		{
			Size = new Vector2(W, H), Scale = Vector2.One * 0.03f,
			Position = new Vector2(12f, 16f), MouseFilter = Control.MouseFilterEnum.Ignore,
		};
		slot.AddChild(sample);
		BuildInto(sample, false);
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
		Rig r = CreateRig(root);
		Sky(r.Screen);
		BuildCity(r.Screen);
		r.Back = Layer(r.Screen, "Back");
		Portrait(r);
		Stripes(r);
		Finish(r, intro);
	}

	private static Rig CreateRig(Control root)
	{
		Rig r = new() { Root = root };
		r.Screen = Layer(root, "Screen");
		r.Screen.Position = new Vector2(0f, ScreenTop);
		return r;
	}

	private static void BuildCity(Control screen)
	{
		Band(screen);
		City(screen);
		Cards(screen);
		Rooftop(screen);
		Shade(screen);
	}

	private static void Finish(Rig r, bool intro, Func<bool>? canDraw = null)
	{
		r.Front = Layer(r.Screen, "Front");
		Animate(r);
		Entrance(r, intro, canDraw);
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
	/// 楼顶前景（左右翻过来：水塔在最右、挨着立绘面板，栏杆 / 空调 / 广告架往左铺到底）。
	/// </summary>
	private static void Rooftop(Control screen)
	{
		Texture2D tex = Need("cs_rooftop");
		const float k = 0.72f, tankRight = 1010f;
		Vector2 at = new(tankRight - 1506f * k, SH + 40f - tex.GetSize().Y * k);
		screen.AddChild(new Sprite2D { Name = "Rooftop", Texture = tex, Centered = false, FlipH = true, Position = at, Scale = Vector2.One * k });
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

	private static Texture2D? _shade;

	/// <summary>左侧（信息面板那边）和底部（角色按钮那边）压暗一点，衬字。</summary>
	private static void Shade(Control screen)
	{
		Vector2 pad = new(300f, 300f), size = new(SW + 600f, SH + 600f);
		if (_shade == null)
		{
			// A static mask can share the normal texture pipeline with the rooftop.
			const int columns = 128, rows = 96;
			byte[] pixels = new byte[columns * rows * 4];
			for (int y = 0; y < rows; y++)
			{
				float screenY = (y + 0.5f) * size.Y / rows - pad.Y;
				float bottom = 0.45f * Mathf.Clamp((screenY - 880f) / 200f, 0f, 1f);
				for (int x = 0; x < columns; x++)
				{
					float screenX = (x + 0.5f) * size.X / columns - pad.X;
					float left = 0.42f * Mathf.Clamp(1f - screenX / 860f, 0f, 1f);
					pixels[(y * columns + x) * 4 + 3] = (byte)Mathf.Round(255f * Mathf.Min(left + bottom, 0.75f));
				}
			}
			_shade = ImageTexture.CreateFromImage(Image.CreateFromData(columns, rows, false, Image.Format.Rgba8, pixels));
		}
		screen.AddChild(new TextureRect { Name = "Shade", Position = -pad, Size = size, Texture = _shade,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Ignore });
	}

	/// <summary>
	/// P3 那条斜切色带（照结城理 mod 的 Band：-9°，位置低 90px 让开介绍板）：半透明黑带 + 下沿细红线 + 超大号淡字 TAKE YOUR HEART 缓慢漂移；
	/// 在天空和城市之间（城市剪影压在它前面），一路斜着穿到立绘后面。
	/// </summary>
	private static void Band(Control screen)
	{
		Node2D band = new() { Name = "Band", Position = new Vector2(-200f, 790f), Rotation = Mathf.DegToRad(-9f) };   // 比 P3 低 90px：让开左边介绍板的天赋说明
		screen.AddChild(band);
		P5Ui.Quad(band, new[] { new Vector2(0f, 0f), new Vector2(1900f, 0f), new Vector2(1820f, 150f), new Vector2(-80f, 150f) }, new Color(Ink, 0.26f));
		P5Ui.Quad(band, new[] { new Vector2(0f, 160f), new Vector2(1900f, 160f), new Vector2(1897f, 165f), new Vector2(-3f, 165f) }, new Color(Crimson, 0.6f));
		Label text = P5Ui.Text(band, "TAKE YOUR HEART    TAKE YOUR HEART    TAKE YOUR HEART", P5Ui.Font(P5Ui.Barlow), 130, new Color(1f, 0.92f, 0.92f, 0.12f), new Vector2(-300f, -10f));
		Tween drift = text.CreateTween().SetLoops();
		drift.TweenProperty(text, "position:x", 0f, 26.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		drift.TweenProperty(text, "position:x", -300f, 26.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
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
	c.a *= smoothstep(lid_y + 10.0, lid_y - 10.0, q.y);
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
		Node2D group = new() { Name = "Joker", Position = POrigin, Scale = Vector2.One * PScale };
		r.Screen.AddChild(group);
		r.Group = group;
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

	/// <summary>
	/// 切口上的斜条组（照 P3 mod 的 Slash：7 条宽窄不同的条，换成 P5 的黑 / 红 / 白），从切口往左量（负数 = 切口右边、面板里）：
	/// 外侧半透明黑宽带 232..300 · 黑色实心斜板 -9..92（压住切口）· 斜板上贴切口的红细线 6..12 · 贴切口的亮红线 -12..-6 ·
	/// 白条 108..116 · 红细线 134..137 · 最外侧白发丝线 206..208。斜板上滚动竖排小字（66px 处），斜板外沿一个红色标签块。
	/// 入场：各条沿切线方向分别从上 / 下错开切入（0.4s OutExpo，延迟同 P3），0.3s 小字淡入并开始滚动，0.35s 标签块弹出。
	/// </summary>
	private static void Stripes(Rig r, bool entering = false)
	{
		Control at = Layer(r.Screen, "Stripes");
		Vector2 cut = CutB - CutA, u = cut.Normalized();
		Node2D strips = new() { Position = CutA, Rotation = cut.Angle() };
		at.AddChild(strips);
		float length = cut.Length();
		(float o0, float o1, Color c, int enterFrom, float delay)[] bands =
		{
			(232f, 300f, new Color(Ink, 0.28f), +1, 0.12f),              // 外侧半透明黑宽带（从下）
			(-9f, 92f, new Color(Ink, 0.97f), -1, 0f),                   // 黑色实心斜板（从上）
			(6f, 12f, new Color(Crimson, 0.95f), +1, 0.05f),             // 斜板上贴切口的红细线（从下）
			(-12f, -6f, new Color(1f, 0.22f, 0.28f, 0.95f), -1, 0.04f),  // 贴切口的亮红线（从上）
			(108f, 116f, new Color(1f, 1f, 1f, 0.9f), -1, 0.1f),         // 白条（从上）
			(134f, 137f, new Color(Crimson, 0.6f), +1, 0.16f),           // 红细线（从下）
			(206f, 208f, new Color(1f, 1f, 1f, 0.35f), -1, 0.2f),        // 最外侧白发丝线（从上）
		};
		foreach (var (o0, o1, c, enterFrom, delay) in bands)
		{
			Polygon2D strip = new()
			{
				Polygon = new[] { new Vector2(0f, o0), new Vector2(length, o0), new Vector2(length, o1), new Vector2(0f, o1) },
				Color = c, Antialiased = true,
			};
			bool fromTop = enterFrom < 0;
			RevealStrip(strip, fromTop, entering ? 0f : 1f, length);
			strips.AddChild(strip);
			r.Bands.Add((strip, fromTop, delay));
		}
		Vector2 leftN = new(-u.Y, u.X);
		// 竖排小字：沿斜板（切口左边 62px），从上往下读，字头朝面板；30px、半透明白；一个循环单元宽正好是贴图宽，滚动首尾相接（26px/s）
		Texture2D text = Need("cs_slab_text");
		const float k = 0.44f;
		float unit = text.GetSize().X * k;
		Node2D lane = new() { Name = "SlabText", Position = CutA + leftN * 62f, Rotation = Mathf.Atan2(u.Y, u.X), Modulate = new Color(1f, 1f, 1f, 0.6f) };
		at.AddChild(lane);
		Node2D scroll = new() { Position = new Vector2(-unit - 120f, 0f) };
		lane.AddChild(scroll);
		for (int i = 0; i < 3; i++)
		{
			scroll.AddChild(new Sprite2D { Texture = text, Centered = false, Scale = Vector2.One * k, Position = new Vector2(i * unit, -text.GetSize().Y * k * 0.5f) });
		}
		Tween roll = scroll.CreateTween().SetLoops();
		roll.TweenProperty(scroll, "position:x", -120f, unit / 26.0).From(-unit - 120f);
		r.SlabText = lane;
		// 红色标签块（切口往下 72% 处）：P3 贴在斜板外沿，但这里外面是红天，红块看不见 → 压在黑斜板内侧（往左 28px，小字的里侧），竖条 16×140 + 下面一小块白
		Node2D tag = new() { Name = "Tag", Position = CutA + (CutB - CutA) * 0.72f + leftN * 28f, Rotation = Mathf.Atan2(u.Y, u.X) - Mathf.Pi / 2f };
		at.AddChild(tag);
		Fx.Poly(tag, new[] { new Vector2(-8f, -70f), new Vector2(8f, -70f), new Vector2(8f, 70f), new Vector2(-8f, 70f) }, new Color(Crimson, 0.95f));
		Fx.Poly(tag, new[] { new Vector2(-8f, 76f), new Vector2(8f, 76f), new Vector2(8f, 90f), new Vector2(-8f, 90f) }, new Color(1f, 1f, 1f, 0.9f));
		r.Tag = tag;
		if (entering)
		{
			lane.Modulate = new Color(1f, 1f, 1f, 0f);
			tag.Scale = Vector2.Zero;
		}
	}

	private static void RevealStrip(Polygon2D strip, bool fromTop, float progress, float length)
	{
		if (progress <= 0f)
		{
			strip.Hide();
			return;
		}
		float extent = 1.16f * progress;
		strip.Position = new Vector2(length * (fromTop ? -0.08f : 1.08f - extent), 0f);
		strip.Scale = new Vector2(extent, 1f);
		strip.Show();
	}

	// ───────────────────────── 动起来 ─────────────────────────

	private static float SmoothStep(float x) { x = Mathf.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }

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
			// 眨眼（用户 2026-11-17：原来 0.24s 一下太快，像闪了一下）：眼皮 0.10s 合上（先慢后快）→ 闭着停 0.06s → 0.22s 慢慢睁开，共 0.38s
			const float Shut = 0.10f, Hold = 0.06f, Rise = 0.22f, Whole = Shut + Hold + Rise;
			float b = t - blinkAt;
			float close = b < 0f ? 0f
				: b < Shut ? Mathf.Pow(b / Shut, 1.6f)
				: b < Shut + Hold ? 1f
				: b < Whole ? 1f - SmoothStep((b - Shut - Hold) / Rise) : 0f;   // 睁开两头慢、中间快（OutCubic 开头太猛，一两帧就睁完了）
			if (b >= Whole && t >= nextBlink)
			{
				blinkAt = t;
				nextBlink = t + (Fx.Rng.NextDouble() < 0.2 ? Whole + 0.12f : Fx.Rand(3f, 5.6f));   // 偶尔连眨两下
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
	/// 入场（照 P3 mod 的选人界面）：
	/// 0s 立绘沿斜切口从下往上升入（0.45s OutExpo，0.15s 淡入，后面拖红 / 白两道残影，0.3s 后淡掉；切口始终落在同一条斜线上）、
	///    斜条各自从上 / 下错开切入、面板底亮出、左上标题从左滑入；0.3s 小字淡入、0.35s 标签块弹出；
	/// 0.45s 立绘落定 → 眼神特写切入（横幅插在立绘下面、往左挪到看得见主眼的地方；背景压暗，不用召唤舞台 —— 不会整片闪白）；
	/// 1.15s 裂开：横幅上的裂纹 + 冲击点一小团闪光 + 碎片，同一刻立绘眼睛金光一亮；压暗在 0.6s 内退掉；随后原版信息面板淡入（CharSelectPatches）。
	/// 声音：0s 切入「嗖」（立绘 / 斜条进场）→ 0.65s 原版眼神特写的那一下（主音）→ 1.15s 碎裂 + 低频垫底；之后安静。
	/// </summary>
	private static void Entrance(Rig r, bool intro, Func<bool>? canDraw)
	{
		Node2D g = r.Group;
		Vector2 home = g.Position;
		// 斜条
		float length = (CutB - CutA).Length();
		foreach (var (strip, fromTop, delay) in r.Bands)
		{
			RevealStrip(strip, fromTop, 0f, length);
			Fx.Animate(strip, delay, 0.4, t => RevealStrip(strip, fromTop, Ease5.OutExpo(t), length));
		}
		r.SlabText.Modulate = new Color(1f, 1f, 1f, 0f);
		Fx.Animate(r.SlabText, 0.3, 0.4, t => r.SlabText.Modulate = new Color(1f, 1f, 1f, 0.6f * t));
		r.Tag.Scale = Vector2.Zero;
		Fx.Animate(r.Tag, 0.35, 0.3, t => r.Tag.Scale = Vector2.One * Mathf.Lerp(1.6f, 1f, Ease5.OutBack(t)) * Mathf.Clamp(t * 3f, 0f, 1f));
		// 立绘：沿切口方向从下往上升入（起点在切口方向往下 520px），两道残影（同一张身体图的剪影，也按切口裁）落后 0.05 / 0.1s
		Vector2 from = home + (CutB - CutA).Normalized() * 520f;
		Vector2 P(float t) => from.Lerp(home, Ease5.OutExpo(Mathf.Clamp(t / Rise, 0f, 1f)));
		var ghosts = new List<(Sprite2D s, float lag, float a)>();
		Texture2D body = Need("cs_body");
		foreach ((Color c, float lag, float a) in new[] { (new Color(1f, 0.92f, 0.92f), 0.1f, 0.3f), (Crimson, 0.05f, 0.5f) })
		{
			Sprite2D e = new() { Texture = body, Centered = false, Scale = Vector2.One * PScale, Material = ClipMaterial(r, 1f, c), Position = from };
			r.Screen.AddChild(e);
			r.Screen.MoveChild(e, g.GetIndex());
			ghosts.Add((e, lag, a));
		}
		g.Position = from;
		g.Modulate = new Color(1f, 1f, 1f, 0f);
		Fx.Animate(g, 0, 0.6, u =>
		{
			float t = u * 0.6f;
			g.Position = P(t);
			g.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(t / 0.15f, 0f, 1f));
			float fade = (1f - Fx.InCubic(Mathf.Clamp((t - 0.3f) / 0.3f, 0f, 1f))) * Mathf.Clamp(t / 0.1f, 0f, 1f);
			foreach (var (s, lag, a) in ghosts)
			{
				if (!GodotObject.IsInstanceValid(s)) continue;
				s.Position = P(t - lag);
				s.Modulate = new Color(1f, 1f, 1f, a * fade);
			}
		});
		foreach (var (s, _, _) in ghosts) Fx.FreeAfter(s, 0.65);
		Sfx.Play(Sfx.CutIn, 0.7f);                              // 立绘 / 斜条进场：一声切入
		if (!intro)
		{
			return;
		}
		// 立绘落定后：眼神特写
		Fx.After(g, Rise, () =>
		{
			if (canDraw?.Invoke() == false) return;
			Vector2 screen = Fx.Screen(r.Back);
			// 背景压暗（在立绘 / 斜条后面，横幅前面会盖上来）：0.2s 压到 0.55，碎开后 0.6s 退掉
			ColorRect dim = new() { Position = new Vector2(-300f, -300f), Size = new Vector2(SW + 600f, SH + 600f), Color = new Color(0.04f, 0f, 0.015f, 0f),
				MouseFilter = Control.MouseFilterEnum.Ignore };
			r.Back.AddChild(dim);
			Fx.Animate(dim, 0, 0.2, t => dim.Color = new Color(0.04f, 0f, 0.015f, 0.55f * Ease5.OutQuint(t)));
			Action clearDecor = CutInDecor(r);
			CutInPlayback clip = new(CutInSplit) { Offset = new Vector2(-CutInShift * CutIn.BandScale(screen) / 1.23f, 0f) };
			Vector2 eyeAt = CutIn.MainEye(screen) + clip.Offset;
			clip.ShatterFrom = eyeAt;
			CutIn.Eyes(r.Back, clip);
			Fx.After(g, ShatterAt - Rise, () =>
			{
				Vector2 impact = r.Back.GetCanvasTransform().AffineInverse() * eyeAt;
				Glow.Flash(r.Back, impact, new Color(1f, 0.93f, 0.88f), 380f * r.Screen.GetGlobalTransform().Scale.X, 0.2f);   // 冲击点一小团闪光（在立绘后面）
				Sfx.Play(Sfx.SubBoom, 0.45f, 0.033f);                 // 和 CutIn 里的碎裂声同一刻
				Fx.Animate(dim, 0, 0.6, t => dim.Color = new Color(0.04f, 0f, 0.015f, 0.55f * (1f - Fx.InCubic(t))));
				Fx.FreeAfter(dim, 0.65);
				clearDecor();
				Tween t = r.Group.CreateTween();
				t.TweenMethod(Callable.From<float>(v => r.Flare = v), 0f, 1f, 0.08);
				t.TweenMethod(Callable.From<float>(v => r.Flare = v), 1f, 0f, 1.0).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
			});
		});
	}

	/// <summary>
	/// 眼神特写期间的背景拼贴 + 左上名字标签（照结城理 mod CutInFx.BeginOn 的 decor / RedTag，配色换成 P5）：
	/// 左下白色斜切大色块（上沿一道红线）+ 上面斜着的超大人格面具名 ARSÈNE 水印、右上黑色斜块，从画面外滑入（0.3s）；
	/// 左上白底红字的名字标签「雨宮 蓮」+ 一行 PERSONA ▸ ARSÈNE。都在横幅后面 / 立绘后面（标签在最前）。返回碎开时调用的收尾。
	/// </summary>
	private static Action CutInDecor(Rig r)
	{
		Node2D decor = new() { Name = "CutInDecor" };
		r.Back.AddChild(decor);
		Node2D slabs = new();
		decor.AddChild(slabs);
		P5Ui.Slab(slabs, -200f, SW * 0.4f, SH * 0.64f, SH + 60f, 190f, new Color(0.94f, 0.93f, 0.93f, 0.92f));
		P5Ui.Slab(slabs, -200f, SW * 0.36f, SH * 0.64f, SH * 0.64f + 10f, 190f, new Color(Crimson, 0.95f));
		Node2D word = new() { Position = new Vector2(SW * 0.03f, SH * 1.08f), Rotation = -1.0f };
		slabs.AddChild(word);
		P5Ui.Text(word, "ARSÈNE", P5Ui.Font(P5Ui.Barlow), (int)(SH * 0.3f), new Color(0.07f, 0.02f, 0.03f, 0.88f), new Vector2(0f, -SH * 0.3f));
		Node2D top = new();
		decor.AddChild(top);
		P5Ui.Slab(top, SW * 0.5f, SW + 300f, -60f, SH * 0.15f, -140f, new Color(Ink, 0.92f));
		// 名字标签（最前面：Front 层）
		Node2D ui = new() { Name = "CutInTag" };
		r.Front.AddChild(ui);
		Font cjk = P5Ui.Font(P5Ui.CjkBold), lat = P5Ui.Font(P5Ui.Barlow);
		const string name = "雨宮 蓮";
		const int size = 44;
		float w = P5Ui.Width(cjk, name, size);
		// The stock backdrop can extend past the viewport at narrow aspect ratios.
		// Keep the entire nameplate inside the visible viewport throughout the slide.
		Vector2 TagPosition(float slide)
		{
			Rect2 visible = r.Front.GetViewportRect();
			Vector2 inset = visible.Size * new Vector2(slide, 0.12f);
			return r.Front.GetGlobalTransformWithCanvas().AffineInverse() * (visible.Position + inset);
		}
		Node2D tag = new() { Position = TagPosition(0.035f) };
		ui.AddChild(tag);
		P5Ui.Slab(tag, -12f, w + 40f, 8f, 70f, 14f, new Color(Ink, 0.95f));
		P5Ui.Slab(tag, -24f, w + 28f, 0f, 62f, 14f, new Color(0.97f, 0.96f, 0.96f, 1f));
		Node2D nameAt = new() { Position = new Vector2(8f, -2f), Skew = -0.2f };
		tag.AddChild(nameAt);
		P5Ui.Text(nameAt, name, cjk, size, Crimson, Vector2.Zero);
		Node2D sub = new() { Position = new Vector2(30f, 76f), Skew = -0.25f };
		tag.AddChild(sub);
		P5Ui.Text(sub, "PERSONA  \u25B8  ARSÈNE", lat, 26, new Color(1f, 0.93f, 0.93f), Vector2.Zero, 4, new Color(0.08f, 0f, 0.02f));
		Fx.Animate(decor, 0, 3.0, t =>
		{
			float time = t * 3f;
			float a = Ease5.OutQuint(Mathf.Clamp(time / 0.3f, 0f, 1f));
			float b = Ease5.OutQuint(Mathf.Clamp((time - 0.05f) / 0.3f, 0f, 1f));
			slabs.Position = new Vector2(-SW * 0.5f * (1f - a), 0f);
			top.Position = new Vector2(SW * 0.5f * (1f - b), 0f);
			word.Position = new Vector2(SW * 0.03f + 30f * time, SH * 1.08f);
		});
		Fx.Animate(tag, 0.04, 0.26, t =>
		{
			tag.Position = TagPosition(Mathf.Lerp(0.035f, 0.055f, Ease5.OutQuint(t)));
			tag.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(t * 4f, 0f, 1f));
		});
		return () =>
		{
			if (GodotObject.IsInstanceValid(decor))
			{
				Fx.Animate(decor, 0, 0.55, t =>
				{
					decor.Modulate = new Color(1f, 1f, 1f, 1f - t * t * (3f - 2f * t));
					decor.Scale = Vector2.One * (1f + 0.05f * Fx.OutCubic(t));
				});
				Fx.FreeAfter(decor, 0.6);
			}
			if (GodotObject.IsInstanceValid(ui))
			{
				Fx.Animate(ui, 0, 0.3, t =>
				{
					ui.Modulate = new Color(1f, 1f, 1f, 1f - t * t * (3f - 2f * t));
				});
				Fx.FreeAfter(ui, 0.35);
			}
		};
	}
}
