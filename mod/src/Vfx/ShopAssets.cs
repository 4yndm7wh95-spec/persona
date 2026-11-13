using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace P3Makoto.Vfx;

/// <summary>
/// 0.15.30 (user: entering the shop room from the map visibly stalls). Every entry used to
/// decode ~100 ms of PNG, parse three GLB models and their clips, and compile the room and
/// goods-page shaders on the main thread, all in the frames of the room transition.
/// During a Makoto run this decodes everything on a worker thread, uploads the textures and
/// warms the shaders while the map is idle and the mouse is still, and keeps them for the
/// whole session. Room code asks <see cref="Texture"/>; if the cache is not ready it decodes
/// synchronously as before, so nothing depends on the warmup having run.
/// </summary>
internal static class ShopAssets
{
	private const string Root = "res://p3makoto/";
	private static readonly string[] Files =
	{
		"shop/room_lattice.png", "shop/lattice_mask.png", "shop/lantern_left.png", "shop/lantern_right.png",
		"shop/back_idle.png", "shop/back_focus.png", "shop/equip_word.png", "hero3d/tex/T_UI_Camp_Caustics_00.png",
	};
	private static readonly ConcurrentDictionary<string, Image> Decoded = new(StringComparer.Ordinal);
	private static readonly Dictionary<string, Texture2D> Textures = new(StringComparer.Ordinal);
	private static readonly List<Resource> Held = new();
	private static Task? decode;
	private static SceneTree? tree;
	private static ulong nextCheck, mouseStill;
	private static Vector2 lastMouse;
	private static bool shadersWarm;
	private static CanvasLayer? warmLayer;
	private static int warmFrames;

	internal static void Watch()
	{
		if (tree != null || Engine.GetMainLoop() is not SceneTree t) return;
		tree = t;
		t.ProcessFrame += Tick;
	}

	/// <summary>Cached texture for a file under res://p3makoto/; decodes on the spot if the warmup has not reached it.</summary>
	internal static Texture2D? Texture(string file)
	{
		if (Textures.TryGetValue(file, out Texture2D? tex)) return tex;
		Image? image = Decoded.TryRemove(file, out Image? ready) ? ready : DecodeNow(file);
		if (image == null) return null;
		tex = ImageTexture.CreateFromImage(image);
		image.Dispose();
		Textures[file] = tex;
		return tex;
	}

	/// <summary>Keeps imported resources (room shader, painted room, blink patches) alive between shop visits.</summary>
	internal static T? Keep<T>(string path) where T : Resource
	{
		T? res = ResourceLoader.Load<T>(path);
		if (res != null && !Held.Contains(res)) Held.Add(res);
		return res;
	}

	private static Image? DecodeNow(string file)
	{
		string path = Root + file;
		if (!FileAccess.FileExists(path)) return null;
		Image image = new();
		if (image.LoadPngFromBuffer(FileAccess.GetFileAsBytes(path)) == Error.Ok && !image.IsEmpty()) return image;
		image.Dispose();
		return null;
	}

	private static bool MapIdle() =>
		NRun.Instance != null && !CombatManager.Instance.IsInProgress && NMapScreen.Instance is NMapScreen map
		&& GodotObject.IsInstanceValid(map) && map.IsOpen && map.IsVisibleInTree();

	/// <summary>Main-thread work only when the player is just looking at the map: mouse still for 0.6 s.</summary>
	private static bool Quiet(ulong now)
	{
		Vector2 m = DisplayServer.MouseGetPosition();
		if (m != lastMouse) { lastMouse = m; mouseStill = now; }
		return now - mouseStill >= 600;
	}

	private static void Tick()
	{
		try { Step(); }
		catch (Exception e) { ModEntry.Report("shop warmup", e); Stop(); }
	}

	private static void Stop()
	{
		if (tree != null) tree.ProcessFrame -= Tick;
		if (warmLayer != null && GodotObject.IsInstanceValid(warmLayer)) warmLayer.QueueFree();
		warmLayer = null;
	}

	private static void Step()
	{
		if (warmLayer != null)
		{
			if (++warmFrames < 4) return;
			if (GodotObject.IsInstanceValid(warmLayer)) warmLayer.QueueFree();
			warmLayer = null;
			shadersWarm = true;
			GD.Print($"{ModEntry.Tag} perf shop warmup finished (textures, models, shaders)");
			Stop();
			return;
		}
		ulong now = Time.GetTicksMsec();
		bool quiet = Quiet(now);
		if (now < nextCheck) return;
		nextCheck = now + 120;
		if (decode == null)
		{
			nextCheck = now + 500;
			if (NRun.Instance == null || !P3Makoto.Patches.Stage.MakotoRun) return;
			ulong start = Time.GetTicksMsec();
			decode = Task.Run(() =>
			{
				foreach (string file in Files)
					if (DecodeNow(file) is Image image) Decoded[file] = image;
			});
			HeroStage.ShopPrefetch();
			decode.ContinueWith(_ => GD.Print($"{ModEntry.Tag} perf shop prefetch decoded in {Time.GetTicksMsec() - start}ms"));
			return;
		}
		if (!decode.IsCompleted || !MapIdle() || !quiet) return;
		// One upload per step so a slow GPU never stalls the map for long.
		foreach (string file in Files)
		{
			if (Textures.ContainsKey(file))
			{
				// Decoded again by the worker after an on-demand load in a room: drop the duplicate.
				if (Decoded.TryRemove(file, out Image? dup)) dup.Dispose();
				continue;
			}
			ulong t0 = Time.GetTicksUsec();
			Texture(file);
			GD.Print($"{ModEntry.Tag} perf shop warmup {file} {(Time.GetTicksUsec() - t0) / 1000f:0.0}ms");
			return;
		}
		if (!HeroStage.ShopPrefetchDone) return;
		if (!HeroStage.WarmShopTextures()) return;   // one hero texture per step as well
		if (!shadersWarm) WarmShaders();
	}

	/// <summary>Draws the room and goods-page materials a few frames at tiny size so their shaders compile on the map.</summary>
	private static void WarmShaders()
	{
		if (tree == null) return;
		warmLayer = new CanvasLayer { Name = "P3ShopWarmup", Layer = 128, ProcessMode = Node.ProcessModeEnum.Always };
		tree.Root.AddChild(warmLayer);
		warmFrames = 0;
		// Drawn inside the visible area: offscreen canvas items are culled and would not compile.
		Node2D tiny = new() { Position = new Vector2(2f, 2f), Scale = Vector2.One * 0.002f, Modulate = new Color(1f, 1f, 1f, 0.004f) };
		warmLayer.AddChild(tiny);
		if (Keep<Shader>("res://p3makoto/shaders/velvet_shop.gdshader") is Shader room)
		{
			var mat = new ShaderMaterial { Shader = room };
			VelvetShopLife.ApplyLayers(mat);
			tiny.AddChild(new TextureRect { Texture = Texture("shop/room_lattice.png"), Material = mat, Size = new Vector2(1000f, 760f), MouseFilter = Control.MouseFilterEnum.Ignore });
		}
		foreach (Shader sh in new[] { Shaders.CommuBg, Shaders.ShopPaper, Shaders.HeroKeyOut })
			tiny.AddChild(new ColorRect { Size = new Vector2(1000f, 760f), Material = new ShaderMaterial { Shader = sh }, MouseFilter = Control.MouseFilterEnum.Ignore });
	}
}
