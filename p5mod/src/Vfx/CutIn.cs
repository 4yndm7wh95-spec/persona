using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 眼神特写（用户 2026-10-29：逐帧复刻 P5R 原版的切入 / 切出和撕纸细节，用 Joker 自己的原版切入图 BCT_P_BC0001 = 贴图 "eyes"；
/// Persona 召唤时放，在背景层）。坐标全部按画面比例（u = x / 宽，v = y / 高）量自用户给的原版录像：
/// · 形状：一道往右张开的楔形裂口。上沿内侧从 (0, .575) 几乎直线升到 (.75, .235)；下沿左三成平在 .75，u≈.35 抬到 .675，
///   再缓升到 (.75, .61)。左端高 .19，中段 .31～.35。
/// · 边（从图往外）：白纸条（粗细不一，偶尔分叉成两股、中间夹一道黑缝）→ 窄而时有时无的黑边 → 游戏画面；
///   沿边散着细长的刀片状白纸片（顺着裂口的斜度，两头尖，翘起一个小撕角，黑描边），一端常贴着纸条。
/// · 右边：大「!!」（两根上宽下窄的斜条 + 两个方点，白底粗黑描边）压在一块黑底上，黑底上拼着红块、灰蓝板、碎条。
/// · 节奏（录像 30fps）：0～.10s 左边伸出一道白裂缝，长到 .33 再到 .42 停住；.10～.145s 整条撕开、口子里先是白纸（中间偏右鼓得最开）；
///   .145s 眼睛图出现（黑底红块同时出现）；.20s「!!」弹出；之后完全静止（不推近、不平移）到 .52s；
///   .52～.77s 像闭眼：从左往右上下合拢、整条往右上收，下沿露出红纸、左边撕成一根根尖条；「!!」先抖、裂开，
///   变成一块细长的白纸板（带黑裂纹、下面一条红）退到右上角，最后剩一小块红白碎纸。
/// </summary>
public static class CutIn
{
	// 时间轴（秒）
	private const float TCrack = 0.10f, TOpen = 0.145f, TBang = 0.24f, THold = 0.553f, TExit = 0.25f, TEnd = THold + TExit + 0.08f;

	private static readonly Color White = new("fbfbfa"), Ink = new("060608"), Red = new("e3121c"), Slate = new("b9cbe0");

	private static Shader? _band;

	/// <summary>裂口的上沿 / 下沿（白纸条内侧那条线），和着色器里的 topBase / botBase 一样。</summary>
	public static float TopV(float u) => 0.575f - 0.453f * u;

	public static float BotV(float u) =>
		u < 0.3f ? 0.75f : u < 0.42f ? Mathf.Lerp(0.75f, 0.675f, (u - 0.3f) / 0.12f) : u < 0.62f ? Mathf.Lerp(0.675f, 0.67f, (u - 0.42f) / 0.2f)
		: u < 0.75f ? Mathf.Lerp(0.67f, 0.61f, (u - 0.62f) / 0.13f) : 0.61f - 0.2f * (u - 0.75f);

	private static Shader Band => _band ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D tex : filter_linear_mipmap;
uniform vec2 screen = vec2(1920.0, 1080.0);
uniform float crack = 0.0;      // 裂缝长到哪（u）
uniform float open = 0.0;       // 张开程度 0～1
uniform float bulge = 0.0;      // 刚撕开时口子中间偏右鼓出来
uniform float blank = 0.0;      // 1 = 口子里是白纸（还没换成图）
uniform float closeL = -1.0;    // 合上时：左端合拢到哪（u）
uniform float red = 0.0;        // 下沿红纸露出多少
uniform float streak = 0.0;     // 切出的最后一拍：带子合成一道粗的白色撕纸斜条（原版 1.167s）
uniform vec2 shift = vec2(0.0); // 合上时整条往右上挪（u, v）
uniform float seed = 0.0;
uniform float alpha = 1.0;
float hash(float x) { return fract(sin(x * 127.1 + seed * 311.7) * 43758.5453); }
// 折线噪声：随机值之间直线连起来，转角是尖的（撕纸）
float ln(float x) { float i = floor(x); return mix(hash(i), hash(i + 1.0), fract(x)); }
// 撕纸的切口（原版的「小分支」）：沿纸条一段段，每段可能有一道黑色细缝——一头尖、一头圆，顺着纸条拉长、略斜。
// 贴着外沿（t≈1）= 把最外一层撕成往外翘的白尖舌；贴着图那侧（t≈0，也伸进图里）= 一根白尖舌伸进图；在中间 = 纸条分成两股又合上。
// t：在纸条里的位置（0 = 挨着图，1 = 外沿）；L：一段多长（u）；返回 1 = 这里是黑缝。
float cutAt(float u, float t, float L, float sd) {
	float c = floor(u / L), x = fract(u / L);
	if (hash(c * 1.7 + sd) < 0.42) return 0.0;
	float x0 = 0.04 + 0.25 * hash(c * 3.1 + sd + 1.0);
	float x1 = min(x0 + 0.4 + 0.5 * hash(c * 5.3 + sd + 2.0), 0.98);
	float k = (x - x0) / (x1 - x0);
	if (k < 0.0 || k > 1.0) return 0.0;
	if (hash(c * 7.9 + sd + 3.0) < 0.62) k = 1.0 - k;           // 多数尖头朝左（顺着撕的方向）
	float th = (0.16 + 0.3 * hash(c * 9.7 + sd + 4.0)) * pow(k, 0.9) * sqrt(max(0.0, 1.0 - pow(k, 5.0)));
	float an = hash(c * 11.3 + sd + 5.0);
	float t0 = an < 0.38 ? 1.05 : an < 0.7 ? -0.05 : 0.3 + 0.4 * hash(c * 13.1 + sd);
	float tc = t0 + (hash(c * 17.0 + sd) - 0.5) * 0.7 * (k - 0.5);
	return step(abs(t - tc), th);
}
float cuts(float u, float t, float sd) { return max(cutAt(u, t, 0.055, sd), cutAt(u + 0.37, t, 0.1, sd + 50.0)); }
// 白纸和图交界处的小平台阶
float steps(float u, float sd) { return floor(ln(u * 70.0 + sd) * 3.0) / 3.0 * 0.004; }
float topBase(float u) { return 0.575 - 0.453 * u; }
float botBase(float u) {
	if (u < 0.3) return 0.75;
	if (u < 0.42) return mix(0.75, 0.675, (u - 0.3) / 0.12);
	if (u < 0.62) return mix(0.675, 0.67, (u - 0.42) / 0.2);
	if (u < 0.75) return mix(0.67, 0.61, (u - 0.62) / 0.13);
	return 0.61 - 0.2 * (u - 0.75);
}
// 撕开那一帧（原版 0.583s，量出来的）：一道约 0.03 屏高的细白条，中心线从左 0.64 斜升到右 0.33，
// 只在 u 0.4～0.72 之间下沿垂下一个圆肚子（u 0.6 处垂到 0.6）
float blankC(float u) {
	if (u < 0.1) return mix(0.636, 0.644, u / 0.1);
	if (u < 0.2) return mix(0.644, 0.61, (u - 0.1) / 0.1);
	if (u < 0.3) return mix(0.61, 0.558, (u - 0.2) / 0.1);
	if (u < 0.4) return mix(0.558, 0.478, (u - 0.3) / 0.1);
	if (u < 0.5) return mix(0.478, 0.44, (u - 0.4) / 0.1);
	if (u < 0.8) return mix(0.44, 0.415, (u - 0.5) / 0.3);
	if (u < 0.9) return mix(0.415, 0.393, (u - 0.8) / 0.1);
	return mix(0.393, 0.33, (u - 0.9) / 0.1);
}
const vec4 PAPER = vec4(0.985, 0.985, 0.98, 1.0);
const vec4 INK = vec4(0.024, 0.024, 0.03, 1.0);
void fragment() {
	vec2 uv = UV - shift;
	float u = uv.x, v = uv.y;
	float o = clamp(open, 0.0, 1.0);
	float shut = closeL < -0.5 ? 1.0 : smoothstep(closeL, closeL + 0.3, u);
	float top0 = topBase(u) + (ln(u * 9.0) - 0.5) * 0.022 + (ln(u * 37.0 + 5.0) - 0.5) * 0.008;
	float bot0 = botBase(u) + (ln(u * 8.0 + 11.0) - 0.5) * 0.024 + (ln(u * 33.0 + 17.0) - 0.5) * 0.009;
	float mid = (top0 + bot0) * 0.5, half_ = (bot0 - top0) * 0.5;
	// 刚撕开（白纸）时：中间偏右往下鼓一个圆肚子，两头细（原版 0.583s）
	float b = 1.0 - bulge * 0.7 * smoothstep(0.4, 0.0, u) - bulge * 0.6 * smoothstep(0.7, 1.0, u);
	float h = half_ * o * b * shut;
	float top = mid - h, bot = mid + h;
	// 白纸那两帧：上沿几乎不动、下沿在中间偏右往下垂一个圆肚子
	bot += bulge * 0.45 * exp(-pow((u - 0.56) / 0.1, 2.0));
	// 白纸条（偶尔翻出一大片）、窄而时有时无的黑边、偶尔分叉出去的第二股白条（两头尖，中间夹黑缝）
	float wT = 0.015 + pow(ln(u * 23.0 + 3.0), 3.0) * 0.03, wB = 0.016 + pow(ln(u * 21.0 + 41.0), 3.0) * 0.032;
	float kT = 0.003 + pow(ln(u * 15.0 + 7.0), 5.0) * 0.03, kB = 0.004 + pow(ln(u * 13.0 + 29.0), 5.0) * 0.025;
	float s = max(clamp(o * 1.5, 0.0, 1.0), step(0.5, red)) * shut;     // 切出时白纸边不跟着变细
	s *= 1.0 + streak * 1.6;                                            // 最后一拍：只剩白纸，加粗成一道撕纸斜条
	wT *= s; wB *= s; kT *= s; kB *= s;
	top -= steps(u, 3.0) * s; bot += steps(u, 9.0) * s;
	vec4 col = vec4(0.0);
	if (blank > 0.5) {
		float c = blankC(u) + (ln(u * 30.0 + 3.0) - 0.5) * 0.008;
		float th = 0.013 + pow(ln(u * 19.0 + 7.0), 3.0) * 0.012;
		float k = clamp((u - 0.4) / 0.32, 0.0, 1.0);
		float belly = 0.21 * pow(sin(k * 3.14159), 0.6) * smoothstep(0.0, 0.12, k);   // 圆肚子：两边陡、底是圆的
		float t0 = c - th - 0.02 * belly / 0.21, b0 = c + th + belly + (ln(u * 44.0 + 13.0) - 0.5) * 0.01;
		if (v > t0 && v < b0) { col = PAPER; }
		else if (v > t0 - 0.004 && v < b0 + 0.004) { col = INK; }
	}
	else if (h > 0.0005) {
		if (v > top && v < bot) {
			if (blank > 0.5) {
				col = PAPER;
			} else {
				// 眼睛图：按裂口的斜度（-9°）放，眼睛那一行落在口子正中：画面 (.465, .515) = 图 (.47, .56)；图宽 = 屏宽 1.1 倍；图本身不动
				vec2 p = vec2(u, v) * screen - vec2(0.465, 0.515) * screen;
				float a = radians(9.0);
				p = vec2(cos(a) * p.x - sin(a) * p.y, sin(a) * p.x + cos(a) * p.y);
				float w = screen.x * 1.1;
				vec2 t = vec2(0.47, 0.56) + p / vec2(w, w * 0.4);
				col = vec4(texture(tex, t).rgb, 1.0);
			}
		}
		else if (v > top - wT && v <= top) { col = PAPER; }
		else if (v > top - wT - kT && v <= top - wT) { col = INK; }
		else if (v >= bot && v < bot + wB) { col = PAPER; }
		else if (v >= bot + wB && v < bot + wB + kB) { col = INK; }
		// 切口：在白纸条里、以及挨着纸条的图边上（t < 0 伸进图里）
		if (blank < 0.5 && wT > 0.002) {
			float tT = (top - v) / wT, tB = (v - bot) / wB;
			if ((tT > -0.6 && tT < 1.0 && cuts(u, tT, 1.0) > 0.5) || (tB > -0.6 && tB < 1.0 && cuts(u, tB, 7.0) > 0.5)) { col = INK; }
		}
	}
	// 下沿后面的红纸：贴着（正在合拢的）下沿，往左伸出一截、末端撕成一根根尖条，黑描边；合上时最显眼
	if (col.a < 0.5 && red > 0.0) {
		float hr = half_ * o * b * smoothstep(closeL - 0.05, closeL + 0.3, u);
		float rTop = mid + hr - 0.006;
		// 一大块粗楔形红纸贴着带子左下（合拢处最粗），往左只伸出一小截、撕成一根根尖条
		float reach = smoothstep(closeL - 0.12, closeL + 0.02, u) * smoothstep(0.85, 0.45, u);
		float strand = mix(0.25, 1.0, pow(ln(u * 46.0 + 5.0), 1.5));
		float wedge = 0.018 + 0.03 * exp(-pow((u - closeL - 0.1) / 0.14, 2.0));
		float rTh = red * (wedge + pow(ln(u * 11.0 + 61.0), 3.0) * 0.012) * reach * mix(strand, 1.0, smoothstep(closeL - 0.02, closeL + 0.1, u));
		if (v > rTop && v < rTop + rTh) { col = vec4(0.89, 0.07, 0.11, 1.0); }
		else if (rTh > 0.002 && v > rTop - 0.003 && v < rTop + rTh + 0.004) { col = INK; }
	}
	// 裂缝（还没撕开时）：从左边伸出的一道白色细缝，往右越来越细、尖头，黑描边
	if (col.a < 0.5 && crack > 0.0 && o < 0.01) {
		float k = u / max(crack, 0.001);
		float c = (topBase(u) + botBase(u)) * 0.5 + 0.02 - k * 0.05 + (ln(u * 30.0) - 0.5) * 0.012;
		float th = 0.016 * pow(max(1.0 - k, 0.0), 0.6) * (0.6 + ln(u * 41.0 + 9.0));
		float d = abs(v - c);
		if (k < 1.0 && d < th) { col = PAPER; }
		else if (k < 1.0 && d < th + 0.004) { col = INK; }
	}
	COLOR = vec4(col.rgb, col.a * alpha);
}" };

	/// <summary>放一次眼神特写（back = 背景特效层；整张屏幕的坐标）。</summary>
	public static void Eyes(Control back)
	{
		if (Fx.Tex("eyes") is not Texture2D tex)
		{
			return;
		}
		Vector2 screen = Fx.Screen(back);
		Vector2 P(float u, float v) => new(u * screen.X, v * screen.Y);
		Node2D root = Fx.Root(back, Vector2.Zero, "P5CutIn", TEnd + 0.1);
		root.GlobalPosition = Vector2.Zero;

		ColorRect band = new() { Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Band } };
		root.AddChild(band);
		ShaderMaterial m = (ShaderMaterial)band.Material;
		m.SetShaderParameter("tex", tex);
		m.SetShaderParameter("screen", screen);
		m.SetShaderParameter("seed", Fx.Rand(0f, 50f));

		// 沿裂口散着的白纸片，张开时出现，合上时跟着往右上被带走
		Node2D shards = new();
		root.AddChild(shards);
		List<Node2D> bits = Shards(shards, screen);

		// 右边一整团（黑底、碎裂层、「!!」）一起动：切出时先原地碎裂，再往右上角收成一道斜条
		Node2D right = new();
		root.AddChild(right);
		Node2D plate = Plate(right, P);
		Node2D shatter = Shatter(right, P, out Node2D whites);
		Node2D bang = Bang(right, P, out Node2D cracks, out List<Node2D> bars);
		Node2D scrap = FinalScrap(root, P);
		plate.Visible = bang.Visible = shatter.Visible = scrap.Visible = shards.Visible = false;

		Fx.Animate(root, 0, TEnd, t =>
		{
			float s = t * TEnd;
			// 切入：裂缝 → 撕开（白）→ 图
			m.SetShaderParameter("crack", s < TCrack ? (s < 0.035f ? 0.33f * s / 0.035f : Mathf.Lerp(0.33f, 0.42f, Mathf.Min(1f, (s - 0.035f) / 0.03f))) : 0f);
			float open = s < TCrack ? 0f : s < TOpen ? 0.42f : 1f;
			m.SetShaderParameter("bulge", s < TOpen ? 0.45f : 0f);
			m.SetShaderParameter("blank", s >= TCrack && s < TOpen ? 1f : 0f);
			plate.Visible = s >= TOpen;
			right.Visible = true;
			band.Visible = true;
			shards.Visible = s >= TOpen;
			bang.Visible = s >= TBang;
			float pop = Mathf.Clamp((s - TBang) / 0.07f, 0f, 1f);
			float sc = Mathf.Lerp(1.12f, 1f, Fx.OutCubic(pop));
			Vector2 pivot = P(0.82f, 0.38f);
			bang.Scale = Vector2.One * sc;
			bang.Position = pivot * (1f - sc);
			bang.Rotation = 0f;
			bang.Modulate = Colors.White;
			cracks.Visible = false;
			m.SetShaderParameter("red", 0f);
			m.SetShaderParameter("closeL", -1f);
			// 切出：像闭眼
			float e = Mathf.Clamp((s - THold) / TExit, 0f, 1f);
			if (e > 0f)
			{
				// 量自原版（e = 切出进度；1.033s≈.19、1.067≈.32、1.100≈.45、1.133≈.59、1.167≈.72、1.200≈.85）：
				// 带子 .19→.85 张开、.32→.7、.45→.5、.59→.45；.72 合成一道粗的白色撕纸斜条（红纸在下）；.85 只剩右上角红白碎纸
				open = e < 0.59f ? Mathf.Lerp(1f, 0.45f, Mathf.SmoothStep(0f, 1f, e / 0.59f)) : Mathf.Lerp(0.45f, 0.04f, Mathf.Clamp((e - 0.59f) / 0.13f, 0f, 1f));
				float closeL = e < 0.59f ? Mathf.Lerp(-0.1f, 0.12f, e / 0.59f) : Mathf.Lerp(0.12f, 0.38f, Mathf.Clamp((e - 0.59f) / 0.13f, 0f, 1f));
				float late = Mathf.Clamp((e - 0.59f) / 0.13f, 0f, 1f);
				m.SetShaderParameter("closeL", closeL);
				m.SetShaderParameter("streak", late);
				m.SetShaderParameter("shift", new Vector2(0.05f, -0.06f) * late);
				m.SetShaderParameter("red", 1f);
				band.Visible = e < 0.79f;
				shards.Position = P(0.03f, -0.02f) * e + P(0.06f, -0.05f) * late;
				shards.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(e));
				// 「!!」：.19 还在（裂纹出来），.32 劈开、各块错开，.42 就没了
				cracks.Visible = true;
				float brk = Mathf.Clamp((e - 0.15f) / 0.27f, 0f, 1f);
				for (int i = 0; i < bars.Count; i++)
				{
					Vector2 dir = new Vector2(Mathf.Cos(i * 2.3f), Mathf.Sin(i * 2.3f));
					bars[i].Position = dir * P(0f, 0.03f).Y * brk + new Vector2(Fx.Rand(-3f, 3f), Fx.Rand(-3f, 3f));
				}
				bang.Modulate = Colors.White;                          // 不做半透明（叠在黑底上会发灰）：碎开、错开，然后直接没了
				bang.Visible = brk < 0.8f;
				// 黑底：白色锯齿轮廓和裂纹 .2 起亮出，.59 前原地微微往右上挪；.59～.72 收成一道斜条（压扁、转到裂口的斜度、往右上角靠）
				shatter.Visible = true;
				whites.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((e - 0.12f) * 8f, 0f, 1f));
				float k = Mathf.SmoothStep(0f, 1f, late);
				Vector2 corner = P(0.99f, 0.16f);
				float sx = Mathf.Lerp(1f, 0.75f, k), sy = Mathf.Lerp(1f, 0.22f, k);
				right.Rotation = -0.42f * k;
				right.Scale = new Vector2(sx, sy);
				right.Position = corner - (corner * new Vector2(sx, sy)).Rotated(right.Rotation) + P(0.015f, -0.02f) * Mathf.Min(1f, e / 0.59f);
				right.Visible = e < 0.79f;
				scrap.Visible = e >= 0.79f && e < 1f;
				scrap.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((e - 0.93f) / 0.07f, 0f, 1f));
			}
			m.SetShaderParameter("open", open);
			// 碎片停住时轻轻飘
			for (int i = 0; i < bits.Count; i++)
			{
				Node2D b = bits[i];
				Vector2 home = (Vector2)b.GetMeta("home");
				float ph = i * 1.7f;
				b.Position = home + new Vector2(Mathf.Sin(s * 2.1f + ph), Mathf.Cos(s * 1.7f + ph)) * 3f;
			}
		});
	}

	/// <summary>一片撕下来的纸：刀片状细长、两头尖，一侧翘起一个小撕角（白底黑描边）。</summary>
	private static Node2D Scrap(Node parent, Vector2 at, float len, float wid, float ang, Color fill)
	{
		Node2D n = new() { Position = at, Rotation = ang };
		parent.AddChild(n);
		float notch = Fx.Rand(0.35f, 0.65f);
		Vector2[] pts =
		{
			new(-len * 0.5f, 0f),
			new(-len * Fx.Rand(0.1f, 0.25f), -wid * 0.45f),
			new(len * (notch - 0.5f), -wid * Fx.Rand(0.3f, 0.5f)),
			new(len * (notch - 0.44f), -wid * Fx.Rand(0.6f, 0.85f)),
			new(len * Fx.Rand(0.2f, 0.32f), -wid * 0.3f),
			new(len * 0.5f, Fx.Rand(-wid * 0.15f, wid * 0.1f)),
			new(len * Fx.Rand(0.05f, 0.2f), wid * 0.4f),
			new(-len * Fx.Rand(0.2f, 0.32f), wid * 0.22f),
		};
		foreach (Vector2[] o in Geometry2D.OffsetPolygon(pts, Mathf.Max(2f, wid * 0.14f), Geometry2D.PolyJoinType.Miter))
		{
			Fx.Poly(n, o, Ink);
		}
		Fx.Poly(n, pts, fill);
		return n;
	}

	/// <summary>沿裂口上下沿散着的纸片：顺着裂口的斜度（上沿约 -24°，下沿往右上翘），离边远近不一。</summary>
	private static List<Node2D> Shards(Node parent, Vector2 screen)
	{
		List<Node2D> list = new();
		float topAng = Mathf.Atan2(-0.453f * screen.Y, screen.X);
		// （u, 离边多远（屏幕高度比例，正 = 往外）, 长（屏宽比例）, 宽（屏高比例））
		(float U, float Off, float Len, float Wid)[] top =
		{
			(0.05f, 0.03f, 0.1f, 0.016f), (0.13f, 0.065f, 0.075f, 0.017f), (0.21f, 0.028f, 0.11f, 0.012f), (0.36f, 0.09f, 0.06f, 0.013f),
			(0.43f, 0.04f, 0.12f, 0.018f), (0.55f, 0.075f, 0.07f, 0.012f), (0.64f, 0.035f, 0.09f, 0.014f),
		};
		foreach (var (u, off, len, wid) in top)
		{
			Vector2 at = new(u * screen.X, (TopV(u) - off) * screen.Y);
			list.Add(Home(Scrap(parent, at, len * screen.X, wid * screen.Y, topAng + Fx.Rand(-0.2f, 0.15f), White)));
		}
		(float U, float Off, float Len, float Wid)[] bot =
		{
			(0.1f, 0.045f, 0.09f, 0.013f), (0.3f, 0.06f, 0.12f, 0.016f), (0.44f, 0.095f, 0.08f, 0.012f), (0.53f, 0.045f, 0.13f, 0.017f),
			(0.66f, 0.075f, 0.07f, 0.012f),
		};
		foreach (var (u, off, len, wid) in bot)
		{
			Vector2 at = new(u * screen.X, (BotV(u) + off) * screen.Y);
			list.Add(Home(Scrap(parent, at, len * screen.X, wid * screen.Y, Fx.Rand(-0.35f, -0.12f), White)));
		}
		return list;
	}

	private static Node2D Home(Node2D n)
	{
		n.SetMeta("home", n.Position);
		return n;
	}

	/// <summary>多边形外面包一圈黑描边（Geometry2D.OffsetPolygon，尖角）。</summary>
	private static void Outlined(Node parent, Vector2[] pts, Color fill, float ink)
	{
		foreach (Vector2[] o in Geometry2D.OffsetPolygon(pts, ink, Geometry2D.PolyJoinType.Miter))
		{
			Fx.Poly(parent, o, Ink);
		}
		Fx.Poly(parent, pts, fill);
	}

	/// <summary>「!!」后面的黑底 + 红块 + 灰蓝板 + 碎条（坐标量自原版定格帧）。</summary>
	private static Node2D Plate(Node parent, System.Func<float, float, Vector2> P)
	{
		Node2D n = new();
		parent.AddChild(n);
		Fx.Poly(n, new[] { P(0.8f, -0.02f), P(1.02f, -0.02f), P(1.02f, 0.63f), P(0.88f, 0.6f), P(0.82f, 0.74f), P(0.735f, 0.63f),
			P(0.725f, 0.44f), P(0.745f, 0.39f) }, Ink);
		Fx.Poly(n, new[] { P(0.857f, 0.283f), P(0.876f, 0.17f), P(0.908f, 0.187f), P(0.876f, 0.305f) }, Red);
		Fx.Poly(n, new[] { P(0.879f, 0.166f), P(0.912f, 0.147f), P(0.91f, 0.187f), P(0.876f, 0.172f) }, White);
		Fx.Poly(n, new[] { P(0.973f, 0.181f), P(1.01f, 0.14f), P(1.01f, 0.19f) }, Red);
		Fx.Poly(n, new[] { P(0.968f, 0.424f), P(1.01f, 0.356f), P(1.01f, 0.455f) }, Red);
		Fx.Poly(n, new[] { P(0.924f, 0.511f), P(0.955f, 0.447f), P(0.987f, 0.486f), P(0.965f, 0.545f) }, Slate);
		Fx.Poly(n, new[] { P(0.898f, 0.566f), P(0.908f, 0.543f), P(0.927f, 0.583f), P(0.908f, 0.588f) }, Slate);
		Fx.Poly(n, new[] { P(0.843f, 0.305f), P(0.862f, 0.318f), P(0.852f, 0.334f) }, Slate);
		Fx.Poly(n, new[] { P(0.98f, 0.49f), P(1.01f, 0.5f), P(1.0f, 0.575f), P(0.985f, 0.53f) }, White);
		return n;
	}

	/// <summary>「!!」：两根上宽下窄的斜条 + 两个方点，白底粗黑描边（坐标量自原版定格帧）。cracks：切出时出现的黑裂纹。</summary>
	private static Node2D Bang(Node parent, System.Func<float, float, Vector2> P, out Node2D cracks, out List<Node2D> bars)
	{
		Node2D n = new();
		parent.AddChild(n);
		float ink = P(0f, 0.018f).Y;
		bars = new List<Node2D>();
		foreach (Vector2[] bar in new[]
		{
			new[] { P(0.806f, -0.02f), P(0.889f, 0.042f), P(0.797f, 0.407f), P(0.755f, 0.402f) },
			new[] { P(0.743f, 0.447f), P(0.799f, 0.452f), P(0.768f, 0.526f), P(0.737f, 0.498f) },
			new[] { P(0.936f, 0.209f), P(1.01f, 0.25f), P(0.841f, 0.585f), P(0.807f, 0.537f) },
			new[] { P(0.783f, 0.579f), P(0.828f, 0.619f), P(0.804f, 0.715f), P(0.761f, 0.622f) },
		})
		{
			Node2D b = new();
			n.AddChild(b);
			Outlined(b, bar, White, ink);
			bars.Add(b);
		}
		// 碎裂时劈开「!!」的粗黑锯齿裂纹（横着、斜着把竖条劈成几段）
		cracks = new Node2D();
		n.AddChild(cracks);
		float w = P(0f, 0.016f).Y;
		foreach (Vector2[] c in new[]
		{
			new[] { P(0.76f, 0.12f), P(0.79f, 0.1f), P(0.8f, 0.15f), P(0.84f, 0.12f), P(0.87f, 0.16f) },
			new[] { P(0.75f, 0.28f), P(0.78f, 0.31f), P(0.8f, 0.26f), P(0.83f, 0.3f) },
			new[] { P(0.86f, 0.33f), P(0.9f, 0.37f), P(0.93f, 0.33f), P(0.97f, 0.38f) },
			new[] { P(0.82f, 0.48f), P(0.85f, 0.45f), P(0.87f, 0.5f), P(0.9f, 0.47f) },
			new[] { P(0.77f, 0.64f), P(0.8f, 0.68f), P(0.83f, 0.64f) },
			new[] { P(0.95f, 0.22f), P(0.93f, 0.27f), P(0.97f, 0.29f), P(1.0f, 0.26f) },
		})
		{
			cracks.AddChild(new Line2D { Points = c, Width = w, DefaultColor = Ink, JointMode = Line2D.LineJointMode.Sharp });
		}
		return n;
	}

	/// <summary>一道白色闪电状锯齿裂纹（原版切出时黑底上那种：折线、尖角、粗细不一，带几根短分叉）。</summary>
	private static void Bolt(Node parent, Vector2 a, Vector2 b, float w, int segs)
	{
		Vector2 d = b - a, n = d.Orthogonal().Normalized();
		var pts = new Vector2[segs + 1];
		for (int i = 0; i <= segs; i++)
		{
			float k = (float)i / segs;
			pts[i] = a + d * k + n * (i == 0 || i == segs ? 0f : Fx.Rand(-1f, 1f) * d.Length() * 0.12f);
		}
		parent.AddChild(new Line2D { Points = pts, Width = w, DefaultColor = White, JointMode = Line2D.LineJointMode.Sharp,
			WidthCurve = Glow.Taper });
		for (int i = 1; i < segs; i++)
		{
			if (Fx.Rng.NextDouble() < 0.45)
			{
				Vector2 tip = pts[i] + (n * Fx.Rand(-1f, 1f) + d.Normalized() * Fx.Rand(-0.3f, 0.6f)).Normalized() * d.Length() * Fx.Rand(0.08f, 0.18f);
				parent.AddChild(new Line2D { Points = new[] { pts[i], tip }, Width = w * 0.7f, DefaultColor = White, WidthCurve = Glow.Taper });
			}
		}
	}

	/// <summary>
	/// 切出时被撕碎的右侧黑底：黑底轮廓同 Plate，上面几道白色闪电裂纹，边上一圈白色碎片（三角 / 细长条，尖头朝外）。
	/// whites：裂纹和碎片（切出的第一拍才亮出来）。
	/// </summary>
	private static Node2D Shatter(Node parent, System.Func<float, float, Vector2> P, out Node2D whites)
	{
		Node2D n = new();
		parent.AddChild(n);
		Fx.Poly(n, new[] { P(0.8f, -0.02f), P(1.02f, -0.02f), P(1.02f, 0.63f), P(0.88f, 0.6f), P(0.82f, 0.74f), P(0.735f, 0.63f),
			P(0.725f, 0.44f), P(0.745f, 0.39f) }, Ink);
		Fx.Poly(n, new[] { P(0.968f, 0.424f), P(1.01f, 0.356f), P(1.01f, 0.455f) }, Red);
		whites = new Node2D();
		n.AddChild(whites);
		float w = P(0f, 0.026f).Y;
		// 黑底左边缘一圈白色锯齿轮廓（原版 1.100s）
		Vector2[] rim = { P(0.8f, -0.02f), P(0.745f, 0.39f), P(0.725f, 0.44f), P(0.735f, 0.63f), P(0.82f, 0.74f), P(0.88f, 0.6f) };
		for (int i = 0; i + 1 < rim.Length; i++)
		{
			Bolt(whites, rim[i], rim[i + 1], w * 0.9f, 5);
		}
		Bolt(whites, P(0.83f, -0.01f), P(0.79f, 0.42f), w, 6);
		Bolt(whites, P(0.93f, 0.05f), P(0.85f, 0.56f), w * 1.2f, 7);
		Bolt(whites, P(1.0f, 0.2f), P(0.9f, 0.6f), w, 5);
		Bolt(whites, P(0.76f, 0.45f), P(0.84f, 0.7f), w * 0.8f, 4);
		Bolt(whites, P(0.86f, 0.3f), P(1.0f, 0.38f), w * 0.8f, 4);
		// 边上崩出的白碎片
		(float U, float V, float A)[] bits =
		{
			(0.73f, 0.5f, 2.6f), (0.75f, 0.38f, 2.2f), (0.79f, 0.68f, 2.4f), (0.84f, 0.71f, -1.4f), (0.9f, 0.62f, -1.2f),
			(0.97f, 0.6f, -0.9f), (0.79f, 0.05f, 2.0f), (0.88f, 0.0f, -2.6f), (0.74f, 0.58f, 2.9f),
		};
		foreach (var (u, v, a) in bits)
		{
			float len = P(0f, Fx.Rand(0.04f, 0.09f)).Y, wid = len * Fx.Rand(0.25f, 0.45f);
			Node2D b = new() { Position = P(u, v), Rotation = a };
			whites.AddChild(b);
			Fx.Poly(b, new[] { new Vector2(len, 0f), new Vector2(-len * 0.2f, -wid * 0.5f), new Vector2(-len * 0.35f, wid * 0.1f),
				new Vector2(-len * 0.1f, wid * 0.5f) }, White);
		}
		return n;
	}

	/// <summary>最后留在右上角的一小撮红白碎纸（原版 1.200s，u .79～.99，v .19～.35）。</summary>
	private static Node2D FinalScrap(Node parent, System.Func<float, float, Vector2> P)
	{
		Node2D n = new();
		parent.AddChild(n);
		Outlined(n, new[] { P(0.8f, 0.34f), P(0.86f, 0.29f), P(0.9f, 0.27f), P(0.96f, 0.21f), P(1.0f, 0.19f), P(0.99f, 0.24f),
			P(0.94f, 0.28f), P(0.91f, 0.33f), P(0.86f, 0.32f) }, Red, P(0f, 0.004f).Y);
		Fx.Poly(n, new[] { P(0.79f, 0.345f), P(0.84f, 0.315f), P(0.83f, 0.335f) }, White);
		Fx.Poly(n, new[] { P(0.88f, 0.27f), P(0.93f, 0.235f), P(0.91f, 0.26f) }, White);
		Fx.Poly(n, new[] { P(0.95f, 0.27f), P(1.0f, 0.255f), P(0.97f, 0.285f) }, White);
		Fx.Poly(n, new[] { P(0.83f, 0.3f), P(0.87f, 0.275f), P(0.855f, 0.295f) }, Red);
		return n;
	}
}
