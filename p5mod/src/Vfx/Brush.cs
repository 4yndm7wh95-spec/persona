using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 实时渲染的墨笔 / 墨迹 / 玻璃（用户 2026-11-01：真正需要画的才生图，普通攻击特效游戏内渲染）。
/// · Slash —— 一笔墨笔斩痕：两头尖、中段粗，边缘噪声毛刺，沿笔画方向的拉丝飞白（收笔和边缘最多），上沿内夹一道白芯、下沿一道黑边；
///   起笔「刷」到收笔（前沿也是毛的），再从起笔那头褪掉。每一刀弯度、粗细、飞白都随机。
/// · Splat —— 命中的墨迹：一团不规则红墨（局部压黑），朝 dir 长出长短不一的尖刺，再从细处蚀掉；甩出去的墨点另算（Drops）。
/// · Glass —— 防御的玻璃板：半透明暗红厚玻璃 + 斜反光条 + 白 / 红亮边，裂纹实时画、碎片按多边形真实切开。
/// 噪声全部是不用 sin 的 hash（显卡上 sin hash 精度会崩），时间都由 C# 传进去（不用 TIME）。
/// </summary>
public static class Brush
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

	// ───────────── 墨笔斩痕 ─────────────

	private static Shader? _slash;

	private static Shader SlashShader => _slash ??= new Shader { Code = @"
shader_type canvas_item;
uniform float seed = 0.0;
uniform float reveal = 1.0;     // 刷到哪（0→1）
uniform float wipe = 0.0;       // 从起笔那头褪到哪
uniform float bend = 0.2;       // 中线拱起多少（半高的比例）
uniform float width = 0.5;      // 最粗处上半宽（半高的比例），下半宽 0.55 倍
uniform float dry = 0.6;        // 飞白多少
uniform float aspect = 5.0;     // 长 / 高
" + Noise + @"
void fragment() {
	float x = UV.x;
	float y = UV.y * 2.0 - 1.0;
	float yc = -bend * 4.0 * x * (1.0 - x);
	float prof = pow(clamp(sin(3.14159 * pow(x, 0.7)), 0.0, 1.0), 0.65) * width;
	float en = (vnoise(vec2(x * 46.0, 3.0)) - 0.5) * 0.18 + (vnoise(vec2(x * 8.0, 9.0)) - 0.5) * 0.22;
	float top = max(prof * (1.0 + en), 0.0);
	float bot = max(prof * 0.55 * (1.0 + en * 1.6), 0.0);
	float d = y - yc;
	float inside = step(-top, d) * step(d, bot);
	float rel = (d + top) / max(top + bot, 0.0001);                 // 0 = 上沿，1 = 下沿
	// 拉丝飞白：沿笔画方向拉长的噪声，收笔那段和上下边缘断得多
	float streak = vnoise(vec2(x * 3.0 * aspect / 5.0, rel * 22.0)) * 0.65 + vnoise(vec2(x * 14.0, rel * 60.0)) * 0.35;
	float need = dry * (0.2 + 0.5 * smoothstep(0.45, 1.0, x) + 0.4 * smoothstep(0.5, 1.0, abs(rel - 0.4) * 2.0));
	inside *= step(need, streak);
	// 笔肚里的拉丝：一道道暗红的细纹（墨的浓淡），不是一整块平涂
	float fib = step(0.78, vnoise(vec2(x * 2.0 * aspect / 5.0, rel * 38.0)));
	// 刷出来的前沿和褪掉的后沿都是毛的
	float front = x + (vnoise(vec2(rel * 9.0, 5.0)) - 0.5) * 0.06;
	inside *= step(front, reveal) * step(wipe, front);
	vec3 col = mix(vec3(0.9, 0.07, 0.1), vec3(0.55, 0.02, 0.06), fib);
	float core = step(abs(rel - 0.3), 0.05 * (1.0 - x * 0.7)) * step(0.06, x) * step(x, 0.9) * step(0.35, vnoise(vec2(x * 20.0, 1.0)));
	col = mix(col, vec3(1.0, 0.97, 0.95), core);
	col = mix(col, vec3(0.04, 0.0, 0.02), step(0.84 + 0.06 * vnoise(vec2(x * 30.0, 4.0)), rel));
	COLOR = vec4(col, inside * COLOR.a);
}" };

	/// <summary>
	/// 一笔墨笔斩痕：中心 at、方向 ang、长 len、最粗处约 thick 像素；flip = 上下翻（刀从另一侧挥）。0.05s 刷完，life 末尾从起笔褪掉。
	/// </summary>
	public static void Slash(Control layer, Vector2 at, float ang, float len, float thick, float life = 0.4f, float delay = 0f, bool flip = false,
		float bend = -1f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5BrushSlash", life + 0.05);
			root.Rotation = ang;
			root.Scale = new Vector2(1f, flip ? -1f : 1f);
			float h = thick * 1.25f;
			ColorRect r = new()
			{
				Size = new Vector2(len, h * 2f), Position = new Vector2(-len / 2f, -h), MouseFilter = Control.MouseFilterEnum.Ignore,
				Material = new ShaderMaterial { Shader = SlashShader },
			};
			root.AddChild(r);
			ShaderMaterial m = (ShaderMaterial)r.Material;
			m.SetShaderParameter("seed", Fx.Rand(0f, 100f));
			m.SetShaderParameter("bend", bend >= 0f ? bend : Fx.Rand(0.08f, 0.3f));
			m.SetShaderParameter("width", 0.62f);
			m.SetShaderParameter("dry", Fx.Rand(0.45f, 0.75f));
			m.SetShaderParameter("aspect", len / (h * 2f));
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				m.SetShaderParameter("reveal", Ease5.OutExpo(Mathf.Min(1f, s / 0.055f)) * 1.08f);
				m.SetShaderParameter("wipe", Fx.InCubic(Ease5.Seg(t, 0.4f, 0.6f)) * 1.08f);
			});
		});
	}

	// ───────────── 墨迹 ─────────────

	private static Shader? _splat;

	private static Shader SplatShader => _splat ??= new Shader { Code = @"
shader_type canvas_item;
uniform float seed = 0.0;
uniform float prog = 1.0;       // 0→1：墨团鼓出来、尖刺长出去
uniform float fade = 0.0;       // 0→1：从细处蚀掉
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p);
	vec2 dir = p / max(r, 0.0001);
	float n1 = vnoise(dir * 1.6 + 7.0);
	float n2 = vnoise(dir * 7.0 + 3.0);
	float n3 = vnoise(dir * 15.0 + 11.0);
	float bias = 0.25 + 0.75 * max(dir.x, 0.0);                    // 朝 +x 甩得长
	float spike = (pow(n2, 4.0) * 0.75 + pow(n3, 7.0) * 0.5) * bias * Ease(prog);
	float R = 0.2 * (0.7 + 0.7 * n1) * min(1.0, prog * 2.0) + spike;
	float inside = step(r, R);
	// 蚀掉：靠外、靠细的先没
	float dn = vnoise(p * 7.0 + 2.0) * 0.6 + vnoise(p * 19.0) * 0.4;
	inside *= step(fade * 1.15, dn * (1.0 - 0.6 * r / max(R, 0.001)) + 0.25 * (1.0 - fade));
	vec3 col = mix(vec3(0.9, 0.07, 0.1), vec3(0.05, 0.0, 0.02), step(0.7, vnoise(p * 3.5 + 5.0)) * step(0.12, r));
	col = mix(col, vec3(1.0, 0.96, 0.94), step(0.86, vnoise(vec2(atan(p.y, p.x) * 6.0, r * 3.0))) * step(r, R * 0.8) * step(0.15, r));
	COLOR = vec4(col, inside * COLOR.a);
}".Replace("Ease(prog)", "(1.0 - pow(1.0 - prog, 3.0))") };

	/// <summary>命中墨迹：中心 at、朝 ang 甩、半径 size。life 秒：前 0.08s 甩出去，后半段蚀掉。</summary>
	public static void Splat(Control layer, Vector2 at, float ang, float size, float life = 0.5f, float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5BrushSplat", life + 0.05);
			root.Rotation = ang;
			ColorRect r = new()
			{
				Size = new Vector2(size * 2f, size * 2f), Position = new Vector2(-size * 0.55f, -size), MouseFilter = Control.MouseFilterEnum.Ignore,
				Material = new ShaderMaterial { Shader = SplatShader },
			};
			r.Position = new Vector2(-size, -size);
			root.AddChild(r);
			ShaderMaterial m = (ShaderMaterial)r.Material;
			m.SetShaderParameter("seed", Fx.Rand(0f, 100f));
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				m.SetShaderParameter("prog", Mathf.Min(1f, s / 0.08f));
				m.SetShaderParameter("fade", Ease5.Seg(t, 0.35f, 0.65f));
			});
		});
	}

	/// <summary>甩出去的墨点：沿 dir ± spread 飞，速度方向上拉长，越飞越慢、往下坠、缩没。红黑两色。</summary>
	public static void Drops(Control layer, Vector2 at, Vector2 dir, int count, float spread, float speed, float size, float life = 0.45f, float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			Node2D root = Fx.Root(layer, at, "P5BrushDrops", life + 0.05);
			var list = new List<(Node2D N, Vector2 V, float S)>();
			float a0 = dir.Angle();
			for (int i = 0; i < count; i++)
			{
				float a = a0 + Fx.Rand(-spread, spread);
				float s = size * Fx.Rand(0.3f, 1f);
				Node2D n = new() { Rotation = a };
				root.AddChild(n);
				Polygon2D p = Fx.Poly(n, Fx.Circle(s, 10), i % 4 == 0 ? Ink.Black : Ink.Red);
				p.Scale = new Vector2(2.2f, 0.8f);
				list.Add((n, Vector2.Right.Rotated(a) * speed * Fx.Rand(0.35f, 1f), s));
			}
			const float tau = 0.12f;
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				float go = tau * (1f - Mathf.Exp(-s / tau));
				foreach (var (n, v, _) in list)
				{
					n.Position = v * go + new Vector2(0f, 500f * s * s);
					n.Scale = new Vector2(Mathf.Lerp(1.6f, 0.6f, t), 1f) * (1f - Fx.InCubic(Ease5.Seg(t, 0.5f, 0.5f)));
				}
			});
		});
	}

	// ───────────── 玻璃板（防御）─────────────

	private static Shader? _glass;

	private static Shader GlassShader => _glass ??= new Shader { Code = @"
shader_type canvas_item;
uniform float h = 400.0;        // 玻璃板高（像素），反光条按它排
uniform float sweep = -1.0;     // 一道亮反光扫过的位置（-1 → 1）
uniform float glint = 0.0;      // 整块白一下
uniform float alpha = 1.0;
varying vec2 lp;
void vertex() { lp = VERTEX; }
void fragment() {
	vec2 q = lp / h;
	float g = clamp(q.y + 0.5, 0.0, 1.0);
	vec3 col = mix(vec3(0.34, 0.02, 0.06), vec3(0.08, 0.0, 0.02), g);
	float a = mix(0.62, 0.8, g);
	float s = q.x * 1.0 + q.y * 0.55;
	float bands = step(abs(fract(s * 2.2 + 0.15) - 0.5), 0.035) * 0.5 + step(abs(fract(s * 2.2 + 0.31) - 0.5), 0.012) * 0.7;
	float sw = step(abs(s - sweep), 0.05) + step(abs(s - sweep - 0.09), 0.012);
	col = mix(col, vec3(1.0, 0.95, 0.94), clamp(bands * 0.6 + sw + glint, 0.0, 1.0));
	a = max(a, clamp(bands * 0.6 + sw, 0.0, 1.0) * 0.9);
	COLOR = vec4(col, a * alpha * COLOR.a);
}" };

	/// <summary>玻璃板外形（中心原点，高 h）：一块斜切的不规则碎玻璃大板（八边，凸多边形，切碎片时用）。</summary>
	public static Vector2[] PaneShape(float h)
	{
		float w = h * 0.46f;
		return new[]
		{
			new Vector2(-w * 0.3f, -h * 0.5f), new Vector2(w * 0.08f, -h * 0.47f), new Vector2(w * 0.56f, -h * 0.34f),
			new Vector2(w * 0.47f, h * 0.12f), new Vector2(w * 0.5f, h * 0.3f), new Vector2(w * 0.12f, h * 0.5f),
			new Vector2(-w * 0.46f, h * 0.38f), new Vector2(-w * 0.52f, -h * 0.1f),
		};
	}

	private static Vector2[] Loop(Vector2[] pts)
	{
		var o = new Vector2[pts.Length + 1];
		pts.CopyTo(o, 0);
		o[^1] = pts[0];
		return o;
	}

	/// <summary>一块玻璃（多边形 pts，局部坐标）：玻璃面 + 外白边 + 内细红边。返回玻璃面的材质。</summary>
	private static ShaderMaterial Pane(Node2D parent, Vector2[] pts, float h, float rim = 4f)
	{
		ShaderMaterial m = new() { Shader = GlassShader };
		m.SetShaderParameter("h", h);
		parent.AddChild(new Polygon2D { Polygon = pts, Material = m });
		parent.AddChild(new Line2D { Points = Loop(pts), Width = rim, DefaultColor = new Color(1f, 0.96f, 0.95f), JointMode = Line2D.LineJointMode.Sharp });
		var inner = new Vector2[pts.Length];
		Vector2 c = Vector2.Zero;
		foreach (Vector2 v in pts) c += v;
		c /= pts.Length;
		for (int i = 0; i < pts.Length; i++) inner[i] = c + (pts[i] - c) * 0.93f;
		parent.AddChild(new Line2D { Points = Loop(inner), Width = rim * 0.45f, DefaultColor = Ink.Red, JointMode = Line2D.LineJointMode.Sharp });
		return m;
	}

	/// <summary>
	/// 防御玻璃：在 at（中心）立起一块高 h 的玻璃板 —— 从敌人那侧斜着「拍」到位（0.1s，缓出），落定白一下、一道反光扫过；
	/// 停到 crackAt 从 hit 点（板上偏敌人一侧）放射裂开（白裂纹 0.05s 画出来、定格），breakAt 碎开：
	/// 碎片按「环 × 扇区」切开玻璃板的真实多边形，往外炸开、迅速减速、转着落下、缩没。facing = 1 敌人在右。
	/// </summary>
	public static void Glass(Control layer, Vector2 at, float h, float facing, float life = 1.0f, float crackAt = 0.55f, float breakAt = 0.62f)
	{
		Node2D root = Fx.Root(layer, at, "P5Glass", life + 0.1);
		Node2D whole = new();
		root.AddChild(whole);
		Vector2[] shape = PaneShape(h);
		if (facing < 0f)
		{
			for (int i = 0; i < shape.Length; i++) shape[i] = new Vector2(-shape[i].X, shape[i].Y);
			System.Array.Reverse(shape);
		}
		ShaderMaterial m = Pane(whole, shape, h, 5f);
		Vector2 hit = new(h * 0.08f * facing, -h * 0.08f);
		// 裂纹：从 hit 放射 9 道（每道三折），再连两圈
		int slices = 9;
		float a0 = Fx.Rand(0f, Mathf.Tau);
		var ang = new float[slices];
		for (int j = 0; j < slices; j++) ang[j] = a0 + Mathf.Tau * j / slices + Fx.Rand(-0.15f, 0.15f);
		float[] rings = { 0f, h * 0.12f, h * 0.28f, h * 0.5f, h * 1.2f };
		Vector2 P(int ring, int slice)
		{
			float a = ang[(slice % slices + slices) % slices] + (ring % 2) * 0.08f;
			float r = rings[ring] * (ring == 0 ? 0f : Fx.Rand(0.88f, 1.12f));
			return hit + Vector2.Right.Rotated(a) * r;
		}
		var grid = new Vector2[rings.Length, slices];
		for (int r = 0; r < rings.Length; r++) for (int j = 0; j < slices; j++) grid[r, j] = P(r, j);
		var pieces = new List<Vector2[]>();
		for (int r = 0; r < rings.Length - 1; r++)
		{
			for (int j = 0; j < slices; j++)
			{
				int k = (j + 1) % slices;
				Vector2[] cell = r == 0 ? new[] { hit, grid[1, j], grid[1, k] } : new[] { grid[r, j], grid[r + 1, j], grid[r + 1, k], grid[r, k] };
				foreach (Vector2[] piece in Geometry2D.IntersectPolygons(cell, shape))
				{
					pieces.Add(piece);
				}
			}
		}
		Node2D cracks = new() { Visible = false };
		root.AddChild(cracks);
		var crackLines = new List<(Line2D L, Vector2[] Full)>();
		foreach (Vector2[] piece in pieces)
		{
			Vector2[] loop = Loop(piece);
			Line2D l = new() { Width = 2.5f, DefaultColor = new Color(1f, 0.97f, 0.96f), JointMode = Line2D.LineJointMode.Sharp };
			cracks.AddChild(l);
			crackLines.Add((l, loop));
		}
		bool cracked = false, broke = false;
		Fx.Animate(root, 0, life, t =>
		{
			float s = t * life;
			float k = Ease5.OutQuint(Mathf.Min(1f, s / 0.1f));
			if (!broke)
			{
				whole.Position = new Vector2(facing * h * 0.3f * (1f - k), -h * 0.05f * (1f - k));
				whole.Rotation = facing * 0.25f * (1f - k);
				whole.Scale = Vector2.One * Mathf.Lerp(1.12f, 1f, k);
				whole.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, s / 0.03f));
				m.SetShaderParameter("glint", s > 0.09f ? Mathf.Max(0f, 0.8f - (s - 0.09f) / 0.1f) : 0f);
				m.SetShaderParameter("sweep", Mathf.Lerp(-0.9f, 0.9f, Fx.OutCubic(Ease5.Seg(s, 0.1f, 0.22f))));
			}
			if (!cracked && s >= crackAt)
			{
				cracked = true;
				cracks.Visible = true;
			}
			if (cracked && !broke)
			{
				// 裂纹从 hit 往外长出来（按离 hit 的距离截断每条线）
				float reach = h * 1.3f * Ease5.OutExpo(Mathf.Min(1f, (s - crackAt) / 0.05f));
				foreach (var (l, full) in crackLines)
				{
					var pts = new List<Vector2>();
					foreach (Vector2 v in full)
					{
						if ((v - hit).Length() <= reach) pts.Add(v);
					}
					l.Points = pts.ToArray();
				}
			}
			if (!broke && s >= breakAt)
			{
				broke = true;
				whole.Visible = false;
				cracks.Visible = false;
				Sfx.Play(Sfx.Glass, 0.6f);
				Shards(root, pieces, hit, h, facing, life - breakAt);
				Ink.Needles(layer, at + hit, 6, h * 0.45f, 9f, 0.14f);
			}
		});
	}

	private static void Shards(Node2D root, List<Vector2[]> pieces, Vector2 hit, float h, float facing, float total)
	{
		const float tau = 0.12f;
		foreach (Vector2[] piece in pieces)
		{
			Vector2 c = Vector2.Zero;
			foreach (Vector2 v in piece) c += v;
			c /= piece.Length;
			var local = new Vector2[piece.Length];
			for (int i = 0; i < piece.Length; i++) local[i] = piece[i] - c;
			Node2D shard = new() { Position = c };
			root.AddChild(shard);
			ShaderMaterial sm = Pane(shard, local, h, 3f);
			Vector2 dir = (c - hit).Normalized();
			float dist = (c - hit).Length();
			Vector2 vel = (dir * (900f + dist * 3f) + new Vector2(-facing * 300f, Fx.Rand(-300f, 100f))) * Fx.Rand(0.75f, 1.2f);
			float spin = Fx.Rand(-10f, 10f);
			Vector2 p0 = c;
			Fx.Animate(shard, 0, total, t =>
			{
				float s = t * total;
				float go = tau * (1f - Mathf.Exp(-s / tau));
				shard.Position = p0 + vel * go + vel * 0.05f * s + new Vector2(0f, 700f * s * s);
				shard.Rotation = spin * go + spin * 0.1f * s;
				float flick = Mathf.Pow(Mathf.Abs(Mathf.Sin(shard.Rotation * 2f + spin)), 6f);
				sm.SetShaderParameter("glint", Mathf.Clamp(1f - s / 0.1f, 0f, 1f) * 0.6f + 0.5f * flick);
				shard.Scale = Vector2.One * (1f - Fx.InCubic(Ease5.Seg(t, 0.55f, 0.45f)));
			});
		}
	}
}
