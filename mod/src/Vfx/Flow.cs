using System;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using P3Makoto.Core;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;

namespace P3Makoto.Vfx;

/// <summary>
/// P3R 的"水流"图形语言（v0.10.1）：大圆弧 / 斜条像一股水流冲过画面。
/// · 同一个形状复制成几层（深蓝底浪 → 青色中浪 → 白色高光线 → 浅蓝余波），错开几帧出发，层与层之间有流速差 —— 层次感来自"同形重复"，不是堆不同的图形；
/// · 前沿极快冲出（OutExpo）、尾巴随后追上去（InOutCubic）：整条带子顺着同一方向流走，不原地淡出；
/// · 带子沿流向渐变（尾淡头亮），尾端渐细，内部有缓慢流动的焦散光，前沿一条白色亮边；半透明，透出后面的战斗画面；
/// · 沿途从前沿溅出细小水光。
/// 实现：路径（圆弧或直线）预先铺成一条三角带网格，UV.x = 沿路径 0→1，UV.y = 横向 0→1；
/// 着色器只按 head / tail 两个参数决定显示哪一段，每帧只改参数，不重建网格。
/// </summary>
public static class Flow
{
	private const string ShaderCode = @"
uniform vec4 col_tail : source_color = vec4(0.05, 0.2, 0.9, 0.0);
uniform vec4 col_head : source_color = vec4(0.3, 0.75, 1.0, 1.0);
uniform float head = 0.0;
uniform float tail = 0.0;
uniform float taper = 0.25;        // 尾端剩下的宽度比例
uniform float edge_glow = 0.6;     // 前沿白边亮度
uniform float caustic_amt = 0.25;  // 内部焦散光
uniform float alpha = 1.0;
varying vec2 uv;
void vertex() { uv = UV; }
float caustic(vec2 p, float t) {
	vec2 i = p;
	float c = 1.0;
	float inten = 0.005;
	for (int n = 0; n < 4; n++) {
		float tt = t * (1.0 - (3.5 / float(n + 1)));
		i = p + vec2(cos(tt - i.x) + sin(tt + i.y), sin(tt - i.y) + cos(tt + i.x));
		float sx = sin(i.x + tt), cy = cos(i.y + tt);
		sx = (sx < 0.0 ? -1.0 : 1.0) * max(abs(sx), 1e-3);
		cy = (cy < 0.0 ? -1.0 : 1.0) * max(abs(cy), 1e-3);
		c += 1.0 / max(length(vec2(p.x / (sx / inten), p.y / (cy / inten))), 1e-4);
	}
	c /= 4.0;
	c = 1.17 - pow(clamp(c, 0.0, 50.0), 1.4);
	return clamp(pow(abs(c), 8.0), 0.0, 4.0);
}
void fragment() {
	float u = uv.x;
	if (u > head || u < tail) { discard; }
	float span = max(head - tail, 1e-4);
	float k = (u - tail) / span;                       // 0 = 尾，1 = 前沿
	// 新月形截面：尾端收细、中段饱满、前沿收成尖浪头
	float half_w = 0.5 * mix(taper, 1.0, smoothstep(0.0, 0.55, k)) * mix(1.0, 0.12, smoothstep(0.8, 1.0, k));
	float d = abs(uv.y - 0.5);
	float pv = max(fwidth(uv.y), 1e-5);
	float across = 1.0 - smoothstep(half_w - pv * 1.5, half_w, d);
	float pu = max(fwidth(u), 1e-5);
	float head_aa = 1.0 - smoothstep(head - pu * 1.5, head, u);
	// 水的体积：外缘（uv.y = 1 一侧）不透明、发亮，内缘逐渐透明
	float side = clamp((uv.y - (0.5 - half_w)) / max(2.0 * half_w, 1e-4), 0.0, 1.0);
	float body = mix(0.3, 1.0, smoothstep(0.0, 0.85, side));
	float lip = smoothstep(half_w - pv * 7.0, half_w - pv * 1.5, d) * step(0.5, uv.y) * 0.55;
	float rim = exp(-(head - u) / (pu * 14.0)) * edge_glow;
	// 顺着流向高速流动的细光丝
	float strand = pow(0.5 + 0.5 * sin(uv.y * 70.0 + sin(u * 9.0 - TIME * 7.0) * 2.2), 24.0) * 0.35 * k;
	vec4 c = mix(col_tail, col_head, smoothstep(0.0, 1.0, k));
	float cs = clamp(caustic(vec2(u * 18.0 - TIME * 0.9, uv.y * 2.4), TIME * 0.6), 0.0, 1.0);
	c.rgb += vec3(0.55, 0.85, 1.0) * (cs * caustic_amt + strand) * k;
	c.rgb += vec3(rim + lip * k);
	float fade = smoothstep(0.0, 0.18, span);          // 尾巴追上浪头、带子变短时一起淡掉，不留色块
	float a = c.a * body * smoothstep(0.0, 0.35, k) * across * head_aa * alpha * fade;
	COLOR = vec4(c.rgb, clamp(a + (rim + lip * k) * across * head_aa * alpha * fade * 0.6, 0.0, 1.0));
}";

	private static Shader? _mix, _add;
	private static Shader MixShader => _mix ??= new Shader { Code = "shader_type canvas_item;\n" + ShaderCode };
	private static Shader AddShader => _add ??= new Shader { Code = "shader_type canvas_item;\nrender_mode blend_add;\n" + ShaderCode };

	/// <summary>一层水流的外观与时间。</summary>
	public sealed record Layer(
		float Offset,          // 相对主路径的横向偏移（圆弧为半径偏移，直线为法向偏移）
		float Width,
		Color Tail,
		Color Head,
		float Delay,           // 出发延迟（秒）
		float InTime,          // 前沿冲完全程的时间
		float OutDelay,        // 尾巴开始追的时间（相对本层出发）
		float OutTime,         // 尾巴追完的时间
		bool Additive = false,
		float Taper = 0.25f,
		float EdgeGlow = 0.6f,
		float Caustic = 0.25f);

	/// <summary>四层标准水流（宽度 w）。</summary>
	public static Layer[] Standard(float w, float speed = 1f) => new[]
	{
		// 深蓝底浪：最宽、最慢、半透明
		new Layer(0f, w, new Color(0.02f, 0.08f, 0.45f, 0f), new Color(0.08f, 0.28f, 0.95f, 0.62f), 0f, 0.42f / speed, 0.2f / speed, 0.62f / speed, Caustic: 0.35f, EdgeGlow: 0.25f),
		// 青色中浪：稍窄、晚 2~3 帧、加色
		new Layer(w * 0.18f, w * 0.55f, new Color(0.1f, 0.5f, 1f, 0f), new Color(0.35f, 0.85f, 1f, 0.55f), 0.04f / speed, 0.36f / speed, 0.16f / speed, 0.55f / speed, Additive: true, Caustic: 0.2f, EdgeGlow: 0.5f),
		// 白色高光线：贴外缘、最快
		new Layer(w * 0.47f, Mathf.Max(3f, w * 0.05f), new Color(0.7f, 0.9f, 1f, 0f), new Color(1f, 1f, 1f, 0.95f), 0.02f / speed, 0.3f / speed, 0.12f / speed, 0.45f / speed, Additive: true, Taper: 0.1f, EdgeGlow: 1f, Caustic: 0f),
		// 浅蓝余波：内侧、最晚、最淡
		new Layer(-w * 0.62f, w * 0.32f, new Color(0.3f, 0.6f, 1f, 0f), new Color(0.6f, 0.85f, 1f, 0.38f), 0.1f / speed, 0.5f / speed, 0.22f / speed, 0.6f / speed, Additive: true, Caustic: 0.15f, EdgeGlow: 0.35f),
	};

	// ───────── 路径 ─────────

	/// <summary>圆弧路径：圆心 center、半径 radius，从角度 a0 走到 a1（弧度，Godot 坐标 y 向下）。</summary>
	public static Func<float, float, (Vector2 pos, Vector2 normal)> Arc(Vector2 center, float radius, float a0, float a1) => (u, off) =>
	{
		float a = Mathf.Lerp(a0, a1, u);
		Vector2 n = new(Mathf.Cos(a), Mathf.Sin(a));
		return (center + n * (radius + off), n);
	};

	/// <summary>直线路径（斜条）：from → to。</summary>
	public static Func<float, float, (Vector2 pos, Vector2 normal)> Line(Vector2 from, Vector2 to) => (u, off) =>
	{
		Vector2 d = (to - from).Normalized();
		Vector2 n = new(-d.Y, d.X);
		return (from.Lerp(to, u) + n * off, n);
	};

	private static MeshInstance2D Strip(Node parent, Func<float, float, (Vector2 pos, Vector2 normal)> path, Layer l, int segments)
	{
		Vector2[] verts = new Vector2[(segments + 1) * 2];
		Vector2[] uvs = new Vector2[verts.Length];
		for (int i = 0; i <= segments; i++)
		{
			float u = i / (float)segments;
			(Vector2 p, Vector2 n) = path(u, l.Offset);
			verts[i * 2] = p - n * l.Width * 0.5f;
			verts[i * 2 + 1] = p + n * l.Width * 0.5f;
			uvs[i * 2] = new Vector2(u, 0f);
			uvs[i * 2 + 1] = new Vector2(u, 1f);
		}
		Godot.Collections.Array arrays = new();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = verts;
		arrays[(int)Mesh.ArrayType.TexUV] = uvs;
		ArrayMesh mesh = new();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.TriangleStrip, arrays);
		ShaderMaterial mat = new() { Shader = l.Additive ? AddShader : MixShader };
		mat.SetShaderParameter("col_tail", l.Tail);
		mat.SetShaderParameter("col_head", l.Head);
		mat.SetShaderParameter("taper", l.Taper);
		mat.SetShaderParameter("edge_glow", l.EdgeGlow);
		mat.SetShaderParameter("caustic_amt", l.Caustic);
		mat.SetShaderParameter("head", 0f);
		mat.SetShaderParameter("tail", 0f);
		MeshInstance2D mi = new() { Mesh = mesh, Material = mat };
		parent.AddChild(mi);
		return mi;
	}

	/// <summary>
	/// 一股水流：layers 各自沿 path 冲出、流走。返回总时长（秒）。
	/// spray：前沿沿途溅出的水光数量（0 不溅）。坐标是 layer 上的全局坐标。
	/// </summary>
	public static float Wave(Control layer, Func<float, float, (Vector2 pos, Vector2 normal)> path, Layer[] layers, int segments = 96, int spray = 3, string name = "P3Flow")
	{
		Node2D root = new() { Name = name };
		layer.AddChild(root);
		root.GlobalPosition = Vector2.Zero;
		float total = 0f;
		foreach (Layer l in layers)
		{
			total = Mathf.Max(total, l.Delay + l.OutDelay + l.OutTime);
		}
		ShaderMaterial[] mats = new ShaderMaterial[layers.Length];
		for (int i = 0; i < layers.Length; i++)
		{
			mats[i] = (ShaderMaterial)Strip(root, path, layers[i], segments).Material;
		}
		Fx.Animate(root, total, t =>
		{
			float time = t * total;
			for (int i = 0; i < layers.Length; i++)
			{
				Layer l = layers[i];
				float lt = time - l.Delay;
				float head = Ease.OutExpo(Mathf.Clamp(lt / l.InTime, 0f, 1f));
				float tail = Ease.InOutCubic(Mathf.Clamp((lt - l.OutDelay) / l.OutTime, 0f, 1f));
				mats[i].SetShaderParameter("head", lt <= 0f ? 0f : head);
				mats[i].SetShaderParameter("tail", Mathf.Min(tail, head));
			}
		});
		// 溅起的水光：在前沿刚冲过的几处，喷出一小撮细光点（顺着流向，向外侧散开）
		Layer main = layers[0];
		for (int k = 0; k < spray; k++)
		{
			float u = 0.35f + 0.5f * k / Mathf.Max(1, spray - 1);
			float lt = main.InTime * Mathf.Log(1f / (1f - Mathf.Min(u, 0.99f))) / Mathf.Log(1024f);   // OutExpo 的反函数：前沿到达 u 的时刻
			Fx.After(root, main.Delay + lt, "flow spray", () =>
			{
				if (!GodotObject.IsInstanceValid(root)) return;
				(Vector2 p, Vector2 n) = path(u, main.Offset + main.Width * 0.45f);
				Vector2 along = (path(Mathf.Min(1f, u + 0.01f), 0f).pos - path(u, 0f).pos).Normalized();
				Node2D at = new() { Position = p };
				root.AddChild(at);
				GpuParticles2D sp = Fx.Sparks(at, new Color(0.75f, 0.92f, 1f), 14, 120f, 380f, 0.55f, gravity: 260f, scaleMin: 0.05f, scaleMax: 0.16f,
					ramp: new[] { Pal.White, Pal.Ice, Pal.Cyan });
				ParticleProcessMaterial pm = (ParticleProcessMaterial)sp.ProcessMaterial;
				Vector2 dir = (along + n * 0.8f).Normalized();
				pm.Direction = new Vector3(dir.X, dir.Y, 0f);
				pm.Spread = 28f;
			});
		}
		Fx.FreeAfter(root, total + 0.1);
		return total;
	}

	private static void Shake(ShakeStrength s, float angle = 0f) => NGame.Instance?.ScreenShake(s, ShakeDuration.Short, angle);

	// ═════════ 试播（测试工具的按钮通过反射调用，不挂在任何卡牌上）═════════

	/// <summary>在当前战斗里直接播放一个水流特效：which = "arc"（出牌圆弧）/ "bar"（重击斜条）。返回说明文字。</summary>
	public static string Demo(string which)
	{
		CombatState? state = CombatManager.Instance is { IsInProgress: true } cm ? cm.DebugOnlyGetState() : null;
		if (state == null) return "不在战斗中";
		NCreature? me = Makoto.Node(LocalContext.GetMe(state));
		Control? front = Makoto.FxLayer(), back = Makoto.FxLayer(back: true);
		if (me == null || front == null || back == null) return "找不到结城理";
		switch (which)
		{
			case "arc":
				CastArc(back, Makoto.Center(me), Makoto.Head(me));
				return "试播：水流圆弧";
			case "bar":
				NCreature? foe = state.HittableEnemies.Count > 0 ? NCombatRoom.Instance?.GetCreatureNode(state.HittableEnemies[0]) : null;
				if (foe == null) return "没有敌人";
				StrikeBar(front, foe.VfxSpawnPosition);
				return "试播：水流斜条";
			default:
				return "未知特效 " + which;
		}
	}

	// ═════════ 成品 ═════════

	/// <summary>
	/// 出牌：一道大圆弧从人物身后左下方扫起、越过头顶、向右上方流走（放在人物身后的图层）。
	/// center / head：人物中心与头顶；s：规模。
	/// </summary>
	public static void CastArc(Control back, Vector2 center, Vector2 head, float s = 1f)
	{
		// 巨大的圆弧：圆心远在画面下方，弧顶擦过头顶上方 70px，从画面左侧一直扫到人物右前方
		float radius = 1050f * s;
		Vector2 c = new(center.X + 160f * s, head.Y - 70f * s + radius);
		float w = 150f * s;
		Wave(back, Arc(c, radius, Mathf.DegToRad(203f), Mathf.DegToRad(322f)), Standard(w), segments: 128, spray: 3, name: "P3CastArc");
		// 外圈第二道浪：更细、更淡、晚 0.07 秒
		Flow.Layer[] outer =
		{
			new(0f, w * 0.4f, new Color(0.05f, 0.2f, 0.8f, 0f), new Color(0.25f, 0.6f, 1f, 0.4f), 0.07f, 0.5f, 0.24f, 0.62f, Additive: true, Caustic: 0.3f, EdgeGlow: 0.45f),
			new(w * 0.16f, Mathf.Max(3f, w * 0.03f), new Color(0.7f, 0.9f, 1f, 0f), new Color(1f, 1f, 1f, 0.75f), 0.08f, 0.4f, 0.18f, 0.5f, Additive: true, Taper: 0.1f, EdgeGlow: 0.9f, Caustic: 0f),
		};
		Wave(back, Arc(c, radius + w * 1.05f, Mathf.DegToRad(198f), Mathf.DegToRad(328f)), outer, segments: 128, spray: 0, name: "P3CastArcOuter");
	}

	/// <summary>
	/// 重击：一条斜条横穿敌人（左下 → 右上），冲到中间顿一下，随后分成两道更细的余波继续流走。
	/// </summary>
	public static void StrikeBar(Control front, Vector2 target, float s = 1f, bool hitStop = true)
	{
		float len = 1300f * s;
		Vector2 dir = new Vector2(1f, -0.36f).Normalized();
		Vector2 from = target - dir * len * 0.5f, to = target + dir * len * 0.5f;
		float w = 90f * s;
		Wave(front, Line(from, to), Standard(w, 1.25f), spray: 2, name: "P3StrikeBar");
		// 余波：主条两侧各一道细浪，晚 0.08 秒，稍微张开
		Vector2 n = new(-dir.Y, dir.X);
		foreach (float side in new[] { -1f, 1f })
		{
			Flow.Layer[] echo =
			{
				new(0f, w * 0.22f, new Color(0.2f, 0.5f, 1f, 0f), new Color(0.55f, 0.85f, 1f, 0.45f), 0.08f, 0.32f, 0.12f, 0.42f, Additive: true, Caustic: 0.1f, EdgeGlow: 0.6f),
			};
			Vector2 off = n * side * w * 0.95f;
			Wave(front, Line(from + off - dir * 60f, to + off + n * side * 40f), echo, spray: 0, name: "P3StrikeEcho");
		}
		Fx.After(front, 0.1, "strike bar peak", () =>
		{
			if (hitStop) EvokerShot.HitStop(0.05);
			Shake(ShakeStrength.Medium, Mathf.RadToDeg(dir.Angle()));
			P3Fx.Flash(front, target, Pal.Ice, 300f * s, 0.18f);
		});
	}
}
