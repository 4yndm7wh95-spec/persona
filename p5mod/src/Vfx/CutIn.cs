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

	/// <summary>Use the existing summon trigger/background layer and native speed (2.5).</summary>
	public static void Eyes(Control back)
	{
		if (Fx.Tex("eyes") is not Texture2D tex) return;
		Load();
		Clip clip = _clip!;
		Prepared[] parts = _parts!;
		float seconds = clip.Duration / clip.Speed;
		Vector2 screen = Fx.Screen(back);
		Node2D root = Fx.Root(back, Vector2.Zero, "P5CutIn", seconds + 0.1);
		root.GlobalPosition = Vector2.Zero;
		root.Scale = new Vector2(screen.X / clip.Canvas[0], screen.Y / clip.Canvas[1]);
		MeshInstance2D[] nodes = new MeshInstance2D[parts.Length];
		// Exported meshes are sorted by original world Z, including outlines.
		for (int i = 0; i < parts.Length; i++)
		{
			Prepared p = parts[i];
			MeshInstance2D node = new() { Mesh = p.Mesh, Visible = false };
			if (p.Part.Textured)
			{
				node.Texture = tex;
				node.Material = _opaqueEyes;
				// Source UVs extend slightly below zero; both original wrap axes repeat.
				node.TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled;
			}
			root.AddChild(node);
			nodes[i] = node;
		}
		Fx.Animate(root, 0, seconds, progress =>
		{
			float time = progress * clip.Duration;
			for (int i = 0; i < parts.Length; i++)
			{
				float alpha = Opacity(parts[i].Part, time);
				nodes[i].Visible = alpha > 0.0001f;
				nodes[i].Modulate = new Color(1f, 1f, 1f, alpha);
			}
		});
	}
}
