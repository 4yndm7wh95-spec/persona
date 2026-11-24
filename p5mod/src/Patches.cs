using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.ValueProps;

namespace P5Joker;

/// <summary>Read the native hit count for the local effects.</summary>
[HarmonyPatch(typeof(AttackCommand), nameof(AttackCommand.Execute))]
public static class AttackHitVfxPatch
{
	private static readonly FieldInfo? HitCount = AccessTools.Field(typeof(AttackCommand), "_hitCount");

	public sealed class Swing(CardModel card, Batch5Fx.Sequence motion)
	{
		public CardModel Card { get; } = card;
		public Batch5Fx.Sequence Motion { get; } = motion;
		public bool Played;
	}

	private static readonly AsyncLocal<Swing?> CurrentSwing = new();

	internal static Batch5Fx.Sequence? SequenceFor(CardModel card) =>
		CurrentSwing.Value is { } swing && ReferenceEquals(swing.Card, card) ? swing.Motion : null;

	internal static bool AlreadySwung(Creature? owner)
	{
		if (CurrentSwing.Value is not { } swing || swing.Card.Owner.Creature != owner) return false;
		if (swing.Played) return true;
		swing.Played = true;
		return false;
	}

	public static void Prefix(AttackCommand __instance, out Swing? __state)
	{
		__state = CurrentSwing.Value;
		CurrentSwing.Value = null;
		ModEntry.Safe("AttackHitVfx", () =>
		{
			if (__instance.ModelSource is not CardModel card || !Joker.OwnsCard(card)) return;

			int count = HitCount?.GetValue(__instance) is int hits ? hits : 1;
			HitWeights.BeginAttack(card, count);
			string name = card.GetType().Name;
			if (Batch5Fx.OneSwing.ContainsKey(name))
			{
				CurrentSwing.Value = new Swing(card, new Batch5Fx.Sequence(name, count));
			}
			if (name == "DaggerSpray" && NCombatRoom.Instance is { } room)
			{
				var foes = new List<Batch3Fx.Foe>();
				foreach (Creature target in __instance.Attacker?.CombatState?.HittableEnemies ?? (IReadOnlyList<Creature>)Array.Empty<Creature>())
					if (room.GetCreatureNode(target) is NCreature n && Fx.Active(n))
						foes.Add(new Batch3Fx.Foe(n.VfxSpawnPosition, n.GetTopOfHitbox(), n.Hitbox.GetGlobalRect().Size.X));
				Batch3Fx.ArmSpray(foes);
			}
		});
	}
	public static void Finalizer(Swing? __state) => CurrentSwing.Value = __state;
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
			ulong id = __instance.GetInstanceId();
			if (PlayerField.GetValue(__instance) is not Player player || !Joker.Is(player))
			{
				if (Views.TryGetValue(id, out AmmoView? old) && Fx.Active(old.Root)) old.Root.Hide();
				return;
			}
			bool local = MegaCrit.Sts2.Core.Context.LocalContext.IsMe(player);
			if (!Views.TryGetValue(id, out AmmoView? view) || !Godot.GodotObject.IsInstanceValid(view.Root))
			{
				view = new AmmoView(__instance);
				Views[id] = view;
				TrackSelection(view, id);
				// A mid-run switch can keep the existing End Turn button and skip its _Ready.
				if (local && JokerCostume.ManagedSelection == true && NCombatRoom.Instance?.Ui?.EndTurnButton is { } button)
					JokerEndTurnSkinPatch.Postfix(button);
			}
			view.Root.Show();
			if (local)
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

	private static void TrackSelection(AmmoView view, ulong id)
	{
		Godot.SceneTree tree = view.Root.GetTree();
		bool managed = JokerCostume.ManagedSelection == true;
		if (managed) tree.ProcessFrame += DropIfUnselected;
		view.Root.TreeExiting += () =>
		{
			if (managed) tree.ProcessFrame -= DropIfUnselected;
			Views.Remove(id);
			if (Local == view) Local = null;
		};

		void DropIfUnselected()
		{
			if (JokerCostume.ManagedSelection != false) return;
			view.Root.Hide();
			view.Root.QueueFree();
		}
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
			if (SinfulPlay.IsPlaying(cardSource))
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
				if (hit.Count == 1 && Joker.FxLayer() is Godot.Control b5 && AttackHitVfxPatch.SequenceFor(cardSource) is { } sequence && sequence.Hit(b5,
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
	public static void Postfix(Creature target, DamageResult result, NDamageNumVfx? __result)
	{
		if (__result == null) return;
		ModEntry.Safe("DamageNumber", () =>
		{
			if (target.IsPlayer) return;
			if (NCombatRoom.Instance?.GetCreatureNode(target) is not NCreature node || Joker.FxLayer() is not Godot.Control layer)
				return;
			int damage = result.UnblockedDamage + (target.Monster is MegaCrit.Sts2.Core.Models.Monsters.Osty ? 0 : result.OverkillDamage);
			if (damage <= 0) return;

			ulong id = node.GetInstanceId();
			var at = node.VfxSpawnPosition;
			var top = node.GetTopOfHitbox();
			float width = node.Hitbox.GetGlobalRect().Size.X;
			var body = node.Visuals;
			bool killed = result.WasTargetKilled;
			OutbreakCue? outbreak = OutbreakVisualPatch.Current;
			if (PoisonDamage.Current.Value is { } poison && ReferenceEquals(poison.Owner, target)
				&& (outbreak != null || (poison.Applier?.Player is { } owner && Joker.Is(owner))))
			{
				void ShowPoison()
				{
					outbreak?.ReleaseHealth(target);
					Popups.Damage(layer, id, at, damage, false);
					PoisonFx.Tick(layer, at, top, width, Godot.GodotObject.IsInstanceValid(body) ? body : null, outbreak != null, killed);
					Grip.OnTick(id);
					DepthGrip.OnTick(id);
					Myriad.OnPoisonTick(layer, id, at, top, null);
				}
				if (outbreak != null) outbreak.Show(layer, at, ShowPoison);
				else ShowPoison();
				__result.Visible = false;
				return;
			}
			if (!Hits.Recent(target, out bool gun)) return;

			bool sinful = SinfulPlay.Active;
			float wait = sinful ? SinfulPlay.ImpactDelay(target) : JokerFx.DamageNumberDelay(node);
			(Weight floor, bool calm) = Hits.FloorOf(target);
			bool finale = killed && target.CombatState?.Enemies.All(e => e.IsDead) == true;
			var grade = Impact.Grade(floor, result.UnblockedDamage, target.CurrentHp + result.UnblockedDamage, killed, finale, calm);
			Fx.After(layer, wait, () =>
			{
				Popups.Damage(layer, id, at, damage, gun);
				if (!sinful)
					Impact.Confirm(layer, Godot.GodotObject.IsInstanceValid(body) ? body : null, at, new Godot.Vector2(1f, 0.12f), grade);
			});
			__result.Visible = false;
		});
	}
}

[HarmonyPatch(typeof(NDamageBlockedVfx), nameof(NDamageBlockedVfx.Create))]
public static class BlockPopupPatch
{
	public static void Postfix(Creature target, NDamageBlockedVfx? __result) => ModEntry.Safe("BlockPopup", () =>
	{
		if (target.IsPlayer && Joker.Is(target.Player) && NCombatRoom.Instance?.GetCreatureNode(target) is NCreature node
			&& Joker.FxLayer() is Godot.Control front)
		{
			Popups.Block(front, Joker.Chest(node), Joker.Facing(node));
			if (__result != null) __result.Visible = false;
		}
	});
}

[HarmonyPatch(typeof(NBlockSparkVfx), nameof(NBlockSparkVfx.Create))]
public static class BlockSparkPatch
{
	public static void Postfix(Creature target, NBlockSparkVfx? __result) => ModEntry.Safe("BlockSpark", () =>
	{
		if (__result != null && target.IsPlayer && Joker.Is(target.Player))
			__result.Visible = false;
	});
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
				// 模糊例外：它的专属特效就是配着蜂巢盾做的。扫腿也例外（用户 2026-11-19：防御特效不见了 ——
				// 它的专属特效只管挂虚弱，格挡本来就交给蜂巢盾，见 CardSkillFx2.LegSweep）。
				// 逃脱计划的专属特效只表现抽牌和烟弹，抽到技能获得格挡时也用蜂巢盾。
				string name = cardPlay.Card.GetType().Name;
				if (CardSkillFx.Cards.ContainsKey(name) && name is not ("Blur" or "LegSweep" or "EscapePlan"))
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

/// <summary>出牌那一刻（结算之前）：技能 / 能力牌 Joker 自己施法（Aim.PlayCast），放出牌特效；记下施法动作（Aim 改动画名时用）。</summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCardPlayed))]
public static class CardPlayedPatch
{
	public static void Prefix(CardPlay cardPlay)
	{
		ModEntry.Safe("CardPlayed", () =>
		{
			if (cardPlay.Card.Type == CardType.Status) return;
			if (Joker.OwnsCard(cardPlay.Card) && Joker.Node(cardPlay.Card.Owner) is NCreature node)
			{
				Aim.PendingCast[node.GetInstanceId()] = Casts.For(cardPlay.Card);
				if (cardPlay.Card.Type != CardType.Attack)
				{
					Aim.PlayCast(node, cardPlay.Card, OutbreakCue.Prepare(cardPlay));
				}
				JokerFx.OnPlay(cardPlay.Card, node, cardPlay.Target);
			}
		});
	}
}

[HarmonyPatch(typeof(NPowerAppliedVfx), nameof(NPowerAppliedVfx.Create))]
public static class PoisonApplyPatch
{
	public static void Postfix(PowerModel power, int amount, NPowerAppliedVfx? __result)
	{
		ModEntry.Safe("PoisonApply", () =>
		{
			var wave = CorrosiveDrawPatch.Current.Value;
			if (power is not PoisonPower || power.Owner.IsPlayer || (wave?.Owner ?? power.Applier)?.Player is not { } p || !Joker.Is(p)
				|| Joker.FxLayer() is not Godot.Control layer || NCombatRoom.Instance?.GetCreatureNode(power.Owner) is not NCreature n)
				return;

			if (wave != null)
			{
				var at = n.VfxSpawnPosition;
				var top = n.GetTopOfHitbox();
				float width = n.Hitbox.GetGlobalRect().Size.X;
				var body = n.Visuals;
				float delay = (float)Math.Max(0, wave.ImpactAt - HitStop.VisualSeconds());
				Fx.After(layer, delay, () => PoisonFx.CorrosiveImpact(layer, at, top, width,
					Godot.GodotObject.IsInstanceValid(body) ? body : null, amount));
			}
			else if (OutbreakVisualPatch.Current == null)
			{
				PoisonFx.Apply(layer, n.VfxSpawnPosition, n.GetTopOfHitbox(), n.Hitbox.GetGlobalRect().Size.X, n.Visuals, amount, outbreak: false);
			}
			if (__result != null && (wave != null || OutbreakVisualPatch.Current != null))
				__result.Visible = false;
		});
	}
}

/// <summary>Draw and poison settle normally. Schedule the wave and bite visuals on their own timeline.</summary>
[HarmonyPatch(typeof(CorrosiveWavePower), nameof(CorrosiveWavePower.AfterCardDrawn))]
public static class CorrosiveDrawPatch
{
	public sealed class DrawBatch(Player player)
	{
		public Player Player { get; } = player;
		public int Played;
		public double FirstImpactAt;
	}

	public sealed record Cue(Creature Owner, double ImpactAt);
	internal static readonly AsyncLocal<DrawBatch?> Batch = new();
	internal static readonly AsyncLocal<Cue?> Current = new();

	public static void Prefix(CorrosiveWavePower __instance, CardModel card, out Cue? __state)
	{
		__state = Current.Value;
		ModEntry.Safe("CorrosiveDraw", () =>
		{
			if (card.Owner?.Creature != __instance.Owner || __instance.Owner.Player is not { } p || !Joker.Is(p)
				|| Joker.FxLayer() is not Godot.Control layer || NCombatRoom.Instance is not NCombatRoom room || Joker.Node(p) is not NCreature joker)
				return;
			var targets = new List<CorrosiveFx.Target>();
			foreach (Creature c in __instance.CombatState.HittableEnemies)
				if (room.GetCreatureNode(c) is NCreature n)
					targets.Add(new CorrosiveFx.Target(n.VfxSpawnPosition, n.GetTopOfHitbox(), n.Hitbox.GetGlobalRect().Size.X, n.Visuals));
			if (targets.Count == 0) return;

			var batch = Batch.Value is { } active && active.Player == p ? active : null;
			int index = batch?.Played ?? 0;
			double now = HitStop.VisualSeconds();
			float face = Joker.Facing(joker);
			double impact;
			if (batch == null || index == 0)
			{
				impact = now + CorrosiveFx.OnDraw(layer, Joker.PosePoint(joker, ReadyFx.Feet), face, targets);
				if (batch != null) batch.FirstImpactAt = impact;
			}
			else
			{
				float delay = (float)Math.Max(0, batch.FirstImpactAt - now + (index - 1) * 0.09);
				Fx.After(layer, delay, () => CorrosiveFx.Erode(layer, face, targets, index));
				impact = now + delay + CorrosiveFx.ErodeHit;
			}
			if (batch != null) batch.Played++;
			Current.Value = new Cue(__instance.Owner, impact);
		});
	}

	public static void Finalizer(Cue? __state) => Current.Value = __state;
}

[HarmonyPatch]
public static class CorrosiveDrawBatchPatch
{
	public static MethodBase TargetMethod()
	{
		Type[] args = { typeof(PlayerChoiceContext), typeof(decimal), typeof(Player), typeof(bool) };
		return AccessTools.Method(typeof(CardPileCmd), "DrawInternal", args)
			?? AccessTools.Method(typeof(CardPileCmd), nameof(CardPileCmd.Draw), args);
	}

	public static void Prefix(Player player, out (bool Started, CorrosiveDrawPatch.DrawBatch? Previous) __state)
	{
		__state = (true, CorrosiveDrawPatch.Batch.Value);
		CorrosiveDrawPatch.Batch.Value = new CorrosiveDrawPatch.DrawBatch(player);
	}

	public static void Finalizer((bool Started, CorrosiveDrawPatch.DrawBatch? Previous) __state)
	{
		if (__state.Started)
		{
			CorrosiveDrawPatch.Batch.Value = __state.Previous;
		}
	}
}

[HarmonyPatch]
public static class EchoingSlashPatch
{
	public static IEnumerable<MethodBase> TargetMethods()
	{
		if (AccessTools.Method("MegaCrit.Sts2.Core.Models.Cards.EchoingSlash:OnPlay") is { } method)
			yield return method;
	}

	public static void Prefix(CardModel __instance) => ModEntry.Safe("EchoingSlash", () =>
	{
		if (!Joker.OwnsCard(__instance) || Joker.Node(__instance.Owner) is not NCreature node) return;
		Batch5Fx.BeginEcho();
		Aim.PlayAttack(node);
	});
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
		if (__result != null && (CorrosiveDrawPatch.Current.Value != null || SnakeFx.FlaskActive))
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
[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.RefreshOptions))]
public static class RewardOpenPatch
{
	private static readonly FieldInfo? CardTween = AccessTools.Field(typeof(NCardRewardSelectionScreen), "_cardTween");
	private sealed record Presentation(bool Enabled);
	private static readonly ConditionalWeakTable<NCardRewardSelectionScreen, Presentation> Presentations = new();

	public static void Prefix(NCardRewardSelectionScreen __instance) => ModEntry.Safe("RewardRefresh", () =>
	{
		RewardFx.Recover(__instance);
	});

	public static void Postfix(NCardRewardSelectionScreen __instance, IReadOnlyList<CardCreationResult> options)
	{
		ModEntry.Safe("RewardOpen", () =>
		{
			// Rerolls share a screen and its skinned controls. Each new reward screen reads the setting once.
			if (!Joker.LocalRun || !Presentations.GetValue(__instance,
					static _ => new Presentation(P5Settings.Enabled(P5Settings.Option.Reward))).Enabled
				|| __instance.GetNodeOrNull<Godot.Control>("UI/CardRow") is not { } row
				|| CardTween?.GetValue(__instance) is not Godot.Tween tween || !Godot.GodotObject.IsInstanceValid(tween))
				return;

			var live = row.GetChildren().OfType<MegaCrit.Sts2.Core.Nodes.Cards.Holders.NGridCardHolder>()
				.Where(h => !h.IsQueuedForDeletion()).ToList();
			if (options.Count == 0 || live.Count != options.Count) return;

			var holders = new List<Godot.Control>(options.Count);
			var finals = new List<Godot.Vector2>(options.Count);
			for (int i = 0; i < options.Count; i++)
			{
				var holder = live.Find(h => ReferenceEquals(h.CardModel, options[i].Card));
				if (holder == null) return;
				live.Remove(holder);
				holders.Add(holder);
				finals.Add(Godot.Vector2.Right * (i - (options.Count - 1) * 0.5f) * 350f);
			}

			bool first = __instance.GetNodeOrNull("P5RewardBackdrop") == null;
			try
			{
				RewardFx.Open(__instance, row, holders, finals, first, RectOf, CardOf,
					GameCompat.DirectionalNavigation);
				tween.Kill();
			}
			catch
			{
				RewardFx.Recover(__instance);
				throw;
			}
			// 原版的横幅、跳过按钮也换成剪报风（Codex 素材）
			if (Traverse.Create(__instance).Field("_banner").GetValue() is Godot.Control banner && Traverse.Create(banner).Field("label").GetValue() is Godot.Label bl)
			{
				RewardFx.SkinBanner(banner, bl);
			}
			if (Traverse.Create(__instance).Field("_rewardAlternativesContainer").GetValue() is Godot.Control alts)
			{
				foreach (var btn in alts.GetChildren().OfType<MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardAlternativeButton>())
				{
					var b = btn;
					void Skin()
					{
						if (Traverse.Create(b).Field("_image").GetValue() is Godot.TextureRect img && Traverse.Create(b).Field("_label").GetValue() is Godot.Label lab)
						{
							RewardFx.SkinButton(b, img, lab);
						}
					}
					if (b.IsNodeReady()) Skin();
					else b.Ready += () => ModEntry.Safe("RewardSkipSkin", Skin);   // 加子节点被延迟时，等按钮自己 _Ready 设好文字再换
				}
			}
		});
	}

	private static object? CardOf(Godot.Control c) =>
		(c as MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder)?.CardModel;

	internal static Godot.Rect2 RectOf(Godot.Control c) =>
		c is MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder h && h.Hitbox != null && Godot.GodotObject.IsInstanceValid(h.Hitbox)
			? h.Hitbox.GetGlobalRect() : c.GetGlobalRect();
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), "SelectCard", typeof(NCardHolder))]
public static class RewardPickPatch
{
	private static readonly FieldInfo? Completion = AccessTools.Field(typeof(NCardRewardSelectionScreen), "_completionSource");
	private static readonly FieldInfo? Options = AccessTools.Field(typeof(NCardRewardSelectionScreen), "_options");

	public static void Prefix(NCardRewardSelectionScreen __instance, NCardHolder __0) => ModEntry.Safe("RewardPick", () =>
	{
		if (Joker.LocalRun && Fx.Active(__instance) && __instance.IsVisibleInTree()
			&& __instance.HasNode("P5RewardBackdrop")
			&& Completion?.GetValue(__instance) is TaskCompletionSource<int?> { Task.IsCompleted: false }
			&& Fx.Active(__0) && __0.GetParent() == __instance.GetNodeOrNull<Godot.Control>("UI/CardRow")
			&& Options?.GetValue(__instance) is IReadOnlyList<CardCreationResult> options
			&& options.Any(option => ReferenceEquals(option.Card, __0.CardModel)))
			RewardFx.Confirm(__instance, RewardOpenPatch.RectOf(__0));
	});
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.AfterOverlayHidden))]
public static class RewardHidePatch
{
	public static void Prefix(NCardRewardSelectionScreen __instance) =>
		ModEntry.Safe("RewardHide", () => RewardFx.Settle(__instance));
}

/// <summary>选卡界面跳过按钮换皮之后：悬停 / 手柄选中换红色纸板（原版是 HSV 着色器调亮，换皮时去掉了）。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardAlternativeButton), "OnFocus")]
public static class RewardSkipFocusPatch
{
	public static void Postfix(MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardAlternativeButton __instance) => ModEntry.Safe("RewardSkipFocus", () =>
	{
		if (Traverse.Create(__instance).Field("_image").GetValue() is Godot.TextureRect img) RewardFx.SkinButtonHover(img, true);
	});
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardAlternativeButton), "OnUnfocus")]
public static class RewardSkipUnfocusPatch
{
	public static void Postfix(MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardRewardAlternativeButton __instance) => ModEntry.Safe("RewardSkipUnfocus", () =>
	{
		if (Traverse.Create(__instance).Field("_image").GetValue() is Godot.TextureRect img) RewardFx.SkinButtonHover(img, false);
	});
}

/// <summary>角色头像（左上角顶栏 / 战绩 / 联机大厅 / 继续游戏 / 先古对话）：猎人的图换成 Codex 画的 Joker 头像（art/p5/ui/icons/joker_top_icon.png → icon_top）。</summary>
[HarmonyPatch(typeof(CharacterModel), "get_IconTexture")]
public static class JokerIconTexturePatch
{
	public static void Postfix(CharacterModel __instance, ref Godot.Texture2D __result)
	{
		if (__instance is MegaCrit.Sts2.Core.Models.Characters.Silent && JokerCostume.Selected
			&& Fx.Tex("icon_top") is Godot.Texture2D t) __result = t;
	}
}

[HarmonyPatch(typeof(CharacterModel), "get_IconOutlineTexture")]
public static class JokerIconOutlinePatch
{
	public static void Postfix(CharacterModel __instance, ref Godot.Texture2D __result)
	{
		if (__instance is MegaCrit.Sts2.Core.Models.Characters.Silent && JokerCostume.Selected
			&& Fx.Tex("icon_top_outline") is Godot.Texture2D t) __result = t;
	}
}

/// <summary>顶栏用 CharacterModel.Icon 实例化头像，挂入场景树可能延迟。先换头像根节点的贴图。</summary>
[HarmonyPatch(typeof(CharacterModel), "get_Icon")]
public static class JokerIconScenePatch
{
	public static void Postfix(CharacterModel __instance, Godot.Control __result) => ModEntry.Safe("JokerIconScene", () =>
	{
		if (__instance is MegaCrit.Sts2.Core.Models.Characters.Silent && JokerCostume.Selected && __result is Godot.TextureRect icon
			&& Fx.Tex("icon_top") is Godot.Texture2D portrait)
			icon.Texture = portrait;
	});
}

// The history icon fills its parent. The Joker texture must not grow the slot.
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.RunHistoryScreen.NRunHistoryPlayerIcon), "LoadRun")]
public static class JokerHistoryIconPatch
{
	private sealed record IconLayout(Godot.TextureRect.ExpandModeEnum Expand, Godot.TextureRect.StretchModeEnum Stretch);
	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Godot.TextureRect, IconLayout> Layouts = new();

	public static void Prefix(MegaCrit.Sts2.Core.Runs.RunHistoryPlayer __0, Godot.TextureRect ____icon) =>
		ModEntry.Safe("Joker history icon", () =>
	{
		if (__0.Character.Entry == "SILENT" && JokerCostume.Selected && Fx.Tex("icon_top") != null)
		{
			Layouts.GetValue(____icon, icon => new IconLayout(icon.ExpandMode, icon.StretchMode));
			____icon.ExpandMode = Godot.TextureRect.ExpandModeEnum.IgnoreSize;
			____icon.StretchMode = Godot.TextureRect.StretchModeEnum.KeepAspectCentered;
		}
		else if (Layouts.TryGetValue(____icon, out IconLayout? layout))
		{
			____icon.ExpandMode = layout.Expand;
			____icon.StretchMode = layout.Stretch;
			Layouts.Remove(____icon);
		}
	});
}

/// <summary>
/// 战斗开始：原版「战斗开始」横幅只藏画面（节点照常跑 —— 它播完负责生成第 1 回合的横幅），换成剪报纸带 + TAKE YOUR HEART!（Vfx/BattleUiFx.cs）。
/// </summary>
[HarmonyPatch(typeof(NCombatStartBanner), nameof(NCombatStartBanner._Ready))]
public static class JokerCombatStartBannerPatch
{
	public static void Postfix(NCombatStartBanner __instance) => ModEntry.Safe("JokerCombatStart", () =>
	{
		if (!Joker.LocalRun) return;
		foreach (Godot.Node c in __instance.GetChildren())
		{
			if (c is Godot.CanvasItem ci) ci.Visible = false;
		}
		BattleUiFx.BattleStart((Joker.FxLayer(back: true) ?? Joker.FxLayer()) ?? (Godot.Control)__instance);   // 画在人物后面
	});
}

/// <summary>我方回合横幅换成左上角的剪报纸条（原版的不再创建：返回 null，原版调用处 AddChildSafely 会忽略 null）。</summary>
[HarmonyPatch(typeof(NPlayerTurnBanner), nameof(NPlayerTurnBanner.Create))]
public static class JokerPlayerTurnBannerPatch
{
	public static void Postfix(int roundNumber, NPlayerTurnBanner? __result) => ModEntry.Safe("JokerTurnBanner", () =>
	{
		if (!Joker.LocalRun || (Joker.FxLayer(back: true) ?? Joker.FxLayer()) is not Godot.Control back) return;
			bool extra = MegaCrit.Sts2.Core.Combat.CombatManager.Instance?.PlayersTakingExtraTurn.Count > 0;
		BattleUiFx.TurnBanner(back, player: true, roundNumber, extra);
		if (__result != null) __result.Visible = false;
	});
}
[HarmonyPatch(typeof(NEnemyTurnBanner), nameof(NEnemyTurnBanner.Create))]
public static class JokerEnemyTurnBannerPatch
{
	public static void Postfix(NEnemyTurnBanner? __result) => ModEntry.Safe("JokerTurnBanner", () =>
	{
		if (!Joker.LocalRun || (Joker.FxLayer(back: true) ?? Joker.FxLayer()) is not Godot.Control back) return;
		BattleUiFx.TurnBanner(back, player: false, 0, false);
		if (__result != null) __result.Visible = false;
	});
}
/// <summary>能量球换皮（Vfx/HudSkin.cs）：只在 Joker 的能量球上显示，节点、逻辑都是原版的。</summary>
[HarmonyPatch(typeof(NEnergyCounter), nameof(NEnergyCounter._Ready))]
public static class JokerEnergySkinPatch
{
	private static readonly FieldInfo PlayerField = AccessTools.Field(typeof(NEnergyCounter), "_player");

	public static void Postfix(NEnergyCounter __instance) => ModEntry.Safe("JokerEnergySkin", () =>
	{
		var counter = __instance;
		HudSkin.ApplyEnergy(counter, () => PlayerField.GetValue(counter) is MegaCrit.Sts2.Core.Entities.Players.Player p && Joker.Is(p));
	});
}

/// <summary>结束回合按钮换皮（Vfx/HudSkin.cs）：按钮在角色加载前就建好了，是否显示由它每帧自己判断（本机玩家是 Joker）。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Combat.NEndTurnButton), nameof(MegaCrit.Sts2.Core.Nodes.Combat.NEndTurnButton._Ready))]
public static class JokerEndTurnSkinPatch
{
	public static void Postfix(MegaCrit.Sts2.Core.Nodes.Combat.NEndTurnButton __instance) => ModEntry.Safe("JokerEndTurnSkin", () =>
		HudSkin.ApplyEndTurn(__instance, static () => Joker.LocalRun));
}

/// <summary>
/// 商店里的 Joker 太大（用户 2026-11-19 实机）：原版商店场景 silent_merchant.tscn 里骨骼缩放 0.47 是按猎人的商店骨骼定的，
/// 我们的商店骨骼直接用战斗骨骼（stage_p5.py 的 silent_merchant_skel_data），画出来比原版角色大一圈 → 缩到 0.75 倍（脚底是原点，站位不变）。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantCharacter), nameof(MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantCharacter._Ready))]
public static class JokerMerchantScalePatch
{
	public const float Scale = 0.75f;

	public static void Postfix(MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantCharacter __instance) => ModEntry.Safe("JokerMerchantScale", () =>
	{
		if (!__instance.SceneFilePath.Contains("silent_merchant", System.StringComparison.OrdinalIgnoreCase)
			|| __instance.GetChildCount() == 0 || __instance.GetChild(0) is not Godot.Node2D sprite || sprite.HasMeta("p5_scaled")
			|| !(JokerCostume.OwnsBody(sprite) ?? JokerCostume.Selected))
		{
			return;
		}
		sprite.SetMeta("p5_scaled", true);
		sprite.Scale *= Scale;
	});
}
