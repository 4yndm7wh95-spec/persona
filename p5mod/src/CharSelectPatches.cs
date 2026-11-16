using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace P5Joker;

/// <summary>
/// 选人界面：选中静默猎手时，原版刚放进 AnimatedBg 的背景场景（char_select_bg_silent）换成 P5R 风格的楼顶夜景（Vfx/CharSelectScene.cs）。
/// 原版每次选中都会重新实例化背景，所以每次选中都是"刚被选中"：播一次入场 + 召唤切入。搭建出错就保留原版（CharSelectScene.Build 自己回滚）。
/// </summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
public static class CharSelectBgPatch
{
	private static readonly AccessTools.FieldRef<NCharacterSelectScreen, Control> BgContainer =
		AccessTools.FieldRefAccess<NCharacterSelectScreen, Control>("_bgContainer");

	public static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton charSelectButton, CharacterModel characterModel)
	{
		ModEntry.Safe("CharSelectBg", () =>
		{
			if (characterModel is not Silent || charSelectButton.IsLocked)
			{
				return;
			}
			Control? bg = BgContainer(__instance)?.GetNodeOrNull<Control>(characterModel.Id.Entry + "_bg");
			if (bg != null && bg.GetNodeOrNull("P5Select") == null)
			{
				CharSelectScene.Build(bg, intro: true);
			}
		});
	}
}

/// <summary>选人界面底部的角色按钮：静默猎手换成 Joker 头像（res://p5joker/fx/cs_button.png，未解锁时原版的锁定图不动）。</summary>
[HarmonyPatch(typeof(NCharacterSelectButton), nameof(NCharacterSelectButton.Init))]
public static class CharSelectButtonPatch
{
	private static readonly AccessTools.FieldRef<NCharacterSelectButton, TextureRect> Icon =
		AccessTools.FieldRefAccess<NCharacterSelectButton, TextureRect>("_icon");

	public static void Postfix(NCharacterSelectButton __instance, CharacterModel character)
	{
		ModEntry.Safe("CharSelectButton", () =>
		{
			if (character is Silent && !__instance.IsLocked && Fx.Tex("cs_button") is Texture2D tex && Icon(__instance) is TextureRect icon)
			{
				icon.Texture = tex;
			}
		});
	}
}
