using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace P5Joker;

/// <summary>
/// 总攻击（Vfx/AllOut.cs）接进游戏，触发照 P3 mod 的 AllOutPatch：
/// · 怪物开始死亡动画时（NCreature.StartDeathAnim 前）—— Joker 在打精英 / Boss —— 把它此刻的样子截成剪影存起来（这一局的怪物，最多留 5 只、大的优先）；
///   倒下的是最后一只（敌人全灭、Joker 活着）→ 剪影截好（约 3 帧）立刻开播：最后一击的伤害数字一出来玻璃就碎（用户 2026-11-18）。
/// · 战斗结束（Hook.AfterCombatEnd 后）：结算流程推迟到总攻击放完（AllOut.Hold 减去已经放了的时间，只推迟表现，不改结果）；
///   万一前面没开播（截图失败等），在这里补播。
/// 快速模式「瞬间」不播。截图 / 播放出错都只记日志，照常结算。
/// </summary>
public static class AllOutGame
{
	private static readonly List<(ulong Room, AllOut.Foe Foe)> Foes = new();

	private static readonly AccessTools.FieldRef<NCombatRoom, ICombatRoomVisuals> Visuals =
		AccessTools.FieldRefAccess<NCombatRoom, ICombatRoomVisuals>("_visuals");

	private static bool Instant => SaveManager.Instance?.PrefsSave?.FastMode == FastModeType.Instant;

	private static bool BigRoom(NCombatRoom room)
	{
		RoomType? t = Visuals(room)?.Encounter?.RoomType;
		return t is RoomType.Elite or RoomType.Boss;
	}

	private static bool JokerHere(ICombatState? cs) =>
		cs != null && cs.PlayerCreatures.Any(p => p.Player != null && Joker.Is(p.Player));

	/// <summary>怪物倒下：截剪影。</summary>
	public static void OnDeath(NCreature n)
	{
		if (!AllOut.Playing) AllOut.RestoreTime();
		if (Instant || n.Entity is not { IsMonster: true } e || NCombatRoom.Instance is not NCombatRoom room || !BigRoom(room)
			|| !JokerHere(e.CombatState) || n.Visuals?.Body is not Node2D body || !body.IsVisibleInTree())
		{
			return;
		}
		ulong id = room.GetInstanceId();
		Foes.RemoveAll(f => f.Room != id);
		Rect2 hb = n.Hitbox.GetGlobalTransformWithCanvas() * new Rect2(Vector2.Zero, n.Hitbox.Size);
		float w = Mathf.Max(hb.Size.X * 2.1f, hb.Size.Y * 1.3f), h = hb.Size.Y * 1.75f;
		Rect2 region = new(hb.GetCenter().X - w * 0.5f, hb.End.Y + hb.Size.Y * 0.18f - h, w, h);
		bool last = e.CombatState!.Enemies.All(x => !x.IsAlive) && e.CombatState.PlayerCreatures.Any(p => p.IsAlive && p.Player != null && Joker.Is(p.Player));
		if (last) _pending = true;
		AllOut.Snap(room, region, parent => Clone(n, body, parent), foe =>
		{
			if (foe != null)
			{
				Foes.Add((id, foe));
				if (Foes.Count > 5)
				{
					Foes.Sort((a, b) => (b.Foe.Rect.Size.X * b.Foe.Rect.Size.Y).CompareTo(a.Foe.Rect.Size.X * a.Foe.Rect.Size.Y));
					Foes.RemoveRange(5, Foes.Count - 5);
				}
			}
			if (last) Start(id);
		});
	}

	private static bool _pending;
	private static ulong _playedRoom;

	private static void Start(ulong id)
	{
		_pending = false;
		if (_playedRoom == id || NCombatRoom.Instance is not NCombatRoom r || r.GetInstanceId() != id) return;
		_playedRoom = id;
		var foes = Foes.Where(f => f.Room == id).Select(f => f.Foe).ToList();
		ModEntry.Safe("allout", () => AllOut.Play(r, foes));
	}

	/// <summary>复制怪物此刻的身体（骨骼动画定在同一个动作、同一时刻），变换 = 它在屏幕上的变换。</summary>
	private static bool Clone(NCreature n, Node2D body, Node2D parent)
	{
		Transform2D xf = body.GetGlobalTransformWithCanvas();
		if (n.Visuals.SpineBody != null && Aim.CloneBody(n, parent, "idle_loop") is { } made)
		{
			made.Item1.Transform = xf;
			made.Item1.Modulate = Colors.White;
			return true;
		}
		if (body.Duplicate() is not Node2D copy) return false;
		parent.AddChild(copy);
		copy.Transform = xf;
		copy.Modulate = Colors.White;
		return true;
	}

	/// <summary>战斗结束：该播就播，返回要等多久（0 = 不播）。</summary>
	public static float OnCombatEnd(CombatRoom room, ICombatState? cs)
	{
		// 慢放兜底：总攻击的碎开 / 计时器 / 节点退出都会恢复速度；这里只在没在播总攻击时再恢复一次（不打断正在慢放的前半段）
		if (!AllOut.Playing) AllOut.RestoreTime();
		if (Instant || room.RoomType is not (RoomType.Elite or RoomType.Boss) || !JokerHere(cs)
			|| !cs!.Enemies.All(e => !e.IsAlive) || !cs.PlayerCreatures.Any(p => p.IsAlive && p.Player != null && Joker.Is(p.Player)))
		{
			Foes.Clear();
			return 0f;
		}
		ulong id = NCombatRoom.Instance?.GetInstanceId() ?? 0;
		if (_playedRoom != id)
		{
			// 最后一只倒下时没开播（还在截图就先到了这里 / 截图失败）：截图还在路上就等它，否则现在补播
			if (!_pending) Callable.From(() => Start(id)).CallDeferred();
			return AllOut.Hold + 0.1f;
		}
		float done = (Time.GetTicksMsec() - AllOut.StartedMs) / 1000f;
		return Mathf.Max(0.05f, AllOut.Hold - done);
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
	public static void Postfix(IRunState runState, ICombatState? combatState, CombatRoom room, ref Task __result)
	{
		float hold = 0f;
		ModEntry.Safe("allout check", () => hold = AllOutGame.OnCombatEnd(room, combatState));
		if (hold > 0f)
		{
			__result = Task.WhenAll(__result, Task.Delay((int)(hold * 1000f)));
		}
	}
}
