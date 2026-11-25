using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

[HarmonyPatch(typeof(GrandFinale), "OnPlay")]
public static class GrandFinalePatch
{
	public static void Prefix(GrandFinale __instance, out SinfulPlay.Clip? __state)
	{
		__state = SinfulPlay.Current.Value;
		ModEntry.Safe("GrandFinale start", () =>
		{
			if (Joker.OwnsCard(__instance) && SatanaelPuppet.Available)
				SinfulPlay.Current.Value = SinfulPlay.Start(__instance);
		});
	}

	public static void Finalizer(SinfulPlay.Clip? __state) => SinfulPlay.Current.Value = __state;
}

public static class SinfulPlay
{
	public sealed record Clip(GrandFinale Card, double StartedAt, IReadOnlyDictionary<Creature, float> Impacts);
	internal static readonly AsyncLocal<Clip?> Current = new();
	internal static bool Active => Current.Value != null;
	private static readonly ConditionalWeakTable<NCombatRoom, EndCue> EndCues = new();
	private sealed class EndCue { public double At; }

	internal static double Remaining(NCombatRoom room) => EndCues.TryGetValue(room, out EndCue? cue)
		? Math.Max(0, cue.At - HitStop.VisualSeconds()) : 0;

	internal static bool IsPlaying(CardModel card) => ReferenceEquals(Current.Value?.Card, card);

	internal static float ImpactDelay(Creature target) =>
		Current.Value is { } clip && clip.Impacts.TryGetValue(target, out float hit)
			? Mathf.Max(0f, hit - (float)(HitStop.VisualSeconds() - clip.StartedAt)) : 0f;

	internal static Clip? Start(GrandFinale card)
	{
		if (Joker.Node(card.Owner) is not NCreature joker || Joker.FxLayer() is not Control front || Joker.FxLayer(true) is not Control back
			|| NCombatRoom.Instance is not NCombatRoom room)
		{
			return null;
		}
		List<NCreature> enemies = (joker.Entity?.CombatState?.HittableEnemies ?? Enumerable.Empty<Creature>())
			.Select(room.GetCreatureNode).OfType<NCreature>().ToList();
		if (enemies.Count == 0)
		{
			return null;
		}
		Vector2 feet = Joker.PosePoint(joker, ReadyFx.Feet), head = Joker.PosePoint(joker, ReadyFx.MaskFace);
		var actors = new List<CanvasItem>();
		if (joker.Visuals is CanvasItem jv) actors.Add(jv);
		actors.AddRange(enemies.Select(e => e.Visuals).OfType<CanvasItem>());
		MegaCrit.Sts2.Core.Bindings.MegaSpine.MegaAnimationState? state = joker.Visuals?.SpineBody?.TryGetAnimationState();
		double started = HitStop.VisualSeconds();
		float[] pierce = SinfulShell.Play(back, front, new SinfulShell.Setup
		{
			JokerFeet = feet,
			JokerHead = head,
			JokerMuzzle = Joker.Muzzle(joker),
			FloorY = feet.Y,
			Enemies = enemies.Select(e => e.VfxSpawnPosition).ToList(),
			EnemyVisuals = enemies.Select(e => (CanvasItem?)e.Visuals).ToList(),
			Actors = actors,
			Shadows = enemies.Select(e => (e.GetBottomOfHitbox(), e.Hitbox.GetGlobalRect().Size.X * 0.95f))
				.Prepend((feet, (feet - head).Length() * 0.55f)).ToList(),
			OnJokerShoot = () => ModEntry.Safe("GrandFinale shoot", () =>
			{
				if (state != null && joker.Visuals?.SpineBody?.HasAnimation("shiv_from_aim") == true)
				{
					state.SetSpineAnimation("shiv_from_aim", false, 0);
					state.AddSpineAnimation("idle_loop", 0f, true, 0);
				}
			}),
		});
		// Joker：扶着面具 → 攥面具 → 手顺着弧线放下转枪 → 瞄准（骨骼里保持到开枪）
		if (state != null && joker.Visuals?.SpineBody?.HasAnimation("cast_sinful_from_mask") == true)
		{
			state.SetSpineAnimation("cast_sinful_from_mask", false, 0);
			state.AddSpineAnimation("idle_loop", 0f, true, 0);
		}
		Fx.After(front, 0.16, Aim.ReleaseHeld);                        // 攥面具那一下：面具上的火炸开
		var impacts = new Dictionary<Creature, float>();
		for (int i = 0; i < pierce.Length && i < enemies.Count; i++)
			if (enemies[i].Entity is { } target) impacts[target] = pierce[i];
		EndCues.GetOrCreateValue(room).At = started + SinfulShell.Duration;
		return new Clip(card, started, impacts);
	}
}
