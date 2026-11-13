using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace P5Joker;

/// <summary>Card-local rendezvous: applying poison completes before the hand is allowed to crush.</summary>
public sealed class OutbreakPlayback
{
	// Key by the individual play, not the card model: repeats/canceled plays must never consume another clip.
	private static readonly ConditionalWeakTable<CardPlay, OutbreakPlayback> Pending = new();
	public static readonly AsyncLocal<OutbreakPlayback?> Current = new();
	public readonly CutInPlayback Clip;
	private readonly TaskCompletionSource _crushed = new();
	private bool _visualStarted;

	private OutbreakPlayback(float splitAt) { Clip = new CutInPlayback(splitAt, crush: true) { Ready = false }; }
	public static OutbreakPlayback? Prepare(CardPlay cardPlay)
	{
		if (cardPlay.Card is not Outbreak || Joker.FxLayer(back: true) == null) return null;
		return Pending.GetValue(cardPlay, p => new OutbreakPlayback(Aim.HitFor(p.Card) - 0.04f));
	}
	public static OutbreakPlayback? Take(CardPlay cardPlay)
	{
		if (!Pending.TryGetValue(cardPlay, out var play)) return null;
		Pending.Remove(cardPlay);
		return play;
	}
	public void Started(Node owner)
	{
		_visualStarted = true;
		Clip.Start();
		owner.TreeExiting += () => _crushed.TrySetCanceled();
	}
	public void Crush() => _crushed.TrySetResult();

	public async Task Run(Outbreak card, PlayerChoiceContext context)
	{
		var previous = Current.Value;
		Current.Value = this;
		try
		{
			var combat = card.CombatState ?? throw new InvalidOperationException("Outbreak has no combat state");
			// Do not wait for the old full cast delay and THEN apply poison enemy by enemy.
			await CreatureCmd.TriggerAnim(card.Owner.Creature, "Cast", 0f);
			foreach (Creature target in combat.HittableEnemies.ToArray())
				await PowerCmd.Apply<PoisonPower>(context, target, card.DynamicVars.Poison.BaseValue, card.Owner.Creature, card);
			Clip.Ready = true;
			if (Joker.FxLayer(back: true) is Control back)
			{
				// A failed visual setup must not leave a card waiting forever.
				Fx.After(back, 0.1, () => { if (!_visualStarted) _crushed.TrySetResult(); });
				await _crushed.Task;
			}
			// Start all targets before awaiting any target. Keep each vanilla Trigger, including
			// Accelerant repeats, decrement, damage hooks and death handling; no Task.Run/thread pool.
			// User 2026-11-08: each target's tick waits for the curse streak thrown from the fist to reach it
			// (PoisonFx.Crushed / ArrivalDelay, 0.05-0.18 s of game time, so the crush hit-stop holds it too).
			SceneTree? tree = Engine.GetMainLoop() as SceneTree;
			NCombatRoom? room = NCombatRoom.Instance;
			Task[] ticks = combat.HittableEnemies.ToArray()
				.Select(c => (Creature: c, Power: c.GetPower<PoisonPower>()))
				.Where(x => x.Power != null)
				.Select(x => Land(tree, room?.GetCreatureNode(x.Creature) is { } n ? PoisonFx.ArrivalDelay(n.VfxSpawnPosition) : 0f, x.Power!))
				.ToArray();
			await Task.WhenAll(ticks);
		}
		finally { Clip.Ready = true; Current.Value = previous; }
	}

	private static async Task Land(SceneTree? tree, float delay, PoisonPower power)
	{
		if (delay > 0f && tree != null)
			await tree.ToSignal(tree.CreateTimer(delay), SceneTreeTimer.SignalName.Timeout);
		await power.Trigger();
	}
}

/// <summary>
/// 毒性爆发的毒伤结算（用户 2026-11-08：原版受击声很普通、很平淡）：原版每次扣血都放怪物的受击声（SfxCmd.PlayDamage），
/// 这段时间关掉，换成 PoisonFx.Blast 里的咒怨引爆声。怪物自己的叫声（HurtSfx）不动。
/// </summary>
[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.PlayDamage))]
public static class OutbreakHitSfxPatch
{
	public static bool Prefix() => OutbreakPlayback.Current.Value == null || PoisonDamage.Current.Value == null;
}

[HarmonyPatch(typeof(Outbreak), "OnPlay")]
public static class OutbreakTogetherPatch
{
	public static bool Prefix(Outbreak __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		if (!Joker.OwnsCard(__instance) || OutbreakPlayback.Take(cardPlay) is not { } play) return true;
		__result = play.Run(__instance, choiceContext);
		return false;
	}
}

/// <summary>Tag the real PoisonPower damage call, not a guessed 1.5-second "recent hit" window.</summary>
public static class PoisonDamage
{
	public static readonly AsyncLocal<PoisonPower?> Current = new();
	public static async Task<IEnumerable<DamageResult>> Apply(PlayerChoiceContext context, Creature target,
		decimal amount, ValueProp props, CardModel? cardSource, CardPlay? cardPlay, PoisonPower power)
	{
		var previous = Current.Value;
		Current.Value = power;
		try { return await CreatureCmd.Damage(context, target, amount, props, cardSource, cardPlay); }
		finally { Current.Value = previous; }
	}
}

[HarmonyPatch]
public static class PoisonDamageContextPatch
{
	public static MethodBase TargetMethod() => AccessTools.Method(
		AccessTools.Method(typeof(PoisonPower), nameof(PoisonPower.Trigger))
			.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType, "MoveNext");
	public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
	{
		FieldInfo owner = AccessTools.Field(original.DeclaringType, "<>4__this");
		MethodInfo tagged = AccessTools.Method(typeof(PoisonDamage), nameof(PoisonDamage.Apply));
		var output = new List<CodeInstruction>();
		int count = 0;
		foreach (var instruction in instructions)
		{
			if (instruction.operand is MethodInfo m && m.DeclaringType == typeof(CreatureCmd)
				&& m.Name == nameof(CreatureCmd.Damage) && m.GetParameters() is { Length: 6 } p
				&& p[1].ParameterType == typeof(Creature) && p[2].ParameterType == typeof(decimal))
			{
				output.Add(new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction));
				output.Add(new CodeInstruction(OpCodes.Ldfld, owner));
				output.Add(new CodeInstruction(OpCodes.Call, tagged));
				count++;
			}
			else output.Add(instruction);
		}
		if (count != 1 || owner == null) throw new InvalidOperationException($"PoisonPower.Trigger layout changed ({count} damage calls); no patch applied");
		return output;
	}
}
