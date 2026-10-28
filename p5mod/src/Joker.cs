using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>Joker = 静默猎手的换皮：哪些牌算 Joker 的、他身上各个点在屏幕上的位置。</summary>
public static class Joker
{
	public static bool Is(Player? player) => player?.Character is Silent;

	public static bool OwnsCard(CardModel? card) => card != null && Is(card.Owner);

	/// <summary>小刀牌 = 开枪（原版只有 Shiv 这张牌给静默猎手播 shiv 动画）。</summary>
	public static bool IsShot(CardModel card) => card.Tags.Contains(CardTag.Shiv);

	public static NCreature? Node(Player? player) =>
		player == null ? null : NCombatRoom.Instance?.GetCreatureNode(player.Creature);

	// 骨骼（tools/p5/build_rig_p5.py）：姿势图 1536x1632 的最终画布，ORIGIN 像素是骨骼原点，K 是每像素的骨骼单位
	private const float OriginX = 815f, OriginY = 1622f, K = 0.87f;

	/// <summary>姿势图（art/p5/poses/final）上的一个像素 → 屏幕全局坐标。原版场景里 Visuals 节点带着缩放和偏移，用它的变换换算。</summary>
	public static Vector2 PosePoint(NCreature node, Vector2 px)
	{
		Vector2 local = new((px.X - OriginX) * K, (px.Y - OriginY) * K);
		Node2D? body = node.Visuals?.Body;
		return body != null && GodotObject.IsInstanceValid(body) ? body.ToGlobal(local) : node.GlobalPosition + local * 0.29f;
	}

	/// <summary>胸口（待机帧 idle_a3 上的像素），BLOCK 等贴身弹字的基准点。</summary>
	public static Vector2 Chest(NCreature node) => PosePoint(node, new Vector2(820f, 600f));

	/// <summary>举枪瞄准（gun_a2）时的枪口。</summary>
	public static Vector2 Muzzle(NCreature node) => PosePoint(node, new Vector2(1224f, 326f));

	/// <summary>Joker 是否朝右（联机时奇数位的玩家会被镜像）。</summary>
	public static float Facing(NCreature node)
	{
		Node2D? body = node.Visuals?.Body;
		return body != null && body.GlobalTransform.X.X < 0f ? -1f : 1f;
	}

	public static Control? FxLayer(bool back = false)
	{
		NCombatRoom? room = NCombatRoom.Instance;
		return room == null ? null : back ? room.BackCombatVfxContainer : room.CombatVfxContainer;
	}
}
