using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace P5Joker;

/// <summary>
/// 选人界面：选中静默猎手时，原版刚放进 AnimatedBg 的背景场景（char_select_bg_silent）换成 P5R 风格的选人画面（Vfx/CharSelectScene.cs）。
/// 原版每次选中都会重新实例化背景，所以每次选中都是"刚被选中"：播一次入场 + 眼神特写。搭建出错就保留原版（CharSelectScene.Build 自己回滚）。
/// 同时：原版静默猎手的选中音效拦掉（选人画面自己安排了分层的声音）；左侧信息面板等眼神特写碎开再淡入，免得盖住特写（同 P3 mod）。
/// </summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.SelectCharacter))]
public static class CharSelectBgPatch
{
	private static readonly AccessTools.FieldRef<NCharacterSelectScreen, Control> BgContainer =
		AccessTools.FieldRefAccess<NCharacterSelectScreen, Control>("_bgContainer");
	private static readonly AccessTools.FieldRef<NCharacterSelectScreen, Control> InfoPanel =
		AccessTools.FieldRefAccess<NCharacterSelectScreen, Control>("_infoPanel");
	private static Tween? _panelFade;

	/// <summary>原版先放选中音效、再换背景：这一次要换成 Joker 的画面，就先把那声音效拦下来。</summary>
	public static void Prefix(NCharacterSelectButton charSelectButton, CharacterModel characterModel)
	{
		CharSelectSfxMute.Armed = characterModel is Silent && !charSelectButton.IsRandom && !charSelectButton.IsLocked;
	}

	public static void Postfix(NCharacterSelectScreen __instance, NCharacterSelectButton charSelectButton, CharacterModel characterModel)
	{
		CharSelectSfxMute.Armed = false;
		ModEntry.Safe("CharSelectBg", () =>
		{
			Control? panel = InfoPanel(__instance);
			_panelFade?.Kill();
			_panelFade = null;
			if (panel != null && GodotObject.IsInstanceValid(panel))
			{
				panel.Modulate = Colors.White;                      // 选别的角色 / 快速切换：面板马上恢复
			}
			if (characterModel is not Silent || charSelectButton.IsLocked || charSelectButton.IsRandom)
			{
				return;
			}
			Control? bg = BgContainer(__instance)?.GetNodeOrNull<Control>(characterModel.Id.Entry + "_bg");
			if (bg == null || bg.GetNodeOrNull("P5Select") != null)
			{
				return;
			}
			bool intro = CharSelectScene.IntroEnabled();
			CharSelectScene.Build(bg, intro);
			if (intro && panel != null && GodotObject.IsInstanceValid(panel))
			{
				panel.Modulate = new Color(1f, 1f, 1f, 0f);
				_panelFade = panel.CreateTween();
				_panelFade.TweenInterval(CharSelectScene.InfoPanelDelay);
				_panelFade.TweenProperty(panel, "modulate:a", 1f, 0.25);
			}
		});
	}
}

/// <summary>拦掉一次原版静默猎手的选中音效（event:/sfx/characters/silent/silent_select）。只在 CharSelectBgPatch 要换成 Joker 画面的那一次生效。</summary>
[HarmonyPatch]
public static class CharSelectSfxMute
{
	public static bool Armed;
	private const string Event = "event:/sfx/characters/silent/silent_select";

	public static IEnumerable<MethodBase> TargetMethods() =>
		typeof(MegaCrit.Sts2.Core.Nodes.Audio.NAudioManager).GetMethods(BindingFlags.Public | BindingFlags.Instance)
			.Where(m => m.Name == "PlayOneShot" && m.GetParameters().Length > 0 && m.GetParameters()[0].ParameterType == typeof(string));

	public static bool Prefix(object[] __args)
	{
		if (!Armed || __args.Length == 0 || __args[0] is not string path || path != Event)
		{
			return true;
		}
		Armed = false;
		return false;
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
