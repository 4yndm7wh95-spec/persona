using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace P3TestMod;

/// <summary>
/// 1.2.2：杀戮尖塔 2 不同版本之间签名不一样的接口（0.107.1 正式版 / 0.111.0 测试版），全部用反射按当前版本调用。
/// 对照见 tools/compat/ApiCheck（research/libs 与 research/libs_0.107.1）。
/// </summary>
internal static class Compat
{
	/// <summary>木桩遭遇：0.111 叫 BattlewornDummyEventV1Encounter，0.107 叫 BattlewornDummyEventEncounter（默认就是第一种木桩 BattleFriendV1）。</summary>
	public static EncounterModel DummyEncounter()
	{
		Type? t = AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.Encounters.BattlewornDummyEventV1Encounter")
			?? AccessTools.TypeByName("MegaCrit.Sts2.Core.Models.Encounters.BattlewornDummyEventEncounter")
			?? throw new InvalidOperationException("no training dummy encounter in this game version");
		MethodInfo m = typeof(ModelDb).GetMethod(nameof(ModelDb.Encounter), BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null)!.MakeGenericMethod(t);
		return (EncounterModel)m.Invoke(null, null)!;
	}

	private static readonly MethodInfo ExhaustMethod = typeof(CardCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
		.First(m => m.Name == "Exhaust" && m.GetParameters().Length == 4 && m.GetParameters()[1].ParameterType == typeof(CardModel));

	/// <summary>CardCmd.Exhaust：0.111 返回 Task&lt;CardPileAddResult?&gt;，0.107 返回 Task。</summary>
	public static Task Exhaust(PlayerChoiceContext ctx, CardModel card) => (Task)ExhaustMethod.Invoke(null, new object[] { ctx, card, false, false })!;

	/// <summary>CreatureCmd.Damage 的最终重载：0.111 多一个 cardPlay 参数。</summary>
	public static Task Damage(PlayerChoiceContext ctx, IEnumerable<Creature> targets, decimal amount, ValueProp props)
	{
		MethodInfo m = typeof(CreatureCmd).GetMethods(BindingFlags.Public | BindingFlags.Static)
			.Where(x => x.Name == "Damage" && x.GetParameters().Length >= 6 && x.GetParameters()[1].ParameterType == typeof(IEnumerable<Creature>) && x.GetParameters()[2].ParameterType == typeof(decimal))
			.OrderByDescending(x => x.GetParameters().Length).First();
		object?[] args = m.GetParameters().Length == 7
			? new object?[] { ctx, targets, amount, props, null, null, null }
			: new object?[] { ctx, targets, amount, props, null, null };
		return (Task)m.Invoke(null, args)!;
	}

	public static string GameVersion()
	{
		try
		{
			return typeof(MegaCrit.Sts2.Core.Nodes.NGame).GetMethod("GetGameVersion", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null)?.Invoke(null, null)?.ToString() ?? "?";
		}
		catch (Exception)
		{
			return "?";
		}
	}
}
