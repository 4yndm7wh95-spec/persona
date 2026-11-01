using Godot;

namespace P5Joker;

/// <summary>
/// 亚森（用户 2026-11-01：召唤 Persona 时身后要出现 Persona，配上亚森的施法动作）。
/// 序列帧是原版模型渲的施法动作 BPS0201_000_main_0005（art/p5/fx/arsene/cast，49 帧，tools/p5/stage_p5.py 打成图集 arsene_cast，
/// 每行 7 帧、每帧 671x461）。在背景层（人物后面），Joker 身后上方悬浮、比他高大：
///  · 出场：身后蓝焰一炸，亚森从下往上被蓝焰「烧」出来（着色器：噪声阈值从下往上推进，烧到的边是白热→蓝的火边，刚烧出来的部分带蓝光）；
///  · 施法：30fps 播原版动作，第 18～24 帧双翼展开——正好在眼神特写被劈开的那一下露出来；
///  · 退场：从下往上消失、往上升腾化成蓝焰，蓝色火星往上飘。
/// </summary>
public static class Persona
{
	/// <summary>
	/// 一个人格面具的序列帧（tools/p5/stage_p5.py 打成图集）：图集名、每行几帧、每帧尺寸、一帧里的脚 / 身体中心（锚点 × 0.45）、
	/// 一帧里身体（头顶到脚）多高、只播哪一段帧（原速 30fps）、哪一帧出手、离场时刻。
	/// </summary>
	public sealed record Spec(string Tex, int Cols, float FrameW, float FrameH, Vector2 Foot, Vector2 Body, float BodyPx, int First, int Last,
		int AttackFrame)
	{
		public float AttackAt => (AttackFrame - First) / Fps;

		/// <summary>出手后再停多久才被蓝焰吞掉离场（高潮那一拍他要在场；伸手抓人时要等手收回来）。</summary>
		public float Linger { get; init; } = 0.35f;

		public float ExitAt => AttackAt + Linger;

		public int FrameAt(float s) => Mathf.Clamp(First + (int)(s * Fps), First, Last);
	}

	/// <summary>
	/// 亚森：施法动作 BPS0201_000_main_0005 只播第 5～40 帧原速（用户 2026-11-01：不要加速他的动作）——
	/// 从侧身转过来 → 双翼完全展开、手往前一伸（第 24 帧，出手 = 0.63s，召唤高潮那一拍）→ 收翼；播完停在最后一帧。
	/// </summary>
	public static readonly Spec ArseneSpec = new("arsene_cast", 7, 671f, 461f, new(350.8f, 412.3f), new(350.8f, 273.4f), 315f, 5, 40, 24);

	/// <summary>
	/// 伊邪那岐大神（毒性爆发，用户 2026-11-01）：转杖动作 BPS0360_000_main_0006（art/p5/fx/izanagi_okami/spin，51 帧 824x1024，× 0.45）
	/// 播第 7～40 帧原速：举杖 → 第 15～31 帧横着转一圈 → 第 33 帧竖回来（出手：转完发动），停在竖持。头顶到脚约 612px × 0.45。
	/// </summary>
	public static readonly Spec IzanagiSpec = new("izanagi_spin", 7, 371f, 461f, new(366.553f * 0.45f, 972.14f * 0.45f),
		new(366.553f * 0.45f, 697.025f * 0.45f), 275f, 7, 40, 33);

	/// <summary>亚森出手在他出现后几秒（兼容旧代码）。</summary>
	public const float AttackAt = (24 - 5) / Fps;

	/// <summary>亚森开始被蓝火吞掉离场的时刻（兼容旧代码）。</summary>
	public const float ExitAt = AttackAt + 0.35f;
	private const float Fps = 30f;

	private static Shader? _shader;

	private static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform float burn = 1.0;      // 1 = 全身都烧出来了；0 = 还没出现（从下往上）
uniform float glow = 0.0;      // 整体蓝光
uniform float flip = 0.0;
uniform float fade = 1.0;      // 1 = 退场：从下往上消失（往上升腾化成火）
uniform sampler2D atlas_tex : filter_linear;
uniform vec2 px = vec2(0.0002);
uniform vec4 region = vec4(0.0, 0.0, 1.0, 1.0);
uniform float flame = 0.0;     // 沿剪影长出来的蓝焰强弱
uniform float flen = 60.0;     // 火舌多长（图集像素）
uniform float clock = 0.0;
uniform float stage = 1.0;     // 融进舞台的调色强度（0 = 原样）
uniform vec4 c_dark : source_color = vec4(0.03, 0.06, 0.25, 1.0);
uniform vec4 c_mid : source_color = vec4(0.11, 0.35, 1.0, 1.0);
uniform vec4 c_hi : source_color = vec4(0.31, 0.78, 1.0, 1.0);
uniform vec4 c_hot : source_color = vec4(0.92, 0.99, 1.0, 1.0);   // 这一帧在图集里的 UV 范围
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; } return v; }
void fragment() {
	vec2 lv = (UV - region.xy) / max(region.zw, vec2(0.0001));      // 这一帧里的 0～1
	vec4 c = texture(atlas_tex, UV);                                 // 原版三渲二（漫画化试过不合适，用户 2026-11-01）
	// 轻轻融进红色舞台（用户 2026-11-01：三渲二还是有点违和，稍微掩饰、不要用力过度）：
	// 去一点饱和、加一点对比（压掉 3D 渲染的软渐变）；迎光一侧（右上）的边缘一道细红轮廓光。
	// （暗部偏深红 + 红网点试过：他大部分是黑的，整个人被染红了 —— 用户 2026-11-01，去掉）
	{
		float lum = dot(c.rgb, vec3(0.299, 0.587, 0.114));
		vec3 g = mix(c.rgb, vec3(lum), 0.15);
		g = clamp((g - 0.5) * 1.12 + 0.5, 0.0, 1.0);
		vec2 lo0 = region.xy, hi0 = region.xy + region.zw;
		float nb = texture(atlas_tex, clamp(UV + vec2(3.0 * px.x, -3.0 * px.y), lo0, hi0)).a;
		g = mix(g, vec3(1.0, 0.12, 0.16), c.a * (1.0 - nb) * 0.7 * stage);
		c.rgb = mix(c.rgb, g, stage);
	}
	float h = mix(1.0 - lv.y, lv.y, flip);                           // 出场 0 = 底；退场 0 = 顶
	float n = fbm(vec2(lv.x * 6.0, lv.y * 9.0)) * 0.35;
	float t = burn * 1.4 - 0.2;
	float d = t - (h + n);                                           // > 0：已经烧出来
	float edge = smoothstep(0.045, 0.0, abs(d)) * step(-0.045, d) * (1.0 - flip);   // 离场不画烧边（会在翅膀上糊成一大片）
	vec3 blue = vec3(0.25, 0.65, 1.0), hot = vec3(0.9, 0.98, 1.0);
	vec3 col = mix(c.rgb, blue, glow * 0.25 + smoothstep(0.06, 0.0, d) * 0.5 * (1.0 - flip));   // 只在烧的边附近染蓝
	col = mix(col, mix(blue, hot, smoothstep(0.04, 0.0, abs(d))), edge);
	float a = c.a * (step(0.0, d) + edge * 0.9);
	// 沿他这一帧的剪影往外、往上长出蓝色火舌（只长在身体外面，所以在他身后、不盖住他；用户 2026-11-01：火不要是贴在他上面的一块）
	vec4 fireCol = vec4(0.0);
	if ((c.a < 0.04 || a < 0.5) && flame > 0.0) {                 // 身体外面、以及身体已经被烧掉 / 还没烧出来的地方都长火（不然是一个人形的洞）
		vec2 lo = region.xy, hi2 = region.xy + region.zw;
		float fire = 0.0;
		for (int i = 1; i < 21; i++) {
			float k = float(i) / 20.0;
			float drift = (noise(vec2(lv.x * 18.0, lv.y * 14.0 + clock * 2.2)) - 0.5) * 18.0 * k;
			vec2 q = clamp(UV + vec2(drift * px.x, flen * k * px.y), lo, hi2);
			float s = smoothstep(0.02, 0.35, texture(atlas_tex, q).a);
			float colN = fbm(vec2((lv.x - drift * px.x / region.z) * 16.0, lv.y * 5.0 + clock * 2.6));
			float reach = smoothstep(0.2 + k * 0.5, 0.42 + k * 0.5, colN);
			fire = max(fire, s * reach * (1.0 - k * 0.5));
		}
		float e = pow(fbm(vec2(lv.x * 22.0, lv.y * 6.0 + clock * 3.2)), 1.4) * 1.7;
		vec3 fc = c_dark.rgb;
		fc = mix(fc, c_mid.rgb, smoothstep(0.12, 0.5, e));
		fc = mix(fc, c_hi.rgb, smoothstep(0.45, 0.9, e));
		fc = mix(fc, c_hot.rgb, smoothstep(0.9, 1.3, e));
		fireCol = vec4(fc, clamp(fire * flame * smoothstep(0.18, 0.6, e), 0.0, 1.0));   // 只要火丝的亮部：深色底会叠成一块人形暗影
	}
	float ba = clamp(a, 0.0, 1.0);
	float outA = max(ba, fireCol.a) * fade;
	if (outA < 0.004) { discard; }
	COLOR = vec4(mix(fireCol.rgb, col, ba), outA);
}" };

	/// <param name="back">背景特效层</param>
	/// <param name="feet">Joker 脚底（屏幕）</param>
	/// <param name="height">亚森身体（帽顶到脚）在屏幕上多高</param>
	public static void Arsene(Control back, Vector2 feet, float height, float facing = 1f) => Show(ArseneSpec, back, feet, height, facing);

	public static void Izanagi(Control back, Vector2 feet, float height, float facing = 1f) => Show(IzanagiSpec, back, feet, height, facing);

	/// <summary>人格面具的胸口在屏幕上哪（出手那一拍的水波纹从这里扩出去）。参数和 Show 一样。</summary>
	public static Vector2 ChestOf(Spec spec, Vector2 feet, float height, float facing = 1f)
	{
		float k = height / spec.BodyPx;
		return feet + new Vector2(-0.48f * height * facing, -0.3f * height) + new Vector2(0f, -(spec.Foot.Y - spec.Body.Y) * k);
	}

	public static void Show(Spec spec, Control back, Vector2 feet, float height, float facing = 1f)
	{
		if (Fx.Tex(spec.Tex) is not Texture2D atlas)
		{
			return;
		}
		float k = height / spec.BodyPx;
		Vector2 Foot = spec.Foot, Body = spec.Body;
		float AttackAt = spec.AttackAt, ExitAt = spec.ExitAt;
		// 悬在 Joker 身后上方、离他一段距离（用户 2026-11-01：离得太近）：往左后方让开、脚大约在 Joker 腰的高度（用户：太高、再往左）
		Vector2 anchor = feet + new Vector2(-0.48f * height * facing, -0.3f * height);   // 再往后（2026-11-01）
		const float exit = 0.35f;
		float life = ExitAt + exit;
		Node2D root = Fx.Root(back, anchor, "P5Persona", life + 0.1);
		Sprite2D sprite = new()
		{
			Texture = atlas, RegionEnabled = true, Centered = false, Material = new ShaderMaterial { Shader = Shader },
			Scale = new Vector2(k * facing, k), Position = new Vector2(-Foot.X * k * facing, -Foot.Y * k),
		};
		root.AddChild(sprite);
		ShaderMaterial m = (ShaderMaterial)sprite.Material;
		Vector2 size = atlas.GetSize();
		m.SetShaderParameter("burn", 0f);
		m.SetShaderParameter("atlas_tex", atlas);
		m.SetShaderParameter("c_dark", PersonaMask.Dark);
		m.SetShaderParameter("c_mid", PersonaMask.Mid);
		m.SetShaderParameter("c_hi", PersonaMask.Hi);
		m.SetShaderParameter("c_hot", PersonaMask.Hot);
		Fx.Clock(sprite);
		m.SetShaderParameter("px", new Vector2(1f / size.X, 1f / size.Y));
		// 声音：亚森出场 / 出手前各扇一下翅膀；伊邪那岐大神转杖时三声由低到高的挥动（出手那一拍的冲击声在 SummonStage.Beat）
		if (spec.Tex == "arsene_cast")
		{
			Sfx.PlayHit(Sfx.Wing, 0.55f, 0.06f);
			Sfx.PlayHit(Sfx.Wing, 0.55f, Mathf.Max(0f, AttackAt - 0.14f));
		}
		else
		{
			float s0 = (15 - spec.First) / 30f, s1 = (31 - spec.First) / 30f;
			Sfx.Play(Sfx.SwishLow, 0.45f, s0);
			Sfx.Play(Sfx.Swish, 0.55f, Mathf.Lerp(s0, s1, 0.45f));
			Sfx.Play(Sfx.SwishHeavy, 0.7f, Mathf.Lerp(s0, s1, 0.85f));
		}
		// 出场：身后蓝焰一炸
		Vector2 chest = anchor + new Vector2(0f, -(Foot.Y - Body.Y) * k);
		Glow.Flash(back, chest, PersonaMask.Hi, height * 1.2f, 0.4f);
		Glow.Sparks(back, chest, 50, 120f, 520f, 0.9f, new[] { PersonaMask.Hot, PersonaMask.Hi, PersonaMask.Mid }, -260f, 0.12f, 0.45f);
		// 出场修饰：一圈压扁的蓝色冲击环（火在着色器里沿他的剪影长出来）
		Glow.Shockwave(back, anchor, PersonaMask.Hi, height * 2.2f, 0.55f, 0.35f);
		// 出手：眼神特写劈开之后（第 24 帧，双翼展开）胸口炸开红白光、两圈冲击环、放射速度线（背景层不用折射：会变成暗圆盘）
		Fx.After(back, AttackAt, () =>
		{
			Glow.Flash(back, chest, Pal5.Hot, height * 0.3f, 0.12f);
			Glow.Flash(back, chest, Pal5.Bright, height * 0.7f, 0.3f);
			Glow.Shockwave(back, chest, Pal5.Hot, height * 1.8f, 0.45f);
			Glow.Shockwave(back, chest, Pal5.Bright, height * 2.6f, 0.6f, 1f, 0.06f);
			Glow.SpeedLines(back, chest, Pal5.Bright, 18, height * 0.25f, height * 1.3f, 0.4f);
			Glow.Sparks(back, chest, 40, 300f, 900f, 0.5f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 200f, 0.1f, 0.3f);
		});
		int last = -1;
		Fx.Animate(root, 0, life, t =>
		{
			float s = t * life;
			int f = spec.FrameAt(s);
			if (f != last)
			{
				last = f;
				Rect2 r = new(f % spec.Cols * spec.FrameW, f / spec.Cols * spec.FrameH, spec.FrameW, spec.FrameH);
				sprite.RegionRect = r;
				m.SetShaderParameter("region", new Vector4(r.Position.X / size.X, r.Position.Y / size.Y, r.Size.X / size.X, r.Size.Y / size.Y));
			}
			// 0～.18s 被蓝焰烧出来；最后 exit 秒被蓝焰从下往上吞掉
			float inK = Mathf.Clamp((s - 0.04f) / 0.18f, 0f, 1f), outK = Mathf.Clamp((s - (life - exit)) / exit, 0f, 1f);
			m.SetShaderParameter("burn", outK > 0f ? 1f - outK : Fx.OutCubic(inK));
			m.SetShaderParameter("flip", outK > 0f ? 1f : 0f);
			m.SetShaderParameter("fade", 1f - outK * 0.7f);
			m.SetShaderParameter("glow", (1f - inK) * 0.8f + outK * 0.8f);
			// 火：出场又长又旺（先烧出他的轮廓），显形后退成一圈淡淡的余焰；离场重新变长变旺把他吞掉
			float pre = 1f - Mathf.Clamp((s - 0.18f) / 0.25f, 0f, 1f);
			m.SetShaderParameter("flame", Mathf.Min(1f, s / 0.05f) * (0.35f + 0.65f * Mathf.Max(pre, outK)) * (1f - Mathf.Clamp((outK - 0.75f) / 0.25f, 0f, 1f)));
			m.SetShaderParameter("flen", 45f + 85f * Mathf.Max(pre, outK));
		});
		// 出场：一团蓝焰从脚下蹿起把他整个裹住，火往上退的同时把他烧出来；离场：蓝焰从脚下往上吞掉他，火舌窜走、散成火星
		Fx.After(back, life - exit * 0.5f, () =>
			Glow.Sparks(back, chest, 60, 120f, 420f, 1.1f, new[] { PersonaMask.Hot, PersonaMask.Hi, PersonaMask.Mid }, -380f, 0.1f, 0.42f, Vector2.Up, 50f));
	}
}
