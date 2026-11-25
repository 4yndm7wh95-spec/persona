using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>The dust samples the stop/resume waves at each mote instead of searching for motes in every screen pixel.</summary>
internal sealed class TimeStopDust
{
	private static MultiMesh? _mesh;
	private static int _meshWidth;
	private static Shader? _shader;
	private readonly MultiMeshInstance2D _node;
	private readonly ShaderMaterial _material;
	private Vector2 _size;

	internal TimeStopDust(Node parent)
	{
		_material = new ShaderMaterial { Shader = _shader ??= new Shader { Code = DustSource } };
		_node = new MultiMeshInstance2D { Name = "P5TimeStopDust", Material = _material };
		parent.AddChild(_node);
	}

	internal void Resize(Vector2 size, Vector2 center, float tall)
	{
		if (_size != size)
		{
			float scale = size.Y / 1080f;
			_node.Scale = Vector2.One * scale;
			_node.Multimesh = MeshFor(Mathf.CeilToInt(size.X / scale));
			_material.SetShaderParameter("aspect", size.X / size.Y);
			_size = size;
		}
		_material.SetShaderParameter("center", center);
		_material.SetShaderParameter("tall", tall);
	}

	internal void Step(float time, float resumeAt, float pulseAge, float stopRadius, float goRadius, float amount)
	{
		_material.SetShaderParameter("t", time);
		_material.SetShaderParameter("go_at", resumeAt);
		_material.SetShaderParameter("pulse_s", pulseAge);
		_material.SetShaderParameter("stop_r", stopRadius);
		_material.SetShaderParameter("go_r", goRadius);
		_material.SetShaderParameter("amount", amount);
	}

	private static MultiMesh MeshFor(int width)
	{
		if (_mesh != null && width <= _meshWidth) return _mesh;
		width = Math.Max(1920, width);
		List<Color> points = new();
		for (int layer = 0; layer < 3; layer++)
		{
			float cellSize = 70f - 18f * layer;
			for (int y = -1; y <= Mathf.CeilToInt(1080 / cellSize); y++)
			for (int x = -1; x <= Mathf.CeilToInt(width / cellSize); x++)
			{
				Vector2 cell = new(x, y);
				float seed = Hash(cell + Vector2.One * (layer * 31.7f));
				if (seed > 0.42f) continue;
				Vector2 origin = (cell + Vector2.One * 0.15f + new Vector2(
					Hash(cell + Vector2.One * (layer * 7.3f)),
					Hash(cell + Vector2.One * (layer * 7.3f + 17.31f))) * 0.7f) * cellSize;
				points.Add(new Color(origin.X, origin.Y, seed, layer));
			}
		}
		MultiMesh mesh = new()
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform2D,
			UseCustomData = true,
			Mesh = new QuadMesh { Size = new Vector2(2f, 2f) },
			CustomAabb = new Aabb(new Vector3(-100f, -100f, -1f), new Vector3(width + 200f, 1280f, 2f)),
			InstanceCount = points.Count,
		};
		// Two padded transform rows followed by four custom floats.
		float[] data = new float[points.Count * 12];
		for (int i = 0; i < points.Count; i++)
		{
			int j = i * 12;
			data[j] = data[j + 5] = 1f;
			Color point = points[i];
			data[j + 8] = point.R;
			data[j + 9] = point.G;
			data[j + 10] = point.B;
			data[j + 11] = point.A;
		}
		mesh.Buffer = data;
		_meshWidth = width;
		return _mesh = mesh;
	}

	private static float Fract(float x) => x - MathF.Floor(x);

	private static float Hash(Vector2 p)
	{
		float a = Fract(p.X * 0.1031f), b = Fract(p.Y * 0.1031f);
		float shift = a * (b + 33.33f) + b * (a + 33.33f) + a * (a + 33.33f);
		return Fract((a + b + 2f * shift) * (a + shift));
	}

	private const string DustSource = @"
shader_type canvas_item;
render_mode blend_add;
uniform float aspect = 1.7778;
uniform vec2 center;
uniform float tall = 0.33;
uniform float t = 0.0;
uniform float go_at = 100000.0;
uniform float pulse_s = 10.0;
uniform float stop_r = 0.0;
uniform float go_r = -1.0;
uniform float amount = 0.0;
varying float strength;

float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float arrive(float d, float duration) { return duration * (1.0 - sqrt(max(0.0, 1.0 - clamp(d / 7.5, 0.0, 1.0)))); }
float distance_from_joker(vec2 p) {
	return length((p / 1080.0 - center * vec2(aspect, 1.0)) * vec2(1.0, 1.15) / tall);
}

void vertex() {
	vec2 base = INSTANCE_CUSTOM.xy;
	float h = INSTANCE_CUSTOM.z, layer = INSTANCE_CUSTOM.w;
	float cs = 70.0 - 18.0 * layer;
	float d = distance_from_joker(base);
	float tf = arrive(d, 0.75), tg = go_at + arrive(d, 0.55);
	float tau = min(t, tf) + max(0.0, min(t, tg) - tf) * 0.07 + max(0.0, t - tg);
	vec2 p = base + vec2(sin(tau * (0.5 + h) + h * 40.0) * cs * 0.22, sin(tau * (0.4 + 0.6 * h) + h * 17.0) * cs * 0.18);
	if (pulse_s >= 0.0 && pulse_s < 1.5) {
		float ripple = (d - pulse_s * 3.2) / 0.6;
		vec2 dir = normalize(base / (1080.0 * vec2(aspect, 1.0)) - center + 1e-5);
		p += dir * 9.0 * exp(-ripple * ripple - pulse_s * 1.2);
	}
	float radius = 0.7 + 1.5 * hash(floor(base / cs) + 5.1 + layer);
	float here = distance_from_joker(p);
	float frozen = smoothstep(stop_r + 0.05, stop_r - 0.3, here) *
		(1.0 - smoothstep(go_r + 0.05, go_r - 0.35, here) * step(go_at, t));
	float flash = 0.55 + 0.45 * sin(t * (0.6 + 1.2 * h) + h * 60.0);
	strength = flash * (0.35 + 0.65 * h / 0.42) * (0.5 + 0.25 * layer) *
		(0.25 + 0.75 * frozen) * smoothstep(0.0, 0.25, t) * (1.0 - smoothstep(go_at, go_at + 1.15, t)) * amount;
	VERTEX = p + VERTEX * radius * 4.0;
}

void fragment() {
	float d = length(UV * 2.0 - 1.0);
	float light = smoothstep(0.25, 0.0625, d) + 0.18 * smoothstep(1.0, 0.0, d);
	COLOR = vec4(vec3(0.78, 0.86, 1.0), light * strength * 0.3);
}";
}
