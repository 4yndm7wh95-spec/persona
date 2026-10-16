using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using P3Makoto.Core;

namespace P3Makoto.Patches;

/// <summary>
/// v0.14.46：游戏「设置 → 常规」最下面加一栏 P3MAKOTO，每项演出「完整 / 关闭」（游戏的模组菜单只能启用 / 禁用，没有给 mod 的配置页）。
/// 只用普通 Godot 控件，不复制带脚本的原版设置行（它们各自绑着原版的某个设置）。0.107.1 / 0.111 的设置界面结构相同（%GeneralSettings → Content）。
/// </summary>
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
			if (__instance.GetNodeOrNull<NSettingsPanel>("%GeneralSettings") is not NSettingsPanel panel || panel.Content is not VBoxContainer content)
			{
				return;
			}
			if (content.GetNodeOrNull(SectionName) != null)
			{
				return;
			}
			Build(content);
			// 面板高度按内容算（原版只在 _Ready / 显隐变化时算），加了行之后补算一次，滚动范围才包含新的这几行
			AccessTools.Method(typeof(NSettingsPanel), "RefreshSize")?.Invoke(panel, null);
		});
	}

	private static bool Zh => TranslationServer.GetLocale().StartsWith("zh");

	private static void Build(VBoxContainer content)
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

		Label header = new() { Text = Zh ? "P3MAKOTO · 结城理演出" : "P3MAKOTO · Makoto presentation" };
		Style(header, font, size + 2, Cyan);
		header.CustomMinimumSize = new Vector2(0f, rowH);
		header.VerticalAlignment = VerticalAlignment.Center;
		section.AddChild(header);

		Label hint = new()
		{
			Text = Zh ? "关闭 = 不播这段演出，直接走原版流程。快速模式为「极速」时全部视为关闭。建筑师房间在下次进房间时生效。"
				: "Off = skip it and use the vanilla flow. Instant fast mode turns all of these off. The Architect room applies on the next visit.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart,
		};
		Style(hint, font, Mathf.Max(16, size - 8), new Color(color, 0.65f));
		section.AddChild(hint);

		P3Settings.Data d = P3Settings.Current;
		Row(section, font, size, color, rowH, Zh ? "总攻击（精英 / Boss 战胜利）" : "All-Out Attack (elite / boss wins)", () => d.AllOut, v => d.AllOut = v);
		Row(section, font, size, color, rowH, Zh ? "战斗胜利演出" : "Victory banner", () => d.Victory, v => d.Victory = v);
		Row(section, font, size, color, rowH, Zh ? "选卡 Shuffle Time" : "Card reward Shuffle Time", () => d.ShuffleTime, v => d.ShuffleTime = v);
		Row(section, font, size, color, rowH, Zh ? "Persona 召唤切入（眼神特写）" : "Persona summon cut-in", () => d.CutIn, v => d.CutIn = v);
		Row(section, font, size, color, rowH, Zh ? "选人时的召唤演出" : "Character select summon", () => d.SelectSummon, v => d.SelectSummon = v);
		Row(section, font, size, color, rowH, Zh ? "建筑师房间（关闭 = 全部原版）" : "Architect room (off = all vanilla)", () => d.Architect, v => d.Architect = v);
	}

	private static void Row(VBoxContainer parent, Font? font, int size, Color color, float rowH, string text, Func<bool> get, Action<bool> set)
	{
		HBoxContainer row = new() { CustomMinimumSize = new Vector2(0f, rowH) };
		parent.AddChild(row);
		Label label = new() { Text = text, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
		Style(label, font, size, color);
		row.AddChild(label);
		Button toggle = new() { ToggleMode = true, ButtonPressed = get(), CustomMinimumSize = new Vector2(150f, rowH * 0.7f), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
		if (font != null)
		{
			toggle.AddThemeFontOverride("font", font);
		}
		toggle.AddThemeFontSizeOverride("font_size", Mathf.Max(16, size - 4));
		StyleBoxFlat Box(Color bg, Color border) => new() { BgColor = bg, BorderColor = border, BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 2, ContentMarginLeft = 12, ContentMarginRight = 12 };
		void Refresh()
		{
			bool on = toggle.ButtonPressed;
			toggle.Text = on ? (Zh ? "完整" : "Full") : (Zh ? "关闭" : "Off");
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
		if (font != null)
		{
			l.AddThemeFontOverride("font", font);
		}
		l.AddThemeFontSizeOverride("font_size", size);
		l.AddThemeColorOverride("font_color", color);
	}
}
