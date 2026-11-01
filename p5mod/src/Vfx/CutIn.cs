using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace P5Joker;

/// <summary>
/// Authored P5R geometry and material keys from BCT_P_SUMMON.GMD + BCT_P_SUMMON_BT.GMD.
/// The existing Joker texture replaces btl_cutin.dds. Paper/face/!! keys are discrete.
/// </summary>
public static class CutIn
{
	private sealed class Clip
	{
		public float Duration { get; set; }
		public float Speed { get; set; }
		public float[] Canvas { get; set; } = Array.Empty<float>();
		public Part[] Meshes { get; set; } = Array.Empty<Part>();
	}
	private sealed class Part
	{
		public string Name { get; set; } = "";
		public float[][] Vertices { get; set; } = Array.Empty<float[]>();
		public float[][] Uv { get; set; } = Array.Empty<float[]>();
		public int[] Indices { get; set; } = Array.Empty<int>();
		public float[] Color { get; set; } = Array.Empty<float>();
		public float[][] Keys { get; set; } = Array.Empty<float[]>();
		public bool Textured { get; set; }
		public bool Linear { get; set; }
	}
	private sealed record Prepared(Part Part, ArrayMesh Mesh);
	private static Clip? _clip;
	private static Prepared[]? _parts;
	private static ShaderMaterial? _opaqueEyes;

	private static void Load()
	{
		if (_parts != null) return;
		using Stream stream = typeof(CutIn).Assembly.GetManifestResourceStream("P5Joker.EyeCutIn.json")
			?? throw new InvalidDataException("Missing embedded P5R eye cut-in data");
		Clip clip = JsonSerializer.Deserialize<Clip>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
			?? throw new InvalidDataException("Invalid P5R eye cut-in data");
		if (clip.Duration <= 0 || clip.Speed <= 0 || clip.Canvas.Length != 2 || clip.Meshes.Length == 0)
			throw new InvalidDataException("Invalid P5R eye cut-in timing/canvas");
		List<Prepared> parts = new();
		foreach (Part part in clip.Meshes)
		{
			Vector3[] vertices = new Vector3[part.Vertices.Length];
			Color[] colors = new Color[vertices.Length];
			Vector2[] uv = new Vector2[vertices.Length];
			for (int i = 0; i < vertices.Length; i++)
			{
				vertices[i] = new Vector3(part.Vertices[i][0], part.Vertices[i][1], 0f);
				colors[i] = new Color(part.Color[0], part.Color[1], part.Color[2], 1f);
				if (part.Textured) uv[i] = new Vector2(part.Uv[i][0], part.Uv[i][1]);
			}
			Godot.Collections.Array arrays = new();
			arrays.Resize((int)Mesh.ArrayType.Max);
			arrays[(int)Mesh.ArrayType.Vertex] = vertices;
			arrays[(int)Mesh.ArrayType.Color] = colors;
			arrays[(int)Mesh.ArrayType.Index] = part.Indices;
			if (part.Textured) arrays[(int)Mesh.ArrayType.TexUV] = uv;
			ArrayMesh mesh = new();
			mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
			parts.Add(new Prepared(part, mesh));
		}
		// Original face materials are opaque; PNG alpha must not punch holes through hair.
		_opaqueEyes = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type canvas_item;
render_mode unshaded;
void fragment() { COLOR = vec4(texture(TEXTURE, UV).rgb, 1.0); }
" } };
		_clip = clip;
		_parts = parts.ToArray();
	}

	private static float Opacity(Part part, float time)
	{
		float[][] keys = part.Keys;
		if (keys.Length == 0) return 0f;
		int i = 0;
		while (i + 1 < keys.Length && keys[i + 1][0] <= time + 0.000001f) i++;
		if (!part.Linear || i + 1 == keys.Length) return keys[i][1];
		float span = keys[i + 1][0] - keys[i][0];
		return span <= 0 ? keys[i + 1][1] : Mathf.Lerp(keys[i][1], keys[i + 1][1], Mathf.Clamp((time - keys[i][0]) / span, 0f, 1f));
	}

	// ───────────── 2026-11-01：原版特写直接放没冲击力，改成三下：砸进来 → 停住 → 被一刀劈开 ─────────────
	// 原版时间（实际秒数，速度 2.5）：0～.2 裂缝；.2 图出现；停到 .587；之后原版是慢慢合拢——这里不用，换成劈开。

	/// <summary>
	/// 图出现 / 爪痕劈下（SplitAt 前 ClawTime 秒划过，SplitAt 划完）/ 顿帧之后碎开（ShatterAt）/ 碎片飞完（ShatterTime）。
	/// 舞台、特写、亚森离场、咒焰收掉都对齐这几刻。
	/// </summary>
	public const float ImpactAt = 0.2f, SplitAt = 0.91f, ClawTime = 0.07f, ShatterAt = SplitAt + 0.067f, ShatterTime = 0.42f;

	/// <summary>
	/// 碎纸（舞台和特写共用，2026-11-01：用户嫌「一刀劈成两半滑走」不对）：三道爪痕把画面切成四条，每条再按 4 个点的
	/// Voronoi 分块 → 最多 16 块纸片。shat = 碎开后几秒（&lt; 0 = 还没碎）：每块从自己的中心往外抛（初速快、指数减速，加重力往下掉）、
	/// 打转、绕自己翻面（翻到背面是白纸）。shatterSample 把屏幕上的点反推回碎之前的位置。
	/// </summary>
	public const string ShatterGlsl = @"
uniform float shat = -1.0;
uniform float tseed = 0.0;
uniform vec2 sres = vec2(1920.0, 1080.0);
float sh(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031 + tseed * 0.0137); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
// 三道爪痕：从右上往左下，平行，间距 0.11（屏高单位）
vec2 clawDir() { return normalize(vec2(-1.0, 0.62)); }
vec2 clawN() { vec2 d = clawDir(); return vec2(-d.y, d.x); }
vec2 clawC(float asp) { return vec2(0.56 * asp, 0.47); }
float clawOff(int i) { return (float(i) - 1.0) * 0.11; }
int bandOf(vec2 q, float asp) {
	float b = dot(q - clawC(asp), clawN());
	return b < clawOff(0) ? 0 : b < clawOff(1) ? 1 : b < clawOff(2) ? 2 : 3;
}
vec2 seedOf(int c, float asp) { return vec2((0.12 + 0.76 * sh(vec2(float(c), 1.0))) * asp, 0.12 + 0.76 * sh(vec2(float(c), 2.0))); }
int cellOf(vec2 q, float asp, out float gap) {
	float d1 = 1e9, d2 = 1e9; int best = 0;
	for (int c = 0; c < 6; c++) { float d = length(q - seedOf(c, asp)); if (d < d1) { d2 = d1; d1 = d; best = c; } else if (d < d2) { d2 = d; } }
	gap = (d2 - d1) * 0.5;
	return best;
}
// 离最近的切口多远（Voronoi 边、爪痕线）
float cutDist(vec2 q, float asp) {
	float gap; cellOf(q, asp, gap);
	float b = dot(q - clawC(asp), clawN());
	float dl = min(abs(b - clawOff(0)), min(abs(b - clawOff(1)), abs(b - clawOff(2))));
	return min(gap, dl);
}
bool shatterSample(vec2 uv, out vec2 src, out float back, out float edge) {
	src = uv; back = 0.0; edge = 1.0;
	if (shat < 0.0) return true;
	float asp = sres.x / sres.y;
	vec2 q = vec2(uv.x * asp, uv.y);
	bool hit = false;
	for (int k = 0; k < 4; k++) {
		for (int c = 0; c < 6; c++) {
			float id = float(k * 6 + c);
			// 这一块的中心：这个 Voronoi 点挪到这条带子的中线上
			vec2 sd = seedOf(c, asp);
			float bmid = k == 0 ? clawOff(0) - 0.25 : k == 3 ? clawOff(2) + 0.25 : (clawOff(k - 1) + clawOff(k)) * 0.5;
			float bnow = dot(sd - clawC(asp), clawN());
			vec2 piv = sd + clawN() * (bmid - bnow);
			vec2 out_ = normalize(piv - vec2(0.5 * asp, 0.5) + (vec2(sh(vec2(id, 3.0)), sh(vec2(id, 4.0))) - 0.5) * 0.6);
			float v = 1.1 + 1.1 * sh(vec2(id, 5.0)), t = shat;
			vec2 disp = out_ * v * (1.0 - exp(-4.5 * t)) / 4.5 + vec2(0.0, 3.2 * t * t);
			float spin = (sh(vec2(id, 6.0)) - 0.5) * 12.0;
			float rot = spin * (1.0 - exp(-3.0 * t)) / 3.0;
			float flip = cos((0.3 + 1.2 * sh(vec2(id, 7.0))) * t * 3.14159);     // 多数只翻到一半，少数翻过去露出白背
			if (abs(flip) < 0.04) continue;
			vec2 l = q - piv - disp;
			float cs = cos(-rot), sn = sin(-rot);
			l = vec2(cs * l.x - sn * l.y, sn * l.x + cs * l.y);
			l.x /= flip;
			vec2 s0 = piv + l;
			float gap;
			if (bandOf(s0, asp) == k && cellOf(s0, asp, gap) == c && s0.x > -0.05 && s0.y > -0.05 && s0.x < asp + 0.05 && s0.y < 1.05) {
				hit = true;
				src = vec2(s0.x / asp, s0.y);
				back = flip < 0.0 ? 1.0 : 0.0;
				edge = cutDist(s0, asp);
			}
		}
	}
	return hit;
}
";

	private static Shader? _bandShader;

	/// <summary>
	/// 特写（离屏画面）显示到屏幕：zoom 出现时轻推，shat 碎纸（ShatterGlsl），alpha 最后淡掉。
	/// </summary>
	private static Shader BandShader => _bandShader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D tex : filter_linear;
uniform float zoom = 1.0;
uniform float alpha = 1.0;
" + ShatterGlsl + @"
void fragment() {
	vec2 uv = (UV - 0.5) / zoom + 0.5;
	vec2 src; float back, edge;
	vec4 c = vec4(0.0);
	if (shatterSample(uv, src, back, edge)) {
		c = texture(tex, src);
		if (c.a > 0.5 && shat >= 0.0) {
			if (back > 0.5) c = vec4(0.82, 0.8, 0.78, 1.0);                         // 翻到背面：白纸（压暗一点）
			if (edge < 0.004) c = vec4(0.985, 0.985, 0.98, 1.0);                   // 撕口白纸边
			else if (edge < 0.007) c = vec4(0.02, 0.02, 0.03, 1.0);
		}
	}
	COLOR = vec4(c.rgb, c.a * alpha);
}" };
	/// <summary>放一次眼神特写（背景层，人物、敌人在前面）。原版网格画进离屏画面，再用 BandShader 显示。</summary>
	public static void Eyes(Control back)
	{
		if (Fx.Tex("eyes") is not Texture2D tex) return;
		Load();
		Clip clip = _clip!;
		Prepared[] parts = _parts!;
		Vector2 screen = Fx.Screen(back);
		float life = ShatterAt + ShatterTime + 0.05f;
		// 离屏画面：和屏幕一样大，透明底
		SubViewport view = new()
		{
			Size = new Vector2I(Mathf.CeilToInt(screen.X), Mathf.CeilToInt(screen.Y)), TransparentBg = true, Disable3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
		};
		back.AddChild(view);
		Fx.FreeAfter(view, life + 0.1);
		Node2D root = new();
		view.AddChild(root);
		float scale = Mathf.Min(screen.X / clip.Canvas[0], screen.Y / clip.Canvas[1]) * 0.72f;
		root.Position = (screen - new Vector2(clip.Canvas[0], clip.Canvas[1]) * scale) * 0.5f;
		root.Scale = Vector2.One * scale;
		MeshInstance2D?[] nodes = new MeshInstance2D?[parts.Length];
		for (int i = 0; i < parts.Length; i++)
		{
			Prepared p = parts[i];
			// 原版那层线性淡出的白闪：缩小后会变成一个白方框，白闪交给 SummonStage
			if (p.Part.Linear)
			{
				continue;
			}
			MeshInstance2D node = new() { Mesh = p.Mesh, Visible = false };
			if (p.Part.Textured)
			{
				node.Texture = tex;
				node.Material = _opaqueEyes;
				node.TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled;
			}
			root.AddChild(node);
			nodes[i] = node;
		}
		// 显示：一张铺满屏幕的图，用 BandShader
		Node2D shown = Fx.Root(back, Vector2.Zero, "P5CutIn", life);
		back.MoveChild(shown, back.GetChildCount() - 1);
		ShaderMaterial m = new() { Shader = BandShader };
		m.SetShaderParameter("tseed", SummonStage.TearSeed);
		m.SetShaderParameter("sres", screen);
		m.SetShaderParameter("tex", view.GetTexture());
		Sprite2D sprite = new() { Texture = view.GetTexture(), Centered = false, Material = m };
		shown.AddChild(sprite);
		bool hit = false;
		Fx.Animate(shown, 0, life, progress =>
		{
			float s = progress * life;
			// 原版动画只放到停住那一帧（不放原版的合拢）
			float time = Mathf.Min(s * clip.Speed, 1.45f);
			for (int i = 0; i < parts.Length; i++)
			{
				if (nodes[i] is not MeshInstance2D node)
				{
					continue;
				}
				float alpha = Opacity(parts[i].Part, time);
				node.Visible = alpha > 0.0001f;
				node.Modulate = new Color(1f, 1f, 1f, alpha);
			}
			// 图出现时轻轻推一下（1.08 → 1，0.16s 缓出），不做黑白反相（用户 2026-11-01：入场太生硬）
			float k = Mathf.Clamp((s - ImpactAt) / 0.16f, 0f, 1f);
			m.SetShaderParameter("zoom", s < ImpactAt ? 1f : Mathf.Lerp(1.08f, 1f, Fx.OutCubic(k)));
			if (!hit && s >= ImpactAt)
			{
				hit = true;
			}
			// 碎开
			m.SetShaderParameter("shat", s < ShatterAt ? -1f : s - ShatterAt);
			m.SetShaderParameter("alpha", 1f - Mathf.Clamp((s - ShatterAt - ShatterTime * 0.7f) / (ShatterTime * 0.3f), 0f, 1f));
		});
	}
}
