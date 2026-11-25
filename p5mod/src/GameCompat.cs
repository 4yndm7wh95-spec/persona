using System;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace P5Joker;

internal static class GameCompat
{
	internal static readonly MethodInfo SetAnimationMethod = AccessTools.Method(typeof(MegaAnimationState), "SetAnimation",
		new[] { typeof(string), typeof(bool), typeof(int) });
	private static readonly MethodInfo AddAnimationMethod = AccessTools.Method(typeof(MegaAnimationState), "AddAnimation",
		new[] { typeof(string), typeof(float), typeof(bool), typeof(int) });

	// 0.107 returns transient track wrappers. Newer bindings return void.
	internal static void SetSpineAnimation(this MegaAnimationState state, string name, bool loop, int track) =>
		(SetAnimationMethod.Invoke(state, new object[] { name, loop, track }) as IDisposable)?.Dispose();

	internal static void AddSpineAnimation(this MegaAnimationState state, string name, float delay, bool loop, int track) =>
		(AddAnimationMethod.Invoke(state, new object[] { name, delay, loop, track }) as IDisposable)?.Dispose();

	private static readonly PropertyInfo? Startup = AccessTools.Property(typeof(NGame), "GameStartupComplete");
	internal static bool Started(NGame game) => Startup == null || Startup.GetValue(game) is Task { IsCompletedSuccessfully: true };

	private static readonly PropertyInfo? Directional = AccessTools.Property(typeof(NControllerManager), "IsUsingDirectionalNavigation")
		?? AccessTools.Property(typeof(NControllerManager), "IsUsingController");
	internal static bool DirectionalNavigation() => NControllerManager.Instance is { } manager && Directional?.GetValue(manager) is true;

}
