using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P3Makoto.Core;

/// <summary>定位结城理（储君）在战斗场景中的节点和身体部位坐标。</summary>
public static class Makoto
{
	public static bool Is(Player? player) => player?.Character is Regent;

	public static NCreature? Node(Player? player)
	{
		if (player == null)
		{
			return null;
		}
		return NCombatRoom.Instance?.GetCreatureNode(player.Creature);
	}

	/// <summary>特效层：挂在战斗 VFX 容器下，坐标用全局坐标。</summary>
	public static Control? FxLayer(bool back = false)
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null)
		{
			return null;
		}
		return back ? room.BackCombatVfxContainer : room.CombatVfxContainer;
	}

	public static Vector2 Center(NCreature node) => node.VfxSpawnPosition;

	public static Vector2 Feet(NCreature node) => node.GetBottomOfHitbox();

	/// <summary>太阳穴附近：召唤器抵住的位置。等新骨骼完成后改为读取骨骼 "evoker_tip"。</summary>
	public static Vector2 Head(NCreature node)
	{
		Vector2 center = node.VfxSpawnPosition;
		Vector2 intent = node.Visuals.IntentPosition.GlobalPosition;
		return center.Lerp(intent, 0.55f) + new Vector2(14f, 0f);
	}
}
