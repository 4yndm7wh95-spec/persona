using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>
/// 子弹时间的游戏接法（特效本体在 Vfx/TimeStop.cs）：
/// · 敌人列表：战斗房间里活着的敌人节点，动画速度用骨骼动画的 AnimationState.SetTimeScale（只慢敌人，Joker 和全局时间不动）；
/// · 结束：原版子弹时间给自己挂 NoDrawPower，它在己方回合结束（AfterSideTurnEnd）时移除 → 那一刻时间回来。
/// 时停期间出牌照常立刻结算（不动游戏逻辑），只改表现。
/// </summary>
public static class BulletTimePlay
{
	private static bool _installed;

	public static void Install()
	{
		if (_installed) return;
		_installed = true;
		TimeStop.Enemies = Enemies;
	}

	private static List<TimeStop.Actor> Enemies()
	{
		var list = new List<TimeStop.Actor>();
		if (NCombatRoom.Instance is not NCombatRoom room) return list;
		foreach (NCreature n in room.CreatureNodes)
		{
			if (!GodotObject.IsInstanceValid(n) || n.Entity is not Creature c || !c.IsEnemy || c.IsDead || n.Visuals is not { } v) continue;
			NCreature node = n;
			Rect2 hb = n.Hitbox.GetGlobalRect();
			list.Add(new TimeStop.Actor(v, n.VfxSpawnPosition, hb.Grow(hb.Size.Y * 0.15f), s =>
			{
				if (GodotObject.IsInstanceValid(node) && node.Visuals?.SpineBody?.TryGetAnimationState() is { } st) st.SetTimeScale(s);
			}));
		}
		return list;
	}
}

[HarmonyPatch(typeof(NoDrawPower), nameof(NoDrawPower.AfterSideTurnEnd))]
public static class BulletTimeTurnEndPatch
{
	public static void Prefix(NoDrawPower __instance, IEnumerable<Creature> participants)
	{
		Stage.Safe("bullet time end", () =>
		{
			if (TimeStop.Current != null && __instance.Owner is { Player: { } p } owner && Joker.Is(p) && participants.Contains(owner))
			{
				TimeStop.StopCurrent();
			}
		});
	}
}
