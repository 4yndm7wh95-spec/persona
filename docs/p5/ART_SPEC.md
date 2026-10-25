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

## 阶段 1 结果（用户 2026-10-25）

- 画风选 **A_1**（art/p5/concept/style_A_p5r_official_1.png，P5R 官方立绘风）。唯一的问题：插兜的手用错了 → 应该**左手（远侧，画面偏右）插兜**，右手（近侧，画面偏左）自然下垂。
- 姿势库：research/p5r/pose/（64 段，index.csv 有每段的文字描述）。原版的武器 / 枪是单独模型，姿势图里手是空的。
  可用的参考：#1 待机、#10 / #28 前臂横挡（防御）、#50 手抬到太阳穴 / 脸侧（召唤 Persona）、#55 倒地、#7 / #26 奔跑。

## 阶段 2：母版（docs/codex/codex_p5_master.md）

以 A_1 做图像编辑，只换手：左手插兜、右手下垂。art/p5/master/joker_master_a / _b。

## 卡牌和动作的思路（铁甲战士 → Joker）

铁甲战士的牌偏体术、卖血、力量。P5 里正好对得上：**P5 的物理技能消耗 HP、由 Persona 打出**，名字都有重合（Cleave 就是 P5 的物理技能）。所以分三层：
1. **Joker 本人的动作**：普通攻击类（打击 = 小刀斩、重击 = 踢 / 刀柄砸）、防御（前臂横挡 / 闪身）、部分牌用枪（P5 的 GUN）。
2. **亚森出手**：伤害大的、多段的、AOE 的牌（顺劈斩、旋风斩、重锤、残杀……）= Joker 摆召唤姿势（手到脸侧、蓝焰），亚森出现挥爪 / 斩击，和 P3Makoto 的 Orpheus 一样。
3. **卖血 / 力量类**：卖血牌 = P5 物理技能耗 HP 的演出；力量 / 增益 = P5 的增益技能（Charge、Tarukaja、反叛的意志之类）。
需要的全身姿势图（第一批）：待机、小刀斩、踢、开枪、防御、召唤（手到脸侧）、受击、倒地、胜利。每个姿势先从姿势库挑参考帧。

## 阶段 2 结果

- 母版 art/p5/master/joker_master_a.png / _b 都把手换对了（左手插兜、右手垂下），默认用 **_a**。小问题留到接入时处理：上下留白只有 ~12px（接入时扩画布），轮廓外有 alpha=1 的极淡像素（接入时阈值清零）。

## 阶段 3：第一批姿势图（docs/codex/codex_p5_poses1.md）

参考帧（姿势总览序号 + 第几格）：idle #1-1、slash #11-4、kick #8-3、gun #52-3、guard #10-3、summon #50-3、cast #58-6、hurt #4-3、down #55-6、victory #57-5（拉手套）。
刀、枪都在右手。每个姿势 _a / _b 两张，art/p5/poses/。
