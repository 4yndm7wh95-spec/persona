using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P5Joker;

/// <summary>
/// 特效基础工具。只用引擎自带的节点（Node2D / Polygon2D / Line2D / Sprite2D），动画全靠 Tween：
/// mod 程序集是运行时加载的，自定义 Node 子类可能收不到 _Process 回调（同 P3Makoto 的约定）。
/// </summary>
public static class Fx
{
	public static readonly Random Rng = new();

	public static float Rand(float min, float max) => min + (float)Rng.NextDouble() * (max - min);

	private static CanvasItemMaterial? _additive;

	public static CanvasItemMaterial Additive => _additive ??= new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };

	/// <summary>P5R 原版特效贴图（tools/p5/stage_p5.py 打进 pck 的 res://p5joker/fx/）。</summary>
	public static Texture2D? Tex(string name)
	{
		string path = $"res://p5joker/fx/{name}.png";
		return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
	}

	public static Node2D Root(Control layer, Vector2 globalPos, string name, double life)
	{
		Node2D root = new() { Name = name };
		layer.AddChild(root);
		root.GlobalPosition = globalPos;
		FreeAfter(root, life);
		return root;
	}

	public static void FreeAfter(Node node, double seconds)
	{
		node.GetTree()?.CreateTimer(seconds).Connect("timeout", Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(node))
			{
				node.QueueFree();
			}
		}));
	}

	/// <summary>delay 秒后用 0→1 的进度驱动动画（step 每帧调用）。</summary>
	public static void Animate(Node owner, double delay, double seconds, Action<float> step)
	{
		step(0f);
		Tween tween = owner.CreateTween();
		if (delay > 0)
		{
			tween.TweenInterval(delay);
		}
		tween.TweenMethod(Callable.From<float>(t =>
		{
			if (GodotObject.IsInstanceValid(owner))
			{
				step(t);
			}
		}), 0f, 1f, seconds);
	}

	public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

	public static float InCubic(float t) => t * t * t;

	public static Polygon2D Poly(Node parent, Vector2[] points, Color color, bool additive = false)
	{
		Polygon2D p = new() { Polygon = points, Color = color };
		if (additive)
		{
			p.Material = Additive;
		}
		parent.AddChild(p);
		return p;
	}

	public static Sprite2D Sprite(Node parent, Texture2D tex, bool additive = true)
	{
		Sprite2D s = new() { Texture = tex };
		if (additive)
		{
			s.Material = Additive;
		}
		parent.AddChild(s);
		return s;
	}

	public static Vector2[] Circle(float r, int n = 40)
	{
		Vector2[] pts = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			pts[i] = Vector2.Right.Rotated(Mathf.Tau * i / n) * r;
		}
		return pts;
	}

	/// <summary>锯齿星形（P5 的冲击爆点）：spikes 个尖，内外半径交替并带一点随机，看起来是手绘的不规则形。</summary>
	public static Vector2[] Burst(float outer, float inner, int spikes, float jitter = 0.25f)
	{
		Vector2[] pts = new Vector2[spikes * 2];
		float a0 = Rand(0f, Mathf.Tau);
		for (int i = 0; i < spikes * 2; i++)
		{
			float r = i % 2 == 0 ? outer * (1f + Rand(-jitter, jitter)) : inner * (1f + Rand(-jitter, jitter) * 0.5f);
			pts[i] = Vector2.Right.Rotated(a0 + Mathf.Tau * i / (spikes * 2)) * r;
		}
		return pts;
	}

	public static void Shake(ShakeStrength s) => NGame.Instance?.ScreenShake(s, ShakeDuration.Short);
}
