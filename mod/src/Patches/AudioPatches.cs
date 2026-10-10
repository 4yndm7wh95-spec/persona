using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;

namespace P3Makoto.Patches;

/// <summary>
/// 储君原版的出招 / 施法音效换成本 mod 的合成音效（挥剑破风、召唤器枪声），避免两套声音叠在一起。
/// 只拦截这两个事件，其他音效（锻造、星辉、敌人受击等）照常播放。
/// </summary>
[HarmonyPatch(typeof(SfxCmd), nameof(SfxCmd.Play), typeof(string), typeof(float))]
public static class RegentSfxMutePatch
{
	private const string Attack = "event:/sfx/characters/regent/regent_attack";
	private const string Cast = "event:/sfx/characters/regent/regent_cast";

	public static bool Prefix(string sfx) => sfx != Attack && sfx != Cast;
}
