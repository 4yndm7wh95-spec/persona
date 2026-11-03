你在用户的 Windows 电脑上工作。任务：画毒性爆发用的**「巨手捏碎眼神特写」**的手（4 帧）。不改 mod 代码，不编译，不启动游戏。

**先读 docs/codex/README.md**（生图一律真透明底）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/cool-pascal-mil5i8**。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-crush-hand，报告分支名。

【演出（用户 2026-11-03 的创意）】
眼神特写横幅插进来停住 → 背景深处伸出一只**巨大的手**，朝镜头（也就是朝画面里的横幅）伸过来、五指张开 → 握住横幅 → 攥紧把横幅捏碎（高潮，同一帧敌人身上咒火炸开、跳伤害）。
手占画面很大（游戏里显示约 1100～1300 像素高），是 Persona 的咒怨之手。

【手的样子】
- **姿势照用户自己拍的参考**（只是姿势参考，不要照片质感）：
  - art/p5/fx/v4/crush_hand/ref_user_open.jpg：**掌心正对镜头**、五指张开、手指朝上、强烈的透视（手离镜头近、手腕和前臂往画面下方深处缩小）。
  - art/p5/fx/v4/crush_hand/ref_user_fist.jpg：**正面的拳头**，四根手指的第二指节朝镜头，大拇指横压在食指和中指前面。
- **画风和材质照仓库里已有的咒怨之手**：art/p5/fx/v4/crush_hand/ref_style_curse_hand.png（= art/p5/fx/gen_codex/curse_hands_v2/hand_open.png）——
  黑色的手、暗红色的高光和筋络纹理、粗黑描边、指尖是尖利的爪；P5 的红黑配色和赛璐璐风格。手腕往下渐隐成黑色的咒烟（前臂不用画完整，画到手腕下方一截、然后化成烟）。
- 不要背景、不要光晕；真透明底，轮廓外 alpha = 0。

【4 帧】（art/p5/fx/v4/crush_hand/，每张 1536×1792，**同一只手、同一个位置和大小**——手腕底部在画布里的位置每帧一致，后面的帧以前一帧为底图编辑）
1. **crush_open.png**：掌心对着镜头、五指张开（照 ref_user_open），爪子张开。
2. **crush_curl.png**：以 crush_open 为底图编辑，手指往里弯到一半（像要抓住面前的东西），掌心仍然大半可见。
3. **crush_grip.png**：以 crush_curl 为底图编辑，手指几乎握拢、指尖扣进掌心前方（握住横幅的那一下），还没完全攥成拳。
4. **crush_fist.png**：以 crush_grip 为底图编辑，攥紧的拳头（照 ref_user_fist：指节朝前、大拇指横压在前面），指节绷紧、筋络更明显。
四张的手腕和整体大小一致；手指弯曲是一张比一张多，不要跳。

【检查】出 check_crush_hand.png：4 帧按顺序并排（深灰底 + 棋盘格底各一行），以及四帧叠在一起的半透明叠图（看手腕位置是否一致）。每张最多重画 2 次。

【提交】只 add art/p5/fx/v4/crush_hand/ 下的新文件（四帧、各自 _raw.png 和 _generation.json、check 图、README.md）；
提交信息 "art: crush hand frames for Outbreak (open -> curl -> grip -> fist)"。
【报告】中文：四张的位置、重画次数、手腕对齐情况、提交号或分支名。
