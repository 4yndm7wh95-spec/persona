你在用户的 Windows 电脑上工作。任务：**Joker 召唤「指挥」姿势第 3 版**——换底图做法。不改 mod 代码，不编译，不启动游戏。

**先读 docs/codex/README.md**：生图真透明底；「P5 Joker 姿势图：人物大小 / 比例一致」「动画不能跳」「过渡帧够用就停」三节照做。
找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/cool-pascal-mil5i8**。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-joker-command-v3，报告分支名。

【为什么换做法】
v1 像军姿，v2（art/p5/poses/summon_command_a.png）用户说「姿势还是怪」：手腕往下耷拉像在招手 / 托着东西，手臂举到头的高度，身体往后仰，
头比 idle 大 8.9%、脚也挪了（pose_consistency FAIL）。从 summon_hold_a 自由重画每次都会走样。

**这次直接用游戏里已经定稿的举枪瞄准帧 art/p5/poses/final/gun_aim_b.png 当底图**：右臂已经伸直指向敌人、左手插兜、站姿松弛，
比例和局内完全一致——正好就是「指着敌人让 Persona 上」的样子，只要**把手里的枪换成空手**。
对照图：art/p5/poses/ref_command_v3.png（左 gun_aim_b、中 gun_recoil_b、右被否的 v2）。

【要画的】（覆盖 art/p5/poses/ 下同名文件；旧版在 git 历史里）
1. **summon_command_a**：以 **gun_aim_b**（final 版）为底图做**局部编辑**，只改右手：去掉手枪，换成**张开的红手套手**——
   五指张开、略微弯曲，掌心朝前偏下（对着敌人方向「去吧」），手腕和前臂延长线自然、不耷拉不上翘；手的大小和左手 / idle_a3 的手一致。
   身体、头、左手插兜、腿、脚、大衣**完全不动**（蒙版只盖右手和手枪那一块）。
2. **summon_command_mid_a**：以 **gun_arm_mid_h**（final 版，掏枪途中那帧）为底图做同样的局部编辑：去掉枪，右手空手、手指松开。
   用作「扶面具 → 指挥」中间的过渡；如果 gun_arm_mid_h 的手本来就没拿枪，原样复制一份改名即可，并在报告里说明。
3. 不需要收手帧：指挥完直接走举枪动作原有的收回帧（gun_arm_mid_h → pocket_in_e → idle_a3），Claude 那边接。

【检查】
- pose_consistency.py gun_aim_b summon_command_a、pose_consistency.py gun_arm_mid_h summon_command_mid_a：因为是局部编辑，头 / 身体 / 靴子应该几乎 1.0，有 FAIL 就说明蒙版外被改了，重画（最多 2 次）。
- 两张加进 normalize_poses.py 的 POSES 表（比例、对齐方式抄 gun_aim_b / gun_arm_mid_h 那两行），跑一次。
- sequence_check.py summon_hold_a summon_command_mid_a summon_command_a（挥臂这步可以 --allow）。
- 出 check_command_v3.png：gun_aim_b、summon_command_a 并排 + 右手区域放大对比（深灰底）。

【提交】只 add 两张图（含 final/ 下的）、generation.json、normalize_poses.py 改动、check_command_v3.png、报告 REPORT_summon_command_v3.md；
提交信息 "art: Joker summon command v3 (gun_aim_b with an open hand)"。
【报告】中文：检查输出原样、重画次数、提交号或分支名。做完再接着做亚森 v7 拆层（docs/codex/codex_p5_arsene_v7_layers.md）。
