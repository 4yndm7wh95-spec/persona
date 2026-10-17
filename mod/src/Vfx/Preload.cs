using System.Collections.Generic;
using Godot;

namespace P3Makoto.Vfx;

/// <summary>
/// 预加载：选中结城理那一刻要用到的普通资源（中文字体 17MB、介绍板 / 切入 / 立绘贴图）在 mod 初始化时放到后台线程加载，
/// 加载完一直持有引用（不被资源缓存释放）；同时在屏幕角落用几乎透明的小节点画几帧，让常用着色器提前编译。
/// 减少第一次选中时同步读盘 / 编译着色器的掉帧。Spine 不参与启动期的线程加载，以避免与 Logo 预加载竞争。
/// </summary>
public static class Preload
{
	private static readonly string[] Paths =
	{
		"res://fonts/jpn/NotoSansCJKjp-Bold.otf",
		"res://fonts/jpn/NotoSansCJKjp-Medium.otf",
		"res://p3makoto/ui/info_head.png",
		"res://p3makoto/ui/info_head_ghost.png",
		"res://p3makoto/ui/cutin_eyes.png",
		"res://p3makoto/ui/cutin_ka.png",
		"res://p3makoto/ui/charselect_portrait.png",
		PortraitSpine,                                     // 选人界面的 Spine 立绘（骨骼数据 + 图集）
		"res://p3makoto/pile/pile_skel_data.tres",         // v0.14.49 牌堆界面的 Spine 立绘（第一次打开牌堆时不卡）
		"res://p3makoto/ui/pile_portrait.png",
		"res://p3makoto/pile_deck/pile_deck_skel_data.tres",       // v0.14.56 牌组 / 弃牌堆 / 消耗堆界面的立绘
		"res://p3makoto/pile_discard/pile_discard_skel_data.tres",
		"res://p3makoto/pile_exhaust/pile_exhaust_skel_data.tres",
	};
	private const string PortraitSpine = "res://p3makoto/portrait/portrait_skel_data.tres";

	private static readonly List<Resource> Keep = new();
	private static readonly List<string> Pending = new();
	private static bool _started;
	private static ulong _t0;
	internal static bool OrdinaryReady => _started && Pending.Count == 0;

	public static void Start()
	{
		if (_started || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		_started = true;
		_t0 = Time.GetTicksMsec();
		// Spine 的导入器包含图集/骨骼依赖，不能在启动阶段和原版 Logo 资源争用并行加载任务。
		// 两次黑屏记录都停在 IntroLogo，同时三个 Spine 资源一直没有 ready；普通贴图/字体已完成。
		// 启动期仅预热普通资源；Spine 等主菜单稳定后在主线程逐项预热，仍保留界面按需加载和静态图回退。
		GD.Print($"{ModEntry.Tag} perf preload: startup threaded Spine warmup disabled; serial idle warmup scheduled");
		foreach (string p in Paths)
		{
			if (p.EndsWith("_skel_data.tres", System.StringComparison.Ordinal))
			{
				continue;
			}
			if (ResourceLoader.Exists(p) && ResourceLoader.LoadThreadedRequest(p, "", false) == Error.Ok)
			{
				Pending.Add(p);
			}
		}
		tree.ProcessFrame += Poll;
		// 着色器预编译：等场景树空闲后加一个几乎透明的 2×2 小节点，画 6 帧后删掉
		Callable.From(() => ModEntry.Safe("shader warmup", Warm)).CallDeferred();
		IdleWarmup.Start();                      // 主菜单稳定且原版队列空闲后，再逐项预热牌堆立绘。
	}

	private static void Poll()
	{
		for (int i = Pending.Count - 1; i >= 0; i--)
		{
			string p = Pending[i];
			ResourceLoader.ThreadLoadStatus st = ResourceLoader.LoadThreadedGetStatus(p);
			if (st == ResourceLoader.ThreadLoadStatus.Loaded)
			{
				if (ResourceLoader.LoadThreadedGet(p) is Resource r)
				{
					Keep.Add(r);
					if (p == PortraitSpine)
					{
						ModEntry.Safe("spine warmup", () => WarmSpine(r));
					}
				}
				GD.Print($"{ModEntry.Tag} perf preload {p} ready at +{Time.GetTicksMsec() - _t0}ms");
				Pending.RemoveAt(i);
			}
			else if (st != ResourceLoader.ThreadLoadStatus.InProgress)
			{
				Pending.RemoveAt(i);
			}
		}
		if (Pending.Count == 0 && Engine.GetMainLoop() is SceneTree tree)
		{
			tree.ProcessFrame -= Poll;
		}
	}

	/// <summary>Spine 立绘：数据加载完后在屏幕外实例化一次（解析骨骼 / 建立皮肤的开销挪到启动时），停 3 帧后删掉。</summary>
	private static void WarmSpine(Resource data)
	{
		if (Engine.GetMainLoop() is not SceneTree tree || ClassDB.Instantiate("SpineSprite").AsGodotObject() is not Node2D spine)
		{
			return;
		}
		CanvasLayer layer = new() { Layer = 128 };
		tree.Root.AddChild(layer);
		spine.Position = new Vector2(-4000f, -4000f);
		spine.Modulate = new Color(1f, 1f, 1f, 0.004f);
		spine.Set("skeleton_data_res", data);
		layer.AddChild(spine);
		int frames = 0;
		void Tick()
		{
			if (++frames < 3)
			{
				return;
			}
			tree.ProcessFrame -= Tick;
			if (GodotObject.IsInstanceValid(layer))
			{
				layer.QueueFree();
			}
			GD.Print($"{ModEntry.Tag} perf spine portrait warmed at +{Time.GetTicksMsec() - _t0}ms");
		}
		tree.ProcessFrame += Tick;
	}

	private static void Warm()
	{
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		CanvasLayer layer = new() { Layer = 128 };
		tree.Root.AddChild(layer);
		Node2D n = new() { Position = new Vector2(1f, 1f), Modulate = new Color(1f, 1f, 1f, 0.004f) };
		layer.AddChild(n);
		Fx.Poly(n, new[] { Vector2.Zero, new Vector2(2f, 0f), new Vector2(2f, 2f) }, Colors.White);
		Fx.Poly(n, new[] { Vector2.Zero, new Vector2(2f, 0f), new Vector2(2f, 2f) }, Colors.White, additive: true);
		Label l = Typo.Text(n, "0", Typo.Latin, 8, Colors.White, Vector2.Zero);
		l.Material = Typo.Gradient(Colors.White, Colors.White, 8f);
		ColorRect disc = P3Ui.WaterDisc(n, Vector2.One, 1f);
		_ = disc;
		// 选人界面：夜空着色器、上升光点的粒子着色器、介绍板（字体 / 扫光 / 文字栅格化）
		n.AddChild(new ColorRect { Size = new Vector2(2f, 2f), Material = new ShaderMaterial { Shader = Shaders.Menu }, MouseFilter = Control.MouseFilterEnum.Ignore });
		Fx.Sparks(n, Pal.Cyan, 4, 1f, 2f, 0.3f, gravity: 0f, scaleMin: 0.01f, scaleMax: 0.02f, oneShot: false);
		ModEntry.Safe("charselect info warmup", () => Scenes.CharSelectInfo.Warm(n));
		int frames = 0;
		void Tick()
		{
			if (++frames < 6)
			{
				return;
			}
			tree.ProcessFrame -= Tick;
			if (GodotObject.IsInstanceValid(layer))
			{
				layer.QueueFree();
			}
		}
		tree.ProcessFrame += Tick;
	}
}
