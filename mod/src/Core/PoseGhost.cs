using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;

namespace P3Makoto.Core;

/// <summary>
/// v0.15.23：动画被打断时，从"屏幕上真正显示的那张姿势图"溶解过去。
///
/// 结城理的动作是整身姿势图换图（tools/rig/build_rig_v7.py），每个动画开头的溶解是烘焙好的，
/// 但烘焙时只能假定"上一个姿势"（大多写死成待机图）。动画中途被打断时（多段攻击连挥、举召唤器还没到位就松手、
/// 拖着技能牌又换成攻击牌、受击接受击），旧图层显示的是待机图而不是刚才那张 —— 画面上会闪一下站立姿势再跳到新姿势。
///
/// 做法：游戏每次切动画（MegaAnimationState.SetAnimation，唯一入口）前记下 v7_body 槽当前的姿势图；
/// 切换后的第一帧（Spine 已把新动画应用到骨架、还没生成网格：world_transforms_changed）检查：
/// 新动画换了图、而它烘焙的旧图层不是刚才那张 → 由这里接管旧图层 v7_old，用刚才那张图按烘焙溶解同样的曲线淡出（约 0.07s）。
/// 烘焙的溶解本来就对（上一张就是待机）时完全不碰。期间新动画自己又换图，就交还给烘焙数据。
///
/// Spine 包装对象：骨架和两个槽的包装每个角色只取一次并一直持有（不产生临时包装）；
/// get_attachment 每次返回新的临时包装，读完名字立即在主线程 Dispose（PRG-6985：交给终结器线程释放会损坏内存）。
/// </summary>
public static class PoseGhost
{
	private const string BodySlot = "v7_body";
	private const string OldSlot = "v7_old";
	private const float Fade = 0.07f;

	private sealed class Rig
	{
		public GodotObject Sprite = null!;
		public GodotObject? Skeleton;
		public GodotObject? Body, Old;
		public string? From;          // 切换前显示的姿势图（等待第一帧处理）
		public bool Pending;
		public string? Ghost;         // 正在淡出的旧图（null = 没有接管）
		public string? NewBody;       // 接管时新动画显示的姿势图
		public string? BakedOld;      // 接管时新动画自己烘焙的旧图层附件
		public float Age;
	}

	private static readonly Dictionary<ulong, Rig> ByState = new();
	private static readonly HashSet<ulong> Attached = new();

	/// <summary>给结城理的身体 Spine 挂上监听（SpineHooks.Ensure 调用，每个角色一次）。</summary>
	public static void Attach(MegaSprite body)
	{
		if (body.BoundObject is not GodotObject sprite || !Attached.Add(sprite.GetInstanceId()))
		{
			return;
		}
		if (!sprite.HasSignal("world_transforms_changed") || body.TryGetAnimationState() is not MegaAnimationState state)
		{
			return;
		}
		Rig rig = new() { Sprite = sprite };
		if (!Bind(rig))
		{
			return;              // 不是 v7 骨骼（没有 v7_body / v7_old 槽）：不接管
		}
		int args = SignalArgs(sprite, "world_transforms_changed");
		if (args is < 0 or > 1)
		{
			ModEntry.Warn($"pose ghost off: world_transforms_changed has {args} args");
			return;
		}
		ByState[state.BoundObject.GetInstanceId()] = rig;
		Callable cb = args == 1 ? Callable.From<GodotObject>(_ => OnFrame(rig)) : Callable.From(() => OnFrame(rig));
		sprite.Connect("world_transforms_changed", cb);
	}

	/// <summary>信号参数个数（spine-godot 的信号带 SpineSprite 自己作参数；版本不同时按实际个数连）。没有返回 -1。</summary>
	private static int SignalArgs(GodotObject obj, string signal)
	{
		foreach (Godot.Collections.Dictionary d in obj.GetSignalList())
		{
			if ((string)d["name"] == signal)
			{
				return ((Godot.Collections.Array)d["args"]).Count;
			}
		}
		return -1;
	}

	private static bool _methodsOk, _methodsChecked;

	/// <summary>用到的 Spine 原生方法都在（不同版本的 Spine 扩展少一个就整体不接管，只记一次日志）。</summary>
	private static bool MethodsOk(GodotObject skeleton, GodotObject slot)
	{
		if (!_methodsChecked)
		{
			_methodsChecked = true;
			_methodsOk = skeleton.HasMethod("set_attachment") && skeleton.HasMethod("find_slot")
				&& slot.HasMethod("get_attachment") && slot.HasMethod("set_color");
			if (!_methodsOk)
			{
				ModEntry.Warn("pose ghost off: spine slot API differs");
			}
		}
		return _methodsOk;
	}

	/// <summary>骨架包装变了（换了骨骼数据）就重新取槽。返回是否可用。</summary>
	private static bool Bind(Rig rig)
	{
		using Variant skV = rig.Sprite.Call("get_skeleton");
		GodotObject? sk = skV.VariantType == Variant.Type.Object ? skV.AsGodotObject() : null;
		if (sk == null)
		{
			return false;
		}
		if (ReferenceEquals(sk, rig.Skeleton) && rig.Body != null && rig.Old != null)
		{
			return true;
		}
		rig.Skeleton = sk;
		rig.Body = FindSlot(sk, BodySlot);
		rig.Old = FindSlot(sk, OldSlot);
		return rig.Body != null && rig.Old != null && MethodsOk(sk, rig.Body);
	}

	private static GodotObject? FindSlot(GodotObject skeleton, string name)
	{
		using Variant v = skeleton.Call("find_slot", name);
		return v.VariantType == Variant.Type.Object ? v.AsGodotObject() : null;
	}

	private static string? AttachmentName(GodotObject? slot)
	{
		if (slot == null || !GodotObject.IsInstanceValid(slot))
		{
			return null;
		}
		using Variant v = slot.Call("get_attachment");
		if (v.VariantType != Variant.Type.Object || v.AsGodotObject() is not GodotObject att)
		{
			return null;
		}
		try
		{
			using Variant n = att.Call("get_attachment_name");
			string s = n.AsString();
			return string.IsNullOrEmpty(s) ? null : s;
		}
		finally
		{
			att.Dispose();
		}
	}

	public static bool Tracks(MegaAnimationState state) =>
		ByState.Count > 0 && state.BoundObject is GodotObject o && ByState.ContainsKey(o.GetInstanceId());

	/// <summary>游戏切动画之前：记下现在显示的姿势图。</summary>
	public static void BeforeSwitch(MegaAnimationState state)
	{
		if (!ByState.TryGetValue(state.BoundObject.GetInstanceId(), out Rig? rig))
		{
			return;
		}
		if (!GodotObject.IsInstanceValid(rig.Sprite))
		{
			ByState.Remove(state.BoundObject.GetInstanceId());
			return;
		}
		if (!Bind(rig))
		{
			return;
		}
		// 正在接管的溶解还没结束又被打断：以"现在屏幕上的主图"为准（旧图层马上会被新的溶解替换）
		rig.From = AttachmentName(rig.Body);
		rig.Pending = rig.From != null;
	}

	private static void OnFrame(Rig rig)
	{
		if (!rig.Pending && rig.Ghost == null)
		{
			return;                                     // 平时每帧只走到这里
		}
		ModEntry.Safe("pose ghost", () =>
		{
			if (rig.Sprite is not Node node || !GodotObject.IsInstanceValid(node))
			{
				rig.Pending = false;
				rig.Ghost = null;
				return;
			}
			float dt = (float)node.GetProcessDeltaTime();
			string? body = AttachmentName(rig.Body);
			string? old = AttachmentName(rig.Old);
			if (rig.Pending)
			{
				rig.Pending = false;
				if (rig.Ghost != null)
				{
					// 上一次接管被新的切换打断：先清掉旧图层（下面若需要会重新接管）
					Release(rig, old);
					old = AttachmentName(rig.Old);
				}
				// 没换图（同一张图开头）、或者烘焙的溶解本来就是从这张图开始：交给动画数据
				if (body == null || body == rig.From || old == rig.From)
				{
					return;
				}
				rig.Ghost = rig.From;
				rig.NewBody = body;
				rig.BakedOld = old;
				rig.Age = dt;                            // 新动画在这一帧已经走了一帧（和烘焙溶解的时间轴对齐）
			}
			else
			{
				rig.Age += dt;
				// 新动画自己又换图了（它的旧图层会显示正确的上一张）：交还给动画数据
				bool bakedTookOver = body != rig.NewBody || (old != null && old != rig.Ghost && old != rig.BakedOld);
				if (bakedTookOver || rig.Age >= Fade)
				{
					Release(rig, old);
					return;
				}
			}
			Apply(rig, rig.Ghost!, Alpha(rig.Age));
		});
	}

	/// <summary>和烘焙溶解同一条曲线：旧图盖在上面，1 → 0.3（前 35%）→ 0。</summary>
	private static float Alpha(float age)
	{
		float u = Mathf.Clamp(age / Fade, 0f, 1f);
		return u < 0.35f ? Mathf.Lerp(1f, 0.3f, u / 0.35f) : Mathf.Lerp(0.3f, 0f, (u - 0.35f) / 0.65f);
	}

	private static void Apply(Rig rig, string attachment, float alpha)
	{
		if (rig.Skeleton == null || rig.Old == null)
		{
			return;
		}
		rig.Skeleton.Call("set_attachment", OldSlot, attachment);
		rig.Old.Call("set_color", new Color(1f, 1f, 1f, alpha));
	}

	/// <summary>
	/// 结束接管。当前动画在这一帧给旧图层设了别的图（它自己的溶解）就不碰；
	/// 旧图层上还是我们放的图（动画没有给这个槽打关键帧，不会自己清掉）→ 清空并恢复白色。
	/// </summary>
	private static void Release(Rig rig, string? currentOld)
	{
		string? ghost = rig.Ghost;
		rig.Ghost = null;
		rig.NewBody = rig.BakedOld = null;
		if (ghost == null || rig.Skeleton == null || rig.Old == null || currentOld != ghost)
		{
			return;
		}
		rig.Skeleton.Call("set_attachment", OldSlot, "");
		rig.Old.Call("set_color", new Color(1f, 1f, 1f, 1f));
	}
}

/// <summary>游戏切换 Spine 动画的唯一入口：切之前记下结城理当前显示的姿势图（见 PoseGhost）。</summary>
[HarmonyPatch(typeof(MegaAnimationState), nameof(MegaAnimationState.SetAnimation))]
public static class PoseGhostSwitchPatch
{
	public static void Prefix(MegaAnimationState __instance)
	{
		if (PoseGhost.Tracks(__instance))                 // 其他生物（怪物）只做一次字典查找
		{
			ModEntry.Safe("pose ghost switch", () => PoseGhost.BeforeSwitch(__instance));
		}
	}
}
