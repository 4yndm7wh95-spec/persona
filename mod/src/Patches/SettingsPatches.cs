using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using P3Makoto.Core;

namespace P3Makoto.Patches;

/// <summary>游戏常规设置中的结城理选项；使用简短、直白的名称。</summary>
[HarmonyPatch(typeof(NSettingsScreen), nameof(NSettingsScreen._Ready))]
public static class P3SettingsSectionPatch
{
	private const string SectionName = "P3MakotoSettings";
	private static readonly Color Navy = new(0.03f, 0.1f, 0.3f, 0.95f);
	private static readonly Color Cyan = new(0.46f, 0.83f, 0.97f);

	public static void Postfix(NSettingsScreen __instance)
	{
		ModEntry.Safe("settings section", () =>
		{
			if (__instance.GetNodeOrNull<NSettingsPanel>("%GeneralSettings") is not NSettingsPanel panel || panel.Content is not VBoxContainer content) return;
			if (content.GetNodeOrNull(SectionName) != null) return;
			Build(content);
			AccessTools.Method(typeof(NSettingsPanel), "RefreshSize")?.Invoke(panel, null);
		});
	}

	private static bool Zh => TranslationServer.GetLocale().StartsWith("zh");

	public static void Build(VBoxContainer content)
	{
		// 字体 / 字号 / 颜色照抄原版"快速模式"那一行的标签，看起来是同一套设置
		Font? font = null;
		int size = 28;
		Color color = new(0.95f, 0.9f, 0.8f);
		float rowH = 64f;
		if (content.GetNodeOrNull<Control>("FastMode") is Control sample)
		{
			rowH = Mathf.Max(rowH, sample.Size.Y);
			foreach (Node n in sample.FindChildren("*", "RichTextLabel", true, false))
			{
				RichTextLabel l = (RichTextLabel)n;
				font = l.GetThemeFont("normal_font");
				size = l.GetThemeFontSize("normal_font_size");
				color = l.GetThemeColor("default_color");
				break;
			}
		}

		VBoxContainer section = new() { Name = SectionName };
		section.AddThemeConstantOverride("separation", 6);
		content.AddChild(section);

		Label header = new() { Text = Zh ? "结城理" : "Makoto" };
		Style(header, font, size + 2, Cyan);
		header.CustomMinimumSize = new Vector2(0f, rowH);
		header.VerticalAlignment = VerticalAlignment.Center;
		section.AddChild(header);

		Label hint = new()
		{
			Text = Zh ? "设置自动保存。极速模式会跳过战斗动画。建筑师房间下次进入生效。"
				: "Changes save automatically. Instant mode skips combat animations. Architect changes apply on the next visit.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		Style(hint, font, Mathf.Max(16, size - 8), new Color(color, 0.65f));
		section.AddChild(hint);

		P3Settings.Data d = P3Settings.Current;
		Row(section, font, size, color, rowH, Zh ? "抽牌堆排序" : "Draw pile sorting", () => d.DrawPileOrder, v => d.DrawPileOrder = v, on => on ? (Zh ? "原版顺序" : "Original") : (Zh ? "按类型" : "By type"));
		Row(section, font, size, color, rowH, Zh ? "精英/首领战结算动画" : "Elite / boss victory animation", () => d.AllOut, v => d.AllOut = v);
		Row(section, font, size, color, rowH, Zh ? "胜利提示" : "Victory message", () => d.Victory, v => d.Victory = v);
		// One selector, keeping the existing saved flags compatible: Full -> Quick -> Off.
		Row(section, font, size, color, rowH, Zh ? "选牌动画" : "Card reward animation", () => d.ShuffleTime, _ =>
		{
			if (!d.ShuffleTime) { d.ShuffleTime = true; d.ShuffleFull = true; }
			else if (d.ShuffleFull) d.ShuffleFull = false;
			else d.ShuffleTime = false;
		}, on => !on ? (Zh ? "关闭" : "Off") : d.ShuffleFull ? (Zh ? "完整" : "Full") : (Zh ? "精简" : "Quick"));
		Row(section, font, size, color, rowH, Zh ? "召唤时眼部特写" : "Summon eye close-up", () => d.CutIn, v => d.CutIn = v);
		Row(section, font, size, color, rowH, Zh ? "选人动画" : "Character select animation", () => d.SelectSummon, v => d.SelectSummon = v);
		Row(section, font, size, color, rowH, Zh ? "建筑师房间动画" : "Architect room animation", () => d.Architect, v => d.Architect = v);
		Row(section, font, size, color, rowH, Zh ? "牌堆界面样式" : "Pile screen style", () => d.PileScreen, v => d.PileScreen = v, StyleName);
		Row(section, font, size, color, rowH, Zh ? "状态说明样式" : "Status tooltip style", () => d.TipList, v => d.TipList = v, StyleName);
		Row(section, font, size, color, rowH, Zh ? "鼠标样式" : "Cursor style", () => d.CursorSkin, v =>
		{
			d.CursorSkin = v;
			CursorPatch.RefreshExisting();
		}, on => on ? (Zh ? "P3风格" : "P3 style") : (Zh ? "原版" : "Original"));
	}

	private static string StyleName(bool on) => on ? (Zh ? "P3风格" : "P3 style") : (Zh ? "原版" : "Original");

	private static void Row(VBoxContainer parent, Font? font, int size, Color color, float rowH, string text, Func<bool> get, Action<bool> set, Func<bool, string>? display = null)
	{
		HBoxContainer row = new() { CustomMinimumSize = new Vector2(0f, rowH) };
		parent.AddChild(row);
		Label label = new() { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
		Style(label, font, size, color);
		row.AddChild(label);
		Button toggle = new() { ToggleMode = true, ButtonPressed = get(), CustomMinimumSize = new Vector2(150f, rowH * 0.7f), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		toggle.AddThemeFontOverride("font", TranslationServer.GetLocale().StartsWith("zh") ? Vfx.Typo.CjkBold : Vfx.Typo.Barlow);
		toggle.AddThemeFontSizeOverride("font_size", Mathf.Max(16, size - 4));
		// v0.14.67：斜切（P3R 风格）—— 开：深蓝底青边，关：灰底；悬停提亮；焦点白边
		StyleBoxFlat Box(Color bg, Color border) => new() { BgColor = bg, BorderColor = border, BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2, ContentMarginLeft = 14, ContentMarginRight = 14, Skew = new Vector2(0.3f, 0f), AntiAliasing = true };
		void Refresh()
		{
			bool on = get();
			toggle.SetPressedNoSignal(on);
			toggle.Text = display != null ? display(on) : (on ? (Zh ? "开启" : "On") : (Zh ? "关闭" : "Off"));
			Color bg = on ? Navy : new Color(0.12f, 0.12f, 0.14f, 0.9f);
			Color border = on ? Cyan : new Color(0.5f, 0.5f, 0.55f);
			toggle.AddThemeStyleboxOverride("normal", Box(bg, border));
			toggle.AddThemeStyleboxOverride("pressed", Box(bg, border));
			toggle.AddThemeStyleboxOverride("hover", Box(bg.Lightened(0.15f), border));
			toggle.AddThemeStyleboxOverride("hover_pressed", Box(bg.Lightened(0.15f), border));
			toggle.AddThemeStyleboxOverride("focus", Box(new Color(0f, 0f, 0f, 0f), Colors.White));
			toggle.AddThemeColorOverride("font_color", on ? Colors.White : new Color(0.75f, 0.75f, 0.78f));
			toggle.AddThemeColorOverride("font_pressed_color", Colors.White);
			toggle.AddThemeColorOverride("font_hover_color", Colors.White);
			toggle.AddThemeColorOverride("font_hover_pressed_color", Colors.White);
		}
		toggle.Toggled += on => ModEntry.Safe("settings toggle", () =>
		{
			set(on);
			P3Settings.Save();
			Refresh();
		});
		Refresh();
		row.AddChild(toggle);
	}

	private static void Style(Label l, Font? font, int size, Color color)
	{
		font ??= Zh ? Vfx.Typo.CjkBold : Vfx.Typo.Barlow;
		if (font != null)
		{
			l.AddThemeFontOverride("font", font);
		}
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", color);
	}
}

/// <summary>v0.14.67：设置界面 P3R 主菜单风格（Scenes/SettingsSkin）；推迟一帧挂，确保本 mod 那一栏已经建好。</summary>
[HarmonyPatch(typeof(NSettingsScreen), nameof(NSettingsScreen._Ready))]
public static class SettingsSkinPatch
{
	public static void Postfix(NSettingsScreen __instance)
	{
		NSettingsScreen s = __instance;
		Callable.From(() => ModEntry.Safe("settings skin", () =>
		{
			if (GodotObject.IsInstanceValid(s))
			{
				Scenes.SettingsSkin.Attach(s);
			}
		})).CallDeferred();
	}
}

