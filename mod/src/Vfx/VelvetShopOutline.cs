using Godot;

namespace P3Makoto.Vfx;

internal static class VelvetShopOutline
{
    public static void Attach(Control background, Control merchant, ShaderMaterial material)
    {
        var ripple = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type canvas_item;
uniform float strength = 0.0;
void fragment() {
    vec2 p = (UV - vec2(.5,.58)) / vec2(.105,.021);
    float r = length(p);
    float phase = fract(TIME * .72);
    float steady = 1.0 - smoothstep(.018,.065,abs(r - .81));
    float wave = (1.0 - smoothstep(.012,.06,abs(r - mix(.6,1.22,phase)))) * sin(phase * 3.14159);
    COLOR = vec4(.51,.92,1.0, strength * (.65 * steady + .58 * wave));
}" } };
        var ring = new ColorRect { Material = ripple, MouseFilter = Control.MouseFilterEnum.Ignore };
        background.AddChild(ring);
        ring.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        bool pointer = false, focus = false;
        Tween? tween = null;
        void Refresh()
        {
            float target = pointer || focus ? 1f : 0f;
            material.SetShaderParameter("hover_glow",target);
            tween?.Kill();
            tween = background.CreateTween();
            tween.TweenProperty(ripple,"shader_parameter/strength",target,.15);
        }
        merchant.MouseEntered += () => { pointer = true; Refresh(); };
        merchant.MouseExited += () => { pointer = false; Refresh(); };
        merchant.FocusEntered += () => { focus = true; Refresh(); };
        merchant.FocusExited += () => { focus = false; Refresh(); };
        merchant.Connect("Released", Callable.From<Control>(_ => { pointer = focus = false; Refresh(); }));
    }
}
