using System;
using System.Collections.Generic;

namespace P3Makoto.Vfx;

/// <summary>出牌开场方式。</summary>
public enum Opener
{
	/// <summary>召唤器发动（标准）。</summary>
	Evoker,
	/// <summary>小号召唤器（0 费 / 小技能）。</summary>
	EvokerSmall,
	/// <summary>能力牌：召唤器 + 影时间光柱 + 塔罗牌。</summary>
	Power,
	/// <summary>不召唤（结城理亲自挥剑的普通攻击）。</summary>
	None
}

/// <summary>额外演出，在开场之后叠加。</summary>
[Flags]
public enum Extra
{
	None = 0,
	SkewBand = 1,          // P3R 色带切入
	DebuffTargets = 2,     // 目标身上下压箭环
	BuffSelf = 4,          // 自身上升箭环
	Tarot = 8,             // 塔罗牌（技能牌使用时为冰蓝色）
	Butterflies = 16,      // 额外一群蓝蝶
	DarkHour = 32,         // 影时间降临
	BlackHoleAtTarget = 64,
	BlackHoleCenter = 128,
	HamaSelf = 256,        // 自身六芒星光柱
	MegidoAbove = 512,     // 头顶上空万能爆发
	ShieldSelf = 1024,     // 能力牌附带的护盾演出
	ForgeSelf = 2048,      // 能力牌附带的锻刃演出
	StarsSelf = 4096       // 能力牌附带的星光演出
}

public sealed record CardStyle(Element Hit, Opener Opener, Extra Extra = Extra.None, float HitScale = 1f);

/// <summary>
/// 卡牌 → 特效映射。键为卡牌类名（MegaCrit.Sts2.Core.Models.Cards 下）。
/// 未列出的卡牌按类型给默认风格。格挡、辉星、锻造由各自的钩子自动叠加演出，不需要在这里写。
/// </summary>
public static class CardStyles
{
	private static readonly Dictionary<string, CardStyle> Table = new()
	{
		// ── 攻击：物理 ──
		["StrikeRegent"] = new(Element.Slash, Opener.None),
		["WroughtInWar"] = new(Element.Slash, Opener.None),
		["SovereignBlade"] = new(Element.Royal, Opener.None, Extra.SkewBand),
		["CollisionCourse"] = new(Element.Strike, Opener.EvokerSmall),
		["KinglyPunch"] = new(Element.Strike, Opener.Evoker),
		["KinglyKick"] = new(Element.Strike, Opener.Evoker, Extra.SkewBand, 1.3f),
		["KnockoutBlow"] = new(Element.Strike, Opener.Evoker, Extra.None, 1.25f),
		["HeirloomHammer"] = new(Element.Strike, Opener.Evoker, Extra.None, 1.3f),
		["BeatIntoShape"] = new(Element.Strike, Opener.Evoker),
		["CrushUnder"] = new(Element.Strike, Opener.Evoker),
		["CrescentSpear"] = new(Element.Pierce, Opener.EvokerSmall),
		["PhotonCut"] = new(Element.Pierce, Opener.EvokerSmall),
		["HeavenlyDrill"] = new(Element.Pierce, Opener.Evoker, Extra.None, 0.7f),

		// ── 攻击：魔法 ──
		["SolarStrike"] = new(Element.Agi, Opener.Evoker),
		["ShiningStrike"] = new(Element.Hama, Opener.Evoker, Extra.None, 0.8f),
		["Hegemony"] = new(Element.Hama, Opener.Evoker, Extra.SkewBand, 1.2f),
		["Radiate"] = new(Element.Hama, Opener.Evoker, Extra.None, 0.6f),
		["LunarBlast"] = new(Element.Bufu, Opener.EvokerSmall, Extra.None, 0.7f),
		["GammaBlast"] = new(Element.Zio, Opener.EvokerSmall),
		["Supermassive"] = new(Element.Mudo, Opener.Evoker, Extra.BlackHoleAtTarget),
		["DyingStar"] = new(Element.Mudo, Opener.Evoker, Extra.SkewBand),
		["Devastate"] = new(Element.Megido, Opener.Evoker, Extra.SkewBand),
		["MakeItSo"] = new(Element.Megido, Opener.Evoker),
		["Bombardment"] = new(Element.Megido, Opener.Evoker, Extra.SkewBand),
		["Comet"] = new(Element.Meteor, Opener.Evoker, Extra.None, 1.3f),
		["FallingStar"] = new(Element.Meteor, Opener.EvokerSmall, Extra.None, 0.8f),
		["MeteorShower"] = new(Element.Meteor, Opener.Evoker, Extra.SkewBand | Extra.DarkHour),
		["CrashLanding"] = new(Element.Meteor, Opener.Evoker, Extra.None, 1.2f),
		["SevenStars"] = new(Element.Meteor, Opener.Evoker, Extra.SkewBand, 0.75f),
		["CelestialMight"] = new(Element.Star, Opener.Evoker, Extra.None, 0.8f),
		["AstralPulse"] = new(Element.Star, Opener.EvokerSmall, Extra.None, 0.7f),
		["Stardust"] = new(Element.Star, Opener.EvokerSmall, Extra.None, 0.55f),
		["GuidingStar"] = new(Element.Star, Opener.Evoker),

		// ── 技能 ──
		["KnowThyPlace"] = new(Element.None, Opener.EvokerSmall, Extra.DebuffTargets),
		["Resonance"] = new(Element.None, Opener.Evoker, Extra.DebuffTargets),
		["Conqueror"] = new(Element.None, Opener.Evoker, Extra.DebuffTargets),
		["Patter"] = new(Element.None, Opener.EvokerSmall, Extra.BuffSelf),
		["Terraforming"] = new(Element.None, Opener.Evoker, Extra.BuffSelf),
		["Charge"] = new(Element.None, Opener.Evoker, Extra.BuffSelf),
		["Alignment"] = new(Element.None, Opener.EvokerSmall, Extra.BuffSelf),
		["Convergence"] = new(Element.None, Opener.Evoker, Extra.BuffSelf | Extra.Butterflies),
		["Monologue"] = new(Element.None, Opener.EvokerSmall, Extra.Tarot),
		["ForegoneConclusion"] = new(Element.None, Opener.Evoker, Extra.Tarot),
		["Prophesize"] = new(Element.None, Opener.Evoker, Extra.Tarot),
		["DecisionsDecisions"] = new(Element.None, Opener.EvokerSmall, Extra.Tarot),
		["Glimmer"] = new(Element.None, Opener.EvokerSmall, Extra.Butterflies),
		["Glow"] = new(Element.None, Opener.EvokerSmall, Extra.Butterflies),
		["BigBang"] = new(Element.None, Opener.Evoker, Extra.SkewBand | Extra.MegidoAbove),
		["IAmInvincible"] = new(Element.None, Opener.Evoker, Extra.SkewBand),
		["Largesse"] = new(Element.None, Opener.EvokerSmall, Extra.BuffSelf),
		["Tutor"] = new(Element.None, Opener.EvokerSmall, Extra.Tarot),
		["Plot"] = new(Element.None, Opener.EvokerSmall, Extra.Tarot),

		// ── 能力 ──
		["VoidForm"] = new(Element.None, Opener.Power, Extra.DarkHour | Extra.SkewBand),
		["BlackHole"] = new(Element.None, Opener.Power, Extra.BlackHoleCenter),
		["Genesis"] = new(Element.None, Opener.Power, Extra.HamaSelf | Extra.SkewBand),
		["TheSealedThrone"] = new(Element.None, Opener.Power, Extra.HamaSelf | Extra.SkewBand | Extra.DarkHour),
		["Tyranny"] = new(Element.None, Opener.Power, Extra.DebuffTargets),
		["MonarchsGaze"] = new(Element.None, Opener.Power, Extra.DebuffTargets),
		["Parry"] = new(Element.None, Opener.Power, Extra.ShieldSelf),
		["NeutronAegis"] = new(Element.None, Opener.Power, Extra.ShieldSelf),
		["Arsenal"] = new(Element.None, Opener.Power, Extra.ForgeSelf),
		["SwordSage"] = new(Element.None, Opener.Power, Extra.ForgeSelf | Extra.SkewBand),
		["HammerTime"] = new(Element.None, Opener.Power, Extra.ForgeSelf),
		["SeekingEdge"] = new(Element.None, Opener.Power, Extra.ForgeSelf),
		["Orbit"] = new(Element.None, Opener.Power, Extra.StarsSelf),
		["PaleBlueDot"] = new(Element.None, Opener.Power, Extra.StarsSelf),
		["ChildOfTheStars"] = new(Element.None, Opener.Power, Extra.StarsSelf),
		["SpectrumShift"] = new(Element.None, Opener.Power, Extra.StarsSelf | Extra.Butterflies)
	};

	/// <summary>色带文字：P3 技能名 + 召唤的 Persona。只有带 SkewBand 的牌才会显示。</summary>
	private static readonly Dictionary<string, (string Skill, string Persona)> Captions = new()
	{
		["SovereignBlade"] = ("BRAVE BLADE", "THANATOS"),
		["SwordSage"] = ("BRAVE BLADE", "THANATOS"),
		["KinglyKick"] = ("GOD'S HAND", "ORPHEUS"),
		["Hegemony"] = ("HAMAON", "ORPHEUS"),
		["DyingStar"] = ("DIE FOR ME!", "THANATOS"),
		["Devastate"] = ("MEGIDO", "THANATOS"),
		["Bombardment"] = ("MEGIDOLA", "THANATOS"),
		["SevenStars"] = ("MEGIDOLAON", "THANATOS"),
		["MeteorShower"] = ("MORNING STAR", "MESSIAH"),
		["BigBang"] = ("MEGIDOLAON", "MESSIAH"),
		["IAmInvincible"] = ("TETRAKARN", "ORPHEUS"),
		["VoidForm"] = ("DARK HOUR", "THANATOS"),
		["Genesis"] = ("SALVATION", "MESSIAH"),
		["TheSealedThrone"] = ("GREAT SEAL", "MESSIAH")
	};

	public static (string Skill, string Persona) CaptionFor(object card) =>
		Captions.TryGetValue(card.GetType().Name, out var c) ? c : ("PERSONA", "ORPHEUS");

	/// <summary>出牌时人物做什么动作（与游戏源码一致：OnPlay 里是否 TriggerAnim）。</summary>
	public enum CardAnim { Sword, Evoker, None }

	// 源码扫描结果：会 TriggerAnim("Cast") 的技能牌、TriggerAnim("PowerUp") 的能力牌、用施法动作代替挥剑的攻击牌
	private static readonly HashSet<string> EvokerCards = new()
	{
		"Alignment", "BigBang", "Bulwark", "Charge", "Conqueror", "Constellation", "Convergence", "DecisionsDecisions",
		"ForegoneConclusion", "Glitterstream", "Glow", "Guards", "HiddenCache", "KnowThyPlace", "Largesse", "Monologue",
		"Patter", "Prophesize", "RefineBlade", "Reflect", "Resonance", "RoyalGamble", "SummonForth", "TheSmith", "Venerate",
		"Arsenal", "BlackHole", "ChildOfTheStars", "Furnace", "Genesis", "HammerTime", "MonarchsGaze", "Orbit", "PaleBlueDot",
		"Parry", "Royalties", "SeekingEdge", "SpectrumShift", "SwordSage", "TheSealedThrone", "Tyranny", "VoidForm",
		"Comet", "CrushUnder", "GuidingStar"
	};

	public static CardAnim AnimFor(object card, MegaCrit.Sts2.Core.Entities.Cards.CardType type)
	{
		if (EvokerCards.Contains(card.GetType().Name))
		{
			return CardAnim.Evoker;
		}
		return type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack ? CardAnim.Sword : CardAnim.None;
	}

	public static CardStyle For(object card, MegaCrit.Sts2.Core.Entities.Cards.CardType type)
	{
		if (Table.TryGetValue(card.GetType().Name, out CardStyle? style))
		{
			return style;
		}
		return type switch
		{
			MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack => new CardStyle(Element.Slash, Opener.Evoker),
			MegaCrit.Sts2.Core.Entities.Cards.CardType.Power => new CardStyle(Element.None, Opener.Power),
			_ => new CardStyle(Element.None, Opener.Evoker)
		};
	}
}
