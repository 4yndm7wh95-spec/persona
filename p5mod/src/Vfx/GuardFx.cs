using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 防御：碎玻璃格挡（用户 2026-11-04 选的方向）。代码切的平面碎片太粗糙 → 碎片用 Codex 画的精细碎玻璃
/// （art/p5/fx/v4/glass：16 片带厚度亮边、内部反光条纹、崩口的玻璃 + 星芒 + 玻璃屑），代码只管运动、折射和闪光：
/// · 每片碎片 = 一格贴图 + 折射着色器：碎片后面的画面按它的倾斜方向偏移、带一点红蓝色散，玻璃贴图叠在上面；
/// · 26 片从四周错开时间飞来、边飞边转、由小变大，在 Joker 身前拼成一面错落的墙（略有重叠，不排成网格）；
/// · 停住时轻轻浮动，几片轮流闪出星芒；
/// · 炸开：带着旋转往外飞、往下掉，同时喷出一把玻璃屑。音效用原版尖塔的（block_gain）。
/// </summary>
public static class GuardFx
{
	private static Shader? _shader;

	private static Shader ShardShader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform vec2 tilt = vec2(0.0);
uniform float opacity = 1.0;
uniform float glint = 0.0;
void fragment() {
	vec4 g = texture(TEXTURE, UV);
	if (g.a < 0.02) { COLOR = vec4(0.0); }
	else {
		// 折射：碎片后面的画面按倾斜方向偏移，红蓝轻微错开
		vec2 off = tilt * 0.03;
		vec3 bg = vec3(textureLod(screen_tex, SCREEN_UV + off * 1.15, 0.0).r, textureLod(screen_tex, SCREEN_UV + off, 0.0).g,
			textureLod(screen_tex, SCREEN_UV + off * 0.85, 0.0).b);
		bg *= vec3(1.0, 0.93, 0.95);
		// 玻璃贴图叠在折射的背景上：透明的玻璃身只盖一点，亮边和反光几乎全盖
		float w = smoothstep(0.2, 0.9, g.a);
		vec3 col = mix(bg, g.rgb, w) + g.rgb * glint * 0.8;
		COLOR = vec4(col, opacity);
	}
}" };

	private sealed class Shard
	{
		public Sprite2D Sprite = null!;
		public ShaderMaterial Mat = null!;
		public Vector2 Home, From, Out;
		public float Delay, RotFrom, RotHome, Spin, Scale, Phase;
	}

	/// <summary>放一面碎玻璃墙。fist = 抬起的前臂，shield = 墙的中心，height = 墙高，facing = Joker 朝向。约 0.95s。</summary>
	public static void Shield(Control layer, Vector2 fist, Vector2 shield, float height, float facing = 1f)
	{
		if (Fx.Tex("glass_shards_sheet") is not Texture2D sheet)
		{
			return;
		}
		Texture2D? glints = Fx.Tex("glass_glints"), dust = Fx.Tex("glass_dust");
		float cell = sheet.GetWidth() / 4f;
		BackBufferCopy copy = new() { CopyMode = BackBufferCopy.CopyModeEnum.Viewport, Name = "P5GuardCopy" };
		layer.AddChild(copy);
		Node2D root = Fx.Root(layer, shield, "P5GuardGlass", 1.2);
		const int count = 34;
		float wallW = height * 0.75f, wallH = height * 1.15f;
		var shards = new List<Shard>();
		var rng = new RandomNumberGenerator();
		rng.Randomize();
		for (int i = 0; i < count; i++)
		{
			// 墙里的位置：竖长的椭圆里错落散开（越靠外越稀、越小）
			float ang = rng.RandfRange(0f, Mathf.Tau);
			float r = Mathf.Sqrt(rng.Randf());
			Vector2 home = new Vector2(Mathf.Cos(ang) * wallW * 0.5f, Mathf.Sin(ang) * wallH * 0.5f) * r;
			int idx = rng.RandiRange(0, 15);
			float s = height * rng.RandfRange(0.3f, 0.46f) * (1.15f - 0.4f * r) / cell * 1.6f;
			ShaderMaterial m = new() { Shader = ShardShader };
			Vector2 tilt = new(rng.RandfRange(-1f, 1f), rng.RandfRange(-1f, 1f));
			m.SetShaderParameter("tilt", tilt);
			Sprite2D sp = new()
			{
				Texture = sheet, RegionEnabled = true, RegionRect = new Rect2(idx % 4 * cell, idx / 4 * cell, cell, cell),
				Material = m, FlipH = rng.Randf() < 0.5f,
			};
			root.AddChild(sp);
			Vector2 outDir = (home + new Vector2(wallW * 0.3f * facing, 0f)).Normalized();
			shards.Add(new Shard
			{
				Sprite = sp, Mat = m, Home = home, Scale = s, Phase = rng.Randf() * Mathf.Tau,
				From = home + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * height * rng.RandfRange(0.6f, 1.0f),
				Out = outDir * height * rng.RandfRange(0.5f, 0.9f),
				Delay = rng.RandfRange(0f, 0.1f) + 0.04f * r,
				RotFrom = rng.RandfRange(-3f, 3f), RotHome = rng.RandfRange(-0.6f, 0.6f), Spin = rng.RandfRange(-6f, 6f),
			});
		}
		const float tIn = 0.22f, hold = 0.34f, tOut = 0.4f;
		float life = 0.1f + tIn + hold + tOut;
		Fx.Animate(root, 0, life, t =>
		{
			float time = t * life;
			foreach (Shard sh in shards)
			{
				float u = Mathf.Clamp((time - sh.Delay) / tIn, 0f, 1f);
				float e = 1f - Mathf.Pow(1f - u, 3f);
				float b = Mathf.Clamp((time - 0.1f - tIn - hold) / tOut, 0f, 1f);
				Vector2 pos = sh.From.Lerp(sh.Home, e);
				// 停住时轻轻浮动
				pos += new Vector2(Mathf.Sin(time * 3f + sh.Phase), Mathf.Cos(time * 2.4f + sh.Phase)) * 3f * e;
				// 炸开：往外飞、往下掉
				pos += sh.Out * Ease5.OutQuint(b) + new Vector2(0f, height * 0.9f * b * b);
				sh.Sprite.Position = pos;
				sh.Sprite.Rotation = Mathf.Lerp(sh.RotFrom, sh.RotHome, e) + sh.Spin * b;
				sh.Sprite.Scale = Vector2.One * sh.Scale * Mathf.Lerp(0.4f, 1f, e);
				sh.Mat.SetShaderParameter("opacity", Mathf.Clamp(u * 3f, 0f, 1f) * (1f - b * b));
				// 碎片转到某个角度时亮一下（每片错开）
				float g = Mathf.Max(0f, Mathf.Sin(time * 9f + sh.Phase * 3f));
				sh.Mat.SetShaderParameter("glint", Mathf.Pow(g, 12f) * e * (1f - b));
			}
		});
		// 星芒：拼好之后几片轮流闪一下
		if (glints != null)
		{
			float gw = glints.GetWidth() / 4f;
			for (int k = 0; k < 4; k++)
			{
				Shard sh = shards[rng.RandiRange(0, count - 1)];
				int gi = k;
				Fx.After(layer, 0.1f + tIn + 0.06f + 0.08f * k, () =>
				{
					Sprite2D star = new()
					{
						Texture = glints, RegionEnabled = true, RegionRect = new Rect2(gi * gw, 0f, gw, glints.GetHeight()),
						Material = Fx.Additive, Position = sh.Home + new Vector2(0f, -height * 0.04f), Scale = Vector2.One * height / gw * 0.22f,
					};
					root.AddChild(star);
					Fx.Animate(star, 0, 0.22, t2 =>
					{
						star.Modulate = new Color(1f, 1f, 1f, Mathf.Sin(t2 * Mathf.Pi));
						star.Rotation = t2 * 0.6f;
					});
					Fx.FreeAfter(star, 0.25);
				});
			}
		}
		// 拼好的那一下：一层很淡的光
		Fx.After(layer, 0.1f + tIn, () => Glow.Flash(layer, shield, new Color(1f, 0.85f, 0.88f), height * 0.7f, 0.16f));
		// 炸开：一把玻璃屑
		if (dust != null)
		{
			Fx.After(layer, 0.1f + tIn + hold, () =>
			{
				for (int k = 0; k < 6; k++)
				{
					Vector2 dir = Vector2.FromAngle(rng.RandfRange(-1.2f, 1.2f) + (facing > 0f ? 0f : Mathf.Pi));
					Sprite2D d = new() { Texture = dust, Material = Fx.Additive, Position = new Vector2(rng.RandfRange(-0.2f, 0.2f), rng.RandfRange(-0.4f, 0.4f)) * height, Scale = Vector2.One * height / dust.GetWidth() * rng.RandfRange(0.25f, 0.4f), Rotation = rng.RandfRange(0f, Mathf.Tau) };
					root.AddChild(d);
					Vector2 p0 = d.Position, v = dir * height * rng.RandfRange(0.5f, 0.9f);
					Fx.Animate(d, 0, 0.45, t3 =>
					{
						d.Position = p0 + v * Ease5.OutQuint(t3) + new Vector2(0f, height * 0.5f * t3 * t3);
						d.Modulate = new Color(1f, 1f, 1f, 1f - t3);
					});
				}
			});
		}
		Fx.FreeAfter(copy, life + 0.05);
	}
}
