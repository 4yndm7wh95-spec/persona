using Godot;

namespace P5Joker;

/// <summary>
/// 第四版刀组特效（打击动作）：Codex 手绘刀光（Paint：扫出 → 溶解）+ 原版命中粒子换 P5 LUT（Vanilla）+ P5 漫画爆框。
/// 不画任何几何形状。打击确认感（顿帧、震屏、冲击帧）在 Impact 里，由伤害结算触发。
/// </summary>
public static class KnifeFx
{
	/// <summary>Joker 出刀那一下的刀光（跟着手的轨迹扫过去，from → to 是刀尖的起止点）。</summary>
	public static void Swing(Control layer, Vector2 from, Vector2 to, float facing = 1f)
	{
		Vector2 d = to - from;
		float len = d.Length();
		// slash_thin 的贴图：弧线从左上起笔、往右下收尖，向下鼓 → 上撩要上下翻一下，沿起止点方向摆
		float rot = d.Angle();
		Vector2 scale = new Vector2(len / 820f * 1.25f, -0.55f * facing);
		Paint.Sweep(layer, "slash_thin", from.Lerp(to, 0.5f), rot, scale, Lut5.Blood, 0.07f, 0.03f, 0.2f, 18f);
	}

	/// <summary>
	/// 刀命中（敌人身上）。heavy = 重斩：更大的刀光 + 交叉第二刀 + 更多碎屑。
	/// </summary>
	public static void Hit(Control layer, Vector2 at, float facing = 1f, bool heavy = false)
	{
		float rot = -0.55f * facing + Fx.Rand(-0.12f, 0.12f);
		// P5 漫画爆框垫在最后面，一闪就碎掉
		Paint.Pop(layer, heavy ? "burst_02" : "burst_01", at, Fx.Rand(-0.3f, 0.3f), heavy ? 0.62f : 0.46f, null, 0.04f, 0.16f, 1.4f);
		// 主刀光：斜着划过敌人
		float w = heavy ? 0.62f : 0.46f;
		Paint.Sweep(layer, heavy ? "slash_heavy" : "slash_thin", at, rot, new Vector2(w * facing, w * 0.75f), Lut5.Blood,
			0.05f, 0.05f, heavy ? 0.32f : 0.24f, 40f);
		if (heavy)
		{
			Paint.Sweep(layer, "slash_thin", at, rot + 1.25f * facing, new Vector2(0.62f * facing, -0.5f), Lut5.Blood, 0.05f, 0.04f, 0.26f, 30f, 0.07f);
		}
		// 原版命中粒子（换 P5 LUT）：两侧烟、火星、外放的尖刺（原版的菱形命中闪光像几何图形、大斩击核心第一帧会露出整格，都不用）
		Vanilla.Burst(layer, Vanilla.BigSlashSmoke, at, rot, heavy ? 1.2f : 0.85f, Lut5.Smoke);
		Vanilla.Burst(layer, Vanilla.Specks, at, rot, 1f, Lut5.Blood, amount: heavy ? 40 : 22);
		Vanilla.Burst(layer, Vanilla.Streaks, at, 0f, heavy ? 1.3f : 0.95f, Lut5.Blood);
	}
}
