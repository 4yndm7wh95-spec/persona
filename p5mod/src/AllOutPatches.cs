using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace P5Joker;

/// <summary>
/// Joker 在精英或 Boss 战中于敌人倒下时采集剪影；最后一只倒下就开播总攻击（前半段慢放，0.9.58）。
/// 等本场在途剪影采集结束后播放，奖励界面等到收尾揭幕时再入栈；战斗结束时还没开播就在那里补播。快速模式「瞬间」跳过。
/// </summary>
public static class AllOutGame
{
	private const int MaxFoes = 5;
	private static readonly ConditionalWeakTable<NCreature, SpinePose> BeforeHit = new();
	private sealed record SpinePose(string Animation, float Time);

	private sealed class CaptureBatch(ulong roomId)
	{
		public ulong RoomId { get; } = roomId;
		public List<AllOut.Foe> Foes { get; } = new();
		public int Pending;
		public bool CrabRequested;
		public bool Started;
		public Task? Playback;
	}

	private static CaptureBatch? _batch;

	private static CaptureBatch GetBatch(NCombatRoom room)
	{
		ulong id = room.GetInstanceId();
		if (_batch?.RoomId != id) _batch = new CaptureBatch(id);
		return _batch;
	}

	private static readonly AccessTools.FieldRef<NCombatRoom, ICombatRoomVisuals> Visuals =
		AccessTools.FieldRefAccess<NCombatRoom, ICombatRoomVisuals>("_visuals");

	private static bool Instant => SaveManager.Instance?.PrefsSave?.FastMode == FastModeType.Instant;
	private static bool Enabled => P5Settings.Enabled(P5Settings.Option.AllOut) && !Instant;

	private static bool BigRoom(NCombatRoom room)
	{
		RoomType? t = Visuals(room)?.Encounter?.RoomType;
		return t is RoomType.Elite or RoomType.Boss;
	}

	private static bool JokerHere(ICombatState? cs) =>
		Joker.LocalPlayer is { } local && Joker.Is(local) && cs != null
		&& cs.PlayerCreatures.Any(p => p.IsAlive && p.Player == local);

	// Friendly summons such as Osty have a Monster model too.
	internal static bool IsEnemySilhouette(Creature? creature) => creature is { IsEnemy: true, IsMonster: true };

	// CreatureCmd.Damage triggers Hit after HP reaches zero, before StartDeathAnim.
	// Save the pose before the hit changes the Spine track. Win skips that trigger.
	internal static void RememberLethalHit(NCreature n)
	{
		if (!Enabled || n.Entity is not { IsDead: true } e || !IsEnemySilhouette(e)
			|| NCombatRoom.Instance is not NCombatRoom room || !BigRoom(room) || !JokerHere(e.CombatState)
			|| ReadPose(n.Visuals?.SpineBody) is not { } pose)
		{
			return;
		}
		BeforeHit.GetValue(n, _ => pose);
	}

	private static SpinePose? ReadPose(MegaSprite? sprite)
	{
		if (sprite?.TryGetAnimationState() is not { } state) return null;
		string? name = state.GetCurrentAnimationName(0);
		if (string.IsNullOrEmpty(name) || name is "hurt" or "hit" or "die" or "death") return null;
		MegaTrackEntry? track = state.GetCurrent(0);
		using var trackLifetime = (object?)track as System.IDisposable;
		return new SpinePose(name, track?.GetTrackTime() ?? 0f);
	}

	/// <summary>敌人倒下时采集剪影，保留死亡动作前的形态。</summary>
	public static void OnDeath(NCreature n)
	{
		if (!AllOut.Playing) AllOut.RestoreTime();
		if (!Enabled || n.Entity is not { } e || !IsEnemySilhouette(e) || NCombatRoom.Instance is not { } room
			|| !BigRoom(room) || !JokerHere(e.CombatState)) return;

		CaptureBatch batch = GetBatch(room);
		if (batch.Started) return;
		BeforeHit.TryGetValue(n, out SpinePose? pose);
		BeforeHit.Remove(n);
		// 0.9.58：倒下的是最后一只 → 剪影截好（Play 先等在途的截图）就立刻开播，最后一击的伤害数字一出来玻璃就碎、前半段慢放。
		// 不等游戏确认胜利（那时候结算流程已经往下走了，慢放就没了）。
		bool last = IsFinalBlow(e.CombatState);
		try
		{
			// 会复活的（实验体前两条命）不截：它还会再倒下一次，截了收尾里就有好几个一样的剪影
			if (WillStayDead(e)) CaptureFoe(n, room, batch, pose);
		}
		finally
		{
			if (last) batch.Playback ??= Play(room, batch);
		}
	}

	/// <summary>
	/// 这一下是不是真的赢了（2026-11-24 回归 bug：实验体三条命，打掉第一条命就播了总攻击）。照游戏自己的胜利判定（CombatManager.IsCombatEnding）：
	/// 没有活着的主要敌人（小怪不算），并且没有谁拦着战斗结束 —— 实验体的 AdaptablePower、寄生、蒸汽爆发、存货、惊喜这类会复活 / 进下一阶段的
	/// 都靠 ShouldStopCombatFromEnding 拦着（实验体第三形态时这个 power 已经移除，最后一条命才算）。判断出错就当不是，交给 OnCombatEnd 补播。
	/// </summary>
	private static bool WillStayDead(MegaCrit.Sts2.Core.Entities.Creatures.Creature e)
	{
		try { return e.CombatState is not { } cs || MegaCrit.Sts2.Core.Hooks.Hook.ShouldCreatureBeRemovedFromCombatAfterDeath(cs, e); }
		catch (System.Exception) { return true; }
	}

	private static bool IsFinalBlow(MegaCrit.Sts2.Core.Combat.ICombatState? cs)
	{
		if (cs == null) return false;
		try
		{
			if (cs.Enemies.Any(x => x != null && x.IsAlive && x.IsPrimaryEnemy)) return false;
			return !MegaCrit.Sts2.Core.Hooks.Hook.ShouldStopCombatFromEnding(cs);
		}
		catch (System.Exception ex)
		{
			Stage.Report("allout final blow check", ex);
			return false;
		}
	}

	private static void CaptureFoe(NCreature n, NCombatRoom room, CaptureBatch batch, SpinePose? pose)
	{
		// Kaiser Crab's two targetable creatures are invisible. Both claws and
		// the body are drawn by a single rig in the room background.
		Node2D? crabRig = room.Background?.GetNodeOrNull<Node2D>("%KaiserCrab")?.GetNodeOrNull<Node2D>("%Visuals");
		if (crabRig != null)
		{
			if (batch.CrabRequested) return;
			batch.CrabRequested = true;
			Rect2 crabRegion = room.GetViewport().GetVisibleRect().Grow(128f);
			Capture(room, batch, crabRegion, crabRig, null, crab: true);
			return;
		}

		if (n.Visuals?.GetCurrentBody() is not Node2D body) return;
		if (body == n.Visuals.SpineBody?.BoundObject) pose ??= ReadPose(n.Visuals.SpineBody);
		Rect2 hitbox = n.Hitbox.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, n.Hitbox.Size);
		float width = Mathf.Max(hitbox.Size.X * 2.1f, hitbox.Size.Y * 1.3f);
		float height = hitbox.Size.Y * 1.75f;
		Rect2 region = new(hitbox.GetCenter().X - width * 0.5f, hitbox.End.Y + hitbox.Size.Y * 0.18f - height, width, height);
		Capture(room, batch, region, body, pose);
	}

	private static void Capture(NCombatRoom room, CaptureBatch batch, Rect2 region, Node2D body, SpinePose? pose, bool crab = false)
	{
		batch.Pending++;
		try
		{
			AllOut.Snap(room, region, parent => Clone(body, parent, pose, crab), foe =>
			{
				batch.Pending--;
				if (_batch != batch || batch.Started || foe == null) return;
				batch.Foes.Add(foe);
				if (batch.Foes.Count <= MaxFoes) return;
				batch.Foes.Sort((a, b) => (b.Rect.Size.X * b.Rect.Size.Y).CompareTo(a.Rect.Size.X * a.Rect.Size.Y));
				batch.Foes.RemoveAt(MaxFoes);
			});
		}
		catch
		{
			batch.Pending--;
			throw;
		}
	}

	private static bool Clone(Node2D body, Node2D parent, SpinePose? pose, bool crab)
	{
		Transform2D transform = body.GetGlobalTransformWithCanvas();
		if (body.IsClass(MegaSprite.spineClassName))
		{
			// Use only the rig data. Duplicating the battle node also copied its
			// materials and attached scripts, which ran _Ready in the snapshot.
			Variant data = body.Get("skeleton_data_res");
			if (data.AsGodotObject() == null
				|| ClassDB.Instantiate(MegaSprite.spineClassName).AsGodotObject() is not Node2D copy)
			{
				return false;
			}

			copy.Transform = transform;
			parent.AddChild(copy);
			copy.Set("skeleton_data_res", data);
			if (copy.Call("get_skeleton").AsGodotObject() is not { } skeleton) return false;
			if (body.Call("get_skeleton").AsGodotObject() is { } sourceSkeleton)
			{
				Variant skin = sourceSkeleton.Call("get_skin");
				if (skin.AsGodotObject() != null) skeleton.Call("set_skin", skin);
			}
			skeleton.Call("set_slots_to_setup_pose");

			var sprite = new MegaSprite(copy);
			string? animation;
			if (crab)
			{
				foreach (string boneName in new[] { "ArmBoneL", "ArmBoneR" })
				{
					if (body.GetNodeOrNull<Node2D>(boneName)?.Duplicate(0) is Node2D bone) copy.AddChild(bone);
				}
				animation = "body/idle_loop";
				if (sprite.TryGetAnimationState() is { } state)
				{
					state.SetSpineAnimation(animation, true, 0);
					state.SetSpineAnimation("left/idle_loop", true, 1);
					state.SetSpineAnimation("right/idle_loop", true, 2);
					state.SetTimeScale(0f);
				}
			}
			else
			{
				string preview = body.Get("preview_animation").AsString();
				animation = pose != null && sprite.HasAnimation(pose.Animation) ? pose.Animation
					: sprite.HasAnimation("idle_loop") ? "idle_loop"
					: !string.IsNullOrEmpty(preview) && sprite.HasAnimation(preview) ? preview : null;
				if (animation != null && sprite.TryGetAnimationState() is { } state)
				{
					state.SetSpineAnimation(animation, true, 0);
					MegaTrackEntry? track = state.GetCurrent(0);
					using var trackLifetime = (object?)track as System.IDisposable;
					track?.SetTrackTime(animation == pose?.Animation ? pose.Time : 0f);
					state.SetTimeScale(0f);
				}
			}
			if (copy.HasMethod("update_skeleton")) copy.Call("update_skeleton", 0f);
			GD.Print($"[P5Joker] allout rig: {body.Name}, animation={animation ?? "setup pose"}");
			return true;
		}

		// The static body can contain multiple sprites. Duplicate its geometry
		// without scripts, signal connections or scene instantiation.
		if (body.Duplicate(0) is not Node2D staticCopy) return false;
		CleanStaticBody(staticCopy);
		staticCopy.Transform = transform;
		staticCopy.Show();
		parent.AddChild(staticCopy);
		return true;
	}

	private static void CleanStaticBody(Node node)
	{
		if (node is CanvasItem item)
		{
			item.Modulate = Colors.White;
			item.SelfModulate = Colors.White;
			item.Material = null;
		}
		foreach (Node child in node.GetChildren())
		{
			if (child is GpuParticles2D or CpuParticles2D)
			{
				node.RemoveChild(child);
				child.QueueFree();
			}
			else CleanStaticBody(child);
		}
	}

	public static Task OnCombatEnd(CombatRoom room, ICombatState? cs)
	{
		// 慢放兜底：只在没在播总攻击时恢复（不打断正在慢放的前半段）
		if (!AllOut.Playing) AllOut.RestoreTime();
		if (!Enabled)
		{
			_batch = null;
			return Task.CompletedTask;
		}
		if (room.RoomType is not (RoomType.Elite or RoomType.Boss) || !JokerHere(cs)
			|| NCombatRoom.Instance is not { } view) return Task.CompletedTask;

		CaptureBatch batch = GetBatch(view);
		return batch.Playback ??= Play(view, batch);
	}

	private static async Task Play(NCombatRoom view, CaptureBatch batch)
	{
		bool Current() => Fx.Active(view) && NCombatRoom.Instance == view && _batch == batch;
		try
		{
			// Snap also ticks on ProcessFrame. Allow an extra frame so the last
			// capture callback can run before the overlay reads the batch.
			SceneTree tree = view.GetTree();
			for (int frame = 0; Current() && batch.Pending > 0 && frame <= AllOut.SnapFrameLimit; frame++)
			{
				await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			}
			ulong deadline = Time.GetTicksMsec() + (ulong)(1000 * Math.Max(2.5, SinfulPlay.Remaining(view) + 1.0));
			while (Current() && Enabled && Time.GetTicksMsec() < deadline
				&& (SinfulPlay.Remaining(view) > 0 || OutbreakCue.HasPendingImpact(view)))
			{
				await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
			}
			if (!Current() || !Enabled || batch.Started) return;
			batch.Started = true;
			AllOut.Foe[] foes = batch.Foes.ToArray();
			batch.Foes.Clear();
			GD.Print($"[P5Joker] allout play: {foes.Length} foe silhouette(s)");
			await AllOut.Play(view, foes);
		}
		catch (System.Exception ex)
		{
			GD.PushWarning($"[P5Joker] allout: {ex}");
		}
	}

	internal static bool DeferRewards(NOverlayStack stack, NRewardsScreen rewards)
	{
		if (!Fx.Active(stack) || NCombatRoom.Instance is not { } room || !Fx.Active(room)
			|| _batch is not { Playback: { IsCompleted: false } playback } batch
			|| batch.RoomId != room.GetInstanceId()) return false;
		_ = ShowRewards();
		return true;

		async Task ShowRewards()
		{
			await playback;
			if (!GodotObject.IsInstanceValid(rewards) || rewards.IsQueuedForDeletion() || rewards.GetParent() != null) return;
			if (Fx.Active(stack) && Fx.Active(room) && NCombatRoom.Instance == room)
				Stage.Safe("allout show rewards", () => stack.Push(rewards));
			else
				rewards.QueueFree();
		}
	}
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.SetAnimationTrigger))]
public static class AllOutLethalHitPatch
{
	public static void Prefix(NCreature __instance, string __0)
	{
		if (__0 == "Hit")
			ModEntry.Safe("allout pre-hit pose", () => AllOutGame.RememberLethalHit(__instance));
	}
}

[HarmonyPatch(typeof(NCreature), nameof(NCreature.StartDeathAnim))]
public static class AllOutDeathPatch
{
	public static void Prefix(NCreature __instance) => ModEntry.Safe("allout snap", () => AllOutGame.OnDeath(__instance));
}

[HarmonyPatch(typeof(MegaCrit.Sts2.Core.Hooks.Hook), nameof(MegaCrit.Sts2.Core.Hooks.Hook.AfterCombatEnd))]
public static class AllOutCombatEndPatch
{
	public static void Postfix(IRunState runState, ICombatState? combatState, CombatRoom room)
	{
		ModEntry.Safe("allout combat end", () => AllOutGame.OnCombatEnd(room, combatState));
	}
}

[HarmonyPatch(typeof(NOverlayStack), nameof(NOverlayStack.Push))]
public static class AllOutRewardScreenPatch
{
	private static readonly AccessTools.FieldRef<NRewardsScreen, bool> IsTerminal =
		AccessTools.FieldRefAccess<NRewardsScreen, bool>("_isTerminal");

	public static bool Prefix(NOverlayStack __instance, IOverlayScreen screen)
	{
		if (screen is not NRewardsScreen rewards || !IsTerminal(rewards)) return true;
		bool deferred = false;
		Stage.Safe("allout defer rewards", () => deferred = AllOutGame.DeferRewards(__instance, rewards));
		return !deferred;
	}
}
