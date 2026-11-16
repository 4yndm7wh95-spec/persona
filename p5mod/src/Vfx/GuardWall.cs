using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 防御（用户 2026-11-08）：方向 A 的爆炸形看不出和防御有什么关系 → 改成「一面竖着的盾墙挡在 Joker 和敌人之间」，多用着色器渲染。
/// 整面墙是一个着色器（一块四边形），逐像素算：
/// · 形状：略微前倾的竖板，朝敌人那面是平直硬边（挡住的一面），朝 Joker 那面是锯齿撕边，上下前角切掉（P5 的斜角）；
/// · 套印：黑底按偏移再算一次形状，和红面错开几个像素；
/// · 红面：屏幕空间网点（靠 Joker 那侧、下方更深）+ 慢慢流动的斜条纹；白色内描边；正面一道粗白边，撞上那一下变宽变亮；
/// · 受力：撞上的那一下从正面中间荡开一圈白色波纹；
/// · 出现：从撕边往正面「撕」出来（锯齿状的显露边）；锯齿每 1/15 秒抖一次（一拍二的手绘感）。
/// 代码只管运动：从拳头甩出去「哐」地立在身前（压扁回弹）→ 火花往敌人那边弹开 → 停一下 → 缩成小盾飞进血条上的格挡数字，到了闪一下。
/// 不碎（碎 = 防御被打破，留给以后「格挡被打穿」用）。整段约 0.6s，不顿帧、不震屏，声音用原版格挡音。
/// </summary>
public static class GuardWall
{
	private static Shader? _shader, _glowShader;

	/// <summary>盾墙的距离场，面和辉光两个着色器共用。</summary>
	private const string Sdf = @"
uniform float aspect = 0.62;
uniform float facing = 1.0;
uniform float boil = 0.0;
float hash(float n) { return fract(sin(n * 12.9898 + 4.1) * 43758.5453); }
// 撕边：大小不一的三角齿（尖、不规则），齿上再有细小的崩口
float jag(float y, float s) {
	float k = y * 4.6 + sin(y * 2.3 + s * 1.7) * 0.9 + sin(y * 6.1 + s) * 0.35 + s * 7.31;   // 齿宽不等
	float i = floor(k), f = fract(k);
	float h = 0.35 + 0.65 * hash(i + s * 3.7);
	float peak = 0.25 + 0.5 * hash(i * 1.7 + s);
	float tri = f < peak ? f / peak : (1.0 - f) / (1.0 - peak);
	float k2 = y * 17.0 + s * 3.1;
	float nick = (0.5 - abs(fract(k2) - 0.5)) * hash(floor(k2) + s * 9.1);
	return tri * h * 0.2 + nick * 0.035;
}
// <0 在里面。x 朝敌人。grow = 往外扩
float wall(vec2 p, float s, float grow) {
	vec2 q = vec2(p.x - p.y * 0.12, p.y);
	float hw = 0.3 + grow, hh = 0.92 + grow;
	float front = q.x - hw;
	float back = -(q.x + hw + 0.06 - jag(q.y, s));
	float tb = abs(q.y) - hh;
	float ch = (q.x * 0.7 + abs(q.y)) - (hh + hw * 0.7 - 0.16);
	return max(max(front, back), max(tb, ch));
}
";

	/// <summary>墙后面的加法辉光（同一个距离场往外衰减；撞上时亮）。四边形比墙大一圈：pad = 每边多出的比例。</summary>
	private static Shader GlowShader => _glowShader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float pad = 0.35;
uniform float glow = 0.3;
" + Sdf + @"
void fragment() {
	vec2 p = ((UV - 0.5) * (1.0 + 2.0 * pad)) * 2.0;
	p.x *= aspect * facing;
	float d = max(wall(p, boil, 0.0), 0.0);
	float g = exp(-d * 9.0) * glow;
	COLOR = vec4(vec3(1.0, 0.08, 0.16) * g, 1.0);
}" };

	private static Shader WallShader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform float appear = 1.0;
uniform float flash = 0.0;
uniform float ripple = -1.0;
uniform vec2 ripple_at = vec2(0.3, 0.05);
uniform float shine = -1.0;
uniform float dot_px = 7.0;
uniform float alpha = 1.0;
uniform float clock = 0.0;
uniform vec2 misreg = vec2(0.035, 0.03);
" + Sdf + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	p.x *= aspect * facing;
	float s = boil;
	float dB = wall(p - misreg * vec2(facing, 1.0), s, 0.05);
	float dF = wall(p, s, 0.0);
	float mB = 1.0 - smoothstep(-fwidth(dB), fwidth(dB), dB);
	float mF = 1.0 - smoothstep(-fwidth(dF), fwidth(dF), dF);
	// 厚度：整块往敌人那边、往下错一点再算一次，露出来的那一条是暗红的侧面
	float dS = wall(p - vec2(0.065, -0.03), s, 0.0);
	float mS = 1.0 - smoothstep(-fwidth(dS), fwidth(dS), dS);
	// 显露：从撕边（-x）往正面（+x）撕出来，显露边本身也是锯齿
	float edge = mix(-0.7, 0.75, appear) + jag(p.y * 1.4, s + 5.0) * 0.5 - 0.05;
	float shown = 1.0 - smoothstep(edge - 0.01, edge + 0.01, p.x);
	// 红面：网点（屏幕像素、转 45°），靠 Joker 那侧和下面更深
	float g = clamp(0.15 + 0.75 * smoothstep(0.3, -0.45, p.x) + 0.25 * smoothstep(-0.2, 0.9, p.y), 0.0, 1.0);
	vec2 r = mat2(vec2(0.7071, -0.7071), vec2(0.7071, 0.7071)) * FRAGCOORD.xy / dot_px;
	float dd = length(fract(r) - 0.5), rad = 0.6 * g;
	float dots = 1.0 - smoothstep(rad - fwidth(dd), rad + fwidth(dd), dd);
	vec3 red = vec3(0.9, 0.03, 0.09), deep = vec3(0.3, 0.0, 0.04), white = vec3(1.0, 0.97, 0.95), black = vec3(0.035, 0.02, 0.03);
	vec3 col = mix(red, deep, dots);
	// 斜条纹慢慢往上流（P5 UI 的那种细斜线）
	float stripe = step(0.9, fract((p.x * 0.8 + p.y) * 7.0 - clock * 1.6));
	col = mix(col, vec3(1.0, 0.35, 0.4), stripe * 0.35);
	// 白色内描边
	float inner = 1.0 - smoothstep(0.006, 0.006 + fwidth(dF) * 1.5, abs(dF + 0.055));
	col = mix(col, white, inner);
	// 正面一道粗白边（撞上时变宽变亮）
	vec2 q = vec2(p.x - p.y * 0.12, p.y);
	float bar = 0.03 + 0.07 * flash;
	float front = 1.0 - smoothstep(bar - fwidth(q.x), bar + fwidth(q.x), 0.3 - q.x);
	col = mix(col, white, front);
	// 受力的波纹：从正面中间荡开
	if (ripple >= 0.0) {
		float rr = ripple * 3.2;
		float ring = 1.0 - smoothstep(0.0, 0.035, abs(length(p - ripple_at) - rr));
		col = mix(col, white, ring * clamp(1.0 - ripple / 0.28, 0.0, 1.0));
	}
	// 撞上之后一道白光从撕边扫到正面
	if (shine >= 0.0) {
		float band = (p.x * 0.75 + p.y * 0.35) - mix(-1.0, 1.0, shine);
		float sh = 1.0 - smoothstep(0.0, 0.05, abs(band));
		float soft = exp(-abs(band) * 9.0) * 0.25;
		col = mix(col, white, clamp(sh * 0.7 + soft, 0.0, 1.0));
	}
	col = mix(col, white, flash * 0.3);
	vec3 side = vec3(0.32, 0.0, 0.05) * (0.85 + 0.15 * sin(p.y * 40.0));
	vec3 outc = mF > 0.5 ? col : (mS > 0.5 ? side : black);
	float a = max(max(mB, mF), mS) * shown * alpha;
	COLOR = vec4(outc, a);
}" };

	/// <param name="layer">特效层（前景）</param>
	/// <param name="fist">护身的拳头（全局坐标）：盾墙从这里甩出去</param>
	/// <param name="height">Joker 身高（屏幕像素）</param>
	/// <param name="block">血条上格挡数字的位置（全局坐标）：最后缩成小盾飞进去；null = 原地缩没</param>
	/// <param name="amount">格挡值：10 点以上墙更大、火花更多</param>
	/// <param name="onArrive">飞到格挡数字那一刻（游戏里让格挡数字跳一下）</param>
	public static void Play(Control layer, Vector2 fist, float height, float facing = 1f, int amount = 5, Vector2? block = null, Action? onArrive = null)
	{
		bool big = amount >= 10;
		float H = height * (big ? 0.95f : 0.82f), W = H * 0.62f;
		Vector2 center = fist + new Vector2(height * 0.36f * facing, height * 0.2f);
		Vector2 target = block ?? center;
		float unit = Fx.Screen(layer).Y / 1080f;
		const float fly = 0.08f, holdEnd = 0.34f, gather = 0.2f, life = holdEnd + gather + 0.1f;

		Node2D root = Fx.Root(layer, fist, "P5GuardWall", life + 0.1);
		ShaderMaterial m = new() { Shader = WallShader };
		m.SetShaderParameter("aspect", W / H);
		m.SetShaderParameter("facing", facing);
		m.SetShaderParameter("dot_px", 7f * unit);
		m.SetShaderParameter("appear", 0f);
		ColorRect rect = new() { Size = new Vector2(W, H), Position = new Vector2(-W, -H) / 2f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(rect);
		Fx.Clock(rect);
		// 墙后面一层加法辉光（同一个距离场往外衰减，撞上那一下亮）
		const float pad = 0.35f;
		ShaderMaterial gm = new() { Shader = GlowShader };
		gm.SetShaderParameter("aspect", W / H);
		gm.SetShaderParameter("facing", facing);
		gm.SetShaderParameter("pad", pad);
		gm.SetShaderParameter("glow", 0f);
		Vector2 gsize = new Vector2(W, H) * (1f + 2f * pad);
		ColorRect glowRect = new() { Size = gsize, Position = -gsize / 2f, Material = gm, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(glowRect);
		root.MoveChild(glowRect, 0);

		bool slammed = false, arrived = false;
		int boil = -1;
		Vector2 frontMid = new(W * 0.48f * facing, 0f);
		Fx.Animate(root, 0, life, t =>
		{
			float s = t * life;
			int step = (int)(s * 15f);
			if (step != boil)
			{
				boil = step;
				m.SetShaderParameter("boil", (float)(step % 97));
				gm.SetShaderParameter("boil", (float)(step % 97));
			}
			if (s < fly)
			{
				// 从拳头甩出去：边飞边从撕边那头撕出来，带倾斜
				float k = Fx.OutCubic(s / fly);
				root.GlobalPosition = fist.Lerp(center, k);
				root.Scale = new Vector2(Mathf.Lerp(0.35f, 1.05f, k), Mathf.Lerp(0.5f, 1.08f, k));
				root.Skew = -0.35f * facing * (1f - k);
				m.SetShaderParameter("appear", Mathf.Clamp(s / fly * 1.1f, 0f, 1f));
			}
			else if (s < holdEnd)
			{
				float u = s - fly;
				if (!slammed)
				{
					slammed = true;
					m.SetShaderParameter("appear", 1f);
					Vector2 hit = center + frontMid;
					// 火花往敌人那边弹开（像有东西撞上来被挡回去）+ 墙脚两道落地冲击线
					Glow.Sparks(layer, hit, big ? 26 : 16, 300f, 950f, 0.32f, new[] { new Color(1f, 0.97f, 0.95f), new Color(1f, 0.25f, 0.3f) }, 500f,
						0.05f, 0.16f, new Vector2(facing, -0.15f), 38f);
					Glow.Flash(layer, hit, new Color(1f, 0.85f, 0.85f), H * 0.55f, 0.14f);
					Glow.Sparks(layer, center + new Vector2(0f, H * 0.45f), 10, 150f, 420f, 0.25f, new[] { new Color(1f, 0.6f, 0.62f) }, 300f,
						0.04f, 0.1f, new Vector2(0f, -1f), 80f);
				}
				// 「哐」：压扁再弹回；正面白边亮一下；波纹荡开；之后轻轻呼吸
				float squash = Mathf.Exp(-u / 0.05f) * Mathf.Cos(u * 45f);
				root.GlobalPosition = center;
				root.Skew = 0f;
				root.Scale = new Vector2(1f + 0.1f * squash, 1f - 0.12f * squash) * (1f + 0.012f * Mathf.Sin(u * 25f));
				m.SetShaderParameter("flash", Mathf.Exp(-u / 0.06f));
				m.SetShaderParameter("ripple", u);
				gm.SetShaderParameter("glow", 1.1f * Mathf.Exp(-u / 0.08f) + 0.25f);
				m.SetShaderParameter("shine", Mathf.Clamp((u - 0.03f) / 0.16f, 0f, 1.2f));
			}
			else
			{
				// 缩成小盾，沿弧线飞进格挡数字（先慢后快）
				float k = Mathf.Clamp((s - holdEnd) / gather, 0f, 1f), e = k * k * (3f - 2f * k), ein = k * k;
				Vector2 mid = (center + target) * 0.5f + new Vector2(-height * 0.15f * facing, -height * 0.1f);
				Vector2 a = center.Lerp(mid, ein), b = mid.Lerp(target, ein);
				root.GlobalPosition = a.Lerp(b, ein);
				root.Scale = Vector2.One * Mathf.Lerp(1f, block != null ? 0.12f : 0.02f, e);
				root.Rotation = -0.25f * facing * e;
				m.SetShaderParameter("ripple", -1f);
				m.SetShaderParameter("flash", 0.4f * e);
				gm.SetShaderParameter("glow", 0.25f * (1f - e));
				m.SetShaderParameter("shine", -1f);
				if (k >= 1f && !arrived)
				{
					arrived = true;
					rect.Visible = false;
					glowRect.Visible = false;
					if (block != null)
					{
						Glow.Flash(layer, target, new Color(1f, 0.92f, 0.9f), height * 0.35f, 0.16f);
						Glow.Sparks(layer, target, 10, 120f, 360f, 0.25f, new[] { new Color(1f, 0.97f, 0.95f), new Color(1f, 0.3f, 0.35f) }, 0f, 0.04f, 0.1f);
						onArrive?.Invoke();
					}
				}
			}
		});
	}
}
