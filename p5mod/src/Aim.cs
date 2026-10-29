using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace P5Joker;

/// <summary>
/// 骨骼动画的接续（用户 2026-10-28）：
///  · 拿起小刀牌就转枪出枪瞄准（aim_in → aim_loop），打出去直接开火（shiv 换成 shiv_from_aim；转枪还没转完就打出，
///    换成 shiv 从同一时刻接着播），放回去转枪收走（aim_out）；
///  · 拿起打击类攻击牌就在腰侧转刀起手（knife_in → knife_loop），打出去直接上撩（attack 换成 attack_from_ready；
///    转刀还没转完就打出，换回 attack 从同一时刻接着播），放回去转刀收起（knife_out）；
///  · 连续出攻击牌时，上一刀还在收势就接下一刀（attack 换成 attack_chain，从手里握着刀直接再蓄力），不再从拔刀重播；
///    连续开枪同理（shiv 换成 shiv_from_aim）。
/// 只动 Joker 的骨骼（原版静默猎手被我们的骨骼替换了，这些动画名只在 art/p5/rig/joker.spine-json 里有）。
/// </summary>
public static class Aim
{
	private static readonly HashSet<string> Raised = new() { "aim_in", "aim_loop", "shiv", "shiv_from_aim" };
	private static readonly HashSet<string> Slashing = new() { "attack", "attack_chain", "attack_from_ready" };
	private static readonly HashSet<string> Readied = new() { "knife_in", "knife_loop" };

	/// <summary>换动画之后要跳到的时间（AnimRenamePatch 的 Postfix 用）。</summary>
	public static readonly Dictionary<ulong, float> SkipTo = new();

	/// <summary>SetAnimation 之后：这个 Joker 有待跳的时间就跳过去，并且不做交叉淡化（同一套画面接着播）。返回跳过的秒数。</summary>
	public static float ApplySkip(NCreature node, MegaAnimationState state)
	{
		if (!SkipTo.Remove(node.GetInstanceId(), out float t))
		{
			return 0f;
		}
		using MegaTrackEntry? entry = state.GetCurrent(0);
		entry?.SetTrackTime(t);
		entry?.SetMixDuration(0f);
		return t;
	}

	/// <summary>中和（JokerFx.NeutralizeStart）：游戏要播 attack 时先握刀停住，出牌后这么多秒刀落下（attack_from_ready 第 0.14 秒出刀）。</summary>
	public static readonly Dictionary<ulong, float> HoldCut = new();

	/// <summary>中和换来的 knife_in：Postfix 里接上 knife_loop（游戏只会在 attack 后面排 idle）。</summary>
	public static readonly HashSet<ulong> Holding = new();

	/// <summary>出牌时（CardPlayedPatch）记下的施法动作：游戏随后要播 cast 时换成对应版本。</summary>
	public static readonly Dictionary<ulong, Cast> PendingCast = new();

	/// <summary>这个动画状态是不是本地 Joker 的（换动画的入口所有生物都会经过）。</summary>
	internal static NCreature? Owner(MegaAnimationState state)
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null || state.BoundObject is not GodotObject native)
		{
			return null;
		}
		ulong id = native.GetInstanceId();
		foreach (NCreature n in room.CreatureNodes)
		{
			if (n.Entity?.Player is { } p && Joker.Is(p) && n.Visuals?.SpineBody?.TryGetAnimationState()?.BoundObject is GodotObject o && o.GetInstanceId() == id)
			{
				return n;
			}
		}
		return null;
	}

	public static string? Rename(MegaAnimationState state, string requested)
	{
		if (Owner(state) is not NCreature node || node.Visuals.SpineBody is not MegaSprite body)
		{
			return null;
		}
		if (requested == "cast")
		{
			if (!PendingCast.Remove(node.GetInstanceId(), out Cast kind))
			{
				return null;
			}
			// 撕面具甩手那版（cast_big）不用了（用户 2026-10-29：只留简单的召唤 cast）
			string? name = kind switch { Cast.Guard => "cast_guard", Cast.Glove => "cast_glove", _ => null };
			return name != null && body.HasAnimation(name) ? name : null;
		}
		string? current = Current(state);
		if (requested == "attack" && HoldCut.Remove(node.GetInstanceId(), out float cut) && body.HasAnimation("attack_from_ready"))
		{
			// 中和：先握刀停着（还在转刀就接着转完），等头顶的预告函转出来，到点一刀劈下
			if (Joker.FxLayer() is Control layer)
			{
				Fx.After(layer, Mathf.Max(0f, cut - 0.14f), () =>
				{
					bool low = node.Entity.GetHpPercentRemaining() <= 0.25 && body.HasAnimation("low_health_loop");
					state.SetAnimation("attack_from_ready", false, 0);
					state.AddAnimation(low ? "low_health_loop" : "idle_loop", 0f, true, 0);
				});
			}
			if (current != "knife_loop")
			{
				Holding.Add(node.GetInstanceId());
			}
			if (current == "knife_in")
			{
				using MegaTrackEntry? entry = state.GetCurrent(0);
				if (entry != null)
				{
					SkipTo[node.GetInstanceId()] = entry.GetTrackTime();
				}
				return "knife_in";
			}
			return current == "knife_loop" ? "knife_loop" : "knife_in";
		}
		if (requested == "shiv" && current == "aim_in")
		{
			// 转枪还没转完就打出去了：aim_in 就是 shiv 的前 0.18 秒（tools/p5/build_rig_p5.py GUN_IN），
			// 换成 shiv 并从同一时刻接着播 —— 转枪转完、接住、开火，不从头重来也不硬切
			using MegaTrackEntry? entry = state.GetCurrent(0);
			if (entry != null)
			{
				SkipTo[node.GetInstanceId()] = entry.GetTrackTime();
			}
			return "shiv";
		}
		if (requested == "shiv" && current != null && Raised.Contains(current) && body.HasAnimation("shiv_from_aim"))
		{
			return "shiv_from_aim";
		}
		if (requested == "attack" && current == "knife_in")
		{
			// 转刀还没转完就打出去了：knife_in 就是 attack 的前 0.14 秒，换回 attack 并从同一时刻接着播
			using MegaTrackEntry? entry = state.GetCurrent(0);
			if (entry != null)
			{
				SkipTo[node.GetInstanceId()] = entry.GetTrackTime();
			}
			return "attack";
		}
		if (requested == "attack" && current == "knife_loop" && body.HasAnimation("attack_from_ready"))
		{
			return "attack_from_ready";
		}
		if (requested == "attack" && current != null && Slashing.Contains(current) && body.HasAnimation("attack_chain"))
		{
			return "attack_chain";
		}
		return null;
	}

	public static void Raise(CardModel card)
	{
		if (!Joker.OwnsCard(card) || !LocalContext.IsMe(card.Owner))
		{
			return;
		}
		if (!Joker.IsShot(card))
		{
			if (card.Type == MegaCrit.Sts2.Core.Entities.Cards.CardType.Attack)
			{
				Ready(card);
			}
			return;
		}
		NCreature? node = Joker.Node(card.Owner);
		MegaSprite? body = node?.Visuals?.SpineBody;
		if (body == null || !body.HasAnimation("aim_loop") || body.TryGetAnimationState() is not MegaAnimationState state)
		{
			return;
		}
		string? current = Current(state);
		if (current != null && Raised.Contains(current))
		{
			if (current is "shiv" or "shiv_from_aim")
			{
				state.SetAnimation("aim_loop", true, 0);          // 上一枪还没收完：直接回到瞄准
			}
			return;
		}
		if (current is not ("idle_loop" or "low_health_loop" or "aim_out" or "knife_out"))
		{
			return;                                               // 正在出别的牌 / 受击：不打断（刚收起的可以直接换成举枪）
		}
		state.SetAnimation("aim_in", false, 0);
		state.AddAnimation("aim_loop", 0f, true, 0);
	}

	/// <summary>拿起打击类的攻击牌：腰侧转刀起手、摆好准备姿势停着（knife_in → knife_loop），打出去直接上撩。</summary>
	private static void Ready(CardModel card)
	{
		NCreature? node = Joker.Node(card.Owner);
		MegaSprite? body = node?.Visuals?.SpineBody;
		if (body == null || !body.HasAnimation("knife_loop") || body.TryGetAnimationState() is not MegaAnimationState state)
		{
			return;
		}
		if (Current(state) is not ("idle_loop" or "low_health_loop" or "aim_out" or "knife_out"))
		{
			return;                                               // 正在出别的牌 / 受击 / 已经起手：不打断（刚收起的可以直接换）
		}
		state.SetAnimation("knife_in", false, 0);
		state.AddAnimation("knife_loop", 0f, true, 0);
	}

	public static void Lower(NCreature? node)
	{
		MegaSprite? body = node?.Visuals?.SpineBody;
		if (body == null || body.TryGetAnimationState() is not MegaAnimationState state)
		{
			return;
		}
		string? current = Current(state);
		string? putAway = current is "aim_in" or "aim_loop" ? "aim_out" : current is "knife_in" or "knife_loop" ? "knife_out" : null;
		if (putAway == null || !body.HasAnimation(putAway))
		{
			return;
		}
		bool low = node!.Entity.GetHpPercentRemaining() <= 0.25 && body.HasAnimation("low_health_loop");
		state.SetAnimation(putAway, false, 0);
		state.AddAnimation(low ? "low_health_loop" : "idle_loop", 0f, true, 0);
	}

	public static void LowerLocal()
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null)
		{
			return;
		}
		foreach (NCreature n in room.CreatureNodes.Where(n => n.Entity?.Player is { } p && Joker.Is(p) && LocalContext.IsMe(p)))
		{
			Lower(n);
		}
	}

	private static string? Current(MegaAnimationState state)
	{
		try
		{
			return state.GetCurrentAnimationName(0);
		}
		catch (System.Exception)
		{
			return null;
		}
	}
}

/// <summary>换动画的唯一入口：Joker 正举着枪 / 正在收刀时，把 shiv / attack 换成接续版本。</summary>
[HarmonyPatch(typeof(MegaAnimationState), nameof(MegaAnimationState.SetAnimation))]
public static class AnimRenamePatch
{
	public static void Prefix(MegaAnimationState __instance, ref string animationName)
	{
		if (animationName is not ("shiv" or "attack" or "cast"))
		{
			return;
		}
		string requested = animationName;
		string? replacement = null;
		ModEntry.Safe("AnimRename", () => replacement = Aim.Rename(__instance, requested));
		if (replacement != null)
		{
			animationName = replacement;
		}
	}

	public static void Postfix(MegaAnimationState __instance, string animationName)
	{
		if (animationName == "knife_in")
		{
			// 中和换来的握刀：接着转完刀、停在准备姿势
			ModEntry.Safe("AnimHold", () =>
			{
				if (Aim.Owner(__instance) is NCreature node && Aim.SkipTo.ContainsKey(node.GetInstanceId()) | Aim.Holding.Remove(node.GetInstanceId()))
				{
					Aim.ApplySkip(node, __instance);
					__instance.AddAnimation("knife_loop", 0f, true, 0);
				}
			});
			return;
		}
		if (animationName is not ("shiv" or "shiv_from_aim" or "attack" or "attack_chain" or "attack_from_ready"))
		{
			return;
		}
		ModEntry.Safe("AnimFx", () =>
		{
			if (Aim.Owner(__instance) is not NCreature node)
			{
				return;
			}
			float skipped = animationName is "shiv" or "attack" ? Aim.ApplySkip(node, __instance) : 0f;
			JokerFx.OnAnim(node, animationName, skipped);
		});
	}
}

/// <summary>单体牌开始选目标（拿起小刀牌）：举枪。</summary>
[HarmonyPatch]
public static class StartTargetingPatch
{
	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(NTargetManager).GetMethods(BindingFlags.Instance | BindingFlags.Public)
			.Where(m => m.Name == "StartTargeting" && m.GetParameters().Length > 1 && m.GetParameters()[1].ParameterType == typeof(Control));

	public static void Postfix(object[] __args)
	{
		ModEntry.Safe("StartTargeting", () =>
		{
			if (__args[1] is NCard { Model: CardModel card })
			{
				Aim.Raise(card);
			}
		});
	}
}

/// <summary>群体牌（刀扇下的小刀）拖进出牌区：举枪。</summary>
[HarmonyPatch(typeof(NCardPlay), "ShowMultiCreatureTargetingVisuals")]
public static class MultiTargetingPatch
{
	public static void Postfix(NCardPlay __instance)
	{
		ModEntry.Safe("MultiTargeting", () =>
		{
			if (Traverse.Create(__instance).Property("Card").GetValue() is CardModel card)
			{
				Aim.Raise(card);
			}
		});
	}
}

/// <summary>出牌流程收尾：取消（isFinished = false）就收枪；打出则交给 shiv_from_aim 接着开火。</summary>
[HarmonyPatch(typeof(NCardPlay), "Cleanup")]
public static class CardPlayCleanupPatch
{
	public static void Prefix(bool isFinished)
	{
		if (!isFinished)
		{
			ModEntry.Safe("CardPlayCleanup", Aim.LowerLocal);
		}
	}
}

[HarmonyPatch(typeof(NTargetManager), "FinishTargeting")]
public static class FinishTargetingPatch
{
	public static void Postfix(bool cancel)
	{
		if (cancel)
		{
			ModEntry.Safe("FinishTargeting", Aim.LowerLocal);
		}
	}
}
