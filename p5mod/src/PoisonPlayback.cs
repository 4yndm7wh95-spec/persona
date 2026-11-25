using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Threading;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>Tag native poison ticks and record the local crush cue across the game's own awaits.</summary>
public static class PoisonDamage
{
	internal static readonly MethodInfo? TriggerMethod = AccessTools.Method(typeof(PoisonPower), "Trigger", Type.EmptyTypes);
	internal static bool HasImmediateTrigger => TriggerMethod != null;
	internal static readonly AsyncLocal<PoisonPower?> Current = new();
}

[HarmonyPatch]
public static class PoisonDamageContextPatch
{
	public static MethodBase TargetMethod() => PoisonDamage.TriggerMethod
		?? AccessTools.DeclaredMethod(typeof(PoisonPower), "AfterSideTurnStart");

	public static void Prefix(PoisonPower __instance, out PoisonPower? __state)
	{
		__state = PoisonDamage.Current.Value;
		PoisonDamage.Current.Value = __instance;
		if (OutbreakVisualPatch.Current is { } cue)
			Stage.Safe("outbreak hold health", () => cue.HoldHealth(__instance.Owner));
	}

	public static void Finalizer(PoisonPower? __state) => PoisonDamage.Current.Value = __state;
}

/// <summary>Present the health change and poison damage number at the hand's impact on each enemy.</summary>
public sealed class OutbreakCue
{
	private static readonly ConditionalWeakTable<CardPlay, OutbreakCue> Plays = new();
	private static readonly ConditionalWeakTable<Creature, HealthHold> Health = new();
	private static readonly ConditionalWeakTable<NCombatRoom, RoomImpacts> Impacts = new();
	private sealed class RoomImpacts { public int Pending; }
	private sealed class HealthHold(OutbreakCue cue, NCombatRoom room, RoomImpacts impacts)
	{
		public OutbreakCue Cue { get; } = cue;
		public NCombatRoom Room { get; } = room;
		public RoomImpacts Impacts { get; } = impacts;
		public HashSet<NHealthBar> Bars { get; } = new();
	}
	private readonly List<Action> _pending = new();
	private double? _crushedAt;
	private readonly double _fallbackAt;

	private OutbreakCue(float hit) => _fallbackAt = HitStop.VisualSeconds() + hit + 0.3;

	internal static OutbreakCue? Prepare(CardPlay play) =>
		play.Card is Outbreak && PoisonDamage.HasImmediateTrigger && Joker.FxLayer(back: true) != null
			? Plays.GetValue(play, p => new OutbreakCue(Aim.HitFor(p.Card))) : null;

	internal static OutbreakCue? Find(CardPlay play) => Plays.TryGetValue(play, out var cue) ? cue : null;

	internal static bool HasPendingImpact(NCombatRoom room) =>
		Impacts.TryGetValue(room, out var impacts) && impacts.Pending > 0;

	internal void HoldHealth(Creature target)
	{
		if (target.IsPlayer || NCombatRoom.Instance is not { } room || !Fx.Active(room)) return;
		if (Health.TryGetValue(target, out var previous))
		{
			if (previous.Cue == this) return;
			previous.Cue.ReleaseHealth(target);
		}
		RoomImpacts impacts = Impacts.GetOrCreateValue(room);
		var hold = new HealthHold(this, room, impacts);
		Health.Add(target, hold);
		impacts.Pending++;

		// Release even if the native popup is unavailable or its visual layer leaves.
		double limit = Math.Max(0.4, _fallbackAt - HitStop.VisualSeconds() + 1.0);
		room.GetTree().CreateTimer(limit, ignoreTimeScale: true).Timeout += () => ReleaseHealth(target);
		if (Joker.FxLayer() is Control layer && room.GetCreatureNode(target) is { } node && Fx.Active(node))
			Show(layer, node.VfxSpawnPosition, () => ReleaseHealth(target));
	}

	internal static bool DeferHealth(NHealthBar bar, Creature target)
	{
		if (!Health.TryGetValue(target, out var hold) || NCombatRoom.Instance != hold.Room) return false;
		hold.Bars.Add(bar);
		return true;
	}

	internal void ReleaseHealth(Creature target)
	{
		if (!Health.TryGetValue(target, out var hold) || hold.Cue != this) return;
		Health.Remove(target);
		hold.Impacts.Pending--;
		foreach (NHealthBar bar in hold.Bars)
			if (Fx.Active(bar)) Stage.Safe("outbreak refresh health", bar.RefreshValues);
	}

	internal void Crush()
	{
		_crushedAt = HitStop.VisualSeconds();
		foreach (Action play in _pending.ToArray()) Stage.Safe("outbreak hit", play);
		_pending.Clear();
	}

	internal void Show(Control layer, Vector2 at, Action play)
	{
		if (_crushedAt is double crush)
		{
			float delay = (float)Math.Max(0, crush + PoisonFx.ArrivalDelay(at) - HitStop.VisualSeconds());
			Fx.After(layer, delay, play);
			return;
		}

		void AtCrush() => Fx.After(layer, PoisonFx.ArrivalDelay(at), play);
		_pending.Add(AtCrush);
		Fx.After(layer, Math.Max(0, _fallbackAt - HitStop.VisualSeconds()), () =>
		{
			if (_pending.Remove(AtCrush)) play();
		});
	}
}

[HarmonyPatch(typeof(Outbreak), "OnPlay")]
public static class OutbreakVisualPatch
{
	private static readonly AsyncLocal<OutbreakCue?> Visual = new();
	internal static OutbreakCue? Current => Visual.Value;

	public static void Prefix(CardPlay cardPlay, out OutbreakCue? __state)
	{
		__state = Visual.Value;
		Visual.Value = OutbreakCue.Find(cardPlay);
	}

	public static void Finalizer(OutbreakCue? __state) => Visual.Value = __state;
}

[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.PlayDamage))]
public static class OutbreakHitSfxPatch
{
	public static bool Prefix() => OutbreakVisualPatch.Current == null || PoisonDamage.Current.Value == null;
}
