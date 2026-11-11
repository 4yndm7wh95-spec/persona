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
uniform vec2 size;
uniform float side = 1.0;
uniform float clock = 0.0;
uniform float unit = 300.0;     // Joker 身高（像素）
uniform float diss[5];          // 每条蛇散成烟的程度（攻击时 0 → 1 → 0）
uniform vec4 maw[3];            // 敌人身上冲出来的大嘴：xy 中心，z 由远到近（0～1），w 张嘴（0～1）
uniform float maw_a[3];         // 大嘴的浓度（散掉时往 0 走）

float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { return vn(p) * 0.5 + vn(p * 2.03 + 5.1) * 0.3 + vn(p * 4.1 + 1.7) * 0.2; }
float ease3(float x) { return 1.0 - pow(1.0 - clamp(x, 0.0, 1.0), 3.0); }

// 粗细（s：0 尾 → 1 吻尖）：尾巴细、身体、脖子略收、头上平下宽的楔形
float profile(float s, float up) {
	float body = mix(0.15, 1.0, smoothstep(0.0, 0.42, s)) * mix(1.0, 0.72, smoothstep(0.55, 0.84, s));
	if (s < 0.86) return body;
	float hs = (s - 0.86) / 0.14;
	float top = mix(0.72, 0.95, smoothstep(0.0, 0.3, hs)) * mix(1.0, 0.45, smoothstep(0.3, 1.0, hs));
	float jaw = mix(0.72, 1.18, smoothstep(0.0, 0.25, hs)) * mix(1.0, 0.3, smoothstep(0.25, 1.0, hs));
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
		float headness = smoothstep(0.8, 0.88, bs);
		float L = bs * 7.0;
		float acr = sign(upv) * best / g;
		float nz = fbm(vec2(L - clock * 1.3, acr * 1.1 - clock * 0.2) + float(j) * 11.0);
		float fine = vn(vec2(L * 3.5 - clock * 2.6, acr * 3.0) + float(j) * 4.0);
		float dj = diss[j];
		float torn = (nz - 0.5) * mix(0.8, 0.36, headness) + (fine - 0.5) * 0.2 + (1.0 - smoothstep(0.0, 0.3, bs)) * nz * 0.9;
		torn += dj * (0.5 + nz * 1.8);                                  // 散成烟：边被撕开、往里吃
		float sd = r - 1.0 + torn;
		float dens = smoothstep(0.25, -0.45, sd) * (1.0 - dj * 0.6);
		// 边缘里面一薄层透光（越靠上越亮：天光从上面来）
		float shell = smoothstep(-0.34, -0.05, sd) * (1.0 - smoothstep(-0.05, 0.1, sd));
		float flick = fbm(vec2(L * 1.7 - clock * 2.2, acr * 2.0) + 3.0 + float(j));
		float lit = 0.55 + 0.45 * smoothstep(-0.5, 0.8, -n2.y);
		vec3 emit = vec3(0.6, 0.05, 0.07) * shell * smoothstep(0.5, 0.82, flick) * 1.1 * lit * (1.0 + dj * 1.5);   // 断断续续的透光，不成一圈描边
		float nz2 = fbm(vec2(L * 2.0 - clock * 1.8, acr * 1.6) + float(j) * 2.0);
		vec3 inner = mix(vec3(0.012, 0.003, 0.006), vec3(0.12, 0.022, 0.032), smoothstep(0.3, 0.85, nz2)) * mix(1.0, 0.7, smoothstep(0.6, 0.0, r));
		// 外面一缕缕往上飘的暗烟
		float wn = fbm(vec2(L * 0.7 - clock * 0.8, best / g * 0.9 + clock * 0.9 * sign(n2.y)) + 7.0 + float(j) * 3.0);
		float wisp = smoothstep(0.5, 0.85, wn) * smoothstep(mix(1.8, 2.8, smoothstep(0.0, 0.8, -n2.y)), 0.9, r) * 0.6 * (1.0 - headness * 0.7);
		wisp += dj * (1.0 - dj) * 3.0 * smoothstep(0.4, 0.8, wn) * smoothstep(3.0, 0.6, r);   // 散的时候一大团烟往外、往上飘
		// 几条蛇之间连成一片的薄烟（迷迷糊糊、不分得那么清楚）
		wisp = max(wisp, exp(-pow(r / 2.4, 2.0)) * 0.22 * smoothstep(0.35, 0.75, wn));
		float dim = bz < 0.0 ? 0.8 : 1.0;
		vec3 c = (inner + emit) * dim;
		vec3 wc = vec3(0.03, 0.008, 0.012) + vec3(0.22, 0.02, 0.03) * smoothstep(0.72, 0.9, wn);
		float a = clamp(dens * 0.88 + wisp * (1.0 - dens), 0.0, 1.0) * ba;
		vec3 cc = mix(wc, c, dens / max(dens + wisp * (1.0 - dens), 1e-3));
		over(acc, cc, a);
		// 眼睛：一团晕开的暖红雾光
		vec2 ed = p - eyes[j].xy;
		float e2 = dot(ed, ed);
		glow += (vec3(1.0, 0.55, 0.25) * exp(-e2 / pow(g * 0.11, 2.0)) * 1.2 + vec3(0.9, 0.12, 0.08) * exp(-e2 / pow(g * 0.4, 2.0)) * 0.45) * eyes[j].z * (1.0 - dj);
	}
	// 敌人身上：一张从画面深处朝镜头冲出来的大嘴（正面看的蛇头：两边宽的头、中间张开的嘴把敌人罩在里面，两颗獠牙、两团眼光），
	// 咬合以后整团散成烟。全用距离场 + 噪声，没有贴图。
	if (side > 0.0) {
		float T = unit;
		for (int i = 0; i < 3; i++) {
			if (maw_a[i] <= 0.0) continue;
			float k = maw[i].z, op = maw[i].w, ma = maw_a[i];
			float S = T * 0.95 * mix(0.12, 1.0, k);                  // 头半宽：由远到近一下子放大
			vec2 q = (p - maw[i].xy) / S;
			if (abs(q.x) > 2.6 || abs(q.y) > 2.6) continue;
			float gone = 1.0 - ma;
			float n = fbm(q * 2.2 + vec2(0.0, clock * 0.8) + float(i) * 9.0);
			float n2 = vn(q * 6.0 - clock * 1.5);
			// 头：上宽下窄的圆钝楔形（正面），额头两侧鼓一点
			// 正面的蛇头：扁宽、两腮往外撑、往上收成平的头顶（不要圆鼓鼓的「耳朵」）
			float head = length(q / vec2(1.12 - 0.18 * smoothstep(-0.8, 0.6, -q.y), 0.78) - vec2(0.0, 0.08)) - 1.0;
			// 嘴：中间一道弧形的口子，张开时上下分开
			float mx = clamp(q.x / 0.82, -1.0, 1.0);
			float lip = sqrt(max(1.0 - mx * mx, 0.0));
			float mouth = abs(q.y - 0.18) - 0.6 * op * lip;
			float inMouth = smoothstep(0.04, -0.04, mouth) * step(abs(q.x), 0.82);
			float dens = smoothstep(0.2, -0.45, head + (n - 0.5) * 0.7 + (n2 - 0.5) * 0.15 + gone * (0.5 + n * 1.6)) * (1.0 - inMouth);
			// 咬合缝和嘴里的一圈红光（嘴里是空的，看得见敌人）
			float rim = smoothstep(0.0, -0.08, mouth) * (1.0 - smoothstep(-0.08, -0.2, mouth)) * step(abs(q.x), 0.85) * op * smoothstep(0.45, 0.8, n);
			float seam = exp(-pow((q.y - 0.18 + (n2 - 0.5) * 0.06) / 0.025, 2.0)) * (1.0 - smoothstep(0.5, 0.85, abs(q.x))) * (1.0 - op) * step(0.5, k) * smoothstep(0.35, 0.7, n);
			vec3 sc = mix(vec3(0.012, 0.003, 0.006), vec3(0.11, 0.02, 0.03), smoothstep(0.35, 0.8, n));
			sc += vec3(0.55, 0.05, 0.07) * smoothstep(-0.3, -0.04, head) * smoothstep(0.5, 0.8, n) * 0.9;
			// 獠牙：上颚两颗往下、下颚两颗往上（亮的骨白，带红）
			float fang = 0.0;
			for (int s = 0; s < 2; s++) {
				float sx = s == 0 ? -0.42 : 0.42;
				vec2 fu = q - vec2(sx, 0.18 - 0.6 * op * sqrt(max(1.0 - pow(sx / 0.82, 2.0), 0.0)));
				float tu = clamp(fu.y / (0.42 * op + 0.01), 0.0, 1.0);
				fang = max(fang, smoothstep(0.06, -0.01, abs(fu.x + 0.05 * tu) - 0.055 * (1.0 - tu)) * step(0.0, fu.y) * step(fu.y, 0.42 * op));
				vec2 fl = q - vec2(sx * 0.8, 0.18 + 0.6 * op * sqrt(max(1.0 - pow(sx * 0.8 / 0.82, 2.0), 0.0)));
				float tl = clamp(-fl.y / (0.25 * op + 0.01), 0.0, 1.0);
				fang = max(fang, smoothstep(0.06, -0.01, abs(fl.x) - 0.04 * (1.0 - tl)) * step(fl.y, 0.0) * step(-0.25 * op, fl.y));
			}
			fang *= ma * smoothstep(0.3, 0.8, k);
			// 由远到近：四周一圈往外冲的烟（朝镜头扑过来）
			float ang = atan(q.y, q.x);
			float rad = length(q);
			float streak = smoothstep(0.55, 0.9, vn(vec2(ang * 7.0, rad * 2.0 - k * 6.0))) * smoothstep(0.9, 1.4, rad) * smoothstep(2.4, 1.5, rad) * (1.0 - k) * 1.5;
			over(acc, vec3(0.02, 0.004, 0.008), clamp(streak, 0.0, 0.7) * ma);
			over(acc, sc, clamp(dens * 0.9, 0.0, 1.0) * ma);
			over(acc, vec3(0.5, 0.38, 0.34) + vec3(0.25, 0.04, 0.05) * n, fang * 0.55 * smoothstep(0.25, 0.6, n2 + 0.3));   // 牙也是烟里透出来的，不是贴上去的白三角
			glow += vec3(0.7, 0.06, 0.07) * rim * 0.45 * ma + vec3(1.0, 0.4, 0.28) * seam * 0.9 * ma;
			// 眼睛：额头两侧两团暖红雾光
			for (int s = 0; s < 2; s++) {
				// 眼睛在两腮上方、斜着的细长一团（不是圆鼓鼓的大眼）
				vec2 e = q - vec2(s == 0 ? -0.62 : 0.62, -0.32);
				e = vec2(e.x * 0.8 + e.y * (s == 0 ? 0.6 : -0.6), e.y * 1.6);
				float e2 = dot(e, e);
				glow += (vec3(1.0, 0.5, 0.22) * exp(-e2 / 0.006) * 1.1 + vec3(0.8, 0.1, 0.07) * exp(-e2 / 0.04) * 0.3) * ma * smoothstep(0.2, 0.6, k);
			}
		}
	}
	acc.rgb += glow * (1.0 - acc.a * 0.4);
	acc.a = clamp(acc.a + dot(glow, vec3(0.33)) * 0.6, 0.0, 1.0);
	if (acc.a <= 0.002) discard;
	COLOR = acc;
}
" };

	/// <summary>
	/// 五条蛇各自的轨道（身高为单位，相对脚底；x 朝敌人为正）：中心 (cx, cy)、横向半径 rx、上下 ry、角速度 w（正负 = 方向）、相位、
	/// 深度：z = cos(角) * za + zb（&gt; 0 时转到 Joker 前面）、粗细、身长（身高为单位：身体沿头走过的路往回铺这么长，越老的位置越往下沉）。
	/// </summary>
	private static readonly (float Cx, float Cy, float Rx, float Ry, float W, float Ph, float Za, float Zb, float Size, float Len)[] Orbits =
	{
		// 用户：分布别太散 → 都收在 Joker 身边（左边不出画面），Len = 身长（身高为单位）
		(0.08f, -1.45f, 0.48f, 0.12f, 0.55f, 0.0f, 0.4f, -0.7f, 1.15f, 1.7f),    // 正后方最高、最大，慢慢转
		(-0.45f, -1.0f, 0.3f, 0.24f, -0.8f, 1.3f, 1f, -0.3f, 0.95f, 1.4f),      // 左边，竖着绕
		(0.6f, -1.15f, 0.32f, 0.22f, 0.75f, 2.4f, 1f, -0.3f, 0.95f, 1.4f),      // 右边（敌人那侧）
		(0.05f, -0.48f, 0.78f, 0.1f, 0.5f, 3.6f, 1f, 0f, 0.9f, 1.5f),          // 绕着腰贴身转，会转到 Joker 前面
		(-0.12f, -1.82f, 0.4f, 0.1f, -0.5f, 4.8f, 0.5f, -0.6f, 0.85f, 1.4f),   // 左后上方
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
	private float _t, _show = -1f, _intro = -1f, _fadeOut = -1f;
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
		for (int i = 0; i < Count; i++) _girth[i] = 0.12f * tall * Orbits[i].Size;
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
			Glow.Gather(_back, feet + new Vector2(0f, -0.5f * tall), Pal5.Deep, 1.6f * tall, 0.18f, 1f);
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
	}

	private static float Smooth(float a, float b, float x) { float t = Mathf.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3f - 2f * t); }

	/// <summary>第 i 条蛇的头在时刻 t 的位置（相对脚底，像素）和深度。</summary>
	private static Vector3 Orbit(int i, float t, float T, float face)
	{
		var o = Orbits[i];
		float a = o.W * t + o.Ph;
		float x = o.Cx + o.Rx * Mathf.Sin(a);
		float y = o.Cy + o.Ry * Mathf.Sin(2f * a + 0.7f * i) + 0.05f * Mathf.Sin(t * 0.9f + i * 1.7f);
		float z = Mathf.Cos(a) * o.Za + o.Zb;
		return new Vector3(x * face * T, y * T, z);
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
				Sfx.Play(Sfx.SerpentMaw, 0.75f);
				break;
			}
		}

		for (int i = 0; i < Count; i++) PoseSerpent(i, T, face, alpha);
		UpdateAttacks(dt, feet, T);

		for (int j = 0; j < Count; j++)
		{
			float m = _girth[j] * 3.2f;
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
		const int D = 120;
		Span<Vector3> trail = stackalloc Vector3[D];
		Span<float> acc = stackalloc float[D];
		float want = o.Len * T * o.Size, step = 0.06f;
		int used = D;
		for (int k = 0; k < D; k++)
		{
			float back = k * step;
			Vector3 v = Orbit(i, _t - back, T, face) + new Vector3(0f, back * 0.07f * T, 0f);
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
			v = chest.Lerp(v, grow);
			float a = Smooth(0f, 0.16f, f) * alpha;
			if (v.Z > 0f) a *= 0.85f;
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
		for (int i = 0; i < Count; i++) _diss[i] = 0f;
		for (int s = 0; s < Slots; s++)
		{
			_mawA[s] = 0f;
			if (_attacks[s] is not { } a) continue;
			a.T += dt;
			float t = a.T;
			_diss[a.Serpent] = Smooth(0f, 0.3f, t) * (1f - Smooth(0.7f, 1.1f, t));
			float k = Ease5.OutQuint(Ease5.Seg(t, 0.18f, 0.2f));
			float op = Smooth(0.2f, 0.32f, t) * (1f - Smooth(0.4f, BiteAt, t));
			_maw[s] = new Vector4(a.Target.X, a.Target.Y, k, op);
			_mawA[s] = Smooth(0.18f, 0.24f, t) * (1f - Smooth(0.5f, 0.85f, t));
			if (!a.Bit && t >= BiteAt)
			{
				a.Bit = true;
				Vector2 at = feet + a.Target;
				Glow.Flash(_front, at, Pal5.Crimson, 1.6f * T, 0.3f);
				Glow.Flash(_front, at + new Vector2(0f, 0.17f * T), Pal5.Hot, 0.6f * T, 0.12f);
				Glow.Sparks(_front, at, 30, 200f, 700f, 0.55f, new[] { Pal5.Bright, Pal5.Crimson, Pal5.Deep }, 250f, 0.12f, 0.38f, Vector2.Up, 160f);
				Fx.Shake(2);
				HitStop.Request(0.04);
			}
			if (t > AttackLen) _attacks[s] = null;
		}
	}
}
