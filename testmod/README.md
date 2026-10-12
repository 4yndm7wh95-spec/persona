# P3 测试工具（P3TestMod）

只用于单人测试特效和动画，**不要在正式对局或联机里开**（会改玩法）。和正式 mod（P3Makoto）一起装。

## 功能
- **所有房间都是训练假人战**：地图照常走，任何节点（普通 / 精英 / Boss / 商店 / 休息处 / 事件 / 宝箱）都进同一场训练假人战斗。
  - 假人不攻击、血量 999999、被打掉一半自动补满；
  - 原版假人的"3 回合时限"已去掉；
  - 战斗不给奖励，"击杀假人"后直接回地图。
- **能量 / 辉星无限**：始终不低于 99（直接改数值，不触发获得能量的动画）。
- **玩家不会死**：血量低于 30% 自动补满。
- **测试面板**（F8 或左上角"测试"按钮）：
  - 卡牌按原版卡面显示（和原版牌组 / 图鉴同一个卡牌格子），点一下加入手牌；
  - 筛选：储君 / 无色 / 全部，按卡名搜索，每页 12 张；
  - 按钮：击杀假人（看胜利特效）、让我受击（3 点伤害，看受击动画）、清空手牌、消耗手牌。
- **F7：消耗所有手牌**（面板关着也能用，所有手牌同时消耗）。

## 导演接口（录制演示用）
游戏启动后自动在 **127.0.0.1:47021** 开一个本机 HTTP 接口（只监听本机；环境变量 `P3_BRIDGE_PORT` 改端口，`P3_BRIDGE=0` 关闭）。
配套脚本在 `tools/director/`：`director.py`（命令行 + 按镜头清单配合 OBS 逐段录制）、`mcp_server.py`（MCP 服务，给 Codex 用）、`shots.json`（代表性镜头清单）。

| 接口 | 参数 | 作用 |
|---|---|---|
| `/ping` `/state` | | 连通检查；当前画面 / 手牌（含屏幕坐标）/ 敌人 / 光标 |
| `/cards` | `pool` `q` | 列卡牌（`cls` 类名可直接当 id） |
| `/hand/set` `/hand/add` | `ids` `count` | 换手牌 / 加牌 |
| `/hand/clear` `/hand/exhaust` | | 清空 / 消耗全部手牌 |
| `/play` | `card` `target` `style` `speed` `hover_ms` `aim_ms` | 出牌。`style=drag`（默认）：真实鼠标悬停 → 拖起 → 瞄准 → 松手，和手动出牌同一流程；`instant`：直接打出 |
| `/end_turn` | `style=click/instant` | 鼠标点结束回合按钮，等到下回合可操作才返回 |
| `/wait` | `ms` `until=ready` | 等待 |
| `/mouse/move` `/mouse/click` `/mouse/park` `/mouse/hover_card` | `x` `y` `ms` `relative` | 光标平滑移动 / 点击 / 停到画面外侧 / 悬停手牌 |
| `/ui/find` `/ui/click` | `text` `type` `character` `index` `travelable` | 找 / 点界面节点（按文字、类名、角色；地图节点） |
| `/ui` | `test_button` | 隐藏 / 显示左上角"测试"按钮 |
| `/kill` `/hurt` `/console` | `amount` `command` | 击杀假人、受击、开发者控制台命令 |

录制时不要碰鼠标（脚本在移动真实光标）；游戏用 1920×1080 全屏最稳。

## 实现
全部调用原版开发者控制台命令用的同一套接口（`card` / `energy` / `stars` / `fight` / `damage` / `kill`）：
- `RunManager.CreateRoom`：除地图外都返回 `BattlewornDummyEventV1Encounter` 战斗；
- `BattleFriendV1.AfterAddedToRoom`：跳过（不挂时限）；
- `PlayerCombatState.Energy / Stars` 的 setter：数值不低于 99。

## 编译 / 安装
`dotnet build -c Release`（testmod 目录），把 `P3TestMod.dll` 和 `P3TestMod.json` 放进
`<游戏目录>\mods\P3TestMod\`。不用时删掉这个文件夹即可。
