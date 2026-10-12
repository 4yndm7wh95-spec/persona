using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace P3TestMod;

/// <summary>
/// P3 测试工具（只用于展示 / 测试特效和动画）：
/// · 所有房间（普通 / 精英 / Boss / 商店 / 休息处 / 事件 / 宝箱）都换成一场训练假人战斗，假人不攻击、打不死；
/// · 能量和辉星始终不低于 99；
/// · F8 打开面板：按原版卡面显示卡牌，点一下加入手牌；还有击杀假人 / 让我受击 / 清空手牌等按钮。
/// 实现全部调用原版开发者控制台命令用的同一套接口（card / energy / stars / fight / damage / kill）。
/// 只用于单人测试，不要在正式对局或联机里开。
/// </summary>
[ModInitializer("Init")]
public static class TestEntry
{
	public const string Tag = "[P3TestMod]";

	public static void Init()
	{
		GD.Print($"{Tag} init v1.0.1");
		try
		{
			new Harmony("brooks.p3testmod").PatchAll(typeof(TestEntry).Assembly);
			GD.Print($"{Tag} harmony patches applied");
		}
		catch (Exception e)
		{
			GD.PrintErr($"{Tag} harmony patch failed: {e}");
		}
		Safe("runtime", Runtime.Install);
	}

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
