using Godot;

namespace P5Joker;

/// <summary>
/// 狩猎（稀有：10 伤害，斩杀 → 额外一次卡牌奖励；用户：特效稍微表达一下斩杀时额外获得一次卡牌奖励）第三版「夺宝」（2026-11-18；前两版：贴图猎爪、着色器猎爪，都被否）。
/// 全部实时渲染，不用贴图：
/// · 命中（Hit，DamagePatch）：重斩 + 一道着色器横劈（墨之刃那套由里向外的横劈）补在上面，顿帧；
/// · 斩杀拿到奖励（Reward，CombatRoom.AddExtraReward）：「偷到宝了」——
///   从猎物身上推出一道透明的波，波过之处世界褪成冷灰（红色不褪；他身边一圈保留颜色，和子弹时间同一个语言），一声「嗒」；
///   猎物身上「啪」地抽出一张预告信（背后一团暖金的光、往上飘的金色光尘），翻着升起来、在半空停一下、高光扫过；
///   然后被他一把抢走：牌划一道弧线飞进他手里（两张残影跟着），手里一亮、一声「叮」，颜色从他身边涌回去。~1s。
/// </summary>
public static class HuntFx
{
	private static Vector2 _at, _hand, _chest;
	private static float _h, _tall, _face = 1f;
	private static ulong _until;

	private static Shader? _freeze;

	/// <summary>褪色波（读屏幕）：origin 推出的波里褪成冷灰，红色和他身边一圈（keep）不褪；波前把画面轻轻折一下（没有轮廓线）。</summary>
	internal static Shader Freeze => _freeze ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform vec2 origin = vec2(0.7, 0.6);
uniform vec2 keep = vec2(0.25, 0.6);
uniform float keep_r = 0.25;
uniform float aspect = 1.7778;
uniform float radius = 0.0;
uniform float amount = 1.0;
void fragment() {
	vec2 q = (SCREEN_UV - origin) * vec2(aspect, 1.0);
	float d = length(q);
	float front = exp(-pow((d - radius) / 0.035, 2.0)) * amount;
	vec2 uv = SCREEN_UV - normalize(q + vec2(1e-4)) * front * 0.006 / vec2(aspect, 1.0);
	vec3 c = texture(screen_tex, uv).rgb;
	float inside = 1.0 - smoothstep(radius - 0.12, radius, d);
	vec2 kq = (SCREEN_UV - keep) * vec2(aspect, 1.0);
	float him = 1.0 - smoothstep(keep_r * 0.65, keep_r, length(kq * vec2(1.0, 0.62)));
	float red = smoothstep(0.1, 0.32, c.r - max(c.g, c.b));
	float g = dot(c, vec3(0.3, 0.55, 0.15));
	vec3 gray = vec3(g) * vec3(0.9, 0.95, 1.06) * 0.82;
	float k = amount * inside * (1.0 - him) * (1.0 - red * 0.9);
	COLOR = vec4(mix(c, gray, k), 1.0);
}" };

	/// <summary>命中：记下猎物、Joker 的手和胸口（斩杀奖励的特效要用）。h = 敌人中心到头顶，tall = Joker 身高。</summary>
	public static void Hit(Control layer, Vector2 at, float h, float face, Vector2 hand, Vector2 chest, float tall)
	{
		float u = Fx.Screen(layer).Y / 1080f;
		_at = at; _h = h; _face = face; _hand = hand; _chest = chest; _tall = tall;
		_until = Stage.NowMs() + 2500;
		KnifeFx.Hit(layer, at, face, true);
		SkillRedo5.SlashArc(layer, at + new Vector2(0f, -h * 0.08f), h * 1.15f, h * 0.3f, -0.22f, face, 0.03f, 0.12f, 0.24f, h);
		Fx.After(layer, 0.05f, () =>
		{
			HitStop.Request(0.05);
			Glow.Flash(layer, at, Pal5.Bright, h * 1.6f, 0.3f);
			Phantom.Shards(layer, at, 8, 260f * u, 700f * u, new Vector2(face, 0.2f), 40f, 0.3f, 500f * u, 12f * u);
		});
	}

	/// <summary>斩杀拿到额外奖励（只在刚刚狩猎命中过的 2.5s 里放）。</summary>
	public static void Reward(Control layer)
	{
		if (Stage.NowMs() > _until) return;
		_until = 0;
		float u = Fx.Screen(layer).Y / 1080f, h = _h, tall = _tall > 1f ? _tall : 380f * u;
		Vector2 prey = _at, hand = _hand, scr = Fx.Screen(layer);
		const float pop = 0.08f, hold = 0.42f, dash = 0.6f, arrive = 0.78f, end = 1.15f;

		// ── 褪色波：从猎物身上推出去，抢到牌之后退回去 ──
		Node2D sroot = Fx.ScreenRoot(layer, "P5HuntFreeze");
		ColorRect fr = new() { Size = scr, MouseFilter = Control.MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = Freeze } };
		sroot.AddChild(fr);
		var fm = (ShaderMaterial)fr.Material;
		Vector2 preyS = Fx.ScreenPoint(layer, prey) / scr, keepS = Fx.ScreenPoint(layer, _chest) / scr;
		float tallS = (Fx.ScreenPoint(layer, _chest + new Vector2(0f, tall)) - Fx.ScreenPoint(layer, _chest)).Length() / scr.Y;
		fm.SetShaderParameter("origin", preyS);
		fm.SetShaderParameter("keep", keepS);
		fm.SetShaderParameter("keep_r", 0.0001f);          // 他也一起褪（黑衣 + 红手套，P5 的灰底红）；留一圈颜色看着像一块聚光灯
		_ = tallS;
		fm.SetShaderParameter("aspect", scr.X / scr.Y);
		Fx.FreeAfter(sroot, end + 0.05);
		float reach = (scr.X / scr.Y) * 1.3f;
		Fx.Animate(fr, 0, end, t =>
		{
			float s = t * end;
			float r = reach * (1f - Mathf.Pow(1f - Mathf.Min(1f, s / 0.3f), 2f));
			float back = Ease5.Seg(s, arrive, end - arrive);
			fm.SetShaderParameter("radius", r * (1f - Fx.InCubic(back)) + 0.001f);
			fm.SetShaderParameter("amount", Mathf.Min(1f, s / 0.05f) * (1f - Ease5.Seg(s, end - 0.12f, 0.12f)));
		});
		Sfx.Take("gx_tock", 0.28f);
		Sfx.Take("gx_freeze", 0.16f, 0.02f);
		HitStop.Request(0.05);

		// ── 抽出来的预告信（暖金的光托着） ──
		float w = Mathf.Clamp(tall * 0.22f, 60f * u, 110f * u);
		Vector2 top = prey + new Vector2(-_face * h * 0.2f, -h * 1.25f);
		Phantom.P5Card card = Phantom.Card(layer, prey, w, end);
		card.Root.Visible = false;
		var ghosts = new Phantom.P5Card[2];
		for (int i = 0; i < 2; i++)
		{
			ghosts[i] = Phantom.Card(layer, prey, w, end);
			ghosts[i].Root.Visible = false;
			ghosts[i].Rect.Modulate = new Color(1f, 0.75f, 0.45f, 0.35f - 0.12f * i);
		}
		Color gold = new(1f, 0.78f, 0.38f);
		Fx.After(layer, pop, () =>
		{
			Sfx.Take("gx_card_slide", 0.22f);
			Glow.Flash(layer, prey, gold, h * 1.3f, 0.35f);
			Glow.Glint(layer, prey + new Vector2(0f, -h * 0.2f), 1f);
			Phantom.Shards(layer, prey, 6, 200f * u, 500f * u, Vector2.Up, 70f, 0.3f, 300f * u, 10f * u);
		});
		Fx.After(layer, pop + 0.2f, () =>
		{
			Glow.Sparks(layer, top, 22, 20f * u, 100f * u, 0.9f, new[] { new Color(1f, 0.95f, 0.75f), gold, new Color(0.9f, 0.4f, 0.1f) }, -50f * u, 0.07f, 0.2f, Vector2.Up, 180f);
			Glow.Flash(layer, top, new Color(1f, 0.72f, 0.3f), w * 4.2f, dash - pop - 0.15f);
			Glow.Flash(layer, top, new Color(1f, 0.88f, 0.6f), w * 2f, dash - pop - 0.2f);
			Glow.Glint(layer, top + new Vector2(w * 0.45f, -w * 0.62f), 0.8f, 0.05f);
		});
		var trail = new Vector2[2];
		Vector2 ctrl = top.Lerp(hand, 0.5f) + new Vector2(0f, -tall * 0.35f);
		Fx.Animate(card.Root, pop, end - pop, t =>
		{
			float s = pop + t * (end - pop);
			card.Root.Visible = s < arrive;
			Vector2 p;
			float flip, sc, tilt;
			if (s < hold)
			{
				float k = Ease5.OutBack(Mathf.Min(1f, (s - pop) / 0.26f));
				p = prey.Lerp(top, k) + new Vector2(0f, Mathf.Sin(s * 6f) * h * 0.02f);
				flip = Mathf.Tau * 1.5f * (1f - Fx.OutCubic(Mathf.Min(1f, (s - pop) / 0.32f)));
				sc = Mathf.Lerp(0.4f, 1.1f, Mathf.Min(1f, k));
				tilt = 0.15f * Mathf.Sin(s * 5f);
			}
			else if (s < dash)
			{
				p = top + new Vector2(0f, Mathf.Sin(s * 6f) * h * 0.02f);
				flip = 0f; sc = 1.1f; tilt = 0.15f * Mathf.Sin(s * 5f);
			}
			else
			{
				float e = Fx.InCubic(Mathf.Min(1f, (s - dash) / (arrive - dash)));
				p = top.Lerp(ctrl, e).Lerp(ctrl.Lerp(hand, e), e);
				flip = -Mathf.Pi * e * 2f;
				sc = Mathf.Lerp(1.1f, 0.5f, e);
				tilt = -_face * 0.6f * e;
			}
			card.Place(p, tilt, flip, sc);
			card.Set("sheen", Mathf.Clamp((s - pop - 0.15f) / 0.3f, 0f, 1f) * 3f - 1f);
			card.Set("glow", s < pop + 0.12f ? 0.8f : 0.12f + 0.08f * Mathf.Sin(s * 18f));
			// 飞的时候两张残影跟着（落后几帧的位置）
			for (int i = 0; i < 2; i++)
			{
				var g = ghosts[i];
				bool on = s > dash && s < arrive + 0.04f * (i + 1);
				g.Root.Visible = on;
				if (!on) { trail[i] = p; continue; }
				float lag = 0.035f * (i + 1);
				float e = Fx.InCubic(Mathf.Clamp((s - lag - dash) / (arrive - dash), 0f, 1f));
				Vector2 gp = top.Lerp(ctrl, e).Lerp(ctrl.Lerp(hand, e), e);
				g.Place(gp, -_face * 0.6f * e, -Mathf.Pi * e * 2f, Mathf.Lerp(1.1f, 0.5f, e));
			}
		});
		// 抢到手里：手里一亮、一声「叮」，颜色涌回去
		Fx.After(layer, dash, () => Sfx.Take("gx_whoosh", 0.2f));
		Fx.After(layer, arrive, () =>
		{
			Sfx.Take("gx_ping", 0.3f);
			Sfx.Play("card_draw", 0.3f);
			Glow.Flash(layer, hand, Pal5.Hot, tall * 0.25f, 0.12f);
			Glow.Flash(layer, hand, gold, tall * 0.6f, 0.35f);
			Glow.Glint(layer, hand, 0.9f);
			Glow.Sparks(layer, hand, 12, 60f * u, 200f * u, 0.5f, new[] { new Color(1f, 0.92f, 0.7f), gold }, 200f * u, 0.05f, 0.14f, Vector2.Up, 180f);
		});
	}
}
