using System;
using System.Linq;
using System.Reflection;

namespace P3Makoto.Core;

internal static class MakotoSelectSkin
{
	private static volatile Lazy<Func<bool?>?> Selection = new(FindSelection);

	private static volatile Lazy<Func<string, bool>?> _managedPack = new(FindPackRegistry);
	private static readonly string PackPath = System.IO.Path.Combine(
		System.IO.Path.GetDirectoryName(typeof(MakotoSelectSkin).Assembly.Location) ?? "", "P3Makoto.pck");
	internal static bool PackManaged => _managedPack.Value?.Invoke(PackPath) == true;

	private static Func<string, bool>? FindPackRegistry() =>
		AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == "Sts2SkinManager")
			?.GetType("Sts2SkinManager.Runtime.ManagedPckRegistry")
			?.GetMethod("IsManaged", [typeof(string)])?.CreateDelegate<Func<string, bool>>();

	static MakotoSelectSkin()
	{
		AppDomain.CurrentDomain.AssemblyLoad += (_, args) =>
		{
			if (args.LoadedAssembly.GetName().Name == "Gurio.SkinChanger") Selection = new(FindSelection);
			if (args.LoadedAssembly.GetName().Name == "Sts2SkinManager") _managedPack = new(FindPackRegistry);
		};
	}

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
