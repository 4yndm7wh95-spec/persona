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
uniform float poke = 0.0;          // 入①：左边戳进来的一笔划到哪（1 = 到中间偏右）
uniform float wing = 0.0;          // 入②：这一笔往右延伸到出画
uniform float open = 0.0;          // 入②：中间偏右先垂出一个鼓包，再撑成整条斜带（0~1，可过冲）
uniform float img = 0.0;           // 口子里从白到特写
uniform float zoom = 1.0;
uniform float slide = 0.0;         // 出：白纸 + 特写沿斜带方向往右上滑走（半宽为 1）
uniform float slide_red = 0.0;     // 出：下层红纸慢半拍跟着滑
uniform float thin = 0.0;          // 出：整条变细
uniform float brk = 0.0;           // 出：白纸边裂成几条
uniform float red = 0.0;           // 红纸层显出来
uniform float alpha = 1.0;
uniform float seed = 0.0;
uniform float ext = 1.3;
uniform vec4 crop = vec4(0.18, 0.36, 0.86, 0.74);
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float jag(float x, float s) { return noise(vec2(x * 40.0, s)) * 0.6 + noise(vec2(x * 110.0, s + 3.0)) * 0.4; }

// 口子的轮廓（原版的大致形状）：x 半宽坐标，返回 (纸, 口子里面)
vec2 shape(float x, float y, float grow) {
	float o = clamp(open, 0.0, 1.15);
	float cx = mix(0.25, 0.1, smoothstep(0.0, 1.0, o));
	float e = mix(0.1, 1.45, smoothstep(0.0, 1.0, o));
	float dx = (x - cx) / e;
	float lens = pow(max(1.0 - dx * dx, 0.0), 0.6);
	lens *= mix(1.0, smoothstep(-1.1, -0.45, x) * 0.85 + 0.15, step(0.6, o));      // 左端收尖
	float A = 0.15 * o * (1.0 - 0.75 * thin);
	float droop = 0.03 * (1.0 - smoothstep(0.2, 0.8, o));                          // 鼓包偏下
	float wave = 0.1 * (noise(vec2(x * 3.0, seed + 50.0)) - 0.5);
	float top = A * lens * (1.0 + wave) + 0.004 * jag(x, seed) - droop;
	float bot = A * lens * (1.0 - wave * 0.7) + 0.004 * jag(x, seed + 5.0) + droop;
	float has = step(0.003, A * lens);
	float inside = has * step(-top, y) * step(y, bot);
	float bw = 0.012 + 0.012 * jag(x, seed + 17.0) + grow;
	float paper = has * step(-top - bw, y) * step(y, bot + bw);
	// 边外几条和边平行的碎纸条（断断续续）
	float dT = -y - (top + bw), dB = y - (bot + bw);
	float sl = step(0.012, dT) * step(dT, 0.019) * step(0.58, noise(vec2(x * 6.0, seed + 70.0)))
		+ step(0.014, dB) * step(dB, 0.02) * step(0.62, noise(vec2(x * 6.0, seed + 80.0)));
	paper = max(paper, has * sl * step(0.3, o));
	return vec2(paper, inside);
}

void fragment() {
	float x0 = -1.0 + UV.x * (2.0 + 2.0 * ext);                  // 矩形往右多伸出一截，滑出去时不会被矩形边裁断
	float y = (UV.y - 0.5) * 2.0 * aspect;
	// ① 入场的一笔：左边粗、往右收尖，偶尔一个缺口；撑开后淡掉
	float tip = -1.08 + 1.0 * poke + 1.3 * wing;
	float u = clamp((x0 + 1.08) / max(tip + 1.08, 0.001), 0.0, 1.0);
	float th = (0.004 + 0.03 * pow(1.0 - u, 0.9)) * (1.0 - 0.6 * step(0.82, noise(vec2(x0 * 9.0, seed + 21.0))));
	float yc = 0.006 * sin(x0 * 5.0 + seed);
	float stroke = step(x0, tip) * step(-1.1, x0) * step(abs(y - yc) + 0.003 * jag(x0, seed + 4.0), th);
	stroke *= 1.0 - smoothstep(0.5, 0.9, open);

	// ② 白纸 + 特写（上层）：出的时候沿斜带方向往右上滑
	float x = x0 - slide;
	vec2 w = shape(x, y, 0.0);
	// 出：白纸边裂成几条平行碎条（只裂纸边，不裂特写）
	float border = w.x * (1.0 - w.y);
	border *= mix(1.0, step(brk * 0.85, noise(vec2(x * 1.3 + seed, y * 60.0))), step(0.001, brk));
	vec3 col = vec3(1.0);
	if (w.y > 0.5) {
		float tu = mix(crop.x, crop.z, (x / zoom + 1.0) * 0.5);
		float tv = mix(crop.y, crop.w, 0.5 + y / zoom / (2.0 * 0.15));
		vec4 t = texture(eyes, vec2(tu, tv));
		col = mix(vec3(1.0), mix(vec3(1.0), t.rgb, t.a), img);
	}
	float top_a = max(max(border, w.y), stroke);

	// ③ 红纸（下层）：同一个轮廓、往左下错开一点、稍大一圈，慢半拍跟着滑
	vec2 r = shape(x0 - slide_red + 0.025, y - 0.018, 0.003);
	float red_a = r.x * red;
	vec3 red_col = vec3(0.86, 0.04, 0.08);

	vec3 outc = mix(red_col, col, top_a);
	float a = max(top_a, red_a);
	COLOR = vec4(outc, a * alpha);
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
		// 按屏幕坐标摆：屏幕上 (0.52, 0.42) 处、左低右高斜 -15°（原版的斜度）
		Transform2D canvas = layer.GetGlobalTransformWithCanvas() * layer.GetGlobalTransform().AffineInverse();
		Node2D root = new() { Name = "P5CutIn" };
		layer.AddChild(root);
		root.GlobalTransform = canvas.AffineInverse() * new Transform2D(-0.27f, new Vector2(screen.X * 0.52f, screen.Y * 0.42f));
		Fx.FreeAfter(root, tearAt + 0.5);
		ColorRect rect = new()
		{
			Size = new Vector2(w * 2.3f, h), Position = new Vector2(-w / 2f, -h / 2f), MouseFilter = Control.MouseFilterEnum.Ignore,
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
			m.SetShaderParameter("poke", Ease5.OutQuint(Ease5.Seg(s, 0f, 0.07f)));
			m.SetShaderParameter("wing", Ease5.OutQuint(Ease5.Seg(s, 0.07f, 0.04f)));
			m.SetShaderParameter("open", Ease5.OutBack(Ease5.Seg(s, 0.07f, 0.1f)));
			m.SetShaderParameter("img", Ease5.Seg(s, 0.14f, 0.03f));
			m.SetShaderParameter("zoom", 1f + 0.06f * Mathf.Min(s, tearAt));
			// 出（原版约 10 帧 @60）：白纸 + 特写先往右上挪、左下露出红纸 → 越滑越快、变细、纸边裂开，红纸慢半拍跟着 → 一起出画
			float k = Ease5.Seg(s, tearAt, 0.22f), kr = Ease5.Seg(s, tearAt + 0.035f, 0.22f);
			m.SetShaderParameter("slide", 2.4f * Mathf.Pow(k, 2.2f));
			m.SetShaderParameter("slide_red", 2.4f * Mathf.Pow(kr, 2.2f));
			m.SetShaderParameter("red", Ease5.Seg(s, tearAt, 0.02f));
			m.SetShaderParameter("thin", Ease5.Seg(s, tearAt + 0.02f, 0.14f));
			m.SetShaderParameter("brk", Ease5.Seg(s, tearAt + 0.04f, 0.12f));
			m.SetShaderParameter("alpha", 1f - Ease5.Seg(s, tearAt + 0.24f, 0.05f));
		});
		Fx.After(root, tearAt, () =>
		{
			// 右边崩出的白纸片（带黑边）
			for (int i = 0; i < 6; i++)
			{
				Vector2[] shape = Shard(Fx.Rand(18f, 60f));
				Polygon2D bit = new() { Color = new Color(1f, 1f, 1f), Polygon = shape };
				Line2D edge = new() { Points = shape, Closed = true, Width = 2f, DefaultColor = new Color(0f, 0f, 0f) };
				Node2D piece = new();
				piece.AddChild(bit);
				piece.AddChild(edge);
				root.AddChild(piece);
				Vector2 from = new(Fx.Rand(0.4f, 0.5f) * w, Fx.Rand(-0.4f, 0.2f) * h * 0.3f);
				Vector2 vel = new(Fx.Rand(600f, 1500f), Fx.Rand(-500f, 400f));
				float spin = Fx.Rand(-9f, 9f);
				Fx.Animate(piece, 0.1, 0.3, t =>
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
