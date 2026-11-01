using Godot;

namespace P5Joker;

/// <summary>
/// Persona 召唤的「舞台」（2026-11-01：P5 的冲击力来自整屏的平面语言，而不是贴在人身边的光）。
/// 一块全屏着色器放在背景特效层（人物、敌人后面），读后面的画面：
/// · 入场：白闪、震屏，背景一下变成红黑高反差 + 红色网点（亮度分三档：黑 / 深红 / 红网点，高光偏粉白），
///   从 Joker 头部放射漫画集中线（每两帧换一次），四周压暗。人物和敌人在前面不受影响，像剪影一样跳出来；原版眼神特写在这块舞台上撕开。
/// · 退场：一道撕纸的锯齿斜线从左下扫到右上，扫过的地方恢复原画面，撕口是白纸边 + 黑描边，正好接上眼神特写合拢的那一拍。
/// 全部实时渲染，不用贴图、不画几何。
/// </summary>
public static class SummonStage
{
	/// <summary>舞台持续多久：和眼神特写一起在 CutIn.SplitAt 被同一条撕口劈开。</summary>
	public const float Length = CutIn.SplitAt + CutIn.SplitTime + 0.04f;

	/// <summary>这一次召唤的撕口（舞台和眼神特写共用同一条）。</summary>
	public static float TearSeed;

	/// <summary>劈开的两半飞多远（0～1 → 位移倍数）：猛地裂开，然后加速飞出画面。</summary>
	public static float SplitCurve(float e) => e <= 0f ? 0f : 0.04f * Fx.OutCubic(Mathf.Min(1f, e / 0.25f)) + 1.3f * Fx.InCubic(e);

	private static Shader? _shader;

	private static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec2 res = vec2(1920.0, 1080.0);
uniform vec2 focus = vec2(0.25, 0.45);
uniform float amount = 0.0;
uniform float flash = 0.0;
uniform float clock = 0.0;" + CutIn.TearGlsl + @"
uniform float seed = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float h1(float x) { return hash(vec2(x, seed)); }
float ln(float x) { float i = floor(x); return mix(h1(i), h1(i + 1.0), fract(x)); }
vec3 stage(vec2 suv, vec2 uv) {
	vec3 src = texture(screen_tex, suv).rgb;
	float l = dot(src, vec3(0.299, 0.587, 0.114));
	l = clamp((l - 0.04) * 2.6, 0.0, 1.0);
	vec2 fp = uv * res;
	vec2 rp = vec2(fp.x + fp.y, fp.y - fp.x) * 0.7071;
	vec2 cell = fract(rp / 8.0) - 0.5;
	float r0 = 0.58 * sqrt(l);
	float dots = smoothstep(r0, r0 - 0.09, length(cell));
	vec3 black = vec3(0.025, 0.0, 0.012), deep = vec3(0.36, 0.0, 0.035), red = vec3(0.93, 0.06, 0.1), hot = vec3(1.0, 0.72, 0.68);
	vec3 st = mix(black, deep, smoothstep(0.06, 0.22, l));
	st = mix(st, red, dots * smoothstep(0.12, 0.35, l));
	st = mix(st, hot, smoothstep(0.78, 0.96, l));
	vec2 d = (uv - focus) * vec2(res.x / res.y, 1.0);
	float r = length(d);
	float a = (atan(d.y, d.x) + 3.14159) / 6.28318;
	float step2 = floor(clock * 15.0);
	float slot = floor(a * 220.0);
	float pick = hash(vec2(slot, step2 + seed));
	float wid = 0.25 + 0.5 * hash(vec2(slot + 7.0, step2));
	float inSlot = abs(fract(a * 220.0) - 0.5) * 2.0;
	float start = 0.16 + 0.35 * hash(vec2(slot + 3.0, step2));
	float line = step(0.62, pick) * step(inSlot, wid * smoothstep(start, start + 0.5, r)) * smoothstep(start, start + 0.08, r);
	st = mix(st, black, line * 0.92);
	st *= 1.0 - 0.55 * smoothstep(0.55, 1.15, r);
	return st;
}
void fragment() {
	vec3 src = texture(screen_tex, SCREEN_UV).rgb;
	vec3 col = src;
	// 劈开：上半往右上、下半往左下飞，中间露出原画面；撕口白纸边 + 黑描边（和眼神特写同一条线）
	vec2 off = tearOff();
	vec2 uU = UV - off, uL = UV + off;
	float lU = tearLine(uU.x), lL = tearLine(uL.x);
	if (uU.y < lU) {
		col = stage(SCREEN_UV - off, uU);
		if (uU.y > lU - tearPaper(uU.x)) col = vec3(0.985);
		else if (uU.y > lU - tearPaper(uU.x) - 0.004 * step(0.0005, split)) col = vec3(0.02);
	} else if (uL.y > lL) {
		col = stage(SCREEN_UV + off, uL);
		if (uL.y < lL + tearPaper(uL.x + 7.0)) col = vec3(0.985);
		else if (uL.y < lL + tearPaper(uL.x + 7.0) + 0.004 * step(0.0005, split)) col = vec3(0.02);
	}
	col = mix(src, col, amount);
	col = mix(col, vec3(1.0), flash);
	COLOR = vec4(col, 1.0);
}" };

	/// <summary>放一次召唤舞台。back = 背景特效层；head = Joker 头部（屏幕坐标），集中线的中心。</summary>
	public static void Play(Control back, Vector2 head)
	{
		Vector2 screen = Fx.Screen(back);
		Node2D root = Fx.Root(back, Vector2.Zero, "P5SummonStage", Length + 0.1);
		root.GlobalPosition = Vector2.Zero;
		back.MoveChild(root, back.GetChildCount() - 1);
		ColorRect rect = new() { Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Shader } };
		root.AddChild(rect);
		ShaderMaterial m = (ShaderMaterial)rect.Material;
		m.SetShaderParameter("res", screen);
		m.SetShaderParameter("focus", head / screen);
		m.SetShaderParameter("seed", Fx.Rand(0f, 100f));
		TearSeed = Fx.Rand(0f, 100f);
		m.SetShaderParameter("tseed", TearSeed);
		Fx.Clock(rect);
		Fx.Shake(3);
		bool torn = false;
		Fx.Animate(root, 0, Length, t =>
		{
			float s = t * Length;
			float e = Mathf.Clamp((s - CutIn.SplitAt) / CutIn.SplitTime, 0f, 1f);
			m.SetShaderParameter("amount", Mathf.Min(1f, s / 0.04f) * (1f - Mathf.Clamp((e - 0.75f) / 0.25f, 0f, 1f)));
			m.SetShaderParameter("split", SplitCurve(e));
			// 白闪：施法那一下、图砸进来那一下、劈开那一下
			float fl = 0.95f * Mathf.Exp(-s / 0.045f)
				+ (s >= CutIn.ImpactAt ? 0.5f * Mathf.Exp(-(s - CutIn.ImpactAt) / 0.04f) : 0f)
				+ (e > 0f ? 0.35f * Mathf.Exp(-(s - CutIn.SplitAt) / 0.05f) : 0f);
			m.SetShaderParameter("flash", fl);
			if (!torn && e > 0f)
			{
				torn = true;
				Fx.Shake(3);
			}
		});
	}
}
