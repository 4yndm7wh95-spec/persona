using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 出牌特效调度：
/// - OnPlay：出牌瞬间的开场（召唤器 / 能力演出 / 额外演出），见 CardStyles。
/// - OnHit：每一击伤害时在目标身上播放元素命中特效（与游戏伤害判定同步）。
/// - OnBlock / OnStars / OnForge：获得格挡、辉星、锻造君王之刃时自动叠加对应演出。
/// </summary>
public static class CardFx
{
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

		// 攻击牌：结城理挥剑，召唤器特效立即出现；技能 / 能力牌：等施法动画里扣扳机那一帧（evoker_fire 事件）
		bool attack = play.Card.Type == CardType.Attack;
		switch (style.Opener)
		{
			case Opener.Evoker:
				if (attack) P3Fx.EvokerBurst(front, head);
				else QueueEvoker(caster, 1f, Pal.Cyan);
				break;
			case Opener.EvokerSmall:
				if (attack) P3Fx.EvokerBurst(front, head, 0.7f);
				else QueueEvoker(caster, 0.7f, Pal.Cyan);
				break;
			case Opener.Power:
				QueueEvoker(caster, 1.3f, Pal.DarkHour);
				P3Fx.PowerUp(front, back, feet, head);
				Fx.After(front, 0.15, "power tarot", () => Extras.TarotFlip(front, head + new Vector2(-30f, -150f), Pal.DarkHour));
				break;
		}

		Extra x = style.Extra;
		if (x.HasFlag(Extra.SkewBand))
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
		Fx.After(caster, 0.5, "evoker fallback", () =>
		{
			if (PendingEvoker.Remove(id, out var p) && GodotObject.IsInstanceValid(caster) && Makoto.FxLayer() is Control front)
			{
				P3Fx.EvokerBurst(front, Makoto.Head(caster), p.Power, p.Tint);
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
	}

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
		foreach (Creature c in list)
		{
			NCreature? node = room.GetCreatureNode(c);
			if (node != null)
			{
				Elements.Hit(style.Hit, front, node.VfxSpawnPosition, node.GetBottomOfHitbox(), scale);
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
		Extras.HexShield(front, target.VfxSpawnPosition, big ? 1.3f : 1f, reflect, big ? 0.7f : 0.45f);
	}

	public static void OnStars(NCreature caster, int amount)
	{
		Control? front = Makoto.FxLayer();
		if (front != null)
		{
			Extras.StarGain(front, Makoto.Head(caster), amount);
		}
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
