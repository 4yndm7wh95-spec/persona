using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 技能牌的大特效（用户 2026-10-29 选的方案）：
///  · Adrenaline（肾上腺素）—— 眼神切入撕碎的一瞬间，Joker 身上爆出热浪折射，脚下地面裂开发光的红色裂纹，裂缝里往上冒火星；
///  · BulletTime（子弹时间）—— 一圈火星从 Joker 身上炸出去、飞到一半全场时停：画面变黑白（只留红色），火星定格在空中闪，
///    身后一个表盘指针"咔"地停住；然后颜色从 Joker 这边开始一块块碎回来，火星化成光点散掉；
///  · Burst（爆发）—— Joker 身后"刷"地分出一个纯黑剪影 + 红描边的分身，跟着本体的动作，停一下后被红色速度线扯走。
/// 尽量用着色器实时算（时停滤镜、表盘、剪影），不用大面积贴图。
/// </summary>
public static class SkillFx
{
	// ───────────────────────── 肾上腺素 ─────────────────────────

	public static void Adrenaline(Control layer, Control back, Vector2 feet, Vector2 chest, float facing = 1f)
	{
		CutIn.Eyes(back, Fx.Screen(layer), 0.5f, () =>
		{
			Glow.Flash(layer, chest, new Color(1f, 0.5f, 0.45f), 360f, 0.2f);
			Glow.Ripple(layer, chest, 760f, 0.6f, 0.05f);
			Glow.Ripple(layer, feet, 620f, 0.55f, 0.04f, 0.3f, 0.06f);
			Glow.Shockwave(layer, feet, new Color(0.95f, 0.06f, 0.1f), 480f, 0.5f, 0.26f);
			Cracks(layer, feet, 9);
			Glow.Sparks(layer, chest + new Vector2(0f, 60f), 26, 100f, 380f, 0.9f, new[] { Pal5.Hot, Pal5.Gold, Pal5.Bright, Pal5.Deep }, -260f, 0.1f, 0.3f,
				Vector2.Up, 55f);
			Fx.Shake(2);
		});
	}

	/// <summary>地面裂纹：从脚下往外裂开的折线（压扁成地面透视），先黑色裂口、上面一层红白发光，一下裂开、亮一会、慢慢暗掉，沿缝冒火星。</summary>
	public static void Cracks(Control layer, Vector2 feet, int count)
	{
		Node2D root = Fx.Root(layer, feet, "P5Cracks", 1.4);
		var lines = new List<(Line2D Dark, Line2D Glow, Line2D Core, Vector2[] Pts, float Delay)>();
		for (int i = 0; i < count; i++)
		{
			float a = Mathf.Tau * i / count + Fx.Rand(-0.25f, 0.25f);
			Branch(Vector2.Zero, a, 6, Fx.Rand(40f, 58f), 0f);
		}

		void Branch(Vector2 from, float a, int segs, float len, float delay)
		{
			var pts = new List<Vector2> { from };
			Vector2 p = from;
			for (int s = 0; s < segs; s++)
			{
				a += Fx.Rand(-0.5f, 0.5f);
				p += new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.28f) * len * Fx.Rand(0.7f, 1.2f);
				pts.Add(p);
				if (segs > 3 && s == 2 && Fx.Rng.NextDouble() < 0.6)
				{
					Branch(p, a + Fx.Rand(-0.9f, 0.9f), 3, len * 0.7f, delay + 0.04f);
				}
			}
			Vector2[] arr = pts.ToArray();
			Line2D dark = new() { Width = 5f, DefaultColor = new Color(0.04f, 0f, 0.01f, 0.9f), Antialiased = true, JointMode = Line2D.LineJointMode.Sharp };
			Line2D glow = new() { Width = 9f, DefaultColor = new Color(Pal5.Bright, 0.8f), Material = Fx.Additive, Antialiased = true, JointMode = Line2D.LineJointMode.Sharp };
			Line2D core = new() { Width = 2.2f, DefaultColor = Pal5.Hot, Material = Fx.Additive, Antialiased = true, JointMode = Line2D.LineJointMode.Sharp };
			root.AddChild(dark);
			root.AddChild(glow);
			root.AddChild(core);
			lines.Add((dark, glow, core, arr, delay));
		}

		Fx.Animate(root, 0, 1.3, t =>
		{
			float time = t * 1.3f;
			foreach (var (dark, glow, core, pts, delay) in lines)
			{
				float grow = Ease5.OutExpo(Ease5.Seg(time, delay, 0.14f));
				Vector2[] shown = Partial(pts, grow);
				dark.Points = shown;
				glow.Points = shown;
				core.Points = shown;
				float heat = 1f - Ease5.Seg(time, 0.35f, 0.8f);
				float flicker = 0.85f + 0.15f * Mathf.Sin(time * 40f + delay * 50f);
				glow.Modulate = new Color(1f, 1f, 1f, heat * flicker);
				core.Modulate = new Color(1f, 1f, 1f, heat * heat);
				dark.Modulate = new Color(1f, 1f, 1f, 1f - Ease5.Seg(time, 0.9f, 0.4f));
			}
		});
		// 沿缝冒火星
		for (int i = 0; i < 6; i++)
		{
			int li = i * lines.Count / 6;
			Vector2[] pts = lines[li].Pts;
			Fx.After(root, 0.1 + 0.07 * i, () => Glow.Sparks(layer, feet + pts[^1] * Fx.Rand(0.4f, 1f), 6, 30f, 140f, 0.8f,
				new[] { Pal5.Hot, Pal5.Gold, Pal5.Bright, Pal5.Deep }, -180f, 0.08f, 0.24f, Vector2.Up, 30f));
		}
	}

	private static Vector2[] Partial(Vector2[] pts, float k)
	{
		if (k >= 1f)
		{
			return pts;
		}
		float total = 0f;
		for (int i = 1; i < pts.Length; i++)
		{
			total += pts[i].DistanceTo(pts[i - 1]);
		}
		float want = total * k;
		var outPts = new List<Vector2> { pts[0] };
		for (int i = 1; i < pts.Length && want > 0f; i++)
		{
			float d = pts[i].DistanceTo(pts[i - 1]);
			outPts.Add(d <= want ? pts[i] : pts[i - 1].Lerp(pts[i], want / d));
			want -= d;
		}
		if (outPts.Count < 2)
		{
			outPts.Add(pts[0] + new Vector2(0.1f, 0f));
		}
		return outPts.ToArray();
	}

	// ───────────────────────── 子弹时间 ─────────────────────────

	private static Shader? _timeStop, _dial;

	/// <summary>全屏时停滤镜：amount 0~1 变黑白（红色保留、对比加强、四周压暗）；back 0~1 颜色按 Voronoi 碎块从 center 往外一块块回来，碎块边缘一闪。</summary>
	private static Shader TimeStopShader => _timeStop ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float amount = 0.0;
uniform float back = 0.0;
uniform vec2 center = vec2(0.3, 0.6);
vec2 hash2(vec2 p) { return fract(sin(vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)))) * 43758.5453); }
void fragment() {
	vec3 c = texture(screen_tex, SCREEN_UV).rgb;
	float g = dot(c, vec3(0.299, 0.587, 0.114));
	float red = smoothstep(0.12, 0.35, c.r - max(c.g, c.b));
	vec3 bw = clamp((vec3(g) - 0.5) * 1.12 + 0.56, 0.0, 1.0);
	vec3 stopped = mix(bw, c * vec3(1.15, 0.85, 0.85), red);
	vec2 sc = vec2(14.0, 8.0);
	vec2 p = SCREEN_UV * sc, ip = floor(p), fp = fract(p);
	float d1 = 9.0, d2 = 9.0;
	vec2 cell = vec2(0.0);
	for (int y = -1; y <= 1; y++) {
		for (int x = -1; x <= 1; x++) {
			vec2 o = vec2(float(x), float(y));
			vec2 r = o + hash2(ip + o) - fp;
			float d = dot(r, r);
			if (d < d1) { d2 = d1; d1 = d; cell = ip + o; } else if (d < d2) { d2 = d; }
		}
	}
	float order = length((cell + 0.5) / sc - center) * 0.9 + hash2(cell).x * 0.25;
	float returned = step(order, back * 1.3);
	float k = amount * (1.0 - returned);
	vec3 col = mix(c, stopped, k);
	float vig = smoothstep(0.35, 1.05, length((SCREEN_UV - 0.5) * vec2(1.7, 1.0)));
	col *= 1.0 - 0.25 * vig * k;
	// 碎回来的那一刻：碎块边缘一圈红白光
	float edge = smoothstep(0.08, 0.0, sqrt(d2) - sqrt(d1));
	float front = smoothstep(0.12, 0.0, abs(order - back * 1.3)) * step(0.001, back) * amount;
	col += mix(vec3(1.0, 0.1, 0.12), vec3(1.0), 0.4) * edge * front;
	COLOR = vec4(col, 1.0);
}" };

	/// <summary>表盘：一圈刻度（60 小格、12 大格）+ 一根秒针，红白发光；angle 是秒针角度，alpha 整体。</summary>
	private static Shader DialShader => _dial ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float angle = 0.0;
uniform float alpha = 1.0;
uniform float flash = 0.0;
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float d = length(p);
	float a = atan(p.x, -p.y);
	float ring = smoothstep(0.012, 0.0, abs(d - 0.92)) + smoothstep(0.006, 0.0, abs(d - 0.84)) * 0.6;
	float minor = step(0.86, d) * step(d, 0.92) * smoothstep(0.012, 0.0, abs(fract(a / (2.0 * PI) * 60.0 + 0.5) - 0.5) / 60.0 * 2.0 * PI * d);
	float major = step(0.74, d) * step(d, 0.92) * smoothstep(0.03, 0.0, abs(fract(a / (2.0 * PI) * 12.0 + 0.5) - 0.5) / 12.0 * 2.0 * PI * d);
	vec2 hd = vec2(sin(angle), -cos(angle));
	float along = dot(p, hd), across = abs(dot(p, vec2(-hd.y, hd.x)));
	float hand = step(-0.12, along) * step(along, 0.8) * smoothstep(0.022, 0.0, across);
	float hub = smoothstep(0.05, 0.03, d);
	vec3 red = vec3(1.0, 0.08, 0.12), hot = vec3(1.0, 0.95, 0.92);
	vec3 col = red * (ring + minor * 0.7) + hot * (major + hand + hub) + hot * flash * smoothstep(1.0, 0.0, d) * 0.4;
	float glow = smoothstep(1.0, 0.85, d) * 0.12;
	COLOR = vec4(col + red * glow, clamp(ring + minor * 0.7 + major + hand + hub + glow, 0.0, 1.0) * alpha);
}" };

	public static void BulletTime(Control layer, Control back, Vector2 chest, float facing = 1f)
	{
		const float stopAt = 0.2f, resumeAt = 1.05f;
		Vector2 screen = Fx.Screen(layer);

		// 表盘（Joker 身后）：秒针飞快转、"咔"地停在时停那一刻，时间恢复时碎掉淡出
		Node2D dialRoot = Fx.Root(back, chest + new Vector2(-20f * facing, -30f), "P5Dial", resumeAt + 0.5);
		ColorRect dial = new()
		{
			Size = new Vector2(520f, 520f), Position = new Vector2(-260f, -260f), PivotOffset = new Vector2(260f, 260f),
			MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = DialShader },
		};
		dialRoot.AddChild(dial);
		var dm = (ShaderMaterial)dial.Material;
		float stoppedAngle = Fx.Rand(1f, 2f);
		Fx.Animate(dialRoot, 0, resumeAt + 0.4, t =>
		{
			float s = t * (resumeAt + 0.4f);
			float spin = s < stopAt ? stoppedAngle - (stopAt - s) * 30f : stoppedAngle + (s > resumeAt ? (s - resumeAt) * 20f : 0f);
			float tick = s >= stopAt && s < stopAt + 0.08f ? Mathf.Sin((s - stopAt) / 0.08f * Mathf.Pi) * 0.08f : 0f;
			dm.SetShaderParameter("angle", spin + tick);
			dm.SetShaderParameter("flash", Mathf.Max(0f, 1f - Mathf.Abs(s - stopAt) / 0.1f));
			float appear = Ease5.OutBack(Ease5.Seg(s, 0f, 0.18f));
			dial.Scale = Vector2.One * Mathf.Max(0.01f, appear * (1f + 0.25f * Ease5.Seg(s, resumeAt, 0.4f)));
			dm.SetShaderParameter("alpha", Mathf.Min(1f, s * 8f) * (1f - Ease5.Seg(s, resumeAt, 0.35f)) * 0.9f);
		});

		// 火星：从胸口炸出去，时停那一刻定格，恢复时变成光点散掉
		Node2D motes = Fx.Root(layer, chest, "P5Motes", resumeAt + 0.5);
		var list = new List<(Sprite2D S, Polygon2D Star, Vector2 V, float Phase)>();
		for (int i = 0; i < 44; i++)
		{
			Sprite2D s = new() { Texture = Glow.Soft, Material = Fx.Additive, Modulate = i % 3 == 0 ? Pal5.Hot : Pal5.Bright, Scale = Vector2.One * Fx.Rand(0.3f, 0.6f) };
			motes.AddChild(s);
			Polygon2D star = Fx.Poly(motes, Glow.Star4(Fx.Rand(12f, 22f), 0.16f), Pal5.Hot, additive: true);
			Vector2 v = Vector2.Right.Rotated(Fx.Rand(0f, Mathf.Tau)) * new Vector2(1f, 0.7f) * Fx.Rand(400f, 1300f);
			list.Add((s, star, v, Fx.Rand(0f, Mathf.Tau)));
		}
		Fx.Animate(motes, 0, resumeAt + 0.4, t =>
		{
			float s = t * (resumeAt + 0.4f);
			float flown = Mathf.Min(s, stopAt) + (s > resumeAt ? (s - resumeAt) * 0.6f : 0f);
			foreach (var (sp, star, v, phase) in list)
			{
				Vector2 pos = v * flown * (1f - flown * 1.2f);
				sp.Position = pos;
				star.Position = pos;
				bool frozen = s >= stopAt && s < resumeAt;
				float tw = frozen ? 0.25f + 0.75f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(s * 7f + phase)), 4f) : 0f;
				star.Scale = Vector2.One * tw;
				star.Rotation = phase;
				float fade = 1f - Ease5.Seg(s, resumeAt, 0.35f);
				sp.Modulate = new Color(sp.Modulate, fade);
				sp.Scale = Vector2.One * (0.4f + 0.5f * Ease5.Seg(s, resumeAt, 0.35f));
			}
		});

		// 时停滤镜（单独的 CanvasLayer，盖满屏幕）
		CanvasLayer cl = new() { Layer = 4, Name = "P5TimeStop" };
		layer.AddChild(cl);
		Fx.FreeAfter(cl, resumeAt + 0.6);
		ColorRect overlay = new()
		{
			Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = TimeStopShader },
		};
		cl.AddChild(overlay);
		var om = (ShaderMaterial)overlay.Material;
		Vector2 c = layer.GetGlobalTransformWithCanvas() * chest;
		om.SetShaderParameter("center", c / screen);
		Fx.Animate(overlay, 0, resumeAt + 0.5, t =>
		{
			float s = t * (resumeAt + 0.5f);
			om.SetShaderParameter("amount", Ease5.Seg(s, stopAt, 0.06f));
			om.SetShaderParameter("back", Ease5.Seg(s, resumeAt, 0.45f));
		});
		Fx.After(layer, stopAt, () =>
		{
			Glow.Flash(layer, chest, new Color(1f, 0.9f, 0.9f), 420f, 0.14f);
			Glow.Ripple(layer, chest, 900f, 0.5f, 0.035f);
			Fx.Shake(1);
		});
		Fx.After(layer, resumeAt, () => Glow.Ripple(layer, chest, 700f, 0.45f, 0.03f));
	}

	// ───────────────────────── 爆发：剪影分身 ─────────────────────────

	private static Shader? _silhouette;

	/// <summary>剪影：纯黑填充 + 外面一圈红描边（采样邻域 alpha）；flash 白闪，cut 0~1 从下往上被横向速度线切走。</summary>
	public static Shader SilhouetteShader => _silhouette ??= new Shader { Code = @"
shader_type canvas_item;
uniform float rim_px = 9.0;
uniform float alpha = 1.0;
uniform float flash = 0.0;
uniform float cut = 0.0;
float hash(float n) { return fract(sin(n) * 43758.5453); }
void fragment() {
	float a = texture(TEXTURE, UV).a;
	float m = 0.0;
	for (int i = 0; i < 16; i++) {
		float ang = 6.2831853 * float(i) / 16.0;
		m = max(m, texture(TEXTURE, UV + vec2(cos(ang), sin(ang)) * TEXTURE_PIXEL_SIZE * rim_px).a);
	}
	float rim = clamp(m - a, 0.0, 1.0);
	vec3 col = mix(vec3(0.015, 0.0, 0.01), vec3(1.0, 0.07, 0.13), rim);
	col = mix(col, vec3(1.0, 0.95, 0.95), flash);
	// 被扯走：按屏幕横条随机错开、往右拉成速度线
	float band = floor(SCREEN_UV.y * 90.0);
	float gone = step(hash(band) * 0.7 + (1.0 - SCREEN_UV.y) * 0.3, cut);
	COLOR = vec4(col, max(a, rim) * alpha * (1.0 - gone));
}" };

	/// <summary>
	/// 爆发：clone 是 Joker 身体的一个副本（游戏里复制骨骼节点、预览里另起一个播放器），已经摆在 Joker 身后、跟本体同一个动作；
	/// setMaterial 把剪影材质装上去。分身从本体位置"刷"地退到身后一步、白闪变黑，停一下，然后被一束红色速度线扯走。
	/// </summary>
	public static void Burst(Control layer, Node2D clone, Action<Material> setMaterial, Vector2 chest, float facing = 1f)
	{
		var mat = new ShaderMaterial { Shader = SilhouetteShader };
		setMaterial(mat);
		Vector2 home = clone.Position;
		const float life = 1.25f;
		Fx.Animate(clone, 0, life, t =>
		{
			float s = t * life;
			float step = Ease5.OutExpo(Ease5.Seg(s, 0f, 0.2f));
			clone.Position = home + new Vector2(-16f * facing * step, -4f * step);       // 用户：只露一点红边，贴着本体
			mat.SetShaderParameter("flash", 1f - Ease5.Seg(s, 0f, 0.16f));
			mat.SetShaderParameter("cut", Ease5.Seg(s, 0.95f, 0.25f));
			mat.SetShaderParameter("alpha", Mathf.Min(1f, s * 12f));
		});
		Fx.FreeAfter(clone, life + 0.05);
		Glow.Flash(layer, chest, new Color(1f, 0.55f, 0.5f), 300f, 0.16f);
		Glow.Ripple(layer, chest, 520f, 0.45f, 0.035f);
		Glow.SpeedLines(layer, chest, Pal5.Bright, 10, 80f, 220f, 0.26f);
		Fx.After(layer, 0.95, () =>
		{
			// 扯走：横向的红白速度线往身后抽
			Node2D root = Fx.Root(layer, chest + new Vector2(-20f * facing, 0f), "P5BurstPull", 0.5);
			for (int i = 0; i < 6; i++)
			{
				Polygon2D line = Glow.TaperPoly(root, i % 3 == 0 ? Pal5.Hot : Pal5.Bright);
				float y = Fx.Rand(-220f, 260f), len = Fx.Rand(110f, 240f), lag = Fx.Rand(0f, 0.08f), w = Fx.Rand(3f, 6f);
				line.Visible = false;
				Fx.Animate(line, lag, 0.28, t =>
				{
					line.Visible = true;
					float head = Ease5.OutExpo(t), tail = Fx.InCubic(t);
					line.Polygon = Glow.TaperPts(new Vector2(-facing * len * head, y), new Vector2(-facing * len * tail * 0.4f, y), w);
				});
			}
		});
	}
}
