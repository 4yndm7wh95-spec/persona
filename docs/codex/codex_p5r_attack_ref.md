你在用户的 Windows 电脑上工作（姿势图那个窗口）。任务：把 P5R 原版 Joker 的**普通攻击（小刀）**完整渲出来给用户看，并导出原版小刀的模型和外观，作为重画 slash 的依据。不改 mod 代码，不生图，不启动游戏，不用 Computer Use。

找仓库、拉取、提交推送、「绝对不要做」清单和 docs/codex/codex_p5_poses4.md 开头一样（不要 rebase / reset / amend / --force；不要读取或输出 obs.json 和任何密码；不要改游戏原文件；不要 sparse-checkout disable / set；art/cards 等其他会话的文件原样保留；不要打开结城理 mod 的图片；遇到没写到的情况停下来报告）。
另一个窗口在做特效素材（research/p5r/fx、art/p5/fx），不要碰它的目录。

【背景】用户觉得现在的 slash 太规范、像军体拳，小刀也不像原作，要先看原作的普通攻击是什么样。research/p5r/pose/ 里那 64 段只抽了 6 帧、也没挂刀，看不清。

【1. 找出普通攻击的动作】
- **首先渲 BB0001_051.GAP#main[10]（pose 库 #11）**：Claude 判断这就是普通攻击（1 秒：压低 → 扑出去 → 甩臂横扫 → 回到待机）。之前只套 rotation、只抽 6 帧，冲刺位移和刀都没了，所以看着像乱甩。这次要带根位移（D 方案）、挂刀、每 2 帧一张。
- 然后是 #4（main[3]）、#56（main[58]）作对照；以及 **BB0001_052.GAP 的全部动作**（之前 pose 库只渲了 051，052 一段都没渲过，可能是武器 / 连击动作）。
- 再查 Joker 的其他战斗动作文件（research/p5r/joker_battle_anim_list.csv、本机 MODEL/CHARACTER/0001/ 下 BB0001_*.GAP 以外的 GAP，比如武器 / 近战 / 连击相关的）：普通攻击在 P5R 里是冲到敌人面前挥刀（可能有连段、转身斩），找出所有像挥刀的段。

【2. 挂上原版小刀】
- 在 index.csv / MODEL/WEAPON 下找 Joker 的初始小刀模型（怪盗服默认的那把），导出 GLB（用 tools/p5r/fix_gfd_glb.py 同样的修复）和贴图 PNG。
- 渲染时把小刀挂到 Joker 右手的武器骨骼上（骨骼名里带 weapon / buki / R_hand / wp 之类的；找不到挂点就挂右手掌骨骼，写明用的哪个）。
- 单独渲一张小刀的 3/4 视角大图（1024×1024，透明底）：art/p5/ref/knife_original.png，再拼一张多角度图 knife_views.png。

【3. 逐帧渲染】
- 每段攻击动作用 C 方案（只套 rotation）+ 根节点 translation（D 方案，能看出冲刺位移的话），**每 2 帧渲一张**（30fps 基准），挂着小刀，正交相机 3/4 朝画面右方，**固定相机不重新取景**（这样看得到他往前冲多远），透明底。
- 每段拼一张联系表 research/p5r/attack/<段名>_sheet.png（一行 8 张，格子 256×384，格子上写帧号），再把最像「挥刀命中那一下」的 3~4 帧单独放大存 research/p5r/attack/<段名>_key_XX.png（1024×1536）。
- 再做一个 GIF 预览 research/p5r/attack/<段名>.gif（原速，320 宽），方便用户直接看动作。

【提交】只 add research/p5r/attack、art/p5/ref/knife_original.png、art/p5/ref/knife_views.png；"research: P5R Joker melee attack frames + original knife"。
报告：中文 research/p5r/attack/REPORT.md：找到了哪几段攻击、各自是什么动作（冲刺？横斩？转身斩？几刀？）、小刀模型来源路径和挂点骨骼、提交号。
