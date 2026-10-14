using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P3Makoto.Vfx;

/// <summary>
/// 总攻击（All-Out Attack）：精英 / Boss 战的最后一击之后、胜利结算之前播放（AllOutPatch 把战斗结束流程推迟 Duration 秒；
/// 播了总攻击就不再播普通胜利演出）。
/// v0.14.9（用户：结束得太快，要看清楚原版）—— 按 research/p3r_video/video1 8.0–21.3s 逐 0.25s 对照，节奏照原版（约 12.3s）：
///   0.00  左上角领航小条「成功了！是总攻击的机会！」；0.25 右下斜切蓝带「总攻击 GO! / CANCEL」滑入，0.95 GO! 按下一闪   ♪ AlloutUI_00 @0.25
///   1.15  浅蓝 / 中蓝 / 深蓝同心大圆从画面左侧扫入铺满，之后全屏蓝、两道细圆弧转过                                   ♪ Btl_Allout_Wipe @1.05
///   2.35  亮青色单色"集结"定格：结城理的蓝色平涂剪影 + 斜向光 + 飘落的青色碎片（原版是全队定格，这里只有结城理）        ♪ Allout_Zoom @2.35（有文件时）
///   3.15  近景：结城理立绘（蓝色调）在右侧缓推，前景蓝色碎片飘过
///   4.15  冲锋：蓝色棋盘格地面高速向前卷动，速度线，远处烟云成形
///   5.40  白闪 → 5.50 乱斗烟云 1.8s：蓝白烟团、四芒星、斩击线、圆形纹章                                            ♪ Extermination @5.45
///   7.30  全屏白 0.9s
///   8.20  从白里淡出：蓝色舞台上敌人（黑影）炸成黑红墨迹 + 一道红色斩线 + 白色三角碎片；结城理剪影从前景滑过、收刀走开；
///         9.00 身后一轮巨大的青色圆盘张开
///   9.60  结束立绘：暗蓝 → 0.25s 亮起（原版 A2→A1），背后 MAJESTIC；10.10「DONE AND DUSTED.」从左滑入；停留            ♪ Kime01 高潮对准亮起
///  11.35  褪成黑白 → 11.60「RESULT」蓝色大斜体从左上滑入 → 11.9 淡出
/// 结束立绘用 P3R 原版贴图（allout_finish / allout_finish_dark / allout_text），缺图时退回 victory_portrait。
/// </summary>
public static class AllOut
{
	public const float Duration = 12.3f;
	private const float PromptAt = 0f, IrisAt = 1.15f, PoseAt = 2.35f, CloseAt = 3.15f, RushAt = 4.15f, FlashAt = 5.4f, BrawlAt = 5.5f, WhiteAt = 7.3f, KillAt = 8.2f, FinishAt = 9.6f;

	private static Shader? _gray;

	private static Shader Gray => _gray ??= new Shader
	{
		Code = @"
shader_type canvas_item;
uniform float gray = 0.0;
void fragment() {
	vec4 c = texture(TEXTURE, UV) * COLOR;
	float l = dot(c.rgb, vec3(0.299, 0.587, 0.114));
	COLOR = vec4(mix(c.rgb, vec3(l), gray), c.a);
}",
	};


	public static void Play(Control front, IReadOnlyList<Vector2> enemies)
	{
		Vector2 size = front.GetViewportRect().Size;
		float W = size.X, H = size.Y;
		Vector2 focus = enemies.Count > 0 ? Avg(enemies) : size * new Vector2(0.68f, 0.5f);
		Node2D root = Fx.Root(front, Vector2.Zero, "P3AllOut");
		root.TopLevel = true;
		root.GlobalPosition = Vector2.Zero;

		Audio.Sfx.Clip("Snd_FX_Btl_AlloutUI_00", 0.9f, PromptAt + 0.25f);
		Audio.Sfx.Clip("Btl_Allout_Wipe", 0.8f, IrisAt - 0.1f);
		Audio.Sfx.Clip("Snd_FX_Btl_Allout_Zoom", 0.75f, PoseAt - 0.7f);              // 推镜声的峰值在 0.7s：对准定格镜头切入
		Audio.Sfx.Clip("Snd_FX_Btl_Allout_Extermination", 0.9f, BrawlAt - 0.05f);        // 完整 4.5s，Tails 在烟云结束（1.9s）后缓出
		Audio.Sfx.Clip("atk_hit_enemy_slash", 0.9f, KillAt + 0.3f);
		Audio.Sfx.Clip("sword_finish", 0.7f, KillAt + 1.05f);
		Audio.Sfx.Clip("Snd_FX_Btl_Allout_Kime01", 1.0f, FinishAt + 0.25f - 1.0f);    // 完整版 Kime01 的高潮在 1.0s：对准立绘亮起

		Prompt(root, W, H);
		Fx.After(root, IrisAt, "allout iris", () => Iris(root, focus, W, H));
		Fx.After(root, PoseAt, "allout pose", () => Pose(root, W, H));
		Fx.After(root, CloseAt, "allout close", () => CloseUp(root, W, H));
		Fx.After(root, RushAt, "allout rush", () => Rush(root, focus, W, H));
		Fx.After(root, FlashAt, "allout flash", () => Flash(root, W, H, 0.25f));
		Fx.After(root, BrawlAt, "allout brawl", () => Brawl(root, focus, W, H));
		Fx.After(root, WhiteAt, "allout white", () => WhiteHold(root, W, H));
		Fx.After(root, KillAt, "allout kill", () => Kill(root, enemies.Count > 0 ? enemies : new List<Vector2> { focus }, focus, W, H));
		Fx.After(root, FinishAt, "allout finish", () => Finish(root, W, H));
		Fx.FreeAfter(root, Duration + 0.1f);
	}

	/// <summary>
	/// 卡通烟云（v0.14.9，用户：原来的柔光点叠成一团团模糊白斑，看不出是什么）：
	/// 一团团实心圆叠出烟云轮廓 —— 先整体画一圈深蓝描边，再铺浅蓝白底色，右下压一层淡蓝阴影，左上点白色高光；
	/// 每个圆按自己的相位"翻滚"（大小起伏），整体像原版那团不断翻涌的乱斗烟尘。
	/// </summary>
	private sealed class Cloud
	{
		public Node2D Root = null!;
		public readonly List<(Polygon2D Line, Node2D Puff, Vector2 Pos, float Ph)> Puffs = new();
	}

	private static Cloud MakeCloud(Node2D parent, Vector2 at, float W, float H, int count, float spreadX, float spreadY)
	{
		Cloud c = new() { Root = new Node2D { Position = at } };
		parent.AddChild(c.Root);
		Node2D lines = new(), body = new();
		c.Root.AddChild(lines);
		c.Root.AddChild(body);
		var items = new List<(Vector2 p, float r)>();
		for (int i = 0; i < count; i++)
		{
			// 越靠中心越大、越密；下沿压平（烟贴地）
			float u = Mathf.Pow(Fx.Rand(0f, 1f), 0.7f), a = Fx.Rand(0f, Mathf.Tau);
			Vector2 p = new(Mathf.Cos(a) * u * spreadX, Mathf.Clamp(Mathf.Sin(a) * u * spreadY, -spreadY, spreadY * 0.7f));
			float r = Mathf.Lerp(H * 0.14f, H * 0.065f, u) * Fx.Rand(0.8f, 1.2f) * (spreadY / (H * 0.17f) * 0.5f + 0.5f);
			items.Add((p, r));
		}
		items.Sort((x, y) => x.p.Y.CompareTo(y.p.Y));   // 上面的先画，下面的压在前面
		foreach (var (p, r) in items)
		{
			Polygon2D line = Fx.Poly(lines, Fx.Circle(r + 7f, 28), new Color(0.06f, 0.18f, 0.55f));
			Node2D puff = new();
			body.AddChild(puff);
			// 右下一弯淡蓝阴影（底圆）+ 左上偏移的亮面；三分之一的团块再点一小块高光
			Fx.Poly(puff, Fx.Circle(r, 28), new Color(0.59f, 0.77f, 0.97f)).Position = new Vector2(r * 0.06f, r * 0.08f);
			Fx.Poly(puff, Fx.Circle(r * 0.84f, 28), new Color(0.9f, 0.95f, 1f)).Position = new Vector2(-r * 0.1f, -r * 0.12f);
			if (Fx.Rng.Next(3) == 0)
			{
				Fx.Poly(puff, Fx.Circle(r * 0.2f, 16), new Color(1f, 1f, 1f, 0.7f)).Position = new Vector2(-r * 0.32f, -r * 0.36f);
			}
			c.Puffs.Add((line, puff, p, Fx.Rand(0f, Mathf.Tau)));
		}
		return c;
	}

	private static void BoilCloud(Cloud c, float time, float grow)
	{
		foreach (var (line, puff, p, ph) in c.Puffs)
		{
			float k = grow * (1f + 0.1f * Mathf.Sin(time * 9f + ph));
			Vector2 q = p + new Vector2(6f * Mathf.Sin(time * 5f + ph), 5f * Mathf.Cos(time * 4f + ph * 1.3f));
			line.Position = q;
			line.Scale = Vector2.One * k;
			puff.Position = q;
			puff.Scale = Vector2.One * k;
		}
	}

	private static Vector2 Avg(IReadOnlyList<Vector2> pts)
	{
		Vector2 c = Vector2.Zero;
		foreach (Vector2 p in pts) c += p;
		return c / pts.Count;
	}

	private static ColorRect Rect(Node2D parent, float W, float H, Color c)
	{
		ColorRect r = new() { Size = new Vector2(W, H), Color = c, MouseFilter = Control.MouseFilterEnum.Ignore };
		parent.AddChild(r);
		return r;
	}

	// 0. 领航小条 + 「总攻击 GO!」提示
	private static void Prompt(Node2D root, float W, float H)
	{
		// 左上角领航（原版是风花头像 + 一句话）
		Node2D nav = new() { Position = new Vector2(-W * 0.4f, H * 0.035f) };
		root.AddChild(nav);
		Fx.Poly(nav, new[] { new Vector2(0f, 0f), new Vector2(W * 0.3f, 0f), new Vector2(W * 0.3f - 14f, 40f), new Vector2(0f, 40f) }, new Color(0.04f, 0.12f, 0.4f, 0.92f));
		Fx.Poly(nav, new[] { new Vector2(0f, 36f), new Vector2(W * 0.3f - 15f, 36f), new Vector2(W * 0.3f - 16f, 40f), new Vector2(0f, 40f) }, Pal.Cyan, additive: true);
		Typo.Text(nav, "成功了！是总攻击的机会！", Typo.CjkHeavy, 22, Pal.White, new Vector2(18f, 6f), outline: 2, outlineColor: new Color(0.02f, 0.05f, 0.15f));
		// 右下斜切蓝带
		Node2D b = new() { Position = new Vector2(W, H * 0.8f), Rotation = -0.18f };
		root.AddChild(b);
		float bw = W * 0.55f, bh = H * 0.13f;
		Fx.Poly(b, new[] { new Vector2(-bw, -bh * 0.5f), new Vector2(80f, -bh * 0.5f), new Vector2(80f, bh * 0.5f), new Vector2(-bw + 40f, bh * 0.5f) }, new Color(0.05f, 0.25f, 0.85f, 0.95f));
		Fx.Poly(b, new[] { new Vector2(-bw, -bh * 0.5f), new Vector2(80f, -bh * 0.5f), new Vector2(80f, -bh * 0.5f + 5f), new Vector2(-bw + 1f, -bh * 0.5f + 5f) }, Pal.Cyan, additive: true);
		Line2D sweep = Fx.Line(b, new[] { new Vector2(-bw, bh * 0.2f), new Vector2(-bw, bh * 0.2f) }, new Color(1f, 1f, 1f, 0.9f), 3f);
		Typo.Text(b, "总攻击", Typo.CjkHeavy, (int)(bh * 0.5f), Pal.White, new Vector2(-bw + 70f, -bh * 0.36f), outline: 3, outlineColor: new Color(0.02f, 0.08f, 0.3f));
		Node2D go = new() { Position = new Vector2(-bw + 70f + bh * 1.75f, -bh * 0.42f) };
		b.AddChild(go);
		Fx.Poly(go, new[] { new Vector2(0f, 4f), new Vector2(bh * 0.5f, 4f), new Vector2(bh * 0.5f, bh * 0.52f), new Vector2(0f, bh * 0.52f) }, Pal.White);
		Typo.Text(go, "GO!", Typo.LatinItalic, (int)(bh * 0.62f), Pal.White, new Vector2(bh * 0.58f, -bh * 0.08f), outline: 4, outlineColor: new Color(0.02f, 0.08f, 0.3f));
		Typo.Text(b, "CANCEL", Typo.LatinItalic, (int)(bh * 0.24f), Pal.White, new Vector2(-bw + 70f + bh * 2.2f, -bh * 0.78f), outline: 2, outlineColor: new Color(0.02f, 0.08f, 0.3f));
		const float total = 1.2f;
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			float navIn = Ease.OutExpo(Mathf.Clamp(time / 0.25f, 0f, 1f));
			float outT = Ease.InCubic(Mathf.Clamp((time - 1.0f) / 0.2f, 0f, 1f));
			nav.Position = new Vector2(-W * 0.4f * (1f - navIn), H * 0.035f);
			nav.Modulate = new Color(1f, 1f, 1f, 1f - outT);
			float inn = Ease.OutExpo(Mathf.Clamp((time - 0.25f) / 0.2f, 0f, 1f));
			b.Position = new Vector2(W + bw * 0.7f * (1f - inn) + W * 0.2f * outT, H * 0.8f);
			b.Modulate = new Color(1f, 1f, 1f, inn * (1f - outT));
			float sw = Mathf.Clamp((time - 0.35f) / 0.3f, 0f, 1f);
			sweep.Points = new[] { new Vector2(-bw, bh * 0.2f), new Vector2(Mathf.Lerp(-bw, 60f, Ease.OutCubic(sw)), bh * 0.2f - 30f * sw) };
			float press = time > 0.95f ? Mathf.Max(0f, 1f - (time - 0.95f) / 0.15f) : 0f;
			go.Scale = Vector2.One * (1f + 0.25f * press);
			go.Modulate = new Color(1f + press, 1f + press, 1f + press, 1f);
		});
	}

	// 1. 光圈转场（用户：原版是以怪物倒下的位置为圆心展开）：从敌人位置张开三层同心大圆（青 → 中蓝 → 深蓝），
	//    外圈一道亮青色细弧先行；铺满后全屏深蓝，两道细圆弧绕着那个圆心转
	private static void Iris(Node2D root, Vector2 focus, float W, float H)
	{
		Node2D n = new();
		root.AddChild(n);
		Color[] cols = { new(0.35f, 0.88f, 1f), new(0.12f, 0.45f, 0.95f), new(0.04f, 0.1f, 0.45f) };
		var discs = new List<Polygon2D>();
		for (int i = 0; i < 3; i++)
		{
			Polygon2D d = Fx.Poly(n, Fx.Circle(10f, 128), cols[i]);
			d.Position = focus;
			discs.Add(d);
		}
		Node2D rim = new() { Position = focus };
		n.AddChild(rim);
		Fx.StrokeClosed(rim, Fx.Circle(10f, 128), new Color(0.6f, 0.95f, 1f, 0.95f), 0.6f);
		Node2D arcs = new() { Position = focus };
		n.AddChild(arcs);
		P3Ui.Arc(arcs, Vector2.Zero, H * 0.7f, 0f, Mathf.Pi * 0.9f, new Color(0.45f, 0.9f, 1f, 0.8f), 4f);
		P3Ui.Arc(arcs, Vector2.Zero, H * 1.05f, Mathf.Pi, Mathf.Pi * 1.7f, new Color(1f, 1f, 1f, 0.45f), 2f);
		// 半径要盖住离圆心最远的屏幕角
		float R = 0f;
		foreach (Vector2 corner in new[] { Vector2.Zero, new Vector2(W, 0f), new Vector2(0f, H), new Vector2(W, H) })
		{
			R = Mathf.Max(R, (corner - focus).Length());
		}
		R *= 1.05f;
		const float total = 1.25f;
		Fx.Animate(n, total, t =>
		{
			float time = t * total;
			for (int i = 0; i < 3; i++)
			{
				float u = Ease.InOutCubic(Mathf.Clamp((time - 0.13f * i) / 0.55f, 0f, 1f));
				discs[i].Scale = Vector2.One * (R * u / 10f);
			}
			float ru = Ease.OutCubic(Mathf.Clamp(time / 0.5f, 0f, 1f));
			rim.Scale = Vector2.One * (R * 1.02f * ru / 10f);
			rim.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 0.45f) / 0.15f, 0f, 1f));
			arcs.Rotation = 0.6f * time;
			arcs.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.7f) / 0.15f, 0f, 1f));
			n.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 1.15f) / 0.1f, 0f, 1f));
		});
		Fx.FreeAfter(n, total + 0.05f);
	}

	// 2. 亮青色单色定格：蓝色平涂剪影 + 斜光 + 碎片
	private static void Pose(Node2D root, float W, float H)
	{
		Node2D n = new();
		root.AddChild(n);
		ColorRect bg = Rect(n, W, H, new Color(0.04f, 0.1f, 0.45f, 1f));
		Fx.Poly(n, Fx.Circle(H * 0.75f, 128), new Color(0.35f, 0.88f, 1f)).Position = new Vector2(W * 0.62f, H * 0.6f);   // 原版集结镜头身后的青色大圆盘
		Fx.Poly(n, new[] { new Vector2(W * 0.2f, -20f), new Vector2(W * 0.55f, -20f), new Vector2(W * 0.3f, H + 20f), new Vector2(-W * 0.05f, H + 20f) }, new Color(1f, 1f, 1f, 0.35f), additive: true);
		Texture2D? sil = Tex("bu_silhouette");
		Sprite2D? fig = null;
		if (sil != null)
		{
			fig = new Sprite2D { Texture = sil, Scale = Vector2.One * (H * 1.05f / sil.GetHeight()), Position = new Vector2(W * 0.62f, H * 0.58f), FlipH = true };
			n.AddChild(fig);
		}
		var petals = Petals(n, W, H, 34, new Color(0.15f, 0.5f, 0.95f));
		ColorRect flash = Rect(n, W, H, new Color(1f, 1f, 1f, 1f));
		const float total = 0.85f;
		Fx.Animate(n, total, t =>
		{
			float time = t * total;
			flash.Color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp(time / 0.15f, 0f, 1f));
			if (fig != null)
			{
				fig.Position = new Vector2(W * 0.62f - 40f * time, H * 0.58f);
			}
			DriftPetals(petals, time, H);
			n.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 0.78f) / 0.07f, 0f, 1f));
		});
		Fx.FreeAfter(n, total + 0.05f);
	}

	// 3. 近景：立绘（蓝调）右侧缓推
	private static void CloseUp(Node2D root, float W, float H)
	{
		Node2D n = new();
		root.AddChild(n);
		Rect(n, W, H, new Color(0.03f, 0.1f, 0.42f, 1f));
		Fx.Poly(n, new[] { new Vector2(0f, H * 0.55f), new Vector2(W, H * 0.3f), new Vector2(W, H), new Vector2(0f, H) }, new Color(0.08f, 0.25f, 0.7f, 0.6f));
		// 立绘用总攻击结束立绘的暗蓝版（A2）只取脸部（P3R 半身立绘的脸部差分直接叠会有接缝，不用）
		Texture2D? bu = Tex("allout_finish_dark");
		Sprite2D? fig = null;
		Vector2 faceCenter = new(1075f, 300f);
		if (bu != null)
		{
			fig = new Sprite2D { Texture = bu, Centered = false, Scale = Vector2.One * (H * 1.6f / bu.GetHeight()), Modulate = new Color(0.85f, 0.95f, 1.1f) };
			fig.Position = new Vector2(W * 0.7f, H * 0.5f) - faceCenter * fig.Scale.X;
			n.AddChild(fig);
		}
		var petals = Petals(n, W, H, 40, new Color(0.3f, 0.65f, 1f));
		const float total = 1.0f;
		Fx.Animate(n, total, t =>
		{
			float time = t * total;
			if (fig != null)
			{
				float k = H * 1.6f / bu!.GetHeight() * (1f + 0.05f * time);
				fig.Scale = Vector2.One * k;
				fig.Position = new Vector2(W * 0.7f - 25f * time, H * 0.5f) - faceCenter * k;
			}
			DriftPetals(petals, time + 0.85f, H);
			n.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.06f, 0f, 1f) * (1f - Mathf.Clamp((time - 0.94f) / 0.06f, 0f, 1f)));
		});
		Fx.FreeAfter(n, total + 0.05f);
	}

	private static List<(Polygon2D P, Vector2 O, Vector2 V, float Spin)> Petals(Node2D parent, float W, float H, int count, Color col)
	{
		var list = new List<(Polygon2D, Vector2, Vector2, float)>();
		for (int i = 0; i < count; i++)
		{
			float sz = Fx.Rand(6f, 16f);
			Polygon2D p = Fx.Poly(parent, new[] { new Vector2(0f, -sz), new Vector2(sz * 0.5f, 0f), new Vector2(0f, sz * 0.7f), new Vector2(-sz * 0.4f, 0f) }, new Color(col, Fx.Rand(0.6f, 0.95f)));
			list.Add((p, new Vector2(Fx.Rand(0f, W), Fx.Rand(-H * 0.1f, H)), new Vector2(Fx.Rand(-60f, -20f), Fx.Rand(40f, 120f)), Fx.Rand(-3f, 3f)));
		}
		return list;
	}

	private static void DriftPetals(List<(Polygon2D P, Vector2 O, Vector2 V, float Spin)> petals, float time, float H)
	{
		foreach (var (p, o, v, spin) in petals)
		{
			Vector2 q = o + v * time;
			p.Position = new Vector2(q.X, Mathf.PosMod(q.Y, H * 1.1f) - H * 0.05f);
			p.Rotation = spin * time;
			p.Scale = new Vector2(Mathf.Abs(Mathf.Cos(time * 4f + spin)), 1f);
		}
	}

	// 4. 冲锋：蓝色棋盘格地面高速卷动 + 速度线 + 远处成形的烟云
	private static void Rush(Node2D root, Vector2 focus, float W, float H)
	{
		Node2D n = new();
		root.AddChild(n);
		Rect(n, W, H, new Color(0.02f, 0.07f, 0.3f, 1f));
		float horizon = H * 0.42f;
		Node2D floor = Checker(n, W, H, horizon, out var tiles);
		Cloud cloud = MakeCloud(n, new Vector2(W * 0.55f, horizon + 30f), W, H, 12, W * 0.12f, H * 0.06f);
		Node2D lines = new();
		n.AddChild(lines);
		var streaks = new List<(Polygon2D p, float y, float ph, float len)>();
		for (int i = 0; i < 18; i++)
		{
			float y = Fx.Rand(0f, H);
			Polygon2D p = Fx.Poly(lines, new[] { new Vector2(0f, -2f), new Vector2(1f, 0f), new Vector2(0f, 2f) }, new Color(0.8f, 0.92f, 1f, 0.7f), additive: true);
			streaks.Add((p, y, Fx.Rand(0f, 1f), Fx.Rand(200f, 520f)));
		}
		const float total = 1.3f;
		Fx.Animate(n, total, t =>
		{
			float time = t * total;
			UpdateChecker(tiles, W, H, horizon, time * 3.2f);
			float grow = Ease.OutCubic(Mathf.Clamp((time - 0.55f) / 0.6f, 0f, 1f));
			BoilCloud(cloud, time, grow);
			cloud.Root.Scale = Vector2.One * (0.5f + 0.5f * grow);
			foreach (var (p, y, ph, len) in streaks)
			{
				float u = Mathf.PosMod(ph + time * 2.4f, 1f);
				float x = W * (1.2f - 1.6f * u);
				Fx.Reshape(p, new[] { new Vector2(x, y - 1.5f), new Vector2(x + len, y), new Vector2(x, y + 1.5f) });
			}
			n.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(time / 0.08f, 0f, 1f));
		});
		Fx.FreeAfter(n, total + 0.05f);
	}

	/// <summary>透视棋盘格地面（地平线以下）：返回所有格子，UpdateChecker 按 scroll 重新摆放。</summary>
	private static Node2D Checker(Node2D parent, float W, float H, float horizon, out List<(Polygon2D P, int Row, int Col)> tiles)
	{
		Node2D floor = new();
		parent.AddChild(floor);
		Fx.Poly(floor, new[] { new Vector2(0f, horizon), new Vector2(W, horizon), new Vector2(W, H), new Vector2(0f, H) }, new Color(0.05f, 0.16f, 0.55f));
		tiles = new List<(Polygon2D, int, int)>();
		for (int r = 0; r < 14; r++)
		{
			for (int c = -9; c <= 9; c++)
			{
				Polygon2D p = Fx.Poly(floor, new Vector2[4], new Color(0.25f, 0.5f, 0.95f, 0.9f));
				tiles.Add((p, r, c));
			}
		}
		UpdateChecker(tiles, W, H, horizon, 0f);
		Fx.Poly(floor, new[] { new Vector2(0f, horizon - 2f), new Vector2(W, horizon - 2f), new Vector2(W, horizon + 40f), new Vector2(0f, horizon + 40f) }, new Color(0.02f, 0.07f, 0.3f, 0.75f));
		return floor;
	}

	private static void UpdateChecker(List<(Polygon2D P, int Row, int Col)> tiles, float W, float H, float horizon, float scroll)
	{
		float vx = W * 0.5f;
		float Depth(float z) => horizon + (H - horizon) * 1.6f / (z + 1.6f);   // z = 离镜头的距离（格）
		float frac = scroll - Mathf.Floor(scroll);
		int shift = (int)Mathf.Floor(scroll);
		foreach (var (p, row, col) in tiles)
		{
			float z0 = row - frac, z1 = row + 1 - frac;
			if (z0 < 0f) z0 = 0f;
			float y0 = Depth(z0), y1 = Depth(z1);
			float k0 = (y0 - horizon) / (H - horizon), k1 = (y1 - horizon) / (H - horizon);
			float tw = W * 0.22f;
			Vector2 a = new(vx + col * tw * k0, y0), b = new(vx + (col + 1) * tw * k0, y0);
			Vector2 c = new(vx + (col + 1) * tw * k1, y1), d = new(vx + col * tw * k1, y1);
			Fx.Reshape(p, new[] { a, b, c, d });
			p.Visible = ((row + shift + col) & 1) == 0;
		}
	}

	private static void Flash(Node2D root, float W, float H, float len)
	{
		ColorRect w = Rect(root, W, H, new Color(1f, 1f, 1f, 1f));
		Fx.Animate(w, len, t => w.Color = new Color(1f, 1f, 1f, 1f - t));
		Fx.FreeAfter(w, len + 0.02f);
	}

	// 5. 乱斗烟尘（1.8s）：蓝色舞台 + 蓝白烟团 + 四芒星 + 斩击线 + 圆形纹章
	private static void Brawl(Node2D root, Vector2 focus, float W, float H)
	{
		Node2D n = new();
		root.AddChild(n);
		Rect(n, W, H, new Color(0.03f, 0.12f, 0.45f, 1f));
		float horizon = H * 0.45f;
		Checker(n, W, H, horizon, out var tiles);
		Vector2 at = new(W * 0.5f, H * 0.52f);
		Cloud cloud = MakeCloud(n, at, W, H, 34, W * 0.28f, H * 0.17f);
		const float total = 1.85f;
		Fx.Animate(n, total, t =>
		{
			float time = t * total;
			UpdateChecker(tiles, W, H, horizon, 0.3f * time);
			float inn = Ease.OutBack(Mathf.Clamp(time / 0.2f, 0f, 1f));
			BoilCloud(cloud, time, inn);
			cloud.Root.Position = at + new Vector2(10f * Mathf.Sin(time * 31f), 7f * Mathf.Cos(time * 27f));
		});
		for (int i = 0; i < 24; i++)
		{
			float when = 0.12f + 0.068f * i;
			int idx = i;
			Fx.After(n, when, "brawl hit", () =>
			{
				Vector2 p = at + new Vector2(Fx.Rand(-W * 0.24f, W * 0.24f), Fx.Rand(-H * 0.18f, H * 0.15f));
				Node2D st = new() { Position = p };
				n.AddChild(st);
				if (idx % 5 == 2)
				{
					// 圆形纹章（原版烟云里闪过的圆环 + 星）
					Fx.StrokeClosed(st, Fx.Circle(70f, 48), new Color(1f, 1f, 1f, 0.8f), 5f);
					Fx.Poly(st, Fx.Star4(46f, 0.2f), new Color(1f, 1f, 1f, 0.7f), additive: true);
					Fx.Animate(st, 0.3f, t => { st.Scale = Vector2.One * (0.6f + 0.6f * Ease.OutCubic(t)); st.Modulate = new Color(1f, 1f, 1f, 1f - t); st.Rotation = t; });
					Fx.FreeAfter(st, 0.32);
					return;
				}
				Fx.Poly(st, Fx.Star4(Fx.Rand(40f, 90f), 0.12f), Pal.White, additive: true);
				Fx.Animate(st, 0.18f, t => { st.Scale = Vector2.One * Mathf.Sin(t * Mathf.Pi); st.Rotation = t * 0.6f; });
				Fx.FreeAfter(st, 0.2);
				if (Fx.Rng.Next(2) == 0)
				{
					Vector2 d = Vector2.Right.Rotated(Fx.Rand(-0.6f, 0.6f));
					Node2D sl = new() { Position = p };
					n.AddChild(sl);
					Fx.Stroke(sl, new[] { -d * 170f, d * 170f }, new Color(1f, 1f, 1f, 0.95f), 6f);
					Fx.Animate(sl, 0.14f, t => sl.Modulate = new Color(1f, 1f, 1f, 1f - t));
					Fx.FreeAfter(sl, 0.16);
				}
			});
		}
		Fx.FreeAfter(n, total + 0.05f);
	}

	// 6. 全屏白 0.9s（最后 0.25s 透出下一幕）
	private static void WhiteHold(Node2D root, float W, float H)
	{
		ColorRect w = Rect(root, W, H, new Color(0.97f, 0.98f, 1f, 0f));
		const float total = 1.15f;
		Fx.Animate(w, total, t =>
		{
			float time = t * total;
			float a = time < 0.08f ? time / 0.08f : time < 0.9f ? 1f : 1f - (time - 0.9f) / 0.25f;
			w.Color = new Color(0.97f, 0.98f, 1f, a);
		});
		Fx.FreeAfter(w, total + 0.02f);
	}

	// 7. 击杀：蓝色舞台、敌人黑影炸成黑红墨迹 + 红色斩线 + 白色三角；结城理剪影滑过收刀；9.0 青色大圆盘
	private static void Kill(Node2D root, IReadOnlyList<Vector2> enemies, Vector2 focus, float W, float H)
	{
		Node2D n = new();
		root.AddChild(n);
		root.MoveChild(n, root.GetChildCount() - 2 >= 0 ? root.GetChildCount() - 2 : 0);   // 压在还没退完的白幕下面
		Rect(n, W, H, new Color(0.06f, 0.3f, 0.75f, 1f));
		float horizon = H * 0.5f;
		Checker(n, W, H, horizon, out var tiles);
		Node2D disc = new() { Position = new Vector2(W * 0.66f, H * 0.5f) };
		n.AddChild(disc);
		Fx.Poly(disc, Fx.Circle(H * 0.48f, 96), new Color(0.35f, 0.85f, 1f, 0.95f));
		Fx.StrokeClosed(disc, Fx.Circle(H * 0.54f, 96), new Color(0.15f, 0.5f, 0.95f, 0.9f), H * 0.05f, additive: false);
		disc.Scale = Vector2.Zero;
		// 敌人黑影（画面中右侧一排）
		var blobs = new List<Node2D>();
		int ne = Mathf.Min(enemies.Count, 4);
		for (int i = 0; i < ne; i++)
		{
			Node2D b = new() { Position = new Vector2(W * (0.55f + 0.12f * i), H * 0.6f) };
			n.AddChild(b);
			Fx.Poly(b, Fx.Circle(H * 0.1f, 24, 1.2f), new Color(0.03f, 0.03f, 0.06f, 0.95f));
			blobs.Add(b);
		}
		// 结城理剪影（全身立绘压成深蓝）从左前景滑过 → 停住收刀
		Texture2D? full = Tex("makoto_full");
		Sprite2D? hero = null;
		if (full != null)
		{
			hero = new Sprite2D { Texture = full, Scale = Vector2.One * (H * 0.95f / full.GetHeight()), Modulate = new Color(0.08f, 0.18f, 0.5f, 1f), Position = new Vector2(-W * 0.2f, H * 0.58f) };
			n.AddChild(hero);
		}
		bool burst = false;
		const float total = 1.45f;
		Fx.Animate(n, total, t =>
		{
			float time = t * total;
			if (hero != null)
			{
				float slide = Ease.OutCubic(Mathf.Clamp(time / 0.45f, 0f, 1f));
				hero.Position = new Vector2(Mathf.Lerp(-W * 0.2f, W * 0.3f, slide) + 18f * Mathf.Max(0f, time - 0.45f), H * 0.58f);
				float lit = Mathf.Clamp((time - 0.8f) / 0.3f, 0f, 1f);
				hero.Modulate = new Color(Mathf.Lerp(0.08f, 0.55f, lit), Mathf.Lerp(0.18f, 0.7f, lit), Mathf.Lerp(0.5f, 1f, lit), 1f);
			}
			if (!burst && time >= 0.3f)
			{
				burst = true;
				foreach (Node2D b in blobs)
				{
					Splatter(n, b.Position, H);
					b.Visible = false;
				}
				RedSlash(n, new Vector2(W * 0.55f, H * 0.62f), W);
				Triangles(n, new Vector2(W * 0.62f, H * 0.55f), H);
				NGame.Instance?.ScreenShake(ShakeStrength.Strong, ShakeDuration.Short);
			}
			float d = Ease.OutBack(Mathf.Clamp((time - 0.8f) / 0.4f, 0f, 1f));
			disc.Scale = Vector2.One * d;
			UpdateChecker(tiles, W, H, horizon, 0.1f * time);
		});
		Fx.FreeAfter(n, total + 0.1f);
	}

	// 敌人炸开的黑红墨迹（原版是熔岩状的一摊 + 四溅的墨点）
	private static void Splatter(Node2D parent, Vector2 at, float H)
	{
		Node2D s = new() { Position = at };
		parent.AddChild(s);
		Fx.Poly(s, Fx.Circle(H * 0.12f, 20, 0.35f), new Color(0.06f, 0.02f, 0.05f, 0.9f)).Position = new Vector2(0f, H * 0.08f);
		for (int i = 0; i < 30; i++)
		{
			Node2D d = new();
			s.AddChild(d);
			float r = Fx.Rand(6f, 22f);
			Fx.Poly(d, Fx.Circle(r, 10), i % 4 == 0 ? new Color(0.85f, 0.05f, 0.15f) : new Color(0.05f, 0.03f, 0.06f));
			Vector2 v = Fx.RandDir() * Fx.Rand(140f, 480f);
			Fx.Animate(d, 0.75f, t =>
			{
				d.Position = v * Ease.OutCubic(t) + new Vector2(0f, 160f * t * t);
				d.Scale = new Vector2(1f + 0.8f * t, 1f - 0.4f * t);
				d.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
			});
		}
		Fx.FreeAfter(s, 1.1);
	}

	private static void Triangles(Node2D parent, Vector2 at, float H)
	{
		for (int i = 0; i < 10; i++)
		{
			float sz = Fx.Rand(18f, 50f);
			Polygon2D tri = Fx.Poly(parent, new[] { new Vector2(0f, -sz), new Vector2(sz * 0.8f, sz * 0.6f), new Vector2(-sz * 0.7f, sz * 0.5f) }, new Color(1f, 1f, 1f, 0.95f));
			tri.Position = at;
			Vector2 v = Fx.RandDir() * Fx.Rand(200f, 500f);
			float spin = Fx.Rand(-6f, 6f);
			Fx.Animate(tri, 0.8f, t =>
			{
				tri.Position = at + v * Ease.OutCubic(t);
				tri.Rotation = spin * t;
				tri.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(t));
			});
		}
	}

	private static void RedSlash(Node2D parent, Vector2 focus, float W)
	{
		Node2D n = new();
		parent.AddChild(n);
		Vector2 a = focus + new Vector2(-W * 0.5f, 80f), b = focus + new Vector2(W * 0.5f, -60f);
		Polygon2D poly = Fx.Poly(n, new Vector2[3], new Color(0.95f, 0.1f, 0.2f, 0.95f), additive: true);
		Fx.Animate(n, 0.6f, t =>
		{
			float g = Ease.OutExpo(Mathf.Clamp(t / 0.25f, 0f, 1f));
			Fx.Reshape(poly, Fx.StrokePts(new[] { a, a.Lerp(b, g) }, 8f * (1f - 0.5f * t), taper: true));
			n.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((t - 0.6f) / 0.4f, 0f, 1f)));
		});
		Fx.FreeAfter(n, 0.62);
	}

	// 5. 结束立绘（P3R 原版：T_Btl_AlloutFinish_Pc01 + T_Btl_AlloutFinishText_Pc01）
	//   0.00  青底从右上斜切扫入，巨大圆环张开，白色三角碎片向外飞
	//   0.05  立绘从右侧滑入，先是暗蓝调（A2），0.22 时一道白闪亮起成正常色（A1）
	//   0.18  背后的深蓝斜体巨字从左缓移；放射速度线
	//   0.24  「DONE AND DUSTED.」原版字图砸进来（放大回弹 + 白闪）
	//   1.00  整体褪成黑白，「RESULT」从上滑入；1.25 起淡出
	private static Texture2D? Tex(string name) =>
		ResourceLoader.Exists($"res://p3makoto/ui/{name}.png") ? ResourceLoader.Load<Texture2D>($"res://p3makoto/ui/{name}.png") : null;

	private static void Finish(Node2D root, float W, float H)
	{
		Node2D f = new();
		root.AddChild(f);
		ColorRect bg = new() { Size = new Vector2(W, H), Color = new Color(0.25f, 0.72f, 0.95f, 0.97f), MouseFilter = Control.MouseFilterEnum.Ignore };
		f.AddChild(bg);
		// 斜切的深一档色带（P3R 结束画面左下的斜块）
		Polygon2D band = Fx.Poly(f, new[] { new Vector2(-40f, H * 0.78f), new Vector2(W * 0.62f, H * 0.52f), new Vector2(W * 0.7f, H + 40f), new Vector2(-40f, H + 40f) }, new Color(0.14f, 0.52f, 0.88f, 0.85f));

		Node2D ring = new() { Position = new Vector2(W * 0.66f, H * 0.5f) };
		f.AddChild(ring);
		Fx.StrokeClosed(ring, Fx.Circle(H * 0.56f, 128), new Color(0.12f, 0.45f, 0.9f, 0.9f), H * 0.1f, additive: false);
		Fx.StrokeClosed(ring, Fx.Circle(H * 0.43f, 128), new Color(1f, 1f, 1f, 0.55f), 3f);
		Fx.StrokeClosed(ring, Fx.Circle(H * 0.66f, 128), new Color(1f, 1f, 1f, 0.3f), 2f);
		// 圆环上的刻度
		for (int k = 0; k < 48; k++)
		{
			Vector2 d = Vector2.Right.Rotated(k * Mathf.Tau / 48f);
			float len = k % 4 == 0 ? 26f : 12f;
			Fx.Line(ring, new[] { d * (H * 0.62f), d * (H * 0.62f + len) }, new Color(1f, 1f, 1f, k % 4 == 0 ? 0.6f : 0.3f), k % 4 == 0 ? 3f : 2f);
		}

		Node2D word = new() { Position = new Vector2(W * 0.02f, H * 0.62f), Rotation = -0.12f, Skew = -0.25f };
		f.AddChild(word);
		Typo.Text(word, "MAJESTIC", Typo.LatinItalic, (int)(H * 0.3f), new Color(0.03f, 0.08f, 0.3f, 0.92f), Vector2.Zero);

		// 速度线（从圆环中心放射，压在立绘后面）
		Node2D speed = new() { Position = ring.Position };
		f.AddChild(speed);
		for (int k = 0; k < 26; k++)
		{
			float a = Fx.Rand(0f, Mathf.Tau);
			float r0 = Fx.Rand(H * 0.7f, H * 0.85f);
			Vector2 d = Vector2.Right.Rotated(a);
			Fx.Poly(speed, new[] { d * r0 + d.Orthogonal() * Fx.Rand(3f, 9f), d * (r0 + Fx.Rand(260f, 520f)), d * r0 - d.Orthogonal() * Fx.Rand(3f, 9f) }, new Color(1f, 1f, 1f, 0.5f), additive: true);
		}

		// 三角碎片
		Node2D shards = new() { Position = ring.Position };
		f.AddChild(shards);
		List<(Polygon2D p, Vector2 o, Vector2 v, float w)> bits = new();
		for (int k = 0; k < 18; k++)
		{
			float sz = Fx.Rand(14f, 46f);
			Polygon2D tri = Fx.Poly(shards, new[] { new Vector2(0f, -sz), new Vector2(sz * 0.8f, sz * 0.6f), new Vector2(-sz * 0.7f, sz * 0.5f) }, new Color(1f, 1f, 1f, Fx.Rand(0.6f, 0.95f)));
			tri.Position = Fx.RandDir() * Fx.Rand(H * 0.2f, H * 0.45f);
			bits.Add((tri, tri.Position, tri.Position.Normalized() * Fx.Rand(160f, 420f), Fx.Rand(-5f, 5f)));
		}

		Texture2D? lit = Tex("allout_finish") ?? Tex("victory_portrait");
		Texture2D? dark = Tex("allout_finish_dark");
		Node2D heroRoot = new();
		f.AddChild(heroRoot);
		float hs = lit != null ? H * 1.0f / lit.GetHeight() : 1f;
		Sprite2D hero = new() { Texture = lit, Material = new ShaderMaterial { Shader = Gray }, Scale = Vector2.One * hs };
		heroRoot.AddChild(hero);
		Sprite2D? heroDark = dark != null ? new Sprite2D { Texture = dark, Scale = Vector2.One * hs } : null;
		if (heroDark != null)
			heroRoot.AddChild(heroDark);
		// 立绘的白色剪影闪（亮起那一下）
		Sprite2D flash = new() { Texture = lit, Scale = Vector2.One * hs, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add }, Modulate = new Color(1f, 1f, 1f, 0f) };
		heroRoot.AddChild(flash);
		Vector2 heroAt = new(W * 0.65f, H * 0.56f);

		Texture2D? txt = Tex("allout_text");
		Node2D text = new() { Position = new Vector2(W * 0.2f, H * 0.21f) };
		f.AddChild(text);
		float ts = H * 0.3f / (txt?.GetHeight() ?? 241);
		if (txt != null)
		{
			// 深蓝错位阴影 + 原版字图
			text.AddChild(new Sprite2D { Texture = txt, Scale = Vector2.One * ts, Position = new Vector2(8f, 8f), Modulate = new Color(0.03f, 0.08f, 0.3f, 0.85f) });
			text.AddChild(new Sprite2D { Texture = txt, Scale = Vector2.One * ts });
		}
		else
		{
			int fs = (int)(H * 0.075f);
			Typo.Text(text, "DONE", Typo.LatinItalic, fs, Pal.White, new Vector2(0f, 0f), outline: 4, outlineColor: new Color(0.03f, 0.08f, 0.3f));
			Typo.Text(text, "AND", Typo.LatinItalic, fs, Pal.White, new Vector2(fs * 0.4f, fs * 0.9f), outline: 4, outlineColor: new Color(0.03f, 0.08f, 0.3f));
			Typo.Text(text, "DUSTED.", Typo.LatinItalic, fs, Pal.White, new Vector2(0f, fs * 1.8f), outline: 4, outlineColor: new Color(0.03f, 0.08f, 0.3f));
		}
		Sprite2D textFlash = new() { Texture = txt, Scale = Vector2.One * ts, Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add } };
		if (txt != null)
			text.AddChild(textFlash);

		Node2D result = new() { Position = new Vector2(W * 0.5f, -H * 0.2f) };
		f.AddChild(result);
		Polygon2D rbar = Fx.Poly(result, new[] { new Vector2(-W * 0.6f, -H * 0.07f), new Vector2(W * 0.6f, -H * 0.1f), new Vector2(W * 0.6f, H * 0.06f), new Vector2(-W * 0.6f, H * 0.09f) }, new Color(0.03f, 0.05f, 0.12f, 0.9f));
		Typo.Text(result, "RESULT", Typo.LatinItalic, (int)(H * 0.13f), Pal.White, new Vector2(-H * 0.33f, -H * 0.09f), outline: 3, outlineColor: new Color(0.03f, 0.08f, 0.3f));

		// 原版大闪（青底扫入那一下）
		ColorRect pop = new() { Size = new Vector2(W, H), Color = new Color(1f, 1f, 1f, 1f), MouseFilter = Control.MouseFilterEnum.Ignore };
		f.AddChild(pop);

		ShaderMaterial gm = (ShaderMaterial)hero.Material;
		const float total = 2.4f;
		Fx.Animate(f, total, t =>
		{
			float time = t * total;
			float inn = Ease.OutExpo(Mathf.Clamp(time / 0.25f, 0f, 1f));
			pop.Color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp(time / 0.12f, 0f, 1f));
			bg.Position = new Vector2(W * 0.3f * (1f - inn), -H * 0.3f * (1f - inn));
			band.Position = new Vector2(-W * 0.4f * (1f - inn), 0f);
			heroRoot.Position = heroAt + new Vector2(W * 0.25f * (1f - inn) - 22f * time, 6f * Mathf.Sin(time * 3f));
			// 暗蓝 → 亮起
			float lift = Mathf.Clamp((time - 0.22f) / 0.1f, 0f, 1f);
			if (heroDark != null)
				heroDark.Modulate = new Color(1f, 1f, 1f, 1f - lift);
			float fl = time < 0.22f ? 0f : Mathf.Max(0f, 1f - (time - 0.22f) / 0.22f);
			flash.Modulate = new Color(1f, 1f, 1f, fl * 0.85f);
			ring.Rotation = time * 0.2f;
			ring.Scale = Vector2.One * (0.7f + 0.3f * inn + 0.02f * time);
			speed.Rotation = -time * 0.05f;
			speed.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.15f) / 0.1f, 0f, 1f) * (1f - Mathf.Clamp((time - 0.6f) / 0.4f, 0f, 1f)));
			speed.Scale = Vector2.One * (1f + 0.25f * time);
			foreach ((Polygon2D p, Vector2 o, Vector2 v, float w) in bits)
			{
				p.Position = o + v * Ease.OutCubic(Mathf.Clamp(time / 1.2f, 0f, 1f)) * 1.1f;
				p.Rotation = w * time;
			}
			shards.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((time - 0.5f) / 0.5f, 0f, 1f));
			word.Position = new Vector2(W * 0.02f - 160f * (1f - inn) + 30f * time, H * 0.62f);
			// 字图（原版）：0.5s 起从左侧滑入，0.75s 到位时白闪一下
			float tp = Ease.OutExpo(Mathf.Clamp((time - 0.5f) / 0.25f, 0f, 1f));
			text.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((time - 0.5f) / 0.05f, 0f, 1f));
			text.Position = new Vector2(W * 0.2f - W * 0.45f * (1f - tp), H * 0.21f);
			textFlash.Modulate = new Color(1f, 1f, 1f, time < 0.72f ? 0f : Mathf.Max(0f, 1f - (time - 0.72f) / 0.2f));
			float g = Mathf.Clamp((time - 1.75f) / 0.25f, 0f, 1f);
			gm.SetShaderParameter("gray", g);
			bg.Color = new Color(Mathf.Lerp(0.25f, 0.7f, g), Mathf.Lerp(0.72f, 0.72f, g), Mathf.Lerp(0.95f, 0.74f, g), 0.97f);
			band.Modulate = new Color(1f - 0.6f * g, 1f - 0.4f * g, 1f - 0.5f * g, 1f);
			float rin = Ease.OutExpo(Mathf.Clamp((time - 2.0f) / 0.2f, 0f, 1f));
			result.Position = new Vector2(Mathf.Lerp(-W * 0.2f, W * 0.42f, rin), Mathf.Lerp(-H * 0.2f, H * 0.12f, rin));   // 原版：RESULT 从左上斜着滑入
			rbar.Modulate = new Color(1f, 1f, 1f, rin);
			f.Modulate = new Color(1f, 1f, 1f, 1f - Ease.InCubic(Mathf.Clamp((time - 2.2f) / 0.2f, 0f, 1f)));
		});
		Fx.FreeAfter(f, total + 0.05f);
	}
}
