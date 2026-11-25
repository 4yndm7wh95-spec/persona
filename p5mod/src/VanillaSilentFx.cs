using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;

namespace P5Joker;

/// <summary>
/// 静默猎手原版自带的出牌 / 能力特效，Joker 一律不放（用户 2026-11-13：赶紧都屏蔽掉，别跟我们的重叠）。
/// 以前是一张牌一张牌零散地关（ThinSlicePatch、PoisonImpactPatch、SkillVanilla*Patch……），漏了好几个（毒雾每回合的绿色毒气等）。
/// 这里统一：Joker 出牌后 2.5 秒内（JokerFx.LastPlayMs），以及 Joker 的毒雾 / 腐蚀波能力触发时，下面这些原版特效都藏掉：
/// · 猎手牌和能力专用的特效节点（N*Vfx.Create 的结果设为不可见；按类名找，游戏更新改名 / 删掉了就跳过）；
/// · VfxCmd 按路径放的特效：打在敌人身上的刀光 / 刺击 / 钝击 / 獠牙，放在 Joker 身上的肾上腺素 / 飞斩。
/// 敌人自己的特效不受影响（敌人回合在窗口外；按路径放的只拦打在敌人身上的攻击类和打在 Joker 身上的自身类）。
/// </summary>
public static class VanillaSilentFx
{
	private static ulong _extraUntil;

	/// <summary>现在要不要藏原版猎手特效。</summary>
	public static bool Hiding
	{
		get
		{
			ulong now = Time.GetTicksMsec();
			return (JokerFx.LastPlayMs > 0 && now - JokerFx.LastPlayMs < 2500) || now < _extraUntil;
		}
	}

	/// <summary>Joker 的能力在出牌以外的时刻触发（回合开始的毒雾等）：接下来 seconds 秒也藏。</summary>
	public static void HideFor(float seconds) => _extraUntil = Math.Max(_extraUntil, Time.GetTicksMsec() + (ulong)(seconds * 1000f));

	/// <summary>打在敌人身上的攻击类原版特效（Joker 的攻击已经由 HitFx / 牌的专属特效表现）。</summary>
	internal static readonly HashSet<string> OnEnemyPaths = new()
	{
		"vfx/vfx_attack_slash", "vfx/vfx_dramatic_stab", "vfx/vfx_bite", "vfx/vfx_flying_slash",   // 钝击不在这里：群蛇形态的 SerpentFormHitPatch 要先接管它
	};

	/// <summary>放在 Joker 自己身上的原版特效。</summary>
	internal static readonly HashSet<string> OnSelfPaths = new() { "vfx/vfx_flying_slash", "vfx/vfx_adrenaline", "vfx/vfx_block" };

	internal static bool IsJoker(Creature? c) => c != null && c.IsPlayer && c.Player is { } p && Joker.Is(p);
}

/// <summary>猎手专用的特效节点：Create 出来就设成不可见。</summary>
[HarmonyPatch]
public static class VanillaSilentNodePatch
{
	private static readonly string[] Types =
	{
		"NGaseousImpactVfx", "NSmokePuffVfx", "NSmokyVignetteVfx", "NNightmareHandsVfx", "NDaggerSprayFlurryVfx", "NDaggerSprayImpactVfx",
		"NFanOfKnivesVfx", "NStabVfx", "NThinSliceVfx", "NPoisonImpactVfx", "NSplashVfx", "NLiquidOverlayVfx", "NGrandFinaleVfx",
		"NGrandFinaleImpactVfx", "NShivThrowVfx",
	};

	private static readonly string[] Namespaces =
	{
		"MegaCrit.Sts2.Core.Nodes.Vfx.", "MegaCrit.Sts2.Core.Nodes.Vfx.Cards.", "MegaCrit.Sts2.Core.Nodes.Vfx.Forms.",
	};

	public static IEnumerable<MethodBase> TargetMethods()
	{
		Assembly asm = typeof(VfxCmd).Assembly;
		foreach (string name in Types)
		{
			Type? t = Namespaces.Select(ns => asm.GetType(ns + name)).FirstOrDefault(x => x != null)
				?? asm.GetTypes().FirstOrDefault(x => x.Name == name);
			if (t == null) continue;
			foreach (MethodInfo m in t.GetMethods(BindingFlags.Public | BindingFlags.Static).Where(m => m.Name == "Create" && !m.IsGenericMethod))
			{
				yield return m;
			}
		}
	}

	public static void Postfix(object? __result)
	{
		if (__result is CanvasItem c && VanillaSilentFx.Hiding)
		{
			c.Visible = false;
		}
	}
}

/// <summary>按路径放的原版特效。</summary>
[HarmonyPatch]
public static class VanillaSilentPathPatch
{
	public static IEnumerable<MethodBase> TargetMethods()
	{
		yield return AccessTools.Method(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreatureCenter));
		yield return AccessTools.Method(typeof(VfxCmd), nameof(VfxCmd.PlayOnCreature));
	}

	public static bool Prefix(Creature target, string path)
	{
		bool skip = false;
		ModEntry.Safe("VanillaSilentPath", () =>
		{
			if (target == null || path == null) return;
			if (VanillaSilentFx.IsJoker(target))
			{
				skip = path == "vfx/vfx_block" || (VanillaSilentFx.Hiding && VanillaSilentFx.OnSelfPaths.Contains(path));
			}
			else if (!target.IsPlayer)
			{
				skip = VanillaSilentFx.Hiding && VanillaSilentFx.OnEnemyPaths.Contains(path);
			}
		});
		return !skip;
	}
}

/// <summary>原版全屏特效（肾上腺素）。</summary>
[HarmonyPatch(typeof(VfxCmd), nameof(VfxCmd.PlayFullScreenInCombat))]
public static class VanillaSilentFullScreenPatch
{
	public static bool Prefix(Creature? spawner) => !(VanillaSilentFx.Hiding && VanillaSilentFx.IsJoker(spawner));
}

/// <summary>Joker 的毒雾每回合开始在敌人身上放绿色毒气：这次触发也藏（毒雾常驻的咒怨雾由 FumesField 表现）。</summary>
[HarmonyPatch(typeof(NoxiousFumesPower), nameof(NoxiousFumesPower.AfterSideTurnStart))]
public static class VanillaFumesTickPatch
{
	public static void Prefix(NoxiousFumesPower __instance)
	{
		ModEntry.Safe("FumesTick", () =>
		{
			if (!VanillaSilentFx.IsJoker(__instance.Owner)) return;
			VanillaSilentFx.HideFor(2f);
			Stage.Safe("fumes pulse", () => FumesField.Current?.Pulse());
		});
	}
}
