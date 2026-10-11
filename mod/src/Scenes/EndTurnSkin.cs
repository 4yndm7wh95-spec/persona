using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Patches;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// 结束回合按钮的 P3R 外观（只在结城理的对局里显示，别的角色自动恢复原版）：
/// 深蓝斜切按钮 + 青色描边 + 左侧红色标签块 + 白色斜体字，定时扫过一道光；
/// 不能出牌时原版会让按钮发光提示 —— 改成按钮背后一圈青色光晕呼吸（读原版发光层的透明度）；禁用时整体变暗。
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
		Node2D halo = new();
		deco.AddChild(halo);
		Node2D body = new();
		deco.AddChild(body);
		Node2D sweepClip = new();
		Polygon2D? sweep = null;
		float builtW = -1f;

		LabelSettings? origSettings = label?.LabelSettings;
		bool skinned = false;
		float t = 0f;

		void Build(Vector2 pos, Vector2 size)
		{
			foreach (Node c in halo.GetChildren()) c.QueueFree();
			foreach (Node c in body.GetChildren()) c.QueueFree();
			float w = size.X * 0.92f, h = size.Y * 0.62f;
			Vector2 c0 = pos + size * 0.5f;
			float x0 = c0.X - w / 2f, x1 = c0.X + w / 2f, y0 = c0.Y - h / 2f, y1 = c0.Y + h / 2f;
			float lean = h * 0.35f;
			P3Ui.Slab(halo, x0 - 14f, x1 + 14f, y0 - 12f, y1 + 12f, lean, new Color(Pal.Cyan, 0.55f));
			P3Ui.Slab(body, x0 + 7f, x1 + 7f, y0 + 7f, y1 + 7f, lean, new Color(0.01f, 0.01f, 0.04f, 0.8f));       // 错开的黑块
			P3Ui.Slab(body, x0 - 3f, x1 + 3f, y0 - 3f, y1 + 3f, lean, new Color(Pal.Cyan, 0.95f));                   // 青色描边
			P3Ui.Slab(body, x0, x1, y0, y1, lean, new Color(0.03f, 0.08f, 0.3f, 0.97f));                           // 深蓝主体
			P3Ui.Slab(body, x0 + 4f, x1 - 4f, y0 + 4f, y0 + 6f, lean * (2f / h), new Color(1f, 1f, 1f, 0.35f));      // 上沿高光线
			P3Ui.Slab(body, x0 + 2f, x0 + 2f + h * 0.22f, y0 + 2f, y1 - 2f, lean * 0.92f, new Color(0.9f, 0.08f, 0.22f, 0.95f));   // 左侧红色标签
			sweep = Fx.Poly(body, new[] { new Vector2(-18f, y0), new Vector2(0f, y0), new Vector2(-lean, y1), new Vector2(-18f - lean, y1) }, new Color(1f, 1f, 1f, 0.28f), additive: true);
			sweep.Position = new Vector2(x0 - 40f, 0f);
			builtW = size.X;
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
					if (label.LabelSettings != null)
					{
						LabelSettings ls = (LabelSettings)label.LabelSettings.Duplicate();
						ls.FontColor = Colors.White;
						ls.OutlineColor = new Color(0.02f, 0.05f, 0.2f);
						label.LabelSettings = ls;
					}
					else
					{
						label.AddThemeColorOverride("font_color", Colors.White);
						label.AddThemeColorOverride("font_outline_color", new Color(0.02f, 0.05f, 0.2f));
					}
				}
				else
				{
					label.LabelSettings = origSettings;
					label.RemoveThemeColorOverride("font_color");
					label.RemoveThemeColorOverride("font_outline_color");
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
				if (!Mathf.IsEqualApprox(builtW, image.Size.X) && image.Size.X > 1f)
				{
					Build(image.Position, image.Size);
				}
				t += 0.05f;
				string state = Traverse.Create(button).Field("_state").GetValue()?.ToString() ?? "Enabled";
				float dim = state == "Enabled" ? 1f : 0.45f;
				body.Modulate = new Color(dim, dim, dim, 1f);
				float g = glow?.Modulate.A ?? 0f;                           // 原版"该结束回合了"的发光
				halo.Modulate = new Color(1f, 1f, 1f, g * (0.55f + 0.45f * Mathf.Sin(t * 5f)));
				if (sweep != null && builtW > 0f)
				{
					float cycle = (t % 3.2f) / 0.6f;                           // 每 3.2 秒扫一次，扫 0.6 秒
					sweep.Visible = cycle < 1f && state == "Enabled";
					sweep.Position = new Vector2(image.Position.X - 30f + (builtW + 80f) * Mathf.Clamp(cycle, 0f, 1f), 0f);
				}
			});
		}

		Tween loop = deco.CreateTween().SetLoops();
		loop.TweenCallback(Callable.From(Tick)).SetDelay(0.05);
	}
}
