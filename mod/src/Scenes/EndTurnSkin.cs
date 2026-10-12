using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Patches;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// 结束回合按钮的 P3R 外观（只在结城理的对局里显示，别的角色自动恢复原版），参照 P3R 队伍列表的"普通行 / 选中行"：
/// · 平时：深蓝斜切长条 + 左端青色斜块 + 右侧红色细斜杠 + 错开的黑影，白 → 冰蓝渐变的粗斜体字；
/// · 原版"该结束回合了"发光时：渐变成"选中行"—— 白底 + 右上错开的红色细框，字变深蓝（按原版发光层透明度平滑过渡并轻轻呼吸）；
/// · 不能点（敌方回合等）时整体变暗。
/// 全部用抗锯齿多边形实时画（不用贴图，任何缩放都不出锯齿）；尺寸比 v0.9.15 的楔形小 30%。
/// 原版的图片层只是设为透明，悬停缩放、飞入飞出、快捷键、联机投票图标、长按条全部照常。
/// </summary>
public static class EndTurnSkin
{
	private const string DecoName = "P3EndTurn";
	private const string CjkBoldPath = "res://fonts/jpn/NotoSansCJKjp-Bold.otf";

	private static readonly Color Navy = new(0.035f, 0.08f, 0.3f, 0.97f);
	private static readonly Color Ink = new(0.03f, 0.06f, 0.24f);
	private static readonly Color Ice = new(0.72f, 0.88f, 1f);
	private static readonly Color Red = new(0.91f, 0.12f, 0.23f);
	private static Font? _font;

	/// <summary>游戏自带的 Noto Sans CJK 粗黑体 + 右斜（原版简中按钮用的是思源宋体）。</summary>
	private static Font LabelFont => _font ??= ResourceLoader.Load(CjkBoldPath) is Font f
		? new FontVariation { BaseFont = f, VariationTransform = new Transform2D(new Vector2(1f, 0f), new Vector2(-0.18f, 1f), Vector2.Zero) }
		: Typo.CjkHeavy;

	public static void Apply(NEndTurnButton button)
	{
		Control? visuals = button.GetNodeOrNull<Control>("Visuals");
		TextureRect? image = button.GetNodeOrNull<TextureRect>("Visuals/Image");
		Control? glow = button.GetNodeOrNull<Control>("Visuals/Glow");
		Control? glowVfx = button.GetNodeOrNull<Control>("Visuals/GlowVfx");
		Label? label = button.GetNodeOrNull<Label>("Visuals/Label");
		if (visuals == null || image == null || visuals.GetNodeOrNull(DecoName) != null)
		{
			return;
		}
		Node2D deco = new() { Name = DecoName, Visible = false };
		visuals.AddChild(deco);
		visuals.MoveChild(deco, 0);
		Node2D normal = new();
		deco.AddChild(normal);
		Node2D selected = new();
		deco.AddChild(selected);
		float builtW = -1f;
		string builtText = "";
		Vector2 labelScale = label?.Scale ?? Vector2.One;
		Vector2 labelPivot = label?.PivotOffset ?? Vector2.Zero;
		ShaderMaterial? textMat = null;
		Vector2 labelPos = label?.Position ?? Vector2.Zero;              // 原版标签位置（Build 会挪它，基准要用原始值）

		System.Action? undoLabel = null;
		bool skinned = false;
		float t = 0f;

		void Build(Vector2 pos, Vector2 size)
		{
			foreach (Node c in normal.GetChildren()) c.QueueFree();
			foreach (Node c in selected.GetChildren()) c.QueueFree();
			// 尺寸：v0.9.15 楔形宽 ≈ 368s、高 ≈ 90s（s = min(宽×0.8/368, 高×0.95/100)），这次各缩 30%
			float s = Mathf.Min(size.X * 0.8f / 368f, size.Y * 0.95f / 100f);
			float w = 368f * s * 0.7f, h = 90f * s * 0.7f, lean = h * 0.35f;
			Vector2 c0 = label != null ? labelPos + label.Size * 0.5f : pos + size * 0.5f;
			normal.Position = c0;
			selected.Position = c0;
			float x0 = -w * 0.5f, x1 = w * 0.5f, y0 = -h * 0.5f, y1 = h * 0.5f;
			float wedge = h * 0.34f;
			// 平时：黑影 → 深蓝长条 → 上沿细高光 → 左端青色斜块 → 右侧红色细斜杠
			P3Ui.Slab(normal, x0 + 6f, x1 + 6f, y0 + 6f, y1 + 6f, lean, new Color(0f, 0f, 0.02f, 0.55f));
			P3Ui.Slab(normal, x0, x1, y0, y1, lean, Navy);
			P3Ui.Slab(normal, x0 + wedge + 6f, x1 - 4f, y0 + 3f, y0 + 5f, lean * 2f / h, new Color(1f, 1f, 1f, 0.22f));
			P3Ui.Slab(normal, x0, x0 + wedge, y0, y1, lean, new Color(Pal.Cyan, 0.95f));
			float rx = x1 - h * 0.45f;
			Fx.Line(normal, new[] { new Vector2(rx + lean * 0.8f, y0 + 6f), new Vector2(rx + lean * 0.2f, y1 - 6f) }, Red, 3f, additive: false);
			// 选中：白底 + 右上错开的红色细框（P3R 队伍列表的选中行），左端青块保留
			Vector2[] frame = { new(x0 + lean + 6f, y0 - 6f), new(x1 + lean + 6f, y0 - 6f), new(x1 + 6f, y1 - 6f), new(x0 + 6f, y1 - 6f) };
			Fx.Line(selected, frame, Red, 3f, closed: true, additive: false);
			P3Ui.Slab(selected, x0, x1, y0, y1, lean, new Color(0.97f, 0.98f, 1f, 1f));
			P3Ui.Slab(selected, x0, x0 + wedge, y0, y1, lean, new Color(Pal.Cyan, 0.95f));
			if (label != null)
			{
				// 字缩到长条里（去掉左端青块后的宽度的 86%、高的 62%）
				Font f = label.LabelSettings?.Font ?? label.GetThemeFont("font");
				int fs = label.LabelSettings?.FontSize ?? label.GetThemeFontSize("font_size");
				float tw = f.GetStringSize(label.Text, HorizontalAlignment.Left, -1, fs).X;
				float k = Mathf.Min(1f, Mathf.Min((w - wedge - h * 0.4f) * 0.86f / Mathf.Max(tw, 1f), h * 0.62f / Mathf.Max(fs, 1)));
				label.PivotOffset = label.Size * 0.5f;
				label.Scale = Vector2.One * k;
				label.Position = c0 - label.Size * 0.5f + new Vector2(wedge * 0.5f, 0f);   // 让出左端青块
			}
			builtW = size.X;
			builtText = label?.Text ?? "";
		}

		void Skin(bool on)
		{
			if (on == skinned)
			{
				return;
			}
			skinned = on;
			deco.Visible = on;
			Color hide = new(1f, 1f, 1f, on ? 0f : 1f);
			image.SelfModulate = hide;
			if (glow != null) glow.SelfModulate = hide;
			if (glowVfx != null) glowVfx.SelfModulate = hide;
			if (label != null)
			{
				if (on)
				{
					labelPos = label.Position;
					System.Action undoStyle = Typo.Restyle(label, LabelFont, Colors.White, new Color(1f, 1f, 1f, 0f), 0);
					Material? oldMat = label.Material;
					textMat = Typo.Gradient(Colors.White, Ice, label.Size.Y);
					label.Material = textMat;
					undoLabel = () => { undoStyle(); if (GodotObject.IsInstanceValid(label)) label.Material = oldMat; };
					builtW = -1f;
				}
				else
				{
					undoLabel?.Invoke();
					undoLabel = null;
					textMat = null;
					label.Scale = labelScale;
					label.PivotOffset = labelPivot;
					label.Position = labelPos;
				}
			}
		}

		void Tick()
		{
			if (!GodotObject.IsInstanceValid(button) || !GodotObject.IsInstanceValid(deco))
			{
				return;
			}
			ModEntry.Safe("end turn tick", () =>
			{
				Skin(Stage.MakotoCombat());
				if (!skinned)
				{
					return;
				}
				if ((!Mathf.IsEqualApprox(builtW, image.Size.X) || (label != null && label.Text != builtText)) && image.Size.X > 1f)
				{
					Build(image.Position, image.Size);
				}
				t += 0.05f;
				string state = Traverse.Create(button).Field("_state").GetValue()?.ToString() ?? "Enabled";
				float dim = state == "Enabled" ? 1f : 0.45f;
				deco.Modulate = new Color(dim, dim, dim, 1f);
				// 原版"该结束回合了"的发光 → 选中行（白底红框），带一点呼吸
				float g = Mathf.Clamp((glow?.Modulate.A ?? 0f) * 1.4f, 0f, 1f);
				float sel = g * (0.82f + 0.18f * Mathf.Sin(t * 4f));
				selected.Modulate = new Color(1f, 1f, 1f, sel);
				if (textMat != null)
				{
					textMat.SetShaderParameter("top_color", Colors.White.Lerp(Ink, sel));
					textMat.SetShaderParameter("bottom_color", Ice.Lerp(Ink, sel));
				}
			});
		}

		Tween loop = deco.CreateTween().SetLoops();
		loop.TweenCallback(Callable.From(Tick)).SetDelay(0.05);
	}
}
