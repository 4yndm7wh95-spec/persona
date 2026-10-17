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
	/// <summary>
	/// v0.14.46：这次建筑师房间用不用 P3 结局（设置里可关 = 全部原版）。进房间那一刻按设置定下，房间里中途改设置不影响这一次，
	/// 免得台词 / 按钮 / 结局演出 / 不反击各走各的、流程对不上。
	/// </summary>
	public static bool Active = true;

	public static string? Line(string table, string key)
	{
		if (!Active || table != "ancients" || !key.StartsWith(Prefix) || !Lines.TryGetValue(key[Prefix.Length..], out var l))
		{
			return null;
		}
		return Zh ? l.Zh : l.En;
	}

	// ───────────────────────── 房间 ─────────────────────────

	/// <summary>
	/// v0.14.20（用户：建筑师房间背景仍是原版）：只把塔顶背景挪到最上层不够 —— 原版背景（NCombatBackground）里可能有自带层级 /
	/// 独立画布层的部分，照样画在上面。直接把容器里除塔顶以外的东西都藏起来（含其中的 CanvasLayer），并在日志里列出藏了什么。
	/// </summary>
	private static void HideVanilla(Control bg, Node mine)
	{
		foreach (Node c in bg.GetChildren())
		{
			if (c == mine)
			{
				continue;
			}
			HideTree(c);
			GD.Print($"{ModEntry.Tag} architect bg: hid {c.Name} ({c.GetType().Name})");
		}
	}

	private static void HideTree(Node n)
	{
		if (n is CanvasItem ci)
		{
			ci.Visible = false;
		}
		else if (n is CanvasLayer cl)
		{
			cl.Visible = false;
		}
		foreach (Node c in n.GetChildren())
		{
			HideTree(c);                                                  // 子树里藏着的 CanvasLayer 不受父节点可见性影响，要逐个找
		}
	}

	/// <summary>
	/// v16 塔顶的房间细节（v0.14.27 重做：用户反馈 v0.14.26 的细节基本看不到 —— 太淡太小，跟人物有关的部分在背景创建时人物还没放进事件房间，一个都没生成）。
	/// 坐标按 v16 B 版原图（2048×1152）实测：魔法阵中心 (1028, 820)、外圈半轴 752×159，内圈 590×125；地平线 422；月光倒影柱 x≈1098。
	/// · 魔法阵：沿刻槽的两圈绿光（宽辉光 + 细亮线），24 个符文沿两圈之间按顺序亮起，带柔光；
	/// · 月光柱里的光尘；石碑旁的磷火（柔光 + 亮芯）；
	/// · 人物（每帧检查，新出现的立刻补上，位置每帧跟随）：月光染色、身后背光、脚下接触阴影、湿地面倒影（屏幕纹理镜像 + 水波）；
	/// · 前层：近地薄雾、灰烬；远处闪电时人物和魔法阵一起被照亮。
	/// 返回每帧更新（time, bolt）。
	/// </summary>
	private static System.Action<float, float> RoomDetails(Control root, NCombatRoom room, Texture2D tex, Vector2 origin, float s, Vector2 moonAt, float floorTop, float dotSize)
	{
		float k = tex.GetWidth() / 2048f * s;
		Vector2 P(float x, float y) => origin + new Vector2(x, y) * k;
		Vector2 view = room.GetViewportRect().Size;
		Control? front = room.GetNodeOrNull<Control>("%CombatVfxContainer");
		Color green = new(0.5f, 1f, 0.62f);

		// ── 魔法阵 ──
		Node2D circle = new() { Position = P(1026f, 820f) };
		root.AddChild(circle);
		Vector2[] Ellipse(float rx, float ry, int n = 128)
		{
			var e = new Vector2[n];
			for (int i = 0; i < n; i++)
			{
				float a = Mathf.Tau * i / n;
				e[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
			}
			return e;
		}
		var rings = new List<(Line2D glow, Line2D core)>();
		foreach ((float rx, float ry) in new[] { (752f, 159f), (590f, 125f) })
		{
			Line2D g = Fx.Line(circle, Ellipse(rx * k, ry * k), new Color(green, 0.22f), 14f, closed: true);
			Line2D c = Fx.Line(circle, Ellipse(rx * k, ry * k), new Color(0.75f, 1f, 0.8f, 0.85f), 2.5f, closed: true);
			rings.Add((g, c));
		}
		var runes = new List<(Node2D n, float a)>();
		for (int i = 0; i < 24; i++)
		{
			float a = Mathf.Tau * i / 24f;
			Node2D rn = new() { Position = new Vector2(Mathf.Cos(a) * 671f * k, Mathf.Sin(a) * 142f * k), Scale = new Vector2(1f, 0.5f) };   // 贴地压扁
			circle.AddChild(rn);
			rn.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 70f * k / dotSize, Modulate = new Color(green, 0.6f) });
			Fx.Poly(rn, Fx.Star4(15f * k, 0.22f), new Color(0.85f, 1f, 0.88f), additive: true);
			Fx.Line(rn, Fx.Circle(11f * k, 16), new Color(0.75f, 1f, 0.8f, 0.8f), 1.5f, closed: true);
			runes.Add((rn, a));
		}

		// ── 月光柱里的光尘 ──
		float colX = P(1098f, 0f).X;
		float dustTop = floorTop - 300f * k;
		var dust = new List<(Polygon2D p, float x, float y, float sp, float ph)>();
		for (int i = 0; i < 70; i++)
		{
			Polygon2D d = Fx.Poly(root, Fx.Star4(Fx.Rand(2.5f, 5f), 0.35f), new Color(0.9f, 1f, 0.9f), additive: true);
			dust.Add((d, colX + Fx.Rand(-1f, 1f) * 240f * k, Fx.Rand(dustTop, view.Y), Fx.Rand(8f, 22f), Fx.Rand(0f, Mathf.Tau)));
		}

		// ── 石碑旁的磷火 ──
		var wisps = new List<(Node2D n, Vector2 at, float ph)>();
		foreach (float x in new[] { 330f, 810f, 1320f, 1780f })
		{
			Vector2 at = P(x + 60f + Fx.Rand(-10f, 10f), 372f);           // 石碑右侧、半空（不压在碑面上）
			Node2D w = new() { Position = at };
			root.AddChild(w);
			w.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 60f * k / dotSize, Modulate = new Color(green, 0.7f) });
			w.AddChild(new Sprite2D { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * 16f * k / dotSize, Modulate = new Color(0.9f, 1f, 0.9f, 1f) });
			wisps.Add((w, at, Fx.Rand(0f, Mathf.Tau)));
		}

		// ── 人物：每帧检查，新出现的补上；位置每帧跟随 ──
		var decorated = new Dictionary<ulong, (NCreature c, Sprite2D back, Sprite2D shadow, Sprite2D core, Sprite2D glow, ColorRect? refl, float h)>();
		void Decorate()
		{
			foreach (NCreature c in room.CreatureNodes)
			{
				if (!GodotObject.IsInstanceValid(c) || decorated.ContainsKey(c.GetInstanceId()))
				{
					continue;
				}
				Vector2 feet = c.GetBottomOfHitbox();
				float h = Mathf.Max(200f, (feet.Y - c.VfxSpawnPosition.Y) * 2f);
				Sprite2D back = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = new Vector2(h * 0.9f, h * 1.4f) / dotSize, Modulate = new Color(Moon, 0.22f) };
				Sprite2D glow = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = new Vector2(h * 0.5f, h * 0.14f) / dotSize, Modulate = new Color(0.55f, 1f, 0.65f, 0.25f) };
				Sprite2D shadow = new() { Texture = Fx.SoftDot, Scale = new Vector2(h * 0.75f, h * 0.13f) / dotSize, Modulate = new Color(0f, 0.03f, 0.01f, 0.7f) };
				Sprite2D core = new() { Texture = Fx.SoftDot, Scale = new Vector2(h * 0.36f, h * 0.06f) / dotSize, Modulate = new Color(0f, 0.02f, 0f, 0.85f) };
				root.AddChild(back);
				root.AddChild(glow);
				root.AddChild(shadow);
				root.AddChild(core);
				ColorRect? refl = null;
				if (front != null)
				{
					refl = new() { Size = new Vector2(h * 0.9f, h * 0.8f), MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Reflection } };
					front.AddChild(refl);
					front.MoveChild(refl, 0);                                       // 前层最底下：不盖住其他前层特效
				}
				decorated[c.GetInstanceId()] = (c, back, shadow, core, glow, refl, h);
				GD.Print($"{ModEntry.Tag} architect room: grounded {c.Name} (h={h:0})");
			}
		}

		// ── 前层：近地薄雾 + 灰烬 ──
		var mist = new List<(Sprite2D sp, float y, float speed, float ph)>();
		var ash = new List<(Polygon2D p, float x, float y, float sp, float ph)>();
		if (front != null)
		{
			float feetY = P(0f, 740f).Y;
			for (int i = 0; i < 5; i++)
			{
				Sprite2D m = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(0.55f, 0.9f, 0.65f, 0.1f), Scale = new Vector2(Fx.Rand(9f, 13f), 1.0f) * (view.X / 1920f) * (64f / dotSize) };
				front.AddChild(m);
				mist.Add((m, feetY + Fx.Rand(-24f, 30f), Fx.Rand(20f, 38f) * (i % 2 == 0 ? 1f : -1f), Fx.Rand(0f, 1f)));
			}
			for (int i = 0; i < 14; i++)
			{
				float sz = Fx.Rand(3f, 6f);
				Polygon2D a = Fx.Poly(front, new[] { new Vector2(-sz, 0f), new Vector2(0f, -sz * 0.6f), new Vector2(sz * 1.2f, sz * 0.3f), new Vector2(0f, sz * 0.6f) }, new Color(0.3f, 0.45f, 0.38f, 0.75f));
				ash.Add((a, Fx.Rand(0f, view.X), Fx.Rand(0f, view.Y), Fx.Rand(12f, 30f), Fx.Rand(0f, Mathf.Tau)));
			}
		}

		float nextScan = 0f;
		return (time, bolt) =>
		{
			if (time >= nextScan)
			{
				nextScan = time + 0.5f;
				Decorate();
			}
			// 魔法阵：整体呼吸 + 符文按顺序一圈圈亮起
			float br = 0.5f + 0.5f * Mathf.Sin(time * 0.7f);
			for (int i = 0; i < rings.Count; i++)
			{
				float b = i == 0 ? br : 1f - br;
				rings[i].glow.Modulate = new Color(1f, 1f, 1f, 0.55f + 0.45f * b + bolt);
				rings[i].core.Modulate = new Color(1f, 1f, 1f, 0.5f + 0.5f * b + bolt);
			}
			float head = Mathf.PosMod(time * 0.9f, Mathf.Tau);
			foreach ((Node2D rn, float a) in runes)
			{
				float d = Mathf.PosMod(head - a, Mathf.Tau);
				float lit = Mathf.Exp(-d * 1.4f);
				rn.Modulate = new Color(1f, 1f, 1f, 0.25f + 0.75f * lit + bolt * 0.4f);
				rn.Scale = new Vector2(1f + 0.25f * lit, 0.5f * (1f + 0.25f * lit));
			}
			// 光尘：只在月光柱附近显形
			float span = view.Y - dustTop;
			foreach ((Polygon2D d, float x, float y, float sp, float ph) in dust)
			{
				float yy = dustTop + Mathf.PosMod(y - dustTop - sp * time, span);
				float xx = x + 22f * Mathf.Sin(time * 0.4f + ph);
				d.Position = new Vector2(xx, yy);
				d.Rotation = time * 0.6f + ph;
				float inCol = Mathf.Exp(-Mathf.Pow((xx - colX) / (170f * k), 2f));
				d.Modulate = new Color(1f, 1f, 1f, inCol * (0.55f + 0.45f * Mathf.Sin(time * 2.3f + ph * 3f)));
			}
			foreach ((Node2D w, Vector2 at, float ph) in wisps)
			{
				w.Position = at + new Vector2(8f * Mathf.Sin(time * 0.7f + ph), -14f * Mathf.Sin(time * 0.5f + ph * 2f));
				w.Modulate = new Color(1f, 1f, 1f, 0.65f + 0.35f * Mathf.Sin(time * 3.1f + ph) * Mathf.Sin(time * 1.7f + ph * 2f));
			}
			// 人物：位置跟随、染色、闪电照亮、背光呼吸、倒影水波
			foreach (var (c, back, shadow, core, glow, refl, h) in decorated.Values)
			{
				if (!GodotObject.IsInstanceValid(c))
				{
					continue;
				}
				Vector2 feet = c.GetBottomOfHitbox();
				Vector2 toMoon = (moonAt - feet).Normalized();
				back.Position = feet + new Vector2(toMoon.X * 30f, -h * 0.5f);
				back.Modulate = new Color(Moon, 0.18f + 0.06f * br + 0.3f * bolt);
				glow.Position = feet + new Vector2(0f, 6f);
				shadow.Position = feet + new Vector2(0f, 4f);
				core.Position = feet + new Vector2(0f, 2f);
				c.Visuals.Modulate = new Color(0.78f + 0.3f * bolt, 0.92f + 0.08f * bolt, 0.82f + 0.25f * bolt);
				if (refl != null && GodotObject.IsInstanceValid(refl) && refl.Material is ShaderMaterial m)
				{
					refl.GlobalPosition = feet + new Vector2(-h * 0.45f, -2f);
					m.SetShaderParameter("time_s", time);
				}
			}
			foreach ((Sprite2D m, float y, float speed, float ph) in mist)
			{
				float sp2 = view.X + 900f;
				m.Position = new Vector2(Mathf.PosMod(ph * sp2 + speed * time, sp2) - 450f, y + 6f * Mathf.Sin(time * 0.6f + ph * 4f));
			}
			foreach ((Polygon2D a, float x, float y, float sp, float ph) in ash)
			{
				a.Position = new Vector2(Mathf.PosMod(x + sp * 1.4f * time + 30f * Mathf.Sin(time * 0.5f + ph), view.X + 40f) - 20f, Mathf.PosMod(y + sp * time, view.Y + 40f) - 20f);
				a.Rotation = time * (0.6f + ph * 0.2f);
			}
		};
	}

	private static Shader? _reflection;

	/// <summary>
	/// 湿地面倒影：取屏幕上脚以上的画面镜像到脚下。矩形顶边 = 脚底线；用导数推出顶边在屏幕 UV 里的位置，和画布变换无关。
	/// 越往下越淡、左右两边羽化，带两层水波扰动，染一点月光绿。
	/// </summary>
	private static Shader Reflection => _reflection ??= new Shader
	{
		Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float time_s = 0.0;
uniform float strength = 0.45;
void fragment() {
	float dsy = dFdy(SCREEN_UV.y);
	float duy = dFdy(UV.y);
	float per = abs(duy) > 1e-6 ? dsy / duy : 0.0;                 // 矩形高度对应的屏幕 UV
	float top = SCREEN_UV.y - UV.y * per;
	float my = top - (SCREEN_UV.y - top);
	float wob = sin(UV.y * 60.0 + time_s * 2.2) * 0.0016 + sin(UV.y * 23.0 - time_s * 1.3) * 0.0012;
	vec4 c = textureLod(screen_tex, vec2(SCREEN_UV.x + wob * (0.4 + UV.y), my), 0.0);
	float fade = pow(1.0 - UV.y, 1.6) * sin(3.14159 * UV.x);
	COLOR = vec4(c.rgb * vec3(0.7, 0.95, 0.78), strength * fade);
}",
	};

	/// <summary>
	/// v0.14.25（用户：人物和背景不在一个图层，光影没有落到人物身上，也不像真的站在地板上）：
	/// 人物（结城理和建筑师）整体染上影时间的月光绿、压暗一点；身后一团月光背光（随月光呼吸）；
	/// 脚下接触阴影（深色压扁椭圆 + 更小更深的核心）；湿石板上一道竖向的绿色倒影光。都画在背景层（人物身后）。
	/// 返回背光精灵和基础透明度，供呼吸动画用。
	/// </summary>
	private static List<(Sprite2D, float)> Grounding(Control root, NCombatRoom room, Vector2 moonAt, float dotSize)
	{
		var lit = new List<(Sprite2D, float)>();
		foreach (NCreature c in room.CreatureNodes)
		{
			if (!GodotObject.IsInstanceValid(c))
			{
				continue;
			}
			c.Visuals.Modulate = new Color(0.8f, 0.93f, 0.84f);
			Vector2 feet = c.GetBottomOfHitbox();
			float h = Mathf.Max(200f, (feet.Y - c.VfxSpawnPosition.Y) * 2f);
			// 月光背光：偏向月亮一侧的竖长柔光
			Vector2 toMoon = (moonAt - feet).Normalized();
			Sprite2D back = new()
			{
				Texture = Fx.SoftDot, Material = Fx.Additive, Position = feet + new Vector2(toMoon.X * 30f, -h * 0.5f),
				Scale = new Vector2(h * 0.75f, h * 1.25f) / dotSize, Modulate = new Color(Moon, 0.16f)
			};
			root.AddChild(back);
			lit.Add((back, 0.16f));
			// 湿地面倒影：脚下往下拖一道淡绿竖光
			Sprite2D refl = new()
			{
				Texture = Fx.SoftDot, Material = Fx.Additive, Position = feet + new Vector2(0f, h * 0.18f),
				Scale = new Vector2(h * 0.32f, h * 0.5f) / dotSize, Modulate = new Color(0.55f, 0.95f, 0.65f, 0.1f)
			};
			root.AddChild(refl);
			// 接触阴影：外圈淡、核心深
			root.AddChild(new Sprite2D
			{
				Texture = Fx.SoftDot, Position = feet + new Vector2(0f, 4f),
				Scale = new Vector2(h * 0.62f, h * 0.11f) / dotSize, Modulate = new Color(0f, 0.03f, 0.01f, 0.6f)
			});
			root.AddChild(new Sprite2D
			{
				Texture = Fx.SoftDot, Position = feet + new Vector2(0f, 2f),
				Scale = new Vector2(h * 0.3f, h * 0.05f) / dotSize, Modulate = new Color(0f, 0.02f, 0f, 0.75f)
			});
		}
		return lit;
	}

	public static void Room(NCombatRoom room)
	{
		GD.Print($"{ModEntry.Tag} architect room: {room.GetPath()}");
		if (room.GetNodeOrNull<Control>("%BgContainer") is not Control bg)
		{
			return;
		}
		// v16 新背景（v0.14.26，透视修正：地平线 422/1152 ≈ 人眼高度，站人线 740/1152）优先；
		// 其次 v14（站人线 740/1152，但地平线 620 太低，近大远小对不上）；都没有用 v13 的图（平台前沿 y=745/1024）
		Texture2D? v3 = Tex("tartarus_top_v3");
		Texture2D? v2 = v3 ?? Tex("tartarus_top_v2");
		if (bg.GetNodeOrNull<Control>("P3TartarusTop") is Control broken && broken.FindChild("P3TartarusArt", true, false) == null)
		{
			// v0.14.45：上次搭到一半出错（没有主图）：拆掉重搭，不能留一个空的背景还把原版藏着
			broken.Name = "P3TartarusTopBroken";
			broken.QueueFree();
		}
		if (bg.GetNodeOrNull<Control>("P3TartarusTop") is Control existing)
		{
			// v0.14.16（用户：正式流程里建筑师房间还是原版背景）：原版背景可能比我们晚加进同一个容器、盖在上面 —— 每次都挪回最上层
			bg.MoveChild(existing, bg.GetChildCount() - 1);
			HideVanilla(bg, existing);
			return;
		}
		if ((v2 ?? Tex("tartarus_top")) is not Texture2D tex)
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
		// v0.14.24（用户：建筑师房间一片虚空）：塔顶主图在 v0.14.9 重写对齐时被误删，之前靠原版背景透出来没发现；v0.14.20 藏掉原版背景后只剩月光 / 雾等叠加层
		art.AddChild(new Sprite2D { Name = "P3TartarusArt", Texture = tex, Centered = false, Position = origin, Scale = Vector2.One * s });
		// v0.14.45：主图放好之后才藏原版背景（以前先藏再搭，中途出错就是一片空）
		HideVanilla(bg, root);
		bg.ChildEnteredTree += n =>
		{
			if (n != root && GodotObject.IsInstanceValid(root))
			{
				Callable.From(() => HideVanilla(bg, root)).CallDeferred();
				Callable.From(() => { if (GodotObject.IsInstanceValid(root) && root.GetParent() == bg) bg.MoveChild(root, bg.GetChildCount() - 1); }).CallDeferred();
			}
		};
		// 前景碎石（新图才有）：叠在人物前面（前层特效容器），增加纵深
		if (v2 != null && Tex(v3 != null ? "tartarus_top_v3_fg" : "tartarus_top_v2_fg") is Texture2D fg && room.GetNodeOrNull<Control>("%CombatVfxContainer") is Control fgLayer)
		{
			Sprite2D fgs = new() { Name = "P3TartarusFg", Texture = fg, Centered = false, Scale = Vector2.One * s };
			fgLayer.AddChild(fgs);
			fgs.GlobalPosition = origin;
		}
		// 原图里月亮的中心 / 半径（v14 A 版 1672×941 实测：(992, 208)、r≈165）
		// v16 B 版（2048×1152）实测月亮 (1098, 200)、r≈149
		Vector2 moonAt = origin + (v3 != null ? new Vector2(1098f, 200f) * (tex.GetWidth() / 2048f) : v2 != null ? new Vector2(992f, 208f) * (tex.GetWidth() / 1672f) : new Vector2(899f, 70f)) * s;
		float moonR = (v3 != null ? 149f * tex.GetWidth() / 2048f : v2 != null ? 165f * tex.GetWidth() / 1672f : 230f) * s;

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
		float floorTop = origin.Y + (v3 != null ? 422f / 1152f * tex.GetHeight() : v2 != null ? 620f / 1152f * tex.GetHeight() : 760f) * s;
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
		// v0.14.28（用户：闪电光效有明显的分界线）：原来是高 55% 屏的纯色矩形，下边缘一道硬边。
		// 改成整屏的竖向渐变：天顶最亮，往下平滑衰减到地平线附近，地面只留很淡的一层（湿石板反光）
		var skyGrad = new Gradient
		{
			Offsets = new[] { 0f, 0.3f, 0.5f, 0.62f, 1f },
			Colors = new[] { new Color(1f, 1f, 1f, 1f), new Color(1f, 1f, 1f, 0.7f), new Color(1f, 1f, 1f, 0.3f), new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0.06f) }
		};
		TextureRect sky = new()
		{
			Texture = new GradientTexture2D { Gradient = skyGrad, Width = 4, Height = 256, FillFrom = new Vector2(0f, 0f), FillTo = new Vector2(0f, 1f) },
			Size = new Vector2(W, H), Position = new Vector2(-margin, -margin), StretchMode = TextureRect.StretchModeEnum.Scale,
			Modulate = new Color(0.7f, 1f, 0.75f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore, Material = Fx.Additive
		};
		root.AddChild(sky);
		float nextBolt = Fx.Rand(3f, 6f);
		float boltAt = -10f;

		// 影时间暗绿调 + 四周压暗
		ColorRect tint = new() { Size = new Vector2(W, H), Position = new Vector2(-margin, -margin), Color = new Color(0.02f, 0.12f, 0.06f, 0.18f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(tint);
		var lit = v3 != null ? new List<(Sprite2D, float)>() : Grounding(root, room, moonAt, dotSize);   // v16 图上由 RoomDetails 每帧处理人物
		// v0.14.26 房间细节（用户：光影再细腻一些，看上去是这个房间的内容而不只是背景）：只在 v16 新图上（位置按新图量的）
		System.Action<float, float>? details = v3 != null ? RoomDetails(root, room, tex, origin, s, moonAt, floorTop, dotSize) : null;
		ulong start = Time.GetTicksMsec();
		const float loop = 60f;
		Fx.Animate(root, loop, _ =>
		{
			float time = (Time.GetTicksMsec() - start) / 1000f;
			float breath = 0.5f + 0.5f * Mathf.Sin(time * 0.9f);
			foreach ((Sprite2D back, float a0) in lit)
			{
				back.Modulate = new Color(back.Modulate, a0 * (0.85f + 0.15f * breath));
			}
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
			sky.Modulate = new Color(0.7f, 1f, 0.75f, 0.16f * bolt);
			details?.Invoke(time, bolt);
		}).SetLoops();
	}

	// ───────────────────────── 最后的按钮 ─────────────────────────

	public static void DecorateButton(NEventOptionButton button)
	{
		if (button.GetNodeOrNull<RichTextLabel>("%Text") is RichTextLabel label)
		{
			// v0.14.20（用户：技能菜单里才是「伟大封印」，这个按钮不再叫这个名字，只留下面那段字）
			label.Text = Zh ? "[color=#cfe8ff]以生命为封印，终结这一切[/color]" : "[color=#cfe8ff]Become the seal. End it all.[/color]";
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
	// v0.14.20（用户实机反馈：技能菜单要真的能点；去掉面部特写、伟大封印巨门图、射向建筑师的扇形光束和锁链环；
	// 光柱要从指尖升起；脚下的光雾太廉价）—— 照原版视频重排：
	//   菜单  P3R 技能菜单停在画面上，等玩家点「伟大封印」（悬停有光标音、条目发亮；点下确认音 + 一闪）
	//   0.00  点下：菜单 0.35s 滑出；Nyx 战「封印」原声从这一刻开始（9.6s 处是原声高潮）
	//   0.40  左上技能名横幅；0.9–1.3 转黑，1.3 起从黑里淡回压暗的塔顶，上下遮幅
	//   1.30  脚下亮起蓝白光池，水波一圈圈荡开；蓝色光焰从脚下往上流、光点上飘（原版那种身上的蓝色光雾）
	//   2.40  单手指天（8 帧，约 1.9s）；4.30 指尖到顶 —— 光柱从食指尖直冲天顶
	//   4.30  地面光盘向四周铺开，光柱越来越粗，天顶亮起，光线往上流 → 8.70 光越来越强 → 9.60 全屏白（原声高潮）
	//   9.90  白转深蓝，「MEMENTO MORI / ……于是，早晨来临。」；13.0 交还给游戏的胜利流程，覆盖层 0.8s 淡出后清理
	public const float Duration = 13.0f;
	private const float WhiteAt = 9.6f;

	private static Texture2D? UiTex(string name)
	{
		string path = $"res://p3makoto/ui/{name}.png";
		return ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
	}

	/// <summary>播放结局演出：先等玩家在技能菜单里点「伟大封印」，点下后 Duration 秒完成（返回 true = 已代替原版的攻击演出）。</summary>
	public static Task<bool> Play(NCreature? player, NCreature? architect)
	{
		TaskCompletionSource<bool> done = new();
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			done.SetResult(false);
			return done.Task;
		}
		bool started = false;
		ModEntry.Safe("great seal", () =>
		{
			// v0.14.29：不再按固定时长交还 —— MEMENTO MORI 之后进入「要闭上眼睛吗？」，玩家点了、眼睛完全闭上（全黑）才交还给游戏
			CanvasLayer top = Run(tree, player, architect, () => { }, () =>
			{
				// 交还流程前保证游戏是 1 倍速，并记一行日志方便排查（可能被看门狗 / 出错兜底重复调用，只生效一次）
				if (done.Task.IsCompleted)
				{
					return;
				}
				P3Makoto.Core.TimeScale.Reset();
				GD.Print($"{ModEntry.Tag} great seal done -> back to the game (TimeScale={Engine.TimeScale})");
				done.TrySetResult(true);
			});
			// v0.14.45：演出中途保存退出 / 放弃本局时，整层（含全屏挡点击的透明板）跟着本局一起清掉，不能盖在主菜单上
			if (MegaCrit.Sts2.Core.Nodes.NRun.Instance is Node run)
			{
				run.TreeExiting += () =>
				{
					if (GodotObject.IsInstanceValid(top))
					{
						top.QueueFree();
					}
					P3Makoto.Core.TimeScale.Reset();
				};
			}
			started = true;
		});
		if (!started)
		{
			done.TrySetResult(false);
		}
		return done.Task;
	}

	private static CanvasLayer Run(SceneTree tree, NCreature? player, NCreature? architect, System.Action onStart, System.Action finish)
	{
		Vector2 view = tree.Root.GetVisibleRect().Size;
		CanvasLayer top = new() { Layer = 90, Name = "P3GreatSeal" };
		tree.Root.AddChild(top);
		Node2D world = new();       // 世界坐标上的效果（脚下光雾、光柱……），在遮幅和压暗之上
		Node2D ui = new();          // 菜单 / 白幕 / 结语
		bool valid = player != null && GodotObject.IsInstanceValid(player);
		Vector2 feet = valid ? Makoto.Feet(player!) : view * new Vector2(0.3f, 0.7f);
		Vector2 head = valid ? Makoto.Head(player!) : view * new Vector2(0.3f, 0.35f);
		float body = Mathf.Max((feet.Y - head.Y) * 1.15f, 160f);           // 人物大致身高（屏幕像素）

		ColorRect dim = new() { Size = view, Color = new Color(0.01f, 0.03f, 0.06f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		top.AddChild(dim);
		top.AddChild(world);
		float barH = view.Y * 0.12f;
		ColorRect barTop = new() { Size = new Vector2(view.X, barH), Position = new Vector2(0f, -barH), Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
		ColorRect barBot = new() { Size = new Vector2(view.X, barH), Position = new Vector2(0f, view.Y), Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
		top.AddChild(barTop);
		top.AddChild(barBot);
		top.AddChild(ui);

		SkillMenu(ui, view, () =>
		{
			// v0.14.45 看门狗：点下「伟大封印」之后，无论中间哪一步出错、玩家多久不点「闭上眼睛」，最晚这时一定交还游戏（结局不会卡死）
			tree.CreateTimer(WhiteAt + 90.0, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () =>
			{
				if (GodotObject.IsInstanceValid(top))
				{
					GD.Print($"{ModEntry.Tag} great seal watchdog -> finishing");
					finish();
					top.QueueFree();
				}
			};
			onStart();
			Audio.Sfx.Clip("great_seal", 1.0f);
			Fx.Animate(dim, WhiteAt, t =>
			{
				float time = t * WhiteAt;
				// 0.9–1.3 转黑；1.3–1.9 从黑里淡回压暗 55% 的塔顶
				float a = time < 0.9f ? 0f : time < 1.3f ? (time - 0.9f) / 0.4f : Mathf.Lerp(1f, 0.55f, Mathf.Clamp((time - 1.3f) / 0.6f, 0f, 1f));
				dim.Color = new Color(0.01f, 0.03f, 0.06f, a);
				float bars = Ease.OutCubic(Mathf.Clamp((time - 1.2f) / 0.4f, 0f, 1f));
				barTop.Position = new Vector2(0f, -barH + barH * bars);
				barBot.Position = new Vector2(0f, view.Y - barH * bars);
			});
			Fx.After(ui, 0.4, "seal banner", () => { if (Makoto.FxLayer() is Control front) Uniq.SkillBanner(front, "伟大封印", "THE GREAT SEAL", 1.0f); });
			Fx.After(world, 1.3, "seal aura", () => Aura(world, feet, body, WhiteAt + 0.3f - 1.3f));
			// 8 帧指天：2.4 起手，约 1.9s 后指尖到顶；只有单帧时 3.7 起手（0.3s 处溶解到指天）
			bool frames = HasAnim(player, "point_sky_in");
			Fx.After(world, frames ? 2.4 : 3.7, "seal point", () => PointSky(player));
			// 光柱从食指尖升起：骨骼里 "hand" 在指天最后一帧就放在指尖。
			// v0.14.27（用户：光束有时从身体部位射出）：4.3s 正好是指天动画切到最后一帧的时刻，稍有卡顿读到的就是抬手途中的手。
			// 现在要等动画进入 point_sky_loop 且手在头顶上方才取点；没到就每 0.05s 再看一次，最多 0.5s，仍不行按身高估
			void Pillar(float at)
			{
				Vector2 tip = head + new Vector2(-0.12f, -0.35f) * body;
				bool ready = false;
				if (valid && GodotObject.IsInstanceValid(player) && WeaponFx.Bone(player!, "hand") is Vector2 b && b.DistanceTo(head) < body * 1.2f)
				{
					string? cur = P3Makoto.Core.Compat.CurrentAnimation(player!.Visuals.SpineBody);
					bool posed = !frames || cur == "point_sky_loop";
					if (posed && b.Y < head.Y - body * 0.05f)
					{
						tip = b;
						ready = true;
					}
				}
				if (!ready && at < 4.8f)
				{
					Fx.After(world, 0.05, "seal pillar wait", () => Pillar(at + 0.05f));
					return;
				}
				Ascend(world, tip, feet, body, view, WhiteAt + 0.3f - at);
			}
			Fx.After(world, 4.3, "seal pillar", () => Pillar(4.3f));
			Fx.After(ui, WhiteAt - 0.9, "seal white", () => WhiteRise(ui, view));
			// v0.14.30：白幕之后先问「要闭上眼睛吗？」，闭眼全黑后才是 MEMENTO MORI（用户：闭眼应排在 MEMENTO MORI 前面）
			Fx.After(ui, WhiteAt, "seal eyes", () =>
			{
				try
				{
					CloseEyes(top, view, finish);
				}
				catch (System.Exception)
				{
					finish();                           // 闭眼这段搭不起来：直接交还游戏（结算画面出场后整层由 RevealGameOver 清掉）
					throw;
				}
			});
		});
		return top;
	}

	// 菜单：P3R 技能菜单，停住等玩家点「伟大封印」
	private static void SkillMenu(Node2D ui, Vector2 view, System.Action onConfirm)
	{
		Node2D m = new();
		ui.AddChild(m);
		float W = view.X, H = view.Y;
		// 挡住下面的游戏按钮（透明，接住点击）
		ColorRect blocker = new() { Size = view, Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Stop };
		m.AddChild(blocker);
		// 斜切深蓝带 + 超大 PERSONA（被裁切）
		Node2D band = new() { Position = new Vector2(W * 0.2f, H * 0.62f), Rotation = -0.32f };
		m.AddChild(band);
		Fx.Poly(band, new[] { new Vector2(-W, -H * 0.16f), new Vector2(W, -H * 0.16f), new Vector2(W, H * 0.16f), new Vector2(-W, H * 0.16f) }, new Color(0.03f, 0.14f, 0.62f, 0.95f));
		Fx.Poly(band, new[] { new Vector2(-W, H * 0.16f), new Vector2(W, H * 0.16f), new Vector2(W, H * 0.175f), new Vector2(-W, H * 0.175f) }, Pal.Cyan, additive: true);
		Node2D word = new();
		band.AddChild(word);
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
		Texture2D? sil = UiTex("bu_silhouette");
		Sprite2D? fig = null;
		if (sil != null)
		{
			fig = new Sprite2D { Texture = sil, Scale = Vector2.One * (H * 1.1f / sil.GetHeight()), Position = new Vector2(W * 0.2f, H * 0.6f) };
			m.AddChild(fig);
		}
		// 右上：阿卡纳栏 + 技能列表 + 说明
		Vector2 panelAt = new(W * 0.5f, H * 0.12f);
		Node2D panel = new() { Position = panelAt };
		m.AddChild(panel);
		float pw = W * 0.44f;
		Fx.Poly(panel, new[] { new Vector2(0f, 0f), new Vector2(pw, 0f), new Vector2(pw - 16f, 44f), new Vector2(-16f, 44f) }, new Color(0.95f, 0.97f, 1f, 0.95f));
		Typo.Text(panel, Zh ? "宇宙" : "UNIVERSE", Typo.CjkHeavy, 26, new Color(0.05f, 0.1f, 0.3f), new Vector2(24f, 6f));
		Typo.Text(panel, "ARCANA", Typo.LatinItalic, 16, new Color(0.2f, 0.4f, 0.8f), new Vector2(pw - 110f, 14f));
		Vector2 rowAt = new(-30f, 70f);
		Node2D row = new() { Position = rowAt };
		panel.AddChild(row);
		Node2D hl = new();
		row.AddChild(hl);
		if (UiTex("skill_base") is Texture2D sb)
		{
			hl.AddChild(new Sprite2D { Texture = sb, Centered = false, Scale = new Vector2(pw / sb.GetWidth(), 52f / sb.GetHeight()), Position = new Vector2(-14f, 0f) });
		}
		else
		{
			Fx.Poly(hl, new[] { new Vector2(0f, 0f), new Vector2(pw, 0f), new Vector2(pw - 14f, 52f), new Vector2(-14f, 52f) }, Pal.White);
		}
		Polygon2D shine = Fx.Poly(row, new[] { new Vector2(0f, 0f), new Vector2(40f, 0f), new Vector2(26f, 52f), new Vector2(-14f, 52f) }, new Color(1f, 1f, 1f, 0f), additive: true);
		Fx.Poly(row, new[] { new Vector2(12f, 14f), new Vector2(46f, 14f), new Vector2(40f, 38f), new Vector2(6f, 38f) }, new Color(0.05f, 0.1f, 0.3f));
		Typo.Text(row, Zh ? "伟大封印" : "The Great Seal", Typo.CjkHeavy, 30, new Color(0.03f, 0.05f, 0.12f), new Vector2(60f, 6f));
		Typo.Text(row, "∞", Typo.LatinItalic, 30, new Color(0.03f, 0.05f, 0.12f), new Vector2(pw - 70f, 4f));
		// 选中箭头（P3R 菜单左侧的小三角），悬停时左右轻摆
		Polygon2D cursor = Fx.Poly(row, new[] { new Vector2(-40f, 12f), new Vector2(-18f, 26f), new Vector2(-40f, 40f) }, Pal.Cyan);
		Node2D desc = new() { Position = new Vector2(-10f, 140f) };
		panel.AddChild(desc);
		Typo.Text(desc, Zh ? "解放内在的宇宙后获得的力量。" : "Power drawn from the universe within.", Typo.Cjk, 22, new Color(0.6f, 0.9f, 1f), Vector2.Zero, outline: 2, outlineColor: new Color(0.02f, 0.05f, 0.15f));
		Typo.Text(desc, Zh ? "以自身为封印，封住一切的终结。" : "Become the seal that holds back the end.", Typo.Cjk, 22, new Color(0.6f, 0.9f, 1f), new Vector2(0f, 32f), outline: 2, outlineColor: new Color(0.02f, 0.05f, 0.15f));
		// 点击区域：技能条 + 说明（屏幕坐标）
		Control hit = new() { Position = panelAt + rowAt + new Vector2(-50f, -6f), Size = new Vector2(pw + 60f, 64f + 110f), MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		m.AddChild(hit);
		bool hover = false, confirmed = false;
		float confirmAt = 0f, hoverK = 0f;
		ulong t0 = Time.GetTicksMsec();
		hit.MouseEntered += () =>
		{
			hover = true;
			if (!confirmed) Audio.Sfx.Clip("Sys_Cursor", 0.7f);
		};
		hit.MouseExited += () => hover = false;
		void Confirm()
		{
			if (confirmed)
			{
				return;
			}
			confirmed = true;
			confirmAt = (Time.GetTicksMsec() - t0) / 1000f;
			Audio.Sfx.Clip("Btl_Ok", 0.9f);
			blocker.MouseFilter = hit.MouseFilter = Control.MouseFilterEnum.Ignore;
			Fx.FreeAfter(m, 0.45f);
			onConfirm();
		}
		hit.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			{
				Confirm();
			}
		};
		// v0.14.45：手柄 / 键盘（确认键）也能选（以前只认鼠标左键，手柄玩家在这里会卡住）
		Fx.Animate(m, 3600f, _ =>
		{
			if (!confirmed && Time.GetTicksMsec() - t0 > 400 && Input.IsActionJustPressed("ui_accept"))
			{
				Confirm();
			}
		});
		Audio.Sfx.Clip("Btl_CommandPanel", 0.8f, 0.05f);
		Fx.Animate(m, 3600f, _ =>
		{
			float time = (Time.GetTicksMsec() - t0) / 1000f;
			float inn = Ease.OutExpo(Mathf.Clamp(time / 0.3f, 0f, 1f));
			float since = confirmed ? time - confirmAt : 0f;
			float flash = confirmed ? Mathf.Max(0f, 1f - since / 0.25f) : 0f;
			float outT = confirmed ? Ease.InCubic(Mathf.Clamp((since - 0.1f) / 0.3f, 0f, 1f)) : 0f;
			hoverK = Mathf.MoveToward(hoverK, hover || confirmed ? 1f : 0f, 0.12f);
			if (fig != null)
			{
				fig.Position = new Vector2(W * 0.2f - W * 0.3f * (1f - inn) - W * 0.3f * outT, H * 0.6f);
			}
			band.Position = new Vector2(W * 0.2f + W * 0.5f * (1f - inn), H * 0.62f);
			word.Position = new Vector2(-40f * Mathf.Min(time, 3f) - 8f * Mathf.Max(0f, time - 3f), 0f);
			panel.Position = panelAt + new Vector2(W * 0.5f * (1f - Ease.OutExpo(Mathf.Clamp((time - 0.15f) / 0.3f, 0f, 1f))) + W * 0.5f * outT, 0f);
			float sel = Mathf.Clamp((time - 0.45f) / 0.12f, 0f, 1f);
			hl.Modulate = new Color(1f, 1f, 1f, sel);
			desc.Modulate = new Color(1f, 1f, 1f, sel);
			// 待选时：条目轻轻呼吸，一道光周期性扫过；悬停时更亮、箭头摆动
			float idle = 0.5f + 0.5f * Mathf.Sin(time * 3.2f);
			row.Scale = Vector2.One * (1f + 0.025f * hoverK + 0.04f * flash);
			cursor.Position = new Vector2(-6f * Mathf.Abs(Mathf.Sin(time * 5f)) * (0.4f + 0.6f * hoverK), 0f);
			cursor.Modulate = new Color(1f, 1f, 1f, sel * (0.6f + 0.4f * Mathf.Max(idle, hoverK)));
			float sw = Mathf.PosMod(time / 1.8f, 1f);
			shine.Position = new Vector2(Mathf.Lerp(-30f, pw, Ease.InOutCubic(Mathf.Clamp(sw / 0.5f, 0f, 1f))), 0f);
			shine.Modulate = new Color(1f, 1f, 1f, sw < 0.5f ? sel * (0.35f + 0.4f * hoverK) : 0f);
			m.Modulate = new Color(1f + flash * 0.6f, 1f + flash * 0.6f, 1f + flash * 0.6f, 1f - outT);
		});
	}

	// 1.30 脚下光池 + 蓝色光焰往上流（原版：指天时身上缭绕的蓝色光雾、脚下的光）
	private static void Aura(Node2D world, Vector2 feet, float body, float life)
	{
		Node2D r = new() { Position = feet };
		world.AddChild(r);
		float dot = Fx.SoftDot.GetWidth();
		Sprite2D Glow(Vector2 sizePx, Color c)
		{
			Sprite2D s = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = sizePx / dot, Modulate = c };
			r.AddChild(s);
			return s;
		}
		// 光池（两层）+ 人身后一层很淡的竖向光晕
		Sprite2D poolWide = Glow(new Vector2(body * 2.2f, body * 0.42f), new Color(0.3f, 0.6f, 1f, 0f));
		Sprite2D pool = Glow(new Vector2(body * 1.1f, body * 0.22f), new Color(0.75f, 0.95f, 1f, 0f));
		Sprite2D sheath = Glow(new Vector2(body * 0.9f, body * 1.5f), new Color(0.35f, 0.7f, 1f, 0f));
		sheath.Position = new Vector2(0f, -body * 0.5f);
		// 水波：一圈圈柔边光环从脚下荡开（压扁的圆，带宽光晕 + 细亮边）
		var ripples = new List<(Polygon2D glow, Polygon2D edge, float ph)>();
		for (int k = 0; k < 3; k++)
		{
			Polygon2D g = Fx.Poly(r, new Vector2[3], new Color(0.45f, 0.8f, 1f, 0f), additive: true);
			Polygon2D e = Fx.Poly(r, new Vector2[3], new Color(0.85f, 0.97f, 1f, 0f), additive: true);
			ripples.Add((g, e, k / 3f));
		}
		// 光焰：拉长的柔光团从脚下往上流，左右摆动，越往上越细越淡
		var flames = new List<(Sprite2D s, float x, float w, float h, float period, float ph, float sway)>();
		for (int i = 0; i < 34; i++)
		{
			float w = body * Fx.Rand(0.07f, 0.16f), h = body * Fx.Rand(0.35f, 0.7f);
			Sprite2D s = Glow(new Vector2(w, h), new Color(0.45f, 0.8f, 1f, 0f));
			flames.Add((s, Fx.Rand(-0.26f, 0.26f) * body, w, h, Fx.Rand(1.1f, 1.9f), Fx.Rand(0f, 1f), Fx.Rand(6f, 18f)));
		}
		// 光点：细小的四芒星往上飘、闪烁
		var sparks = new List<(Polygon2D p, float x, float period, float ph)>();
		for (int i = 0; i < 40; i++)
		{
			Polygon2D p = Fx.Poly(r, Fx.Star4(Fx.Rand(2.5f, 6f), 0.25f), Fx.Rng.NextDouble() < 0.6 ? Pal.Ice : Pal.White, additive: true);
			sparks.Add((p, Fx.Rand(-0.6f, 0.6f) * body, Fx.Rand(1.6f, 3.2f), Fx.Rand(0f, 1f)));
		}
		Fx.Animate(r, life, t =>
		{
			float time = t * life;
			float on = Ease.OutCubic(Mathf.Clamp(time / 0.8f, 0f, 1f));
			float grow = 0.65f + 0.35f * Mathf.Clamp((time - 2.5f) / 3f, 0f, 1f);       // 指天后越来越盛
			float breathe = 0.85f + 0.15f * Mathf.Sin(time * 2.6f);
			poolWide.Modulate = new Color(0.3f, 0.6f, 1f, 0.35f * on * grow * breathe);
			pool.Modulate = new Color(0.75f, 0.95f, 1f, 0.55f * on * grow * breathe);
			sheath.Modulate = new Color(0.35f, 0.7f, 1f, 0.16f * on * grow * breathe);
			foreach (var (g, e, ph) in ripples)
			{
				float u = Mathf.PosMod(time / 1.8f + ph, 1f);
				float rad = body * (0.25f + 0.95f * Ease.OutCubic(u));
				float a = on * Mathf.Sin(u * Mathf.Pi) * (1f - u * 0.5f);
				Fx.Reshape(g, Fx.StrokePts(Ring(rad, 0.26f), 12f + 18f * u));
				Fx.Reshape(e, Fx.StrokePts(Ring(rad, 0.26f), 2f));
				g.Color = new Color(0.45f, 0.8f, 1f, 0.22f * a);
				e.Color = new Color(0.85f, 0.97f, 1f, 0.6f * a);
			}
			foreach (var (s, x, w, h, period, ph, sway) in flames)
			{
				float u = Mathf.PosMod(time / period + ph, 1f);
				float y = -u * body * 1.25f;
				s.Position = new Vector2(x * (1f - 0.45f * u) + sway * Mathf.Sin(time * 2.3f + ph * 11f) * u, y - h * 0.3f);
				s.Scale = new Vector2(w * (1f - 0.55f * u), h * (0.7f + 0.6f * u)) / dot;
				s.Modulate = new Color(0.45f + 0.4f * u, 0.8f + 0.15f * u, 1f, 0.5f * Mathf.Sin(u * Mathf.Pi) * on * grow);
			}
			foreach (var (p, x, period, ph) in sparks)
			{
				float u = Mathf.PosMod(time / period + ph, 1f);
				p.Position = new Vector2(x + 14f * Mathf.Sin(time * 1.7f + ph * 9f), -u * body * 1.6f);
				p.Modulate = new Color(1f, 1f, 1f, on * Mathf.Sin(u * Mathf.Pi) * (0.5f + 0.5f * Mathf.Sin(time * 9f + ph * 20f)));
				p.Rotation = time * 0.8f + ph;
			}
			r.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - (life - 0.3f)) / 0.3f, 0f, 1f));
		});
		Fx.FreeAfter(r, life + 0.05f);
	}

	/// <summary>闭合的压扁圆（首尾相接，给 StrokePts 用）。</summary>
	private static Vector2[] Ring(float radius, float squashY)
	{
		Vector2[] c = Fx.Circle(radius, 60, squashY);
		Vector2[] r = new Vector2[c.Length + 1];
		c.CopyTo(r, 0);
		r[^1] = c[0];
		return r;
	}

	private static bool HasAnim(NCreature? player, string name) =>
		player != null && GodotObject.IsInstanceValid(player) && player.Visuals.SpineBody is { } body && body.HasAnimation(name);

	/// <summary>单手指天：骨骼里有 point_sky（_in / _loop）动画就播放，之后停在指天姿势。</summary>
	private static void PointSky(NCreature? player)
	{
		if (player == null || !GodotObject.IsInstanceValid(player) || player.Visuals.SpineBody is not { } body)
		{
			return;
		}
		var state = body.GetAnimationState();
		string intro = body.HasAnimation("point_sky_in") ? "point_sky_in" : body.HasAnimation("point_sky_quick") ? "point_sky_quick" : "";
		if (intro.Length > 0)
		{
			P3Makoto.Core.Compat.SetAnimation(state, intro, loop: false);
			P3Makoto.Core.Compat.AddAnimation(state, body.HasAnimation("point_sky_loop") ? "point_sky_loop" : "idle_loop", 0f, loop: true);
		}
	}

	/// <summary>竖直的柔边光柱：左右两半各一个四边形，中间亮、两边透明（顶点色），加色混合。</summary>
	private static (Polygon2D l, Polygon2D r) Column(Node parent, Color c)
	{
		Polygon2D Half()
		{
			Polygon2D p = new() { Material = Fx.Additive, Polygon = new Vector2[6], VertexColors = new[] { c, c, c, c, c, c } };
			// 上段矩形 + 下段收拢梯形。v0.14.28 第二版（用户：锥形侧边仍有明显边界）：梯形按 (3,2,5)+(2,4,5) 切分，
			// 每一行都是从外缘透明线性过渡到中心，不会在指尖附近挤成 2px 宽的硬边
			p.Polygons = new Godot.Collections.Array { new[] { 0, 1, 2, 3 }, new[] { 3, 2, 5 }, new[] { 2, 4, 5 } };
			parent.AddChild(p);
			return p;
		}
		return (Half(), Half());
	}

	private static void SetColumn((Polygon2D l, Polygon2D r) col, float x, float yTop, float yBot, float width, Color c, float topFade = 1f)
	{
		float hw = width * 0.5f;
		Color edge = new(c, 0f), core = c, coreTop = new(c, c.A * topFade);
		// v0.14.28（用户：指天光柱在指尖高度有一道明显的横向分界）：底部不再是满宽、满亮度的平边。
		// 每半边分两段：上段矩形（外缘透明 → 中心亮），下段向 (x, yBot) 收拢的梯形（同样外缘透明），光从指尖一点发散出去。
		// 两段分开三角化（Polygons），亮度在交界处连续，不会沿长对角线插值出新的硬边
		float taper = Mathf.Min(hw * 1.6f, Mathf.Max(0f, yBot - yTop) * 0.5f);
		float ym = yBot - taper, tipHw = Mathf.Min(hw * 0.06f, 2f);
		col.l.Polygon = new[] { new Vector2(x - hw, yTop), new Vector2(x, yTop), new Vector2(x, ym), new Vector2(x - hw, ym), new Vector2(x, yBot), new Vector2(x - tipHw, yBot) };
		col.l.VertexColors = new[] { edge, coreTop, core, edge, core, edge };
		col.r.Polygon = new[] { new Vector2(x + hw, yTop), new Vector2(x, yTop), new Vector2(x, ym), new Vector2(x + hw, ym), new Vector2(x, yBot), new Vector2(x + tipHw, yBot) };
		col.r.VertexColors = new[] { edge, coreTop, core, edge, core, edge };
	}

	// 4.30 光柱从指尖直冲天顶；地面光盘铺开；光柱越来越粗、天顶亮起、光线往上流 → 全屏白
	private static void Ascend(Node2D world, Vector2 tip, Vector2 feet, float body, Vector2 view, float life)
	{
		Node2D n = new();
		world.AddChild(n);
		float dot = Fx.SoftDot.GetWidth();
		// 地面光盘（压扁的柔光，越铺越开）+ 亮边
		Sprite2D disc = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = feet, Modulate = new Color(0.55f, 0.85f, 1f, 0f) };
		n.AddChild(disc);
		Polygon2D discEdge = Fx.Poly(n, new Vector2[3], new Color(0.85f, 0.97f, 1f, 0f), additive: true);
		discEdge.Position = feet;
		// 天顶的光（光柱打上去的地方）
		Sprite2D sky = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = new Vector2(tip.X, -view.Y * 0.05f), Modulate = new Color(0.8f, 0.95f, 1f, 0f) };
		n.AddChild(sky);
		// 光柱：外层宽光晕 + 内层亮芯
		var glow = Column(n, new Color(0.45f, 0.8f, 1f, 0.55f));
		var core = Column(n, new Color(1f, 1f, 1f, 1f));
		// 光柱周围往上流的细光线
		var streams = new List<((Polygon2D, Polygon2D) col, float dx, float period, float ph, float w)>();
		for (int i = 0; i < 12; i++)
		{
			streams.Add((Column(n, new Color(0.7f, 0.92f, 1f, 0.5f)), Fx.Rand(-1f, 1f) * body * 0.55f, Fx.Rand(0.7f, 1.3f), Fx.Rand(0f, 1f), Fx.Rand(2f, 6f)));
		}
		// 指尖的闪光
		Polygon2D star = Fx.Poly(n, Fx.Star4(body * 0.22f, 0.1f), Pal.White, additive: true);
		star.Position = tip;
		Sprite2D burst = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = tip, Modulate = new Color(0.8f, 0.95f, 1f, 0f) };
		n.AddChild(burst);
		Audio.Sfx.Clip("Snd_FX_Btl_Trigger_00_Flash", 0.75f);
		Audio.Sfx.Clip("Wipe_In_ShadowTime", 0.5f, 1.6f);
		NGame.Instance?.ScreenShake(ShakeStrength.Weak, ShakeDuration.Short);
		Fx.Animate(n, life, t =>
		{
			float time = t * life;
			// 光柱：0.18s 冲上天顶；之后越来越粗
			float up = Ease.OutExpo(Mathf.Clamp(time / 0.18f, 0f, 1f));
			float yTop = Mathf.Lerp(tip.Y, -20f, up);
			float thick = Ease.InOutCubic(Mathf.Clamp((time - 1.2f) / 3.5f, 0f, 1f));
			float pulse = 1f + 0.06f * Mathf.Sin(time * 14f);
			SetColumn(glow, tip.X, yTop, tip.Y, (body * 0.22f + body * 0.9f * thick) * pulse, new Color(0.45f, 0.8f, 1f, 0.5f + 0.3f * thick), 0.6f);
			SetColumn(core, tip.X, yTop, tip.Y, (6f + body * 0.06f * thick) * pulse, new Color(1f, 1f, 1f, 0.95f));
			// 指尖闪光：一开始最亮，之后保持一点
			float fl = time < 0.12f ? time / 0.12f : Mathf.Max(0.35f, 1f - (time - 0.12f) / 0.6f);
			star.Scale = Vector2.One * fl * (1f + 0.3f * thick);
			star.Rotation = time * 0.6f;
			burst.Scale = Vector2.One * body * (0.5f + 0.6f * thick) * fl / dot;
			burst.Modulate = new Color(0.8f, 0.95f, 1f, 0.7f * fl);
			// 地面光盘：0.1s 起向四周铺开
			float spread = Ease.OutCubic(Mathf.Clamp((time - 0.1f) / 2.6f, 0f, 1f));
			float dw = Mathf.Lerp(body * 0.6f, view.X * 1.3f, spread);
			disc.Scale = new Vector2(dw, dw * 0.2f) / dot;
			disc.Modulate = new Color(0.55f, 0.85f, 1f, (0.45f + 0.25f * thick) * Mathf.Clamp(time / 0.2f, 0f, 1f));
			Fx.Reshape(discEdge, Fx.StrokePts(Ring(dw * 0.42f, 0.2f), 3f));
			discEdge.Color = new Color(0.85f, 0.97f, 1f, 0.7f * (1f - spread) * Mathf.Clamp(time / 0.2f, 0f, 1f));
			// 天顶亮起
			float s2 = Ease.InCubic(Mathf.Clamp((time - 0.5f) / (life - 0.8f), 0f, 1f));
			sky.Scale = Vector2.One * (body * 1.2f + view.X * 1.4f * s2) / dot;
			sky.Modulate = new Color(0.8f, 0.95f, 1f, 0.25f + 0.6f * s2);
			// 往上流的细光线：1.2s 起出现
			float sOn = Mathf.Clamp((time - 1.2f) / 0.6f, 0f, 1f);
			foreach (var (col, dx, period, ph, w) in streams)
			{
				float u = Mathf.PosMod(time / period + ph, 1f);
				float yb = Mathf.Lerp(feet.Y, -view.Y * 0.2f, u);
				float x = tip.X + dx * (1f + thick);
				SetColumn(((Polygon2D, Polygon2D))col, x, yb - body * 0.6f, yb, w, new Color(0.7f, 0.92f, 1f, 0.45f * sOn * Mathf.Sin(u * Mathf.Pi)), 0f);
			}
		});
		Fx.FreeAfter(n, life + 0.05f);
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

	// 9.60 全屏白 → 深蓝 → 「MEMENTO MORI / ……于是，早晨来临。」（用户保留了这一幕；去掉了伟大封印巨门图）
	private static void Coda(CanvasLayer top, Vector2 view, float total)
	{
		Node2D root = new();
		top.AddChild(root);
		ColorRect bg = new() { Size = view, Color = Colors.White, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(bg);
		Node2D coda = new() { Position = new Vector2(view.X / 2f, view.Y * 0.5f), Modulate = new Color(1f, 1f, 1f, 0f) };
		root.AddChild(coda);
		Label codaText = Typo.Text(coda, Zh ? "M E M E N T O   M O R I" : "...and the morning came.", Typo.Latin, 30, new Color(0.8f, 0.9f, 1f), new Vector2(-view.X / 2f, -20f), HorizontalAlignment.Center);
		codaText.Size = new Vector2(view.X, codaText.Size.Y);
		Label codaSub = Typo.Text(coda, Zh ? "……于是，早晨来临。" : "", Typo.Cjk, 22, new Color(0.65f, 0.75f, 0.9f), new Vector2(-view.X / 2f, 28f), HorizontalAlignment.Center);
		codaSub.Size = new Vector2(view.X, codaSub.Size.Y);
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float toNavy = Ease.InOutSine(Mathf.Clamp((time - 0.3f) / 0.7f, 0f, 1f));
			bg.Color = Colors.White.Lerp(new Color(0.02f, 0.04f, 0.1f), toNavy);
			float ci = Ease.OutCubic(Mathf.Clamp((time - 0.9f) / 0.7f, 0f, 1f));
			coda.Modulate = new Color(1f, 1f, 1f, ci);
			coda.Position = new Vector2(view.X / 2f, view.Y * 0.5f + 10f * (1f - ci));
		});
	}

	// ───────────────────────── 要闭上眼睛吗？ ─────────────────────────

	private static Font? _endingFont;

	/// <summary>结局字体：P3R 原版字体（Codex 提取到 p3makoto/fonts/p3r_ending.*）优先，否则系统宋体 / 明朝类。</summary>
	private static Font EndingFont
	{
		get
		{
			if (_endingFont != null)
			{
				return _endingFont;
			}
			foreach (string ext in new[] { "otf", "ttf" })
			{
				// pck 里的字体是原始文件（不经过导入），和 CharSelectInfo 的 Barlow 一样用 LoadDynamicFont 读
				string path = $"res://p3makoto/fonts/p3r_ending.{ext}";
				FontFile f = new();
				if (FileAccess.FileExists(path) && f.LoadDynamicFont(path) == Error.Ok)
				{
					return _endingFont = f;
				}
			}
			return _endingFont = new SystemFont { FontNames = new[] { "Noto Serif CJK SC", "Source Han Serif SC", "Songti SC", "SimSun", "NSimSun", "Yu Mincho", "MS Mincho" }, FontWeight = 600 };
		}
	}

	/// <summary>
	/// 「要闭上眼睛吗？」（v0.14.29 起，v0.14.30 按实机反馈改：全屏、排在 MEMENTO MORI 前面、去掉眼睑边缘的细白线）。
	/// · 从白幕里淡入全屏的模糊天台（ui/ending_rooftop.png，美术会话出图；没有时用淡紫灰渐变 + 柔光块代替）；
	///   中间「▸要 闭 上 眼 睛 吗 ？」（光标三角闪烁），左下六个小点依次亮，右下大号「闭上眼睛」（ui/ending_closeeye.png，P3R 原版；没有时文字大小交错排）。
	///   位置按原版截图在画面里的比例：问句 (0.50, 0.53)、小点 x 0.344–0.416 / y 0.743、大字 x 0.571 / 底 y 0.79。
	/// · 点击任意处 → 文字淡出 → 眨一次眼，再缓慢闭到一条缝，最后极慢地闭紧（Lids 曲线）；
	///   全黑后黑底上 MEMENTO MORI / ……于是，早晨来临。淡入淡出，然后才交还游戏（finish）。
	///   黑幕留着，等结算画面打开时（ArchitectGameOverPatch → RevealGameOver）再淡出；保险：交还 12s 后无论如何移除。
	/// · 眼睑用普通 Polygon2D（不走 Fx.Poly 的抗锯齿着色器：它给每个多边形边缘加一圈柔边，实心块和羽化带的接缝处会出现细白线）。
	/// </summary>
	private const float LidCloseLen = 7.25f;

	private static void CloseEyes(CanvasLayer top, Vector2 view, System.Action finish)
	{
		float W = view.X, H = view.Y;
		Node2D root = new() { Name = "P3SealEyes", Modulate = new Color(1f, 1f, 1f, 0f) };
		top.AddChild(root);
		root.AddChild(new ColorRect { Size = view, Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore });

		// ── 全屏背景 ──
		Node2D frame = new();
		root.AddChild(frame);
		// 闭眼越来越模糊（用户：只用美术会话的第一张图，模糊效果由 Claude 做）：同一张图预先做了两档更模糊的版本
		// （ending_rooftop_blur1 / blur2，tools/vfx/ending_blur.py），闭眼时按眼睑合上的程度交叉淡入
		var blurs = new List<Sprite2D>();
		if (UiTex("ending_rooftop") is Texture2D bgTex)
		{
			float sc = Mathf.Max(W / bgTex.GetWidth(), H / bgTex.GetHeight());
			frame.AddChild(new Sprite2D { Texture = bgTex, Position = view / 2f, Scale = Vector2.One * sc });
			foreach (string b in new[] { "ending_rooftop_blur1", "ending_rooftop_blur2" })
			{
				if (UiTex(b) is Texture2D bt)
				{
					Sprite2D sp = new() { Texture = bt, Position = view / 2f, Scale = Vector2.One * Mathf.Max(W / bt.GetWidth(), H / bt.GetHeight()), Modulate = new Color(1f, 1f, 1f, 0f) };
					frame.AddChild(sp);
					blurs.Add(sp);
				}
			}
		}
		else
		{
			Polygon2D g = new() { Polygon = new[] { Vector2.Zero, new Vector2(W, 0f), view, new Vector2(0f, H) } };
			g.VertexColors = new[] { new Color(0.80f, 0.80f, 0.88f), new Color(0.80f, 0.80f, 0.88f), new Color(0.66f, 0.68f, 0.78f), new Color(0.66f, 0.68f, 0.78f) };
			frame.AddChild(g);
			float dot = Fx.SoftDot.GetWidth();
			foreach ((Vector2 at, Vector2 sz, Color c) in new[]
			{
				(new Vector2(0.45f, 0.58f), new Vector2(0.28f, 0.09f), new Color(0.12f, 0.12f, 0.25f, 0.35f)),   // 远处躺着的人影
				(new Vector2(0.41f, 0.46f), new Vector2(0.06f, 0.13f), new Color(0.95f, 0.92f, 0.85f, 0.3f)),
				(new Vector2(0.60f, 0.48f), new Vector2(0.10f, 0.13f), new Color(0.35f, 0.18f, 0.15f, 0.3f)),
				(new Vector2(0.75f, 0.52f), new Vector2(0.15f, 0.24f), new Color(0.92f, 0.93f, 0.97f, 0.35f)),
				(new Vector2(0.18f, 0.12f), new Vector2(0.40f, 0.08f), new Color(0.4f, 0.42f, 0.55f, 0.3f)),
			})
			{
				frame.AddChild(new Sprite2D { Texture = Fx.SoftDot, Position = new Vector2(W * at.X, H * at.Y), Scale = new Vector2(W * sz.X, H * sz.Y) / dot, Modulate = c });
			}
		}

		// ── 场景的动态细节（v0.14.33，用户：别那么"死"）──
		var bgSprites = new List<(Sprite2D sp, float sc)>();
		foreach (Node c in frame.GetChildren())
		{
			if (c is Sprite2D sp && sp.Texture != Fx.SoftDot)
			{
				bgSprites.Add((sp, sp.Scale.X));
			}
		}
		Ambient(root, view, bgSprites);

		// ── 文字 ──
		Node2D texts = new();
		root.AddChild(texts);
		// v0.14.30：问句、六个小点、「闭上眼睛」都用 P3R 原版简中贴图（art/p3r_tex/ending，CinemaScope_02 图集），按 1080p 原尺寸 × H/1080 缩放；
		// 位置按原版截图在画面里的比例：问句中心 (0.50, 0.53)、小点中心 (0.38, 0.743)、大字中心 (0.639, 0.726)。没有贴图时退回文字
		float ui = H / 1080f;
		int qs = (int)(H * 0.03f);
		// v0.14.31（用户：UI 整体往上挪一点）：整组上移 0.08H
		float qy = H * 0.45f;
		float qHalf;
		if (UiTex("ending_question") is Texture2D qTex)
		{
			texts.AddChild(new Sprite2D { Texture = qTex, Position = new Vector2(W * 0.5f, qy), Scale = Vector2.One * ui });
			qHalf = qTex.GetWidth() * ui / 2f;
			qs = (int)(qTex.GetHeight() * ui);
		}
		else
		{
			string q = Zh ? "要 闭 上 眼 睛 吗 ？" : "Close your eyes?";
			Label question = Typo.Text(texts, q, EndingFont, qs, new Color(1f, 1f, 1f), new Vector2(0f, qy - qs * 0.7f), HorizontalAlignment.Center,
				shadow: new Color(0.1f, 0.1f, 0.2f, 0.6f), shadowOffset: new Vector2(2f, 2f));
			question.Size = new Vector2(W, question.Size.Y);
			qHalf = question.GetMinimumSize().X / 2f;
		}
		Polygon2D cursor = Fx.Poly(texts, new[] { new Vector2(0f, -qs * 0.32f), new Vector2(qs * 0.5f, 0f), new Vector2(0f, qs * 0.32f) }, Colors.White);
		cursor.Position = new Vector2(W / 2f - qHalf - qs * 0.9f, qy);
		var dots = new List<CanvasItem>();
		if (UiTex("ending_dots") is Texture2D dTex)
		{
			// 原版六点是一张图：整体轻轻呼吸
			Sprite2D d = new() { Texture = dTex, Position = new Vector2(W * 0.38f, H * 0.663f), Scale = Vector2.One * ui };
			texts.AddChild(d);
			dots.Add(d);
		}
		else
		{
			for (int i = 0; i < 6; i++)
			{
				Polygon2D d = Fx.Poly(texts, Fx.Circle(H * 0.0058f, 12), Colors.White);
				d.Position = new Vector2(W * 0.344f + i * W * 0.0144f, H * 0.663f);
				dots.Add(d);
			}
		}
		// 「闭上眼睛」是按钮（v0.14.31，用户：之前点哪里都行；选中时要有动效）：big 以自身中心为原点，方便缩放
		Vector2 bigAt = new(W * 0.639f, H * 0.646f);
		Node2D big = new() { Position = bigAt };
		texts.AddChild(big);
		Vector2 bigSize;
		Sprite2D bigGlow = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(1f, 1f, 1f, 0f) };
		big.AddChild(bigGlow);
		if (UiTex("ending_closeeye") is Texture2D eyeTex)
		{
			bigSize = new Vector2(eyeTex.GetWidth(), eyeTex.GetHeight()) * ui;
			big.AddChild(new Sprite2D { Texture = eyeTex, Scale = Vector2.One * ui });
		}
		else
		{
			bigSize = new Vector2(H * 0.25f, H * 0.1f);
			float x = -bigSize.X / 2f;
			string word = Zh ? "闭上眼睛" : "CLOSE";
			for (int i = 0; i < word.Length; i++)
			{
				int sz = (int)(H * (i % 2 == 0 ? 0.09f : 0.05f));
				Label ch = Typo.Text(big, word[i].ToString(), EndingFont, sz, new Color(0.93f, 0.93f, 0.97f, 0.85f), new Vector2(x, bigSize.Y * 0.5f - sz * 1.25f),
					shadow: new Color(0.25f, 0.25f, 0.35f, 0.7f), shadowOffset: new Vector2(3f, 3f));
				x += ch.GetMinimumSize().X * 0.92f;
			}
		}

		// ── 眼睑：杏仁形弧边 + 羽化（普通 Polygon2D，同一个多边形里实心 + 羽化，没有接缝） ──
		float bowLid = H * 0.1f, feather = H * 0.11f;
		Node2D MakeLid(bool upper)
		{
			Node2D lid = new();
			root.AddChild(lid);
			const int M = 48;
			float s = upper ? 1f : -1f;
			// 顶点：0..M-1 眼睑边缘（全黑），M..2M-1 羽化外沿（透明），2M、2M+1 远处两角（全黑）
			var pts = new Vector2[M * 2 + 2];
			var cols = new Color[M * 2 + 2];
			for (int i = 0; i < M; i++)
			{
				float u = i / (M - 1f), bow = 1f - (2f * u - 1f) * (2f * u - 1f);
				float x = Mathf.Lerp(-W * 0.1f, W * 1.1f, u);
				float y = -s * bowLid * bow;                      // 弧向外：睁开的形状两头尖、中间宽，闭眼时眼角先合
				pts[i] = new Vector2(x, y);
				pts[M + i] = new Vector2(x, y + s * feather);
				cols[i] = Colors.Black;
				cols[M + i] = new Color(0f, 0f, 0f, 0f);
			}
			pts[M * 2] = new Vector2(-W * 0.1f, -s * H * 1.3f);
			pts[M * 2 + 1] = new Vector2(W * 1.1f, -s * H * 1.3f);
			cols[M * 2] = cols[M * 2 + 1] = Colors.Black;
			var polys = new Godot.Collections.Array();
			for (int i = 0; i < M - 1; i++)
			{
				polys.Add(new[] { i, i + 1, M + i + 1, M + i });                 // 羽化带
				polys.Add(new[] { M * 2, i, i + 1 });                           // 实心部分（扇形连到远处一角）
			}
			polys.Add(new[] { M * 2, M - 1, M * 2 + 1 });
			lid.AddChild(new Polygon2D { Polygon = pts, VertexColors = cols, Polygons = polys });
			return lid;
		}
		Node2D lidTop = MakeLid(true), lidBot = MakeLid(false);
		void PlaceLids(float p)
		{
			// p=0：睁开（眼角处刚好在画面外）；p=1：上下眼睑在中线重叠，全黑
			float mid = H * 0.5f;
			float openTop = -feather - 4f, openBot = H + feather + 4f;
			lidTop.Position = new Vector2(0f, Mathf.Lerp(openTop, mid + bowLid, p));
			lidBot.Position = new Vector2(0f, Mathf.Lerp(openBot, mid - bowLid, p));
		}
		PlaceLids(0f);

		// 入场：从白幕里淡入（白 → 画面）
		ColorRect white = new() { Size = view, Color = Colors.White, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(white);
		root.Modulate = Colors.White;
		Fx.Animate(white, 1.2f, t => white.Color = new Color(1f, 1f, 1f, 1f - Ease.InOutSine(t)));
		Fx.Animate(texts, 600f, t =>
		{
			float time = t * 600f;
			cursor.Modulate = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Abs(Mathf.Cos(time * 2.6f)));
			for (int i = 0; i < dots.Count; i++)
			{
				float ph = dots.Count == 1 ? time * 1.6f : Mathf.PosMod(time * 1.2f - i * 0.18f, 1.2f);
				dots[i].Modulate = new Color(1f, 1f, 1f, dots.Count == 1 ? 0.7f + 0.3f * Mathf.Sin(ph) : 0.45f + 0.55f * Mathf.Exp(-ph * 4f));
			}
		});

		// ── 等玩家选择：只有「闭上眼睛」能点 ──
		// 全屏透明挡板只拦截点击（不触发），按钮区域 = 大字外扩一圈。
		// 待机：大字轻轻呼吸；悬停：放大到 1.08、亮起一团柔光、轻微上浮，光标三角挪到大字左侧指着它；点下：一闪、放大到 1.15 后随文字淡出
		bool chosen = false, hover = false;
		ColorRect blocker = new() { Size = view, Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Stop };
		top.AddChild(blocker);
		Vector2 pad = new(H * 0.03f, H * 0.02f);
		Control hit = new() { Position = bigAt - bigSize / 2f - pad, Size = bigSize + pad * 2f, MouseFilter = Control.MouseFilterEnum.Stop, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		top.AddChild(hit);
		bigGlow.Scale = new Vector2(bigSize.X * 1.4f, bigSize.Y * 1.6f) / Fx.SoftDot.GetWidth();
		Vector2 cursorHome = cursor.Position, cursorBig = bigAt + new Vector2(-bigSize.X / 2f - qs * 1.1f, 0f);
		float hoverK = 0f;
		hit.MouseEntered += () => { if (!chosen) { hover = true; Audio.Sfx.Clip("Sys_Cursor", 0.5f); } };
		hit.MouseExited += () => hover = false;
		Fx.Animate(big, 600f, t =>
		{
			if (chosen)
			{
				return;
			}
			float time = t * 600f;
			hoverK = Mathf.MoveToward(hoverK, hover ? 1f : 0f, 1f / 60f / 0.18f);
			float e = Ease.OutCubic(hoverK);
			big.Scale = Vector2.One * (1f + 0.08f * e + 0.012f * Mathf.Sin(time * 2f) * (1f - e));
			big.Position = bigAt + new Vector2(0f, -6f * e);
			big.Modulate = new Color(1f, 1f, 1f, 0.82f + 0.18f * e + 0.06f * Mathf.Sin(time * 2f) * (1f - e));
			bigGlow.Modulate = new Color(0.85f, 0.9f, 1f, 0.35f * e);
			cursor.Position = cursorHome.Lerp(cursorBig, e);
		});
		ulong shownAt = Time.GetTicksMsec();
		void Choose()
		{
			if (chosen)
			{
				return;
			}
			chosen = true;
			Audio.Sfx.Clip("Btl_Ok", 0.6f);
			hit.QueueFree();                                                        // 挡板留着：闭眼期间点击不能漏到下面的游戏界面
			Vector2 s0 = big.Scale;
			Fx.Animate(big, 0.35f, t =>
			{
				big.Scale = s0.Lerp(Vector2.One * 1.15f, Ease.OutCubic(t));
				bigGlow.Modulate = new Color(1f, 1f, 1f, 0.6f * (1f - t));
			});
			Close();
		}
		hit.GuiInput += ev =>
		{
			if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
			{
				Choose();
			}
		};
		// v0.14.45：手柄 / 键盘确认键也能选
		Fx.Animate(top, 600f, _ =>
		{
			if (!chosen && Time.GetTicksMsec() - shownAt > 400 && Input.IsActionJustPressed("ui_accept"))
			{
				Choose();
			}
		});

		// 点下之后：蓝蝶先飞（左下飞入 → 中间绕一圈 → 右上飞走），飞走时才开始闭眼
		void Close()
		{
			// v0.14.33（用户：眼睛完全闭上时蝴蝶正好飞出）：蝴蝶和闭眼同时开始、同样时长，画在眼睑之上（闭到一条缝、全黑时仍看得见它发光）
			Fx.Animate(texts, 0.6f, t => texts.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InOutSine(t)));
			Butterfly(root, view, LidCloseLen);
			CloseLids();
		}

		void CloseLids()
		{
			// 先由慢到快闭上 → 由快到慢睁开（眨一次眼）→ 缓慢闭到只剩一条缝 → 极慢地闭紧
			//   0.00–0.85 慢→快闭上（全黑）；停 0.1；0.95–1.95 快→慢睁开到 12%；1.95–4.25 缓慢闭到 88%；4.25–7.25 极慢闭紧
			const float closeLen = LidCloseLen, hold = 0.6f;
			static float Lids(float time)
			{
				if (time < 0.85f) return Ease.InCubic(time / 0.85f);
				if (time < 0.95f) return 1f;
				if (time < 1.95f) return Mathf.Lerp(1f, 0.12f, Ease.OutCubic((time - 0.95f) / 1.0f));
				if (time < 4.25f) return Mathf.Lerp(0.12f, 0.88f, Ease.InOutSine((time - 1.95f) / 2.3f));
				return Mathf.Lerp(0.88f, 1f, Ease.OutSine(Mathf.Clamp((time - 4.25f) / 3.0f, 0f, 1f)));
			}
			Fx.Animate(root, closeLen + hold, t =>
			{
				float time = t * (closeLen + hold);
				float p = Lids(time);
				PlaceLids(p);
				float dark = 1f - 0.45f * p;
				frame.Modulate = new Color(dark, dark, dark * 1.02f);
				// 模糊：眨眼那一下几乎不变；闭到一条缝的过程中换成第一档，最后极慢合上时换成第二档
				if (blurs.Count > 0)
				{
					blurs[0].Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 2.2f) / 1.8f, 0f, 1f));
				}
				if (blurs.Count > 1)
				{
					blurs[1].Modulate = new Color(1f, 1f, 1f, Ease.InOutSine(Mathf.Clamp((time - 4.0f) / 2.5f, 0f, 1f)));
				}
			});
			Fx.After(root, closeLen + hold, "seal eyes closed", () =>
			{
				// 全黑：其余层都藏起来（之后淡出黑幕时直接露出结算画面），黑底上出 MEMENTO MORI
				foreach (Node c in top.GetChildren())
				{
					if (c != root && c is CanvasItem ci)
					{
						ci.Visible = false;
					}
				}
				Node first = root.GetChild(0);
				foreach (Node c in root.GetChildren())
				{
					if (c != first && c is CanvasItem ci)
					{
						ci.Visible = false;
					}
				}
				Memento(root, view, () =>
				{
					finish();
					top.GetTree().CreateTimer(12.0, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () =>
					{
						if (GodotObject.IsInstanceValid(top))
						{
							top.QueueFree();
						}
					};
				});
			});
		}
	}

	/// <summary>
	/// 闭眼场景的动态细节（v0.14.33）：
	/// · 镜头呼吸：背景（含两档模糊）极慢地推近 / 平移（±2.5%，周期约 26s）；
	/// · 左上方的阳光：几道暖白色斜光束缓慢起伏 + 一团柔光晕；
	/// · 空气里的光尘：细小的暖白光点慢慢上浮、闪烁；
	/// · 樱花瓣（P3 的结局在 3 月 5 日）：几片花瓣翻转着斜飘过画面；
	/// · 云影：两团很淡的冷色暗影从地面上缓缓移过；
	/// · 四周暗角轻微呼吸。
	/// 都在背景之上、文字之下。
	/// </summary>
	private static void Ambient(Node2D root, Vector2 view, List<(Sprite2D sp, float sc)> bg)
	{
		float W = view.X, H = view.Y, dot = Fx.SoftDot.GetWidth();
		Node2D amb = new();
		root.AddChild(amb);
		// 云影
		var clouds = new List<(Sprite2D s, float y, float speed, float ph)>();
		for (int i = 0; i < 2; i++)
		{
			Sprite2D c = new() { Texture = Fx.SoftDot, Scale = new Vector2(W * 0.55f, H * 0.32f) / dot, Modulate = new Color(0.25f, 0.24f, 0.4f, 0.13f) };
			amb.AddChild(c);
			clouds.Add((c, H * (0.45f + 0.3f * i), W * (0.018f + 0.008f * i), i * 0.55f));
		}
		// 阳光：光束 + 光晕
		Vector2 sun = new(W * 0.04f, -H * 0.12f);
		var rays = new List<(Polygon2D p, float a, float ph)>();
		for (int i = 0; i < 5; i++)
		{
			float a = 0.55f + i * 0.13f + Fx.Rand(-0.03f, 0.03f), len = H * Fx.Rand(1.2f, 1.6f), wid = H * Fx.Rand(0.05f, 0.11f);
			Polygon2D r = new()
			{
				Polygon = new[] { new Vector2(0f, -wid * 0.1f), new Vector2(len, -wid), new Vector2(len, wid), new Vector2(0f, wid * 0.1f) },
				VertexColors = new[] { new Color(1f, 0.95f, 0.85f, 0.16f), new Color(1f, 0.95f, 0.85f, 0f), new Color(1f, 0.95f, 0.85f, 0f), new Color(1f, 0.95f, 0.85f, 0.16f) },
				Material = Fx.Additive, Position = sun, Rotation = a
			};
			amb.AddChild(r);
			rays.Add((r, a, Fx.Rand(0f, Mathf.Tau)));
		}
		Sprite2D bloom = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = new Vector2(W * 0.1f, H * 0.05f), Scale = Vector2.One * H * 0.9f / dot, Modulate = new Color(1f, 0.93f, 0.85f, 0.18f) };
		amb.AddChild(bloom);
		// 光尘
		var motes = new List<(Sprite2D s, Vector2 at, float speed, float ph, float sz)>();
		for (int i = 0; i < 46; i++)
		{
			float sz = H * Fx.Rand(0.003f, 0.008f);
			Sprite2D m = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * sz * 2f / dot };
			amb.AddChild(m);
			motes.Add((m, new Vector2(Fx.Rand(0f, W), Fx.Rand(0f, H)), H * Fx.Rand(0.008f, 0.025f), Fx.Rand(0f, Mathf.Tau), sz));
		}
		// 樱花瓣
		var petals = new List<(Node2D n, Polygon2D p, Vector2 start, float speed, float ph, float spin)>();
		for (int i = 0; i < 9; i++)
		{
			Node2D n = new();
			amb.AddChild(n);
			float sz = H * Fx.Rand(0.008f, 0.013f);
			Polygon2D petal = Fx.Poly(n, new[] { new Vector2(0f, -sz), new Vector2(sz * 0.55f, -sz * 0.2f), new Vector2(sz * 0.35f, sz * 0.7f), new Vector2(0f, sz * 0.5f),
				new Vector2(-sz * 0.35f, sz * 0.7f), new Vector2(-sz * 0.55f, -sz * 0.2f) }, new Color(1f, 0.86f, 0.9f, 0.85f));
			petals.Add((n, petal, new Vector2(Fx.Rand(-0.3f, 0.9f) * W, Fx.Rand(-0.3f, 0.2f) * H), Fx.Rand(0.045f, 0.075f), Fx.Rand(0f, 1f), Fx.Rand(1.2f, 2.6f)));
		}
		// 暗角
		TextureRect vignette = new()
		{
			Texture = new GradientTexture2D
			{
				Gradient = new Gradient { Offsets = new[] { 0f, 0.55f, 1f }, Colors = new[] { new Color(0f, 0f, 0f, 0f), new Color(0.05f, 0.04f, 0.1f, 0.05f), new Color(0.05f, 0.04f, 0.1f, 0.42f) } },
				Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(1.05f, 0.5f), Width = 256, Height = 144
			},
			Size = view, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore
		};
		amb.AddChild(vignette);

		Fx.Animate(amb, 900f, t =>
		{
			float time = t * 900f;
			float cam = 0.5f - 0.5f * Mathf.Cos(time * Mathf.Tau / 26f);
			foreach ((Sprite2D sp, float sc) in bg)
			{
				sp.Scale = Vector2.One * sc * (1f + 0.025f * cam);
				sp.Position = view / 2f + new Vector2(-W * 0.008f * cam, -H * 0.006f * cam);
			}
			foreach ((Sprite2D c, float y, float speed, float ph) in clouds)
			{
				float span = W * 1.8f;
				c.Position = new Vector2(Mathf.PosMod(ph * span + speed * time, span) - W * 0.4f, y + H * 0.02f * Mathf.Sin(time * 0.1f + ph * 5f));
			}
			foreach ((Polygon2D r, float a, float ph) in rays)
			{
				r.Rotation = a + 0.015f * Mathf.Sin(time * 0.21f + ph);
				r.Modulate = new Color(1f, 1f, 1f, 0.55f + 0.45f * Mathf.Sin(time * 0.35f + ph));
			}
			bloom.Modulate = new Color(1f, 0.93f, 0.85f, 0.15f + 0.05f * Mathf.Sin(time * 0.6f));
			foreach ((Sprite2D m, Vector2 at, float speed, float ph, float sz) in motes)
			{
				float y = Mathf.PosMod(at.Y - speed * time, H * 1.1f) - H * 0.05f;
				m.Position = new Vector2(at.X + W * 0.012f * Mathf.Sin(time * 0.3f + ph), y);
				float tw = 0.5f + 0.5f * Mathf.Sin(time * 1.7f + ph * 3f);
				m.Modulate = new Color(1f, 0.97f, 0.9f, 0.2f + 0.45f * tw * tw);
			}
			foreach ((Node2D n, Polygon2D p, Vector2 start, float speed, float ph, float spin) in petals)
			{
				float u = Mathf.PosMod(ph + time * speed / 1.6f, 1f);          // 一片花瓣 1/speed 秒左右飘过一轮
				Vector2 path = new(W * 0.9f * u, H * 1.25f * u);
				n.Position = start + path + new Vector2(W * 0.03f * Mathf.Sin(time * 0.9f + ph * 7f), 0f);
				n.Rotation = time * 0.6f * spin + ph * 6f;
				n.Scale = new Vector2(Mathf.Cos(time * spin + ph * 4f), 1f);    // 翻转
				p.Modulate = new Color(1f, 1f, 1f, Mathf.Sin(u * Mathf.Pi));
			}
			vignette.Modulate = new Color(1f, 1f, 1f, 0.9f + 0.1f * Mathf.Sin(time * 0.4f));
		});
	}

	/// <summary>
	/// 蓝蝶（P3 菲莱蒙的蝴蝶，v0.14.33）：从左下角飞入 → 屏幕中间绕一圈 → 右上角飞走，fly 秒。
	/// 轨迹：一条过所有控制点的向心 Catmull-Rom 样条，按弧长取点（速度只取决于路程，不会因控制点疏密忽快忽慢），
	///   速度曲线 = 起飞柔和加速、绕圈略慢、离开时再加速；朝向逐帧平滑追随速度方向（不会甩头）。
	/// 扇翅：左右翅分开以身体为轴横向缩放（cos 节奏，约 6 次/秒，每隔一会儿张开滑翔片刻，过渡都是平滑插值）；身体随扇翅轻微起伏。
	/// 贴图：ui/butterfly_wing_l / _wing_r / _body（同画布、身体在画布中心、头朝上；美术会话出图）；没有时用多边形画一只。
	/// 身后：一串蓝色亮粉，外加一团跟随的柔光。
	/// </summary>
	private static void Butterfly(Node2D parent, Vector2 view, float fly)
	{
		float W = view.X, H = view.Y, dot = Fx.SoftDot.GetWidth();
		// ── 轨迹 ──
		Vector2[] ctrl =
		{
			new(-0.08f, 1.10f), new(0.10f, 0.86f), new(0.27f, 0.66f), new(0.40f, 0.56f), new(0.52f, 0.58f), new(0.62f, 0.49f),
			new(0.62f, 0.36f), new(0.53f, 0.28f), new(0.42f, 0.32f), new(0.39f, 0.44f), new(0.47f, 0.52f), new(0.60f, 0.46f),
			new(0.75f, 0.30f), new(0.90f, 0.13f), new(1.10f, -0.10f),
		};
		var pts = new List<Vector2>();
		for (int i = 0; i < ctrl.Length; i++)
		{
			pts.Add(new Vector2(ctrl[i].X * W, ctrl[i].Y * H));
		}
		var samples = new List<Vector2>();
		for (int i = 0; i < pts.Count - 1; i++)
		{
			Vector2 p0 = pts[Mathf.Max(i - 1, 0)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Mathf.Min(i + 2, pts.Count - 1)];
			for (int k = 0; k < 24; k++)
			{
				samples.Add(CentripetalCatmull(p0, p1, p2, p3, k / 24f));
			}
		}
		samples.Add(pts[^1]);
		var cum = new float[samples.Count];
		for (int i = 1; i < samples.Count; i++)
		{
			cum[i] = cum[i - 1] + samples[i].DistanceTo(samples[i - 1]);
		}
		float total = cum[^1];
		Vector2 At(float d)
		{
			d = Mathf.Clamp(d, 0f, total);
			int lo = 0, hi = cum.Length - 1;
			while (hi - lo > 1)
			{
				int mid = (lo + hi) / 2;
				if (cum[mid] < d) lo = mid; else hi = mid;
			}
			float seg = cum[hi] - cum[lo];
			return samples[lo].Lerp(samples[hi], seg > 0f ? (d - cum[lo]) / seg : 0f);
		}
		// 速度：起飞 0→1 柔和加速（前 18%），绕圈段（路程 38%–72%）降到 0.8，离开时回到 1.15；对时间积分后归一到总路程
		static float Speed(float u) => Mathf.Lerp(0.35f, 1f, Ease.InOutSine(Mathf.Clamp(u / 0.18f, 0f, 1f)))
			* (1f - 0.2f * Mathf.Sin(Mathf.Pi * Mathf.Clamp((u - 0.36f) / 0.38f, 0f, 1f)))
			* (1f + 0.15f * Ease.InOutSine(Mathf.Clamp((u - 0.75f) / 0.2f, 0f, 1f)));
		const int N = 400;
		var dist = new float[N + 1];
		for (int i = 1; i <= N; i++)
		{
			dist[i] = dist[i - 1] + Speed((i - 0.5f) / N);
		}
		float Dist(float u)
		{
			float f = Mathf.Clamp(u, 0f, 1f) * N;
			int i = Mathf.Min((int)f, N - 1);
			return Mathf.Lerp(dist[i], dist[i + 1], f - i) / dist[N] * total;
		}

		// ── 蝴蝶 ──
		Node2D fly1 = new() { Name = "P3Butterfly" };
		parent.AddChild(fly1);
		float size = H * 0.085f;
		Sprite2D aura = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Scale = Vector2.One * size * 3.2f / dot, Modulate = new Color(0.35f, 0.7f, 1f, 0.5f) };
		fly1.AddChild(aura);
		Node2D wingL = new(), wingR = new(), body = new();
		fly1.AddChild(wingL);
		fly1.AddChild(wingR);
		fly1.AddChild(body);
		Texture2D? tl = UiTex("butterfly_wing_l"), tr = UiTex("butterfly_wing_r"), tb = UiTex("butterfly_body");
		if (tl != null && tr != null)
		{
			float sc = size * 2f / tl.GetWidth();
			wingL.AddChild(new Sprite2D { Texture = tl, Scale = Vector2.One * sc });
			wingR.AddChild(new Sprite2D { Texture = tr, Scale = Vector2.One * sc });
			if (tb != null)
			{
				body.AddChild(new Sprite2D { Texture = tb, Scale = Vector2.One * sc });
			}
		}
		else
		{
			foreach ((Node2D w, float sgn) in new[] { (wingL, -1f), (wingR, 1f) })
			{
				Vector2[] fore = { new(0f, -size * 0.05f), new(sgn * size * 0.5f, -size * 0.75f), new(sgn * size * 1.0f, -size * 0.6f), new(sgn * size * 0.85f, -size * 0.1f), new(sgn * size * 0.2f, size * 0.05f) };
				Vector2[] hind = { new(0f, size * 0.05f), new(sgn * size * 0.7f, size * 0.15f), new(sgn * size * 0.75f, size * 0.55f), new(sgn * size * 0.3f, size * 0.75f), new(sgn * size * 0.05f, size * 0.35f) };
				foreach (Vector2[] shape in new[] { fore, hind })
				{
					Polygon2D g = Fx.Poly(w, shape, new Color(0.35f, 0.75f, 1f, 0.55f), additive: true);
					g.Scale = Vector2.One * 1.12f;
					Fx.Poly(w, shape, new Color(0.12f, 0.45f, 1f, 0.85f));
					Fx.Line(w, shape, new Color(0.8f, 0.95f, 1f, 0.9f), 2f, closed: true);
				}
			}
			Fx.Poly(body, new[] { new Vector2(0f, -size * 0.35f), new Vector2(size * 0.05f, 0f), new Vector2(0f, size * 0.4f), new Vector2(-size * 0.05f, 0f) }, new Color(0.1f, 0.15f, 0.35f));
		}
		Node2D trail = new();
		parent.AddChild(trail);
		parent.MoveChild(trail, fly1.GetIndex());

		float prevAngle = 0f;
		bool first = true;
		float lastDust = 0f;
		Fx.Animate(fly1, fly, t =>
		{
			float time = t * fly;
			float d = Dist(t);
			Vector2 p = At(d), ahead = At(d + total * 0.01f);
			// 朝向：头朝速度方向（贴图头朝上），逐帧平滑追随
			float target = (ahead - p).Angle() + Mathf.Pi / 2f;
			prevAngle = first ? target : Mathf.LerpAngle(prevAngle, target, 0.12f);
			first = false;
			// 扇翅：约 6 次/秒；每 ~1.6s 滑翔一次（翅膀张开、扇动幅度平滑降低）
			float glide = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(time * Mathf.Tau / 1.6f), 6f);
			float flap = Mathf.Cos(time * Mathf.Tau * 6f);
			float open = Mathf.Lerp(0.18f + 0.82f * (0.5f + 0.5f * flap), 0.92f, glide);
			wingL.Scale = new Vector2(open, 1f);
			wingR.Scale = new Vector2(open, 1f);
			float bob = (1f - glide) * size * 0.06f * Mathf.Sin(time * Mathf.Tau * 6f + 1.2f);
			fly1.Position = p + new Vector2(0f, bob);
			fly1.Rotation = prevAngle + 0.08f * Mathf.Sin(time * 2.3f);
			float inOut = Mathf.Clamp(time / 0.4f, 0f, 1f) * Mathf.Clamp((fly - time) / 0.5f, 0f, 1f);
			fly1.Modulate = new Color(1f, 1f, 1f, inOut);
			aura.Modulate = new Color(0.35f, 0.7f, 1f, (0.4f + 0.15f * flap) * inOut);
			// 亮粉：每 0.03s 落一粒，向下飘、渐隐
			if (time - lastDust > 0.03f)
			{
				lastDust = time;
				Sprite2D g = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Position = p + Fx.RandDir() * size * 0.15f, Scale = Vector2.One * size * Fx.Rand(0.08f, 0.16f) / dot, Modulate = new Color(0.55f, 0.85f, 1f, 0.8f) };
				trail.AddChild(g);
				Vector2 g0 = g.Position, drift = new(Fx.Rand(-8f, 8f), Fx.Rand(14f, 34f));
				Fx.Animate(g, 0.9f, u =>
				{
					g.Position = g0 + drift * Ease.OutCubic(u);
					g.Modulate = new Color(0.55f, 0.85f, 1f, 0.8f * (1f - u) * (0.6f + 0.4f * Mathf.Abs(Mathf.Cos(u * 9f))));
				});
				Fx.FreeAfter(g, 0.95f);
			}
		});
		Fx.FreeAfter(fly1, fly + 0.05f);
		Fx.FreeAfter(trail, fly + 1.0f);
	}

	/// <summary>向心 Catmull-Rom（alpha = 0.5）：过控制点、不打结、不过冲。</summary>
	private static Vector2 CentripetalCatmull(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
	{
		static float Knot(float ti, Vector2 a, Vector2 b) => ti + Mathf.Max(Mathf.Sqrt(a.DistanceTo(b)), 1e-3f);
		float t0 = 0f, t1 = Knot(t0, p0, p1), t2 = Knot(t1, p1, p2), t3 = Knot(t2, p2, p3);
		float tt = Mathf.Lerp(t1, t2, t);
		Vector2 a1 = (t1 - tt) / (t1 - t0) * p0 + (tt - t0) / (t1 - t0) * p1;
		Vector2 a2 = (t2 - tt) / (t2 - t1) * p1 + (tt - t1) / (t2 - t1) * p2;
		Vector2 a3 = (t3 - tt) / (t3 - t2) * p2 + (tt - t2) / (t3 - t2) * p3;
		Vector2 b1 = (t2 - tt) / (t2 - t0) * a1 + (tt - t0) / (t2 - t0) * a2;
		Vector2 b2 = (t3 - tt) / (t3 - t1) * a2 + (tt - t1) / (t3 - t1) * a3;
		return (t2 - tt) / (t2 - t1) * b1 + (tt - t1) / (t2 - t1) * b2;
	}

	/// <summary>闭眼全黑后：黑底上 MEMENTO MORI / ……于是，早晨来临。淡入（0.9s）、停留（2.2s）、淡出（0.9s），然后 then()。</summary>
	private static void Memento(Node2D root, Vector2 view, System.Action then)
	{
		Node2D coda = new() { Position = new Vector2(view.X / 2f, view.Y * 0.5f), Modulate = new Color(1f, 1f, 1f, 0f) };
		root.AddChild(coda);
		Label codaText = Typo.Text(coda, "M E M E N T O   M O R I", Typo.Latin, 30, new Color(0.8f, 0.9f, 1f), new Vector2(-view.X / 2f, -20f), HorizontalAlignment.Center);
		codaText.Size = new Vector2(view.X, codaText.Size.Y);
		Label codaSub = Typo.Text(coda, Zh ? "……于是，早晨来临。" : "...and the morning came.", Typo.Cjk, 22, new Color(0.65f, 0.75f, 0.9f), new Vector2(-view.X / 2f, 28f), HorizontalAlignment.Center);
		codaSub.Size = new Vector2(view.X, codaSub.Size.Y);
		const float fin = 0.9f, stay = 2.2f, fout = 0.9f;
		Fx.Animate(coda, fin + stay + fout, t =>
		{
			float time = t * (fin + stay + fout);
			float a = time < fin ? Ease.OutCubic(time / fin) : time < fin + stay ? 1f : 1f - Ease.InOutSine((time - fin - stay) / fout);
			coda.Modulate = new Color(1f, 1f, 1f, a);
			coda.Position = new Vector2(view.X / 2f, view.Y * 0.5f + 10f * (1f - Mathf.Min(1f, time / fin)));
		});
		Fx.After(coda, fin + stay + fout + 0.3, "seal memento done", () => ModEntry.Safe("seal finish", then));
	}

	/// <summary>结算画面打开后调用：闭眼留下的黑幕淡出，露出原版结算 UI。</summary>
	public static void RevealGameOver()
	{
		if (Engine.GetMainLoop() is not SceneTree tree || tree.Root.GetNodeOrNull<CanvasLayer>("P3GreatSeal") is not CanvasLayer top)
		{
			return;
		}
		if (top.GetNodeOrNull<Node2D>("P3SealEyes") is Node2D eyes)
		{
			Fx.Animate(eyes, 0.8f, t => eyes.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InOutSine(t)));
		}
		top.GetTree().CreateTimer(0.85, processAlways: true, processInPhysics: false, ignoreTimeScale: true).Timeout += () =>
		{
			if (GodotObject.IsInstanceValid(top))
			{
				top.QueueFree();
			}
		};
	}
}
