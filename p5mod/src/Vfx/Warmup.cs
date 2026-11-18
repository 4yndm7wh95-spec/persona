using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;

namespace P5Joker;

/// <summary>按需、串行、可暂停的空闲预热；启动阶段只挂回调，不读取或绘制资源。</summary>
public static class Warmup
{
	private sealed record Job(string Name, Func<bool>? Load = null, Action<Control>? Draw = null);
	private static readonly Queue<Job> Jobs = new();
	private static readonly Queue<Job> MenuJobs = new();
	private static readonly HashSet<string> Groups = new(), Textures = new(), Sounds = new();
	private static readonly HashSet<ulong> Shaders = new();
	private static readonly HashSet<Type> ShaderGroups = new();
	private static readonly List<Task> Loading = new();
	private static readonly List<Resource> Resources = new();
	private static readonly WarmupBudget Budget = new();
	private static SceneTree? _tree;
	private static SubViewport? _viewport;
	private static Control? _slot;
	private static bool _installed, _planningMenu;
	private static double _lastTick, _lastPlan, _cpuMs;
	private static string? _lastDraw;
	private static Vector2 _mouse;
	private static int _completed;
	internal static Action<string> Log = message => GD.Print(message);

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

	private static void Tick() => Stage.Safe("idle warmup", Advance);

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

		Queue<Job> queue = Jobs.Count != 0 ? Jobs : MenuJobs;
		Job job = queue.Peek();
		bool done = true;
		long start = Stopwatch.GetTimestamp();
		try
		{
			if (job.Load != null) done = job.Load();
			else if (job.Draw != null)
			{
				Control slot = CreateSlot(_tree.Root);
				_slot = slot;
				bool wasMuted = Sfx.Muted;
				Sfx.Muted = true;
				try { job.Draw(slot); }
				finally { Sfx.Muted = wasMuted; }
				_viewport!.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
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

	private static Control CreateSlot(Window root)
	{
		if (_viewport == null || !GodotObject.IsInstanceValid(_viewport))
		{
			_viewport = new SubViewport
			{
				Name = "P5IdleWarmup", Size = new Vector2I(128, 96), Disable3D = true, World2D = new World2D(),
				TransparentBg = root.TransparentBg, UseHdr2D = root.UseHdr2D, Msaa2D = root.Msaa2D,
				GuiDisableInput = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled,
			};
			root.AddChild(_viewport);
		}
		Control slot = new() { Size = _viewport.Size, MouseFilter = Control.MouseFilterEnum.Ignore };
		_viewport.AddChild(slot);
		return slot;
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

	private static int Pending => Jobs.Count + MenuJobs.Count;
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
			QueueShaders(typeof(SerpentMantle), typeof(SerpentMist), typeof(SerpentEclipse));
			Enqueue(new("serpent body", Draw: SerpentMantle.Warm));
			QueueSounds(new[] { Sfx.SerpentRise, Sfx.SerpentMaw });
			LogPlan("SerpentForm");
			return;
		}
		if (card == "GrandFinale")
		{
			if (!Groups.Add("GrandFinale")) return;
			QueueTextures(SatanaelPuppet.TextureNames().Prepend("sinful_platform"));
			QueueShaders(typeof(SinfulShell), typeof(SatanaelPuppet));
			Enqueue(new("finale sky / platform light", Draw: SinfulShell.WarmScene));
			Enqueue(new("finale Satanael light", Draw: SinfulShell.WarmPuppet));
			Enqueue(new("finale impact", Draw: SinfulShell.WarmImpact));
			QueueSounds(new[] { Sfx.SinfulSweepOpen, Sfx.SinfulSweepClose, Sfx.SinfulThunder, Sfx.SinfulTwirl,
				Sfx.SinfulCharge, Sfx.SinfulShot, Sfx.SinfulPierceLast });
			LogPlan("GrandFinale");
			return;
		}
		if (card == "Snakebite" && Groups.Add("Snakebite")) QueueShaders(typeof(SerpentMantle));
		if (card == "Outbreak" && Groups.Add("Outbreak")) QueueShaders(typeof(CrushHand));
		if (card == "WraithForm" && Groups.Add("WraithForm")) QueueShaders(typeof(WraithVeil));
		// 用当前实际注册的处理器，避开已被重做版覆盖的旧特效，也不扫描整个程序集。
		if (CardSkillFx.Cards.TryGetValue(card, out var effect) && effect.Method.DeclaringType is Type owner)
		{
			while (owner.IsNested && owner.DeclaringType != null) owner = owner.DeclaringType;
			QueueShaders(owner);
		}
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

	private static void QueueShaders(params Type[] types)
	{
		foreach (Type type in types)
		{
			if (!ShaderGroups.Add(type)) continue;
			foreach (PropertyInfo prop in type.GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
			{
				if (prop.PropertyType != typeof(Shader) || prop.GetIndexParameters().Length != 0) continue;
				PropertyInfo property = prop;
				Enqueue(new(type.Name + "." + prop.Name, Draw: slot =>
				{
					if (property.GetValue(null) is not Shader shader || shader.GetMode() != Shader.Mode.CanvasItem
						|| !Shaders.Add(shader.GetInstanceId())) return;
					slot.AddChild(new ColorRect { Material = new ShaderMaterial { Shader = shader },
						Size = new Vector2(8, 8), Position = new Vector2(60, 44), MouseFilter = Control.MouseFilterEnum.Ignore });
				}));
			}
		}
	}

	private static void LogPlan(string group) => Log($"[P5Joker] warmup idle-v2 requested {group}; pending={Pending}; textures={Textures.Count}; sounds={Sounds.Count}");
}
