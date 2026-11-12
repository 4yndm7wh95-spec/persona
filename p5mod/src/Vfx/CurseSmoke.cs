using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 咒烟（2026-11-12 技能牌重做的共用材料，用户：之前的特效细线圆圈像界面图标、颜色各做各的 → 统一用群蛇那种暗红咒烟）：
/// 一团翻滚的近黑烟，边上透一点暗红，长大、往上飘、淡掉。和 SerpentMantle 的咒烟团同一个配方，可以压扁贴地（squash）。
/// Sweep：贴地的一道弧形烟带（扫腿）。全部着色器，普通混合（暗的烟要能压暗背景）。
/// </summary>
public static class CurseSmoke
{
	private const string Noise = @"
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { return vn(p) * 0.5 + vn(p * 2.03 + 5.1) * 0.3 + vn(p * 4.1 + 1.7) * 0.2; }
";

	private static Shader? _blob, _sweep;

	private static Shader Blob => _blob ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float prog = 0.0;     // 0 → 1：一生
uniform float grow = 0.35;    // 前多少长到最大
uniform float seed = 0.0;
uniform float squash = 1.0;   // < 1 压扁（贴地的一摊）
uniform float dens = 0.92;
uniform float rim = 1.0;      // 边上暗红的多少
uniform float hold = 0.4;     // 长到最大后保持多久（占一生的比例）才开始淡
" + Noise + @"
void fragment() {
	vec2 dv = (UV - 0.5) * 2.0;
	dv.y /= squash;
	float k = prog;
	float g = 1.0 - pow(1.0 - clamp(k / grow, 0.0, 1.0), 3.0);
	float d = length(dv);
	float pn = fbm(dv * 2.2 + vec2(seed, -k * 1.4 + seed * 0.3));
	float pf = vn(dv * 6.0 - k * 3.0 + seed);
	float R = 0.35 + 0.6 * g;
	float fade = 1.0 - smoothstep(grow + hold * (1.0 - grow), 1.0, k);
	float body = smoothstep(R, R * 0.25, d + (pn - 0.5) * 0.55 + (pf - 0.5) * 0.12) * fade;
	vec3 c = mix(vec3(0.035, 0.007, 0.012), vec3(0.16, 0.03, 0.04), smoothstep(0.35, 0.8, pn));
	float shell = smoothstep(R * 0.45, R * 0.85, d) * (1.0 - smoothstep(R * 0.85, R, d + (pn - 0.5) * 0.4));
	c += vec3(0.5, 0.05, 0.07) * shell * smoothstep(0.55, 0.85, pf) * rim;
	float a = clamp(body, 0.0, 1.0) * dens;
	if (a <= 0.003) discard;
	COLOR = vec4(c * a, a);
}" };

	private static Shader Sweep => _sweep ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float head = 0.0;     // 弧的头扫到哪（0 → 1）
uniform float tail = 0.0;     // 尾巴跟到哪
uniform float fade = 1.0;
uniform float facing = 1.0;
uniform float seed = 0.0;
" + Noise + @"
void fragment() {
	// 画布：宽 2、高 1 的一块，弧是一个压扁的半椭圆（贴地），从 Joker 那侧扫到另一侧
	vec2 p = (UV - vec2(0.5, 0.75)) * vec2(2.0, 1.0);
	p.x *= facing;
	vec2 q = vec2(p.x, p.y / 0.42);
	float r = length(q);
	float ang = atan(-q.y, -q.x);                       // 0 = Joker 那侧，π = 另一侧（只要上半圈）
	float s = ang / 3.14159265;
	float onArc = step(0.0, -q.y + 0.25);
	float band = 1.0 - smoothstep(0.14, 0.3, abs(r - 0.78 + (fbm(vec2(s * 6.0 - head * 3.0, seed)) - 0.5) * 0.18));
	float along = smoothstep(tail - 0.02, tail + 0.15, s) * (1.0 - smoothstep(head - 0.03, head + 0.01, s));
	float thick = mix(0.35, 1.0, smoothstep(tail, head, s));
	float n = fbm(vec2(s * 9.0 - head * 6.0, r * 5.0) + seed);
	float a = band * along * onArc * thick * smoothstep(0.15, 0.5, n + 0.25) * fade;
	// 前沿一道红、身子是黑烟
	float lead = smoothstep(head - 0.18, head, s);
	vec3 c = mix(vec3(0.05, 0.01, 0.015), vec3(0.92, 0.08, 0.12), lead);
	c = mix(c, vec3(1.0, 0.85, 0.82), lead * lead * smoothstep(0.75, 0.95, n) * 0.6);
	if (a <= 0.003) discard;
	COLOR = vec4(c * a, a);
}" };

	/// <summary>
	/// 一团咒烟：at 中心（层坐标），size 直径（像素），life 秒；rise = 一生里往上飘多少像素；squash &lt; 1 压扁。
	/// 返回根节点（可以自己再挪位置）。
	/// </summary>
	public static Node2D Puff(Control layer, Vector2 at, float size, double life, float delay = 0f, float rise = 0f, float squash = 1f,
		float grow = 0.35f, float hold = 0.4f, float rim = 1f, float dens = 0.92f, Vector2? drift = null)
	{
		Node2D root = Fx.Root(layer, at, "P5CurseSmoke", delay + life + 0.1);
		root.Visible = false;
		ShaderMaterial m = new() { Shader = Blob };
		m.SetShaderParameter("seed", (float)GD.RandRange(0.0, 50.0));
		m.SetShaderParameter("squash", squash);
		m.SetShaderParameter("grow", grow);
		m.SetShaderParameter("hold", hold);
		m.SetShaderParameter("rim", rim);
		m.SetShaderParameter("dens", dens);
		Vector2 sz = new(size, size);
		root.AddChild(new ColorRect { Size = sz, Position = -sz / 2f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
		Vector2 move = (drift ?? Vector2.Zero) + new Vector2(0f, -rise);
		Fx.Animate(root, delay, life, t =>
		{
			root.Visible = true;
			m.SetShaderParameter("prog", t);
			root.GlobalPosition = at + move * Fx.OutCubic(t);
		});
		return root;
	}

	/// <summary>贴地的弧形烟带扫过 at（层坐标，敌人脚下）：width 宽，seconds 扫完，facing 从哪边扫过来。</summary>
	public static void SweepArc(Control layer, Vector2 at, float width, float facing, float seconds = 0.24f, float delay = 0f)
	{
		Vector2 size = new(width * 2f, width);
		Node2D root = Fx.Root(layer, at, "P5CurseSweep", delay + seconds + 0.5);
		root.Visible = false;
		ShaderMaterial m = new() { Shader = Sweep };
		m.SetShaderParameter("facing", facing);
		m.SetShaderParameter("seed", (float)GD.RandRange(0.0, 50.0));
		root.AddChild(new ColorRect { Size = size, Position = new Vector2(-size.X / 2f, -size.Y * 0.75f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
		double life = seconds + 0.35;
		Fx.Animate(root, delay, life, t =>
		{
			root.Visible = true;
			float s = t * (float)life;
			m.SetShaderParameter("head", Fx.OutCubic(Mathf.Clamp(s / seconds, 0f, 1f)) * 1.05f);
			m.SetShaderParameter("tail", Mathf.Clamp((s - seconds * 0.45f) / (seconds * 0.9f), 0f, 1f) * 1.05f);
			m.SetShaderParameter("fade", 1f - Mathf.Clamp((s - seconds) / 0.35f, 0f, 1f));
		});
	}

	/// <summary>把 tint 从白慢慢变到 to、停 hold 秒、再变回来（Joker / 敌人发暗）。</summary>
	public static void Darken(Node anchor, Action<Color>? tint, Color to, float inTime, float hold, float outTime, float delay = 0f)
	{
		if (tint == null) return;
		float life = inTime + hold + outTime;
		Fx.Animate(anchor, delay, life, t =>
		{
			float s = t * life;
			float k = s < inTime ? Fx.OutCubic(s / inTime) : s < inTime + hold ? 1f : 1f - Fx.OutCubic((s - inTime - hold) / outTime);
			tint(Colors.White.Lerp(to, k));
		});
	}
}
