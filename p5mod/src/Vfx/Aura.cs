using System;
using System.Collections.Generic;
using Godot;

namespace P5Joker;

/// <summary>
/// 人物轮廓上的蓝焰（用户 2026-10-29：手扣面具时，Joker 身体轮廓周围也冒一圈蓝色火焰）。
/// 做法：复制几份 Joker 的骨骼放在本体后面、同一个动作，每份放大一点、往上多拉长一点（脚底不动）；
/// 复制品用一个着色器画成往上流动的蓝色火丝（形状取复制品贴图的不透明部分，火丝的纹理按屏幕坐标往上流）。
/// 本体挡在前面，只有身体边缘外那一圈火露出来，跟着动作、衣摆一起动。加法混合。
/// 复制骨骼要用游戏 / 预览自己的节点，由 CloneBody 提供（游戏：p5mod/src/Aim.cs；预览：tools/p5/fxpreview/Preview.cs）。
/// </summary>
public sealed class BodyAura
{
	/// <summary>复制一份 Joker 的骨骼（放在本体后面、同一个动作），返回复制品和"给它换材质"的方法；拿不到就返回 null。</summary>
	public static Func<(Node2D Clone, Action<Material> SetMaterial)?>? CloneBody;

	private static Shader? _shader;

	private static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform vec4 c_mid : source_color = vec4(0.11, 0.35, 1.0, 1.0);
uniform vec4 c_hi : source_color = vec4(0.31, 0.78, 1.0, 1.0);
uniform vec4 c_hot : source_color = vec4(0.92, 0.99, 1.0, 1.0);
uniform float alpha = 0.0;
uniform float clock = 0.0;
uniform float seed = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float ridge(vec2 p) {
	float v = 0.0, a = 0.55;
	for (int i = 0; i < 4; i++) { float n = 1.0 - abs(noise(p) * 2.0 - 1.0); v += a * n * n; p = p * 2.07 + vec2(3.1, 7.7); a *= 0.5; }
	return v;
}
void fragment() {
	float a = smoothstep(0.5, 0.9, texture(TEXTURE, UV).a);   // 只认身体（脚下的影子最多 0.43 不透明，不冒火）
	// 屏幕坐标：火丝竖着拉长、往上流（SCREEN_UV 的 y 往下为正，所以 + 时间 = 往上走）
	vec2 sp = SCREEN_UV * vec2(64.0, 36.0);
	float t = clock + seed;
	vec2 w = vec2(noise(sp * 0.3 + vec2(0.0, t * 0.9)), noise(sp * 0.3 + vec2(5.2, t * 0.9)));
	float n = ridge(vec2(sp.x * 0.8 + w.x * 1.6, sp.y * 0.28 + t * 1.8));
	float f = pow(n, 2.4);
	vec3 col = mix(c_mid.rgb, c_hi.rgb, smoothstep(0.15, 0.55, f));
	col = mix(col, c_hot.rgb, smoothstep(0.55, 0.95, f));
	COLOR = vec4(col, a * alpha * (0.2 + 0.8 * smoothstep(0.08, 0.6, f)));
}" };

	// 三层：越外越大、越往上拉长、越淡、流得越快
	private static readonly (float Scale, float Stretch, float Alpha, float Speed)[] Layers =
	{
		(1.03f, 1.05f, 0.9f, 1.0f),
		(1.065f, 1.11f, 0.55f, 1.25f),
		(1.1f, 1.18f, 0.3f, 1.5f),
	};

	private readonly List<(Node2D Clone, ShaderMaterial Mat, float Alpha)> _layers = new();
	private bool _ended;

	public BodyAura()
	{
		if (CloneBody == null)
		{
			return;
		}
		foreach (var (scale, stretch, alpha, speed) in Layers)
		{
			if (CloneBody() is not { } made)
			{
				return;
			}
			var (clone, setMaterial) = made;
			clone.Scale *= new Vector2(scale, scale * stretch);              // 原点在脚底：脚不动，往上和两边胀
			ShaderMaterial m = new() { Shader = Shader };
			m.SetShaderParameter("c_mid", PersonaMask.Mid);
			m.SetShaderParameter("c_hi", PersonaMask.Hi);
			m.SetShaderParameter("c_hot", PersonaMask.Hot);
			m.SetShaderParameter("seed", Fx.Rand(0f, 20f));
			m.SetShaderParameter("alpha", 0f);
			setMaterial(m);
			_layers.Add((clone, m, alpha));
			// 自己的时钟（不用 TIME，见 Fx.Clock）
			Tween clock = clone.CreateTween().SetLoops();
			clock.TweenMethod(Callable.From<float>(v => m.SetShaderParameter("clock", v * speed)), 0f, 600f, 600.0);
			// 架势摆好之后从脚往上烧起来（淡入），之后随呼吸起伏
			float a0 = alpha;
			Tween fade = clone.CreateTween();
			fade.TweenInterval(0.15);
			fade.TweenMethod(Callable.From<float>(v => m.SetShaderParameter("alpha", v)), 0f, a0, 0.3);
			Tween breathe = clone.CreateTween().SetLoops();
			breathe.TweenInterval(0.5);
			float ph = Fx.Rand(0f, 1f);
			breathe.TweenMethod(Callable.From<float>(v =>
			{
				if (!_ended)
				{
					m.SetShaderParameter("alpha", a0 * (0.85f + 0.15f * Mathf.Sin((v + ph) * Mathf.Tau)));
				}
			}), 0f, 1f, 1.2);
		}
	}

	/// <summary>打出：亮一下、往外一胀再散掉；放回：淡掉。然后删掉复制品。</summary>
	public void End(bool played)
	{
		if (_ended)
		{
			return;
		}
		_ended = true;
		foreach (var (clone, m, alpha) in _layers)
		{
			if (!GodotObject.IsInstanceValid(clone))
			{
				continue;
			}
			Vector2 s0 = clone.Scale;
			Tween tw = clone.CreateTween();
			if (played)
			{
				tw.TweenMethod(Callable.From<float>(t =>
				{
					m.SetShaderParameter("alpha", alpha * (1f + 1.2f * (1f - t)) * (1f - t));
					clone.Scale = s0 * (1f + 0.08f * Fx.OutCubic(t));
				}), 0f, 1f, 0.35);
			}
			else
			{
				tw.TweenMethod(Callable.From<float>(t => m.SetShaderParameter("alpha", alpha * (1f - t))), 0f, 1f, 0.25);
			}
			tw.TweenCallback(Callable.From(() =>
			{
				if (GodotObject.IsInstanceValid(clone))
				{
					clone.QueueFree();
				}
			}));
		}
	}
}
