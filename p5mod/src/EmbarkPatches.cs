using System;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace P5Joker;

/// <summary>Draw the local curtain while the game's native fades and loading proceed.</summary>
public static class Embark
{
	public const string SilentPath = "res://materials/transitions/silent_transition_mat.tres";
	private const string WipeSfx = "event:/sfx/ui/wipe_silent";
	private static CanvasLayer? _layer;
	private static EmbarkTransition.Curtain? _curtain;
	private static uint _revision;

	private static bool Instant => SaveManager.Instance?.PrefsSave?.FastMode == FastModeType.Instant;

	public static bool Replaces(string path) => path == SilentPath && JokerCostume.Selected && !Instant;
	public static bool MutesWipe(string path) => path == WipeSfx && JokerCostume.Selected && !Instant;

	public static async Task Out(NTransition transition, CancellationToken? token)
	{
		uint revision = ++_revision;
		FxClose(_layer);
		_layer = null;
		_curtain = null;
		bool Current() => revision == _revision && Fx.Active(transition)
			&& token?.IsCancellationRequested != true && JokerCostume.Selected;
		try
		{
			await EmbarkTransition.PrepareTextures(transition, Current);
			if (!Current()) return;

			var layer = new CanvasLayer { Layer = 125, Name = "P5EmbarkOverlay" };
			transition.GetViewport().AddChild(layer);
			_layer = layer;
			Fx.FreeAfter(layer, 30);
			var host = new Control { Size = transition.GetViewportRect().Size, MouseFilter = Control.MouseFilterEnum.Ignore };
			layer.AddChild(host);
			_curtain = EmbarkTransition.Out(host);
		}
		catch (Exception e)
		{
			Stage.Report("embark out", e);
			if (revision == _revision) FxClose(_layer);
		}
	}

	private static void FxClose(CanvasLayer? layer)
	{
		if (GodotObject.IsInstanceValid(layer)) layer!.QueueFree();
	}

	public static void In()
	{
		_revision++;
		var layer = _layer;
		var curtain = _curtain;
		_layer = null;
		_curtain = null;
		if (curtain is { Alive: true }) curtain.Open(() => FxClose(layer));
		else FxClose(layer);
	}
}

[HarmonyPatch(typeof(NTransition), nameof(NTransition.FadeOut))]
public static class EmbarkFadeOutPatch
{
	public static void Prefix(NTransition __instance, string transitionPath, CancellationToken? cancelToken)
	{
		if (Embark.Replaces(transitionPath))
			_ = Embark.Out(__instance, cancelToken);
	}
}

[HarmonyPatch(typeof(NTransition), nameof(NTransition.RoomFadeIn))]
public static class EmbarkRoomFadeInPatch
{
	public static void Postfix() => ModEntry.Safe("EmbarkIn", Embark.In);
}

[HarmonyPatch(typeof(NTransition), nameof(NTransition.FadeIn))]
public static class EmbarkFadeInPatch
{
	public static void Postfix() => ModEntry.Safe("EmbarkIn", Embark.In);
}
