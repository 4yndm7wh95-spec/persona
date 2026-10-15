using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace P3TestMod;

/// <summary>
/// v1.2：正常关卡（不再每一关都是木桩，能量 / 辉星不再无限）。测试用的改动全部经由联机同步的控制台命令（Net.Send）。
/// 每帧：F8 开关测试面板，F7 消耗所有手牌；导演接口（录制）在用时，单人局里能量 / 辉星保持 99（录制脚本连续出牌要用）。
/// </summary>
public static class Runtime
{
	public const int Infinite = 99;
	public const int DummyHp = 999999;

	// 以下状态只由 p3test 命令改写（联机时每一端在同一个动作里改），补丁据此决定木桩 / 不死，保证各端一致
	public static bool DummyPending;                 // p3test dummy 刚执行、新战斗还没建好：木桩的初始血量用 DummyHp
	public static EncounterModel? DummyEncounter;    // 测试木桩那一场的遭遇（CombatSetUp 时记下）
	public static bool Immortal;                     // 所有玩家不死

	public static bool RecordingTopUp;               // 导演接口用过：单人局能量 / 辉星保持 99（只在单人局里生效）

	private static bool _installed;
	private static bool _f8Down, _f7Down;
	private static bool _hooked;

	public static bool InDummyCombat =>
		DummyPending || (DummyEncounter != null && ReferenceEquals(CombatManager.Instance.DebugOnlyGetState()?.Encounter, DummyEncounter));

	public static void Install()
	{
		if (_installed || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		_installed = true;
		tree.ProcessFrame += () => TestEntry.Safe("tick", Tick);
	}

	public static CombatState? Combat => CombatManager.Instance is { IsInProgress: true } cm ? cm.DebugOnlyGetState() : null;

	public static Player? Me => Combat is CombatState s ? LocalContext.GetMe(s) : null;

	private static void Tick()
	{
		if (!_hooked)
		{
			_hooked = true;
			CombatManager.Instance.CombatSetUp += s => TestEntry.Safe("dummy setup", () => DummyHooks.OnCombatSetUp(s));
			MpLog.Hook();
		}
		Bridge.Pump();
		MpLog.Tick();
		Panel.Tick();
		bool f8 = Input.IsKeyPressed(Key.F8);
		if (f8 && !_f8Down)
		{
			Panel.Toggle();
		}
		_f8Down = f8;
		bool f7 = Input.IsKeyPressed(Key.F7);
		if (f7 && !_f7Down)
		{
			Panel.Say(ExhaustHand());
		}
		_f7Down = f7;

		// 录制：只在单人局里直接改数值（联机时不能本地改，否则各端不一致）
		if (RecordingTopUp && !Net.IsMultiplayer && Me?.PlayerCombatState is PlayerCombatState pcs && !CombatManager.Instance.IsEnding)
		{
			if (pcs.Energy < Infinite) pcs.Energy = Infinite;
			if (pcs.Stars < Infinite) pcs.Stars = Infinite;
		}
	}

	// ── 按钮（全部走 Net.Send，联机同步）──

	public static string ReplaceWithDummy() =>
		CombatManager.Instance.IsInProgress ? Net.Send("p3test dummy") : "不在战斗中：进战斗后再把怪物换成木桩";

	public static string AddEnergy(int n) => CombatManager.Instance.IsInProgress ? Net.Send($"energy {n}") : "不在战斗中";

	public static string AddStars(int n) => CombatManager.Instance.IsInProgress ? Net.Send($"stars {n}") : "不在战斗中";

	public static string ToggleImmortal() => Net.Send(Immortal ? "p3test immortal off" : "p3test immortal on");

	public static string KillDummy()
	{
		CombatState? state = Combat;
		if (state == null) return "不在战斗中";
		if (!state.Enemies.Any(c => c.IsAlive)) return "没有可击杀的目标";
		return Net.Send("kill all");
	}

	public static string ExhaustHand() => Combat == null ? "不在战斗中" : Net.Send("p3test hand exhaust");

	public static string ClearHand() => Combat == null ? "不在战斗中" : Net.Send("p3test hand clear");

	public static string HurtMe(int amount) => Combat == null ? "不在战斗中" : Net.Send($"p3test hurt {amount}");

	public static string AddCard(CardModel model)
	{
		if (Me is not Player me) return "不在战斗中：先进一场战斗";
		if (PileType.Hand.GetPile(me).Cards.Count >= CardPile.MaxCardsInHand) return "手牌满了（10 张）：先出牌或点\"清空手牌\"";
		Net.Send($"card {model.Id.Entry}");
		return $"已加入手牌：{model.TitleLocString.GetRawText()}";
	}

	public static async System.Threading.Tasks.Task ExhaustAll(List<CardModel> cards)
	{
		// 所有手牌同时开始消耗（不一张一张等），一起播原版的消耗动画
		await System.Threading.Tasks.Task.WhenAll(cards.Select(c => CardCmd.Exhaust(new BlockingPlayerChoiceContext(), c)));
	}
}
