using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>
/// 幽魂形态的游戏接法（特效本体在 Vfx/WraithVeil.cs）：
/// · 被打：无实体把掉血压成 1 之后会调 AfterModifyingHpLostAfterOsty → 给一下反馈；
/// · 结束：无实体在敌方回合结束（AfterSideTurnEnd）时减一层，减到 0 那次收掉；
/// · 兜底：WraithVeil.StillOn 每半秒看一眼 Joker 身上还有没有无实体（被别的方式移除、漏了钩子时也能收掉）。
/// 只改表现，不动游戏逻辑。
/// </summary>
public static class WraithPlay
{
	private static bool _installed;

	public static void Install()
	{
		if (_installed) return;
		_installed = true;
		WraithVeil.StillOn = StillOn;
	}

	private static bool StillOn()
	{
		if (NCombatRoom.Instance is not NCombatRoom room) return false;
		foreach (NCreature n in room.CreatureNodes)
		{
			if (!GodotObject.IsInstanceValid(n) || n.Entity is not Creature c || c.Player is not { } p || !Joker.Is(p)) continue;
			if (c.GetPower<IntangiblePower>() != null) return true;
		}
		return false;
	}

	internal static bool Mine(IntangiblePower power) => power.Owner is { Player: { } p } && Joker.Is(p);
}

[HarmonyPatch(typeof(IntangiblePower), nameof(IntangiblePower.AfterModifyingHpLostAfterOsty))]
public static class WraithHitPatch
{
	public static void Prefix(IntangiblePower __instance)
	{
		Stage.Safe("wraith hit", () =>
		{
			if (WraithVeil.Current != null && WraithPlay.Mine(__instance)) WraithVeil.Current.Hit();
		});
	}
}

[HarmonyPatch(typeof(IntangiblePower), nameof(IntangiblePower.AfterSideTurnEnd))]
public static class WraithTurnEndPatch
{
	public static void Prefix(IntangiblePower __instance, CombatSide side)
	{
		Stage.Safe("wraith end", () =>
		{
			if (WraithVeil.Current != null && side == CombatSide.Enemy && WraithPlay.Mine(__instance) && __instance.Amount <= 1) WraithVeil.StopCurrent();
		});
	}
}
