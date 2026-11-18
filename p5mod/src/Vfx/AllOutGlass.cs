using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 总攻击的「屏幕真的裂开」（用户 2026-11-18）：
/// · 裂纹是 Codex 画的 P5 风格黑色裂纹（art/p5/fx/gen_codex/allout_cracks_v2，参照原版收尾卡上的碎星），
///   tools/p5/allout_native/trace_cracks.py 把图里的主裂纹描成折线（res://p5joker/allout/cracks_*.json）；
/// · 画面沿这些主裂纹切成扇形的几块（Pane）：每块是一个读屏的多边形（内容跟着这块走，慢放的游戏画面在块里照常动），
///   裂一段 → 顿一下 → 再裂一段：每一下裂纹贴图按离撞击点的距离一口气显出一圈，被切开的块沿裂缝错开几像素、各自歪一点、明暗不同；
///   块后面垫黑底，错开时露出来的缝是黑的，正好和黑色裂纹连成一片；
/// · 碎开时把整屏截下来，每块再按离撞击点的几圈切成小片，从「错开后的位置」直接飞出去 —— 碎片和裂纹是同一套几何。
/// </summary>
public static partial class AllOut
{
	/// <summary>读 res://p5joker/allout/ 下的文本（预览工具换成仓库里的文件）。</summary>
	public static Func<string, string?> LoadText = name =>
	{
		using Godot.FileAccess? f = Godot.FileAccess.Open($"res://p5joker/allout/{name}", Godot.FileAccess.ModeFlags.Read);
		return f?.GetAsText();
	};

	// 裂的三下：时间（相对总攻击开始）、这一下裂到离撞击点多远（裂纹画布像素，画布 2048、撞击点在中心）
	private static readonly float[] CrackBeats = { CrackAt, CrackAt + 0.26f, CrackAt + 0.5f };
	private static readonly float[] CrackRadii = { 360f, 700f, 1450f };
	private const float BeatSnap = 0.06f;
	// 每一下之后各块错开多少（像素）、歪多少（度）、明暗差多少
	private static readonly float[] PaneShift = { 1.5f, 5f, 11f };
	private static readonly float[] PaneTilt = { 0.05f, 0.25f, 0.7f };
	private static readonly float[] PaneShade = { 0.03f, 0.07f, 0.13f };

	private sealed class Pane
	{
		public Polygon2D Poly = null!;
		public ShaderMaterial M = null!;
		public Vector2[] Pts = Array.Empty<Vector2>();   // 屏幕坐标（未错开）
		public Vector2 Pivot, Dir;
		public float TiltSign, ShadeSign;
		public Vector2 Off;
		public float Rot;
		public Transform2D Xform => new Transform2D(Rot, Pivot + Off) * new Transform2D(0f, -Pivot);
	}

	private sealed class Glass
	{
		public readonly List<Pane> Panes = new();
		public readonly List<Vector2[]> Lines = new();     // 主裂纹（屏幕坐标，从撞击点往外）
		public Sprite2D? Art, Glow;
		public ShaderMaterial ArtM = null!, GlowM = null!;
		public float Scale;
		public Vector2 Focus;
		public readonly List<(Polygon2D P, float At, Vector2 Pos, float Size)> Glints = new();
		// 主裂纹冲到屏幕边的延长段（第三下裂开时长出去）：黑色楔形 + 一侧断续的白边，正好压在块与块的缝上
		public readonly List<(Polygon2D Ink, Polygon2D Halo, Line2D Lit, Vector2 From, Vector2 Dir, float Len, float W0, float Delay)> Exts = new();
		public float GlowAmount;
		public readonly List<(Polygon2D P, float At, Vector2 From, Vector2 Vel, float Spin)> Chips = new();

		public void Param(string name, Variant v)
		{
			foreach (Pane p in Panes) p.M.SetShaderParameter(name, v);
		}
	}

	/// <summary>建裂开的玻璃：under 里放黑底和各块（读屏），over 里放裂纹贴图、红光、反光、玻璃屑。数据缺失时只铺一整块（没有裂纹）。</summary>
	private static Glass BuildGlass(Node2D under, Node2D over, Vector2 screen, Vector2 focus, RandomNumberGenerator rng)
	{
		Glass g = new() { Focus = focus };
		// 先把此刻的屏幕存一份（各块读的是这份），再垫黑底 —— 否则读屏会读到已经盖上去的黑底
		under.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		under.AddChild(new Polygon2D { Polygon = new[] { new Vector2(-50f, -50f), new Vector2(screen.X + 50f, -50f), screen + new Vector2(50f, 50f), new Vector2(-50f, screen.Y + 50f) }, Color = new Color(0.02f, 0.01f, 0.015f) });
		Texture2D? art = Stage.LoadTexture("aoa_crackp5_a");
		string? json = LoadText("cracks_a.json");
		float D = 0f;
		foreach (Vector2 c in new[] { Vector2.Zero, new Vector2(screen.X, 0f), new Vector2(0f, screen.Y), screen }) D = Mathf.Max(D, c.DistanceTo(focus));
		g.Scale = D / 1500f;                                                    // 裂纹本身的粗细合适；没冲到屏幕边的地方，块与块之间是一道细缝
		float rot = rng.RandfRange(-0.5f, 0.5f);
		var rawLines = new List<Vector2[]>();
		Vector2 center = new(1024f, 1024f);
		if (json != null && art != null && Json.ParseString(json) is Variant v && v.VariantType == Variant.Type.Dictionary)
		{
			var d = v.AsGodotDictionary();
			var cc = d["center"].AsGodotArray();
			center = new Vector2((float)cc[0].AsDouble(), (float)cc[1].AsDouble());
			foreach (Variant crack in d["cracks"].AsGodotArray())
			{
				var pts = new List<Vector2>();
				foreach (Variant p in crack.AsGodotArray())
				{
					var a = p.AsGodotArray();
					Vector2 q = new((float)a[0].AsDouble(), (float)a[1].AsDouble());
					pts.Add(focus + ((q - center) * g.Scale).Rotated(rot));
				}
				if (pts.Count >= 2) rawLines.Add(pts.ToArray());
			}
		}
		rawLines.Sort((a, b) => Ang(a[^1] - focus).CompareTo(Ang(b[^1] - focus)));
		g.Lines.AddRange(rawLines);
		// 扇形：相邻两条主裂纹 + 各自往外延长到很远 + 远处的一段弧
		const float Far = 4200f;
		int n = g.Lines.Count;
		if (n < 2)
		{
			AddPane(g, under, new[] { new Vector2(-60f, -60f), new Vector2(screen.X + 60f, -60f), screen + new Vector2(60f, 60f), new Vector2(-60f, screen.Y + 60f) }, focus, screen, rng);
		}
		for (int i = 0; i < n && n >= 2; i++)
		{
			Vector2[] A = g.Lines[i], B = g.Lines[(i + 1) % n];
			Vector2 extA = A[^1] + (A[^1] - focus).Normalized() * Far, extB = B[^1] + (B[^1] - focus).Normalized() * Far;
			float a0 = Ang(extA - focus), a1 = Ang(extB - focus);
			if (a1 <= a0) a1 += Mathf.Tau;
			var poly = new List<Vector2>();
			poly.AddRange(A);
			poly.Add(extA);
			for (int k = 1; k < 8; k++) poly.Add(focus + Vector2.FromAngle(Mathf.Lerp(a0, a1, k / 8f)) * (Far + 600f));
			poly.Add(extB);
			for (int k = B.Length - 1; k >= 1; k--) poly.Add(B[k]);
			AddPane(g, under, poly.ToArray(), focus, screen, rng);
		}
		// 延长段：从每条主裂纹的末端沿块缝（撞击点 → 末端的方向）一直冲出屏幕
		foreach (Vector2[] line in g.Lines)
		{
			Vector2 end = line[^1], dir = (end - focus).Normalized();
			float len = 0f;
			for (float t = 0f; t < 4000f; t += 20f)
			{
				Vector2 q = end + dir * t;
				len = t;
				if (q.X < -80f || q.Y < -80f || q.X > screen.X + 80f || q.Y > screen.Y + 80f) break;
			}
			if (len < 30f) continue;
			Polygon2D halo = new() { Color = new Color(0.9f, 0.05f, 0.1f, 0f), Antialiased = true, Visible = false };
			Polygon2D ink = new() { Color = new Color(0.02f, 0.01f, 0.015f), Antialiased = true, Visible = false };
			Line2D lit = new() { Width = 1.8f, DefaultColor = new Color(1f, 0.95f, 0.95f, 0.9f), Antialiased = true, Visible = false };
			over.AddChild(halo);
			over.AddChild(ink);
			over.AddChild(lit);
			g.Exts.Add((ink, halo, lit, end, dir, len, rng.RandfRange(7f, 10f), rng.RandfRange(0f, 0.05f)));
		}
		// 裂纹贴图（在各块上面）：红光（加色、模糊）+ 黑色裂纹本体，按离撞击点的距离一圈圈显出来
		if (art != null)
		{
			g.GlowM = new ShaderMaterial { Shader = CrackGlowShader };
			g.Glow = new Sprite2D { Texture = art, Material = g.GlowM, Position = focus, Rotation = rot, Scale = Vector2.One * g.Scale };
			over.AddChild(g.Glow);
			g.ArtM = new ShaderMaterial { Shader = CrackShader };
			g.Art = new Sprite2D { Texture = art, Material = g.ArtM, Position = focus, Rotation = rot, Scale = Vector2.One * g.Scale };
			over.AddChild(g.Art);
		}
		// 每一下裂开：新裂到的那一圈裂纹上闪几颗玻璃反光、崩出几片玻璃屑
		for (int b = 0; b < CrackBeats.Length; b++)
		{
			float r0 = (b == 0 ? 0f : CrackRadii[b - 1]) * g.Scale, r1 = CrackRadii[b] * g.Scale;
			for (int k = 0; k < 7; k++)
			{
				if (g.Lines.Count == 0) break;
				Vector2 at = PointAt(g.Lines[rng.RandiRange(0, g.Lines.Count - 1)], focus, Mathf.Lerp(r0, r1, rng.RandfRange(0.4f, 1f)));
				Polygon2D gl = new() { Color = Colors.White, Antialiased = true, Polygon = StarPoly(1f, 0.12f), Visible = false };
				over.AddChild(gl);
				g.Glints.Add((gl, CrackBeats[b] + rng.RandfRange(0f, 0.08f), at, rng.RandfRange(10f, 22f)));
				float ca = rng.RandfRange(0f, Mathf.Tau), cr = rng.RandfRange(3f, 8f);
				Polygon2D chip = new()
				{
					Color = new Color(1f, 1f, 1f, 0.9f), Antialiased = true, Visible = false,
					Polygon = new[] { Vector2.FromAngle(ca) * cr, Vector2.FromAngle(ca + 2.1f) * cr * rng.RandfRange(0.6f, 1.2f), Vector2.FromAngle(ca + 4.2f) * cr * rng.RandfRange(0.6f, 1.2f) },
				};
				over.AddChild(chip);
				g.Chips.Add((chip, CrackBeats[b] + rng.RandfRange(0f, 0.04f), at, Vector2.FromAngle(rng.RandfRange(0f, Mathf.Tau)) * rng.RandfRange(60f, 180f), rng.RandfRange(-6f, 6f)));
			}
		}
		return g;
	}

	private static void AddPane(Glass g, Node2D under, Vector2[] pts, Vector2 focus, Vector2 screen, RandomNumberGenerator rng)
	{
		// 支点 = 扇形中线上离撞击点 350px 的地方（错开、倾斜都绕这里）
		Vector2 mid = Vector2.Zero;
		foreach (Vector2 p in pts) mid += (p - focus).Normalized();
		Vector2 dir = mid.LengthSquared() > 1e-4f ? mid.Normalized() : Vector2.Right;
		Vector2 pivot = focus + dir * 350f;
		var local = new Vector2[pts.Length];
		for (int i = 0; i < pts.Length; i++) local[i] = pts[i] - pivot;
		ShaderMaterial m = new() { Shader = PaneShader };
		m.SetShaderParameter("origin", pivot);
		m.SetShaderParameter("screen", screen);
		m.SetShaderParameter("focus", focus / screen);
		m.SetShaderParameter("aspect", screen.X / screen.Y);
		Polygon2D poly = new() { Polygon = local, Material = m, Position = pivot, Antialiased = false };
		under.AddChild(poly);
		g.Panes.Add(new Pane { Poly = poly, M = m, Pts = pts, Pivot = pivot, Dir = dir, TiltSign = rng.Randf() < 0.5f ? -1f : 1f, ShadeSign = rng.RandfRange(-1f, 1f) });
	}

	private static float Ang(Vector2 v) => Mathf.Atan2(v.Y, v.X);

	/// <summary>折线上离 focus 最先达到 dist 的点。</summary>
	private static Vector2 PointAt(Vector2[] line, Vector2 focus, float dist)
	{
		for (int i = 1; i < line.Length; i++)
		{
			float d0 = line[i - 1].DistanceTo(focus), d1 = line[i].DistanceTo(focus);
			if (d1 >= dist && d0 <= dist) return line[i - 1].Lerp(line[i], (dist - d0) / Mathf.Max(0.001f, d1 - d0));
		}
		return line[^1];
	}

	/// <summary>每帧更新：裂到哪一圈、各块错开多少、反光和玻璃屑。tremble = 抖动（像素）。</summary>
	private static void UpdateGlass(Glass g, float T, Vector2 screen, float tremble)
	{
		// 现在裂到第几下（每一下 BeatSnap 秒内一口气裂开）
		float reach = 0f, shift = 0f, tilt = 0f, shade = 0f, jolt = 0f;
		for (int b = 0; b < CrackBeats.Length; b++)
		{
			float k = Mathf.Clamp((T - CrackBeats[b]) / BeatSnap, 0f, 1f);
			if (k <= 0f) break;
			float prevR = b == 0 ? 60f : CrackRadii[b - 1];
			reach = Mathf.Lerp(prevR, CrackRadii[b], Quint(k));
			float pk = Back(k, 1.6f);
			shift = Mathf.Lerp(b == 0 ? 0f : PaneShift[b - 1], PaneShift[b], pk);
			tilt = Mathf.Lerp(b == 0 ? 0f : PaneTilt[b - 1], PaneTilt[b], pk);
			shade = Mathf.Lerp(b == 0 ? 0f : PaneShade[b - 1], PaneShade[b], k);
			float since = T - CrackBeats[b];
			if (since < 0.12f) jolt = Mathf.Max(jolt, (1f - since / 0.12f) * (5f + 3f * b));
		}
		if (g.Art != null)
		{
			g.ArtM.SetShaderParameter("reach", reach / 2048f);
			g.ArtM.SetShaderParameter("glow", 0f);
			// 红光：第三下之后越来越亮，碎开前一下涨满
			float pre = Mathf.Clamp((T - CrackBeats[^1]) / (ShatterAt - CrackBeats[^1]), 0f, 1f);
			g.GlowM.SetShaderParameter("reach", reach / 2048f);
			g.GlowAmount = 0.06f + 0.22f * pre + 0.5f * Mathf.Pow(pre, 4f);
			g.GlowM.SetShaderParameter("amount", g.GlowAmount);
		}
		Vector2 shake = new Vector2(Mathf.Sin(T * 97f), Mathf.Cos(T * 83f)) * (tremble + jolt);
		g.Param("shift", shake / screen);
		foreach (Pane p in g.Panes)
		{
			p.Off = p.Dir * shift;
			p.Rot = Mathf.DegToRad(tilt * p.TiltSign);
			p.Poly.Position = p.Pivot + p.Off + shake;
			p.Poly.Rotation = p.Rot;
			p.M.SetShaderParameter("shade", 1f + shade * p.ShadeSign);
		}
		// 延长段：第三下裂开时 0.12s 内冲到屏幕边（先快后慢），之后跟着震
		float ek0 = T - CrackBeats[^1];
		foreach (var e in g.Exts)
		{
			float ek = Mathf.Clamp((ek0 - e.Delay) / 0.12f, 0f, 1f);
			e.Ink.Visible = e.Lit.Visible = e.Halo.Visible = ek > 0f;
			if (ek <= 0f) continue;
			float L = e.Len * Quint(ek);
			Vector2 n = e.Dir.Orthogonal(), a = e.From + shake, b = a + e.Dir * L;
			float w1 = Mathf.Lerp(e.W0, 2f, Mathf.Clamp(L / 1100f, 0f, 1f));
			e.Ink.Polygon = new[] { a + n * e.W0 * 0.5f, b + n * w1 * 0.5f, b + e.Dir * 6f, b - n * w1 * 0.5f, a - n * e.W0 * 0.5f };
			float hw0 = e.W0 * 2.6f, hw1 = w1 * 3f;          // 和裂纹贴图一样渗红光
			e.Halo.Polygon = new[] { a + n * hw0 * 0.5f, b + n * hw1 * 0.5f, b - n * hw1 * 0.5f, a - n * hw0 * 0.5f };
			e.Halo.Color = new Color(0.9f, 0.05f, 0.1f, Mathf.Clamp(0.25f + g.GlowAmount * 0.9f, 0f, 0.85f));
			// 白边：只在一侧、断成几段
			e.Lit.Points = new[] { a + n * e.W0 * 0.5f + e.Dir * L * 0.08f, a + n * Mathf.Lerp(e.W0, w1, 0.35f) * 0.5f + e.Dir * L * 0.35f };
		}
		if (g.Art != null)
		{
			g.Art.Position = g.Focus + shake;
			if (g.Glow != null) g.Glow.Position = g.Focus + shake;
		}
		foreach (var gl in g.Glints)
		{
			float t = (T - gl.At) / 0.18f;
			gl.P.Visible = t >= 0f && t < 1f;
			if (!gl.P.Visible) continue;
			gl.P.Position = gl.Pos + shake;
			gl.P.Scale = Vector2.One * gl.Size * Mathf.Sin(t * Mathf.Pi);
			gl.P.Rotation = 0.4f + t * 0.8f;
		}
		foreach (var ch in g.Chips)
		{
			float t = T - ch.At;
			ch.P.Visible = t >= 0f && t < 0.7f;
			if (!ch.P.Visible) continue;
			ch.P.Position = ch.From + ch.Vel * t + new Vector2(0f, 300f) * t * t;
			ch.P.Rotation = ch.Spin * t;
			ch.P.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((t - 0.4f) / 0.3f, 0f, 1f));
		}
	}

	/// <summary>碎开：每块按离撞击点的几圈切成小片，贴上整屏截图，从各块此刻（错开后）的位置出发。</summary>
	private static List<Shard> BreakGlass(Glass g, Node2D layer, Image shot, Vector2 screen, RandomNumberGenerator rng)
	{
		var list = new List<Shard>();
		ImageTexture tex = ImageTexture.CreateFromImage(shot);
		Vector2 k = new Vector2(shot.GetWidth(), shot.GetHeight()) / screen;
		Vector2[] view = { new(-40f, -40f), new(screen.X + 40f, -40f), screen + new Vector2(40f, 40f), new(-40f, screen.Y + 40f) };
		float[] rings = { 230f, 520f, 900f, 1500f };
		foreach (Pane p in g.Panes)
		{
			var pieces = new List<Vector2[]>();
			foreach (Vector2[] inView in Geometry2D.IntersectPolygons(p.Pts, view))
			{
				var rest = new List<Vector2[]> { inView };
				foreach (float r in rings)
				{
					Vector2[] disc = Disc(g.Focus, r * rng.RandfRange(0.9f, 1.1f), 28);
					var next = new List<Vector2[]>();
					foreach (Vector2[] part in rest)
					{
						foreach (Vector2[] inner in Geometry2D.IntersectPolygons(part, disc)) pieces.Add(inner);
						foreach (Vector2[] outer in Geometry2D.ClipPolygons(part, disc)) next.Add(outer);
					}
					rest = next;
				}
				pieces.AddRange(rest);
			}
			Transform2D xf = p.Xform;
			foreach (Vector2[] piece in pieces)
			{
				if (piece.Length < 3) continue;
				var shown = new Vector2[piece.Length];
				Vector2 c = Vector2.Zero;
				for (int i = 0; i < piece.Length; i++) { shown[i] = xf * piece[i]; c += shown[i]; }
				c /= piece.Length;
				var local = new Vector2[piece.Length];
				var uv = new Vector2[piece.Length];
				for (int i = 0; i < piece.Length; i++) { local[i] = shown[i] - c; uv[i] = shown[i] * k; }
				float dist = c.DistanceTo(g.Focus);
				Shard sh = new()
				{
					Center = c, Dir = (c - g.Focus).LengthSquared() > 1f ? (c - g.Focus).Normalized() : Vector2.Up,
					Speed = (600f + 1500f * Mathf.Clamp(1f - dist / 1800f, 0.2f, 1f)) * rng.RandfRange(0.8f, 1.25f),
					Spin = rng.RandfRange(-2.4f, 2.4f), Delay = Mathf.Clamp(dist / 9000f, 0f, 0.04f),
					Grow = rng.Randf() < 0.45f ? rng.RandfRange(0.7f, 1.6f) : rng.RandfRange(0.1f, 0.45f),
					Back = new Polygon2D { Polygon = local, Color = new Color(0.22f, 0f, 0.03f), Antialiased = true, Visible = false },
					Face = new Polygon2D { Polygon = local, Texture = tex, UV = uv, Antialiased = true, Visible = false },
					Edge = new Line2D { Points = local, Closed = true, Width = 1.2f, DefaultColor = new Color(1f, 0.95f, 0.95f, 0.7f), Antialiased = true, Visible = false },
				};
				layer.AddChild(sh.Back);
				layer.AddChild(sh.Face);
				layer.AddChild(sh.Edge);
				list.Add(sh);
			}
		}
		return list;
	}

	private static Vector2[] Disc(Vector2 c, float r, int n)
	{
		var p = new Vector2[n];
		for (int i = 0; i < n; i++) p[i] = c + Vector2.FromAngle(Mathf.Tau * i / n) * r;
		return p;
	}

	private static Shader? _pane;

	/// <summary>
	/// 一块玻璃：读屏，内容跟着这块走（顶点着色器把未错开时的屏幕坐标传下去）；带顿帧效果（冲击帧黑白反转 / 压暗去色收暗 / 色散）和这一块自己的明暗。
	/// </summary>
	private static Shader PaneShader => _pane ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D scr : hint_screen_texture, filter_linear;
uniform vec2 origin = vec2(0.0);
uniform vec2 screen = vec2(1920.0, 1080.0);
uniform float impact = 0.0;
uniform float hold = 0.0;
uniform vec2 focus = vec2(0.6, 0.5);
uniform float aspect = 1.7778;
uniform vec2 shift = vec2(0.0);
uniform float shade = 1.0;
varying vec2 orig;
void vertex() {
	orig = VERTEX + origin;
}
void fragment() {
	vec2 uv = orig / screen + shift;
	vec2 d = (uv - focus) * vec2(aspect, 1.0);
	float r = length(d);
	vec2 ca = (uv - focus) * 0.006 * hold * smoothstep(0.0, 0.5, r);
	vec3 c = vec3(texture(scr, uv + ca).r, texture(scr, uv).g, texture(scr, uv - ca).b);
	float l = dot(c, vec3(0.299, 0.587, 0.114));
	vec3 held = mix(c, vec3(l), 0.6) * mix(1.0, 0.62, hold);
	held *= 1.0 - hold * 0.55 * smoothstep(0.35, 1.2, r);
	vec3 o = mix(c, held, hold);
	float hi = smoothstep(0.32, 0.38, l);
	o = mix(o, vec3(1.0 - hi), impact);
	COLOR = vec4(o * shade, 1.0);
}" };
}
