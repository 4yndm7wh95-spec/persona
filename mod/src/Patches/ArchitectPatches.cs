using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Ancients;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using P3Makoto.Core;
using P3Makoto.Vfx;

namespace P3Makoto.Patches;

/// <summary>
/// 伟大封印（v0.14.3）：塔顶与建筑师的会面换成 P3 结局 —— 房间、台词、最后的按钮、结局演出（见 Vfx/GreatSeal.cs）。
/// 只换表现：对话流程、胜利结算（RunManager.WinRun）都照原版走。
/// </summary>
[HarmonyPatch(typeof(LocString), nameof(LocString.GetFormattedText))]
public static class ArchitectLinePatch
{
	public static bool Prefix(LocString __instance, ref string __result)
	{
		if (__instance.LocTable != "ancients")                // 热路径：绝大多数文本直接放行
		{
			return true;
		}
		string? line = null;
		ModEntry.Safe("architect line", () => line = GreatSeal.Line(__instance.LocTable, __instance.LocEntryKey));
		if (line == null)
		{
			return true;
		}
		__result = line;
		return false;
	}
}

[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
public static class ArchitectLineRawPatch
{
	public static bool Prefix(LocString __instance, ref string __result) => ArchitectLinePatch.Prefix(__instance, ref __result);
}

/// <summary>进房间：盖上塔尔塔洛斯顶层的背景。</summary>
[HarmonyPatch(typeof(TheArchitect), nameof(TheArchitect.OnRoomEnter))]
public static class ArchitectRoomPatch
{
	public static void Postfix(TheArchitect __instance)
	{
		if (!Makoto.Is(__instance.Owner))
		{
			return;
		}
		void Apply() => ModEntry.Safe("architect room", () =>
		{
			if (NCombatRoom.Instance is not NCombatRoom room || !GodotObject.IsInstanceValid(room))
			{
				return;
			}
			// 只在建筑师确实在这个房间里时才盖背景（补的几次可能落在玩家已经离开之后）
			if (Traverse.Create(__instance).Property("ArchitectCreature").GetValue() is Creature c && room.GetCreatureNode(c) == null)
			{
				return;
			}
			GreatSeal.Room(room);
		});
		Callable.From(Apply).CallDeferred();
		// 正式流程里房间场景 / 原版背景可能稍后才建好：再补几次（Room 是幂等的，已加过就只把它挪回最上层）
		if (Engine.GetMainLoop() is SceneTree tree)
		{
			foreach (double at in new[] { 0.3, 1.0, 2.5 })
			{
				tree.CreateTimer(at).Timeout += Apply;
			}
		}
	}
}

/// <summary>最后的"继续"按钮 → 「伟大封印」。</summary>
[HarmonyPatch(typeof(NEventOptionButton), nameof(NEventOptionButton._Ready))]
public static class ArchitectButtonPatch
{
	public static void Postfix(NEventOptionButton __instance)
	{
		ModEntry.Safe("seal button", () =>
		{
			if (__instance.Event is TheArchitect && __instance.Option.TextKey == "PROCEED" && Makoto.Is(__instance.Event.Owner))
			{
				GreatSeal.DecorateButton(__instance);
			}
		});
	}
}

/// <summary>按下「伟大封印」：玩家的攻击演出换成结局演出（时长 GreatSeal.Duration）。</summary>
[HarmonyPatch(typeof(TheArchitect), "AnimPlayerAttackIfNecessary")]
public static class ArchitectEndingPatch
{
	public static bool Prefix(TheArchitect __instance, ArchitectAttackers attackers, ref Task<bool> __result)
	{
		if (attackers == ArchitectAttackers.None || !Makoto.Is(__instance.Owner))
		{
			return true;
		}
		NCreature? player = Makoto.Node(__instance.Owner);
		NCreature? architect = null;
		ModEntry.Safe("architect node", () =>
		{
			if (Traverse.Create(__instance).Property("ArchitectCreature").GetValue() is Creature c)
			{
				architect = NCombatRoom.Instance?.GetCreatureNode(c);
			}
		});
		__result = GreatSeal.Play(player, architect);
		return false;
	}
}

/// <summary>建筑师不再反击（它已经被封印了）。</summary>
[HarmonyPatch(typeof(TheArchitect), "AnimArchitectAttackIfNecessary")]
public static class ArchitectNoCounterPatch
{
	public static bool Prefix(TheArchitect __instance, ref Task __result)
	{
		if (!Makoto.Is(__instance.Owner))
		{
			return true;
		}
		__result = Task.CompletedTask;
		return false;
	}
}

/// <summary>
/// v0.14.28（用户：建筑师房间里结算画面「胜利……？」出来后按 ESC，背景变回之前的房间）：
/// 结算画面是叠加界面（人物被挪到它上面、暗红底遮住房间）；暂停时游戏会临时藏起叠加界面，后面的塔顶背景、对话气泡、结局按钮就露出来。
/// 结算画面打开时（人物已经挪走）把建筑师房间整个藏起来，暂停时背后是空的暗底。只在我们的塔顶房间生效。
/// </summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen.NGameOverScreen), nameof(MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen.NGameOverScreen.AfterOverlayOpened))]
public static class ArchitectGameOverPatch
{
	public static void Postfix()
	{
		ModEntry.Safe("architect game over", () =>
		{
			if (NEventRoom.Instance is NEventRoom ev && GodotObject.IsInstanceValid(ev)
				&& ev.EmbeddedCombatRoom?.GetNodeOrNull("%BgContainer/P3TartarusTop") != null)
			{
				ev.Visible = false;
				GD.Print($"{ModEntry.Tag} architect room hidden behind game over screen");
			}
		});
	}
}
