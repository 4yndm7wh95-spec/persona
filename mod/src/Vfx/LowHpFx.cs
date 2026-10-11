using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using P3Makoto.Core;

namespace P3Makoto.Vfx;

/// <summary>
/// 低血量（≤30%）"沉入水中"：画面边缘慢慢浮起深蓝暗角 + 焦散光纹（Shaders.Drown），随心跳轻轻增亮；
/// 越接近 0 越浓。回血到阈值以上时 0.8s 淡出。每帧轮询（StarOrbit 同款），不改游戏逻辑。
/// </summary>
public static class LowHpFx
{
	private const float Threshold = 0.3f;
	private static ColorRect? _overlay;
	private static float _level, _time;
	private static bool _hooked;

	public static void Install()
	{
		if (_hooked || Engine.GetMainLoop() is not SceneTree tree)
		{
			return;
		}
		_hooked = true;
		tree.ProcessFrame += () => ModEntry.Safe("low hp", Tick);
	}

	private static void Tick()
	{
		NCombatRoom? room = NCombatRoom.Instance;
		float target = 0f;
		NCreature? node = null;
		if (room != null && GodotObject.IsInstanceValid(room))
		{
			foreach (NCreature n in room.CreatureNodes)
			{
				if (n.Entity.Player is Player p && Makoto.Is(p) && GodotObject.IsInstanceValid(n))
				{
					node = n;
					float hp = (float)n.Entity.GetHpPercentRemaining();
					if (n.Entity.IsAlive && hp <= Threshold)
					{
						target = Mathf.Lerp(0.55f, 1f, 1f - hp / Threshold);
					}
					break;
				}
			}
		}
		float dt = (float)(Engine.GetMainLoop() as SceneTree)!.Root.GetProcessDeltaTime();
		_time += dt;
		_level = Mathf.MoveToward(_level, target, dt / 0.8f);
		if (_level <= 0.001f)
		{
			if (_overlay != null && GodotObject.IsInstanceValid(_overlay))
			{
				_overlay.QueueFree();
			}
			_overlay = null;
			return;
		}
		if (_overlay == null || !GodotObject.IsInstanceValid(_overlay))
		{
			Control? front = node != null ? Makoto.FxLayer() : null;
			if (front == null)
			{
				return;
			}
			Vector2 size = front.GetViewportRect().Size;
			_overlay = new ColorRect
			{
				Size = size,
				MouseFilter = Control.MouseFilterEnum.Ignore,
				Material = new ShaderMaterial { Shader = Shaders.Drown },
				TopLevel = true,
			};
			front.AddChild(_overlay);
			_overlay.GlobalPosition = Vector2.Zero;
			Fx.SetParam(_overlay, "aspect", size.X / size.Y);
		}
		// 心跳：每 1.1s 一次"咚—咚"
		float beat = _time % 1.1f;
		float pulse = Mathf.Exp(-beat * 9f) + 0.6f * Mathf.Exp(-Mathf.Max(beat - 0.22f, 0f) * 9f) * (beat > 0.22f ? 1f : 0f);
		Fx.SetParam(_overlay, "intensity", (_level * _level * (3f - 2f * _level)) * 0.85f);
		Fx.SetParam(_overlay, "pulse", pulse);
	}
}
