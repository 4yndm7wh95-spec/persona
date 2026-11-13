using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 技能牌重做第五批（2026-11-13，会话 C）。融入暗影前后被否的：影子 / 墨 / 烟（会话 A 三版）、斗篷戏法（太难做）、红黑暗焰（用户：火焰很 low、塑料感）。
/// 用户要「华丽」、他认可过的是子弹时间（玻璃钟）、华丽收场、P3 召唤器的碎玻璃 → 改用玻璃和光：一圈水晶「暗之结界」。
/// 每片水晶是一个着色器：折射后面的画面 + 细白边（菲涅尔）+ 暗红的芯 + 下部一点红网点 + 往上扫的高光，不用贴图。用户 2026-11-13：细节要多、不要粗糙；
/// 金卡也不用做到华丽收场那么大，人物周围就行。登记在 CardSkillFx 最后，覆盖第四批的同名牌。
/// </summary>
public static class SkillRedo5
{
	public static void Register(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["Shadowmeld"] = Shadowmeld;
	}

	private static void S(string name, float vol, float at = 0f) => Sfx.Take(name, vol * 0.8f, at);

	private static Shader? _crystal;

	/// <summary>
	/// 一片水晶（ColorRect 的 UV：x 0～1 左右、y 0 顶 → 1 底）：竖长的六棱柱、顶上削成尖；两个棱面折射角度不同，
	/// 中间一道棱线，边上一圈细白光，芯里一道暗红，下部淡红网点，高光带从下往上扫（glint），flash 整片一亮。
	/// </summary>
	private static Shader Crystal => _crystal ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D scr : hint_screen_texture, filter_linear;
uniform float glint = -1.0;     // 高光带的位置（0 底 → 1 顶；< 0 没有）
uniform float flash = 0.0;
uniform float alpha = 1.0;
uniform float lean = 0.0;       // 棱线偏向哪边（-1～1），每片不一样
uniform float seed = 0.0;
uniform vec2 px = vec2(0.002);  // 屏幕一个像素（UV）
void fragment() {
	float x = UV.x * 2.0 - 1.0;              // -1 左 → 1 右
	float y = 1.0 - UV.y;                    // 0 底 → 1 顶
	// 轮廓：底下略窄，0.7 以上削成尖（不对称的尖）
	float tipY = 0.68 + 0.08 * sin(seed * 3.0);
	float w = mix(0.82, 1.0, smoothstep(0.0, 0.12, y));
	float tipX = 0.25 * sin(seed * 5.0);
	if (y > tipY) w *= 1.0 - (y - tipY) / (1.0 - tipY);
	float d = abs(x - tipX * smoothstep(tipY, 1.0, y)) - w;  // < 0 里面
	float aa = fwidth(d) * 1.2 + 0.002;
	float inside = smoothstep(aa, -aa, d);
	if (inside <= 0.001) discard;
	// 棱面：棱线在 lean 处，左边一面、右边一面，折射往不同方向偏
	float ridge = lean * 0.35;
	float face = step(ridge, x);
	vec2 off = (face > 0.5 ? vec2(10.0, -4.0) : vec2(-8.0, 3.0)) * px;
	off += vec2(x * 6.0, 0.0) * px;
	vec3 bg = texture(scr, SCREEN_UV + off).rgb;
	// 玻璃本身：背后画面压暗一点、偏冷白，一面亮一面暗
	vec3 col = bg * mix(0.62, 0.92, face) + vec3(0.05, 0.035, 0.05);
	// 芯：一道暗红从底往上淡
	float core = exp(-pow((x - ridge * 0.5) / 0.22, 2.0)) * (1.0 - y * 0.7);
	col = mix(col, col * vec3(1.2, 0.35, 0.4) + vec3(0.25, 0.0, 0.03), core * 0.55);
	// 下部淡红网点
	vec2 g = vec2(UV.x * 7.0, UV.y * 24.0);
	float r = 0.12 + 0.22 * smoothstep(0.5, 0.0, y);
	float dots = 1.0 - smoothstep(r - 0.06, r, length(fract(g) - 0.5));
	col = mix(col, vec3(0.95, 0.12, 0.18), dots * smoothstep(0.55, 0.0, y) * 0.35);
	// 棱线、边缘：细白光（菲涅尔：越靠边越亮）
	float edge = smoothstep(-0.16, 0.0, d);
	col += vec3(1.0, 0.95, 0.94) * pow(edge, 3.0) * 0.9;
	col += vec3(1.0, 0.92, 0.92) * exp(-abs(x - ridge) / 0.025) * 0.35 * (0.4 + 0.6 * y);
	// 高光带：斜着从下往上扫
	if (glint >= 0.0) {
		float band = exp(-pow((y - glint + x * 0.12) / 0.045, 2.0));
		col += vec3(1.0, 0.96, 0.95) * band * 1.1;
	}
	col = mix(col, vec3(1.0, 0.95, 0.95), flash);
	COLOR = vec4(col, inside * alpha * 0.95);
}" };

	private static Shader? _floor;

	/// <summary>
	/// 脚下的一片玻璃地面：压扁的椭圆里由不规则棱面（Voronoi）拼成，每块折射角度不同、亮暗不同；棱面之间细白缝，缝里透一点红；
	/// spread 0 → 1 从中心往外漫开（边缘参差），alpha 淡出。不是圆圈：外沿由棱面的块参差地截断。
	/// </summary>
	private static Shader Floor => _floor ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D scr : hint_screen_texture, filter_linear;
uniform float spread = 0.0;
uniform float alpha = 1.0;
uniform float seed = 0.0;
uniform float aspect = 4.0;     // 宽 / 高
uniform vec2 px = vec2(0.002);
vec2 h22(vec2 p) { p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3))); return fract(sin(p + seed) * 43758.5453); }
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;                 // 椭圆归一化：边 = 1
	float r = length(p);
	vec2 g = vec2(p.x * aspect, p.y) * 2.2;     // 棱面网格（按真实比例，块是扁的）
	vec2 ip = floor(g), fp = fract(g);
	float d1 = 9.0, d2 = 9.0; vec2 cid = vec2(0.0);
	for (int j = -1; j <= 1; j++) for (int i = -1; i <= 1; i++) {
		vec2 o = vec2(float(i), float(j));
		vec2 pt = o + h22(ip + o);
		float d = length(pt - fp);
		if (d < d1) { d2 = d1; d1 = d; cid = ip + o; } else if (d < d2) d2 = d;
	}
	float cellR = length(vec2(cid.x / aspect, cid.y) / 2.2 + 0.25 / 2.2);
	float h = h22(cid + 7.0).x;
	// 漫开：按块参差地出现（块离中心越远越晚），外沿再随机截掉一些块
	float show = smoothstep(cellR - 0.05, cellR + 0.05, spread * 1.15 - h * 0.15) * step(cellR + h * 0.18, 1.05);
	show *= 1.0 - smoothstep(0.85, 1.0, r);
	if (show <= 0.002) discard;
	float edge = 1.0 - smoothstep(0.0, 0.05 + fwidth(d2 - d1), d2 - d1);
	vec2 off = (h22(cid + 3.0) - 0.5) * 14.0 * px;
	vec3 bg = texture(scr, SCREEN_UV + off).rgb;
	vec3 col = bg * mix(0.55, 0.95, h) + vec3(0.06, 0.03, 0.05);
	col = mix(col, vec3(1.0, 0.94, 0.93), edge * 0.4);
	col += vec3(0.6, 0.03, 0.08) * edge * (1.0 - r) * 0.5;
	col += vec3(0.5, 0.05, 0.08) * (1.0 - r) * 0.25;
	COLOR = vec4(col, show * alpha * (0.4 + 0.3 * (1.0 - r)));
}" };

	private sealed class Shard
	{
		public Node2D Root = null!;
		public ShaderMaterial M = null!;
		public float Ang, H, W, Tilt, Delay, Depth;
		public bool Front;
	}

	/// <summary>
	/// 融入暗影（本回合获得的格挡翻倍），「暗之结界」，约 1.3s：
	/// ① 0～0.3 九片细长的水晶围着他从地面一片接一片破土升起（后排高、前排矮，不挡人），根部迸出白红碎屑；
	/// ② 0.35 主拍：整圈水晶微微一转，一道高光同时扫过每一片、「叮」一闪，他暗下来一点；
	/// ③ 0.85 第二下（翻倍）：水晶依次碎开，碎玻璃带着棱光往外迸，他身上闪一下淡红剪影，亮回来。
	/// 拍子：破土（很轻）→ 一闪（叮，主拍）→ 碎开（玻璃）。
	/// </summary>
	public static void Shadowmeld(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		const float flashAt = 0.35f, breakAt = 0.85f, life = 1.3f;
		Control back = c.Back ?? c.Front;
		Vector2 ring = c.Feet + new Vector2(0f, -h * 0.01f);
		float rx = h * 0.44f, ry = h * 0.15f;
		S("gx_freeze", 0.12f, 0.02f);
		S("gx_ping", 0.2f, flashAt);
		S("gx_glass", 0.3f, breakAt);

		// 前面那一层先拷贝一次屏幕（带上人物），水晶折射才看得到他
		Node2D frontRoot = Fx.Root(c.Front, Vector2.Zero, "P5Crystals", life + 0.2);
		// 不用 TopLevel（会脱离所在层的绘制顺序、画到人物前面），位置每帧按全局坐标设
		frontRoot.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		Vector2 scr = Fx.Screen(c.Front);

		// 角度：前排（下半圈，在他前面）只放在他身体两侧、正前方不放（用户：遮挡关系要理清）；后排在他身后。按远近排序，近的后画
		var angs = new List<float>();
		foreach (float a0 in new[] { 0.32f, 0.85f, 2.29f, 2.82f, 3.45f, 3.95f, 4.45f, 4.97f, 5.47f, 5.98f }) angs.Add(a0 + Fx.Rand(-0.07f, 0.07f));
		angs.Sort((x, y) => Mathf.Sin(x).CompareTo(Mathf.Sin(y)));
		var shards = new List<Shard>();
		Node2D backRoot = Fx.Root(back, Vector2.Zero, "P5CrystalBack", life + 0.2);

		foreach (float ang in angs)
		{
			bool front = Mathf.Sin(ang) > 0f;
			float depth = Mathf.Sin(ang);                                   // -1 最远 → 1 最近
			float k = 1f + 0.12f * depth;                                    // 近大远小
			float hgt = h * k * (front ? Fx.Rand(0.3f, 0.42f) : Fx.Rand(0.5f, 0.8f));
			float wid = h * k * (front ? Fx.Rand(0.06f, 0.08f) : Fx.Rand(0.06f, 0.09f));
			Node2D root = new() { Name = "P5Crystal" };
			(front ? frontRoot : backRoot).AddChild(root);
			ShaderMaterial m = new() { Shader = Crystal };
			m.SetShaderParameter("lean", Fx.Rand(-1f, 1f));
			m.SetShaderParameter("seed", Fx.Rand(0f, 20f));
			m.SetShaderParameter("px", new Vector2(1f / scr.X, 1f / scr.Y));
			root.AddChild(new ColorRect { Size = new Vector2(wid, hgt), Position = new Vector2(-wid / 2f, -hgt), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore });
			float lum = 0.8f + 0.2f * (depth * 0.5f + 0.5f);                 // 远的稍暗
			root.Modulate = new Color(lum, lum, lum, 1f);
			float order = Mathf.Abs(Mathf.Wrap(ang - Mathf.Pi * 0.5f, -Mathf.Pi, Mathf.Pi)) / Mathf.Pi;
			shards.Add(new Shard { Root = root, M = m, Ang = ang, H = hgt, W = wid, Front = front, Depth = depth, Delay = 0.05f + order * 0.2f,
				Tilt = Mathf.Cos(ang) * 0.24f + Fx.Rand(-0.06f, 0.06f) });
		}
		foreach (Shard sh in shards)
		{
			Shard s0 = sh;
			s0.Root.Visible = false;
			Fx.Animate(s0.Root, 0, life, t =>
			{
				float s = t * life;
				float up = s < s0.Delay ? 0f : Ease5.OutBack(Mathf.Min(1f, (s - s0.Delay) / 0.16f));
				float turn = 0.12f * f * Fx.OutCubic(Ease5.Seg(s, flashAt - 0.08f, 0.3f));   // 小转，不让前后排越过两侧
				float a = s0.Ang + turn;
				s0.Root.Visible = up > 0.001f && s < breakAt + s0.Delay * 0.4f;
				s0.Root.GlobalPosition = ring + new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
				s0.Root.Rotation = s0.Tilt + Mathf.Sin(s * 3f + s0.Ang) * 0.015f;
				s0.Root.Scale = new Vector2(1f, Mathf.Max(0.001f, up));
				s0.M.SetShaderParameter("glint", s >= flashAt - 0.04f && s < flashAt + 0.2f ? (s - flashAt + 0.04f) / 0.2f * 1.3f - 0.15f : -1f);
				s0.M.SetShaderParameter("flash", s >= flashAt ? Mathf.Exp(-(s - flashAt) * 14f) * 0.55f : 0f);
				s0.M.SetShaderParameter("alpha", Mathf.Min(1f, up * 1.5f));
			});
			// 破土：根部迸一小撮白红碎屑
			Fx.After(c.Front, s0.Delay + 0.02, () =>
			{
				Vector2 at = s0.Root.GlobalPosition;
				Phantom.Shards(c.Front, at, 4, 120f * u, 300f * u, new Vector2(Mathf.Cos(s0.Ang) * 0.6f, -1f), 35f, 0.25f, 500f * u, 9f * u);
			});
		}
		// 脚下的玻璃地面（人后面一层）：0～0.3 从脚下往外漫开，碎开时淡掉
		{
			Vector2 fsz = new(rx * 2.6f, ry * 2.6f);
			Node2D fr = Fx.Root(back, ring + new Vector2(0f, h * 0.01f), "P5CrystalFloor", life + 0.1);
			ShaderMaterial fm = new() { Shader = Floor };
			fm.SetShaderParameter("seed", Fx.Rand(0f, 30f));
			fm.SetShaderParameter("aspect", fsz.X / fsz.Y);
			fm.SetShaderParameter("px", new Vector2(1f / scr.X, 1f / scr.Y));
			fr.AddChild(new ColorRect { Size = fsz, Position = -fsz / 2f, Material = fm, MouseFilter = Control.MouseFilterEnum.Ignore });
			back.MoveChild(fr, backRoot.GetIndex());          // 在后排水晶下面
			Fx.Animate(fr, 0, life, t =>
			{
				float s = t * life;
				fm.SetShaderParameter("spread", Fx.OutCubic(Ease5.Seg(s, 0f, 0.32f)));
				fm.SetShaderParameter("alpha", 1f - Fx.OutCubic(Ease5.Seg(s, breakAt, 0.35f)));
			});
		}
		// 身后一团柔和的暗红背光，衬出轮廓
		{
			Node2D gr = Fx.Root(back, ring + new Vector2(0f, -h * 0.45f), "P5CrystalGlow", life + 0.1);
			back.MoveChild(gr, backRoot.GetIndex());
			Sprite2D glow = Glow.SoftSprite(gr, Vector2.Zero, new Color(1f, 0.1f, 0.18f, 0f), h * 0.05f);
			glow.Scale = new Vector2(h * 0.04f, h * 0.055f);
			Fx.Animate(gr, 0, life, t =>
			{
				float s = t * life;
				float k = Fx.OutCubic(Ease5.Seg(s, 0.05f, 0.3f)) * (1f - Fx.OutCubic(Ease5.Seg(s, breakAt, 0.4f)));
				float pulse = s >= flashAt ? Mathf.Exp(-(s - flashAt) * 6f) * 0.5f : 0f;
				glow.Modulate = new Color(1f, 0.1f, 0.18f, 0.32f * k + 0.3f * pulse);
			});
		}
		// 水晶之间慢慢浮起的细小碎光
		for (int i = 0; i < 4; i++)
		{
			float d = 0.2f + 0.14f * i;
			Fx.After(c.Front, d, () => Glow.Sparks(c.Front, ring + new Vector2(Fx.Rand(-0.3f, 0.3f) * h, -h * 0.05f), 8, 15f, 60f, 0.9f,
				new[] { new Color(1f, 0.97f, 0.95f), new Color(1f, 0.55f, 0.6f) }, -70f, 0.025f * u, 0.055f * u, Vector2.Up, 50f));
		}
		// 主拍：一闪
		Fx.After(c.Front, flashAt, () =>
		{
			Fx.Shake(1);
			Glow.Flash(back, ring + new Vector2(0f, -h * 0.25f), new Color(1f, 0.25f, 0.3f), h * 1.1f, 0.22f);
			Glow.Sparks(c.Front, ring + new Vector2(0f, -h * 0.3f), 20, 40f, 160f, 0.6f, new[] { new Color(1f, 0.97f, 0.95f), new Color(1f, 0.3f, 0.35f) }, -60f,
				0.03f * u, 0.07f * u, Vector2.Up, 160f);
		});
		// 碎开：从前往后依次碎，碎玻璃往外迸
		foreach (Shard sh in shards)
		{
			Shard s0 = sh;
			float d = breakAt + s0.Delay * 0.4f;
			Fx.After(c.Front, d, () =>
			{
				Vector2 basePt = s0.Root.GlobalPosition;
				Vector2 mid = basePt + new Vector2(Mathf.Sin(s0.Tilt), -Mathf.Cos(s0.Tilt)) * s0.H * 0.5f;
				Vector2 outDir = new(Mathf.Cos(s0.Ang), Mathf.Sin(s0.Ang) * 0.4f - 0.5f);
				Control layer = s0.Front ? c.Front : back;
				Glow.Shards(layer, mid, s0.Front ? 6 : 8, 0.9f * u, outDir, 1.0f);
				Phantom.Shards(layer, mid, 5, 200f * u, 520f * u, outDir, 50f, 0.35f, 380f * u, 12f * u);
				Glow.Flash(layer, mid, new Color(1f, 0.9f, 0.9f), s0.H * 0.7f, 0.12f);
			});
		}
		Fx.After(c.Front, breakAt + 0.05, () =>
		{
			if (c.Back != null && c.CopyBody != null)
			{
				SkillFx.Ghosts(c.Back, c.CopyBody, c.Region, f, 1, sound: false, subtle: true);
			}
		});
		// 他暗下来一点，碎开时亮回来
		if (c.SelfTint is { } tint)
		{
			Node2D tk = Fx.Root(c.Front, c.Feet, "P5MeldTint", life + 0.1);
			Color dim = new(0.5f, 0.44f, 0.55f);
			Fx.Animate(tk, 0, life, t =>
			{
				float s = t * life;
				float k = Fx.OutCubic(Ease5.Seg(s, flashAt - 0.05f, 0.15f)) * (1f - Fx.OutCubic(Ease5.Seg(s, breakAt, 0.3f)));
				tint(Colors.White.Lerp(dim, k));
			});
			tk.TreeExiting += () => tint(Colors.White);
		}
	}
}
