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

	// Godot 4 的 Polygon2D 边缘不做抗锯齿（Antialiased 属性不生效，MSAA 也不一定开）：
	// 用着色器按"到多边形边的距离"把最外 1 个像素淡出 —— 对任意形状（凹的也行）都成立，不会出额外的线或条纹。
	private const int AaMax = 128;
	private const string AaBody = @"
uniform vec2 pts[128];
uniform int n = 0;
varying vec2 lp;
void vertex() { lp = VERTEX; }
void fragment() {
	COLOR *= texture(TEXTURE, UV);
	if (n >= 3) {
		float d = 1e9;
		for (int i = 0; i < 128; i++) {
			if (i >= n) { break; }
			vec2 a = pts[i];
			vec2 ab = pts[(i + 1) % n] - a;
			float t = clamp(dot(lp - a, ab) / max(dot(ab, ab), 1e-6), 0.0, 1.0);
			d = min(d, length(lp - a - ab * t));
		}
		float px = max(length(fwidth(lp)) * 0.7071, 1e-4);
		COLOR.a *= clamp(d / px, 0.0, 1.0);
	}
}";
	private static Shader? _aa, _aaAdd;
	private static Shader AaShader => _aa ??= new Shader { Code = "shader_type canvas_item;\n" + AaBody };
	private static Shader AaAddShader => _aaAdd ??= new Shader { Code = "shader_type canvas_item;\nrender_mode blend_add;\n" + AaBody };

	public static Polygon2D Poly(Node parent, Vector2[] points, Color color, bool additive = false)
	{
		Polygon2D poly = new Polygon2D { Color = color, Material = new ShaderMaterial { Shader = additive ? AaAddShader : AaShader } };
		Reshape(poly, points);
		parent.AddChild(poly);
		return poly;
	}

	/// <summary>改多边形形状（同步抗锯齿着色器里的顶点）。Fx.Poly 创建的多边形改形状都要走这里，不要直接赋 Polygon。</summary>
	public static void Reshape(Polygon2D poly, Vector2[] points)
	{
		poly.Polygon = points;
		if (poly.Material is ShaderMaterial m)
		{
			int n = points.Length <= AaMax ? points.Length : 0;            // 顶点太多就不做抗锯齿
			var arr = new Vector2[AaMax];
			System.Array.Copy(points, arr, System.Math.Min(points.Length, AaMax));
			m.SetShaderParameter("pts", arr);
			m.SetShaderParameter("n", n);
		}
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
		float lifetime, float gravity = 0f, float scaleMin = 0.15f, float scaleMax = 0.45f, bool oneShot = true, Color[]? ramp = null)
	{
		Gradient gradient = ramp == null
			? new Gradient
			{
				Offsets = new[] { 0f, 0.7f, 1f },
				Colors = new[] { Colors.White, Colors.White, new Color(1f, 1f, 1f, 0f) }
			}
			: RampGradient(ramp);
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
			Color = ramp == null ? color : Colors.White,
			ColorRamp = new GradientTexture1D { Gradient = gradient }
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

	/// <summary>把若干颜色均匀分布成渐变，最后一个颜色之后自动淡出到透明。</summary>
	public static Gradient RampGradient(Color[] colors)
	{
		int n = colors.Length + 1;
		float[] offsets = new float[n];
		Color[] cols = new Color[n];
		for (int i = 0; i < colors.Length; i++)
		{
			offsets[i] = (float)i / (n - 1);
			cols[i] = colors[i];
		}
		offsets[n - 1] = 1f;
		cols[n - 1] = new Color(colors[^1], 0f);
		return new Gradient { Offsets = offsets, Colors = cols };
	}

	/// <summary>四角星形（用于星光）。</summary>
	public static Vector2[] Star4(float radius, float inner = 0.28f)
	{
		Vector2[] pts = new Vector2[8];
		for (int i = 0; i < 8; i++)
		{
			float r = i % 2 == 0 ? radius : radius * inner;
			float a = Mathf.Tau * i / 8f - Mathf.Pi / 2f;
			pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
		}
		return pts;
	}

	/// <summary>正六边形（尖朝上）。</summary>
	public static Vector2[] Hexagon(float radius)
	{
		Vector2[] pts = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			float a = Mathf.Tau * i / 6f - Mathf.Pi / 2f;
			pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
		}
		return pts;
	}

	/// <summary>延迟执行（游戏时间），回调包在 Safe 里。</summary>
	public static void After(Node anchor, double seconds, string what, Action action)
	{
		if (seconds <= 0)
		{
			ModEntry.Safe(what, action);
			return;
		}
		anchor.GetTree().CreateTimer(seconds).Timeout += () => ModEntry.Safe(what, action);
	}
}
