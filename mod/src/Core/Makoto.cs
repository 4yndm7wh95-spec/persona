using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P3Makoto.Core;

/// <summary>定位结城理（储君）在战斗场景中的节点和身体部位坐标。</summary>
public static class Makoto
{
	/// <summary>
	/// 这个玩家是不是"我们的"结城理：角色是储君，而且实际在用的储君骨骼是本 mod 的。
	/// v0.14.45（用户：可能和别的储君换皮 mod 冲突）：别的换皮 mod 后加载、把储君骨骼换成它自己的时，
	/// 本 mod 的全部表现（动作替换、出牌特效、舞台演出、选人界面）自动让路，整局按原版 + 那个 mod 走，避免两套混在一起出错。
	/// </summary>
	public static bool Is(Player? player)
	{
		if (player?.Character is not Regent)
		{
			return false;
		}
		NCreature? node = NCombatRoom.Instance?.GetCreatureNode(player.Creature);
		if (node != null && GodotObject.IsInstanceValid(node) && node.Visuals?.SpineBody is MegaSprite body)
		{
			return BodyIsOurs(node.GetInstanceId(), body);
		}
		return AssetsOurs;
	}

	/// <summary>只有本 mod 的结城理骨骼才有的动画（拔剑进入预备姿势）。</summary>
	private const string Signature = "sword_in";
	private const string SkeletonPath = "res://animations/characters/regent/regent_skel_data.tres";

	private static readonly System.Collections.Generic.Dictionary<ulong, bool> Bodies = new();

	private static bool BodyIsOurs(ulong nodeId, MegaSprite body)
	{
		lock (Bodies)
		{
			if (Bodies.TryGetValue(nodeId, out bool known))
			{
				return known;
			}
		}
		bool ours;
		try
		{
			if (body.GetSkeleton() == null)
			{
				return AssetsOurs;                      // 骨骼还没建好（HasAnimation 会是 false）：先按资源判断，不缓存
			}
			ours = body.HasAnimation(Signature);
		}
		catch (System.Exception)
		{
			return AssetsOurs;                          // 读不到就不缓存，下次再看
		}
		lock (Bodies)
		{
			if (Bodies.Count > 64)
			{
				Bodies.Clear();
			}
			Bodies[nodeId] = ours;
		}
		if (!ours)
		{
			NotOurs("the Regent in combat");
		}
		return ours;
	}

	private static bool? _assetsOurs;

	/// <summary>资源层面：res:// 里的储君战斗骨骼是不是本 mod 的（没有战斗节点时用，例如选人界面）。读不出来时当作是。</summary>
	public static bool AssetsOurs
	{
		get
		{
			if (_assetsOurs is bool b)
			{
				return b;
			}
			bool ours = true;
			try
			{
				// 骨骼数据没加载完时动画列表是空的，那种情况算"不知道"，当作是本 mod 的，也不缓存
				if (ResourceLoader.Load(SkeletonPath) is Resource res && res.HasMethod("find_animation") && res.HasMethod("get_animations"))
				{
					if (res.Call("get_animations").AsGodotArray().Count == 0)
					{
						return true;
					}
					ours = res.Call("find_animation", Signature).AsGodotObject() != null;
				}
			}
			catch (System.Exception)
			{
				ours = true;
			}
			_assetsOurs = ours;
			if (!ours)
			{
				NotOurs("res://animations/characters/regent");
			}
			return ours;
		}
	}

	private static bool _warned;

	private static void NotOurs(string where)
	{
		if (_warned)
		{
			return;
		}
		_warned = true;
		ModEntry.Warn($"{where} is not the P3Makoto skeleton (another Regent reskin mod loaded after us?) -> P3Makoto effects stay off for it");
	}

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
	public const float BodyScale = 0.89f;

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
