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

	// 用召唤器 / 施法动作出招的攻击牌：只有第一次 Attack 触发播动作，之后的多段攻击（七星、星尘）
	// 或卡牌自己再触发一次的 Attack（星灭）都不再播挥剑（否则会接一个斩击动作），只保留游戏的等待节奏。
	private static readonly Dictionary<ulong, (ulong Until, int Count)> SingleAttack = new();

	public static void MarkSingleAttackAnim(NCreature node, bool on)
	{
		if (on)
		{
			SingleAttack[node.GetInstanceId()] = (Godot.Time.GetTicksMsec() + 120000, 0);   // 到下一次出牌才清掉（星尘 X 很大时要打很久）
		}
		else
		{
			SingleAttack.Remove(node.GetInstanceId());
		}
	}

	// 某些牌的效果要等演出到某一刻才结算（胜券在王：命运之轮停下、奖励落进胸口才获得辉星）
	private static readonly Dictionary<ulong, (ulong Until, float Seconds)> CastHold = new();

	public static void HoldCast(NCreature node, float seconds) =>
		CastHold[node.GetInstanceId()] = (Godot.Time.GetTicksMsec() + 1000, seconds);

	/// <summary>本次出牌的 Cast 触发需要等待的时长（没有标记返回 0）。只用一次。</summary>
	public static float TakeCastHold(NCreature node) =>
		CastHold.Remove(node.GetInstanceId(), out var e) && Godot.Time.GetTicksMsec() <= e.Until ? e.Seconds : 0f;

	/// <summary>
	/// 先 TriggerAnim("Cast") 再 DamageCmd.Attack 的攻击牌（下砸、彗星、引路星）：施法动作就算这次出牌的出招，
	/// 之后的 Attack 触发不再接斩击。
	/// </summary>
	public static void CountCastAsAttack(NCreature node)
	{
		ulong id = node.GetInstanceId();
		if (SingleAttack.TryGetValue(id, out var e) && Godot.Time.GetTicksMsec() <= e.Until)
		{
			SingleAttack[id] = (e.Until, System.Math.Max(e.Count, 1));
		}
	}

	/// <summary>这次 Attack 触发是否应跳过动作（本次出牌已经播过一次）。</summary>
	public static bool SkipRepeatAttack(NCreature node)
	{
		ulong id = node.GetInstanceId();
		if (!SingleAttack.TryGetValue(id, out var e) || Godot.Time.GetTicksMsec() > e.Until)
		{
			return false;
		}
		SingleAttack[id] = (e.Until, e.Count + 1);
		return e.Count >= 1;
	}

	/// <summary>这次出牌不召唤 Persona：接下来 1 秒内游戏请求的 cast 换成轻的身体动作 cast_light（不拿召唤器）。</summary>
	public static void MarkLightCast(NCreature node)
	{
		MegaSprite? body = node.Visuals.SpineBody;
		if (body != null && body.HasAnimation("cast_light") && body.TryGetAnimationState() is MegaAnimationState state)
		{
			LightCast[state.BoundObject.GetInstanceId()] = Godot.Time.GetTicksMsec() + 1000;
		}
	}

	// 召唤 Persona 的牌（君王之刃 + 所有召唤牌）：游戏要播的 attack / cast 统一换成"召唤开枪"动画
	// （attack_sovereign / sovereign_from_aim：召唤器抵住太阳穴停一拍，~0.4s 扣扳机，配合 CutInFx 的眼神切入）
	private static readonly Dictionary<ulong, ulong> Summon = new();

	/// <summary>这次出牌召唤 Persona：接下来 1 秒内游戏请求的 attack / cast 换成召唤开枪。</summary>
	public static void MarkSummon(NCreature node)
	{
		if (node.Visuals.SpineBody?.TryGetAnimationState() is MegaAnimationState state)
		{
			Summon[state.BoundObject.GetInstanceId()] = Godot.Time.GetTicksMsec() + 1000;
		}
	}

	private static ulong _lastSummonAnim;

	/// <summary>正在召唤开枪：有未过期的召唤标记，或者 0.4s 内刚改写过召唤动画。原版 regent_attack 的挥动声此时不播（见 Sfx.ReplaceRegent）。</summary>
	public static bool SummonActive()
	{
		ulong now = Godot.Time.GetTicksMsec();
		if (now - _lastSummonAnim < 400)
		{
			return true;
		}
		foreach (ulong until in Summon.Values)
		{
			if (now <= until)
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>这个角色是否有待执行的召唤开枪（伤害 / 效果等待据此延后，见 SovereignTimingPatch）。</summary>
	public static bool HasSummon(NCreature node) =>
		node.Visuals.SpineBody?.TryGetAnimationState() is MegaAnimationState state
		&& Summon.TryGetValue(state.BoundObject.GetInstanceId(), out ulong until) && Godot.Time.GetTicksMsec() <= until;

	/// <summary>
	/// 按这次出牌的标记改写动画：不召唤的施法 cast → cast_light；召唤 Persona 的攻击 attack → attack_sovereign
	/// （正在举枪瞄准时 → sovereign_from_aim）。
	/// </summary>
	public static string? Light(MegaAnimationState state, string requested)
	{
		ulong id = state.BoundObject.GetInstanceId();
		ulong now = Godot.Time.GetTicksMsec();
		if (requested is "cast" or "attack" && !Summon.ContainsKey(id) && LightCast.Remove(id, out ulong until))
		{
			return now <= until ? "cast_light" : null;
		}
		if (requested is "attack" or "cast" or "attack_sovereign" && Summon.Remove(id, out ulong until2) && now <= until2)
		{
			string anim = FromHolding(state.GetCurrentAnimationName(), "attack_sovereign") ?? "attack_sovereign";
			_lastSummonAnim = now;
			return anim;
		}
		return null;
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
