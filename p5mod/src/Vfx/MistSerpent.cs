using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 群蛇形态 · 墨烟蛇（用户 2026-11-11：八岐大蛇太艳、不像蛇、太丑太吓人，又不要太分明的形象）。
/// 思路：靠「动作」认出是蛇，不靠画清楚的样子。
/// - 颜色：画面保持原样，只压暗四角；蛇是近黑到深酒红的烟，只有朝上的一侧被天光勾出一线暗红。日蚀只是天上一个小点。
/// - 形象：一条平滑、往尾巴变细的烟带，头是简单的三角形轮廓（窄脖子 → 宽的头根 → 尖吻），不画牙、不张嘴、不画鳞；眼睛最多一点很弱的暖光。
/// - 动作：一道 S 形的波从尾巴传到头；身体沿着自己走过的路线前进（真蛇的走法）。
///   主蛇从脚下的雾里绕着 Joker 盘旋升起（前半圈在他前面、后半圈在他身后），过了头顶朝敌人探一下头，再散回烟里；
///   两条小的在身后，S 形地升起来，更淡，藏在雾里。
/// 全部着色器：脊线在 CPU 上算好（每条 24 个点，带深度），着色器里求到折线的距离、按体长套一条蛇的粗细曲线，边缘用流动的噪声撕成烟。
/// 前后两层：深度 &gt; 0 的部分画在前景层（盖住 Joker），其余画在背景层。
/// </summary>
public sealed class MistSerpent
{
	private const int N = 24;          // 每条蛇的脊线点数（最后 HeadPts 个是头）
	private const int HeadPts = 4;
	private const int Count = 3;
	public const float Life = 3.1f;

	private static Shader? _body, _air;

	private static Shader Body => _body ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec3 spine[72];          // 3 条 × 24 点：xy = 相对 Joker 脚底的像素，z = 深度（> 0 在 Joker 前面）
uniform float girth[3];          // 身体最粗处的半宽（像素）
uniform float vis[3];
uniform vec3 eyes[3];            // xy 位置，z 亮度
uniform vec2 origin;             // 这块四边形左上角相对脚底的像素
uniform vec2 size;
uniform float side = 1.0;        // 1 前景层 / -1 背景层
uniform float clock = 0.0;
uniform float dissolve = 0.0;
uniform float ground = 0.0;      // 地面（像素 y，相对脚底）

float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { return vn(p) * 0.5 + vn(p * 2.03 + 5.1) * 0.3 + vn(p * 4.1 + 1.7) * 0.2; }

// 蛇的粗细（s：0 尾尖 → 1 吻尖）：尾巴细 → 身体 → 脖子收窄 → 头根最宽 → 三角形收到吻尖
float profile(float s) {
	float body = mix(0.12, 1.0, smoothstep(0.0, 0.42, s)) * mix(1.0, 0.72, smoothstep(0.45, 0.8, s));
	float neck = 1.0 - 0.28 * smoothstep(0.74, 0.82, s);
	float hs = clamp((s - 0.82) / 0.18, 0.0, 1.0);
	float head = mix(0.62, 1.0, smoothstep(0.0, 0.18, hs)) * mix(1.0, 0.38, smoothstep(0.25, 1.0, hs));
	return s < 0.82 ? body * neck : head * 1.12;
}

void fragment() {
	vec2 p = origin + UV * size;
	vec3 acc = vec3(0.0);
	float A = 0.0;
	for (int j = 0; j < 3; j++) {
		if (vis[j] <= 0.001) continue;
		float best = 1e9, bs = 0.0, bsg = 1.0, bz = 0.0;
		vec2 bdir = vec2(1.0, 0.0), bq = p;
		for (int k = 0; k < 23; k++) {
			vec3 a = spine[j * 24 + k], b = spine[j * 24 + k + 1];
			if (side > 0.0 ? max(a.z, b.z) <= 0.0 : min(a.z, b.z) > 0.0) continue;
			vec2 ab = b.xy - a.xy;
			float h = clamp(dot(p - a.xy, ab) / max(dot(ab, ab), 1e-3), 0.0, 1.0);
			vec2 q = a.xy + ab * h;
			float d = length(p - q);
			if (d < best) {
				best = d; bs = (float(k) + h) / 23.0; bz = mix(a.z, b.z, h); bq = q; bdir = normalize(ab + vec2(1e-4));
				bsg = sign(ab.x * (p.y - a.y) - ab.y * (p.x - a.x));
			}
		}
		float w = girth[j] * profile(bs) * (1.0 + 0.12 * bz);
		if (best > w * 2.6) continue;
		float headness = smoothstep(0.78, 0.86, bs);
		float L = bs * 11.0;
		vec2 flowp = vec2(L + clock * 1.3, bsg * best / girth[j] * 0.9 - clock * 0.25);     // 烟往尾巴、往外流
		float nz = fbm(flowp + float(j) * 13.0);
		float fine = vn(flowp * 3.0 + 4.0);
		float r = best / w;
		float torn = (nz - 0.5) * mix(0.85, 0.3, headness) + (fine - 0.5) * 0.1;
		torn += smoothstep(0.25, 0.0, bs) * (nz - 0.3) * 0.9;            // 尾巴散成雾
		torn += dissolve * (0.5 + nz * 1.6);
		float dd = r + torn;
		float dens = smoothstep(1.0, 0.6, dd) * smoothstep(0.0, 0.08, bs);
		// 埋进地里的部分被地面的雾吞掉
		float under = smoothstep(ground + girth[j] * 0.2, ground - girth[j] * 1.6, p.y);
		dens *= under;
		// 光：只有朝上（略朝左上，天光那边）的一侧勾出一线暗红；里面近黑，带一点酒红的烟色变化
		vec2 n2 = (p - bq) / max(best, 1e-3);
		// 身体外面一缕缕的烟
		float wn = fbm(vec2(L * 0.6 + clock * 1.4, best / girth[j] * 0.8 - clock * 0.9) + 7.0 + float(j) * 3.0);
		float upSide = smoothstep(-0.3, 0.6, dot(n2, vec2(0.0, -1.0)));
		float wisp = smoothstep(0.48, 0.8, wn) * smoothstep(mix(1.8, 2.8, upSide), 1.0, r) * (1.0 - headness * 0.7) * 0.5 + smoothstep(1.9, 0.9, r) * 0.15;
		wisp *= under;
		float lit = max(dot(n2, normalize(vec2(-0.35, -1.0))), 0.0);
		float rim = smoothstep(0.35, 0.95, dd) * pow(lit, 1.4);
		// 体积感：朝光的半边整体透一点酒红，背光半边近黑
		float halfLit = smoothstep(-0.2, 0.8, dot(n2, normalize(vec2(-0.35, -1.0)))) * smoothstep(0.0, 0.9, r);
		vec3 core = mix(vec3(0.03, 0.012, 0.016), vec3(0.13, 0.04, 0.047), smoothstep(0.3, 0.8, nz)) + vec3(0.075, 0.016, 0.02) * halfLit;
		float band = smoothstep(0.75, 1.0, sin(L * 1.6 - clock * 4.0)) * (1.0 - headness) * 0.08;   // 沿身体往前爬的一点暗光：看得出在游动
		vec3 col = core + vec3(0.5, 0.13, 0.13) * rim * 0.9 + vec3(0.3, 0.07, 0.08) * band;
		float depthDim = bz < 0.0 ? 0.62 : 1.0;
		col *= depthDim;
		float a = clamp(dens * 0.93 + wisp * (1.0 - dens), 0.0, 1.0) * vis[j] * mix(0.8, 1.0, depthDim);
		vec3 wc = vec3(0.05, 0.016, 0.02) + vec3(0.12, 0.03, 0.035) * smoothstep(0.65, 0.9, wn);
		vec3 c = mix(wc, col, dens / max(dens + wisp * (1.0 - dens), 1e-3));
		// 眼睛：头上一点很弱的暖光（没有眼球、不瞪人）
		vec2 ed = p - eyes[j].xy;
		float e = exp(-dot(ed, ed) / pow(girth[j] * 0.16, 2.0)) * eyes[j].z * dens;
		c += vec3(0.55, 0.24, 0.12) * e * 0.9;
		// 叠起来（后来的蛇在下面）
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
	// 暗角：中间几乎不动，四角压暗，带一点点酒红
	float vig = smoothstep(0.3, 1.05, length((uv - vec2(0.45, 0.5)) * vec2(aspect * 0.55, 1.0)));
	vec3 col = vec3(0.025, 0.006, 0.01);
	float a = 0.1 + vig * 0.42;
	// 日蚀：一个小点 + 一圈很柔的红晕（不要白边）
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
	// 脚下的暗雾：翻涌、中间浓、往四周和上面淡开
	vec2 pd = (uv - pool) / vec2(pool_w, pool_w * 0.28);
	float pn = fbm(vec2(uv.x * 7.0 - clock * 0.15, uv.y * 12.0 + clock * 0.1));
	float pm = exp(-dot(pd, pd) * 1.1) * (0.45 + 0.7 * pn);
	col = mix(col, vec3(0.07, 0.02, 0.026) + vec3(0.12, 0.03, 0.035) * smoothstep(0.5, 0.9, pn), clamp(pm, 0.0, 1.0) * 0.8);
	a = max(a, clamp(pm * 0.55, 0.0, 0.6));
	COLOR = vec4(col * a * amount, a * amount);
}" };

	private readonly Node2D _frontRoot, _backRoot, _airRoot;
	private readonly ShaderMaterial _frontM, _backM, _airM;
	private readonly float _tall, _face;
	private readonly Vector2 _toEnemy;
	private readonly Vector3[] _spine = new Vector3[N * Count];
	private readonly float[] _girth = new float[Count], _vis = new float[Count];
	private readonly Vector3[] _eyes = new Vector3[Count];

	/// <summary>放一次。feet = Joker 脚底（层坐标），tall = 身高（像素），face = 朝向（1 朝右），enemy = 敌人大致的位置（主蛇往那边探头）。</summary>
	public static MistSerpent Play(Control front, Control back, Vector2 feet, float tall, float face, Vector2 enemy) => new(front, back, feet, tall, face, enemy);

	private MistSerpent(Control front, Control back, Vector2 feet, float tall, float face, Vector2 enemy)
	{
		_tall = tall;
		_face = face;
		Vector2 te = enemy - (feet + new Vector2(0f, -1.3f * tall));
		_toEnemy = te.LengthSquared() > 1f ? te.Normalized() : new Vector2(face, 0.1f).Normalized();
		_girth[0] = 0.13f * tall;
		_girth[1] = 0.09f * tall;
		_girth[2] = 0.08f * tall;

		Vector2 origin = new(-1.7f * tall, -2.3f * tall), size = new(3.6f * tall, 2.7f * tall);
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
		_airM.SetShaderParameter("pool_w", tallS * 0.9f / scr.X);
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

	private static float Ease(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp(x, 0f, 1f), 3f);
	private static float Smooth(float a, float b, float x) { float t = Mathf.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3f - 2f * t); }

	/// <summary>
	/// 主蛇的路线（u 0 → 1 盘旋上升，u &gt; 1 往敌人那边直伸出去）。返回 (x, y, 深度)，像素、相对脚底。
	/// 从侧前方的雾里出来，绕 Joker 转一圈多，过了头顶时正好在他正前方、朝着敌人走。
	/// </summary>
	private Vector3 MainPath(float u, float t)
	{
		float T = _tall;
		if (u > 1f)
		{
			Vector3 end = MainPath(1f, t);
			Vector3 pre = MainPath(0.97f, t);
			Vector2 tan = new Vector2(end.X - pre.X, end.Y - pre.Y).Normalized();
			float k = (u - 1f) / 0.3f;
			Vector2 dir = tan.Lerp(_toEnemy, Smooth(0f, 0.6f, k)).Normalized();
			return new Vector3(end.X + dir.X * k * 0.75f * T, end.Y + dir.Y * k * 0.75f * T, end.Z);
		}
		float th = (-2.3f + u * 2.3f) * Mathf.Pi * _face;
		float R = T * (0.78f - 0.2f * u);
		float x = R * Mathf.Sin(th), z = Mathf.Cos(th);
		float y = T * (0.12f - 1.62f * Smooth(0f, 1f, u * 0.85f + 0.15f * u * u)) + z * 0.1f * T;
		// 横向的 S 波：沿路线往前传
		float wave = Mathf.Sin((u * 3.2f - t * 0.9f) * Mathf.Tau) * 0.045f * T;
		return new Vector3(x + wave * Mathf.Cos(th), y + wave * 0.4f, z);
	}

	/// <summary>小蛇：在 Joker 身后 S 形地升起来，一直在背景层。</summary>
	private Vector3 SidePath(int i, float u, float t)
	{
		float T = _tall;
		float cx = (i == 1 ? -0.55f : 0.5f) * T * _face;
		float h = (i == 1 ? 1.25f : 1.05f) * T;
		float amp = 0.22f * T * (1f - 0.4f * u);
		float sx = Mathf.Sin(u * Mathf.Pi * 1.6f + (i == 1 ? 0.4f : 2.2f) - t * 0.9f) * amp;
		float lean = u * u * 0.35f * T * _face;
		return new Vector3(cx + sx + lean, 0.15f * T - u * h, -0.6f);
	}

	private Vector3 PathOf(int i, float u, float t) => i == 0 ? MainPath(u, t) : SidePath(i, u, t);

	private void Update(float t)
	{
		// 时间线：0～1.5 升起（小蛇晚一点）→ 1.75 朝敌人探头 → 2.05 收回一点 → 2.2 起散成烟
		float lunge = Smooth(1.72f, 1.88f, t) - 0.55f * Smooth(1.9f, 2.25f, t);
		float diss = Smooth(2.2f, 2.95f, t);
		for (int i = 0; i < Count; i++)
		{
			float start = i == 0 ? 0.12f : (i == 1 ? 0.3f : 0.42f);
			float rise = i == 0 ? 1.45f : 1.35f;
			float head = Ease((t - start) / rise);
			float headU = i == 0 ? head * 1f + lunge * 0.3f - diss * 0.25f : head * 0.95f - diss * 0.2f;
			float len = i == 0 ? 0.62f : 0.75f;                   // 身体沿路线占多长（u）
			_vis[i] = Smooth(start, start + 0.3f, t) * (1f - diss * 0.6f) * (i == 0 ? 1f : (i == 1 ? 0.65f : 0.55f));
			// 身体：从头往回沿路线均匀取点（按弧长），头：沿最后的切线伸出去一段直的三角形
			Vector3[] dense = new Vector3[64];
			float[] acc = new float[64];
			for (int k = 0; k < 64; k++)
			{
				float u = headU - len * (1f - k / 63f);
				dense[k] = PathOf(i, u, t);
				acc[k] = k == 0 ? 0f : acc[k - 1] + new Vector2(dense[k].X - dense[k - 1].X, dense[k].Y - dense[k - 1].Y).Length();
			}
			int nb = N - HeadPts;
			float total = acc[63];
			int seg = 0;
			for (int k = 0; k < nb; k++)
			{
				float target = total * k / (nb - 1);
				while (seg < 62 && acc[seg + 1] < target) seg++;
				float f = acc[seg + 1] > acc[seg] ? (target - acc[seg]) / (acc[seg + 1] - acc[seg]) : 0f;
				_spine[i * N + k] = dense[seg].Lerp(dense[seg + 1], Mathf.Clamp(f, 0f, 1f));
			}
			Vector3 tip = _spine[i * N + nb - 1], prev = _spine[i * N + nb - 3];
			Vector2 dir = new Vector2(tip.X - prev.X, tip.Y - prev.Y);
			dir = dir.LengthSquared() > 1e-3f ? dir.Normalized() : new Vector2(_face, 0f);
			float headLen = _girth[i] * 2.5f;
			for (int k = 1; k <= HeadPts; k++)
			{
				Vector2 hp = new Vector2(tip.X, tip.Y) + dir * headLen * k / HeadPts;
				_spine[i * N + nb - 1 + k] = new Vector3(hp.X, hp.Y, tip.Z);
			}
			// 眼睛：头根往前 40%，偏朝上那一侧
			Vector2 up = new Vector2(dir.Y, -dir.X);
			if (up.Y > 0f) up = -up;
			Vector2 eye = new Vector2(tip.X, tip.Y) + dir * headLen * 0.42f + up * _girth[i] * 0.3f;
			_eyes[i] = new Vector3(eye.X, eye.Y, Smooth(start + 0.6f, start + 1.2f, t) * (1f - diss) * (i == 0 ? 0.7f : 0.35f));
		}
		foreach (ShaderMaterial m in new[] { _frontM, _backM })
		{
			m.SetShaderParameter("spine", _spine);
			m.SetShaderParameter("girth", _girth);
			m.SetShaderParameter("vis", _vis);
			m.SetShaderParameter("eyes", _eyes);
			m.SetShaderParameter("dissolve", diss);
			m.SetShaderParameter("ground", 0.05f * _tall);
		}
		_airM.SetShaderParameter("amount", Smooth(0f, 0.45f, t) * (1f - Smooth(2.5f, 3.1f, t)));
	}
}
