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
	private const int Slots = 5;         // 同时最多几条蛇在咬（2026-11-13：3 → 5，连出牌时不排队等）
	private const float BiteAt = 0.46f, AttackLen = 1.15f;
	/// <summary>能力触发的那一咬从攻击动画的哪一刻开始（用户 2026-11-13：伤害都出了蛇才慢慢去咬 → 跳过前面在轨道上猛窜的一段）。</summary>
	private const float StrikeStart = 0f;
	/// <summary>
	/// 咬下去之前那段（轨道上一缩、猛窜、化成烟飞到敌人上方、聚出蛇头、扑咬）的播放倍速。
	/// 用户 2026-11-13：直接从扑咬开始虽然跟手，但「这条蛇加速冲一下、消失、咬完再回来」的表现没了 → 整段保留、加速播，
	/// 从触发到咬合约 0.2s（BiteAt / 2.3），咬完以后（散成烟、回原位）照常速。
	/// </summary>
	private const float PreBiteSpeed = 2.3f;
	/// <summary>能力触发到咬合的秒数（伤害数字推迟到这一刻跳）。</summary>
	public const float StrikeDelay = (BiteAt - StrikeStart) / PreBiteSpeed;

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
uniform vec4 puff[15];          // 咒烟团（每发攻击 3 个：烟团、咬中、原位留下的残烟）：xy 中心，z 进度（0～1；< 0 没有），w 大小（像素；负数 = 咬中那一团，中间带一下光）
uniform vec4 streak[5];        // 攻击的烟迹：xy 尾、zw 头（相对脚底）；蛇在原位散掉的同时化成一道咒烟飞向敌人上方
uniform float streak_a[5];     // 烟迹的浓度（0 = 没有）
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
		// ── 蛇身：脊线 0～27（27 = 颈根）上每一小段是一个圆头的管子 ──
		// 交叉的地方分前后（2026-11-11 用户：提亮以后蛇身一节一节的——以前所有小段取并集当一个形状算明暗，
		// 身体自己交叉 / 两段重叠的地方在交界上折出一道痕）：先找离得最近的那段；如果这个像素同时在身体另一截
		// （脊线上隔得远的段）里面、而那一截更靠前，就用那一截自己的轮廓算明暗，盖在后面那截上。轮廓（透明度）仍取并集。
		float sdArr[27];
		float zArr[27];
		float sdU = 1e9; int kb = -1;
		for (int k = 0; k < 27; k++) {
			sdArr[k] = 1e9; zArr[k] = -9.0;
			vec4 a = spine[j * 32 + k], b = spine[j * 32 + k + 1];
			if (max(a.w, b.w) <= 0.001) continue;
			// 前后两层在 z = 0 附近重叠一段（前景层在那里渐渐淡掉），绕到身后的地方不会断成一个圆头（用户：一节一节的）
			if (side > 0.0 ? max(a.z, b.z) <= -0.05 : min(a.z, b.z) > 0.15) continue;
			vec2 ab = b.xy - a.xy;
			float h = clamp(dot(p - a.xy, ab) / max(dot(ab, ab), 1e-3), 0.0, 1.0);
			vec2 cq = a.xy + ab * h;
			float sk = (float(k) + h) / 27.0;
			float sdk = length(p - cq) - g * bodyR(sk);
			sdArr[k] = sdk; zArr[k] = mix(a.z, b.z, h);
			if (sdk < sdU) { sdU = sdk; kb = k; }
		}
		int kc = kb;
		if (kb >= 0) {
			float zb = zArr[kb], zf = zb + 0.02;
			for (int k = 0; k < 27; k++) {
				if (abs(k - kb) > 3 && sdArr[k] < 0.0 && zArr[k] > zf) { zf = zArr[k]; kc = k; }
			}
		}
		// 选中的那一截：在它前后 2 段里取最近的（同一截里换段是连续的，不会折）
		float sdB = 1e9, bs = 0.0, bz = 0.0, ba = 0.0, br = g;
		vec2 bq = p;
		if (kc >= 0) {
			for (int k = max(kc - 2, 0); k <= min(kc + 2, 26); k++) {
				if (sdArr[k] > 1e8) continue;
				vec4 a = spine[j * 32 + k], b = spine[j * 32 + k + 1];
				vec2 ab = b.xy - a.xy;
				float h = clamp(dot(p - a.xy, ab) / max(dot(ab, ab), 1e-3), 0.0, 1.0);
				vec2 cq = a.xy + ab * h;
				float sk = (float(k) + h) / 27.0;
				float rk = g * bodyR(sk);
				float sdk = length(p - cq) - rk;
				if (sdk < sdB) { sdB = sdk; bs = sk; bz = mix(a.z, b.z, h); ba = mix(a.w, b.w, h); bq = cq; br = rk; }
			}
		}
		bool layered = kc != kb;
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
		float dens = smoothstep(0.16, -0.3, e) * (1.0 - dj * 0.6);   // 边缘更软、半透（2026-11-11 用户：通体乌黑不好看）
		// 前面那截的边缘下面是后面那截的身体，不能透出背景：透明度按并集的轮廓
		if (layered) dens = max(dens, smoothstep(0.16, -0.3, sdU / max(br, 0.5) + 1.0 + torn) * (1.0 - dj * 0.6) * 0.92);
		// 颜色：近黑的烟；边里面断断续续透一点暗红；朝上一侧一道细的暗红边光（头只在上半边，下颚不勾线）
		float lit = 0.55 + 0.45 * smoothstep(-0.5, 0.8, -n2.y);
		float shell = smoothstep(-0.34, -0.05, e) * (1.0 - smoothstep(-0.05, 0.1, e));
		float upOnly = mix(1.0, smoothstep(-0.2, 0.15, qu.y), headness);
		vec3 emit = vec3(0.6, 0.05, 0.07) * shell * smoothstep(0.5, 0.82, flick) * 1.1 * lit * (1.0 + dj * 1.5) * upOnly;
		// 体积（用户 2026-11-11：蛇通体乌漆嘛黑不好看，但太亮又会露出没细节）：只用大面积的柔和过渡，不加线条亮斑——
		// 核心深，往边缘渐渐变成背光透过来的暗红烟；朝上的一侧整体亮一点；烟里的明暗随噪声慢慢流动
		vec3 inner = mix(vec3(0.045, 0.009, 0.014), vec3(0.13, 0.026, 0.034), smoothstep(0.35, 0.9, nz2));
		float transl = smoothstep(0.15, 1.0, rr);
		inner = mix(inner, vec3(0.3, 0.055, 0.06) * (0.8 + 0.4 * nz), transl * 0.8);
		inner *= 0.75 + 0.55 * smoothstep(-0.6, 0.8, -n2.y);
		float topRim = smoothstep(0.72, 0.95, rr) * smoothstep(0.35, 0.9, -n2.y) * (1.0 - smoothstep(-0.04, 0.04, e)) * upOnly;   // 只在最上沿一细条
		inner += vec3(0.42, 0.05, 0.06) * topRim * 0.45;
		// 外面往上飘的烟丝；散的时候一大团；几条之间一层薄烟
		float wn = fbm(vec2(L * 0.7 - clock * 0.8, rr * 0.9 + clock * 0.9 * sign(n2.y)) + 7.0 + float(j) * 3.0);
		float wisp = smoothstep(0.5, 0.85, wn) * smoothstep(mix(1.8, 2.8, smoothstep(0.0, 0.8, -n2.y)), 0.9, rr) * 0.6 * (1.0 - headness * 0.75);
		wisp += dj * (1.0 - dj) * 3.0 * smoothstep(0.4, 0.8, wn) * smoothstep(2.9, 0.6, rr);
		wisp = max(wisp, exp(-pow(rr / 1.8, 2.0)) * 0.2 * smoothstep(0.35, 0.75, wn) * smoothstep(3.0, 2.0, rr) * (1.0 - headness));
		float dim = (headness > 0.5 ? root.z : bz) < 0.0 ? 0.8 : 1.0;
		vec3 c = (inner + emit) * dim;
		vec3 wc = vec3(0.09, 0.02, 0.026) + vec3(0.22, 0.03, 0.04) * smoothstep(0.72, 0.9, wn);
		float alphaJ = mix(ba, root.w, headness);
		float a = clamp(dens + wisp * (1.0 - dens), 0.0, 1.0) * alphaJ;
		if (side > 0.0) a *= mix(smoothstep(-0.05, 0.12, bz), 1.0, headness);   // 前景层：身体快绕到身后时渐渐淡掉，下面接着背景层的那段
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
			glow += (vec3(1.0, 0.6, 0.3) * exp(-e2 / pow(g * 0.14, 2.0)) * 2.0 + vec3(0.95, 0.15, 0.08) * exp(-e2 / pow(g * 0.42, 2.0)) * 0.55) * eyes[j].z * (1.0 - dj * 0.6);   // 散成烟 / 聚出来时眼睛比身体先亮、后灭
		}
	}
	// 咒烟团（前景层）：蛇从烟里聚出来的那一团、咬中时往外炸开的那一团、蛇散掉时的那一团。翻滚着变大、变淡
	if (side > 0.0) {
		for (int i = 0; i < 15; i++) {
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

	// 攻击的烟迹（前景层）：一道越往前越粗的咒烟，烟里一条暗红的芯，最前面带着那条蛇的一对红眼
	if (side > 0.0) {
		for (int i = 0; i < 5; i++) {
			if (streak_a[i] <= 0.001) continue;
			vec2 sa = streak[i].xy, sb = streak[i].zw;
			vec2 ab = sb - sa;
			float L2 = max(dot(ab, ab), 1.0);
			float h = clamp(dot(p - sa, ab) / L2, 0.0, 1.0);
			float d = length(p - (sa + ab * h));
			float w = unit * mix(0.03, 0.2, pow(h, 1.5));
			if (d > w * 3.0 && length(p - sb) > unit * 0.2) continue;
			float sn = fbm(vec2(h * sqrt(L2) / unit * 5.0 - clock * 6.0, d / max(w, 1.0) * 0.8) + float(i) * 5.0);
			float body = smoothstep(w, w * 0.1, d + (sn - 0.5) * w * 1.0) * smoothstep(0.0, 0.25, h);
			vec3 sc = mix(vec3(0.03, 0.006, 0.01), vec3(0.12, 0.022, 0.03), smoothstep(0.4, 0.8, sn));
			sc += vec3(0.5, 0.06, 0.07) * exp(-d * d / pow(w * 0.35, 2.0)) * h * 0.8;
			over(acc, sc, clamp(body * streak_a[i], 0.0, 1.0) * 0.92);
			vec2 ed = p - sb;
			glow += (vec3(1.0, 0.6, 0.3) * exp(-dot(ed, ed) / pow(unit * 0.012, 2.0)) * 2.2 + vec3(0.95, 0.15, 0.08) * exp(-dot(ed, ed) / pow(unit * 0.05, 2.0)) * 0.7) * streak_a[i];
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
		// 第四版（用户 2026-11-11：还是围着他游，只小幅修分布和轨迹；示意图 docs/p5/preview/serpent_orbit_tweak.png）：
		// 每条守着自己那一块、圈小一些、转得一样快（相位错开），不会挤到一边，身体也不会绕成结。
		// 上下也要匀（用户：头部以上和腰部以下不均匀，腰部以下没有）：头顶以上两条、胸口一条、腰胯一条、膝盖一条
		(-0.48f, -1.3f, 0.28f, 0.18f, 0.45f, 1f, 0.0f, 25f, 0.5f, -0.45f, 1.05f, 2.3f),   // 左上，身后（头顶高度）
		(0.32f, -1.55f, 0.3f, 0.15f, -0.42f, 1f, 1.9f, -15f, 0.4f, -0.5f, 0.95f, 2.2f),   // 右上，身后（头顶以上）
		(0.66f, -0.82f, 0.24f, 0.22f, 0.48f, 2f, 3.4f, 15f, 0.8f, -0.15f, 0.95f, 2.2f),   // 右侧胸口高度，朝敌人
		(0.05f, -0.45f, 0.48f, 0.1f, -0.45f, 1f, 4.6f, -8f, 0.9f, 0.3f, 0.9f, 2.4f),      // 腰胯，从他身前横过去
		(-0.55f, -0.24f, 0.26f, 0.09f, 0.44f, 1f, 5.7f, -12f, 0.8f, 0.0f, 0.85f, 2.2f),   // 左下，膝盖高度贴着地游
	};

	private readonly Control _front, _back;
	private Func<(Vector2 Feet, float Tall, float Face)?> _body;
	private bool _live;
	private readonly Node2D _frontRoot, _backRoot;
	private readonly Control _top = null!;
	private readonly ShaderMaterial _frontM, _backM;
	private readonly SceneTree _tree;
	private readonly Vector4[] _spine = new Vector4[Count * N];
	private readonly Vector4[] _box = new Vector4[Count];
	private readonly float[] _girth = new float[Count];
	private readonly Vector2[] _hup = new Vector2[Count];
	private readonly Vector3[] _eyes = new Vector3[Count];
	private readonly float[] _diss = new float[Count];
	private readonly Vector4[] _puff = new Vector4[Slots * 3];
	private readonly Vector4[] _streak = new Vector4[Slots];
	private readonly float[] _streakA = new float[Slots];
	private readonly float[] _mouth = new float[Count];
	private readonly Queue<(Vector2 At, Node2D? Body)> _targets = new();
	private readonly Attack?[] _attacks = new Attack?[Slots];
	private float _t, _show = -1f, _intro = -1f, _fadeOut = -1f, _dimT, _fogT, _glowT;
	private bool _introFresh;
	private readonly float[] _introDiss = new float[Count];
	private readonly float[] _tau = new float[Count];        // 每条蛇自己的时间（出动时会快一下）
	private readonly Vector2[] _lunge = new Vector2[Count];  // 出动时朝敌人窜出去的偏移
	private readonly Node2D? _skyRoot, _mistFrontRoot;
	private readonly ShaderMaterial? _skyM, _mistFrontM;
	private readonly bool _biteOnly;   // 只有一条蛇从咒烟里冒出来咬一口（蛇咬那张牌用，Bite）：没有场景、蛇巢、其它几条
	private int _nextSerpent;

	private sealed class Attack
	{
		public int Serpent;
		public Vector2 Target;            // 相对脚底
		public float T;
		public bool Bit, Emerged, Faded;
		public Node2D? Body;              // 被咬的敌人（咬中时闪一下白）
		public Vector2 From;              // 冲出去散掉的位置（烟迹从这里飞出，原位的残烟也在这里）
		public float Side = 1f, Tilt;     // 从哪边扑（1 = 敌人靠 Joker 这一侧的斜上方，-1 = 另一侧）、整套姿势绕目标转多少（正 = 更平、负 = 更从上往下）

		/// <summary>扑咬姿势里的一个偏移（按「从左上扑、面朝右」画的）换到这次的方向。</summary>
		public Vector2 L(Vector2 v, float face) { float sf = face * Side; return new Vector2(v.X * sf, v.Y).Rotated(Tilt * sf); }
	}

	/// <summary>
	/// 扑咬的几个方向（用户 2026-11-13：只从左上角咬容易看腻）：敌人两侧的斜上方、更平的侧面、更陡的上方。
	/// 连着出牌时挑一个和正在咬的都不一样、也不是上一次的方向，几条蛇像是从四面围上去咬。
	/// </summary>
	private static readonly (float Side, float Tilt)[] Approaches = { (1f, 0f), (-1f, 0f), (1f, 0.38f), (-1f, 0.32f), (1f, -0.3f), (-1f, -0.34f) };
	private int _lastApproach = -1;

	private (float Side, float Tilt) PickApproach()
	{
		var used = new HashSet<int>();
		foreach (Attack? a in _attacks)
		{
			if (a == null) continue;
			for (int k = 0; k < Approaches.Length; k++)
				if (Mathf.IsEqualApprox(Approaches[k].Side, a.Side) && Mathf.IsEqualApprox(Approaches[k].Tilt, a.Tilt)) used.Add(k);
		}
		var pool = new List<int>();
		for (int k = 0; k < Approaches.Length; k++) if (!used.Contains(k) && k != _lastApproach) pool.Add(k);
		if (pool.Count == 0) for (int k = 0; k < Approaches.Length; k++) if (k != _lastApproach) pool.Add(k);
		int pick = pool[(int)Fx.Rand(0f, pool.Count - 0.001f)];
		_lastApproach = pick;
		return Approaches[pick];
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

	/// <summary>
	/// 蛇咬（2026-11-11 用户：把群蛇形态里「敌人那里冒出一条蛇去咬」那段单独拿出来给蛇咬用，敌人身上的上毒特效照旧）：
	/// at（层坐标，敌人身体中间）斜上方一团咒烟里聚出一条蛇，往后一缩、张嘴，扑下去咬合；和群蛇形态的攻击同一套姿势和烟，
	/// 只是没有前面那条蛇冲出去 / 烟迹。返回从现在到咬合那一下的秒数（游戏的上毒要等到这一刻）。不影响正在场上的群蛇（不是 Current）。
	/// </summary>
	public static float Bite(Control front, Control back, Vector2 at, Node2D? body, float tall, float face)
	{
		float small = tall * BiteScale;   // 「小蛇」：整套姿势、烟团、粗细都按 0.6 倍的身高算，烟团离敌人更近
		var m = new SerpentMantle(front, back, () => (at, small, face), live: false, biteOnly: true) { _show = 1f };
		m._attacks[0] = new Attack { Serpent = 0, Target = Vector2.Zero, Body = body, T = BiteStart };
		return BiteDelay;
	}

	private const float BiteStart = EmergeAt - 0.06f;
	private const float BiteScale = 0.6f;   // 从烟团开始聚的前一点开始

	/// <summary>Bite 开始到咬合的秒数。</summary>
	public const float BiteDelay = BiteAt - BiteStart;

	public static void StopCurrent()
	{
		if (Current != null && Current._fadeOut < 0f) Current._fadeOut = 0f;
	}

	private SerpentMantle(Control front, Control back, Func<(Vector2 Feet, float Tall, float Face)?> body, bool live, bool biteOnly = false)
	{
		_biteOnly = biteOnly;
		_front = front;
		_back = back;
		_body = body;
		_live = live;
		(Vector2 feet, float tall, float _) = body() ?? (Vector2.Zero, 300f, 1f);
		var rect = new Rect2(-3f * tall, -2.9f * tall, 10f * tall, 3.6f * tall);
		(_frontRoot, _frontM) = Make(front, feet, rect, 1f, "P5SerpentsFront");
		// 用户 2026-11-13：蛇咬时被打击牌的命中特效盖住 → 身前那层蛇和咬合特效画在命中特效上面
		_frontRoot.ZIndex = 30;
		_top = new Control { Name = "P5SerpentTop", ZIndex = 31, MouseFilter = Control.MouseFilterEnum.Ignore };
		front.AddChild(_top);
		_frontRoot.TreeExiting += () => { if (GodotObject.IsInstanceValid(_top)) _top.QueueFree(); };
		(_backRoot, _backM) = Make(back, feet, rect, -1f, "P5SerpentsBack");
		for (int i = 0; i < Count; i++) _girth[i] = 0.068f * tall * Orbits[i].Size;   // 修长：细一些、长一些（第三版：再细，用户说太肥）
		if (!biteOnly)
		{
		_skyRoot = Fx.ScreenRoot(back, "P5SerpentSky");
		back.MoveChild(_skyRoot, 0);
		// 场景：咒雾漫地（Vfx/SerpentMist.cs；2026-11-11 用户：日蚀不搭、太耀眼，换掉）。背景层调色 + 雾 + 雾里的巨蛇背，前景层只有贴地的雾丝
		_skyM = new ShaderMaterial { Shader = SerpentMist.Shader };
		_skyM.SetShaderParameter("side", -1f);
		var skyRect = new ColorRect { Size = Fx.Screen(back), Material = _skyM, MouseFilter = Control.MouseFilterEnum.Ignore };
		_skyRoot.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });   // 读屏前先刷新（和子弹时间的褪色叠在一起时不读到旧画面）
		_skyRoot.AddChild(skyRect);
		Fx.Clock(skyRect);
		_mistFrontRoot = Fx.ScreenRoot(front, "P5SerpentMistFront");
		_mistFrontM = new ShaderMaterial { Shader = SerpentMist.Shader };
		_mistFrontM.SetShaderParameter("side", 1f);
		var mistRect = new ColorRect { Size = Fx.Screen(front), Material = _mistFrontM, MouseFilter = Control.MouseFilterEnum.Ignore };
		_mistFrontRoot.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });   // 读屏前先刷新（2026-11-13）
		_mistFrontRoot.AddChild(mistRect);
		Fx.Clock(mistRect);
		}
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

	/// <summary>
	/// 出场（2026-11-11 用户认可的分镜，约 1.2 秒，层层叠进来）：0 画面先压暗（四角先暗）→ 0.1 雾浪从脚下沿地面铺开 →
	/// 0.25 身后蛇巢鼓起、背光亮起 → 0.35 起五条蛇几乎一起（间隔 0.05s、各 0.9s）从蛇巢里游出来 → 0.9 雾里翻起第一道巨蛇背、轻震 →
	/// 1.25 五双眼睛由近到远亮起。已经在场上就只亮眼睛。
	/// </summary>
	private void Intro()
	{
		if (_show > 0.3f) { _intro = 1.0f; _introFresh = false; return; }
		_intro = 0f;
		_introFresh = true;
		_show = 1f;
		Stage.PlayEvent?.Invoke(IntroSfx, 0.6f);
		Fx.After(_frontRoot, 0.9, () => Fx.Shake(1));
	}

	/// <summary>能力触发：一条蛇朝 target（层坐标，敌人身体中间）吐一颗法球。</summary>
	public void Strike(Vector2 target, Node2D? body = null)
	{
		if (_targets.Count < 8) _targets.Enqueue((target, body));
	}

	private bool _detached;

	private void Detach()
	{
		if (_detached) return;
		_detached = true;
		_tree.ProcessFrame -= Tick;
		if (Current == this) Current = null;
	}

	private void Free()
	{
		Detach();
		if (GodotObject.IsInstanceValid(_frontRoot)) _frontRoot.QueueFree();
		if (GodotObject.IsInstanceValid(_backRoot)) _backRoot.QueueFree();
		if (GodotObject.IsInstanceValid(_skyRoot)) _skyRoot.QueueFree();
		if (GodotObject.IsInstanceValid(_mistFrontRoot)) _mistFrontRoot.QueueFree();
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
		if (_intro >= 0f) { _intro += dt; if (_intro > 2.6f) _intro = -1f; }
		if (_fadeOut >= 0f)
		{
			_fadeOut += dt;
			if (_fadeOut > 0.8f) { Free(); return; }
		}
		_frontRoot.GlobalPosition = feet;
		_backRoot.GlobalPosition = feet;
		float alpha = (_intro >= 0f ? 1f : Smooth(0f, 0.8f, _show)) * (_fadeOut >= 0f ? 1f - Smooth(0f, 0.8f, _fadeOut) : 1f);
		// 场景：出场时从 Joker 脚下漫开（0.7s），静静浮现时淡入；能力没了一起淡掉
		// 场景分层推进（出场分镜）；不播出场时 0.8s 一起淡入；能力没了跟着 amount 一起淡掉
		if (_intro >= 0f)
		{
			_dimT = Mathf.Max(_dimT, Smooth(0f, 0.25f, _intro));
			_fogT = Mathf.Max(_fogT, Ease5.Seg(_intro, 0.1f, 0.6f));
			_glowT = Mathf.Max(_glowT, Smooth(0.25f, 0.65f, _intro));
		}
		else
		{
			float k = Smooth(0f, 0.8f, _show);
			_dimT = Mathf.Max(_dimT, k); _fogT = Mathf.Max(_fogT, k); _glowT = Mathf.Max(_glowT, k);
		}
		float surge = _intro >= 0f && _introFresh ? Smooth(0.82f, 1.0f, _intro) * (1f - Smooth(1.3f, 1.9f, _intro)) : 0f;
		Vector2 scr = Fx.Screen(_back);
		Vector2 fs = Fx.ScreenPoint(_back, feet);
		float tallS = T * _back.GetCanvasTransform().Scale.Y;
		// 场景：咒雾漫地，出场时从 Joker 身边漫开；能力没了一起淡掉
		float amt = _fadeOut >= 0f ? 1f - Smooth(0f, 0.8f, _fadeOut) : (_show > 0f || _intro >= 0f ? 1f : 0f);
		foreach (ShaderMaterial? sm in new[] { _skyM, _mistFrontM })
		{
			if (sm == null) continue;
			sm.SetShaderParameter("amount", amt);
			sm.SetShaderParameter("dim_t", _dimT);
			sm.SetShaderParameter("fog_t", _fogT);
			sm.SetShaderParameter("glow_t", _glowT);
			sm.SetShaderParameter("surge", surge);
			sm.SetShaderParameter("aspect", scr.X / scr.Y);
			sm.SetShaderParameter("feet", new Vector2(fs.X / scr.X, fs.Y / scr.Y));
			sm.SetShaderParameter("tall", tallS / scr.Y);
			sm.SetShaderParameter("face", face);
		}
		Vector2 nestP = new(-0.12f * face * T, -0.78f * T);

		// 攻击：有目标、出场演完了 → 挑一条闲着的蛇；都在忙（玩家出牌很快）就接手一条已经咬完、正散成烟的蛇（用户 2026-11-13：
		// 快速出牌时不要排队等上一条演完）。每帧把排着的目标都派出去，不是一帧一个。
		while (_targets.Count > 0 && _show > 0.3f && (_intro < 0f || _intro > 0.8f))
		{
			int slot = -1, serp = -1;
			for (int s = 0; s < Slots && slot < 0; s++) if (_attacks[s] == null) slot = s;
			for (int k = 0; k < Count && serp < 0; k++)
			{
				int c = (_nextSerpent + k) % Count;
				if (AttackOf(c) == null) serp = c;
			}
			if (slot < 0 || serp < 0)
			{
				// 接手：咬完（T ≥ 0.6，已经在散成烟）里最靠后的那条
				int best = -1;
				for (int s = 0; s < Slots; s++)
					if (_attacks[s] is { } a && a.T >= 0.6f && (best < 0 || a.T > _attacks[best]!.T)) best = s;
				if (best < 0) break;
				slot = best;
				serp = _attacks[best]!.Serpent;
				_streakA[best] = 0f;
			}
			_nextSerpent = (serp + 2) % Count;
			var (tat, tbody) = _targets.Dequeue();
			var (side, tilt) = PickApproach();
			Vector3 op = Orbit(serp, _tau[serp], T, face);
			// 直接从扑咬开始（StrikeStart）：跳过了在轨道上窜出去那段，原位的残烟就放在这条蛇现在所在的位置
			Attack na = new() { Serpent = serp, Target = tat - feet, Body = tbody, T = StrikeStart, Side = side, Tilt = tilt, From = new Vector2(op.X, op.Y) };
			_attacks[slot] = na;
			// 扑下去的疾光 + 咬合的獠牙（画在命中特效上面）
			if (!_biteOnly && GodotObject.IsInstanceValid(_top))
				SkillRedo5.SerpentStrike(_top, _back, tat + na.L(new Vector2(-0.72f * T, -0.72f * T), face), tat, T, StrikeDelay, (0.36f - StrikeStart) / PreBiteSpeed);
		}

		for (int i = 0; i < Count; i++)
		{
			// 扑咬时那条蛇放大一些（更有气势），回到轨道上恢复
			Attack? sa = AttackOf(i);
			float big = sa != null ? Smooth(EmergeAt - 0.02f, EmergeAt + 0.02f, sa.T) * (1f - Smooth(LeaveAt - 0.02f, LeaveAt, sa.T)) : 0f;
			_girth[i] = 0.068f * T * Orbits[i].Size * (1f + 1.0f * big);   // 扑咬时放大到两倍（用户：要更有表现力）
			PoseSerpent(i, T, face, _biteOnly && sa == null ? 0f : alpha);
		}
		UpdateAttacks(dt, feet, T);
		if (_biteOnly && _attacks[0] == null) { Free(); return; }
		for (int i = 0; i < Count; i++) _diss[i] = Mathf.Max(_diss[i], _introDiss[i]);

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
			// 出场 0.25s 蛇巢鼓起来（一团烟往上翻涌、比平时大一圈），再收回平时的大小
			float swell = _intro >= 0f && _introFresh ? Smooth(0.2f, 0.45f, _intro) * (1f - Smooth(0.6f, 1.2f, _intro)) : 0f;
			mat.SetShaderParameter("nest", new Vector3(nestP.X, nestP.Y - 0.1f * T * swell, 0.55f * T * (1f + 0.4f * swell)));
			mat.SetShaderParameter("nest_a", (_biteOnly ? 0f : alpha) * (_intro >= 0f && _introFresh ? Smooth(0.2f, 0.45f, _intro) : 1f));
			mat.SetShaderParameter("puff", _puff);
			mat.SetShaderParameter("streak", _streak);
			mat.SetShaderParameter("streak_a", _streakA);
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
		Vector2 A = P + atk.L(new Vector2(-0.6f * T, -1.25f * T), face);          // 烟团
		Vector2 cocked = P + atk.L(new Vector2(-0.72f * T, -0.72f * T), face);    // 往后缩、准备扑
		float headLen = _girth[i] * 2.16f;   // 照示意图：头根（x=-0.3）到吻尖（1.56）= 1.86 W，W = 1.16 g
		float lunge = Ease5.Seg(t, 0.38f, BiteAt - 0.38f);
		lunge = lunge * lunge;
		float rear = Smooth(EmergeAt, 0.38f, t);
		Vector2 toP = (P - cocked).Normalized();
		Vector2 bite = P - toP * headLen * 0.75f;
		Vector2 hb = cocked.Lerp(cocked - toP * 0.22f * T, rear * (1f - lunge)).Lerp(bite, lunge);
		Vector2 c1 = A + atk.L(new Vector2(0.35f * T, 0.3f * T), face);
		Vector2 c2 = hb - toP * 0.55f * T + atk.L(new Vector2(-0.2f * T, -0.25f * T), face);
		Vector2 tail = A + atk.L(new Vector2(-0.15f * T, -0.1f * T), face);
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
		// 眼睛先亮：烟迹扎进烟团时最亮，蛇头聚出来以后回到平常
		_eyes[i] = new Vector3(eye.X, eye.Y, alpha * (1f + 1.2f * (1f - Smooth(EmergeAt + 0.04f, 0.42f, t))));
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

	/// <summary>缠腰的那条（Orbits[3]，腰胯从身前横过）。</summary>
	private const int Belt = 3;

	// 音效用游戏自带的 FMOD 事件（2026-11-11 用户：合成的音效有很重的代码感，跟专业音效差远了 → 以后都换成游戏自带的；
	// 预览工具里没有 FMOD，不出声，要在游戏里听）。原来合成的 serpent_rise / serpent_maw 不再使用。
	private const string BiteSfx = "event:/sfx/enemy/enemy_attacks/scroll_of_biting/scroll_of_biting_bite";
	private const string IntroSfx = "event:/sfx/characters/defect/defect_dark_channel";

	/// <summary>出场时眼睛亮起的先后（0 最先）：缠腰的最近，其次胸口朝敌人的、左下，最后身后的两条。</summary>
	private static readonly int[] EyeRank = { 3, 4, 1, 0, 2 };

	/// <summary>
	/// 缠腰那条蛇的身体（用户 2026-11-11：它的尾巴从 Joker 腋下钻过去，透视很怪）。以前尾巴直接拉回胸口高度、身后的蛇巢，
	/// 身体从身前斜着穿过他的躯干才到身后。现在身体在胯部高度绕他一圈（俯视略斜的椭圆，下半弧在他前面、上半弧在他后面），
	/// 前后只在椭圆左右两端换（那里在他轮廓外面）；绕到身后以后才往上收进蛇巢（被他挡住）。头在身前偏敌人一侧，顺着身体朝敌人。
	/// f：0 尾 → 1 颈根；len：身长（像素）。
	/// </summary>
	private Vector3 BeltPoint(float f, float len, float T, float face)
	{
		const float cy = -0.47f, a = 0.31f, b = 0.065f;   // 身高为单位：胯部高度、半宽（比大衣和垂下的手宽）、斜看的半高
		float s = (1f - f) * len / T;                     // 离颈根的路程（身高为单位）
		float th0 = 0.5f + 0.22f * Mathf.Sin(_t * 0.45f) + 0.06f * Mathf.Sin(_t * 1.1f + 1f);   // 颈根在身前右侧，慢慢往前探、往回收
		const float thEnd = 1.62f * Mathf.Pi;              // 绕到身后哪里为止（之后往上收进蛇巢）
		float ra = (a + b) * 0.5f;
		float th = th0 + s / ra;
		Vector3 Ring(float t, float grow)
		{
			float aa = a * (1f + grow);
			float breathe = 1f + 0.03f * Mathf.Sin(_t * 0.9f + t * 2f);
			return new Vector3(aa * breathe * Mathf.Cos(t) * face * T, (cy + b * Mathf.Sin(t)) * T, Mathf.Sin(t));
		}
		// 靠近头的一段稍微往外、往上撑开：头探出轮廓、不贴着衣服
		float near = 1f - Smooth(0f, 0.35f, s);
		if (th <= thEnd)
		{
			Vector3 r = Ring(th, 0.18f * near);
			r.Y -= 0.05f * T * near;
			return r;
		}
		// 身后：从环的末端往上收进蛇巢（都在身后，被 Joker 挡住）
		Vector3 e = Ring(thEnd, 0f);
		float rest = (th - thEnd) * ra;
		float k = Mathf.Clamp(rest / 0.6f, 0f, 1f);
		Vector3 nestP = new(-0.12f * face * T, -0.78f * T, -0.6f);
		Vector3 v = e.Lerp(nestP, Smooth(0f, 1f, k));
		v.Z = Mathf.Min(v.Z, -0.3f);
		return v;
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
		// 出场：五条蛇依次从身后的蛇巢里窜出来（先是一道散着的暗影，窜到位置时聚成形）
		float grow = 1f;
		_introDiss[i] = 0f;
		if (_intro >= 0f && _introFresh && _intro < 2.2f)
		{
			// 「钻」出来（用户 2026-11-11：现在纯是平移出来的）；第二版（用户：出来那么快又马上停住，常驻几乎是停住的，很怪 → 幅度小一些、慢一些）：
			// 每条 1.1s、间隔 0.15s，平缓减速、不冲过头，最后自然接上常驻的慢游
			float g0 = Ease5.Seg(_intro, 0.35f + 0.05f * i, 0.9f);   // 第三版（用户：一个一个出来太拖沓）：几乎一起出来，间隔 0.05s、各 0.9s
			grow = g0 >= 1f ? 1f : 1f - Mathf.Pow(1f - g0, 2.2f);
			_introDiss[i] = g0 < 1f ? 0.6f * (1f - Smooth(0.0f, 0.45f, g0)) : 0f;   // 头刚冲出蛇巢时带着烟
		}
		Vector3 chest = new(-0.12f * face * T, -0.78f * T, -0.5f);   // 从蛇巢出来（变量名沿用）
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
			if (i == Belt) v = BeltPoint(f, o.Len * T * o.Size, T, face);
			else {
			// 身体只「大致」顺着头游过的路（用户 2026-11-11：圈小了身体会把整圈都描一遍、绕成结）：
			// 和蛇巢 → 头的直线各取一半，弯是跟着游动的弯，但不会回头描圈
			Vector3 headNow = trail[0];
			Vector3 nestP = new(-0.12f * face * T, -0.78f * T, -0.5f);
			Vector3 line = nestP.Lerp(headNow, f) + new Vector3(0f, -Mathf.Sin(f * Mathf.Pi) * 0.25f * T, 0f);
			v = v.Lerp(line, 0.55f * (1f - f * f * 0.6f));
			}
			// 沿身体往尾巴传的 S 波
			float wave = Mathf.Sin(f * 9f - _t * 3.4f + i) * 0.065f * T * (0.25f + 0.75f * (1f - f));
			v += new Vector3(wave * 0.4f, wave, 0f);
			// 出动：朝敌人窜一下（越靠头越多）
			v += new Vector3(_lunge[i].X, _lunge[i].Y, 0f) * Mathf.Pow(f, 1.5f);
			// 一体：尾巴都收进 Joker 身后的蛇巢（同一团咒烟里伸出五个头）
			float pull = i == Belt ? 0f : 1f - Smooth(0f, 0.75f, f);   // 收进蛇巢的那段拉长一些，不在身体中间折出急弯
			Vector3 nest3 = new(-0.12f * face * T + Mathf.Sin(_t * 0.7f + i * 1.3f) * 0.12f * T, -0.78f * T + Mathf.Cos(_t * 0.6f + i * 2.1f) * 0.1f * T, -0.5f);
			v = v.Lerp(nest3, pull * pull * 0.92f);
			float a = Smooth(0f, 0.08f, f) * alpha;
			_spine[i * N + k] = new Vector4(v.X, v.Y, v.Z, a);
		}
		// 脊线平滑四遍（2026-11-11 用户：一节一节的）：拉向蛇巢的那一段会在身体上折出急弯，折线的拐点就成了一个球形的关节
		for (int pass = 0; pass < 4; pass++)
		{
			Vector4 prev = _spine[i * N];
			for (int k = 1; k < nb - 1; k++)
			{
				Vector4 cur = _spine[i * N + k], next = _spine[i * N + k + 1];
				Vector4 avg = (prev + next) * 0.5f;
				_spine[i * N + k] = new Vector4(Mathf.Lerp(cur.X, avg.X, 0.5f), Mathf.Lerp(cur.Y, avg.Y, 0.5f), Mathf.Lerp(cur.Z, avg.Z, 0.5f), cur.W);
				prev = cur;
			}
		}
		// 出场最后五个头一起转向敌人：不单独转头，而是把脖子最后一截弯过去（头永远顺着脖子，不会错开）
		Vector2 enemyDir = new Vector2(face, 0.12f).Normalized();
		// 出场「钻」：蛇顺着自己最终的身体路线从蛇巢里游出来——身上 f 处的点放在路线的 f − (1 − grow) 处
		// （还在蛇巢里的部分留在路线起点、藏起来），头在前面开路、身体沿着头走过的路一节节跟出来；
		// 游的时候身上一道往尾巴传的 S 形扭动，越接近到位越平缓；grow 到位时略过头（OutBack），头冲出去一点再收回
		if (grow < 1f)
		{
			Span<Vector4> fin = stackalloc Vector4[N];
			for (int k = 0; k < nb; k++) fin[k] = _spine[i * N + k];
			float lag = 1f - grow;
			for (int k = 0; k < nb; k++)
			{
				float f = k / (nb - 1f);
				float sp = f - lag;
				Vector4 v;
				if (sp <= 0f) { v = fin[0]; v.W = 0f; }
				else
				{
					float x = sp * (nb - 1f);
					int a0 = Mathf.Min((int)x, nb - 2);
					float u = x - a0;
					v = fin[a0].Lerp(fin[a0 + 1], u);   // sp > 1（冲过头）时沿最后一段往外延
					// 扭动：垂直于身体、往尾巴传的 S 波，刚出来时最大
					Vector2 tg = new Vector2(fin[a0 + 1].X - fin[a0].X, fin[a0 + 1].Y - fin[a0].Y).Normalized();
					float wig = Mathf.Sin(sp * 11f - _intro * 12f + i) * 0.035f * T * Mathf.Clamp(lag * 1.4f, 0f, 1f) * Smooth(0f, 0.25f, sp);
					v += new Vector4(-tg.Y * wig, tg.X * wig, 0f, 0f);
					v.W = fin[k].W * Smooth(0f, 0.12f, sp);
				}
				_spine[i * N + k] = v;
			}
		}
		// 出场 1.1s 五双眼睛由近到远依次亮一下（不转头：用户说一起转头很怪）
		float eyes = 0.45f + 0.1f * Mathf.Sin(_t * 2f + i);
		if (_intro >= 0f)
		{
			float te = 1.25f + 0.06f * EyeRank[i];
			eyes = Mathf.Max(eyes, Smooth(te, te + 0.06f, _intro) * (1f - Smooth(te + 0.3f, te + 0.75f, _intro)));
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
			a.T += dt * (a.T < BiteAt && !_biteOnly ? PreBiteSpeed : 1f);   // 蛇咬那张牌（_biteOnly）照原速：它的结算时机按 BiteDelay 算
			float t = a.T;
			float face = a.Target.X >= 0f ? 1f : -1f;
			Vector2 cloud = a.Target + a.L(new Vector2(-0.6f * T, -1.25f * T), face);
			// 加强表达（2026-11-11 用户：随机到的那条加速冲出、缓缓消失、敌人那里出现一只去咬——这套再强一点）：
			// 0～0.07 往回一缩、眼睛一亮（看得出是它要出手）→ 0.07～0.23 朝敌人猛窜 0.85 身高、边窜边散 →
			// 0.12～0.32 原地化成一道咒烟、带着那对红眼飞到敌人上方，原位留下一团慢慢散掉的残烟（到 0.9）→
			// 0.28 烟迹扎进烟团，眼睛先亮、蛇头再聚出来 → 扑咬 → 散掉、回原位重新聚起
			_diss[a.Serpent] = t < EmergeAt ? Smooth(0.1f, EmergeAt, t)
				: t < 0.56f ? 1f - Smooth(EmergeAt, 0.34f, t)
				: t < LeaveAt ? Smooth(0.56f, 0.76f, t)
				: 1f - Smooth(LeaveAt, 1.12f, t);
			if (t < EmergeAt)
			{
				Vector3 hp = Orbit(a.Serpent, _tau[a.Serpent], T, 1f);
				Vector2 dir = (a.Target - new Vector2(hp.X, hp.Y)).Normalized();
				float coil = -0.12f * T * Smooth(0f, 0.07f, t) * (1f - Smooth(0.07f, 0.11f, t));
				float dash = 0.85f * T * Ease5.OutQuint(Ease5.Seg(t, 0.07f, 0.16f));
				_lunge[a.Serpent] = dir * (coil + dash);
				// 头现在在哪（烟迹的起点）：跟着窜出去的头走，窜到底就定住
				if (t < 0.2f)
				{
					Vector4 hb = _spine[a.Serpent * N + N - HeadPts - 1];
					a.From = new Vector2(hb.X, hb.Y);
				}
				// 眼睛：缩的时候一亮，随烟迹飞走
				float flare = Smooth(0f, 0.05f, t) * (1f - Smooth(0.13f, 0.19f, t));
				Vector3 ey = _eyes[a.Serpent];
				_eyes[a.Serpent] = new Vector3(ey.X, ey.Y, Mathf.Max(ey.Z * (1f - Smooth(0.13f, 0.19f, t)), 1.6f * flare));
			}
			// 烟迹：头 0.12→0.3 从原位飞到烟团（先快后慢），尾巴落后一点跟上
			if (!_biteOnly && t >= 0.12f && t < 0.42f)
			{
				float hk = Ease5.OutQuint(Ease5.Seg(t, 0.12f, 0.18f));
				float tk = Ease5.OutQuint(Ease5.Seg(t, 0.17f, 0.2f));
				Vector2 head = a.From.Lerp(cloud, hk), tail = a.From.Lerp(cloud, tk * 0.97f);
				// 飞的路线往上拱一点，不是直线
				head += new Vector2(0f, -0.25f * T * Mathf.Sin(hk * Mathf.Pi));
				tail += new Vector2(0f, -0.25f * T * Mathf.Sin(tk * Mathf.Pi));
				_streak[s] = new Vector4(tail.X, tail.Y, head.X, head.Y);
				_streakA[s] = Smooth(0.12f, 0.15f, t) * (1f - Smooth(0.3f, 0.42f, t));
			}
			else _streakA[s] = 0f;
			// 咒烟团：原位留下的残烟（慢慢散）、聚出来的那一团（烟团处，大一点）、咬中往外炸的那一团
			if (!_biteOnly && t >= 0.14f && t < 0.95f) Puff(a.From, Ease5.Seg(t, 0.14f, 0.8f), 0.55f * T, s * 3 + 2);
			if (t >= EmergeAt - 0.04f) Puff(cloud, Ease5.Seg(t, EmergeAt - 0.04f, 0.7f), 0.75f * T, s * 3);
			if (t >= BiteAt) Puff(a.Target, Ease5.Seg(t, BiteAt, 0.6f), -0.5f * T, s * 3 + 1);
			if (!a.Bit && t >= BiteAt)
			{
				a.Bit = true;
				Stage.PlayEvent?.Invoke(BiteSfx, 0.8f);   // 只有咬合的声音（用户：冲出来不要音效）；2026-11-13 用户：稍微小了一点 → 0.55 提到 0.8
				Vector2 at = feet + a.Target;
				Control fx = GodotObject.IsInstanceValid(_top) ? _top : _front;
				Glow.Flash(fx, at, Pal5.Crimson, 0.8f * T, 0.22f);
				Glow.Sparks(fx, at, 22, 180f, 600f, 0.5f, new[] { Pal5.Bright, Pal5.Crimson, Pal5.Deep }, 250f, 0.1f, 0.32f, Vector2.Up, 160f);
				if (a.Body != null && GodotObject.IsInstanceValid(a.Body)) BodyTint.Pulse(a.Body, new Color(2.2f, 2.2f, 2.2f), 0.05, 0.2);   // 被咬的敌人闪一下白
				Fx.Shake(2);
				HitStop.Request(0.06);
			}
			if (t > AttackLen) { _attacks[s] = null; _streakA[s] = 0f; }
		}
	}
}
