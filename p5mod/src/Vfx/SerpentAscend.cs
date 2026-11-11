using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 群蛇形态 · 起手「升龙」（2026-11-11。用户：蛇影太阴暗；群蛇形态是很强、很帅的牌，要霸气）。
/// 脚下先往里一收 → 炸开 → 光做的巨蛇绕着 Joker 高速螺旋冲上去（前半圈在他前面、后半圈在身后，一路洒火星）
/// → 冲过头顶在高处昂首定住半秒：嘴张到最大、眼睛亮成一道横向的光、闪光 + 速度线 + 震屏（「吼」）
/// → 掉头俯冲，一头扎进 Joker 胸口：白闪、两道地面冲击环、火星往上喷、重震、短暂的顿帧，Joker 身上一层红光退掉。
/// 之后常驻的蛇（SerpentMantle）从他身上浮现出来。画由 LightSerpent 负责，这里只排路线和节奏。
/// </summary>
public sealed class SerpentAscend
{
	public const float Life = 2.3f;
	/// <summary>扎进胸口的时刻（从开始算）：常驻的蛇在这之后浮现。</summary>
	public const float StrikeAt = 1.62f;
	/// <summary>最近一次起手扎进胸口的时刻（引擎毫秒）：常驻的蛇等到这之后再出来。</summary>
	public static ulong BusyUntilMs;

	private const int PathN = 500;
	private const float BodyLen = 2.4f;

	private readonly LightSerpent _s;
	private readonly Control _front, _back;
	private readonly Vector2 _feet;
	private readonly float _tall, _face;
	private readonly Vector3[] _path = new Vector3[PathN];
	private readonly float[] _acc = new float[PathN];
	private readonly float _sHang, _sEntry;
	private readonly Action<Color>? _selfTint;
	private float _nextSpark;
	private bool _gathered, _burst, _roared, _struck;

	public static SerpentAscend Play(Control front, Control back, Vector2 feet, float tall, float face, Action<Color>? selfTint = null)
		=> new(front, back, feet, tall, face, selfTint);

	private SerpentAscend(Control front, Control back, Vector2 feet, float tall, float face, Action<Color>? selfTint)
	{
		_front = front;
		_back = back;
		_feet = feet;
		_tall = tall;
		_face = face;
		_selfTint = selfTint;
		BusyUntilMs = Time.GetTicksMsec() + (ulong)(StrikeAt * 1000f);
		(_sHang, _sEntry) = BuildPath();
		_s = new LightSerpent(front, back, feet, 0.16f * tall,
			new Rect2(-1.8f * tall, -2.7f * tall, 3.8f * tall, 3.0f * tall), "P5SerpentAscend", Life + 0.1);
		Update(0f);
		Fx.Animate(_s.Owner, 0, Life, k => Update(k * Life));
	}

	private static float Smooth(float a, float b, float x) { float t = Mathf.Clamp((x - a) / (b - a), 0f, 1f); return t * t * (3f - 2f * t); }

	private static Vector3 CatRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float t2 = t * t, t3 = t2 * t;
		return 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
	}

	/// <summary>路线：螺旋上升 1.6 圈 → 冲过头顶到高处昂首 → 掉头俯冲进胸口（再往里一点，被「吞掉」）。返回 (昂首处路程, 胸口处路程)。</summary>
	private (float, float) BuildPath()
	{
		float T = _tall, f = _face;
		var pts = new System.Collections.Generic.List<Vector3>();
		const int spiral = 26;
		for (int k = 0; k <= spiral; k++)
		{
			float u = k / (float)spiral;
			float th = (-0.35f + u * 1.6f) * Mathf.Tau * f;
			float R = Mathf.Lerp(0.85f, 0.58f, u) * T;
			float y = Mathf.Lerp(0.1f, -1.15f, u) * T;
			pts.Add(new Vector3(Mathf.Sin(th) * R, y + Mathf.Cos(th) * 0.18f * T, Mathf.Cos(th)));
		}
		pts.Add(new Vector3(-0.15f * T * f, -1.5f * T, -0.4f));
		pts.Add(new Vector3(0.2f * T * f, -1.8f * T, -0.3f));
		int hangIdx = pts.Count;
		pts.Add(new Vector3(0.66f * T * f, -1.74f * T, -0.2f));       // 昂首：头在这里，朝敌人、略朝下
		pts.Add(new Vector3(0.8f * T * f, -1.4f * T, 0.3f));
		pts.Add(new Vector3(0.32f * T * f, -1.05f * T, 0.6f));
		int entryIdx = pts.Count;
		pts.Add(new Vector3(0.04f * T * f, -0.72f * T, 0.6f));       // 胸口
		pts.Add(new Vector3(-0.12f * T * f, -0.5f * T, 0.6f));
		pts.Add(new Vector3(-0.25f * T * f, -0.3f * T, 0.6f));

		var raw = new System.Collections.Generic.List<Vector3>();
		var idxAt = new int[pts.Count];
		for (int i = 0; i < pts.Count - 1; i++)
		{
			idxAt[i] = raw.Count;
			Vector3 p0 = pts[Math.Max(0, i - 1)], p1 = pts[i], p2 = pts[i + 1], p3 = pts[Math.Min(pts.Count - 1, i + 2)];
			for (int k = 0; k < 10; k++) raw.Add(CatRom(p0, p1, p2, p3, k / 10f));
		}
		idxAt[^1] = raw.Count;
		raw.Add(pts[^1]);
		var rawAcc = new float[raw.Count];
		for (int i = 1; i < raw.Count; i++) rawAcc[i] = rawAcc[i - 1] + new Vector2(raw[i].X - raw[i - 1].X, raw[i].Y - raw[i - 1].Y).Length();
		float total = rawAcc[^1];
		int seg = 0;
		for (int i = 0; i < PathN; i++)
		{
			float s = total * i / (PathN - 1);
			while (seg < raw.Count - 2 && rawAcc[seg + 1] < s) seg++;
			float t = rawAcc[seg + 1] > rawAcc[seg] ? (s - rawAcc[seg]) / (rawAcc[seg + 1] - rawAcc[seg]) : 0f;
			_path[i] = raw[seg].Lerp(raw[seg + 1], Mathf.Clamp(t, 0f, 1f));
			_acc[i] = s;
		}
		return (rawAcc[idxAt[hangIdx]], rawAcc[idxAt[entryIdx]]);
	}

	private Vector3 At(float s)
	{
		if (s <= 0f) return _path[0] + new Vector3(0f, -s * 0.15f, 0f);   // 还在地下
		if (s >= _acc[PathN - 1]) return _path[PathN - 1];
		float k = s / _acc[PathN - 1] * (PathN - 1);
		int i = Mathf.Clamp((int)k, 0, PathN - 2);
		return _path[i].Lerp(_path[i + 1], k - i);
	}

	private void Update(float t)
	{
		float T = _tall;
		// 时间线：0～0.16 脚下往里收 → 0.16 炸开 → 0.16～1.0 冲上去（先猛后收）→ 1.0～1.45 昂首（吼）→ 1.45～1.62 俯冲扎进胸口 → 余光
		float rise = Ease5.OutQuint(Ease5.Seg(t, 0.16f, 0.88f));
		float dive = Ease5.Seg(t, 1.45f, 0.24f);
		dive = dive * dive * (1.6f - 0.6f * dive);
		float head = rise * _sHang + dive * (_sEntry - _sHang + BodyLen * T * 1.05f);
		float hang = Smooth(0.95f, 1.05f, t) * (1f - Smooth(1.42f, 1.48f, t));
		for (int k = 0; k < LightSerpent.N; k++)
		{
			float f = k / (LightSerpent.N - 1f);
			float s = head - BodyLen * T * (1f - f);
			Vector3 v = At(s);
			// 昂首时身体起伏（S 波沿身体往尾巴传），头微微往后蓄力
			float wave = Mathf.Sin(f * 9f - t * 11f) * 0.04f * T * hang * (1f - f * 0.6f);
			v += new Vector3(wave * 0.6f - hang * 0.06f * T * _face * f * f, wave, 0f);
			float a = Smooth(-0.15f * T, 0.2f * T, s) * (s > _sEntry ? 0f : 1f) * Smooth(0.16f, 0.22f, t);
			a *= 1f - Smooth(2.0f, 2.3f, t);
			_s.Spine[k] = new Vector4(v.X, v.Y, v.Z, a);
		}
		float eyes = Smooth(0.4f, 0.8f, t) * 0.4f + 0.6f * hang;
		float boost = hang * 1.0f + Mathf.Exp(-Mathf.Pow((t - 1.6f) / 0.08f, 2f));
		_s.Push(0.38f + 0.5f * hang, eyes, boost, 1f, _face);

		(Vector2 tip, Vector2 hd) = _s.Head();
		Vector2 headAt = _feet + tip;
		// 开场：脚下往里一收
		if (!_gathered)
		{
			_gathered = true;
			Glow.Gather(_back, _feet + new Vector2(0f, -0.05f * T), Pal5.Bright, 1.6f * T, 0.16f, 0.35f);
		}
		// 炸开
		if (!_burst && t >= 0.16f)
		{
			_burst = true;
			Glow.Flash(_back, _feet + new Vector2(0f, -0.1f * T), Pal5.Bright, 2.0f * T, 0.35f);
			Glow.Shockwave(_back, _feet, Pal5.Crimson, 3.4f * T, 0.55f, 0.28f);
			Glow.Sparks(_front, _feet + new Vector2(0.3f * T * _face, 0f), 36, 200f, 700f, 0.7f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 250f, 0.14f, 0.4f, Vector2.Up, 45f);
			Fx.Shake(1);
		}
		// 一路洒火星
		if (t > _nextSpark && t > 0.2f && t < 1.6f && _s.Spine[LightSerpent.N - 1].W > 0.5f)
		{
			_nextSpark = t + 0.045f;
			Glow.Sparks(_s.Spine[LightSerpent.N - 1].Z > 0f ? _front : _back, headAt, 6, 50f, 200f, 0.5f,
				new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, -60f, 0.12f, 0.32f, -hd, 70f);
		}
		// 吼：昂首定住的那一下
		if (!_roared && t >= 1.02f)
		{
			_roared = true;
			Glow.Flash(_front, headAt, Pal5.Bright, 1.6f * T, 0.35f);
			Glow.Flash(_front, headAt, Pal5.Hot, 0.7f * T, 0.2f);
			Glow.SpeedLines(_front, headAt, Pal5.Bright, 26, 0.45f * T, 2.4f * T);
			Fx.Shake(2);
			HitStop.Request(0.05);
		}
		// 扎进胸口
		if (!_struck && head >= _sEntry)
		{
			_struck = true;
			Vector2 chest = _feet + new Vector2(0.04f * T * _face, -0.72f * T);
			Glow.Flash(_front, chest, Pal5.Hot, 2.0f * T, 0.35f);
			Glow.Flash(_front, chest, Pal5.Bright, 3.0f * T, 0.5f);
			Glow.Shockwave(_back, _feet, Pal5.Crimson, 4.2f * T, 0.75f, 0.28f);
			Glow.Shockwave(_back, _feet, Pal5.Bright, 2.6f * T, 0.5f, 0.28f, 0.06f);
			Glow.SpeedLines(_front, chest, Pal5.Hot, 20, 0.5f * T, 2.2f * T);
			Glow.Sparks(_front, chest, 60, 250f, 900f, 0.8f, new[] { Pal5.Hot, Pal5.Gold, Pal5.Bright, Pal5.Deep }, 350f, 0.15f, 0.5f, Vector2.Up, 170f);
			Fx.Shake(3);
			HitStop.Request(0.07);
		}
		// Joker 身上：扎进去的一下亮红，再退
		if (_selfTint != null && t > 1.55f)
		{
			float k = Smooth(1.58f, 1.64f, t) * (1f - Smooth(1.7f, 2.2f, t));
			_selfTint(new Color(1f + 0.5f * k, 1f - 0.12f * k, 1f - 0.12f * k));
		}
	}
}
