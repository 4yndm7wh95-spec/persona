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
/// 结局演出（Play，约 13.6s，配 Nyx 战「封印」原声 great_seal）：分段时间见 Duration 上方的注释（v0.14.9 按原版视频重排）。
/// </summary>
public static class GreatSeal
{
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
		// v14 新背景（Codex 按站位要求重画：站人线 y=740/1152）优先；没有就用 v13 的图（平台前沿 y=745/1024）
		Texture2D? v2 = Tex("tartarus_top_v2");
		if (bg.GetNodeOrNull("P3TartarusTop") != null || (v2 ?? Tex("tartarus_top")) is not Texture2D tex)
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
		// v0.14.9（用户：人物的水平 / 高低位置和背景不符）：按人物脚底对齐 —— 原图里圆形平台的前沿在 y≈745，
		// 让它落在脚底上方 6% 屏高处（人物站在平台地面上，而不是楼梯 / 墙上）；放大到能盖住下边，水平居中
		float feetY = view.Y * 0.66f;
		int cnt = 0;
		float sum = 0f;
		foreach (NCreature c in room.CreatureNodes)
		{
			if (GodotObject.IsInstanceValid(c))
			{
				sum += c.GetBottomOfHitbox().Y;
				cnt++;
			}
		}
		if (cnt > 0)
		{
			feetY = sum / cnt;
		}
		float s;
		Vector2 origin;
		if (v2 != null)
		{
			// 新图：把图里的站人线（740/1152）对准人物脚底，放大到盖满（上下都留余量）
			float standSrc = 740f / 1152f * tex.GetHeight();
			s = Mathf.Max(W / tex.GetWidth(), H / tex.GetHeight());
			s = Mathf.Max(s, (feetY + margin) / standSrc);
			s = Mathf.Max(s, (view.Y + margin - feetY) / (tex.GetHeight() - standSrc));
			origin = new Vector2(-margin + (W - tex.GetWidth() * s) / 2f, feetY - standSrc * s);
		}
		else
		{
			const float rimSrc = 745f;
			float rimY = feetY - view.Y * 0.06f;
			s = Mathf.Max(Mathf.Max(W / tex.GetWidth(), H / tex.GetHeight()) * 1.05f, (view.Y + margin - rimY) / (tex.GetHeight() - rimSrc));
			origin = new Vector2(-margin + (W - tex.GetWidth() * s) / 2f, rimY - rimSrc * s);
		}
		Vector2 artSize = new(tex.GetWidth() * s, tex.GetHeight() * s);
		// 前景碎石（新图才有）：叠在人物前面（前层特效容器），增加纵深
		if (v2 != null && Tex("tartarus_top_v2_fg") is Texture2D fg && room.GetNodeOrNull<Control>("%CombatVfxContainer") is Control fgLayer)
		{
			Sprite2D fgs = new() { Name = "P3TartarusFg", Texture = fg, Centered = false, Scale = Vector2.One * s };
			fgLayer.AddChild(fgs);
			fgs.GlobalPosition = origin;
		}
		// 原图里月亮的中心 / 半径（v14 A 版 1672×941 实测：(992, 208)、r≈165）
		Vector2 moonAt = origin + (v2 != null ? new Vector2(992f, 208f) * (tex.GetWidth() / 1672f) : new Vector2(899f, 70f)) * s;
		float moonR = (v2 != null ? 165f * tex.GetWidth() / 1672f : 230f) * s;

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
		// ── v0.14.9 更多会动的细节（用户：只有一张背景图显得廉价）──
		// 月光光束：从月亮向下扇形散开的几道极淡的加色光带，缓慢摆动
		Node2D rays = new() { Position = moonAt };
		art.AddChild(rays);
		var rayList = new List<(Polygon2D p, float a, float ph)>();
		for (int k = 0; k < 7; k++)
		{
			float a = Mathf.Pi / 2f + (k - 3) * 0.17f + Fx.Rand(-0.05f, 0.05f);
			float len = view.Y * Fx.Rand(1.1f, 1.5f), wid = Fx.Rand(40f, 110f);
			Polygon2D r = Fx.Poly(rays, new[] { new Vector2(0f, -wid * 0.15f), new Vector2(len, -wid), new Vector2(len, wid), new Vector2(0f, wid * 0.15f) }, new Color(0.7f, 1f, 0.75f, 0.05f), additive: true);
			r.Rotation = a;
			rayList.Add((r, a, Fx.Rand(0f, Mathf.Tau)));
		}
		// 漂浮的碎石：塔顶崩落的石块剪影，在半空缓慢上浮、自转（循环）
		var debris = new List<(Polygon2D p, float x, float y0, float speed, float spin, float ph)>();
		for (int k = 0; k < 12; k++)
		{
			float r0 = Fx.Rand(6f, 24f);
			Vector2[] pts = new Vector2[5];
			for (int j = 0; j < 5; j++)
			{
				pts[j] = Vector2.Right.Rotated(j * Mathf.Tau / 5f + Fx.Rand(-0.3f, 0.3f)) * r0 * Fx.Rand(0.6f, 1.1f);
			}
			Polygon2D d = Fx.Poly(art, pts, new Color(0.03f, 0.08f, 0.05f, 0.85f));
			Fx.Line(d, new[] { pts[0], pts[1], pts[2] }, new Color(0.5f, 1f, 0.6f, 0.35f), 1.2f);   // 朝月亮一侧的亮边
			debris.Add((d, Fx.Rand(0f, view.X), Fx.Rand(view.Y * 0.1f, view.Y * 0.55f), Fx.Rand(4f, 12f), Fx.Rand(-0.4f, 0.4f), Fx.Rand(0f, 1f)));
		}
		// 湿地面的反光：平台地面上若隐若现的横向细光
		var glints = new List<(Polygon2D p, float ph, float rate)>();
		float floorTop = origin.Y + (v2 != null ? 620f / 1152f * tex.GetHeight() : 760f) * s;
		for (int k = 0; k < 16; k++)
		{
			float y = Fx.Rand(floorTop + 20f, view.Y + 20f);
			float len = Fx.Rand(60f, 220f) * (0.6f + 0.6f * (y - floorTop) / Mathf.Max(1f, view.Y - floorTop));
			Polygon2D g = Fx.Poly(art, new[] { new Vector2(-len / 2f, 0f), new Vector2(0f, -1.5f), new Vector2(len / 2f, 0f), new Vector2(0f, 1.5f) }, new Color(0.75f, 1f, 0.8f, 1f), additive: true);
			g.Position = new Vector2(Fx.Rand(0f, view.X), y);
			glints.Add((g, Fx.Rand(0f, Mathf.Tau), Fx.Rand(0.4f, 1.1f)));
		}
		// 平台前沿贴地的薄雾带（比上面的绿雾更低、更扁）
		var lowMist = new List<(Sprite2D sp, float speed, float ph)>();
		for (int k = 0; k < 5; k++)
		{
			Sprite2D m = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(0.55f, 0.9f, 0.65f, 0.1f), Scale = new Vector2(Fx.Rand(10f, 16f), 1.1f) * (view.X / 1920f) * (64f / dotSize) };
			art.AddChild(m);
			lowMist.Add((m, Fx.Rand(14f, 30f) * (k % 2 == 0 ? 1f : -1f), Fx.Rand(0f, 1f)));
		}
		// 远处云层里的闪电：每 5~9 秒天空闪一下（两次短闪）
		ColorRect sky = new() { Size = new Vector2(W, H * 0.55f), Position = new Vector2(-margin, -margin), Color = new Color(0.7f, 1f, 0.75f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore, Material = Fx.Additive };
		root.AddChild(sky);
		float nextBolt = Fx.Rand(3f, 6f);
		float boltAt = -10f;

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
			foreach ((Polygon2D r, float a, float ph) in rayList)
			{
				r.Rotation = a + 0.03f * Mathf.Sin(time * 0.25f + ph);
				r.Modulate = new Color(1f, 1f, 1f, 0.6f + 0.4f * Mathf.Sin(time * 0.4f + ph));
			}
			foreach ((Polygon2D d, float x, float y0, float speed, float spin, float ph) in debris)
			{
				float span = view.Y * 0.7f;
				float u = Mathf.PosMod(ph + speed * time / span, 1f);
				d.Position = new Vector2(x + 14f * Mathf.Sin(time * 0.3f + ph * 7f), y0 + view.Y * 0.2f - u * span);
				d.Rotation = spin * time + ph * 6f;
				d.Modulate = new Color(1f, 1f, 1f, Mathf.Sin(u * Mathf.Pi));
			}
			foreach ((Polygon2D g, float ph, float rate) in glints)
			{
				float v = Mathf.Max(0f, Mathf.Sin(time * rate + ph));
				g.Modulate = new Color(1f, 1f, 1f, 0.22f * v * v);
			}
			foreach ((Sprite2D m, float speed, float ph) in lowMist)
			{
				float span = view.X + 800f;
				m.Position = new Vector2(Mathf.PosMod(ph * span + speed * time, span) - 400f, floorTop + 6f * Mathf.Sin(time * 0.5f + ph * 5f));
			}
			if (time >= nextBolt)
			{
				boltAt = time;
				nextBolt = time + Fx.Rand(5f, 9f);
			}
			float bt = time - boltAt;
			float bolt = bt < 0f ? 0f : bt < 0.07f ? 1f : bt < 0.14f ? 0.2f : bt < 0.2f ? 0.7f : Mathf.Max(0f, 0.7f - (bt - 0.2f) * 2f);
			sky.Color = new Color(0.7f, 1f, 0.75f, 0.14f * bolt);
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
	// v0.14.9（用户给了原版伟大封印的视频：不是召唤器，而是单手指天；塔罗牌展示和原版关系不大）——按原版重排：
	//   0.00  P3R 技能菜单：左侧巨大的蓝色平涂剪影（结城理立绘压平）+ 斜切深蓝带上的超大「PERSONA」，
	//         右上「宇宙」阿卡纳栏，技能列表里「伟大封印」被选中（白底黑字），下方两行说明；1.30 确认一闪
	//   1.70  左上技能名横幅「伟大封印」；画面转黑
	//   2.30  电影遮幅（上下黑边）：昏暗的塔顶，结城理脚下亮起蓝白光圈，光雾和细长的光丝从地面升起、光点上飘
	//   3.60  单手指天（有 point_sky 动画就播，没有也照样）：头顶一道细光柱直冲天顶
	//   4.60  面部特写（P3R 原版立绘 + 眼部差分）：闭眼 → 5.60 半睁 → 5.80 睁开，蓝光在身后流动，镜头缓推
	//   6.60  回到塔顶：地面中央升起一团光穹，十几道光束从地面扇形射向上方的建筑师，建筑师被三圈封印环锁住、褪成石色
	//   8.70  光越来越强 → 9.60 全屏白（原声高潮）
	//   9.60  白里浮现「伟大封印」巨门（10.2 褪尽白），光流汇向门中人像，标题；12.2 再次全屏白 → 12.5 转深蓝「MEMENTO MORI」
	//  13.60  交还给游戏的胜利流程；覆盖层再 0.8s 淡出后清理
	public const float Duration = 13.6f;

	private static Texture2D? UiTex(string name)
	{
		string path = $"res://p3makoto/ui/{name}.png";
		return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
	}

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
		Vector2 view = tree.Root.GetVisibleRect().Size;
		CanvasLayer top = new() { Layer = 90, Name = "P3GreatSeal" };
		tree.Root.AddChild(top);
		Node2D world = new();       // 世界坐标上的效果（脚下光雾、光束……），在遮幅和压暗之上
		Node2D ui = new();          // 菜单 / 特写 / 白幕
		Vector2 feet = player != null && GodotObject.IsInstanceValid(player) ? Makoto.Feet(player) : view * new Vector2(0.3f, 0.7f);
		Vector2 head = player != null && GodotObject.IsInstanceValid(player) ? Makoto.Head(player) : view * new Vector2(0.3f, 0.35f);
		Vector2 foe = architect != null && GodotObject.IsInstanceValid(architect) ? architect.VfxSpawnPosition : view * new Vector2(0.72f, 0.5f);

		// 压暗层 + 黑场 + 遮幅（都在 world 下面）
		ColorRect dim = new() { Size = view, Color = new Color(0.01f, 0.03f, 0.06f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		top.AddChild(dim);
		top.AddChild(world);
		float barH = view.Y * 0.12f;
		ColorRect barTop = new() { Size = new Vector2(view.X, barH), Position = new Vector2(0f, -barH), Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
		ColorRect barBot = new() { Size = new Vector2(view.X, barH), Position = new Vector2(0f, view.Y), Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
		top.AddChild(barTop);
		top.AddChild(barBot);
		top.AddChild(ui);
		Fx.Animate(dim, 9.6f, t =>
		{
			float time = t * 9.6f;
			// 1.9–2.3 转黑；2.3–2.9 从黑里淡出到"昏暗的塔顶"（压暗 55%）；面部特写期间被特写盖住
			float a = time < 1.9f ? 0f : time < 2.3f ? (time - 1.9f) / 0.4f : Mathf.Lerp(1f, 0.55f, Mathf.Clamp((time - 2.3f) / 0.6f, 0f, 1f));
			dim.Color = new Color(0.01f, 0.03f, 0.06f, a);
			float bars = Ease.OutCubic(Mathf.Clamp((time - 2.2f) / 0.4f, 0f, 1f));
			barTop.Position = new Vector2(0f, -barH + barH * bars);
			barBot.Position = new Vector2(0f, view.Y - barH * bars);
		});

		SkillMenu(ui, view);
		Fx.After(ui, 1.7, "seal banner", () => { if (Makoto.FxLayer() is Control front) Uniq.SkillBanner(front, "伟大封印", "THE GREAT SEAL", 1.2f); });
		Fx.After(world, 2.3, "seal rise", () => Rise(world, feet, view));
		Fx.After(world, 3.6, "seal point", () =>
		{
			PointSky(player);
			SkyBeam(world, head, view);
		});
		Fx.After(ui, 4.6, "seal face", () => FaceCloseUp(ui, view, barH));
		Fx.After(world, 6.6, "seal dome", () => Dome(world, feet, foe, architect, view));
		Fx.After(ui, 8.7, "seal white", () => WhiteRise(ui, view));
		Fx.After(ui, 9.6, "seal gate", () => Gate(top, view));
		Fx.After(ui, Duration + 0.8, "seal cleanup", () => top.QueueFree());
	}

	// 0.00 P3R 技能菜单
	private static void SkillMenu(Node2D ui, Vector2 view)
	{
		Node2D m = new();
		ui.AddChild(m);
		float W = view.X, H = view.Y;
		// 斜切深蓝带 + 超大 PERSONA（被裁切）
		Node2D band = new() { Position = new Vector2(W * 0.2f, H * 0.62f), Rotation = -0.32f };
		m.AddChild(band);
		Fx.Poly(band, new[] { new Vector2(-W, -H * 0.16f), new Vector2(W, -H * 0.16f), new Vector2(W, H * 0.16f), new Vector2(-W, H * 0.16f) }, new Color(0.03f, 0.14f, 0.62f, 0.95f));
		Fx.Poly(band, new[] { new Vector2(-W, H * 0.16f), new Vector2(W, H * 0.16f), new Vector2(W, H * 0.175f), new Vector2(-W, H * 0.175f) }, Pal.Cyan, additive: true);
		Node2D word = new();
		band.AddChild(word);
		// 原版「PERSONA」字形（PLG_UI_Battle Skill_TitleLogo，白色多边形字）：压成深蓝、外描一圈亮蓝
		if (UiTex("persona_logo") is Texture2D plogo)
		{
			float pls = H * 0.42f / plogo.GetHeight();
			foreach (Vector2 o in new[] { new Vector2(-6, 0), new Vector2(6, 0), new Vector2(0, -6), new Vector2(0, 6) })
			{
				word.AddChild(new Sprite2D { Texture = plogo, Scale = Vector2.One * pls, Position = new Vector2(-W * 0.05f, 0f) + o, Modulate = new Color(0.25f, 0.55f, 1f) });
			}
			word.AddChild(new Sprite2D { Texture = plogo, Scale = Vector2.One * pls, Position = new Vector2(-W * 0.05f, 0f), Modulate = new Color(0.02f, 0.05f, 0.25f) });
		}
		else
		{
			int fs = (int)(H * 0.3f);
			word.Skew = -0.18f;
			Typo.Text(word, "PERSONA", Typo.LatinItalic, fs, new Color(0.02f, 0.05f, 0.25f), new Vector2(-W * 0.2f, -fs * 0.6f), outline: 6, outlineColor: new Color(0.25f, 0.55f, 1f));
		}
		// 剪影
		Texture2D? sil = UiTex("bu_silhouette");
		Sprite2D? fig = null;
		if (sil != null)
		{
			fig = new Sprite2D { Texture = sil, Scale = Vector2.One * (H * 1.1f / sil.GetHeight()), Position = new Vector2(W * 0.2f, H * 0.6f) };
			m.AddChild(fig);
		}
		// 右上：阿卡纳栏 + 技能列表 + 说明
		Node2D panel = new() { Position = new Vector2(W * 0.5f, H * 0.12f) };
		m.AddChild(panel);
		float pw = W * 0.44f;
		Fx.Poly(panel, new[] { new Vector2(0f, 0f), new Vector2(pw, 0f), new Vector2(pw - 16f, 44f), new Vector2(-16f, 44f) }, new Color(0.95f, 0.97f, 1f, 0.95f));
		Typo.Text(panel, Zh ? "宇宙" : "UNIVERSE", Typo.CjkHeavy, 26, new Color(0.05f, 0.1f, 0.3f), new Vector2(24f, 6f));
		Typo.Text(panel, "ARCANA", Typo.LatinItalic, 16, new Color(0.2f, 0.4f, 0.8f), new Vector2(pw - 110f, 14f));
		Node2D row = new() { Position = new Vector2(-30f, 70f) };
		panel.AddChild(row);
		Node2D hl = new();
		row.AddChild(hl);
		if (UiTex("skill_base") is Texture2D sb)
		{
			// 原版技能条底板（PLG_UI_Battle SkillName_base_2）拉到条目宽度
			hl.AddChild(new Sprite2D { Texture = sb, Centered = false, Scale = new Vector2(pw / sb.GetWidth(), 52f / sb.GetHeight()), Position = new Vector2(-14f, 0f) });
		}
		else
		{
			Fx.Poly(hl, new[] { new Vector2(0f, 0f), new Vector2(pw, 0f), new Vector2(pw - 14f, 52f), new Vector2(-14f, 52f) }, Pal.White);
		}
		Fx.Poly(row, new[] { new Vector2(12f, 14f), new Vector2(46f, 14f), new Vector2(40f, 38f), new Vector2(6f, 38f) }, new Color(0.05f, 0.1f, 0.3f));   // 技能图标块
		Typo.Text(row, Zh ? "伟大封印" : "The Great Seal", Typo.CjkHeavy, 30, new Color(0.03f, 0.05f, 0.12f), new Vector2(60f, 6f));
		Typo.Text(row, "∞", Typo.LatinItalic, 30, new Color(0.03f, 0.05f, 0.12f), new Vector2(pw - 70f, 4f));
		Node2D desc = new() { Position = new Vector2(-10f, 140f) };
		panel.AddChild(desc);
		Typo.Text(desc, Zh ? "解放内在的宇宙后获得的力量。" : "Power drawn from the universe within.", Typo.Cjk, 22, new Color(0.6f, 0.9f, 1f), Vector2.Zero, outline: 2, outlineColor: new Color(0.02f, 0.05f, 0.15f));
		Typo.Text(desc, Zh ? "以自身为封印，封住一切的终结。" : "Become the seal that holds back the end.", Typo.Cjk, 22, new Color(0.6f, 0.9f, 1f), new Vector2(0f, 32f), outline: 2, outlineColor: new Color(0.02f, 0.05f, 0.15f));
		// 原版战斗菜单音：面板弹出 / 光标落到技能上 / 确认
		Audio.Sfx.Clip("Btl_CommandPanel", 0.8f, 0.05f);
		Audio.Sfx.Clip("Sys_Cursor", 0.8f, 0.55f);
		Audio.Sfx.Clip("Btl_Ok", 0.9f, 1.3f);
		const float total = 1.95f;
		Fx.Animate(m, total, t =>
		{
			float time = t * total;
			float inn = Ease.OutExpo(Mathf.Clamp(time / 0.3f, 0f, 1f));
			float outT = Ease.InCubic(Mathf.Clamp((time - 1.6f) / 0.3f, 0f, 1f));
			if (fig != null)
			{
				fig.Position = new Vector2(W * 0.2f - W * 0.3f * (1f - inn) - W * 0.3f * outT, H * 0.6f);
			}
			band.Position = new Vector2(W * 0.2f + W * 0.5f * (1f - inn), H * 0.62f);
			word.Position = new Vector2(-40f * time, 0f);
			panel.Position = new Vector2(W * 0.5f + W * 0.5f * (1f - Ease.OutExpo(Mathf.Clamp((time - 0.15f) / 0.3f, 0f, 1f))) + W * 0.5f * outT, H * 0.12f);
			float sel = Mathf.Clamp((time - 0.55f) / 0.1f, 0f, 1f);
			float confirm = time > 1.3f ? Mathf.Max(0f, 1f - (time - 1.3f) / 0.25f) : 0f;
			hl.Modulate = new Color(1f, 1f + 0.0f * confirm, 1f, sel);
			row.Scale = new Vector2(1f + 0.04f * confirm, 1f + 0.04f * confirm);
			desc.Modulate = new Color(1f, 1f, 1f, sel);
			m.Modulate = new Color(1f + confirm * 0.6f, 1f + confirm * 0.6f, 1f + confirm * 0.6f, 1f - outT);
		});
		Fx.FreeAfter(m, total + 0.05f);
	}

	// 2.30 脚下光雾升起
	private static void Rise(Node2D world, Vector2 feet, Vector2 view)
	{
		Node2D r = new() { Position = feet };
		world.AddChild(r);
		Sprite2D pool = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(0.55f, 0.85f, 1f, 0f), Scale = new Vector2(7f, 1.6f) * (64f / Fx.SoftDot.GetWidth()) };
		r.AddChild(pool);
		Node2D ring = new() { Scale = new Vector2(1f, 0.3f) };
		r.AddChild(ring);
		Fx.StrokeClosed(ring, Fx.Circle(120f, 64), new Color(0.75f, 0.95f, 1f, 0.8f), 3f);
		Fx.StrokeClosed(ring, Fx.Circle(150f, 64), new Color(0.5f, 0.85f, 1f, 0.4f), 1.5f);
		// 细长光丝：从地面升起、越升越淡
		var wisps = new List<(Line2D l, float x, float speed, float len, float ph)>();
		for (int i = 0; i < 22; i++)
		{
			Line2D l = new() { Width = Fx.Rand(1.5f, 4f), Material = Fx.Additive, DefaultColor = new Color(0.7f, 0.92f, 1f, 0f), Points = new[] { Vector2.Zero, Vector2.Zero } };
			r.AddChild(l);
			wisps.Add((l, Fx.Rand(-130f, 130f), Fx.Rand(120f, 260f), Fx.Rand(60f, 160f), Fx.Rand(0f, 1f)));
		}
		var motes = new List<(Polygon2D p, float x, float speed, float ph)>();
		for (int i = 0; i < 30; i++)
		{
			Polygon2D p = Fx.Poly(r, Fx.Star4(Fx.Rand(3f, 7f), 0.3f), Pal.Ice, additive: true);
			motes.Add((p, Fx.Rand(-200f, 200f), Fx.Rand(40f, 110f), Fx.Rand(0f, 1f)));
		}
		const float total = 4.4f;   // 2.3 → 6.7（面部特写期间被盖住，回来时还在）
		Fx.Animate(r, total, t =>
		{
			float time = t * total;
			float on = Mathf.Clamp(time / 0.6f, 0f, 1f);
			pool.Modulate = new Color(0.55f, 0.85f, 1f, (0.5f + 0.2f * Mathf.Sin(time * 3f)) * on);
			ring.Rotation = time * 0.4f;
			ring.Modulate = new Color(1f, 1f, 1f, on * (0.7f + 0.3f * Mathf.Sin(time * 4f)));
			foreach (var (l, x, speed, len, ph) in wisps)
			{
				float u = Mathf.PosMod(ph + time * speed / 420f, 1f);
				float y = -u * 420f;
				l.Points = new[] { new Vector2(x + 8f * Mathf.Sin(time * 2f + ph * 9f), y), new Vector2(x, y + len) };
				l.DefaultColor = new Color(0.7f, 0.92f, 1f, Mathf.Sin(u * Mathf.Pi) * 0.7f * on);
			}
			foreach (var (p, x, speed, ph) in motes)
			{
				float u = Mathf.PosMod(ph + time * speed / 500f, 1f);
				p.Position = new Vector2(x + 20f * Mathf.Sin(time + ph * 7f), -u * 500f);
				p.Modulate = new Color(1f, 1f, 1f, Mathf.Sin(u * Mathf.Pi) * on);
				p.Rotation = time + ph;
			}
		});
		Fx.FreeAfter(r, total + 0.05f);
	}

	/// <summary>单手指天：骨骼里有 point_sky（_in / _loop）动画就播放，结束后回待机；没有就跳过（光柱照样升起）。</summary>
	private static void PointSky(NCreature? player)
	{
		if (player == null || !GodotObject.IsInstanceValid(player) || player.Visuals.SpineBody is not { } body)
		{
			return;
		}
		var state = body.GetAnimationState();
		if (body.HasAnimation("point_sky_in"))
		{
			state.SetAnimation("point_sky_in", loop: false);
			state.AddAnimation(body.HasAnimation("point_sky_loop") ? "point_sky_loop" : "idle_loop", 0f, loop: true);
		}
		else if (body.HasAnimation("point_sky"))
		{
			state.SetAnimation("point_sky", loop: false);
			state.AddAnimation("idle_loop", 0f, loop: true);
		}
	}

	// 3.60 指天：头顶一道细光柱直冲天顶
	private static void SkyBeam(Node2D world, Vector2 head, Vector2 view)
	{
		Node2D b = new() { Position = head + new Vector2(10f, -60f) };
		world.AddChild(b);
		Line2D core = new() { Width = 6f, Material = Fx.Additive, DefaultColor = new Color(1f, 1f, 1f, 0.95f), Points = new[] { Vector2.Zero, Vector2.Zero } };
		Line2D glow = new() { Width = 40f, Material = Fx.Additive, DefaultColor = new Color(0.5f, 0.85f, 1f, 0.35f), Points = new[] { Vector2.Zero, Vector2.Zero } };
		b.AddChild(glow);
		b.AddChild(core);
		Polygon2D star = Fx.Poly(b, Fx.Star4(40f, 0.12f), Pal.White, additive: true);
		float len = b.Position.Y + 40f;
		const float total = 1.1f;
		Fx.Animate(b, total, t =>
		{
			float time = t * total;
			float g = Ease.OutExpo(Mathf.Clamp(time / 0.25f, 0f, 1f));
			core.Points = new[] { Vector2.Zero, new Vector2(0f, -len * g) };
			glow.Points = core.Points;
			float fade = 1f - Mathf.Clamp((time - 0.7f) / 0.4f, 0f, 1f);
			core.Width = 6f * fade + 1f;
			b.Modulate = new Color(1f, 1f, 1f, fade);
			star.Scale = Vector2.One * (time < 0.2f ? Ease.OutBack(time / 0.2f) : 1f - 0.5f * Mathf.Clamp((time - 0.2f) / 0.5f, 0f, 1f));
			star.Rotation = time;
		});
		Audio.Sfx.Clip("Theurgia_Up", 0.6f);
		Fx.FreeAfter(b, total + 0.05f);
	}

	// 4.60 面部特写（v0.14.9：P3R 立绘的眼部 / 嘴部差分要配合原版材质分层，直接叠会有亮色接缝和重影 → 改用总攻击结束立绘的脸）：
	//      先是暗蓝调（A2），蓝光从身后流过；1.0s 一道光从左到右扫过脸，亮成本色（A1），两只眼睛各闪一下青光 —— "睁眼"的那一拍
	private static void FaceCloseUp(Node2D ui, Vector2 view, float barH)
	{
		Texture2D? lit = UiTex("allout_finish");
		Texture2D? dark = UiTex("allout_finish_dark");
		if (lit == null)
		{
			return;
		}
		Node2D n = new();
		ui.AddChild(n);
		float W = view.X, H = view.Y;
		ColorRect bg = new() { Size = new Vector2(W, H - barH * 2f), Position = new Vector2(0f, barH), Color = new Color(0.03f, 0.14f, 0.38f), MouseFilter = Control.MouseFilterEnum.Ignore };
		n.AddChild(bg);
		var streams = new List<(Polygon2D p, float y, float ph, float speed)>();
		for (int i = 0; i < 12; i++)
		{
			float y = Fx.Rand(barH, H - barH);
			Polygon2D p = Fx.Poly(n, new[] { new Vector2(0f, -12f), new Vector2(W * 0.5f, -3f), new Vector2(W * 0.5f, 3f), new Vector2(0f, 12f) }, new Color(0.5f, 0.85f, 1f, 0.16f), additive: true);
			streams.Add((p, y, Fx.Rand(0f, 1f), Fx.Rand(0.15f, 0.35f)));
		}
		Vector2 faceCenter = new(1075f, 300f);                        // allout_finish 里脸的中心
		Vector2[] eyes = { new(1005f, 255f), new(1145f, 268f) };
		float sc = H * 1.75f / lit.GetHeight();
		Node2D face = new() { Position = new Vector2(W * 0.55f, H * 0.52f) };
		n.AddChild(face);
		Sprite2D litS = new() { Texture = lit, Centered = false, Scale = Vector2.One * sc, Position = -faceCenter * sc };
		face.AddChild(litS);
		Sprite2D? darkS = dark != null ? new Sprite2D { Texture = dark, Centered = false, Scale = Vector2.One * sc, Position = -faceCenter * sc } : null;
		if (darkS != null) face.AddChild(darkS);
		Polygon2D sweep = Fx.Poly(face, new[] { new Vector2(-60f, -H), new Vector2(60f, -H), new Vector2(160f, H), new Vector2(40f, H) }, new Color(0.85f, 0.97f, 1f, 0.5f), additive: true);
		// 闭眼 / 半睁差分（Codex 从结束立绘编辑，art/v14/face；只裁了眼部 224×99，左上角在立绘的 (967,218)）：
		// 亮色版叠在 litS 上，暗色版（同一份编辑按原版 亮 → 暗 的颜色对照表转出来）叠在 darkS 上、随它一起褪。没有就保持暗 → 亮 + 扫光的做法
		Vector2 eyeAt = new(967f, 218f);
		Sprite2D? Eye(Sprite2D? host, string name, bool visible)
		{
			if (host == null || UiTex(name) is not Texture2D tx)
			{
				return null;
			}
			Sprite2D e = new() { Texture = tx, Centered = false, Position = eyeAt, Visible = visible };
			host.AddChild(e);                                         // 子节点：跟着宿主的缩放 / 位置 / 透明度
			return e;
		}
		Sprite2D? closedS = Eye(litS, "face_closed", true), halfS = Eye(litS, "face_half", false);
		Sprite2D? closedD = Eye(darkS, "face_closed_dark", true), halfD = Eye(darkS, "face_half_dark", false);
		var glints = new List<Polygon2D>();
		foreach (Vector2 e in eyes)
		{
			Polygon2D g = Fx.Poly(face, Fx.Star4(46f, 0.1f), new Color(0.6f, 0.95f, 1f), additive: true);
			g.Position = (e - faceCenter) * sc;
			g.Scale = Vector2.Zero;
			glints.Add(g);
		}
		var motes = new List<(Polygon2D p, Vector2 o, float ph)>();
		for (int i = 0; i < 26; i++)
		{
			Polygon2D p = Fx.Poly(n, Fx.Star4(Fx.Rand(3f, 8f), 0.3f), Pal.Ice, additive: true);
			motes.Add((p, new Vector2(Fx.Rand(0f, W), Fx.Rand(barH, H - barH)), Fx.Rand(0f, Mathf.Tau)));
		}
		Audio.Sfx.Clip("Snd_FX_Btl_PersonaAura_00", 0.6f, 1.0f);
		const float total = 2.05f;
		Fx.Animate(n, total, t =>
		{
			float time = t * total;
			face.Scale = Vector2.One * (1f + 0.06f * time);
			face.Position = new Vector2(W * 0.55f - 20f * time, H * 0.52f + 8f * time);
			float wake = Mathf.Clamp((time - 1.0f) / 0.3f, 0f, 1f);
			if (darkS != null) darkS.Modulate = new Color(1f, 1f, 1f, 1f - wake);
			litS.Modulate = new Color(1f, 1f, 1f, darkS != null ? 1f : 0.4f + 0.6f * wake);
			float sw = Mathf.Clamp((time - 0.9f) / 0.4f, 0f, 1f);
			sweep.Position = new Vector2(Mathf.Lerp(-W * 0.6f, W * 0.6f, Ease.InOutCubic(sw)), 0f);
			sweep.Modulate = new Color(1f, 1f, 1f, sw > 0f && sw < 1f ? 1f : 0f);
			// 闭眼 → 1.0 半睁 → 1.15 睁开（和暗 → 亮的褪色、扫光、眼中青光同一拍）
			bool closed = time < 1.0f, half = time >= 1.0f && time < 1.15f;
			if (closedS != null) closedS.Visible = closed;
			if (closedD != null) closedD.Visible = closed;
			if (halfS != null) halfS.Visible = half;
			if (halfD != null) halfD.Visible = half;
			float gl = time < 1.15f ? 0f : time < 1.3f ? Ease.OutBack((time - 1.15f) / 0.15f) : Mathf.Max(0f, 1f - (time - 1.3f) / 0.35f);
			foreach (Polygon2D g in glints)
			{
				g.Scale = Vector2.One * gl;
				g.Rotation = time;
			}
			foreach (var (p, y, ph, speed) in streams)
			{
				float u = Mathf.PosMod(ph + time * speed, 1f);
				p.Position = new Vector2(W * (1.2f - 1.8f * u), y);
			}
			foreach (var (p, o, ph) in motes)
			{
				p.Position = o + new Vector2(-30f * time, -40f * time + 6f * Mathf.Sin(time * 2f + ph));
				p.Modulate = new Color(1f, 1f, 1f, 0.4f + 0.4f * Mathf.Sin(time * 3f + ph));
			}
			n.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.15f, 0f, 1f) * (1f - Mathf.Clamp((time - 1.9f) / 0.15f, 0f, 1f)));
		});
		Fx.FreeAfter(n, total + 0.05f);
	}

	// 6.60 光穹 + 扇形光束射向建筑师；建筑师被封印环锁住、褪成石色
	private static void Dome(Node2D world, Vector2 feet, Vector2 foe, NCreature? architect, Vector2 view)
	{
		Node2D d = new();
		world.AddChild(d);
		Vector2 baseAt = new(view.X * 0.5f, view.Y * 0.86f);
		Sprite2D dome = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = baseAt, Modulate = new Color(0.8f, 0.95f, 1f, 0f) };
		d.AddChild(dome);
		Polygon2D domeShape = Fx.Poly(d, HalfDisc(view.X * 0.35f, 48), new Color(0.75f, 0.92f, 1f, 0f), additive: true);
		domeShape.Position = baseAt;
		var beams = new List<(Polygon2D p, float a, float len, float delay)>();
		for (int k = 0; k < 14; k++)
		{
			Vector2 toFoe = (foe - baseAt).Normalized();
			float a = toFoe.Angle() + (k - 6.5f) * 0.11f + Fx.Rand(-0.03f, 0.03f);
			float len = view.Length() * Fx.Rand(0.7f, 1.0f);
			float w = Fx.Rand(10f, 34f);
			Polygon2D p = Fx.Poly(d, new[] { new Vector2(0f, -w * 0.2f), new Vector2(len, -w), new Vector2(len, w), new Vector2(0f, w * 0.2f) }, new Color(0.85f, 0.97f, 1f, 0.5f), additive: true);
			p.Position = baseAt;
			p.Rotation = a;
			p.Scale = new Vector2(0f, 1f);
			beams.Add((p, a, len, Fx.Rand(0.3f, 0.9f)));
		}
		// 建筑师身上的封印环
		Node2D rings = new() { Position = foe };
		d.AddChild(rings);
		var rs = new List<Node2D>();
		for (int i = 0; i < 3; i++)
		{
			Node2D r = new() { Position = new Vector2(0f, (i - 1) * 70f), Modulate = new Color(1f, 1f, 1f, 0f) };
			rings.AddChild(r);
			Fx.StrokeClosed(r, Fx.Circle(150f, 64, 0.32f), new Color(0.75f, 0.95f, 1f, 0.9f), 3f);
			for (int k = 0; k < 16; k++)
			{
				float ang = k * Mathf.Tau / 16f;
				Fx.Poly(r, Fx.Star4(5f, 0.3f), Pal.White, additive: true).Position = new Vector2(Mathf.Cos(ang) * 150f, Mathf.Sin(ang) * 150f * 0.32f);
			}
			rs.Add(r);
		}
		Color? baseMod = architect != null && GodotObject.IsInstanceValid(architect) ? architect.Modulate : null;
		Audio.Sfx.Clip("Snd_FX_Btl_StAbsorption_00_1", 0.8f, 0.6f);
		const float total = 3.0f;
		Fx.Animate(d, total, t =>
		{
			float time = t * total;
			float rise = Ease.OutCubic(Mathf.Clamp(time / 0.8f, 0f, 1f));
			dome.Scale = new Vector2(view.X * 0.9f, view.Y * 0.8f) * rise / Fx.SoftDot.GetWidth();
			dome.Modulate = new Color(0.8f, 0.95f, 1f, 0.75f * rise);
			domeShape.Scale = new Vector2(rise, rise);
			domeShape.Modulate = new Color(1f, 1f, 1f, 0.35f + 0.2f * Mathf.Sin(time * 6f));
			foreach (var (p, a, len, delay) in beams)
			{
				float g = Ease.OutExpo(Mathf.Clamp((time - delay) / 0.35f, 0f, 1f));
				p.Scale = new Vector2(g, 1f + 0.3f * Mathf.Sin(time * 8f + a * 10f));
				p.Modulate = new Color(1f, 1f, 1f, g * (0.7f + 0.3f * Mathf.Sin(time * 10f + a * 7f)));
			}
			for (int i = 0; i < rs.Count; i++)
			{
				float c = Ease.OutCubic(Mathf.Clamp((time - 0.8f - i * 0.12f) / 0.5f, 0f, 1f));
				rs[i].Scale = Vector2.One * Mathf.Lerp(1.8f, 0.75f, c);
				rs[i].Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(c * 3f, 0f, 1f));
				rs[i].Rotation = (i - 1) * 0.08f;
			}
			if (baseMod is Color m && architect != null && GodotObject.IsInstanceValid(architect))
			{
				float g = Ease.InOutCubic(Mathf.Clamp((time - 1.1f) / 0.8f, 0f, 1f));
				architect.Modulate = m.Lerp(new Color(0.55f, 0.62f, 0.82f, m.A), g);
			}
		});
		Fx.FreeAfter(d, total + 0.1f);
	}

	private static Vector2[] HalfDisc(float r, int n)
	{
		Vector2[] pts = new Vector2[n + 1];
		for (int i = 0; i <= n; i++)
		{
			float a = Mathf.Pi + Mathf.Pi * i / n;
			pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.6f);
		}
		return pts;
	}

	// 8.70 → 9.60 光越来越强，全屏白（对准原声高潮）
	private static void WhiteRise(Node2D ui, Vector2 view)
	{
		ColorRect w = new() { Size = view, Color = new Color(1f, 1f, 1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		ui.AddChild(w);
		Fx.Animate(w, 1.0f, t => w.Color = new Color(1f, 1f, 1f, Ease.InCubic(Mathf.Clamp(t / 0.9f, 0f, 1f))));
		Fx.After(w, 0.95, "seal white shake", () => NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short));
		Fx.FreeAfter(w, 1.6);
	}

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
			streams.Add((line, head, path, Fx.Rand(0.1f, 0.5f)));
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

		// 9.6s 接 WhiteRise 的全屏白开始；与 Run 的清理时刻（Duration + 0.8）对齐
		const float total = Duration + 0.8f - 9.6f;
		white.Color = Pal.White;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;                  // 0 = 9.6s
			root.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - (total - 0.7f)) / 0.6f, 0f, 1f));
			art.Scale = Vector2.One * (1f + 0.07f * Ease.InOutCubic(Mathf.Clamp(time / 2.6f, 0f, 1f)));
			art.Position = view / 2f + (door - view / 2f) * 0.07f * Ease.InOutCubic(Mathf.Clamp(time / 2.6f, 0f, 1f)) * -1f;
			foreach ((Line2D line, Polygon2D head, Vector2[] path, float delay) in streams)
			{
				float u = Ease.InOutCubic(Mathf.Clamp((time - delay) / 1.0f, 0f, 1f));
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
			// 门心白光：1.8 起暴涨 → 2.6 全屏白（= 12.2s）
			float g = Ease.InCubic(Mathf.Clamp((time - 1.8f) / 0.7f, 0f, 1f));
			glow.Scale = Vector2.One * (Mathf.Lerp(2f, 40f, g) * 64f / Fx.SoftDot.GetWidth());
			glow.Modulate = new Color(1f, 1f, 1f, 0.3f + 0.7f * g);
			float ti = Ease.OutCubic(Mathf.Clamp((time - 0.5f) / 0.7f, 0f, 1f));
			title.Modulate = new Color(1f, 1f, 1f, ti * (1f - Mathf.Clamp((time - 2.2f) / 0.3f, 0f, 1f)));
			title.Scale = Vector2.One * (1.06f - 0.06f * ti);
			// 白：开场从全屏白褪出（0.1–0.6，画面从光里浮现）；2.4–2.6 再涨满，2.9 起转深蓝
			float w = Mathf.Max(1f - Ease.OutCubic(Mathf.Clamp((time - 0.1f) / 0.5f, 0f, 1f)), Mathf.Clamp((time - 2.4f) / 0.2f, 0f, 1f));
			float toNavy = Ease.InOutCubic(Mathf.Clamp((time - 2.9f) / 0.6f, 0f, 1f));
			white.Color = new Color(1f, 1f, 1f, w).Lerp(new Color(0.02f, 0.04f, 0.1f, w), toNavy);
			coda.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 3.2f) / 0.5f, 0f, 1f));
			if (time > 2.6f && time < 2.7f)
			{
				NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short);
			}
		});
	}
}
