using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using Option = P5Joker.P5Settings.Option;

namespace P5Joker;

[HarmonyPatch(typeof(NSettingsPanel), nameof(NSettingsPanel._Ready))]
internal static class P5SettingsSectionPatch
{
	internal const string SectionName = "P5JokerSettings";
	private const string SettingsControl = "p5_settings_control";
	private static readonly System.Reflection.MethodInfo? RefreshNavigation = AccessTools.Method(typeof(NSettingsPanel), "UpdateNavigation");
	private static readonly Color Red = new("ee454f"), Ink = new("181014"), Gray = new("77727a");
	private static bool Zh => TranslationServer.GetLocale().StartsWith("zh", StringComparison.OrdinalIgnoreCase);
	private static string T(string zh, string en) => Zh ? zh : en;

	private static void Prefix(NSettingsPanel __instance) => ModEntry.Safe("settings section", () =>
	{
		if (__instance.Name == "GeneralSettings" && __instance.GetNodeOrNull<VBoxContainer>("VBoxContainer") is { } content)
			Build(content);
	});

	// Register the controls in the game's existing focus list before the panel wires navigation.
	[HarmonyPatch(typeof(NSettingsPanel), "IsSettingsOption")]
	internal static class Navigation
	{
		private static bool Prepare() => AccessTools.Method(typeof(NSettingsPanel), "IsSettingsOption") != null;
		private static void Postfix(Control __0, ref bool __result)
		{
			if (__0.HasMeta(SettingsControl)) __result = true;
		}
	}

	internal static void Build(VBoxContainer content)
	{
		if (content.GetNodeOrNull(SectionName) != null) return;
		RichTextLabel? sample = content.GetNodeOrNull("FastMode")?.FindChildren("*", "RichTextLabel", true, false)
			.OfType<RichTextLabel>().FirstOrDefault();
		Font font = sample?.GetThemeFont("normal_font") ?? P5Ui.Font(Zh ? P5Ui.CjkMedium : P5Ui.Barlow);
		int size = sample?.GetThemeFontSize("normal_font_size") ?? 28;
		Color textColor = sample?.GetThemeColor("default_color") ?? new Color("f2eadf");
		VBoxContainer section = new() { Name = SectionName };
		section.AddThemeConstantOverride("separation", 6);
		content.AddChild(section);

		Label title = Text("Joker", font, size + 4, Colors.White);
		title.CustomMinimumSize = new Vector2(0, 60);
		section.AddChild(title);

		Label error = Text(T("设置保存失败", "Could not save settings"), font, size, Red);
		var switches = new List<(Option Option, Button Button)>();
		AddSwitch(Option.Reward, T("奖励选牌演出", "Card reward presentation"),
			T("关闭后，下次打开时使用原版选牌界面。", "Use the base game's card reward screen the next time it opens."));
		AddSwitch(Option.AllOut, T("Boss／精英战总攻击动画", "Boss / elite All-Out Attack"),
			T("关闭后，Boss 和精英战胜利时直接进入原版结算。", "Go straight to the regular results after winning a boss or elite fight."));
		AddSwitch(Option.Pile, T("牌堆界面（P5R 菜单风格）", "Pile screens (P5R menu style)"),
			T("关闭后，下次打开牌堆时使用原版界面。", "Use the base game's pile screens the next time one opens."));

		error.Hide();
		section.AddChild(error);
		section.VisibilityChanged += () => ModEntry.Safe("settings visibility", () =>
		{
			if (!section.IsInsideTree()) return;
			// The P3 page can hide this section while keeping the general panel open.
			var focus = section.Visible ? Control.FocusModeEnum.All : Control.FocusModeEnum.None;
			foreach (var (_, button) in switches) button.FocusMode = focus;
			if (section.IsVisibleInTree()) Refresh();
			if (content.GetParent() is NSettingsPanel panel && panel.IsNodeReady())
				RefreshNavigation?.Invoke(panel, null);
		});
		Refresh();

		void AddSwitch(Option option, string label, string? tip = null)
		{
			Button button = new() { Name = option.ToString(), ToggleMode = true };
			switches.Add((option, button));
			AddRow(label, button, tip);
			button.Toggled += on => Update(P5Settings.Set(option, on));
		}

		void AddRow(string label, Button button, string? tip)
		{
			HBoxContainer row = new() { CustomMinimumSize = new Vector2(0, 58) };
			section.AddChild(row);
			Label text = Text(label, font, size, textColor);
			text.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			row.AddChild(text);
			button.FocusMode = Control.FocusModeEnum.All;
			button.SetMeta(SettingsControl, true);
			button.CustomMinimumSize = new Vector2(148, 44);
			button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			button.AddThemeFontOverride("font", font);
			button.AddThemeFontSizeOverride("font_size", Math.Max(16, size - 4));
			if (tip != null) button.TooltipText = tip;
			row.AddChild(button);
		}

		void Update(bool saved)
		{
			error.Visible = !saved;
			Refresh();
		}

		void Refresh()
		{
			foreach (var (option, button) in switches)
			{
				bool on = P5Settings.Enabled(option);
				button.SetPressedNoSignal(on);
				button.Text = on ? T("开启", "On") : T("关闭", "Off");
				Style(button, on ? Red : Gray);
			}
		}
	}

	private static Label Text(string text, Font font, int size, Color color)
	{
		Label label = new() { Text = text, VerticalAlignment = VerticalAlignment.Center };
		label.AddThemeFontOverride("font", font);
		label.AddThemeFontSizeOverride("font_size", size);
		label.AddThemeColorOverride("font_color", color);
		return label;
	}

	private static void Style(Button button, Color edge)
	{
		StyleBoxFlat Box(Color bg, Color border) => new()
		{
			BgColor = bg, BorderColor = border, BorderWidthLeft = 4, BorderWidthRight = 2,
			BorderWidthTop = 2, BorderWidthBottom = 2, ContentMarginLeft = 12, ContentMarginRight = 12,
		};
		StyleBoxFlat normal = Box(Ink, edge), hover = Box(Ink.Lightened(0.12f), edge);
		button.AddThemeStyleboxOverride("normal", normal);
		button.AddThemeStyleboxOverride("pressed", normal);
		button.AddThemeStyleboxOverride("hover", hover);
		button.AddThemeStyleboxOverride("hover_pressed", hover);
		button.AddThemeStyleboxOverride("focus", Box(Colors.Transparent, Colors.White));
	}
}
