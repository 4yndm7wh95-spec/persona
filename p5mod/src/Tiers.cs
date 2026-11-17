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
	/// <summary>稀有但演出要朴素的牌：按罕见处理（亚森直接现身出手，不出红色舞台 / 眼神特写）。用户 2026-11-03：腐蚀波不需要眼神特写和背景变化。</summary>
	private static readonly System.Collections.Generic.HashSet<string> PlainRare = new() { "CorrosiveWave" };

	public static Tier For(CardModel card) => PlainRare.Contains(card.GetType().Name) ? Tier.Uncommon : card.Rarity switch
	{
		CardRarity.Basic => Tier.Basic,
		CardRarity.Uncommon => Tier.Uncommon,
		CardRarity.Rare or CardRarity.Ancient => Tier.Rare,
		_ => Tier.Common,
	};
}
