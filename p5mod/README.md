# P5Joker（静默猎手换皮）

## 0.5.7：眼神特写复用 P5R 原版数据

召唤时的眼神特写改为播放原版 `BCT_P_SUMMON.GMD` + `BCT_P_SUMMON_BT.GMD` 转出的几何、UV 和材质关键帧，使用现有 Joker `eyes.png`。原版裂缝、白边、红纸收拢、碎片、双感叹号及其破碎均随源数据播放，替换 0.5.6 的程序近似。转换数据嵌入 DLL，贴图和既有召唤触发点继续复用。

预览：[GIF](../docs/p5/preview/eye_cutin_native.gif)。来源、复现方式和验证范围见 [转换工具说明](../tools/p5/native_cutin/README.md) 与 [报告](../docs/codex/reports/p5_native_eye_cutin_0.5.7.md)。

PCK（骨骼动画 + 特效贴图）+ DLL（特效代码，p5mod/src）：战斗、商店、休息处的静默猎手换成 Joker；0.2.0 起有第一批出牌特效（小刀斩、开枪）。

## 生成（云端）
1. `python tools/p5/build_rig_p5.py`：战斗骨骼（整图切换 + 交叉淡化 + 呼吸 + 眨眼 + 闲置扯手套），预览 GIF 在 `art/p5/rig/preview/`。
2. `python tools/p5/build_restsite_p5.py`：休息处（Joker + 摩尔加纳，网格微动 + 两人错开眨眼）。
3. `python tools/p5/stage_p5.py`：写 `p5mod/pck_stage/`，按原版 uid 替换
   `animations/characters/silent/`、`animations/merchant/silent/`、`animations/rest_site/silent/`。

## 编译特效代码（本地）
`dotnet build -c Release`（在 p5mod 目录），产物 `bin/Release/net9.0/P5Joker.dll`。云端只能做类型检查。

## 打包安装（本地）
`gdre_tools --headless --pck-create=<p5mod/pck_stage 的副本> --output=P5Joker.pck --pck-version=3 --pck-engine-version=4.5.1`，
把 `P5Joker.json`、`P5Joker.dll` 和 `P5Joker.pck` 放进 `游戏目录/mods/P5Joker/`。

## 第一版的限制
- cast 固定播普通召唤，shiv 固定播一次完整的开枪；按牌切换（召唤 / 防御 / 扯手套、选牌举枪 / 打出开火）要 DLL，第二版做。
- 特效只有小刀斩（斩痕、红黑爆点、碎片）和开枪（枪口火光、弹道、原版命中火光）；召唤、防御、增益还没有。没有换头像、选人界面、能量球。
