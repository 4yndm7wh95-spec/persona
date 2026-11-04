using Godot;

namespace P5Joker;

/// <summary>
/// 防御（用户 2026-11-04：重做；不要用代码拼形状，学巨手的思路用渲染）。
/// 一面渲染出来的弧形护盾：着色器里按一段球面来算（每个点有法线），所以边缘亮、中间透；
/// 后面的画面按法线折射（像隔着一层弯曲的红玻璃）；边缘一圈锐利的红色轮廓光（菲涅尔），玻璃里暗红的纹路缓缓流动。
/// 出现：从抬起的前臂那里向外长出来，生长的前沿一道亮光扫过；收起：从边缘按噪声烧成火星。音效用原版尖塔的（block_gain）。
/// </summary>
public static class GuardFx
{
	private static Shader? _shader;

	private static Shader ShieldShader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D noise : repeat_enable, filter_linear;
uniform float clock = 0.0;
uniform float grow = 0.0;       // 0 → 1：从前臂长出来
uniform float burn = 0.0;       // 0 → 1：烧散
uniform float flash = 0.0;
uniform float facing = 1.0;
uniform float aspect = 0.75;
uniform vec2 origin = vec2(0.2, 0.55);   // 前臂在矩形里的位置（UV）

void fragment() {
	// 矩形坐标：x 朝敌人（facing 翻过来），y 朝下；高度方向 -1～1
	vec2 p = (UV - 0.5) * vec2(aspect, 1.0) * 2.0;
	p.x *= facing;
	vec2 o = (origin - 0.5) * vec2(aspect, 1.0) * 2.0;
	o.x *= facing;
	// 球面：球心在 Joker 那一侧，取朝敌人、略朝镜头的那一块球冠
	vec2 c = vec2(-0.7, 0.0);
	float R = 1.18;
	vec2 q = p - c;
	float r2 = dot(q, q);
	float inSphere = step(r2, R * R);
	float z = sqrt(max(R * R - r2, 0.0));
	vec3 n = vec3(q, z) / R;
	vec3 axis = normalize(vec3(1.0, 0.0, 0.75));
	float ca = dot(n, axis);
	float cap = smoothstep(0.52, 0.56, ca) * inSphere;
	// 生长：从前臂往外，前沿被噪声扭得不规整
	float nz = texture(noise, UV * 2.2 + vec2(clock * 0.15, 0.0)).r;
	float dist = length(p - o) + (nz - 0.5) * 0.25;
	float reach = grow * 2.6;
	float shown = 1.0 - smoothstep(reach - 0.06, reach, dist);
	float front = (1.0 - smoothstep(0.0, 0.12, abs(dist - reach))) * step(grow, 0.999);
	// 烧散：噪声侵蚀，边缘一圈火星红
	float bn = texture(noise, UV * 3.1 + vec2(0.37, clock * 0.1)).r;
	float keep = smoothstep(burn - 0.03, burn + 0.03, bn * 0.95 + 0.05 * (1.0 - ca));
	float ember = (1.0 - smoothstep(0.0, 0.07, abs(bn * 0.95 + 0.05 * (1.0 - ca) - burn))) * step(0.001, burn);
	float a = cap * shown * keep;
	// 折射：按法线偏移读后面的画面（弯曲的玻璃），带一点红
	vec2 off = n.xy * vec2(facing, 1.0) * 0.022 * cap;
	vec3 bg = textureLod(screen_tex, SCREEN_UV - off, 0.6).rgb;
	vec3 col = mix(bg, bg * vec3(1.0, 0.45, 0.5), 0.45) + vec3(0.18, 0.0, 0.03);
	// 玻璃里的纹路：沿球面流动的暗红细丝
	float fl = texture(noise, n.xy * 1.6 + vec2(0.0, -clock * 0.3)).r;
	float fl2 = texture(noise, n.xy * 3.4 + vec2(clock * 0.2, 0.0)).r;
	float veins = 1.0 - smoothstep(0.0, 0.05, abs(fract(fl * 3.0 + fl2 * 0.6) - 0.5));
	col += vec3(0.75, 0.05, 0.1) * veins * 0.35;
	// 轮廓光：球冠的外缘（菲涅尔）和内缘（切口）都亮
	float fres = 1.0 - n.z;
	float rim = smoothstep(0.55, 0.95, fres);
	float cut = 1.0 - smoothstep(0.0, 0.025, abs(ca - 0.56));
	col += vec3(1.0, 0.12, 0.18) * (rim * 1.3 + cut * 1.1);
	col += vec3(1.0, 0.8, 0.78) * smoothstep(0.9, 0.99, fres) * 0.8;
	// 生长前沿、出现那一下、烧散的边
	col += vec3(1.0, 0.5, 0.5) * front * 1.4 * cap;
	col += vec3(1.0, 0.3, 0.32) * flash * (0.4 + rim);
	col += vec3(1.0, 0.25, 0.15) * ember * 1.5 * cap * shown;
	float alpha = a * (0.78 + 0.22 * rim) + ember * cap * shown * 0.8;
	COLOR = vec4(col, clamp(alpha, 0.0, 1.0));
}" };

	/// <summary>
	/// 放一面护盾。fist = 抬起的前臂（拳头），shield = 护盾中心，height = 护盾高度，facing = Joker 朝向。约 0.85s。
	/// </summary>
	public static void Shield(Control layer, Vector2 fist, Vector2 shield, float height, float facing = 1f)
	{
		Vector2 size = new(height * 0.78f, height);
		Vector2 pos = shield - size * 0.5f;
		ShaderMaterial m = new() { Shader = ShieldShader };
		m.SetShaderParameter("noise", Paint.Noise);
		m.SetShaderParameter("facing", facing);
		m.SetShaderParameter("aspect", size.X / size.Y);
		m.SetShaderParameter("origin", (fist - pos) / size);
		// 先抓一次屏幕：同一帧前面有别的读屏幕的着色器时，Godot 会复用更早的画面
		BackBufferCopy copy = new() { CopyMode = BackBufferCopy.CopyModeEnum.Viewport, Name = "P5GuardCopy" };
		layer.AddChild(copy);
		ColorRect r = new() { Material = m, Size = size, MouseFilter = Control.MouseFilterEnum.Ignore, Name = "P5Guard" };
		layer.AddChild(r);
		r.GlobalPosition = pos;
		Fx.Clock(r);
		const float tGrow = 0.18f, hold = 0.36f, tBurn = 0.32f;
		float life = tGrow + hold + tBurn;
		bool flashed = false;
		Fx.Animate(r, 0, life, t =>
		{
			float s = t * life;
			m.SetShaderParameter("grow", Fx.OutCubic(Mathf.Min(1f, s / tGrow)));
			m.SetShaderParameter("flash", s < tGrow ? 0f : Mathf.Max(0f, 1f - (s - tGrow) / 0.14f));
			m.SetShaderParameter("burn", Mathf.Clamp((s - tGrow - hold) / tBurn, 0f, 1f));
			if (!flashed && s >= tGrow)
			{
				flashed = true;
				Vanilla.Burst(layer, Vanilla.Specks, shield + new Vector2(height * 0.2f * facing, 0f), facing > 0f ? 0f : Mathf.Pi, 0.7f, Lut5.Blood, amount: 10);
			}
		});
		Fx.FreeAfter(r, life + 0.05);
		Fx.FreeAfter(copy, life + 0.05);
	}
}
