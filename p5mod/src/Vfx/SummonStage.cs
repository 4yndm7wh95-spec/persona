using Godot;

namespace P5Joker;

/// <summary>
/// Persona 召唤的「舞台」（P5 的冲击力来自整屏的平面语言，而不是贴在人身边的光）。全屏着色器放在背景特效层（人物、敌人后面），读后面的画面：
/// · 入场（2026-11-01 重做）：5 条斜切的红色纸条左右交替「啪啪」拍进来（每条 0.035s 错开，0.1s 滑到位、缓出），
///   前沿是白纸边 + 黑描边；铺满后背景是红黑高反差 + 红网点 + 从 Joker 头部放射的集中线。原版眼神特写在上面撕开。
/// · 退场：眼神特写像玻璃一样碎掉（CutIn.Shatter），背景同时白闪着退掉。
/// 全部实时渲染，不用贴图。
/// </summary>
public static class SummonStage
{
	public static float Length => CutIn.SplitAt + 0.25f;

	/// <summary>这一次召唤的碎法（舞台和眼神特写共用）。</summary>
	public static float TearSeed;

	private static Shader? _shader;

	private static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec2 res = vec2(1920.0, 1080.0);
uniform vec2 focus = vec2(0.25, 0.45);
uniform float intro = 1.0;      // 入场后几秒（纸条一条条拍进来）
uniform float amount = 1.0;
uniform float flash = 0.0;
uniform float clock = 0.0;
uniform float seed = 0.0;
" + CutIn.ShatterGlsl + @"
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
	float step2 = floor(clock * 7.5);
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
// 入场纸条：这一点盖上了吗（1 盖上，0.5 = 前沿白纸边，0.25 = 前沿黑描边，0 = 还没盖）
float slat(vec2 uv) {
	float asp = res.x / res.y;
	vec2 q = vec2((uv.x - 0.5) * asp, uv.y - 0.5);
	vec2 dir = normalize(vec2(1.0, -0.28)), n = vec2(-dir.y, dir.x);
	float a = dot(q, dir), b = dot(q, n);
	float i = clamp(floor((b + 0.66) / 0.264), 0.0, 4.0);
	float p = clamp((intro - i * 0.035) / 0.1, 0.0, 1.0);
	p = 1.0 - pow(1.0 - p, 4.0);                                     // 缓出：猛地滑进来、最后轻轻停住
	float jag = (ln(b * 40.0 + i * 7.0) - 0.5) * 0.05 + pow(ln(b * 9.0 + i * 3.0), 5.0) * 0.06;
	float edge = mod(i, 2.0) < 0.5 ? mix(-1.25, 1.25, p) + jag - a : a - (mix(1.25, -1.25, p) - jag);
	if (p >= 1.0 || edge > 0.016) return edge > 0.0 ? 1.0 : 0.0;
	return edge > 0.006 ? 0.5 : edge > 0.0 ? 0.25 : 0.0;
}
void fragment() {
	vec3 src = texture(screen_tex, SCREEN_UV).rgb;
	vec3 col = src;
	vec2 s0; float back, edge;
	if (shat < 0.0) {
		float sl = slat(UV);
		if (sl > 0.9) col = stage(SCREEN_UV, UV);
		else if (sl > 0.4) col = vec3(0.985);
		else if (sl > 0.1) col = vec3(0.02);
	} else if (shatterSample(UV, s0, back, edge)) {
		col = back > 0.5 ? vec3(0.82, 0.8, 0.78) : stage(s0, s0);
		if (edge < 0.004) col = vec3(0.985);
		else if (edge < 0.007) col = vec3(0.02);
	}
	col = mix(src, col, amount);
	col = mix(col, vec3(1.0), flash);
	COLOR = vec4(col, 1.0);
}" };

	/// <summary>
	/// 召唤高潮那一拍（2026-11-01，学结城理 mod 召唤器扣扳机：所有东西压在同一帧）：人格面具出手 = 眼神特写碎开 = 伤害结算。
	/// 顿帧 90ms、强震屏、舞台白闪；从人格面具胸口扩出大水波纹（召唤器枪口那一圈）；
	/// 声音：暴击 + 合成低频重击（玻璃碎裂由 CutIn.Shatter 放）。front = 前景层，mask = 面具位置。
	/// </summary>
	/// <summary>拍 1「召唤」在舞台开始后几秒：眼神特写撕开（ImpactAt 0.2）之后停 0.32s。</summary>
	public const float EvokeAt = 0.52f;

	/// <summary>
	/// 拍 1「召唤」（标杆 1，2026-11-02）：照结城理 mod 召唤器扣扳机原样移植 —— 顿帧 90ms、强震屏、从 Joker 的面具朝人格面具
	/// 一道贯穿光 + 出口玻璃龟裂 + 锥形玻璃 + 出口环 + 大折射波 + 蓝焰升腾 + 黑羽（Evoke.cs）；眼神特写同时碎开（CutIn.Shatter）。
	/// 声音：暴击 + 低频（玻璃声在 CutIn.Shatter）。mask = Joker 的面具，persona = 人格面具胸口（光射向哪）。
	/// </summary>
	public static void Beat(Control front, Vector2 mask, Control? back = null, float facing = 1f, Vector2? persona = null)
	{
		HitStop.Request(0.09);
		Fx.Shake(3);
		Vector2 dir = persona is Vector2 p ? p - mask : new Vector2(-facing, -0.3f);
		Evoke.Fire(front, back ?? front, mask, dir, 1f);
		// 碎玻璃这一拍（用户 2026-11-03：暴击音在后面毒爆里又出现，像是同一下）：不用暴击音，换成游戏自带的玻璃音效
		// （缺陷者玻璃球，P3 mod 召唤器碎玻璃用的就是它）叠在 CutIn 的玻璃碎裂上；暴击音只留给毒爆等收尾的大拍
		// 玻璃声在 CutIn 的碎裂里（P3 召唤器那一枪），这里不再叠游戏自带的玻璃音，免得糊成一团
		Sfx.Play(Sfx.SubBoom, 1f);
		Quiet = Stage.NowMs() + 400;
	}

	/// <summary>到这个时刻（毫秒）为止是召唤高潮那一拍：其他特效的冲击声别再叠（CardFx.CurseHands 等看它）。</summary>
	public static ulong Quiet;

	/// <summary>放一次召唤舞台。back = 背景特效层；front = 最前面一层；head = Joker 头部（屏幕坐标），集中线的中心。</summary>
	public static void Play(Control back, Control front, Vector2 head, bool maskFlame = true, CutInPlayback? playback = null)
	{
		playback ??= new CutInPlayback(CutIn.SplitAt);
		float split = playback.SplitAt, length = split + 0.25f;
		float crackDelay = playback.Crush ? 0f : 0.05f;
		Vector2 screen = Fx.Screen(back);
		TearSeed = playback.Seed;
		Node2D root = Fx.ScreenRoot(back, "P5SummonStage");
		back.MoveChild(root, back.GetChildCount() - 1);
		// Snapshot the room *here*: an earlier screen-reading effect may have cached
		// a clear/black buffer before the room background was drawn this frame.
		root.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		ColorRect rect = new() { Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Shader } };
		root.AddChild(rect);
		ShaderMaterial m = (ShaderMaterial)rect.Material;
		m.SetShaderParameter("res", screen);
		m.SetShaderParameter("sres", screen);
		m.SetShaderParameter("focus", Fx.ScreenPoint(back, head) / screen);
		m.SetShaderParameter("seed", Fx.Rand(0f, 100f));
		m.SetShaderParameter("tseed", playback.Seed);
		Fx.Clock(rect);
		if (maskFlame)
		{
			Sfx.Play(Sfx.PersonaFlame, 0.9f);                   // 面具蓝焰一炸
		}
		Sfx.Play(Sfx.CutIn, 0.8f, 0.03f);                       // 纸条拍进来
		bool broke = false;
		Fx.Play(root, playback, length, s =>
		{
			m.SetShaderParameter("intro", s);
			// 背景：横幅碎开（裂纹定格 0.05s 之后）那一下白闪着退掉，0.12s 缓出
			float gone = Mathf.Clamp((s - split - crackDelay) / 0.12f, 0f, 1f);
			m.SetShaderParameter("amount", 1f - (1f - Mathf.Pow(1f - gone, 3f)));
			// 白闪：纸条拍进来那一下（弱）、爪痕划完那一下
			float fl = 0.12f * Mathf.Exp(-s / 0.05f) + (s >= split + crackDelay ? 0.6f * Mathf.Exp(-(s - split - crackDelay) / 0.06f) : 0f);
			m.SetShaderParameter("flash", fl);
			if (!broke && s >= split + crackDelay)
			{
				broke = true;
				Fx.Shake(2);                                       // 整段召唤只在玻璃碎开这一下轻震一次
			}
		});
	}
}
