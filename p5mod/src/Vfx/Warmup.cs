using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

namespace P5Joker;

/// <summary>菜单提前读取选人资源。战斗特效分帧在小视口中静默绘制，同一游戏进程只做一次。</summary>
public static class Warmup
{
	private static bool _menuStarted, _combatRunning, _combatReady;
	private static ulong _lastRoom;
	private static Task? _menu;
	private static Task<bool>? _critical;
	private static readonly List<Resource> SerpentResources = new();

	private static readonly string[] MenuTextures =
	{
		"cs_button", "cs_body", "cs_body_line", "cs_hand", "cs_hand_line", "cs_blink", "cs_warp",
		"cs_skyline", "cs_rooftop", "cs_slab_text", "cs_eye_streak", "cs_card",
		"cs_info_head", "cs_info_ghost", "eyes", "cutin_tail_left", "cutin_tail_right", "star4",
	};

	private static readonly string[] FinaleSounds =
	{
		Sfx.SinfulSweepOpen, Sfx.SinfulSweepClose, Sfx.SinfulThunder, Sfx.SinfulTwirl,
		Sfx.SinfulCharge, Sfx.SinfulShot, Sfx.SinfulPierceLast,
	};

	private static bool Alive(Node node) =>
		GodotObject.IsInstanceValid(node) && node.IsInsideTree() && !node.IsQueuedForDeletion();

	public static void StartMenu()
	{
		if (_menuStarted) return;
		_menuStarted = true;
		Callable.From(() =>
		{
			if (Engine.GetMainLoop() is SceneTree tree) _menu = PrepareMenu(tree.Root);
		}).CallDeferred();
	}

	private static async Task PrepareMenu(Window parent)
	{
		try
		{
			if (!await LoadTextures(parent, MenuTextures)) return;
			if (await RenderBatch(parent, MenuSteps())) GD.Print("[P5Joker] character select warmup ready");
			if (Alive(parent)) await EnsureCritical(parent);
		}
		catch (Exception e)
		{
			Stage.Report("menu warmup", e);
		}
	}

	public static void Run(Control layer, ulong room)
	{
		if (_combatReady || _combatRunning || room == _lastRoom || !Alive(layer)) return;
		_lastRoom = room;
		_combatRunning = true;
		_ = PrepareCombat(layer);
	}

	private static async Task PrepareCombat(Control layer)
	{
		try
		{
			if (_menu != null) await _menu;
			if (!Alive(layer)) return;
			await EnsureCritical(layer.GetViewport());
			if (!Alive(layer)) return;
			if (!await RenderBatch(layer, CombatSteps())) return;
			_combatReady = await LoadTextures(layer, Fx.TextureNames());
		}
		catch (Exception e)
		{
			Stage.Report("combat warmup", e);
		}
		finally
		{
			_combatRunning = false;
		}
	}

	// 关键的两张牌用稳定的视口预热。切房间或从菜单进局不会取消已经排入的工作。
	private static Task<bool> EnsureCritical(Viewport viewport) => _critical ??= PrepareCritical(viewport);

	private static async Task<bool> PrepareCritical(Viewport viewport)
	{
		try
		{
			if (!await LoadTextures(viewport, SatanaelPuppet.TextureNames().Prepend("sinful_platform"))) return false;
			if (!await LoadSerpentResources(viewport)) return false;
			bool ready = await RenderBatch(viewport, CriticalSteps());
			if (ready) GD.Print("[P5Joker] Serpent Form / Grand Finale warmup ready");
			return ready;
		}
		catch (Exception e)
		{
			Stage.Report("Serpent Form / Grand Finale warmup", e);
			return false;
		}
	}

	private static async Task<bool> LoadSerpentResources(Viewport viewport)
	{
		if (!Alive(viewport)) return false;
		SceneTree tree = viewport.GetTree();
		foreach (string path in new[]
		{
			"res://scenes/vfx/forms/serpent/vfx_serpent_form_idle_vfx.tscn",
			"res://images/powers/serpent_form_power.png",
		})
		{
			Resource? resource = null;
			while (Alive(viewport) && !Fx.PollResource(path, out resource))
				await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			if (resource != null) SerpentResources.Add(resource);
			if (!Alive(viewport)) return false;
		}
		return true;
	}

	private static async Task<bool> LoadTextures(Node parent, IEnumerable<string> names)
	{
		if (!Alive(parent)) return false;
		SceneTree tree = parent.GetTree();
		foreach (string name in names)
		{
			while (Alive(parent) && !Fx.PollTexture(name))
				await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			if (!Alive(parent)) return false;
			await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		}
		return Alive(parent);
	}

	private static async Task<bool> RenderBatch(Node parent, IEnumerable<(string Name, Action<Control> Draw)> steps)
	{
		if (!Alive(parent)) return false;
		// 预热前等待当前场景挂载结束。视口不合成到游戏画面，2D 设置跟随实际目标，
		// 避免在不同的 HDR / MSAA 管线里预热，或意外把读屏着色器画到玩家视野。
		SceneTree tree = parent.GetTree();
		await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
		if (!Alive(parent)) return false;
		Viewport target = parent as Viewport ?? parent.GetViewport();
		SubViewport viewport = new()
		{
			Name = "P5Warmup", Size = new Vector2I(128, 96), Disable3D = true,
			World2D = new World2D(), TransparentBg = target.TransparentBg, UseHdr2D = target.UseHdr2D, Msaa2D = target.Msaa2D,
			GuiDisableInput = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
		};
		parent.AddChild(viewport);
		try
		{
			foreach (var (name, draw) in steps)
			{
				if (!Alive(viewport)) return false;
				Control slot = new() { Size = viewport.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
				viewport.AddChild(slot);
				bool wasMuted = Sfx.Muted;
				Sfx.Muted = true;
				try { Stage.Safe("warmup " + name, () => draw(slot)); }
				finally { Sfx.Muted = wasMuted; }

				// 等实际绘制完成，再在下一帧释放节点并推进，避免所有阶段挤在同一帧。
				await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
				await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
				if (GodotObject.IsInstanceValid(slot)) slot.Free();
			}
			return Alive(viewport);
		}
		finally
		{
			if (GodotObject.IsInstanceValid(viewport)) viewport.QueueFree();
		}
	}

	private static Vector2 Center(Control slot) => Fx.Screen(slot) * 0.5f;

	private static IEnumerable<(string, Action<Control>)> MenuSteps()
	{
		yield return ("character select", CharSelectScene.Warm);
		yield return ("character info", slot =>
		{
			var sample = new Node2D { Position = new Vector2(36f, 16f), Scale = Vector2.One * 0.08f };
			slot.AddChild(sample);
			CharSelectInfoPanel.Build(sample,
				new("70/70", "99", "蛇之戒指", "在每场战斗开始时，额外抽[blue]2[/blue]张牌。", null), animate: false);
		});
		yield return ("eye cut-in", slot => CutIn.Eyes(slot, warm: true));
		foreach (var step in ShaderSteps(typeof(CutIn))) yield return step;
		foreach (string sound in new[] { Sfx.CutIn, Sfx.OneMore, Sfx.SubBoom, Sfx.EvokerShatter })
			yield return ("audio " + sound, _ => Sfx.Preload(sound));
	}

	private static IEnumerable<(string, Action<Control>)> CriticalSteps()
	{
		yield return ("serpent body", SerpentMantle.Warm);
		yield return ("serpent mist", slot => DrawShader(slot, SerpentMist.Shader));
		yield return ("serpent eclipse", slot => DrawShader(slot, SerpentEclipse.Shader));
		yield return ("finale sky / platform light", SinfulShell.WarmScene);
		yield return ("finale Satanael light", SinfulShell.WarmPuppet);
		yield return ("finale impact", SinfulShell.WarmImpact);
		foreach (var step in ShaderSteps(typeof(SinfulShell), typeof(SatanaelPuppet))) yield return step;
		foreach (string sound in FinaleSounds) yield return ("audio " + sound, _ => Sfx.Preload(sound));
	}

	private static IEnumerable<(string, Action<Control>)> CombatSteps()
	{
		yield return ("gun", slot =>
		{
			Vector2 c = Center(slot);
			GunFx.Muzzle(slot, c, Vector2.Right);
			GunFx.Shot(slot, c, c + new Vector2(30f, 0f));
		});
		yield return ("poison", slot =>
		{
			Vector2 c = Center(slot);
			PoisonFx.Apply(slot, c, c - new Vector2(0f, 12f), 20f, null, 9);
			PoisonFx.Tick(slot, c, c - new Vector2(0f, 12f), 20f, null);
		});
		foreach (string tex in new[] { "slash_thin", "slash_heavy" })
			yield return (tex, slot => Paint.Sweep(slot, tex, Center(slot), 0f, Vector2.One * 0.03f, Lut5.Blood, 0.05f, 0.05f, 0.2f));
		yield return ("impact", Impact.Warm);
		yield return ("blue flame", slot => BlueFlame.Tongue(slot, Center(slot), 0.5f, 0.2f));
		yield return ("smoke LUT", slot => Vanilla.Burst(slot, Vanilla.BigSlashSmoke, Center(slot), 0f, 0.3f, Lut5.Smoke));
		yield return ("slash smoke LUT", slot => Vanilla.Burst(slot, Vanilla.BigSlashSmoke, Center(slot), 0f, 0.3f, Lut5.Blood));
		foreach (FieldInfo field in typeof(Vanilla).GetFields(BindingFlags.Static | BindingFlags.Public))
			if (field.IsLiteral && field.GetRawConstantValue() is string path)
				yield return (path, slot => Vanilla.Burst(slot, path, Center(slot), 0f, 0.3f, Lut5.Curse));
		foreach (var step in ShaderSteps(typeof(Warmup).Assembly.GetTypes()
			.Except(new[] { typeof(SinfulShell), typeof(SatanaelPuppet), typeof(SerpentMantle), typeof(SerpentMist),
				typeof(SerpentEclipse), typeof(CharSelectScene), typeof(CharSelectInfoPanel), typeof(P5Ui), typeof(CutIn) }).ToArray()))
			yield return step;
		foreach (string sound in Sfx.PreloadNames()) yield return ("audio " + sound, _ => Sfx.Preload(sound));
	}

	private static IEnumerable<(string, Action<Control>)> ShaderSteps(params Type[] types)
	{
		foreach (Type type in types)
		foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
		{
			if (prop.PropertyType != typeof(Shader) || prop.GetIndexParameters().Length != 0) continue;
			yield return (type.Name + "." + prop.Name, slot =>
			{
				if (prop.GetValue(null) is Shader shader && shader.GetMode() == Shader.Mode.CanvasItem)
					DrawShader(slot, shader);
			});
		}
	}

	private static void DrawShader(Control slot, Shader shader) =>
		slot.AddChild(new ColorRect
		{
			Material = new ShaderMaterial { Shader = shader }, Size = new Vector2(8f, 8f), Position = Center(slot),
			MouseFilter = Control.MouseFilterEnum.Ignore,
		});
}
