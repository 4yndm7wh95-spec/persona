using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 刀刃陷阱（把消耗牌堆里的小刀全部对一个敌人打出）（用户 2026-11-13：原来太简单；第二次：只有五六颗光弹也太简单，要更多创意）：
/// ① 布下（0～0.4s）：枪口一闪，一颗颗光弹从枪口划着弧飞出去，落到敌人四周；
/// ② 成笼：光弹绕着敌人立体地转（有高有低，转到前面在敌人身前、后面在身后），几道细刀光绕着它转成一个笼子，脚下一团红光晕一呼一吸；
/// ③ 收网：每一发小刀打中（GunFx.Hit），一颗光弹扎进敌人（柔光尾、闪光、一朵小网点光晕）；
/// ④ 收官：最后一颗被收掉时整个笼子往里一收 —— 大网点光晕、十字光芒、碎光、轻震；
/// ⑤ 3.6s 后没用完的淡掉。全部圆的、柔光的，没有尖角、没有细线圈。
/// </summary>
public static class KnifeTrapFx
{
	private sealed class Orb
	{
		public Node2D F = null!, B = null!;
		public Sprite2D FG = null!, FC = null!, BG = null!, BC = null!;
		public float Ang, Hgt, Phase, Born;
		public bool Used;
		public Vector2 Pos;
	}

	private static readonly List<Orb> _orbs = new();
	private static Vector2 _center;
	private static float _h, _w;
	private static Control? _front, _back;
	private static ulong _until;
	private static int _left;

	public static void Begin(Control front, Control back, Vector2 muzzle, Vector2 center, float th, float tw, int count, Node2D? body)
	{
		Clear();
		float u = Fx.Screen(front).Y / 1080f;
		th = Mathf.Max(th, 120f);
		tw = Mathf.Max(tw, th * 0.6f);
		_front = front;
		_back = back;
		_center = center;
		_h = th;
		_w = tw;
		const float life = 3.6f, fly = 0.22f;
		_until = Stage.NowMs() + (ulong)(life * 1000f);
		count = Mathf.Clamp(count, 4, 12);
		_left = count;
		float rx = Mathf.Max(tw * 0.75f, th * 0.55f), ry = rx * 0.32f;
		Vector2 feet = center + new Vector2(0f, th * 0.5f);
		Sfx.Take("gx_whoosh", 0.16f * 0.8f, 0f);
		Sfx.Take("gx_ping", 0.14f * 0.8f, 0.3f);
		// 枪口一闪
		Glow.Flash(front, muzzle, new Color(1f, 0.9f, 0.88f), th * 0.35f, 0.14f);
		Glow.Flash(back, muzzle, new Color(0.9f, 0.1f, 0.15f), th * 0.8f, 0.25f);
		// 布下那一下：敌人身上一暗、身后一闪、脚下漾开网点光晕
		Fx.After(front, fly, () =>
		{
			Glow.Flash(back, center, new Color(0.9f, 0.1f, 0.15f), Mathf.Max(tw, th) * 1.6f, 0.3f);
			SkillRedo5.SoftBurst(back, feet, Mathf.Max(tw, th) * 1.4f, 0.45f, new Color(0.85f, 0.05f, 0.12f));
			BodyTint.Pulse(body, new Color(0.65f, 0.4f, 0.45f), 0.08, 0.4);
		});
		// 脚下一团红光晕，一呼一吸
		Node2D floor = Fx.Root(back, feet, "P5TrapFloor", life + 0.1);
		Sprite2D fs = Glow.SoftSprite(floor, Vector2.Zero, new Color(1f, 0.1f, 0.18f, 0f), 1f);
		fs.Scale = new Vector2(rx * 2.4f, rx * 0.55f) / 32f;
		Fx.Animate(floor, 0, life, t =>
		{
			float s = t * life;
			float a = Fx.OutCubic(Ease5.Seg(s, fly, 0.25f)) * (1f - Ease5.Seg(s, life - 0.4f, 0.4f));
			fs.Modulate = new Color(1f, 0.1f, 0.18f, (0.4f + 0.15f * Mathf.Sin(s * 4f)) * a);
		});
		// 笼子：几道细刀光绕着它转
		SkillRedo5.TrapCage(front, back, feet, th, rx * 1.05f, fly, life - fly);
		// 光弹：从枪口飞出去，落到环上，然后绕着转
		for (int i = 0; i < count; i++)
		{
			Orb o = new()
			{
				Ang = Mathf.Tau * i / count + Fx.Rand(-0.12f, 0.12f), Hgt = Fx.Rand(-0.35f, 0.3f) * th, Phase = Fx.Rand(0f, Mathf.Tau),
				Born = 0.02f * i, Pos = muzzle,
			};
			o.F = Fx.Root(front, muzzle, "P5TrapOrbF", life + 0.2);
			o.B = Fx.Root(back, muzzle, "P5TrapOrbB", life + 0.2);
			o.FG = Glow.SoftSprite(o.F, Vector2.Zero, new Color(1f, 0.12f, 0.2f, 0f), th * 0.6f / 32f);
			o.FC = Glow.SoftSprite(o.F, Vector2.Zero, new Color(1f, 0.92f, 0.9f, 0f), th * 0.2f / 32f);
			o.BG = Glow.SoftSprite(o.B, Vector2.Zero, new Color(1f, 0.12f, 0.2f, 0f), th * 0.6f / 32f);
			o.BC = Glow.SoftSprite(o.B, Vector2.Zero, new Color(1f, 0.92f, 0.9f, 0f), th * 0.2f / 32f);
			o.F.Visible = o.B.Visible = false;
			_orbs.Add(o);
		}
		Node2D driver = Fx.Root(front, center, "P5TrapDriver", life + 0.1);
		var orbs = new List<Orb>(_orbs);
		Fx.Animate(driver, 0, life, t =>
		{
			float s = t * life;
			foreach (Orb o in orbs)
			{
				if (o.Used || !GodotObject.IsInstanceValid(o.F)) continue;
				float ang = o.Ang + s * 1.6f;
				Vector2 slot = center + new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry + o.Hgt + Mathf.Sin(s * 3f + o.Phase) * th * 0.03f);
				float k = Ease5.Seg(s, o.Born, fly);
				Vector2 mid = (muzzle + slot) * 0.5f + new Vector2(0f, -th * 0.5f);
				float e = Fx.OutCubic(k);
				o.Pos = k < 1f ? muzzle.Lerp(mid, e).Lerp(mid.Lerp(slot, e), e) : slot;
				bool fr = k < 1f || Mathf.Sin(ang) > 0f;
				float a = (s < o.Born ? 0f : 1f) * (1f - Ease5.Seg(s, life - 0.4f, 0.4f));
				float tw2 = 0.8f + 0.2f * Mathf.Sin(s * 9f + o.Phase);
				float depth = k < 1f ? 1f : 0.75f + 0.25f * Mathf.Sin(ang);   // 后面的暗一点、小一点
				float pop = 0.4f + 0.6f * Ease5.OutBack(Mathf.Min(1f, Mathf.Max(0f, s - o.Born) / 0.15f));
				foreach ((Node2D n, Sprite2D g, Sprite2D cc, bool on) in new[] { (o.F, o.FG, o.FC, fr), (o.B, o.BG, o.BC, !fr) })
				{
					n.Visible = on && a > 0f;
					n.GlobalPosition = o.Pos;
					n.Scale = Vector2.One * pop * depth;
					g.Modulate = new Color(1f, 0.12f, 0.2f, 0.9f * a * tw2 * depth);
					cc.Modulate = new Color(1f, 0.92f, 0.9f, 0.95f * a);
				}
			}
		});
		// 飞出去的那段拖一串柔光
		foreach (Orb o in orbs)
		{
			for (int k = 1; k <= 4; k++)
			{
				float d = o.Born + fly * k / 5f;
				Orb oo = o;
				Fx.After(front, d, () => Glow.Sparks(front, oo.Pos, 3, 10f * u, 40f * u, 0.3f,
					new[] { new Color(1f, 0.9f, 0.88f), new Color(0.9f, 0.12f, 0.18f) }, 0f, 0.02f * u, 0.04f * u, Vector2.Up, 180f));
			}
		}
	}

	/// <summary>一发小刀打中（GunFx.Hit）：陷阱还在、打的是这个敌人 → 收一颗光弹扎进去；最后一颗收完整个笼子往里一收。</summary>
	public static void OnHit(Vector2 at)
	{
		if (Stage.NowMs() > _until || _front == null || !GodotObject.IsInstanceValid(_front)) return;
		if (at.DistanceTo(_center) > _h * 1.2f) return;
		Orb? pick = null;
		foreach (Orb o in _orbs)
			if (!o.Used && GodotObject.IsInstanceValid(o.F)) { pick = o; break; }
		if (pick == null) return;
		pick.Used = true;
		_left--;
		Vector2 from = pick.Pos;
		Vector2 to = _center + (from - _center) * 0.1f;
		Control f = _front;
		Control b = _back != null && GodotObject.IsInstanceValid(_back) ? _back : f;
		float h = _h, w = _w;
		Vector2 c = _center;
		Orb po = pick;
		SkillRedo5.Streak(f, t => from.Lerp(to, t), 0f, 0.09f, h * 0.5f, h * 0.08f, new Color(0.9f, 0.08f, 0.15f), 1.3f);
		Fx.Animate(po.F, 0, 0.09, t =>
		{
			if (!GodotObject.IsInstanceValid(po.F)) return;
			po.B.Visible = false;
			po.F.Visible = true;
			po.F.GlobalPosition = from.Lerp(to, t * t);
			po.FG.Modulate = new Color(1f, 0.12f, 0.2f, 0.9f * (1f - t));
			po.FC.Modulate = new Color(1f, 0.92f, 0.9f, 1f - t);
		});
		bool last = _left <= 0;
		Fx.After(f, 0.09, () =>
		{
			Glow.Flash(f, to, new Color(1f, 0.9f, 0.88f), h * 0.4f, 0.12f);
			Glow.Flash(b, to, new Color(0.9f, 0.1f, 0.15f), h * 0.9f, 0.22f);
			SkillRedo5.SoftBurst(b, to, h * 0.7f, 0.3f, new Color(0.85f, 0.05f, 0.12f));
			if (!last) return;
			// 收官：整个笼子往里一收
			Fx.Shake(2);
			Glow.Flash(f, c, new Color(1f, 0.92f, 0.9f), w * 0.9f, 0.16f);
			Glow.Flash(b, c, new Color(0.9f, 0.08f, 0.14f), Mathf.Max(w, h) * 2.2f, 0.36f);
			SkillRedo5.SoftBurst(b, c, Mathf.Max(w, h) * 2.2f, 0.5f, new Color(0.85f, 0.05f, 0.12f));
			SkillRedo5.Cross(f, c, h * 1.2f);
			float u = Fx.Screen(f).Y / 1080f;
			Glow.Sparks(f, c, 30, 160f * u, 520f * u, 0.5f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 200f * u,
				0.02f * u, 0.05f * u, Vector2.Up, 180f);
			_until = 0;
		});
	}

	private static void Clear()
	{
		foreach (Orb o in _orbs)
		{
			if (GodotObject.IsInstanceValid(o.F)) o.F.QueueFree();
			if (GodotObject.IsInstanceValid(o.B)) o.B.QueueFree();
		}
		_orbs.Clear();
	}
}
