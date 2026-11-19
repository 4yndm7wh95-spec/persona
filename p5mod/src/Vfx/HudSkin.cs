using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 战斗 HUD 换皮（2026-11-19，用户：替换原版的局内能量、结束本回合 UI，延续选卡界面 / 回合横幅的剪报风）。
/// Codex 素材 art/p5/ui/hud（任务单 docs/codex/codex_p5_hud_art.md）+ 回合横幅的拼贴数字（art/p5/ui/battle/digits）。
/// 照 P3 mod 的 HudSkin / EndTurnSkin：原版节点、逻辑一律照常，只把原版的图层藏掉（不是删掉），装饰每帧自己判断要不要显示 ——
/// 节点在角色加载前就建好了，别的角色的对局自动恢复原版。
/// · 能量：黑色剪纸圆片 + 红色星芒（能量用完换成熄灭版），外面一圈断开的纸环慢慢转；中间拼贴数字「当前」大、「/上限」小贴在右下，
///   能量涨了数字弹一下。
/// · 结束回合：黑底纸板 + 拼贴字「结束回合」，轮次数字小纸片贴在右上角；原版发光提示「该结束回合了」时渐变成红色纸板；
///   悬停放大一点；不能点时跟着原版变灰。其他文字（联机的「取消」等）照常显示在纸板上。
/// </summary>
public static class HudSkin
{
	private const string DecoName = "P5Hud";
	private static readonly Color Hidden = new(1f, 1f, 1f, 0f);

	private static bool Zh => TranslationServer.GetLocale().StartsWith("zh", StringComparison.OrdinalIgnoreCase);

	/// <summary>每帧调 tick（直到 owner 或装饰没了）。</summary>
	private static void Drive(Node owner, Node deco, string where, Action<float> tick)
	{
		SceneTree? tree = owner.GetTree();
		if (tree == null) return;
		void Frame()
		{
			if (!GodotObject.IsInstanceValid(owner) || !GodotObject.IsInstanceValid(deco))
			{
				tree.ProcessFrame -= Frame;
				return;
			}
			Stage.Safe(where, () => tick((float)owner.GetProcessDeltaTime()));
		}
		tree.ProcessFrame += Frame;
		deco.TreeExiting += () => tree.ProcessFrame -= Frame;
	}

	private static Sprite2D? Spr(Node parent, string tex, float scale)
	{
		if (Fx.Tex(tex) is not Texture2D t) return null;
		Sprite2D s = new() { Texture = t, Scale = Vector2.One * scale };
		parent.AddChild(s);
		return s;
	}

	/// <summary>一张拼贴数字纸片（battle_digit_N / hud_slash），纸片高约 h。</summary>
	private static Sprite2D? Digit(Node parent, string tex, float h, Vector2 at, float rot)
	{
		if (Fx.Tex(tex) is not Texture2D t) return null;
		Sprite2D s = new() { Texture = t, Scale = Vector2.One * h / (t.GetHeight() * 0.73f), Position = at, Rotation = rot };
		parent.AddChild(s);
		return s;
	}

	private static (int cur, int max) ParseEnergy(string text)
	{
		string[] parts = text.Split('/');
		int.TryParse(parts[0].Trim(), out int cur);
		int max = 0;
		if (parts.Length > 1) int.TryParse(parts[1].Trim(), out max);
		return (cur, max);
	}

	// ───────────────── 能量 ─────────────────

	public static void ApplyEnergy(Control counter, Func<bool> joker)
	{
		if (counter.GetNodeOrNull(DecoName) != null || Fx.Tex("hud_energy_base") == null) return;
		Control? layers = counter.GetNodeOrNull<Control>("%Layers");
		Label? label = counter.GetNodeOrNull<Label>("Label");
		CanvasItem? vfxBack = counter.GetNodeOrNull<CanvasItem>("%EnergyVfxBack");
		CanvasItem? vfxFront = counter.GetNodeOrNull<CanvasItem>("%EnergyVfxFront");
		if (layers == null || label == null) return;

		Node2D deco = new() { Name = DecoName, Visible = false };
		counter.AddChild(deco);
		counter.MoveChild(deco, layers.GetIndex() + 1);
		// 底座：黑色圆片直径约 120（素材半尺寸 512 里圆片约 0.62）
		const float k = 0.37f;
		Node2D medal = new();
		deco.AddChild(medal);
		Sprite2D? lit = Spr(medal, "hud_energy_base", k);
		Sprite2D? spent = Spr(medal, "hud_energy_spent", k);
		if (spent != null) spent.Modulate = Hidden;
		Sprite2D? ring = Spr(deco, "hud_energy_ring", 0.46f);
		Node2D num = new() { Position = new Vector2(-2f, 2f) };
		deco.AddChild(num);

		bool skinned = false;
		string shownText = "";
		int shownCur = -1;
		float pulse = 0f, dead = 0f;

		void Skin(bool on)
		{
			if (on == skinned) return;
			skinned = on;
			deco.Visible = on;
			layers.Visible = !on;
			if (vfxBack != null) vfxBack.Visible = !on;
			if (vfxFront != null) vfxFront.Visible = !on;
			label.SelfModulate = on ? Hidden : Colors.White;
			shownText = "";
		}

		void Build(int cur, int max)
		{
			foreach (Node c in num.GetChildren()) c.QueueFree();
			string a = Math.Clamp(cur, 0, 99).ToString();
			float h = a.Length > 1 ? 48f : 62f, step = h * 0.62f;
			float x0 = -10f - (a.Length - 1) * step * 0.5f;
			for (int i = 0; i < a.Length; i++)
			{
				Digit(num, "battle_digit_" + a[i], h, new Vector2(x0 + i * step, -4f), (i % 2 == 0 ? -0.08f : 0.07f));
			}
			if (max <= 0) return;
			string b = Math.Clamp(max, 0, 99).ToString();
			float bx = x0 + (a.Length - 1) * step + h * 0.42f;
			Digit(num, "hud_slash", 30f, new Vector2(bx, 18f), 0f);
			for (int i = 0; i < b.Length; i++)
			{
				Digit(num, "battle_digit_" + b[i], 26f, new Vector2(bx + 15f + i * 16f, 26f), (i % 2 == 0 ? 0.1f : -0.06f));
			}
		}

		Drive(counter, deco, "P5HudEnergy", dt =>
		{
			Skin(joker());
			if (!skinned) return;
			deco.Position = counter.Size * 0.5f;
			if (label.Text != shownText)
			{
				shownText = label.Text;
				(int cur, int max) = ParseEnergy(shownText);
				if (shownCur >= 0 && cur > shownCur) pulse = 1f;
				shownCur = cur;
				Build(cur, max);
			}
			pulse = Mathf.Max(0f, pulse - dt * 5f);
			float target = shownCur == 0 ? 1f : 0f;
			dead = Mathf.MoveToward(dead, target, dt * 4f);
			if (lit != null) lit.Modulate = new Color(1f, 1f, 1f, 1f - dead);
			if (spent != null) spent.Modulate = new Color(1f, 1f, 1f, dead);
			num.Scale = Vector2.One * (1f + 0.35f * pulse * pulse);
			medal.Scale = Vector2.One * (1f + 0.08f * pulse);
			if (ring != null)
			{
				ring.Rotation += Mathf.DegToRad(shownCur == 0 ? 4f : 16f) * dt;
				ring.Modulate = new Color(1f, 1f, 1f, 1f - 0.5f * dead);
			}
		});
	}

	// ───────────────── 结束回合 ─────────────────

	public static void ApplyEndTurn(Control button, Func<bool> joker)
	{
		Control? visuals = button.GetNodeOrNull<Control>("Visuals");
		TextureRect? image = button.GetNodeOrNull<TextureRect>("Visuals/Image");
		CanvasItem? glow = button.GetNodeOrNull<CanvasItem>("Visuals/Glow");
		CanvasItem? glowVfx = button.GetNodeOrNull<CanvasItem>("Visuals/GlowVfx");
		Label? label = button.GetNodeOrNull<Label>("Visuals/Label");
		if (visuals == null || image == null || label == null || visuals.GetNodeOrNull(DecoName) != null || Fx.Tex("hud_endturn_plate") == null) return;

		Node2D deco = new() { Name = DecoName, Visible = false };
		visuals.AddChild(deco);
		visuals.MoveChild(deco, image.GetIndex() + 1);
		Node2D body = new();
		deco.AddChild(body);
		Sprite2D? plate = Spr(body, "hud_endturn_plate", 1f);
		Sprite2D? ready = Spr(body, "hud_endturn_plate_ready", 1f);
		if (ready != null) ready.Modulate = Hidden;
		Sprite2D? text = Spr(body, Zh ? "hud_endturn_text_zh" : "hud_endturn_text_en", 1f);
		Node2D badge = new();
		body.AddChild(badge);

		bool skinned = false;
		string shownText = "";
		bool useArt = false;
		float hover = 0f, readyA = 0f;

		void Skin(bool on)
		{
			if (on == skinned) return;
			skinned = on;
			deco.Visible = on;
			image.SelfModulate = on ? Hidden : Colors.White;
			if (glow != null) glow.SelfModulate = on ? Hidden : Colors.White;
			if (glowVfx != null) glowVfx.SelfModulate = on ? Hidden : Colors.White;
			if (!on) label.SelfModulate = Colors.White;
			shownText = "";
		}

		Drive(button, deco, "P5HudEndTurn", dt =>
		{
			Skin(joker());
			if (!skinned) return;
			Vector2 size = image.Size;
			deco.Position = image.Position + size * 0.5f;
			// 纸板素材 1024×384（半尺寸 512），纸板本身约占宽 0.88
			float pk = size.X * 1.08f / (512f * 0.88f);
			if (plate != null) plate.Scale = Vector2.One * pk;
			if (ready != null) ready.Scale = Vector2.One * pk;

			if (label.Text != shownText)
			{
				shownText = label.Text;
				string t = shownText.Trim();
				useArt = text != null && (t.StartsWith("结束", StringComparison.Ordinal) || t.StartsWith("End Turn", StringComparison.OrdinalIgnoreCase));
				foreach (Node c in badge.GetChildren()) c.QueueFree();
				if (useArt)
				{
					// 「结束第 N 回合」的 N：小数字纸片贴在纸板右上角
					string digits = "";
					foreach (char ch in t) if (char.IsAsciiDigit(ch)) digits += ch;
					if (digits.Length > 2) digits = digits[^2..];
					for (int i = 0; i < digits.Length; i++)
					{
						Digit(badge, "battle_digit_" + digits[i], 34f, new Vector2(i * 20f, 0f), i % 2 == 0 ? 0.12f : -0.05f);
					}
				}
			}
			if (text != null)
			{
				text.Visible = useArt;
				text.Scale = Vector2.One * size.X * 0.74f / (384f * 0.88f);
				text.Position = new Vector2(-size.X * 0.03f, 0f);
			}
			badge.Position = new Vector2(size.X * 0.47f, -size.Y * 0.36f);
			label.SelfModulate = useArt ? Hidden : Colors.White;

			// 原版「该结束回合了」的发光层透明度（0 → 0.75）→ 红色纸板
			float target = glow != null ? Mathf.Clamp(glow.Modulate.A / 0.75f, 0f, 1f) : 0f;
			readyA = Mathf.MoveToward(readyA, target, dt * 4f);
			if (ready != null) ready.Modulate = new Color(1f, 1f, 1f, readyA);
			// 悬停：原版把文字染成红 / 青
			Color lm = label.Modulate;
			bool hovered = Mathf.Abs(lm.R - lm.G) > 0.3f || Mathf.Abs(lm.G - lm.B) > 0.3f;
			hover = Mathf.MoveToward(hover, hovered ? 1f : 0f, dt * 8f);
			body.Scale = Vector2.One * (1f + 0.06f * hover);
			body.Rotation = -0.02f * hover;
			// 不能点：原版把图片染灰
			deco.Modulate = image.Modulate;
		});
	}
}
