using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 出牌特效入口。当前阶段：所有储君卡牌共用「召唤器发动」，攻击牌追加十字斩，能力牌追加影时间光柱。
/// 后续按 docs/DESIGN.md 的分级表逐张接入专属特效。
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
		Control? back = Makoto.FxLayer(back: true);
		if (caster == null || front == null || back == null)
		{
			return;
		}

		CardType type = play.Card.Type;
		Vector2 head = Makoto.Head(caster);
		if (type == CardType.Power)
		{
			P3Fx.EvokerBurst(front, head, 1.3f, Pal.DarkHour);
			P3Fx.PowerUp(front, back, Makoto.Feet(caster), head);
			return;
		}

		P3Fx.EvokerBurst(front, head);

		if (type == CardType.Attack && play.Target is Creature target)
		{
			NCreature? targetNode = NCombatRoom.Instance?.GetCreatureNode(target);
			if (targetNode != null)
			{
				// 落在原版攻击动作挥出之后
				front.GetTree().CreateTimer(0.2).Timeout += () => ModEntry.Safe("cross slash", () =>
				{
					if (GodotObject.IsInstanceValid(targetNode))
					{
						P3Fx.CrossSlash(front, targetNode.VfxSpawnPosition, Pal.Cyan);
					}
				});
			}
		}
	}
}
