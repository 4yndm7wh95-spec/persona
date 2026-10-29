using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 眼神切入（照着 P5R 原版：一条斜着的撕纸条从左边"刷"地扫进来，里面是 Joker 眼睛的特写，上下是撕开的白纸边 + 黑描边，
/// 后面垫一条红色撕纸带；停一下，然后整条从中间"撕碎"，几块碎片带着红纸屑往右飞散）。
/// 素材是原版切入图（BATTLE/CUTIN/BCT_P_BC0001，fx 名 eyes）；撕边、扫入、白色前沿都是着色器实时算的。
/// 放在单独的 CanvasLayer 上，按屏幕坐标摆，不受战斗镜头缩放影响。onTear 在撕碎那一刻调用（接后续特效）。
/// </summary>
public static class CutIn
{
	private static Shader? _strip;

	/// <summary>撕纸条：UV 是切入图的贴图坐标；纸条只取 y0~y1 这一横条，上下边用噪声撕开，外面一圈白纸边 + 细黑边。</summary>
	private static Shader Strip => _strip ??= new Shader { Code = @"
shader_type canvas_item;
uniform float reveal = 1.0;
uniform float y0 = 0.36;
uniform float y1 = 0.74;
uniform float seed = 0.0;
uniform float alpha = 1.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float v = (UV.y - y0) / (y1 - y0);
	float top = 0.12 + 0.07 * noise(vec2(UV.x * 26.0, seed)) + 0.035 * noise(vec2(UV.x * 95.0, seed + 4.0));
	float bot = 0.88 - 0.07 * noise(vec2(UV.x * 23.0, seed + 9.0)) - 0.035 * noise(vec2(UV.x * 88.0, seed + 2.0));
	float aa = 0.006;
	float inside = smoothstep(top - aa, top + aa, v) * smoothstep(bot + aa, bot - aa, v);
	float pt = top - 0.05 - 0.03 * noise(vec2(UV.x * 140.0, seed + 7.0));
	float pb = bot + 0.05 + 0.03 * noise(vec2(UV.x * 130.0, seed + 5.0));
	float paper = smoothstep(pt - aa, pt + aa, v) * smoothstep(pb + aa, pb - aa, v);
	float ink = smoothstep(pt - 0.02 - aa, pt - 0.02 + aa, v) * smoothstep(pb + 0.02 + aa, pb + 0.02 - aa, v);
	vec3 col = mix(vec3(0.0), vec3(1.0), paper);
	col = mix(col, tex.rgb, inside * tex.a + inside * (1.0 - tex.a));
	col = mix(col, vec3(1.0), inside * (1.0 - tex.a));
	// 从左往右扫进来：前沿是参差的，带一道白光
	float front = mix(0.1, 0.92, reveal) * 1.2 - 0.1 + (noise(vec2(v * 9.0, seed + 1.0)) - 0.5) * 0.08;
	float shown = smoothstep(front + 0.004, front - 0.004, UV.x);
	col = mix(col, vec3(1.0), smoothstep(front - 0.05, front, UV.x) * shown);
	COLOR = vec4(col, ink * shown * alpha);
}" };

	/// <summary>
	/// 播放眼神切入。screen = 屏幕大小；整段约 0.8s：0~0.15 扫入，停到 tearAt，然后撕碎飞散 0.3s。
	/// </summary>
	public static void Eyes(Node parent, Vector2 screen, float tearAt = 0.5f, Action? onTear = null)
	{
		if (Fx.Tex("eyes") is not Texture2D tex)
		{
			onTear?.Invoke();
			return;
		}
		CanvasLayer cl = new() { Layer = 5, Name = "P5CutIn" };
		parent.AddChild(cl);
		Fx.FreeAfter(cl, tearAt + 0.6);
		Node2D root = new() { Position = new Vector2(screen.X * 0.5f, screen.Y * 0.28f), Rotation = -0.12f };
		cl.AddChild(root);
		// 只取眼睛那一块：横向 0.18~0.86（两边是白底），纵向 0.36~0.74
		const float y0 = 0.36f, y1 = 0.74f, x0 = 0.18f, x1 = 0.86f;
		Vector2 ts = tex.GetSize();
		float w = screen.X * 0.95f, h = w * (y1 - y0) * ts.Y / ((x1 - x0) * ts.X);
		float seed = Fx.Rand(0f, 50f);

		// 后面的红色撕纸带：略低、略斜，先于纸条扫进来
		Polygon2D red = new() { Color = Pal5.Crimson, Polygon = Torn(w * 1.05f, h * 0.55f, 40, 0.18f), Position = new Vector2(-30f, h * 0.38f), Rotation = 0.05f };
		root.AddChild(red);

		// 纸条：竖着切成 6 块（边是锯齿），撕碎时各自飞走
		var mat = new ShaderMaterial { Shader = Strip };
		mat.SetShaderParameter("y0", y0);
		mat.SetShaderParameter("y1", y1);
		mat.SetShaderParameter("seed", seed);
		const int n = 6;
		var cuts = new List<float[]>();
		for (int i = 0; i <= n; i++)
		{
			var c = new float[6];
			for (int j = 0; j < c.Length; j++)
			{
				c[j] = i == 0 ? -0.02f : i == n ? 1.02f : (float)i / n + Fx.Rand(-0.035f, 0.035f);
			}
			cuts.Add(c);
		}
		var pieces = new List<Polygon2D>();
		for (int i = 0; i < n; i++)
		{
			var pts = new List<Vector2>();
			var uvs = new List<Vector2>();
			void Add(float u, float v)
			{
				pts.Add(new Vector2((u - 0.5f) * w, (v - 0.5f) * h));
				uvs.Add(new Vector2((x0 + Mathf.Clamp(u, 0f, 1f) * (x1 - x0)) * ts.X, (y0 + v * (y1 - y0)) * ts.Y));
			}
			float[] l = cuts[i], r = cuts[i + 1];
			for (int j = 0; j < l.Length; j++)
			{
				Add(l[j], (float)j / (l.Length - 1));
			}
			for (int j = r.Length - 1; j >= 0; j--)
			{
				Add(r[j], (float)j / (r.Length - 1));
			}
			Polygon2D piece = new() { Texture = tex, Polygon = pts.ToArray(), UV = uvs.ToArray(), Material = mat };
			root.AddChild(piece);
			pieces.Add(piece);
		}

		Fx.Animate(root, 0, tearAt + 0.4, t =>
		{
			float s = t * (tearAt + 0.4f);
			mat.SetShaderParameter("reveal", Ease5.OutQuint(Ease5.Seg(s, 0.03f, 0.14f)));
			red.Scale = new Vector2(Ease5.OutExpo(Ease5.Seg(s, 0f, 0.12f)), 1f);
			// 停着的时候往右慢慢推、微微放大
			float hold = Mathf.Min(s, tearAt);
			root.Position = new Vector2(screen.X * 0.5f + 40f * hold, screen.Y * 0.28f);
			root.Scale = Vector2.One * (1f + 0.04f * hold);
			float k = Ease5.Seg(s, tearAt, 0.3f);
			for (int i = 0; i < pieces.Count; i++)
			{
				float dir = i % 2 == 0 ? -1f : 1f, sp = 0.6f + 0.15f * i;
				pieces[i].Position = new Vector2(1300f * sp * Ease5.OutQuint(k), dir * 160f * Ease5.OutQuint(k));
				pieces[i].Rotation = dir * 0.5f * sp * k;
				pieces[i].Scale = Vector2.One * (1f - 0.25f * k);
			}
			red.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(s, tearAt, 0.04f));
			mat.SetShaderParameter("alpha", 1f - Fx.InCubic(k));
		});

		Fx.After(root, tearAt, () =>
		{
			// 撕碎：红纸屑往右下飞 + 一道白光横扫
			for (int i = 0; i < 24; i++)
			{
				Polygon2D bit = new() { Color = i % 3 == 0 ? Pal5.Hot : Pal5.Crimson, Polygon = Torn(Fx.Rand(30f, 90f), Fx.Rand(12f, 30f), 6, 0.35f) };
				root.AddChild(bit);
				Vector2 from = new(Fx.Rand(-w * 0.45f, w * 0.45f), Fx.Rand(-h * 0.2f, h * 0.6f));
				Vector2 vel = new(Fx.Rand(700f, 1600f), Fx.Rand(-300f, 400f));
				float spin = Fx.Rand(-10f, 10f);
				Fx.Animate(bit, 0, 0.4, t =>
				{
					float s = t * 0.4f;
					bit.Position = from + vel * s + new Vector2(0f, 600f * s * s);
					bit.Rotation = spin * s;
					bit.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t));
				});
			}
			onTear?.Invoke();
		});
	}

	/// <summary>撕边的长条：宽 w、高 h，上下边各 n 段锯齿，jag = 锯齿深度（占高度比例）。</summary>
	private static Vector2[] Torn(float w, float h, int n, float jag)
	{
		var pts = new Vector2[n * 2 + 2];
		for (int i = 0; i <= n; i++)
		{
			float x = -w / 2f + w * i / n;
			pts[i] = new Vector2(x, -h / 2f + Fx.Rand(0f, jag) * h);
			pts[n * 2 + 1 - i] = new Vector2(x + Fx.Rand(-w / n * 0.3f, w / n * 0.3f), h / 2f - Fx.Rand(0f, jag) * h);
		}
		return pts;
	}
}
