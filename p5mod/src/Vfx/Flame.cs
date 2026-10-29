using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 细丝火焰（用户 2026-10-29 给的参考：P5 觉醒时的一整面蓝焰；火里的亚森脸试过不好看，用户要求只做火）。
/// 火不是一团柔光，而是成百上千道竖向、细长、边缘锐利的火丝叠在一起：脊状噪声（1-|n|）竖着拉长往上流、再经两层扭曲弯曲分叉；
/// 只有火丝的脊亮，丝与丝之间是很深的底色，明暗对比强。普通混合（不是加法），所以底色能压暗后面、亚森的脸能是一块暗剪影。
/// 咒怨的黑红火柱（Column）用它；Persona 的蓝焰面具（PersonaMask）用同一套细丝，火源换成面具轮廓。
/// </summary>
public static class Flame
{
	private static Shader? _strands;

	public static Shader Strands => _strands ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec4 c_dark : source_color = vec4(0.04, 0.07, 0.25, 1.0);
uniform vec4 c_mid : source_color = vec4(0.12, 0.36, 1.0, 1.0);
uniform vec4 c_hi : source_color = vec4(0.31, 0.78, 1.0, 1.0);
uniform vec4 c_hot : source_color = vec4(0.92, 1.0, 1.0, 1.0);
uniform float rise = 1.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
uniform float speed = 1.0;
uniform float body = 0.7;
uniform float base_w = 0.42;
uniform float top_w = 0.1;
uniform float glow = 1.0;
uniform float base_fade = 0.08;
uniform float clock = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 4; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.3, 4.1); a *= 0.5; }
	return v;
}
float ridge(vec2 p) {
	float v = 0.0, a = 0.55;
	for (int i = 0; i < 5; i++) { float n = 1.0 - abs(noise(p) * 2.0 - 1.0); v += a * n * n; p = p * 2.07 + vec2(3.1, 7.7); a *= 0.5; }
	return v;
}
void fragment() {
	vec2 uv = UV;
	float y = 1.0 - uv.y;
	float t = clock * speed + seed * 13.0;
	// 两层扭曲：大尺度的摆动 + 往上流的卷曲
	vec2 w = vec2(fbm(vec2(uv.x * 2.2 + seed, y * 1.1 - t * 0.3)), fbm(vec2(uv.x * 2.2 + 5.2, y * 1.1 - t * 0.3 + 3.0)));
	vec2 w2 = vec2(fbm(vec2(uv.x * 5.0 + w.x * 2.0, y * 2.5 - t * 0.9)), fbm(vec2(uv.x * 5.0 + 2.7, y * 2.5 - t * 0.9 + w.y * 2.0)));
	float x = uv.x + (w.x - 0.5) * 0.2 * (0.4 + y) + (w2.x - 0.5) * 0.06;
	// 轮廓：底宽顶窄，边缘和顶端被火舌撕碎
	float yy = clamp(y / max(rise, 0.01), 0.0, 1.5);
	float hw = mix(base_w, top_w, pow(clamp(yy, 0.0, 1.0), 1.1)) + (fbm(vec2(x * 4.0, y * 3.0 - t * 1.2)) - 0.5) * 0.3;
	// 两侧往外甩的火舌：沿高度往上流的噪声在边上掏出缺口、伸出尖
	float side = noise(vec2(y * 7.0 - t * 2.4, x * 3.0 + seed)) - 0.5;
	hw += side * 0.12 * smoothstep(0.1, 0.6, yy);
	float topNoise = fbm(vec2(x * 6.0 + seed, y * 2.0 - t * 1.6));
	float shape = smoothstep(hw, hw - 0.07, abs(x - 0.5)) * smoothstep(0.0, base_fade, y + (topNoise - 0.5) * base_fade) * smoothstep(1.0, 0.9, y);
	shape *= smoothstep(rise + 0.05, rise - 0.3, y + (topNoise - 0.5) * 0.35);
	float tongues = smoothstep(0.5, 0.78, noise(vec2(x * 8.0 + seed, y * 4.0 - t * 2.0)));
	shape *= 1.0 - tongues * smoothstep(0.5, 1.0, yy) * 0.85;
	// 体积：大块的明暗（火的身体）
	float vol = fbm(vec2(x * 3.0 + w2.y, y * 1.3 - t * 0.6));
	// 笔触火丝（中等宽度、竖长、弯曲）+ 白热细丝（稀疏）
	float sA = ridge(vec2(x * 5.5 + w2.y * 1.5, y * 0.9 - t * 1.0));
	float sB = ridge(vec2(x * 11.0 + w.x * 3.0 + 4.0, y * 1.6 - t * 1.7));
	float sC = ridge(vec2(x * 21.0 + w2.x * 4.0 + 9.0, y * 2.4 - t * 2.4));
	float e = vol * 0.55 + pow(sA, 2.2) * 0.75 + pow(sB, 3.5) * 0.7 + pow(sC, 6.0) * 0.8;
	e *= 0.9 + 0.4 * smoothstep(0.6, 0.0, yy);         // 下面更亮
	float k = e * shape;
	vec3 col = c_dark.rgb;
	col = mix(col, c_mid.rgb, smoothstep(0.15, 0.55, k));
	col = mix(col, c_hi.rgb, smoothstep(0.5, 0.95, k));
	col = mix(col, c_hot.rgb, smoothstep(0.95, 1.35, k));
	float a = shape * (body + (1.0 - body) * smoothstep(0.1, 0.6, k));
	COLOR = vec4(col * glow, clamp(a, 0.0, 1.0) * alpha);
}" };

	public static ColorRect Rect(Node parent, Vector2 foot, float w, float h)
	{
		ColorRect r = new()
		{
			Size = new Vector2(w, h), Position = foot - new Vector2(w / 2f, h), PivotOffset = new Vector2(w / 2f, h),
			MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Strands },
		};
		parent.AddChild(r);
		Set(r, "seed", Fx.Rand(0f, 10f));
		Fx.Clock(r);
		return r;
	}

	public static void Set(ColorRect r, string name, Variant v) => ((ShaderMaterial)r.Material).SetShaderParameter(name, v);

	public static void Palette(ColorRect r, Color dark, Color mid, Color hi, Color hot)
	{
		Set(r, "c_dark", dark);
		Set(r, "c_mid", mid);
		Set(r, "c_hi", hi);
		Set(r, "c_hot", hot);
	}

	// P5 的咒怨（Eiha）：黑底、绯红火丝、粉白热芯
	public static readonly Color CurseDark = new("070002"), CurseMid = new("5c000e"), CurseHi = new("e3122b"), CurseHot = new("ff9a86");

	/// <summary>
	/// 一根咒焰柱：脚在 foot，宽 w 高 h；delay 后 0.12s 蹿到顶（过冲）、烧 life 秒，最后往中间一缩收掉。
	/// </summary>
	public static ColorRect Column(Control layer, Vector2 foot, float w, float h, float delay, float life, float speed = 1.3f)
	{
		Node2D root = Fx.Root(layer, foot, "P5CurseColumn", delay + life + 0.1);
		ColorRect r = Rect(root, Vector2.Zero, w, h);
		Palette(r, CurseDark, CurseMid, CurseHi, CurseHot);
		Set(r, "speed", speed);
		Set(r, "body", 0.9f);
		Set(r, "base_fade", 0.2f);
		Set(r, "base_w", 0.3f);
		Set(r, "top_w", 0.06f);
		Set(r, "rise", 0f);
		Fx.Animate(root, delay, life, t =>
		{
			float up = Ease5.OutBack(Ease5.Seg(t, 0f, 0.12f / life)), end = Fx.InCubic(Ease5.Seg(t, 0.78f, 0.22f));
			Set(r, "rise", Mathf.Clamp(up, 0f, 1.1f) * (1f - 0.5f * end));
			Set(r, "base_w", 0.3f * (1f - end));
			Set(r, "top_w", 0.06f * (1f - end));
			Set(r, "glow", 1f + 0.6f * (1f - Ease5.Seg(t, 0f, 0.25f)));
		});
		return r;
	}
}

/// <summary>
/// Persona 的蓝焰面具（用户 2026-10-29：火不接地，沿着面具轮廓烧，轮廓不用太规整）：
/// 拿着技能牌、手扣面具时，Joker 脸上戴着一张宽宽的、两边尖角往上翘的面具形状的蓝焰（用户：要真的在脸上，不是背景；大小不变），
/// 火丝从面具的边沿往上舔，两个眼洞是空的。从中间往两边点燃，一直烧；打出时一亮、往外一胀炸开，放回时往中间缩掉。
/// </summary>
public sealed class PersonaMask
{
	public static readonly Color Dark = new("081040"), Mid = new("1d58ff"), Hi = new("4fc6ff"), Hot = new("eafcff");

	private static Shader? _shader;

	/// <summary>
	/// 面具形状的距离场（两侧对称的斜椭圆 + 往上翘的尖角 + 鼻梁，挖掉两个眼洞），先被噪声扭一下（不规整）；
	/// 每个像素往下看几段（火往上烧 = 往下找火源），沿途横向漂移，叠出从轮廓往上舔的火舌；再乘上和蓝焰墙同一套细丝。
	/// 单位：面具半宽 = 1。kx / ky：矩形的宽 / 高是几个面具半宽；cy：面具中心在矩形里的高度（UV，往下为正）。
	/// </summary>
	private static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec4 c_dark : source_color = vec4(0.03, 0.06, 0.25, 1.0);
uniform vec4 c_mid : source_color = vec4(0.11, 0.35, 1.0, 1.0);
uniform vec4 c_hi : source_color = vec4(0.31, 0.78, 1.0, 1.0);
uniform vec4 c_hot : source_color = vec4(0.92, 0.99, 1.0, 1.0);
uniform float kx = 2.8;
uniform float ky = 2.2;
uniform float cy = 0.66;
uniform float ignite = 1.0;
uniform float flame = 1.0;
uniform float window = 1.0;
uniform float outline = 1.0;
uniform float alpha = 1.0;
uniform float glow = 1.0;
uniform float seed = 0.0;
uniform float clock = 0.0;
uniform float yaw = 0.75;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 4; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.3, 4.1); a *= 0.5; }
	return v;
}
float ridge(vec2 p) {
	float v = 0.0, a = 0.55;
	for (int i = 0; i < 5; i++) { float n = 1.0 - abs(noise(p) * 2.0 - 1.0); v += a * n * n; p = p * 2.07 + vec2(3.1, 7.7); a *= 0.5; }
	return v;
}
float ell(vec2 p, vec2 c, vec2 r, float ang) {
	p -= c;
	float cs = cos(ang), sn = sin(ang);
	p = vec2(cs * p.x + sn * p.y, -sn * p.x + cs * p.y);
	return (length(p / r) - 1.0) * min(r.x, r.y);
}
float smin(float a, float b, float k) { float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0); return mix(b, a, h) - k * h * (1.0 - h); }
// Joker 的面具：<0 在面具上
float maskSdf(vec2 p) {
	vec2 q = vec2(abs(p.x), p.y);
	float body = ell(q, vec2(0.46, 0.03), vec2(0.56, 0.26), 0.2);
	float tip = ell(q, vec2(0.9, 0.3), vec2(0.38, 0.06), 0.72);
	float bridge = ell(q, vec2(0.0, 0.0), vec2(0.2, 0.13), 0.0);
	float d = smin(smin(body, tip, 0.08), bridge, 0.1);
	float hole = ell(q, vec2(0.44, 0.04), vec2(0.23, 0.12), 0.18);
	return max(d, -hole);
}
void fragment() {
	float t = clock + seed * 11.0;
	vec2 sp = vec2((UV.x - 0.5) * kx, (cy - UV.y) * ky);
	// 面具贴在 3/4 侧的脸上：绕竖轴转 yaw（透视：远的那一半，也就是 +x，变窄变小）。屏幕点反推回面具平面上的点
	float f = 3.5, cs = cos(yaw), sn = sin(yaw);
	float mx = sp.x * f / max(f * cs - sp.x * sn, 0.2);
	vec2 p = vec2(mx, sp.y * (f + mx * sn * 0.5) / f);          // 竖向只给一半的透视，近的那半边不显胖
	// 轮廓不规整：整体被慢慢流动的噪声扭一下
	vec2 wob = vec2(fbm(p * 1.6 + vec2(seed, -t * 0.4)), fbm(p * 1.6 + vec2(4.3, -t * 0.4 + 2.0))) - 0.5;
	vec2 pw = p + wob * 0.14;
	float reveal = smoothstep(ignite * 1.25, ignite * 1.25 - 0.25, abs(p.x));
	// 火舌：往下找火源；每一列火舌有高有低（列噪声高的地方烧得高），越往上越细越淡
	float sd0 = maskSdf(pw);
	float onMask = smoothstep(0.03, -0.03, sd0);
	float fire = 0.0;
	float len = 1.35 * flame;
	// 采样够密、每段的边够软，相邻两段互相重叠，火舌上不出台阶
	for (int i = 1; i < 25; i++) {
		float k = float(i) / 24.0;
		float drift = (noise(vec2(p.x * 3.0 + seed, p.y * 2.5 - t * 2.2)) - 0.5) * 0.3 * k;
		float d = maskSdf(pw - vec2(drift, len * k));
		float src = smoothstep(0.06, -0.04, d);
		float colN = fbm(vec2((p.x - drift) * 7.0 + seed, p.y * 1.1 - t * 3.0));
		float reach = smoothstep(0.2 + k * 0.55, 0.4 + k * 0.55, colN);
		// 上半截的火舌一块块断开、往上飘走
		float gap = smoothstep(0.52, 0.8, noise(vec2((p.x - drift) * 6.0 + seed * 2.0, p.y * 2.6 - t * 3.6)));
		fire = max(fire, src * reach * (1.0 - k * 0.55) * (1.0 - gap * smoothstep(0.35, 0.8, k) * 0.95));
	}
	fire = max(fire, onMask * 0.32 * outline);                         // 面具本体淡一些，主要看亮边和火舌（用户：稍微挡人物）
	// 细丝
	vec2 w = vec2(fbm(vec2(p.x * 2.0, p.y * 1.2 - t * 0.5)), fbm(vec2(p.x * 2.0 + 5.0, p.y * 1.2 - t * 0.5)));
	float sA = ridge(vec2(p.x * 4.0 + w.y * 1.5, p.y * 1.2 - t * 1.6));
	float sB = ridge(vec2(p.x * 9.0 + w.x * 2.5 + 4.0, p.y * 2.0 - t * 2.4));
	float vol = fbm(vec2(p.x * 2.5, p.y * 2.0 - t * 0.9));
	float e = (vol * 0.5 + pow(sA, 2.2) * 0.75 + pow(sB, 3.5) * 0.7) * fire;
	// 面具轮廓本身一道白热的边
	float rim = exp(-pow(sd0 / 0.03, 2.0));
	e += rim * 0.75 * (0.6 + 0.6 * sA) * outline;
	e *= reveal;
	float shape = fire * reveal;
	vec3 col = c_dark.rgb;
	col = mix(col, c_mid.rgb, smoothstep(0.12, 0.5, e));
	col = mix(col, c_hi.rgb, smoothstep(0.45, 0.9, e));
	col = mix(col, c_hot.rgb, smoothstep(0.9, 1.3, e));
	float a = clamp(shape * (0.35 + 0.65 * smoothstep(0.1, 0.5, e)) + rim * reveal * 0.6 * outline, 0.0, 1.0);
	// 正对脸的中间开一扇「窗」：越靠近面具中心越透，脸和头发从火里透出来
	float win = smoothstep(0.62, 0.12, length(p * vec2(1.0, 1.35)));
	a *= 1.0 - 0.6 * win * window;
	COLOR = vec4(col * glow, a * alpha);
}" };

	private readonly Control _layer, _back;
	private readonly Node2D _root, _halo, _rear;
	private readonly ColorRect _fire, _fireRear, _haze;
	private readonly GpuParticles2D[] _particles;
	private readonly float _half, _w, _h;
	private bool _ended;

	private static void Set(ColorRect r, string n, Variant v) => ((ShaderMaterial)r.Material).SetShaderParameter(n, v);

	private ColorRect Layer(Node2D parent, float w, float h, float facing, float cy)
	{
		ColorRect r = new()
		{
			Size = new Vector2(w, h), Position = new Vector2(-w / 2f, -h * cy), PivotOffset = new Vector2(w / 2f, h * cy),
			Scale = new Vector2(facing, 1f), MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Shader },
		};
		parent.AddChild(r);
		Fx.Clock(r);
		Set(r, "kx", 2.8f);
		Set(r, "ky", 2.2f);
		Set(r, "cy", cy);
		Set(r, "seed", Fx.Rand(0f, 10f));
		Set(r, "ignite", 0f);
		Set(r, "c_dark", Dark);
		Set(r, "c_mid", Mid);
		Set(r, "c_hi", Hi);
		Set(r, "c_hot", Hot);
		return r;
	}

	/// <param name="center">面具中心（屏幕，约在 Joker 眼睛的高度）</param>
	/// <param name="halfWidth">面具半宽（屏幕像素）</param>
	/// <param name="roll">跟着头的倾斜转一点（弧度）</param>
	public PersonaMask(Control back, Control front, Vector2 center, float halfWidth, float facing = 1f, float roll = 0f)
	{
		_layer = front;
		_back = back;
		_half = halfWidth;
		const float cy = 0.66f;
		float w = halfWidth * 2.8f, h = halfWidth * 2.2f;
		_w = w;
		_h = h;
		// 托底的蓝光 + 后面一层更大、更暗、烧得更慢的蓝焰（衬出纵深，在人物后面）
		_halo = Fx.Root(back, center, "P5PersonaMaskHalo", 600);
		Sprite2D halo = Glow.SoftSprite(_halo, new Vector2(0f, -halfWidth * 0.2f), new Color(Mid, 0.4f), 1f);
		halo.Scale = new Vector2(w / 32f * 0.85f, h / 32f * 0.75f);
		_rear = Fx.Root(back, center, "P5PersonaMaskRear", 600);
		_rear.Rotation = roll;
		_fireRear = Layer(_rear, w * 1.3f, h * 1.3f, facing, cy);
		Set(_fireRear, "glow", 0.7f);
		Set(_fireRear, "alpha", 0.75f);
		Set(_fireRear, "window", 0f);
		Set(_fireRear, "outline", 0f);                           // 后面那层只要火舌，不画面具本身（不然是一圈重影）
		// 面具戴在脸上（人物前面）
		_root = Fx.Root(front, center, "P5PersonaMask", 600);
		_root.Rotation = roll;
		_fire = Layer(_root, w, h, facing, cy);
		// 面具上方的热浪
		_haze = CardFx.HazeRect(_root, new Vector2(0f, -h * 0.45f), w * 0.9f, h * 0.7f, 0.004f);
		// 从中间往两边点燃，火舌随后长出来；烧满的那一刻整体亮一下、往外一胀
		foreach (ColorRect r in new[] { _fire, _fireRear })
		{
			ColorRect f = r;
			float baseGlow = f == _fire ? 1f : 0.7f;
			Fx.Animate(f, f == _fire ? 0.05 : 0.1, 0.4, t =>
			{
				Set(f, "ignite", Fx.OutCubic(Mathf.Min(1f, t * 1.15f)));
				Set(f, "flame", 0.3f + 0.7f * Ease5.OutBack(t));
				float flare = Mathf.Exp(-Mathf.Pow((t - 0.9f) / 0.1f, 2f));
				Set(f, "glow", baseGlow * (1f + 0.7f * flare));
				f.Scale = new Vector2(Mathf.Sign(f.Scale.X), 1f) * (1f + 0.07f * flare);
			});
			// 之后火势随呼吸起伏
			float ph = Fx.Rand(0f, 1f);
			Tween breathe = f.CreateTween().SetLoops();
			breathe.TweenInterval(0.5);
			breathe.TweenMethod(Callable.From<float>(t =>
			{
				if (!_ended)
				{
					Set(f, "glow", baseGlow * (1f + 0.12f * Mathf.Sin((t + ph) * Mathf.Tau)));
					Set(f, "flame", 1f + 0.12f * Mathf.Sin((t * 2f + ph) * Mathf.Tau));
				}
			}), 0f, 1f, 1.4);
		}
		// 余烬：往上飘的火星（后）、绕着面具打旋的细火星（前）、慢慢飘的大块柔光余烬（后）
		Vector2 top = center + new Vector2(0f, -halfWidth * 0.25f);
		_particles = new[]
		{
			Drift(back, top, new Vector2(halfWidth * 0.9f, halfWidth * 0.3f), 40, 1.1f, 90f, 230f, 0.08f, 0.26f, 0.15f, new[] { Hot, Hi, Mid }),
			Drift(front, center, new Vector2(halfWidth * 1.05f, halfWidth * 0.45f), 28, 1.5f, 15f, 70f, 0.05f, 0.14f, 0.6f, new[] { Hot, Hi }),
			Drift(back, top, new Vector2(halfWidth * 1.1f, halfWidth * 0.4f), 6, 2.2f, 20f, 60f, 0.5f, 0.95f, 0.3f,
				new[] { new Color(Hi, 0.55f), new Color(Mid, 0.4f) }),
		};
		// 面具亮边上隔一会儿闪一点星光
		Tween glints = _root.CreateTween().SetLoops();
		glints.TweenInterval(0.45);
		glints.TweenCallback(Callable.From(() =>
		{
			if (!_ended)
			{
				float side = Fx.Rng.NextDouble() < 0.5 ? -1f : 1f;
				Star(_root, new Vector2(side * halfWidth * Fx.Rand(0.35f, 0.95f), -halfWidth * Fx.Rand(0.05f, 0.4f)), Fx.Rand(0.7f, 1.2f));
			}
		}));
	}

	/// <summary>一直飘的粒子（湍流，柔光贴图，加法）。extents = 发射区域半宽高；turb = 湍流的影响（0 = 直线，越大越乱）。</summary>
	private static GpuParticles2D Drift(Node parent, Vector2 at, Vector2 extents, int rate, float life, float vMin, float vMax,
		float sMin, float sMax, float turb, Color[] ramp)
	{
		var offsets = new float[ramp.Length + 2];
		var colors = new Color[ramp.Length + 2];
		offsets[0] = 0f;
		colors[0] = new Color(ramp[0], 0f);
		for (int i = 0; i < ramp.Length; i++)
		{
			offsets[i + 1] = 0.15f + 0.65f * i / Mathf.Max(1, ramp.Length - 1);
			colors[i + 1] = ramp[i];
		}
		offsets[^1] = 1f;
		colors[^1] = new Color(ramp[^1], 0f);
		ParticleProcessMaterial pm = new()
		{
			Direction = new Vector3(0f, -1f, 0f), Spread = 40f, InitialVelocityMin = vMin, InitialVelocityMax = vMax,
			Gravity = new Vector3(0f, -20f, 0f), DampingMin = 0.5f, DampingMax = 1.5f, ScaleMin = sMin, ScaleMax = sMax,
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box, EmissionBoxExtents = new Vector3(extents.X, extents.Y, 0f),
			TurbulenceEnabled = turb > 0f, TurbulenceNoiseStrength = 4f, TurbulenceNoiseScale = 2.5f, TurbulenceNoiseSpeed = new Vector3(0.3f, 0.3f, 0f),
			TurbulenceInfluenceMin = turb * 0.5f, TurbulenceInfluenceMax = turb,
			ColorRamp = new GradientTexture1D { Gradient = new Gradient { Offsets = offsets, Colors = colors } },
		};
		GpuParticles2D p = new()
		{
			Amount = Mathf.Max(2, (int)(rate * life)), Lifetime = life, ProcessMaterial = pm, Texture = Glow.Soft, Material = Fx.Additive,
			LocalCoords = false, Emitting = true, Position = at,
		};
		parent.AddChild(p);
		return p;
	}

	/// <summary>蓝色四角星光，一闪即逝。</summary>
	private static void Star(Node2D parent, Vector2 at, float size)
	{
		Node2D n = new() { Position = at };
		parent.AddChild(n);
		Sprite2D halo = Glow.SoftSprite(n, Vector2.Zero, Hi, 1.6f * size);
		Polygon2D star = Fx.Poly(n, Glow.Star4(20f * size, 0.14f), Hot, additive: true);
		Fx.Animate(n, 0, 0.3, t =>
		{
			float k = Mathf.Sin(Mathf.Min(t * 1.5f, 1f) * Mathf.Pi);
			n.Scale = Vector2.One * k;
			star.Rotation = t * 0.8f;
		});
		Fx.FreeAfter(n, 0.35);
	}

	/// <summary>打出：一亮、往外一胀、火舌窜高，蓝色火星炸开，0.4s 散掉。放回：往中间缩掉。</summary>
	public void End(bool played)
	{
		if (_ended || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}
		_ended = true;
		foreach (GpuParticles2D p in _particles)
		{
			if (GodotObject.IsInstanceValid(p))
			{
				p.Emitting = false;
				Fx.FreeAfter(p, 2.4);
			}
		}
		Vector2 at = _root.GlobalPosition;
		if (played)
		{
			Glow.Flash(_back, at, Hi, _half * 3.2f, 0.45f);
			Glow.Sparks(_layer, at, 60, 220f, 820f, 0.75f, new[] { Hot, Hi, Mid }, -120f, 0.1f, 0.4f);
			Glow.Sparks(_back, at, 30, 80f, 300f, 1.1f, new[] { Hi, Mid }, -200f, 0.3f, 0.6f);
			Fx.Shake(1);
		}
		float dur = played ? 0.45f : 0.3f;
		foreach (ColorRect r in new[] { _fire, _fireRear })
		{
			ColorRect f = r;
			float a0 = f == _fire ? 1f : 0.75f;
			Fx.Animate(f, 0, dur, t =>
			{
				if (played)
				{
					f.Scale = new Vector2(Mathf.Sign(f.Scale.X), 1f) * (1f + 0.35f * Ease5.OutExpo(t));
					Set(f, "flame", 1f + 0.9f * Ease5.OutExpo(t));
					Set(f, "glow", 1f + 0.9f * (1f - t));
					Set(f, "alpha", a0 * (1f - Fx.InCubic(t)));
				}
				else
				{
					Set(f, "ignite", 1f - Fx.OutCubic(t));
					Set(f, "alpha", a0 * (1f - t));
				}
			});
		}
		Set(_haze, "alpha", 0f);
		Tween fade = _halo.CreateTween();
		fade.TweenProperty(_halo, "modulate:a", 0f, dur);
		foreach (Node2D n in new[] { _root, _halo, _rear })
		{
			Fx.FreeAfter(n, dur + 0.1);
		}
	}
}
