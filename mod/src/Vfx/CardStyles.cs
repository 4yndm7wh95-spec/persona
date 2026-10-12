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
	// v0.9：每张牌的"命中元素"（每一击伤害时）。出牌时的专属演出见 CardSigs（多数牌各不相同）。
	// Opener 只有召唤 Persona 的大招（Captions）才会真正用到召唤器；SkewBand 色带只留给这 9 张大招。
	private static readonly Dictionary<string, CardStyle> Table = new()
	{
		// ── 攻击：剑 ──
		["StrikeRegent"] = new(Element.Slash, Opener.None),
		["WroughtInWar"] = new(Element.Slash, Opener.None, Extra.None, 1.1f),
		["CelestialMight"] = new(Element.Slash, Opener.None, Extra.None, 0.75f),
		["SovereignBlade"] = new(Element.Royal, Opener.Evoker, Extra.SkewBand),
		// ── 攻击：钝击 ──
		["CollisionCourse"] = new(Element.Strike, Opener.None),
		["KinglyPunch"] = new(Element.Strike, Opener.None, Extra.None, 1.1f),
		["KinglyKick"] = new(Element.Strike, Opener.None, Extra.None, 1.3f),
		["KnockoutBlow"] = new(Element.Strike, Opener.None, Extra.None, 1.3f),
		["HeirloomHammer"] = new(Element.Strike, Opener.None, Extra.None, 1.35f),
		["BeatIntoShape"] = new(Element.Strike, Opener.None, Extra.None, 1.1f),
		["CrushUnder"] = new(Element.Strike, Opener.None, Extra.None, 0.9f),
		// ── 攻击：贯穿 ──
		["CrescentSpear"] = new(Element.Pierce, Opener.None, Extra.None, 1.1f),
		["PhotonCut"] = new(Element.Pierce, Opener.None),
		["HeavenlyDrill"] = new(Element.Pierce, Opener.None, Extra.None, 0.7f),
		// ── 攻击：魔法 ──
		["SolarStrike"] = new(Element.Agi, Opener.None),
		["ShiningStrike"] = new(Element.Hama, Opener.None, Extra.None, 0.8f),
		["Hegemony"] = new(Element.Hama, Opener.None, Extra.None, 1.2f),
		["Radiate"] = new(Element.Hama, Opener.None, Extra.None, 0.6f),
		["LunarBlast"] = new(Element.Bufu, Opener.None, Extra.None, 0.6f),
		["GammaBlast"] = new(Element.Zio, Opener.None, Extra.None, 1.1f),
		["Supermassive"] = new(Element.Mudo, Opener.None, Extra.BlackHoleAtTarget),
		["DyingStar"] = new(Element.Mudo, Opener.Evoker, Extra.SkewBand | Extra.DarkHour, 0.9f),
		["Devastate"] = new(Element.Megido, Opener.None, Extra.None, 1.2f),
		["MakeItSo"] = new(Element.Megido, Opener.None),
		["Bombardment"] = new(Element.Meteor, Opener.None, Extra.None, 1.2f),
		["Comet"] = new(Element.None, Opener.None),
		["FallingStar"] = new(Element.Meteor, Opener.None),
		["MeteorShower"] = new(Element.Meteor, Opener.Evoker, Extra.SkewBand | Extra.DarkHour, 0.9f),
		["CrashLanding"] = new(Element.Meteor, Opener.None, Extra.None, 1.3f),
		["SevenStars"] = new(Element.Meteor, Opener.Evoker, Extra.SkewBand, 0.7f),
		["AstralPulse"] = new(Element.Star, Opener.None, Extra.None, 0.8f),
		["Stardust"] = new(Element.Star, Opener.None, Extra.None, 0.6f),
		["GuidingStar"] = new(Element.Star, Opener.None),

		// ── 技能 / 能力：命中元素为空，演出全在 CardSigs；这里只标出色带大招与影时间 ──
		["BigBang"] = new(Element.None, Opener.Evoker, Extra.SkewBand),
		["VoidForm"] = new(Element.None, Opener.Power, Extra.SkewBand | Extra.DarkHour),
		["Genesis"] = new(Element.None, Opener.Power, Extra.SkewBand),
		["TheSealedThrone"] = new(Element.None, Opener.Power, Extra.SkewBand | Extra.DarkHour),
		["SwordSage"] = new(Element.None, Opener.Power, Extra.SkewBand),
		["BlackHole"] = new(Element.None, Opener.Power, Extra.BlackHoleCenter),
	};

	/// <summary>色带文字：P3 技能名 + 召唤的 Persona。只有带 SkewBand 的牌才会显示。</summary>
	private static readonly Dictionary<string, (string Skill, string Persona)> Captions = new()
	{
		["SovereignBlade"] = ("BRAVE BLADE", "THANATOS"),
		["SwordSage"] = ("BRAVE BLADE", "THANATOS"),
		["DyingStar"] = ("DIE FOR ME!", "THANATOS"),
		["SevenStars"] = ("MEGIDOLAON", "THANATOS"),
		["MeteorShower"] = ("MORNING STAR", "MESSIAH"),
		["BigBang"] = ("MEGIDOLAON", "MESSIAH"),
		["VoidForm"] = ("DARK HOUR", "THANATOS"),
		["Genesis"] = ("SALVATION", "MESSIAH"),
		["TheSealedThrone"] = ("GREAT SEAL", "MESSIAH")
	};

	public static (string Skill, string Persona) CaptionFor(object card) =>
		Captions.TryGetValue(card.GetType().Name, out var c) ? c : ("PERSONA", "ORPHEUS");

	/// <summary>
	/// 出牌时人物做什么动作（与游戏源码一致：OnPlay 里是否 TriggerAnim）。
	/// Evoker：召唤器抵太阳穴开枪 —— 只留给真正召唤 Persona 的大招（有色带字幕的牌），太频繁就不稀罕了；
	/// Gesture：游戏会播施法动作、但不召唤的牌 —— 只做一个轻的身体动作（cast_light），不拿召唤器。
	/// </summary>
	public enum CardAnim { Sword, Evoker, Gesture, None }

	/// <summary>这张牌是否召唤 Persona（用召唤器）：以色带字幕表为准。</summary>
	public static bool Summons(object card) => Captions.ContainsKey(card.GetType().Name);

	// 源码扫描结果：会 TriggerAnim("Cast") 的技能牌、TriggerAnim("PowerUp") 的能力牌、用施法动作代替挥剑的攻击牌
	private static readonly HashSet<string> EvokerCards = new()
	{
		"Alignment", "BigBang", "Bulwark", "Charge", "Conqueror", "Constellation", "Convergence", "DecisionsDecisions",
		"ForegoneConclusion", "Glitterstream", "Glow", "Guards", "HiddenCache", "KnowThyPlace", "Largesse", "Monologue",
		"Patter", "Prophesize", "RefineBlade", "Reflect", "Resonance", "RoyalGamble", "SummonForth", "TheSmith", "Venerate",
		"Arsenal", "BlackHole", "ChildOfTheStars", "Furnace", "Genesis", "HammerTime", "MonarchsGaze", "Orbit", "PaleBlueDot",
		"Parry", "Royalties", "SeekingEdge", "SpectrumShift", "SwordSage", "TheSealedThrone", "Tyranny", "VoidForm",
		"Comet", "CrushUnder", "GuidingStar", "Stardust", "LunarBlast"
	};

	public static CardAnim AnimFor(object card, MegaCrit.Sts2.Core.Entities.Cards.CardType type)
	{
		// 君王之刃：召唤器开枪 → Thanatos 挥剑，所以选中时举召唤器（而不是持剑）
		if (card.GetType().Name == "SovereignBlade")
		{
			return CardAnim.Evoker;
		}
		if (EvokerCards.Contains(card.GetType().Name))
		{
			return Summons(card) ? CardAnim.Evoker : CardAnim.Gesture;
		}
		// 召唤 Persona 的攻击大招（星灭 / 七星 / 流星雨）：不挥剑，改为召唤器开枪（Pose.MarkEvokerAttack）
		if (type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack && Summons(card))
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
			MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack => new CardStyle(Element.Slash, Opener.None),
			MegaCrit.Sts2.Core.Entities.Cards.CardType.Power => new CardStyle(Element.None, Opener.Power),
			_ => new CardStyle(Element.None, Opener.None)
		};
	}
}
