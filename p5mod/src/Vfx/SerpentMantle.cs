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
uniform vec4 maw[3];            // 敌人身上的咬合：xy 中心，z 飞进来（0～1），w 合上（0 张开 → 1 咬死）
uniform float maw_a[3];         // 大嘴的浓度（散掉时往 0 走）

float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { return vn(p) * 0.5 + vn(p * 2.03 + 5.1) * 0.3 + vn(p * 4.1 + 1.7) * 0.2; }
float ease3(float x) { return 1.0 - pow(1.0 - clamp(x, 0.0, 1.0), 3.0); }

// 粗细（s：0 尾 → 1 吻尖）：尾巴细、身体、脖子略收、头上平下宽的楔形
float profile(float s, float up) {
	// 第三版（用户：身子太肥，像一坨）：从脖子往尾巴一路收细，尾巴拉成又细又长的一条；最粗在靠头的三分之一
	float body = mix(0.08, 1.0, pow(smoothstep(0.0, 0.72, s), 0.8)) * mix(1.0, 0.85, smoothstep(0.74, 0.88, s));
	if (s < 0.9) return body;
	float hs = (s - 0.9) / 0.1;
	float top = mix(0.86, 0.92, smoothstep(0.0, 0.3, hs)) * mix(1.0, 0.42, smoothstep(0.3, 1.0, hs));
	float jaw = mix(0.86, 1.02, smoothstep(0.0, 0.25, hs)) * mix(1.0, 0.3, smoothstep(0.25, 1.0, hs));
	return mix(jaw, top, up);
}

// 叠一层（预乘）
void over(inout vec4 acc, vec3 c, float a) { acc.rgb += (1.0 - acc.a) * c * a; acc.a += (1.0 - acc.a) * a; }

void fragment() {
	vec2 p = origin + UV * size;
	vec4 acc = vec4(0.0);
	vec3 glow = vec3(0.0);
	for (int j = 0; j < 5; j++) {
		vec4 bb = box[j];
		if (p.x < bb.x || p.y < bb.y || p.x > bb.z || p.y > bb.w) continue;
		float g = girth[j];
		float best = 1e9, bs = 0.0, bz = 0.0, ba = 0.0;
		vec2 bq = p;
		for (int k = 0; k < 31; k++) {
			vec4 a = spine[j * 32 + k], b = spine[j * 32 + k + 1];
			if (max(a.w, b.w) <= 0.001) continue;
			if (side > 0.0 ? max(a.z, b.z) <= 0.0 : min(a.z, b.z) > 0.0) continue;
			vec2 ab = b.xy - a.xy;
			float h = clamp(dot(p - a.xy, ab) / max(dot(ab, ab), 1e-3), 0.0, 1.0);
			vec2 q = a.xy + ab * h;
			float d = length(p - q);
			if (d < best) { best = d; bs = (float(k) + h) / 31.0; bz = mix(a.z, b.z, h); ba = mix(a.w, b.w, h); bq = q; }
		}
		if (best > 1e8) continue;
		vec2 n2 = (p - bq) / max(best, 1e-3);
		float upv = dot(n2, hup[j]);
		float w = g * profile(bs, smoothstep(-0.3, 0.3, upv));
		float r = best / max(w, 0.5);
		if (r > 3.0) continue;
		float headness = smoothstep(0.86, 0.92, bs);
		float L = bs * 11.0;
		float acr = sign(upv) * best / g;
		float nz = fbm(vec2(L * 2.2 - clock * 1.3, acr * 1.1 - clock * 0.2) + float(j) * 11.0);
		float fine = vn(vec2(L * 3.5 - clock * 2.6, acr * 3.0) + float(j) * 4.0);
		float dj = diss[j];
		float torn = (nz - 0.5) * mix(0.3, 0.18, headness) + (fine - 0.5) * 0.22 + (1.0 - smoothstep(0.0, 0.25, bs)) * nz * 0.6;   // 不要沿身子一块块的疙瘩，只在边上细碎
		torn += dj * (0.5 + nz * 1.8);                                  // 散成烟：边被撕开、往里吃
		float sd = r - 1.0 + torn;
		float dens = smoothstep(0.16, -0.22, sd) * (1.0 - dj * 0.6);   // 芯是实的：挡住 / 被挡住看得出层次，只有边是烟
		// 边缘里面一薄层透光（越靠上越亮：天光从上面来）
		float shell = smoothstep(-0.34, -0.05, sd) * (1.0 - smoothstep(-0.05, 0.1, sd));
		float flick = fbm(vec2(L * 1.7 - clock * 2.2, acr * 2.0) + 3.0 + float(j));
		float lit = 0.55 + 0.45 * smoothstep(-0.5, 0.8, -n2.y);
		vec3 emit = vec3(0.6, 0.05, 0.07) * shell * smoothstep(0.5, 0.82, flick) * 1.1 * lit * (1.0 + dj * 1.5);   // 断断续续的透光，不成一圈描边
		float nz2 = fbm(vec2(L * 2.0 - clock * 1.8, acr * 1.6) + float(j) * 2.0);
		vec3 inner = mix(vec3(0.008, 0.002, 0.004), vec3(0.045, 0.01, 0.016), smoothstep(0.4, 0.9, nz2));
		// 圆柱的体积：朝上一侧一道细的暗红边光（日冕从上面照下来），背光一侧纯黑
		float topRim = smoothstep(0.45, 0.9, r) * smoothstep(0.1, 0.8, -upv * sign(-hup[j].y) + 0.0) * (1.0 - smoothstep(0.95, 1.05, r + torn));
		inner += vec3(0.42, 0.05, 0.06) * topRim * smoothstep(-0.2, 0.6, -n2.y) * 0.8;
		// 外面一缕缕往上飘的暗烟
		float wn = fbm(vec2(L * 0.7 - clock * 0.8, best / g * 0.9 + clock * 0.9 * sign(n2.y)) + 7.0 + float(j) * 3.0);
		float wisp = smoothstep(0.5, 0.85, wn) * smoothstep(mix(1.8, 2.8, smoothstep(0.0, 0.8, -n2.y)), 0.9, r) * 0.6 * (1.0 - headness * 0.7);
		wisp += dj * (1.0 - dj) * 3.0 * smoothstep(0.4, 0.8, wn) * smoothstep(2.9, 0.6, r);   // 散的时候一大团烟往外、往上飘
		// 几条蛇之间连成一片的薄烟（迷迷糊糊、不分得那么清楚）
		wisp = max(wisp, exp(-pow(r / 1.8, 2.0)) * 0.2 * smoothstep(0.35, 0.75, wn) * smoothstep(3.0, 2.0, r));
		float dim = bz < 0.0 ? 0.8 : 1.0;
		vec3 c = (inner + emit) * dim;
		vec3 wc = vec3(0.03, 0.008, 0.012) + vec3(0.22, 0.02, 0.03) * smoothstep(0.72, 0.9, wn);
		float a = clamp(dens + wisp * (1.0 - dens), 0.0, 1.0) * ba;
		vec3 cc = mix(wc, c, dens / max(dens + wisp * (1.0 - dens), 1e-3));
		over(acc, cc, a);
		// 眼睛：一团晕开的暖红雾光
		vec2 ed = p - eyes[j].xy;
		float e2 = dot(ed, ed);
		glow += (vec3(1.0, 0.55, 0.25) * exp(-e2 / pow(g * 0.11, 2.0)) * 1.2 + vec3(0.9, 0.12, 0.08) * exp(-e2 / pow(g * 0.4, 2.0)) * 0.45) * eyes[j].z * (1.0 - dj);
	}
	// 敌人身上：咬合（用户选的 B：不画蛇头）——上下两排烟做的獠牙从两边飞进来、交错着猛地合上，合缝亮一下、留一排红的牙印，
	// 然后两排牙散成烟。牙也是烟（近黑、边上透暗红），没有眼睛、不成一张脸。
	if (side > 0.0) {
		float T = unit;
		for (int i = 0; i < 3; i++) {
			if (maw_a[i] <= 0.0) continue;
			float k = maw[i].z, c = maw[i].w, ma = maw_a[i];
			float S = T * 0.5;
			vec2 q = (p - maw[i].xy) / S;
			if (abs(q.x) > 1.8 || abs(q.y) > 2.2) continue;
			float gone = 1.0 - ma;
			float n = fbm(q * 2.6 + vec2(0.0, clock * 0.7) + float(i) * 9.0);
			float n2 = fbm(q * 5.0 - clock * 1.6 + float(i) * 3.0);
			float gap = mix(0.75, 0.0, c) * mix(1.5, 1.0, k);            // 从远处飞进来，再合上
			float bow = 0.36 * (1.0 - q.x * q.x);
			float rootU = -gap * 0.5 - bow, rootL = gap * 0.5 + bow;    // 上颚 / 下颚的牙根线（屏幕 y 朝下）
			float endFade = smoothstep(1.05, 0.7, abs(q.x));
			// 牙：上面 5 颗朝下，下面 4 颗朝上，交错
			float teeth = 0.0, tip = 0.0;
			for (int t = 0; t < 9; t++) {
				bool up = t < 5;
				float xi = up ? (float(t) - 2.0) * 0.3 : (float(t - 5) - 1.5) * 0.3;
				float root = up ? -gap * 0.5 - 0.36 * (1.0 - xi * xi) : gap * 0.5 + 0.36 * (1.0 - xi * xi);
				float len = 0.5 * (1.0 - 0.3 * abs(xi));
				float dy = (q.y - root) * (up ? 1.0 : -1.0);
				float tt = dy / len;
				float w = 0.09 * (1.0 - clamp(tt, 0.0, 1.0));
				float d = abs(q.x - xi + 0.04 * tt * tt * sign(xi + 0.001)) - w + (n2 - 0.5) * 0.03;
				float in_ = smoothstep(0.02, -0.02, d) * smoothstep(-0.05, 0.02, dy) * smoothstep(len + 0.02, len - 0.06, dy);
				teeth = max(teeth, in_);
				tip = max(tip, exp(-dot(q - vec2(xi, root + (up ? len : -len)), q - vec2(xi, root + (up ? len : -len))) / 0.004));
			}
			// 牙龈：沿牙根线的一道烟带（往外一侧）
			float gumU = abs(q.y - (rootU - 0.14)) - 0.16 * endFade;
			float gumL = abs(q.y - (rootL + 0.14)) - 0.16 * endFade;
			float gums = smoothstep(0.08, -0.12, min(gumU, gumL) + (n - 0.5) * 0.16) * endFade;
			float erode = gone * (0.6 + n * 1.6);
			teeth *= smoothstep(0.0, 0.25, 1.0 - erode) * endFade;
			gums *= smoothstep(0.0, 0.3, 1.0 - erode);
			// 颜色：近黑的烟，边上透暗红；牙比牙龈亮一点（暗的骨红）
			vec3 tc = mix(vec3(0.03, 0.006, 0.01), vec3(0.32, 0.12, 0.11), smoothstep(0.35, 0.85, n)) ;
			vec3 gc = mix(vec3(0.012, 0.003, 0.006), vec3(0.08, 0.014, 0.02), smoothstep(0.4, 0.85, n)) + vec3(0.45, 0.04, 0.06) * smoothstep(0.62, 0.8, n2) * smoothstep(-0.12, 0.0, min(gumU, gumL)) * 0.7;   // 只在牙龈边上零星透光
			// 四周一团暗烟（让它有分量，不是两排剪纸）
			float shroud = smoothstep(1.4, 0.3, length(q * vec2(0.75, 1.0 / (0.6 + gap * 0.5)))) * (0.45 + 0.55 * n) * k * 0.45;
			over(acc, tc, teeth * 0.95 * ma);
			over(acc, gc, gums * 0.92 * ma);
			over(acc, vec3(0.02, 0.004, 0.008), clamp(shroud * (1.0 - erode), 0.0, 1.0) * ma);
			// 合上的一下：合缝一道光 + 一排红的牙印，慢慢退掉
			float shut = smoothstep(0.85, 1.0, c);
			float seam = exp(-pow((q.y + (n2 - 0.5) * 0.05) / 0.035, 2.0)) * endFade * shut * ma;
			glow += vec3(1.0, 0.35, 0.25) * seam * 1.1 + vec3(0.9, 0.08, 0.08) * tip * shut * (0.6 + 0.8 * gone) * (1.0 - gone * gone) * 1.2;
			glow += vec3(0.6, 0.05, 0.06) * smoothstep(0.02, -0.02, min(gumU, gumL) + 0.05) * smoothstep(0.6, 0.9, n2) * 0.3 * ma;
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
	private readonly Vector4[] _maw = new Vector4[Slots];
	private readonly float[] _mawA = new float[Slots];
	private readonly Queue<Vector2> _targets = new();
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
		public bool Bit;
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
	public void Strike(Vector2 target)
	{
		if (_targets.Count < 8) _targets.Enqueue(target);
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
		_skyM.SetShaderParameter("disk_r", 0.045f);
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
				_attacks[s] = new Attack { Serpent = serp, Target = _targets.Dequeue() - feet };
				Sfx.Play(Sfx.SerpentMaw, 0.8f, BiteAt - 0.01f);   // 只有咬合的声音（用户：冲出来不要音效）
				break;
			}
		}

		for (int i = 0; i < Count; i++) PoseSerpent(i, T, face, alpha);
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
			mat.SetShaderParameter("maw", _maw);
			mat.SetShaderParameter("maw_a", _mawA);
		}
	}

	private Attack? AttackOf(int serpent)
	{
		foreach (Attack? a in _attacks) if (a != null && a.Serpent == serpent) return a;
		return null;
	}

	private void PoseSerpent(int i, float T, float face, float alpha)
	{
		var o = Orbits[i];
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
		Vector4 hb4 = _spine[i * N + nb - 1], pr4 = _spine[i * N + nb - 4];
		Vector2 hb = new(hb4.X, hb4.Y);
		Vector2 move = new(hb4.X - pr4.X, hb4.Y - pr4.Y);
		Vector2 dir = move.LengthSquared() > 1e-3f ? move.Normalized() : new Vector2(face, 0f);
		// 出场最后五个头一起转向敌人
		Vector2 enemyDir = new Vector2(face, 0.12f).Normalized();
		float turn = _intro >= 0f ? Smooth(1.05f, 1.25f, _intro) * (1f - Smooth(1.9f, 2.2f, _intro)) : 0f;
		float eyes = 0.45f + 0.1f * Mathf.Sin(_t * 2f + i);
		if (turn > 0f)
		{
			dir = dir.Lerp(enemyDir, turn).Normalized();
			eyes = Mathf.Max(eyes, turn);
		}
		_spine[i * N + nb - 1] = new Vector4(hb.X, hb.Y, hb4.Z, hb4.W);
		float headLen = _girth[i] * 2.6f;
		for (int k = 1; k <= HeadPts; k++)
		{
			Vector2 hp = hb + dir * headLen * k / HeadPts;
			_spine[i * N + nb - 1 + k] = new Vector4(hp.X, hp.Y, hb4.Z, hb4.W);
		}
		Vector2 up = new(dir.Y, -dir.X);
		if (up.Y > 0f) up = -up;
		_hup[i] = up;
		Vector2 eye = hb + dir * headLen * 0.38f + up * _girth[i] * 0.42f;
		_eyes[i] = new Vector3(eye.X, eye.Y, eyes * hb4.W);
	}

	/// <summary>
	/// 攻击的时间线（从挑中算）：0～0.3 这条蛇散成烟 → 0.18 敌人身上一张大嘴从深处朝镜头冲出来（0.2s 放大、嘴张开）
	/// → 0.4～0.46 咬合（BiteAt：闪光、火星、震屏、顿帧）→ 0.5～0.85 大嘴散成烟 → 0.7～1.1 这条蛇在原来的轨道上重新聚起来。
	/// </summary>
	private void UpdateAttacks(float dt, Vector2 feet, float T)
	{
		for (int i = 0; i < Count; i++) { _diss[i] = 0f; _lunge[i] = Vector2.Zero; }
		for (int s = 0; s < Slots; s++)
		{
			_mawA[s] = 0f;
			if (_attacks[s] is not { } a) continue;
			a.T += dt;
			float t = a.T;
			_diss[a.Serpent] = Smooth(0.06f, 0.32f, t) * (1f - Smooth(0.7f, 1.1f, t));
			// 窜出去：0.12s 内冲出去，边冲边散；散完以后不再偏（重新聚起来时在原轨道上）
			Vector3 hp = Orbit(a.Serpent, _tau[a.Serpent], T, 1f);
			Vector2 toward = a.Target - new Vector2(hp.X, hp.Y);
			float lk = Ease5.OutQuint(Ease5.Seg(t, 0f, 0.18f)) * (1f - Smooth(0.45f, 0.6f, t));
			_lunge[a.Serpent] = toward.LimitLength(0.45f * T) * lk;
			float k = Ease5.OutQuint(Ease5.Seg(t, 0.2f, 0.16f));
			float shut = Ease5.Seg(t, 0.38f, BiteAt - 0.38f);
			shut = shut * shut;                                  // 越合越快，猛地咬死
			_maw[s] = new Vector4(a.Target.X, a.Target.Y, k, shut);
			_mawA[s] = Smooth(0.18f, 0.24f, t) * (1f - Smooth(0.5f, 0.85f, t));
			if (!a.Bit && t >= BiteAt)
			{
				a.Bit = true;
				Vector2 at = feet + a.Target;
				Glow.Flash(_front, at, Pal5.Crimson, 0.9f * T, 0.25f);
				Glow.Flash(_front, at + new Vector2(0f, 0.15f * T), Pal5.Hot, 0.35f * T, 0.12f);
				Glow.Sparks(_front, at, 30, 200f, 700f, 0.55f, new[] { Pal5.Bright, Pal5.Crimson, Pal5.Deep }, 250f, 0.12f, 0.38f, Vector2.Up, 160f);
				Fx.Shake(2);
				HitStop.Request(0.04);
			}
			if (t > AttackLen) _attacks[s] = null;
		}
	}
}
