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
		// v0.14.91（实机日志：Apply 里空引用 ×3）：卡牌节点还没 _Ready 时原版就可能调 UpdateTitleLabel 等，这时 _frame 等字段还是空的；
		// 跳过即可，_Ready 后的 Reload 会再走一遍。
		if (card.Body == null || Frame(card) == null || Border(card) == null || Banner(card) == null || Plaque(card) == null || Portrait(card) == null || Title(card) == null)
		{
			return;
		}
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

	private static Font? _titleBase;
	private static readonly Dictionary<ulong, FontVariation> TitleFonts = new();

	/// <summary>
	/// 卡名用的字体（只加载一次）：
	/// 简体中文 = 霞鹜文楷 Bold（OFL；v0.14.88 用户：思源宋体太板正，从候选里选了文楷），只打包储君卡名用到的 212 个字（88KB，p3makoto/fonts/LXGWWenKai-Bold-CardTitles.ttf）；
	/// 繁体中文 = 游戏自带思源宋体 TC（文楷子集没有繁体字）；其他语言 = Cinzel。缺字都回退到原字体。
	/// </summary>
	private static Font TitleBase()
	{
		if (_titleBase == null)
		{
			string loc = TranslationServer.GetLocale().ToLowerInvariant();
			bool zh = loc.StartsWith("zh");
			bool zht = zh && (loc.Contains("tw") || loc.Contains("hk") || loc.Contains("hant") || loc.Contains("zht"));
			if (zh && !zht)
			{
				FontFile f = new();
				if (f.LoadDynamicFont("res://p3makoto/fonts/LXGWWenKai-Bold-CardTitles.ttf") == Error.Ok)
				{
					_titleBase = f;
				}
			}
			if (_titleBase == null && zht && ResourceLoader.Exists("res://fonts/zht/SourceHanSerifTC-Bold.otf"))
			{
				_titleBase = ResourceLoader.Load<Font>("res://fonts/zht/SourceHanSerifTC-Bold.otf");
			}
			_titleBase ??= Typo.Cinzel;
		}
		return _titleBase;
	}

	/// <summary>
	/// v0.14.88（用户：打开牌堆等有新卡面的地方会卡一下）：卡名字体全局共用一个 FontVariation（以前每张卡 new 一个，
	/// 每个新实例都要重新排版 / 栅格化字形，牌一多就卡）；按原字体（缺字回退用）各缓存一份。
	/// </summary>
	private static Font TitleFont(Font orig)
	{
		ulong key = orig.GetInstanceId();
		if (!TitleFonts.TryGetValue(key, out FontVariation? fv))
		{
			fv = new FontVariation { BaseFont = TitleBase(), SpacingGlyph = 1, Fallbacks = new Godot.Collections.Array<Font> { orig } };
			TitleFonts[key] = fv;
		}
		return fv;
	}

	/// <summary>储君卡名用到的全部汉字（localization/zhs/cards.json，含君王之剑和 "+"），预热时先栅格化。</summary>
	internal const string TitleChars = "+一七上下世中之事产人传伽位体何倒偏僭光共冲决冷凝击刀刃切划列创利券前剑割力助印去召向君品喜器国圣在地型墙大天太头好如子宇宙定导封射尘就崇序库座引彗形彩征御微态慨慷成战所打承抉护招拜择拳指捐授撞收改政敌斗新无既时明星映暗暴月服权架柱此武汇洞流淡淬溢漠火灭炉炸点炼熔爆独王环甚生界白盾矛破砸碰秘移穹空筹篷类粒纪绕群耀者胜脉自荟萃葬蓝藏虚视言谱质资超越踢踪轨轰辉辐迫迹追送造道量钻铸锋锤锻长间防阳际降陨隐集雨霸面项预马驾鸣黑！，";

	/// <summary>
	/// v0.14.88 空闲预热（IdleWarmup "@card frame"）：卡框 / 卡图贴图先加载并在极小的画布上画一次（上传显存），
	/// 卡名字体先加载、把所有卡名用到的字在常用字号下画一遍（栅格化进字形缓存），阿卡纳数字也画一遍。打开牌堆时就不用现做。
	/// </summary>
	/// <summary>预热要读的全部贴图路径（IdleWarmup 先在后台线程把它们读进缓存，v0.14.91）。</summary>
	public static List<string> WarmPaths()
	{
		var paths = new List<string> { Dir + "arcana_tab.png" };
		foreach (string kind in new[] { "attack", "skill", "power" })
		{
			paths.Add(Dir + $"card_frame_{kind}_s.png");
			foreach (string r in new[] { "common", "uncommon", "rare" })
			{
				paths.Add(Dir + $"card_portrait_border_{kind}_{r}.png");
			}
		}
		foreach (string r in new[] { "common", "uncommon", "rare" })
		{
			paths.Add(Dir + $"card_portrait_border_plaque_{r}.png");
			paths.Add(Dir + "card_banner_" + r + ".png");
		}
		foreach (string id in Arcana.Keys)
		{
			paths.Add($"res://p3makoto/cards/{id}.png");
		}
		return paths;
	}

	public static void Warm(Node2D tiny)
	{
		ulong t0 = Time.GetTicksUsec();
		List<string> paths = WarmPaths();
		foreach (string r in new[] { "common", "uncommon", "rare" })
		{
			BannerTex(r);
		}
		float x = 0f;
		foreach (string p in paths)
		{
			if (Tex(p) is Texture2D tex)
			{
				tiny.AddChild(new Sprite2D { Texture = tex, Position = new Vector2(x, 0f) });
				x += 8f;
			}
		}
		Font font = TitleBase();
		for (int size = 16; size <= 26; size += 2)
		{
			tiny.AddChild(new Label { Text = TitleChars, LabelSettings = new LabelSettings { Font = font, FontSize = size, OutlineSize = 6, ShadowSize = 6 }, Position = new Vector2(0f, size * 2f) });
		}
		tiny.AddChild(new Label { Text = "0 I II III IV V VI VII VIII IX X XI XII XIII XIV XV XVI XVII XVIII XIX XX XXI", LabelSettings = new LabelSettings { Font = Typo.Cinzel, FontSize = 17, OutlineSize = 4 } });
		GD.Print($"{ModEntry.Tag} perf card frame warm {(Time.GetTicksUsec() - t0) / 1000f:0.0}ms ({paths.Count} textures)");
	}

	/// <summary>
	/// 卡名（用户：原来的字体丑）：衬线白字 + 细深蓝描边（用户在三种写法里选的 C），不要原版的粗描边。
	/// 字体见 TitleBase（简中霞鹜文楷 / 繁中思源宋体 / 其他 Cinzel）；升级后的卡沿用原版"绿色"的约定，用浅青绿。
	/// NCard 复用给别的卡时（m = null）还原原版的字体、颜色、描边、阴影。
	/// </summary>
	private static void TitleStyle(NCard card, CardModel? m)
	{
		Label t = Title(card);
		Shade(t, false);   // v0.14.91（用户：卡名后面那块阴影看着很脏）：不再垫深色椭圆，只留文字描边；旧卡上已有的会被移除
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
			t.AddThemeFontOverride("font", TitleFont(orig));
			// 用户选 C：白字 + 深蓝描边 + 轻投影；v0.14.85（v0.14.84 用投影做的"柔光"边缘太硬）：字后面另放一块羽化的深色椭圆（见 Shade）
			t.AddThemeConstantOverride("outline_size", 6);
			t.AddThemeConstantOverride("shadow_outline_size", 6);
			t.AddThemeConstantOverride("shadow_offset_x", 0);
			t.AddThemeConstantOverride("shadow_offset_y", 2);
			t.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.35f));
		}
		// 升级后沿用原版"绿色卡名"的约定，换成和塔罗卡框搭的浅青绿
		t.AddThemeColorOverride("font_color", m.IsUpgraded ? new Color(0.62f, 1f, 0.78f) : Colors.White);
		t.AddThemeColorOverride("font_outline_color", new Color(0.03f, 0.07f, 0.14f));
	}

	private static GradientTexture2D? _shadeTex;

	/// <summary>
	/// 卡名后面一块羽化的深色椭圆（中心 70% 不透明的深海军蓝 → 边缘全透明），垫在横幅和字之间，让白字在银白 / 冰蓝 / 暗金横幅上都看得清。
	/// 放在卡名节点前面一个位置（同一个父节点），矩形和卡名一样居中、略小：(-95, -200) ~ (95, -152)。
	/// </summary>
	private static void Shade(Label title, bool on)
	{
		Node parent = title.GetParent();
		TextureRect? sh = parent.GetNodeOrNull<TextureRect>("P3TitleShade");
		if (sh == null)
		{
			if (!on)
			{
				return;
			}
			if (_shadeTex == null)
			{
				Color c = new(0.02f, 0.06f, 0.14f);
				Gradient g = new();
				g.SetColor(0, new Color(c, 0.7f));
				g.SetColor(1, new Color(c, 0f));
				g.AddPoint(0.55f, new Color(c, 0.32f));
				_shadeTex = new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1f, 0.5f), Width = 128, Height = 64 };
			}
			sh = new TextureRect { Name = "P3TitleShade", Texture = _shadeTex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore };
			sh.SetAnchorsPreset(Control.LayoutPreset.Center);
			sh.OffsetLeft = -95f;
			sh.OffsetTop = -200f;
			sh.OffsetRight = 95f;
			sh.OffsetBottom = -152f;
			parent.AddChild(sh);
			parent.MoveChild(sh, title.GetIndex());
		}
		sh.Visible = on;
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
