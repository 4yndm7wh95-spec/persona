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
uniform float poke = 0.0;          // 入①：左边戳进来的撕口划到哪（1 = 到画面中间）
uniform float open = 0.0;          // 入②：从中间往两边鼓开（0~1，可以过冲）
uniform float img = 0.0;           // 口子里从白到特写
uniform float zoom = 1.0;
uniform float close = 0.0;         // 出：口子从左往右合上（左端往右收）+ 压扁
uniform float red = 0.0;           // 出：收过的地方留下的红色撕痕
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
	float x = (UV.x - 0.5) * 2.0;
	float y = (UV.y - 0.5) * 2.0 * aspect;
	// ① 撕口：从左边缘戳进来，左粗右尖、边上撕成一缕缕的纤维
	float tip = -1.08 + 1.1 * poke;
	float u = clamp((x + 1.08) / max(tip + 1.08, 0.001), 0.0, 1.0);
	float yc = 0.012 * sin(x * 6.0 + seed);
	float th = (0.01 + 0.06 * pow(1.0 - u, 0.8)) * (0.35 + 1.2 * noise(vec2(x * 7.0, seed + 21.0))) * (0.7 + 0.6 * jag(x, seed + 23.0));
	float fibre = step(0.6, noise(vec2(x * 5.0, y * 140.0 + seed)));
	float stab = step(x, tip) * smoothstep(th, th * 0.7, abs(y - yc)) * (1.0 - fibre * step(th * 0.55, abs(y - yc)));
	stab *= 1.0 - smoothstep(0.6, 1.0, open);
	// ② 口子：从中间往两边鼓开，两头尖、中间宽；出的时候左端往右收、整体压扁
	float e = 0.1 + 1.0 * clamp(open, 0.0, 1.2);
	float lens = pow(max(1.0 - (x / e) * (x / e), 0.0), 0.7);
	float left = -1.15 + 2.3 * close;
	float A = 0.15 * min(open, 1.1) * (1.0 - 0.7 * close) * step(left, x);
	// 不规整：上下边各自有大块的鼓包和凹口（低频噪声大幅度），再叠锯齿；上下不对称
	float top = A * lens * (0.45 + 1.0 * noise(vec2(x * 3.2, seed + 50.0))) * (0.8 + 0.4 * jag(x, seed)) + 0.005 * jag(x, seed + 8.0);
	float bot = A * lens * (0.35 + 0.9 * noise(vec2(x * 2.6, seed + 60.0))) * (0.8 + 0.4 * jag(x, seed + 5.0)) + 0.005 * jag(x, seed + 2.0);
	float has = step(0.003, A * lens);
	float inside = has * (y < 0.0 ? step(-top, y) : step(y, bot));
	float bw = 0.016 + 0.024 * jag(x, seed + 17.0);
	// 纸边伸出去的细长尖刺（随机几根，三角形），上下都有
	float cell = floor(x * 13.0), fx = fract(x * 13.0 + 0.3 * noise(vec2(x * 4.0, seed)));
	float tooth = step(0.62, hash(vec2(cell, seed))) * pow(max(1.0 - abs(fx - 0.5) * 2.0, 0.0), 2.5);
	float toothB = step(0.68, hash(vec2(cell, seed + 9.0))) * pow(max(1.0 - abs(fx - 0.5) * 2.0, 0.0), 2.5);
	float reachT = top + bw + tooth * 0.11 * min(open, 1.0) * lens;
	float reachB = bot + bw + toothB * 0.09 * min(open, 1.0) * lens;
	float paper = has * (y < 0.0 ? step(-reachT, y) : step(y, reachB));
	// 边外飘着的小碎纸
	float edgeD = y < 0.0 ? -y - reachT : y - reachB;
	paper = max(paper, has * step(0.8, noise(vec2(x * 26.0, y * 55.0) + seed + 70.0)) * step(0.01, edgeD) * step(edgeD, 0.06 * min(open, 1.0)));
	float ink = step(0.88, noise(vec2(x * 110.0, y * 150.0) + seed)) * (paper - inside);
	vec3 col = vec3(1.0);
	if (inside > 0.5) {
		float tu = mix(crop.x, crop.z, (x / zoom + 1.0) * 0.5);
		float tv = mix(crop.y, crop.w, 0.5 + y / zoom / (2.0 * 0.15));
		vec4 t = texture(eyes, vec2(tu, tv));
		col = mix(vec3(1.0), mix(vec3(1.0), t.rgb, t.a), img);
	}
	col = mix(col, vec3(0.0), ink);
	float a = max(paper, stab);
	// 出：收过的地方（左端后面）一缕缕红色撕痕
	float behind = step(left - 0.75, x) * step(x, left + 0.02);
	float rw = 0.012 + 0.04 * jag(x, seed + 30.0) * (1.0 - (left - x) / 0.75);
	float r = behind * step(abs(y - 0.015 - 0.02 * noise(vec2(x * 2.0, seed))), rw) * step(0.5, noise(vec2(x * 2.5, y * 220.0 + seed))) * red;
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
			m.SetShaderParameter("poke", Ease5.OutQuint(Ease5.Seg(s, 0f, 0.08f)));
			m.SetShaderParameter("open", Ease5.OutBack(Ease5.Seg(s, 0.07f, 0.1f)));
			m.SetShaderParameter("img", Ease5.Seg(s, 0.13f, 0.04f));
			m.SetShaderParameter("zoom", 1f + 0.06f * Mathf.Min(s, tearAt));
			m.SetShaderParameter("close", Ease5.OutQuint(Ease5.Seg(s, tearAt, 0.2f)));
			m.SetShaderParameter("red", Ease5.Seg(s, tearAt, 0.05f) * (1f - Ease5.Seg(s, tearAt + 0.14f, 0.12f)));
			m.SetShaderParameter("alpha", 1f - Ease5.Seg(s, tearAt + 0.2f, 0.08f));
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
				Vector2 from = new(Fx.Rand(0.3f, 0.5f) * w, Fx.Rand(-0.5f, 0.5f) * h * 0.3f);
				Vector2 vel = new(Fx.Rand(600f, 1500f), Fx.Rand(-500f, 400f));
				float spin = Fx.Rand(-9f, 9f);
				Fx.Animate(piece, 0.08, 0.36, t =>
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
