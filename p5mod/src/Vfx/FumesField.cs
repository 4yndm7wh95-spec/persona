using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 毒雾的常驻咒怨雾（用户 2026-11-13）：
/// · 横跨整个画面、贴着地面的一层暗红咒怨雾，左右上下都没有边（屏幕坐标铺满整宽；上下按噪声起伏、软软地淡掉）；
/// · 打出时像一阵风从屏幕左边很快地吹进来，再慢下来（前沿 OutExpo，流速从快衰减到很慢的漂移），吹的时候带横向的风丝；
/// · 之后一直在：慢慢流动，小怨灵时不时从雾里冒出一两个；每回合毒雾触发（Pulse）再来一阵风、冒几个怨灵；
/// · 毒雾能力没了 / 出了战斗（Alive 返回 false）就慢慢散掉。后面一层在人物身后，前面一层很淡、只贴着脚边，让人物站在雾里。
/// </summary>
public sealed class FumesField
{
	/// <summary>游戏里设：Joker 还在战斗里、身上还有毒雾。null（预览）= 活 PreviewLife 秒。</summary>
	public static Func<bool>? Alive;
	public static float PreviewLife = 6f;
	public static FumesField? Current;

	private static Shader? _shader;

	private static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float floor_y = 0.75;
uniform float band = 0.25;
uniform float front = 1.3;
uniform float flow = 0.0;
uniform float gust = 0.0;
uniform float alpha = 1.0;
uniform float aspect = 1.78;
uniform float seed = 0.0;
uniform float clock = 0.0;
uniform float dens = 1.0;
uniform float low = 0.0;      // 前面那层：只留贴地的一薄层
uniform vec3 deep = vec3(0.12, 0.0, 0.03);
uniform vec3 mid = vec3(0.72, 0.04, 0.14);
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; } return v; }
void fragment() {
	vec2 uv = UV;
	float x = uv.x * aspect;
	float h = (floor_y - uv.y) / band;                 // 0 地面，1 雾带顶；地面以下为负
	float n = fbm(vec2(x * 1.6 - flow, h * 1.1 + clock * 0.04) + seed);
	float n2 = fbm(vec2(x * 4.2 - flow * 1.7, h * 2.6 - clock * 0.07) + seed * 1.7);
	float top = mix(0.55 + 0.75 * n, 0.22 + 0.25 * n, low);
	// 上下都软：地面往下慢慢淡掉（没有底线），往上按噪声起伏的雾顶淡掉（没有顶线）
	float prof = smoothstep(-1.0, 0.1, h) * (1.0 - smoothstep(top * 0.25, top, h));
	// 风的前沿：带噪声、软
	float edge = uv.x + (n2 - 0.5) * 0.16;
	float lead = 1.0 - smoothstep(front - 0.18, front + 0.02, edge);
	// 风丝：横向拉长的细丝，吹进来的时候亮
	float st = fbm(vec2(x * 0.7 - flow * 2.4, uv.y / band * 7.0 + seed));
	float streak = smoothstep(0.55, 0.82, st) * gust * smoothstep(-0.6, 0.2, h) * (1.0 - smoothstep(0.5, 1.3, h));
	float holes = smoothstep(0.2, 0.7, fbm(vec2(x * 2.8 - flow * 1.3, h * 1.8) + seed * 3.1));
	float d = prof * lead * (0.3 + 0.7 * n2) * mix(0.45, 1.0, holes) * dens;
	vec3 col = mix(deep, mid, clamp(pow(n2 * n * 2.0, 2.0), 0.0, 1.0)) * d;
	col += mid * streak * 0.55 * lead;
	float a = clamp(d * 0.8 + streak * 0.2 * lead, 0.0, 0.82) * alpha;
	COLOR = vec4(col * alpha, a);
}" };

	private readonly Control _front;
	private readonly Control? _back;
	private readonly float _tall;
	private readonly SceneTree _tree;
	private readonly List<(Node2D Root, ShaderMaterial M)> _layers = new();
	private float _t, _flow, _speed, _gust, _fade = -1f, _nextGhost;
	private readonly float _floorWorldY;

	/// <summary>开始（或已经有了：再吹一阵风）。feet：Joker 脚底（世界坐标），决定地面高度。</summary>
	public static FumesField? Start(Control front, Control? back, Vector2 feet, float tall)
	{
		if (Current is { } cur && cur.Valid)
		{
			cur.Pulse();
			return cur;
		}
		if (!GodotObject.IsInstanceValid(front) || front.GetTree() is not SceneTree tree) return null;
		Current = new FumesField(front, back, feet, tall, tree);
		return Current;
	}

	/// <summary>风吹到屏幕横坐标 sx（0～1）要几秒（出牌时让敌人在雾漫过时一闪）。</summary>
	public static float ArriveAt(float sx)
	{
		// front(t) = -0.2 + 1.55 * OutExpo(t / Entry) → 解出 t
		float k = Mathf.Clamp((sx + 0.2f) / 1.55f, 0f, 0.999f);
		return Entry * -Mathf.Log(1f - k) / (10f * Mathf.Log(2f));
	}

	private const float Entry = 1.1f;

	private bool Valid => GodotObject.IsInstanceValid(_front) && _layers.Count > 0 && GodotObject.IsInstanceValid(_layers[0].Root);

	private FumesField(Control front, Control? back, Vector2 feet, float tall, SceneTree tree)
	{
		_front = front;
		_back = back;
		_tall = tall;
		_tree = tree;
		_floorWorldY = feet.Y;
		_speed = 1.6f;
		_gust = 1f;
		_nextGhost = 0.35f;
		float seed = Fx.Rand(0f, 50f);
		foreach ((Control? layer, float dens, float low) in new[] { (back ?? front, 1f, 0f), ((Control?)front, 0.3f, 1f) })
		{
			if (layer == null || !GodotObject.IsInstanceValid(layer)) continue;
			Node2D root = Fx.ScreenRoot(layer, "P5FumesField");
			Vector2 scr = Fx.Screen(layer);
			ShaderMaterial m = new() { Shader = Shader };
			ColorRect q = new() { Size = scr, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(q);
			m.SetShaderParameter("seed", seed);
			m.SetShaderParameter("aspect", scr.X / scr.Y);
			m.SetShaderParameter("dens", dens);
			m.SetShaderParameter("low", low);
			m.SetShaderParameter("front", -0.2f);
			Fx.Clock(q);
			_layers.Add((root, m));
		}
		_tree.ProcessFrame += Tick;
		Tick();
	}

	/// <summary>再来一阵风（每回合毒雾触发、再打一张毒雾）：风丝亮起、流速一下变快、冒几个怨灵。</summary>
	public void Pulse()
	{
		_gust = 1f;
		_speed = Mathf.Max(_speed, 1.1f);
		for (int i = 0; i < 3; i++) Ghost(0.1f + 0.15f * i);
	}

	private void Tick()
	{
		if (!Valid)
		{
			Free();
			return;
		}
		float dt = Mathf.Min((float)_layers[0].Root.GetProcessDeltaTime(), 0.1f);
		_t += dt;
		// 流速：吹进来时很快，指数衰减到很慢的漂移
		_speed = Mathf.Lerp(_speed, 0.06f, 1f - Mathf.Exp(-dt * 2.2f));
		_flow += _speed * dt;
		_gust = Mathf.Max(0f, _gust - dt * 0.9f);
		float front = -0.2f + 1.55f * Ease5.OutExpo(Mathf.Min(1f, _t / Entry));
		bool alive = _t < 2.5f || (Alive?.Invoke() ?? _t < PreviewLife);   // 前 2.5s 不查：能力可能比特效晚一点挂上
		if (!alive && _fade < 0f) _fade = 0f;
		float a = Mathf.Min(1f, _t / 0.15f);
		if (_fade >= 0f)
		{
			_fade += dt;
			a *= 1f - Mathf.Clamp(_fade / 1.2f, 0f, 1f);
			if (_fade > 1.25f)
			{
				Free();
				return;
			}
		}
		Vector2 scr = Fx.Screen(_front);
		float floorY = Fx.ScreenPoint(_front, new Vector2(0f, _floorWorldY)).Y / scr.Y;
		float tallS = _tall * _front.GetCanvasTransform().Scale.Y / scr.Y;
		foreach (var (_, m) in _layers)
		{
			m.SetShaderParameter("front", front);
			m.SetShaderParameter("flow", _flow);
			m.SetShaderParameter("gust", _gust);
			m.SetShaderParameter("alpha", a);
			m.SetShaderParameter("floor_y", floorY + tallS * 0.04f);
			m.SetShaderParameter("band", tallS * 0.95f);
		}
		// 小怨灵：时不时冒一两个（风还没吹到的地方不冒）
		if (_fade < 0f && _t > _nextGhost)
		{
			_nextGhost = _t + Fx.Rand(1.0f, 2.4f);
			int n = Fx.Rand(0f, 1f) < 0.35f ? 2 : 1;
			for (int i = 0; i < n; i++) Ghost(i * 0.3f, Mathf.Min(1f, front));
		}
	}

	private void Ghost(float delay, float maxX = 1f)
	{
		if (!GodotObject.IsInstanceValid(_front)) return;
		Vector2 scr = Fx.Screen(_front);
		Transform2D inv = _front.GetCanvasTransform().AffineInverse();
		float sx = Fx.Rand(0.04f, Mathf.Max(0.08f, maxX - 0.04f)) * scr.X;
		Vector2 at = inv * new Vector2(sx, 0f);
		at.Y = _floorWorldY - _tall * Fx.Rand(-0.02f, 0.12f);
		bool behind = Fx.Rand(0f, 1f) < 0.6f && _back != null && GodotObject.IsInstanceValid(_back);
		Paint.Rise(behind ? _back! : _front, Fx.Rand(0f, 1f) < 0.35f ? "curse_ghost" : "curse_soul", at, _tall * Fx.Rand(0.35f, 0.6f),
			Fx.Rand(0.45f, 0.7f), Lut5.Curse, Fx.Rand(0.9f, 1.3f), delay);
	}

	private void Free()
	{
		_tree.ProcessFrame -= Tick;
		foreach (var (root, _) in _layers)
		{
			if (GodotObject.IsInstanceValid(root)) root.QueueFree();
		}
		_layers.Clear();
		if (Current == this) Current = null;
	}
}
