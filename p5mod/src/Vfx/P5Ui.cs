using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 选人界面 / 介绍板用的界面小工具（只用 Godot 类型，预览工具也能编译）：
/// · 字体：Barlow Condensed 粗斜体（mod 自带，SIL OFL，res://p5joker/fonts/，原样文件用 LoadDynamicFont 读，不需要导入）、
///   中文用游戏自带的 Noto Sans CJK（res://fonts/jpn/…，同结城理 mod）。预览工具里换成本地文件（Load 可替换）。
/// · Quad：抗锯齿的凸四边形（着色器算到各边的距离；原生 Polygon2D 的斜边有锯齿）。
/// · Text / Gradient：文字标签、上下渐变的文字材质（同结城理 mod 的 Typo）。
/// </summary>
public static class P5Ui
{
	public const string Barlow = "res://p5joker/fonts/BarlowCondensed-900Italic.ttf";
	public const string BarlowBold = "res://p5joker/fonts/BarlowCondensed-700Italic.ttf";
	public const string CjkBold = "res://fonts/jpn/NotoSansCJKjp-Bold.otf";
	public const string CjkMedium = "res://fonts/jpn/NotoSansCJKjp-Medium.otf";

	/// <summary>按路径读字体：mod 自带的原样 ttf 用 LoadDynamicFont，游戏自带的（已导入）用 ResourceLoader。预览工具替换成读本地文件。</summary>
	public static Func<string, Font?> Load = path =>
	{
		if (path.StartsWith("res://p5joker/", StringComparison.Ordinal))
		{
			FontFile f = new();
			return f.LoadDynamicFont(path) == Error.Ok ? f : null;
		}
		return ResourceLoader.Exists(path) ? ResourceLoader.Load(path) as Font : null;
	};

	private static readonly Dictionary<string, Font?> Cache = new();

	internal static bool PollFont(string path)
	{
		if (Cache.ContainsKey(path)) return true;
		if (!Fx.PollResource(path, out Resource? resource)) return false;
		if (resource is Font font) Cache[path] = font;
		return true;
	}

	public static Font Font(string path)
	{
		if (!Cache.TryGetValue(path, out Font? f))
		{
			f = Load(path);
			Cache[path] = f;
		}
		return f ?? ThemeDB.FallbackFont;
	}

	private static Shader? _quad, _grad;

	private static Shader QuadShader => _quad ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 p0; uniform vec2 p1; uniform vec2 p2; uniform vec2 p3;
uniform float sgn = 1.0;
uniform vec4 color : source_color = vec4(1.0);
varying vec2 lp;
void vertex() { lp = VERTEX; }
float edge(vec2 a, vec2 b, vec2 p) { vec2 d = b - a; return sgn * (d.x * (p.y - a.y) - d.y * (p.x - a.x)) / length(d); }
void fragment() {
	float s = min(min(edge(p0, p1, lp), edge(p1, p2, lp)), min(edge(p2, p3, lp), edge(p3, p0, lp)));
	COLOR = vec4(color.rgb, color.a * clamp(s + 0.5, 0.0, 1.0) * COLOR.a);
}" };

	/// <summary>抗锯齿凸四边形（parent 本地坐标的 4 个点，顺序随意但要沿边走一圈）。</summary>
	public static ColorRect Quad(Node parent, Vector2[] pts, Color color)
	{
		Vector2 min = pts[0], max = pts[0];
		foreach (Vector2 p in pts)
		{
			min = new Vector2(Mathf.Min(min.X, p.X), Mathf.Min(min.Y, p.Y));
			max = new Vector2(Mathf.Max(max.X, p.X), Mathf.Max(max.Y, p.Y));
		}
		min -= Vector2.One * 2f;
		max += Vector2.One * 2f;
		ShaderMaterial m = new() { Shader = QuadShader };
		Vector2 c = (pts[0] + pts[1] + pts[2] + pts[3]) * 0.25f;
		Vector2 d = pts[1] - pts[0];
		float side = d.X * (c.Y - pts[0].Y) - d.Y * (c.X - pts[0].X);
		for (int i = 0; i < 4; i++)
		{
			m.SetShaderParameter($"p{i}", pts[i] - min);
		}
		m.SetShaderParameter("sgn", side >= 0f ? 1f : -1f);
		m.SetShaderParameter("color", color);
		ColorRect r = new() { Position = min, Size = max - min, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		parent.AddChild(r);
		return r;
	}

	/// <summary>平行四边形：左上 (x0, y0) 到右下 (x1, y1)，底边相对顶边往左错开 skew（正数 = 右边往右上斜）。</summary>
	public static ColorRect Slab(Node parent, float x0, float x1, float y0, float y1, float skew, Color color) =>
		Quad(parent, new[] { new Vector2(x0 + skew, y0), new Vector2(x1 + skew, y0), new Vector2(x1, y1), new Vector2(x0, y1) }, color);

	public static Label Text(Node parent, string text, Font font, int size, Color color, Vector2 pos, int outline = 0, Color? outlineColor = null)
	{
		Label l = new()
		{
			Text = text, Position = pos, MouseFilter = Control.MouseFilterEnum.Ignore,
			LabelSettings = new LabelSettings { Font = font, FontSize = size, FontColor = color, OutlineSize = outline, OutlineColor = outlineColor ?? Colors.Black },
		};
		parent.AddChild(l);
		return l;
	}

	/// <summary>文字上下渐变（每行从 top 渐到 bottom；lineH = 行高）。</summary>
	public static ShaderMaterial Gradient(Color top, Color bottom, float lineH)
	{
		_grad ??= new Shader
		{
			Code = @"shader_type canvas_item;
uniform vec4 top_color : source_color = vec4(1.0);
uniform vec4 bottom_color : source_color = vec4(1.0);
uniform float line_h = 32.0;
varying float ly;
void vertex() { ly = VERTEX.y; }
void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float k = clamp(fract(ly / max(line_h, 1.0)) * 1.15, 0.0, 1.0);
	c.rgb *= mix(top_color.rgb, bottom_color.rgb, k);
	COLOR = c;
}"
		};
		ShaderMaterial m = new() { Shader = _grad };
		m.SetShaderParameter("top_color", top);
		m.SetShaderParameter("bottom_color", bottom);
		m.SetShaderParameter("line_h", lineH);
		return m;
	}

	public static float Width(Font font, string text, int size) => font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
}
