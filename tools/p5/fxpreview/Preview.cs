using System;
using System.Collections.Generic;
using Godot;
using P5Joker;

/// <summary>
/// 假的战斗场景（1920x1080，和游戏里一样大）：左边 Joker（真的骨骼动画，SpinePlayer 读 art/p5/rig/joker.*，按游戏里的大小），右边一个敌人剪影，
/// 前景一层 Control 当 Stage.Front。按 --scenario=<名字> 在固定时间点放特效，render.sh 用 --write-movie 录下来。
/// </summary>
public partial class Preview : Node2D
{
	// 游戏里的大致站位：Joker 脚底、敌人中心（VfxSpawnPosition）
	public static readonly Vector2 JokerFeet = new(430f, 830f);
	public static readonly Vector2 Enemy = new(1420f, 600f);
	public static readonly Vector2 Enemy2 = new(1150f, 640f);
	// 姿势图像素 → 屏幕：ORIGIN (815,1622)，K 0.87 × Visuals 缩放 0.29（tools/p5/build_rig_p5.py、p5mod/src/Joker.cs）
	public static Vector2 Pose(float x, float y) => JokerFeet + new Vector2(x - 815f, y - 1622f) * 0.87f * 0.29f;
	public static Vector2 Chest => Pose(820f, 600f);

	private Node2D _world = null!;
	private Control _front = null!, _back = null!;
	private int _shake;
	private float _shakeLeft;
	private double _clock;

	private SpinePlayer? _joker;
	private readonly Dictionary<Vector2, Node2D> _enemies = new();

	// 游戏里的时序：打击 attack 动画开始后 0.15s 结算伤害（静默猎手 AttackAnimDelay），开枪 shiv 0.2s 后
	private static readonly Dictionary<string, Action<Preview>> Scenarios = new()
	{
		["damage"] = p =>
		{
			p.Strike(0.3, Enemy, 7);
			p.Strike(1.5, Enemy, 24);
			p.Shot(2.6, Enemy2, 13);
			p.Strike(3.6, Enemy, 158);
		},
		["multihit"] = p =>
		{
			p.At(0.3, () => p._joker?.Play("attack"));
			for (int i = 0; i < 4; i++)
			{
				p.Hit(0.45 + 0.18 * i, Enemy, 4, false);
			}
			for (int i = 0; i < 3; i++)
			{
				p.Shot(1.9 + 0.45 * i, Enemy, 9);
			}
		},
		// 开枪动作：选中小刀牌举枪瞄准 → 打出去 → 收枪；再来一张直接开枪（没先瞄准）
		// 开枪：选中小刀牌（转枪出枪、瞄准）→ 打出去 → 转枪收走；直接开一枪；转枪转到一半就打出（接着转完开火，p5mod/src/Aim.cs）
		["gun"] = p =>
		{
			p.At(0.3, () => { p._joker?.Play("aim_in"); p._joker?.Queue("aim_loop", true); });
			p.At(1.3, () => p._joker?.Play("shiv_from_aim"));
			p.At(1.48, () => HitFx.Muzzle(p._front, Muzzle));
			p.Hit(1.5, Enemy, 6, true);
			p.Shot(2.5, Enemy, 6);
			p.At(3.6, () => { p._joker?.Play("aim_in"); p._joker?.Queue("aim_loop", true); });
			float skip = 0f;
			p.At(3.67, () => { skip = p._joker!.Time; p._joker.PlayFrom("shiv", skip); p.At(0.18 - skip, () => HitFx.Muzzle(p._front, Muzzle)); });
			p.Hit(3.87, Enemy, 6, true);
		},
		// 打击转刀：直接打一刀；选中打击牌转刀起手停住 → 打出；转刀转到一半就打出（接着转完再砍）
		["knife"] = p =>
		{
			p.Strike(0.3, Enemy, 7);
			p.At(1.6, () => { p._joker?.Play("knife_in"); p._joker?.Queue("knife_loop", true); });
			p.At(2.6, () => p._joker?.Play("attack_from_ready"));
			p.At(2.73, () => HitFx.Swing(p._front, Pose(760f, 260f), Pose(600f, 850f), Pose(1376f, 450f)));
			p.Hit(2.75, Enemy, 12, false);
			p.At(3.8, () => { p._joker?.Play("knife_in"); p._joker?.Queue("knife_loop", true); });
			float skip = 0f;
			p.At(3.85, () => { skip = p._joker!.Time; p._joker.PlayFrom("attack", skip);
				p.At(Math.Max(0, 0.13 - skip), () => HitFx.Swing(p._front, Pose(760f, 260f), Pose(600f, 850f), Pose(1376f, 450f))); });
			p.Hit(4.0, Enemy, 9, false);
		},
		["block"] = p =>
		{
			p.EnemyAttack(0.3, Enemy);
			p.At(0.42, () => Popups.Block(p._front, Chest));
			for (int i = 0; i < 3; i++)
			{
				p.EnemyAttack(1.8 + 0.3 * i, Enemy);
				p.At(1.92 + 0.3 * i, () => Popups.Block(p._front, Chest));
			}
		},
	};

	// 和 p5mod/src/JokerFx.cs 一样的点和时刻
	private void Strike(double t, Vector2 enemy, int amount)
	{
		At(t, () => _joker?.Play("attack"));
		At(t + 0.13, () => HitFx.Swing(_front, Pose(760f, 260f), Pose(600f, 850f), Pose(1376f, 450f)));
		Hit(t + 0.15, enemy, amount, false);
	}

	private void Shot(double t, Vector2 enemy, int amount)
	{
		At(t, () => _joker?.Play("shiv"));
		At(t + 0.18, () => HitFx.Muzzle(_front, Muzzle));
		Hit(t + 0.2, enemy, amount, true);
	}

	public static Vector2 Muzzle => Pose(1347f, 306f);

	private void Hit(double t, Vector2 enemy, int amount, bool gun) => At(t, () =>
	{
		if (gun)
		{
			HitFx.Shot(_front, Muzzle, enemy);
		}
		else
		{
			HitFx.Slash(_front, enemy);
		}
		Popups.Damage(_front, enemy == Enemy ? 1ul : 2ul, enemy, amount, gun);
		if (_enemies.TryGetValue(enemy, out Node2D? n))
		{
			Tween tw = n.CreateTween();
			tw.TweenProperty(n, "position", enemy + new Vector2(28f, 0f), 0.04);
			tw.TweenProperty(n, "position", enemy, 0.25).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
	});

	/// <summary>敌人往左扑一下（0.12s 到最远），扑到的那一刻被 Joker 挡住。</summary>
	private void EnemyAttack(double t, Vector2 enemy) => At(t, () =>
	{
		if (_enemies.TryGetValue(enemy, out Node2D? n))
		{
			Tween tw = n.CreateTween();
			tw.TweenProperty(n, "position", enemy + new Vector2(-260f, 10f), 0.12).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
			tw.TweenProperty(n, "position", enemy, 0.35).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		}
	});

	public override void _Ready()
	{
		string scenario = "damage";
		foreach (string a in OS.GetCmdlineUserArgs())
		{
			if (a.StartsWith("--scenario="))
			{
				scenario = a["--scenario=".Length..];
			}
		}
		string texDir = ProjectSettings.GlobalizePath("res://.tex");
		Stage.LoadTexture = name =>
		{
			string path = $"{texDir}/{name}.png";
			return FileAccess.FileExists(path) ? ImageTexture.CreateFromImage(Image.LoadFromFile(path)) : null;
		};
		Stage.Report = (where, e) => GD.PrintErr($"[P5Joker] {where}: {e}");
		Stage.NowMs = () => (ulong)(_clock * 1000.0);
		Stage.Shake = s => { _shake = s; _shakeLeft = 0.25f; };

		_world = new Node2D();
		AddChild(_world);
		BuildBackdrop();
		_back = Layer();
		BuildActors();
		_front = Layer();
		Stage.Front = _front;
		Stage.Back = _back;

		if (!Scenarios.TryGetValue(scenario, out var run))
		{
			GD.PrintErr($"[P5Joker] unknown scenario {scenario}: {string.Join(", ", Scenarios.Keys)}");
			return;
		}
		run(this);
	}

	public override void _Process(double delta)
	{
		_clock += delta;
		if (_shakeLeft > 0f)
		{
			_shakeLeft -= (float)delta;
			float a = _shake * 4f * Mathf.Max(_shakeLeft / 0.25f, 0f);
			_world.Position = new Vector2((float)GD.RandRange(-a, a), (float)GD.RandRange(-a, a));
		}
		else
		{
			_world.Position = Vector2.Zero;
		}
	}

	public void At(double seconds, Action action) =>
		GetTree().CreateTimer(seconds).Timeout += () => Stage.Safe("scenario", action);

	private Control Layer()
	{
		Control c = new() { Size = new Vector2(1920f, 1080f), MouseFilter = Control.MouseFilterEnum.Ignore };
		_world.AddChild(c);
		return c;
	}

	/// <summary>暗色的室内背景 + 地面，颜色接近杀戮尖塔 2 第一幕的战斗场景，只为看清特效和人的关系。</summary>
	private void BuildBackdrop()
	{
		Polygon2D sky = new()
		{
			Polygon = new[] { new Vector2(0f, 0f), new Vector2(1920f, 0f), new Vector2(1920f, 1080f), new Vector2(0f, 1080f) },
			VertexColors = new[] { new Color("2a3340"), new Color("2a3340"), new Color("15191f"), new Color("15191f") },
		};
		_world.AddChild(sky);
		Polygon2D floor = new()
		{
			Polygon = new[] { new Vector2(0f, 820f), new Vector2(1920f, 820f), new Vector2(1920f, 1080f), new Vector2(0f, 1080f) },
			VertexColors = new[] { new Color("3a3528"), new Color("3a3528"), new Color("1d1a14"), new Color("1d1a14") },
		};
		_world.AddChild(floor);
	}

	private void BuildActors()
	{
		string rig = ProjectSettings.GlobalizePath("res://") + "../../../art/p5/rig/joker";
		_joker = SpinePlayer.Load(rig + ".spine-json", rig + ".atlas", rig + ".png");
		_joker.Position = JokerFeet;
		_joker.Scale = new Vector2(0.29f, -0.29f);
		_world.AddChild(_joker);
		Enemy1(Enemy, 1f);
		Enemy1(Enemy2, 0.75f);
	}

	/// <summary>敌人剪影：一个深紫色的大块头（大小接近第一幕的普通怪），中心就是 VfxSpawnPosition。</summary>
	private void Enemy1(Vector2 center, float k)
	{
		Node2D n = new() { Position = center };
		_world.AddChild(n);
		_enemies[center] = n;
		var pts = new Vector2[28];
		for (int i = 0; i < pts.Length; i++)
		{
			float a = Mathf.Tau * i / pts.Length;
			pts[i] = new Vector2(Mathf.Cos(a) * 135f, Mathf.Sin(a) * 170f + (Mathf.Sin(a) > 0f ? 40f : 0f)) * k;
		}
		n.AddChild(new Polygon2D { Polygon = pts, Color = new Color("4b2f5c") });
		n.AddChild(new Polygon2D { Polygon = Fx.Circle(16f * k, 16), Color = new Color("f2d14a"), Position = new Vector2(-45f, -60f) * k });
		n.AddChild(new Polygon2D { Polygon = Fx.Circle(16f * k, 16), Color = new Color("f2d14a"), Position = new Vector2(20f, -66f) * k });
	}
}
