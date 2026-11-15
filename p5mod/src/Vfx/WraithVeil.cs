using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 幽魂形态第八版「幻影之身」（2026-11-15）。
/// 前七版的教训：身后的大件（月亮、羽翼、裂隙）用着色器画不出质感；场景不用变；被打时把身体错开几截不要；
/// 「无实体期间一直持续、结束时收回」用户认可；身体质感（半透明 + 一侧红轮廓光）用户说还可以；
/// 从脚往上扫的覆盖（v8.2）用户说很 low，特效太克制。
/// 这一版：没有扫描。主意象是「魂震」—— 他的魂（此刻这个姿势的他，原画）一下下从身上震出去：
/// · 蓄力 0～0.45：压暗，光线往胸口收，一下很小的预震；
/// · 主拍 0.45：一瞬间变成幻影，同时三层魂像从胸口往外震开（越来越大、越来越淡），疾光往四面射、尘往外扬、十字光、重震；
/// · 持续：每隔两秒左右很淡地震一下（不打扰），细尘往上飘；被打时震一下；
/// · 结束：两层魂像从外面收回到他身上，一闪，变回实体。
/// 游戏接法在 WraithPlay.cs；身体材质通过 SkillCtx.BodyMaterial 换。
/// </summary>
public sealed class WraithVeil
{
	public static WraithVeil? Current { get; private set; }

	/// <summary>无实体还在不在（游戏里设置；预览不设）。在场 2 秒后每半秒问一次，不在了就自己收掉。</summary>
	public static Func<bool>? StillOn;

	private const float Beat = 0.45f, EndDur = 0.6f;

	private readonly SkillCtx _c;
	private readonly Control _front, _back;
	private readonly Node2D _glow;
	private readonly Sprite2D _glowSprite;
	private readonly ShaderMaterial _m;
	private readonly Action<ShaderMaterial?>? _setBody;
	private readonly Vector2 _feet, _chest;
	private readonly float _tall, _face, _unit;
	private readonly SceneTree _tree;
	private readonly double _t0;
	private float _t, _stopAt = -1f, _hitAt = -10f, _nextCheck = 2f, _nextApply, _nextMote = 0.7f, _nextPulse = Beat + 2.2f;
	private bool _done;

	private WraithVeil(SkillCtx c)
	{
		_c = c;
		_front = c.Front;
		_back = c.Back ?? c.Front;
		_setBody = c.BodyMaterial;
		_feet = c.Feet;
		_chest = c.Chest;
		_tall = c.Tall;
		_face = c.Face;
		_unit = c.Unit;
		_tree = (SceneTree)Engine.GetMainLoop();
		_t0 = HitStop.RealSeconds();
		_m = new ShaderMaterial { Shader = Body };
		_m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
		_m.SetShaderParameter("band", 0f);
		_m.SetShaderParameter("pre", new Vector2(-1f, -1f));
		_glow = new Node2D { Name = "P5WraithGlow" };
		_back.AddChild(_glow);
		_glow.GlobalPosition = _feet + new Vector2(0f, -0.52f * _tall);
		_glowSprite = Glow.SoftSprite(_glow, Vector2.Zero, new Color(1f, 0.1f, 0.18f, 0f), 1f);
		_tree.ProcessFrame += Tick;
		_glow.TreeExiting += Finish;
		Step();
	}

	/// <summary>打出幽魂形态。已经开着（再打一张）：震一下。</summary>
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
		Sfx.Play(Sfx.WraithForm, 0.5f);
		Sfx.Take("gx_freeze", 0.2f, 0.02f);
		Sfx.Take("gx_tick", 0.1f, 0.2f);
		Sfx.Take("gx_whoosh", 0.24f, Beat - 0.03f);
		Sfx.Take("gx_ping", 0.14f, Beat);
		float h = c.Tall, u = c.Unit;
		Control back = w._back;
		Vector2 chest = c.Chest, mid = c.Feet + new Vector2(0f, -0.55f * h);
		SkillRedo5.Dim(back, mid, new Vector2(h * 3.6f, h * 2.8f), 1.6, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.3f)) * (1f - Ease5.Seg(s, 0.9f, 0.7f)) * 0.85f);
		// 蓄力：光线往胸口收，正好在主拍收到
		for (int i = 0; i < 26; i++)
		{
			float ang = Mathf.Tau * i / 26f + Fx.Rand(-0.1f, 0.1f);
			Vector2 dir = new(Mathf.Cos(ang), Mathf.Sin(ang) * 0.85f);
			float r0 = h * Fx.Rand(0.6f, 0.95f), r1 = h * Fx.Rand(0.05f, 0.12f);
			float dur = Fx.Rand(0.2f, 0.28f), delay = Beat - dur - Fx.Rand(0f, 0.2f);
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
		// 预震：很小的一下
		w.Pulse(0.2f, 1f, 1.1f, 0.45f, 0.3f, false);
		// 主拍：三层魂像震开（在主拍前 0.1 秒抓姿势，那时身体还没换成幻影）
		Fx.After(c.Front, Beat - 0.1f, () =>
		{
			w.Pulse(0.1f, 1f, 1.25f, 0.95f, 0.45f, false, 1.2f);
			w.Pulse(0.16f, 1.03f, 1.45f, 0.7f, 0.5f, false, 0.6f);
			w.Pulse(0.22f, 1.06f, 1.65f, 0.45f, 0.55f, false, 0.2f);
		});
		Fx.After(c.Front, Beat, () =>
		{
			HitStop.Request(0.07);
			Fx.Shake(2);
			SkillRedo5.Cross(c.Front, chest, h * 1.2f);
			Glow.Flash(back, chest, new Color(1f, 0.12f, 0.2f), h * 2.4f, 0.4f);
			Glow.Flash(c.Front, chest, new Color(1f, 0.95f, 0.94f), h * 0.6f, 0.14f);
			// 疾光从胸口往四面射出去（不出屏，最长一个多身高）
			for (int k = 0; k < 12; k++)
			{
				float ang = Mathf.Tau * k / 12f + Fx.Rand(-0.12f, 0.12f);
				Vector2 d = new(Mathf.Cos(ang), Mathf.Sin(ang) * 0.75f);
				Vector2 a0 = chest + d * h * 0.12f, b0 = chest + d * h * Fx.Rand(0.95f, 1.35f);
				bool white = k % 3 == 1;
				SkillRedo5.Streak(k % 2 == 0 ? back : c.Front, t => a0.Lerp(b0, t), Fx.Rand(0f, 0.05f), 0.24f, h * 0.55f, h * (white ? 0.06f : 0.12f),
					white ? new Color(1f, 0.9f, 0.9f) : new Color(0.95f, 0.07f, 0.16f), white ? 1.6f : 1.2f);
			}
			// 尘往外扬
			Glow.Sparks(back, chest, 60, 200f * u, 900f * u, 1.1f,
				new[] { new Color(1f, 0.95f, 0.93f), new Color(1f, 0.28f, 0.35f), new Color(0.5f, 0.02f, 0.08f) }, -60f * u, 0.006f * u, 0.026f * u, Vector2.Up, 180f);
			Glow.Sparks(c.Front, chest, 30, 160f * u, 700f * u, 0.9f,
				new[] { new Color(1f, 0.96f, 0.95f), new Color(1f, 0.3f, 0.38f) }, -40f * u, 0.008f * u, 0.032f * u, Vector2.Up, 180f);
		});
	}

	/// <summary>
	/// 魂像：抓此刻的他（原画），放在身后，从胸口往外放大、变淡（converge = 反过来，从大收回到原位、变亮）。
	/// delay 秒后开始动；s0 → s1 缩放；a0 起始亮度；dur 时长；hot 边更亮。
	/// </summary>
	private void Pulse(float delay, float s0, float s1, float a0, float dur, bool converge, float hot = 0f)
	{
		if (SkillRedo4.Freeze(_c, _back, delay + dur + 0.1) is not { } fr) return;
		ShaderMaterial m = new() { Shader = Soul };
		m.SetShaderParameter("alpha", 0f);
		m.SetShaderParameter("hot", hot);
		Sprite2D sp = new() { Texture = fr.Tex, Centered = false, Material = m };
		fr.Root.AddChild(sp);
		Vector2 pivot = _feet + new Vector2(0f, -0.5f * _tall) - _c.Region.Position;   // 身体中间（胸口点在有的姿势里偏到手边）
		float stretch = Fx.Rand(1.0f, 1.06f);
		Fx.Animate(sp, delay, dur, t =>
		{
			float e = converge ? Fx.InCubic(t) : Fx.OutCubic(t);
			float sc = Mathf.Lerp(s0, s1, e);
			Vector2 k = new(sc * Mathf.Lerp(1f, stretch, e), sc);
			sp.Scale = k;
			sp.Position = pivot - pivot * k;
			float a = converge ? a0 * Mathf.Min(1f, t * 2.5f) * (1f - Ease5.Seg(t, 0.85f, 0.15f)) : a0 * Mathf.Pow(1f - t, 1.6f) * Mathf.Min(1f, t * 12f);
			m.SetShaderParameter("alpha", a);
		});
	}

	/// <summary>被打（伤害被压成 1 的那一下）：震一下，轮廓亮一下。</summary>
	public void Hit()
	{
		if (_done || _stopAt >= 0f || _t < Beat) return;
		_hitAt = _t;
		_nextPulse = _t + 2.2f;
		Sfx.Take("gx_ping", 0.1f);
		Pulse(0f, 1f, 1.35f, 0.75f, 0.45f, false, 0.6f);
		Glow.Sparks(_front, _chest, 16, 120f * _unit, 520f * _unit, 0.55f,
			new[] { new Color(1f, 0.96f, 0.95f), new Color(1f, 0.3f, 0.38f) }, 60f * _unit, 0.01f * _unit, 0.03f * _unit, new Vector2(-_face, -0.3f), 80f);
	}

	/// <summary>无实体结束：魂像从外面收回到他身上，一闪，变回实体。</summary>
	public void Stop()
	{
		if (_done || _stopAt >= 0f) return;
		_stopAt = _t;
		Sfx.Take("gx_fan_close", 0.16f);
		Sfx.Take("gx_ping", 0.12f, EndDur - 0.12f);
		Pulse(0f, 1.7f, 1f, 0.5f, EndDur - 0.12f, true, 0.3f);
		Pulse(0.08f, 1.35f, 1f, 0.7f, EndDur - 0.2f, true, 0.6f);
		Control fr = _front;
		Vector2 ch = _chest;
		float uu = _unit, hh2 = _tall;
		Fx.After(fr, EndDur - 0.12f, () =>
		{
			Glow.Flash(fr, ch, new Color(1f, 0.92f, 0.92f), hh2 * 0.7f, 0.16f);
			Glow.Sparks(fr, ch, 30, 120f * uu, 560f * uu, 0.6f, new[] { new Color(1f, 0.95f, 0.93f), new Color(1f, 0.28f, 0.35f) }, 200f * uu, 0.008f * uu, 0.03f * uu, Vector2.Up, 180f);
			Fx.Shake(1);
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
		if (s >= _nextApply)
		{
			_nextApply = s + 0.5f;
			if (_setBody != null) Stage.Safe("WraithVeil.body", () => _setBody(_m));
		}
		Vector2 scr = Fx.Screen(_front);
		float feetY = Fx.ScreenPoint(_front, _feet).Y / scr.Y, topY = Fx.ScreenPoint(_front, _feet + new Vector2(0f, -1.12f * _tall)).Y / scr.Y;
		// 幻影：主拍那一瞬间整个人一起换（没有扫描）；结束时在最后那一闪换回来
		bool ghost = s >= Beat && (g < 0f || g < EndDur - 0.12f);
		float hit = s >= _hitAt ? Mathf.Exp(-(s - _hitAt) * 5f) : 0f;
		float arrive = s >= Beat ? Mathf.Exp(-(s - Beat) * 3.5f) : 0f;
		float back = g >= EndDur - 0.12f ? Mathf.Exp(-(g - EndDur + 0.12f) * 8f) : 0f;
		_m.SetShaderParameter("feet_y", feetY);
		_m.SetShaderParameter("top_y", topY);
		_m.SetShaderParameter("line", ghost ? 1.3f : -0.3f);
		_m.SetShaderParameter("clock", s);
		_m.SetShaderParameter("flash", Mathf.Min(1f, hit + arrive + back));
		_m.SetShaderParameter("thin", 0.16f * hit);
		_m.SetShaderParameter("aspect", scr.X / scr.Y);
		_m.SetShaderParameter("face", _face);

		// 身后的柔光只在主拍和被打的一瞬间亮一下
		float on = g >= 0f ? 1f - Smooth(Seg(g, 0f, EndDur - 0.1f)) : 1f;
		_glowSprite.Scale = new Vector2(_tall * (0.95f + 0.3f * arrive), _tall * (1.5f + 0.3f * arrive)) / 32f;
		_glowSprite.Modulate = new Color(1f, 0.1f, 0.18f, on * (0.45f * arrive + 0.16f * hit));

		// 持续期间：隔一会儿很淡地震一下
		if (g < 0f && s >= _nextPulse)
		{
			_nextPulse = s + Fx.Rand(1.9f, 2.6f);
			Pulse(0f, 1f, 1.16f, 0.32f, 0.7f, false);
		}
		// 细尘往上飘
		if (g < 0f && s >= _nextMote && s > Beat)
		{
			_nextMote = s + Fx.Rand(0.1f, 0.2f);
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
		if (Current == this) Current = null;
	}

	// ───────────────────────── 着色器 ─────────────────────────

	private static Shader? _body, _soul;

	/// <summary>魂像：TEXTURE = 冻住的他（原画）。加法：边上一圈亮、里面按原画明暗透一点淡红白（不是填色）。</summary>
	private static Shader Soul => _soul ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float alpha = 1.0;
uniform float hot = 0.0;
void fragment() {
	vec4 t = texture(TEXTURE, UV);
	float a = smoothstep(0.2, 0.7, t.a);
	float nb = 0.0, nb2 = 0.0;
	for (int i = 0; i < 8; i++) {
		float ang = 6.2831853 * (float(i) + 0.5) / 8.0;
		vec2 d = vec2(cos(ang), sin(ang)) * TEXTURE_PIXEL_SIZE;
		nb += smoothstep(0.2, 0.7, texture(TEXTURE, UV + d * 2.5).a);
		nb2 += smoothstep(0.2, 0.7, texture(TEXTURE, UV + d * 8.0).a);
	}
	nb /= 8.0; nb2 /= 8.0;
	float rim = clamp((a - nb) * 2.5, 0.0, 1.0) + clamp((nb - a) * 1.2, 0.0, 1.0);
	float soft = clamp((a - nb2) * 1.3, 0.0, 1.0);
	float l = dot(t.rgb, vec3(0.299, 0.587, 0.114));
	vec3 col = vec3(0.95, 0.04, 0.13) * (a * 0.3 + soft * 0.7) + vec3(1.0, 0.7, 0.72) * l * a * 0.3 + vec3(1.0, 0.92, 0.9) * rim * (0.9 + hot);
	COLOR = vec4(col * alpha, 1.0);
}" };

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
