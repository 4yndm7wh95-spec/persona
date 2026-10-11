using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// P3R 菜单 UI 的视觉语言（参照原版菜单截图）：
/// · 色块拼接：深蓝 / 白 / 青的大块平面被锐利斜线切开、层层叠放；
/// · 水之圆：巨大的圆形里是深蓝渐变 + 流动的水下焦散光纹，切在白色或深色背景上；
/// · 超大斜体字：旋转的巨型粗黑 / 白字做背景水印（"SYSTEM"、"EQUIP"、"COMMU"）；
/// · 选中项：红色粗斜体字压在白色平行四边形上，后面错开一块黑色；
/// · 细圆弧 / 半圆环点缀。
/// </summary>
public static class P3Ui
{
	/// <summary>水之圆（焦散）。返回圆的 ColorRect，SetParam("reveal", 0~1) 控制张开，("alpha") 控制淡出。</summary>
	public static ColorRect WaterDisc(Node2D parent, Vector2 center, float radius)
	{
		Node2D at = new() { Position = center };
		parent.AddChild(at);
		ColorRect disc = Fx.ShaderQuad(at, Shaders.WaterDisc, radius * 2f);
		Fx.SetParam(disc, "reveal", 0f);
		return disc;
	}

	/// <summary>斜切色块：给定左右两端 x、上下 y 和倾斜量（上边相对下边右移 lean）。</summary>
	public static Polygon2D Slab(Node parent, float x0, float x1, float yTop, float yBottom, float lean, Color color)
	{
		return Fx.Poly(parent, new[] { new Vector2(x0 + lean, yTop), new Vector2(x1 + lean, yTop), new Vector2(x1, yBottom), new Vector2(x0, yBottom) }, color);
	}

	/// <summary>超大斜体水印字（旋转、半透明）。</summary>
	public static Node2D GiantWord(Node2D parent, string text, Vector2 pos, int size, float rotation, Color color)
	{
		Node2D w = new() { Position = pos, Rotation = rotation, Skew = -0.2f };
		parent.AddChild(w);
		Typo.Text(w, text, Typo.Latin, size, color, new Vector2(0f, -size * 0.6f));
		return w;
	}

	/// <summary>"选中项"标签：白色平行四边形 + 错开的黑块 + 红色粗斜体字。</summary>
	public static Node2D RedTag(Node2D parent, string text, Vector2 pos, int size, bool cjk = true)
	{
		Node2D tag = new() { Position = pos, Skew = -0.25f };
		parent.AddChild(tag);
		float w = text.Length * size * (cjk ? 1.05f : 0.62f) + size * 1.6f, h = size * 1.45f;
		Fx.Poly(tag, new[] { new Vector2(10f, 10f), new Vector2(w + 34f, 4f), new Vector2(w + 10f, h + 14f), new Vector2(-6f, h + 10f) }, new Color(0.02f, 0.02f, 0.05f, 0.95f));
		Fx.Poly(tag, new[] { new Vector2(0f, 0f), new Vector2(w + 22f, -6f), new Vector2(w, h), new Vector2(-14f, h) }, new Color(0.98f, 0.98f, 1f, 0.97f));
		Fx.Poly(tag, new[] { new Vector2(-14f, h - 6f), new Vector2(w, h - 6f), new Vector2(w, h), new Vector2(-14f, h) }, new Color(0.9f, 0.1f, 0.25f, 0.95f));
		Typo.Text(tag, text, cjk ? Typo.Cjk : Typo.Latin, size, new Color(0.88f, 0.06f, 0.16f), new Vector2(size * 0.8f, h * 0.5f - size * 0.68f),
			shadow: new Color(0.02f, 0.02f, 0.05f, 0.9f), shadowOffset: new Vector2(4f, 3f));
		return tag;
	}

	/// <summary>细圆弧（start~end 弧度），描边。</summary>
	public static Line2D Arc(Node parent, Vector2 center, float radius, float start, float end, Color color, float width)
	{
		const int n = 48;
		var pts = new Vector2[n + 1];
		for (int i = 0; i <= n; i++)
		{
			float a = Mathf.Lerp(start, end, i / (float)n);
			pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
		}
		return Fx.Line(parent, pts, color, width);
	}
}
