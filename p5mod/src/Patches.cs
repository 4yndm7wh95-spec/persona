using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace P5Joker;

/// <summary>Joker 的攻击牌：关掉原版命中特效（刀光、飞刀），换成 HitFx（JokerFx.OnHit）；原版命中音效保留。</summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
public static class AttackHitVfxPatch
{
	private static readonly PropertyInfo HitVfx = AccessTools.Property(typeof(AttackCommand), "HitVfx");
	private static readonly FieldInfo CustomHitNodes = AccessTools.Field(typeof(AttackCommand), "_customHitVfxNodes");
	private static readonly FieldInfo HitCount = AccessTools.Field(typeof(AttackCommand), "_hitCount");
	private static readonly FieldInfo CustomAttackerNodes = AccessTools.Field(typeof(AttackCommand), "_customAttackerVfxNodes");

	public static void Prefix(AttackCommand __instance)
	{
		ModEntry.Safe("AttackHitVfx", () =>
		{
			if (__instance.ModelSource is CardModel card && Joker.OwnsCard(card))
			{
				HitWeights.BeginAttack(card, HitCount.GetValue(__instance) is int n ? n : 1);
				HitVfx.SetValue(__instance, null);
				(CustomHitNodes.GetValue(__instance) as IList)?.Clear();     // 小刀牌原版的飞刀（NShivThrowVfx）
				if (card.GetType().Name == "DaggerThrow")
				{
					// 投掷匕首：原版和匕首雨共用一片飞刀（NDaggerSprayFlurryVfx）→ 关掉，用打击动作 + 刀的命中（用户 2026-11-04）
					(CustomAttackerNodes.GetValue(__instance) as IList)?.Clear();
				}
			}
		});
	}
}

/// <summary>原版中和出牌时在敌人身上放的细斩（NThinSliceVfx）：Joker 出的牌不要，刀光由 HitFx / CardFx 放。</summary>
[HarmonyPatch(typeof(NThinSliceVfx), nameof(NThinSliceVfx.Create))]
public static class ThinSlicePatch
{
	public static bool Prefix(ref NThinSliceVfx? __result)
	{
		if (Godot.Time.GetTicksMsec() - JokerFx.LastPlayMs < 250)
		{
			__result = null;
			return false;
		}
		return true;
	}
}

/// <summary>小刀弹药（Vfx/Ammo.cs）：Joker 的能量球每帧更新旁边的左轮弹巢 = 手牌里的小刀牌张数。</summary>
[HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter._Process))]
public static class AmmoPatch
{
	private static readonly FieldInfo PlayerField = AccessTools.Field(typeof(NEnergyCounter), "_player");
	private static readonly Dictionary<ulong, AmmoView> Views = new();

	/// <summary>本地 Joker 的弹巢（开火时 JokerFx 让它转一格）。</summary>
	public static AmmoView? Local;

	public static void Postfix(NEnergyCounter __instance, double delta)
	{
		ModEntry.Safe("Ammo", () =>
		{
			if (PlayerField.GetValue(__instance) is not MegaCrit.Sts2.Core.Entities.Players.Player player || !Joker.Is(player))
			{
				return;
			}
			ulong id = __instance.GetInstanceId();
			if (!Views.TryGetValue(id, out AmmoView? view) || !Godot.GodotObject.IsInstanceValid(view.Root))
			{
				view = new AmmoView(__instance);
				Views[id] = view;
			}
			if (MegaCrit.Sts2.Core.Context.LocalContext.IsMe(player))
			{
				Local = view;
				// 腐蚀波生效中：Joker 脚下咒怨一直翻腾（Vfx/CorrosiveFx.cs）
				if (player.Creature.GetPower<MegaCrit.Sts2.Core.Models.Powers.CorrosiveWavePower>() != null && Joker.FxLayer() is Godot.Control cl
					&& Joker.Node(player) is NCreature jn)
				{
					CorrosiveFx.Aura(cl, Joker.PosePoint(jn, ReadyFx.Feet));
				}
				// 每场战斗预热一次第四版特效（首次打出不卡，Vfx/Warmup.cs）
				if (Joker.FxLayer() is Godot.Control fx && NCombatRoom.Instance is NCombatRoom room)
				{
					Warmup.Run(fx, room.GetInstanceId());
				}
			}
			// 能量球右上方
			view.Root.Position = __instance.Size / 2f + new Godot.Vector2(110f, -70f);
			int shivs = player.PlayerCombatState?.Hand.Cards.Count(Joker.IsShot) ?? 0;
			view.Set(shivs);
			view.Tick((float)delta);
		});
	}
}

/// <summary>每一击伤害（所有 Damage 重载最终汇到参数最多的那个）：放命中特效，记下 Joker 打中了谁、是不是枪击（伤害数字据此换成 P5 样式）。</summary>
[HarmonyPatch]
public static class DamagePatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "Damage" && m.GetParameters().Any(p => p.Name == "cardSource") && m.GetParameters().Any(p => p.Name == "targets"))
			.OrderByDescending(m => m.GetParameters().Length).Take(1);

	public static void Prefix(IEnumerable<Creature>? targets, decimal amount, CardModel? cardSource)
	{
		ModEntry.Safe("Damage", () =>
		{
			if (targets == null || cardSource == null || !Joker.OwnsCard(cardSource))
			{
				return;
			}
			if (SinfulPlay.Active)
			{
				Hits.Mark(targets, false, Weight.Heavy);          // 华丽收场：数字照样换成 P5 样式，敌人那边的特效和声音全由 SinfulShell 放
				return;
			}
			bool gun = Joker.IsShot(cardSource);
			(Weight floor, bool calm) = HitWeights.NextHit(cardSource, gun);
			Hits.Mark(targets, gun, floor, calm);
			if (Joker.Node(cardSource.Owner) is NCreature joker && NCombatRoom.Instance is NCombatRoom room)
			{
				List<NCreature> hit = targets.Where(c => !c.IsPlayer).Select(room.GetCreatureNode).OfType<NCreature>().ToList();
				if (gun && hit.Count > 1)
				{
					JokerFx.OnRicochet(joker, hit);                // 刀扇：一发子弹弹过所有敌人
					return;
				}
				foreach (NCreature n in hit)
				{
					// 枪：这一发（按基础伤害估计）会打死目标 → 用收尾的重命中声（Five Cards 演示最后一发）
					bool final = gun && n.Entity is Creature ce && amount >= ce.CurrentHp + ce.Block;
					JokerFx.OnHit(joker, n, gun, final);
				}
			}
		});
	}
}

/// <summary>Joker 最近 1.5 秒内打中的目标（多段 / 延迟结算都在窗口内）。</summary>
public static class Hits
{
	private static readonly Dictionary<Creature, (ulong Time, bool Gun, Weight Floor, bool Calm)> Marked = new();

	public static void Mark(IEnumerable<Creature> targets, bool gun, Weight floor = Weight.Medium, bool calm = false)
	{
		ulong now = Godot.Time.GetTicksMsec();
		if (Marked.Count > 32)
		{
			foreach (Creature old in Marked.Where(kv => now - kv.Value.Time > 1500).Select(kv => kv.Key).ToList())
			{
				Marked.Remove(old);
			}
		}
		foreach (Creature c in targets)
		{
			if (!c.IsPlayer)
			{
				Marked[c] = (now, gun, floor, calm);
			}
		}
	}

	public static bool Recent(Creature target, out bool gun)
	{
		gun = false;
		if (!Marked.TryGetValue(target, out var m) || Godot.Time.GetTicksMsec() - m.Time > 1500)
		{
			return false;
		}
		gun = m.Gun;
		return true;
	}

	/// <summary>这一下的底档（HitWeights 按牌定的）和是不是多段攻击的前几下。</summary>
	public static (Weight Floor, bool Calm) FloorOf(Creature target) =>
		Marked.TryGetValue(target, out var m) ? (m.Floor, m.Calm) : (Weight.Medium, false);
}

/// <summary>伤害数字：Joker 打中的敌人，原版数字换成 P5R 样式（Popups.Damage）。全被格挡（0 点）时保留原版。</summary>
[HarmonyPatch(typeof(NDamageNumVfx), nameof(NDamageNumVfx.Create), typeof(Creature), typeof(DamageResult))]
public static class DamageNumberPatch
{
	public static void Postfix(Creature target, DamageResult result, ref NDamageNumVfx? __result)
	{
		NDamageNumVfx? original = __result;
		bool replaced = false;
		ModEntry.Safe("DamageNumber", () =>
		{
			// Every real Joker-owned poison tick, including every Accelerant repeat, owns its number.
			if (original != null && !target.IsPlayer && PoisonDamage.Current.Value is { } poison
				&& ReferenceEquals(poison.Owner, target)
				&& poison.Applier?.Player is { } owner && Joker.Is(owner)
				&& NCombatRoom.Instance?.GetCreatureNode(target) is NCreature pn && Joker.FxLayer() is Godot.Control pl)
			{
				int damage = result.UnblockedDamage + (target.Monster is MegaCrit.Sts2.Core.Models.Monsters.Osty ? 0 : result.OverkillDamage);
				if (damage <= 0) return;
				Popups.Damage(pl, pn.GetInstanceId(), pn.VfxSpawnPosition, damage, false);
				PoisonFx.Tick(pl, pn.VfxSpawnPosition, pn.GetTopOfHitbox(), pn.Hitbox.GetGlobalRect().Size.X, pn.Visuals,
					outbreak: OutbreakPlayback.Current.Value != null, killed: result.WasTargetKilled);
				Grip.OnTick(pn.GetInstanceId());
				DepthGrip.OnTick(pn.GetInstanceId());
				Myriad.OnPoisonTick(pl, pn.GetInstanceId(), pn.VfxSpawnPosition, pn.GetTopOfHitbox(), null);
				original.QueueFree();
				replaced = true;
				return;
			}
			if (original == null || target.IsPlayer || !Hits.Recent(target, out bool gun)
				|| NCombatRoom.Instance?.GetCreatureNode(target) is not NCreature node || Joker.FxLayer() is not Godot.Control layer)
			{
				return;
			}
			int amount = result.UnblockedDamage + (target.Monster is MegaCrit.Sts2.Core.Models.Monsters.Osty ? 0 : result.OverkillDamage);
			if (amount <= 0)
			{
				return;
			}
			float wait = JokerFx.DamageNumberDelay(node);
			if (wait > 0f)
			{
				Fx.After(layer, wait, () => Popups.Damage(layer, node.GetInstanceId(), node.VfxSpawnPosition, amount, gun));
			}
			else
			{
				Popups.Damage(layer, node.GetInstanceId(), node.VfxSpawnPosition, amount, gun);
			}
			// 打击确认感：顿帧 + 定向震屏 + 受击白闪 / 击退 + 重击 / 击杀的全屏冲击帧（Vfx/Impact.cs）
			(Weight floor, bool calm) = Hits.FloorOf(target);
			bool finale = result.WasTargetKilled && target.CombatState?.Enemies.All(e => e.IsDead) == true;
			if (!SinfulPlay.Active)
			Impact.Confirm(layer, node.Visuals, node.VfxSpawnPosition, new Godot.Vector2(1f, 0.12f),
				Impact.Grade(floor, result.UnblockedDamage, target.CurrentHp + result.UnblockedDamage, result.WasTargetKilled, finale, calm));
			original.QueueFree();
			replaced = true;
		});
		if (replaced)
		{
			__result = null;                                   // 调用方拿到 null 就不显示原版数字
		}
	}
}

/// <summary>
/// 「BLOCK」：Joker 的格挡完全挡住一次攻击时（原版 CreatureCmd.Damage 里 WasFullyBlocked → NDamageBlockedVfx.Create），
/// 原版的"格挡"字样换成 P5 的 BLOCK（Popups.Block），照 P3 mod 的做法。原版音效保留。
/// </summary>
[HarmonyPatch(typeof(NDamageBlockedVfx), nameof(NDamageBlockedVfx.Create))]
public static class BlockPopupPatch
{
	public static bool Prefix(Creature target, ref NDamageBlockedVfx? __result)
	{
		bool replaced = false;
		ModEntry.Safe("BlockPopup", () =>
		{
			if (target.IsPlayer && Joker.Is(target.Player) && NCombatRoom.Instance?.GetCreatureNode(target) is NCreature node
				&& Joker.FxLayer() is Godot.Control front)
			{
				Popups.Block(front, Joker.Chest(node), Joker.Facing(node));
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

/// <summary>Joker 格挡时原版的蓝色格挡火花也不要（换成 Popups.Block 的冲击）。</summary>
[HarmonyPatch(typeof(NBlockSparkVfx), nameof(NBlockSparkVfx.Create))]
public static class BlockSparkPatch
{
	public static bool Prefix(Creature target, ref NBlockSparkVfx? __result)
	{
		bool skip = false;
		ModEntry.Safe("BlockSpark", () => skip = target.IsPlayer && Joker.Is(target.Player));
		if (skip)
		{
			__result = null;
			return false;
		}
		return true;
	}
}

/// <summary>Joker 用牌获得格挡（所有 GainBlock 重载都汇到带 cardPlay 的那个）：身前张开光盾（JokerFx.OnBlock）。</summary>
[HarmonyPatch]
public static class GainBlockPatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(m => m.Name == "GainBlock" && m.GetParameters().Any(p => p.Name == "cardPlay") && m.GetParameters().Any(p => p.Name == "creature"))
			.OrderByDescending(m => m.GetParameters().Length).Take(1);

	public static void Prefix(Creature creature, decimal amount, CardPlay? cardPlay)
	{
		ModEntry.Safe("GainBlock", () =>
		{
			if (amount > 0m && cardPlay != null && Joker.OwnsCard(cardPlay.Card) && creature.Player is { } p && Joker.Is(p)
				&& NCombatRoom.Instance?.GetCreatureNode(creature) is NCreature node)
			{
				JokerFx.OnBlock(node, (int)amount);
			}
		});
	}
}

/// <summary>原版致命毒药等出牌时在敌人身上放的毒雾（NPoisonImpactVfx）：Joker 出的牌不要，换成咒怨（CardFx.Eiha）。</summary>
[HarmonyPatch(typeof(NPoisonImpactVfx), nameof(NPoisonImpactVfx.Create), typeof(Creature))]
public static class PoisonImpactPatch
{
	public static bool Prefix(ref NPoisonImpactVfx? __result)
	{
		if (Godot.Time.GetTicksMsec() - JokerFx.LastPlayMs < 250)
		{
			__result = null;
			return false;
		}
		return true;
	}
}

/// <summary>
/// Joker 的刀砍下去和伤害同时到：原版中和等牌在普通速度下把出刀到结算拉长到 0.35s，Joker 的 attack 0.14s 就出刀了，
/// 命中特效和数字晚一截 —— Joker 的牌一律按静默猎手的 AttackAnimDelay（0.15s）结算。只改等待时间，不改结果。
/// </summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.WithAttackerAnim))]
public static class AttackDelayPatch
{
	public static void Prefix(AttackCommand __instance, string? animName, ref float delay)
	{
		if (animName == "Attack" && delay > 0.15f && __instance.ModelSource is CardModel card && Joker.OwnsCard(card))
		{
			delay = 0.15f;
		}
	}
}

/// <summary>
/// Persona 召唤：牌在施法（TriggerAnim "Cast"）之后才结算（加中毒、扣血），把这段等待推到亚森出手的那一刻（Aim.SummonHit），
/// 亚森出手、敌人身上炸开、数字跳出来是同一刻（用户 2026-11-01）。只改等待时间，不改结果。
/// </summary>
[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.TriggerAnim))]
public static class TriggerAnimDelayPatch
{
	public static void Prefix(Creature creature, string triggerName, ref float waitTime)
	{
		float hit = 0f;
		ModEntry.Safe("TriggerAnimDelay", () =>
		{
			if (triggerName == "Cast" && creature.IsPlayer && Joker.Is(creature.Player) && NCombatRoom.Instance?.GetCreatureNode(creature) is NCreature node)
			{
				hit = Aim.TakeSummon(node);
				if (OutbreakPlayback.Current.Value != null) hit = 0f;
			}
		});
		if (OutbreakPlayback.Current.Value != null && triggerName == "Cast") waitTime = 0f;
		if (hit > 0f)
		{
			waitTime = Godot.Mathf.Max(waitTime, hit);
		}
	}
}

/// <summary>出牌那一刻（结算之前）：技能 / 能力牌 Joker 自己施法（Aim.PlayCast），放出牌特效；记下施法动作（Aim 改动画名时用）。</summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed))]
public static class CardPlayedPatch
{
	public static void Prefix(CardPlay cardPlay)
	{
		ModEntry.Safe("CardPlayed", () =>
		{
			if (Joker.OwnsCard(cardPlay.Card) && Joker.Node(cardPlay.Card.Owner) is NCreature node)
			{
				OutbreakPlayback? playback = OutbreakPlayback.Prepare(cardPlay);
				Aim.PendingCast[node.GetInstanceId()] = Casts.For(cardPlay.Card);
				if (cardPlay.Card.Type != CardType.Attack)
				{
					Aim.PlayCast(node, cardPlay.Card, playback);
				}
				JokerFx.OnPlay(cardPlay.Card, node, cardPlay.Target);
			}
		});
	}
}

/// <summary>
/// 施毒（第四版，Vfx/PoisonFx.cs）：游戏给敌人加中毒 / 叠毒时都会弹一下状态图标（NCreature.OnPowerIncreased → NPowerAppliedVfx.Create），
/// 挂在这里就和游戏加毒同一刻，所有来源（致命毒药、带毒刺击、弹跳药瓶每一跳、涂毒、腐蚀波、毒雾、迷雾……）一起覆盖。只给 Joker 施的毒放。
/// </summary>
[HarmonyPatch(typeof(NPowerAppliedVfx), nameof(NPowerAppliedVfx.Create))]
public static class PoisonApplyPatch
{
	/// <summary>
	/// 毒性爆发（用户 2026-11-08）：毒在出牌一开始就加上了，原版的中毒图标弹出 + 减益特效会让敌人在 0 秒就有反应、抢了捏碎的戏 →
	/// Joker 的毒性爆发上毒时不弹（只是不显示，层数照加）。敌人的反应全部留到捏碎后咒怨打到的那一刻。
	/// </summary>
	public static bool Prefix(PowerModel power, ref NPowerAppliedVfx? __result)
	{
		if (OutbreakPlayback.Current.Value != null && power is MegaCrit.Sts2.Core.Models.Powers.PoisonPower && !power.Owner.IsPlayer)
		{
			__result = null;
			return false;
		}
		return true;
	}

	public static void Postfix(PowerModel power, int amount)
	{
		ModEntry.Safe("PoisonApply", () =>
		{
			if (power is not MegaCrit.Sts2.Core.Models.Powers.PoisonPower || power.Owner.IsPlayer || power.Applier?.Player is not { } p || !Joker.Is(p)
				|| Joker.FxLayer() is not Godot.Control layer || NCombatRoom.Instance?.GetCreatureNode(power.Owner) is not NCreature n)
			{
				return;
			}
			PoisonFx.Apply(layer, n.VfxSpawnPosition, n.GetTopOfHitbox(), n.Hitbox.GetGlobalRect().Size.X, n.Visuals, amount, outbreak: OutbreakPlayback.Current.Value != null);
		});
	}
}

/// <summary>
/// 腐蚀波：每抽到一张牌（CorrosiveWavePower.AfterCardDrawn，原版在这里给全体上毒、在每个敌人身上放绿色毒气）——
/// 在原版上毒之前排一道贴地的咒火浪（Vfx/CorrosiveFx.cs）；这段时间的上毒不放普通施毒落点，原版的绿色毒气藏掉。只给 Joker 的腐蚀波。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.CorrosiveWavePower), nameof(MegaCrit.Sts2.Core.Models.Powers.CorrosiveWavePower.AfterCardDrawn))]
public static class CorrosiveDrawPatch
{
	public static void Prefix(MegaCrit.Sts2.Core.Models.Powers.CorrosiveWavePower __instance, CardModel card)
	{
		ModEntry.Safe("CorrosiveDraw", () =>
		{
			if (card.Owner?.Creature != __instance.Owner || __instance.Owner.Player is not { } p || !Joker.Is(p)
				|| Joker.FxLayer() is not Godot.Control layer || NCombatRoom.Instance is not NCombatRoom room || Joker.Node(p) is not NCreature joker)
			{
				return;
			}
			var targets = new List<CorrosiveFx.Target>();
			foreach (Creature c in __instance.CombatState.HittableEnemies)
			{
				if (room.GetCreatureNode(c) is NCreature n)
				{
					targets.Add(new CorrosiveFx.Target(n.VfxSpawnPosition, n.GetTopOfHitbox(), n.Hitbox.GetGlobalRect().Size.X, n.Visuals));
				}
			}
			CorrosiveFx.OnDraw(layer, Joker.PosePoint(joker, ReadyFx.Feet), Joker.Facing(joker), targets);
		});
	}
}

/// <summary>原版腐蚀波每次抽牌在敌人身上放的绿色毒气：Joker 的腐蚀波正由咒火浪表现时藏掉。</summary>
[HarmonyPatch(typeof(NGaseousImpactVfx), nameof(NGaseousImpactVfx.Create), typeof(Godot.Vector2), typeof(Godot.Color))]   // 原版腐蚀波用的是这个重载
public static class CorrosiveGasPatch
{
	public static void Postfix(NGaseousImpactVfx? __result)
	{
		if (__result != null && (CorrosiveFx.Suppressing || SnakeFx.FlaskActive))
		{
			__result.Visible = false;
		}
	}
}

/// <summary>
/// 弹跳烧瓶（Joker）：原版扔出的绿色毒药瓶藏掉，换成咒怨火球沿同一条线飞（SnakeFx.Throw）。
/// 原版给的终点是敌人碰撞框底边，火球落到敌人身体中心（VfxSpawnPosition），按底边找最近的那个敌人。
/// </summary>
[HarmonyPatch(typeof(NItemThrowVfx), nameof(NItemThrowVfx.Create))]
public static class FlaskThrowPatch
{
	public static void Postfix(NItemThrowVfx? __result, Godot.Vector2 sourcePosition, Godot.Vector2 targetPosition)
	{
		if (__result == null || !SnakeFx.FlaskActive)
		{
			return;
		}
		ModEntry.Safe("FlaskThrow", () =>
		{
			__result.Visible = false;
			if (Joker.FxLayer() is not Godot.Control layer || NCombatRoom.Instance is not NCombatRoom room)
			{
				return;
			}
			Godot.Vector2 to = targetPosition;
			float best = float.MaxValue;
			foreach (NCreature n in room.CreatureNodes)
			{
				float d = n.GetBottomOfHitbox().DistanceSquaredTo(targetPosition);
				if (n.Entity != null && !n.Entity.IsPlayer && d < best)
				{
					best = d;
					to = n.VfxSpawnPosition;
				}
			}
			SnakeFx.Throw(layer, sourcePosition, to);
		});
	}
}

/// <summary>弹跳烧瓶（Joker）：原版落地的绿色水花、液体罩子藏掉（毒气在 CorrosiveGasPatch 里一起藏）。</summary>
[HarmonyPatch]
public static class FlaskSplashPatch
{
	public static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(NSplashVfx), nameof(NSplashVfx.Create));
		yield return AccessTools.Method(typeof(NLiquidOverlayVfx), nameof(NLiquidOverlayVfx.Create));
	}

	public static void Postfix(object? __result)
	{
		if (__result is Godot.CanvasItem c && SnakeFx.FlaskActive)
		{
			c.Visible = false;
		}
	}
}
