using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 刀刃陷阱（把消耗牌堆里的小刀全部对一个敌人打出）「预告信陷阱」
/// （用户 2026-11-13：原来太简单 → 光弹太少 → 光点看不清、太小胆；2026-11-14：光点不该从 Joker 那里出来，
///   应该在敌人附近现身；有几张小刀就有几个；表现力要更冲击、更漂亮）：
/// ① 布阵：有几张小刀就有几张预告信卡，一张接一张在敌人四周的空中现身（原地红白一闪、网点光晕、碎光，卡翻转着弹出来，
///    每张一声很轻的翻牌声），围成一个大圈；前半圈在它身前、后半圈在身后；悬停时轻轻浮动，高光偶尔扫过牌面。
///    敌人周围一大圈压暗、脚下红光一呼一吸。
/// ② 收网：每一发小刀打中（GunFx.Hit），一张卡带着红白拖尾边转边冲进它 —— 冲到时被一刀切开、从切口烧掉，
///    它身上一闪、迸出纸屑和网点光晕、轻震。
/// ③ 收官：最后一张时更大的爆发 —— 十字光芒、大网点光晕、重震。
/// </summary>
public static class KnifeTrapFx
{
	private sealed class Trap
	{
		public Phantom.P5Card Card = null!;
		public Vector2 Home;
		public float Tilt, Phase, Born;
		public bool Used, Front;
	}

	private static readonly List<Trap> _traps = new();
	private static Vector2 _center;
	private static float _h, _w, _cw;
	private static Control? _front, _back;
	private static ulong _until;
	private static int _left;

	public const float Interval = 0.07f;

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
		float life = 1.0f + Interval * count + 3.0f;
		_until = Stage.NowMs() + (ulong)(life * 1000f);
		float rx = Mathf.Max(tw * 0.95f, th * 0.75f), ry = th * 0.55f;
		float cw = th * 0.3f;
		_cw = cw;
		Vector2 feet = center + new Vector2(0f, th * 0.5f);
		// 敌人周围一大圈压暗透红、脚下红光呼吸
		SkillRedo5.Dim(back, center, new Vector2(rx * 3.6f, th * 2.8f), life, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.3f)) * (1f - Ease5.Seg(s, life - 0.6f, 0.6f)));
		Node2D floor = Fx.Root(back, feet, "P5TrapFloor", life + 0.1);
		Sprite2D fs = Glow.SoftSprite(floor, Vector2.Zero, new Color(1f, 0.1f, 0.18f, 0f), 1f);
		fs.Scale = new Vector2(rx * 2.6f, rx * 0.5f) / 32f;
		Fx.Animate(floor, 0, life, t =>
		{
			float s = t * life;
			float a = Fx.OutCubic(Ease5.Seg(s, 0f, 0.3f)) * (1f - Ease5.Seg(s, life - 0.5f, 0.5f));
			fs.Modulate = new Color(1f, 0.1f, 0.18f, (0.5f + 0.18f * Mathf.Sin(s * 4f)) * a);
		});
		Glow.Flash(back, center, new Color(0.9f, 0.1f, 0.15f), rx * 2.4f, 0.3f);
		BodyTint.Pulse(body, new Color(0.62f, 0.38f, 0.42f), 0.1, 0.5);

		// 一张张现身：从敌人正前上方开始，交替左右排开（看起来是一圈围拢）
		for (int i = 0; i < count; i++)
		{
			float k = count == 1 ? 0f : (float)i / count;
			float ang = -Mathf.Pi / 2f + Mathf.Tau * k + Fx.Rand(-0.1f, 0.1f);
			Vector2 home = center + new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry);
			bool fr = Mathf.Sin(ang) > -0.15f;
			Trap tp = new()
			{
				Home = home, Phase = Fx.Rand(0f, Mathf.Tau), Born = 0.05f + Interval * i, Front = fr,
				// 卡身朝着敌人倾斜
				Tilt = (center - home).Angle() + Mathf.Pi / 2f,
			};
			tp.Card = Phantom.Card(fr ? front : back, home, cw, life + 0.2, 0.5f);
			tp.Card.Root.Visible = false;
			_traps.Add(tp);
			Trap t0 = tp;
			Control layer = fr ? front : back;
			Fx.After(front, tp.Born, () =>
			{
				Sfx.Take("gx_card_slide", 0.1f * 0.8f);
				Glow.Flash(layer, t0.Home, new Color(1f, 0.92f, 0.9f), cw * 1.6f, 0.12f);
				Glow.Flash(back, t0.Home, new Color(0.9f, 0.1f, 0.15f), cw * 3.4f, 0.26f);
				SkillRedo5.SoftBurst(back, t0.Home, cw * 3.2f, 0.35f, new Color(0.85f, 0.05f, 0.12f));
				Glow.Sparks(layer, t0.Home, 12, 80f * u, 300f * u, 0.4f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 60f * u,
					0.015f * u, 0.04f * u, Vector2.Up, 180f);
			});
		}
		Fx.After(front, 0.05f + Interval * count, () => Sfx.Take("gx_ping", 0.16f * 0.8f));
		var traps = new List<Trap>(_traps);
		Node2D driver = Fx.Root(front, center, "P5TrapDriver", life + 0.1);
		Fx.Animate(driver, 0, life, t =>
		{
			float s = t * life;
			foreach (Trap tp in traps)
			{
				if (tp.Used || !GodotObject.IsInstanceValid(tp.Card.Root)) continue;
				if (s < tp.Born) continue;
				float a = s - tp.Born;
				tp.Card.Root.Visible = true;
				float pop = Ease5.OutBack(Mathf.Min(1f, a / 0.22f));
				float spin = (1f - Fx.OutCubic(Mathf.Min(1f, a / 0.3f))) * Mathf.Tau * 1.5f;   // 翻转着弹出来
				Vector2 pos = tp.Home + new Vector2(0f, Mathf.Sin(s * 2.6f + tp.Phase) * _h * 0.03f);
				tp.Card.Place(pos, tp.Tilt + Mathf.Sin(s * 1.7f + tp.Phase) * 0.08f, spin + Mathf.Sin(s * 1.3f + tp.Phase) * 0.35f, 0.3f + 0.7f * pop);
				tp.Card.Set("sheen", Mathf.PosMod(s * 0.6f + tp.Phase, 3f) - 1f);
				tp.Card.Set("glow", Mathf.Exp(-a * 8f) * 0.8f);
				tp.Card.Set("alpha", 1f - Ease5.Seg(s, life - 0.4f, 0.4f));
			}
		});
	}

	/// <summary>一发小刀打中（GunFx.Hit）：陷阱还在、打的是这个敌人 → 一张卡冲进去、被切开烧掉；最后一张收官。</summary>
	public static void OnHit(Vector2 at)
	{
		if (Stage.NowMs() > _until || _front == null || !GodotObject.IsInstanceValid(_front)) return;
		if (at.DistanceTo(_center) > _h * 1.2f) return;
		Trap? pick = null;
		foreach (Trap tp in _traps)
			if (!tp.Used && GodotObject.IsInstanceValid(tp.Card.Root)) { pick = tp; break; }
		if (pick == null) return;
		pick.Used = true;
		_left--;
		bool last = _left <= 0;
		Control f = _front;
		Control b = _back != null && GodotObject.IsInstanceValid(_back) ? _back : f;
		float h = _h, w = _w, cw = _cw;
		Vector2 c = _center;
		Phantom.P5Card card = pick.Card;
		Vector2 from = card.Root.GlobalPosition, to = c + (from - c) * 0.15f;
		float tilt0 = card.Root.Rotation;
		const float dash = 0.09f;
		Fx.Animate(card.Root, 0, dash, t =>
		{
			if (!GodotObject.IsInstanceValid(card.Root)) return;
			float e = t * t;
			card.Place(from.Lerp(to, e), tilt0 + t * 2.5f, t * Mathf.Tau, 1f + 0.2f * t);
			card.Set("glow", t * 0.6f);
		});
		SkillRedo5.Streak(f, t => from.Lerp(to, t), 0f, dash, h * 0.6f, h * 0.09f, new Color(0.9f, 0.08f, 0.15f), 1.3f);
		Fx.After(f, dash, () =>
		{
			if (!GodotObject.IsInstanceValid(card.Root)) return;
			float u = Fx.Screen(f).Y / 1080f;
			// 卡被一刀切开、从切口烧掉
			Vector2 cut = Vector2.FromAngle(Fx.Rand(0f, Mathf.Tau));
			card.Set("clip", new Vector3(cut.X, cut.Y, 0f));
			card.Trail?.Kill();
			Node2D cr = card.Root;
			Fx.Animate(cr, 0, 0.32, t =>
			{
				if (!GodotObject.IsInstanceValid(cr)) return;
				card.Set("burn", t);
				card.Set("glow", 0.6f * (1f - t));
				cr.GlobalPosition = to + cut * cw * 0.3f * t;
			});
			Fx.FreeAfter(cr, 0.35);
			Glow.Flash(f, to, new Color(1f, 0.93f, 0.9f), h * 0.55f, 0.13f);
			Glow.Flash(b, to, new Color(0.9f, 0.1f, 0.15f), h * 1.3f, 0.25f);
			SkillRedo5.SoftBurst(b, c, h * 1.2f, 0.35f, new Color(0.85f, 0.05f, 0.12f));
			Phantom.Confetti(f, to, 14, 420f * u, Vector2.Up, 160f, 0.8f, 8f * u);
			Glow.Sparks(f, to, 16, 160f * u, 480f * u, 0.4f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f) }, 160f * u,
				0.02f * u, 0.045f * u, Vector2.Up, 180f);
			Fx.Shake(1);
			if (!last) return;
			// 收官
			Fx.Shake(3);
			Glow.Flash(f, c, new Color(1f, 0.95f, 0.92f), w * 1.3f, 0.18f);
			Glow.Flash(b, c, new Color(0.9f, 0.08f, 0.14f), Mathf.Max(w, h) * 3.2f, 0.42f);
			SkillRedo5.SoftBurst(b, c, Mathf.Max(w, h) * 3f, 0.6f, new Color(0.85f, 0.05f, 0.12f));
			SkillRedo5.Cross(f, c, h * 1.6f);
			Phantom.Confetti(f, c, 40, 700f * u, Vector2.Up, 180f, 1.1f, 10f * u);
			Glow.Sparks(f, c, 50, 220f * u, 800f * u, 0.6f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f), new Color(0.5f, 0.02f, 0.08f) },
				220f * u, 0.025f * u, 0.06f * u, Vector2.Up, 180f);
			_until = 0;
		});
	}

	private static void Clear()
	{
		foreach (Trap tp in _traps)
			if (GodotObject.IsInstanceValid(tp.Card.Root)) tp.Card.Root.QueueFree();
		_traps.Clear();
	}
}
