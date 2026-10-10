using System;
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
		GD.Print($"{Tag} init v0.0.1");
		try
		{
			new Harmony("brooks.p3makoto").PatchAll(typeof(ModEntry).Assembly);
			GD.Print($"{Tag} harmony patches applied");
		}
		catch (Exception e)
		{
			GD.PrintErr($"{Tag} harmony patch failed: {e}");
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
			GD.PrintErr($"{Tag} {where} failed: {e}");
		}
	}
}
