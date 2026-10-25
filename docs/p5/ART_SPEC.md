# P5 Joker mod 美术规格

下一个 mod：杀戮尖塔 2 的**铁甲战士**换成 P5R 的雨宫莲（怪盗服，**不戴面具**），Persona 亚森（Arsène）。思路和 P3Makoto 一样。

## 决定（用户 2026-10-25）

- 局内人物走 P3Makoto 的路线：**生图母版 → 每个关键姿势画一张完整的全身姿势图**（P3Makoto ART_SPEC 阶段 12 的做法），游戏里在姿势图之间切换 + 小幅网格微动。
- **不直接用 P5R 原版 3D 模型渲 2D**（用户：原版建模粗糙，被玩家叫"比目鱼"）。原版模型和动作**只做姿势参考**：要做某个动作时，先从原版动作里挑合适的一帧，用 3D 渲成姿势参考图，再让生图按参考图的姿势、按母版的画风重画。
- 参考图：art/p5/ref/joker_nomask_ref.jpg（用户给的不戴面具的样子，只表示造型，不是指定画风）；art/p5/ref/joker_thief_nomask_3d.png（原版模型渲的服装参考：正面 + 3/4）。

## 原版资源状态（research/p5r/）

- 模型修复：tools/p5r/fix_gfd_glb.py `--drop kamen,Blur --flip-v`（GFD Studio 导出的逆绑定矩阵错、无蒙皮网格多变换一次、UV 上下颠倒；Blur* 是动态模糊替身，隐藏）。成品在本机资源库 model/joker/fixed2/。
- 动作套用：**只套 rotation 通道**（research/p5r/model/diag/README_round3.md 的 C 方案），translation / scale 用模型自己的。原样套会出框。
- 亚森：MODEL/CHARACTER/PERSONA/0201/PS0201.GMD（不是 0000 = Mokoi，也不是 0001 = Metatron）。研究资料里旧的 arsene 命名待更正。
- 怪盗服战斗动作：BB0001_051.GAP（64 段）。

## 阶段 1：定画风 + 姿势库（docs/codex/codex_p5_stage1.md）

- 3 个画风候选的全身定妆图，用户挑一张作母版。
- 64 段战斗动作的姿势总览（只套旋转），以后挑动作参考用。
