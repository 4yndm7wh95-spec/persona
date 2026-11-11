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
	private const float Charge = 0.28f, Fly = 0.2f, Recover = 0.35f;

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
uniform vec4 bolt[3];           // xy 法球位置，zw 出发点（嘴）
uniform float bolt_a[3];
uniform vec3 charge[3];         // xy 嘴前，z 聚力（0～1）
uniform vec3 impact[3];         // xy 命中点，z 进度（0～1；< 0 = 没有）

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
		float torn = (nz - 0.5) * mix(0.8, 0.36, headness) + (fine - 0.5) * 0.2 + (1.0 - smoothstep(0.0, 0.3, bs)) * nz * 0.9;
		float sd = r - 1.0 + torn;
		float dens = smoothstep(0.18, -0.38, sd);
		// 边缘里面一薄层透光（越靠上越亮：天光从上面来）
		float shell = smoothstep(-0.34, -0.05, sd) * (1.0 - smoothstep(-0.05, 0.1, sd));
		float flick = fbm(vec2(L * 1.7 - clock * 2.2, acr * 2.0) + 3.0 + float(j));
		float lit = 0.55 + 0.45 * smoothstep(-0.5, 0.8, -n2.y);
		vec3 emit = vec3(0.62, 0.05, 0.07) * shell * smoothstep(0.5, 0.82, flick) * 1.4 * lit;   // 断断续续的透光，不成一圈描边
		float nz2 = fbm(vec2(L * 2.0 - clock * 1.8, acr * 1.6) + float(j) * 2.0);
		vec3 inner = mix(vec3(0.012, 0.003, 0.006), vec3(0.12, 0.022, 0.032), smoothstep(0.3, 0.85, nz2)) * mix(1.0, 0.7, smoothstep(0.6, 0.0, r));
		// 外面一缕缕往上飘的暗烟
		float wn = fbm(vec2(L * 0.7 - clock * 0.8, best / g * 0.9 + clock * 0.9 * sign(n2.y)) + 7.0 + float(j) * 3.0);
		float wisp = smoothstep(0.5, 0.85, wn) * smoothstep(mix(1.8, 2.8, smoothstep(0.0, 0.8, -n2.y)), 0.9, r) * 0.6 * (1.0 - headness * 0.7);
		float dim = bz < 0.0 ? 0.8 : 1.0;
		vec3 c = (inner + emit) * dim;
		vec3 wc = vec3(0.03, 0.008, 0.012) + vec3(0.22, 0.02, 0.03) * smoothstep(0.72, 0.9, wn);
		float a = clamp(dens * 0.94 + wisp * (1.0 - dens), 0.0, 1.0) * ba;
		vec3 cc = mix(wc, c, dens / max(dens + wisp * (1.0 - dens), 1e-3));
		over(acc, cc, a);
		// 眼睛：一团晕开的暖红雾光
		vec2 ed = p - eyes[j].xy;
		float e2 = dot(ed, ed);
		glow += (vec3(1.0, 0.55, 0.25) * exp(-e2 / pow(g * 0.11, 2.0)) * 1.2 + vec3(0.9, 0.12, 0.08) * exp(-e2 / pow(g * 0.4, 2.0)) * 0.45) * eyes[j].z;
	}
	if (side > 0.0) {
		float T = unit;
		for (int i = 0; i < 3; i++) {
			// 嘴前聚力：一团暗红的光越聚越亮，外面一圈往里收的烟
			if (charge[i].z > 0.0) {
				float d = length(p - charge[i].xy);
				float k = charge[i].z;
				float rr = T * (0.03 + 0.06 * k);
				glow += vec3(1.0, 0.45, 0.3) * exp(-d * d / (rr * rr * 0.25)) * 1.4 * k + vec3(0.8, 0.06, 0.08) * exp(-d * d / (rr * rr * 2.5)) * 0.8 * k;
				float ring = exp(-pow((d - T * 0.22 * (1.0 - k)) / (T * 0.04), 2.0)) * (1.0 - k) * k * 4.0;
				over(acc, vec3(0.04, 0.008, 0.012) + vec3(0.3, 0.02, 0.04) * ring, clamp(ring * 0.5, 0.0, 0.6));
			}
			// 法球：烟壳 + 热红的芯，拖一道烟尾
			if (bolt_a[i] > 0.0) {
				vec2 bp = bolt[i].xy, bf = bolt[i].zw;
				vec2 ab = bp - bf;
				float h = clamp(dot(p - bf, ab) / max(dot(ab, ab), 1e-3), 0.0, 1.0);
				float dt = length(p - (bf + ab * h));
				float tw = T * 0.05 * (0.25 + 0.75 * h);
				float tn = fbm(vec2(h * 8.0 - clock * 6.0, dt / T * 20.0) + float(i) * 5.0);
				float trail = smoothstep(tw * 1.3, tw * 0.2, dt + (tn - 0.5) * tw) * h * h * bolt_a[i];
				over(acc, vec3(0.03, 0.006, 0.01) + vec3(0.35, 0.02, 0.04) * smoothstep(0.6, 0.9, tn), trail * 0.8);
				glow += vec3(0.85, 0.08, 0.08) * exp(-dt * dt / (T * T * 0.0003)) * h * h * 0.7 * bolt_a[i];
				float d = length(p - bp);
				float sn = fbm((p - bp) / T * 12.0 + clock * 3.0);
				float sh = smoothstep(T * 0.13, T * 0.07, d + (sn - 0.5) * T * 0.05);
				over(acc, vec3(0.03, 0.006, 0.01) + vec3(0.45, 0.03, 0.05) * smoothstep(T * 0.04, T * 0.1, d), sh * 0.9 * bolt_a[i]);
				glow += (vec3(1.0, 0.6, 0.4) * exp(-d * d / (T * T * 0.0012)) * 1.6 + vec3(0.9, 0.1, 0.08) * exp(-d * d / (T * T * 0.012)) * 0.7) * bolt_a[i];
			}
			// 命中：一团往外翻滚、慢慢散掉的咒烟（不是细圈）+ 里面一下闪
			if (impact[i].z >= 0.0) {
				float k = impact[i].z;
				vec2 dv = p - impact[i].xy;
				float d = length(dv);
				float R = T * (0.14 + 0.5 * ease3(k));
				float bn = fbm(dv / T * 5.0 - vec2(0.0, k * 2.0) + float(i) * 7.0);
				float puff = smoothstep(R, R * 0.35, d + (bn - 0.5) * R * 0.9) * pow(1.0 - k, 1.4);
				float hole = smoothstep(R * 0.2, R * 0.75, d) * 0.5 + 0.5;
				over(acc, vec3(0.03, 0.006, 0.01) + vec3(0.45, 0.04, 0.06) * smoothstep(0.55, 0.85, bn) * (1.0 - k), clamp(puff * hole, 0.0, 1.0) * 0.85);
				glow += vec3(1.0, 0.4, 0.28) * exp(-d * d / (T * T * 0.02)) * pow(1.0 - k, 4.0) * 1.5;
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
	private readonly Vector4[] _bolt = new Vector4[Slots];
	private readonly float[] _boltA = new float[Slots];
	private readonly Vector3[] _charge = new Vector3[Slots];
	private readonly Vector3[] _impact = new Vector3[Slots];
	private readonly Queue<Vector2> _targets = new();
	private readonly Attack?[] _attacks = new Attack?[Slots];
	private float _t, _show = -1f, _intro = -1f, _fadeOut = -1f;
	private int _nextSerpent;

	private sealed class Attack
	{
		public int Serpent;
		public Vector2 Target;            // 相对脚底
		public float T;
		public bool Fired, Hit;
		public Vector2 Mouth;
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
		for (int i = 0; i < Slots; i++) _impact[i] = new Vector3(0f, 0f, -1f);
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
				Sfx.Play(Sfx.SerpentBolt, 0.7f);
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
			mat.SetShaderParameter("bolt", _bolt);
			mat.SetShaderParameter("bolt_a", _boltA);
			mat.SetShaderParameter("charge", _charge);
			mat.SetShaderParameter("impact", _impact);
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
		Attack? atk = AttackOf(i);
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
		// 出场最后五个头一起转向敌人；攻击时转向目标、往后一缩
		Vector2 enemyDir = new Vector2(face, 0.12f).Normalized();
		float turn = _intro >= 0f ? Smooth(1.05f, 1.25f, _intro) * (1f - Smooth(1.9f, 2.2f, _intro)) : 0f;
		float eyes = 0.45f + 0.1f * Mathf.Sin(_t * 2f + i);
		Vector2 recoil = Vector2.Zero;
		if (atk != null)
		{
			Vector2 toT = (atk.Target - hb).Normalized();
			float aim = Smooth(0f, 0.15f, atk.T) * (1f - Smooth(Charge + Fly, Charge + Fly + Recover, atk.T));
			dir = dir.Lerp(toT, aim).Normalized();
			float pull = Smooth(0f, Charge, atk.T) * (1f - Smooth(Charge, Charge + 0.06f, atk.T));
			float kick = Mathf.Exp(-Mathf.Pow((atk.T - Charge - 0.04f) / 0.05f, 2f));
			recoil = -toT * (pull * 0.1f + kick * 0.14f) * T;
			eyes = Mathf.Max(eyes, aim);
		}
		if (turn > 0f)
		{
			dir = dir.Lerp(enemyDir, turn).Normalized();
			eyes = Mathf.Max(eyes, turn);
		}
		hb += recoil;
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
		if (atk != null) atk.Mouth = hb + dir * headLen * 1.15f;
	}

	private void UpdateAttacks(float dt, Vector2 feet, float T)
	{
		for (int s = 0; s < Slots; s++)
		{
			_charge[s] = Vector3.Zero;
			_boltA[s] = 0f;
			if (_impact[s].Z >= 0f)
			{
				_impact[s].Z += dt / 0.5f;
				if (_impact[s].Z > 1f) _impact[s].Z = -1f;
			}
			if (_attacks[s] is not { } a) continue;
			a.T += dt;
			if (a.T < Charge)
			{
				_charge[s] = new Vector3(a.Mouth.X, a.Mouth.Y, Smooth(0f, Charge, a.T));
			}
			else if (a.T < Charge + Fly)
			{
				float k = (a.T - Charge) / Fly;
				k = Mathf.Min(k * k * (0.4f + 0.6f * k) + 0.25f * k, 1f);      // 一出口就快，越飞越快
				Vector2 from = a.Mouth;
				Vector2 mid = from.Lerp(a.Target, 0.5f) + new Vector2(0f, -0.25f * T);
				Vector2 pos = (1 - k) * (1 - k) * from + 2 * (1 - k) * k * mid + k * k * a.Target;
				_bolt[s] = new Vector4(pos.X, pos.Y, from.X, from.Y);
				_boltA[s] = 1f;
				if (!a.Fired)
				{
					a.Fired = true;
					Glow.Flash(_front, feet + from, Pal5.Crimson, 0.6f * T, 0.15f);
				}
			}
			else if (!a.Hit)
			{
				a.Hit = true;
				_impact[s] = new Vector3(a.Target.X, a.Target.Y, 0f);
				Vector2 at = feet + a.Target;
				Glow.Flash(_front, at, Pal5.Crimson, 1.3f * T, 0.3f);
				Glow.Sparks(_front, at, 28, 150f, 620f, 0.55f, new[] { Pal5.Bright, Pal5.Crimson, Pal5.Deep }, 200f, 0.12f, 0.38f, (a.Target - a.Mouth).Normalized(), 80f);
				Fx.Shake(1);
			}
			else if (a.T > Charge + Fly + Recover)
			{
				_attacks[s] = null;
			}
		}
	}
}
