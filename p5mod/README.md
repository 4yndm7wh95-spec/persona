# P5Joker（静默猎手换皮）

第一版只有 PCK（骨骼动画资源），没有 DLL：战斗、商店、休息处的静默猎手换成 Joker。

## 生成（云端）
1. `python tools/p5/build_rig_p5.py`：战斗骨骼（整图切换 + 交叉淡化 + 呼吸 + 眨眼 + 闲置扯手套），预览 GIF 在 `art/p5/rig/preview/`。
2. `python tools/p5/build_restsite_p5.py`：休息处（Joker + 摩尔加纳，网格微动 + 两人错开眨眼）。
3. `python tools/p5/stage_p5.py`：写 `p5mod/pck_stage/`，按原版 uid 替换
   `animations/characters/silent/`、`animations/merchant/silent/`、`animations/rest_site/silent/`。

## 打包安装（本地）
`gdre_tools --headless --pck-create=<p5mod/pck_stage 的副本> --output=P5Joker.pck --pck-version=3 --pck-engine-version=4.5.1`，
把 `P5Joker.json` 和 `P5Joker.pck` 放进 `游戏目录/mods/P5Joker/`。

## 第一版的限制
- cast 固定播普通召唤，shiv 固定播一次完整的开枪；按牌切换（召唤 / 防御 / 扯手套、选牌举枪 / 打出开火）要 DLL，第二版做。
- 没有特效（蓝焰面具、斩痕、枪口火光等），也没有换头像、选人界面、能量球。
