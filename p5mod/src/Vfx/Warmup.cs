using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;

namespace P5Joker;

/// <summary>Joker 进局时准备华丽收场，其余资源走可暂停的空闲队列。</summary>
public static class Warmup
{
	private sealed record Job(string Name, Func<bool>? Load = null, Action<Control>? Draw = null);
	private static readonly Queue<Job> Jobs = new();
	private static readonly Queue<Job> MenuJobs = new();
	private static readonly Queue<Job> CardShaders = new();
	private static readonly HashSet<string> Groups = new(), Textures = new(), Sounds = new();
	private static readonly HashSet<ulong> Shaders = new();
	private static readonly HashSet<Type> ShaderGroups = new();
	private static readonly List<Task> Loading = new();
	private static readonly List<Resource> Resources = new();
	private static readonly WarmupBudget Budget = new();
	private static readonly string[] FinaleTextures = SatanaelPuppet.TextureNames().Prepend("sinful_platform").ToArray();
	private static readonly string[] FinaleSounds = { Sfx.SinfulSweepOpen, Sfx.SinfulSweepClose, Sfx.SinfulThunder,
		Sfx.SinfulTwirl, Sfx.SinfulCharge, Sfx.SinfulShot, Sfx.SinfulPierceLast, Sfx.SubBoom, Sfx.Crush };
	private static readonly (string Name, Action<Control> Draw)[] FinaleSamples =
	{
		("sky", SinfulShell.WarmSky),
		("platform light", SinfulShell.WarmPlatform),
		("Satanael light", SinfulShell.WarmPuppet),
		("impact", SinfulShell.WarmImpact),
	};
	private static SceneTree? _tree;
	private static SubViewport? _viewport;
	private static Control? _slot;
	private static Task? _finaleLoading;
	private static bool _installed, _planningMenu;
	private static double _lastTick, _lastPlan, _cpuMs;
	private static string? _lastDraw;
	private static Vector2 _mouse;
	private static int _completed;
	internal static Action<string> Log = message => GD.Print(message);
	internal static bool FinalePrepared { get; private set; }

	// 游戏类型留在 WarmupGame.cs，预览工程仍然只需 Godot。
	public static Func<string?> IdleContext = () => null;
	public static Action RefreshPlan = () => { };

	public static void WaitFor(Task task)
	{
		lock (Loading)
			if (!task.IsCompleted && !Loading.Contains(task)) Loading.Add(task);
	}

	public static void StartMenu()
	{
		if (_installed) return;
		_installed = true;
		Callable.From(() =>
		{
			if (Engine.GetMainLoop() is not SceneTree tree) return;
			_tree = tree;
			_mouse = tree.Root.GetMousePosition();
			tree.ProcessFrame += Tick;
			Log("[P5Joker] warmup idle-v2 installed; startup work=0; full-directory sweeps disabled");
		}).CallDeferred();
	}

	private static void Tick()
	{
		try
		{
			Advance();
		}
		catch (Exception error) when (error.GetBaseException() is MissingMemberException or TypeLoadException)
		{
			// A missing API cannot recover on the next frame. Stop this optional
			// scheduler so it cannot flood the game log or stall the main thread.
			if (_tree != null) _tree.ProcessFrame -= Tick;
			if (GodotObject.IsInstanceValid(_viewport)) _viewport!.QueueFree();
			_viewport = null;
			_slot = null;
			Stage.Report("idle warmup disabled on incompatible game API", error);
		}
		catch (Exception error)
		{
			Stage.Report("idle warmup", error);
		}
	}

	private static void Advance()
	{
		if (_tree == null || !GodotObject.IsInstanceValid(_tree.Root)) return;
		long begin = Stopwatch.GetTimestamp();
		double now = Time.GetTicksUsec() / 1000.0, frameMs = now - _lastTick;
		_lastTick = now;
		double frameLimit = Engine.MaxFps > 0 ? Math.Max(20, 1400.0 / Engine.MaxFps) : 20;

		// 上一项只画一帧。释放也是工作，不和下一项挤在同一帧。
		if (_slot != null)
		{
			if (GodotObject.IsInstanceValid(_slot)) _slot.Free();
			_slot = null;
			if (_viewport != null && GodotObject.IsInstanceValid(_viewport))
				_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
			Budget.SlowDraw(now, frameMs, Math.Max(24, frameLimit));
			Log($"[P5Joker] warmup idle-v2 {_lastDraw}: cpu={_cpuMs:F2}ms frame={frameMs:F1}ms remaining={Pending}");
			_lastDraw = null;
			return;
		}

		bool loading;
		lock (Loading)
		{
			Loading.RemoveAll(task => task.IsCompleted);
			loading = Loading.Count != 0;
		}
		if (now - _lastPlan >= 500)
		{
			_lastPlan = now;
			RefreshPlan();
		}
		string? context = !loading && !_tree.Paused ? IdleContext() : null;
		Vector2 mouse = _tree.Root.GetMousePosition();
		bool input = Input.IsAnythingPressed() || mouse.DistanceSquaredTo(_mouse) > 1;
		_mouse = mouse;
		Budget.Observe(now, frameMs, frameLimit, context != null, input);
		double cpuUsed = Stopwatch.GetElapsedTime(begin).TotalMilliseconds;
		if (Pending == 0 || !Budget.CanStart(now, cpuUsed)) return;

		Queue<Job> queue = CardShaders.Count != 0 ? CardShaders : Jobs.Count != 0 ? Jobs : MenuJobs;
		Job job = queue.Peek();
		bool done = true;
		long start = Stopwatch.GetTimestamp();
		try
		{
			if (job.Load != null) done = job.Load();
			else if (job.Draw != null)
			{
				if (_viewport == null || !GodotObject.IsInstanceValid(_viewport))
					_viewport = CreateViewport(_tree.Root, "P5IdleWarmup");
				Control slot = CreateSlot(_viewport);
				_slot = slot;
				DrawMuted(slot, job.Draw);
				_viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
				_lastDraw = job.Name;
			}
		}
		catch (Exception e) { Stage.Report("warmup " + job.Name, e); }
		_cpuMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
		Budget.Submitted(now, _cpuMs);
		if (done)
		{
			queue.Dequeue();
			_completed++;
			if (_slot == null && _cpuMs >= 2)
				Log($"[P5Joker] warmup idle-v2 {job.Name}: cpu={_cpuMs:F2}ms context={context} remaining={Pending}");
			if (Pending == 0) Log($"[P5Joker] warmup idle-v2 queue drained; completed={_completed}; context={context}");
		}
	}

	private static SubViewport CreateViewport(Window root, string name)
	{
		SubViewport viewport = new()
		{
			Name = name, Size = new Vector2I(128, 96), Disable3D = true, World2D = new World2D(),
			TransparentBg = root.TransparentBg, UseHdr2D = root.UseHdr2D, Msaa2D = root.Msaa2D,
			GuiDisableInput = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
		};
		root.AddChild(viewport);
		return viewport;
	}

	private static Control CreateSlot(SubViewport viewport)
	{
		Control slot = new() { Size = viewport.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
		viewport.AddChild(slot);
		return slot;
	}

	private static void DrawMuted(Control slot, Action<Control> draw)
	{
		bool wasMuted = Sfx.Muted;
		Sfx.Muted = true;
		try { draw(slot); }
		finally { Sfx.Muted = wasMuted; }
	}

	public static void RequestMenu()
	{
		if (!Groups.Add("menu")) return;
		_planningMenu = true;
		try { BuildMenuPlan(); }
		finally { _planningMenu = false; }
		LogPlan("menu");
	}

	public static void MenuShown()
	{
		Groups.Add("menu");
		int skipped = MenuJobs.Count;
		MenuJobs.Clear();
		if (skipped != 0) Log($"[P5Joker] warmup idle-v2 skipped {skipped} menu tasks; selector will load them on demand");
	}

	private static int Pending => CardShaders.Count + Jobs.Count + MenuJobs.Count;
	private static void Enqueue(Job job) => (_planningMenu ? MenuJobs : Jobs).Enqueue(job);

	private static void BuildMenuPlan()
	{
		// 在已可操作的主菜单空闲时，先读两张最大的立绘。其余资源在对应页面按需读取。
		foreach (string name in new[] { "cs_body", "cs_hand" })
			Enqueue(new("texture " + name, Load: () => Fx.PollTexture(name)));
	}

	public static void RequestJoker()
	{
		if (!Groups.Add("common")) return;
		SerpentMantle.PrepareHead();
		QueueTextures(new[] { "shell", "slash_thin", "slash_heavy", "slash_thrust", "muzzle_front",
			"muzzle_side_01", "muzzle_side_02", "muzzle_side_03" });
		QueueShaders(typeof(GunFx), typeof(Glow), typeof(Paint), typeof(Blade), typeof(PoisonFx), typeof(Impact));
		Enqueue(new("common impact", Draw: Impact.Warm));
		// 只建立材质样本，不调用会影响实战状态的完整命中 / 施毒流程。
		Enqueue(new("muzzle material", Draw: slot => Paint.Pop(slot, "muzzle_side_01", new Vector2(64, 48),
			0, 0.04f, Lut5.Muzzle, 0.02f, 0.08f, 0.65f)));
		foreach (string path in new[] { Vanilla.Specks, Vanilla.Streaks, Vanilla.BigSlashSmoke })
		{
			string scene = path;
			Enqueue(new("common particle resource " + scene, Load: () =>
			{
				if (!Fx.PollResource(scene, out Resource? resource)) return false;
				if (resource != null) Resources.Add(resource);
				return true;
			}));
			Enqueue(new("common particle material " + scene, Draw: slot =>
				Vanilla.Burst(slot, scene, new Vector2(64, 48), 0, 0.1f, Lut5.Blood, amount: 2)));
		}
		QueueSounds(new[] { Sfx.Ricochet, Sfx.GunHit, Sfx.GunHitFinal, Sfx.PoisonTick });
		LogPlan("common");
	}

	public static void RequestCard(string card)
	{
		if (card is "Snakebite" or "SerpentForm" && Groups.Add("serpent body"))
		{
			SerpentMantle.PrepareHead();
			CardShaders.Enqueue(new("serpent head field", Load: static () => SerpentMantle.HeadReady));
			CardShaders.Enqueue(new("serpent body", Draw: SerpentMantle.Warm));
			QueueSounds(new[] { Sfx.SerpentMaw });
		}
		if (card == "SerpentForm")
		{
			if (!Groups.Add("SerpentForm")) return;
			foreach (string path in new[]
			{
				"res://scenes/vfx/forms/serpent/vfx_serpent_form_idle_vfx.tscn",
				"res://images/powers/serpent_form_power.png",
			})
			{
				string resourcePath = path;
				Enqueue(new("serpent resource " + path, Load: () =>
				{
					if (!Fx.PollResource(resourcePath, out Resource? resource)) return false;
					if (resource != null) Resources.Add(resource);
					return true;
				}));
			}
			QueueShaders(CardShaders.Enqueue, typeof(SerpentMist), typeof(SerpentEclipse));
			QueueSounds(new[] { Sfx.SerpentRise });
			LogPlan("SerpentForm");
			return;
		}
		if (card == "BulletTime")
		{
			if (!Groups.Add(card)) return;
			CardShaders.Enqueue(new("bullet time background", Draw: TimeStop.WarmBackground));
			CardShaders.Enqueue(new("bullet time enemies", Draw: TimeStop.WarmEnemies));
			CardShaders.Enqueue(new("bullet time dust", Draw: TimeStop.WarmDust));
			CardShaders.Enqueue(new("bullet time card", Draw: slot => Phantom.Card(slot, slot.Size / 2f, 18f, 0.3f)));
			QueueSounds(new[] { "gx_tock", "gx_freeze", "gx_tick", "gx_burn", "gx_glass", "gx_whoosh" }
				.SelectMany(name => Enumerable.Range(0, Sfx.Palette[name]).Select(index => name + "_" + index)));
			LogPlan(card);
			return;
		}
		if (card == "GrandFinale") { RequestFinale(); return; }
		if (card == "NoxiousFumes" && Groups.Add(card))
		{
			CardShaders.Enqueue(new("noxious fumes", Draw: FumesField.Warm));
			QueueTextures(new[] { "curse_ghost", "curse_soul" });
			QueueSounds(new[] { Sfx.FumesRoll });
		}
		if (card == "Outbreak" && Groups.Add("Outbreak")) QueueShaders(typeof(CrushHand));
		if (card == "WraithForm" && Groups.Add("WraithForm")) QueueShaders(typeof(WraithVeil));
		// 用当前实际注册的处理器，避开已被重做版覆盖的旧特效，也不扫描整个程序集。
		if (CardSkillFx.Cards.TryGetValue(card, out var effect) && effect.Method.DeclaringType is Type owner)
		{
			while (owner.IsNested && owner.DeclaringType != null) owner = owner.DeclaringType;
			QueueShaders(owner);
		}
	}

	internal static Task PrepareFinaleForRun(SceneTree tree, Task gameLoad)
	{
		if (FinalePrepared || DisplayServer.GetName() == "headless" || gameLoad.IsFaulted || gameLoad.IsCanceled)
			return gameLoad;
		if (_finaleLoading == null || _finaleLoading.IsCompleted)
			_finaleLoading = PrepareFinale(tree);
		return Task.WhenAll(gameLoad, _finaleLoading);
	}

	private static async Task PrepareFinale(SceneTree tree)
	{
		SubViewport? viewport = null;
		long started = Stopwatch.GetTimestamp();
		try
		{
			Window root = tree.Root;
			// The native loader still runs. A stalled resource request must not hold the run indefinitely.
			bool Reading() => Fx.Active(root) && Stopwatch.GetElapsedTime(started).TotalSeconds < 2;
			await Fx.PrepareTextures(tree, FinaleTextures, Reading);
			if (!Fx.Active(root) || FinaleTextures.Any(name => Fx.ReadyTexture(name) == null))
			{
				Log("[P5Joker] GrandFinale loading warmup incomplete: textures");
				return;
			}
			foreach (string sound in FinaleSounds) Sfx.Preload(sound);
			await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			if (!Fx.Active(root)) return;

			viewport = CreateViewport(root, "P5FinaleLoading");
			foreach ((string name, Action<Control> draw) in FinaleDraws())
			{
				if (!await RenderSample(tree, viewport, draw))
				{
					Log($"[P5Joker] GrandFinale loading warmup incomplete: {name} was not drawn");
					return;
				}
			}
			RuntimeHelpers.PrepareMethod(((Func<Control, Control, SinfulShell.Setup, float[]>)SinfulShell.Play).Method.MethodHandle);
			FinalePrepared = true;
			Log($"[P5Joker] GrandFinale loading warmup ready in {Stopwatch.GetElapsedTime(started).TotalMilliseconds:F1}ms");
		}
		catch (Exception e) { Stage.Report("finale loading warmup", e); }
		finally
		{
			if (GodotObject.IsInstanceValid(viewport)) viewport!.QueueFree();
		}
	}

	private static async Task<bool> RenderSample(SceneTree tree, SubViewport viewport, Action<Control> draw)
	{
		Control slot = CreateSlot(viewport);
		bool drawn = false;
		void OnDraw() => drawn = true;
		RenderingServer.FramePostDraw += OnDraw;
		try
		{
			DrawMuted(slot, draw);
			viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
			for (int frame = 0; frame < 4 && !drawn; frame++)
				await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			return drawn;
		}
		finally
		{
			RenderingServer.FramePostDraw -= OnDraw;
			if (GodotObject.IsInstanceValid(slot)) slot.Free();
			if (GodotObject.IsInstanceValid(viewport)) viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
		}
	}

	private static IEnumerable<(string Name, Action<Control> Draw)> FinaleDraws()
	{
		foreach (var sample in FinaleSamples) yield return sample;
		foreach (var sample in ShaderSamples(typeof(SinfulShell), null,
			nameof(SinfulShell.SkyShader), nameof(SinfulShell.PlatformShader)))
			yield return sample;
		foreach (var sample in ShaderSamples(typeof(SatanaelPuppet), null)) yield return sample;
	}

	internal static void RequestFinale()
	{
		if (FinalePrepared || !Groups.Add("GrandFinale")) return;
		QueueTextures(FinaleTextures);
		QueueSounds(FinaleSounds);
		foreach ((string name, Action<Control> draw) in FinaleDraws())
			Enqueue(new("finale " + name, Draw: draw));
		Enqueue(new("finale warmup finished", Load: () =>
		{
			FinalePrepared = FinaleTextures.All(name => Fx.ReadyTexture(name) != null);
			Log($"[P5Joker] GrandFinale idle warmup finished; prepared={FinalePrepared}");
			return true;
		}));
		LogPlan("GrandFinale");
	}

	public static void RequestReward()
	{
		if (!Groups.Add("reward")) return;
		QueueTextures(new[] { "reward_band", "reward_hover_plate", "reward_hover_plate_shadow",
			"reward_stamp_stolen", "reward_header_plate", "reward_header_text_zh",
			"reward_skip_plate", "reward_skip_plate_hover", "reward_skip_text_en", "reward_skip_text_zh",
			"reward_letter_T", "reward_letter_R", "reward_letter_E1", "reward_letter_A",
			"reward_letter_S", "reward_letter_U", "reward_letter_R2", "reward_letter_E2" });
	}

	/// <summary>抽牌堆界面（PileDraw.cs）：原版暂停菜单素材 + 预告信、星纹 / 剪影着色器、开关菜单音效。</summary>
	public static void RequestPile()
	{
		if (!Groups.Add("pile")) return;
		QueueTextures(PileDraw.Textures);
		QueueTextures(PileExhaust.Textures);
		QueueTextures(PileDeck.Textures);
		QueueTextures(PileDiscard.Textures);
		QueueTextures(PileText.Textures());
		QueueShaders(typeof(PileDraw));
		QueueShaders(typeof(PileExhaust));
		QueueShaders(typeof(PileDiscard));
		QueueShaders(typeof(PileField));
		QueueShaders(typeof(PileDeck));
		// 用户 2026-11-24：入场卡 —— 第一次打开时才编译的东西都提前做：裁剪（clip_children）+ 读屏反色的画法（消耗堆）、菜单 3D Joker（网格、动作片段、3D 材质）
		Enqueue(new("pile clip + invert", Draw: slot =>
		{
			Polygon2D clip = new() { Polygon = new[] { new Vector2(0, 0), new Vector2(16, 0), new Vector2(16, 16) }, ClipChildren = CanvasItem.ClipChildrenMode.Only, Position = new Vector2(60, 44) };
			clip.AddChild(new ColorRect { Size = new Vector2(16, 16), Material = PileDraw.StarMaterial(118f, Colors.White, Colors.Black, Colors.Gray, Vector2.Zero), MouseFilter = Control.MouseFilterEnum.Ignore });
			slot.AddChild(clip);
			slot.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
			slot.AddChild(new Polygon2D { Polygon = new[] { new Vector2(60, 44), new Vector2(76, 44), new Vector2(76, 60) }, Antialiased = true, Material = new ShaderMaterial { Shader = PileExhaust.InvertShader } });
		}));
		QueueTextures(new[] { "m3d_c0001_t01_face01UI", "m3d_c0001_t01_kamenUI" });
		Enqueue(new("menu 3d data", Load: () => { MenuJoker3D.Preload("main_0000", "main_0005", "main_0010", "main_0015", "main_0016"); return true; }));
		Enqueue(new("menu 3d material", Draw: MenuJoker3D.Warm));
		LogPlan("pile");
	}

	public static void RequestAllOut()
	{
		if (!Groups.Add("allout")) return;
		QueueTextures(new[] { "aoa_crackp5_a", "aoa_crackp5_core", "aoa_drip" });
		QueueShaders(typeof(AllOut));
		QueueSounds(new[] { Sfx.AoaZoom, Sfx.AoaCrack, Sfx.Shatter, Sfx.AoaSplatter });
		Enqueue(new("allout model", Load: () => { AllOutFinale.Preload(); return true; }));
		LogPlan("allout");
	}

	public static void RequestPersona()
	{
		if (!Groups.Add("Arsene")) return;
		QueueTextures(ArseneFx.TextureNames());
		QueueShaders(typeof(ArseneFx));
		LogPlan("Arsene");
	}

	private static void QueueTextures(IEnumerable<string> names)
	{
		foreach (string name in names)
		{
			if (!Textures.Add(name)) continue;
			string texture = name;
			Enqueue(new("texture " + texture, Load: () => Fx.PollTexture(texture)));
			Enqueue(new("upload " + texture, Draw: slot =>
			{
				if (Fx.ReadyTexture(texture) is not Texture2D tex) return;
				slot.AddChild(new Sprite2D { Texture = tex, Position = new Vector2(64, 48),
					Scale = Vector2.One * (48f / Math.Max(tex.GetWidth(), tex.GetHeight())) });
			}));
		}
	}

	private static void QueueSounds(IEnumerable<string> names)
	{
		foreach (string name in names)
		{
			if (!Sounds.Add(name)) continue;
			string sound = name;
			Enqueue(new("audio " + sound, Load: () => { Sfx.Preload(sound); return true; }));
		}
	}

	private static void QueueShaders(params Type[] types) => QueueShaders(Enqueue, types);

	private static void QueueShaders(Action<Job> enqueue, params Type[] types)
	{
		foreach (Type type in types)
		{
			if (!ShaderGroups.Add(type)) continue;
			foreach ((string name, Action<Control> draw) in ShaderSamples(type, Shaders))
				enqueue(new(name, Draw: draw));
		}
	}

	private static IEnumerable<(string Name, Action<Control> Draw)> ShaderSamples(Type type, HashSet<ulong>? seen, params string[] drawnSeparately)
	{
		foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
		{
			if (prop.PropertyType != typeof(Shader) || prop.GetIndexParameters().Length != 0
				|| drawnSeparately.Contains(prop.Name)) continue;
			PropertyInfo property = prop;
			yield return (type.Name + "." + prop.Name, slot =>
			{
				if (property.GetValue(null) is not Shader shader || shader.GetMode() != Shader.Mode.CanvasItem
					|| (seen != null && !seen.Add(shader.GetInstanceId()))) return;
				slot.AddChild(new ColorRect { Material = new ShaderMaterial { Shader = shader },
					Size = new Vector2(8, 8), Position = new Vector2(60, 44), MouseFilter = Control.MouseFilterEnum.Ignore });
			});
		}
	}

	private static void LogPlan(string group) => Log($"[P5Joker] warmup idle-v2 requested {group}; pending={Pending}; textures={Textures.Count}; sounds={Sounds.Count}");
}
