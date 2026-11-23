using System;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// 君王之剑的替代（2026-11-23）：一只绕人物飞的 3D 蓝蝶。用户要求：是「特效」，纯着色器渲染、没有明显轮廓（参考毒性爆发大手的思路）；
/// 要有 3D 空间感 —— 蝴蝶本身是立体的（朝向、侧倾、翅膀在空间里张合），绕人物飞时真正穿到人物身后（被挡住）再绕到身前。
/// **还没接进 mod**：只依赖 Godot。接入：整份搬进 mod/src/Vfx，在 SovereignBladeReskinPatch 里 BladeReskin.Hide 之后
/// Start(人物前面的特效层, 人物后面的特效层, () => (脚底, 身高))；君王之剑节点释放时 Stop()。
///
/// 渲染（一个着色器，前景 / 背景两块画布，按深度分层、穿过人物时交叉淡入淡出）：
/// · 3D 机身：C# 每帧给出蝴蝶在 3D 里的位置和姿态（前向 f = 飞行方向，背向 u，右向 r；转弯时绕 f 侧倾），正交投影到屏幕。
/// · 翅膀是两片真的平面：右翅平面由 f 和 w = cos(a)·r + sin(a)·u 张成（a = 扑翅角，左翅镜像）。每个像素解出它落在翅面上的
///   (翼展 s, 沿身 t) 坐标 —— 所以扑翅、侧倾、转身都有真实透视；翅膀转到侧面会变成一道光。两片翅膀按深度先后叠。
/// · 翅面：前翅（尖、往前上方伸）+ 后翅（圆）+ 一条小尾突，边缘细波浪；半透明玻璃蓝，随朝向变虹彩（蓝紫 ↔ 青）；
///   从翅根放射的翅脉、中室的一圈脉、翅缘一圈小亮斑；从翅根往外流动的光；边上一层柔光晕，没有描边。
/// · 身体：一节节发光的胸腹 + 头 + 两根带发光小球的触须（都是 3D 点投影）。
/// · 扑翅：相位非线性（下扑快、上抬慢），频率随飞行速度；一帧里叠三个相邻扑翅角（扑得快时自然带出动感模糊）；慢下来会滑翔。
/// · 拖尾：四角星形的闪光 + 往下飘的鳞粉，沿最近 0.75s 的路径，也按深度分前后。
/// </summary>
public sealed class SovereignButterfly
{
	private const int Trail = 24;
	private const float TrailSpan = 0.75f;

	private readonly Func<(Vector2 Feet, float Tall)?> _body;
	private readonly ColorRect _front, _back;
	private readonly ShaderMaterial _fm, _bm;
	private readonly Vector4[] _trail = new Vector4[Trail];
	private readonly Vector3[] _hist = new Vector3[256];
	private readonly float[] _histT = new float[256];
	private int _histN;
	private float _t, _theta, _flap, _dart, _nextDart = 2.0f, _alpha, _bank;
	private Vector3 _fwd = new(1f, 0f, 0f), _prevP, _prevV;
	private bool _stopping, _first = true;
	private readonly float _seed;

	private SovereignButterfly(Control front, Control back, Func<(Vector2 Feet, float Tall)?> body, float seed)
	{
		_body = body;
		_seed = seed;
		_fm = new ShaderMaterial { Shader = Shader };
		_bm = new ShaderMaterial { Shader = Shader };
		_fm.SetShaderParameter("side", 1f);
		_bm.SetShaderParameter("side", -1f);
		_front = new ColorRect { Material = _fm, MouseFilter = Control.MouseFilterEnum.Ignore, Name = "P3Butterfly" };
		_back = new ColorRect { Material = _bm, MouseFilter = Control.MouseFilterEnum.Ignore, Name = "P3ButterflyBack" };
		front.AddChild(_front);
		back.AddChild(_back);
		SceneTree tree = front.GetTree();
		void Frame()
		{
			if (!GodotObject.IsInstanceValid(_front) || !GodotObject.IsInstanceValid(_back)) { tree.ProcessFrame -= Frame; return; }
			try { Tick((float)_front.GetProcessDeltaTime()); }
			catch (Exception e) { GD.PushError("P3 butterfly: " + e.Message); tree.ProcessFrame -= Frame; }
		}
		tree.ProcessFrame += Frame;
		_front.TreeExiting += () => tree.ProcessFrame -= Frame;
	}

	/// <summary>开始绕人物飞。front / back = 人物前面 / 后面的特效层；body 每帧给出人物脚底（层坐标）和身高，拿不到返回 null（蝴蝶淡出）。</summary>
	public static SovereignButterfly Start(Control front, Control back, Func<(Vector2 Feet, float Tall)?> body, float seed = 0f) => new(front, back, body, seed);

	/// <summary>淡出后释放。</summary>
	public void Stop() => _stopping = true;

	private static float N1(float x, float s) => Mathf.Sin(x + s) * 0.5f + Mathf.Sin(x * 2.17f + s * 1.7f) * 0.3f + Mathf.Sin(x * 4.31f + s * 2.3f) * 0.2f;

	/// <summary>θ 处的 3D 位置（身高为单位：x 右、y 下、z 朝镜头；原点在脚底）。</summary>
	private Vector3 Path(float th, float t)
	{
		float R = 0.6f + 0.1f * Mathf.Sin(2f * th + 0.7f + _seed) + 0.05f * N1(t * 0.9f, _seed + 3f);
		float x = R * Mathf.Cos(th) * 1.05f;
		float z = R * Mathf.Sin(th);
		float h = 0.6f + 0.3f * Mathf.Sin(1.6f * th + 0.4f * Mathf.Sin(t * 0.5f) + _seed) + 0.07f * Mathf.Sin(4.7f * th + 1.3f);
		x += 0.03f * N1(t * 2.6f, _seed + 7f);
		h += 0.035f * N1(t * 3.1f, _seed + 11f);
		return new Vector3(x, -h, z);
	}

	private void Tick(float dt)
	{
		dt = Mathf.Min(dt, 0.05f);
		_t += dt;
		var b = _body();
		_alpha = Mathf.MoveToward(_alpha, _stopping || b == null ? 0f : 1f, dt * 2.5f);
		if (_stopping && _alpha <= 0f) { Free(); return; }
		if (b == null) { _front.Visible = _back.Visible = false; return; }
		var (feet, T) = b.Value;
		_front.Visible = _back.Visible = true;

		// 速度：两层慢噪声 + 不定时的「一冲」→ 时快时慢
		if (_t >= _nextDart) { _dart = 1f; _nextDart = _t + 2.3f + 2.2f * (0.5f + 0.5f * Mathf.Sin(_t * 3.7f + _seed)); }
		_dart = Mathf.Max(0f, _dart - dt / 0.5f);
		float burst = _dart * _dart * (3f - 2f * _dart);
		float speed = Mathf.Clamp(0.62f + 0.36f * N1(_t * 0.55f, _seed) + 1.0f * burst, 0.25f, 2.1f);
		_theta += dt * 0.9f * speed;

		Vector3 P = Path(_theta, _t);
		Vector3 V = _first ? new Vector3(1f, 0f, 0f) : (P - _prevP) / Mathf.Max(dt, 1e-4f);
		Vector3 A = _first ? Vector3.Zero : (V - _prevV) / Mathf.Max(dt, 1e-4f);
		_prevP = P; _prevV = V; _first = false;

		// 姿态：前向 = 飞行方向（稍微抬头），平滑；侧倾 = 横向加速度（往弯里倒）
		Vector3 want = (V.Normalized() + new Vector3(0f, -0.35f, 0f)).Normalized();
		_fwd = _fwd.Lerp(want, Mathf.Min(1f, dt * 6f)).Normalized();
		Vector3 worldUp = new(0f, -1f, 0f);
		Vector3 r0 = _fwd.Cross(worldUp);
		if (r0.LengthSquared() < 1e-4f) r0 = new Vector3(0f, 0f, 1f);
		r0 = r0.Normalized();
		Vector3 u0 = r0.Cross(_fwd).Normalized();                    // 背向（大致朝上）
		float lateral = A.Dot(r0);
		_bank = Mathf.Lerp(_bank, Mathf.Clamp(-lateral * 0.12f, -0.8f, 0.8f), Mathf.Min(1f, dt * 4f));
		Vector3 r = r0 * Mathf.Cos(_bank) + u0 * Mathf.Sin(_bank);
		Vector3 u = u0 * Mathf.Cos(_bank) - r0 * Mathf.Sin(_bank);

		// 扑翅：非线性相位（下扑快、上抬慢）；慢的时候幅度小、偶尔滑翔
		_flap += dt * Mathf.Tau * (4.6f + 3.4f * Mathf.Min(speed, 1.6f));
		float glide = Mathf.Clamp(1f - (speed - 0.35f) / 0.3f, 0f, 1f) * (0.5f + 0.5f * Mathf.Sin(_t * 0.8f + _seed));
		float ph = _flap + 0.6f * Mathf.Sin(_flap);
		float s = 0.5f + 0.5f * Mathf.Sin(ph);
		float amp = Mathf.Lerp(1f, 0.3f, glide);
		float a = Mathf.Lerp(-0.35f, 1.25f, s) * amp + 0.12f * (1f - amp);
		float da = 0.18f * amp * Mathf.Abs(Mathf.Cos(ph)) * (4.6f + 3.4f * Mathf.Min(speed, 1.6f)) / 8f;   // 三个相邻角的间隔（扑得快 → 模糊开）

		// 投影：正交、稍微俯视（深度往上挪一点）；近大远小
		Vector2 Proj(Vector3 q) => feet + new Vector2(q.X, q.Y - q.Z * 0.18f) * T;
		Vector2 sp = Proj(P);
		float depth = P.Z / 0.6f;
		float scale = T * 0.115f * (1f + 0.25f * Mathf.Clamp(depth, -1f, 1f));

		_hist.AsSpan(0, _hist.Length - 1).CopyTo(_hist.AsSpan(1));
		_histT.AsSpan(0, _histT.Length - 1).CopyTo(_histT.AsSpan(1));
		_hist[0] = new Vector3(sp.X, sp.Y, depth);
		_histT[0] = _t;
		_histN = Math.Min(_histN + 1, _hist.Length);

		Vector2 c0 = feet + new Vector2(-1.2f, -1.45f) * T, size = new Vector2(2.4f, 1.8f) * T;
		foreach (ColorRect cr in new[] { _front, _back }) { cr.Position = c0; cr.Size = size; }
		for (int i = 0; i < Trail; i++)
		{
			float age = TrailSpan * (i + 1) / Trail;
			Vector3 h = Sample(_t - age);
			float fade = 1f - (float)i / (Trail - 1);
			_trail[i] = new Vector4(h.X - c0.X, h.Y - c0.Y, fade, h.Z);
		}
		// 屏幕坐标系下的 3D 方向（y 向下；z 朝镜头，投影时 z 往上挪 0.18）
		Vector3 Scr(Vector3 v) => new(v.X, v.Y - v.Z * 0.18f, v.Z);
		foreach (ShaderMaterial m in new[] { _fm, _bm })
		{
			m.SetShaderParameter("size_px", size);
			m.SetShaderParameter("pos", sp - c0);
			m.SetShaderParameter("fwd", Scr(_fwd));
			m.SetShaderParameter("right", Scr(r));
			m.SetShaderParameter("up", Scr(u));
			m.SetShaderParameter("flap", a);
			m.SetShaderParameter("flap_spread", da);
			m.SetShaderParameter("scale", scale);
			m.SetShaderParameter("depth", depth);
			m.SetShaderParameter("trail", _trail);
			m.SetShaderParameter("unit", T);
			m.SetShaderParameter("clock", _t);
			m.SetShaderParameter("alpha", _alpha);
			m.SetShaderParameter("burst", burst);
		}
	}

	private Vector3 Sample(float t)
	{
		if (_histN == 0) return Vector3.Zero;
		for (int i = 0; i < _histN - 1; i++)
		{
			if (_histT[i + 1] <= t)
			{
				float span = _histT[i] - _histT[i + 1];
				float u = span > 1e-5f ? (t - _histT[i + 1]) / span : 0f;
				return _hist[i + 1].Lerp(_hist[i], u);
			}
		}
		return _hist[_histN - 1];
	}

	private void Free()
	{
		if (GodotObject.IsInstanceValid(_front)) _front.QueueFree();
		if (GodotObject.IsInstanceValid(_back)) _back.QueueFree();
	}

	private static Shader? _shader;
	private static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec2 size_px = vec2(800.0);
uniform vec2 pos = vec2(400.0);
uniform vec3 fwd = vec3(1.0, 0.0, 0.0);
uniform vec3 right = vec3(0.0, 0.0, 1.0);
uniform vec3 up = vec3(0.0, -1.0, 0.0);
uniform float flap = 0.3;
uniform float flap_spread = 0.05;
uniform float scale = 30.0;
uniform float depth = 0.0;
uniform float side = 1.0;
uniform vec4 trail[24];
uniform float unit = 400.0;
uniform float clock = 0.0;
uniform float alpha = 1.0;
uniform float burst = 0.0;

float h12(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float vn(vec2 p) {
	vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h12(i), h12(i + vec2(1, 0)), f.x), mix(h12(i + vec2(0, 1)), h12(i + vec2(1, 1)), f.x), f.y);
}
float layerW(float d) { return side > 0.0 ? smoothstep(-0.14, 0.14, d) : 1.0 - smoothstep(-0.14, 0.14, d); }

float ell(vec2 q, vec2 c, vec2 r, float a) {
	vec2 d = q - c; float cs = cos(a), sn = sin(a);
	d = vec2(cs * d.x + sn * d.y, -sn * d.x + cs * d.y) / r;
	return 1.0 - length(d);
}
float smax(float a, float b, float k) { float h = clamp(0.5 + 0.5 * (a - b) / k, 0.0, 1.0); return mix(b, a, h) + k * h * (1.0 - h); }

// 翅面上 (s 翼展向外, t 沿身体向前) 的形状场：>0 在翅膀里
float wingField(vec2 q) {
	float th = atan(q.y, q.x);
	float scal = 0.018 * sin(th * 22.0);                                  // 边缘细波浪
	float fore = smax(ell(q, vec2(0.52, 0.30), vec2(0.56, 0.31), 0.55), ell(q, vec2(0.88, 0.62), vec2(0.2, 0.13), 0.7), 0.12);
	float hind = smax(ell(q, vec2(0.36, -0.28), vec2(0.4, 0.33), -0.4), ell(q, vec2(0.42, -0.7), vec2(0.07, 0.15), -0.15), 0.1);
	return smax(fore, hind, 0.08) + scal;
}

// 一片翅膀在像素 p 处：返回 premul 颜色 + 覆盖度，和深度
vec4 wing(vec2 p, vec3 w, float face, out float z) {
	// 解 p = pos + (t * f.xy + s * w.xy) * scale
	vec2 d = (p - pos) / scale;
	float det = fwd.x * w.y - fwd.y * w.x;
	z = -1e9;
	float ad = abs(det);
	if (ad < 0.02) return vec4(0.0);
	float t = (d.x * w.y - d.y * w.x) / det;
	float s = (fwd.x * d.y - fwd.y * d.x) / det;
	z = t * fwd.z + s * w.z;
	if (s < 0.0) return vec4(0.0);
	vec2 q = vec2(s, t);
	float F = wingField(q);
	float px = 1.5 / (scale * max(ad, 0.15));                             // 一个像素在翅面坐标里多大（侧过来时边缘不糊成锯齿）
	float cover = smoothstep(-px * 1.5, 0.03 + px, F);
	// 翅脉：从翅根放射 + 中室一圈
	vec2 rq = q - vec2(0.02, 0.0);
	float ang = atan(rq.y, rq.x), rr = length(rq);
	float rad = pow(abs(cos(ang * 6.5 + 0.25 * sin(rr * 6.0))), 40.0) * smoothstep(0.08, 0.3, rr);
	float cell = exp(-pow((length((q - vec2(0.36, 0.22)) / vec2(0.22, 0.12)) - 1.0) / 0.12, 2.0));
	float veins = clamp(rad + cell * 0.8, 0.0, 1.0) * smoothstep(0.0, 0.08, F);
	// 翅缘亮斑：沿边缘一圈小点
	float band = smoothstep(0.0, 0.05, F) * (1.0 - smoothstep(0.05, 0.13, F));
	float spots = smoothstep(0.55, 1.0, sin(atan(q.y - 0.05, q.x - 0.25) * 26.0)) * band;
	// 流光：从翅根往外流
	float flow = vn(vec2(rr * 7.0 - clock * 2.4, ang * 3.0)) * smoothstep(0.0, 0.25, F);
	// 颜色：虹彩随朝向（face = 翅面对镜头的程度）在蓝紫和青之间变；翅根亮、往外深
	vec3 violet = vec3(0.32, 0.24, 1.0), blue = vec3(0.1, 0.36, 1.0), cyan = vec3(0.35, 0.85, 1.0), white = vec3(0.9, 0.97, 1.0);
	vec3 base = mix(violet, cyan, face);
	base = mix(base, blue, smoothstep(0.1, 0.5, rr) * 0.6);
	float glass = 0.55 + 0.25 * (1.0 - face) + 0.25 * flow;
	vec3 c = base * (0.8 + 0.8 * exp(-rr * 2.2)) + white * (veins * 0.7 + spots * 1.0 + flow * 0.22);
	c *= 1.0 + 0.4 * burst;
	float a = cover * clamp(glass + veins * 0.3 + spots * 0.4, 0.0, 0.95);
	// 外面一圈柔光晕（不是描边）
	float halo = exp(-max(-F, 0.0) * 9.0) * (1.0 - cover) * 0.35 * smoothstep(-0.6, 0.0, F);
	return vec4(c * a + cyan * halo, a);
}

vec4 overC(vec4 top, vec4 bot) { return top + bot * (1.0 - top.a); }

void fragment() {
	vec2 p = UV * size_px;
	vec4 acc = vec4(0.0);
	vec3 glow = vec3(0.0);

	// ── 拖尾：四角星闪光 + 往下飘的鳞粉 ──
	for (int i = 0; i < 24; i++) {
		vec4 tp = trail[i];
		float lw = layerW(tp.w);
		if (lw <= 0.001 || tp.z <= 0.001) continue;
		float age = 1.0 - tp.z;
		for (int k = 0; k < 2; k++) {
			float hk = h12(vec2(float(i) * 1.7, float(k) + floor(clock * 0.4)));
			vec2 off = vec2((hk - 0.5) * unit * 0.07, age * age * unit * 0.14 + (h12(vec2(hk, 3.0)) - 0.5) * unit * 0.03);
			vec2 dd = p - (tp.xy + off);
			float rr = unit * (0.003 + 0.005 * tp.z);
			float tw = 0.4 + 0.6 * pow(0.5 + 0.5 * sin(clock * 8.0 + hk * 40.0), 2.0);
			float star = exp(-abs(dd.x) * abs(dd.y) / (rr * rr * 0.6)) * exp(-length(dd) / (rr * 3.0));
			float dotg = exp(-dot(dd, dd) / (rr * rr));
			glow += vec3(0.5, 0.82, 1.0) * (star * 0.9 + dotg) * tw * tp.z * lw;
		}
		vec2 d0 = p - tp.xy; float r0 = unit * mix(0.004, 0.016, tp.z);
		glow += vec3(0.2, 0.5, 1.0) * exp(-dot(d0, d0) / (r0 * r0)) * tp.z * 0.35 * lw;
	}

	// ── 蝴蝶本体（3D 翅膀 + 身体）──
	float wb = layerW(depth);
	if (wb > 0.001) {
		vec3 view = vec3(0.0, 0.0, 1.0);
		vec4 body = vec4(0.0);
		// 三个相邻扑翅角叠起来（动感模糊）
		for (int j = -1; j <= 1; j++) {
			float aa = flap + float(j) * flap_spread;
			float wj = j == 0 ? 0.5 : 0.25;
			vec3 wr = cos(aa) * right + sin(aa) * up;
			vec3 wl = -cos(aa) * right + sin(aa) * up;
			float fr = abs(dot(normalize(cross(fwd, wr)), view));
			float fl = abs(dot(normalize(cross(fwd, wl)), view));
			float zr, zl;
			vec4 cr = wing(p, wr, fr, zr);
			vec4 cl = wing(p, wl, fl, zl);
			vec4 both = zr > zl ? overC(cr, cl) : overC(cl, cr);
			body += both * wj;
		}
		acc = body;
		// 身体：胸腹 + 头 + 触须（3D 点投影，软光）
		vec2 fx = fwd.xy * scale, ux = up.xy * scale;
		vec3 bodyC = vec3(0.0);
		for (int k = 0; k < 6; k++) {
			float tt = mix(-0.42, 0.18, float(k) / 5.0);
			vec2 c = pos + fx * tt;
			float rad = scale * mix(0.035, 0.065, smoothstep(-0.42, 0.0, tt));
			vec2 dd = p - c;
			bodyC += vec3(0.75, 0.92, 1.0) * exp(-dot(dd, dd) / (rad * rad)) * 0.7;
		}
		vec2 hd = p - (pos + fx * 0.27);
		bodyC += vec3(0.9, 0.97, 1.0) * exp(-dot(hd, hd) / pow(scale * 0.06, 2.0)) * 0.9;
		for (int sgn = -1; sgn <= 1; sgn += 2) {
			vec3 ad = normalize(fwd * 0.85 + right * 0.32 * float(sgn) + up * 0.25);
			for (int k = 1; k <= 7; k++) {
				float u_ = float(k) / 7.0;
				vec3 off3 = fwd * 0.27 + ad * u_ * 0.5 + up * 0.08 * u_ * u_;
				vec2 c = pos + off3.xy * scale;
				vec2 dd = p - c;
				float rad = scale * (k == 7 ? 0.035 : 0.012);
				bodyC += vec3(0.6, 0.88, 1.0) * exp(-dot(dd, dd) / (rad * rad)) * (k == 7 ? 1.1 : 0.5);
			}
		}
		acc = vec4(acc.rgb + bodyC * (1.0 - acc.a * 0.6), acc.a);
		// 整只的柔光
		vec2 dd = p - pos;
		glow += vec3(0.15, 0.4, 1.0) * exp(-length(dd) / (scale * 1.6)) * 0.22 * (1.0 + burst);
		acc *= wb;
		glow *= 1.0;
	}
	vec3 col = acc.rgb + glow;
	COLOR = vec4(col * alpha, acc.a * alpha);
}
" };
}
