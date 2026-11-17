using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 0.9.49 审阅第二批（2026-11-17）：跟踪（小改）、磨蚀（大改）、触媒（大改）、萎靡（大改）、谋划专家（新做，原来走通用积木）。
/// 雷点照旧：不用贴图拼形状、不用棱角分明的多边形、不用光束 / 大片小光点 / 围着敌人转的光环、敌人那里不做画面扭曲；节奏慢快慢、音效克制。
/// 火焰是实时着色器（ArseneFx 那套觉醒火的形状，换成红 / 暗红配色；只用低频噪声的大火舌、颜色和边缘都平滑过渡，不是硬边分层），不是贴图。
/// </summary>
public static partial class SkillRedo5
{
	public static void RegisterBatch10(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["Tracking"] = Tracking10;
		cards["Abrasive"] = Abrasive10;
		cards["Accelerant"] = Accelerant10;
		cards["Malaise"] = Malaise10;
		cards["MasterPlanner"] = MasterPlanner;
	}

	private static Control BackOf(SkillCtx c) => c.Back != null && GodotObject.IsInstanceValid(c.Back) ? c.Back : c.Front;

	// ───────────────────────── 红焰（P5 觉醒火的画法：噪声撕出尖叶形火舌，四档硬边分层）─────────────────────────

	private static Shader? _redFlame;

	private static Shader RedFlame => _redFlame ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D noise : repeat_enable, filter_linear;
uniform float clock = 0.0;
uniform float life = 0.0;      // 0～0.22 窜起来，0.5 之后从下往上烧散
uniform float seed = 0.0;
uniform float sink = 0.0;      // 0～1：火顶往下压回去（萎靡：被压进地里）
uniform float alpha = 1.0;
uniform vec3 c0; uniform vec3 c1; uniform vec3 c2; uniform vec3 c3;
void fragment() {
	float x = UV.x * 2.0 - 1.0;
	float y = 1.0 - UV.y;
	float grow = smoothstep(0.0, 0.22, life);
	float fade = smoothstep(0.55, 1.0, life);
	float hgt = mix(0.3, 1.0, grow) * (1.0 - 0.4 * fade) * (1.0 - 0.85 * sink);
	// 大火舌：只用低频噪声（用户：棱角分明 / 一条条细刺都不行），越往上摆得越开
	float n1 = texture(noise, vec2(x * 0.55 + seed, y * 0.45 - clock * 1.1)).r;
	float n2 = texture(noise, vec2(x * 1.2 + seed * 1.7, y * 0.38 - clock * 1.8)).r;
	float xw = x + (n1 - 0.5) * 0.7 * y;
	float yy = clamp(y / max(hgt, 0.01), 0.0, 1.3);
	float wid = 0.66 * pow(max(1.0 - yy, 0.0), 0.6) * mix(0.65, 1.0, grow) * (1.0 + 0.35 * sink);
	float v = wid - abs(xw) + (n2 - 0.5) * (0.18 + 0.4 * yy);
	v -= smoothstep(0.1, 0.0, y) * 0.2;
	v -= fade * 0.4 + max(0.0, fade * 1.2 - y) * 0.6;
	float a = smoothstep(-0.02, 0.14, v);
	vec3 col = mix(c0, c1, smoothstep(0.0, 0.12, v));
	col = mix(col, c2, smoothstep(0.12, 0.28, v));
	col = mix(col, c3, smoothstep(0.3, 0.5, v) * 0.85);
	COLOR = vec4(col, a * alpha);
}" };

	private static readonly Color[] FlameRed = { new(0.16f, 0f, 0.04f), new(0.72f, 0.02f, 0.12f), new(1f, 0.28f, 0.32f), new(1f, 0.93f, 0.9f) };
	private static readonly Color[] FlameCurse = { new(0.03f, 0f, 0.01f), new(0.22f, 0.01f, 0.05f), new(0.58f, 0.03f, 0.1f), new(0.95f, 0.4f, 0.4f) };

	/// <summary>一根火柱：底边中点在 basePt，宽 w、高 hgt；t0 秒后烧起来、life 秒烧完。返回材质（萎靡要调 sink）。</summary>
	private static ShaderMaterial FlameAt(Control layer, Vector2 basePt, float w, float hgt, Color[] pal, float t0, float life, float alpha = 1f)
	{
		ShaderMaterial m = new() { Shader = RedFlame };
		m.SetShaderParameter("noise", Paint.Noise);
		m.SetShaderParameter("seed", Fx.Rand(0f, 10f));
		m.SetShaderParameter("alpha", alpha);
		m.SetShaderParameter("c0", new Vector3(pal[0].R, pal[0].G, pal[0].B));
		m.SetShaderParameter("c1", new Vector3(pal[1].R, pal[1].G, pal[1].B));
		m.SetShaderParameter("c2", new Vector3(pal[2].R, pal[2].G, pal[2].B));
		m.SetShaderParameter("c3", new Vector3(pal[3].R, pal[3].G, pal[3].B));
		m.SetShaderParameter("life", 0f);
		ColorRect r = new() { Material = m, Size = new Vector2(w, hgt), MouseFilter = Control.MouseFilterEnum.Ignore, Name = "P5RedFlame" };
		layer.AddChild(r);
		r.GlobalPosition = basePt + new Vector2(-w * 0.5f, -hgt);
		Fx.Clock(r);
		Fx.Animate(r, t0, life, k => m.SetShaderParameter("life", k));
		Fx.FreeAfter(r, t0 + life + 0.05);
		return m;
	}

	// ═══════════════════════════ 跟踪（小改）═══════════════════════════

	/// <summary>
	/// 跟踪（虚弱的敌人受到的攻击伤害 +50%；审阅：还不错但太简约 → 小改）：原来的「面具寒光 → 细红线连到每个敌人 → 准星锁住」保留；
	/// 加上：扫的时候四周稍微暗下来、他身后一团暗红慢慢亮起；每锁住一个，敌人身上一下红光（不是光环）；全部锁完，面具上一颗更大的星光、身后红光一跳。
	/// </summary>
	public static void Tracking10(SkillCtx c)
	{
		float h = c.Tall;
		Control back = BackOf(c);
		var targets = new List<SkillFx.Victim>(c.Enemies);
		if (targets.Count == 0 && c.Target is SkillFx.Victim only) targets.Add(only);
		float end = 0.07f + 0.11f * Mathf.Max(0, targets.Count - 1) + 0.12f + 0.24f;
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		Dim(back, mid + new Vector2(c.Face * h * 1.5f, 0f), new Vector2(h * 6f, h * 3f), end + 0.6, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.75f, 0.25f)) * 0.55f);
		Glow.Flash(back, c.Mask, new Color(0.8f, 0.04f, 0.12f), h * 1.2f, end + 0.3f);
		CardSkillFx.Tracking(c);
		for (int i = 0; i < targets.Count; i++)
		{
			SkillFx.Victim v = targets[i];
			Vector2 at = v.Feet.Lerp(v.Top, 0.55f);
			float lockAt = 0.07f + 0.11f * i + 0.12f + 0.24f;
			Fx.After(c.Front, lockAt, () => Glow.Flash(c.Front, at, Red, Mathf.Max(v.Width, h * 0.5f) * 1.1f, 0.25f));
		}
		Fx.After(c.Front, end + 0.04f, () =>
		{
			Glow.Glint(c.Front, c.Mask + new Vector2(c.Face * h * 0.03f, 0f), 1.3f);
			Glow.Flash(back, c.Mask, Red, h * 1.5f, 0.35f);
		});
	}

	// ═══════════════════════════ 磨蚀（大改）═══════════════════════════

	/// <summary>
	/// 磨蚀（敏捷 + 荆棘；审阅：太怪不契合 → 大改，月牙全部去掉）：「刺风」。
	/// 慢：四周暗下来，一圈红白的风刃绕着他越转越快（前后两层，绕到身后的在人后面）—— 灵巧；
	/// 快：一顿、身上一下红光，绕身的风刃「唰」地撑大一圈再散掉（荆棘：谁碰谁挨扎），重闪、轻震；
	/// 慢：剩下几道细的风刃贴着身体慢慢转、慢慢淡掉。
	/// </summary>
	public static void Abrasive10(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = BackOf(c), front = c.Front;
		const float beat = 0.42f;
		Vector2 mid = c.Feet + new Vector2(0f, -h * 0.55f);
		S("gx_whoosh", 0.14f, 0.03f);
		S("gx_slice", 0.24f, beat);
		Dim(back, mid, new Vector2(h * 3.4f, h * 2.8f), beat + 0.8, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.15f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.7f);
		Vortex(front, back, c.Feet, h, h * 0.5f, 6, 0.02f, beat + 0.06f, f, 0.12f, 0.95f, 1.15f, 0.06f);
		Glow.Flash(back, mid, new Color(0.85f, 0.04f, 0.14f), h * 1.6f, beat + 0.1f);
		if (c.SelfTint is { } tint)
			Fx.Animate(front, 0, beat + 0.6, t =>
			{
				float s = t * (beat + 0.6f);
				float k = 0.9f * Mathf.Exp(-Mathf.Pow((s - beat) / 0.06f, 2f)) + 0.2f * Ease5.Seg(s, 0.05f, beat - 0.05f) * (1f - Ease5.Seg(s, beat, 0.5f));
				tint(Colors.White.Lerp(new Color(1.5f, 0.75f, 0.8f), k));
			});
		// 炸开：风刃一下子撑大一圈（半径翻倍、更粗）再散掉（荆棘：一圈往外扎的风）；不用放射状直线、也不用平面的弧刃
		Vortex(front, back, c.Feet, h, h * 0.85f, 5, beat, 0.3f, f, 0.15f, 0.9f, 1.6f, 0.05f);
		Fx.After(front, beat, () =>
		{
			HitStop.Request(0.045);
			Fx.Shake(1);
			Glow.Flash(front, mid, Hot, h * 0.45f, 0.1f);
			Glow.Flash(back, mid, new Color(1f, 0.12f, 0.2f), h * 2.4f, 0.4f);
		});
		// 余下的细风刃贴身慢转淡掉
		Vortex(front, back, c.Feet, h, h * 0.36f, 3, beat + 0.08f, 0.7f, f, 0.25f, 0.8f, 0.5f, 0.04f);
	}

	// ═══════════════════════════ 触媒（大改）═══════════════════════════

	/// <summary>
	/// 触媒（中毒额外触发；审阅：太简单没特色 → 大改）：「咒火点燃两次」。他挥手，伸直的掌心「噗」地燃起一小簇红焰（P5 觉醒火的画法）；
	/// 一拍之后第二次点燃 —— 掌心的火猛地窜高一大截，同一瞬间他身后整个烧起一根比人还高的红焰（顿帧、轻震、身上一下红光）；
	/// 然后两团火一起从下往上烧散。两次点燃 = 毒多触发一次。只在他身上，不连到敌人。
	/// </summary>
	public static void Accelerant10(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face;
		Control back = BackOf(c), front = c.Front;
		Vector2 palm = c.Feet + new Vector2(f * h * 0.28f, -h * 0.8f);    // 挥手伸直的手
		const float ig1 = 0.03f, ig2 = 0.3f;
		S("gx_pop", 0.14f, ig1);
		S("gx_whoosh", 0.2f, ig2 - 0.02f);
		Dim(back, c.Feet + new Vector2(0f, -h * 0.6f), new Vector2(h * 3.4f, h * 3f), ig2 + 0.9, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.2f)) * (1f - Ease5.Seg(s, 0.7f, 0.3f)) * 0.7f);
		// 第一次：掌心一小簇
		FlameAt(front, palm + new Vector2(0f, h * 0.06f), h * 0.3f, h * 0.42f, FlameRed, ig1, 0.42f);
		Fx.After(front, ig1, () =>
		{
			Glow.Flash(front, palm, new Color(1f, 0.75f, 0.75f), h * 0.22f, 0.08f);
			Glow.Flash(back, palm, new Color(0.85f, 0.03f, 0.14f), h * 0.6f, 0.25f);
		});
		// 第二次：掌心猛地窜高 + 身后一根比人高的火柱
		FlameAt(front, palm + new Vector2(0f, h * 0.08f), h * 0.42f, h * 0.95f, FlameRed, ig2, 0.62f);
		FlameAt(back, c.Feet + new Vector2(-f * h * 0.04f, h * 0.02f), h * 1.05f, h * 1.75f, FlameRed, ig2 + 0.02f, 0.75f, 0.92f);
		Fx.After(front, ig2, () =>
		{
			HitStop.Request(0.04);
			Fx.Shake(1);
			Glow.Flash(front, palm, Hot, h * 0.35f, 0.1f);
			Glow.Flash(back, c.Feet + new Vector2(0f, -h * 0.6f), Red, h * 2.4f, 0.45f);
			Glow.Ripple(front, palm, h * 0.9f, 0.32f, 0.025f);
		});
		if (c.SelfTint is { } tint)
			Fx.After(front, ig2, () => Fx.Animate(front, 0, 0.6, t => tint(Colors.White.Lerp(new Color(1.6f, 0.7f, 0.75f), (1f - t) * (1f - t)))));
	}

	// ═══════════════════════════ 萎靡（大改）═══════════════════════════

	/// <summary>
	/// 萎靡（敌人失去力量 + 虚弱；审阅：别让敌人那里画面扭曲、不要小光点和光环，全部重做 → 大改）：「咒火吞噬，再压灭」。
	/// 他扶着面具，面具上一簇暗红的火一跳；快：目标脚下「轰」地烧起一根暗红黑焰把它整个吞进去（前面一层半透明、后面一层实的）；
	/// 慢：火在它身上烧一会儿，它身上越来越暗；快：火被猛地往下压回地里（力量被压掉）、顿帧，它身上发灰；慢：最后一点火苗烧散。
	/// </summary>
	public static void Malaise10(SkillCtx c)
	{
		if (c.Target is not SkillFx.Victim v) return;
		float jh = c.Tall, h = Mathf.Max(120f, (v.Feet - v.Top).Length());
		Control back = BackOf(c), front = c.Front;
		const float erupt = 0.24f, crush = 0.72f, life = 1.15f;
		float wid = Mathf.Max(v.Width * 1.25f, h * 0.9f);
		S("gx_whoosh", 0.12f, erupt - 0.02f);
		S("gx_pop", 0.22f, crush);
		// 面具上一簇暗红火
		FlameAt(front, c.Mask + new Vector2(c.Face * jh * 0.02f, jh * 0.06f), jh * 0.22f, jh * 0.34f, FlameRed, 0f, 0.4f);
		Glow.Flash(back, c.Mask, new Color(0.8f, 0.03f, 0.12f), jh * 0.7f, 0.3f);
		// 目标脚下的黑焰：后面一层实的、前面一层半透明窄一些
		ShaderMaterial mb = FlameAt(back, v.Feet + new Vector2(0f, h * 0.04f), wid, h * 1.45f, FlameCurse, erupt, life - erupt);
		ShaderMaterial mf = FlameAt(front, v.Feet + new Vector2(0f, h * 0.05f), wid * 0.8f, h * 1.2f, FlameCurse, erupt + 0.03f, life - erupt - 0.03f, 0.55f);
		Node2D driver = Fx.Root(front, v.Feet, "P5MalaiseSink", life + 0.05);
		Fx.Animate(driver, 0, life, t =>
		{
			float s = t * life;
			float k = Ease5.Seg(s, crush, 0.12f);
			k = k * k;
			mb.SetShaderParameter("sink", k);
			mf.SetShaderParameter("sink", k);
		});
		Fx.After(front, erupt, () => Glow.Flash(back, v.Feet.Lerp(v.Top, 0.4f), new Color(0.5f, 0.02f, 0.08f), h * 2f, 0.4f));
		Fx.After(front, crush + 0.08f, () =>
		{
			HitStop.Request(0.035);
			Fx.Shake(1);
			Glow.Flash(back, v.Feet, new Color(0.45f, 0.02f, 0.08f), wid * 1.6f, 0.35f);
		});
		if (v.Tint is { } vt)
			Fx.Animate(driver, 0, life + 0.6, t =>
			{
				float s = t * (life + 0.6f);
				float dark = Ease5.Seg(s, erupt, crush - erupt) * 0.6f + 0.4f * Ease5.Seg(s, crush + 0.06f, 0.1f);
				dark *= 1f - Ease5.Seg(s, life, 0.6f) * 0.75f;
				vt(Colors.White.Lerp(new Color(0.48f, 0.38f, 0.42f), dark));
			});
	}

	// ═══════════════════════════ 谋划专家（新做）═══════════════════════════

	/// <summary>
	/// 谋划专家（打出技能牌时该牌获得奇巧；原来走通用积木太简单 → 新做）：「排兵布阵」。
	/// 慢：五张红黑的预告牌从他手里一张张弹出去，在他头顶后方摊成一道扇面（带拖尾）；停一下，牌面从左到右依次一亮（每张都被「做了手脚」= 奇巧）；
	/// 快：五张一齐收拢叠成一张、白闪、短顿帧；慢：那一张翻着落回他手里，化成一点红光收进掌心。
	/// </summary>
	public static void MasterPlanner(SkillCtx c)
	{
		float h = c.Tall, u = c.Unit, f = c.Face, w = h * 0.16f;
		Control back = BackOf(c);
		Vector2 hub = c.Feet + new Vector2(-f * h * 0.08f, -h * 1.28f);
		const int n = 5;
		const float out0 = 0.04f, step = 0.06f, fly = 0.22f, mark0 = 0.42f, markStep = 0.06f, gather = 0.82f, home = 1.12f;
		S("gx_card_slide", 0.16f);
		S("gx_card_stack", 0.16f, gather);
		Glow.Flash(back, hub, new Color(0.75f, 0.03f, 0.12f), h * 1.6f, home + 0.3f);
		Vector2 palm = c.Hand + new Vector2(f * h * 0.04f, 0f);
		for (int i = 0; i < n; i++)
		{
			int k = i;
			float ang = -Mathf.Pi * 0.5f + f * (k - 2f) * 0.42f;
			Vector2 slot = hub + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * h * 0.42f + new Vector2(0f, h * 0.18f);
			float tilt = f * (k - 2f) * 0.3f;
			float d = out0 + step * k, markAt = mark0 + markStep * k;
			Phantom.P5Card card = Phantom.Card(c.Front, palm, w, home + 0.2, 0.8f);
			card.Root.Visible = false;
			Vector2 ctrl = palm.Lerp(slot, 0.5f) + new Vector2(f * h * 0.25f, -h * 0.1f);
			Fx.Animate(card.Root, d, home - d, t =>
			{
				card.Root.Visible = true;
				float s = d + t * (home - d);
				if (s < d + fly)
				{
					float e = Ease5.OutQuint((s - d) / fly);
					card.Place(palm.Lerp(ctrl, e).Lerp(ctrl.Lerp(slot, e), e), tilt * e, (1f - e) * 8f, 0.6f + 0.4f * e);
				}
				else if (s < gather)
				{
					card.Place(slot + new Vector2(0f, Mathf.Sin(s * 5f + k) * h * 0.008f), tilt, 0f, 1f);
					float m = s >= markAt ? Mathf.Exp(-(s - markAt) * 7f) : 0f;
					card.Set("glow", m);
					card.Set("sheen", s >= markAt ? Mathf.Min(1f, (s - markAt) / 0.15f) * 3f - 1f : -1f);
					if (card.Trail != null) card.Trail.Fade = 1f - Mathf.Min(1f, (s - d - fly) / 0.1f);
				}
				else
				{
					float e = Fx.InCubic(Mathf.Min(1f, (s - gather) / 0.1f));
					card.Place(slot.Lerp(hub, e), tilt * (1f - e), 0f, 1f);
					card.Set("glow", e);
					if (k != 2 && e >= 1f) card.Root.Visible = false;
				}
			});
			Fx.After(c.Front, markAt, () => Glow.Glint(c.Front, slot + new Vector2(w * 0.3f, -w * 0.55f), 0.5f));
			Fx.After(c.Front, home, () => { card.Trail?.Kill(); card.Root.Visible = false; });
		}
		Fx.After(c.Front, gather + 0.1f, () =>
		{
			HitStop.Request(0.03);
			Glow.Flash(c.Front, hub, Hot, w * 3f, 0.1f);
			Glow.Flash(back, hub, Red, h * 1.2f, 0.3f);
			Glow.Glint(c.Front, hub, 1f);
		});
		// 叠成的那一张翻着落回手里
		Phantom.P5Card last = Phantom.Card(c.Front, hub, w, home + 0.6, 0.9f);
		last.Root.Visible = false;
		Fx.Animate(last.Root, gather + 0.1f, home + 0.35f - gather - 0.1f, t =>
		{
			last.Root.Visible = true;
			float e = Fx.InCubic(t) * 0.7f + t * 0.3f;
			Vector2 ctrl2 = hub.Lerp(palm, 0.5f) + new Vector2(f * h * 0.3f, -h * 0.05f);
			last.Place(hub.Lerp(ctrl2, e).Lerp(ctrl2.Lerp(palm, e), e), e * f * 1.2f, e * 10f, 1f - 0.5f * e);
		});
		Fx.After(c.Front, home + 0.35f, () =>
		{
			last.Trail?.Kill();
			last.Root.Visible = false;
			Glow.Flash(c.Front, palm, Hot, h * 0.25f, 0.1f);
			Glow.Flash(back, palm, Red, h * 0.9f, 0.3f);
			Phantom.Shards(c.Front, palm, 6, 200f * u, 450f * u, Vector2.Up, 60f, 0.25f, 300f * u, 14f * u);
		});
	}
}
