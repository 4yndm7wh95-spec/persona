using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 撒旦耶尔的部件动画（华丽收场 / 大罪穿甲弹，用户 2026-11-08）。用 Codex 的第 6 版分层（art/p5/fx/v4/satanael/layers_v6 →
/// tools/p5/satanael/game_layers.py 裁成 sat_*.png，坐标都是 1080p 下的屏幕像素）。
/// · 构图（用户）：比屏幕大得多、显示不全才显得巨大——脸和眼睛推出屏幕上方，身体大半在屏幕左边外面，画面里只看得到翅膀、一部分身体和伸进来的持枪手臂；
/// · 登场（用户：不要直接显现）：天空裂开一道蓝白的缝 → 从下往上被「烧」出来（溶解，边缘蓝焰），翅膀同时展开；
/// · 抬枪：预备下沉 → 上臂先甩、前臂慢半拍 → 冲过头 → 阻尼回正 → 蓄力时越来越抖（转枪等 Codex 补空手 / 单独的枪）；
/// · 后坐：前臂绕手肘 0.06s 内上翻约 40°（枪口甩向头），上臂抬约 12°，身体晚一点后仰、翅膀被震开，慢慢放回；
/// · 光照（PersonaShader）：压低金色、冷蓝环境光、天空方向的青色边缘光；枪口蓄力光和开枪白闪照亮他；登场的溶解也在这个着色器里。
/// 各层平铺在同一个父节点下（保持在天空和平台之间的绘制顺序），层级关系的变换在代码里逐层相乘。
/// </summary>
public sealed class SatanaelPuppet
{
	private sealed record Layer(string Name, Vector2 Offset, Vector2 Pivot, string? Parent);

	// tools/p5/satanael/game_layers.py 的 game.json（K = 0.6），按绘制顺序（从后往前）
	private static readonly Layer[] Layers =
	{
		new("wing_back", new(684f, 369.6f), new(783.6f, 1160.4f), "body"),
		new("wing_front", new(0f, 66f), new(526.2f, 1125.6f), "body"),
		new("cape", new(0f, 681f), new(539.4f, 1144.8f), "body"),
		new("arm_upper", new(738.6f, 1142.4f), new(825f, 1227f), "body"),
		new("head", new(426f, 556.8f), new(696f, 1164f), "body"),
		new("body", new(180.6f, 1072.8f), new(686.4f, 1638f), null),
		new("arm_fore_gun", new(908.4f, 1168.2f), new(974.4f, 1306.2f), "arm_upper"),
		new("arm_near", new(150.6f, 1263f), new(330.6f, 1317f), "body"),
		new("eyes_glow", new(750f, 978f), new(772.8f, 997.8f), "head"),
	};
	private static readonly Vector2 MuzzlePx = new(1999.2f, 1302.6f);

	private static Shader? _shader, _rift;

	/// <summary>撒旦耶尔的材质：夜光调色 + 边缘光 + 点光 / 白闪 + 登场溶解（边缘蓝焰）。reveal_y 以下（屏幕像素）显现。</summary>
	private static Shader PersonaShader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 sres = vec2(1920.0, 1080.0);
uniform float reveal_y = -9999.0;
uniform float clock = 0.0;
uniform float alpha = 1.0;
uniform vec4 point_light = vec4(0.0, 0.0, 300.0, 0.0);
uniform float flash = 0.0;
uniform float dim = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	vec2 sp = SCREEN_UV * sres;
	float l = dot(c.rgb, vec3(0.3, 0.59, 0.11));
	// 夜光：压低饱和度（金色变成淡银金）、乘冷蓝环境光；越往下越暗（隐入深渊）
	vec3 g = mix(vec3(l), c.rgb, 0.4) * vec3(0.55, 0.66, 0.95);
	g *= mix(1.0, 0.45, smoothstep(sres.y * 0.55, sres.y * 1.05, sp.y));
	// 边缘光：天空（右上）方向那一侧的轮廓亮一道青色
	float a2 = texture(TEXTURE, UV + vec2(0.7, -0.7) * TEXTURE_PIXEL_SIZE * 7.0).a;
	float rim = clamp(c.a - a2, 0.0, 1.0);
	g += vec3(0.3, 0.72, 1.0) * rim * 0.85;
	// 点光（枪口蓄力）：近的地方被照亮，亮部被照得更多
	float d = length(sp - point_light.xy);
	g += vec3(0.45, 0.8, 1.0) * point_light.w * exp(-d / point_light.z) * (0.35 + l);
	g *= 1.0 - 0.4 * dim;
	g = mix(g, vec3(0.92, 0.97, 1.0), flash * 0.65);
	// 登场溶解：reveal_y 以下显现；分界是一圈往上舔的蓝焰（四档硬边，和面具蓝焰同一套）
	float n = (noise(sp * vec2(0.012, 0.006) + vec2(0.0, clock * 1.6)) - 0.5) * 160.0;
	float s = (sp.y + n) - reveal_y;
	float vis = smoothstep(-1.5, 1.5, s);
	float e = exp(-abs(s) / 28.0) * step(-70.0, s) * (0.7 + 0.6 * noise(sp * 0.03 + clock * 4.0));
	float band = step(0.18, e) * (1.0 - smoothstep(0.0, 40.0, s));
	vec3 flame = e > 0.75 ? vec3(0.92, 0.98, 1.0) : e > 0.45 ? vec3(0.35, 0.78, 1.0) : vec3(0.08, 0.32, 0.95);
	g = mix(g, flame, band);
	COLOR = vec4(g, c.a * alpha * max(vis, band));
}" };

	/// <summary>天空裂开的那道缝：一条竖着的锯齿亮线从上往下裂开，张开成蓝白的裂口，边上一圈蓝光。</summary>
	private static Shader RiftShader => _rift ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec2 sres = vec2(1920.0, 1080.0);
uniform float cx = 300.0;
uniform float open = 0.0;
uniform float grow = 0.0;
uniform float clock = 0.0;
float hash(float n) { return fract(sin(n) * 43758.5453); }
float noise1(float x) { float i = floor(x), f = fract(x); f = f * f * (3.0 - 2.0 * f); return mix(hash(i), hash(i + 1.0), f); }
void fragment() {
	vec2 p = UV * sres;
	float len = clamp(grow, 0.0, 1.0) * sres.y * 1.1;
	float jag = (noise1(p.y * 0.03) - 0.5) * 70.0 + (noise1(p.y * 0.12 + 7.0) - 0.5) * 24.0;
	float x = p.x - (cx + jag);
	float w = open * (14.0 + 90.0 * (0.5 + 0.5 * sin(p.y * 0.004 + 1.0)));
	float inside = step(p.y, len);
	float core = exp(-pow(x / max(w * 0.35, 1.0), 2.0));
	float glow = exp(-abs(x) / max(w * 1.6, 1.0)) * 0.6;
	float fl = noise1(p.y * 0.05 - clock * 9.0) * 0.6 + 0.4;
	vec3 col = vec3(0.92, 0.98, 1.0) * core * 1.5 + vec3(0.25, 0.65, 1.0) * glow * fl;
	COLOR = vec4(col * inside * open, 1.0);
}" };

	private readonly Dictionary<string, Sprite2D> _sprites = new();
	private readonly Dictionary<string, float> _angle = new();
	private readonly Node2D _parent;
	private readonly ShaderMaterial _mat, _riftMat;
	private readonly Vector2 _origin;
	private readonly float _scale, _aim, _riftX;
	private Transform2D _root = Transform2D.Identity, _muzzleXf = Transform2D.Identity;

	public static bool Available => Fx.Tex("sat_body") != null && Fx.Tex("sat_arm_fore_gun") != null;

	/// <summary>origin = 图层画布左上角在屏幕上的位置；scale = 额外缩放（1080p 下为 1）；target = 瞄准点（屏幕坐标）。</summary>
	public SatanaelPuppet(Node2D parent, Vector2 origin, float scale, Vector2 target)
	{
		_parent = parent;
		_origin = origin;
		_scale = scale;
		Vector2 screen = Fx.Screen(parent);
		_mat = new ShaderMaterial { Shader = PersonaShader };
		_mat.SetShaderParameter("sres", screen);
		_riftMat = new ShaderMaterial { Shader = RiftShader };
		_riftMat.SetShaderParameter("sres", screen);
		ColorRect rift = new() { Size = screen, Material = _riftMat, MouseFilter = Control.MouseFilterEnum.Ignore };
		parent.AddChild(rift);
		Fx.Clock(rift);
		foreach (Layer L in Layers)
		{
			Sprite2D sp = new() { Texture = Fx.Tex("sat_" + L.Name), Centered = false, Offset = L.Offset, Material = _mat };
			parent.AddChild(sp);
			_sprites[L.Name] = sp;
			_angle[L.Name] = 0f;
		}
		Fx.Clock(_sprites["body"]);
		Vector2 shoulder = Find("arm_upper").Pivot;
		Vector2 sS = origin + shoulder * scale;
		_aim = Mathf.Clamp(Mathf.RadToDeg((target - sS).Angle() - (MuzzlePx - shoulder).Angle()), -6f, 9f);   // 不往下压到敌人头上
		_riftX = Mathf.Max(origin.X + 760f * scale, 120f * scale);
	}

	private static Layer Find(string n) => Array.Find(Layers, l => l.Name == n)!;

	private static Transform2D Joint(Vector2 pivot, float deg) =>
		Transform2D.Identity.Translated(-pivot).Rotated(Mathf.DegToRad(deg)).Translated(pivot);

	private Transform2D World(string name)
	{
		Layer L = Find(name);
		Transform2D own = Joint(L.Pivot, _angle[name]);
		return L.Parent == null ? own : World(L.Parent) * own;
	}

	/// <summary>枪口在 parent 坐标（屏幕像素）里的位置。</summary>
	public Vector2 MuzzleLocal => _muzzleXf * MuzzlePx;

	/// <summary>枪口的全局位置（特效层坐标）。</summary>
	public Vector2 MuzzleGlobal => _parent.GetGlobalTransform() * MuzzleLocal;

	private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp(x, 0f, 1f), 3f);
	private static float EaseInOut(float x) { x = Mathf.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }

	/// <summary>登场：裂缝张开 RiftOpen 秒，然后 RevealTime 秒从下往上烧出来。</summary>
	public const float RiftOpen = 0.32f, RevealTime = 0.7f;

	/// <summary>按全局时刻 g 摆姿势、更新材质。appearAt：裂缝开始裂；raiseAt：开始抬枪；fireAt：开枪；exitAt：开始退场。</summary>
	public void Update(float g, float appearAt, float raiseAt, float fireAt, float exitAt, Vector2? chargeLight = null, float chargePower = 0f,
		float flash = 0f, float dim = 0f)
	{
		Vector2 screen = Fx.Screen(_parent);
		float a = g - appearAt;
		float rv = Mathf.Clamp((a - RiftOpen) / RevealTime, 0f, 1f);
		float revealY = Mathf.Lerp(screen.Y + 200f, -500f, EaseOutCubic(rv));
		if (g >= exitAt)
		{
			revealY = Mathf.Lerp(-500f, screen.Y + 300f, EaseInOut((g - exitAt) / 0.55f));      // 退场：从上往下烧回去
		}
		_mat.SetShaderParameter("reveal_y", a < RiftOpen ? screen.Y + 400f : revealY);
		_mat.SetShaderParameter("flash", flash);
		_mat.SetShaderParameter("dim", dim);
		_mat.SetShaderParameter("point_light", chargeLight is Vector2 cl ? new Vector4(cl.X, cl.Y, 260f * _scale, chargePower) : Vector4.Zero);
		float rift = a < 0f ? 0f : a < RiftOpen ? EaseOutCubic(a / RiftOpen) : Mathf.Max(0f, 1f - (a - RiftOpen) / (RevealTime * 0.8f));
		_riftMat.SetShaderParameter("open", rift);
		_riftMat.SetShaderParameter("cx", _riftX);
		_riftMat.SetShaderParameter("grow", Mathf.Clamp(a / RiftOpen, 0f, 1.5f));

		float rise = 60f * (1f - EaseOutCubic(rv)) + 5f * Mathf.Sin(g * 1.7f);
		if (g >= exitAt)
		{
			rise += 120f * EaseInOut((g - exitAt) / 0.55f);
		}
		float breathe = 0.006f * Mathf.Sin(g * 2.3f);
		// 翅膀：登场时从收拢展开，之后慢慢鼓动
		float unfold = EaseOutCubic((a - RiftOpen * 0.6f) / 0.6f);
		_angle["wing_back"] = Mathf.Lerp(14f, 0f, unfold) + 1.6f * Mathf.Sin(g * 1.9f);
		_angle["wing_front"] = Mathf.Lerp(-18f, 0f, unfold) - 2f * Mathf.Sin(g * 1.9f + 0.4f);
		_angle["cape"] = 1.2f * Mathf.Sin(g * 1.4f + 1f);
		_angle["arm_near"] = 2f * Mathf.Sin(g * 1.6f + 0.5f);
		_angle["head"] = 1f * Mathf.Sin(g * 1.1f);
		_angle["eyes_glow"] = 0f;

		float upper = 55f + 1.5f * Mathf.Sin(g * 2.1f), fore = 25f + 2f * Mathf.Sin(g * 2.1f - 0.6f);
		float r = g - raiseAt;
		if (r >= 0f)
		{
			const float ant = 0.12f, swing = 0.3f, lag = 0.06f;
			upper = r < ant ? Mathf.Lerp(55f, 61f, EaseInOut(r / ant))
				: r < ant + swing ? Mathf.Lerp(61f, -4f, EaseOutCubic((r - ant) / swing))
				: -4f * Mathf.Exp(-(r - ant - swing) / 0.09f) * Mathf.Cos((r - ant - swing) * 22f);
			float rf = r - lag;
			fore = rf < ant ? Mathf.Lerp(25f, 31f, EaseInOut(Mathf.Max(rf, 0f) / ant))
				: rf < ant + swing ? Mathf.Lerp(31f, -3f, EaseOutCubic((rf - ant) / swing))
				: -3f * Mathf.Exp(-(rf - ant - swing) / 0.1f) * Mathf.Cos((rf - ant - swing) * 20f);
			if (g < fireAt && r > ant + swing)
			{
				float k = Mathf.Clamp((g - (raiseAt + ant + swing)) / Mathf.Max(0.1f, fireAt - raiseAt - ant - swing), 0f, 1f);
				upper += (0.2f + 0.7f * k) * Mathf.Sin(g * 47f);
				fore += (0.2f + 0.9f * k) * Mathf.Sin(g * 61f + 1.3f);
			}
		}
		float lean = 0f;
		Vector2 push = Vector2.Zero;
		float f = g - fireAt;
		if (f >= 0f)
		{
			float kick = f < 0.06f ? EaseOutCubic(f / 0.06f)
				: f < 0.35f ? 1f + 0.06f * Mathf.Exp(-(f - 0.06f) / 0.08f) * Mathf.Sin((f - 0.06f) * 60f)
				: 1f - EaseOutCubic((f - 0.35f) / 0.6f) - 0.07f * Mathf.Sin(Mathf.Clamp((f - 0.35f) / 0.6f, 0f, 1f) * Mathf.Pi);
			upper += -12f * kick;
			fore += -40f * kick;
			float kb = f < 0.03f ? 0f : kick * Mathf.Clamp((f - 0.03f) / 0.08f, 0f, 1f);
			lean = -2.5f * kb;
			push = new Vector2(-18f, -6f) * kb;
			_angle["head"] += -3f * kb;
			_angle["wing_back"] += 4f * kb;
			_angle["wing_front"] += -5f * kb;
		}
		_angle["arm_upper"] = _aim + upper;
		_angle["arm_fore_gun"] = fore;
		_angle["body"] = lean;
		Vector2 bodyPivot = Find("body").Pivot;
		_root = Transform2D.Identity.Translated(-bodyPivot).Scaled(new Vector2(1f, 1f + breathe)).Translated(bodyPivot)
			.Scaled(Vector2.One * _scale).Translated(_origin + new Vector2(0f, rise) + push);
		foreach (Layer L in Layers)
		{
			_sprites[L.Name].Transform = _root * World(L.Name);
		}
		_muzzleXf = _root * World("arm_fore_gun");
	}
}
