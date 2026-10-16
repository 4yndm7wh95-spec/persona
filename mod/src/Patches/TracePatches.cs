using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace P3Makoto.Patches;

/// <summary>
/// v0.14.45 排查用的流程日志（只记一行，不改任何东西）：用户反馈 0.107.1 上击杀某些怪后整局卡住、日志里没有任何报错。
/// 记下"出牌结束 / 死亡（死亡动画时长）/ 战斗结束"三个节点，下次卡住时看最后一行就知道停在哪一步。
/// </summary>
internal static class Trace
{
	public static void Log(string text) => GD.Print($"{ModEntry.Tag} trace {text} (t={Time.GetTicksMsec()}ms, timescale={Engine.TimeScale:0.##})");
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCardPlayed))]
public static class TraceCardPlayedPatch
{
	public static void Prefix(CardPlay cardPlay)
	{
		ModEntry.Safe("trace", () => Trace.Log($"card done {cardPlay.Card?.Id}"));
	}
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterDeath))]
public static class TraceDeathPatch
{
	public static void Prefix(Creature creature, bool wasRemovalPrevented, float deathAnimLength)
	{
		ModEntry.Safe("trace", () => Trace.Log($"death {(creature.IsMonster ? creature.Monster?.Id.ToString() : "player")} anim {deathAnimLength:0.00}s prevented={wasRemovalPrevented}"));
	}
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCombatEnd))]
public static class TraceCombatEndPatch
{
	public static void Prefix() => ModEntry.Safe("trace", () => Trace.Log("combat end"));
}
