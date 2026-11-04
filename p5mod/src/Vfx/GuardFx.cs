using Godot;

namespace P5Joker;

/// <summary>
/// 防御（用户 2026-11-04：重做；不要代码拼形状，用渲染；弧形护盾太像盾、太光滑 → 选了「碎玻璃格挡」）。
/// 一个着色器算出一整面碎玻璃墙：墙面按 Voronoi 切成 18 片不规则的碎片，每片有自己的朝向（法线）——
/// 折射的偏移、明暗、高光各不相同；碎片边缘一道白亮边、后面衬一条暗红的缝；转动时偶尔有一片闪一下高光。
/// 拼起来：碎片从四周错开时间飞进来、边飞边转，拼成一面错落的墙；停一下；炸开：往外飞、往下掉、淡掉。
/// 和眼神特写碎开是同一套「玻璃 / 碎镜」的语言。音效用原版尖塔的（block_gain）。
/// </summary>
public static class GuardFx
{
	private static Shader? _shader;

	private static Shader GlassShader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float clock = 0.0;
uniform float assemble = 0.0;   // 0 → 1：碎片飞进来拼成墙
uniform float burst = 0.0;      // 0 → 1：炸开
uniform float facing = 1.0;
uniform float aspect = 0.75;
uniform float seed = 0.0;
const int N = 18;
float h1(float n) { return fract(sin(n * 127.1 + seed * 13.7) * 43758.5453); }
vec2 seedOf(int i) { float f = float(i); return vec2((h1(f * 3.1 + 1.0) - 0.5) * 0.95, (h1(f * 5.7 + 2.0) - 0.5) * 1.75); }
int cellOf(vec2 s, out float gap) {
	float d1 = 1e9, d2 = 1e9; int best = 0;
	for (int c = 0; c < N; c++) { float d = length(s - seedOf(c)); if (d < d1) { d2 = d1; d1 = d; best = c; } else if (d < d2) { d2 = d; } }
	gap = (d2 - d1) * 0.5;
	return best;
}
// 墙的外形：竖长、略往敌人那边鼓，边缘按角度起伏（不规整的碎边）
bool inWall(vec2 s) {
	float a = atan(s.y, s.x);
	float wob = 1.0 + 0.07 * sin(a * 7.0 + seed) + 0.05 * sin(a * 13.0 - seed * 2.0);
	return length(s / vec2(0.5, 0.92)) < wob;
}
void fragment() {
	vec2 q = (UV - 0.5) * vec2(aspect, 1.0) * 2.0;
	q.x *= facing;
	int hit = -1; float edge = 1.0; float fade = 1.0; float rotHit = 0.0;
	for (int i = 0; i < N; i++) {
		float fi = float(i);
		vec2 piv = seedOf(i);
		float ai = clamp((assemble - 0.35 * h1(fi * 9.1)) / 0.65, 0.0, 1.0);
		if (ai <= 0.0) continue;
		float e = 1.0 - pow(1.0 - ai, 3.0);
		vec2 inDir = normalize(piv + (vec2(h1(fi * 2.3), h1(fi * 4.9)) - 0.5) * 0.8 + vec2(0.001));
		vec2 disp = inDir * 1.5 * (1.0 - e);
		float rot = (h1(fi * 6.7) - 0.5) * 5.0 * (1.0 - e);
		// 炸开：往外飞、往下掉、打转
		float bt = burst;
		vec2 outDir = normalize(piv + (vec2(h1(fi * 8.3), h1(fi * 1.7)) - 0.5) * 0.7 + vec2(0.3 * facing, 0.0));
		disp += outDir * (0.9 + 0.8 * h1(fi * 3.3)) * bt + vec2(0.0, 1.4 * bt * bt);
		rot += (h1(fi * 7.7) - 0.5) * 7.0 * bt;
		vec2 l = q - piv - disp;
		float cs = cos(-rot), sn = sin(-rot);
		vec2 s = piv + vec2(cs * l.x - sn * l.y, sn * l.x + cs * l.y);
		float gap;
		if (cellOf(s, gap) == i && inWall(s)) { hit = i; edge = gap; fade = 1.0 - bt * bt; rotHit = rot; }
	}
	if (hit < 0) { COLOR = vec4(0.0); }
	else {
		float fh = float(hit);
		// 每片的朝向：各自歪一点，飞的时候跟着转（高光会扫过）
		vec2 tilt = (vec2(h1(fh * 2.0), h1(fh * 3.0)) - 0.5) * 0.9;
		float cr = cos(rotHit * 0.7), sr = sin(rotHit * 0.7);
		tilt = vec2(cr * tilt.x - sr * tilt.y, sr * tilt.x + cr * tilt.y) + 0.12 * vec2(sin(clock * 2.3 + fh), cos(clock * 1.9 + fh * 1.3));
		vec3 n = normalize(vec3(tilt, 1.0));
		vec2 off = n.xy * vec2(facing, 1.0) * 0.06;
		vec3 bg = vec3(textureLod(screen_tex, SCREEN_UV - off * 1.1, 0.0).r, textureLod(screen_tex, SCREEN_UV - off, 0.0).g,
			textureLod(screen_tex, SCREEN_UV - off * 0.9, 0.0).b);
		// 通透的玻璃：背景只染一点红，按朝向明暗起伏，斜着的那几片泛出红色反光
		float slope = length(tilt);
		vec3 col = bg * mix(vec3(1.0, 0.8, 0.82), vec3(1.0), 0.55) * (1.0 + 0.25 * n.x * facing + 0.1 * h1(fh * 11.0));
		col += vec3(0.55, 0.06, 0.1) * smoothstep(0.25, 0.6, slope) * 0.35 + vec3(0.08, 0.02, 0.03);
		float spec = pow(max(dot(n, normalize(vec3(-0.35 * facing, -0.5, 0.79))), 0.0), 120.0);
		col += vec3(1.0, 0.94, 0.92) * spec * 0.7;                          // 只在转到那个角度时闪一下，不冲成整片白
		col = mix(col, vec3(0.75, 0.05, 0.1), 1.0 - smoothstep(0.006, 0.013, edge));      // 红色的缝
		col = mix(col, vec3(1.0, 0.96, 0.95), 1.0 - smoothstep(0.0, 0.0045, edge));      // 白亮边
		COLOR = vec4(col, fade);
	}
}" };

	/// <summary>放一面碎玻璃墙。fist = 抬起的前臂，shield = 墙的中心，height = 墙高，facing = Joker 朝向。约 0.9s。</summary>
	public static void Shield(Control layer, Vector2 fist, Vector2 shield, float height, float facing = 1f)
	{
		Vector2 size = new(height * 0.82f, height * 1.15f);
		ShaderMaterial m = new() { Shader = GlassShader };
		m.SetShaderParameter("facing", facing);
		m.SetShaderParameter("aspect", size.X / size.Y);
		m.SetShaderParameter("seed", Fx.Rand(0f, 50f));
		BackBufferCopy copy = new() { CopyMode = BackBufferCopy.CopyModeEnum.Viewport, Name = "P5GuardCopy" };
		layer.AddChild(copy);
		ColorRect r = new() { Material = m, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore, Name = "P5Guard" };
		layer.AddChild(r);
		r.GlobalPosition = shield - size * 0.5f;
		Fx.Clock(r);
		const float tIn = 0.26f, hold = 0.34f, tOut = 0.36f;
		float life = tIn + hold + tOut;
		Fx.Animate(r, 0, life, t =>
		{
			float s = t * life;
			m.SetShaderParameter("assemble", Mathf.Min(1f, s / tIn));
			m.SetShaderParameter("burst", Mathf.Clamp((s - tIn - hold) / tOut, 0f, 1f));
		});
		Fx.After(layer, tIn, () => Glow.Flash(layer, shield, new Color(1f, 0.55f, 0.58f), height * 0.6f, 0.18f));
		Fx.FreeAfter(r, life + 0.05);
		Fx.FreeAfter(copy, life + 0.05);
	}
}
