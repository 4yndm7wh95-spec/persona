using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 小刀流的关键牌（用户 2026-11-04：接下来做剩余其他牌的特效）。小刀流是 Joker 自己的怪盗手法（扯手套施法），不召唤人格面具。
/// 拿到小刀本身已经有反馈：能量球旁的弹巢一发发装弹（AmmoView）。
/// </summary>
public static class ShivFx
{
	public readonly record struct Target(Vector2 At, float Size);

	/// <summary>
	/// 刀扇（之后每一发都打所有敌人）。用户 2026-11-04：小刀在这套 mod 里就是开枪 → 转一下枪就好，不用像扇子、不用夸张。
	/// 选中这张牌 Joker 掏枪瞄准（和小刀牌一样），打出时把枪在指尖转一圈收起（aim_out）：转的时候指尖甩出一圈红色光环和几颗火星，
	/// 枪停下那一刻指尖一闪（用户 11-04：锁定框没必要）。finger = 转枪的指尖（GunGrip），spin = 转一圈要多久。
	/// </summary>
	public static void FanOfKnives(Control layer, Vector2 finger, IReadOnlyList<Target> enemies, float spin = 0.22f)
	{
		Sfx.Play(Sfx.GunDraw, 0.45f);
		Node2D root = Fx.Root(layer, finger, "P5GunTwirl", spin + 0.4);
		ColorRect ring = Glow.RingRect(root, 230f);
		ColorRect outer = Glow.RingRect(root, 300f);
		Glow.SetParam(outer, "tint", new Color(1f, 0.1f, 0.16f, 1f));
		Glow.SetParam(outer, "width", 0.02f);
		Glow.SetParam(ring, "tint", new Color(1f, 0.18f, 0.24f, 1f));
		Glow.SetParam(ring, "width", 0.035f);
		Fx.Animate(root, 0, spin + 0.25f, t =>
		{
			float s = t * (spin + 0.25f);
			float grow = Ease5.OutQuint(Mathf.Min(1f, s / spin));
			Glow.SetParam(ring, "radius", Mathf.Lerp(0.2f, 0.62f, grow));
			float a = s < spin ? 0.9f : 0.9f * (1f - (s - spin) / 0.25f);
			Glow.SetParam(ring, "alpha", a);
			Glow.SetParam(outer, "radius", Mathf.Lerp(0.3f, 0.7f, grow));
			Glow.SetParam(outer, "alpha", a * 0.5f);
			root.Rotation = s * 30f;
		});
		Vanilla.Burst(layer, Vanilla.Specks, finger, -Mathf.Pi / 2f, 0.6f, Lut5.Blood, amount: 10);
		Fx.After(layer, spin, () =>
		{
			Glow.Flash(layer, finger, new Color(1f, 0.4f, 0.42f), 160f, 0.2f);      // 用户 2026-11-04：不要锁定框
		});
	}

	// ───────────────────────── 子弹时间 ─────────────────────────

	private static Shader? _vignette;

	private static Shader Vignette => _vignette ??= new Shader { Code = @"
shader_type canvas_item;
uniform float amount = 0.0;
void fragment() {
	vec2 p = UV - 0.5;
	float v = smoothstep(0.25, 0.75, length(p * vec2(1.3, 1.0)));
	COLOR = vec4(0.03, 0.0, 0.02, v * amount);
}" };

	/// <summary>
	/// 子弹时间（本回合手牌全部免费、不能再抽）：转枪（Aim.PlayTwirl）的同时从 Joker 胸口荡开一圈很慢的空间波纹，
	/// 画面四周压暗一下再恢复——像时间被放慢。chest = Joker 胸口。
	/// </summary>
	public static void BulletTime(Control layer, Vector2 chest)
	{
		Sfx.Play(Sfx.GunDraw, 0.45f);
		Sfx.Play(Sfx.SubBoom, 0.3f);
		Glow.Ripple(layer, chest, 1400f, 1.1f, 0.03f);
		Vector2 screen = Fx.Screen(layer);
		ShaderMaterial m = new() { Shader = Vignette };
		ColorRect r = new() { Material = m, Size = screen, MouseFilter = Control.MouseFilterEnum.Ignore, Name = "P5BulletTime" };
		layer.AddChild(r);
		r.GlobalPosition = Vector2.Zero;
		const float life = 0.9f;
		Fx.Animate(r, 0, life, t => m.SetShaderParameter("amount", 0.55f * Mathf.Sin(Mathf.Pi * Mathf.Pow(t, 0.6f))));
		Fx.FreeAfter(r, life + 0.05);
	}

	// ───────────────────────── 残影 ─────────────────────────

	/// <summary>
	/// 残影（每打一张牌获得格挡）：Joker 身上分出两道红色残影，往身后滑开、淡掉。
	/// clone = 复制一份 Joker（同一帧的姿势），返回复制品和本体的全局变换（Aim.CloneBody）。
	/// </summary>
	public static void Afterimage(Control back, System.Func<Node, (Node2D Clone, Transform2D Body)?> clone, float facing)
	{
		Sfx.Play(Sfx.SwishLow, 0.45f);
		for (int i = 0; i < 2; i++)
		{
			int k = i;
			Fx.After(back, 0.07f * k, () =>
			{
				if (clone(back) is not { } c)
				{
					return;
				}
				c.Clone.GlobalTransform = c.Body;
				Vector2 from = c.Clone.Position;
				c.Clone.Modulate = new Color(1f, 0.25f, 0.3f, 0.6f);
				float dist = (90f + 70f * k) * -facing;
				Fx.Animate(c.Clone, 0, 0.5, t =>
				{
					float e = Ease5.OutQuint(t);
					c.Clone.Position = from + new Vector2(dist * e, 0f);
					c.Clone.Modulate = new Color(1f, 0.25f, 0.3f, 0.6f * (1f - t * t));
				});
				Fx.FreeAfter(c.Clone, 0.55);
			});
		}
	}

	// ───────────────────────── 精心策划 ─────────────────────────

	/// <summary>
	/// 精心策划（回合结束保留手牌）：Joker 指间弹出一张预告信，往上翻着转两圈，再飞进下方的手牌区淡掉。hand = 扯手套的手。
	/// </summary>
	public static void CallingCard(Control layer, Vector2 hand)
	{
		if (Fx.Tex("calling_card") is not Texture2D tex)
		{
			return;
		}
		Sfx.Play(Sfx.Swish, 0.4f);
		Vector2 screen = Fx.Screen(layer);
		Vector2 peak = hand + new Vector2(60f, -220f), dest = new(screen.X * 0.5f, screen.Y - 60f);
		float k = 150f / tex.GetHeight();
		Node2D root = Fx.Root(layer, hand, "P5CallingCard", 1.0);
		Sprite2D card = new() { Texture = tex, Scale = Vector2.One * k };
		root.AddChild(card);
		Sprite2D glow = Glow.SoftSprite(root, Vector2.Zero, new Color(1f, 0.2f, 0.25f, 0.5f), 5f);
		root.MoveChild(glow, 0);
		const float up = 0.32f, down = 0.36f;
		Fx.Animate(root, 0, up + down, t =>
		{
			float s = t * (up + down);
			if (s < up)
			{
				float u = Ease5.OutQuint(s / up);
				root.GlobalPosition = hand.Lerp(peak, u);
				card.Rotation = u * Mathf.Tau * 2f;
				card.Scale = new Vector2(k * Mathf.Cos(u * Mathf.Tau * 1.5f), k);      // 翻转：宽度来回变
			}
			else
			{
				float u = (s - up) / down;
				float e = u * u;
				root.GlobalPosition = peak.Lerp(dest, e);
				card.Rotation = Mathf.Tau * 2f + e * 0.6f;
				card.Scale = Vector2.One * k * (1f - 0.5f * e);
				root.Modulate = new Color(1f, 1f, 1f, 1f - Mathf.Max(0f, u - 0.7f) / 0.3f);
			}
		});
		Fx.After(layer, up + down - 0.05f, () => Sfx.Play(Sfx.Select, 0.5f));
	}

	// ───────────────────────── 隐形 ─────────────────────────

	/// <summary>隐形（本回合格挡翻倍）：脚下腾起一圈暗烟，Joker 整个人暗下去一下再恢复，像隐进影子里。</summary>
	public static void Shadowmeld(Control back, Vector2 feet, float height, Node2D? body)
	{
		Sfx.Play(Sfx.SwishLow, 0.5f);
		float k = height / 600f;
		Vanilla.Burst(back, Vanilla.BigSlashSmoke, feet + new Vector2(0f, -20f), -Mathf.Pi / 2f, 1.1f * k, Lut5.Smoke);
		Vanilla.Burst(back, Vanilla.RoundSmoke, feet + new Vector2(-60f * k, -10f), 0f, 0.9f * k, Lut5.Smoke, delay: 0.05f);
		Vanilla.Burst(back, Vanilla.RoundSmoke, feet + new Vector2(60f * k, -10f), 0f, 0.9f * k, Lut5.Smoke, delay: 0.08f);
		BodyTint.Pulse(body, new Color(0.3f, 0.26f, 0.36f), 0.35, 0.45);
	}
}
