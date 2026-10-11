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
/// 标题条右侧 ARCANA 0 / THE FOOL 小字；天赋上方一道青色斜线分隔；右下角半透明超大 FOOL 水印。各层错开滑入。
/// </summary>
public static class CharSelectInfo
{
	private const string DecoName = "P3InfoDeco";

	private sealed class Saved
	{
		public readonly List<(CanvasItem Item, Color SelfModulate)> Hidden = new();
		public LabelSettings? NameSettings;
		public bool NameOverride;
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

		// 名字：深蓝字（白色标题条上）
		if (name != null)
		{
			Color navy = new(0.03f, 0.07f, 0.26f);
			if (name.LabelSettings != null)
			{
				saved.NameSettings = name.LabelSettings;
				LabelSettings ls = (LabelSettings)name.LabelSettings.Duplicate();
				ls.FontColor = navy;
				ls.OutlineColor = new Color(1f, 1f, 1f, 0f);
				ls.ShadowColor = new Color(0f, 0f, 0f, 0f);
				name.LabelSettings = ls;
			}
			else
			{
				saved.NameOverride = true;
				name.AddThemeColorOverride("font_color", navy);
				name.AddThemeColorOverride("font_outline_color", new Color(1f, 1f, 1f, 0f));
				name.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0f));
			}
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
		if (name != null && GodotObject.IsInstanceValid(name))
		{
			if (saved.NameSettings != null)
			{
				name.LabelSettings = saved.NameSettings;
			}
			if (saved.NameOverride)
			{
				name.RemoveThemeColorOverride("font_color");
				name.RemoveThemeColorOverride("font_outline_color");
				name.RemoveThemeColorOverride("font_shadow_color");
			}
		}
	}

	private static void Build(Node2D deco, Vector2 size, Label? name)
	{
		float w = Mathf.Max(size.X, 560f), h = Mathf.Max(size.Y, 300f);
		const float lean = 46f;                                     // 斜切：上边比下边右移
		Color navy = new(0.02f, 0.06f, 0.21f, 0.9f);

		// 名字所在的高度（标题条对齐名字）
		float nameTop = 10f, nameH = 86f;
		if (!GodotObject.IsInstanceValid(deco))
		{
			return;
		}
		if (name != null && GodotObject.IsInstanceValid(name))
		{
			Vector2 p = name.GlobalPosition - deco.GetParent<Control>().GlobalPosition;
			nameTop = p.Y - 8f;
			nameH = Mathf.Max(name.Size.Y, 60f) + 16f;
		}

		Node2D main = new();
		deco.AddChild(main);
		P3Ui.Slab(main, -10f, w + 10f, -6f, h + 6f, lean, navy);                                       // 主板
		P3Ui.Slab(main, -16f, -10f, -6f, h + 6f, lean, new Color(Pal.Cyan, 0.95f));                    // 左缘青色亮线
		P3Ui.Slab(main, -10f, w * 0.7f, h + 6f, h + 9f, 0f, new Color(Pal.Cyan, 0.7f));               // 底边细线
		Node2D mark = P3Ui.GiantWord(main, "FOOL", new Vector2(w * 0.5f, h * 0.98f), (int)(h * 0.42f), -0.12f, new Color(Pal.Royal, 0.12f));

		Node2D head = new();
		deco.AddChild(head);
		float hx0 = 4f, hx1 = w * 0.62f;
		float hy0 = nameTop, hy1 = nameTop + nameH;
		float hl = (hy1 - hy0) * 0.3f;
		P3Ui.Slab(head, hx0 + 8f, hx1 + 8f, hy0 + 8f, hy1 + 8f, hl, new Color(0.01f, 0.01f, 0.04f, 0.85f));   // 错开的黑块
		P3Ui.Slab(head, hx0, hx1, hy0, hy1, hl, new Color(0.97f, 0.98f, 1f, 0.97f));                       // 白色标题条
		P3Ui.Slab(head, hx0, hx1 - 2f, hy1 - 7f, hy1, 0f, new Color(0.9f, 0.08f, 0.22f, 0.95f));           // 红色底线
		Typo.Text(head, "ARCANA 0  /  THE FOOL", Typo.Latin, 22, new Color(Pal.Cyan, 0.95f), new Vector2(hx1 + 26f, hy0 + nameH * 0.5f - 14f));

		// 天赋（最后一段）上方的斜线分隔
		Node2D line = new();
		deco.AddChild(line);
		float ly = h * 0.7f;
		Fx.Line(line, new[] { new Vector2(10f, ly + 10f), new Vector2(w * 0.82f, ly - 6f) }, new Color(Pal.Cyan, 0.75f), 2f, additive: false);

		// 入场：主板先从左滑入，标题条稍晚、带一点过冲，水印缓慢漂移
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
			line.Scale = new Vector2(c, 1f);
			line.Modulate = new Color(1f, 1f, 1f, c);
		});
		Tween drift = mark.CreateTween().SetLoops();
		drift.TweenProperty(mark, "position:x", w * 0.5f - 40f, 9.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		drift.TweenProperty(mark, "position:x", w * 0.5f, 9.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}
}
