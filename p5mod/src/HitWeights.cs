using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace P5Joker;

/// <summary>
/// 打击确认感的底档（Vfx/Impact.cs 再按实际伤害升档）。用户 2026-11-02：太频繁容易花眼、看腻，所以"看牌为主、看伤害为辅"：
/// · 小刀（子弹）每一发、多段攻击的前几下 → 轻；
/// · 普通单发攻击、多段攻击的最后一下 → 中；
/// · 大招类攻击牌（下表）→ 至少重（多段的大招只有最后一下重，前几下照样轻）。
/// 多段：AttackCommand.Execute 开始时记下这张牌的段数（_hitCount），之后每次结算 CreatureCmd.Damage 数一下，数到最后一下就是收尾。
/// 遗物 / 能力加出来的额外段数（Hook.ModifyAttackHitCount）不在 _hitCount 里，多出来的那几下按收尾算，不会更乱。
/// </summary>
public static class HitWeights
{
	private static readonly HashSet<string> Big = new()
	{
		"Assassinate",      // 刺杀
		"Murder",           // 谋杀
		"MementoMori",      // 铭记死亡
		"TheHunt",          // 狩猎
		"GrandFinale",      // 华丽收场
		"Predator",         // 猎杀者
		"Skewer",           // 串刺（最后一下）
		"Finisher",         // 终结技（最后一下）
	};

	private static CardModel? _card;
	private static int _hits = 1, _index;

	public static void BeginAttack(CardModel card, int hitCount)
	{
		_card = card;
		_hits = hitCount < 1 ? 1 : hitCount;
		_index = 0;
	}

	/// <summary>这张牌又结算了一下：返回底档、以及是不是多段攻击的前几下（calm：最多升到中）。</summary>
	public static (Weight Floor, bool Calm) NextHit(CardModel card, bool gun)
	{
		if (!ReferenceEquals(card, _card))
		{
			BeginAttack(card, 1);                  // 不走 AttackCommand 的伤害（直接 CreatureCmd.Damage）当单发
		}
		_index++;
		bool last = _index >= _hits;
		if (!last)
		{
			return (Weight.Light, true);
		}
		if (Big.Contains(card.GetType().Name))
		{
			return (Weight.Heavy, false);
		}
		return (gun ? Weight.Light : Weight.Medium, false);
	}
}
