using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 「怪盗风」的特效积木（2026-11-12，技能牌重做第三版；用户：按你的审美来，好看、帅、细节足够）。
/// 红 / 黑 / 白高对比，靠运动和构图出效果：
///  · P5Card：一张做工精细的预告信卡（着色器：牌背红底黑斜纹 + 网点，牌面暗底红纹 + 白细边，翻转时一道高光扫过，边缘抗锯齿），能被一刀切开；
///  · Ribbon：牌 / 刀光后面的拖尾（多边形，顶点渐变：尾巴透明、收尖 → 头部白芯）；
///  · Band：身后一道一闪而过的红色斜切色带，带网点和黑边（P5 菜单那种），从一头扫进、从另一头扫出；
///  · Shards：沿速度方向拉长的细碎火花；Confetti：红黑白纸屑，翻着落。
/// </summary>
public static class Phantom
{
	public static readonly Color Red = new(0.92f, 0.06f, 0.12f), Hot = new(1f, 0.97f, 0.95f), Ink = new(0.04f, 0.02f, 0.03f);

	// ═══════════════════════════ 卡 ═══════════════════════════

	private static Shader? _card;

	private static Shader CardShader => _card ??= new Shader { Code = @"
shader_type canvas_item;
uniform float face = 0.0;        // 0 牌背，1 牌面
uniform float sheen = -1.0;      // 高光带的位置（-1 ～ 2 扫过去）
uniform float alpha = 1.0;
uniform float glow = 0.0;        // 牌整体泛白（定格时亮一下）
uniform vec3 clip = vec3(0.0, 0.0, -9.0);   // 切开：只留 dot(p, clip.xy) > clip.z 的一半（p 是牌内坐标 -1～1）
uniform float burn = 0.0;        // 从切口往外烧掉
uniform float seed = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y); }
float box(vec2 p, vec2 b, float r) { vec2 q = abs(p) - b + r; return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r; }
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;                       // -1～1，牌占满（比例由节点尺寸给：5 : 7）
	vec2 px = fwidth(p);
	float aa = max(px.x, px.y) * 1.2;
	float d = box(p, vec2(0.96, 0.97), 0.12);
	float inside = 1.0 - smoothstep(-aa, aa, d);
	vec3 col;
	if (face < 0.5) {
		// 牌背：深红底，斜着的黑色粗纹，纹里一层网点，中间一个菱形开窗露出更亮的红
		float diag = p.x * 0.7 + p.y;
		float stripe = smoothstep(-aa * 3.0, aa * 3.0, sin(diag * 9.0));
		vec2 hp = p * 18.0; hp = vec2(hp.x - hp.y, hp.x + hp.y) * 0.707;
		float dots = smoothstep(0.38, 0.32, length(fract(hp) - 0.5)) * (0.6 + 0.4 * stripe);
		col = mix(vec3(0.42, 0.02, 0.05), vec3(0.78, 0.04, 0.09), 0.5 + 0.5 * p.y);
		col = mix(col, vec3(0.06, 0.01, 0.02), stripe * 0.75);
		col = mix(col, vec3(0.95, 0.15, 0.2), dots * (1.0 - stripe) * 0.35);
		float dia = abs(p.x) * 1.35 + abs(p.y);
		float win = 1.0 - smoothstep(0.42 - aa * 2.0, 0.42 + aa * 2.0, dia);
		float winEdge = 1.0 - smoothstep(0.0, aa * 3.0, abs(dia - 0.42));
		col = mix(col, vec3(0.95, 0.12, 0.16), win * 0.9);
		col = mix(col, vec3(1.0), winEdge * 0.9);
	} else {
		// 牌面：近黑到暗红的斜向渐变，一圈红边、里面一道白细框，中间红色的利爪纹（三道斜切）
		col = mix(vec3(0.03, 0.01, 0.015), vec3(0.32, 0.02, 0.05), clamp(0.5 - 0.5 * (p.x + p.y), 0.0, 1.0));
		float slash = 0.0;
		for (int i = 0; i < 3; i++) {
			float o = (float(i) - 1.0) * 0.3;
			float l = abs(p.x * 0.6 - p.y * 0.8 + o) - 0.045 * (1.0 - abs(p.y) * 0.6);
			slash = max(slash, (1.0 - smoothstep(-aa, aa, l)) * step(abs(p.y), 0.62));
		}
		col = mix(col, vec3(0.95, 0.08, 0.13), slash);
	}
	float rimR = 1.0 - smoothstep(-aa, aa, abs(d + 0.06) - 0.05);   // 红边
	float rimW = 1.0 - smoothstep(-aa, aa, abs(d + 0.17) - 0.012);  // 白细框
	col = mix(col, vec3(0.9, 0.05, 0.1), rimR * face);
	col = mix(col, vec3(1.0), rimW);
	col = mix(col, vec3(0.02), (1.0 - smoothstep(-aa, aa, abs(d) - 0.02)) * (1.0 - face) * 0.8);
	// 翻转时扫过的高光（斜向窄带 + 一圈宽的柔光）
	float band = p.x * 0.5 + p.y * 0.86 - (sheen * 2.0 - 1.0) * 1.6;
	col += vec3(1.0, 0.95, 0.92) * (exp(-band * band * 60.0) * 0.8 + exp(-band * band * 4.0) * 0.12);
	col = mix(col, vec3(1.0, 0.92, 0.9), glow * 0.6);
	// 切开 + 从切口烧掉
	float keep = 1.0;
	if (clip.z > -8.0) {
		float s = dot(p, clip.xy) - clip.z;
		float n = noise(p * 6.0 + seed) * 0.35 + noise(p * 15.0 + seed) * 0.15;
		float edge = s - burn * 2.2 + n * min(1.0, burn * 4.0);
		keep = smoothstep(-aa, aa, s) * smoothstep(0.0, 0.04, edge);
		float hot = exp(-max(edge, 0.0) / 0.05) * step(0.0, edge) * step(0.001, burn);
		col = mix(col, vec3(1.0, 0.5, 0.35), hot * 0.9);
		col += vec3(1.0, 0.9, 0.85) * exp(-abs(s) / 0.03) * (1.0 - min(1.0, burn * 3.0)) * 1.2;   // 刚切开时切口一道白
	}
	COLOR = vec4(col, inside * keep * alpha);
}" };

	public sealed class P5Card
	{
		public Node2D Root = null!;
		public ColorRect Rect = null!;
		public Ribbon? Trail;
		private readonly ShaderMaterial _m;
		public P5Card(ShaderMaterial m) { _m = m; }
		public void Set(string n, Variant v) => _m.SetShaderParameter(n, v);
		/// <summary>位置、倾斜、绕竖轴翻转（横向缩放 = cos，过 90° 换面）、整体缩放。</summary>
		public void Place(Vector2 pos, float tilt, float flip, float scale = 1f)
		{
			Root.GlobalPosition = pos;
			Root.Rotation = tilt;
			float c = Mathf.Cos(flip);
			Root.Scale = new Vector2(Mathf.Max(0.04f, Mathf.Abs(c)), 1f) * scale;
			_m.SetShaderParameter("face", c >= 0f ? 1f : 0f);
			Trail?.Push(pos);
		}
	}

	/// <summary>一张卡（w = 牌宽像素），life 秒后删掉。trail = 带拖尾（宽 w × trail）。</summary>
	public static P5Card Card(Control layer, Vector2 at, float w, double life, float trail = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5Card", life);
		ShaderMaterial m = new() { Shader = CardShader };
		m.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		Vector2 size = new(w, w * 1.4f);
		ColorRect r = new() { Size = size, Position = -size / 2f, PivotOffset = size / 2f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		var c = new P5Card(m) { Root = root, Rect = r };
		if (trail > 0f)
		{
			c.Trail = new Ribbon(layer, w * trail, life, 9);
		}
		root.AddChild(r);
		return c;
	}

	// ═══════════════════════════ 拖尾 ═══════════════════════════

	/// <summary>
	/// 拖尾：记最近 n 个位置，画成尾尖头宽的带子；两层——外面一层红（半透明），里面一层白芯；顶点颜色从尾巴透明渐到头部。
	/// </summary>
	public sealed class Ribbon
	{
		private readonly Polygon2D _outer, _core;
		private readonly List<Vector2> _pts = new();
		private readonly float _w;
		private readonly int _n;
		private readonly Node2D _root;
		public float Fade = 1f;

		/// <summary>牌停下 / 消失时：拖尾在 0.12s 里收掉（不留一截冻住的带子）。</summary>
		public void Kill()
		{
			if (!GodotObject.IsInstanceValid(_root)) return;
			Fx.Animate(_root, 0, 0.12, t => _root.Modulate = new Color(1f, 1f, 1f, 1f - t));
		}

		/// <summary>清掉记下的点（拖尾从下一次 Push 重新开始，不会连回旧位置）。</summary>
		public void Reset()
		{
			_pts.Clear();
			if (!GodotObject.IsInstanceValid(_root)) return;
			_outer.Polygon = new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero };
			_outer.VertexColors = new[] { Colors.Transparent, Colors.Transparent, Colors.Transparent };
			_core.Polygon = new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero };
			_core.VertexColors = new[] { Colors.Transparent, Colors.Transparent, Colors.Transparent };
		}

		private readonly bool _ink;

		/// <summary>ink = 墨色拖尾（不叠加发光：外层近黑、芯是暗红），给暗影类的牌用。</summary>
		public Ribbon(Control layer, float width, double life, int n = 10, bool ink = false)
		{
			_ink = ink;
			_w = width;
			_n = n;
			Node2D root = new() { Name = "P5Ribbon", TopLevel = true };
			_root = root;
			layer.AddChild(root);
			root.GlobalPosition = Vector2.Zero;
			Fx.FreeAfter(root, life);
			_outer = Fx.Poly(root, new[] { Vector2.Zero, Vector2.Right, Vector2.Down }, Colors.White, additive: !ink);
			_core = Fx.Poly(root, new[] { Vector2.Zero, Vector2.Right, Vector2.Down }, Colors.White, additive: !ink);
		}

		public void Push(Vector2 p)
		{
			if (_pts.Count > 0 && _pts[^1].DistanceSquaredTo(p) < 0.25f) return;
			_pts.Add(p);
			while (_pts.Count > _n) _pts.RemoveAt(0);
			if (_pts.Count < 3) return;
			if (_ink)
			{
				Draw(_outer, _w, new Color(0.03f, 0.01f, 0.02f, 0f), new Color(0.04f, 0.01f, 0.02f, 0.85f * Fade));
				Draw(_core, _w * 0.3f, new Color(0.4f, 0.02f, 0.05f, 0f), new Color(0.62f, 0.03f, 0.08f, 0.9f * Fade));
				return;
			}
			Draw(_outer, _w, new Color(Red, 0f), new Color(1f, 0.25f, 0.3f, 0.75f * Fade));
			Draw(_core, _w * 0.32f, new Color(1f, 0.6f, 0.6f, 0f), new Color(Hot, 0.95f * Fade));
		}

		private void Draw(Polygon2D poly, float w, Color tail, Color head)
		{
			int n = _pts.Count;
			var outPts = new Vector2[n * 2];
			var cols = new Color[n * 2];
			for (int i = 0; i < n; i++)
			{
				float f = (float)i / (n - 1);
				Vector2 d = (_pts[Mathf.Min(i + 1, n - 1)] - _pts[Mathf.Max(i - 1, 0)]).Normalized();
				Vector2 nn = new Vector2(-d.Y, d.X) * w * 0.5f * Mathf.Pow(f, 1.4f);
				outPts[i] = _pts[i] + nn;
				outPts[n * 2 - 1 - i] = _pts[i] - nn;
				Color c = tail.Lerp(head, f * f);
				cols[i] = c;
				cols[n * 2 - 1 - i] = c;
			}
			poly.Polygon = outPts;
			poly.VertexColors = cols;
		}
	}

	// ═══════════════════════════ 斜切色带 ═══════════════════════════

	private static Shader? _band;

	private static Shader BandShader => _band ??= new Shader { Code = @"
shader_type canvas_item;
uniform float head = 0.0;    // 色带前沿（沿长度 0～1）
uniform float tail = 0.0;    // 色带尾端
uniform float alpha = 1.0;
void fragment() {
	vec2 uv = UV;
	float aa = fwidth(uv.x) * 1.5;
	float x = uv.x + (uv.y - 0.5) * 0.18;                              // 两端斜切
	float body = smoothstep(tail - aa, tail + aa, x) * (1.0 - smoothstep(head - aa, head + aa, x));
	float edgeY = min(uv.y, 1.0 - uv.y);
	float ink = 1.0 - smoothstep(0.06, 0.06 + fwidth(uv.y) * 1.5, edgeY);          // 上下两道黑边
	vec3 col = mix(vec3(0.95, 0.08, 0.14), vec3(0.62, 0.02, 0.07), uv.y);
	// 网点：往前沿越密
	vec2 g = vec2(uv.x * 46.0, uv.y * 10.0);
	float r = 0.18 + 0.25 * smoothstep(tail, head, x);
	float dots = 1.0 - smoothstep(r - 0.06, r, length(fract(g) - 0.5));
	col = mix(col, vec3(1.0, 0.45, 0.45), dots * 0.35);
	col = mix(col, vec3(0.03, 0.01, 0.02), ink);
	// 前沿一道白
	col = mix(col, vec3(1.0), exp(-abs(x - head) / 0.01) * 0.9);
	COLOR = vec4(col, body * alpha);
}" };

	/// <summary>
	/// 一道斜切色带（中心 at、长 len、厚 thick、角度 ang）：前沿 0.12s 扫过去，停一下，尾端 0.15s 追上去收掉。
	/// </summary>
	public static void Band(Control layer, Vector2 at, float len, float thick, float ang, float delay = 0f, float hold = 0.08f, float alpha = 0.85f, bool ink = false)
	{
		Node2D root = Fx.Root(layer, at, "P5Band", delay + 0.4 + hold);
		root.Rotation = ang;
		ShaderMaterial m = new() { Shader = BandShader };
		ColorRect r = new() { Size = new Vector2(len, thick), Position = new Vector2(-len / 2f, -thick / 2f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(r);
		m.SetShaderParameter("alpha", alpha);
		if (ink) r.Modulate = new Color(0.08f, 0.04f, 0.05f);
		m.SetShaderParameter("head", 0f);
		m.SetShaderParameter("tail", 0f);
		float total = 0.12f + hold + 0.16f;
		Fx.Animate(r, delay, total, t =>
		{
			float s = t * total;
			m.SetShaderParameter("head", Ease5.OutExpo(Mathf.Min(1f, s / 0.12f)) * 1.1f);
			m.SetShaderParameter("tail", Fx.InCubic(Mathf.Clamp((s - 0.12f - hold) / 0.16f, 0f, 1f)) * 1.15f - 0.05f);
		});
	}

	// ═══════════════════════════ 火花 / 纸屑 ═══════════════════════════

	/// <summary>沿速度方向拉长的细碎火花：n 根，从 at 往 dir ± spread（度）飞，重力 g，life 秒。白芯红晕，越飞越短。</summary>
	public static void Shards(Control layer, Vector2 at, int n, float speedMin, float speedMax, Vector2 dir, float spread, float life, float g, float len, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5Shards", delay + life + 0.1);
		var parts = new List<(Polygon2D Glow, Polygon2D Core, Vector2 V)>();
		for (int i = 0; i < n; i++)
		{
			float a = dir.Angle() + Mathf.DegToRad(Fx.Rand(-spread, spread));
			Vector2 v = Vector2.Right.Rotated(a) * Fx.Rand(speedMin, speedMax);
			Polygon2D glow = Fx.Poly(root, new[] { new Vector2(0f, -1f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0f) }, new Color(1f, 0.2f, 0.25f, 0.8f), additive: true);
			Polygon2D core = Fx.Poly(root, new[] { new Vector2(0f, -1f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-1f, 0f) }, Hot, additive: true);
			parts.Add((glow, core, v));
		}
		root.Visible = false;
		Fx.Animate(root, delay, life, t =>
		{
			root.Visible = true;
			float s = t * life;
			for (int i = 0; i < parts.Count; i++)
			{
				var (glow, core, v0) = parts[i];
				float drag = Mathf.Exp(-s * 3f);
				Vector2 vel = v0 * drag + new Vector2(0f, g * s);
				Vector2 pos = v0 * (1f - drag) / 3f + new Vector2(0f, 0.5f * g * s * s);
				float l = len * (1f - t) * Mathf.Clamp(vel.Length() / 400f, 0.25f, 1.6f);
				glow.Position = core.Position = pos;
				glow.Rotation = core.Rotation = vel.Angle();
				glow.Scale = new Vector2(l, len * 0.22f);
				core.Scale = new Vector2(l * 0.8f, len * 0.08f);
			}
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.6f, 0.4f));
		});
	}

	/// <summary>红黑白纸屑：n 片从 at 往 dir 迸开、翻着飘落，life 秒。</summary>
	public static void Confetti(Control layer, Vector2 at, int n, float speed, Vector2 dir, float spread, float life, float size, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5Confetti", delay + life + 0.1);
		Color[] cols = { Red, Ink, Hot, new(0.6f, 0.02f, 0.06f) };
		var parts = new List<(Polygon2D P, Vector2 V, float Spin, float Ph)>();
		for (int i = 0; i < n; i++)
		{
			float a = dir.Angle() + Mathf.DegToRad(Fx.Rand(-spread, spread));
			Vector2 v = Vector2.Right.Rotated(a) * speed * Fx.Rand(0.4f, 1f);
			float w = size * Fx.Rand(0.6f, 1.2f), h = w * Fx.Rand(0.4f, 0.8f);
			Polygon2D p = Fx.Poly(root, new[] { new Vector2(-w, -h), new Vector2(w, -h * 0.7f), new Vector2(w * 0.8f, h), new Vector2(-w * 0.9f, h * 0.8f) }, cols[i % cols.Length]);
			parts.Add((p, v, Fx.Rand(6f, 14f) * (i % 2 == 0 ? 1f : -1f), Fx.Rand(0f, 6f)));
		}
		root.Visible = false;
		Fx.Animate(root, delay, life, t =>
		{
			root.Visible = true;
			float s = t * life;
			foreach (var (p, v0, spin, ph) in parts)
			{
				float drag = Mathf.Exp(-s * 4f);
				p.Position = v0 * (1f - drag) / 4f + new Vector2(Mathf.Sin(s * 5f + ph) * size * 1.5f, 180f * s * s);
				p.Rotation = s * spin * 0.3f + ph;
				p.Scale = new Vector2(Mathf.Cos(s * spin + ph), 1f);
			}
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.65f, 0.35f));
		});
	}
}
