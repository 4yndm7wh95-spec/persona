using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace P5Joker;

/// <summary>
/// 血条上「下回合会被毒掉的那一段」（原版绿色）：Joker 上的毒换成咒怨色（用户 2026-11-13）。
/// 血条本身是整块亮红（F1373E），灾厄是紫色 → 用黑底 + 暗红斜条纹慢慢往右流，两边都分得开；
/// 毒能毒死时血量数字原版变绿，换成红字 + 黑描边。只认 Joker 施的毒（PoisonPower.Applier 是 Joker）。
/// </summary>
[HarmonyPatch(typeof(NHealthBar), nameof(NHealthBar.RefreshValues))]
public static class PoisonBarPatch
{
	private static readonly FieldInfo PoisonFg = AccessTools.Field(typeof(NHealthBar), "_poisonForeground");
	private static readonly FieldInfo CreatureF = AccessTools.Field(typeof(NHealthBar), "_creature");
	private static readonly FieldInfo LabelF = AccessTools.Field(typeof(NHealthBar), "_hpLabel");

	private static Shader? _shader;

	private static Shader Shader => _shader ??= new Shader { Code = @"
shader_type canvas_item;
uniform float clock = 0.0;
void fragment() {
	vec4 tex = texture(TEXTURE, UV);
	float lum = dot(tex.rgb, vec3(0.3, 0.59, 0.11));
	// 黑底 + 暗红斜条纹（用户 2026-11-13：紫粉色和原版「灾厄」撞了 → 换成红黑；血量本身是整块亮红，这个是黑底条纹，分得开）
	vec3 deep = vec3(0.07, 0.01, 0.015);
	vec3 mid = vec3(0.2, 0.02, 0.03);
	vec3 col = mix(deep, mid, smoothstep(0.15, 0.85, lum));
	float s = fract((FRAGCOORD.x + FRAGCOORD.y) / 14.0 - clock * 0.6);
	float stripe = smoothstep(0.42, 0.5, s) * (1.0 - smoothstep(0.78, 0.86, s));
	col = mix(col, vec3(0.78, 0.06, 0.1), stripe * 0.85);
	col += vec3(0.5, 0.08, 0.08) * smoothstep(0.35, 0.0, UV.y) * 0.3;
	COLOR = vec4(col, tex.a);
}" };

	private static ShaderMaterial? _mat;

	private static ShaderMaterial Mat
	{
		get
		{
			if (_mat != null) return _mat;
			_mat = new ShaderMaterial { Shader = Shader };
			// 所有血条共用一个材质、一个时钟（10 分钟绕回 0，避免 TIME 太大时精度崩）
			if (Engine.GetMainLoop() is SceneTree tree)
				tree.ProcessFrame += () => _mat.SetShaderParameter("clock", Time.GetTicksMsec() % 600000 / 1000f);
			return _mat;
		}
	}

	public static void Postfix(NHealthBar __instance)
	{
		ModEntry.Safe("PoisonBar", () =>
		{
			if (CreatureF.GetValue(__instance) is not Creature c || c.IsPlayer) return;
			PoisonPower? poison = c.GetPower<PoisonPower>();
			bool joker = poison?.Applier?.Player is { } p && Joker.Is(p);
			if (PoisonFg.GetValue(__instance) is CanvasItem fg)
			{
				if (joker && fg.Material != Mat)
				{
					fg.Material = Mat;
				}
				else if (!joker && fg.Material == Mat)
				{
					fg.Material = null;
				}
			}
			// 毒能毒死：血量数字原版是绿字深绿描边 → 粉白字 + 暗紫描边
			int dmg = poison?.CalculateTotalDamageNextTurn() ?? 0;
			if (joker && dmg > 0 && dmg >= c.CurrentHp && LabelF.GetValue(__instance) is Control label)
			{
				label.AddThemeColorOverride("font_color", new Color("FF6E6E"));
				label.AddThemeColorOverride("font_outline_color", new Color("1E0000"));
			}
		});
	}
}
