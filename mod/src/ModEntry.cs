using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace P3Makoto;

[ModInitializer("Init")]
public static class ModEntry
{
	public const string Tag = "[P3Makoto]";
	public const string Version = "0.15.46";

	public static void Init()
	{
		if (!MountLocalContent()) return;
		GD.Print($"{Tag} init v{Version}");
		// v0.14.44（多版本兼容）：记下游戏版本，换版本出问题时日志里一眼能看到是哪个版本
		Safe("game version", () => GD.Print($"{Tag} game version {P3Makoto.Core.Compat.GameVersion()}"));
		// iOS 移植端加载 DLL 后，按运行时的动态代码能力决定是否尝试安装 Harmony 补丁。
		if (!CanPatch(out string why))
		{
			GD.Print($"{Tag} runtime code patching unavailable ({why}) -> effects disabled, only the skin art from the pck is used");
			Safe("asset guard", () => Core.AssetGuard.Apply("init (no patching)"));
			return;
		}
		try
		{
			// v0.14.31：逐个补丁类应用（PatchAll 遇到第一个失败就整体中断，后面的补丁全部不生效）；失败的单独记日志
			// v0.14.44：读补丁特性也放进 try —— 别的游戏版本里目标类型被删 / 改名时，读 typeof(X) 的特性本身就会抛异常
			Harmony harmony = new("brooks.p3makoto");
			int ok = 0, failed = 0;
			foreach (Type t in AccessTools.GetTypesFromAssembly(typeof(ModEntry).Assembly))
			{
				try
				{
					if (t.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
					{
						continue;
					}
				var applied = harmony.CreateClassProcessor(t).Patch();
				if (t == typeof(Patches.DamagePatch) && applied is { Count: > 0 })
					{
						Patches.DamagePatch.Installed = true;
						GD.Print($"{Tag} damage hook installed: {string.Join(", ", Patches.DamagePatch.TargetMethods())}");
					}
					ok++;
				}
				catch (Exception e)
				{
					failed++;
					GD.PrintErr($"{Tag} patch {t.Name} failed: {e.Message}");
				}
			}
			GD.Print($"{Tag} harmony patches applied: {ok} ok, {failed} failed");
		}
		catch (Exception e)
		{
			GD.PrintErr($"{Tag} harmony patch failed: {e}");
		}
		// v0.14.48：把原版储君路径指回本 mod 的资源；所有 mod 加载完之后（下一帧）再来一次，盖过后加载的 mod
		Safe("asset guard", () => Core.AssetGuard.Apply("init"));
		Callable.From(() => Safe("cursor late init", Patches.CursorPatch.RefreshExisting)).CallDeferred();
		if (Engine.GetMainLoop() is SceneTree tree)
		{
			tree.CreateTimer(0.0).Timeout += () => Safe("asset guard", () => Core.AssetGuard.Apply("after all mods"));
		}
		Safe("timescale watchdog", Core.TimeScale.Install);     // v0.14.51：减速没恢复时自己恢复（玩家：一直像子弹时间）
		Safe("health log", Core.Health.Install);              // v0.14.60：卡顿诊断日志（每 60 秒一行 + 低帧率时的明细）
		Safe("sfx prewarm", Audio.Sfx.Prewarm);
		Safe("shop assets", Vfx.ShopAssets.Watch);         // 0.15.30：商店房间的贴图 / 模型 / 着色器在地图空闲时提前准备
		Safe("star orbit", Vfx.StarOrbit.Install);
		Safe("preload", Vfx.Preload.Start);                  // 选中结城理时要用的大资源后台预加载 + 着色器预编译（避免第一次选中掉帧）
		// 低血溺水暗角已按用户要求关闭（Vfx/LowHpFx.cs 保留未用）
		try
		{
			Debug.Showcase.TryStart();
		}
		catch (Exception e)
		{
			GD.PrintErr($"{Tag} showcase start failed: {e}");
		}
	}

	// The loader normally mounts PCKs before calling Init. The manifest opts out
	// so a Workshop installation cannot replace even a vanilla texture.
	private static bool MountLocalContent()
	{
		string? directory = Path.GetDirectoryName(typeof(ModEntry).Assembly.Location);
		var candidates = ModManager.Mods.Where(m => m.manifest?.id == "P3Makoto").ToArray();
		var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		var source = candidates.FirstOrDefault(m => string.Equals(
			Path.GetFullPath(m.path), directory, comparison));
		if (source == null && candidates.Length == 1) source = candidates[0];
		if (source?.modSource == ModSource.SteamWorkshop || WorkshopId(source) > 0
			|| (source == null && directory?.Replace('\\', '/').Contains(
				"/steamapps/workshop/content/2868840/", StringComparison.OrdinalIgnoreCase) == true))
		{
			GD.Print($"{Tag} Workshop distribution is not enabled.");
			return false;
		}
		if (source?.manifest is { } manifest && (manifest.author != "B站川川errr 抖音川川" || manifest.description != "MOD唯一作者：B站川川errr 抖音川川。如果您在B站、抖音、玩家动力之外的渠道获取本MOD，均为盗版！"))
		{
			GD.PrintErr($"{Tag} The author or distribution notice differs from the release manifest.");
			return false;
		}
		string? root = source?.path ?? directory;
		if (string.IsNullOrEmpty(root)) return false;
		string pack = Path.Combine(root, "P3Makoto.pck");
		if (!File.Exists(pack))
		{
			GD.PrintErr($"{Tag} Missing local resource pack: {pack}");
			return false;
		}
		// Managers scan manifests before initializers. Advertise only this validated
		// local installation on its next launch; the distributed manifest stays opt-out.
		// 只认真正启用的 SkinChanger（在设置里被关掉的也会出现在 Mods 列表里，state = Disabled）。
		bool skinChanger = ModManager.Mods.Any(m => m.manifest?.id == "Gurio.SkinChanger"
			&& m.state is not (ModLoadState.Disabled or ModLoadState.DisabledDuplicate or ModLoadState.Failed));
		if (source?.manifest != null && skinChanger)
			EnableLocalPackDiscovery(root);
		if (ResourceLoader.Exists("res://p3makoto/ui/charselect_portrait.png")) return true;
		// SkinChanger 已经按皮肤接管了这个包（json 里 has_pck = true，它故意不全局挂载，只在选中这套皮肤时按需挂）：
		// 这时候自己再全局挂一次，会把原版角色的资源全局换掉，选别的皮肤也会混进来 → 交给 SkinChanger，不自己挂。
		if (skinChanger && source?.manifest?.hasPck == true)
		{
			GD.Print($"{Tag} resource pack is managed by SkinChanger; not mounting it globally.");
			return true;
		}
		if (ProjectSettings.LoadResourcePack(pack)) return true;
		GD.PrintErr($"{Tag} Could not mount local resource pack: {pack}");
		return false;
	}

	// Mod.workshopId 是 0.111 才有的字段：直接写 source.workshopId，0.107 上整个 MountLocalContent 一调用就抛
	// MissingFieldException，mod 初始化失败、什么都不生效（用户 2026-11-26：0.107 的玩家装上没用）→ 反射读，读不到当 0。
	private static decimal WorkshopId(Mod? mod)
	{
		if (mod == null) return 0;
		try
		{
			const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
			object? value = typeof(Mod).GetField("workshopId", flags)?.GetValue(mod) ?? typeof(Mod).GetProperty("workshopId", flags)?.GetValue(mod);
			return value == null ? 0 : Convert.ToDecimal(value);
		}
		catch (Exception) { return 0; }
	}

	private static void EnableLocalPackDiscovery(string root)
	{
		string path = Path.Combine(root, "P3Makoto.json");
		string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
		try
		{
			var doc = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
			if (doc?["id"]?.GetValue<string>() != "P3Makoto" || doc["has_pck"]?.GetValue<bool>() != false) return;
			doc["has_pck"] = true;
			File.WriteAllText(temp, doc.ToJsonString(new System.Text.Json.JsonSerializerOptions {
				WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
			}));
			File.Move(temp, path, true);
			GD.Print($"{Tag} Local skin pack discovery enabled. Restart the game once to refresh the manager catalog.");
		}
		catch (Exception e) { GD.PrintErr($"{Tag} Could not enable local pack discovery at {path}: {e.Message}"); }
		finally { try { File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
	}

	private static bool CanPatch(out string why)
	{
		why = "";
		try
		{
			string os = OS.GetName();
			if (os == "Web")
			{
				why = "platform " + os;
				return false;
			}
			// iOS 移植端是解释执行（IsDynamicCodeCompiled = false），但 Harmony 一样能挂补丁 —— P5Joker 在 iOS 上就是这样跑的。
			// 之前这里要求 IsDynamicCodeCompiled，iOS 上就把全部补丁和特效关掉了；现在只在完全不支持动态代码时才放弃，
			// 其余情况逐个补丁类尝试，挂不上的单独记日志跳过（同 P5Joker）。
			GD.Print($"{Tag} runtime: os={os} dynamicCode={System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported} compiled={System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeCompiled}");
			if (!System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported)
			{
				why = "no dynamic code";
				return false;
			}
		}
		catch (Exception e)
		{
			why = e.GetType().Name;
			return false;
		}
		return true;
	}

	/// <summary>特效代码出错时只记日志，绝不让异常冒泡进游戏逻辑。</summary>
	public static void Safe(string where, Action action)
	{
		long t0 = Core.Prof.Begin();                       // v0.14.64：耗时统计（见 Core/Prof.cs）
		try
		{
			action();
		}
		catch (Exception e)
		{
			Report(where, e);
		}
		finally
		{
			Core.Prof.End(where, t0);
		}
	}

	/// <summary>记一次出错。v0.14.44：同一处反复出错（例如别的游戏版本里某个成员不存在，每次调用都会失败）只记前 3 次，免得刷满日志拖慢游戏。</summary>
	public static void Report(string where, Exception e)
	{
		int n = Failures.AddOrUpdate(where, 1, (_, c) => c + 1);
		if (n <= 3)
		{
			Warn($"{where} failed: {e}" + (n == 3 ? " (further errors here are not logged)" : ""));
		}
	}

	private static readonly ConcurrentDictionary<string, int> Failures = new();

	/// <summary>
	/// 报错同时写进 Godot 输出和原版日志（MegaCrit Log）：原版日志会被测试工具的多人日志（P3TestMod MpLog）一并收集，
	/// 联机测试时能看到本 mod 的报错发生在哪个动作前后。
	/// </summary>
	public static void Warn(string text)
	{
		GD.PrintErr($"{Tag} {text}");
		try
		{
			MegaCrit.Sts2.Core.Logging.Log.Warn($"{Tag} {text}");
		}
		catch
		{
		}
	}
}

/// <summary>
/// v0.14.44（多版本兼容）：补丁方法的保护壳。补丁本体放进单独的方法（不内联），由壳 try/catch 调用 ——
/// 别的游戏版本里补丁用到的成员不存在时，异常在调用本体时抛出，能被壳接住，退回原版行为。
/// 这类"成员不存在 / 类型加载失败"的错误每次都会重复，第一次出现就把这个补丁关掉（之后直接走原版，不再尝试）；
/// 其他偶发错误只记日志，下次照常尝试。
/// </summary>
internal static class Guard
{
	private static readonly ConcurrentDictionary<string, byte> Disabled = new();

	public static bool Off(string key) => Disabled.ContainsKey(key);

	public static void Fail(string key, Exception e)
	{
		if (e is MissingMemberException or TypeLoadException or System.IO.FileNotFoundException or BadImageFormatException or InvalidProgramException)
		{
			if (Disabled.TryAdd(key, 0))
			{
				ModEntry.Warn($"{key}: not compatible with this game version, using vanilla behaviour: {e.GetType().Name}: {e.Message}");
			}
			return;
		}
		ModEntry.Report(key, e);
	}
}
