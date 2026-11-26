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
			return BodyIsOurs(body);
		}
		return AssetsOurs;
	}

	/// <summary>只有本 mod 的结城理骨骼才有的动画（拔剑进入预备姿势）。</summary>
	private const string Signature = "sword_in";
	private const string SkeletonPath = "res://animations/characters/regent/regent_skel_data.tres";

	private sealed record SkeletonSignature(bool Ours);
	private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Resource, SkeletonSignature> Known = new();

	private static bool? Identify(Resource? skeleton)
	{
		if (skeleton == null || !GodotObject.IsInstanceValid(skeleton)) return null;
		if (Known.TryGetValue(skeleton, out var cached)) return cached.Ours;
		if (!skeleton.HasMethod("get_animations") || !skeleton.HasMethod("find_animation")
			|| skeleton.Call("get_animations").AsGodotArray().Count == 0) return null;
		return Known.GetValue(skeleton, s => new SkeletonSignature(
			s.Call("find_animation", Signature).AsGodotObject() != null)).Ours;
	}

	private static bool BodyIsOurs(MegaSprite body)
	{
		if (body.BoundObject is GodotObject sprite && GodotObject.IsInstanceValid(sprite)
			&& sprite.HasMethod("get_skeleton_data_res")
			&& Identify(sprite.Get("skeleton_data_res").AsGodotObject() as Resource) is bool owns)
		{
			if (!owns) NotOurs("the Regent in combat");
			return owns;
		}
		return body.IsAnimationStateReady() ? body.HasAnimation(Signature) : AssetsOurs;
	}

	public static void ResetAssetCheck() => _warned = false;

	public static bool AssetsOurs
	{
		get
		{
			Resource? skeleton = ResourceLoader.GetCachedRef(SkeletonPath) ?? ResourceLoader.Load(SkeletonPath);
			bool? ours = Identify(skeleton);
			if (ours == false) NotOurs("res://animations/characters/regent");
			return ours ?? MakotoSelectSkin.ManagedSelection ?? AssetGuard.PackLoaded;
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

	/// <summary>原版包围战翻转 Body；读取实际世界横轴，不缓存、不按敌人屏幕位置猜方向。</summary>
	public static float Facing(NCreature node) => node.Body.GlobalTransform.X.X < 0f ? -1f : 1f;

	/// <summary>将朝右编写的身体相对偏移转到当前朝向；骨骼世界坐标不能再经过此方法。</summary>
	public static Vector2 Offset(NCreature node, Vector2 offset) =>
		Vfx.CrabRoom.Offset(node, new Vector2(offset.X * Facing(node), offset.Y));

	public static Vector2 Center(NCreature node) => Shrink(node, node.VfxSpawnPosition);

	private static Vector2 Shrink(NCreature node, Vector2 p)
	{
		Vector2 feet = Feet(node);
		return feet + (p - feet) * BodyScale;
	}

	public static Vector2 Feet(NCreature node) => Vfx.CrabRoom.Active
		? node.Hitbox.GetGlobalTransform() * new Vector2(node.Hitbox.Size.X * 0.5f, node.Hitbox.Size.Y)
		: node.GetBottomOfHitbox();

	/// <summary>太阳穴附近：召唤器抵住的位置。等新骨骼完成后改为读取骨骼 "evoker_tip"。</summary>
	public static Vector2 Head(NCreature node)
	{
		if (Vfx.CrabRoom.Active && Vfx.WeaponFx.Bone(node, "head") is Vector2 head)
			return head + Offset(node, new Vector2(0f, -25f));
		Vector2 center = node.VfxSpawnPosition;
		Vector2 intent = node.Visuals.IntentPosition.GlobalPosition;
		return Shrink(node, center.Lerp(intent, 0.55f) + Offset(node, new Vector2(14f, 0f)));
	}
}
