using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Monsters;

namespace P3TestMod;

// v1.2：不再把所有房间换成假人、能量 / 辉星也不再无限（正常关卡）。
// 下面这些补丁只改"被测试按钮召出来的木桩"和"开了不死的玩家"，而且全部是确定性的：
// 触发它们的状态（Runtime.DummyEncounter / Runtime.Immortal）只由联机同步的控制台命令（TestCmd）改写，
// 每个玩家的游戏里都在同一个动作里、按同样的顺序执行，所以联机时各端的数值保持一致，不会不同步。

/// <summary>木桩召出来的时候血量 999999（只对 p3test dummy 召出的那一场生效；原版"战损假人"事件不受影响）。</summary>
[HarmonyPatch(typeof(BattleFriendV1), nameof(BattleFriendV1.MinInitialHp), MethodType.Getter)]
public static class DummyMinHpPatch
{
	public static void Postfix(ref int __result)
	{
		if (Runtime.DummyPending)
		{
			__result = Runtime.DummyHp;
		}
	}
}

[HarmonyPatch(typeof(BattleFriendV1), nameof(BattleFriendV1.MaxInitialHp), MethodType.Getter)]
public static class DummyMaxHpPatch
{
	public static void Postfix(ref int __result)
	{
		if (Runtime.DummyPending)
		{
			__result = Runtime.DummyHp;
		}
	}
}

/// <summary>木桩上场时会挂一个"3 回合时限"（时间到战斗就结束）：测试按钮召出来的木桩不要。</summary>
[HarmonyPatch(typeof(BattleFriendV1), nameof(BattleFriendV1.AfterAddedToRoom))]
public static class DummyNoTimeLimitPatch
{
	public static bool Prefix(ref System.Threading.Tasks.Task __result)
	{
		if (!Runtime.InDummyCombat)
		{
			return true;
		}
		__result = System.Threading.Tasks.Task.CompletedTask;
		return false;
	}
}

/// <summary>
/// 扣血（原版所有伤害最后都走 Creature.LoseHpInternal，各端在同一个动作里执行）：
/// · 开了不死的玩家：这一下最多扣到剩 1 点；扣完低于 30% 就补满；
/// · 测试木桩：扣完低于一半就补满（血量真的归零 = "击杀木桩"按钮 / kill 命令，不拦）。
/// </summary>
[HarmonyPatch(typeof(Creature), nameof(Creature.LoseHpInternal))]
public static class LoseHpPatch
{
	public static void Prefix(Creature __instance, ref decimal amount)
	{
		if (Runtime.Immortal && __instance.IsPlayer && __instance.CurrentHp > 1 && amount >= __instance.CurrentHp)
		{
			amount = __instance.CurrentHp - 1;
		}
	}

	public static void Postfix(Creature __instance)
	{
		if (__instance.CurrentHp <= 0)
		{
			return;
		}
		if (__instance.IsPlayer)
		{
			if (Runtime.Immortal && __instance.CurrentHp < __instance.MaxHp * 0.3m)
			{
				__instance.SetCurrentHpInternal(__instance.MaxHp);
				MpLog.Event($"immortal refill player {__instance.Player?.NetId} -> {__instance.MaxHp}");
			}
			return;
		}
		if (__instance.Monster is BattleFriendV1 && Runtime.InDummyCombat && __instance.CurrentHp < __instance.MaxHp / 2)
		{
			__instance.SetCurrentHpInternal(__instance.MaxHp);
		}
	}
}

/// <summary>和 CombatManager.CombatSetUp 一起：木桩那一场的血量设定只对这一次建房生效。</summary>
public static class DummyHooks
{
	public static void OnCombatSetUp(CombatState state)
	{
		if (Runtime.DummyPending)
		{
			Runtime.DummyEncounter = state.Encounter;            // 记下这一场实际用的遭遇实例（各端各自记自己的，同一时刻）
			Runtime.DummyPending = false;
			MpLog.Event($"dummy combat set up: encounter={state.Encounter?.Id.Entry} enemies={state.Enemies.Count} hp={string.Join(",", System.Linq.Enumerable.Select(state.Enemies, e => e.CurrentHp + "/" + e.MaxHp))}");
		}
	}
}
