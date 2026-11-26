using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace P5Joker;

internal static class SkinChangerSelect
{
	internal const string ManagerId = "Gurio.SkinChanger";
	private const string ProviderId = "P5Joker";
	private delegate bool TryGetProvider(string groupId, string optionId, out string providerId);
	private static Func<string, bool?>? _selection;
	private static bool _checked;

	internal static bool Installed => _selection != null;

	// Character selection starts after the game has loaded all mods. This also
	// finds SkinChanger when its assembly was absent during Joker's initializer.
	internal static void Install()
	{
		if (_checked) return;
		_checked = true;
		Assembly? manager = AppDomain.CurrentDomain.GetAssemblies()
			.FirstOrDefault(assembly => assembly.GetName().Name == ManagerId);
		Type? controls = manager?.GetType("STS2SkinChanger.Ui.ContextualSkinControls");
		Type? service = manager?.GetType("STS2SkinChanger.Core.SkinService");
		if (controls == null || service == null) return;

		MethodInfo? rebuild = AccessTools.DeclaredMethod(controls, "RebuildCharacterDisplay",
			[typeof(NCharacterSelectScreen), typeof(CharacterModel), typeof(string)]);
		MethodInfo? displayed = AccessTools.DeclaredMethod(service, "GetDisplayedSelection", [typeof(string)])
			?? AccessTools.DeclaredMethod(service, "GetVisualSelection", [typeof(string)]);
		MethodInfo? getCatalog = AccessTools.PropertyGetter(service, "Catalog");
		if (rebuild == null || displayed == null || getCatalog == null) return;

		MethodInfo? getProvider = AccessTools.Method(getCatalog.ReturnType, "TryGetVisualProviderId",
			[typeof(string), typeof(string), typeof(string).MakeByRefType()]);
		if (getProvider == null) return;

		var display = displayed.CreateDelegate<Func<string, string>>();
		var catalog = getCatalog.CreateDelegate<Func<object?>>();
		object? cachedCatalog = null;
		TryGetProvider? lookup = null;
		new Harmony("brooks.p5joker.skinchanger-select").Patch(rebuild,
			postfix: new HarmonyMethod(typeof(SkinChangerSelect), nameof(AfterRebuild)));
		_selection = groupId =>
		{
			object? currentCatalog = catalog();
			if (currentCatalog == null) return null;
			if (!ReferenceEquals(currentCatalog, cachedCatalog))
			{
				lookup = getProvider.CreateDelegate<TryGetProvider>(currentCatalog);
				cachedCatalog = currentCatalog;
			}
			return lookup!(groupId, display(groupId), out string provider)
				? string.Equals(provider, ProviderId, StringComparison.OrdinalIgnoreCase) : null;
		};
	}

	internal static bool? ShowsJoker(string groupId) => JokerCostume.ManagedSelection ?? _selection?.Invoke(groupId);
	internal static bool? ShowsJoker() => ShowsJoker("silent");

	private static void AfterRebuild(NCharacterSelectScreen __0, CharacterModel __1, string __2) =>
		ModEntry.Safe("SkinChanger selection refresh", () => CharSelectBgPatch.AfterSkinChangerRefresh(__0, __1, __2));
}
