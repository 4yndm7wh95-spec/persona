using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 选人界面点「出发」之后的过场：替换原版静默猎手那段 0.8s 的黑色斜线淡出（docs/p5/CHARSELECT_PLAN.md「出发过场」）。只用 Godot，预览工具也能编译。
/// 用户 2026-11-17：不要锯齿、不要贴图，照 P5R 的 UI 做 —— 全部是代码画的直边色块 + 字体，P5 的几条做法：
///   · 少数几块大面、干净的直边（斜度和选人界面立绘那条斜切线一样：x 每往下 1 往右 0.2），边上带白色细线 + 一道黑缝 + 红带的层次；
///   · 红黑白三色；出手那一下整个画面平滑地变成红黑双色调（读屏幕的着色器，不是硬阈值）+ 一下推镜的冲击；
///   · 节奏：一下砸到位（指数缓出）→ 停一拍 → 甩出去；两扇门错开一拍到，形成两下重音。
/// Out（OutTime 后全黑，onBlack 之后游戏接原版的黑屏去加载）：
///   0       定格：双色调 + 推镜 + 一闪
///   0.05    右门滑进来（盖住右半边）；0.14 左门到，合上 —— 中间是两条红带拼成的红色斜带，两侧白线
///   0.31    TAKE / YOUR / HEART 三个词依次砸上去（白字 + 红色错位影子，倾斜）
///   0.62    字甩出去，红带缩回门后，白线淡掉 → 纯黑
/// In（InTime）：中间一道白线一闪 → 红带从门后张开 → 两扇门往两边滑开。
/// host：盖满全屏、画在所有界面上面的 Control（游戏里是 NGame 的 NTransition；预览里是最上层 CanvasLayer 里的 Control）。
/// 所有元素都由一个主时钟 Pose(t) 摆出来。
/// </summary>
public static class EmbarkTransition
{
	public const float OutTime = 0.9f;
	public const float InTime = 0.62f;

	private const float Slant = 0.2f;                 // 斜线：x 每往下 1 往右 0.2（= 选人界面立绘的斜切线 CutA→CutB）
	private const float H = 620f;                     // 色块上下伸到屏外
	private const float RedW = 150f;                  // 每扇门内沿的红带宽（两扇拼起来 300）
	private const float Gap = 14f;                    // 红带和白线之间的黑缝
	private const float LineW = 4f;                   // 白线宽

	private static readonly Color Red = new(0.9f, 0.07f, 0.1f);
	private static readonly Color Ink = new(0.025f, 0.012f, 0.016f);
	private static readonly Color White = new(1f, 0.97f, 0.95f);

	private static Shader? _strike;

	/// <summary>读屏幕：平滑的红黑双色调（暗部黑、中间调红、亮部白）+ 推镜（以画面中心放大）+ 一闪。</summary>
	private static Shader StrikeShader => _strike ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float amount = 0.0;
uniform float zoom = 1.0;
uniform float flash = 0.0;
uniform vec4 red : source_color = vec4(0.9, 0.07, 0.1, 1.0);
void fragment() {
	vec2 uv = vec2(0.5) + (SCREEN_UV - vec2(0.5)) / zoom;
	vec3 c = texture(screen_tex, uv).rgb;
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	vec3 d = mix(vec3(0.025, 0.012, 0.016), red.rgb, smoothstep(0.05, 0.42, l));
	d = mix(d, vec3(1.0, 0.97, 0.95), smoothstep(0.58, 0.92, l));
	vec3 o = mix(c, d, amount);
	COLOR = vec4(mix(o, vec3(1.0), flash), 1.0);
}" };

	/// <summary>一扇门：黑色门体 + 内沿白线 + 红带（红带画在门体后面，缩回去时藏到门体底下）。side = -1 左门、+1 右门。</summary>
	private sealed class Door
	{
		public Node2D Root = null!, RedHolder = null!;
		public ColorRect Line = null!;
	}

	/// <summary>斜条：中线 y=0 处横跨 a..b（a &lt; b），上下伸到 ±H。</summary>
	private static Vector2[] SlabPts(float a, float b) => new[]
	{
		new Vector2(a - Slant * H, -H), new Vector2(b - Slant * H, -H), new Vector2(b + Slant * H, H), new Vector2(a + Slant * H, H),
	};

	private static Door BuildDoor(Node2D stage, float side, float far)
	{
		Door d = new() { Root = new Node2D(), RedHolder = new Node2D() };
		stage.AddChild(d.Root);
		d.Root.AddChild(d.RedHolder);
		// 合上时：红带贴着中线（各越过中线 1 防接缝），门体从红带外侧（留 2 重叠）一直到屏外，白线压在门体上、离红带一道黑缝
		float s = side;
		P5Ui.Quad(d.RedHolder, Span(-s, s * (RedW + 2f)), Red);
		P5Ui.Quad(d.Root, Span(s * (RedW - 1f), s * far), Ink);
		d.Line = P5Ui.Quad(d.Root, Span(s * (RedW + Gap), s * (RedW + Gap + LineW)), White);
		return d;
	}

	/// <summary>两个横向位置之间的斜条（不管谁大谁小）。</summary>
	private static Vector2[] Span(float x0, float x1) => SlabPts(Mathf.Min(x0, x1), Mathf.Max(x0, x1));

	private sealed class Word
	{
		public Control Root = null!;
		public float At;
	}

	/// <summary>播出发过场。返回过场的根节点（onBlack 之后仍然全黑地留着，调用方接手黑屏后自己 QueueFree）。</summary>
	public static Control Out(Control host, Action? onBlack = null)
	{
		Vector2 size = host.Size.X > 2f ? host.Size : Fx.Screen(host);
		Control root = new() { Name = "P5Embark", Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
		host.AddChild(root);
		float k = size.Y / 1080f;
		float hw = size.X / k * 0.5f;
		float far = hw + 700f;

		ShaderMaterial strikeMat = new() { Shader = StrikeShader };
		strikeMat.SetShaderParameter("red", Red);
		ColorRect strike = new() { Size = size, Material = strikeMat, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(strike);
		ColorRect baseBlack = new() { Size = size, Color = Ink, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		root.AddChild(baseBlack);

		Node2D stage = new() { Position = size * 0.5f, Scale = Vector2.One * k };
		root.AddChild(stage);
		Door right = BuildDoor(stage, 1f, far), left = BuildDoor(stage, -1f, far);

		// 字：三个词一行，倾斜，白字 + 红色错位影子
		Node2D text = new() { Rotation = Mathf.DegToRad(-8f) };
		stage.AddChild(text);
		Word[] words = BuildWords(text, new[] { "TAKE", "YOUR", "HEART" }, new[] { 0.31f, 0.37f, 0.43f });

		ColorRect black = new() { Size = size, Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(black);

		void Pose(float t)
		{
			// 定格：双色调、推镜、一闪
			strike.Visible = t < 0.32f;
			strikeMat.SetShaderParameter("amount", Smooth(Seg(t, 0f, 0.08f)));
			strikeMat.SetShaderParameter("zoom", 1f + 0.035f * OutCubic(Seg(t, 0f, 0.12f)));
			strikeMat.SetShaderParameter("flash", 0.22f * (1f - Seg(t, 0f, 0.08f)));

			// 两扇门：右门 0.05 起、左门 0.14 起，各 0.15s 指数缓出
			float ro = Mathf.Lerp(hw + 380f, 0f, OutExpo(Seg(t, 0.05f, 0.20f)));
			float lo = Mathf.Lerp(-hw - 380f, 0f, OutExpo(Seg(t, 0.14f, 0.29f)));
			float shake = Impulse(t, 0.29f, 7f, 0.12f) + Impulse(t, 0.20f, 4f, 0.1f);
			stage.Position = size * 0.5f + new Vector2(Mathf.Sin(t * 230f), Mathf.Cos(t * 190f)) * shake * k;
			right.Root.Position = new Vector2(ro, 0f);
			left.Root.Position = new Vector2(lo, 0f);
			baseBlack.Visible = t >= 0.29f;                                  // 门合上以后底下垫黑（红带缩回去时露出来的是黑）

			// 收尾：红带缩到门体底下，白线淡掉
			float retract = (RedW + 6f) * InCubic(Seg(t, 0.64f, 0.80f));
			right.RedHolder.Position = new Vector2(retract, 0f);
			left.RedHolder.Position = new Vector2(-retract, 0f);
			Color lc = new(1f, 1f, 1f, 1f - Seg(t, 0.70f, 0.82f));
			right.Line.Modulate = lc;
			left.Line.Modulate = lc;

			PoseWords(words, text, t, 0.62f);
			black.Modulate = new Color(1f, 1f, 1f, Seg(t, 0.80f, 0.86f));
		}

		Run(root, Pose, OutTime, () => Stage.Safe("embark black", () => onBlack?.Invoke()));
		// 声音（P5R 原声，一拍一个）：门滑进来的菜单风声（峰值在样本 0.21s，对着右门到位）→ 左门合上的重击 + 低频（主冲击）→
		// 字砸上去的切入声 → 字甩走的短促「唰」
		Sfx.Play(Sfx.MenuWhoosh, 0.85f, 0f);
		Sfx.PlayHit(Sfx.Slam, 0.5f, 0.29f);
		Sfx.PlayHit(Sfx.SubBoom, 0.55f, 0.29f);
		Sfx.Play(Sfx.CutIn, 0.5f, 0.31f);
		Sfx.Play(Sfx.Swish, 0.45f, 0.62f);
		return root;
	}

	/// <summary>进局：一开始整屏全黑（接原版的黑屏），InTime 后自己删掉。</summary>
	public static Control In(Control host, Action? onDone = null)
	{
		Vector2 size = host.Size.X > 2f ? host.Size : Fx.Screen(host);
		Control root = new() { Name = "P5EmbarkIn", Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
		host.AddChild(root);
		float k = size.Y / 1080f;
		float hw = size.X / k * 0.5f;
		float far = hw + 700f;

		ColorRect baseBlack = new() { Size = size, Color = Ink, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(baseBlack);
		Node2D stage = new() { Position = size * 0.5f, Scale = Vector2.One * k };
		root.AddChild(stage);
		Door right = BuildDoor(stage, 1f, far), left = BuildDoor(stage, -1f, far);
		// 中线一闪的白线
		ColorRect cut = P5Ui.Quad(stage, SlabPts(-2f, 2f), White);

		void Pose(float t)
		{
			cut.Modulate = new Color(1f, 1f, 1f, Seg(t, 0f, 0.03f) * (1f - Seg(t, 0.06f, 0.14f)));
			// 红带从门体底下张开，白线跟着亮起来
			float retract = (RedW + 6f) * (1f - OutCubic(Seg(t, 0.04f, 0.14f)));
			right.RedHolder.Position = new Vector2(retract, 0f);
			left.RedHolder.Position = new Vector2(-retract, 0f);
			Color lc = new(1f, 1f, 1f, Seg(t, 0.04f, 0.12f));
			right.Line.Modulate = lc;
			left.Line.Modulate = lc;
			baseBlack.Visible = t < 0.14f;                                    // 门开始滑开之前撤掉底下的黑
			float ro = Mathf.Lerp(0f, hw + 380f, InCubic(Seg(t, 0.16f, 0.58f)));
			float lo = Mathf.Lerp(0f, -hw - 380f, InCubic(Seg(t, 0.14f, 0.56f)));
			right.Root.Position = new Vector2(ro, 0f);
			left.Root.Position = new Vector2(lo, 0f);
		}

		Run(root, Pose, InTime, () =>
		{
			if (GodotObject.IsInstanceValid(root)) root.QueueFree();
			Stage.Safe("embark in done", () => onDone?.Invoke());
		});
		// 中线白光的短促「唰」→ 两扇门滑开的风声
		Sfx.Play(Sfx.Swish, 0.45f, 0f);
		Sfx.Play(Sfx.SwishHeavy, 0.5f, 0.12f);
		return root;
	}

	// ───────── 字 ─────────

	private const int WordSize = 156;

	private static Word[] BuildWords(Node2D parent, string[] texts, float[] at)
	{
		Font font = P5Ui.Font(P5Ui.Barlow);
		float gap = WordSize * 0.32f;
		float total = -gap;
		float[] widths = new float[texts.Length];
		for (int i = 0; i < texts.Length; i++)
		{
			widths[i] = P5Ui.Width(font, texts[i], WordSize);
			total += widths[i] + gap;
		}
		Word[] words = new Word[texts.Length];
		float x = -total * 0.5f;
		for (int i = 0; i < texts.Length; i++)
		{
			Control w = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
			parent.AddChild(w);
			// 红色错位影子在后、白字在前（P5 菜单字的叠法）
			// 黑描边把字从红带上分出来；红影子同样描黑边，叠在一起是 P5 那种「白 - 黑 - 红」的层次
			P5Ui.Text(w, texts[i], font, WordSize, Red, new Vector2(9f, 9f), 12, Ink);
			Label face = P5Ui.Text(w, texts[i], font, WordSize, White, Vector2.Zero, 12, Ink);
			Vector2 sz = face.GetMinimumSize();
			w.Size = sz;
			w.Position = new Vector2(x, -sz.Y * 0.5f);
			w.PivotOffset = sz * 0.5f;
			x += widths[i] + gap;
			words[i] = new Word { Root = w, At = at[i] };
			w.Visible = false;
		}
		return words;
	}

	private static void PoseWords(Word[] words, Node2D line, float t, float exitAt)
	{
		// 甩出去：整行沿字的方向往左加速滑出画面（不淡出，半透明的字发灰）
		float exit = InCubic(Seg(t, exitAt, exitAt + 0.14f));
		line.Position = new Vector2(-1500f * exit, 0f);
		for (int i = 0; i < words.Length; i++)
		{
			Word w = words[i];
			float lt = t - w.At;
			w.Root.Visible = lt >= 0f && exit < 1f;
			if (!w.Root.Visible) continue;
			float p = Seg(lt, 0f, 0.1f);
			w.Root.Scale = Vector2.One * Mathf.Lerp(1.45f, 1f, OutBack(p, 1.3f));
			w.Root.Modulate = new Color(1f, 1f, 1f, Seg(lt, 0f, 0.03f));
		}
	}

	// ───────── 时钟 / 缓动 ─────────

	private static void Run(Control root, Action<float> pose, float time, Action done)
	{
		pose(0f);
		Tween tw = root.CreateTween();
		tw.TweenMethod(Callable.From<float>(t => { if (GodotObject.IsInstanceValid(root)) pose(t); }), 0f, time, time);
		tw.TweenCallback(Callable.From(() => { if (GodotObject.IsInstanceValid(root)) done(); }));
	}

	private static float Seg(float t, float a, float b) => Mathf.Clamp((t - a) / (b - a), 0f, 1f);
	private static float Smooth(float x) => x * x * (3f - 2f * x);
	private static float OutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
	private static float InCubic(float x) => x * x * x;
	private static float OutExpo(float x) => x >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * x);
	private static float OutBack(float x, float k) { float y = x - 1f; return 1f + (k + 1f) * y * y * y + k * y * y; }
	/// <summary>t0 时刻起的一次衰减冲击（amp → 0，dur 秒）。</summary>
	private static float Impulse(float t, float t0, float amp, float dur) => t < t0 || t > t0 + dur ? 0f : amp * (1f - (t - t0) / dur);
}
