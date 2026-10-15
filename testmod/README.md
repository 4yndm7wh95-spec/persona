# P3 测试工具（P3TestMod）

测试特效和动画用，**不要在正式对局里开**（会改玩法）。和正式 mod（P3Makoto）一起装。
支持联机测试：**所有玩家都要装这个测试 mod**（它标记了"影响玩法"，原版联机握手时会检查双方的这类 mod 是否一致；P3Makoto 标记的是不影响玩法，只装在自己这边也能联机）。

## 功能（v1.2）
- **正常关卡**：不再每一关都是木桩，能量 / 辉星也不再无限。
- **左上角常驻按钮**（一开游戏就有）：
  - **测试 (F8)**：打开测试面板；
  - **换成木桩**：把当前战斗的怪物换成一只打不死的训练木桩（血量 999999，被打掉一半自动补满，不攻击，没有 3 回合时限）。打完 / 击杀后照常回地图（不给奖励）；
  - **+3 能量**、**+3 辉星**：只在战斗中有效。
- **测试面板**（F8）：
  - 卡牌按原版卡面显示，点一下加入手牌；筛选储君 / 无色 / 全部，按卡名搜索，每页 12 张；
  - 按钮：击杀敌人（看胜利特效）、让我受击（3 点）、清空手牌、消耗手牌（F7 也行）、不死开关（所有玩家：一下最多扣到 1 点，低于 30% 补满）、打开日志文件夹。
- **联机同步**：所有会改动游戏状态的按钮都不直接改数值，而是发一条开发者控制台命令，走原版控制台同样的联机通道（ConsoleCmdGameAction → ActionQueueSynchronizer），每个玩家的游戏在同一个位置执行同一条命令。
  用到的命令：原版 `energy` / `stars` / `card` / `kill`，本工具的 `p3test dummy | immortal on|off | hurt <n> | hand clear|exhaust`（也可以在开发者控制台里直接输入）。
- **多人日志**：每个游戏进程单独写一个文件 `%APPDATA%\SlayTheSpire2\logs\p3_mp\p3mp_日期_时间_pid进程号.log`（同一台电脑开两个游戏不会互相覆盖），保留最近 30 个。
  - 原版自己的全部日志（联机握手、动作队列、校验和、状态分歧报错等；联机时动作 / 同步两类自动调到 Debug 级别）；
  - `[EVT]`：联机身份（单人 / 房主 / 客机）、双方的 mod 列表、玩家列表、进房间、战斗结束、测试命令的发送和执行；
  - `[SNAP]`：每回合开始和战斗建好时，所有生物的血量 / 格挡 / 能力，每个玩家的能量 / 辉星 / 手牌 / 抽牌堆 / 弃牌堆张数。**两个玩家的日志里同一回合的 SNAP 应该完全一样**，第一处不一样的就是开始不同步的地方；
  - `[EXC]`：未捕获的异常。P3Makoto 的报错也会写进原版日志，所以也在这里。
  每行立即写盘，游戏崩溃也不会丢。

## 本机双开联机测试
原版自带的本机联机测试参数 `--fastmp`：联机不走 Steam，改用本机 ENet（127.0.0.1:33771）。
- 房主：`SlayTheSpire2.exe --fastmp -wpos 0 0` → 多人游戏 → 主持；
- 客机：`SlayTheSpire2.exe --fastmp --clientId 1001 -wpos 960 0` → 多人游戏 → 加入（自动连本机房主）；
- `-wpos x y` 是原版的窗口位置参数（会把游戏切成窗口模式，并记进设置）；
- 第二个游戏的导演接口端口会冲突：启动前设环境变量 `P3_BRIDGE=0` 关掉它。
两个进程用同一个存档目录，测试前先备份存档。

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
| `/kill` `/hurt` `/dummy` `/console` | `amount` `command` | 击杀敌人、受击、换成木桩、开发者控制台命令 |

录制时不要碰鼠标（脚本在移动真实光标）；游戏用 1920×1080 全屏最稳。

## 实现
- `p3test dummy`：和原版 `fight` 命令同一个入口 `RunManager.EnterRoomDebug`，遭遇 `BattlewornDummyEventV1Encounter`；
- 木桩血量：`BattleFriendV1.MinInitialHp / MaxInitialHp`（只在 p3test dummy 建房时）+ `Creature.LoseHpInternal` 后补血；时限：`BattleFriendV1.AfterAddedToRoom` 跳过（只对测试木桩）；
- 不死：`Creature.LoseHpInternal` 前后；
- 这些补丁依据的状态只由联机同步的命令改写，各端在同一个动作里改，所以各端一致；
- 录制（导演接口）用过之后，单人局里能量 / 辉星保持 99（联机时不生效）。

## 编译 / 安装
`dotnet build -c Release`（testmod 目录），把 `P3TestMod.dll` 和 `P3TestMod.json` 放进
`<游戏目录>\mods\P3TestMod\`。不用时删掉这个文件夹即可。
