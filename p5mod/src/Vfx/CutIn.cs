using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 眼神切入（逐帧照着 P5R 原版，用户 2026-10-29 的参考视频）：
///  入 —— 一道极细的锯齿白色裂缝从左往右划过画面（4 帧），裂缝中间像眼睛一样"鼓开"成一个两头尖、中间宽的斜口子，
///        口子里先是一团白，随即显出 Joker 眼睛的特写；口子边缘是撕开的白纸边，纸边上有黑色墨点；
///  停 —— 特写在口子里慢慢推近；
///  出 —— 口子被压扁成一条细线、往右扯走，左边拖出一道红色撕痕，右边崩出白色纸片。
/// 整个口子是一个着色器实时算的（裂缝、鼓开、纸边、墨点、红痕），素材只有原版切入图（BCT_P_BC0001，fx 名 eyes）。
/// 用户：切入不能盖住局内人物 —— 放在人物身后那层（back），按屏幕坐标摆。
/// </summary>
public static class CutIn
{
	private static Shader? _slit;

	private static Shader Slit => _slit ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D eyes : filter_linear;
uniform float aspect = 0.32;       // 矩形高 / 半宽
uniform float line = 0.0;          // 裂缝从左往右划到哪（0~1）
uniform float open = 0.0;          // 口子张开（0~1，可以过冲）
uniform float img = 0.0;           // 口子里从白到特写
uniform float zoom = 1.0;
uniform float shift = 0.0;         // 出：往右扯
uniform float red = 0.0;           // 出：左边红痕
uniform float alpha = 1.0;
uniform float seed = 0.0;
uniform vec4 crop = vec4(0.18, 0.36, 0.86, 0.74);   // 特写取图范围 x0 y0 x1 y1
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float jag(float x, float s) { return noise(vec2(x * 22.0, s)) * 0.6 + noise(vec2(x * 75.0, s + 3.0)) * 0.4; }
void fragment() {
	float x = (UV.x - 0.5) * 2.0 - shift;
	float y = (UV.y - 0.5) * 2.0 * aspect;
	float lens = pow(max(1.0 - x * x, 0.0), 0.75);
	float A = 0.15 * open;
	// 上下边各自撕开（锯齿 + 小缺口）
	float top = A * lens * (0.82 + 0.36 * jag(x, seed)) + 0.006 * jag(x, seed + 8.0);
	float bot = A * lens * (0.82 + 0.36 * jag(x, seed + 5.0)) + 0.006 * jag(x, seed + 2.0);
	// 裂缝：还没张开时的一道细锯齿白线
	float crack = (0.004 + 0.007 * jag(x, seed + 11.0)) * step(x, line * 2.3 - 1.15) * step(-1.0, x) * step(x, 1.0);
	float inside = y < 0.0 ? step(-top, y) : step(y, bot);
	float bw = 0.018 + 0.022 * jag(x, seed + 17.0);
	float paper = y < 0.0 ? step(-top - bw, y) : step(y, bot + bw);
	paper = max(paper * step(0.002, A * lens), step(abs(y), crack));
	// 纸边上的黑墨点
	float ink = step(0.87, noise(vec2(x * 110.0, y * 150.0) + seed)) * (paper - inside);
	vec3 col = vec3(1.0);
	if (inside > 0.5 && A * lens > 0.002) {
		float u = mix(crop.x, crop.z, ((x + shift * 0.0) / zoom + 1.0) * 0.5);
		float v = mix(crop.y, crop.w, 0.5 + y / zoom / (2.0 * 0.15));
		vec4 t = texture(eyes, vec2(u, v));
		col = mix(vec3(1.0), mix(vec3(1.0), t.rgb, t.a), img);
	}
	col = mix(col, vec3(0.0), ink);
	float a = paper;
	// 出：左边一道红色撕痕（横向的拉丝）
	float rx = clamp(-x - 0.05, 0.0, 1.0);
	float streak = step(0.42, noise(vec2(x * 3.0, y * 90.0 + seed))) * step(abs(y - 0.02), 0.09 * rx + 0.006) * step(x, -0.05) * step(-1.2, x);
	float r = streak * red;
	col = mix(col, vec3(0.9, 0.04, 0.08), r * (1.0 - paper));
	a = max(a, r);
	COLOR = vec4(col, a * alpha);
}" };

	/// <summary>
	/// 播放眼神切入。layer = 人物身后那层（Control），screen = 屏幕大小。0~0.2 入，停到 tearAt，tearAt 起 0.22s 出；onTear 在出的那一刻调用。
	/// </summary>
	public static void Eyes(Control layer, Vector2 screen, float tearAt = 0.5f, Action? onTear = null)
	{
		if (Fx.Tex("eyes") is not Texture2D tex)
		{
			onTear?.Invoke();
			return;
		}
		float w = screen.X * 1.05f, aspect = 0.34f, h = w / 2f * aspect * 2f;
		// 按屏幕坐标摆：屏幕上 (0.5, 0.36) 处、斜 -10°
		Transform2D canvas = layer.GetGlobalTransformWithCanvas() * layer.GetGlobalTransform().AffineInverse();
		Node2D root = new() { Name = "P5CutIn" };
		layer.AddChild(root);
		root.GlobalTransform = canvas.AffineInverse() * new Transform2D(-0.17f, new Vector2(screen.X * 0.5f, screen.Y * 0.36f));
		Fx.FreeAfter(root, tearAt + 0.5);
		ColorRect rect = new()
		{
			Size = new Vector2(w, h), Position = new Vector2(-w / 2f, -h / 2f), MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = Slit },
		};
		root.AddChild(rect);
		var m = (ShaderMaterial)rect.Material;
		m.SetShaderParameter("eyes", tex);
		m.SetShaderParameter("aspect", aspect);
		m.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		float total = tearAt + 0.4f;
		Fx.Animate(root, 0, total, t =>
		{
			float s = t * total;
			m.SetShaderParameter("line", Ease5.OutQuint(Ease5.Seg(s, 0f, 0.07f)));
			float open = Ease5.OutBack(Ease5.Seg(s, 0.06f, 0.11f));
			float close = Ease5.OutQuint(Ease5.Seg(s, tearAt, 0.16f));
			m.SetShaderParameter("open", open * (1f - 0.88f * close));
			m.SetShaderParameter("img", Ease5.Seg(s, 0.12f, 0.06f));
			m.SetShaderParameter("zoom", 1f + 0.06f * Mathf.Min(s, tearAt));
			m.SetShaderParameter("shift", 0.9f * Fx.InCubic(Ease5.Seg(s, tearAt + 0.06f, 0.24f)));
			m.SetShaderParameter("red", Ease5.Seg(s, tearAt, 0.06f) * (1f - Ease5.Seg(s, tearAt + 0.16f, 0.14f)));
			m.SetShaderParameter("alpha", 1f - Ease5.Seg(s, tearAt + 0.22f, 0.1f));
		});
		Fx.After(root, tearAt, () =>
		{
			// 右边崩出的白纸片（带黑边）
			for (int i = 0; i < 16; i++)
			{
				Vector2[] shape = Shard(Fx.Rand(18f, 60f));
				Polygon2D bit = new() { Color = new Color(1f, 1f, 1f), Polygon = shape };
				Line2D edge = new() { Points = shape, Closed = true, Width = 2f, DefaultColor = new Color(0f, 0f, 0f) };
				Node2D piece = new();
				piece.AddChild(bit);
				piece.AddChild(edge);
				root.AddChild(piece);
				Vector2 from = new(Fx.Rand(0.1f, 0.5f) * w, Fx.Rand(-0.6f, 0.6f) * h * 0.4f);
				Vector2 vel = new(Fx.Rand(600f, 1500f), Fx.Rand(-500f, 400f));
				float spin = Fx.Rand(-9f, 9f);
				Fx.Animate(piece, 0.04, 0.36, t =>
				{
					float s = t * 0.36f;
					piece.Position = from + vel * s;
					piece.Rotation = spin * s;
					piece.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t));
				});
			}
			onTear?.Invoke();
		});
	}

	private static Vector2[] Shard(float r)
	{
		int n = 4 + Fx.Rng.Next(3);
		var pts = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			pts[i] = Vector2.Right.Rotated(Mathf.Tau * i / n + Fx.Rand(-0.4f, 0.4f)) * r * Fx.Rand(0.4f, 1f) * new Vector2(1.6f, 0.7f);
		}
		return pts;
	}
}
