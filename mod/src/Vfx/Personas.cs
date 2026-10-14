using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 静态立绘的 Persona（P3R 原版渲染图，art/p3r_tex/persona → p3makoto/persona/*.png）。
///
/// Orpheus（随机召唤的常客，对照 video2 0:05.4–0:08.4 的原版召唤）：
///   0.00  结城理身后先亮起一排绿色方格"音量条"（Orpheus 的音乐意象），随节拍一格格跳动
///   0.00  Orpheus 从上方翻滚着落下（先是竖琴那一侧入画），带两道残影；0.32 落定：白色剪影闪 + 水波 + 光点
///   0.32–1.0  悬浮呼吸，竖琴边缘飘出淡青色音符状光点；音量条随之起伏
///   1.0–1.5  上浮、变成青白色剪影后散成光点
///
/// Messiah（大爆炸 / 封印王座）：从光柱中缓缓降下，身后展开一圈金白色光翼。
/// 封印王座另有专门的演出 MessiahThrone（见下）。
/// 全部放在后层（人物身后），不挡人物和敌人。
/// </summary>
public static class Personas
{
	private static readonly Dictionary<string, Texture2D?> Cache = new();

	public static Texture2D? Tex(string name)
	{
		if (!Cache.TryGetValue(name, out Texture2D? t))
		{
			string path = $"res://p3makoto/persona/{name}.png";
			t = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
			Cache[name] = t;
		}
		return t;
	}

	private static readonly Color OrpheusGreen = new(0.35f, 1f, 0.6f);
	private static readonly Color Gold = new(1f, 0.88f, 0.55f);

	/// <summary>立绘 + 加色辉光 + 白色剪影闪三层。返回 (根, 本体, 辉光, 剪影)。</summary>
	private static (Node2D root, Sprite2D body, Sprite2D glow, Sprite2D flash) Figure(Control layer, Vector2 at, Texture2D tex, float height, Color glowTint)
	{
		Node2D root = Fx.Root(layer, at, "P3PersonaFigure");
		float s = height / tex.GetHeight();
		Sprite2D glow = new()
		{
			Texture = tex, Scale = Vector2.One * s * 1.05f, Modulate = new Color(glowTint, 0.45f),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		Sprite2D body = new() { Texture = tex, Scale = Vector2.One * s };
		Sprite2D flash = new()
		{
			Texture = tex, Scale = Vector2.One * s, Modulate = new Color(1f, 1f, 1f, 0f),
			Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
		};
		root.AddChild(glow);
		root.AddChild(body);
		root.AddChild(flash);
		return (root, body, glow, flash);
	}

	/// <summary>召唤器碎裂后 Orpheus 现身（不攻击的召唤）。</summary>
	public static bool Orpheus(NCreature caster)
	{
		Control? back = Makoto.FxLayer(back: true);
		if (back == null || Tex("orpheus") is not Texture2D tex)
		{
			return false;
		}
		Vector2 feet = Makoto.Feet(caster);
		Vector2 center = Makoto.Center(caster);
		float body = Mathf.Max(260f, (feet.Y - center.Y) * 2.1f);
		float height = body * 1.6f;
		Vector2 home = new(center.X - height * 0.26f, center.Y - height * 0.22f);

		EqualizerBars(back, home + new Vector2(0f, height * 0.1f), height, 1.5f);

		var (root, fig, glow, flash) = Figure(back, home, tex, height, Pal.Cyan);
		// 两道残影（下落时拖在上方）
		List<Sprite2D> trails = new();
		for (int i = 0; i < 2; i++)
		{
			Sprite2D tr = new()
			{
				Texture = tex, Scale = fig.Scale, Modulate = new Color(0.5f, 0.9f, 1f, 0f),
				Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }
			};
			root.AddChild(tr);
			root.MoveChild(tr, 0);
			trails.Add(tr);
		}
		Audio.Sfx.Clip("Snd_FX_Btl_Summon_00", 0.8f);

		bool landed = false;
		const float total = 1.5f;
		const float drop = 0.32f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float d = Mathf.Clamp(time / drop, 0f, 1f);
			float e = Ease.OutBack(d);
			// 从上方翻滚着落下：竖琴那一侧（画面右）先入画
			Vector2 fall = new(height * 0.08f * (1f - e), -height * 0.75f * (1f - e));
			float bob = time > drop ? 6f * Mathf.Sin((time - drop) * 3.2f) : 0f;
			float rise = Ease.InCubic(Mathf.Clamp((time - 1.0f) / 0.5f, 0f, 1f));
			root.Position = home + fall + new Vector2(0f, bob - height * 0.12f * rise);
			root.Rotation = 0.55f * (1f - Ease.OutCubic(d));
			for (int i = 0; i < trails.Count; i++)
			{
				float lag = (i + 1) * 0.06f;
				float dl = Mathf.Clamp((time - lag) / drop, 0f, 1f);
				float el = Ease.OutBack(dl);
				trails[i].Position = new Vector2(height * 0.08f * (1f - el), -height * 0.75f * (1f - el)) - (fall);
				trails[i].Rotation = 0.55f * (1f - Ease.OutCubic(dl)) - root.Rotation;
				trails[i].Modulate = new Color(0.5f, 0.9f, 1f, d < 1f ? 0.35f / (i + 1) : Mathf.Max(0f, 0.35f / (i + 1) * (1f - (time - drop) / 0.12f)));
			}
			// 落定白闪 → 消散时变成青白剪影
			float land = time < drop ? 0f : Mathf.Max(0f, 1f - (time - drop) / 0.25f);
			flash.Modulate = new Color(0.85f, 0.97f, 1f, Mathf.Max(land * 0.9f, rise * 0.8f));
			fig.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.06f, 0f, 1f) * (1f - rise));
			glow.Modulate = new Color(Pal.Cyan, (0.35f + 0.15f * Mathf.Sin(time * 5f)) * (1f - rise));
			flash.Modulate = new Color(flash.Modulate, flash.Modulate.A * (1f - Mathf.Clamp((time - 1.3f) / 0.2f, 0f, 1f)));
			if (!landed && time >= drop)
			{
				landed = true;
				Vector2 g = root.GlobalPosition;
				P3Fx.Ripple(back, new Vector2(g.X, feet.Y), 700f, 0.7f, 0.025f, squashY: 0.35f);
				P3Fx.Flash(back, g, Pal.Cyan, height * 0.7f, 0.28f);
				Motes(back, g, height, Pal.Ice, 14, 1.0f);
			}
		});
		Fx.After(root, 1.05, "orpheus dissolve", () => Motes(back, root.GlobalPosition, height, Pal.Cyan, 22, 0.9f, up: true));
		Fx.FreeAfter(root, total + 0.05);
		return true;
	}

	/// <summary>Orpheus 身后的绿色方格音量条：一列列从下往上点亮，随节拍起伏，最后整体淡出。</summary>
	private static void EqualizerBars(Control back, Vector2 at, float height, float total)
	{
		Node2D root = Fx.Root(back, at, "P3OrpheusBars");
		const int cols = 11;
		const int rows = 9;
		float cell = height * 0.055f;
		float gap = cell * 0.28f;
		float w = cols * (cell + gap);
		List<(Polygon2D p, int col, int row)> cells = new();
		for (int c = 0; c < cols; c++)
		{
			for (int r = 0; r < rows; r++)
			{
				Vector2 o = new(-w / 2f + c * (cell + gap), -r * (cell + gap));
				Polygon2D q = Fx.Poly(root, new[] { o, o + new Vector2(cell, 0f), o + new Vector2(cell, -cell), o + new Vector2(0f, -cell) }, OrpheusGreen, additive: true);
				q.Modulate = new Color(1f, 1f, 1f, 0f);
				cells.Add((q, c, r));
			}
		}
		float[] phase = new float[cols];
		for (int c = 0; c < cols; c++)
		{
			phase[c] = Fx.Rand(0f, Mathf.Tau);
		}
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float env = Mathf.Clamp(time / 0.2f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.05f) / 0.4f, 0f, 1f));
			foreach ((Polygon2D p, int col, int row) in cells)
			{
				// 中间列高、两边低，跟着"节拍"跳
				float shape = 1f - Mathf.Abs(col - (cols - 1) / 2f) / ((cols - 1) / 2f) * 0.6f;
				float beat = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(time * 7f + phase[col]));
				float level = rows * shape * beat * Mathf.Clamp((time - col * 0.012f) / 0.25f, 0f, 1f);
				float on = row < level ? 1f : Mathf.Clamp(level - row + 1f, 0f, 1f) * 0.4f;
				float top = row >= level - 1f && row < level ? 1.4f : 1f;   // 最上一格更亮
				p.Modulate = new Color(top, top, top, on * env * (0.22f + 0.05f * row / rows));
			}
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	/// <summary>一团上飘 / 四散的光点。</summary>
	private static void Motes(Control layer, Vector2 at, float height, Color tint, int count, float life, bool up = false)
	{
		Node2D root = Fx.Root(layer, at, "P3PersonaMotes");
		List<(Polygon2D p, Vector2 o, Vector2 v)> dots = new();
		for (int i = 0; i < count; i++)
		{
			float r = Fx.Rand(2.5f, 6f);
			Polygon2D d = Fx.Poly(root, Fx.Star4(r * 2f, 0.3f), tint, additive: true);
			Vector2 o = new(Fx.Rand(-height * 0.2f, height * 0.2f), Fx.Rand(-height * 0.35f, height * 0.35f));
			d.Position = o;
			Vector2 v = up ? new Vector2(Fx.Rand(-30f, 30f), Fx.Rand(-260f, -120f)) : o.Normalized() * Fx.Rand(80f, 220f);
			dots.Add((d, o, v));
		}
		Fx.Animate(root, life, t =>
		{
			foreach ((Polygon2D p, Vector2 o, Vector2 v) in dots)
			{
				p.Position = o + v * life * Ease.OutCubic(t);
				p.Modulate = new Color(1f, 1f, 1f, (1f - Ease.InCubic(t)) * (0.6f + 0.4f * Mathf.Sin(t * 20f + o.X)));
				p.Rotation = t * 3f;
			}
		});
		Fx.FreeAfter(root, life + 0.05);
	}

	/// <summary>光翼：一圈细长的金白色光刃从身后扇形展开（Messiah 用）。</summary>
	private static Node2D Wings(Node2D parent, float height, Color tint)
	{
		Node2D wings = new();
		parent.AddChild(wings);
		wings.MoveChild(wings, 0);
		for (int side = -1; side <= 1; side += 2)
		{
			for (int i = 0; i < 6; i++)
			{
				float a = side * (0.35f + i * 0.2f) - Mathf.Pi / 2f;
				Vector2 d = Vector2.Right.Rotated(a);
				float len = height * (0.62f - i * 0.05f);
				float wid = height * 0.026f;
				Vector2 n = d.Orthogonal();
				Fx.Poly(wings, new[] { d * height * 0.08f + n * wid, d * len, d * height * 0.08f - n * wid }, new Color(tint, 0.55f - i * 0.05f), additive: true);
				Fx.Poly(wings, new[] { d * height * 0.08f + n * wid * 0.35f, d * len * 0.94f, d * height * 0.08f - n * wid * 0.35f }, new Color(1f, 1f, 1f, 0.7f), additive: true);
			}
		}
		return wings;
	}

	/// <summary>Messiah 现身（大爆炸）：光柱中缓缓降下，金白光翼展开，悬停后升入光中。</summary>
	public static bool Messiah(NCreature caster)
	{
		Control? back = Makoto.FxLayer(back: true);
		if (back == null || Tex("messiah") is not Texture2D tex)
		{
			return false;
		}
		Vector2 feet = Makoto.Feet(caster);
		Vector2 center = Makoto.Center(caster);
		float body = Mathf.Max(260f, (feet.Y - center.Y) * 2.1f);
		float height = body * 1.7f;
		Vector2 home = new(center.X - height * 0.22f, center.Y - height * 0.25f);
		P3Fx.LightPillar(back, new Vector2(home.X, feet.Y), Gold, height * 0.4f, height * 1.4f, 1.3f);
		var (root, fig, glow, flash) = Figure(back, home, tex, height, Gold);
		Node2D wings = Wings(root, height, Gold);
		Audio.Sfx.Clip("Snd_FX_Btl_Summon_00", 0.8f);
		const float total = 1.6f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float inn = Ease.OutCubic(Mathf.Clamp(time / 0.5f, 0f, 1f));
			float rise = Ease.InCubic(Mathf.Clamp((time - 1.15f) / 0.45f, 0f, 1f));
			root.Position = home + new Vector2(0f, -height * 0.18f * (1f - inn) - height * 0.15f * rise + 5f * Mathf.Sin(time * 2.6f));
			fig.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.3f, 0f, 1f) * (1f - rise));
			flash.Modulate = new Color(1f, 0.97f, 0.9f, Mathf.Max(1f - Mathf.Clamp(time / 0.45f, 0f, 1f), rise) * 0.85f * (1f - Mathf.Clamp((time - 1.45f) / 0.15f, 0f, 1f)));
			glow.Modulate = new Color(Gold, (0.35f + 0.15f * Mathf.Sin(time * 4f)) * (1f - rise));
			float w = Ease.OutBack(Mathf.Clamp((time - 0.15f) / 0.45f, 0f, 1f));
			wings.Scale = new Vector2(w, w);
			wings.Modulate = new Color(1f, 1f, 1f, (0.75f + 0.25f * Mathf.Sin(time * 6f)) * (1f - rise));
		});
		Fx.After(root, 0.5, "messiah settle", () => Motes(back, root.GlobalPosition, height, Gold, 16, 1.0f));
		Fx.After(root, 1.15, "messiah ascend", () => Motes(back, root.GlobalPosition, height, Pal.Ice, 24, 0.9f, up: true));
		Fx.FreeAfter(root, total + 0.05);
		return true;
	}

	/// <summary>
	/// 封印王座 —— 弥赛亚降临（v0.14.2）。约 2.6 秒，全在后层：
	///   0.00  头顶上方一枚金色「审判 XX」大法阵由一笔弧线画成：外环 → 12 道刻度 → 内环 → 中央 XX
	///   0.20  Messiah 从法阵中心缓缓降下，先是一团白光剪影，0.6 时显出本色；身后十二道光翼依次展开
	///   0.30  金色羽状光屑从上方缓缓飘落（整个演出期间）
	///   1.10  「封印」：Messiah 前方的法阵收缩成一枚小印，沿一道金白色光流落进结城理脑后 ——
	///         正是之后常驻的那枚 XX 印记（ThroneSeal）出现的位置；落点闪光 + 水波 + 金色冲击环
	///   1.40  Messiah 合拢光翼，上升融进光里；法阵碎成金色光点
	/// 音效：召唤 + Nyx 战「封印」原声的高潮段（messiah_seal，封印那一下正好是高潮）。
	/// </summary>
	public static void MessiahThrone(NCreature caster)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null || Tex("messiah") is not Texture2D tex)
		{
			return;
		}
		Vector2 feet = Makoto.Feet(caster);
		Vector2 center = Makoto.Center(caster);
		Vector2 head = Makoto.Head(caster);
		float body = Mathf.Max(260f, (feet.Y - center.Y) * 2.1f);
		float height = body * 1.85f;
		Vector2 home = new(center.X - height * 0.2f, center.Y - height * 0.3f);
		Vector2 circleAt = home + new Vector2(0f, -height * 0.18f);
		float R = height * 0.42f;

		Audio.Sfx.Clip("Snd_FX_Btl_Summon_00", 0.8f);
		Audio.Sfx.Clip("messiah_seal", 0.95f, 0.1f);

		// ── 审判法阵 ──
		Node2D circle = Fx.Root(back, circleAt, "P3MessiahCircle");
		Line2D outer = Fx.Line(circle, new[] { Vector2.Zero }, new Color(Gold, 0.85f), 4f);
		Line2D inner = Fx.Line(circle, new[] { Vector2.Zero }, new Color(1f, 1f, 1f, 0.6f), 2f);
		Node2D ticks = new();
		circle.AddChild(ticks);
		for (int k = 0; k < 12; k++)
		{
			Vector2 d = Vector2.Right.Rotated(k * Mathf.Tau / 12f - Mathf.Pi / 2f);
			Polygon2D tk = Fx.Poly(ticks, new[] { d * R * 0.86f + d.Orthogonal() * 4f, d * R * 1.08f, d * R * 0.86f - d.Orthogonal() * 4f }, new Color(Gold, 0.9f), additive: true);
			tk.Modulate = new Color(1f, 1f, 1f, 0f);
		}
		Node2D numeral = new() { Modulate = new Color(1f, 1f, 1f, 0f) };
		circle.AddChild(numeral);
		Typo.Text(numeral, "XX", Typo.LatinItalic, (int)(R * 0.42f), new Color(1f, 0.95f, 0.8f), new Vector2(-R * 0.24f, -R * 0.27f), outline: 3, outlineColor: new Color(0.45f, 0.3f, 0.05f));

		// ── Messiah ──
		var (root, fig, glow, flash) = Figure(back, home, tex, height, Gold);
		Node2D wings = Wings(root, height * 1.15f, Gold);
		wings.Scale = Vector2.Zero;

		// ── 飘落的光羽 ──
		Node2D feathers = Fx.Root(back, circleAt, "P3MessiahFeathers");
		List<(Polygon2D p, Vector2 o, float sway, float fall, float delay)> fs = new();
		for (int i = 0; i < 26; i++)
		{
			float len = Fx.Rand(10f, 22f);
			Polygon2D f = Fx.Poly(feathers, new[] { new Vector2(0f, -len), new Vector2(len * 0.22f, 0f), new Vector2(0f, len * 0.6f), new Vector2(-len * 0.22f, 0f) }, new Color(1f, 0.95f, 0.8f, 0.8f), additive: true);
			f.Modulate = new Color(1f, 1f, 1f, 0f);
			fs.Add((f, new Vector2(Fx.Rand(-R * 1.4f, R * 1.4f), Fx.Rand(-R * 1.2f, -R * 0.2f)), Fx.Rand(12f, 34f), Fx.Rand(90f, 180f), Fx.Rand(0.3f, 1.4f)));
		}

		const float total = 2.6f;
		const float sealAt = 1.1f;
		bool sealed_ = false;
		Fx.Animate(circle, total, t =>
		{
			float time = t * total;
			// 法阵一笔画成
			float draw = Ease.InOutCubic(Mathf.Clamp(time / 0.45f, 0f, 1f));
			outer.Points = Arc(R, draw);
			inner.Points = Arc(R * 0.72f, Ease.InOutCubic(Mathf.Clamp((time - 0.15f) / 0.4f, 0f, 1f)), reverse: true);
			for (int k = 0; k < ticks.GetChildCount(); k++)
			{
				float on = Mathf.Clamp((time - 0.12f - k * 0.025f) / 0.1f, 0f, 1f);
				((Node2D)ticks.GetChild(k)).Modulate = new Color(1f, 1f, 1f, on);
			}
			numeral.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.45f) / 0.15f, 0f, 1f));
			circle.Rotation = 0.15f * time;
			numeral.Rotation = -circle.Rotation;
			// 封印：法阵收缩 → 落进结城理脑后
			float shrink = Ease.InCubic(Mathf.Clamp((time - sealAt + 0.25f) / 0.25f, 0f, 1f));
			circle.Scale = Vector2.One * Mathf.Lerp(1f, 66f / R, shrink);   // 收到和常驻印记（ThroneSeal，半径 66）一样大，无缝交接
			Vector2 sealTo = head + new Vector2(-28f, -36f);
			float fly = Ease.InCubic(Mathf.Clamp((time - sealAt + 0.1f) / 0.1f, 0f, 1f));
			circle.GlobalPosition = circleAt.Lerp(sealTo, fly);
			circle.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - sealAt) / 0.08f, 0f, 1f));
			if (!sealed_ && time >= sealAt)
			{
				sealed_ = true;
				Seal(back, front, circleAt, sealTo, height);
				ThroneSeal.Release(caster);
			}
		});
		// 离场（v0.14.8，用户：出去的动画不完善）：
		//   1.45 合拢光翼、身体先往下一沉（蓄势）→ 1.55 一道金色光柱从天而降罩住它 →
		//   1.60 加速升空：身体被拉长（纵向拉伸、横向收窄）、拖出三道金白残影、整个身影渐渐变成白光剪影 →
		//   1.75 羽毛从身上向四周炸开、金色光点上飘 → 2.15 头顶出画处一颗十字星闪一下收尾
		const float exitAt = 1.45f;
		float Rise(float tt) => Ease.InCubic(Mathf.Clamp((tt - exitAt - 0.15f) / 0.55f, 0f, 1f));
		float Dip(float tt) => tt > exitAt && tt < exitAt + 0.15f ? Mathf.Sin((tt - exitAt) / 0.15f * Mathf.Pi) : 0f;
		Vector2 PosAt(float tt)
		{
			float inn = Ease.OutCubic(Mathf.Clamp((tt - 0.2f) / 0.6f, 0f, 1f));
			return home + new Vector2(0f, -height * 0.25f * (1f - inn) + height * 0.03f * Dip(tt) - height * 1.1f * Rise(tt) + 5f * Mathf.Sin(tt * 2.4f));
		}
		Node2D trail = Fx.Root(back, home, "P3MessiahTrail");
		back.MoveChild(trail, root.GetIndex());
		List<Sprite2D> ghosts = new();
		for (int i = 0; i < 3; i++)
		{
			Sprite2D g = new() { Texture = tex, Scale = fig.Scale, Material = Fx.Additive, Modulate = new Color(1f, 0.92f, 0.7f, 0f) };
			trail.AddChild(g);
			ghosts.Add(g);
		}
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float rise = Rise(time);
			root.GlobalPosition = PosAt(time);
			root.Scale = new Vector2(1f - 0.15f * rise, 1f + 0.35f * rise);
			float vis = Mathf.Clamp((time - 0.2f) / 0.15f, 0f, 1f);
			float gone = Mathf.Clamp((time - exitAt - 0.55f) / 0.25f, 0f, 1f);
			fig.Modulate = new Color(1f, 1f, 1f, vis * Mathf.Clamp((time - 0.35f) / 0.3f, 0f, 1f) * (1f - Mathf.Clamp((time - exitAt - 0.3f) / 0.35f, 0f, 1f)));
			float white = vis * (1f - Mathf.Clamp((time - 0.4f) / 0.35f, 0f, 1f));
			float pulse = time >= sealAt ? Mathf.Max(0f, 1f - (time - sealAt) / 0.3f) * 0.6f : 0f;
			float lift = Mathf.Clamp((time - exitAt - 0.1f) / 0.35f, 0f, 1f);
			flash.Modulate = new Color(1f, 0.97f, 0.88f, Mathf.Max(Mathf.Max(white, pulse), lift) * (1f - gone));
			glow.Modulate = new Color(Gold, (0.35f + 0.2f * Mathf.Sin(time * 4f) + pulse + 0.4f * lift) * vis * (1f - gone));
			// 光翼：依次展开 → 封印时猛张一下 → 离场时向上合拢成一束
			float open = Ease.OutBack(Mathf.Clamp((time - 0.35f) / 0.5f, 0f, 1f));
			float flare = time >= sealAt ? 0.12f * Mathf.Max(0f, 1f - (time - sealAt) / 0.35f) : 0f;
			float close = Ease.InOutCubic(Mathf.Clamp((time - exitAt) / 0.3f, 0f, 1f));
			wings.Scale = new Vector2((open + flare) * (1f - 0.85f * close), (open + flare) * (1f + 0.25f * close));
			wings.Modulate = new Color(1f, 1f, 1f, (0.7f + 0.3f * Mathf.Sin(time * 5f)) * (1f - gone));
			// 残影：沿上升轨迹落后 0.05 / 0.1 / 0.15s
			for (int i = 0; i < ghosts.Count; i++)
			{
				float lagT = time - 0.05f * (i + 1);
				ghosts[i].GlobalPosition = PosAt(lagT);
				ghosts[i].Scale = fig.Scale * new Vector2(1f - 0.15f * Rise(lagT), 1f + 0.35f * Rise(lagT));
				float on = rise > 0.02f ? (0.45f - 0.12f * i) * (1f - gone) : 0f;
				ghosts[i].Modulate = new Color(1f, 0.92f, 0.7f, on);
			}
		});
		Fx.After(root, exitAt + 0.1, "messiah exit pillar", () =>
			P3Fx.LightPillar(back, home + new Vector2(0f, height * 0.5f), Gold, height * 0.5f, height * 2.4f, 0.8f));
		Fx.After(root, exitAt + 0.3, "messiah exit feathers", () => FeatherBurst(back, PosAt(exitAt + 0.3f), height));
		Fx.After(root, exitAt + 0.7, "messiah exit star", () =>
		{
			Vector2 topAt = PosAt(exitAt + 0.7f) - new Vector2(0f, height * 0.2f);
			P3Fx.Flash(back, topAt, Gold, height * 0.5f, 0.3f);
			Node2D st = Fx.Root(back, topAt, "P3MessiahExitStar");
			Fx.Poly(st, Fx.Star4(height * 0.12f, 0.12f), Pal.White, additive: true);
			Fx.Animate(st, 0.4f, t =>
			{
				st.Scale = Vector2.One * (t < 0.3f ? Ease.OutBack(t / 0.3f) : 1f - Ease.InCubic((t - 0.3f) / 0.7f));
				st.Rotation = t * 0.8f;
			});
			Fx.FreeAfter(st, 0.42);
		});
		Fx.FreeAfter(trail, total + 0.05);
		Fx.Animate(feathers, total, t =>
		{
			float time = t * total;
			foreach ((Polygon2D p, Vector2 o, float sway, float fall, float delay) in fs)
			{
				float lt = time - delay;
				if (lt < 0f)
				{
					continue;
				}
				p.Position = o + new Vector2(sway * Mathf.Sin(lt * 2.2f + o.X), fall * lt);
				p.Rotation = 0.4f * Mathf.Sin(lt * 2.2f + o.X);
				p.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(lt / 0.2f, 0f, 1f) * (1f - Mathf.Clamp((lt - 0.7f) / 0.4f, 0f, 1f)));
			}
		});
		Fx.After(root, 0.8, "messiah motes", () => Motes(back, root.GlobalPosition, height, Gold, 18, 1.0f));
		Fx.After(root, 1.6, "messiah ascend", () => Motes(back, root.GlobalPosition, height, Gold, 30, 1.0f, up: true));
		Fx.FreeAfter(root, total + 0.05);
		Fx.FreeAfter(circle, total + 0.05);
		Fx.FreeAfter(feathers, total + 0.05);
	}

	/// <summary>离场时羽毛从身上向四周炸开：先快后慢、边飞边翻转、往下飘落着淡出。</summary>
	private static void FeatherBurst(Control layer, Vector2 at, float height)
	{
		Node2D root = Fx.Root(layer, at, "P3MessiahFeatherBurst");
		List<(Polygon2D p, Vector2 o, Vector2 v, float spin)> fs = new();
		for (int i = 0; i < 22; i++)
		{
			float len = Fx.Rand(12f, 26f);
			Polygon2D f = Fx.Poly(root, new[] { new Vector2(0f, -len), new Vector2(len * 0.22f, 0f), new Vector2(0f, len * 0.6f), new Vector2(-len * 0.22f, 0f) }, new Color(1f, 0.95f, 0.82f, 0.9f), additive: true);
			f.Position = new Vector2(Fx.Rand(-height * 0.12f, height * 0.12f), Fx.Rand(-height * 0.2f, height * 0.2f));
			Vector2 v = (f.Position.Normalized() + Fx.RandDir() * 0.4f).Normalized() * Fx.Rand(180f, 420f);
			fs.Add((f, f.Position, v, Fx.Rand(-6f, 6f)));
		}
		Fx.Animate(root, 1.1f, t =>
		{
			float time = t * 1.1f;
			foreach ((Polygon2D p, Vector2 o, Vector2 v, float spin) in fs)
			{
				p.Position = o + v * 0.45f * Ease.OutCubic(t) + new Vector2(0f, 60f * time);
				p.Rotation = spin * time;
				p.Scale = new Vector2(Mathf.Abs(Mathf.Cos(time * 5f + spin)), 1f);
				p.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((time - 0.5f) / 0.6f, 0f, 1f)));
			}
		});
		Fx.FreeAfter(root, 1.15);
	}

	/// <summary>法阵化成的小印沿光流落进结城理脑后：金白光流 + 落点闪光、水波、冲击环。</summary>
	private static void Seal(Control back, Control front, Vector2 from, Vector2 to, float height)
	{
		Node2D streak = Fx.Root(back, Vector2.Zero, "P3MessiahSealStreak");
		Line2D line = Fx.Line(streak, new[] { from, from }, new Color(1f, 0.93f, 0.7f, 0.9f), 10f);
		Line2D core = Fx.Line(streak, new[] { from, from }, new Color(1f, 1f, 1f, 1f), 3f);
		Fx.Animate(streak, 0.4f, t =>
		{
			float head = Ease.OutExpo(Mathf.Clamp(t / 0.3f, 0f, 1f));
			float tail = Ease.InCubic(t);
			Vector2 a = from.Lerp(to, tail);
			Vector2 b = from.Lerp(to, head);
			line.Points = new[] { a, b };
			core.Points = new[] { a, b };
			streak.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
		});
		Fx.FreeAfter(streak, 0.45);
		P3Fx.Flash(back, to, Gold, height * 0.45f, 0.3f, 0.06f);
		P3Fx.Shockwave(back, to, Gold, height * 0.6f, 0.5f, delay: 0.06f);
		P3Fx.Ripple(back, to, 600f, 0.6f, 0.025f, delay: 0.06f);
		Motes(front, to, height * 0.3f, Gold, 12, 0.7f);
	}

	private static Vector2[] Arc(float r, float frac, bool reverse = false)
	{
		int n = Mathf.Max(2, (int)(96 * frac) + 1);
		Vector2[] pts = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			float a = -Mathf.Pi / 2f + (reverse ? -1f : 1f) * Mathf.Tau * frac * i / (n - 1);
			pts[i] = Vector2.Right.Rotated(a) * r;
		}
		return pts;
	}
}
