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
	// 第 10 版（照原版模型画的远景巨人，layers_v10 → tools/p5/satanael/game_layers.py v10 0.42），按绘制顺序（从后往前）
	private static readonly Layer[] Layers =
	{
		new("wings_back_far", new(18.5f, 193.2f), new(1008f, 588f), "body"),
		new("wings_back_near", new(1032.4f, 112.1f), new(1113f, 672f), "body"),
		new("body", new(597.7f, 501.1f), new(987f, 840f), null),
		new("wings_front", new(1087f, 596.4f), new(1146.6f, 693f), "body"),
		new("head", new(768.2f, 258.7f), new(966f, 546f), "body"),
		new("arm_upper", new(1101.2f, 543.5f), new(1146.6f, 588f), "body"),
		new("arm_fore_gun", new(1180.6f, 628.7f), new(1245.3f, 699.3f), "arm_upper"),
		// 转枪：空手 + 单独的枪（护圈中心 GunGuard 平移到食指 SpinFinger 上、绕食指转）+ 食指尖（画在枪上面）
		new("arm_fore_open", new(1180.6f, 628.7f), new(1245.3f, 699.3f), "arm_upper"),
		new("gun", new(1438.5f, 721.1f), new(1559f, 820.7f), "arm_fore_open"),
		new("finger_tip", new(1532.2f, 796.7f), new(1559f, 820.7f), "arm_fore_open"),
	};
	/// <summary>画里枪管的朝向（度，顺时针为正；layers.json 的 barrel_dir）。第 10 版加长后的短管枪（用户 2026-11-09）。</summary>
	private const float BarrelDirDeg = 23.4f;
	private static readonly Vector2 MuzzlePx = new(1699.7f, 848f), GunGuard = new(1559f, 820.7f), SpinFinger = new(1559f, 820.7f);
	/// <summary>光环中心（头的局部像素，头顶后上方）、云海顶线（大腿中部，图层像素 y）。</summary>
	private static readonly Vector2 WristPx = new(1373.4f, 785.4f);   // 手腕（图层像素，前臂坐标系）：拖尾跟这里
	private static readonly Vector2 HaloAt = new(918f, 205f);
	private const float CloudTopPx = 930f;    // 腰下（图层像素）：身体图层在 1259 处截断，云要盖过断口一大段（用户 2026-11-09：腿像被截断）

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
uniform float haze = 0.4;
uniform float ghost_len = 400.0;
uniform float ghost_amt = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
void fragment() {
	float ma = COLOR.a;                                                      // 节点 modulate 的透明度（转枪残影用）
	vec4 c = texture(TEXTURE, UV);
	vec2 sp = SCREEN_UV * sres;
	float l = dot(c.rgb, vec3(0.3, 0.59, 0.11));
	// 夜光：压低饱和度（金色变成淡银金）、乘冷蓝环境光；越往下越暗（隐入深渊）
	vec3 g = mix(vec3(l), c.rgb, 0.55) * vec3(0.6, 0.7, 0.95);
	g = mix(g, vec3(0.16, 0.26, 0.4), haze);                                // 远处的大气：蒙一层冷蓝的雾
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
	// 轮廓先行 / 残留（用户 2026-11-09：登场、退场不要直接出现 / 消失）：烧线上方 ghost_len 内只亮一圈轮廓线（闪烁的青白），
	// 登场时轮廓先描出来、实体随后烧上来；退场时实体烧没了，轮廓还留一会儿再散
	vec2 tp = TEXTURE_PIXEL_SIZE * 3.0;
	float nb = min(min(texture(TEXTURE, UV + vec2(tp.x, 0.0)).a, texture(TEXTURE, UV - vec2(tp.x, 0.0)).a),
		min(texture(TEXTURE, UV + vec2(0.0, tp.y)).a, texture(TEXTURE, UV - vec2(0.0, tp.y)).a));
	float outline = clamp(c.a - nb, 0.0, 1.0);
	float lead = step(s, 0.0) * (1.0 - smoothstep(0.0, ghost_len, -s)) * ghost_amt;
	float fl = 0.6 + 0.4 * noise(sp * 0.04 + vec2(clock * 9.0, 0.0));
	float oa = outline * lead * fl;
	float fa = c.a * max(vis, band);
	vec3 oc = vec3(0.55, 0.88, 1.0) * 1.25;
	COLOR = vec4(mix(oc, g, fa / (fa + oa + 1e-4)), max(fa, oa) * alpha * ma);
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

	private static Shader? _ghostSh, _swoosh;

	/// <summary>动作残影：贴图的形状染成一片青蓝（叠加），透明度由 modulate 给。</summary>
	private static Shader GhostShader => _ghostSh ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(0.3, 0.65, 1.0, 1.0);
void fragment() {
	float a = texture(TEXTURE, UV).a;
	COLOR = vec4(tint.rgb * a * COLOR.a, 1.0);
}" };

	/// <summary>
	/// 转枪的弧光（用户 2026-11-09：骨骼动画像提线木偶，要用特效修饰）：以食指为圆心的一段扇环，头部最亮、往后渐隐，
	/// 外缘（枪口扫过的地方，速度最快）最亮，沿半径方向是细细的拉丝；颜色从白到青到紫。
	/// </summary>
	private static Shader SwooshShader => _swoosh ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float head = 0.0;
uniform float span = 2.0;
uniform float r_in = 0.25;
uniform float alpha = 0.0;
uniform float clock = 0.0;
float hh(float n) { return fract(sin(n * 78.233) * 43758.5453); }
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), ang = atan(p.y, p.x);
	float da = mod(head - ang, 6.2831853);
	float k = da / max(span, 0.01);
	float along = (1.0 - smoothstep(0.0, 1.0, k)) * step(da, span);
	along = pow(along, 1.6);
	float rr = (r - r_in) / (1.0 - r_in);
	float radial = smoothstep(0.0, 0.15, rr) * (1.0 - smoothstep(0.82, 1.0, rr)) * mix(0.25, 1.0, rr);
	float fibers = 0.65 + 0.35 * hh(floor(r * 90.0));
	float headGlow = exp(-da / 0.3) * radial * radial;
	float a = along * radial * fibers;
	vec3 col = mix(vec3(0.95, 0.98, 1.0), mix(vec3(0.3, 0.75, 1.0), vec3(0.55, 0.3, 1.0), k), smoothstep(0.0, 0.5, k));
	COLOR = vec4((col * a + vec3(0.9, 0.97, 1.0) * headGlow * 0.22) * alpha, 1.0);
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

	private static Shader? _halo, _clouds;

	/// <summary>原版头顶的光环（模型 m_ring：一圈细环上挂 12 片红色尖刺翼片，model_ref/halo_cel.png）：细环 + 往外的红色尖刺，硬边 P5 配色，慢慢转。</summary>
	private static Shader HaloShader => _halo ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_mix;
uniform float spin = 0.0;
uniform float glow = 1.0;
uniform float alpha = 1.0;
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), a = atan(p.y, p.x) + spin;
	float ring = 1.0 - smoothstep(0.012, 0.03, abs(r - 0.52));
	float k = 12.0;
	float seg = fract(a / 6.28318 * k) - 0.5;                 // 每片尖刺的局部角度
	float t = (r - 0.56) / 0.4;                                // 0 = 根部，1 = 尖
	float blade = step(0.0, t) * step(t, 1.0) * step(abs(seg), 0.42 * (1.0 - t) * (0.6 + 0.4 * sin(t * 9.0 + 1.0)));
	vec3 red = vec3(0.85, 0.05, 0.1), dark = vec3(0.25, 0.0, 0.04), hot = vec3(1.0, 0.55, 0.5);
	vec3 col = mix(red, dark, smoothstep(0.0, 0.25, abs(seg))) + hot * exp(-t * 6.0) * 0.4;
	float halo = exp(-abs(r - 0.6) * 9.0) * 0.25;
	vec3 c = ring > 0.5 ? vec3(0.12, 0.1, 0.12) : col;
	float aa = max(ring, blade);
	COLOR = vec4(c * glow + vec3(1.0, 0.15, 0.2) * halo * glow, max(aa, halo * 0.6) * alpha);
}" };

	/// <summary>远处的云海：盖在人格面具的大腿以下（他从云里升起来），顶线是翻滚的云头，越往下越实；冷蓝、顶上一道天光。cloud_y 是屏幕上的顶线。</summary>
	private static Shader CloudShader => _clouds ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 sres = vec2(1920.0, 1080.0);
uniform float cloud_y = 700.0;
uniform float blast_x = 0.0;
uniform float blast_t = -1.0;
uniform float clock = 0.0;
uniform float alpha = 1.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; } return v; }
void fragment() {
	vec2 px = UV * sres;
	// 两层翻滚的云头（远层稍高、稍淡），顶上一圈冷光；往下越来越实；顶线上方一层薄雾，人物下半身是慢慢没进云里
	float n1 = fbm(vec2(px.x * 0.0035 + clock * 0.02, clock * 0.04));
	float n2 = fbm(vec2(px.x * 0.007 - clock * 0.03, 3.1 + clock * 0.05));
	float top = cloud_y + (n1 - 0.5) * 150.0 + 35.0 * sin(px.x * 0.005 + clock * 0.3);
	// 开枪的冲击波（Blast）：从枪口正下方往两边推开——波前把云面压下去，外侧鼓起一道亮的浪头，里面被压平
	float wave = 0.0;
	if (blast_t >= 0.0) {
		float r = 1100.0 * blast_t + 30.0, fade = exp(-blast_t / 0.7), dx = abs(px.x - blast_x);
		wave = exp(-pow((dx - r) / 120.0, 2.0)) * fade;
		top += 120.0 * wave + 70.0 * fade * (1.0 - smoothstep(r - 200.0, r, dx));
		top -= 55.0 * exp(-pow((dx - r - 170.0) / 90.0, 2.0)) * fade;
	}
	float top2 = top - 70.0 + (n2 - 0.5) * 90.0;
	float d = px.y - top, d2 = px.y - top2;
	float puff = 0.7 + 0.3 * fbm(px * 0.005 + vec2(clock * 0.03, 0.0));
	float near = smoothstep(-40.0, 110.0, d) * mix(puff, 1.0, smoothstep(60.0, 160.0, d));   // 云头有起伏，往下是实心的（不透出身体的断口）
	float back = smoothstep(-30.0, 80.0, d2) * 0.55 * puff;
	float haze = smoothstep(-260.0, 0.0, d) * 0.28;
	float dens = clamp(max(max(near, back), haze), 0.0, 1.0);
	float rim = exp(-abs(d) / 45.0) * near + exp(-abs(d2) / 40.0) * back * 0.7;
	vec3 body = mix(vec3(0.07, 0.11, 0.21), vec3(0.04, 0.06, 0.13), smoothstep(0.0, 400.0, d));
	rim += wave * 1.2 * exp(-abs(d) / 70.0) * near;   // 只亮浪头（云面那一圈），不要竖着一整条
	vec3 col = body + vec3(0.42, 0.6, 0.85) * rim * 1.1 + vec3(0.1, 0.16, 0.26) * (puff - 0.7) * near;
	col = mix(col, vec3(0.16, 0.24, 0.40), haze * (1.0 - near));
	COLOR = vec4(col, dens * alpha);
}" };

	private readonly Dictionary<string, Sprite2D> _sprites = new();
	private readonly Dictionary<string, float> _angle = new();
	private readonly Node2D _parent;
	private readonly ShaderMaterial _mat, _riftMat;
	private readonly Vector2 _origin;
	private readonly float _scale, _aim, _riftX;
	private float _spin = -1f, _spinSpeed;
	private Node2D? _haloRoot;
	private ShaderMaterial? _haloMat;
	private readonly ShaderMaterial _cloudMat;
	private readonly Sprite2D[] _ghosts = new Sprite2D[2];
	// 动作修饰：手臂残影（按历史姿势回放）、枪口拖尾、转枪弧光、烧线上的余烬
	private readonly Sprite2D[] _armGhosts = new Sprite2D[2];
	private readonly List<(float G, Transform2D Fore, bool Open)> _history = new();
	private readonly List<(float G, Vector2 P)> _tipHistory = new();
	private Line2D? _trail, _trailWide;
	private ShaderMaterial? _swooshMat;
	private Node2D? _swooshNode;
	private GpuParticles2D? _embers;
	private float _lastG = -1f;
	private float _embersHalf;
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
			if (L.Name == "wings_back_near")
			{
				// 光环：在翅膀前、身体和头后面；跟着头动，压扁成椭圆（从下往上看的透视）
				_haloRoot = new Node2D { Name = "Halo" };
				parent.AddChild(_haloRoot);
				_haloMat = new ShaderMaterial { Shader = HaloShader };
				const float hs = 300f;
				ColorRect hr = new() { Size = new Vector2(hs, hs), Position = new Vector2(-hs / 2f, -hs / 2f), Material = _haloMat, MouseFilter = Control.MouseFilterEnum.Ignore };
				Node2D squash = new() { Position = HaloAt, Scale = new Vector2(1f, 0.32f), Rotation = -0.18f };
				_haloRoot.AddChild(squash);
				squash.AddChild(hr);
			}
		}
		_cloudMat = new ShaderMaterial { Shader = CloudShader };
		_cloudMat.SetShaderParameter("sres", screen);
		ColorRect clouds = new() { Size = screen, Material = _cloudMat, MouseFilter = Control.MouseFilterEnum.Ignore };
		parent.AddChild(clouds);
		parent.MoveChild(clouds, _sprites["wings_front"].GetIndex() + 1);   // 云盖住腿和翅膀下端，头和持枪手臂在云上面
		Fx.Clock(clouds);
		Fx.Clock(_sprites["body"]);
		// 转枪的残影：枪后面两层半透明的枪，落后几度（转得越快落得越多）——转起来是连贯的动态模糊，不是一帧帧跳
		if (_sprites["gun"].Texture != null)
		{
			for (int i = 0; i < 2; i++)
			{
				Sprite2D gh = new() { Texture = _sprites["gun"].Texture, Centered = false, Offset = Find("gun").Offset, Material = _mat, Visible = false,
					Modulate = new Color(1f, 1f, 1f, i == 0 ? 0.4f : 0.18f) };
				parent.AddChild(gh);
				parent.MoveChild(gh, _sprites["gun"].GetIndex());
				_ghosts[i] = gh;
			}
		}
		BuildMotionFx(parent, origin, scale);
		Vector2 shoulder = Find("arm_upper").Pivot;
		Vector2 sS = origin + shoulder * scale;
		// 瞄准（用户 2026-11-09：枪口没对着敌人）：不是让「肩 → 枪口」指向敌人，而是让**枪管的延长线**穿过敌人——
		// 手臂转 θ 后枪口在 M(θ)、枪管朝向 D(θ) = 画里的枪管朝向 + θ；迭代到 D 正好指向「M → 敌人」
		// 逐个角度试：枪管延长线离敌人最近、敌人又在枪口前方的那个角度
		float drawnBarrel = Mathf.DegToRad(BarrelDirDeg), th = 0f, best = float.MaxValue;
		for (float deg = -30f; deg <= 30f; deg += 0.5f)
		{
			float r = Mathf.DegToRad(deg);
			Vector2 m = sS + (MuzzlePx - shoulder).Rotated(r) * scale;
			Vector2 d = Vector2.FromAngle(drawnBarrel + r);
			Vector2 tm = target - m;
			float miss = Mathf.Abs(d.Cross(tm)) + (d.Dot(tm) < 0f ? 1e5f : 0f);
			if (miss < best) { best = miss; th = r; }
		}
		_aim = Mathf.Clamp(Mathf.RadToDeg(th), -25f, 25f);
		_riftX = origin.X + 400f * scale;
	}

	private static Layer Find(string n) => Array.Find(Layers, l => l.Name == n)!;

	private static Transform2D Joint(Vector2 pivot, float deg) =>
		Transform2D.Identity.Translated(-pivot).Rotated(Mathf.DegToRad(deg)).Translated(pivot);

	private Transform2D World(string name)
	{
		Layer L = Find(name);
		Transform2D own = Joint(L.Pivot, _angle[name]);
		if (name == "gun")
		{
			own = own * Transform2D.Identity.Translated(SpinFinger - GunGuard);   // 护圈套到食指上
		}
		return L.Parent == null ? own : World(L.Parent) * own;
	}

	/// <summary>枪口在 parent 坐标（屏幕像素）里的位置。</summary>
	public Vector2 MuzzleLocal => _muzzleXf * MuzzlePx;

	/// <summary>枪口的全局位置（特效层坐标）。</summary>
	public Vector2 MuzzleGlobal => _parent.GetGlobalTransform() * MuzzleLocal;

	private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp(x, 0f, 1f), 3f);
	private static float EaseInOut(float x) { x = Mathf.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }

	/// <summary>登场：闪雷劈下 0.05s 后从下往上烧出来（轮廓线先行、实体随后，从雾里显形）。退场：闪雷劈下后从上往下烧没（轮廓残留一会儿、翅膀收拢、往云里沉），余烬往上飘。</summary>
	public const float RevealDelay = 0.05f, RevealTime = 0.55f, VanishDelay = 0.04f, VanishTime = 0.5f;

	/// <summary>转枪：抬枪预备之后手一甩，枪套在食指上转两圈（先快后慢），SpinTime 秒后「啪」地接住。</summary>
	public const float Anticipation = 0.04f, SpinTime = 0.45f;

	/// <summary>接住枪的时刻（相对 raiseAt）：Joker 的转枪和接枪声对到这里。</summary>
	public const float CatchAfter = Anticipation + SpinTime;

	/// <summary>现在闪雷有多亮（给场景闪光 / 灯光用）。</summary>
	public float Lightning { get; private set; }

	/// <summary>按全局时刻 g 摆姿势、更新材质。appearAt：召唤的雷劈下；raiseAt：开始抬枪；fireAt：开枪；exitAt：退场的雷劈下。</summary>
	private void BuildMotionFx(Node2D parent, Vector2 origin, float scale)
	{
		// 前臂残影（只做前臂 + 手：上臂几乎不动，残影会在肩甲后面露出一块）：两层，回放 0.035s / 0.07s 前的姿势，藏在上臂后面
		int behind = _sprites["arm_upper"].GetIndex();
		for (int lvl = 0; lvl < 2; lvl++)
		{
			ShaderMaterial gm = new() { Shader = GhostShader };
			gm.SetShaderParameter("tint", lvl == 0 ? new Color(0.3f, 0.65f, 1f) : new Color(0.4f, 0.35f, 1f));
			Sprite2D gs = new() { Texture = _sprites["arm_fore_gun"].Texture, Centered = false, Offset = Find("arm_fore_gun").Offset, Material = gm, Visible = false };
			parent.AddChild(gs);
			parent.MoveChild(gs, behind);
			_armGhosts[lvl] = gs;
		}
		// 手腕拖尾：细的白青 + 宽的淡青，尾部渐隐、两头收尖（轨迹做过平滑，不出折角）
		Line2D trail(float width, Color head, Color tail)
		{
			Line2D l = new()
			{
				Width = width, JointMode = Line2D.LineJointMode.Round, BeginCapMode = Line2D.LineCapMode.Round, EndCapMode = Line2D.LineCapMode.Round,
				Antialiased = true, Material = Fx.Additive,
				Gradient = new Gradient { Offsets = new[] { 0f, 0.6f, 1f }, Colors = new[] { new Color(tail, 0f), tail, head } },
				WidthCurve = new Curve(),
			};
			l.WidthCurve.AddPoint(new Vector2(0f, 0f));
			l.WidthCurve.AddPoint(new Vector2(0.75f, 1f));
			l.WidthCurve.AddPoint(new Vector2(1f, 0.35f));
			parent.AddChild(l);
			return l;
		}
		_trailWide = trail(46f * scale, new Color(0.45f, 0.8f, 1f, 0.35f), new Color(0.4f, 0.3f, 1f, 0.15f));
		_trail = trail(12f * scale, new Color(0.95f, 0.99f, 1f, 0.95f), new Color(0.35f, 0.75f, 1f, 0.6f));
		// 转枪弧光
		_swooshMat = new ShaderMaterial { Shader = SwooshShader };
		_swooshNode = new Node2D { Visible = false };
		float rs = 190f;
		ColorRect sq = new() { Size = new Vector2(rs * 2f, rs * 2f), Position = new Vector2(-rs, -rs), Material = _swooshMat, MouseFilter = Control.MouseFilterEnum.Ignore };
		_swooshNode.AddChild(sq);
		parent.AddChild(_swooshNode);
		parent.MoveChild(_swooshNode, _sprites["gun"].GetIndex());
		Fx.Clock(sq);
		// 烧线余烬：一条横着的发射带，跟着烧线走
		_embersHalf = 1050f * scale;
		_embers = ReadyFx.Embers(parent, new Vector2(origin.X + 1150f * scale, 2000f),
			new[] { new Color(0.9f, 0.97f, 1f), new Color(0.35f, 0.75f, 1f), new Color(0.15f, 0.3f, 0.95f) }, 220, 10f, 1.0f, 60f, 220f, 18f);
		if (_embers.ProcessMaterial is ParticleProcessMaterial pm)
		{
			pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
			pm.EmissionBoxExtents = new Vector3(_embersHalf, 8f, 0f);
		}
		_embers.Emitting = false;
	}

	/// <summary>一小团光点爆开（接枪、甩手时）：柔光点往四周飞、变小变淡。</summary>
	private void Burst(Vector2 at, int n, float speed, Color tint)
	{
		Sprite2D core = Glow.SoftSprite(_parent, at, new Color(0.9f, 0.97f, 1f), 0.9f * _scale * 2f);
		core.Material = Fx.Additive;
		Fx.Animate(core, 0, 0.18, t => { core.Scale = Vector2.One * _scale * Mathf.Lerp(1.6f, 2.6f, t); core.Modulate = new Color(1f, 1f, 1f, 1f - t); });
		Fx.FreeAfter(core, 0.2);
		for (int i = 0; i < n; i++)
		{
			Vector2 v = Vector2.FromAngle(Fx.Rand(0f, Mathf.Tau)) * speed * Fx.Rand(0.4f, 1f) * _scale;
			Sprite2D d = Glow.SoftSprite(_parent, at, tint, Fx.Rand(0.12f, 0.3f) * _scale * 2f);
			d.Material = Fx.Additive;
			float life = Fx.Rand(0.25f, 0.5f), s0 = d.Scale.X;
			Fx.Animate(d, 0, life, t => { d.Position = at + v * Fx.OutCubic(t) * life; d.Scale = Vector2.One * s0 * (1f - t); d.Modulate = new Color(1f, 1f, 1f, 1f - t * t); });
			Fx.FreeAfter(d, life + 0.05);
		}
	}

	private void UpdateMotionFx(float g, float raiseAt, float fireAt, bool spinning)
	{
		float dt = _lastG < 0f ? 0f : g - _lastG;
		float r = g - raiseAt, f = g - fireAt;
		// 一次性的点缀：甩手出去那一下、接住枪那一下
		if (_lastG >= 0f)
		{
			float r0 = _lastG - raiseAt;
			if (r0 < Anticipation && r >= Anticipation)
			{
				Burst(_root * World("arm_fore_gun") * Find("arm_fore_gun").Pivot, 6, 260f, new Color(0.4f, 0.8f, 1f));
			}
			if (Twirls && r0 < CatchAfter && r >= CatchAfter)
			{
				Burst(_parent.GetGlobalTransform().AffineInverse() * MuzzleGlobal, 14, 420f, new Color(0.55f, 0.85f, 1f));
			}
		}
		_lastG = g;
		if (dt <= 0f)
		{
			return;
		}
		// 历史姿势（前臂）
		Transform2D fore = _root * World("arm_fore_gun");
		_history.Add((g, fore, spinning));
		while (_history.Count > 0 && g - _history[0].G > 0.2f) _history.RemoveAt(0);
		bool swing = (r >= Anticipation && r < CatchAfter + 0.1f) || (f >= 0f && f < 0.3f);
		for (int lvl = 0; lvl < 2; lvl++)
		{
			float back = 0.035f * (lvl + 1);
			var past = _history.FindLast(h => g - h.G >= back);
			// 动得越快越显：按这段时间里前臂转了多少度
			float turned = past.G > 0f ? Mathf.Abs(Mathf.RadToDeg(Mathf.AngleDifference(past.Fore.Rotation, fore.Rotation))) : 0f;
			float alpha = (lvl == 0 ? 0.24f : 0.13f) * Mathf.Clamp((turned - 2f) / 10f, 0f, 1f);
			Sprite2D gs = _armGhosts[lvl];
			gs.Visible = swing && alpha > 0.01f;
			if (gs.Visible)
			{
				string tex = past.Open && Twirls ? "arm_fore_open" : "arm_fore_gun";
				gs.Texture = _sprites[tex].Texture;
				gs.Offset = Find(tex).Offset;
				gs.Transform = past.Fore;
				gs.Modulate = new Color(1f, 1f, 1f, alpha);
			}
		}
		// 手腕拖尾（局部坐标）：最近 0.14s 的手腕轨迹，Catmull-Rom 平滑
		Vector2 tip = fore * WristPx;
		_tipHistory.Add((g, tip));
		while (_tipHistory.Count > 0 && g - _tipHistory[0].G > 0.14f) _tipHistory.RemoveAt(0);
		float travel = 0f;
		for (int i = 1; i < _tipHistory.Count; i++) travel += _tipHistory[i].P.DistanceTo(_tipHistory[i - 1].P);
		bool trailOn = swing && _tipHistory.Count > 3 && travel > 25f * _scale;
		foreach (Line2D? l in new[] { _trail, _trailWide })
		{
			if (l == null) continue;
			l.Visible = trailOn;
			if (trailOn)
			{
				var pts = new List<Vector2>();
				int n = _tipHistory.Count;
				for (int i = 0; i < n - 1; i++)
				{
					Vector2 p0 = _tipHistory[Math.Max(i - 1, 0)].P, p1 = _tipHistory[i].P, p2 = _tipHistory[i + 1].P, p3 = _tipHistory[Math.Min(i + 2, n - 1)].P;
					for (int k = 0; k < 4; k++)
					{
						float t = k / 4f, t2 = t * t, t3 = t2 * t;
						pts.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
					}
				}
				pts.Add(_tipHistory[n - 1].P);
				l.Points = pts.ToArray();
				l.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(travel / (120f * _scale), 0f, 1f));
			}
		}
		// 转枪弧光：跟着食指，弧头对着现在的枪管方向，弧长跟转速走
		if (_swooshNode != null && _swooshMat != null)
		{
			_swooshNode.Visible = spinning && _spinSpeed > 120f;
			if (_swooshNode.Visible)
			{
				_swooshNode.Transform = _root * World("arm_fore_open") * Transform2D.Identity.Translated(SpinFinger);
				_swooshMat.SetShaderParameter("head", Mathf.DegToRad(BarrelDirDeg + _spin));
				_swooshMat.SetShaderParameter("span", Mathf.Clamp(Mathf.DegToRad(_spinSpeed * 0.07f), 0.3f, 3.6f));
				_swooshMat.SetShaderParameter("alpha", Mathf.Clamp(_spinSpeed / 1500f, 0f, 1f));
			}
		}
	}

	/// <summary>开枪：远处云海被枪口冲击波压开、往两边翻卷（云着色器里的 blast）。</summary>
	public void Blast()
	{
		_cloudMat.SetShaderParameter("blast_x", MuzzleGlobal.X);
		Fx.Animate(_parent, 0, 1.6, t => _cloudMat.SetShaderParameter("blast_t", t * 1.6f));
		Fx.After(_parent, 1.65, () => _cloudMat.SetShaderParameter("blast_t", -1f));
	}

	public void Update(float g, float appearAt, float raiseAt, float fireAt, float exitAt, Vector2? chargeLight = null, float chargePower = 0f,
		float flash = 0f, float dim = 0f)
	{
		Vector2 screen = Fx.Screen(_parent);
		float a = g - appearAt;
		float rv = Mathf.Clamp((a - RevealDelay) / RevealTime, 0f, 1f);
		float cloudLine = (_root * new Vector2(0f, CloudTopPx)).Y;
		float revealY = a < RevealDelay ? screen.Y + 400f : Mathf.Lerp(cloudLine + 140f, -160f, EaseInOut(rv));   // 烧线扫过人物本身（云线 → 翅尖），整段时间都看得见
		float x = g - exitAt;
		if (x >= VanishDelay)
		{
			revealY = Mathf.Lerp(-160f, cloudLine + 160f, EaseInOut((x - VanishDelay) / VanishTime));
			if (x - VanishDelay >= VanishTime) revealY = screen.Y + 600f;                              // 烧完：把烧线挪出画面（不然云里还留一截蓝焰）      // 退场：雷劈下，从翅尖往下烧到云里
		}
		_mat.SetShaderParameter("reveal_y", revealY);
		float vx = Mathf.Clamp((x - VanishDelay) / VanishTime, 0f, 1f);
		bool leaving = x >= VanishDelay;
		// 从雾里显形 / 隐回雾里；轮廓线：登场时领先实体，退场时残留
		_mat.SetShaderParameter("haze", leaving ? Mathf.Lerp(0.4f, 0.85f, EaseInOut(vx)) : Mathf.Lerp(0.85f, 0.4f, EaseOutCubic(rv)));
		_mat.SetShaderParameter("ghost_len", (leaving ? 620f : 460f) * _scale);
		_mat.SetShaderParameter("ghost_amt", leaving ? 1f - Mathf.Clamp((x - VanishDelay - VanishTime * 0.6f) / (VanishTime * 0.7f), 0f, 1f)
			: a < RevealDelay ? 0f : 1f - Mathf.Clamp((rv - 0.75f) / 0.25f, 0f, 1f));
		// 烧线上的余烬（登场往上冒、退场往上飘散）
		if (_embers != null)
		{
			bool burning = (a >= RevealDelay && rv < 1f) || (leaving && vx < 1f);
			_embers.Emitting = burning;
			_embers.Position = new Vector2(_origin.X + 1150f * _scale, Mathf.Clamp(revealY, -100f, screen.Y + 100f));
		}
		_mat.SetShaderParameter("flash", flash);
		_mat.SetShaderParameter("dim", dim);
		_mat.SetShaderParameter("point_light", chargeLight is Vector2 cl ? new Vector4(cl.X, cl.Y, 260f * _scale, chargePower) : Vector4.Zero);
		float s1 = Strike(a), s2 = Strike(x);
		Lightning = Mathf.Max(s1, s2);
		_riftMat.SetShaderParameter("strike", Lightning);
		_riftMat.SetShaderParameter("cx", _riftX);
		_riftMat.SetShaderParameter("seed", s2 > 0f ? 7.3f : 1.1f);

		float rise = 40f * (1f - EaseOutCubic(rv)) + 5f * Mathf.Sin(g * 1.7f) + 70f * EaseInOut(vx) * (leaving ? 1f : 0f);
		float breathe = 0.006f * Mathf.Sin(g * 2.3f);
		// 翅膀：登场时从收拢展开，之后小幅鼓动（±1.5° 以内，翼根不露切边）
		float unfold = EaseOutCubic((a - RevealDelay) / 0.55f) * (leaving ? 1f - EaseInOut(vx) : 1f);   // 退场时翅膀收拢
		_angle["wings_back_far"] = Mathf.Lerp(-6f, 0f, unfold) - 1.2f * Mathf.Sin(g * 1.9f);
		_angle["wings_back_near"] = Mathf.Lerp(6f, 0f, unfold) + 1.5f * Mathf.Sin(g * 1.9f + 0.4f);
		_angle["wings_front"] = Mathf.Lerp(8f, 0f, unfold) + 1.2f * Mathf.Sin(g * 1.7f + 0.9f);
		_angle["head"] = 1f * Mathf.Sin(g * 1.1f);
		_angle["arm_fore_open"] = 0f;
		_angle["finger_tip"] = 0f;
		float upper = 55f + 1.5f * Mathf.Sin(g * 2.1f), fore = 25f + 2f * Mathf.Sin(g * 2.1f - 0.6f);
		_spin = -1f;
		_spinSpeed = 0f;
		_angle["gun"] = 0f;
		float r = g - raiseAt;
		if (r >= 0f)
		{
			const float ant = Anticipation, swing = 0.32f, lag = 0.05f;
			upper = r < ant ? Mathf.Lerp(55f, 58f, EaseInOut(r / ant))
				: r < ant + swing ? Mathf.Lerp(58f, -4f, EaseInOut((r - ant) / swing))
				: -4f * Mathf.Exp(-(r - ant - swing) / 0.09f) * Mathf.Cos((r - ant - swing) * 22f);
			float rf = r - lag;
			fore = rf < ant ? Mathf.Lerp(25f, 28f, EaseInOut(Mathf.Max(rf, 0f) / ant))
				: rf < ant + swing ? Mathf.Lerp(28f, -3f, EaseInOut((rf - ant) / swing))
				: -3f * Mathf.Exp(-(rf - ant - swing) / 0.1f) * Mathf.Cos((rf - ant - swing) * 20f);
			// 转枪：手一甩张开，枪绕食指转两圈（先快后慢），最后接住
			if (r >= ant && r < CatchAfter)
			{
				// 转速从快到 0 平滑减下来（接住时速度正好为 0）；手腕跟着枪甩，幅度越来越小，手不僵在原地
				float k = (r - ant) / SpinTime;
				_spin = 720f * (1f - Mathf.Pow(1f - k, 3f));
				_angle["gun"] = _spin;
				fore += 7f * Mathf.Sin(Mathf.DegToRad(_spin)) * (1f - k);
				upper += 2.5f * Mathf.Sin(Mathf.DegToRad(_spin) + 0.8f) * (1f - k);
				_spinSpeed = 720f * 3f * Mathf.Pow(1f - k, 2f) / SpinTime;      // 度/秒（残影用）
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
			_angle["wings_back_near"] += 2f * kb;
			_angle["wings_front"] += 2.5f * kb;
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
		if (_haloRoot != null && _haloMat != null)
		{
			_haloRoot.Transform = _root * World("head");
			_haloMat.SetShaderParameter("spin", g * 0.6f);
			_haloMat.SetShaderParameter("alpha", revealY < screen.Y ? Mathf.Clamp((screen.Y - revealY) / 300f, 0f, 1f) : 0f);
		}
		_cloudMat.SetShaderParameter("cloud_y", (_root * new Vector2(0f, CloudTopPx)).Y);
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
		for (int i = 0; i < 2; i++)
		{
			if (_ghosts[i] is not Sprite2D gh) continue;
			gh.Visible = spinning && _spinSpeed > 200f;
			if (gh.Visible)
			{
				float lagDeg = Mathf.Min(70f, _spinSpeed * 0.018f * (i + 1));
				Layer G = Find("gun");
				gh.Transform = _root * World("arm_fore_open") * Joint(G.Pivot, _spin - lagDeg) * Transform2D.Identity.Translated(SpinFinger - GunGuard);
			}
		}
		UpdateMotionFx(g, raiseAt, fireAt, spinning);
	}
}
