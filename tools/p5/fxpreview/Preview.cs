using System;
using System.Collections.Generic;
using Godot;
using P5Joker;

/// <summary>
/// 假的战斗场景（1920x1080，和游戏里一样大）：左边 Joker（待机帧，按游戏里的大小），右边一个敌人剪影，
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

	private static readonly Dictionary<string, Action<Preview>> Scenarios = new()
	{
		["damage"] = p =>
		{
			p.At(0.3, () => Popups.Damage(p._front, 1, Enemy, 7, false));
			p.At(1.4, () => Popups.Damage(p._front, 1, Enemy, 24, false));
			p.At(2.5, () => Popups.Damage(p._front, 2, Enemy2, 13, true));
			p.At(3.4, () => Popups.Damage(p._front, 1, Enemy, 158, false));
		},
		["multihit"] = p =>
		{
			for (int i = 0; i < 4; i++)
			{
				p.At(0.3 + 0.18 * i, () => Popups.Damage(p._front, 1, Enemy, 4, false));
			}
			for (int i = 0; i < 3; i++)
			{
				p.At(1.8 + 0.22 * i, () => Popups.Damage(p._front, 1, Enemy, 9, true));
			}
		},
		["block"] = p =>
		{
			p.At(0.3, () => Popups.Block(p._front, Chest));
			for (int i = 0; i < 3; i++)
			{
				p.At(1.7 + 0.25 * i, () => Popups.Block(p._front, Chest));
			}
		},
	};

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
		Texture2D? joker = Stage.LoadTexture("_joker");
		if (joker != null)
		{
			Sprite2D s = new() { Texture = joker, Centered = false, Scale = Vector2.One * 0.87f * 0.29f };
			s.Position = Pose(0f, 0f);
			_world.AddChild(s);
		}
		Enemy1(Enemy, 1f);
		Enemy1(Enemy2, 0.75f);
	}

	/// <summary>敌人剪影：一个深紫色的大块头（大小接近第一幕的普通怪），中心就是 VfxSpawnPosition。</summary>
	private void Enemy1(Vector2 center, float k)
	{
		Node2D n = new() { Position = center };
		_world.AddChild(n);
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
