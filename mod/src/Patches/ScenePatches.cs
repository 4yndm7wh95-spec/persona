using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using P3Makoto.Core;
using P3Makoto.Scenes;

namespace P3Makoto.Patches;

/// <summary>君王之刃：结城理锻造出的剑换成 P3 蓝。</summary>
[HarmonyPatch(typeof(NSovereignBladeVfx), "_Ready")]
public static class SovereignBladeReskinPatch
{
	public static void Postfix(NSovereignBladeVfx __instance)
	{
		ModEntry.Safe("BladeReskin", () =>
		{
			if (Makoto.Is(__instance.Card?.Owner))
			{
				BladeReskin.Apply(__instance);
			}
		});
	}
}

/// <summary>选人界面背景换成 P3R 风格（见 CharSelectBg）。</summary>
[HarmonyPatch(typeof(NRegentCharacterSelectBg), "_Ready")]
public static class CharSelectBgPatch
{
	public static void Postfix(NRegentCharacterSelectBg __instance)
	{
		ModEntry.Safe("CharSelectBg", () => CharSelectBg.Build(__instance));
	}
}
