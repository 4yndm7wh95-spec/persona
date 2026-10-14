using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 伟大封印（v0.14.3）—— 塔顶与建筑师的最后会面，换成 P3 的结局。
///
/// 房间（Room）：原版背景上盖一层 P3R 式「塔尔塔洛斯顶层」（art/v13/tartarus_top_b）：巨大的绿月、环形平台、棺材状石柱；
///   月光呼吸、低处两层绿雾缓慢横移、光点不停上飘、影时间的暗绿色调。
/// 对话（Line）：结城理话很少；建筑师问他要什么、问牺牲的意义。三种来访各一套（中 / 英）。
/// 最后的按钮（DecorateButton）：「伟大封印 / THE GREAT SEAL」—— 深蓝斜切底板 + 金边、左侧旋转的小封印、周期性扫过的光带、外圈呼吸光晕。
/// 结局演出（Play，约 11.6s，配 Nyx 战「封印」原声 great_seal：前 8s 低鸣、9–10s 高潮）：
///   1.2  切入结束后画面压暗成影时间的暗绿；
///   0.2  眼神切入「伟大封印 / PERSONA ▸ UNIVERSE」，召唤器抵住太阳穴 → 扣扳机，玻璃碎裂；
///   1.4  十五张大阿卡纳（P3R 原版牌面，0 愚者 … XII、XIX 太阳，最后是 XIII 死神）依次在结城理周围翻出，
///        每张都有一根细细的青色"羁绊"光线连回他胸口，整圈缓慢旋转；
///   4.6  所有牌螺旋收进胸口 → 5.3 宇宙爆发：白金闪光 + 冲击环 + 速度线 + 12 颗金星的圆环，
///        黑色斜带上巨大的「UNIVERSE」横移（ARCANA XXI · 宇宙）；
///   5.8  三道光链从结城理射向建筑师，三圈金色封印环在它身上收紧，建筑师褪成石像般的灰蓝；
///   6.4  全屏淡入「伟大封印」画面（巨门中的人像、黑月、星海），镜头缓推；十四道光流从四周汇向门心，光点漂浮；
///   7.0  标题「伟大封印 / THE GREAT SEAL」浮现；9.3 门心白光暴涨 → 9.9 全屏白（原声高潮）；
///   10.4 白 → 深蓝，「MEMENTO MORI」；11.6 交还给游戏的胜利流程，覆盖层再用 1s 淡出。
/// </summary>
public static class GreatSeal
{
	public const float Duration = 11.6f;
	private static readonly Color Gold = new(1f, 0.88f, 0.55f);
	private static readonly Color Moon = new(0.55f, 1f, 0.6f);

	private static Texture2D? Tex(string name)
	{
		string path = $"res://p3makoto/fx/{name}.png";
		return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
	}

	private static bool Zh => LocManager.Instance?.Language is string l && l.StartsWith("zh");

	// ───────────────────────── 对话 ─────────────────────────

	private static readonly Dictionary<string, (string Zh, string En)> Lines = new()
	{
		["0-0.char"] = ("……这里就是塔顶。", "...So this is the top."),
		["0-0.next"] = ("继续", "Continue"),
		["0-1.ancient"] = ("又一个爬到尽头的人。你想要什么？财富？力量？……还是答案？", "Another climber at the end of the stairs. What do you want? Wealth? Power? ...An answer?"),
		["0-1.next"] = ("回答", "Answer"),
		["0-2.char"] = ("答案，我已经有了。", "I already have my answer."),
		["1-0r.char"] = ("又见面了。", "We meet again."),
		["1-0r.next"] = ("继续", "Continue"),
		["1-1r.ancient"] = ("你每次都会回来。塔每一次都会重建——你的牺牲又有什么意义？", "You always come back. The Spire rebuilds itself every time. What is your sacrifice worth?"),
		["1-1r.next"] = ("回答", "Answer"),
		["1-2r.char"] = ("只要还有人能醒来迎接明天，就值得。", "As long as someone wakes up to tomorrow, it's worth it."),
		["2-0r.char"] = ("……该结束了。", "...It's time to end this."),
		["2-0r.next"] = ("继续", "Continue"),
		["2-1r.ancient"] = ("结束？你连终结本身都想封印么。", "End it? You would seal away the end itself?"),
		["2-1r.next"] = ("回答", "Answer"),
		["2-2r.char"] = ("Memento mori。——正因如此，才要活下去。", "Memento mori. That's exactly why we live."),
	};

	private const string Prefix = "THE_ARCHITECT.talk.REGENT.";

	/// <summary>建筑师对话（REGENT 那一套）换成结城理的台词；不是这些键返回 null。</summary>
	public static string? Line(string table, string key)
	{
		if (table != "ancients" || !key.StartsWith(Prefix) || !Lines.TryGetValue(key[Prefix.Length..], out var l))
		{
			return null;
		}
		return Zh ? l.Zh : l.En;
	}

	// ───────────────────────── 房间 ─────────────────────────

	public static void Room(NCombatRoom room)
	{
		if (room.GetNodeOrNull<Control>("%BgContainer") is not Control bg)
		{
			return;
		}
		if (bg.GetNodeOrNull("P3TartarusTop") != null || Tex("tartarus_top") is not Texture2D tex)
		{
			return;
		}
		Vector2 view = room.GetViewportRect().Size;
		const float margin = 60f;                                   // 震屏时不露边
		Control root = new() { Name = "P3TartarusTop", MouseFilter = Control.MouseFilterEnum.Ignore };
		bg.AddChild(root);
		Transform2D gt = bg.GetGlobalTransform();
		root.Scale = new Vector2(1f / gt.Scale.X, 1f / gt.Scale.Y);
		root.GlobalPosition = Vector2.Zero;
		Node2D art = new();
		root.AddChild(art);
		float W = view.X + margin * 2f, H = view.Y + margin * 2f;
		// 底对齐、略放大：让原图里的圆形平台地面（原图 y≈745）落在人物脚下（屏高约 70% 处），月亮露出下半圆
		float s = Mathf.Max(W / tex.GetWidth(), H / tex.GetHeight()) * 1.05f;
		Vector2 artSize = new(tex.GetWidth() * s, tex.GetHeight() * s);
		Vector2 origin = new(-margin + (W - artSize.X) / 2f, view.Y + margin - artSize.Y);
		art.AddChild(new Sprite2D { Texture = tex, Centered = false, Position = origin, Scale = Vector2.One * s });
		Vector2 moonAt = origin + new Vector2(899f, 70f) * s;     // 原图里月亮的中心
		float moonR = 230f * s;

		// 月光：两层加色光晕呼吸
		Sprite2D halo = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = moonAt, Modulate = new Color(Moon, 0.35f) };
		Sprite2D core = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = moonAt, Modulate = new Color(0.85f, 1f, 0.85f, 0.25f) };
		art.AddChild(halo);
		art.AddChild(core);
		float dotSize = Fx.SoftDot.GetWidth();
		// 低处绿雾：几团压扁的柔光，左右缓慢漂
		List<(Sprite2D sp, float y, float speed, float phase)> fog = new();
		for (int i = 0; i < 7; i++)
		{
			Sprite2D f = new()
			{
				Texture = Fx.SoftDot, Material = Fx.Additive,
				Modulate = new Color(0.3f, 0.75f, 0.45f, Fx.Rand(0.08f, 0.16f)),
				Scale = new Vector2(Fx.Rand(9f, 14f), Fx.Rand(1.6f, 2.6f)) * (view.X / 1920f) * (64f / dotSize)
			};
			art.AddChild(f);
			fog.Add((f, view.Y * Fx.Rand(0.55f, 0.95f), Fx.Rand(10f, 26f) * (Fx.Rng.NextDouble() < 0.5 ? -1f : 1f), Fx.Rand(0f, 1f)));
		}
		// 上飘的光点
		List<(Polygon2D p, float x, float speed, float phase, float sway)> motes = new();
		for (int i = 0; i < 34; i++)
		{
			Polygon2D m = Fx.Poly(art, Fx.Star4(Fx.Rand(3f, 7f), 0.3f), Fx.Rng.NextDouble() < 0.7 ? new Color(0.7f, 1f, 0.75f) : Pal.Ice, additive: true);
			motes.Add((m, Fx.Rand(0f, view.X), Fx.Rand(18f, 46f), Fx.Rand(0f, 1f), Fx.Rand(8f, 30f)));
		}
		// 影时间暗绿调 + 四周压暗
		ColorRect tint = new() { Size = new Vector2(W, H), Position = new Vector2(-margin, -margin), Color = new Color(0.02f, 0.12f, 0.06f, 0.18f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(tint);
		ulong start = Time.GetTicksMsec();
		const float loop = 60f;
		Fx.Animate(root, loop, _ =>
		{
			float time = (Time.GetTicksMsec() - start) / 1000f;
			float breath = 0.5f + 0.5f * Mathf.Sin(time * 0.9f);
			halo.Scale = Vector2.One * (moonR * 3.4f / dotSize) * (1f + 0.05f * breath);
			halo.Modulate = new Color(Moon, 0.28f + 0.1f * breath);
			core.Scale = Vector2.One * (moonR * 2.1f / dotSize);
			core.Modulate = new Color(0.85f, 1f, 0.85f, 0.16f + 0.08f * breath);
			foreach ((Sprite2D sp, float y, float speed, float phase) in fog)
			{
				float span = view.X + 900f;
				float x = Mathf.PosMod(phase * span + speed * time, span) - 450f;
				sp.Position = new Vector2(x, y + 10f * Mathf.Sin(time * 0.3f + phase * 6f));
			}
			foreach ((Polygon2D p, float x, float speed, float phase, float sway) in motes)
			{
				float span = view.Y + 80f;
				float u = Mathf.PosMod(phase + speed * time / span, 1f);
				p.Position = new Vector2(x + sway * Mathf.Sin(time * 0.8f + phase * 9f), view.Y + 40f - u * span);
				p.Modulate = new Color(1f, 1f, 1f, Mathf.Sin(u * Mathf.Pi) * (0.35f + 0.25f * Mathf.Sin(time * 3f + phase * 20f)));
				p.Rotation = time * 0.8f + phase;
			}
			tint.Color = new Color(0.02f, 0.12f, 0.06f, 0.14f + 0.06f * breath);
		}).SetLoops();
	}

	// ───────────────────────── 最后的按钮 ─────────────────────────

	public static void DecorateButton(NEventOptionButton button)
	{
		if (button.GetNodeOrNull<RichTextLabel>("%Text") is RichTextLabel label)
		{
			label.Text = Zh
				? "[color=#ffe7a8][b]伟大封印[/b][/color]\n[color=#9fd8ff]THE GREAT SEAL —— 以生命为封印，终结这一切[/color]"
				: "[color=#ffe7a8][b]The Great Seal[/b][/color]\n[color=#9fd8ff]Become the seal. End it all.[/color]";
		}
		if (button.GetNodeOrNull<CanvasItem>("Image") is CanvasItem image)
		{
			image.Modulate = new Color(0.12f, 0.2f, 0.45f);
		}
		// 底板等布局算完（按钮有了尺寸）再画
		button.GetTree().CreateTimer(0.05).Timeout += () => ModEntry.Safe("seal button", () =>
		{
			if (GodotObject.IsInstanceValid(button))
			{
				Decor(button);
			}
		});
	}

	private static void Decor(NEventOptionButton button)
	{
		if (button.GetNodeOrNull("P3SealDecor") != null)
		{
			return;
		}
		Vector2 size = button.Size;
		Node2D decor = new() { Name = "P3SealDecor" };
		button.AddChild(decor);
		int under = button.GetNodeOrNull("Outline") is Node outline ? outline.GetIndex() + 1 : 1;
		button.MoveChild(decor, Mathf.Min(under, button.GetChildCount() - 1));
		const float slant = 18f;
		Vector2[] plate = { new(-6f + slant, -4f), new(size.X + 10f, -4f), new(size.X + 10f - slant, size.Y + 4f), new(-6f, size.Y + 4f) };
		Polygon2D glow = Fx.Poly(decor, Fx.StrokePts(Close(plate), 14f), new Color(Gold, 0.25f), additive: true);
		Fx.Poly(decor, plate, new Color(0.03f, 0.06f, 0.16f, 0.88f));
		Fx.Line(decor, Close(plate), new Color(Gold, 0.95f), 2f);
		Fx.Line(decor, Close(new[] { plate[0] + new Vector2(5f, 4f), plate[1] + new Vector2(-5f, 4f), plate[2] + new Vector2(-5f, -4f), plate[3] + new Vector2(5f, -4f) }), new Color(1f, 1f, 1f, 0.25f), 1f);
		// 左侧小封印：外环 + 12 刻度 + 中心十字星
		Node2D emblem = new() { Position = new Vector2(-24f, size.Y / 2f) };
		decor.AddChild(emblem);
		Fx.StrokeClosed(emblem, Fx.Circle(22f, 48), new Color(Gold, 0.95f), 2f);
		Fx.StrokeClosed(emblem, Fx.Circle(16f, 48), new Color(1f, 1f, 1f, 0.5f), 1f);
		for (int k = 0; k < 12; k++)
		{
			Vector2 d = Vector2.Up.Rotated(k * Mathf.Tau / 12f);
			Fx.Line(emblem, new[] { d * 22f, d * (k % 3 == 0 ? 30f : 26f) }, new Color(Gold, 0.9f), k % 3 == 0 ? 2f : 1f);
		}
		Polygon2D star = Fx.Poly(emblem, Fx.Star4(11f, 0.25f), new Color(1f, 0.97f, 0.85f), additive: true);
		// 扫光：斜的白色光带周期性从左扫到右（裁在底板里：用两端渐隐代替裁剪）
		Polygon2D sweep = Fx.Poly(decor, new[] { new Vector2(0f, -4f), new Vector2(26f, -4f), new Vector2(26f - slant, size.Y + 4f), new Vector2(-slant, size.Y + 4f) }, new Color(1f, 1f, 1f, 0.22f), additive: true);
		ulong start = Time.GetTicksMsec();
		Fx.Animate(decor, 600f, _ =>
		{
			if (!GodotObject.IsInstanceValid(button))
			{
				return;
			}
			float time = (Time.GetTicksMsec() - start) / 1000f;
			float hover = button.GetGlobalRect().HasPoint(button.GetGlobalMousePosition()) || button.HasFocus() ? 1f : 0f;
			emblem.Rotation = time * (0.5f + 1.5f * hover);
			star.Scale = Vector2.One * (0.85f + 0.2f * Mathf.Sin(time * 3f));
			glow.Modulate = new Color(1f, 1f, 1f, 0.55f + 0.3f * Mathf.Sin(time * 2.2f) + 0.4f * hover);
			float u = Mathf.PosMod(time / 2.4f, 1f);
			float x = Mathf.Lerp(-40f, size.X + 40f, Ease.InOutCubic(Mathf.Clamp(u / 0.45f, 0f, 1f)));
			sweep.Position = new Vector2(x, 0f);
			float edge = Mathf.Clamp(x / 60f, 0f, 1f) * Mathf.Clamp((size.X - x) / 60f, 0f, 1f);
			sweep.Modulate = new Color(1f, 1f, 1f, u < 0.45f ? edge * (1f + hover) : 0f);
		}).SetLoops();
	}

	private static Vector2[] Close(Vector2[] pts)
	{
		Vector2[] r = new Vector2[pts.Length + 1];
		pts.CopyTo(r, 0);
		r[^1] = pts[0];
		return r;
	}

	// ───────────────────────── 结局演出 ─────────────────────────

	/// <summary>播放结局演出，Duration 秒后完成（返回 true = 已代替原版的攻击演出）。</summary>
	public static Task<bool> Play(NCreature? player, NCreature? architect)
	{
		TaskCompletionSource<bool> done = new();
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			done.SetResult(false);
			return done.Task;
		}
		ModEntry.Safe("great seal", () => Run(tree, player, architect));
		tree.CreateTimer(Duration, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () => done.TrySetResult(true);
		return done.Task;
	}

	private static void Run(SceneTree tree, NCreature? player, NCreature? architect)
	{
		Audio.Sfx.Clip("great_seal", 1.0f);
		Control? front = Makoto.FxLayer();
		Vector2 view = tree.Root.GetVisibleRect().Size;
		CanvasLayer top = new() { Layer = 90, Name = "P3GreatSeal" };
		tree.Root.AddChild(top);
		Node2D cover = new();
		top.AddChild(cover);

		// 0.0 影时间压暗（覆盖层最底下的半透明暗绿，不挡人物）
		ColorRect dim = new() { Size = view, Color = new Color(0.01f, 0.06f, 0.03f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		cover.AddChild(dim);
		// 覆盖层在眼神切入之上 → 等切入结束（≈1.2s）再压暗，不把切入横幅也压暗
		Fx.Animate(dim, 6.4f, t => dim.Color = new Color(0.01f, 0.06f, 0.03f, 0.45f * Mathf.Clamp((t * 6.4f - 1.2f) / 0.6f, 0f, 1f)));

		Vector2 chest = player != null && GodotObject.IsInstanceValid(player) ? Makoto.Center(player) : view * new Vector2(0.3f, 0.55f);
		Vector2 foe = architect != null && GodotObject.IsInstanceValid(architect) ? architect.VfxSpawnPosition : view * new Vector2(0.72f, 0.5f);

		// 0.2 眼神切入 + 召唤器
		if (player != null && front != null)
		{
			Fx.After(cover, 0.2, "seal cutin", () =>
			{
				CutInFx.Begin(player, Zh ? "伟大封印" : "THE GREAT SEAL", "UNIVERSE", null);
				Pose.MarkSummon(player);
				_ = MegaCrit.Sts2.Core.Commands.CreatureCmd.TriggerAnim(player.Entity, "Cast", 0f);
			});
		}
		Node2D world = new();
		top.AddChild(world);
		Fx.After(cover, 1.4, "seal arcana", () => Arcana(world, chest));
		Fx.After(cover, 5.3, "seal universe", () => Universe(world, chest, view));
		Fx.After(cover, 5.8, "seal chains", () => Chains(world, chest, foe, architect));
		Fx.After(cover, 6.4, "seal gate", () => Gate(top, view));
		Fx.After(cover, Duration + 1.2, "seal cleanup", () => top.QueueFree());
	}

	// 1.4–5.3 十五张大阿卡纳
	private static readonly int[] Order = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 15, 13 };   // 15 = 图集最后一格（XIX 太阳），死神压轴

	private static void Arcana(Node2D world, Vector2 chest)
	{
		if (Tex("arcana_faces") is not Texture2D atlas)
		{
			return;
		}
		Node2D ring = new() { Position = chest };
		world.AddChild(ring);
		int n = Order.Length;
		const float gap = 0.19f;
		const float total = 3.9f;
		const float rx = 250f, ry = 150f;
		const float card = 84f;
		List<(Node2D node, Sprite2D flash, Line2D thread, float born)> cards = new();
		for (int i = 0; i < n; i++)
		{
			int cell = Order[i];
			AtlasTexture face = new() { Atlas = atlas, Region = new Rect2(cell % 4 * 256, cell / 4 * 256, 256, 256) };
			Line2D thread = Fx.Line(ring, new[] { Vector2.Zero, Vector2.Zero }, new Color(Pal.Cyan, 0.4f), 1.5f);
			Node2D node = new() { Scale = Vector2.Zero };
			ring.AddChild(node);
			node.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0.4f), Scale = Vector2.One * (card * 1.9f / Fx.SoftDot.GetWidth()) });
			node.AddChild(new Sprite2D { Texture = face, Scale = Vector2.One * (card / 256f) });
			Sprite2D flash = new() { Texture = face, Scale = Vector2.One * (card / 256f), Material = Fx.Additive };
			node.AddChild(flash);
			cards.Add((node, flash, thread, i * gap));
			if (i % 2 == 0)
			{
				Audio.Sfx.Clip("ShuffleTime_CardSet", 0.35f, i * gap);
			}
		}
		Audio.Sfx.Clip("ShuffleTime_ArcanaBurst", 0.7f, 3.9f);
		Fx.Animate(ring, total, t =>
		{
			float time = t * total;
			float spin = time * 0.35f;
			float pull = Ease.InCubic(Mathf.Clamp((time - 3.2f) / 0.6f, 0f, 1f));   // 4.6 起螺旋收进胸口
			for (int i = 0; i < cards.Count; i++)
			{
				(Node2D node, Sprite2D flash, Line2D thread, float born) = cards[i];
				float lt = time - born;
				if (lt < 0f)
				{
					continue;
				}
				float a = -Mathf.Pi / 2f + i * Mathf.Tau / n + spin + pull * 2.4f;
				float r = 1f - pull * Mathf.Clamp(1f + (i - n) * 0.02f, 0f, 1f);
				Vector2 at = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry) * r + new Vector2(0f, 6f * Mathf.Sin(time * 2f + i));
				node.Position = at;
				// 翻出：先是窄条（侧面），0.18s 内展开成正面
				float flip = Ease.OutBack(Mathf.Clamp(lt / 0.18f, 0f, 1f));
				float sc = Mathf.Clamp(lt / 0.12f, 0f, 1f) * (1f - 0.6f * pull);
				node.Scale = new Vector2(flip * sc, sc);
				node.Rotation = 0.12f * Mathf.Sin(time * 1.5f + i);
				flash.Modulate = new Color(1f, 1f, 1f, Mathf.Max(0f, 1f - lt / 0.25f) * 0.9f + pull * 0.8f);
				node.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((pull - 0.85f) / 0.15f, 0f, 1f));
				thread.Points = new[] { Vector2.Zero, at };
				thread.DefaultColor = new Color(Pal.Cyan, Mathf.Clamp(lt / 0.3f, 0f, 1f) * (0.25f + 0.15f * Mathf.Sin(time * 4f + i)) * (1f - pull));
			}
		});
		Fx.FreeAfter(ring, total + 0.05);
	}

	// 5.3 宇宙爆发
	private static void Universe(Node2D world, Vector2 chest, Vector2 view)
	{
		Audio.Sfx.Clip("Theurgia_Max", 0.9f);
		NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
		Node2D root = new();
		world.AddChild(root);
		// 白金闪光
		Sprite2D burst = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = chest, Modulate = new Color(1f, 0.95f, 0.8f, 1f) };
		root.AddChild(burst);
		// 冲击环
		Node2D ringA = new() { Position = chest };
		root.AddChild(ringA);
		Fx.StrokeClosed(ringA, Fx.Circle(100f, 64), new Color(Gold, 0.9f), 6f);
		Fx.StrokeClosed(ringA, Fx.Circle(86f, 64), new Color(1f, 1f, 1f, 0.6f), 2f);
		// 12 颗金星的圆环
		Node2D stars = new() { Position = chest };
		root.AddChild(stars);
		for (int k = 0; k < 12; k++)
		{
			Vector2 d = Vector2.Up.Rotated(k * Mathf.Tau / 12f);
			Fx.Poly(stars, Fx.Star4(k % 3 == 0 ? 16f : 10f, 0.25f), k % 3 == 0 ? Pal.White : Gold, additive: true).Position = d * 170f;
		}
		Fx.StrokeClosed(stars, Fx.Circle(170f, 96), new Color(Gold, 0.45f), 1.5f);
		// 速度线
		Node2D speed = new() { Position = chest };
		root.AddChild(speed);
		for (int k = 0; k < 30; k++)
		{
			Vector2 d = Fx.RandDir();
			float r0 = Fx.Rand(130f, 200f);
			Fx.Poly(speed, new[] { d * r0 + d.Orthogonal() * Fx.Rand(2f, 6f), d * (r0 + Fx.Rand(300f, 700f)), d * r0 - d.Orthogonal() * Fx.Rand(2f, 6f) }, new Color(1f, 0.95f, 0.85f, 0.6f), additive: true);
		}
		// 黑色斜带 + 巨大的 UNIVERSE
		Node2D band = new() { Position = new Vector2(0f, view.Y * 0.2f), Rotation = -0.08f };
		root.AddChild(band);
		Fx.Poly(band, new[] { new Vector2(-100f, -view.Y * 0.07f), new Vector2(view.X + 100f, -view.Y * 0.07f), new Vector2(view.X + 100f, view.Y * 0.08f), new Vector2(-100f, view.Y * 0.08f) }, new Color(0.01f, 0.02f, 0.06f, 0.78f));
		Node2D word = new() { Skew = -0.2f };
		band.AddChild(word);
		int fs = (int)(view.Y * 0.16f);
		Typo.Text(word, "UNIVERSE", Typo.LatinItalic, fs, Pal.White, new Vector2(0f, -fs * 0.62f), outline: 4, outlineColor: new Color(0.1f, 0.3f, 0.75f));
		Typo.Text(band, Zh ? "ARCANA XXI · 宇宙" : "ARCANA XXI · THE UNIVERSE", Typo.LatinItalic, 26, Gold, new Vector2(view.X * 0.62f, view.Y * 0.035f), outline: 3, outlineColor: new Color(0.02f, 0.04f, 0.1f));
		const float total = 1.3f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float b = Ease.OutExpo(Mathf.Clamp(time / 0.25f, 0f, 1f));
			burst.Scale = Vector2.One * (Mathf.Lerp(1f, 9f, b) * 64f / Fx.SoftDot.GetWidth());
			burst.Modulate = new Color(1f, 0.95f, 0.8f, 1f - Ease.InCubic(Mathf.Clamp(time / 0.6f, 0f, 1f)));
			ringA.Scale = Vector2.One * Mathf.Lerp(0.3f, 4.2f, Ease.OutExpo(Mathf.Clamp(time / 0.6f, 0f, 1f)));
			ringA.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp(time / 0.6f, 0f, 1f));
			stars.Rotation = time * 0.6f;
			stars.Scale = Vector2.One * Ease.OutBack(Mathf.Clamp(time / 0.35f, 0f, 1f));
			stars.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 0.9f) / 0.4f, 0f, 1f));
			speed.Scale = Vector2.One * (0.6f + 0.6f * b);
			speed.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 0.15f) / 0.45f, 0f, 1f));
			float wi = Ease.OutExpo(Mathf.Clamp((time - 0.05f) / 0.3f, 0f, 1f));
			word.Position = new Vector2(Mathf.Lerp(view.X, view.X * 0.08f, wi) - 60f * time, 0f);
			band.Modulate = new Color(1f, 1f, 1f, wi * (1f - Mathf.Clamp((time - 1.0f) / 0.3f, 0f, 1f)));
		});
		Fx.FreeAfter(root, total + 0.05);
	}

	// 5.8 光链 + 封印环：建筑师褪成石像
	private static void Chains(Node2D world, Vector2 from, Vector2 foe, NCreature? architect)
	{
		Audio.Sfx.Clip("Snd_FX_Btl_StAbsorption_00_1", 0.8f);
		Node2D root = new();
		world.AddChild(root);
		List<Line2D> beams = new();
		for (int i = 0; i < 3; i++)
		{
			Line2D b = Fx.Line(root, new[] { from, from }, new Color(Gold, 0.85f), 5f - i);
			beams.Add(b);
		}
		Node2D rings = new() { Position = foe };
		root.AddChild(rings);
		List<Node2D> rs = new();
		for (int i = 0; i < 3; i++)
		{
			Node2D r = new() { Position = new Vector2(0f, (i - 1) * 70f) };
			rings.AddChild(r);
			Fx.StrokeClosed(r, Fx.Circle(150f, 64, 0.32f), new Color(Gold, 0.9f), 3f);
			for (int k = 0; k < 16; k++)
			{
				float a = k * Mathf.Tau / 16f;
				Vector2 p = new(Mathf.Cos(a) * 150f, Mathf.Sin(a) * 150f * 0.32f);
				Fx.Poly(r, Fx.Star4(5f, 0.3f), Pal.White, additive: true).Position = p;
			}
			rs.Add(r);
		}
		Color? baseMod = architect != null && GodotObject.IsInstanceValid(architect) ? architect.Modulate : null;
		const float total = 1.0f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			for (int i = 0; i < beams.Count; i++)
			{
				float head = Ease.OutExpo(Mathf.Clamp((time - i * 0.06f) / 0.2f, 0f, 1f));
				Vector2 bend = new(0f, (i - 1) * 40f * Mathf.Sin(head * Mathf.Pi));
				beams[i].Points = new[] { from, from.Lerp(foe, 0.5f) * head + from * (1f - head) + bend, from.Lerp(foe, head) };
				beams[i].DefaultColor = new Color(Gold, 0.85f * (1f - Mathf.Clamp((time - 0.6f) / 0.4f, 0f, 1f)));
			}
			for (int i = 0; i < rs.Count; i++)
			{
				float c = Ease.OutCubic(Mathf.Clamp((time - 0.15f - i * 0.07f) / 0.35f, 0f, 1f));
				rs[i].Scale = Vector2.One * Mathf.Lerp(1.8f, 0.75f, c);
				rs[i].Rotation = (i - 1) * 0.08f;
				rs[i].Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(c * 3f, 0f, 1f));
			}
			if (baseMod is Color m && architect != null && GodotObject.IsInstanceValid(architect))
			{
				float g = Ease.InOutCubic(Mathf.Clamp((time - 0.3f) / 0.6f, 0f, 1f));
				architect.Modulate = m.Lerp(new Color(0.55f, 0.62f, 0.82f, m.A), g);
			}
		});
		Fx.FreeAfter(root, total + 0.6f);
	}

	// 6.4–11.6 伟大封印
	private static void Gate(CanvasLayer top, Vector2 view)
	{
		Node2D root = new();
		top.AddChild(root);
		Texture2D? tex = Tex("great_seal");
		Node2D art = new() { Position = view / 2f };
		root.AddChild(art);
		float s = tex != null ? Mathf.Max(view.X / tex.GetWidth(), view.Y / tex.GetHeight()) * 1.04f : 1f;
		if (tex != null)
		{
			art.AddChild(new Sprite2D { Texture = tex, Scale = Vector2.One * s });
		}
		else
		{
			art.AddChild(new ColorRect { Size = view, Position = -view / 2f, Color = new Color(0.03f, 0.04f, 0.12f) });
		}
		// 门心（原图里人像胸口的位置）
		Vector2 door = view / 2f + (tex != null ? (new Vector2(760f, 380f) - new Vector2(tex.GetWidth(), tex.GetHeight()) / 2f) * s : Vector2.Zero);

		// 十四道光流：从画面四周弯向门心，逐渐画出，光头沿线跑
		List<(Line2D line, Polygon2D head, Vector2[] path, float delay)> streams = new();
		for (int i = 0; i < 14; i++)
		{
			float a = i * Mathf.Tau / 14f + Fx.Rand(-0.15f, 0.15f);
			Vector2 edge = view / 2f + Vector2.Right.Rotated(a) * view.Length() * 0.6f;
			Vector2 ctrl = door.Lerp(edge, 0.5f) + Vector2.Right.Rotated(a + Mathf.Pi / 2f) * Fx.Rand(120f, 280f);
			Vector2[] path = new Vector2[32];
			for (int k = 0; k < path.Length; k++)
			{
				float u = k / (path.Length - 1f);
				path[k] = edge * (1f - u) * (1f - u) + ctrl * 2f * u * (1f - u) + door * u * u;
			}
			Line2D line = Fx.Line(root, new[] { edge, edge }, new Color(0.75f, 0.85f, 1f, 0.55f), Fx.Rand(1.5f, 3.5f));
			Polygon2D head = Fx.Poly(root, Fx.Star4(9f, 0.3f), Pal.White, additive: true);
			streams.Add((line, head, path, Fx.Rand(0f, 0.8f)));
		}
		// 漂浮光点
		List<(Polygon2D p, Vector2 o, Vector2 v)> motes = new();
		for (int i = 0; i < 40; i++)
		{
			Polygon2D m = Fx.Poly(root, Fx.Star4(Fx.Rand(2f, 5f), 0.3f), new Color(0.8f, 0.9f, 1f), additive: true);
			motes.Add((m, new Vector2(Fx.Rand(0f, view.X), Fx.Rand(0f, view.Y)), new Vector2(Fx.Rand(-12f, 12f), Fx.Rand(-30f, -8f))));
		}
		// 门心白光
		Sprite2D glow = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = door, Modulate = new Color(1f, 1f, 1f, 0f) };
		root.AddChild(glow);
		// 标题
		Node2D title = new() { Position = new Vector2(view.X / 2f, view.Y * 0.8f), Modulate = new Color(1f, 1f, 1f, 0f) };
		root.AddChild(title);
		int tfs = (int)(view.Y * 0.085f);
		Typo.Text(title, Zh ? "伟 大 封 印" : "THE GREAT SEAL", Zh ? Typo.CjkHeavy : Typo.LatinItalic, tfs, Pal.White, new Vector2(-view.X / 2f, -tfs * 0.7f), HorizontalAlignment.Center, outline: 5, outlineColor: new Color(0.08f, 0.2f, 0.55f));
		Typo.Text(title, Zh ? "T H E   G R E A T   S E A L" : "M E M E N T O   M O R I", Typo.LatinItalic, 22, new Color(0.75f, 0.88f, 1f), new Vector2(-view.X / 2f, tfs * 0.55f), HorizontalAlignment.Center, outline: 2, outlineColor: new Color(0.02f, 0.04f, 0.12f));
		foreach (Node c in title.GetChildren())
		{
			if (c is Control ctl)
			{
				ctl.Size = new Vector2(view.X, ctl.Size.Y);
			}
		}
		// 全屏白 / 深蓝 / 结语
		ColorRect white = new() { Size = view, Color = new Color(1f, 1f, 1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(white);
		Node2D coda = new() { Position = new Vector2(view.X / 2f, view.Y * 0.5f), Modulate = new Color(1f, 1f, 1f, 0f) };
		root.AddChild(coda);
		Label codaText = Typo.Text(coda, Zh ? "M E M E N T O   M O R I" : "...and the morning came.", Typo.LatinItalic, 30, new Color(0.8f, 0.9f, 1f), new Vector2(-view.X / 2f, -20f), HorizontalAlignment.Center);
		codaText.Size = new Vector2(view.X, codaText.Size.Y);
		Label codaSub = Typo.Text(coda, Zh ? "……于是，早晨来临。" : "", Typo.Cjk, 22, new Color(0.65f, 0.75f, 0.9f), new Vector2(-view.X / 2f, 28f), HorizontalAlignment.Center);
		codaSub.Size = new Vector2(view.X, codaSub.Size.Y);

		const float total = Duration - 6.4f + 1.0f;   // 6.4 → 11.6，再加 1s 淡出
		Fx.Animate(root, total, t =>
		{
			float time = t * total;                  // 0 = 6.4s
			root.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.5f, 0f, 1f) * (1f - Mathf.Clamp((time - (total - 1f)) / 1f, 0f, 1f)));
			art.Scale = Vector2.One * (1f + 0.07f * Ease.InOutCubic(Mathf.Clamp(time / 3.6f, 0f, 1f)));
			art.Position = view / 2f + (door - view / 2f) * 0.07f * Ease.InOutCubic(Mathf.Clamp(time / 3.6f, 0f, 1f)) * -1f;
			foreach ((Line2D line, Polygon2D head, Vector2[] path, float delay) in streams)
			{
				float u = Ease.InOutCubic(Mathf.Clamp((time - delay) / 1.6f, 0f, 1f));
				int nPts = Mathf.Max(2, (int)(u * (path.Length - 1)) + 1);
				Vector2[] pts = new Vector2[nPts];
				System.Array.Copy(path, pts, nPts);
				line.Points = pts;
				float run = Mathf.PosMod((time - delay) / 1.1f, 1f);
				head.Position = path[Mathf.Clamp((int)(run * (path.Length - 1)), 0, path.Length - 1)];
				head.Modulate = new Color(1f, 1f, 1f, u >= 1f ? 0.9f : 0f);
				line.DefaultColor = new Color(0.75f, 0.85f, 1f, 0.35f + 0.2f * Mathf.Sin(time * 5f + delay * 9f));
			}
			foreach ((Polygon2D p, Vector2 o, Vector2 v) in motes)
			{
				p.Position = o + v * time;
				p.Modulate = new Color(1f, 1f, 1f, 0.3f + 0.3f * Mathf.Sin(time * 3f + o.X));
			}
			// 门心白光：3.0 起暴涨 → 3.5 全屏白（= 9.9s）
			float g = Ease.InCubic(Mathf.Clamp((time - 2.6f) / 0.9f, 0f, 1f));
			glow.Scale = Vector2.One * (Mathf.Lerp(2f, 40f, g) * 64f / Fx.SoftDot.GetWidth());
			glow.Modulate = new Color(1f, 1f, 1f, 0.3f + 0.7f * g);
			float ti = Ease.OutCubic(Mathf.Clamp((time - 0.6f) / 0.8f, 0f, 1f));
			title.Modulate = new Color(1f, 1f, 1f, ti * (1f - Mathf.Clamp((time - 3.0f) / 0.4f, 0f, 1f)));
			title.Scale = Vector2.One * (1.06f - 0.06f * ti);
			// 白：3.3–3.5 涨满，4.0 起转深蓝
			float w = Mathf.Clamp((time - 3.25f) / 0.25f, 0f, 1f);
			float toNavy = Ease.InOutCubic(Mathf.Clamp((time - 4.0f) / 0.8f, 0f, 1f));
			white.Color = new Color(1f, 1f, 1f, w).Lerp(new Color(0.02f, 0.04f, 0.1f, w), toNavy);
			coda.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 4.4f) / 0.6f, 0f, 1f));
			if (time > 3.5f && time < 3.6f)
			{
				NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short);
			}
		});
	}
}
