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
	/// ① 0～0.3 红黑的暗焰贴着他的轮廓从脚下烧上来（同拿起技能牌时的面具火那一套，换成红黑），脚下两侧扫出两股火星，他暗下去；
	/// ② 0.3 主拍：火舌一窜——小的红闪、火星往上、轻震；
	/// ③ 烧着、火星往上飘；0.85 第二下（翻倍）：火从脚往上剥离、被拔高，从头顶上方化成一股火星散掉，人亮回来。
	/// 拍子：点火（风，很轻）→ 一窜（主拍）→ 第二下（轻）。
	/// </summary>
	public static void Shadowmeld(SkillCtx c)
	{
		float h = c.Tall;
		const float surge = 0.3f, second = 0.85f, life = 1.4f;
		Control back = c.Back ?? c.Front;
		Vector2 body = new(c.Feet.X, c.Feet.Y - h * 0.5f);
		S("gx_whoosh", 0.12f);
		S("gx_burn", 0.24f, surge);
		S("gx_pop", 0.14f, second);

		// 贴身的暗焰（人后面一层）：从脚下 0.3s 烧满全身（BodyAura 自己的点火），火舌沿轮廓均匀；不要脚下那圈底座火
		//（用户 2026-11-13：脚下有一道明显的横条，不像特效）。不用做太大（用户：金卡也不用到华丽收场那种程度，人物周围帅一点就行）
		// 离屏画面在头顶多留 0.55 身高、两边各多 0.15：火舌拔高时不碰边
		Rect2 region = new(c.Region.Position - new Vector2(h * 0.15f, h * 0.55f), c.Region.Size + new Vector2(h * 0.3f, h * 0.55f));
		BodyAura? aura = BodyAura.CloneBody != null ? new BodyAura(back, region, c.Feet, 0f, DarkFlame, even: 0.8f) : null;
		// 进场：点火时脚下两侧贴地扫出两股火星
		Fx.After(back, 0.16, () =>
		{
			for (int sd = -1; sd <= 1; sd += 2)
			{
				Glow.Sparks(c.Front, c.Feet + new Vector2(sd * h * 0.1f, -h * 0.02f), 16, 160f, 420f, 0.45f, Embers, 60f, 0.04f, 0.12f,
					new Vector2(sd, -0.18f), 14f);
			}
		});
		// 主拍：火舌一窜（收着：小一点的红闪、轻震，没有顿帧）
		Fx.After(back, surge, () =>
		{
			aura?.Surge(0.7f, 0.32f);
			Fx.Shake(1);
			Glow.Flash(back, body, new Color(1f, 0.12f, 0.18f), h * 0.9f, 0.2f);
			Glow.Sparks(c.Front, body + new Vector2(0f, h * 0.2f), 26, 90f, 300f, 0.55f, Embers, -220f, 0.04f, 0.12f, Vector2.Up, 50f);
		});
		// 烧着的时候火星慢慢往上飘
		for (int i = 0; i < 3; i++)
		{
			float d = surge + 0.12f + 0.15f * i;
			Fx.After(back, d, () => Glow.Sparks(c.Front, body + new Vector2(Fx.Rand(-0.15f, 0.15f) * h, h * 0.2f), 10, 30f, 110f, 0.8f, Embers, -140f, 0.04f, 0.1f,
				Vector2.Up, 40f));
		}
		// 退场（翻倍的第二下）：火从脚往上剥离、被往上拔高，最后一截从头顶上方化成一股往上的火星散掉
		Fx.After(back, second, () => aura?.PeelUp(0.45f, 1.3f));
		Fx.After(back, second + 0.3, () =>
		{
			Vector2 top = c.Head + new Vector2(0f, -h * 0.08f);
			Glow.Sparks(c.Front, top, 30, 120f, 380f, 0.7f, Embers, -160f, 0.04f, 0.12f, Vector2.Up, 35f);
			Glow.Flash(back, top, new Color(1f, 0.2f, 0.25f), h * 0.5f, 0.22f);
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
