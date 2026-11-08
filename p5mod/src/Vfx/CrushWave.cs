using Godot;

namespace P5Joker;

/// <summary>P3Makoto Uniq.BigBang → P3Fx.Shockwave / Shaders.Ring, recolored red/white.</summary>
public static class CrushWave
{
	private static Shader? _shader;
	private static Shader Ring => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(1.0, 0.04, 0.12, 1.0);
uniform float radius = 0.04;
uniform float width = 0.05;
uniform float alpha = 0.0;
void fragment() {
	float d = length(UV * 2.0 - 1.0);
	float core = 1.0 - smoothstep(0.0, width, abs(d - radius));
	float glow = (1.0 - smoothstep(0.0, width * 5.0, abs(d - radius))) * 0.35;
	float edge = 1.0 - smoothstep(0.8, 1.0, d);
	COLOR = vec4(tint.rgb * (1.0 + core), (core + glow) * alpha * tint.a * edge);
}" };

	public static void Play(Control back, Vector2 screenAt)
	{
		Node2D root = Fx.ScreenRoot(back, "P5CrushWave");
		Color[] colors = { new(1f, 0.85f, 0.82f), new(1f, 0.035f, 0.1f), new(1f, 0.35f, 0.4f), new(0.9f, 0.025f, 0.1f), new(1f, 0.1f, 0.25f) };
		float unit = Fx.Screen(back).Y / 1080f;
		for (int i = 0; i < colors.Length; i++)
		{
			float size = (600f + 500f * i) * unit, duration = 0.7f + 0.1f * i;
			ShaderMaterial mat = new() { Shader = Ring };
			mat.SetShaderParameter("tint", colors[i]);
			ColorRect quad = new() { Position = screenAt - Vector2.One * size / 2f, Size = Vector2.One * size,
				Material = mat, MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(quad);
			Fx.Animate(quad, 0.04f * i, duration, t =>
			{
				float e = t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
				mat.SetShaderParameter("radius", Mathf.Lerp(0.04f, 0.95f, e));
				mat.SetShaderParameter("width", Mathf.Lerp(0.05f, 0.008f, e));
				mat.SetShaderParameter("alpha", 1f - Fx.InCubic(t));
			});
		}
		Fx.FreeAfter(root, 1.4);
	}
}
