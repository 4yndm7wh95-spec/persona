using System;
using Godot;

namespace P5Joker;

/// <summary>
/// 第四版亚森（用户 2026-11-02：Persona 一律生图，不要三渲二）。Codex 的 6 层分层图（art/p5/fx/v4/arsene/layers，layers.json 关节点）
/// 在游戏里实时做 2D 骨骼动画：
/// · 现身：青蓝火焰从脚下往上把他「烧出来」（着色器按高度 + 噪声溶解，溶解边一圈青白火光），身体上浮、两翼张开；
/// · 待机：身体缓慢起伏，两翼轻扇；
/// · 出手（attackAt，游戏的上毒结算被 Aim 推迟到这一刻）：前伸的手臂先往回收（蓄），再猛地探出去，身体前倾、两翼一振、全身白闪一下，眼睛亮；
///   这一刻记下爪尖位置（ClawAt），PoisonFx 从这里把咒怨甩向敌人；
/// · 退场（exitAt）：从头顶往下烧回青焰散掉。
/// 接口照旧版 Persona.Show（Aim 的召唤流程不变：稀有牌照旧有眼神特写 + 舞台）。
/// </summary>
public static class ArseneFx
{
	private const float Pack = 0.35f;                       // 分层图打包时的缩放（tools/p5/stage_p5.py）
	private const float BodyTop = 331f, BodyToe = 2249f;    // 全图坐标：帽顶、脚尖
	private static readonly Vector2 ToeAt = new(600f, 2249f);

	private record struct Part(string Tex, Vector2 Pivot);

	// 从后往前（layers.json 的 z_index）
	private static readonly Part[] Parts =
	{
		new("arsene_wing_back", new(740f, 940f)),
		new("arsene_wing_front", new(800f, 880f)),
		new("arsene_arm_back", new(560f, 1000f)),
		new("arsene_body", new(680f, 1080f)),
		new("arsene_arm_reach", new(820f, 860f)),
		new("arsene_eyes_glow", new(650f, 780f)),
	};

	private static readonly Vector2 ClawTip = new(1210f, 760f);

	private static Shader? _shader;

	/// <summary>reveal：1 = 完全显形，从下往上按噪声溶解，溶解边青白火光；flash：整体提白（出手那一下）。</summary>
	private static Shader PersonaShader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform sampler2D noise : repeat_enable, filter_linear;
uniform float reveal = 1.0;
uniform float top_down = 0.0;
uniform float flash = 0.0;
uniform vec3 edge_col = vec3(0.45, 0.88, 1.0);
void fragment() {
	vec4 c = texture(TEXTURE, UV);
	float n = texture(noise, UV * vec2(3.0, 2.2)).r;
	float h = mix(1.0 - UV.y, UV.y, top_down);
	float d = reveal * 1.3 - 0.15 - (h + (n - 0.5) * 0.28);
	float vis = smoothstep(0.0, 0.015, d);
	float edge = (1.0 - smoothstep(0.0, 0.07, d)) * step(0.0, d) * step(reveal, 0.999);
	vec3 col = mix(c.rgb, vec3(1.0, 0.96, 0.96), flash) + edge_col * edge * 2.2;
	COLOR = vec4(col, c.a * max(vis, edge) * COLOR.a);
}" };

	/// <summary>现在在场的亚森：出手那一刻的爪尖（全局坐标）和时刻（毫秒）。</summary>
	private static Vector2 _claw;
	private static ulong _clawAt;

	/// <summary>亚森刚出过手（0.6s 内）：返回爪尖位置，PoisonFx 从这里甩出咒怨。</summary>
	public static bool TryClaw(out Vector2 claw)
	{
		claw = _claw;
		return _clawAt != 0 && Stage.NowMs() - _clawAt < 600;
	}

	/// <summary>
	/// feet = Joker 脚底，height = 亚森身高（屏幕像素），attackAt / exitAt = 现身后几秒出手 / 退场（Persona.Spec）。
	/// </summary>
	public static void Show(Control back, Vector2 feet, float height, float facing, float attackAt, float exitAt)
	{
		float k = height / (BodyToe - BodyTop);                // 全图像素 → 屏幕
		// 飘在 Joker 左后方、离开一段距离，脚尖大约在 Joker 腰的高度（沿用用户对 0.7 版亚森站位的意见：别太近、别太高、再往左）
		Vector2 toeScreen = feet + new Vector2(-0.48f * height * facing, -0.3f * height);
		Node2D root = new() { Name = "P5Arsene", Scale = new Vector2(k * facing, k) };
		back.AddChild(root);
		root.GlobalPosition = toeScreen - new Vector2(ToeAt.X * k * facing, ToeAt.Y * k);

		ShaderMaterial mat = new() { Shader = PersonaShader };
		mat.SetShaderParameter("noise", Paint.Noise);
		mat.SetShaderParameter("reveal", 0f);
		var nodes = new Node2D[Parts.Length];
		for (int i = 0; i < Parts.Length; i++)
		{
			Node2D joint = new() { Position = Parts[i].Pivot };
			root.AddChild(joint);
			if (Fx.Tex(Parts[i].Tex) is Texture2D tex)
			{
				joint.AddChild(new Sprite2D
				{
					Texture = tex, Centered = false, Scale = Vector2.One / Pack, Offset = -Parts[i].Pivot * Pack, Material = mat,
				});
			}
			nodes[i] = joint;
		}
		Node2D wingB = nodes[0], wingF = nodes[1], armB = nodes[2], body = nodes[3], armR = nodes[4], eyes = nodes[5];
		// 手臂、眼睛跟着身体一起浮动 / 前倾：挂到身体关节下（换算成身体关节的局部坐标）
		foreach (Node2D n in new[] { armB, armR, eyes, wingB, wingF })
		{
			Vector2 p = n.Position - body.Position;
			root.RemoveChild(n);
			body.AddChild(n);
			n.Position = p;
		}
		// 保持叠放顺序：翅膀在身体后面，前伸的手臂、眼睛在前面
		body.MoveChild(wingB, 0);
		body.MoveChild(wingF, 1);
		body.MoveChild(armB, 2);

		Glow.Flash(back, toeScreen + new Vector2(0f, -0.45f * height), new Color(0.3f, 0.8f, 1f), height * 0.9f, 0.5f);

		const float appear = 0.26f, outT = 0.38f;
		float total = exitAt + outT;
		float windUp = Mathf.Max(appear, attackAt - 0.14f);
		bool struck = false;
		Fx.Animate(root, 0, total, t =>
		{
			float time = t * total;
			// 现身 / 退场
			float rev = time < appear ? Ease5.OutQuint(time / appear) : time > exitAt ? 1f - Mathf.Clamp((time - exitAt) / outT, 0f, 1f) : 1f;
			mat.SetShaderParameter("reveal", rev);
			mat.SetShaderParameter("top_down", time > exitAt ? 1f : 0f);
			// 待机：起伏、轻扇；现身时从低处浮上来、两翼从收拢张开
			float bob = Mathf.Sin(time * 3.2f) * 6f / k;
			float rise = (1f - Ease5.OutQuint(Mathf.Min(1f, time / 0.45f))) * 40f / k;
			float open = 1f - Ease5.OutQuint(Mathf.Min(1f, time / 0.4f));
			float flap = Mathf.Sin(time * 4.1f) * 0.035f;
			// 出手：蓄（手臂回收、后仰）→ 探出（OutExpo）→ 慢慢回到待机
			float lean = 0f, reach = 0f, beat = 0f;
			if (time >= windUp && time < attackAt)
			{
				float u = (time - windUp) / Mathf.Max(0.01f, attackAt - windUp);
				reach = 0.22f * Ease5.OutQuint(u);
				lean = -0.04f * u;
			}
			else if (time >= attackAt)
			{
				float u = time - attackAt;
				float hit = Ease5.OutExpo(Mathf.Min(1f, u / 0.07f));
				float back2 = Mathf.Clamp((u - 0.18f) / 0.4f, 0f, 1f);
				reach = Mathf.Lerp(0.22f, -0.26f, hit) * (1f - back2);
				lean = Mathf.Lerp(-0.04f, 0.08f, hit) * (1f - back2);
				beat = Mathf.Max(0f, 1f - u / 0.16f);
				if (!struck)
				{
					struck = true;
					_claw = armR.GetGlobalTransform() * (ClawTip - Parts[4].Pivot);
					_clawAt = Stage.NowMs();
					Sfx.Play(Sfx.Curse, 0.4f);
				}
			}
			body.Position = Parts[3].Pivot + new Vector2(0f, bob + rise);
			body.Rotation = lean;
			armR.Rotation = reach;
			armB.Rotation = -reach * 0.3f + Mathf.Sin(time * 2.6f) * 0.03f;
			wingB.Rotation = -0.3f * open + flap - beat * 0.12f;
			wingF.Rotation = 0.3f * open - flap + beat * 0.16f;
			mat.SetShaderParameter("flash", beat * beat * 0.4f);       // 出手一闪（平方衰减：顿帧拖慢时不会一直发灰）
			eyes.Modulate = new Color(1f + beat * 3f, 1f + beat * 1.5f, 1f + beat * 1.5f, 1f);
		});
		Fx.FreeAfter(root, total + 0.05);
	}
}
