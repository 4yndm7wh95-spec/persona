using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// P5 的「冲击」语言（2026-11-01 用户：不要依赖原版素材、自己想 P5 的风格 + 游戏内渲染）。全部实时渲染：
/// · ImpactFrame —— 冲击帧：命中那 2～3 帧，命中点周围一块锯齿状的区域把画面压成红黑白三色剪影 + 红网点（读屏幕），硬切进、硬切出。
///   这是 P5 打中东西时最有辨识度的那一下「定格」。
/// · Split —— 画面被切开：沿斩线两侧的画面往相反方向错开几像素，缝里露出红芯白线，随后合上。
/// · Razor —— 剃刀斩线：极细的白色两头尖的线（红色薄光晕），0.03s 划过，变细消失。
/// · Slivers —— 斜切碎片：红 / 白 / 黑的平行四边形尖片，猛地冲出去、急停、转着缩没（不像卡通往下掉）。
/// · Cracks —— 放射裂纹：从命中点放射的几道折线白裂纹（枪击），0.04s 裂出来、定格、褪掉。
/// </summary>
public static class Cut
{
	private const string Noise = @"
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vnoise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	float a = hash(i + seed); float b = hash(i + vec2(1.0, 0.0) + seed);
	float c = hash(i + vec2(0.0, 1.0) + seed); float d = hash(i + vec2(1.0, 1.0) + seed);
	return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}
";

	private static ColorRect FullScreen(Node2D root, Control layer, Shader shader)
	{
		Vector2 screen = Fx.Screen(layer);
		ColorRect r = new() { Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = shader } };
		root.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });   // 读屏前先刷新
		root.AddChild(r);
		((ShaderMaterial)r.Material).SetShaderParameter("res", screen);
		return r;
	}

	// ───────────── 冲击帧 ─────────────

	private static Shader? _impact;

	private static Shader ImpactShader => _impact ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec2 res = vec2(1920.0, 1080.0);
uniform vec2 center = vec2(960.0, 540.0);
uniform float radius = 260.0;
uniform float stretch = 2.2;    // 菱形块长 / 宽
uniform float ang = 0.0;        // 块的方向（沿刀路）
uniform float seed = 0.0;
uniform float amount = 1.0;
" + Noise + @"
void fragment() {
	vec2 px = SCREEN_UV * res;
	vec2 d = px - center;
	vec2 tg = vec2(cos(ang), sin(ang));
	float u = dot(d, tg);
	float v = dot(d, vec2(-tg.y, tg.x));
	float W = radius * amount, L = radius * stretch * amount;
	// 两头尖的斜切菱形块（沿刀路），边缘是不规则的锯齿（一段段直的齿，不是圆的波浪）
	float k = clamp(abs(u) / L, 0.0, 1.0);
	float teeth = abs(fract(u * 0.035 + hash(vec2(floor(u * 0.035), seed)) * 0.5) - 0.5) * 0.5;
	float half_w = W * (1.0 - pow(k, 1.6)) * (0.82 + teeth + (vnoise(vec2(u * 0.01, 2.0)) - 0.5) * 0.3);
	float inside = step(abs(v), half_w) * step(abs(u), L);
	float rim = step(half_w - 5.0, abs(v)) * inside;
	vec3 src = texture(screen_tex, SCREEN_UV).rgb;
	float l = dot(src, vec3(0.299, 0.587, 0.114));
	l = clamp((l - 0.06) * 2.6, 0.0, 1.0);
	vec2 rp = vec2(px.x + px.y, px.y - px.x) * 0.7071;
	vec2 c = fract(rp / 7.0) - 0.5;
	float r0 = 0.62 * sqrt(l);
	float dots = 1.0 - smoothstep(r0 - 0.08, r0, length(c));
	vec3 black = vec3(0.025, 0.0, 0.01), red = vec3(0.9, 0.05, 0.09), white = vec3(1.0, 0.96, 0.94);
	vec3 st = mix(black, red, step(0.2, l) * mix(0.0, 1.0, max(dots, step(0.45, l))));
	st = mix(st, white, step(0.82, l));
	st = mix(st, white, rim);
	COLOR = vec4(st, inside);
}" };

	/// <summary>
	/// 冲击帧：at 处一块沿 ang 方向、两头尖的锯齿菱形（半宽 radius、半长 radius × stretch）压成红黑白剪影，持续 dur 秒（硬切）。
	/// </summary>
	public static void ImpactFrame(Control layer, Vector2 at, float radius, float dur = 0.09f, float delay = 0f, float ang = 0f, float stretch = 2.2f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, Vector2.Zero, "P5ImpactFrame", dur + 0.02);
			root.GlobalPosition = Vector2.Zero;
			ColorRect r = FullScreen(root, layer, ImpactShader);
			ShaderMaterial m = (ShaderMaterial)r.Material;
			m.SetShaderParameter("center", at);
			m.SetShaderParameter("radius", radius);
			m.SetShaderParameter("ang", ang);
			m.SetShaderParameter("stretch", stretch);
			m.SetShaderParameter("seed", Fx.Rand(0f, 100f));
			// 第一帧略大、之后缩一点（像被打得一缩），最后硬切掉
			Fx.Animate(root, 0, dur, t => m.SetShaderParameter("amount", t < 0.34f ? 1.08f : 1f));
		});
	}

	// ───────────── 画面切开 ─────────────

	private static Shader? _split;

	private static Shader SplitShader => _split ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec2 res = vec2(1920.0, 1080.0);
uniform vec2 p0 = vec2(0.0);
uniform vec2 p1 = vec2(100.0, 0.0);
uniform float open = 0.0;       // 错开多少像素
uniform float reach = 90.0;     // 离线多远的画面跟着错开
void fragment() {
	vec2 px = SCREEN_UV * res;
	vec2 ab = p1 - p0;
	float len = length(ab);
	vec2 tg = ab / max(len, 0.001);
	vec2 nm = vec2(-tg.y, tg.x);
	float t = dot(px - p0, tg) / max(len, 0.001);
	float d = dot(px - p0, nm);
	float taper = clamp(sin(3.14159 * clamp(t, 0.0, 1.0)), 0.0, 1.0) * step(0.0, t) * step(t, 1.0);
	float band = smoothstep(reach, reach * 0.3, abs(d)) * taper;
	float side = sign(d);
	vec2 shift = tg * side * open * band + nm * side * open * 0.35 * band;
	vec3 col = texture(screen_tex, (px - shift) / res).rgb;
	float gap = open * 0.45 * taper;
	float inGap = step(abs(d), gap);
	col = mix(col, vec3(0.9, 0.05, 0.09), inGap);
	col = mix(col, vec3(1.0, 0.97, 0.95), step(abs(d), gap * 0.3) * inGap);
	COLOR = vec4(col, step(0.002, band) * step(0.3, open));
}" };

	/// <summary>沿 a→b 把画面切开：0.03s 错开到 open 像素，停一下，0.15s 合上。</summary>
	public static void Split(Control layer, Vector2 a, Vector2 b, float open = 14f, float reach = 90f, float delay = 0f, float life = 0.22f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, Vector2.Zero, "P5Split", life + 0.02);
			root.GlobalPosition = Vector2.Zero;
			ColorRect r = FullScreen(root, layer, SplitShader);
			ShaderMaterial m = (ShaderMaterial)r.Material;
			m.SetShaderParameter("p0", a);
			m.SetShaderParameter("p1", b);
			m.SetShaderParameter("reach", reach);
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				float k = s < 0.03f ? Ease5.OutExpo(s / 0.03f) : 1f - Fx.InCubic(Ease5.Seg(s, 0.07f, life - 0.07f));
				m.SetShaderParameter("open", open * k);
			});
		});
	}

	// ───────────── 剃刀斩线 ─────────────

	/// <summary>极细的白色斩线（两头尖），外面一层薄红光晕（加法）；0.03s 划过、停一下、变细消失。</summary>
	public static void Razor(Control layer, Vector2 at, float ang, float len, float w = 7f, float life = 0.2f, float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5Razor", life + 0.05);
			root.Rotation = ang;
			Polygon2D halo = Fx.Poly(root, new Vector2[3], new Color(Pal5.Bright, 0.75f), additive: true);
			Polygon2D core = Fx.Poly(root, new Vector2[3], Ink.White);
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				float head = Ease5.OutExpo(Mathf.Min(1f, s / 0.03f));
				float thin = 1f - Fx.InCubic(Ease5.Seg(t, 0.35f, 0.65f));
				float tail = Ease5.Seg(t, 0.5f, 0.5f) * 0.6f;
				halo.Polygon = Ink.Lens(len * 1.04f, w * 4f * thin, tail, head);
				core.Polygon = Ink.Lens(len, w * thin, tail, head);
			});
		});
	}

	// ───────────── 斜切碎片 ─────────────

	/// <summary>斜切的平行四边形尖片：沿 dir ± spread 猛冲出去、急停（指数减速）、转着缩没。</summary>
	public static void Slivers(Control layer, Vector2 at, int count, float size, Vector2 dir, float spread, float speed = 1600f, float life = 0.42f,
		float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5Slivers", life + 0.05);
			Color[] cols = { Ink.Red, Ink.White, Ink.Red, new Color(0.06f, 0.01f, 0.02f), Ink.White };
			var list = new List<(Node2D N, Vector2 V, float W)>();
			float a0 = dir.Angle();
			for (int i = 0; i < count; i++)
			{
				float a = a0 + Fx.Rand(-spread, spread);
				float l = size * Fx.Rand(0.5f, 1.3f), w = l * Fx.Rand(0.12f, 0.3f), sk = l * Fx.Rand(0.2f, 0.5f);
				Node2D n = new() { Rotation = a + Fx.Rand(-0.4f, 0.4f) };
				root.AddChild(n);
				Fx.Poly(n, new[] { new Vector2(-l * 0.5f, w * 0.5f), new Vector2(l * 0.5f - sk, w * 0.5f), new Vector2(l * 0.5f, -w * 0.5f), new Vector2(-l * 0.5f + sk, -w * 0.5f) },
					cols[i % cols.Length]);
				list.Add((n, Vector2.Right.Rotated(a) * speed * Fx.Rand(0.35f, 1f), Fx.Rand(-8f, 8f)));
			}
			const float tau = 0.07f;
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				float go = tau * (1f - Mathf.Exp(-s / tau));
				foreach (var (n, v, w) in list)
				{
					n.Position = v * go + v * 0.04f * s;
					n.Rotation += w * (0.3f + 2f * Mathf.Exp(-s / tau)) / 60f;
					n.Scale = Vector2.One * (1f - Fx.InCubic(Ease5.Seg(t, 0.45f, 0.55f)));
				}
			});
		});
	}

	// ───────────── 放射裂纹 ─────────────

	/// <summary>从 at 放射 count 道三折的白裂纹（长 len 左右），0.04s 裂出来、定格、褪掉。</summary>
	public static void Cracks(Control layer, Vector2 at, int count, float len, float life = 0.3f, float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5Cracks", life + 0.05);
			var lines = new List<(Line2D L, Vector2[] Pts)>();
			float a0 = Fx.Rand(0f, Mathf.Tau);
			for (int i = 0; i < count; i++)
			{
				float a = a0 + Mathf.Tau * (i + Fx.Rand(-0.3f, 0.3f)) / count;
				float l = len * Fx.Rand(0.5f, 1.1f);
				var pts = new Vector2[4];
				pts[0] = Vector2.Zero;
				for (int k = 1; k < 4; k++)
				{
					pts[k] = Vector2.Right.Rotated(a + Fx.Rand(-0.25f, 0.25f)) * l * k / 3f;
				}
				Line2D line = new() { Width = 3f, DefaultColor = Ink.White, JointMode = Line2D.LineJointMode.Sharp };
				line.WidthCurve = Glow.Taper;
				root.AddChild(line);
				lines.Add((line, pts));
			}
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				float k = Ease5.OutExpo(Mathf.Min(1f, s / 0.04f));
				foreach (var (line, pts) in lines)
				{
					var cur = new List<Vector2>();
					float total = 3f * k;
					for (int j = 0; j < 4; j++)
					{
						if (j <= total) cur.Add(pts[j]);
						else
						{
							cur.Add(pts[j - 1].Lerp(pts[j], total - (j - 1)));
							break;
						}
					}
					line.Points = cur.ToArray();
				}
				root.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(Ease5.Seg(t, 0.5f, 0.5f)));
			});
		});
	}
}
