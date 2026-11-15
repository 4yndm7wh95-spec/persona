using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace P5Joker;

public static class RetainHand
{
	/// <summary>回合结束时，本地 Joker 保留下来的手牌（按手里从左到右）。</summary>
	public static void OnRetained(IEnumerable<CardModel> cards)
	{
		if (NPlayerHand.Instance is not { } hand) return;
		var nodes = cards.Select(c => hand.GetCard(c)).OfType<NCard>().Where(GodotObject.IsInstanceValid)
			.OrderBy(n => n.GlobalPosition.X).ToList();
		if (nodes.Count == 0) return;
		Sfx.Take("gx_card_slide", 0.08f);
		Sfx.Take("gx_card_stack", 0.1f, 0.2f);
		for (int i = 0; i < nodes.Count; i++)
			RetainFx.Play(nodes[i].Body ?? nodes[i], 0.08f * i);
	}
}

/// <summary>回合结束清手牌之后（Hook.AfterFlush）：保留下来的牌放「封」特效。参数按类型找，不靠参数名（游戏版本间改名也不坏）。</summary>
[HarmonyPatch(typeof(Hook), nameof(Hook.AfterFlush))]
public static class RetainFlushPatch
{
	public static void Prefix(object[] __args) => ModEntry.Safe("RetainFlush", () =>
	{
		Player? player = __args.OfType<Player>().FirstOrDefault();
		if (player == null || !Joker.Is(player) || !LocalContext.IsMe(player)) return;
		var piles = __args.OfType<IReadOnlyCollection<CardModel>>().ToList();
		if (piles.Count == 0) return;
		var retained = piles[^1];
		if (retained.Count == 0) return;
		RetainHand.OnRetained(retained.ToList());
	});
}
