using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;

namespace P5Joker;

internal static class JokerCostume
{
	private const string SkeletonPath = "res://animations/characters/silent/silent_skel_data.tres";
	private const string SkeletonFile = "res://animations/characters/silent/silent.spine-json";
	private const string Signature = "shiv_from_aim";
	private const string ProviderId = "P5Joker";
	private sealed record SkeletonSignature(bool IsJoker);
	private static readonly ConditionalWeakTable<Resource, SkeletonSignature> Signatures = new();
	private static readonly Lazy<Func<bool?>?> ManagerSelection = new(FindManagerSelection);
	private static ulong _checkedFrame = ulong.MaxValue;
	private static bool _hasStandaloneSkeleton;

	internal static bool? ManagedSelection => ManagerSelection.Value?.Invoke();
	internal static bool Selected => ManagedSelection ?? HasStandaloneSkeleton;

	internal static bool HasStandaloneSkeleton
	{
		get
		{
			ulong frame = Engine.GetProcessFrames();
			if (_checkedFrame != frame)
			{
				// Read the small descriptor so checking the menu doesn't load a whole Spine atlas.
				_hasStandaloneSkeleton = FileAccess.GetFileAsString(SkeletonPath)
					.Contains("\"" + SkeletonFile + "\"", StringComparison.Ordinal);
				_checkedFrame = frame;
			}
			return _hasStandaloneSkeleton;
		}
	}

	internal static bool? OwnsBody(Node2D body)
	{
		if (!body.HasMethod("get_skeleton_data_res")) return false;
		if (body.Get("skeleton_data_res").AsGodotObject() is not Resource skeleton) return null;
		if (Signatures.TryGetValue(skeleton, out SkeletonSignature? known)) return known.IsJoker;
		if (!skeleton.HasMethod("find_animation") || !skeleton.HasMethod("get_animations")
			|| skeleton.Call("get_animations").AsGodotArray().Count == 0)
			return null;

		// Cache by the resource carried by this sprite. The manager can swap a
		// skeleton in an existing battle even though the Player and NCreature stay the same.
		return Signatures.GetValue(skeleton, static data =>
			new SkeletonSignature(data.Call("find_animation", Signature).AsGodotObject() != null)).IsJoker;
	}

	private static Func<bool?>? FindManagerSelection()
	{
		// ManagedSkinModLoader only exists when SkinChanger is loaded. Its assembly
		// ownership lets existing nodes stop the moment SkinChanger turns this pack off.
		const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		Type? loader = AppDomain.CurrentDomain.GetAssemblies()
			.FirstOrDefault(assembly => assembly.GetName().Name == "Gurio.SkinChanger")
			?.GetType("STS2SkinChanger.Core.ManagedSkinModLoader");
		var isActive = loader?.GetMethod("IsProviderAssemblyActive", flags, [typeof(Assembly)])
			?.CreateDelegate<Func<Assembly, bool>>();
		var isManaged = loader?.GetMethod("IsProviderAssemblyFor", flags, [typeof(string), typeof(Assembly)])
			?.CreateDelegate<Func<string, Assembly, bool>>();
		if (isActive == null || isManaged == null) return null;
		Assembly provider = typeof(JokerCostume).Assembly;
		return () => isActive(provider) ? true : isManaged(ProviderId, provider) ? false : null;
	}
}
