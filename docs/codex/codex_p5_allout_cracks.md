你在用户的 Windows 电脑上工作。任务：给 P5Joker 的总攻击画几张**玻璃被击碎的裂痕**贴图（生图），给 Claude 叠在程序做的碎玻璃上用。不改任何代码，不启动游戏。

**先读 docs/codex/README.md**，按里面的长期规则来：生图一律真透明底（RGBA，裂纹以外 alpha = 0），每张只生成一次，生成完清理透明残点、保存、直接提交推送，不自我审查、不重画、不写报告。

【仓库】
用你上次成功推送的那个仓库（remote = github.com/4yndm7wh95-spec/persona），所有 git 命令加 -c safe.directory=<repo>。fetch 并切到 claude/exciting-babbage-lm8ubb（fetch 带完整写法 origin claude/exciting-babbage-lm8ubb:refs/remotes/origin/claude/exciting-babbage-lm8ubb，失败按 5 / 10 / 20 / 40 秒重试），merge --ff-only；不能快进就停下来报告。

【用途】
总攻击开头：最后一击打中敌人那一刻画面定格，**以敌人为中心**整块屏幕像玻璃一样被击裂，0.3 秒内裂纹从撞击点爬满全屏，然后碎开。现在的裂纹是程序画的折线，用户觉得太像蜘蛛网、太规整。要的是**真实的钢化玻璃 / 屏幕被重击后的裂痕**，同时带 P5 的锐利感。

【画面要求（每张都一样）】
- 纯白裂纹（可以有极淡的灰白层次表现玻璃裂面的反光），透明底，裂纹以外 alpha = 0。不要画背景、不要画玻璃本身、不要阴影、不要颜色。
- 撞击点在画布**正中心**：中心一小团密集粉碎（细碎的短裂纹和碎屑，直径约画布宽的 6%），从这里放射出 9～14 条主裂纹，**粗细、长短、走向都不规则**（有的笔直冲到画布边、有的半路拐弯或停下），主裂纹上有分叉；只在中心附近有零星几段弧形的横向裂纹（不要一圈圈整齐的环，**不要蛛网**）。
- 线条：靠近撞击点粗（约 6～8px），越远越细（最细 1px），线条边缘锐利、有玻璃断口那种细小的锯齿和崩口，不要模糊、不要发光。
- 画布 2048×2048。

【要画的四张】放 art/p5/fx/gen_codex/allout_cracks/：
1. crack_main.png：上面描述的完整裂痕（主用）。
2. crack_alt.png：同样要求，换一种裂法（主裂纹更少、更长、更斜）。
3. crack_core.png：只有撞击点那一团粉碎（画布 1024×1024，中心密集碎裂，往外 30% 半径内就淡出），叠在撞击点上用。
4. crack_branch_sheet.png：2×2 宫格，每格一条单独的长分叉裂纹（从格子左边中点出发往右延伸、带 2～4 个分叉），画布 2048×2048，Claude 拿来补在程序裂纹上。

【提交】
只 add art/p5/fx/gen_codex/allout_cracks/；commit -m "art: P5 all-out glass crack overlays (Codex)"；push origin claude/exciting-babbage-lm8ubb（被拒先 pull --no-rebase --no-edit 再推，有冲突就停下来报告）。回复提交号。
