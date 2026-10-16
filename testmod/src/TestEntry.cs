using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace P3TestMod;

/// <summary>
/// P3 测试工具（只用于展示 / 测试特效和动画）：
/// · v1.2：正常关卡；左上角「换成木桩」（当前战斗的怪物换成打不死的木桩）、「+3 能量」「+3 辉星」；
/// · F8 打开面板：按原版卡面显示卡牌，点一下加入手牌；还有击杀敌人 / 让我受击 / 清空手牌 / 不死开关 / 打开日志文件夹；
/// · 支持联机：会改游戏状态的按钮都发联机同步的控制台命令（Net / TestCmd），所有玩家都要装这个测试 mod；
/// · 多人日志（MpLog）：每个游戏进程单独一个日志文件，含回合状态快照，方便对比两端是否同步；
/// · 导演接口（Bridge）：本机 127.0.0.1:47021，供录制脚本 / MCP 驱动游戏，见 README。
/// </summary>
[ModInitializer("Init")]
public static class TestEntry
{
	public const string Tag = "[P3TestMod]";
	public const string Version = "1.2.2";

	public static void Init()
	{
		GD.Print($"{Tag} init v{Version}");
		Safe("mp log", MpLog.Start);
		try
		{
			new Harmony("brooks.p3testmod").PatchAll(typeof(TestEntry).Assembly);
			GD.Print($"{Tag} harmony patches applied");
			MpLog.Event("harmony patches applied");
		}
		catch (Exception e)
		{
			GD.PrintErr($"{Tag} harmony patch failed: {e}");
		}
		Safe("runtime", Runtime.Install);
		Safe("bridge", Bridge.Start);
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
