using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 毒性爆发（金卡专属，用户 2026-11-01）：亚森的雾沉进画面深处，从每个敌人身后的深处扑出一只手，侧握（手心朝左）一把攥住敌人，
/// 停一拍后所有手同时「捏爆」（= 牌的结算：上毒 + 立刻触发中毒，游戏结算推到这一刻，Aim.PlayCast），之后一直死死握紧（触媒多结算的每一轮只让敌人闪一下），
/// 结算完松开、缩回深处。手是 Codex 画的 depth_hand（stage_p5.py 把握住的两帧切成「手掌在敌人后面 / 手指在敌人前面」两层）。
/// 深度全靠动画：从敌人身后一个又小、又暗的点 0.13s 放大变亮扑出来（在敌人后面的图层），握住那一刻手指换到敌人前面的图层；
/// 雾丝越往深处越细越暗。
/// </summary>
public static class DepthGrip
{
	/// <summary>攥住之后多久捏爆（= 游戏结算推后多少，Aim 用）。</summary>
	public const float CrushDelay = 0.2f;    // 前半段：手伸到敌人身上虚握着；玻璃碎开那一下才狠狠捏爆

	private const float Img = 627f;
	// depth_hand_v2：握住的地方（四指扣住的中心）、远处的手腕（画面上方，烟往上后方散）
	private static readonly Vector2 Hold = new(0.45f * Img, 0.6f * Img), WristPt = new(0.5f * Img, 0.2f * Img);

	private static Shader? _strand;

	/// <summary>雾丝：a（UV.x=0）宽 w0、b 宽 w1；越细的地方越暗（远）；head 从 a 往 b 显示到哪；pulse 一团光。</summary>
	private static Shader StrandShader => _strand ??= new Shader { Code = @"
shader_type canvas_item;
uniform float head = 1.0;
uniform float alpha = 0.5;
uniform float w0 = 0.8;
uniform float w1 = 0.2;
uniform float pulse = -1.0;
uniform float clock = 0.0;
uniform float seed = 0.0;
uniform float aspect = 8.0;
uniform float gx0 = 0.0;        // 这一段在整条雾丝上从哪到哪（雾丝是一串小段拼成的弯曲带子）
uniform float gx1 = 1.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vnoise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i + seed), hash(i + vec2(1.0, 0.0) + seed), f.x), mix(hash(i + vec2(0.0, 1.0) + seed), hash(i + vec2(1.0, 1.0) + seed), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * vnoise(p); p = p * 2.03 + 1.7; a *= 0.5; } return v; }
void fragment() {
	float x = mix(gx0, gx1, UV.x);
	float y = (UV.y - 0.5) * 2.0;
	vec2 q = vec2(x * aspect, y);
	float w = mix(w0, w1, x);
	float wob = (fbm(vec2(x * aspect * 0.4 - clock * 1.5, 3.0)) - 0.5) * 0.6 * w;
	float d = abs(y - wob) / max(w, 0.01);
	float body = smoothstep(1.0, 0.15, d + (fbm(q * vec2(0.9, 2.5) - vec2(clock * 3.0, 0.0)) - 0.5) * 0.9);
	float wisp = smoothstep(0.6, 0.8, fbm(q * vec2(1.6, 4.0) - vec2(clock * 4.5, 0.0))) * body;
	float far = 1.0 - clamp(w / max(max(w0, w1), 0.01), 0.0, 1.0);   // 细 = 远：更暗更淡
	float on = smoothstep(head, head - 0.04, x) * smoothstep(0.0, 0.04, x) * smoothstep(1.0, 0.97, x);
	float p = pulse >= 0.0 ? exp(-pow((x - pulse) / 0.05, 2.0)) * smoothstep(1.0, 0.3, d) : 0.0;
	vec3 col = mix(vec3(0.18, 0.0, 0.05), vec3(0.85, 0.07, 0.14), wisp) * (1.0 - 0.55 * far);
	col = mix(col, vec3(0.85, 0.45, 1.0), p);
	float a = (body * 0.5 + wisp * 0.7) * alpha * (1.0 - 0.5 * far) + p * 0.9;
	COLOR = vec4(col, clamp(a, 0.0, 1.0) * on);
}" };

	/// <summary>
	/// 雾丝（用户 2026-11-02：亚森到手的连接太直勾勾）：沿一条二次贝塞尔曲线排的一串小段，曲线的拱度随时间轻轻飘，
	/// 每段用同一个着色器、按在整条线上的位置取宽度 / 噪声 / 显示进度，所以拼起来是一条连续弯曲的雾带。
	/// </summary>
	private sealed class Strand
	{
		public readonly List<(ColorRect R, ShaderMaterial M, Node2D N)> Segs = new();
		public float Px, Bend, Seed;

		public void Set(string name, Variant v)
		{
			foreach (var (_, m, _) in Segs) m.SetShaderParameter(name, v);
		}
	}

	private const int SegCount = 12;

	private static Strand MakeStrand(Node2D parent, float w0, float w1, float px, float bend)
	{
		Strand st = new() { Px = px, Bend = bend, Seed = Fx.Rand(0f, 40f) };
		for (int i = 0; i < SegCount; i++)
		{
			Node2D n = new();
			parent.AddChild(n);
			ColorRect r = new() { MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = StrandShader } };
			n.AddChild(r);
			ShaderMaterial m = (ShaderMaterial)r.Material;
			m.SetShaderParameter("w0", w0);
			m.SetShaderParameter("w1", w1);
			m.SetShaderParameter("seed", st.Seed);
			m.SetShaderParameter("head", 0f);
			m.SetShaderParameter("gx0", (float)i / SegCount);
			m.SetShaderParameter("gx1", (float)(i + 1) / SegCount);
			r.Size = new Vector2(10f, px);
			r.Position = new Vector2(0f, -px / 2f);
			Fx.Clock(r);
			st.Segs.Add((r, m, n));
		}
		return st;
	}

	/// <summary>把雾丝摆成 a → b 的一条弧（拱向 bend 那一侧，拱度随 time 轻轻飘动）。</summary>
	private static void Place(Strand s, Vector2 a, Vector2 b, float time)
	{
		float len = Mathf.Max(1f, a.DistanceTo(b));
		Vector2 dir = (b - a) / len, nrm = new(-dir.Y, dir.X);
		float sway = 1f + 0.18f * Mathf.Sin(time * 2.3f + s.Seed) + 0.08f * Mathf.Sin(time * 5.1f + s.Seed * 2f);
		Vector2 ctrl = (a + b) / 2f + nrm * len * s.Bend * sway + dir * len * 0.08f * Mathf.Sin(time * 1.7f + s.Seed);
		Vector2 P(float t) => a * (1 - t) * (1 - t) + ctrl * 2 * t * (1 - t) + b * t * t;
		float total = 0f;
		var pts = new Vector2[SegCount + 1];
		for (int i = 0; i <= SegCount; i++) pts[i] = P((float)i / SegCount);
		for (int i = 0; i < SegCount; i++) total += pts[i].DistanceTo(pts[i + 1]);
		for (int i = 0; i < SegCount; i++)
		{
			var (r, m, n) = s.Segs[i];
			Vector2 p0 = pts[i], p1 = pts[i + 1];
			float l = p0.DistanceTo(p1) + 2f;                              // 每段多出 2px 盖住接缝
			n.GlobalPosition = p0;
			n.Rotation = (p1 - p0).Angle();
			r.Size = new Vector2(l, s.Px);
			m.SetShaderParameter("aspect", total / s.Px);
		}
	}

	private sealed class Hand
	{
		public Vector2 Center, Grip, Deep;
		public float H, K;
		public Node2D BackN = null!, FrontN = null!;
		public Sprite2D Back = null!, Front = null!;
		public ShaderMaterial BackM = null!, FrontM = null!;
		public Strand Branch = null!;
		public Sprite2D Glow = null!;
		public Action<Color>? Tint;
	}

	private sealed class Live
	{
		public ulong LastTick;
		public int Ticks;
		public float CrushAt = -1f, SqueezeAt = -1f;
	}

	private static Live? _live;
	private static readonly HashSet<ulong> Ids = new();

	/// <summary>游戏结算了一轮毒伤（PoisonTickPatch）：毒性爆发攥着时，第一轮 = 捏爆，之后每轮再捏一下。</summary>
	public static void OnTick(ulong targetId)
	{
		if (_live != null && Ids.Contains(targetId))
		{
			_live.LastTick = Stage.NowMs();
			_live.Ticks++;
		}
	}

	/// <param name="front">人物前面的特效层</param>
	/// <param name="back">人物后面的特效层（握住前的手、手掌、雾丝）</param>
	/// <param name="from">亚森伸出手的地方</param>
	/// <param name="enemies">(节点 id, 中心, 中心到头顶的距离, 上色)</param>
	/// <param name="attack">攥住的时刻（= 召唤高潮那一拍），捏爆在 attack + CrushDelay</param>
	public static void Play(Control front, Control back, Vector2 from, IReadOnlyList<(ulong Id, Vector2 Center, float H, float W, Action<Color>? Tint)> enemies,
		float attack)
	{
		if (Fx.Tex("depth_open") is not Texture2D tOpen || enemies.Count == 0)
		{
			return;
		}
		Texture2D tClose = Fx.Tex("depth_close") ?? tOpen;
		Texture2D tGripB = Fx.Tex("depth_grip_back") ?? tOpen, tGripF = Fx.Tex("depth_grip_front") ?? tOpen;
		Texture2D tTightB = Fx.Tex("depth_grip_tight_back") ?? tGripB, tTightF = Fx.Tex("depth_grip_tight_front") ?? tGripF;
		Texture2D tCloseB = Fx.Tex("depth_close_back") ?? tGripB, tCloseF = Fx.Tex("depth_close_front") ?? tGripF;
		Ids.Clear();
		Live live = new();
		_live = live;
		const float maxHold = 3.6f, back0 = 0.25f;
		float life = attack + maxHold + 1f;
		Node2D rootB = Fx.Root(back, Vector2.Zero, "P5DepthGripBack", life);
		Node2D rootF = Fx.Root(front, Vector2.Zero, "P5DepthGripFront", life);
		rootB.GlobalPosition = rootF.GlobalPosition = Vector2.Zero;

		// 深处的汇合点：亚森和敌人之间、往上 —— 亚森的雾先沉到这里，再分到每只手
		Vector2 sum = Vector2.Zero;
		float hMax = 80f;
		foreach (var e in enemies) { sum += e.Center; hMax = Mathf.Max(hMax, e.H); }
		Vector2 groupC = sum / enemies.Count;
		Vector2 deepPt = from.Lerp(groupC, 0.55f) + new Vector2(0f, -hMax * 1.4f);
		Strand trunk = MakeStrand(rootB, 0.85f, 0.18f, hMax * 1.0f, -0.22f);           // 往上拱、绕一点弯沉进深处
		Place(trunk, from, deepPt, 0f);

		var hands = new List<Hand>();
		foreach (var (id, c, h, bodyW, tint) in enemies)
		{
			Ids.Add(id);
			// 手的大小 / 位置按敌人的宽度定（用户 2026-11-02：手像直接穿透了敌人）：四指从指尖（贴图 x≈0.04）到切开处（DEPTH_CUT 0.66）
			// 横跨整个敌人，切开处（手掌开始的地方）落在敌人身体右侧边缘外一点 —— 手掌露在敌人身后的边上，手指绕过敌人正面
			// 正好握住敌人（用户 2026-11-02：手太大）：手指横跨敌人宽度、四指那几排的高度约等于敌人身高，两者取小
			float halfW = Mathf.Clamp(bodyW * 0.5f, h * 0.45f, h * 1.1f);
			float k = Mathf.Min(halfW * 1.75f / ((0.66f - 0.04f) * Img), h * 1.5f / (0.57f * Img));
			Hand hd = new() { Center = c, H = h, K = k, Tint = tint };
			hd.Grip = c + new Vector2(halfW * 0.85f, 0f) - (new Vector2(0.66f * Img, Hold.Y) - new Vector2(Img / 2f, Img / 2f)) * k;
			hd.Deep = c + new Vector2(h * 0.15f, -h * 1.3f);                        // 敌人身后上方的深处（手从上后方扑下来）
			hd.Branch = MakeStrand(rootB, 0.12f, 0.5f, h * 0.8f, Fx.Rand(0.12f, 0.24f) * (hands.Count % 2 == 0 ? 1f : -1f));
			Sprite2D Make(Node2D parent, Texture2D t, out Node2D node, out ShaderMaterial m)
			{
				node = new Node2D { Visible = false };
				parent.AddChild(node);
				m = new ShaderMaterial { Shader = Grip.HandShader };
				m.SetShaderParameter("seed", Fx.Rand(0f, 40f));
				m.SetShaderParameter("px", new Vector2(1f / Img, 1f / Img));
				Sprite2D sp = new() { Texture = t, Scale = Vector2.One * k, Material = m, TextureFilter = CanvasItem.TextureFilterEnum.Linear };
				node.AddChild(sp);
				Fx.Clock(sp);
				return sp;
			}
			hd.Back = Make(rootB, tOpen, out hd.BackN, out hd.BackM);
			hd.Front = Make(rootF, tGripF, out hd.FrontN, out hd.FrontM);
			hd.FrontM.SetShaderParameter("aura_k", 0f);
			// 指缝里渗出的紫黑毒光（握住到捏爆之间越来越亮）
			hd.Glow = Glow.SoftSprite(rootF, c, new Color(0.62f, 0.2f, 1f), 0.1f);
			hd.Glow.Modulate = new Color(0.62f, 0.2f, 1f, 0f);
			hands.Add(hd);
		}

		float launch = attack - 0.2f, travelTrunk = 0.07f, emerge = 0.13f;
		// 亚森手边：雾一收再射进深处
		Fx.After(front, Mathf.Max(0f, launch - 0.18f), () =>
			Glow.Gather(back, from, new Color(0.85f, 0.05f, 0.1f), hMax * 1.6f, 0.18f));
		Fx.After(front, Mathf.Max(0f, launch), () =>
		{
			Glow.Shockwave(back, from, new Color(0.9f, 0.05f, 0.1f), hMax * 1.8f, 0.3f, 0.5f);
			Glow.Ripple(back, from, hMax * 2.4f, 0.3f, 0.03f);
		});
		// 每只手扑出来那一下：深处一圈扭曲 + 速度线（在敌人后面）
		foreach (Hand hd in hands)
		{
			Hand h0 = hd;
			Fx.After(front, Mathf.Max(0f, launch + travelTrunk), () =>
			{
				Glow.Ripple(back, h0.Deep, h0.H * 2.2f, 0.3f, 0.035f);
				Glow.SpeedLines(back, h0.Grip, new Color(0.85f, 0.1f, 0.16f), 14, h0.H * 0.5f, h0.H * 2.2f, 0.25f);
			});
		}

		bool crushed = false, released = false;
		float releaseAt = -1f, lastSqueeze = -1f;
		int seenTicks = 0;
		Fx.Animate(rootB, 0, life, t =>
		{
			float s = t * life;
			float sl = s - launch;
			// 主干雾丝：射进深处；之后变淡留着；松开后缩回
			float trunkHead = sl < 0f ? 0f : Ease5.OutExpo(Mathf.Min(1f, sl / travelTrunk)) * 1.03f;
			float relK = released ? Mathf.Clamp((s - releaseAt) / (back0 + 0.15f), 0f, 1f) : 0f;
			Place(trunk, from, deepPt, s);
			trunk.Set("head", trunkHead * (1f - Fx.InCubic(relK)));
			trunk.Set("alpha", sl < travelTrunk ? 0.9f : 0.5f);

			// 捏爆：第一轮毒伤（或到点兜底）
			if (!crushed && s >= attack && (live.Ticks > 0 || s >= attack + CrushDelay + 0.2f))
			{
				crushed = true;
				seenTicks = Math.Max(live.Ticks, 1);
				live.CrushAt = s;
				lastSqueeze = s;
				Crush(front, back, hands);
			}
			else if (crushed && !released && live.Ticks > seenTicks)
			{
				seenTicks = live.Ticks;
				lastSqueeze = s;
				Sfx.Play(Sfx.PoisonTick, 0.7f);
				foreach (Hand hd in hands)
				{
					Glow.Flash(front, hd.Center, new Color(0.65f, 0.25f, 1f), hd.H * 1.4f, 0.22f);
					hd.Tint?.Invoke(new Color(0.75f, 0.3f, 0.9f));
				}
			}
			if (crushed && !released)
			{
				bool quiet = Stage.NowMs() - live.LastTick > 400;
				if ((s >= live.CrushAt + 0.35f && quiet) || s >= attack + maxHold)
				{
					released = true;
					releaseAt = s;
					_live = null;
					foreach (Hand hd in hands) hd.Tint?.Invoke(Colors.White);
				}
			}

			float squeeze = crushed && live.CrushAt >= 0f ? Mathf.Exp(-(s - live.CrushAt) / 0.08f) : 0f;   // 只有捏爆那一下
			bool tight = crushed;                                   // 用户 2026-11-02：握紧了就一直握紧（不是每轮毒伤捏一下）
			foreach (Hand hd in hands)
			{
				Place(hd.Branch, deepPt, hd.BackN.GlobalPosition + (WristPt - new Vector2(Img / 2f, Img / 2f)) * hd.K * hd.BackN.Scale.X, s);
				float e = Mathf.Clamp((sl - travelTrunk) / emerge, 0f, 1f);                     // 扑出来的进度
				hd.Branch.Set("head", (sl < travelTrunk ? 0f : Ease5.OutExpo(Mathf.Min(1f, (sl - travelTrunk) / 0.05f)) * 1.03f) * (1f - Fx.InCubic(relK)));
				hd.Branch.Set("alpha", 0.55f);
				bool shown = sl >= travelTrunk && !(released && relK >= 1f);
				hd.BackN.Visible = shown;
				// 深度：从深处的小暗点放大变亮扑到位（缓出）；松开后反过来缩回深处
				float d = released ? 1f - Fx.InCubic(Mathf.Clamp((s - releaseAt) / back0, 0f, 1f)) : Ease5.OutQuint(e);
				float scale = Mathf.Lerp(0.14f, 1f, d);
				Vector2 pos = hd.Deep.Lerp(hd.Grip, d);
				float bright = Mathf.Lerp(0.25f, 1f, d);
				bool gripped = s >= attack && !released;
				hd.FrontN.Visible = gripped;
				// 握住前张开扑来；握住后先虚握（半弯的手指贴在敌人身上）；捏爆那一下狠狠攥紧，之后一直攥着
				hd.Back.Texture = gripped ? (tight ? tTightB : tCloseB) : tOpen;
				hd.Front.Texture = tight ? tTightF : tCloseF;
				float sq = gripped ? 1f - 0.16f * squeeze - 0.03f * Mathf.Exp(-(s - attack) / 0.07f) : 1f;   // 捏爆那一下明显往里一缩
				float shake = gripped ? Mathf.Sin(s * 70f) * 1.6f * (0.3f + squeeze) : 0f;
				hd.BackN.GlobalPosition = hd.FrontN.GlobalPosition = pos + new Vector2(shake, shake * 0.4f);
				hd.BackN.Scale = hd.FrontN.Scale = new Vector2(scale * sq, scale * (2f - sq));
				hd.BackN.Modulate = new Color(bright, bright, bright, Mathf.Min(1f, d * 3f));
				hd.BackM.SetShaderParameter("dissolve", released ? Mathf.Clamp((s - releaseAt) / back0, 0f, 1f) * 0.6f : (1f - Mathf.Min(1f, e * 2.5f)) * 0.5f);
				float hot = gripped ? 0.5f * Mathf.Max(squeeze, Mathf.Exp(-(s - attack) / 0.06f)) : 0f;
				hd.BackM.SetShaderParameter("hot", hot);
				hd.FrontM.SetShaderParameter("hot", hot);
				// 毒光：握住后在指缝里越来越亮，捏爆时炸掉，之后随每次捏一闪
				float g = gripped ? (crushed ? 0.35f + 0.5f * squeeze : Mathf.Clamp((s - attack) / CrushDelay, 0f, 1f)) : 0f;
				hd.Glow.Modulate = new Color(0.62f, 0.2f, 1f, 0.9f * g);
				hd.Glow.Scale = Vector2.One * hd.H / 60f * (0.6f + 0.6f * g);
			}
			if (released && relK >= 1f)
			{
				rootB.Visible = rootF.Visible = false;
			}
		});
	}

	/// <summary>捏爆：所有手同一帧 —— 紫黑毒光炸开、墨点甩出去、咒焰贴身烧起来、敌人一闪；顿帧 + 低频。</summary>
	private static void Crush(Control front, Control back, List<Hand> hands)
	{
		// 拍 2（人格面具出手）：捏爆自己带顿帧 + 震屏 + 干净的冲击声
		HitStop.Request(0.08);
		Fx.Shake(2);
		Sfx.Play(Sfx.Hit, 0.8f);
		Sfx.Play(Sfx.SubBoom, 0.9f);
		foreach (Hand hd in hands)
		{
			Vector2 c = hd.Center;
			Glow.Flash(front, c, new Color(1f, 0.92f, 1f), hd.H * 1.2f, 0.1f);
			Glow.Flash(front, c, new Color(0.6f, 0.15f, 0.95f), hd.H * 2.8f, 0.35f);
			Glow.Shockwave(front, c, new Color(0.7f, 0.25f, 1f), hd.H * 3f, 0.4f);
			Brush.Drops(front, c, Vector2.Up, 18, Mathf.Pi, hd.H * 7f, hd.H * 0.07f, 0.5f);
			Glow.Sparks(front, c, 24, hd.H * 2f, hd.H * 6f, 0.5f, new[] { new Color(1f, 0.85f, 1f), new Color(0.65f, 0.25f, 1f), new Color(0.2f, 0f, 0.3f) },
				hd.H * 2f, 0.08f, 0.25f);
			Flame.Wrap(front, back, c + new Vector2(0f, hd.H * 0.95f), hd.H * 1.3f, hd.H * 2.3f, 0.02f, 0.6f);
			hd.Tint?.Invoke(new Color(0.55f, 0.18f, 0.7f));
			Action<Color>? tint = hd.Tint;
			if (tint != null)
			{
				// 动画的拥有者不能是图层本身：Fx.Animate 在延迟期间会把拥有者藏起来（之前整层前景特效闪没了两帧）
				Node2D holder = Fx.Root(front, Vector2.Zero, "P5CrushTint", 0.5);
				Fx.Animate(holder, 0.05, 0.4, u => tint(new Color(0.55f, 0.18f, 0.7f).Lerp(new Color(0.85f, 0.7f, 0.9f), u)));
			}
		}
	}
}
