using System.Collections.Generic;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace P3Makoto.Core;

/// <summary>
/// 预备出牌姿势的进入 / 退出。每个方向都有专门的过渡动画（见 tools/rig/build_rig.py）：
///   拖牌：aim_in → aim_loop / sword_in → ready_sword_loop
///   取消：aim_out / sword_out → idle_loop（或低血量待机）
///   打出：游戏要播 cast / attack 时，由 AnimRenamePatch 换成 cast_from_aim / attack_from_ready，直接接招不垂手。
/// 骨骼里没有这些动画时（例如原版储君骨骼），退化为直接切换循环动画。
/// </summary>
public static class Pose
{
	public const string Aim = "aim_loop";
	public const string ReadySword = "ready_sword_loop";

	private static readonly Dictionary<string, (string In, string Out)> Transitions = new()
	{
		[Aim] = ("aim_in", "aim_out"),
		[ReadySword] = ("sword_in", "sword_out"),
	};

	/// <summary>处于（或正在进入）预备姿势的动画名。</summary>
	public static readonly HashSet<string> Holding = new() { "aim_in", Aim, "sword_in", ReadySword };

	public static bool Enter(NCreature? node, string loopAnim)
	{
		MegaSprite? body = node?.Visuals.SpineBody;
		if (body == null || !body.HasAnimation(loopAnim))
		{
			return false;
		}
		MegaAnimationState state = body.GetAnimationState();
		string inAnim = Transitions[loopAnim].In;
		if (body.HasAnimation(inAnim))
		{
			state.SetAnimation(inAnim, loop: false);
			state.AddAnimation(loopAnim, 0f, loop: true);
		}
		else
		{
			state.SetAnimation(loopAnim, loop: true);
		}
		return true;
	}

	public static bool IsHolding(NCreature? node)
	{
		string? current = node?.Visuals.SpineBody?.GetAnimationState().GetCurrentAnimationName();
		return current != null && Holding.Contains(current);
	}

	/// <summary>只有当前仍处于预备姿势时才播放放下动画并回到待机，避免打断出牌动画。</summary>
	public static bool Exit(NCreature? node)
	{
		MegaSprite? body = node?.Visuals.SpineBody;
		if (body == null)
		{
			return false;
		}
		MegaAnimationState state = body.GetAnimationState();
		string? current = state.GetCurrentAnimationName();
		if (current == null || !Holding.Contains(current))
		{
			return false;
		}
		string loop = current.StartsWith("aim") ? Aim : ReadySword;
		string outAnim = Transitions[loop].Out;
		bool lowHealth = node!.Entity.GetHpPercentRemaining() <= 0.25 && body.HasAnimation("low_health_loop");
		string idle = lowHealth ? "low_health_loop" : "idle_loop";
		if (body.HasAnimation(outAnim))
		{
			state.SetAnimation(outAnim, loop: false);
			state.AddAnimation(idle, 0f, loop: true);
		}
		else
		{
			state.SetAnimation(idle, loop: true);
		}
		return true;
	}

	private static readonly Dictionary<ulong, ulong> LightCast = new();

	/// <summary>这次出牌不召唤 Persona：接下来 1 秒内游戏请求的 cast 换成轻的身体动作 cast_light（不拿召唤器）。</summary>
	public static void MarkLightCast(NCreature node)
	{
		MegaSprite? body = node.Visuals.SpineBody;
		if (body != null && body.HasAnimation("cast_light") && body.TryGetAnimationState() is MegaAnimationState state)
		{
			LightCast[state.BoundObject.GetInstanceId()] = Godot.Time.GetTicksMsec() + 1000;
		}
	}

	/// <summary>游戏请求 cast 时，若这次出牌已标记为不召唤，返回 cast_light。</summary>
	public static string? Light(MegaAnimationState state, string requested)
	{
		if (requested != "cast" || !LightCast.Remove(state.BoundObject.GetInstanceId(), out ulong until))
		{
			return null;
		}
		return Godot.Time.GetTicksMsec() <= until ? "cast_light" : null;
	}

	/// <summary>游戏请求播放 requested 时，如果角色正处于预备姿势，返回"从预备姿势接招"的版本。</summary>
	public static string? FromHolding(string? current, string requested)
	{
		if (current == null || !Holding.Contains(current))
		{
			return null;
		}
		bool aiming = current.StartsWith("aim");
		return requested switch
		{
			"cast" when aiming => "cast_from_aim",
			"attack_sovereign" when aiming => "sovereign_from_aim",
			"attack" when !aiming => "attack_from_ready",
			_ => null
		};
	}
}
