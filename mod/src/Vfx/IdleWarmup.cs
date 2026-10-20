using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>主菜单/地图空闲后逐项预热（贴图、3D 主角在后台线程读，重的同步项只在地图界面做），不在 Logo 阶段提交 Spine 后台加载。</summary>
internal static class IdleWarmup
{
	private static readonly List<string> Jobs = new(new[]
	{
		// v0.14.89（实机日志：玩家很快开局，预热只跑了几项就进了战斗，第一次打开牌堆仍卡）：最常用、最重的排前面
		"@card frame",   // v0.14.88：塔罗卡框 / 重绘卡图 / 卡名字体（打开牌堆第一次会卡一下）
		"@hero3d",       // v0.14.90：原版 COMMU 模型 / 动作 / 材质，离屏渲染两帧。
		"@pile shaders",
		"res://p3makoto/pile/pile_skel_data.tres", "res://p3makoto/portrait/portrait_skel_data.tres",
		"@pile text",   // v0.14.91：牌堆界面的全部字号（PileFx.WarmTextJobs）；第一次轮到时展开成每份 6 个中文字的小项
		"res://p3makoto/ui/butterfly_wing_l.png", "res://p3makoto/ui/butterfly_wing_r.png", "res://p3makoto/ui/butterfly_body.png",
		"res://p3makoto/pile_deck/pile_deck_skel_data.tres", "res://p3makoto/pile_discard/pile_discard_skel_data.tres", "res://p3makoto/pile_exhaust/pile_exhaust_skel_data.tres",
		"res://p3makoto/ui/pile_deck.png", "res://p3makoto/ui/pile_discard.png", "res://p3makoto/ui/pile_exhaust.png",
		// v0.14.68：设置界面（第一次打开卡了约 0.9s：三张 1920×1080 立绘解码 + 三个新着色器编译）
	}.Concat(SystemJobs()));

	/// <summary>设置界面（P3R 风格）的资源；v0.14.76 起这个界面先关着（SettingsSkin.Enabled），关着就不预热。</summary>
	private static IEnumerable<string> SystemJobs()
	{
		if (!P3Makoto.Scenes.SettingsSkin.Enabled) yield break;
		// v0.14.68：设置界面（第一次打开卡了约 0.9s：三张 1920×1080 立绘解码 + 三个新着色器编译）
		foreach (string p in new[] { "@system shaders", "@system type", "res://p3makoto/ui/system_portrait.png", "res://p3makoto/ui/system_portrait_q1.png", "res://p3makoto/ui/system_portrait_half.png", "res://p3makoto/ui/system_portrait_q3.png", "res://p3makoto/ui/system_portrait_closed.png" })
		{
			yield return p;
		}
		foreach (string p in DiveFrames())
		{
			yield return p;
		}
	}

	/// <summary>v0.14.73 设置界面入水转场的原版水花逐帧图（只导出了不重复的帧，存在哪几张预热哪几张）。</summary>
	private static IEnumerable<string> DiveFrames()
	{
		for (int f = 20; f <= 52; f++)
		{
			string p = $"res://p3makoto/ui/dive/dive_{f:000}.png";
			if (ResourceLoader.Exists(p)) yield return p;
		}
	}
	private static readonly List<Resource> Held = new();
	private static readonly FieldInfo? LoaderInstance = AccessTools.Field(typeof(NAssetLoader), "_instance");
	private static readonly FieldInfo? Current = AccessTools.Field(typeof(NAssetLoader), "_currentSession");
	private static readonly FieldInfo? Sessions = AccessTools.Field(typeof(NAssetLoader), "_sessions");
	private static readonly PropertyInfo? SessionCompleted = Current?.FieldType.GetProperty("IsCompleted");
	private static SceneTree? tree;
	private static CanvasLayer? active;
	private static readonly IdleWarmupGate Gate = new();
	private static int frames, failed;
	private static ulong nextCheck;
	internal static bool Complete { get; private set; }

	internal static void Start()
	{
		if (tree != null || Engine.GetMainLoop() is not SceneTree t) return;
		if (LoaderInstance == null || Current == null || Sessions == null || SessionCompleted == null) { ModEntry.Warn("idle warmup skipped: native loader state unavailable"); return; }
		tree = t;
		t.ProcessFrame += Tick;
	}

	private static bool NativeIdle()
	{
		// Instance getter 在原版节点未 Ready 时会 new 一个节点；这里只读已经存在的实例，启动阶段不创建它。
		if (LoaderInstance!.GetValue(null) is not NAssetLoader loader || !GodotObject.IsInstanceValid(loader)) return false;
		object? session = Current!.GetValue(loader);
		bool completed = session == null || SessionCompleted!.GetValue(session) is true;
		return completed && Sessions!.GetValue(loader) is ICollection queue && queue.Count == 0;
	}

	private static bool MenuOrMap()
	{
		if (NGame.Instance?.MainMenu is CanvasItem menu && menu.IsVisibleInTree()) return true;
		// 原版的地图单例；不再每 150ms 全树遍历找 NMapScreen。
		if (NRun.Instance != null && !CombatManager.Instance.IsInProgress && NMapScreen.Instance is NMapScreen map && GodotObject.IsInstanceValid(map) && map.IsOpen && map.IsVisibleInTree()) return true;
		// v0.14.89：战斗里也做（每项 2~30ms，远低于打开牌堆的那一下）；牌堆等界面开着时 NCapstoneContainer.InUse 已经挡住
		return NRun.Instance != null && CombatManager.Instance.IsInProgress;
	}

	private static void Stop()
	{
		if (tree != null) tree.ProcessFrame -= Tick;
	}

	private static void Tick()
	{
		try { Step(); }
		catch (Exception e) { ModEntry.Report("idle warmup", e); Stop(); }
	}

	/// <summary>
	/// v0.14.91（实机日志：预热在选人界面一项接一项在主线程上跑，单项 50~290ms，选人动画一直掉帧）：
	/// 只能在主线程同步做、又很重的几项（Spine 立绘数据、中文大字体），只在地图界面空闲时做（开局必先经过地图）。
	/// </summary>
	private static bool Heavy(string path) => path.EndsWith("_skel_data.tres", StringComparison.Ordinal);

	/// <summary>
	/// v0.14.91（第二份日志：读档直接进战斗，没经过地图，重活一直没做，第一次开牌堆时才做）：
	/// 战斗里也允许做重活，但要等玩家安静下来 —— 连续 1.5 秒鼠标没动。
	/// </summary>
	private static Vector2 lastMouse;
	private static ulong mouseStill;
	private static bool Quiet(ulong now)
	{
		Vector2 m = DisplayServer.MouseGetPosition();
		if (m != lastMouse) { lastMouse = m; mouseStill = now; }
		return now - mouseStill >= 1500;
	}

	private static bool MapIdle() => NRun.Instance != null && !CombatManager.Instance.IsInProgress && NMapScreen.Instance is NMapScreen map && GodotObject.IsInstanceValid(map) && map.IsOpen && map.IsVisibleInTree();

	private static readonly HashSet<string> Requested = new(StringComparer.Ordinal);
	private static List<(Font Font, int Size, int Outline, string Text, bool Cjk)>? TextJobs;
	private static bool textBatch;
	private static bool TextJob(string path) => path.StartsWith("@pile text #", StringComparison.Ordinal);

	/// <summary>贴图类先交给后台线程读进缓存（不是 Spine，不碰启动黑屏那个问题），读完再在主线程画一下；3D 主角等它的后台解析完。</summary>
	private static bool Prefetched(string path)
	{
		if (path == "@hero3d") return HeroStage.PrefetchDone;
		List<string> pre = path == "@card frame" ? P3Makoto.Scenes.CardFrameSkin.WarmPaths()
			: path.EndsWith(".png", StringComparison.Ordinal) ? new List<string> { path }
			: new List<string>();
		bool done = true;
		foreach (string p in pre)
		{
			if (Requested.Add(p))
			{
				if (!ResourceLoader.Exists(p) || ResourceLoader.LoadThreadedRequest(p, "", false) != Error.Ok) continue;
			}
			ResourceLoader.ThreadLoadStatus st = ResourceLoader.LoadThreadedGetStatus(p);
			if (st == ResourceLoader.ThreadLoadStatus.InProgress) done = false;
			else if (st == ResourceLoader.ThreadLoadStatus.Loaded && ResourceLoader.LoadThreadedGet(p) is Resource r) Held.Add(r);
		}
		return done;
	}

	private static void Step()
	{
		if (active != null)
		{
			if (++frames < 4) return;
			if (GodotObject.IsInstanceValid(active)) active.QueueFree();
			active = null;
			// 文字小批次已经跨四帧保留到真实绘制；不再额外等 150ms。
			nextCheck = Time.GetTicksMsec() + (textBatch ? 0UL : 150UL);
			return;
		}
		if (Jobs.Count == 0)
		{
			Complete = failed == 0;
			GD.Print($"{ModEntry.Tag} perf pile idle warmup finished; complete={Complete}; failed={failed}");
			Stop();
			return;
		}
		ulong now = Time.GetTicksMsec();
		if (now < nextCheck) return;
		nextCheck = now + 150;
		bool eligible = Preload.OrdinaryReady && NCapstoneContainer.Instance?.InUse != true && NativeIdle() && MenuOrMap();
		if (eligible) HeroStage.Prefetch();              // 3D 主角的模型 / 贴图 / 动作在后台线程解析，越早开始越好
		if (!Gate.Ready(now, eligible)) return;
		bool calm = MapIdle() || Quiet(now);
		int pick = Jobs.FindIndex(j => calm || !Heavy(j));
		if (pick < 0) return;                             // 只剩重活，等地图界面或鼠标停下来再做
		string path = Jobs[pick];
		if (path == "@pile text" && tree != null)
		{
			TextJobs = PileFx.WarmTextJobs(tree.Root.GetVisibleRect().Size.Y / 1080f);
			Jobs.RemoveAt(pick);
			Jobs.InsertRange(pick, Enumerable.Range(0, TextJobs.Count).Select(i => "@pile text #" + i));
			return;
		}
		if (!Prefetched(path)) return;                    // 后台还在读，下次再看
		Jobs.RemoveAt(pick);
		ulong start = Time.GetTicksUsec();
		try
		{
			WarmOne(path);
		}
		catch (Exception e) { failed++; ModEntry.Report("idle warmup " + path, e); }
		GD.Print($"{ModEntry.Tag} perf idle warmup {path} {(Time.GetTicksUsec() - start) / 1000f:0.0}ms; {Jobs.Count} remaining");
	}

	private static void WarmOne(string path)
	{
		if (tree == null) return;
		textBatch = TextJob(path);
		Resource? res = null;
		if (!path.StartsWith("@", StringComparison.Ordinal))
		{
			if (!ResourceLoader.Exists(path)) throw new InvalidOperationException("resource missing: " + path);
			res = ResourceLoader.Load(path);       // 只在确认原版队列空闲后同步加载一个资源，没有后台 Spine 并发。
			if (res == null) throw new InvalidOperationException("resource load returned null: " + path);
			Held.Add(res);
		}
		active = new CanvasLayer { Name = "P3IdleWarmup", Layer = 128, ProcessMode = Node.ProcessModeEnum.Always };
		tree.Root.AddChild(active);
		frames = 0;
		Node2D tiny = new() { Position = new Vector2(2f, 2f), Scale = Vector2.One * 0.002f, Modulate = new Color(1f, 1f, 1f, 0.004f) };
		active.AddChild(tiny);                    // 在可视区真实绘制极小的图形，屏外节点会被裁掉而无法预热 GPU。
		if (path == "@system shaders")
		{
			foreach (Shader sh in new[] { Shaders.CampPanel, Shaders.CampWater, Shaders.CampPortrait, Shaders.GradientMap, Shaders.BlobWipe, Shaders.DiveSplash, Shaders.DiveMask })
			{
				tiny.AddChild(new ColorRect { Size = new Vector2(1000f, 1000f), Material = new ShaderMaterial { Shader = sh }, MouseFilter = Control.MouseFilterEnum.Ignore });
			}
			return;
		}
		if (path == "@card frame")
		{
			P3Makoto.Scenes.CardFrameSkin.Warm(tiny);
			return;
		}
		if (path == "@hero3d")
		{
			if (!HeroStage.Warm(tiny)) failed++;
			return;
		}
		if (path == "@system type")
		{
			// v0.14.80（点设置后卡约 0.5s）：设置界面的大号字（竖排 SYSTEM 230px、分页大字、面板标题）先栅格化一遍
			float k = tree.Root.GetVisibleRect().Size.Y / 1080f;
			const string latin = "SYSTEMGENRALPHICOUNDP3MAKTabcdefghijklmnopqrstuvwxyz0123456789 ·.";
			foreach (int size in new[] { (int)(230f * k), 230, 64, (int)(70f * k), (int)(58f * k), (int)(64f * k), (int)(56f * k), (int)(60f * k), (int)(34f * k), (int)(17f * k) })
			{
				Typo.Text(tiny, latin, Typo.Barlow, size, Colors.White, Vector2.Zero);
			}
			return;
		}
		if (path == "@pile shaders")
		{
			ColorRect bg = new() { Size = new Vector2(1000f, 1000f), Material = new ShaderMaterial { Shader = Shaders.Underwater }, MouseFilter = Control.MouseFilterEnum.Ignore };
			tiny.AddChild(bg);
			return;
		}
		if (path.StartsWith("@pile text #", StringComparison.Ordinal))
		{
			PileFx.WarmText(tiny, TextJobs![int.Parse(path[12..])]);
			return;
		}
		if (res is Texture2D tex) tiny.AddChild(new Sprite2D { Texture = tex });
		else if (ClassDB.Instantiate("SpineSprite").AsGodotObject() is Node2D spine)
		{
			tiny.AddChild(spine);
			spine.Set("skeleton_data_res", res!);
			Scenes.CharSelectBg.PlayIdle(spine, 2, () => { });
		}
		else throw new InvalidOperationException("SpineSprite class unavailable");
	}
}
