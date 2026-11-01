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
	private const int Cols = 7, Frames = 49;
	private const float FrameW = 671f, FrameH = 461f, Fps = 30f;
	// 图集一帧里的脚 / 身体中心（sources.json 的 cast 锚点 × 0.45）
	private static readonly Vector2 Foot = new(350.8f, 412.3f), Body = new(350.8f, 273.4f);

	private static Shader? _shader;

	private static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform float burn = 1.0;      // 1 = 全身都烧出来了；0 = 还没出现（从下往上）
uniform float glow = 0.0;      // 整体蓝光
uniform float flip = 0.0;
uniform float fade = 1.0;      // 1 = 退场：从下往上消失（往上升腾化成火）
uniform vec4 region = vec4(0.0, 0.0, 1.0, 1.0);   // 这一帧在图集里的 UV 范围
varying vec2 local;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; } return v; }
void fragment() {
	vec2 lv = (UV - region.xy) / max(region.zw, vec2(0.0001));      // 这一帧里的 0～1
	vec4 c = texture(TEXTURE, UV);
	float h = mix(1.0 - lv.y, lv.y, flip);                           // 出场 0 = 底；退场 0 = 顶
	float n = fbm(vec2(lv.x * 6.0, lv.y * 9.0)) * 0.35;
	float t = burn * 1.4 - 0.2;
	float d = t - (h + n);                                           // > 0：已经烧出来
	float edge = smoothstep(0.045, 0.0, abs(d)) * step(-0.045, d);
	vec3 blue = vec3(0.25, 0.65, 1.0), hot = vec3(0.9, 0.98, 1.0);
	vec3 col = mix(c.rgb, blue, glow * 0.25 + smoothstep(0.06, 0.0, d) * 0.5);   // 只在烧的边附近染蓝
	col = mix(col, mix(blue, hot, smoothstep(0.04, 0.0, abs(d))), edge);
	float a = c.a * (step(0.0, d) + edge * 0.9);
	COLOR = vec4(col, clamp(a, 0.0, 1.0) * fade);
}" };

	/// <param name="back">背景特效层</param>
	/// <param name="feet">Joker 脚底（屏幕）</param>
	/// <param name="height">亚森身体（帽顶到脚）在屏幕上多高</param>
	public static void Arsene(Control back, Vector2 feet, float height, float facing = 1f)
	{
		if (Fx.Tex("arsene_cast") is not Texture2D atlas)
		{
			return;
		}
		const float body = 315f;                                          // 一帧里帽顶到最低处约 315px
		float k = height / body;
		// 悬浮在 Joker 身后、略往后、脚离地
		Vector2 anchor = feet + new Vector2(-0.06f * height * facing, -0.06f * height);
		float life = Frames / Fps + 0.35f;
		Node2D root = Fx.Root(back, anchor, "P5Arsene", life + 0.1);
		Sprite2D sprite = new()
		{
			Texture = atlas, RegionEnabled = true, Centered = false, Material = new ShaderMaterial { Shader = Shader },
			Scale = new Vector2(k * facing, k), Position = new Vector2(-Foot.X * k * facing, -Foot.Y * k),
		};
		root.AddChild(sprite);
		ShaderMaterial m = (ShaderMaterial)sprite.Material;
		Vector2 size = atlas.GetSize();
		m.SetShaderParameter("burn", 0f);
		// 出场：身后蓝焰一炸
		Vector2 chest = anchor + new Vector2(0f, -(Foot.Y - Body.Y) * k);
		Glow.Flash(back, chest, PersonaMask.Hi, height * 1.2f, 0.4f);
		Glow.Sparks(back, chest, 50, 120f, 520f, 0.9f, new[] { PersonaMask.Hot, PersonaMask.Hi, PersonaMask.Mid }, -260f, 0.12f, 0.45f);
		int last = -1;
		Fx.Animate(root, 0, life, t =>
		{
			float s = t * life;
			int f = Mathf.Min(Frames - 1, (int)(s * Fps));
			if (f != last)
			{
				last = f;
				Rect2 r = new(f % Cols * FrameW, f / Cols * FrameH, FrameW, FrameH);
				sprite.RegionRect = r;
				m.SetShaderParameter("region", new Vector4(r.Position.X / size.X, r.Position.Y / size.Y, r.Size.X / size.X, r.Size.Y / size.Y));
			}
			// 0～.28s 烧出来；最后 .35s 从下往上化掉
			float inK = Mathf.Clamp(s / 0.28f, 0f, 1f), outK = Mathf.Clamp((s - (life - 0.35f)) / 0.35f, 0f, 1f);
			m.SetShaderParameter("burn", outK > 0f ? 1f - outK : Fx.OutCubic(inK));
			m.SetShaderParameter("flip", outK > 0f ? 1f : 0f);
			m.SetShaderParameter("fade", 1f - outK * 0.7f);
			m.SetShaderParameter("glow", (1f - inK) * 0.8f + outK * 0.8f);
		});
		Fx.After(back, life - 0.35f, () =>
			Glow.Sparks(back, chest, 40, 80f, 300f, 1.0f, new[] { PersonaMask.Hot, PersonaMask.Hi, PersonaMask.Mid }, -320f, 0.1f, 0.4f, Vector2.Up, 60f));
	}
}
