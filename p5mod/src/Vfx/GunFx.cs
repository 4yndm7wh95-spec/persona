using Godot;

namespace P5Joker;

/// <summary>
/// 第四版枪组（小刀牌 = 开枪）。小刀牌打得最多（用户 2026-11-02：常打的牌不用那么花，但质量不能降）：
/// 所以短、干净，但每样都是手绘材质 + 着色器，不画几何形状：
/// · 开火：Codex 手绘的侧面枪口火焰（热芯对准枪口、沿射击方向喷出，LUT 换成 P5 红白），2～3 帧闪完从边缘烧掉；枪口一下暖白光；一枚黄铜弹壳往后上方抛出、旋转、落下；
/// · 弹道：突刺刀光贴图拉成一道细长的光（宽的一头在枪口），0.04s 扫到目标，随即溶解；
/// · 命中：一小团手绘火花（LUT 红白）+ 原版火星（沿弹道方向溅）+ 小的外放尖刺 + 很轻的空间扭曲。打击确认感（顿帧、震屏）在 Impact。
/// 刀扇的子弹弹射（HitFx.Ricochet）保留 0.7.27 的（用户：只有它保留）。
/// </summary>
public static class GunFx
{
	// muzzle_side_0N 贴图（打包后 512×256）上热芯的横向位置（占宽度的比例）
	private static readonly float[] CoreX = { 320f / 1024f, 286f / 1024f, 266f / 1024f };
	private const float SideW = 512f;

	/// <summary>开火那一帧（动画 0.18s）：枪口火焰 + 暖白光 + 抛弹壳 + 枪声。dir = 射击方向（没有目标时用朝向）。</summary>
	public static void Muzzle(Control layer, Vector2 muzzle, Vector2 dir, float facing = 1f)
	{
		Sfx.Play(Sfx.Gun, 0.55f);   // 用户 2026-11-13：演示的枪声像抽鞭子 → 换回 2026-11-03 试听时选的 D（gun_10，干脆的手枪声）；一直用这一个
		dir = dir.LengthSquared() > 0.001f ? dir.Normalized() : new Vector2(facing, 0f);
		float rot = dir.Angle();
		int i = Fx.Rng.Next(CoreX.Length);
		const float sc = 0.42f;
		Vector2 center = muzzle + dir * (0.5f - CoreX[i]) * SideW * sc;
		Paint.Pop(layer, $"muzzle_side_0{i + 1}", center, rot, sc, Lut5.Muzzle, 0.02f, 0.08f, 0.65f, 0f, 1.25f);
		Glow.Flash(layer, muzzle + dir * 12f, new Color(1f, 0.75f, 0.6f), 150f, 0.08f);
		Shell(layer, muzzle - dir * 30f + new Vector2(0f, -6f), facing);
	}

	/// <summary>弹道 + 命中（伤害结算那一刻）。</summary>
	public static void Shot(Control layer, Vector2 muzzle, Vector2 hit, bool final = false)
	{
		Vector2 d = hit - muzzle;
		float len = d.Length();
		if (len > 1f)
		{
			// slash_thrust（打包后 1024×512）：宽的起爆在左、细线往右；贴图里从 x≈60 到 x≈1000 是那道光
			float sx = len / 940f;
			Vector2 center = muzzle + d * 0.5f + d.Normalized() * (512f - 530f) * sx;
			Paint.Sweep(layer, "slash_thrust", center, d.Angle(), new Vector2(sx, 0.13f), Lut5.Blood, 0.04f, 0.01f, 0.12f, 0f, 0f, 1.3f);
		}
		Hit(layer, hit, len > 1f ? d / len : Vector2.Right, final);
	}

	public static void Hit(Control layer, Vector2 at, Vector2 dir, bool final = false)
	{
		// 命中声照 Five Cards 演示（用户 2026-11-09）：升调压低的命中；打死目标的那一发用收尾的重命中（加低频重击）
		Sfx.Play(final ? Sfx.GunHitFinal : Sfx.GunHit, Sfx.DemoMixVolume);
		Paint.Pop(layer, "muzzle_front", at, Fx.Rand(-3.1f, 3.1f), 0.2f, Lut5.Muzzle, 0.01f, 0.1f, 0.6f, 0f, 1.2f);
		Vanilla.Burst(layer, Vanilla.Specks, at, dir.Angle(), 0.8f, Lut5.Blood, amount: 14);
		Vanilla.Burst(layer, Vanilla.Streaks, at, 0f, 0.55f, Lut5.Blood);
		Glow.Ripple(layer, at, 220f, 0.3f, 0.012f);
	}

	/// <summary>一枚黄铜弹壳：往后上方抛出、旋转、受重力落下，最后 0.15s 淡掉。</summary>
	private static void Shell(Control layer, Vector2 at, float facing)
	{
		if (Fx.Tex("shell") is not Texture2D tex)
		{
			return;
		}
		Sprite2D s = new() { Texture = tex, Scale = Vector2.One * 0.32f, Name = "P5Shell" };
		layer.AddChild(s);
		s.GlobalPosition = at;
		Vector2 v = new(-facing * Fx.Rand(150f, 220f), -Fx.Rand(280f, 360f));
		float spin = facing * Fx.Rand(14f, 22f);
		const float life = 0.6f;
		Fx.Animate(s, 0, life, t =>
		{
			float time = t * life;
			s.GlobalPosition = at + v * time + new Vector2(0f, 1400f * time * time * 0.5f);
			s.Rotation = spin * time;
			s.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp((life - time) / 0.15f, 0f, 1f));
		});
		Fx.FreeAfter(s, life + 0.05);
	}
}
