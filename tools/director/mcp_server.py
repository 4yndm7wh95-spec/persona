"""P3 演示录制 MCP 服务（stdio）：把 P3TestMod 导演接口和 OBS 录制包装成 MCP 工具，供 Codex 直接调用。

在 Codex 的 config.toml 里登记（路径换成本机仓库路径）：
  [mcp_servers.p3director]
  command = "python"
  args = ["<仓库>/tools/director/mcp_server.py"]

依赖：pip install mcp obsws-python requests
"""
from __future__ import annotations

import contextlib
import json
import sys
from pathlib import Path

from mcp.server.fastmcp import FastMCP

import director as d

mcp = FastMCP("p3director")
_obs: d.Obs | None = None


def _o() -> d.Obs:
    global _obs
    if _obs is None:
        _obs = d.Obs()
    return _obs


def _call(endpoint: str, **args) -> str:
    try:
        return json.dumps(d.call(endpoint, {k: v for k, v in args.items() if v is not None}), ensure_ascii=False)
    except d.BridgeError as e:
        return json.dumps({"ok": False, "error": str(e)}, ensure_ascii=False)


@mcp.tool()
def game_state() -> str:
    """当前画面（combat / map / char_select / other）、能量、手牌（序号、id、屏幕坐标）、敌人、光标位置。"""
    return _call("state")


@mcp.tool()
def list_cards(pool: str = "regent", query: str = "") -> str:
    """列出卡牌：pool = regent（储君）/ colorless / all；query 按名称或 id 过滤。cls 字段（类名）可直接当卡牌 id 用。"""
    return _call("cards", pool=pool, q=query)


@mcp.tool()
def hand_set(cards: list[str]) -> str:
    """把手牌换成指定的牌（按顺序；先把原手牌移到弃牌堆）。cards 用类名或 id，如 ["SovereignBlade", "StrikeRegent"]。"""
    return _call("hand/set", ids=cards)


@mcp.tool()
def hand_add(cards: list[str], count: int = 1) -> str:
    """往手牌里加牌（每张加 count 次，手牌上限 10）。"""
    return _call("hand/add", ids=cards, count=count)


@mcp.tool()
def hand_clear() -> str:
    """把手牌全部移到弃牌堆（无动画）。"""
    return _call("hand/clear")


@mcp.tool()
def hand_exhaust() -> str:
    """一次性消耗全部手牌（播放原版消耗动画）。"""
    return _call("hand/exhaust")


@mcp.tool()
def play_card(card: str, target: int = 0, style: str = "drag", speed: float = 1.0, hover_ms: int | None = None, aim_ms: int | None = None) -> str:
    """出一张牌。card：手牌序号（0 起，-1 为最后一张）或类名 / id。target：敌人序号。
    style=drag：像真人一样用鼠标悬停 → 拖起 → 瞄准 → 松手（默认，用于录制）；style=instant：直接打出。
    speed：拖动速度倍率（0.6 更慢更从容，1.5 更快）；hover_ms / aim_ms：悬停 / 瞄准停顿（毫秒）。等到动画结算完才返回。"""
    return _call("play", card=card, target=target, style=style, speed=speed, hover_ms=hover_ms, aim_ms=aim_ms)


@mcp.tool()
def end_turn(style: str = "click") -> str:
    """结束回合：style=click 用鼠标移过去点按钮（默认），instant 直接结束。等到下一回合可操作才返回。"""
    return _call("end_turn", style=style)


@mcp.tool()
def wait(ms: int = 0, until_ready: bool = False) -> str:
    """等待 ms 毫秒；until_ready=True 时一直等到玩家可以操作（动画 / 敌方回合结束）。"""
    return _call("wait", ms=ms, until="ready" if until_ready else None)


@mcp.tool()
def mouse_move(x: float, y: float, ms: int | None = None, relative: bool = False) -> str:
    """平滑移动光标到 (x, y)（1920×1080 视口坐标；relative=True 时用 0~1 的比例坐标）。"""
    return _call("mouse/move", x=x, y=y, ms=ms, relative=relative)


@mcp.tool()
def mouse_click(x: float, y: float, relative: bool = False) -> str:
    """移动到 (x, y) 并左键单击。"""
    return _call("mouse/click", x=x, y=y, relative=relative)


@mcp.tool()
def mouse_park() -> str:
    """光标移到屏幕上方正中，不挡画面。"""
    return _call("mouse/park")


@mcp.tool()
def hover_card(card: str, hover_ms: int = 900) -> str:
    """光标移到某张手牌上悬停（看抬牌和人物举召唤器 / 持剑）。"""
    return _call("mouse/hover_card", card=card, hover_ms=hover_ms)


@mcp.tool()
def ui_find(text: str | None = None, type: str | None = None, character: str | None = None) -> str:
    """找界面上可见的节点：text = 文字包含；type = 节点类名（如 NCharacterSelectButton、NMapPoint）；character = 角色（Regent）。返回坐标。"""
    return _call("ui/find", text=text, type=type, character=character)


@mcp.tool()
def ui_click(text: str | None = None, type: str | None = None, character: str | None = None, index: int = 0, travelable: bool = False) -> str:
    """用鼠标点击界面节点（参数同 ui_find；travelable=True 只点地图上可前往的节点）。"""
    return _call("ui/click", text=text, type=type, character=character, index=index, travelable=travelable)


@mcp.tool()
def set_test_button(visible: bool) -> str:
    """显示 / 隐藏左上角的"测试 (F8)"按钮（录制时隐藏）。"""
    return _call("ui", test_button=visible)


@mcp.tool()
def kill_dummy() -> str:
    """击杀假人（看胜利演出）。"""
    return _call("kill")


@mcp.tool()
def hurt_me(amount: int = 3) -> str:
    """对自己造成伤害（看受击动作）。"""
    return _call("hurt", amount=amount)


@mcp.tool()
def console(command: str) -> str:
    """执行原版开发者控制台命令（如 "fight ..."、"relic ..."）。"""
    return _call("console", command=command)


@mcp.tool()
def record_start() -> str:
    """OBS 开始录制。"""
    _o().start()
    return "recording"


@mcp.tool()
def record_stop() -> str:
    """OBS 停止录制，返回文件路径。"""
    return str(_o().stop())


@mcp.tool()
def run_shots(plan: str = "shots.json", only: list[str] | None = None, use_obs: bool = True) -> str:
    """按镜头清单逐段录制（plan 为 tools/director 下的文件名或绝对路径；only 只录指定镜头名）。返回输出目录。"""
    p = Path(plan)
    if not p.is_absolute():
        p = Path(__file__).parent / p
    with contextlib.redirect_stdout(sys.stderr):  # stdout 是 MCP 协议通道，进度信息改打到 stderr
        out = d.run_shots(p, only, use_obs)
    return str(out) if out else "done (no obs)"


if __name__ == "__main__":
    mcp.run()
