using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>主菜单/地图空闲后逐项在主线程预热，不在 Logo 阶段提交 Spine 后台加载。</summary>
internal static class IdleWarmup
{
	private static readonly Queue<string> Jobs = new(new[]
	{
		"@pile shaders",
		"@pile title", "@pile big type", "@pile chinese",
		"res://p3makoto/ui/pile_deck.png", "res://p3makoto/ui/pile_discard.png", "res://p3makoto/ui/pile_exhaust.png",
		"res://p3makoto/ui/butterfly_wing_l.png", "res://p3makoto/ui/butterfly_wing_r.png", "res://p3makoto/ui/butterfly_body.png",
		"res://p3makoto/pile/pile_skel_data.tres", "res://p3makoto/pile_deck/pile_deck_skel_data.tres",
		"res://p3makoto/pile_discard/pile_discard_skel_data.tres", "res://p3makoto/pile_exhaust/pile_exhaust_skel_data.tres",
		"res://p3makoto/portrait/portrait_skel_data.tres",
	});
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
		if (NRun.Instance == null || CombatManager.Instance.IsInProgress || tree == null) return false;
		Stack<Node> pending = new();
		pending.Push(tree.Root);
		while (pending.Count > 0)
		{
			Node n = pending.Pop();
			if (n is CanvasItem ci && n.GetType().Name == "NMapScreen" && ci.IsVisibleInTree()) return true;
			foreach (Node child in n.GetChildren()) pending.Push(child);
		}
		return false;
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

	private static void Step()
	{
		if (active != null)
		{
			if (++frames < 4) return;
			if (GodotObject.IsInstanceValid(active)) active.QueueFree();
			active = null;
			nextCheck = Time.GetTicksMsec() + 150;
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
		if (!Gate.Ready(now, eligible)) return;
		string path = Jobs.Dequeue();
		ulong start = Time.GetTicksUsec();
		try { WarmOne(path); }
		catch (Exception e) { failed++; ModEntry.Report("idle warmup " + path, e); }
		GD.Print($"{ModEntry.Tag} perf idle warmup {path} {(Time.GetTicksUsec() - start) / 1000f:0.0}ms; {Jobs.Count} remaining");
	}

	private static void WarmOne(string path)
	{
		if (tree == null) return;
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
		if (path == "@pile shaders")
		{
			ColorRect bg = new() { Size = new Vector2(1000f, 1000f), Material = new ShaderMaterial { Shader = Shaders.Underwater }, MouseFilter = Control.MouseFilterEnum.Ignore };
			tiny.AddChild(bg);
			return;
		}
		if (path.StartsWith("@pile", StringComparison.Ordinal))
		{
			float k = tree.Root.GetVisibleRect().Size.Y / 1080f;
			bool chinese = path == "@pile chinese";
			int size = (int)((chinese ? 19f : path == "@pile title" ? 92f : 250f) * k);
			Label label = Typo.Text(tiny, chinese ? "抽牌堆弃牌堆消耗牌组攻击技能能力诅咒整副牌按类型费用排列最新在前" : "DRAW PILE DISCARD EXHAUST DECK ATTACK SKILL POWER CURSE 0123456789", chinese ? Typo.CjkBold : Typo.Barlow, size, Colors.White, Vector2.Zero);
			label.AddThemeConstantOverride("outline_size", chinese ? 0 : (int)(5f * k));
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
