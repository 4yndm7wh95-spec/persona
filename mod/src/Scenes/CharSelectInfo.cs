using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// 选人界面左侧的角色介绍板（所有角色共用的 InfoPanel）：选中结城理时换成 P3R 菜单风格，选别的角色时全部恢复原样。只改外观。
/// 视觉语言照 P3R 菜单截图（人格面具 / 装备 / 社群界面）：
/// · 一整块斜切板子：上面白色抬头、下面深蓝正文，左右边缘和斜切角度完全一致（选人背景本身很满，这块要"安静"）；抬头上沿错开露出一条青边。
/// · 名字：烘焙好的字样（tools/ui/build_plates.nameplate）—— 深蓝渐变"結城 理" + 青色错位残影 + 浅灰大号"愚者"；原版名字标签隐藏但保留排版位置。
/// · 天赋图标垫一张青边小斜卡。
/// · 字不用纯色：逐行竖向渐变（Typo.Gradient）—— 正文白 → 冰蓝，天赋名冰白 → 青，血量白 → 青、金币白 → 黄（+ 同色下划线、红色斜杠）。
/// · 描述文案换成精简版（只在显示上替换，切走时还原原文）。
/// </summary>
public static class CharSelectInfo
{
	private const string DecoName = "P3InfoDeco";
	private const string PlatePath = "res://p3makoto/ui/nameplate.png";
	private const string HpPath = "VBoxContainer/HpGoldSpacer/HpGold/Hp/Label";
	private const string GoldPath = "VBoxContainer/HpGoldSpacer/HpGold/Gold/Label";
	private const string DescPath = "VBoxContainer/DescriptionLabel";
	private const string RelicNamePath = "VBoxContainer/Relic/Name/RichTextLabel";
	private const string RelicDescPath = "VBoxContainer/Relic/Description";
	// 名字字样贴图（build_plates.nameplate，724×155，透明底）：名字字形左下角、名字字高
	private static readonly Vector2 PlateAnchor = new(20f, 135f);
	private const float PlateNameH = 102.5f;

	private static readonly Color Yellow = new(1f, 0.82f, 0.12f);
	private static readonly Color Red = new(0.91f, 0.14f, 0.23f);
	private static readonly Color Ice = new(0.66f, 0.84f, 1f);
	private static readonly Color Navy = new(0.016f, 0.04f, 0.18f, 0.95f);

	private sealed class Saved
	{
		public readonly List<(CanvasItem Item, Color SelfModulate)> Hidden = new();
		public readonly List<System.Action> Undo = new();
	}

	private static readonly Dictionary<ulong, Saved> State = new();

	public static void Apply(NCharacterSelectScreen screen, NCharacterSelectButton button, CharacterModel model)
	{
		Control? panel = Traverse.Create(screen).Field("_infoPanel").GetValue<Control>();
		Label? name = Traverse.Create(screen).Field("_name").GetValue<Label>();
		if (panel == null || !GodotObject.IsInstanceValid(panel))
		{
			return;
		}
		Restore(panel);
		if (button.IsRandom || button.IsLocked || model.GetType().Name != "Regent")
		{
			return;
		}
		Saved saved = new();
		State[panel.GetInstanceId()] = saved;

		// 原版底板：面板自身的绘制 + 面板下除内容容器以外的直接子节点（只是底图 / 装饰）
		Hide(saved, panel);
		foreach (Node child in panel.GetChildren())
		{
			if (child is CanvasItem ci && child is not Container && child.Name != DecoName)
			{
				Hide(saved, ci);
			}
		}
		// 名字：原版标签隐藏（保留排版位置），换成名字色带贴图
		if (name != null)
		{
			Hide(saved, name);
		}
		// 字色先统一设成白（渐变由材质上色，见 Build），去掉原版投影
		foreach (string path in new[] { DescPath, RelicNamePath, RelicDescPath })
		{
			if (panel.GetNodeOrNull<Control>(path) is Control c)
			{
				saved.Undo.Add(Typo.Theme(c, colors: new[] { ("default_color", Colors.White), ("font_shadow_color", new Color(0f, 0f, 0f, 0f)) }));
			}
		}
		foreach (string path in new[] { HpPath, GoldPath })
		{
			if (panel.GetNodeOrNull<Label>(path) is Label l)
			{
				saved.Undo.Add(Typo.Restyle(l, Typo.LatinItalic, Colors.White, new Color(0.02f, 0.06f, 0.3f), 4));
			}
		}
		// 各行之间拉开（P3R 留白多）：内容容器的行距加大，名字行下方再多空一截给色带
		if (panel.GetNodeOrNull<Control>("VBoxContainer") is Control vbox)
		{
			int sep = vbox.GetThemeConstant("separation");
			saved.Undo.Add(Typo.Theme(vbox, consts: new[] { ("separation", sep + 14) }));
		}
		if (name != null)
		{
			float minY = name.CustomMinimumSize.Y;
			name.CustomMinimumSize = new Vector2(name.CustomMinimumSize.X, Mathf.Max(minY, name.Size.Y + 22f));
			saved.Undo.Add(() => { if (GodotObject.IsInstanceValid(name)) name.CustomMinimumSize = new Vector2(name.CustomMinimumSize.X, minY); });
		}
		// 精简文案
		if (panel.GetNodeOrNull<RichTextLabel>(DescPath) is RichTextLabel desc)
		{
			string orig = desc.Text;
			desc.Text = ShortDesc();
			saved.Undo.Add(() => { if (GodotObject.IsInstanceValid(desc)) desc.Text = orig; });
		}

		Node2D deco = new() { Name = DecoName };                     // Node2D：面板若是容器也不会被它排版、不挤动原版内容
		panel.AddChild(deco);
		panel.MoveChild(deco, 0);                                   // 最先画：内容都在它上面
		// 文字刚换过，容器要到下一帧才重新排版：等两帧再按实际位置画
		Fx.After(deco, 0.034, "info panel deco", () => Build(deco, panel.Size, saved, name));
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

	private static void Hide(Saved saved, CanvasItem item)
	{
		saved.Hidden.Add((item, item.SelfModulate));
		item.SelfModulate = new Color(1f, 1f, 1f, 0f);
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
		foreach ((CanvasItem item, Color c) in saved.Hidden)
		{
			if (GodotObject.IsInstanceValid(item))
			{
				item.SelfModulate = c;
			}
		}
		for (int i = saved.Undo.Count - 1; i >= 0; i--)
		{
			saved.Undo[i]();
		}
	}

	/// <summary>节点相对面板的矩形（面板在滑入动画中也成立：两者一起移动）。</summary>
	private static Rect2? RectIn(Control panel, string path)
	{
		if (panel.GetNodeOrNull<Control>(path) is not Control c || !c.IsVisibleInTree() || c.Size.X < 1f)
		{
			return null;
		}
		return new Rect2(c.GlobalPosition - panel.GlobalPosition, c.Size * c.GetGlobalTransform().Scale);
	}

	private static void Build(Node2D deco, Vector2 size, Saved saved, Label? name)
	{
		if (!GodotObject.IsInstanceValid(deco) || deco.GetParent() is not Control panel)
		{
			return;
		}
		// 渐变字（排版完成后才量得准行高）
		void Grad(string path, Color top, Color bottom)
		{
			if (panel.GetNodeOrNull<Control>(path) is Control c)
			{
				saved.Undo.Add(Typo.ApplyGradient(c, top, bottom));
			}
		}
		Grad(DescPath, Colors.White, Ice);
		Grad(RelicDescPath, Colors.White, Ice);
		Grad(RelicNamePath, new Color(0.85f, 0.97f, 1f), Pal.Cyan);
		Grad(HpPath, Colors.White, Pal.Cyan);
		Grad(GoldPath, new Color(1f, 0.98f, 0.85f), Yellow);

		// 按原版每一块的实际位置画（不靠固定比例猜）
		Rect2 box = RectIn(panel, "VBoxContainer") ?? new Rect2(Vector2.Zero, size);
		Rect2? nameR = RectIn(panel, "VBoxContainer/Name");
		Rect2? iconR = RectIn(panel, "VBoxContainer/Relic/Icon");

		// ── 一整块板子：上面白色抬头（名字），下面深蓝正文；左右边缘和斜切角度完全一致 ──
		const float lean = 40f;
		float left = box.Position.X - 44f, right = box.End.X + 24f;
		float bottom = box.End.Y + 26f;
		float headTop = box.Position.Y - 14f, headBottom = box.Position.Y + 60f;
		Node2D head = new();
		Node2D text = new();
		if (nameR is Rect2 n)
		{
			// 按名字文字本身的行高定位（名字标签下方额外加了留白，不能用整个标签高度）
			float lh = name != null && GodotObject.IsInstanceValid(name) ? name.GetLineHeight() : n.Size.Y;
			float textTop = n.Position.Y;
			if (name != null && name.VerticalAlignment == VerticalAlignment.Center) textTop += (n.Size.Y - lh) * 0.5f;
			else if (name != null && name.VerticalAlignment == VerticalAlignment.Bottom) textTop += n.Size.Y - lh;
			headTop = textTop - lh * 0.22f;
			headBottom = textTop + lh * 1.08f;
			if (ResourceLoader.Load(PlatePath) is Texture2D plate)
			{
				float s = lh * 0.78f / PlateNameH;
				Vector2 origin = new(n.Position.X + 6f, textTop + lh * 0.9f);
				text.AddChild(new Sprite2D { Texture = plate, Centered = false, Scale = Vector2.One * s, Position = origin - PlateAnchor * s });
			}
		}
		float total = bottom - headTop;
		float X(float x, float y) => x + lean * (bottom - y) / total;       // 同一条斜边上 y 处的 x
		Fx.Poly(head, new[] { new Vector2(X(left, headTop - 7f) + 70f, headTop - 7f), new Vector2(X(right, headTop - 7f) + 10f, headTop - 7f), new Vector2(X(right, headTop) + 10f, headTop), new Vector2(X(left, headTop) + 70f, headTop) }, new Color(Pal.Cyan, 0.95f));   // 上沿错开的青边
		Fx.Poly(head, new[] { new Vector2(X(left, headTop), headTop), new Vector2(X(right, headTop), headTop), new Vector2(X(right, headBottom), headBottom), new Vector2(X(left, headBottom), headBottom) }, new Color(0.97f, 0.98f, 1f, 0.98f));   // 白色抬头
		Node2D main = new();
		deco.AddChild(main);
		Fx.Poly(main, new[] { new Vector2(X(left, headBottom), headBottom), new Vector2(X(right, headBottom), headBottom), new Vector2(right, bottom), new Vector2(left, bottom) }, Navy);   // 深蓝正文
		deco.AddChild(head);
		deco.AddChild(text);

		// ── 天赋：图标垫一张青边小斜卡 ──
		Node2D relic = new();
		deco.AddChild(relic);
		if (iconR is Rect2 ic)
		{
			Rect2 c = ic.Grow(6f);
			float k = c.Size.Y * 0.18f;
			Fx.Poly(relic, new[] { new Vector2(c.Position.X + k - 3f, c.Position.Y - 3f), new Vector2(c.End.X + k + 3f, c.Position.Y - 3f), new Vector2(c.End.X + 3f, c.End.Y + 3f), new Vector2(c.Position.X - 3f, c.End.Y + 3f) }, new Color(Pal.Cyan, 0.95f));
			Fx.Poly(relic, new[] { new Vector2(c.Position.X + k, c.Position.Y), new Vector2(c.End.X + k, c.Position.Y), c.End, new Vector2(c.Position.X, c.End.Y) }, new Color(0.03f, 0.08f, 0.32f, 1f));
		}

		// ── 血量 / 金币：同色下划线 + 中间红色斜杠 ──
		Node2D stats = new();
		deco.AddChild(stats);
		Rect2? hpR = RectIn(panel, HpPath), goldR = RectIn(panel, GoldPath);
		if (hpR is Rect2 h)
		{
			P3Ui.Slab(stats, h.Position.X - 40f, h.End.X + 8f, h.End.Y + 1f, h.End.Y + 5f, 0f, Pal.Cyan);
		}
		if (goldR is Rect2 g)
		{
			P3Ui.Slab(stats, g.Position.X - 40f, g.End.X + 8f, g.End.Y + 1f, g.End.Y + 5f, 0f, Yellow);
			if (hpR is Rect2 h2 && g.Position.X - 40f > h2.End.X + 20f)
			{
				float sx = (h2.End.X + 8f + g.Position.X - 40f) * 0.5f;
				Fx.Line(stats, new[] { new Vector2(sx + 7f, g.Position.Y + 2f), new Vector2(sx - 7f, g.End.Y + 4f) }, Red, 3f, additive: false);
			}
		}


		// 入场：板子先从左滑入，名字稍晚、带一点过冲，天赋 / 数字最后
		Fx.Animate(deco, 0.7, t =>
		{
			float time = t * 0.7f;
			float a = Ease.OutExpo(Mathf.Clamp(time / 0.4f, 0f, 1f));
			float b = Ease.OutBack(Mathf.Clamp((time - 0.08f) / 0.35f, 0f, 1f));
			float c = Ease.OutQuint(Mathf.Clamp((time - 0.15f) / 0.4f, 0f, 1f));
			main.Position = new Vector2(-120f * (1f - a), 0f);
			main.Modulate = new Color(1f, 1f, 1f, a);
			head.Position = main.Position;                          // 抬头和正文是同一块板子，一起动
			head.Modulate = main.Modulate;
			text.Position = new Vector2(-80f * (1f - b), 0f);       // 名字稍晚、带一点过冲
			text.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.08f) / 0.12f, 0f, 1f));
			relic.Position = new Vector2(-60f * (1f - c), 0f);
			relic.Modulate = new Color(1f, 1f, 1f, c);
			stats.Modulate = new Color(1f, 1f, 1f, c);
		});
	}
}
