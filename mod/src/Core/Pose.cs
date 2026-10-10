using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace P3Makoto.Core;

/// <summary>
/// 直接切换角色 Spine 动画，用于原版状态机里没有的姿势（预备出牌）。
/// 骨骼里没有对应动画时什么也不做，所以在新骨骼完成前是安全的空操作。
/// </summary>
public static class Pose
{
	public const string Aim = "aim_loop";
	public const string ReadySword = "ready_sword_loop";

	public static bool Enter(NCreature? node, string anim)
	{
		MegaSprite? body = node?.Visuals.SpineBody;
		if (body == null || !body.HasAnimation(anim))
		{
			return false;
		}
		body.GetAnimationState().SetAnimation(anim, loop: true);
		return true;
	}

	public static bool IsHolding(NCreature? node)
	{
		string? current = node?.Visuals.SpineBody?.GetAnimationState().GetCurrentAnimationName();
		return current == Aim || current == ReadySword;
	}

	/// <summary>只有当前仍处于我们设置的预备姿势时才恢复待机，避免打断出牌动画。</summary>
	public static bool Exit(NCreature? node)
	{
		MegaSprite? body = node?.Visuals.SpineBody;
		if (body == null)
		{
			return false;
		}
		MegaAnimationState state = body.GetAnimationState();
		string? current = state.GetCurrentAnimationName();
		if (current != Aim && current != ReadySword)
		{
			return false;
		}
		bool lowHealth = node!.Entity.GetHpPercentRemaining() <= 0.25 && body.HasAnimation("low_health_loop");
		state.SetAnimation(lowHealth ? "low_health_loop" : "idle_loop", loop: true);
		return true;
	}
}
