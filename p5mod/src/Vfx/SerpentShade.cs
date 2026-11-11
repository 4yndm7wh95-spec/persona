using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 群蛇形态 · 蛇影（2026-11-11，用户让我自己想创意；之前立起来的大蛇 / 墨烟蛇都不要了）。
/// 不画立起来的蛇：Joker 脚下自己的影子变成一条大蛇的影子，在地面上游出去。
/// 从上往下看的蛇影最好认（菱形的头、S 形蜿蜒的身体），又不需要任何细节，也不吓人。
/// 动作：影子从脚下漫开 → 蛇影蜿蜒游到敌人脚下、从它们下面绕过去（经过的敌人暗一下）→ 游回来在 Joker 脚边盘两圈、收紧
/// → 沉进他的影子里，Joker 身上闪一下暗红。
/// 颜色：影子近黑，边缘透一圈很淡的暗红光（光从影子底下漏出来），两点小小的暖色眼睛；画面别的地方不动。
/// 全在一个地面着色器里：路线在 CPU 上算（地面坐标，x 横向、z 纵深），着色器里把像素换回地面坐标再求到脊线的距离，所以有透视的压扁。
/// </summary>
public sealed class SerpentShade
{
	public const float Life = 3.6f;
	private const int N = 40;                 // 脊线点数
	private const float Squash = 0.36f;       // 地面纵深在屏幕上压扁的比例
	private const int PathN = 600;

	private static Shader? _shader;

	private static Shader Shade => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec2 spine[40];          // 地面坐标（像素，z 没压扁），相对 Joker 脚底；0 = 尾尖，39 = 吻尖
uniform vec4 box;                // 地面坐标的包围盒
uniform float width = 40.0;      // 身体最宽处的半宽（地面像素）
uniform float squash = 0.3;
uniform vec2 origin;             // 四边形左上角（屏幕像素，相对脚底）
uniform vec2 size;
uniform float alpha = 1.0;
uniform float pool = 0.0;        // 脚下影子漫开的程度
uniform float pool_r = 100.0;
uniform float clock = 0.0;
uniform float eyes = 1.0;

float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }

// 从上往下看的蛇：尾尖细 → 身体 → 脖子略收 → 菱形的头（两腮最宽）→ 圆钝的吻
float profile(float s) {
	float body = mix(0.08, 1.0, smoothstep(0.0, 0.45, s)) * mix(1.0, 0.78, smoothstep(0.55, 0.84, s));
	float neck = mix(1.0, 0.82, smoothstep(0.82, 0.86, s));
	float head = 1.0 + 0.42 * smoothstep(0.85, 0.915, s);                 // 两腮鼓出来
	float snout = sqrt(max(1.0 - pow(smoothstep(0.915, 1.0, s), 2.0), 0.0)) * 0.75 + 0.25;   // 圆钝地收到吻尖
	return body * neck * head * mix(1.0, snout, step(0.915, s));
}

void fragment() {
	vec2 sp = origin + UV * size;
	vec2 p = vec2(sp.x, sp.y / squash);        // 换回地面坐标
	float A = 0.0;
	vec3 C = vec3(0.0);
	// 脚下的影子漫开（蛇从这里出来，最后沉回这里）
	float pd = length(p / vec2(1.0, 1.0)) / max(pool_r, 1.0);
	float pn = vn(p * 0.02 + vec2(clock * 0.3, 0.0));
	float pa = smoothstep(1.0, 0.45, pd + (pn - 0.5) * 0.35) * pool * 0.55;
	float prim = exp(-pow((pd + (pn - 0.5) * 0.35 - 0.9) / 0.2, 2.0)) * pool * 0.1;
	C = vec3(0.012, 0.004, 0.007) * pa + vec3(0.45, 0.07, 0.08) * prim;
	A = pa;
	if (p.x > box.x && p.y > box.y && p.x < box.z && p.y < box.w) {
		float best = 1e9, bs = 0.0;
		vec2 bq = p, bt = vec2(1.0, 0.0);
		for (int k = 0; k < 39; k++) {
			vec2 a = spine[k], b = spine[k + 1];
			vec2 ab = b - a;
			float h = clamp(dot(p - a, ab) / max(dot(ab, ab), 1e-3), 0.0, 1.0);
			vec2 q = a + ab * h;
			float d = length(p - q);
			if (d < best) { best = d; bs = (float(k) + h) / 39.0; bq = q; bt = normalize(ab + vec2(1e-4)); }
		}
		// 吻尖外面：圆头（到最后一点的距离）
		float w = width * profile(bs);
		float r = best / max(w, 0.5);
		float edgeN = vn(vec2(bs * 60.0, clock * 2.0)) - 0.5;
		float soft = 0.22;                           // 影子边缘柔
		float sh = smoothstep(1.0 + soft, 1.0 - soft, r + edgeN * 0.12);
		// 鳞：从上面看是一排排往后的「人」字，只是影子里明暗的一点点变化，往前流
		vec2 nrm = vec2(-bt.y, bt.x);
		float across = dot(p - bq, nrm) / max(w, 0.5);
		float along = bs * 70.0 - abs(across) * 1.2;
		float scale = smoothstep(0.6, 1.0, sin(along * 3.14159)) * smoothstep(0.9, 0.2, abs(across)) * (1.0 - smoothstep(0.88, 0.92, bs));
		vec3 sc = mix(vec3(0.015, 0.005, 0.008), vec3(0.06, 0.012, 0.018), scale * 0.8);
		// 边缘漏出来的暗红光：贴着影子外沿的一圈，头那边亮一点
		float rim = exp(-pow((r - 1.08) / 0.16, 2.0)) * (0.35 + 0.65 * smoothstep(0.6, 0.95, bs));
		float glowUnder = exp(-pow(best / (width * 1.4), 2.0)) * smoothstep(0.75, 1.0, bs) * 0.16;   // 头底下一小片红光
		// 眼睛：头两侧两点暖光（从上面看眼睛在两侧）
		float eA = 0.0;
		vec2 head = spine[39], neck = spine[35];
		vec2 hd = normalize(head - neck + vec2(1e-4));
		vec2 hn = vec2(-hd.y, hd.x);
		vec2 ec = head - hd * width * 0.85;
		for (int k = 0; k < 2; k++) {
			vec2 e = ec + hn * width * 0.55 * (k == 0 ? -1.0 : 1.0);
			float d2 = dot(p - e, p - e) / pow(width * 0.13, 2.0);
			eA += exp(-d2) + exp(-d2 * 0.12) * 0.25;
		}
		eA *= eyes;
		float a = sh * 0.9;
		vec3 c = sc * sh + vec3(0.55, 0.09, 0.09) * rim * 0.55 + vec3(0.5, 0.08, 0.08) * glowUnder;
		float ra = rim * 0.4 + glowUnder;
		C = C * (1.0 - a) + c;
		A = A + (1.0 - A) * clamp(a + ra * (1.0 - a), 0.0, 1.0);
		C += vec3(1.0, 0.5, 0.2) * eA * 0.9;
		A = max(A, clamp(eA, 0.0, 1.0));
	}
	if (A <= 0.002) discard;
	COLOR = vec4(C * alpha, A * alpha);
}" };

	private readonly Node2D _root;
	private readonly ShaderMaterial _m;
	private readonly float _tall, _width;
	private readonly Vector2[] _path = new Vector2[PathN];
	private readonly float[] _acc = new float[PathN];
	private readonly float _travel;          // 蛇头要走的总路程（到盘好为止）
	private readonly Vector2[] _spine = new Vector2[N];
	private readonly Vector2[] _enemies;
	private readonly Action<Color>?[] _enemyTint;
	private readonly Action<Color>? _selfTint;
	private const float BodyLen = 1.7f;      // 身长（身高为单位）

	/// <summary>
	/// 放一次。layer = 背景层（在角色下面）；feet = Joker 脚底；tall = 身高（像素）；face = 朝向；
	/// enemyFeet = 敌人脚底（蛇影从它们脚下绕过），enemyTint = 对应的染色回调（可空）；selfTint = Joker 的染色回调（可空）。
	/// </summary>
	public static SerpentShade Play(Control layer, Vector2 feet, float tall, float face, Vector2[] enemyFeet, Action<Color>?[]? enemyTint = null, Action<Color>? selfTint = null)
		=> new(layer, feet, tall, face, enemyFeet, enemyTint ?? new Action<Color>?[enemyFeet.Length], selfTint);

	private SerpentShade(Control layer, Vector2 feet, float tall, float face, Vector2[] enemyFeet, Action<Color>?[] enemyTint, Action<Color>? selfTint)
	{
		_tall = tall;
		_width = 0.19f * tall;
		_enemyTint = enemyTint;
		_selfTint = selfTint;
		_enemies = new Vector2[enemyFeet.Length];
		for (int i = 0; i < enemyFeet.Length; i++)
		{
			Vector2 d = enemyFeet[i] - feet;
			_enemies[i] = new Vector2(d.X, d.Y / Squash);
		}
		_travel = BuildPath(face);

		// 四边形盖住整片地面（Joker 后面一点到最远的敌人后面）
		float minX = -1.2f * tall, maxX = 1.2f * tall;
		foreach (Vector2 e in _enemies) { minX = Mathf.Min(minX, e.X - 1.0f * tall); maxX = Mathf.Max(maxX, e.X + 1.0f * tall); }
		Vector2 origin = new(minX, -1.0f * tall * Squash * 1.4f), size = new(maxX - minX, 2.2f * tall * Squash * 1.4f);
		_root = new Node2D { Name = "P5SerpentShade" };
		layer.AddChild(_root);
		_root.GlobalPosition = feet;
		_m = new ShaderMaterial { Shader = Shade };
		var q = new ColorRect { Position = origin, Size = size, Material = _m, MouseFilter = Control.MouseFilterEnum.Ignore };
		_root.AddChild(q);
		_m.SetShaderParameter("origin", origin);
		_m.SetShaderParameter("size", size);
		_m.SetShaderParameter("squash", Squash);
		_m.SetShaderParameter("width", _width);
		_m.SetShaderParameter("pool_r", 0.55f * tall);
		Fx.Clock(q);
		Fx.FreeAfter(_root, Life + 0.1);
		Update(0f);
		Fx.Animate(_root, 0, Life, k => Update(k * Life));
	}

	private static float Smooth(float a, float b, float x) { float t = Mathf.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3f - 2f * t); }

	private static Vector2 CatRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
	{
		float t2 = t * t, t3 = t2 * t;
		return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
	}

	/// <summary>
	/// 路线（地面坐标，像素）：从脚下出发 → 蜿蜒游到敌人那边、从它们前面过去、绕到后面 → 游回来 → 在 Joker 脚边盘两圈收紧。
	/// 蜿蜒（S 形）直接做在路线上（侧向的正弦按路程走），所以身体跟着头走过的同一条弯道 —— 真蛇就是这么游的。返回总路程。
	/// </summary>
	private float BuildPath(float face)
	{
		float T = _tall;
		float ex = 2.4f * T, ez = 0f;
		if (_enemies.Length > 0)
		{
			float sx = 0f, sz = 0f;
			foreach (Vector2 e in _enemies) { sx += e.X; sz += e.Y; }
			ex = sx / _enemies.Length; ez = sz / _enemies.Length;
		}
		ex = Mathf.Min(ex, 2.3f * T * Mathf.Abs(face)) * (ex < 0f ? -1f : 1f);
		// 往外：在敌人脚前（靠镜头的一侧，不被敌人挡住）蜿蜒过去，在中间那只敌人脚前绕一个弯，再从更靠前的地方游回来
		Vector2[] way =
		{
			new(0f, 0f), new(0.3f * T * face, 0.15f * T),
			new(ex * 0.4f, 0.5f * T), new(ex - 0.35f * T * face, 0.62f * T), new(ex + 0.45f * T * face, 0.45f * T),
			new(ex + 0.35f * T * face, 0.12f * T), new(ex - 0.3f * T * face, 0.2f * T),
			new(ex * 0.55f, 0.85f * T), new(0.85f * T * face, 0.5f * T),
		};
		// 盘：从 (0.85T, 0.5T) 附近开始，绕 Joker 两圈，半径从 0.7T 收到 0.38T
		const int coilPts = 28;
		var all = new Vector2[way.Length + coilPts];
		way.CopyTo(all, 0);
		float a0 = Mathf.Atan2(0.5f, 0.85f * face);
		for (int k = 0; k < coilPts; k++)
		{
			float f = (k + 1f) / coilPts;
			float a = a0 + face * f * Mathf.Tau * 1.75f;
			float r = Mathf.Lerp(0.78f, 0.4f, f) * T;
			all[way.Length + k] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 1.0f);
		}
		// Catmull-Rom 细分
		var raw = new System.Collections.Generic.List<Vector2>();
		for (int i = 0; i < all.Length - 1; i++)
		{
			Vector2 p0 = all[Math.Max(0, i - 1)], p1 = all[i], p2 = all[i + 1], p3 = all[Math.Min(all.Length - 1, i + 2)];
			for (int k = 0; k < 12; k++) raw.Add(CatRom(p0, p1, p2, p3, k / 12f));
		}
		raw.Add(all[^1]);
		var rawAcc = new float[raw.Count];
		for (int i = 1; i < raw.Count; i++) rawAcc[i] = rawAcc[i - 1] + (raw[i] - raw[i - 1]).Length();
		float total = rawAcc[^1];
		// 按路程均匀重采样，并加蜿蜒（侧向正弦，按路程；盘的那段蜿蜒小一点）
		int seg = 0;
		float lambda = 0.95f * T, amp = 0.13f * T;
		float coilStart = rawAcc[Math.Min(raw.Count - 1, (way.Length - 1) * 12)];
		for (int i = 0; i < PathN; i++)
		{
			float s = total * i / (PathN - 1);
			while (seg < raw.Count - 2 && rawAcc[seg + 1] < s) seg++;
			float f = rawAcc[seg + 1] > rawAcc[seg] ? (s - rawAcc[seg]) / (rawAcc[seg + 1] - rawAcc[seg]) : 0f;
			Vector2 pt = raw[seg].Lerp(raw[seg + 1], Mathf.Clamp(f, 0f, 1f));
			Vector2 tg = (raw[seg + 1] - raw[seg]).Normalized();
			Vector2 nrm = new(-tg.Y, tg.X);
			float a = amp * Mathf.Lerp(1f, 0.25f, Smooth(coilStart - 0.3f * T, coilStart + 0.4f * T, s)) * Smooth(0f, 0.6f * T, s);
			_path[i] = pt + nrm * Mathf.Sin(s / lambda * Mathf.Tau) * a;
		}
		for (int i = 1; i < PathN; i++) _acc[i] = _acc[i - 1] + (_path[i] - _path[i - 1]).Length();
		return _acc[PathN - 1];
	}

	private Vector2 At(float s)
	{
		if (s <= 0f) return _path[0] + (_path[1] - _path[0]).Normalized() * s * 0.2f;   // 还没出来的部分缩在脚下
		int lo = 0, hi = PathN - 1;
		while (hi - lo > 1) { int mid = (lo + hi) / 2; if (_acc[mid] < s) lo = mid; else hi = mid; }
		float f = _acc[hi] > _acc[lo] ? (s - _acc[lo]) / (_acc[hi] - _acc[lo]) : 0f;
		return _path[lo].Lerp(_path[hi], Mathf.Clamp(f, 0f, 1f));
	}

	private void Update(float t)
	{
		float T = _tall;
		// 时间线：0～0.3 影子漫开；0.25～2.75 游（先快后慢，盘的时候慢下来）；2.75～3.0 盘着收紧；3.0～3.45 沉进影子
		float go = Smooth(0.25f, 2.75f, t);
		float head = go * _travel;
		float sink = Smooth(3.0f, 3.45f, t);
		float len = BodyLen * T * (1f - 0.55f * sink);
		for (int k = 0; k < N; k++)
		{
			float s = head - len * (1f - k / (N - 1f));
			Vector2 pt = At(s);
			_spine[k] = pt * (1f - 0.5f * sink);
		}
		Vector2 lo = new(1e9f, 1e9f), hi = new(-1e9f, -1e9f);
		foreach (Vector2 q in _spine) { lo = new Vector2(Mathf.Min(lo.X, q.X), Mathf.Min(lo.Y, q.Y)); hi = new Vector2(Mathf.Max(hi.X, q.X), Mathf.Max(hi.Y, q.Y)); }
		float m = _width * 4f;
		_m.SetShaderParameter("spine", _spine);
		_m.SetShaderParameter("box", new Vector4(lo.X - m, lo.Y - m, hi.X + m, hi.Y + m));
		_m.SetShaderParameter("pool", Smooth(0f, 0.35f, t) * (1f - Smooth(3.2f, 3.6f, t)) * (1f + 0.4f * sink));
		_m.SetShaderParameter("alpha", Smooth(0.15f, 0.4f, t) * (1f - Smooth(3.25f, 3.6f, t)));
		_m.SetShaderParameter("eyes", Smooth(0.5f, 0.8f, t) * (1f - Smooth(3.0f, 3.25f, t)));
		// 经过的敌人暗一下
		Vector2 hp = _spine[N - 1];
		for (int i = 0; i < _enemies.Length; i++)
		{
			if (_enemyTint[i] is not { } tint) continue;
			float d = (hp - _enemies[i]).Length() / T;
			float k = Mathf.Clamp(1f - d / 0.9f, 0f, 1f);
			k = k * k * (3f - 2f * k) * (1f - sink);
			tint(new Color(1f - 0.32f * k, 1f - 0.42f * k, 1f - 0.38f * k));
		}
		// 沉进影子的那一下：Joker 身上闪一下暗红
		if (_selfTint != null && t > 2.95f)
		{
			float f = Mathf.Exp(-Mathf.Pow((t - 3.2f) / 0.12f, 2f));
			_selfTint(new Color(1f + 0.25f * f, 1f - 0.35f * f, 1f - 0.32f * f));
		}
	}
}
