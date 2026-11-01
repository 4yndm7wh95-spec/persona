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
	/// <param name="weaponAt">权杖（圆环中心）在哪：Joker 身前（用户 2026-11-01：一支就够，在莲的前方）</param>
	/// <param name="weaponLen">权杖多长（屏幕像素）</param>
	/// <param name="exitAt">人格面具开始离场的时刻（出牌后几秒）：权杖和他一起消失</param>
	public static void Outbreak(Control layer, IReadOnlyList<(Vector2 Center, Vector2 Top)> enemies, float hit, Vector2 weaponAt, float weaponLen,
		float exitAt, float facing = 1f, IReadOnlyList<Action<Color>?>? tints = null)
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
			CardFx.CurseHands(layer, feet, c, h, 0.45f, hit, hit + 0.25f, enemies.Count > 1);
		}
		Replica(layer, weaponAt, weaponLen, 0.15f, spin0, spin1, hit, exitAt, 0.35f, facing);
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
				Flame.Wrap(layer, Stage.Back, feet, h * 1.3f, h * 2.4f, 0.1f, 0.75f);
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
			// 光柱：用户 2026-11-01 去掉
			Dome(layer, mid, radius);
			CardFx.Smoke(layer, mid + new Vector2(0f, -hMax * 0.9f), hMax);
		});
	}

	// ───────────── 中毒多结算几轮（触媒）─────────────

	/// <summary>毒性爆发正在结算：到这个时刻（毫秒）为止，每个敌人（节点 id）已经结算过几轮毒伤。</summary>
	private static ulong _armedUntil;
	private static readonly Dictionary<ulong, int> Ticks = new();

	/// <summary>毒性爆发出牌时调用：接下来几秒里的毒伤按轮次出特效（PoisonTickPatch → OnPoisonTick）。</summary>
	public static void Arm(float seconds)
	{
		Ticks.Clear();
		_armedUntil = Stage.NowMs() + (ulong)(seconds * 1000f);
	}

	/// <summary>
	/// 游戏结算了一轮毒伤（PoisonPower.Trigger 里每一轮一次；触媒让它多结算几轮）。毒性爆发结算期间：每个敌人的第一轮由主爆发演出，
	/// 之后每多一轮在这个敌人身上补一下毒火脉冲（Pulse），和伤害数字同一刻。
	/// </summary>
	public static void OnPoisonTick(Control layer, ulong id, Vector2 center, Vector2 top, Action<Color>? tint = null)
	{
		if (Stage.NowMs() > _armedUntil)
		{
			return;
		}
		Ticks.TryGetValue(id, out int n);
		Ticks[id] = n + 1;
		if (n >= 1)
		{
			Pulse(layer, center, top, n, tint);
		}
	}

	/// <summary>多结算的一轮：咒焰从脚下一窜、白紫一闪、两根光柱、一圈贴地的紫环、几颗火星。n = 第几轮（越往后越亮一点）。</summary>
	public static void Pulse(Control layer, Vector2 center, Vector2 top, int n, Action<Color>? tint = null)
	{
		Sfx.Play(Sfx.PoisonTick, 0.7f);
		if (tint != null)
		{
			// 敌人本身闪一下紫红（反馈在敌人身上，不靠盖住他的特效）
			Fx.Animate(layer, 0, 0.3, t => tint(Colors.White.Lerp(new Color(0.75f, 0.3f, 0.85f), 1f - Fx.OutCubic(t))));
			Fx.After(layer, 0.32, () => tint(Colors.White));
		}
		float h = Mathf.Max(80f, (center - top).Length());
		Vector2 feet = center + new Vector2(0f, h * 0.95f);
		float k = 1f + 0.12f * Mathf.Min(n, 4);
		// 用户 2026-11-01：别挡住敌人 —— 咒焰、光柱、地面环都在人物后面的背景层（敌人画在它们前面），光柱让到身体两侧；
		// 前景只留一下很小、很快的白紫闪和几颗火星
		Control back = Stage.Back ?? layer;
		Flame.Wrap(back, back, feet, h * 1.2f * k, h * 2.2f * k, 0f, 0.42f);
		Glow.Shockwave(back, feet, new Color(0.7f, 0.35f, 1f), h * 2.6f * k, 0.35f, 0.26f);
		Glow.Flash(layer, center, new Color(0.85f, 0.6f, 1f), h * 0.9f * k, 0.1f);
		CardFx.Embers(layer, feet, h, 8, 0.5f);
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
	float trail = exp(-d * 1.6) * smoothstep(0.0, 0.1, d) + exp(-d * 1.6) * 0.25;   // 刃尖处不要一条生硬的直边
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

	private static void Replica(Control layer, Vector2 c, float len, float appear, float spin0, float spin1, float hit, float exitAt, float exitDur,
		float facing)
	{
		if (Fx.Tex("izanagi_weapon") is not Texture2D tex)
		{
			return;
		}
		float k = len / tex.GetHeight();
		Vector2 ring = new(219.735f / 2f, 951.511f / 2f);              // weapon.json 的圆环中心（贴图是一半大小）
		float reach = Mathf.Max(ring.Y, tex.GetHeight() - ring.Y) * k;
		float life = exitAt + exitDur + 0.05f;
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
		bool pulsed = false;
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
			pivot.Rotation = theta * facing;
			float du = (spin1 - spin0) > 0f ? Mathf.Abs(u > 0f && u < 1f ? (u < 0.5f ? 12f * u * u : 3f * Mathf.Pow(-2f * u + 2f, 2f)) : 0f) / 3f : 0f;
			float spinning = u > 0f && u < 1f ? 1f : 0f;
			dm.SetShaderParameter("speed", Mathf.Clamp(0.35f * spinning + du, 0f, 1f) * inK);
			dm.SetShaderParameter("ang", facing > 0f ? theta - Mathf.Pi / 2f : -theta - Mathf.Pi / 2f);
			// 出手那一下白一闪；之后跟人格面具一起离场：往白里洗、往上飘一点、淡掉
			float pulse = s >= hit ? Mathf.Exp(-(s - hit) / 0.08f) : 0f;
			float outK = Mathf.Clamp((s - exitAt) / exitDur, 0f, 1f);
			gm.SetShaderParameter("whiten", Mathf.Clamp((1f - inK) * 0.9f + pulse + outK * 0.6f, 0f, 1f));
			gm.SetShaderParameter("alpha", Mathf.Min(1f, inK * 1.5f) * 0.95f * (1f - Fx.InCubic(outK)));
			aura.Modulate = new Color(0.3f, 0.6f, 1f, (0.5f + 0.5f * pulse) * Mathf.Min(1f, inK * 1.5f) * (1f - outK));
			pivot.Position = new Vector2(0f, -len * 0.08f * Fx.OutCubic(outK));
			if (!pulsed && s >= hit)
			{
				pulsed = true;
				Glow.Flash(layer, c, new Color(0.75f, 0.88f, 1f), len * 0.8f, 0.25f);
			}
		});
	}

	// ───────────── 光柱 ─────────────

	private static Shader? _beam;

	/// <summary>
	/// 一根光柱（加法）：横向 —— 白热的细芯 + 彩色光身 + 柔和的外晕；纵向 —— 底部贴地处更亮更宽，往上逐渐收、顶端被噪声打散；
	/// 表面有往上流的能量纹（竖条噪声随 clock 往上滚）和轻微闪烁。reach 0→1 从地面往上冲（前沿一颗亮头），thin 1→0 收细，fade 消散。
	/// </summary>
	private static Shader BeamShader => _beam ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(1.0, 0.85, 0.3, 1.0);
uniform float reach = 1.0;
uniform float thin = 1.0;
uniform float fade = 0.0;
uniform float clock = 0.0;
uniform float seed = 0.0;
uniform float aspect = 12.0;    // 高 / 宽
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vnoise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i + seed), hash(i + vec2(1.0, 0.0) + seed), f.x), mix(hash(i + vec2(0.0, 1.0) + seed), hash(i + vec2(1.0, 1.0) + seed), f.x), f.y);
}
void fragment() {
	float x = (UV.x - 0.5) * 2.0;           // -1..1
	float y = 1.0 - UV.y;                    // 0 = 地面，1 = 顶
	float w = thin * mix(1.0, 0.55, y);      // 往上收
	float ax = abs(x) / max(w, 0.001);
	float core = exp(-pow(ax / 0.1, 2.0));
	float body = exp(-pow(ax / 0.32, 2.0));
	float halo = exp(-pow(ax / 0.6, 2.0)) * 0.4;
	float edge = smoothstep(1.0, 0.7, abs(x));                       // 矩形两边不要硬切
	// 往上流的能量纹（竖条）
	float flow = vnoise(vec2(x * 7.0, y * aspect * 0.35 - clock * 9.0)) * 0.6 + vnoise(vec2(x * 16.0, y * aspect * 0.9 - clock * 14.0)) * 0.4;
	float streak = mix(0.35, 1.6, flow);
	// 冲上去：只到 reach，前沿一颗亮头
	float top = reach;
	float lead = exp(-pow((y - top) / 0.035, 2.0)) * step(0.02, reach) * (1.0 - fade);
	float on = smoothstep(top, top - 0.06, y);
	// 顶端被噪声打散、整体消散时从上往下 / 噪声蚀掉
	float breakup = smoothstep(0.65, 1.0, y) * vnoise(vec2(x * 5.0, y * 8.0 - clock * 4.0));
	float keep = smoothstep(fade * 1.2, fade * 1.2 + 0.2, vnoise(vec2(x * 3.0 + 7.0, y * 6.0)) * 0.8 + (1.0 - y) * 0.4) ;
	// 底部更亮
	float base = 1.0 + 1.2 * exp(-y / 0.06);
	float flick = 0.85 + 0.15 * vnoise(vec2(clock * 20.0, seed));
	float i = (core * 1.6 + body * streak * 1.1 + halo) * on * base * flick * (1.0 - breakup * 0.8) * keep * edge;
	vec3 col = mix(tint.rgb, vec3(1.0, 0.98, 0.95), clamp(core * 0.9 + lead, 0.0, 1.0));
	COLOR = vec4(col * (i + lead * body * 2.0 * edge), 1.0);
}" };

	private static readonly Color[] BeamCols = { new(1f, 0.82f, 0.25f), new(0.3f, 0.88f, 1f), new(1f, 0.9f, 0.45f), new(0.62f, 0.45f, 1f), new(0.4f, 0.95f, 1f) };

	/// <summary>敌人一带窜起的黄 / 青 / 紫光柱：错开出现、粗细高低不一。</summary>
	private static void Pillars(Control layer, Vector2 left, Vector2 right, float height)
	{
		const int n = 7;
		for (int i = 0; i < n; i++)
		{
			float x = Mathf.Lerp(left.X, right.X, (i + Fx.Rand(0.1f, 0.9f)) / n);
			bool big = i % 3 == 1;
			float w = big ? Fx.Rand(150f, 200f) : Fx.Rand(70f, 110f);
			float hgt = height * (big ? Fx.Rand(1.0f, 1.25f) : Fx.Rand(0.55f, 0.95f));
			Color col = BeamCols[(i * 2) % BeamCols.Length];
			float delay = i * 0.04f + Fx.Rand(0f, 0.06f);
			Fx.After(layer, delay, () => Beam(layer, x, left.Y, w, hgt, col, big ? 0.6f : 0.45f, big));
		}
	}

	/// <summary>一根光柱（BeamShader）+ 底部地面光晕、压扁的光环、横向闪光、沿柱子往上飘的光点。</summary>
	private static void Beam(Control layer, float x, float groundY, float w, float hgt, Color col, float life, bool big)
	{
		Node2D root = Fx.Root(layer, new Vector2(x, groundY), "P5MyriadPillar", life + 0.1);
		ColorRect beam = new()
		{
			Size = new Vector2(w, hgt), Position = new Vector2(-w / 2f, -hgt), MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = BeamShader },
		};
		root.AddChild(beam);
		ShaderMaterial m = (ShaderMaterial)beam.Material;
		m.SetShaderParameter("tint", col);
		m.SetShaderParameter("seed", Fx.Rand(0f, 50f));
		m.SetShaderParameter("aspect", hgt / w);
		Fx.Clock(beam);
		Fx.Animate(root, 0, life, t =>
		{
			float s = t * life;
			m.SetShaderParameter("reach", Ease5.OutExpo(Mathf.Min(1f, s / 0.12f)));
			m.SetShaderParameter("thin", 1f - 0.75f * Fx.InCubic(Ease5.Seg(t, 0.45f, 0.55f)));
			m.SetShaderParameter("fade", Ease5.Seg(t, 0.55f, 0.45f));
		});
		// 底部：一圈压扁的地面光晕 + 一道横向的光
		Glow.Flash(layer, new Vector2(x, groundY), col, w * 2.4f, 0.3f);
		Glow.Shockwave(layer, new Vector2(x, groundY), col, w * 3f, 0.35f, 0.22f);
		Node2D flare = Fx.Root(layer, new Vector2(x, groundY - 4f), "P5MyriadFlare", 0.3);
		Polygon2D line = Glow.TaperPoly(flare, new Color(col, 0.9f));
		Fx.Animate(flare, 0, 0.25, t =>
		{
			float lw = w * 2.2f * Ease5.OutExpo(Mathf.Min(1f, t * 3f));
			line.Polygon = Glow.TaperPts(new Vector2(-lw, 0f), new Vector2(lw, 0f), 6f * (1f - t));
		});
		// 沿柱子往上飘的光点
		Glow.Sparks(layer, new Vector2(x, groundY - hgt * 0.15f), big ? 18 : 10, 260f, 700f, 0.5f,
			new[] { new Color(1f, 1f, 0.95f), col, new Color(col, 0f) }, -500f, 0.08f, 0.22f, Vector2.Up, 12f);
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
