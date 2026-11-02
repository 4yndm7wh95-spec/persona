using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// P5 的渐变表（LUT）。原版特效的着色器用贴图的亮度（R 通道）查 LUT 得到颜色（research/sts2_vfx/recovered/shaders/vfx/common/），
/// 我们自己的材质（Paint.cs）也一样，所以颜色只在这里定。
/// </summary>
public static class Lut5
{
	private static readonly Dictionary<string, GradientTexture1D> Cache = new();

	private static GradientTexture1D Make(string key, params (float At, Color C)[] stops)
	{
		if (Cache.TryGetValue(key, out GradientTexture1D? t))
		{
			return t;
		}
		// 注意：Gradient.Offsets / Colors 取出来是副本，要整组建好再赋值
		float[] offsets = new float[stops.Length];
		Color[] colors = new Color[stops.Length];
		for (int i = 0; i < stops.Length; i++)
		{
			offsets[i] = stops[i].At;
			colors[i] = stops[i].C;
		}
		Gradient g = new() { Offsets = offsets, Colors = colors };
		t = new GradientTexture1D { Gradient = g, Width = 256 };
		Cache[key] = t;
		return t;
	}

	/// <summary>刀光 / 命中：暗部墨黑 → 深红 → 绯红 → 热白。</summary>
	public static GradientTexture1D Blood => Make("blood",
		(0f, new Color("12020a")), (0.25f, new Color("5a0610")), (0.5f, new Color("e5191c")), (0.78f, new Color("ff5a4a")), (1f, new Color("fff4f0")));

	/// <summary>烟：墨黑 → 暗红灰（不抢主体）。</summary>
	public static GradientTexture1D Smoke => Make("smoke",
		(0f, new Color("0a0306")), (0.5f, new Color("2a0a10")), (1f, new Color("6a2028")));

	/// <summary>亚森的召唤蓝焰：深青 → 青 → 淡青白，饱和度压低（用户：生图的蓝焰太艳）。</summary>
	public static GradientTexture1D Persona => Make("persona",
		(0f, new Color("06222e")), (0.35f, new Color("1f6f8c")), (0.65f, new Color("66c6d8")), (1f, new Color("eefbff")));

	/// <summary>枪口火焰 / 枪击火花：暗红 → 绯红 → 粉白 → 白（Codex 的橙黄火焰换成 P5 红白）。</summary>
	public static GradientTexture1D Muzzle => Make("muzzle",
		(0f, new Color("1a0204")), (0.3f, new Color("7a0a12")), (0.55f, new Color("e5191c")), (0.8f, new Color("ff8a70")), (1f, new Color("fffaf6")));

	/// <summary>金：暴击 / 撒旦耶尔。</summary>
	public static GradientTexture1D Gold => Make("gold",
		(0f, new Color("2a1400")), (0.4f, new Color("a8640c")), (0.75f, new Color("ffcf5a")), (1f, new Color("fffbe8")));
}

/// <summary>
/// 直接用游戏自己的特效（res://scenes/vfx/...，游戏 PCK 里有；预览工程里是 research/sts2_vfx 的拷贝）。
/// 只用没有脚本的组件场景（根节点就是 GPUParticles2D 的那些），发射由我们控制；
/// 材质复制一份再换 LUT，不影响游戏里其他地方用到的同一份材质。
/// </summary>
public static class Vanilla
{
	public const string SlashCore = "res://scenes/vfx/slash/vfx_slash_core.tscn";
	public const string SlashQuick = "res://scenes/vfx/slash/vfx_slash_quick.tscn";
	public const string BigSlashCore = "res://scenes/vfx/big_slash/vfx_big_slash_impact_core.tscn";
	public const string BigSlashSmoke = "res://scenes/vfx/big_slash/vfx_big_slash_impact_side_smoke.tscn";
	public const string HitFlare = "res://scenes/vfx/common/vfx_common_hit_flare.tscn";
	public const string Specks = "res://scenes/vfx/common/vfx_common_specks.tscn";
	public const string Streaks = "res://scenes/vfx/common/vfx_common_outward_streaks.tscn";
	public const string CleanFlare = "res://scenes/vfx/common/vfx_common_clean_flare_1.tscn";
	public const string BluntCore = "res://scenes/vfx/common/vfx_common_blunt_impact_core.tscn";
	public const string RoundSmoke = "res://scenes/vfx/common/vfx_common_round_smoke_flipbook.tscn";
	public const string DaggerImpact = "res://scenes/vfx/dagger/vfx_dagger_impact_core.tscn";

	private static readonly Dictionary<string, PackedScene?> Scenes = new();

	private static PackedScene? Scene(string path)
	{
		if (!Scenes.TryGetValue(path, out PackedScene? s))
		{
			s = ResourceLoader.Exists(path) ? ResourceLoader.Load<PackedScene>(path) : null;
			if (s == null)
			{
				Stage.Report("Vanilla", new System.IO.FileNotFoundException(path));
			}
			Scenes[path] = s;
		}
		return s;
	}

	/// <summary>
	/// 放一次原版粒子。at 全局坐标；rot 弧度；scale 缩放（负数翻转）；lut 换颜色（null = 原色）；tint 乘在顶点色上；
	/// amount &gt; 0 时覆盖粒子数；speed 播放速度。
	/// </summary>
	public static Node2D? Burst(Node parent, string path, Vector2 at, float rot = 0f, float scale = 1f, Texture2D? lut = null,
		Color? tint = null, int amount = 0, float speed = 1f, float delay = 0f)
	{
		if (delay > 0f)
		{
			Fx.After(parent, delay, () => Burst(parent, path, at, rot, scale, lut, tint, amount, speed));
			return null;
		}
		if (Scene(path)?.Instantiate() is not Node2D root)
		{
			return null;
		}
		parent.AddChild(root);
		root.GlobalPosition = at;
		root.Rotation += rot;
		root.Scale *= scale;
		double life = 0.2;
		foreach (GpuParticles2D p in Particles(root))
		{
			if (lut != null && p.Material is ShaderMaterial m)
			{
				ShaderMaterial copy = (ShaderMaterial)m.Duplicate();
				copy.SetShaderParameter("lut", lut);
				p.Material = copy;
			}
			if (tint is Color c)
			{
				p.SelfModulate = c;
			}
			else if (lut != null)
			{
				p.SelfModulate = Colors.White;                // 原版场景的 self_modulate 带蓝 / 紫调，换了 LUT 就去掉
			}
			if (amount > 0)
			{
				p.Amount = amount;
			}
			p.SpeedScale = speed;
			p.Emitting = true;
			p.Restart();
			life = Mathf.Max(life, p.Lifetime / Mathf.Max(0.05f, speed) + 0.3);
		}
		Fx.FreeAfter(root, life);
		return root;
	}

	private static IEnumerable<GpuParticles2D> Particles(Node n)
	{
		if (n is GpuParticles2D p)
		{
			yield return p;
		}
		foreach (Node c in n.GetChildren())
		{
			foreach (GpuParticles2D q in Particles(c))
			{
				yield return q;
			}
		}
	}
}
