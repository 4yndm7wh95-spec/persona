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
		GD.Print($"{Tag} init v0.14.43");
		try
		{
			// v0.14.31：逐个补丁类应用（PatchAll 遇到第一个失败就整体中断，后面的补丁全部不生效）；失败的单独记日志
			Harmony harmony = new("brooks.p3makoto");
			int ok = 0, failed = 0;
			foreach (Type t in AccessTools.GetTypesFromAssembly(typeof(ModEntry).Assembly))
			{
				if (t.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0)
				{
					continue;
				}
				try
				{
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
			Warn($"{where} failed: {e}");
		}
	}

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
