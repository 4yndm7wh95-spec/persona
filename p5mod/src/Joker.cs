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

	/// <summary>举枪瞄准（gun_aim_b）时的枪口。</summary>
	public static Vector2 Muzzle(NCreature node) => PosePoint(node, new Vector2(1347f, 306f));

	/// <summary>Joker 是否朝右（联机时奇数位的玩家会被镜像）。</summary>
	public static float Facing(NCreature node)
	{
		Node2D? body = node.Visuals?.Body;
		return body != null && body.GlobalTransform.X.X < 0f ? -1f : 1f;
	}

	public static Control? FxLayer(bool back = false)
	{
		NCombatRoom? room = NCombatRoom.Instance;
		Control? c = room == null ? null : back ? room.BackCombatVfxContainer : room.CombatVfxContainer;
		return c == null ? null : Unscaled(c);
	}

	/// <summary>
	/// 特效层下面垫的一层「抵消缩放」的容器（用户 2026-11-13：Boss / 精英房里群蛇的蛇头和蛇身断开）。
	/// 原版按遭遇把战斗场景缩放（EncounterModel.GetCameraScaling，Boss 房镜头拉远）；特效的坐标、大小都是按人物在画布上的实际位置和身高算的，
	/// 放进被缩放的容器就会再缩一次（身前、身后两层缩得不一样时，跨两层画的东西就对不上）。这层把容器的缩放抵消掉、原点对到画布原点，
	/// 震屏时容器的平移照样传下来。普通房间缩放是 1，等于没有。
	/// </summary>
	private static Control Unscaled(Control layer)
	{
		const string name = "P5Unscaled";
		Control? wrap = layer.GetNodeOrNull<Control>(name);
		if (wrap == null || !GodotObject.IsInstanceValid(wrap))
		{
			wrap = new Control { Name = name, MouseFilter = Control.MouseFilterEnum.Ignore };
			layer.AddChild(wrap);
		}
		// 每次取的时候看一下：容器的缩放可能是建好以后才设的（房间刚载入时）
		Transform2D g = layer.GetGlobalTransform();
		float s = g.Scale.X;
		if (s > 0.01f && Mathf.Abs(wrap.GetGlobalTransform().Scale.X - 1f) > 0.001f)
		{
			// wrap 的全局变换 = 单位变换：局部变换取容器全局变换的逆（只有缩放和平移）
			Transform2D inv = g.AffineInverse();
			wrap.Scale = inv.Scale;
			wrap.Position = inv.Origin;
		}
		return wrap;
	}
}
