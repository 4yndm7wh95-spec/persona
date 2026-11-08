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
		// 转枪（Codex 补的）：空手 + 单独的枪（以扳机护圈为轴套在食指上转）+ 食指尖（画在枪上面，看起来手指穿过护圈）
		new("arm_fore_open", new(908.4f, 1231.2f), new(974.4f, 1306.2f), "arm_upper"),
		new("gun", new(1185.6f, 1168.2f), new(1332f, 1320.6f), "arm_fore_open"),
		new("finger_tip", new(1320.6f, 1301.4f), new(1332f, 1320.6f), "arm_fore_open"),
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

	/// <summary>
	/// 召唤 / 退场的闪雷（用户 2026-11-08：雷的轮廓要更尖锐、闪得更快）：竖着劈下来的一道折线（每 60px 一个随机折点，直线连接 = 尖角），
	/// 上面再叠一层每 18px 的细碎折线，外加两条往斜下方岔出去的分叉；白芯 + 青色辉光。强度 strike 由代码按「劈下 → 闪几下 → 灭」给。
	/// </summary>
	private static Shader RiftShader => _rift ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec2 sres = vec2(1920.0, 1080.0);
uniform float cx = 300.0;
uniform float strike = 0.0;
uniform float seed = 0.0;
float h(float n) { return fract(sin(n * 91.345 + seed * 13.7) * 43758.5453); }
// 折线：每 seg 像素一个折点，折点之间直线连接
float zig(float y, float seg, float amp, float salt) {
	float k = y / seg, i = floor(k), f = k - i;
	return mix(h(i + salt) - 0.5, h(i + 1.0 + salt) - 0.5, f) * 2.0 * amp;
}
float zigSlope(float y, float seg, float amp, float salt) {
	float i = floor(y / seg);
	return (h(i + 1.0 + salt) - h(i + salt)) * 2.0 * amp / seg;
}
float bolt(vec2 p, float x0, float y0, float y1, float lean, float salt, float width) {
	if (p.y < y0 || p.y > y1) return 0.0;
	float y = p.y - y0;
	float path = x0 + lean * y + zig(y, 60.0, 46.0, salt) + zig(y, 18.0, 9.0, salt + 50.0);
	float slope = lean + zigSlope(y, 60.0, 46.0, salt) + zigSlope(y, 18.0, 9.0, salt + 50.0);
	float d = abs(p.x - path) / sqrt(1.0 + slope * slope);
	float taper = 1.0 - smoothstep(0.75, 1.0, y / max(y1 - y0, 1.0));
	return (exp(-pow(d / width, 2.0)) * 1.6 + exp(-d / (width * 9.0)) * 0.45) * taper;
}
void fragment() {
	vec2 p = UV * sres;
	float b = bolt(p, cx, -20.0, sres.y * 1.05, 0.05, 0.0, 2.6);
	// 两条分叉：从主干上一个随机高度往斜下方岔出去
	float by1 = sres.y * (0.25 + 0.2 * h(3.0)), by2 = sres.y * (0.5 + 0.2 * h(7.0));
	float bx1 = cx + 0.05 * by1 + zig(by1, 60.0, 46.0, 0.0), bx2 = cx + 0.05 * by2 + zig(by2, 60.0, 46.0, 0.0);
	b = max(b, 0.7 * bolt(p, bx1, by1, by1 + sres.y * 0.22, 0.7, 20.0, 1.8));
	b = max(b, 0.6 * bolt(p, bx2, by2, by2 + sres.y * 0.18, -0.6, 40.0, 1.6));
	vec3 col = vec3(0.95, 0.99, 1.0) * min(b, 1.6) + vec3(0.25, 0.65, 1.0) * b * 0.6;
	COLOR = vec4(col * strike, 1.0);
}" };

	/// <summary>闪雷的强度曲线（从劈下算起，秒）：0.03s 劈满 → 闪几下 → 0.3s 灭。</summary>
	public static float Strike(float t)
	{
		if (t < 0f || t > 0.32f) return 0f;
		if (t < 0.03f) return t / 0.03f;
		float[] beats = { 1f, 0.25f, 1f, 0.15f, 0.8f, 0.4f, 0.55f, 0.2f };
		int k = Mathf.Min(beats.Length - 1, (int)((t - 0.03f) / 0.035f));
		return beats[k] * (1f - Mathf.Clamp((t - 0.22f) / 0.1f, 0f, 1f));
	}

	private readonly Dictionary<string, Sprite2D> _sprites = new();
	private readonly Dictionary<string, float> _angle = new();
	private readonly Node2D _parent;
	private readonly ShaderMaterial _mat, _riftMat;
	private readonly Vector2 _origin;
	private readonly float _scale, _aim, _riftX;
	private float _spin = -1f;
	private Transform2D _root = Transform2D.Identity, _muzzleXf = Transform2D.Identity;

	public static bool Available => Fx.Tex("sat_body") != null && Fx.Tex("sat_arm_fore_gun") != null;

	private bool Twirls => _sprites.TryGetValue("gun", out Sprite2D? g) && g.Texture != null && _sprites["arm_fore_open"].Texture != null;

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
		_riftX = Mathf.Max(origin.X + 820f * scale, 160f * scale);
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

	/// <summary>登场：闪雷劈下 0.05s 后开始从下往上烧出来（RevealTime 秒）。退场：闪雷劈下 0.04s 后 0.2s 从上往下烧没。</summary>
	public const float RevealDelay = 0.05f, RevealTime = 0.45f, VanishDelay = 0.04f, VanishTime = 0.2f;

	/// <summary>转枪：抬枪预备之后手一甩，枪套在食指上转两圈（先快后慢），SpinTime 秒后「啪」地接住。</summary>
	public const float Anticipation = 0.12f, SpinTime = 0.7f;

	/// <summary>接住枪的时刻（相对 raiseAt）：Joker 的转枪和接枪声对到这里。</summary>
	public const float CatchAfter = Anticipation + SpinTime;

	/// <summary>现在闪雷有多亮（给场景闪光 / 灯光用）。</summary>
	public float Lightning { get; private set; }

	/// <summary>按全局时刻 g 摆姿势、更新材质。appearAt：召唤的雷劈下；raiseAt：开始抬枪；fireAt：开枪；exitAt：退场的雷劈下。</summary>
	public void Update(float g, float appearAt, float raiseAt, float fireAt, float exitAt, Vector2? chargeLight = null, float chargePower = 0f,
		float flash = 0f, float dim = 0f)
	{
		Vector2 screen = Fx.Screen(_parent);
		float a = g - appearAt;
		float rv = Mathf.Clamp((a - RevealDelay) / RevealTime, 0f, 1f);
		float revealY = a < RevealDelay ? screen.Y + 400f : Mathf.Lerp(screen.Y + 200f, -500f, EaseOutCubic(rv));
		float x = g - exitAt;
		if (x >= VanishDelay)
		{
			revealY = Mathf.Lerp(-500f, screen.Y + 300f, EaseInOut((x - VanishDelay) / VanishTime));      // 退场：雷劈下，从上往下一下烧没
		}
		_mat.SetShaderParameter("reveal_y", revealY);
		_mat.SetShaderParameter("flash", flash);
		_mat.SetShaderParameter("dim", dim);
		_mat.SetShaderParameter("point_light", chargeLight is Vector2 cl ? new Vector4(cl.X, cl.Y, 260f * _scale, chargePower) : Vector4.Zero);
		float s1 = Strike(a), s2 = Strike(x);
		Lightning = Mathf.Max(s1, s2);
		_riftMat.SetShaderParameter("strike", Lightning);
		_riftMat.SetShaderParameter("cx", _riftX);
		_riftMat.SetShaderParameter("seed", s2 > 0f ? 7.3f : 1.1f);

		float rise = 40f * (1f - EaseOutCubic(rv)) + 5f * Mathf.Sin(g * 1.7f);
		float breathe = 0.006f * Mathf.Sin(g * 2.3f);
		// 翅膀：登场时从收拢展开，之后慢慢鼓动
		float unfold = EaseOutCubic((a - RevealDelay) / 0.45f);
		_angle["wing_back"] = Mathf.Lerp(14f, 0f, unfold) + 1.6f * Mathf.Sin(g * 1.9f);
		_angle["wing_front"] = Mathf.Lerp(-18f, 0f, unfold) - 2f * Mathf.Sin(g * 1.9f + 0.4f);
		_angle["cape"] = 1.2f * Mathf.Sin(g * 1.4f + 1f);
		_angle["arm_near"] = 2f * Mathf.Sin(g * 1.6f + 0.5f);
		_angle["head"] = 1f * Mathf.Sin(g * 1.1f);
		_angle["eyes_glow"] = 0f;
		_angle["arm_fore_open"] = 0f;
		_angle["finger_tip"] = 0f;

		float upper = 55f + 1.5f * Mathf.Sin(g * 2.1f), fore = 25f + 2f * Mathf.Sin(g * 2.1f - 0.6f);
		_spin = -1f;
		_angle["gun"] = 0f;
		float r = g - raiseAt;
		if (r >= 0f)
		{
			const float ant = Anticipation, swing = 0.3f, lag = 0.06f;
			upper = r < ant ? Mathf.Lerp(55f, 61f, EaseInOut(r / ant))
				: r < ant + swing ? Mathf.Lerp(61f, -4f, EaseOutCubic((r - ant) / swing))
				: -4f * Mathf.Exp(-(r - ant - swing) / 0.09f) * Mathf.Cos((r - ant - swing) * 22f);
			float rf = r - lag;
			fore = rf < ant ? Mathf.Lerp(25f, 31f, EaseInOut(Mathf.Max(rf, 0f) / ant))
				: rf < ant + swing ? Mathf.Lerp(31f, -3f, EaseOutCubic((rf - ant) / swing))
				: -3f * Mathf.Exp(-(rf - ant - swing) / 0.1f) * Mathf.Cos((rf - ant - swing) * 20f);
			// 转枪：手一甩张开，枪绕食指转两圈（先快后慢），最后接住
			if (r >= ant && r < CatchAfter)
			{
				float k = (r - ant) / SpinTime;
				_spin = 720f * (1f - Mathf.Pow(1f - k, 2.4f));
				_angle["gun"] = _spin;
			}
			else if (r >= CatchAfter)
			{
				float u = r - CatchAfter;
				fore += 5f * Mathf.Exp(-u / 0.06f) * Mathf.Sin(Mathf.Min(u / 0.06f, 1f) * Mathf.Pi);   // 接住那一下手腕往下一顿
			}
			if (g < fireAt && r > CatchAfter)
			{
				float k = Mathf.Clamp((r - CatchAfter) / Mathf.Max(0.1f, fireAt - raiseAt - CatchAfter), 0f, 1f);
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
		bool spinning = Twirls && _spin >= 0f;
		_sprites["arm_fore_gun"].Visible = !spinning;
		foreach (string n2 in new[] { "arm_fore_open", "gun", "finger_tip" })
		{
			if (_sprites.TryGetValue(n2, out Sprite2D? sp2))
			{
				sp2.Visible = spinning;
			}
		}
		_muzzleXf = spinning ? _root * World("gun") : _root * World("arm_fore_gun");
	}
}
