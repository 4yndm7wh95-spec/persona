using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Patches;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// v0.14.59：战斗界面左下 / 右下的图标换成 P3R 风格（样式见 tools/ui/hud_preview.py），只在结城理的对局里显示，别的情况自动恢复原版：
/// · 能量：白边深蓝圆盘 + 一圈按上限分格的青色格子（有几点亮几格）+ 斜切黑底上的大号粗斜体数字 + 外圈慢转的青色细弧和刻度点 + "ENERGY" 斜切标签；
///   能量增加时圆盘弹一下、外扩一道光环；能量为 0 时整体压暗、数字变红。
/// · 辉星：白边深蓝菱形牌 + 内圈青线 + 顶上一颗四角星 + 大号数字；后面一个慢转的细线方框（代替原版的旋转层）。
/// · 抽牌堆 / 弃牌堆 / 消耗堆按钮：两张塔罗牌背斜着叠放 + 斜切黑底白边红尾的张数牌 + 青色小字 DRAW / DISCARD / EXHAUST（右侧按钮左右镜像）。
/// 原版的图片层只是设为透明 / 隐藏，悬停放大、按下变暗、进出场动画、提示框、快捷键都照常（牌堆按钮的装饰挂在原版 Icon 下面，跟着它缩放和变暗）。
/// </summary>
public static class HudSkin
{
	private const string DecoName = "P3HudSkin";
	private static readonly Color Navy = new(0.04f, 0.086f, 0.275f, 1f);
	private static readonly Color Ink = new(0.024f, 0.047f, 0.157f, 1f);
	private static readonly Color Empty = new(0.12f, 0.2f, 0.47f, 1f);
	private static readonly Color Red = new(0.886f, 0.125f, 0.227f, 1f);
	private static Texture2D? _back;
	private static Texture2D? Back => _back ??= ResourceLoader.Exists("res://p3makoto/fx/pile_card_back.png") ? ResourceLoader.Load<Texture2D>("res://p3makoto/fx/pile_card_back.png") : null;

	/// <summary>每帧回调；节点没了就自己退订。出错一次就整套退回原版（不每帧重试每帧报错）。</summary>
	private static void Drive(Node owner, Node2D deco, string what, Action<float> tick, Action restore)
	{
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		ulong last = Time.GetTicksUsec();
		Action? handler = null;
		handler = () =>
		{
			if (!GodotObject.IsInstanceValid(owner) || !GodotObject.IsInstanceValid(deco))
			{
				tree.ProcessFrame -= handler;
				return;
			}
			ulong now = Time.GetTicksUsec();
			float dt = Mathf.Min((now - last) / 1e6f, 0.1f);
			last = now;
			try
			{
				tick(dt);
			}
			catch (Exception e)
			{
				tree.ProcessFrame -= handler;
				ModEntry.Report(what, e);
				try { restore(); } catch (Exception) { }
				deco.QueueFree();
			}
		};
		tree.ProcessFrame += handler;
	}

	private static Vector2[] Circle(float r, int n = 48)
	{
		Vector2[] p = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			float a = Mathf.Tau * i / n;
			p[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
		}
		return p;
	}

	private static Sprite2D Shadow(Node2D parent, float r, Vector2 offset)
	{
		Sprite2D s = new() { Texture = Fx.SoftDot, Position = offset, Scale = Vector2.One * r * 2.5f / Fx.SoftDot.GetWidth(), Modulate = new Color(0f, 0f, 0.04f, 0.75f) };
		parent.AddChild(s);
		return s;
	}

	private static Label Number(Node2D parent, int size, Color color, int outline, Vector2 box)
	{
		Label l = Typo.Text(parent, "", Typo.Barlow, size, color, -box * 0.5f, HorizontalAlignment.Center, outline, Ink);
		l.VerticalAlignment = VerticalAlignment.Center;
		l.Size = box;
		return l;
	}

	/// <summary>
	/// 把数字的"笔画中心"放到 ink（不是按行高居中）：Barlow 的数字高 0.7em、坐在基线上，按行高居中会往下坠约 0.05em。
	/// align = Left 时 ink.X 是字的左边缘，否则是字的水平中心。
	/// </summary>
	private static void PlaceInk(Label l, Vector2 ink, float boxW, HorizontalAlignment align = HorizontalAlignment.Center)
	{
		int size = l.LabelSettings?.FontSize ?? 16;
		Font f = l.LabelSettings?.Font ?? Typo.Barlow;
		float ascent = f.GetAscent(size);
		l.VerticalAlignment = VerticalAlignment.Top;
		l.HorizontalAlignment = align;
		l.Size = new Vector2(boxW, f.GetHeight(size));
		l.Position = new Vector2(align == HorizontalAlignment.Left ? ink.X : ink.X - boxW * 0.5f, ink.Y - (ascent - 0.35f * size));
	}

	/// <summary>P3R 斜切标签：白边 + 黑底 + 右端红色尾边，白色粗斜体字（中心对齐到 center）。</summary>
	private static (Node2D Root, Label Text) Tag(Node2D parent, Vector2 center, string text, int size, float h, bool red = true)
	{
		Node2D root = new() { Position = center };
		parent.AddChild(root);
		float w = Typo.Barlow.GetStringSize(text, HorizontalAlignment.Left, -1, size).X + h * 0.9f;
		Rebuild(root, w, h, red);
		Label l = Typo.Text(root, text, Typo.Barlow, size, Colors.White, Vector2.Zero, HorizontalAlignment.Center);
		PlaceTagText(l, w, h, red);
		l.Name = "Text";
		return (root, l);
	}

	/// <summary>标签字放在去掉红尾后的黑底中心（斜切让上沿右移 lean，所以再往右挪 lean/2）。</summary>
	private static void PlaceTagText(Label l, float w, float h, bool red)
	{
		float tail = red ? h * 0.28f : 0f;
		PlaceInk(l, new Vector2(-w * 0.5f + (w - tail) * 0.5f + h * 0.15f, 0f), w);
	}

	private static void Rebuild(Node2D root, float w, float h, bool red)
	{
		foreach (Node c in root.GetChildren())
		{
			if (c is Polygon2D)
			{
				c.QueueFree();
			}
		}
		float x = -w * 0.5f, y = -h * 0.5f, lean = h * 0.3f;
		Polygon2D edge = Fx.Poly(root, new[] { new Vector2(x - 2f + lean, y - 2f), new Vector2(x + w + lean + 2f, y - 2f), new Vector2(x + w + 2f, y + h + 2f), new Vector2(x - 2f, y + h + 2f) }, Colors.White);
		Polygon2D body = Fx.Poly(root, new[] { new Vector2(x + lean, y), new Vector2(x + w + lean, y), new Vector2(x + w, y + h), new Vector2(x, y + h) }, Ink);
		root.MoveChild(edge, 0);
		root.MoveChild(body, 1);
		if (red)
		{
			Polygon2D tail = Fx.Poly(root, new[] { new Vector2(x + w - h * 0.28f + lean, y), new Vector2(x + w + lean, y), new Vector2(x + w, y + h), new Vector2(x + w - h * 0.28f, y + h) }, Red);
			root.MoveChild(tail, 2);
		}
	}

	private static void Ring(Node2D parent, int n, float r0, float r1, float gapDeg)
	{
		foreach (Node c in parent.GetChildren())
		{
			c.QueueFree();
		}
		float span = 360f / n, gap = n == 1 ? 0f : gapDeg;
		for (int i = 0; i < n; i++)
		{
			float s = Mathf.DegToRad(-90f + i * span + gap / 2f), e = Mathf.DegToRad(-90f + (i + 1) * span - gap / 2f);
			const int k = 10;
			Vector2[] pts = new Vector2[(k + 1) * 2];
			for (int j = 0; j <= k; j++)
			{
				float a = Mathf.Lerp(s, e, j / (float)k);
				pts[j] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r1;
				pts[pts.Length - 1 - j] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r0;
			}
			Fx.Poly(parent, pts, Empty);
		}
	}

	private static (int Cur, int Max) ParseEnergy(string text)
	{
		int slash = text.IndexOf('/');
		if (slash < 0)
		{
			return (int.TryParse(text.Trim(), out int only) ? only : 0, 0);
		}
		int.TryParse(text[..slash].Trim(), out int cur);
		int.TryParse(text[(slash + 1)..].Trim(), out int max);
		return (cur, max);
	}

	/// <summary>一圈往外扩、渐隐的光环（能量 / 辉星增加时）。</summary>
	private static void Burst(Node2D parent, float r, Color color)
	{
		Line2D ring = Fx.Line(parent, Circle(r, 40), color, 3f, closed: true);
		Fx.Animate(ring, 0.45, t =>
		{
			ring.Scale = Vector2.One * (1f + 0.55f * Ease.OutCubic(t));
			ring.Modulate = new Color(1f, 1f, 1f, 1f - t);
			ring.Width = 3f * (1f - 0.6f * t);
		});
		Fx.FreeAfter(ring, 0.5);
	}

	// ── 能量 ──
	public static void ApplyEnergy(NEnergyCounter counter)
	{
		if (counter.GetNodeOrNull(DecoName) != null)
		{
			return;
		}
		Control? layers = counter.GetNodeOrNull<Control>("%Layers");
		Label? label = counter.GetNodeOrNull<Label>("Label");
		CanvasItem? vfxBack = counter.GetNodeOrNull<CanvasItem>("%EnergyVfxBack");
		CanvasItem? vfxFront = counter.GetNodeOrNull<CanvasItem>("%EnergyVfxFront");
		if (layers == null || label == null)
		{
			return;
		}
		Node2D deco = new() { Name = DecoName, Visible = false };
		counter.AddChild(deco);
		counter.MoveChild(deco, layers.GetIndex() + 1);
		const float r = 60f;
		Shadow(deco, r, new Vector2(6f, 8f));
		Node2D spin = new();
		deco.AddChild(spin);
		P3Ui.Arc(spin, Vector2.Zero, r + 12f, Mathf.DegToRad(150f), Mathf.DegToRad(380f), new Color(Pal.Cyan, 0.85f), 3f);
		for (int i = 0; i < 8; i++)
		{
			float a = Mathf.DegToRad(200f + i * 20f);
			Polygon2D d = Fx.Poly(spin, Circle(2.6f, 10), new Color(0.86f, 0.96f, 1f));
			d.Position = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r + 12f);
		}
		Node2D counterSpin = new();
		deco.AddChild(counterSpin);
		P3Ui.Arc(counterSpin, Vector2.Zero, r + 19f, Mathf.DegToRad(20f), Mathf.DegToRad(62f), new Color(Red, 0.9f), 2.5f).Material = null;
		Node2D disc = new();
		deco.AddChild(disc);
		Fx.Poly(disc, Circle(r), Colors.White);
		Fx.Poly(disc, Circle(r - 4f), Navy);
		Node2D ring = new();
		disc.AddChild(ring);
		Fx.Poly(disc, new[] { new Vector2(-34f, -22f), new Vector2(40f, -22f), new Vector2(32f, 24f), new Vector2(-42f, 24f) }, new Color(Ink, 0.92f));
		// 大数字的笔画中心对准斜切黑底的中心；"/上限"和大数字底边对齐，贴在它右下
		Label big = Number(disc, 62, Colors.White, 4, new Vector2(90f, 76f));
		Label max = Number(disc, 26, Pal.Cyan, 3, new Vector2(48f, 32f));
		void LayoutNumbers(int size)
		{
			big.LabelSettings.FontSize = size;
			// "大数字 + /上限" 整组的水平中心对准黑底中心（x = -1）
			float digitW = Typo.Barlow.GetStringSize(big.Text, HorizontalAlignment.Left, -1, size).X;
			float slashW = max.Text.Length > 0 ? Typo.Barlow.GetStringSize(max.Text, HorizontalAlignment.Left, -1, 26).X + 1f : 0f;
			float bx = -1f - slashW * 0.5f;
			PlaceInk(big, new Vector2(bx, 1f), 90f);
			float bottom = 1f + 0.35f * size;
			PlaceInk(max, new Vector2(bx + digitW * 0.5f + 1f, bottom - 0.35f * 26f), 48f, HorizontalAlignment.Left);
		}
		Tag(deco, new Vector2(-2f, -r - 12f), "ENERGY", 20, 24f);

		int builtMax = -1, shown = -1;
		float pulse = 0f, glow = 0f;
		bool skinned = false;
		Color hide = new(1f, 1f, 1f, 0f);

		void Skin(bool on)
		{
			if (on == skinned)
			{
				return;
			}
			skinned = on;
			deco.Visible = on;
			layers.Visible = !on;
			if (vfxBack != null) vfxBack.Visible = !on;
			if (vfxFront != null) vfxFront.Visible = !on;
			label.SelfModulate = on ? hide : Colors.White;
			shown = -1;
		}

		Drive(counter, deco, "hud energy", dt =>
		{
			Skin(Stage.MakotoCombat());
			if (!skinned)
			{
				return;
			}
			deco.Position = counter.Size * 0.5f;
			(int cur, int mx) = ParseEnergy(label.Text);
			int segs = Mathf.Clamp(mx, 1, 10);
			if (segs != builtMax)
			{
				builtMax = segs;
				Ring(ring, segs, r - 16f, r - 7f, 7f);
				shown = -1;
			}
			if (cur != shown)
			{
				if (shown >= 0 && cur > shown)
				{
					pulse = 1f;
					Burst(deco, r, Pal.Cyan);
				}
				shown = cur;
				big.Text = cur.ToString();
				big.LabelSettings.FontColor = cur == 0 ? Pal.Blood : Colors.White;
				max.Text = mx > 0 ? "/" + mx : "";
				LayoutNumbers(cur >= 10 ? 46 : 62);
				int lit = mx <= 10 ? Mathf.Min(cur, segs) : Mathf.RoundToInt(Mathf.Clamp(cur / (float)mx, 0f, 1f) * segs);
				int i = 0;
				foreach (Node c in ring.GetChildren())
				{
					if (c is Polygon2D p && !p.IsQueuedForDeletion())
					{
						p.Color = i < lit ? Pal.Cyan : Empty;
						i++;
					}
				}
			}
			pulse = Mathf.Max(0f, pulse - dt * 3f);
			// 超过上限（多出来的能量）时青色格子带一点呼吸亮度
			glow += dt;
			float over = cur > mx && mx > 0 ? 0.25f + 0.25f * Mathf.Sin(glow * 5f) : 0f;
			ring.Modulate = new Color(1f + over, 1f + over, 1f + over, 1f);
			disc.Scale = Vector2.One * (1f + 0.12f * pulse * pulse);
			float speed = cur == 0 ? 5f : 22f;
			spin.Rotation += Mathf.DegToRad(speed) * dt;
			counterSpin.Rotation -= Mathf.DegToRad(speed * 1.6f) * dt;
			float dim = cur == 0 ? 0.6f : 1f;
			deco.Modulate = new Color(dim, dim, dim + 0.04f, 1f);
		}, () => Skin(false));
	}

	// ── 辉星 ──
	public static void ApplyStars(NStarCounter counter)
	{
		Control? icon = counter.GetNodeOrNull<Control>("Icon");
		RichTextLabel? label = counter.GetNodeOrNull<RichTextLabel>("%CountLabel");
		CanvasItem? rotation = counter.GetNodeOrNull<CanvasItem>("%RotationLayers");
		if (icon == null || label == null || icon.GetNodeOrNull(DecoName) != null)
		{
			return;
		}
		Node2D deco = new() { Name = DecoName, Visible = false };
		icon.AddChild(deco);
		Node2D body = new();
		deco.AddChild(body);
		Node2D frame = new();
		body.AddChild(frame);
		Node2D badge = new();
		body.AddChild(badge);
		Label num = Number(body, 40, Colors.White, 3, new Vector2(70f, 50f));
		float builtS = -1f;
		int shown = -1;
		float pulse = 0f;
		bool skinned = false;
		Color hide = new(1f, 1f, 1f, 0f);

		Vector2[] Dia(float k) => new[] { new Vector2(0f, -k), new Vector2(k * 0.78f, 0f), new Vector2(0f, k), new Vector2(-k * 0.78f, 0f) };

		void Build(float s)
		{
			foreach (Node c in frame.GetChildren()) c.QueueFree();
			foreach (Node c in badge.GetChildren()) c.QueueFree();
			// 背后慢转的细线方框（代替原版的旋转层）
			Fx.Line(frame, new[] { new Vector2(-s, -s), new Vector2(s, -s), new Vector2(s, s), new Vector2(-s, s) }, new Color(Pal.Cyan, 0.55f), 2f, closed: true);
			Sprite2D sh = Shadow(badge, s * 0.8f, new Vector2(5f, 6f));
			Fx.Poly(badge, Dia(s), Colors.White);
			Fx.Poly(badge, Dia(s - 4f), new Color(0.047f, 0.118f, 0.36f));
			Fx.Line(badge, Dia(s - 11f), Pal.Cyan, 2f, closed: true, additive: false);
			Vector2 st = new(0f, -s + 13f);
			float a = 9f * s / 40f, b = 2.5f * s / 40f;
			Fx.Poly(badge, new[] { st + new Vector2(0f, -a), st + new Vector2(b, -b), st + new Vector2(a, 0f), st + new Vector2(b, b), st + new Vector2(0f, a), st + new Vector2(-b, b), st + new Vector2(-a, 0f), st + new Vector2(-b, -b) }, Colors.White);
			num.LabelSettings.FontSize = Mathf.RoundToInt(40f * s / 40f);
			PlaceInk(num, new Vector2(0f, s * 0.12f), 70f * s / 40f);   // 菱形中心略偏下（上面有四角星）
			builtS = s;
			_ = sh;
		}

		void Skin(bool on)
		{
			if (on == skinned)
			{
				return;
			}
			skinned = on;
			deco.Visible = on;
			icon.SelfModulate = on ? hide : Colors.White;
			if (rotation != null) rotation.Visible = !on;
			label.SelfModulate = on ? hide : Colors.White;
			shown = -1;
		}

		Drive(counter, deco, "hud stars", dt =>
		{
			Skin(Stage.MakotoCombat());
			if (!skinned)
			{
				return;
			}
			float s = Mathf.Clamp(Mathf.Min(icon.Size.X, icon.Size.Y) * 0.42f, 24f, 56f);
			if (!Mathf.IsEqualApprox(s, builtS))
			{
				Build(s);
			}
			deco.Position = icon.Size * 0.5f;
			int.TryParse(label.GetParsedText().Trim(), out int stars);
			if (stars != shown)
			{
				if (shown >= 0 && stars > shown)
				{
					pulse = 1f;
					Burst(deco, s * 0.9f, new Color(1f, 0.92f, 0.6f));
				}
				shown = stars;
				num.Text = stars.ToString();
				num.LabelSettings.FontColor = stars == 0 ? Pal.Blood : Colors.White;
			}
			pulse = Mathf.Max(0f, pulse - dt * 3f);
			body.Scale = Vector2.One * (1f + 0.15f * pulse);
			frame.Rotation += Mathf.DegToRad(stars == 0 ? 6f : 20f) * dt;
			float dim = stars == 0 ? 0.65f : 1f;
			deco.Modulate = new Color(dim, dim, dim, 1f);
		}, () => Skin(false));
	}

	// ── 抽牌堆 / 弃牌堆 / 消耗堆按钮 ──
	public static void ApplyPile(NCombatCardPile button)
	{
		Control? icon = button.GetNodeOrNull<Control>("Icon");
		Control? box = button.GetNodeOrNull<Control>("CountContainer");
		Label? count = button.GetNodeOrNull<Label>("CountContainer/Count");
		if (icon == null || box == null || count == null || icon.GetNodeOrNull(DecoName) != null || Back is not Texture2D back)
		{
			return;
		}
		string word = button is NDiscardPileButton ? "DISCARD" : button is NExhaustPileButton ? "EXHAUST" : "DRAW";
		bool mirror = button is not NDrawPileButton;
		Node2D deco = new() { Name = DecoName, Visible = false };
		icon.AddChild(deco);
		Node2D cards = new();
		deco.AddChild(cards);
		Label caption = Typo.Text(deco, word, Typo.Barlow, 17, Pal.Cyan, Vector2.Zero, HorizontalAlignment.Center, 3, Ink);
		caption.VerticalAlignment = VerticalAlignment.Center;
		caption.Size = new Vector2(120f, 22f);
		Node2D countHolder = new() { Name = DecoName, Visible = false };   // v0.14.71：默认藏着 —— 其他角色时 Skin(false) 直接返回，以前数字牌一直显示在原版牌堆上
		box.AddChild(countHolder);
		(Node2D tag, Label tagText) = Tag(countHolder, Vector2.Zero, "0", 30, 34f);
		Sprite2D[] backs = new Sprite2D[2];
		Sprite2D[] shades = new Sprite2D[2];
		for (int i = 0; i < 2; i++)
		{
			shades[i] = new Sprite2D { Texture = Fx.SoftDot, Modulate = new Color(0f, 0f, 0.04f, 0.55f) };
			cards.AddChild(shades[i]);
			backs[i] = new Sprite2D { Texture = back };
			cards.AddChild(backs[i]);
		}
		float builtH = -1f;
		string shownText = "";
		float tagW = -1f, t = 0f, shuffle = 0f;
		bool skinned = false;
		Color hide = new(1f, 1f, 1f, 0f);

		void Build(float h)
		{
			builtH = h;
			float sc = h / back.GetHeight();
			(float ang, Vector2 off)[] pose = { (-16f, new Vector2(-9f, 4f)), (-5f, Vector2.Zero) };
			for (int i = 0; i < 2; i++)
			{
				float a = mirror ? -pose[i].ang : pose[i].ang;
				Vector2 o = (mirror ? new Vector2(-pose[i].off.X, pose[i].off.Y) : pose[i].off) * h / 74f;
				backs[i].Scale = Vector2.One * sc;
				backs[i].RotationDegrees = a;
				backs[i].Position = o;
				shades[i].Scale = new Vector2(back.GetWidth() * sc * 1.5f, h * 1.3f) / Fx.SoftDot.GetWidth();
				shades[i].RotationDegrees = a;
				shades[i].Position = o + new Vector2(4f, 5f) * h / 74f;
			}
			caption.Position = new Vector2(-60f + (mirror ? 2f : -2f) * h / 74f, -h * 0.5f - 24f * h / 74f);
		}

		void Skin(bool on)
		{
			if (on == skinned)
			{
				return;
			}
			skinned = on;
			deco.Visible = on;
			countHolder.Visible = on;
			icon.SelfModulate = on ? hide : Colors.White;
			box.SelfModulate = on ? hide : Colors.White;
			foreach (Node c in box.GetChildren())
			{
				if (c is CanvasItem ci && c != countHolder)
				{
					ci.SelfModulate = on ? hide : Colors.White;
				}
			}
			shownText = "";
		}

		Drive(button, deco, "hud pile " + word, dt =>
		{
			Skin(Stage.MakotoCombat());
			if (!skinned)
			{
				return;
			}
			float h = Mathf.Clamp(icon.Size.Y * 0.9f, 48f, 110f);
			if (!Mathf.IsEqualApprox(h, builtH))
			{
				Build(h);
			}
			deco.Position = icon.Size * 0.5f;
			countHolder.Position = box.Size * 0.5f + new Vector2(mirror ? -4f : 4f, 2f);
			if (count.Text != shownText)
			{
				if (shownText != "")
				{
					shuffle = 1f;                         // 张数变了：上面那张牌轻轻弹一下
				}
				shownText = count.Text;
				tagText.Text = shownText;
				float w = Typo.Barlow.GetStringSize(shownText, HorizontalAlignment.Left, -1, 30).X + 34f * 0.9f;
				if (!Mathf.IsEqualApprox(w, tagW))
				{
					tagW = w;
					Rebuild(tag, w, 34f, true);
					PlaceTagText(tagText, w, 34f, true);
				}
				tag.Modulate = shownText == "0" ? new Color(0.62f, 0.66f, 0.75f, 1f) : Colors.White;
			}
			tag.Scale = count.Scale;                       // 原版加 / 减牌时数字的弹跳
			t += dt;
			shuffle = Mathf.Max(0f, shuffle - dt * 4f);
			float bob = Mathf.Sin(t * 1.6f) * 1.2f;
			backs[1].Position = new Vector2(backs[1].Position.X, bob - 6f * Mathf.Sin(shuffle * Mathf.Pi) * h / 74f);
			cards.Modulate = shownText == "0" ? new Color(0.6f, 0.62f, 0.7f, 1f) : Colors.White;
		}, () => { Skin(false); if (GodotObject.IsInstanceValid(countHolder)) countHolder.QueueFree(); });
	}
}
