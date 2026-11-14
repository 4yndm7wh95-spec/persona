using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 幽魂形态第六版「黑羽」（2026-11-14；用户：月亮和群蛇的日轮重复，不要；水影 + 划刀表现力太弱；
/// 以前的黑羽是色块、很塑料 → 这次羽毛本身要有细节：羽轴、羽枝纹、裂口、绒羽、光泽、背光勾出的红边）。
/// 没有人物动作，全靠特效：
/// ① 0 压暗，身后红色背光亮起，人褪成半透明的苍白；
/// ② 0.26 起一对黑羽翼从他背后一片片展开（三层羽：飞羽 / 次级 / 覆羽），一道光泽顺着翼面扫过去；
/// ③ 0.86 主拍：羽翼散成漫天黑羽 —— 向外迸开、被强阻尼拉住悬在半空慢慢翻；
/// ④ 之后羽毛飘飘荡荡地落下，前排的直接从他身上穿过去（他没有实体）。
/// </summary>
public static partial class SkillRedo5
{
	private static Shader? _feather, _wingGlow;

	/// <summary>
	/// 一片羽毛。UV.x：0 羽根 → 1 羽尖；UV.y 横向。
	/// tilt：光泽带的位置（-1..1，翻动时变）；rim：背光勾边强度；glint：一道扫过的高光；under：1 = 翻到背面（灰一点、没光泽）。
	/// </summary>
	private static Shader Feather => _feather ??= new Shader { Code = @"
shader_type canvas_item;
uniform float alpha = 1.0;
uniform float tilt = 0.0;
uniform float rim = 1.0;
uniform float glint = 0.0;
uniform float under = 0.0;
uniform float seed = 0.0;
uniform float bend = 0.22;
uniform float lit = 0.0;      // 整片被闪光照亮
float h11(float n) { return fract(sin(n * 127.1 + 311.7) * 43758.5453); }
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), f.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), f.x), f.y); }
void fragment() {
	float x = UV.x, y = (UV.y - 0.5) * 2.0;
	float e = y - bend * (x * x - 0.32);
	float side = step(0.0, e);                       // 0 = 外侧羽片（宽）、1 = 内侧（窄）
	float ad = abs(e);
	float grow = smoothstep(0.1, 0.36, x);
	float tx = clamp((x - 0.66) / 0.335, 0.0, 1.0);
	float tip = sqrt(max(0.0, 1.0 - tx * tx));          // 圆头
	float w = grow * tip * mix(0.9, 0.62, side) * (0.86 + 0.14 * smoothstep(0.3, 0.7, x));
	// 羽枝坐标：羽枝从羽轴斜着朝羽尖长出去
	float bc = x * 36.0 - ad * 9.0 + seed * 13.0;
	float hb = h11(floor(bc) + side * 57.0 + seed);
	// 羽缘：每根羽枝长短略不同；偶尔裂开一道口子
	float grp = bc * 0.3;
	float split = step(0.8, h11(floor(grp) + side * 31.0 + seed * 3.0));
	float cut = split * smoothstep(0.26, 0.0, fract(grp)) * 0.6;
	float wv = w * (1.0 - 0.08 * hb) * (1.0 - cut);
	float aa = fwidth(ad) * 1.3 + 0.004;
	float vane = smoothstep(wv, wv - aa - 0.015, ad) * step(0.001, w);
	// 羽轴：根粗尖细，一直到羽尖前一点
	float ts = 0.034 * (1.0 - x * 0.88) + 0.006;
	float shaft = smoothstep(ts, ts - aa, ad) * (1.0 - smoothstep(0.8, 0.92, x)) * step(0.02, x);
	float u = clamp(ad / max(w, 0.001), 0.0, 1.0);    // 0 羽轴 → 1 羽缘
	// 羽枝纹：细密的明暗条
	float st = 0.5 + 0.5 * sin(bc * 6.2831853);
	float fine = mix(0.84, 1.0, smoothstep(0.1, 0.9, st));
	float coarse = 0.85 + 0.15 * vn(vec2(bc * 0.22, side * 9.0 + seed));
	vec3 base = mix(vec3(0.035, 0.022, 0.035), vec3(0.09, 0.075, 0.09), under);
	// 光泽带：位置随翻动移动，沿羽枝方向有断续
	float pos = 0.48 + 0.36 * tilt;
	float sheen = exp(-pow((u - pos) / 0.2, 2.0)) * (0.55 + 0.45 * hb) * (1.0 - 0.8 * under);
	vec3 sheenCol = mix(vec3(0.16, 0.02, 0.06), vec3(0.75, 0.3, 0.38), glint);
	vec3 col = base * coarse + sheenCol * sheen * fine * (0.8 + 1.6 * glint);
	col *= mix(1.15, 0.72, x);                        // 羽尖更暗
	col *= 1.0 - 0.35 * smoothstep(0.12, 0.0, u) * (1.0 - shaft);   // 羽轴两侧一道浅沟
	// 背光：外侧羽缘被勾出红边，最边上一线发白；薄的地方透一点红
	float edge = smoothstep(0.72, 1.0, u) * mix(1.0, 0.25, side);
	col += vec3(0.95, 0.06, 0.14) * edge * rim * 0.75 * fine;
	col += vec3(1.0, 0.8, 0.78) * pow(edge, 6.0) * rim * 0.45;
	col += vec3(1.0, 0.4, 0.42) * lit * (0.1 + 0.5 * fine * sheen);
	vec3 shaftCol = mix(vec3(0.5, 0.1, 0.15), vec3(1.0, 0.8, 0.8), clamp(glint + lit * 0.6 + rim * 0.2, 0.0, 1.0));
	col = mix(col, shaftCol, shaft * 0.85);
	float a = max(vane * mix(0.98, 0.82, smoothstep(0.65, 1.0, u)), shaft);
	COLOR = vec4(col, a * alpha);
}" };

	/// <summary>羽翼背后的背光：两片朝外上方张开的柔光（不是圆）。</summary>
	private static Shader WingGlow => _wingGlow ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float alpha = 1.0;
uniform float open = 1.0;
uniform float clock = 0.0;
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1.0, 0.0)), f.x), mix(h21(i + vec2(0.0, 1.0)), h21(i + vec2(1.0, 1.0)), f.x), f.y); }
void fragment() {
	vec2 p = (UV - vec2(0.5, 0.62)) * vec2(2.0, 2.0);
	float ax = abs(p.x);
	// 两瓣：沿斜向上的轴拉长
	vec2 q = vec2(ax - 0.34 * open, p.y + 0.22 * open);
	float c = cos(0.6), s = sin(0.6);
	vec2 r = vec2(q.x * c - q.y * s, q.x * s + q.y * c);
	float lobe = exp(-(r.x * r.x * 3.2 / max(0.2, open * open) + r.y * r.y * 11.0));
	float core = exp(-(p.x * p.x * 9.0 + (p.y + 0.05) * (p.y + 0.05) * 5.0));
	float rays = 0.9 + 0.1 * vn(p * 3.0 + clock * 0.2);
	float v = (lobe * 0.9 + core * 0.4) * rays;
	vec3 col = mix(vec3(0.9, 0.04, 0.12), vec3(1.0, 0.55, 0.55), smoothstep(0.55, 1.2, v));
	COLOR = vec4(col * v * alpha, 1.0);
}" };

	private sealed class Plume
	{
		public Node2D Node = null!;
		public ShaderMaterial Mat = null!;
		public Vector2 Root, Slot, Vel, Pos;
		public float Ang, Len, Span, T0, Spin, Rot, Phi, PhiSpeed, Sway, SwayAmp, Fall, Side, Fade;
		public bool Loose, Front, Vanish;
	}

	public static void WraithFormV3(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = c.Back ?? c.Front, front = c.Front;
		const float life = 2.7f, open = 0.26f, burst = 0.86f;
		Sfx.Play(Sfx.WraithForm, 0.5f);
		S("gx_fan_open", 0.2f, open);
		S("gx_whoosh", 0.24f, burst - 0.04f);
		Vector2 feet = c.Feet, chest = c.Chest;
		Vector2 origin = chest + new Vector2(-f * h * 0.04f, h * 0.06f);

		Dim(back, chest + new Vector2(0f, -h * 0.2f), new Vector2(h * 4.6f, h * 3.4f), life,
			s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.3f)) * (1f - Ease5.Seg(s, life - 0.9f, 0.9f)));

		float Flashed(float s) => s > burst ? Mathf.Exp(-(s - burst) * 4.5f) : 0f;

		// 背光
		{
			Vector2 size = new(h * 4.2f, h * 3.2f);
			Node2D root = Fx.Root(back, origin, "P5WraithGlow", life + 0.05);
			ShaderMaterial m = new() { Shader = WingGlow };
			root.AddChild(new ColorRect { Size = size, Position = new Vector2(-size.X / 2f, -size.Y * 0.62f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
			Fx.Animate(root, 0, life, t =>
			{
				float s = t * life;
				float a = Fx.OutCubic(Ease5.Seg(s, 0.05f, 0.5f)) * (1f - Ease5.Seg(s, burst + 0.7f, 1.0f));
				m.SetShaderParameter("open", 0.25f + 0.75f * Ease5.OutQuint(Ease5.Seg(s, open, 0.5f)) + 0.25f * Flashed(s));
				m.SetShaderParameter("alpha", a * 0.42f + 0.3f * Flashed(s));
				m.SetShaderParameter("clock", s);
			});
		}
		// 人：半透明的苍白
		if (c.SelfTint is { } tint)
		{
			Node2D tick = Fx.Root(front, feet, "P5WraithTint", life + 0.05);
			Color ghost = new(1f, 0.82f, 0.86f, 0.58f);
			Fx.Animate(tick, 0, life, t =>
			{
				float s = t * life;
				float k = Ease5.Seg(s, 0.12f, 0.35f) * (1f - Ease5.Seg(s, life - 0.8f, 0.7f));
				tint(Colors.White.Lerp(ghost, k));
			});
			tick.TreeExiting += () => tint(Colors.White);
		}

		// 羽毛
		List<Plume> plumes = new();
		Plume Make(Control layer, float len, float bendK)
		{
			Node2D n = new() { Name = "P5Plume", Visible = false };
			layer.AddChild(n);
			Fx.FreeAfter(n, life + 0.1);
			ShaderMaterial m = new() { Shader = Feather };
			m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
			m.SetShaderParameter("bend", bendK);
			m.SetShaderParameter("alpha", 0f);
			float wid = len * 0.4f;
			n.AddChild(new ColorRect { Size = new Vector2(len, wid), Position = new Vector2(0f, -wid / 2f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
			Plume p = new() { Node = n, Mat = m, Len = len, Phi = Fx.Rand(-0.3f, 0.3f), PhiSpeed = Fx.Rand(1.0f, 2.2f) * (Fx.Rand(0f, 1f) < 0.5f ? -1f : 1f),
				Sway = Fx.Rand(0f, Mathf.Tau), SwayAmp = Fx.Rand(0.5f, 1f), Fall = Fx.Rand(0.75f, 1.25f), Fade = Fx.Rand(0f, 0.35f) };
			plumes.Add(p);
			return p;
		}
		// 翼骨：从背后往外上方伸出去再往下弯
		Vector2 Bone(float sgn, float s) => origin + new Vector2(sgn * h * (0.1f + 0.92f * s), -h * (0.12f + 0.62f * Mathf.Sin(s * 2.05f)));
		foreach (float sgn in new[] { -1f, 1f })
		{
			// 三层：先画飞羽（最底），再次级，再覆羽
			(int n, float s0, float s1, float a0, float a1, float len0, float len1, float off)[] rows =
			{
				(13, 0.4f, 1.0f, 1.3f, 0.0f, 0.5f, 0.72f, 0f),      // 初级飞羽：从朝下一直扇到朝外
				(11, 0.04f, 0.5f, 1.5f, 1.25f, 0.42f, 0.52f, 0.02f),   // 次级飞羽：朝下
				(13, 0.06f, 0.97f, 1.3f, 0.35f, 0.24f, 0.34f, -0.015f), // 覆羽：盖住羽根
				(10, 0.1f, 0.92f, 1.15f, 0.45f, 0.14f, 0.2f, -0.035f),   // 小覆羽
			};
			for (int r = 0; r < rows.Length; r++)
			{
				var row = rows[r];
				for (int i = 0; i < row.n; i++)
				{
					float k = row.n == 1 ? 0f : i / (float)(row.n - 1);
					float s = Mathf.Lerp(row.s0, row.s1, k);
					float a = Mathf.Lerp(row.a0, row.a1, k) + Fx.Rand(-0.05f, 0.05f);
					float len = h * Mathf.Lerp(row.len0, row.len1, Mathf.Sin(k * Mathf.Pi * 0.75f)) * Fx.Rand(0.94f, 1.06f);
					Plume p = Make(back, len, 0.22f * sgn * -1f);
					p.Side = sgn;
					p.Span = s;
					p.Slot = Bone(sgn, s) + new Vector2(0f, row.off * h);
					p.Ang = sgn > 0 ? a : Mathf.Pi - a;      // a：从水平朝外往下转的角度
					p.Root = origin;
					p.T0 = open + s * 0.24f + r * 0.03f;
					p.Vanish = Fx.Rand(0f, 1f) < 0.5f;
				}
			}
		}
		// 主拍时额外迸出的散羽（前排，落下时从他身上穿过去）
		for (int i = 0; i < 18; i++)
		{
			Plume p = Make(i % 3 == 0 ? back : front, h * Fx.Rand(0.16f, 0.34f), Fx.Rand(-0.3f, 0.3f));
			p.Loose = true;
			p.Front = i % 3 != 0;
			p.Root = origin + new Vector2(Fx.Rand(-0.25f, 0.25f) * h, Fx.Rand(-0.35f, 0.25f) * h);
			p.Ang = Fx.Rand(0f, Mathf.Tau);
			p.T0 = burst + Fx.Rand(0f, 0.05f);
			p.Side = Fx.Rand(0f, 1f) < 0.5f ? -1f : 1f;
		}

		bool broke = false;
		float last = 0f;
		Node2D driver = Fx.Root(back, origin, "P5WraithPlumes", life + 0.05);
		Fx.Animate(driver, 0, life, t =>
		{
			float s = t * life, dt = Mathf.Clamp(s - last, 0f, 0.05f);
			last = s;
			if (!broke && s >= burst)
			{
				broke = true;
				foreach (Plume p in plumes)
				{
					Vector2 from = p.Loose ? p.Root : p.Slot + Vector2.Right.Rotated(p.Ang) * p.Len * 0.4f;
					Vector2 dir = (from - origin).Normalized();
					if (p.Loose) dir = Vector2.Right.Rotated(Fx.Rand(0f, Mathf.Tau));
					p.Pos = p.Loose ? p.Root : p.Slot;
					p.Rot = p.Ang;
					p.Vel = (dir * Fx.Rand(380f, 1050f) + new Vector2(0f, -Fx.Rand(60f, 300f))) * u * (p.Loose ? 0.9f : 1f);
					p.Spin = Fx.Rand(-7f, 7f);
				}
			}
			float sweep = Ease5.Seg(s, open + 0.3f, 0.34f) * 1.5f - 0.25f;    // 光泽扫过翼面：翼根 → 翼尖
			foreach (Plume p in plumes)
			{
				if (s < p.T0) continue;
				Node2D n = p.Node;
				n.Visible = true;
				float rimK = 1f, glint = 0f, alpha = 1f, tilt, under = 0f;
				if (s < burst && !p.Loose)
				{
					// 展开：从背后滑到位、由收拢转到张开；到位后微微呼吸
					float e = Ease5.Seg(s, p.T0, 0.3f);
					float eo = Ease5.OutQuint(e);
					float overs = Mathf.Sin(e * Mathf.Pi) * 0.12f * (1f - e);
					Vector2 pos = p.Root.Lerp(p.Slot, eo);
					float folded = p.Side > 0 ? 1.45f : Mathf.Pi - 1.45f;
					float breathe = Mathf.Sin(s * 5f + p.Span * 3f) * 0.025f * e;
					float ang = Mathf.LerpAngle(folded, p.Ang, eo) + (overs + breathe) * -p.Side;
					n.GlobalPosition = pos;
					n.Rotation = ang;
					n.Scale = new Vector2(0.35f + 0.65f * eo, (0.5f + 0.5f * eo) * (p.Side > 0 ? 1f : -1f));
					alpha = Mathf.Min(1f, e * 5f);
					tilt = Mathf.Sin(s * 2f + p.Span * 4f) * 0.4f;
					glint = Mathf.Exp(-Mathf.Pow((p.Span - sweep) / 0.14f, 2f));
				}
				else
				{
					if (!broke) continue;
					float since = s - Mathf.Max(burst, p.T0);
					// 迸开 → 强阻尼悬停 → 飘落（左右荡、翻面）
					float settle = Ease5.Seg(since, 0.22f, 0.45f);
					float drag = Mathf.Exp(-dt * Mathf.Lerp(5.2f, 1.7f, settle));
					p.Vel *= drag;
					float swayV = Mathf.Cos(since * 3.1f * p.Fall + p.Sway);
					p.Vel += new Vector2(swayV * 260f * p.SwayAmp * settle, (230f + 150f * Mathf.Abs(swayV)) * p.Fall * settle) * u * dt * 3f;
					p.Pos += p.Vel * dt;
					p.Spin *= Mathf.Exp(-dt * 3f);
					p.Rot += (p.Spin + swayV * 1.1f * settle) * dt;
					p.Phi += p.PhiSpeed * dt * (0.5f + 1.2f * settle + 2f * Mathf.Exp(-since * 4f));
					float cy = Mathf.Cos(p.Phi);
					n.GlobalPosition = p.Pos;
					n.Rotation = p.Rot;
					float sy = (cy < 0f ? -1f : 1f) * Mathf.Max(0.55f, Mathf.Abs(cy));
					n.Scale = new Vector2(1f, sy * (p.Side > 0 ? 1f : -1f));
					tilt = Mathf.Sin(p.Phi);
					under = cy < 0f ? 1f : 0f;
					float inK = Mathf.Min(1f, since * 12f);
					float outAt = life - 1.0f + p.Fade * 0.9f;
					alpha = inK * (1f - Ease5.Seg(s, outAt, 0.5f));
					if (p.Vanish) alpha *= 1f - Ease5.Seg(since, 0.12f, 0.4f);
					// 落到脚下那条线附近就淡掉（落进地里）
					alpha *= 1f - Ease5.Seg(p.Pos.Y, feet.Y - h * 0.02f, h * 0.14f);
					rimK = 1.25f;
					glint = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(p.Phi * 2f + p.Sway)), 12f) * 0.8f;
				}
				p.Mat.SetShaderParameter("alpha", alpha);
				p.Mat.SetShaderParameter("tilt", tilt);
				p.Mat.SetShaderParameter("under", under);
				p.Mat.SetShaderParameter("glint", glint);
				p.Mat.SetShaderParameter("rim", rimK * Fx.OutCubic(Ease5.Seg(s, 0.1f, 0.5f)));
				p.Mat.SetShaderParameter("lit", Flashed(s));
			}
		});

		// 展开时羽根处一点碎光
		Fx.After(front, open + 0.05f, () =>
			Glow.Sparks(back, origin, 22, 80f * u, 420f * u, 0.6f, new[] { new Color(1f, 0.95f, 0.94f), new Color(1f, 0.25f, 0.32f) }, 60f * u, 0.012f * u, 0.035f * u, Vector2.Up, 150f));
		// ③ 主拍
		Fx.After(front, burst, () =>
		{
			Glow.Flash(back, origin + new Vector2(0f, -h * 0.25f), new Color(1f, 0.14f, 0.22f), h * 2.4f, 0.4f);
			Glow.Flash(back, origin, new Color(1f, 0.93f, 0.92f), h * 1.1f, 0.18f);
			HitStop.Request(0.06);
			Fx.Shake(2);
			Glow.Sparks(front, origin, 46, 200f * u, 900f * u, 0.8f,
				new[] { new Color(1f, 0.96f, 0.95f), new Color(1f, 0.3f, 0.38f), new Color(0.6f, 0.03f, 0.1f) }, 200f * u, 0.012f * u, 0.04f * u, Vector2.Up, 180f);
		});
		Glitter(front, feet, h, u, burst + 0.2f, 1.2f, 10, 0.7f);
	}
}
