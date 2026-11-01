using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// P5 的平面「墨」积木（用户 2026-11-01：打击 / 枪击 / 防御 / 选敌标记太朴素，重做）。
/// P5 的打击感来自平面漫画语言，不是发光：硬边、黑描边、红 / 白 / 黑三色、锯齿爆点、网点、平面碎片、速度线，
/// 加上原版 P5R 的 UI 贴图（箭头、星光、GUARD 字、枪击爆点序列帧）。这里的东西都是普通混合（不是加法），边缘是硬的。
/// </summary>
public static class Ink
{
	public static readonly Color Black = new("0b0205"), Red = new("e5191c"), White = new("fff8f4"), Dark = new("5c0610"), Gold = new("ffd24a");

	// ───────────── 锯齿爆点 ─────────────

	/// <summary>
	/// 漫画爆点：黑描边 → 红 → 白芯三层锯齿星，0.05s 猛地炸开（过冲），停一下，再转着缩没（硬切，不淡出）。
	/// squash：横向拉长（枪口焰用），ang：拉长的方向。
	/// </summary>
	public static void Burst(Control layer, Vector2 at, float r, int spikes, float life, float delay = 0f, float squash = 1f, float ang = 0f,
		Color? mid = null)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5InkBurst", life + 0.05);
			root.Rotation = ang;
			Node2D body = new();
			root.AddChild(body);
			Vector2[] shape = Fx.Burst(r, r * 0.52f, spikes, 0.32f);
			Fx.Poly(body, Scale(shape, 1.16f, 7f), Black);
			Fx.Poly(body, shape, mid ?? Red);
			Polygon2D core = Fx.Poly(body, Scale(Fx.Burst(r * 0.62f, r * 0.3f, spikes, 0.3f), 1f, 0f), White);
			float spin = Fx.Rand(-0.5f, 0.5f);
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				float pop = s < 0.05f ? Ease5.OutExpo(s / 0.05f) * 1.18f : Mathf.Lerp(1.18f, 1f, Mathf.Min(1f, (s - 0.05f) / 0.06f));
				float gone = Fx.InCubic(Ease5.Seg(t, 0.55f, 0.45f));
				float k = pop * (1f - gone);
				body.Scale = new Vector2(k * squash, k);
				body.Rotation = spin * t;
				core.Scale = Vector2.One * (1f - Ease5.Seg(t, 0.3f, 0.35f));       // 白芯先收掉，剩红黑
			});
		});
	}

	/// <summary>多边形往外放大 k 倍再外扩 pad 像素（描边层）。</summary>
	public static Vector2[] Scale(Vector2[] pts, float k, float pad)
	{
		var o = new Vector2[pts.Length];
		for (int i = 0; i < pts.Length; i++)
		{
			float l = pts[i].Length();
			o[i] = l < 0.001f ? pts[i] : pts[i] / l * (l * k + pad);
		}
		return o;
	}

	// ───────────── 斩线（两头尖的硬边透镜形）─────────────

	/// <summary>透镜形：从 -len/2 到 len/2，中间最宽 w，两头尖；只取 u0..u1 那一段（画出来 / 收掉）。</summary>
	public static Vector2[] Lens(float len, float w, float u0, float u1)
	{
		const int n = 18;
		if (u1 - u0 < 0.005f || w < 0.2f)
		{
			return new Vector2[3];
		}
		var pts = new Vector2[n * 2];
		for (int i = 0; i < n; i++)
		{
			float u = Mathf.Lerp(u0, u1, (float)i / (n - 1));
			float half = w * 0.5f * Mathf.Pow(Mathf.Sin(u * Mathf.Pi), 0.7f);
			float x = (u - 0.5f) * len;
			pts[i] = new Vector2(x, -half);
			pts[n * 2 - 1 - i] = new Vector2(x, half * 0.55f);           // 下沿窄一点：刀锋是斜的，不是对称的梭子
		}
		return pts;
	}

	/// <summary>
	/// 一道斩线：0.045s 从一头划到另一头（黑描边 + 白芯 + 错开一点的红线），然后变细收掉。ang = 方向。
	/// </summary>
	public static void Slash(Control layer, Vector2 at, float ang, float len, float w, float life = 0.26f, float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5InkSlash", life + 0.05);
			root.Rotation = ang;
			Polygon2D red = Fx.Poly(root, new Vector2[3], Red);
			Polygon2D black = Fx.Poly(root, new Vector2[3], Black);
			Polygon2D white = Fx.Poly(root, new Vector2[3], White);
			red.Position = new Vector2(10f, w * 0.55f);
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				float head = Ease5.OutExpo(Mathf.Min(1f, s / 0.045f));
				float thin = 1f - Fx.InCubic(Ease5.Seg(t, 0.3f, 0.7f));
				float tail = Ease5.Seg(t, 0.55f, 0.45f) * 0.5f;
				float grow = 1f + 0.12f * t;
				black.Polygon = Lens(len * 1.06f * grow, (w + 12f) * thin, tail, head);
				white.Polygon = Lens(len * grow, w * thin, tail, head);
				red.Polygon = Lens(len * 0.9f * grow, w * 0.45f * thin, tail, Ease5.OutExpo(Mathf.Min(1f, Mathf.Max(0f, s - 0.015f) / 0.05f)));
			});
		});
	}

	// ───────────── 网点环 ─────────────

	private static Shader? _halftone;

	private static Shader HalftoneShader => _halftone ??= new Shader { Code = @"
shader_type canvas_item;
uniform float size = 400.0;     // 方块边长（像素）
uniform float prog = 0.0;       // 0→1：环从中心扩到边上
uniform float band = 0.22;      // 环的宽度（半径的比例）
uniform float cell = 14.0;      // 网点间距（像素）
uniform float fade = 0.0;
uniform vec4 tint : source_color = vec4(0.9, 0.08, 0.1, 1.0);
void fragment() {
	vec2 p = (UV - 0.5) * size;
	float d = length(UV - 0.5) * 2.0;
	vec2 rp = vec2(p.x + p.y, p.y - p.x) * 0.7071;
	vec2 c = fract(rp / cell) - 0.5;
	float k = clamp(1.0 - abs(d - prog) / band, 0.0, 1.0) * (1.0 - fade);
	k *= step(d, 1.0);
	float r = 0.5 * k;
	float dot_ = 1.0 - smoothstep(r - 0.06, r, length(c));
	COLOR = vec4(tint.rgb, tint.a * dot_ * step(0.02, k));
}" };

	/// <summary>一圈红色网点从中心往外扩（网点在环中间最大、两边变小），squashY = 压扁（地面上的环）。</summary>
	public static void Halftone(Control layer, Vector2 at, float radius, float life, float delay = 0f, Color? tint = null, float squashY = 1f,
		float cell = 14f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5InkDots", life + 0.05);
			root.Scale = new Vector2(1f, squashY);
			float size = radius * 2f;
			ColorRect r = new()
			{
				Size = new Vector2(size, size), Position = new Vector2(-radius, -radius), MouseFilter = Control.MouseFilterEnum.Ignore,
				Material = new ShaderMaterial { Shader = HalftoneShader },
			};
			root.AddChild(r);
			ShaderMaterial m = (ShaderMaterial)r.Material;
			m.SetShaderParameter("size", size);
			m.SetShaderParameter("cell", cell);
			m.SetShaderParameter("tint", tint ?? Red);
			Fx.Animate(root, 0, life, t =>
			{
				m.SetShaderParameter("prog", Ease5.OutQuint(t) * 0.95f);
				m.SetShaderParameter("fade", Ease5.Seg(t, 0.5f, 0.5f));
			});
		});
	}

	// ───────────── 平面碎片 ─────────────

	/// <summary>
	/// 平面三角碎片（黑描边，红 / 白 / 黑），往 dir 方向 ± spread 喷出去，转着、受重力往下掉，最后缩没。
	/// </summary>
	public static void Shards(Control layer, Vector2 at, int count, float size, Vector2 dir, float spread, float speed = 700f, float life = 0.55f,
		float delay = 0f, Color[]? colors = null)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5InkShards", life + 0.05);
			colors ??= new[] { Red, White, Red, Black };
			var list = new List<(Node2D N, Vector2 V, float W)>();
			float baseAng = dir.Angle();
			for (int i = 0; i < count; i++)
			{
				float s = size * Fx.Rand(0.45f, 1.1f);
				Vector2[] tri = { new(s, 0f), new(-s * 0.4f, -s * Fx.Rand(0.25f, 0.5f)), new(-s * 0.55f, s * Fx.Rand(0.15f, 0.4f)) };
				Node2D n = new() { Rotation = Fx.Rand(0f, Mathf.Tau) };
				root.AddChild(n);
				Fx.Poly(n, Scale(tri, 1f, 4f), Black);
				Fx.Poly(n, tri, colors[i % colors.Length]);
				float a = baseAng + Fx.Rand(-spread, spread);
				list.Add((n, Vector2.Right.Rotated(a) * speed * Fx.Rand(0.4f, 1f), Fx.Rand(-14f, 14f)));
			}
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				foreach (var (n, v, w) in list)
				{
					n.Position = v * (s - 0.35f * s * s / life) + new Vector2(0f, 900f * s * s);   // 先冲出去，越来越慢，再往下掉
					n.Rotation += w / 60f;
					n.Scale = Vector2.One * (1f - Fx.InCubic(Ease5.Seg(t, 0.55f, 0.45f)));
				}
			});
		});
	}

	// ───────────── 速度线 ─────────────

	/// <summary>从 at 往外射的硬边速度线（黑 / 白交替的细楔子），从内圈射到外圈再收掉。</summary>
	public static void Streaks(Control layer, Vector2 at, int count, float inner, float outer, float life = 0.22f, float delay = 0f, float width = 7f,
		Vector2? dir = null, float spread = Mathf.Pi)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5InkStreaks", life + 0.05);
			var lines = new List<(Polygon2D P, Vector2 D, float L, float W)>();
			float baseAng = (dir ?? Vector2.Right).Angle();
			for (int i = 0; i < count; i++)
			{
				float a = dir == null ? Mathf.Tau * (i + Fx.Rand(-0.3f, 0.3f)) / count : baseAng + Fx.Rand(-spread, spread);
				Polygon2D p = Fx.Poly(root, new Vector2[3], i % 3 == 0 ? White : Black);
				lines.Add((p, Vector2.Right.Rotated(a), Fx.Rand(0.6f, 1f), width * Fx.Rand(0.6f, 1.2f)));
			}
			Fx.Animate(root, 0, life, t =>
			{
				float head = Ease5.OutExpo(Mathf.Min(1f, t * 2.2f)), tail = Fx.InCubic(t);
				foreach (var (p, d, l, w) in lines)
				{
					float r0 = Mathf.Lerp(inner, outer * l, tail), r1 = Mathf.Lerp(inner, outer * l, head);
					p.Polygon = r1 - r0 < 1f ? new Vector2[3] : Glow.TaperPts(d * r1, d * r0, w * (1f - t * 0.5f));
				}
			});
		});
	}

	// ───────────── 原版贴图：硬边 + 黑描边 ─────────────

	private static Shader? _sprite;

	/// <summary>
	/// 把原版的白色剪影贴图画成硬边（alpha 阈值）+ 黑描边；fill = 填充色（乘贴图颜色），cut 往上推 = 从细处往粗处「蚀掉」（烟、爆点消失）。
	/// </summary>
	public static Shader SpriteShader => _sprite ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec4 fill : source_color = vec4(1.0);
uniform vec4 line : source_color = vec4(0.04, 0.0, 0.02, 1.0);
uniform float width = 3.0;
uniform float cut = 0.5;
uniform float alpha = 1.0;
void fragment() {
	vec4 src = texture(TEXTURE, UV);
	float o = src.a;
	vec2 px = TEXTURE_PIXEL_SIZE * width;
	for (int i = 0; i < 12; i++) {
		float a = float(i) * 0.5236;
		o = max(o, texture(TEXTURE, UV + vec2(cos(a), sin(a)) * px).a);
	}
	float fa = smoothstep(cut - 0.08, cut + 0.08, src.a);
	float oa = width > 0.01 ? smoothstep(cut - 0.08, cut + 0.08, o) : fa;
	vec3 c = mix(line.rgb, fill.rgb * src.rgb, fa);
	COLOR = vec4(c, oa * alpha * COLOR.a);
}" };

	/// <summary>一张硬边描边的贴图（region = 贴图集里的一帧，null = 整张）。</summary>
	public static Sprite2D? Sprite(Node parent, string tex, Color fill, float width = 3f, Rect2? region = null)
	{
		if (Fx.Tex(tex) is not Texture2D t)
		{
			return null;
		}
		ShaderMaterial m = new() { Shader = SpriteShader };
		m.SetShaderParameter("fill", fill);
		m.SetShaderParameter("width", width);
		Sprite2D s = new() { Texture = t, Material = m, TextureFilter = CanvasItem.TextureFilterEnum.Linear };
		if (region is Rect2 r)
		{
			s.RegionEnabled = true;
			s.RegionRect = r;
		}
		parent.AddChild(s);
		return s;
	}

	public static void Set(Sprite2D s, string name, Variant v) => ((ShaderMaterial)s.Material).SetShaderParameter(name, v);

	/// <summary>原版星光（BTL_BTN キラキラ）：一闪 —— 猛地放大、转一点、缩没。</summary>
	public static void Twinkle(Control layer, Vector2 at, float size, float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5InkTwinkle", 0.35);
			if (Sprite(root, "ui_sparkle", White, 3f) is not Sprite2D s)
			{
				return;
			}
			float k = size / 354f;
			Fx.Animate(root, 0, 0.3, t =>
			{
				float pop = t < 0.25f ? Ease5.OutBack(t / 0.25f) : 1f - Fx.InCubic((t - 0.25f) / 0.75f);
				s.Scale = Vector2.One * k * pop;
				s.Rotation = 0.6f * t;
			});
		});
	}

	// ───────────── 原版枪击爆点（gun_hit_light_anim：2×2 四帧）─────────────

	/// <summary>
	/// 原版枪击命中的爆点序列帧（弹孔星 → 炸开 → 碎点 → 余星），白色硬边黑描边，后面垫一张放大的红色同款。0.2s。
	/// </summary>
	public static void GunHit(Control layer, Vector2 at, float size, float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5InkGunHit", 0.3);
			root.Rotation = Fx.Rand(0f, Mathf.Tau);
			float k = size / 128f;
			Sprite2D? red = Sprite(root, "gun_hit", Red, 2.5f, Fx.Frame(0, 2, 128f, 128f));
			Sprite2D? white = Sprite(root, "gun_hit", White, 2.5f, Fx.Frame(0, 2, 128f, 128f));
			if (red == null || white == null)
			{
				return;
			}
			red.Position = new Vector2(8f, 8f);
			Fx.Animate(root, 0, 0.24, t =>
			{
				int f = Mathf.Min(3, (int)(t * 4.4f));
				red.RegionRect = white.RegionRect = Fx.Frame(f, 2, 128f, 128f);
				float pop = Ease5.OutExpo(Mathf.Min(1f, t * 6f));
				white.Scale = Vector2.One * k * (0.6f + 0.5f * pop);
				red.Scale = white.Scale * 1.22f;
			});
		});
	}

	// ───────────── 弧段 ─────────────

	/// <summary>圆环的一段（a0 起、扫 sweep 弧度），半径 r、宽 w。</summary>
	public static Vector2[] Arc(float r, float w, float a0, float sweep, int n = 16)
	{
		var pts = new Vector2[n * 2];
		for (int i = 0; i < n; i++)
		{
			float a = a0 + sweep * i / (n - 1);
			Vector2 d = Vector2.Right.Rotated(a);
			pts[i] = d * (r + w * 0.5f);
			pts[n * 2 - 1 - i] = d * (r - w * 0.5f);
		}
		return pts;
	}
}
