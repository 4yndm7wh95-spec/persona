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
			if (Stage.MakotoCombat() && (Makoto.FxLayer(back: true) ?? Makoto.FxLayer()) is Control back)
			{
				StageFx.TurnBanner(back, player: true, roundNumber);
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
			if (Stage.MakotoCombat() && (Makoto.FxLayer(back: true) ?? Makoto.FxLayer()) is Control back)
			{
				StageFx.TurnBanner(back, player: false, 0);
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
				StageFx.KnockDown(front, Makoto.FxLayer(back: true), __instance.VfxSpawnPosition);
			}
		});
	}
}

/// <summary>战斗胜利：P3R 结算风格的大字演出。异步钩子可能不在主线程上，延迟到主线程执行。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCombatVictory))]
public static class VictoryPatch
{
	public static void Prefix(MegaCrit.Sts2.Core.Rooms.CombatRoom room)
	{
		// v0.14.8：精英 / Boss 战已经播了总攻击（AllOutPatch，AfterCombatEnd 在 AfterCombatVictory 之前），不再叠普通胜利演出
		if (room.RoomType is MegaCrit.Sts2.Core.Rooms.RoomType.Elite or MegaCrit.Sts2.Core.Rooms.RoomType.Boss)
		{
			return;
		}
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
			if (!Stage.MakotoRun || Traverse.Create(__instance).Field("_cardRow").GetValue() is not Control row)
			{
				return;
			}
			// v0.14.1：P3R Shuffle Time（明牌）—— 每次打开 / 重抽都播洗牌入场（接管原版的滑入 Tween）
			if (Traverse.Create(__instance).Field("_cardTween").GetValue() is Tween vanillaTween)
			{
				vanillaTween.Kill();
			}
			bool first = __instance.GetNodeOrNull("P3RewardBackdrop") == null;
			StageFx.ShuffleIntro(__instance, row, first);
			if (!first)
			{
				return;
			}
			Vector2 center = row.GetGlobalRect().GetCenter();
			StageFx.RewardBackdrop(__instance, center, () =>
				row.GetChildren().OfType<Control>().Select(c => c.GetGlobalRect().GetCenter()).ToArray());
		});
	}
}

/// <summary>Shuffle Time：选中一张牌 → 那张牌爆出阿卡纳光（ShuffleTime_ArcanaBurst），其余的牌轻轻沉下去。</summary>
[HarmonyPatch(typeof(NCardRewardSelectionScreen), "SelectCard")]
public static class RewardPickPatch
{
	public static void Prefix(NCardRewardSelectionScreen __instance, MegaCrit.Sts2.Core.Nodes.Cards.Holders.NCardHolder cardHolder)
	{
		ModEntry.Safe("reward pick", () =>
		{
			if (Stage.MakotoRun)
			{
				StageFx.ShufflePick(__instance, cardHolder);
			}
		});
	}
}

/// <summary>
/// 商店里的人物：原版 regent_merchant.tscn 用 0.47 倍显示储君（坐着、矮）；结城理是站着的全身骨骼，同样倍率会高到顶住屏幕上沿。
/// 只缩这个场景（文件名判断），脚底是骨骼原点，缩放后仍踩在原位置。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantCharacter), nameof(MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantCharacter._Ready))]
public static class MerchantScalePatch
{
	private const float Scale = 0.34f;

	public static void Postfix(MegaCrit.Sts2.Core.Nodes.Screens.Shops.NMerchantCharacter __instance)
	{
		ModEntry.Safe("merchant scale", () =>
		{
			if (__instance.SceneFilePath.Contains("regent_merchant") && __instance.GetChildCount() > 0 && __instance.GetChild(0) is Node2D sprite)
			{
				sprite.Scale = Vector2.One * Scale;
			}
		});
	}
}

/// <summary>
/// 总攻击（v0.14.1）：结城理赢下精英 / Boss 战时，在战斗结束流程（AfterCombatEnd → 胜利结算）前插入约 3.7s 的总攻击演出。
/// 只推迟表现，不改任何结果。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCombatEnd))]
public static class AllOutPatch
{
	public static void Postfix(MegaCrit.Sts2.Core.Rooms.CombatRoom room, MegaCrit.Sts2.Core.Combat.ICombatState? combatState, ref System.Threading.Tasks.Task __result)
	{
		bool play = false;
		ModEntry.Safe("allout check", () =>
		{
			play = room.RoomType is MegaCrit.Sts2.Core.Rooms.RoomType.Elite or MegaCrit.Sts2.Core.Rooms.RoomType.Boss
				&& Stage.MakotoCombat()
				&& combatState != null
				&& System.Linq.Enumerable.All(combatState.Enemies, e => !e.IsAlive)
				&& System.Linq.Enumerable.Any(combatState.PlayerCreatures, p => p.IsAlive);
		});
		if (!play)
		{
			return;
		}
		Callable.From(() => ModEntry.Safe("allout", () =>
		{
			if (Makoto.FxLayer() is Control front && NCombatRoom.Instance is { } r)
			{
				var pts = new System.Collections.Generic.List<Vector2>();
				foreach (NCreature n in r.CreatureNodes)
				{
					if (GodotObject.IsInstanceValid(n) && n.Entity.IsMonster)
					{
						pts.Add(n.VfxSpawnPosition);
					}
				}
				AllOut.Play(front, pts);
			}
		})).CallDeferred();
		__result = Delay(__result);
	}

	private static async System.Threading.Tasks.Task Delay(System.Threading.Tasks.Task inner)
	{
		await inner;
		await System.Threading.Tasks.Task.Delay((int)(AllOut.Duration * 1000f));
	}
}
