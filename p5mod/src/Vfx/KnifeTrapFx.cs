using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 刀刃陷阱（把消耗牌堆里的小刀全部对一个敌人打出）（用户 2026-11-13：原来太简单）：
/// · 布阵：敌人四周的空中一颗颗亮起红色光弹（白芯红晕的柔光球，轻轻浮动），围成一圈，转到前面的画在敌人身前、后面的在身后；
///   数量 = 要打出的小刀数（4～12）；敌人脚下漾开一圈网点光晕、身上暗一下（被困住）。
/// · 收网：之后每一发小刀打中（GunFx.Hit），就有一颗光弹拖着柔光尾扎进敌人、打中处一闪 —— 一颗颗被收掉，跟连射同一个节奏。
/// · 3 秒后没用完的淡掉。全部圆的、柔光的，没有尖角。
/// </summary>
public static class KnifeTrapFx
{
	private sealed class Orb
	{
		public Node2D Root = null!;
		public Sprite2D Glow = null!, Core = null!;
		public float Ang, Phase;
		public bool Used;
	}

	private static readonly List<Orb> _orbs = new();
	private static Vector2 _center;
	private static float _h;
	private static Control? _front, _back;
	private static ulong _until;

	public static void Begin(Control front, Control back, Vector2 center, float th, float tw, int count, Node2D? body)
	{
		Clear();
		float u = Fx.Screen(front).Y / 1080f;
		th = Mathf.Max(th, 120f);
		tw = Mathf.Max(tw, th * 0.6f);
		_front = front;
		_back = back;
		_center = center;
		_h = th;
		_until = Stage.NowMs() + 3000;
		count = Mathf.Clamp(count, 4, 12);
		float rx = Mathf.Max(tw * 0.85f, th * 0.6f), ry = th * 0.62f;
		const float life = 3.2f;
		Sfx.Take("gx_ping", 0.14f * 0.8f, 0.05f);
		Glow.Flash(back, center, new Color(0.9f, 0.1f, 0.15f), Mathf.Max(tw, th) * 1.6f, 0.3f);
		SkillRedo5.SoftBurst(back, center + new Vector2(0f, th * 0.35f), Mathf.Max(tw, th) * 1.3f, 0.4f, new Color(0.85f, 0.05f, 0.12f));
		BodyTint.Pulse(body, new Color(0.65f, 0.4f, 0.45f), 0.08, 0.4);
		for (int i = 0; i < count; i++)
		{
			float ang = Mathf.Tau * i / count + Fx.Rand(-0.15f, 0.15f) - Mathf.Pi / 2f;
			bool front2 = Mathf.Sin(ang) > -0.2f;
			Orb o = new() { Ang = ang, Phase = Fx.Rand(0f, Mathf.Tau) };
			_orbs.Add(o);
			o.Root = Fx.Root(front2 ? front : back, center, "P5TrapOrb", life);
			o.Glow = Glow.SoftSprite(o.Root, Vector2.Zero, new Color(1f, 0.12f, 0.2f, 0f), th * 0.48f / 32f);
			o.Core = Glow.SoftSprite(o.Root, Vector2.Zero, new Color(1f, 0.92f, 0.9f, 0f), th * 0.17f / 32f);
			o.Root.Visible = false;
			float d0 = 0.03f * i;
			Vector2 home = center + new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry);
			Orb oo = o;
			Fx.Animate(o.Root, d0, life - d0, t =>
			{
				if (oo.Used) return;
				oo.Root.Visible = true;
				float s = t * (life - d0);
				float pop = Ease5.OutBack(Mathf.Min(1f, s / 0.18f));
				float bob = Mathf.Sin(s * 3.2f + oo.Phase) * th * 0.025f;
				oo.Root.GlobalPosition = home + new Vector2(0f, bob);
				oo.Root.Scale = Vector2.One * pop;
				float a = Mathf.Min(1f, s / 0.1f) * (1f - Ease5.Seg(s, life - d0 - 0.4f, 0.4f));
				float tw2 = 0.8f + 0.2f * Mathf.Sin(s * 9f + oo.Phase);
				oo.Glow.Modulate = new Color(1f, 0.12f, 0.2f, 0.95f * a * tw2);
				oo.Core.Modulate = new Color(1f, 0.92f, 0.9f, 0.95f * a);
			});
			Fx.After(front, d0, () => Glow.Sparks(front2 ? front : back, home, 5, 20f * u, 80f * u, 0.3f,
				new[] { new Color(1f, 0.9f, 0.88f), new Color(0.9f, 0.12f, 0.18f) }, 0f, 0.012f * u, 0.028f * u, Vector2.Up, 180f));
		}
	}

	/// <summary>一发小刀打中（GunFx.Hit）：陷阱还在、打的是这个敌人 → 收一颗光弹扎进去。</summary>
	public static void OnHit(Vector2 at)
	{
		if (Stage.NowMs() > _until || _front == null || !GodotObject.IsInstanceValid(_front)) return;
		if (at.DistanceTo(_center) > _h * 1.2f) return;
		Orb? pick = null;
		foreach (Orb o in _orbs)
			if (!o.Used && GodotObject.IsInstanceValid(o.Root) && o.Root.Visible) { pick = o; break; }
		if (pick == null) return;
		pick.Used = true;
		Vector2 from = pick.Root.GlobalPosition;
		Vector2 to = _center + (from - _center) * 0.12f;
		Node2D root = pick.Root;
		Sprite2D g = pick.Glow, c = pick.Core;
		SkillRedo5.Streak(_front, t => from.Lerp(to, t), 0f, 0.09f, _h * 0.45f, _h * 0.07f, new Color(0.9f, 0.08f, 0.15f), 1.3f);
		Fx.Animate(root, 0, 0.09, t =>
		{
			if (!GodotObject.IsInstanceValid(root)) return;
			root.GlobalPosition = from.Lerp(to, t * t);
			g.Modulate = new Color(1f, 0.12f, 0.2f, 0.75f * (1f - t));
			c.Modulate = new Color(1f, 0.92f, 0.9f, 1f - t);
		});
		Control f = _front;
		Fx.After(f, 0.09, () =>
		{
			Glow.Flash(f, to, new Color(1f, 0.9f, 0.88f), _h * 0.4f, 0.12f);
			if (_back != null && GodotObject.IsInstanceValid(_back)) Glow.Flash(_back, to, new Color(0.9f, 0.1f, 0.15f), _h * 0.9f, 0.22f);
		});
	}

	private static void Clear()
	{
		foreach (Orb o in _orbs)
			if (GodotObject.IsInstanceValid(o.Root)) o.Root.QueueFree();
		_orbs.Clear();
	}

}
