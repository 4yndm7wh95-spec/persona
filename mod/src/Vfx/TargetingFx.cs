using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 预备出牌（拖牌选目标）期间的常驻表现：
/// 脚下法阵 + 周身持续飘起蝴蝶 + 角色进入预备姿势；悬停敌人时显示锁定框。
/// 全局只存在一份，Begin/End 都是幂等的。
/// </summary>
public static class TargetingFx
{
	private static Node2D? _aura;
	private static Node2D? _reticle;
	private static NCreature? _caster;
	private static NCreature? _hovered;

	public static bool Active => _aura != null && GodotObject.IsInstanceValid(_aura);

	public static void Begin(CardModel card)
	{
		if (Active || !Makoto.Is(card.Owner))
		{
			return;
		}
		NCreature? node = Makoto.Node(card.Owner);
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (node == null || back == null || front == null)
		{
			return;
		}
		_caster = node;
		bool attack = card.Type == CardType.Attack;
		Color tint = card.Type == CardType.Power ? Pal.DarkHour : Pal.Blue;

		_aura = Fx.Root(back, Makoto.Feet(node), "P3TargetingAura");
		ColorRect circle = Fx.ShaderQuad(_aura, Shaders.MagicCircle, 360f, 0.3f);
		Fx.SetParam(circle, "tint", tint);
		Fx.SetParam(circle, "spin", attack ? 0.7f : 0.35f);
		_aura.Scale = Vector2.One * 0.4f;
		_aura.Modulate = new Color(1f, 1f, 1f, 0f);
		Tween grow = _aura.CreateTween().SetParallel();
		grow.TweenProperty(_aura, "scale", Vector2.One, 0.3f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
		grow.TweenProperty(_aura, "modulate:a", 1f, 0.2f);

		// 周身上升的光点
		GpuParticles2D motes = Fx.Sparks(_aura, tint, 30, 20f, 60f, 1.4f, gravity: -70f, scaleMin: 0.1f, scaleMax: 0.3f, oneShot: false);
		((ParticleProcessMaterial)motes.ProcessMaterial).EmissionShape = ParticleProcessMaterial.EmissionShapeEnum.Box;
		((ParticleProcessMaterial)motes.ProcessMaterial).EmissionBoxExtents = new Vector3(110f, 10f, 0f);

		// 每隔一段时间从脚边放飞一只蝴蝶
		Timer timer = new Timer { WaitTime = 0.45, Autostart = true };
		_aura.AddChild(timer);
		Vector2 feet = Makoto.Feet(node);
		Vector2 head = Makoto.Head(node);
		timer.Timeout += () => ModEntry.Safe("aura butterfly", () =>
		{
			Vector2 from = feet + new Vector2(Fx.Rand(-90f, 90f), Fx.Rand(-10f, 0f));
			Vector2 to = new Vector2(from.X + Fx.Rand(-40f, 60f), head.Y - Fx.Rand(20f, 120f));
			P3Fx.Butterfly(front, from, to, Fx.Rand(1.4f, 2.0f), Fx.Rand(0.55f, 0.85f), tint);
		});

		Pose.Enter(node, attack ? Pose.ReadySword : Pose.Aim);
	}

	public static void Hover(NCreature target)
	{
		if (!Active || target == _hovered)
		{
			return;
		}
		Unhover();
		Control? front = Makoto.FxLayer();
		if (front == null)
		{
			return;
		}
		_hovered = target;
		_reticle = P3Fx.Reticle(front, target.VfxSpawnPosition);
	}

	public static void Unhover()
	{
		P3Fx.Dismiss(_reticle, 0.12f);
		_reticle = null;
		_hovered = null;
	}

	public static void End()
	{
		Unhover();
		if (_aura != null && GodotObject.IsInstanceValid(_aura))
		{
			Node2D aura = _aura;
			Tween shrink = aura.CreateTween().SetParallel();
			shrink.TweenProperty(aura, "scale", Vector2.One * 0.6f, 0.2f);
			shrink.TweenProperty(aura, "modulate:a", 0f, 0.2f);
			shrink.Chain().TweenCallback(Callable.From(aura.QueueFree));
		}
		_aura = null;
		if (_caster != null && GodotObject.IsInstanceValid(_caster))
		{
			Pose.Exit(_caster);
		}
		_caster = null;
	}
}
