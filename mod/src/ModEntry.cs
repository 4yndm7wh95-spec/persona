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
		GD.Print($"{Tag} init v0.10.1");
		try
		{
			new Harmony("brooks.p3makoto").PatchAll(typeof(ModEntry).Assembly);
			GD.Print($"{Tag} harmony patches applied");
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
			GD.PrintErr($"{Tag} {where} failed: {e}");
		}
	}
}
