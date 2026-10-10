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
