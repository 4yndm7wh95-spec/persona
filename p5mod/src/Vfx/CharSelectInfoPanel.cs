using System;
using System.Text.RegularExpressions;
using Godot;

namespace P5Joker;

/// <summary>
/// 选人界面左侧的介绍板（照结城理 mod 的 CharSelectInfo：同样的排版 / 坐标 / 入场 / 常驻，换成 P5R 状态界面的黑红白）：
/// · 抬头：黑色倾斜带（烘焙图 cs_info_head：上沿红条、下沿白细条、大号「0」+ ARCANA、勒索信剪贴字 JOKER、CODE NAME / REN AMAMIYA），
///   带子里一个深红色「愚者」慢慢漂移（cs_info_ghost），每隔几秒扫过一道光；
/// · 数字：Barlow Condensed 粗斜体，血量白 → 红、金币白 → 金渐变，同色粗下划线，中间白色斜杠；
/// · 描述（精简文案）+ 左侧红线；天赋：红边图标斜卡 + 名称 + 白色 PASSIVE 标签 + 说明（原版富文本）。中文用游戏自带的 Noto Sans CJK。
/// 入场在眼神特写碎开之后（CharSelectPatches 里按 CharSelectScene.InfoPanelDelay 调），抬头滑入 → 数字弹入并从 0 数上来 → 描述 → 天赋。
/// 坐标以原版信息面板的左上角为原点（同 P3：在真实截图上量的）。只用 Godot 类型，预览工具也能编译。
/// </summary>
public static class CharSelectInfoPanel
{
	public sealed record Data(string Hp, string Gold, string RelicTitle, string RelicDesc, Texture2D? RelicIcon);

	private const float Tilt = -5f * Mathf.Pi / 180f;           // 整组右端上扬 5°（同抬头）
	private static readonly Color White = new(0.98f, 0.97f, 0.97f);
	private static readonly Color Ink = new("0a0608");
	private static readonly Color Deep = new(0.08f, 0.01f, 0.03f);
	private static readonly Color Red = new("de101e");
	private static readonly Color Pale = new(1f, 0.78f, 0.8f);
	private static readonly Color Gold = new(1f, 0.8f, 0.2f);
	private static readonly Color Grey = new(0.84f, 0.82f, 0.83f);

	private static Shader? _sheen;

	/// <summary>抬头扫光：一道斜的亮带沿贴图横向扫过，只加在不透明像素上（透明度不变）。sweep &lt; -0.5 时不显示。</summary>
	private static Shader SheenShader => _sheen ??= new Shader { Code = @"
shader_type canvas_item;
uniform float sweep = -1.0;
void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float x = UV.x + (UV.y - 0.5) * 0.35;
	float b = sweep < -0.5 ? 0.0 : exp(-pow((x - sweep) / 0.05, 2.0));
	c.rgb += vec3(1.0, 0.94, 0.94) * b * 0.32 * c.a;
	COLOR = c;
}" };

	/// <summary>精简描述：日文界面用日文，其余都用中文（用户 2026-11-17：这段要中文）。</summary>
	public static string ShortDesc() => TranslationServer.GetLocale().StartsWith("ja", StringComparison.Ordinal)
		? "心の怪盗団のリーダー。\n反逆の魂で、歪んだ心を奪う。"
		: "心之怪盗团的首领。\n以叛逆之魂，夺取扭曲的心。";

	private static Label Text(Node parent, string text, Font font, int size, Color top, Color bottom, Vector2 pos, int outline = 0)
	{
		Label l = P5Ui.Text(parent, text, font, size, Colors.White, pos, outline, Deep);
		l.Material = P5Ui.Gradient(top, bottom, size * 1.35f);
		return l;
	}

	private static float Seg(float t, float start, float dur) => Mathf.Clamp((t - start) / dur, 0f, 1f);

	/// <summary>在 parent（原版信息面板，或预览里的假面板）下面搭介绍板并播入场。返回整组节点（拆掉它就恢复）。</summary>
	public static Node2D Build(Node parent, Data d)
	{
		Node2D deco = new() { Name = "P5InfoDeco" };
		parent.AddChild(deco);
		Font num = P5Ui.Font(P5Ui.Barlow), cjkB = P5Ui.Font(P5Ui.CjkBold), cjkM = P5Ui.Font(P5Ui.CjkMedium);

		// ── 抬头：黑色倾斜带（烘焙图，已转好角度、已抗锯齿，1:1 贴）+「愚者」（慢慢漂移）+ 扫光 ──
		Node2D head = new() { Position = new Vector2(252f, -28f) };
		deco.AddChild(head);
		ShaderMaterial sheen = new() { Shader = SheenShader };
		if (Fx.Tex("cs_info_head") is Texture2D headTex)
		{
			head.AddChild(new Sprite2D { Texture = headTex, Material = sheen });
		}
		Sprite2D? ghost = null;
		if (Fx.Tex("cs_info_ghost") is Texture2D ghostTex)
		{
			ghost = new Sprite2D { Texture = ghostTex };
			head.AddChild(ghost);
		}

		// ── 数字：HP（白 → 红）/ 金币（白 → 金），同色粗下划线，中间白色斜杠 ──
		Node2D stats = new() { Position = new Vector2(202f, 150f), Rotation = Tilt };
		deco.AddChild(stats);
		const int big = 72, small = 24;
		Text(stats, "HP", num, small, Pale, Red, new Vector2(-350f, -12f));
		Label hpLabel = Text(stats, d.Hp, num, big, White, new Color(1f, 0.6f, 0.62f), new Vector2(-308f, -60f), 4);
		float hpW = P5Ui.Width(num, d.Hp, big);
		Node2D hpLine = new() { Position = new Vector2(-356f, 0f) };                // 下划线从左端"画出来"：缩放中心放在左端
		stats.AddChild(hpLine);
		P5Ui.Slab(hpLine, 0f, 60f + hpW, 34f, 43f, 4f, Red);
		P5Ui.Quad(stats, new[] { new Vector2(-2.5f, -44f), new Vector2(2.5f, -44f), new Vector2(-17.5f, 42f), new Vector2(-22.5f, 42f) }, new Color(White, 0.9f));
		Text(stats, "G", num, small, new Color(1f, 0.94f, 0.7f), Gold, new Vector2(20f, -12f));
		Label goldLabel = Text(stats, d.Gold, num, big, new Color(1f, 0.99f, 0.92f), Gold, new Vector2(50f, -60f), 4);
		float goldW = P5Ui.Width(num, d.Gold, big);
		Node2D goldLine = new() { Position = new Vector2(14f, 0f) };
		stats.AddChild(goldLine);
		P5Ui.Slab(goldLine, 0f, 50f + goldW, 34f, 43f, 4f, Gold);

		// ── 描述：左侧红线 + 两行（白 → 浅灰） ──
		Node2D desc = new() { Position = new Vector2(-148f, 238f) };
		deco.AddChild(desc);
		ColorRect bar = P5Ui.Slab(desc, 0f, 5f, 2f, 70f, 0f, Red);
		Label descLabel = Text(desc, ShortDesc(), cjkM, 27, White, Grey, new Vector2(22f, -6f));
		descLabel.LabelSettings!.LineSpacing = 2f;

		// ── 天赋：红边图标斜卡 + 名称 + PASSIVE + 说明（说明是原版富文本，带图标） ──
		Node2D relic = new() { Position = new Vector2(-144f, 324f) };
		deco.AddChild(relic);
		P5Ui.Slab(relic, 0f, 66f, -2f, 62f, 10f, Red);
		P5Ui.Slab(relic, 4f, 62f, 2f, 58f, 10f, Ink);
		if (d.RelicIcon != null)
		{
			relic.AddChild(new Sprite2D { Texture = d.RelicIcon, Position = new Vector2(35f, 30f), Scale = Vector2.One * (52f / Mathf.Max(d.RelicIcon.GetWidth(), 1)) });
		}
		Text(relic, d.RelicTitle, cjkB, 26, White, Pale, new Vector2(94f, -6f));
		float tx0 = 94f + P5Ui.Width(cjkB, d.RelicTitle, 26) + 18f;
		Node2D tag = new() { Position = new Vector2(tx0 + 48f, 18f) };
		relic.AddChild(tag);
		P5Ui.Slab(tag, -48f, 44f, -11f, 11f, 5f, White);
		Label passive = P5Ui.Text(tag, "PASSIVE", num, 19, Ink, Vector2.Zero);
		passive.Position = new Vector2(-P5Ui.Width(num, "PASSIVE", 19) * 0.5f + 2.5f, -19f * 0.68f);
		RichTextLabel rd = new()
		{
			BbcodeEnabled = true, Text = d.RelicDesc, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.Off,
			MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(94f, 30f), Size = new Vector2(620f, 30f),
		};
		rd.AddThemeFontOverride("normal_font", cjkM);
		rd.AddThemeFontSizeOverride("normal_font_size", 21);
		rd.AddThemeColorOverride("default_color", Colors.White);
		rd.Material = P5Ui.Gradient(White, Grey, 21 * 1.4f);
		relic.AddChild(rd);

		// ── 入场（同 P3 的节奏） ──
		int.TryParse(Regex.Match(d.Gold, @"\d+").Value, out int goldVal);
		Vector2 headAt = head.Position, statsAt = stats.Position, descAt = desc.Position, relicAt = relic.Position;
		foreach (CanvasItem c in new CanvasItem[] { head, stats, desc, relic })
		{
			c.Modulate = new Color(1f, 1f, 1f, 0f);
		}
		const float intro = 0.9f;
		Fx.Animate(deco, 0, intro, t =>
		{
			float time = t * intro;
			float a = Ease5.OutExpo(Seg(time, 0f, 0.36f));
			head.Position = headAt + new Vector2(-320f * (1f - a), 0f);
			head.Modulate = new Color(1f, 1f, 1f, Seg(time, 0f, 0.12f));
			float b = Ease5.OutBack(Seg(time, 0.1f, 0.32f));
			stats.Position = statsAt + new Vector2(-140f * (1f - b), 0f);
			stats.Modulate = new Color(1f, 1f, 1f, Seg(time, 0.1f, 0.12f));
			float n = Ease5.OutQuint(Seg(time, 0.12f, 0.45f));                     // 数字从 0 数上来
			hpLabel.Text = Regex.Replace(d.Hp, @"\d+", m => ((int)Mathf.Round(int.Parse(m.Value) * n)).ToString());
			goldLabel.Text = ((int)Mathf.Round(goldVal * n)).ToString();
			hpLine.Scale = new Vector2(Ease5.OutExpo(Seg(time, 0.16f, 0.3f)), 1f);
			goldLine.Scale = hpLine.Scale;
			float c = Ease5.OutQuint(Seg(time, 0.22f, 0.32f));
			desc.Position = descAt + new Vector2(0f, 14f * (1f - c));
			desc.Modulate = new Color(1f, 1f, 1f, c);
			bar.Scale = new Vector2(1f, Ease5.OutExpo(Seg(time, 0.22f, 0.3f)));
			float r = Ease5.OutQuint(Seg(time, 0.3f, 0.34f));
			relic.Position = relicAt + new Vector2(-90f * (1f - r), 0f);
			relic.Modulate = new Color(1f, 1f, 1f, r);
			tag.Scale = Vector2.One * Mathf.Lerp(1.6f, 1f, Ease5.OutBack(Seg(time, 0.48f, 0.22f)));
			tag.Modulate = new Color(1f, 1f, 1f, Seg(time, 0.48f, 0.08f));
		});

		// ── 常驻（入场之后）：扫光、「愚者」漂移、整组极轻微浮动、下划线呼吸 ──
		Fx.After(deco, intro, () =>
		{
			if (!GodotObject.IsInstanceValid(hpLabel) || !GodotObject.IsInstanceValid(goldLabel))
			{
				return;
			}
			hpLabel.Text = d.Hp;
			goldLabel.Text = d.Gold;
			Fx.Animate(deco, 0, 3600.0, t =>
			{
				float time = t * 3600f;
				float s = (time % 3.6f) / 0.7f;                                    // 每 3.6 秒扫一次光，扫 0.7 秒
				sheen.SetShaderParameter("sweep", s < 1f ? -0.2f + 1.4f * (s * s * (3f - 2f * s)) : -1f);
				if (ghost != null)
				{
					ghost.Position = new Vector2(Mathf.Cos(Tilt), Mathf.Sin(Tilt)) * (12f * Mathf.Sin(time * 0.4f));   // 沿带子方向漂移
				}
				head.Position = headAt + new Vector2(0f, 1.6f * Mathf.Sin(time * 1.1f));
				stats.Position = statsAt + new Vector2(0f, 1.6f * Mathf.Sin(time * 1.1f - 0.7f));
				hpLine.Modulate = new Color(1f, 1f, 1f, 0.78f + 0.22f * (0.5f + 0.5f * Mathf.Sin(time * 2.2f)));
				goldLine.Modulate = new Color(1f, 1f, 1f, 0.78f + 0.22f * (0.5f + 0.5f * Mathf.Sin(time * 2.2f - 1.2f)));
			});
		});
		return deco;
	}
}
