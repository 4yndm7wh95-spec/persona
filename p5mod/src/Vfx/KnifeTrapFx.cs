using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 刀刃陷阱（把消耗牌堆里的小刀全部对一个敌人打出）「子弹牢笼」
/// （用户 2026-11-13：原来太简单；第二次：五六颗光弹太简单；第三次：光球看不清、设计太小胆 → 更大胆、更华丽、范围更大）：
/// ① 布阵（0～0.35s）：枪口一闪，一大片红白疾光从枪口泼洒出去，各自落到敌人周围一个大球壳上（直径约 2.6 倍敌人身高），
///    弹头都对准它，悬停着慢慢浮动、闪；前半球在它身前、后半球在身后（远的暗一点细一点）。
///    同时它周围一大圈压暗透红，脚下红光呼吸，外面一层刀光笼子绕着转。
/// ② 收网：每一发小刀打中（GunFx.Hit），一批疾光同时射进它（闪光、网点光晕）。
/// ③ 收官：最后一发时剩下的全部一齐射进去 —— 超大网点光晕、十字光芒、大片碎光、重震。
/// ④ 3.8s 后没用完的淡掉。全部圆头的疾光和柔光，没有尖碎片、没有细线圈。
/// </summary>
public static class KnifeTrapFx
{
	private sealed class Shot
	{
		public Node2D F = null!, B = null!, GF = null!, GB = null!;
		public ShaderMaterial FM = null!, BM = null!;
		public Vector3 Dir;          // 球面上的单位方向（z > 0 = 朝向镜头 = 在敌人身前）
		public float Born, Phase, Len, Thick;
		public bool Used;
		public Vector2 Pos;
	}

	private static readonly List<Shot> _shots = new();
	private static Vector2 _center;
	private static float _h, _w, _r;
	private static Control? _front, _back;
	private static ulong _until;
	private static int _hitsLeft, _perHit;

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
		const float life = 3.8f, fly = 0.3f;
		_until = Stage.NowMs() + (ulong)(life * 1000f);
		count = Mathf.Clamp(count, 1, 12);
		int n = Mathf.Clamp(count * 4, 24, 44);
		_hitsLeft = count;
		_perHit = Mathf.Max(2, n / (count + 1));
		float R = Mathf.Max(th, tw) * 1.3f;
		_r = R;
		Vector2 feet = center + new Vector2(0f, th * 0.5f);
		Sfx.Take("gx_fan_open", 0.24f * 0.8f, 0f);
		Sfx.Take("gx_ping", 0.18f * 0.8f, fly);

		// 枪口一闪
		Glow.Flash(front, muzzle, new Color(1f, 0.92f, 0.9f), th * 0.45f, 0.16f);
		Glow.Flash(back, muzzle, new Color(0.9f, 0.1f, 0.15f), th * 1.1f, 0.28f);
		// 周围一大圈压暗透红
		SkillRedo5.Dim(back, center, new Vector2(R * 3.4f, R * 2.8f), life, s => Fx.OutCubic(Ease5.Seg(s, 0f, 0.3f)) * (1f - Ease5.Seg(s, life - 0.6f, 0.6f)));
		// 布阵那一下
		Fx.After(front, fly, () =>
		{
			Glow.Flash(back, center, new Color(0.9f, 0.1f, 0.15f), R * 2.6f, 0.36f);
			SkillRedo5.SoftBurst(back, center, R * 2.4f, 0.5f, new Color(0.85f, 0.05f, 0.12f));
			BodyTint.Pulse(body, new Color(0.62f, 0.38f, 0.42f), 0.1, 0.5);
			Fx.Shake(1);
		});
		// 脚下红光呼吸
		Node2D floor = Fx.Root(back, feet, "P5TrapFloor", life + 0.1);
		Sprite2D fs = Glow.SoftSprite(floor, Vector2.Zero, new Color(1f, 0.1f, 0.18f, 0f), 1f);
		fs.Scale = new Vector2(R * 2.6f, R * 0.5f) / 32f;
		Fx.Animate(floor, 0, life, t =>
		{
			float s = t * life;
			float a = Fx.OutCubic(Ease5.Seg(s, fly, 0.25f)) * (1f - Ease5.Seg(s, life - 0.5f, 0.5f));
			fs.Modulate = new Color(1f, 0.1f, 0.18f, (0.5f + 0.18f * Mathf.Sin(s * 4f)) * a);
		});
		// 外面一层刀光笼子
		SkillRedo5.TrapCage(front, back, feet, th, R * 0.95f, fly, life - fly);

		// 球壳上的疾光（斐波那契球面均匀分布）
		float golden = Mathf.Pi * (3f - Mathf.Sqrt(5f));
		for (int i = 0; i < n; i++)
		{
			float y = 1f - 2f * (i + 0.5f) / n;
			float rr = Mathf.Sqrt(1f - y * y);
			float th2 = golden * i;
			Vector3 d = new(Mathf.Cos(th2) * rr, y, Mathf.Sin(th2) * rr);
			bool white = i % 3 == 1;
			Shot s = new()
			{
				Dir = d, Born = 0.006f * i, Phase = Fx.Rand(0f, Mathf.Tau), Pos = muzzle,
				Len = th * Fx.Rand(0.5f, 0.75f), Thick = th * (white ? 0.09f : 0.19f),
			};
			Color tint = white ? new Color(1f, 0.85f, 0.85f) : new Color(0.95f, 0.06f, 0.15f);
			(s.F, s.FM) = SkillRedo5.Needle(front, tint, white ? 1.5f : 1.1f, life + 0.3);
			(s.B, s.BM) = SkillRedo5.Needle(back, tint, white ? 1.5f : 1.1f, life + 0.3);
			// 弹头一团柔光
			s.GF = Fx.Root(front, muzzle, "P5NeedleGlow", life + 0.3);
			s.GB = Fx.Root(back, muzzle, "P5NeedleGlow", life + 0.3);
			Glow.SoftSprite(s.GF, Vector2.Zero, new Color(1f, 0.15f, 0.22f, 0.8f), th * 0.3f / 32f);
			Glow.SoftSprite(s.GB, Vector2.Zero, new Color(1f, 0.15f, 0.22f, 0.8f), th * 0.3f / 32f);
			_shots.Add(s);
		}
		var shots = new List<Shot>(_shots);
		Node2D driver = Fx.Root(front, center, "P5TrapDriver", life + 0.1);
		Fx.Animate(driver, 0, life, t =>
		{
			float s = t * life;
			foreach (Shot o in shots)
			{
				if (o.Used || !GodotObject.IsInstanceValid(o.F)) continue;
				// 球壳慢慢转一点、每根浮一点
				float spin = s * 0.25f;
				Vector3 d = new(o.Dir.X * Mathf.Cos(spin) - o.Dir.Z * Mathf.Sin(spin), o.Dir.Y, o.Dir.X * Mathf.Sin(spin) + o.Dir.Z * Mathf.Cos(spin));
				float bob = 1f + 0.04f * Mathf.Sin(s * 3f + o.Phase);
				Vector2 slot = center + new Vector2(d.X, d.Y * 0.9f) * R * bob;
				float k = Ease5.Seg(s, o.Born, fly);
				float e = Fx.OutCubic(k);
				Vector2 mid = (muzzle + slot) * 0.5f + new Vector2(0f, -th * 0.4f);
				Vector2 pos = k < 1f ? muzzle.Lerp(mid, e).Lerp(mid.Lerp(slot, e), e) : slot;
				o.Pos = pos;
				// 方向：飞的时候顺着飞行方向，落定后弹头对准敌人
				Vector2 toC = center - pos;
				Vector2 flyDir = (mid.Lerp(slot, e) - muzzle.Lerp(mid, e));
				float ang = (k < 1f ? flyDir : toC).Angle();
				float proj = Mathf.Max(0.35f, new Vector2(d.X, d.Y).Length());   // 朝镜头的那几根看起来短
				float depth = 0.65f + 0.35f * (d.Z * 0.5f + 0.5f);
				bool fr = k < 1f || d.Z > 0f;
				float a = (s < o.Born ? 0f : Mathf.Min(1f, (s - o.Born) / 0.06f)) * (1f - Ease5.Seg(s, life - 0.4f, 0.4f));
				float tw2 = 0.85f + 0.15f * Mathf.Sin(s * 11f + o.Phase);
				foreach ((Node2D nd, ShaderMaterial m, bool on) in new[] { (o.F, o.FM, fr), (o.B, o.BM, !fr) })
				{
					nd.Visible = on && a > 0f;
					nd.GlobalPosition = pos;
					nd.Rotation = ang;
					nd.Scale = new Vector2(o.Len * (k < 1f ? 1f : proj) * depth, o.Thick * depth);
					m.SetShaderParameter("alpha", a * tw2 * depth);
					m.SetShaderParameter("flow", s * 6f + o.Phase);
				}
				foreach ((Node2D g, bool on) in new[] { (o.GF, fr), (o.GB, !fr) })
				{
					g.Visible = on && a > 0f;
					g.GlobalPosition = pos;
					g.Modulate = new Color(1f, 1f, 1f, a * tw2 * depth);
					g.Scale = Vector2.One * depth;
				}
			}
		});
	}

	/// <summary>一发小刀打中（GunFx.Hit）：陷阱还在、打的是这个敌人 → 一批疾光射进去；最后一发全部射进去收官。</summary>
	public static void OnHit(Vector2 at)
	{
		if (Stage.NowMs() > _until || _front == null || !GodotObject.IsInstanceValid(_front)) return;
		if (at.DistanceTo(_center) > _h * 1.2f) return;
		_hitsLeft--;
		bool last = _hitsLeft <= 0;
		var pick = new List<Shot>();
		foreach (Shot o in _shots)
		{
			if (o.Used || !GodotObject.IsInstanceValid(o.F)) continue;
			if (!last && pick.Count >= _perHit) break;
			pick.Add(o);
		}
		if (pick.Count == 0) return;
		Control f = _front;
		Control b = _back != null && GodotObject.IsInstanceValid(_back) ? _back : f;
		float h = _h, w = _w, R = _r;
		Vector2 c = _center;
		foreach (Shot o in pick)
		{
			o.Used = true;
			if (GodotObject.IsInstanceValid(o.GF)) o.GF.Visible = false;
			if (GodotObject.IsInstanceValid(o.GB)) o.GB.Visible = false;
			Vector2 from = o.Pos, to = c + (from - c) * 0.08f;
			Shot oo = o;
			bool fr = o.F.Visible;
			Node2D nd = fr ? o.F : o.B;
			ShaderMaterial m = fr ? o.FM : o.BM;
			float d0 = Fx.Rand(0f, last ? 0.05f : 0.03f);
			Fx.Animate(nd, d0, 0.1, t =>
			{
				if (!GodotObject.IsInstanceValid(nd)) return;
				nd.GlobalPosition = from.Lerp(to, t * t);
				nd.Scale = new Vector2(oo.Len * (1f + 0.6f * t), oo.Thick);
				m.SetShaderParameter("alpha", 1f - Ease5.Seg(t, 0.7f, 0.3f));
			});
		}
		Fx.After(f, 0.1, () =>
		{
			Glow.Flash(f, c, new Color(1f, 0.92f, 0.9f), h * 0.5f, 0.12f);
			Glow.Flash(b, c, new Color(0.9f, 0.1f, 0.15f), h * 1.2f, 0.24f);
			SkillRedo5.SoftBurst(b, c, h * 1.1f, 0.32f, new Color(0.85f, 0.05f, 0.12f));
			if (!last) return;
			Fx.Shake(3);
			Glow.Flash(f, c, new Color(1f, 0.95f, 0.92f), w * 1.2f, 0.18f);
			Glow.Flash(b, c, new Color(0.9f, 0.08f, 0.14f), R * 3f, 0.42f);
			SkillRedo5.SoftBurst(b, c, R * 3f, 0.6f, new Color(0.85f, 0.05f, 0.12f));
			SkillRedo5.Cross(f, c, R * 1.3f);
			float u = Fx.Screen(f).Y / 1080f;
			Glow.Sparks(f, c, 50, 220f * u, 800f * u, 0.6f, new[] { new Color(1f, 0.96f, 0.94f), new Color(1f, 0.22f, 0.3f), new Color(0.5f, 0.02f, 0.08f) },
				220f * u, 0.025f * u, 0.06f * u, Vector2.Up, 180f);
			_until = 0;
		});
	}

	private static void Clear()
	{
		foreach (Shot o in _shots)
		{
			if (GodotObject.IsInstanceValid(o.F)) o.F.QueueFree();
			if (GodotObject.IsInstanceValid(o.B)) o.B.QueueFree();
			if (GodotObject.IsInstanceValid(o.GF)) o.GF.QueueFree();
			if (GodotObject.IsInstanceValid(o.GB)) o.GB.QueueFree();
		}
		_shots.Clear();
	}
}
