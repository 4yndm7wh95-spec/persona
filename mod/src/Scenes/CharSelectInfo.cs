using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// 选人界面左侧的角色介绍板：选中结城理时换成 P3R 人格面具状态界面风格（效果图方向 A），选别的角色时全部恢复原样。只改显示。
/// 原版面板整块隐藏，内容由我们重新排（数值 / 天赋名称 / 天赋说明 / 图标都实时读原版的标签和数据）：
/// · 抬头：倾斜白带（烘焙图 tools/ui/build_info_panel.py：大号"0"、ARCANA、"結城 理"、YUKI MAKOTO），裁在白带里的浅灰"愚者"；
/// · 数字：Barlow Condensed 粗斜体（mod 自带，SIL OFL），血量白 → 青、金币白 → 黄渐变，同色粗下划线，中间红色斜杠；
/// · 描述（精简文案）+ 左侧青线；天赋：青边图标斜卡 + 名称 + 黄色 PASSIVE 标签 + 说明。中文用游戏自带的 Noto Sans CJK。
/// 动画：等选中时的召唤切入（眼神横幅碎裂）结束再入场 —— 白带滑入 → 数字弹入并从 0 数上来 → 描述 → 天赋；
/// 之后常驻：白带每隔几秒扫过一道光、"愚者"缓慢漂移、整组极轻微浮动、下划线轻轻呼吸。
/// 坐标按 1920×1080 下原版面板左上角为原点（效果图量出来的）。
/// </summary>
public static class CharSelectInfo
{
	private const string DecoName = "P3InfoDeco";
	private const string HeadPath = "res://p3makoto/ui/info_head.png";
	private const string GhostPath = "res://p3makoto/ui/info_head_ghost.png";
	private const string BarlowPath = "res://p3makoto/fonts/BarlowCondensed-900Italic.ttf";
	private const string CjkBoldPath = "res://fonts/jpn/NotoSansCJKjp-Bold.otf";
	private const string CjkMediumPath = "res://fonts/jpn/NotoSansCJKjp-Medium.otf";
	private const float Tilt = -5f * Mathf.Pi / 180f;           // 整组右端上扬 5°（和选人背景里的大斜条同方向）

	private static readonly Color White = new(0.97f, 0.98f, 1f);
	private static readonly Color Ink = new(0.03f, 0.04f, 0.1f);
	private static readonly Color Navy = new(0.024f, 0.063f, 0.26f);
	private static readonly Color Deep = new(0.012f, 0.03f, 0.14f);
	private static readonly Color Ice = new(0.69f, 0.84f, 1f);
	private static readonly Color Yellow = new(1f, 0.82f, 0.16f);
	private static readonly Color Red = new(0.91f, 0.12f, 0.23f);

	private sealed class Saved
	{
		public readonly List<(CanvasItem Item, Color Color, bool Self)> Hidden = new();
	}

	private static readonly Dictionary<ulong, Saved> State = new();
	private static Font? _barlow;

	private static Font? Barlow
	{
		get
		{
			if (_barlow == null)
			{
				FontFile f = new();
				if (f.LoadDynamicFont(BarlowPath) == Error.Ok)
				{
					_barlow = f;
				}
			}
			return _barlow ?? Typo.Latin;
		}
	}

	private static Font CjkBold => ResourceLoader.Load(CjkBoldPath) as Font ?? Typo.Cjk;
	private static Font CjkMedium => ResourceLoader.Load(CjkMediumPath) as Font ?? Typo.Cjk;

	public static void Apply(NCharacterSelectScreen screen, NCharacterSelectButton button, CharacterModel model)
	{
		Control? panel = Traverse.Create(screen).Field("_infoPanel").GetValue<Control>();
		if (panel == null || !GodotObject.IsInstanceValid(panel))
		{
			return;
		}
		Restore(panel);
		if (button.IsRandom || button.IsLocked || model.GetType().Name != "Regent" || !Core.Makoto.AssetsOurs)
		{
			return;
		}
		Saved saved = new();
		State[panel.GetInstanceId()] = saved;

		// 原版面板整块隐藏：底板（面板自身 + 非容器子节点）用 SelfModulate，内容容器用 Modulate（连同所有子节点）
		saved.Hidden.Add((panel, panel.SelfModulate, true));
		panel.SelfModulate = new Color(1f, 1f, 1f, 0f);
		foreach (Node child in panel.GetChildren())
		{
			if (child is not CanvasItem ci || child.Name == DecoName)
			{
				continue;
			}
			if (child is Container)
			{
				saved.Hidden.Add((ci, ci.Modulate, false));
				ci.Modulate = new Color(1f, 1f, 1f, 0f);
			}
			else
			{
				saved.Hidden.Add((ci, ci.SelfModulate, true));
				ci.SelfModulate = new Color(1f, 1f, 1f, 0f);
			}
		}

		// 内容：读原版的标签 / 数据
		string hp = Traverse.Create(screen).Field("_hp").GetValue<Label>()?.Text ?? "";
		string gold = Traverse.Create(screen).Field("_gold").GetValue<Label>()?.Text ?? "";
		string relicTitle = Traverse.Create(screen).Field("_relicTitle").GetValue<RichTextLabel>()?.Text ?? "";
		string relicDesc = Traverse.Create(screen).Field("_relicDescription").GetValue<RichTextLabel>()?.Text ?? "";
		Texture2D? relicIcon = Traverse.Create(screen).Field("_relicIcon").GetValue<TextureRect>()?.Texture;

		Node2D deco = new() { Name = DecoName, Visible = false };
		panel.AddChild(deco);
		// 等选中时的召唤切入（眼神横幅 0.7s 后碎裂）结束再入场，两者不同时出现
		// v0.14.45：搭新面板出错时恢复原版面板（不能留下一块空白）
		Fx.After(deco, CutInFx.Enabled ? 0.85 : 0.1, "info panel", () =>
		{
			// 审查：快速切走再切回结城理时，上一次的计时器仍会触发 —— 它的面板已经不是当前这块了，什么都不做（否则会把新面板拆掉）
			if (!GodotObject.IsInstanceValid(deco) || deco.GetParent() != panel || deco.IsQueuedForDeletion())
			{
				return;
			}
			try
			{
				Perf.Time("info panel build", () => Build(deco, hp, gold, Regex.Replace(relicTitle, @"\[[^\]]*\]", ""), relicDesc, relicIcon));
			}
			catch (System.Exception)
			{
				if (GodotObject.IsInstanceValid(panel))
				{
					Restore(panel);
				}
				throw;
			}
		});
	}

	private static string ShortDesc()
	{
		string loc = TranslationServer.GetLocale();
		if (loc.StartsWith("zh"))
		{
			return "影时间中觉醒的少年。\n以羁绊为力，唤醒人格面具。";
		}
		if (loc.StartsWith("ja"))
		{
			return "影時間に目覚めた少年。\n絆を力に、ペルソナを呼び覚ます。";
		}
		return "A silent youth awakened in the Dark Hour.\nHis bonds call forth his Persona.";
	}

	private static void Restore(Control panel)
	{
		if (panel.GetNodeOrNull(DecoName) is Node old)
		{
			panel.RemoveChild(old);
			old.QueueFree();
		}
		if (!State.Remove(panel.GetInstanceId(), out Saved? saved))
		{
			return;
		}
		foreach ((CanvasItem item, Color c, bool self) in saved.Hidden)
		{
			if (!GodotObject.IsInstanceValid(item)) continue;
			if (self) item.SelfModulate = c;
			else item.Modulate = c;
		}
	}

	/// <summary>文字标签（字体 / 字号 / 白字 + 渐变材质 + 可选描边）。</summary>
	private static Label Text(Node parent, string text, Font font, int size, Color top, Color bottom, Vector2 pos, int outline = 0, Color? outlineColor = null)
	{
		Label l = Typo.Text(parent, text, font, size, Colors.White, pos, outline: outline, outlineColor: outlineColor ?? Deep);
		l.Material = Typo.Gradient(top, bottom, size * 1.35f);
		return l;
	}

	private static float TextWidth(Font font, string text, int size) => font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;

	private static void Build(Node2D deco, string hp, string gold, string relicTitle, string relicDesc, Texture2D? relicIcon)
	{
		if (!GodotObject.IsInstanceValid(deco))
		{
			return;
		}
		deco.Visible = true;
		Font num = Barlow!;

		// ── 抬头：倾斜白带（烘焙图，已转好角度、已抗锯齿，1:1 贴，不旋转不缩放）+ "愚者"（常驻缓慢漂移）+ 扫光（着色器，只亮在白带像素上） ──
		Node2D head = new() { Position = new Vector2(252f, -28f) };
		deco.AddChild(head);
		ShaderMaterial? sheen = null;
		if (ResourceLoader.Load(HeadPath) is Texture2D headTex)
		{
			sheen = new ShaderMaterial { Shader = SheenShader };
			head.AddChild(new Sprite2D { Texture = headTex, Material = sheen });
		}
		Sprite2D? ghost = null;
		if (ResourceLoader.Load(GhostPath) is Texture2D ghostTex)
		{
			ghost = new Sprite2D { Texture = ghostTex };
			head.AddChild(ghost);
		}

		// ── 数字：HP（白 → 青）/ 金币（白 → 黄），同色粗下划线，中间红色斜杠 ──
		Node2D stats = new() { Position = new Vector2(202f, 150f), Rotation = Tilt };
		deco.AddChild(stats);
		const int big = 72, small = 24;
		Text(stats, "HP", num, small, new Color(0.67f, 0.94f, 1f), Pal.Cyan, new Vector2(-350f, -12f));
		Label hpLabel = Text(stats, hp, num, big, White, Pal.Cyan, new Vector2(-308f, -60f), 4);
		float hpW = TextWidth(num, hp, big);
		Node2D hpLine = new() { Position = new Vector2(-356f, 0f) };                // 下划线从左端"画出来"：缩放中心放在左端
		stats.AddChild(hpLine);
		P3Ui.Slab(hpLine, 0f, 60f + hpW, 34f, 43f, 4f, Pal.Cyan);
		Fx.Stroke(stats, new[] { new Vector2(0f, -44f), new Vector2(-20f, 42f) }, Red, 5f, additive: false);
		Text(stats, "G", num, small, new Color(1f, 0.94f, 0.67f), Yellow, new Vector2(20f, -12f));
		Label goldLabel = Text(stats, gold, num, big, new Color(1f, 0.99f, 0.9f), Yellow, new Vector2(50f, -60f), 4);
		float goldW = TextWidth(num, gold, big);
		Node2D goldLine = new() { Position = new Vector2(14f, 0f) };
		stats.AddChild(goldLine);
		P3Ui.Slab(goldLine, 0f, 50f + goldW, 34f, 43f, 4f, Yellow);

		// ── 描述：左侧青线 + 两行（白 → 冰蓝） ──
		Node2D desc = new() { Position = new Vector2(-148f, 238f) };
		deco.AddChild(desc);
		Polygon2D bar = P3Ui.Slab(desc, 0f, 5f, 2f, 70f, 0f, Pal.Cyan);
		Label descLabel = Text(desc, ShortDesc(), CjkMedium, 27, White, Ice, new Vector2(22f, -6f));
		descLabel.LabelSettings!.LineSpacing = 2f;
		descLabel.Material = Typo.Gradient(White, Ice, 27 * 1.35f + 2f);

		// ── 天赋：青边图标斜卡 + 名称 + PASSIVE + 说明（说明带原版的星星图标，用富文本） ──
		Node2D relic = new() { Position = new Vector2(-144f, 324f) };
		deco.AddChild(relic);
		P3Ui.Slab(relic, 0f, 66f, -2f, 62f, 10f, Pal.Cyan);
		P3Ui.Slab(relic, 4f, 62f, 2f, 58f, 10f, Navy);
		if (relicIcon != null)
		{
			relic.AddChild(new Sprite2D { Texture = relicIcon, Position = new Vector2(35f, 30f), Scale = Vector2.One * (52f / Mathf.Max(relicIcon.GetWidth(), 1)) });
		}
		Font cjkB = CjkBold;
		Text(relic, relicTitle, cjkB, 26, new Color(0.88f, 0.98f, 1f), Pal.Cyan, new Vector2(94f, -6f));
		float tx0 = 94f + TextWidth(cjkB, relicTitle, 26) + 18f;
		Node2D tag = new() { Position = new Vector2(tx0 + 48f, 18f) };
		relic.AddChild(tag);
		P3Ui.Slab(tag, -48f, 44f, -11f, 11f, 5f, Yellow);
		Label passive = Typo.Text(tag, "PASSIVE", num, 19, Ink, Vector2.Zero);
		float pw = TextWidth(num, "PASSIVE", 19);
		passive.Position = new Vector2(-pw * 0.5f + 2.5f, -19f * 0.68f);              // 在黄色斜标签里居中
		RichTextLabel rd = new()
		{
			BbcodeEnabled = true, Text = relicDesc, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.Off,
			MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(94f, 30f), Size = new Vector2(620f, 30f)
		};
		rd.AddThemeFontOverride("normal_font", CjkMedium);
		rd.AddThemeFontSizeOverride("normal_font_size", 21);
		rd.AddThemeColorOverride("default_color", Colors.White);
		rd.Material = Typo.Gradient(White, Ice, 21 * 1.4f);
		relic.AddChild(rd);

		// ── 入场 ──
		int.TryParse(Regex.Match(hp, @"\d+").Value, out int hpCur);
		int.TryParse(Regex.Match(gold, @"\d+").Value, out int goldVal);
		Vector2 headAt = head.Position, statsAt = stats.Position, descAt = desc.Position, relicAt = relic.Position;
		foreach (CanvasItem c in new CanvasItem[] { head, stats, desc, relic })
		{
			c.Modulate = new Color(1f, 1f, 1f, 0f);
		}
		const float intro = 0.9f;
		Fx.Animate(deco, intro, t =>
		{
			float time = t * intro;
			float a = Ease.OutExpo(Seg(time, 0f, 0.36f));
			head.Position = headAt + new Vector2(-320f * (1f - a), 0f);
			head.Modulate = new Color(1f, 1f, 1f, Seg(time, 0f, 0.12f));
			float b = Ease.OutBack(Seg(time, 0.1f, 0.32f));
			stats.Position = statsAt + new Vector2(-140f * (1f - b), 0f);
			stats.Modulate = new Color(1f, 1f, 1f, Seg(time, 0.1f, 0.12f));
			float n = Ease.OutQuint(Seg(time, 0.12f, 0.45f));                     // 数字从 0 数上来
			hpLabel.Text = Regex.Replace(hp, @"\d+", m => ((int)Mathf.Round(int.Parse(m.Value) * n)).ToString());
			goldLabel.Text = ((int)Mathf.Round(goldVal * n)).ToString();
			hpLine.Scale = new Vector2(Ease.OutExpo(Seg(time, 0.16f, 0.3f)), 1f);
			goldLine.Scale = hpLine.Scale;
			float c = Ease.OutQuint(Seg(time, 0.22f, 0.32f));
			desc.Position = descAt + new Vector2(0f, 14f * (1f - c));
			desc.Modulate = new Color(1f, 1f, 1f, c);
			bar.Scale = new Vector2(1f, Ease.OutExpo(Seg(time, 0.22f, 0.3f)));
			float r = Ease.OutQuint(Seg(time, 0.3f, 0.34f));
			relic.Position = relicAt + new Vector2(-90f * (1f - r), 0f);
			relic.Modulate = new Color(1f, 1f, 1f, r);
			tag.Scale = Vector2.One * Mathf.Lerp(1.6f, 1f, Ease.OutBack(Seg(time, 0.48f, 0.22f)));
			tag.Modulate = new Color(1f, 1f, 1f, Seg(time, 0.48f, 0.08f));
		});

		// ── 常驻（入场之后）：扫光、"愚者"漂移、整组极轻微浮动、下划线呼吸 ──
		Fx.After(deco, intro, "info panel idle", () =>
		{
			// 选人界面已经切走时标签可能已被释放（日志：info panel idle failed: ObjectDisposedException）
			if (!GodotObject.IsInstanceValid(hpLabel) || !GodotObject.IsInstanceValid(goldLabel))
			{
				return;
			}
			hpLabel.Text = hp;
			goldLabel.Text = gold;
			Fx.Animate(deco, 3600.0, t =>
			{
				float time = t * 3600f;
				float s = (time % 3.6f) / 0.7f;                                    // 每 3.6 秒扫一次光，扫 0.7 秒
				sheen?.SetShaderParameter("sweep", s < 1f ? -0.2f + 1.4f * Ease.InOutCubic(s) : -1f);
				if (ghost != null)
				{
					ghost.Position = new Vector2(Mathf.Cos(Tilt), Mathf.Sin(Tilt)) * (12f * Mathf.Sin(time * 0.4f));   // 沿白带方向漂移
				}
				head.Position = headAt + new Vector2(0f, 1.6f * Mathf.Sin(time * 1.1f));
				stats.Position = statsAt + new Vector2(0f, 1.6f * Mathf.Sin(time * 1.1f - 0.7f));
				float pulse = 0.78f + 0.22f * (0.5f + 0.5f * Mathf.Sin(time * 2.2f));
				hpLine.Modulate = new Color(1f, 1f, 1f, pulse);
				goldLine.Modulate = new Color(1f, 1f, 1f, 0.78f + 0.22f * (0.5f + 0.5f * Mathf.Sin(time * 2.2f - 1.2f)));
			});
		});
	}

	/// <summary>
	/// 预热（mod 启动后由 Preload 调用，画在几乎透明的角落节点里）：读 Barlow 字体、编译扫光着色器、
	/// 把介绍板用到的数字 / 描述文字按实际字号先栅格化一遍 —— 第一次选中结城理时就不用同步读盘、编译、逐字栅格化。
	/// </summary>
	internal static void Warm(Node2D parent)
	{
		if (Barlow is Font num)
		{
			foreach (int size in new[] { 19, 24, 72 })
			{
				Typo.Text(parent, "HP G PASSIVE 0123456789/", num, size, Colors.White, Vector2.Zero);
			}
		}
		Typo.Text(parent, ShortDesc(), CjkMedium, 27, Colors.White, Vector2.Zero);
		Typo.Text(parent, ShortDesc(), CjkBold, 26, Colors.White, Vector2.Zero);
		if (ResourceLoader.Load(HeadPath) is Texture2D headTex)
		{
			parent.AddChild(new Sprite2D { Texture = headTex, Material = new ShaderMaterial { Shader = SheenShader }, Scale = Vector2.One * 0.01f });
		}
	}

	private static Shader? _sheen;

	/// <summary>白带扫光：一道斜的亮带沿贴图横向扫过，只加在不透明像素上（透明度不变，边缘不会出锯齿）。sweep &lt; -0.5 时不显示。</summary>
	private static Shader SheenShader => _sheen ??= new Shader
	{
		Code = @"shader_type canvas_item;
uniform float sweep = -1.0;
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	float d = abs((UV.x + (1.0 - UV.y) * 0.18) - sweep);
	float k = smoothstep(0.045, 0.0, d) * step(-0.5, sweep);
	c.rgb += vec3(0.55, 0.85, 1.0) * k * 0.45 * c.a;
	COLOR = c * COLOR;
}"
	};

	private static float Seg(float time, float start, float len) => Mathf.Clamp((time - start) / len, 0f, 1f);
}
