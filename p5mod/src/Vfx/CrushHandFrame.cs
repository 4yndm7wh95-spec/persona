using Godot;

namespace P5Joker;

/// <summary>Raymarch once; composite the cached palm and fingers around the paper.</summary>
internal sealed class CrushHandFrame
{
	private static Shader? _composite;
	private static Shader Composite => _composite ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha, unshaded;
uniform sampler2D frame : filter_nearest, repeat_disable;
uniform bool foreground = false;
// The HDR cache packs the binary surface classification in alpha, with a gap
// around 0.5. Decode BEFORE filtering; filtering packed alpha swaps surfaces.
vec4 read_part(ivec2 p, ivec2 size) {
	vec4 c = texelFetch(frame, clamp(p, ivec2(0), size - ivec2(1)), 0);
	bool front = c.a > 0.5;
	float a = clamp((c.a - (front ? 0.51 : 0.0)) / 0.49, 0.0, 1.0);
	return front == foreground ? vec4(c.rgb, a) : vec4(0.0);
}
void fragment() {
	ivec2 size = textureSize(frame, 0);
	vec2 px = UV * vec2(size) - 0.5;
	ivec2 p = ivec2(floor(px));
	vec2 f = fract(px);
	vec4 a = mix(read_part(p, size), read_part(p + ivec2(1,0), size), f.x);
	vec4 b = mix(read_part(p + ivec2(0,1), size), read_part(p + ivec2(1,1), size), f.x);
	COLOR = mix(a, b, f.y);
}" };

	public CrushHand Hand { get; }
	private readonly SubViewport _view;
	private readonly ColorRect _rear, _front;

	public CrushHandFrame(Node2D owner, Control rear, Control front, Vector2 screen)
	{
		// Fixed quality, not a frame-rate throttle: the volumetric smoke updates
		// every rendered frame. Only this effect is capped at 720p.
		float k = Mathf.Min(1f, 720f / screen.Y);
		Vector2I size = new(Mathf.Max(1, Mathf.RoundToInt(screen.X * k)), Mathf.Max(1, Mathf.RoundToInt(screen.Y * k)));
		_view = new SubViewport { Name = "P5HandCache", Size = size, TransparentBg = true, Disable3D = true,
			UseHdr2D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled };
		owner.AddChild(_view);
		Control input = new() { Scale = new Vector2(size.X / screen.X, size.Y / screen.Y) };
		_view.AddChild(input);
		Hand = new CrushHand(input, Vector2.Zero, screen, packed: true);
		ColorRect Layer(Control parent, bool foreground)
		{
			ShaderMaterial m = new() { Shader = Composite };
			m.SetShaderParameter("frame", _view.GetTexture());
			m.SetShaderParameter("foreground", foreground);
			ColorRect rect = new() { Size = screen, Material = m, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
			parent.AddChild(rect);
			return rect;
		}
		_rear = Layer(rear, false);
		_front = Layer(front, true);
	}

	public void SetVisible(bool visible)
	{
		_view.RenderTargetUpdateMode = visible ? SubViewport.UpdateMode.Always : SubViewport.UpdateMode.Disabled;
		_rear.Visible = visible;
		_front.Visible = visible;
	}
}
