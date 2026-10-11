using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// "虚空形态"开启后的常驻演出 —— 影时间（Dark Hour）：人物身后一轮巨大的黄绿色月亮、缓慢转动的影时间时钟
/// （罗马数字表盘，时针分针都指向 XII = 午夜零点）、不断上升的绿色光点，每隔几秒荡开一圈水波。
/// 挂在原版 NVoidFormVfx 节点下面（原版的子节点全部隐藏），所以生命周期（死亡、离开战斗时释放）跟原版一致；
/// 原版效果"用完 / 回合开始重新激活"时调用 SetActive，这里同步把整体调暗 / 调亮。
/// </summary>
public static class FormFx
{
	public const string AuraName = "P3DarkHourAura";

	public static void DarkHour(Node2D vanilla, NCreature creature)
	{
		foreach (Node c in vanilla.GetChildren())
		{
			if (c is CanvasItem ci)
			{
				ci.Visible = false;
			}
		}
		Node2D root = new() { Name = AuraName, ShowBehindParent = true };
		vanilla.AddChild(root);
		root.GlobalPosition = Makoto.Center(creature) + new Vector2(-20f, -90f);

		// 月亮：大圆盘 + 光晕（复用闪光着色器做柔和光晕，圆盘用多边形）
		ColorRect halo = Fx.ShaderQuad(root, Shaders.Flash, 760f);
		Fx.SetParam(halo, "tint", new Color(Pal.DarkHour, 0.5f));
		Fx.SetParam(halo, "alpha", 0.55f);
		Fx.Poly(root, Fx.Circle(150f, 64), new Color(0.86f, 0.95f, 0.62f, 0.55f));
		Fx.Poly(root, Fx.Circle(150f, 64), new Color(Pal.DarkHour, 0.18f), additive: true);

		// 时钟：外圈 + 刻度 + 罗马数字，缓慢旋转；指针固定指向 XII
		Node2D clock = new() { Name = "Clock" };
		root.AddChild(clock);
		Fx.Line(clock, Fx.Circle(235f, 96), new Color(Pal.DarkHour, 0.55f), 3f, closed: true);
		Fx.Line(clock, Fx.Circle(212f, 96), new Color(Pal.DarkHour, 0.3f), 1.5f, closed: true);
		string[] numerals = { "XII", "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI" };
		for (int i = 0; i < 12; i++)
		{
			Vector2 dir = Vector2.Up.Rotated(Mathf.Tau * i / 12f);
			Fx.Line(clock, new[] { dir * 214f, dir * (i % 3 == 0 ? 196f : 204f) }, new Color(Pal.DarkHour, 0.7f), i % 3 == 0 ? 3f : 1.5f);
			Node2D num = new() { Position = dir * 262f, Rotation = Mathf.Tau * i / 12f };
			clock.AddChild(num);
			Typo.Text(num, numerals[i], Typo.Latin, 26, new Color(Pal.DarkHour, 0.65f), new Vector2(-18f, -16f));
		}
		Tween spin = clock.CreateTween().SetLoops();
		spin.TweenProperty(clock, "rotation", Mathf.Tau, 90.0).AsRelative();
		Fx.Line(root, new[] { Vector2.Zero, Vector2.Up * 175f }, new Color(Pal.Ice, 0.75f), 5f);
		Fx.Line(root, new[] { Vector2.Zero, Vector2.Up * 130f }, new Color(Pal.Ice, 0.85f), 8f);

		// 上升的绿色光点
		Node2D motesAt = new() { Position = new Vector2(0f, 180f) };
		root.AddChild(motesAt);
		GpuParticles2D motes = Fx.Sparks(motesAt, Pal.DarkHour, 40, 20f, 60f, 3.5f, gravity: -25f, scaleMin: 0.15f, scaleMax: 0.45f, oneShot: false);
		ParticleProcessMaterial pm = (ParticleProcessMaterial)motes.ProcessMaterial;
		pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		pm.EmissionBoxExtents = new Vector3(240f, 40f, 0f);
		pm.Spread = 20f;

		// 呼吸般的明暗 + 每 3 秒一圈水波
		Tween pulse = root.CreateTween().SetLoops();
		pulse.TweenProperty(root, "self_modulate", new Color(1f, 1f, 1f, 0.8f), 1.6).SetTrans(Tween.TransitionType.Sine);
		pulse.TweenProperty(root, "self_modulate", Colors.White, 1.6).SetTrans(Tween.TransitionType.Sine);
		Timer ripple = new() { WaitTime = 3.0, Autostart = true };
		root.AddChild(ripple);
		ripple.Timeout += () => ModEntry.Safe("dark hour ripple", () =>
		{
			if (Makoto.FxLayer(back: true) is Control back && root.IsVisibleInTree())
			{
				P3Fx.Ripple(back, root.GlobalPosition, 640f, 1.4f, 0.02f);
			}
		});

		// 登场：从无到有放大显形
		root.Modulate = new Color(1f, 1f, 1f, 0f);
		root.Scale = Vector2.One * 0.6f;
		Tween intro = root.CreateTween().SetParallel();
		intro.TweenProperty(root, "modulate:a", 1f, 0.6).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		intro.TweenProperty(root, "scale", Vector2.One, 0.8).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	/// <summary>原版"激活 / 失效"切换时：调亮 / 调暗。</summary>
	public static void SetActive(Node2D vanilla, bool active)
	{
		if (vanilla.GetNodeOrNull<Node2D>(AuraName) is Node2D root)
		{
			root.CreateTween().TweenProperty(root, "modulate:a", active ? 1f : 0.3f, 0.5);
		}
	}
}
