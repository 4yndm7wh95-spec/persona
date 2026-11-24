using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
	}

	public static void Finalizer(PoisonPower? __state) => PoisonDamage.Current.Value = __state;
}

/// <summary>
/// 毒性爆发（0.9.58 的演绎，用户 2026-11-08）：毒在出牌一开始就挂上（不弹图标），每个敌人的毒伤结算等到捏碎后从拳心甩出的咒怨
/// 打到它的那一刻（PoisonFx.Crushed / ArrivalDelay）才跑。原版 Outbreak.OnPlay 照跑，只是 PoisonPower.Trigger 开始前多等一会儿：
/// 只改本地等待，结算内容和顺序都是原版的（加速剂多跳、扣层、死亡处理）。
/// </summary>
public sealed class OutbreakCue
{
	private static readonly ConditionalWeakTable<CardPlay, OutbreakCue> Plays = new();
	private readonly List<Action> _pending = new();
	private double? _crushedAt;
	private readonly double _fallbackAt;

	private OutbreakCue(float hit) => _fallbackAt = HitStop.VisualSeconds() + hit + 0.3;

	internal static OutbreakCue? Prepare(CardPlay play) =>
		play.Card is Outbreak && PoisonDamage.HasImmediateTrigger && Joker.FxLayer(back: true) != null
			? Plays.GetValue(play, p => new OutbreakCue(Aim.HitFor(p.Card))) : null;

	internal static OutbreakCue? Find(CardPlay play) => Plays.TryGetValue(play, out var cue) ? cue : null;

	/// <summary>总攻击等在途的咒怨（结算已经推迟到打到那一刻，这里不用再等）。</summary>
	internal static bool HasPendingImpact(NCombatRoom room) => false;

	internal void Crush()
	{
		if (_crushedAt != null) return;
		_crushedAt = HitStop.VisualSeconds();
		foreach (Action play in _pending.ToArray()) Stage.Safe("outbreak hit", play);
		_pending.Clear();
	}

	/// <summary>捏碎之后咒怨打到 at 的那一刻调 play（捏碎没来就按兜底时刻算）。</summary>
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

	/// <summary>等咒怨打到 target。画面层没了 / 敌人节点没了就不等。</summary>
	internal Task Arrival(Creature target)
	{
		if (Joker.FxLayer() is not Control layer || NCombatRoom.Instance?.GetCreatureNode(target) is not { } node || !Fx.Active(node))
			return Task.CompletedTask;
		var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Show(layer, node.VfxSpawnPosition, () => done.TrySetResult());
		// 万一补间没跑（画面层中途离开场景树），按真实时间兜底放行
		double limit = Math.Max(0.4, _fallbackAt - HitStop.VisualSeconds() + 1.5);
		if (layer.GetTree() is { } tree)
			tree.CreateTimer(limit, ignoreTimeScale: true).Timeout += () => done.TrySetResult();
		else
			done.TrySetResult();
		layer.TreeExiting += () => done.TrySetResult();
		return done.Task;
	}
}

/// <summary>毒性爆发里的 PoisonPower.Trigger：先等咒怨打到这个敌人，再跑原版。</summary>
[HarmonyPatch]
public static class OutbreakLandPatch
{
	private static bool _bypass;

	public static bool Prepare() => PoisonDamage.TriggerMethod != null;
	public static MethodBase TargetMethod() => PoisonDamage.TriggerMethod!;

	[HarmonyPriority(Priority.First)]
	public static bool Prefix(PoisonPower __instance, ref Task __result)
	{
		if (_bypass || OutbreakVisualPatch.Current is not { } cue || __instance.Owner.IsPlayer) return true;
		Task wait = Task.CompletedTask;
		Stage.Safe("outbreak land", () => wait = cue.Arrival(__instance.Owner));
		if (wait.IsCompleted) return true;
		__result = Land(__instance, wait);
		return false;
	}

	private static async Task Land(PoisonPower power, Task wait)
	{
		await wait;
		Task run;
		_bypass = true;
		try
		{
			run = power.Trigger();
		}
		finally
		{
			_bypass = false;
		}
		await run;
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
