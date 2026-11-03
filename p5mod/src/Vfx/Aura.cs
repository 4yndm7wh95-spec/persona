using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 人物轮廓上的一圈蓝焰（用户 2026-10-29：手扣面具时身体周围冒蓝火；第一版放大的复制品在游戏里错开成了"幻影"，
/// 用户要的是贴着身体的一圈火焰轮廓）。
/// 做法：复制一份 Joker 的骨骼（同一个动作）画进一张离屏画面（SubViewport），得到他这一帧的剪影；
/// 再把这张画面放回人物后面，用着色器只在剪影外面画火：贴着边一圈亮的火边，火舌往上舔（往下找剪影 = 火从身体边上往上烧）、
/// 长短不一、上半截断开；身体里面什么都不画，所以不会有幻影。和面具火同一套配色、普通混合。
/// 复制骨骼要用游戏 / 预览自己的节点，由 CloneBody 提供（游戏：p5mod/src/Aim.cs；预览：tools/p5/fxpreview/Preview.cs）。
/// </summary>
public sealed class BodyAura
{
	/// <summary>在 parent 下面复制一份 Joker 的骨骼（同一个动作），返回复制品和本体此刻的全局变换；拿不到就返回 null。</summary>
	public static Func<Node, (Node2D Clone, Transform2D BodyGlobal)?>? CloneBody;

	private static Shader? _shader;

	private static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform vec4 c_dark : source_color = vec4(0.03, 0.06, 0.25, 1.0);
uniform vec4 c_mid : source_color = vec4(0.11, 0.35, 1.0, 1.0);
uniform vec4 c_hi : source_color = vec4(0.31, 0.78, 1.0, 1.0);
uniform vec4 c_hot : source_color = vec4(0.92, 0.99, 1.0, 1.0);
uniform sampler2D sil_tex : filter_linear;
uniform vec2 px = vec2(0.002, 0.002);
uniform float rim = 5.0;
uniform float len = 46.0;
uniform float alpha = 0.0;
uniform float climb = 1.0;      // 点火：火从脚下往上爬（0 = 还没烧，1 = 烧到头顶）
uniform vec2 feet = vec2(0.5, 0.95);   // 脚底（UV）：脚下一圈底座火
uniform float base_w = 0.12;    // 底座火半宽（UV）
uniform float clock = 0.0;
uniform float seed = 0.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 4; i++) { v += a * noise(p); p = p * 2.03 + vec2(1.3, 4.1); a *= 0.5; }
	return v;
}
float ridge(vec2 p) {
	float v = 0.0, a = 0.55;
	for (int i = 0; i < 4; i++) { float n = 1.0 - abs(noise(p) * 2.0 - 1.0); v += a * n * n; p = p * 2.07 + vec2(3.1, 7.7); a *= 0.5; }
	return v;
}
// 剪影：只认身体（脚下的影子最多 0.43 不透明，不算）
float sil(vec2 uv) { return smoothstep(0.5, 0.9, texture(sil_tex, uv).a); }
void fragment() {
	vec2 sp = UV / px;                                     // 像素坐标
	float t = clock + seed;
	float body = sil(UV);
	// 贴着身体的一圈：往四周 rim 像素内有身体
	float ring = 0.0;
	for (int i = 0; i < 12; i++) {
		float an = 6.2832 * float(i) / 12.0;
		ring = max(ring, sil(UV + vec2(cos(an), sin(an)) * rim * px));
	}
	// 火舌：往下找身体（火从身体边上往上烧），沿途横向飘；每一列高低不一，上半截断开
	float fire = 0.0;
	for (int i = 1; i < 13; i++) {
		float k = float(i) / 12.0;
		float drift = (noise(vec2(sp.x * 0.05 + seed, sp.y * 0.04 + t * 2.2)) - 0.5) * 14.0 * k;
		// 往正下 + 斜下两侧找身体：竖直的外套边也会沿着身体往上舔（用户 2026-11-01：只有头上有火，头大身小）
		float s = sil(UV + vec2(drift, len * k) * px);
		s = max(s, 0.85 * sil(UV + vec2(drift + len * k * 0.55, len * k * 0.7) * px));
		s = max(s, 0.85 * sil(UV + vec2(drift - len * k * 0.55, len * k * 0.7) * px));
		float colN = fbm(vec2((sp.x - drift) * 0.06 + seed, sp.y * 0.02 + t * 2.4));
		float reach = smoothstep(0.3 + k * 0.36, 0.44 + k * 0.36, colN);
		float gap = smoothstep(0.55, 0.8, noise(vec2(sp.x * 0.07 + seed * 3.0, sp.y * 0.05 + t * 3.4)));
		fire = max(fire, s * reach * smoothstep(0.0, 0.18, k) * (1.0 - k * 0.45) * (1.0 - gap * smoothstep(0.3, 0.8, k)));
	}
	// 不画贴身的一圈（用户 2026-11-01：太像描边），只要往上窜的火舌
	// 脚下的底座火：贴地一圈低矮的火往上冒，把人「托」起来（重心往下移）
	float gx = abs(UV.x - feet.x) / base_w;
	float gy = (feet.y - UV.y) / (len * 1.3 * px.y);
	if (gx < 1.0 && gy > -0.04 && gy < 1.0) {
		float colG = fbm(vec2(sp.x * 0.05 + seed * 2.0, sp.y * 0.03 + t * 2.6));
		float hG = (1.0 - gx * gx) * (0.45 + 0.55 * colG);
		fire = max(fire, smoothstep(hG, hG - 0.25, gy) * smoothstep(-0.04, 0.05, gy) * 0.9);
	}
	fire *= 1.0 - body;                                     // 只在身体外面
	// 点火：从脚往上爬
	float up = (feet.y - UV.y) / max(feet.y, 0.01);
	fire *= smoothstep(climb * 1.15, climb * 1.15 - 0.15, up);
	// 火丝
	vec2 w = vec2(noise(sp * 0.02 + vec2(0.0, t * 0.8)), noise(sp * 0.02 + vec2(5.2, t * 0.8)));
	float n = ridge(vec2(sp.x * 0.07 + w.x * 1.5, sp.y * 0.022 + t * 1.9));
	float e = pow(n, 2.0) * 1.3;
	// 用户 2026-11-03：重制成和亚森现身蓝焰同一套（P5 动画觉醒那段）——外形一刀切的硬边、四档硬边分层
	// （深海军蓝 → 饱和蓝 → 青 → 白芯），不再是柔和渐变的半透明火
	float f = fire * (0.45 + 0.55 * smoothstep(0.1, 0.5, e));
	float heat = f * (0.55 + 0.6 * e);
	float a = smoothstep(0.16, 0.19, f);
	vec3 col = heat > 0.78 ? vec3(0.94, 0.98, 1.0) : heat > 0.55 ? vec3(0.4, 0.8, 1.0) : heat > 0.32 ? vec3(0.1, 0.38, 0.92) : vec3(0.03, 0.09, 0.32);
	COLOR = vec4(col, a * alpha);
}" };

	private readonly Node2D? _root;
	private readonly SubViewport? _view;
	private readonly ShaderMaterial? _mat;
	private bool _ended;

	/// <param name="back">人物后面的特效层</param>
	/// <param name="region">要罩住的区域（全局坐标：人物 + 四周留给火的边）</param>
	public BodyAura(Control back, Rect2 region, Vector2? feetGlobal = null, float baseHalfWidth = 0f)
	{
		if (CloneBody == null)
		{
			return;
		}
		Vector2I size = new(Mathf.CeilToInt(region.Size.X), Mathf.CeilToInt(region.Size.Y));
		_view = new SubViewport { Size = size, TransparentBg = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always, Disable3D = true };
		back.AddChild(_view);
		if (CloneBody(_view) is not { } made)
		{
			_view.QueueFree();
			_view = null;
			return;
		}
		// 复制品在离屏画面里的位置 = 本体的全局变换减去区域左上角
		made.Clone.Transform = new Transform2D(0f, -region.Position) * made.BodyGlobal;

		_root = Fx.Root(back, region.Position, "P5BodyAura", 600);
		_mat = new ShaderMaterial { Shader = Shader };
		Sprite2D sprite = new() { Texture = _view.GetTexture(), Centered = false, Material = _mat };
		_root.AddChild(sprite);
		_mat.SetShaderParameter("sil_tex", _view.GetTexture());
		_mat.SetShaderParameter("px", new Vector2(1f / size.X, 1f / size.Y));
		_mat.SetShaderParameter("rim", Mathf.Max(3f, region.Size.Y * 0.008f));
		_mat.SetShaderParameter("len", region.Size.Y * 0.2f);
		_mat.SetShaderParameter("c_dark", PersonaMask.Dark);
		_mat.SetShaderParameter("c_mid", PersonaMask.Mid);
		_mat.SetShaderParameter("c_hi", PersonaMask.Hi);
		_mat.SetShaderParameter("c_hot", PersonaMask.Hot);
		_mat.SetShaderParameter("seed", Fx.Rand(0f, 20f));
		_mat.SetShaderParameter("alpha", 0f);
		if (feetGlobal is Vector2 fg)
		{
			_mat.SetShaderParameter("feet", (fg - region.Position) / region.Size);
			_mat.SetShaderParameter("base_w", baseHalfWidth / region.Size.X);
		}
		_mat.SetShaderParameter("climb", 0f);
		ShaderMaterial m = _mat;
		// 自己的时钟（不用 TIME，见 Fx.Clock）
		Tween clock = sprite.CreateTween().SetLoops();
		clock.TweenMethod(Callable.From<float>(v => m.SetShaderParameter("clock", v)), 0f, 600f, 600.0);
		// 架势摆好之后烧起来，之后随呼吸起伏
		Tween fade = sprite.CreateTween();
		fade.TweenInterval(0.15);
		fade.TweenMethod(Callable.From<float>(v =>
		{
			m.SetShaderParameter("alpha", Mathf.Min(1f, v * 2f));
			m.SetShaderParameter("climb", Fx.OutCubic(v));                // 0.3s 从脚烧到头顶
		}), 0f, 1f, 0.3);
		Tween breathe = sprite.CreateTween().SetLoops();
		breathe.TweenInterval(0.5);
		breathe.TweenMethod(Callable.From<float>(v =>
		{
			if (!_ended)
			{
				m.SetShaderParameter("alpha", 0.85f + 0.15f * Mathf.Sin(v * Mathf.Tau));
			}
		}), 0f, 1f, 1.2);
	}

	/// <summary>打出：火舌一下窜高、亮一下再散掉；放回：淡掉。然后删掉离屏画面和复制品。</summary>
	public void End(bool played)
	{
		if (_ended || _root == null || _mat == null || !GodotObject.IsInstanceValid(_root))
		{
			return;
		}
		_ended = true;
		ShaderMaterial m = _mat;
		float len0 = (float)m.GetShaderParameter("len");
		Tween tw = _root.CreateTween();
		if (played)
		{
			tw.TweenMethod(Callable.From<float>(t =>
			{
				m.SetShaderParameter("alpha", (1f + 1.2f * (1f - t)) * (1f - t));
				m.SetShaderParameter("len", len0 * (1f + 1.2f * Fx.OutCubic(t)));
			}), 0f, 1f, 0.35);
		}
		else
		{
			tw.TweenMethod(Callable.From<float>(t => m.SetShaderParameter("alpha", 1f - t)), 0f, 1f, 0.25);
		}
		SubViewport? view = _view;
		Node2D root = _root;
		tw.TweenCallback(Callable.From(() =>
		{
			if (view != null && GodotObject.IsInstanceValid(view))
			{
				view.QueueFree();
			}
			if (GodotObject.IsInstanceValid(root))
			{
				root.QueueFree();
			}
		}));
	}
}
