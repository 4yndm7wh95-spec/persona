using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes.Debug;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;

namespace P3TestMod;

/// <summary>
/// 测试按钮的联机同步（v1.2）：所有会改动游戏状态的按钮都不直接改数值，而是发一条开发者控制台命令。
/// 和原版控制台完全一样的流程（DevConsole.ProcessCommand）：
/// · 单人：直接执行；
/// · 联机：包成 ConsoleCmdGameAction 交给 ActionQueueSynchronizer —— 房主排进动作队列并广播，客机先发给房主；
///   每个玩家的游戏都在同一个位置执行同一条命令（发命令的那个玩家作为 issuingPlayer），状态保持一致。
/// 原版命令：energy / stars / card / kill；本 mod 自己的：p3test（见 TestCmd）。所有玩家都要装测试 mod（它标记了影响玩法，原版会在联机握手时检查）。
/// </summary>
public static class Net
{
	public static RunState? Run => RunManager.Instance.IsInProgress ? RunManager.Instance.DebugOnlyGetState() : null;

	public static bool IsMultiplayer => RunManager.Instance.IsInProgress && !RunManager.Instance.IsSingleplayerOrFakeMultiplayer;

	public static string Send(string cmd)
	{
		RunState? run = Run;
		Player? me = run != null ? LocalContext.GetMe(run) : null;
		if (me == null)
		{
			return "不在一局游戏里";
		}
		MpLog.Event($"send cmd '{cmd}' (local player {me.NetId}, multiplayer={IsMultiplayer}, combat={CombatManager.Instance.IsInProgress})");
		if (IsMultiplayer)
		{
			RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(new ConsoleCmdGameAction(me, cmd, CombatManager.Instance.IsInProgress));
			return $"已同步给所有玩家：{cmd}";
		}
		TaskHelper.RunSafely(NDevConsole.Instance.ProcessNetCommand(me, cmd));
		return $"已执行：{cmd}";
	}
}

/// <summary>
/// 控制台命令 p3test（联机同步）。各子命令都以发命令的玩家（issuingPlayer）为准，不用"本机玩家"，这样每一端执行的结果都一样：
/// · dummy：把当前战斗的怪物换成一只打不死的木桩（和原版 fight 命令同一个入口 EnterRoomDebug）；
/// · immortal on|off：所有玩家不死（这一下最多扣到 1 点，低于 30% 补满）；
/// · hurt N：发命令的玩家受到 N 点伤害；
/// · hand clear|exhaust：发命令的玩家的手牌移到弃牌堆 / 全部消耗。
/// </summary>
public class TestCmd : AbstractConsoleCmd
{
	public override string CmdName => "p3test";

	public override string Args => "dummy | immortal on|off | hurt <n> | hand clear|exhaust";

	public override string Description => "P3TestMod: synced test helpers.";

	public override bool IsNetworked => true;

	public override CmdResult Process(Player? issuingPlayer, string[] args)
	{
		MpLog.Event($"exec p3test {string.Join(" ", args)} (issued by {issuingPlayer?.NetId})");
		if (args.Length == 0)
		{
			return new CmdResult(success: false, Args);
		}
		switch (args[0])
		{
			case "dummy":
			{
				if (!RunManager.Instance.IsInProgress)
				{
					return new CmdResult(success: false, "A run is currently not in progress!");
				}
				EncounterModel encounter = ModelDb.Encounter<BattlewornDummyEventV1Encounter>().ToMutable();
				encounter.DebugRandomizeRng();
				Runtime.DummyPending = true;
				Task task = RunManager.Instance.EnterRoomDebug(RoomType.Monster, MegaCrit.Sts2.Core.Map.MapPointType.Unassigned, encounter);
				return new CmdResult(task, success: true, "Replaced the room with a training dummy.");
			}
			case "immortal":
				Runtime.Immortal = args.Length < 2 || args[1] != "off";
				return new CmdResult(success: true, $"Immortal = {Runtime.Immortal}");
			case "hurt":
			{
				if (issuingPlayer?.Creature is not Creature pc || !CombatManager.Instance.IsInProgress)
				{
					return new CmdResult(success: false, "This command only works in combat.");
				}
				int amount = args.Length > 1 && int.TryParse(args[1], out int n) ? n : 3;
				Task task = CreatureCmd.Damage(new BlockingPlayerChoiceContext(), new List<Creature> { pc }, amount, ValueProp.Unpowered, null, null, null);
				return new CmdResult(task, success: true, $"Hurt player {issuingPlayer.NetId} by {amount}.");
			}
			case "hand":
			{
				if (issuingPlayer == null || !CombatManager.Instance.IsInProgress)
				{
					return new CmdResult(success: false, "This command only works in combat.");
				}
				List<CardModel> cards = PileType.Hand.GetPile(issuingPlayer).Cards.ToList();
				bool exhaust = args.Length > 1 && args[1] == "exhaust";
				// 联机时逐张处理：同时消耗的话各端动画等待的快慢不同，状态改动的先后可能不一样
				Task task = exhaust ? (Net.IsMultiplayer ? ExhaustOneByOne(cards) : Runtime.ExhaustAll(cards)) : DiscardAll(cards);
				return new CmdResult(task, success: true, $"{(exhaust ? "Exhausted" : "Discarded")} {cards.Count} cards.");
			}
		}
		return new CmdResult(success: false, Args);
	}

	private static async Task ExhaustOneByOne(List<CardModel> cards)
	{
		foreach (CardModel c in cards)
		{
			await CardCmd.Exhaust(new BlockingPlayerChoiceContext(), c);
		}
	}

	private static async Task DiscardAll(List<CardModel> cards)
	{
		foreach (CardModel c in cards)
		{
			await CardPileCmd.Add(c, PileType.Discard);
		}
	}
}
