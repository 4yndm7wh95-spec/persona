using MegaCrit.Sts2.Core.DevConsole;
using MegaCrit.Sts2.Core.DevConsole.ConsoleCommands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace P3Makoto.Debug;

/// <summary>
/// 控制台命令 p3seal：直接进入建筑师房间（伟大封印结局的测试用）。
/// 原版的 event 命令只认地图事件和先古之民，建筑师（最终房间）不在列表里，所以单独加这一条。
/// </summary>
public class SealConsoleCmd : AbstractConsoleCmd
{
	public override string CmdName => "p3seal";

	public override string Args => "";

	public override string Description => "P3Makoto: jump to The Architect (Great Seal ending test).";

	public override bool IsNetworked => false;

	public override CmdResult Process(Player? issuingPlayer, string[] args)
	{
		if (!RunManager.Instance.IsInProgress || issuingPlayer?.RunState == null)
		{
			return new CmdResult(success: false, "A run is currently not in progress!");
		}
		EventModel architect = ModelDb.Event<TheArchitect>();
		issuingPlayer.RunState.AppendToMapPointHistory(MapPointType.Unknown, RoomType.Event, architect.Id);
		System.Threading.Tasks.Task task = RunManager.Instance.EnterRoom(new EventRoom(architect));
		return new CmdResult(task, success: true, "Jumped to The Architect.");
	}
}
