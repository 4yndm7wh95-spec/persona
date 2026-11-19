using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Runs;

namespace P5Joker;

/// <summary>原版加载结束后的菜单和非战斗房间才可预热。</summary>
public static class WarmupGame
{
	private static readonly AccessTools.FieldRef<NCharacterSelectScreen, NCharacterSelectButton> Selected =
		AccessTools.FieldRefAccess<NCharacterSelectScreen, NCharacterSelectButton>("_selectedButton");

	public static void Install()
	{
		Warmup.IdleContext = IdleContext;
		Warmup.RefreshPlan = RefreshPlan;
		Warmup.StartMenu();
	}

	private static bool Started(NGame? game) => game != null && game.GameStartupComplete.IsCompletedSuccessfully;

	private static string? IdleContext()
	{
		NGame? game = NGame.Instance;
		if (!Started(game) || game!.Transition.InTransition) return null;
		if (game.CurrentRunNode == null) return game.MainMenu?.IsVisibleInTree() == true ? "menu" : null;
		CombatManager combat = CombatManager.Instance;
		if (combat.IsInProgress || combat.IsStarting || combat.IsEnding) return null;
		return game.CurrentRunNode.IsVisibleInTree() ? "non-combat" : null;
	}

	private static void RefreshPlan()
	{
		NGame? game = NGame.Instance;
		if (!Started(game)) return;
		if (game!.CurrentRunNode == null)
		{
			Warmup.RequestMenu();
			if (game.MainMenu?.SubmenuStack is { SubmenusOpen: true } stack && stack.Peek() is NCharacterSelectScreen screen
				&& Selected(screen)?.Character is Silent)
				Warmup.RequestJoker();
			return;
		}
		if (!RunManager.Instance.IsInProgress) return;
		var state = RunManager.Instance.DebugOnlyGetState();
		if (state == null) return;
		foreach (var player in state.Players.Where(Joker.Is))
		{
			Warmup.RequestJoker();
			Warmup.RequestAllOut();
			// 生成、复制或控制台给的华丽收场可直接进战斗手牌，不经过牌组。
			Warmup.RequestFinale();
			Warmup.RequestReward();
			foreach (var card in player.Deck.Cards)
			{
				Warmup.RequestCard(card.GetType().Name);
				if (Aim.Summons(card)) Warmup.RequestPersona();
			}
		}
	}
}

// 与原版的进局加载并行，保证直接读档进战斗也会先提交华丽收场的开场材质。
[HarmonyPatch(typeof(PreloadManager), nameof(PreloadManager.LoadRunAssets))]
public static class WarmupRunAssetsPatch
{
	public static void Postfix(IEnumerable<CharacterModel> __0, ref Task __result)
	{
		try
		{
			if (__0.Any(character => character is Silent) && Engine.GetMainLoop() is SceneTree tree)
				__result = Warmup.PrepareFinaleForRun(tree, __result);
		}
		catch (Exception e) { Stage.Report("finale preload", e); }
		Warmup.WaitFor(__result);
	}
}

// 其他原版加载只暂停空闲队列。
[HarmonyPatch]
public static class WarmupLoadingPatch
{
	public static IEnumerable<MethodBase> TargetMethods() => typeof(PreloadManager)
		.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
		.Where(method => method.Name is "LoadAssetSets" or "LoadCommonAndMainMenuAssets" or "LoadRoomAssets")
		.Where(method => typeof(Task).IsAssignableFrom(method.ReturnType));

	public static void Postfix(Task __result) => Warmup.WaitFor(__result);
}
