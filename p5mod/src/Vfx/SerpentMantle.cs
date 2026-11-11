using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 群蛇形态 · 常驻的光蛇（2026-11-11，用户：更重要的是开完群蛇之后的常驻特效，要霸气）。
/// 能力在身上的时候，一条光做的蛇一直盘在 Joker 身上：从腰后绕到胸前再绕到背后（前半圈盖在他身前、后半圈在身后），
/// 脖子从肩后立起来，头悬在他肩膀后上方、朝着敌人，慢慢地呼吸、起伏，身上的光往尾巴流，眼睛半亮。
/// 每打出一张牌（能力触发、原版对随机敌人造成伤害）：蛇头猛地扑出去咬那个敌人——嘴张到最大、眼睛亮成一道横光、
/// 身体被拉长成一道光，咬中的一下：闪光、速度线、火星、轻震、咬合声；然后收回来，盘回原处。连续出牌就一口接一口。
/// 起手（SerpentAscend）还在演的时候先不出来，等它扎进胸口以后从身上浮现。
/// 画由 LightSerpent 负责；这里每帧跟着 Joker 的脚底重新排路线。
/// </summary>
public sealed class SerpentMantle
{
	private const float StrikeTime = 0.42f;   // 扑出去 → 咬 → 收回
	private const float BiteAt = 0.13f;       // 咬中的时刻（从扑出去算）

	public static SerpentMantle? Current { get; private set; }

	private readonly LightSerpent _s;
	private readonly Control _front, _back;
	private readonly Func<(Vector2 Feet, float Tall, float Face)?> _body;
	private readonly Queue<Vector2> _targets = new();
	private readonly SceneTree _tree;
	private float _t, _fadeIn = 1f, _fadeOut = -1f;
	private float _strikeT = -1f;
	private Vector2 _target;
	private bool _bitten;

	/// <summary>
	/// 开始（已经有就沿用）。body 每帧给出 Joker 的脚底（层坐标）、身高、朝向；拿不到（死了 / 不在了）就返回 null，蛇跟着消失。
	/// delay = 多久以后才浮现（起手还在演的时候）。
	/// </summary>
	public static SerpentMantle Start(Control front, Control back, Func<(Vector2 Feet, float Tall, float Face)?> body, float delay = 0f)
	{
		if (Current != null && Current._s.Valid && Current._fadeOut < 0f)
		{
			return Current;
		}
		Current?.Free();
		Current = new SerpentMantle(front, back, body, delay);
		return Current;
	}

	/// <summary>能力没了（或者战斗结束）：慢慢淡掉。</summary>
	public static void StopCurrent()
	{
		if (Current != null && Current._fadeOut < 0f)
		{
			Current._fadeOut = 0f;
		}
	}

	private SerpentMantle(Control front, Control back, Func<(Vector2 Feet, float Tall, float Face)?> body, float delay)
	{
		_front = front;
		_back = back;
		_body = body;
		_fadeIn = -delay;
		(Vector2 feet, float tall, float _) = body() ?? (Vector2.Zero, 300f, 1f);
		// 范围：身后一点到右边很远（扑出去咬敌人）
		_s = new LightSerpent(front, back, feet, 0.1f * tall, new Rect2(-6f * tall, -2.4f * tall, 12f * tall, 3.0f * tall), "P5SerpentMantle", -1);
		_tree = (SceneTree)Engine.GetMainLoop();
		_tree.ProcessFrame += Tick;
		_s.Owner.TreeExiting += Detach;
	}

	/// <summary>能力触发：扑出去咬 target（层坐标，敌人身体中间）。正在咬就排队。</summary>
	public void Strike(Vector2 target)
	{
		if (_targets.Count < 6)
		{
			_targets.Enqueue(target);
		}
	}

	private void Detach()
	{
		_tree.ProcessFrame -= Tick;
		if (Current == this) Current = null;
	}

	private void Free()
	{
		Detach();
		_s.Free();
	}

	private static float Smooth(float a, float b, float x) { float t = Mathf.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3f - 2f * t); }

	private void Tick()
	{
		if (!_s.Valid || !GodotObject.IsInstanceValid(_front) || !GodotObject.IsInstanceValid(_back))
		{
			Free();
			return;
		}
		if (_body() is not var (feet, tall, face))
		{
			Free();
			return;
		}
		float dt = Mathf.Min((float)_s.Owner.GetProcessDeltaTime(), 0.1f);   // 游戏帧时间（顿帧、录屏时都对）
		_t += dt;
		_fadeIn += dt;
		if (_fadeOut >= 0f)
		{
			_fadeOut += dt;
			if (_fadeOut > 0.6f)
			{
				Free();
				return;
			}
		}
		if (_strikeT < 0f && _targets.Count > 0 && _fadeIn > 0.4f)
		{
			_target = _targets.Dequeue();
			_strikeT = 0f;
			_bitten = false;
			Sfx.Play(Sfx.SerpentStrike, 0.7f);
		}
		float strike = 0f;
		if (_strikeT >= 0f)
		{
			_strikeT += dt;
			strike = _strikeT;
			if (_strikeT >= StrikeTime)
			{
				_strikeT = -1f;
				strike = 0f;
			}
		}
		_s.MoveTo(feet);
		Pose(tall, face, strike, _target - feet);

		float show = Smooth(0f, 0.6f, _fadeIn) * (_fadeOut >= 0f ? 1f - Smooth(0f, 0.6f, _fadeOut) : 1f);
		float out_ = strike > 0f ? Smooth(0f, 0.05f, strike) * (1f - Smooth(BiteAt + 0.06f, StrikeTime, strike)) : 0f;
		float bite = strike > 0f ? Mathf.Exp(-Mathf.Pow((strike - BiteAt) / 0.05f, 2f)) : 0f;
		float breathe = 0.5f + 0.5f * Mathf.Sin(_t * 1.6f);
		// 平时半亮、不抢戏；扑出去的时候全亮
		float alpha = show * Mathf.Lerp(0.55f + 0.08f * breathe, 1f, out_);
		_s.Push(0.22f + 0.08f * breathe + 0.7f * out_, 0.35f + 0.1f * breathe + 0.65f * out_, 0.15f * breathe + 0.8f * bite, alpha, face);

		if (strike > 0f && !_bitten && strike >= BiteAt)
		{
			_bitten = true;
			(Vector2 tip, Vector2 hd) = _s.Head();
			Vector2 at = feet + tip;
			Glow.Flash(_front, at, Pal5.Bright, 1.1f * tall, 0.25f);
			Glow.Flash(_front, at, Pal5.Hot, 0.45f * tall, 0.14f);
			Glow.SpeedLines(_front, at, Pal5.Bright, 12, 0.25f * tall, 1.0f * tall);
			Glow.Sparks(_front, at, 22, 150f, 600f, 0.45f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 300f, 0.12f, 0.35f, hd, 70f);
			Fx.Shake(1);
		}
	}

	/// <summary>
	/// 排身体（相对脚底）。盘：从腰后起，绕身体 1.15 圈往上到胸口高度、收在背后；脖子从肩后立起来，头悬在肩膀后上方朝敌人。
	/// strike &gt; 0：头沿一道弧扑向 target（相对脚底），身体被拉长跟着出去，再收回来。
	/// </summary>
	private void Pose(float T, float face, float strike, Vector2 target)
	{
		var pts = new List<Vector3>(64);
		float breathe = Mathf.Sin(_t * 1.6f);
		const int coil = 30;
		for (int k = 0; k <= coil; k++)
		{
			float u = k / (float)coil;
			// 从背后左下开始，绕一圈多，收在背后偏右（离敌人远的一侧的肩后）
			float th = (0.62f + u * 1.15f) * Mathf.Tau;
			float R = Mathf.Lerp(0.5f, 0.38f, u) * T * (1f + 0.025f * breathe);
			float y = Mathf.Lerp(-0.18f, -0.92f, u) * T;
			float sx = Mathf.Sin(th) * face;
			pts.Add(new Vector3(sx * R, y + Mathf.Cos(th) * 0.07f * T + Mathf.Sin(_t * 2.1f + u * 7f) * 0.012f * T, Mathf.Cos(th)));
		}
		// 脖子：从肩后立起来，S 形，头根停在肩膀后上方
		Vector3 c0 = pts[^1];
		Vector2 restBase = new Vector2(-0.12f * face * T, -1.42f * T) + new Vector2(Mathf.Sin(_t * 1.3f) * 0.03f * T, breathe * 0.025f * T);
		Vector2 look = new Vector2(face, 0.22f).Normalized();
		for (int k = 1; k <= 10; k++)
		{
			float u = k / 10f;
			Vector2 a = new(c0.X, c0.Y), b = restBase;
			Vector2 mid = a.Lerp(b, u) + new Vector2(-face * Mathf.Sin(u * Mathf.Pi) * 0.16f * T, 0f) - look * 0.12f * T * u * u;
			pts.Add(new Vector3(mid.X, mid.Y, Mathf.Lerp(c0.Z, -0.3f, u)));
		}
		Vector2 headDir = look;
		// 扑出去：从头根沿一道往上拱的弧冲向目标，到了再收回来
		float reach = 0f;
		if (strike > 0f)
		{
			reach = strike < BiteAt ? Ease5.OutQuint(strike / BiteAt) : 1f - Smooth(BiteAt + 0.05f, StrikeTime, strike);
			Vector2 from = restBase;
			Vector2 to = target - (target - from).Normalized() * 0.32f * T;    // 头根停在目标前面一点，吻尖正好咬到
			Vector2 ctrl = from.Lerp(to, 0.5f) + new Vector2(0f, -0.45f * T);
			const int seg = 14;
			for (int k = 1; k <= seg; k++)
			{
				float u = k / (float)seg * reach;
				Vector2 q = (1 - u) * (1 - u) * from + 2 * (1 - u) * u * ctrl + u * u * to;
				pts.Add(new Vector3(q.X, q.Y, 0.4f));
			}
			Vector2 tg = 2 * (1 - reach) * (ctrl - from) + 2 * reach * (to - ctrl);
			headDir = look.Lerp(tg.Normalized(), Smooth(0f, 0.3f, reach)).Normalized();
		}
		// 头：沿朝向伸出去一段
		Vector3 hb = pts[^1];
		float headLen = 0.3f * T;
		// 按路程把身体（0～34）铺在整条线上，头（35～39）沿朝向
		var acc = new float[pts.Count];
		for (int i = 1; i < pts.Count; i++) acc[i] = acc[i - 1] + new Vector2(pts[i].X - pts[i - 1].X, pts[i].Y - pts[i - 1].Y).Length();
		float total = acc[^1];
		int j = 0;
		for (int k = 0; k <= 34; k++)
		{
			float s = total * k / 34f;
			while (j < pts.Count - 2 && acc[j + 1] < s) j++;
			float f = acc[j + 1] > acc[j] ? (s - acc[j]) / (acc[j + 1] - acc[j]) : 0f;
			Vector3 v = pts[j].Lerp(pts[j + 1], Mathf.Clamp(f, 0f, 1f));
			float a = Smooth(0f, 0.12f, k / 34f);                    // 尾巴淡出
			if (v.Z > 0f) a *= 0.8f;                                 // 盖在 Joker 身前的那段淡一点
			_s.Spine[k] = new Vector4(v.X, v.Y, v.Z, a);
		}
		for (int k = 35; k < LightSerpent.N; k++)
		{
			float u = (k - 34) / 5f;
			Vector2 p = new Vector2(hb.X, hb.Y) + headDir * headLen * u;
			_s.Spine[k] = new Vector4(p.X, p.Y, reach > 0.05f ? 0.4f : -0.3f, 1f);
		}
		_s.Spine[34] = new Vector4(hb.X, hb.Y, reach > 0.05f ? 0.4f : -0.3f, 1f);
	}
}
