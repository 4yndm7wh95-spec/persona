using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 幽魂形态第七版「异世界裂隙」（2026-11-14；用户：规模要像子弹时间，无实体期间一直持续、被打要有反馈）。
/// 主意象：他身后的空间裂开一道口子，里面是红黑的异世界；它漫出来把整个场景染成异世界的样子 —— 他站到了另一层，所以打不实。
/// 结构照子弹时间（TimeStop.cs）：
/// · 背景层一张读屏的全屏着色器：裂隙本体（里面流动的红黑纹、边上白热的光、往里拽的折射）+ 场景调色（从裂隙往外漫开的波）+ 浮在空中的光粒；
/// · 前景层一小块读屏：他身上的错位（平时只有一点红边在晃，被打时整个人横着错开几截再合上）；
/// · Hit()：被打（无实体把伤害压成 1 的那一下）；Stop()：无实体结束，异世界收回裂隙、裂隙合上。
/// 游戏接法在 WraithPlay.cs。
/// </summary>
public sealed class WraithRift
{
	public static WraithRift? Current { get; private set; }

	/// <summary>无实体还在不在（游戏里设置；预览不设）。在场 2 秒后每半秒问一次，不在了就自己收掉（防止漏掉结束的钩子）。</summary>
	public static Func<bool>? StillOn;

	private const float Reach = 9f, OpenAt = 0.16f, FloodAt = 0.52f, EndDur = 0.95f;

	private readonly Control _front, _back;
	private readonly Node2D _screen, _shimmer;
	private readonly ShaderMaterial _m, _sm;
	private readonly Action<Color>? _tint;
	private readonly Vector2 _feet, _center;
	private readonly float _tall, _face;
	private readonly SceneTree _tree;
	private readonly double _t0;
	private float _t, _stopAt = -1f, _hitAt = -10f, _nextCheck = 2f;
	private int _hits;
	private bool _done;

	private WraithRift(SkillCtx c)
	{
		_front = c.Front;
		_back = c.Back ?? c.Front;
		_tint = c.SelfTint;
		_feet = c.Feet;
		_tall = c.Tall;
		_face = c.Face;
		_center = c.Feet + new Vector2(0f, -0.5f * c.Tall);
		_tree = (SceneTree)Engine.GetMainLoop();
		_t0 = HitStop.RealSeconds();
		_screen = Fx.ScreenRoot(_back, "P5WraithRift");
		_m = new ShaderMaterial { Shader = World };
		_screen.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		_screen.AddChild(new ColorRect { Size = Fx.Screen(_back), Material = _m, MouseFilter = Control.MouseFilterEnum.Ignore });
		_shimmer = Fx.ScreenRoot(_front, "P5WraithShimmer");
		_front.MoveChild(_shimmer, 0);
		_sm = new ShaderMaterial { Shader = Shimmer };
		_shimmer.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		_shimmer.AddChild(new ColorRect { Size = Fx.Screen(_front), Material = _sm, MouseFilter = Control.MouseFilterEnum.Ignore });
		_tree.ProcessFrame += Tick;
		_screen.TreeExiting += Finish;
		_shimmer.TreeExiting += Finish;
		Step();
	}

	/// <summary>打出幽魂形态。已经开着（再打一张）：只涨一下。</summary>
	public static void Start(SkillCtx c)
	{
		if (Current is { _done: false, _stopAt: < 0f } cur)
		{
			cur.Hit();
			return;
		}
		Current?.Finish();
		var w = new WraithRift(c);
		Current = w;
		// 拍 1：裂开（冷的、往里收的一声）；拍 2（主拍）：异世界漫出来
		Sfx.Play(Sfx.WraithForm, 0.5f);
		Sfx.Take("gx_freeze", 0.22f, 0.02f);
		Sfx.Take("gx_whoosh", 0.26f, FloodAt - 0.04f);
		float u = c.Unit, h = c.Tall;
		Vector2 mid = w.RiftMid;
		Fx.After(c.Front, OpenAt, () =>
		{
			// 裂口张开：光从缝里往两边迸
			foreach (float sgn in new[] { -1f, 1f })
				Glow.Sparks(w._back, mid, 26, 160f * u, 820f * u, 0.6f, new[] { new Color(1f, 0.97f, 0.96f), new Color(1f, 0.3f, 0.36f), new Color(0.6f, 0.02f, 0.08f) },
					80f * u, 0.012f * u, 0.04f * u, new Vector2(sgn, -0.15f), 38f);
			Glow.Flash(w._back, mid, new Color(1f, 0.92f, 0.92f), h * 0.9f, 0.16f);
		});
		Fx.After(c.Front, FloodAt, () =>
		{
			HitStop.Request(0.06);
			Fx.Shake(2);
			Glow.Flash(w._back, mid, new Color(1f, 0.12f, 0.2f), h * 2.6f, 0.4f);
			Glow.Sparks(c.Front, mid, 40, 200f * u, 1000f * u, 0.8f, new[] { new Color(1f, 0.96f, 0.95f), new Color(1f, 0.3f, 0.38f), new Color(0.6f, 0.03f, 0.1f) },
				120f * u, 0.012f * u, 0.042f * u, Vector2.Up, 180f);
		});
	}

	/// <summary>被打（伤害被压成 1 的那一下）：裂隙一亮一张、场景里推出一圈折射、他整个人横着错开几截再合上。</summary>
	public void Hit()
	{
		if (_done || _stopAt >= 0f || _t < FloodAt) return;
		_hitAt = _t;
		_hits++;
		Sfx.Take("gx_ping", 0.12f);
		float u = _tall / 360f;
		Glow.Sparks(_front, _center, 14, 120f * u, 520f * u, 0.45f, new[] { new Color(1f, 0.96f, 0.95f), new Color(1f, 0.3f, 0.38f) }, 60f * u, 0.01f * u, 0.03f * u, new Vector2(-_face, -0.2f), 60f);
	}

	/// <summary>无实体结束：异世界收回裂隙，裂隙合成一道线、一闪消失。</summary>
	public void Stop()
	{
		if (_done || _stopAt >= 0f) return;
		_stopAt = _t;
		Sfx.Take("gx_fan_close", 0.18f);
		Sfx.Take("gx_tock", 0.2f, 0.6f);
	}

	public static void StopCurrent() => Current?.Stop();

	private Vector2 RiftMid => _center + new Vector2(-_face * 0.05f, -0.27f) * _tall;

	private void Tick()
	{
		if (_done) return;
		if (!GodotObject.IsInstanceValid(_screen) || !GodotObject.IsInstanceValid(_shimmer) || !GodotObject.IsInstanceValid(_front))
		{
			Finish();
			return;
		}
		Stage.Safe("WraithRift", Step);
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
			Stage.Safe("WraithRift.alive", () => still = StillOn());
			if (!still) Stop();
		}
		float g = _stopAt >= 0f ? s - _stopAt : -1f;
		if (g >= EndDur)
		{
			Finish();
			return;
		}
		float hit = Mathf.Exp(-Mathf.Max(0f, s - _hitAt) * 5f) * (s >= _hitAt ? 1f : 0f);
		// 裂隙：先是一道竖着长出来的线，再猛地张开（略过头），随后收到平时的宽度，慢慢呼吸
		float grow = Ease5.OutQuint(Seg(s, 0f, OpenAt + 0.06f));
		float e = Seg(s, OpenAt, 0.4f);
		float back = 1f + 2.2f * Mathf.Pow(e - 1f, 3f) + 1.2f * Mathf.Pow(e - 1f, 2f);   // OutBack
		float settle = Smooth(Seg(s, 1.15f, 0.9f));
		float open = Mathf.Lerp(0.035f, 1f, back) * Mathf.Lerp(1f, 0.62f, settle) + 0.035f * Mathf.Sin(s * 1.3f) * settle + 0.22f * hit;
		float glow = Mathf.Lerp(1.7f, 0.85f, settle) + 0.9f * hit;
		float reach = Reach * Fx.OutCubic(Seg(s, FloodAt, 0.75f));
		float k = Mathf.Lerp(1f, 0.6f, Smooth(Seg(s, 1.3f, 1.0f))) + 0.2f * hit;
		float seal = 0f;
		if (g >= 0f)
		{
			// 收：调色从远处退回裂隙 → 裂隙合成线 → 线从两头缩成一点、一闪
			float back1 = Smooth(Seg(g, 0f, 0.55f));
			reach *= 1f - back1;
			open = Mathf.Lerp(open, 0.03f, Smooth(Seg(g, 0.3f, 0.3f)));
			grow *= 1f - Fx.InCubic(Seg(g, 0.62f, 0.26f));
			glow += 1.2f * Mathf.Sin(Seg(g, 0.3f, 0.6f) * Mathf.Pi);
			seal = Mathf.Sin(Seg(g, 0.55f, 0.35f) * Mathf.Pi);
		}
		Vector2 scr = Fx.Screen(_back);
		Vector2 mid = Fx.ScreenPoint(_back, RiftMid);
		float tall = _tall * _back.GetGlobalTransformWithCanvas().Scale.Y / scr.Y;
		_m.SetShaderParameter("aspect", scr.X / scr.Y);
		_m.SetShaderParameter("center", new Vector2(mid.X / scr.X, mid.Y / scr.Y));
		_m.SetShaderParameter("tall", tall);
		_m.SetShaderParameter("clock", s);
		_m.SetShaderParameter("grow", grow);
		_m.SetShaderParameter("open", open);
		_m.SetShaderParameter("glow", glow);
		_m.SetShaderParameter("reach", reach);
		_m.SetShaderParameter("k", k);
		_m.SetShaderParameter("front", s < FloodAt || g >= 0f ? 0f : 1f - Seg(s, FloodAt + 0.55f, 0.3f));
		_m.SetShaderParameter("pulse_s", s >= _hitAt ? s - _hitAt : 10f);
		_m.SetShaderParameter("seal", seal);

		// 他身上
		float on = Smooth(Seg(s, FloodAt - 0.1f, 0.3f)) * (g >= 0f ? 1f - Smooth(Seg(g, 0.1f, 0.5f)) : 1f);
		float entry = Mathf.Exp(-Mathf.Max(0f, s - FloodAt) * 4f) * (s >= FloodAt ? 1f : 0f);
		Vector2 a = Fx.ScreenPoint(_front, _feet + new Vector2(-0.62f, -1.22f) * _tall), b = Fx.ScreenPoint(_front, _feet + new Vector2(0.62f, 0.1f) * _tall);
		_sm.SetShaderParameter("box", new Vector4(a.X / scr.X, a.Y / scr.Y, b.X / scr.X, b.Y / scr.Y));
		_sm.SetShaderParameter("amp", (0.014f * hit + 0.012f * entry) * tall * 3f);
		_sm.SetShaderParameter("split", (0.0016f + 0.0012f * Mathf.Sin(s * 2.1f)) * on + 0.006f * (hit + entry));
		_sm.SetShaderParameter("clock", s);
		_sm.SetShaderParameter("stamp", (float)_hits * 7.3f);
		_sm.SetShaderParameter("on", on);
		if (_tint != null)
		{
			Color ghost = new(1f, 0.88f, 0.9f, 0.74f);
			Color col = Colors.White.Lerp(ghost, on);
			col.A = Mathf.Clamp(col.A - 0.3f * (hit + entry) * on, 0.3f, 1f);
			Stage.Safe("WraithRift.tint", () => _tint(col));
		}
	}

	private void Finish()
	{
		if (_done) return;
		_done = true;
		_tree.ProcessFrame -= Tick;
		if (_tint != null) Stage.Safe("WraithRift.tint", () => _tint(Colors.White));
		if (GodotObject.IsInstanceValid(_screen)) _screen.QueueFree();
		if (GodotObject.IsInstanceValid(_shimmer)) _shimmer.QueueFree();
		if (Current == this) Current = null;
	}

	// ───────────────────────── 着色器 ─────────────────────────

	private static Shader? _world, _shim;

	private static Shader World => _world ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float aspect = 1.7778;
uniform vec2 center = vec2(0.25, 0.5);   // 裂隙中心（0～1）
uniform float tall = 0.33;               // 身高（屏幕高度的比例）
uniform float clock = 0.0;
uniform float grow = 1.0;                // 裂隙竖向长出来 0 → 1
uniform float open = 1.0;                // 张开的宽度
uniform float glow = 1.0;                // 边光强度
uniform float reach = 0.0;               // 异世界漫到多远（身高为单位）
uniform float k = 1.0;                   // 调色强度
uniform float front = 0.0;               // 漫开时波前的亮度
uniform float pulse_s = 10.0;            // 被打后过了几秒
uniform float seal = 0.0;                // 合上的那一闪

float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float a = 0.5, s = 0.0; for (int i = 0; i < 4; i++) { s += a * noise(p); p = p * 2.03 + 7.1; a *= 0.5; } return s; }
float lum(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }

// 裂隙：返回到边缘的距离（身高单位，里面为负）
float rift(vec2 p, out float yy) {
	yy = p.y / (0.98 * max(grow, 0.001));
	float prof = pow(max(0.0, 1.0 - yy * yy), 0.62);
	float side = p.x < 0.0 ? 3.7 : 0.0;
	float wob = fbm(vec2(p.y * 2.4 + side, clock * 0.32 + side)) - 0.5;
	float wob2 = noise(vec2(p.y * 9.0 + side * 2.0, clock * 0.9)) - 0.5;
	float hw = (open * (0.25 + 0.3 * wob) + 0.045 * wob2 * min(1.0, open * 3.0)) * prof;
	hw = max(hw, 0.006 * prof);
	// 裂口不是直的：中线自己也歪歪扭扭
	float cx = (0.1 * sin(p.y * 1.7 + 1.0) + 0.12 * (fbm(vec2(p.y * 1.4 + 2.0, clock * 0.08)) - 0.5)) * min(1.0, open * 2.0);
	return abs(p.x - cx) - hw;
}

// 异世界里面：一层层流动的红黑纹，越靠里越暗，中间一道往上的光
vec3 inside(vec2 p, float d) {
	// 很深的黑红底，中间有光从深处透出来
	float core = exp(-p.x * p.x * 14.0);
	vec3 col = mix(vec3(0.012, 0.0, 0.006), vec3(0.3, 0.0, 0.05), core * (0.6 + 0.4 * sin(p.y * 2.0 - clock * 0.8)));
	// 三层流动的红纹（远的细、暗、慢），是线不是色块
	for (int i = 0; i < 3; i++) {
		float fi = float(i);
		float sc = 1.6 - fi * 0.4;
		vec2 q = p * sc + vec2(fi * 3.1, -clock * (0.1 + 0.09 * fi));
		float warp = fbm(q * 1.1 + fi * 9.0 + vec2(clock * 0.04, 0.0));
		float v = sin(q.y * 6.0 + warp * 7.0 + sin(q.x * 2.2 + clock * 0.25) * 1.2);
		float line = smoothstep(0.7, 0.96, v);
		float hot = smoothstep(0.93, 1.0, v);
		float near = 0.35 + 0.325 * fi;
		col += (vec3(0.85, 0.03, 0.1) * line + vec3(1.0, 0.65, 0.62) * hot * 0.6) * near * (0.5 + 0.5 * noise(q * 0.8 + 4.0));
	}
	// 细碎的亮点慢慢往上漂
	vec2 g = p * 10.0 + vec2(0.0, clock * 0.5);
	vec2 id = floor(g), f = fract(g) - 0.5;
	float h = hash(id);
	vec2 o = (vec2(hash(id + 3.1), hash(id + 7.7)) - 0.5) * 0.6;
	float star = smoothstep(0.08, 0.0, length(f - o)) * step(0.66, h) * (0.5 + 0.5 * sin(clock * 3.0 + h * 40.0));
	col += vec3(1.0, 0.8, 0.8) * star * 0.9;
	// 靠近裂口边缘的一圈被外面照亮一点，往里沉下去
	col *= mix(0.55, 1.15, smoothstep(-0.3, 0.0, d));
	return col;
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 p = (uv - center) * vec2(aspect, 1.0) / tall;
	float yy;
	float d = rift(p, yy);
	float alive = step(0.001, grow);
	float r = length(p * vec2(1.0, 1.15));
	vec2 dirp = p / max(r, 1e-4);
	vec2 toUv = vec2(1.0 / aspect, 1.0) * tall;

	// 折射：裂隙往里拽 + 漫开的波前 + 被打时推出的一圈 + 异世界里缓慢的起伏
	vec2 off = vec2(0.0);
	float pull = exp(-max(d, 0.0) * 4.5) * min(1.0, open * 1.6) * alive * smoothstep(1.15, 0.8, abs(yy));
	off += vec2(-sign(p.x) * 0.05 * pull, 0.0);
	float wf = exp(-pow((r - reach) / 0.22, 2.0)) * front;
	off += dirp * wf * 0.09;
	float pr = pulse_s * 5.5;
	float pw = exp(-pow((r - pr) / 0.16, 2.0)) * exp(-pulse_s * 2.6);
	off += dirp * pw * 0.06;
	float inW = smoothstep(reach, reach - 0.3, r);
	off += vec2(sin(p.y * 5.0 + clock * 0.9), cos(p.x * 4.0 + clock * 0.7)) * 0.006 * inW * k;
	vec3 src = texture(screen_tex, uv + off * toUv).rgb;

	// 调色：暗处压黑、中间调染成深红、高光留一点暖白；保留一部分原来的层次
	float l = lum(src);
	vec3 tone = mix(vec3(0.07, 0.0, 0.025), vec3(0.82, 0.04, 0.11), smoothstep(0.0, 0.32, l));
	tone = mix(tone, vec3(1.0, 0.66, 0.6), smoothstep(0.32, 0.8, l));
	tone = mix(tone, src * vec3(1.5, 0.7, 0.74), 0.3);
	float gmask = inW * clamp(k, 0.0, 1.2);
	vec3 col = mix(src, tone, gmask * 0.8);
	// 异世界的纹：很宽、很淡的明暗带，从裂隙往外慢慢推
	float bandv = sin(r * 5.2 - clock * 1.1 + fbm(p * 0.8 + clock * 0.05) * 5.0);
	col += vec3(0.5, 0.02, 0.06) * smoothstep(0.55, 1.0, bandv) * 0.1 * gmask;
	col *= 1.0 - 0.1 * smoothstep(-0.55, -1.0, bandv) * gmask;
	// 浮在空中的光粒
	for (int i = 0; i < 2; i++) {
		float fi = float(i);
		vec2 g = p * (2.6 + fi * 1.7) + vec2(fi * 5.3 + sin(clock * 0.2 + fi) * 0.3, clock * (0.14 + 0.08 * fi));
		vec2 id = floor(g), f = fract(g) - 0.5;
		float h = hash(id + fi * 11.0);
		vec2 o = (vec2(hash(id + 1.7), hash(id + 9.2)) - 0.5) * 0.7;
		float tw = 0.5 + 0.5 * sin(clock * (1.5 + h * 2.0) + h * 30.0);
		float mote = smoothstep(0.07 - fi * 0.02, 0.0, length(f - o)) * step(0.55, h) * tw;
		col += mix(vec3(1.0, 0.16, 0.22), vec3(1.0, 0.85, 0.85), h) * mote * 0.55 * gmask;
	}
	// 波前：一道宽的柔光
	col += vec3(1.0, 0.14, 0.2) * wf * 0.55 + vec3(1.0, 0.8, 0.8) * pow(wf, 3.0) * 0.35;
	col += vec3(1.0, 0.2, 0.26) * pw * 0.3;

	// 裂隙本体
	if (alive > 0.0) {
		float aa = fwidth(d) * 1.2 + 0.002;
		float inR = smoothstep(aa, -aa, d) * step(abs(yy), 1.0);
		vec3 world2 = inside(p, d);
		col = mix(col, world2, inR);
		// 边：最里一线白热，往外是红的辉光，沿边粗细不匀、有细小的光往外舔
		float lick = fbm(vec2(p.y * 7.0 + (p.x < 0.0 ? 5.0 : 0.0), clock * 1.5));
		float dn = d - 0.035 * (lick - 0.35) * min(1.0, open * 2.5);
		float vary = 0.35 + 0.9 * noise(vec2(p.y * 5.0 + (p.x < 0.0 ? 9.0 : 0.0), clock * 0.7));
		float hot = exp(-abs(d) * 85.0) * vary;
		float halo = exp(-max(dn, 0.0) * 5.5) * (1.0 - inR) * (0.5 + 0.5 * vary);
		float inner = exp(-max(-d, 0.0) * 13.0) * inR;
		float tips = smoothstep(1.0, 0.8, abs(yy));
		col += (vec3(1.0, 0.93, 0.9) * hot * 1.1 + vec3(1.0, 0.1, 0.17) * halo * 0.55 + vec3(1.0, 0.3, 0.34) * inner * 0.5) * glow * tips;
		// 合上的那一闪：整条线发白
		col += vec3(1.0, 0.9, 0.9) * exp(-abs(p.x) * 30.0) * smoothstep(1.05, 0.2, abs(yy)) * seal * 1.4;
	}
	COLOR = vec4(col, 1.0);
}" };

	/// <summary>他身上的错位（前景层读屏，椭圆软边）：split = 红边晃动的偏移；amp = 被打时整个人按横条错开的幅度。</summary>
	private static Shader Shimmer => _shim ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec4 box = vec4(0.0, 0.0, 1.0, 1.0);
uniform float amp = 0.0;
uniform float split = 0.0;
uniform float clock = 0.0;
uniform float stamp = 0.0;
uniform float on = 0.0;
float h11(float n) { return fract(sin(n * 127.1 + 311.7) * 43758.5453); }
float lum(vec3 c) { return dot(c, vec3(0.299, 0.587, 0.114)); }
void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 c = (box.xy + box.zw) * 0.5, hs = (box.zw - box.xy) * 0.5;
	vec2 q = (uv - c) / hs;
	float m = smoothstep(1.0, 0.6, length(q));
	if (m <= 0.001 || on <= 0.001) discard;
	// 横条错位：把身体分成高矮不一的几截，每截往左或往右挪
	float row = (uv.y - box.y) / (box.w - box.y);
	float cuts = 7.0;
	float id = floor(row * cuts + h11(floor(row * 3.0) + stamp) * 0.8);
	float dir = h11(id + stamp * 1.3) - 0.5;
	float sel = step(0.25, abs(dir));
	float dx = dir * 2.0 * sel * amp * m;
	vec3 a = texture(screen_tex, uv + vec2(dx, 0.0)).rgb;
	vec3 l = texture(screen_tex, uv + vec2(dx - split, 0.0)).rgb;
	vec3 r = texture(screen_tex, uv + vec2(dx + split, 0.0)).rgb;
	// 红色的重影边：左右两份和本体不一样的地方透出红
	float diff = abs(lum(l) - lum(a)) + abs(lum(r) - lum(a));
	vec3 col = a + vec3(1.0, 0.08, 0.16) * clamp(diff * 1.6, 0.0, 1.0) * 0.55 * m * on;
	// 错开的那几截边上一线亮
	float seam = smoothstep(0.06, 0.0, abs(fract(row * cuts + h11(floor(row * 3.0) + stamp) * 0.8) - 0.5) - 0.44) * sel * clamp(amp * 60.0, 0.0, 1.0);
	col += vec3(1.0, 0.7, 0.72) * seam * 0.25 * m;
	COLOR = vec4(col, 1.0);
}" };
}
