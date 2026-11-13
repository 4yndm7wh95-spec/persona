你在用户的 Windows 电脑上工作。任务：**给 P5 Joker 新画一张姿势图 summon_grip_a（扶面具的手攥紧）**。不改 mod 代码，不编译，不启动游戏。

**先读 docs/codex/README.md**（真透明底、姿势图大小 / 比例一致、安全清单都在里面，照做）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，
**分支是 claude/epic-archimedes-m9an69**（先 fetch 这个分支并切过去）。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-summon-grip，报告分支名。

【背景（用户 2026-11-08）】
毒性爆发这张牌：选中时 Joker 手扶面具（summon_touch2_a），打出后原来会切到插兜 + 伸手指挥，用户嫌变化太大。
现在改成：打出那一刻，**扶面具的那只手猛地攥紧，一直攥着**（约 1.2 秒，等巨手把眼神特写捏碎），然后松手放下。
动画和代码已经接好（tools/p5/build_rig_p5.py 的 cast_outbreak / cast_outbreak_from_mask），只缺这张图：
art/p5/poses/final/summon_grip_a.png 一出现，构建脚本就会自动用它（没有时用 summon_hold_a 顶着）。

【第 1 版为什么不对（用户 2026-11-08 验收：明显错误）】
第 1 版见 art/p5/poses/ref_grip_v1_wrong.png，问题：
- **脸上多画了一副面具**。我们的 Joker 姿势图里**脸上从来没有实体面具**（idle_a3、summon_touch2_a、summon_hold_a 都是素颜），
  「面具」只是游戏里代码画的蓝焰特效。上一版提示词里写了 mask，是提示词的错，这次提示词里**完全不提 mask**。
- 头转成了正脸、眼睛又大又白，和其他姿势图的 3/4 侧脸对不上。
- 手还是张开盖着，没有攥紧。
- 外套、垂着的手、身体都被重画了（不是局部编辑），切换时会跳。

【这次的做法：底图换成 summon_hold_a，只重画手】
- **底图：art/p5/poses/summon_hold_a.png**（1024×1536 RGBA）。这张手已经盖在脸上，头的角度、露出来的那只眼睛、外套全都对，
  而且游戏里本来就是 summon_touch2_a → summon_hold_a 这样切的，比例已经验过。
- **蒙版 1（主要）：只盖抬起来的那只红手套**——用颜色自动选出红手套的像素（高饱和红），往外扩 12～16 px，
  再补上手腕和袖口交界那一小段。**头发、脸、眼睛、领子、袖子都不在蒙版里**，一个像素都不改。
- **蒙版 2：画面右侧垂着的那只红手套**，同样按红色选出来、外扩 12～16 px。
- 蒙版外 0 改动；不要整图重绘，不要用整图生成再对齐。

【要画成】
- 蒙版 1：同一只手、同一个位置，从「张开盖着」改成**狠狠攥住自己的脸**：五指像爪子一样弯下去、指尖抠进脸颊和额头，
  指节高高凸起，手套皮革在指节上绷紧起褶，大拇指压在下巴 / 颧骨上。手指之间的缝隙变小。
  露出来的那只眼睛（底图里手指上方那只灰眼睛）**保持原样不动**——它不在蒙版里。
- 蒙版 2：垂着的手攥成拳，位置、大小、手腕和袖口不变。
- 红手套画法照 art/p5/poses/ref_command_hand_style.png（暗红阴影和高光、指节 / 手背褶子、粗黑描边、手指粗短有力）。
- 不要任何特效。真透明底（轮廓外 alpha = 0）。

可用的生图提示词（局部重画用，按工具调整，意思不要变；**不要加任何关于 mask 的词**）：

```
Repaint only the selected red glove. Same red-gloved hand in the same place on his face, but now gripping his own face hard:
fingers bent like claws, fingertips digging into the cheek and forehead, knuckles sharply raised, glove leather stretched and
creased over the knuckles, thumb pressed against the jaw, small gaps between the fingers. Keep the hand size, wrist and cuff.
Persona 5 Royal official illustration style: bold black outlines, cel shading, hard-edged dark-red shadows and highlights on
the red glove, short strong fingers. Nothing on the face except the hand. No effects, no glow, no text.
```

垂着的手：

```
Repaint only the selected red glove: the hanging hand clenched into a tight fist, knuckles facing the viewer, same size and
wrist position, red glove, bold black outlines, cel shading with hard dark-red shadows.
```

【检查】
1. 蒙版外逐像素核对：和 **summon_hold_a.png** 相比，蒙版外 0 改动（报告里写出蒙版外改动的像素数，必须是 0）；轮廓外 alpha = 0，边缘没有底色残留。
2. 一致性：脸被手挡住，所以用 idle_a3 比：
   `python tools/p5/pose_consistency.py idle_a3 summon_grip_a --occluded summon_grip_a`
   有 FAIL 就用底图重画，不要手工缩放。
3. 归一化：在 tools/p5/normalize_poses.py 的 POSES 表里加一行 `'summon_grip_a': (1.0, 'front'),`（和 summon_touch2_a、summon_hold_a 同样的写法），
   然后只对这一张跑归一化，产出 art/p5/poses/final/summon_grip_a.png（1536×1632 RGBA），其他 final 图不要动。
4. 出 art/p5/poses/check_summon_grip.png：summon_touch2_a、summon_hold_a、summon_grip_a 三张 final 并排（深灰底），再加一排手部放大。
   另外出一张 summon_hold_a 和 summon_grip_a 的差异图（改过的像素标红）：红色只能出现在两只手套的位置。
   **自己先看一眼：脸上除了手以外不能多出任何东西（没有面具、眼罩、眼眶白边），头还是 3/4 侧脸。**
最多重画 2 次，还不行就停下报告。

【提交】只 add：art/p5/poses/summon_grip_a.png、art/p5/poses/final/summon_grip_a.png、art/p5/poses/summon_grip_a_generation.json
（完整提示词、输入、原始输出路径、蒙版，格式照 summon_hold_a_generation.json）、art/p5/poses/check_summon_grip.png、art/p5/poses/ref_grip_v1_wrong.png（已在仓库里就不用管）、
tools/p5/normalize_poses.py（只加那一行）、art/p5/poses/REPORT_summon_grip.md。
提交信息 "art: Joker summon grip (hand clenched on the mask, for Outbreak)"。
**不要**跑 build_rig_p5.py / stage_p5.py，骨骼和 PCK 由 Claude 重建。

【报告】中文：检查输出原样、重画次数、提交号或分支名。
