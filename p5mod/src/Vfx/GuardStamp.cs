using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 防御（用户 2026-11-08 选的方向 A：P5 漫画式格挡）。出拳护身那一下，拳头前面「啪」地弹开一块硬边的红黑漫画爆炸形，停一下，碎成几条甩走。
/// 用户担心做出来像一张粗糙的贴图，所以：
/// · 不用贴图：形状是代码生成的多边形（每次锯齿都不一样），任何大小都锐利；
/// · 硬边 + 套印感：黑底、红面（屏幕空间网点，往一边加深）、白色内描边三层，红层和黑层错开几个像素；
/// · 「抖」：P5 的 UI 图形一拍二（每两帧换一次形），这里弹出和停住期间每 1/15 秒重新抖一次锯齿；
/// · 有顿挫：从拳头弹出（冲过头再回、带倾斜和压扁）→ 停住轻微呼吸 → 沿斜线裂成几条，带转往外甩、往下掉，最后一下缩没（不是整体淡出）；
/// · 和人连上：拳头上一颗白色星芒 + 几道从拳头往外的速度线。
/// 防御是每场打得最多的牌：整段 0.55s，不顿帧、不震屏，声音用原版格挡音。
/// </summary>
public static class GuardStamp
{
	private static Shader? _halftone;

	/// <summary>红面：底色 + 网点（按屏幕像素排、转 45°），网点从 grad_from 到 grad_to 由无到大。</summary>
	private static Shader Halftone => _halftone ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec4 base : source_color = vec4(0.9, 0.02, 0.09, 1.0);
uniform vec4 ink : source_color = vec4(0.28, 0.0, 0.04, 1.0);
uniform float dot_px = 7.0;
uniform vec2 grad_from = vec2(0.0);
uniform vec2 grad_to = vec2(0.0, 100.0);
varying vec2 lp;
void vertex() { lp = VERTEX; }
void fragment() {
	vec2 d = grad_to - grad_from;
	float g = clamp(dot(lp - grad_from, d) / max(dot(d, d), 1.0), 0.0, 1.0);
	vec2 r = mat2(vec2(0.7071, -0.7071), vec2(0.7071, 0.7071)) * FRAGCOORD.xy / dot_px;
	float dist = length(fract(r) - 0.5);
	float rad = mix(0.0, 0.62, g * g);
	float aa = fwidth(dist) * 0.8;
	float m = 1.0 - smoothstep(rad - aa, rad + aa, dist);
	COLOR = vec4(mix(base.rgb, ink.rgb, m), COLOR.a);
}" };

	private static readonly Color Black = new(0.04f, 0.02f, 0.03f), White = new(1f, 0.97f, 0.95f);

	/// <summary>一块爆炸形：n 个尖，外半径 r（竖着拉长 = 盾形），jitter 抖动；朝 facing 那边多两根长刺。</summary>
	private static Vector2[] Burst(float r, int n, float jitter, float facing, int seed)
	{
		var rng = new RandomNumberGenerator { Seed = (ulong)seed };
		var pts = new List<Vector2>();
		for (int i = 0; i < n; i++)
		{
			float a0 = Mathf.Tau * i / n - Mathf.Pi / 2f;
			float aOut = a0 + rng.RandfRange(-0.12f, 0.12f);
			float aIn = a0 + Mathf.Pi / n + rng.RandfRange(-0.08f, 0.08f);
			Vector2 dirOut = Vector2.FromAngle(aOut), dirIn = Vector2.FromAngle(aIn);
			float lean = Mathf.Max(0f, dirOut.X * facing);                     // 朝外那边的刺更长（被打出来的方向）
			float outR = r * (0.88f + rng.RandfRange(-jitter, jitter) + 0.32f * lean * lean);
			float inR = r * (0.6f + rng.RandfRange(-jitter, jitter) * 0.6f);
			pts.Add(new Vector2(dirOut.X * 0.74f, dirOut.Y) * outR);
			pts.Add(new Vector2(dirIn.X * 0.74f, dirIn.Y) * inR);
		}
		return pts.ToArray();
	}

	private static Vector2[] Scale(Vector2[] p, float k)
	{
		var o = new Vector2[p.Length];
		for (int i = 0; i < p.Length; i++) o[i] = p[i] * k;
		return o;
	}

	private sealed class Layers
	{
		public readonly Node2D Root = new();
		public readonly Polygon2D Back, Face;
		public readonly Line2D Rim;

		public Layers(Node parent, ShaderMaterial face)
		{
			parent.AddChild(Root);
			Back = new Polygon2D { Color = Black, Antialiased = true };
			Face = new Polygon2D { Color = Colors.White, Material = face, Antialiased = true };
			Rim = new Line2D { DefaultColor = White, Closed = true, JointMode = Line2D.LineJointMode.Sharp, Antialiased = true };
			Root.AddChild(Back);
			Root.AddChild(Face);
			Root.AddChild(Rim);
		}

		public void Set(Vector2[] back, Vector2[] face, Vector2[] rim, Vector2 misreg, float rimWidth)
		{
			Back.Polygon = back;
			Back.Position = misreg;
			Face.Polygon = face;
			Rim.Points = rim;
			Rim.Width = rimWidth;
		}
	}

	/// <param name="fist">护身的拳头（全局坐标）</param>
	/// <param name="height">Joker 身高（屏幕像素），决定大小</param>
	/// <param name="amount">格挡值：10 点以上更大、多一圈速度线</param>
	public static void Play(Control layer, Vector2 fist, float height, float facing = 1f, int amount = 5)
	{
		bool big = amount >= 10;
		float r = height * (big ? 0.27f : 0.22f);
		Vector2 center = fist + new Vector2(height * 0.4f * facing, height * 0.14f);    // 挡在身前、不盖住脸和上身
		float unit = Fx.Screen(layer).Y / 1080f;
		int seed = (int)(Fx.Rng.NextDouble() * 100000);
		const float appear = 0.07f, breakAt = 0.3f, life = 0.58f;

		Node2D root = Fx.Root(layer, center, "P5GuardStamp", life + 0.1);
		ShaderMaterial face = new() { Shader = Halftone };
		face.SetShaderParameter("dot_px", 7f * unit);
		face.SetShaderParameter("grad_from", new Vector2(-r * 0.5f * facing, -r * 0.6f));
		face.SetShaderParameter("grad_to", new Vector2(r * 0.7f * facing, r * 0.8f));
		Layers whole = new(root, face);
		Vector2 misreg = new Vector2(5f * facing, 4f) * unit;
		int frame = -1;
		Vector2[] shapeBack = Burst(r * 1.12f, 11, 0.06f, facing, seed + 7), shapeFace = Burst(r, 11, 0.06f, facing, seed);
		void Shape(int step)
		{
			shapeBack = Burst(r * 1.12f, 11, 0.06f, facing, seed + 7 + step * 31);
			shapeFace = Burst(r, 11, 0.06f, facing, seed + step * 31);
			whole.Set(shapeBack, shapeFace, Scale(Burst(r, 11, 0.04f, facing, seed + step * 31), 0.8f), misreg, 2.6f * unit);
		}
		Shape(0);

		// 拳头：白色四角星一闪 + 速度线
		Node2D spark = new() { Position = fist - center };
		root.AddChild(spark);
		Polygon2D star = Fx.Poly(spark, Glow.Star4(46f * unit, 0.16f), White);
		Polygon2D starInk = Fx.Poly(spark, Glow.Star4(58f * unit, 0.2f), Black);
		spark.MoveChild(starInk, 0);
		int lines = big ? 9 : 6;
		var streaks = new List<(Polygon2D poly, Vector2 dir, float len)>();
		for (int i = 0; i < lines; i++)
		{
			float a = Fx.Rand(-0.75f, 0.75f) + (facing > 0 ? 0f : Mathf.Pi);
			Vector2 dir = Vector2.FromAngle(a);
			Polygon2D s = new() { Color = i % 3 == 0 ? Black : White, Antialiased = true };
			root.AddChild(s);
			root.MoveChild(s, 0);
			streaks.Add((s, dir, height * Fx.Rand(0.35f, 0.6f)));
		}

		// 碎开用的斜切线（垂直于往外甩的方向），把形切成 4～5 条
		Fx.Animate(root, 0, life, t =>
		{
			float s = t * life;
			// ── 弹出 + 停住：每 1/15s 抖一次锯齿；冲过头再回（1.2 → 0.94 → 1），带倾斜
			if (s < breakAt)
			{
				int step = (int)(s * 15f);
				if (step != frame)
				{
					frame = step;
					Shape(step);
				}
				float k = Mathf.Clamp(s / appear, 0f, 1f);
				float pop = s < appear ? Mathf.Lerp(0.15f, 1.2f, Fx.OutCubic(k))
					: 1f + 0.2f * Mathf.Exp(-(s - appear) / 0.035f) * Mathf.Cos((s - appear) * 40f);
				float breath = 1f + 0.015f * Mathf.Sin(s * 30f);
				whole.Root.Scale = new Vector2(pop * breath * (1f + 0.12f * (1f - k)), pop * breath * (1f - 0.1f * (1f - k)));
				whole.Root.Skew = -0.3f * facing * (1f - Fx.OutCubic(k));
				whole.Root.Rotation = 0.06f * facing * (1f - k);
				whole.Root.Position = (fist - center) * (1f - Fx.OutCubic(k));
			}
			// 拳头星芒：0.12s 一闪
			float sk = Mathf.Clamp(s / 0.12f, 0f, 1f);
			spark.Visible = sk < 1f;
			spark.Scale = Vector2.One * Mathf.Sin(sk * Mathf.Pi) * (1.2f - 0.4f * sk);
			spark.Rotation = sk * 0.6f;
			// 速度线：0.05s 射出，0.12s 缩回拳头外端
			foreach ((Polygon2D poly, Vector2 dir, float len) in streaks)
			{
				float grow = Fx.OutCubic(Mathf.Clamp(s / 0.05f, 0f, 1f)), shrink = Fx.InCubic(Mathf.Clamp((s - 0.06f) / 0.12f, 0f, 1f));
				poly.Visible = shrink < 1f;
				Vector2 a = fist - center + dir * (len * 0.25f + len * 0.75f * shrink), b = fist - center + dir * len * (0.25f + 0.75f * grow);
				Vector2 n = dir.Orthogonal() * 3.2f * unit * (1f - shrink);
				poly.Polygon = new[] { a, b + n, b - n };
			}
		});

		// ── 碎开：沿几条斜线切开，每条带转往外甩、往下掉，最后 0.08s 缩没
		Fx.After(root, breakAt, () =>
		{
			if (!GodotObject.IsInstanceValid(root))
			{
				return;
			}
			whole.Root.Visible = false;
			Transform2D xf = whole.Root.Transform;
			Vector2 cutDir = new Vector2(0.55f * facing, -1f).Normalized(), across = cutDir.Orthogonal();
			int strips = big ? 6 : 5;
			float span = r * 1.9f;
			var offsets = new List<float>();
			for (int i = 1; i < strips; i++) offsets.Add(-span / 2f + span * i / strips + Fx.Rand(-0.08f, 0.08f) * span / strips);
			for (int i = 0; i < strips; i++)
			{
				float lo = i == 0 ? -span : offsets[i - 1], hi = i == strips - 1 ? span : offsets[i];
				Vector2[] band =
				{
					across * lo + cutDir * span, across * hi + cutDir * span * 0.9f, across * hi - cutDir * span, across * lo - cutDir * span * 1.1f,
				};
				var pieces = new List<(Vector2[] back, Vector2[] face)>();
				Godot.Collections.Array<Vector2[]> backs = Geometry2D.IntersectPolygons(shapeBack, band);
				Godot.Collections.Array<Vector2[]> faces = Geometry2D.IntersectPolygons(shapeFace, band);
				if (faces.Count == 0 && backs.Count == 0)
				{
					continue;
				}
				// 每条绕自己的中心转（不是绕整块的中心，不然几条一起公转、看着还是一坨）
				Vector2 c = Vector2.Zero;
				int cn = 0;
				foreach (Vector2[] f in faces.Count > 0 ? faces : backs)
				{
					foreach (Vector2 v in f) { c += v; cn++; }
				}
				c /= Mathf.Max(1, cn);
				Vector2[] Shift(Vector2[] src) { var o = new Vector2[src.Length]; for (int j = 0; j < src.Length; j++) o[j] = src[j] - c; return o; }
				Node2D strip = new() { Transform = new Transform2D(xf.X, xf.Y, xf * c) };
				root.AddChild(strip);
				foreach (Vector2[] b in backs)
				{
					strip.AddChild(new Polygon2D { Polygon = Shift(b), Color = Black, Position = misreg, Antialiased = true });
				}
				// 网点的渐变是按局部坐标算的：每条挪了中心，渐变也跟着挪回去，拼起来和碎之前一样
				ShaderMaterial sf = (ShaderMaterial)face.Duplicate();
				sf.SetShaderParameter("grad_from", (Vector2)face.GetShaderParameter("grad_from") - c);
				sf.SetShaderParameter("grad_to", (Vector2)face.GetShaderParameter("grad_to") - c);
				foreach (Vector2[] f in faces)
				{
					strip.AddChild(new Polygon2D { Polygon = Shift(f), Color = Colors.White, Material = sf, Antialiased = true });
					strip.AddChild(new Line2D { Points = Shift(f), Closed = true, Width = 2f * unit, DefaultColor = White, JointMode = Line2D.LineJointMode.Sharp,
						Antialiased = true });
				}
				float side = (lo + hi) * 0.5f / span;
				// 每条往两边散开（越靠边散得越开）+ 整体往外，速度、转速各不相同，不成一坨一起走
				Vector2 vel = (across * side * 4.5f + new Vector2(0.8f * facing, -0.3f) + new Vector2(Fx.Rand(-0.4f, 0.4f), Fx.Rand(-0.4f, 0.4f)))
					.Normalized() * height * Fx.Rand(2.4f, 4.2f);
				float spin = Fx.Rand(4f, 11f) * (side >= 0 ? 1f : -1f) * facing;
				Vector2 p0 = strip.Position;
				float rot0 = strip.Rotation;
				Vector2 scale0 = strip.Scale;
				float dur = life - breakAt;
				Fx.Animate(strip, 0, dur, t =>
				{
					float s = t * dur;
					float burst = 0.12f * (1f - Mathf.Exp(-s / 0.12f));
					strip.Position = p0 + vel * burst + new Vector2(0f, height * 2.4f * s * s);
					strip.Rotation = rot0 + spin * burst * 2f;
					float end = Mathf.Clamp((s - (dur - 0.08f)) / 0.08f, 0f, 1f);
					strip.Scale = scale0 * (1f - Fx.InCubic(end));
				});
			}
		});
	}
}
