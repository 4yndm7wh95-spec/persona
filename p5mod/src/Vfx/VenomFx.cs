using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 致命毒药、蛇咬（用户 2026-11-10：这两张白卡原来和别的毒牌走同一套「光束 + 脚下咒火」，没有自己的样子；白卡，不要复杂）。
/// 各有一个一眼认得出的动作，再共用一个「中毒了」的收尾——毒脉络：
/// · 致命毒药（1 费，打得最频繁，约 0.6s）：亚森爪尖弹出一滴黑红的咒怨，落在敌人胸口，黑红的细脉络从落点沿着身体往外爬，像心跳一样搏动一下，沉进去；
/// · 蛇咬（2 费 7 层，约 0.9s）：不画蛇——敌人上下各凝出一排咒火凝成的尖刺，一合咬住（顿帧、震屏），留下两个咬痕，脉络从咬痕往外爬，更粗更远。
/// 全部着色器画；脉络没法贴着敌人的轮廓（游戏里读不到敌人的画面），按敌人的大小画在身体范围内、边缘淡出。
/// 入口：JokerFx.OnPlay 记下这张牌（Arm），游戏上毒那一刻（PoisonFx.Apply）转过来。
/// </summary>
public static class VenomFx
{
	public enum Kind { None, Drop, Bite }

	private static Kind _next;
	private static ulong _until;

	/// <summary>Joker 打出致命毒药 / 蛇咬：接下来 3 秒里的第一次上毒走这张牌自己的特效。</summary>
	public static void Arm(Kind kind)
	{
		_next = kind;
		_until = Stage.NowMs() + 3000;
	}

	/// <summary>蛇咬刚打出：原版的獠牙贴图（vfx/vfx_bite）不要。</summary>
	public static bool HidesVanillaBite => _next == Kind.Bite && Stage.NowMs() < _until;

	/// <summary>取走记下的牌（只用一次）。</summary>
	public static Kind Take()
	{
		Kind k = Stage.NowMs() < _until ? _next : Kind.None;
		_next = Kind.None;
		return k;
	}

	private const string Noise = @"
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 4; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; }
	return v;
}
";

	private static Shader? _vein, _drop, _jaw;

	/// <summary>
	/// 毒脉络：从一个（或两个）起点往外爬的细脉——噪声的等值线（绕着起点取样，所以是一股股往外走、会分叉合并的线，不是直的放射线），
	/// 越往外越细，远处再长出更细的毛细脉；芯是压暗的黑红（premul 的 alpha = 真的压暗），两侧一层绯红的光。
	/// grow = 现在爬到多远（像素，前沿按角度参差不齐）；pulse = 搏动的光走到多远；fade 1→0 沉进身体（按噪声一段段灭掉）。
	/// </summary>
	private static Shader VeinShader => _vein ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec2 size_px = vec2(300.0, 400.0);
uniform vec2 o1 = vec2(0.0);
uniform vec2 o2 = vec2(0.0);
uniform float two = 0.0;
uniform float grow = 0.0;
uniform float pulse = -100.0;
uniform float fade = 1.0;
uniform float thick = 1.0;
uniform float seed = 0.0;
" + Noise + @"
// 返回 (线, 光, 到起点的距离)
vec3 veins(vec2 p, vec2 o, float sd) {
	vec2 q = p - o;
	float r = length(q) + 0.001;
	vec2 c = q / r;
	c = normalize(c + (vec2(fbm(q * 0.011 + sd), fbm(q * 0.011 + sd + 7.3)) - 0.5) * 0.9);     // 走向被噪声带歪
	float n1 = noise(c * 1.7 + vec2(sd * 3.1, sd) + r * 0.0032);
	float n2 = noise(c * 4.6 + vec2(sd, sd * 5.7) + r * 0.007);
	// 到等值线的像素距离 = 差值 / 屏幕上的梯度：线到哪里都一样细，噪声平的地方不会鼓成一块
	float d1 = abs(n1 - 0.5) / max(length(vec2(dFdx(n1), dFdy(n1))), 0.0008);
	float d2 = abs(n2 - 0.5) / max(length(vec2(dFdx(n2), dFdy(n2))), 0.0016);
	float edge = grow * (0.62 + 0.38 * noise(c * 2.6 + sd * 1.3));                               // 前沿参差
	float reach = 1.0 - smoothstep(edge - 14.0, edge, r);
	float w = thick * mix(2.1, 0.65, smoothstep(0.0, max(grow, 1.0), r));
	float near = smoothstep(6.0, 40.0, r);                             // 起点附近所有线挤在一起：压住，不然糊成一团
	float l1 = 1.0 - smoothstep(w * 0.5, w * 1.2, d1);
	float l2 = (1.0 - smoothstep(w * 0.22, w * 0.6, d2)) * smoothstep(edge * 0.3, edge * 0.55, r) * 0.7;
	float line = max(l1, l2) * reach * near;
	float glow = (exp(-d1 / (w * 2.2)) + 0.4 * exp(-d2 / (w * 1.6)) * smoothstep(edge * 0.3, edge * 0.55, r)) * reach * near;
	return vec3(line, glow, r);
}
void fragment() {
	vec2 p = (UV - 0.5) * size_px;
	vec3 v = veins(p, o1, seed);
	if (two > 0.5) {
		vec3 b = veins(p, o2, seed + 4.7);
		v = vec3(max(v.x, b.x), max(v.y, b.y), min(v.z, b.z));
	}
	// 身体范围：椭圆，边缘淡出
	float body = 1.0 - smoothstep(0.72, 1.0, length(p / (size_px * 0.5)));
	// 沉进去：按噪声一段段灭
	float gone = smoothstep(fade - 0.15, fade + 0.15, fbm(p * 0.02 + seed));
	float k = body * (1.0 - gone) * smoothstep(0.0, 0.08, fade);
	float beat = exp(-pow((v.z - pulse) / 34.0, 2.0));
	// 起点的一小块墨渍
	float blot = (1.0 - smoothstep(5.0 * thick, 12.0 * thick, v.z + (noise(p * 0.2 + seed) - 0.5) * 8.0)) * step(1.0, grow);
	vec3 crimson = vec3(0.9, 0.06, 0.1), hot = vec3(1.0, 0.72, 0.68);
	float line = max(v.x, blot);
	vec3 light = crimson * (v.y * 0.2 + line * 0.16) + mix(crimson, hot, 0.3) * beat * (line * 1.1 + v.y * 0.3);
	COLOR = vec4(light * k, line * 0.9 * k);
}" };

	/// <summary>
	/// 脉络爬上身体。at = 敌人中心，half = 半高，width = 宽度；o1 / o2 = 起点（相对中心，o2 为空就只有一个）。
	/// reach = 爬多远（像素），growTime 秒爬完，beatAt 搏动一下，之后 sinkTime 秒沉进去。
	/// </summary>
	private static void Veins(Control layer, Vector2 at, float half, float width, Vector2 o1, Vector2? o2, float reach, float thick,
		float growTime, float beatAt, float sinkTime)
	{
		Vector2 size = new(Mathf.Max(width * 1.25f, 170f), Mathf.Max(half * 2.5f, 220f));
		float life = beatAt + 0.3f + sinkTime;
		Node2D root = Fx.Root(layer, at, "P5Veins", life + 0.1);
		ShaderMaterial m = new() { Shader = VeinShader };
		m.SetShaderParameter("size_px", size);
		m.SetShaderParameter("o1", o1);
		m.SetShaderParameter("o2", o2 ?? Vector2.Zero);
		m.SetShaderParameter("two", o2 != null ? 1f : 0f);
		m.SetShaderParameter("thick", thick);
		m.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		ColorRect q = new() { Size = size, Position = -size / 2f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		Fx.Animate(root, 0, life, t =>
		{
			float time = t * life;
			m.SetShaderParameter("grow", reach * Ease5.OutQuint(Mathf.Clamp(time / growTime, 0f, 1f)));
			float b = time - beatAt;
			m.SetShaderParameter("pulse", b < 0f ? -200f : b / 0.26f * (reach + 80f));
			float s = time - (beatAt + 0.3f);
			m.SetShaderParameter("fade", s < 0f ? 1f : 1f - s / sinkTime);
		});
	}

	// ───────────────────────── 致命毒药：一滴咒怨 ─────────────────────────

	/// <summary>飞着的那一滴：黑芯、绯红的边，往后拖一条被噪声撕开的细尾（x 轴 = 飞行方向，头在右边）。</summary>
	private static Shader DropShader => _drop ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float clock = 0.0;
uniform float seed = 0.0;
" + Noise + @"
void fragment() {
	vec2 p = (UV - vec2(0.82, 0.5)) * vec2(5.0, 2.0);               // 头在 (0,0)，尾巴往 -x 伸
	float head = length(p * vec2(1.0, 1.25));
	float tail_w = 0.34 * (1.0 + p.x * 0.24) * (0.75 + 0.5 * noise(vec2(p.x * 3.0 - clock * 14.0, seed)));
	float tail = step(p.x, 0.0) * (1.0 - smoothstep(tail_w * 0.5, tail_w, abs(p.y + (noise(vec2(p.x * 2.0 - clock * 9.0, seed + 3.0)) - 0.5) * 0.25 * -p.x)));
	tail *= smoothstep(-4.0, -0.6, p.x);
	float core = max(1.0 - smoothstep(0.26, 0.36, head), tail * 0.8);
	float rim = exp(-pow((head - 0.36) / 0.12, 2.0)) + tail * 0.5;
	float glow = exp(-head * 2.2) * 0.6;
	vec3 crimson = vec3(0.95, 0.07, 0.11);
	COLOR = vec4(crimson * (rim * 0.9 + glow) + vec3(1.0, 0.7, 0.66) * exp(-pow((head - 0.33) / 0.05, 2.0)) * 0.5, core * 0.9);
}" };

	/// <summary>
	/// 致命毒药。from = 亚森的爪尖（没有召唤就为空：直接落在身上）；at / top / width = 敌人中心、头顶、宽度。
	/// 一滴 0.14s 划一道小弧线飞到胸口 → 「嗒」地渗进去（一圈很小的环、几点溅起来的咒火星）→ 脉络爬开 → 心跳一下 → 沉进去。
	/// </summary>
	public static void Drop(Control layer, Vector2? from, Vector2 at, Vector2 top, float width, Node2D? body)
	{
		float half = Mathf.Max(80f, (at - top).Length());
		Vector2 chest = at + new Vector2(0f, -half * 0.18f);
		float fly = 0f;
		if (from is Vector2 a && a.DistanceTo(chest) > 60f)
		{
			fly = 0.14f;
			Vector2 mid = (a + chest) / 2f + new Vector2(0f, -Mathf.Min(120f, a.DistanceTo(chest) * 0.16f));
			Node2D drop = Fx.Root(layer, a, "P5VenomDrop", fly + 0.02);
			ShaderMaterial dm = new() { Shader = DropShader };
			dm.SetShaderParameter("seed", Fx.Rand(0f, 30f));
			Vector2 ds = new(150f, 60f);
			ColorRect dq = new() { Size = ds, Position = new Vector2(-ds.X * 0.82f, -ds.Y / 2f), Material = dm, MouseFilter = Control.MouseFilterEnum.Ignore };
			drop.AddChild(dq);
			Fx.Clock(dq);
			Vector2 last = a;
			Fx.Animate(drop, 0, fly, t =>
			{
				float e = t * t;                                              // 越飞越快
				Vector2 pos = a.Lerp(mid, e).Lerp(mid.Lerp(chest, e), e);
				if (pos.DistanceSquaredTo(last) > 0.5f) drop.Rotation = (pos - last).Angle();
				last = pos;
				drop.GlobalPosition = pos;
			});
		}
		Fx.After(layer, fly, () =>
		{
			Sfx.Play(Sfx.VenomDrop, 0.75f);
			Stage.ShakeDir?.Invoke(1, 20f);
			Glow.Flash(layer, chest, new Color(1f, 0.2f, 0.25f), width * 0.7f, 0.14f);
			Glow.Shockwave(layer, chest, new Color(1f, 0.12f, 0.18f), width * 0.75f, 0.22f);
			Vanilla.Burst(layer, Vanilla.Specks, chest, -Mathf.Pi / 2f, 0.6f, Lut5.Curse, amount: 8);
			BodyTint.Pulse(body, new Color(1.5f, 0.75f, 0.78f), 0.03, 0.08);
			Veins(layer, at, half, width, chest - at, null, Mathf.Max(half * 1.15f, width * 0.6f), 1f, 0.24f, 0.27f, 0.3f);
			// 心跳那一下：身体暗一下（毒进去了）
			Fx.After(layer, 0.27, () => BodyTint.Pulse(body, new Color(0.7f, 0.42f, 0.5f), 0.1, 0.4));
		});
	}

	// ───────────────────────── 蛇咬：咒怨之颚 ─────────────────────────

	/// <summary>
	/// 咒怨之颚（用户 2026-11-10：平面形状填颜色怎么柔化都像贴图，要毒性爆发那只巨手的感觉——没有明确轮廓、纯靠渲染）。
	/// 和 CrushHand 同一套做法：着色器里光线步进一副 3D 的颚——上下各一道马蹄形的牙床（两侧的牙比中间的远）、两根往里弯的长毒牙、几根小刺，
	/// 全是咒烟：密度 = 距离场被流动的 3D 噪声撕毛；主体近乎纯黑，只有最外一层薄壳透红光（擦着边看穿过的壳长 → 边缘自己亮起来，不是描边），
	/// 第一次进到烟里按形状打一次光（主光 + 轮廓光 + 凹处压暗）→ 牙是圆的、有前后；四周一缕缕往上飘的黑烟，背后一圈暗红。
	/// 世界坐标：镜头在原点朝 +z 看，颚的中心在 (0,0,z0)。yu / yl = 上下牙床的高度，tu / tl = 牙床往镜头这边倒多少（张着时毒牙尖朝镜头，合上时摆正）。
	/// </summary>
	private static Shader JawShader => _jaw ??= new Shader { Code = @"
shader_type canvas_item;
uniform float clock = 0.0;
uniform float seed = 0.0;
uniform float aspect = 0.6;
uniform float focal = 2.2;
uniform float z0 = 6.0;
uniform float jaw_r = 0.9;      // 马蹄形牙床的半径
uniform float yu = 1.9;
uniform float yl = -1.9;
uniform float tu = -0.45;
uniform float tl = -0.3;
uniform float hot = 0.0;        // 咬合那一下：咬住的地方透出红光
uniform float dissolve = 0.0;   // 散成咒烟
uniform float alpha = 1.0;

float sd_tcap(vec3 p, vec3 a, vec3 b, float ra, float rb) {
	vec3 pa = p - a, ba = b - a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(pa - ba * h) - mix(ra, rb, h);
}
float smin(float a, float b, float k) {
	float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
	return mix(b, a, h) - k * h * (1.0 - h);
}
vec3 arc(float th, float r) { return vec3(r * sin(th), 0.0, r * (1.0 - cos(th))); }
// 一副牙床（自己的坐标：牙床在 y=0，牙朝 -y 长，左右对称）。L = 毒牙多长，tf / ts = 毒牙、小刺在马蹄上的角度
float jaw(vec3 q, float r, float L, float tf, float ts, float small) {
	q.x = abs(q.x);
	vec3 b = arc(tf, r);
	vec3 j1 = b + vec3(-0.03, -0.42 * L, -0.11 * L);                 // 中段往镜头这边鼓
	vec3 j2 = b + vec3(-0.09, -0.78 * L, -0.08 * L);
	vec3 tip = b + vec3(-0.17, -1.0 * L, 0.05 * L);                  // 尖往里、往回勾
	float d = sd_tcap(q, b + vec3(0.0, 0.1, 0.0), j1, 0.2, 0.135);
	d = smin(d, sd_tcap(q, j1, j2, 0.135, 0.07), 0.04);
	d = smin(d, sd_tcap(q, j2, tip, 0.07, 0.006), 0.03);
	vec3 s1 = arc(ts, r);
	d = min(d, sd_tcap(q, s1 + vec3(0.0, 0.04, 0.0), s1 + vec3(-0.04, -small, 0.0), 0.07, 0.005));
	vec3 s0 = arc(0.0, r);
	d = min(d, sd_tcap(q, s0 + vec3(0.0, 0.04, 0.0), s0 + vec3(0.0, -small * 0.8, 0.0), 0.06, 0.005));
	// 牙床：沿马蹄的一缕细烟（只是把牙连起来，不能成一根横梁）
	float g = sd_tcap(q, s0 + vec3(0.0, 0.1, 0.0), b + vec3(0.0, 0.14, 0.0), 0.05, 0.09);
	g = smin(g, sd_tcap(q, b + vec3(0.0, 0.14, 0.0), s1 + vec3(0.0, 0.08, 0.0), 0.09, 0.04), 0.05);
	return smin(d, g, 0.07);
}
float map(vec3 pw) {
	vec3 p = pw - vec3(0.0, 0.0, z0);
	vec3 q = p - vec3(0.0, yu, 0.0);
	float c = cos(tu), s = sin(tu);
	q.yz = vec2(c * q.y - s * q.z, s * q.y + c * q.z);
	float d = jaw(q, jaw_r, 1.3, 0.62, 1.2, 0.26);
	q = p - vec3(0.0, yl, 0.0);
	q.y = -q.y;
	c = cos(tl); s = sin(tl);
	q.yz = vec2(c * q.y - s * q.z, s * q.y + c * q.z);
	return min(d, jaw(q, jaw_r * 0.84, 0.8, 0.36, 1.0, 0.2));
}
float hash3(vec3 p) { p = fract(p * 0.3183099 + 0.1); p *= 17.0; return fract(p.x * p.y * p.z * (p.x + p.y + p.z)); }
float vnoise(vec3 x) {
	vec3 i = floor(x), f = fract(x); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash3(i), hash3(i + vec3(1, 0, 0)), f.x), mix(hash3(i + vec3(0, 1, 0)), hash3(i + vec3(1, 1, 0)), f.x), f.y),
		mix(mix(hash3(i + vec3(0, 0, 1)), hash3(i + vec3(1, 0, 1)), f.x), mix(hash3(i + vec3(0, 1, 1)), hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}
float fbm(vec3 x) { return vnoise(x) * 0.62 + vnoise(x * 2.3 + 7.1) * 0.38; }
vec3 normal_at(vec3 p, float e) {
	vec2 k = vec2(1.0, -1.0);
	return normalize(k.xyy * map(p + k.xyy * e) + k.yyx * map(p + k.yyx * e) + k.yxy * map(p + k.yxy * e) + k.xxx * map(p + k.xxx * e));
}
float enter(vec3 rd, float shell, out float m) {
	float t = z0 - 2.6; m = 1e3;
	for (int i = 0; i < 56; i++) {
		float d = map(rd * t);
		m = min(m, d / t);
		if (d < shell) return t;
		t += d * 0.9;
		if (t > z0 + 3.0) break;
	}
	return -1.0;
}
void fragment() {
	if (alpha <= 0.0) { discard; }
	vec2 uv = (UV * 2.0 - 1.0) * vec2(aspect, -1.0);
	vec3 rd = normalize(vec3(uv, focal));
	float m;
	float t = enter(rd, 0.3, m);
	vec3 col = vec3(0.0);
	float T = 1.0;
	if (t > 0.0) {
		float dt = 0.026;
		bool shaded = false;
		t += fract(sin(dot(UV * 1000.0, vec2(12.9898, 78.233)) + clock) * 43758.5453) * dt;
		for (int i = 0; i < 56; i++) {
			vec3 pw = rd * t;
			if (pw.z > z0 + 2.6 || T < 0.02) break;
			vec3 lp = pw - vec3(0.0, 0.0, z0) + seed;
			float s = map(pw);
			if (s > 0.32) { t += s - 0.28; continue; }
			float stepv = s > 0.06 ? 0.05 : dt;
			// 四周的咒烟丝：被往上飘的噪声撕成一缕一缕
			float wn = fbm(lp * vec3(2.6, 1.4, 2.6) + vec3(0.0, -clock * 1.5, clock * 0.2));
			float aura = smoothstep(0.3, 0.02, s) * smoothstep(0.5, 0.72, wn) * step(0.0, s);
			if (aura > 0.001) {
				float aa = 1.0 - exp(-aura * stepv * 9.0);
				col += T * (aa * vec3(0.025, 0.0, 0.008) + vec3(0.5, 0.025, 0.055) * aura * smoothstep(0.62, 0.8, wn) * stepv * 1.2);
				T *= 1.0 - aa;
			}
			float nz = fbm(lp * 5.0 + vec3(0.0, -clock * 1.6, clock * 0.3));
			float sd = s + (nz - 0.5) * 0.11 + dissolve * (0.2 + (nz - 0.5) * 0.7);
			float dens = smoothstep(0.035, -0.045, sd);
			if (!shaded && dens > 0.25) {
				shaded = true;
				vec3 n = normal_at(pw, 0.004 * t);
				float ao = clamp(map(pw + n * 0.06) / 0.06, 0.0, 1.0);
				ao *= clamp(0.4 + map(pw + n * 0.15) / 0.15 * 0.6, 0.0, 1.0);
				float key = smoothstep(0.05, 0.75, dot(n, normalize(vec3(-0.45, 0.75, -0.5))));
				float fres = 1.0 - abs(dot(n, rd));
				vec3 light = vec3(0.2, 0.014, 0.03) * key * (0.6 + 0.4 * nz) + vec3(1.0, 0.34, 0.28) * smoothstep(0.5, 0.86, fres) * 1.9;
				col += T * light * ao * 0.9 * (1.0 + 0.8 * hot);
				T *= 1.0 - 0.6 * dens;
			}
			if (dens > 0.001) {
				float inner = max(-sd, 0.0);
				float shellGlow = exp(-inner * 75.0);
				float flick = fbm(lp * 4.0 + vec3(0.0, -clock * 2.4, 0.0));
				vec3 emit = mix(vec3(0.5, 0.03, 0.05), vec3(1.0, 0.32, 0.24), smoothstep(0.5, 0.8, flick)) * shellGlow * 0.95;
				float vn = fbm(lp * 2.6 + vec3(0.0, -clock * 0.4, 0.0));
				emit += vec3(0.7, 0.03, 0.07) * (1.0 - smoothstep(0.0, 0.035, abs(vn - 0.5))) * exp(-inner * 28.0) * 0.6;
				// 咬住的地方：一团红光从牙缝里透出来
				emit += vec3(1.0, 0.3, 0.22) * hot * exp(-length(pw - vec3(0.0, 0.0, z0)) * 2.2) * 2.4;
				float a = 1.0 - exp(-dens * dt * 130.0);
				col += T * (a * vec3(0.004, 0.0, 0.002) + emit * dens * dt * 17.0);
				T *= 1.0 - a;
			}
			t += stepv;
		}
	}
	vec4 acc = vec4(col, 1.0 - T);
	// 背后一圈暗红：把颚从背景上托出来（很淡）
	float glow = exp(-m * focal / 0.05) * 0.22;
	acc.rgb += (1.0 - acc.a) * vec3(0.75, 0.04, 0.07) * glow;
	acc.a += (1.0 - acc.a) * glow;
	float edge = 1.0 - smoothstep(0.82, 1.0, max(abs(UV.x - 0.5), abs(UV.y - 0.5)) * 2.0);
	float a = clamp(acc.a, 0.0, 1.0);
	COLOR = vec4(acc.rgb / max(a, 0.001), a * alpha * edge);
}" };

	/// <summary>蛇咬里从打出到咬合的时间（秒）：颚先张着凝出来、逼近，再一下合上。</summary>
	public const float SnapAfter = 0.19f;

	/// <summary>
	/// 蛇咬。at / top / width = 敌人中心、头顶、宽度。颚凝出来、慢慢逼近（0.15s）→ 0.04s 合上 = 咬住：顿帧、上下震屏、两个咬痕亮一下、
	/// 身体先提亮再染暗 → 颚烧散，脉络从两个咬痕往外爬（比致命毒药粗、远），心跳一下，沉进去。
	/// </summary>
	public static void Bite(Control layer, Vector2 at, Vector2 top, float width, Node2D? body)
	{
		float half = Mathf.Max(80f, (at - top).Length());
		float halfw = Mathf.Clamp(width * 0.6f, 105f, 250f);
		float big = Mathf.Clamp(half * 0.82f, 95f, 210f);
		// 画布：高 = 颚张到最大时的范围；着色器里 1 个单位 ≈ big 像素（镜头 focal 2.2、颚在 z0 = 6）
		const float focal = 2.2f, z0 = 6f;
		Vector2 size = new(halfw * 3.4f, big * 5.4f);
		float unit = size.Y / 2f / (z0 / focal);
		float jawR = halfw * 1.0f / unit;
		const float hold = 0.12f, burn = 0.3f;
		float life = SnapAfter + hold + burn;
		Node2D root = Fx.Root(layer, at, "P5VenomJaw", life + 0.1);
		ShaderMaterial m = new() { Shader = JawShader };
		m.SetShaderParameter("aspect", size.X / size.Y);
		m.SetShaderParameter("focal", focal);
		m.SetShaderParameter("z0", z0);
		m.SetShaderParameter("jaw_r", jawR);
		m.SetShaderParameter("seed", Fx.Rand(0f, 30f));
		ColorRect q = new() { Size = size, Position = -size / 2f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		Fx.Clock(q);
		Sfx.Play(Sfx.VenomBite, 0.85f);                               // 文件里：0.19s 的嘶声往上提 → 咔 → 嘶声散掉 → 心跳
		Fx.Animate(root, 0, life, t =>
		{
			float time = t * life;
			float approach = SnapAfter - 0.04f;
			float close = time < approach ? 0.3f * Ease5.OutQuint(time / approach)
				: time < SnapAfter ? Mathf.Lerp(0.3f, 1f, Mathf.Pow((time - approach) / 0.04f, 2f)) : 1f;
			// 逼近时轻轻发抖（憋着）
			q.Position = -size / 2f + (time < approach ? new Vector2(Fx.Rand(-1.5f, 1.5f), Fx.Rand(-1.5f, 1.5f)) : Vector2.Zero);
			m.SetShaderParameter("yu", Mathf.Lerp(2.15f, 0.95f, close));
			m.SetShaderParameter("yl", Mathf.Lerp(-1.95f, -0.62f, close));
			m.SetShaderParameter("tu", Mathf.Lerp(-0.5f, 0.04f, close));      // 张着时毒牙尖朝镜头，合上时摆正
			m.SetShaderParameter("tl", Mathf.Lerp(-0.35f, 0.02f, close));
			float after = time - SnapAfter;
			m.SetShaderParameter("hot", after < 0f ? 0f : Mathf.Exp(-after / 0.09f));
			m.SetShaderParameter("alpha", Mathf.Clamp(time / 0.07f, 0f, 1f));
			m.SetShaderParameter("dissolve", after < hold ? 0f : (after - hold) / burn * 1.3f);
		});
		Fx.After(layer, SnapAfter, () =>
		{
			HitStop.Request(0.07);
			Stage.ShakeDir?.Invoke(3, 90f);
			Glow.Flash(layer, at, new Color(1f, 0.3f, 0.34f), width * 1.5f, 0.16f);
			// 两根毒牙的尖咬进去的地方
			float fx = (jawR * Mathf.Sin(0.62f) - 0.17f) * unit;
			Vector2 m1 = new(-fx, 0.2f * unit), m2 = new(fx, 0.2f * unit);
			Glow.Glint(layer, at + m1, 0.7f);
			Glow.Glint(layer, at + m2, 0.7f, 0.03f);
			Vanilla.Burst(layer, Vanilla.Specks, at, -Mathf.Pi / 2f, 0.9f, Lut5.Curse, amount: 16);
			BodyTint.Pulse(body, new Color(1.8f, 0.7f, 0.75f), 0.04, 0.08);
			Fx.After(layer, 0.1, () => Veins(layer, at, half, width, m1, m2, Mathf.Max(half * 1.5f, width * 0.8f), 1.5f, 0.3f, 0.34f, 0.36f));
			Fx.After(layer, 0.44, () => BodyTint.Pulse(body, new Color(0.62f, 0.36f, 0.45f), 0.14, 0.5));
		});
	}
}
