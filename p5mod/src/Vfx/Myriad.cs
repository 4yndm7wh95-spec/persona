using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 毒性爆发（稀有）：伊邪那岐大神的「万物真言」（用户 2026-11-01 给的原版录像），叠在咒怨毒火上（带毒的牌都有毒火和手）。
/// 时间从出牌算起（人格面具在 0.04s 出现，见 Aim.PlayCast；伊邪那岐大神转杖第 15～31 帧 ≈ 0.31～0.84s，第 33 帧出手 = hit）：
///  · 0.15s：每个敌人面前浮现一把他的环形长兵器（白光里显形、从大一圈收到原大），竖着；
///  · 0.31～0.84s：兵器跟他同步转一整圈（缓入缓出），一圈蓝白光盘跟着转、两端拖着白色弧光；
///    0.45s 起咒怨之手从敌人脚下钻出来；
///  · hit：兵器一白、碎掉；每个敌人身上白紫星光炸开、四周窜起黄 / 青光柱；一层半透明的光膜鼓起罩住全部敌人，白屏一下；
///    咒怨之手拍下去，咒焰柱从每个敌人脚下蹿起，光膜被染成紫黑、往里塌掉，黑烟、火星，敌人染暗红。
/// enemies：(中心, 头顶) 的列表。
/// </summary>
public static class Myriad
{
	public static void Outbreak(Control layer, IReadOnlyList<(Vector2 Center, Vector2 Top)> enemies, float hit, IReadOnlyList<Action<Color>?>? tints = null)
	{
		if (enemies.Count == 0)
		{
			return;
		}
		float spin0 = 0.04f + (15 - 7) / 30f, spin1 = 0.04f + (31 - 7) / 30f;
		Vector2 min = new(float.MaxValue, float.MaxValue), max = new(float.MinValue, float.MinValue);
		float hMax = 80f;
		for (int i = 0; i < enemies.Count; i++)
		{
			var (c, top) = enemies[i];
			float h = Mathf.Max(80f, (c - top).Length());
			hMax = Mathf.Max(hMax, h);
			Vector2 feet = c + new Vector2(0f, h * 0.95f);
			min = min.Min(top);
			max = max.Max(feet);
			Replica(layer, c, h, 0.15f, spin0, spin1, hit);
			CardFx.CurseHands(layer, feet, c, h, 0.45f, hit, hit + 0.25f, enemies.Count > 1);
		}
		Fx.After(layer, hit, () =>
		{
			Fx.Shake(1);
			Sfx.Play(Sfx.Curse, 1f);
			Vector2 mid = (min + max) / 2f;
			float radius = Mathf.Max((max - min).X * 0.5f + hMax * 0.6f, hMax * 1.3f);
			for (int i = 0; i < enemies.Count; i++)
			{
				var (c, top) = enemies[i];
				float h = Mathf.Max(80f, (c - top).Length());
				Vector2 feet = c + new Vector2(0f, h * 0.95f);
				// 白紫星光
				Glow.Flash(layer, c, new Color(1f, 0.96f, 1f), h * 1.6f, 0.16f);
				Glow.Flash(layer, c, new Color(0.7f, 0.35f, 1f), h * 3f, 0.4f);
				Glow.Glint(layer, c + new Vector2(0f, -h * 0.2f), 2.2f);
				Glow.SpeedLines(layer, c, new Color(0.85f, 0.7f, 1f), 14, h * 0.3f, h * 1.8f, 0.3f);
				// 咒焰（毒火）
				Flame.Column(layer, feet + new Vector2(0f, 8f), h * 1.7f, h * 3f, 0.1f, 0.75f, 1.4f);
				CardFx.Embers(layer, feet, h, 26, 1f);
				if (tints != null && i < tints.Count && tints[i] is Action<Color> tint)
				{
					Fx.Animate(layer, 0.1, 0.7, t =>
					{
						float k = Mathf.Min(1f, t * 6f) * (1f - Fx.InCubic(Ease5.Seg(t, 0.55f, 0.45f)));
						tint(Colors.White.Lerp(new Color(0.55f, 0.16f, 0.3f), k));
					});
					Fx.After(layer, 0.85, () => tint(Colors.White));
				}
			}
			Pillars(layer, new Vector2(min.X - hMax * 0.6f, max.Y), new Vector2(max.X + hMax * 0.6f, max.Y), hMax * 3.6f);
			Dome(layer, mid, radius);
			CardFx.Smoke(layer, mid + new Vector2(0f, -hMax * 0.9f), hMax);
		});
	}

	// ───────────── 兵器复制体 ─────────────

	private static Shader? _disc, _ghost;

	/// <summary>转动的蓝白光盘：越靠近两端的刃尖越亮、往后拖着淡掉；外沿一道白弧。speed 0～1 = 转得多快。</summary>
	private static Shader DiscShader => _disc ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float speed = 0.0;
uniform float ang = 0.0;        // 刃尖方向（屏幕里的角度，y 朝下）
uniform float seed = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vnoise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i + seed), hash(i + vec2(1.0, 0.0) + seed), f.x), mix(hash(i + vec2(0.0, 1.0) + seed), hash(i + vec2(1.0, 1.0) + seed), f.x), f.y);
}
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p);
	float a = atan(p.y, p.x);
	float d = mod(ang - a + 31.4159, 3.14159);                     // 刃尖后面多远（两端对称，周期 π）
	float trail = exp(-d * 1.6);
	float band = smoothstep(1.0, 0.9, r) * smoothstep(0.08, 0.3, r);
	float streak = vnoise(vec2(r * 22.0, d * 2.0));
	float disc = band * (0.25 + 0.75 * trail) * (0.45 + 0.55 * streak) * speed * 1.8;
	float rim = smoothstep(0.035, 0.0, abs(r - 0.93)) * trail * speed * 2.4;
	vec3 col = mix(vec3(0.12, 0.38, 1.0), vec3(0.85, 0.95, 1.0), clamp(trail * 0.6 + rim, 0.0, 1.0));
	COLOR = vec4(col * (disc + rim), 1.0);
}" };

	/// <summary>兵器显形：whiten 0～1 往白里洗（显形和碎掉时），alpha 整体透明度。</summary>
	private static Shader GhostShader => _ghost ??= new Shader { Code = @"
shader_type canvas_item;
uniform float whiten = 0.0;
uniform float alpha = 1.0;
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	vec3 col = mix(c.rgb, vec3(0.92, 0.97, 1.0), whiten);
	COLOR = vec4(col, c.a * alpha);
}" };

	private static void Replica(Control layer, Vector2 c, float h, float appear, float spin0, float spin1, float hit)
	{
		if (Fx.Tex("izanagi_weapon") is not Texture2D tex)
		{
			return;
		}
		float len = h * 3.6f, k = len / tex.GetHeight();
		Vector2 ring = new(219.735f / 2f, 951.511f / 2f);              // weapon.json 的圆环中心（贴图是一半大小）
		float reach = Mathf.Max(ring.Y, tex.GetHeight() - ring.Y) * k;
		float life = hit + 0.35f;
		Node2D root = Fx.Root(layer, c, "P5MyriadWeapon", life);
		// 光盘（在兵器后面）
		float ds = reach * 2.1f;
		ColorRect disc = new()
		{
			Size = new Vector2(ds, ds), Position = new Vector2(-ds / 2f, -ds / 2f), MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = DiscShader },
		};
		root.AddChild(disc);
		ShaderMaterial dm = (ShaderMaterial)disc.Material;
		dm.SetShaderParameter("seed", Fx.Rand(0f, 50f));
		Node2D pivot = new();
		root.AddChild(pivot);
		// 外面一层淡蓝的光（加法、放大一点），里面是兵器本体
		Sprite2D aura = new() { Texture = tex, Centered = false, Offset = -ring, Scale = Vector2.One * k * 1.04f, Material = Fx.Additive, Modulate = new Color(0.3f, 0.6f, 1f, 0.5f) };
		ShaderMaterial gm = new() { Shader = GhostShader };
		Sprite2D body = new() { Texture = tex, Centered = false, Offset = -ring, Scale = Vector2.One * k, Material = gm, TextureFilter = CanvasItem.TextureFilterEnum.Linear };
		pivot.AddChild(aura);
		pivot.AddChild(body);
		root.Visible = false;
		bool broke = false;
		Fx.Animate(root, 0, life, t =>
		{
			float s = t * life;
			root.Visible = s >= appear;
			float inK = Mathf.Clamp((s - appear) / 0.14f, 0f, 1f);
			pivot.Scale = Vector2.One * Mathf.Lerp(1.25f, 1f, Ease5.OutExpo(inK));
			// 转：跟伊邪那岐大神同步转一圈（缓入缓出）
			float u = Mathf.Clamp((s - spin0) / (spin1 - spin0), 0f, 1f);
			float e = u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) / 2f;
			float theta = Mathf.Tau * e;
			pivot.Rotation = theta;
			float du = (spin1 - spin0) > 0f ? Mathf.Abs(u > 0f && u < 1f ? (u < 0.5f ? 12f * u * u : 3f * Mathf.Pow(-2f * u + 2f, 2f)) : 0f) / 3f : 0f;
			float spinning = u > 0f && u < 1f ? 1f : 0f;
			dm.SetShaderParameter("speed", Mathf.Clamp(0.35f * spinning + du, 0f, 1f) * inK);
			dm.SetShaderParameter("ang", theta - Mathf.Pi / 2f);
			float white = (1f - inK) * 0.9f + (s >= hit ? 1f : 0f);
			gm.SetShaderParameter("whiten", Mathf.Clamp(white, 0f, 1f));
			float outK = Mathf.Clamp((s - hit) / 0.25f, 0f, 1f);
			gm.SetShaderParameter("alpha", Mathf.Min(1f, inK * 1.5f) * 0.92f * (1f - outK));
			aura.Modulate = new Color(0.3f, 0.6f, 1f, 0.5f * Mathf.Min(1f, inK * 1.5f) * (1f - outK));
			pivot.Scale *= 1f + 0.15f * outK;
			if (!broke && s >= hit)
			{
				broke = true;
				Glow.Shards(layer, c, 12, 1.1f, null, Mathf.Pi);
			}
		});
	}

	// ───────────── 光柱 ─────────────

	/// <summary>敌人一带随机窜起的黄 / 青细光柱（加法），从地面往上 0.08s 窜满，0.3s 变细淡掉，错开出现。</summary>
	private static void Pillars(Control layer, Vector2 left, Vector2 right, float height)
	{
		Color[] cols = { new(1f, 0.85f, 0.25f, 0.9f), new(0.3f, 0.9f, 1f, 0.9f), new(1f, 0.92f, 0.5f, 0.9f), new(0.55f, 0.8f, 1f, 0.9f) };
		const int n = 6;
		for (int i = 0; i < n; i++)
		{
			float x = Mathf.Lerp(left.X, right.X, (i + Fx.Rand(0.05f, 0.95f)) / n);
			float w = Fx.Rand(5f, 18f), hgt = height * Fx.Rand(0.45f, 1.15f);
			Color col = cols[(i * 3) % cols.Length];
			float delay = i * 0.045f + Fx.Rand(0f, 0.08f);
			Fx.After(layer, delay, () =>
			{
				Node2D root = Fx.Root(layer, new Vector2(x, left.Y), "P5MyriadPillar", 0.45);
				Polygon2D glow = Fx.Poly(root, new Vector2[4], new Color(col, col.A * 0.4f), additive: true);
				Polygon2D core = Fx.Poly(root, new Vector2[4], col, additive: true);
				Fx.Animate(root, 0, 0.4, t =>
				{
					float up = Ease5.OutExpo(Mathf.Min(1f, t / 0.2f)) * hgt;
					float ww = w * (1f - Fx.InCubic(Ease5.Seg(t, 0.25f, 0.75f)));
					glow.Polygon = new[] { new Vector2(-ww * 1.8f, 0f), new Vector2(ww * 1.8f, 0f), new Vector2(ww * 1.2f, -up), new Vector2(-ww * 1.2f, -up) };
					core.Polygon = new[] { new Vector2(-ww * 0.5f, 0f), new Vector2(ww * 0.5f, 0f), new Vector2(ww * 0.3f, -up), new Vector2(-ww * 0.3f, -up) };
				});
			});
		}
	}

	// ───────────── 光膜 ─────────────

	private static Shader? _dome;

	/// <summary>半透明的光膜球：边缘（菲涅尔）亮、中间透；dark 0→1 染成紫黑。</summary>
	private static Shader DomeShader => _dome ??= new Shader { Code = @"
shader_type canvas_item;
uniform float alpha = 1.0;
uniform float dark = 0.0;
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p);
	float fres = pow(clamp(r, 0.0, 1.0), 4.0);
	vec3 light = mix(vec3(0.6, 0.75, 1.0), vec3(0.97, 0.98, 1.0), fres);
	vec3 col = mix(light, vec3(0.22, 0.02, 0.26), dark);
	float a = (0.1 + 0.62 * fres) * step(r, 1.0) * smoothstep(1.0, 0.985, r) * alpha;
	COLOR = vec4(col, a);
}" };

	private static void Dome(Control layer, Vector2 at, float radius)
	{
		Node2D root = Fx.Root(layer, at, "P5MyriadDome", 0.9);
		root.Scale = new Vector2(1f, 0.85f);
		float size = radius * 2f;
		ColorRect r = new()
		{
			Size = new Vector2(size, size), Position = new Vector2(-radius, -radius), PivotOffset = new Vector2(radius, radius),
			MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = DomeShader },
		};
		root.AddChild(r);
		ShaderMaterial m = (ShaderMaterial)r.Material;
		// 0.04s 后白屏一下（只罩住这一片）
		Glow.Flash(layer, at, new Color(1f, 1f, 1f), radius * 2.6f, 0.14f, 0.05f);
		Fx.Animate(root, 0, 0.85, t =>
		{
			float s = t * 0.85f;
			float grow = Ease5.OutExpo(Mathf.Min(1f, s / 0.2f));
			float collapse = Fx.InCubic(Ease5.Seg(s, 0.35f, 0.45f));
			r.Scale = Vector2.One * (0.55f + 0.5f * grow) * (1f - 0.45f * collapse);
			m.SetShaderParameter("dark", Ease5.Seg(s, 0.15f, 0.25f));
			m.SetShaderParameter("alpha", Mathf.Min(1f, s / 0.05f) * (1f - collapse));
		});
	}
}
