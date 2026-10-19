using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Nodes.Cards;
using P3Makoto.Core;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// v0.14.81 储君卡牌的卡框换成 P3R 塔罗牌风格（art/card_frame，照 P3R 原版阿卡纳牌 T_FX_Arcana_01_Col 画）：
/// 版式完全不动，只把原版拼卡框的几张小图换掉 —— 卡框本体（按类型）、卡名横幅 / 卡图边框 / 类型标签（按稀有度：银白 / 冰蓝 / 暗金），
/// 新图自带颜色，所以去掉原版给它们上色的 hsv 材质；卡框下沿正中加一块阿卡纳数字牌（每张卡对应一张大阿卡纳，升级后数字变金）。
/// 有重绘卡图的卡（art/cards/v3 → p3makoto/cards/）顺便换卡图。
/// NCard 会被复用来显示别的卡：原版 Reload 会把卡框 / 横幅 / 边框的贴图和材质重新设回去，只有类型标签的贴图原版不设，这里自己还原。
/// </summary>
public static class CardFrameSkin
{
	private const string Dir = "res://p3makoto/card_frame/";
	private const string TabName = "P3ArcanaTab";

	private static readonly AccessTools.FieldRef<NCard, TextureRect> Frame = AccessTools.FieldRefAccess<NCard, TextureRect>("_frame");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> Border = AccessTools.FieldRefAccess<NCard, TextureRect>("_portraitBorder");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> Banner = AccessTools.FieldRefAccess<NCard, TextureRect>("_banner");
	private static readonly AccessTools.FieldRef<NCard, NinePatchRect> Plaque = AccessTools.FieldRefAccess<NCard, NinePatchRect>("_typePlaque");
	private static readonly AccessTools.FieldRef<NCard, TextureRect> Portrait = AccessTools.FieldRefAccess<NCard, TextureRect>("_portrait");
	private static readonly AccessTools.FieldRef<NCard, Label> Title = AccessTools.FieldRefAccess<NCard, Label>("_titleLabel");

	private static readonly Dictionary<string, Texture2D?> Cache = new();
	private static Texture2D? _vanillaPlaque;

	// 每张卡对应的大阿卡纳（docs/codex/codex_card_art.md【5】）
	private static readonly Dictionary<string, string> Arcana = new()
	{
		["strike_regent"] = "0", ["defend_regent"] = "0", ["falling_star"] = "XVII", ["venerate"] = "V", ["astral_pulse"] = "XVIII", ["celestial_might"] = "XI", ["cloak_of_stars"] = "XVII", ["collision_course"] = "XVI", ["cosmic_indifference"] = "IX", ["crescent_spear"] = "XVIII", ["crush_under"] = "XI", ["gather_light"] = "XIX", ["glitterstream"] = "XIV", ["glow"] = "XVII", ["guiding_star"] = "XVII", ["hidden_cache"] = "II", ["know_thy_place"] = "VIII", ["patter"] = "XVII", ["photon_cut"] = "I", ["refine_blade"] = "XIV", ["solar_strike"] = "XIX", ["spoils_of_battle"] = "VII", ["wrought_in_war"] = "VII", ["begone"] = "XIII", ["alignment"] = "XVII", ["black_hole"] = "XIII", ["bulwark"] = "VIII", ["charge"] = "VII", ["child_of_the_stars"] = "XVII", ["conqueror"] = "IV", ["constellation"] = "VI", ["convergence"] = "X", ["devastate"] = "XIII", ["furnace"] = "XV", ["gamma_blast"] = "XVI", ["glimmer"] = "II", ["hegemony"] = "IV", ["kingly_kick"] = "IV", ["kingly_punch"] = "IV", ["knockout_blow"] = "XIII", ["largesse"] = "III", ["lunar_blast"] = "XVIII", ["manifest_authority"] = "IV", ["monologue"] = "IX", ["orbit"] = "X", ["pale_blue_dot"] = "XVII", ["parry"] = "VIII", ["particle_wall"] = "XI", ["plot"] = "XV", ["pillar_of_creation"] = "XX", ["prophesize"] = "II", ["quasar"] = "I", ["radiate"] = "XIX", ["reflect"] = "XVIII", ["resonance"] = "VI", ["royal_gamble"] = "X", ["shining_strike"] = "XIX", ["spectrum_shift"] = "XIV", ["stardust"] = "XVII", ["summon_forth"] = "VII", ["supermassive"] = "XXI", ["terraforming"] = "III", ["arsenal"] = "VII", ["beat_into_shape"] = "XV", ["big_bang"] = "XXI", ["bombardment"] = "XVI", ["bundle_of_joy"] = "III", ["comet"] = "XVII", ["crash_landing"] = "XVI", ["decisions_decisions"] = "X", ["dying_star"] = "XIII", ["foregone_conclusion"] = "XX", ["genesis"] = "XX", ["guards"] = "VIII", ["hammer_time"] = "XV", ["heavenly_drill"] = "XVI", ["heirloom_hammer"] = "XV", ["i_am_invincible"] = "XI", ["make_it_so"] = "I", ["monarchs_gaze"] = "IV", ["neutron_aegis"] = "VIII", ["royalties"] = "III", ["seeking_edge"] = "VIII", ["seven_stars"] = "XVII", ["sword_sage"] = "VII", ["the_smith"] = "XV", ["tutor"] = "IX", ["tyranny"] = "IV", ["void_form"] = "XXI", ["meteor_shower"] = "XVII", ["the_sealed_throne"] = "IV",
	};

	private static Texture2D? Tex(string path)
	{
		if (!Cache.TryGetValue(path, out Texture2D? t))
		{
			t = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
			Cache[path] = t;
		}
		return t;
	}

	/// <summary>原版横幅是带 margin = Rect2(1, 23, 2, 25) 的 AtlasTexture（上下补透明边再拉进节点）；新图包一层同样的 margin，位置才和原版一致。</summary>
	private static Texture2D? BannerTex(string rarity)
	{
		string key = "banner:" + rarity;
		if (!Cache.TryGetValue(key, out Texture2D? t))
		{
			t = Tex(Dir + "card_banner_" + rarity + ".png") is Texture2D raw
				? new AtlasTexture { Atlas = raw, Region = new Rect2(0, 0, raw.GetWidth(), raw.GetHeight()), Margin = new Rect2(1, 23, 2, 25) }
				: null;
			Cache[key] = t;
		}
		return t;
	}

	private static string? Rarity(CardRarity r) => r switch
	{
		CardRarity.Basic or CardRarity.Common => "common",
		CardRarity.Uncommon => "uncommon",
		CardRarity.Rare => "rare",
		_ => null,
	};

	private static string? Kind(CardType t) => t switch
	{
		CardType.Attack => "attack",
		CardType.Skill => "skill",
		CardType.Power => "power",
		_ => null,
	};

	public static void Apply(NCard card)
	{
		CardModel? m = card.Model;
		NinePatchRect plaque = Plaque(card);
		_vanillaPlaque ??= plaque.Texture;
		string? rarity = m == null ? null : Rarity(m.Rarity);
		string? kind = m == null ? null : Kind(m.Type);
		bool on = P3Settings.CardFrame && m != null && m.VisualCardPool is RegentCardPool && rarity != null && kind != null
			&& Tex(Dir + $"card_frame_{kind}_s.png") != null;
		Control? tab = card.Body.GetNodeOrNull<Control>(TabName);
		if (!on)
		{
			if (_vanillaPlaque != null && plaque.Texture != _vanillaPlaque)
			{
				plaque.Texture = _vanillaPlaque;
			}
			if (tab != null)
			{
				tab.Visible = false;
			}
			TitleStyle(card, null);
			return;
		}

		TextureRect frame = Frame(card);
		frame.Texture = Tex(Dir + $"card_frame_{kind}_s.png");
		frame.Material = null;
		TextureRect border = Border(card);
		border.Texture = Tex(Dir + $"card_portrait_border_{kind}_{rarity}.png") ?? border.Texture;
		border.Material = null;
		TextureRect banner = Banner(card);
		banner.Texture = BannerTex(rarity!) ?? banner.Texture;
		banner.Material = null;
		plaque.Texture = Tex(Dir + $"card_portrait_border_plaque_{rarity}.png") ?? plaque.Texture;
		plaque.Material = null;

		string id = m!.Id.Entry.ToLowerInvariant();
		if (Tex($"res://p3makoto/cards/{id}.png") is Texture2D art)
		{
			Portrait(card).Texture = art;
		}

		TitleStyle(card, m);
		tab ??= MakeTab(card);
		tab.Visible = Arcana.ContainsKey(id);
		if (tab.Visible)
		{
			Label num = tab.GetNode<Label>("Num");
			num.Text = Arcana[id];
			// 升级后数字变金（塔罗的"正位"觉醒）
			num.AddThemeColorOverride("font_color", m.IsUpgraded ? new Color(1f, 0.86f, 0.5f) : Colors.White);
		}
	}

	private static Font? _titleZh, _titleZht;

	/// <summary>
	/// 卡名（用户：原来的字体丑）：像塔罗牌的铭牌 —— 金属横幅上刻出来的深色衬线字，不要原版的粗描边。
	/// 中文用游戏自带的思源宋体（fonts/zhs、zht），其他语言用 Cinzel（缺字回退到原字体）；升级后的卡沿用原版"绿色"的约定，用深绿。
	/// NCard 复用给别的卡时（m = null）还原原版的字体、颜色、描边、阴影。
	/// </summary>
	private static void TitleStyle(NCard card, CardModel? m)
	{
		Label t = Title(card);
		if (m == null)
		{
			if (t.HasMeta("p3_font"))
			{
				t.AddThemeFontOverride("font", t.GetMeta("p3_font").As<Font>());
				t.AddThemeColorOverride("font_shadow_color", t.GetMeta("p3_shadow").AsColor());
				t.AddThemeConstantOverride("outline_size", t.GetMeta("p3_outline").AsInt32());
				t.AddThemeConstantOverride("shadow_outline_size", t.GetMeta("p3_shadow_outline").AsInt32());
				t.AddThemeConstantOverride("shadow_offset_x", t.GetMeta("p3_sx").AsInt32());
				t.AddThemeConstantOverride("shadow_offset_y", t.GetMeta("p3_sy").AsInt32());
				t.RemoveMeta("p3_font");
			}
			return;
		}
		if (!t.HasMeta("p3_font"))
		{
			Font orig = t.GetThemeFont("font");
			t.SetMeta("p3_font", orig);
			t.SetMeta("p3_shadow", t.GetThemeColor("font_shadow_color"));
			t.SetMeta("p3_outline", t.GetThemeConstant("outline_size"));
			t.SetMeta("p3_shadow_outline", t.GetThemeConstant("shadow_outline_size"));
			t.SetMeta("p3_sx", t.GetThemeConstant("shadow_offset_x"));
			t.SetMeta("p3_sy", t.GetThemeConstant("shadow_offset_y"));
			string loc = TranslationServer.GetLocale().ToLowerInvariant();
			bool zht = loc.StartsWith("zh") && (loc.Contains("tw") || loc.Contains("hk") || loc.Contains("hant") || loc.Contains("zht"));
			string path = zht ? "res://fonts/zht/SourceHanSerifTC-Bold.otf" : "res://fonts/zhs/SourceHanSerifSC-Bold.otf";
			Font? serif = null;
			if (loc.StartsWith("zh") && ResourceLoader.Exists(path))
			{
				serif = zht ? (_titleZht ??= ResourceLoader.Load<Font>(path)) : (_titleZh ??= ResourceLoader.Load<Font>(path));
			}
			Font baseFont = serif ?? Typo.Cinzel;
			t.AddThemeFontOverride("font", new FontVariation { BaseFont = baseFont, SpacingGlyph = serif != null ? 2 : 1, Fallbacks = new Godot.Collections.Array<Font> { orig } });
			t.AddThemeConstantOverride("outline_size", 0);
			t.AddThemeConstantOverride("shadow_outline_size", 0);
			t.AddThemeConstantOverride("shadow_offset_x", 0);
			t.AddThemeConstantOverride("shadow_offset_y", 1);
			t.AddThemeColorOverride("font_shadow_color", new Color(1f, 1f, 1f, 0.55f));   // 浅色压印：字像刻进金属里
		}
		Color ink = m.IsUpgraded ? new Color(0.04f, 0.3f, 0.18f) : m.Rarity == CardRarity.Rare ? new Color(0.24f, 0.17f, 0.04f) : new Color(0.07f, 0.14f, 0.24f);
		t.AddThemeColorOverride("font_color", ink);
	}

	/// <summary>卡框下沿正中的数字牌：场景坐标 (-42, 183) ~ (42, 215)，和 tools/ui/card_compose.py 的 "arcana" 一致。</summary>
	private static Control MakeTab(NCard card)
	{
		Control tab = new() { Name = TabName, MouseFilter = Control.MouseFilterEnum.Ignore };
		tab.SetAnchorsPreset(Control.LayoutPreset.Center);
		tab.OffsetLeft = -42f;
		tab.OffsetTop = 183f;
		tab.OffsetRight = 42f;
		tab.OffsetBottom = 215f;
		card.Body.AddChild(tab);
		TextureRect bg = new() { Texture = Tex(Dir + "arcana_tab.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore };
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		tab.AddChild(bg);
		Label num = new() { Name = "Num", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
		num.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		num.OffsetTop = 3f;
		num.AddThemeFontOverride("font", Typo.Cinzel);
		num.AddThemeFontSizeOverride("font_size", 17);
		num.AddThemeColorOverride("font_outline_color", new Color(0.06f, 0.14f, 0.22f));
		num.AddThemeConstantOverride("outline_size", 4);
		tab.AddChild(num);
		return tab;
	}
}

/// <summary>原版 NCard.Reload 设完卡框 / 横幅 / 标签之后换成塔罗卡框（UpdatePortrait 单独被调用时也会重设卡框和横幅，一起补上）。</summary>
[HarmonyPatch(typeof(NCard), "Reload")]
public static class CardFrameReloadPatch
{
	public static void Postfix(NCard __instance) => ModEntry.Safe("card frame", () => CardFrameSkin.Apply(__instance));
}

/// <summary>原版 UpdateTitleLabel 每次都把卡名颜色设回奶油色 / 绿色，之后再上一次我们的颜色。</summary>
[HarmonyPatch(typeof(NCard), "UpdateTitleLabel")]
public static class CardFrameTitlePatch
{
	public static void Postfix(NCard __instance) => ModEntry.Safe("card frame", () => CardFrameSkin.Apply(__instance));
}

/// <summary>0.107.1 没有 UpdatePortrait（那一版只在 Reload 里设）：方法不存在就不打这个补丁。</summary>
[HarmonyPatch]
public static class CardFramePortraitPatch
{
	private static System.Reflection.MethodBase? Target => AccessTools.Method(typeof(NCard), "UpdatePortrait");

	public static bool Prepare() => Target != null;

	public static System.Reflection.MethodBase TargetMethod() => Target!;

	public static void Postfix(NCard __instance) => ModEntry.Safe("card frame", () => CardFrameSkin.Apply(__instance));
}
