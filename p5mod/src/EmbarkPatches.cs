using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace P5Joker;

/// <summary>
/// 出发过场（Vfx/EmbarkTransition.cs）接进游戏。原版流程（NCharacterSelectScreen.StartNewSingleplayerRun，主菜单「继续」、每日挑战等同理）：
///   SfxCmd.Play(CharacterTransitionSfx = event:/sfx/ui/wipe_silent) → NTransition.FadeOut(0.8, silent_transition_mat) → 加载 → 进局时 RoomFadeIn / FadeIn。
/// · FadeOut 用静默猎手的过场材质时换成我们的过场，画在 NTransition 上面（全屏、在所有界面之上的那块 ColorRect）。画面盖住那一刻调一次原版
///   FadeOut(0 秒、普通淡出材质)，让 NTransition 自己在底下变成不透明的黑 —— 之后加载、淡入用的全是原版自己的状态；
///   我们的幕（钉着预告信的那一帧）留在 NTransition 上面当加载画面。
/// · 点出发时原版先放的 wipe_silent 拦掉（过场自己有声音，CharSelectSfxMute）。
/// · 紧接着的第一次 RoomFadeIn / FadeIn（进局）：原版淡入的两层（材质阈值、SimpleTransition）当场变透明（它们的补间还没开始取初值，
///   之后就从 0 补到 0），我们的幕展开（预告信顺着门缝一分为二、两扇门滑开）。30 秒内没等到淡入就把幕撤掉（底下是原版的黑屏）。
/// 快速模式「瞬间」不换（原版直接隐藏 NTransition）。过场出错就退回原版的黑屏，不打断进局。
/// </summary>
public static class Embark
{
	public const string SilentPath = "res://materials/transitions/silent_transition_mat.tres";
	private const string FadePath = "res://materials/transitions/fade_transition_mat.tres";
	private const string WipeSfx = "event:/sfx/ui/wipe_silent";

	private static bool _running;
	private static EmbarkTransition.Curtain? _curtain;                     // 盖着的幕（等进局时展开）

	private static readonly AccessTools.FieldRef<NTransition, Control> Simple =
		AccessTools.FieldRefAccess<NTransition, Control>("_simpleTransition");

	private static bool Instant => SaveManager.Instance?.PrefsSave?.FastMode == FastModeType.Instant;

	public static bool Replaces(string path) => path == SilentPath && !_running && !Instant;

	public static bool MutesWipe(string path) => path == WipeSfx && !Instant;

	public static async Task Out(NTransition t, CancellationToken? token)
	{
		_running = true;
		EmbarkTransition.Curtain? curtain = null;
		try
		{
			Close(_curtain);
			Traverse.Create(t).Property("InTransition").SetValue(true);
			bool covered = false;
			curtain = EmbarkTransition.Out(t, () => covered = true);
			ulong start = Time.GetTicksMsec();
			ulong limit = (ulong)((EmbarkTransition.OutTime + 2f) * 1000f);  // 补间被意外停掉时的保底
			while (!covered && curtain.Alive && Time.GetTicksMsec() - start < limit && !(token?.IsCancellationRequested ?? false))
			{
				await t.ToSignal(t.GetTree(), SceneTree.SignalName.ProcessFrame);
			}
		}
		catch (Exception e)
		{
			Stage.Report("embark out", e);
		}
		finally
		{
			_running = false;
		}
		// 接上原版的黑屏：原版 FadeOut 0 秒同步跑完（循环一次都不进），NTransition 换成普通淡出材质、阈值 1 = 不透明的黑，并挡住输入。
		// 我们的幕还盖在它上面（钉着预告信的那一帧当加载画面），等进局时展开；30 秒没等到就撤掉
		await t.FadeOut(0f, FadePath, null);
		if (curtain is { Alive: true })
		{
			_curtain = curtain;
			t.GetTree().CreateTimer(30.0).Timeout += () => { if (_curtain == curtain) { Close(curtain); _curtain = null; } };
		}
	}

	private static void Close(EmbarkTransition.Curtain? c)
	{
		if (c is { Alive: true }) c.Root.QueueFree();
	}

	/// <summary>进局淡入：只接我们还盖着的幕，原版的两层当场透明，幕展开。</summary>
	public static void In(NTransition t)
	{
		EmbarkTransition.Curtain? curtain = _curtain;
		_curtain = null;
		if (curtain is not { Alive: true }) return;
		if (Simple(t) is Control simple)
		{
			Color c = simple.Modulate;
			c.A = 0f;
			simple.Modulate = c;
		}
		(t.Material as ShaderMaterial)?.SetShaderParameter("threshold", 0f);
		curtain.Open();
	}
}

[HarmonyPatch(typeof(NTransition), nameof(NTransition.FadeOut))]
public static class EmbarkFadeOutPatch
{
	public static bool Prefix(NTransition __instance, string transitionPath, CancellationToken? cancelToken, ref Task __result)
	{
		if (!Embark.Replaces(transitionPath))
		{
			return true;
		}
		Task? run = null;
		ModEntry.Safe("EmbarkOut", () => run = Embark.Out(__instance, cancelToken));
		if (run == null)
		{
			return true;
		}
		__result = run;
		return false;
	}
}

[HarmonyPatch(typeof(NTransition), nameof(NTransition.RoomFadeIn))]
public static class EmbarkRoomFadeInPatch
{
	public static void Postfix(NTransition __instance) => ModEntry.Safe("EmbarkIn", () => Embark.In(__instance));
}

[HarmonyPatch(typeof(NTransition), nameof(NTransition.FadeIn))]
public static class EmbarkFadeInPatch
{
	public static void Postfix(NTransition __instance) => ModEntry.Safe("EmbarkIn", () => Embark.In(__instance));
}
