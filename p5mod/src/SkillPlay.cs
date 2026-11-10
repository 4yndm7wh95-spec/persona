using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;

namespace P5Joker;

/// <summary>
/// 技能 / 能力牌的出牌特效（用户 2026-11-10：还只有人物动作的牌一次做完）。JokerFx.OnPlay 里没有专门特效的牌都走这里：
/// 按牌名查表，拼 Vfx/SkillFx.cs 的积木。格挡不在这里——所有获得格挡的牌统一走 GainBlockPatch → JokerFx.OnBlock（蜂巢护盾）。
/// 时刻：召唤亚森的牌（扶面具、罕见及以上）对到亚森出手（Aim.HitFor）；其余对到 Joker 施法动作里「出手」的那一帧（Moment）。
/// </summary>
public static class SkillPlay
{
	private enum Kind
	{
		Draw, DrawDiscard, Discard, Gamble, Storm, Load, DiscardLoad, Surge, SurgeDraw,
		BuffBlue, BuffRed, BuffCurse, FormCurse, FormVeil, Veil, Ghosts, GhostsBuff, StepAway,
		Plan, PlanOne, Nightmare, Debuff, Shatter, Mark, Wail, TimeSlow, LoadBuff,
	}

	private static readonly Dictionary<string, Kind> Table = new()
	{
		// 过牌 / 弃牌 / 能量
		["Acrobatics"] = Kind.DrawDiscard, ["Prepared"] = Kind.DrawDiscard, ["Expertise"] = Kind.Draw, ["Reflex"] = Kind.Draw,
		["Backflip"] = Kind.Draw, ["EscapePlan"] = Kind.Draw, ["Survivor"] = Kind.Discard, ["CalculatedGamble"] = Kind.Gamble,
		["Tactician"] = Kind.Surge, ["Adrenaline"] = Kind.SurgeDraw,
		// 生成小刀 = 装弹
		["BladeDance"] = Kind.Load, ["CloakAndDagger"] = Kind.Load, ["BladeOfInk"] = Kind.Load, ["UpMySleeve"] = Kind.Load,
		["BladeSymphony"] = Kind.Load, ["HiddenDaggers"] = Kind.DiscardLoad, ["StormOfSteel"] = Kind.Storm,
		["InfiniteBlades"] = Kind.LoadBuff, ["PhantomBlades"] = Kind.LoadBuff,
		// 增益
		["Footwork"] = Kind.BuffBlue, ["Anticipate"] = Kind.BuffBlue, ["Sidestep"] = Kind.BuffBlue, ["Sneaky"] = Kind.BuffBlue,
		["Abrasive"] = Kind.BuffBlue, ["Fade"] = Kind.BuffBlue,
		["Accuracy"] = Kind.BuffRed, ["Speedster"] = Kind.BuffRed, ["Tracking"] = Kind.BuffRed, ["Burst"] = Kind.BuffRed,
		["Envenom"] = Kind.BuffCurse, ["Accelerant"] = Kind.BuffCurse, ["NoxiousFumes"] = Kind.BuffCurse, ["Concoct"] = Kind.BuffCurse,
		["SerpentForm"] = Kind.FormCurse, ["WraithForm"] = Kind.FormVeil,
		// 潜行
		["Shadowmeld"] = Kind.Veil, ["Blur"] = Kind.Ghosts, ["Afterimage"] = Kind.GhostsBuff, ["ShadowStep"] = Kind.StepAway,
		// 谋划
		["MasterPlanner"] = Kind.Plan, ["WellLaidPlans"] = Kind.Plan, ["ToolsOfTheTrade"] = Kind.Plan, ["HandTrick"] = Kind.PlanOne,
		["Nightmare"] = Kind.Nightmare,
		// 压在敌人身上的
		["Malaise"] = Kind.Debuff, ["LegSweep"] = Kind.Debuff, ["Expose"] = Kind.Shatter, ["Flanking"] = Kind.Mark, ["KnifeTrap"] = Kind.Mark,
		["PiercingWail"] = Kind.Wail,
		["BulletTime"] = Kind.TimeSlow,
	};

	/// <summary>Joker 的这些牌打出后几秒内，原版自带的全屏 / 烟雾特效不显示（换成我们的）。</summary>
	private static readonly HashSet<string> HidesVanilla = new() { "Adrenaline", "Expose", "Nightmare", "Haze" };
	private static ulong _hideUntil;

	public static bool HidingVanilla => Time.GetTicksMsec() < _hideUntil;

	/// <summary>施法动作里「出手」的那一帧离现在还有几秒（tools/p5/build_rig_p5.py 的帧表）。</summary>
	private static float Moment(NCreature joker)
	{
		string? cur = joker.Visuals?.SpineBody?.TryGetAnimationState() is { } st ? Aim.Current(st) : null;
		return cur switch
		{
			"cast_glove" => 0.25f,                 // idle .02 → 伸手 .21 → 扯（glove_a）
			"cast_glove_from_ready" => 0.05f,
			"cast_guard" => 0.08f,
			"cast_guard_from_ready" => 0.03f,
			"cast" => 0.4f,                        // 扶面具 → 手甩出去指向前方（summon_command_a）
			"cast_from_mask" => 0.23f,
			"cast_wave" => 0.08f,                  // 挥手：手甩出去（summon_command_a）
			"cast_wave_from_ready" => 0.07f,
			"aim_in" or "aim_out" => 0.1f,
			_ => 0.12f,
		};
	}

	public static void OnPlay(CardModel card, NCreature joker, NCreature? target, Control layer, NCombatRoom room)
	{
		string name = card.GetType().Name;
		if (HidesVanilla.Contains(name))
		{
			_hideUntil = Time.GetTicksMsec() + 3000;
		}
		if (!Table.TryGetValue(name, out Kind kind))
		{
			return;
		}
		Control? back = Joker.FxLayer(back: true);
		bool summon = card.Type != CardType.Attack && Aim.StanceFor(card) == Stance.Mask && Tiers.For(card) >= Tier.Uncommon;
		float at = summon ? Aim.HitFor(card) : Moment(joker);
		int cards = Count(card);
		Fx.After(layer, at, () =>
		{
			if (!GodotObject.IsInstanceValid(joker))
			{
				return;
			}
			Vector2 feet = Joker.PosePoint(joker, ReadyFx.Feet), chest = Joker.Chest(joker), hand = Joker.PosePoint(joker, ReadyFx.GloveCuff);
			float tall = (feet - Joker.PosePoint(joker, new Vector2(815f, 120f))).Length(), face = Joker.Facing(joker);
			if (summon)
			{
				hand = chest;                                   // 召唤时手不在扯手套的位置：从胸口出
			}
			Action<Color> selfTint = c => { if (GodotObject.IsInstanceValid(joker) && joker.Visuals is { } v) v.Modulate = c; };
			Rect2 region = new(feet + new Vector2(-tall * 0.75f, -tall * 1.15f), new Vector2(tall * 1.5f, tall * 1.3f));
			Func<Node, (Node2D, Transform2D)?> clone = parent => Aim.CloneBody(joker, parent, "idle_loop");
			if (CardSkillFx.Cards.TryGetValue(name, out Action<SkillCtx>? perCard))
			{
				var enemies = new List<SkillFx.Victim>();
				if (joker.Entity?.CombatState is { } ecs)
				{
					foreach (Creature e in ecs.HittableEnemies)
					{
						if (room.GetCreatureNode(e) is NCreature en && GodotObject.IsInstanceValid(en))
						{
							enemies.Add(new SkillFx.Victim(en.GetBottomOfHitbox(), en.GetTopOfHitbox(), en.Hitbox.GetGlobalRect().Size.X, Tint(en)));
						}
					}
				}
				SkillFx.Victim? tv = target != null && GodotObject.IsInstanceValid(target)
					? new SkillFx.Victim(target.GetBottomOfHitbox(), target.GetTopOfHitbox(), target.Hitbox.GetGlobalRect().Size.X, Tint(target))
					: null;
				perCard(new SkillCtx(layer, back, feet, chest, hand, Joker.PosePoint(joker, ReadyFx.MaskFace), tall, face, selfTint, enemies, tv)
				{
					CopyBody = clone, Region = region,
				});
				return;
			}
			switch (kind)
			{
				case Kind.Draw:
					SkillFx.Draw(layer, hand, cards, face);
					break;
				case Kind.DrawDiscard:
					SkillFx.Draw(layer, hand, cards, face);
					Fx.After(layer, 0.45, () => SkillFx.Discard(layer, hand, 1, face));
					break;
				case Kind.Discard:
					SkillFx.Discard(layer, hand, 1, face);
					break;
				case Kind.Gamble:
					SkillFx.Discard(layer, hand, 4, face);
					Fx.After(layer, 0.4, () => SkillFx.Draw(layer, hand, 4, face));
					break;
				case Kind.Storm:
					SkillFx.Discard(layer, hand, 4, face);
					Fx.After(layer, 0.35, () => SkillFx.Load(layer, hand, 5, face));
					break;
				case Kind.Load:
					SkillFx.Load(layer, hand, cards, face);
					break;
				case Kind.DiscardLoad:
					SkillFx.Discard(layer, hand, 2, face);
					Fx.After(layer, 0.3, () => SkillFx.Load(layer, hand, cards, face));
					break;
				case Kind.LoadBuff:
					SkillFx.Buff(layer, back, feet, tall, SkillFx.Tone.Red);
					Fx.After(layer, 0.25, () => SkillFx.Load(layer, hand, 1, face));
					break;
				case Kind.Surge:
					SkillFx.Surge(layer, chest, tall);
					break;
				case Kind.SurgeDraw:
					SkillFx.Surge(layer, chest, tall);
					Fx.After(layer, 0.4, () => SkillFx.Draw(layer, chest, cards, face));
					break;
				case Kind.BuffBlue:
					SkillFx.Buff(layer, back, feet, tall, SkillFx.Tone.Blue);
					break;
				case Kind.BuffRed:
					SkillFx.Buff(layer, back, feet, tall, SkillFx.Tone.Red);
					break;
				case Kind.BuffCurse:
					SkillFx.Buff(layer, back, feet, tall, SkillFx.Tone.Curse);
					break;
				case Kind.FormCurse:
					SkillFx.Buff(layer, back, feet, tall, SkillFx.Tone.Curse, big: true);
					break;
				case Kind.FormVeil:
					SkillFx.Veil(layer, back, feet, tall, selfTint, big: true);
					break;
				case Kind.Veil:
					SkillFx.Veil(layer, back, feet, tall, selfTint);
					break;
				case Kind.Ghosts:
					if (back != null) SkillFx.Ghosts(back, clone, region, face);
					break;
				case Kind.GhostsBuff:
					if (back != null) SkillFx.Ghosts(back, clone, region, face);
					SkillFx.Buff(layer, back, feet, tall, SkillFx.Tone.Blue, sound: false);
					break;
				case Kind.StepAway:
					SkillFx.Discard(layer, hand, 3, face);
					if (back != null) Fx.After(layer, 0.25, () => SkillFx.Ghosts(back, clone, region, face, 3));
					break;
				case Kind.Plan:
					SkillFx.Plan(layer, back, chest, tall, 3);
					break;
				case Kind.PlanOne:
					SkillFx.Plan(layer, back, chest, tall, 1);
					break;
				case Kind.Nightmare:
					SkillFx.Plan(layer, back, chest, tall, 3, dark: true);
					break;
				case Kind.Debuff when target != null && GodotObject.IsInstanceValid(target):
					SkillFx.Debuff(layer, back, target.GetBottomOfHitbox(), target.GetTopOfHitbox(), target.Hitbox.GetGlobalRect().Size.X, Tint(target));
					break;
				case Kind.Shatter when target != null && GodotObject.IsInstanceValid(target):
					SkillFx.Shatter(layer, back, target.VfxSpawnPosition, target.GetBottomOfHitbox(), target.GetTopOfHitbox(),
						target.Hitbox.GetGlobalRect().Size.X, new Vector2(face, -0.15f), Tint(target));
					break;
				case Kind.Mark when target != null && GodotObject.IsInstanceValid(target):
				{
					Vector2 size = target.Hitbox.GetGlobalRect().Size;
					SkillFx.Mark(layer, target.VfxSpawnPosition, size.X, size.Y);
					break;
				}
				case Kind.Wail:
				{
					var victims = new List<SkillFx.Victim>();
					if (joker.Entity?.CombatState is { } cs)
					{
						foreach (Creature c in cs.HittableEnemies)
						{
							if (room.GetCreatureNode(c) is NCreature n)
							{
								victims.Add(new SkillFx.Victim(n.GetBottomOfHitbox(), n.GetTopOfHitbox(), n.Hitbox.GetGlobalRect().Size.X, Tint(n)));
							}
						}
					}
					SkillFx.Wail(layer, back, Joker.PosePoint(joker, ReadyFx.MaskFace), victims);
					break;
				}
				case Kind.TimeSlow:
					SkillFx.TimeSlow(layer, chest, tall);
					break;
			}
		});
	}

	/// <summary>这张牌抽几张 / 生成几张（DynamicVars.Cards；没有就 2）。</summary>
	private static int Count(CardModel card)
	{
		int n = 2;
		ModEntry.Safe("SkillPlay.Count", () => n = Mathf.Clamp(card.DynamicVars.Cards.IntValue, 1, 5));
		return n;
	}

	private static Action<Color> Tint(NCreature n) => c =>
	{
		if (GodotObject.IsInstanceValid(n) && n.Visuals is { } v)
		{
			v.Modulate = c;
		}
	};
}

/// <summary>原版肾上腺素的全屏特效、暴露时在玩家身上放的飞斩：Joker 打出时不放（换成 SkillFx）。</summary>
[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayFullScreenInCombat))]
public static class SkillVanillaFullScreenPatch
{
	public static bool Prefix(string path, Creature? spawner) =>
		!(SkillPlay.HidingVanilla && path == "vfx/vfx_adrenaline" && spawner?.Player is { } p && Joker.Is(p));
}

[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreatureCenter))]
public static class SkillVanillaCenterPatch
{
	public static bool Prefix(Creature target, string path) =>
		!(SkillPlay.HidingVanilla && path == "vfx/vfx_flying_slash" && target?.Player is { } p && Joker.Is(p));
}

/// <summary>原版夜魇 / 迷雾的彩色烟雾暗角、迷雾的绿色烟团、夜魇的手：Joker 打出时藏掉（照 CorrosiveGasPatch 的做法只设不可见）。</summary>
[HarmonyPatch]
public static class SkillVanillaSmokePatch
{
	public static IEnumerable<System.Reflection.MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(NSmokyVignetteVfx), nameof(NSmokyVignetteVfx.Create));
		yield return AccessTools.Method(typeof(NSmokePuffVfx), nameof(NSmokePuffVfx.Create));
		yield return AccessTools.Method(typeof(NNightmareHandsVfx), nameof(NNightmareHandsVfx.Create));
	}

	public static void Postfix(object? __result)
	{
		if (__result is CanvasItem c && SkillPlay.HidingVanilla)
		{
			c.Visible = false;
		}
	}
}
