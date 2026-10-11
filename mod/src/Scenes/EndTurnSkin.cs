using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Patches;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// 结束回合按钮的 P3R 外观（只在结城理的对局里显示，别的角色自动恢复原版）：
/// P3R 主菜单"选中项"同款（白色楔形碎片 + 错开的黑色碎片 + 品红细边，贴图见 tools/ui/build_plates.endturn），文字换成红色渐变粗斜体；
/// 尺寸比原版按钮小一圈。原版"该结束回合了"的发光提示 —— 改成楔形背后一圈青色加法光晕呼吸（读原版发光层的透明度）；禁用时整体变暗。
/// 原版的图片层只是设为透明，悬停缩放、飞入飞出、快捷键、联机投票图标、长按条全部照常。
/// </summary>
public static class EndTurnSkin
{
	private const string DecoName = "P3EndTurn";

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
		Texture2D? tex = ResourceLoader.Load("res://p3makoto/ui/endturn.png") as Texture2D;
		Texture2D? haloTex = ResourceLoader.Load("res://p3makoto/ui/endturn_sel.png") as Texture2D;
		Sprite2D halo = new() { Texture = haloTex, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
		deco.AddChild(halo);
		Sprite2D body = new() { Texture = tex };
		deco.AddChild(body);
		float builtW = -1f;
		string builtText = "";
		Vector2 labelScale = label?.Scale ?? Vector2.One;
		Vector2 labelPivot = label?.PivotOffset ?? Vector2.Zero;

		System.Action? undoLabel = null;
		bool skinned = false;
		float t = 0f;

		// 贴图 460×150：白色楔形 x 56~424、y 14~116；文字放在楔形中间偏左 (215, 70)，贴图中心 (230, 75)
		void Build(Vector2 pos, Vector2 size)
		{
			if (tex == null) return;
			float s = Mathf.Min(size.X * 0.8f / 368f, size.Y * 0.95f / 100f);
			Vector2 c = label != null ? label.Position + label.Size * 0.5f : pos + size * 0.5f;
			body.Scale = Vector2.One * s;
			body.Position = c + new Vector2(230f - 215f, 75f - 70f) * s;
			halo.Scale = body.Scale;
			halo.Position = body.Position;
			if (label != null)
			{
				// 字缩到楔形里（楔形宽的 68%、高的 55%）
				Font? f = label.LabelSettings?.Font ?? label.GetThemeFont("font");
				int fs = label.LabelSettings?.FontSize ?? label.GetThemeFontSize("font_size");
				float tw = f != null ? f.GetStringSize(label.Text, HorizontalAlignment.Left, -1, fs).X : label.Size.X;
				float k = Mathf.Min(1f, Mathf.Min(368f * s * 0.68f / Mathf.Max(tw, 1f), 100f * s * 0.55f / Mathf.Max(fs, 1)));
				label.PivotOffset = label.Size * 0.5f;
				label.Scale = Vector2.One * k;
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
					System.Action undoStyle = Typo.Restyle(label, Typo.CjkHeavy, Colors.White, new Color(1f, 1f, 1f, 0f), 0);
					Material? oldMat = label.Material;
					label.Material = Typo.Gradient(new Color(1f, 0.35f, 0.45f), new Color(0.82f, 0.02f, 0.14f), label.Size.Y);   // 红色渐变字
					undoLabel = () => { undoStyle(); if (GodotObject.IsInstanceValid(label)) label.Material = oldMat; };
					builtW = -1f;
				}
				else
				{
					undoLabel?.Invoke();
					undoLabel = null;
					label.Scale = labelScale;
					label.PivotOffset = labelPivot;
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
				body.Modulate = new Color(dim, dim, dim, 1f);
				float g = glow?.Modulate.A ?? 0f;                           // 原版"该结束回合了"的发光
				halo.Modulate = new Color(1f, 1f, 1f, g * (0.55f + 0.35f * Mathf.Sin(t * 5f)));
			});
		}

		Tween loop = deco.CreateTween().SetLoops();
		loop.TweenCallback(Callable.From(Tick)).SetDelay(0.05);
	}
}
