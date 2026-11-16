你在用户的 Windows 电脑上工作。任务：给 P5 Joker 画**召唤时「指挥」的新姿势帧**（3 张）。不改 mod 代码，不编译，不启动游戏。

**先读 docs/codex/README.md**：生图真透明底；**「P5 Joker 姿势图：人物大小 / 比例一致」「动画不能跳」「过渡帧够用就停」三节严格照做**
（以底图做图像编辑、pose_consistency.py、normalize_poses.py、sequence_check.py）。
找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/cool-pascal-mil5i8**。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-joker-command，报告分支名。

【用户要求（2026-11-03）】
召唤 Persona 时，现在的动作是：右手扶面具（summon_hold_a）→ 往上扯（summon_rip_a2）→ **右臂往后上方甩开、大衣扬起（summon_release_b 等）** → 放下。
用户**不要后半段往后甩手**，要：**扶着面具之后，Joker 把右手平伸向敌人（画面右侧），像指挥 Persona 去打它们那样，干脆利落。**
现在的几帧并排见 art/p5/poses/ref_summon_current.png（从左到右：summon_touch2_a、summon_hold_a、summon_rip_a2、summon_release_b、summon_down1_e、idle_a3）。

【要画的 3 张】（都在 art/p5/poses/，命名如下）
1. **summon_command_mid_a**：以 summon_hold_a 为底图编辑。右手离开面具，右臂从胸前往画面右侧挥出到一半（肘部还弯着，前臂已经朝右前方），手掌张开；
   身体、头、腿、大衣保持底图不变，只动右臂（大衣下摆可以跟着微微动）。
2. **summon_command_a**：以 summon_command_mid_a 为底图编辑。**右臂完全伸直、平举到肩膀高度，指向画面右侧（敌人那边）**，手掌张开、掌心朝前下、五指并拢
   （「去吧」的指挥手势，红手套清楚可见）；身体略微朝右前倾，下巴微抬、眼神看向右前方；大衣下摆因为这一下挥臂往后扬起一点。
   这是整段动作停留最久的一帧，要帅、要干脆。
3. **summon_command_lower_a**：以 summon_command_a 为底图编辑。右臂往下放到一半（介于平举和 idle_a3 垂手之间），身体回正一点；用来从指挥姿势回到 idle_a3。

【检查（照 README）】
- 每张出完先跑 pose_consistency.py（第 1 张对 summon_hold_a，脸被手挡住就按 README 用 idle_a3 当第一个参数；第 2、3 张对前一张），有 FAIL 就以底图重画，最多 2 次。
- 三张都加进 tools/p5/normalize_poses.py 的 POSES 表（比例 1.0，对齐方式和 summon_hold_a 一样），跑一次。
- 跑 sequence_check.py：summon_touch2_a summon_hold_a summon_command_mid_a summon_command_a summon_command_lower_a idle_a3，
  有 JUMP 就按 README 补过渡帧（以前后两张里已定稿的那张为底图，取中间姿势）；挥臂那一步（hold → command_mid → command）是爆发动作，可以 --allow。
- 出一张 check_command.png：六张按播放顺序并排（深灰底），放 art/p5/poses/。

【提交】只 add 新增的姿势图、art/p5/poses/final/ 下对应的新文件、tools/p5/normalize_poses.py 的改动、check_command.png；
提交信息 "art: Joker summon command poses (hand on mask -> right arm thrust toward the enemy)"。
【报告】中文：三张（及补的过渡帧）的位置、pose_consistency 和 sequence_check 的输出、重画次数、提交号或分支名。
