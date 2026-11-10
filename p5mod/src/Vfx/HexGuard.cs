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
// 盾面：一块圆角竖板，绕竖轴转 yaw 斜对着敌人（透视：远的那侧小），中间往敌人那边凸。
// 屏幕点 p（矩形内 [-1,1]，x 已按朝向翻过、乘过宽高比）→ 板上坐标 (u, v)，|u| < hw、|v| < 1 在板上
uniform float yaw = 0.9;
uniform float persp = 0.22;
uniform float bulge = 0.14;
uniform float hw = 0.85;
vec2 toPanel(vec2 p) {
	float xs = p.x - bulge * (1.0 - p.y * p.y) + bulge * 0.4;
	float cs = cos(yaw), sn = sin(yaw);
	float u = xs / max(cs - xs * persp * sn, 0.05);
	float v = p.y * (1.0 + persp * u * sn);
	return vec2(u, v);
}
// 板的轮廓：超椭圆（圆角矩形），<1 在里面
float panel(vec2 uv) { vec2 q = abs(vec2(uv.x / hw, uv.y)); return pow(pow(q.x, 2.6) + pow(q.y, 2.6), 1.0 / 2.6); }   // 圆润的盾形，不是方框
// 展开的顺序：从靠 Joker 那侧（拳头甩过来的地方）往敌人那侧扫，0 = 起点，~1 = 最远
float order(vec2 uv) { return clamp(length((uv - vec2(-hw * 0.9, 0.05)) / vec2(hw * 1.9, 1.7)), 0.0, 1.0); }
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
	p.x *= aspect * facing;
	vec2 uv = toPanel(p);
	float d = panel(uv);
	if (d > 1.03) { COLOR = vec4(0.0); }
	else {
		vec4 hc = hexCell(uv * cells);
		vec2 lp = hc.xy;
		vec2 cid = hc.zw;
		float rnd = hash(cid);
		float rc = clamp(panel(cid / cells), 0.0, 1.0);                     // 这一格离板中心多远（0～1，决定边上掉没）
		float ro = order(cid / cells);
		// 入：从 Joker 那侧扫向敌人那侧；出：从最远的那侧往回退
		float ageOn = (on - ro * 0.85 - rnd * 0.06) / 0.14;
		float ageOff = (off - (1.0 - ro) * 0.85 - rnd * 0.1) / 0.16;
		float a = clamp(ageOn, 0.0, 1.0), z = clamp(ageOff, 0.0, 1.0);
		// 翻牌：入场每格从侧面翻进来（横向 0 → 冲过头 → 1，竖向略缩），出场翻过去（横向收到 0）
		float flipIn = outBack(a), flipOut = 1.0 - z * z;
		vec2 sc = vec2(max(flipIn * flipOut, 0.001), max(mix(0.7, 1.0, a) * mix(1.0, 0.85, z), 0.001));
		vec2 q = lp / sc;
		float hd = hexDist(q);
		float aa = fwidth(hd) * 1.2;
		float inside = 1.0 - smoothstep(0.5 - aa, 0.5, hd);
		float edge = (1.0 - smoothstep(0.022, 0.022 + aa * 1.5, abs(hd - 0.462))) * inside;
		float core = (1.0 - smoothstep(0.0, aa * 1.2, abs(hd - 0.462))) * inside;
		float bevel = clamp(0.5 + dot(q, vec2(-0.55, -0.85)) * 1.4, 0.0, 1.0);
		float spark = step(0.86, rnd);
		float fill = inside * (0.02 + 0.05 * smoothstep(0.45, 0.1, hd) + spark * 0.22 * smoothstep(0.5, 0.0, hd)) * (0.6 + 0.8 * bevel);
		// 边界不画框：靠边的格子随机掉没、越往外越淡，轮廓是一格格参差的，不是一条线
		float keep = 1.0 - smoothstep(0.45 + 0.35 * hash(cid + 7.3), 0.95, rc);
		float lit = step(0.0, ageOn) * (1.0 - z) * keep;
		float pop = exp(-max(ageOn, 0.0) * 2.2) * step(0.0, ageOn) + 1.4 * z * (1.0 - z) * step(0.0, ageOff);   // 翻进来、翻过去时各亮一下
		float sweep = scan >= 0.0 ? exp(-abs(uv.y - mix(-1.1, 1.1, scan)) * 9.0) : 0.0;
		// 受光：板的正面朝敌人；凸出来的中间一竖条最亮，往两边、往远处暗下去；靠板边更亮（掠射）
		float face = 0.55 + 0.45 * (1.0 - abs(uv.x / hw));
		float far = clamp(1.0 - 0.35 * uv.x / hw, 0.5, 1.3);
		float fres = smoothstep(0.6, 1.0, d);
		float bright = (0.5 + 0.5 * rnd) * (0.75 + 0.5 * bevel) * mix(0.35, 1.0, fres) * face * far;
		float ell = 1.0 - smoothstep(0.7, 1.02, d);
		vec3 red = vec3(1.0, 0.1, 0.16), hot = vec3(1.0, 0.92, 0.9);
		vec3 col = red * (edge * bright * (0.9 + 1.2 * fres) + fill) + hot * (core * (0.35 + pop + sweep) + edge * (pop * 0.8 + sweep * 0.6));
		float shown = smoothstep(order(uv) - 0.12, order(uv) + 0.02, on * 1.05) * (1.0 - smoothstep(1.0 - order(uv) - 0.12, 1.0 - order(uv) + 0.02, off * 1.05));
		// 朝敌人那一侧整体再亮一点（迎面），用柔和的辉光，不画线
		float front = smoothstep(0.1, 0.8, uv.x / hw) * shown;
		col *= 1.0 + 0.5 * front;
		col += red * exp(-abs(d - 0.8) * 6.0) * 0.12 * shown * (0.6 + 0.6 * front);
		col += hot * flash * 0.25 * (1.0 - smoothstep(0.0, 0.9, d));
		COLOR = vec4(col * lit * ell * glow + red * exp(-abs(d - 0.8) * 6.0) * 0.1 * shown * glow * ell, 1.0);
	}
}" };

	private static Shader Refract => _refract ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float strength = 0.012;
" + Common + @"
uniform float facing = 1.0;
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	vec2 pp = vec2(p.x * aspect * facing, p.y);
	vec2 uv = toPanel(pp);
	float d = panel(uv);
	float shown = smoothstep(order(uv) - 0.12, order(uv) + 0.02, on * 1.05) * (1.0 - smoothstep(1.0 - order(uv) - 0.12, 1.0 - order(uv) + 0.02, off * 1.05));
	float ell = 1.0 - smoothstep(0.55, 1.0, d);
	vec2 off2 = -vec2(uv.x / hw * facing, uv.y) * d * strength * shown;
	vec3 bg = vec3(textureLod(screen_tex, SCREEN_UV + off2 * 1.2, 0.0).r, textureLod(screen_tex, SCREEN_UV + off2, 0.0).g,
		textureLod(screen_tex, SCREEN_UV + off2 * 0.8, 0.0).b);
	bg = mix(bg, bg * vec3(1.15, 0.85, 0.88) + vec3(0.03, 0.0, 0.005), 0.25);
	COLOR = vec4(bg, ell * shown * 0.8);
}" };

	/// <param name="chest">Joker 胸口（全局坐标）：盾立在它前面</param>
	/// <param name="height">Joker 身高（屏幕像素）</param>
	/// <param name="amount">格挡值：10 点以上盾更大、停得更久</param>
	public static void Play(Control layer, Vector2 chest, float height, float facing = 1f, int amount = 5, Vector2? fist = null)
	{
		bool big = amount >= 10;
		Sfx.Play(Sfx.HexGuard, big ? 0.6f : 0.5f);                   // 用户 2026-11-10：音效也加上（原版格挡音照常响，这一声压得低）
		float k = big ? 1.18f : 1f;
		Vector2 size = new Vector2(height * 0.8f, height * 0.94f) * k;
		Vector2 center = chest + new Vector2(height * 0.42f * facing, height * 0.08f);    // 立在身前，不盖住人
		float unit = Fx.Screen(layer).Y / 1080f;
		float appear = 0.28f, hold = big ? 0.45f : 0.32f, vanish = 0.28f, life = appear + hold + vanish;

		Node2D root = Fx.Root(layer, center, "P5HexGuard", life + 0.2);
		ShaderMaterial rm = new() { Shader = Refract }, fm = new() { Shader = Face };
		foreach (ShaderMaterial mat in new[] { rm, fm })
		{
			mat.SetShaderParameter("aspect", size.X / size.Y);
			mat.SetShaderParameter("cells", 5f);
			mat.SetShaderParameter("on", 0f);
			mat.SetShaderParameter("off", 0f);
		}
		fm.SetShaderParameter("facing", facing);
		rm.SetShaderParameter("facing", facing);
		ColorRect refract = new() { Size = size, Position = -size / 2f, Material = rm, MouseFilter = Control.MouseFilterEnum.Ignore };
		ColorRect face = new() { Size = size, Position = -size / 2f, Material = fm, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(refract);
		root.AddChild(face);

		// 入场：拳头甩出一颗火星（0.06s）射到盾靠 Joker 那侧 → 护盾从这里展开（闪光、扭曲波纹也在这里）
		Vector2 seed = center + new Vector2(-size.X * 0.3f * facing, size.Y * 0.02f);
		Vector2 from = fist ?? seed;
		const float shoot = 0.06f;
		Node2D bolt = Fx.Root(layer, from, "P5HexGuardSeed", shoot + 0.3);
		Sprite2D boltGlow = Glow.SoftSprite(bolt, Vector2.Zero, new Color(1f, 0.2f, 0.28f), 1.4f);
		Polygon2D boltCore = Fx.Poly(bolt, Glow.Star4(14f * unit, 0.22f), new Color(1f, 0.95f, 0.92f), additive: true);
		Fx.Animate(bolt, 0, shoot, t =>
		{
			bolt.GlobalPosition = from.Lerp(seed, Fx.OutCubic(t));
			bolt.Rotation = t * 3f;
			bolt.Visible = t < 1f;
		});
		Glow.Sparks(layer, from, 6, 120f, 360f, 0.25f, new[] { new Color(1f, 0.95f, 0.92f), new Color(1f, 0.25f, 0.3f) }, 0f, 0.03f * unit, 0.08f * unit,
			seed - from, 25f);
		Fx.After(layer, shoot, () =>
		{
			Glow.Flash(layer, seed, new Color(1f, 0.3f, 0.35f), size.Y * 0.6f, 0.22f);
			Glow.Ripple(layer, seed, size.Y * 1.3f, 0.55f, 0.022f);
		});
		int embers = 0;
		Fx.Animate(root, 0, life + shoot, t =>
		{
			float s = t * (life + shoot) - shoot;
			float on = Mathf.Clamp(s / appear, 0f, 1f) * 1.25f;
			float off = Mathf.Clamp((s - appear - hold) / vanish, 0f, 1f) * 1.25f;
			foreach (ShaderMaterial mat in new[] { rm, fm })
			{
				mat.SetShaderParameter("on", on);
				mat.SetShaderParameter("off", off);
			}
			fm.SetShaderParameter("flash", s >= 0f ? Mathf.Exp(-s / 0.08f) : 0f);
			fm.SetShaderParameter("scan", s > appear * 0.6f && s < appear + hold ? (s - appear * 0.6f) / (appear * 0.4f + hold) : -1f);
			// 出场：从最远那侧往回退，退到哪里哪里冒一把往上飘的火星
			float o = Mathf.Clamp((s - appear - hold) / vanish, 0f, 1f);
			while (embers < 4 && o >= 0.15f + 0.2f * embers)
			{
				float x = Mathf.Lerp(0.38f, -0.28f, embers / 3f);
				Vector2 at = center + new Vector2(x * size.X * facing, Fx.Rand(-0.3f, 0.3f) * size.Y);
				Glow.Sparks(layer, at, 7, 40f, 150f, 0.5f, new[] { new Color(1f, 0.92f, 0.9f), new Color(1f, 0.2f, 0.28f) }, -120f,
					0.035f * unit, 0.09f * unit, Vector2.Up, 50f);
				embers++;
			}
		});
	}
}
