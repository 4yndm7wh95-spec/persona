using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 刀刃陷阱（把消耗牌堆里的小刀全部对一个敌人打出）第三版「时钟陷阱」（2026-11-18，照用户 0.9.49 审阅写的方案）：
/// · 切入：从被选中的敌人身上推出一道透明的波，背景褪成冷灰（红色不褪；人物和特效保持原色）；他身后迸出一圈红黑白碎片、飞出去就冻在半空（子弹时间的语言）；
///   以敌人为中心展开一个巨大的透明时钟（着色器画的玻璃表盘：外沿厚玻璃边、一百二十道细刻度、六十道分刻度、十二道红色时刻度、红秒针），一直在转；
/// · 表盘外沿游离着预告信小卡（消耗牌堆里有几张小刀就有几张，最多显示 24 张），一张张在表盘边上闪现，各自一直自转；
/// · 每开一枪（GunFx.Hit 打中这个敌人）：子弹穿过表盘靠枪口那一侧，那一侧的卡被打爆（白闪、撕开烧掉、纸屑顺着弹道喷出去）；
///   表盘的旋转加速一下（而且越打越快）、所有卡的自转也加速一下、红秒针往前跳一格、表盘亮一下；
///   小刀超过 24 张：多出来的在打爆一张之后补到空出来的位置上（卡从表盘边上闪现）；
/// · 切出（最后一枪 / 超时）：表盘碎成玻璃碎片往外迸、大闪光、颜色从敌人那里涌回去、他身后冻住的碎片接着飞散落下。
/// </summary>
public static class KnifeTrapFx
{
	/// <summary>切入（时钟展开 + 卡全部现身）要多久：游戏推迟这么久再开始打（Aim.HoldCast）。</summary>
	public const float EntryTime = 0.62f;
	/// <summary>旧接口：每张卡现身的间隔（JokerFx 算等待时间用）。</summary>
	public const float Interval = 0.02f;
	private const int MaxShown = 24;

	private sealed class Slot
	{
		public Phantom.P5Card? Card;
		public float Off, Born, Seed, Spin;
		public bool Dead;
	}

	private sealed class Bit
	{
		public Node2D Node = null!;
		public Vector2 Pos, Vel;
		public float Rot, SpinV, Life = -1f;
	}

	private static readonly List<Slot> _slots = new();
	private static readonly List<Bit> _bits = new();
	private static Control? _front, _back;
	private static Vector2 _center, _muzzle, _joker;
	private static float _h, _w, _R, _cw, _tall;
	private static int _left, _pending, _shots;
	private static double _t0;
	private static float _t, _last, _rot, _spinPhase, _hand, _kickAt = -10f, _endAt = -1f, _lastHitAt;
	/// <summary>每一枪：什么时候、表盘转多大一格、卡转多少（用户 2026-11-18：每开一枪转一下、停顿一下再接着转，不要连成一直在转）。</summary>
	private static readonly List<(float At, float Dial, float Spin)> _steps = new();

	/// <summary>一格：0.15s 内转到位，过冲一点点再回来，然后停住。</summary>
	private static float Spring(float s) => s <= 0f ? 0f : 1f - Mathf.Exp(-s * 20f) * Mathf.Cos(s * 30f);
	private static ShaderMaterial? _clockM, _worldM;
	private static Node2D? _clockRoot, _worldRoot, _bitsRoot;
	private static SceneTree? _tree;
	private static bool _hooked;
	private static ulong _until;

	private static Shader? _clockShader;

	/// <summary>透明的大表盘（预乘混合：表盘里很淡的冷色压暗 + 加上去的玻璃高光、刻度、红秒针）。UV 铺满一个正方形，半径 0.92。</summary>
	private static Shader ClockShader => _clockShader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float rot = 0.0;
uniform float hand = 0.0;
uniform float open = 1.0;
uniform float alpha = 1.0;
uniform float kick = 0.0;
uniform float shatter = 0.0;
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
void fragment() {
	vec2 p = UV * 2.0 - 1.0;
	float R = 0.72 * max(open, 0.001);          // 留出 r 到 1.3 的余量（外沿的红晕、碎散）
	vec2 q = p / R;
	float r = length(q);
	if (r > 1.3) discard;
	float ang = atan(q.x, -q.y);                // 0 = 12 点，顺时针
	float a = ang - rot;
	// 碎散：按噪声一块块化掉
	float sh = vn(q * 5.0 + 3.1) * 0.8 + vn(q * 13.0) * 0.2;
	float vis = 1.0 - smoothstep(sh - 0.1, sh + 0.02, shatter * 1.15);
	if (vis <= 0.001) discard;
	vec2 n = q / max(r, 1e-4);
	float lit = max(0.0, dot(n, normalize(vec2(-0.6, -0.8))));
	float back = max(0.0, dot(n, normalize(vec2(0.6, 0.8))));
	// 玻璃：表盘里很淡的冷色（一点点压暗），外沿厚边受光面亮，里面两道细的玻璃环
	float inside = smoothstep(1.0, 0.97, r);
	float rim = exp(-pow((r - 1.0) / 0.028, 2.0));
	float rim2 = exp(-pow((r - 0.86) / 0.007, 2.0));
	float rim3 = exp(-pow((r - 0.6) / 0.006, 2.0));
	float spec = rim * (0.1 + 0.75 * pow(lit, 2.0) + 0.25 * pow(back, 3.0)) + rim2 * (0.12 + 0.3 * lit) + rim3 * 0.1;
	// 刻度：一百二十道细的、六十道长一点的（外圈），十二道红色时刻度（3、6、9、12 更长更粗）
	float t120 = abs(fract(a / 6.28318 * 120.0 + 0.5) - 0.5) / 120.0 * 6.28318 * r;
	float m120 = smoothstep(0.0032, 0.001, t120) * smoothstep(0.885, 0.895, r) * smoothstep(0.955, 0.945, r);
	float t60 = abs(fract(a / 6.28318 * 60.0 + 0.5) - 0.5) / 60.0 * 6.28318 * r;
	float m60 = smoothstep(0.0048, 0.0018, t60) * smoothstep(0.87, 0.88, r) * smoothstep(0.972, 0.962, r);
	float fi = a / 6.28318 * 12.0;
	float idx = floor(fi + 0.5);
	bool major = mod(idx + 12.0, 3.0) < 0.5;
	float a12 = (fi - idx) / 12.0 * 6.28318 * r;
	float tw = major ? 0.02 : 0.012, tin = major ? 0.64 : 0.7;
	float m12 = smoothstep(tw, tw * 0.4, abs(a12)) * smoothstep(tin, tin + 0.01, r) * smoothstep(0.84, 0.83, r);
	// 里圈一道慢慢反着转的细刻度环
	float ai = ang + rot * 0.6;
	float t48 = abs(fract(ai / 6.28318 * 48.0 + 0.5) - 0.5) / 48.0 * 6.28318 * r;
	float m48 = smoothstep(0.004, 0.0015, t48) * smoothstep(0.52, 0.53, r) * smoothstep(0.58, 0.57, r);
	// 红秒针 + 钟心
	vec2 hd = vec2(sin(hand), -cos(hand));
	float al = dot(q, hd), pp = dot(q, vec2(-hd.y, hd.x));
	float ww = 0.012 * (1.0 - 0.6 * clamp(al / 0.92, 0.0, 1.0)) + 0.002;
	float mh = smoothstep(ww, ww * 0.35, abs(pp)) * smoothstep(-0.2, -0.17, al) * smoothstep(0.93, 0.89, al);
	float hub = smoothstep(0.04, 0.028, r);
	vec3 white = vec3(0.86, 0.86, 0.92), red = vec3(1.0, 0.12, 0.18);
	vec3 add = white * (spec * 0.55 + m120 * 0.32 + m60 * 0.45 + m48 * 0.22) * (1.0 + 0.6 * kick)
		+ red * (m12 * 0.85 + mh * 1.05 + hub * 0.9)
		+ red * (rim + rim2) * kick * 0.7;
	// 外沿一圈很淡的红晕
	add += vec3(0.75, 0.04, 0.09) * exp(-max(r - 1.0, 0.0) / 0.05) * step(1.0, r) * (0.12 + 0.35 * kick);
	float body = inside * 0.14;
	vec3 tint = vec3(0.015, 0.02, 0.035) * body;
	add *= vis * alpha;
	body *= vis * alpha;
	COLOR = vec4(tint * vis * alpha + add, body);
}" };

	private static Vector2 RimAt(float ang, float k = 1.1f) => _center + new Vector2(Mathf.Sin(ang), -Mathf.Cos(ang)) * _R * k;

	/// <summary>打出刀刃陷阱：count = 消耗牌堆里的小刀张数。joker / tall = Joker 胸口和身高（他身后的碎片用）。</summary>
	public static void Begin(Control front, Control back, Vector2 muzzle, Vector2 center, float th, float tw, int count, Node2D? body,
		Vector2? joker = null, float tall = 0f)
	{
		Clear();
		float u = Fx.Screen(front).Y / 1080f;
		th = Mathf.Max(th, 120f * u);
		tw = Mathf.Max(tw, th * 0.6f);
		_front = front;
		_back = back;
		_center = center;
		_muzzle = muzzle;
		_h = th;
		_w = tw;
		_tall = tall > 1f ? tall : th * 1.3f;
		_joker = joker ?? muzzle + new Vector2(-0.4f * _tall, 0.1f * _tall);
		_R = Mathf.Max(th * 1.2f, tw * 0.95f);        // 巨大的表盘：直径约 2.4 倍敌人身高
		_cw = Mathf.Clamp(_R * 0.19f, 40f * u, 92f * u);
		_left = Mathf.Max(0, count);
		int shown = Mathf.Min(_left, MaxShown);
		_pending = _left - shown;
		_shots = 0;
		_rot = 0f; _spinPhase = 0f; _hand = 0f; _kickAt = -10f; _endAt = -1f;
		_steps.Clear();
		_t0 = HitStop.RealSeconds();
		_t = 0f; _last = 0f; _lastHitAt = 0f;
		_until = Stage.NowMs() + (ulong)((6f + 0.7f * _left) * 1000f);

		// ── 背景褪成冷灰：从敌人身上推出去（背景层最底下：人物、特效都在它上面，保持原色）──
		Vector2 scr = Fx.Screen(back);
		_worldRoot = Fx.ScreenRoot(back, "P5TrapWorld");
		back.MoveChild(_worldRoot, 0);
		_worldRoot.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		_worldM = new ShaderMaterial { Shader = HuntFx.Freeze };
		_worldRoot.AddChild(new ColorRect { Size = scr, Material = _worldM, MouseFilter = Control.MouseFilterEnum.Ignore });
		_worldM.SetShaderParameter("origin", Fx.ScreenPoint(back, center) / scr);
		_worldM.SetShaderParameter("keep", new Vector2(-9f, -9f));
		_worldM.SetShaderParameter("keep_r", 0.0001f);
		_worldM.SetShaderParameter("aspect", scr.X / scr.Y);
		_worldM.SetShaderParameter("radius", 0.001f);
		_worldM.SetShaderParameter("amount", 0f);

		// ── 透明大表盘（敌人身后）──
		_clockRoot = Fx.Root(back, center, "P5TrapClock", 600.0);
		float qs = _R * 2f / 0.72f;
		_clockM = new ShaderMaterial { Shader = ClockShader };
		_clockRoot.AddChild(new ColorRect { Size = Vector2.One * qs, Position = -Vector2.One * qs * 0.5f, Material = _clockM, MouseFilter = Control.MouseFilterEnum.Ignore });
		_clockM.SetShaderParameter("open", 0.2f);
		_clockM.SetShaderParameter("alpha", 0f);
		// 表盘尺寸按 quad 换算：shader 里半径 0.72（相对 quad 半边）对应 _R
		_clockRoot.Scale = Vector2.One * 0.25f;

		// ── 预告信小卡：表盘边上一张张闪现 ──
		for (int i = 0; i < shown; i++)
		{
			float off = Mathf.Tau * i / Mathf.Max(1, shown);
			var sl = new Slot { Off = off, Born = 0.28f + 0.3f * i / Mathf.Max(1, shown), Seed = Fx.Rand(0f, 6f), Spin = Fx.Rand(0f, Mathf.Tau) };
			sl.Card = Phantom.Card(front, RimAt(off), _cw, 600.0);
			sl.Card.Root.Visible = false;
			_slots.Add(sl);
		}

		// ── 他身后：一圈红黑白碎片迸出去、冻在半空（子弹时间的语言）──
		_bitsRoot = new Node2D { Name = "P5TrapBits" };
		back.AddChild(_bitsRoot);
		float uu = _tall / 360f;
		for (int i = 0; i < 18; i++)
		{
			float ang = Mathf.Tau * (i + Fx.Rand(-0.35f, 0.35f)) / 18f;
			Vector2 d = Vector2.Right.Rotated(ang);
			AddSliver(_joker + new Vector2(d.X * 0.12f, d.Y * 0.28f) * _tall, d * Fx.Rand(900f, 1400f) * uu, Fx.Rand(9f, 20f) * uu, i % 3);
		}
		Glow.Flash(back, _joker, new Color(0.7f, 0.04f, 0.1f), _tall * 1.6f, 0.6f);
		SkillRedo5.Dim(back, center, new Vector2(Mathf.Max(_R * 3.4f, tw * 3f), _R * 3f), 8.0 + 0.7 * _left, s => _endAt < 0f ? Fx.OutCubic(Ease5.Seg(s, 0f, 0.3f)) * 0.5f : 0.5f * (1f - Ease5.Seg(_t - _endAt, 0f, 0.5f)));
		BodyTint.Pulse(body, new Color(0.7f, 0.5f, 0.55f), 0.1, 0.5);

		Sfx.Take("gx_tock", 0.4f);
		Sfx.Take("gx_freeze", 0.2f, 0.02f);
		Sfx.Take("gx_fan_open", 0.16f, 0.26f);
		Sfx.Take("gx_ping", 0.14f, EntryTime - 0.04f);

		_tree = (SceneTree)Engine.GetMainLoop();
		if (!_hooked) { _tree.ProcessFrame += Tick; _hooked = true; }
	}

	/// <summary>一片尖碎片：kind 0 红、1 黑、2 白。</summary>
	private static void AddSliver(Vector2 at, Vector2 v, float size, int kind)
	{
		if (_bitsRoot == null) return;
		Node2D n = new();
		_bitsRoot.AddChild(n);
		float w = size, h = size * Fx.Rand(0.22f, 0.4f), k = Fx.Rand(-0.3f, 0.3f);
		Vector2[] tri = { new(-w, -h * 0.4f + k * h), new(w * 1.1f, 0f), new(-w * 0.7f, h) };
		Color c = kind switch { 0 => Phantom.Red, 1 => new Color(0.05f, 0.03f, 0.04f), _ => new Color(0.92f, 0.9f, 0.88f) };
		Fx.Poly(n, tri, c);
		_bits.Add(new Bit { Node = n, Pos = at, Vel = v, Rot = Fx.Rand(0f, Mathf.Tau), SpinV = Fx.Rand(6f, 14f) * (Fx.Rand(0f, 1f) < 0.5f ? -1f : 1f) });
		n.Position = at;
	}

	private static void Tick() => Stage.Safe("KnifeTrap", Step);

	private static void Step()
	{
		if (_front == null || !GodotObject.IsInstanceValid(_front) || _clockRoot == null || !GodotObject.IsInstanceValid(_clockRoot))
		{
			Clear();
			return;
		}
		float now = (float)(HitStop.RealSeconds() - _t0);
		float dt = Mathf.Clamp(now - _last, 0f, 0.1f);
		_last = now;
		_t = now;
		// 超时（游戏没打到这里 / 打完了没收到最后一枪）：自己收
		if (_endAt < 0f && (Stage.NowMs() > _until || (_shots > 0 && _t - _lastHitAt > 2.6f))) End();

		// ── 旋转：一枪一格（转到位、回弹一下、停住，等下一枪）；越往后每格转得越大 ──
		float kick = _t >= _kickAt ? Mathf.Exp(-(_t - _kickAt) * 4f) : 0f;
		_rot = -1.4f * (1f - Fx.OutCubic(Mathf.Min(1f, _t / 0.42f)));     // 展开的时候拧到位
		_spinPhase = 0.35f * _t;                                           // 卡平时只极慢地自转
		_hand = 0f;
		foreach (var (at, dial, spin) in _steps)
		{
			float k = Spring(_t - at);
			_rot += dial * k;
			_spinPhase += spin * k;
			_hand += Mathf.Tau / 12f * k;
		}
		float endK = _endAt < 0f ? 0f : _t - _endAt;

		// 表盘
		float open = Ease5.OutBack(Mathf.Clamp(_t / 0.36f, 0f, 1f));
		_clockRoot.Scale = Vector2.One * (0.25f + 0.75f * Mathf.Clamp(open, 0f, 1.2f)) * (1f + 0.03f * kick + 0.08f * Fx.OutCubic(Mathf.Min(1f, endK / 0.3f)));
		_clockM!.SetShaderParameter("open", 1f);
		_clockM.SetShaderParameter("rot", _rot);
		_clockM.SetShaderParameter("hand", _hand);
		_clockM.SetShaderParameter("kick", kick);
		_clockM.SetShaderParameter("alpha", Mathf.Min(1f, _t / 0.15f));
		_clockM.SetShaderParameter("shatter", _endAt < 0f ? 0f : Mathf.Clamp(endK / 0.35f, 0f, 1f));

		// 背景褪色：切入 0.3s 推出去，切出 0.45s 退回来
		if (_worldM != null && _worldRoot != null && GodotObject.IsInstanceValid(_worldRoot))
		{
			Vector2 scr = Fx.Screen(_front);
			float reach = scr.X / scr.Y * 1.4f;
			float r = reach * (1f - Mathf.Pow(1f - Mathf.Min(1f, _t / 0.3f), 2f));
			Vector2 o = Fx.ScreenPoint(_front, _center) / scr;
			_worldM.SetShaderParameter("radius", r + 0.001f);
			_worldM.SetShaderParameter("amount", Mathf.Min(1f, _t / 0.05f));
			_worldM.SetShaderParameter("origin", o);
			// 切出：颜色从敌人身上往外涌回去（保留原色的那一圈从敌人身上张开）
			_worldM.SetShaderParameter("keep", o);
			_worldM.SetShaderParameter("keep_r", _endAt < 0f ? 0.0001f : reach * 1.3f * Fx.OutCubic(Mathf.Clamp(endK / 0.5f, 0f, 1f)) + 0.0001f);
		}

		// 卡：沿表盘外沿跟着转，各自自转；刚现身闪一下
		foreach (Slot sl in _slots)
		{
			if (sl.Card == null || !GodotObject.IsInstanceValid(sl.Card.Root)) continue;
			if (sl.Dead) continue;
			float a = sl.Off + _rot;
			float age = _t - sl.Born;
			if (age < 0f) { sl.Card.Root.Visible = false; continue; }
			sl.Card.Root.Visible = true;
			float pop = Ease5.OutBack(Mathf.Min(1f, age / 0.2f));
			float bob = Mathf.Sin(_t * 2.2f + sl.Seed) * 0.02f;
			Vector2 pos = RimAt(a, 1.1f + bob + 0.04f * kick) ;
			float fade = _endAt < 0f ? 1f : 1f - Mathf.Clamp(endK / 0.2f, 0f, 1f);
			// 自转：在画面里转（不绕竖轴翻——翻到侧面会变成一根细白线，24 张一起转就是一把白线），带一点立体的摆动
			float spinA = sl.Spin + _spinPhase * (0.8f + 0.2f * Mathf.Sin(sl.Seed));
			sl.Card.Place(pos, a + spinA, 0.7f * Mathf.Sin(spinA * 0.5f + sl.Seed), (0.3f + 0.7f * pop) * (1f + 0.06f * kick));
			sl.Card.Set("glow", Mathf.Exp(-age * 8f) * 0.8f + 0.1f);
			sl.Card.Set("sheen", Mathf.PosMod(_t * 0.5f + sl.Seed, 2.5f) - 1f);
			sl.Card.Set("alpha", fade);
		}

		// 他身后的碎片：迸出去、冻住（极慢地转）；收尾时接着飞、落下、淡掉
		float uu = _tall / 360f;
		for (int i = _bits.Count - 1; i >= 0; i--)
		{
			Bit b = _bits[i];
			if (!GodotObject.IsInstanceValid(b.Node)) { _bits.RemoveAt(i); continue; }
			if (_endAt >= 0f && b.Life < 0f)
			{
				b.Life = 0f;
				b.Vel = (b.Pos - _joker).Normalized() * 520f * uu + new Vector2(0f, -140f * uu);
			}
			if (b.Life < 0f)
			{
				b.Vel *= Mathf.Exp(-9f * dt);                    // 飞一小段就急停
				b.Pos += b.Vel * dt + new Vector2(0f, Mathf.Sin(_t * 1.3f + i) * 3f * uu * dt);
				b.Rot += b.SpinV * dt * (0.04f + Mathf.Min(1f, b.Vel.Length() / (300f * uu)));
			}
			else
			{
				b.Life += dt;
				b.Vel += new Vector2(0f, 1600f * uu) * dt;
				b.Pos += b.Vel * dt;
				b.Rot += b.SpinV * dt;
				b.Node.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Clamp((b.Life - 0.2f) / 0.4f, 0f, 1f));
				if (b.Life > 0.65f) { b.Node.QueueFree(); _bits.RemoveAt(i); continue; }
			}
			b.Node.Position = b.Pos;
			b.Node.Rotation = b.Rot;
			b.Node.Scale = new Vector2(1f, 0.35f + 0.65f * Mathf.Abs(Mathf.Cos(b.Rot * 1.3f)));
		}

		if (_endAt >= 0f && endK > 0.9f) Clear();
	}

	/// <summary>一发小刀打中（GunFx.Hit）：陷阱还在、打的是这个敌人 → 枪口那一侧的卡被打爆，表盘和卡加速，多出来的补位；最后一枪收尾。</summary>
	public static void OnHit(Vector2 at)
	{
		if (_endAt >= 0f || _front == null || !GodotObject.IsInstanceValid(_front) || _clockRoot == null) return;
		if (at.DistanceTo(_center) > Mathf.Max(_h, _w) * 1.3f) return;
		Control f = _front;
		Control b = _back != null && GodotObject.IsInstanceValid(_back) ? _back : f;
		float u = Fx.Screen(f).Y / 1080f;
		_shots++;
		_left--;
		_lastHitAt = _t;
		_until = Stage.NowMs() + 4000;
		// 这一枪：表盘转一格（越往后越大：18° → 54°）、所有卡跟着猛转一下（越往后越多）、秒针跳一格，转到位就停住
		int n = _steps.Count;
		_steps.Add((_t, Mathf.DegToRad(Mathf.Min(18f + 5f * n, 54f)), Mathf.Pi * Mathf.Min(0.6f + 0.1f * n, 1.6f)));
		_kickAt = _t;
		// 子弹穿过表盘靠枪口那一侧：挑离那个方向最近、还在的卡
		Vector2 toMuzzle = _muzzle - _center;
		float target = Mathf.Atan2(toMuzzle.X, -toMuzzle.Y);
		Slot? pick = null;
		float best = 99f;
		foreach (Slot sl in _slots)
		{
			if (sl.Dead || sl.Card == null || !GodotObject.IsInstanceValid(sl.Card.Root) || !sl.Card.Root.Visible) continue;
			float d = Mathf.Abs(Mathf.AngleDifference(sl.Off + _rot, target));
			if (d < best) { best = d; pick = sl; }
		}
		Vector2 dir = (_center - _muzzle).Normalized();
		Glow.Flash(b, _center, new Color(0.85f, 0.06f, 0.12f), _R * 2.2f, 0.25f);
		if (pick?.Card is { } card)
		{
			pick.Dead = true;
			Node2D cr = card.Root;
			Vector2 from = cr.GlobalPosition;
			float rot0 = cr.Rotation, sc0 = cr.Scale.Y;
			// 被子弹打爆：白闪、撕开烧掉、纸屑和碎光顺着弹道喷出去
			Vector2 cut = dir.Orthogonal();
			card.Set("clip", new Vector3(cut.X, cut.Y, 0f));
			Fx.Animate(cr, 0, 0.14, t =>
			{
				if (!GodotObject.IsInstanceValid(cr)) return;
				card.Place(from + dir * _cw * 0.6f * Fx.OutCubic(t), rot0 + t * 1.2f, 0f, sc0 * (1f + 0.4f * t));
				card.Set("glow", 0.8f * (1f - t));
				card.Set("burn", Fx.OutCubic(t) * 1.2f);
				card.Set("alpha", 1f - Ease5.Seg(t, 0.5f, 0.5f));
			});
			Fx.FreeAfter(cr, 0.16);
			Glow.Flash(f, from, new Color(1f, 0.95f, 0.92f), _cw * 2.6f, 0.12f);
			Glow.Flash(b, from, new Color(0.95f, 0.1f, 0.16f), _cw * 4.5f, 0.3f);
			Phantom.Confetti(f, from, 14, 520f * u, dir, 55f, 0.7f, 7f * u);
			Phantom.Shards(f, from, 8, 300f * u, 800f * u, dir, 40f, 0.3f, 200f * u, 12f * u);
			Sfx.Take("gx_slice", 0.14f);
			// 多出来的小刀：补到这个空位上
			if (_pending > 0)
			{
				_pending--;
				Slot sl = pick;
				Fx.After(f, 0.16f, () =>
				{
					if (_endAt >= 0f || _front == null || !GodotObject.IsInstanceValid(_front)) return;
					sl.Card = Phantom.Card(_front, RimAt(sl.Off + _rot), _cw, 600.0);
					sl.Dead = false;
					sl.Born = _t;
					Glow.Flash(_front, RimAt(sl.Off + _rot), new Color(1f, 0.92f, 0.9f), _cw * 1.8f, 0.12f);
					Sfx.Take("gx_card_slide", 0.08f);
				});
			}
		}
		if (_left <= 0) Fx.After(f, 0.06f, End);
	}

	/// <summary>切出：表盘碎成玻璃迸开、大闪光、颜色从敌人那里涌回去、他身后的碎片接着飞。</summary>
	private static void End()
	{
		if (_endAt >= 0f || _front == null || !GodotObject.IsInstanceValid(_front)) return;
		_endAt = _t;
		Control f = _front;
		Control b = _back != null && GodotObject.IsInstanceValid(_back) ? _back : f;
		float u = Fx.Screen(f).Y / 1080f;
		Vector2 c = _center;
		Fx.Shake(2);
		HitStop.Request(0.05);
		Sfx.Take("gx_glass", 0.4f);
		Sfx.Take("gx_tock", 0.4f, 0.02f);
		Sfx.Take("gx_whoosh", 0.18f, 0.04f);
		Glow.Flash(f, c, new Color(1f, 0.95f, 0.92f), _R * 1.2f, 0.16f);
		Glow.Flash(b, c, new Color(0.9f, 0.08f, 0.14f), _R * 3.4f, 0.5f);
		// 玻璃碎片从表盘上往外迸
		if (_bitsRoot != null && GodotObject.IsInstanceValid(_bitsRoot))
		{
			for (int i = 0; i < 30; i++)
			{
				float a = Fx.Rand(0f, Mathf.Tau);
				float rr = Mathf.Sqrt(Fx.Rand(0.2f, 1f)) * _R;
				Vector2 d = new(Mathf.Sin(a), -Mathf.Cos(a));
				Vector2 at = c + d * rr;
				Node2D n = new();
				_bitsRoot.AddChild(n);
				float sz = Fx.Rand(10f, 26f) * u;
				Vector2[] tri = { new(-sz, -sz * Fx.Rand(0.2f, 0.6f)), new(sz * Fx.Rand(0.6f, 1.1f), -sz * 0.1f), new(-sz * 0.3f, sz * Fx.Rand(0.5f, 0.9f)) };
				Fx.Poly(n, tri, new Color(0.78f, 0.85f, 0.95f, 0.18f));
				Fx.Poly(n, new[] { tri[0], tri[1], tri[0].Lerp(tri[1], 0.5f) + new Vector2(0f, sz * 0.12f) }, new Color(1f, 0.97f, 0.96f, 0.85f), additive: true);
				if (i % 4 == 0) Fx.Poly(n, new[] { tri[1], tri[2], tri[1].Lerp(tri[2], 0.5f) }, new Color(1f, 0.15f, 0.2f, 0.7f), additive: true);
				_bits.Add(new Bit { Node = n, Pos = at, Vel = d * Fx.Rand(300f, 760f) * u * (0.6f + rr / _R) + new Vector2(0f, -160f * u), Rot = Fx.Rand(0f, Mathf.Tau), SpinV = Fx.Rand(6f, 16f), Life = 0f });
				n.Position = at;
			}
		}
		Phantom.Confetti(f, c, 30, 640f * u, Vector2.Up, 180f, 1.1f, 9f * u);
		// 剩下的卡（没打完就收尾）一起烧掉
		foreach (Slot sl in _slots)
		{
			if (sl.Dead || sl.Card == null || !GodotObject.IsInstanceValid(sl.Card.Root)) continue;
			sl.Card.Set("clip", new Vector3(0f, -1f, -1.2f));
			Phantom.P5Card cc = sl.Card;
			Fx.Animate(cc.Root, 0, 0.3, t => cc.Set("burn", t * 1.2f));
		}
	}

	private static void Clear()
	{
		foreach (Slot sl in _slots)
			if (sl.Card != null && GodotObject.IsInstanceValid(sl.Card.Root)) sl.Card.Root.QueueFree();
		_slots.Clear();
		foreach (Bit b in _bits) if (GodotObject.IsInstanceValid(b.Node)) b.Node.QueueFree();
		_bits.Clear();
		foreach (Node? n in new Node?[] { _clockRoot, _worldRoot, _bitsRoot })
			if (n != null && GodotObject.IsInstanceValid(n)) n.QueueFree();
		_clockRoot = null; _worldRoot = null; _bitsRoot = null; _clockM = null; _worldM = null;
		_endAt = -1f;
		if (_hooked && _tree != null) { _tree.ProcessFrame -= Tick; _hooked = false; }
	}
}
