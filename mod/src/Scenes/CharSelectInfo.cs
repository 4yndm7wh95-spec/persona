using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// 选人界面左侧的角色介绍板（所有角色共用的 InfoPanel）：选中结城理时换成 P3R 菜单风格，选别的角色时恢复原样。
/// 只改外观 —— 原版底板隐藏、名字换色，再垫一层 P3R 色块；文字内容（名字 / 血量 / 金币 / 描述 / 天赋）全部是原版的，不动。
/// 外观：深蓝斜切主板 + 左缘青色亮线；名字压在白色斜切标题条上（深蓝字 + 红色底线，P3R"选中项"样式）；
/// 标题条右侧 ARCANA 0 / THE FOOL 小字；天赋上方一道青色斜线分隔；各层错开滑入。
/// </summary>
public static class CharSelectInfo
{
	private const string DecoName = "P3InfoDeco";

	private sealed class Saved
	{
		public readonly List<(CanvasItem Item, Color SelfModulate)> Hidden = new();
		public System.Action? UndoName;
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
		Restore(panel, name);
		if (button.IsRandom || button.IsLocked || model.GetType().Name != "Regent")
		{
			return;
		}
		Saved saved = new();
		State[panel.GetInstanceId()] = saved;

		// 原版底板：面板自身的绘制 + 面板下除内容容器以外的直接子节点（只是底图 / 装饰）
		saved.Hidden.Add((panel, panel.SelfModulate));
		panel.SelfModulate = new Color(1f, 1f, 1f, 0f);
		foreach (Node child in panel.GetChildren())
		{
			if (child is CanvasItem ci && child is not Container && child.Name != DecoName)
			{
				saved.Hidden.Add((ci, ci.SelfModulate));
				ci.SelfModulate = new Color(1f, 1f, 1f, 0f);
			}
		}

		// 名字：P3R 粗斜体、深蓝字（白色标题条上）
		if (name != null)
		{
			saved.UndoName = Typo.Restyle(name, Typo.CjkHeavy, new Color(0.03f, 0.07f, 0.26f), new Color(1f, 1f, 1f, 0f), 0);
		}

		Node2D deco = new() { Name = DecoName };                     // Node2D：面板若是容器也不会被它排版、不挤动原版内容
		panel.AddChild(deco);
		panel.MoveChild(deco, 0);                                   // 最先画：内容都在它上面
		// 名字刚换过文字，容器要到下一帧才重新排版：等两帧再按名字的实际位置画
		Fx.After(deco, 0.034, "info panel deco", () => Build(deco, panel.Size, name));
	}

	private static void Restore(Control panel, Label? name)
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
		saved.UndoName?.Invoke();
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

	private static void Build(Node2D deco, Vector2 size, Label? name)
	{
		if (!GodotObject.IsInstanceValid(deco) || deco.GetParent() is not Control panel)
		{
			return;
		}
		// 按原版每一块的实际位置画（不靠固定比例猜）
		Rect2 box = RectIn(panel, "VBoxContainer") ?? new Rect2(Vector2.Zero, size);
		Rect2? nameR = RectIn(panel, "VBoxContainer/Name");
		Rect2? hpR = RectIn(panel, "VBoxContainer/HpGoldSpacer");
		Rect2? relicR = RectIn(panel, "VBoxContainer/Relic");

		// 名字实际文字宽度（标签本身可能比文字宽）
		float textW = 0f;
		if (name != null && GodotObject.IsInstanceValid(name) && nameR is Rect2 nr)
		{
			Font? f = name.LabelSettings?.Font ?? name.GetThemeFont("font");
			int fs = name.LabelSettings?.FontSize ?? name.GetThemeFontSize("font_size");
			textW = f != null ? f.GetStringSize(name.Text, HorizontalAlignment.Left, -1, fs).X : nr.Size.Y * name.Text.Length;
			textW = Mathf.Min(textW, nr.Size.X);
		}

		const float lean = 34f;                                     // 斜切：上边比下边右移
		Color navy = new(0.02f, 0.06f, 0.21f, 0.9f);
		float left = box.Position.X - 34f, right = box.End.X + 34f;
		float top = box.Position.Y - 18f, bottom = box.End.Y + 18f;

		Node2D main = new();
		deco.AddChild(main);
		P3Ui.Slab(main, left - lean, right, top, bottom, lean, navy);                                         // 主板
		P3Ui.Slab(main, left - lean - 6f, left - lean, top, bottom, lean, new Color(Pal.Cyan, 0.95f));      // 左缘青色亮线
		P3Ui.Slab(main, left - lean, left + (right - left) * 0.6f, bottom, bottom + 3f, 0f, new Color(Pal.Cyan, 0.7f));   // 底边细线

		Node2D head = new();
		deco.AddChild(head);
		if (nameR is Rect2 n)
		{
			// 标题条只包住名字这一行（不压到下面的血量 / 金币）；左边留出斜切的量，字不会露在外面
			float hy0 = n.Position.Y + n.Size.Y * 0.08f;
			float hy1 = hpR is Rect2 h ? Mathf.Min(n.End.Y, h.Position.Y) - 2f : n.End.Y - 2f;
			float hl = (hy1 - hy0) * 0.28f;
			float hx0 = n.Position.X - 22f - hl, hx1 = n.Position.X + textW + 46f;
			P3Ui.Slab(head, hx0 + 7f, hx1 + 7f, hy0 + 7f, hy1 + 7f, hl, new Color(0.01f, 0.01f, 0.04f, 0.85f));   // 错开的黑块
			P3Ui.Slab(head, hx0, hx1, hy0, hy1, hl, new Color(0.97f, 0.98f, 1f, 0.97f));                       // 白色标题条
			P3Ui.Slab(head, hx0, hx1 - 3f, hy1 - 6f, hy1, 0f, new Color(0.9f, 0.08f, 0.22f, 0.95f));           // 红色底线（名字正下方）
			Typo.Text(head, "ARCANA 0  /  THE FOOL", Typo.Latin, 22, new Color(Pal.Cyan, 0.95f), new Vector2(hx1 + 24f, hy1 - 30f));
		}

		// 描述和天赋之间的斜线分隔（落在两段之间的空隙里，不穿过文字）
		Node2D line = new();
		deco.AddChild(line);
		if (relicR is Rect2 r)
		{
			float ly = r.Position.Y - 6f;
			Fx.Line(line, new[] { new Vector2(box.Position.X, ly + 4f), new Vector2(box.Position.X + box.Size.X * 0.85f, ly - 4f) }, new Color(Pal.Cyan, 0.7f), 2f, additive: false);
		}

		// 入场：主板先从左滑入，标题条稍晚、带一点过冲
		Fx.Animate(deco, 0.7, t =>
		{
			float time = t * 0.7f;
			float a = Ease.OutExpo(Mathf.Clamp(time / 0.4f, 0f, 1f));
			float b = Ease.OutBack(Mathf.Clamp((time - 0.08f) / 0.35f, 0f, 1f));
			float c = Ease.OutQuint(Mathf.Clamp((time - 0.15f) / 0.4f, 0f, 1f));
			main.Position = new Vector2(-120f * (1f - a), 0f);
			main.Modulate = new Color(1f, 1f, 1f, a);
			head.Position = new Vector2(-160f * (1f - b), 0f);
			head.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.08f) / 0.12f, 0f, 1f));
			line.Modulate = new Color(1f, 1f, 1f, c);
		});
	}
}
