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

	private const float SweepAt = 0.1f, SweepDur = 0.5f, EndDur = 0.6f;

	private readonly Control _front, _back;
	private readonly Node2D _glow;
	private readonly Sprite2D _glowSprite;
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
		float h = c.Tall, u = c.Unit;
		Control back = w._back;
		Vector2 mid = c.Feet + new Vector2(0f, -0.55f * h);
		SkillRedo5.Dim(back, mid, new Vector2(h * 3.4f, h * 2.6f), 1.5, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.3f)) * (1f - Ease5.Seg(s, 0.8f, 0.7f)) * 0.8f);
		Fx.After(c.Front, SweepAt + SweepDur, () =>
		{
			HitStop.Request(0.05);
			Fx.Shake(1);
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
		float up = Ease5.OutQuint(Seg(s, SweepAt, SweepDur) * 0.9f) / Ease5.OutQuint(0.9f);
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
		_m.SetShaderParameter("thin", 0.3f * hit);
		_m.SetShaderParameter("aspect", scr.X / scr.Y);

		// 身后的柔光：到位后淡淡地亮着、慢慢呼吸；被打亮一下
		float on = Smooth(Seg(s, SweepAt + 0.1f, SweepDur)) * (g >= 0f ? 1f - Smooth(Seg(g, 0f, EndDur - 0.1f)) : 1f);
		float breath = 0.5f + 0.5f * Mathf.Sin(s * 1.4f);
		_glowSprite.Scale = new Vector2(_tall * (0.95f + 0.08f * breath + 0.3f * arrive), _tall * (1.5f + 0.1f * breath + 0.3f * arrive)) / 32f;
		_glowSprite.Modulate = new Color(1f, 0.1f, 0.18f, on * (0.3f + 0.06f * breath + 0.3f * arrive + 0.2f * hit));

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
			_nextMote = s + Fx.Rand(0.35f, 0.7f);
			Vector2 at = _feet + new Vector2(Fx.Rand(-0.28f, 0.28f) * _tall, -Fx.Rand(0.1f, 0.85f) * _tall);
			Glow.Sparks(Fx.Rand(0f, 1f) < 0.5f ? _back : _front, at, 2, 30f * _unit, 90f * _unit, 1.3f, new[] { new Color(1f, 0.9f, 0.9f), new Color(1f, 0.22f, 0.3f) },
				-60f * _unit, 0.008f * _unit, 0.022f * _unit, Vector2.Up, 40f);
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

	// 轮廓：离边越近越亮（两圈：贴边的一线 + 往里的一段柔边）
	float near = ring(TEXTURE, UV, TEXTURE_PIXEL_SIZE, 2.2);
	float far = ring(TEXTURE, UV, TEXTURE_PIXEL_SIZE, 7.0);
	float rimHot = clamp((a - near) * 2.6, 0.0, 1.0);
	float rimSoft = clamp((a - far) * 1.5, 0.0, 1.0);
	float rimWide = clamp((a - ring(TEXTURE, UV, TEXTURE_PIXEL_SIZE, 17.0)) * 1.25, 0.0, 1.0);
	// 轮廓上的光是流动的：一段段亮、往上走
	float flow = fbm(vec2(q.x * 5.0 + seed, q.y * 3.2 - clock * 0.9));
	float flowK = 0.35 + 1.1 * smoothstep(0.3, 0.75, flow);

	// 幻影的身体：原画还在，褪色发白、稍微压暗，半透明
	float l = dot(base.rgb, vec3(0.299, 0.587, 0.114));
	vec3 pale = mix(base.rgb, vec3(l) * vec3(1.0, 0.9, 0.93), 0.5);
	pale = pale * 1.05 + vec3(0.17, 0.04, 0.07);
	// 身体里往上流的光：细长的竖条，疏密不匀
	float st = noise(vec2(q.x * 26.0 + seed * 3.0, q.y * 2.6 - clock * 0.55));
	float st2 = noise(vec2(q.x * 11.0, q.y * 1.4 - clock * 0.3 + 4.0));
	float stream = smoothstep(0.62, 0.92, st) * smoothstep(0.35, 0.7, st2);
	vec3 ghost = pale + (vec3(1.0, 0.16, 0.24) + vec3(0.0, 0.5, 0.45) * smoothstep(0.85, 1.0, st)) * stream * 0.7;
	ghost += vec3(0.85, 0.06, 0.14) * rimWide * 0.5;
	// 轮廓光
	ghost += vec3(1.0, 0.1, 0.18) * rimSoft * 1.0 * flowK;
	ghost += vec3(1.0, 0.88, 0.86) * rimHot * (0.7 + 0.5 * flowK);
	ghost += vec3(1.0, 0.75, 0.75) * flash * (0.18 + 0.7 * rimSoft);
	ghost += vec3(1.0, 0.5, 0.52) * under * 0.6;

	vec3 col = mix(base.rgb, ghost, cv);
	col += vec3(1.0, 0.94, 0.92) * lineGlow * 1.2;
	// 透明度：身体半透，轮廓不透（边还是清楚的）；扫描线本身不透
	float bodyA = mix(0.42, 0.8, smoothstep(0.04, 0.5, l)) - thin;
	float alphaK = mix(bodyA, 1.0, clamp(rimSoft * 1.3 + rimHot, 0.0, 1.0));
	float outA = a * mix(1.0, alphaK, cv);
	outA = max(outA, a * min(1.0, lineGlow));
	COLOR = vec4(col, outA);
}" };
}
