using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using P3Makoto.Core;
using P3Makoto.Vfx;

namespace P3Makoto.Patches;

/// <summary>
/// 与游戏事件同步的特效钩子：每一击伤害、获得格挡、获得辉星、锻造君王之刃。
/// 只对结城理（储君）打出的牌生效。
/// </summary>
internal static class Sig
{
	public static bool Params(MethodInfo m, params string[] names) =>
		m.GetParameters().Select(p => p.Name).SequenceEqual(names);
}

/// <summary>结城理的攻击牌使用自定义命中特效时，关掉原版命中特效，避免两套叠在一起。</summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
public static class AttackHitVfxPatch
{
	private static readonly PropertyInfo HitVfx = AccessTools.Property(typeof(AttackCommand), "HitVfx");
	private static readonly PropertyInfo HitSfx = AccessTools.Property(typeof(AttackCommand), "HitSfx");
	private static readonly PropertyInfo TmpHitSfx = AccessTools.Property(typeof(AttackCommand), "TmpHitSfx");

	public static void Prefix(AttackCommand __instance)
	{
		ModEntry.Safe("AttackHitVfx", () =>
		{
			if (__instance.ModelSource is CardModel card && CardFx.ReplacesHitVfx(card))
			{
				HitVfx.SetValue(__instance, null);
				// 命中音效换成 P3R 原声时（Elements.Hit 里播），关掉原版命中音效（原版对 null 有判断）
				if (Audio.Sfx.HasElementSound(CardStyles.For(card, card.Type).Hit))
				{
					HitSfx.SetValue(__instance, null);
					TmpHitSfx.SetValue(__instance, null);
				}
			}
		});
	}
}

/// <summary>每一击伤害（所有 Damage 重载最终都汇到这个签名）。</summary>
[HarmonyPatch]
public static class DamagePatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "Damage" && Sig.Params(m, "choiceContext", "targets", "amount", "props", "dealer", "cardSource", "cardPlay"));

	public static void Prefix(IEnumerable<Creature>? targets, Creature? dealer, CardModel? cardSource)
	{
		ModEntry.Safe("Damage", () =>
		{
			if (targets == null)
			{
				return;
			}
			if (CardFx.IsMakotoCard(cardSource))
			{
				Popups.MarkHits(targets);
				CardFx.OnHit(cardSource!, targets);
			}
			else if (dealer?.Player is Player p && Makoto.Is(p))
			{
				Popups.MarkHits(targets);                 // 不是出牌造成的伤害（黑洞等能力）：伤害数字也换成 P3R 样式
			}
		});
	}
}

/// <summary>获得格挡（所有 GainBlock 重载最终都汇到这个签名）。</summary>
[HarmonyPatch]
public static class GainBlockPatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "GainBlock" && Sig.Params(m, "creature", "amount", "props", "cardPlay", "fast"));

	public static void Prefix(Creature creature, decimal amount, CardPlay? cardPlay)
	{
		ModEntry.Safe("GainBlock", () =>
		{
			if (amount <= 0m || cardPlay == null || !CardFx.IsMakotoCard(cardPlay.Card))
			{
				return;
			}
			NCreature? node = NCombatRoom.Instance?.GetCreatureNode(creature);
			if (node != null)
			{
				CardFx.OnBlock(cardPlay.Card, node);
			}
		});
	}
}

[HarmonyPatch(typeof(ForgeCmd), nameof(ForgeCmd.Forge))]
public static class ForgePatch
{
	public static void Prefix(decimal amount, Player player)
	{
		ModEntry.Safe("Forge", () =>
		{
			if (amount > 0m && Makoto.Is(player) && Makoto.Node(player) is NCreature node)
			{
				Extras.PersonaCharge(node, (int)amount, amount >= 10m);      // v0.14.0：锻造 = Persona 蓄力增益（不再敲剑 / 拼光剑）
			}
		});
	}
}

/// <summary>
/// 结城理的出招等待：君王之刃推迟到 0.62s（Thanatos 显形 → 冲刺 → 劈下，伤害数字与斩击同帧）；
/// 召唤 Persona 的攻击大招推迟到 0.32s（先开枪再结算）。只影响表现等待，不改任何数值。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim))]
public static class SovereignTimingPatch
{
	private static bool _inner;

	/// <summary>调用原版 TriggerAnim（跳过本补丁，避免递归）。</summary>
	private static System.Threading.Tasks.Task Original(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature, string triggerName, float waitTime)
	{
		_inner = true;
		try
		{
			return CreatureCmd.TriggerAnim(creature, triggerName, waitTime);
		}
		finally
		{
			_inner = false;
		}
	}

	private static async System.Threading.Tasks.Task Then(System.Threading.Tasks.Task first, System.Threading.Tasks.Task gate)
	{
		await first;
		await gate;
	}

	public static bool Prefix(MegaCrit.Sts2.Core.Entities.Creatures.Creature creature, string triggerName, ref float waitTime, ref System.Threading.Tasks.Task __result)
	{
		if (_inner || !Makoto.Is(creature.Player))
		{
			return true;
		}
		// 召唤器 / 施法出招的攻击牌：第二次起的 Attack 触发不播动作（不接斩击），只保留等待
		if (triggerName == "Attack" && Makoto.Node(creature.Player) is NCreature caster && !creature.IsDead && Pose.SkipRepeatAttack(caster))
		{
			__result = Cmd.CustomScaledWait(System.Math.Min(waitTime * 0.5f, 0.25f), waitTime);
			return false;
		}
		if (triggerName == "Cast" && Makoto.Node(creature.Player) is NCreature holder && Pose.TakeCastHold(holder) is float hold and > 0f)
		{
			waitTime = System.Math.Max(waitTime, hold);
		}
		if (triggerName == "sovereignBladeTrigger")
		{
			bool vorpal = creature.HasPower<MegaCrit.Sts2.Core.Models.Powers.SeekingEdgePower>();
			waitTime = vorpal ? 0.05f : Vfx.PersonaFx.SovereignWait;
			if (vorpal)
			{
				// 空间杀法：伤害等到斩击结束（Vorpal 释放）才结算 —— 先放动作，再等闸门（最多 3.5s 真实时间兜底）
				System.Threading.Tasks.Task anim = Original(creature, triggerName, waitTime);
				__result = Then(anim, Vfx.Vorpal.Gate(3.5f));
				return false;
			}
		}
		// 召唤牌改播"召唤开枪"（~0.42s 扣扳机 → 横幅碎裂、Persona 现身）：伤害 / 效果等到开枪、Persona 现身之后
		else if (triggerName is "Attack" or "Cast" && Makoto.Node(creature.Player) is NCreature node && Pose.HasSummon(node))
		{
			waitTime = System.Math.Max(waitTime, triggerName == "Attack" ? 0.6f : 0.5f);
		}
		return true;
	}
}

/// <summary>伤害数字：结城理打中的敌人，原版数字换成 P3R 样式（Popups.Damage）。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Vfx.NDamageNumVfx), nameof(MegaCrit.Sts2.Core.Nodes.Vfx.NDamageNumVfx.Create),
	typeof(Creature), typeof(MegaCrit.Sts2.Core.Entities.Creatures.DamageResult))]
public static class DamageNumberPatch
{
	public static void Postfix(Creature target, MegaCrit.Sts2.Core.Entities.Creatures.DamageResult result,
		ref MegaCrit.Sts2.Core.Nodes.Vfx.NDamageNumVfx? __result)
	{
		if (__result == null || target.IsPlayer || !Popups.IsMakotoHit(target))
		{
			return;
		}
		NCreature? node = NCombatRoom.Instance?.GetCreatureNode(target);
		Godot.Control? layer = Makoto.FxLayer();
		if (node == null || layer == null)
		{
			return;
		}
		int amount = result.UnblockedDamage + (target.Monster is MegaCrit.Sts2.Core.Models.Monsters.Osty ? 0 : result.OverkillDamage);
		__result.QueueFree();
		__result = null;                                     // 调用方拿到 null 就不显示原版数字
		ModEntry.Safe("P3 damage number", () => Popups.Damage(layer, node.GetInstanceId(), node.VfxSpawnPosition, amount));
		if (result.UnblockedDamage >= 20 && result.UnblockedDamage >= target.MaxHp * 0.3m)
		{
			ModEntry.Safe("P3 critical", () => Popups.Critical(layer, node.GetInstanceId(), node.VfxSpawnPosition));
		}
	}
}

/// <summary>创世之柱：每次生成卡牌（给格挡的那一刻），身后的天鹅绒门开一次。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.PillarOfCreationPower), nameof(MegaCrit.Sts2.Core.Models.Powers.PillarOfCreationPower.AfterCardGeneratedForCombat))]
public static class PillarGatePatch
{
	public static void Prefix(MegaCrit.Sts2.Core.Models.Powers.PillarOfCreationPower __instance, Player? creator)
	{
		ModEntry.Safe("PillarGate", () =>
		{
			if (creator != null && creator.Creature == __instance.Owner && Makoto.Is(creator))
			{
				ThemeFx.GatePulse();
			}
		});
	}
}

/// <summary>创世纪：每回合获得辉星时，头顶的星系小转一圈。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.GenesisPower), nameof(MegaCrit.Sts2.Core.Models.Powers.GenesisPower.AfterEnergyReset))]
public static class GenesisTurnPatch
{
	public static void Prefix(MegaCrit.Sts2.Core.Models.Powers.GenesisPower __instance, Player player)
	{
		ModEntry.Safe("GenesisTurn", () =>
		{
			if (player == __instance.Owner.Player && Makoto.Is(player) && Makoto.Node(player) is NCreature node)
			{
				Rares.Genesis(node, 0.6f, 1.2f);
			}
		});
	}
}

/// <summary>暴政：回合开始的消耗选择 → 接下来这次消耗演出棺材。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.TyrannyPower), nameof(MegaCrit.Sts2.Core.Models.Powers.TyrannyPower.AfterPlayerTurnStart))]
public static class TyrannyTurnPatch
{
	public static ulong ArmedUntil;

	public static void Prefix(MegaCrit.Sts2.Core.Models.Powers.TyrannyPower __instance, Player player)
	{
		if (player == __instance.Owner.Player && Makoto.Is(player))
		{
			ArmedUntil = Godot.Time.GetTicksMsec() + 60000;
		}
	}
}

[HarmonyPatch(typeof(CardCmd), nameof(CardCmd.Exhaust))]
public static class TyrannyExhaustPatch
{
	public static void Prefix(CardModel card)
	{
		ModEntry.Safe("TyrannyExhaust", () =>
		{
			if (Godot.Time.GetTicksMsec() <= TyrannyTurnPatch.ArmedUntil && Makoto.Is(card.Owner) && Makoto.Node(card.Owner) is NCreature node)
			{
				TyrannyTurnPatch.ArmedUntil = 0;
				Rares.TyrannyExhaust(node);
			}
		});
	}
}

/// <summary>
/// 抽牌音效（v0.13.6）：结城理每抽到一张牌（打出的牌抽牌、每回合开始的自动抽牌都算）就播一次 P3R 翻牌声。
/// Hook.AfterCardDrawn 每张牌调用一次；连续抽牌由 Sfx.CardDrawn 排队错开，保证抽几张响几次。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCardDrawn))]
public static class CardDrawnPatch
{
	public static void Postfix(CardModel card)
	{
		ModEntry.Safe("CardDrawn", () =>
		{
			if (CardFx.IsMakotoCard(card))
			{
				Audio.Sfx.CardDrawn();
			}
		});
	}
}

/// <summary>
/// v0.13.9：关掉原版储君的全部角色音效（攻击 / 施法 / 能力 / 君王之刃 / 锻造 / 精炼 / 引导之星 / 选人），换成本 mod 的 P3R 原声。
/// 储君的音效都走 NAudioManager.PlayOneShot("event:/sfx/characters/regent/…")；只保留死亡音效。
/// </summary>
[HarmonyPatch]
public static class RegentSfxMutePatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(MegaCrit.Sts2.Core.Nodes.Audio.NAudioManager).GetMethods(BindingFlags.Public | BindingFlags.Instance)
			.Where(m => m.Name == "PlayOneShot" && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(string));

	public static bool Prefix(object[] __args)
	{
		if (__args.Length == 0 || __args[0] is not string path || !path.StartsWith("event:/sfx/characters/regent/", System.StringComparison.Ordinal))
		{
			return true;
		}
		bool play = true;
		ModEntry.Safe("RegentSfx", () => play = Audio.Sfx.ReplaceRegent(path.Substring("event:/sfx/characters/regent/".Length)));
		return play;
	}
}

/// <summary>
/// v0.14.0：结城理的格挡完全挡住一次攻击时（原版 CreatureCmd.Damage 里 WasFullyBlocked → NDamageBlockedVfx.Create），
/// 把原版的"格挡"字样换成 P3R 的「BLOCK」（Popups.Block）。原版的格挡火花和音效保留。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Vfx.NDamageBlockedVfx), nameof(MegaCrit.Sts2.Core.Nodes.Vfx.NDamageBlockedVfx.Create))]
public static class BlockPopupPatch
{
	public static bool Prefix(Creature target, ref MegaCrit.Sts2.Core.Nodes.Vfx.NDamageBlockedVfx? __result)
	{
		bool replaced = false;
		ModEntry.Safe("BlockPopup", () =>
		{
			if (target.IsPlayer && Makoto.Is(target.Player) && NCombatRoom.Instance?.GetCreatureNode(target) is NCreature node && Makoto.FxLayer() is Godot.Control front)
			{
				Popups.Block(front, Makoto.Center(node));
				replaced = true;
			}
		});
		if (replaced)
		{
			__result = null;
			return false;
		}
		return true;
	}
}

/// <summary>结城理格挡时原版的蓝色格挡火花也不要（换成 Popups.Block 的 P3R 冲击）。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Vfx.NBlockSparkVfx), nameof(MegaCrit.Sts2.Core.Nodes.Vfx.NBlockSparkVfx.Create))]
public static class BlockSparkPatch
{
	public static bool Prefix(Creature target, ref MegaCrit.Sts2.Core.Nodes.Vfx.NBlockSparkVfx? __result)
	{
		if (target.IsPlayer && Makoto.Is(target.Player))
		{
			__result = null;
			return false;
		}
		return true;
	}
}

/// <summary>封印王座：结城理每打出一张牌（获得辉星）→ 印记脉冲 + 光流 + 星从印记凝出（ThroneSeal.Pulse）。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.TheSealedThronePower), nameof(MegaCrit.Sts2.Core.Models.Powers.TheSealedThronePower.BeforeCardPlayed))]
public static class SealedThronePulsePatch
{
	public static void Prefix(MegaCrit.Sts2.Core.Models.Powers.TheSealedThronePower __instance, MegaCrit.Sts2.Core.Entities.Cards.CardPlay cardPlay)
	{
		ModEntry.Safe("SealedThronePulse", () =>
		{
			Player? owner = __instance.Owner?.Player;
			if (owner != null && cardPlay.Card.Owner == owner && Makoto.Is(owner) && Makoto.Node(owner) is NCreature node)
			{
				ThroneSeal.Pulse(node);
			}
		});
	}
}
