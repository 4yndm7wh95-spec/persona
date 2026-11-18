using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 紧勒（0.9.49 审阅：敌人那里出现一圈像被绳子勒住的特效，要是特效不要贴图；这回合每打一张牌敌人掉血时都要有反馈）。
/// 绳索全是着色器画的：敌人脖子 / 上身一圈压扁的椭圆，三股绞在一起的暗红黑绳（每股沿圈扭转、明暗交替 = 麻绳的绞纹），
/// 外沿一道细的红光，后半圈（离镜头远）淡一些。
/// · 套上（Bind，出牌时）：绳圈从大一圈的地方猛地收紧到身上（过冲回弹），一声「咔」、勒出一圈碎光、身体一暗，之后慢慢松开淡掉；
/// · 每勒一次（Cinch，StranglePower 每次结算，挂在 PowerModel.Flash 上）：绳圈一下出现、猛地再勒紧一截（更细、更狠）、红光一闪、极短的顿帧、身体一暗。
/// </summary>
public static class StrangleFx
{
	private static Shader? _rope;

	private static Shader RopeShader => _rope ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float radius = 1.0;     // 绳圈大小（1 = 贴在身上）
uniform float thick = 0.18;     // 绳粗（单位 = 半径）
uniform float squeeze = 0.0;    // 勒紧时绳更细更亮
uniform float alpha = 1.0;
uniform float clock = 0.0;
uniform float seed = 0.0;
uniform float half_side = 0.0;   // 1 = 只画前半圈（在敌人身前），-1 = 只画后半圈（在敌人身后）
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y); }
void fragment() {
	vec2 q = (UV - 0.5) * 2.0 / 0.82;
	float r = length(q);
	float th = atan(q.y, q.x);
	float front = smoothstep(-0.12, 0.12, sin(th));           // 下半圈离镜头近
	float keep = half_side > 0.5 ? front : (half_side < -0.5 ? 1.0 - front : 1.0);
	float t = thick * (1.0 - 0.35 * squeeze);
	float body = 0.0, shade = 0.0, hi = 0.0;
	for (int k = 0; k < 3; k++) {
		float ph = th * 9.0 + float(k) * 2.094 + clock * 0.6;  // 三股绳绕着彼此转
		float off = sin(ph) * t * 0.45;
		float d = abs(r - radius - off) / (t * 0.42);
		float strand = 1.0 - smoothstep(0.7, 1.0, d);
		float lit = cos(ph) * 0.5 + 0.5;                       // 转到前面的那一段亮
		float m = strand * step(body, strand * (0.6 + 0.4 * lit));
		body = max(body, strand);
		shade = mix(shade, lit * (1.0 - d * 0.5), m);
		hi = max(hi, strand * pow(lit, 6.0) * (1.0 - d));
	}
	float fray = noise(vec2(th * 40.0, r * 30.0) + seed);
	float glow = exp(-abs(r - radius) / (t * 1.4)) * (0.35 + 0.65 * squeeze);
	vec3 col = vec3(0.07, 0.0, 0.015) * body
		+ vec3(0.6, 0.03, 0.09) * body * shade * (0.5 + 0.5 * fray)
		+ vec3(1.0, 0.75, 0.72) * hi * (0.4 + 0.6 * squeeze)
		+ vec3(0.9, 0.05, 0.14) * glow * (1.0 - body) * 0.8;
	float dimBack = mix(0.55, 1.0, front);
	float a = clamp(body * 0.95 + glow * 0.4, 0.0, 1.0) * dimBack * keep * alpha;
	COLOR = vec4(col * dimBack * keep * alpha, a);
}" };

	/// <summary>一圈绳：中心 at，半宽 rx（屏幕像素），压扁成 ry；返回材质和根节点。</summary>
	private sealed class RopeSet
	{
		public readonly List<ShaderMaterial> Mats = new();
		public void Set(string k, Variant v) { foreach (var m in Mats) m.SetShaderParameter(k, v); }
	}

	/// <summary>一圈绳：后半圈画在敌人身后（back），前半圈画在敌人身前（front）—— 这样才像套在身上，不是浮着的光环。</summary>
	private static (Node2D Root, RopeSet M) Rope(Control front, Control back, Vector2 at, float rx, float ry, double life)
	{
		var set = new RopeSet();
		float seed = Fx.Rand(0f, 20f);
		Node2D? first = null;
		foreach (var (layer, side) in new[] { (back, -1f), (front, 1f) })
		{
			Node2D root = Fx.Root(layer, at, "P5Strangle", life);
			ShaderMaterial m = new() { Shader = RopeShader };
			m.SetShaderParameter("seed", seed);
			m.SetShaderParameter("half_side", ReferenceEquals(front, back) ? 0f : side);
			Vector2 size = new(rx * 2f * 1.7f, rx * 2f * 1.7f);
			ColorRect r = new() { Size = size, Position = -size * 0.5f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			root.AddChild(r);
			root.Scale = new Vector2(1f, ry / rx);
			root.Rotation = -0.08f;
			set.Mats.Add(m);
			first ??= root;
			if (ReferenceEquals(front, back)) break;
		}
		return (first!, set);
	}

	/// <summary>绳圈套在哪：脖子 / 上身（中心和头顶之间偏上）。</summary>
	private static (Vector2 At, float Rx) Place(Vector2 center, Vector2 top, float width)
	{
		float h = Mathf.Max(80f, (center - top).Length());
		return (center.Lerp(top, 0.3f), Mathf.Clamp(width * 0.3f, h * 0.3f, h * 0.7f));
	}

	/// <summary>套上：出牌那一下。</summary>
	public static void Bind(Control layer, Control? back, Vector2 center, Vector2 top, float width, System.Action<Color>? tint, float delay = 0f)
	{
		Fx.After(layer, delay, () =>
		{
			var (at, rx) = Place(center, top, width);
			float u = Fx.Screen(layer).Y / 1080f;
			var (root, m) = Rope(layer, back ?? layer, at, rx, rx * 0.3f, 1.4);
			Sfx.Take("gx_whoosh", 0.12f);
			Sfx.Take("gx_tock", 0.2f, 0.2f);
			Fx.Animate(root, 0, 1.35, t =>
			{
				float s = t * 1.35f;
				float k = Ease5.OutBack(Mathf.Min(1f, s / 0.22f));
				m.Set("radius", Mathf.Lerp(1.55f, 1f, k));
				m.Set("squeeze", Mathf.Exp(-Mathf.Pow((s - 0.22f) / 0.08f, 2f)));
				m.Set("clock", s * 3f);
				m.Set("alpha", Mathf.Min(1f, s / 0.06f) * (1f - Ease5.Seg(s, 0.9f, 0.45f)));
			});
			Fx.After(layer, 0.2f, () =>
			{
				HitStop.Request(0.04);
				Glow.Flash(layer, at, new Color(1f, 0.15f, 0.22f), rx * 2.2f, 0.3f);
				Phantom.Shards(layer, at, 10, 200f * u, 560f * u, Vector2.Up, 180f, 0.3f, 400f * u, 12f * u);
				if (tint != null) Fx.Animate(layer, 0, 0.5, tt => tint(Colors.White.Lerp(new Color(0.55f, 0.35f, 0.42f), (1f - tt) * (1f - tt))));
			});
		});
	}

	/// <summary>每勒一次：绳圈一下出现、再狠狠勒紧一截。</summary>
	public static void Cinch(Control layer, Control? back, Vector2 center, Vector2 top, float width, System.Action<Color>? tint)
	{
		var (at, rx) = Place(center, top, width);
		float u = Fx.Screen(layer).Y / 1080f;
		var (root, m) = Rope(layer, back ?? layer, at, rx, rx * 0.3f, 0.75);
		Sfx.Take("gx_tock", 0.18f);
		Fx.Animate(root, 0, 0.7, t =>
		{
			float s = t * 0.7f;
			float k = Ease5.OutBack(Mathf.Min(1f, s / 0.12f));
			m.Set("radius", Mathf.Lerp(1.15f, 0.88f, k));
			m.Set("squeeze", Mathf.Exp(-Mathf.Pow((s - 0.12f) / 0.07f, 2f)));
			m.Set("clock", s * 4f);
			m.Set("alpha", Mathf.Min(1f, s / 0.04f) * (1f - Ease5.Seg(s, 0.35f, 0.35f)));
		});
		Fx.After(layer, 0.11f, () =>
		{
			HitStop.Request(0.03);
			Glow.Flash(layer, at, new Color(1f, 0.12f, 0.2f), rx * 1.8f, 0.25f);
			Glow.Flash(layer, at, new Color(1f, 0.8f, 0.78f), rx * 0.7f, 0.08f);
			Phantom.Shards(layer, at, 6, 180f * u, 460f * u, Vector2.Up, 180f, 0.26f, 400f * u, 10f * u);
			if (tint != null) Fx.Animate(layer, 0, 0.35, tt => tint(Colors.White.Lerp(new Color(0.6f, 0.38f, 0.45f), (1f - tt) * (1f - tt))));
		});
	}
}
