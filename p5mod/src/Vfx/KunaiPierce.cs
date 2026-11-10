using Godot;

namespace P5Joker;

/// <summary>
/// 苦无戳碎眼神特写（用户 2026-11-10：眼神背景不一定都用大手捏碎——再做一种：一把苦无从屏幕深处飞出来，穿破眼神横幅；
/// 尖端突破横幅的那一下要慢放，要看得见苦无在旋转；苦无要像大手一样用着色器渲染，不用贴图）。
/// 苦无是着色器里光线步进的一把 3D 苦无：菱形截面的叶形刃、缠着布的柄、柄尾一个环，透视镜头——朝镜头飞过来，近大远小都是真的；
/// 绕自己的长轴旋转，刃的两个斜面轮流接到光，所以转动看得见。材质：近黑的钢，刃口一线白热，轮廓上暗红的光，贴着刃的地方拖一点咒烟。
/// 横幅在 band_z 这个深度：比它远的部分不画（还在横幅后面），穿过横幅的那一圈是烧红的裂口和往外的裂纹。
/// 时间轴（从 emergeAt 算）：冲过来 Approach → 尖端碰到横幅：慢放 Slow（横幅被顶得鼓起来，裂纹越裂越多，苦无慢慢转着挤出来）
/// → SplitAt 横幅碎开（CutIn 自己做），苦无恢复速度冲出镜头。
/// </summary>
public sealed class KunaiPierce
{
	private static Shader? _shader;

	private static Shader KunaiShader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float clock = 0.0;
uniform float aspect = 1.7778;
uniform float focal = 1.25;
uniform vec3 pos = vec3(0.0, 0.0, 6.0);          // 苦无重心（刃根）在世界里的位置；镜头在原点朝 +z 看，y 朝上
uniform vec3 aim = vec3(-0.3, -0.2, -1.0);       // 刃尖指的方向（朝镜头，略偏）
uniform float spin = 0.0;                        // 绕长轴转了多少
uniform float size = 1.0;
uniform float band_z = 4.0;                      // 眼神横幅的深度
uniform float alpha = 1.0;
uniform float heat = 0.0;                        // 穿破时刃口和裂口有多烫
uniform float crack = 0.0;                       // 横幅上的裂纹裂开多少

float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
// 世界 → 苦无自己的坐标：y 沿长轴（尖在 +y），绕 y 转 spin
vec3 to_local(vec3 pw) {
	vec3 d = normalize(aim);
	vec3 x = normalize(cross(vec3(0.0, 1.0, 0.0), d));
	vec3 z = cross(d, x);
	vec3 q = (pw - pos) / size;
	vec3 l = vec3(dot(q, x), dot(q, d), dot(q, z));
	float c = cos(spin), s = sin(spin);
	return vec3(c * l.x - s * l.z, l.y, s * l.x + c * l.z);
}
// 刃：菱形截面，y 0（刃根）→ 1.25（尖）；最宽在 0.38
float blade(vec3 p) {
	float y = clamp(p.y, 0.0, 1.25);
	float w = y < 0.38 ? mix(0.075, 0.23, y / 0.38) : mix(0.23, 0.0, (y - 0.38) / 0.87);
	float t = w * 0.3 + 0.005;                                     // 薄的叶形刃，中间一道脊
	float d = (abs(p.x) * t + abs(p.z) * w - w * t) / sqrt(w * w + t * t + 1e-5);
	float cap = max(-p.y, p.y - 1.25);
	return max(max(d, cap), abs(p.z) - w - 0.002);                 // 宽度方向也要封住（不然尖附近 w→0，整个刃面所在的平面都成了「表面」）
}
float sd_cyl(vec3 p, float h0, float h1, float r) {
	vec2 d = vec2(length(p.xz) - r, max(h0 - p.y, p.y - h1));
	return min(max(d.x, d.y), 0.0) + length(max(d, 0.0));
}
float sd_torus(vec3 p, float R, float r) { return length(vec2(length(p.xy) - R, p.z)) - r; }
// 返回 (距离, 材质：0 刃 1 柄 2 环)
vec2 kunai(vec3 p) {
	float b = blade(p);
	float wrap = 0.006 * sin(p.y * 70.0 + atan(p.z, p.x) * 2.0);          // 缠柄的布：一圈圈斜着的棱
	float h = sd_cyl(p, -0.62, 0.02, 0.055 + wrap);
	float guard = sd_cyl(p, -0.02, 0.03, 0.085);
	float r = sd_torus(p - vec3(0.0, -0.76, 0.0), 0.12, 0.03);
	vec2 res = vec2(b, 0.0);
	if (min(h, guard) < res.x) res = vec2(min(h, guard), h < guard ? 1.0 : 2.0);
	if (r < res.x) res = vec2(r, 2.0);
	return res;
}
float map(vec3 pw) { return kunai(to_local(pw)).x * size; }
vec3 normal_at(vec3 p, float e) {
	vec2 k = vec2(1.0, -1.0);
	return normalize(k.xyy * map(p + k.xyy * e) + k.yyx * map(p + k.yyx * e) + k.yxy * map(p + k.yxy * e) + k.xxx * map(p + k.xxx * e));
}
void fragment() {
	if (alpha <= 0.0) { discard; }
	vec2 uv = (UV * 2.0 - 1.0) * vec2(aspect, -1.0);
	vec3 rd = normalize(vec3(uv, focal));
	float t = 0.15, m = 1e3, hit = -1.0;
	for (int i = 0; i < 80; i++) {
		vec3 pw = rd * t;
		float d = map(pw);
		if (pw.z < band_z) m = min(m, d / t);
		if (d < 0.0015 * t) { hit = t; break; }
		t += d * 0.7;
		if (t > 16.0) break;
	}
	vec3 col = vec3(0.0);
	float a = 0.0;
	if (hit > 0.0 && (rd * hit).z < band_z) {
		vec3 pw = rd * hit;
		vec3 lp = to_local(pw);
		float mat = kunai(lp).y;
		vec3 n = normal_at(pw, 0.002 * hit);
		vec3 key = normalize(vec3(-0.5, 0.7, -0.6));
		float dif = max(dot(n, key), 0.0);
		vec3 hv = normalize(key - rd);
		float fres = pow(1.0 - abs(dot(n, rd)), 3.0);
		if (mat < 0.5) {
			// 刃：近黑的钢，斜面接到光时一片冷白的高光（转起来两个面轮流亮），刃口（菱形最外沿）一线白热
			float spec = pow(max(dot(n, hv), 0.0), 60.0);
			float sheen = pow(max(dot(n, hv), 0.0), 8.0);
			float y = clamp(lp.y, 0.0, 1.25);
			float w = y < 0.38 ? mix(0.075, 0.23, y / 0.38) : mix(0.23, 0.0, (y - 0.38) / 0.87);
			float edge = smoothstep(0.82, 1.0, abs(lp.x) / max(w, 0.002));
			float grind = 0.9 + 0.1 * noise(vec2(lp.y * 60.0, lp.x * 6.0));                 // 顺着刃的磨痕
			// 压在黑色横幅上要读得出来：钢是亮的冷灰，背光的斜面暗下去（两个斜面一明一暗，转起来才看得见）
			float facet = 0.5 + 0.5 * dot(n, normalize(vec3(-0.6, 0.5, -0.6)));
			col = mix(vec3(0.2, 0.21, 0.27), vec3(0.86, 0.9, 1.0), facet * facet) * grind + vec3(0.85, 0.9, 1.0) * sheen * 0.45 + vec3(1.0) * spec * 1.4;
			col += mix(vec3(0.9, 0.92, 1.0), vec3(1.0, 0.45, 0.35), heat) * edge * (0.6 + 1.6 * heat);
			col += vec3(1.0, 0.16, 0.2) * fres * 0.5;
			// 尖端烧红
			col += vec3(1.0, 0.35, 0.25) * heat * smoothstep(1.0, 1.25, lp.y) * 0.35;
		} else if (mat < 1.5) {
			// 柄：缠着暗红的布
			float weave = 0.75 + 0.25 * sin(lp.y * 70.0 + atan(lp.z, lp.x) * 2.0);
			col = vec3(0.5, 0.03, 0.06) * (0.4 + 0.9 * dif) * weave + vec3(1.0, 0.2, 0.22) * fres * 0.6;
		} else {
			float spec = pow(max(dot(n, hv), 0.0), 40.0);
			col = vec3(0.3, 0.32, 0.38) * (0.3 + dif) + vec3(1.0) * spec * 1.0 + vec3(1.0, 0.16, 0.2) * fres * 0.8;
		}
		// 越深越暗（刚从横幅里出来的那一截）
		col *= mix(1.0, 0.6, smoothstep(band_z - 0.6, band_z, pw.z));
		a = 1.0;
	}
	// 贴着轮廓的一圈暗红光 + 往后拖的咒烟
	float sz = size * focal / max(pos.z, 0.6);
	float mu = m * focal;
	float outline = exp(-mu / (sz * 0.012)) * (1.0 - a);             // 贴着轮廓一线白光：压在什么底色上都看得出形状
	float halo = exp(-mu / (sz * 0.05)) * 0.5 * (1.0 - a);
	float wisp = exp(-mu / (sz * 0.16)) * smoothstep(0.45, 0.8, noise(UV * vec2(26.0, 14.0) + vec2(clock * 1.5, -clock * 3.0))) * 0.35 * (1.0 - a);
	col += vec3(1.0, 0.1, 0.14) * halo + vec3(0.6, 0.03, 0.07) * wisp + vec3(1.0, 0.95, 0.95) * outline * 0.9;
	a = max(a, clamp(halo + wisp + outline, 0.0, 1.0));
	// 横幅上被穿破的那一圈：烧红的裂口 + 往外的裂纹（crack 越大裂得越远）
	vec3 q = rd * (band_z / rd.z);
	float dq = map(q);
	vec3 d0 = normalize(aim);
	vec3 ctr = pos + d0 * ((band_z - pos.z) / d0.z);                    // 长轴和横幅的交点
	vec2 rel = q.xy - ctr.xy;
	float rr = length(rel), ang = atan(rel.y, rel.x);
	float inband = step(pos.z + d0.z * 1.25 * size, band_z) * step(band_z, pos.z - d0.z * 0.9 * size);   // 苦无正跨在横幅上
	float through = max(inband, step(0.001, crack));
	float ring = exp(-max(dq, 0.0) * 34.0) * step(-0.002, dq) * inband;
	float cn = noise(vec2(ang * 2.2, 3.7)) + 0.5 * noise(vec2(ang * 6.0, 1.3));
	float cg = max(abs(dFdx(cn)) + abs(dFdy(cn)), 0.0006);
	float line = 1.0 - smoothstep(0.6, 1.6, abs(cn - 0.8) / cg);
	float reach = crack * (0.5 + 0.9 * noise(vec2(ang * 1.7, 9.0)));
	float cracks = line * (1.0 - smoothstep(reach * 0.8, reach, rr)) * smoothstep(0.02, 0.08, rr) * through * step(0.0, dq);
	float bulge = exp(-rr * rr / 0.5) * crack * 0.25 * through * step(0.0, dq);                            // 被顶起来的一块：微微发亮
	// 衬托旋转：绕着穿孔甩出去的几道弧光（跟着苦无一起转，头亮尾淡，两圈半径），转得越快甩得越开
	float swirl = 0.0;
	for (int i = 0; i < 2; i++) {
		float R = (0.2 + 0.2 * float(i)) * size;
		float saw = fract((ang - spin * (1.0 - 0.25 * float(i))) / 6.2831853 * 3.0 + float(i) * 0.17);
		swirl += exp(-pow((rr - R * (1.0 + 0.25 * (1.0 - saw))) / (0.02 + 0.014 * float(i)), 2.0)) * pow(saw, 2.0);
	}
	swirl *= inband;
	bulge = 0.0;
	float pa = clamp(ring + cracks * 0.9 + swirl, 0.0, 1.0) * (1.0 - a);
	col += mix(vec3(1.0, 0.3, 0.3), vec3(1.0, 0.95, 0.92), swirl) * swirl * (1.0 - a) * 1.3;
	col += mix(vec3(1.0, 0.2, 0.16), vec3(1.0, 0.85, 0.8), heat * ring) * (ring * 1.3 + cracks) + vec3(1.0, 0.25, 0.25) * bulge;
	a = max(a, pa);
	COLOR = vec4(col * alpha, clamp(a, 0.0, 1.0) * alpha);
}" };

	private readonly ShaderMaterial _mat;
	public readonly ColorRect Rect;

	private KunaiPierce(Node parent, Vector2 screen)
	{
		_mat = new ShaderMaterial { Shader = KunaiShader };
		_mat.SetShaderParameter("aspect", screen.X / screen.Y);
		Rect = new ColorRect { Material = _mat, Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Name = "P5Kunai" };
		parent.AddChild(Rect);
	}

	/// <summary>时间轴（秒，从 emergeAt 算）：冲到横幅、慢放、冲出镜头。</summary>
	public const float Approach = 0.1f, Slow = 0.28f, Exit = 0.12f;   // 用户 2026-11-10：要迅捷、干脆利落，慢放只一小会儿

	/// <summary>从苦无出现到横幅碎开过了多久（CutInPlayback 的 SplitAt = emergeAt + PierceAfter）。</summary>
	public const float PierceAfter = Approach + Slow;

	private const float Focal = 1.25f, BandZ = 2.2f;
	private static readonly Vector3 Aim = new Vector3(1f, -0.42f, -0.5f).Normalized();   // 用户 2026-11-10：要看得出是一把苦无 → 更侧着飞（朝右下、略朝镜头），整把的侧影都看得见

	/// <summary>世界里的点 → 屏幕像素。</summary>
	private static Vector2 ToScreen(Vector3 w, Vector2 screen) =>
		new((w.X / w.Z * Focal / (screen.X / screen.Y) + 1f) * 0.5f * screen.X, (1f - w.Y / w.Z * Focal) * 0.5f * screen.Y);

	/// <summary>
	/// 放一次（和 CutIn.Eyes 同一个 playback；放在背景层、眼神横幅上面、人物后面）。onTouch：尖端碰到横幅（慢放开始）；onPierce：横幅碎开。
	/// </summary>
	public static KunaiPierce Play(Control back, float emergeAt, CutInPlayback playback, System.Action? onTouch = null, System.Action? onPierce = null,
		Vector2? center = null)
	{
		Vector2 screen = Fx.Screen(back);
		Node2D root = Fx.ScreenRoot(back, "P5KunaiPierce");
		KunaiPierce k = new(root, screen);
		const float size = 1.15f;     // 整把苦无（尖到环 ≈ 2.1 × size）在慢放里全部穿出来、横在画面中间
		// 长轴穿过横幅的点（世界）：屏幕中间偏右上一点；刃尖沿 Aim 朝镜头走
		// 用户 2026-11-10：从眼神横幅的正中间穿出来（横幅中心在屏幕上的位置由 center 给，换算到横幅所在的深度）
		Vector2 c = center ?? new Vector2(screen.X * 0.4f, screen.Y * 0.36f);
		Vector3 through = new((c.X / screen.X * 2f - 1f) * (screen.X / screen.Y) * BandZ / Focal, (1f - c.Y / screen.Y * 2f) * BandZ / Focal, BandZ);
		// 刃根的位置 = through - Aim * (尖到刃根 1.25*size) + Aim * 走了多远
		Vector3 baseAt(float tipPast) => through + Aim * (tipPast - 1.25f * size);
		float touchAt = emergeAt + Approach, splitAt = emergeAt + PierceAfter, life = splitAt + Exit;
		bool touched = false, pierced = false;
		playback.Outward = true;                                    // 碎片朝玩家炸开
		float spin = 0f, last = 0f;
		k.Set("alpha", 0f);
		k.Set("band_z", BandZ);
		k.Set("size", size);
		k.Set("aim", Aim);
		Fx.Play(root, playback, life, s =>
		{
			float dt = Mathf.Max(0f, s - last);
			last = s;
			float tipPast, heat = 0f, crack = 0f, alpha = 1f, rate;
			if (s < emergeAt)
			{
				tipPast = -6f;
				alpha = 0f;
				rate = 0f;
			}
			else if (s < touchAt)
			{
				// 从深处冲过来：还在横幅后面，看不见，只看得见横幅被顶起来之前的一瞬
				float u = (s - emergeAt) / Approach;
				tipPast = Mathf.Lerp(-6f, 0f, u * u);
				rate = 26f;
			}
			else if (s < splitAt)
			{
				// 慢放：尖端挤出来，转得看得清，裂纹越裂越远，刃口烧红
				float u = (s - touchAt) / Slow;
				tipPast = Mathf.Lerp(0.5f * size, 2.35f * size, Mathf.Pow(u, 0.75f));     // 一刺破就露出半截刃，慢放里整把（柄、环）都穿出来
				rate = Mathf.Lerp(16f, 9f, u);
				heat = Mathf.Min(1f, u * 2.5f);
				crack = 0.3f + 1.4f * u;
				if (!touched)
				{
					touched = true;
					Sfx.Play(Sfx.Crack, 0.6f);                         // 尖端顶破横幅
					Sfx.Play(Sfx.SubBoom, 0.5f);
					// 刺破那一下的冲击（用户 2026-11-10：冲击力、表现力要强）：白闪、两圈冲击环、空间一扭、玻璃碴朝镜头迸出来、火星、震屏
					Vector2 hole = root.ToGlobal(ToScreen(through, screen));
					float unit = screen.Y / 1080f;
					Fx.Shake(4);
					HitStop.Request(0.03);
					Glow.Flash(back, hole, new Color(1f, 0.96f, 0.94f), 420f * unit, 0.12f);
					Glow.Flash(back, hole, new Color(1f, 0.2f, 0.25f), 1100f * unit, 0.35f);
					Glow.Shockwave(back, hole, new Color(1f, 0.92f, 0.9f), 900f * unit, 0.3f);
					Glow.Shockwave(back, hole, new Color(1f, 0.25f, 0.3f), 1500f * unit, 0.45f, 1f, 0.04f);
					Glow.Shockwave(back, hole, new Color(1f, 0.6f, 0.6f), 2200f * unit, 0.55f, 1f, 0.08f);   // 不用折射波纹：这里它把舞台后面的原场景折出一圈暗绿
					Glow.Shards(back, hole, 30, 2.2f, new Vector2(0.6f, 0.5f), 2.6f);
					Glow.Sparks(back, hole, 40, 500f, 1700f, 0.4f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.35f, 0.38f), new Color(0.7f, 0.04f, 0.12f) }, 300f,
						0.03f * unit, 0.09f * unit, new Vector2(0.6f, 0.5f), 150f);
					onTouch?.Invoke();
				}
				playback.ShatterFrom = ToScreen(through, screen);       // 横幅不变形（用户 2026-11-10），只记下从哪里碎开
			}
			else
			{
				// 横幅碎开，苦无恢复速度冲出镜头
				float u = (s - splitAt) / Exit;
				tipPast = Mathf.Lerp(2.35f * size, 2.35f * size + 5f, u * u);
				rate = 26f;
				heat = 1f - u;
				alpha = 1f - Mathf.Clamp((u - 0.7f) / 0.3f, 0f, 1f);
				if (!pierced)
				{
					pierced = true;
					onPierce?.Invoke();
				}
			}
			spin += dt * rate;
			k.Set("pos", baseAt(tipPast));
			k.Set("spin", spin);
			k.Set("heat", heat);
			k.Set("crack", pierced ? 0f : crack);
			k.Set("alpha", alpha);
			k.Set("clock", s);
			k.Rect.Visible = alpha > 0f;
		});
		return k;
	}

	private void Set(string name, Variant v) => _mat.SetShaderParameter(name, v);
}
