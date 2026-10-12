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
  - 按钮：击杀假人（看胜利特效）、让我受击（3 点伤害，看受击动画）、清空手牌。

## 实现
全部调用原版开发者控制台命令用的同一套接口（`card` / `energy` / `stars` / `fight` / `damage` / `kill`）：
- `RunManager.CreateRoom`：除地图外都返回 `BattlewornDummyEventV1Encounter` 战斗；
- `BattleFriendV1.AfterAddedToRoom`：跳过（不挂时限）；
- `PlayerCombatState.Energy / Stars` 的 setter：数值不低于 99。

## 编译 / 安装
`dotnet build -c Release`（testmod 目录），把 `P3TestMod.dll` 和 `P3TestMod.json` 放进
`<游戏目录>\mods\P3TestMod\`。不用时删掉这个文件夹即可。
