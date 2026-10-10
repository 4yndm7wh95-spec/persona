using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 出牌特效入口。v0.0.1 阶段：所有储君卡牌共用「召唤器发动」基础特效，
/// 攻击牌额外在目标身上打出交叉斩。后续按 docs/DESIGN.md 的分级表逐张接入专属特效。
/// </summary>
public static class CardFx
{
	public static void OnPlay(CardPlay play)
	{
		if (!Makoto.Is(play.Player))
		{
			return;
		}
		NCreature? caster = Makoto.Node(play.Player);
		Control? front = Makoto.FxLayer();
		if (caster == null || front == null)
		{
			return;
		}

		CardType type = play.Card.Type;
		float power = type == CardType.Power ? 1.4f : 1f;
		P3Fx.EvokerBurst(front, Makoto.Head(caster), power);

		if (type == CardType.Power)
		{
			P3Fx.Shockwave(Makoto.FxLayer(back: true) ?? front, Makoto.Feet(caster), Pal.DarkHour, 700f, 0.8f, squashY: 0.3f);
		}

		if (type == CardType.Attack && play.Target is Creature target)
		{
			NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
			if (targetNode != null)
			{
				// 稍作延迟，让斩击落在原版攻击动作挥出之后
				front.GetTree().CreateTimer(0.18).Timeout += () => ModEntry.Safe("cross slash", () =>
				{
					if (GodotObject.IsInstanceValid(targetNode))
					{
						P3Fx.CrossSlash(front, targetNode.VfxSpawnPosition, Pal.Blue);
					}
				});
			}
		}
	}
}
