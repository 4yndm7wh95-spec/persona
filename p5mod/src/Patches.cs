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
	// 多段攻击只挥一次（0.9.49 审阅第五批，Vfx/Batch5Fx.cs）：字段找不到（游戏改名）就保持原版行为
	private static readonly FieldInfo? PlayOnEveryHit = AccessTools.Field(typeof(AttackCommand), "_playOnEveryHit");
	private static readonly FieldInfo? WaitBeforeHit = AccessTools.Field(typeof(AttackCommand), "_waitBeforeHit");
	private static readonly FieldInfo? AnimDelay = AccessTools.Field(typeof(AttackCommand), "_attackerAnimDelay");

	public static void Prefix(AttackCommand __instance)
	{
		ModEntry.Safe("AttackHitVfx", () =>
		{
			if (__instance.ModelSource is CardModel card && Joker.OwnsCard(card))
			{
				HitWeights.BeginAttack(card, HitCount.GetValue(__instance) is int n ? n : 1);
				string cname = card.GetType().Name;
				if (Batch5Fx.OneSwing.TryGetValue(cname, out float gap) && PlayOnEveryHit != null && WaitBeforeHit?.GetValue(__instance) is float[] wait && wait.Length >= 2)
				{
					// 终结技 / 飞镖 / 连续反弹（用户：不要让 Joker 重复打击动作 N 次）：动作只在第一下放，之后每下隔 gap 秒，由 Batch5Fx 表现
					PlayOnEveryHit.SetValue(__instance, false);
					wait[0] = gap;
					wait[1] = gap;
					if (AnimDelay?.GetValue(__instance) is float d) AnimDelay.SetValue(__instance, Math.Max(0f, d - gap));   // 第一下别因为多等的 gap 变晚
					Batch5Fx.Begin(cname, HitCount.GetValue(__instance) is int hc ? hc : 1);
				}
				HitVfx.SetValue(__instance, null);
				(CustomHitNodes.GetValue(__instance) as IList)?.Clear();     // 小刀牌原版的飞刀（NShivThrowVfx）
				if (card.GetType().Name == "DaggerSpray" && Joker.Node(card.Owner) is NCreature jn && Joker.FxLayer() is Godot.Control dl
					&& NCombatRoom.Instance is NCombatRoom room)
				{
					// 匕首雨（0.9.49 审阅：没有群体攻击的感觉；用户 2026-11-18：是攻击两下的）：原版每一下都重放攻击动作（_playOnEveryHit），
					// 动作开始约 0.15s 后结算伤害 → 每挥一刀放一拨群体特效（Vfx/Batch3Fx.cs）
					var foes = new List<Batch3Fx.Foe>();
					foreach (Creature c in __instance.Attacker?.CombatState?.HittableEnemies ?? (IReadOnlyList<Creature>)Array.Empty<Creature>())
					{
						if (room.GetCreatureNode(c) is NCreature en && Godot.GodotObject.IsInstanceValid(en))
						{
							foes.Add(new Batch3Fx.Foe(en.VfxSpawnPosition, en.GetTopOfHitbox(), en.Hitbox.GetGlobalRect().Size.X));
						}
					}
					Batch3Fx.ArmSpray(foes);   // 每次挥刀（JokerFx.OnAnim）放一拨，跟着两下伤害走
				}
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
				// 预热由菜单 / 非战斗空闲队列负责，不在战斗首次更新时集中启动。
			}
			// 能量球右上方
			view.Root.Position = __instance.Size / 2f + new Godot.Vector2(110f, -70f);
			int shivs = player.PlayerCombatState?.Hand.Cards.Count(Joker.IsShot) ?? 0;
			view.Set(shivs);
			view.Tick((float)delta);
		});
	}
}

/// <summary>在伤害结算入口触发一次命中特效。0.113 的攻击直接传入 DamageTargetData，旧版传入 Creature。</summary>
[HarmonyPatch]
public static class DamagePatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
		.Where(m => m.Name == nameof(CreatureCmd.Damage) && m.GetParameters() is { Length: >= 6 } p
			&& p[2].ParameterType == typeof(decimal) && p.Any(a => a.Name == "cardSource")
			&& (p[1].ParameterType == typeof(IEnumerable<Creature>) || p[1].ParameterType == TargetDataSequence))
		.OrderByDescending(m => m.GetParameters()[1].ParameterType == TargetDataSequence)
		.ThenByDescending(m => m.GetParameters().Length).Take(1);

	private static readonly Type? TargetData = typeof(Creature).Assembly.GetType("MegaCrit.Sts2.Core.Entities.Creatures.DamageTargetData");
	private static readonly Type? TargetDataSequence = TargetData == null ? null : typeof(IEnumerable<>).MakeGenericType(TargetData);
	private static readonly FieldInfo? TargetCreature = TargetData?.GetField("target");

	private static IEnumerable<Creature>? Creatures(object? targetList) =>
		targetList is IEnumerable<Creature> creatures ? creatures
			: targetList is System.Collections.IEnumerable data && TargetCreature is FieldInfo target
				? data.Cast<object>().Select(d => target.GetValue(d)).OfType<Creature>().ToArray()
				: null;

	public static void Prefix(object? __1, decimal amount, CardModel? cardSource)
	{
		ModEntry.Safe("Damage", () =>
		{
			if (cardSource == null || !Joker.OwnsCard(cardSource))
			{
				return;
			}
			IEnumerable<Creature>? targets = Creatures(__1);
			if (targets == null)
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
				if (hit.Count == 1 && Joker.FxLayer() is Godot.Control b5 && Batch5Fx.Hit(b5, cardSource.GetType().Name,
					new Batch5Fx.NCreatureView(Joker.PosePoint(joker, ReadyFx.GloveCuff), Joker.Chest(joker),
						(Joker.PosePoint(joker, ReadyFx.Feet) - Joker.PosePoint(joker, new Godot.Vector2(815f, 120f))).Length(), Joker.Facing(joker)),
					hit[0].VfxSpawnPosition, (hit[0].VfxSpawnPosition - hit[0].GetTopOfHitbox()).Length(), d => JokerFx.DelayNumber(hit[0], d)))
				{
					return;
				}
				if (hit.Count == 1 && Joker.FxLayer() is Godot.Control b6 && cardSource.GetType().Name is "Suppress" or "TheHunt")
				{
					// 0.9.49 审阅第六批第三版：压制「三重锁压」（Vfx/SuppressFx.cs）、狩猎「夺宝」（Vfx/HuntFx.cs）
					NCreature tn = hit[0];
					if (cardSource.GetType().Name == "Suppress")
						SuppressFx.Play(b6, Joker.FxLayer(back: true), tn.VfxSpawnPosition, tn.GetTopOfHitbox(), tn.Hitbox.GetGlobalRect().Size.X, Joker.Facing(joker), SkillPlay.Tint(tn));
					else
						HuntFx.Hit(b6, tn.VfxSpawnPosition, (tn.VfxSpawnPosition - tn.GetTopOfHitbox()).Length(), Joker.Facing(joker),
							Joker.PosePoint(joker, ReadyFx.GloveCuff), Joker.Chest(joker),
							(Joker.PosePoint(joker, ReadyFx.Feet) - Joker.PosePoint(joker, new Godot.Vector2(815f, 120f))).Length());
					return;
				}
				if (cardSource.GetType().Name == "EchoingSlash")
				{
					// 回响斩击：一波一道横切全体的长刀光；第二波起是回响（推迟一拍，数字也一起推迟）
					float delay = Joker.FxLayer() is not Godot.Control el ? 0f : Batch5Fx.EchoWave(el, hit.Select(n => (n.VfxSpawnPosition, n.GetTopOfHitbox(), n.Hitbox.GetGlobalRect().Size.X)).ToList(), Joker.Facing(joker));
					if (delay > 0f) foreach (NCreature n in hit) JokerFx.DelayNumber(n, delay);
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
			// 打击确认感：顿帧 + 定向震屏 + 受击白闪 / 击退 + 重击 / 击杀的全屏冲击帧（Vfx/Impact.cs）；数字推迟时一起推迟（打到的那一刻才有冲击）
			(Weight floor, bool calm) = Hits.FloorOf(target);
			bool finale = result.WasTargetKilled && target.CombatState?.Enemies.All(e => e.IsDead) == true;
			var grade = Impact.Grade(floor, result.UnblockedDamage, target.CurrentHp + result.UnblockedDamage, result.WasTargetKilled, finale, calm);
			void Land()
			{
				if (!Godot.GodotObject.IsInstanceValid(node) || !Godot.GodotObject.IsInstanceValid(layer)) return;
				Popups.Damage(layer, node.GetInstanceId(), node.VfxSpawnPosition, amount, gun);
				if (!SinfulPlay.Active)
					Impact.Confirm(layer, node.Visuals, node.VfxSpawnPosition, new Godot.Vector2(1f, 0.12f), grade);
			}
			if (wait > 0f) Fx.After(layer, wait, Land);
			else Land();
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
			if (AfterimageTickPatch.Depth > 0)
			{
				return;                                         // 余像每张牌的 1 点格挡：只有淡淡的残影（AfterimageTickPatch），不立盾
			}
			if (amount > 0m && cardPlay != null && Joker.OwnsCard(cardPlay.Card) && creature.Player is { } p && Joker.Is(p)
				&& NCombatRoom.Instance?.GetCreatureNode(creature) is NCreature node)
			{
				// 用户 2026-11-13：有专属特效的牌（后空翻、生存者……）再叠一层通用蜂巢盾，两种特效打架 → 只放专属的。
				// 模糊例外：它的专属特效就是配着蜂巢盾做的。
				string name = cardPlay.Card.GetType().Name;
				if (CardSkillFx.Cards.ContainsKey(name) && name != "Blur")
				{
					return;
				}
				JokerFx.OnBlock(node, (int)amount);
			}
		});
	}
}

/// <summary>Joker 获得格挡时原版的蓝色盾牌（vfx/vfx_block）不要：有蜂巢盾或者牌的专属特效（用户 2026-11-13：几层特效叠在一起冲突）。</summary>
[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreatureCenter))]
public static class BlockVfxPatch
{
	public static bool Prefix(Creature target, string path)
	{
		bool skip = false;
		ModEntry.Safe("BlockVfx", () => skip = path == "vfx/vfx_block" && target != null && target.IsPlayer && target.Player is { } p && Joker.Is(p));
		return !skip;
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
/// Joker 的腐蚀波：先放咒怨卷浪（Vfx/CorrosiveFx.cs），把原版这一步（上毒）推迟到浪拍中敌人的那一刻再跑（用户 2026-11-18：挂毒的确认感要和拍击同步、不要滞后）。
/// 抽牌是一张一张等着跑的，所以连抽时一道浪接一道浪。原版的绿色毒气藏掉（CorrosiveGasPatch）。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Models.Powers.CorrosiveWavePower), nameof(MegaCrit.Sts2.Core.Models.Powers.CorrosiveWavePower.AfterCardDrawn))]
public static class CorrosiveDrawPatch
{
	private static bool _bypass;
	private static ulong _lastMs;
	private static int _chain;

	public static bool Prefix(MegaCrit.Sts2.Core.Models.Powers.CorrosiveWavePower __instance, MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw, ref System.Threading.Tasks.Task __result)
	{
		if (_bypass)
		{
			return true;
		}
		float wait = 0f;
		Godot.Node? anchor = null;
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
			// 用户 2026-11-18：一次抽好几张只放一道完整的浪；后面的每一张在浪尾上变成一下快速的「咬蚀」，每张仍然各挂一次毒
			//（抽牌是一张一张等着跑的：上一张挂完毒才轮到下一张，所以离上一张结束不到 0.5s 就算同一串连抽）
			ulong now = Stage.NowMs();
			bool chained = _lastMs != 0 && now < _lastMs + 500;
			_chain = chained ? _chain + 1 : 0;
			if (_chain == 0)
				wait = CorrosiveFx.OnDraw(layer, Joker.PosePoint(joker, ReadyFx.Feet), Joker.Facing(joker), targets, true);
			else
				wait = CorrosiveFx.Erode(layer, Joker.Facing(joker), targets, _chain);
			_lastMs = now + (ulong)(wait * 1000f);   // 这一张挂毒的时刻
			anchor = layer;
		});
		if (wait <= 0f || anchor == null)
		{
			return true;
		}
		__result = Delayed(__instance, choiceContext, card, fromHandDraw, wait, anchor);
		return false;
	}

	private static async System.Threading.Tasks.Task Delayed(MegaCrit.Sts2.Core.Models.Powers.CorrosiveWavePower power, MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext ctx, CardModel card, bool fromHandDraw, float wait, Godot.Node anchor)
	{
		if (Godot.GodotObject.IsInstanceValid(anchor) && anchor.GetTree() is Godot.SceneTree tree)
		{
			await anchor.ToSignal(tree.CreateTimer(wait), Godot.SceneTreeTimer.SignalName.Timeout);
		}
		System.Threading.Tasks.Task run;
		_bypass = true;
		try
		{
			run = power.AfterCardDrawn(ctx, card, fromHandDraw);
		}
		finally
		{
			_bypass = false;
		}
		await run;
	}
}

/// <summary>
/// 回响斩击（用户 2026-11-17 审阅：会卡住局内人物的动作，特效也没做）：原版不走 AttackCommand，不触发攻击动作 → Joker 一直停在举刀的架势上。
/// 出牌时自己挥一刀（Aim.PlayAttack），把原版结算推迟到出刀那一帧（约 0.14s）再跑，伤害和刀光对齐；每波的特效在 DamagePatch → Batch5Fx.EchoWave。
/// </summary>
[HarmonyPatch]
public static class EchoingSlashPatch
{
	private static readonly MethodInfo? OnPlay = AccessTools.Method("MegaCrit.Sts2.Core.Models.Cards.EchoingSlash:OnPlay");
	private static bool _bypass;

	public static IEnumerable<MethodBase> TargetMethods()
	{
		if (OnPlay != null) yield return OnPlay;
	}

	public static bool Prefix(CardModel __instance, object[] __args, ref System.Threading.Tasks.Task __result)
	{
		if (_bypass || !Joker.OwnsCard(__instance) || Joker.Node(__instance.Owner) is not NCreature node || OnPlay == null)
		{
			return true;
		}
		ModEntry.Safe("EchoingSlash", () =>
		{
			Batch5Fx.BeginEcho();
			Aim.PlayAttack(node);
		});
		__result = Delayed(__instance, __args, node);
		return false;
	}

	private static async System.Threading.Tasks.Task Delayed(CardModel card, object[] args, NCreature node)
	{
		if (Godot.GodotObject.IsInstanceValid(node) && node.GetTree() is Godot.SceneTree tree)
		{
			await node.ToSignal(tree.CreateTimer(0.14), Godot.SceneTreeTimer.SignalName.Timeout);
		}
		System.Threading.Tasks.Task run;
		_bypass = true;
		try
		{
			run = (System.Threading.Tasks.Task)OnPlay!.Invoke(card, args)!;
		}
		finally
		{
			_bypass = false;
		}
		await run;
	}
}

/// <summary>狩猎斩杀 → 额外一次卡牌奖励（CombatRoom.AddExtraReward）：世界褪灰，猎物身上抽出一张预告信、被他抢进手里（HuntFx.Reward）。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Rooms.CombatRoom), nameof(MegaCrit.Sts2.Core.Rooms.CombatRoom.AddExtraReward))]
public static class HuntRewardPatch
{
	public static void Postfix(MegaCrit.Sts2.Core.Entities.Players.Player player)
	{
		ModEntry.Safe("HuntReward", () =>
		{
			if (Joker.Is(player) && MegaCrit.Sts2.Core.Context.LocalContext.IsMe(player) && Joker.FxLayer() is Godot.Control layer)
			{
				HuntFx.Reward(layer);
			}
		});
	}
}

/// <summary>紧勒：StranglePower 每次结算（这回合 Joker 每打一张牌）前会 Flash 一下 → 在被勒的敌人身上勒紧一次（Vfx/StrangleFx.cs）。</summary>
[HarmonyPatch(typeof(PowerModel), "Flash")]
public static class StrangleFlashPatch
{
	public static void Postfix(PowerModel __instance)
	{
		ModEntry.Safe("StrangleFlash", () =>
		{
			if (__instance is not MegaCrit.Sts2.Core.Models.Powers.StranglePower || __instance.Applier?.Player is not { } p || !Joker.Is(p)
				|| Joker.FxLayer() is not Godot.Control layer || NCombatRoom.Instance?.GetCreatureNode(__instance.Owner) is not NCreature n)
			{
				return;
			}
			float face = Joker.Node(p) is NCreature jn ? Joker.Facing(jn) : 1f;
			float tall = Joker.Node(p) is NCreature jn2 ? (Joker.PosePoint(jn2, ReadyFx.Feet) - Joker.PosePoint(jn2, new Godot.Vector2(815f, 120f))).Length() : 600f;
			StrangleFx.Cinch(layer, Joker.FxLayer(back: true), n.VfxSpawnPosition, n.GetTopOfHitbox(), n.Hitbox.GetGlobalRect().Size.X, SkillPlay.Tint(n), tall, face);
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

/// <summary>
/// 选卡界面「夺宝」（Vfx/RewardFx.cs，用户 2026-11-19：参考 P3 mod，有特色且不拖沓）：每次打开 / 重抽接管原版的滑入补间，
/// 牌从右边甩进来、背后 P5 斜切色带、左上角 TREASURE 标签、悬停红底板。牌一开始就看得见、点得到。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen), nameof(MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen.RefreshOptions))]
public static class RewardOpenPatch
{
	public static void Postfix(MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen __instance)
	{
		ModEntry.Safe("RewardOpen", () =>
		{
			if (!Joker.LocalRun || Traverse.Create(__instance).Field("_cardRow").GetValue() is not Godot.Control row)
			{
				return;
			}
			var holders = RewardHolders(row);
			if (holders.Count == 0)
			{
				return;
			}
			if (Traverse.Create(__instance).Field("_cardTween").GetValue() is Godot.Tween tw && Godot.GodotObject.IsInstanceValid(tw))
			{
				tw.Kill();
			}
			// 原版的位置：一排居中，间距 350
			var finals = new List<Godot.Vector2>();
			for (int i = 0; i < holders.Count; i++)
			{
				finals.Add(Godot.Vector2.Left * (holders.Count - 1) * 350f * 0.5f + Godot.Vector2.Right * 350f * i);
			}
			bool first = __instance.GetNodeOrNull("P5RewardBackdrop") == null;
			RewardFx.Open(__instance, row, holders, finals, first, RectOf);
		});
	}

	internal static List<Godot.Control> RewardHolders(Godot.Control row) =>
		row.GetChildren().OfType<MegaCrit.Sts2.Core.Nodes.Cards.Holders.NGridCardHolder>().Where(h => !h.IsQueuedForDeletion()).Cast<Godot.Control>().ToList();

	internal static Godot.Rect2 RectOf(Godot.Control c) =>
		c is MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder h && h.Hitbox != null && Godot.GodotObject.IsInstanceValid(h.Hitbox) ? h.Hitbox.GetGlobalRect() : c.GetGlobalRect();
}

/// <summary>选卡界面选中一张：「偷到手」（RewardFx.Pick）。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen), "SelectCard")]
public static class RewardPickPatch
{
	public static void Prefix(MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardSelectionScreen __instance, MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder cardHolder)
	{
		ModEntry.Safe("RewardPick", () =>
		{
			if (!Joker.LocalRun || Traverse.Create(__instance).Field("_cardRow").GetValue() is not Godot.Control row)
			{
				return;
			}
			RewardFx.Pick(__instance, row, RewardOpenPatch.RewardHolders(row), cardHolder, RewardOpenPatch.RectOf);
		});
	}
}
