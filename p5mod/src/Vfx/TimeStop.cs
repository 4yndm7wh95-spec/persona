using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 子弹时间（本回合手牌全部免费、不能再抽牌）：「只有他在动的世界」。用户 2026-11-12：必须是时停，但不能让出牌像卡住了一样没反馈。
/// 所以牌照常立刻结算、Joker 照常出手，停住的只是「世界」：
/// · 打出：从 Joker 身边推出一道透明的折射波，波过之处颜色褪成冷灰（红色不褪，P5 的灰底红），空气里的浮尘停住不动、敌人的动画慢到 1/10；
///   他身边留着一圈有颜色的空间（软边，边上空气轻轻扭动，没有轮廓线）。同时从他身上迸出一圈红黑白的碎片，飞出那圈空间就急停、悬在半空。
/// · 时停期间：每出一张牌，那圈空间轻轻一涨、一声很轻的「嗒」；每次命中，命中点溅出的碎片和火花飞一小段就冻在半空，越挂越多。
/// · 回合结束（原版 NoDrawPower 在回合结束被移除）：一声「咔」，颜色从他身边涌回去（暖一点的折射波），波过之处浮尘接着飘、
///   悬着的碎片接着飞散落下、被打过的敌人一震，碎玻璃一样的细碎声。
/// 只用 Godot：游戏这边的接法在 BulletTimePlay.cs（敌人列表、回合结束），预览工具自己接。
/// </summary>
public sealed class TimeStop
{
	/// <summary>场上的敌人：身体（受击一震）、命中中心（全局坐标）、身体范围（全局，敌人身上单独褪色用）、设动画速度（游戏里 MegaAnimationState.SetTimeScale）。</summary>
	public readonly record struct Actor(Node2D Body, Vector2 At, Rect2 Box, Action<float> Speed);

	/// <summary>当前活着的敌人（游戏 / 预览各自设置）。每帧调用。</summary>
	public static Func<List<Actor>> Enemies = () => new();

	public static TimeStop? Current { get; private set; }

	public static bool Frozen => Current is { _goAt: < 0f };

	private const float StopDur = 0.75f, GoDur = 0.55f, Reach = 7.5f, EnemySlow = 0.1f;

	private readonly Control _front;
	private readonly Node2D _screen, _enemyScreen, _bitsRoot;
	private readonly ShaderMaterial _m, _em;
	private readonly Vector2 _center;
	private readonly float _tall, _face;
	private readonly SceneTree _tree;
	private readonly ulong _t0;
	private float _t, _goAt = -1f, _pulseAt = -10f, _last;
	private bool _done, _hadEnemies;
	private readonly List<Bit> _bits = new();
	private readonly Dictionary<ulong, (Actor A, float Cur, bool Hit, bool Jolted)> _actors = new();
	private readonly List<(Vector2 At, Vector2 Dir)> _marks = new();
	private readonly List<float> _ticks = new();          // 时停里每出一张牌的时刻：秒针往前跳一格
	private float _prevS, _prevM;

	private sealed class Bit
	{
		public Node2D Node = null!;
		public Polygon2D? Glint;
		public Vector2 Pos, Vel, Origin;
		public float Rot, Spin, Travel, Need, Speed = 1f, Life = -1f, Fade = 1f;
		public bool Streak;
		public float Len;
	}

	private TimeStop(Control front, Control back, Vector2 feet, float tall, float face)
	{
		_front = front;
		_tall = tall;
		_face = face;
		_center = feet + new Vector2(0f, -0.5f * tall);
		_tree = (SceneTree)Engine.GetMainLoop();
		_t0 = Stage.NowMs();
		// 背景调色垫在背景层最底下（人物在它上面，Joker 自然保持原色，不用在他身边挖一圈）；
		// 敌人另外在前景层最底下按各自身体范围软边褪色（之后放的特效、伤害数字在它上面保持原色）
		_screen = Fx.ScreenRoot(back, "P5TimeStop");
		back.MoveChild(_screen, 0);
		_m = new ShaderMaterial { Shader = Shader };
		_m.SetShaderParameter("mode", 0);
		_screen.AddChild(new ColorRect { Size = Fx.Screen(back), Material = _m, MouseFilter = Control.MouseFilterEnum.Ignore });
		_enemyScreen = Fx.ScreenRoot(front, "P5TimeStopEnemies");
		front.MoveChild(_enemyScreen, 0);
		_em = new ShaderMaterial { Shader = Shader };
		_em.SetShaderParameter("mode", 1);
		// 背景层已经读过一次屏幕：这里先重新拷贝一次（带上人物），不然读到的是没有人物的旧画面
		_enemyScreen.AddChild(new BackBufferCopy { CopyMode = BackBufferCopy.CopyModeEnum.Viewport });
		_enemyScreen.AddChild(new ColorRect { Size = Fx.Screen(front), Material = _em, MouseFilter = Control.MouseFilterEnum.Ignore });
		_bitsRoot = new Node2D { Name = "P5TimeStopBits" };
		front.AddChild(_bitsRoot);
		_tree.ProcessFrame += Tick;
		_screen.TreeExiting += Detach;
		_enemyScreen.TreeExiting += Detach;
	}

	/// <summary>打出子弹时间。已经在时停里（同一回合又打一张）：只再涨一下，不再开一层。</summary>
	public static void Start(Control front, Control? back, Vector2 feet, float tall, float face)
	{
		if (Current is { } cur && !cur._done && cur._goAt < 0f)
		{
			cur.Pulse();
			Sfx.Take("gx_tock", 0.35f);
			return;
		}
		Current?.Finish();
		var ts = new TimeStop(front, back ?? front, feet, tall, face);
		Current = ts;
		// 拍 1：「咔」——钟停住；冷的霜声往里收。拍 2（0.62s）：最后一下很轻的「嗒」，像秒针停住前的余震
		Sfx.Take("gx_tock", 0.55f);
		Sfx.Take("gx_freeze", 0.28f, 0.02f);
		Sfx.Take("gx_tick", 0.28f, 0.62f);   // 指针急停、卡住
		ts.Burst();
	}

	/// <summary>时停里出了一张牌（delay 秒后 = 出手那一帧）：空间轻轻一涨 + 很轻的「嗒」。</summary>
	public void Card(float delay)
	{
		if (_goAt >= 0f) return;
		Fx.After(_front, delay, () => { Pulse(); _ticks.Add(_t); Sfx.Take("gx_tick", 0.3f); });
	}

	/// <summary>命中（Impact.Confirm）：命中点溅出的碎片、火花飞一小段就冻在半空，回合结束一齐接着飞。</summary>
	public void Hit(Vector2 at, Vector2 dir, Node2D? body)
	{
		if (_goAt >= 0f || _done) return;
		dir = dir.LengthSquared() > 0.001f ? dir.Normalized() : new Vector2(_face, 0f);
		if (_marks.Count < 14) _marks.Add((at, dir));
		if (body != null && _actors.TryGetValue(body.GetInstanceId(), out var a)) _actors[body.GetInstanceId()] = a with { Hit = true };
		int n = _bits.Count > 160 ? 5 : 13;
		float u = _tall / 360f;
		for (int i = 0; i < n; i++)
		{
			float ang = dir.Angle() + Mathf.DegToRad(Fx.Rand(-80f, 80f));
			Vector2 v = Vector2.Right.Rotated(ang) * Fx.Rand(600f, 1300f) * u;
			Vector2 start = at + Vector2.Right.Rotated(ang) * Fx.Rand(4f, 18f) * u;
			if (i % 2 == 0) AddStreak(start, v, Fx.Rand(45f, 95f) * u, Fx.Rand(0.06f, 0.24f) * _tall, i % 4 == 0);
			else AddSliver(start, v, Fx.Rand(10f, 22f) * u, Fx.Rand(0.05f, 0.28f) * _tall, i % 3);
		}
	}

	/// <summary>回合结束：颜色从他身边涌回去，悬着的东西接着飞。</summary>
	public void Stop()
	{
		if (_goAt >= 0f || _done) return;
		_goAt = _t;
		Sfx.Take("gx_tock", 0.5f);
		Sfx.Take("gx_glass", 0.36f, 0.1f);
		Sfx.Take("gx_whoosh", 0.18f, 0.04f);
		for (int i = 0; i < 4; i++) Sfx.Take("gx_tick", 0.13f - 0.02f * i, 0.05f + 0.07f * i);   // 指针快进追回时间：一串很轻的棘轮声
	}

	public static void StopCurrent() => Current?.Stop();

	// ───────────────────────── 内部 ─────────────────────────

	private void Pulse() => _pulseAt = _t;

	/// <summary>打出那一下：一圈红黑白碎片从他身上迸出，飞出有颜色的那圈空间就急停。</summary>
	private void Burst()
	{
		float u = _tall / 360f;
		for (int i = 0; i < 26; i++)
		{
			float ang = Mathf.Tau * (i + Fx.Rand(-0.35f, 0.35f)) / 26f;
			Vector2 d = Vector2.Right.Rotated(ang);
			Vector2 start = _center + new Vector2(d.X * 0.12f, d.Y * 0.3f) * _tall;
			Vector2 v = d * Fx.Rand(700f, 1150f) * u;
			// 飞出那圈空间（椭圆，横 0.5、竖 0.66 身高）再多走一段才停：停在他四周不同的远近上
			float edge = 1f / Mathf.Sqrt(d.X * d.X / (0.5f * 0.5f) + d.Y * d.Y / (0.66f * 0.66f));
			float need = (edge + Fx.Rand(0.05f, 0.65f)) * _tall - 0.2f * _tall;
			if (i % 3 == 2) AddStreak(start, v, Fx.Rand(30f, 60f) * u, need, i % 2 == 0);
			else AddSliver(start, v, Fx.Rand(9f, 20f) * u, need, i % 3);
		}
	}

	private static readonly Color Red = Phantom.Red, Hot = Phantom.Hot, Ink = new(0.05f, 0.03f, 0.04f), Deep = new(0.55f, 0.02f, 0.07f);

	/// <summary>一片尖碎片：kind 0 红、1 黑、2 白；一条边上有一道会随转动明暗的高光（像在光里慢慢翻转）。</summary>
	private void AddSliver(Vector2 at, Vector2 v, float size, float need, int kind)
	{
		Node2D n = new();
		_bitsRoot.AddChild(n);
		float w = size, h = size * Fx.Rand(0.22f, 0.4f), k = Fx.Rand(-0.3f, 0.3f);
		Vector2[] tri = { new(-w, -h * 0.4f + k * h), new(w * 1.1f, 0f), new(-w * 0.7f, h) };
		Color body = kind switch { 0 => Red, 1 => Ink, _ => new Color(0.92f, 0.9f, 0.88f) };
		Fx.Poly(n, tri, body);
		if (kind != 2) Fx.Poly(n, new[] { new Vector2(-w * 0.55f, h * 0.15f), new Vector2(w * 0.7f, h * 0.05f), new Vector2(-w * 0.4f, h * 0.6f) }, kind == 0 ? Deep : new Color(0.16f, 0.1f, 0.12f));
		Polygon2D glint = Fx.Poly(n, new[] { tri[0], tri[1], tri[0].Lerp(tri[1], 0.5f) + new Vector2(0f, h * 0.18f) }, new Color(1f, 0.96f, 0.94f, 0.9f), additive: true);
		_bits.Add(new Bit { Node = n, Glint = glint, Pos = at, Origin = at, Vel = v, Rot = Fx.Rand(0f, Mathf.Tau), Spin = Fx.Rand(9f, 22f) * (Fx.Rand(0f, 1f) < 0.5f ? -1f : 1f), Need = need });
		n.Position = at;
	}

	/// <summary>一道火花：白芯红晕、沿速度拉长，冻住时保持拉长的样子（停住的动态模糊）。</summary>
	private void AddStreak(Vector2 at, Vector2 v, float len, float need, bool red)
	{
		Node2D n = new();
		_bitsRoot.AddChild(n);
		Vector2[] dia = { new(-1f, 0f), new(0f, -1f), new(0.35f, 0f), new(0f, 1f) };
		Polygon2D glow = Fx.Poly(n, dia, red ? new Color(1f, 0.15f, 0.2f, 0.75f) : new Color(1f, 0.45f, 0.4f, 0.55f), additive: true);
		glow.Scale = new Vector2(len, len * 0.13f);
		Polygon2D core = Fx.Poly(n, dia, Hot, additive: true);
		core.Scale = new Vector2(len * 0.8f, len * 0.045f);
		_bits.Add(new Bit { Node = n, Pos = at, Origin = at, Vel = v, Rot = v.Angle(), Need = need, Streak = true, Len = len });
		n.Position = at;
		n.Rotation = v.Angle();
	}

	/// <summary>离他多远（身高为单位，竖向压一点）：折射波按这个推进。</summary>
	private float Dist(Vector2 p)
	{
		Vector2 d = (p - _center) / _tall;
		return new Vector2(d.X, d.Y * 1.15f).Length();
	}

	private static float WaveR(float s, float dur) => s <= 0f ? 0f : Reach * (1f - Mathf.Pow(1f - Mathf.Min(1f, s / dur), 2f));

	private void Tick()
	{
		if (_done) return;
		if (!GodotObject.IsInstanceValid(_screen) || !GodotObject.IsInstanceValid(_enemyScreen) || !GodotObject.IsInstanceValid(_front))
		{
			Detach();
			return;
		}
		Stage.Safe("TimeStop", Step);
	}

	private void Step()
	{
		float now = (Stage.NowMs() - _t0) / 1000f;
		float dt = Mathf.Clamp(now - _last, 0f, 0.1f);
		_last = now;
		_t = now;
		float stopR = WaveR(_t, StopDur), goR = _goAt >= 0f ? WaveR(_t - _goAt, GoDur) : -1f;

		// 调色层
		Vector2 scr = Fx.Screen(_front);
		Vector2 cs = Fx.ScreenPoint(_front, _center);
		float tallS = _tall * _front.GetCanvasTransform().Scale.Y;
		float ps = _t - _pulseAt;
		foreach (ShaderMaterial m in new[] { _m, _em })
		{
			m.SetShaderParameter("aspect", scr.X / scr.Y);
			m.SetShaderParameter("center", new Vector2(cs.X / scr.X, cs.Y / scr.Y));
			m.SetShaderParameter("tall", tallS / scr.Y);
			m.SetShaderParameter("t", _t);
			m.SetShaderParameter("go_at", _goAt >= 0f ? _goAt : 1e5f);
			m.SetShaderParameter("stop_dur", StopDur);
			m.SetShaderParameter("go_dur", GoDur);
			m.SetShaderParameter("reach", Reach);
			m.SetShaderParameter("pulse_s", ps);
			m.SetShaderParameter("amount", Mathf.Min(1f, _t / 0.06f));
		}
		Clock(dt);

		// 敌人：波过了就慢下来（平滑地慢，不是一下卡住）；回来的波过了恢复，被打过的一震
		List<Actor> live = Enemies();
		// 敌人全死了（回合没结束战斗就赢了）：时间也回来
		if (live.Count > 0) _hadEnemies = true;
		else if (_hadEnemies && _goAt < 0f) Stop();
		var seen = new HashSet<ulong>();
		foreach (Actor a in live)
		{
			if (!GodotObject.IsInstanceValid(a.Body)) continue;
			ulong id = a.Body.GetInstanceId();
			seen.Add(id);
			var e = _actors.TryGetValue(id, out var old) ? old with { A = a } : (A: a, Cur: 1f, Hit: false, Jolted: false);
			float d = Dist(a.At);
			bool resumed = goR >= 0f && d < goR;
			float target = d < stopR && !resumed ? EnemySlow : 1f;
			e.Cur = Mathf.MoveToward(e.Cur, target, dt * (target < e.Cur ? 3.2f : 6f));
			a.Speed(e.Cur);
			if (resumed && e.Hit && !e.Jolted)
			{
				e.Jolted = true;
				Jolt(a);
			}
			_actors[id] = e;
		}
		// 敌人身上的褪色范围（屏幕 0～1：中心、半宽高），最多 6 个
		var boxes = new Godot.Collections.Array<Vector4>();
		foreach (Actor a in live)
		{
			if (boxes.Count >= 6 || !GodotObject.IsInstanceValid(a.Body)) continue;
			Vector2 p0 = Fx.ScreenPoint(_front, a.Box.Position), p1 = Fx.ScreenPoint(_front, a.Box.End);
			Vector2 c0 = (p0 + p1) * 0.5f, hs = (p1 - p0).Abs() * 0.5f;
			boxes.Add(new Vector4(c0.X / scr.X, c0.Y / scr.Y, hs.X / scr.X, hs.Y / scr.Y));
		}
		while (boxes.Count < 6) boxes.Add(new Vector4(-9f, -9f, 0f, 0f));
		_em.SetShaderParameter("boxes", boxes);
		// 死掉 / 不在列表里的：恢复正常速度（死亡动画按原速放）
		foreach (ulong id in new List<ulong>(_actors.Keys))
		{
			if (seen.Contains(id)) continue;
			var e = _actors[id];
			if (GodotObject.IsInstanceValid(e.A.Body)) e.A.Speed(1f);
			_actors.Remove(id);
		}

		// 回来的波经过命中点：那一下「落地」——碎光迸开
		if (goR >= 0f)
		{
			for (int i = _marks.Count - 1; i >= 0; i--)
			{
				if (Dist(_marks[i].At) < goR)
				{
					var (at, dir) = _marks[i];
					Phantom.Shards(_front, at, 7, 260f, 620f, dir, 70f, 0.42f, 900f, 12f * _tall / 360f);
					_marks.RemoveAt(i);
				}
			}
		}

		// 碎片 / 火花
		for (int i = _bits.Count - 1; i >= 0; i--)
		{
			Bit b = _bits[i];
			if (!GodotObject.IsInstanceValid(b.Node))
			{
				_bits.RemoveAt(i);
				continue;
			}
			float d = Dist(b.Pos);
			bool resumed = goR >= 0f && d < goR;
			if (resumed && b.Life < 0f)
			{
				// 接着飞：顺着原来的方向猛地蹿出去，然后落下、淡掉
				b.Life = 0f;
				Vector2 dir = b.Vel.LengthSquared() > 1f ? b.Vel.Normalized() : (b.Pos - b.Origin).Normalized();
				b.Vel = dir * Mathf.Max(b.Vel.Length() * 0.7f, 520f * _tall / 360f) + new Vector2(0f, -120f);
				b.Speed = 1f;
			}
			bool freeze = b.Life < 0f && d < stopR && b.Travel >= b.Need;
			if (b.Life < 0f)
			{
				b.Speed = freeze ? Mathf.MoveToward(b.Speed, 0.006f, dt * 9f) : b.Speed;
			}
			float s = b.Speed;
			Vector2 step = b.Vel * s * dt;
			b.Pos += step;
			b.Travel += step.Length();
			if (b.Life < 0f)
			{
				b.Vel *= Mathf.Exp(-1.2f * dt * s);
			}
			else
			{
				b.Life += dt;
				b.Vel += new Vector2(0f, 1700f * _tall / 360f) * dt;
				b.Vel *= Mathf.Exp(-1.5f * dt);
				b.Fade = 1f - Mathf.Clamp((b.Life - 0.2f) / 0.4f, 0f, 1f);
				if (b.Life > 0.65f)
				{
					b.Node.QueueFree();
					_bits.RemoveAt(i);
					continue;
				}
			}
			b.Node.Position = b.Pos;
			if (b.Streak)
			{
				// 火花：飞着的时候越拉越短；冻住就停在当时的长短（停住的动态模糊）
				if (s > 0.2f) b.Len *= Mathf.Exp(-dt * 1.6f);
				b.Node.Rotation = b.Vel.Angle();
				b.Node.Scale = new Vector2(Mathf.Max(0.25f, b.Len / 40f), 1f) * (b.Life < 0f ? 1f : 1f - 0.5f * (1f - b.Fade));
			}
			else
			{
				b.Rot += b.Spin * s * dt;
				b.Node.Rotation = b.Rot;
				// 翻转：横向压扁随转动变化，像薄片在空中翻；冻住时几乎不动，高光跟着慢慢变
				b.Node.Scale = new Vector2(1f, 0.35f + 0.65f * Mathf.Abs(Mathf.Cos(b.Rot * 1.3f)));
				if (b.Glint != null) b.Glint.Modulate = new Color(1f, 1f, 1f, 0.15f + 0.85f * Mathf.Pow(Mathf.Abs(Mathf.Sin(b.Rot * 1.3f + 0.6f)), 6f));
			}
			// 冻住的东西退一点亮度，靠近冷灰的世界；接着飞的时候回到原色
			float cold = b.Life < 0f ? 1f - s : 0f;
			b.Node.Modulate = new Color(1f - 0.18f * cold, 1f - 0.12f * cold, 1f, (1f - 0.12f * cold) * b.Fade);
		}

		// 回来的波走完：收尾
		if (_goAt >= 0f && _t - _goAt > GoDur + 0.7f)
		{
			Finish();
		}
		else if (_t > 900f)
		{
			Stop();
		}
	}

	private static float OutBack(float x) { const float c1 = 1.70158f; float y = x - 1f; return 1f + (c1 + 1f) * y * y * y + c1 * y * y; }

	private static float Spring(float s) => s <= 0f ? 0f : 1f - Mathf.Exp(-s * 22f) * Mathf.Cos(s * 34f);

	/// <summary>
	/// 透明大时钟（背景层着色器画，玻璃一样只靠折射 + 高光显形；用户 2026-11-12：从 Joker 身上展开一个透明的时钟特效）：
	/// 打出时从他胸口展开、三根针飞转后急停卡住（0.62s，和「嗒」对上）；之后淡到很淡挂在他身后；
	/// 时停里每出一张牌，红色秒针往前跳一格（带一点回弹），钟亮一下；回合结束指针快进追回时间，钟放大、碎散。
	/// </summary>
	private void Clock(float dt)
	{
		float t = _t;
		float k = Mathf.Clamp(t / 0.62f, 0f, 1f), e = 1f - Mathf.Pow(1f - k, 3f);
		float hh = -1.05f - 0.6f * (1f - e), hm = 1.9f - Mathf.Tau * (1f - e), hs = -5f * Mathf.Pi * (1f - e);
		float rs = t - 0.62f;
		if (rs > 0f) hs += 0.09f * Mathf.Sin(rs * 45f) * Mathf.Exp(-rs * 10f);
		const float notch = Mathf.Tau / 12f;
		float lastTick = -10f;
		foreach (float tk in _ticks)
		{
			hs += notch * Spring(t - tk);
			hm += notch / 12f * Spring(t - tk);
			lastTick = tk;
		}
		float r = 1.3f * (0.25f + 0.75f * OutBack(Mathf.Min(1f, t / 0.5f)));
		float a = Mathf.Min(1f, t / 0.25f);
		a = Mathf.Lerp(a, 0.42f, Mathf.SmoothStep(0.9f, 1.7f, t));
		a = Mathf.Min(1f, a + 0.45f * Mathf.Exp(-(t - lastTick) * 3f) * (t >= lastTick ? 1f : 0f));
		float shatter = 0f;
		if (_goAt >= 0f)
		{
			float g = t - _goAt, kk = Mathf.Clamp(g / 0.42f, 0f, 1f), ee = kk * kk * (3f - 2f * kk);
			hs += 6f * Mathf.Pi * ee;
			hm += 1.5f * Mathf.Pi * ee;
			hh += 0.4f * ee;
			r *= 1f + 0.45f * ee;
			a = Mathf.Max(a, 1f - g / 0.25f) * (1f - Mathf.SmoothStep(0.15f, 0.6f, g));
			shatter = Mathf.Clamp((g - 0.12f) / 0.45f, 0f, 1f);
		}
		// 转得快的时候指针拖出残影（按这一帧转过的角度）
		float bs = dt > 0f ? Mathf.Min(1.4f, Mathf.Abs(hs - _prevS)) : 0f, bm = dt > 0f ? Mathf.Min(1.0f, Mathf.Abs(hm - _prevM)) : 0f;
		_prevS = hs;
		_prevM = hm;
		_m.SetShaderParameter("ck_r", r);
		_m.SetShaderParameter("ck_a", a);
		_m.SetShaderParameter("ck_h", new Vector3(hh, hm, hs));
		_m.SetShaderParameter("ck_blur", new Vector2(bm, bs));
		_m.SetShaderParameter("ck_shatter", shatter);
	}

	/// <summary>被打过的敌人：时间回来那一下，攒着的受击一下子落到身上——往后一震、白一下。</summary>
	private void Jolt(Actor a)
	{
		Node2D body = a.Body;
		Vector2 home = body.Position;
		Vector2 dir = (a.At - _center).Normalized();
		Tween tw = body.CreateTween().SetIgnoreTimeScale(true);
		body.Position = home + dir * 22f * _tall / 360f;
		tw.TweenInterval(0.04);
		tw.TweenProperty(body, "position", home, 0.34).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
		BodyTint.Pulse(body, new Color(1.8f, 1.6f, 1.6f), 0.03, 0.22);
	}

	private void Finish()
	{
		if (_done) return;
		_done = true;
		foreach (var e in _actors.Values)
		{
			if (GodotObject.IsInstanceValid(e.A.Body)) Stage.Safe("TimeStop.speed", () => e.A.Speed(1f));
		}
		_actors.Clear();
		_tree.ProcessFrame -= Tick;
		if (GodotObject.IsInstanceValid(_screen)) _screen.QueueFree();
		if (GodotObject.IsInstanceValid(_enemyScreen)) _enemyScreen.QueueFree();
		if (GodotObject.IsInstanceValid(_bitsRoot)) _bitsRoot.QueueFree();
		if (Current == this) Current = null;
	}

	private void Detach()
	{
		if (_done) return;
		Finish();
	}

	// ───────────────────────── 着色器 ─────────────────────────

	private static Shader? _shader;

	public static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform int mode = 0;                   // 0 = 背景层（褪色 + 浮尘 + 折射波），1 = 前景层敌人身上（只褪色）
uniform vec4 boxes[6];                  // mode 1：敌人身体范围（屏幕 0～1：中心 xy、半宽高 zw）
uniform float aspect = 1.7778;
uniform vec2 center = vec2(0.22, 0.6);  // 他身体中间（0～1）
uniform float tall = 0.33;              // 身高（屏幕高度的比例）
uniform float t = 0.0;                  // 打出后过了几秒
uniform float go_at = 100000.0;         // 回合结束（时间回来）在第几秒
uniform float stop_dur = 0.75;
uniform float go_dur = 0.55;
uniform float reach = 7.5;              // 波走多远（身高为单位）
uniform float pulse_s = 10.0;           // 时停里出牌后过了几秒（从他身边推出一小圈涟漪）
uniform float amount = 1.0;
// 透明大时钟（mode 0）
uniform float ck_r = 1.3;               // 半径（身高为单位）
uniform float ck_a = 0.0;               // 可见度
uniform vec3 ck_h = vec3(0.0);          // 时针、分针、秒针角度（0 = 12 点，顺时针）
uniform vec2 ck_blur = vec2(0.0);       // 分针、秒针这一帧转过的角度（拖影）
uniform float ck_shatter = 0.0;         // 回合结束碎散 0 → 1
uniform vec2 ck_c = vec2(0.0, -0.13);   // 钟心相对身体中间（身高为单位）：胸口

float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
vec2 hash2(vec2 p) { return vec2(hash(p), hash(p + 17.31)); }

vec2 local(vec2 uv) { return (uv - center) * vec2(aspect, 1.0) / tall; }
float dist(vec2 q) { return length(q * vec2(1.0, 1.15)); }
float arrive(float d, float t0, float dur) { return t0 + dur * (1.0 - sqrt(max(0.0, 1.0 - clamp(d / reach, 0.0, 1.0)))); }
float waveR(float s, float dur) { return s <= 0.0 ? 0.0 : reach * (1.0 - pow(1.0 - min(1.0, s / dur), 2.0)); }

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

// 一根指针（钟面单位坐标，半径 1）：返回 x = 覆盖，y = 有符号的横向位置（-1～1，算折射和受光边）
vec2 hand(vec2 cq, float a, float len, float tail, float w) {
	vec2 dir = vec2(sin(a), -cos(a));
	float al = dot(cq, dir), pp = dot(cq, vec2(-dir.y, dir.x));
	float ww = w * (1.0 - 0.65 * clamp(al / len, 0.0, 1.0)) + 0.002;
	float m = smoothstep(ww, ww * 0.45, abs(pp)) * smoothstep(-tail - 0.01, -tail + 0.02, al) * smoothstep(len + 0.01, len - 0.03, al);
	return vec2(m, clamp(pp / ww, -1.0, 1.0));
}

// 拖影：沿转过的角度往回取 5 次平均
vec2 handBlur(vec2 cq, float a, float blur, float len, float tail, float w) {
	vec2 acc = vec2(0.0); float wsum = 0.0;
	for (int i = 0; i < 5; i++) {
		float f = float(i) / 4.0;
		float wt = 1.0 - 0.75 * f;
		vec2 h = hand(cq, a - blur * f, len, tail, w);
		acc += vec2(h.x * wt, h.y * h.x * wt); wsum += wt;
	}
	return vec2(min(1.0, acc.x / wsum * (1.0 + blur * 1.5)), acc.x > 0.001 ? acc.y / max(acc.x, 1e-3) : 0.0);
}

// 透明大时钟：返回折射偏移（身高单位）、高光、红、表盘内部
struct Clk { vec2 off; float spec; float red; float inside; };
Clk clockFx(vec2 q) {
	Clk o; o.off = vec2(0.0); o.spec = 0.0; o.red = 0.0; o.inside = 0.0;
	if (ck_a <= 0.001) return o;
	vec2 cq = (q - ck_c) / ck_r;
	float r = length(cq);
	if (r > 1.15) return o;
	float ang = atan(cq.x, -cq.y);
	vec2 n = cq / max(r, 1e-4);
	vec2 L = normalize(vec2(-0.55, -0.85));          // 光从左上来
	// 碎散：按噪声一块块化掉
	float sh = noise(cq * 5.0 + 3.1) * 0.8 + noise(cq * 13.0) * 0.2;
	float vis = ck_a * (1.0 - smoothstep(sh - 0.1, sh + 0.02, ck_shatter * 1.15));
	if (vis <= 0.001) return o;
	// 表盘：一块很薄的透镜，往里放大一点
	float inside = smoothstep(1.0, 0.96, r);
	o.inside = inside * vis;
	vec2 off = -cq * 0.05 * (1.0 - r * r) * inside;
	// 外沿：玻璃的厚边（内外两道斜面，受光面亮、背光面暗），不是线
	float rim = exp(-pow((r - 1.0) / 0.03, 2.0)), rim2 = exp(-pow((r - 0.955) / 0.012, 2.0));
	off += n * ((r - 1.0) / 0.03) * rim * 0.03 - n * ((r - 0.955) / 0.012) * rim2 * 0.008;
	float lit = max(0.0, dot(n, L));
	float spec = rim * (0.12 + 0.9 * pow(lit, 3.0)) + rim2 * (0.08 + 0.5 * pow(max(0.0, -dot(n, L)), 4.0));
	// 60 个分刻度（细小的玻璃切口）
	float a60 = abs(fract(ang / 6.28318 * 60.0 + 0.5) - 0.5) / 60.0 * 6.28318 * r;
	float m60 = smoothstep(0.006, 0.002, a60) * smoothstep(0.89, 0.9, r) * smoothstep(0.945, 0.935, r);
	spec += m60 * 0.3;
	// 12 个时刻度：玻璃条，红芯；3、6、9、12 更长更粗
	float fi = ang / 6.28318 * 12.0;
	float idx = floor(fi + 0.5);
	bool major = mod(idx + 12.0, 3.0) < 0.5;
	float a12 = (fi - idx) / 12.0 * 6.28318 * r;
	float tw = major ? 0.03 : 0.018, tin = major ? 0.66 : 0.75;
	float m12 = smoothstep(tw, tw * 0.4, abs(a12)) * smoothstep(tin - 0.01, tin + 0.01, r) * smoothstep(0.9, 0.88, r);
	vec2 tdir = vec2(cos(ang), sin(ang));
	off += tdir * sign(a12) * m12 * (1.0 - abs(a12) / tw) * 0.012;
	spec += m12 * (0.25 + 0.6 * smoothstep(0.0, tw, -a12 * sign(dot(tdir, L) + 1e-3)));
	o.red += smoothstep(tw * 0.55, tw * 0.15, abs(a12)) * m12 * (major ? 0.95 : 0.7);
	// 红色半调网点环（P5）：钟面里一圈，网点越往外越大，跟分针反着慢慢转
	float ra = ang - ck_h.y * 0.35;
	vec2 hp = vec2(ra / 6.28318 * 84.0, (r - 0.42) * 26.0);
	float row = floor(hp.y);
	vec2 hc = vec2(fract(hp.x + 0.5 * mod(row, 2.0)) - 0.5, fract(hp.y) - 0.5);
	float ring = smoothstep(0.42, 0.47, r) * smoothstep(0.64, 0.58, r);
	float dotR = 0.12 + 0.3 * clamp((r - 0.42) / 0.22, 0.0, 1.0);
	o.red += smoothstep(dotR, dotR - 0.08, length(hc * vec2(1.0, 1.1))) * ring * 0.45;
	// 指针：时针、分针透明玻璃，秒针 P5 红
	vec2 hH = hand(cq, ck_h.x, 0.48, 0.08, 0.05);
	vec2 hM = handBlur(cq, ck_h.y, ck_blur.x, 0.72, 0.1, 0.036);
	vec2 hS = handBlur(cq, ck_h.z, ck_blur.y, 0.9, 0.2, 0.014);
	for (int i = 0; i < 2; i++) {
		vec2 h = i == 0 ? hH : hM;
		float a = i == 0 ? ck_h.x : ck_h.y;
		vec2 dir = vec2(sin(a), -cos(a)), side = vec2(-dir.y, dir.x);
		off += side * h.y * h.x * 0.016;
		spec += h.x * (0.1 + 0.75 * pow(max(0.0, h.y * sign(dot(side, L))), 5.0));
	}
	o.red += hS.x * 0.95;
	spec += hS.x * smoothstep(0.6, 0.0, abs(hS.y)) * 0.45;
	// 秒针尾巴上的配重圆片 + 钟心
	vec2 sd = vec2(sin(ck_h.z), -cos(ck_h.z));
	float cw = smoothstep(0.045, 0.03, length(cq + sd * 0.15));
	float hub = smoothstep(0.045, 0.03, r);
	o.red += max(cw, hub) * 0.95;
	spec += hub * smoothstep(0.03, 0.0, length(cq - vec2(-0.012, -0.014))) * 0.9;
	o.off = off * vis;
	o.spec = spec * vis;
	o.red = clamp(o.red, 0.0, 1.0) * vis;
	return o;
}

// 褪色：冷灰，红色不褪
vec3 grade(vec3 b, float tint) {
	float l = dot(b, vec3(0.299, 0.587, 0.114));
	vec3 g = mix(b, vec3(l), 0.86) * mix(vec3(1.0), vec3(0.88, 0.95, 1.08), tint);
	float red = clamp((b.r - max(b.g, b.b)) * 2.2, 0.0, 1.0);
	return mix(g, b * vec3(1.0, 0.86, 0.9), smoothstep(0.25, 0.75, red) * 0.85);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 q = local(uv);
	float d = dist(q);
	float sR = waveR(t, stop_dur);
	float gS = t - go_at;
	float gR = waveR(gS, go_dur);
	float frozen = smoothstep(sR + 0.05, sR - 0.3, d) * (1.0 - smoothstep(gR + 0.05, gR - 0.35, d) * step(0.0, gS));

	if (mode == 1) {
		// 敌人身上：软边范围内褪色（背景已经在背景层褪过，再褪一次几乎不变，所以看不出范围边）
		float m = 0.0;
		for (int i = 0; i < 6; i++) {
			vec4 bx = boxes[i];
			vec2 e = abs(uv - bx.xy) / max(bx.zw, vec2(1e-4));
			float r = length(max(e - vec2(0.7, 0.75), 0.0) / vec2(0.75, 0.6));
			m = max(m, 1.0 - smoothstep(0.0, 1.0, r));
		}
		m *= frozen * amount;
		if (m <= 0.001) discard;
		vec3 b = texture(screen_tex, uv).rgb;
		COLOR = vec4(grade(b, 0.0), m);
	} else {
		vec2 radial = normalize(q + 1e-5) / vec2(aspect, 1.0) * tall;
		// 折射波：透明的一圈空气推出去，只有扭曲 + 很轻的色散，不发光、没有线
		float sFade = pow(1.0 - min(1.0, t / stop_dur), 0.7);
		float sFront = exp(-pow((d - sR) / 0.22, 2.0)) * sFade * step(0.001, t);
		float gFade = gS > 0.0 ? pow(1.0 - min(1.0, gS / go_dur), 0.6) : 0.0;
		float gFront = exp(-pow((d - gR) / 0.28, 2.0)) * gFade;
		// 出牌：他身边推出一小圈涟漪（走 1.6 个身高就散）
		float pR = pulse_s * 3.2;
		float pFront = pulse_s >= 0.0 && pulse_s < 0.6 ? exp(-pow((d - pR) / 0.16, 2.0)) * (1.0 - pulse_s / 0.6) : 0.0;
		Clk ck = clockFx(q);
		vec2 off = radial * (sFront * 0.05 + gFront * 0.07 + pFront * 0.025) + ck.off * ck_r * tall / vec2(aspect, 1.0);
		vec3 b;
		b.r = texture(screen_tex, uv - off * 1.35).r;
		b.g = texture(screen_tex, uv - off).g;
		b.b = texture(screen_tex, uv - off * 0.7).b;
		vec3 g = grade(b, 1.0);
		g = (g - 0.4) * 1.04 + 0.42;
		float vig = smoothstep(0.45, 1.3, length((uv - vec2(0.5)) * vec2(aspect * 0.6, 1.0)));
		g *= 1.0 - 0.16 * vig;
		// P5 的红：暗部透一点暗红（亮部还是冷白），四角一层很淡的红色半调网点
		float gl = dot(g, vec3(0.3, 0.55, 0.15));
		g += vec3(0.075, 0.0, 0.012) * (1.0 - smoothstep(0.02, 0.32, gl));
		vec2 hpx = mat2(vec2(0.7071, 0.7071), vec2(-0.7071, 0.7071)) * (uv * vec2(aspect, 1.0) * 1080.0) / 11.0;
		float hdot = smoothstep(vig * 0.55, vig * 0.55 - 0.1, length(fract(hpx) - 0.5));
		g = mix(g, vec3(0.42, 0.03, 0.06), hdot * smoothstep(0.15, 0.8, vig) * 0.38);
		vec3 col = mix(b, g, frozen);

		// 浮尘：动着的时候慢慢漂；波过之处停住，时间回来接着漂
		vec2 px = uv * vec2(aspect, 1.0) * 1080.0;
		float dust = 0.0;
		for (int k = 0; k < 3; k++) {
			float fk = float(k);
			float cs = 70.0 - 18.0 * fk;
			vec2 cell = floor(px / cs);
			for (int j = 0; j < 9; j++) {
				vec2 c2 = cell + vec2(float(j % 3) - 1.0, float(j / 3) - 1.0);
				float h = hash(c2 + fk * 31.7);
				if (h > 0.42) continue;
				vec2 base = (c2 + 0.15 + 0.7 * hash2(c2 + fk * 7.3)) * cs;
				float cd = dist(local(base / 1080.0 / vec2(aspect, 1.0)));
				float tau = min(t, arrive(cd, 0.0, stop_dur)) + max(0.0, t - arrive(cd, go_at, go_dur));
				vec2 p = base + vec2(sin(tau * (0.5 + h) + h * 40.0) * cs * 0.22, sin(tau * (0.4 + 0.6 * h) + h * 17.0) * cs * 0.18);
				float r = 0.7 + 1.5 * hash(c2 + 5.1 + fk);
				float dd = length(px - p);
				float tw = 0.55 + 0.45 * sin(tau * (1.3 + 2.0 * h) + h * 60.0);
				dust += (smoothstep(r, r * 0.25, dd) + 0.18 * smoothstep(r * 4.0, 0.0, dd)) * tw * (0.35 + 0.65 * h / 0.42) * (0.5 + 0.25 * fk);
			}
		}
		float dustA = (0.25 + 0.75 * frozen) * smoothstep(0.0, 0.25, t) * (1.0 - step(0.0, gS) * smoothstep(0.0, go_dur + 0.6, gS));
		col += vec3(0.78, 0.86, 1.0) * dust * 0.3 * dustA;
		// 时钟：表盘里透一点暗红的光（越近钟心越暖），玻璃高光，红色的刻度、网点、秒针
		float cr = length((q - ck_c) / ck_r);
		col += vec3(0.16, 0.012, 0.03) * ck.inside * pow(max(0.0, 1.0 - cr), 1.5) * 0.9;
		col += vec3(1.0, 0.96, 0.95) * ck.spec * 0.5;
		col = mix(col, vec3(0.93, 0.07, 0.13), ck.red * 0.85);
		col += vec3(0.25, 0.02, 0.04) * ck.red * 0.35;
		COLOR = vec4(mix(b, col, amount), 1.0);
	}
}" };
}
