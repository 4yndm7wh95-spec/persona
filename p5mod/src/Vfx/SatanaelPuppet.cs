using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 撒旦耶尔的部件动画（华丽收场 / 大罪穿甲弹，用户 2026-11-08）。现在用的是动作原型的粗切图（tools/p5/satanael/proto_cut.py：
/// 身体 / 上臂 / 前臂 + 枪三张），Codex 的第 6 版正式分层到了再换。
/// 动作（不僵的关键：手臂两节、各部位错开几帧、快慢不匀）：
/// · 登场：从深渊里升上来；手臂垂着（上臂下放约 55°，前臂再垂 25°）；
/// · 抬枪：先往下沉一点（预备）→ 上臂先甩上来、前臂慢半拍跟上，枪口走弧线 → 冲过头 3～4° → 阻尼回正；蓄力时枪口越来越抖；
/// · 开枪后坐：0.06s 里前臂绕手肘往上翻约 40°（枪口甩向头那边）、上臂往上抬约 12°、上身后仰；停住带阻尼震颤；然后慢慢放回去、略微过头再回正。
/// 弹起极快、回落很慢，身体比手臂晚一点反应。
/// </summary>
public sealed class SatanaelPuppet
{
	public readonly Node2D Root;
	private readonly Node2D _body, _shoulder, _elbow, _muzzle;
	private readonly float _aim;              // 瞄准角（度，顺时针为正 = 往下），整条手臂绕肩膀转到这个角度指向敌人
	private readonly float _scale;
	private readonly Vector2 _home;

	// 原型切图里的关节点（像素，sat_*.png 1700×1800，见 art/p5/fx/v4/satanael/proto/proto.json）
	private static readonly Vector2 Shoulder = new(720f, 1000f), Elbow = new(805f, 1065f), Muzzle = new(1670f, 1080f), Hip = new(700f, 1500f),
		Head = new(660f, 800f);

	public static bool Available => Fx.Tex("sat_base") != null && Fx.Tex("sat_upper") != null && Fx.Tex("sat_fore") != null;

	/// <param name="parent">放在场景里天空和平台之间（屏幕坐标）</param>
	/// <param name="headAt">头在屏幕上的位置</param>
	/// <param name="scale">原型图像素 → 屏幕像素</param>
	/// <param name="target">瞄准的点（屏幕坐标，一般是敌人群中间）</param>
	public SatanaelPuppet(Node parent, Vector2 headAt, float scale, Vector2 target)
	{
		_scale = scale;
		Root = new Node2D { Name = "Satanael", Scale = Vector2.One * scale };
		parent.AddChild(Root);
		_home = headAt - Head * scale;
		Root.Position = _home;
		// 夜景冷光：先用 modulate 压成冷蓝、压低金色（光照着色器之后接）
		Root.Modulate = new Color(0.6f, 0.7f, 0.95f);
		_body = new Node2D { Position = Hip };
		Root.AddChild(_body);
		_body.AddChild(new Sprite2D { Texture = Fx.Tex("sat_base"), Centered = false, Position = -Hip });
		_shoulder = new Node2D { Position = Shoulder - Hip };
		_body.AddChild(_shoulder);
		_shoulder.AddChild(new Sprite2D { Texture = Fx.Tex("sat_upper"), Centered = false, Position = -Shoulder });
		_elbow = new Node2D { Position = Elbow - Shoulder };
		_shoulder.AddChild(_elbow);
		_elbow.AddChild(new Sprite2D { Texture = Fx.Tex("sat_fore"), Centered = false, Position = -Elbow });
		_muzzle = new Node2D { Position = Muzzle - Elbow };
		_elbow.AddChild(_muzzle);
		// 瞄准角：肩膀 → 目标的角度，减去画里手臂本来的角度（肩 → 枪口）
		Vector2 shoulderScreen = _home + Shoulder * scale;
		float drawn = Mathf.RadToDeg((Muzzle - Shoulder).Angle());
		_aim = Mathf.RadToDeg((target - shoulderScreen).Angle()) - drawn;
		_aim = Mathf.Clamp(_aim, -10f, 25f);
		Pose(0f, 55f, 25f, 0f, Vector2.Zero);
	}

	/// <summary>枪口现在的全局位置（特效层坐标）。</summary>
	public Vector2 MuzzleGlobal => _muzzle.GlobalPosition;

	/// <summary>枪口现在在屏幕上的位置。</summary>
	public Vector2 MuzzleScreen => Root.GetParent<Node2D>() is Node2D p ? p.GetGlobalTransform().AffineInverse() * _muzzle.GlobalPosition : _muzzle.GlobalPosition;

	/// <summary>枪管现在的朝向（屏幕上，单位向量）。</summary>
	public Vector2 BarrelDir => Vector2.FromAngle(_muzzle.GlobalRotation);

	private void Pose(float rise, float upper, float fore, float lean, Vector2 push)
	{
		Root.Position = _home + new Vector2(0f, rise) + push;
		_shoulder.RotationDegrees = _aim + upper;
		_elbow.RotationDegrees = fore;
		_body.RotationDegrees = lean;
	}

	private static float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp(x, 0f, 1f), 3f);
	private static float EaseInOut(float x) { x = Mathf.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }

	/// <summary>
	/// 按全局时刻 g（从出牌算起，秒）摆姿势。riseAt：开始升起；raiseAt：开始抬枪；fireAt：开枪；exitAt：开始退场。
	/// </summary>
	public void Update(float g, float riseAt, float raiseAt, float fireAt, float exitAt)
	{
		// ── 升起：从 360px 下面升上来（缓出），之后轻轻浮动
		float rise = 360f * (1f - EaseOutCubic((g - riseAt) / 0.75f)) / _scale;
		rise += 6f * Mathf.Sin(g * 1.7f) / _scale;
		if (g >= exitAt)
		{
			rise += 420f * Mathf.Pow(Mathf.Clamp((g - exitAt) / 0.6f, 0f, 1f), 2f) / _scale;      // 退场：沉回深渊
		}
		float alpha = Mathf.Clamp((g - riseAt) / 0.25f, 0f, 1f) * (1f - Mathf.Clamp((g - exitAt) / 0.35f, 0f, 1f));   // 跟场景一起收掉
		Root.Modulate = new Color(0.6f, 0.7f, 0.95f, alpha);

		// ── 手臂：上臂（相对瞄准角）、前臂（相对上臂）
		float upper = 55f + 1.5f * Mathf.Sin(g * 2.1f), fore = 25f + 2f * Mathf.Sin(g * 2.1f - 0.6f);
		float lean = 0f;
		Vector2 push = Vector2.Zero;
		float r = g - raiseAt;
		if (r >= 0f)
		{
			const float ant = 0.12f, swing = 0.3f, lag = 0.06f;
			// 上臂：沉 → 甩上来（冲过头 -4°）→ 阻尼回正
			if (r < ant)
			{
				upper = Mathf.Lerp(55f, 61f, EaseInOut(r / ant));
			}
			else if (r < ant + swing)
			{
				upper = Mathf.Lerp(61f, -4f, EaseOutCubic((r - ant) / swing));
			}
			else
			{
				float u = r - ant - swing;
				upper = -4f * Mathf.Exp(-u / 0.09f) * Mathf.Cos(u * 22f);
			}
			// 前臂：慢半拍跟上（跟随动作）
			float rf = r - lag;
			if (rf < ant)
			{
				fore = Mathf.Lerp(25f, 31f, EaseInOut(Mathf.Max(rf, 0f) / ant));
			}
			else if (rf < ant + swing)
			{
				fore = Mathf.Lerp(31f, -3f, EaseOutCubic((rf - ant) / swing));
			}
			else
			{
				float u = rf - ant - swing;
				fore = -3f * Mathf.Exp(-u / 0.1f) * Mathf.Cos(u * 20f);
			}
			// 蓄力：枪口越来越抖（越接近开枪越厉害）
			if (g < fireAt && r > ant + swing)
			{
				float k = Mathf.Clamp((g - (raiseAt + ant + swing)) / Mathf.Max(0.1f, fireAt - raiseAt - ant - swing), 0f, 1f);
				upper += (0.2f + 0.7f * k) * Mathf.Sin(g * 47f);
				fore += (0.2f + 0.9f * k) * Mathf.Sin(g * 61f + 1.3f);
			}
		}
		// ── 后坐：弹起极快（0.06s）、停住带阻尼震颤、慢慢放回（略过头）
		float f = g - fireAt;
		if (f >= 0f)
		{
			float kick;
			if (f < 0.06f)
			{
				kick = EaseOutCubic(f / 0.06f);
			}
			else if (f < 0.35f)
			{
				float u = f - 0.06f;
				kick = 1f + 0.06f * Mathf.Exp(-u / 0.08f) * Mathf.Sin(u * 60f);
			}
			else
			{
				float u = Mathf.Clamp((f - 0.35f) / 0.6f, 0f, 1f);
				float e = 1f - Mathf.Pow(1f - u, 3f);
				kick = 1f - e - 0.07f * Mathf.Sin(u * Mathf.Pi) * u;              // 落回去时微微过头
			}
			upper += -12f * kick;
			fore += -40f * kick;
			// 身体晚一点、轻一点地被往后推、上身后仰
			float kb = f < 0.03f ? 0f : kick * Mathf.Clamp((f - 0.03f) / 0.08f, 0f, 1f);
			lean = -2.5f * kb;
			push = new Vector2(-16f, -6f) * kb;
		}
		Pose(rise, upper, fore, lean, push);
	}
}
