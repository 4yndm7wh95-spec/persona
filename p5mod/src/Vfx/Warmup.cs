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
		Stage.Safe("warmup textures", Fx.PreloadAll);
		Stage.Safe("warmup sfx", () => Sfx.PreloadAll(layer));
		Stage.Safe("warmup cutin", CutIn.Preload);
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
		Stage.Safe("warmup poison", () =>
		{
			PoisonFx.Apply(hidden, c, c - new Vector2(0f, 120f), 200f, null, 9);
			PoisonFx.Tick(hidden, c, c - new Vector2(0f, 120f), 200f, null);
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
		Stage.Safe("warmup blue flame", () => BlueFlame.Tongue(hidden, c, 0.5f, 0.2f));
		Stage.Safe("warmup sinful shell", () => SinfulShell.Warm(hidden, c));   // 华丽收场：撒旦耶尔大贴图上传、场景 + 灯光、粒子、命中区
		// 所有着色器（mod 里每个 static Shader 属性：巨手、亚森、眼神特写、舞台、刀光……）各画一个 4×4 的小方块编译掉
		foreach (System.Type type in typeof(Warmup).Assembly.GetTypes())
		{
			foreach (System.Reflection.PropertyInfo prop in type.GetProperties(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
			{
				if (prop.PropertyType != typeof(Shader) || prop.GetIndexParameters().Length > 0)
				{
					continue;
				}
				Stage.Safe("warmup shader " + type.Name + "." + prop.Name, () =>
				{
					if (prop.GetValue(null) is not Shader sh || sh.GetMode() != Shader.Mode.CanvasItem)
					{
						return;
					}
					ColorRect r = new() { Material = new ShaderMaterial { Shader = sh }, Size = new Vector2(4f, 4f), MouseFilter = Control.MouseFilterEnum.Ignore };
					hidden.AddChild(r);
					r.GlobalPosition = c;
				});
			}
		}
		// 原版特效场景：每个都放一次（粒子材质第一次用也要编译）
		foreach (System.Reflection.FieldInfo fi in typeof(Vanilla).GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
		{
			if (fi.IsLiteral && fi.GetRawConstantValue() is string path)
			{
				Stage.Safe("warmup " + path, () => Vanilla.Burst(hidden, path, c, 0f, 0.3f, Lut5.Curse));
			}
		}
	}
}
