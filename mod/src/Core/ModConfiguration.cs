using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;

[assembly: AssemblyMetadata("RitsuLib.ModSettingsInterop.ProviderType", "P3Makoto.Core.ModConfiguration")]

namespace P3Makoto.Core;

/// <summary>沿用已安装 RegentFX 的 RitsuLib schema 协议，无硬依赖；旧版也可从模组详情打开。</summary>
public static class ModConfiguration
{
	private static bool Zh => TranslationServer.GetLocale().StartsWith("zh");
	private static readonly string[] Keys = { "AllOut", "Victory", "CutIn", "SelectSummon", "Architect", "PileScreen", "TipList", "CursorSkin", "DrawPileOrder" };
	private static readonly string[] LabelsZh = { "总攻击（精英 / Boss 战胜利）", "战斗胜利演出", "Persona 召唤切入", "选人时的召唤演出", "建筑师房间", "牌堆 / 牌组界面（P3R 风格）", "状态说明列表（P3R 风格）", "鼠标光标（P3R 风格）", "抽牌堆按原版顺序显示" };
	private static readonly string[] LabelsEn = { "All-Out Attack", "Victory banner", "Persona summon cut-in", "Character select summon", "Architect room", "P3R pile / deck screens", "P3R status tip list", "P3R mouse cursor", "Vanilla draw pile order" };
	public static object CreateRitsuLibSettingsSchema()
	{
		object Choice(string key, string label, string[] values, string[] labels) => new { id = key, key, type = "choice", label, scope = "global", options = Options(values, labels) };
		List<object> entries = new()
		{
			Choice("CardArt", Zh ? "卡面插画（立即生效）" : "Card illustrations (applies immediately)", new[] { "new", "vanilla" }, Zh ? new[] { "新版插画", "原版插画" } : new[] { "New artwork", "Vanilla" }),
			Choice("ShuffleMode", "SHUFFLE TIME", new[] { "full", "quick", "off" }, Zh ? new[] { "完整", "精简", "关闭" } : new[] { "Full", "Quick", "Off" })
		};
		for (int i = 0; i < Keys.Length; i++) entries.Add(new { id = Keys[i], key = Keys[i], type = "toggle", label = Zh ? LabelsZh[i] : LabelsEn[i], scope = "global" });
		return JsonSerializer.Serialize(new { modId = "P3Makoto", modDisplayName = "P3MAKOTO", modSidebarOrder = 50, pages = new[] { new { pageId = "main", title = Zh ? "结城理 · 模组配置" : "Makoto · Mod configuration", description = Zh ? "插画可随时切回原版，卡框保持原版。极速模式跳过战斗演出；建筑师房间下次进入生效。" : "Switch illustrations at any time. Instant mode skips combat presentations. Architect changes apply on the next visit.", sections = new[] { new { id = "presentation", title = Zh ? "画面与演出" : "Artwork and presentation", entries } } } } });
	}
	private static object[] Options(string[] values, string[] labels)
	{
		object[] options = new object[values.Length];
		for (int i = 0; i < values.Length; i++) options[i] = new { value = values[i], label = labels[i] };
		return options;
	}
	private static PropertyInfo? Property(string key) => Array.IndexOf(Keys, key) >= 0 ? typeof(P3Settings.Data).GetProperty(key) : null;
	public static object? GetRitsuLibSettingValue(string key) => key is "CardArt" or "ShuffleMode" ? GetRitsuLibSettingString(key) : Property(key)?.GetValue(P3Settings.Current);
	public static bool GetRitsuLibSettingBool(string key) => Property(key)?.GetValue(P3Settings.Current) is true;
	public static string GetRitsuLibSettingString(string key) => key switch
	{
		"CardArt" => P3Settings.Current.CardArt ? "new" : "vanilla",
		"ShuffleMode" => !P3Settings.Current.ShuffleTime ? "off" : P3Settings.Current.ShuffleFull ? "full" : "quick",
		_ => ""
	};
	public static void SetRitsuLibSettingValue(string key, object? value)
	{
		if (value is JsonElement element) value = element.ValueKind == JsonValueKind.String ? element.GetString() : element.ValueKind == JsonValueKind.True;
		if (key is "CardArt" or "ShuffleMode") SetRitsuLibSettingString(key, value?.ToString() ?? "");
		else if (value is bool flag) SetRitsuLibSettingBool(key, flag);
	}
	public static void SetRitsuLibSettingBool(string key, bool value)
	{
		Property(key)?.SetValue(P3Settings.Current, value);
		if (key == "CursorSkin") ModEntry.Safe("config cursor", Patches.CursorPatch.RefreshExisting);
	}
	public static void SetRitsuLibSettingString(string key, string value)
	{
		if (key == "CardArt" && value is "new" or "vanilla")
		{
			P3Settings.Current.CardArt = value == "new";
			ModEntry.Safe("config artwork", Scenes.ApprovedCardArt.RefreshExisting);
		}
		if (key == "ShuffleMode" && value is "full" or "quick" or "off")
		{
			P3Settings.Current.ShuffleTime = value != "off";
			P3Settings.Current.ShuffleFull = value == "full";
		}
	}
	public static void SaveRitsuLibSettings() => P3Settings.Save();

	public static void Open(Control host)
	{
		if (host.HasNode("P3Configuration")) return;
		CanvasLayer layer = new() { Name = "P3Configuration", Layer = 100 };
		host.AddChild(layer);
		Control root = new(); layer.AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		ColorRect dim = new() { Color = new Color(0, 0, 0, 0.8f) }; root.AddChild(dim); dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		PanelContainer panel = new(); root.AddChild(panel);
		panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
		panel.AnchorLeft = 0.1f; panel.AnchorTop = 0.1f; panel.AnchorRight = 0.9f; panel.AnchorBottom = 0.9f;
		panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0.035f, 0.065f, 0.12f), ContentMarginLeft = 32, ContentMarginRight = 32, ContentMarginTop = 20, ContentMarginBottom = 20 });
		VBoxContainer body = new(); panel.AddChild(body);
		Button close = new() { Text = Zh ? "关闭" : "Close", SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd };
		close.AddThemeFontOverride("font", Zh ? Vfx.Typo.CjkBold : Vfx.Typo.Barlow); close.AddThemeFontSizeOverride("font_size", 26);
		close.Pressed += () => layer.QueueFree(); body.AddChild(close);
		ScrollContainer scroll = new() { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; body.AddChild(scroll);
		VBoxContainer content = new() { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }; scroll.AddChild(content);
		Patches.P3SettingsSectionPatch.Build(content);
		close.GrabFocus();
	}
}

[HarmonyPatch(typeof(NModInfoContainer), nameof(NModInfoContainer.Fill))]
internal static class ModConfigurationButtonPatch
{
	public static void Postfix(NModInfoContainer __instance, Mod mod) => ModEntry.Safe("mod config button", () =>
	{
		Button? button = __instance.GetNodeOrNull<Button>("P3ConfigButton");
		if (button == null)
		{
			button = new Button { Name = "P3ConfigButton", Text = TranslationServer.GetLocale().StartsWith("zh") ? "模组配置" : "Mod configuration", ZIndex = 10 };
			button.AddThemeFontOverride("font", TranslationServer.GetLocale().StartsWith("zh") ? Vfx.Typo.CjkBold : Vfx.Typo.Barlow);
			button.AddThemeFontSizeOverride("font_size", 26);
			__instance.AddChild(button);
			button.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
			button.OffsetLeft = -230; button.OffsetRight = -10; button.OffsetTop = 6; button.OffsetBottom = 56;
			button.Pressed += () => ModConfiguration.Open(__instance);
		}
		button.Visible = mod.manifest?.id == "P3Makoto";
	});
}

[HarmonyPatch]
internal static class ModConfigurationClearPatch
{
	private static MethodBase? Target => AccessTools.Method(typeof(NModInfoContainer), "Clear");
	public static bool Prepare() => Target != null;
	public static MethodBase TargetMethod() => Target!;
	public static void Postfix(NModInfoContainer __instance)
	{
		if (__instance.GetNodeOrNull<Button>("P3ConfigButton") is Button button) button.Hide();
		__instance.GetNodeOrNull("P3Configuration")?.QueueFree();
	}
}
