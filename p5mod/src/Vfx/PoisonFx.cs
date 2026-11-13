using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 被打的人物本体的「原色」：受击闪、中毒染色都从这里恢复，几个特效同时改 modulate 也不会把颜色卡在半路。
/// </summary>
public static class BodyTint
{
	private static readonly Dictionary<ulong, Color> Rest = new();

	public static Color RestOf(Node2D body)
	{
		ulong id = body.GetInstanceId();
		if (!Rest.TryGetValue(id, out Color c))
		{
			c = body.Modulate;
			if (Rest.Count > 64)
			{
				Rest.Clear();
			}
			Rest[id] = c;
		}
		return c;
	}

	/// <summary>染成 tint（乘在原色上）停 hold 秒，再用 fade 秒回到原色。真实时间，不受顿帧影响。</summary>
	public static void Pulse(Node2D? body, Color tint, double hold, double fade)
	{
		if (body == null || !GodotObject.IsInstanceValid(body))
		{
			return;
		}
		Color rest = RestOf(body);
		Tween tw = body.CreateTween().SetIgnoreTimeScale(true);
		tw.TweenProperty(body, "modulate", rest * tint, 0.06);
		tw.TweenInterval(hold);
		tw.TweenProperty(body, "modulate", rest, fade).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
	}
}

/// <summary>
/// 第四版毒组（咒怨 Eiha：P5 原作的黑红咒火）。用户 2026-11-02：猎人两大流派之一，毒的特效是重点；
/// 施毒和每回合毒伤打得非常频繁，所以短而干净，但材质不降级。不画任何几何形状：
/// · 黑红咒火：原版火焰序列帧换「反向」咒怨 LUT（火芯越亮越黑、边缘亮红），从目标脚下 / 身上舔起来；
/// · 怨灵：P5R 原版的怨灵贴图（be0094soul3 / be217_ghost2）带着扭曲飘起、从边缘烧散；
/// · 底下一层暗红辉光、原版火星（咒怨 LUT）；目标被染成暗红再褪回。
/// 施毒 = PoisonApplyPatch（游戏给敌人加中毒 / 叠毒那一刻，所有毒牌、涂毒、腐蚀波、毒雾都走这里）；
/// 毒伤 = PoisonTickPatch（每轮毒伤结算）。
/// </summary>
public static class PoisonFx
{
	/// <summary>
	/// 一团黑红咒火：底下一层加法的红色径向光（着色器，发光要用加法，半透明混合叠在背景上会变成橙棕色），
	/// 上面原版火焰序列帧用反向 LUT 画成黑芯红边，再错开叠一团更小的让火更密。
	/// </summary>
	private static void CurseFlame(Control layer, string scene, Vector2 at, float scale, float delay = 0f)
	{
		Glow.Flash(layer, at + new Vector2(0f, -60f * scale), new Color(1f, 0.04f, 0.12f), 300f * scale, 0.45f, delay);
		Vanilla.Burst(layer, scene, at, 0f, scale, Lut5.CurseInk, delay: delay);
		Vanilla.Burst(layer, scene, at + new Vector2(Fx.Rand(-30f, 30f) * scale, 6f), 0f, scale * 0.7f, Lut5.CurseInk, delay: delay + 0.07f);
	}

	/// <summary>
	/// 施毒。亚森刚出过手（ArseneFx.TryClaw）：咒怨从他的爪尖甩出去——一道黑红的咒怨光束 0.08s 扫到敌人，沿路拖着怨灵——再在敌人身上炸开；
	/// 没有召唤（带毒刺击、涂毒、毒雾……）就只有落点，轻一些。
	/// at = 目标中心，top = 头顶，width = 目标宽度，amount = 这次加的层数（多就更旺）。
	/// </summary>
	public static void Apply(Control layer, Vector2 at, Vector2 top, float width, Node2D? body, int amount, bool? outbreak = null) =>
		Apply(layer, at, top, width, body, amount, 6, outbreak ?? InOutbreak);

	private static void Apply(Control layer, Vector2 at, Vector2 top, float width, Node2D? body, int amount, int retry, bool outbreak)
	{
		// 弹跳烧瓶：咒怨火球落到这个敌人身上（SnakeFx.Throw 飞过来，落地 = 这一刻）
		if (SnakeFx.FlaskActive)
		{
			SnakeFx.Land(layer, at, top, width, body);
			return;
		}
		// 毒性爆发（巨手版）：结算推到了捏碎那一刻，标记早在攥住横幅时做过（Squeeze）→ 这里只让毒伤立刻引爆
		if (outbreak && HandMode)
		{
			ulong nowMs = Stage.NowMs();
			_beamLand = nowMs > 380 ? nowMs - 380 : 0;
			return;
		}
		// 游戏上毒和亚森出手对在同一时刻，谁先执行不一定：上毒先执行时爪尖还没记下 → 亚森已现身、还没出手就等他出手（每次 0.05s，最多 6 次），
		// 免得走成没召唤的普通落点（用户 2026-11-04：从亚森到敌人那一条有时有有时没有）
		if (retry > 0 && !ArseneFx.TryClaw(out _) && ArseneFx.AttackPending)
		{
			Fx.After(layer, 0.05, () => Apply(layer, at, top, width, body, amount, retry - 1, outbreak));
			return;
		}
		if (CorrosiveFx.Suppressing && !ArseneFx.TryClaw(out _))
		{
			return;                                                   // 腐蚀波：抽牌带来的上毒由贴地的咒火浪表现（CorrosiveFx.Wave）
		}
		if (Stage.NowMs() >= NoBeamUntil && ArseneFx.TryClaw(out Vector2 claw))
		{
			Vector2 d = at - claw;
			float len = d.Length();
			if (len > 40f)
			{
				// 用户 2026-11-13：原来的贴图光束（slash_thrust）偏硬、很贴图 → 换成着色器画的咒怨光带（CurseBeam）
				Beam(layer, claw, at, 0.07f, 0.08f, Mathf.Clamp(width * 0.16f, 22f, 46f));
				for (int i = 1; i <= 3; i++)
				{
					Vector2 p = claw + d * (0.25f * i);
					Paint.Rise(layer, "curse_soul", p, 70f, 0.4f, Lut5.Curse, 0.5f, 0.03f * i);
				}
			}
			_beamLand = Stage.NowMs() + 80;
			// 毒性爆发：光束命中只留「标记」，不炸（用户 2026-11-03：太花、几个高潮挤在一起）——唯一的高潮留给引爆
			if (outbreak)
			{
				Fx.After(layer, 0.08, () => Mark(layer, at, top, width, body));
				return;
			}
			Fx.After(layer, 0.08, () => Impact(layer, at, top, width, body, amount, d, true));
			return;
		}
		if (outbreak)
		{
			Mark(layer, at, top, width, body);
			return;
		}
		Impact(layer, at, top, width, body, amount, new Vector2(1f, 0.1f), false);
	}

	/// <summary>弹跳烧瓶的一次落地：一团咒火炸开（中等）；最后一跳更大（两侧各一团、顿帧更长、震屏更重、一圈空间扭曲）。</summary>
	public static void FlaskBurst(Control layer, Vector2 at, Vector2 top, float width, Node2D? body, bool last)
	{
		float h = Mathf.Max(80f, (at - top).Length());
		Vector2 foot = at + new Vector2(0f, h * 0.75f);
		float k = Mathf.Clamp(width / 240f, 0.55f, 1.3f);
		Sfx.Play(Sfx.CurseSoft, last ? 0.6f : 0.45f);
		Sfx.Play(Sfx.SubBoom, last ? 0.5f : 0.3f);
		if (last)
		{
			Sfx.Play(Sfx.CurseBlast, 0.45f);                          // 最后一跳：咒怨引爆声（不用重拳声，像打击）
		}
		HitStop.Request(last ? 0.07 : 0.035);
		Stage.ShakeDir?.Invoke(last ? 3 : 1, 90f);
		Glow.Flash(layer, at, new Color(1f, 0.3f, 0.35f), width * (last ? 1.6f : 1.1f), 0.2f);
		if (last)
		{
			Glow.Ripple(layer, at, width * 2.2f, 0.45f, 0.03f);
		}
		CurseFlame(layer, Vanilla.FireCenter, foot, (last ? 1.15f : 0.75f) * k);
		if (last)
		{
			CurseFlame(layer, Vanilla.FireLeft, foot + new Vector2(-0.3f * width, 0f), 0.75f * k, 0.05f);
			CurseFlame(layer, Vanilla.FireRight, foot + new Vector2(0.3f * width, 0f), 0.75f * k, 0.08f);
		}
		Vanilla.Burst(layer, Vanilla.Embers, foot, 0f, (last ? 1.2f : 0.8f) * k, Lut5.Curse);
		Vanilla.Burst(layer, Vanilla.Specks, at, -Mathf.Pi / 2f, 0.9f, Lut5.Curse, amount: last ? 22 : 12);
		Paint.Rise(layer, "curse_soul", at + new Vector2(Fx.Rand(-0.2f, 0.2f) * width, 0f), (last ? 200f : 130f) * k, (last ? 0.95f : 0.7f) * k, Lut5.Curse, 0.7f, 0.04f);
		BodyTint.Pulse(body, new Color(1.8f, 0.7f, 0.75f), 0.03, 0.08);
		Fx.After(layer, 0.12, () => BodyTint.Pulse(body, new Color(0.72f, 0.45f, 0.52f), 0.18, 0.45));
	}

	/// <summary>咒怨打中：顿帧 + 沿来向的震屏 + 一下红白闪 + 空间扭曲 + 黑红咒火从脚下冲起 + 怨灵飘散 + 目标被染暗。</summary>
	private static void Impact(Control layer, Vector2 at, Vector2 top, float width, Node2D? body, int amount, Vector2 dir, bool persona)
	{
		float h = Mathf.Max(80f, (at - top).Length());
		Vector2 foot = at + new Vector2(0f, h * 0.75f);
		float k = Mathf.Clamp(width / 240f, 0.55f, 1.3f);
		bool big = amount >= 6;
		Sfx.Play(Sfx.CurseSoft, persona ? 0.55f : 0.4f);              // 用户 2026-11-03：重拳声像打击，不符合咒怨 → 咒怨闷响
		Sfx.Play(Sfx.SubBoom, persona ? 0.5f : 0.3f);
		HitStop.Request(persona ? (big ? 0.08 : 0.06) : 0.035);
		if (Stage.ShakeDir != null)
		{
			Stage.ShakeDir(persona ? (big ? 3 : 2) : 1, Mathf.RadToDeg(dir.Angle()));
		}
		Glow.Flash(layer, at, new Color(1f, 0.35f, 0.4f), width * (persona ? 1.5f : 1.1f), 0.18f);
		Glow.Ripple(layer, at, width * 2f, 0.4f, persona ? 0.03f : 0.015f);
		float f = persona ? 1.1f : 0.8f;
		CurseFlame(layer, Vanilla.FireCenter, foot, (big ? 1.05f : 0.9f) * k * f);
		if (big || persona)
		{
			CurseFlame(layer, Vanilla.FireLeft, foot + new Vector2(-0.3f * width, 0f), 0.7f * k * f, 0.05f);
			CurseFlame(layer, Vanilla.FireRight, foot + new Vector2(0.3f * width, 0f), 0.7f * k * f, 0.09f);
		}
		Vanilla.Burst(layer, Vanilla.Embers, foot, 0f, 1.1f * k, Lut5.Curse);
		Vanilla.Burst(layer, Vanilla.Specks, at, dir.Angle(), 0.9f, Lut5.Curse, amount: 18);
		Paint.Rise(layer, "curse_soul", at + new Vector2(Fx.Rand(-0.25f, 0.25f) * width, 0.1f * h), 190f * k, 0.95f * k, Lut5.Curse, 0.8f, 0.06f);
		if (big || persona)
		{
			Paint.Rise(layer, "curse_ghost", at + new Vector2(Fx.Rand(-0.2f, 0.2f) * width, 0f), 240f * k, 0.72f * k, Lut5.Curse, 0.95f, 0.12f);
		}
		BodyTint.Pulse(body, new Color(1.8f, 0.7f, 0.75f), 0.03, 0.08);              // 打中一下提亮（受击闪）
		Fx.After(layer, 0.12, () => BodyTint.Pulse(body, new Color(0.72f, 0.45f, 0.52f), 0.18, 0.45));
	}

	// ───────────────────────── 毒性爆发（稀有）─────────────────────────

	private static ulong _outbreakUntil, _beamLand, _lastBlast;

	/// <summary>
	/// 毒性爆发出牌时（JokerFx.OnPlay）：接下来 seconds 秒里的毒伤结算（牌效果里的「立即触发中毒」）不走普通的一小团，
	/// 而是等亚森甩出去的咒怨光束都落地之后，所有敌人身上的咒火同时冲天而起（Blast），第一下压全屏红黑冲击帧 + 最重的顿帧。
	/// 前面的眼神特写、舞台、亚森现身走 Aim 的稀有召唤流程。
	/// </summary>
	public static void BeginOutbreak(float seconds, bool hand = true)
	{
		_crushFrom = null;
		_outbreakUntil = Stage.NowMs() + (ulong)(seconds * 1000f);
		HandMode = hand;
		ulong now = Stage.NowMs();
		_beamLand = now > 380 ? now - 380 : 0;
	}

	/// <summary>
	/// 毒性爆发走巨手版（用户 2026-11-03 创意，CrushHand）：眼神特写里伸出一只咒烟巨手，攥住横幅、捏碎 = 引爆。
	/// 没有亚森现身和光束；攥住横幅那一刻所有敌人被标记（Squeeze），捏碎那一刻游戏结算毒伤、所有敌人一起引爆（Blast）。
	/// </summary>
	public static bool HandMode;

	public readonly record struct Target(Vector2 At, Vector2 Top, float Width, Node2D? Body);

	/// <summary>
	/// 打出毒性爆发那一刻（用户 2026-11-08：敌人身上要马上有反馈、有铺垫，不能等到快结算才冒出来）：
	/// 每个敌人身上先落一个咒印（暗红光 + 一小团咒火 + 一缕怨灵、身体暗一下、一声很轻的闷响），
	/// 之后脚下一直暗燃——咒火星越来越密、越窜越快，隔一会儿舔起一小团火舌，越来越大，一直烧到捏碎；
	/// 巨手攥拳那一下（fistIn）由 Squeeze 再压一层，捏碎后咒怨打到才引爆（Blast）。
	/// </summary>
	public static void Brand(Control layer, IReadOnlyList<Target> targets, float crushIn, float fistIn)
	{
		if (targets.Count == 0)
		{
			return;
		}
		Sfx.Play(Sfx.CurseSoft, 0.4f);
		Color[] ramp = { new(1f, 0.16f, 0.22f), new(0.6f, 0.02f, 0.08f), new(0.12f, 0f, 0.03f) };
		foreach (Target t in targets)
		{
			float h = Mathf.Max(80f, (t.At - t.Top).Length());
			float k = Mathf.Clamp(t.Width / 240f, 0.55f, 1.3f);
			Vector2 foot = t.At + new Vector2(0f, h * 0.7f);
			Glow.Flash(layer, t.At, new Color(0.9f, 0.06f, 0.14f), t.Width * 1.1f, 0.35f);
			Vanilla.Burst(layer, Vanilla.FireCenter, foot, 0f, 0.5f * k, Lut5.CurseInk);
			Paint.Rise(layer, "curse_soul", t.At, 90f * k, 0.55f * k, Lut5.Curse, 0.6f);
			BodyTint.Pulse(t.Body, new Color(0.8f, 0.58f, 0.64f), Mathf.Max(0.05f, fistIn - 0.2f), 0.12);
			Node2D root = Fx.Root(layer, foot, "P5OutbreakSmolder", crushIn + 1.5);
			GpuParticles2D em = ReadyFx.Embers(root, Vector2.Zero, ramp, 28, t.Width * 0.35f, 0.9f, 40f, 140f, 35f);
			em.AmountRatio = 0.3f;
			Fx.Animate(root, 0, Mathf.Max(0.1f, crushIn), u =>
			{
				em.AmountRatio = Mathf.Lerp(0.3f, 1f, u * u);
				em.SpeedScale = 1.0 + 0.8 * u;
			});
			Fx.After(root, crushIn, () => em.Emitting = false);
			for (float at = 0.22f; at < crushIn - 0.08f; at += 0.2f)
			{
				float g = at / crushIn;
				Vanilla.Burst(layer, Vanilla.FireCenter, foot + new Vector2(Fx.Rand(-0.3f, 0.3f) * t.Width, 0f), 0f, (0.28f + 0.4f * g) * k,
					Lut5.CurseInk, delay: at);
			}
		}
	}

	/// <summary>巨手攥住横幅：所有敌人身上一团暗燃的咒火（被攥住了），停到捏碎那一刻一起炸。</summary>
	public static void Squeeze(Control layer, IEnumerable<Target> targets)
	{
		foreach (Target t in targets)
		{
			Mark(layer, t.At, t.Top, t.Width, t.Body);
		}
	}

	public static bool InOutbreak => Stage.NowMs() < _outbreakUntil;

	/// <summary>这段时间里的上毒走普通落点（不从亚森爪尖甩光束）：蛇咬召唤了亚森，但毒是蛇咬上的（用户 2026-11-13：蛇咬原来的毒特效保留）。</summary>
	public static ulong NoBeamUntil;

	private static Shader? _beam;

	/// <summary>
	/// 咒怨光带（亚森爪尖 → 敌人）：ColorRect 沿着两点放（UV.x 0 起点 → 1 终点，UV.y 横向）。整条按 bend 拱起一道弧；
	/// 头 head 从起点冲到终点，两头都是圆的（宽度平滑收起来，没有尖角）；里面的纹理和细丝往终点方向流（flow）；
	/// 白热芯、暗红身、外面一圈柔光；thin 0 → 1 变细淡掉。
	/// </summary>
	private static Shader BeamShader => _beam ??= new Shader { Code = @"
shader_type canvas_item;
uniform float head = 1.0;
uniform float tail = 0.0;
uniform float thin = 0.0;
uniform float flow = 0.0;
uniform float bend = 0.2;
uniform float waves = 1.0;     // 1 = 一道弧；> 1 = 像蛇一样弯几道（越往终点摆得越小）
uniform float aspect = 8.0;
uniform float seed = 0.0;
uniform float alpha = 1.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
void fragment() {
	float x = UV.x;
	// 弧：中间拱起（UV.y 0.5 是直线，quad 上下留了余量）
	float y = (UV.y - 0.5) * 2.0 + bend * sin(x * 3.14159 * waves) * 2.0 * (waves > 1.0 ? (1.0 - 0.6 * x) : 1.0);
	// 两头圆：在头、尾附近宽度平滑收起来
	float span = max(head - tail, 0.001);
	float capL = 0.45 / aspect;   // 圆头的长度 ≈ 光带半宽
	float body = smoothstep(tail, tail + capL, x) * (1.0 - smoothstep(head - capL, head, x));
	float w = (0.42 + 0.12 * sin(x * 18.0 - flow * 1.3 + seed)) * body * (1.0 - 0.75 * thin);
	float n = noise(vec2(x * aspect * 1.6 - flow * 2.0, y * 3.0 + seed));
	float n2 = noise(vec2(x * aspect * 4.0 - flow * 3.5, y * 9.0 - seed));
	float wl = max(w, 1e-3);
	float d = abs(y) / wl;
	float glow = exp(-d * d * 0.9) * body;
	float bodyA = smoothstep(1.0, 0.55, d + (n - 0.5) * 0.4);
	float core = smoothstep(0.42, 0.0, d + (n2 - 0.5) * 0.25) * (1.0 - thin * 0.6);
	// 细丝：顺着方向流
	float fil = pow(1.0 - abs(noise(vec2(x * aspect * 2.2 - flow * 4.0, y * 14.0 + seed)) * 2.0 - 1.0), 8.0) * smoothstep(1.2, 0.3, d);
	vec3 deep = vec3(0.32, 0.0, 0.06);
	vec3 red = vec3(0.92, 0.06, 0.16);
	vec3 col = mix(deep, red, smoothstep(0.9, 0.3, d) * (0.6 + 0.4 * n));
	col = mix(col, vec3(1.0, 0.92, 0.9), core);
	col += vec3(1.0, 0.45, 0.45) * fil * 0.6 * body;
	float a = max(bodyA * body * (0.75 + 0.25 * n), glow * 0.35) + fil * 0.3 * body;
	a = clamp(a, 0.0, 1.0) * alpha;
	if (a <= 0.003) discard;
	COLOR = vec4(col, a);
}" };

	/// <summary>
	/// 从 from 到 to 的一道咒怨光带：grow 秒冲过去，停一下，再 fade 秒变细淡掉。bend 拱起的程度（相对长度），width 光带粗细（像素）。
	/// </summary>
	public static void Beam(Control layer, Vector2 from, Vector2 to, float bend, float grow, float width, float hold = 0.06f, float fade = 0.2f, float waves = 1f)
	{
		Vector2 dv = to - from;
		float len = dv.Length();
		if (len < 10f) return;
		float hgt = width * 2f + Mathf.Abs(bend) * len * 1.2f;   // 上下留出弧的余量
		Node2D root = Fx.Root(layer, from, "P5CurseBeam", grow + hold + fade + 0.1);
		root.Rotation = dv.Angle();
		ShaderMaterial m = new() { Shader = BeamShader };
		m.SetShaderParameter("aspect", len / hgt);
		m.SetShaderParameter("waves", waves);
		m.SetShaderParameter("seed", Fx.Rand(0f, 30f));
		// bend 是相对 quad 半高的：拱起 bend*len 像素 → 换算
		m.SetShaderParameter("bend", bend * len / hgt * (dv.X >= 0f ? 1f : -1f));   // 往上拱（朝左时 quad 是倒过来的）
		ColorRect q = new() { Size = new Vector2(len, hgt), Position = new Vector2(0f, -hgt / 2f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		float total = grow + hold + fade;
		Fx.Animate(root, 0, total, t =>
		{
			float s = t * total;
			m.SetShaderParameter("head", Ease5.OutQuint(Mathf.Min(1f, s / grow)) * 1.02f);
			m.SetShaderParameter("tail", Fx.InCubic(Ease5.Seg(s, grow + hold * 0.5f, fade + hold * 0.5f)) * 1.02f);
			m.SetShaderParameter("thin", Ease5.Seg(s, grow + hold, fade));
			m.SetShaderParameter("flow", s * 12f);
			m.SetShaderParameter("alpha", 1f - Ease5.Seg(s, grow + hold + fade * 0.6f, fade * 0.4f));
		});
	}

	/// <summary>捏碎时的拳心（全局坐标）：引爆时敌人被往远离拳心的方向打退；没捏碎过为空。</summary>
	private static Vector2? _crushFrom;

	/// <summary>
	/// 捏碎（用户 2026-11-08：捏爆那一下和敌人身上的伤害要连上）：从拳心朝每个敌人甩出一道咒怨，ArrivalDelay 秒后打到 =
	/// 那个敌人结算毒伤、引爆（OutbreakPlayback.Run 按同一个时刻等）。全场定格（HitStop）期间这些都停着，定格一松开才飞。
	/// </summary>
	public static void Crushed(Control layer, Vector2 from, IEnumerable<Vector2> targets)
	{
		_crushFrom = from;
		Glow.Flash(layer, from, new Color(1f, 0.55f, 0.6f), 900f, 0.2f);
		foreach (Vector2 at in targets)
		{
			Vector2 d = at - from;
			float len = d.Length();
			if (len < 60f)
			{
				continue;
			}
			float travel = ArrivalDelay(at);
			// 和亚森爪尖甩出去的咒怨光束同一张笔触（slash_thrust）：宽的一头在拳心，沿着路扫过去，扫满 = 打到
			float sx = len / 940f;
			Vector2 center = from + d * 0.5f + d.Normalized() * (512f - 530f) * sx;
			Paint.Sweep(layer, "slash_thrust", center, d.Angle(), new Vector2(sx, 0.5f), Lut5.Curse, travel, 0.03f, 0.2f, 0f, 0f, 1.5f);
			Paint.Rise(layer, "curse_soul", from + d * 0.5f, 60f, 0.45f, Lut5.Curse, 0.45f, travel * 0.5f);
		}
	}

	/// <summary>拳心甩出去的咒怨打到 at 要几秒（离得远的晚一点点，看得出是从拳心传过去的）。</summary>
	public static float ArrivalDelay(Vector2 at) =>
		_crushFrom is Vector2 from ? Mathf.Clamp(0.05f + from.DistanceTo(at) / 6000f, 0.05f, 0.18f) : 0f;

	/// <summary>毒性爆发期间：离引爆还有几秒（毒伤的伤害数字推迟到这一刻再跳，DamageNumberPatch）。</summary>
	public static double BlastWait()
	{
		ulong now = Stage.NowMs();
		return _beamLand + 380 > now ? (_beamLand + 380 - now) / 1000.0 : 0.0;
	}

	/// <summary>
	/// 毒性爆发的标记（光束命中时）：敌人身上一团暗暗燃着的小咒火 + 一点红光，身体暗下去；没有顿帧、没有震屏、声音很轻。
	/// 引爆前全场都被标上，停 ~0.3s 让人看清，再一起炸。
	/// </summary>
	private static void Mark(Control layer, Vector2 at, Vector2 top, float width, Node2D? body)
	{
		float h = Mathf.Max(80f, (at - top).Length());
		float k = Mathf.Clamp(width / 240f, 0.55f, 1.3f);
		Sfx.Play(Sfx.CurseSoft, 0.3f);
		Glow.Flash(layer, at, new Color(0.9f, 0.08f, 0.15f), width * 0.9f, 0.45f);
		Vanilla.Burst(layer, Vanilla.FireCenter, at + new Vector2(0f, h * 0.35f), 0f, 0.5f * k, Lut5.CurseInk);
		BodyTint.Pulse(body, new Color(0.62f, 0.4f, 0.48f), 0.45, 0.3);
	}

	/// <summary>
	/// 引爆（毒性爆发唯一的高潮）：所有标记同时炸开——一次顿帧 + 震屏 + 红黑帧（重档，0.15s）；每个敌人一根大咒火柱 + 两边各一团 + 一只怨灵，
	/// 之后只留烟和火星慢慢散（不再有第二波，原来的第二波让画面一直很满）。同一批里只有第一下带全屏效果和音效。
	/// </summary>
	private static void Blast(Control layer, Vector2 at, Vector2 top, float width, Node2D? body, bool killed)
	{
		float h = Mathf.Max(80f, (at - top).Length());
		Vector2 foot = at + new Vector2(0f, h * 0.75f);
		float k = Mathf.Clamp(width / 240f, 0.55f, 1.3f);
		ulong now = Stage.NowMs();
		if (now - _lastBlast > 150)
		{
			Sfx.Play(Sfx.OutbreakBurst, 1.2f);   // 用户 2026-11-08：被捏碎 / 碎玻璃声盖住、听不出来 → 文件压缩提响 + 音量 1.2                        // 用户 2026-11-08：换掉平淡的受击声（原版受击声在 OutbreakHitSfxPatch 里关掉）
			Sfx.Play(Sfx.SubBoom, 0.45f);
		}
		_lastBlast = now;
		// 每个敌人都被打得一顿（白闪 + 往远离拳心的方向退）；顿帧 / 震屏 / 红黑帧整批只放一次（Impact 自己合并）
		Vector2 dir = _crushFrom is Vector2 from && (at - from).LengthSquared() > 1f ? (at - from).Normalized() : Vector2.Up;
		global::P5Joker.Impact.Confirm(layer, killed ? null : body, at, dir, Weight.Heavy);   // 打死的不击退，交给死亡动画
		Glow.Flash(layer, at, new Color(1f, 0.3f, 0.35f), width * 1.8f, 0.25f);
		Glow.Ripple(layer, at, width * 2.4f, 0.5f, 0.03f);
		CurseFlame(layer, Vanilla.FireCenter, foot, 1.45f * k);
		CurseFlame(layer, Vanilla.FireLeft, foot + new Vector2(-0.32f * width, 0f), 0.9f * k, 0.05f);
		CurseFlame(layer, Vanilla.FireRight, foot + new Vector2(0.32f * width, 0f), 0.9f * k, 0.08f);
		Vanilla.Burst(layer, Vanilla.Embers, foot, 0f, 1.3f * k, Lut5.Curse, delay: 0.1f);
		Vanilla.Burst(layer, Vanilla.BigSlashSmoke, foot + new Vector2(0f, -0.3f * h), -Mathf.Pi / 2f, 1.1f * k, Lut5.Smoke, delay: 0.15f);
		Paint.Rise(layer, "curse_ghost", at + new Vector2(Fx.Rand(-0.15f, 0.15f) * width, 0f), 260f * k, 0.9f * k, Lut5.Curse, 1.0f, 0.08f);
		Fx.After(layer, 0.24, () => BodyTint.Pulse(body, new Color(0.6f, 0.35f, 0.42f), 0.35, 0.6));   // 等受击白闪（Impact）走完再染暗
	}

	/// <summary>每轮毒伤（回合开始，敌人越多越频繁）：一小团咒火 + 一缕怨灵 + 身体暗一下。</summary>
	public static void Tick(Control layer, Vector2 at, Vector2 top, float width, Node2D? body, bool? outbreak = null, bool killed = false)
	{
		if (outbreak ?? InOutbreak)
		{
			// The gameplay gate already releases on the actual crush. Never add a beam-land delay.
			Blast(layer, at, top, width, body, killed);
			return;
		}
		float h = Mathf.Max(80f, (at - top).Length());
		float k = Mathf.Clamp(width / 240f, 0.55f, 1.3f);
		Sfx.Play(Sfx.CurseSoft, 0.45f);                              // 柔和的咒怨闷响（原来的 continuas_damage 太尖，用户嫌腐蚀波刺耳，毒伤也一起换）
		CurseFlame(layer, Vanilla.FireCenter, at + new Vector2(0f, h * 0.6f), 0.55f * k);
		Paint.Rise(layer, "curse_soul", at + new Vector2(Fx.Rand(-0.2f, 0.2f) * width, 0f), 120f * k, 0.7f * k, Lut5.Curse, 0.65f, 0.05f);
		BodyTint.Pulse(body, new Color(0.85f, 0.62f, 0.68f), 0.06, 0.3);
	}
}
