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
	private static readonly System.Collections.Generic.Dictionary<string, Texture2D?> Cache = new();

	public static Texture2D? Tex(string name)
	{
		if (!Cache.TryGetValue(name, out Texture2D? tex))
		{
			string path = $"res://p5joker/fx/{name}.png";
			tex = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
			Cache[name] = tex;
		}
		return tex;
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
		Tween tween = owner.CreateTween();
		if (delay > 0)
		{
			// 等待期间藏起来（否则贴图会先在默认位置闪一下），开始时再显示
			bool shown = owner is CanvasItem ci0 && ci0.Visible;
			if (owner is CanvasItem hide)
			{
				hide.Visible = false;
			}
			tween.TweenInterval(delay);
			tween.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(owner))
				{
					if (shown && owner is CanvasItem show)
					{
						show.Visible = true;
					}
					step(0f);
				}
			}));
		}
		else
		{
			step(0f);
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

	/// <summary>delay 秒后执行（节点没了就不执行）。</summary>
	public static void After(Node anchor, double delay, Action action)
	{
		if (delay <= 0)
		{
			ModEntry.Safe("fx", action);
			return;
		}
		anchor.GetTree()?.CreateTimer(delay).Connect("timeout", Callable.From(() =>
		{
			if (GodotObject.IsInstanceValid(anchor))
			{
				ModEntry.Safe("fx", action);
			}
		}));
	}

	public static Vector2 Screen(Node any) => any.GetViewport()?.GetVisibleRect().Size ?? new Vector2(1920f, 1080f);

	/// <summary>白字黑描边的文字（P5 的字都是硬边粗体）。</summary>
	public static Label Text(Node parent, string text, int size, Color color, Color outline, int outlineSize = 8)
	{
		Label l = new()
		{
			Text = text,
			LabelSettings = new LabelSettings { FontSize = size, FontColor = color, OutlineColor = outline, OutlineSize = outlineSize },
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		parent.AddChild(l);
		return l;
	}

	/// <summary>平行四边形（P5 的斜切色块）：左下角在原点，宽 w、高 h，顶边往右错开 skew。</summary>
	public static Vector2[] Slant(float w, float h, float skew) =>
		new[] { new Vector2(0f, 0f), new Vector2(w, 0f), new Vector2(w + skew, -h), new Vector2(skew, -h) };

	public static Vector2[] Arrow(float w, float h) =>
		new[] { new Vector2(0f, -h), new Vector2(w, -h * 0.35f), new Vector2(w * 0.4f, -h * 0.35f), new Vector2(w * 0.4f, h * 0.5f),
			new Vector2(-w * 0.4f, h * 0.5f), new Vector2(-w * 0.4f, -h * 0.35f), new Vector2(-w, -h * 0.35f) };

	/// <summary>贴图集里第 i 帧（cols 列、每帧 w x h）。</summary>
	public static Rect2 Frame(int i, int cols, float w, float h) => new(i % cols * w, i / cols * h, w, h);
}
