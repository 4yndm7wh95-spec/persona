using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 第五批续 2：余像、萎靡加料（用户 2026-11-13：特效偏极简、太少，要更多更花哨，别拘谨；萎靡从 Joker 手上出手奇怪 → 用召唤用的面具）。
/// 新积木：P5 红网点爆星（HalftoneBurst）、短暂的刀光旋涡（Vortex）、飘散的光粒（Glitter）。
/// </summary>
public static partial class SkillRedo5
{
	private static Shader? _burst;

	/// <summary>
	/// 圆润的红网点光晕（用户 2026-11-13：不喜欢有棱有角的爆星）：轮廓是缓慢起伏的圆团（低频噪声，没有尖角），边缘软；
	/// 里面 45° 网点，越往外点越小、越淡，底下一层柔和的红光；grow 长出来，hole 从里往外软软地化开（不是细圆圈）。
	/// </summary>
	private static Shader Burst => _burst ??= new Shader { Code = @"
shader_type canvas_item;
uniform float grow = 1.0;
uniform float hole = 0.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
uniform float spikes = 16.0;
uniform float cell = 0.05;
uniform vec4 tint : source_color = vec4(0.92, 0.06, 0.14, 1.0);
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn1(float x) { float i = floor(x), f = fract(x); f = f * f * (3.0 - 2.0 * f); return mix(h21(vec2(i, seed)), h21(vec2(i + 1.0, seed)), f); }
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float r = length(p);
	float a = atan(p.y, p.x) / 6.2831853 + 0.5;
	float n = 5.0;
	float wob = vn1(a * n) * 0.6 + vn1(a * n * 2.0 + 7.0) * 0.4;
	wob = mix(wob, vn1((a - 1.0) * n) * 0.6 + vn1((a - 1.0) * n * 2.0 + 7.0) * 0.4, smoothstep(0.9, 1.0, a));
	float R = (0.72 + 0.26 * wob) * grow;
	float k = r / max(R, 1e-3);
	float body = smoothstep(1.0, 0.72, k);
	vec2 g = mat2(vec2(0.7071, 0.7071), vec2(-0.7071, 0.7071)) * p / cell;
	vec2 gf = fract(g) - 0.5;
	float dr = mix(0.4, 0.06, smoothstep(0.2, 1.0, k));
	float aa = fwidth(length(gf)) + 1e-3;
	float dotm = smoothstep(dr + aa, dr - aa, length(gf));
	float hollow = smoothstep(hole * 0.6, hole + 0.08, k);
	float glow = exp(-k * k * 2.2) * 0.35;
	float al = (dotm * body * 0.95 + glow * body) * hollow;
	if (al <= 0.003) discard;
	COLOR = vec4(mix(tint.rgb, vec3(1.0, 0.5, 0.55), glow * 0.5), al * alpha);
}" };

	/// <summary>一下漾开的红网点光晕：dur 内长出来、从里往外化开、淡掉。</summary>
	private static void HalftoneBurst(Control layer, Vector2 at, float size, float dur, float delay = 0f, Color? tint = null, float alpha = 1f, float cell = 0.045f)
	{
		Node2D root = Fx.Root(layer, at, "P5HalftoneBurst", delay + dur + 0.1);
		root.Rotation = Fx.Rand(0f, Mathf.Tau);
		ShaderMaterial m = new() { Shader = Burst };
		m.SetShaderParameter("seed", Fx.Rand(0f, 50f));
		m.SetShaderParameter("cell", cell);
		m.SetShaderParameter("spikes", Mathf.Round(Fx.Rand(13f, 19f)));
		m.SetShaderParameter("tint", tint ?? new Color(0.92f, 0.06f, 0.14f));
		root.AddChild(new ColorRect { Size = new Vector2(size, size), Position = new Vector2(-size / 2f, -size / 2f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
		root.Visible = false;
		Fx.Animate(root, delay, dur, t =>
		{
			root.Visible = true;
			m.SetShaderParameter("grow", 0.35f + 0.65f * Ease5.OutExpo(t));
			m.SetShaderParameter("hole", Fx.OutCubic(Ease5.Seg(t, 0.2f, 0.8f)) * 1.1f);
			m.SetShaderParameter("alpha", 0.75f * alpha * (1f - Ease5.Seg(t, 0.5f, 0.5f)));
		});
	}

	/// <summary>一阵短暂的刀光旋涡：n 道刀光绕 basePt（脚下）在 y0～y1 高度转、往上卷、淡掉。</summary>
	private static void Vortex(Control front, Control back, Vector2 basePt, float h, float radius, int n, float t0, float dur, float dir, float y0, float y1,
		float widthK = 1f, float rise = 0.12f)
	{
		var list = new List<(Blade b, float y, float r, float spin, float ph, float w)>();
		for (int i = 0; i < n; i++)
		{
			bool thin = i % 2 == 1;
			bool ink = i % 3 == 2;
			Color tint = thin ? new Color(1f, 0.82f, 0.82f) : new Color(0.95f, 0.06f, 0.15f);
			float w = (thin ? 0.03f : 0.09f) * widthK;
			Blade b = MakeBlade(front, back, basePt, ink ? new Color(0.9f, 0.05f, 0.12f) : tint, thin ? 1.5f : 0.9f, ink ? 1f : 0f, w, Fx.Rand(1.8f, 2.6f), dir, t0 + dur);
			list.Add((b, Mathf.Lerp(y0, y1, n == 1 ? 0.5f : i / (n - 1f)), radius * Fx.Rand(0.85f, 1.15f), dir * Fx.Rand(10f, 14f), Fx.Rand(0f, Mathf.Tau), w));
		}
		Node2D driver = Fx.Root(front, basePt, "P5VortexDriver", t0 + dur + 0.05);
		Fx.Animate(driver, t0, dur, t =>
		{
			float s = t * dur;
			foreach (var x in list)
			{
				x.b.B.Visible = true;
				x.b.F.Visible = true;
				float rr = x.r * (0.8f + 0.35f * Fx.OutCubic(t));
				Vector2 at = basePt + new Vector2(0f, -h * (x.y + rise * t));
				Vector2 sz = new(rr * 2f, rr * 2f * 0.3f);
				foreach (Node2D nd in new[] { x.b.B, x.b.F })
				{
					nd.GlobalPosition = at;
					nd.Scale = sz / 2f;
				}
				x.b.Set("head", Mathf.PosMod(x.ph + x.spin * s, Mathf.Tau));
				x.b.Set("flow", s * 14f);
				x.b.Set("alpha", Mathf.Min(1f, s / 0.06f) * (1f - Ease5.Seg(t, 0.55f, 0.45f)));
				x.b.Set("width", x.w * (1f - 0.5f * t));
			}
		});
	}

	/// <summary>一道绕身横扫一圈的大弧光（斜着的轨道，前后两层各画一半）：转大半圈、往外扩、变细淡掉。代替直线斜斩（用户：不要有棱有角）。</summary>
	private static void Sweep(Control front, Control back, Vector2 center, float radius, float tilt, float dir, float t0, float dur, float width,
		Color tint, float hot = 1f, float turns = 0.9f)
	{
		Blade b = MakeBlade(front, back, center, tint, hot, 0f, width, 3.4f, dir, t0 + dur);
		float ph = Fx.Rand(0f, Mathf.Tau);
		Node2D driver = Fx.Root(front, center, "P5SweepDriver", t0 + dur + 0.05);
		Fx.Animate(driver, t0, dur, t =>
		{
			b.B.Visible = true;
			b.F.Visible = true;
			float e = Ease5.OutQuint(t);
			float rr = radius * (0.75f + 0.4f * e);
			Vector2 sz = new(rr * 2f, rr * 2f * 0.34f);
			foreach (Node2D nd in new[] { b.B, b.F })
			{
				nd.GlobalPosition = center;
				nd.Rotation = tilt;
				nd.Scale = sz / 2f;
			}
			b.Set("head", Mathf.PosMod(ph + dir * e * Mathf.Tau * turns, Mathf.Tau));
			b.Set("len", 1.2f + 2.6f * Mathf.Min(1f, t * 3f));
			b.Set("flow", t * 12f);
			b.Set("alpha", Mathf.Min(1f, t * 10f) * (1f - Ease5.Seg(t, 0.5f, 0.5f)));
			b.Set("width", width * (1f - 0.6f * t));
		});
	}

	/// <summary>他周围慢慢往上飘、一闪一闪的红白光粒（一段时间里分几次喷）。</summary>
	private static void Glitter(Control layer, Vector2 basePt, float h, float u, float t0, float dur, int bursts = 6, float spread = 0.35f)
	{
		for (int i = 0; i < bursts; i++)
		{
			float d = t0 + dur * i / Mathf.Max(1, bursts - 1);
			Vector2 at = basePt + new Vector2(Fx.Rand(-spread, spread) * h, -h * Fx.Rand(0.1f, 0.9f));
			Fx.After(layer, d, () => Glow.Sparks(layer, at, 7, 15f * u, 70f * u, 0.9f, new[] { new Color(1f, 0.97f, 0.95f), new Color(1f, 0.3f, 0.38f), new Color(0.7f, 0.03f, 0.1f) },
				-50f * u, 0.015f * u, 0.04f * u, Vector2.Up, 70f));
		}
	}

	/// <summary>
	/// 余像（能力：之后每打出一张牌获得 1 点格挡），约 1.2s：
	/// ① 0 身后红网点爆星迸开，两道斜斩光交叉划过他，一大片水平疾光 + 身后宽的红色速度光带往后掠，碎光往后喷；
	///    贴身一层红残影被拽到身后、拖着横向速度纹；
	/// ② 0.5 主拍：残影「啪」地弹回——白红闪光、十字光芒、第二朵更大的爆星、碎光片和放射斩线迸开、轻震、面具一亮（叮）；
	/// ③ 之后一阵细刀光绕着他腰上转两圈往上卷散掉，光粒慢慢往上飘。
	/// </summary>
	public static void Afterimage(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		const float snap = 0.5f, life = 1.25f;
		Control back = c.Back ?? c.Front;
		S("gx_whoosh", 0.22f, 0.02f);
		S("gx_slice", 0.14f, 0.05f);
		S("gx_ping", 0.22f, snap);
		S("gx_whoosh", 0.1f, snap + 0.12f);

		HalftoneBurst(back, c.Chest + new Vector2(-f * h * 0.15f, 0f), h * 1.6f, 0.42f);
		Sweep(c.Front, back, c.Chest, h * 0.55f, -0.35f * f, -f, 0f, 0.4f, 0.12f, new Color(0.95f, 0.06f, 0.15f), 1f);
		Sweep(c.Front, back, c.Chest + new Vector2(0f, h * 0.15f), h * 0.6f, 0.3f * f, -f, 0.05f, 0.38f, 0.04f, new Color(1f, 0.85f, 0.85f), 1.5f);
		// 身后宽的红色速度光带
		for (int i = 0; i < 3; i++)
		{
			Vector2 at = c.Feet + new Vector2(-f * h * 0.6f, -h * (0.25f + 0.3f * i));
			Fx.After(c.Front, 0.02 * i, () => Flare(back, at, h * 2.4f, h * 0.22f, new Color(1f, 0.1f, 0.2f, 0.4f), 0.4f));
		}
		// 水平疾光：从身前往身后
		for (int i = 0; i < 24; i++)
		{
			float y = c.Feet.Y - h * Fx.Rand(0.03f, 1.02f);
			float x0 = c.Feet.X + f * h * Fx.Rand(0.4f, 0.95f), x1 = c.Feet.X - f * h * Fx.Rand(0.8f, 1.4f);
			bool fr = i % 3 == 0;
			bool white = i % 4 == 1;
			Vector2 a = new(x0, y), b = new(x1, y + Fx.Rand(-0.02f, 0.02f) * h);
			DashFly(fr ? c.Front : back, t => a.Lerp(b, t), Fx.Rand(0f, 0.18f), Fx.Rand(0.22f, 0.34f), h * Fx.Rand(0.45f, 0.9f),
				h * (white ? Fx.Rand(0.018f, 0.03f) : Fx.Rand(0.04f, 0.075f)), white ? new Color(1f, 0.8f, 0.8f) : new Color(0.95f, 0.06f, 0.15f), white ? 1.4f : 0.9f);
		}
		for (int k = 0; k < 5; k++)
		{
			float d = 0.02f + 0.04f * k;
			Vector2 at = c.Feet + new Vector2(-f * h * 0.2f, -h * Fx.Rand(0.15f, 0.9f));
			Fx.After(c.Front, d, () => Glow.Sparks(back, at, 12, 220f * u, 600f * u, 0.45f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 0f,
				0.015f * u, 0.04f * u, new Vector2(-f, 0f), 14f));
		}
		if (RedEcho(c, life) is { } echo)
		{
			var (sp, m) = echo;
			float off = -f * h * 0.075f;
			Fx.Animate(sp, 0, life, t =>
			{
				float s = t * life;
				float pull = Ease5.OutQuint(Ease5.Seg(s, 0.02f, 0.16f));
				float drift = Ease5.Seg(s, 0.18f, snap - 0.18f);
				float b0 = Fx.InCubic(Ease5.Seg(s, snap - 0.08f, 0.08f));
				sp.Position = new Vector2(off * pull * (1f + 0.2f * drift) * (1f - b0), 0f);
				float sm = f * 0.16f * (pull * (1f - Ease5.Seg(s, 0.12f, 0.25f) * 0.7f)) * (1f - b0);
				if (s > snap - 0.08f && s < snap) sm = -f * 0.1f * b0;
				m.SetShaderParameter("smear", sm);
				m.SetShaderParameter("flow", s * 6f);
				m.SetShaderParameter("fill", 0.5f);
				m.SetShaderParameter("alpha", Mathf.Min(1f, s / 0.04f) * (s > snap ? 1f - Ease5.Seg(s, snap, 0.14f) : 0.9f));
			});
		}
		Fx.After(c.Front, snap, () =>
		{
			Fx.Shake(1);
			Glow.Flash(c.Front, c.Chest, new Color(1f, 0.93f, 0.9f), h * 0.5f, 0.16f);
			Glow.Flash(back, c.Chest, new Color(1f, 0.15f, 0.25f), h * 1.5f, 0.3f);
			HalftoneBurst(back, c.Chest, h * 2.1f, 0.45f);
			Flare(c.Front, c.Chest, h * 1.8f, h * 0.05f, new Color(1f, 0.15f, 0.25f, 0.85f), 0.3f);
			Flare(c.Front, c.Chest, h * 1.4f, h * 0.012f, new Color(1f, 0.97f, 0.95f, 1f), 0.24f);
			Flare(c.Front, c.Chest, h * 0.05f, h * 1.3f, new Color(1f, 0.15f, 0.25f, 0.7f), 0.28f);
			Flare(c.Front, c.Chest, h * 0.012f, h * 1.0f, new Color(1f, 0.97f, 0.95f, 1f), 0.22f);
			Sweep(c.Front, back, c.Chest, h * 0.6f, 0.4f, f, 0f, 0.42f, 0.11f, new Color(0.95f, 0.06f, 0.15f), 1f);
			Sweep(c.Front, back, c.Chest + new Vector2(0f, h * 0.1f), h * 0.66f, -0.45f, -f, 0.03f, 0.4f, 0.035f, new Color(1f, 0.85f, 0.85f), 1.5f);
			Glow.Sparks(c.Front, c.Chest, 30, 200f * u, 620f * u, 0.5f, new[] { new Color(1f, 0.97f, 0.95f), new Color(1f, 0.25f, 0.32f) }, 60f * u,
				0.02f * u, 0.05f * u, Vector2.Up, 180f);
			Glow.Glint(c.Front, c.Mask, 0.8f * u);
		});
		Vortex(c.Front, back, c.Feet, h, h * 0.42f, 4, snap + 0.04f, 0.6f, f, 0.3f, 0.7f, 0.9f);
		Glitter(c.Front, c.Feet, h, u, snap + 0.1f, 0.5f, 5);
		Glitter(back, c.Feet, h, u, snap + 0.15f, 0.5f, 4);
		SkillRedo.Pulse(c, new Color(1.4f, 0.72f, 0.74f), snap, 0.26f);
	}

	/// <summary>
	/// 余像生效（之后每打出一张牌、获得那 1 点格挡的一刻）：贴身一层淡红残影往身后滑开、拖着速度纹淡掉，身后掠过四道疾光、一小撮碎光。0.4s，不抢戏。
	/// </summary>
	public static void AfterimageTick(SkillCtx c)
	{
		float h = c.Tall, f = c.Face, u = c.Unit;
		const float life = 0.42f;
		Control back = c.Back ?? c.Front;
		S("gx_tick", 0.05f);
		if (RedEcho(c, life) is { } echo)
		{
			var (sp, m) = echo;
			Fx.Animate(sp, 0, life, t =>
			{
				float e = Ease5.OutQuint(t);
				sp.Position = new Vector2(-f * h * 0.055f * e, 0f);
				m.SetShaderParameter("smear", f * 0.11f * (1f - e * 0.6f));
				m.SetShaderParameter("flow", t * 3f);
				m.SetShaderParameter("fill", 0.4f);
				m.SetShaderParameter("alpha", 0.65f * Mathf.Min(1f, t * life / 0.03f) * (1f - Ease5.Seg(t, 0.45f, 0.55f)));
			});
		}
		for (int i = 0; i < 4; i++)
		{
			float y = c.Feet.Y - h * (0.2f + 0.22f * i + Fx.Rand(-0.05f, 0.05f));
			Vector2 a = new(c.Feet.X - f * h * 0.08f, y), b = new(c.Feet.X - f * h * Fx.Rand(0.6f, 0.9f), y);
			DashFly(back, t => a.Lerp(b, t), 0.02f * i, 0.26f, h * 0.4f, h * (i % 2 == 1 ? 0.016f : 0.03f), i % 2 == 1 ? new Color(1f, 0.85f, 0.85f) : new Color(0.95f, 0.08f, 0.16f), 1.2f);
		}
		Glow.Sparks(back, c.Chest + new Vector2(-f * h * 0.15f, 0f), 8, 150f * u, 380f * u, 0.35f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 0f,
			0.012f * u, 0.03f * u, new Vector2(-f, 0f), 20f);
	}

	/// <summary>
	/// 萎靡（失去力量 + 虚弱），约 1.7s。用户 2026-11-13：从手上出手奇怪 → 从召唤用的面具出手（扶面具 → 指向敌人，Casts 里改成 Summon；不召唤亚森）：
	/// ① 0 面具上一下炸亮：白红闪光、十字光芒、网点爆星、放射斩线和碎光——面具里甩出三道刀光（一粗两细，弧度不同）飞向它头顶；
	/// ② 0.32 落在它头顶炸开（爆星、闪光），散成六道刀光绕着它螺旋往下压、越收越紧；
	/// ③ 0.66 主拍（力量）：刀光扣到它脚边，一排疾光从上往下砸、竖向光芒、放射斩线、碎光片，它被压扁压暗，暗红光粒从它身上被抽起来；
	///    红光顺着三道弧一串串流回他的面具，面具每接一下闪一下，最后他身上一亮；
	/// ④ 1.0 第二拍（虚弱）：再砸一下、再压一下、小爆星，然后回弹，刀光散掉。
	/// 拍子：面具（叮）→ 甩出（风）→ 压（斩，主拍）→ 再压（咔）。
	/// </summary>
	public static void Malaise(SkillCtx c)
	{
		if (c.Target is not SkillFx.Victim v) return;
		float th = Mathf.Max(120f, (v.Feet - v.Top).Length()), u = c.Unit, jh = c.Tall;
		float wid = Mathf.Max(v.Width, th * 0.6f);
		const float land = 0.32f, hit = 0.66f, beat2 = 1.0f, life = 1.7f;
		Control back = c.Back ?? c.Front;
		S("gx_ping", 0.2f, 0f);
		S("gx_whoosh", 0.18f, 0.05f);
		S("gx_slice", 0.26f, hit);
		S("gx_tock", 0.14f, beat2);
		Vector2 mask = c.Mask, tc = v.Feet.Lerp(v.Top, 0.5f);

		// ① 面具炸亮
		Glow.Flash(c.Front, mask, new Color(1f, 0.94f, 0.92f), jh * 0.35f, 0.16f);
		Glow.Flash(back, mask, new Color(1f, 0.15f, 0.25f), jh * 1.0f, 0.3f);
		HalftoneBurst(back, mask, jh * 1.1f, 0.4f);
		Flare(c.Front, mask, jh * 1.3f, jh * 0.035f, new Color(1f, 0.15f, 0.25f, 0.85f), 0.3f);
		Flare(c.Front, mask, jh * 1.0f, jh * 0.009f, new Color(1f, 0.97f, 0.95f, 1f), 0.24f);
		Flare(c.Front, mask, jh * 0.03f, jh * 0.7f, new Color(1f, 0.15f, 0.25f, 0.7f), 0.26f);
		Glow.Glint(c.Front, mask, 1.2f * u);
		Glow.Sparks(c.Front, mask, 20, 100f * u, 380f * u, 0.4f, new[] { new Color(1f, 0.97f, 0.95f), new Color(1f, 0.22f, 0.3f) }, 60f * u, 0.015f * u, 0.04f * u, Vector2.Up, 180f);
		if (c.Back != null) Vortex(c.Front, back, mask + new Vector2(0f, jh * 0.12f), jh, jh * 0.2f, 2, 0f, 0.4f, c.Face, 0.05f, 0.15f, 0.8f);

		// 三道弧：面具 → 它头顶
		Vector2 top = v.Top + new Vector2(0f, -th * 0.15f);
		float span = Mathf.Max(jh, th);
		Func<float, Vector2> MakeArc(float lift, float sway)
		{
			Vector2 ctl = (mask + top) * 0.5f + new Vector2(sway, -span * lift);
			return t => mask.Lerp(ctl, t).Lerp(ctl.Lerp(top, t), t);
		}
		var arcs = new[] { MakeArc(0.55f, 0f), MakeArc(0.85f, -span * 0.1f), MakeArc(0.3f, span * 0.12f) };
		Func<float, float> fly = t => Mathf.Pow(t, 0.8f);
		DashFly(c.Front, arcs[0], 0.04f, land - 0.04f, jh * 0.95f, jh * 0.15f, new Color(0.95f, 0.06f, 0.15f), 1.1f, fly, 0.9f);
		DashFly(c.Front, arcs[0], 0.02f, land - 0.02f, jh * 1.15f, jh * 0.09f, new Color(0.6f, 0.0f, 0.08f), 0.5f, fly, 0.85f);
		DashFly(c.Front, arcs[0], 0.06f, land - 0.06f, jh * 0.7f, jh * 0.035f, new Color(1f, 0.85f, 0.85f), 1.5f, fly, 0.9f);
		DashFly(c.Front, arcs[1], 0.07f, land - 0.05f, jh * 0.7f, jh * 0.05f, new Color(1f, 0.2f, 0.28f), 1.2f, fly, 0.9f);
		DashFly(c.Front, arcs[2], 0.09f, land - 0.07f, jh * 0.6f, jh * 0.03f, new Color(1f, 0.85f, 0.85f), 1.5f, fly, 0.9f);
		for (int k = 0; k < 6; k++)
		{
			float d = 0.07f + 0.035f * k;
			Fx.After(c.Front, d, () => Glow.Sparks(c.Front, arcs[0](fly((d - 0.04f) / (land - 0.04f))), 12, 30f * u, 150f * u, 0.45f,
				new[] { new Color(1f, 0.95f, 0.93f), new Color(1f, 0.22f, 0.3f) }, 80f * u, 0.015f * u, 0.035f * u, Vector2.Down, 90f));
		}
		Fx.After(c.Front, land, () =>
		{
			Glow.Flash(c.Front, top, new Color(1f, 0.9f, 0.88f), th * 0.6f, 0.16f);
			Glow.Flash(back, top, new Color(1f, 0.15f, 0.25f), th * 1.2f, 0.26f);
			HalftoneBurst(back, top, th * 1.3f, 0.36f);
			Glow.Sparks(c.Front, top, 22, 120f * u, 380f * u, 0.45f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 120f * u, 0.02f * u, 0.04f * u, Vector2.Up, 180f);
		});

		// ② ③ 六道刀光绕着它螺旋往下压
		var spec = new (float r, float w, Color tint, float hot, float ink, float y0, float y1)[]
		{
			(0.85f, 0.18f, new Color(0.95f, 0.06f, 0.15f), 0.9f, 0f, 1.1f, 0.3f),
			(0.95f, 0.18f, new Color(0.9f, 0.05f, 0.12f), 0.6f, 1f, 0.95f, 0.16f),
			(0.78f, 0.09f, new Color(1f, 0.75f, 0.75f), 1.4f, 0f, 1.2f, 0.5f),
			(1.0f, 0.06f, new Color(1f, 0.85f, 0.85f), 1.5f, 0f, 0.85f, 0.08f),
			(0.9f, 0.15f, new Color(1f, 0.2f, 0.28f), 1.1f, 0f, 1.25f, 0.62f),
			(1.05f, 0.05f, new Color(1f, 0.85f, 0.85f), 1.5f, 0f, 0.7f, 0.04f),
			(0.7f, 0.12f, new Color(0.9f, 0.05f, 0.12f), 0.6f, 1f, 1.3f, 0.75f),
			(1.12f, 0.04f, new Color(1f, 0.85f, 0.85f), 1.5f, 0f, 1.0f, 0.22f),
		};
		const float squash = 0.34f;
		float side = Mathf.Sign(c.Feet.X - v.Feet.X);
		var blades = new List<(Blade b, float r, float w, float y0, float y1, float spin, float ph)>();
		foreach (var sp in spec)
		{
			Blade b = MakeBlade(c.Front, back, v.Feet, sp.tint, sp.hot, sp.ink, sp.w, Fx.Rand(2.2f, 3.0f), -side, life);
			blades.Add((b, wid * sp.r, sp.w, sp.y0, sp.y1, -side * Fx.Rand(8f, 11f), Fx.Rand(0f, Mathf.Tau)));
		}
		Node2D driver = Fx.Root(c.Front, v.Feet, "P5MalaiseDriver", life + 0.05);
		Fx.Animate(driver, 0, life, t =>
		{
			float s = t * life;
			foreach (var x in blades)
			{
				float a = s - land;
				bool on = a > 0f;
				x.b.B.Visible = on;
				x.b.F.Visible = on;
				if (!on) continue;
				float down = Mathf.Pow(Ease5.Seg(s, land, hit - land), 1.6f);
				float bump = s > beat2 ? Mathf.Sin(Mathf.Pi * Ease5.Seg(s, beat2, 0.14f)) : 0f;
				float rr = x.r * Mathf.Lerp(0.6f, 1.15f, 1f - down) * (1f - 0.12f * bump) * (0.85f + 0.15f * Fx.OutCubic(Mathf.Min(1f, a / 0.08f)));
				float yy = Mathf.Lerp(x.y0, x.y1, down) - 0.04f * bump;
				float ang = x.ph + x.spin * a * (1f - 0.5f * down);
				Vector2 at = v.Feet + new Vector2(0f, -th * yy);
				Vector2 sz = new(rr * 2f, rr * 2f * squash);
				foreach (Node2D n in new[] { x.b.B, x.b.F })
				{
					n.GlobalPosition = at;
					n.Rotation = 0f;
					n.Scale = sz / 2f;
				}
				x.b.Set("head", Mathf.PosMod(ang, Mathf.Tau));
				x.b.Set("flow", a * 14f);
				x.b.Set("alpha", Mathf.Min(1f, a / 0.06f) * (1f - Ease5.Seg(s, beat2 + 0.18f, 0.3f)));
				x.b.Set("width", x.w * (1f + 0.6f * down * Mathf.Exp(-Mathf.Max(0f, s - hit) * 5f)));
			}
		});
		Glitter(c.Front, v.Feet, th, u, land + 0.05f, hit - land, 4, wid / th * 0.6f);

		// 往下砸的疾光
		void Rain(float at0, int n, float power)
		{
			for (int i = 0; i < n; i++)
			{
				float x = v.Feet.X + wid * Fx.Rand(-0.6f, 0.6f);
				float y0 = v.Top.Y - th * Fx.Rand(0.5f, 0.95f), y1 = v.Feet.Y - th * Fx.Rand(0.0f, 0.3f);
				bool fr = i % 2 == 0;
				bool white = i % 3 == 1;
				Vector2 a = new(x, y0), b = new(x + Fx.Rand(-0.04f, 0.04f) * th, y1);
				DashFly(fr ? c.Front : back, t => a.Lerp(b, t), at0 - 0.08f + Fx.Rand(0f, 0.05f), 0.16f, th * Fx.Rand(0.6f, 0.95f) * power,
					th * (white ? 0.03f : 0.07f) * power, white ? new Color(1f, 0.85f, 0.85f) : new Color(0.95f, 0.06f, 0.15f), white ? 1.4f : 0.9f,
					Fx.InCubic, 0.85f);
			}
		}
		Rain(hit, 16, 1f);
		Rain(beat2, 9, 0.8f);
		// 压扁（读屏，椭圆软边，不压暗背景）
		Fx.After(c.Front, hit - 0.02, () =>
		{
			Node2D root = Fx.ScreenRoot(c.Front, "P5MalaisePress");
			Fx.FreeAfter(root, life - hit + 0.1);
			root.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
			ShaderMaterial m = new() { Shader = Press };
			var rect = new ColorRect { Size = Fx.Screen(c.Front), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(rect);
			Vector2 scr = Fx.Screen(c.Front);
			Vector2 a = Fx.ScreenPoint(c.Front, v.Top + new Vector2(-wid * 0.9f, -th * 0.5f)), b = Fx.ScreenPoint(c.Front, v.Feet + new Vector2(wid * 0.9f, th * 0.5f));
			Vector2 fl = Fx.ScreenPoint(c.Front, v.Feet);
			m.SetShaderParameter("box", new Vector4(a.X / scr.X, a.Y / scr.Y, b.X / scr.X, b.Y / scr.Y));
			m.SetShaderParameter("floor_y", fl.Y / scr.Y);
			float dur = life - hit + 0.02f;
			Fx.Animate(rect, 0, dur, t =>
			{
				float s = t * dur, s2 = s - (beat2 - hit + 0.02f);
				float k1 = Fx.OutCubic(Mathf.Clamp(s / 0.06f, 0f, 1f)) * Mathf.Exp(-Mathf.Max(0f, s - 0.08f) * 3f);
				float k2 = s2 > 0f ? Fx.OutCubic(Mathf.Clamp(s2 / 0.05f, 0f, 1f)) * Mathf.Exp(-Mathf.Max(0f, s2 - 0.06f) * 4f) * 0.7f : 0f;
				m.SetShaderParameter("amt", 0.18f * Mathf.Max(k1, k2 + 0.2f * k1));
			});
		});
		Fx.After(c.Front, hit, () =>
		{
			Fx.Shake(1);
			Glow.Flash(back, tc, new Color(1f, 0.15f, 0.25f), wid * 2.3f, 0.34f);
			Glow.Flash(c.Front, tc, new Color(1f, 0.92f, 0.9f), wid * 0.8f, 0.16f);
			HalftoneBurst(back, tc, Mathf.Max(wid, th) * 2.2f, 0.5f);
			Flare(c.Front, tc, th * 0.07f, th * 1.8f, new Color(1f, 0.15f, 0.25f, 0.8f), 0.3f);
			Flare(c.Front, tc, th * 0.02f, th * 1.4f, new Color(1f, 0.97f, 0.95f, 1f), 0.24f);
			Flare(c.Front, v.Feet + new Vector2(0f, -th * 0.04f), wid * 2.4f, th * 0.05f, new Color(1f, 0.15f, 0.25f, 0.85f), 0.3f);
			Flare(c.Front, v.Feet + new Vector2(0f, -th * 0.04f), wid * 1.8f, th * 0.012f, new Color(1f, 0.97f, 0.95f, 1f), 0.24f);
			Sweep(c.Front, back, v.Feet + new Vector2(0f, -th * 0.15f), wid * 0.9f, 0.12f, -side, 0f, 0.45f, 0.13f, new Color(0.95f, 0.06f, 0.15f), 1f);
			Sweep(c.Front, back, v.Feet + new Vector2(0f, -th * 0.25f), wid * 1.0f, -0.15f, side, 0.04f, 0.42f, 0.04f, new Color(1f, 0.85f, 0.85f), 1.5f);
			Glow.Sparks(c.Front, v.Feet, 28, 150f * u, 500f * u, 0.45f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 300f * u,
				0.02f * u, 0.045f * u, Vector2.Up, 75f);
			// 从它身上被抽起来的暗红光粒
			Glow.Sparks(c.Front, tc, 26, 40f * u, 140f * u, 0.9f, new[] { new Color(1f, 0.5f, 0.55f), new Color(0.85f, 0.05f, 0.14f), new Color(0.4f, 0f, 0.06f) },
				-160f * u, 0.02f * u, 0.05f * u, Vector2.Up, 60f);
		});
		Fx.After(c.Front, beat2, () =>
		{
			HalftoneBurst(back, tc, Mathf.Max(wid, th) * 1.4f, 0.36f, 0f, null, 0.8f);
			Flare(c.Front, v.Feet + new Vector2(0f, -th * 0.04f), wid * 1.8f, th * 0.04f, new Color(1f, 0.15f, 0.25f, 0.7f), 0.26f);
			Glow.Sparks(c.Front, v.Feet, 18, 100f * u, 340f * u, 0.4f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 300f * u,
				0.02f * u, 0.04f * u, Vector2.Up, 75f);
		});

		// 红光顺着三道弧流回他的面具（力量被他抽走）
		for (int i = 0; i < 12; i++)
		{
			float d = hit + 0.04f + 0.035f * i;
			bool white = i % 3 == 1;
			var path = arcs[i % 3];
			DashFly(c.Front, t => path(1f - t), d, 0.3f, jh * 0.5f, jh * (white ? 0.03f : 0.07f), white ? new Color(1f, 0.85f, 0.85f) : new Color(0.95f, 0.06f, 0.15f),
				1.2f, t => Fx.InCubic(t) * 0.4f + t * 0.6f, 0.85f);
			if (i % 3 == 0)
				Fx.After(c.Front, d + 0.29f, () =>
				{
					Glow.Flash(c.Front, mask, new Color(1f, 0.25f, 0.32f), jh * 0.45f, 0.16f);
					Glow.Sparks(c.Front, mask, 10, 40f * u, 180f * u, 0.35f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 0f, 0.015f * u, 0.035f * u, Vector2.Up, 180f);
				});
		}
		Fx.After(c.Front, hit + 0.5f, () =>
		{
			Glow.Glint(c.Front, mask, 1f * u);
			HalftoneBurst(back, mask, jh * 0.8f, 0.32f, 0f, null, 0.8f);
		});
		Glitter(c.Front, c.Feet, jh, u, hit + 0.4f, 0.4f, 4);
		SkillRedo.Pulse(c, new Color(1.4f, 0.7f, 0.72f), hit + 0.42f, 0.3f);

		// 它压暗（跟着压的节奏）
		if (v.Tint is { } tint)
		{
			Node2D tk = Fx.Root(c.Front, v.Feet, "P5MalaiseTint", life + 0.1);
			Color dark = new(0.5f, 0.36f, 0.42f);
			Fx.Animate(tk, 0, life, t =>
			{
				float s = t * life;
				float k = Ease5.Seg(s, hit, 0.05f) * (1f - Ease5.Seg(s, beat2 + 0.15f, 0.35f) * 0.85f);
				tint(Colors.White.Lerp(dark, k));
			});
			tk.TreeExiting += () => tint(Colors.White);
		}
	}
}

public static partial class SkillRedo5
{
	/// <summary>
	/// 群蛇形态的一咬（用户 2026-11-13：蛇咬的时候敌人身上同时有打击牌的刀光火花，蛇被挡住、看不清 → 加强）：
	/// 扑下去那 0.1s 一道亮红白的疾光从蛇缩起的位置射向咬点；咬合那一刻上下两道弧形「獠牙」猛地合上，
	/// 身后漾开一圈红网点光晕。top = 盖在命中特效上面的那一层，back = 敌人身后。from = 蛇缩起的位置，at = 咬点，T = Joker 身高。
	/// </summary>
	public static void SerpentStrike(Control top, Control back, Vector2 from, Vector2 at, float T, float biteIn, float lungeAt = 0f)
	{
		float fly = Mathf.Max(0.04f, biteIn - lungeAt);
		Vector2 dir = (at - from).LengthSquared() > 1f ? (at - from).Normalized() : Vector2.Right;
		Vector2 perp = new(-dir.Y, dir.X);
		Vector2 a0 = from, a1 = at;
		// 疾光从蛇头开始往下扑的那一刻（lungeAt）射出，和蛇头一起到
		DashFly(top, t => a0.Lerp(a1, t), lungeAt, fly + 0.04f, T * 0.7f, T * 0.09f, new Color(0.95f, 0.06f, 0.15f), 1f, Fx.InCubic, 0.7f);
		DashFly(top, t => a0.Lerp(a1, t), lungeAt + 0.01f, fly + 0.03f, T * 0.55f, T * 0.025f, new Color(1f, 0.85f, 0.85f), 1.5f, Fx.InCubic, 0.7f);
		Fx.After(top, biteIn, () =>
		{
			float R = T * 0.45f;
			foreach (float s in new[] { 1f, -1f })
			{
				// 獠牙：从咬点两侧外面（偏后）沿弧线猛地合到咬点
				Vector2 p0 = at + perp * R * s - dir * R * 0.7f, c = at + perp * R * 0.9f * s + dir * R * 0.5f;
				Func<float, Vector2> jaw = t => p0.Lerp(c, t).Lerp(c.Lerp(at, t), t);
				DashFly(top, jaw, 0f, 0.2f, R * 1.4f, T * 0.11f, new Color(0.95f, 0.06f, 0.15f), 1f, t => Ease5.OutQuint(Mathf.Min(1f, t * 2.2f)), 0.55f);
				DashFly(top, jaw, 0.005f, 0.19f, R * 1.2f, T * 0.035f, new Color(1f, 0.9f, 0.88f), 1.6f, t => Ease5.OutQuint(Mathf.Min(1f, t * 2.2f)), 0.55f);
			}
			Glow.Flash(top, at, new Color(1f, 0.92f, 0.9f), T * 0.45f, 0.14f);
			HalftoneBurst(back, at, T * 1.5f, 0.38f);
			Glow.Flash(back, at, new Color(1f, 0.12f, 0.22f), T * 1.4f, 0.3f);
		});
	}
}

public static partial class SkillRedo5
{
	/// <summary>迷雾：一圈刀光绕着敌人往下压（rise 负 = 往下卷）。</summary>
	public static void HazeVortex(Control front, Control back, Vector2 feet, float h, float radius, float t0, float dur, float dir) =>
		Vortex(front, back, feet, h, radius, 6, Mathf.Max(0f, t0), dur, dir, 0.35f, 1.0f, 1.3f, -0.4f);

	/// <summary>十字光芒（横竖各一道红晕 + 白芯），给别的文件用。</summary>
	public static void Cross(Control layer, Vector2 at, float h)
	{
		Flare(layer, at, h * 1.7f, h * 0.05f, new Color(1f, 0.15f, 0.22f, 0.85f), 0.3f);
		Flare(layer, at, h * 1.3f, h * 0.012f, new Color(1f, 0.95f, 0.96f, 1f), 0.24f);
		Flare(layer, at, h * 0.05f, h * 1.2f, new Color(1f, 0.15f, 0.22f, 0.7f), 0.28f);
		Flare(layer, at, h * 0.012f, h * 0.9f, new Color(1f, 0.95f, 0.96f, 1f), 0.22f);
	}

	/// <summary>一道疾光沿直线 / 曲线飞（给别的文件用；path 0 → 1）。</summary>
	public static void Streak(Control layer, Func<float, Vector2> path, float t0, float dur, float len, float thick, Color tint, float hot = 1.2f) =>
		DashFly(layer, path, t0, dur, len, thick, tint, hot, Ease5.OutQuint, 0.7f);

	/// <summary>绕身横扫的大弧光（给别的文件用）。</summary>
	public static void RingSweep(Control front, Control back, Vector2 center, float radius, float tilt, float dir, float t0, float dur, float width, Color tint, float hot = 1f, float turns = 0.9f) =>
		Sweep(front, back, center, radius, tilt, dir, t0, dur, width, tint, hot, turns);

	/// <summary>刀光旋涡（给别的文件用）。</summary>
	public static void Whirl(Control front, Control back, Vector2 basePt, float h, float radius, int n, float t0, float dur, float dir, float y0, float y1, float widthK = 1f, float rise = 0.12f) =>
		Vortex(front, back, basePt, h, radius, n, t0, dur, dir, y0, y1, widthK, rise);

	/// <summary>往上飘的光粒（给别的文件用）。</summary>
	public static void Motes(Control layer, Vector2 basePt, float h, float u, float t0, float dur, int bursts = 6, float spread = 0.35f) =>
		Glitter(layer, basePt, h, u, t0, dur, bursts, spread);

	/// <summary>圆润的网点光晕（给别的文件用）。</summary>
	public static void SoftBurst(Control layer, Vector2 at, float size, float dur, Color tint) => HalftoneBurst(layer, at, size, dur, 0f, tint);
}
