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
		NCreature? node = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
		SpineHooks.Ensure(node);
		return node;
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

	/// <summary>战斗立绘的整体缩放（与 tools/rig/build_rig_v7.py 的 BODY_SCALE 一致）。判定框 / 特效生成点是原版场景里固定的，人物缩小后按脚底换算。</summary>
	public const float BodyScale = 0.92f;

	public static Vector2 Center(NCreature node) => Shrink(node, node.VfxSpawnPosition);

	private static Vector2 Shrink(NCreature node, Vector2 p)
	{
		Vector2 feet = node.GetBottomOfHitbox();
		return feet + (p - feet) * BodyScale;
	}

	public static Vector2 Feet(NCreature node) => node.GetBottomOfHitbox();

	/// <summary>太阳穴附近：召唤器抵住的位置。等新骨骼完成后改为读取骨骼 "evoker_tip"。</summary>
	public static Vector2 Head(NCreature node)
	{
		Vector2 center = node.VfxSpawnPosition;
		Vector2 intent = node.Visuals.IntentPosition.GlobalPosition;
		return Shrink(node, center.Lerp(intent, 0.55f) + new Vector2(14f, 0f));
	}
}
