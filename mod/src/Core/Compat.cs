using System;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;

namespace P3Makoto.Core;

/// <summary>
/// v0.14.45 版本适配层：杀戮尖塔 2 不同版本之间签名不一样的游戏接口，全部经过这里（运行时按当前版本选写法），
/// 本 mod 其他代码不直接调用它们 —— 这样同一个 DLL 在 0.107.1（Steam 正式版）和 0.111.0（测试版）上都能用。
/// 对照结果（tools/compat/ApiCheck，research/libs 与 research/libs_0.107.1）：
///   · MegaAnimationState.SetAnimation / AddAnimation：0.107 返回 MegaTrackEntry，0.111 返回 void（参数相同）→ 反射调用；
///   · MegaSprite.GetGlobalBoneTransform：0.107 没有 → 直接调 Spine 原生方法 get_global_bone_transform，再不行用骨骼的 get_global_transform；
///   · MegaAnimationState.GetCurrentAnimationName：0.107 在轨道为空时会抛空引用 → 直接读原生轨道；
///   · NGame.GetGameVersion：0.107 没有 → 反射，取不到就算了。
/// </summary>
public static class Compat
{
	private static readonly MethodInfo? SetAnim =
		typeof(MegaAnimationState).GetMethod("SetAnimation", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string), typeof(bool), typeof(int) }, null);

	private static readonly MethodInfo? AddAnim =
		typeof(MegaAnimationState).GetMethod("AddAnimation", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string), typeof(float), typeof(bool), typeof(int) }, null);

	public static void SetAnimation(MegaAnimationState state, string name, bool loop = true, int track = 0)
	{
		if (SetAnim != null)
		{
			SetAnim.Invoke(state, new object[] { name, loop, track });
		}
		else
		{
			state.BoundObject.Call("set_animation", name, loop, track);
		}
	}

	public static void AddAnimation(MegaAnimationState state, string name, float delay = 0f, bool loop = true, int track = 0)
	{
		if (AddAnim != null)
		{
			AddAnim.Invoke(state, new object[] { name, delay, loop, track });
		}
		else
		{
			state.BoundObject.Call("add_animation", name, delay, loop, track);
		}
	}

	/// <summary>轨道上正在播的动画名；轨道为空或读不到时返回 null（不抛异常）。</summary>
	public static string? CurrentAnimation(MegaAnimationState? state, int track = 0)
	{
		try
		{
			if (state?.BoundObject is not GodotObject native || !GodotObject.IsInstanceValid(native))
			{
				return null;
			}
			if (native.Call("get_current", track).AsGodotObject() is not GodotObject entry || !GodotObject.IsInstanceValid(entry))
			{
				return null;
			}
			if (entry.Call("get_animation").AsGodotObject() is not GodotObject anim || !GodotObject.IsInstanceValid(anim))
			{
				return null;
			}
			string name = anim.Call("get_name").AsString();
			return name.Length == 0 ? null : name;
		}
		catch (Exception)
		{
			return null;
		}
	}

	public static string? CurrentAnimation(MegaSprite? sprite, int track = 0)
	{
		if (sprite == null)
		{
			return null;
		}
		try
		{
			return CurrentAnimation(sprite.TryGetAnimationState(), track);
		}
		catch (Exception)
		{
			return null;
		}
	}

	private static readonly StringName GetGlobalBone = "get_global_bone_transform";

	/// <summary>骨骼的全局变换（和 0.111 的 MegaSprite.GetGlobalBoneTransform 一致）；读不到返回 null。</summary>
	public static Transform2D? BoneTransform(MegaSprite? sprite, string bone)
	{
		if (sprite?.BoundObject is not GodotObject native || !GodotObject.IsInstanceValid(native))
		{
			return null;
		}
		try
		{
			if (native.HasMethod(GetGlobalBone))
			{
				Variant v = native.Call(GetGlobalBone, bone);
				return v.VariantType == Variant.Type.Transform2D ? v.AsTransform2D() : null;
			}
			// 旧版 Spine 扩展没有上面那个方法：骨架 → 找骨骼 → 骨骼的全局变换
			if (native.Call("get_skeleton").AsGodotObject() is GodotObject skeleton
				&& skeleton.Call("find_bone", bone).AsGodotObject() is GodotObject b && GodotObject.IsInstanceValid(b))
			{
				Variant v = b.Call("get_global_transform");
				return v.VariantType == Variant.Type.Transform2D ? v.AsTransform2D() : null;
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	/// <summary>游戏版本号（"v0.107.1" 之类）；取不到返回 "?"。</summary>
	public static string GameVersion()
	{
		try
		{
			Type? game = typeof(MegaCrit.Sts2.Core.Nodes.NGame);
			if (game.GetMethod("GetGameVersion", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null) is MethodInfo m)
			{
				return m.Invoke(null, null)?.ToString() ?? "?";
			}
			string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(OS.GetExecutablePath()) ?? "", "release_info.json");
			if (System.IO.File.Exists(path))
			{
				using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
				if (doc.RootElement.TryGetProperty("version", out System.Text.Json.JsonElement v))
				{
					return v.GetString() ?? "?";
				}
			}
		}
		catch (Exception)
		{
		}
		return "?";
	}
}
