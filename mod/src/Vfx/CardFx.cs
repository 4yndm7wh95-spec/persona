using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using P3Makoto.Audio;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 出牌特效调度：
/// - OnPlay：出牌瞬间的开场（召唤器 / 能力演出 / 额外演出），见 CardStyles。
/// - OnHit：每一击伤害时在目标身上播放元素命中特效（与游戏伤害判定同步）。
/// - OnBlock / OnForge：获得格挡、锻造君王之刃时自动叠加对应演出（辉星的获得 / 消耗由 StarOrbit 演出）。
/// </summary>
public static class CardFx
{
	// 有自己物件演出（契约 / 全书 / Shuffle Time / 天鹅绒门）的牌：施法时不再额外翻一张小塔罗
	private static readonly HashSet<string> ThemeCards = new() { "ForegoneConclusion", "Prophesize", "DecisionsDecisions", "PillarOfCreation", "LunarBlast", "CrashLanding", "HeavenlyDrill", "MonarchsGaze", "Furnace", "Tyranny", "Genesis", "Radiate" };

	public static bool IsMakotoCard(CardModel? card) => card != null && Makoto.Is(card.Owner);

	public static bool ReplacesHitVfx(CardModel? card) =>
		IsMakotoCard(card) && CardStyles.For(card!, card!.Type).Hit != Element.None;

	public static void OnPlay(CardPlay play)
	{
		if (!Makoto.Is(play.Player))
		{
			return;
		}
		NCreature? caster = Makoto.Node(play.Player);
		Control? front = Makoto.FxLayer();
		Control? back = Makoto.FxLayer(back: true);
		if (caster == null || front == null || back == null)
		{
			return;
		}
		CardStyle style = CardStyles.For(play.Card, play.Card.Type);
		Vector2 head = Makoto.Head(caster);
		Vector2 feet = Makoto.Feet(caster);

		// 只有召唤 Persona 的大招才用召唤器：挥剑的攻击牌召唤特效立即出现；施法牌等扣扳机那一帧（evoker_fire 事件）。
		// 其他牌不召唤，只保留它自己的效果（护盾、星光、塔罗等）；能力牌仍有光柱演出，但不开枪。
		CardStyles.CardAnim anim = CardStyles.AnimFor(play.Card, play.Card.Type);
		bool attack = anim == CardStyles.CardAnim.Sword;
		bool summon = anim != CardStyles.CardAnim.None && CardStyles.Summons(play.Card);
		bool sovereign = play.Card.GetType().Name == "SovereignBlade";
		bool cutIn = summon && CutInFx.Enabled;
		// 召唤器 / 施法出招的攻击牌：整次出牌只播一次出招动作（后续多段攻击不再接挥剑）
		Pose.MarkSingleAttackAnim(caster, play.Card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack && anim != CardStyles.CardAnim.Sword);
		bool stardust = play.Card.GetType().Name == "Stardust";
		bool gamble = play.Card.GetType().Name == "RoyalGamble";
		if (gamble)
		{
			Pose.HoldCast(caster, 0.15f + Grand.FortunePayout);   // 辉星等命运之轮的奖励落进胸口才到账
		}
		if (stardust)
		{
			StarOrbit.ArmVolley(caster);                 // 本次花掉的辉星不碎，留在头顶，每一击掷出一颗
		}
		if (summon)
		{
			Pose.MarkSummon(caster);                      // attack / cast → "召唤开枪"（抵住太阳穴停一拍再扣扳机）
		}
		if (cutIn)
		{
			// P3R 技能切入：眼神特写横幅 → 扣扳机时碎裂 → Persona 现身（君王之刃：Thanatos 冲过去斩击；其他：显形后消散）
			List<NCreature> cutTargets = Targets(play);
			Color tint = play.Card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Power ? Pal.DarkHour : Pal.Cyan;
			CutInFx.Begin(caster, play.Card.Title, CardStyles.CaptionFor(play.Card).Persona, elapsed =>
			{
				if (sovereign)
				{
					PersonaFx.SummonThanatos(caster, cutTargets.Count > 0 ? cutTargets[0] : null, elapsed);
				}
				else
				{
					PersonaFx.Apparition(caster, tint);
				}
			});
		}
		if (anim == CardStyles.CardAnim.Gesture)
		{
			Pose.MarkLightCast(caster);
			// 收手（cast_light 0.36s 起）：掌心残留的一团淡青光沿手放下的弧线落到腰侧、拖着几粒光点散掉 —— 视线跟着手走，
			// 掩住过渡帧之间人物外观的细微差异
			Fx.After(front, 0.36, "gesture afterglow", () => Extras.PalmAfterglow(front, caster));
			// 掌心托起的那一刻，手上方翻出一张塔罗牌（牌本身带塔罗演出的就不重复）
			if (!style.Extra.HasFlag(Extra.Tarot) && !stardust && !gamble && !ThemeCards.Contains(play.Card.GetType().Name) && !CardSigs.Has(play.Card))
			{
				Fx.After(front, 0.2, "gesture tarot", () =>
				{
					Vector2 hand = WeaponFx.Bone(caster, "hand") ?? Makoto.Center(caster);
					Extras.TarotFlip(front, hand + new Vector2(10f, -80f), play.Card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Power ? Pal.DarkHour : Pal.Cyan, 0.3f);
				});
			}
		}
		Opener opener = summon || style.Opener == Opener.Power ? style.Opener : Opener.None;
		switch (opener)
		{
			case Opener.Evoker:
				if (attack && !cutIn) P3Fx.EvokerBurst(front, head);
				else QueueEvoker(caster, 1f, Pal.Cyan);
				break;
			case Opener.EvokerSmall:
				if (attack && !cutIn) P3Fx.EvokerBurst(front, head, 0.7f);
				else QueueEvoker(caster, 0.7f, Pal.Cyan);
				break;
			case Opener.Power:
				if (summon)
				{
					QueueEvoker(caster, 1.3f, Pal.DarkHour);
				}
				P3Fx.PowerUp(front, back, feet, head, P3Fx.ArcanaFor(play.Card.GetType().Name));
				break;
		}

		Extra x = style.Extra;
		if (x.HasFlag(Extra.SkewBand) && !cutIn)            // 有眼神切入时不再叠蓝色色带
		{
			(string skill, string persona) = CardStyles.CaptionFor(play.Card);
			Extras.SkewBand(front, new Extras.Caption(skill, persona, play.Card.Title));
		}
		if (x.HasFlag(Extra.DarkHour))
		{
			Extras.DarkHour(back);
		}
		if (x.HasFlag(Extra.Tarot))
		{
			Fx.After(front, 0.12, "skill tarot", () => Extras.TarotFlip(front, head + new Vector2(-30f, -150f), Pal.Cyan, 0.25f));
		}
		if (x.HasFlag(Extra.Butterflies))
		{
			Fx.After(front, 0.2, "butterflies", () => P3Fx.ButterflyBurst(front, head, 6, 320f));
		}
		if (x.HasFlag(Extra.BuffSelf))
		{
			Extras.BuffArrows(front, Makoto.Center(caster), feet, up: true);
		}
		if (x.HasFlag(Extra.HamaSelf))
		{
			Fx.After(front, 0.2, "hama self", () => Elements.Hama(front, Makoto.Center(caster), feet, 1.3f));
		}
		if (x.HasFlag(Extra.MegidoAbove))
		{
			Fx.After(front, 0.15, "megido above", () => Elements.Megido(front, head + new Vector2(260f, -120f), 1.3f));
		}
		if (x.HasFlag(Extra.ShieldSelf))
		{
			Fx.After(front, 0.2, "shield self", () => Extras.HexShield(front, Makoto.Center(caster), 1.1f, hold: 0.6f));
		}
		if (x.HasFlag(Extra.ForgeSelf))
		{
			Fx.After(front, 0.2, "forge self", () => Extras.ForgeBlade(front, Makoto.Center(caster) + new Vector2(120f, -40f)));
		}
		if (x.HasFlag(Extra.StarsSelf))
		{
			Fx.After(front, 0.2, "stars self", () => Extras.StarGain(front, head, 6));
		}
		if (x.HasFlag(Extra.BlackHoleCenter))
		{
			Vector2 size = front.GetViewportRect().Size;
			Fx.After(front, 0.2, "black hole", () => Extras.BlackHole(front, new Vector2(size.X * 0.62f, size.Y * 0.38f)));
		}

		List<NCreature> targets = Targets(play);
		CardSigs.Play(play.Card, new CardSigs.Ctx(front, back, caster, head, Makoto.Center(caster), feet, targets));
		// 君王之刃 = 召唤 Thanatos 挥剑（Persona 骨骼动画）
		if (sovereign && !cutIn)
		{
			PersonaFx.SummonThanatos(caster, targets.Count > 0 ? targets[0] : null);
		}
		if (x.HasFlag(Extra.DebuffTargets))
		{
			foreach (NCreature t in targets)
			{
				Extras.BuffArrows(front, t.VfxSpawnPosition, t.GetBottomOfHitbox(), up: false);
			}
		}
		if (x.HasFlag(Extra.BlackHoleAtTarget))
		{
			foreach (NCreature t in targets)
			{
				Extras.BlackHole(front, t.VfxSpawnPosition, 0.9f);
			}
		}
	}

	private static readonly Dictionary<ulong, (float Power, Color Tint)> PendingEvoker = new();

	/// <summary>登记一次召唤器发动，等施法动画的 evoker_fire 事件触发；0.5 秒内没等到就在头部直接发动。</summary>
	private static void QueueEvoker(NCreature caster, float power, Color tint)
	{
		ulong id = caster.GetInstanceId();
		PendingEvoker[id] = (power, tint);
		Fx.After(caster, 0.7, "evoker fallback", () =>              // 召唤开枪 ~0.43s 扣扳机，留余量
		{
			if (PendingEvoker.Remove(id, out var p) && GodotObject.IsInstanceValid(caster) && Makoto.FxLayer() is Control front)
			{
				P3Fx.EvokerBurst(front, Makoto.Head(caster), p.Power, p.Tint);
				EvokerSound(p.Power);
			}
		});
	}

	/// <summary>施法动画扣扳机的那一帧：在枪口（太阳穴）位置发动召唤器。没有待发动的牌时放一个小号的。</summary>
	public static void FireEvoker(NCreature caster)
	{
		if (Makoto.FxLayer() is not Control front)
		{
			return;
		}
		(float power, Color tint) = PendingEvoker.Remove(caster.GetInstanceId(), out var p) ? p : (0.6f, Pal.Cyan);
		// 扣扳机时显示的是手绘举枪手臂：枪口 / 握把取它自己的骨骼（muzzle / grip）
		Vector2 muzzle = WeaponFx.Bone(caster, "muzzle") ?? WeaponFx.Bone(caster, "evoker_tip") ?? Makoto.Head(caster);
		Vector2 grip = WeaponFx.Bone(caster, "grip") ?? WeaponFx.Bone(caster, "hand") ?? muzzle + Vector2.Right;
		EvokerShot.Fire(front, muzzle, muzzle - grip, power, tint);
		EvokerSound(power);
		CutInFx.Fire(caster);                             // 有进行中的眼神切入：横幅碎裂、Persona 现身
	}

	/// <summary>召唤器枪声（本 mod 唯一的自制音效；其余都用游戏原版音效）。</summary>
	private static void EvokerSound(float power) => Sfx.Play(Snd.Gunshot, 1.1f + 0.15f * power);

	/// <summary>单体牌取目标；群体 / 无目标的攻击性牌取全部存活敌人。</summary>
	private static List<NCreature> Targets(CardPlay play)
	{
		NCombatRoom? room = NCombatRoom.Instance;
		List<NCreature> result = new();
		if (room == null)
		{
			return result;
		}
		IEnumerable<Creature> creatures = play.Target != null
			? new[] { play.Target }
			: play.Card.CombatState?.Enemies.Where(c => c.IsAlive) ?? Enumerable.Empty<Creature>();
		foreach (Creature c in creatures)
		{
			NCreature? node = room.GetCreatureNode(c);
			if (node != null)
			{
				result.Add(node);
			}
		}
		return result;
	}

	// 召唤 Persona 的攻击牌打中敌人时弹"WEAK!"（P3R 原版弱点命中的感觉）；同一次出牌对同一个敌人只弹一次
	private static readonly Dictionary<(CardModel Card, Creature Target), ulong> WeakShown = new();

	private static void WeakPopup(CardModel card, NCreature node, Control front)
	{
		ulong now = Time.GetTicksMsec();
		var key = (card, node.Entity);
		if (WeakShown.TryGetValue(key, out ulong t) && now - t < 2000)
		{
			return;
		}
		WeakShown[key] = now;
		Popups.Weak(front, node.VfxSpawnPosition);
	}

	public static void OnHit(CardModel card, IEnumerable<Creature> targets)
	{
		CardStyle style = CardStyles.For(card, card.Type);
		if (style.Hit == Element.None)
		{
			return;
		}
		NCombatRoom? room = NCombatRoom.Instance;
		Control? front = Makoto.FxLayer();
		if (room == null || front == null)
		{
			return;
		}
		List<Creature> list = targets.ToList();
		float scale = style.HitScale * (list.Count > 1 ? 0.8f : 1f);
		// 王之凝视在场：被结城理攻击的敌人身上闪一下眼形印记（力量下降）
		if (card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack && card.Owner?.Creature?.HasPower<MegaCrit.Sts2.Core.Models.Powers.MonarchsGazePower>() == true)
		{
			foreach (Creature c in list)
			{
				if (!c.IsPlayer && room.GetCreatureNode(c) is NCreature n)
				{
					Rares.Mark(front, n.VfxSpawnPosition + new Vector2(0f, -60f), 0.8f);
				}
			}
		}
		// 天际钻头：每一击 = 头顶的螺旋光锥往下钻一下
		if (card.GetType().Name == "HeavenlyDrill")
		{
			foreach (Creature c in list)
			{
				if (room.GetCreatureNode(c) is NCreature n)
				{
					Rares.DrillHit(n);
				}
			}
			return;
		}
		// 迫降：命中特效由坠落的月亮统一演出，这里只在每个敌人身上补一点碎光
		if (card.GetType().Name == "CrashLanding")
		{
			foreach (Creature c in list)
			{
				if (room.GetCreatureNode(c) is NCreature n)
				{
					P3Fx.GlassShatter(front, n.VfxSpawnPosition, 10, 0.5f, Pal.Ice);
				}
			}
			return;
		}
		// 月面射击：每一击 = 目标头顶的月亮削下一道新月光刃
		if (card.GetType().Name == "LunarBlast")
		{
			foreach (Creature c in list)
			{
				if (room.GetCreatureNode(c) is NCreature n)
				{
					Lunar.Hit(n, 1f);
				}
			}
			return;
		}
		// 星尘：每一击 = 头顶待命的一颗辉星砸向目标（命中特效在辉星落地时播放）
		if (card.GetType().Name == "Stardust" && Makoto.Node(card.Owner) is NCreature thrower)
		{
			foreach (Creature c in list)
			{
				if (room.GetCreatureNode(c) is NCreature n)
				{
					Vector2 at = n.VfxSpawnPosition, feet = n.GetBottomOfHitbox();
					StarOrbit.Throw(thrower, at, () => Elements.Hit(Element.Star, front, at, feet, scale));
				}
			}
			return;
		}
		// 物理系命中：50ms 顿帧 + 轻震屏，给刀砍进去的"顿感"
		if (style.Hit is Element.Slash or Element.Royal or Element.Strike or Element.Pierce)
		{
			EvokerShot.HitStop(0.05);
			MegaCrit.Sts2.Core.Nodes.NGame.Instance?.ScreenShake(MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeStrength.Weak, MegaCrit.Sts2.Core.Nodes.Vfx.Utilities.ShakeDuration.Short);
		}
		foreach (Creature c in list)
		{
			NCreature? node = room.GetCreatureNode(c);
			if (node != null)
			{
				Elements.Hit(style.Hit, front, node.VfxSpawnPosition, node.GetBottomOfHitbox(), scale);
				if (CardStyles.Summons(card) && !c.IsPlayer)
				{
					WeakPopup(card, node, front);
				}
			}
		}
	}

	public static void OnBlock(CardModel card, NCreature target)
	{
		Control? front = Makoto.FxLayer();
		if (front == null)
		{
			return;
		}
		string name = card.GetType().Name;
		bool reflect = name == "Reflect";
		bool big = name is "IAmInvincible" or "Bulwark";
		Extras.HexShield(front, target.VfxSpawnPosition, big ? 1.6f : 1.25f, reflect, big ? 0.7f : 0.5f);
	}

	public static void OnForge(NCreature caster)
	{
		Control? front = Makoto.FxLayer();
		if (front != null)
		{
			Extras.ForgeBlade(front, Makoto.Center(caster) + new Vector2(120f, -40f));
		}
	}
}
