using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 幽魂形态第九版「怪盗替身术」（2026-11-15；用户：魂震不够意思，不要拘泥于「幽魂」的表达）。
/// 无实体 = 打不中他：打中的是替身。一套魔术师的手法 ——
/// · 打出：一大把预告信从他身上炸开、飞散悬在四周（翻转、闪光、拖尾），一瞬间全部从四面穿过他的身体收回去，
///   收拢的那一下他成了幻影（身体着色器，半透明 + 一侧红轮廓光），十字光、疾光、纸屑、重震；留下三张替身卡悬在他身后；
/// · 持续：三张卡慢慢浮动翻转；
/// · 被打：一张卡闪到他身前挡住，被一刀切开、烧成纸屑（打中的是替身），过一会儿再补一张；
/// · 结束：剩下的卡飞回他身上，一闪，变回实体。
/// 游戏接法在 WraithPlay.cs；身体材质通过 SkillCtx.BodyMaterial 换。
/// </summary>
public sealed class WraithVeil
{
	public static WraithVeil? Current { get; private set; }

	/// <summary>无实体还在不在（游戏里设置；预览不设）。在场 2 秒后每半秒问一次，不在了就自己收掉。</summary>
	public static Func<bool>? StillOn;

	private const float Out = 0.4f, Hang = 0.62f, Beat = 0.82f, EndDur = 0.6f;

	private sealed class Decoy
	{
		public Phantom.P5Card C = null!;
		public Vector2 Slot, Pos;
		public float Seed, Born, Busy = -1f, Sheen = -10f;
		public bool Gone;
	}

	private readonly SkillCtx _c;
	private readonly Control _front, _back;
	private readonly Node2D _glow;
	private readonly Sprite2D _glowSprite;
	private readonly ShaderMaterial _m;
	private readonly Action<ShaderMaterial?>? _setBody;
	private readonly Vector2 _feet, _chest, _mid;
	private readonly float _tall, _face, _unit, _w;
	private readonly SceneTree _tree;
	private readonly double _t0;
	private readonly Decoy?[] _decoys = new Decoy?[3];
	private readonly float[] _refill = { -1f, -1f, -1f };
	private float _t, _stopAt = -1f, _hitAt = -10f, _nextCheck = 2f, _nextApply, _nextMote = 1f, _nextSheen = 2f;
	private bool _done;

	private WraithVeil(SkillCtx c)
	{
		_c = c;
		_front = c.Front;
		_back = c.Back ?? c.Front;
		_setBody = c.BodyMaterial;
		_feet = c.Feet;
		_tall = c.Tall;
		_face = c.Face;
		_unit = c.Unit;
		_mid = _feet + new Vector2(0f, -0.55f * _tall);
		_chest = _feet + new Vector2(0f, -0.62f * _tall);
		_w = _tall * 0.14f;
		_tree = (SceneTree)Engine.GetMainLoop();
		_t0 = HitStop.RealSeconds();
		_m = new ShaderMaterial { Shader = Body };
		_m.SetShaderParameter("seed", Fx.Rand(0f, 9f));
		_m.SetShaderParameter("band", 0f);
		_m.SetShaderParameter("pre", new Vector2(-1f, -1f));
		_glow = new Node2D { Name = "P5WraithGlow" };
		_back.AddChild(_glow);
		_glow.GlobalPosition = _mid;
		_glowSprite = Glow.SoftSprite(_glow, Vector2.Zero, new Color(1f, 0.1f, 0.18f, 0f), 1f);
		_tree.ProcessFrame += Tick;
		_glow.TreeExiting += Finish;
		Step();
	}

	/// <summary>打出幽魂形态。已经开着（再打一张）：替身补满、亮一下。</summary>
	public static void Start(SkillCtx c)
	{
		if (Current is { _done: false, _stopAt: < 0f } cur)
		{
			for (int i = 0; i < 3; i++) if (cur._decoys[i] == null || cur._decoys[i]!.Gone) cur.Spawn(i, 0f);
			return;
		}
		Current?.Finish();
		var w = new WraithVeil(c);
		Current = w;
		Sfx.Play(Sfx.WraithForm, 0.45f);
		Sfx.Take("gx_fan_open", 0.24f, 0.0f);
		Sfx.Take("gx_card_slide", 0.18f, 0.08f);
		Sfx.Take("gx_fan_close", 0.26f, Beat - 0.12f);
		Sfx.Take("gx_ping", 0.14f, Beat);
		w.Storm();
	}

	/// <summary>开场：一大把卡从他身上炸开、悬住、再从四面穿过他收回去。</summary>
	private void Storm()
	{
		float h = _tall, u = _unit, f = _face;
		Control back = _back, front = _front;
		SkillRedo5.Dim(back, _mid, new Vector2(h * 3.8f, h * 2.9f), 1.8, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.25f)) * (1f - Ease5.Seg(s, 1.1f, 0.7f)) * 0.85f);
		Glow.Flash(back, _chest, new Color(1f, 0.14f, 0.22f), h * 1.6f, 0.3f);
		Glow.Sparks(front, _chest, 24, 160f * u, 700f * u, 0.5f, new[] { Phantom.Hot, Phantom.Red }, 0f, 0.01f * u, 0.03f * u, Vector2.Up, 180f);
		// 卡排成他身后一把整齐的大扇面（外圈 10 张、内圈 5 张，牌面朝外），从中间往两边依次落位
		var slots = new System.Collections.Generic.List<(Vector2 Pos, float Tilt, float Scale, float Order)>();
		for (int i = 0; i < 10; i++)
		{
			float k = i / 9f;
			float ang = Mathf.Lerp(-Mathf.Pi - 0.38f, 0.38f, k);
			Vector2 dir = new(Mathf.Cos(ang), Mathf.Sin(ang) * 0.86f);
			slots.Add((_chest + new Vector2(0f, -h * 0.06f) + dir * h * 0.88f, ang + Mathf.Pi / 2f, 1.08f, Mathf.Abs(k - 0.5f) * 2f));
		}
		for (int i = 0; i < 5; i++)
		{
			float k = i / 4f;
			float ang = Mathf.Lerp(-Mathf.Pi + 0.2f, -0.2f, k);
			Vector2 dir = new(Mathf.Cos(ang), Mathf.Sin(ang) * 0.86f);
			slots.Add((_chest + new Vector2(0f, -h * 0.06f) + dir * h * 0.56f, ang + Mathf.Pi / 2f, 0.82f, Mathf.Abs(k - 0.5f) * 2f + 0.1f));
		}
		// 扇面后面的背光：一片宽的柔光托着
		Node2D fanGlow = Fx.Root(back, _chest + new Vector2(0f, -h * 0.3f), "P5DecoyFanGlow", Beat + 0.3);
		Sprite2D fg = Glow.SoftSprite(fanGlow, Vector2.Zero, new Color(1f, 0.1f, 0.18f, 0f), 1f);
		Fx.Animate(fanGlow, 0, Beat + 0.25, t =>
		{
			float s = t * (Beat + 0.25f);
			float a = Fx.OutCubic(Ease5.Seg(s, 0.1f, 0.3f)) * (1f - Ease5.Seg(s, Hang, Beat - Hang));
			fg.Scale = new Vector2(h * 2.3f, h * 1.3f) * (0.7f + 0.3f * a) / 32f;
			fg.Modulate = new Color(1f, 0.1f, 0.18f, 0.55f * a);
		});
		// 蓄力：光线往胸口收（在扇面收回之前）
		for (int i = 0; i < 22; i++)
		{
			float ang = Mathf.Tau * i / 22f + Fx.Rand(-0.1f, 0.1f);
			Vector2 dir = new(Mathf.Cos(ang), Mathf.Sin(ang) * 0.85f);
			float r0 = h * Fx.Rand(0.5f, 0.75f), r1 = h * Fx.Rand(0.05f, 0.12f);
			float dur = Fx.Rand(0.18f, 0.24f), delay = Beat - dur - Fx.Rand(0.02f, 0.12f);
			Node2D lr = Fx.Root(front, _chest, "P5WraithIn", delay + dur + 0.1);
			Polygon2D ln = Glow.TaperPoly(lr, i % 3 == 0 ? Pal5.Hot : new Color(1f, 0.16f, 0.24f));
			float lw = Fx.Rand(2f, 4f) * u;
			ln.Visible = false;
			Fx.Animate(ln, delay, dur, t =>
			{
				ln.Visible = true;
				float hd = Fx.InCubic(Mathf.Min(1f, t * 1.3f)), tl = Fx.InCubic(t);
				ln.Polygon = Glow.TaperPts(dir * Mathf.Lerp(r0 * 1.25f, r1, tl), dir * Mathf.Lerp(r0, r1, hd), lw);
				ln.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 4f) * (1f - Ease5.Seg(t, 0.8f, 0.2f)));
			});
		}
		for (int i = 0; i < slots.Count; i++)
		{
			var (hang, tiltT, sc, order) = slots[i];
			bool inner = i >= 10;
			Vector2 exit = _chest + (_chest - hang).Normalized() * h * 0.2f;
			Phantom.P5Card card = Phantom.Card(inner ? front : back, _chest, _w * sc, Beat + 0.2, 0.45f);
			float o0 = 0.03f + order * 0.12f, land = o0 + 0.24f;
			float back0 = Hang + order * 0.05f;
			float side = hang.X < _chest.X ? -1f : 1f;
			bool killed = false;
			Vector2 ctrl = (_chest + hang) * 0.5f + new Vector2(side * h * 0.25f, h * 0.15f);   // 往外甩出去的弧
			Fx.After(front, land, () =>
			{
				Glow.Flash(front, hang, new Color(1f, 0.92f, 0.9f), h * 0.22f, 0.12f);
				Glow.Sparks(front, hang, 6, 60f * u, 260f * u, 0.4f, new[] { Phantom.Hot, Phantom.Red }, 0f, 0.008f * u, 0.022f * u, (hang - _chest).Normalized(), 50f);
			});
			Fx.Animate(card.Root, 0, Beat + 0.05, t =>
			{
				float s = t * (Beat + 0.05f);
				Vector2 pos;
				float tilt, flip, a = 1f, glow = 0f;
				if (s < back0)
				{
					float e = Ease5.OutQuint(Mathf.Clamp((s - o0) / (land - o0), 0f, 1f));
					Vector2 q1 = _chest.Lerp(ctrl, e), q2 = ctrl.Lerp(hang, e);
					pos = q1.Lerp(q2, e);
					float settle = Mathf.Max(0f, s - land);
					pos += new Vector2(0f, Mathf.Sin(settle * 9f) * Mathf.Exp(-settle * 8f) * h * 0.02f);
					tilt = Mathf.LerpAngle(tiltT + side * 2.5f, tiltT, e);
					flip = Mathf.Pi + (1f - e) * 9f * side;   // 落位后露出牌背（红底菱形）
					a = Mathf.Min(1f, (s - o0) * 25f);
					glow = 0.45f * Mathf.Exp(-Mathf.Max(0f, s - land) * 14f);
					if (s > land + 0.02f && !killed) { killed = true; card.Trail?.Kill(); }
					// 高光依次扫过整把扇面（从左到右）
					float sweepAt = Hang - 0.2f + ((hang.X - _chest.X) / h * 0.5f + 0.5f) * 0.18f;
					card.Set("sheen", Mathf.Lerp(-1f, 2f, Mathf.Clamp((s - sweepAt) / 0.22f, 0f, 1f)));
				}
				else
				{
					float e = Mathf.Clamp((s - back0) / (Beat - back0), 0f, 1f);
					float ei = e * e * e;
					pos = ei < 0.85f ? hang.Lerp(_chest, ei / 0.85f) : _chest.Lerp(exit, (ei - 0.85f) / 0.15f);
					tilt = tiltT;
					flip = Mathf.Pi + e * 5f;
					a = 1f - Ease5.Seg(ei, 0.7f, 0.18f);
					glow = 0.6f * e;
				}
				card.Place(pos, tilt, flip, 1f);
				card.Set("alpha", a);
				card.Set("glow", glow);
			});
		}
		Sfx.Take("gx_ping", 0.08f, Hang - 0.12f);
		// 主拍：全部收拢穿过他
		Fx.After(front, Beat, () =>
		{
			HitStop.Request(0.07);
			Fx.Shake(2);
			SkillRedo5.Cross(back, _chest, h * 1.0f);
			Glow.Flash(back, _chest, new Color(1f, 0.12f, 0.2f), h * 2.6f, 0.45f);
			Glow.Flash(front, _chest, new Color(1f, 0.95f, 0.94f), h * 0.32f, 0.1f);
			for (int k = 0; k < 12; k++)
			{
				float ang = Mathf.Tau * k / 12f + Fx.Rand(-0.12f, 0.12f);
				Vector2 d = new(Mathf.Cos(ang), Mathf.Sin(ang) * 0.75f);
				Vector2 a0 = _chest + d * h * 0.35f, b0 = _chest + d * h * Fx.Rand(1.05f, 1.4f);
				bool white = k % 3 == 1;
				SkillRedo5.Streak(back, t => a0.Lerp(b0, t), Fx.Rand(0f, 0.04f), 0.22f, h * 0.5f, h * (white ? 0.06f : 0.11f),
					white ? new Color(1f, 0.9f, 0.9f) : new Color(0.95f, 0.07f, 0.16f), white ? 1.6f : 1.2f);
			}
			Phantom.Confetti(front, _chest, 26, 520f * u, Vector2.Up, 180f, 1.3f, 8f * u);
			Glow.Sparks(back, _chest, 50, 200f * u, 900f * u, 1.0f, new[] { Phantom.Hot, new Color(1f, 0.28f, 0.35f), new Color(0.5f, 0.02f, 0.08f) }, -60f * u, 0.006f * u, 0.026f * u, Vector2.Up, 180f);
			for (int i = 0; i < 3; i++) Spawn(i, 0.08f * i);
		});
	}

	private Vector2 SlotOf(int i)
	{
		float f = _face, h = _tall;
		return i switch
		{
			0 => _feet + new Vector2(-f * h * 0.44f, -h * 0.98f),
			1 => _feet + new Vector2(-f * h * 0.6f, -h * 0.62f),
			_ => _feet + new Vector2(f * h * 0.08f, -h * 1.22f),
		};
	}

	/// <summary>放出一张替身卡：从他胸口飞到身后的位置。</summary>
	private void Spawn(int i, float delay)
	{
		if (_done) return;
		Phantom.P5Card card = Phantom.Card(_back, _chest, _w, 900.0, 0.45f);
		card.Root.Visible = false;
		var d = new Decoy { C = card, Slot = SlotOf(i), Pos = _chest, Seed = Fx.Rand(0f, 6f), Born = _t + delay };
		_decoys[i] = d;
		_refill[i] = -1f;
	}

	/// <summary>被打（伤害被压成 1 的那一下）：一张替身卡闪到他身前，被切开烧掉。</summary>
	public void Hit()
	{
		if (_done || _stopAt >= 0f || _t < Beat) return;
		_hitAt = _t;
		int pick = -1;
		for (int i = 0; i < 3; i++) if (_decoys[i] is { Gone: false, Busy: < 0f } dd && _t > dd.Born + 0.35f) { pick = i; break; }
		Sfx.Take("gx_slice", 0.22f);
		float h = _tall, u = _unit, f = _face;
		Vector2 guard = _chest + new Vector2(f * h * 0.32f, -h * 0.04f);
		if (pick < 0)
		{
			Glow.Flash(_front, guard, new Color(1f, 0.9f, 0.9f), h * 0.4f, 0.12f);
			return;
		}
		Decoy d = _decoys[pick]!;
		d.Busy = _t;
		Vector2 from = d.Pos;
		Phantom.P5Card card = d.C;
		Control front = _front;
		// 闪到身前（0.09s），挡下的一刀，切开烧掉
		Fx.Animate(card.Root, 0, 0.09, t => card.Place(from.Lerp(guard, Ease5.OutQuint(t)), f * 0.2f, t * 3f, 1.2f));
		Fx.After(front, 0.09f, () =>
		{
			if (!GodotObject.IsInstanceValid(card.Root)) return;
			Cut.Razor(front, guard, f > 0f ? Mathf.Pi - 0.7f : 0.7f, h * 0.42f, 4f * u, 0.12f);
			Glow.Flash(front, guard, new Color(1f, 0.95f, 0.93f), h * 0.45f, 0.12f);
			Glow.Sparks(front, guard, 18, 160f * u, 620f * u, 0.45f, new[] { Phantom.Hot, Phantom.Red }, 80f * u, 0.01f * u, 0.03f * u, new Vector2(-f, -0.3f), 60f);
			SkillRedo.BurnAway(_c, card, guard, f * 0.2f, new Vector2(-f * h * 0.08f, -h * 0.05f));
			Sfx.Take("gx_burn", 0.14f, 0.05f);
		});
		Fx.After(front, 0.6f, () => { d.Gone = true; });
		_refill[pick] = _t + 1.3f;
	}

	/// <summary>无实体结束：剩下的卡飞回他身上，一闪，变回实体。</summary>
	public void Stop()
	{
		if (_done || _stopAt >= 0f) return;
		_stopAt = _t;
		Sfx.Take("gx_card_stack", 0.2f, EndDur - 0.2f);
		Sfx.Take("gx_ping", 0.12f, EndDur - 0.12f);
		Control fr = _front;
		Vector2 ch = _chest;
		float uu = _unit, hh2 = _tall;
		Fx.After(fr, EndDur - 0.12f, () =>
		{
			Glow.Flash(fr, ch, new Color(1f, 0.92f, 0.92f), hh2 * 0.7f, 0.16f);
			Phantom.Confetti(fr, ch, 14, 380f * uu, Vector2.Up, 180f, 1.0f, 7f * uu);
			Glow.Sparks(fr, ch, 26, 120f * uu, 560f * uu, 0.6f, new[] { Phantom.Hot, new Color(1f, 0.28f, 0.35f) }, 200f * uu, 0.008f * uu, 0.03f * uu, Vector2.Up, 180f);
			Fx.Shake(1);
		});
	}

	public static void StopCurrent() => Current?.Stop();

	private void Tick()
	{
		if (_done) return;
		if (!GodotObject.IsInstanceValid(_glow) || !GodotObject.IsInstanceValid(_front))
		{
			Finish();
			return;
		}
		Stage.Safe("WraithVeil", Step);
	}

	private static float Seg(float t, float a, float len) => Mathf.Clamp((t - a) / len, 0f, 1f);
	private static float Smooth(float x) => x * x * (3f - 2f * x);

	private void Step()
	{
		_t = (float)(HitStop.RealSeconds() - _t0);
		float s = _t;
		if (_stopAt < 0f && StillOn != null && s >= _nextCheck)
		{
			_nextCheck = s + 0.5f;
			bool still = true;
			Stage.Safe("WraithVeil.alive", () => still = StillOn());
			if (!still) Stop();
		}
		float g = _stopAt >= 0f ? s - _stopAt : -1f;
		if (g >= EndDur)
		{
			Finish();
			return;
		}
		if (s >= _nextApply)
		{
			_nextApply = s + 0.5f;
			if (_setBody != null) Stage.Safe("WraithVeil.body", () => _setBody(_m));
		}
		Vector2 scr = Fx.Screen(_front);
		float feetY = Fx.ScreenPoint(_front, _feet).Y / scr.Y, topY = Fx.ScreenPoint(_front, _feet + new Vector2(0f, -1.12f * _tall)).Y / scr.Y;
		// 幻影：主拍那一瞬间整个人一起换（没有扫描）；结束时在最后那一闪换回来
		bool ghost = s >= Beat && (g < 0f || g < EndDur - 0.12f);
		float hit = s >= _hitAt ? Mathf.Exp(-(s - _hitAt) * 5f) : 0f;
		float arrive = s >= Beat ? Mathf.Exp(-(s - Beat) * 3.5f) : 0f;
		float back = g >= EndDur - 0.12f ? Mathf.Exp(-(g - EndDur + 0.12f) * 8f) : 0f;
		_m.SetShaderParameter("feet_y", feetY);
		_m.SetShaderParameter("top_y", topY);
		_m.SetShaderParameter("line", ghost ? 1.3f : -0.3f);
		_m.SetShaderParameter("clock", s);
		_m.SetShaderParameter("flash", Mathf.Min(1f, hit + arrive + back));
		_m.SetShaderParameter("thin", 0.16f * hit);
		_m.SetShaderParameter("aspect", scr.X / scr.Y);
		_m.SetShaderParameter("face", _face);

		// 身后的柔光只在主拍和被打的一瞬间亮一下
		float on = g >= 0f ? 1f - Smooth(Seg(g, 0f, EndDur - 0.1f)) : 1f;
		_glowSprite.Scale = new Vector2(_tall * (0.95f + 0.3f * arrive), _tall * (1.5f + 0.3f * arrive)) / 32f;
		_glowSprite.Modulate = new Color(1f, 0.1f, 0.18f, on * (0.45f * arrive + 0.16f * hit));

		StepDecoys(g);
		// 细尘往上飘
		if (g < 0f && s >= _nextMote && s > Beat + 0.3f)
		{
			_nextMote = s + Fx.Rand(0.2f, 0.35f);
			float hh = Fx.Rand(0f, 1f); hh *= hh;
			float wide = Mathf.Lerp(0.34f, 0.14f, hh);
			Vector2 at = _feet + new Vector2((Fx.Rand(0f, 1f) < 0.5f ? -1f : 1f) * Fx.Rand(0.5f, 1f) * wide * _tall, -(0.05f + 0.95f * hh) * _tall);
			Glow.Sparks(Fx.Rand(0f, 1f) < 0.4f ? _back : _front, at, 3, 40f * _unit, 150f * _unit, 1.2f,
				new[] { new Color(1f, 0.92f, 0.9f), new Color(1f, 0.25f, 0.32f), new Color(0.5f, 0.02f, 0.08f) },
				-70f * _unit, 0.006f * _unit, 0.02f * _unit, new Vector2(-_face * 0.35f, -1f), 34f);
		}
	}

	private void StepDecoys(float g)
	{
		float s = _t, h = _tall;
		bool sheen = s >= _nextSheen;
		if (sheen) _nextSheen = s + Fx.Rand(1.2f, 2f);
		for (int i = 0; i < 3; i++)
		{
			if (g < 0f && _refill[i] > 0f && s >= _refill[i]) Spawn(i, 0f);
			if (_decoys[i] is not { } d || d.Gone || d.Busy >= 0f || !GodotObject.IsInstanceValid(d.C.Root)) continue;
			float age = s - d.Born;
			if (age < 0f) continue;
			d.C.Root.Visible = true;
			float e = Ease5.OutQuint(Mathf.Min(1f, age / 0.35f));
			Vector2 bob = new(Mathf.Sin(s * 0.9f + d.Seed) * h * 0.02f, Mathf.Sin(s * 1.3f + d.Seed * 2f) * h * 0.03f);
			Vector2 pos = _chest.Lerp(d.Slot + bob, e);
			float flip = Mathf.Sin(s * 0.8f + d.Seed) * 0.9f + (1f - e) * 8f;
			float tilt = Mathf.Sin(s * 0.6f + d.Seed * 3f) * 0.18f + (i - 1) * 0.25f * -_face;
			float a = Mathf.Min(1f, age * 8f);
			if (g >= 0f)
			{
				// 收回：飞回胸口
				float k = Fx.InCubic(Mathf.Clamp(g / (EndDur - 0.14f), 0f, 1f));
				pos = pos.Lerp(_chest, k);
				flip += k * 8f;
				a *= 1f - Ease5.Seg(k, 0.85f, 0.15f);
				if (k >= 1f) { d.C.Trail?.Kill(); d.C.Root.Visible = false; d.Gone = true; continue; }
			}
			d.Pos = pos;
			d.C.Place(pos, tilt, flip, 1f);
			d.C.Set("alpha", a);
			if (sheen && i == (int)(s * 7f) % 3) d.Sheen = s;
			d.C.Set("sheen", Mathf.Lerp(-1f, 2f, Mathf.Clamp((s - d.Sheen) / 0.5f, 0f, 1f)));
			d.C.Set("glow", 0.3f * Mathf.Exp(-Mathf.Max(0f, s - _hitAt) * 4f));
		}
	}

	private void Finish()
	{
		if (_done) return;
		_done = true;
		_tree.ProcessFrame -= Tick;
		if (_setBody != null) Stage.Safe("WraithVeil.body", () => _setBody(null));
		if (GodotObject.IsInstanceValid(_glow)) _glow.QueueFree();
		foreach (Decoy? d in _decoys) if (d != null && GodotObject.IsInstanceValid(d.C.Root)) { d.C.Trail?.Kill(); d.C.Root.QueueFree(); }
		if (Current == this) Current = null;
	}

	// ───────────────────────── 着色器 ─────────────────────────

	private static Shader? _body;

	/// <summary>
	/// Joker 身体的幻影材质。TEXTURE = 姿势图（图集，不预乘）。位置用屏幕坐标（feet_y / top_y：脚、头顶在屏幕上的纵坐标 0～1），不依赖节点怎么摆。
	/// line：扫描线在身上的高度（0 脚 → 1 头），线以下是幻影、线以上还是实体。
	/// </summary>
	private static Shader Body => _body ??= new Shader { Code = @"
shader_type canvas_item;
uniform float feet_y = 0.8;
uniform float top_y = 0.4;
uniform float line = 1.1;
uniform float band = 0.0;      // 扫描线本身亮不亮
uniform float clock = 0.0;
uniform float flash = 0.0;
uniform float thin = 0.0;      // 被打时更透一点
uniform float aspect = 1.7778;
uniform float seed = 0.0;
uniform float face = 1.0;
uniform vec2 pre = vec2(-1.0, -1.0);   // 两道预扫的高度（<0 = 没有）
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) { float a = 0.5, s = 0.0; for (int i = 0; i < 3; i++) { s += a * noise(p); p = p * 2.03 + 7.1; a *= 0.5; } return s; }
// 周围一圈的平均透明度（轮廓用）
float ring(sampler2D tx, vec2 uv, vec2 px, float r) {
	float s = 0.0;
	for (int i = 0; i < 8; i++) {
		float a = 6.2831853 * (float(i) + 0.5) / 8.0;
		s += texture(tx, uv + vec2(cos(a), sin(a)) * px * r).a;
	}
	return s / 8.0;
}
void fragment() {
	vec4 base = texture(TEXTURE, UV) * COLOR;
	float a = base.a;
	// 身上的位置：h = 0 脚 → 1 头；x 以身高为单位
	float h = (feet_y - SCREEN_UV.y) / max(0.001, feet_y - top_y);
	vec2 q = vec2(SCREEN_UV.x * aspect / max(0.001, feet_y - top_y), h);
	// 扫描线：边缘带一点起伏，不是一条直线
	float wob = (noise(vec2(q.x * 7.0 + seed, clock * 2.0)) - 0.5) * 0.05;
	float dl = h - (line + wob);
	float cv = smoothstep(0.012, -0.012, dl);                 // 1 = 已经是幻影
	float lineGlow = exp(-abs(dl) * 55.0) * band;
	float under = exp(-max(-dl, 0.0) * 9.0) * band * cv;     // 线刚扫过的地方还热着

	// 背光：光从他身后上方来。朝光那一侧、紧贴轮廓的地方有一道利落的亮边（不是整圈发光）；
	// 另一侧是地面反上来的一点红。
	vec2 px = TEXTURE_PIXEL_SIZE;
	vec2 L = normalize(vec2(-face * 0.75, -0.66));
	float keyRim = clamp((a - texture(TEXTURE, UV + L * px * 3.0).a) * 1.6, 0.0, 1.0) * clamp((a - texture(TEXTURE, UV + L * px * 7.0).a) * 1.6 + 0.35, 0.0, 1.0);
	float keyWide = clamp((a - texture(TEXTURE, UV + L * px * 12.0).a) * 1.2, 0.0, 1.0);
	vec2 B = normalize(vec2(face * 0.6, 0.8));
	float bounce = clamp((a - texture(TEXTURE, UV + B * px * 5.0).a) * 1.4, 0.0, 1.0);
	// 亮边的强弱沿身体缓慢起伏（光在动）
	float flick = 0.75 + 0.25 * sin(q.y * 9.0 - clock * 1.7 + seed);

	// 身体：原画褪掉一些颜色、略冷；厚薄像雾一样缓慢流动（大块、柔的，不是颗粒），脸和胸口留得实一些
	float l = dot(base.rgb, vec3(0.299, 0.587, 0.114));
	vec3 ghost = mix(base.rgb, vec3(l) * vec3(0.97, 0.93, 1.0), 0.42);
	ghost = ghost + vec3(0.035, 0.02, 0.04);
	float mist = fbm(vec2(q.x * 3.6 + seed, q.y * 2.4 - clock * 0.28));
	float core = smoothstep(0.45, 0.9, h);
	float bodyA = mix(0.36, 0.9, smoothstep(0.28, 0.68, mist + core * 0.3)) - thin;
	// 光：背光的亮边（白，利落）+ 往里一小段的受光过渡 + 另一侧的红
	ghost = mix(ghost, vec3(1.0, 0.95, 0.93), keyRim * 0.95 * flick);
	ghost += vec3(0.5, 0.42, 0.46) * keyWide * 0.22 * flick;
	ghost = mix(ghost, vec3(0.95, 0.08, 0.16), bounce * 0.6);
	ghost += vec3(1.0, 0.85, 0.85) * flash * (0.12 + 0.6 * keyRim + 0.3 * bounce);
	ghost += vec3(1.0, 0.5, 0.52) * under * 0.5;

	vec3 col = mix(base.rgb, ghost, cv);
	float preGlow = exp(-abs(h - pre.x) * 42.0) * step(0.0, pre.x) + exp(-abs(h - pre.y) * 42.0) * step(0.0, pre.y);
	col += vec3(1.0, 0.94, 0.92) * lineGlow * 1.2 + vec3(1.0, 0.45, 0.48) * preGlow * 0.9;
	float alphaK = mix(bodyA, 1.0, clamp(keyRim + bounce * 0.8, 0.0, 1.0));
	float outA = a * mix(1.0, alphaK, cv);
	outA = max(outA, a * min(1.0, lineGlow));
	COLOR = vec4(col, outA);
}" };
}
