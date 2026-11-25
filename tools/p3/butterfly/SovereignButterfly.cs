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
	// 拖尾（用户 2026-11-23：原来那串连续的光团太黏，像一坨橡皮泥黏在屁股上、像负重）：
	// 改成「翅尖两道一下就散的细光丝」+「下扑时从翅尖抖落、甩出去以后自己飘的鳞粉」——粒子出来就不跟着蝴蝶走了
	private const int Tip = 14, MaxDust = 40;
	private const float TipSpan = 0.45f;

	private readonly Func<(Vector2 Feet, float Tall)?> _body;
	private readonly ColorRect _front, _back;
	private readonly ShaderMaterial _fm, _bm;
	private readonly Vector4[] _tipL = new Vector4[Tip], _tipR = new Vector4[Tip];     // xy = 画布像素，z = 亮度，w = 深度
	private readonly Vector3[] _tipHistL = new Vector3[64], _tipHistR = new Vector3[64];
	private readonly float[] _tipT = new float[64];
	private int _tipN;
	private readonly Vector2[] _dp = new Vector2[MaxDust], _dv = new Vector2[MaxDust];
	private readonly float[] _dAge = new float[MaxDust], _dLife = new float[MaxDust], _dDepth = new float[MaxDust];
	private readonly Vector4[] _dust = new Vector4[MaxDust];
	private int _dNext;
	private float _emitAcc, _prevS;
	private readonly RandomNumberGenerator _rng = new();
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
		// 屏幕上的翅尖（右 / 左翅的翅尖在翅面坐标 (s 0.95, t 0.55)）
		Vector3 ScrV(Vector3 v) => new(v.X, v.Y - v.Z * 0.18f, v.Z);
		Vector3 f3 = ScrV(_fwd), r3 = ScrV(r), u3 = ScrV(u);
		// 光丝从翅膀「平均张开」时的翅尖拉出（不跟着扑翅上下跳，不然拉成一道道锯齿折线）
		const float aMid = 0.45f;
		Vector3 wr3 = r3 * Mathf.Cos(aMid) + u3 * Mathf.Sin(aMid), wl3 = -r3 * Mathf.Cos(aMid) + u3 * Mathf.Sin(aMid);
		Vector2 tipR = sp + (new Vector2(f3.X, f3.Y) * 0.55f + new Vector2(wr3.X, wr3.Y) * 0.95f) * scale;
		Vector2 tipL = sp + (new Vector2(f3.X, f3.Y) * 0.55f + new Vector2(wl3.X, wl3.Y) * 0.95f) * scale;
		_tipHistL.AsSpan(0, 63).CopyTo(_tipHistL.AsSpan(1)); _tipHistR.AsSpan(0, 63).CopyTo(_tipHistR.AsSpan(1)); _tipT.AsSpan(0, 63).CopyTo(_tipT.AsSpan(1));
		_tipHistL[0] = new Vector3(tipL.X, tipL.Y, depth); _tipHistR[0] = new Vector3(tipR.X, tipR.Y, depth); _tipT[0] = _t;
		_tipN = Math.Min(_tipN + 1, 64);
		for (int i = 0; i < Tip; i++)
		{
			float age = TipSpan * i / (Tip - 1), fade = 1f - (float)i / (Tip - 1);
			Vector3 hl = SampleTip(_tipHistL, _t - age), hr = SampleTip(_tipHistR, _t - age);
			_tipL[i] = new Vector4(hl.X - c0.X, hl.Y - c0.Y, fade, hl.Z);
			_tipR[i] = new Vector4(hr.X - c0.X, hr.Y - c0.Y, fade, hr.Z);
		}
		// 鳞粉：下扑的那一下从两个翅尖各抖落 1～2 粒，平时零星几粒；出来以后自己飘（带一点蝴蝶的惯性，空气拖慢、轻轻打旋、慢慢往下落）
		Vector2 bv = _histN > 3 ? new Vector2(_hist[0].X - _hist[3].X, _hist[0].Y - _hist[3].Y) / Mathf.Max(_histT[0] - _histT[3], 1e-3f) : Vector2.Zero;
		bool down = _prevS > 0.62f && s <= 0.62f;
		_prevS = s;
		// 2026-11-23 第二版（用户：正下方挂着一竖串，很黏）：只在下扑和飞得快时放，飞得慢几乎不放；
		// 粒子散在刚飞过的那一小段路上、往后和侧面散开、几乎不下坠 → 留下的是沿飞行路线往后渐淡的星点，不是吊在身上的一串
		float vpx = bv.Length() / Mathf.Max(T, 1f);
		_emitAcc += dt * 14f * Mathf.Clamp((vpx - 0.35f) / 0.8f, 0f, 1f);
		int emit = down ? 2 : 0;
		while (_emitAcc >= 1f) { emit++; _emitAcc -= 1f; }
		for (int e = 0; e < emit; e++)
		{
			int k = _dNext; _dNext = (_dNext + 1) % MaxDust;
			Vector2 at = ((e % 2 == 0) ? tipR : tipL).Lerp(sp, _rng.RandfRange(0f, 0.5f)) - bv * _rng.RandfRange(0f, 0.08f);
			_dp[k] = at + new Vector2(_rng.RandfRange(-1f, 1f), _rng.RandfRange(-1f, 1f)) * scale * 0.2f;
			Vector2 back = bv.LengthSquared() > 1f ? -bv.Normalized() : Vector2.Zero;
			Vector2 sideV = new Vector2(-back.Y, back.X) * _rng.RandfRange(-1f, 1f);
			_dv[k] = back * T * _rng.RandfRange(0.04f, 0.12f) + sideV * T * 0.1f;
			_dAge[k] = 0f;
			_dLife[k] = _rng.RandfRange(0.3f, 0.6f);
			_dDepth[k] = depth;
		}
		for (int k = 0; k < MaxDust; k++)
		{
			if (_dLife[k] <= 0f) { _dust[k] = new Vector4(0f, 0f, 0f, 0f); continue; }
			_dAge[k] += dt;
			float lifeK = 1f - _dAge[k] / _dLife[k];
			if (lifeK <= 0f) { _dLife[k] = 0f; _dust[k] = Vector4.Zero; continue; }
			_dv[k] *= Mathf.Exp(-3f * dt);
			_dv[k] += new Vector2(Mathf.Sin(_dp[k].Y * 0.025f + _t * 2.1f + k), Mathf.Cos(_dp[k].X * 0.025f + _t * 1.7f + k)) * T * 0.06f * dt;
			_dv[k].Y += T * 0.012f * dt;
			_dp[k] += _dv[k] * dt;
			_dust[k] = new Vector4(_dp[k].X - c0.X, _dp[k].Y - c0.Y, lifeK, _dDepth[k]);
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
			m.SetShaderParameter("tip_l", _tipL);
			m.SetShaderParameter("tip_r", _tipR);
			m.SetShaderParameter("dust", _dust);
			m.SetShaderParameter("unit", T);
			m.SetShaderParameter("clock", _t);
			m.SetShaderParameter("alpha", _alpha);
			m.SetShaderParameter("burst", burst);
		}
	}

	private Vector3 SampleTip(Vector3[] h, float t)
	{
		if (_tipN == 0) return Vector3.Zero;
		for (int i = 0; i < _tipN - 1; i++)
		{
			if (_tipT[i + 1] <= t)
			{
				float span = _tipT[i] - _tipT[i + 1];
				float u = span > 1e-5f ? (t - _tipT[i + 1]) / span : 0f;
				return h[i + 1].Lerp(h[i], u);
			}
		}
		return h[_tipN - 1];
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
uniform vec4 tip_l[14];
uniform vec4 tip_r[14];
uniform vec4 dust[40];
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

	// ── 翅尖两道细光丝：很细、很淡、0.3s 就散（划过空气的痕迹，不是一条光带）──
	for (int side_i = 0; side_i < 2; side_i++) {
		for (int i = 0; i < 13; i++) {
			vec4 a0 = side_i == 0 ? tip_l[i] : tip_r[i];
			vec4 a1 = side_i == 0 ? tip_l[i + 1] : tip_r[i + 1];
			float lw = layerW(a0.w);
			if (lw <= 0.001 || a0.z <= 0.001) continue;
			vec2 ab = a1.xy - a0.xy;
			float h = clamp(dot(p - a0.xy, ab) / max(dot(ab, ab), 1e-3), 0.0, 1.0);
			float d = length(p - (a0.xy + ab * h));
			float fz = mix(a0.z, a1.z, h);
			float w = unit * 0.0022 * (0.3 + fz);
			glow += vec3(0.55, 0.85, 1.0) * (exp(-d * d / (w * w)) * 0.45 + exp(-d / (w * 5.0)) * 0.08) * fz * fz * lw;
		}
	}
	// ── 鳞粉：甩出去自己飘的小闪光（四角星 + 一点芯），一闪一闪 ──
	for (int i = 0; i < 40; i++) {
		vec4 dp = dust[i];
		if (dp.z <= 0.001) continue;
		float lw = layerW(dp.w);
		if (lw <= 0.001) continue;
		vec2 dd = p - dp.xy;
		float hk = h12(vec2(float(i), 7.0));
		float rr = unit * mix(0.0025, 0.0055, hk) * (0.6 + 0.4 * dp.z);
		if (dot(dd, dd) > rr * rr * 120.0) continue;
		float tw = 0.35 + 0.65 * pow(0.5 + 0.5 * sin(clock * (10.0 + hk * 8.0) + hk * 40.0), 3.0);
		float star = exp(-abs(dd.x) * abs(dd.y) / (rr * rr * 0.5)) * exp(-length(dd) / (rr * 3.5));
		float dotg = exp(-dot(dd, dd) / (rr * rr));
		glow += mix(vec3(0.45, 0.75, 1.0), vec3(0.85, 0.95, 1.0), hk) * (star * 0.8 + dotg) * tw * dp.z * lw;
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
