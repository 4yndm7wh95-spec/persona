你在用户的 Windows 电脑上工作。任务：**重画 Joker 召唤「指挥」姿势**（上一版太僵）。不改 mod 代码，不编译，不启动游戏。

**先读 docs/codex/README.md**：生图真透明底；「P5 Joker 姿势图：人物大小 / 比例一致」「动画不能跳」「过渡帧够用就停」三节照做。
找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/cool-pascal-mil5i8**。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-joker-command-v2，报告分支名。
**检查脚本现在不需要 scipy 了**（tools/p5/_ndimage.py 是纯 numpy 的替代，pose_consistency.py / sequence_check.py / normalize_poses.py 没有 scipy 时自动用它），这次请把检查都跑完。

【上一版的问题】（art/p5/poses/summon_command_mid_a.png、summon_command_a.png，见 REPORT_summon_command.md）
- 用户：**太僵硬，像在站很标准的军姿**。手臂笔直平举在肩高、五指并拢、身体直挺、两脚并着。
- 中间帧掌心朝上、手指托起，像捧着东西递出去，不像指挥。
- 这是 Claude 上一份任务写得太死（「完全伸直、平举到肩高、五指并拢」）造成的，这次不要照那个做。

【这次要的感觉】
扶着面具之后，**随手往敌人那边一挥：「去吧」**——像怪盗团团长指挥 Persona 出手，从容、带点帅气和不屑，动作干脆但人是放松的。
- **手臂**：右手从面具前方往画面右前方挥出去，停在**略高于肩、斜向前上方**的位置；**手肘微弯**，不要绷直；**手腕放松**、手背朝外或朝上，
  **手指自然张开、略弯**（不要并拢成一片，也不要掌心朝上托着）。
- **身体**：重心往前脚移，上身随挥臂略微转向敌人、肩膀放松（挥臂那侧的肩稍微送出去），头微微抬、侧着脸看向敌人，嘴角可以带一点笑。
- **腿**：两脚分开（前后错开一点，像刚往前迈了半步），前膝微屈；不要并脚站直。
- **大衣**：被这一挥带起来，下摆往后、往外甩开。
- 参考 P5 Joker 战斗里施法 / 胜利时那种松弛又帅的动作感觉，不要照抄任何截图。

【要画的】（art/p5/poses/，可以覆盖上一版同名文件；旧版留在 git 历史里就行）
1. **summon_command_mid_a**：以 summon_hold_a 为底图编辑——右手离开面具，挥到一半（手在胸前偏右，肘弯着，手背领先、手指松开）；身体开始转向敌人、重心开始前移。
2. **summon_command_a**：以新的 summon_command_mid_a 为底图编辑——挥到位（上面「这次要的感觉」），这是停留最久的一帧，要帅。
3. **summon_command_lower_a**：以 summon_command_a 为底图编辑——手臂放下一半、身体回正一点、腿往回收一点，用来回到 idle_a3。
先只出第 2 张给用户看，用户说可以再出第 1、3 张（用户在你对话里会直接回答）。

【检查（照 README）】pose_consistency.py（第 1 张对 summon_hold_a，脸被手挡就用 idle_a3 当第一个参数）；三张都加进 normalize_poses.py 的 POSES 表并跑一次；
sequence_check.py：summon_touch2_a summon_hold_a summon_command_mid_a summon_command_a summon_command_lower_a idle_a3，挥臂那一步可以 --allow，其余 JUMP 补过渡帧。
出 check_command.png（六张按顺序并排，深灰底）。

【提交】只 add 新增 / 覆盖的姿势图、art/p5/poses/final/ 下对应文件、normalize_poses.py 的改动、check_command.png；
提交信息 "art: Joker summon command poses v2 (relaxed 'go' wave)"。
【报告】中文：图的位置、检查输出、重画次数、提交号或分支名。
