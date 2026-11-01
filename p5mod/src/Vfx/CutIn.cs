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

	/// <summary>图出现（冲击帧）/ 劈开的时刻（秒）；SummonStage 用同一刻、同一条撕口。</summary>
	public const float ImpactAt = 0.2f, SplitAt = 0.91f, SplitTime = 0.12f;     // 用户 2026-11-01：背景要撑到出手、咒焰、手都演完再劈开

	/// <summary>
	/// 劈开用的撕口和两半的位移（两个着色器共用）：一条顺着带子斜度的锯齿线穿过画面中间，
	/// 上半往右上、下半往左下飞；split 0～1 是飞了多远。
	/// </summary>
	public const string TearGlsl = @"
uniform float split = 0.0;
uniform float tseed = 0.0;
float th(float x) { vec3 p3 = fract(vec3(x, tseed, x * 0.37) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float tln(float x) { float i = floor(x); return mix(th(i), th(i + 1.0), fract(x)); }
float tearLine(float u) { return 0.5 - (u - 0.5) * 0.3 + (tln(u * 26.0) - 0.5) * 0.035 + (tln(u * 90.0 + 5.0) - 0.5) * 0.012 + pow(tln(u * 9.0 + 3.0), 6.0) * 0.04; }
vec2 tearOff() { float k = split; return vec2(0.75, -0.42) * k; }
float tearPaper(float u) { return (0.006 + pow(tln(u * 40.0 + 2.0), 3.0) * 0.014) * step(0.0005, split); }
";

	private static Shader? _bandShader;

	/// <summary>
	/// 特写（离屏画面）显示到屏幕：zoom 砸进来的缩放（以屏幕中心），invert 冲击帧（黑白反相），
	/// split 劈开（上下两半各自飞走，撕口白纸边，飞得快时红绿蓝错开），alpha 最后淡掉。
	/// </summary>
	private static Shader BandShader => _bandShader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D tex : filter_linear;
uniform float zoom = 1.0;
uniform float invert = 0.0;
uniform float chroma = 0.0;
uniform float alpha = 1.0;
" + TearGlsl + @"
vec4 tap(vec2 uv, vec2 d) {
	vec4 c = texture(tex, uv);
	if (chroma > 0.0) { c.r = texture(tex, uv + d * chroma).r; c.b = texture(tex, uv - d * chroma).b; }
	return c;
}
void fragment() {
	vec2 uv = (UV - 0.5) / zoom + 0.5;
	vec2 off = tearOff();
	vec2 d = normalize(vec2(0.75, -0.42)) * 0.01;
	vec4 c = vec4(0.0);
	vec2 uU = uv - off, uL = uv + off;
	float lU = tearLine(uU.x), lL = tearLine(uL.x);
	if (uU.y < lU) {
		c = tap(uU, d);
		if (uU.y > lU - tearPaper(uU.x) && texture(tex, vec2(uU.x, lU - 0.03)).a > 0.5) c = vec4(0.985, 0.985, 0.98, 1.0);
	} else if (uL.y > lL) {
		c = tap(uL, -d);
		if (uL.y < lL + tearPaper(uL.x + 7.0) && texture(tex, vec2(uL.x, lL + 0.03)).a > 0.5) c = vec4(0.985, 0.985, 0.98, 1.0);
	}
	// 冲击帧：黑白反相（漫画的负片闪）
	float lum = dot(c.rgb, vec3(0.299, 0.587, 0.114));
	c.rgb = mix(c.rgb, vec3(step(lum, 0.5)), invert);
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
		float life = SplitAt + SplitTime + 0.05f;
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
			m.SetShaderParameter("invert", 0f);
			if (!hit && s >= ImpactAt)
			{
				hit = true;
			}
			// 劈开
			float e = Mathf.Clamp((s - SplitAt) / SplitTime, 0f, 1f);
			m.SetShaderParameter("split", SummonStage.SplitCurve(e));
			m.SetShaderParameter("chroma", e > 0f ? (1f - e) * 1.2f : 0f);
			m.SetShaderParameter("alpha", 1f - Mathf.Clamp((e - 0.7f) / 0.3f, 0f, 1f));
		});
	}
}
