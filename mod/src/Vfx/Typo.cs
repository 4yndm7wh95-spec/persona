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

	private static FontVariation? _cjkHeavy;

	/// <summary>P3R 标题风格：粗体 + 右斜（选中项、名字、按钮文字用）。</summary>
	public static Font CjkHeavy => _cjkHeavy ??= new FontVariation
	{
		BaseFont = Cjk,
		VariationEmbolden = 0.55f,
		VariationTransform = new Transform2D(new Vector2(1f, 0f), new Vector2(-0.22f, 1f), Vector2.Zero),
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

	public static Label Text(Node parent, string text, Font font, int size, Color color, Vector2 pos,
		HorizontalAlignment align = HorizontalAlignment.Left, int outline = 0, Color? outlineColor = null,
		Color? shadow = null, Vector2? shadowOffset = null)
	{
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
