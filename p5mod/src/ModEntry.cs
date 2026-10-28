using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace P5Joker;

[ModInitializer("Init")]
public static class ModEntry
{
	public const string Tag = "[P5Joker]";
	public const string Version = "0.3.2";

	public static void Init()
	{
		GD.Print($"{Tag} init v{Version}");
		// 逐个补丁类应用：一个失败（别的游戏版本里目标被改名）不影响其他
		Harmony harmony = new("brooks.p5joker");
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

	/// <summary>特效出错只记日志，绝不打断游戏流程。</summary>
	public static void Safe(string where, Action action)
	{
		try
		{
			action();
		}
		catch (Exception e)
		{
			GD.PrintErr($"{Tag} {where}: {e}");
		}
	}
}
