using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 光做的蛇（群蛇形态的起手 SerpentAscend 和常驻 SerpentMantle 共用）。只负责画：调用方每帧填好 Spine（40 点，相对 Anchor 的像素，
/// z = 深度（&gt; 0 画在前景层、盖住 Joker），w = 这一点的透明度；0 = 尾尖，34 = 头根，39 = 吻尖），再调 Push。
/// 画法：不画细节，只靠光——热白的芯、绯红的身体、深红外晕，身上能量往尾巴流、隐约的人字鳞光；
/// 头单独画：一圈亮的轮廓勾出上颚 / 下颌 / 后脑，里面淡淡的能量，嘴张开里面空着；两只眼睛热白 + 横向的光。加法混合。
/// </summary>
public sealed class LightSerpent
{
	public const int N = 40;
	private static Shader? _shader;

	public static Shader Shader => _shader ??= new Shader { Code = @"
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
uniform float alpha = 1.0;

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
	COLOR = vec4(col * alpha, 1.0);
}" };

	public readonly Vector4[] Spine = new Vector4[N];
	private readonly Node2D _frontRoot, _backRoot;
	private readonly ShaderMaterial _frontM, _backM;
	public float Width { get; }

	/// <summary>anchor = 坐标原点（层坐标）；rect = 画的范围（相对 anchor）；life &lt; 0 = 一直在，直到 Free。</summary>
	public LightSerpent(Control front, Control back, Vector2 anchor, float width, Rect2 rect, string name, double life)
	{
		Width = width;
		(_frontRoot, _frontM) = Make(front, anchor, rect, 1f, name + "Front", life);
		(_backRoot, _backM) = Make(back, anchor, rect, -1f, name + "Back", life);
	}

	public Node Owner => _frontRoot;
	public bool Valid => GodotObject.IsInstanceValid(_frontRoot) && GodotObject.IsInstanceValid(_backRoot);

	private (Node2D, ShaderMaterial) Make(Control layer, Vector2 anchor, Rect2 rect, float side, string name, double life)
	{
		Node2D root = new() { Name = name };
		layer.AddChild(root);
		root.GlobalPosition = anchor;
		if (life >= 0) Fx.FreeAfter(root, life);
		var m = new ShaderMaterial { Shader = Shader };
		var q = new ColorRect { Position = rect.Position, Size = rect.Size, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		m.SetShaderParameter("origin", rect.Position);
		m.SetShaderParameter("size", rect.Size);
		m.SetShaderParameter("side", side);
		m.SetShaderParameter("width", Width);
		Fx.Clock(q);
		return (root, m);
	}

	public void MoveTo(Vector2 anchor)
	{
		if (!Valid) return;
		_frontRoot.GlobalPosition = anchor;
		_backRoot.GlobalPosition = anchor;
	}

	/// <summary>把 Spine 和头的参数交给着色器。open = 张嘴角度，eyes = 眼光（0～1），boost = 整体加亮，alpha = 整体透明度。</summary>
	public void Push(float open, float eyes, float boost, float alpha = 1f, float face = 1f)
	{
		if (!Valid) return;
		Vector2 lo = new(1e9f, 1e9f), hi = new(-1e9f, -1e9f);
		foreach (Vector4 q in Spine)
		{
			if (q.W <= 0.001f) continue;
			lo = new Vector2(Mathf.Min(lo.X, q.X), Mathf.Min(lo.Y, q.Y));
			hi = new Vector2(Mathf.Max(hi.X, q.X), Mathf.Max(hi.Y, q.Y));
		}
		float m = Width * 9f;
		var box = new Vector4(lo.X - m, lo.Y - m, hi.X + m, hi.Y + m);
		Vector2 hb = new(Spine[34].X, Spine[34].Y), hp = new(Spine[N - 1].X, Spine[N - 1].Y);
		Vector2 hd = (hp - hb).LengthSquared() > 1e-3f ? (hp - hb).Normalized() : new Vector2(face, 0f);
		Vector2 hu = new(-hd.Y, hd.X);
		if (hu.Y > 0f) hu = -hu;
		foreach (ShaderMaterial mat in new[] { _frontM, _backM })
		{
			mat.SetShaderParameter("spine", Spine);
			mat.SetShaderParameter("box", box);
			mat.SetShaderParameter("head_base", hb);
			mat.SetShaderParameter("head_dir", hd);
			mat.SetShaderParameter("head_up", hu);
			mat.SetShaderParameter("head_z", Spine[34].Z);
			mat.SetShaderParameter("head_a", Spine[34].W);
			mat.SetShaderParameter("head_open", open);
			mat.SetShaderParameter("eyes", eyes);
			mat.SetShaderParameter("boost", boost);
			mat.SetShaderParameter("alpha", alpha);
		}
	}

	/// <summary>头（吻尖）和头的朝向，相对 anchor。</summary>
	public (Vector2 Tip, Vector2 Dir) Head()
	{
		Vector2 hb = new(Spine[34].X, Spine[34].Y), hp = new(Spine[N - 1].X, Spine[N - 1].Y);
		return (hp, (hp - hb).LengthSquared() > 1e-3f ? (hp - hb).Normalized() : Vector2.Right);
	}

	public void Free()
	{
		if (GodotObject.IsInstanceValid(_frontRoot)) _frontRoot.QueueFree();
		if (GodotObject.IsInstanceValid(_backRoot)) _backRoot.QueueFree();
	}
}
