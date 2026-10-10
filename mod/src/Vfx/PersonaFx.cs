using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// Persona 召唤（骨骼动画，tools/rig/build_persona.py 生成）。目前：出"君王之刃"时召唤 Thanatos ——
/// 在结城理身后升起显形（summon）→ 挥剑斩向目标（attack，slash 事件 = 命中帧）→ 上浮消散（dismiss）。
/// 外观：主体偏冷蓝、略透明；身后再叠一份同步播放的加色青光副本作为灵体光晕。
/// </summary>
public static class PersonaFx
{
	private const string Thanatos = "res://p3makoto/persona/thanatos_skel_data.tres";
	private const float FigureHeight = 1440f;          // 立绘在骨骼单位下的高度（原图像素）

	public static void SummonThanatos(NCreature caster, NCreature? target)
	{
		Control? back = Makoto.FxLayer(back: true);
		Control? front = Makoto.FxLayer();
		if (back == null || front == null || ResourceLoader.Load(Thanatos) is not Resource data)
		{
			return;
		}
		Vector2 feet = Makoto.Feet(caster);
		Vector2 center = Makoto.Center(caster);
		float height = Mathf.Max(260f, (feet.Y - center.Y) * 2.1f);
		float scale = height * 1.55f / FigureHeight;
		// 骨骼原点在 Thanatos 的腰部：放在结城理身后偏左上方，悬浮在他头顶附近
		Node2D root = Fx.Root(back, new Vector2(center.X - height * 0.35f, center.Y - height * 0.25f), "P3Thanatos");
		root.Scale = Vector2.One * scale;

		Node2D? glow = Sprite(root, data, new Color(Pal.Cyan, 0.45f), additive: true);
		if (glow != null)
		{
			glow.Scale = Vector2.One * 1.035f;
		}
		Node2D? body = Sprite(root, data, new Color(0.78f, 0.86f, 1f, 0.93f), additive: false);
		if (body == null)
		{
			root.QueueFree();
			return;
		}
		// 显形时脚下一圈涟漪 + 光点
		P3Fx.Ripple(back, root.GlobalPosition, 560f, 0.6f, 0.025f);
		Node2D motes = Fx.Root(front, root.GlobalPosition, "P3ThanatosMotes");
		Fx.Sparks(motes, Pal.Cyan, 40, 120f, 420f, 0.7f, gravity: -60f);
		Fx.FreeAfter(motes, 1.0);

		Play(glow, null);
		Play(body, () => OnSlash(front, target));
		Fx.FreeAfter(root, 0.22 + 0.55 + 0.4 + 0.3);
	}

	private static Node2D? Sprite(Node2D parent, Resource data, Color modulate, bool additive)
	{
		if (ClassDB.Instantiate("SpineSprite").AsGodotObject() is not Node2D spine)
		{
			return null;
		}
		parent.AddChild(spine);
		spine.Set("skeleton_data_res", data);
		spine.Modulate = modulate;
		if (additive)
		{
			spine.Set("normal_material", Fx.Additive);
		}
		return spine;
	}

	private static void Play(Node2D? spine, System.Action? onSlash, int triesLeft = 20)
	{
		if (spine == null || !GodotObject.IsInstanceValid(spine))
		{
			return;
		}
		MegaSprite sprite = new(spine);
		if (sprite.TryGetAnimationState() is not MegaAnimationState state)
		{
			if (triesLeft > 0)
			{
				Fx.After(spine, 0.016, "persona spine", () => Play(spine, onSlash, triesLeft - 1));
			}
			return;
		}
		state.SetAnimation("summon", loop: false);
		state.AddAnimation("attack", 0f, loop: false);
		state.AddAnimation("dismiss", 0f, loop: false);
		if (onSlash != null)
		{
			sprite.ConnectAnimationEvent(Callable.From<GodotObject, GodotObject, GodotObject, GodotObject>((_, _, _, ev) =>
				ModEntry.Safe("persona event", () =>
				{
					if (new MegaEvent(ev).GetData().GetEventName() == "slash")
					{
						onSlash();
					}
				})));
		}
	}

	/// <summary>斩击命中帧：目标身上十字斩 + 冲击波 + 顿帧 + 震屏。</summary>
	private static void OnSlash(Control front, NCreature? target)
	{
		Vector2 at = target != null && GodotObject.IsInstanceValid(target)
			? target.VfxSpawnPosition
			: front.GetViewportRect().Size * new Vector2(0.68f, 0.45f);
		P3Fx.CrossSlash(front, at, Pal.Royal, 260f);
		P3Fx.Shockwave(front, at, Pal.Cyan, 620f, 0.5f);
		P3Fx.SpeedLines(front, at, Pal.Ice, 16, 60f, 420f, 0.35f);
		EvokerShot.HitStop(0.06);
		NGame.Instance?.ScreenShake(ShakeStrength.Medium, ShakeDuration.Short);
	}
}
