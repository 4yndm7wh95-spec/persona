你在用户的 Windows 电脑上工作。任务：把**已确认的亚森第 7 版修脸版**（art/p5/fx/v4/arsene/arsene_draft_v7_facefix.png，用户 2026-11-03 通过；以它为准，不是未修脸的 arsene_draft_v7.png）拆成游戏里做 2D 骨骼动画用的分层图。
不改 mod 代码，不编译，不启动游戏。

**先读 docs/codex/README.md**。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/cool-pascal-mil5i8**。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-arsene-v7-layers，报告分支名。

**做法照上次第 5 版拆层**（art/p5/fx/v4/arsene/layers/ 和你那次的 README / layers.json / check 图，那次的做法用户和 Claude 都认可）：
各层独立生成后按全图手工对位、叠起来和全图一致、被遮挡处补全、每层同样大小同样坐标。第 7 版四周已经留了边，不需要扩画布。

【输出】art/p5/fx/v4/arsene/layers_v7/（不要覆盖旧的 layers/）：
- wing_back.png（身后 / 画面左侧那只翅膀，根部补全）、wing_front.png（另一只翅膀，根部补全）、
  arm_back.png（叉腰的那只手臂）、body.png（头 + 礼帽 + 躯干 + 燕尾 + 腿，不含手臂和翅膀，被手臂挡住的胸口 / 领巾 / 腰部补全）、
  arm_reach.png（抬在胸前领巾处的那只手臂——游戏里出手时由它朝敌人猛地探出去，所以手臂和手要完整、肩关节清楚）、eyes_glow.png（只有发光的眼睛）。叠放顺序按画面实际的前后关系定，写进 layers.json。
- layers.json：每层文件名、z_index（从后往前）、关节点（全图像素坐标）：两翼肩根、两只手臂的肩关节、身体浮动中心、**胸前那只手的指尖 claw_tip**、
  **脚尖最低点 toe**（悬浮时贴近地面的那只靴尖）、**帽顶 hat_top**。
- check_layers.png、check_moved.png（翅膀 ±12°、手臂 ±10° 的叠图，看有没有露洞；露洞就补，最多 2 次）。

【提交】只 add art/p5/fx/v4/arsene/layers_v7/；提交信息 "art: Arsene v7 layered for 2D puppet animation"。
【报告】中文：每层补画了哪些地方、check_moved 有没有露洞、layers.json 位置、提交号或分支名。
