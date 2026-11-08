using System.Collections.Generic;
using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace P5Joker;

/// <summary>
/// Authored P5R geometry and material keys from BCT_P_SUMMON.GMD + BCT_P_SUMMON_BT.GMD.
/// The existing Joker texture replaces btl_cutin.dds. Paper/face/!! keys are discrete.
/// </summary>
public static class CutIn
{
	private sealed class Clip
	{
		public float Duration { get; set; }
		public float Speed { get; set; }
		public float[] Canvas { get; set; } = Array.Empty<float>();
		public Part[] Meshes { get; set; } = Array.Empty<Part>();
	}
	private sealed class Part
	{
		public string Name { get; set; } = "";
		public float[][] Vertices { get; set; } = Array.Empty<float[]>();
		public float[][] Uv { get; set; } = Array.Empty<float[]>();
		public int[] Indices { get; set; } = Array.Empty<int>();
		public float[] Color { get; set; } = Array.Empty<float>();
		public float[][] Keys { get; set; } = Array.Empty<float[]>();
		public bool Textured { get; set; }
		public bool Linear { get; set; }
	}
	private sealed record Prepared(Part Part, ArrayMesh Mesh);
	private static Clip? _clip;
	private static Prepared[]? _parts;
	private static ShaderMaterial? _opaqueEyes;

	/// <summary>预热：眼神特写的网格数据先解析好。</summary>
	public static void Preload() => Load();

	private static void Load()
	{
		if (_parts != null) return;
		using Stream stream = typeof(CutIn).Assembly.GetManifestResourceStream("P5Joker.EyeCutIn.json")
			?? throw new InvalidDataException("Missing embedded P5R eye cut-in data");
		Clip clip = JsonSerializer.Deserialize<Clip>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
			?? throw new InvalidDataException("Invalid P5R eye cut-in data");
		if (clip.Duration <= 0 || clip.Speed <= 0 || clip.Canvas.Length != 2 || clip.Meshes.Length == 0)
			throw new InvalidDataException("Invalid P5R eye cut-in timing/canvas");
		List<Prepared> parts = new();
		foreach (Part part in clip.Meshes)
		{
			Vector3[] vertices = new Vector3[part.Vertices.Length];
			Color[] colors = new Color[vertices.Length];
			Vector2[] uv = new Vector2[vertices.Length];
			for (int i = 0; i < vertices.Length; i++)
			{
				vertices[i] = new Vector3(part.Vertices[i][0], part.Vertices[i][1], 0f);
				colors[i] = new Color(part.Color[0], part.Color[1], part.Color[2], 1f);
				if (part.Textured) uv[i] = new Vector2(part.Uv[i][0], part.Uv[i][1]);
			}
			Godot.Collections.Array arrays = new();
			arrays.Resize((int)Mesh.ArrayType.Max);
			arrays[(int)Mesh.ArrayType.Vertex] = vertices;
			arrays[(int)Mesh.ArrayType.Color] = colors;
			arrays[(int)Mesh.ArrayType.Index] = part.Indices;
			if (part.Textured) arrays[(int)Mesh.ArrayType.TexUV] = uv;
			ArrayMesh mesh = new();
			mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
			parts.Add(new Prepared(part, mesh));
		}
		// Original face materials are opaque; PNG alpha must not punch holes through hair.
		_opaqueEyes = new ShaderMaterial { Shader = new Shader { Code = @"
shader_type canvas_item;
render_mode unshaded;
void fragment() { COLOR = vec4(texture(TEXTURE, UV).rgb, 1.0); }
" } };
		_clip = clip;
		_parts = parts.ToArray();
	}

	private static float Opacity(Part part, float time)
	{
		float[][] keys = part.Keys;
		if (keys.Length == 0) return 0f;
		int i = 0;
		while (i + 1 < keys.Length && keys[i + 1][0] <= time + 0.000001f) i++;
		if (!part.Linear || i + 1 == keys.Length) return keys[i][1];
		float span = keys[i + 1][0] - keys[i][0];
		return span <= 0 ? keys[i + 1][1] : Mathf.Lerp(keys[i][1], keys[i + 1][1], Mathf.Clamp((time - keys[i][0]) / span, 0f, 1f));
	}

	// ───────────── 2026-11-01：原版特写直接放没冲击力，改成三下：砸进来 → 停住 → 被一刀劈开 ─────────────
	// 原版时间（实际秒数，速度 2.5）：0～.2 裂缝；.2 图出现；停到 .587；之后原版是慢慢合拢——这里不用，换成劈开。

	/// <summary>
	/// 图出现 / 爪痕劈下（SplitAt 前 ClawTime 秒划过，SplitAt 划完）/ 顿帧之后碎开（ShatterAt）/ 碎片飞完（ShatterTime）。
	/// 舞台、特写、亚森离场、咒焰收掉都对齐这几刻。
	/// </summary>
	public const float ImpactAt = 0.2f, ClawTime = 0.07f, ShatterTime = 0.42f;

	/// <summary>
	/// 横幅裂开的时刻（裂纹定格 0.05s 后碎开）。2026-11-01 用户同意「高潮被拆散了」：每次召唤前由 Aim 设成
	/// 人格面具出手时刻 - 0.05 —— 人格面具出手、眼神特写碎开、伤害结算落在同一拍。
	/// </summary>
	public static float SplitAt = 0.91f;

	public static float ShatterAt => SplitAt + 0.067f;

	/// <summary>碎片从裂开到淡完多久（结城理 mod 的 1.35s 节奏，这里稍短一点不拖）。</summary>
	public const float ShatterLife = 0.5f;                     // 用户 2026-11-03：碎片飘太久压到后面的出手 / 引爆，太花

	/// <summary>
	/// 碎片动画真正要多久（用户 2026-11-08：收尾潦草、碎片直接消失）：原来动画只跑 ShatterLife（0.5s），碎片 0.25s 才开始淡、
	/// 远处的碎片还要晚 0.1～0.27s 才出发 → 动画停的时候碎片还剩三成多不透明、定住，0.7s 时整层被删 = 一下子没了；
	/// 碎片取图的离屏画面 0.6s 就被释放，碎片会先变空白。现在每块碎片都完整淡完，离屏画面留到碎片淡完之后。
	/// </summary>
	public const float ShardLife = 1.05f;

	/// <summary>
	/// 碎纸（舞台和特写共用，2026-11-01：用户嫌「一刀劈成两半滑走」不对）：三道爪痕把画面切成四条，每条再按 4 个点的
	/// Voronoi 分块 → 最多 16 块纸片。shat = 碎开后几秒（&lt; 0 = 还没碎）：每块从自己的中心往外抛（初速快、指数减速，加重力往下掉）、
	/// 打转、绕自己翻面（翻到背面是白纸）。shatterSample 把屏幕上的点反推回碎之前的位置。
	/// </summary>
	public const string ShatterGlsl = @"
uniform float shat = -1.0;
uniform float tseed = 0.0;
uniform vec2 sres = vec2(1920.0, 1080.0);
float sh(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031 + tseed * 0.0137); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
// 三道爪痕：从右上往左下，平行，间距 0.11（屏高单位）
vec2 clawDir() { return normalize(vec2(-1.0, 0.62)); }
vec2 clawN() { vec2 d = clawDir(); return vec2(-d.y, d.x); }
vec2 clawC(float asp) { return vec2(0.56 * asp, 0.47); }
float clawOff(int i) { return (float(i) - 1.0) * 0.11; }
int bandOf(vec2 q, float asp) {
	float b = dot(q - clawC(asp), clawN());
	return b < clawOff(0) ? 0 : b < clawOff(1) ? 1 : b < clawOff(2) ? 2 : 3;
}
vec2 seedOf(int c, float asp) { return vec2((0.12 + 0.76 * sh(vec2(float(c), 1.0))) * asp, 0.12 + 0.76 * sh(vec2(float(c), 2.0))); }
int cellOf(vec2 q, float asp, out float gap) {
	float d1 = 1e9, d2 = 1e9; int best = 0;
	for (int c = 0; c < 6; c++) { float d = length(q - seedOf(c, asp)); if (d < d1) { d2 = d1; d1 = d; best = c; } else if (d < d2) { d2 = d; } }
	gap = (d2 - d1) * 0.5;
	return best;
}
// 离最近的切口多远（Voronoi 边、爪痕线）
float cutDist(vec2 q, float asp) {
	float gap; cellOf(q, asp, gap);
	float b = dot(q - clawC(asp), clawN());
	float dl = min(abs(b - clawOff(0)), min(abs(b - clawOff(1)), abs(b - clawOff(2))));
	return min(gap, dl);
}
bool shatterSample(vec2 uv, out vec2 src, out float back, out float edge) {
	src = uv; back = 0.0; edge = 1.0;
	if (shat < 0.0) return true;
	float asp = sres.x / sres.y;
	vec2 q = vec2(uv.x * asp, uv.y);
	bool hit = false;
	for (int k = 0; k < 4; k++) {
		for (int c = 0; c < 6; c++) {
			float id = float(k * 6 + c);
			// 这一块的中心：这个 Voronoi 点挪到这条带子的中线上
			vec2 sd = seedOf(c, asp);
			float bmid = k == 0 ? clawOff(0) - 0.25 : k == 3 ? clawOff(2) + 0.25 : (clawOff(k - 1) + clawOff(k)) * 0.5;
			float bnow = dot(sd - clawC(asp), clawN());
			vec2 piv = sd + clawN() * (bmid - bnow);
			vec2 out_ = normalize(piv - vec2(0.5 * asp, 0.5) + (vec2(sh(vec2(id, 3.0)), sh(vec2(id, 4.0))) - 0.5) * 0.6);
			float v = 1.1 + 1.1 * sh(vec2(id, 5.0)), t = shat;
			vec2 disp = out_ * v * (1.0 - exp(-4.5 * t)) / 4.5 + vec2(0.0, 3.2 * t * t);
			float spin = (sh(vec2(id, 6.0)) - 0.5) * 12.0;
			float rot = spin * (1.0 - exp(-3.0 * t)) / 3.0;
			float flip = cos((0.3 + 1.2 * sh(vec2(id, 7.0))) * t * 3.14159);     // 多数只翻到一半，少数翻过去露出白背
			if (abs(flip) < 0.04) continue;
			vec2 l = q - piv - disp;
			float cs = cos(-rot), sn = sin(-rot);
			l = vec2(cs * l.x - sn * l.y, sn * l.x + cs * l.y);
			l.x /= flip;
			vec2 s0 = piv + l;
			float gap;
			if (bandOf(s0, asp) == k && cellOf(s0, asp, gap) == c && s0.x > -0.05 && s0.y > -0.05 && s0.x < asp + 0.05 && s0.y < 1.05) {
				hit = true;
				src = vec2(s0.x / asp, s0.y);
				back = flip < 0.0 ? 1.0 : 0.0;
				edge = cutDist(s0, asp);
			}
		}
	}
	return hit;
}
";

	private static Shader? _bandShader;

	/// <summary>
	/// 特写（离屏画面）显示到屏幕：zoom 出现时轻推，shat 碎纸（ShatterGlsl），alpha 最后淡掉。
	/// </summary>
	private static Shader BandShader => _bandShader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D tex : filter_linear;
uniform float zoom = 1.0;
uniform float alpha = 1.0;
uniform vec2 pinch_at = vec2(0.5);
uniform float pinch = 0.0;
" + ShatterGlsl + @"
void fragment() {
	vec2 uv = (UV - 0.5) / zoom + 0.5;
	// 被巨手攥住（毒性爆发，CrushHand）：横幅往拳心皱进去——拳心附近取更远处的画面（往里收），一圈圈褶子压暗
	float fold = 0.0;
	if (pinch > 0.0) {
		vec2 d = (uv - pinch_at) * vec2(sres.x / sres.y, 1.0);
		float r = length(d);
		float ang = atan(d.y, d.x);
		float f = pinch * exp(-r * r / 0.14) * (1.0 + 0.3 * sin(ang * 7.0 + r * 22.0));
		uv = pinch_at + (uv - pinch_at) * (1.0 + f * 1.5);
		fold = pinch * exp(-r * r / 0.1) * (0.5 + 0.5 * sin(ang * 7.0 + r * 22.0));
	}
	vec2 src; float back, edge;
	vec4 c = vec4(0.0);
	if (shatterSample(uv, src, back, edge)) {
		c = texture(tex, src);
		if (c.a > 0.5 && shat >= 0.0) {
			if (back > 0.5) c = vec4(0.82, 0.8, 0.78, 1.0);                         // 翻到背面：白纸（压暗一点）
			if (edge < 0.004) c = vec4(0.985, 0.985, 0.98, 1.0);                   // 撕口白纸边
			else if (edge < 0.007) c = vec4(0.02, 0.02, 0.03, 1.0);
		}
	}
	c.rgb *= 1.0 - fold * 0.45;
	COLOR = vec4(c.rgb, c.a * alpha);
}" };

	/// <summary>毒性爆发的巨手攥住横幅：皱进去多少（0 = 不皱）、拳心在屏幕上的位置（像素）。CrushHand 每帧写，Eyes 每帧读。</summary>
	public static float Pinch;
	public static Vector2 PinchAt;

	/// <summary>碎开的中心（null = 横幅默认位置）。毒性爆发设成拳心。</summary>
	public static Vector2? ShatterFrom;

	/// <summary>放一次眼神特写（背景层，人物、敌人在前面）。原版网格画进离屏画面，再用 BandShader 显示。</summary>
	public static void Eyes(Control back, CutInPlayback? playback = null)
	{
		if (Fx.Tex("eyes") is not Texture2D tex) return;
		Load();
		playback ??= new CutInPlayback(SplitAt);
		Clip clip = _clip!;
		Prepared[] parts = _parts!;
		Vector2 screen = Fx.Screen(back);
		float life = playback.SplitAt + ShardLife + 0.15f;            // 离屏画面（碎片的图）要活到碎片淡完
		// 离屏画面：和屏幕一样大，透明底
		SubViewport view = new()
		{
			Size = new Vector2I(Mathf.CeilToInt(screen.X), Mathf.CeilToInt(screen.Y)), TransparentBg = true, Disable3D = true,
			RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
		};
		back.AddChild(view);

		Node2D root = new();
		view.AddChild(root);
		// 原版带子缩到 82%、往上挪一点（用户 2026-11-01：铺满太大、缩小两头又空）；两头接上 Codex 画的撕纸延伸段，把带子接到屏幕边缘
		float scale = Mathf.Max(screen.X / clip.Canvas[0], screen.Y / clip.Canvas[1]) * 0.82f;
		root.Position = (screen - new Vector2(clip.Canvas[0], clip.Canvas[1]) * scale) * 0.5f + new Vector2(0f, -screen.Y * 0.06f);
		Node2D[] tails = Tails(root, clip.Canvas[0]);
		root.Scale = Vector2.One * scale;
		MeshInstance2D?[] nodes = new MeshInstance2D?[parts.Length];
		for (int i = 0; i < parts.Length; i++)
		{
			Prepared p = parts[i];
			// 原版那层线性淡出的白闪：缩小后会变成一个白方框，白闪交给 SummonStage
			if (p.Part.Linear)
			{
				continue;
			}
			MeshInstance2D node = new() { Mesh = p.Mesh, Visible = false };
			if (p.Part.Textured)
			{
				node.Texture = tex;
				node.Material = _opaqueEyes;
				node.TextureRepeat = CanvasItem.TextureRepeatEnum.Enabled;
			}
			root.AddChild(node);
			nodes[i] = node;
		}
		foreach (Node2D tl in tails)
		{
			root.MoveChild(tl, root.GetChildCount() - 1);          // 延伸段在原版网格上面：盖住带子两端的竖直切边
		}
		// 显示：一张铺满屏幕的图，用 BandShader
		Node2D shown = Fx.ScreenRoot(back, "P5CutIn");
		playback.PaperRoot = shown;
		shown.TreeExiting += () => { if (GodotObject.IsInstanceValid(view)) view.QueueFree(); };
		back.MoveChild(shown, back.GetChildCount() - 1);
		ShaderMaterial m = new() { Shader = BandShader };
		m.SetShaderParameter("tseed", playback.Seed);
		m.SetShaderParameter("sres", screen);
		m.SetShaderParameter("tex", view.GetTexture());
		Sprite2D sprite = new() { Texture = view.GetTexture(), Centered = false, Material = m };
		shown.AddChild(sprite);
		bool hit = false, shattered = false;
		Rect2 bandRect = new(new Vector2(0f, root.Position.Y), new Vector2(screen.X, clip.Canvas[1] * scale));     // 连两头的延伸段一起碎
		Fx.Play(shown, playback, life, s =>
		{
			// 原版动画只放到停住那一帧（不放原版的合拢）
			float time = Mathf.Min(s * clip.Speed, 1.45f);
			for (int i = 0; i < parts.Length; i++)
			{
				if (nodes[i] is not MeshInstance2D node)
				{
					continue;
				}
				float alpha = Opacity(parts[i].Part, time);
				node.Visible = alpha > 0.0001f;
				node.Modulate = new Color(1f, 1f, 1f, alpha);
			}
			// 图出现时轻轻推一下（1.08 → 1，0.16s 缓出），不做黑白反相（用户 2026-11-01：入场太生硬）
			// 延伸段：图出现前一点开始，从带子两端往屏幕边缘「撕」出去（0.12s 缓出）
			float ext = Fx.OutCubic(Mathf.Clamp((s - ImpactAt + 0.04f) / 0.12f, 0f, 1f));
			foreach (Node2D tl in tails)
			{
				tl.Visible = ext > 0f;
				tl.Scale = new Vector2((float)tl.GetMeta("k") * ext, (float)tl.GetMeta("k"));
			}
			float k = Mathf.Clamp((s - ImpactAt) / 0.16f, 0f, 1f);
			m.SetShaderParameter("zoom", s < ImpactAt ? 1f : Mathf.Lerp(1.08f, 1f, Fx.OutCubic(k)));
			m.SetShaderParameter("pinch", playback.Pinch);
			m.SetShaderParameter("pinch_at", playback.PinchAt / screen);
			if (!hit && s >= ImpactAt)
			{
				hit = true;
				Sfx.Play(Sfx.OneMore, 1.1f);                         // 原版弱点眼神特写的音（用户 2026-11-03 试过 battle_cutin，又换回来）
			}
			// 碎开（用户 2026-11-01：复用结城理 mod 的玻璃碎裂，不要爪痕）：先裂纹定格 0.05s，然后横幅换成一块块碎片炸开；亚森离场、咒焰收掉、背景退掉都在碎开这一刻
			if (!shattered && s >= playback.SplitAt)
			{
				shattered = true;
				Shatter(back, view.GetTexture(), screen, bandRect, () => sprite.Visible = false, playback.ShatterFrom, playback.Crush ? 0f : 0.05f,
					playback.Pinch, playback.PinchAt / screen);
			}
		});
	}

	private static Shader? _tailShader;

	/// <summary>延伸段：接口那一头（fade 那一截）是一道锯齿状的硬撕边（不是柔边渐变，P5 都是硬边），盖住原版带子的竖直切边。</summary>
	private static Shader TailShader => _tailShader ??= new Shader { Code = @"
shader_type canvas_item;
uniform float join = 1.0;     // 接口在贴图的哪一边（1 = 右边，0 = 左边）
uniform float fade = 0.1;     // 渐变多宽（贴图宽度的比例）
uniform float seed = 0.0;
float h(float x) { vec2 p = fract(vec2(x, seed) * vec2(0.1031, 0.1030)); p += dot(p, p.yx + 33.33); return fract((p.x + p.y) * p.x); }
float vn(float x) { float i = floor(x); float f = fract(x); return mix(h(i), h(i + 1.0), f * f * (3.0 - 2.0 * f)); }
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	float d = abs(UV.x - join);
	// 接口不是一道柔边渐变（糊），而是一道撕开的硬边：沿 y 方向锯齿起伏
	float y = UV.y * 60.0;
	float edge = fade * (0.2 + 0.55 * vn(y) + 0.25 * vn(y * 4.3 + 11.0));
	COLOR = vec4(c.rgb, c.a * step(edge, d));
}" };

	/// <summary>
	/// 带子两头的撕纸延伸段（Codex 画的 cutin_tail_left / right，原版画布坐标）。原版带子两端是一道竖直的切边（用户：很明显的分割），
	/// 所以延伸段盖在原版带子上面、往里伸进去一截（overlap，原版画布单位：左 90，右端挨着「!!」只伸 30），接口那一截渐变透明：原版的竖直切边被盖住，两段融在一起。
	/// 左尾：贴图右边缘带身 y 121～529，对齐原版 x=0 处带身 y 400～575；右尾：贴图左边缘带身 y 511～814，对齐 x=1280 处 y 100～440。
	/// 原点放在接口处，横向缩放就是「撕出去」。
	/// </summary>
	private static Node2D[] Tails(Node2D root, float canvasW)
	{
		var list = new List<Node2D>();
		void Add(string name, float joinX, float y0, float y1, float texY0, float texY1, bool left, float overlap)
		{
			if (Fx.Tex(name) is not Texture2D t)
			{
				return;
			}
			float k = (y1 - y0) / (texY1 - texY0);
			Node2D n = new() { Position = new Vector2(joinX, y0 - texY0 * k), Visible = false };
			n.SetMeta("k", k);
			ShaderMaterial m = new() { Shader = TailShader };
			m.SetShaderParameter("join", left ? 1f : 0f);
			m.SetShaderParameter("fade", overlap * 0.9f / k / t.GetWidth());
			m.SetShaderParameter("seed", Fx.Rand(0f, 100f));
			float shift = overlap / k;
			n.AddChild(new Sprite2D { Texture = t, Centered = false, Material = m, Offset = new Vector2(left ? -t.GetWidth() + shift : -shift, 0f) });
			root.AddChild(n);                                       // 在原版带子上面（盖住它两端的竖直切边）
			list.Add(n);
		}
		Add("cutin_tail_left", 0f, 400f, 575f, 121f, 529f, true, 90f);
		Add("cutin_tail_right", canvasW, 100f, 440f, 511f, 814f, false, 30f);   // 右端紧挨着「!!」，只盖一小截
		return list.ToArray();
	}

	// ───────────── 玻璃碎裂（移植自结城理 mod 的 mod/src/Vfx/CutInFx.cs Shatter，配色换成红白） ─────────────

	private static Shader? _shardShader, _maskShader;

	/// <summary>碎片：横幅贴图的切块（UV 对应），glint 一闪的白光（只在有图的地方）。</summary>
	private static Shader ShardShader => _shardShader ??= new Shader { Code = @"
shader_type canvas_item;
uniform float glint = 0.0;
uniform float phase = 0.0;
// 碎开那一刻横幅被巨手攥住的形变（和 BandShader 同一套）：碎片按碎之前的样子取图，不会一下子弹回没被攥过的形状（用户 2026-11-08 的 bug）
uniform vec2 sres = vec2(1920.0, 1080.0);
uniform vec2 pinch_at = vec2(0.5);
uniform float pinch = 0.0;
void fragment() {
	vec2 uv = UV;
	float fold = 0.0;
	if (pinch > 0.0) {
		vec2 d = (uv - pinch_at) * vec2(sres.x / sres.y, 1.0);
		float r = length(d);
		float ang = atan(d.y, d.x);
		float f = pinch * exp(-r * r / 0.14) * (1.0 + 0.3 * sin(ang * 7.0 + r * 22.0));
		uv = pinch_at + (uv - pinch_at) * (1.0 + f * 1.5);
		fold = pinch * exp(-r * r / 0.1) * (0.5 + 0.5 * sin(ang * 7.0 + r * 22.0));
	}
	vec4 c = texture(TEXTURE, uv);
	c.rgb *= 1.0 - fold * 0.45;
	// 玻璃：一道斜的高光随碎片转动扫过、整体偏冷一点、稍透
	float sheen = exp(-pow((UV.x + UV.y * 0.55 - phase) / 0.025, 2.0)) * 0.9 + exp(-pow((UV.x + UV.y * 0.55 - phase - 0.06) / 0.008, 2.0)) * 0.6;
	c.rgb = mix(c.rgb, c.rgb * vec3(0.92, 0.96, 1.05), 0.4) + vec3(1.0, 0.95, 0.95) * (glint + sheen) * c.a;
	c.a *= 0.92;
	COLOR = c * COLOR;
}" };

	/// <summary>碎片的玻璃亮边：只画在横幅有图的地方（按碎片原来的位置读离屏画面的不透明度）。</summary>
	private static Shader? _rimShader;

	private static Shader RimShader => _rimShader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform sampler2D mask : filter_linear;
uniform vec2 origin = vec2(0.0);
uniform vec2 res = vec2(1920.0, 1080.0);
varying vec2 buv;
void vertex() { buv = (VERTEX + origin) / res; }
void fragment() { COLOR = vec4(COLOR.rgb, COLOR.a * step(0.5, texture(mask, buv).a)); }
" };

	/// <summary>裂纹线：只画在横幅有图的地方（读离屏画面的不透明度）。</summary>
	private static Shader MaskShader => _maskShader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_add;
uniform sampler2D mask : filter_linear;
void fragment() { COLOR = vec4(COLOR.rgb, COLOR.a * step(0.5, texture(mask, SCREEN_UV).a)); }
" };

	private static float Hash(int k, float a, float b)
	{
		float v = Mathf.Sin(k * 12.9898f) * 43758.5453f;
		return a + (b - a) * (v - Mathf.Floor(v));
	}

	/// <summary>横幅从冲击点放射状裂开，0.05s 裂纹定格（白裂纹 + 白闪），然后碎成大块玻璃炸开、迅速减速、慢慢转着漂、一部分飞向镜头放大、淡出。</summary>
	private static void Shatter(Control back, Texture2D tex, Vector2 screen, Rect2 band, System.Action hideBand, Vector2? shatterFrom, float crackDelay,
		float pinch = 0f, Vector2 pinchAt = default)
	{
		Vector2 hit = shatterFrom ?? band.Position + band.Size * new Vector2(0.42f, 0.52f);      // 毒性爆发：从拳心碎开
		Vector2[] rect = { band.Position, band.Position + new Vector2(band.Size.X, 0f), band.End, band.Position + new Vector2(0f, band.Size.Y) };
		float reach = screen.Length();
		float[] rings = { 0f, 70f, 160f, 290f, 460f, 700f, 1000f, 1400f, reach + 200f };
		const int slices = 16;
		float a0 = Fx.Rand(0f, Mathf.Tau);
		var jitterA = new float[slices];
		for (int j = 0; j < slices; j++) jitterA[j] = a0 + Mathf.Tau * j / slices + Fx.Rand(-0.12f, 0.12f);
		Vector2 P(int ring, int slice)
		{
			float a = jitterA[(slice % slices + slices) % slices] + (ring % 2) * 0.09f;
			float r = rings[ring] * (ring == 0 ? 0f : Hash(ring * 131 + slice, 0.85f, 1.15f));
			return hit + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
		}
		var pieces = new List<Vector2[]>();
		for (int r = 0; r < rings.Length - 1; r++)
		{
			for (int j = 0; j < slices; j++)
			{
				Vector2[] cell = r == 0 ? new[] { hit, P(1, j), P(1, j + 1) } : new[] { P(r, j), P(r + 1, j), P(r + 1, j + 1), P(r, j + 1) };
				foreach (Vector2[] piece in Geometry2D.IntersectPolygons(cell, rect))
				{
					pieces.Add(piece);
				}
			}
		}
		Node2D root = Fx.ScreenRoot(back, "P5CutInShatter");
		Fx.FreeAfter(root, crackDelay + ShardLife + 0.1);
		back.MoveChild(root, back.GetChildCount() - 1);
		// 裂纹定格：白裂纹（只在横幅上）
		Node2D cracks = new();
		root.AddChild(cracks);
		ShaderMaterial mask = new() { Shader = MaskShader };
		mask.SetShaderParameter("mask", tex);
		foreach (Vector2[] piece in pieces)
		{
			var pts = new Vector2[piece.Length + 1];
			piece.CopyTo(pts, 0);
			pts[^1] = piece[0];
			cracks.AddChild(new Line2D { Points = pts, Width = 3f, DefaultColor = new Color(1f, 0.96f, 0.94f), Material = mask, JointMode = Line2D.LineJointMode.Sharp });
		}
		Fx.Animate(cracks, 0, 0.16, t => cracks.Modulate = new Color(1f, 1f, 1f, 1f - Fx.InCubic(t)));
		const float total = ShardLife, tau = 0.2f;
		Fx.After(root, crackDelay, () =>
		{
			hideBand();
			Sfx.Play(Sfx.EvokerShatter, 1.1f);                       // P3 mod 召唤器那一枪的碎裂声（用户 2026-11-03）                             // 「啪」地碎开（用户 2026-11-03：原来那声不明显、没有震撼感）
			Control light = new();
			root.AddChild(light);
			Glow.Flash(light, root.ToGlobal(hit), Pal5.Hot, screen.X * 0.35f, 0.18f);
			foreach (Vector2[] piece in pieces)
			{
				Vector2 c = Vector2.Zero;
				foreach (Vector2 v in piece) c += v;
				c /= piece.Length;
				var local = new Vector2[piece.Length];
				for (int i = 0; i < piece.Length; i++) local[i] = piece[i] - c;
				Node2D shard = new() { Position = c };
				root.AddChild(shard);
				ShaderMaterial sm = new() { Shader = ShardShader };
				sm.SetShaderParameter("sres", screen);
				sm.SetShaderParameter("pinch", pinch);
				sm.SetShaderParameter("pinch_at", pinchAt);
				shard.AddChild(new Polygon2D { Polygon = local, UV = piece, Texture = tex, TextureFilter = CanvasItem.TextureFilterEnum.Linear, Material = sm });
				// 玻璃亮边：外面一道白、里面一道细红（加法，只在有图的地方）
				var loop = new Vector2[local.Length + 1];
				local.CopyTo(loop, 0);
				loop[^1] = local[0];
				ShaderMaterial rm = new() { Shader = RimShader };
				rm.SetShaderParameter("mask", tex);
				rm.SetShaderParameter("origin", c);
				rm.SetShaderParameter("res", screen);
				Line2D rim = new() { Points = loop, Width = 3f, DefaultColor = new Color(1f, 0.92f, 0.92f, 0.9f), Material = rm, JointMode = Line2D.LineJointMode.Sharp };
				shard.AddChild(rim);
				float ph0 = Fx.Rand(-0.3f, 0.6f);
				Vector2 dir = (c - hit).Normalized();
				float dist = (c - hit).Length();
				Vector2 vel = dir * (1500f + dist * 2.4f) * Fx.Rand(0.75f, 1.25f) + new Vector2(0f, Fx.Rand(-500f, 150f));
				float spin = Fx.Rand(-9f, 9f);
				float grow = Fx.Rand(0f, 1f) < 0.4f ? Fx.Rand(0.3f, 0.9f) : Fx.Rand(-0.3f, 0.1f);   // 一部分飞向镜头（放大）
				float delay = dist / 7000f;
				Vector2 p0 = c;
				Fx.Animate(shard, 0, total, t =>
				{
					float tt = t * total - delay;
					if (tt < 0f) return;
					float burst = tau * (1f - Mathf.Exp(-tt / tau));                 // 爆发后迅速减速
					shard.Position = p0 + vel * burst + vel * 0.06f * tt + new Vector2(0f, 90f * tt * tt);
					shard.Rotation = spin * burst + spin * 0.12f * tt;
					shard.Scale = Vector2.One * (1f + grow * Fx.OutCubic(Mathf.Clamp(tt / 0.9f, 0f, 1f)));
					float flick = Mathf.Pow(Mathf.Abs(Mathf.Sin(shard.Rotation * 2f + spin)), 6f);   // 转到某个角度反光一闪
					sm.SetShaderParameter("glint", Mathf.Clamp(1f - tt / 0.15f, 0f, 1f) * 0.7f + 0.45f * flick);
					sm.SetShaderParameter("phase", ph0 + shard.Rotation * 0.35f);
					rim.Modulate = new Color(1f, 1f, 1f, 0.55f + 0.45f * flick);
					// 0.2s 起淡，0.45s 里淡完（每块按自己的出发时刻算；最晚出发的 ~0.27s 也在 total 里淡完），淡的同时缩一点、往下沉
					float k = Mathf.Clamp((tt - 0.2f) / 0.45f, 0f, 1f);
					float fade = k * k * (3f - 2f * k);
					shard.Modulate = new Color(1f, 1f, 1f, 1f - fade);
					shard.Scale *= 1f - 0.25f * fade;
				});
			}
			Glow.Sparks(light, root.ToGlobal(hit), 30, 300f, 1100f, 0.6f, new[] { Pal5.Hot, Pal5.Bright, Pal5.Deep }, 300f, 0.1f, 0.35f);
		});
	}
}
