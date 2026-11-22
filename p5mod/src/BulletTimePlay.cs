using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>
/// 子弹时间的游戏接法（特效本体在 Vfx/TimeStop.cs）：
/// · 敌人列表：战斗房间里活着的敌人节点，动画速度用骨骼动画的 AnimationState.SetTimeScale（只慢敌人，Joker 和全局时间不动）；
/// · 结束：跟随出牌者的回合和房间生命周期。
/// 时停期间出牌照常立刻结算（不动游戏逻辑），只改表现。
/// </summary>
public static class BulletTimePlay
{
	private static bool _installed;
	private sealed class Turn { public int Revision; }
	private static readonly ConditionalWeakTable<Creature, Turn> Turns = new();
	private static readonly List<TimeStop.Actor> LiveEnemies = new();
	private static readonly ConditionalWeakTable<NCreature, EnemySpeed> SpeedBindings = new();

	private sealed class EnemySpeed(NCreature node)
	{
		internal readonly Action<float> Apply = speed =>
		{
			if (GodotObject.IsInstanceValid(node) && node.Visuals?.SpineBody?.TryGetAnimationState() is { } state)
				state.SetTimeScale(speed);
		};
	}

	public static void Install()
	{
		if (_installed) return;
		_installed = true;
		TimeStop.Enemies = Enemies;
	}

	internal static Func<bool> BindTurn(Creature owner, NCombatRoom room)
	{
		var combat = owner.CombatState;
		int? turnNumber = owner.Player?.PlayerCombatState?.TurnNumber;
		Turn turn = Turns.GetOrCreateValue(owner);
		int revision = turn.Revision;
		return () => turn.Revision == revision && combat != null && turnNumber != null
			&& Fx.Active(room) && NCombatRoom.Instance == room && owner.CombatState == combat
			&& Joker.Is(owner.Player)
			&& owner.IsAlive && !CombatManager.Instance.IsOverOrEnding && combat.CurrentSide == owner.Side
			&& owner.Player?.PlayerCombatState?.TurnNumber == turnNumber;
	}

	internal static void EndTurn(Creature owner)
	{
		if (Turns.TryGetValue(owner, out Turn? turn)) turn.Revision++;
	}

	private static List<TimeStop.Actor> Enemies()
	{
		LiveEnemies.Clear();
		if (NCombatRoom.Instance is not NCombatRoom room) return LiveEnemies;
		foreach (NCreature n in room.CreatureNodes)
		{
			if (!GodotObject.IsInstanceValid(n) || n.Entity is not Creature c || !c.IsEnemy || c.IsDead || n.Visuals is not { } v) continue;
			Rect2 hb = n.Hitbox.GetGlobalRect();
			Action<float> speed = SpeedBindings.GetValue(n, static node => new EnemySpeed(node)).Apply;
			LiveEnemies.Add(new TimeStop.Actor(v, n.VfxSpawnPosition, hb.Grow(hb.Size.Y * 0.15f), speed));
		}
		return LiveEnemies;
	}
}

[HarmonyPatch]
public static class BulletTimeTurnEndPatch
{
	public static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(Hook), "AfterSideTurnEnd")
		?? AccessTools.Method(typeof(Hook), "AfterTurnEnd");
	public static void Prefix(CombatSide side, IEnumerable<Creature> participants)
	{
		if (side != CombatSide.Player) return;
		Stage.Safe("bullet time end", () =>
		{
			foreach (Creature owner in participants)
				BulletTimePlay.EndTurn(owner);
		});
	}
}
