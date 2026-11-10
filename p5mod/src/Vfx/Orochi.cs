using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 群蛇形态：Joker 身后的八岐大蛇 + 日蚀天色（用户 2026-11-10）。
/// 大蛇：光线步进的距离场（同毒性爆发的大手那套：只靠光照出形体，没有描边、没有贴图）。五个头露出来（前三个大、后两个小、半隐在雾里），
/// 脖子从地面的暗红雾里伸出来，看不到底；近黑带暗红光泽的鳞，背后日冕打过来的暗红逆光勾出形体，云里的雷光闪一下才看清全貌。
/// 天色：整屏压暗偏暗红，身后高处一轮黑日 + 暗红日冕，光束穿过飘动的云，贴地一层没有边界的薄红雾。
/// </summary>
public sealed class Orochi
{
	public const int Heads = 5;

	private static Shader? _bodyShader, _skyShader;

	private static Shader BodyShader => _bodyShader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec3 base_p[5];
uniform vec3 ctrl_p[5];
uniform vec3 ctrl2_p[5];
uniform vec3 head_p[5];
uniform vec3 head_f[5];
uniform vec3 head_u[5];
uniform float head_s[5];
uniform float jaw[5];
uniform float eye[5];
uniform float clock = 0.0;
uniform float lightning = 0.0;
uniform float alpha = 1.0;
uniform float aspect = 1.7778;
uniform float focal = 1.3;
uniform float ground_y = -1.0;
uniform float ground_s = -0.5;   // 蛇身没入地面的那条线（屏幕 uv.y，-1 下 → 1 上）

float hash3(vec3 p) { p = fract(p * 0.3183099 + 0.1); p *= 17.0; return fract(p.x * p.y * p.z * (p.x + p.y + p.z)); }
float vnoise(vec3 x) {
	vec3 i = floor(x), f = fract(x); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(mix(hash3(i), hash3(i + vec3(1, 0, 0)), f.x), mix(hash3(i + vec3(0, 1, 0)), hash3(i + vec3(1, 1, 0)), f.x), f.y),
		mix(mix(hash3(i + vec3(0, 0, 1)), hash3(i + vec3(1, 0, 1)), f.x), mix(hash3(i + vec3(0, 1, 1)), hash3(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}
float smin(float a, float b, float k) { float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0); return mix(b, a, h) - k * h * (1.0 - h); }
float sdEll(vec3 p, vec3 r) { float k0 = length(p / r); float k1 = length(p / (r * r)); return k0 * (k0 - 1.0) / max(k1, 1e-4); }
float sdRC(vec3 p, vec3 a, vec3 b, float r1, float r2) {
	vec3 ba = b - a; float l2 = dot(ba, ba); float rr = r1 - r2; float a2 = l2 - rr * rr; float il2 = 1.0 / l2;
	vec3 pa = p - a; float y = dot(pa, ba); float z = y - l2;
	vec3 xv = pa * l2 - ba * y; float x2 = dot(xv, xv); float y2 = y * y * l2; float z2 = z * z * l2;
	float k = sign(rr) * rr * rr * x2;
	if (sign(z) * a2 * z2 > k) return sqrt(x2 + z2) * il2 - r2;
	if (sign(y) * a2 * y2 < k) return sqrt(x2 + y2) * il2 - r1;
	return (sqrt(x2 * a2 * il2) + y * rr) * il2 - r1;
}
vec3 bez(vec3 a, vec3 b, vec3 c, vec3 d, float t) { vec3 ab = mix(a, b, t), bc = mix(b, c, t), cd = mix(c, d, t); return mix(mix(ab, bc, t), mix(bc, cd, t), t); }

// 头：局部坐标 q（x 侧、y 上、z 朝前 = 吻部方向），单位 = 头长约 1.2
float head(vec3 q, float open) {
	vec3 qs = vec3(abs(q.x), q.y, q.z);
	float d = sdEll(q - vec3(0.0, 0.02, 0.18), vec3(0.4, 0.3, 0.5));                     // 头骨
	d = smin(d, sdRC(q * vec3(1.0, 1.25, 1.0), vec3(0.0, 0.02, 0.35), vec3(0.0, -0.02, 1.15), 0.27, 0.12), 0.12);   // 上吻（扁）
	float a = open * 0.55;
	vec3 jq = q - vec3(0.0, -0.08, 0.05);
	jq = vec3(jq.x, jq.y * cos(a) + jq.z * sin(a), -jq.y * sin(a) + jq.z * cos(a));                // 下颌绕铰链往下张
	d = smin(d, sdRC(jq * vec3(1.0, 1.4, 1.0), vec3(0.0, -0.1, 0.0), vec3(0.0, -0.12, 0.95), 0.22, 0.09), 0.08);
	d = smin(d, sdEll(qs - vec3(0.22, 0.2, 0.42), vec3(0.11, 0.07, 0.32)), 0.06);       // 眉骨
	d = smin(d, sdRC(qs, vec3(0.2, 0.22, 0.05), vec3(0.42, 0.58, -0.45), 0.08, 0.018), 0.07);     // 角
	d = smin(d, sdRC(qs, vec3(0.28, 0.05, -0.05), vec3(0.5, 0.0, -0.4), 0.06, 0.015), 0.06);      // 腮边的骨刺
	// 张嘴：口腔挖空
	if (open > 0.02) {
		float mouth = sdEll(q - vec3(0.0, -0.1 - open * 0.15, 0.6), vec3(0.18, 0.06 + open * 0.16, 0.45));
		d = max(d, -mouth);
	}
	return d;
}

vec3 toHead(int i, vec3 p) {
	vec3 f = head_f[i], u = head_u[i], s = normalize(cross(u, f));
	vec3 r = (p - head_p[i]) / head_s[i];
	return vec3(dot(r, s), dot(r, u), dot(r, f));
}

// 一条蛇：脖子（贝塞尔上 8 段圆锥，越往上越细）+ 头。返回距离；part = 0 脖子 / 1 头
float serpent(int i, vec3 p, out float part) {
	float sc = head_s[i];
	float bound = length(p - ctrl_p[i]) - (length(head_p[i] - base_p[i]) * 0.75 + sc * 1.6);
	part = 0.0;
	if (bound > 0.6) return bound;
	float d = 1e3;
	vec3 prev = base_p[i];
	vec3 neckTop = head_p[i] - head_f[i] * sc * 0.15;
	for (int k = 1; k <= 10; k++) {
		float t = float(k) / 10.0;
		vec3 cur = bez(base_p[i], ctrl_p[i], ctrl2_p[i], neckTop, t);
		float t0 = float(k - 1) / 10.0;
		float r0 = mix(0.62, 0.24, pow(t0, 0.75)) * sc, r1 = mix(0.62, 0.24, pow(t, 0.75)) * sc;
		d = min(d, sdRC(p, prev, cur, r0, r1));
		prev = cur;
	}
	float h = head(toHead(i, p), jaw[i]) * sc;
	if (h < d) part = 1.0;
	return smin(d, h, 0.18 * sc);
}

float map(vec3 p, out int id, out float part) {
	float d = 1e3; id = 0; part = 0.0;
	for (int i = 0; i < 5; i++) {
		float pp;
		float di = serpent(i, p, pp);
		if (di < d) { d = di; id = i; part = pp; }
	}
	return d;
}
float mapd(vec3 p) { int id; float pt; return map(p, id, pt); }
vec3 normal_at(vec3 p, float e) {
	vec2 k = vec2(1.0, -1.0);
	return normalize(k.xyy * mapd(p + k.xyy * e) + k.yyx * mapd(p + k.yyx * e) + k.yxy * mapd(p + k.yxy * e) + k.xxx * mapd(p + k.xxx * e));
}

void fragment() {
	vec2 uv = (UV * 2.0 - 1.0) * vec2(aspect, -1.0);
	vec3 rd = normalize(vec3(uv, focal));
	float t = 2.0, m = 1e3, tm = 0.0;
	int id = 0; float part = 0.0;
	bool hit = false;
	for (int i = 0; i < 110; i++) {
		vec3 p = rd * t;
		float d = map(p, id, part);
		if (d / t < m) { m = d / t; tm = t; }
		if (d < 0.002 * t) { hit = true; break; }
		t += d * 0.85;
		if (t > 40.0) break;
	}
	vec3 col = vec3(0.0);
	float a = 0.0;
	// 地面的雾：越低越被吞掉（看不到蛇身的底）
	if (hit) {
		vec3 p = rd * t;
		vec3 n = normal_at(p, 0.0015 * t);
		float sc = head_s[id];
		vec3 lp = part > 0.5 ? toHead(id, p) : p / sc;
		// 鳞：表面的细微起伏（法线扰动，不画网格）
		vec3 sp = lp * (part > 0.5 ? 9.0 : 5.5);
		vec3 bump = vec3(vnoise(sp), vnoise(sp + 3.7), vnoise(sp + 7.3)) - 0.5;
		float cell = vnoise(sp * 1.7 + 11.0);
		n = normalize(n + bump * 0.45);
		float fres = 1.0 - abs(dot(n, rd));
		vec3 Lb = normalize(vec3(0.25, 0.45, 1.0));               // 日冕在身后
		vec3 Lf = normalize(vec3(-0.45, 0.8, -0.4));              // 微弱的前上方补光
		vec3 Ll = normalize(vec3(-0.7, 0.9, -0.35));              // 雷光
		float ao = clamp(0.35 + mapd(p + n * 0.25 * sc) / (0.25 * sc) * 0.65, 0.0, 1.0);
		float back = pow(fres, 2.5) * clamp(dot(n, Lb) * 0.6 + 0.55, 0.0, 1.0);
		float wrap = clamp((dot(n, Lf) + 0.4) / 1.4, 0.0, 1.0);
		float ltn = clamp((dot(n, Ll) + 0.2) / 1.2, 0.0, 1.0);
		vec3 albedo = mix(vec3(0.07, 0.012, 0.02), vec3(0.2, 0.035, 0.045), cell);
		float sheen = pow(clamp(dot(reflect(rd, n), Lf), 0.0, 1.0), 6.0) * smoothstep(0.4, 0.8, cell);
		col = albedo * (0.6 + 1.8 * wrap) * ao + vec3(0.55, 0.1, 0.1) * sheen * 0.35 * ao
			+ vec3(0.95, 0.16, 0.12) * back * (0.75 + 0.5 * cell) * mix(0.6, 1.0, ao)
			+ vec3(0.85, 0.82, 1.0) * ltn * lightning * (0.25 + 0.5 * cell) * ao;
		// 眼：两道发光的竖瞳；张嘴：口腔里的暗红光
		if (part > 0.5) {
			vec3 q = lp; q.x = abs(q.x);
			float ed = length((q - vec3(0.24, 0.13, 0.62)) * vec3(1.6, 0.55, 1.0));
			col += vec3(1.0, 0.18, 0.12) * exp(-ed / 0.035) * (1.5 + 2.0 * eye[id]);
			col += vec3(1.0, 0.7, 0.55) * exp(-ed / 0.012) * (0.6 + eye[id]);
			float mouthGlow = jaw[id] * exp(-length((lp - vec3(0.0, -0.18, 0.55)) * vec3(1.2, 2.0, 0.7)) * 3.5);
			col += vec3(1.0, 0.15, 0.1) * mouthGlow * 1.6;
		}
		// 远的蛇更暗、更多雾
		float haze = smoothstep(8.0, 18.0, t);
		col = mix(col, vec3(0.16, 0.02, 0.035), haze * 0.55);
		a = 1.0;
		float fogn = vnoise(vec3(p.x * 0.6 - clock * 0.2, p.y * 0.9, p.z * 0.5 + clock * 0.07));
		float fy = (uv.y - ground_s) * 7.0 + (fogn - 0.5) * 1.3;
		float low = smoothstep(1.8, 0.0, fy);
		col = mix(col, vec3(0.32, 0.03, 0.05), low * 0.75);          // 没进雾里的那截被雾照成暗红，不是一刀切黑
		a *= smoothstep(-0.25, 0.9, fy);
		col *= a;
	}
	// 剪影外一圈暗红的辉（逆光透过来的散射）：离剪影越近越亮，没有硬边
	float glow = exp(-max(m, 0.0) * focal * 90.0) * (hit ? 0.0 : 1.0);
	vec3 gp = rd * tm;
	glow *= smoothstep(0.1, 0.5, uv.y - ground_s);
	col += (1.0 - a) * vec3(0.5, 0.03, 0.04) * glow * 0.6;
	a += (1.0 - a) * glow * 0.12;
	COLOR = vec4(col * alpha, a * alpha);
}" };

	private static Shader SkyShader => _skyShader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec2 sun = vec2(0.22, 0.2);      // 日蚀在屏幕上的位置（0～1）
uniform float aspect = 1.7778;
uniform float clock = 0.0;
uniform float lightning = 0.0;
uniform float amount = 1.0;
uniform float ground = 0.84;            // 地面的高度（0～1，从上往下）
uniform vec2 pool = vec2(0.2, 0.8);     // 蛇身没入地面的地方（0～1）
uniform float pool_w = 0.25;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.02 + vec2(3.1, 7.7); a *= 0.5; } return v; }
void fragment() {
	vec2 uv = UV;
	vec2 d = (uv - sun) * vec2(aspect, 1.0);
	float r = length(d), ang = atan(d.y, d.x);
	const float R = 0.075;
	// 底色：整屏压暗偏暗红（盖在战斗背景上，不是不透明的）
	vec3 col = mix(vec3(0.16, 0.02, 0.04), vec3(0.07, 0.0, 0.02), uv.y);
	float a = 0.4;
	// 云：两层往右飘，日冕附近的云边被照红，雷光时整片云亮一下
	float c1 = fbm(vec2(uv.x * 2.2 - clock * 0.012, uv.y * 4.0));
	float c2 = fbm(vec2(uv.x * 4.5 - clock * 0.03, uv.y * 7.0 + 3.0));
	float cloud = smoothstep(0.45, 0.75, c1 * 0.7 + c2 * 0.45) * (1.0 - smoothstep(0.55, 0.8, uv.y));
	float near = exp(-max(r - R, 0.0) / 0.35);
	// 日冕
	float corona = exp(-max(r - R, 0.0) / 0.018) * step(R, r);
	float halo = exp(-max(r - R, 0.0) / 0.12);
	float rays = pow(noise(vec2(ang * 9.0, clock * 0.05)), 3.0) * exp(-max(r - R, 0.0) / 0.45) * (1.0 - cloud * 0.8) * smoothstep(ground - 0.02, ground - 0.25, uv.y);
	vec3 light = vec3(1.0, 0.85, 0.75) * corona * 1.4 + vec3(0.9, 0.08, 0.06) * (halo * 0.9 + rays * 0.55);
	light *= 1.0 - cloud * 0.6;
	col += light;
	a = max(a, clamp(corona + halo * 0.4, 0.0, 1.0));
	// 云体：暗，靠日冕和雷光照出边
	vec3 cc = vec3(0.025, 0.0, 0.008) + vec3(0.6, 0.05, 0.05) * near * smoothstep(0.3, 0.9, c2) * 0.6 + vec3(0.7, 0.3, 0.35) * lightning * smoothstep(0.35, 0.9, c2) * 0.3;
	col = mix(col, cc, cloud * 0.85);
	a = max(a, cloud * 0.8);
	// 黑日
	float disk = 1.0 - smoothstep(R - 0.003, R + 0.002, r);
	col = mix(col, vec3(0.0), disk);
	a = max(a, disk);
	// 贴地的薄红雾：没有边界，往上淡掉
	float gy = (uv.y - ground + 0.25) / 0.35;
	float mist = smoothstep(0.0, 1.0, gy) * (0.5 + 0.5 * fbm(vec2(uv.x * 3.0 - clock * 0.04, uv.y * 6.0)));
	col += vec3(0.35, 0.02, 0.04) * mist * 0.35;
	// 雷光：整屏轻轻提亮
	col += vec3(0.4, 0.12, 0.15) * lightning * 0.08;
	// 暗角
	float vig = smoothstep(0.45, 1.05, length((uv - 0.5) * vec2(aspect * 0.6, 1.0)));
	col *= 1.0 - vig * 0.3;
	a = max(a, vig * 0.3);
	// 蛇身没入的地方：一大片翻涌的暗红雾，中间亮、往四周淡开，往上飘起几缕
	vec2 pd = (uv - pool) / vec2(pool_w, pool_w * 0.35);
	float pn = fbm(vec2(uv.x * 5.0 - clock * 0.08, uv.y * 9.0 + clock * 0.05));
	float pm = exp(-dot(pd, pd) * 1.2) * (0.55 + 0.6 * pn);
	float wisp = smoothstep(0.55, 0.85, fbm(vec2(uv.x * 7.0, uv.y * 3.0 + clock * 0.25))) * exp(-pow((uv.x - pool.x) / pool_w, 2.0)) * smoothstep(pool.y + 0.05, pool.y - 0.35, uv.y) * smoothstep(pool.y - 0.5, pool.y - 0.1, uv.y);
	col += vec3(0.75, 0.08, 0.1) * pm * 0.55 + vec3(0.6, 0.05, 0.08) * wisp * 0.25;
	a = max(a, clamp(pm * 0.6, 0.0, 0.85));
	COLOR = vec4(col * amount, a * amount);
}" };

	private readonly Control _layer;
	private readonly Node2D _root;
	private readonly ColorRect _sky, _body;
	private readonly ShaderMaterial _skyM, _bodyM;
	private readonly Vector2 _feet;
	private readonly float _tall;
	private const float Focal = 1.3f;

	private readonly Vector3[] _base = new Vector3[Heads], _ctrl = new Vector3[Heads], _ctrl2 = new Vector3[Heads], _head = new Vector3[Heads], _fwd = new Vector3[Heads], _up = new Vector3[Heads];
	private readonly Vector3[] _restHead = new Vector3[Heads];
	private readonly float[] _scale = new float[Heads], _jaw = new float[Heads], _eye = new float[Heads];
	private float _clock, _lightning, _amount;

	/// <summary>每个头相对 Joker 脚底的位置（身高为单位，x 右、y 下）和深度。前三个大，后两个小、远。</summary>
	private static readonly (float X, float Y, float Z, float Size)[] Layout =
	{
		(-0.62f, -1.5f, 10f, 1f), (0.02f, -1.82f, 10.5f, 1.1f), (0.6f, -1.42f, 10f, 1f), (-1.05f, -1.15f, 14f, 0.85f), (0.95f, -1.12f, 14f, 0.85f),
	};

	public Orochi(Control back, Vector2 feet, float tall)
	{
		_layer = back;
		_feet = feet;
		_tall = tall;
		Vector2 scr = Fx.Screen(back);
		_root = Fx.ScreenRoot(back, "P5Orochi");
		_skyM = new ShaderMaterial { Shader = SkyShader };
		_sky = new ColorRect { Size = scr, Material = _skyM, MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.AddChild(_sky);
		_bodyM = new ShaderMaterial { Shader = BodyShader };
		_body = new ColorRect { Size = scr, Material = _bodyM, MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.AddChild(_body);
		float aspect = scr.X / scr.Y;
		_skyM.SetShaderParameter("aspect", aspect);
		_bodyM.SetShaderParameter("aspect", aspect);
		_bodyM.SetShaderParameter("focal", Focal);
		Vector2 feetS = Fx.ScreenPoint(back, feet);
		_skyM.SetShaderParameter("sun", new Vector2(feetS.X / scr.X + 0.03f, 0.17f));
		_skyM.SetShaderParameter("ground", feetS.Y / scr.Y);
		float tallS = tall * back.GetCanvasTransform().Scale.Y;
		for (int i = 0; i < Heads; i++)
		{
			var (x, y, z, size) = Layout[i];
			Vector3 head = World(feetS + new Vector2(x, y) * tallS, z, scr);
			Vector3 baseP = World(feetS + new Vector2(x * 0.35f, 0.35f) * tallS, z + 0.5f, scr);
			float tallW = tallS / (scr.Y * 0.5f) * z / Focal;
			_scale[i] = 0.34f * tallW * size;
			_restHead[i] = head;
			_base[i] = baseP;
			_eye[i] = 0f;
		}
		_bodyM.SetShaderParameter("ground_y", World(feetS + new Vector2(0f, 0.05f * tallS), 10f, scr).Y);
		float lineY = feetS.Y - 0.1f * tallS;
		_bodyM.SetShaderParameter("ground_s", -(lineY / scr.Y * 2f - 1f));
		_skyM.SetShaderParameter("pool", new Vector2(feetS.X / scr.X, lineY / scr.Y));
		_skyM.SetShaderParameter("pool_w", tallS * 1.3f / scr.X);
		Pose(0f);
	}

	private static Vector3 World(Vector2 screenPx, float z, Vector2 scr)
	{
		float aspect = scr.X / scr.Y;
		float ux = (screenPx.X / scr.X * 2f - 1f) * aspect, uy = -(screenPx.Y / scr.Y * 2f - 1f);
		return new Vector3(ux * z / Focal, uy * z / Focal, z);
	}

	/// <summary>闲置姿势：每个脖子各自慢慢摆（不同频率），头朝敌人那边微微低着。</summary>
	private void Pose(float time)
	{
		for (int i = 0; i < Heads; i++)
		{
			float s = _scale[i];
			Vector3 sway = new(Mathf.Sin(time * 0.6f + i * 1.7f) * 0.35f * s, Mathf.Sin(time * 0.8f + i * 2.3f) * 0.25f * s, Mathf.Cos(time * 0.5f + i) * 0.3f * s);
			_head[i] = _restHead[i] + sway;
			float side = Layout[i].X;
			Vector3 span = _head[i] - _base[i];
			float bendA = (i % 2 == 0 ? 1f : -1f) * 1.4f * s + Mathf.Sin(time * 0.5f + i) * 0.3f * s;
			_ctrl[i] = _base[i] + span * 0.33f + new Vector3(bendA + side * 0.8f * s, 0f, 1.5f * s);
			_ctrl2[i] = _base[i] + span * 0.72f + new Vector3(-bendA * 0.8f - 0.9f * s, 0.3f * s, -0.6f * s);
			float look = Mathf.Sin(time * 0.4f + i * 1.3f) * 0.2f;
			_fwd[i] = new Vector3(0.7f + look, -0.42f + 0.1f * Mathf.Sin(time * 0.7f + i), -0.6f).Normalized();
			Vector3 right = _fwd[i].Cross(Vector3.Up).Normalized();
			_up[i] = right.Cross(_fwd[i]).Normalized();
		}
		Push();
	}

	private void Push()
	{
		_bodyM.SetShaderParameter("base_p", _base);
		_bodyM.SetShaderParameter("ctrl_p", _ctrl);
		_bodyM.SetShaderParameter("ctrl2_p", _ctrl2);
		_bodyM.SetShaderParameter("head_p", _head);
		_bodyM.SetShaderParameter("head_f", _fwd);
		_bodyM.SetShaderParameter("head_u", _up);
		_bodyM.SetShaderParameter("head_s", _scale);
		_bodyM.SetShaderParameter("jaw", _jaw);
		_bodyM.SetShaderParameter("eye", _eye);
	}

	/// <summary>预览 / 调试：直接设时间、雷光、张嘴、整体显隐。</summary>
	public void Set(float time, float lightning, float amount, float jawAll = 0.15f, float eyeAll = 0.3f)
	{
		_clock = time;
		_lightning = lightning;
		_amount = amount;
		for (int i = 0; i < Heads; i++)
		{
			_jaw[i] = jawAll * (0.6f + 0.4f * Mathf.Sin(time + i));
			_eye[i] = eyeAll;
		}
		Pose(time);
		_skyM.SetShaderParameter("clock", time);
		_skyM.SetShaderParameter("lightning", lightning);
		_skyM.SetShaderParameter("amount", amount);
		_bodyM.SetShaderParameter("clock", time);
		_bodyM.SetShaderParameter("lightning", lightning);
		_bodyM.SetShaderParameter("alpha", amount);
	}

	public void Free() => _root.QueueFree();
}
