using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 蛇咬 / 弹跳烧瓶（用户 2026-11-03）。
/// · 蛇咬：亚森的咒怨光束打到敌人的同一刻，一张正面朝向玩家的黑红咒怨蛇口在敌人身上上下咬合（獠牙取原版 vfx_bite 的那排牙，换咒怨配色）：
///   先张开、往外再撑一下蓄力 → 一下合上（顿帧 + 上下方向的震屏 + 咬合点一道红光 + 咬碎声）→ 停一下 → 獠牙从边缘烧成咒火散掉。
/// · 弹跳烧瓶：原版扔三个绿色毒药瓶一跳一跳（每跳 0.5s，落地上毒）→ 换成亚森爪尖甩出的一颗黑红咒怨火球，沿原版同一条弹跳路线飞，
///   带着火尾；每落到一个敌人身上炸开一团咒火（落地 = 游戏上毒那一刻），最后一跳炸得最大。原版瓶子、水花、毒气藏掉（Patches）。
/// </summary>
public static class SnakeFx
{
	private static Shader? _fangShader, _orbShader;

	/// <summary>獠牙：贴图亮度查咒怨 LUT（白热的牙芯 → 绯红的边 → 黑），burn 从边缘按噪声烧掉。</summary>
	private static Shader FangShader => _fangShader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D lut : repeat_disable, filter_linear;
uniform sampler2D noise : repeat_enable, filter_linear;
uniform float burn = 0.0;
uniform float flash = 0.0;
uniform float alpha = 1.0;
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	float lum = dot(c.rgb, vec3(0.3, 0.55, 0.15));
	vec3 col = texture(lut, vec2(clamp(lum * 0.95, 0.0, 1.0), 0.5)).rgb;
	col = mix(col, vec3(1.0, 0.16, 0.14), flash * (1.0 - smoothstep(0.2, 0.6, lum)) * 0.9);   // 咬合瞬间：牙的边缘一下烧亮
	float n = texture(noise, UV * 2.3).r;
	float keep = smoothstep(burn - 0.04, burn + 0.04, n * 0.9 + lum * 0.1);
	float rim = (1.0 - smoothstep(0.0, 0.08, abs(n * 0.9 + lum * 0.1 - burn))) * step(0.001, burn);
	col += vec3(1.0, 0.25, 0.2) * rim;
	COLOR = vec4(col, c.a * keep * alpha);
}" };

	/// <summary>
	/// 咒怨火球：黑芯、绯红的火边、火舌往飞行的反方向拖成一条尾巴（vel = 飞行方向，屏幕坐标；speed 0～1 决定尾巴多长）。
	/// </summary>
	private static Shader OrbShader => _orbShader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D noise : repeat_enable, filter_linear;
uniform sampler2D lut : repeat_disable, filter_linear;
uniform vec2 dir = vec2(1.0, 0.0);
uniform float speed = 1.0;
uniform float clock = 0.0;
uniform float alpha = 1.0;
void fragment() {
	vec2 p = (UV - 0.5) * 2.0;
	// 转到飞行方向的坐标：x 朝前
	vec2 f = normalize(dir);
	vec2 q = vec2(dot(p, f), dot(p, vec2(-f.y, f.x)));
	// 尾巴：往后（-x）拉长、越往后越细
	float back = max(-q.x, 0.0);
	float wid = 0.34 * (1.0 - smoothstep(0.0, 0.6 + 0.9 * speed, back) * 0.85);
	float n1 = texture(noise, vec2(q.x * 1.3 + clock * 3.2, q.y * 1.6) * 0.6).r;
	float n2 = texture(noise, vec2(q.x * 3.0 + clock * 5.0, q.y * 3.5) * 0.6).r;
	float body = length(vec2(q.x > 0.0 ? q.x / 0.34 : back / (0.34 + 0.9 * speed), q.y / wid));
	float d = body + (n1 - 0.5) * 0.5 + (n2 - 0.5) * 0.25;
	float a = 1.0 - smoothstep(0.85, 1.0, d);
	// 颜色：芯是黑的（咒怨），往外一圈绯红、最外缘亮红
	float heat = smoothstep(0.35, 0.95, d);
	vec3 col = texture(lut, vec2(clamp(0.15 + heat * 0.8 + (n2 - 0.5) * 0.15, 0.0, 1.0), 0.5)).rgb;
	COLOR = vec4(col, a * alpha);
}" };

	// ───────────────────────── 蛇咬 ─────────────────────────

	/// <summary>正在张着等光束的蛇口：光束打到敌人那一刻（PoisonFx.Apply）调 Snap() 合上。</summary>
	private static System.Action? _snap;

	/// <summary>光束打到敌人：张着的蛇口立刻合上（和咒怨打中同一帧）。没有张着的蛇口就什么也不做。</summary>
	public static void Snap()
	{
		System.Action? s = _snap;
		_snap = null;
		s?.Invoke();
	}

	/// <summary>
	/// 蛇口咬合：at = 敌人中心，width = 敌人宽度，closeIn = 预计几秒后合上（光束打到敌人）。
	/// 实际合上的时刻由 Snap() 决定（光束打中那一刻，和咒怨打中严格同步）；光束迟迟不来（超过预计 0.5s）就自己合上。
	/// </summary>
	public static void Bite(Control layer, Vector2 at, float width, float closeIn)
	{
		if (Fx.Tex("bite_top") is not Texture2D topTex || Fx.Tex("bite_bottom") is not Texture2D botTex)
		{
			return;
		}
		float w = Mathf.Clamp(width * 1.35f, 220f, 460f);
		float k = w / topTex.GetWidth();
		Node2D root = Fx.Root(layer, at, "P5SnakeBite", closeIn + 0.7);
		ShaderMaterial MakeMat()
		{
			ShaderMaterial m = new() { Shader = FangShader };
			m.SetShaderParameter("lut", Lut5.CurseInk);                // 黑芯红边（咒怨）：牙芯近黑、外缘烧红
			m.SetShaderParameter("noise", Paint.Noise);
			return m;
		}
		ShaderMaterial mt = MakeMat(), mb = MakeMat();
		Sprite2D top = new() { Texture = topTex, Material = mt, Scale = Vector2.One * k, Offset = new Vector2(0f, -topTex.GetHeight() * 0.5f) };
		Sprite2D bot = new() { Texture = botTex, Material = mb, Scale = Vector2.One * k, Offset = new Vector2(0f, botTex.GetHeight() * 0.5f) };
		root.AddChild(top);
		root.AddChild(bot);
		float open = 0.42f * w, wide = 0.55f * w;
		float appear = Mathf.Max(0.06f, closeIn - 0.1f);
		bool shut = false;
		float closedAt = -1f, now = 0f;
		_snap = () => { if (closedAt < 0f) closedAt = Mathf.Max(now, appear); };
		float life = closeIn + 0.5f + 0.55f;
		Fx.Animate(root, 0, life, t =>
		{
			float s = t * life;
			now = s;
			if (closedAt < 0f && s >= closeIn + 0.5f)
			{
				closedAt = s;                                          // 光束没来：自己合上
			}
			float closeT = closedAt < 0f ? float.MaxValue : closedAt;
			float gap, alpha = 1f, burn = 0f, flash = 0f, scale = 1f;
			if (s < appear)
			{
				// 张开：从小一点、半透明冲出来，越张越大
				float u = Ease5.OutQuint(s / appear);
				gap = Mathf.Lerp(0.25f * w, open, u);
				alpha = Mathf.Clamp(s / 0.06f, 0f, 1f);
				scale = Mathf.Lerp(0.8f, 1f, u);
			}
			else if (s < closeT)
			{
				// 蓄力：再往外撑一下、微微发抖，等光束
				float u = Mathf.Min(1f, (s - appear) / Mathf.Max(0.01f, closeIn - appear));
				gap = Mathf.Lerp(open, wide, Ease5.OutQuint(u));
				root.Position = at + new Vector2(Mathf.Sin(s * 90f), 0f) * 2f;
			}
			else
			{
				// 咬合：0.05s 一下合上（牙交错）→ 停 0.1s → 烧掉
				float u = s - closeT;
				float snap = Mathf.Clamp(u / 0.05f, 0f, 1f);
				gap = Mathf.Lerp(wide, -0.06f * w, snap * snap);
				flash = Mathf.Max(0f, 1f - u / 0.15f);
				scale = 1f + 0.06f * Mathf.Max(0f, 1f - u / 0.12f);
				burn = Mathf.Clamp((u - 0.15f) / 0.35f, 0f, 1f);
				root.Position = at;
				if (!shut)
				{
					shut = true;
					Sfx.Play(Sfx.Crush, 0.55f);
					Sfx.Play(Sfx.Slam, 0.35f);
					HitStop.Request(0.07);
					Stage.ShakeDir?.Invoke(3, 90f);
					Glow.Flash(layer, at, new Color(1f, 0.25f, 0.3f), w * 1.2f, 0.2f);
					Vanilla.Burst(layer, Vanilla.Embers, at + new Vector2(0f, 0.1f * w), 0f, 0.8f, Lut5.CurseInk);
				}
			}
			top.Position = new Vector2(0f, -gap * 0.5f);
			bot.Position = new Vector2(0f, gap * 0.5f);
			root.Scale = Vector2.One * scale;
			foreach (ShaderMaterial m in new[] { mt, mb })
			{
				m.SetShaderParameter("alpha", alpha);
				m.SetShaderParameter("burn", burn);
				m.SetShaderParameter("flash", flash);
			}
		});
	}

	// ───────────────────────── 弹跳烧瓶 ─────────────────────────

	private static ulong _flaskUntil;
	private static int _flaskLeft;
	private static bool _flaskFirst;

	/// <summary>Joker 打出弹跳烧瓶（JokerFx.OnPlay）：接下来几秒里原版的瓶子 / 水花 / 毒气藏掉，换成咒怨火球。bounces = 一共跳几下。</summary>
	public static void BeginFlask(int bounces)
	{
		_flaskUntil = Stage.NowMs() + 6000;
		_flaskLeft = bounces;
		_flaskFirst = true;
	}

	public static bool FlaskActive => Stage.NowMs() < _flaskUntil && _flaskLeft > 0;

	/// <summary>原版扔出一个瓶子（NItemThrowVfx.Create）：换成一颗咒怨火球，沿同样的起点 / 终点飞 0.5s。第一跳从亚森的爪尖出手。</summary>
	public static void Throw(Control layer, Vector2 from, Vector2 to)
	{
		if (_flaskFirst && ArseneFx.TryClaw(out Vector2 claw))
		{
			from = claw;
		}
		_flaskFirst = false;
		const float fly = 0.5f;
		float dist = from.DistanceTo(to);
		float arc = Mathf.Clamp(dist * 0.22f, 70f, 200f);
		ShaderMaterial m = new() { Shader = OrbShader };
		m.SetShaderParameter("noise", Paint.Noise);
		m.SetShaderParameter("lut", Lut5.Curse);
		const float size = 280f;
		Node2D root = Fx.Root(layer, from, "P5CurseOrb", fly + 0.1);
		ColorRect orb = new() { Material = m, Size = new Vector2(size, size), Position = new Vector2(-size, -size) * 0.5f, MouseFilter = Control.MouseFilterEnum.Ignore };
		Sprite2D halo = Glow.SoftSprite(root, Vector2.Zero, new Color(0.9f, 0.05f, 0.12f, 0.6f), 7f);
		root.AddChild(orb);
		Fx.Clock(orb);
		Sfx.Play(Sfx.SwishLow, 0.45f);
		Vector2 last = from;
		float trailT = 0f;
		Fx.Animate(root, 0, fly, t =>
		{
			// 抛物线：水平匀速、竖直先升后落
			Vector2 p = from.Lerp(to, t) + Vector2.Up * arc * 4f * t * (1f - t);
			Vector2 v = p - last;
			if (v.LengthSquared() > 0.01f)
			{
				m.SetShaderParameter("dir", v.Normalized());
				m.SetShaderParameter("speed", Mathf.Clamp(v.Length() / 18f, 0.3f, 1f));
			}
			root.GlobalPosition = p;
			last = p;
			// 火尾：一路掉下暗红的火星（每 0.05s 一小团，原版火星粒子 + 很快淡掉的柔光）
			if (t * fly - trailT > 0.05f)
			{
				trailT = t * fly;
				Sprite2D ember = Glow.SoftSprite(layer, p, new Color(0.8f, 0.05f, 0.1f, 0.5f), 3.4f);
				ember.CreateTween().TweenProperty(ember, "modulate:a", 0f, 0.25);
				ember.CreateTween().TweenProperty(ember, "scale", Vector2.One * 0.8f, 0.25);
				Fx.FreeAfter(ember, 0.3);
			}
			halo.Scale = Vector2.One * (7f + 0.6f * Mathf.Sin(t * 40f));
		});
	}

	/// <summary>火球落到敌人身上（= 游戏上毒那一刻，PoisonFx.Apply 转过来）。最后一跳炸得最大。</summary>
	public static void Land(Control layer, Vector2 at, Vector2 top, float width, Node2D? body)
	{
		_flaskLeft--;
		bool last = _flaskLeft <= 0;
		PoisonFx.FlaskBurst(layer, at, top, width, body, last);
	}
}
