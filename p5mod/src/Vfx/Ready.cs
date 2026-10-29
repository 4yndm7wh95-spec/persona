using System;
using Godot;

namespace P5Joker;

/// <summary>拿起一张牌、还没打出去时 Joker 摆的架势（p5mod/src/Aim.cs 选、tools/p5/build_rig_p5.py 的 *_in / *_loop / *_out）。</summary>
public enum Stance
{
	Knife,      // 打击类攻击：腰侧握刀
	Gun,        // 小刀牌：举枪瞄准
	Mask,       // 技能（召唤 Persona）：手扣面具
	Guard,      // 格挡类：拳头半举
	Glove       // 能力 / 卡牌技巧：伸手去扯手套
}

/// <summary>
/// 预备出牌的特效（用户 2026-10-29：选中牌、拖着选目标时也要有表现）。一直循环到打出（End(true)：一闪收掉，交给出牌特效）
/// 或放回去（End(false)：慢慢熄掉）。全是实时渲染：着色器火焰、GPU 粒子、加法光。
/// 点都是姿势图（art/p5/poses/final）上量的像素，由调用方换算成屏幕坐标传进来（pose）。
/// </summary>
public sealed class ReadyFx
{
	// 姿势图上的点（各架势的定格帧）
	public static readonly Vector2 MaskHand = new(1000f, 285f), MaskFace = new(1010f, 215f), MaskEyes = new(1025f, 215f);   // summon_touch2_a
	public static readonly Vector2 GuardFist = new(1015f, 430f);                                   // guard_mid_a
	public static readonly Vector2 GloveCuff = new(1010f, 410f), GlovePinch = new(950f, 465f);     // glove_reach_a
	public static readonly Vector2 KnifeHilt = new(790f, 760f), KnifeTip = new(600f, 890f);        // knife_ready_b
	public static readonly Vector2 GunMuzzle = new(1347f, 306f), GunGrip = new(1275f, 345f);      // gun_aim_b
	public static readonly Vector2 Feet = new(815f, 1622f), Chest = new(820f, 600f);

	/// <summary>Persona 的蓝焰（P5 召唤时面具上烧起来的那种）。</summary>
	public static readonly Color BlueHot = new("e8fbff"), Blue = new("39b6ff"), BlueDeep = new("0b2a9e");

	private readonly Node2D _root, _back;
	private readonly Control _frontLayer, _backLayer;
	private PersonaMask? _wall;
	private readonly Stance _stance;
	private readonly Func<Vector2, Vector2> _pose;
	private readonly float _facing;
	private bool _ended;

	public Stance Stance => _stance;

	private ReadyFx(Control front, Control back, Stance stance, Func<Vector2, Vector2> pose, float facing)
	{
		_stance = stance;
		_frontLayer = front;
		_backLayer = back;
		_pose = pose;
		_facing = facing;
		_root = new Node2D { Name = "P5Ready" };
		front.AddChild(_root);
		_back = new Node2D { Name = "P5ReadyBack" };
		back.AddChild(_back);
		// 架势摆好之前别冒出来（*_in 约 0.15～0.25s）
		foreach (Node2D n in new[] { _root, _back })
		{
			n.Modulate = new Color(1f, 1f, 1f, 0f);
			Tween tw = n.CreateTween();
			tw.TweenInterval(stance is Stance.Mask ? 0.18 : 0.1);
			tw.TweenProperty(n, "modulate:a", 1f, 0.22).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
	}

	public static ReadyFx Start(Control front, Control back, Stance stance, Func<Vector2, Vector2> pose, float facing = 1f)
	{
		ReadyFx fx = new(front, back, stance, pose, facing);
		switch (stance)
		{
			case Stance.Mask:
				fx.BuildMask();
				break;
			case Stance.Guard:
				fx.BuildGuard();
				break;
			case Stance.Glove:
				fx.BuildGlove();
				break;
			case Stance.Knife:
				fx.BuildKnife();
				break;
			case Stance.Gun:
				fx.BuildGun();
				break;
		}
		return fx;
	}

	/// <summary>打出去了（played = true：亮一下立刻收，出牌特效接上）/ 放回去了（慢慢熄灭）。</summary>
	public void End(bool played)
	{
		if (_ended)
		{
			return;
		}
		_ended = true;
		_wall?.End(played);
		// 火焰着色器自己写颜色，不吃节点的 modulate：它们的透明度要单独淡掉（用户 2026-10-29：收手后火还留在半空）
		foreach (Node2D n in new[] { _root, _back })
		{
			if (GodotObject.IsInstanceValid(n))
			{
				FadeShaders(n, played ? 0.2f : 0.25f);
			}
		}
		foreach (Node2D n in new[] { _root, _back })
		{
			if (!GodotObject.IsInstanceValid(n))
			{
				continue;
			}
			foreach (Node c in n.GetChildren())
			{
				if (c is GpuParticles2D p)
				{
					p.Emitting = false;                           // 已经飞出去的粒子自己飘完
				}
			}
			Tween tw = n.CreateTween();
			if (played)
			{
				tw.TweenProperty(n, "modulate", new Color(1.8f, 1.8f, 1.8f, 1f), 0.04);
				tw.TweenProperty(n, "modulate", new Color(1f, 1f, 1f, 0f), 0.16);
			}
			else
			{
				tw.TweenProperty(n, "modulate:a", 0f, 0.28).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
			}
			Fx.FreeAfter(n, 0.9);
		}
	}

	// ───────────────────────── 手扣面具：蓝焰从指缝里漏出来 ─────────────────────────

	private void BuildMask()
	{
		Vector2 hand = _pose(MaskHand), face = _pose(MaskFace), feet = _pose(Feet);
		// 脸上戴着一张面具形状的蓝焰（人物前面，中心在鼻梁、眼睛那一行），两边尖角往上翘
		float body = (feet - face).Length();
		_wall = new PersonaMask(_backLayer, _frontLayer, _pose(MaskEyes), body * 0.42f, _facing, 0.08f * _facing);
		// 指缝里漏出来的蓝焰：同样的细丝火，小而亮，贴着手的外沿
		HandFire(hand + new Vector2(20f * _facing, -14f), 46f, 92f);
		HandFire(hand + new Vector2(-6f * _facing, -30f), 38f, 74f);
		Sprite2D halo = Glow.SoftSprite(_root, hand + new Vector2(10f * _facing, -18f), PersonaMask.Hi, 1.5f);
		Pulse(halo, 0.3f, 0.6f, 0.4);
	}

	private void HandFire(Vector2 foot, float w, float h)
	{
		ColorRect r = Flame.Rect(_root, foot, w, h);
		Flame.Palette(r, PersonaMask.Dark, PersonaMask.Mid, PersonaMask.Hi, PersonaMask.Hot);
		Flame.Set(r, "body", 0.35f);
		Flame.Set(r, "base_w", 0.32f);
		Flame.Set(r, "top_w", 0.04f);
		Flame.Set(r, "speed", 1.6f);
		Flame.Set(r, "rise", 0f);
		Fx.Animate(r, 0, 0.25, t => Flame.Set(r, "rise", Ease5.OutBack(t) * 0.95f));
	}

	// ───────────────────────── 半举的拳：红光在拳头上蓄着，前面一道光弧慢慢成形 ─────────────────────────

	private void BuildGuard()
	{
		Vector2 fist = _pose(GuardFist);
		Sprite2D glow = Glow.SoftSprite(_root, fist, Pal5.Bright, 2.4f);
		Pulse(glow, 0.45f, 0.9f, 0.5);
		Sprite2D core = Glow.SoftSprite(_root, fist, Pal5.Hot, 0.8f);
		Pulse(core, 0.5f, 1f, 0.5);
		// 身前一道竖着的细光弧（盾的轮廓）：一圈压扁的光环，慢慢转、明暗起伏
		Node2D arc = new() { Position = fist + new Vector2(60f * _facing, 10f) };
		_root.AddChild(arc);
		for (int i = 0; i < 2; i++)
		{
			ColorRect ring = Glow.RingRect(arc, 300f - i * 50f, 0.22f);
			ring.Rotation = Mathf.Pi / 2f;                       // 竖着：从侧面看过去的一面盾
			Glow.SetParam(ring, "tint", i == 0 ? Pal5.Bright : Pal5.Hot);
			Glow.SetParam(ring, "radius", 0.78f);
			Glow.SetParam(ring, "width", 0.012f);
			float phase = i * 0.5f;
			Tween loop = ring.CreateTween().SetLoops();
			loop.TweenMethod(Callable.From<float>(t => Glow.SetParam(ring, "alpha", 0.35f + 0.3f * Mathf.Sin((t + phase) * Mathf.Tau))), 0f, 1f, 0.9);
		}
		// 往上飘的红色火星 + 向拳头收拢的光点
		Embers(_root, fist, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 10, 22f, 0.7f, 40f, 110f);
		Inward(_root, fist, Pal5.Bright, 12, 110f, 0.45f);
	}

	// ───────────────────────── 扯手套：两只手之间噼啪的红色电弧 ─────────────────────────

	private void BuildGlove()
	{
		Vector2 cuff = _pose(GloveCuff), pinch = _pose(GlovePinch);
		Sprite2D glow = Glow.SoftSprite(_root, cuff, Pal5.Bright, 1.8f);
		Pulse(glow, 0.4f, 0.85f, 0.35);
		// 每 0.07s 换一道折线电弧（抖动的锯齿线），有时多分一叉
		Line2D bolt = Bolt(_root, Pal5.Hot, 2.2f), boltGlow = Bolt(_root, new Color(Pal5.Bright, 0.8f), 7f);
		Line2D fork = Bolt(_root, Pal5.Hot, 1.4f);
		Tween loop = bolt.CreateTween().SetLoops();
		loop.TweenCallback(Callable.From(() =>
		{
			Vector2[] pts = Zigzag(pinch, cuff, 6, 9f);
			bolt.Points = pts;
			boltGlow.Points = pts;
			bool on = Fx.Rng.NextDouble() < 0.8;
			bolt.Visible = boltGlow.Visible = on;
			fork.Visible = on && Fx.Rng.NextDouble() < 0.5;
			if (fork.Visible)
			{
				Vector2 from = pts[Fx.Rng.Next(1, pts.Length - 1)];
				fork.Points = Zigzag(from, from + new Vector2(Fx.Rand(-1f, 1f), Fx.Rand(-1f, -0.2f)) * 34f, 3, 6f);
			}
		}));
		loop.TweenInterval(0.07);
		Embers(_root, cuff, new[] { Pal5.Hot, Pal5.Bright }, 12, 20f, 0.45f, 50f, 160f, spread: 180f);
	}

	private static Line2D Bolt(Node parent, Color c, float w)
	{
		Line2D l = new() { DefaultColor = c, Width = w, Material = Fx.Additive, Antialiased = true, JointMode = Line2D.LineJointMode.Sharp };
		parent.AddChild(l);
		return l;
	}

	public static Vector2[] Zigzag(Vector2 a, Vector2 b, int segments, float amp)
	{
		Vector2[] pts = new Vector2[segments + 1];
		Vector2 n = (b - a).Orthogonal().Normalized();
		for (int i = 0; i <= segments; i++)
		{
			float k = (float)i / segments;
			pts[i] = a.Lerp(b, k) + (i == 0 || i == segments ? Vector2.Zero : n * Fx.Rand(-amp, amp));
		}
		return pts;
	}

	// ───────────────────────── 握刀：刀刃上一道红光、一点星光沿刃滑过 ─────────────────────────

	private void BuildKnife()
	{
		Vector2 hilt = _pose(KnifeHilt), tip = _pose(KnifeTip);
		Polygon2D edge = Glow.TaperPoly(_root, new Color(Pal5.Bright, 0.75f));
		edge.Polygon = Glow.TaperPts(tip, hilt, 7f);
		Pulse(edge, 0.35f, 0.9f, 0.6);
		Node2D glint = new();
		_root.AddChild(glint);
		Polygon2D star = Fx.Poly(glint, Glow.Star4(13f, 0.16f), Pal5.Hot, additive: true);
		Sprite2D halo = Glow.SoftSprite(glint, Vector2.Zero, Pal5.Bright, 0.9f);
		Tween loop = glint.CreateTween().SetLoops();
		loop.TweenMethod(Callable.From<float>(t =>
		{
			float k = Mathf.Clamp(t / 0.35f, 0f, 1f);
			glint.Position = hilt.Lerp(tip, Fx.OutCubic(k));
			float s = Mathf.Sin(k * Mathf.Pi);
			glint.Scale = Vector2.One * s;
			glint.Rotation = t * 3f;
			glint.Visible = t < 0.35f;
		}), 0f, 1f, 1.3);
	}

	// ───────────────────────── 举枪：枪口一点红光，隔一会儿一道光沿枪身划到枪口 ─────────────────────────

	private void BuildGun()
	{
		Vector2 muzzle = _pose(GunMuzzle), grip = _pose(GunGrip);
		Sprite2D flare = Glow.SoftSprite(_root, muzzle, Pal5.Bright, 0.9f);
		Pulse(flare, 0.4f, 0.9f, 0.35);
		Glow.SoftSprite(_root, muzzle, Pal5.Hot, 0.22f);
		Node2D glint = new();
		_root.AddChild(glint);
		Fx.Poly(glint, Glow.Star4(12f, 0.16f), Pal5.Hot, additive: true);
		Glow.SoftSprite(glint, Vector2.Zero, Pal5.Bright, 0.8f);
		Tween loop = glint.CreateTween().SetLoops();
		loop.TweenMethod(Callable.From<float>(t =>
		{
			float k = Mathf.Clamp(t / 0.3f, 0f, 1f);
			glint.Position = grip.Lerp(muzzle, Fx.OutCubic(k));
			glint.Scale = Vector2.One * Mathf.Sin(k * Mathf.Pi);
			glint.Rotation = t * 3f;
			glint.Visible = t < 0.3f;
		}), 0f, 1f, 1.2);
	}

	// ───────────────────────── 共用 ─────────────────────────

	/// <summary>节点下所有带着色器的 ColorRect：把着色器的 alpha 在 seconds 内淡到 0。</summary>
	private static void FadeShaders(Node node, float seconds)
	{
		foreach (Node c in node.GetChildren())
		{
			if (c is ColorRect { Material: ShaderMaterial mat } r)
			{
				float from = mat.GetShaderParameter("alpha").VariantType == Variant.Type.Float ? (float)mat.GetShaderParameter("alpha") : 1f;
				Tween tw = r.CreateTween();
				tw.TweenMethod(Callable.From<float>(a => mat.SetShaderParameter("alpha", a)), from, 0f, seconds);
			}
			FadeShaders(c, seconds);
		}
	}

	/// <summary>明暗起伏（一直循环）。</summary>
	private static void Pulse(CanvasItem item, float lo, float hi, double period)
	{
		Color c = item.Modulate;
		float phase = Fx.Rand(0f, 1f);
		Tween loop = item.CreateTween().SetLoops();
		loop.TweenMethod(Callable.From<float>(t => item.Modulate = new Color(c, c.A * Mathf.Lerp(lo, hi, 0.5f + 0.5f * Mathf.Sin((t + phase) * Mathf.Tau)))),
			0f, 1f, period);
	}

	/// <summary>一直往上飘的火星（GPU 粒子，持续发射）。rate = 每秒几颗。</summary>
	public static GpuParticles2D Embers(Node parent, Vector2 at, Color[] ramp, int rate, float radius, float life, float speedMin, float speedMax,
		float spread = 25f)
	{
		var offsets = new float[ramp.Length + 1];
		var colors = new Color[ramp.Length + 1];
		for (int i = 0; i < ramp.Length; i++)
		{
			offsets[i] = ramp.Length == 1 ? 0f : 0.8f * i / (ramp.Length - 1);
			colors[i] = ramp[i];
		}
		offsets[^1] = 1f;
		colors[^1] = new Color(ramp[^1], 0f);
		ParticleProcessMaterial pm = new()
		{
			Direction = new Vector3(0f, -1f, 0f),
			Spread = spread,
			InitialVelocityMin = speedMin,
			InitialVelocityMax = speedMax,
			Gravity = new Vector3(0f, -30f, 0f),
			ScaleMin = 0.08f,
			ScaleMax = 0.28f,
			DampingMin = 1f,
			DampingMax = 3f,
			EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Sphere,
			EmissionSphereRadius = radius,
			ColorRamp = new GradientTexture1D { Gradient = new Gradient { Offsets = offsets, Colors = colors } },
		};
		GpuParticles2D p = new()
		{
			Amount = Mathf.Max(2, (int)(rate * life)),
			Lifetime = life,
			ProcessMaterial = pm,
			Texture = Glow.Soft,
			Material = Fx.Additive,
			LocalCoords = false,
			Emitting = true,
			Position = at,
		};
		parent.AddChild(p);
		return p;
	}

	/// <summary>四周的光点一直往 at 收拢（蓄力）。</summary>
	private static void Inward(Node parent, Vector2 at, Color tint, int count, float radius, float period)
	{
		for (int i = 0; i < count; i++)
		{
			Sprite2D dot = Glow.SoftSprite(parent, at, tint, Fx.Rand(0.12f, 0.25f));
			float ang = Fx.Rand(0f, Mathf.Tau), r = radius * Fx.Rand(0.7f, 1.2f), off = (float)i / count;
			Tween loop = dot.CreateTween().SetLoops();
			loop.TweenMethod(Callable.From<float>(t =>
			{
				float k = (t + off) % 1f;
				dot.Position = at + Vector2.Right.Rotated(ang + k * 0.8f) * r * (1f - Fx.InCubic(k));
				dot.Modulate = new Color(tint, Mathf.Sin(k * Mathf.Pi));
			}), 0f, 1f, period * Fx.Rand(0.9f, 1.3f));
		}
	}

	private static void Set(ColorRect r, string name, Variant v) => ((ShaderMaterial)r.Material).SetShaderParameter(name, v);
}

/// <summary>
/// 锁定（拖着单体牌、指到一个敌人时）：P5 式的红色准星 —— 两圈细光环反向慢转，四个尖角从外面"咔"地收到敌人身上，中间一点热白。
/// 指到别的敌人 / 松开就 Release()。
/// </summary>
public sealed class LockOn
{
	private readonly Node2D _root;
	private bool _done;

	private LockOn(Control layer, Vector2 at, float size)
	{
		_root = new Node2D { Name = "P5LockOn", Position = at };
		layer.AddChild(_root);
		_root.GlobalPosition = at;
		for (int i = 0; i < 2; i++)
		{
			ColorRect ring = Glow.RingRect(_root, size * (i == 0 ? 1.15f : 0.9f), 1f);
			Glow.SetParam(ring, "tint", i == 0 ? Pal5.Bright : Pal5.Hot);
			Glow.SetParam(ring, "width", 0.012f);
			Glow.SetParam(ring, "alpha", 0f);
			int dir = i == 0 ? 1 : -1;
			Tween tw = ring.CreateTween();
			tw.TweenMethod(Callable.From<float>(t =>
			{
				Glow.SetParam(ring, "radius", Mathf.Lerp(0.98f, 0.8f, Ease5.OutExpo(t)));
				Glow.SetParam(ring, "alpha", (i == 0 ? 0.8f : 0.5f) * t);
			}), 0f, 1f, 0.22);
			Tween spin = ring.CreateTween().SetLoops();
			spin.TweenProperty(ring, "rotation", dir * Mathf.Tau, 6.0).AsRelative();
		}
		// 四个尖角（往里指的三角），收拢时带一点过冲
		for (int i = 0; i < 4; i++)
		{
			Node2D arm = new() { Rotation = Mathf.Pi / 4f + i * Mathf.Pi / 2f };
			_root.AddChild(arm);
			Polygon2D tri = Fx.Poly(arm, new[] { new Vector2(0f, -9f), new Vector2(26f, 0f), new Vector2(0f, 9f) }, Pal5.Bright, additive: true);
			Polygon2D hot = Fx.Poly(arm, new[] { new Vector2(4f, -3.5f), new Vector2(20f, 0f), new Vector2(4f, 3.5f) }, Pal5.Hot, additive: true);
			float r0 = size * 0.95f, r1 = size * 0.5f;
			Tween tw = arm.CreateTween();
			tw.TweenMethod(Callable.From<float>(t =>
			{
				float r = Mathf.Lerp(r0, r1, Ease5.OutBack(t));
				tri.Position = hot.Position = new Vector2(-r - 26f, 0f);
				arm.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 3f));
			}), 0f, 1f, 0.24);
			Tween breathe = arm.CreateTween().SetLoops();
			breathe.TweenInterval(0.24);
			breathe.TweenMethod(Callable.From<float>(t => tri.Position = hot.Position = new Vector2(-r1 - 26f - 6f * Mathf.Sin(t * Mathf.Tau), 0f)), 0f, 1f, 0.7);
		}
		Sprite2D center = Glow.SoftSprite(_root, Vector2.Zero, Pal5.Bright, 0.5f);
		Glow.SoftSprite(_root, Vector2.Zero, Pal5.Hot, 0.18f);
		Tween pop = center.CreateTween().SetLoops();
		pop.TweenProperty(center, "scale", Vector2.One * 0.7f, 0.35);
		pop.TweenProperty(center, "scale", Vector2.One * 0.45f, 0.35);
		Glow.Flash(layer, at, Pal5.Bright, size * 0.9f, 0.18f);
	}

	public static LockOn Start(Control layer, Vector2 at, float size = 230f) => new(layer, at, size);

	public void Release()
	{
		if (_done || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}
		_done = true;
		Tween tw = _root.CreateTween();
		tw.TweenProperty(_root, "modulate:a", 0f, 0.15);
		tw.Parallel().TweenProperty(_root, "scale", Vector2.One * 1.15f, 0.15);
		Fx.FreeAfter(_root, 0.2);
	}
}
