using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 第四版亚森（用户 2026-11-02：Persona 一律生图，不要三渲二）。Codex 的 6 层分层图（art/p5/fx/v4/arsene/layers，layers.json 关节点）
/// 在游戏里实时做 2D 骨骼动画：
/// · 现身：青蓝火焰从脚下往上把他「烧出来」（着色器按高度 + 噪声溶解，溶解边一圈青白火光），身体上浮、两翼张开；
/// · 待机：身体缓慢起伏，两翼轻扇；
/// · 出手（attackAt，游戏的上毒结算被 Aim 推迟到这一刻）：前伸的手臂先往回收（蓄），再猛地探出去，身体前倾、两翼一振、全身白闪一下，眼睛亮；
///   这一刻记下爪尖位置（ClawAt），PoisonFx 从这里把咒怨甩向敌人；
/// · 退场（exitAt）：从头顶往下烧回青焰散掉。
/// 接口照旧版 Persona.Show（Aim 的召唤流程不变：稀有牌照旧有眼神特写 + 舞台）。
/// </summary>
public static class ArseneFx
{
	private const float Pack = 0.35f;                       // 分层图打包时的缩放（tools/p5/stage_p5.py）
	// 第 7 版（art/p5/fx/v4/arsene/layers_v7/layers.json，用户 2026-11-03 通过的修脸版拆层）全图坐标：帽顶、脚尖
	private const float BodyTop = 70f, BodyToe = 2185f;
	private static readonly Vector2 ToeAt = new(955f, 2185f);

	private record struct Part(string Tex, Vector2 Pivot);

	// 从后往前（layers.json 的 z_index）
	private static readonly Part[] Parts =
	{
		new("arsene_wing_back", new(1060f, 790f)),
		new("arsene_wing_front", new(1370f, 745f)),
		new("arsene_arm_back", new(1090f, 665f)),           // 叉腰那只
		new("arsene_body", new(1260f, 1130f)),
		new("arsene_arm_reach", new(1380f, 665f)),          // 抬在胸前那只：出手时朝敌人探出去
		new("arsene_eyes_glow", new(1255f, 508f)),
	};

	private static readonly Vector2 ClawTip = new(1250f, 610f);

	private static Shader? _shader;

	/// <summary>
	/// 合成后的整个亚森（SubViewport 画出来的一张图）上的着色器：
	/// · 人物：reveal 从下往上（退场 top_down 从上往下）按噪声溶解、溶解边一圈青白光；flash 出手一闪；
	/// · P5R 召唤蓝焰（用户 2026-11-03：之前一缕缕小火苗撒上去太松散、看得出是拼的）：火只从**整个剪影**的轮廓往上烧——
	///   每个像素往下找剪影（沿途被噪声左右扭），找到了就有火，越远越弱；两层往上滚的噪声把这片火切成一条条连续的火舌。
	///   所以火是一整团贴着亚森的轮廓、跟着他的动作走；身体里面不画火（显形之前整个剪影先是一团火，再从火里显出来）。
	/// </summary>
	private static Shader PersonaShader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D noise : repeat_enable, filter_linear;
uniform sampler2D lut : repeat_disable, filter_linear;
uniform float clock = 0.0;
uniform float reveal = 1.0;
uniform float top_down = 0.0;
uniform float flash = 0.0;
uniform float fire = 1.0;
uniform float flame_px = 120.0;
uniform vec2 body_y = vec2(0.15, 0.95);
uniform float aura = 0.0;   // 蓝焰强度：只在现身 / 退场时烧（用户 2026-11-03：照 P5 动画觉醒那段的蓝焰），待机时 0
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	vec2 ps = TEXTURE_PIXEL_SIZE;
	float n = texture(noise, UV * vec2(3.0, 2.5)).r;
	float hh = clamp((UV.y - body_y.x) / (body_y.y - body_y.x), 0.0, 1.0);
	float h = mix(1.0 - hh, hh, top_down);
	float d = reveal * 1.3 - 0.15 - (h + (n - 0.5) * 0.28);
	float vis = smoothstep(0.0, 0.02, d);
	float edge = (1.0 - smoothstep(0.0, 0.08, d)) * step(0.0, d) * step(reveal, 0.999) * c.a;

	float n1 = texture(noise, vec2(UV.x * 2.4, UV.y * 1.2 + clock * 0.6)).r;
	float n2 = texture(noise, vec2(UV.x * 6.0 + 0.37, UV.y * 2.8 + clock * 1.4)).r;
	float jit = texture(noise, UV * 37.0).r;
	// 离下方剪影多远（0 = 就在轮廓上，1 = 超出火苗长度）：往下逐步找，沿途被噪声左右扭；采样位置加抖动，免得出现一层层的横纹
	float dist = 1.0;
	for (int i = 0; i < 16; i++) {
		float f = (float(i) + jit) / 16.0;
		// 横向偏移按「往上斜着找」散开（-0.35～0.35 的斜率 + 噪声），否则翅膀尖那一侧的火会是一条竖直的直边
		float slope = (texture(noise, vec2(UV.x * 3.1 + float(i) * 0.13, clock * 0.4)).r - 0.5) * 0.7;
		vec2 o = vec2(((n2 - 0.5) * 26.0 + slope * flame_px) * f, f * flame_px * fire) * ps;
		dist = min(dist, mix(1.0, f, step(0.4, texture(TEXTURE, UV + o).a)));
	}
	// 火舌：横向很密、纵向拉长、往上滚的噪声，减去距离——越往外越要噪声够高才有火，于是边缘碎成一缕缕长短不一的火舌
	// 火舌：P5 动画觉醒那段的蓝焰是又长又尖的叶形火舌、往上猛甩 → 噪声横向更密、纵向拉得更长、滚得更快
	float tn = texture(noise, vec2(UV.x * 6.5 + (n1 - 0.5) * 0.4, UV.y * 0.8 + clock * 2.4)).r;
	// aura 不改透明度、改火舌长短（硬边的火一半透明就发灰）：aura 小 → 要求的噪声更高 → 火舌缩短、收进轮廓
	float v = clamp(tn * 1.6 + 0.15 - dist * 1.9 + (n2 - 0.5) * 0.3 - (1.0 - aura) * 0.9, 0.0, 1.0);
	float body_fire = c.a * (1.0 - vis) * smoothstep(0.25, 0.75, n2 + (n1 - 0.5) * 0.6);   // 显形之前 / 烧掉时：剪影里面是翻滚的蓝火（不是一整块）
	// 硬边：火的外形是一刀切的（赛璐璐），不是柔边半透明
	float flame = max(smoothstep(0.06, 0.09, v), body_fire * smoothstep(0.34, 0.38, n1 + n2 * 0.6 - (1.0 - aura) * 0.8)) * (1.0 - c.a * vis);
	flame *= smoothstep(0.0, 0.06, UV.y);                         // 离屏画面上沿：火舌尖收掉，别被切平
	// 身上的火斑（用户 2026-11-03：火不用大，但各部位都要有、分布均匀）：剪影里面被往上滚的噪声切成一片片尖叶形的火，
	// 盖在头、手臂、翅膀、腿上；aura 越大火斑越多（现身时多，很快退掉露出人物；退场反过来）
	float bn = texture(noise, vec2(UV.x * 7.0 + (n1 - 0.5) * 0.3, UV.y * 2.2 + clock * 2.2)).r;
	float bn2 = texture(noise, vec2(UV.x * 15.0 + 0.5, UV.y * 4.5 + clock * 3.1)).r;
	float pv = bn * 0.8 + bn2 * 0.35 - mix(1.05, 0.8, aura);          // 用户 2026-11-03：火斑太多太密 → 最多盖住约两成
	float patch = c.a * vis * smoothstep(0.0, 0.015, pv) * step(0.001, aura);
	// 颜色：P5 动画的四档硬边分层——贴着轮廓白芯 → 青 → 饱和蓝 → 外缘深海军蓝，档与档之间没有渐变
	float heat = mix(clamp(v * 1.35, 0.0, 1.0), 0.3 + 0.5 * n1, body_fire);
	vec3 fc = heat > 0.8 ? vec3(0.93, 0.98, 1.0) : heat > 0.56 ? vec3(0.4, 0.8, 1.0) : heat > 0.3 ? vec3(0.1, 0.38, 0.92) : vec3(0.03, 0.09, 0.32);
	flame *= step(0.001, aura);                                   // aura = 0 关掉（待机不烧）
	vec3 pc = mix(c.rgb, vec3(1.0, 0.96, 0.96), flash);
	float pa = c.a * vis;
	vec3 pcol = pv > 0.2 ? vec3(0.94, 0.98, 1.0) : pv > 0.1 ? vec3(0.4, 0.8, 1.0) : pv > 0.04 ? vec3(0.1, 0.38, 0.92) : vec3(0.03, 0.09, 0.32);
	pc = mix(pc, pcol, patch);
	vec3 rgb = mix(fc, pc, pa) + vec3(0.45, 0.65, 0.85) * edge * 1.1;
	// 注意：COLOR 的输入已经乘过贴图透明度，剪影外是 0——不能再乘，不然剪影外的火全没了
	COLOR = vec4(rgb, clamp(max(pa, flame) + edge, 0.0, 1.0));
}" };

	private static Shader? _flameShader;

	/// <summary>
	/// 亚森现身 / 退场的一团蓝焰（用户 2026-11-03：照 P5 动画觉醒那段；不要大面积、不要贴着轮廓烧；原版序列帧套蓝色像贴纸）。
	/// 一根独立的火柱：下宽上尖的形状被往上滚的噪声撕成一条条尖叶形的火舌；四档硬边分层（深海军蓝 → 饱和蓝 → 青 → 白芯），
	/// 越往里越亮。life 0→1：0～0.22 猛地烧起来、窜高，0.5 之后从下往上烧散、整体缩回去。
	/// </summary>
	private static Shader FlameShader => _flameShader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D noise : repeat_enable, filter_linear;
uniform float clock = 0.0;
uniform float life = 0.0;
uniform float seed = 0.0;
void fragment() {
	float x = UV.x * 2.0 - 1.0;
	float y = 1.0 - UV.y;
	float grow = smoothstep(0.0, 0.22, life);
	float fade = smoothstep(0.5, 1.0, life);
	float hgt = mix(0.25, 1.0, grow) * (1.0 - 0.45 * fade);
	float n1 = texture(noise, vec2(x * 0.9 + seed, y * 0.7 - clock * 1.6)).r;
	float n2 = texture(noise, vec2(x * 2.6 + seed * 1.7, y * 0.55 - clock * 2.8)).r;
	float n3 = texture(noise, vec2(x * 6.0 - seed, y * 1.4 - clock * 3.6)).r;
	// 越往上左右摆得越厉害；火柱宽度从底到顶收尖
	float xw = x + (n1 - 0.5) * 0.6 * y;
	float yy = clamp(y / max(hgt, 0.01), 0.0, 1.2);
	float wid = 0.62 * pow(max(1.0 - yy, 0.0), 0.75) * mix(0.6, 1.0, grow);
	// 火舌：纵向拉长的噪声把边缘切成尖叶形（越往上越碎）
	float v = wid - abs(xw) + (n2 - 0.5) * (0.25 + 0.55 * yy) + (n3 - 0.5) * 0.12;
	// 底部收边（圆一点），烧散时从下往上被吃掉
	v -= smoothstep(0.12, 0.0, y) * 0.25;
	v -= fade * 0.45 + smoothstep(fade * 1.2 - 0.25, fade * 1.2, y) * 0.0;
	v -= max(0.0, fade * 1.3 - y) * 0.8;
	float a = smoothstep(0.0, 0.012, v);
	vec3 col = v > 0.34 ? vec3(0.94, 0.98, 1.0) : v > 0.2 ? vec3(0.4, 0.8, 1.0) : v > 0.08 ? vec3(0.1, 0.38, 0.92) : vec3(0.03, 0.09, 0.32);
	COLOR = vec4(col, a);
}" };

	/// <summary>放一团蓝焰火柱：toe = 亚森脚尖（屏幕），height = 亚森身高，front = 盖在亚森前面（现身 / 退场时人从火里显出 / 被火吞掉）。</summary>
	private static void SummonFlame(Control back, Vector2 toe, float height, float life = 0.55f)
	{
		Vector2 size = new(height * 0.75f, height * 1.05f);
		ShaderMaterial m = new() { Shader = FlameShader };
		m.SetShaderParameter("noise", Paint.Noise);
		m.SetShaderParameter("seed", Fx.Rand(0f, 10f));
		ColorRect r = new() { Material = m, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore, Name = "P5ArseneFlame" };
		back.AddChild(r);
		r.GlobalPosition = toe + new Vector2(-size.X * 0.5f, -size.Y + 0.05f * height);
		Fx.Clock(r);
		Fx.Animate(r, 0, life, k => m.SetShaderParameter("life", k));
		Fx.FreeAfter(r, life + 0.05);
	}

	/// <summary>现在在场的亚森：出手那一刻的爪尖（全局坐标）和时刻（毫秒）。</summary>
	private static Vector2 _claw;
	private static ulong _clawAt;

	/// <summary>亚森刚出过手（0.6s 内）：返回爪尖位置，PoisonFx 从这里甩出咒怨。</summary>
	public static bool TryClaw(out Vector2 claw)
	{
		claw = _claw;
		return _clawAt != 0 && Stage.NowMs() - _clawAt < 900;
	}

	/// <summary>亚森已经现身、还没出手（到这个时刻为止）：这段时间里的上毒要等他出手、从爪尖甩出去（PoisonFx.Apply）。</summary>
	private static ulong _attackPendingUntil;

	public static bool AttackPending => Stage.NowMs() < _attackPendingUntil;

	/// <summary>
	/// feet = Joker 脚底，height = 亚森身高（屏幕像素），attackAt / exitAt = 现身后几秒出手 / 退场（Persona.Spec）。
	/// </summary>
	/// <summary>
	/// 下一次现身改用「伸手攥拳」（毒性爆发巨手版，用户 2026-11-03）：不扑出去，现身时就把手臂伸向前方、身体基本不动；
	/// attackAt 手臂往前一送（巨手冲出来）→ fistAt 跟巨手一起攥紧、越攥越抖 → crushAt 往前一顶（捏碎）。时刻都从现身算。用一次就清掉。
	/// </summary>
	public static (float FistAt, float CrushAt)? NextClench;

	public static void Show(Control back, Vector2 feet, float height, float facing, float attackAt, float exitAt)
	{
		(float FistAt, float CrushAt)? clench = NextClench;
		NextClench = null;
		_attackPendingUntil = Stage.NowMs() + (ulong)((attackAt + 0.3f) * 1000f);
		float k = height / (BodyToe - BodyTop);                // 全图像素 → 屏幕
		// 飘在 Joker 左后方、离开一段距离，脚尖大约在 Joker 腰的高度（沿用用户对 0.7 版亚森站位的意见：别太近、别太高、再往左）
		Vector2 toeScreen = feet + new Vector2(-0.62f * height * facing, -0.3f * height);   // v7 两翼垂着、扑出去时别压到 Joker 身上
		// 整个亚森画进一张离屏画面（SubViewport），蓝焰在合成后的剪影上烧（见 PersonaShader）。
		// 画面范围（全图坐标）：x -300～2350、y -500～2400，四周留出扑出去的余量
		const float vx0 = -300f, vy0 = -900f, vw = 2650f, vh = 3300f;   // 上面多留 400：现身 / 退场的火舌
		Vector2 vpSize = new Vector2(vw, vh) * k;
		SubViewport vp = new()
		{
			Size = new Vector2I(Mathf.CeilToInt(vpSize.X), Mathf.CeilToInt(vpSize.Y)), TransparentBg = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Disable3D = true, Name = "P5ArseneVp",
		};
		back.AddChild(vp);
		Node2D root = new() { Name = "P5Arsene", Scale = new Vector2(k * facing, k) };
		vp.AddChild(root);
		root.Position = new Vector2(facing > 0f ? -vx0 * k : vpSize.X + vx0 * k, -vy0 * k);
		Vector2 rootScreen = toeScreen - new Vector2(ToeAt.X * k * facing, ToeAt.Y * k);
		ShaderMaterial mat = new() { Shader = PersonaShader };
		mat.SetShaderParameter("noise", Paint.Noise);
		mat.SetShaderParameter("lut", Lut5.PersonaAura);
		mat.SetShaderParameter("reveal", 0f);
		mat.SetShaderParameter("flame_px", 0.26f * height);
		mat.SetShaderParameter("body_y", new Vector2((BodyTop - vy0) / vh, (BodyToe - vy0) / vh));
		Sprite2D display = new() { Texture = vp.GetTexture(), Centered = false, Material = mat, Name = "P5ArseneView" };
		back.AddChild(display);
		display.GlobalPosition = rootScreen - root.Position;
		var nodes = new Node2D[Parts.Length];
		for (int i = 0; i < Parts.Length; i++)
		{
			Node2D joint = new() { Position = Parts[i].Pivot };
			root.AddChild(joint);
			if (Fx.Tex(Parts[i].Tex) is Texture2D tex)
			{
				joint.AddChild(new Sprite2D
				{
					Texture = tex, Centered = false, Scale = Vector2.One / Pack, Offset = -Parts[i].Pivot * Pack,
				});
			}
			nodes[i] = joint;
		}
		Node2D wingB = nodes[0], wingF = nodes[1], armB = nodes[2], body = nodes[3], armR = nodes[4], eyes = nodes[5];
		// 手臂、眼睛跟着身体一起浮动 / 前倾：挂到身体关节下（换算成身体关节的局部坐标）
		foreach (Node2D n in new[] { armB, armR, eyes, wingB, wingF })
		{
			Vector2 p = n.Position - body.Position;
			root.RemoveChild(n);
			body.AddChild(n);
			n.Position = p;
		}
		// 伸手攥拳（毒性爆发）：胸前那只手臂换成伸直张爪的那张（Codex 2026-11-03，layers_v7/arm_reach_extend，同画布同肩点），攥拳时换成拳头那张
		Sprite2D? armSprite = armR.GetChildCount() > 0 ? armR.GetChild(0) as Sprite2D : null;
		Texture2D? fistTex = clench != null ? Fx.Tex("arsene_arm_reach_fist") : null;
		bool fistShown = false;
		if (clench != null && armSprite != null && Fx.Tex("arsene_arm_reach_extend") is Texture2D extendTex)
		{
			armSprite.Texture = extendTex;
		}
		// 叠放顺序（layers_v7 的 z_index）：两只翅膀在身体后面，叉腰的手、胸前的手、眼睛在身体前面
		body.MoveChild(wingB, 0);
		body.MoveChild(wingF, 1);

		// 现身：一团蓝焰（SummonFlame）轰地烧起来，亚森从火里显形；身后一团青光
		Glow.Flash(back, toeScreen + new Vector2(0f, -0.45f * height), new Color(0.3f, 0.8f, 1f), height * 0.8f, 0.4f);

		bool burned = false;

		const float appear = 0.26f, outT = 0.38f;
		float total = exitAt + outT;
		float windUp = Mathf.Max(appear, attackAt - 0.14f);
		bool struck = false;
		// 动作幅度按屏幕像素算再换成局部单位（用户：出手时整个人看上去一点也没动 → 全身扑出去、蓄力要看得见）
		float px = 1f / k;
		Node2D? ghost = null;
		_ = ghost;
		Fx.Animate(display, 0, total, t =>
		{
			float time = t * total;
			// 蓝焰：现身时整个剪影先是一团大火（fire 1.5），火褪成贴身的一圈（0.62，慢慢跳动）；出手一下火一窜；退场又烧旺、把人烧掉
			if (!burned && time >= exitAt)
			{
				burned = true;
				Sfx.Play(Sfx.PersonaFlame, 0.5f);
			}
			float fire = time < 0.6f ? Mathf.Lerp(1.5f, 0.62f, Ease5.OutQuint(time / 0.6f)) : 0.62f + 0.06f * Mathf.Sin(time * 5.3f);
			if (time > exitAt)
			{
				float e = Mathf.Clamp((time - exitAt) / outT, 0f, 1f);
				fire = Mathf.Lerp(0.62f, 1.6f, Mathf.Sqrt(e)) * (1f - e * e);
			}
			mat.SetShaderParameter("clock", time);
			// 现身 / 退场
			float rev = time < appear ? Ease5.OutQuint(time / appear) : time > exitAt ? 1f - Mathf.Clamp((time - exitAt) / outT, 0f, 1f) : 1f;
			mat.SetShaderParameter("reveal", rev);
			mat.SetShaderParameter("top_down", time > exitAt ? 1f : 0f);
			// 现身：从下方 90px 升上来、整体 0.88 → 1、两翼从收拢猛地张开（带一点过冲）
			float inU = Mathf.Min(1f, time / 0.42f);
			Vector2 pos = new(0f, (1f - Ease5.OutQuint(inU)) * 90f * px);
			float scale = Mathf.Lerp(0.88f, 1f, Ease5.OutBack(inU));
			float open = 1f - Ease5.OutBack(Mathf.Min(1f, time / 0.38f));
			// 待机：起伏、两翼慢扇
			pos.Y += Mathf.Sin(time * 3.2f) * 7f * px;
			float flap = Mathf.Sin(time * 4.1f) * 0.05f;
			float lean = 0f, reach = 0f, beat = 0f, wingKick = 0f, stretch = 0f;
			if (clench is (float fistAt, float crushAt))
			{
				// 伸手攥拳：现身的同时把伸直张爪的手臂抬起来指向前方（身体不动）。
				// 用户 2026-11-03：前倾只要一次 → 巨手冲出来时不再往前送，唯一一次往前顶留给捏碎
				float rch = Ease5.OutQuint(Mathf.Clamp((time - 0.04f) / Mathf.Max(0.05f, attackAt - 0.04f), 0f, 1f));
				reach = 0.45f * (1f - rch);
				// 攥紧：手臂往回收一点、整个人绷住，越攥越抖
				if (time >= fistAt && time < crushAt)
				{
					float g = Mathf.Clamp((time - fistAt) / Mathf.Max(0.05f, crushAt - fistAt), 0f, 1f);
					float tight = Ease5.OutQuint(Mathf.Min(1f, (time - fistAt) / 0.08f));
					if (!fistShown && armSprite != null && fistTex != null)
					{
						fistShown = true;
						armSprite.Texture = fistTex;                     // 跟巨手同一刻攥拳
					}
					float amp = (2f + 6f * g * g) * px;
					pos += new Vector2(Mathf.Sin(time * 83f) * amp, Mathf.Cos(time * 67f) * amp * 0.6f);
					reach += Mathf.Sin(time * 71f) * 0.025f * g;
					scale *= 1f - 0.02f * tight;
					wingKick = -0.12f * tight;
				}
				// 捏碎：往前一顶，再慢慢松开回到待机
				if (time >= crushAt)
				{
					float u = time - crushAt;
					float hit = Ease5.OutExpo(Mathf.Min(1f, u / 0.07f));
					float w = 1f - Ease5.OutQuint(Mathf.Clamp((u - 0.12f) / 0.45f, 0f, 1f));
					pos += new Vector2(40f, 6f) * px * hit * w;
					lean += 0.12f * hit * w;
					reach += -0.08f * hit * w;
					wingKick = 0.28f * hit * w;
					stretch = 0.7f * hit * w;
					beat = Mathf.Max(0f, 1f - u / 0.18f);
				}
				if (time >= attackAt && time < crushAt + 0.3f)
				{
					_claw = display.GlobalPosition + armR.GetGlobalTransform() * (ClawTip - Parts[4].Pivot);
					_clawAt = Stage.NowMs();
				}
			}
			else if (time >= windUp && time < attackAt)
			{
				// 蓄：整个人往后收、往上提，手臂拉满，两翼高高扬起
				float u = Ease5.OutQuint((time - windUp) / Mathf.Max(0.01f, attackAt - windUp));
				pos += new Vector2(-45f, -28f) * px * u;
				lean = -0.14f * u;
				reach = 0.5f * u;
				wingKick = -0.3f * u;
				scale *= 1f + 0.04f * u;
			}
			else if (time >= attackAt)
			{
				// 扑：朝敌人方向猛扑出去（OutExpo 0.08s）、前倾、拉伸、手臂甩出、两翼往前一振；再 0.45s 慢慢退回待机
				float u = time - attackAt;
				float hit = Ease5.OutExpo(Mathf.Min(1f, u / 0.08f));
				float rec = Ease5.OutQuint(Mathf.Clamp((u - 0.14f) / 0.45f, 0f, 1f));
				float w = 1f - rec;
				pos += Vector2.Zero.Lerp(new Vector2(70f, 10f), hit) * px * w + new Vector2(-45f, -28f) * px * (1f - hit);
				lean = Mathf.Lerp(-0.14f, 0.2f, hit) * w;
				reach = Mathf.Lerp(0.5f, -0.6f, hit) * w;
				wingKick = Mathf.Lerp(-0.3f, 0.32f, hit) * w;
				stretch = hit * w;
				scale *= 1f + 0.08f * hit * w;
				beat = Mathf.Max(0f, 1f - u / 0.18f);
				if (!struck)
				{
					struck = true;
					// 原地留一道红色残影（蓄满那一刻的姿势），读起来是「扑出去」
					Sprite2D snap = new()
					{
						Texture = ImageTexture.CreateFromImage(vp.GetTexture().GetImage()), Centered = false,
						Modulate = new Color(1f, 0.25f, 0.3f, 0.55f), Name = "P5ArseneGhost",
					};
					back.AddChild(snap);
					back.MoveChild(snap, display.GetIndex());
					snap.GlobalPosition = display.GlobalPosition;
					ghost = snap;
					ghost.CreateTween().TweenProperty(ghost, "modulate:a", 0f, 0.3);
					Fx.FreeAfter(ghost, 0.35);
					Sfx.Play(Sfx.Curse, 0.45f);
					Sfx.Play(Sfx.SubBoom, 0.4f);
				}
				if (u < 0.3f)
				{
					_claw = display.GlobalPosition + armR.GetGlobalTransform() * (ClawTip - Parts[4].Pivot);   // 爪尖：离屏画面坐标 → 屏幕
					_clawAt = Stage.NowMs();
				}
			}
			body.Position = Parts[3].Pivot + pos;
			body.Rotation = lean;
			body.Scale = new Vector2(scale * (1f + 0.1f * stretch), scale * (1f - 0.05f * stretch));
			armR.Rotation = reach;
			armB.Rotation = -reach * 0.35f + Mathf.Sin(time * 2.6f) * 0.04f;
			wingB.Rotation = -0.6f * open + flap + wingKick;
			wingF.Rotation = 0.6f * open - flap - wingKick * 1.2f;
			mat.SetShaderParameter("flash", beat * beat * 0.3f);
			// 现身 / 退场的蓝焰（P5 动画觉醒那段）：现身时整团火把剪影吞没、火舌拉到很长，0.25s 起退开露出人物，0.55s 收完；
			// 退场时火重新窜起来把人烧掉。待机不烧（用户之前嫌待机的火艳、散）
			float burst = time < 0.55f ? 1f - Mathf.SmoothStep(0.22f, 0.55f, time) : 0f;
			float fireLen = 0.35f + 0.25f * burst;           // 用户：不要大面积 → 轮廓上只冒短火舌
			if (time > exitAt)
			{
				float e = Mathf.Clamp((time - exitAt) / outT, 0f, 1f);
				burst = Mathf.Max(burst, Mathf.Min(1f, e * 4f) * (1f - e * e));
				fireLen = 0.35f + 0.25f * Mathf.Min(1f, e * 3f);
			}
			mat.SetShaderParameter("aura", burst);
			mat.SetShaderParameter("fire", fireLen + beat * 0.35f);       // 出手一闪（平方衰减：顿帧拖慢时不会一直发灰）
			eyes.Modulate = new Color(1f + beat * 3f, 1f + beat * 1.5f, 1f + beat * 1.5f, 1f);
		});
		Fx.FreeAfter(display, total + 0.05);
		Fx.FreeAfter(vp, total + 0.1);
	}
}
