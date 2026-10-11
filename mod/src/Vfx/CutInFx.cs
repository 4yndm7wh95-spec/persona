using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// P3R 技能切入：召唤 Persona 的牌打出时 ——
/// 1. 背景压暗，横贯画面的"眼神特写"横幅从中线张开（青蓝双色调，缓慢横移，气泡上浮）；左上角技能名标签滑入，右侧"咔"字砸入。
///    横幅在人物身后的特效层：结城理站在横幅前面（和原版一样）。
/// 2. 召唤器扣扳机那一帧（evoker_fire，或超时兜底）：横幅从人物头部为中心放射状裂开 → 碎成大块玻璃向外炸飞、翻转、放大（飞向镜头），
///    碎片本身就是横幅贴图的切块（UV 对应），边缘带青色描边；同时 Persona 从碎裂处现身（由 onFire 回调完成）。
/// 素材：p3makoto/ui/cutin_eyes.png（tools/ui/build_ui_assets.py 从 art/v6/portrait_a.png 生成）。
/// </summary>
public static class CutInFx
{
	private const string EyesPath = "res://p3makoto/ui/cutin_eyes.png";
	private const string KaPath = "res://p3makoto/ui/cutin_ka.png";
	private static readonly Vector2 EyeTex = new(690f, 215f);          // 贴图里眼睛的位置（build_ui_assets.EYE）
	private const float TexW = 1600f, TexH = 440f;

	private sealed class State
	{
		public Node2D Band = null!;
		public Polygon2D Face = null!;
		public ColorRect Dim = null!;
		public Node2D Ui = null!;
		public Node2D Decor = null!;
		public Texture2D Eyes = null!;
		public Vector2 Screen;
		public float Cy, HalfH, Pan;
		public ulong Start;
		public Action<float>? OnFire;
		public bool Fired;
	}

	private static readonly Dictionary<ulong, State> Live = new();

	/// <summary>"瞬间"速度下不演（直接回调）。</summary>
	public static bool Enabled => SaveManager.Instance?.PrefsSave.FastMode != FastModeType.Instant;

	private static bool Fast => SaveManager.Instance?.PrefsSave.FastMode == FastModeType.Fast;

	/// <summary>开始切入。onFire(从开始到扣扳机经过的秒数) 在玻璃碎裂那一刻调用（召唤 Persona）。</summary>
	public static void Begin(NCreature caster, string title, string persona, Action<float>? onFire)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null || ResourceLoader.Load(EyesPath) is not Texture2D eyes)
		{
			onFire?.Invoke(0f);
			return;
		}
		ulong id = caster.GetInstanceId();
		if (Live.Remove(id, out State? old))
		{
			Clear(old);
		}
		Vector2 size = back.GetViewportRect().Size;
		var st = new State { Eyes = eyes, Screen = size, Cy = size.Y * 0.4f, HalfH = size.Y * 0.19f, Start = Time.GetTicksMsec(), OnFire = onFire };
		Live[id] = st;

		// 压暗背景（在人物身后）
		st.Dim = new ColorRect { Color = new Color(0.01f, 0.03f, 0.1f, 0f), Size = size * 1.2f, Position = -size * 0.1f, MouseFilter = Control.MouseFilterEnum.Ignore };
		back.AddChild(st.Dim);
		st.Dim.GlobalPosition = -size * 0.1f;

		// P3R 菜单式背景拼贴（都在横幅后面）：左下白色斜切大色块 + 上面旋转的超大 Persona 名字水印、右上深蓝色块、
		// 右侧巨大的"水之圆"（焦散光纹）+ 两道细圆弧。依次从画面外滑入 / 张开。
		Node2D decor = Fx.Root(back, Vector2.Zero, "P3CutInDecor");
		st.Decor = decor;
		Node2D slabs = new();
		decor.AddChild(slabs);
		P3Ui.Slab(slabs, -200f, size.X * 0.4f, size.Y * 0.64f, size.Y + 60f, 190f, new Color(0.93f, 0.96f, 1f, 0.9f));
		P3Ui.Slab(slabs, -200f, size.X * 0.36f, size.Y * 0.64f, size.Y * 0.64f + 10f, 190f, new Color(0.9f, 0.1f, 0.25f, 0.9f));
		Node2D word = P3Ui.GiantWord(slabs, persona, new Vector2(size.X * 0.03f, size.Y * 1.08f), (int)(size.Y * 0.3f), -1.0f, new Color(0.02f, 0.03f, 0.09f, 0.88f));
		Node2D topSlab = new();
		decor.AddChild(topSlab);
		P3Ui.Slab(topSlab, size.X * 0.5f, size.X + 300f, -60f, size.Y * 0.15f, -140f, new Color(0.02f, 0.06f, 0.24f, 0.92f));
		Vector2 discAt = new(size.X * 0.84f, st.Cy + size.Y * 0.06f);
		float discR = size.Y * 0.62f;
		ColorRect disc = P3Ui.WaterDisc(decor, discAt, discR);
		Node2D arcs = new() { Position = discAt };
		decor.AddChild(arcs);
		Line2D arc1 = P3Ui.Arc(arcs, Vector2.Zero, discR * 1.07f, Mathf.Pi * 0.55f, Mathf.Pi * 1.45f, new Color(Pal.Cyan, 0.55f), 3f);
		Line2D arc2 = P3Ui.Arc(arcs, Vector2.Zero, discR * 1.16f, Mathf.Pi * 0.8f, Mathf.Pi * 1.25f, new Color(1f, 1f, 1f, 0.45f), 2f);
		Fx.Animate(decor, 3.0, t =>
		{
			float time = t * 3f;
			float a = Ease.OutExpo(Mathf.Clamp(time / 0.2f, 0f, 1f));
			float b = Ease.OutExpo(Mathf.Clamp((time - 0.04f) / 0.2f, 0f, 1f));
			slabs.Position = new Vector2(-size.X * 0.5f * (1f - a), 0f);
			topSlab.Position = new Vector2(size.X * 0.5f * (1f - b), 0f);
			word.Position = new Vector2(size.X * 0.03f + 30f * time, size.Y * 1.08f);
			Fx.SetParam(disc, "reveal", Ease.OutExpo(Mathf.Clamp((time - 0.02f) / 0.24f, 0f, 1f)));
			arc1.Rotation = 0.25f * time;
			arc2.Rotation = -0.18f * time;
			arc1.Modulate = arc2.Modulate = new Color(1f, 1f, 1f, b);
		});

		// 横幅：两条曲边（越往两端越高，像镜头的弧面），贴图横向缓慢平移
		st.Band = Fx.Root(back, new Vector2(0f, st.Cy), "P3CutInBand");
		st.Face = new Polygon2D { Texture = eyes, Polygon = BandPoly(st, 0f), UV = BandUv(st), TextureFilter = CanvasItem.TextureFilterEnum.Linear };
		st.Band.AddChild(st.Face);
		Polygon2D frameTop = Fx.Poly(st.Band, Strip(st, -1f, 16f), new Color(0.02f, 0.05f, 0.16f, 0.96f));
		Polygon2D frameBot = Fx.Poly(st.Band, Strip(st, 1f, 16f), new Color(0.02f, 0.05f, 0.16f, 0.96f));
		Fx.Line(st.Band, Edge(st, -1f), new Color(Pal.Cyan, 0.9f), 3f);
		Fx.Line(st.Band, Edge(st, 1f), new Color(Pal.Cyan, 0.9f), 3f);
		Bubbles(st);
		// 眼中蓝光（实时）：柔光 + 十字星 + 横向镜头光，跟着贴图平移，呼吸闪烁
		Node2D eye = new();
		st.Band.AddChild(eye);
		Sprite2D bloom = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(Pal.Cyan, 0.85f), Scale = Vector2.One * 2.2f };
		eye.AddChild(bloom);
		Polygon2D star = Fx.Poly(eye, Fx.Star4(70f, 0.06f), Pal.White, additive: true);
		Sprite2D streak = new() { Texture = Fx.SoftDot, Material = Fx.Additive, Modulate = new Color(0.6f, 0.92f, 1f, 0.8f), Scale = new Vector2(22f, 0.22f) };
		eye.AddChild(streak);
		// 斜向扫过的一道光
		Polygon2D sweep = Fx.Poly(st.Band, new[] { new Vector2(-90f, -st.HalfH * 1.3f), new Vector2(0f, -st.HalfH * 1.3f), new Vector2(-160f, st.HalfH * 1.3f), new Vector2(-250f, st.HalfH * 1.3f) },
			new Color(0.6f, 0.9f, 1f, 0.35f), additive: true);
		Fx.Animate(eye, 3.0, t =>
		{
			float time = t * 3f;
			float u = (EyeTex.X + st.Pan) / (TexW * 0.92f);
			eye.Position = new Vector2(X(st, u), (EyeTex.Y / TexH * 2f - 1f) * Half(st, u));
			float pulse = 0.8f + 0.2f * Mathf.Sin(time * 9f);
			float flare = Ease.OutExpo(Mathf.Clamp((time - 0.08f) / 0.15f, 0f, 1f));
			bloom.Scale = Vector2.One * 2.2f * pulse * flare;
			star.Scale = Vector2.One * flare * (0.7f + 0.3f * Mathf.Sin(time * 13f));
			star.Rotation = time * 0.8f;
			streak.Scale = new Vector2(22f * flare, 0.22f * pulse);
			sweep.Position = new Vector2(Mathf.Lerp(-200f, st.Screen.X + 400f, Mathf.Clamp(time / 0.5f, 0f, 1f)), 0f);
		});
		st.Band.Scale = new Vector2(1f, 0.02f);

		// 左上技能名 + 右侧"咔"
		st.Ui = Fx.Root(front, Vector2.Zero, "P3CutInUi");
		// 技能名：P3R 菜单"选中项"样式（白色平行四边形 + 错开黑块 + 红色粗斜体），下面一行青色 PERSONA ▸ 名字
		Node2D tag = P3Ui.RedTag(st.Ui, title, new Vector2(-420f, size.Y * 0.08f), 44);
		Node2D sub = new() { Position = new Vector2(30f, 80f), Skew = -0.25f };
		tag.AddChild(sub);
		Typo.Text(sub, "PERSONA  \u25B8  " + persona, Typo.Latin, 26, Pal.Cyan, Vector2.Zero, outline: 4, outlineColor: new Color(0.02f, 0.04f, 0.12f));
		// "咔"：离线生成的空心双描边设计字（build_ui_assets.cutin_ka），不是系统字体
		Node2D ka = new() { Position = new Vector2(size.X * 0.8f, st.Cy - st.HalfH * 0.1f), Rotation = -0.06f };
		st.Ui.AddChild(ka);
		if (ResourceLoader.Load(KaPath) is Texture2D kaTex)
		{
			ka.AddChild(new Sprite2D { Texture = kaTex, Scale = Vector2.One * (st.HalfH * 2.1f / kaTex.GetHeight()) });
		}
		ka.Scale = Vector2.One * 2.2f;
		ka.Modulate = new Color(1f, 1f, 1f, 0f);

		float open = 0.12f;
		Fx.Animate(st.Band, 3.0, t =>
		{
			float time = t * 3f;
			float e = Ease.OutExpo(Mathf.Clamp(time / open, 0f, 1f));
			st.Band.Scale = new Vector2(1f, Mathf.Max(e, 0.02f));
			st.Pan = -70f * Mathf.Min(time, 1f);
			st.Face.UV = BandUv(st);                          // 横向缓慢平移（碎片用同一个映射 ToUv，严丝合缝）
			st.Dim.Color = new Color(0.01f, 0.03f, 0.1f, 0.6f * e);
			tag.Position = new Vector2(Mathf.Lerp(-420f, 70f, Ease.OutExpo(Mathf.Clamp(time / 0.16f, 0f, 1f))), size.Y * 0.08f);
			float k = Mathf.Clamp((time - 0.06f) / 0.1f, 0f, 1f);
			ka.Scale = Vector2.One * Mathf.Lerp(2.2f, 1f, Ease.OutBack(k));
			ka.Modulate = new Color(1f, 1f, 1f, k);
		});
		P3Fx.Flash(back, new Vector2(size.X * 0.5f, st.Cy), Pal.Cyan, size.X * 0.5f, 0.18f);
		// 兜底：没等到扣扳机就自己碎
		Fx.After(st.Band, Fast ? 0.3 : 0.65, "cut-in timeout", () => Fire(caster));
	}

	/// <summary>扣扳机：横幅碎裂、Persona 现身。没有进行中的切入时什么也不做。</summary>
	public static void Fire(NCreature caster)
	{
		if (!Live.Remove(caster.GetInstanceId(), out State? st) || st.Fired || !GodotObject.IsInstanceValid(st.Band))
		{
			return;
		}
		st.Fired = true;
		float elapsed = (Time.GetTicksMsec() - st.Start) / 1000f;
		Control? back = Makoto.FxLayer(back: true);
		if (back != null)
		{
			Shatter(back, st, GodotObject.IsInstanceValid(caster) ? Makoto.Head(caster) : new Vector2(st.Screen.X * 0.3f, st.Cy));
		}
		Clear(st, keepDim: true);
		st.OnFire?.Invoke(elapsed);
	}

	private static void Clear(State st, bool keepDim = false)
	{
		if (GodotObject.IsInstanceValid(st.Band)) st.Band.QueueFree();
		if (GodotObject.IsInstanceValid(st.Decor))
		{
			Node2D decor = st.Decor;
			Fx.Animate(decor, 0.35, t =>
			{
				decor.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
				decor.Scale = Vector2.One * (1f + 0.06f * Ease.OutCubic(t));
			});
			Fx.FreeAfter(decor, 0.4);
		}
		if (GodotObject.IsInstanceValid(st.Ui))
		{
			Node2D ui = st.Ui;
			Fx.Animate(ui, 0.15, t => ui.Modulate = new Color(1f, 1f, 1f, 1f - t));
			Fx.FreeAfter(ui, 0.2);
		}
		if (GodotObject.IsInstanceValid(st.Dim))
		{
			ColorRect dim = st.Dim;
			float a0 = dim.Color.A;
			Fx.Animate(dim, keepDim ? 0.6 : 0.15, t => dim.Color = new Color(0.01f, 0.03f, 0.1f, a0 * (1f - Ease.InCubic(t))));
			Fx.FreeAfter(dim, keepDim ? 0.65 : 0.2);
		}
	}

	// ───────── 横幅几何 ─────────
	private const int Seg = 28;

	private static float Half(State st, float u) => st.HalfH * (1f + 0.18f * (2f * u - 1f) * (2f * u - 1f));

	private static float X(State st, float u) => -60f + u * (st.Screen.X + 120f);

	private static Vector2[] BandPoly(State st, float _)
	{
		var p = new Vector2[(Seg + 1) * 2];
		for (int i = 0; i <= Seg; i++)
		{
			float u = i / (float)Seg;
			p[i] = new Vector2(X(st, u), -Half(st, u));
			p[(Seg + 1) * 2 - 1 - i] = new Vector2(X(st, u), Half(st, u));
		}
		return p;
	}

	private static Vector2[] BandUv(State st)
	{
		var uv = new Vector2[(Seg + 1) * 2];
		for (int i = 0; i <= Seg; i++)
		{
			float u = i / (float)Seg;
			uv[i] = new Vector2(u * TexW * 0.92f - st.Pan, 0f);
			uv[(Seg + 1) * 2 - 1 - i] = new Vector2(u * TexW * 0.92f - st.Pan, TexH);
		}
		return uv;
	}

	/// <summary>屏幕点（相对横幅中线）→ 贴图坐标（和 BandUv 同一映射，含平移）。</summary>
	private static Vector2 ToUv(State st, Vector2 local)
	{
		float u = Mathf.Clamp((local.X + 60f) / (st.Screen.X + 120f), 0f, 1f);
		float h = Half(st, u);
		return new Vector2(u * TexW * 0.92f - st.Pan, (local.Y + h) / (2f * h) * TexH);
	}

	private static Vector2[] Edge(State st, float side)
	{
		var p = new Vector2[Seg + 1];
		for (int i = 0; i <= Seg; i++)
		{
			float u = i / (float)Seg;
			p[i] = new Vector2(X(st, u), side * Half(st, u));
		}
		return p;
	}

	private static Vector2[] Strip(State st, float side, float thick)
	{
		var p = new Vector2[(Seg + 1) * 2];
		for (int i = 0; i <= Seg; i++)
		{
			float u = i / (float)Seg;
			float h = Half(st, u);
			p[i] = new Vector2(X(st, u), side * h);
			p[(Seg + 1) * 2 - 1 - i] = new Vector2(X(st, u), side * (h + thick));
		}
		return p;
	}

	private static void Bubbles(State st)
	{
		for (int i = 0; i < 14; i++)
		{
			float r = Fx.Rand(6f, 22f);
			Vector2 p0 = new(Fx.Rand(0f, st.Screen.X), Fx.Rand(-st.HalfH * 0.8f, st.HalfH * 0.9f));
			Line2D ring = Fx.Line(st.Band, Fx.Circle(r, 18), new Color(Pal.Ice, Fx.Rand(0.35f, 0.75f)), 2.5f, closed: true);
			ring.Position = p0;
			float speed = Fx.Rand(30f, 90f), wob = Fx.Rand(0f, 6f);
			Fx.Animate(ring, 3.0, t =>
			{
				float time = t * 3f;
				ring.Position = p0 + new Vector2(8f * Mathf.Sin(time * 3f + wob), -speed * time);
			});
		}
	}

	/// <summary>同一个格点每次取同一个随机值（相邻碎片共用顶点，碎片之间严丝合缝）。</summary>
	private static float Hash(int k, float a, float b)
	{
		float v = Mathf.Sin(k * 12.9898f) * 43758.5453f;
		return a + (b - a) * (v - Mathf.Floor(v));
	}

	// ───────── 碎裂 ─────────
	private static void Shatter(Control back, State st, Vector2 impact)
	{
		Vector2 origin = new(0f, st.Cy);
		Vector2 hit = impact - origin;                                // 横幅局部坐标里的冲击点
		Vector2[] band = BandPoly(st, 0f);
		float reach = st.Screen.Length();
		float[] rings = { 0f, 70f, 160f, 290f, 460f, 700f, 1000f, 1400f, reach + 200f };
		const int slices = 16;
		float a0 = Fx.Rand(0f, Mathf.Tau);
		var jitterA = new float[slices];
		for (int j = 0; j < slices; j++) jitterA[j] = a0 + Mathf.Tau * j / slices + Fx.Rand(-0.12f, 0.12f);
		Vector2 P(int ring, int slice)
		{
			float a = jitterA[(slice % slices + slices) % slices] + (ring % 2) * 0.09f;
			float r = rings[ring] * (ring == 0 ? 0f : Hash(ring * 131 + slice, 0.85f, 1.15f));
			return hit + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
		}
		var pieces = new List<Vector2[]>();
		for (int r = 0; r < rings.Length - 1; r++)
		{
			for (int j = 0; j < slices; j++)
			{
				Vector2[] cell = r == 0
					? new[] { hit, P(1, j), P(1, j + 1) }
					: new[] { P(r, j), P(r + 1, j), P(r + 1, j + 1), P(r, j + 1) };
				foreach (Vector2[] piece in Geometry2D.IntersectPolygons(cell, band))
				{
					pieces.Add(piece);
				}
			}
		}

		// 0.05s 裂纹定格（白色裂纹 + 白闪），然后碎片炸开
		Node2D root = Fx.Root(back, origin, "P3CutInShatter");
		Node2D cracks = new();
		root.AddChild(cracks);
		Fx.Poly(cracks, band, new Color(1f, 1f, 1f, 0.55f), additive: true);
		foreach (Vector2[] piece in pieces)
		{
			Fx.Line(cracks, piece, new Color(1f, 1f, 1f, 0.95f), 2.5f, closed: true);
		}
		Fx.Animate(cracks, 0.1, t => cracks.Modulate = new Color(1f, 1f, 1f, 1f - t));
		Fx.FreeAfter(cracks, 0.12);

		const float total = 0.85f;
		foreach (Vector2[] piece in pieces)
		{
			Vector2 c = Vector2.Zero;
			foreach (Vector2 v in piece) c += v;
			c /= piece.Length;
			var local = new Vector2[piece.Length];
			var uv = new Vector2[piece.Length];
			for (int i = 0; i < piece.Length; i++)
			{
				local[i] = piece[i] - c;
				uv[i] = ToUv(st, piece[i]);
			}
			Node2D shard = new() { Position = c };
			root.AddChild(shard);
			shard.AddChild(new Polygon2D { Texture = st.Eyes, Polygon = local, UV = uv, TextureFilter = CanvasItem.TextureFilterEnum.Linear });
			Line2D rim = Fx.Line(shard, local, new Color(Pal.Cyan, 0.85f), 2.5f, closed: true);
			Polygon2D glint = Fx.Poly(shard, local, new Color(0.7f, 0.95f, 1f, 0.5f), additive: true);
			Vector2 dir = (c - hit).Normalized();
			float dist = (c - hit).Length();
			Vector2 vel = dir * (420f + dist * 1.1f) * Fx.Rand(0.75f, 1.25f) + new Vector2(0f, Fx.Rand(-160f, 40f));
			float spin = Fx.Rand(-5f, 5f);
			float grow = Fx.Rand(0f, 1f) < 0.4f ? Fx.Rand(0.3f, 0.9f) : Fx.Rand(-0.3f, 0.1f);   // 一部分飞向镜头（放大）
			float delay = 0.04f + dist / 6000f;
			Vector2 p0 = c;
			Fx.Animate(shard, total, t =>
			{
				float time = t * total - delay;
				if (time < 0f) return;
				float tt = time;
				shard.Position = p0 + vel * tt + new Vector2(0f, 500f * tt * tt);
				shard.Rotation = spin * tt;
				shard.Scale = Vector2.One * (1f + grow * Ease.OutCubic(Mathf.Clamp(tt / 0.6f, 0f, 1f)));
				glint.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(1f - tt / 0.15f, 0f, 1f) + 0.25f * Mathf.Abs(Mathf.Sin(tt * 18f + spin)));
				shard.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((tt - 0.3f) / 0.45f, 0f, 1f)));
			});
		}
		Fx.FreeAfter(root, total + 0.1);
		P3Fx.Flash(back, impact, Pal.Ice, st.Screen.X * 0.45f, 0.2f);
		P3Fx.GlassShatter(back, impact, 30, 1.4f, Pal.Cyan);
	}
}
