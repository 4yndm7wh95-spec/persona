你在用户的 Windows 电脑上工作。任务：**给 P5 Joker 新画一张姿势图 summon_grip_a（扶面具的手攥紧）**。不改 mod 代码，不编译，不启动游戏。

**先读 docs/codex/README.md**（真透明底、姿势图大小 / 比例一致、安全清单都在里面，照做）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，
**分支是 claude/epic-archimedes-m9an69**（先 fetch 这个分支并切过去）。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-summon-grip，报告分支名。

【背景（用户 2026-11-08）】
毒性爆发这张牌：选中时 Joker 手扶面具（summon_touch2_a），打出后原来会切到插兜 + 伸手指挥，用户嫌变化太大。
现在改成：打出那一刻，**扶面具的那只手猛地攥紧，一直攥着**（约 1.2 秒，等巨手把眼神特写捏碎），然后松手放下。
动画和代码已经接好（tools/p5/build_rig_p5.py 的 cast_outbreak / cast_outbreak_from_mask），只缺这张图：
art/p5/poses/final/summon_grip_a.png 一出现，构建脚本就会自动用它（没有时用 summon_hold_a 顶着）。

【底图和参考】
- **底图：art/p5/poses/summon_touch2_a.png**（1024×1536 RGBA，选中牌时扶面具的姿势，游戏里从它直接切到新图）。
  以它为底图做**局部蒙版重画**：蒙版只盖抬起的那只手 + 前臂 + 肩、头部（脸、头发），以及另一侧垂着的那只手；
  外套、身体、腿、脚、外套下摆的像素**保持 0 改动**。不要整图重绘。
- 手的位置参考：art/p5/poses/summon_hold_a.png（手整个盖在面具上）。新图的手介于两者之间，但要**攥紧**。
- 红手套的画法照局内已定稿的手：art/p5/poses/ref_command_hand_style.png（暗红阴影和高光、指节 / 手背褶子、粗黑描边、手指粗短有力、袖口包住腕口）。
- 画风母版：art/p5/concept/style_A_p5r_official_1.png。

【要画成】
- **同一只手**：就是 summon_touch2_a 里扶着面具的那只（近侧，画面偏左那只红手套），不能换手。
- 五指像爪子一样弯曲，**抠进面具和脸**：指节高高凸起，手套皮革在指节上绷紧、起褶；大拇指压在颧骨上。
- 前臂绷紧、微微往里拧，手肘比 summon_touch2_a 抬高一点、往身体收；这一侧的肩膀因为用力耸起来。
- 头微微低下、往手里压。指缝间露出一只眼睛：眯着、狠狠地瞪着，冷而有威胁感；嘴抿成一条线。
- 另一侧垂着的手（画面偏右那只红手套）位置不变，但**也攥成拳**。
- 不要任何特效：没有火、没有光、没有速度线、没有文字。**真透明底**（轮廓外 alpha = 0）。

可用的生图提示词（按工具需要调整，意思不要变）：

```
Edit the reference image (Joker, Persona 5 Royal, Phantom Thief outfit). Keep the coat, body, legs, feet and coat tails
exactly as they are; only repaint the masked area (raised arm, shoulder, head, and the other hand).
The near-side red-gloved hand on his face is now CLENCHED HARD over the domino mask: fingers bent like claws digging into the
mask and face, knuckles sharply raised, glove leather stretched and creased over the knuckles, thumb pressed against the
cheekbone. Forearm tense and slightly twisted inward, elbow a little higher and closer to the body, that shoulder hunched with
strain. Head tilted slightly down and forward into the grip. One eye visible between the fingers, narrowed and glaring, cold and
menacing; mouth pressed into a tight line. The far-side hand hanging at his side is clenched into a tight fist.
P5R official illustration style, bold black outlines, cel shading with hard-edged dark-red shadows and highlights on the red
gloves, short strong fingers. No effects, no flames, no glow, no motion lines, no text. Transparent background.
```

【检查】
1. 蒙版外逐像素核对：和 summon_touch2_a.png 相比，蒙版外 0 改动；轮廓外 alpha = 0，边缘没有底色残留。
2. 一致性：脸被手挡住，所以用 idle_a3 比：
   `python tools/p5/pose_consistency.py idle_a3 summon_grip_a --occluded summon_grip_a`
   有 FAIL 就用底图重画，不要手工缩放。
3. 归一化：在 tools/p5/normalize_poses.py 的 POSES 表里加一行 `'summon_grip_a': (1.0, 'front'),`（和 summon_touch2_a、summon_hold_a 同样的写法），
   然后只对这一张跑归一化，产出 art/p5/poses/final/summon_grip_a.png（1536×1632 RGBA），其他 final 图不要动。
4. 出 art/p5/poses/check_summon_grip.png：summon_touch2_a、summon_hold_a、summon_grip_a 三张 final 并排（深灰底），再加一排手部放大。
   另外把 summon_touch2_a 和 summon_grip_a 半透明叠在一起看一眼：除了手臂、肩、头和另一只手，其他地方应该完全重合。
最多重画 2 次，还不行就停下报告。

【提交】只 add：art/p5/poses/summon_grip_a.png、art/p5/poses/final/summon_grip_a.png、art/p5/poses/summon_grip_a_generation.json
（完整提示词、输入、原始输出路径、蒙版坐标，格式照 summon_touch2_a_generation.json）、art/p5/poses/check_summon_grip.png、
tools/p5/normalize_poses.py（只加那一行）、art/p5/poses/REPORT_summon_grip.md。
提交信息 "art: Joker summon grip (hand clenched on the mask, for Outbreak)"。
**不要**跑 build_rig_p5.py / stage_p5.py，骨骼和 PCK 由 Claude 重建。

【报告】中文：检查输出原样、重画次数、提交号或分支名。
