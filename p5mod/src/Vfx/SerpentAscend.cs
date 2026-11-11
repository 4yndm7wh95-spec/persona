using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 群蛇形态 · 升龙（2026-11-11。用户：蛇影太阴暗，群蛇形态是很强、很帅的牌，要有气势）。
/// 一条光做的巨蛇：脚下炸开 → 绕着 Joker 高速螺旋冲上去（前半圈在他前面、后半圈在身后，拖着光尾和火星）
/// → 冲过头顶在高处昂首定住一下，眼睛亮起一道横向的光、一圈冲击波（「吼」）→ 掉头俯冲，一头扎进 Joker 胸口
/// → 白闪、地面冲击环、火星往上喷、震屏，Joker 身上一层红光慢慢退掉。
/// 画法：不画任何细节（鳞、牙都不画），只靠光：热白的芯、绯红的身体、深红的外晕，身上的能量往尾巴流，隐约的人字鳞光。
/// 全部加法混合着色器；脊线（40 点，带深度和每点透明度）在 CPU 上沿路线取，身体走头走过的同一条路。
/// </summary>
public sealed class SerpentAscend
{
	public const float Life = 2.6f;
	private const int N = 40;
	private const int PathN = 500;

	private static Shader? _shader;

	private static Shader Body => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 spine[40];          // xy 相对 Joker 脚底的像素，z 深度（> 0 在 Joker 前面），w 这一点的透明度；0 = 尾尖，39 = 头
uniform vec4 box;
uniform float width = 40.0;
uniform vec2 origin;
uniform vec2 size;
uniform float side = 1.0;
uniform float clock = 0.0;
uniform float boost = 0.0;        // 昂首 / 扎进去时整体更亮
uniform vec2 head_base;          // 头根（脖子末端）
uniform vec2 head_dir = vec2(1.0, 0.0);
uniform vec2 head_up = vec2(0.0, -1.0);
uniform float head_z = 1.0;
uniform float head_a = 0.0;
uniform float head_open = 0.4;
uniform float eyes = 0.0;

float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { return vn(p) * 0.55 + vn(p * 2.1 + 3.7) * 0.3 + vn(p * 4.3 + 1.1) * 0.15; }
float sdE(vec2 p, vec2 r) { float k0 = length(p / r); float k1 = length(p / (r * r)); return k0 * (k0 - 1.0) / max(k1, 1e-4); }
float smin(float a, float b, float k) { float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0); return mix(b, a, h) - k * h * (1.0 - h); }
vec2 rot(vec2 v, float a) { float c = cos(a), s = sin(a); return vec2(c * v.x - s * v.y, s * v.x + c * v.y); }

// 尾巴细 → 身体 → 脖子略收 → 头（两腮鼓出来）→ 吻尖
float profile(float s) {
	float body = mix(0.1, 1.0, smoothstep(0.0, 0.45, s)) * mix(1.0, 0.7, smoothstep(0.6, 0.85, s));
	return body * (1.0 - smoothstep(0.84, 0.865, s));      // 身体在脖子收住，头单独画
}

void fragment() {
	vec2 p = origin + UV * size;
	vec3 col = vec3(0.0);
	if (p.x > box.x && p.y > box.y && p.x < box.z && p.y < box.w) {
		float best = 1e9, bs = 0.0, ba = 0.0, bz = 0.0;
		vec2 bt = vec2(1.0, 0.0), bq = p;
		for (int k = 0; k < 39; k++) {
			vec4 a = spine[k], b = spine[k + 1];
			if (max(a.w, b.w) <= 0.001) continue;
			if (side > 0.0 ? max(a.z, b.z) <= 0.0 : min(a.z, b.z) > 0.0) continue;
			vec2 ab = b.xy - a.xy;
			float h = clamp(dot(p - a.xy, ab) / max(dot(ab, ab), 1e-3), 0.0, 1.0);
			vec2 q = a.xy + ab * h;
			float d = length(p - q);
			if (d < best) { best = d; bs = (float(k) + h) / 39.0; ba = mix(a.w, b.w, h); bz = mix(a.z, b.z, h); bt = normalize(ab + vec2(1e-4)); bq = q; }
		}
		if (best < 1e8) {
			float w = width * profile(bs) * (1.0 + 0.12 * bz);
			float r = best / max(w, 0.5);
			vec2 nrm = vec2(-bt.y, bt.x);
			float across = dot(p - bq, nrm) / max(w, 0.5);
			float flow = fbm(vec2(bs * 22.0 + clock * 9.0, across * 1.4 + clock * 0.5));
			float core = exp(-pow(r / (0.34 + 0.08 * boost), 2.0)) * (0.7 + 0.6 * flow);
			float bodyM = smoothstep(1.12, 0.55, r + (flow - 0.5) * 0.3);
			float halo = exp(-r * 1.25) * 0.5;
			// 人字鳞光：只在身体里，往头那边一排排，随能量明暗
			float chev = smoothstep(0.75, 1.0, sin(bs * 150.0 - abs(across) * 5.0 + clock * 6.0)) * bodyM * smoothstep(0.85, 0.3, abs(across)) * (1.0 - smoothstep(0.86, 0.9, bs));
			vec3 hot = vec3(1.0, 0.93, 0.82), crim = vec3(1.0, 0.16, 0.12), deep = vec3(0.5, 0.02, 0.05), gold = vec3(1.0, 0.62, 0.25);
			col = hot * core * (1.0 + boost * 0.6) + crim * bodyM * (0.35 + 0.45 * flow) + deep * halo + gold * chev * 0.35;
			col *= ba * (bz < 0.0 ? 0.65 : 1.0);
		}
	}
	// 头：只用光画——轮廓一圈亮、里面淡淡的能量、张开的嘴里空着；上颚 / 下颌 / 两腮的形状读得出是蛇；眼睛两点热白 + 横向的光
	if ((side > 0.0) == (head_z > 0.0) && head_a > 0.001) {
		vec2 rel = p - head_base;
		vec2 q = vec2(dot(rel, head_dir), dot(rel, head_up)) / (width * 1.4);
		if (q.x > -1.5 && q.x < 4.0 && abs(q.y) < 2.5) {
			vec2 H = vec2(0.2, -0.02);
			vec2 u = rot(q - H, -head_open * 0.3) + H;
			vec2 l = rot(q - H, head_open * 0.7) + H;
			float cran = sdE(u - vec2(0.75, 0.22), vec2(1.0, 0.58));
			float snout = sdE(u - vec2(1.95, 0.1), vec2(0.95, 0.3));
			float dUp = max(smin(cran, snout, 0.3), -(u.y + 0.04));
			float dLo = max(sdE(l - vec2(1.2, -0.18), vec2(1.15, 0.21)), l.y + 0.04);
			float nape = sdE(q - vec2(-0.25, 0.0), vec2(0.65, 0.5));
			float d = min(smin(dUp, nape, 0.35), dLo);
			float hn = fbm(q * 1.6 + vec2(clock * 3.0, 0.0));
			float rim = exp(-pow(d / 0.13, 2.0));
			float fill = smoothstep(0.05, -0.35, d) * (0.45 + 0.4 * hn);
			float halo = exp(-max(d, 0.0) * 2.2) * 0.35;
			vec3 hcol = vec3(1.0, 0.85, 0.72) * rim * 1.1 + vec3(1.0, 0.18, 0.12) * fill + vec3(0.55, 0.03, 0.06) * halo;
			// 眉骨上一道更亮的棱
			hcol += vec3(1.0, 0.6, 0.4) * exp(-pow(sdE(u - vec2(0.95, 0.52), vec2(0.55, 0.06)) / 0.08, 2.0)) * 0.6;
			// 眼睛
			vec2 e = u - vec2(1.0, 0.3);
			float dot_ = exp(-dot(e, e) / 0.012) * 2.2;
			float streak = exp(-pow(e.x / (0.9 + 2.2 * eyes), 2.0) - pow(e.y / 0.045, 2.0)) * 1.0;
			hcol += (vec3(1.0, 0.97, 0.9) * dot_ + vec3(1.0, 0.45, 0.28) * streak) * (0.4 + 0.6 * eyes);
			col += hcol * head_a * (head_z < 0.0 ? 0.7 : 1.0);
		}
	}
	COLOR = vec4(col, 1.0);
}" };

	private readonly ShaderMaterial _frontM, _backM;
	private readonly Control _front, _back;
	private readonly Vector2 _feet;
	private readonly float _tall, _face, _width;
	private readonly Vector3[] _path = new Vector3[PathN];
	private readonly float[] _acc = new float[PathN];
	private readonly float _sHang, _sEntry;
	private readonly Vector4[] _spine = new Vector4[N];
	private readonly Action<Color>? _selfTint;
	private float _nextSpark;
	private bool _roared, _struck, _burst;
	private const float BodyLen = 2.3f;

	public static SerpentAscend Play(Control front, Control back, Vector2 feet, float tall, float face, Action<Color>? selfTint = null)
		=> new(front, back, feet, tall, face, selfTint);

	private SerpentAscend(Control front, Control back, Vector2 feet, float tall, float face, Action<Color>? selfTint)
	{
		_front = front;
		_back = back;
		_feet = feet;
		_tall = tall;
		_face = face;
		_width = 0.13f * tall;
		_selfTint = selfTint;
		(_sHang, _sEntry) = BuildPath();
		Vector2 origin = new(-1.6f * tall, -2.6f * tall), size = new(3.4f * tall, 2.9f * tall);
		_frontM = Make(front, origin, size, 1f);
		_backM = Make(back, origin, size, -1f);
		Update(0f);
		Node owner = _frontM.GetMeta("owner").As<Node>();
		Fx.Animate(owner, 0, Life, k => Update(k * Life));
	}

	private ShaderMaterial Make(Control layer, Vector2 origin, Vector2 size, float side)
	{
		Node2D root = Fx.Root(layer, _feet, side > 0 ? "P5SerpentAscendFront" : "P5SerpentAscendBack", Life + 0.1);
		var m = new ShaderMaterial { Shader = Body };
		var q = new ColorRect { Position = origin, Size = size, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		m.SetShaderParameter("origin", origin);
		m.SetShaderParameter("size", size);
		m.SetShaderParameter("side", side);
		m.SetShaderParameter("width", _width);
		m.SetMeta("owner", root);
		Fx.Clock(q);
		return m;
	}

	private static float Smooth(float a, float b, float x) { float t = Mathf.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3f - 2f * t); }

	private static Vector3 CatRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float t2 = t * t, t3 = t2 * t;
		return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
	}

	/// <summary>路线：螺旋上升 1.6 圈 → 冲过头顶到高处昂首 → 掉头俯冲进胸口（再往里一点，被「吞掉」）。返回 (昂首处路程, 胸口处路程)。</summary>
	private (float, float) BuildPath()
	{
		float T = _tall, f = _face;
		var pts = new System.Collections.Generic.List<Vector3>();
		const int spiral = 26;
		for (int k = 0; k <= spiral; k++)
		{
			float u = k / (float)spiral;
			float th = (-0.35f + u * 1.6f) * Mathf.Tau * f;
			float R = Mathf.Lerp(0.8f, 0.55f, u) * T;
			float y = Mathf.Lerp(0.08f, -1.15f, u) * T;
			pts.Add(new Vector3(Mathf.Sin(th) * R, y + Mathf.Cos(th) * 0.16f * T, Mathf.Cos(th)));
		}
		pts.Add(new Vector3(-0.15f * T * f, -1.5f * T, -0.4f));
		pts.Add(new Vector3(0.2f * T * f, -1.78f * T, -0.3f));
		int hangIdx = pts.Count;
		pts.Add(new Vector3(0.62f * T * f, -1.72f * T, -0.2f));       // 昂首：头在这里，朝敌人、略朝下
		pts.Add(new Vector3(0.78f * T * f, -1.4f * T, 0.3f));
		pts.Add(new Vector3(0.3f * T * f, -1.05f * T, 0.6f));
		int entryIdx = pts.Count;
		pts.Add(new Vector3(0.04f * T * f, -0.72f * T, 0.6f));       // 胸口
		pts.Add(new Vector3(-0.12f * T * f, -0.5f * T, 0.6f));
		pts.Add(new Vector3(-0.25f * T * f, -0.3f * T, 0.6f));

		var raw = new System.Collections.Generic.List<Vector3>();
		var idxAt = new int[pts.Count];
		for (int i = 0; i < pts.Count - 1; i++)
		{
			idxAt[i] = raw.Count;
			Vector3 p0 = pts[Math.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Math.Min(pts.Count - 1, i + 2)];
			for (int k = 0; k < 10; k++) raw.Add(CatRom(p0, p1, p2, p3, k / 10f));
		}
		idxAt[^1] = raw.Count;
		raw.Add(pts[^1]);
		var rawAcc = new float[raw.Count];
		for (int i = 1; i < raw.Count; i++) rawAcc[i] = rawAcc[i - 1] + new Vector2(raw[i].X - raw[i - 1].X, raw[i].Y - raw[i - 1].Y).Length();
		float total = rawAcc[^1];
		int seg = 0;
		for (int i = 0; i < PathN; i++)
		{
			float s = total * i / (PathN - 1);
			while (seg < raw.Count - 2 && rawAcc[seg + 1] < s) seg++;
			float t = rawAcc[seg + 1] > rawAcc[seg] ? (s - rawAcc[seg]) / (rawAcc[seg + 1] - rawAcc[seg]) : 0f;
			_path[i] = raw[seg].Lerp(raw[seg + 1], Mathf.Clamp(t, 0f, 1f));
			_acc[i] = s;
		}
		return (rawAcc[idxAt[hangIdx]], rawAcc[idxAt[entryIdx]]);
	}

	private Vector3 At(float s)
	{
		if (s <= 0f) return _path[0] + new Vector3(0f, -s * 0.15f, 0f);   // 还在地下
		if (s >= _acc[PathN - 1]) return _path[PathN - 1];
		float k = s / _acc[PathN - 1] * (PathN - 1);
		int i = Mathf.Clamp((int)k, 0, PathN - 2);
		return _path[i].Lerp(_path[i + 1], k - i);
	}

	private Vector2 Screen(Vector3 v) => _feet + new Vector2(v.X, v.Y);

	private void Update(float t)
	{
		float T = _tall;
		// 时间线：0.06～0.95 冲上去（先猛后收）→ 0.95～1.3 昂首（眼睛亮、吼）→ 1.3～1.5 俯冲扎进胸口 → 身体被吞完 → 余光
		float rise = Ease5.OutQuint(Ease5.Seg(t, 0.06f, 0.95f));
		float dive = Ease5.Seg(t, 1.3f, 0.32f);
		dive = dive * dive * (1.6f - 0.6f * dive);
		float head = rise * _sHang + dive * (_sEntry - _sHang + BodyLen * T * 1.05f);
		float hang = Smooth(0.9f, 1.0f, t) * (1f - Smooth(1.28f, 1.34f, t));
		for (int k = 0; k < N; k++)
		{
			float f = k / (N - 1f);
			float s = head - BodyLen * T * (1f - f);
			Vector3 v = At(s);
			// 昂首时身体微微起伏（S 波沿身体往尾巴传）
			float wave = Mathf.Sin(f * 9f - t * 10f) * 0.035f * T * hang * (1f - f * 0.6f);
			v += new Vector3(wave * 0.6f, wave, 0f);
			float a = Smooth(-0.15f * T, 0.2f * T, s) * (s > _sEntry ? 0f : 1f) * Smooth(0f, 0.06f, t);
			a *= 1f - Smooth(2.0f, 2.4f, t);
			_spine[k] = new Vector4(v.X, v.Y, v.Z, a);
		}
		Vector2 lo = new(1e9f, 1e9f), hi = new(-1e9f, -1e9f);
		foreach (Vector4 q in _spine) { if (q.W <= 0.001f) continue; lo = new Vector2(Mathf.Min(lo.X, q.X), Mathf.Min(lo.Y, q.Y)); hi = new Vector2(Mathf.Max(hi.X, q.X), Mathf.Max(hi.Y, q.Y)); }
		float m = _width * 6f;
		lo -= new Vector2(_width * 3f, _width * 3f); hi += new Vector2(_width * 3f, _width * 3f);
		var box = new Vector4(lo.X - m, lo.Y - m, hi.X + m, hi.Y + m);
		// 头：根在脊线第 34 点（脖子末端），朝向指向吻尖那几点；「上」取朝屏幕上方的那一侧
		Vector2 hb = new(_spine[34].X, _spine[34].Y), hp = new(_spine[N - 1].X, _spine[N - 1].Y);
		Vector2 hd = (hp - hb).LengthSquared() > 1e-3f ? (hp - hb).Normalized() : new Vector2(_face, 0f);
		Vector2 hu = new(-hd.Y, hd.X);
		if (hu.Y > 0f) hu = -hu;
		float eyes = Smooth(0.25f, 0.6f, t) * 0.4f + 0.6f * hang;
		float boost = hang * 0.8f + Mathf.Exp(-Mathf.Pow((t - 1.5f) / 0.08f, 2f));
		foreach (ShaderMaterial mat in new[] { _frontM, _backM })
		{
			mat.SetShaderParameter("spine", _spine);
			mat.SetShaderParameter("box", box);
			mat.SetShaderParameter("head_base", hb);
			mat.SetShaderParameter("head_dir", hd);
			mat.SetShaderParameter("head_up", hu);
			mat.SetShaderParameter("head_z", _spine[34].Z);
			mat.SetShaderParameter("head_a", _spine[34].W);
			mat.SetShaderParameter("head_open", 0.38f + 0.35f * hang);
			mat.SetShaderParameter("eyes", eyes);
			mat.SetShaderParameter("boost", boost);
		}

		// 一路洒火星
		if (t > _nextSpark && t < 1.5f && _spine[N - 1].W > 0.5f)
		{
			_nextSpark = t + 0.05f;
			Glow.Sparks(_spine[N - 1].Z > 0f ? _front : _back, Screen(new Vector3(hp.X, hp.Y, 0f)), 5, 40f, 160f, 0.45f,
				new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, -60f, 0.12f, 0.3f, -hd, 70f);
		}
		// 吼：昂首定住的那一下
		if (!_roared && t >= 0.97f)
		{
			_roared = true;
			Vector2 at = Screen(new Vector3(hp.X, hp.Y, 0f));
			Glow.Flash(_front, at, Pal5.Bright, 0.9f * T, 0.25f);
			Glow.SpeedLines(_front, at, Pal5.Bright, 16, 0.4f * T, 1.6f * T);
			Fx.Shake(1);
		}
		// 扎进胸口
		if (!_struck && head >= _sEntry)
		{
			_struck = true;
			Vector2 chest = Screen(new Vector3(0.04f * T * _face, -0.72f * T, 0f));
			Glow.Flash(_front, chest, Pal5.Hot, 1.3f * T, 0.3f);
			Glow.Shockwave(_back, _feet, Pal5.Crimson, 3.2f * T, 0.7f, 0.28f);
			Glow.Sparks(_front, chest, 40, 200f, 700f, 0.7f, new[] { Pal5.Hot, Pal5.Gold, Pal5.Bright, Pal5.Deep }, 300f, 0.15f, 0.45f, Vector2.Up, 160f);
			Fx.Shake(2);
		}
		// 开场：脚下炸开
		if (!_burst)
		{
			_burst = true;
			Glow.Flash(_back, _feet + new Vector2(0f, -0.05f * T), Pal5.Bright, 1.4f * T, 0.3f);
			Glow.Shockwave(_back, _feet, Pal5.Crimson, 2.6f * T, 0.5f, 0.28f);
			Glow.Sparks(_front, _feet + new Vector2(0.3f * T * _face, 0f), 24, 150f, 520f, 0.6f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 200f, 0.12f, 0.35f, Vector2.Up, 50f);
		}
		// Joker 身上：扎进去的一下亮红，再慢慢退
		if (_selfTint != null && t > 1.4f)
		{
			float k = Smooth(1.45f, 1.52f, t) * (1f - Smooth(1.58f, 2.1f, t));
			_selfTint(new Color(1f + 0.45f * k, 1f - 0.12f * k, 1f - 0.12f * k));
		}
	}
}
