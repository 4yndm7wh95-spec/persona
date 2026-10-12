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
		// 画面本体放到"人物身后特效层"的最底下（第 0 个子节点）：天色和月亮只压住战斗背景，
		// 之后在身后层出现的特效（天鹅绒门、塔罗牌、法阵……）都画在它上面，不会被盖住。
		// root 仍挂在原版节点下，负责生命周期：原版节点释放时一起释放 vis。
		Control? backLayer = Makoto.FxLayer(back: true);
		Node2D vis = new() { Name = AuraName + "Vis" };
		if (backLayer != null)
		{
			backLayer.AddChild(vis);
			backLayer.MoveChild(vis, 0);
		}
		else
		{
			root.AddChild(vis);
		}
		vis.GlobalPosition = root.GlobalPosition;
		root.SetMeta("vis", vis);
		root.TreeExiting += () => { if (GodotObject.IsInstanceValid(vis)) vis.QueueFree(); };

		// 影时间的天色：盖在战斗背景上（人物之下），整屏暗绿 + 地面绿雾 + 月光光柱 + 暗角
		Vector2 view = creature.GetViewportRect().Size;
		Node2D skyRoot = new() { Name = "Sky", ShowBehindParent = true };
		vis.AddChild(skyRoot);
		ColorRect sky = new() { Material = new ShaderMaterial { Shader = Shaders.DarkSky }, Size = view * 3f, MouseFilter = Control.MouseFilterEnum.Ignore };
		skyRoot.AddChild(sky);
		sky.GlobalPosition = -view;
		((ShaderMaterial)sky.Material).SetShaderParameter("moon", vis.GlobalPosition / view);

		// 月亮：着色器画的黄绿色满月（环形山、球面明暗、亮边、两圈光环、飘过的云带）+ 外层柔光
		ColorRect halo = Fx.ShaderQuad(vis, Shaders.Flash, 900f);
		Fx.SetParam(halo, "tint", new Color(Pal.DarkHour, 0.45f));
		Fx.SetParam(halo, "alpha", 0.4f);
		ColorRect moonQuad = Fx.ShaderQuad(vis, Shaders.DarkMoon, 640f);
		Tween moonBreath = moonQuad.CreateTween().SetLoops();
		moonBreath.TweenProperty(moonQuad, "scale", Vector2.One * 1.015f, 2.4).SetTrans(Tween.TransitionType.Sine);
		moonBreath.TweenProperty(moonQuad, "scale", Vector2.One, 2.4).SetTrans(Tween.TransitionType.Sine);
		moonQuad.PivotOffset = moonQuad.Size / 2f;

		// 时钟：外圈 + 刻度 + 罗马数字，缓慢旋转；指针固定指向 XII
		Node2D clock = new() { Name = "Clock" };
		vis.AddChild(clock);
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
		Fx.Line(vis, new[] { Vector2.Zero, Vector2.Up * 175f }, new Color(Pal.Ice, 0.75f), 5f);
		Fx.Line(vis, new[] { Vector2.Zero, Vector2.Up * 130f }, new Color(Pal.Ice, 0.85f), 8f);

		// 上升的绿色光点
		Node2D motesAt = new() { Position = new Vector2(0f, 180f) };
		vis.AddChild(motesAt);
		GpuParticles2D motes = Fx.Sparks(motesAt, Pal.DarkHour, 40, 20f, 60f, 3.5f, gravity: -25f, scaleMin: 0.15f, scaleMax: 0.45f, oneShot: false);
		ParticleProcessMaterial pm = (ParticleProcessMaterial)motes.ProcessMaterial;
		pm.EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		pm.EmissionBoxExtents = new Vector3(240f, 40f, 0f);
		pm.Spread = 20f;

		// 呼吸般的明暗 + 每 3 秒一圈水波
		Tween pulse = vis.CreateTween().SetLoops();
		pulse.TweenProperty(vis, "self_modulate", new Color(1f, 1f, 1f, 0.8f), 1.6).SetTrans(Tween.TransitionType.Sine);
		pulse.TweenProperty(vis, "self_modulate", Colors.White, 1.6).SetTrans(Tween.TransitionType.Sine);
		Timer ripple = new() { WaitTime = 3.0, Autostart = true };
		vis.AddChild(ripple);
		ripple.Timeout += () => ModEntry.Safe("dark hour ripple", () =>
		{
			if (Makoto.FxLayer(back: true) is Control back && vis.IsVisibleInTree())
			{
				P3Fx.Ripple(back, vis.GlobalPosition, 640f, 1.4f, 0.02f);
			}
		});

		// 登场：从无到有放大显形
		vis.Modulate = new Color(1f, 1f, 1f, 0f);
		vis.Scale = Vector2.One * 0.6f;
		Tween intro = vis.CreateTween().SetParallel();
		intro.TweenProperty(vis, "modulate:a", 1f, 0.6).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
		intro.TweenProperty(vis, "scale", Vector2.One, 0.8).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
	}

	/// <summary>原版"激活 / 失效"切换时：调亮 / 调暗。</summary>
	public static void SetActive(Node2D vanilla, bool active)
	{
		if (vanilla.GetNodeOrNull<Node2D>(AuraName) is Node2D root && root.HasMeta("vis") && root.GetMeta("vis").AsGodotObject() is Node2D vis && GodotObject.IsInstanceValid(vis))
		{
			vis.CreateTween().TweenProperty(vis, "modulate:a", active ? 1f : 0.3f, 0.5);
		}
	}
}
