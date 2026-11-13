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
	public const string Version = "0.9.30";

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
		Stage.PlayEvent = (e, v) => MegaCrit.Sts2.Core.Commands.SfxCmd.Play(e, v);
		Stage.ShakeDir = (s, deg) => NGame.Instance?.ScreenShake(Strength(s), ShakeDuration.Short, deg);
		BulletTimePlay.Install();
		// 毒雾的常驻咒怨雾：Joker 还在战斗里、身上还有毒雾能力就一直留着（Vfx/FumesField.cs）
		FumesField.Alive = () =>
		{
			if (MegaCrit.Sts2.Core.Nodes.Rooms.NCombatRoom.Instance is not { } room) return false;
			foreach (var n in room.CreatureNodes)
			{
				if (n.Entity is { IsPlayer: true, IsDead: false } c && c.Player is { } p && Joker.Is(p)
					&& c.GetPower<MegaCrit.Sts2.Core.Models.Powers.NoxiousFumesPower>() != null) return true;
			}
			return false;
		};
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

	private static ShakeStrength Strength(int s) => s switch
	{
		<= 1 => ShakeStrength.VeryWeak,
		2 => ShakeStrength.Weak,
		3 => ShakeStrength.Medium,
		_ => ShakeStrength.Strong,
	};

	/// <summary>特效出错只记日志，绝不打断游戏流程。</summary>
	public static void Safe(string where, Action action) => Stage.Safe(where, action);
}
