using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>
/// 华丽收场 = 大罪穿甲弹（Vfx/SinfulShell.cs）接进游戏（用户 2026-11-09）。
/// 原版 GrandFinale.OnPlay：放原版聚光灯特效、等它的预备时长，然后对所有敌人一次结算（带原版命中特效和 blunt_attack 声）。
/// Joker 打出时整个换掉：
///   1. 放大罪穿甲弹演出（场景、撒旦耶尔、开枪、子弹），Joker 播 cast_sinful_from_mask，开枪时 shiv_from_aim；
///   2. 等到子弹碰到第一个敌人那一刻才结算伤害（一次，所有敌人），不放原版命中特效和受击声，也不放我们自己的刀 / 枪命中特效（Active 期间 DamagePatch 跳过）；
///      打中只响 SinfulShell 自己的那一声（用户：一发子弹只响一声）；
///   3. 结算后继续等到场景收回完，演出期间出牌队列停着（等于锁住操作）。
/// 伤害数值、目标、升级都和原版一样。
/// </summary>
[HarmonyPatch(typeof(GrandFinale), "OnPlay")]
public static class GrandFinalePatch
{
	public static bool Prefix(GrandFinale __instance, PlayerChoiceContext choiceContext, CardPlay cardPlay, ref Task __result)
	{
		if (!Joker.OwnsCard(__instance) || !SatanaelPuppet.Available)
		{
			return true;
		}
		__result = Run(__instance, choiceContext, cardPlay);
		return false;
	}

	private static async Task Run(GrandFinale card, PlayerChoiceContext ctx, CardPlay play)
	{
		float hit = 0f, rest = 0f;
		ModEntry.Safe("GrandFinale start", () => (hit, rest) = SinfulPlay.Start(card));
		ModEntry.Safe("GrandFinale card tuck", () => SinfulPlay.TuckPlayedCard(card));
		if (hit > 0f)
		{
			await Cmd.Wait(hit);
		}
		SinfulPlay.Active = true;
		try
		{
			if (card.CombatState is { } cs)
			{
				await DamageCmd.Attack(card.DynamicVars.Damage.BaseValue).FromCard(card, play).TargetingAllOpponents(cs)
					.WithNoAttackerAnim().WithHitFx(null, null, null).Execute(ctx);
			}
		}
		finally
		{
			SinfulPlay.Active = false;
		}
		if (rest > 0f)
		{
			await Cmd.Wait(rest);
		}
		ModEntry.Safe("GrandFinale card restore", () => SinfulPlay.RestorePlayedCard(card));   // 真的进弃牌堆以后恢复（节点可能被复用）
	}
}

/// <summary>华丽收场演出的游戏侧：从真实节点取坐标，开演出，驱动 Joker 的动作。</summary>
public static class SinfulPlay
{
	/// <summary>华丽收场正在结算伤害：DamagePatch / 伤害数字的打击确认跳过（敌人那边的特效和声音全由 SinfulShell 放）。</summary>
	public static bool Active;

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

	/// <summary>开演出。返回（出牌后多久结算伤害, 结算后还要等多久演出才完）。拿不到节点就返回 (0,0)：照常立即结算。</summary>
	public static (float Hit, float After) Start(GrandFinale card)
	{
		if (Joker.Node(card.Owner) is not NCreature joker || Joker.FxLayer() is not Control front || Joker.FxLayer(true) is not Control back
			|| NCombatRoom.Instance is not NCombatRoom room)
		{
			return (0f, 0f);
		}
		List<NCreature> enemies = (joker.Entity?.CombatState?.HittableEnemies ?? Enumerable.Empty<Creature>())
			.Select(room.GetCreatureNode).OfType<NCreature>().ToList();
		if (enemies.Count == 0)
		{
			return (0f, 0f);
		}
		Vector2 feet = Joker.PosePoint(joker, ReadyFx.Feet), head = Joker.PosePoint(joker, ReadyFx.MaskFace);
		var actors = new List<CanvasItem>();
		if (joker.Visuals is CanvasItem jv) actors.Add(jv);
		actors.AddRange(enemies.Select(e => e.Visuals).OfType<CanvasItem>());
		MegaCrit.Sts2.Core.Bindings.MegaSpine.MegaAnimationState? state = joker.Visuals?.SpineBody?.TryGetAnimationState();
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
					state.SetAnimation("shiv_from_aim", false, 0);
					state.AddAnimation("idle_loop", 0f, true, 0);
				}
			}),
		});
		// Joker：扶着面具 → 攥面具 → 手顺着弧线放下转枪 → 瞄准（骨骼里保持到开枪）
		if (state != null && joker.Visuals?.SpineBody?.HasAnimation("cast_sinful_from_mask") == true)
		{
			state.SetAnimation("cast_sinful_from_mask", false, 0);
			state.AddAnimation("idle_loop", 0f, true, 0);
		}
		Fx.After(front, 0.16, Aim.ReleaseHeld);                        // 攥面具那一下：面具上的火炸开
		float hit = pierce.Length > 0 ? pierce.Min() : SinfulShell.FireAt + 0.1f;
		return (hit, Mathf.Max(0f, SinfulShell.Duration - hit));
	}
}
