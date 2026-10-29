using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P5Joker;

[ModInitializer("Init")]
public static class ModEntry
{
	public const string Tag = "[P5Joker]";
	public const string Version = "0.5.0";

	public static void Init()
	{
		GD.Print($"{Tag} init v{Version}");
		Stage.Report = (where, e) => GD.PrintErr($"{Tag} {where}: {e}");
		Stage.Shake = s => NGame.Instance?.ScreenShake(s switch
		{
			<= 1 => ShakeStrength.VeryWeak,
			2 => ShakeStrength.Weak,
			3 => ShakeStrength.Medium,
			_ => ShakeStrength.Strong,
		}, ShakeDuration.Short);
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
	public static void Safe(string where, Action action) => Stage.Safe(where, action);
}
