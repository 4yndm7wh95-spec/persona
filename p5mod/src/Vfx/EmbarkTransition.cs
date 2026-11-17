using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 选人界面点「出发」之后的过场：替换原版静默猎手那段 0.8s 的黑色斜线淡出（docs/p5/CHARSELECT_PLAN.md 第 8 节）。只用 Godot，预览工具也能编译。
/// 用户 2026-11-17 定的方向：照 P5R 每天切日历那段 —— 一把飞刀「锵~」地飞进来钉住；换到这里是飞刀钉住预告信，然后画面展开。
/// 素材都是 P5R 原件：预告信 = 暂停菜单 P5_CARD02（校正掉一半透视），飞刀 / 刀身闪光 = 日替わり图集的 ナイフ / きらめき，
/// 声音 = 切日历的原版两条 cue（SYSTEM.AWB 流 37 calendar_change、流 82 stab_knife）。门和红边是代码画的直边色块。
/// Out（OutTime，之后这一帧停着当加载画面，Curtain.Open 时再展开）：
///   0       定格：画面平滑地变成红黑双色调 + 推镜 + 一闪
///   0.04    两扇门同时滑进来，0.18 合拢（「彭」）；门上是很暗的红色同心圆环（接着卡面的圆环往外扩）
///   0.12    预告信立体转着进来：绕竖轴从背面转大半圈到正面（带俯仰、真透视），由小变大，缓出落定
///   0.74    飞刀从上方飞进来钉在预告信右上角：刀尖截进纸里、入口撕开一道口子、卡一震、刀身闪光（插入闷响 + 锵~~）
/// Open（OpenTime）：飞刀被一下拔走 → 预告信立体翻转着缩小飞走 → 门缝白光一闪，两扇门往两边滑开。
/// host：盖满全屏、画在所有界面上面的 Control（游戏里是 NGame 的 NTransition；预览里是最上层 CanvasLayer 里的 Control）。
/// 所有元素都由一个主时钟 Pose(t) 摆出来。
/// </summary>
public static class EmbarkTransition
{
	public const float OutTime = 0.98f;
	public const float OpenTime = 0.86f;

	// ───────── 时间轴（秒） ─────────
	private const float DoorsIn = 0.04f, Close = 0.18f;          // 两扇门同时滑进来、合拢
	private const float CardIn = 0.12f, CardLand = 0.62f;        // 预告信转着进来、落定
	private const float Stab = 0.74f, Throw = Stab - 0.06f;      // 飞刀出手、钉住（P5R 原版只飞两三帧：一下就扎住）

	// ───────── 形状 / 摆放（1080 高的画面坐标，原点在画面中心） ─────────
	private const float Slant = 0.2f;                     // 斜线：x 每往下 1 往右 0.2（= 选人界面立绘的斜切线 CutA→CutB）
	private const float H = 620f;                         // 色块上下伸到屏外
	private const float RedW = 22f;                       // 每扇门内沿的红边宽（两扇拼起来 44，一道细红边）
	private const float Gap = 10f;                        // 红边和白线之间的黑缝
	private const float LineW = 3f;                       // 白线宽
	private static readonly Vector2 CardAt = new(0f, -15f);   // 稍微偏上（用户：上下空间不平衡）
	private const float CardH = 450f;                     // 预告信贴图显示高（含透明边；卡面约 420 高）
	private const float CardDeg = -4f;                    // 落定后略微歪一点
	private const float Focal = 1500f;                    // 立体转动的透视焦距（越小透视越强）
	private static readonly Vector2 PinAt = new(222f, -192f);   // 刀扎进纸面的入口：预告信右上角
	private const float Hidden = 45f;                           // 刀尖进了纸的那一截长
	private const float KnifeDeg = -57f;                  // 飞刀几乎从正上方扎下来（刀柄朝右上，同 P5R 切日历那把）
	private const float KnifeScale = 0.55f;               // 原图 858×468
	private static readonly Vector2 KnifeTip = new(8f, 236f);       // 刀尖在原图里的位置（刀身水平，刀尖朝左）
	private static readonly Vector2 GlintOnBlade = new(230f, 230f); // 闪光在刀身上的位置（原图像素）

	private static readonly Color Red = new(0.9f, 0.07f, 0.1f);
	private static readonly Color Ink = new(0.025f, 0.012f, 0.016f);
	private static readonly Color White = new(1f, 0.97f, 0.95f);

	private static Shader? _strike, _card, _cut, _ring;

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

	/// <summary>
	/// 立体的预告信：每个像素沿视线打到「绕竖轴转 yaw、绕横轴转 pitch」的卡面上（真透视，焦距 focal），取卡面贴图；
	/// 转到背面时画卡背（深色 + 同样的白边）。卡面贴图四周有透明边，转的时候边缘也是抗锯齿的。
	/// </summary>
	private static Shader CardShader => _card ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D card_tex : filter_linear;
uniform vec2 card_size = vec2(600.0, 450.0);
uniform vec2 rect_size = vec2(1200.0, 900.0);
uniform float yaw = 0.0;
uniform float pitch = 0.0;
uniform float focal = 1500.0;
uniform vec4 back_tint : source_color = vec4(0.16, 0.02, 0.03, 1.0);
varying vec2 lp;
void vertex() { lp = VERTEX - rect_size * 0.5; }
void fragment() {
	float cy = cos(yaw), sy = sin(yaw), cp = cos(pitch), sp = sin(pitch);
	vec3 ex = vec3(cy, 0.0, -sy);
	vec3 ey = vec3(sy * sp, cp, cy * sp);
	vec3 n = vec3(sy * cp, -sp, cy * cp);
	vec3 r = vec3(lp, focal);
	vec3 c0 = vec3(0.0, 0.0, focal);
	float dn = dot(n, r);
	float t = dot(n, c0) / (abs(dn) < 1e-5 ? 1e-5 : dn);
	vec3 h = r * t - c0;
	vec2 q = vec2(dot(h, ex), dot(h, ey));
	vec2 uv = q / card_size + 0.5;
	bool inside = t > 0.0 && uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0;
	vec4 c = inside ? texture(card_tex, uv) : vec4(0.0);
	if (dn < 0.0) {
		vec4 b = inside ? texture(card_tex, vec2(1.0 - uv.x, uv.y)) : vec4(0.0);
		float white = step(0.92, min(b.r, min(b.g, b.b)));
		c = vec4(mix(back_tint.rgb + b.rgb * 0.08, vec3(1.0, 0.97, 0.95), white), b.a);
	}
	COLOR = c * COLOR;
}" };

	/// <summary>飞刀：纹理 x &lt; cut_px 的部分（刀尖那截）不画，边缘抗锯齿 —— 刀尖「进了纸里」。</summary>
	private static Shader CutShader => _cut ??= new Shader { Code = @"
shader_type canvas_item;
uniform float tex_w = 858.0;
uniform float cut_px = -1.0;
void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float px = UV.x * tex_w;
	float w = max(fwidth(px), 1e-4);
	COLOR = vec4(c.rgb, c.a * clamp((px - cut_px) / w + 0.5, 0.0, 1.0));
}" };

	/// <summary>门上的暗红同心圆环（画面坐标），离圆心越远越淡，门缝两边留出红边和白线。</summary>
	private static Shader RingShader => _ring ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 rect_origin;
uniform vec2 ring_center;
uniform float clear_w = 60.0;
uniform float ring_slant = 0.2;
varying vec2 lp;
void vertex() { lp = VERTEX + rect_origin; }
void fragment() {
	float d = length(lp - ring_center);
	float px = max(fwidth(d), 1e-4);
	float b = mod(d - 330.0, 150.0);
	float band = clamp(b / px, 0.0, 1.0) * clamp((58.0 - b) / px, 0.0, 1.0);
	float fade = 1.0 - smoothstep(330.0, 900.0, d);
	float keep = clamp((abs(lp.x - ring_slant * lp.y) - clear_w) / max(fwidth(lp.x), 1e-4), 0.0, 1.0);
	COLOR = vec4(0.3, 0.025, 0.04, band * fade * keep * 0.4 * COLOR.a);
}" };

	/// <summary>一扇门：黑色门体 + 内沿白线 + 红边。</summary>
	private sealed class Door
	{
		public Node2D Root = null!;
	}

	/// <summary>斜条：中线 y=0 处横跨 a..b（a &lt; b），上下伸到 ±H。</summary>
	private static Vector2[] SlabPts(float a, float b) => new[]
	{
		new Vector2(a - Slant * H, -H), new Vector2(b - Slant * H, -H), new Vector2(b + Slant * H, H), new Vector2(a + Slant * H, H),
	};

	private static Vector2[] Span(float x0, float x1) => SlabPts(Mathf.Min(x0, x1), Mathf.Max(x0, x1));

	/// <summary>side = -1 左门、+1 右门。合上时红边贴着中线（各越过 1 防接缝），门体从红边外侧（留 2 重叠）伸到屏外，白线压在门体上、离红边一道黑缝。</summary>
	private static Door BuildDoor(Node2D stage, float side, float far)
	{
		Door d = new() { Root = new Node2D() };
		stage.AddChild(d.Root);
		P5Ui.Quad(d.Root, Span(-side, side * (RedW + 2f)), Red);
		P5Ui.Quad(d.Root, Span(side * (RedW - 1f), side * far), Ink);
		P5Ui.Quad(d.Root, Span(side * (RedW + Gap), side * (RedW + Gap + LineW)), White);
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
			// 预告信转出去不出声（用户 2026-11-17），只有两扇门展开的菜单风声
			Sfx.Play(Sfx.MenuWhoosh, 0.75f, 0.36f);
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

		// 门上的暗红同心圆环（用户：两边全黑太空，简单丰富一下）：圆心在预告信中心，门合拢后才有，开门前淡掉
		ShaderMaterial ringMat = new() { Shader = RingShader };
		Vector2 ringMin = new(-hw - 20f, -560f);
		ringMat.SetShaderParameter("rect_origin", ringMin);
		ringMat.SetShaderParameter("ring_center", CardAt);
		ringMat.SetShaderParameter("clear_w", RedW + Gap + LineW + 10f);
		ringMat.SetShaderParameter("ring_slant", Slant);
		ColorRect rings = new() { Position = ringMin, Size = new Vector2(2f * hw + 40f, 1120f), Material = ringMat, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		stage.AddChild(rings);

		// 预告信：立体卡（着色器做透视），挂在 cardNode 上（位置 / 歪的角度 / 整体缩放）
		Texture2D? cardTex = Fx.Tex("yokoku");
		Vector2 cardSize = cardTex != null ? cardTex.GetSize() * (CardH / cardTex.GetHeight()) : new Vector2(610f, 450f);
		Vector2 rectSize = cardSize * 1.7f;
		ShaderMaterial cardMat = new() { Shader = CardShader };
		cardMat.SetShaderParameter("card_tex", cardTex);
		cardMat.SetShaderParameter("card_size", cardSize);
		cardMat.SetShaderParameter("rect_size", rectSize);
		cardMat.SetShaderParameter("focal", Focal);
		Node2D cardNode = new() { Position = CardAt, Visible = false };
		stage.AddChild(cardNode);
		cardNode.AddChild(new ColorRect { Position = -rectSize * 0.5f, Size = rectSize, Material = cardMat, MouseFilter = Control.MouseFilterEnum.Ignore });

		// 飞刀 + 扎开的口子（照 P5R 原版：刀压在预告信上面，在扎进去的那一点把刀尖那截截掉；入口处纸面顺着刀的方向裂开一道白缝，旁边一片被顶得翻起来）
		Node2D pin = new();
		stage.AddChild(pin);
		Vector2 toHandle = Vector2.Right.Rotated(Mathf.DegToRad(KnifeDeg));   // 刀尖 → 刀柄的方向（飞来的方向）
		Node2D slit = new() { Position = PinAt, Rotation = Mathf.DegToRad(KnifeDeg), Visible = false };
		pin.AddChild(slit);
		float sl = Hidden * 1.05f;
		P5Ui.Quad(slit, new[] { new Vector2(6f, -26f), new Vector2(0f, 0f), new Vector2(-sl, 0.6f), new Vector2(-sl * 0.45f, -20f) }, new Color(1f, 0.93f, 0.92f, 0.42f));
		P5Ui.Quad(slit, new[] { new Vector2(2f, -3.2f), new Vector2(2f, 3.2f), new Vector2(-sl, 0.5f), new Vector2(-sl, -0.5f) }, White);
		Texture2D? knifeTex = Fx.Tex("day_knife");
		Sprite2D KnifeSprite()
		{
			ShaderMaterial m = new() { Shader = CutShader };
			m.SetShaderParameter("tex_w", knifeTex?.GetWidth() ?? 858f);
			m.SetShaderParameter("cut_px", -1f);
			return new Sprite2D { Texture = knifeTex, Centered = false, Offset = -KnifeTip, Material = m };
		}
		Node2D[] ghosts = new Node2D[2];
		for (int i = 0; i < ghosts.Length; i++)
		{
			ghosts[i] = new Node2D { Rotation = Mathf.DegToRad(KnifeDeg), Scale = Vector2.One * KnifeScale, Visible = false };
			ghosts[i].AddChild(KnifeSprite());
			pin.AddChild(ghosts[i]);
		}
		Node2D knife = new() { Rotation = Mathf.DegToRad(KnifeDeg), Scale = Vector2.One * KnifeScale, Visible = false };
		pin.AddChild(knife);
		Sprite2D blade = KnifeSprite();
		knife.AddChild(blade);
		Sprite2D glint = new() { Texture = Fx.Tex("day_glint"), Position = GlintOnBlade - KnifeTip, Material = Fx.Additive, Visible = false };
		knife.AddChild(glint);
		Vector2 tipAt = PinAt - toHandle * Hidden;                          // 钉住时刀尖的位置（在纸里）

		ColorRect flash = new() { Size = size, Color = White, MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1f, 1f, 1f, 0f) };
		root.AddChild(flash);

		Vector2 KnifePos(float t) => tipAt + toHandle * 1300f * (1f - Seg(t, Throw, Stab));   // 匀速飞来、钉住就停

		void PoseCard(float yaw, float pitch, float scale, float deg, Vector2 at, float alpha)
		{
			cardMat.SetShaderParameter("yaw", yaw);
			cardMat.SetShaderParameter("pitch", pitch);
			cardNode.Scale = Vector2.One * scale;
			cardNode.Rotation = Mathf.DegToRad(deg);
			cardNode.Position = at;
			cardNode.Modulate = new Color(1f, 1f, 1f, alpha);
		}

		void Pose(float t)
		{
			// 定格：双色调、推镜、一闪
			strike.Visible = t < Close + 0.02f;
			strikeMat.SetShaderParameter("amount", Smooth(Seg(t, 0f, 0.08f)));
			strikeMat.SetShaderParameter("zoom", 1f + 0.035f * OutCubic(Seg(t, 0f, 0.12f)));
			strikeMat.SetShaderParameter("flash", 0.22f * (1f - Seg(t, 0f, 0.08f)));

			// 两扇门同时滑进来（指数缓出），Close 合拢；合拢以后底下垫黑，圆环淡入
			float door = OutExpo(Seg(t, DoorsIn, Close));
			right.Root.Position = new Vector2(Mathf.Lerp(hw + 400f, 0f, door), 0f);
			left.Root.Position = new Vector2(Mathf.Lerp(-hw - 400f, 0f, door), 0f);
			baseBlack.Visible = t >= Close;
			rings.Visible = t >= Close;
			rings.Modulate = new Color(1f, 1f, 1f, Smooth(Seg(t, Close, Close + 0.25f)));

			// 预告信立体转进来：绕竖轴从 -200° 转到 0（背面 → 侧面 → 正面），俯仰 18° → 0，0.45 → 1 倍，
			// 一条五次方缓出（开头快、落定前很慢，没有回弹：丝滑），歪的角度跟着转慢慢摆正到 -4°
			float u = Seg(t, CardIn, CardLand);
			float e = 1f - Mathf.Pow(1f - u, 5f);
			float stabDt = t - Stab;
			float jolt = stabDt >= 0f ? Mathf.Exp(-stabDt / 0.06f) : 0f;
			float bump = 1f + 0.05f * jolt;                                       // 扎中那一下整张卡一震
			cardNode.Visible = t >= CardIn;
			PoseCard(Mathf.DegToRad(-200f) * (1f - e), Mathf.DegToRad(18f) * (1f - e), Mathf.Lerp(0.45f, 1f, e) * bump,
				Mathf.Lerp(-16f, CardDeg, e) - 1f * jolt, CardAt - toHandle * 6f * jolt, Smooth(Seg(u, 0f, 0.12f)));

			// 口子：扎中的一瞬间顺着刀的方向撕开（0.04s 长到全长），跟着卡一起震
			slit.Visible = t >= Stab;
			slit.Scale = new Vector2(OutCubic(Seg(t, Stab, Stab + 0.04f)), 1f) * bump;
			slit.Position = PinAt - toHandle * 6f * jolt;
			// 刀尖进了纸：扎中以后把入口之前的那截截掉（纹理像素坐标，刀尖在 x = KnifeTip.x）
			if (blade.Material is ShaderMaterial bm) bm.SetShaderParameter("cut_px", t >= Stab ? KnifeTip.X + Hidden / KnifeScale : -1f);

			// 飞刀：飞行 → 钉住后只沿刀身轻轻回弹一下
			knife.Visible = t >= Throw;
			float wob = stabDt >= 0f ? Mathf.Sin(stabDt * Mathf.Tau * 12f) * Mathf.Exp(-stabDt / 0.04f) : 0f;
			knife.Position = KnifePos(t) + toHandle * 4f * wob;
			knife.Rotation = Mathf.DegToRad(KnifeDeg + 1f * wob);
			for (int i = 0; i < ghosts.Length; i++)
			{
				float gt = t - 0.012f * (i + 1);
				ghosts[i].Visible = t >= Throw && t < Stab && gt >= Throw;
				ghosts[i].Position = KnifePos(gt);
				ghosts[i].Modulate = new Color(1f, 1f, 1f, 0.35f / (i + 1));
			}
			// 刀身闪光：钉住后 0.02s 起亮，张开再收掉，边亮边转
			float g = Seg(t, Stab + 0.02f, Stab + 0.22f);
			glint.Visible = g > 0f && g < 1f;
			glint.Scale = Vector2.One * (1.6f * (g < 0.2f ? OutCubic(g / 0.2f) : 1f - Smooth((g - 0.2f) / 0.8f)));
			glint.Rotation = 0.5f * g;

			// 震动：合拢最重，飞刀钉住轻一点
			float shake = Impulse(t, Close, 9f, 0.13f) + Impulse(t, Stab, 6f, 0.1f);
			stage.Position = size * 0.5f + new Vector2(Mathf.Sin(t * 230f), Mathf.Cos(t * 190f)) * shake * k;
			flash.Modulate = new Color(1f, 1f, 1f, t >= Close ? 0.3f * (1f - Seg(t, Close, Close + 0.06f)) : 0f);
		}

		// 展开：飞刀被一下拔走 → 预告信立体翻转着缩小、往左上飞走 → 门缝白光一闪，两扇门往两边加速滑开
		ColorRect slash = P5Ui.Quad(stage, SlabPts(-3f, 3f), White);
		slash.Visible = false;
		void Open(float t)
		{
			// 拔刀：沿刀身往回加速抽走，刀尖露出来；口子跟着淡掉
			float pull = Seg(t, 0f, 0.14f);
			if (blade.Material is ShaderMaterial bm) bm.SetShaderParameter("cut_px", t > 0.01f ? -1f : KnifeTip.X + Hidden / KnifeScale);
			knife.Position = tipAt + toHandle * 1500f * pull * pull * pull;
			knife.Visible = pull < 1f;
			slit.Modulate = new Color(1f, 1f, 1f, 1f - Seg(t, 0.04f, 0.16f));

			// 预告信飞走：绕竖轴继续转到 +230°，往后仰，缩到 0.35 倍，往左上飘、最后淡出（缓入：先慢后快）
			float c = Seg(t, 0.1f, 0.48f);
			float ce = c * c * (3f - 2f * c) * 0.35f + c * c * c * 0.65f;
			PoseCard(Mathf.DegToRad(230f) * ce, Mathf.DegToRad(-24f) * ce, Mathf.Lerp(1f, 0.35f, ce), CardDeg + 10f * ce,
				CardAt + new Vector2(-260f, -340f) * ce, 1f - Smooth(Seg(c, 0.7f, 1f)));
			cardNode.Visible = c < 1f;

			// 门：白光一闪，圆环淡掉，两扇门滑开
			slash.Visible = t > 0.34f && t < 0.5f;
			slash.Modulate = new Color(1f, 1f, 1f, Seg(t, 0.34f, 0.36f) * (1f - Seg(t, 0.4f, 0.5f)));
			rings.Modulate = new Color(1f, 1f, 1f, 1f - Seg(t, 0.36f, 0.46f));
			baseBlack.Visible = t < 0.38f;
			float ro = Mathf.Lerp(0f, hw + 1000f, InCubic(Seg(t, 0.38f, 0.84f)));
			right.Root.Position = new Vector2(ro, 0f);
			left.Root.Position = new Vector2(-ro, 0f);
		}

		Run(root, Pose, OutTime, () => Stage.Safe("embark covered", () => onCovered?.Invoke()));
		// 声音：P5R 切日历的原版两声 —— 合拢 = 日期砸下来的「彭」，钉住 = 飞刀的插入闷响 + 「锵~~」（同一条 cue）
		Sfx.PlayHit(Sfx.DoorSlam, 0.8f, Close);
		Sfx.PlayHit(Sfx.KnifeStab, 0.9f, Stab);
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
