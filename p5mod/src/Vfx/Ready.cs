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
	private BodyAura? _aura;
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
		// 预备层音效（很轻）：拿起牌一声切入提示；掏枪 / 转刀 / 面具点火各一声
		switch (stance)
		{
			case Stance.Gun: break; // Selected Shiv: silent; shot sound still plays on use.
			case Stance.Knife:
				// 转刀（用户：原来那声风声像空转；换成低沉的挥动。试过刀落回手里加一记金属声，用户说不需要）
				Sfx.Play(Sfx.SwishLow, 0.45f, 0.02f);
				break;
			case Stance.Mask: Sfx.Play(Sfx.PersonaFlame, 0.3f, 0.05f); break;
		}
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
	/// <summary>已经收掉了。</summary>
	public bool Ended => _ended;

	/// <summary>
	/// 毒性爆发（用户 2026-11-08）：扶面具的手猛地一攥 → 面具火被攥灭：往手心一缩、蓝焰被攥碎往四面八方甩出去、只剩几缕青烟。
	/// 不是普通打出那样一亮一胀。
	/// </summary>
	public void Snuff() => End(true, _pose(MaskHand));

	public void End(bool played) => End(played, null);

	private void End(bool played, Vector2? snuffAt)
	{
		if (_ended)
		{
			return;
		}
		_ended = true;
		if (snuffAt is Vector2 grip && _wall != null)
		{
			_wall.Snuff(grip);
		}
		else
		{
			_wall?.End(played);
		}
		_aura?.End(played);
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
		_wall = new PersonaMask(_backLayer, _frontLayer, _pose(MaskEyes), body * 0.24f, _facing, 0.08f * _facing);   // 收一点（用户：头大身小）
		// 身体轮廓的蓝焰去掉（用户 2026-11-04：火没跟着人物动作走——复制出来取轮廓的骨骼停在扶面具的姿势；只留面具火）
		// 手上只留一点蓝光（指缝里的两小簇火像两块贴图，用户 2026-10-29 嫌难看，删了；火交给面具和轮廓）
		Sprite2D halo = Glow.SoftSprite(_root, hand + new Vector2(10f * _facing, -18f), PersonaMask.Hi, 1.5f);
		Pulse(halo, 0.3f, 0.6f, 0.4);
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
/// 锁定（拖着单体牌、指到一个敌人时）。2026-11-01 用户：太朴素 → P5 平面风：
/// · 原版 P5R 的箭头光标（BTL_BTN 矢印，白芯黑边）从右上方飞进来指着敌人、带一张红色错位影，沿指向一下下戳；
/// · 准星：四段黑描边的红色粗弧慢转，里面一圈白色细弧反着转；四个黑红三角从外面「咔」地收进来（过冲），之后轻轻呼吸；
/// · 一进来红网点往外扩一圈。全部硬边，不发光。指到别的敌人 / 松开就 Release()。
/// </summary>
public sealed class LockOn
{
	private readonly Node2D _root;
	private bool _done;

	private LockOn(Control layer, Vector2 at, float size, bool sound)
	{
		_root = new Node2D { Name = "P5LockOn", Position = at };
		layer.AddChild(_root);
		_root.GlobalPosition = at;
		if (sound) Sfx.Play(Sfx.Cursor, 0.45f); // Selecting a Shiv is silent, including target hover.
		float r = size * 0.42f;
		// 外圈：四段红色粗弧（黑描边），慢转
		Node2D outer = new();
		_root.AddChild(outer);
		for (int i = 0; i < 4; i++)
		{
			float a0 = i * Mathf.Pi / 2f + 0.22f, sweep = Mathf.Pi / 2f - 0.44f;
			Fx.Poly(outer, Ink.Arc(r, 16f, a0 - 0.03f, sweep + 0.06f), Ink.Black);
			Fx.Poly(outer, Ink.Arc(r, 7f, a0, sweep), Ink.Red);
		}
		// 内圈：八段白色细弧（黑描边），反着转
		Node2D inner = new();
		_root.AddChild(inner);
		for (int i = 0; i < 8; i++)
		{
			float a0 = i * Mathf.Tau / 8f, sweep = Mathf.Tau / 8f * 0.55f;
			Fx.Poly(inner, Ink.Arc(r * 0.8f, 8f, a0 - 0.02f, sweep + 0.04f, 8), Ink.Black);
			Fx.Poly(inner, Ink.Arc(r * 0.8f, 3f, a0, sweep, 8), Ink.White);
		}
		Tween spinO = outer.CreateTween().SetLoops();
		spinO.TweenProperty(outer, "rotation", Mathf.Tau, 7.0).AsRelative();
		Tween spinI = inner.CreateTween().SetLoops();
		spinI.TweenProperty(inner, "rotation", -Mathf.Tau, 5.0).AsRelative();
		Fx.Animate(_root, 0, 0.18, t =>
		{
			float k = Ease5.OutBack(t);
			outer.Scale = Vector2.One * Mathf.Lerp(1.6f, 1f, k);
			inner.Scale = Vector2.One * Mathf.Lerp(0.4f, 1f, k);
			outer.Modulate = inner.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 3f));
		});
		// 四个三角：从外面收进来，指着中心
		for (int i = 0; i < 4; i++)
		{
			Node2D arm = new() { Rotation = i * Mathf.Pi / 2f };
			_root.AddChild(arm);
			Node2D tri = new();
			arm.AddChild(tri);
			Vector2[] shape = { new(-24f, -13f), new(4f, 0f), new(-24f, 13f) };
			Fx.Poly(tri, Ink.Scale(shape, 1f, 5f), Ink.Black);
			Fx.Poly(tri, shape, Ink.Red);
			float r0 = r * 2.1f, r1 = r * 1.18f;
			Tween tw = arm.CreateTween();
			tw.TweenMethod(Callable.From<float>(t =>
			{
				tri.Position = new Vector2(-Mathf.Lerp(r0, r1, Ease5.OutBack(t)), 0f);
				arm.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 3f));
			}), 0f, 1f, 0.22);
			Tween breathe = arm.CreateTween().SetLoops();
			breathe.TweenInterval(0.22);
			breathe.TweenMethod(Callable.From<float>(t => tri.Position = new Vector2(-r1 - 7f * Mathf.Sin(t * Mathf.Tau), 0f)), 0f, 1f, 0.8);
		}
		// 原版箭头：在右上方，指向敌人（贴图里箭头朝右上，转 180° 朝左下）
		Node2D arrow = new() { Rotation = Mathf.Pi + 0.1f };
		_root.AddChild(arrow);
		Vector2 home = new Vector2(r * 1.25f, -r * 1.4f);
		Vector2 axis = (-home).Normalized();
		Sprite2D? red = Ink.Sprite(arrow, "ui_arrow", Ink.Red, 0f);
		Sprite2D? white = Ink.Sprite(arrow, "ui_arrow", Colors.White, 0f);
		float ak = Mathf.Clamp(size / 230f, 0.75f, 1.3f) * 1.05f;
		if (red != null && white != null)
		{
			red.Position = new Vector2(-9f, -9f);                    // 转了 180°：错位影落在右下
			red.Scale = white.Scale = Vector2.One * ak;
		}
		Tween fly = arrow.CreateTween();
		fly.TweenMethod(Callable.From<float>(t =>
		{
			arrow.Position = home - axis * 260f * (1f - Ease5.OutBack(t));
			arrow.Modulate = new Color(1f, 1f, 1f, Mathf.Min(1f, t * 4f));
		}), 0f, 1f, 0.22);
		Tween poke = arrow.CreateTween().SetLoops();
		poke.TweenInterval(0.22);
		poke.TweenMethod(Callable.From<float>(t => arrow.Position = home + axis * 14f * Mathf.Pow(Mathf.Sin(t * Mathf.Pi), 2f)), 0f, 1f, 0.55);
		Ink.Halftone(layer, at, r * 1.7f, 0.32f, 0f, null, 1f, 12f);
	}

	public static LockOn Start(Control layer, Vector2 at, float size = 230f, bool sound = true) => new(layer, at, size, sound);

	public void Release()
	{
		if (_done || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}
		_done = true;
		Tween tw = _root.CreateTween();
		tw.TweenProperty(_root, "modulate:a", 0f, 0.12);
		tw.Parallel().TweenProperty(_root, "scale", Vector2.One * 0.85f, 0.12);
		Fx.FreeAfter(_root, 0.2);
	}
}
