using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// P3R 风格排版。字体用 Windows 自带字体：Bahnschrift（窄体工业风无衬线，接近 P3R UI），
/// 中文回退到微软雅黑粗体。斜体用 Node2D.Skew 实现，和 P3R 的倾斜字一致。
/// </summary>
public static class Typo
{
	private static Font? _latin;
	private static Font? _cjk;

	public static Font Cjk => _cjk ??= new SystemFont
	{
		FontNames = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "PingFang SC" },
		FontWeight = 700
	};

	public static Font Latin
	{
		get
		{
			if (_latin == null)
			{
				SystemFont f = new SystemFont
				{
					FontNames = new[] { "Bahnschrift", "Bahnschrift SemiBold Condensed", "Arial Narrow", "Impact", "Arial" },
					FontWeight = 800,
					FontStretch = 75
				};
				f.Fallbacks = new Godot.Collections.Array<Font> { Cjk };
				_latin = f;
			}
			return _latin;
		}
	}

	private static Font? _barlow;
	private static Font? _cjkBold;

	/// <summary>
	/// v0.14.50 Barlow Condensed 900 Italic（P3R 菜单标题那种窄体粗斜体，打包在 p3makoto/fonts）。
	/// 资源包里的 .ttf 没有导入文件，ResourceLoader.Load 读不到，要用 FontFile.LoadDynamicFont；读不到退回 Latin。中文字符回退到 CjkBold。
	/// </summary>
	public static Font Barlow
	{
		get
		{
			if (_barlow == null)
			{
				FontFile f = new();
				if (f.LoadDynamicFont("res://p3makoto/fonts/BarlowCondensed-900Italic.ttf") == Error.Ok)
				{
					f.Fallbacks = new Godot.Collections.Array<Font> { CjkBold };
					_barlow = f;
				}
			}
			return _barlow ?? Latin;
		}
	}

	private static Font? _cinzel;

	/// <summary>v0.14.81 Cinzel Bold（OFL，罗马碑文体）：塔罗卡框底部的阿卡纳罗马数字（P3R 原版牌底的数字就是这种衬线大写）。读不到退回 Latin。</summary>
	public static Font Cinzel
	{
		get
		{
			if (_cinzel == null)
			{
				FontFile f = new();
				if (f.LoadDynamicFont("res://p3makoto/fonts/Cinzel-Bold.ttf") == Error.Ok)
				{
					_cinzel = f;
				}
			}
			return _cinzel ?? Latin;
		}
	}

	/// <summary>游戏自带的思源黑体粗体（正体，不斜切）；读不到用系统中文粗体。</summary>
	public static Font CjkBold => _cjkBold ??= ResourceLoader.Load("res://fonts/jpn/NotoSansCJKjp-Bold.otf") as Font ?? Cjk;

	private static FontVariation? _cjkHeavy;

	/// <summary>P3R 标题风格：加粗的中文（选中项、名字、按钮文字用）。v0.14.50 起是正体：中文斜切看着别扭（用户实机反馈），斜体只留给英文。</summary>
	public static Font CjkHeavy => _cjkHeavy ??= new FontVariation
	{
		BaseFont = Cjk,
		VariationEmbolden = 0.3f,
	};

	private static FontVariation? _latinItalic;

	/// <summary>P3R 数字风格：窄体粗斜体（血量 / 金币等数字）。</summary>
	public static Font LatinItalic => _latinItalic ??= new FontVariation
	{
		BaseFont = Latin,
		VariationEmbolden = 0.2f,
		VariationTransform = new Transform2D(new Vector2(1f, 0f), new Vector2(-0.2f, 1f), Vector2.Zero),
	};

	/// <summary>
	/// 把一个原版标签（Label / MegaLabel）的字体和颜色换成指定样式，返回恢复用的回调。
	/// 有 LabelSettings 的复制一份改；没有的用主题覆盖。
	/// </summary>
	public static System.Action Restyle(Label label, Font font, Color color, Color outline, int outlineSize = -1)
	{
		LabelSettings? orig = label.LabelSettings;
		if (orig != null)
		{
			LabelSettings ls = (LabelSettings)orig.Duplicate();
			ls.Font = font;
			ls.FontColor = color;
			ls.OutlineColor = outline;
			if (outlineSize >= 0) ls.OutlineSize = outlineSize;
			ls.ShadowColor = new Color(0f, 0f, 0f, 0f);
			label.LabelSettings = ls;
		}
		else
		{
			label.AddThemeFontOverride("font", font);
			label.AddThemeColorOverride("font_color", color);
			label.AddThemeColorOverride("font_outline_color", outline);
			label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0f));
			if (outlineSize >= 0) label.AddThemeConstantOverride("outline_size", outlineSize);
		}
		return () =>
		{
			if (!GodotObject.IsInstanceValid(label)) return;
			label.LabelSettings = orig;
			label.RemoveThemeFontOverride("font");
			label.RemoveThemeColorOverride("font_color");
			label.RemoveThemeColorOverride("font_outline_color");
			label.RemoveThemeColorOverride("font_shadow_color");
			label.RemoveThemeConstantOverride("outline_size");
		};
	}

	/// <summary>
	/// 主题覆盖（字体 / 颜色 / 常量），返回恢复回调：恢复成覆盖之前的值（原来就有覆盖的放回原值，没有的移除）。
	/// 原版 MegaRichTextLabel 要求自带 normal_font 覆盖，所以不能简单地 Remove。
	/// </summary>
	public static System.Action Theme(Control c, Font? font = null, string fontKey = "font",
		(string Key, Color Value)[]? colors = null, (string Key, int Value)[]? consts = null)
	{
		var undo = new System.Collections.Generic.List<System.Action>();
		if (font != null)
		{
			bool had = c.HasThemeFontOverride(fontKey);
			Font? old = had ? c.GetThemeFont(fontKey) : null;
			c.AddThemeFontOverride(fontKey, font);
			undo.Add(() => { if (had && old != null) c.AddThemeFontOverride(fontKey, old); else c.RemoveThemeFontOverride(fontKey); });
		}
		foreach ((string k, Color v) in colors ?? System.Array.Empty<(string, Color)>())
		{
			bool had = c.HasThemeColorOverride(k);
			Color old = had ? c.GetThemeColor(k) : default;
			c.AddThemeColorOverride(k, v);
			undo.Add(() => { if (had) c.AddThemeColorOverride(k, old); else c.RemoveThemeColorOverride(k); });
		}
		foreach ((string k, int v) in consts ?? System.Array.Empty<(string, int)>())
		{
			bool had = c.HasThemeConstantOverride(k);
			int old = had ? c.GetThemeConstant(k) : 0;
			c.AddThemeConstantOverride(k, v);
			undo.Add(() => { if (had) c.AddThemeConstantOverride(k, old); else c.RemoveThemeConstantOverride(k); });
		}
		return () =>
		{
			if (!GodotObject.IsInstanceValid(c)) return;
			for (int i = undo.Count - 1; i >= 0; i--) undo[i]();
		};
	}

	private static Shader? _gradShader;

	/// <summary>逐行竖向渐变的文字材质（P3R 的字不用纯色）：每一行从 top 渐变到 bottom；line_h 是行距，y0 是第一行顶部（标签本地坐标）。</summary>
	public static ShaderMaterial Gradient(Color top, Color bottom, float lineH, float y0 = 0f)
	{
		_gradShader ??= new Shader
		{
			Code = @"shader_type canvas_item;
uniform vec4 top_color : source_color = vec4(1.0);
uniform vec4 bottom_color : source_color = vec4(1.0);
uniform float line_h = 32.0;
uniform float y0 = 0.0;
varying float ly;
void vertex() { ly = VERTEX.y; }
void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float k = clamp(fract((ly - y0) / max(line_h, 1.0)) * 1.15, 0.0, 1.0);
	c.rgb *= mix(top_color.rgb, bottom_color.rgb, k);
	COLOR = c;
}"
		};
		ShaderMaterial m = new() { Shader = _gradShader };
		m.SetShaderParameter("top_color", top);
		m.SetShaderParameter("bottom_color", bottom);
		m.SetShaderParameter("line_h", lineH);
		m.SetShaderParameter("y0", y0);
		return m;
	}

	/// <summary>给标签挂渐变材质（字色先设成白，由材质上色），返回恢复回调。行高自动量。</summary>
	public static System.Action ApplyGradient(Control label, Color top, Color bottom)
	{
		float lineH = 32f;
		if (label is RichTextLabel rt && rt.GetLineCount() > 0)
		{
			lineH = rt.GetContentHeight() / (float)rt.GetLineCount();
		}
		else if (label is Label l)
		{
			lineH = l.GetLineHeight() + (l.LabelSettings?.LineSpacing ?? l.GetThemeConstant("line_spacing"));
			if (l.GetLineCount() <= 1) lineH = l.Size.Y;
		}
		Material? old = label.Material;
		label.Material = Gradient(top, bottom, lineH);
		return () => { if (GodotObject.IsInstanceValid(label)) label.Material = old; };
	}

	public static Label Text(Node parent, string text, Font font, int size, Color color, Vector2 pos,
		HorizontalAlignment align = HorizontalAlignment.Left, int outline = 0, Color? outlineColor = null,
		Color? shadow = null, Vector2? shadowOffset = null)
	{
		Core.Prof.Labels++;
		LabelSettings ls = new LabelSettings
		{
			Font = font,
			FontSize = size,
			FontColor = color,
			OutlineSize = outline,
			OutlineColor = outlineColor ?? Colors.Black
		};
		if (shadow.HasValue)
		{
			ls.ShadowColor = shadow.Value;
			ls.ShadowSize = 0;
			ls.ShadowOffset = shadowOffset ?? new Vector2(4f, 4f);
		}
		Label label = new Label
		{
			Text = text,
			LabelSettings = ls,
			HorizontalAlignment = align,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		parent.AddChild(label);
		label.Position = pos;
		return label;
	}
}
