你在用户的 Windows 电脑上工作。任务：**亚森第 7 版草图**（改朝向）。不改 mod 代码，不编译，不启动游戏。

**先读 docs/codex/README.md**（生图一律真透明底）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/cool-pascal-mil5i8**。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-arsene-v7，报告分支名。

【用户对第 6 版的意见】
- v6B（前压出招）太夸张，不要。
- v6A（挺立张开）的气势可以，但**朝向不对**：现在是正面朝镜头。亚森在战斗里站在 Joker 身后、和 Joker 一起面对右边的敌人，
  所以应该和局内的 Joker 差不多朝向——**3/4 侧身朝画面右侧**。
- 参考之前三渲二亚森的朝向和姿势。

【参考】art/p5/fx/v4/arsene/ref_v7_facing.png（从左到右：三渲二亚森出手那一帧、三渲二亚森定格、局内 Joker 的站姿 idle_a3）；
原图在 art/p5/fx/arsene/cast/0024.png、art/p5/fx/arsene/arsene_still.png、art/p5/poses/final/idle_a3.png。

【要求】以 art/p5/fx/v4/arsene/arsene_draft_v6a.png 为底图编辑，设计、画风、描边、配色完全不变，改这些：
1. **身体转成 3/4 侧身朝右**：胸口、骨盆、头都朝右前方（和 Joker idle_a3 的转身角度接近），看得到右肩在前、左肩在后；鸟喙面具朝右。躯干挺直，不弯腰、不缩。
2. **两翼大幅展开、偏水平**（像三渲二那两帧）：近处那只翅膀（身体左后方）往左上方伸展，远处那只往右上方伸展；两翼尽量少被身体挡住。
3. **手臂**：靠近敌人那只（右臂）往右前方伸出、爪子张开；另一只手臂自然往身体外侧张开。
4. **腿**：悬浮，自然垂下，两腿之间留一点空隙（不要并拢夹紧），一只膝盖微屈；高跟靴尖朝下。不要大跨步。
5. 竖构图 2048×2304，透明底，四周至少留 60px，翼尖、帽顶、靴尖都不碰画布边；不要背景、光晕、烟雾。
输出 art/p5/fx/v4/arsene/arsene_draft_v7.png（+ _raw.png + _generation.json）和 check_arsene_v7.png（从左到右：v6A、v7、三渲二出手帧、Joker idle_a3；深灰底）。
最多重画 2 次。**只出草图，不拆层**，等用户确认。

【提交】只 add 新文件；提交信息 "art: Arsene draft v7 (v6A turned three-quarter to face the enemy)"。
【报告】中文：草图位置、重画次数、自己觉得哪里还不像、提交号或分支名。
