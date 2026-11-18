using Godot;

namespace P5Joker;

/// <summary>
/// 咒怨裂隙（2026-11-18，致命毒药 / 咕嘟冒泡；用户：爪击其实不错，但是少了从人物 / 人格面具到怪物的关系特效表达。
/// 之前的咒怨光束「太直白」、光点砸过去「很丑」、紧勒从人物连绳子过去「太直白」→ 不在两边之间连任何东西）：
/// 亚森挥爪的那一刻，他爪前的空间被「划开」一道裂口（从一头裂到另一头、再张开），同一刻敌人身上裂开一道一模一样的口子，
/// 爪痕从那道口子里撕出来划过敌人 —— 两道裂口一模一样、同时裂开、同时合上：「这一爪穿过空间抓到了他」。
/// 裂口：里面是近黑的虚空、暗红的烟顺着裂口往里吸（爪前那道）/ 往外喷（敌人那道），边缘一圈锯齿状的暗红热边，张开那一下边上白热一闪。
/// 全部着色器画的，预乘混合（虚空压暗背景、热边往上加）。
/// </summary>
public static class CurseRift
{
	private static Shader? _shader;

	private static Shader Shade => _shader ??= new Shader { Code = @"
shader_type canvas_item;
render_mode blend_premul_alpha;
uniform float cut = 1.0;        // 裂开的进度（沿长度从一头到另一头）
uniform float open = 0.0;       // 张开的程度
uniform float flow = 1.0;       // +1 往里吸，-1 往外喷
uniform float clock = 0.0;
uniform float seed = 0.0;
uniform float flash = 0.0;
uniform float alpha = 1.0;
uniform float aspect = 4.0;     // 长 / 高
float h21(vec2 p) { vec3 p3 = fract(vec3(p.xyx) * 0.1031); p3 += dot(p3, p3.yzx + 33.33); return fract((p3.x + p3.y) * p3.z); }
float vn(vec2 p) { vec2 i = floor(p), f = fract(p); f = f * f * (3.0 - 2.0 * f);
	return mix(mix(h21(i), h21(i + vec2(1, 0)), f.x), mix(h21(i + vec2(0, 1)), h21(i + vec2(1, 1)), f.x), f.y); }
void fragment() {
	// u 沿裂口 -1～1，v 横向（以长度为单位）
	float u = UV.x * 2.0 - 1.0;
	float v = (UV.y * 2.0 - 1.0) / aspect;
	// 裂到哪了：从 u = -1 往 +1 裂过去，裂开的前沿后面才张开
	float front = cut * 2.4 - 1.2;
	float behind = smoothstep(front, front - 0.35, u);
	// 锯齿状的边：两边各自的噪声，越靠两头越窄
	float body = pow(max(1.0 - u * u, 0.0), 0.7);
	float jt = (vn(vec2(u * 11.0 + seed, 1.0)) - 0.5) * 0.5 + (vn(vec2(u * 27.0 + seed, 5.0)) - 0.5) * 0.25;
	float jb = (vn(vec2(u * 11.0 + seed, 9.0)) - 0.5) * 0.5 + (vn(vec2(u * 27.0 + seed, 13.0)) - 0.5) * 0.25;
	float hw = 0.16 * open * behind * body;
	float top = hw * (1.0 + jt), bot = hw * (1.0 + jb);
	float d = v > 0.0 ? v - bot : -v - top;          // < 0 在口子里
	float edgeW = 0.006 + 0.01 * open;
	float inside = smoothstep(edgeW * 0.5, -edgeW * 0.5, d);
	// 还没张开时：一道细的热线（正在被划开）
	float line = exp(-pow(v / 0.004, 2.0)) * smoothstep(front + 0.04, front - 0.02, u) * smoothstep(-1.0, -0.9, u) * (1.0 - smoothstep(0.0, 0.6, open)) * body;
	float rim = exp(-pow(d / edgeW, 2.0)) * step(-edgeW * 3.0, d) * smoothstep(0.0, 0.15, open) * behind * body;
	float glow = exp(-max(d, 0.0) / (0.05 + 0.04 * open)) * open * behind * body;
	// 虚空里的烟：顺着裂口往中间吸（或往外喷），一缕缕暗红
	float sx = u * 4.0 + flow * clock * 3.0 * sign(u + 1e-4) * -1.0;
	float smoke = vn(vec2(sx + seed, v * 40.0 + clock * 2.0)) * 0.6 + vn(vec2(sx * 2.3 - seed, v * 90.0)) * 0.4;
	vec3 voidC = vec3(0.012, 0.0, 0.005) + vec3(0.42, 0.02, 0.06) * smoothstep(0.55, 0.95, smoke) * (0.5 + 0.5 * open);
	vec3 rimC = mix(vec3(0.95, 0.1, 0.15), vec3(1.0, 0.72, 0.66), flash) * (rim + line * 1.4);
	vec3 glowC = vec3(0.7, 0.03, 0.08) * glow * (0.55 + 0.8 * flash);
	float a = inside * alpha;
	COLOR = vec4(voidC * a + (rimC + glowC * (1.0 - inside)) * alpha, a);
}" };

	/// <summary>
	/// 一道裂口：at 中心，angle 长轴方向，len 长度（像素）；delay 秒后开始裂（cutTime 秒裂完），张开、停 hold 秒、close 秒合上。
	/// flow：+1 往里吸（爪子伸进去的那道），-1 往外喷（爪子撕出来的那道）。
	/// </summary>
	public static void Open(Control layer, Vector2 at, float angle, float len, float delay, float flow, float cutTime = 0.05f, float hold = 0.14f, float close = 0.16f)
	{
		float hgt = len * 0.5f;
		Node2D root = Fx.Root(layer, at, "P5CurseRift", delay + cutTime + hold + close + 0.3);
		root.Rotation = angle;
		root.Visible = false;
		ShaderMaterial m = new() { Shader = Shade };
		m.SetShaderParameter("aspect", len / hgt);
		m.SetShaderParameter("seed", Fx.Rand(0f, 40f));
		m.SetShaderParameter("flow", flow);
		ColorRect q = new() { Size = new Vector2(len, hgt), Position = new Vector2(-len * 0.5f, -hgt * 0.5f), Material = m, MouseFilter = Control.MouseFilterEnum.Ignore };
		root.AddChild(q);
		float total = cutTime + 0.06f + hold + close;
		Fx.Animate(root, delay, total, t =>
		{
			float s = t * total;
			root.Visible = true;
			m.SetShaderParameter("cut", Mathf.Min(1f, s / cutTime));
			float op = Ease5.OutBack(Ease5.Seg(s, cutTime * 0.6f, 0.08f)) * (1f - Fx.InCubic(Ease5.Seg(s, cutTime + 0.06f + hold, close)));
			m.SetShaderParameter("open", Mathf.Max(0f, op));
			m.SetShaderParameter("flash", Mathf.Exp(-Mathf.Max(0f, s - cutTime) / 0.06f));
			m.SetShaderParameter("clock", s);
			m.SetShaderParameter("alpha", 1f - Ease5.Seg(s, total - 0.05f, 0.05f));
		});
	}
}
