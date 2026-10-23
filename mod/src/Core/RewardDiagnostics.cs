using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Rewards;

namespace P3Makoto.Core;

/// <summary>万花筒报告尚无失败日志。只记录原版流程，不补发、不自动选择或吞掉异常。</summary>
internal static class RewardDiagnostics
{
	private static int _kaleidoscopes;
	public static bool Active => _kaleidoscopes > 0;
	public static void Begin()
	{
		_kaleidoscopes++;
		Write("relic obtained: begin");
	}

	public static void Write(string message) => GD.Print($"{ModEntry.Tag} kaleidoscope {message}");

	public static string Describe(RewardsSet set) => $"count={set.Rewards.Count}, rewards=[{string.Join(", ", set.Rewards.Select(r => $"{r.GetType().Name}:selected={r.SuccessfullySelected}"))}]";

	public static async Task Observe(Task original, string phase, RewardsSet? set = null, bool finishRelic = false)
	{
		try
		{
			await original;
			ModEntry.Safe("kaleidoscope completion trace", () =>
				Write($"{phase}: completed{(set == null ? "" : "; " + Describe(set))}"));
		}
		catch (Exception e)
		{
			ModEntry.Safe("kaleidoscope failure trace", () => GD.PrintErr($"{ModEntry.Tag} kaleidoscope {phase}: {e}"));
			throw;
		}
		finally
		{
			if (finishRelic) _kaleidoscopes = Math.Max(0, _kaleidoscopes - 1);
		}
	}
}
