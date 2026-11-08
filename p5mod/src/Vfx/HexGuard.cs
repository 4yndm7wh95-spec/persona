using Godot;

namespace P5Joker;

/// <summary>
/// 防御（用户 2026-11-08：照结城理 mod 的 Tetrakarn 六边形蜂巢护盾，P3Makoto Extras.HexShield）。
/// 早期那版六边形是一个个多边形，用户：太粗糙、细节很拉、廉价 → 这版整面盾是着色器逐像素算的：
/// · 曲面：六边形网格按球面投影铺，越靠边格子越扁、越密；边缘一圈菲涅尔亮边；
/// · 每个格子：细而锐的亮边 + 斜面高光（左上亮右下暗）+ 很淡的内填充；亮度各有一点随机；
/// · 点亮：从中心往外一圈圈弹出来（每格冲过头再回、亮起那一下白闪）；停住时一道扫描光从上扫到下；
/// · 熄灭：从外往里，每格缩小、变暗消失；
/// · 两层：底下一层折射盾后面的画面（微微泛红，像隔着一层能量膜），上面一层加法的亮边和辉光。
/// 颜色是 P5 的红 / 白。整段约 0.85s（防御打得多：不顿帧、不震屏，声音用原版格挡音）。
/// </summary>
public static class HexGuard
{
	/// <summary>共用：盾面坐标、六边形网格、每格的点亮 / 熄灭进度。</summary>
	private const string Common = @"
uniform float aspect = 0.55;
uniform float on = 0.0;
uniform float off = 0.0;
uniform float cells = 6.5;
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
// 六边形网格：返回 (格子里的局部坐标, 格子编号)
vec4 hexCell(vec2 uv) {
	const vec2 s = vec2(1.0, 1.7320508);
	vec4 c = floor(vec4(uv, uv - vec2(0.5, 1.0)) / s.xyxy) + 0.5;
	vec4 h = vec4(uv - c.xy * s, uv - (c.zw + 0.5) * s);
	return dot(h.xy, h.xy) < dot(h.zw, h.zw) ? vec4(h.xy, c.xy * s) : vec4(h.zw, (c.zw + 0.5) * s);
}
float hexDist(vec2 p) { p = abs(p); return max(dot(p, vec2(0.5, 0.8660254)), p.x); }
// 盾面上的点（椭圆内 d < 1）→ 网格坐标：球面投影，越靠边越压缩
vec2 toGrid(vec2 p, float d) { return p / sqrt(max(1.0 - 0.6 * d * d, 0.08)) * cells; }
float outBack(float x) { float c = 1.9; x -= 1.0; return 1.0 + (c + 1.0) * x * x * x + c * x * x; }
";

	private static Shader? _face, _refract;

	private static Shader Face => _face ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float scan = -1.0;
uniform float flash = 0.0;
uniform float glow = 1.0;
uniform float facing = 1.0;
" + Common + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	p.x /= 1.0;                                     // UV 已经是椭圆的外接矩形
	float d = length(p);
	if (d > 1.02) { COLOR = vec4(0.0); }
	else {
		vec2 g = toGrid(vec2(p.x * aspect, p.y), d);
		vec4 hc = hexCell(g);
		vec2 lp = hc.xy;
		vec2 cid = hc.zw;
		float rnd = hash(cid);
		float rc = clamp(length(cid) / (cells * 1.25), 0.0, 1.0);          // 这一格离中心多远（0～1）
		float ageOn = (on - rc * 0.85 - rnd * 0.08) / 0.16;
		float ageOff = (off - (1.0 - rc) * 0.85 - rnd * 0.08) / 0.16;
		float a = clamp(ageOn, 0.0, 1.0), z = clamp(ageOff, 0.0, 1.0);
		float sc = max(outBack(a) * (1.0 - 0.7 * z), 0.001);
		vec2 q = lp / sc;
		float hd = hexDist(q);
		float aa = fwidth(hd) * 1.2;
		float inside = 1.0 - smoothstep(0.5 - aa, 0.5, hd);
		float edge = (1.0 - smoothstep(0.022, 0.022 + aa * 1.5, abs(hd - 0.462))) * inside;
		float core = (1.0 - smoothstep(0.0, aa * 1.2, abs(hd - 0.462))) * inside;     // 亮边正中一条更亮的芯
		float bevel = clamp(0.5 + dot(q, vec2(-0.55, -0.85)) * 1.4, 0.0, 1.0);        // 斜面：左上亮
		float spark = step(0.86, rnd);                                                      // 少数几格整格亮着（不全是一样的网）
		float fill = inside * (0.02 + 0.05 * smoothstep(0.45, 0.1, hd) + spark * 0.22 * smoothstep(0.5, 0.0, hd)) * (0.6 + 0.8 * bevel);
		float lit = step(0.0, ageOn) * (1.0 - z);
		float pop = exp(-max(ageOn, 0.0) * 2.2) * step(0.0, ageOn);                  // 刚亮起那一下白闪
		float sweep = scan >= 0.0 ? exp(-abs(p.y - mix(-1.1, 1.1, scan)) * 9.0) : 0.0;
		// 中间通透、越往边越亮（曲面的掠射角）；朝敌人那侧受光更亮
		float side = clamp(0.75 + 0.45 * p.x * facing, 0.3, 1.3);
		float bright = (0.5 + 0.5 * rnd) * (0.75 + 0.5 * bevel) * mix(0.3, 1.0, smoothstep(0.15, 0.95, d)) * side;
		// 菲涅尔：越靠盾的边越亮
		float fres = smoothstep(0.55, 1.0, d);
		float ell = 1.0 - smoothstep(0.96, 1.0, d);
		vec3 red = vec3(1.0, 0.1, 0.16), hot = vec3(1.0, 0.92, 0.9);
		vec3 col = red * (edge * bright * (0.9 + 1.4 * fres) + fill) + hot * (core * (0.35 + pop + sweep) + edge * (pop * 0.8 + sweep * 0.6));
		// 盾的轮廓：一圈细亮边（跟着整体点亮 / 熄灭）
		float shown = smoothstep(d - 0.15, d, on * 1.1) * (1.0 - smoothstep(1.0 - d - 0.15, 1.0 - d, off * 1.1 - 0.05));
		float rim = (1.0 - smoothstep(0.0, 0.02, abs(d - 0.985))) * shown;
		col += mix(red, hot, 0.5) * rim * 1.4 + red * fres * fres * 0.25 * shown;
		col += hot * flash * 0.25 * ell * (1.0 - d);
		COLOR = vec4(col * lit * ell * glow + mix(red, hot, 0.5) * rim * 1.2 * glow, 1.0);
	}
}" };

	private static Shader Refract => _refract ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float strength = 0.012;
" + Common + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float d = length(p);
	float shown = smoothstep(d - 0.15, d, on * 1.1) * (1.0 - smoothstep(1.0 - d - 0.15, 1.0 - d, off * 1.1 - 0.05));
	float ell = 1.0 - smoothstep(0.95, 1.0, d);
	// 像透过一块凸起的膜：中间放大、边上往外推，红蓝轻微错开
	vec2 off2 = -p * d * strength * shown;
	vec3 bg = vec3(textureLod(screen_tex, SCREEN_UV + off2 * 1.2, 0.0).r, textureLod(screen_tex, SCREEN_UV + off2, 0.0).g,
		textureLod(screen_tex, SCREEN_UV + off2 * 0.8, 0.0).b);
	bg = mix(bg, bg * vec3(1.15, 0.85, 0.88) + vec3(0.03, 0.0, 0.005), 0.25);
	COLOR = vec4(bg, ell * shown * 0.85);
}" };

	/// <param name="chest">Joker 胸口（全局坐标）：盾立在它前面</param>
	/// <param name="height">Joker 身高（屏幕像素）</param>
	/// <param name="amount">格挡值：10 点以上盾更大、停得更久</param>
	public static void Play(Control layer, Vector2 chest, float height, float facing = 1f, int amount = 5)
	{
		bool big = amount >= 10;
		float k = big ? 1.18f : 1f;
		Vector2 size = new Vector2(height * 0.5f, height * 0.94f) * k;
		Vector2 center = chest + new Vector2(height * 0.42f * facing, height * 0.08f);    // 立在身前，不盖住人
		float unit = Fx.Screen(layer).Y / 1080f;
		float appear = 0.28f, hold = big ? 0.45f : 0.32f, vanish = 0.28f, life = appear + hold + vanish;

		Node2D root = Fx.Root(layer, center, "P5HexGuard", life + 0.1);
		// 侧面看过去的一面弧形盾：沿 x 压窄一点，朝敌人那边微微转开
		root.Scale = new Vector2(0.86f, 1f);
		root.Skew = 0.06f * facing;
		ShaderMaterial rm = new() { Shader = Refract }, fm = new() { Shader = Face };
		foreach (ShaderMaterial mat in new[] { rm, fm })
		{
			mat.SetShaderParameter("aspect", size.X / size.Y);
			mat.SetShaderParameter("cells", 5f);
			mat.SetShaderParameter("on", 0f);
			mat.SetShaderParameter("off", 0f);
		}
		fm.SetShaderParameter("facing", facing);
		ColorRect refract = new() { Size = size, Position = -size / 2f, Material = rm, MouseFilter = Control.MouseFilterEnum.Ignore };
		ColorRect face = new() { Size = size, Position = -size / 2f, Material = fm, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(refract);
		root.AddChild(face);

		Glow.Flash(layer, center, new Color(1f, 0.25f, 0.3f), size.Y * 0.8f, 0.25f);
		Glow.Ripple(layer, center, size.Y * 1.3f, 0.55f, 0.022f);
		bool dropped = false;
		Fx.Animate(root, 0, life, t =>
		{
			float s = t * life;
			float on = Mathf.Clamp(s / appear, 0f, 1f) * 1.25f;
			float off = Mathf.Clamp((s - appear - hold) / vanish, 0f, 1f) * 1.25f;
			foreach (ShaderMaterial mat in new[] { rm, fm })
			{
				mat.SetShaderParameter("on", on);
				mat.SetShaderParameter("off", off);
			}
			fm.SetShaderParameter("flash", Mathf.Exp(-s / 0.08f));
			fm.SetShaderParameter("scan", s > appear * 0.6f && s < appear + hold ? (s - appear * 0.6f) / (appear * 0.4f + hold) : -1f);
			if (!dropped && s >= appear + hold + vanish * 0.3f)
			{
				// 熄灭时边上掉下几颗火星
				dropped = true;
				for (int i = 0; i < 2; i++)
				{
					Vector2 side = center + new Vector2((i == 0 ? -0.35f : 0.35f) * size.X, Fx.Rand(-0.2f, 0.3f) * size.Y);
					Glow.Sparks(layer, side, 6, 40f, 160f, 0.45f, new[] { new Color(1f, 0.92f, 0.9f), new Color(1f, 0.2f, 0.28f) }, 260f,
						0.04f * unit, 0.1f * unit);
				}
			}
		});
	}
}
