using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 咒烟形状（2026-11-18，给紧勒的绞索、夜魇的梦魇之手用）：一串粗细渐变的胶囊（线段 + 两端半径）平滑地融成一个形状，
/// 材质完全照群蛇形态的咒烟蛇 / 毒性爆发的巨手（用户认可的「没有明确轮廓、纯靠渲染」）：
/// 近黑的核心，往边缘渐渐变成背光透过来的暗红；边里面一薄层断断续续透出暗红的光；朝上一侧一道细的暗红边光；
/// 边缘被顺着形状流动的噪声撕成烟，外面一缕缕往上飘的烟丝。不用贴图。普通混合（暗的烟要能压暗背景）。
/// 每一段有深度 z（&gt; 0 在角色前面）：前景层画 z &gt; 0 的部分、背景层画 z &lt; 0 的部分，交界处两层各淡一半接上（绕到身后的绳子、从身后伸出的手）。
/// 用法：每帧往 Segs 里放这一帧的形状，调 Commit()。
/// </summary>
public sealed class CurseForm
{
	public const int Max = 64;

	public struct Seg
	{
		public Vector2 A, B;
		public float Ra, Rb;
		public float Za, Zb;
		public float La, Lb;      // 沿形状的长度参数（噪声沿这个方向流）：一般 = 累计长度 / (身高 × 0.2)
		public float Alpha;
		public float Diss;        // 0 实 → 1 散成烟
		public float Tear;        // 边缘撕成烟的程度：1 正常（群蛇的身体），小一点更干净（手指）
	}

	public readonly List<Seg> Segs = new();
	/// <summary>整体往外透的暗红光（勒紧 / 抓住的那一下）：0～1。</summary>
	public float Flare;
	/// <summary>暗背景下看得清（蛇咬单独出场时的 ember）：0～1。</summary>
	public float Ember = 0.8f;
	/// <summary>绞绳纹（沿长度每单位多少弧度，0 = 没有）。</summary>
	public float TwistF;

	private readonly (Node2D Root, ColorRect Rect, ShaderMaterial M)[] _layers;
	private readonly float _unit;
	private float _clock;
	private readonly Vector4[] _sa = new Vector4[Max], _sb = new Vector4[Max], _sx = new Vector4[Max];
	private readonly float[] _tear = new float[Max];

	private static Shader? _shader;

	private static Shader Shade => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform vec4 sa[64];            // ax, ay, ra, za
uniform vec4 sb[64];            // bx, by, rb, zb
uniform vec4 sx[64];            // la, lb, alpha, diss
uniform float tear[64];
uniform int n = 0;
uniform vec2 origin;
uniform vec2 size;
uniform float side = 1.0;
uniform float clock = 0.0;
uniform float flare = 0.0;
uniform float ember = 0.8;
uniform float twist_f = 0.0;    // 绞绳纹：沿长度每单位多少弧度（0 = 没有；紧勒的咒烟绞索用）

float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { return vn(p) * 0.5 + vn(p * 2.03 + 5.1) * 0.3 + vn(p * 4.1 + 1.7) * 0.2; }
float smin(float a, float b, float k) { float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0); return mix(b, a, h) - k * h * (1.0 - h); }
void over(inout vec4 acc, vec3 c, float a) { acc.rgb += (1.0 - acc.a) * c * a; acc.a += (1.0 - acc.a) * a; }

void fragment() {
	vec2 p = origin + UV * size;
	// 所有胶囊平滑并集；离得最近的那段拿来算明暗（沿形状的参数、法线、深度）
	float sdU = 1e9, best = 1e9;
	float bh = 0.0, br = 1.0, bz = 0.0, bL = 0.0, ba = 0.0, bd = 0.0, bt = 1.0;
	vec2 bq = p;
	for (int k = 0; k < 64; k++) {
		if (k >= n) break;
		vec4 a = sa[k], b = sb[k], x = sx[k];
		if (x.z <= 0.001) continue;
		// 这一层只画自己那一边（交界 ±0.15 两层都画、各淡一半）
		if (side > 0.0 ? max(a.w, b.w) < -0.3 : (side < 0.0 && min(a.w, b.w) > 0.4)) continue;
		vec2 ab = b.xy - a.xy;
		float h = clamp(dot(p - a.xy, ab) / max(dot(ab, ab), 1e-3), 0.0, 1.0);
		vec2 cq = a.xy + ab * h;
		float r = mix(a.z, b.z, h);
		float sd = length(p - cq) - r;
		sdU = sdU > 1e8 ? sd : smin(sdU, sd, max(r * 0.45, 1.0));
		if (sd < best) {
			best = sd; bh = h; br = r; bq = cq; bz = mix(a.w, b.w, h); bL = mix(x.x, x.y, h); ba = x.z; bd = x.w; bt = tear[k];
		}
	}
	if (sdU > 1e8) discard;
	float sdn = sdU / max(br, 0.5);                 // 中线 ≈ -1，边 = 0
	if (sdn > 2.6) discard;
	float rr = 1.0 + sdn;
	vec2 n2 = normalize(p - bq + vec2(1e-4));
	float acr = rr * (n2.y < 0.0 ? 1.0 : -1.0);
	float L = bL;
	float nz = fbm(vec2(L * 2.2 - clock * 1.3, acr * 1.1 - clock * 0.2));
	float fine = vn(vec2(L * 3.5 - clock * 2.6, acr * 3.0));
	float nz2 = fbm(vec2(L * 2.0 - clock * 1.8, acr * 1.6) + 2.0);
	float flick = fbm(vec2(L * 1.7 - clock * 2.2, acr * 2.0) + 3.0);
	float torn = ((nz - 0.5) * 0.3 + (fine - 0.5) * 0.22) * bt + bd * (0.5 + nz * 1.8);
	float e = sdn + torn;
	float dens = smoothstep(0.16, -0.3, e) * (1.0 - bd * 0.6);
	// 颜色：近黑的烟；边里面断断续续透一点暗红；朝上一侧一道细的暗红边光
	float lit = 0.55 + 0.45 * smoothstep(-0.5, 0.8, -n2.y);
	float shell = smoothstep(-0.34, -0.05, e) * (1.0 - smoothstep(-0.05, 0.1, e));
	vec3 emit = vec3(0.6, 0.05, 0.07) * shell * smoothstep(0.5 - 0.3 * ember - 0.3 * flare, 0.82, flick) * 1.1 * lit * (1.0 + bd * 1.5) * (1.0 + 1.4 * ember);
	emit += vec3(0.95, 0.1, 0.12) * shell * flare * (0.6 + 0.6 * flick);
	// 外面只一层很淡的暗红晕（太亮就成了霓虹描边）
	vec3 glow = vec3(0.75, 0.04, 0.1) * exp(-max(e, 0.0) * 3.5) * step(0.0, e) * (ember * 0.08 + flare * 0.3) * ba * (1.0 - bd);
	// 体积：核心深，往边缘渐渐变成背光透过来的暗红烟；朝上的一侧整体亮一点；烟里的明暗随噪声慢慢流动
	vec3 inner = mix(vec3(0.045, 0.009, 0.014), vec3(0.13, 0.026, 0.034), smoothstep(0.35, 0.9, nz2));
	float transl = smoothstep(0.15, 1.0, rr);
	inner = mix(inner, vec3(0.3, 0.055, 0.06) * (0.8 + 0.4 * nz), transl * (twist_f > 0.0 ? 0.35 : 0.8));
	inner *= 0.75 + 0.55 * smoothstep(-0.6, 0.8, -n2.y);
	float topRim = smoothstep(0.72, 0.95, rr) * smoothstep(0.35, 0.9, -n2.y) * (1.0 - smoothstep(-0.04, 0.04, e));
	inner += vec3(0.42, 0.05, 0.06) * topRim * (twist_f > 0.0 ? 0.2 : 0.45 + 0.8 * flare);
	inner += vec3(0.5, 0.04, 0.07) * flare * transl * 0.5;
	// 绞绳纹：斜着的绳股，股中间受光、股之间的凹槽暗；勒紧的时候凹槽里透出暗红的光
	if (twist_f > 0.0) {
		float cord = 0.5 + 0.5 * sin(L * twist_f + acr * 2.4 + (nz - 0.5) * 1.2);
		float groove = pow(1.0 - cord, 2.2);
		inner *= mix(0.45, 1.3, smoothstep(0.1, 0.9, cord));
		inner += vec3(0.22, 0.05, 0.06) * pow(cord, 4.0) * lit * smoothstep(-0.6, 0.6, -n2.y);
		emit *= 0.35;                                                  // 绳子的外沿不要一圈亮红边（像霓虹描边）
		emit += vec3(0.8, 0.06, 0.1) * groove * (1.0 - smoothstep(0.35, 0.95, rr)) * (0.06 + flare * 0.55);   // 只在绳子中间的凹槽里透红，边缘保持暗（太亮像拐杖糖）
	}
	// 外面往上飘的烟丝；散的时候一大团
	float wn = fbm(vec2(L * 0.7 - clock * 0.8, rr * 0.9 + clock * 0.9 * sign(n2.y)) + 7.0);
	float wisp = smoothstep(0.5, 0.85, wn) * smoothstep(mix(1.8, 2.6, smoothstep(0.0, 0.8, -n2.y)), 0.9, rr) * 0.55 * bt;
	wisp += bd * (1.0 - bd) * 3.0 * smoothstep(0.4, 0.8, wn) * smoothstep(2.6, 0.6, rr);
	float dimBack = bz < 0.0 ? 0.8 : 1.0;
	vec3 c = (inner + emit) * dimBack;
	vec3 wc = vec3(0.09, 0.02, 0.026) + vec3(0.22, 0.03, 0.04) * smoothstep(0.72, 0.9, wn);
	float a = clamp(dens + wisp * (1.0 - dens), 0.0, 1.0) * ba;
	// 后层画到稍微进前面一点、前层从稍微进后面一点开始：交界处前层盖在后层上，不会透出缺口
	a *= side > 0.0 ? smoothstep(-0.3, 0.3, bz) : (side < 0.0 ? 1.0 - smoothstep(0.15, 0.4, bz) : 1.0);   // 绕到身后的地方渐渐隐进去，不硬切
	vec3 cc = mix(wc, c, dens / max(dens + wisp * (1.0 - dens), 1e-3));
	vec4 acc = vec4(0.0);
	over(acc, cc, a);
	acc.rgb += glow * (1.0 - acc.a * 0.4);
	acc.a = clamp(acc.a + dot(glow, vec3(0.33)) * 0.6, 0.0, 1.0);
	if (acc.a <= 0.002) discard;
	COLOR = acc;
}
" };

	private CurseForm(Control front, Control? back, double life, float unit)
	{
		_unit = unit;
		var list = new List<(Node2D, ColorRect, ShaderMaterial)>();
		foreach (var (layer, side) in new[] { (front, 1f), (back, -1f) })
		{
			if (layer == null || !GodotObject.IsInstanceValid(layer)) continue;
			Node2D root = Fx.Root(layer, Vector2.Zero, side > 0 ? "P5CurseFormF" : "P5CurseFormB", life);
			ShaderMaterial m = new() { Shader = Shade };
			m.SetShaderParameter("side", ReferenceEquals(front, back) || back == null ? 0f : side);
			ColorRect r = new() { Material = m, MouseFilter = Control.MouseFilterEnum.Ignore, Size = Vector2.One };
			root.AddChild(r);
			list.Add((root, r, m));
			if (back == null || ReferenceEquals(front, back)) break;
		}
		// 只有一层时（没有背景层）：一层画全部（side = 0 时两边的淡出都不生效）
		if (list.Count == 1) list[0].Item3.SetShaderParameter("side", 1f);
		_layers = list.ToArray();
		if (list.Count == 1) _single = true;
	}

	private readonly bool _single;

	/// <summary>建一个咒烟形状（life 秒后删掉）。unit = 身高（像素），决定外面留多少边。</summary>
	public static CurseForm Create(Control front, Control? back, double life, float unit) => new(front, back, life, unit);

	/// <summary>把这一帧的 Segs 推给着色器；dt = 这一帧多少秒（噪声流动）。</summary>
	public void Commit(float dt)
	{
		_clock += dt;
		int n = Mathf.Min(Segs.Count, Max);
		float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
		for (int i = 0; i < n; i++)
		{
			Seg s = Segs[i];
			float pad = Mathf.Max(s.Ra, s.Rb) * 2.8f + 4f;
			minX = Mathf.Min(minX, Mathf.Min(s.A.X, s.B.X) - pad);
			minY = Mathf.Min(minY, Mathf.Min(s.A.Y, s.B.Y) - pad);
			maxX = Mathf.Max(maxX, Mathf.Max(s.A.X, s.B.X) + pad);
			maxY = Mathf.Max(maxY, Mathf.Max(s.A.Y, s.B.Y) + pad);
			_sa[i] = new Vector4(s.A.X, s.A.Y, s.Ra, _single ? 1f : s.Za);
			_sb[i] = new Vector4(s.B.X, s.B.Y, s.Rb, _single ? 1f : s.Zb);
			_sx[i] = new Vector4(s.La, s.Lb, s.Alpha, s.Diss);
			_tear[i] = s.Tear;
		}
		if (n == 0)
		{
			foreach (var (root, _, _) in _layers) if (GodotObject.IsInstanceValid(root)) root.Visible = false;
			return;
		}
		Vector2 origin = new(minX, minY), size = new(Mathf.Max(2f, maxX - minX), Mathf.Max(2f, maxY - minY));
		foreach (var (root, rect, m) in _layers)
		{
			if (!GodotObject.IsInstanceValid(root)) continue;
			root.Visible = true;
			root.GlobalPosition = origin;
			rect.Position = Vector2.Zero;
			rect.Size = size;
			m.SetShaderParameter("sa", _sa);
			m.SetShaderParameter("sb", _sb);
			m.SetShaderParameter("sx", _sx);
			m.SetShaderParameter("tear", _tear);
			m.SetShaderParameter("n", n);
			m.SetShaderParameter("origin", origin);
			m.SetShaderParameter("size", size);
			m.SetShaderParameter("clock", _clock);
			m.SetShaderParameter("flare", Flare);
			m.SetShaderParameter("ember", Ember);
			m.SetShaderParameter("twist_f", TwistF);
		}
	}

	/// <summary>把一条折线（点 + 每点半径 + 深度）加进去：相邻两点一段，长度参数沿线累计（单位 = 身高 × 0.2）。</summary>
	public void AddPolyline(IReadOnlyList<Vector2> pts, IReadOnlyList<float> radii, IReadOnlyList<float> z, float alpha, float diss, float tear, float l0 = 0f)
	{
		float L = l0;
		float k = 1f / Mathf.Max(1f, _unit * 0.2f);
		for (int i = 0; i + 1 < pts.Count; i++)
		{
			float len = pts[i].DistanceTo(pts[i + 1]) * k;
			Segs.Add(new Seg
			{
				A = pts[i], B = pts[i + 1], Ra = radii[i], Rb = radii[i + 1], Za = z[i], Zb = z[i + 1],
				La = L, Lb = L + len, Alpha = alpha, Diss = diss, Tear = tear,
			});
			L += len;
		}
	}
}
