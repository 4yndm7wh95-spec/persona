using System;
using System.Linq;
using System.Reflection;

namespace P3Makoto.Core;

internal static class MakotoSelectSkin
{
	private static readonly Lazy<Func<bool?>?> Selection = new(FindSelection);

	internal static bool? ManagedSelection => Selection.Value?.Invoke();
	internal static bool IsSelected => ManagedSelection ?? Makoto.AssetsOurs;

	private static Func<bool?>? FindSelection()
	{
		const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
		Type? loader = AppDomain.CurrentDomain.GetAssemblies()
			.FirstOrDefault(assembly => assembly.GetName().Name == "Gurio.SkinChanger")
			?.GetType("STS2SkinChanger.Core.ManagedSkinModLoader");
		var isActive = loader?.GetMethod("IsProviderAssemblyActive", flags, [typeof(Assembly)])
			?.CreateDelegate<Func<Assembly, bool>>();
		var isManaged = loader?.GetMethod("IsProviderAssemblyFor", flags, [typeof(string), typeof(Assembly)])
			?.CreateDelegate<Func<string, Assembly, bool>>();
		if (isActive == null || isManaged == null) return null;
		Assembly provider = typeof(MakotoSelectSkin).Assembly;
		return () => isActive(provider) ? true : isManaged("P3Makoto", provider) ? false : null;
	}
}
