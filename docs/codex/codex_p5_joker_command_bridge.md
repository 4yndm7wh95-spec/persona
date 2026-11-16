你在用户的 Windows 电脑上工作。任务：给 Joker 召唤动作补**过渡帧**：从「扶面具」（summon_hold_a）到「指挥」（summon_command_mid_a）。不改 mod 代码，不编译，不启动游戏。

**先读 docs/codex/README.md**：生图真透明底；「P5 Joker 姿势图：人物大小 / 比例一致」「动画不能跳」「过渡帧够用就停」三节严格照做。
找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/cool-pascal-mil5i8**。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-joker-command-bridge，报告分支名。

【问题】用户 2026-11-03：「从扶着面具到指挥那个动作跳跃太大了，应该有几帧过渡」。
sequence_check.py summon_hold_a summon_command_mid_a 的结果：overlap 0.33、脸移动 189px、一只脚移动 383px → JUMP。
两帧差在：①右手从脸上（面具）落到身前偏右下；②左手从身侧垂着变成插进裤兜；③站姿从两脚靠拢（summon_hold_a）变成前后大步分开（gun 系列站姿）；④身体从略侧转向敌人。

【要画的】art/p5/poses/summon_bridge1_a、summon_bridge2_a（需要的话再加 summon_bridge3_a），按 README「过渡帧以前后两张里已定稿的那张为底图编辑、姿势取两者正中间、脚的位置也取中间」：
- **summon_bridge1_a**：以 **summon_hold_a** 为底图编辑。右手离开面具、在下巴前方往前送出一点（手还在胸口高度，手指松开）；左手开始往胯边抬；
  后脚往后撤出约三分之一（两脚之间拉开一点），身体开始转向画面右侧。头的大小、身高、大衣不变。
- **summon_bridge2_a**：以 **summon_command_mid_a** 为底图编辑。右手比 summon_command_mid_a 更靠胸前、更高一点（介于下巴和身前偏右下之间）；
  左手已经快插进兜里（手掌一半进兜）；两脚的距离介于 bridge1 和 command_mid 之间。
- 两张做完跑 sequence_check：summon_hold_a summon_bridge1_a summon_bridge2_a summon_command_mid_a summon_command_a。
  **每一步都不能 JUMP**（脸 ≤ 90px、每只脚一步 ≤ 120px、overlap ≥ 0.62）；还有 JUMP 就在那一步中间再补一张 summon_bridge3_a（照同样规则取中间），最多补到 3 张。
  这是一下挥手的动作，每张在游戏里只停 0.03～0.04 秒，脚 120～140px 的小超标可以接受（README「过渡帧够用就停」）。

【检查】每张 pose_consistency.py（bridge1 对 summon_hold_a，脸被手挡就用 idle_a3 当第一个参数；bridge2 对 summon_command_mid_a）；
新帧都加进 normalize_poses.py 的 POSES 表（比例 1.0；bridge1 的对齐方式抄 summon_hold_a 那行、bridge2 抄 summon_command_mid_a 那行），跑一次；
出 check_command_bridge.png：summon_hold_a → 过渡帧 → summon_command_mid_a → summon_command_a 按顺序并排（深灰底）。

【提交】只 add 新帧（含 final/）、generation.json、normalize_poses.py 改动、check_command_bridge.png、REPORT_summon_command_bridge.md；
提交信息 "art: Joker summon bridge frames (hand on mask -> command)"。
【报告】中文：每张的位置、pose_consistency 和 sequence_check 输出原样、重画次数、提交号或分支名。
