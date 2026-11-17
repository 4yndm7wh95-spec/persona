你在用户的 Windows 电脑上工作。任务：**只补撒旦耶尔第 10 版分层里的一个洞**。不改 mod 代码，不编译，不启动游戏，不用 Computer Use。
仓库规则同 docs/codex/codex_p5_satanael_layers_v10.md（分支 claude/epic-archimedes-m9an69，先 fetch 再 merge --ff-only）。

【Claude 审阅 layers_v10 的结论（2026-11-09）】
可以用：上臂往下转 -40° 那一格（check_moved_v10.png 第一行第二格）肩肘接得很干净、枪口正好指向右下方，这正是游戏里要的瞄准姿势。
你报告里的其他未通过项 Claude 在游戏代码里处理（翅膀只做 ±3° 以内的小幅鼓动，不会转到 ±10°；关节点、转枪轴心 Claude 自己在游戏里校准；
独立枪和握枪的枪只在接住那一下切换，有闪光盖住），**不用再改**。

【只需要修这一处】原图里长枪横穿过右上方的两片翼片（wings_back_far / wings_back_near 里被枪挡住的那一条）。枪的手臂往下转之后，
这里露出一条**枪形状的空洞**（看 check_moved_v10.png 第一行第二格：右上两片金色翼片之间那条斜着的空带）。
请把被枪挡住的翼片部分补画完整（翼膜、金色骷髅图案、边缘描边都接上），原生分辨率补画，蒙版外 0 改动，覆盖原文件；
重新出 check_moved_v10.png。只补这一处，最多 2 次。
【提交】只 add 改动的翼片层和 check 图；提交信息 "art: Satanael v10 layers - fill the wing area behind the gun"。
