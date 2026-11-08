你在用户的 Windows 电脑上工作。任务：给「华丽收场 / 大罪穿甲弹」演出生成**断崖平台**的图。不改 mod 代码，不编译，不启动游戏，不用 Computer Use。

**先读 docs/codex/README.md**（生图一律真透明底）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/epic-archimedes-m9an69**
（仓库副本已经在这个分支上：先 fetch origin claude/epic-archimedes-m9an69:refs/remotes/origin/claude/epic-archimedes-m9an69，再 merge --ff-only）。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-sinful-platform，报告分支名。

【用途（用户 2026-11-08）】
打出「华丽收场」时整个战斗场景变成夜空（天空、乌云由游戏里的着色器实时画，不用你画），场景里只有**一块平台**：
从**屏幕右边伸出来**，一直延伸到画面左边约 1/5 处，然后是**悬崖**（往下断掉）。敌人站在平台右边，Joker 站在平台**最左边的悬崖边上**；
悬崖左边是空的深渊，巨大的人格面具（撒旦耶尔）从那里顶天立地地立着。
现在的布局示意：art/p5/fx/v4/satanael/ref_layout.png（平台目前是着色器画的一块平板，太简单，要换成你的图）。
原版氛围参考：art/p5/fx/v4/satanael/ref_platform_1.png、ref_platform_2.png（夜色、冷蓝光、脚下是黑色岩石）。

【要画的】一张图 art/p5/fx/v4/satanael/platform.png：
1. **3840×1100，真透明底**。平台的**顶面是一条接近水平的线**，在图片里 **y ≈ 140**（顶面以上全透明，只有零星几块碎石 / 翘起的岩角可以略高出顶面）；
   顶面从图的**右边缘一直到 x ≈ 760**，在 x ≈ 760 处是悬崖的边沿（Joker 站的地方），往左是空的（透明）。
2. **悬崖面**：从边沿往下延伸到图片底部，崖面**往下、往右收进去**（上面突出、下面凹进），有大块的断层、裂缝、悬着的碎石；崖面底部可以碎成往下掉的石块、渐渐隐入黑暗（底部可以半透明淡出）。
3. **平台本体**：黑色 / 深蓝黑的岩石，厚重、有层理和裂纹；**顶面边缘一道被冷色天光照亮的细亮边**（偏青蓝）；悬崖面靠下被深渊底下的暗红光映出一点红边。
   顶面是可以站人的平地（略有起伏，但整体是平的），不要树、不要建筑、不要人物。
4. 画风：和 Joker / 人格面具一致的 P5 赛璐璐风（粗黑描边、两阶硬明暗、颜色压暗），不是写实照片。
5. 整体偏暗，不要高光过曝；不要天空、背景、烟雾（这些游戏里另外做）。
输出 platform.png（+ _raw.png + _generation.json）、check_platform.png（把 platform.png 按 1920 宽缩小后叠在 ref_layout.png 上：顶面对齐 Joker 脚底那条线，深灰底另放一张原图）。
最多重画 2 次。

【提交】只 add 新文件；提交信息 "art: Sinful Shell scene platform (rock ledge from the right, cliff on the left)"。
【报告】中文：图的位置、顶面在图里的 y、悬崖边沿的 x、重画次数、提交号或分支名。
