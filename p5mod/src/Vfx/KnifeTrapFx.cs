using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 刀刃陷阱（把消耗牌堆里的小刀全部对一个敌人打出）「预告信星环」
/// （用户 2026-11-13：原来太简单 → 光弹太少 → 光点看不清、太小胆；2026-11-14：光点不该从 Joker 那里出来，应该在敌人附近现身、
///   有几张小刀就有几个；v4 卡冲过去撞敌人怪怪的；要再华丽、再花哨）：
/// ① 布阵：有几张小刀就有几张预告信卡，一张接一张在敌人四周现身（红白一闪、网点光晕、碎光，卡翻转着弹出来，很轻的翻牌声），
///    排成绕着它腰上转的一圈斜光环 —— 卡拖着红白丝带绕它转，转到身前在它前面、转到身后在它后面；
///    阵成时一道大弧光绕它扫一圈、光粒往上飘；敌人周围压暗、脚下红光呼吸。
/// ② 每一枪（GunFx.Hit）：下一张卡停下来正对它、一亮，射出一道带弯的红白光矛扎进它；
///    卡在原地被切开烧掉、炸成纸屑；它身上闪光、网点光晕、碎光、一道弧光绕身一扫、轻震。剩下的卡越转越快。
/// ③ 收官（最后一张）：整圈炸开 —— 刀光旋涡往上卷、两道交叉大弧光、十字光芒、超大网点光晕、漫天纸屑、重震。
/// </summary>
public static class KnifeTrapFx
{
	private sealed class Trap
	{
		public Phantom.P5Card Card = null!;
		public Phantom.Ribbon RibF = null!, RibB = null!;
		public float Slot, Phase, Born;
		public bool Used, Front;
	}

	private static readonly List<Trap> _traps = new();
	private static Vector2 _center;
	private static float _h, _w, _cw, _rx, _ry;
	private static float _spin, _rot, _lastS;
	private static Control? _front, _back;
	private static ulong _until;
	private static int _left, _total;

	public const float Interval = 0.07f;

	private static Vector2 OrbitAt(float ang) => _center + new Vector2(Mathf.Cos(ang) * _rx, Mathf.Sin(ang) * _ry - _h * 0.05f);

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
		count = Mathf.Clamp(count, 1, 14);
		_left = count;
		_total = count;
		float life = 1.0f + Interval * count + 3.5f;
		_until = Stage.NowMs() + (ulong)(life * 1000f);
		_rx = Mathf.Max(tw * 0.95f, th * 0.78f);
		_ry = th * 0.26f;
		float cw = th * 0.27f;
		_cw = cw;
		_spin = 0.9f;
		_rot = 0f;
		_lastS = 0f;
		Vector2 feet = center + new Vector2(0f, th * 0.5f);
		// 敌人周围一大圈压暗透红、脚下红光呼吸
		SkillRedo5.Dim(back, center, new Vector2(_rx * 3.6f, th * 2.8f), life, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.3f)) * (1f - Ease5.Seg(s, life - 0.6f, 0.6f)));
		Node2D floor = Fx.Root(back, feet, "P5TrapFloor", life + 0.1);
		Sprite2D fs = Glow.SoftSprite(floor, Vector2.Zero, new Color(1f, 0.1f, 0.18f, 0f), 1f);
		fs.Scale = new Vector2(_rx * 2.6f, _rx * 0.5f) / 32f;
		Fx.Animate(floor, 0, life, t =>
		{
			float s = t * life;
			float a = Fx.OutCubic(Ease5.Seg(s, 0f, 0.3f)) * (1f - Ease5.Seg(s, life - 0.5f, 0.5f));
			fs.Modulate = new Color(1f, 0.1f, 0.18f, (0.5f + 0.18f * Mathf.Sin(s * 4f)) * a);
		});
		// 环本身的一层柔光（斜椭圆，身后）
		Node2D halo = Fx.Root(back, OrbitAt(0f) - new Vector2(_rx, 0f), "P5TrapHalo", life + 0.1);
		halo.GlobalPosition = center + new Vector2(0f, -th * 0.05f);
		Sprite2D hs = Glow.SoftSprite(halo, Vector2.Zero, new Color(0.95f, 0.08f, 0.16f, 0f), 1f);
		hs.Scale = new Vector2(_rx * 2.5f, _ry * 2.6f) / 32f;
		Fx.Animate(halo, 0, life, t =>
		{
			float s = t * life;
			float a = Fx.OutCubic(Ease5.Seg(s, 0.1f, 0.5f)) * (1f - Ease5.Seg(s, life - 0.5f, 0.5f));
			hs.Modulate = new Color(0.95f, 0.08f, 0.16f, (0.22f + 0.08f * Mathf.Sin(s * 5f)) * a);
		});
		Glow.Flash(back, center, new Color(0.9f, 0.1f, 0.15f), _rx * 2.4f, 0.3f);
		BodyTint.Pulse(body, new Color(0.62f, 0.38f, 0.42f), 0.1, 0.5);

		// 一张张现身：从正上方开始顺着环排开
		for (int i = 0; i < count; i++)
		{
			float slot = -Mathf.Pi / 2f + Mathf.Tau * i / count;
			Trap tp = new() { Slot = slot, Phase = Fx.Rand(0f, Mathf.Tau), Born = 0.05f + Interval * i };
			tp.Card = Phantom.Card(back, OrbitAt(slot), cw, life + 0.2);
			tp.Card.Root.Visible = false;
			tp.RibF = new Phantom.Ribbon(front, cw * 0.55f, life + 0.2, 12);
			tp.RibB = new Phantom.Ribbon(back, cw * 0.55f, life + 0.2, 12);
			_traps.Add(tp);
			Trap t0 = tp;
			Fx.After(front, tp.Born, () =>
			{
				if (!GodotObject.IsInstanceValid(t0.Card.Root)) return;
				Vector2 at = t0.Card.Root.GlobalPosition;
				Control layer = t0.Front ? front : back;
				Sfx.Take("gx_card_slide", 0.08f);
				Glow.Flash(layer, at, new Color(1f, 0.92f, 0.9f), cw * 1.8f, 0.12f);
				Glow.Flash(back, at, new Color(0.9f, 0.1f, 0.15f), cw * 3.6f, 0.26f);
				SkillRedo5.SoftBurst(back, at, cw * 3.4f, 0.35f, new Color(0.85f, 0.05f, 0.12f));
				Glow.Sparks(layer, at, 14, 80f * u, 320f * u, 0.45f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 60f * u,
					0.015f * u, 0.04f * u, Vector2.Up, 180f);
			});
		}
		// 阵成：一道大弧光沿着环扫一圈、光粒往上飘、叮
		float done = 0.05f + Interval * count;
		Fx.After(front, done, () => Sfx.Take("gx_ping", 0.13f));
		SkillRedo5.RingSweep(front, back, center + new Vector2(0f, -th * 0.05f), _rx * 1.05f, 0f, 1f, done, 0.6f, 0.07f, new Color(0.95f, 0.08f, 0.16f), 1.2f, 1.1f);
		SkillRedo5.RingSweep(front, back, center + new Vector2(0f, -th * 0.05f), _rx * 0.9f, 0.12f, 1f, done + 0.06f, 0.5f, 0.03f, new Color(1f, 0.85f, 0.85f), 1.5f, 1.0f);
		SkillRedo5.Motes(front, feet, th, u, 0.2f, life - 1.2f, 10, 0.55f);

		var traps = new List<Trap>(_traps);
		Node2D driver = Fx.Root(front, center, "P5TrapDriver", life + 0.1);
		Fx.Animate(driver, 0, life, t =>
		{
			float s = t * life;
			float dt = s - _lastS;
			_lastS = s;
			_spin = Mathf.Lerp(_spin, 0.9f + 3.2f * (1f - (float)_left / _total), Mathf.Min(1f, dt * 4f));
			_rot += _spin * dt;
			foreach (Trap tp in traps)
			{
				if (tp.Used || !GodotObject.IsInstanceValid(tp.Card.Root)) continue;
				float ang = tp.Slot + _rot;
				Vector2 pos = OrbitAt(ang) + new Vector2(0f, Mathf.Sin(s * 2.6f + tp.Phase) * _h * 0.025f);
				bool fr = Mathf.Sin(ang) > 0f;
				if (fr != tp.Front || tp.Card.Root.GetParent() == null)
				{
					tp.Front = fr;
					Control want = fr ? _front! : _back!;
					if (GodotObject.IsInstanceValid(want) && tp.Card.Root.GetParent() != want) tp.Card.Root.Reparent(want, true);
					(fr ? tp.RibB : tp.RibF).Reset();
				}
				if (s < tp.Born) continue;
				float a = s - tp.Born;
				tp.Card.Root.Visible = true;
				float pop = Ease5.OutBack(Mathf.Min(1f, a / 0.22f));
				float spin = (1f - Fx.OutCubic(Mathf.Min(1f, a / 0.3f))) * Mathf.Tau * 1.5f;   // 翻转着弹出来
				// 深度：身后的小一点、暗一点
				float depth = 0.5f + 0.5f * Mathf.Sin(ang);
				float sc = (0.3f + 0.7f * pop) * (0.82f + 0.25f * depth);
				// 卡沿着环的切线方向转着飞，牌面时正时反
				float tilt = Mathf.Sin(ang + Mathf.Pi / 2f) * 0.35f + Mathf.Sin(s * 1.7f + tp.Phase) * 0.08f;
				tp.Card.Place(pos, tilt, spin + ang * 0.5f + Mathf.Sin(s * 1.3f + tp.Phase) * 0.3f, sc);
				tp.Card.Root.Modulate = new Color(1f, 1f, 1f).Lerp(new Color(0.75f, 0.6f, 0.65f), 1f - depth);
				tp.Card.Set("sheen", Mathf.PosMod(s * 0.6f + tp.Phase, 3f) - 1f);
				tp.Card.Set("glow", Mathf.Exp(-a * 8f) * 0.8f + 0.15f);
				float fade = 1f - Ease5.Seg(s, life - 0.4f, 0.4f);
				tp.Card.Set("alpha", fade);
				Phantom.Ribbon rib = fr ? tp.RibF : tp.RibB;
				rib.Fade = fade * Mathf.Min(1f, a / 0.25f) * (0.55f + 0.45f * Mathf.Min(1f, _spin / 2f));
				rib.Push(pos);
			}
		});
	}

	/// <summary>一发小刀打中（GunFx.Hit）：陷阱还在、打的是这个敌人 → 一张卡射出光矛扎进去、自己被切开烧掉；最后一张收官。</summary>
	public static void OnHit(Vector2 at)
	{
		if (Stage.NowMs() > _until || _front == null || !GodotObject.IsInstanceValid(_front)) return;
		if (at.DistanceTo(_center) > _h * 1.2f) return;
		// 挑一张已经现身、离镜头最近（在身前）的卡
		Trap? pick = null;
		float best = -9f;
		foreach (Trap tp in _traps)
		{
			if (tp.Used || !GodotObject.IsInstanceValid(tp.Card.Root) || !tp.Card.Root.Visible) continue;
			float d = Mathf.Sin(tp.Slot + _rot);
			if (d > best) { best = d; pick = tp; }
		}
		if (pick == null) return;
		pick.Used = true;
		_left--;
		bool last = _left <= 0;
		Control f = _front;
		Control b = _back != null && GodotObject.IsInstanceValid(_back) ? _back : f;
		Control own = pick.Front ? f : b;
		float h = _h, w = _w, cw = _cw;
		Vector2 c = _center;
		float u = Fx.Screen(f).Y / 1080f;
		Phantom.P5Card card = pick.Card;
		pick.RibF.Kill();
		pick.RibB.Kill();
		Node2D cr = card.Root;
		Vector2 from = cr.GlobalPosition;
		Vector2 hit = c + new Vector2(Fx.Rand(-0.12f, 0.12f) * w, Fx.Rand(-0.2f, 0.1f) * h);
		float sc0 = cr.Scale.Y;
		// 卡一亮、往后一缩，随即被切开、碎成纸屑（很快，不留一块发白的牌面）
		Vector2 cut = Vector2.FromAngle(Fx.Rand(0f, Mathf.Tau));
		card.Set("clip", new Vector3(cut.X, cut.Y, 0f));
		float rot0 = cr.Rotation;
		Fx.Animate(cr, 0, 0.16, t =>
		{
			if (!GodotObject.IsInstanceValid(cr)) return;
			Vector2 back = (from - c).Normalized() * cw * 0.3f * Fx.OutCubic(t);
			card.Place(from + back, rot0 + t * 0.6f, 0f, sc0 * (1f + 0.25f * t));
			card.Set("glow", 0.5f * (1f - t));
			card.Set("burn", Fx.OutCubic(t));
			card.Set("alpha", 1f - Ease5.Seg(t, 0.5f, 0.5f));
		});
		Fx.FreeAfter(cr, 0.18);
		Glow.Sparks(own, from, 14, 100f * u, 380f * u, 0.4f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 120f * u,
			0.015f * u, 0.04f * u, (from - c).Normalized(), 120f);
		Glow.Flash(own, from, new Color(1f, 0.95f, 0.92f), cw * 2.2f, 0.14f);
		Glow.Flash(b, from, new Color(0.95f, 0.1f, 0.16f), cw * 4f, 0.3f);
		SkillRedo5.SoftBurst(b, from, cw * 3.6f, 0.4f, new Color(0.85f, 0.05f, 0.12f));
		Phantom.Confetti(own, from, 12, 300f * u, (from - c).Normalized(), 120f, 0.8f, 7f * u);
		// 带弯的光矛：红色宽身 + 白芯
		Vector2 mid = (from + hit) / 2f;
		Vector2 nrm = (hit - from).Orthogonal().Normalized() * (Fx.Rand(0f, 1f) > 0.5f ? 1f : -1f);
		Vector2 ctl = mid + nrm * from.DistanceTo(hit) * 0.28f;
		Vector2 Bez(float t) => from.Lerp(ctl, t).Lerp(ctl.Lerp(hit, t), t);
		const float lance = 0.08f;
		SkillRedo5.Streak(f, Bez, 0f, lance, h * 0.9f, h * 0.11f, new Color(0.95f, 0.08f, 0.16f), 1.4f);
		SkillRedo5.Streak(f, Bez, 0.01f, lance, h * 0.7f, h * 0.035f, new Color(1f, 0.9f, 0.9f), 2f);
		Fx.After(f, lance, () =>
		{
			Glow.Flash(f, hit, new Color(1f, 0.93f, 0.9f), h * 0.6f, 0.13f);
			Glow.Flash(b, hit, new Color(0.9f, 0.1f, 0.15f), h * 1.4f, 0.26f);
			SkillRedo5.SoftBurst(b, c, h * 1.3f, 0.38f, new Color(0.85f, 0.05f, 0.12f));
			Glow.Sparks(f, hit, 18, 160f * u, 520f * u, 0.42f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 160f * u,
				0.02f * u, 0.045f * u, (hit - from).Normalized(), 75f);
			SkillRedo5.RingSweep(f, b, c, w * 0.75f, Fx.Rand(-0.6f, 0.6f), Fx.Rand(0f, 1f) > 0.5f ? 1f : -1f, 0f, 0.32f, 0.05f, new Color(0.95f, 0.08f, 0.16f), 1.3f, 0.7f);
			Fx.Shake(1);
			if (last) Finale(f, b, c, h, w, u);
		});
	}

	private static void Finale(Control f, Control b, Vector2 c, float h, float w, float u)
	{
		_until = 0;
		float big = Mathf.Max(w, h);
		Vector2 feet = c + new Vector2(0f, h * 0.5f);
		Fx.Shake(3);
		Sfx.Take("gx_ping", 0.16f);
		Glow.Flash(f, c, new Color(1f, 0.95f, 0.92f), w * 1.4f, 0.2f);
		Glow.Flash(b, c, new Color(0.9f, 0.08f, 0.14f), big * 3.4f, 0.45f);
		SkillRedo5.SoftBurst(b, c, big * 3.2f, 0.65f, new Color(0.85f, 0.05f, 0.12f));
		SkillRedo5.SoftBurst(b, c, big * 1.8f, 0.45f, new Color(1f, 0.3f, 0.35f));
		SkillRedo5.Cross(f, c, h * 1.7f);
		SkillRedo5.Whirl(f, b, feet, h, _rx * 0.85f, 8, 0f, 0.75f, 1f, 0.15f, 0.95f, 1.2f, 0.35f);
		SkillRedo5.RingSweep(f, b, c, _rx * 1.25f, 0.45f, 1f, 0.02f, 0.55f, 0.08f, new Color(0.95f, 0.08f, 0.16f), 1.3f, 1f);
		SkillRedo5.RingSweep(f, b, c, _rx * 1.15f, -0.45f, -1f, 0.06f, 0.55f, 0.06f, new Color(1f, 0.85f, 0.85f), 1.6f, 1f);
		// 沿着环一圈纸屑往外炸
		for (int i = 0; i < 10; i++)
		{
			float ang = Mathf.Tau * i / 10f;
			Vector2 p = OrbitAt(ang);
			Phantom.Confetti(Mathf.Sin(ang) > 0f ? f : b, p, 6, 420f * u, (p - c).Normalized(), 60f, 1f, 8f * u, 0.03f);
		}
		Phantom.Confetti(f, c, 40, 720f * u, Vector2.Up, 180f, 1.2f, 10f * u);
		Glow.Sparks(f, c, 60, 220f * u, 860f * u, 0.65f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f), new Color(0.5f, 0.02f, 0.08f) },
			220f * u, 0.025f * u, 0.06f * u, Vector2.Up, 180f);
		SkillRedo5.Motes(f, feet, h, u, 0.1f, 0.9f, 6, 0.6f);
	}

	private static void Clear()
	{
		foreach (Trap tp in _traps)
			if (GodotObject.IsInstanceValid(tp.Card.Root)) tp.Card.Root.QueueFree();
		_traps.Clear();
	}
}
