using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 牌堆入场的揭幕（用户 2026-11-24：之前是整张画面定格住、平移进来，太没有设计感）：一道斜线（上端偏右，P5 的斜切）从屏幕右边扫到左边，
/// 线右边露出入场画面，线上压一条红 + 黑的前沿；3D Joker 同时从第 0 帧开始连续播（不再定格等滑入）。
/// 用法：每帧 Set(t)；多边形用 Clip() 裁到露出来的一侧；整屏 / 精灵用 Material（读 line 参数，线左边透明）。
/// </summary>
public sealed class PileReveal
{
	public const float Duration = 0.16f;
	private const float Slope = 0.36f;                    // 线的斜率：x 每往下一行往左挪多少（上端偏右）
	private readonly Vector2 _view;
	private readonly float _k;
	private readonly Polygon2D _red, _black;
	private float _c = -1e6f;                             // 线：x = _c − Slope·y（屏幕像素）；右边露出
	public bool Done { get; private set; }

	/// <summary>整屏 / 精灵的遮罩：线左边透明，边缘抗锯齿；用 Track() 登记，Set() 时一起更新。</summary>
	public static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 line = vec2(-1e6, 0.36);
varying vec2 sp;
void vertex() { sp = (CANVAS_MATRIX * MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy; }
void fragment() {
	float d = sp.x - (line.x - line.y * sp.y);
	COLOR.a *= clamp(d + 0.5, 0.0, 1.0);
}
" };
	private static Shader? _shader;
	private readonly List<ShaderMaterial> _mats = new();

	/// <summary>parent：前沿那两条加在哪（入场层的最上面）。</summary>
	public PileReveal(Node parent, Vector2 view)
	{
		_view = view;
		_k = view.Y / 1080f;
		_red = new Polygon2D { Color = new Color(0.86f, 0.06f, 0.1f), Antialiased = true };
		_black = new Polygon2D { Color = new Color(0.02f, 0.02f, 0.02f), Antialiased = true };
		parent.AddChild(_red);
		parent.AddChild(_black);
	}

	/// <summary>新建一个遮罩材质并登记（给整屏的 ColorRect、3D 精灵等）。</summary>
	public ShaderMaterial Track()
	{
		var m = new ShaderMaterial { Shader = Shader };
		_mats.Add(m);
		return m;
	}

	/// <summary>登记一个已有的材质（它自己的着色器里也要有 uniform vec2 line 同样的用法）。</summary>
	public void Track(ShaderMaterial m) => _mats.Add(m);

	/// <summary>t = 入场时间（秒）。</summary>
	public void Set(float t)
	{
		float p = Mathf.Clamp(t / Duration, 0f, 1f);
		float e = 1f - Mathf.Pow(1f - p, 3f);
		float from = _view.X + Slope * _view.Y + 60f * _k, to = -60f * _k;
		_c = Mathf.Lerp(from, to, e);
		Done = p >= 1f;
		foreach (ShaderMaterial m in _mats) m.SetShaderParameter("line", new Vector2(Done ? -1e6f : _c, Slope));
		_red.Visible = _black.Visible = !Done && p > 0f;
		if (Done) return;
		// 前沿：红条在线左边（还没露出来的那侧）、黑条压在线上，越到后面越细
		float w = Mathf.Lerp(26f, 10f, e) * _k;
		_black.Polygon = Band(0f, 7f * _k);
		_red.Polygon = Band(7f * _k, 7f * _k + w);
	}

	private Vector2[] Band(float a, float b)
	{
		float y0 = -40f, y1 = _view.Y + 40f;
		Vector2 P(float off, float y) => new(_c - Slope * y - off, y);
		return new[] { P(a, y0), P(b, y0), P(b, y1), P(a, y1) };
	}

	/// <summary>多边形裁到露出来的一侧（屏幕坐标）；完全没露出来返回空数组。揭幕结束后原样返回。</summary>
	public Vector2[] Clip(Vector2[] poly)
	{
		if (Done || poly.Length < 3) return poly;
		float y0 = -4000f, y1 = 4000f;
		Vector2[] half = { new(_c - Slope * y0, y0), new(1e5f, y0), new(1e5f, y1), new(_c - Slope * y1, y1) };
		var parts = Geometry2D.IntersectPolygons(poly, half);
		if (parts.Count == 0) return System.Array.Empty<Vector2>();
		Vector2[] best = parts[0];
		float area = 0f;
		foreach (Vector2[] q in parts)
		{
			float a = Mathf.Abs(Area(q));
			if (a > area) { area = a; best = q; }
		}
		return best;
	}

	private static float Area(Vector2[] q)
	{
		float s = 0f;
		for (int i = 0, j = q.Length - 1; i < q.Length; j = i++) s += (q[j].X + q[i].X) * (q[j].Y - q[i].Y);
		return s * 0.5f;
	}

	/// <summary>3D 动作的进度：t 从 0 到 cut 把原版第 0 帧到第 last 帧播完（开头就在动，后面慢慢收进定格姿势，不会顿住）。</summary>
	public static float Progress(float t, float cut, float last)
	{
		float x = Mathf.Clamp(t / cut, 0f, 1f);
		return last * (1f - Mathf.Pow(1f - x, 1.6f));
	}
}
