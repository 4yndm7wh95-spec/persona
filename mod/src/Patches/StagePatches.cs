using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using P3Makoto.Core;
using P3Makoto.Vfx;

namespace P3Makoto.Patches;

/// <summary>战斗舞台演出（StageFx）的挂载点。只在结城理的对局里生效，其他角色保持原版。</summary>
internal static class Stage
{
	/// <summary>当前战斗里有没有结城理（顺便记住"这一局是结城理"，给不在战斗房间里的界面用，例如选牌）。</summary>
	public static bool MakotoCombat()
	{
		NCombatRoom? room = NCombatRoom.Instance;
		if (room == null || !GodotObject.IsInstanceValid(room))
		{
			return false;
		}
		bool yes = room.CreatureNodes.Any(n => n.Entity.Player is Player p && Makoto.Is(p));
		MakotoRun = yes;
		return yes;
	}

	public static bool MakotoRun;
}

/// <summary>玩家回合横幅换成 P3R 风格（原版的不再创建：返回 null，原版调用处 AddChildSafely 会忽略 null）。</summary>
[HarmonyPatch(typeof(NPlayerTurnBanner), nameof(NPlayerTurnBanner.Create))]
public static class PlayerTurnBannerPatch
{
	public static bool Prefix(int roundNumber, ref NPlayerTurnBanner? __result)
	{
		bool replaced = false;
		ModEntry.Safe("turn banner", () =>
		{
			if (Stage.MakotoCombat() && Makoto.FxLayer() is Control front)
			{
				StageFx.TurnBanner(front, player: true, roundNumber);
				replaced = true;
			}
		});
		if (replaced)
		{
			__result = null;
			return false;
		}
		return true;
	}
}

[HarmonyPatch(typeof(NEnemyTurnBanner), nameof(NEnemyTurnBanner.Create))]
public static class EnemyTurnBannerPatch
{
	public static bool Prefix(ref NEnemyTurnBanner? __result)
	{
		bool replaced = false;
		ModEntry.Safe("enemy banner", () =>
		{
			if (Stage.MakotoCombat() && Makoto.FxLayer() is Control front)
			{
				StageFx.TurnBanner(front, player: false, 0);
				replaced = true;
			}
		});
		if (replaced)
		{
			__result = null;
			return false;
		}
		return true;
	}
}

/// <summary>敌人倒下：交叉色块斩 + 小水之圆 + 玻璃碎片（叠在原版死亡动画上）。</summary>
[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
public static class KnockDownPatch
{
	public static void Postfix(NCreature __instance)
	{
		ModEntry.Safe("knock down", () =>
		{
			if (__instance.Entity.IsMonster && Stage.MakotoCombat() && Makoto.FxLayer() is Control front)
			{
				StageFx.KnockDown(front, __instance.VfxSpawnPosition);
			}
		});
	}
}

/// <summary>战斗胜利：P3R 结算风格的大字演出。异步钩子可能不在主线程上，延迟到主线程执行。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCombatVictory))]
public static class VictoryPatch
{
	public static void Prefix()
	{
		Callable.From(() => ModEntry.Safe("victory", () =>
		{
			if (Stage.MakotoCombat() && Makoto.FxLayer() is Control front)
			{
				StageFx.Victory(front);
			}
		})).CallDeferred();
	}
}

/// <summary>选牌界面：牌后面的水之圆背景 + 每张牌落位时的十字闪光（每次打开 / 重抽都只加一次背景）。</summary>
[HarmonyPatch(typeof(NCardRewardSelectionScreen), nameof(NCardRewardSelectionScreen.RefreshOptions))]
public static class RewardBackdropPatch
{
	public static void Postfix(NCardRewardSelectionScreen __instance)
	{
		ModEntry.Safe("reward backdrop", () =>
		{
			if (!Stage.MakotoRun || __instance.GetNodeOrNull("P3RewardBackdrop") != null)
			{
				return;
			}
			if (Traverse.Create(__instance).Field("_cardRow").GetValue() is not Control row)
			{
				return;
			}
			Vector2 center = row.GetGlobalRect().GetCenter();
			StageFx.RewardBackdrop(__instance, center, () =>
				row.GetChildren().OfType<Control>().Select(c => c.GetGlobalRect().GetCenter()).ToArray());
		});
	}
}
