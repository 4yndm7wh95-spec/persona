using Godot;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// 君王之刃（锻造出的环绕大剑）换成 P3 的蓝：给整棵节点树套上色相旋转材质。
/// 原版发光 tween 用的是 modulate（#ff7300），而 canvas 着色器里的 COLOR 已经包含 modulate，所以发光也一起变蓝。
/// 已有自定义 ShaderMaterial 的节点、带粒子序列帧动画的粒子保持原样（换材质会丢掉序列帧 UV）。
/// </summary>
public static class BladeReskin
{
	private static ShaderMaterial? _mix, _add;
	private static ShaderMaterial Mix => _mix ??= new ShaderMaterial { Shader = Shaders.HueShift };
	private static ShaderMaterial Add => _add ??= new ShaderMaterial { Shader = Shaders.HueShiftAdd };

	public static void Apply(Node root) => Walk(root);

	private static void Walk(Node n)
	{
		string cls = n.GetClass();
		if (cls == "SpineSprite")
		{
			n.Set("normal_material", Mix);
			n.Set("additive_material", Add);
		}
		else if (cls != "SpineMesh2D" && n is CanvasItem ci)
		{
			switch (ci.Material)
			{
				case null:
					ci.Material = Mix;
					break;
				case CanvasItemMaterial { ParticlesAnimation: false, BlendMode: CanvasItemMaterial.BlendModeEnum.Add }:
					ci.Material = Add;
					break;
				case CanvasItemMaterial { ParticlesAnimation: false, BlendMode: CanvasItemMaterial.BlendModeEnum.Mix }:
					ci.Material = Mix;
					break;
			}
		}
		foreach (Node c in n.GetChildren())
		{
			Walk(c);
		}
	}
}
