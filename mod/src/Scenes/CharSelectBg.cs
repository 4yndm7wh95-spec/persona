using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using P3Makoto.Vfx;

namespace P3Makoto.Scenes;

/// <summary>
/// 选人界面背景（储君的 RegentBg 场景）：隐藏原版的星座王座，换成游戏内实时渲染的 P3R 风格画面 ——
/// 深蓝夜空与巨大的月亮、月光碎在水面上、斜切的 MEMENTO MORI 色带、上升的光点和蓝蝶，
/// 结城理本人用战斗骨骼站在水面上（带倒影），播放待机动画。
/// 原版脚本按名字取的 7 个悬停区域保留不动（它们只切换已隐藏的原版骨骼皮肤）。
/// </summary>
public static class CharSelectBg
{
	// 场景根 Control 的尺寸（tscn：offset -960,-521 .. 1600,679），本地 (960,521) 是屏幕中心
	private const float W = 2560f, H = 1200f;
	private static readonly Vector2 Feet = new(1200f, 985f);
	private const float CharScale = 0.6f;
	private const float HorizonY = 830f;

	public static void Build(Control bg)
	{
		foreach (Node c in bg.GetChildren())
		{
			if (c is Node2D n2)
			{
				n2.Visible = false;                     // 原版 SpineSprite、星星粒子
			}
		}

		Control root = new() { Name = "P3RBackground", MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(W, H) };
		bg.AddChild(root);
		bg.MoveChild(root, 0);

		const float pad = 300f;
		ShaderMaterial sky = new() { Shader = Shaders.Menu };
		Vector2 full = new(W + 2 * pad, H + 2 * pad);
		sky.SetShaderParameter("aspect", full.X / full.Y);
		sky.SetShaderParameter("moon", new Vector2((1640f + pad) / full.X, (250f + pad) / full.Y));
		sky.SetShaderParameter("horizon", (HorizonY + pad) / full.Y);
		root.AddChild(new ColorRect { Position = new Vector2(-pad, -pad), Size = full, Material = sky, MouseFilter = Control.MouseFilterEnum.Ignore });

		Band(root);
		Motes(root);
		Character(root, reflection: true);
		Character(root, reflection: false);
		Butterflies(root);
	}

	/// <summary>P3R 标志性的斜切色带 + 超大号描边字缓慢漂移。</summary>
	private static void Band(Control root)
	{
		Node2D band = new() { Name = "Band", Position = new Vector2(-200f, 700f), Rotation = Mathf.DegToRad(-9f) };
		root.AddChild(band);
		band.AddChild(new Polygon2D
		{
			Polygon = new[] { new Vector2(0, 0), new Vector2(1900, 0), new Vector2(1820, 150), new Vector2(-80, 150) },
			Color = new Color(Pal.Royal, 0.22f),
		});
		band.AddChild(new Polygon2D
		{
			Polygon = new[] { new Vector2(0, 160), new Vector2(1900, 160), new Vector2(1897, 166), new Vector2(-3, 166) },
			Color = new Color(Pal.Cyan, 0.5f),
			Material = Fx.Additive,
		});
		Label text = Typo.Text(band, "MEMENTO MORI   MEMENTO MORI   MEMENTO MORI", Typo.Latin, 130, new Color(Pal.Ice, 0.13f), new Vector2(-300f, -6f));
		Tween drift = text.CreateTween().SetLoops();
		drift.TweenProperty(text, "position:x", 0f, 26.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		drift.TweenProperty(text, "position:x", -300f, 26.0).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	/// <summary>水面上缓缓上升的光点。</summary>
	private static void Motes(Control root)
	{
		Node2D at = new() { Position = new Vector2(1250f, HorizonY + 120f) };
		root.AddChild(at);
		GpuParticles2D p = Fx.Sparks(at, Pal.Cyan, 70, 8f, 30f, 7f, gravity: -6f, scaleMin: 0.12f, scaleMax: 0.4f, oneShot: false);
		p.LocalCoords = true;
		ParticleProcessMaterial pm = (ParticleProcessMaterial)p.ProcessMaterial;
		pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		pm.EmissionBoxExtents = new Vector3(1100f, 160f, 0f);
		pm.Direction = new Vector3(0f, -1f, 0f);
		pm.Spread = 25f;
		pm.DampingMin = 0f;
		pm.DampingMax = 0f;
		p.Preprocess = 6.0;
	}

	/// <summary>结城理（战斗骨骼，待机动画）；reflection = 水中倒影（上下翻转、淡蓝、半透明）。</summary>
	private static void Character(Control root, bool reflection)
	{
		if (ClassDB.Instantiate("SpineSprite").AsGodotObject() is not Node2D spine)
		{
			return;
		}
		spine.Name = reflection ? "MakotoReflection" : "Makoto";
		root.AddChild(spine);
		spine.Position = Feet;
		spine.Scale = new Vector2(CharScale, reflection ? -CharScale * 0.9f : CharScale);
		if (reflection)
		{
			spine.Modulate = new Color(0.45f, 0.7f, 1f, 0.22f);
		}
		spine.Set("skeleton_data_res", GD.Load<Resource>("res://animations/characters/regent/regent_skel_data.tres"));
		MegaSprite sprite = new(spine);
		PlayWhenReady(spine, sprite, 40);
	}

	private static void PlayWhenReady(Node2D spine, MegaSprite sprite, int triesLeft)
	{
		if (!GodotObject.IsInstanceValid(spine))
		{
			return;
		}
		if (sprite.TryGetAnimationState() is MegaAnimationState state)
		{
			state.SetAnimation("idle_loop");
			return;
		}
		if (triesLeft > 0)
		{
			Fx.After(spine, 0.05, "char select spine", () => PlayWhenReady(spine, sprite, triesLeft - 1));
		}
	}

	/// <summary>蓝蝶不时从水面飞起。</summary>
	private static void Butterflies(Control root)
	{
		Timer timer = new() { WaitTime = 2.4, Autostart = true };
		root.AddChild(timer);
		timer.Timeout += () => ModEntry.Safe("char select butterfly", () =>
		{
			if (!root.IsVisibleInTree())
			{
				return;
			}
			Vector2 origin = root.GlobalPosition;
			Vector2 from = origin + new Vector2(Fx.Rand(800f, 1600f), HorizonY + Fx.Rand(40f, 160f));
			Vector2 to = origin + new Vector2(Fx.Rand(900f, 1700f), Fx.Rand(120f, 520f));
			P3Fx.Butterfly(root, from, to, Fx.Rand(3.5f, 5f), Fx.Rand(0.6f, 1f));
		});
	}
}
