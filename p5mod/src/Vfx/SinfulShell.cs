using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace P5Joker;

/// <summary>
/// 华丽收场 = 撒旦耶尔的大罪穿甲弹（用户 2026-11-08，参考 art/p5/fx/v4/satanael/ref_*.png）。全部是着色器实时渲染，不画贴图：
/// ① 蓝焰从 Joker 脚下往外烧，烧到哪里战斗背景就被烧穿，露出夜空场景（NightScene：夜空、多层翻滚的乌云、云底红光、远处闪电、两边断崖、中间深渊雾）；
/// ② （撒旦耶尔从断崖下升起——等第 2 版立绘，这里先空着）；
/// ③ 举枪蓄力：枪口一团往里吸的能量漩涡（Vortex），场景压暗；
/// ④ 开枪：全屏白闪 + 顿帧 + 枪口一串冲击环（Rings）+ 大罪穿甲弹（Bullet：刺眼的弹芯 + 双螺旋气流 + 往后喷的等离子尾），
///    一路贯穿每个敌人（每穿过一个就在那里炸开、结算一次），从屏幕右边飞出去；
/// ⑤ 最后一个敌人被贯穿：P5 红黑白冲击帧；⑥ 场景从远处往回烧掉，露出原来的战场。
/// </summary>
public static class SinfulShell
{
	// ───────────────────────── 场景 ─────────────────────────

	private const string Noise = @"
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 5; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.7, 9.2); a *= 0.5; }
	return v;
}
";

	private static Shader? _scene, _vortex, _bullet, _ring;

	/// <summary>夜空场景。part 0 = 天空 + 深渊（最底下），part 1 = 两边断崖（盖在人格面具前面、人物后面）。</summary>
	private static Shader Scene => _scene ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec2 sres = vec2(1920.0, 1080.0);
uniform vec2 origin = vec2(430.0, 830.0);
uniform float reveal = 0.0;
uniform vec2 origin2 = vec2(1920.0, 600.0);
uniform float conceal = 0.0;
uniform int part = 0;
uniform float clock = 0.0;
uniform float dim = 0.0;
uniform float flash = 0.0;
uniform float feet_y = 830.0;
uniform float cliff_x = 640.0;
uniform float floor_y = 830.0;
uniform float cliff2_x = 1000.0;
" + Noise + @"
// 蓝焰边：烧穿的边界（d = 到边界的有向距离，>0 在场景里）
vec4 flameEdge(vec2 px, float s) {
	float t = clock;
	float w = 26.0 + 22.0 * noise(vec2(px.x * 0.02, t * 2.0));
	float tongue = fbm(vec2(px.x * 0.018, px.y * 0.012 + t * 2.4));
	float e = exp(-max(-s, 0.0) / w) * step(-w * 3.0, s) * (1.0 - smoothstep(0.0, w * 0.8, s));
	e *= 0.6 + 0.9 * tongue;
	vec3 c = e > 1.0 ? vec3(0.94, 0.98, 1.0) : e > 0.62 ? vec3(0.4, 0.8, 1.0) : e > 0.3 ? vec3(0.1, 0.38, 0.92) : vec3(0.03, 0.09, 0.32);
	float a = smoothstep(0.12, 0.16, e);
	return vec4(c * 1.2, a);
}
float revealDist(vec2 px) {
	float n = (fbm(px / 110.0 + vec2(0.0, -clock * 0.5)) - 0.5) * 160.0;
	float d = length((px - origin) * vec2(1.0, 1.25)) + n;
	float s = reveal - d;
	if (conceal > 0.0) {
		float d2 = length((px - origin2) * vec2(1.0, 1.25)) + n;
		s = min(s, d2 - conceal);
	}
	return s;
}
// 断崖：<0 在岩石里
float rock(vec2 px) {
	// 左边 Joker 脚下那块：顶面在 feet_y，右边是往下、往里收的锯齿崖面
	float topL = feet_y - 4.0 - 10.0 * fbm(vec2(px.x * 0.012, 3.1));
	float edgeL = cliff_x - (px.y - feet_y) * 0.32 + (fbm(vec2(px.y * 0.02, 7.7)) - 0.5) * 70.0;
	float l = max(topL - px.y, px.x - edgeL);
	// 右边敌人脚下那块（远一点、矮一点）
	float topR = floor_y - 2.0 - 8.0 * fbm(vec2(px.x * 0.01, 5.3));
	float edgeR = cliff2_x + (px.y - floor_y) * 0.22 + (fbm(vec2(px.y * 0.022, 2.4)) - 0.5) * 60.0;
	float r = max(topR - px.y, edgeR - px.x);
	return min(l, r);
}
void fragment() {
	vec2 px = UV * sres;
	vec2 uv = UV;
	float s = revealDist(px);
	float shown = smoothstep(-2.0, 2.0, s);
	vec4 edge = flameEdge(px, s);
	vec3 col;
	float a;
	if (part == 0) {
		// 天空：深蓝 → 地平线一圈青蓝的亮
		col = mix(vec3(0.012, 0.025, 0.07), vec3(0.04, 0.12, 0.22), smoothstep(0.0, 0.62, uv.y));
		col += vec3(0.06, 0.32, 0.42) * exp(-abs(uv.y - 0.6) * 9.0);
		// 星星（云少的上空）
		vec2 sg = floor(px / 3.0);
		col += vec3(0.7, 0.85, 1.0) * step(0.9985, hash(sg)) * smoothstep(0.45, 0.0, uv.y) * (0.6 + 0.4 * sin(clock * 3.0 + hash(sg) * 30.0));
		// 乌云：两层域扭曲噪声，横向拉长，慢慢翻滚；亮边偏蓝，云底透红光
		vec2 p = vec2(px.x / sres.y, px.y / sres.y);
		for (int k = 0; k < 2; k++) {
			float fk = float(k);
			vec2 q = vec2(p.x * (1.1 + fk * 0.5) + clock * (0.012 + fk * 0.01), p.y * (2.8 + fk));
			vec2 warp = vec2(fbm(q + vec2(clock * 0.05, 0.0)), fbm(q + vec2(4.3, clock * 0.04)));
			float c = fbm(q + warp * 1.4 + fk * 3.7);
			float band = smoothstep(0.78 - fk * 0.18, 0.12, uv.y) * smoothstep(-0.05, 0.1, uv.y);
			float dens = smoothstep(0.44, 0.78, c) * band;
			float lit = clamp((c - fbm(q + warp * 1.4 + fk * 3.7 + vec2(0.0, 0.05))) * 6.0 + 0.5, 0.0, 1.0);
			vec3 cc = mix(vec3(0.02, 0.04, 0.09), vec3(0.18, 0.34, 0.5), lit);
			cc += vec3(0.55, 0.04, 0.08) * (1.0 - lit) * smoothstep(0.15, 0.6, uv.y) * (0.6 + 0.4 * fk);
			col = mix(col, cc, dens * (0.85 - fk * 0.15));
		}
		// 远处闪电：偶尔在一片云里亮一下
		float slot = floor(clock * 2.5);
		float fl = step(0.82, hash(vec2(slot, 1.7))) * exp(-fract(clock * 2.5) * 9.0);
		vec2 lpos = vec2(hash(vec2(slot, 3.1)) * sres.x, (0.1 + 0.3 * hash(vec2(slot, 5.9))) * sres.y);
		col += vec3(0.5, 0.75, 1.0) * fl * exp(-length((px - lpos) / vec2(420.0, 220.0)));
		// 深渊：地面线以下越来越暗，底下翻上来暗红的雾
		float below = smoothstep(feet_y - 40.0, sres.y, px.y);
		float mist = fbm(vec2(px.x * 0.004 + clock * 0.03, px.y * 0.01 - clock * 0.25));
		col = mix(col, vec3(0.01, 0.015, 0.03), below * 0.85);
		col += vec3(0.5, 0.05, 0.08) * smoothstep(0.45, 0.85, mist) * below * 0.6;
		col *= 1.0 - 0.5 * dim;
		col += vec3(0.85, 0.95, 1.0) * flash;
		a = shown;
		col = mix(col, edge.rgb, edge.a);
		a = max(a, edge.a);
	} else {
		float d = rock(px);
		float inside = 1.0 - smoothstep(-1.0, 1.0, d);
		// 岩石：暗蓝黑，层理 + 裂纹；顶面一道被天光照亮的冷色边；崖面靠深渊那侧被底下的红雾映红
		float strata = fbm(vec2(px.x * 0.01, px.y * 0.05));
		float crack = smoothstep(0.02, 0.0, abs(fbm(px * 0.02) - 0.5));
		col = mix(vec3(0.02, 0.025, 0.045), vec3(0.06, 0.07, 0.1), strata) * (1.0 - 0.5 * crack);
		float topLit = smoothstep(14.0, 0.0, abs(min(px.y - (feet_y - 6.0), px.y - (floor_y - 4.0)))) * step(d, 0.0);
		col += vec3(0.25, 0.5, 0.75) * topLit * 0.5;
		float rim = smoothstep(-16.0, 0.0, d);
		col += vec3(0.45, 0.05, 0.08) * rim * smoothstep(feet_y, sres.y, px.y) * 1.2;
		col += vec3(0.3, 0.55, 0.8) * rim * smoothstep(feet_y + 60.0, feet_y, px.y) * 0.4;
		col *= 1.0 - 0.45 * dim;
		col += vec3(0.85, 0.95, 1.0) * flash * 0.6;
		a = inside * shown;
		col = mix(col, edge.rgb, edge.a * inside);
	}
	COLOR = vec4(col, a);
}" };

	// ───────────────────────── 蓄力漩涡 ─────────────────────────

	private static Shader Vortex => _vortex ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float power = 0.0;
uniform float clock = 0.0;
" + Noise + @"
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	float r = length(p), ang = atan(p.y, p.x);
	// 几条往里旋进去的螺旋臂（越近中心越亮越细），外面一层往里吸的放射线
	float arms = pow(0.5 + 0.5 * sin(ang * 4.0 + log(r + 0.02) * 9.0 + clock * 26.0), 14.0);
	float streak = pow(noise(vec2(ang * 6.0, r * 3.0 + clock * 9.0)), 6.0) * smoothstep(1.0, 0.35, r);
	float body = arms * smoothstep(1.0, 0.15, r) * smoothstep(0.0, 0.12, r);
	float core = exp(-r * 9.0) * 1.6 + exp(-r * 3.0) * 0.4;
	float a = (body * 0.9 + streak * 0.9 + core) * power * smoothstep(1.0, 0.8, r);
	vec3 c = mix(vec3(0.15, 0.45, 1.0), vec3(0.85, 0.95, 1.0), clamp(core + arms * 0.4, 0.0, 1.0));
	c += vec3(0.5, 0.2, 0.9) * streak * 0.5;
	COLOR = vec4(c * a, 1.0);
}" };

	// ───────────────────────── 子弹 ─────────────────────────

	/// <summary>沿 x 从尾（0）到头（1）。head = 弹头在四边形里的位置（尾巴比弹头先出现在枪口那头）。</summary>
	private static Shader BulletShader => _bullet ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform float clock = 0.0;
uniform float len_px = 1000.0;
uniform float thick_px = 120.0;
uniform float fade = 1.0;
uniform float tail = 1.0;
" + Noise + @"
void fragment() {
	float x = UV.x, y = (UV.y - 0.5) * 2.0;
	float xp = x * len_px;
	// 尾巴：越往后越细越淡；tail 控制尾巴长度（0～1）
	float taper = smoothstep(1.0 - tail, 1.0, x);
	float w = mix(0.08, 0.42, taper);
	// 弹芯：一条刺眼的细线，弹头是一颗拉长的亮核
	float core = exp(-pow(y / (w * 0.35), 2.0)) * taper;
	vec2 hp = vec2((x - 0.985) * len_px / thick_px * 1.4, y);
	float head = exp(-dot(hp, hp) * 3.0) * 2.2;
	// 双螺旋：两股气流绕着弹芯转（3D 螺旋投影成两条交织的正弦）
	float ph = xp / (thick_px * 0.9) - clock * 34.0;
	float s1 = abs(y - w * 0.85 * sin(ph)), s2 = abs(y - w * 0.85 * sin(ph + 3.14159));
	float front1 = 0.6 + 0.4 * cos(ph), front2 = 0.6 + 0.4 * cos(ph + 3.14159);
	float helix = (exp(-s1 / 0.035) * front1 + exp(-s2 / 0.035) * front2) * taper;
	// 往后喷的等离子：拉长的噪声，越往尾越散
	float plasma = fbm(vec2(xp * 0.012 - clock * 22.0, y * 3.0)) * exp(-pow(y / (w * 1.3), 2.0)) * taper;
	plasma = smoothstep(0.35, 0.9, plasma);
	float glow = exp(-abs(y) / (w * 0.9)) * taper * 0.35;
	vec3 white = vec3(0.95, 0.98, 1.0), cyan = vec3(0.25, 0.75, 1.0), violet = vec3(0.65, 0.3, 1.0);
	vec3 col = white * (core * 1.6 + head) + cyan * (helix * 1.3 + glow) + mix(cyan, violet, 0.5 + 0.5 * y) * plasma * 1.1;
	// 四边形边缘一律收到 0（不然弹头光晕被切成方块）
	float edge = (1.0 - smoothstep(0.75, 1.0, abs(y))) * smoothstep(0.0, 0.04, x) * (1.0 - smoothstep(0.985, 1.0, x));
	COLOR = vec4(col * fade * edge, 1.0);
}" };

	private static Shader RingShader => _ring ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 tint : source_color = vec4(0.4, 0.8, 1.0, 1.0);
uniform float radius = 0.1;
uniform float width = 0.05;
uniform float alpha = 1.0;
void fragment() {
	float d = length(UV * 2.0 - 1.0);
	float core = 1.0 - smoothstep(0.0, width, abs(d - radius));
	float glow = (1.0 - smoothstep(0.0, width * 4.0, abs(d - radius))) * 0.4;
	COLOR = vec4(tint.rgb * (1.0 + core) * (core + glow) * alpha * (1.0 - smoothstep(0.85, 1.0, d)), 1.0);
}" };

	// ───────────────────────── 时序 ─────────────────────────

	/// <summary>各拍的时刻（从出牌算起，秒）。</summary>
	public const float RevealAt = 0.05f, RevealTime = 0.7f, AimAt = 1.35f, ChargeAt = 1.5f, FireAt = 2.5f, BulletSpeed = 3600f, ExitAfter = 1.2f, ExitTime = 0.6f;

	public sealed class Setup
	{
		public Vector2 JokerFeet, JokerHead, JokerMuzzle, PersonaMuzzle;
		public float FloorY;
		public List<Vector2> Enemies = new();
		/// <summary>穿过第 i 个敌人（按离枪口由近到远）的那一刻：游戏在这里结算这个敌人的伤害。</summary>
		public Action<int>? OnPierce;
		/// <summary>Joker 该开枪的时刻（游戏里播 shiv_from_aim）：FireAt - 0.18。</summary>
		public Action? OnJokerAim, OnJokerShoot;
	}

	/// <summary>放整段大罪穿甲弹。back = 背景层（场景、人格面具），front = 前景层（子弹、命中）。返回子弹穿过每个敌人的时刻（秒，从现在算）。</summary>
	public static float[] Play(Control back, Control front, Setup s)
	{
		Vector2 screen = Fx.Screen(back);
		float unit = screen.Y / 1080f;
		Func<Vector2, Vector2> toScreen = w => Fx.ScreenPoint(back, w);
		Vector2 feetS = toScreen(s.JokerFeet), floorS = toScreen(new Vector2(0f, s.FloorY));
		var enemiesOrdered = s.Enemies.Select((e, i) => (e, i)).OrderBy(t => t.e.DistanceSquaredTo(s.PersonaMuzzle)).ToList();
		float firstEnemyX = enemiesOrdered.Count > 0 ? enemiesOrdered.Min(t => toScreen(t.e).X) : screen.X * 0.6f;

		// ── 场景：天空层（最底下）+ 断崖层（盖在人格面具前面）
		Node2D root = Fx.ScreenRoot(back, "P5SinfulShellScene");
		back.MoveChild(root, 0);
		ShaderMaterial sky = new() { Shader = Scene }, rocks = new() { Shader = Scene };
		float total = FireAt + ExitAfter + ExitTime + 0.2f;
		foreach (ShaderMaterial m in new[] { sky, rocks })
		{
			m.SetShaderParameter("sres", screen);
			m.SetShaderParameter("origin", feetS);
			m.SetShaderParameter("origin2", new Vector2(screen.X * 1.05f, screen.Y * 0.55f));
			m.SetShaderParameter("feet_y", feetS.Y);
			m.SetShaderParameter("floor_y", floorS.Y);
			m.SetShaderParameter("cliff_x", feetS.X + 210f * unit);
			m.SetShaderParameter("cliff2_x", Mathf.Max(feetS.X + 420f * unit, firstEnemyX - 160f * unit));
		}
		sky.SetShaderParameter("part", 0);
		rocks.SetShaderParameter("part", 1);
		ColorRect skyRect = new() { Size = screen, Material = sky, MouseFilter = Control.MouseFilterEnum.Ignore };
		Node2D personaSlot = new() { Name = "PersonaSlot" };          // 撒旦耶尔第 2 版立绘放这里（天空和断崖之间）
		ColorRect rockRect = new() { Size = screen, Material = rocks, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(skyRect);
		root.AddChild(personaSlot);
		root.AddChild(rockRect);
		Fx.Clock(skyRect);
		Fx.Clock(rockRect);
		float far = (screen * 1.3f).Length();
		Fx.Animate(root, 0, total, t =>
		{
			float time = t * total;
			float rv = Mathf.Clamp((time - RevealAt) / RevealTime, 0f, 1f);
			float reveal = far * (1f - Mathf.Pow(1f - rv, 2.2f));
			float ex = Mathf.Clamp((time - FireAt - ExitAfter) / ExitTime, 0f, 1f);
			float conceal = ex <= 0f ? 0f : far * ex * ex;
			float dim = Mathf.Clamp((time - ChargeAt) / (FireAt - ChargeAt), 0f, 1f) * (time < FireAt ? 1f : Mathf.Exp(-(time - FireAt) / 0.4f));
			float flash = time >= FireAt ? Mathf.Exp(-(time - FireAt) / 0.07f) : 0f;
			foreach (ShaderMaterial m in new[] { sky, rocks })
			{
				m.SetShaderParameter("reveal", reveal);
				m.SetShaderParameter("conceal", conceal);
				m.SetShaderParameter("dim", dim);
				m.SetShaderParameter("flash", flash);
			}
		});
		Fx.FreeAfter(root, total + 0.1);
		// 开场：Joker 面具上的蓝焰一炸（烧穿从这里开始）
		Glow.Flash(front, s.JokerHead, new Color(0.4f, 0.8f, 1f), 260f * unit, 0.3f);
		Glow.Sparks(front, s.JokerFeet, 40, 200f, 700f, 0.7f, new[] { new Color(0.9f, 0.97f, 1f), new Color(0.3f, 0.7f, 1f), new Color(0.1f, 0.3f, 0.9f) },
			-200f, 0.06f, 0.2f, Vector2.Up, 70f);
		Sfx.Play(Sfx.PersonaFlame, 0.9f);
		// 场景里飘的蓝色余烬
		Fx.After(back, RevealAt + RevealTime * 0.6f, () =>
		{
			GpuParticles2D embers = ReadyFx.Embers(root, new Vector2(screen.X * 0.5f, screen.Y * 0.9f), new[] { new Color(0.8f, 0.95f, 1f), new Color(0.3f, 0.7f, 1f), new Color(0.1f, 0.3f, 0.9f) },
				26, screen.X * 0.5f, 3.2f, 30f, 110f, 25f);
			Fx.After(root, FireAt + ExitAfter - (RevealAt + RevealTime * 0.6f), () => embers.Emitting = false);
		});

		// ── 举枪、蓄力
		Fx.After(front, AimAt, () => s.OnJokerAim?.Invoke());
		Vector2 muzzle = s.PersonaMuzzle;
		Vector2 aimEnd = enemiesOrdered.Count > 0 ? enemiesOrdered[^1].e : muzzle + new Vector2(1000f, 200f);
		Vector2 dir = (aimEnd - muzzle).Normalized();
		Fx.After(front, ChargeAt, () =>
		{
			float size = 520f * unit;
			Node2D vroot = Fx.Root(front, muzzle, "P5SinfulCharge", FireAt - ChargeAt + 0.3);
			ShaderMaterial vm = new() { Shader = Vortex };
			ColorRect vr = new() { Size = Vector2.One * size, Position = -Vector2.One * size / 2f, Material = vm, MouseFilter = Control.MouseFilterEnum.Ignore };
			vroot.AddChild(vr);
			Fx.Clock(vr);
			float dur = FireAt - ChargeAt;
			Fx.Animate(vroot, 0, dur + 0.12, t =>
			{
				float time = t * (dur + 0.12f);
				float k = Mathf.Clamp(time / dur, 0f, 1f);
				vm.SetShaderParameter("power", time <= dur ? 0.25f + 1.2f * k * k : Mathf.Max(0f, 1.45f * (1f - (time - dur) / 0.12f)));
				vroot.Scale = Vector2.One * (1.15f - 0.35f * k);
				vroot.Rotation = -time * 2f;
			});
			// 周围的余烬被吸进枪口
			for (int i = 0; i < 5; i++)
			{
				float delay = i * dur / 5f;
				Fx.After(front, delay, () =>
				{
					float a = Fx.Rand(0f, Mathf.Tau);
					Vector2 from = muzzle + Vector2.FromAngle(a) * size * 0.75f;
					Glow.Sparks(front, from, 10, 300f, 700f, 0.3f, new[] { new Color(0.85f, 0.95f, 1f), new Color(0.3f, 0.7f, 1f) }, 0f, 0.04f, 0.1f, muzzle - from, 12f);
				});
			}
			Sfx.Play(Sfx.PersonaFlame, 0.6f, dur * 0.3f);
		});

		// ── 开枪
		Fx.After(front, FireAt - 0.18f, () => s.OnJokerShoot?.Invoke());
		var pierce = new float[s.Enemies.Count];
		var orderIndex = enemiesOrdered.Select(t => t.i).ToList();
		var travel = new float[enemiesOrdered.Count];
		for (int k = 0; k < enemiesOrdered.Count; k++)
		{
			travel[k] = Mathf.Max(0.03f, (enemiesOrdered[k].e - muzzle).Dot(dir) / (BulletSpeed * unit));
			pierce[orderIndex[k]] = FireAt + travel[k];
		}
		Fx.After(front, FireAt, () =>
		{
			HitStop.Request(0.14, 0.04);
			Fx.Shake(4);
			Glow.Flash(front, muzzle, new Color(0.85f, 0.95f, 1f), 600f * unit, 0.2f);
			Glow.Ripple(front, muzzle, 1100f * unit, 0.7f, 0.05f);
			Sfx.Play(Sfx.Gun, 1.2f);
			Sfx.Play(Sfx.SubBoom, 1f);
			Sfx.Play(Sfx.Critical, 0.7f);
			Rings(front, muzzle, dir, unit);
			Bullet(front, muzzle, dir, screen, unit);
			Glow.Sparks(front, muzzle, 50, 600f, 1800f, 0.4f, new[] { new Color(0.95f, 0.98f, 1f), new Color(0.35f, 0.75f, 1f), new Color(0.6f, 0.3f, 1f) }, 0f,
				0.05f, 0.16f, dir, 22f);
		});
		for (int k = 0; k < enemiesOrdered.Count; k++)
		{
			int kk = k, idx = orderIndex[k];
			Vector2 e = enemiesOrdered[k].e;
			// 弹道上离这个敌人最近的那一点（子弹从这里穿过去）
			Vector2 hit = muzzle + dir * (e - muzzle).Dot(dir);
			hit = hit.Lerp(e, 0.6f);
			bool last = k == enemiesOrdered.Count - 1;
			Fx.After(front, FireAt + travel[k], () =>
			{
				Pierce(front, hit, dir, unit, last);
				s.OnPierce?.Invoke(idx);
			});
		}
		return pierce;
	}

	/// <summary>枪口外一串冲击环：沿枪管方向往前推，压扁成椭圆（垂直于枪管），一个接一个。</summary>
	private static void Rings(Control layer, Vector2 at, Vector2 dir, float unit)
	{
		Node2D root = Fx.Root(layer, at, "P5SinfulRings", 1.0);
		root.Rotation = dir.Angle();
		for (int i = 0; i < 5; i++)
		{
			float size = (200f + 70f * i) * unit;
			ShaderMaterial m = new() { Shader = RingShader };
			m.SetShaderParameter("tint", i % 2 == 0 ? new Color(0.5f, 0.85f, 1f) : new Color(0.75f, 0.55f, 1f));
			Node2D holder = new() { Scale = new Vector2(0.28f, 1f) };
			root.AddChild(holder);
			ColorRect q = new() { Size = Vector2.One * size, Position = -Vector2.One * size / 2f, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
			holder.AddChild(q);
			float delay = 0.025f * i, travel = (60f + 90f * i) * unit;
			Fx.Animate(holder, 0, 0.55f, t =>
			{
				float time = t * 0.55f - delay;
				if (time < 0f)
				{
					m.SetShaderParameter("alpha", 0f);
					return;
				}
				float k = Mathf.Clamp(time / 0.45f, 0f, 1f), e = 1f - Mathf.Pow(1f - k, 3f);
				holder.Position = new Vector2(travel * e, 0f);
				m.SetShaderParameter("radius", Mathf.Lerp(0.15f, 0.92f, e));
				m.SetShaderParameter("width", Mathf.Lerp(0.06f, 0.012f, e));
				m.SetShaderParameter("alpha", 1f - k * k);
			});
		}
	}

	/// <summary>大罪穿甲弹：一个沿弹道拉长的四边形，弹头以 BulletSpeed 飞出屏幕，尾巴跟着拉长，飞出去后尾巴还亮一下再散。</summary>
	private static void Bullet(Control layer, Vector2 muzzle, Vector2 dir, Vector2 screen, float unit)
	{
		float thick = 230f * unit;
		float reach = screen.Length() * 1.2f;
		float fly = reach / (BulletSpeed * unit), linger = 0.5f;
		Node2D root = Fx.Root(layer, muzzle, "P5SinfulBullet", fly + linger + 0.1);
		root.Rotation = dir.Angle();
		ShaderMaterial m = new() { Shader = BulletShader };
		ColorRect q = new() { Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		Fx.Clock(q);
		Fx.Animate(root, 0, fly + linger, t =>
		{
			float time = t * (fly + linger);
			float headDist = Mathf.Min(time, fly) * BulletSpeed * unit;
			float len = Mathf.Max(headDist, thick);
			q.Size = new Vector2(len + thick * 0.8f, thick);
			q.Position = new Vector2(-thick * 0.1f, -thick / 2f);
			m.SetShaderParameter("len_px", len + thick * 0.8f);
			m.SetShaderParameter("thick_px", thick);
			m.SetShaderParameter("tail", Mathf.Clamp(0.15f + time * 2.5f, 0f, 1f));
			m.SetShaderParameter("fade", time < fly ? 1f : Mathf.Max(0f, 1f - (time - fly) / linger));
		});
	}

	/// <summary>贯穿一个敌人：沿弹道方向喷出的火花 + 一圈冲击环 + 白闪 + 命中确认；最后一个敌人加 P5 红黑白冲击帧和横扫全场的冲击波。</summary>
	private static void Pierce(Control layer, Vector2 at, Vector2 dir, float unit, bool last)
	{
		Glow.Flash(layer, at, new Color(0.8f, 0.92f, 1f), (last ? 700f : 460f) * unit, 0.22f);
		Glow.Sparks(layer, at, last ? 70 : 40, 500f, 1600f, 0.45f, new[] { new Color(0.95f, 0.98f, 1f), new Color(0.35f, 0.75f, 1f), new Color(0.65f, 0.3f, 1f) },
			200f, 0.05f, 0.15f, dir, 35f);
		Glow.Sparks(layer, at, 20, 200f, 600f, 0.35f, new[] { new Color(0.9f, 0.95f, 1f), new Color(0.3f, 0.6f, 1f) }, 300f, 0.04f, 0.1f, -dir, 60f);
		Rings(layer, at, dir, unit * (last ? 1.4f : 0.9f));
		Glow.Ripple(layer, at, (last ? 900f : 520f) * unit, 0.5f, last ? 0.05f : 0.03f);
		Sfx.Play(Sfx.Hit, 0.9f);
		Impact.Confirm(layer, null, at, dir, Weight.Heavy);
	}
}
