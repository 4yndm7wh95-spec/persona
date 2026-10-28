using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>出牌时 Joker 播哪种施法动作（骨骼里的 cast 被换成对应版本，见 Aim.Rename）。</summary>
public enum Cast
{
	Summon,     // 手扶面具（原版 cast）
	Big,        // 撕面具（cast_big）
	Guard,      // 防御（cast_guard）
	Glove       // 扯手套（cast_glove）
}

/// <summary>一张牌的特效：命中时的近战 / 枪击样子、是否带咒怨、出牌时的演出、格挡时的样子、施法动作。</summary>
public sealed record CardDef(Blade? Blade = null, Shot? Shot = null, float Size = 1f, bool Curse = false,
	Action<CardCtx>? Play = null, int Guard = 0, Cast Cast = Cast.Summon);

public sealed class CardCtx
{
	public required NCreature Joker;
	public required CardModel Card;
	public required List<NCreature> Enemies;        // 场上可被攻击的敌人
	public required List<NCreature> Targets;        // 这张牌的目标（单体 = 选中的敌人，否则 = 全部敌人）
	public NCreature? Ally;                          // 指定友方的牌（调剂、渐隐）
	public NCreature One => Targets.Count > 0 ? Targets[0] : Joker;
	public Control? Front => P5Joker.Joker.FxLayer();
}

/// <summary>
/// 卡牌 → 特效（用户 2026-10-28：至少覆盖静默猎手一半的牌，尽量不套同一个模板；用人格面具的牌要华丽）。
/// 毒 = 亚森的咒怨（Eiha / Maeiha / Eigaon），小刀 = 枪，格挡 = 原版「BLOCK」字样，抽牌 / 弃牌 = 预告信，增益 / 减益 = P5 的 -kaja / -nda。
/// 键是卡牌类名（MegaCrit.Sts2.Core.Models.Cards）。没列出的牌：攻击牌用普通斩，其他只播施法动作。
/// </summary>
public static class CardFx
{
	private static readonly Color Green = new(0.4f, 1f, 0.55f), Blue = new(0.35f, 0.75f, 1f), Gold = new(1f, 0.82f, 0.3f),
		Purple = new(0.65f, 0.3f, 1f), Red = new(0.95f, 0.1f, 0.15f), Grey = new(0.45f, 0.45f, 0.5f), Ink = new(0.12f, 0.06f, 0.16f);

	private static Vector2[] Offsets(params float[] xy)
	{
		Vector2[] r = new Vector2[xy.Length / 2];
		for (int i = 0; i < r.Length; i++)
		{
			r[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
		}
		return r;
	}

	private static readonly Dictionary<string, CardDef> Table = new()
	{
		// ── 攻击：刀 ──
		["StrikeSilent"] = new(Blade.Cut),
		["Slice"] = new(Blade.Cut, Size: 0.8f),
		["Backstab"] = new(Blade.Cross, Size: 1.1f, Play: c =>
		{
			Tricks.Shadow(c.Joker, 0.6);
			Tricks.Afterimages(c.Joker, Offsets(60, 0, 140, 0, 220, 0), Ink, 0.4);
		}),
		["Dash"] = new(Blade.Thrust, Size: 1.15f, Play: c => Tricks.Afterimages(c.Joker, Offsets(-160, 0, -110, 0, -60, 0), Red, 0.35, 0.02f)),
		["PreciseCut"] = new(Blade.Thrust, Size: 0.9f, Play: c => Tricks.Reticle(c.One)),
		["Predator"] = new(Blade.Cross, Size: 1.2f, Play: c =>
		{
			Tricks.Reticle(c.One, weak: true);
			Tricks.CallingCards(c.Joker, 1, c.One.VfxSpawnPosition);
		}),
		["Strangle"] = new(Blade.Blunt, Play: c => Tricks.Chains(c.One, 4)),
		["Pounce"] = new(Blade.Heavy, Play: c => Tricks.Afterimages(c.Joker, Offsets(-150, -40, -100, -110, -50, -80), Red, 0.4)),
		["SuckerPunch"] = new(Blade.Blunt, Play: c => Spells.Nda(c.One, Red, 0.25f)),
		["Suppress"] = new(Blade.Thrust, Play: c => Spells.Nda(c.One, Red, 0.2f)),
		["Neutralize"] = new(Blade.Cut, Size: 0.9f, Play: c => Spells.Nda(c.One, Red, 0.2f)),
		["PoisonedStab"] = new(Blade.Thrust, Curse: true),
		["MementoMori"] = new(Blade.Cross, Curse: true, Play: c => Tricks.Shadow(c.Joker, 0.6)),
		["Finisher"] = new(Blade.Flurry),
		["EchoingSlash"] = new(Blade.Echo),
		["FlickFlack"] = new(Blade.Echo, Play: c => Tricks.Afterimages(c.Joker, Offsets(-40, -120, 30, -160, 90, -90), Blue, 0.4)),
		["LeadingStrike"] = new(Blade.Cut, Play: c => Fx.After(c.Joker, 0.3, () => Tricks.Reload(c.Joker, 1))),
		["Assassinate"] = new(Blade.Heavy, Size: 1.3f, Play: c =>
		{
			Tricks.CallingCards(c.Joker, 1, c.One.VfxSpawnPosition);
			Tricks.Reticle(c.One, weak: true, delay: 0.05f);
		}),
		["TheHunt"] = new(Blade.Heavy, Size: 1.2f, Play: c =>
		{
			Tricks.Shadow(c.Joker, 0.7);
			Tricks.Reticle(c.One);
		}),
		["Murder"] = new(Blade.Heavy, Size: 1.4f, Curse: true, Play: c => Persona.Summon(c.Joker, "Eigaon", () => Spells.Eigaon(c.One), big: true)),
		["GrandFinale"] = new(Blade.Echo, Play: c => Persona.Summon(c.Joker, "Maeigaon", () => Spells.Maeiha(c.Enemies, 1.3f), big: true)),
		// ── 攻击：枪 ──
		["Skewer"] = new(Shot: Shot.Rapid),
		["Pinpoint"] = new(Shot: Shot.Single, Play: c => Tricks.Reticle(c.One)),
		["Ricochet"] = new(Shot: Shot.Ricochet),
		["DaggerThrow"] = new(Shot: Shot.Single, Play: c => Fx.After(c.Joker, 0.35, () => Tricks.CallingCards(c.Joker, 1))),
		["DaggerSpray"] = new(Shot: Shot.Rapid),
		["Flechettes"] = new(Shot: Shot.Pellet, Cast: Cast.Glove),
		// ── 格挡 ──
		["DefendSilent"] = new(Guard: 0, Cast: Cast.Guard),
		["Deflect"] = new(Guard: 1, Cast: Cast.Guard),
		["DodgeAndRoll"] = new(Guard: 0, Cast: Cast.Guard, Play: c => Tricks.Afterimages(c.Joker, Offsets(-60, 0, -120, 12, -175, 0), Blue)),
		["Backflip"] = new(Guard: 0, Cast: Cast.Guard, Play: c =>
		{
			Tricks.Afterimages(c.Joker, Offsets(30, -60, -20, -150, -90, -90), Blue, 0.4);
			Tricks.CallingCards(c.Joker, 2);
		}),
		["EscapePlan"] = new(Guard: 0, Cast: Cast.Guard, Play: c => Tricks.Smoke(c.Joker, Grey)),
		["Blur"] = new(Guard: 0, Cast: Cast.Guard, Play: c => Tricks.Afterimages(c.Joker, Offsets(-26, 0, 26, 0, -12, -6, 14, 4), Blue, 0.6, 0.0f)),
		["Survivor"] = new(Guard: 2, Cast: Cast.Guard, Play: c => Tricks.CallingCards(c.Joker, 1)),
		["CloakAndDagger"] = new(Guard: 0, Cast: Cast.Guard, Play: c =>
		{
			Tricks.CoatSwirl(c.Joker);
			Fx.After(c.Joker, 0.3, () => Tricks.Reload(c.Joker, 1));
		}),
		["HandTrick"] = new(Guard: 0, Cast: Cast.Glove, Play: c => Tricks.CallingCards(c.Joker, 3, spread: 1.1f)),
		["Untouchable"] = new(Guard: 2, Cast: Cast.Guard, Play: c => Tricks.Afterimages(c.Joker, Offsets(-90, 0, 90, 0, -45, -10, 45, -10), Purple, 0.5, 0.0f)),
		["LegSweep"] = new(Guard: 0, Cast: Cast.Guard, Play: c =>
		{
			Slash.Play(c.Joker, c.Targets.Take(1).ToList(), Blade.Low);
			Spells.Nda(c.One, Red, 0.15f);
		}),
		["Mirage"] = new(Guard: 0, Cast: Cast.Summon, Play: c => Persona.Quick(c.Joker, "Mirage", () =>
		{
			Tricks.Afterimages(c.Joker, Offsets(-70, 0, 70, 0), Purple, 0.6, 0.0f);
			Spells.Maeiha(c.Enemies, 0.5f);
		})),
		// ── 抽牌 / 弃牌：预告信 ──
		["Acrobatics"] = new(Cast: Cast.Glove, Play: c =>
		{
			Tricks.Afterimages(c.Joker, Offsets(0, -90, 0, -170), Blue, 0.35);
			Tricks.CallingCards(c.Joker, 3, spread: 0.9f);
		}),
		["Prepared"] = new(Cast: Cast.Glove, Play: c => Tricks.CallingCards(c.Joker, 2)),
		["Expertise"] = new(Cast: Cast.Glove, Play: c => Tricks.CallingCards(c.Joker, 5, spread: 1.4f)),
		["CalculatedGamble"] = new(Cast: Cast.Glove, Play: c => Tricks.CallingCards(c.Joker, 6, spread: 2.2f)),
		["Reflex"] = new(Cast: Cast.Glove, Play: c => Tricks.CallingCards(c.Joker, 1)),
		["WellLaidPlans"] = new(Cast: Cast.Glove, Play: c => Tricks.CallingCards(c.Joker, 3, spread: 0.6f)),
		// ── 能量 / 时间 ──
		["Adrenaline"] = new(Cast: Cast.Summon, Play: c => Persona.Quick(c.Joker, "Sukukaja", () =>
		{
			Spells.Kaja(c.Joker, Green);
			Tricks.Afterimages(c.Joker, Offsets(-50, 0, -100, 0), Red, 0.4);
		})),
		["BulletTime"] = new(Cast: Cast.Glove, Play: c =>
		{
			if (c.Front is Control f)
			{
				Tricks.TimeStop(f);
			}
		}),
		["Nightmare"] = new(Cast: Cast.Summon, Play: c => Persona.Quick(c.Joker, "Nightmare", () =>
		{
			Tricks.Shadow(c.Joker, 1.0);
			Tricks.CallingCards(c.Joker, 3, spread: 0.7f);
		})),
		["Shadowmeld"] = new(Cast: Cast.Glove, Play: c => Tricks.Shadow(c.Joker, 1.0)),
		["ShadowStep"] = new(Cast: Cast.Glove, Play: c =>
		{
			Tricks.Shadow(c.Joker, 0.8);
			Tricks.Afterimages(c.Joker, Offsets(-80, 0, -150, 0), Ink, 0.5);
		}),
		["Sidestep"] = new(Cast: Cast.Guard, Play: c => Tricks.Afterimages(c.Joker, Offsets(0, 0, -70, 0), Blue, 0.35, 0.05f)),
		["Tactician"] = new(Cast: Cast.Glove, Play: c => Persona.Quick(c.Joker, "Concentrate", () => Spells.Kaja(c.Joker, Gold))),
		["Burst"] = new(Cast: Cast.Summon, Play: c => Persona.Quick(c.Joker, "Charge", () => Spells.Kaja(c.Joker, new Color(1f, 0.5f, 0.2f)))),
		// ── 装填（生成小刀 = 子弹） ──
		["BladeDance"] = new(Cast: Cast.Glove, Play: c => Tricks.Reload(c.Joker, 3)),
		["BladeOfInk"] = new(Cast: Cast.Glove, Play: c => Tricks.Reload(c.Joker, 2, 1)),
		["HiddenDaggers"] = new(Cast: Cast.Glove, Play: c =>
		{
			Tricks.CoatSwirl(c.Joker);
			Fx.After(c.Joker, 0.25, () => Tricks.Reload(c.Joker, 2));
		}),
		["UpMySleeve"] = new(Cast: Cast.Glove, Play: c => Tricks.Reload(c.Joker, 3)),
		["StormOfSteel"] = new(Cast: Cast.Glove, Play: c => Tricks.Reload(c.Joker, 6, 3)),
		["BladeSymphony"] = new(Cast: Cast.Glove, Play: c =>
		{
			Tricks.Reload(c.Joker, 2, 2);
			Spells.Kaja(c.Joker, Gold, 0.2f);
		}),
		["KnifeTrap"] = new(Cast: Cast.Glove, Play: c =>
		{
			Tricks.Reticle(c.One);
			Tricks.Reload(c.Joker, 3);
		}),
		// ── 毒 = 亚森的咒怨 ──
		["DeadlyPoison"] = new(Cast: Cast.Summon, Play: c => Persona.Summon(c.Joker, "Eiha", () => Spells.Eiha(c.One))),
		["BubbleBubble"] = new(Cast: Cast.Summon, Play: c => Persona.Quick(c.Joker, "Eiha", () => Spells.Eiha(c.One, 0.9f))),
		["BouncingFlask"] = new(Cast: Cast.Summon, Play: c => Persona.Quick(c.Joker, "Eiha", () =>
		{
			for (int i = 0; i < 3 && c.Enemies.Count > 0; i++)
			{
				Spells.Eiha(c.Enemies[(int)(Fx.Rng.Next() % c.Enemies.Count)], 0.6f, i * 0.18f);
			}
		})),
		["Snakebite"] = new(Cast: Cast.Summon, Play: c => Persona.Quick(c.Joker, "Eiha", () =>
		{
			Tricks.Chains(c.One, 2);
			Spells.CurseLick(c.One);
		})),
		["Haze"] = new(Cast: Cast.Summon, Play: c => Persona.Summon(c.Joker, "Maeiha", () =>
		{
			Spells.Maeiha(c.Enemies);
			foreach (NCreature e in c.Enemies)
			{
				Spells.Nda(e, Red, 0.3f);
			}
		})),
		["Outbreak"] = new(Cast: Cast.Big, Play: c => Persona.Summon(c.Joker, "Maeiha", () => Spells.Maeiha(c.Enemies, 1.1f), big: true)),
		["CorrosiveWave"] = new(Cast: Cast.Summon, Play: c => Persona.Summon(c.Joker, "Maeiha", () => Spells.Maeiha(c.Enemies, 0.75f))),
		["Concoct"] = new(Cast: Cast.Glove, Play: c => Spells.Kaja(c.Ally ?? c.Joker, Purple)),
		// ── 减益：-nda / 准星 ──
		["Expose"] = new(Cast: Cast.Glove, Play: c =>
		{
			Tricks.Reticle(c.One, weak: true);
			Spells.Nda(c.One, Blue, 0.3f);
		}),
		["Malaise"] = new(Cast: Cast.Summon, Play: c => Persona.Summon(c.Joker, "Debilitate", () =>
		{
			Spells.Nda(c.One, Red);
			Spells.Nda(c.One, Blue, 0.15f);
		})),
		["PiercingWail"] = new(Cast: Cast.Big, Play: c => Persona.Summon(c.Joker, "Matarunda", () =>
		{
			foreach (NCreature e in c.Enemies)
			{
				Spells.Nda(e, Red);
			}
		}, big: true)),
		["Flanking"] = new(Cast: Cast.Glove, Play: c =>
		{
			Tricks.Reticle(c.One);
			Tricks.Afterimages(c.Joker, Offsets(70, 0, 140, 0), Red, 0.4);
		}),
		["Tracking"] = new(Cast: Cast.Glove, Play: c =>
		{
			for (int i = 0; i < c.Enemies.Count; i++)
			{
				Tricks.Reticle(c.Enemies[i], delay: i * 0.08f);
			}
		}),
		// ── 增益：-kaja ──
		["Footwork"] = new(Cast: Cast.Summon, Play: c => Persona.Summon(c.Joker, "Sukukaja", () => Spells.Kaja(c.Joker, Green))),
		["Anticipate"] = new(Cast: Cast.Guard, Play: c => Persona.Quick(c.Joker, "Rakukaja", () => Spells.Kaja(c.Joker, Blue))),
		["Fade"] = new(Cast: Cast.Glove, Play: c => Spells.Kaja(c.Ally ?? c.Joker, Blue)),
		["Abrasive"] = new(Cast: Cast.Summon, Play: c => Persona.Quick(c.Joker, "Rakukaja", () =>
		{
			Spells.Kaja(c.Joker, Blue);
			Tricks.Chains(c.Joker, 3);
		})),
		["Speedster"] = new(Cast: Cast.Glove, Play: c =>
		{
			Spells.Kaja(c.Joker, Green);
			Tricks.Afterimages(c.Joker, Offsets(-50, 0, -100, 0, -150, 0), Green, 0.4);
		}),
		["Accuracy"] = new(Cast: Cast.Summon, Play: c => Persona.Quick(c.Joker, "Concentrate", () => Spells.Kaja(c.Joker, Gold))),
		["Afterimage"] = new(Cast: Cast.Glove, Play: c => Tricks.Afterimages(c.Joker, Offsets(-80, 0, 80, 0, -40, -20, 40, -20, 0, -30), Blue, 0.7, 0.04f)),
		["Sneaky"] = new(Cast: Cast.Glove, Play: c =>
		{
			Tricks.Shadow(c.Joker, 0.9);
			Spells.Kaja(c.Joker, Purple, 0.2f);
		}),
		["MasterPlanner"] = new(Cast: Cast.Glove, Play: c =>
		{
			Tricks.CallingCards(c.Joker, 4, spread: 1.6f);
			Spells.Kaja(c.Joker, Gold, 0.2f);
		}),
		["ToolsOfTheTrade"] = new(Cast: Cast.Glove, Play: c =>
		{
			Tricks.CallingCards(c.Joker, 2);
			Fx.After(c.Joker, 0.25, () => Tricks.Reload(c.Joker, 2));
		}),
		["InfiniteBlades"] = new(Cast: Cast.Glove, Play: c => Tricks.Reload(c.Joker, 4, 2)),
		["PhantomBlades"] = new(Cast: Cast.Glove, Play: c =>
		{
			Tricks.Afterimages(c.Joker, Offsets(-60, 0, 60, 0), Blue, 0.5, 0.0f);
			Tricks.Reload(c.Joker, 3, 2);
		}),
		["FanOfKnives"] = new(Cast: Cast.Glove, Play: c =>
		{
			Tricks.CallingCards(c.Joker, 5, spread: 1.8f);
			Fx.After(c.Joker, 0.3, () => Tricks.Reload(c.Joker, 5));
		}),
		// ── 能力：召唤亚森 ──
		["Envenom"] = new(Cast: Cast.Summon, Play: c => Persona.Quick(c.Joker, "Envenom", () =>
		{
			Spells.Kaja(c.Joker, Purple);
			Tricks.Reload(c.Joker, 1, 1);
		})),
		["Accelerant"] = new(Cast: Cast.Summon, Play: c => Persona.Quick(c.Joker, "Accelerant", () => Spells.Kaja(c.Joker, Purple))),
		["NoxiousFumes"] = new(Cast: Cast.Summon, Play: c => Persona.Summon(c.Joker, "Maeiha", () =>
		{
			Tricks.Smoke(c.Joker, Purple, 9, 1.4f);
			Spells.Maeiha(c.Enemies, 0.55f);
		})),
		["SerpentForm"] = new(Cast: Cast.Big, Play: c => Persona.Summon(c.Joker, "Arsène", () =>
		{
			Spells.Kaja(c.Joker, Purple);
			Tricks.Chains(c.Joker, 4);
		}, big: true)),
		["WraithForm"] = new(Cast: Cast.Big, Play: c => Persona.Summon(c.Joker, "Arsène", () =>
		{
			Tricks.Shadow(c.Joker, 1.2);
			Spells.Kaja(c.Joker, new Color(0.5f, 0.6f, 1f));
		}, big: true)),
	};

	public static int Count => Table.Count;

	public static CardDef? For(CardModel card) => Table.TryGetValue(card.GetType().Name, out CardDef? d) ? d : null;

	/// <summary>出牌那一刻（Hook.BeforeCardPlayed）。</summary>
	public static void OnPlay(CardModel card, Creature? target)
	{
		if (For(card) is not CardDef def || def.Play == null)
		{
			return;
		}
		NCombatRoom? room = NCombatRoom.Instance;
		NCreature? joker = Joker.Node(card.Owner);
		if (room == null || joker == null)
		{
			return;
		}
		List<NCreature> enemies = (card.CombatState?.HittableEnemies ?? Array.Empty<Creature>()).Select(room.GetCreatureNode).OfType<NCreature>().ToList();
		NCreature? t = target != null ? room.GetCreatureNode(target) : null;
		CardCtx ctx = new()
		{
			Joker = joker,
			Card = card,
			Enemies = enemies,
			Targets = t != null && !target!.IsPlayer ? new List<NCreature> { t } : enemies,
			Ally = t != null && target!.IsPlayer ? t : null
		};
		def.Play(ctx);
	}

	/// <summary>每一击伤害。</summary>
	public static void OnHit(CardModel card, NCreature joker, List<NCreature> targets)
	{
		CardDef? def = For(card);
		if (Joker.IsShot(card) || def?.Shot is not null)
		{
			Gunshot.Play(joker, targets, def?.Shot ?? Shot.Single);
		}
		else if (def?.Blade is Blade b)
		{
			Slash.Play(joker, targets, b, def.Size);
		}
		else if (card.Type == CardType.Attack)
		{
			Slash.Play(joker, targets);
		}
		if (def?.Curse == true)
		{
			foreach (NCreature t in targets)
			{
				Spells.CurseLick(t);
			}
		}
	}

	/// <summary>获得格挡：表里的牌按自己的样子，没列出的格挡牌用普通样子。</summary>
	public static void OnBlock(CardModel card, NCreature joker) => Tricks.Guard(joker, For(card)?.Guard ?? 0);

	public static Cast CastFor(CardModel card) => For(card)?.Cast ?? (card.Type == CardType.Power ? Cast.Glove : Cast.Summon);
}
