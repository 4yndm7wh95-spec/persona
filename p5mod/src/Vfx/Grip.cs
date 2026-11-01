using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 人格面具之手（用户 2026-11-01）：召唤人格面具的单体毒牌 —— 从亚森那里隐隐伸出一只手，狠狠地一把攥住中毒的怪，
/// 毒没结算完就一直捏着，结算完才回去。手是 Codex 画的同一只手的 7 个姿势（grip_hand，打包时对齐了手腕），从左往右伸，朝左的话镜像。
/// 距离的处理：手不整段飞过去 —— 亚森手边冒一团黑红雾，0.08s 一道雾的拖尾「唰」地射到怪身前，手从雾里凝出来扑上去，最后一刻攥成实心拳
/// （= 召唤高潮那一拍）。手和亚森之间只有一缕模模糊糊的雾丝（实时渲染、半透明、被噪声搅动、往前流），看得出联系但不像手臂。
/// 每结算一轮毒伤：一团微光顺着雾丝流到手上，手更用力地一捏（fist_tight）、怪闪一下。最后一轮之后 0.4s（至少攥住 0.45s）松开，
/// 化成雾顺着雾丝缩回亚森。
/// </summary>
public static class Grip
{
	private static readonly string[] Frames = { "grip_reach", "grip_close", "grip_hold", "grip_fist", "grip_fist_tight", "grip_release" };
	private const int Reach = 0, Close = 1, Hold = 2, Fist = 3, Tight = 4, Release = 5;

	/// <summary>贴图（900x600）里手腕在哪、实心拳的中心在哪。</summary>
	private static readonly Vector2 Wrist = new(180f, 300f), FistCenter = new(690f, 300f);

	private static Shader? _hand, _mist;

	/// <summary>手：dissolve 0→1 从噪声里凝出来 / 散掉，hot 攥住那一下红光过曝。</summary>
	private static Shader HandShader => _hand ??= new Shader { Code = @"
shader_type canvas_item;
uniform float dissolve = 0.0;   // 0 = 完整，1 = 全散成雾
uniform float hot = 0.0;
uniform float seed = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vnoise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i + seed), hash(i + vec2(1.0, 0.0) + seed), f.x), mix(hash(i + vec2(0.0, 1.0) + seed), hash(i + vec2(1.0, 1.0) + seed), f.x), f.y);
}
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	// 从手腕那端（左）先散：噪声 + 横向偏置
	float n = vnoise(UV * vec2(14.0, 10.0)) * 0.6 + vnoise(UV * 40.0) * 0.4;
	float th = dissolve * 1.25 - (UV.x - 0.2) * 0.35;
	float keep = smoothstep(th - 0.06, th + 0.06, n);
	vec3 col = mix(c.rgb, vec3(1.0, 0.1, 0.14), hot * step(0.3, c.r));
	col += vec3(0.9, 0.05, 0.1) * smoothstep(th + 0.1, th, n) * step(0.01, dissolve) * 0.8;   // 散开的边缘发红
	COLOR = vec4(col, c.a * keep);
}" };

	/// <summary>
	/// 雾丝：一条从 a（UV.x = 0）到 b（UV.x = 1）的带子，在 b 那头更粗；半透明的黑红雾，边缘被噪声搅动，往前（+x）流；
	/// 只显示 [tail, head] 这一段；pulse 处一团亮红光（每轮毒伤顺着流过去）。
	/// </summary>
	private static Shader MistShader => _mist ??= new Shader { Code = @"
shader_type canvas_item;
uniform float head = 1.0;
uniform float tail = 0.0;
uniform float alpha = 0.4;
uniform float pulse = -1.0;
uniform float clock = 0.0;
uniform float seed = 0.0;
uniform float aspect = 8.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vnoise(vec2 p) {
	vec2 i = floor(p); vec2 f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i + seed), hash(i + vec2(1.0, 0.0) + seed), f.x), mix(hash(i + vec2(0.0, 1.0) + seed), hash(i + vec2(1.0, 1.0) + seed), f.x), f.y);
}
float fbm(vec2 p) { float v = 0.0, a = 0.5; for (int i = 0; i < 4; i++) { v += a * vnoise(p); p = p * 2.03 + 1.7; a *= 0.5; } return v; }
void fragment() {
	float x = UV.x;
	float y = (UV.y - 0.5) * 2.0;
	vec2 q = vec2(x * aspect, y);
	// 带子的中线被噪声扭来扭去、宽度往 b 那头变粗、边缘被搅碎
	float wob = (fbm(vec2(x * aspect * 0.4 - clock * 1.5, 3.0)) - 0.5) * 0.7;
	float w = mix(0.22, 0.85, pow(x, 1.4));
	float d = abs(y - wob) / w;
	float body = smoothstep(1.0, 0.2, d + (fbm(q * vec2(0.9, 2.5) - vec2(clock * 3.0, 0.0)) - 0.5) * 0.9);
	float wisp = smoothstep(0.62, 0.8, fbm(q * vec2(1.6, 4.0) - vec2(clock * 4.5, 0.0))) * body;
	float on = smoothstep(head, head - 0.04, x) * smoothstep(tail, tail + 0.06, x);
	float ends = smoothstep(0.0, 0.06, x) * smoothstep(1.0, 0.94, x);
	float p = pulse >= 0.0 ? exp(-pow((x - pulse) / 0.05, 2.0)) * smoothstep(1.0, 0.3, d) : 0.0;
	vec3 col = mix(vec3(0.18, 0.0, 0.04), vec3(0.85, 0.06, 0.12), wisp);
	col = mix(col, vec3(1.0, 0.35, 0.4), p);
	float a = (body * 0.5 + wisp * 0.75) * alpha + p * 0.9;
	COLOR = vec4(col, clamp(a, 0.0, 1.0) * on * ends);
}" };

	private sealed class Live
	{
		public ulong LastTickMs;
		public int Ticks;
		public Action? OnTick;
	}

	private static readonly Dictionary<ulong, Live> Active = new();

	/// <summary>游戏结算了一轮毒伤（PoisonTickPatch）：这个怪正被攥着就再捏一下。</summary>
	public static void OnTick(ulong targetId)
	{
		if (Active.TryGetValue(targetId, out Live? l))
		{
			l.LastTickMs = Stage.NowMs();
			l.Ticks++;
			l.OnTick?.Invoke();
		}
	}

	/// <param name="layer">前景层</param>
	/// <param name="from">人格面具伸出手的地方</param>
	/// <param name="center">怪的中心</param>
	/// <param name="h">怪中心到头顶的距离</param>
	/// <param name="attack">攥住的那一刻（从现在起几秒，= 召唤高潮那一拍）</param>
	public static void Play(Control layer, ulong targetId, Vector2 from, Vector2 center, float h, float attack, float facing, Action<Color>? tint = null)
	{
		if (Fx.Tex(Frames[0]) is not Texture2D t0)
		{
			return;
		}
		Texture2D[] tex = new Texture2D[Frames.Length];
		for (int i = 0; i < Frames.Length; i++) tex[i] = Fx.Tex(Frames[i]) ?? t0;
		float k = h * 1.0f / 240f;                                       // 实心拳约 240px 高 → 和怪差不多大
		Vector2 fistAt = center + new Vector2(-0.25f * h * facing, -0.05f * h);   // 拳头压在怪靠近 Joker 那半边，不压到后面的怪
		Vector2 wristAt = fistAt - new Vector2((FistCenter.X - Wrist.X) * k * facing, 0f);
		Vector2 dir = (wristAt - from).Normalized();
		float len = from.DistanceTo(wristAt);
		const float maxHold = 3.5f;
		Node2D root = Fx.Root(layer, Vector2.Zero, "P5Grip", attack + maxHold + 1f);
		root.GlobalPosition = Vector2.Zero;

		// 雾丝（在手后面）
		Node2D mistN = new() { Position = from, Rotation = (wristAt - from).Angle() };
		root.AddChild(mistN);
		float mw = h * 0.9f;
		ColorRect mist = new()
		{
			Size = new Vector2(len + h * 0.3f, mw), Position = new Vector2(0f, -mw / 2f), MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = MistShader },
		};
		mistN.AddChild(mist);
		ShaderMaterial mm = (ShaderMaterial)mist.Material;
		mm.SetShaderParameter("aspect", (len + h * 0.3f) / mw);
		mm.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		mm.SetShaderParameter("head", 0f);
		Fx.Clock(mist);

		// 手
		Node2D handN = new() { Position = wristAt, Visible = false };
		root.AddChild(handN);
		ShaderMaterial hm = new() { Shader = HandShader };
		hm.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		Sprite2D hand = new()
		{
			Texture = tex[Reach], Centered = false, Offset = -Wrist, Scale = new Vector2(k * facing, k), Material = hm,
			TextureFilter = CanvasItem.TextureFilterEnum.Linear,
		};
		handN.AddChild(hand);

		Live live = new() { LastTickMs = 0 };
		Active[targetId] = live;
		float tickT = -1f;                                               // 最近一轮毒伤在动画里的时刻
		float clock = 0f;
		live.OnTick = () =>
		{
			tickT = clock;
			Sfx.Play(Sfx.PoisonTick, 0.7f, 0.1f);
		};

		// 出手：亚森手边一团雾 + 一声重挥（雾射过去）
		float launch = attack - 0.16f;
		Fx.After(layer, Mathf.Max(0f, launch), () =>
		{
			Glow.Flash(layer, from, new Color(0.7f, 0.03f, 0.08f), h * 1.2f, 0.25f);
			Glow.Sparks(layer, from, 14, 200f, 600f, 0.4f, new[] { new Color(0.9f, 0.1f, 0.14f), new Color(0.3f, 0f, 0.04f) }, -100f, 0.12f, 0.35f, dir, 30f);
		});
		Sfx.Play(Sfx.SwishHeavy, 0.6f, Mathf.Max(0f, launch));

		float releaseAt = -1f;
		bool released = false, gripped = false;
		const float travel = 0.08f, back = 0.2f;
		Fx.Animate(root, 0, attack + maxHold + 0.9f, t =>
		{
			float s = t * (attack + maxHold + 0.9f);
			clock = s;
			float sinceLaunch = s - launch;
			// 雾丝：0.08s 射过去（头在前），之后留着；缩回时尾巴追着头往回收
			float headK = Mathf.Clamp(sinceLaunch / travel, 0f, 1f);
			if (!released)
			{
				mm.SetShaderParameter("head", sinceLaunch < 0f ? 0f : Ease5.OutExpo(headK) * 1.05f);
				mm.SetShaderParameter("tail", 0f);
				mm.SetShaderParameter("alpha", sinceLaunch < travel ? 1f : Mathf.Lerp(1f, 0.6f, Mathf.Clamp((sinceLaunch - travel) / 0.25f, 0f, 1f)));
			}
			// 手：在雾头到达时凝出来，张开 → 收拢 → 抓 → 攥成实心拳（attack）
			float appear = launch + travel * 0.75f;
			handN.Visible = s >= appear && !(released && s > releaseAt + back);
			int f = s < attack - 0.05f ? Reach : s < attack - 0.025f ? Close : s < attack ? Hold : Fist;
			float pulse = -1f;
			if (tickT >= 0f && s - tickT < 0.26f)
			{
				float u = (s - tickT) / 0.1f;                              // 光团 0.1s 顺着雾丝流过去，然后手一捏
				pulse = u < 1f ? u : -1f;
				if (s - tickT >= 0.1f && s - tickT < 0.22f) f = Tight;
				if (s - tickT >= 0.1f && s - tickT < 0.11f && tint != null)
				{
					tint(new Color(0.75f, 0.3f, 0.85f));
				}
			}
			mm.SetShaderParameter("pulse", pulse);
			if (released) f = Release;
			hand.Texture = tex[f];
			float inK = Mathf.Clamp((s - appear) / 0.06f, 0f, 1f);
			float dis = released ? Mathf.Clamp((s - releaseAt) / back, 0f, 1f) : 1f - inK;
			hm.SetShaderParameter("dissolve", dis);
			// 扑上去：从雾头的位置往前冲到位（缓出）；攥住时压一下；捏的时候抖
			float lunge = 1f - Ease5.OutExpo(Mathf.Clamp((s - appear) / 0.1f, 0f, 1f));
			Vector2 pos = wristAt - dir * h * 0.6f * lunge;
			if (released)
			{
				pos = wristAt.Lerp(from, Fx.InCubic(Mathf.Clamp((s - releaseAt) / back, 0f, 1f)) * 0.6f);
			}
			float squash = s >= attack ? Mathf.Exp(-(s - attack) / 0.07f) : 0f;
			if (tickT >= 0f && s - tickT >= 0.1f) squash = Mathf.Max(squash, 0.7f * Mathf.Exp(-(s - tickT - 0.1f) / 0.07f));
			float shake = gripped && !released ? Mathf.Sin(s * 70f) * 1.5f * (0.4f + squash) : 0f;
			handN.Position = pos + new Vector2(shake, shake * 0.4f);
			handN.Scale = new Vector2(1f + 0.06f * squash, 1f - 0.1f * squash) * Mathf.Lerp(1.25f, 1f, Ease5.OutExpo(inK));
			hm.SetShaderParameter("hot", 0.6f * squash);
			if (!gripped && s >= attack)
			{
				gripped = true;
				live.LastTickMs = Stage.NowMs();
				Glow.Flash(layer, fistAt, Pal5.Hot, h * 1.0f, 0.12f);
				Glow.Flash(layer, fistAt, Pal5.Bright, h * 2.0f, 0.28f);
				Glow.Shockwave(layer, fistAt, Pal5.Bright, h * 2.4f, 0.35f);
				Glow.Sparks(layer, fistAt, 18, 300f, 900f, 0.4f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 300f, 0.08f, 0.24f);
				if (tint != null)
				{
					Fx.Animate(layer, 0, 0.35, u => tint(Colors.White.Lerp(new Color(0.6f, 0.15f, 0.25f), 1f - Fx.OutCubic(u))));
				}
			}
			// 松开：最后一轮毒伤之后 0.4s（至少攥住 0.45s，最多 maxHold）
			if (gripped && !released)
			{
				bool quiet = Stage.NowMs() - live.LastTickMs > 400;
				if ((s >= attack + 0.45f && quiet) || s >= attack + maxHold)
				{
					released = true;
					releaseAt = s;
					Active.Remove(targetId);
					tint?.Invoke(Colors.White);
					Sfx.Play(Sfx.SwishLow, 0.35f);
				}
			}
			if (released)
			{
				float r = Mathf.Clamp((s - releaseAt) / (back + 0.1f), 0f, 1f);
				mm.SetShaderParameter("head", 1.05f - Fx.InCubic(r) * 1.05f);
				mm.SetShaderParameter("alpha", 0.6f * (1f - r * 0.5f));
				if (r >= 1f)
				{
					root.Visible = false;
				}
			}
		});
	}
}
