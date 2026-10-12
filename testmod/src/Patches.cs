using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace P3TestMod;

/// <summary>
/// 所有房间都换成训练假人战斗：拦截原版生成房间的统一入口 RunManager.CreateRoom（地图本身除外）。
/// 地图照常显示、照常行走，走到哪个节点都是同一场假人战斗；假人战斗不给奖励，打完直接回地图。
/// </summary>
[HarmonyPatch(typeof(RunManager), "CreateRoom")]
public static class AllRoomsAreDummyPatch
{
	public static bool Prefix(RunManager __instance, RoomType roomType, ref AbstractRoom __result)
	{
		if (roomType == RoomType.Map)
		{
			return true;
		}
		RunState? state = Traverse.Create(__instance).Property("State").GetValue<RunState>();
		if (state == null)
		{
			return true;
		}
		EncounterModel encounter = ModelDb.Encounter<BattlewornDummyEventV1Encounter>().ToMutable();
		encounter.DebugRandomizeRng();                                   // 和原版 fight 命令一样：给遭遇战一个随机种子
		__result = new CombatRoom(encounter, state);
		return false;
	}
}

/// <summary>训练假人上场时会挂一个"3 回合时限"（时间到战斗就结束）：测试时不要。</summary>
[HarmonyPatch(typeof(BattleFriendV1), nameof(BattleFriendV1.AfterAddedToRoom))]
public static class DummyNoTimeLimitPatch
{
	public static bool Prefix(ref Task __result)
	{
		__result = Task.CompletedTask;
		return false;
	}
}

/// <summary>能量始终不低于 99：出牌扣费会被立刻顶回 99（直接改数值，不触发"获得能量"的动画）。</summary>
[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.Energy), MethodType.Setter)]
public static class InfiniteEnergyPatch
{
	public static void Prefix(ref int value)
	{
		if (value < Runtime.Infinite)
		{
			value = Runtime.Infinite;
		}
	}
}

/// <summary>辉星始终不低于 99。</summary>
[HarmonyPatch(typeof(PlayerCombatState), nameof(PlayerCombatState.Stars), MethodType.Setter)]
public static class InfiniteStarsPatch
{
	public static void Prefix(ref int value)
	{
		if (value < Runtime.Infinite)
		{
			value = Runtime.Infinite;
		}
	}
}
