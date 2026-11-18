using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 墨之刃第三版「甩墨出刃」（2026-11-18，用户：炸墨太差 → 换创意：三维空间里从手里朝镜头甩墨，然后出 2/3 颗子弹；
/// 要做好拼接、不要有贴图感、不要只靠贴图，要像腐蚀波那样和着色器特效组合）。Codex 的四套序列帧在 art/p5/fx/v4/ink_fling/。
/// 序列帧统一走 InkFlipShader：相邻两帧交叉淡入（不一跳一跳）、每格边缘羽化（甩墨那几帧生成时有硬截边）、噪声蚀散、半透明边缘透红光。
/// </summary>
public static partial class SkillRedo5
{
	private static Shader? _inkFlip;

	private static Shader InkFlipShader => _inkFlip ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D sheet : filter_linear;
uniform float frame = 0.0;      // 0～15，小数部分 = 和下一帧交叉淡入
uniform float erode = 0.0;      // 0→1 被噪声一块块蚀掉
uniform float alpha = 1.0;
uniform float rim = 0.45;       // 半透明边缘透的红光（太重墨就变粉了）
uniform float seed = 0.0;
uniform float flip = 0.0;       // 1 = 水平翻转
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y); }
vec4 cell(float f, vec2 cu) {
	float fi = clamp(floor(f), 0.0, 15.0);
	vec2 o = vec2(mod(fi, 4.0), floor(fi / 4.0));
	return texture(sheet, (o + clamp(cu, 0.004, 0.996)) / 4.0);
}
void fragment() {
	vec2 cu = vec2(flip > 0.5 ? 1.0 - UV.x : UV.x, UV.y);
	float k = fract(frame);
	vec4 a = cell(frame, cu), b = cell(min(frame + 1.0, 15.0), cu);
	vec4 c = mix(a, b, smoothstep(0.0, 1.0, k));
	// 格子边缘羽化（生成图里有几帧墨团碰到格子边、被切出硬边）
	float n = noise(cu * 9.0 + seed) * 0.6 + noise(cu * 31.0 - seed) * 0.4;
	// 格子边缘：按离边的距离 + 噪声不规则地淡掉（只做直线羽化的话，淡出的边界本身还是一条直线）
	float dEdge = min(min(cu.x, 1.0 - cu.x), min(cu.y, 1.0 - cu.y));
	float edge = smoothstep(0.02, 0.24, dEdge + (n - 0.5) * 0.16);
	float kept = smoothstep(erode - 0.06, erode + 0.06, n);
	vec3 col = c.rgb + vec3(0.85, 0.05, 0.12) * clamp(c.a * (1.0 - c.a) * 4.0, 0.0, 1.0) * rim * (0.5 + 0.5 * n);
	COLOR = vec4(col, c.a * edge * kept * alpha);
}" };

	/// <summary>一块序列帧（ColorRect 居中在 root 上）：返回 root（位置 / 缩放 / 旋转自己动）和材质（frame / erode / alpha 自己驱动）。size = 一格画多大。</summary>
	private static (Node2D Root, ShaderMaterial M)? InkFlip(Control layer, string sheet, Vector2 at, float size, bool flipX, double life)
	{
		if (Fx.Tex(sheet) is not Texture2D tex) return null;
		Node2D root = Fx.Root(layer, at, "P5InkFlip", life);
		ShaderMaterial m = new() { Shader = InkFlipShader };
		m.SetShaderParameter("sheet", tex);
		m.SetShaderParameter("seed", Fx.Rand(0f, 20f));
		m.SetShaderParameter("flip", flipX ? 1f : 0f);
		ColorRect r = new() { Size = Vector2.One * size, Position = -Vector2.One * size * 0.5f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(r);
		return (root, m);
	}

	/// <summary>
	/// 墨之刃（稀有：生成 2/3 张带「墨」附魔的小刀）「甩墨出刃」：
	/// 慢（蓄墨 0～0.2）：四周压暗，细光线往他手里收，手里一团墨卷起来（蓄墨序列帧，底下垫红光），身上淡红；
	/// 快（甩 0.2～0.55，主拍）：一甩 —— 一道红疾光先抽出墨鞭的弧，墨从手边被甩向镜头、越来越大（甩墨序列帧整体放大 = 透视），
	///     冲到最近那一刻顿帧、震屏、白闪、一圈空间扭曲；几颗近景墨点放大着从镜头前飞过去（景深）；
	/// 慢（凝弹 0.42～0.75）：甩出去的墨里 2/3 滴在空中拉长、墨皮剥落成子弹（墨凝成弹序列帧，成形时星光 + 红光）；
	/// 收（0.75～1.0）：子弹转向他手，拖着红疾光一颗颗飞回来入膛（「嗒」×n、「咔」），手上白闪、身上红残影一胀。
	/// </summary>
	public static void BladeOfInk3(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = BackOf(c), front = c.Front;
		int n = Mathf.Clamp(c.Count > 0 ? c.Count : 2, 1, 4);
		const float gatherEnd = 0.2f, fling0 = 0.18f, flingDur = 0.4f, form0 = 0.44f, formDur = 0.28f, home0 = 0.78f;
		float peak = fling0 + flingDur * 9f / 16f;
		Vector2 hand = c.Hand, mid = c.Feet + new Vector2(0f, -h * 0.55f);
		bool flip = f < 0f;
		S("gx_whoosh", 0.1f, 0.02f);
		S("gx_whoosh", 0.26f, fling0 + 0.02f);
		S("gx_slice", 0.16f, peak);
		S("gx_pop", 0.14f, peak + 0.02f);
		Dim(back, mid + new Vector2(f * h * 0.5f, 0f), new Vector2(h * 4f, h * 3f), home0 + 0.6, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.12f)) * (1f - Ease5.Seg(s, 0.72f, 0.28f)) * 0.62f);

		// ── 蓄墨
		Gather(front, hand, h, u, gatherEnd, 14, 0.5f);
		Glow.Flash(back, hand, new Color(0.85f, 0.04f, 0.13f), h * 0.9f, gatherEnd + 0.15f);
		if (InkFlip(front, "ink_gather_sheet", hand, h * 0.5f, flip, gatherEnd + 0.12) is { } g)
		{
			Fx.Animate(g.Root, 0, gatherEnd + 0.08, t =>
			{
				float s = t * (gatherEnd + 0.08f);
				g.M.SetShaderParameter("frame", Mathf.Min(15f, 3f + 12f * Mathf.Min(1f, s / gatherEnd)));
				g.Root.Scale = Vector2.One * (0.8f + 0.3f * Ease5.OutBack(Mathf.Min(1f, s / gatherEnd)));
				g.Root.Rotation = -s * 6f * f;
				g.M.SetShaderParameter("alpha", 1f - Ease5.Seg(s, gatherEnd, 0.08f));
			});
		}

		// ── 甩：先一道红疾光抽出墨鞭的弧，墨被甩向镜头（整块放大 = 透视）
		Vector2 whipEnd = hand + new Vector2(f * h * 1.1f, h * 0.35f);
		Vector2 whipCtrl = hand + new Vector2(f * h * 0.8f, -h * 0.35f);
		Func<float, Vector2> whip = e => hand.Lerp(whipCtrl, e).Lerp(whipCtrl.Lerp(whipEnd, e), e);
		StreakSharp(front, whip, fling0, 0.16f, h * 0.9f, 34f * u, new Color(1f, 0.12f, 0.2f), 1.5f);
		StreakSharp(front, e => whip(e) + new Vector2(0f, h * 0.05f), fling0 + 0.02f, 0.16f, h * 0.6f, 10f * u, new Color(1f, 0.7f, 0.7f), 1.8f);
		Vector2 flingAt = hand + new Vector2(f * h * 0.55f, h * 0.12f);
		Glow.Flash(back, flingAt, new Color(0.5f, 0.02f, 0.08f), h * 1.6f, flingDur + 0.2f, fling0);
		if (InkFlip(front, "ink_fling_sheet", flingAt, h * 1.35f, flip, fling0 + flingDur + 0.1) is { } fl)
		{
			fl.Root.Visible = false;
			Fx.Animate(fl.Root, fling0, flingDur, t =>
			{
				fl.Root.Visible = true;
				fl.M.SetShaderParameter("frame", 15f * t);
				// 越往后越大、往镜头（右下）推 = 透视
				float e = Fx.InCubic(Mathf.Min(1f, t * 1.4f));
				fl.Root.Scale = Vector2.One * (0.85f + 0.65f * e);
				fl.Root.GlobalPosition = flingAt + new Vector2(f * h * 0.25f, h * 0.18f) * e;
				fl.M.SetShaderParameter("erode", Ease5.Seg(t, 0.72f, 0.28f) * 0.9f);
				fl.M.SetShaderParameter("alpha", 1f - Ease5.Seg(t, 0.85f, 0.15f));
			});
		}
		// 冲到最近：顿帧、震屏、白闪、空间扭曲
		Fx.After(front, peak, () =>
		{
			HitStop.Request(0.06);
			Fx.Shake(2);
			Vector2 near = flingAt + new Vector2(f * h * 0.3f, h * 0.2f);
			Glow.Flash(front, near, Hot, h * 0.6f, 0.1f);
			Glow.Flash(back, near, new Color(0.6f, 0.02f, 0.08f), h * 2.2f, 0.4f);
			Phantom.Shards(front, near, 16, 500f * u, 1300f * u, new Vector2(f * 0.8f, 0.5f), 70f, 0.4f, 900f * u, 22f * u);
		});
		// 近景墨点：放大着从镜头前飞过去（景深）
		for (int i = 0; i < 6; i++)
		{
			int k = i;
			float d = peak - 0.05f + 0.025f * k;
			Vector2 from = flingAt + new Vector2(f * h * Fx.Rand(0.1f, 0.5f), h * Fx.Rand(-0.1f, 0.3f));
			Vector2 dir = new Vector2(f * Fx.Rand(0.4f, 1f), Fx.Rand(0.2f, 0.9f)).Normalized();
			if (InkFlip(front, "ink_foreground_sheet", from, h * 0.3f, flip, d + 0.35) is { } fg)
			{
				fg.M.SetShaderParameter("frame", (float)Fx.Rng.Next(16));
				fg.M.SetShaderParameter("rim", 0.6f);
				fg.Root.Visible = false;
				float spin = Fx.Rand(-1.5f, 1.5f);
				Fx.Animate(fg.Root, d, 0.3, t =>
				{
					fg.Root.Visible = true;
					float e = Fx.InCubic(t);
					fg.Root.GlobalPosition = from + dir * h * 2.4f * e;
					fg.Root.Scale = Vector2.One * (0.6f + 4.5f * e * e);
					fg.Root.Rotation = spin * t;
					fg.M.SetShaderParameter("alpha", Mathf.Min(1f, t * 6f) * (1f - Ease5.Seg(t, 0.55f, 0.45f)));
				});
			}
		}

		// ── 凝弹：甩出去的墨里 n 滴在空中凝成子弹，再一颗颗飞回手里
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float x = n == 1 ? 0.5f : k / (n - 1f);
			Vector2 at = hand + new Vector2(f * h * (0.75f + 0.45f * x), h * (-0.28f + 0.42f * x));
			float t0 = form0 + 0.045f * k, back0 = home0 + 0.06f * k, fly = 0.17f;
			if (InkFlip(front, "ink_bullet_form_sheet", at, h * 0.5f, flip, back0 + fly + 0.05) is not { } bf) continue;
			bf.Root.Visible = false;
			Fx.Animate(bf.Root, t0, back0 + fly - t0, t =>
			{
				bf.Root.Visible = true;
				float s = t0 + t * (back0 + fly - t0);
				if (s < back0)
				{
					bf.M.SetShaderParameter("frame", Mathf.Min(15f, 15f * (s - t0) / formDur));
					bf.Root.GlobalPosition = at + new Vector2(f * h * 0.04f * Mathf.Min(1f, (s - t0) / formDur), 0f);
					bf.Root.Rotation = 0f;
					bf.Root.Scale = Vector2.One;
				}
				else
				{
					float e = Fx.InCubic((s - back0) / fly);
					Vector2 p0 = at + new Vector2(f * h * 0.04f, 0f);
					bf.Root.GlobalPosition = p0.Lerp(hand, e);
					// 弹头转向他手（图里弹头朝右；朝左时整块已经翻过来了）
					Vector2 dv = hand - p0;
					float ang = f > 0 ? dv.Angle() : dv.Angle() - Mathf.Pi;
					bf.Root.Rotation = Mathf.LerpAngle(0f, ang, Mathf.Min(1f, (s - back0) / 0.06f));
					bf.Root.Scale = Vector2.One * (1f - 0.45f * e);
					bf.M.SetShaderParameter("rim", 0.9f);
				}
			});
			Fx.After(front, t0 + formDur * 0.9f, () =>
			{
				Glow.Glint(front, at + new Vector2(f * h * 0.08f, 0f), 0.6f);
				Glow.Flash(front, at, new Color(1f, 0.3f, 0.35f), h * 0.3f, 0.25f);
			});
			StreakSharp(front, e => (at + new Vector2(f * h * 0.04f, 0f)).Lerp(hand, Fx.InCubic(e)), back0, fly, h * 0.45f, 14f * u, Red, 1.5f);
			Fx.After(front, back0 + fly, () =>
			{
				Sfx.Take("gx_tick", 0.14f);
				Glow.Flash(front, hand, Hot, h * 0.18f, 0.08f);
			});
		}
		float done = home0 + 0.06f * (n - 1) + 0.17f;
		Fx.After(front, done + 0.02f, () =>
		{
			Sfx.Take("gx_tock", 0.16f);
			Glow.Flash(back, hand, Red, h * 1.0f, 0.3f);
			Cross(front, hand, h * 0.35f);
			EchoPulse(c, 0.05f, 0.3f, 0.6f);
		});
		if (c.SelfTint is { } tint)
			Fx.Animate(front, 0, done + 0.5, t =>
			{
				float s = t * (done + 0.5f);
				float kk = 0.25f * Ease5.Seg(s, 0f, gatherEnd) * (1f - Ease5.Seg(s, peak, 0.2f)) + 0.8f * Mathf.Exp(-Mathf.Pow((s - peak) / 0.07f, 2f));
				tint(Colors.White.Lerp(new Color(1.5f, 0.75f, 0.8f), kk));
			});
	}
}
