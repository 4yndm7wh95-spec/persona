using System.Collections.Generic;
using Godot;

namespace P3Makoto.Core;

/// <summary>
/// v0.14.48（玩家：装了 Skin Changer 等一堆 mod 后，本 mod 的美术一个都没出来 —— 选人立绘、头像、特效全是原版，但设置里的 P3MAKOTO 一栏在）：
/// 本 mod 靠资源包覆盖原版储君的路径（骨骼、选人图、头像、地图标记）。别的 mod 后加载、或者在运行中把这些路径换回原版 / 换成它自己的文件时，
/// 我们的就全没了；之后 Makoto.AssetsOurs 发现"不是我们的骨骼"，连特效也一起让路 = 什么都没有。
/// 现在资源包里另有一份放在私有路径（res://p3makoto/rig、res://p3makoto/override，tools/pck/make_private_copies.py 生成，别的 mod 不会碰），
/// 在游戏要用到这些资源之前（启动、选人、开局 / 进战斗 / 商店 / 休息处的预加载）用 Resource.TakeOverPath 把私有副本盖回原版路径：
/// 之后游戏按原版路径加载（包括场景里的引用）拿到的都是我们的。
/// </summary>
public static class AssetGuard
{
	private static readonly (string Vanilla, string Mine)[] Map =
	{
		("res://animations/characters/regent/regent_skel_data.tres", "res://p3makoto/rig/characters/regent_skel_data.tres"),
		("res://animations/merchant/regent/regent_merchant_skel_data.tres", "res://p3makoto/rig/merchant/regent_merchant_skel_data.tres"),
		("res://animations/rest_site/regent/rest_site_regent_skel_data.tres", "res://p3makoto/rig/rest_site/rest_site_regent_skel_data.tres"),
		("res://images/packed/character_select/char_select_regent.png", "res://p3makoto/override/char_select_regent.png"),
		("res://images/packed/character_select/char_select_regent_locked.png", "res://p3makoto/override/char_select_regent_locked.png"),
		("res://images/packed/map/icons/map_marker_regent.png", "res://p3makoto/override/map_marker_regent.png"),
		("res://images/ui/top_panel/character_icon_regent.png", "res://p3makoto/override/character_icon_regent.png"),
		("res://images/ui/top_panel/character_icon_regent_outline.png", "res://p3makoto/override/character_icon_regent_outline.png"),
	};

	/// <summary>已加载的私有副本（强引用：缓存里的条目只在有人引用时存在）。键 = 原版路径。</summary>
	private static readonly Dictionary<string, Resource> Held = new();

	private static bool _warnedMissing;

	/// <summary>本 mod 的资源包是否真的装上了（P3Makoto.pck 缺失时，代码照常运行但所有美术都不在）。</summary>
	public static bool PackLoaded => ResourceLoader.Exists("res://p3makoto/ui/charselect_portrait.png");

	public static void Apply(string when)
	{
		if (!PackLoaded)
		{
			if (!_warnedMissing)
			{
				_warnedMissing = true;
				ModEntry.Warn($"P3Makoto.pck resources are missing ({when}) -> check that P3Makoto.pck sits next to P3Makoto.dll; all P3 art and effects stay off");
			}
			return;
		}
		int taken = 0;
		foreach ((string vanilla, string mine) in Map)
		{
			// v0.14.65（实机日志：ObjectDisposedException at TakeOverPath）：我们拿着的私有副本被别处 Dispose 了（C# 包装失效）→ 丢掉重新加载
			if (Held.TryGetValue(vanilla, out Resource? held) && !GodotObject.IsInstanceValid(held))
			{
				Held.Remove(vanilla);
			}
			if (!Held.TryGetValue(vanilla, out Resource? res))
			{
				if (!ResourceLoader.Exists(mine))
				{
					continue;
				}
				res = ResourceLoader.Load(mine);
				if (res == null)
				{
					continue;
				}
				Held[vanilla] = res;
			}
			// 缓存里这个路径已经是我们的就不动（不触发任何加载）
			if (ResourceLoader.HasCached(vanilla) && ResourceLoader.GetCachedRef(vanilla) == res)
			{
				continue;
			}
			try
			{
				res.TakeOverPath(vanilla);
				taken++;
			}
			catch (System.ObjectDisposedException)
			{
				Held.Remove(vanilla);                          // 下次再加载一份新的；这一项不影响其他路径
			}
		}
		if (taken > 0)
		{
			GD.Print($"{ModEntry.Tag} asset guard ({when}): {taken} Regent path(s) pointed back to P3Makoto resources");
			Makoto.ResetAssetCheck();
		}
	}
}
