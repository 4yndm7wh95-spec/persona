using System;
using System.Collections.Concurrent;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace P3Makoto;

[ModInitializer("Init")]
public static class ModEntry
{
	public const string Tag = "[P3Makoto]";

	public static void Init()
	{
		GD.Print($"{Tag} init v0.14.46");
		// v0.14.44（多版本兼容）：记下游戏版本，换版本出问题时日志里一眼能看到是哪个版本
		Safe("game version", () => GD.Print($"{Tag} game version {P3Makoto.Core.Compat.GameVersion()}"));
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
					harmony.CreateClassProcessor(t).Patch();
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
		Safe("sfx prewarm", Audio.Sfx.Prewarm);
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

	/// <summary>特效代码出错时只记日志，绝不让异常冒泡进游戏逻辑。</summary>
	public static void Safe(string where, Action action)
	{
		try
		{
			action();
		}
		catch (Exception e)
		{
			Report(where, e);
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
