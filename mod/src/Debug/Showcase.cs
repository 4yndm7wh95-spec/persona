using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Godot;
using P3Makoto.Vfx;

namespace P3Makoto.Debug;

/// <summary>
/// 特效展示模式（离线录制用）。
/// 当 mod 文件夹里存在 showcase.txt 时启用：游戏启动约 10 秒后，在最上层盖一个模拟战斗场景，
/// 依次播放各个特效，按游戏时间每 1/30 秒截一帧（JPG）存到 mod 文件夹的 showcase_out/，然后自动退出游戏。
/// 录制期间把 Engine.TimeScale 降到 0.25，保证截图开销不影响特效的时间轴。
/// </summary>
public static class Showcase
{
	private const double StartDelay = 10.0;
	private const float TimeScale = 0.25f;
	private const double FrameStep = 1.0 / 30.0;

	private static string ModDir => Path.GetDirectoryName(typeof(ModEntry).Assembly.Location) ?? ".";

	public static void TryStart()
	{
		if (!File.Exists(Path.Combine(ModDir, "showcase.txt")))
		{
			return;
		}
		if (Engine.GetMainLoop() is not SceneTree tree)
		{
			GD.PrintErr($"{ModEntry.Tag} showcase: no SceneTree");
			return;
		}
		GD.Print($"{ModEntry.Tag} showcase armed, starting in {StartDelay}s");
		tree.CreateTimer(StartDelay).Timeout += () => _ = RunSafe(tree);
	}

	private static async Task RunSafe(SceneTree tree)
	{
		try
		{
			await Run(tree);
		}
		catch (Exception e)
		{
			GD.PrintErr($"{ModEntry.Tag} showcase failed: {e}");
			File.WriteAllText(Path.Combine(ModDir, "showcase_out", "ERROR.txt"), e.ToString());
		}
		finally
		{
			Engine.TimeScale = 1.0;
			tree.Quit();
		}
	}

	private sealed record Stage(Control Back, Control Front, Vector2 Head, Vector2 Feet, Vector2 Enemy);

	private static async Task Run(SceneTree tree)
	{
		string outRoot = Path.Combine(ModDir, "showcase_out");
		Directory.CreateDirectory(outRoot);

		Vector2 size = tree.Root.GetVisibleRect().Size;
		CanvasLayer layer = new CanvasLayer { Layer = 120 };
		tree.Root.AddChild(layer);
		Stage stage = BuildStage(layer, size);

		var shots = new List<(string name, double seconds, Action<Stage> play)>
		{
			("01_evoker", 1.8, s => P3Fx.EvokerBurst(s.Front, s.Head)),
			("02_targeting", 3.4, PlayTargeting),
			("03_attack", 2.0, s =>
			{
				P3Fx.EvokerBurst(s.Front, s.Head);
				s.Front.GetTree().CreateTimer(0.2).Timeout += () => P3Fx.CrossSlash(s.Front, s.Enemy, Pal.Cyan);
			}),
			("04_power", 2.2, s =>
			{
				P3Fx.EvokerBurst(s.Front, s.Head, 1.3f, Pal.DarkHour);
				P3Fx.PowerUp(s.Front, s.Back, s.Feet, s.Head);
			})
		};

		Engine.TimeScale = TimeScale;
		foreach (var shot in shots)
		{
			GD.Print($"{ModEntry.Tag} showcase recording {shot.name}");
			await Record(tree, stage, Path.Combine(outRoot, shot.name), shot.seconds, shot.play);
			await Wait(tree, 0.4);
		}
		File.WriteAllText(Path.Combine(outRoot, "DONE.txt"), DateTime.Now.ToString("O"));
		GD.Print($"{ModEntry.Tag} showcase done");
	}

	/// <summary>预备出牌：法阵展开 → 0.9s 后锁定敌人 → 2.5s 取消锁定 → 2.7s 收起法阵。</summary>
	private static void PlayTargeting(Stage s)
	{
		Node2D aura = P3Fx.Aura(s.Back, s.Front, s.Feet, s.Head, Pal.Cyan, 0.7f);
		Node2D? reticle = null;
		SceneTree tree = s.Front.GetTree();
		tree.CreateTimer(0.9).Timeout += () => reticle = P3Fx.Reticle(s.Front, s.Enemy);
		tree.CreateTimer(2.5).Timeout += () => P3Fx.Dismiss(reticle, 0.15f);
		tree.CreateTimer(2.7).Timeout += () => P3Fx.Dismiss(aura, 0.25f);
	}

	private static async Task Record(SceneTree tree, Stage stage, string dir, double seconds, Action<Stage> play)
	{
		Directory.CreateDirectory(dir);
		StringBuilder index = new StringBuilder("frame,game_ms\n");
		play(stage);
		ulong startUsec = Time.GetTicksUsec();
		double nextShot = 0.0;
		int frame = 0;
		while (true)
		{
			await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
			double gameTime = (Time.GetTicksUsec() - startUsec) / 1_000_000.0 * TimeScale;
			if (gameTime > seconds)
			{
				break;
			}
			if (gameTime + 1e-6 < nextShot)
			{
				continue;
			}
			Image img = tree.Root.GetTexture().GetImage();
			img.Resize(960, 540, Image.Interpolation.Bilinear);
			img.SaveJpg(Path.Combine(dir, $"f{frame:D4}.jpg"), 0.88f);
			index.Append(frame).Append(',').Append((int)(gameTime * 1000)).Append('\n');
			frame++;
			nextShot += FrameStep;
		}
		File.WriteAllText(Path.Combine(dir, "index.csv"), index.ToString());
	}

	private static async Task Wait(SceneTree tree, double gameSeconds)
	{
		await tree.ToSignal(tree.CreateTimer(gameSeconds), SceneTreeTimer.SignalName.Timeout);
	}

	/// <summary>模拟战斗画面：深色渐变背景、网格（便于看出折射）、地平线、玩家和敌人的剪影。</summary>
	private static Stage BuildStage(CanvasLayer layer, Vector2 size)
	{
		Control root = new Control { Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
		layer.AddChild(root);

		TextureRect bg = new TextureRect
		{
			Size = size,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			Texture = new GradientTexture2D
			{
				Gradient = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { new Color("1b2440"), new Color("05070d") } },
				FillFrom = new Vector2(0.5f, 0f),
				FillTo = new Vector2(0.5f, 1f),
				Width = 64,
				Height = 64
			}
		};
		root.AddChild(bg);

		Node2D grid = new Node2D();
		root.AddChild(grid);
		for (float x = 0; x <= size.X; x += 80f)
		{
			Fx.Line(grid, new[] { new Vector2(x, 0f), new Vector2(x, size.Y) }, new Color(1f, 1f, 1f, 0.05f), 1f, additive: false);
		}
		for (float y = 0; y <= size.Y; y += 80f)
		{
			Fx.Line(grid, new[] { new Vector2(0f, y), new Vector2(size.X, y) }, new Color(1f, 1f, 1f, 0.05f), 1f, additive: false);
		}

		Control back = new Control { Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(back);

		float floorY = size.Y * 0.74f;
		Fx.Line(grid, new[] { new Vector2(0f, floorY), new Vector2(size.X, floorY) }, new Color(1f, 1f, 1f, 0.15f), 2f, additive: false);

		Vector2 feet = new Vector2(size.X * 0.27f, floorY);
		Vector2 enemyFeet = new Vector2(size.X * 0.72f, floorY);
		Node2D actors = new Node2D();
		root.AddChild(actors);
		// 玩家剪影：约等于储君的 230×335 包围盒
		Fx.Poly(actors, Capsule(feet, 150f, 335f), new Color("2a3550"));
		Fx.Poly(actors, Capsule(feet + new Vector2(10f, -265f), 70f, 70f), new Color("34405e"));
		// 敌人剪影
		Fx.Poly(actors, Capsule(enemyFeet, 260f, 240f), new Color("3a2a30"));

		Control front = new Control { Size = size, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(front);

		Vector2 head = feet + new Vector2(24f, -290f);
		Vector2 enemyCenter = enemyFeet + new Vector2(0f, -120f);
		return new Stage(back, front, head, feet, enemyCenter);
	}

	private static Vector2[] Capsule(Vector2 bottomCenter, float width, float height)
	{
		List<Vector2> pts = new List<Vector2>();
		float r = width / 2f;
		Vector2 topCenter = bottomCenter + new Vector2(0f, -height + r);
		for (int i = 0; i <= 12; i++)
		{
			float a = Mathf.Pi + Mathf.Pi * i / 12f;
			pts.Add(topCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
		}
		pts.Add(bottomCenter + new Vector2(r, 0f));
		pts.Add(bottomCenter + new Vector2(-r, 0f));
		return pts.ToArray();
	}
}
