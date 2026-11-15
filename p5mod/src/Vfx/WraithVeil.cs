using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 幽魂形态第八版「幻影之身」（2026-11-15）。
/// 前七版的教训：身后的大件（月亮、羽翼、裂隙）用着色器画不出质感，用户都否了；场景不用变；被打时把身体错开几截也不要；
/// 「无实体期间一直持续、结束时收回」这一点用户认可。
/// 这一版不加任何物件、不改场景：直接给 Joker 的身体换一套着色器，他本人变成幻影 ——
/// · 原画还在（不是剪影填色），褪成半透明的苍白；
/// · 轮廓上一圈流动的红白光（按图的透明度算出来的真轮廓，跟着每个动作走）；
/// · 身体里有光往上流；
/// · 打出时一道白热的线从脚扫到头，扫过的地方变成幻影；结束时从头扫回脚；
/// · 被打：轮廓亮一下、人更透一下。
/// 身后只有一团很淡的柔光和零星往上飘的光粒（衬托，不是主体）。
/// 游戏接法在 WraithPlay.cs；身体材质通过 SkillCtx.BodyMaterial 换（游戏里是骨骼动画节点的材质）。
/// </summary>
public sealed class WraithVeil
{
	public static WraithVeil? Current { get; private set; }

	/// <summary>无实体还在不在（游戏里设置；预览不设）。在场 2 秒后每半秒问一次，不在了就自己收掉。</summary>
	public static Func<bool>? StillOn;

	private const float SweepAt = 0.16f, SweepDur = 0.5f, EndDur = 0.6f, PreA = 0.04f, PreB = 0.2f, PreDur = 0.2f;

	private readonly Control _front, _back;
	private readonly Node2D _glow;
	private readonly Sprite2D _glowSprite, _lineRed, _lineHot;
	private readonly Node2D _lineRoot;
	private readonly ShaderMaterial _m;
	private readonly Action<ShaderMaterial?>? _setBody;
	private readonly Vector2 _feet;
	private readonly float _tall, _face, _unit;
	private readonly SceneTree _tree;
	private readonly double _t0;
	private float _t, _stopAt = -1f, _hitAt = -10f, _nextCheck = 2f, _nextApply, _nextMote = 0.7f, _nextSpark;
	private bool _done;

	private WraithVeil(SkillCtx c)
	{
		_front = c.Front;
		_back = c.Back ?? c.Front;
		_setBody = c.BodyMaterial;
		_feet = c.Feet;
		_tall = c.Tall;
		_face = c.Face;
		_unit = c.Unit;
		_tree = (SceneTree)Engine.GetMainLoop();
		_t0 = HitStop.RealSeconds();
		_m = new ShaderMaterial { Shader = Body };
		_m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
		_glow = new Node2D { Name = "P5WraithGlow" };
		_back.AddChild(_glow);
		_glow.GlobalPosition = _feet + new Vector2(0f, -0.52f * _tall);
		_glowSprite = Glow.SoftSprite(_glow, Vector2.Zero, new Color(1f, 0.1f, 0.18f, 0f), 1f);
		// 扫描的那道横光（前景，比身体宽，两头尖）
		_lineRoot = new Node2D { Name = "P5WraithLine" };
		_front.AddChild(_lineRoot);
		_lineRed = Glow.SoftSprite(_lineRoot, Vector2.Zero, new Color(1f, 0.12f, 0.2f, 0f), 1f);
		_lineHot = Glow.SoftSprite(_lineRoot, Vector2.Zero, new Color(1f, 0.95f, 0.93f, 0f), 1f);
		_tree.ProcessFrame += Tick;
		_glow.TreeExiting += Finish;
		Step();
	}

	/// <summary>打出幽魂形态。已经开着（再打一张）：只亮一下。</summary>
	public static void Start(SkillCtx c)
	{
		if (Current is { _done: false, _stopAt: < 0f } cur)
		{
			cur.Hit();
			return;
		}
		Current?.Finish();
		var w = new WraithVeil(c);
		Current = w;
		// 拍 1：冷的、往里收的一声（开始褪）；拍 2（主拍）：扫到头顶，整个人成了幻影
		Sfx.Play(Sfx.WraithForm, 0.5f);
		Sfx.Take("gx_freeze", 0.2f, 0.02f);
		Sfx.Take("gx_ping", 0.14f, SweepAt + SweepDur);
		Sfx.Take("gx_whoosh", 0.2f, SweepAt + SweepDur - 0.03f);
		float h = c.Tall, u = c.Unit;
		Control back = w._back;
		Vector2 mid = c.Feet + new Vector2(0f, -0.55f * h);
		SkillRedo5.Dim(back, mid, new Vector2(h * 3.4f, h * 2.6f), 1.5, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.3f)) * (1f - Ease5.Seg(s, 0.8f, 0.7f)) * 0.8f);
		// 两道很快的预扫：细一点的横光从脚扫到头，每道过去两边扬起一点尘
		foreach (float t0 in new[] { PreA, PreB })
		{
			Node2D pr = Fx.Root(c.Front, c.Feet, "P5WraithPre", t0 + PreDur + 0.1);
			Sprite2D red = Glow.SoftSprite(pr, Vector2.Zero, new Color(1f, 0.12f, 0.2f, 0f), 1f), hot = Glow.SoftSprite(pr, Vector2.Zero, new Color(1f, 0.95f, 0.93f, 0f), 1f);
			Vector2 feet0 = c.Feet;
			Fx.Animate(pr, t0, PreDur, t =>
			{
				pr.GlobalPosition = feet0 + new Vector2(0f, -1.18f * h * t);
				float vis = Mathf.Pow(Mathf.Sin(t * Mathf.Pi), 0.5f);
				float wide = h * (1.0f + 0.3f * Mathf.Sin(t * Mathf.Pi));
				red.Scale = new Vector2(wide, h * 0.045f) / 32f;
				hot.Scale = new Vector2(wide * 0.75f, h * 0.012f) / 32f;
				red.Modulate = new Color(1f, 0.12f, 0.2f, 0.7f * vis);
				hot.Modulate = new Color(1f, 0.95f, 0.93f, 0.9f * vis);
			});
			Sfx.Take("gx_tick", 0.1f, t0);
		}
		// 光线往胸口收（和影刃旋风同一种做法）
		Vector2 chest = c.Chest;
		for (int i = 0; i < 24; i++)
		{
			float ang = Mathf.Tau * i / 24f + Fx.Rand(-0.1f, 0.1f);
			Vector2 dir = new(Mathf.Cos(ang), Mathf.Sin(ang) * 0.8f);
			float r0 = h * Fx.Rand(0.55f, 0.85f), r1 = h * Fx.Rand(0.06f, 0.14f);
			float delay = Fx.Rand(0f, 0.2f), dur = Fx.Rand(0.18f, 0.26f);
			Node2D lr = Fx.Root(c.Front, chest, "P5WraithIn", delay + dur + 0.1);
			Polygon2D ln = Glow.TaperPoly(lr, i % 3 == 0 ? Pal5.Hot : new Color(1f, 0.16f, 0.24f));
			float lw = Fx.Rand(2f, 4.5f) * u;
			ln.Visible = false;
			Fx.Animate(ln, delay, dur, t =>
			{
				ln.Visible = true;
				float hd = Fx.InCubic(Mathf.Min(1f, t * 1.3f)), tl = Fx.InCubic(t);
				ln.Polygon = Glow.TaperPts(dir * Mathf.Lerp(r0 * 1.25f, r1, tl), dir * Mathf.Lerp(r0, r1, hd), lw);
				ln.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 4f) * (1f - Ease5.Seg(t, 0.8f, 0.2f)));
			});
		}
		Fx.After(c.Front, SweepAt + SweepDur, () =>
		{
			HitStop.Request(0.06);
			Fx.Shake(2);
			SkillRedo5.Cross(c.Front, chest, h * 1.1f);
			// 几道疾光贴着身体从脚下往上冲出头顶（前后都有）
			for (int k = 0; k < 9; k++)
			{
				float fx = (k - 4f) / 4f;
				float x = fx * h * 0.3f + Fx.Rand(-0.03f, 0.03f) * h;
				Vector2 a0 = c.Feet + new Vector2(x, -h * Fx.Rand(0.1f, 0.5f)), b0 = c.Feet + new Vector2(x + fx * h * 0.35f, -h * Fx.Rand(1.35f, 1.75f));
				bool white = k % 3 == 1;
				SkillRedo5.Streak(k % 2 == 0 ? back : c.Front, t => a0.Lerp(b0, t), Fx.Rand(0f, 0.09f), 0.26f, h * 0.8f, h * (white ? 0.08f : 0.16f),
					white ? new Color(1f, 0.9f, 0.9f) : new Color(0.95f, 0.07f, 0.16f), white ? 1.6f : 1.2f);
			}
			// 他的「实体」被一口气吹散：大量细尘从全身往上、往身后扬起
			for (int k = 0; k < 6; k++)
			{
				Vector2 at = c.Feet + new Vector2(Fx.Rand(-0.2f, 0.2f) * h, -h * (0.1f + 0.15f * k));
				Glow.Sparks(k % 2 == 0 ? back : c.Front, at, 26, 120f * u, 620f * u, 1.3f,
					new[] { new Color(1f, 0.95f, 0.93f), new Color(1f, 0.28f, 0.35f), new Color(0.5f, 0.02f, 0.08f) }, -90f * u, 0.006f * u, 0.024f * u,
					new Vector2(-c.Face * 0.5f, -1f), 70f);
			}
			Glow.Flash(back, mid, new Color(1f, 0.12f, 0.2f), h * 2.2f, 0.45f);
			Glow.Flash(back, c.Feet + new Vector2(0f, -h), new Color(1f, 0.93f, 0.92f), h * 0.7f, 0.16f);
			Glow.Sparks(c.Front, c.Feet + new Vector2(0f, -h * 0.95f), 34, 120f * u, 700f * u, 0.9f,
				new[] { new Color(1f, 0.96f, 0.95f), new Color(1f, 0.3f, 0.38f), new Color(0.6f, 0.03f, 0.1f) }, 240f * u, 0.012f * u, 0.04f * u, Vector2.Up, 120f);
		});
	}

	/// <summary>被打（伤害被压成 1 的那一下）：轮廓亮一下、人更透一下。</summary>
	public void Hit()
	{
		if (_done || _stopAt >= 0f || _t < SweepAt + SweepDur) return;
		_hitAt = _t;
		Sfx.Take("gx_ping", 0.1f);
		Glow.Sparks(_front, _feet + new Vector2(0f, -0.55f * _tall), 12, 100f * _unit, 460f * _unit, 0.5f,
			new[] { new Color(1f, 0.96f, 0.95f), new Color(1f, 0.3f, 0.38f) }, 60f * _unit, 0.01f * _unit, 0.03f * _unit, new Vector2(-_face, -0.3f), 70f);
	}

	/// <summary>无实体结束：从头到脚扫回去，变回实体。</summary>
	public void Stop()
	{
		if (_done || _stopAt >= 0f) return;
		_stopAt = _t;
		Sfx.Take("gx_fan_close", 0.16f);
		Sfx.Take("gx_ping", 0.1f, EndDur - 0.14f);
		Control fr = _front;
		Vector2 ft = _feet;
		float uu = _unit, hh2 = _tall;
		Fx.After(fr, EndDur - 0.14f, () =>
		{
			Glow.Flash(fr, ft + new Vector2(0f, -hh2 * 0.05f), new Color(1f, 0.9f, 0.9f), hh2 * 0.6f, 0.16f);
			Glow.Sparks(fr, ft, 26, 100f * uu, 520f * uu, 0.6f, new[] { new Color(1f, 0.95f, 0.93f), new Color(1f, 0.28f, 0.35f) }, 300f * uu, 0.008f * uu, 0.03f * uu, Vector2.Up, 150f);
		});
	}

	public static void StopCurrent() => Current?.Stop();

	private void Tick()
	{
		if (_done) return;
		if (!GodotObject.IsInstanceValid(_glow) || !GodotObject.IsInstanceValid(_front))
		{
			Finish();
			return;
		}
		Stage.Safe("WraithVeil", Step);
	}

	private static float Seg(float t, float a, float len) => Mathf.Clamp((t - a) / len, 0f, 1f);
	private static float Smooth(float x) => x * x * (3f - 2f * x);

	private void Step()
	{
		_t = (float)(HitStop.RealSeconds() - _t0);
		float s = _t;
		if (_stopAt < 0f && StillOn != null && s >= _nextCheck)
		{
			_nextCheck = s + 0.5f;
			bool still = true;
			Stage.Safe("WraithVeil.alive", () => still = StillOn());
			if (!still) Stop();
		}
		float g = _stopAt >= 0f ? s - _stopAt : -1f;
		if (g >= EndDur)
		{
			Finish();
			return;
		}
		// 材质隔一会儿重新设一次（游戏自己换过材质的话接回来）
		if (s >= _nextApply)
		{
			_nextApply = s + 0.5f;
			if (_setBody != null) Stage.Safe("WraithVeil.body", () => _setBody(_m));
		}
		Vector2 scr = Fx.Screen(_front);
		float feetY = Fx.ScreenPoint(_front, _feet).Y / scr.Y, topY = Fx.ScreenPoint(_front, _feet + new Vector2(0f, -1.12f * _tall)).Y / scr.Y;
		// 扫描线的位置：0 = 脚下，1 = 头顶以上。打出时往上扫，结束时往下扫回去
		float up0 = Seg(s, SweepAt, SweepDur);
		float up = up0 * up0 * (1.6f - 0.6f * up0);   // 先慢后快，正好在主拍扫到头顶
		float line = g >= 0f ? 1f - Smooth(Seg(g, 0f, EndDur - 0.12f)) : up;
		bool sweeping = (s >= SweepAt && s < SweepAt + SweepDur) || g >= 0f;
		float hit = s >= _hitAt ? Mathf.Exp(-(s - _hitAt) * 5f) : 0f;
		float arrive = s >= SweepAt + SweepDur ? Mathf.Exp(-(s - SweepAt - SweepDur) * 3.5f) : 0f;
		_m.SetShaderParameter("feet_y", feetY);
		_m.SetShaderParameter("top_y", topY);
		_m.SetShaderParameter("line", Mathf.Lerp(-0.08f, 1.08f, line));
		_m.SetShaderParameter("band", sweeping ? 1f : 0f);
		_m.SetShaderParameter("clock", s);
		_m.SetShaderParameter("flash", Mathf.Min(1f, hit + arrive));
		_m.SetShaderParameter("thin", 0.16f * hit);
		_m.SetShaderParameter("aspect", scr.X / scr.Y);
		_m.SetShaderParameter("face", _face);
		float p1 = Seg(s, PreA, PreDur), p2 = Seg(s, PreB, PreDur);
		_m.SetShaderParameter("pre", new Vector2(p1 > 0f && p1 < 1f ? p1 * 1.05f : -1f, p2 > 0f && p2 < 1f ? p2 * 1.05f : -1f));

		// 身后的柔光：到位后淡淡地亮着、慢慢呼吸；被打亮一下
		float on = Smooth(Seg(s, SweepAt + 0.1f, SweepDur)) * (g >= 0f ? 1f - Smooth(Seg(g, 0f, EndDur - 0.1f)) : 1f);
		float breath = 0.5f + 0.5f * Mathf.Sin(s * 1.4f);
		_glowSprite.Scale = new Vector2(_tall * (0.95f + 0.08f * breath + 0.3f * arrive), _tall * (1.5f + 0.1f * breath + 0.3f * arrive)) / 32f;
		// 身后的柔光只在到位和被打的一瞬间亮一下，平时没有（用户：平时那层淡淡的光怪）
		_glowSprite.Modulate = new Color(1f, 0.1f, 0.18f, on * (0.45f * arrive + 0.16f * hit));
		// 横光跟着扫描线走：扫的时候宽、亮，快到头时收窄
		if (GodotObject.IsInstanceValid(_lineRoot))
		{
			float vis = sweeping && line < 0.99f ? Mathf.Sin(Mathf.Clamp(line, 0f, 1f) * Mathf.Pi) : 0f;
			vis = Mathf.Pow(vis, 0.35f) * (sweeping ? 1f : 0f);
			_lineRoot.GlobalPosition = _feet + new Vector2(0f, -1.12f * _tall * line);
			float wide = _tall * (1.5f + 0.5f * Mathf.Sin(line * Mathf.Pi));
			_lineRed.Scale = new Vector2(wide, _tall * 0.075f) / 32f;
			_lineHot.Scale = new Vector2(wide * 0.8f, _tall * 0.02f) / 32f;
			_lineRed.Modulate = new Color(1f, 0.12f, 0.2f, 0.85f * vis);
			_lineHot.Modulate = new Color(1f, 0.95f, 0.93f, vis);
		}

		// 扫描线上往两边迸的碎光
		if (sweeping && s >= _nextSpark && line > 0.02f && line < 0.98f)
		{
			_nextSpark = s + 0.035f;
			Vector2 at = _feet + new Vector2(Fx.Rand(-0.16f, 0.16f) * _tall, -1.12f * _tall * line);
			float side = Fx.Rand(0f, 1f) < 0.5f ? -1f : 1f;
			Glow.Sparks(_front, at, 3, 80f * _unit, 380f * _unit, 0.45f, new[] { new Color(1f, 0.96f, 0.95f), new Color(1f, 0.28f, 0.36f) },
				-80f * _unit, 0.01f * _unit, 0.03f * _unit, new Vector2(side, -0.4f), 50f);
		}
		// 平时零星往上飘的光粒
		if (g < 0f && s >= _nextMote && s > SweepAt + SweepDur)
		{
			_nextMote = s + Fx.Rand(0.1f, 0.2f);
			// 从身体边上剥下来的细尘：下半身多一些，往上、往身后漂
			float hh = Fx.Rand(0f, 1f); hh *= hh;
			float wide = Mathf.Lerp(0.34f, 0.14f, hh);
			Vector2 at = _feet + new Vector2((Fx.Rand(0f, 1f) < 0.5f ? -1f : 1f) * Fx.Rand(0.5f, 1f) * wide * _tall, -(0.05f + 0.95f * hh) * _tall);
			Glow.Sparks(Fx.Rand(0f, 1f) < 0.4f ? _back : _front, at, 3, 40f * _unit, 150f * _unit, 1.2f,
				new[] { new Color(1f, 0.92f, 0.9f), new Color(1f, 0.25f, 0.32f), new Color(0.5f, 0.02f, 0.08f) },
				-70f * _unit, 0.006f * _unit, 0.02f * _unit, new Vector2(-_face * 0.35f, -1f), 34f);
		}
	}

	private void Finish()
	{
		if (_done) return;
		_done = true;
		_tree.ProcessFrame -= Tick;
		if (_setBody != null) Stage.Safe("WraithVeil.body", () => _setBody(null));
		if (GodotObject.IsInstanceValid(_glow)) _glow.QueueFree();
		if (GodotObject.IsInstanceValid(_lineRoot)) _lineRoot.QueueFree();
		if (Current == this) Current = null;
	}

	// ───────────────────────── 着色器 ─────────────────────────

	private static Shader? _body;

	/// <summary>
	/// Joker 身体的幻影材质。TEXTURE = 姿势图（图集，不预乘）。位置用屏幕坐标（feet_y / top_y：脚、头顶在屏幕上的纵坐标 0～1），不依赖节点怎么摆。
	/// line：扫描线在身上的高度（0 脚 → 1 头），线以下是幻影、线以上还是实体。
	/// </summary>
	private static Shader Body => _body ??= new Shader { Code = @"
shader_type canvas_item;
uniform float feet_y = 0.8;
uniform float top_y = 0.4;
uniform float line = 1.1;
uniform float band = 0.0;      // 扫描线本身亮不亮
uniform float clock = 0.0;
uniform float flash = 0.0;
uniform float thin = 0.0;      // 被打时更透一点
uniform float aspect = 1.7778;
uniform float seed = 0.0;
uniform float face = 1.0;
uniform vec2 pre = vec2(-1.0, -1.0);   // 两道预扫的高度（<0 = 没有）
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float a = 0.5, s = 0.0; for (int i = 0; i < 3; i++) { s += a * noise(p); p = p * 2.03 + 7.1; a *= 0.5; } return s; }
// 周围一圈的平均透明度（轮廓用）
float ring(sampler2D tx, vec2 uv, vec2 px, float r) {
	float s = 0.0;
	for (int i = 0; i < 8; i++) {
		float a = 6.2831853 * (float(i) + 0.5) / 8.0;
		s += texture(tx, uv + vec2(cos(a), sin(a)) * px * r).a;
	}
	return s / 8.0;
}
void fragment() {
	vec4 base = texture(TEXTURE, UV) * COLOR;
	float a = base.a;
	// 身上的位置：h = 0 脚 → 1 头；x 以身高为单位
	float h = (feet_y - SCREEN_UV.y) / max(0.001, feet_y - top_y);
	vec2 q = vec2(SCREEN_UV.x * aspect / max(0.001, feet_y - top_y), h);
	// 扫描线：边缘带一点起伏，不是一条直线
	float wob = (noise(vec2(q.x * 7.0 + seed, clock * 2.0)) - 0.5) * 0.05;
	float dl = h - (line + wob);
	float cv = smoothstep(0.012, -0.012, dl);                 // 1 = 已经是幻影
	float lineGlow = exp(-abs(dl) * 55.0) * band;
	float under = exp(-max(-dl, 0.0) * 9.0) * band * cv;     // 线刚扫过的地方还热着

	// 背光：光从他身后上方来。朝光那一侧、紧贴轮廓的地方有一道利落的亮边（不是整圈发光）；
	// 另一侧是地面反上来的一点红。
	vec2 px = TEXTURE_PIXEL_SIZE;
	vec2 L = normalize(vec2(-face * 0.75, -0.66));
	float keyRim = clamp((a - texture(TEXTURE, UV + L * px * 3.0).a) * 1.6, 0.0, 1.0) * clamp((a - texture(TEXTURE, UV + L * px * 7.0).a) * 1.6 + 0.35, 0.0, 1.0);
	float keyWide = clamp((a - texture(TEXTURE, UV + L * px * 12.0).a) * 1.2, 0.0, 1.0);
	vec2 B = normalize(vec2(face * 0.6, 0.8));
	float bounce = clamp((a - texture(TEXTURE, UV + B * px * 5.0).a) * 1.4, 0.0, 1.0);
	// 亮边的强弱沿身体缓慢起伏（光在动）
	float flick = 0.75 + 0.25 * sin(q.y * 9.0 - clock * 1.7 + seed);

	// 身体：原画褪掉一些颜色、略冷；厚薄像雾一样缓慢流动（大块、柔的，不是颗粒），脸和胸口留得实一些
	float l = dot(base.rgb, vec3(0.299, 0.587, 0.114));
	vec3 ghost = mix(base.rgb, vec3(l) * vec3(0.97, 0.93, 1.0), 0.42);
	ghost = ghost + vec3(0.035, 0.02, 0.04);
	float mist = fbm(vec2(q.x * 3.6 + seed, q.y * 2.4 - clock * 0.28));
	float core = smoothstep(0.45, 0.9, h);
	float bodyA = mix(0.36, 0.9, smoothstep(0.28, 0.68, mist + core * 0.3)) - thin;
	// 光：背光的亮边（白，利落）+ 往里一小段的受光过渡 + 另一侧的红
	ghost = mix(ghost, vec3(1.0, 0.95, 0.93), keyRim * 0.95 * flick);
	ghost += vec3(0.5, 0.42, 0.46) * keyWide * 0.22 * flick;
	ghost = mix(ghost, vec3(0.95, 0.08, 0.16), bounce * 0.6);
	ghost += vec3(1.0, 0.85, 0.85) * flash * (0.12 + 0.6 * keyRim + 0.3 * bounce);
	ghost += vec3(1.0, 0.5, 0.52) * under * 0.5;

	vec3 col = mix(base.rgb, ghost, cv);
	float preGlow = exp(-abs(h - pre.x) * 42.0) * step(0.0, pre.x) + exp(-abs(h - pre.y) * 42.0) * step(0.0, pre.y);
	col += vec3(1.0, 0.94, 0.92) * lineGlow * 1.2 + vec3(1.0, 0.45, 0.48) * preGlow * 0.9;
	float alphaK = mix(bodyA, 1.0, clamp(keyRim + bounce * 0.8, 0.0, 1.0));
	float outA = a * mix(1.0, alphaK, cv);
	outA = max(outA, a * min(1.0, lineGlow));
	COLOR = vec4(col, outA);
}" };
}
