using System.Collections.Generic;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace P5Joker;

/// <summary>出牌时 Joker 播哪种施法动作（骨骼里的 cast 被换成对应版本，见 Aim.Rename）。</summary>
public enum Cast
{
	Summon,     // 手扶面具（原版 cast）
	Big,        // 撕面具（cast_big）
	Guard,      // 防御（cast_guard）
	Glove       // 扯手套（cast_glove）
}

/// <summary>每张牌的施法动作（人物动作已定稿，特效重做期间保留）。没列出的：能力牌扯手套，其他手扶面具。</summary>
public static class Casts
{
	private static readonly Dictionary<string, Cast> Table = new()
	{
		["Flechettes"] = Cast.Glove,
		["DefendSilent"] = Cast.Guard,
		["Deflect"] = Cast.Guard,
		["DodgeAndRoll"] = Cast.Guard,
		["Backflip"] = Cast.Guard,
		["EscapePlan"] = Cast.Guard,
		["Blur"] = Cast.Guard,
		["Survivor"] = Cast.Guard,
		["CloakAndDagger"] = Cast.Guard,
		["HandTrick"] = Cast.Glove,
		["Untouchable"] = Cast.Guard,
		["LegSweep"] = Cast.Guard,
		["Acrobatics"] = Cast.Glove,
		["Prepared"] = Cast.Glove,
		["Expertise"] = Cast.Glove,
		["CalculatedGamble"] = Cast.Glove,
		["Reflex"] = Cast.Glove,
		["WellLaidPlans"] = Cast.Glove,
		["BulletTime"] = Cast.Glove,
		["Shadowmeld"] = Cast.Glove,
		["ShadowStep"] = Cast.Glove,
		["Sidestep"] = Cast.Guard,
		["Tactician"] = Cast.Glove,
		["BladeDance"] = Cast.Glove,
		["BladeOfInk"] = Cast.Glove,
		["HiddenDaggers"] = Cast.Glove,
		["UpMySleeve"] = Cast.Glove,
		["StormOfSteel"] = Cast.Glove,
		["BladeSymphony"] = Cast.Glove,
		["KnifeTrap"] = Cast.Glove,
		["Outbreak"] = Cast.Big,
		["Concoct"] = Cast.Glove,
		["Expose"] = Cast.Glove,
		["PiercingWail"] = Cast.Big,
		["Flanking"] = Cast.Glove,
		["Tracking"] = Cast.Glove,
		["Anticipate"] = Cast.Guard,
		["Fade"] = Cast.Glove,
		["Speedster"] = Cast.Glove,
		["Afterimage"] = Cast.Glove,
		["Sneaky"] = Cast.Glove,
		["MasterPlanner"] = Cast.Glove,
		["ToolsOfTheTrade"] = Cast.Glove,
		["InfiniteBlades"] = Cast.Glove,
		["PhantomBlades"] = Cast.Glove,
		["FanOfKnives"] = Cast.Glove,
		["SerpentForm"] = Cast.Big,
		["WraithForm"] = Cast.Big,
	};

	public static Cast For(CardModel card) =>
		Table.TryGetValue(card.GetType().Name, out Cast c) ? c : card.Type == CardType.Power ? Cast.Glove : Cast.Summon;
}
