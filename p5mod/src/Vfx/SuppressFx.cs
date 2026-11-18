using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 压制（远古：11 伤害 + 3 层虚弱；用户：特效参考中和，但要更丰富、华丽一些）第三版「三重锁压」（2026-11-18；前两版：手绘重劈贴图、能量鞭，都被否）。
/// 全部着色器实时画，不用贴图，词汇照用户认可的中和（锁环收紧 + 往下砸的 V 光 + 下沉的光尘 + 脚下扁的暗红波），每一样都做得更重：
/// · 刀砍中（重斩）；四周压暗；
/// · 三道锁环一道接一道「咔、咔、咔」扣在他身上（头、胸、膝 = 3 层虚弱）：每道从外面猛地收紧、过冲，扣上那一下白热一闪；
///   锁环是立体的：后半圈在他身后（被身体挡住）、前半圈在身前，四个卡扣跟着慢慢转；
/// · 四道 V 光从头顶一道比一道快、一道比一道大地砸下来，每道后面拖一截残影；
/// · 最后一道巨大的 V 从画面顶上砸下来 —— 重压：顿帧、震屏、一道往下灌的暗红光柱，三道锁环被一起压到脚下、挤成一团再炸亮；
///   脚下两圈扁的暗红冲击波、地面扭一下、尘往两边贴地冲出去、红色的光尘往下沉；他整个人一沉、变暗；
/// · 锁环在脚下慢慢转着暗下去。
/// </summary>
public static class SuppressFx
{
	private static Shader? _ring, _chev, _column;

	/// <summary>立体锁环（压扁的环）：half = -1 只画后半圈（画面上方那半，放在敌人身后的图层）、+1 只画前半圈；四个卡扣；flare = 扣上那一下的白热。</summary>
	private static Shader Ring => _ring ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float radius = 0.5;
uniform float width = 0.03;
uniform float alpha = 1.0;
uniform float spin = 0.0;
uniform float half_side = 1.0;
uniform float flare = 0.0;
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float d = length(p);
	float side = half_side > 0.0 ? smoothstep(-0.12, 0.12, p.y) : 1.0 - smoothstep(-0.12, 0.12, p.y);
	float a = atan(p.y, p.x) + spin;
	float g = cos(a * 4.0);
	float gap = smoothstep(0.955, 0.985, g);                          // 卡扣之间断开一小截
	float clamp_ = smoothstep(0.78, 0.9, g) * (1.0 - gap);            // 断口两边亮一截（卡扣）
	float x = (d - radius) / width;
	// 沿着环流动的明暗（不是一根均匀的霓虹管）：几团亮的光顺着环跑
	float flow = 0.55 + 0.45 * sin(a * 3.0 - spin * 4.0) * sin(a * 7.0 + spin * 2.0 + 1.3);
	float band = exp(-x * x) * (1.0 - gap * 0.9) * (0.6 + 0.4 * flow);
	float hot = exp(-pow((d - radius + width * 0.25) / (width * 0.3), 2.0)) * (1.0 - gap) * smoothstep(0.5, 1.0, flow);
	float glow = exp(-x * x / 22.0) * 0.28;
	float front = half_side > 0.0 ? 1.0 : 0.5;                         // 后半圈远、暗一点
	vec3 col = mix(vec3(0.55, 0.02, 0.07), vec3(1.0, 0.1, 0.15), band) * (band * 1.1 + glow + clamp_ * band * 1.1)
		+ vec3(1.0, 0.62, 0.55) * hot * (0.08 + 1.1 * flare);
	float m = (band + glow + hot * 0.6) * front * side;
	float edge = smoothstep(1.0, 0.9, d);
	COLOR = vec4(col, clamp(m, 0.0, 1.0) * alpha * edge);
}" };

	/// <summary>往下砸的 V 光（距离场画的，边缘柔、芯白热，上面拖一截残影）。UV 原点在 V 的尖上方 pad 处；单位像素。</summary>
	private static Shader Chev => _chev ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec2 size = vec2(400.0, 600.0);
uniform float w = 150.0;
uniform float hh = 60.0;
uniform float th = 14.0;
uniform float trail = 200.0;
uniform float alpha = 1.0;
uniform float heat = 0.5;
float seg(vec2 p, vec2 a, vec2 b) { vec2 pa = p - a, ba = b - a; float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0); return length(pa - ba * h); }
float sdv(vec2 p) {
	// 尖朝下的斜切 V（P5 的硬边几何）：两条平行的 V 线之间一条等宽的带子，尖是斜接的尖角，两头斜着切掉；臂越往外越细
	vec2 n = normalize(vec2(hh, w));                    // 臂的法线（朝下）
	float t = clamp(abs(p.x) / w, 0.0, 1.0);
	float tk = th * (1.0 - 0.5 * t);
	float lo = dot(vec2(abs(p.x), p.y), n);             // 到 V 外沿（下沿）那条线的距离：下沿过尖 (0, 0)
	float band = max(lo, -(lo + tk * 2.0));             // 下沿往上 2tk 的带子
	float cap = dot(vec2(abs(p.x), p.y) - vec2(w, -hh), normalize(vec2(1.0, -0.35)));   // 两头斜切
	return max(band, cap);
}
float lead(vec2 p) { vec2 n = normalize(vec2(hh, w)); return dot(vec2(abs(p.x), p.y), n); }
void fragment() {
	vec2 p = (UV - vec2(0.5, 1.0)) * size + vec2(0.0, th * 2.5);
	float d = sdv(p);
	float aa = 1.5;
	float body = smoothstep(aa, -aa, d);
	float glow = exp(-max(d, 0.0) / (th * 1.4)) * 0.45;
	// 残影：往上几份、越远越淡越宽
	float tr = 0.0;
	for (int i = 1; i <= 6; i++) {
		float k = float(i) / 6.0;
		float dd = sdv(p + vec2(0.0, trail * k));
		tr = max(tr, exp(-max(dd, 0.0) / (th * (0.8 + 1.5 * k))) * (1.0 - k) * 0.5);
	}
	// 带子里面：上面（后沿）暗红 → 下面（前沿、砸下去的那一边）亮红，前沿一道很细的热边
	float across = clamp(-lead(p) / (th * 2.0), 0.0, 1.0);
	vec3 bodyC = mix(vec3(1.0, 0.14, 0.16), vec3(0.42, 0.02, 0.06), across);
	float edgeHot = exp(-pow(lead(p) / (th * 0.22), 2.0)) * step(d, th * 0.3);
	vec3 col = bodyC * body * 1.1 + vec3(0.85, 0.04, 0.1) * (glow + tr) + vec3(1.0, 0.6, 0.5) * edgeHot * (0.25 + 0.6 * heat);
	float m = clamp(body + glow + tr + edgeHot * 0.5, 0.0, 1.0);
	COLOR = vec4(col, m * alpha);
}" };

	/// <summary>重压那一下往下灌的暗红光柱：竖着流的条纹、两边柔、底下最亮。</summary>
	private static Shader Column => _column ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float clock = 0.0;
uniform float alpha = 1.0;
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
void fragment() {
	float x = UV.x * 2.0 - 1.0;
	float across = exp(-x * x * 3.2);
	float streak = vn(vec2(UV.x * 14.0, UV.y * 1.6 - clock * 7.0)) * 0.65 + vn(vec2(UV.x * 31.0, UV.y * 2.4 - clock * 11.0)) * 0.35;
	streak = smoothstep(0.35, 0.95, streak);
	float down = smoothstep(0.0, 0.85, UV.y) * (1.0 - smoothstep(0.96, 1.0, UV.y));
	float m = across * down * (0.25 + 0.75 * streak);
	vec3 col = mix(vec3(0.5, 0.02, 0.06), vec3(1.0, 0.25, 0.25), streak * across * down);
	COLOR = vec4(col, m * alpha * 0.8);
}" };

	private static ColorRect Quad(Node parent, Shader sh, Vector2 size)
	{
		ColorRect r = new()
		{
			Size = size, Position = -size / 2f, PivotOffset = size / 2f, MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = sh },
		};
		parent.AddChild(r);
		return r;
	}

	private static void P(ColorRect r, string k, Variant v) => ((ShaderMaterial)r.Material).SetShaderParameter(k, v);

	/// <summary>压制命中（DamagePatch，伤害结算那一刻）。at / top = 敌人中心 / 头顶，width = 敌人宽。</summary>
	public static void Play(Control layer, Control? back, Vector2 at, Vector2 top, float width, float face, Action<Color>? tint)
	{
		Control bk = back != null && GodotObject.IsInstanceValid(back) ? back : layer;
		float u = Fx.Screen(layer).Y / 1080f, h = Mathf.Max(90f * u, (at - top).Length());
		Vector2 waist = at + new Vector2(0f, h * 0.1f), feet = at + new Vector2(0f, h * 0.95f);
		const float press = 0.69f, end = 1.4f;

		KnifeFx.Hit(layer, at, face, true);
		SkillRedo5.Dim(bk, at, new Vector2(Mathf.Max(width * 3.2f, h * 3.4f), h * 4.4f), end,
			s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.12f)) * (1f - Ease5.Seg(s, press + 0.15f, 0.5f)) * 0.6f);

		// ── 三道锁环：头、胸、膝 ──
		float diam = Mathf.Max(width * 1.2f, h * 1.15f);
		float[] ys = { -0.55f, 0.05f, 0.62f };
		for (int i = 0; i < 3; i++)
		{
			int ii = i;
			float lockAt = 0.06f + 0.1f * i;            // 开始收
			float y0 = ys[i] * h;
			float dz = diam * (i == 1 ? 1.08f : 0.94f);
			var halves = new (Node2D Root, ColorRect Q)[2];
			for (int sidx = 0; sidx < 2; sidx++)
			{
				Node2D root = Fx.Root(sidx == 0 ? bk : layer, at + new Vector2(0f, y0), "P5SuppressRing", end + 0.1);
				root.Scale = new Vector2(1f, 0.26f);
				root.Visible = false;
				ColorRect q = Quad(root, Ring, Vector2.One * dz * 2f);
				P(q, "half_side", sidx == 0 ? -1f : 1f);
				P(q, "width", 0.026f);
				halves[sidx] = (root, q);
			}
			float spin0 = Fx.Rand(0f, Mathf.Tau);
			Fx.Animate(halves[0].Root, 0, end, t =>
			{
				float s = t * end;
				if (s < lockAt) return;
				float k = Ease5.OutBack(Ease5.Seg(s, lockAt, 0.13f));
				float r = Mathf.Lerp(0.98f, 0.5f, k);
				// 重压：三道一起被压到脚下（挤扁、再炸亮），之后在脚下慢慢转着暗掉
				float down = Fx.InCubic(Ease5.Seg(s, press - 0.01f, 0.09f));
				float y = Mathf.Lerp(y0, h * 0.95f, down) + Mathf.Sin(s * 5f + ii) * h * 0.01f * (1f - down);
				r *= Mathf.Lerp(1f, 1.15f + 0.12f * ii, down);
				float flare = Mathf.Exp(-Mathf.Pow((s - lockAt - 0.1f) / 0.05f, 2f)) + 1.3f * Mathf.Exp(-Mathf.Pow((s - press - 0.04f) / 0.07f, 2f));
				float a = Mathf.Min(1f, (s - lockAt) / 0.04f) * (1f - Ease5.Seg(s, end - 0.45f, 0.45f));
				foreach (var (root, q) in halves)
				{
					if (!GodotObject.IsInstanceValid(root)) continue;
					root.Visible = true;
					root.GlobalPosition = at + new Vector2(0f, y);
					root.Scale = new Vector2(1f, Mathf.Lerp(0.26f, 0.2f, down));
					P(q, "radius", r);
					P(q, "spin", spin0 + s * (1.2f + 0.5f * ii) * (ii == 1 ? -1f : 1f));
					P(q, "flare", flare);
					P(q, "alpha", a);
				}
			});
			// 扣上那一下：白热一闪、两边迸几点火星、一声轻的「咔」、他身上一暗
			Fx.After(layer, lockAt + 0.1f, () =>
			{
				Vector2 c = at + new Vector2(0f, y0);
				Glow.Flash(layer, c + new Vector2(0f, dz * 0.12f), Pal5.Hot, dz * 0.45f, 0.12f);
				Glow.Flash(bk, c, Pal5.Bright, dz * 1.3f, 0.25f);
				Phantom.Shards(layer, c + new Vector2(-dz * 0.48f, 0f), 3, 120f * u, 320f * u, Vector2.Left, 50f, 0.22f, 400f * u, 8f * u);
				Phantom.Shards(layer, c + new Vector2(dz * 0.48f, 0f), 3, 120f * u, 320f * u, Vector2.Right, 50f, 0.22f, 400f * u, 8f * u);
				HitStop.Request(0.02);
				Sfx.Play(Sfx.RetainLock, 0.32f);
				tint?.Invoke(Colors.White.Lerp(new Color(0.62f, 0.42f, 0.48f), 0.4f + 0.2f * ii));
			});
		}

		// ── 四道 V 光：一道比一道快、一道比一道大 ──
		float[] cAt = { 0.33f, 0.43f, 0.51f, 0.57f };
		for (int i = 0; i < 4; i++)
			Chevron(layer, top + new Vector2(0f, -h * (0.75f + 0.12f * i)), waist, h * (0.62f + 0.14f * i), cAt[i], 0.12f - 0.015f * i, 0.3f + 0.15f * i, u);

		// ── 最后一道：巨大的 V 从画面顶上砸下来 ──
		Chevron(layer, at + new Vector2(0f, -h * 2.8f), waist + new Vector2(0f, h * 0.05f), h * 1.7f, press - 0.09f, 0.09f, 1f, u, giant: true);
		// 光柱：重压那一刻往下灌
		{
			float colH = h * 3.4f, colW = Mathf.Max(width * 1.3f, h * 1.2f);
			Node2D root = Fx.Root(bk, feet + new Vector2(0f, -colH * 0.5f), "P5SuppressColumn", end);
			ColorRect q = Quad(root, Column, new Vector2(colW, colH));
			P(q, "alpha", 0f);
			Fx.Animate(q, press - 0.06f, 0.5, t =>
			{
				float s = t * 0.5f;
				P(q, "clock", s);
				P(q, "alpha", Mathf.Min(1f, s / 0.05f) * (1f - Fx.OutCubic(Ease5.Seg(s, 0.08f, 0.42f))));
			});
		}
		Fx.After(layer, press, () =>
		{
			HitStop.Request(0.08);
			Fx.Shake(2);
			// 音效只留干净的三拍：刀命中 →「咔、咔、咔」三下轻的扣锁 → 一声重压（用户 2026-11-18：之前叠了下压双音、风声、闷响，太乱）
			Sfx.Play(Sfx.Slam, 0.32f);
			Sfx.Play(Sfx.SubBoom, 0.34f);
			Glow.Flash(layer, waist, Pal5.Hot, h * 0.9f, 0.12f);
			Glow.Flash(bk, waist, Pal5.Bright, h * 3.2f, 0.45f);
			Glow.Flash(bk, feet, new Color(0.7f, 0.03f, 0.08f), h * 2.6f, 0.6f);
			Glow.Shockwave(layer, feet, Pal5.Bright, Mathf.Max(width * 3.4f, h * 3.4f), 0.55f, 0.2f);
			Glow.Ripple(layer, feet, h * 3f, 0.5f, 0.02f, 0.3f);
			// 尘往两边贴地冲出去、红色的光尘往下沉
			Phantom.Shards(layer, feet, 8, 500f * u, 1100f * u, Vector2.Left, 18f, 0.3f, 0f, 14f * u);
			Phantom.Shards(layer, feet, 8, 500f * u, 1100f * u, Vector2.Right, 18f, 0.3f, 0f, 14f * u);
			Glow.Sparks(layer, waist, 34, 60f * u, 260f * u, 0.9f, new[] { Pal5.Bright, Pal5.Crimson, Pal5.Deep }, 520f * u, 0.1f, 0.32f, Vector2.Down, 85f);
			Glow.Sparks(layer, feet, 20, 300f * u, 700f * u, 0.45f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 300f * u, 0.08f, 0.22f, Vector2.Up, 70f);
			if (tint != null)
				Fx.Animate(layer, 0, 0.75, t => tint(Colors.White.Lerp(new Color(0.42f, 0.27f, 0.33f), 1f - Fx.InCubic(t))));
		});
	}

	/// <summary>一道 V 光：从 from 砸到 to（fall 秒，越来越快），落地亮一下散掉；拖着残影。</summary>
	private static void Chevron(Control layer, Vector2 from, Vector2 to, float width, float delay, float fall, float heat, float u, bool giant = false)
	{
		float w = width * 0.5f, hh = width * 0.36f, th = width * (giant ? 0.06f : 0.07f), trail = width * (giant ? 1.6f : 1.1f);
		Vector2 size = new(width * 1.5f, hh + trail + th * 8f);
		Node2D root = Fx.Root(layer, from, "P5SuppressChevron", delay + fall + 0.4);
		ColorRect q = new()
		{
			Size = size, Position = new Vector2(-size.X * 0.5f, -size.Y + th * 2.5f), MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = Chev },
		};
		root.AddChild(q);
		P(q, "size", size);
		P(q, "w", w);
		P(q, "hh", hh);
		P(q, "th", th);
		P(q, "trail", trail);
		P(q, "heat", heat);
		root.Visible = false;
		float life = fall + 0.22f;
		Fx.Animate(root, delay, life, t =>
		{
			float s = t * life;
			root.Visible = true;
			float k = Fx.InCubic(Mathf.Min(1f, s / fall));
			root.GlobalPosition = from.Lerp(to, k);
			float after = Ease5.Seg(s, fall, 0.22f);
			P(q, "trail", trail * (1f - after) * Mathf.Min(1f, s / (fall * 0.5f)));
			P(q, "alpha", Mathf.Min(1f, s / 0.03f) * (1f - Fx.OutCubic(after)));
			root.Scale = new Vector2(1f + 0.35f * Fx.OutCubic(after), 1f - 0.3f * Fx.OutCubic(after));
		});
		Fx.After(layer, delay + fall, () =>
		{
			Glow.Flash(layer, to, Pal5.Hot, width * (giant ? 0.5f : 0.35f), 0.1f);
			if (!giant) Glow.Flash(layer, to, Pal5.Bright, width * 1.1f, 0.2f);
		});
	}
}
