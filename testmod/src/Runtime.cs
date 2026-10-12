using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.ValueProps;

namespace P3TestMod;

/// <summary>
/// 每帧检查（只在战斗中）：
/// · 假人：血量上限和当前血量拉到 999999，被打掉一半就补满（"击杀"按钮会临时放行）；
/// · 玩家：血量低于 30% 补满（"让我受击"不会真的打死）；能量、辉星不足 99 就直接设回 99；
/// · F8：开关测试面板。
/// </summary>
public static class Runtime
{
	public const int Infinite = 99;
	public const int DummyHp = 999999;

	private static bool _installed;
	private static bool _f8Down;
	private static bool _busy;                     // 有一个血量命令还在执行，等它完成再检查
	public static bool AllowEnemyDeath;            // "击杀假人"时临时放行

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
		bool f8 = Input.IsKeyPressed(Key.F8);
		if (f8 && !_f8Down)
		{
			Panel.Toggle();
		}
		_f8Down = f8;

		CombatState? state = Combat;
		if (state == null || CombatManager.Instance.IsEnding)
		{
			AllowEnemyDeath = false;
			return;
		}
		Player? me = LocalContext.GetMe(state);
		if (me?.PlayerCombatState is PlayerCombatState pcs)
		{
			if (pcs.Energy < Infinite) pcs.Energy = Infinite;
			if (pcs.Stars < Infinite) pcs.Stars = Infinite;
		}
		if (_busy)
		{
			return;
		}
		if (!AllowEnemyDeath)
		{
			foreach (Creature e in state.Enemies.Where(c => c.IsAlive))
			{
				if (e.MaxHp < DummyHp)
				{
					Run(async () =>
					{
						await CreatureCmd.GainMaxHp(e, DummyHp - e.MaxHp);
						await CreatureCmd.SetCurrentHp(e, DummyHp);
					});
					return;
				}
				if (e.CurrentHp < DummyHp / 2)
				{
					Run(() => CreatureCmd.SetCurrentHp(e, DummyHp));
					return;
				}
			}
		}
		if (me?.Creature is Creature pc && pc.IsAlive && pc.CurrentHp < pc.MaxHp * 0.3f)
		{
			Run(() => CreatureCmd.SetCurrentHp(pc, pc.MaxHp));
		}
	}

	/// <summary>执行一个异步命令（和开发者控制台一样用 TaskHelper.RunSafely），执行期间不再发新的血量命令。</summary>
	public static void Run(System.Func<System.Threading.Tasks.Task> command)
	{
		_busy = true;
		async System.Threading.Tasks.Task Wrapped()
		{
			try
			{
				await command();
			}
			finally
			{
				_busy = false;
			}
		}
		TaskHelper.RunSafely(Wrapped());
	}

	// ── 面板按钮用 ──

	public static string KillDummy()
	{
		CombatState? state = Combat;
		if (state == null) return "不在战斗中";
		List<Creature> alive = state.Enemies.Where(c => c.IsAlive).ToList();
		if (alive.Count == 0) return "没有可击杀的目标";
		AllowEnemyDeath = true;
		TaskHelper.RunSafely(KillAll(alive));
		return "已击杀假人（看胜利特效）";
	}

	private static async System.Threading.Tasks.Task KillAll(List<Creature> enemies)
	{
		await CreatureCmd.Kill(enemies, force: true);
		await CombatManager.Instance.CheckWinCondition();
	}

	public static string HurtMe(int amount)
	{
		if (Me?.Creature is not Creature pc) return "不在战斗中";
		TaskHelper.RunSafely(CreatureCmd.Damage(new BlockingPlayerChoiceContext(), new List<Creature> { pc }, amount, ValueProp.Unpowered, null, null, null));
		return $"对自己造成 {amount} 点伤害（看受击动画）";
	}
}
