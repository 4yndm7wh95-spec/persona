using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 群蛇形态 · 五条咒烟大蛇（2026-11-11 第四版，用户：光蛇太艳、细节不行的形象更难堪；要更偏另一个会话那种暗的、像烟雾的形象，
/// 大一些、霸气，烟雾正好掩盖形象做不好的问题；五条蛇各自不同轨迹、浮空悬浮着绕 Joker 漂；群蛇形态造成伤害时随机一条蛇
/// 发动远程的法术攻击那个敌人）。
/// - 质感照毒性爆发的大手（CrushHand）：近黑的咒烟，只有边缘里面一薄层透出暗红的光，边缘被流动的噪声撕成烟，外面一缕缕往上飘的暗烟；
///   眼睛是一团晕开的暖红雾光。不画牙、不画鳞，头只是一个上平下宽的楔形。
/// - 漂浮：五条各有一条自己的轨道（椭圆 + 上下起伏，有的在身后高处慢慢转，有的绕着腰贴身转，方向、快慢都不同），
///   身体跟着头走过的路（按时间往回取头的位置），再叠一道沿身体传的 S 波，尾巴散成烟。
/// - 出场（打出群蛇形态）：脚下一团咒烟炸开，五条蛇从 Joker 身上一条接一条地冲出来、甩到各自的轨道上，最后五个头一起转向敌人、眼睛一亮。
/// - 攻击（能力触发）：挑一条闲着的蛇，转头对准敌人往后一缩，嘴前聚起一团暗红的光（0.28s）→ 吐出一颗咒怨法球（烟壳、热红的芯、
///   拖一道烟尾），0.2s 飞到 → 命中：一圈往外炸的咒烟环 + 闪光 + 火星 + 轻震 → 蛇回到轨道上。连续出牌几条蛇轮流吐。
/// 全部一个着色器（前景层 / 背景层各一块，深度 &gt; 0 的部分画在前面盖住 Joker），普通混合（暗的烟要能压暗背景）。
/// </summary>
public sealed class SerpentMantle
{
	private const int Count = 5;
	private const int N = 32;            // 每条脊线点数（最后 4 个是头）
	private const int HeadPts = 4;
	private const int Slots = 3;         // 同时最多几发法球
	private const float BiteAt = 0.46f, AttackLen = 1.15f;

	public static SerpentMantle? Current { get; private set; }

	private static Shader? _shader;

	// 蛇头轮廓（和 tools/p5/diagrams/serpent_head_plan.py 同一组点，Catmull-Rom 每段 5 个点）
	private static readonly Vector2[] Upper = { new(-0.95f, 0.5f), new(-0.45f, 0.52f), new(-0.1f, 0.64f), new(0.35f, 0.66f), new(0.78f, 0.6f), new(0.95f, 0.55f), new(1.25f, 0.4f), new(1.47f, 0.22f), new(1.56f, 0.04f) };
	private static readonly Vector2[] Mouth = { new(1.53f, -0.04f), new(1.2f, -0.08f), new(0.75f, -0.1f), new(0.35f, -0.13f), new(0.14f, -0.2f), new(0.06f, -0.3f) };
	private static readonly Vector2[] JawTop = { new(1.36f, -0.1f), new(1.2f, -0.085f), new(0.75f, -0.1f), new(0.35f, -0.13f), new(0.14f, -0.2f), new(0.06f, -0.3f) };
	private static readonly Vector2[] JawUnder = { new(0.06f, -0.3f), new(-0.02f, -0.5f), new(0.05f, -0.64f), new(0.35f, -0.58f), new(0.75f, -0.4f), new(1.12f, -0.24f), new(1.36f, -0.1f) };

	private static List<Vector2> CatRom(Vector2[] p, int n = 5)
	{
		var o = new List<Vector2>();
		for (int i = 0; i < p.Length - 1; i++)
		{
			Vector2 p0 = p[Math.Max(0, i - 1)], p1 = p[i], p2 = p[i + 1], p3 = p[Math.Min(p.Length - 1, i + 2)];
			for (int k = 0; k < n; k++)
			{
				float t = k / (float)n, t2 = t * t, t3 = t2 * t;
				o.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
			}
		}
		o.Add(p[^1]);
		return o;
	}

	private static void PushHead(ShaderMaterial m)
	{
		var up = CatRom(Upper); var mouth = CatRom(Mouth); var jt = CatRom(JawTop); var ju = CatRom(JawUnder);
		var fixedP = new List<Vector2>(up); fixedP.AddRange(mouth);
		fixedP.AddRange(new Vector2[] { new(0f, -0.56f), new(-0.3f, -0.56f), new(-0.6f, -0.5f), new(-0.95f, -0.5f) });
		var jaw = new List<Vector2>(jt); jaw.AddRange(ju.GetRange(1, ju.Count - 2));
		var f72 = new Vector2[72]; for (int i = 0; i < fixedP.Count && i < 72; i++) f72[i] = fixedP[i];
		var j60 = new Vector2[60]; for (int i = 0; i < jaw.Count && i < 60; i++) j60[i] = jaw[i];
		m.SetShaderParameter("head_fixed", f72); m.SetShaderParameter("head_fixed_n", Math.Min(fixedP.Count, 72));
		m.SetShaderParameter("head_jaw", j60); m.SetShaderParameter("head_jaw_n", Math.Min(jaw.Count, 60));
		m.SetShaderParameter("lip_up", mouth.ToArray()); m.SetShaderParameter("lip_low", jt.ToArray());
	}

	private static Shader Shade => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec4 spine[160];        // 5 条 × 32 点：xy 相对脚底的像素，z 深度（> 0 在 Joker 前面），w 透明度；0 = 尾，27 = 头根，31 = 吻尖
uniform vec4 box[5];
uniform float girth[5];
uniform vec2 hup[5];
uniform vec3 eyes[5];           // xy 位置，z 亮度
uniform vec2 origin;
uniform vec3 nest;               // 蛇巢：xy 中心（相对脚底），z 半径；五条蛇的尾巴都收进这一团咒烟
uniform float nest_a = 0.0;
uniform vec2 size;
uniform float side = 1.0;
uniform float clock = 0.0;
uniform float unit = 300.0;     // Joker 身高（像素）
uniform float diss[5];          // 每条蛇散成烟的程度（攻击时 0 → 1 → 0）
uniform vec4 puff[6];           // 咒烟团：xy 中心，z 进度（0～1；< 0 没有），w 大小（像素；负数 = 咬中那一团，中间带一下光）
uniform float mouth[5];         // 嘴张多大：0 平时微张，1 张到最大，< 0 合上
// 蛇头的轮廓（照 docs/p5/preview/serpent_head_plan.png 的点，tools/p5/diagrams/serpent_head_plan.py 同一组；单位 = 脖子粗细 W，x 朝吻尖，y 朝上）
uniform vec2 head_fixed[72];    // 不动的部分：脖子上沿 → 后脑 → 头顶 → 眉骨 → 吻尖 → 嘴线 → 铰点 → 后脑下沿 → 脖子下沿
uniform int head_fixed_n = 0;
uniform vec2 head_jaw[60];      // 下颚：嘴线（下巴 → 铰点）+ 下沿（铰点 → 颚角 → 下巴）
uniform int head_jaw_n = 0;
uniform vec2 lip_up[26];        // 上唇（嘴线，吻尖 → 铰点）
uniform vec2 lip_low[26];       // 下唇（下巴 → 铰点，下颚自己的坐标）

float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { return vn(p) * 0.5 + vn(p * 2.03 + 5.1) * 0.3 + vn(p * 4.1 + 1.7) * 0.2; }
float ease3(float x) { return 1.0 - pow(1.0 - clamp(x, 0.0, 1.0), 3.0); }
float sdPolyFixed(vec2 p) {
	float d = dot(p - head_fixed[0], p - head_fixed[0]); float sg = 1.0;
	for (int i = 0, j = head_fixed_n - 1; i < head_fixed_n; j = i, i++) {
		vec2 e = head_fixed[j] - head_fixed[i], w = p - head_fixed[i];
		vec2 b = w - e * clamp(dot(w, e) / dot(e, e), 0.0, 1.0);
		d = min(d, dot(b, b));
		bvec3 c = bvec3(p.y >= head_fixed[i].y, p.y < head_fixed[j].y, e.x * w.y > e.y * w.x);
		if (all(c) || all(not(c))) sg *= -1.0;
	}
	return sg * sqrt(d);
}
float sdPolyJaw(vec2 p) {
	float d = dot(p - head_jaw[0], p - head_jaw[0]); float sg = 1.0;
	for (int i = 0, j = head_jaw_n - 1; i < head_jaw_n; j = i, i++) {
		vec2 e = head_jaw[j] - head_jaw[i], w = p - head_jaw[i];
		vec2 b = w - e * clamp(dot(w, e) / dot(e, e), 0.0, 1.0);
		d = min(d, dot(b, b));
		bvec3 c = bvec3(p.y >= head_jaw[i].y, p.y < head_jaw[j].y, e.x * w.y > e.y * w.x);
		if (all(c) || all(not(c))) sg *= -1.0;
	}
	return sg * sqrt(d);
}
// 唇线在 x 处的高度（点按 x 从大到小排）
float lipY(vec2 pts[26], float x) {
	for (int i = 0; i < 25; i++) {
		vec2 a = pts[i], b = pts[i + 1];
		if (x <= a.x && x >= b.x) return mix(a.y, b.y, (x - a.x) / min(b.x - a.x, -1e-4));
	}
	return x > pts[0].x ? pts[0].y : pts[25].y;
}
vec2 rotAbout(vec2 p, vec2 c, float a) { float s = sin(a), co = cos(a); p -= c; return c + vec2(co * p.x - s * p.y, s * p.x + co * p.y); }
float smin(float a, float b, float k) { float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0); return mix(b, a, h) - k * h * (1.0 - h); }

// 身体的半径（× girth；s：0 尾尖 → 1 颈根）
float bodyR(float s) {
	// 照 docs/p5/preview/serpent_body_plan.png 的蓝线（用户 2026-11-11：身子太肥）：全宽（单位 W = 脖子粗细）中段最粗 1.1 W（比 1.3 W 的头窄），
	// 后 55% 慢慢收到又细又长的尾巴，往颈根收到 1.0 W；换成半径 × girth：W / 2 = 0.58 girth
	float w = 0.04 + 1.06 * pow(smoothstep(0.0, 0.55, s), 0.75);
	w *= 1.0 - (1.0 - 1.0 / 1.1) * smoothstep(0.82, 0.97, s);
	return w * 0.58;
}

// 叠一层（预乘）
void over(inout vec4 acc, vec3 c, float a) { acc.rgb += (1.0 - acc.a) * c * a; acc.a += (1.0 - acc.a) * a; }

void fragment() {
	vec2 p = origin + UV * size;
	vec4 acc = vec4(0.0);
	vec3 glow = vec3(0.0);
	// 每条蛇是「一个」形状（2026-11-11 重写：以前蛇身管道和头的轮廓各画各的、靠替换 / 裁掉拼起来，越改越错位）：
	// 蛇身 = 一根粗细连续变化的管子（所有小段的并集，交叉处不会互相挖空）；头 = 示意图的轮廓（docs/p5/preview/serpent_head_plan.png），
	// 以颈根为原点、沿脖子末端的方向放，和管子平滑地融成一个形状。烟、透光、层次都对这一个形状算；嘴、牙、舌头只在头的坐标里画。
	for (int j = 0; j < 5; j++) {
		vec4 bb = box[j];
		if (p.x < bb.x || p.y < bb.y || p.x > bb.z || p.y > bb.w) continue;
		float g = girth[j];
		float dj = diss[j];
		// ── 蛇身：脊线 0～27（27 = 颈根）上每一小段是一个圆头的管子，取并集（距离 − 半径 最小的那段）──
		float sdB = 1e9, bs = 0.0, bz = 0.0, ba = 0.0, br = g;
		vec2 bq = p;
		for (int k = 0; k < 27; k++) {
			vec4 a = spine[j * 32 + k], b = spine[j * 32 + k + 1];
			if (max(a.w, b.w) <= 0.001) continue;
			if (side > 0.0 ? max(a.z, b.z) <= 0.0 : min(a.z, b.z) > 0.0) continue;
			vec2 ab = b.xy - a.xy;
			float h = clamp(dot(p - a.xy, ab) / max(dot(ab, ab), 1e-3), 0.0, 1.0);
			vec2 cq = a.xy + ab * h;
			float sk = (float(k) + h) / 27.0;
			float rk = g * bodyR(sk);
			float sdk = length(p - cq) - rk;
			if (sdk < sdB) { sdB = sdk; bs = sk; bz = mix(a.z, b.z, h); ba = mix(a.w, b.w, h); bq = cq; br = rk; }
		}
		// ── 头：示意图的轮廓，单位 W = 脖子粗细；原点在颈根后 0.3 W（示意图 x = -0.3 是颈根）──
		vec4 root = spine[j * 32 + 27];
		vec2 hDir = normalize(spine[j * 32 + 31].xy - root.xy + vec2(1e-4));
		float Wd = g * 1.16;
		vec2 q = vec2(dot(p - root.xy, hDir) / Wd - 0.3, dot(p - root.xy, hup[j]) / Wd);
		bool headHere = root.w > 0.001 && (side > 0.0 ? root.z > 0.0 : root.z <= 0.0) && q.x > -1.3 && q.x < 2.5 && abs(q.y) < 1.8;
		float mo = mouth[j];
		float openA = radians(mo < 0.0 ? 0.0 : 12.0 + 28.0 * mo);   // 平时 12°，扑咬 40°，咬住 0°
		float upA = radians(mo > 0.0 ? 8.0 * mo : 0.0);
		vec2 HINGE = vec2(0.06, -0.3);
		vec2 qu = rotAbout(q, HINGE, -upA);
		vec2 ql = rotAbout(q, HINGE, openA);
		float sdH = 1e9;
		if (headHere) {
			// 示意图的「不动部分」带着一截脖子（x < -0.3），这里只要头：颈根往后切掉，脖子由管子负责
			float dF = max(sdPolyFixed(qu), -0.25 - qu.x);
			float dJ = sdPolyJaw(ql);
			sdH = smin(dF, dJ, 0.05) * Wd;
		}
		if (sdB > 1e8 && sdH > 1e8) continue;
		// ── 一个形状：管子和头平滑融合（颈根像一个球关节，转头不会错开）──
		// 没有头的地方直接用蛇身（1e9 进 smin 会在显卡上把蛇身的距离吃掉，整条变成半透明的带子）
		float sd = sdH > 1e8 ? sdB : (sdB > 1e8 ? sdH : smin(sdB, sdH, 0.2 * Wd));
		float headness = sdH < 1e8 ? smoothstep(0.12 * Wd, -0.12 * Wd, sdH - sdB) : 0.0;
		float R = mix(br, 0.5 * Wd, headness);
		float sdn = sd / max(R, 0.5);                              // 中线 ≈ -1，边 = 0
		if (sdn > 2.2) continue;
		float rr = 1.0 + sdn;                                       // 离中线几个半径
		vec2 hc = root.xy + hDir * Wd * 0.9;                        // 头中间（算头上的光照方向）
		vec2 n2 = normalize(mix(p - bq, p - hc, headness) + vec2(1e-4));
		float hx = clamp(q.x, -0.3, 1.6);
		float L = mix(bs * 11.0, 11.0 + hx * 1.5, headness);
		float acr = rr * (n2.y < 0.0 ? 1.0 : -1.0);
		float nz = fbm(vec2(L * 2.2 - clock * 1.3, acr * 1.1 - clock * 0.2) + float(j) * 11.0);
		float fine = vn(vec2(L * 3.5 - clock * 2.6, acr * 3.0) + float(j) * 4.0);
		float nz2 = fbm(vec2(L * 2.0 - clock * 1.8, acr * 1.6) + float(j) * 2.0);
		float flick = fbm(vec2(L * 1.7 - clock * 2.2, acr * 2.0) + 3.0 + float(j));
		// 烟：身体边上细碎、尾巴散开；头上后脑带一点，吻部、颚、嘴线保持干净
		float headSmoke = mix(0.3, 0.04, smoothstep(-0.1, 0.7, q.x));
		float torn = (nz - 0.5) * mix(0.3, headSmoke, headness) + (fine - 0.5) * mix(0.22, 0.05, headness)
			+ (1.0 - smoothstep(0.0, 0.25, bs)) * nz * 0.6 * (1.0 - headness);
		torn += dj * (0.5 + nz * 1.8);                              // 散成烟
		float e = sdn + torn;
		float dens = smoothstep(0.16, -0.22, e) * (1.0 - dj * 0.6);
		// 颜色：近黑的烟；边里面断断续续透一点暗红；朝上一侧一道细的暗红边光（头只在上半边，下颚不勾线）
		float lit = 0.55 + 0.45 * smoothstep(-0.5, 0.8, -n2.y);
		float shell = smoothstep(-0.34, -0.05, e) * (1.0 - smoothstep(-0.05, 0.1, e));
		float upOnly = mix(1.0, smoothstep(-0.2, 0.15, qu.y), headness);
		vec3 emit = vec3(0.6, 0.05, 0.07) * shell * smoothstep(0.5, 0.82, flick) * 1.1 * lit * (1.0 + dj * 1.5) * upOnly;
		vec3 inner = mix(vec3(0.008, 0.002, 0.004), vec3(0.045, 0.01, 0.016), smoothstep(0.4, 0.9, nz2));
		float topRim = smoothstep(0.72, 0.95, rr) * smoothstep(0.35, 0.9, -n2.y) * (1.0 - smoothstep(-0.04, 0.04, e)) * upOnly;   // 只在最上沿一细条
		inner += vec3(0.42, 0.05, 0.06) * topRim * 0.45;
		// 外面往上飘的烟丝；散的时候一大团；几条之间一层薄烟
		float wn = fbm(vec2(L * 0.7 - clock * 0.8, rr * 0.9 + clock * 0.9 * sign(n2.y)) + 7.0 + float(j) * 3.0);
		float wisp = smoothstep(0.5, 0.85, wn) * smoothstep(mix(1.8, 2.8, smoothstep(0.0, 0.8, -n2.y)), 0.9, rr) * 0.6 * (1.0 - headness * 0.75);
		wisp += dj * (1.0 - dj) * 3.0 * smoothstep(0.4, 0.8, wn) * smoothstep(2.9, 0.6, rr);
		wisp = max(wisp, exp(-pow(rr / 1.8, 2.0)) * 0.2 * smoothstep(0.35, 0.75, wn) * smoothstep(3.0, 2.0, rr) * (1.0 - headness));
		float dim = (headness > 0.5 ? root.z : bz) < 0.0 ? 0.8 : 1.0;
		vec3 c = (inner + emit) * dim;
		vec3 wc = vec3(0.03, 0.008, 0.012) + vec3(0.22, 0.02, 0.03) * smoothstep(0.72, 0.9, wn);
		float alphaJ = mix(ba, root.w, headness);
		float a = clamp(dens + wisp * (1.0 - dens), 0.0, 1.0) * alphaJ;
		vec3 cc = mix(wc, c, dens / max(dens + wisp * (1.0 - dens), 1e-3));
		// ── 头上的细节（头的坐标里画，跟着颚动）──
		if (headHere && sdH < 1e8) {
			float hw = smoothstep(-0.3, 0.0, q.x) * (1.0 - dj);
			// 口腔：上唇和下唇之间，嘴角最深（近黑），往吻尖淡掉（前面透出后面）
			if (openA > 0.001 && q.x > HINGE.x) {
				float upLip = lipY(lip_up, qu.x);
				float lowLip = lipY(lip_low, ql.x);
				float inGap = smoothstep(0.015, -0.015, qu.y - upLip) * smoothstep(-0.015, 0.015, ql.y - lowLip);
				float th = inGap * clamp((1.35 - q.x) / 0.6, 0.0, 1.0) * hw;
				vec3 throatC = mix(vec3(0.015, 0.002, 0.005), vec3(0.27, 0.047, 0.07), smoothstep(HINGE.x, HINGE.x + 0.55, q.x)) * (0.85 + 0.3 * nz);
				cc = mix(cc, throatC, th * (1.0 - dens));
				a = max(a, th * root.w);
			}
			// 舌头：细长、末端分叉，一伸一缩、抖；隐约一道暗红
			if (openA > 0.001) {
				vec2 qt = rotAbout(q, HINGE, openA * 0.5);
				float flickT = 0.5 + 0.5 * sin(clock * 3.1 + float(j) * 2.3);
				float tipX = mix(1.55, 2.2, flickT * flickT);
				float wav = sin(qt.x * 9.0 - clock * 22.0) * 0.025 * smoothstep(1.2, 2.0, qt.x);
				float cy = -0.17 + wav;
				float fork = smoothstep(tipX - 0.24, tipX, qt.x);
				float dy = min(abs(qt.y - cy - fork * 0.09), abs(qt.y - cy + fork * 0.09));
				float tw = 0.024 * (1.0 - 0.5 * smoothstep(tipX - 0.5, tipX, qt.x));
				float tongue = smoothstep(tw + 0.02, tw - 0.01, dy) * smoothstep(0.75, 0.9, qt.x) * smoothstep(tipX + 0.02, tipX - 0.04, qt.x);
				tongue *= hw * (0.55 + 0.25 * fine);
				cc = mix(cc, vec3(0.5, 0.07, 0.11), tongue * (1.0 - dens * 0.7));
				a = max(a, tongue * 0.75 * root.w);
			}
			// 獠牙：示意图的位置和长度（受开口限制），模糊的烟痕；上排长、下排短，近的亮、远的稍后且暗
			float gapK = tan(openA + upA);
			float fang = 0.0;
			for (int f = 0; f < 4; f++) {
				bool upper = f < 2;
				bool far_ = f == 1 || f == 3;
				vec2 base = upper ? (far_ ? vec2(1.14, -0.09) : vec2(1.24, -0.085)) : (far_ ? vec2(0.93, -0.105) : vec2(1.02, -0.1));
				float Lf = min(upper ? (far_ ? 0.28 : 0.36) : (far_ ? 0.17 : 0.22), (base.x - HINGE.x) * gapK * (upper ? (far_ ? 0.5 : 0.62) : (far_ ? 0.32 : 0.4)));
				vec2 fq = upper ? qu : ql;
				float t = clamp((upper ? base.y - fq.y : fq.y - base.y) / max(Lf, 1e-3), -0.2, 1.2);
				float cx = base.x - 0.12 * Lf * t * t;
				float wid = 0.04 * (1.0 - clamp(t, 0.0, 1.0)) + 0.006;
				float fd = abs(fq.x - cx) - wid + (fine - 0.5) * 0.02;
				float m = smoothstep(0.025, -0.01, fd) * smoothstep(-0.05, 0.03, t) * smoothstep(1.05, 0.85, t) * step(0.02, Lf);
				fang = max(fang, m * (far_ ? 0.45 : 1.0));
			}
			fang *= hw;
			vec3 fangC = mix(vec3(0.3, 0.12, 0.11), vec3(0.75, 0.58, 0.5), smoothstep(0.3, 0.8, nz2));
			cc = mix(cc, fangC, fang * 0.85);
			a = max(a, fang * 0.85 * root.w);
		}
		over(acc, cc, a);
		// 眼睛：一团晕开的暖红雾光（只在头所在的那一层）
		if (headHere) {
			vec2 ed = p - eyes[j].xy;
			float e2 = dot(ed, ed);
			glow += (vec3(1.0, 0.6, 0.3) * exp(-e2 / pow(g * 0.14, 2.0)) * 2.0 + vec3(0.95, 0.15, 0.08) * exp(-e2 / pow(g * 0.42, 2.0)) * 0.55) * eyes[j].z * (1.0 - dj);
		}
	}
	// 咒烟团（前景层）：蛇从烟里聚出来的那一团、咬中时往外炸开的那一团、蛇散掉时的那一团。翻滚着变大、变淡
	if (side > 0.0) {
		for (int i = 0; i < 6; i++) {
			if (puff[i].z < 0.0) continue;
			float k = puff[i].z;
			float sz = abs(puff[i].w);
			vec2 dv = (p - puff[i].xy) / sz;
			float d = length(dv);
			if (d > 2.2) continue;
			float pn = fbm(dv * 2.4 + vec2(0.0, -k * 1.6) + float(i) * 7.0);
			float pf = vn(dv * 6.0 - k * 3.0 + float(i));
			float R = 0.45 + 0.75 * ease3(k);
			float body = smoothstep(R, R * 0.25, d + (pn - 0.5) * 0.6 + (pf - 0.5) * 0.12) * pow(1.0 - k, 1.3);
			float hole = puff[i].w < 0.0 ? mix(1.0, smoothstep(0.0, R * 0.7, d), smoothstep(0.0, 0.4, k)) : 1.0;   // 炸开的那团中间先空
			vec3 pc = mix(vec3(0.015, 0.003, 0.007), vec3(0.09, 0.016, 0.024), smoothstep(0.35, 0.8, pn)) + vec3(0.45, 0.04, 0.06) * smoothstep(0.62, 0.85, pf) * (1.0 - k) * 0.6;
			over(acc, pc, clamp(body * hole, 0.0, 1.0) * 0.9);
			if (puff[i].w < 0.0) glow += vec3(1.0, 0.35, 0.22) * exp(-d * d / 0.08) * pow(1.0 - k, 5.0) * 0.9;
		}
	}

	// 蛇巢（背景层，在蛇后面）：Joker 身后一团翻滚的咒烟，里面零星透出暗红的光，往上飘起烟丝
	if (side < 0.0 && nest_a > 0.0) {
		vec2 dv = (p - nest.xy) / nest.z;
		float d = length(dv * vec2(0.85, 1.15));
		float nn = fbm(dv * 1.6 + vec2(clock * 0.15, clock * 0.35));
		float nf = vn(dv * 4.5 - vec2(0.0, clock * 0.8));
		float body = smoothstep(1.0, 0.25, d + (nn - 0.5) * 0.6 + (nf - 0.5) * 0.12);
		float rise = smoothstep(0.5, 0.85, fbm(vec2(dv.x * 2.0, dv.y * 1.2 + clock * 0.6) + 4.0)) * smoothstep(1.9, 0.6, d) * smoothstep(0.6, -0.6, dv.y) * 0.5;
		vec3 nc = mix(vec3(0.015, 0.003, 0.007), vec3(0.09, 0.016, 0.024), smoothstep(0.35, 0.8, nn));
		nc += vec3(0.4, 0.035, 0.05) * smoothstep(0.7, 0.9, nf) * smoothstep(0.9, 0.3, d) * 0.3;
		over(acc, nc, clamp(body * 0.92 + rise * (1.0 - body), 0.0, 1.0) * nest_a);
	}
	acc.rgb += glow * (1.0 - acc.a * 0.4);
	acc.a = clamp(acc.a + dot(glow, vec3(0.33)) * 0.6, 0.0, 1.0);
	if (acc.a <= 0.002) discard;
	COLOR = acc;
}
" };




	/// <summary>
	/// 五条蛇各自的游动路线（第三版，用户：一层一层的像魂环，太怪；分布自然一些、大概围绕就行；不要只有水平运动，要斜着上下）。
	/// 每条是一个斜放的利萨如曲线（椭圆 / 8 字），再叠一点慢慢漂的偏移：头会斜着往上游、再斜着往下沉，几条的高低、远近、快慢都错开，
	/// 只是大致围着 Joker，不是一圈圈地绕。
	/// Cx, Cy：中心（身高为单位，相对脚底，x 朝敌人为正）；Ax, Ay：两个方向的幅度；W：角速度；R：竖直方向的频率比（1 椭圆、2 八字、1.5 扭一下）；
	/// Ph：相位；Tilt：整条路线斜放的角度（度，正 = 朝敌人那边往上翘）；Za, Zb：深度 z = cos(角) * Za + Zb（&gt; 0 时在 Joker 前面）；粗细；身长。
	/// </summary>
	private static readonly (float Cx, float Cy, float Ax, float Ay, float W, float R, float Ph, float Tilt, float Za, float Zb, float Size, float Len)[] Orbits =
	{
		(0.15f, -1.4f, 0.5f, 0.3f, 0.42f, 1f, 0.0f, 28f, 0.6f, -0.45f, 1.05f, 2.3f),     // 身后偏右上，斜着一高一低地兜
		(-0.4f, -0.9f, 0.45f, 0.35f, -0.55f, 1f, 1.3f, -42f, 1f, 0.05f, 0.95f, 2.2f),   // 左边，斜着从他身前穿过去再绕回身后
		(0.5f, -1.0f, 0.38f, 0.42f, 0.36f, 2f, 2.4f, 12f, 1f, -0.2f, 0.95f, 2.2f),      // 右边（敌人那侧），竖着的 8 字，上下起伏大
		(-0.05f, -0.42f, 0.72f, 0.16f, 0.46f, 1f, 3.6f, -10f, 1f, 0.05f, 0.9f, 2.4f),   // 低处，绕着腿，略斜
		(-0.3f, -1.72f, 0.38f, 0.26f, -0.38f, 1.5f, 4.8f, 35f, 0.5f, -0.5f, 0.85f, 2.2f), // 最高处偏左，身后
	};

	private readonly Control _front, _back;
	private Func<(Vector2 Feet, float Tall, float Face)?> _body;
	private bool _live;
	private readonly Node2D _frontRoot, _backRoot;
	private readonly ShaderMaterial _frontM, _backM;
	private readonly SceneTree _tree;
	private readonly Vector4[] _spine = new Vector4[Count * N];
	private readonly Vector4[] _box = new Vector4[Count];
	private readonly float[] _girth = new float[Count];
	private readonly Vector2[] _hup = new Vector2[Count];
	private readonly Vector3[] _eyes = new Vector3[Count];
	private readonly float[] _diss = new float[Count];
	private readonly Vector4[] _puff = new Vector4[6];
	private readonly float[] _mouth = new float[Count];
	private readonly Queue<(Vector2 At, Node2D? Body)> _targets = new();
	private readonly Attack?[] _attacks = new Attack?[Slots];
	private float _t, _show = -1f, _intro = -1f, _fadeOut = -1f, _reveal;
	private readonly float[] _tau = new float[Count];        // 每条蛇自己的时间（出动时会快一下）
	private readonly Vector2[] _lunge = new Vector2[Count];  // 出动时朝敌人窜出去的偏移
	private readonly Node2D _skyRoot;
	private readonly ShaderMaterial _skyM;
	private int _nextSerpent;

	private sealed class Attack
	{
		public int Serpent;
		public Vector2 Target;            // 相对脚底
		public float T;
		public bool Bit, Emerged, Faded;
		public Node2D? Body;              // 被咬的敌人（咬中时闪一下白）
	}

	/// <summary>
	/// 开始（已经有就沿用）。body 每帧给出 Joker 的脚底（层坐标）、身高、朝向；拿不到就返回 null，蛇跟着消失。
	/// live = body 跟着真的 Joker 走（能力挂上时给的）；false = 只是出牌时的一个定点（之后有 live 的就换掉）。
	/// intro = 播出场（打出群蛇形态）；不播出场时 delay 秒后静静浮现。
	/// </summary>
	public static SerpentMantle Start(Control front, Control back, Func<(Vector2 Feet, float Tall, float Face)?> body, bool live, bool intro, float delay = 0f)
	{
		if (Current != null && Current.Valid && Current._fadeOut < 0f)
		{
			if (live && !Current._live) { Current._body = body; Current._live = true; }
			if (intro) Current.Intro();
			return Current;
		}
		Current?.Free();
		Current = new SerpentMantle(front, back, body, live);
		if (intro) Current.Intro(); else Current._show = -delay;
		return Current;
	}

	public static void StopCurrent()
	{
		if (Current != null && Current._fadeOut < 0f) Current._fadeOut = 0f;
	}

	private SerpentMantle(Control front, Control back, Func<(Vector2 Feet, float Tall, float Face)?> body, bool live)
	{
		_front = front;
		_back = back;
		_body = body;
		_live = live;
		(Vector2 feet, float tall, float _) = body() ?? (Vector2.Zero, 300f, 1f);
		var rect = new Rect2(-3f * tall, -2.9f * tall, 10f * tall, 3.6f * tall);
		(_frontRoot, _frontM) = Make(front, feet, rect, 1f, "P5SerpentsFront");
		(_backRoot, _backM) = Make(back, feet, rect, -1f, "P5SerpentsBack");
		for (int i = 0; i < Count; i++) _girth[i] = 0.068f * tall * Orbits[i].Size;   // 修长：细一些、长一些（第三版：再细，用户说太肥）
		_skyRoot = Fx.ScreenRoot(back, "P5SerpentSky");
		back.MoveChild(_skyRoot, 0);
		_skyM = new ShaderMaterial { Shader = EclipseSky.Shader };
		var skyRect = new ColorRect { Size = Fx.Screen(back), Material = _skyM, MouseFilter = Control.MouseFilterEnum.Ignore };
		_skyRoot.AddChild(skyRect);
		Fx.Clock(skyRect);
		for (int k = 0; k < _puff.Length; k++) _puff[k] = new Vector4(0f, 0f, -1f, 1f);
		_tree = (SceneTree)Engine.GetMainLoop();
		_tree.ProcessFrame += Tick;
		_frontRoot.TreeExiting += Detach;
	}

	private bool Valid => GodotObject.IsInstanceValid(_frontRoot) && GodotObject.IsInstanceValid(_backRoot);

	private static (Node2D, ShaderMaterial) Make(Control layer, Vector2 anchor, Rect2 rect, float side, string name)
	{
		Node2D root = new() { Name = name };
		layer.AddChild(root);
		root.GlobalPosition = anchor;
		var m = new ShaderMaterial { Shader = Shade };
		var q = new ColorRect { Position = rect.Position, Size = rect.Size, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		m.SetShaderParameter("origin", rect.Position);
		m.SetShaderParameter("size", rect.Size);
		m.SetShaderParameter("side", side);
		PushHead(m);
		Fx.Clock(q);
		return (root, m);
	}

	/// <summary>出场：一团咒烟炸开，五条蛇一条接一条冲出来。已经在场上就只让五个头一起转向敌人吼一下。</summary>
	private void Intro()
	{
		if (_show > 0.3f) { _intro = 1.0f; return; }          // 已经在了：直接跳到「一起转头」那一段
		_intro = 0f;
		_show = 1f;
		if (_body() is var (feet, tall, _))
		{
			Fx.After(_frontRoot, 0.18, () =>
			{
				Glow.Flash(_back, feet + new Vector2(0f, -0.6f * tall), Pal5.Deep, 2.6f * tall, 0.5f);
				Glow.Flash(_front, feet + new Vector2(0f, -0.6f * tall), Pal5.Crimson, 1.2f * tall, 0.25f);
				Glow.Sparks(_front, feet + new Vector2(0f, -0.5f * tall), 40, 150f, 650f, 0.8f, new[] { Pal5.Bright, Pal5.Crimson, Pal5.Deep }, -120f, 0.12f, 0.4f, Vector2.Up, 180f);
				Fx.Shake(2);
			});
		}
	}

	/// <summary>能力触发：一条蛇朝 target（层坐标，敌人身体中间）吐一颗法球。</summary>
	public void Strike(Vector2 target, Node2D? body = null)
	{
		if (_targets.Count < 8) _targets.Enqueue((target, body));
	}

	private void Detach()
	{
		_tree.ProcessFrame -= Tick;
		if (Current == this) Current = null;
	}

	private void Free()
	{
		Detach();
		if (GodotObject.IsInstanceValid(_frontRoot)) _frontRoot.QueueFree();
		if (GodotObject.IsInstanceValid(_backRoot)) _backRoot.QueueFree();
		if (GodotObject.IsInstanceValid(_skyRoot)) _skyRoot.QueueFree();
	}

	private static float Smooth(float a, float b, float x) { float t = Mathf.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3f - 2f * t); }

	/// <summary>第 i 条蛇的头在时刻 t 的位置（相对脚底，像素）和深度。</summary>
	private static Vector3 Orbit(int i, float t, float T, float face)
	{
		var o = Orbits[i];
		float a = o.W * t + o.Ph;
		Vector2 l = new(o.Ax * Mathf.Sin(a), o.Ay * Mathf.Sin(o.R * a + 1.57f + 0.6f * i));
		l = l.Rotated(-Mathf.DegToRad(o.Tilt));                          // 斜放（屏幕 y 朝下，所以取负）
		// 慢慢漂：整条路线自己也在挪，不会一直重复同一个圈
		l += new Vector2(0.08f * Mathf.Sin(t * 0.23f + i * 2.1f), 0.1f * Mathf.Sin(t * 0.31f + i * 1.3f));
		float z = Mathf.Cos(a) * o.Za + o.Zb;
		return new Vector3((o.Cx + l.X) * face * T, (o.Cy + l.Y) * T, z);
	}

	private void Tick()
	{
		if (!Valid || !GodotObject.IsInstanceValid(_front) || !GodotObject.IsInstanceValid(_back) || _body() is not var (feet, T, face))
		{
			Free();
			return;
		}
		float dt = Mathf.Min((float)_frontRoot.GetProcessDeltaTime(), 0.1f);   // 游戏帧时间（顿帧、录屏时都对）
		_t += dt;
		for (int i = 0; i < Count; i++)
		{
			// 出动的那条：先猛地快一下，再慢下来（配合散成烟）
			float boost = 0f;
			if (AttackOf(i) is { } ak) boost = 5f * Smooth(0f, 0.07f, ak.T) * (1f - Smooth(0.07f, 0.4f, ak.T));
			_tau[i] += dt * (1f + boost);
		}
		_show += dt;
		if (_intro >= 0f) { _intro += dt; if (_intro > 2.2f) _intro = -1f; }
		if (_fadeOut >= 0f)
		{
			_fadeOut += dt;
			if (_fadeOut > 0.8f) { Free(); return; }
		}
		_frontRoot.GlobalPosition = feet;
		_backRoot.GlobalPosition = feet;
		float alpha = (_intro >= 0f ? 1f : Smooth(0f, 0.8f, _show)) * (_fadeOut >= 0f ? 1f - Smooth(0f, 0.8f, _fadeOut) : 1f);
		// 场景：出场时从 Joker 脚下漫开（0.7s），静静浮现时淡入；能力没了一起淡掉
		_reveal = _intro >= 0f ? Mathf.Max(_reveal, Ease5.OutQuint(Ease5.Seg(_intro, 0.05f, 0.75f))) : Mathf.Max(_reveal, Smooth(0f, 0.8f, _show));
		Vector2 scr = Fx.Screen(_back);
		Vector2 fs = Fx.ScreenPoint(_back, feet);
		float tallS = T * _back.GetCanvasTransform().Scale.Y;
		// 场景：另一个会话的日蚀天色（Vfx/EclipseSky.cs），压一点饱和度、底色盖实；蛇巢那里正好是它「蛇身没入」的翻涌红雾
		_skyM.SetShaderParameter("amount", _fadeOut >= 0f ? 1f - Smooth(0f, 0.8f, _fadeOut) : (_show > 0f || _intro >= 0f ? 1f : 0f));
		_skyM.SetShaderParameter("reveal", _reveal);
		_skyM.SetShaderParameter("desat", 0.3f);
		_skyM.SetShaderParameter("cover", 0.62f);
		_skyM.SetShaderParameter("disk_r", 0.07f);
		_skyM.SetShaderParameter("bleed", 1f);
		_skyM.SetShaderParameter("blend", 1f);
		_skyM.SetShaderParameter("aspect", scr.X / scr.Y);
		_skyM.SetShaderParameter("from_uv", new Vector2(fs.X / scr.X, (fs.Y - 0.5f * tallS) / scr.Y));
		_skyM.SetShaderParameter("ground", fs.Y / scr.Y);
		_skyM.SetShaderParameter("sun", new Vector2(Mathf.Clamp((fs.X + face * tallS * 1.7f) / scr.X, 0.15f, 0.85f), 0.15f));
		_skyM.SetShaderParameter("pool", new Vector2((fs.X - 0.12f * face * tallS) / scr.X, (fs.Y - 0.05f * tallS) / scr.Y));
		_skyM.SetShaderParameter("pool_w", tallS * 1.1f / scr.X);
		Vector2 nestP = new(-0.12f * face * T, -0.78f * T);

		// 攻击：有目标、有空位、出场演完了 → 挑一条闲着的蛇
		if (_targets.Count > 0 && _show > 0.6f && (_intro < 0f || _intro > 1.6f))
		{
			for (int s = 0; s < Slots; s++)
			{
				if (_attacks[s] != null) continue;
				int serp = -1;
				for (int k = 0; k < Count; k++)
				{
					int c = (_nextSerpent + k) % Count;
					if (AttackOf(c) == null) { serp = c; break; }
				}
				if (serp < 0) break;
				_nextSerpent = (serp + 2) % Count;
				var (tat, tbody) = _targets.Dequeue();
				_attacks[s] = new Attack { Serpent = serp, Target = tat - feet, Body = tbody };
				Sfx.Play(Sfx.SerpentMaw, 0.8f, BiteAt - 0.01f);   // 只有咬合的声音（用户：冲出来不要音效）
				break;
			}
		}

		for (int i = 0; i < Count; i++)
		{
			// 扑咬时那条蛇放大一些（更有气势），回到轨道上恢复
			Attack? sa = AttackOf(i);
			float big = sa != null ? Smooth(EmergeAt - 0.02f, EmergeAt + 0.02f, sa.T) * (1f - Smooth(LeaveAt - 0.02f, LeaveAt, sa.T)) : 0f;
			_girth[i] = 0.068f * T * Orbits[i].Size * (1f + 0.45f * big);
			PoseSerpent(i, T, face, alpha);
		}
		UpdateAttacks(dt, feet, T);

		for (int j = 0; j < Count; j++)
		{
			float m = _girth[j] * 3.4f;
			Vector2 lo = new(1e9f, 1e9f), hi = new(-1e9f, -1e9f);
			for (int k = 0; k < N; k++)
			{
				Vector4 q = _spine[j * N + k];
				lo = new Vector2(Mathf.Min(lo.X, q.X), Mathf.Min(lo.Y, q.Y));
				hi = new Vector2(Mathf.Max(hi.X, q.X), Mathf.Max(hi.Y, q.Y));
			}
			_box[j] = new Vector4(lo.X - m, lo.Y - m, hi.X + m, hi.Y + m);
		}
		foreach (ShaderMaterial mat in new[] { _frontM, _backM })
		{
			mat.SetShaderParameter("spine", _spine);
			mat.SetShaderParameter("box", _box);
			mat.SetShaderParameter("girth", _girth);
			mat.SetShaderParameter("hup", _hup);
			mat.SetShaderParameter("eyes", _eyes);
			mat.SetShaderParameter("unit", T);
			mat.SetShaderParameter("diss", _diss);
			mat.SetShaderParameter("nest", new Vector3(nestP.X, nestP.Y, 0.55f * T));
			mat.SetShaderParameter("nest_a", alpha * (_intro >= 0f ? Smooth(0.05f, 0.4f, _intro) : 1f));
			mat.SetShaderParameter("puff", _puff);
			mat.SetShaderParameter("mouth", _mouth);
		}
	}

	private Attack? AttackOf(int serpent)
	{
		foreach (Attack? a in _attacks) if (a != null && a.Serpent == serpent) return a;
		return null;
	}

	// 攻击：0～0.28 在原轨道上猛窜、散成烟 → 0.28 敌人斜上方一团咒烟，这条蛇的头和前半身从烟里聚出来、往后一缩、嘴张大
	// → 0.38～0.46 猛地扑下去咬（BiteAt）→ 0.5～0.75 散成烟 → 0.78 起回到原轨道上慢慢聚起来
	private const float EmergeAt = 0.28f, LeaveAt = 0.78f;

	/// <summary>扑咬时的样子：尾巴藏在敌人斜上方的烟团里，S 形的前半身，头先往后缩、再扑向目标。</summary>
	private void StrikePose(int i, Attack atk, float T, float face, float alpha)
	{
		int nb = N - HeadPts;
		float t = atk.T;
		Vector2 P = atk.Target;
		Vector2 A = P + new Vector2(-0.45f * face * T, -0.95f * T);         // 烟团
		Vector2 cocked = P + new Vector2(-0.5f * face * T, -0.55f * T);     // 往后缩、准备扑
		float headLen = _girth[i] * 2.16f;   // 照示意图：头根（x=-0.3）到吻尖（1.56）= 1.86 W，W = 1.16 g
		float lunge = Ease5.Seg(t, 0.38f, BiteAt - 0.38f);
		lunge = lunge * lunge;
		float rear = Smooth(EmergeAt, 0.38f, t);
		Vector2 toP = (P - cocked).Normalized();
		Vector2 bite = P - toP * headLen * 0.75f;
		Vector2 hb = cocked.Lerp(cocked - toP * 0.12f * T, rear * (1f - lunge)).Lerp(bite, lunge);
		Vector2 c1 = A + new Vector2(0.1f * face * T, 0.35f * T);
		Vector2 c2 = hb - toP * 0.45f * T + new Vector2(0f, -0.15f * T);
		Vector2 tail = A + new Vector2(-0.15f * face * T, -0.1f * T);
		for (int k = 0; k < nb; k++)
		{
			float f = k / (nb - 1f);
			float u = 1f - f;
			Vector2 q = u * u * u * tail + 3f * u * u * f * c1 + 3f * u * f * f * c2 + f * f * f * hb;
			float wave = Mathf.Sin(f * 8f - _t * 5f) * 0.03f * T * (1f - f);
			q += new Vector2(0f, wave);
			float a = Smooth(0.15f, 0.4f, f) * alpha;                         // 尾巴那截藏在烟里
			_spine[i * N + k] = new Vector4(q.X, q.Y, 0.5f, a);
		}
		Vector2 dir = NeckDir(i, face);                                   // 头顺着脖子
		for (int k = 1; k <= HeadPts; k++)
		{
			Vector2 hp = hb + dir * headLen * k / HeadPts;
			_spine[i * N + nb - 1 + k] = new Vector4(hp.X, hp.Y, 0.5f, alpha);
		}
		Vector2 up = new(dir.Y, -dir.X);
		if (up.Y > 0f) up = -up;
		_hup[i] = up;
		Vector2 eye = hb + dir * _girth[i] * 1.16f * 1.12f + up * _girth[i] * 1.16f * 0.3f;   // 示意图的眼睛 (0.82, 0.30)
		_eyes[i] = new Vector3(eye.X, eye.Y, alpha);
		// 嘴：聚出来以后张到最大，扑到时猛地合上
		_mouth[i] = t < BiteAt ? Smooth(EmergeAt, 0.36f, t) : -0.2f;
	}

	/// <summary>脖子末端的方向（头就朝这个方向放）。</summary>
	private Vector2 NeckDir(int i, float face)
	{
		int nb = N - HeadPts;
		Vector4 a = _spine[i * N + nb - 3], b = _spine[i * N + nb - 1];
		Vector2 d = new(b.X - a.X, b.Y - a.Y);
		return d.LengthSquared() > 1e-3f ? d.Normalized() : new Vector2(face, 0f);
	}

	/// <summary>把脖子最后 8 个点绕第 9 个点逐渐弯过去，让末端朝 target 方向（amount 0～1）。</summary>
	private void BendNeck(int i, Vector2 target, float amount)
	{
		int nb = N - HeadPts, piv = nb - 9;
		float ang = NeckDir(i, 1f).AngleTo(target) * amount;
		Vector4 pv = _spine[i * N + piv];
		Vector2 c = new(pv.X, pv.Y);
		for (int k = piv + 1; k < nb; k++)
		{
			float w = Smooth(piv, nb - 1, k);
			Vector4 v = _spine[i * N + k];
			Vector2 r = (new Vector2(v.X, v.Y) - c).Rotated(ang * w) + c;
			_spine[i * N + k] = new Vector4(r.X, r.Y, v.Z, v.W);
		}
	}

	private void PoseSerpent(int i, float T, float face, float alpha)
	{
		var o = Orbits[i];
		_mouth[i] = 0f;
		if (AttackOf(i) is { } sa && sa.T >= EmergeAt && sa.T < LeaveAt)
		{
			StrikePose(i, sa, T, face, alpha);
			return;
		}
		// 出场：每条蛇从 Joker 胸口冲出来、甩到自己的轨道上
		float grow = 1f;
		if (_intro >= 0f && _intro < 1.6f)
		{
			grow = Ease5.OutQuint(Ease5.Seg(_intro, 0.18f + 0.09f * i, 0.75f));
		}
		Vector3 chest = new(0f, -0.7f * T, 0.3f);
		int nb = N - HeadPts;
		// 沿头走过的路往回取（越老的位置越往下沉一点，绕圈的蛇就成了松开的螺旋、不会叠成一团），按路程铺满身长
		const int D = 220;
		Span<Vector3> trail = stackalloc Vector3[D];
		Span<float> acc = stackalloc float[D];
		float want = o.Len * T * o.Size, step = 0.06f;
		int used = D;
		for (int k = 0; k < D; k++)
		{
			float back = k * step;
			Vector3 v = Orbit(i, _tau[i] - back, T, face);
			trail[k] = v;
			acc[k] = k == 0 ? 0f : acc[k - 1] + new Vector2(v.X - trail[k - 1].X, v.Y - trail[k - 1].Y).Length();
			if (acc[k] >= want) { used = k + 1; break; }
		}
		float total = acc[used - 1];
		for (int k = 0; k < nb; k++)
		{
			float f = k / (nb - 1f);                       // 0 尾 → 1 头根
			float s = total * (1f - f);                    // 离头的路程
			int seg = 0;
			while (seg < used - 2 && acc[seg + 1] < s) seg++;
			float u = acc[seg + 1] > acc[seg] ? Mathf.Clamp((s - acc[seg]) / (acc[seg + 1] - acc[seg]), 0f, 1f) : 0f;
			Vector3 v = trail[seg].Lerp(trail[seg + 1], u);
			// 沿身体往尾巴传的 S 波
			float wave = Mathf.Sin(f * 9f - _t * 3.4f + i) * 0.065f * T * (0.25f + 0.75f * (1f - f));
			v += new Vector3(wave * 0.4f, wave, 0f);
			// 出动：朝敌人窜一下（越靠头越多）
			v += new Vector3(_lunge[i].X, _lunge[i].Y, 0f) * Mathf.Pow(f, 1.5f);
			// 一体：尾巴都收进 Joker 身后的蛇巢（同一团咒烟里伸出五个头）
			float pull = 1f - Smooth(0f, 0.5f, f);
			Vector3 nest3 = new(-0.12f * face * T + Mathf.Sin(_t * 0.7f + i * 1.3f) * 0.12f * T, -0.78f * T + Mathf.Cos(_t * 0.6f + i * 2.1f) * 0.1f * T, -0.5f);
			v = v.Lerp(nest3, pull * pull * 0.92f);
			v = chest.Lerp(v, grow);
			float a = Smooth(0f, 0.08f, f) * alpha;
			_spine[i * N + k] = new Vector4(v.X, v.Y, v.Z, a * (_intro >= 0f ? Smooth(0f, 0.15f, grow) : 1f));
		}
		// 出场最后五个头一起转向敌人：不单独转头，而是把脖子最后一截弯过去（头永远顺着脖子，不会错开）
		Vector2 enemyDir = new Vector2(face, 0.12f).Normalized();
		float turn = _intro >= 0f ? Smooth(1.05f, 1.25f, _intro) * (1f - Smooth(1.9f, 2.2f, _intro)) : 0f;
		float eyes = 0.45f + 0.1f * Mathf.Sin(_t * 2f + i);
		if (turn > 0f)
		{
			BendNeck(i, enemyDir, turn);
			eyes = Mathf.Max(eyes, turn);
		}
		Vector4 hb4 = _spine[i * N + nb - 1];
		Vector2 hb = new(hb4.X, hb4.Y);
		Vector2 dir = NeckDir(i, face);
		float headLen = _girth[i] * 2.16f;   // 照示意图：头根（x=-0.3）到吻尖（1.56）= 1.86 W，W = 1.16 g
		for (int k = 1; k <= HeadPts; k++)
		{
			Vector2 hp = hb + dir * headLen * k / HeadPts;
			_spine[i * N + nb - 1 + k] = new Vector4(hp.X, hp.Y, hb4.Z, hb4.W);
		}
		Vector2 up = new(dir.Y, -dir.X);
		if (up.Y > 0f) up = -up;
		_hup[i] = up;
		Vector2 eye = hb + dir * _girth[i] * 1.16f * 1.12f + up * _girth[i] * 1.16f * 0.3f;   // 示意图的眼睛 (0.82, 0.30)
		_eyes[i] = new Vector3(eye.X, eye.Y, eyes * hb4.W);
	}

	private void Puff(Vector2 at, float progress, float size, int slot) => _puff[slot] = new Vector4(at.X, at.Y, progress, size);

	private void UpdateAttacks(float dt, Vector2 feet, float T)
	{
		for (int i = 0; i < Count; i++) { _diss[i] = 0f; _lunge[i] = Vector2.Zero; }
		for (int k = 0; k < _puff.Length; k++) _puff[k].Z = -1f;
		for (int s = 0; s < Slots; s++)
		{
			if (_attacks[s] is not { } a) continue;
			a.T += dt;
			float t = a.T;
			float face = a.Target.X >= 0f ? 1f : -1f;
			Vector2 cloud = a.Target + new Vector2(-0.45f * face * T, -0.95f * T);
			// 散 / 聚：轨道上散掉 → 烟里聚出来 → 咬完散掉 → 回轨道上聚起来
			_diss[a.Serpent] = t < EmergeAt ? Smooth(0.06f, EmergeAt, t)
				: t < 0.5f ? 1f - Smooth(EmergeAt, 0.36f, t)
				: t < LeaveAt ? Smooth(0.5f, 0.74f, t)
				: 1f - Smooth(LeaveAt, 1.12f, t);
			if (t < EmergeAt)
			{
				// 猛窜：朝敌人窜出去，边窜边散
				Vector3 hp = Orbit(a.Serpent, _tau[a.Serpent], T, 1f);
				_lunge[a.Serpent] = (a.Target - new Vector2(hp.X, hp.Y)).LimitLength(0.45f * T) * Ease5.OutQuint(Ease5.Seg(t, 0f, 0.18f));
			}
			// 咒烟团：聚出来的那一团（烟团处）、咬中往外炸的那一团（敌人身上）、散掉的那一团（蛇身中间）
			if (t >= EmergeAt - 0.04f) Puff(cloud, Ease5.Seg(t, EmergeAt - 0.04f, 0.7f), 0.5f * T, s * 2);
			if (t >= BiteAt) Puff(a.Target, Ease5.Seg(t, BiteAt, 0.55f), -0.42f * T, s * 2 + 1);
			if (!a.Bit && t >= BiteAt)
			{
				a.Bit = true;
				Vector2 at = feet + a.Target;
				Glow.Flash(_front, at, Pal5.Crimson, 0.8f * T, 0.22f);
				Glow.Sparks(_front, at, 22, 180f, 600f, 0.5f, new[] { Pal5.Bright, Pal5.Crimson, Pal5.Deep }, 250f, 0.1f, 0.32f, Vector2.Up, 160f);
				if (a.Body != null && GodotObject.IsInstanceValid(a.Body)) BodyTint.Pulse(a.Body, new Color(2.2f, 2.2f, 2.2f), 0.05, 0.2);   // 被咬的敌人闪一下白
				Fx.Shake(2);
				HitStop.Request(0.04);
			}
			if (t > AttackLen) _attacks[s] = null;
		}
	}
}
