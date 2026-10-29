using Godot;

namespace P5Joker;

/// <summary>
/// 眼神特写（用户 2026-10-29：复刻 P5R 原版，用 Joker 自己的原版切入图 BATTLE/CUTIN/BCT_P_BC0001 = 贴图 "eyes"；
/// Persona 召唤时放，放在背景层 —— 人物和敌人前面挡着它）。对着用户给的原版录像逐帧排的时间线：
///  0～0.09s  一条斜的窄带从左边撕进来（撕纸的白边 + 黑描边，锯齿每次不一样），眼睛那一条；
///  0.05s     右边红黑锯齿块滑进来，白色的「!!」带过冲弹出来；
///  0.12s     白闪；
///  ～0.55s   停住：眼睛缓缓推近、整条带子轻轻平移，「!!」抖；
///  0.55～0.72s  整条往右撕走：从左边一路被撕掉、同时往右滑，下沿拖出红色撕痕，白纸片往右飞。
/// 带子本身是一块着色器矩形（撕边、描边、揭开 / 撕掉都在着色器里实时算），图只是里面那一条眼睛。
/// </summary>
public static class CutIn
{
	private static Shader? _band;

	private static Shader Band => _band ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D tex : filter_linear_mipmap;
uniform vec4 crop = vec4(0.0, 0.38, 1.0, 0.74);
uniform float reveal = 1.0;
uniform float eaten = 0.0;
uniform float zoom = 1.0;
uniform float seed = 0.0;
uniform float alpha = 1.0;
float hash(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}
// 撕纸的锯齿：细碎的毛边 + 偶尔一根长尖
float jag(float x, float s) {
	return noise(vec2(x * 60.0 + s, s)) * 0.022 + pow(noise(vec2(x * 11.0 + s * 3.0, s)), 5.0) * 0.16 + noise(vec2(x * 3.0, s)) * 0.03;
}
void fragment() {
	vec2 uv = UV;
	// 带子在矩形里占 y 0.22～0.78；上下沿各自撕成锯齿（往外翘的是白纸）
	float top = 0.22 - jag(uv.x, seed), bot = 0.78 + jag(uv.x, seed + 7.0);
	float rimT = 0.035 + noise(vec2(uv.x * 25.0, seed + 2.0)) * 0.03, rimB = 0.035 + noise(vec2(uv.x * 25.0, seed + 5.0)) * 0.03;
	float line = 0.012;
	// 左边：揭开的前沿（往右推进）/ 撕掉的后沿（也往右推进），都是竖向的锯齿
	float front = reveal * 1.3 - 0.15 + (noise(vec2(uv.y * 30.0, seed + 11.0)) - 0.5) * 0.08 + (uv.y - 0.5) * 0.12;
	float back = eaten * 1.45 - 0.2 + (noise(vec2(uv.y * 26.0, seed + 13.0)) - 0.5) * 0.1 - (uv.y - 0.5) * 0.1;
	if (uv.x > front || uv.x < back) { discard; }
	float y = uv.y;
	vec4 col;
	if (y < top - line || y > bot + line) { discard; }
	else if (y < top || y > bot) { col = vec4(0.0, 0.0, 0.0, 1.0); }                       // 最外一圈黑描边
	else if (y < top + rimT || y > bot - rimB) { col = vec4(1.0, 0.99, 0.97, 1.0); }         // 撕开的白纸边
	else {
		// 眼睛：按带子的直边映射，图本身不跟着锯齿扭；中心推近
		float v = (y - 0.22) / 0.56;
		vec2 c = vec2(uv.x, v) - 0.5;
		c /= zoom;
		c += 0.5;
		vec2 t = vec2(mix(crop.x, crop.z, c.x), mix(crop.y, crop.w, c.y));
		col = texture(tex, t);
		col.a = 1.0;
	}
	// 前沿 / 后沿也描一道黑边、一道白边（像被撕开的纸）
	float edgeF = front - uv.x, edgeB = uv.x - back;
	float e = min(edgeF, edgeB);
	if (e < 0.004) { col = vec4(0.0, 0.0, 0.0, 1.0); }
	else if (e < 0.012) { col = vec4(1.0, 0.99, 0.97, 1.0); }
	COLOR = vec4(col.rgb, col.a * alpha);
}" };

	private static readonly Color Red = new("e60012"), Ink = new("0a0a0a"), Paper = new("fffcf7");

	/// <summary>放一次眼神特写（back = 背景特效层；整张屏幕的坐标）。</summary>
	public static void Eyes(Control back)
	{
		if (Fx.Tex("eyes") is not Texture2D tex)
		{
			return;
		}
		Vector2 screen = Fx.Screen(back);
		float w = screen.X * 1.25f, hb = screen.Y * 0.3f, h = hb / 0.56f;
		Vector2 center = new(screen.X * 0.5f, screen.Y * 0.4f);
		const float tilt = -0.07f;                                       // 左低右高
		Node2D root = Fx.Root(back, center, "P5CutIn", 0.9);
		root.Rotation = tilt;

		// 背景压暗一点（只在人物后面），带子更跳
		ColorRect dim = new() { Size = screen * 1.6f, Position = -screen * 0.8f, Color = new Color(0f, 0f, 0f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(dim);

		ColorRect band = new()
		{
			Size = new Vector2(w, h), Position = new Vector2(-w / 2f, -h / 2f), MouseFilter = Control.MouseFilterEnum.Ignore,
			Material = new ShaderMaterial { Shader = Band },
		};
		root.AddChild(band);
		ShaderMaterial m = (ShaderMaterial)band.Material;
		m.SetShaderParameter("tex", tex);
		m.SetShaderParameter("seed", Fx.Rand(0f, 50f));
		m.SetShaderParameter("reveal", 0f);

		// 右边红黑锯齿块（在「!!」后面）
		Node2D block = new() { Position = new Vector2(screen.X * 0.34f, 0f) };
		root.AddChild(block);
		Fx.Poly(block, new[] { new Vector2(-40f, -hb * 0.62f), new Vector2(160f, -hb * 0.7f), new Vector2(120f, -hb * 0.1f), new Vector2(190f, hb * 0.05f),
			new Vector2(140f, hb * 0.6f), new Vector2(-80f, hb * 0.66f), new Vector2(-20f, hb * 0.1f), new Vector2(-70f, -hb * 0.05f) }, Ink);
		Fx.Poly(block, new[] { new Vector2(-20f, -hb * 0.55f), new Vector2(150f, -hb * 0.62f), new Vector2(105f, -hb * 0.08f), new Vector2(165f, hb * 0.04f),
			new Vector2(120f, hb * 0.52f), new Vector2(-55f, hb * 0.57f), new Vector2(0f, hb * 0.08f), new Vector2(-45f, -hb * 0.04f) }, Red);
		Fx.Poly(block, new[] { new Vector2(30f, -hb * 0.6f), new Vector2(60f, -hb * 0.6f), new Vector2(10f, hb * 0.55f), new Vector2(-20f, hb * 0.55f) }, Ink);

		// 「!!」：白底、黑描边、红投影，带过冲弹出来，停住时抖
		Node2D bang = new() { Position = new Vector2(screen.X * 0.27f, -hb * 0.08f), Rotation = 0.22f };
		root.AddChild(bang);
		for (int i = 0; i < 2; i++)
		{
			Vector2 o = new(i * hb * 0.36f, i * hb * 0.04f);
			Mark(bang, o + new Vector2(10f, 9f), hb, Red, 1f);
			Mark(bang, o, hb, Ink, 1.13f);
			Mark(bang, o, hb, Paper, 1f);
		}

		// 白闪（背景层，整屏）
		ColorRect flash = new() { Size = screen * 1.6f, Position = -screen * 0.8f, Color = new Color(1f, 1f, 1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(flash);
		root.MoveChild(flash, 1);

		bool torn = false;
		Fx.Animate(root, 0, 0.78, t =>
		{
			float s = t * 0.78f;
			m.SetShaderParameter("reveal", Fx.OutCubic(Mathf.Min(1f, s / 0.09f)));
			m.SetShaderParameter("zoom", 1f + 0.05f * Mathf.Min(1f, s / 0.6f));
			float exit = Mathf.Clamp((s - 0.55f) / 0.17f, 0f, 1f);
			m.SetShaderParameter("eaten", Fx.InCubic(exit));
			root.Position = center + new Vector2(14f * Mathf.Min(1f, s / 0.55f) + 420f * Fx.InCubic(exit), 0f).Rotated(tilt);
			dim.Color = new Color(0f, 0f, 0f, 0.35f * Mathf.Min(1f, s / 0.08f) * (1f - exit));
			flash.Color = new Color(1f, 1f, 1f, 0.4f * Mathf.Exp(-Mathf.Pow((s - 0.13f) / 0.035f, 2f)));
			// 红黑块从右边滑进来、「!!」弹出（过冲），停住时抖；撕走时一起被带走、变淡
			float inB = Ease5.OutBack(Mathf.Clamp((s - 0.04f) / 0.12f, 0f, 1f));
			block.Position = new Vector2(screen.X * 0.34f + 260f * (1f - inB), 0f);
			float pop = Ease5.OutBack(Mathf.Clamp((s - 0.06f) / 0.12f, 0f, 1f));
			bang.Scale = Vector2.One * pop;
			bang.Position = new Vector2(screen.X * 0.27f, -hb * 0.08f) + (s > 0.18f && s < 0.55f ? new Vector2(Fx.Rand(-3f, 3f), Fx.Rand(-3f, 3f)) : Vector2.Zero);
			block.Modulate = bang.Modulate = new Color(1f, 1f, 1f, 1f - exit);
			if (!torn && s >= 0.55f)
			{
				torn = true;
				Tear(back, center, w, hb, tilt);
			}
		});
	}

	/// <summary>一个「!」：上宽下窄的竖条 + 下面一个方点。scale 用来画黑描边（略大的同形状垫在后面）。</summary>
	private static void Mark(Node parent, Vector2 at, float hb, Color c, float scale)
	{
		float bw = hb * 0.16f, bh = hb * 0.62f;
		Node2D n = new() { Position = at, Scale = Vector2.One * scale };
		parent.AddChild(n);
		Fx.Poly(n, new[] { new Vector2(-bw * 0.6f, -bh), new Vector2(bw * 0.6f, -bh * 1.02f), new Vector2(bw * 0.22f, bh * 0.25f), new Vector2(-bw * 0.22f, bh * 0.25f) }, c);
		Fx.Poly(n, new[] { new Vector2(-bw * 0.3f, bh * 0.42f), new Vector2(bw * 0.32f, bh * 0.4f), new Vector2(bw * 0.28f, bh * 0.68f), new Vector2(-bw * 0.32f, bh * 0.7f) }, c);
	}

	/// <summary>撕走：下沿拖出几道红色撕痕往右甩，白纸片和黑纸片往右上飞散、打转。</summary>
	private static void Tear(Control back, Vector2 center, float w, float hb, float tilt)
	{
		Node2D root = Fx.Root(back, center, "P5CutInTear", 0.7);
		root.Rotation = tilt;
		for (int i = 0; i < 5; i++)
		{
			float y = hb * (0.36f + i * 0.05f), x0 = -w * 0.45f + i * w * 0.08f;
			Polygon2D streak = Fx.Poly(root, new[] { Vector2.Zero }, Red);
			float lag = i * 0.015f;
			Fx.Animate(streak, lag, 0.3, t =>
			{
				float head = Fx.OutCubic(Mathf.Min(1f, t * 1.6f)), tail = Fx.InCubic(t);
				float a = x0 + w * 0.9f * tail, b = x0 + w * 0.9f * head, th = hb * (0.07f - i * 0.008f);
				streak.Polygon = new[] { new Vector2(a, y), new Vector2(b, y - th * 0.4f), new Vector2(b + 30f, y), new Vector2(b, y + th * 0.6f) };
			});
		}
		for (int i = 0; i < 16; i++)
		{
			float sz = Fx.Rand(14f, 46f);
			Vector2[] shape = { new(-sz, -sz * Fx.Rand(0.2f, 0.6f)), new(sz * Fx.Rand(0.3f, 1f), -sz * 0.5f), new(sz, sz * Fx.Rand(0.1f, 0.5f)), new(-sz * 0.4f, sz * 0.4f) };
			Node2D bit = new() { Position = new Vector2(Fx.Rand(-w * 0.4f, w * 0.2f), Fx.Rand(-hb * 0.5f, hb * 0.5f)) };
			root.AddChild(bit);
			Fx.Poly(bit, shape, i % 4 == 0 ? Ink : Paper);
			Vector2 v = new Vector2(Fx.Rand(900f, 1700f), Fx.Rand(-500f, 150f));
			float spin = Fx.Rand(-12f, 12f);
			Vector2 p0 = bit.Position;
			Fx.Animate(bit, Fx.Rand(0f, 0.08f), 0.45, t =>
			{
				float s = t * 0.45f;
				bit.Position = p0 + v * s + new Vector2(0f, 600f * s * s);
				bit.Rotation = spin * s;
				bit.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t));
			});
		}
	}
}
