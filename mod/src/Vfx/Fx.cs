using System;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// 特效基础工具。
/// 约定：只使用引擎自带节点（Node2D / Polygon2D / Line2D / ColorRect / GpuParticles2D / Timer），
/// 动画全部靠 Tween 和 lambda 驱动，不定义自定义 Node 子类。
/// 原因：mod 程序集是运行时加载的，自定义 Node 子类可能收不到 _Process / _Draw 回调。
/// </summary>
public static class Fx
{
	public static readonly Random Rng = new();

	public static float Rand(float min, float max) => min + (float)Rng.NextDouble() * (max - min);

	public static Vector2 RandDir() => Vector2.Right.Rotated(Rand(0f, Mathf.Tau));

	private static CanvasItemMaterial? _additive;

	public static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial
	{
		BlendMode = CanvasItemMaterial.BlendModeEnum.Add
	};

	/// <summary>在特效层上新建一个根节点，放在指定全局坐标。</summary>
	public static Node2D Root(Control layer, Vector2 globalPos, string name)
	{
		Node2D root = new Node2D { Name = name };
		layer.AddChild(root);
		root.GlobalPosition = globalPos;
		return root;
	}

	public static void FreeAfter(Node node, double seconds)
	{
		SceneTree? tree = node.GetTree();
		if (tree == null)
		{
			return;
		}
		tree.CreateTimer(seconds).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(node))
			{
				node.QueueFree();
			}
		};
	}

	/// <summary>用 0→1 的进度驱动任意动画。</summary>
	public static Tween Animate(Node owner, double seconds, Action<float> step)
	{
		Tween tween = owner.CreateTween();
		tween.TweenMethod(Callable.From<float>(t =>
		{
			if (GodotObject.IsInstanceValid(owner))
			{
				step(t);
			}
		}), 0f, 1f, seconds);
		return tween;
	}

	public static Polygon2D Poly(Node parent, Vector2[] points, Color color, bool additive = false)
	{
		Polygon2D poly = new Polygon2D { Polygon = points, Color = color };
		if (additive)
		{
			poly.Material = Additive;
		}
		parent.AddChild(poly);
		return poly;
	}

	public static Line2D Line(Node parent, Vector2[] points, Color color, float width, bool closed = false, bool additive = true)
	{
		Line2D line = new Line2D
		{
			Points = points,
			DefaultColor = color,
			Width = width,
			Closed = closed,
			JointMode = Line2D.LineJointMode.Sharp,
			Antialiased = true
		};
		if (additive)
		{
			line.Material = Additive;
		}
		parent.AddChild(line);
		return line;
	}

	public static Vector2[] Circle(float radius, int segments = 32, float squashY = 1f)
	{
		Vector2[] pts = new Vector2[segments];
		for (int i = 0; i < segments; i++)
		{
			float a = Mathf.Tau * i / segments;
			pts[i] = new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * squashY);
		}
		return pts;
	}

	/// <summary>挂一个使用代码着色器的方形面片，中心对齐父节点原点。</summary>
	public static ColorRect ShaderQuad(Node parent, Shader shader, float size, float squashY = 1f)
	{
		ColorRect rect = new ColorRect
		{
			Size = new Vector2(size, size),
			Position = new Vector2(-size / 2f, -size / 2f),
			PivotOffset = new Vector2(size / 2f, size / 2f),
			Scale = new Vector2(1f, squashY),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = shader }
		};
		parent.AddChild(rect);
		return rect;
	}

	public static void SetParam(ColorRect rect, string name, Variant value)
	{
		((ShaderMaterial)rect.Material).SetShaderParameter(name, value);
	}

	private static GradientTexture2D? _softDot;

	/// <summary>径向柔光点贴图（代码生成，不依赖图片资源）。</summary>
	public static GradientTexture2D SoftDot => _softDot ??= new GradientTexture2D
	{
		Gradient = new Gradient
		{
			Offsets = new[] { 0f, 0.35f, 1f },
			Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.45f), new Color(1f, 1f, 1f, 0f) }
		},
		Fill = GradientTexture2D.FillEnum.Radial,
		FillFrom = new Vector2(0.5f, 0.5f),
		FillTo = new Vector2(1f, 0.5f),
		Width = 32,
		Height = 32
	};

	/// <summary>一次性喷发的柔光粒子。</summary>
	public static GpuParticles2D Sparks(Node parent, Color color, int amount, float speedMin, float speedMax,
		float lifetime, float gravity = 0f, float scaleMin = 0.15f, float scaleMax = 0.45f, bool oneShot = true)
	{
		ParticleProcessMaterial pm = new ParticleProcessMaterial
		{
			Direction = new Vector3(0f, -1f, 0f),
			Spread = 180f,
			InitialVelocityMin = speedMin,
			InitialVelocityMax = speedMax,
			Gravity = new Vector3(0f, gravity, 0f),
			ScaleMin = scaleMin,
			ScaleMax = scaleMax,
			DampingMin = 2f,
			DampingMax = 6f,
			Color = color,
			ColorRamp = new GradientTexture1D
			{
				Gradient = new Gradient
				{
					Offsets = new[] { 0f, 0.7f, 1f },
					Colors = new[] { Colors.White, Colors.White, new Color(1f, 1f, 1f, 0f) }
				}
			}
		};
		GpuParticles2D p = new GpuParticles2D
		{
			Amount = amount,
			Lifetime = lifetime,
			OneShot = oneShot,
			Explosiveness = oneShot ? 0.95f : 0f,
			ProcessMaterial = pm,
			Texture = SoftDot,
			Material = Additive,
			LocalCoords = false,
			Emitting = true
		};
		parent.AddChild(p);
		return p;
	}
}
