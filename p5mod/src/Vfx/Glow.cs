using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 发光特效积木（2026-10-29，移植自 P3 mod 的 mod/src/Vfx/P3Fx.cs，换成 Joker 的红白调色）。
/// 特效的主体是"光"：加法混合的柔光、白芯 + 红晕的锥形光线、着色器径向闪光 / 细光环、屏幕折射波、GPU 柔光粒子。
/// 运动法则同 P3（docs/STYLE_P3R.md 第 2 节）：预备 → 爆发 → 余韵，OutExpo，错峰，过冲，消失不急停。
/// 见 docs/p5/FX_PLAN.md。
/// </summary>
public static class Pal5
{
	public static readonly Color Hot = new("fff4f0");       // 热白：光的芯
	public static readonly Color Bright = new("ff1426");    // 亮红：光晕（加法混合会往粉色偏，所以绿 / 蓝分量压得很低）
	public static readonly Color Crimson = new("e5191c");   // 绯红：主色
	public static readonly Color Deep = new("5a0610");      // 深红：暗部
	public static readonly Color Gold = new("ffcf5a");      // 点缀金：暴击 / 大招
}

public static class Ease5
{
	public static float OutExpo(float t) => t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);

	public static float OutQuint(float t) => 1f - Mathf.Pow(1f - t, 5f);

	public static float OutBack(float t)
	{
		const float c1 = 1.70158f, c3 = c1 + 1f;
		return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
	}

	/// <summary>全局进度 t 在 [start, start+length] 里的局部进度。</summary>
	public static float Seg(float t, float start, float length) => Mathf.Clamp((t - start) / length, 0f, 1f);
}

public static class Glow
{
	// ───────────────────────── 着色器 ─────────────────────────

	private static Shader? _ring, _flash, _refract;

	/// <summary>细光环：radius 0~1，width 环宽，alpha 整体透明度。</summary>
	private static Shader Ring => _ring ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(1.0, 0.2, 0.25, 1.0);
uniform float radius = 0.3;
uniform float width = 0.05;
uniform float alpha = 1.0;
void fragment() {
	float d = length(UV * 2.0 - 1.0);
	float core = smoothstep(width, 0.0, abs(d - radius));
	float glow = smoothstep(width * 5.0, 0.0, abs(d - radius)) * 0.35;
	float edge = smoothstep(1.0, 0.8, d);
	COLOR = vec4(tint.rgb * (1.0 + core), (core + glow) * alpha * tint.a * edge);
}" };

	/// <summary>径向闪光。</summary>
	private static Shader FlashShader => _flash ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(1.0, 0.85, 0.85, 1.0);
uniform float alpha = 1.0;
void fragment() {
	float d = length(UV * 2.0 - 1.0);
	float a = pow(clamp(1.0 - d, 0.0, 1.0), 2.2);
	COLOR = vec4(tint.rgb, a * alpha * tint.a);
}" };

	/// <summary>折射冲击波：把后面的画面沿环扭一下，带 RGB 色散和一层红色高光。</summary>
	private static Shader Refract => _refract ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec4 tint : source_color = vec4(1.0, 0.35, 0.35, 1.0);
uniform float radius = 0.3;
uniform float width = 0.12;
uniform float strength = 0.025;
uniform float alpha = 1.0;
void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float d = length(uv);
	float x = (d - radius) / max(width, 0.0001);
	float edge = smoothstep(1.0, 0.72, d);
	float k = exp(-x * x * 2.5) * edge;
	vec2 dir = d > 0.0001 ? uv / d : vec2(0.0);
	vec2 off = dir * (-x * exp(-x * x * 2.5)) * edge * strength * alpha;
	float r = texture(screen_tex, SCREEN_UV - off * 1.12).r;
	float g = texture(screen_tex, SCREEN_UV - off).g;
	float b = texture(screen_tex, SCREEN_UV - off * 0.88).b;
	float rim = exp(-x * x * 9.0) * edge;
	vec3 col = vec3(r, g, b) + tint.rgb * rim * 0.22 * alpha;
	COLOR = vec4(col, clamp(k * 3.0, 0.0, 1.0) * clamp(alpha * 4.0, 0.0, 1.0));
}" };

	private static Shader? _curseFire;

	/// <summary>
	/// 咒焰（实时生成）：两层分形噪声互相扭曲、随时间往上流动，裁出一根往上收窄的火柱；
	/// 火的身体是近黑的暗红，边缘一圈亮红描边（P5 的"黑底红边"），里面有往上窜的红色纹路，顶端被噪声撕碎。
	/// height 0~1：火柱长到多高；alpha：整体；seed：每根火不一样；sway：左右摆。
	/// </summary>
	public static Shader CurseFireShader => _curseFire ??= new Shader { Code = @"
shader_type canvas_item;
uniform float height = 1.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
uniform float width = 1.0;
uniform vec4 dark_color : source_color = vec4(0.09, 0.0, 0.03, 1.0);
uniform vec4 rim_color : source_color = vec4(1.0, 0.07, 0.11, 1.0);
uniform float speed = 1.0;
uniform float body_alpha = 0.5;
uniform float clock = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 5; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; }
	return v;
}
void fragment() {
	float y = 1.0 - UV.y;                                   // 0 = 底
	float t = clock * 1.9 * speed + seed * 17.0;
	vec2 q = vec2(UV.x * 3.2 + seed, y * 2.4 - t);
	float warp = fbm(q * 1.4 + vec2(0.0, -t * 0.4));
	float n = fbm(q + warp * 1.6);
	float yy = clamp(y / max(height, 0.01), 0.0, 1.0);
	float w = mix(0.34, 0.03, pow(yy, 0.7)) * width;
	float dx = abs(UV.x - 0.5 + (n - 0.5) * 0.6 * y + sin(t * 1.3 + y * 5.0) * 0.04 * y);
	float d = w - dx + (n - 0.5) * 0.38 * (0.4 + yy);
	// 往上飘走的火舌：沿高度往上流的噪声在火身上掏出缺口，上半截一块块断开
	float gaps = smoothstep(0.55, 0.85, noise(vec2(UV.x * 7.0 + seed * 3.0, y * 4.0 - t * 1.7)));
	d -= gaps * 0.2 * smoothstep(0.25, 0.9, yy);
	d *= smoothstep(height, height - 0.3, y) * smoothstep(0.0, 0.05, y);
	float body = smoothstep(0.0, 0.012, d);
	float rim = body - smoothstep(0.016, 0.05, d);
	// 往上窜的亮纹（竖向拉长的噪声）
	float veins = smoothstep(0.55, 0.8, fbm(vec2(UV.x * 5.0, y * 1.6 - t * 1.4) + warp)) * (1.0 - rim);
	vec3 dark = dark_color.rgb;
	vec3 red = rim_color.rgb;
	vec3 col = mix(dark, red, rim) + red * 0.9 * veins * body;
	col += vec3(1.0, 0.8, 0.75) * smoothstep(0.8, 1.0, rim) * 0.45;
	float a = body * mix(body_alpha, 1.0, max(rim, veins));         // 火身半透（透得出后面的敌人），边和亮纹不透
	COLOR = vec4(col, a * alpha);
}" };

	/// <summary>一根咒焰：脚在 foot，宽 w、最高 h；delay 后 0.15s 蹿到顶、摇着烧 life 秒、再往下缩掉。</summary>
	public static void CurseFire(Control layer, Vector2 foot, float w, float h, float delay, float life, float widthK = 1f,
		Color? rim = null, Color? dark = null, float speed = 1f, float bodyAlpha = 0.5f)
	{
		Node2D root = Fx.Root(layer, foot, "P5CurseFire", delay + life + 0.1);
		ColorRect r = new()
		{
			Size = new Vector2(w, h), Position = new Vector2(-w / 2f, -h), MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = CurseFireShader },
		};
		root.AddChild(r);
		Fx.Clock(r);
		Set(r, "seed", Fx.Rand(0f, 10f));
		Set(r, "width", widthK);
		Set(r, "height", 0f);
		Set(r, "speed", speed);
		Set(r, "body_alpha", bodyAlpha);
		if (rim is Color rc)
		{
			Set(r, "rim_color", rc);
		}
		if (dark is Color dc)
		{
			Set(r, "dark_color", dc);
		}
		Fx.Animate(root, delay, life, t =>
		{
			float up = Ease5.OutBack(Ease5.Seg(t, 0f, 0.22f)), down = Fx.InCubic(Ease5.Seg(t, 0.6f, 0.4f));
			Set(r, "height", Mathf.Clamp(up * (1f - 0.85f * down), 0f, 1.05f));
			Set(r, "alpha", 1f - Ease5.Seg(t, 0.85f, 0.15f));
		});
	}

	private static ColorRect Quad(Node parent, Shader shader, float size, float squashY = 1f)
	{
		ColorRect rect = new()
		{
			Size = new Vector2(size, size),
			Position = new Vector2(-size / 2f, -size / 2f),
			PivotOffset = new Vector2(size / 2f, size / 2f),
			Scale = new Vector2(1f, squashY),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = shader },
		};
		parent.AddChild(rect);
		return rect;
	}

	private static void Set(ColorRect r, string name, Variant v) => ((ShaderMaterial)r.Material).SetShaderParameter(name, v);

	public static void SetParam(ColorRect r, string name, Variant v) => Set(r, name, v);

	/// <summary>一圈细光环（着色器，参数 tint / radius / width / alpha 由调用方驱动），中心在 parent 原点。</summary>
	public static ColorRect RingRect(Node parent, float size, float squashY = 1f) => Quad(parent, Ring, size, squashY);

	/// <summary>一团柔光（加法），scale 1 = 32px。</summary>
	public static Sprite2D SoftSprite(Node parent, Vector2 at, Color tint, float scale)
	{
		Sprite2D s = new() { Texture = Soft, Material = Fx.Additive, Position = at, Scale = Vector2.One * scale, Modulate = tint };
		parent.AddChild(s);
		return s;
	}

	// ───────────────────────── 积木 ─────────────────────────

	/// <summary>径向闪光：一下亮到顶、0.5 倍胀到 1.25 倍、淡掉。</summary>
	public static void Flash(Control layer, Vector2 at, Color tint, float size = 300f, float duration = 0.22f, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5Flash", duration + delay + 0.1);
		ColorRect q = Quad(root, FlashShader, size);
		Set(q, "tint", tint);
		Set(q, "alpha", 0f);
		Fx.Animate(q, delay, duration, t =>
		{
			Set(q, "alpha", 1f - Fx.OutCubic(t));
			q.Scale = Vector2.One * Mathf.Lerp(0.5f, 1.25f, Ease5.OutExpo(t));
		});
	}

	/// <summary>细光环往外扩。</summary>
	public static void Shockwave(Control layer, Vector2 at, Color tint, float size = 480f, float duration = 0.5f, float squashY = 1f, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5Shockwave", duration + delay + 0.1);
		ColorRect q = Quad(root, Ring, size, squashY);
		Set(q, "tint", tint);
		Set(q, "alpha", 0f);
		Fx.Animate(q, delay, duration, t =>
		{
			float e = Ease5.OutExpo(t);
			Set(q, "radius", Mathf.Lerp(0.04f, 0.95f, e));
			Set(q, "width", Mathf.Lerp(0.05f, 0.008f, e));
			Set(q, "alpha", 1f - Fx.InCubic(t));
		});
	}

	/// <summary>往里收的蓄力环（预备段）。</summary>
	public static void Gather(Control layer, Vector2 at, Color tint, float size = 300f, float duration = 0.14f, float squashY = 1f)
	{
		Node2D root = Fx.Root(layer, at, "P5Gather", duration + 0.05);
		ColorRect q = Quad(root, Ring, size, squashY);
		Set(q, "tint", tint);
		Fx.Animate(q, 0, duration, t =>
		{
			Set(q, "radius", Mathf.Lerp(0.9f, 0.05f, Fx.InCubic(t)));
			Set(q, "width", 0.02f);
			Set(q, "alpha", Mathf.Lerp(0.2f, 1f, t));
		});
	}

	private static int _ripples;

	/// <summary>屏幕折射波（空间被扭一下）。同时最多 3 个（每个像素采样三次，叠多了吃显卡）。</summary>
	public static void Ripple(Control layer, Vector2 at, float size = 460f, float duration = 0.55f, float strength = 0.03f, float squashY = 1f, float delay = 0f)
	{
		if (_ripples >= 3)
		{
			return;
		}
		_ripples++;
		Node2D root = Fx.Root(layer, at, "P5Ripple", duration + delay + 0.1);
		root.TreeExiting += () => _ripples--;
		ColorRect q = Quad(root, Refract, size, squashY);
		Set(q, "strength", strength);
		Set(q, "alpha", 0f);
		Fx.Animate(q, delay, duration, t =>
		{
			float e = Fx.OutCubic(t);
			Set(q, "radius", Mathf.Lerp(0.02f, 0.9f, e));
			Set(q, "width", Mathf.Lerp(0.16f, 0.07f, e));
			Set(q, "alpha", (1f - t) * (1f - t));
		});
	}

	private static GradientTexture2D? _soft;

	/// <summary>径向柔光点（代码生成）。</summary>
	public static GradientTexture2D Soft => _soft ??= new GradientTexture2D
	{
		Gradient = new Gradient
		{
			Offsets = new[] { 0f, 0.35f, 1f },
			Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0f) },
		},
		Fill = GradientTexture2D.FillEnum.Radial,
		FillFrom = new Vector2(0.5f, 0.5f),
		FillTo = new Vector2(1f, 0.5f),
		Width = 32,
		Height = 32,
	};

	/// <summary>
	/// 一次性喷出的柔光粒子（GPU）。dir / spread：喷射方向和张角（度）；ramp：颜色随寿命变化（最后自动淡出）。
	/// </summary>
	public static GpuParticles2D Sparks(Control layer, Vector2 at, int amount, float speedMin, float speedMax, float lifetime,
		Color[] ramp, float gravity = 0f, float scaleMin = 0.15f, float scaleMax = 0.45f, Vector2? dir = null, float spread = 180f)
	{
		Node2D root = Fx.Root(layer, at, "P5Sparks", lifetime + 0.3);
		var offsets = new float[ramp.Length + 1];
		var colors = new Color[ramp.Length + 1];
		for (int i = 0; i < ramp.Length; i++)
		{
			offsets[i] = ramp.Length == 1 ? 0f : 0.85f * i / (ramp.Length - 1);
			colors[i] = ramp[i];
		}
		offsets[^1] = 1f;
		colors[^1] = new Color(ramp[^1], 0f);                    // 最后自动淡出
		var g = new Gradient { Offsets = offsets, Colors = colors };
		Vector2 d = dir ?? Vector2.Up;
		ParticleProcessMaterial pm = new()
		{
			Direction = new Vector3(d.X, d.Y, 0f),
			Spread = spread,
			InitialVelocityMin = speedMin,
			InitialVelocityMax = speedMax,
			Gravity = new Vector3(0f, gravity, 0f),
			ScaleMin = scaleMin,
			ScaleMax = scaleMax,
			DampingMin = 2f,
			DampingMax = 6f,
			ColorRamp = new GradientTexture1D { Gradient = g },
		};
		GpuParticles2D p = new()
		{
			Amount = Mathf.Max(1, amount),
			Lifetime = lifetime,
			OneShot = true,
			Explosiveness = 0.95f,
			ProcessMaterial = pm,
			Texture = Soft,
			Material = Fx.Additive,
			LocalCoords = false,
			Emitting = true,
		};
		root.AddChild(p);
		return p;
	}

	private static Curve? _taper;

	/// <summary>两头尖、中间粗的线宽曲线。</summary>
	public static Curve Taper
	{
		get
		{
			if (_taper == null)
			{
				_taper = new Curve();
				_taper.AddPoint(new Vector2(0f, 0f));
				_taper.AddPoint(new Vector2(0.35f, 1f));
				_taper.AddPoint(new Vector2(1f, 0f));
			}
			return _taper;
		}
	}

	/// <summary>加法混合的线（可选锥形线宽）。</summary>
	public static Line2D Line(Node parent, Vector2[] pts, Color color, float width, bool taper = true)
	{
		Line2D l = new() { Points = pts, DefaultColor = color, Width = width, Material = Fx.Additive, Antialiased = true,
			JointMode = Line2D.LineJointMode.Round };
		if (taper)
		{
			l.WidthCurve = Taper;
		}
		parent.AddChild(l);
		return l;
	}

	/// <summary>
	/// 两头尖的直线光（多边形画，加法）：p0 → p1，最宽处在 35%，宽 w。
	/// （Line2D 的 WidthCurve 在兼容渲染器里画不出来，所以锥形线都用多边形。）
	/// </summary>
	public static Vector2[] TaperPts(Vector2 p0, Vector2 p1, float w)
	{
		Vector2 d = p1 - p0;
		if (d.LengthSquared() < 0.01f)
		{
			d = new Vector2(0.1f, 0f);
		}
		Vector2 n = new Vector2(-d.Y, d.X).Normalized() * w / 2f, m = p0 + d * 0.35f;
		return new[] { p0, m + n, p1, m - n };
	}

	/// <summary>沿折线的带子：尾巴是尖、头部最宽 w。</summary>
	public static Vector2[] Ribbon(List<Vector2> pts, float w)
	{
		int n = pts.Count;
		var outPts = new Vector2[n * 2];
		for (int i = 0; i < n; i++)
		{
			Vector2 d = (pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)]).Normalized();
			Vector2 nn = new Vector2(-d.Y, d.X) * w * 0.5f * Mathf.Pow((float)i / (n - 1), 1.3f);
			outPts[i] = pts[i] + nn;
			outPts[n * 2 - 1 - i] = pts[i] - nn;
		}
		return outPts;
	}

	public static Polygon2D TaperPoly(Node parent, Color color) => Fx.Poly(parent, new[] { Vector2.Zero, Vector2.Right, Vector2.Down }, color, additive: true);

	/// <summary>
	/// 光刃：一道直线光，红晕 + 白芯，头部飞快伸出、尾巴随后追上，越收越细。at 为中心，ang 为方向，length 全长。
	/// </summary>
	public static void Blade(Control layer, Vector2 at, float ang, float length, float width = 30f, float duration = 0.36f, float delay = 0f,
		Color? tint = null)
	{
		Node2D root = Fx.Root(layer, at, "P5Blade", duration + delay + 0.1);
		Vector2 dir = Vector2.Right.Rotated(ang), a0 = -dir * length / 2f;
		Polygon2D glow = TaperPoly(root, new Color(tint ?? Pal5.Bright, 0.9f));
		Polygon2D core = TaperPoly(root, Pal5.Hot);
		root.Visible = delay <= 0;
		Fx.Animate(root, delay, duration, t =>
		{
			root.Visible = true;
			float head = Ease5.OutExpo(Mathf.Min(1f, t * 3f)), tail = Fx.InCubic(t);
			Vector2 p0 = a0 + dir * length * tail, p1 = a0 + dir * length * head;
			float w = Mathf.Lerp(1f, 0.35f, t);
			glow.Polygon = TaperPts(p0, p1, width * w);
			core.Polygon = TaperPts(p0, p1, width * 0.3f * w);
		});
	}

	/// <summary>放射速度线：外端飞快伸出、内端追上，整条收掉。</summary>
	public static void SpeedLines(Control layer, Vector2 at, Color tint, int count = 12, float inner = 50f, float outer = 300f,
		float duration = 0.32f, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5Speed", duration + delay + 0.2);
		for (int i = 0; i < count; i++)
		{
			Vector2 dir = Vector2.Right.Rotated(Mathf.Tau * i / count + Fx.Rand(-0.12f, 0.12f));
			float reach = outer * Fx.Rand(0.6f, 1.1f), lag = Fx.Rand(0f, 0.06f);
			Polygon2D line = TaperPoly(root, i % 3 == 0 ? Pal5.Hot : tint);
			float w = Fx.Rand(3f, 6f);
			line.Visible = false;
			Fx.Animate(line, delay + lag, duration, t =>
			{
				line.Visible = true;
				float head = Ease5.OutExpo(Mathf.Min(1f, t * 1.6f)), tail = Fx.InCubic(t);
				line.Polygon = TaperPts(dir * Mathf.Lerp(inner, reach, tail), dir * Mathf.Lerp(inner, reach, head), w);
				line.Modulate = new Color(1f, 1f, 1f, 1f - tail);
			});
		}
	}

	/// <summary>
	/// 锐利碎片：细长三角带阻尼迸射，转动时随角度闪一下棱光（白边 + 半透明红面，加法）。
	/// </summary>
	public static void Shards(Control layer, Vector2 at, int count, float power = 1f, Vector2? bias = null, float spread = Mathf.Pi)
	{
		Node2D root = Fx.Root(layer, at, "P5Shards", 1.1);
		Vector2 b = bias ?? Vector2.Right;
		for (int i = 0; i < count; i++)
		{
			float len = Fx.Rand(10f, 26f) * power, wid = len * Fx.Rand(0.25f, 0.5f);
			Vector2[] shape = { new(-len * 0.5f, 0f), new(-len * 0.1f, -wid * 0.5f), new(len * 0.5f, Fx.Rand(-wid * 0.2f, wid * 0.2f)), new(len * 0.05f, wid * 0.5f) };
			Node2D shard = new();
			root.AddChild(shard);
			Polygon2D body = Fx.Poly(shard, shape, new Color(Pal5.Bright, 0.45f), additive: true);
			Line2D edge = new() { Points = shape, Closed = true, Width = 1.4f, DefaultColor = Pal5.Hot, Material = Fx.Additive, Antialiased = true };
			shard.AddChild(edge);
			Vector2 dir = b.Rotated(Fx.Rand(-spread, spread));
			float speed = Fx.Rand(380f, 1000f) * power, spin = Fx.Rand(-14f, 14f), life = Fx.Rand(0.5f, 0.9f), phase = Fx.Rand(0f, Mathf.Tau);
			const float drag = 4.5f;
			Fx.Animate(shard, 0, life, t =>
			{
				float s = t * life, travel = speed * (1f - Mathf.Exp(-drag * s)) / drag;
				shard.Position = dir * travel + new Vector2(0f, 240f * s * s);
				shard.Rotation = dir.Angle() + spin * (1f - Mathf.Exp(-2.5f * s)) / 2.5f;
				float glint = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(shard.Rotation * 3f + phase)), 8f);
				edge.Modulate = new Color(1f + glint, 1f + glint, 1f + glint, 1f);
				body.Modulate = new Color(1f, 1f, 1f, 0.6f + glint * 0.8f);
				shard.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t));
				shard.Scale = Vector2.One * Mathf.Lerp(1f, 0.6f, t);
			});
		}
	}

	/// <summary>四角星光：一闪即逝，带一点红晕。</summary>
	public static void Glint(Control layer, Vector2 at, float size = 1f, float delay = 0f)
	{
		Node2D root = Fx.Root(layer, at, "P5Glint", delay + 0.35);
		Sprite2D halo = new() { Texture = Soft, Material = Fx.Additive };
		root.AddChild(halo);
		Polygon2D star = Fx.Poly(root, Star4(26f * size, 0.14f), Pal5.Hot, additive: true);
		root.Visible = false;
		Fx.Animate(root, delay, 0.28, t =>
		{
			root.Visible = true;
			float k = Mathf.Sin(Mathf.Min(t * 1.5f, 1f) * Mathf.Pi);
			star.Scale = Vector2.One * k;
			star.Rotation = t * 0.7f;
			halo.Scale = Vector2.One * 2.2f * size * k;
			halo.Modulate = new Color(Pal5.Bright, 0.7f * k);
		});
	}

	public static Vector2[] Star4(float r, float inner = 0.28f)
	{
		var pts = new Vector2[8];
		for (int i = 0; i < 8; i++)
		{
			pts[i] = Vector2.Up.Rotated(Mathf.Tau * i / 8) * (i % 2 == 0 ? r : r * inner);
		}
		return pts;
	}
}
