using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 群蛇形态 · 墨烟大蛇（用户 2026-11-11：八岐大蛇太艳、不像蛇、太丑太吓人，又不要太分明的形象）。
/// 第二版（用户：只看得到一个头；要五个头；要稍微围着 Joker、有一体感；蛇头上下宽窄不能一样）：
/// - 一体：一圈烟做的蛇身贴着地面绕在 Joker 脚边（前半圈在他腿前，后半圈在身后），五条脖子都从这一圈里长出来，
///   像同一条大蛇的五个头：三个在身后高处，两个在左右两边、低一点、在他前面。
/// - 头：侧面看是蛇头——头顶平、略收；下颌在头根处往下鼓出来，再一起收到吻尖；一条暗的嘴线；眼睛在上半边，一点很弱的暖光。
/// - 颜色：画面保持原样，只压暗四角；蛇是近黑到深酒红的烟，朝上一侧被天光勾一线暗红。天上一个很小的日蚀。
/// - 动作：脖子沿自己的路线长出来（头带着走），身上一道 S 形的波往头传；五个头各自慢慢摆，
///   一起朝敌人探一下头，再散回烟里。
/// 全部着色器：脊线在 CPU 上算（6 条 × 24 点，带深度），着色器里求到折线的距离、套粗细曲线，边缘用流动的噪声撕成烟；
/// 深度 &gt; 0 的部分画在前景层（盖住 Joker），其余画在背景层。
/// </summary>
public sealed class MistSerpent
{
	private const int N = 24;          // 每条的脊线点数（脖子的最后一个点 = 头根，头在着色器里单独画）
	private const int Strands = 6;     // 0 = 脚边那一圈蛇身，1～5 = 五条脖子
	private const int Necks = 5;
	public const float Life = 3.2f;
	private const float HeadScale = 1.25f;

	private static Shader? _body, _air;

	private static Shader Body => _body ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec3 spine[144];         // 6 条 × 24 点：xy = 相对 Joker 脚底的像素，z = 深度（> 0 在 Joker 前面）。脖子的最后一个点 = 头根
uniform vec4 box[6];             // 每条的包围盒（像素，已放宽，含头），盒外直接跳过
uniform float girth[6];          // 最粗处的半宽（像素）；头的尺寸单位也是它
uniform float vis[6];
uniform vec2 hdir[6];            // 头朝向（屏幕）
uniform vec2 hup[6];             // 头的「上」方向（屏幕）
uniform float hopen[6];          // 张嘴的角度（弧度）
uniform float hscale = 1.25;     // 头比脖子粗多少
uniform vec2 origin;
uniform vec2 size;
uniform float side = 1.0;        // 1 前景层 / -1 背景层
uniform float clock = 0.0;
uniform float dissolve = 0.0;
uniform float ground = 0.0;

float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { return vn(p) * 0.5 + vn(p * 2.03 + 5.1) * 0.3 + vn(p * 4.1 + 1.7) * 0.2; }
float sdE(vec2 p, vec2 r) { float k0 = length(p / r); float k1 = length(p / (r * r)); return k0 * (k0 - 1.0) / max(k1, 1e-4); }
float smin(float a, float b, float k) { float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0); return mix(b, a, h) - k * h * (1.0 - h); }
vec2 rot(vec2 v, float a) { float c = cos(a), s = sin(a); return vec2(c * v.x - s * v.y, s * v.x + c * v.y); }

// 一颗獠牙：f = 相对牙根（牙尖朝 -y），往后弯一点
float fang(vec2 f, float len, float w) {
	float t = clamp(-f.y / len, 0.0, 1.0);
	float d = abs(f.x + 0.18 * len * t * t) - w * (1.0 - t);
	return max(d, max(f.y, -len - f.y));
}

// 脖子的粗细（s：0 根部 → 1 头根）：越往上越细，头后面一截再收一点
float profile(float s) { return mix(1.0, 0.62, smoothstep(0.05, 0.85, s)) * (1.0 - 0.15 * smoothstep(0.85, 1.0, s)); }

void fragment() {
	vec2 p = origin + UV * size;
	vec3 acc = vec3(0.0);
	float A = 0.0;
	vec2 L2 = normalize(vec2(-0.35, -1.0));
	for (int j = 0; j < 6; j++) {
		if (vis[j] <= 0.001) continue;
		vec4 bb = box[j];
		if (p.x < bb.x || p.y < bb.y || p.x > bb.z || p.y > bb.w) continue;
		bool ring = j == 0;
		float g = girth[j];
		// ── 脖子 / 脚边那一圈：到折线的距离 ──
		float best = 1e9, bs = 0.0, bz = 0.0;
		vec2 bq = p;
		for (int k = 0; k < 23; k++) {
			vec3 a = spine[j * 24 + k], b = spine[j * 24 + k + 1];
			if (side > 0.0 ? max(a.z, b.z) <= 0.0 : min(a.z, b.z) > 0.0) continue;
			vec2 ab = b.xy - a.xy;
			float h = clamp(dot(p - a.xy, ab) / max(dot(ab, ab), 1e-3), 0.0, 1.0);
			vec2 q = a.xy + ab * h;
			float d = length(p - q);
			if (d < best) { best = d; bs = (float(k) + h) / 23.0; bz = mix(a.z, b.z, h); bq = q; }
		}
		vec3 tip = spine[j * 24 + 23];
		bool headLayer = !ring && (side > 0.0 ? tip.z > 0.0 : tip.z <= 0.0);
		if (best > 1e8 && !headLayer) continue;
		float under = smoothstep(ground + g * 0.5, ground - g * 1.2, p.y);
		float dens = 0.0, wisp = 0.0;
		vec3 col = vec3(0.0);
		if (best < 1e8) {
			vec2 n2 = (p - bq) / max(best, 1e-3);
			float w = g * (ring ? 1.0 : profile(bs)) * (1.0 + 0.1 * bz);
			if (best < w * 2.8) {
				float L = bs * (ring ? 16.0 : 9.0);
				vec2 flowp = vec2(L - clock * 1.1, best / g * 0.9 - clock * 0.25);
				float nz = fbm(flowp + float(j) * 13.0);
				float fine = vn(flowp * 3.0 + 4.0);
				float r = best / w;
				float torn = (nz - 0.5) * (ring ? 1.1 : 0.8) + (fine - 0.5) * 0.1;
				torn += ring ? 0.0 : smoothstep(0.12, 0.0, bs) * (nz - 0.25) * 0.8;
				torn += dissolve * (0.5 + nz * 1.6);
				float dd = r + torn;
				dens = smoothstep(1.0, 0.58, dd) * under;
				float wn = fbm(vec2(L * 0.6 - clock * 1.2, best / g * 0.8 - clock * 0.9) + 7.0 + float(j) * 3.0);
				float upSide = smoothstep(-0.3, 0.6, -n2.y);
				wisp = (smoothstep(0.48, 0.8, wn) * smoothstep(mix(1.8, 2.8, upSide), 1.0, r) * 0.5 + smoothstep(1.9, 0.9, r) * 0.14) * under;
				float lit = max(dot(n2, L2), 0.0);
				float rim = smoothstep(0.35, 0.95, dd) * pow(lit, 1.4);
				float halfLit = smoothstep(-0.2, 0.8, dot(n2, L2)) * smoothstep(0.0, 0.9, r);
				vec3 core = mix(vec3(0.03, 0.012, 0.016), vec3(0.13, 0.04, 0.047), smoothstep(0.3, 0.8, nz)) + vec3(0.08, 0.017, 0.021) * halfLit;
				float band = smoothstep(0.75, 1.0, sin(L * 1.6 + clock * 4.0)) * 0.08;
				col = core + vec3(0.52, 0.13, 0.13) * rim * 0.9 + vec3(0.3, 0.07, 0.08) * band;
				if (bz < 0.0) col *= 0.66;
				vec3 wc = vec3(0.05, 0.016, 0.02) + vec3(0.12, 0.03, 0.035) * smoothstep(0.65, 0.9, wn);
				col = mix(wc, col, dens / max(dens + wisp * (1.0 - dens), 1e-3));
			}
		}
		// ── 头：上颚 + 张开的下颌 + 口腔 + 獠牙 + 分叉的舌头 + 竖瞳的眼睛（局部坐标 x 朝前、y 朝上，单位 = 脖子半宽 × hscale）──
		float fx = 0.0;     // 牙 / 舌 / 眼这些盖在最上面的东西
		vec3 fc = vec3(0.0);
		if (headLayer) {
			float u0 = g * hscale;
			vec2 rel = p - tip.xy;
			vec2 q = vec2(dot(rel, hdir[j]), dot(rel, hup[j])) / u0;
			if (q.x > -1.6 && q.x < 4.6 && abs(q.y) < 2.6) {
				float op = hopen[j];
				vec2 H = vec2(0.35, -0.05);                              // 下颌的铰点
				vec2 u = rot(q - H, -op * 0.25) + H;                     // 上颚往上抬一点
				vec2 l = rot(q - H, op * 0.75) + H;                       // 下颌往下张
				// 上颚：后脑（宽）+ 吻部（窄、扁）+ 眉骨；底面平（嘴缝）
				float cran = sdE(u - vec2(0.85, 0.3), vec2(1.15, 0.66));
				float snout = sdE(u - vec2(2.05, 0.16), vec2(1.0, 0.4));
				float brow = sdE(u - vec2(1.25, 0.6), vec2(0.5, 0.2));
				float dUp = smin(smin(cran, snout, 0.3), brow, 0.12);
				dUp = max(dUp, -(u.y + 0.05));
				// 下颌：细长，颌角往下鼓；顶面平
				float jaw = sdE(l - vec2(1.3, -0.22), vec2(1.3, 0.26));
				float jang = sdE(l - vec2(0.45, -0.3), vec2(0.55, 0.4));
				float dLo = max(smin(jaw, jang, 0.25), l.y + 0.05);
				// 脖子接进后脑
				float nape = sdE(q - vec2(-0.2, 0.0), vec2(0.7, 0.62));
				float dHead = min(smin(dUp, nape, 0.35), dLo);
				// 口腔：两片颌之间的楔形
				vec2 m = q - H;
				float ang = atan(m.y, m.x);
				// 只在两片颌合起来的范围里（不会伸出颌外成一块）
				float mouth = smoothstep(-op * 0.75 - 0.02, -op * 0.75 + 0.08, ang) * smoothstep(op * 0.25 + 0.02, op * 0.25 - 0.08, ang)
					* smoothstep(2.0, 1.55, length(m)) * step(0.0, m.x);
				float hn = fbm(q * 1.3 + vec2(-clock * 0.8, clock * 0.2) + float(j) * 5.0);
				float dh = dHead + (hn - 0.5) * 0.22 + dissolve * (0.4 + hn * 1.4);
				float hd = smoothstep(0.1, -0.12, dh) * under;
				float md = mouth * (1.0 - dissolve) * under;
				// 头的颜色：近黑酒红；上沿被天光勾一线暗红；后脑有一点烟的变化
				float topEdge = smoothstep(-0.32, -0.02, dUp) * smoothstep(-0.1, 0.4, u.y) * (dUp < 0.05 ? 1.0 : 0.0);
				vec3 hc = mix(vec3(0.035, 0.013, 0.017), vec3(0.12, 0.035, 0.042), smoothstep(0.35, 0.8, hn));
				hc += vec3(0.55, 0.14, 0.14) * topEdge * 0.85;
				hc += vec3(0.25, 0.05, 0.06) * smoothstep(-0.25, 0.0, dLo) * (dLo < 0.05 ? 1.0 : 0.0) * 0.9;   // 下颌边
				vec3 mc = mix(vec3(0.2, 0.025, 0.035), vec3(0.04, 0.004, 0.01), smoothstep(1.6, 0.3, length(m)));   // 口腔：里面深
				float hmix = max(hd, md);
				vec3 hcol = mix(hc, mc, md * (1.0 - smoothstep(-0.05, -0.25, min(dUp, dLo))));
				if (tip.z < 0.0) hcol *= 0.72;
				if (hmix > dens) { col = mix(col, hcol, smoothstep(0.0, 1.0, hmix)); dens = hmix; }
				// 獠牙：上颚前端两颗（远的那颗小一点），下颌一颗小的
				float f1 = fang(u - vec2(2.2, -0.05), 0.75, 0.13);
				float f2 = fang(u - vec2(1.85, -0.05), 0.45, 0.09);
				float f3 = fang((l - vec2(2.1, -0.05)) * vec2(1.0, -1.0), 0.3, 0.08);
				float fd = min(min(f1, f2), f3);
				float fa = smoothstep(0.04, -0.03, fd) * (1.0 - dissolve);
				vec3 fcol = mix(vec3(0.74, 0.67, 0.56), vec3(0.38, 0.3, 0.26), smoothstep(-0.2, 0.3, q.y));
				// 舌头：从口腔里伸出来，分叉，快速地一伸一缩、抖
				float midA = (op * 0.3 - op * 0.7) * 0.5;
				vec2 tq = rot(q - (H + vec2(0.3, 0.0)), -midA);
				float flick = 0.55 + 0.45 * sin(clock * 9.0 + float(j) * 1.7);
				float Lt = 2.3 + 0.8 * flick;
				float wav = sin(tq.x * 3.5 - clock * 26.0) * 0.06 * smoothstep(1.0, 2.5, tq.x);
				float stem = max(abs(tq.y + wav) - 0.075, max(-tq.x, tq.x - (Lt - 0.5)));
				float fx0 = tq.x - (Lt - 0.5);
				float fork = max(min(abs(tq.y + wav - fx0 * 0.55), abs(tq.y + wav + fx0 * 0.55)) - 0.055 * (1.0 - fx0 / 0.6), max(-fx0, fx0 - 0.55));
				float td = min(stem, fork);
				float ta = smoothstep(0.03, -0.03, td) * (1.0 - dissolve);
				vec3 tcol = vec3(0.48, 0.07, 0.1) * (0.75 + 0.35 * smoothstep(0.0, 0.07, -tq.y - wav));
				// 眼睛：杏仁形，金红的虹膜 + 竖的瞳孔 + 一点高光，外面一圈很弱的光晕
				vec2 e = u - vec2(1.2, 0.3);
				float eye = sdE(e, vec2(0.27, 0.17));
				float ea = smoothstep(0.03, -0.03, eye) * (1.0 - dissolve);
				float slit = smoothstep(0.05, 0.02, abs(e.x) / max(1.0 - pow(e.y / 0.17, 2.0), 0.05));
				vec3 ecol = mix(vec3(1.0, 0.62, 0.2), vec3(0.75, 0.16, 0.06), smoothstep(0.0, 0.25, length(e)));
				ecol = mix(ecol, vec3(0.02, 0.0, 0.0), slit);
				ecol += vec3(1.0, 0.9, 0.75) * smoothstep(0.06, 0.0, length(e - vec2(-0.08, 0.06))) * 0.8;
				float halo = exp(-dot(e, e) / 0.12) * 0.35 * (1.0 - dissolve) * (1.0 - ea);
				// 叠：舌头 → 牙 → 眼睛（舌头在嘴里那段被下颌挡住一点）
				fc = tcol; fx = ta;
				fc = mix(fc, fcol, fa); fx = max(fx, fa);
				fc = mix(fc, ecol, ea); fx = max(fx, ea);
				col += vec3(0.8, 0.3, 0.1) * halo;
				dens = max(dens, halo * 0.5);
			}
		}
		float a = clamp(dens * 0.94 + wisp * (1.0 - dens), 0.0, 1.0) * vis[j];
		float dim = bz < 0.0 && dens <= 0.0 ? 0.82 : 1.0;
		vec3 c = col * dim;
		c = mix(c, fc, fx);
		a = max(a, fx * vis[j]);
		acc += (1.0 - A) * c * a;
		A += (1.0 - A) * a;
	}
	if (A <= 0.002) discard;
	COLOR = vec4(acc, A);
}" };

	/// <summary>天色：只压暗四角 + 一点点酒红；天上一个很小的日蚀；Joker 脚下一片暗雾。全都很淡。</summary>
	private static Shader Air => _air ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float amount = 0.0;
uniform float aspect = 1.7778;
uniform vec2 sun = vec2(0.36, 0.13);
uniform vec2 pool = vec2(0.22, 0.77);
uniform float pool_w = 0.2;
uniform float clock = 0.0;
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { return vn(p) * 0.5 + vn(p * 2.03 + 5.1) * 0.3 + vn(p * 4.1 + 1.7) * 0.2; }
void fragment() {
	vec2 uv = UV;
	float vig = smoothstep(0.3, 1.05, length((uv - vec2(0.45, 0.5)) * vec2(aspect * 0.55, 1.0)));
	vec3 col = vec3(0.025, 0.006, 0.01);
	float a = 0.1 + vig * 0.42;
	vec2 d = (uv - sun) * vec2(aspect, 1.0);
	float r = length(d);
	const float R = 0.018;
	float halo = exp(-max(r - R, 0.0) / 0.05) * step(R * 0.9, r);
	col += vec3(0.4, 0.09, 0.09) * halo * 0.35;
	float disk = 1.0 - smoothstep(R - 0.003, R + 0.002, r);
	col = mix(col, vec3(0.03, 0.008, 0.012), disk);
	a = max(a, disk * 0.9);
	float ring = exp(-pow((r - R) / 0.0035, 2.0));
	col += vec3(0.5, 0.13, 0.12) * ring * 0.35;
	vec2 pd = (uv - pool) / vec2(pool_w, pool_w * 0.28);
	float pn = fbm(vec2(uv.x * 7.0 - clock * 0.15, uv.y * 12.0 + clock * 0.1));
	float pm = exp(-dot(pd, pd) * 1.1) * (0.45 + 0.7 * pn);
	col = mix(col, vec3(0.07, 0.02, 0.026) + vec3(0.12, 0.03, 0.035) * smoothstep(0.5, 0.9, pn), clamp(pm, 0.0, 1.0) * 0.8);
	a = max(a, clamp(pm * 0.55, 0.0, 0.6));
	COLOR = vec4(col * a * amount, a * amount);
}" };

	/// <summary>
	/// 五条脖子：根在脚边那一圈上的角度（0 = 朝敌人那边，90° = 正前方），头的位置（身高为单位，x 朝敌人、y 往上为负），
	/// 头朝向，大小，深度（&gt; 0 在 Joker 前面），出场延迟。
	/// </summary>
	private static readonly (float Ang, float HX, float HY, float LX, float LY, float Size, float Z, float Delay)[] Layout =
	{
		// 用户 2026-11-11：分布太散 → 五个头收拢在 Joker 身后和两肩上方
		(250f, 0.12f, -1.68f, 1f, 0.3f, 1.0f, -0.6f, 0.15f),      // 正中、最高，在身后
		(215f, -0.4f, -1.42f, 0.95f, -0.05f, 0.9f, -0.55f, 0.28f), // 左后
		(320f, 0.52f, -1.4f, 1f, 0.35f, 0.95f, -0.5f, 0.22f),     // 右后
		(30f, 0.62f, -0.92f, 1f, 0.12f, 0.85f, 0.55f, 0.36f),     // 右前、低
		(150f, -0.64f, -0.98f, -0.7f, -0.3f, 0.82f, 0.5f, 0.44f), // 左前、低，朝外
	};

	private readonly Node2D _frontRoot, _backRoot, _airRoot;
	private readonly ShaderMaterial _frontM, _backM, _airM;
	private readonly float _tall, _face;
	private readonly Vector3[] _spine = new Vector3[N * Strands];
	private readonly Vector4[] _box = new Vector4[Strands];
	private readonly float[] _girth = new float[Strands], _vis = new float[Strands];
	private readonly Vector2[] _hup = new Vector2[Strands], _hdir = new Vector2[Strands];
	private readonly float[] _hopen = new float[Strands];

	/// <summary>放一次。feet = Joker 脚底（层坐标），tall = 身高（像素），face = 朝向（1 朝右）。</summary>
	public static MistSerpent Play(Control front, Control back, Vector2 feet, float tall, float face) => new(front, back, feet, tall, face);

	private MistSerpent(Control front, Control back, Vector2 feet, float tall, float face)
	{
		_tall = tall;
		_face = face;
		_girth[0] = 0.15f * tall;
		for (int i = 0; i < Necks; i++) _girth[i + 1] = 0.095f * tall * Layout[i].Size;

		Vector2 origin = new(-1.9f * tall, -2.3f * tall), size = new(3.9f * tall, 2.65f * tall);
		(_frontRoot, _frontM) = MakeBody(front, feet, origin, size, 1f);
		(_backRoot, _backM) = MakeBody(back, feet, origin, size, -1f);

		_airRoot = Fx.ScreenRoot(back, "P5MistSerpentAir");
		Vector2 scr = Fx.Screen(back);
		_airM = new ShaderMaterial { Shader = Air };
		var air = new ColorRect { Size = scr, Material = _airM, MouseFilter = Control.MouseFilterEnum.Ignore };
		_airRoot.AddChild(air);
		back.MoveChild(_airRoot, 0);
		Vector2 fs = Fx.ScreenPoint(back, feet);
		float tallS = tall * back.GetCanvasTransform().Scale.Y;
		_airM.SetShaderParameter("aspect", scr.X / scr.Y);
		_airM.SetShaderParameter("pool", new Vector2(fs.X / scr.X, (fs.Y - 0.02f * tallS) / scr.Y));
		_airM.SetShaderParameter("pool_w", tallS * 1.1f / scr.X);
		_airM.SetShaderParameter("sun", new Vector2((fs.X + face * tallS * 1.6f) / scr.X, 0.12f));
		Fx.Clock(air);

		Fx.FreeAfter(_frontRoot, Life + 0.1);
		Fx.FreeAfter(_backRoot, Life + 0.1);
		Fx.FreeAfter(_airRoot, Life + 0.1);
		Update(0f);
		Fx.Animate(_frontRoot, 0, Life, k => Update(k * Life));
	}

	private static (Node2D, ShaderMaterial) MakeBody(Control layer, Vector2 feet, Vector2 origin, Vector2 size, float side)
	{
		Node2D root = new() { Name = side > 0 ? "P5MistSerpentFront" : "P5MistSerpentBack" };
		layer.AddChild(root);
		root.GlobalPosition = feet;
		var m = new ShaderMaterial { Shader = Body };
		var q = new ColorRect { Position = origin, Size = size, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		m.SetShaderParameter("origin", origin);
		m.SetShaderParameter("size", size);
		m.SetShaderParameter("side", side);
		Fx.Clock(q);
		return (root, m);
	}

	private static float Smooth(float a, float b, float x) { float t = Mathf.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3f - 2f * t); }

	/// <summary>脚边那一圈上角度 deg 处的点（x 朝敌人，前半圈在下面、在 Joker 前面）。</summary>
	private Vector3 Ring(float deg, float t)
	{
		float a = Mathf.DegToRad(deg);
		float rx = 0.62f * _tall, ry = 0.12f * _tall;
		float wob = Mathf.Sin(a * 3f + t * 1.5f) * 0.02f * _tall;
		return new Vector3(Mathf.Cos(a) * rx * _face, -0.04f * _tall + Mathf.Sin(a) * ry + wob, Mathf.Sin(a));
	}

	private static Vector3 Bez(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
	{
		float u = 1f - t;
		return a * (u * u * u) + b * (3f * u * u * t) + c * (3f * u * t * t) + d * (t * t * t);
	}

	private void Update(float t)
	{
		float T = _tall;
		float lunge = Smooth(1.72f, 1.9f, t) - 0.6f * Smooth(1.95f, 2.3f, t);
		float diss = Smooth(2.25f, 3.0f, t);

		// 0：脚边那一圈（首尾接上，绕着慢慢转；出场时从雾里浓起来）
		float spin = t * 12f;
		for (int k = 0; k < N; k++)
		{
			_spine[k] = Ring(spin + 360f * k / (N - 1), t);
		}
		_vis[0] = Smooth(0.0f, 0.45f, t) * (1f - diss * 0.6f) * 0.9f;
		_hup[0] = new Vector2(0f, -1f);
		_hdir[0] = new Vector2(1f, 0f);

		for (int i = 0; i < Necks; i++)
		{
			var (ang, hx, hy, lx, ly, sz, z, delay) = Layout[i];
			int j = i + 1;
			float g = _girth[j];
			float grow = Smooth(delay, delay + 1.0f, t);
			grow = 1f - (1f - grow) * (1f - grow);
			// 每个头各自慢慢摆；探头时一起往朝向那边伸
			Vector2 look = new Vector2(lx * _face, ly).Normalized();
			Vector2 sway = new(Mathf.Sin(t * 1.3f + i * 1.9f) * 0.05f * T, Mathf.Sin(t * 1.7f + i * 2.6f) * 0.035f * T);
			Vector2 headP = new Vector2(hx * _face * T, hy * T) + sway + look * lunge * 0.28f * T + new Vector2(0f, diss * 0.15f * T);
			Vector3 baseP = Ring(ang, t);
			Vector3 head3 = new(headP.X, headP.Y, z);
			float H = baseP.Y - headP.Y;
			// S 形的脖子：先往外鼓，再往回弯，最后顺着头的朝向进去
			float outward = (hx >= 0f ? 1f : -1f) * _face;
			// 用户 2026-11-11：别一直往上再在顶上折成直角横过去 → 脖子是一道圆滑的大弧：根部略往外鼓，上半段从斜下方弯进头里
			Vector3 c1 = baseP + new Vector3(outward * 0.16f * T, -H * 0.55f, 0f);
			Vector3 c2 = head3 - new Vector3(look.X, look.Y, 0f) * 0.2f * T + new Vector3(0f, 0.3f * T, 0f);
			c1.Z = baseP.Z * 0.5f + z * 0.5f;
			c2.Z = z;

			// 沿曲线 0 → grow 取点（头带着身体从根部长出来），按弧长均匀分
			const int D = 48;
			Vector3[] dense = new Vector3[D];
			float[] acc = new float[D];
			for (int k = 0; k < D; k++)
			{
				float u = grow * k / (D - 1);
				Vector3 pt = Bez(baseP, c1, c2, head3, u);
				// 往头传的 S 波（根部和头附近为 0）
				float env = Mathf.Sin(Mathf.Pi * Mathf.Clamp(u, 0f, 1f)) * (1f - 0.6f * u) * 0.05f * T;
				float wave = Mathf.Sin((u * 1.8f - t * 1.1f) * Mathf.Tau + i);
				Vector3 tg = Bez(baseP, c1, c2, head3, Mathf.Min(1f, u + 0.01f)) - Bez(baseP, c1, c2, head3, Mathf.Max(0f, u - 0.01f));
				Vector2 nrm = new Vector2(-tg.Y, tg.X).Normalized();
				dense[k] = pt + new Vector3(nrm.X, nrm.Y, 0f) * wave * env;
				acc[k] = k == 0 ? 0f : acc[k - 1] + new Vector2(dense[k].X - dense[k - 1].X, dense[k].Y - dense[k - 1].Y).Length();
			}
			const int nb = N;
			float total = acc[D - 1];
			int seg = 0;
			for (int k = 0; k < nb; k++)
			{
				float target = total * k / (nb - 1);
				while (seg < D - 2 && acc[seg + 1] < target) seg++;
				float f = acc[seg + 1] > acc[seg] ? (target - acc[seg]) / (acc[seg + 1] - acc[seg]) : 0f;
				_spine[j * N + k] = dense[seg].Lerp(dense[seg + 1], Mathf.Clamp(f, 0f, 1f));
			}
			// 头：朝向 = 脖子末端的切线和设定的朝向混一下；嘴一直张着，探头时张得更大
			Vector3 tip = _spine[j * N + nb - 1], prev = _spine[j * N + nb - 3];
			Vector2 tan = new Vector2(tip.X - prev.X, tip.Y - prev.Y);
			tan = tan.LengthSquared() > 1e-3f ? tan.Normalized() : look;
			Vector2 dir = tan.Lerp(look, 0.45f * grow).Normalized();
			Vector2 up = new Vector2(dir.Y, -dir.X);
			if (up.Y > 0f) up = -up;
			_hup[j] = up;
			_hdir[j] = dir;
			_hopen[j] = (0.42f + 0.06f * Mathf.Sin(t * 2.3f + i) + 0.3f * Mathf.Max(lunge, 0f)) * Smooth(delay + 0.2f, delay + 0.8f, t);
			_vis[j] = Smooth(delay, delay + 0.25f, t) * (1f - diss * 0.6f);
		}

		for (int j = 0; j < Strands; j++)
		{
			float m = _girth[j] * 3f;
			Vector2 lo = new(1e9f, 1e9f), hi = new(-1e9f, -1e9f);
			for (int k = 0; k < N; k++)
			{
				Vector3 q = _spine[j * N + k];
				lo = new Vector2(Mathf.Min(lo.X, q.X), Mathf.Min(lo.Y, q.Y));
				hi = new Vector2(Mathf.Max(hi.X, q.X), Mathf.Max(hi.Y, q.Y));
			}
			if (j > 0)
			{
				// 头（含伸出来的舌头）大约在头根前后 5 个头单位内
				Vector3 tp = _spine[j * N + N - 1];
				float hr = _girth[j] * HeadScale * 5f;
				lo = new Vector2(Mathf.Min(lo.X, tp.X - hr), Mathf.Min(lo.Y, tp.Y - hr));
				hi = new Vector2(Mathf.Max(hi.X, tp.X + hr), Mathf.Max(hi.Y, tp.Y + hr));
			}
			_box[j] = new Vector4(lo.X - m, lo.Y - m, hi.X + m, hi.Y + m);
		}
		foreach (ShaderMaterial mat in new[] { _frontM, _backM })
		{
			mat.SetShaderParameter("spine", _spine);
			mat.SetShaderParameter("box", _box);
			mat.SetShaderParameter("girth", _girth);
			mat.SetShaderParameter("vis", _vis);
			mat.SetShaderParameter("hup", _hup);
			mat.SetShaderParameter("hdir", _hdir);
			mat.SetShaderParameter("hopen", _hopen);
			mat.SetShaderParameter("hscale", HeadScale);
			mat.SetShaderParameter("dissolve", diss);
			mat.SetShaderParameter("ground", 0.06f * T);
		}
		_airM.SetShaderParameter("amount", Smooth(0f, 0.45f, t) * (1f - Smooth(2.6f, 3.2f, t)));
	}
}
