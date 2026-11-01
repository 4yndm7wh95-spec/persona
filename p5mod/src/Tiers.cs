using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace P5Joker;

/// <summary>特效分级（用户 2026-11-01：按卡牌稀有度做简单的特效划分，普通白卡不需要华丽完整的演出）。</summary>
public enum Tier
{
	/// <summary>基础牌：只有 Joker 自己的动作和命中特效。</summary>
	Basic,
	/// <summary>普通（白卡）：一个干净的技能特效打在目标上（≤0.6s），不召唤、不出舞台 / 眼神特写。</summary>
	Common,
	/// <summary>罕见（蓝卡）：人格面具短暂现身（亚森在身后被蓝焰烧出来、出手、消失），不出舞台 / 眼神特写。</summary>
	Uncommon,
	/// <summary>稀有（金卡）：完整召唤 —— 红色舞台 + 眼神特写 + 人格面具 + 完整演出。</summary>
	Rare,
}

public static class Tiers
{
	public static Tier For(CardModel card) => card.Rarity switch
	{
		CardRarity.Basic => Tier.Basic,
		CardRarity.Uncommon => Tier.Uncommon,
		CardRarity.Rare or CardRarity.Ancient => Tier.Rare,
		_ => Tier.Common,
	};
}
