using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 牌堆入场的色块合成（用户 2026-11-24：入场里很多不规则图形和瑕疵，原版截图直接抠出来的区域照搬不了 → 自己设计干净的几何色块）。
/// 一个整屏着色器一次画完：
/// · 色块 = 多边形（C# 每帧按动画参数生成：直边、贝塞尔弧、两条曲线夹出来的笔触），着色器算到多边形的有符号距离，边缘解析抗锯齿（不靠 Polygon2D）；
/// · 层：按添加顺序从下往上叠（P5 的色域碰撞：星纹 / 红底 / 灰板 / 黑区），每层可以带一条贴着外边的描边（运动中的前沿）；
/// · 3D Joker（MenuJoker3D 的渲染）直接采样进来：黑区里反色（黑 Joker → 白 / 浅灰，P 系列的撞色，颜色按页面的立绘配：SetInvert），其余区域照常；
/// · 第 0 层是「盖子」：盖子外面透明（露出游戏画面），它的前沿描边画在盖子外面。
/// 星纹和红底用 PileDraw.StarShader 同一套星星数据和参数，和最终排版的红星纹底一模一样（切到最终排版时不跳）。
/// </summary>
public sealed class PileField
{
	/// <summary>Dark = 黑区（Joker 反色）、Red = 红星纹、Star = 黑白星纹、Gray = 浅灰板、Solid = 纯色（用这一层的描边颜色填满，比如前沿的红条）。</summary>
	public enum Kind { Dark = 0, Red = 1, Star = 2, Gray = 3, Solid = 4 }

	private const int MaxPts = 224, MaxEntries = 10;
	private readonly ColorRect _rect;
	private readonly ShaderMaterial _mat;
	private readonly Vector2[] _pts = new Vector2[MaxPts];
	private readonly Vector4[] _info = new Vector4[MaxEntries];
	private readonly Color[] _rim = new Color[MaxEntries];
	private readonly float _k;
	private int _used, _entries;

	/// <summary>parent：加到哪（入场层）；view：屏幕大小；baseKind：盖子里面最底下那层是什么。</summary>
	public PileField(Node parent, Vector2 view, Kind baseKind)
	{
		_k = view.Y / 1080f;
		_mat = new ShaderMaterial { Shader = Shader };
		ShaderMaterial red = PileDraw.StarMaterial(160f, new Color(0.466f, 0.034f, 0.06f), new Color(0.27f, 0.02f, 0.035f), new Color(0.27f, 0.02f, 0.035f), new Vector2(0.32f, 0.22f));
		_mat.SetShaderParameter("sdata", red.GetShaderParameter("sdata"));
		_mat.SetShaderParameter("size", view);
		_mat.SetShaderParameter("base_kind", (int)baseKind);
		_mat.SetShaderParameter("red_paper", new Color(0.466f, 0.034f, 0.06f));
		_mat.SetShaderParameter("red_ink", new Color(0.27f, 0.02f, 0.035f));
		_mat.SetShaderParameter("star_paper", new Color(0.91f, 0.91f, 0.91f));
		_mat.SetShaderParameter("star_ink", new Color(0.03f, 0.03f, 0.03f));
		_mat.SetShaderParameter("star_gray", new Color(0.376f, 0.376f, 0.376f));
		_mat.SetShaderParameter("gray_col", new Color(0.84f, 0.84f, 0.84f));
		SetInvert(new Color(8 / 255f, 8 / 255f, 8 / 255f), Colors.White, new Color(8 / 255f, 8 / 255f, 8 / 255f));
		_rect = new ColorRect { Name = "PileField", Size = view, Material = _mat, MouseFilter = Control.MouseFilterEnum.Ignore };
		parent.AddChild(_rect);
	}

	public bool Visible { get => _rect.Visible; set => _rect.Visible = value; }

	/// <summary>3D Joker 的渲染（屏幕大小、屏幕坐标对齐）；null = 不画 Joker。</summary>
	public void SetJoker(Texture2D? tex)
	{
		_mat.SetShaderParameter("joker_tex", tex);
		_mat.SetShaderParameter("joker_on", tex != null ? 1f : 0f);
	}

	/// <summary>
	/// 黑区的颜色（和这一页的 2D 立绘一致，切过去不跳）：bg = 没有 Joker 的地方；Joker 按亮度 c 反色成 hi − c·(hi − lo)（黑 → hi，白 → lo）。
	/// 默认是牌组页（黑块 8、Joker 纯白）；弃牌堆页的立绘在黑块里是浅灰 222（mock_discard.py）。
	/// </summary>
	public void SetInvert(Color bg, Color hi, Color lo)
	{
		_mat.SetShaderParameter("dark_bg", bg);
		_mat.SetShaderParameter("inv_hi", hi);
		_mat.SetShaderParameter("inv_lo", lo);
	}

	/// <summary>红星纹的转动时间（跟最终排版的红底用同一个时间，切过去的时候星星不跳）。</summary>
	public void SetTime(float t) => _mat.SetShaderParameter("time", t);

	/// <summary>开始新的一帧：盖子多边形（1080p 排版坐标）+ 它前沿的描边宽度（px@1080p）和颜色。</summary>
	public void Begin(IReadOnlyList<Vector2> cover, float rim = 0f, Color? rimColor = null)
	{
		_used = 0;
		_entries = 0;
		Add(cover, Kind.Dark, rim, rimColor);
	}

	/// <summary>加一层（1080p 排版坐标；少于 3 个点就跳过）。rim：贴着外边的描边宽度（px@1080p，0 = 不描）。</summary>
	public void Add(IReadOnlyList<Vector2>? poly, Kind kind, float rim = 0f, Color? rimColor = null)
	{
		if (poly == null || poly.Count < 3 || _entries >= MaxEntries || _used + poly.Count > MaxPts) return;
		int start = _used;
		for (int i = 0; i < poly.Count; i++) _pts[_used++] = poly[i] * _k;
		_info[_entries] = new Vector4(start, poly.Count, (int)kind, Mathf.Max(0f, rim) * _k);
		_rim[_entries] = rimColor ?? new Color(0.86f, 0.06f, 0.1f);
		_entries++;
	}

	/// <summary>把这一帧的多边形交给着色器。</summary>
	public void Commit()
	{
		_mat.SetShaderParameter("pts", _pts);
		_mat.SetShaderParameter("info", _info);
		_mat.SetShaderParameter("rimc", _rim);
		_mat.SetShaderParameter("n_info", _entries);
	}

	private static Shader? _shader;
	internal static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode unshaded;
uniform vec2 size = vec2(1920.0, 1080.0);
uniform vec2 pts[224];
uniform vec4 info[10];       // 起点、点数、种类（0 黑 1 红星纹 2 星纹 3 灰 4 纯色）、描边宽（px）
uniform vec4 rimc[10];
uniform int n_info = 0;
uniform int base_kind = 0;
uniform sampler2D joker_tex : filter_linear, repeat_disable;
uniform float joker_on = 0.0;
uniform float sdata[245];
uniform float time = 0.0;
// 同 PileDraw.StarMaterial 的参数（红底 = 160 / 红；星纹 = 118 / 白黑灰），传法也一样（source_color），颜色和最终排版完全一致
uniform vec4 red_paper : source_color = vec4(0.466, 0.034, 0.06, 1.0);
uniform vec4 red_ink : source_color = vec4(0.27, 0.02, 0.035, 1.0);
uniform vec4 star_paper : source_color = vec4(0.91, 0.91, 0.91, 1.0);
uniform vec4 star_ink : source_color = vec4(0.03, 0.03, 0.03, 1.0);
uniform vec4 star_gray : source_color = vec4(0.376, 0.376, 0.376, 1.0);
uniform vec4 gray_col : source_color = vec4(0.84, 0.84, 0.84, 1.0);
uniform vec4 dark_bg : source_color = vec4(0.031, 0.031, 0.031, 1.0);
uniform vec4 inv_hi : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform vec4 inv_lo : source_color = vec4(0.031, 0.031, 0.031, 1.0);

float sd_poly(vec2 p, int s, int n) {
	vec2 v0 = pts[s];
	float d = dot(p - v0, p - v0);
	float sg = 1.0;
	for (int k = 0; k < 64; k++) {
		if (k >= n) break;
		vec2 vi = pts[s + k];
		vec2 vj = pts[s + (k == 0 ? n - 1 : k - 1)];
		vec2 e = vj - vi, w = p - vi;
		vec2 b = w - e * clamp(dot(w, e) / max(dot(e, e), 1e-6), 0.0, 1.0);
		d = min(d, dot(b, b));
		bvec3 c = bvec3(p.y >= vi.y, p.y < vj.y, e.x * w.y > e.y * w.x);
		if (all(c) || all(not(c))) sg = -sg;
	}
	return sg * sqrt(d);
}

// 同 PileDraw.StarShader（同心五角星，黑白交替，cover 方式铺满）
vec3 star_col(vec2 px, float base_size, vec3 pp, vec3 ik, vec3 gk, vec2 shade, float tm) {
	float kk = max(size.x / 1280.0, size.y / 720.0);
	vec2 off = (size - vec2(1280.0, 720.0) * kk) * 0.5;
	vec2 p0 = (px - off) / kk;
	const float SEG = 0.6283185;
	const float rings = 8.0;
	vec2 p2 = vec2(sin(SEG), cos(SEG)) * 0.46;
	vec2 nn = normalize(vec2(p2.y - 1.0, -p2.x));
	float c0 = nn.y;
	float grad = length(nn) / abs(c0);
	vec3 col = pp;
	for (int i = 0; i < 49; i++) {
		float s = sdata[i * 5 + 2] * base_size;
		vec2 d = (p0 - vec2(sdata[i * 5], sdata[i * 5 + 1])) / s;
		float r = length(d);
		if (r > 1.02) continue;
		float sgn = (i % 2 == 0) ? 1.0 : -1.0;
		float th = radians(sdata[i * 5 + 3] + tm * 1.2 * sgn);
		vec2 q = vec2(d.x * cos(th) + d.y * sin(th), -d.x * sin(th) + d.y * cos(th));
		float a = atan(q.x, -q.y);
		float t = abs(mod(a, 2.0 * SEG) - SEG);
		float dist = (r * sin(t) * nn.x + r * cos(t) * nn.y) / c0;
		if (dist >= 1.0 + 4.0 / (kk * s)) continue;
		float w = grad / (kk * s);
		float cov = clamp((1.0 - dist) / w + 0.5, 0.0, 1.0);
		float sr = dist * rings;
		float phase = mod(rings - 1.0, 2.0);
		float f = fract((sr - phase) * 0.5);
		float h = w * rings * 0.25 + 1e-4;
		float inkAmt = smoothstep(-h, h, f) - smoothstep(0.5 - h, 0.5 + h, f) + smoothstep(1.0 - h, 1.0 + h, f);
		vec3 ic = sdata[i * 5 + 4] > 0.5 ? gk : ik;
		col = mix(col, mix(pp, ic, clamp(inkAmt, 0.0, 1.0)), cov);
	}
	vec2 uvs = px / size;
	return col * (1.0 - shade.x * uvs.x - shade.y * uvs.y);
}

vec3 kind_col(int kind, vec2 px) {
	if (kind == 1) return star_col(px, 160.0, red_paper.rgb, red_ink.rgb, red_ink.rgb, vec2(0.32, 0.22), time);
	if (kind == 2) return star_col(px, 118.0, star_paper.rgb, star_ink.rgb, star_gray.rgb, vec2(0.0), 0.0);
	return gray_col.rgb;
}

void fragment() {
	vec2 px = UV * size;
	float dc = sd_poly(px, int(info[0].x), int(info[0].y));
	float ac = clamp(0.5 - dc, 0.0, 1.0);
	float rc = info[0].w > 0.0 ? clamp(0.5 + dc, 0.0, 1.0) - clamp(0.5 + dc - info[0].w, 0.0, 1.0) : 0.0;
	if (ac <= 0.0 && rc <= 0.0) discard;

	vec3 col = vec3(0.0);
	if (ac > 0.0) {
		float A[10]; float R[10];
		for (int i = 1; i < 10; i++) {
			A[i] = 0.0; R[i] = 0.0;
			if (i >= n_info) continue;
			float d = sd_poly(px, int(info[i].x), int(info[i].y));
			A[i] = clamp(0.5 - d, 0.0, 1.0);
			if (info[i].w > 0.0) R[i] = clamp(0.5 + d, 0.0, 1.0) - clamp(0.5 + d - info[i].w, 0.0, 1.0);
		}
		// 从上往下叠：Lp = 亮色部分（预乘），dk = 黑区（Joker 反色）的权重
		vec3 Lp = vec3(0.0); float dk = 0.0; float T = 1.0;
		for (int i = 9; i >= 1; i--) {
			if (i >= n_info || T <= 0.0) continue;
			if (R[i] > 0.0) { Lp += T * R[i] * rimc[i].rgb; T *= 1.0 - R[i]; }
			if (A[i] > 0.0 && T > 0.0) {
				int kind = int(info[i].z);
				float w = T * A[i];
				if (kind == 0) dk += w; else if (kind == 4) Lp += w * rimc[i].rgb; else Lp += w * kind_col(kind, px);
				T *= 1.0 - A[i];
			}
		}
		if (T > 0.0) { if (base_kind == 0) dk += T; else Lp += T * kind_col(base_kind, px); }
		vec4 j = joker_on > 0.5 ? texture(joker_tex, UV) : vec4(0.0);
		vec3 dcol = mix(dark_bg.rgb, inv_hi.rgb - j.rgb * (inv_hi.rgb - inv_lo.rgb), j.a);
		col = Lp * (1.0 - j.a) + (1.0 - dk) * j.rgb * j.a + dk * dcol;
	}
	float alpha = clamp(ac + rc, 0.0, 1.0);
	col = (col * ac + rimc[0].rgb * rc) / max(alpha, 1e-4);
	COLOR = vec4(col, alpha);
}
" };
}

/// <summary>入场色块的几何：贝塞尔曲线、半平面、笔触、多边形求交；缓动。坐标都是 1080p 排版坐标。</summary>
internal static class PileShapes
{
	public const float Far = 6000f;

	public static float Seg(float t, float a, float len) => Mathf.Clamp((t - a) / len, 0f, 1f);
	public static float OutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
	public static float OutQuint(float x) => 1f - Mathf.Pow(1f - x, 5f);
	public static float InQuad(float x) => x * x;
	public static float InOutCubic(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;
	/// <summary>带一点回弹的缓出（s = 回弹量，1.2 ≈ 3%）。</summary>
	public static float OutBack(float x, float s = 1.2f) { float c = s + 1f; float y = x - 1f; return 1f + c * y * y * y + s * y * y; }

	/// <summary>二次贝塞尔（a → b，中点往 a→b 的左法线方向鼓 bulge 像素）上的 n 个点。</summary>
	public static Vector2[] Curve(Vector2 a, Vector2 b, float bulge, int n = 18)
	{
		Vector2 d = b - a;
		Vector2 nrm = new Vector2(d.Y, -d.X).Normalized();
		Vector2 c = (a + b) * 0.5f + nrm * (2f * bulge);
		var o = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			float t = i / (float)(n - 1);
			o[i] = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
		}
		return o;
	}

	/// <summary>
	/// 一条（可以带弧度的）边一侧的整片区域：边从 a 到 b（两端放在画面外），区域在 a→b 方向的 (−d.y, d.x) 那一侧
	///（屏幕坐标 y 向下：a 在上、b 在下时是边的左边）。bulge：边的中点往 a→b 的 (d.y, −d.x) 方向鼓多少像素。
	/// </summary>
	public static Vector2[] Side(Vector2 a, Vector2 b, float bulge = 0f, int n = 18)
	{
		Vector2[] edge = Mathf.Abs(bulge) < 0.5f ? new[] { a, b } : Curve(a, b, bulge, n);
		Vector2 d = (b - a).Normalized();
		Vector2 side = new(-d.Y, d.X);
		// 边沿自己的方向往两头再延长 Far（两端在画面外，延长段也在画面外），再往 side 推 Far：斜边也能把整片半平面盖满
		Vector2 a0 = a - d * Far, b0 = b + d * Far;
		var o = new Vector2[edge.Length + 4];
		o[0] = a0;
		for (int i = 0; i < edge.Length; i++) o[i + 1] = edge[i];
		o[edge.Length + 1] = b0;
		o[edge.Length + 2] = b0 + side * Far;
		o[edge.Length + 3] = a0 + side * Far;
		return o;
	}

	/// <summary>一条从上（top）到下（bottom）的边左边的整片区域；bulge &gt; 0 = 边往右鼓（区域是凸的）。</summary>
	public static Vector2[] LeftOf(Vector2 top, Vector2 bottom, float bulge = 0f) => Side(top, bottom, bulge);

	/// <summary>一条从上（top）到下（bottom）的边右边的整片区域；bulge &gt; 0 = 边往左鼓（区域是凸的）。</summary>
	public static Vector2[] RightOf(Vector2 top, Vector2 bottom, float bulge = 0f) => Side(bottom, top, bulge);

	/// <summary>沿一条边（a → b，同 Side 的鼓法）外侧的一道描边：宽 w，在 Side 区域的对面。</summary>
	public static Vector2[]? EdgeStripe(Vector2 a, Vector2 b, float bulge, float w, int n = 18)
	{
		if (w < 0.5f) return null;
		Vector2[] edge = Curve(a, b, bulge, n);
		Vector2 d = (b - a).Normalized();
		Vector2 off = new Vector2(d.Y, -d.X) * w;
		var o = new Vector2[n * 2];
		for (int i = 0; i < n; i++) { o[i] = edge[i]; o[n * 2 - 1 - i] = edge[i] + off; }
		return o;
	}

	/// <summary>笔触：沿贝塞尔中心线从 s0 到 s1（0..1）那一段，半宽 half（两头削尖 taper 那么长的比例）。</summary>
	public static Vector2[]? Stroke(Vector2 a, Vector2 c, Vector2 b, float s0, float s1, float half, float taper = 0.12f, int n = 20)
	{
		if (s1 - s0 < 1e-3f) return null;
		var left = new Vector2[n];
		var right = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			float t = Mathf.Lerp(s0, s1, i / (float)(n - 1));
			Vector2 p = (1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * b;
			Vector2 tan = (2 * (1 - t) * (c - a) + 2 * t * (b - c)).Normalized();
			Vector2 nrm = new(tan.Y, -tan.X);
			float u = i / (float)(n - 1);
			float w = half * Mathf.Min(1f, Mathf.Min(u, 1f - u) / taper * 0.7f + 0.3f);
			left[i] = p + nrm * w;
			right[i] = p - nrm * w;
		}
		var o = new Vector2[n * 2];
		for (int i = 0; i < n; i++) { o[i] = left[i]; o[n * 2 - 1 - i] = right[i]; }
		return o;
	}

	public static Vector2[] Shift(Vector2[] poly, Vector2 by)
	{
		var o = new Vector2[poly.Length];
		for (int i = 0; i < poly.Length; i++) o[i] = poly[i] + by;
		return o;
	}

	public static Vector2[] Lerp(Vector2[] a, Vector2[] b, float t)
	{
		var o = new Vector2[a.Length];
		for (int i = 0; i < a.Length; i++) o[i] = a[i].Lerp(b[i], t);
		return o;
	}

	/// <summary>两个多边形的交集里面积最大的那块（没有交集返回 null）。</summary>
	public static Vector2[]? Intersect(Vector2[] a, Vector2[] b)
	{
		var parts = Geometry2D.IntersectPolygons(a, b);
		Vector2[]? best = null;
		float area = 0f;
		foreach (Vector2[] p in parts)
		{
			float s = 0f;
			for (int i = 0, j = p.Length - 1; i < p.Length; j = i++) s += (p[j].X + p[i].X) * (p[j].Y - p[i].Y);
			s = Mathf.Abs(s);
			if (s > area) { area = s; best = p; }
		}
		return best;
	}
}
