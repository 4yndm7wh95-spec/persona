using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>
/// 华丽收场 = 大罪穿甲弹（Vfx/SinfulShell.cs）接进游戏（用户 2026-11-09）。原版 GrandFinale.OnPlay 照跑，只改表现和等待（0.9.58 的节奏）：
///   1. 放大罪穿甲弹演出（场景、撒旦耶尔、开枪、子弹），Joker 播 cast_sinful_from_mask，开枪时 shiv_from_aim；牌在中间亮一下就收走；
///   2. 原版聚光灯的预备等待（Cmd.Wait）换成等到子弹碰到第一个敌人那一刻才结算伤害（一次，所有敌人）；
///      不放原版命中特效和受击声（打中只响 SinfulShell 自己的那一声，用户：一发子弹只响一声），也不放我们自己的刀 / 枪命中特效；
///   3. 结算后继续等到场景收回完（演出期间出牌队列停着）。伤害数值、目标、升级都和原版一样。
/// </summary>
[HarmonyPatch(typeof(GrandFinale), "OnPlay")]
public static class GrandFinalePatch
{
	public static void Prefix(GrandFinale __instance, out SinfulPlay.Clip? __state)
	{
		__state = SinfulPlay.Current.Value;
		ModEntry.Safe("GrandFinale start", () =>
		{
			if (Joker.OwnsCard(__instance) && SatanaelPuppet.Available)
			{
				SinfulPlay.Current.Value = SinfulPlay.Start(__instance);
				if (SinfulPlay.Current.Value != null) SinfulPlay.TuckPlayedCard(__instance);
			}
		});
	}

	public static void Postfix(GrandFinale __instance, ref Task __result)
	{
		if (SinfulPlay.Current.Value is { } clip && ReferenceEquals(clip.Card, __instance))
			__result = Rest(__result, clip);
	}

	private static async Task Rest(Task play, SinfulPlay.Clip clip)
	{
		try
		{
			await play;
			if (clip.Rest > 0f) await Cmd.Wait(clip.Rest);
		}
		finally
		{
			ModEntry.Safe("GrandFinale card restore", () => SinfulPlay.RestorePlayedCard(clip.Card));   // 真的进弃牌堆以后恢复（节点可能被复用）
		}
	}

	public static void Finalizer(SinfulPlay.Clip? __state) => SinfulPlay.Current.Value = __state;
}

/// <summary>华丽收场里原版聚光灯的预备等待：换成等到子弹碰到第一个敌人。</summary>
[HarmonyPatch(typeof(Cmd), nameof(Cmd.Wait), typeof(float), typeof(CancellationToken), typeof(bool))]
public static class GrandFinaleWaitPatch
{
	public static void Prefix(ref float seconds)
	{
		if (SinfulPlay.Current.Value is { WaitTaken: false } clip)
		{
			clip.WaitTaken = true;
			seconds = clip.Hit;
		}
	}
}

/// <summary>华丽收场的伤害不放原版命中声（blunt_attack）：打中只响 SinfulShell 那一声。</summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.WithHitFx))]
public static class GrandFinaleHitFxPatch
{
	public static void Prefix(AttackCommand __instance, ref string? vfx, ref string? sfx, ref string? tmpSfx)
	{
		if (SinfulPlay.Current.Value is { } clip && ReferenceEquals(__instance.ModelSource, clip.Card))
			vfx = sfx = tmpSfx = null;
	}
}

public static class SinfulPlay
{
	public sealed record Clip(GrandFinale Card, double StartedAt, IReadOnlyDictionary<Creature, float> Impacts, float Hit, float Rest)
	{
		public bool WaitTaken;
	}
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

	/// <summary>
	/// 原版出牌时把这张牌摆在屏幕中间（出牌区，PlayContainer）一直到效果结算完，会挡住演出（用户 2026-11-09）：
	/// 演出期间把它淡到隐约看得见（alpha），结算完、飞去弃牌堆之前再淡回来。牌节点那时可能还在往中间飞，找不到就 0.1s 后再找一次。
	/// </summary>
	public static void FadePlayedCard(CardModel card, float alpha, float seconds, bool retry = true)
	{
		if (NCombatRoom.Instance?.Ui is not { } ui)
		{
			return;
		}
		NCard? node = ui.PlayContainer.GetChildren().OfType<NCard>().FirstOrDefault(c => c.Model == card)
			?? ui.PlayQueue.GetCardNode(card);
		if (node == null || !GodotObject.IsInstanceValid(node))
		{
			if (retry)
			{
				Fx.After(ui, 0.1, () => FadePlayedCard(card, alpha, seconds, false));
			}
			return;
		}
		Tween tw = node.CreateTween();
		tw.TweenProperty(node, "modulate:a", alpha, seconds);
	}

	private static NCard? PlayedNode(CardModel card) =>
		NCombatRoom.Instance?.Ui is { } ui ? ui.PlayContainer.GetChildren().OfType<NCard>().FirstOrDefault(c => c.Model == card) ?? ui.PlayQueue.GetCardNode(card) : null;

	private static readonly Dictionary<CardModel, Vector2> TuckedScale = new();

	/// <summary>
	/// 用户 2026-11-13：牌（半透明）在屏幕中间停太久、挡演出 → 在中间亮一下（0.2s）就缩小、淡掉、飞向弃牌堆。
	/// 游戏要等结算完才真的把它收进弃牌堆，那时它已经看不见了；收完以后 RestorePlayedCard 恢复（牌节点会被复用）。
	/// </summary>
	public static void TuckPlayedCard(CardModel card, bool retry = true)
	{
		if (NCombatRoom.Instance?.Ui is not { } ui)
		{
			return;
		}
		NCard? node = PlayedNode(card);
		if (node == null || !GodotObject.IsInstanceValid(node))
		{
			if (retry) Fx.After(ui, 0.1, () => TuckPlayedCard(card, false));
			return;
		}
		TuckedScale[card] = node.Scale;
		Vector2 pile = ui.DiscardPile.GetGlobalRect().GetCenter();
		Tween tw = node.CreateTween();
		tw.TweenInterval(0.2);
		tw.SetParallel();
		tw.TweenProperty(node, "global_position", pile, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tw.TweenProperty(node, "scale", node.Scale * 0.3f, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
		tw.TweenProperty(node, "modulate:a", 0f, 0.3).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
	}

	public static void RestorePlayedCard(CardModel card)
	{
		NCard? node = PlayedNode(card);
		Vector2 scale = TuckedScale.TryGetValue(card, out Vector2 s) ? s : Vector2.One;
		TuckedScale.Remove(card);
		if (node == null || !GodotObject.IsInstanceValid(node) || NCombatRoom.Instance?.Ui is not { } ui)
		{
			return;
		}
		// 游戏把它飞进弃牌堆要一小会儿：看不见地飞完再恢复
		Fx.After(ui, 0.7, () =>
		{
			if (!GodotObject.IsInstanceValid(node) || node.Modulate.A > 0.05f) return;   // 游戏已经自己改过就不动
			node.Modulate = new Color(node.Modulate, 1f);
			if (node.Scale.X < scale.X * 0.5f) node.Scale = scale;
		});
	}

	/// <summary>开演出。拿不到节点就返回 null：照常按原版结算。</summary>
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
		float hit = pierce.Length > 0 ? pierce.Min() : SinfulShell.FireAt + 0.1f;
		return new Clip(card, started, impacts, hit, Mathf.Max(0f, SinfulShell.Duration - hit));
	}
}
