using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 牌的特效（2026-10-29 第三版，用发光积木 Glow.cs）：主体是光，P5 的味道靠锐利的形状和干脆的节奏。
/// 每个都按"预备 → 爆发 → 余韵"编排，元素错开几十毫秒，运动 OutExpo，见 docs/p5/FX_PLAN.md。
/// </summary>
public static class CardFx
{
	// ───────────────────────── 防御：六边形光盾阵 ─────────────────────────

	/// <summary>
	/// 防御（Joker 获得格挡时）：
	/// 预备 —— 身前一圈红光往里收；
	/// 构建 —— 一面由十几块六边形光板拼成的竖直盾，从中心一块往外一圈圈"点亮"：每块先白闪、OutBack 弹出，
	///         然后变成半透明红光面 + 白热亮边；外面一圈红晕轮廓；同时折射波 + 光环；
	/// 停留 —— 一道斜的亮光从上往下扫过盾面（经过的光板一亮），边角星光，背后几颗慢慢上浮的光粒；
	/// 碎裂 —— 光板从外往里一块块碎掉，发光碎片往前飞散，中心一团闪光。
	/// chest = 胸口，facing = 敌人方向。
	/// </summary>
	public static void Guard(Control layer, Vector2 chest, float facing = 1f)
	{
		const float R = 25f;                                    // 六边形外接圆半径
		Vector2 center = chest + new Vector2(150f * facing, 30f);
		Glow.Gather(layer, center, Pal5.Bright, 300f, 0.12f);

		// 蜂窝格子：竖直的盾形（椭圆里的格子），尖顶六边形，列间距 √3R、行间距 1.5R
		var cells = new List<Vector2>();
		float w = Mathf.Sqrt(3f) * R;
		for (int row = -4; row <= 4; row++)
		{
			for (int col = -2; col <= 2; col++)
			{
				Vector2 p = new(col * w + (row % 2 != 0 ? w / 2f : 0f), row * 1.5f * R);
				if (p.X * p.X / (70f * 70f) + p.Y * p.Y / (150f * 150f) <= 1f)
				{
					cells.Add(p);
				}
			}
		}

		Node2D root = Fx.Root(layer, center, "P5Guard", 1.2);
		root.Scale = new Vector2(facing * 0.82f, 1f);           // 横向压一点：盾面略微侧对着敌人
		root.Skew = -0.12f * facing;
		// 盾后面一层红色柔光，给光板一个"发光体"的底
		Sprite2D bloom = new() { Texture = Glow.Soft, Material = Fx.Additive, Scale = Vector2.Zero, Modulate = new Color(Pal5.Bright, 0.6f) };
		root.AddChild(bloom);
		var panels = new List<(Node2D N, Polygon2D Fill, Line2D Rim, Polygon2D Flash, float Delay, Vector2 P)>();
		Vector2[] hex = Hex(R - 2.5f);
		foreach (Vector2 p in cells)
		{
			Node2D n = new() { Position = p, Scale = Vector2.Zero };
			root.AddChild(n);
			Polygon2D fill = Fx.Poly(n, hex, new Color(Pal5.Bright, 0.34f), additive: true);
			Line2D rim = new() { Points = hex, Closed = true, Width = 2.6f, DefaultColor = Pal5.Hot, Material = Fx.Additive, Antialiased = true };
			n.AddChild(rim);
			Polygon2D flash = Fx.Poly(n, hex, Pal5.Hot, additive: true);
			float delay = 0.12f + p.Length() / 150f * 0.12f + Fx.Rand(0f, 0.015f);
			panels.Add((n, fill, rim, flash, delay, p));
		}
		// 外框：一圈粗一点的红晕描边（勾出盾的轮廓）
		Line2D outline = new() { Points = Ellipse(92f, 172f, 48), Closed = true, Width = 8f, DefaultColor = new Color(Pal5.Bright, 0.55f),
			Material = Fx.Additive, Antialiased = true };
		root.AddChild(outline);
		outline.Scale = Vector2.Zero;

		const float total = 0.9f, breakAt = 0.62f;
		Fx.Animate(root, 0, total, t =>
		{
			float time = t * total;
			// 扫光位置：0.34~0.56s 一条斜线从上往下
			float sw = Ease5.Seg(time, 0.34f, 0.22f), sweepY = Mathf.Lerp(-190f, 190f, Fx.OutCubic(sw));
			foreach (var (n, fill, rim, flash, delay, p) in panels)
			{
				float u = Ease5.Seg(time, delay, 0.12f);
				float brk = Ease5.Seg(time, breakAt + (1f - p.Length() / 160f) * 0.06f, 0.05f);   // 外圈先碎
				n.Visible = u > 0f && brk < 1f;
				n.Scale = Vector2.One * Ease5.OutBack(u) * (1f - 0.3f * brk);
				flash.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(time, delay, 0.1f));
				float lit = sw > 0f && sw < 1f ? Mathf.Exp(-Mathf.Pow((p.Y + p.X * 0.6f - sweepY) / 28f, 2f)) : 0f;
				fill.Modulate = new Color(1f + lit * 2f, 1f + lit, 1f + lit, 1f + lit * 2f);
				rim.Modulate = new Color(1f, 1f, 1f, 0.75f + 0.25f * Mathf.Sin(time * 40f + p.Y * 0.05f) + lit);
			}
			float o = Ease5.OutExpo(Ease5.Seg(time, 0.12f, 0.2f)) * (1f - Ease5.Seg(time, breakAt, 0.08f));
			outline.Scale = Vector2.One * Mathf.Max(o, 0.01f);
			bloom.Scale = new Vector2(7f, 12f) * o;
			outline.Modulate = new Color(1f, 1f, 1f, o * (0.7f + 0.3f * Mathf.Sin(time * 30f)));
		});
		Fx.After(root, 0.12, () =>
		{
			Glow.Ripple(layer, center, 360f, 0.5f, 0.03f, 1.6f);
			Glow.Shockwave(layer, center, new Color(0.95f, 0.06f, 0.1f), 260f, 0.4f, 1.7f);
			Fx.Shake(1);
		});
		Glow.Sparks(layer, center, 10, 15f, 50f, 0.8f, new[] { Pal5.Hot, Pal5.Bright }, -50f, 0.12f, 0.3f, Vector2.Up, 60f);
		Glow.Glint(layer, center + new Vector2(55f * facing, -125f), 0.9f, 0.3f);
		Glow.Glint(layer, center + new Vector2(-45f * facing, 110f), 0.7f, 0.42f);
		Glow.Glint(layer, center + new Vector2(70f * facing, 40f), 0.6f, 0.5f);
		Fx.After(root, breakAt, () =>
		{
			Glow.Flash(layer, center, new Color(1f, 0.55f, 0.5f), 260f, 0.2f);
			Glow.Shards(layer, center, 16, 1.0f, new Vector2(facing, -0.15f).Normalized(), 1.2f);
			Glow.Sparks(layer, center, 30, 150f, 520f, 0.55f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 180f, 0.1f, 0.3f,
				new Vector2(facing, -0.1f).Normalized(), 100f);
		});
	}

	private static Vector2[] Ellipse(float rx, float ry, int n)
	{
		var pts = new Vector2[n];
		for (int i = 0; i < n; i++)
		{
			float a = Mathf.Tau * i / n;
			pts[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
		}
		return pts;
	}

	/// <summary>尖顶六边形。</summary>
	private static Vector2[] Hex(float r)
	{
		var pts = new Vector2[6];
		for (int i = 0; i < 6; i++)
		{
			pts[i] = Vector2.Up.Rotated(Mathf.Tau * i / 6) * r;
		}
		return pts;
	}

	// ───────────────────────── 中和：预告函旋出 → 一刀斜斩 → 燃尽 ─────────────────────────

	private static Shader? _cardBurn;

	/// <summary>
	/// 被斩开的半张预告函（Polygon2D 贴图）：从切口往外烧——离切口 burn 以内的部分被噪声啃掉，
	/// 烧到的边沿是一圈白热→红的火口，火口前面一段先焦黑。cut_p / cut_n：切线上一点和法线（UV，按宽高比校正）。
	/// </summary>
	private static Shader CardBurn => _cardBurn ??= new Shader { Code = @"
shader_type canvas_item;
uniform float burn = 0.0;
uniform vec2 cut_p = vec2(0.5);
uniform vec2 cut_n = vec2(0.0, 1.0);
uniform float aspect = 0.6875;
uniform float seed = 0.0;
uniform float alpha = 1.0;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.1; a *= 0.5; }
	return v;
}
void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float d = abs(dot((UV - cut_p) * vec2(aspect, 1.0), cut_n));
	float n = fbm(UV * vec2(9.0 * aspect, 9.0) + seed);
	float front = burn - d - (n - 0.5) * 0.3;
	float gone = smoothstep(0.0, 0.01, front);
	float ember = smoothstep(-0.05, 0.0, front) * (1.0 - gone);
	float scorch = smoothstep(-0.14, -0.02, front);
	vec3 col = tex.rgb * (1.0 - 0.8 * scorch);
	col = mix(col, vec3(1.0, 0.1, 0.08), ember * 0.85);
	col += vec3(1.0, 0.85, 0.6) * pow(ember, 4.0);
	COLOR = vec4(col, tex.a * (1.0 - gone) * alpha);
}" };

	/// <summary>
	/// 中和（用户 2026-10-29 的设计）：敌人头顶，一张预告函从一个光点里转着旋出来（正反面交替翻，越转越慢、越转越大，
	/// 侧面对着镜头那一瞬间是一道白光），停在正面一亮；接着一刀斜斩下来——白闪、屏幕折射、火花顺着刀路喷，
	/// 预告函沿刀痕裂成两半：先卡顿一下，两半顺着切口错开、往两边飘，从切口开始烧：焦黑 → 火口 → 被啃掉，
	/// 一路掉火星和灰烬，慢慢烧没（消逝，不是直接消失）。head = 敌人头顶（GetTopOfHitbox），卡片悬在头顶上不挡身体。
	/// 斩预告函的就是砍敌人的那一刀（用户：不该分开）：一道长光刃从卡片上方一路劈到敌人身上，命中特效打在刀路穿过身体的地方；
	/// cutAt = 出牌后多久出刀（= 伤害结算的时刻，JokerFx.NeutralizeStart 把伤害推迟到这里），body = 敌人的 VfxSpawnPosition。
	/// </summary>
	public static void Neutralize(Control layer, Vector2 head, Vector2 body, float facing, float cutAt)
	{
		if (Fx.Tex("calling_card") is not Texture2D front)
		{
			return;
		}
		Texture2D back = Fx.Tex("calling_card_back") ?? front;
		Vector2 size = front.GetSize();
		float k = 170f / size.Y;
		Vector2 home = head + new Vector2(-40f * facing, -100f);               // 稍微偏后：一刀斜劈下来正好穿过敌人身体
		float slashT = cutAt, spinT = Mathf.Clamp(cutAt - 0.1f, 0.25f, 0.45f);

		// ── 旋出 ──
		Glow.Gather(layer, head + new Vector2(0f, -30f), Pal5.Bright, 220f, 0.16f);
		Node2D root = Fx.Root(layer, home, "P5Neutralize", slashT + 0.05);
		root.Rotation = -0.06f * facing;
		Sprite2D halo = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(Pal5.Bright, 0f) };
		root.AddChild(halo);
		Sprite2D card = Fx.Sprite(root, front, additive: false);
		Sprite2D shine = Fx.Sprite(root, front, additive: true);
		Polygon2D edge = Glow.TaperPoly(root, Pal5.Hot);
		Fx.Animate(root, 0, slashT, t =>
		{
			float time = t * slashT;
			float u = Ease5.Seg(time, 0f, spinT);
			float spin = Mathf.Tau * 2f * Fx.OutCubic(u);                 // 转两圈，越转越慢，停在正面
			float c = Mathf.Cos(spin);
			float grow = Ease5.OutBack(u);
			Texture2D face = c >= 0f ? front : back;
			card.Texture = face;
			shine.Texture = face;
			card.Scale = new Vector2(k * Mathf.Max(Mathf.Abs(c), 0.03f), k) * grow;
			shine.Scale = card.Scale;
			// 侧面那一瞬间：一道竖直白光；停稳时整张一亮再退
			float side = Mathf.Pow(1f - Mathf.Abs(c), 6f) * (1f - u * 0.5f);
			edge.Polygon = Glow.TaperPts(new Vector2(0f, -100f * grow), new Vector2(0f, 100f * grow), 12f * side + 0.1f);
			edge.Visible = side > 0.02f;
			float settle = Ease5.Seg(time, spinT - 0.04f, 0.04f) * (1f - Ease5.Seg(time, spinT, 0.12f));
			shine.Modulate = new Color(1f, 1f, 1f, 0.15f + 0.7f * settle + 0.5f * Ease5.Seg(time, slashT - 0.05f, 0.05f));
			halo.Scale = new Vector2(3.2f, 4.6f) * grow;
			halo.Modulate = new Color(Pal5.Bright, (0.45f + 0.15f * Mathf.Sin(time * 20f)) * Mathf.Min(1f, u * 3f));
			root.GlobalPosition = head.Lerp(home, Ease5.OutQuint(u)) + new Vector2(0f, -4f * Mathf.Sin(time * 6f));
			root.Rotation = (-0.06f + 0.25f * (1f - Ease5.OutQuint(u))) * facing;
		});
		Glow.Sparks(layer, head + new Vector2(0f, -40f), 16, 120f, 340f, 0.5f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 120f, 0.1f, 0.3f, Vector2.Up, 70f);
		Fx.After(layer, spinT - 0.03, () =>
		{
			Glow.Shockwave(layer, home, new Color(0.95f, 0.06f, 0.1f), 260f, 0.36f);
			Glow.Glint(layer, home + new Vector2(-52f, -78f), 0.8f);
			Glow.Glint(layer, home + new Vector2(54f, 74f), 0.6f, 0.05f);
		});

		// ── 斩 ──
		float rot = -0.06f * facing;
		Vector2 dir = new Vector2(facing * 0.42f, 1f).Normalized();               // 从上往下斜斩
		Vector2 gdir = dir.Rotated(rot), n = new Vector2(-dir.Y, dir.X);
		// 刀路：卡片上方 170px 起，一路劈到敌人身体下半截；穿过身体中心高度的那一点就是命中点
		float reach = (body.Y + 80f - home.Y) / gdir.Y;
		Vector2 hit = home + gdir * ((body.Y - home.Y) / gdir.Y);
		Fx.After(layer, slashT, () =>
		{
			float ang = gdir.Angle();
			Vector2 mid = home + gdir * (reach - 170f) / 2f;
			Glow.Blade(layer, mid, ang, reach + 170f, 40f, 0.32f);
			Glow.Blade(layer, mid + gdir.Orthogonal() * 5f, ang, (reach + 170f) * 0.8f, 12f, 0.26f, 0.02f, Pal5.Hot);
			Glow.Flash(layer, home, new Color(1f, 0.6f, 0.55f), 260f, 0.18f);
			Glow.Sparks(layer, home, 14, 200f, 500f, 0.4f, new[] { Pal5.Hot, Pal5.Gold, Pal5.Bright }, 200f, 0.1f, 0.28f, -gdir, 20f);
			Glow.Shards(layer, home, 5, 0.6f, gdir, 0.9f);
			HitFx.Impact(layer, hit, gdir, facing, 0.04f);
			for (int side = -1; side <= 1; side += 2)
			{
				Half(layer, front, k, home, rot, dir, n * side, side);
			}
			// 斩痕：刀过后切口上留一道细细的白热光，慢慢淡掉
			Node2D scar = Fx.Root(layer, home, "P5Scar", 0.4);
			Polygon2D line = Glow.TaperPoly(scar, Pal5.Hot);
			Polygon2D glow = Glow.TaperPoly(scar, new Color(Pal5.Bright, 0.8f));
			Fx.Animate(scar, 0, 0.32, t =>
			{
				float w = 1f - Fx.OutCubic(t);
				Vector2 a0 = -gdir * 150f, a1 = gdir * 150f;
				glow.Polygon = Glow.TaperPts(a0, a1, 14f * w);
				line.Polygon = Glow.TaperPts(a0 * 0.9f, a1 * 0.9f, 4f * w);
			});
		});
	}

	/// <summary>被斩开的半张：side 一侧的多边形（按切线裁卡片矩形），顿一下 → 顺切口滑开 → 往外飘 + 转 → 从切口烧尽，沿途掉火星。</summary>
	private static void Half(Control layer, Texture2D tex, float k, Vector2 at, float rot, Vector2 dir, Vector2 n, int side)
	{
		Vector2 size = tex.GetSize(), c = size / 2f;
		var uv = ClipHalf(size, c, n);
		var local = new Vector2[uv.Length];
		for (int i = 0; i < uv.Length; i++)
		{
			local[i] = (uv[i] - c) * k;
		}
		const float life = 1.15f;
		Node2D root = Fx.Root(layer, at, "P5CardHalf", life + 0.1);
		root.Rotation = rot;
		Polygon2D poly = new() { Polygon = local, UV = uv, Texture = tex, Material = new ShaderMaterial { Shader = CardBurn } };
		root.AddChild(poly);
		var mat = (ShaderMaterial)poly.Material;
		mat.SetShaderParameter("cut_n", n);                                       // UV × (宽高比, 1) 就是按卡高归一的像素空间，法线不用换
		mat.SetShaderParameter("aspect", size.X / size.Y);
		mat.SetShaderParameter("seed", Fx.Rand(0f, 20f));
		Vector2 gn = n.Rotated(rot), gd = dir.Rotated(rot);
		float slide = side > 0 ? 34f : -22f, spin = 0.3f * side, fall = side > 0 ? 50f : 14f;
		Fx.Animate(root, 0, life, t =>
		{
			float s = t * life;
			float slip = Ease5.OutQuint(Ease5.Seg(s, 0.08f, 0.3f));                   // 先顿 80ms，再顺着切口错开
			float drift = Ease5.OutQuint(Ease5.Seg(s, 0.12f, 0.95f));
			root.GlobalPosition = at + gd * slide * slip + gn * 64f * drift + new Vector2(0f, fall * s * s);
			root.Rotation = rot + spin * drift;
			mat.SetShaderParameter("burn", Burn(s));
			mat.SetShaderParameter("alpha", 1f - Ease5.Seg(s, 1.0f, 0.14f));
		});
		// 火星和灰烬：沿着往外推进的火口掉
		for (int i = 0; i < 10; i++)
		{
			float s = 0.16f + 0.085f * i;
			Fx.After(root, s, () =>
			{
				float burn = Burn(s);
				Vector2 p = c + dir * Fx.Rand(-0.45f, 0.45f) * size.Y * 0.6f + n * burn * size.Y;
				if (p.X < 0f || p.X > size.X || p.Y < 0f || p.Y > size.Y)
				{
					p = new Vector2(Mathf.Clamp(p.X, 0f, size.X), Mathf.Clamp(p.Y, 0f, size.Y));
				}
				Vector2 g = root.GlobalTransform * ((p - c) * k);
				Glow.Sparks(layer, g, 5, 30f, 120f, 0.7f, new[] { Pal5.Hot, Pal5.Gold, Pal5.Bright, Pal5.Deep }, -140f, 0.08f, 0.22f, Vector2.Up, 60f);
				Glow.Sparks(layer, g, 3, 20f, 70f, 0.9f, new[] { Pal5.Deep, Black }, -90f, 0.12f, 0.28f, Vector2.Up, 50f);
			});
		}
	}

	/// <summary>火口离切口多远（卡高的倍数）：切口先只是一条火线，0.2s 后越烧越快。</summary>
	private static float Burn(float s) => Mathf.Lerp(0.012f, 0.85f, Mathf.Pow(Ease5.Seg(s, 0.1f, 0.9f), 1.5f));

	/// <summary>卡片矩形（0,0)-size 里，切线（过 c、法线 n）n 那一侧的多边形。</summary>
	private static Vector2[] ClipHalf(Vector2 size, Vector2 c, Vector2 n)
	{
		Vector2[] rect = { Vector2.Zero, new(size.X, 0f), size, new(0f, size.Y) };
		var outPts = new List<Vector2>();
		for (int i = 0; i < 4; i++)
		{
			Vector2 a = rect[i], b = rect[(i + 1) % 4];
			float da = (a - c).Dot(n), db = (b - c).Dot(n);
			if (da >= 0f)
			{
				outPts.Add(a);
			}
			if (da * db < 0f)
			{
				outPts.Add(a.Lerp(b, da / (da - db)));
			}
		}
		return outPts.ToArray();
	}

	/// <summary>锁定准星：四个发光的角括号从大到小转着收拢，中间一圈蓄力环。</summary>
	private static void Lock(Control layer, Vector2 at)
	{
		Glow.Gather(layer, at, Pal5.Bright, 300f, 0.2f);
		Node2D root = Fx.Root(layer, at, "P5Lock", 0.5);
		var brackets = new List<Line2D>();
		for (int i = 0; i < 4; i++)
		{
			Line2D l = new() { Points = new[] { new Vector2(0f, 18f), Vector2.Zero, new Vector2(18f, 0f) }, Width = 4f,
				DefaultColor = i % 2 == 0 ? Pal5.Hot : Pal5.Bright, Material = Fx.Additive, Antialiased = true,
				JointMode = Line2D.LineJointMode.Sharp };
			root.AddChild(l);
			brackets.Add(l);
		}
		Fx.Animate(root, 0, 0.45, t =>
		{
			float time = t * 0.45f;
			float c = Ease5.OutExpo(Ease5.Seg(time, 0f, 0.2f));
			float d = Mathf.Lerp(150f, 62f, c);
			root.Rotation = Mathf.Lerp(-1.2f, 0f, c);
			for (int i = 0; i < 4; i++)
			{
				// 四个角：左上 / 右上 / 右下 / 左下，括号的开口朝里
				brackets[i].Position = Vector2.Right.Rotated(Mathf.Pi * 1.25f + Mathf.Pi / 2f * i) * d;
				brackets[i].Rotation = Mathf.Pi / 2f * i;
			}
			root.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(time, 0.24f, 0.2f));
		});
	}

	// ───────────────────────── 减益：锁定 + 下箭头 ─────────────────────────

	/// <summary>
	/// 减益（中和、扫腿、刺耳尖啸……施加虚弱 / 易伤）：P5R 的"↓"。
	/// 锁定 —— 四个发光角括号转着收拢到敌人身上；
	/// 压下 —— 锁定那一刻白闪，三层发光的红色"V"字箭头从敌人头顶一层接一层往下砸（越往下越大、越淡），
	///         脚下一圈暗红光波压扁扩散，红色光粒往下坠。at = 敌人的 VfxSpawnPosition。
	/// </summary>
	public static void Debuff(Control layer, Vector2 at)
	{
		Lock(layer, at);
		const float lockT = 0.2f;
		Fx.After(layer, lockT, () =>
		{
			Glow.Flash(layer, at, new Color(1f, 0.5f, 0.45f), 240f, 0.18f);
			Fx.Shake(1);
		});
		for (int i = 0; i < 3; i++)
		{
			float d = lockT + 0.07f * i;
			Chevron(layer, at + new Vector2(0f, -110f + 10f * i), at + new Vector2(0f, 20f + 30f * i), 70f + 18f * i, false, d);
		}
		Fx.After(layer, lockT + 0.2, () =>
		{
			Glow.Shockwave(layer, at + new Vector2(0f, 110f), new Color(0.65f, 0.02f, 0.06f), 360f, 0.55f, 0.3f);
			Glow.Sparks(layer, at, 20, 40f, 140f, 0.8f, new[] { Pal5.Bright, Pal5.Deep }, 300f, 0.12f, 0.3f, Vector2.Down, 60f);
		});
	}

	/// <summary>
	/// 一个发光的"V"字箭头（down = true 朝下的"V"、false 朝上的"Λ"……这里 up 参数为 true 时箭头朝上）：
	/// 从 from 飞到 to，过程中先弹出（OutBack）再淡掉。w = 箭头宽度。
	/// </summary>
	private static void Chevron(Control layer, Vector2 from, Vector2 to, float w, bool up, float delay)
	{
		Node2D root = Fx.Root(layer, from, "P5Chevron", delay + 0.6);
		float s = up ? -1f : 1f;                     // 朝下的 V：尖在下面
		Vector2[] shape =
		{
			new(-w / 2f, -w * 0.32f * s), new(0f, w * 0.18f * s), new(w / 2f, -w * 0.32f * s),
			new(w / 2f, -w * 0.08f * s), new(0f, w * 0.42f * s), new(-w / 2f, -w * 0.08f * s),
		};
		Polygon2D glow = Fx.Poly(root, Grow(shape, 1.35f), new Color(Pal5.Bright, 0.7f), additive: true);
		Polygon2D core = Fx.Poly(root, shape, Pal5.Hot, additive: true);
		Polygon2D red = Fx.Poly(root, Grow(shape, 0.7f), new Color(Pal5.Crimson, 0.9f), additive: true);
		root.Visible = false;
		Fx.Animate(root, delay, 0.5, t =>
		{
			root.Visible = true;
			float m = Ease5.OutExpo(Mathf.Min(1f, t * 2.2f));
			root.GlobalPosition = from.Lerp(to, m);
			root.Scale = Vector2.One * Ease5.OutBack(Mathf.Min(1f, t * 3f));
			float a = 1f - Fx.InCubic(Ease5.Seg(t, 0.4f, 0.6f));
			root.Modulate = new Color(1f, 1f, 1f, a);
			core.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(t, 0.1f, 0.4f) * 0.6f);
		});
	}

	/// <summary>多边形绕中心缩放（做光晕层）。</summary>
	private static Vector2[] Grow(Vector2[] pts, float k)
	{
		Vector2 c = Vector2.Zero;
		foreach (Vector2 p in pts)
		{
			c += p;
		}
		c /= pts.Length;
		var o = new Vector2[pts.Length];
		for (int i = 0; i < pts.Length; i++)
		{
			o[i] = c + (pts[i] - c) * k;
		}
		return o;
	}

	// ───────────────────────── 增益：上箭头 + 光柱 ─────────────────────────

	/// <summary>
	/// 增益（能力牌）：P5R 的"↑"。脚下一圈红光收拢 → 扁的光环扩散 + 一道从脚下升起的红色光柱（白芯），
	/// 一串发光的"Λ"箭头从脚往头一个接一个窜上去，光粒上浮，头顶星光。feet / head = Joker 的脚底 / 头顶；
	/// 光柱画在人物后面（back 层），不盖住人。
	/// </summary>
	public static void Buff(Control layer, Control back, Vector2 feet, Vector2 head)
	{
		Glow.Gather(layer, feet, Pal5.Bright, 320f, 0.12f, 0.3f);
		Fx.After(layer, 0.12, () =>
		{
			Glow.Shockwave(layer, feet, new Color(0.95f, 0.06f, 0.1f), 420f, 0.55f, 0.28f);
			Glow.Ripple(layer, feet, 360f, 0.5f, 0.025f, 0.35f);
			Pillar(back, feet, head);
			Fx.Shake(1);
		});
		float h = feet.Y - head.Y;
		for (int i = 0; i < 4; i++)
		{
			float x = (i % 2 == 0 ? -1f : 1f) * (30f + 25f * (i / 2));
			Vector2 from = feet + new Vector2(x, -h * 0.15f), to = feet + new Vector2(x, -h * (0.75f + 0.12f * (i % 2)));
			Chevron(layer, from, to, 46f, true, 0.16f + 0.06f * i);
		}
		Glow.Sparks(layer, feet + new Vector2(0f, -20f), 26, 60f, 260f, 0.9f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, -140f, 0.1f, 0.3f,
			Vector2.Up, 25f);
		Glow.Glint(layer, head + new Vector2(30f, -10f), 0.9f, 0.4f);
	}

	/// <summary>竖直光柱：一根柔光贴图拉成细长条，0.1s 窜到头顶、停一下、从下往上收掉。</summary>
	private static void Pillar(Control layer, Vector2 feet, Vector2 head)
	{
		float h = feet.Y - head.Y + 80f;
		Node2D root = Fx.Root(layer, feet, "P5Pillar", 0.8);
		Sprite2D glow = new() { Texture = Glow.Soft, Material = Fx.Additive, Centered = true, Modulate = new Color(Pal5.Bright, 0.75f) };
		Sprite2D core = new() { Texture = Glow.Soft, Material = Fx.Additive, Centered = true, Modulate = Pal5.Hot };
		root.AddChild(glow);
		root.AddChild(core);
		Fx.Animate(root, 0, 0.7, t =>
		{
			float up = Ease5.OutExpo(Mathf.Min(1f, t * 5f)), off = Fx.InCubic(Ease5.Seg(t, 0.45f, 0.55f));
			float len = h * up * (1f - off);
			glow.Scale = new Vector2(5.5f * (1f - 0.5f * t), len / 32f * 1.2f);
			core.Scale = new Vector2(1.2f * (1f - 0.6f * t), len / 32f);
			glow.Position = core.Position = new Vector2(0f, -h * up + len / 2f);
		});
	}

	// ───────────────────────── 咒（Eiha）：黑红咒焰 ─────────────────────────

	private static readonly Color Black = new(0.03f, 0.0f, 0.02f);

	/// <summary>
	/// 咒（原本给毒牌的特效，用户：改成亚森那种咒属性，不一定体现毒；不要粉）：P5R Eiha 式的黑红咒焰。
	/// 预备 —— 敌人脚下一滩黑色咒印张开，边上一圈暗红光；中心一颗黑球慢慢胀大（暗红描边、里面隐约的红光）；
	/// 爆发 —— 一圈黑底红边的咒焰从咒印里蹿起来（着色器实时生成，噪声翻卷往上流），黑球往里一缩再炸开、中间三根高火冲上去：
	///         暗红闪光 + 折射波 + 黑色碎片 + 红色火星往上冒；
	/// 余韵 —— 咒焰越烧越细、淡掉，咒印收拢。at = 敌人的 VfxSpawnPosition。
	/// </summary>
	public static void Curse(Control layer, Vector2 at)
	{
		Vector2 ground = at + new Vector2(0f, 95f);

		// 咒印：黑色扁椭圆 + 暗红光环
		Node2D seal = Fx.Root(layer, ground, "P5CurseSeal", 1.3);
		Polygon2D pool = Fx.Poly(seal, Ellipse(130f, 30f, 40), new Color(Black, 0.85f));
		Line2D rim = new() { Points = Ellipse(130f, 30f, 40), Closed = true, Width = 4f, DefaultColor = Pal5.Crimson, Material = Fx.Additive, Antialiased = true };
		seal.AddChild(rim);
		Line2D rim2 = new() { Points = Ellipse(100f, 22f, 40), Closed = true, Width = 2f, DefaultColor = new Color(Pal5.Bright, 0.6f), Material = Fx.Additive, Antialiased = true };
		seal.AddChild(rim2);
		Fx.Animate(seal, 0, 1.2, t =>
		{
			float time = t * 1.2f;
			float o = Ease5.OutExpo(Ease5.Seg(time, 0f, 0.18f)) * (1f - Fx.InCubic(Ease5.Seg(time, 0.95f, 0.25f)));
			seal.Scale = Vector2.One * Mathf.Max(o, 0.01f);
			rim2.Scale = new Vector2(1f + 0.06f * Mathf.Sin(time * 9f), 1f);
			rim.Modulate = new Color(1f, 1f, 1f, 0.7f + 0.3f * Mathf.Sin(time * 25f));
		});

		// 黑球：中心慢慢胀大，0.42s 往里一缩，0.48s 炸开
		Node2D orb = Fx.Root(layer, at, "P5CurseOrb", 0.55);
		Sprite2D inner = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(Pal5.Crimson, 0.8f) };
		orb.AddChild(inner);
		Polygon2D body = Fx.Poly(orb, Fx.Circle(40f, 32), new Color(Black, 0.92f));
		Line2D edge = new() { Points = Fx.Circle(40f, 32), Closed = true, Width = 3f, DefaultColor = Pal5.Bright, Material = Fx.Additive, Antialiased = true };
		orb.AddChild(edge);
		Sprite2D core = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(Pal5.Bright, 0.7f), Scale = Vector2.One * 1.2f };
		orb.AddChild(core);
		Fx.Animate(orb, 0, 0.5, t =>
		{
			float time = t * 0.5f;
			float grow = Ease5.OutQuint(Ease5.Seg(time, 0.05f, 0.35f)), pinch = Fx.InCubic(Ease5.Seg(time, 0.4f, 0.08f));
			float k = Mathf.Max(0.01f, grow * (1f - 0.75f * pinch));
			orb.Scale = Vector2.One * k;
			inner.Scale = Vector2.One * (5f + Mathf.Sin(time * 30f));
			core.Modulate = new Color(Pal5.Bright, 0.4f + 0.5f * Mathf.Abs(Mathf.Sin(time * 18f)));
		});

		// 咒焰（着色器实时生成，Glow.CurseFire）：先在咒印边上蹿起几根矮的，黑球炸开时中间三根高的冲上去
		Node2D glowRoot = Fx.Root(layer, ground, "P5CurseGlow", 1.3);
		Sprite2D under = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(Pal5.Crimson, 0f), Position = new Vector2(0f, -120f) };
		glowRoot.AddChild(under);
		Fx.Animate(glowRoot, 0, 1.2, t =>
		{
			float k = Ease5.Seg(t, 0.05f, 0.3f) * (1f - Ease5.Seg(t, 0.7f, 0.3f));
			under.Scale = new Vector2(9f, 11f) * (0.6f + 0.4f * k);
			under.Modulate = new Color(Pal5.Crimson, 0.3f * k);
		});
		for (int i = 0; i < 4; i++)
		{
			float x = (i % 2 == 0 ? -1f : 1f) * (i < 2 ? 105f : 70f) + Fx.Rand(-8f, 8f);
			Glow.CurseFire(layer, ground + new Vector2(x, Fx.Rand(-6f, 8f)), 120f, Fx.Rand(160f, 220f), 0.08f + 0.03f * i, 0.95f, 0.85f);
		}
		// 高火放在两侧，中间留空：不把敌人整个挡住（用户 2026-10-29）
		Glow.CurseFire(layer, ground + new Vector2(-118f, 4f), 170f, 340f, 0.44f, 0.8f);
		Glow.CurseFire(layer, ground + new Vector2(122f, 2f), 170f, 310f, 0.47f, 0.8f);

		Fx.After(layer, 0.48, () =>
		{
			Glow.Flash(layer, at, new Color(0.9f, 0.05f, 0.08f), 360f, 0.3f);
			Glow.Ripple(layer, at, 420f, 0.55f, 0.04f);
			Glow.Shockwave(layer, at, new Color(0.75f, 0.02f, 0.05f), 340f, 0.45f, 0.8f);
			Glow.Sparks(layer, at, 30, 90f, 360f, 0.9f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, -180f, 0.12f, 0.36f, Vector2.Up, 80f);
			DarkShards(layer, at, 12);
			Fx.Shake(2);
		});
	}

	/// <summary>黑色碎片（不发光，带一道暗红边），往外崩开落下。</summary>
	private static void DarkShards(Control layer, Vector2 at, int count)
	{
		for (int i = 0; i < count; i++)
		{
			Node2D sh = Fx.Root(layer, at, "P5DarkShard", 0.8);
			float len = Fx.Rand(10f, 22f), wid = len * 0.45f;
			Vector2[] shape = { new(-len * 0.5f, 0f), new(0f, -wid * 0.5f), new(len * 0.5f, 0f), new(0f, wid * 0.5f) };
			Fx.Poly(sh, shape, Black);
			Line2D e = new() { Points = shape, Closed = true, Width = 1.6f, DefaultColor = Pal5.Bright, Material = Fx.Additive, Antialiased = true };
			sh.AddChild(e);
			Vector2 dir = Vector2.Up.Rotated(Fx.Rand(-1.6f, 1.6f));
			float speed = Fx.Rand(250f, 600f), spin = Fx.Rand(-12f, 12f);
			Fx.Animate(sh, 0, 0.7, t =>
			{
				float s = t * 0.7f, travel = speed * (1f - Mathf.Exp(-4.5f * s)) / 4.5f;
				sh.GlobalPosition = at + dir * travel + new Vector2(0f, 300f * s * s);
				sh.Rotation = spin * s;
				sh.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t));
			});
		}
	}

	// ───────────────────────── 抽牌：卡片划着光轨飞进手里 ─────────────────────────

	/// <summary>
	/// 抽牌（杂技、后空翻、预备……）：身后上方一张接一张黑底红边的卡片翻着跟头、拖着红白光轨划弧线飞进举起的手套里，
	/// 每接住一张手里"啪"地一亮（星光 + 小闪 + 几颗火花），最后一张接住时手边一圈光环。hand = 手套（姿势图 cast_glove 的 989,400）。
	/// </summary>
	public static void Draw(Control layer, Vector2 hand, int count, float facing = 1f)
	{
		count = Mathf.Clamp(count, 1, 5);
		for (int i = 0; i < count; i++)
		{
			float delay = 0.07f * i;
			Vector2 from = hand + new Vector2((-240f - 70f * i) * facing, -230f + 60f * i + Fx.Rand(-20f, 20f));
			Vector2 ctrl = hand + new Vector2((-60f - 40f * i) * facing, -360f + 40f * i);
			FlyCard(layer, from, ctrl, hand, delay, 0.3f, facing);
			bool last = i == count - 1;
			Fx.After(layer, delay + 0.3f, () =>
			{
				Glow.Glint(layer, hand + new Vector2(Fx.Rand(-10f, 10f), Fx.Rand(-12f, 6f)), last ? 1f : 0.65f);
				Glow.Flash(layer, hand, new Color(1f, 0.55f, 0.5f), last ? 200f : 120f, 0.14f);
				Glow.Sparks(layer, hand, 8, 120f, 360f, 0.35f, new[] { Pal5.Hot, Pal5.Bright }, 260f, 0.08f, 0.22f,
					new Vector2(facing, -0.4f).Normalized(), 60f);
				if (last)
				{
					Glow.Shockwave(layer, hand, new Color(0.95f, 0.06f, 0.1f), 220f, 0.35f);
				}
			});
		}
	}

	/// <summary>一张飞行的卡片：沿二次贝塞尔从 from 经 ctrl 到 to，翻着跟头、越飞越小，身后一条红白光轨。</summary>
	private static void FlyCard(Control layer, Vector2 from, Vector2 ctrl, Vector2 to, float delay, float dur, float facing)
	{
		Node2D trailRoot = Fx.Root(layer, Vector2.Zero, "P5CardTrail", delay + dur + 0.3);
		trailRoot.GlobalPosition = Vector2.Zero;
		Polygon2D trailGlow = Glow.TaperPoly(trailRoot, new Color(Pal5.Bright, 0.75f));
		Polygon2D trailCore = Glow.TaperPoly(trailRoot, Pal5.Hot);
		Node2D card = Fx.Root(layer, from, "P5FlyCard", delay + dur + 0.05);
		Vector2[] rect = { new(-22f, -32f), new(22f, -32f), new(22f, 32f), new(-22f, 32f) };
		Fx.Poly(card, rect, new Color(Black, 0.95f));
		Fx.Poly(card, new Vector2[] { new(-22f, 4f), new(22f, -14f), new(22f, -4f), new(-22f, 14f) }, Pal5.Crimson);
		Line2D rim = new() { Points = rect, Closed = true, Width = 3f, DefaultColor = Pal5.Hot, Material = Fx.Additive, Antialiased = true };
		card.AddChild(rim);
		Sprite2D halo = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(Pal5.Bright, 0.5f), Scale = new Vector2(1.6f, 2f) };
		card.AddChild(halo);
		card.Visible = trailRoot.Visible = false;
		Vector2 Bez(float u) => from.Lerp(ctrl, u).Lerp(ctrl.Lerp(to, u), u);
		Fx.Animate(card, delay, dur, t =>
		{
			card.Visible = trailRoot.Visible = true;
			float u = Fx.InCubic(t) * 0.35f + t * 0.65f;                         // 越飞越快
			card.GlobalPosition = Bez(u);
			card.Rotation = -facing * Mathf.Tau * 1.5f * u;
			card.Scale = new Vector2(Mathf.Cos(u * Mathf.Pi * 3f), 1f) * Mathf.Lerp(1.15f, 0.45f, u);
			card.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 6f));
			float tail = Mathf.Max(0f, u - 0.35f);
			var pts = new List<Vector2>();
			for (int j = 0; j <= 8; j++)
			{
				pts.Add(Bez(Mathf.Lerp(tail, u, j / 8f)));
			}
			trailGlow.Polygon = Ribbon(pts, 16f);
			trailCore.Polygon = Ribbon(pts, 5f);
		});
		Fx.Animate(trailRoot, delay + dur, 0.2, t => trailRoot.Modulate = new Color(1f, 1f, 1f, 1f - t));
	}

	/// <summary>沿折线的带子：尾巴是尖、头部最宽 w。</summary>
	private static Vector2[] Ribbon(List<Vector2> pts, float w)
	{
		int n = pts.Count;
		var outPts = new Vector2[n * 2];
		for (int i = 0; i < n; i++)
		{
			Vector2 d = (pts[Mathf.Min(i + 1, n - 1)] - pts[Mathf.Max(i - 1, 0)]).Normalized();
			Vector2 nn = new Vector2(-d.Y, d.X) * w * 0.5f * Mathf.Pow((float)i / (n - 1), 1.3f);
			outPts[i] = pts[i] + nn;
			outPts[n * 2 - 1 - i] = pts[i] - nn;
		}
		return outPts;
	}

	// ───────────────────────── 生成小刀：光刃阵 ─────────────────────────

	/// <summary>
	/// 生成小刀（刀刃之舞、斗篷与匕首、暗藏匕首……）：Joker 身后上方张开一道扇形，一把把发光的红色小刀从一道竖直光缝里"抽"出来，
	/// 刀尖齐刷刷转向敌人那边、一亮，然后一把接一把甩进手套里（拖着光轨），每把接住一下火花。center = 胸口，hand = 手套。
	/// </summary>
	public static void Shivs(Control layer, Vector2 center, Vector2 hand, int count, float facing = 1f)
	{
		count = Mathf.Clamp(count, 1, 6);
		for (int i = 0; i < count; i++)
		{
			float a = Mathf.Lerp(-2.5f, -0.65f, count == 1 ? 0.5f : (float)i / (count - 1));
			if (facing < 0f)
			{
				a = -Mathf.Pi - a;
			}
			Vector2 home = center + Vector2.Right.Rotated(a) * new Vector2(190f, 170f) + new Vector2(-40f * facing, -40f);
			float appear = 0.05f * i, launch = 0.42f + 0.05f * i;
			Blade(layer, home, hand, appear, launch, facing);
		}
		Glow.Gather(layer, center + new Vector2(-40f * facing, -120f), Pal5.Bright, 360f, 0.16f);
		Fx.After(layer, 0.36, () => Glow.Ripple(layer, center + new Vector2(-40f * facing, -120f), 380f, 0.4f, 0.02f));
	}

	/// <summary>一把光刃小刀：光缝里抽出 → 刀尖转向前方、一亮 → 甩进手里。</summary>
	private static void Blade(Control layer, Vector2 home, Vector2 hand, float appear, float launch, float facing)
	{
		const float fly = 0.16f;
		Node2D root = Fx.Root(layer, home, "P5ShivBlade", launch + fly + 0.05);
		Vector2[] shape = { new(-30f, 0f), new(-22f, -5f), new(30f, -2f), new(42f, 0f), new(30f, 2f), new(-22f, 5f) };
		Vector2[] grip = { new(-46f, -3f), new(-30f, -3f), new(-30f, 3f), new(-46f, 3f) };
		Sprite2D halo = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = new Color(Pal5.Bright, 0.55f), Scale = new Vector2(2.2f, 0.9f) };
		root.AddChild(halo);
		Fx.Poly(root, grip, new Color(Black, 0.95f));
		Polygon2D body = Fx.Poly(root, shape, new Color(Pal5.Bright, 0.85f), additive: true);
		Line2D rim = new() { Points = shape, Closed = true, Width = 2f, DefaultColor = Pal5.Hot, Material = Fx.Additive, Antialiased = true };
		root.AddChild(rim);
		Polygon2D slit = Glow.TaperPoly(layer, Pal5.Hot);
		slit.GlobalPosition = home;
		Fx.FreeAfter(slit, appear + 0.25);
		root.Visible = false;
		slit.Visible = false;
		float aim = facing > 0f ? -0.25f : Mathf.Pi + 0.25f;
		Fx.Animate(root, appear, launch + fly - appear, t =>
		{
			float s = appear + t * (launch + fly - appear);
			root.Visible = true;
			float open = Ease5.OutBack(Ease5.Seg(s, appear + 0.04f, 0.14f));
			float line = Mathf.Clamp(1f - Ease5.Seg(s, appear + 0.06f, 0.1f), 0f, 1f) * Ease5.Seg(s, appear, 0.04f);
			if (GodotObject.IsInstanceValid(slit))
			{
				slit.Visible = line > 0.02f;
				slit.Polygon = Glow.TaperPts(new Vector2(0f, -60f), new Vector2(0f, 60f), 10f * line + 0.1f);
			}
			float turn = Ease5.OutBack(Ease5.Seg(s, appear + 0.12f, 0.2f));
			float k = Ease5.Seg(s, launch, fly);
			root.GlobalPosition = k > 0f ? home.Lerp(hand, Fx.InCubic(k)) : home + new Vector2(0f, -4f * Mathf.Sin(s * 12f));
			float flyAng = (hand - home).Angle();
			root.Rotation = Mathf.LerpAngle(Mathf.Lerp(-Mathf.Pi / 2f, aim, turn), flyAng, Ease5.OutExpo(k));
			root.Scale = new Vector2(open, Mathf.Max(open, 0.01f)) * Mathf.Lerp(1.4f, 0.6f, k);
			float flash = Mathf.Pow(Mathf.Max(0f, 1f - Mathf.Abs(s - (appear + 0.3f)) / 0.06f), 2f);
			body.Modulate = new Color(1f + flash, 1f + flash, 1f + flash, 1f);
			halo.Modulate = new Color(Pal5.Bright, 0.45f + 0.5f * flash);
		});
		Fx.After(layer, launch, () => Glow.Blade(layer, home.Lerp(hand, 0.5f), (hand - home).Angle(), home.DistanceTo(hand), 14f, 0.2f, 0.04f));
		Fx.After(layer, launch + fly, () =>
		{
			Glow.Sparks(layer, hand, 6, 100f, 320f, 0.3f, new[] { Pal5.Hot, Pal5.Bright }, 240f, 0.08f, 0.2f);
			Glow.Glint(layer, hand, 0.6f);
		});
	}

	// ───────────────────────── 能量：觉醒星爆 ─────────────────────────

	/// <summary>
	/// 能量 / 爆发（肾上腺素、子弹时间、爆发）：Joker 身后"砰"地炸开一个黑底红边的 P5 星爆（尖角放射形，两层错开转），
	/// 一圈红白速度线往外喷，脚下扁光环 + 折射，面具位置一颗星光，红金火花往上冒。back = 人物身后那层，feet / chest / head 为身上的点。
	/// 只在 Joker 身上，不碰敌人。
	/// </summary>
	public static void Surge(Control layer, Control back, Vector2 feet, Vector2 chest, Vector2 head)
	{
		Glow.Gather(layer, chest, Pal5.Bright, 300f, 0.12f);
		Fx.After(layer, 0.12, () =>
		{
			Glow.Flash(layer, chest, new Color(1f, 0.5f, 0.45f), 380f, 0.22f);
			Glow.SpeedLines(back, chest, Pal5.Bright, 20, 140f, 420f, 0.34f);
			Glow.Shockwave(layer, feet, new Color(0.95f, 0.06f, 0.1f), 460f, 0.5f, 0.28f);
			Glow.Ripple(layer, chest, 520f, 0.5f, 0.03f);
			Glow.Glint(layer, head + new Vector2(14f, 40f), 1.1f, 0.05f);
			Glow.Sparks(layer, feet, 30, 120f, 420f, 0.8f, new[] { Pal5.Hot, Pal5.Gold, Pal5.Bright, Pal5.Deep }, -200f, 0.1f, 0.32f, Vector2.Up, 50f);
			Fx.Shake(1);
		});
		for (int layerI = 0; layerI < 2; layerI++)
		{
			bool outer = layerI == 0;
			Node2D root = Fx.Root(back, chest, "P5Surge", 0.8);
			Vector2[] star = Fx.Burst(outer ? 230f : 165f, outer ? 140f : 100f, outer ? 14 : 11, 0.3f);
			// 外层：半透的黑底（压暗身后、衬出人物）；内层：加法的红光面，不是实心贴片
			Polygon2D fill = Fx.Poly(root, star, outer ? new Color(Black, 0.45f) : new Color(Pal5.Bright, 0.28f), additive: !outer);
			Line2D rim = new() { Points = star, Closed = true, Width = outer ? 5f : 3f, DefaultColor = outer ? Pal5.Bright : Pal5.Hot, Material = Fx.Additive,
				Antialiased = true, JointMode = Line2D.LineJointMode.Sharp };
			root.AddChild(rim);
			root.Visible = false;
			float spin = outer ? 0.5f : -0.8f;
			Fx.Animate(root, 0.12 + (outer ? 0 : 0.04), 0.6, t =>
			{
				root.Visible = true;
				float pop = Ease5.OutBack(Ease5.Seg(t, 0f, 0.25f));
				root.Scale = Vector2.One * Mathf.Max(0.01f, pop * (1f + 0.25f * t));
				root.Rotation = spin * t;
				rim.Width = (outer ? 5f : 3f) * (1f - 0.6f * t);
				root.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(Ease5.Seg(t, 0.35f, 0.65f)));
			});
		}
	}
}
