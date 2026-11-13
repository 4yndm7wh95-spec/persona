using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 技能牌重做第五批（2026-11-13，会话 C）。第四批的融入暗影 / 余像 / 萎靡（影子、墨、烟）改了三版都被否（用户：质量太差、塑料感、看不出关联）；
/// 斗篷戏法也被否（用户：太难做了，弄点特效就好，不要非弄这些）→ 用已经认可的东西做「特效」：
/// 贴着人物轮廓烧的火（BodyAura，就是拿起技能牌时的面具蓝焰）换成红黑暗焰 + 闪光、火星、折射波。细节要多、不要粗糙（用户 2026-11-13）。
/// 登记在 CardSkillFx 最后，覆盖第四批的同名牌。
/// </summary>
public static class SkillRedo5
{
	public static void Register(Dictionary<string, Action<SkillCtx>> cards)
	{
		cards["Shadowmeld"] = Shadowmeld;
	}

	private static void S(string name, float vol, float at = 0f) => Sfx.Take(name, vol * 0.8f, at);

	/// <summary>红黑暗焰的四档：暗（近黑的深红）→ 绯红 → 亮红 → 白芯。</summary>
	private static readonly Color[] DarkFlame =
	{
		new(0.09f, 0.0f, 0.025f), new(0.62f, 0.02f, 0.07f), new(1f, 0.16f, 0.2f), new(1f, 0.88f, 0.84f),
	};

	private static readonly Color[] Embers = { new(1f, 0.92f, 0.88f), new(1f, 0.2f, 0.26f), new(0.5f, 0.0f, 0.08f) };

	/// <summary>
	/// 融入暗影（本回合获得的格挡翻倍），约 1.3s：
	/// ① 0～0.25 红黑的暗焰贴着他的轮廓从脚下烧上来（同拿起技能牌时的面具火那一套，换成红黑），他暗下去；
	/// ② 0.25 主拍：火舌猛地一窜——红闪、火星往上迸、空间扭一下、轻震；
	/// ③ 烧着、火星往上飘；0.85 第二下（翻倍）：火舌再往上一冲、一闪，散掉，人亮回来。
	/// 拍子：点火（风，很轻）→ 一窜（主拍）→ 第二下（轻）。
	/// </summary>
	public static void Shadowmeld(SkillCtx c)
	{
		float h = c.Tall;
		const float surge = 0.25f, second = 0.85f, life = 1.3f;
		Control back = c.Back ?? c.Front;
		Vector2 body = new(c.Feet.X, c.Feet.Y - h * 0.5f);
		S("gx_whoosh", 0.12f);
		S("gx_burn", 0.24f, surge);
		S("gx_pop", 0.14f, second);

		// 贴身的暗焰（人后面一层）
		// 不要脚下那圈底座火（用户 2026-11-13：脚下有一道明显的横条，不像特效）；火舌沿轮廓分布均匀一些（even）
		BodyAura? aura = BodyAura.CloneBody != null ? new BodyAura(back, c.Region, c.Feet, 0f, DarkFlame, even: 0.8f) : null;
		Fx.After(back, surge, () =>
		{
			aura?.Surge(1.0f, 0.35f);
			HitStop.Request(0.04);
			Fx.Shake(1);
			Glow.Flash(back, body, new Color(1f, 0.12f, 0.18f), h * 1.2f, 0.25f);
			Glow.Ripple(c.Front, body, h * 1.2f, 0.5f, 0.022f);
			Glow.Sparks(c.Front, c.Feet + new Vector2(0f, -h * 0.1f), 40, 120f, 420f, 0.6f, Embers, -260f, 0.05f, 0.15f, Vector2.Up, 55f);
		});
		// 烧着的时候火星慢慢往上飘
		for (int i = 0; i < 3; i++)
		{
			float d = surge + 0.12f + 0.15f * i;
			Fx.After(back, d, () => Glow.Sparks(c.Front, body + new Vector2(Fx.Rand(-0.15f, 0.15f) * h, h * 0.2f), 10, 30f, 110f, 0.8f, Embers, -140f, 0.04f, 0.1f,
				Vector2.Up, 40f));
		}
		Fx.After(back, second, () =>
		{
			aura?.End(true);
			Glow.Flash(back, body + new Vector2(0f, -h * 0.15f), new Color(1f, 0.2f, 0.25f), h * 0.9f, 0.2f);
			Glow.Sparks(c.Front, body + new Vector2(0f, -h * 0.3f), 24, 150f, 380f, 0.5f, Embers, -220f, 0.04f, 0.12f, Vector2.Up, 70f);
		});
		// 他沉进暗处，再亮回来
		if (c.SelfTint is { } tint)
		{
			Node2D tk = Fx.Root(c.Front, c.Feet, "P5MeldTint", life + 0.1);
			Color dim = new(0.28f, 0.2f, 0.26f), ember = new(0.85f, 0.35f, 0.4f);
			Fx.Animate(tk, 0, life, t =>
			{
				float s = t * life;
				float k = Fx.OutCubic(Ease5.Seg(s, 0.02f, surge)) * (1f - Fx.OutCubic(Ease5.Seg(s, second, 0.4f)));
				Color col = Colors.White.Lerp(dim, k);
				float flick = s >= surge ? Mathf.Exp(-(s - surge) * 9f) * 0.5f : 0f;
				tint(col.Lerp(ember, flick));
			});
			tk.TreeExiting += () => tint(Colors.White);
		}
	}
}
