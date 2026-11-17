using Godot;

namespace P5Joker;

/// <summary>
/// 第四版刀组特效（打击动作）：Codex 手绘刀光（Paint：扫出 → 溶解）+ 原版命中粒子换 P5 LUT（Vanilla）+ 着色器白闪 / 空间扭曲。
/// 不画任何几何形状；按 docs/p5/FX_RULES.md 不用卡通爆炸星。打击确认感（顿帧、震屏、冲击帧）在 Impact 里，由伤害结算触发。
/// </summary>
public static class KnifeFx
{
	// slash_thin 贴图（1024×512 打包后）上起笔 / 收尾两个尖的位置（相对贴图中心）：起笔在左上，往下兜出弧底，再往右上收尖
	private static readonly Vector2 ThinTipA = new(40f - 512f, 104f - 256f), ThinTipB = new(1006f - 512f, 327f - 256f);

	/// <summary>
	/// 把一张刀光贴图摆到一段刀路上：贴图的起笔尖对 from、收尾尖对 to（只旋转 + 等比缩放，不翻转，弧底自然落在刀路下方 = 上撩），
	/// 两头各往外延 extend 倍，整体往前（facing 方向）挪 forward 像素。返回 (中心, 旋转, 缩放)。
	/// </summary>
	public static (Vector2 Center, float Rot, Vector2 Scale) Fit(Vector2 from, Vector2 to, Vector2 tipA, Vector2 tipB, float facing,
		float extend = 1.35f, float forward = 40f, float squash = 0.8f)
	{
		Vector2 mid = (from + to) * 0.5f + new Vector2(forward * facing, 0f);
		Vector2 half = (to - from) * 0.5f * extend;
		Vector2 a = mid - half, b = mid + half;
		if (facing < 0f)
		{
			(a, b) = (new Vector2(2f * mid.X - a.X, a.Y), new Vector2(2f * mid.X - b.X, b.Y));
		}
		Vector2 tex = tipB - tipA, scr = (b - a) * new Vector2(facing, 1f);
		float k = scr.Length() / tex.Length();
		float rot = scr.Angle() - tex.Angle();
		// 贴图中心相对两个尖中点的偏移，转到屏幕上
		Vector2 off = (-(tipA + tipB) * 0.5f) * new Vector2(k, k * squash);
		Vector2 center = mid + off.Rotated(rot) * new Vector2(facing, 1f);
		return (center, rot * facing, new Vector2(k * facing, k * squash));
	}

	/// <summary>Joker 出刀那一下的刀光：刀尖从腰前（from）撩到右上方（to），刀光取后半段，扫在身体前方、手的外侧。</summary>
	public static void Swing(Control layer, Vector2 from, Vector2 to, float facing = 1f)
	{
		// 只画刀路的后半段（前半段在身前、贴着身体，画出来像贴在人身上），往敌人方向推一点，收尾顺势甩出去
		(Vector2 c, float rot, Vector2 scale) = Fit(from.Lerp(to, 0.38f), to, ThinTipA, ThinTipB, facing, 1.75f, 85f, 0.75f);
		Paint.Sweep(layer, "slash_thin", c, rot, scale, Lut5.Blood, 0.07f, 0.03f, 0.2f, 14f);
	}

	/// <summary>
	/// 刀命中（敌人身上）。heavy = 重斩：更大的刀光 + 交叉第二刀 + 更多碎屑。
	/// </summary>
	public static void Hit(Control layer, Vector2 at, float facing = 1f, bool heavy = false)
	{
		float rot = -0.55f * facing + Fx.Rand(-0.12f, 0.12f);
		// Vanilla AttackCommand hit sound is already retained; do not layer P5 hit/all-out/sub-bass over it.
		// 命中点一下白闪（着色器径向光）+ 空间扭曲
		Glow.Flash(layer, at, new Color(1f, 0.6f, 0.55f), heavy ? 340f : 240f, 0.16f);
		Glow.Ripple(layer, at, heavy ? 520f : 380f, 0.45f, heavy ? 0.035f : 0.025f);
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
