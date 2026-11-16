你在用户的 Windows 电脑上工作。任务：**只改 summon_command_a 的右手**（v3 的手心朝向不对）。不改 mod 代码，不编译，不启动游戏。

**先读 docs/codex/README.md**。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/cool-pascal-mil5i8**。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-joker-command-v3b，报告分支名。

【用户对 v3 的验收（2026-11-03）】
- 左手插兜：**通过**（summon_command_a、summon_command_mid_a 的左手都不要再动）。
- 右手：**不对**。用户要「**手心向下**」的指挥手势。v3 的右手是**手心朝着镜头**、大拇指朝上、五指张开（像侧着举手说「停」/ 击掌），
  见 art/p5/poses/ref_command_hand_v3.png（放大图，能看到掌心的纹路就说明朝向错了）。
- 另外手套和袖口之间有一条竖直的接缝（贴回的边界），这次一起处理干净。

- 用户追加（2026-11-03）：「这手不对」。除了朝向，v3 的手**画风也和身上其他红手套对不上**：平涂、没有明暗、描边细、手指又细又长，像另外贴上去的。
  局内已定稿的红手套长这样：art/p5/poses/ref_command_hand_style.png（从 summon_release_b、summon_open2_b、summon_open1_b 放大）——
  手套有暗红阴影和高光、指节 / 手背的褶子、粗黑描边、手指粗短有力、手腕被袖口包住。**新手的画法、粗细、明暗照这三只手**（这三只露的正好是手背）。

【要改成】（做法同 v3：在 art/p5/poses/summon_command_a.png 上局部蒙版重画右手，蒙版只盖右手和袖口边缘，其他像素保持 0 改动，然后按 v3 的方法贴回、归一化）
- **手心朝下（朝地面）、手背朝上**：从侧面看，看到的是**手背和手的侧边**，看不到掌心纹路；
- 手腕平、顺着前臂方向往前伸，不往下耷拉也不往上翘；
- 四指并拢或微微分开、自然伸直略弯，指尖指向敌人（画面右侧），大拇指在下方、贴着食指一侧（从这个角度大部分被挡住）；
- 就是「去吧」时往前一压的手势：手掌像往敌人那边按下去；
- 手的大小和 v3 一样，红手套，黑色粗描边和画风不变；
- 手套和袖口的衔接自然，袖口压住手套的腕口，没有竖直接缝。

【检查】pose_consistency.py gun_aim_b summon_command_a（应全部 x1.000、ok）；normalize_poses.py 跑一次（POSES 表已有这一行，不用再加）；
出 check_command_v3b.png：v3 和 v3b 的右手区域放大并排 + 全身并排（深灰底）。最多重画 2 次。

【提交】只 add summon_command_a.png（含 final/）、generation.json、check_command_v3b.png、REPORT_summon_command_v3b.md；
提交信息 "art: Joker summon command v3b (right hand palm down)"。
【报告】中文：检查输出原样、重画次数、提交号或分支名。做完再接着做亚森 v7 拆层。
