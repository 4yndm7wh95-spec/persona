using Godot;

namespace P3Makoto.Vfx;

/// <summary>技能、能力、资源类特效。</summary>
public static class Extras
{
	/// <summary>
	/// 六边形蜂巢护盾（Tetrakarn 风格）：在角色前方的椭圆区域内，六边形从中心向外波浪式点亮，
	/// 停留片刻后自外向内熄灭。reflect = true 时为 Makarakarn 的紫粉色。
	/// </summary>
	public static void HexShield(Control layer, Vector2 center, float scale = 1f, bool reflect = false, float hold = 0.45f)
	{
		Color tint = reflect ? new Color("e07bff") : Pal.Cyan;
		Node2D root = Fx.Root(layer, center + new Vector2(95f * scale, 0f), "P3HexShield");
		float r = 24f * scale;
		float w = Mathf.Sqrt(3f) * r;
		float total = 0.35f + hold + 0.35f;
		for (int q = -4; q <= 4; q++)
		{
			for (int row = -6; row <= 6; row++)
			{
				Vector2 p = new Vector2(q * w + (row % 2 != 0 ? w / 2f : 0f), row * r * 1.5f);
				// 椭圆范围：竖长，模拟曲面护盾的正面投影
				float ex = p.X / (95f * scale);
				float ey = p.Y / (175f * scale);
				float d = ex * ex + ey * ey;
				if (d > 1f)
				{
					continue;
				}
				Node2D hex = new Node2D { Position = new Vector2(p.X * (1f - 0.25f * ey * ey), p.Y) };
				root.AddChild(hex);
				Fx.Poly(hex, Fx.Hexagon(r * 0.92f), new Color(tint, 0.12f + 0.12f * (1f - d)), additive: true);
				Fx.Line(hex, Fx.Hexagon(r * 0.92f), new Color(Pal.Ice, 0.8f), 1.4f * scale, closed: true);
				float dist = Mathf.Sqrt(d);
				float on = 0.35f * dist;
				float off = 0.35f + hold + 0.35f * (1f - dist);
				hex.Scale = Vector2.Zero;
				Fx.Animate(hex, total, t =>
				{
					float time = t * total;
					float appear = Ease.OutBack(Mathf.Clamp((time - on) / 0.2f, 0f, 1f));
					float vanish = Ease.InCubic(Mathf.Clamp((time - off) / 0.2f, 0f, 1f));
					hex.Scale = Vector2.One * appear * (1f - vanish * 0.5f);
					float flash = Mathf.Max(0f, 1f - Mathf.Abs(time - on - 0.1f) * 8f);
					hex.Modulate = new Color(1f + flash, 1f + flash, 1f + flash, 1f - vanish);
				});
			}
		}
		P3Fx.Ripple(layer, center + new Vector2(95f * scale, 0f), 420f * scale, 0.6f, 0.025f);
		P3Fx.Flash(layer, center + new Vector2(95f * scale, 0f), tint, 300f * scale, 0.25f);
		Fx.FreeAfter(root, total + 0.1);
	}

	/// <summary>获得辉星：四角星从身边浮现，旋转上升并闪烁。</summary>
	public static void StarGain(Control layer, Vector2 head, int count = 3)
	{
		count = Mathf.Clamp(count, 1, 8);
		for (int i = 0; i < count; i++)
		{
			Vector2 from = head + new Vector2(Fx.Rand(-90f, 90f), Fx.Rand(40f, 160f));
			Vector2 to = from + new Vector2(Fx.Rand(-30f, 30f), -Fx.Rand(140f, 220f));
			float delay = i * 0.07f;
			Fx.After(layer, delay, "star gain", () =>
			{
				Node2D star = Fx.Root(layer, from, "P3StarGain");
				Fx.Poly(star, Fx.Star4(22f), Pal.White, additive: true);
				Fx.Poly(star, Fx.Star4(40f, 0.1f), new Color(Pal.Cyan, 0.6f), additive: true);
				Sprite2D glow = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0.5f), Scale = Vector2.One * 2.2f };
				star.AddChild(glow);
				Fx.Animate(star, 1.0f, t =>
				{
					star.GlobalPosition = from.Lerp(to, Ease.OutCubic(t));
					float pop = Ease.OutBack(Mathf.Min(1f, t * 4f));
					float twinkle = 0.8f + 0.2f * Mathf.Sin(t * 30f);
					star.Scale = Vector2.One * pop * twinkle * Mathf.Lerp(1f, 0.6f, t);
					star.Rotation = t * 2.5f;
					star.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
				});
				Fx.FreeAfter(star, 1.1);
			});
		}
	}

	/// <summary>
	/// 轻施法收手时的掌心余光：从伸出的掌心（按原画比例估算：脚底往右 0.37h、往上 0.9h，h = 脚到头的高度）
	/// 沿一道向外下垂的弧线落到腰侧口袋（右 0.23h、上 0.6h），0.45s；一团淡青柔光 + 白色小核 + 一路掉下的光点，落地后散掉。
	/// </summary>
	public static void PalmAfterglow(Control layer, MegaCrit.Sts2.Core.Nodes.Combat.NCreature caster)
	{
		Vector2 feet = P3Makoto.Core.Makoto.Feet(caster), head = P3Makoto.Core.Makoto.Head(caster);
		float h = Mathf.Max(feet.Y - head.Y, 120f);
		Vector2 from = feet + P3Makoto.Core.Makoto.Offset(caster, new Vector2(0.37f, -0.9f) * h);
		Vector2 to = feet + P3Makoto.Core.Makoto.Offset(caster, new Vector2(0.23f, -0.6f) * h);
		Vector2 ctrl = from + P3Makoto.Core.Makoto.Offset(caster, new Vector2(0.1f * h, 0.2f * h));
		Node2D root = Fx.Root(layer, from, "P3PalmAfterglow");
		root.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0.45f), Scale = Vector2.One * 1.6f });
		root.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.White, 0.6f), Scale = Vector2.One * 0.45f });
		GpuParticles2D motes = Fx.Sparks(root, Pal.Ice, 14, 10f, 40f, 0.5f, gravity: 40f, scaleMin: 0.06f, scaleMax: 0.16f,
			ramp: new[] { new Color(Pal.White, 0.8f), new Color(Pal.Cyan, 0.5f), new Color(Pal.Cyan, 0f) }, oneShot: false);
		motes.LocalCoords = false;                        // 光点留在原地，拖出一条轨迹
		const float dur = 0.45f;
		Fx.Animate(root, dur + 0.25f, t =>
		{
			float time = t * (dur + 0.25f);
			float e = Ease.InOutCubic(Mathf.Clamp(time / dur, 0f, 1f));
			float k = 1f - e;
			root.GlobalPosition = from * (k * k) + ctrl * (2f * k * e) + to * (e * e);
			float fade = 1f - Mathf.Clamp((time - dur + 0.1f) / 0.25f, 0f, 1f);
			root.Modulate = new Color(1f, 1f, 1f, fade * Mathf.Clamp(time / 0.05f, 0f, 1f));
			root.Scale = Vector2.One * (1f - 0.4f * e);
			motes.Emitting = time < dur;
		});
		Fx.FreeAfter(root, dur + 0.8f);
	}

	/// <summary>
	/// 塔罗牌：从角色身后翻转出现（宽度 0→1），上浮发光，停留后碎成玻璃。用于能力牌。
	/// </summary>
	public static void TarotFlip(Control layer, Vector2 at, Color tint, float hold = 0.35f)
	{
		Audio.Sfx.Play(Audio.Snd.TarotReveal, 0.6f);
		Node2D root = Fx.Root(layer, at, "P3Tarot");
		Node2D card = new Node2D();
		root.AddChild(card);
		Vector2 half = new Vector2(62f, 100f);
		Vector2[] rect = { new(-half.X, -half.Y), new(half.X, -half.Y), new(half.X, half.Y), new(-half.X, half.Y) };
		Fx.Poly(card, rect, new Color(Pal.Abyss, 0.92f));
		Fx.Line(card, rect, tint, 3f, closed: true);
		Vector2[] inner = { new(-half.X + 10, -half.Y + 10), new(half.X - 10, -half.Y + 10), new(half.X - 10, half.Y - 10), new(-half.X + 10, half.Y - 10) };
		Fx.Line(card, inner, new Color(Pal.Ice, 0.7f), 1.2f, closed: true);
		// 牌面：圆环 + 十字 + 中心星，简化的阿尔卡纳纹样
		Fx.Line(card, Fx.Circle(34f, 32), tint, 2f, closed: true);
		Fx.Line(card, new[] { new Vector2(0f, -58f), new Vector2(0f, 58f) }, new Color(tint, 0.8f), 1.5f);
		Fx.Line(card, new[] { new Vector2(-40f, 0f), new Vector2(40f, 0f) }, new Color(tint, 0.8f), 1.5f);
		Fx.Poly(card, Fx.Star4(16f), Pal.White, additive: true);
		Sprite2D glow = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(tint, 0.55f), Scale = new Vector2(6f, 8f) };
		root.AddChild(glow);
		root.MoveChild(glow, 0);

		float total = 0.35f + hold;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float flip = Ease.OutExpo(Mathf.Min(1f, time / 0.35f));
			card.Scale = new Vector2(Mathf.Cos((1f - flip) * Mathf.Pi * 0.5f), 1f) * Mathf.Lerp(0.8f, 1f, flip);
			root.Position = new Vector2(0f, -40f * Ease.OutCubic(t));
			glow.Modulate = new Color(tint, 0.35f + 0.25f * Mathf.Sin(time * 12f));
		});
		Fx.After(layer, total, "tarot shatter", () =>
		{
			Vector2 p = root.GlobalPosition;
			root.QueueFree();
			P3Fx.Flash(layer, p, Pal.Ice, 320f, 0.2f);
			P3Fx.GlassShatter(layer, p, 26, 0.9f, tint.Lightened(0.4f));
			P3Fx.Ripple(layer, p, 480f, 0.6f, 0.03f);
		});
	}

	/// <summary>增益（箭头上升，Sukukaja 风格）或减益（箭头下压，Tarunda 风格）。</summary>
	public static void BuffArrows(Control layer, Vector2 center, Vector2 feet, bool up, Color? tint = null)
	{
		Color c = tint ?? (up ? new Color("ffb347") : new Color("ff4060"));
		Node2D root = Fx.Root(layer, feet, "P3Buff");
		for (int i = 0; i < 3; i++)
		{
			Node2D ring = new Node2D();
			root.AddChild(ring);
			Fx.Line(ring, Fx.Circle(110f, 40, 0.3f), new Color(c, 0.85f), 2.5f, closed: true);
			for (int k = 0; k < 4; k++)
			{
				float x = (k - 1.5f) * 50f;
				float dir = up ? -1f : 1f;
				Fx.Line(ring, new[] { new Vector2(x - 12f, 8f * -dir), new Vector2(x, -8f * -dir), new Vector2(x + 12f, 8f * -dir) }, Pal.White, 3f);
			}
			float delay = i * 0.12f;
			float span = feet.Y - center.Y + 80f;
			Fx.Animate(ring, 0.8f + delay, t =>
			{
				float local = Ease.Segment(t, delay / (0.8f + delay), 0.8f / (0.8f + delay));
				float y = up ? -span * Ease.OutCubic(local) : -span * (1f - Ease.OutCubic(local));
				ring.Position = new Vector2(0f, y);
				ring.Scale = Vector2.One * Mathf.Lerp(0.7f, 1.1f, local);
				ring.Modulate = new Color(1f, 1f, 1f, local <= 0f ? 0f : Ease.Envelope(local, 0.15f, 0.6f));
			});
		}
		P3Fx.Flash(layer, center, c, 260f, 0.25f);
		Fx.FreeAfter(root, 1.3);
	}

	/// <summary>君王之刃锻造：碎光从四周收拢，拼成一把幽蓝光剑后闪光消散。</summary>
	public static void ForgeBlade(Control layer, Vector2 at)
	{
		Node2D root = Fx.Root(layer, at, "P3Forge");
		root.Rotation = -0.35f;
		Vector2[] blade = { new(0f, -170f), new(14f, -120f), new(10f, 60f), new(26f, 70f), new(26f, 80f), new(6f, 82f), new(6f, 120f), new(-6f, 120f), new(-6f, 82f), new(-26f, 80f), new(-26f, 70f), new(-10f, 60f), new(-14f, -120f) };
		const float gather = 0.3f;
		for (int i = 0; i < 16; i++)
		{
			Vector2 target = blade[i % blade.Length] * 0.9f;
			Vector2 start = Fx.RandDir() * Fx.Rand(160f, 260f);
			Node2D shard = new Node2D { Position = start };
			root.AddChild(shard);
			Fx.Poly(shard, new Vector2[] { new(-8, 0), new(0, -14), new(8, 0), new(0, 6) }, new Color(Pal.Ice, 0.8f), additive: true);
			float spin = Fx.Rand(-8f, 8f);
			Fx.Animate(shard, gather, t =>
			{
				float e = Ease.InCubic(t);
				shard.Position = start.Lerp(target, e);
				shard.Rotation = spin * (1f - e);
			}).TweenCallback(Callable.From(shard.QueueFree));
		}
		Fx.After(layer, gather, "forge blade", () =>
		{
			Polygon2D glow = Fx.Poly(root, blade, new Color(Pal.Royal, 0.7f), additive: true);
			Line2D edge = Fx.Line(root, blade, Pal.Ice, 2.5f, closed: true);
			glow.Scale = edge.Scale = new Vector2(1.25f, 1.25f);
			Fx.Animate(glow, 0.65f, t =>
			{
				float s = Mathf.Lerp(1.25f, 1f, Ease.OutExpo(Mathf.Min(1f, t * 3f)));
				glow.Scale = edge.Scale = Vector2.One * s;
				float a = 1f - Ease.InCubic(t);
				glow.Modulate = edge.Modulate = new Color(1f + (1f - t), 1f + (1f - t), 1f + (1f - t), a);
			});
			P3Fx.Flash(layer, at, Pal.Ice, 340f, 0.2f);
			P3Fx.Ripple(layer, at, 460f, 0.55f, 0.03f);
			Fx.Sparks(root, Pal.Cyan, 30, 150f, 450f, 0.5f);
		});
		Fx.FreeAfter(root, gather + 0.8);
	}

	/// <summary>
	/// 影时间降临（VoidForm 等）：全屏染上暗绿，天空出现巨大满月，水面般的波纹扩散，随后褪去。
	/// </summary>
	public static void DarkHour(Control layer, float duration = 2.2f)
	{
		Vector2 size = layer.GetViewportRect().Size;
		// 放在人物身后的特效层（不用 TopLevel：TopLevel 会画到所有人物上面）。该层在可缩放 / 震屏的场景容器里，染色矩形放大到三倍屏幕保证盖满
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3DarkHour");
		ColorRect tintRect = new ColorRect { Position = -size, Size = size * 3f, Color = new Color(0.05f, 0.25f, 0.12f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(tintRect);
		Vector2 moonPos = new Vector2(size.X * 0.78f, size.Y * 0.2f);
		Sprite2D moonGlow = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Position = moonPos, Scale = Vector2.One * 16f, Modulate = new Color(0.75f, 1f, 0.6f, 0f) };
		root.AddChild(moonGlow);
		Polygon2D moon = Fx.Poly(root, Fx.Circle(70f, 48), new Color(0.92f, 1f, 0.75f, 0f), additive: true);   // 抗锯齿着色器
		moon.Position = moonPos;
		Fx.Animate(root, duration, t =>
		{
			float a = Ease.Envelope(t, 0.18f, 0.65f);
			tintRect.Color = new Color(0.05f, 0.25f, 0.12f, 0.38f * a);
			moon.Color = new Color(0.92f, 1f, 0.75f, 0.9f * a);
			moonGlow.Modulate = new Color(0.75f, 1f, 0.6f, 0.45f * a);
			moon.Scale = Vector2.One * Mathf.Lerp(0.85f, 1.05f, Ease.OutCubic(t));
		});
		P3Fx.Ripple(layer, size / 2f, size.X * 1.4f, 1.2f, 0.02f);
		P3Fx.Shockwave(layer, size / 2f, Pal.DarkHour, size.X * 1.2f, 1.1f, delay: 0.1f);
		Fx.FreeAfter(root, duration + 0.1);
	}

	/// <summary>色带上的文字：技能名（英文大字）、Persona 名、卡牌名（中文）。</summary>
	public sealed record Caption(string Skill, string Persona, string CardTitle);

	/// <summary>
	/// P3R 风格色带（v0.9.2 重做，参照原版菜单的色块拼接）：
	/// 左：白色斜切色块（上面是红字选中标签 = 中文卡名，和超大描边水印字）；中：深蓝主色带（技能名粗斜体白字 + 青色 PERSONA ▸ 名字）；
	/// 接缝处一道红色斜块；右端一个"水之圆"（焦散）带两道细圆弧。
	/// 入场：白块从左、深蓝块从右同时切入（OutExpo，错开 30ms）→ 红块竖向弹出 → 水之圆张开 → 标题从右滑入并收紧 → 标签盖章；
	/// 停留：整体缓慢漂移、圆弧转动；出场：白块向左、深蓝块向右加速切出，水之圆收拢，最后 40% 淡出。
	/// </summary>
	public static void SkewBand(Control layer, Caption? caption = null, float yRatio = 0.17f, float hold = 0.6f, Color? tint = null)
	{
		Vector2 size = layer.GetViewportRect().Size;
		float h = size.Y * 0.15f, lean = h * 0.6f;
		float y = size.Y * yRatio, top = y - h / 2f, bottom = y + h / 2f;
		Node2D root = Fx.Root(layer, Vector2.Zero, "P3SkewBand");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;

		// 深蓝主色带（从右切入）
		Node2D navy = new();
		root.AddChild(navy);
		P3Ui.Slab(navy, size.X * 0.3f, size.X + lean * 2f, top, bottom, lean, new Color(0.02f, 0.06f, 0.24f, 0.94f));
		P3Ui.Slab(navy, size.X * 0.3f, size.X + lean * 2f, bottom, bottom + 6f, 4f, new Color(Pal.Cyan, 0.9f));
		for (int i = 0; i < 4; i++)
		{
			float yy = top + h * (0.18f + 0.18f * i), x0 = Fx.Rand(0.4f, 0.85f) * size.X;
			Fx.Line(navy, new[] { new Vector2(x0, yy), new Vector2(x0 + Fx.Rand(90f, 300f), yy) }, new Color(Pal.Ice, 0.16f), 2f);
		}
		// 水之圆（右端）
		Vector2 discAt = new(size.X * 0.87f, y + h * 0.1f);
		float discR = h * 1.3f;
		ColorRect disc = P3Ui.WaterDisc(navy, discAt, discR);
		Node2D arcs = new() { Position = discAt };
		navy.AddChild(arcs);
		P3Ui.Arc(arcs, Vector2.Zero, discR * 1.1f, Mathf.Pi * 0.6f, Mathf.Pi * 1.5f, new Color(Pal.Cyan, 0.6f), 2.5f);
		P3Ui.Arc(arcs, Vector2.Zero, discR * 1.2f, Mathf.Pi * 0.9f, Mathf.Pi * 1.3f, new Color(1f, 1f, 1f, 0.5f), 2f);

		// 白色斜切色块（从左切入）
		Node2D white = new();
		root.AddChild(white);
		P3Ui.Slab(white, -lean * 2f, size.X * 0.36f, top - h * 0.14f, bottom + h * 0.06f, lean, new Color(0.95f, 0.97f, 1f, 0.96f));
		// 接缝红块
		Node2D red = new() { Position = new Vector2(size.X * 0.36f, bottom + h * 0.06f) };
		root.AddChild(red);
		P3Ui.Slab(red, 0f, h * 0.18f, -h * 1.2f, 0f, lean * 1.05f, new Color(0.9f, 0.08f, 0.22f, 0.95f));

		Node2D? watermark = null, title = null, meta = null, tag = null;
		if (caption != null)
		{
			watermark = new Node2D { Position = new Vector2(size.X * 0.02f, top - h * 0.05f), Skew = -0.22f };
			white.AddChild(watermark);
			Typo.Text(watermark, caption.Skill, Typo.Latin, (int)(h * 1.0f), new Color(1f, 1f, 1f, 0f), Vector2.Zero,
				outline: 3, outlineColor: new Color(0.02f, 0.06f, 0.24f, 0.18f));
			tag = P3Ui.RedTag(root, caption.CardTitle, new Vector2(size.X * 0.06f, y - h * 0.26f), (int)(h * 0.26f));
			title = new Node2D { Position = new Vector2(size.X * 0.42f, top + h * 0.3f), Skew = -0.25f };
			root.AddChild(title);
			Typo.Text(title, caption.Skill, Typo.Latin, (int)(h * 0.46f), Pal.White, Vector2.Zero,
				outline: 2, outlineColor: new Color(Pal.Abyss, 1f), shadow: new Color(0.9f, 0.08f, 0.22f, 0.9f), shadowOffset: new Vector2(6f, 5f));
			meta = new Node2D { Position = new Vector2(size.X * 0.42f + 8f, top + h * 0.08f), Skew = -0.25f };
			root.AddChild(meta);
			Typo.Text(meta, "PERSONA  \u25B8  " + caption.Persona, Typo.Latin, (int)(h * 0.14f), Pal.Cyan, Vector2.Zero);
		}

		const float tIn = 0.3f, tOut = 0.24f;
		float total = tIn + hold + tOut;
		Vector2 titleHome = title?.Position ?? Vector2.Zero, tagHome = tag?.Position ?? Vector2.Zero, metaHome = meta?.Position ?? Vector2.Zero;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float inA = Ease.OutExpo(Mathf.Clamp(time / 0.22f, 0f, 1f));
			float inB = Ease.OutExpo(Mathf.Clamp((time - 0.03f) / 0.22f, 0f, 1f));
			float drift = Mathf.Clamp((time - tIn) / hold, 0f, 1f);
			float o = Mathf.Clamp((time - tIn - hold) / tOut, 0f, 1f);
			float outE = Ease.InExpo(o);
			white.Position = new Vector2(-size.X * 0.55f * (1f - inA) - 30f * drift - size.X * 0.6f * outE, 0f);
			navy.Position = new Vector2(size.X * 0.9f * (1f - inB) + 30f * drift + size.X * outE, 0f);
			float r = Mathf.Clamp((time - 0.1f) / 0.14f, 0f, 1f);
			red.Scale = new Vector2(1f, Ease.OutBack(r) * (1f - outE));
			Fx.SetParam(disc, "reveal", Ease.OutExpo(Mathf.Clamp((time - 0.08f) / 0.26f, 0f, 1f)) * (1f - Ease.InCubic(o)));
			arcs.Rotation = 0.3f * time;
			float fade = 1f - Mathf.Clamp((o - 0.6f) / 0.4f, 0f, 1f);
			root.Modulate = new Color(1f, 1f, 1f, fade);
			if (watermark != null)
			{
				watermark.Position = new Vector2(size.X * 0.02f - time * 70f, top - h * 0.05f);
			}
			if (title != null && meta != null && tag != null)
			{
				float u = Ease.OutExpo(Mathf.Clamp((time - 0.06f) / 0.3f, 0f, 1f));
				title.Position = titleHome + new Vector2(280f * (1f - u) + 24f * drift + size.X * 0.7f * outE, 0f);
				title.Scale = new Vector2(Mathf.Lerp(1.4f, 1f, u), 1f);
				title.Modulate = new Color(1f, 1f, 1f, u);
				float m = Ease.OutCubic(Mathf.Clamp((time - 0.14f) / 0.16f, 0f, 1f));
				meta.Position = metaHome + new Vector2(60f * (1f - m) + size.X * 0.7f * outE, 0f);
				meta.Modulate = new Color(1f, 1f, 1f, m);
				float k = Mathf.Clamp((time - 0.12f) / 0.16f, 0f, 1f);
				tag.Scale = Vector2.One * Mathf.Lerp(1.7f, 1f, Ease.OutBack(k));
				tag.Modulate = new Color(1f, 1f, 1f, k);
				tag.Position = tagHome - new Vector2(size.X * 0.6f * outE, 0f);
			}
		});
		Fx.FreeAfter(root, total + 0.1);
	}

	/// <summary>黑洞：中心暗核 + 向内旋转吸入的光粒 + 旋转吸积环。</summary>
	public static void BlackHole(Control layer, Vector2 at, float duration = 1.4f)
	{
		Node2D root = Fx.Root(layer, at, "P3BlackHole");
		Node2D disk = new Node2D { Scale = new Vector2(1f, 0.35f) };
		root.AddChild(disk);
		for (int i = 0; i < 3; i++)
		{
			float a0 = Mathf.Tau * i / 3f;
			Line2D arc = Fx.Line(disk, Arc(130f + i * 18f, a0, a0 + 2.2f), i == 1 ? Pal.Ice : Pal.Cyan, 4f - i);
		}
		Polygon2D core = Fx.Poly(root, Fx.Circle(60f, 40), new Color(0f, 0f, 0.02f, 0.95f));
		Line2D rim = Fx.Line(root, Fx.Circle(62f, 40), Pal.Ice, 2.5f, closed: true);
		for (int i = 0; i < 24; i++)
		{
			float a = Fx.Rand(0f, Mathf.Tau);
			float r0 = Fx.Rand(220f, 340f);
			float delay = Fx.Rand(0f, duration * 0.5f);
			Sprite2D mote = new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = Pal.Cyan, Scale = Vector2.One * 0.5f };
			root.AddChild(mote);
			Fx.Animate(mote, duration, t =>
			{
				float local = Ease.Segment(t, delay / duration, 0.45f);
				float r = Mathf.Lerp(r0, 60f, Ease.InCubic(local));
				float ang = a + local * 4f;
				mote.Position = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.5f) * r;
				mote.Modulate = new Color(Pal.Cyan, local <= 0f || local >= 1f ? 0f : 1f);
			});
		}
		Fx.Animate(root, duration, t =>
		{
			float grow = Ease.OutBack(Mathf.Min(1f, t * 3f));
			float end = Ease.InExpo(Ease.Segment(t, 0.8f, 0.2f));
			core.Scale = rim.Scale = Vector2.One * grow * (1f - end);
			disk.Rotation = t * 5f;
			disk.Modulate = new Color(1f, 1f, 1f, 1f - end);
		});
		P3Fx.Ripple(layer, at, 520f, duration * 0.8f, 0.04f);
		Fx.After(layer, duration * 0.95, "black hole collapse", () =>
		{
			P3Fx.Flash(layer, at, Pal.Ice, 380f, 0.25f);
			P3Fx.Shockwave(layer, at, Pal.Cyan, 520f, 0.5f);
		});
		Fx.FreeAfter(root, duration + 0.1);
	}

	private static Vector2[] Arc(float radius, float from, float to, int segments = 24)
	{
		Vector2[] pts = new Vector2[segments + 1];
		for (int i = 0; i <= segments; i++)
		{
			float a = Mathf.Lerp(from, to, (float)i / segments);
			pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
		}
		return pts;
	}

	/// <summary>
	/// 锻造 = P3R 增益（v0.14.0，取代"碎光拼成光剑"和打铁）：脚下一圈深红色环 → 6 道深红色上升箭头（白芯，像 P3R 的攻击力上升）
	/// 从脚边错落升到头顶 → 头旁弹出 P3R 斜标签「ATTACK UP」+ 大号「+N」（锻造的数值）。v0.14.22 起不再浮现 Persona（锻造不召唤），细节见 ChargeDetails。
	/// big（铸剑者等一次锻造很多）：箭头更多、标签更大、Persona 更清晰。
	/// </summary>
	private static ulong _forgeDeferUntil;
	private static float _forgeDefer;

	/// <summary>铸剑者：接下来 1.5s 内的锻造推迟 seconds 秒（等锻打演出的新剑入手），且不再拼光剑。</summary>
	public static void DeferForge(float seconds)
	{
		_forgeDeferUntil = Time.GetTicksMsec() + 1500;
		_forgeDefer = seconds;
	}

	/// <summary>这次锻造的（推迟秒数，是否拼光剑）。</summary>
	public static (float Delay, bool Blade) TakeForgeDelay() =>
		Time.GetTicksMsec() <= _forgeDeferUntil ? (_forgeDefer, false) : (0f, true);

	public static void PersonaCharge(MegaCrit.Sts2.Core.Nodes.Combat.NCreature caster, int amount, bool big)
	{
		Control? front = P3Makoto.Core.Makoto.FxLayer();
		Control? back = P3Makoto.Core.Makoto.FxLayer(back: true);
		if (front == null || back == null)
		{
			return;
		}
		Vector2 feet = P3Makoto.Core.Makoto.Feet(caster), head = P3Makoto.Core.Makoto.Head(caster), center = P3Makoto.Core.Makoto.Center(caster);
		Color red = new(1f, 0.18f, 0.22f), deep = new(0.55f, 0.02f, 0.08f);
		Audio.Sfx.Clip("ShuffleTime_PowerUp", big ? 0.9f : 0.7f);
		// v0.14.22（用户：锻造不召唤 Persona —— 只有召唤器动画时身后才能有 Persona；ATTACK+N 太简单，丰富细节）：
		// 去掉 Thanatos 虚影，补上 起势收束 → 身周红色气焰 → 身后深红剑印 → 标签落定"铛"一声 + 火星 → 余烬 这几层
		ChargeDetails(caster, front, back, feet, head, center, red, deep, big);

		// 脚下深红环（压扁）
		Node2D ring = Fx.Root(back, feet, "P3ChargeRing");
		ring.Scale = new Vector2(1f, 0.3f);
		Fx.StrokeClosed(ring, Fx.Circle(150f, 56), new Color(red, 0.9f), 6f);
		Fx.StrokeClosed(ring, Fx.Circle(126f, 56), new Color(deep, 0.6f), 10f);
		Fx.Animate(ring, 0.8f, t =>
		{
			ring.Scale = new Vector2(0.4f + 0.8f * Ease.OutExpo(t), (0.4f + 0.8f * Ease.OutExpo(t)) * 0.3f);
			ring.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
		});
		Fx.FreeAfter(ring, 0.82);

		// 上升箭头：粗 V 形（尖朝上），深红外缘 + 白芯，错落升起、上升中拉长后淡出
		int n = big ? 10 : 6;
		for (int i = 0; i < n; i++)
		{
			float x = Mathf.Lerp(-110f, 110f, n == 1 ? 0.5f : i / (float)(n - 1)) + Fx.Rand(-14f, 14f);
			float delay = 0.06f * i + Fx.Rand(0f, 0.08f);
			float size = Fx.Rand(26f, 40f) * (big ? 1.2f : 1f);
			Node2D arrow = Fx.Root(front, feet + new Vector2(x, -20f), "P3ChargeArrow");
			Vector2[] chev = { new(0f, -size), new(size * 0.8f, -size * 0.1f), new(size * 0.8f, size * 0.35f), new(0f, -size * 0.45f), new(-size * 0.8f, size * 0.35f), new(-size * 0.8f, -size * 0.1f) };
			Vector2[] core = { new(0f, -size * 0.78f), new(size * 0.5f, -size * 0.12f), new(size * 0.5f, size * 0.08f), new(0f, -size * 0.42f), new(-size * 0.5f, size * 0.08f), new(-size * 0.5f, -size * 0.12f) };
			Fx.Poly(arrow, chev, new Color(red, 0.95f), additive: true);
			Fx.Poly(arrow, core, new Color(1f, 0.9f, 0.88f), additive: true);
			arrow.Modulate = new Color(1f, 1f, 1f, 0f);
			Vector2 from = arrow.Position;
			float rise = (feet.Y - head.Y) + Fx.Rand(40f, 120f);
			Fx.Animate(arrow, 0.75f + delay, t =>
			{
				float time = t * (0.75f + delay) - delay;
				if (time < 0f) return;
				float u = Mathf.Clamp(time / 0.75f, 0f, 1f);
				arrow.Position = from + new Vector2(6f * Mathf.Sin(u * 7f + i), -rise * Ease.OutCubic(u));
				arrow.Scale = new Vector2(1f, 1f + 0.6f * u);
				arrow.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(u / 0.12f, 0f, 1f) * (1f - Mathf.Clamp((u - 0.65f) / 0.35f, 0f, 1f)));
			});
			Fx.FreeAfter(arrow, 0.8f + delay);
		}
		P3Fx.Flash(back, center, red, big ? 420f : 300f, 0.3f);

		// 标签：黑色斜条 +「ATTACK UP」（深红斜体）+ 大号「+N」
		Fx.After(front, 0.25, "charge tag", () =>
		{
			Vector2 home = head + P3Makoto.Core.Makoto.Offset(caster, new Vector2(120f, -40f));
			Node2D tag = Fx.Root(front, home, "P3ChargeTag");
			tag.Rotation = -0.18f;
			float sc = big ? 1.25f : 1f;
			Fx.Poly(tag, new[] { new Vector2(-6f, 0f), new Vector2(190f, 0f), new Vector2(178f, 34f), new Vector2(-18f, 34f) }, new Color(0.04f, 0.03f, 0.05f, 0.95f));
			Fx.Poly(tag, new[] { new Vector2(-6f, 0f), new Vector2(190f, 0f), new Vector2(189f, 3f), new Vector2(-7f, 3f) }, red, additive: true);
			Typo.Text(tag, "ATTACK UP", Typo.LatinItalic, 26, new Color(1f, 0.32f, 0.35f), new Vector2(6f, 0f));
			if (amount > 0)
			{
				Typo.Text(tag, "+" + amount, Typo.LatinItalic, 54, Pal.White, new Vector2(30f, 28f), outline: 5, outlineColor: deep);
			}
			tag.Scale = Vector2.One * sc;
			// 斜条上扫过一道亮光（落定后）
			Polygon2D shine = Fx.Poly(tag, new Vector2[4], new Color(1f, 0.85f, 0.85f, 0f), additive: true);
			Fx.Animate(tag, 1.1f, t =>
			{
				float time = t * 1.1f;
				float inn = Ease.OutBack(Mathf.Clamp(time / 0.14f, 0f, 1f));
				tag.Position = home + new Vector2(-50f * (1f - inn), -14f * Ease.OutCubic(t));
				tag.Modulate = new Color(1f, 1f, 1f, inn * (1f - Mathf.Clamp((time - 0.85f) / 0.25f, 0f, 1f)));
				float sw = Mathf.Clamp((time - 0.16f) / 0.3f, 0f, 1f);
				float x0 = Mathf.Lerp(-30f, 200f, Ease.InOutCubic(sw));
				Fx.Reshape(shine, new[] { new Vector2(x0, 0f), new Vector2(x0 + 18f, 0f), new Vector2(x0 + 6f, 34f), new Vector2(x0 - 12f, 34f) });
				shine.Color = new Color(1f, 0.85f, 0.85f, 0.55f * Mathf.Sin(sw * Mathf.Pi));
			});
			Fx.FreeAfter(tag, 1.15);
			// 标签落定（0.14s）：数字后面一记白色十字星 + 火星向右上溅开
			Fx.After(tag, 0.13, "charge clang", () =>
			{
				Vector2 at = tag.GlobalPosition + new Vector2(70f, 52f).Rotated(tag.Rotation) * sc;
				Node2D star = Fx.Root(front, at, "P3ChargeClang");
				Fx.Poly(star, Fx.Star4(46f * sc, 0.08f), Pal.White, additive: true);
				Fx.Poly(star, Fx.Star4(78f * sc, 0.05f), new Color(red, 0.6f), additive: true).Rotation = Mathf.Pi / 4f;
				Fx.Animate(star, 0.22f, t =>
				{
					star.Scale = Vector2.One * (t < 0.3f ? Ease.OutExpo(t / 0.3f) : 1f - Ease.InCubic((t - 0.3f) / 0.7f));
					star.Rotation = t * 0.6f;
				});
				Fx.FreeAfter(star, 0.26);
				Node2D sp = Fx.Root(front, at, "P3ChargeSparks");
				GpuParticles2D e = Fx.Sparks(sp, red, big ? 34 : 22, 220f, 560f, 0.45f, gravity: 900f, scaleMin: 0.06f, scaleMax: 0.18f,
					ramp: new[] { Pal.White, new Color(1f, 0.75f, 0.4f), red });
				ParticleProcessMaterial pm = (ParticleProcessMaterial)e.ProcessMaterial;
				pm.Direction = new Vector3(0.5f, -1f, 0f);
				pm.Spread = 55f;
				Fx.FreeAfter(sp, 0.6);
				// v0.14.25（用户：打铁声重复 —— 老的锻造特效 / 铸剑者锤击本来就有打铁声）：标签落定不再另加一声
			});
		});
	}

	/// <summary>锻造增益的细节层（v0.14.22）：起势收束、红色气焰、身后剑印、余烬。都在人物身后或很细，不挡人。</summary>
	private static void ChargeDetails(MegaCrit.Sts2.Core.Nodes.Combat.NCreature caster, Control front, Control back, Vector2 feet, Vector2 head, Vector2 center, Color red, Color deep, bool big)
	{
		float bodyH = Mathf.Max(200f, feet.Y - head.Y);
		// 1. 起势（0~0.2s）：12 道细红光从四周收向胸口（身后层）
		for (int i = 0; i < 12; i++)
		{
			Vector2 dir = Vector2.Right.Rotated(Mathf.Tau * i / 12f + Fx.Rand(-0.15f, 0.15f));
			float r0 = Fx.Rand(220f, 300f);
			Line2D streak = Fx.Line(Fx.Root(back, center, "P3ChargeIn"), new[] { dir * r0, dir * r0 }, new Color(red, 0.8f), 3f);
			Node2D sroot = (Node2D)streak.GetParent();
			Fx.Animate(sroot, 0.22f, t =>
			{
				float a = Ease.InCubic(t), b = Ease.InCubic(Mathf.Max(0f, t - 0.25f) / 0.75f);
				streak.Points = new[] { dir * Mathf.Lerp(r0, 30f, b), dir * Mathf.Lerp(r0, 30f, a) };
				streak.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t) * 0.6f);
			});
			Fx.FreeAfter(sroot, 0.24);
		}
		// 2. 身周红色气焰（0.1~1.1s）：拉长的柔光从脚下沿身体两侧往上窜（身后层），像 P3R 增益的红色气场
		int wisps = big ? 30 : 20;
		for (int i = 0; i < wisps; i++)
		{
			float delay = 0.1f + Fx.Rand(0f, 0.65f);
			float x = Fx.Rand(-1f, 1f);
			x = Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), 0.6f) * 95f;
			Color c = Fx.Rng.Next(3) == 0 ? new Color(1f, 0.55f, 0.5f) : red;
			Sprite2D w = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(c, 0f) };
			Node2D wr = Fx.Root(back, feet + new Vector2(x, -10f), "P3ChargeWisp");
			wr.AddChild(w);
			float rise = bodyH * Fx.Rand(0.6f, 1.05f), life = Fx.Rand(0.4f, 0.6f), sx = Fx.Rand(0.5f, 0.9f);
			Fx.Animate(wr, delay + life, t =>
			{
				float u = Mathf.Clamp((t * (delay + life) - delay) / life, 0f, 1f);
				wr.Position = feet + new Vector2(x * (1f - 0.25f * u), -10f - rise * Ease.OutCubic(u));
				w.Scale = new Vector2(sx * (1f - 0.5f * u), 1.2f + 3.2f * Mathf.Sin(u * Mathf.Pi));
				w.Modulate = new Color(c, u <= 0f ? 0f : 0.45f * Mathf.Sin(u * Mathf.Pi));
			});
			Fx.FreeAfter(wr, delay + life + 0.05f);
		}
		// 3. 身后深红剑印（0.2~1.2s）：君王之刃的轮廓在背后竖起、一道亮光从剑尖扫到剑柄，随后化成火星散去
		float bs = bodyH / 300f * (big ? 1.25f : 1.05f);
		Vector2[] blade = { new(0f, -170f), new(14f, -120f), new(10f, 60f), new(26f, 70f), new(26f, 80f), new(6f, 82f), new(6f, 120f), new(-6f, 120f), new(-6f, 82f), new(-26f, 80f), new(-26f, 70f), new(-10f, 60f), new(-14f, -120f) };
		Vector2 home = center + P3Makoto.Core.Makoto.Offset(caster, new Vector2(-bodyH * 0.12f, -bodyH * 0.18f));
		Node2D sig = Fx.Root(back, home, "P3ChargeBlade");
		sig.Scale = Vector2.One * bs;
		sig.Rotation = -0.12f * P3Makoto.Core.Makoto.Facing(caster);
		Polygon2D fill = Fx.Poly(sig, blade, new Color(deep, 0.35f), additive: true);
		Line2D edge = Fx.Line(sig, blade, new Color(red, 0.95f), 3f, closed: true);
		Line2D sheen = Fx.Line(sig, new[] { Vector2.Zero, Vector2.Zero }, new Color(1f, 0.92f, 0.9f, 0.9f), 5f);
		sig.Modulate = new Color(1f, 1f, 1f, 0f);
		Fx.Animate(sig, 1.25f, t =>
		{
			float time = t * 1.25f;
			float inn = Ease.OutCubic(Mathf.Clamp((time - 0.15f) / 0.25f, 0f, 1f));
			float outk = Ease.InCubic(Mathf.Clamp((time - 0.9f) / 0.35f, 0f, 1f));
			sig.Position = home + new Vector2(0f, 40f * (1f - inn) - 20f * outk);
			sig.Scale = Vector2.One * bs * (0.9f + 0.1f * inn + 0.08f * outk);
			sig.Modulate = new Color(1f, 1f, 1f, inn * (1f - outk));
			// 亮光沿剑脊从剑尖扫到剑柄（0.38~0.62s，和标签落定差不多同时）
			float sw = Mathf.Clamp((time - 0.38f) / 0.24f, 0f, 1f);
			float y = Mathf.Lerp(-170f, 120f, Ease.InOutCubic(sw));
			sheen.Points = new[] { new Vector2(0f, Mathf.Max(-170f, y - 50f)), new Vector2(0f, y) };
			sheen.Modulate = new Color(1f, 1f, 1f, Mathf.Sin(sw * Mathf.Pi));
			fill.Color = new Color(deep, 0.35f + 0.3f * Mathf.Sin(sw * Mathf.Pi));
		});
		Fx.FreeAfter(sig, 1.3);
		Fx.After(sig, 0.95, "charge blade dissolve", () =>
		{
			Node2D dust = Fx.Root(back, home, "P3ChargeBladeDust");
			GpuParticles2D e = Fx.Sparks(dust, red, big ? 40 : 26, 20f, 90f, 0.8f, gravity: -140f, scaleMin: 0.06f, scaleMax: 0.16f);
			ParticleProcessMaterial pm = (ParticleProcessMaterial)e.ProcessMaterial;
			pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
			pm.EmissionBoxExtents = new Vector3(14f * bs, 140f * bs, 0f);
			Fx.FreeAfter(dust, 1.0);
		});
		// 4. 余烬（0.6~1.6s）：几颗红色小火星在身边慢慢往上飘、闪烁熄灭
		for (int i = 0; i < (big ? 14 : 9); i++)
		{
			float delay = 0.6f + Fx.Rand(0f, 0.4f);
			Vector2 at = feet + new Vector2(Fx.Rand(-120f, 120f), -Fx.Rand(20f, bodyH * 0.9f));
			Fx.After(front, delay, "charge ember", () =>
			{
				Node2D em = Fx.Root(front, at, "P3ChargeEmber");
				Fx.Poly(em, Fx.Star4(Fx.Rand(3f, 5.5f), 0.25f), Fx.Rng.Next(2) == 0 ? red : new Color(1f, 0.8f, 0.6f), additive: true);
				Vector2 drift = new(Fx.Rand(-12f, 12f), -Fx.Rand(40f, 80f));
				float life = Fx.Rand(0.6f, 0.9f);
				Fx.Animate(em, life, t =>
				{
					em.GlobalPosition = at + drift * Ease.OutCubic(t);
					em.Modulate = new Color(1f, 1f, 1f, (1f - Ease.InCubic(t)) * (0.6f + 0.4f * Mathf.Abs(Mathf.Cos(t * 9f))));
				});
				Fx.FreeAfter(em, life + 0.05f);
			});
		}
		// 脚下第二道环（比第一道晚 0.18s、更细），让落地的环有回响
		Node2D ring2 = Fx.Root(back, feet, "P3ChargeRing2");
		Fx.StrokeClosed(ring2, Fx.Circle(150f, 56), new Color(1f, 0.6f, 0.6f, 0.7f), 2.5f);
		ring2.Modulate = new Color(1f, 1f, 1f, 0f);
		Fx.Animate(ring2, 0.95f, t =>
		{
			float u = Mathf.Clamp((t * 0.95f - 0.18f) / 0.77f, 0f, 1f);
			float k = 0.3f + 1.1f * Ease.OutExpo(u);
			ring2.Scale = new Vector2(k, k * 0.3f);
			ring2.Modulate = new Color(1f, 1f, 1f, u <= 0f ? 0f : 1f - Ease.InCubic(u));
		});
		Fx.FreeAfter(ring2, 1.0);
	}
}
