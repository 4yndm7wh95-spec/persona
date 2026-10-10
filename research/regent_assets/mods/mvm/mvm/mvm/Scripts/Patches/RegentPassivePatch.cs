using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;

namespace Test.Scripts.Patches;

[HarmonyPatch]
public static class RegentPassivePatch
{
	private static readonly string LogPath = "mods/mvm/regent_passive.log";

	private static readonly Dictionary<Player, SovereignBlade> _cards = new Dictionary<Player, SovereignBlade>();

	private static readonly Dictionary<ICombatState, int> _turnCounts = new Dictionary<ICombatState, int>();

	private static void Log(string msg)
	{
		if (Entry.IsMobile || !Entry.DebugMode)
		{
			return;
		}
		try
		{
			File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
		}
		catch
		{
		}
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(NCombatUi), "Activate")]
	private static void Activate_Postfix(CombatState state)
	{
		List<Player> list = ((state != null) ? state.Players.Where(delegate(Player p)
		{
			CharacterModel character = p.Character;
			return ((character != null) ? ((AbstractModel)character).Id.Entry : null) == "REGENT";
		}).ToList() : null);
		if (list == null || list.Count == 0)
		{
			return;
		}
		_turnCounts[(ICombatState)(object)state] = 0;
		Log($"Regent被动：战斗开始 → {list.Count} 位储君");
		foreach (Player item in list)
		{
			SpawnBladeVfxOnRoom(item);
		}
	}

	private static void SpawnBladeVfxOnRoom(Player player)
	{
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		NCombatRoom instance = NCombatRoom.Instance;
		NCreature val = ((instance != null) ? instance.GetCreatureNode(player.Creature) : null);
		if (instance == null || val == null)
		{
			Log("  ❌ room或NCreature为null");
			return;
		}
		if (((IEnumerable)((Node)instance).GetChildren(false)).OfType<NSovereignBladeVfx>().Any(delegate(NSovereignBladeVfx b)
		{
			CardModel card = b.Card;
			return ((card != null) ? card.Owner : null) == player;
		}))
		{
			Log("  ⚠ VFX已存在，跳过");
			return;
		}
		SovereignBlade val2 = player.Creature.CombatState.CreateCard<SovereignBlade>(player);
		_cards[player] = val2;
		NSovereignBladeVfx val3 = NSovereignBladeVfx.Create((CardModel)(object)val2);
		if (val3 == null)
		{
			Log("  ❌ VFX.Create 返回null");
			return;
		}
		GodotTreeExtensions.AddChildSafely((Node)(object)instance, (Node)(object)val3);
		((Node2D)val3).GlobalPosition = ((Control)val).GlobalPosition;
		val3.Forge(0f, true);
		CharacterModel character = player.Character;
		object obj;
		if (character == null)
		{
			obj = null;
		}
		else
		{
			ModelId id = ((AbstractModel)character).Id;
			obj = ((id != null) ? id.Entry : null);
		}
		Log("  ✅ '" + (string?)obj + "' 剑VFX创建完成");
	}

	[HarmonyPostfix]
	[HarmonyPatch(typeof(Hook), "AfterSideTurnStart", new Type[]
	{
		typeof(ICombatState),
		typeof(CombatSide),
		typeof(IReadOnlyList<Creature>)
	})]
	private static void AfterSideTurnStart_Postfix(ICombatState combatState, CombatSide side, IReadOnlyList<Creature> participants, ref Task __result)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		if ((int)side != 2)
		{
			return;
		}
		int num = _turnCounts.GetValueOrDefault(combatState, 0) + 1;
		_turnCounts[combatState] = num;
		if (num % 2 == 0)
		{
			List<Player> list = combatState.Players.Where(delegate(Player p)
			{
				CharacterModel character = p.Character;
				return ((character != null) ? ((AbstractModel)character).Id.Entry : null) == "REGENT" && _cards.ContainsKey(p);
			}).ToList();
			if (list.Count != 0)
			{
				Task original = __result;
				__result = ChainBladeAttack(original, combatState, list, num);
			}
		}
	}

	private static async Task ChainBladeAttack(Task original, ICombatState combatState, List<Player> regentPlayers, int turn)
	{
		await original;
		foreach (Player player in regentPlayers)
		{
			if (!_cards.TryGetValue(player, out SovereignBlade value))
			{
				continue;
			}
			IReadOnlyList<Creature> hittableEnemies = combatState.HittableEnemies;
			if (hittableEnemies == null || hittableEnemies.Count == 0)
			{
				continue;
			}
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(2, 3);
			CharacterModel character = player.Character;
			object obj;
			if (character == null)
			{
				obj = null;
			}
			else
			{
				ModelId id = ((AbstractModel)character).Id;
				obj = ((id != null) ? id.Entry : null);
			}
			if (obj == null)
			{
				obj = "regent";
			}
			defaultInterpolatedStringHandler.AppendFormatted((string?)obj);
			defaultInterpolatedStringHandler.AppendLiteral("_");
			defaultInterpolatedStringHandler.AppendFormatted(player.NetId);
			defaultInterpolatedStringHandler.AppendLiteral("_");
			defaultInterpolatedStringHandler.AppendFormatted(turn);
			int deterministicHashCode = StringHelper.GetDeterministicHashCode(defaultInterpolatedStringHandler.ToStringAndClear());
			Creature val = hittableEnemies[Math.Abs(deterministicHashCode) % hittableEnemies.Count];
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler2 = new DefaultInterpolatedStringHandler(17, 3);
			defaultInterpolatedStringHandler2.AppendLiteral("第");
			defaultInterpolatedStringHandler2.AppendFormatted(turn);
			defaultInterpolatedStringHandler2.AppendLiteral("回合: '");
			CharacterModel character2 = player.Character;
			object value2;
			if (character2 == null)
			{
				value2 = null;
			}
			else
			{
				ModelId id2 = ((AbstractModel)character2).Id;
				value2 = ((id2 != null) ? id2.Entry : null);
			}
			defaultInterpolatedStringHandler2.AppendFormatted((string?)value2);
			defaultInterpolatedStringHandler2.AppendLiteral("' 君王之剑 → '");
			defaultInterpolatedStringHandler2.AppendFormatted(val.Name);
			defaultInterpolatedStringHandler2.AppendLiteral("'");
			Log(defaultInterpolatedStringHandler2.ToStringAndClear());
			NCombatRoom instance = NCombatRoom.Instance;
			NSovereignBladeVfx vfx = ((instance != null) ? ((IEnumerable)((Node)instance).GetChildren(false)).OfType<NSovereignBladeVfx>().FirstOrDefault(delegate(NSovereignBladeVfx b)
			{
				CardModel card = b.Card;
				return ((card != null) ? card.Owner : null) == player;
			}) : null);
			NCombatRoom instance2 = NCombatRoom.Instance;
			NCreature nTarget = ((instance2 != null) ? instance2.GetCreatureNode(val) : null);
			await DamageCmd.Attack(((DynamicVar)((CardModel)value).DynamicVars.Damage).BaseValue).FromCard((CardModel)(object)value).WithHitCount(((DynamicVar)((CardModel)value).DynamicVars.Repeat).IntValue)
				.WithAttackerAnim("Cast", player.Character.AttackAnimDelay, (Creature)null)
				.WithAttackerFx((string)null, "event:/sfx/characters/regent/regent_sovereign_blade", (string)null)
				.Targeting(val)
				.BeforeDamage((Func<Task>)async delegate
				{
					if (vfx != null && nTarget != null)
					{
						vfx.Attack(nTarget.VfxSpawnPosition);
					}
					await Task.CompletedTask;
				})
				.WithHitVfxNode((Func<Creature, Node2D>)((Creature t) => (Node2D?)(object)NBigSlashVfx.Create(t)))
				.WithHitVfxNode((Func<Creature, Node2D>)((Creature t) => (Node2D?)(object)NBigSlashImpactVfx.Create(t)))
				.Execute((PlayerChoiceContext)new BlockingPlayerChoiceContext());
		}
	}
}
