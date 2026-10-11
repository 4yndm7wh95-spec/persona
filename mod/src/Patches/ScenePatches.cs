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

/// <summary>虚空形态：原版常驻特效换成影时间（见 FormFx）。</summary>
[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx), nameof(MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx.Create))]
public static class VoidFormVfxPatch
{
	public static void Postfix(MegaCrit.Sts2.Core.Entities.Creatures.Creature target, MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx? __result)
	{
		ModEntry.Safe("VoidFormVfx", () =>
		{
			if (__result != null && Makoto.Is(target.Player) && Makoto.Node(target.Player) is MegaCrit.Sts2.Core.Nodes.Combat.NCreature node)
			{
				Vfx.FormFx.DarkHour(__result, node);
			}
		});
	}
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx), nameof(MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx.SetActive))]
public static class VoidFormActivePatch
{
	public static void Postfix(MegaCrit.Sts2.Core.Nodes.Vfx.Forms.NVoidFormVfx __instance, bool isActive)
	{
		ModEntry.Safe("VoidFormActive", () => Vfx.FormFx.SetActive(__instance, isActive));
	}
}
