using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Forms;

namespace P5Joker;

/// <summary>
/// 群蛇形态的常驻特效（2026-11-11）：原版能力挂上时在角色身上放一个 NSerpentFormVfx（骨骼上跟着几条小蛇），
/// 每打出一张牌能力触发：OnEffectTriggered + 对随机敌人放 vfx/vfx_attack_blunt 再造成伤害。
/// Joker 的换成：原版的小蛇藏掉，常驻一条光蛇盘在身上（Vfx/SerpentMantle.cs）；触发时光蛇扑出去咬那个敌人，原版的钝击特效不放。
/// </summary>
public static class SerpentFormPlay
{
	private static ulong _triggeredMs;

	/// <summary>能力挂上（或刷新）：常驻的蛇开始；起手还在演就等它扎进胸口再浮现。</summary>
	internal static void Begin(Creature owner)
	{
		if (owner.Player is not { } player || !Joker.Is(player) || Joker.Node(player) is not NCreature node)
		{
			return;
		}
		if (Joker.FxLayer() is not Control front || Joker.FxLayer(back: true) is not Control back)
		{
			return;
		}
		ulong now = Time.GetTicksMsec();
		// 起手在出牌后 Aim.HitFor 才开始，比能力挂上晚：至少等起手演到扎进胸口
		float delay = SerpentAscend.BusyUntilMs > now ? (SerpentAscend.BusyUntilMs - now) / 1000f : SerpentAscend.StrikeAt + 0.6f;
		SerpentMantle.Start(front, back, () => Body(node), delay);
	}

	private static (Vector2 Feet, float Tall, float Face)? Body(NCreature node)
	{
		if (!GodotObject.IsInstanceValid(node) || node.Entity is not { } c || c.IsDead)
		{
			return null;
		}
		Vector2 feet = Joker.PosePoint(node, ReadyFx.Feet);
		float tall = (feet - Joker.PosePoint(node, new Vector2(815f, 120f))).Length();
		return (feet, tall, Joker.Facing(node));
	}

	internal static void Triggered() => _triggeredMs = Time.GetTicksMsec();

	/// <summary>能力触发后紧跟着的那次钝击特效：换成光蛇扑过去咬。返回 true = 已经接管（原版不放）。</summary>
	internal static bool TakeHit(Creature target)
	{
		if (Time.GetTicksMsec() - _triggeredMs > 600 || SerpentMantle.Current is not { } m || NCombatRoom.Instance is not NCombatRoom room)
		{
			return false;
		}
		if (room.GetCreatureNode(target) is not NCreature n || !GodotObject.IsInstanceValid(n))
		{
			return false;
		}
		_triggeredMs = 0;
		m.Strike(n.Hitbox.GetGlobalRect().GetCenter());
		return true;
	}
}

[HarmonyPatch(typeof(NSerpentFormVfx), nameof(NSerpentFormVfx.Create))]
public static class SerpentFormVfxCreatePatch
{
	public static void Postfix(Creature target, NSerpentFormVfx? __result)
	{
		if (target.Player is { } p && Joker.Is(p))
		{
			if (__result != null)
			{
				__result.Visible = false;
			}
			Stage.Safe("serpent mantle", () => SerpentFormPlay.Begin(target));
		}
	}
}

[HarmonyPatch(typeof(NSerpentFormVfx), nameof(NSerpentFormVfx.OnEffectTriggered))]
public static class SerpentFormVfxTriggerPatch
{
	public static void Postfix(NSerpentFormVfx __instance)
	{
		if (!__instance.Visible)
		{
			SerpentFormPlay.Triggered();
		}
	}
}

[HarmonyPatch(typeof(NFormVfx), nameof(NFormVfx.SetActive))]
public static class SerpentFormVfxActivePatch
{
	public static void Postfix(NFormVfx __instance, bool isActive)
	{
		if (!isActive && __instance is NSerpentFormVfx && !__instance.Visible)
		{
			SerpentMantle.StopCurrent();
		}
	}
}

[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreatureCenter))]
public static class SerpentFormHitPatch
{
	public static bool Prefix(Creature target, string path)
	{
		if (path != "vfx/vfx_attack_blunt" || target == null)
		{
			return true;
		}
		bool took = false;
		Stage.Safe("serpent strike", () => took = SerpentFormPlay.TakeHit(target));
		return !took;
	}
}
