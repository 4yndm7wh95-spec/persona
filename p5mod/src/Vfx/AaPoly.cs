using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 抗锯齿的纯色多边形（用户 2026-11-24：牌堆界面 UI 边缘锯齿感很重）。Godot 4.5 的 Polygon2D.Antialiased 实测不起作用（边上一个半透明像素都没有），
/// 这里自己画：多边形本体 + 沿每条边往外 1 个屏幕像素的羽化条（顶点色从不透明渐变到透明），任何渲染器都一样。
/// 用法同 Polygon2D：Polygon、Color；ClipChildren / Material 照常（羽化条的透明度也进遮罩，裁出来的边同样是软的）。
/// </summary>
public partial class AaPoly : Node2D
{
	private Vector2[] _pts = Array.Empty<Vector2>();
	private Color _color = Colors.White;
	private Vector2[] _ring = Array.Empty<Vector2>();
	private Color[] _ringCol = Array.Empty<Color>();
	private int[] _ringIdx = Array.Empty<int>();

	public Vector2[] Polygon
	{
		get => _pts;
		set { _pts = value ?? Array.Empty<Vector2>(); QueueRedraw(); }
	}

	public Color Color
	{
		get => _color;
		set { if (_color == value) return; _color = value; QueueRedraw(); }
	}

	/// <summary>羽化宽度（屏幕像素）。</summary>
	public float Feather { get; set; } = 1f;

	public override void _Draw()
	{
		int n = _pts.Length;
		if (n < 3 || _color.A <= 0f) return;
		var idx = Geometry2D.TriangulatePolygon(_pts);
		if (idx.Length < 3) return;
		RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), idx, _pts, new[] { _color });

		// 羽化条：每个顶点往外推（相邻两条边外法线的角平分线，按夹角放长，尖角处封顶），一条边两个三角形
		float scale = Mathf.Max(1e-3f, GetGlobalTransformWithCanvas().Scale.Length() / Mathf.Sqrt2);
		float w = Feather / scale;
		float area = 0f;
		for (int i = 0, j = n - 1; i < n; j = i++) area += _pts[j].X * _pts[i].Y - _pts[i].X * _pts[j].Y;
		float side = area >= 0f ? 1f : -1f;     // 屏幕坐标（y 向下）：面积为正 = 顺时针，外法线在边方向的左手边
		if (_ring.Length != n * 2) { _ring = new Vector2[n * 2]; _ringCol = new Color[n * 2]; _ringIdx = new int[n * 6]; }
		Color clear = new(_color.R, _color.G, _color.B, 0f);
		for (int i = 0; i < n; i++)
		{
			Vector2 p = _pts[i], a = _pts[(i + n - 1) % n], b = _pts[(i + 1) % n];
			Vector2 e0 = (p - a).Normalized(), e1 = (b - p).Normalized();
			Vector2 n0 = new Vector2(e0.Y, -e0.X) * side, n1 = new Vector2(e1.Y, -e1.X) * side;
			Vector2 m = n0 + n1;
			float len = m.Length();
			Vector2 dir = len < 1e-4f ? n1 : m / len;
			float cos = Mathf.Max(0.25f, dir.Dot(n1));
			_ring[i * 2] = p;
			_ring[i * 2 + 1] = p + dir * (w / cos);
			_ringCol[i * 2] = _color;
			_ringCol[i * 2 + 1] = clear;
		}
		for (int i = 0; i < n; i++)
		{
			int a0 = i * 2, a1 = a0 + 1, b0 = (i + 1) % n * 2, b1 = b0 + 1, o = i * 6;
			_ringIdx[o] = a0; _ringIdx[o + 1] = b0; _ringIdx[o + 2] = b1;
			_ringIdx[o + 3] = a0; _ringIdx[o + 4] = b1; _ringIdx[o + 5] = a1;
		}
		RenderingServer.CanvasItemAddTriangleArray(GetCanvasItem(), _ringIdx, _ring, _ringCol);
	}
}
