using System;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace P5Joker;

/// <summary>
/// 毒必杀判定（用户 2026-11-25：毒确定能在本回合结束后毒死敌人时，在敌人身上加个标记；要考虑触媒这些）。
/// 毒在敌人那一方回合开始时跳（PoisonPower.AfterSideTurnStart），每跳 = 当前层数、跳完减一，加速剂让它多跳几次（不超过层数）。
/// 「确定」= 只在稳死时亮：
/// · 伤害：原版 PoisonPower.CalculateTotalDamageNextTurn 的算法（加速剂次数、伤害修正、无实体的伤害封顶都在 Hook.ModifyDamage 里），
///   每跳再过一遍 HP 损失修正（Hook.ModifyHpLost：缓冲、滑溜这类只在扣血那一步生效的，原版预览没算）；硬壳每回合总扣血有上限，累计封顶。
///   缓冲 / 滑溜在预览里每跳都会生效（实际只挡前几跳）→ 偏保守，宁可不亮也不乱亮。
/// · 救命：Hook.ShouldDie（有东西会拦下死亡）→ 不算。
/// · 复活：Hook.ShouldCreatureBeRemovedFromCombatAfterDeath 为假（实验体前两条命、再生 / 幻影这类）→ 不算；实验体第三条命去掉了可复活的状态，照常算。
/// 触媒（层数翻倍 / 三倍）、新上的毒、加速剂变化、无实体等都会改变层数或修正，血条每次刷新都重新算，所以标记实时跟着变。
/// </summary>
internal static class PoisonLethal
{
	private const ValueProp Props = ValueProp.Unblockable | ValueProp.Unpowered;

	public static bool Sure(Creature c)
	{
		if (c.IsPlayer || !c.IsAlive || c.CurrentHp <= 0 || c.HpDisplay.IsInfinite()) return false;
		if (c.GetPower<PoisonPower>() is not { Amount: > 0 } poison) return false;
		var cs = c.CombatState;
		var rs = cs?.RunState;
		if (cs == null || rs == null) return false;
		// 同 PoisonPower.TriggerCount（私有）：对面活着的人身上的加速剂层数之和 + 1，不超过毒层数
		int accel = cs.GetOpponentsOf(c).Where(o => o.IsAlive).Sum(o => o.GetPowerAmount<AccelerantPower>());
		int triggers = Math.Min(poison.Amount, 1 + accel);
		decimal hp = c.CurrentHp, lost = 0m;
		decimal shellCap = c.GetPower<HardenedShellPower>() is { } shell ? shell.Amount : decimal.MaxValue;
		for (int i = 0; i < triggers && hp > 0m; i++)
		{
			decimal dmg = Hook.ModifyDamage(rs, cs, c, null, poison.Amount - i, Props, null, null, ModifyDamageHookType.All, CardPreviewMode.None, out _);
			decimal loss = Math.Max(0m, Math.Floor(Hook.ModifyHpLost(rs, cs, c, dmg, Props, null, null, HpLossHookPhase.All, out _)));
			loss = Math.Min(loss, Math.Max(0m, shellCap - lost));
			lost += loss;
			hp -= loss;
		}
		if (hp > 0m) return false;
		if (!Hook.ShouldDie(rs, cs, c, out _)) return false;
		return Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(cs, c);
	}
}
