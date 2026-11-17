using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 选人界面点「出发」之后的过场：替换原版静默猎手那段 0.8s 的黑色斜线淡出（docs/p5/CHARSELECT_PLAN.md 第 8 节）。只用 Godot，预览工具也能编译。
/// 用户 2026-11-17 定的方向：照 P5R 每天切日历那段 —— 切到下一天，一把飞刀「锵~」地飞进来钉住；换到这里是飞刀钉住预告信，然后画面展开。
/// 尽量直接用 P5R 的素材：预告信（予告状）、飞刀（ナイフ）、刀上的闪光（きらめき）都是 P5R 日替わり图集里的原图（HIGAWARI_COMMON.SPD #193 / #98 / #56）；
/// 门和红带是代码画的直边色块（斜度同选人界面立绘的斜切线：x 每往下 1 往右 0.2，内沿 白线 + 黑缝 + 红带）。
/// Out（OutTime，之后这一帧就停着当加载画面，Curtain.Open 时再展开）：
///   0       定格：画面平滑地变成红黑双色调 + 推镜 + 一闪
///   0.04    两扇门同时滑进来，0.18 合拢 —— 同一帧预告信砸出来（大一号出现、0.05s 收到原大，屏幕一闪一震）
///   0.30    飞刀从右上飞进来，0.40 钉住预告信：锵~，刀身闪光、预告信一震、刀尾颤几下
/// Open（OpenTime）：沿门缝一道白光 → 预告信顺着门缝一分为二，两半跟着两扇门往两边展开（飞刀跟右半边走），露出进局的画面。
/// host：盖满全屏、画在所有界面上面的 Control（游戏里是 NGame 的 NTransition；预览里是最上层 CanvasLayer 里的 Control）。
/// 所有元素都由一个主时钟 Pose(t) 摆出来。
/// </summary>
public static class EmbarkTransition
{
	public const float OutTime = 0.62f;
	public const float OpenTime = 0.56f;

	// ───────── 时间轴（秒） ─────────
	private const float DoorsIn = 0.04f, Close = 0.18f;   // 两扇门同时滑进来、合拢（预告信同一帧砸出来）
	private const float Throw = 0.30f, Stab = 0.40f;      // 飞刀出手、钉住

	// ───────── 形状 / 摆放（1080 高的画面坐标，原点在画面中心） ─────────
	private const float Slant = 0.2f;                     // 斜线：x 每往下 1 往右 0.2（= 选人界面立绘的斜切线 CutA→CutB）
	private const float H = 620f;                         // 色块上下伸到屏外
	private const float RedW = 180f;                      // 每扇门内沿的红带宽（两扇拼起来 360）
	private const float Gap = 14f;                        // 红带和白线之间的黑缝
	private const float LineW = 4f;                       // 白线宽
	private static readonly Vector2 CardAt = new(0f, -10f);
	private const float CardH = 640f;                     // 预告信显示高（原图 552×528，卡面本身画成斜的）
	private static readonly Vector2 PinAt = new(170f, -175f);   // 刀尖钉进去的位置：预告信右上角那片红黑圈上（不挡标志和字）
	private const float KnifeDeg = -20f;                  // 飞刀从右上斜着扎下来（刀尖朝左下）
	private const float KnifeScale = 0.62f;               // 原图 858×468
	private static readonly Vector2 KnifeTip = new(8f, 236f);   // 刀尖在原图里的位置（刀身是水平的，刀尖朝左）
	private static readonly Vector2 GlintOnBlade = new(230f, 230f); // 闪光在刀身上的位置（原图像素）

	private static readonly Color Red = new(0.9f, 0.07f, 0.1f);
	private static readonly Color Ink = new(0.025f, 0.012f, 0.016f);
	private static readonly Color White = new(1f, 0.97f, 0.95f);

	private static Shader? _strike, _half;

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

	/// <summary>预告信的半边：只画门缝（画布坐标里的一条线，跟着自己那扇门走）的一侧，边缘抗锯齿。</summary>
	private static Shader HalfShader => _half ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 seam_p;
uniform vec2 seam_d = vec2(0.0, 1.0);
uniform float side = 1.0;
varying vec2 wp;
void vertex() { wp = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy; }
void fragment() {
	float s = -side * (seam_d.x * (wp.y - seam_p.y) - seam_d.y * (wp.x - seam_p.x));
	float px = max(length(fwidth(wp)) * 0.7071, 1e-4);
	vec4 c = texture(TEXTURE, UV) * COLOR;
	COLOR = vec4(c.rgb, c.a * clamp(s / px + 0.5, 0.0, 1.0));
}" };

	/// <summary>一扇门：黑色门体 + 内沿白线 + 红带。</summary>
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

	private static Vector2[] Span(float x0, float x1) => SlabPts(Mathf.Min(x0, x1), Mathf.Max(x0, x1));

	/// <summary>side = -1 左门、+1 右门。合上时红带贴着中线（各越过 1 防接缝），门体从红带外侧（留 2 重叠）伸到屏外，白线压在门体上、离红带一道黑缝。</summary>
	private static Door BuildDoor(Node2D stage, float side, float far)
	{
		Door d = new() { Root = new Node2D(), RedHolder = new Node2D() };
		stage.AddChild(d.Root);
		d.Root.AddChild(d.RedHolder);
		P5Ui.Quad(d.RedHolder, Span(-side, side * (RedW + 2f)), Red);
		P5Ui.Quad(d.Root, Span(side * (RedW - 1f), side * far), Ink);
		d.Line = P5Ui.Quad(d.Root, Span(side * (RedW + Gap), side * (RedW + Gap + LineW)), White);
		return d;
	}

	/// <summary>合上的幕：Out 播完以后一直停在钉住预告信的那一帧，Open 时展开并删掉自己。</summary>
	public sealed class Curtain
	{
		public Control Root { get; }
		private readonly Action<float> _open;
		private bool _opening;

		internal Curtain(Control root, Action<float> open)
		{
			Root = root;
			_open = open;
		}

		public bool Alive => GodotObject.IsInstanceValid(Root) && !Root.IsQueuedForDeletion();

		public void Open(Action? onDone = null)
		{
			if (_opening || !Alive)
			{
				return;
			}
			_opening = true;
			Run(Root, _open, OpenTime, () =>
			{
				if (GodotObject.IsInstanceValid(Root)) Root.QueueFree();
				Stage.Safe("embark open done", () => onDone?.Invoke());
			});
			// 沿门缝那道白光的短促「唰」→ 两扇门展开的菜单风声
			Sfx.Play(Sfx.Swish, 0.45f, 0f);
			Sfx.Play(Sfx.MenuWhoosh, 0.8f, 0.02f);
		}
	}

	/// <summary>播出发过场。onCovered：画面已经完全被盖住（OutTime），之后可以在底下换场景；幕一直留着，直到调用 Open。</summary>
	public static Curtain Out(Control host, Action? onCovered = null)
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

		// 预告信：合拢时整张砸出来；展开时换成两个半边（各挂在自己那扇门上，按门缝裁开）
		Texture2D? cardTex = Fx.Tex("yokoku");
		float cardScale = cardTex != null ? CardH / cardTex.GetHeight() : 1f;
		Sprite2D card = new() { Texture = cardTex, Position = CardAt, Visible = false };
		stage.AddChild(card);
		Sprite2D HalfOf(Door d, float side)
		{
			ShaderMaterial m = new() { Shader = HalfShader };
			m.SetShaderParameter("side", side);
			Sprite2D h = new() { Texture = cardTex, Material = m, Position = CardAt, Scale = Vector2.One * cardScale, Visible = false };
			d.Root.AddChild(h);
			return h;
		}
		Sprite2D halfL = HalfOf(left, -1f), halfR = HalfOf(right, 1f);

		// 飞刀：原点在刀尖（Offset 把刀尖挪到原点），画在预告信上面（展开时跟着右门走）；飞行时后面拖两层残影
		Texture2D? knifeTex = Fx.Tex("day_knife");
		Node2D knife = new() { Rotation = Mathf.DegToRad(KnifeDeg), Scale = Vector2.One * KnifeScale, Visible = false };
		stage.AddChild(knife);
		knife.AddChild(new Sprite2D { Texture = knifeTex, Centered = false, Offset = -KnifeTip });
		Sprite2D glint = new() { Texture = Fx.Tex("day_glint"), Position = GlintOnBlade - KnifeTip, Material = Fx.Additive, Visible = false };
		knife.AddChild(glint);
		Sprite2D[] ghosts = new Sprite2D[2];
		for (int i = 0; i < ghosts.Length; i++)
		{
			ghosts[i] = new Sprite2D { Texture = knifeTex, Centered = false, Offset = -KnifeTip, Rotation = knife.Rotation, Scale = knife.Scale, Visible = false };
			stage.AddChild(ghosts[i]);
			stage.MoveChild(ghosts[i], knife.GetIndex());                   // 残影在刀后面
		}
		Vector2 toHandle = Vector2.Right.Rotated(knife.Rotation);   // 刀尖 → 刀柄的方向（飞来的方向）

		ColorRect flash = new() { Size = size, Color = White, MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0f) };
		root.AddChild(flash);

		Vector2 KnifePos(float t)
		{
			// 匀速飞来、钉住就停（不减速：扔出去的刀是一下扎住的）
			float u = Seg(t, Throw, Stab);
			return PinAt + toHandle * 1400f * (1f - u);
		}

		void Pose(float t)
		{
			// 定格：双色调、推镜、一闪
			strike.Visible = t < Close + 0.02f;
			strikeMat.SetShaderParameter("amount", Smooth(Seg(t, 0f, 0.08f)));
			strikeMat.SetShaderParameter("zoom", 1f + 0.035f * OutCubic(Seg(t, 0f, 0.12f)));
			strikeMat.SetShaderParameter("flash", 0.22f * (1f - Seg(t, 0f, 0.08f)));

			// 两扇门同时滑进来（指数缓出），Close 合拢；合拢以后底下垫黑
			float door = OutExpo(Seg(t, DoorsIn, Close));
			right.Root.Position = new Vector2(Mathf.Lerp(hw + 400f, 0f, door), 0f);
			left.Root.Position = new Vector2(Mathf.Lerp(-hw - 400f, 0f, door), 0f);
			baseBlack.Visible = t >= Close;

			// 预告信：合拢那一帧大一号砸出来，0.05s 收到原大；飞刀扎进来时被推一下
			card.Visible = t >= Close;
			float stabDt = t - Stab;
			float jolt = stabDt >= 0f ? Mathf.Exp(-stabDt / 0.06f) : 0f;
			card.Scale = Vector2.One * cardScale * Mathf.Lerp(1.3f, 1f, OutExpo(Seg(t, Close, Close + 0.05f)));
			card.Position = CardAt - toHandle * 9f * jolt;
			card.Rotation = Mathf.DegToRad(-1.2f) * jolt;

			// 飞刀：飞行 → 钉住后沿刀身往回弹一点、刀尾颤几下（衰减的正弦）
			knife.Visible = t >= Throw;
			float wob = stabDt >= 0f ? Mathf.Sin(stabDt * Mathf.Tau * 16f) * Mathf.Exp(-stabDt / 0.09f) : 0f;
			knife.Position = KnifePos(t) + toHandle * 5f * wob;
			knife.Rotation = Mathf.DegToRad(KnifeDeg + 2.5f * wob);
			for (int i = 0; i < ghosts.Length; i++)
			{
				float gt = t - 0.018f * (i + 1);
				ghosts[i].Visible = t >= Throw && t < Stab && gt >= Throw;
				ghosts[i].Position = KnifePos(gt);
				ghosts[i].Modulate = new Color(1f, 1f, 1f, 0.35f / (i + 1));
			}
			// 刀身闪光：钉住后 0.02s 起亮，0.04s 张开、0.16s 收掉，边亮边转
			float g = Seg(t, Stab + 0.02f, Stab + 0.22f);
			glint.Visible = g > 0f && g < 1f;
			glint.Scale = Vector2.One * (1.6f * (g < 0.2f ? OutCubic(g / 0.2f) : 1f - Smooth((g - 0.2f) / 0.8f)));
			glint.Rotation = 0.5f * g;

			// 震动：合拢 + 预告信砸出来最重，飞刀钉住轻一点
			float shake = Impulse(t, Close, 9f, 0.13f) + Impulse(t, Stab, 6f, 0.1f);
			stage.Position = size * 0.5f + new Vector2(Mathf.Sin(t * 230f), Mathf.Cos(t * 190f)) * shake * k;
			float fl = (t >= Close ? 0.45f * (1f - Seg(t, Close, Close + 0.06f)) : 0f) + (t >= Stab ? 0.14f * (1f - Seg(t, Stab, Stab + 0.05f)) : 0f);
			flash.Modulate = new Color(1f, 1f, 1f, fl);
		}

		// 展开：沿门缝的白光一闪 → 预告信换成两个半边，两扇门（带着各自的半边、右边带着飞刀）往两边加速滑开
		ColorRect slash = P5Ui.Quad(stage, SlabPts(-3f, 3f), White);
		slash.Visible = false;
		void Open(float t)
		{
			slash.Visible = t < 0.14f;
			slash.Modulate = new Color(1f, 1f, 1f, Seg(t, 0f, 0.02f) * (1f - Seg(t, 0.05f, 0.14f)));
			bool split = t >= 0.04f;
			card.Visible = !split;
			halfL.Visible = halfR.Visible = split;
			foreach (Sprite2D h in new[] { halfL, halfR })
			{
				h.Position = card.Position;
				h.Rotation = card.Rotation;
				h.Scale = card.Scale;
			}
			baseBlack.Visible = !split;
			float open = InCubic(Seg(t, 0.04f, 0.5f));
			float ro = Mathf.Lerp(0f, hw + 1000f, open);
			right.Root.Position = new Vector2(ro, 0f);
			left.Root.Position = new Vector2(-ro, 0f);
			knife.Position = PinAt + new Vector2(ro, 0f);
			// 半边按各自那扇门上的门缝裁（门缝在门的本地坐标里是 x = 0.2y 这条线）
			foreach ((Sprite2D h, Door d) in new[] { (halfL, left), (halfR, right) })
			{
				if (h.Material is ShaderMaterial m)
				{
					Transform2D g2 = d.Root.GetGlobalTransform();
					m.SetShaderParameter("seam_p", g2 * Vector2.Zero);
					m.SetShaderParameter("seam_d", g2.BasisXform(new Vector2(Slant, 1f)).Normalized());
				}
			}
		}

		Run(root, Pose, OutTime, () => Stage.Safe("embark covered", () => onCovered?.Invoke()));
		// 声音（P5R 原声）：两扇门滑进来的菜单风声（峰值在样本 0.21s，对着合拢）→ 合拢 + 预告信砸出来的重击 + 低频 →
		// 飞刀出手的「唰」→ 钉住的清亮「锵~」+ 一记轻的低频
		Sfx.Play(Sfx.MenuWhoosh, 0.85f, 0f);
		Sfx.PlayHit(Sfx.Slam, 0.55f, Close);
		Sfx.PlayHit(Sfx.SubBoom, 0.6f, Close);
		Sfx.Play(Sfx.Swish, 0.45f, Throw);
		Sfx.Play(Sfx.KnifeChing, 0.7f, Stab - 0.02f);
		Sfx.PlayHit(Sfx.SubBoom, 0.3f, Stab);
		return new Curtain(root, Open);
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
	/// <summary>t0 时刻起的一次衰减冲击（amp → 0，dur 秒）。</summary>
	private static float Impulse(float t, float t0, float amp, float dur) => t < t0 || t > t0 + dur ? 0f : amp * (1f - (t - t0) / dur);
}
