using Godot;

namespace P5Joker;

/// <summary>
/// 夜魇（稀有：选一张手牌，下回合得到它的 3 张复制）第四版「梦魇之手」（2026-11-18；前三版：贴图暗影手、着色器触须、二维咒烟手，都被否 / 自己看不过关）。
/// 手就是毒性爆发那只用户认可的 3D 咒烟巨手（CrushHand：着色器光线步进，没有贴图），这回不是捏碎，是「攥住一张牌、复制三份、拖进梦里」：
/// · 四周压成暗红黑；他手里那张牌翻着浮起来，停在他身前上方；
/// · 牌后面的黑暗深处，一只巨手冲出来（从远处一个小点猛地放大、冲过头一点再回来），五指张开悬在牌后面，指尖慢慢往里扣；
/// · 一把攥住（手掌在牌后面、手指在牌前面）—— 顿帧、震屏、重的一声；
/// · 拳头攥着牌「咚、咚、咚」三下心跳，每一下指缝里透出红光、拳头往前一顶，牌的一张暗红复制从拳头上沿被挤出来、翻着飞到拳头上方排成扇形；
/// · 拳头松开一点，原牌掉出来、飞回他手里一闪；拳头往黑暗深处猛地缩回去、散成咒烟，三张复制被一起拖进深处（越来越小、越来越暗、从边上烧掉）；
/// · 四周慢慢亮回来。下回合那三张就从梦里回来。
/// </summary>
public static class NightmareFx
{
	private const float Focal = 1.25f;      // 和 CrushHand 着色器的 focal 一样

	/// <summary>手的姿态（CrushHand 的参数）：位置（世界坐标，镜头在原点朝 +z），绕 x 倾斜、绕 z 旋转、攥拳、拇指、张开。</summary>
	private readonly record struct HP(Vector3 Pos, float Tilt, float Roll, float Curl, float Thumb, float Spread);

	private static HP Lerp(HP a, HP b, float k) => new(a.Pos.Lerp(b.Pos, k), Mathf.Lerp(a.Tilt, b.Tilt, k), Mathf.Lerp(a.Roll, b.Roll, k),
		Mathf.Lerp(a.Curl, b.Curl, k), Mathf.Lerp(a.Thumb, b.Thumb, k), Mathf.Lerp(a.Spread, b.Spread, k));

	/// <summary>手的局部点 → 相对手腕的世界偏移（和着色器 to_local 互逆；hand_size = 1、yaw = 0）。</summary>
	private static Vector3 Off(HP p, Vector3 v)
	{
		float c = Mathf.Cos(p.Tilt), s = Mathf.Sin(p.Tilt);
		v = new Vector3(v.X, c * v.Y - s * v.Z, s * v.Y + c * v.Z);
		c = Mathf.Cos(p.Roll); s = Mathf.Sin(p.Roll);
		return new Vector3(c * v.X - s * v.Y, s * v.X + c * v.Y, v.Z);
	}

	/// <summary>世界坐标 → 屏幕像素（巨手的画布铺满整个屏幕）。</summary>
	private static Vector2 Project(Vector3 w, Vector2 scr)
	{
		float a = scr.X / scr.Y, z = Mathf.Max(w.Z, 0.05f);
		return new Vector2((w.X / z * Focal / a + 1f) * 0.5f * scr.X, (1f - w.Y / z * Focal) * 0.5f * scr.Y);
	}

	private static Vector3 Unproject(Vector2 px, float z, Vector2 scr)
	{
		float a = scr.X / scr.Y;
		return new Vector3((px.X / scr.X * 2f - 1f) * a * z / Focal, (1f - px.Y / scr.Y * 2f) * z / Focal, z);
	}

	/// <summary>把手的局部点 anchor 摆到屏幕上的 at（手腕深度 = p.Pos.Z 不变）。</summary>
	private static HP Aim(HP p, Vector3 anchor, Vector2 at, Vector2 scr)
	{
		Vector3 o = Off(p, anchor);
		return p with { Pos = Unproject(at, p.Pos.Z + o.Z, scr) - o };
	}

	private static void Apply(CrushHand h, HP p)
	{
		h.Set("hand_pos", p.Pos);
		h.Set("tilt", p.Tilt);
		h.Set("roll", p.Roll);
		h.Set("curl", p.Curl);
		h.Set("thumb", p.Thumb);
		h.Set("spread", p.Spread);
		// 前臂藏在手后面：顺着穿过手掌下半的视线往深处伸，稍微往下偏一点（手是从下面的黑暗里伸上来的）
		Vector3 palm = p.Pos + Off(p, new Vector3(0f, 0.2f, 0.1f));
		h.Set("elbow", palm * ((palm.Z + 6f) / palm.Z) + new Vector3(0f, -0.9f, 0f));
	}

	private static Shader? _vortex;

	/// <summary>梦魇漩涡：极坐标里卷的咒烟（近黑），三条暗红的旋臂往中心卷；中心一点暗红的深处。预乘混合（暗烟压暗背景、红光往上加）。</summary>
	private static Shader Vortex => _vortex ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float clock = 0.0;
uniform float open = 0.0;
uniform float spin = 0.0;
uniform float pulse = 0.0;
uniform float alpha = 1.0;
uniform float flip = 1.0;
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
float fbm(vec2 p) { return vn(p) * 0.5 + vn(p * 2.03 + 5.1) * 0.3 + vn(p * 4.1 + 1.7) * 0.2; }
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	p.x *= flip;
	float R = max(open, 0.001);
	float rn = length(p) / R;
	if (rn > 1.15) discard;
	float a = atan(p.y, p.x);
	// 越往中心转得越多（卷进去）
	float sw = a + spin + 2.4 / (rn + 0.3) + clock * 0.9;
	vec2 q = vec2(cos(sw), sin(sw)) * rn;
	float n = fbm(q * 2.6 + vec2(clock * 0.25, -clock * 0.1));
	float n2 = fbm(q * 5.5 - vec2(clock * 0.4, 0.0) + 3.0);
	float edge = 1.0 - smoothstep(0.55, 1.05, rn + (n - 0.5) * 0.35);       // 软边，被噪声撕开
	float dens = edge * (0.55 + 0.45 * n) * smoothstep(0.0, 0.25, rn + 0.1);
	float arms = 0.5 + 0.5 * sin(sw * 3.0 + n * 5.0);
	// 旋臂不要是一条光滑的红带子：被两层噪声撕成一缕缕的烟丝
	float n3 = fbm(q * 11.0 + vec2(clock * 0.8, 1.3));
	float fil = pow(arms, 6.0) * smoothstep(0.3, 0.8, n2 + 0.25 * n) * (0.5 + 0.7 * n3) * edge * smoothstep(0.08, 0.35, rn);
	float core = exp(-rn * 5.0);
	vec3 smoke = mix(vec3(0.02, 0.004, 0.008), vec3(0.09, 0.015, 0.022), n2);
	vec3 glow = vec3(0.55, 0.03, 0.07) * fil * (0.3 + 0.9 * pulse) + vec3(0.75, 0.06, 0.1) * core * (0.18 + 0.5 * pulse);
	float al = clamp(dens * 0.9, 0.0, 1.0) * alpha;
	COLOR = vec4(smoke * al + glow * alpha * edge, al);
}" };

	private static Shader? _squeeze;

	/// <summary>攥紧的画面收缩（读屏）：拳心周围的画面往里一缩（蓄力），攥住那一下弹回来、一道冲击往外推、带一点红色的色散。</summary>
	private static Shader SqueezeShader => _squeeze ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec2 c = vec2(0.5);
uniform float aspect = 1.7778;
uniform float r = 0.3;
uniform float k = 0.0;
uniform float ring = -1.0;
uniform float ring_a = 0.0;
void fragment() {
	vec2 d = (SCREEN_UV - c) * vec2(aspect, 1.0);
	float l = length(d);
	vec2 n = d / max(l, 1e-4);
	float f = k * exp(-pow(l / r, 2.0));
	float w = ring_a * exp(-pow((l - ring) / 0.035, 2.0));
	vec2 off = n * (l * f - w * 0.02);
	vec2 uv = SCREEN_UV + off / vec2(aspect, 1.0);
	vec2 ca = n / vec2(aspect, 1.0) * (abs(f) * 0.012 + w * 0.006);
	vec3 col = vec3(texture(screen_tex, uv + ca).r, texture(screen_tex, uv).g, texture(screen_tex, uv - ca).b);
	col *= mix(vec3(1.0), vec3(1.12, 0.82, 0.85), clamp(abs(f) * 3.0 + w, 0.0, 0.6));
	COLOR = vec4(col, 1.0);
}" };

	private static void Squeeze(Control layer, Vector2 at, float h, float from, float hit)
	{
		Vector2 scr = Fx.Screen(layer);
		Node2D root = Fx.ScreenRoot(layer, "P5NightmareSqueeze");
		root.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		ShaderMaterial m = new() { Shader = SqueezeShader };
		ColorRect rect = new() { Size = scr, Material = m, MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
		root.AddChild(rect);
		Vector2 cs = Fx.ScreenPoint(layer, at) / scr;
		float hs = h * layer.GetCanvasTransform().Scale.Y / scr.Y;
		m.SetShaderParameter("aspect", scr.X / scr.Y);
		m.SetShaderParameter("r", hs * 0.9f);
		float total = hit - from + 0.45f;
		Fx.FreeAfter(root, from + total + 0.05);
		Fx.Animate(rect, from, total, t =>
		{
			float s = from + t * total;
			rect.Visible = true;
			m.SetShaderParameter("c", Fx.ScreenPoint(layer, at) / scr);
			// 往里缩（越来越快）→ 攥住时弹出去一点 → 回平
			float k = s < hit ? 0.22f * Fx.InCubic(Mathf.Clamp((s - from) / (hit - from), 0f, 1f))
				: Mathf.Lerp(-0.08f, 0f, Fx.OutCubic(Mathf.Clamp((s - hit) / 0.25f, 0f, 1f))) * (s - hit < 0.03f ? (s - hit) / 0.03f : 1f);
			m.SetShaderParameter("k", k);
			float rs = Mathf.Max(0f, s - hit);
			m.SetShaderParameter("ring", s < hit ? -1f : hs * (0.15f + 1.6f * Fx.OutCubic(Mathf.Min(1f, rs / 0.4f))));
			m.SetShaderParameter("ring_a", s < hit ? 0f : 1f - Mathf.Min(1f, rs / 0.4f));
		});
		_ = cs;
	}

	public static void Play(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, fc = c.Face;
		Control front = c.Front;
		Control back = c.Back != null && GodotObject.IsInstanceValid(c.Back) ? c.Back : c.Front;
		Vector2 scr = Fx.Screen(front);
		Vector2 cardAt = c.Hand + new Vector2(fc * h * 0.62f, -h * 0.3f);
		// 巨手的镜头是整个屏幕：牌的位置、身高都换成屏幕像素
		Vector2 cardS = Fx.ScreenPoint(front, cardAt);
		float hS = Mathf.Max(60f, (Fx.ScreenPoint(front, c.Head) - Fx.ScreenPoint(front, c.Feet)).Length() / 1.02f);
		float pxK = hS / Mathf.Max(1f, h);                                      // 屏幕像素 / 世界像素
		float zo = 2.3f * Focal * (scr.Y * 0.5f) / (1.3f * hS);                 // 张开的手 ≈ 1.3 倍身高

		const float surge0 = 0.1f, surge1 = 0.32f, grab0 = 0.44f, grab1 = 0.56f, beat0 = 0.57f, pull0 = 0.98f, pull1 = 1.3f, end = 1.46f;   // 用户 2026-11-19：捏完三张一起冒出来，不要一张一张
		Vector3 anchor = new(0f, 0.85f, -0.3f);
		float rl = 0.14f * fc;
		HP deep = Aim(new HP(new Vector3(0f, 0f, zo * 2.8f), -0.1f, rl * 0.4f, 0.15f, 0.1f, 1f), anchor, cardS + new Vector2(0f, -0.1f * hS), scr);
		HP open = Aim(new HP(new Vector3(0f, 0f, zo), -0.15f, rl, 0f, 0f, 1.4f), anchor, cardS, scr);
		HP hover = Aim(new HP(new Vector3(0f, 0f, zo * 0.95f), -0.2f, rl, 0.14f, 0.05f, 1.45f), anchor, cardS, scr);
		HP fist = Aim(new HP(new Vector3(0f, 0f, zo * 1.3f), -1.05f, rl * 2f, 1f, 1f, 0.9f), anchor, cardS, scr);
		HP gone = Aim(fist with { Pos = new Vector3(0f, 0f, zo * 6f), Tilt = -0.8f }, anchor, cardS + new Vector2(fc * 0.08f * hS, 0.06f * hS), scr);   // 往深处沉（三张复制从上面被拖下去）

		// ── 声音 ──
		Sfx.Take("gx_card_slide", 0.12f);
		Sfx.Play(Sfx.Curse, 0.4f, surge0);
		Sfx.Take("gx_whoosh", 0.16f, surge0 + 0.04f);
		// 攥紧（用户 2026-11-19：捏紧那一下要强反馈、音效做好）：先往里吸的冷声（空气被攥走）→ 捏碎一样的低沉闷响 + 低频重击 + 一层咒怨的闷响尾巴
		Sfx.Take("gx_freeze", 0.22f, grab0 - 0.04f);
		Sfx.Take("gx_whoosh", 0.14f, grab0);
		Sfx.Play(Sfx.Crush, 0.65f, grab1);
		Sfx.Play(Sfx.SubBoom, 0.48f, grab1);
		Sfx.Play(Sfx.CurseSoft, 0.4f, grab1 + 0.02f);
		Sfx.Take("gx_glass", 0.14f, grab1 + 0.03f);
		// 捏住的同一刻三张复制迸出来（用户 2026-11-19：立刻出来、干脆利落有冲击感）：扇形一甩 + 三张同时钉住的「唰」
		Sfx.Take("gx_fan_open", 0.26f, beat0);
		Sfx.Play("card_flick", 0.34f, beat0 + 0.02f);
		Sfx.Take("gx_slice", 0.2f, beat0 + 0.07f);
		Sfx.Play(Sfx.Crack, 0.2f, pull0);
		Sfx.Take("gx_whoosh", 0.2f, pull0 + 0.02f);
		Sfx.Take("gx_burn", 0.14f, pull0 + 0.12f);
		Sfx.Take("gx_card_stack", 0.14f, pull0 + 0.34f);

		float[] beats = { beat0 };
		float Beat(float s)
		{
			float v = 0f;
			foreach (float b in beats) v += s > b - 0.03f ? (s < b ? 1f + (s - b) / 0.03f : Mathf.Exp(-(s - b) / 0.09f)) : 0f;
			return v;
		}

		// ── 四周压暗：跟着心跳一下下收紧（黑暗在呼吸）──
		SkillRedo5.Dim(back, cardAt + new Vector2(-fc * h * 0.2f, h * 0.2f), new Vector2(h * 5.5f, h * 3.6f), end + 0.3,
			s => Mathf.Min(0.97f, Fx.OutCubic(Ease5.Seg(s, 0f, 0.22f)) * (1f - Ease5.Seg(s, pull1 - 0.1f, 0.4f)) * (0.8f + 0.12f * Beat(s)
				+ 0.18f * Mathf.Exp(-Mathf.Max(0f, s - grab1) / 0.12f) * (s >= grab1 - 0.02f ? 1f : 0f))));   // 攥紧那一下四周猛地再暗一截
		Glow.Flash(back, cardAt, new Color(0.28f, 0.01f, 0.045f), h * 1.5f, pull1);
		// 巨手冲出来的地方：黑暗里裂开一团暗红
		Glow.Flash(back, cardAt + new Vector2(0f, -h * 0.05f), new Color(0.55f, 0.02f, 0.08f), h * 2.6f, 0.55f, surge0);

		// ── 梦魇漩涡（巨手背后）：黑暗里旋开一团咒烟漩涡，巨手从漩涡中心冲出来；跟着心跳透出暗红的旋臂；拳头沉回中心时漩涡旋紧收拢 ──
		Vector2 vortexAt = cardAt + new Vector2(fc * h * 0.08f, h * 0.06f);
		float vSize = h * 2.6f;
		Node2D vroot = Fx.Root(back, vortexAt, "P5NightmareVortex", end + 0.1);
		vroot.Scale = new Vector2(1f, 0.88f);
		ColorRect vq = new()
		{
			Size = Vector2.One * vSize, Position = -Vector2.One * vSize * 0.5f, MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = Vortex },
		};
		vroot.AddChild(vq);
		var vm = (ShaderMaterial)vq.Material;
		vm.SetShaderParameter("open", 0f);
		vm.SetShaderParameter("flip", fc);

		// ── 原牌（他手里浮起来、被攥住、掉出来飞回去）──
		Phantom.P5Card card = Phantom.Card(front, c.Hand, h * 0.2f, end + 0.05);
		// ── 三张复制（先建好藏着，这样画在手指下面：像从拳头里挤出来）──
		var copies = new Phantom.P5Card[3];
		var fanAt = new Vector2[3];
		var fanTilt = new float[3];
		for (int i = 0; i < 3; i++)
		{
			copies[i] = Phantom.Card(front, cardAt, h * 0.2f, end + 0.05);
			copies[i].Root.Visible = false;
			copies[i].Rect.Modulate = new Color(1f, 0.36f, 0.4f, 1f);       // 暗红的影子牌
			copies[i].Set("seed", 7f + i * 13f);
			float k = i - 1f;
			fanAt[i] = cardAt + new Vector2(k * h * 0.4f, -h * (0.62f - 0.1f * Mathf.Abs(k)));
			fanTilt[i] = k * 0.38f;
		}

		// ── 巨手（光线步进一次，手掌画在牌后面 / 人物后面，手指画在牌前面）──
		Node2D rearRoot = Fx.ScreenRoot(back, "P5NightmareHand");
		Control rear = new();
		rearRoot.AddChild(rear);
		Node2D frontRoot = Fx.ScreenRoot(front, "P5NightmareFingers");
		Control fingers = new();
		frontRoot.AddChild(fingers);
		CrushHandFrame frame = new(rearRoot, rear, fingers, scr);
		CrushHand hand = frame.Hand;
		hand.Set("band_z", 50f);
		hand.Set("alpha", 0f);
		frame.SetVisible(false);
		Fx.FreeAfter(rearRoot, end + 0.05);
		Fx.FreeAfter(frontRoot, end + 0.05);

		Node2D drive = Fx.Root(front, cardAt, "P5NightmareDrive", end + 0.05);
		Fx.Animate(drive, 0, end, t =>
		{
			float s = t * end;
			// ── 手 ──
			HP pose;
			float alpha = 1f, grip = 0f, dissolve = 0f, flash = 0f;
			if (s < surge0) { pose = deep; alpha = 0f; }
			else if (s < surge1)
			{
				float k = (s - surge0) / (surge1 - surge0);
				float e = 1f + 2.2f * Mathf.Pow(k - 1f, 3f) + 1.2f * Mathf.Pow(k - 1f, 2f);     // 冲过头一点再回来
				pose = Lerp(deep, open, e);
				alpha = Mathf.Clamp(k / 0.3f, 0f, 1f);
			}
			else if (s < grab0) pose = Lerp(open, hover, Mathf.SmoothStep(0f, 1f, (s - surge1) / (grab0 - surge1)));
			else if (s < grab1)
			{
				float k = (s - grab0) / (grab1 - grab0);
				float e = k < 0.5f ? 4f * k * k * k : 1f - Mathf.Pow(-2f * k + 2f, 3f) / 2f;   // 先蓄后猛
				pose = Lerp(hover, fist, e);
				grip = 0.35f * e;
			}
			else if (s < pull0)
			{
				pose = fist;
				grip = 0.25f;
				float push = 0f;
				foreach (float b in beats)
				{
					float d = s - b;
					if (d < -0.03f) continue;
					float p = d < 0f ? 1f + d / 0.03f : Mathf.Exp(-d / 0.07f);    // 心跳：一顶、红光一涨
					grip += 0.6f * p;
					push = Mathf.Max(push, p);
				}
				pose = pose with { Pos = pose.Pos + new Vector3(0f, 0f, -0.12f * push) };
				float amp = 0.004f + 0.006f * Ease5.Seg(s, grab1, pull0 - grab1);
				pose = pose with { Pos = pose.Pos + new Vector3(Mathf.Sin(s * 83f), Mathf.Cos(s * 67f), 0f) * amp * pose.Pos.Z };
				if (s < grab1 + 0.08f) flash = 0.6f * (1f - (s - grab1) / 0.08f);
			}
			else if (s < pull1)
			{
				float k = (s - pull0) / (pull1 - pull0);
				pose = Lerp(fist, gone, Fx.InCubic(k));
				grip = Mathf.Max(0f, 0.6f - k);
				dissolve = Fx.OutCubic(Mathf.Clamp((k - 0.15f) / 0.85f, 0f, 1f)) * 0.9f;
				alpha = 1f - Fx.InCubic(Mathf.Clamp((k - 0.35f) / 0.65f, 0f, 1f));
			}
			else { pose = gone; alpha = 0f; }
			Apply(hand, pose);
			hand.Set("clock", s);
			hand.Set("grip", grip);
			hand.Set("flash", flash);
			hand.Set("dissolve", dissolve);
			hand.Set("alpha", alpha);
			hand.Set("front_mix", Mathf.SmoothStep(0f, 1f, Mathf.Clamp((s - grab0) / (grab1 - grab0), 0f, 1f)));
			frame.SetVisible(alpha > 0.001f);

			// ── 原牌 ──
			{
				float up = Ease5.OutBack(Mathf.Min(1f, s / 0.3f));
				Vector2 p = c.Hand.Lerp(cardAt, up) + new Vector2(0f, Mathf.Sin(s * 3f) * h * 0.01f);
				float flip = Mathf.Pi * (1f - Fx.OutCubic(Mathf.Min(1f, s / 0.36f)));        // 翻着浮起来：牌背 → 牌面
				float sc = 0.6f + 0.4f * up;
				if (s > surge1 && s < grab1) p += new Vector2(Fx.Rand(-1f, 1f), Fx.Rand(-1f, 1f)) * 2.5f * u * Ease5.Seg(s, surge1, grab1 - surge1);
				if (s >= grab1 && s < pull0)
				{
					sc *= 0.88f;
					p += new Vector2(Fx.Rand(-1f, 1f), Fx.Rand(-1f, 1f)) * 3f * u;
				}
				float back2 = Ease5.Seg(s, pull0 + 0.04f, 0.32f);
				if (back2 > 0f)
				{
					float e = Fx.InCubic(back2);
					Vector2 drop = cardAt + new Vector2(fc * h * 0.06f, h * 0.12f);
					p = cardAt.Lerp(drop, Fx.OutCubic(Mathf.Min(1f, back2 * 3f))).Lerp(c.Hand, e);
					sc = 0.88f - 0.42f * e;
					flip = 0f;
				}
				card.Place(p, 0.1f * Mathf.Sin(s * 2.5f) - fc * 0.5f * back2, flip, sc);
				card.Set("sheen", Mathf.Clamp((s - 0.12f) / 0.3f, 0f, 1f) * 3f - 1f);
				float gl = s < grab1 ? 0.1f : 0.15f;
				foreach (float b in beats) gl += s > b - 0.02f ? 0.55f * Mathf.Exp(-(s - b) / 0.08f) : 0f;
				card.Set("glow", Mathf.Min(0.8f, gl));
				card.Root.Visible = s < pull0 + 0.36f;
			}

			// ── 复制：心跳一下挤出来一张，飞到拳头上方排成扇形；拳头缩回去时被一起拖进深处 ──
			Vector2 fistNow = Project(pose.Pos + Off(pose, anchor), scr);
			Vector2 fistW = cardAt + (fistNow - cardS) / pxK;                        // 屏幕 → 世界（相对牌的位置换算回去）
			float depthK = fist.Pos.Z / Mathf.Max(0.1f, pose.Pos.Z);                  // 近大远小
			for (int i = 0; i < 3; i++)
			{
				var cp = copies[i];
				float d = s - beat0;
				if (d < 0f || s > pull1) { cp.Root.Visible = false; continue; }
				cp.Root.Visible = true;
				float out1 = Ease5.OutBack(Mathf.Min(1f, d / 0.09f));               // 0.09s 甩到位、冲过头一点再回来
				Vector2 p = cardAt.Lerp(fanAt[i], out1) + new Vector2(0f, Mathf.Sin(s * 4f + i) * h * 0.008f);
				float tilt = fanTilt[i] * out1;
				float flip = 0.5f * (1f - Mathf.Min(1f, out1)) * (i == 1 ? 1f : -1f);
				float sc = Mathf.Lerp(0.3f, 1f, out1) * (1f + 0.12f * Mathf.Exp(-Mathf.Max(0f, d - 0.07f) / 0.05f) * (d > 0.07f ? 1f : 0f));   // 钉住那一下一涨
				float burn = 0f, fade = 1f;
				if (s > pull0)
				{
					// 被拖进拳头里：先被拽一下（慢）、再一口气吸进去（快），跟着拳头近大远小，进去的时候从边上烧掉
					float k = Fx.InCubic(Ease5.Seg(s, pull0 + 0.03f * i, pull1 - pull0 - 0.08f));
					p = fanAt[i].Lerp(fistW, k);
					tilt *= 1f - k;
					sc *= Mathf.Lerp(1f, depthK * 0.7f, k);
					burn = Mathf.Clamp((k - 0.55f) * 2.4f, 0f, 1.2f);
					fade = 1f - Ease5.Seg(k, 0.85f, 0.15f);
				}
				cp.Place(p, tilt, flip, sc);
				cp.Set("glow", d < 0.06f ? 0.45f * (1f - d / 0.06f) : 0f);          // 只白一下（攥紧的顿帧会把这段拉长）
				cp.Set("sheen", Mathf.Clamp(d / 0.3f, 0f, 1f) * 3f - 1f);
				cp.Set("clip", new Vector3(0f, 1f, -1.3f));
				cp.Set("burn", burn);
				cp.Set("alpha", fade);
			}

			// ── 漩涡 ──
			{
				float op = Fx.OutCubic(Mathf.Clamp((s - surge0 + 0.04f) / 0.3f, 0f, 1f));
				float col = Fx.InCubic(Ease5.Seg(s, pull0, pull1 - pull0 + 0.06f));
				vm.SetShaderParameter("open", op * (1f - col) * (1f + 0.04f * Beat(s)));
				vm.SetShaderParameter("spin", fc * (2.4f * (1f - op) + 5f * col * col));
				vm.SetShaderParameter("clock", s);
				vm.SetShaderParameter("pulse", Mathf.Min(1.3f, Beat(s) + 0.8f * Mathf.Exp(-Mathf.Pow((s - grab1 - 0.03f) / 0.07f, 2f))));
				vm.SetShaderParameter("alpha", 1f - Ease5.Seg(s, pull1, 0.1f));
			}
		});

		// ── 攥紧前：四周的光被吸向拳心（蓄力），画面往拳心一缩 ──
		Squeeze(front, cardAt, h, grab0, grab1);
		// ── 攥紧：最重的一拍 ──
		Fx.After(front, grab1, () =>
		{
			HitStop.Request(0.11);
			Fx.Shake(3);
			// 拳心一下白热、再一大团暗红；手指缝里迸出黑红的碎光（四个指缝的方向）
			Glow.Flash(front, cardAt, Pal5.Hot, h * 0.22f, 0.1f);
			Glow.Flash(front, cardAt, new Color(0.9f, 0.1f, 0.15f), h * 0.55f, 0.26f);
			Glow.Flash(back, cardAt, new Color(0.6f, 0.02f, 0.08f), h * 2.2f, 0.5f);
			// 指缝里迸出的暗红火星（四个指缝的方向；不用白色的碎光——一把白线）
			for (int i = 0; i < 4; i++)
			{
				Vector2 dir = Vector2.FromAngle(-Mathf.Pi * 0.5f + (i - 1.5f) * 0.55f);
				Glow.Sparks(front, cardAt + dir * h * 0.12f, 9, 380f * u, 900f * u, 0.32f, new[] { new Color(1f, 0.3f, 0.32f), Pal5.Crimson, new Color(0.3f, 0.02f, 0.06f) }, 500f * u, 0.07f, 0.16f, dir, 14f);
			}
			Glow.Sparks(front, cardAt, 26, 200f * u, 640f * u, 0.5f, new[] { new Color(1f, 0.4f, 0.4f), Pal5.Crimson, new Color(0.25f, 0.02f, 0.05f) }, 600f * u, 0.08f, 0.22f, Vector2.Up, 180f);
			if (c.SelfTint is { } st) Fx.Animate(front, 0, 0.3, t => st(Colors.White.Lerp(new Color(1.3f, 0.75f, 0.8f), (1f - t) * 0.6f)));
		});
		// ── 每一下心跳：拳头上沿一下红光、一圈很淡的波，复制被挤出来 ──
		// 三张复制钉住的那一下：每张身上一下白热 + 一团红光、往外迸的暗红火星，画面轻轻一扭
		Fx.After(front, beat0 + 0.07f, () =>
		{
			HitStop.Request(0.03);
			for (int i = 0; i < 3; i++)
			{
				Vector2 at = fanAt[i];
				Vector2 outDir = (at - cardAt).Normalized();
				Glow.Flash(front, at, Pal5.Hot, h * 0.16f, 0.09f);
				Glow.Flash(front, at, new Color(0.95f, 0.12f, 0.18f), h * 0.42f, 0.24f);
				Glow.Sparks(front, at, 8, 220f * u, 620f * u, 0.3f, new[] { new Color(1f, 0.4f, 0.4f), Pal5.Crimson, new Color(0.3f, 0.02f, 0.06f) }, 300f * u, 0.05f, 0.14f, outDir, 50f);
			}
			Glow.Ripple(front, cardAt + new Vector2(0f, -h * 0.5f), h * 1.3f, 0.3f, 0.02f);
		});
		// ── 原牌落回手里 ──
		Fx.After(front, pull0 + 0.36f, () =>
		{
			Glow.Flash(front, c.Hand, Phantom.Hot, h * 0.18f, 0.1f);
			Glow.Glint(front, c.Hand, 0.6f);
		});
		if (c.SelfTint is { } tint)
			Fx.Animate(front, 0, end, t =>
			{
				float s = t * end;
				float k = 0.4f * Ease5.Seg(s, 0f, 0.25f) * (1f - Ease5.Seg(s, pull1 - 0.1f, 0.4f));
				tint(Colors.White.Lerp(new Color(0.7f, 0.55f, 0.6f), k));
			});
	}
}
