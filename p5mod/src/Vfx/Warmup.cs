using Godot;

namespace P5Joker;

/// <summary>
/// 预热（用户 2026-11-02：特效首次打出会卡顿一下）。第一次用到某个特效时引擎要现编译着色器、现加载原版特效场景，那一帧就会卡。
/// 每场战斗开始时把第四版用到的东西都在一个几乎全透明（alpha 0.004，仍然会真正绘制）的层里各放一次：
/// 原版粒子场景（换 LUT 的复制材质）、手绘贴图材质（正常 + 加法两种）、命中白闪 / 空间扭曲、全屏冲击帧。玩家看不见，之后第一次打出就不卡。
/// </summary>
public static class Warmup
{
	private static ulong _lastRoom;

	/// <summary>room = 这场战斗的标识（同一场只预热一次）；layer = 战斗特效层。</summary>
	public static void Run(Control layer, ulong room)
	{
		if (room == _lastRoom || !GodotObject.IsInstanceValid(layer))
		{
			return;
		}
		_lastRoom = room;
		Control hidden = new()
		{
			Name = "P5Warmup",
			Modulate = new Color(1f, 1f, 1f, 0.004f),
			MouseFilter = Control.MouseFilterEnum.Ignore,
			Size = Fx.Screen(layer),
		};
		layer.AddChild(hidden);
		hidden.GlobalPosition = Vector2.Zero;
		Vector2 c = Fx.Screen(layer) * 0.5f;
		Sfx.Muted = true;                                             // 特效里自带的音效（枪声、命中声）预热时不要响
		try
		{
			WarmAll(hidden, c);
		}
		finally
		{
			Sfx.Muted = false;
		}
		Fx.FreeAfter(hidden, 1.5);
	}

	private static void WarmAll(Control hidden, Vector2 c)
	{

		foreach (string path in new[] { Vanilla.BigSlashSmoke, Vanilla.Specks, Vanilla.Streaks })
		{
			Stage.Safe("warmup " + path, () => Vanilla.Burst(hidden, path, c, 0f, 1f, Lut5.Blood));
		}
		Stage.Safe("warmup smoke lut", () => Vanilla.Burst(hidden, Vanilla.BigSlashSmoke, c, 0f, 1f, Lut5.Smoke));
		Stage.Safe("warmup gun", () =>
		{
			GunFx.Muzzle(hidden, c, Vector2.Right);
			GunFx.Shot(hidden, c, c + new Vector2(300f, 0f));
		});
		foreach (string tex in new[] { "slash_thin", "slash_heavy" })
		{
			Stage.Safe("warmup " + tex, () => Paint.Sweep(hidden, tex, c, 0f, Vector2.One * 0.3f, Lut5.Blood, 0.05f, 0.05f, 0.2f));
		}
		Stage.Safe("warmup glow", () =>
		{
			Glow.Flash(hidden, c, new Color(1f, 1f, 1f, 0f), 8f, 0.1f);
			Glow.Ripple(hidden, c, 8f, 0.1f, 0f);
		});
		Stage.Safe("warmup impact", () => Impact.Warm(hidden));
	}
}
