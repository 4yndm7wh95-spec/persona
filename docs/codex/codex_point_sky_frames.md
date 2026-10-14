你在用户的 Windows 电脑上工作。任务：用 imagegen 做结城理「伟大封印 · 单手指天」的**全身逐帧动画**（8 张全身帧，只做美术，不改代码）。

【背景】
P3Makoto 是杀戮尖塔 2 的 mod（结城理换皮）。结局「伟大封印」演出里，结城理要照 P3R 原版做一遍"抬手 → 指天"的动作。这是过场演出，不是战斗动画，所以不拆零件：直接做 8 张全身帧，由 Claude 按原版的节奏（约 1.9 秒，从身侧慢慢抬起，不赶）溶解切换。
上一轮只画手臂零件，手总是画成张开的手掌（像要打人 / 挥手），不合格。这一轮对手势有严格限定。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（C:\Users\kkk\ 下、桌面、Documents；可能有多个副本，用你上次成功推送的那个），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
先 git status：如果只有 art\parts_src\ 下的 arm_point_mid.png、arm_point_up.png、preview_point.png 这 3 个未跟踪文件（另一个会话留下的，远端提交 44519ee 已有同名文件），把它们移动到新建的 C:\Users\kkk\sts2_tools\untracked_backup_<时间戳>\（不是删除）后继续；有其他未提交改动就停下报告。然后 checkout claude/amazing-darwin-b00arn，
git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次），git merge --ff-only origin/claude/amazing-darwin-b00arn（不能快进就 git merge --no-edit origin/claude/amazing-darwin-b00arn，不要 rebase）。
如果仓库是稀疏检出、需要的参考文件不在磁盘上：允许执行 git -c safe.directory=<repo> sparse-checkout add <需要的目录>；路径过长报错时允许单次命令加 -c core.longpaths=true。不要 sparse-checkout disable / set。

【原版参考（Claude 从用户给的 P3R 原版视频截的，先仔细看）】
- <repo>\art\parts_src\ref\point_sky_p3r_seq.png：原版动作 6 帧（从左到右）：右手握拳收到下巴前 → 前臂横过额头上方 → 手臂向斜上方扫 → 整条手臂几乎笔直朝天。
- <repo>\art\parts_src\ref\point_sky_p3r_detail.png：同一段动作每 1/6 秒一帧（左上角是时刻，共 20 帧）——**8 张帧的姿势以这张为准**：0.83s 手移到身前腰带处 → 1.17 前臂横在腰前 → 1.5 拳到胸口 → 1.83 拳到下巴 → 2.17 前臂横过额头上方 → 2.33 拳在头顶左上 → 2.5 斜上伸展 → 2.83 起直指天空。
- <repo>\art\parts_src\ref\point_sky_p3r_mid.png：前臂横在额头上方的那一帧。
- <repo>\art\parts_src\ref\point_sky_p3r_final.png：最终姿势。
要点：
- 举起的是结城理的**右臂** = 画面左侧那条手臂（待机图里垂在身侧、靠近腰间枪套的那条）。**红色袖章在另一条手臂（左臂，画面右侧）上，举起的手臂没有袖章。**
- 另一只手（画面右侧）始终插在裤袋里，不动。
- 最终手势：**握拳、只有食指伸直指向天空**（"数字 1" 的手势）；从镜头看到手背 / 小指一侧，看不到掌心；手在画面上是窄长的竖条，不是张开的扇形。
- 表情始终平静，头和脸不变。

【底图】
<repo>\art\v12\transparent\idle_a.png（1024×1536，RGBA 透明背景，结城理全身待机图）。8 张帧都用 imagegen 的**编辑**功能从这张图改出来：只改画面左侧那条手臂（和它挡住 / 露出的外套、衬衫、枪套），头、脸、头发、躯干、腿、鞋、另一条手臂、画布位置全部不变。

【要做的 8 张】输出到 <repo>\art\v15\point\，都是 1024×1536、真透明背景（轮廓外 alpha=0），人物和 idle_a.png 逐像素对齐（脚、腿、头不能移动）。
原版（point_sky_p3r_seq.png）里手臂是一点点抬上去的，8 帧要把这个过程拆细，相邻两帧之间手只移动一小段（括号里是该帧在原版里的时刻，播放时按这个节奏）：
1. point_1_hip.png（0.00s）—— 右手从身侧开始抬起：手离开大腿外侧，移到身前腰带扣的高度，肘部微弯靠近身体；手**轻轻握拳**。
2. point_2_waist.png（0.33s）—— 拳头抬到腰 / 肚子前方，前臂接近水平地横在身前，肘部向画面左侧外张。
3. point_3_chest.png（0.67s）—— 拳头继续升到胸口 / 锁骨前方，前臂水平横在胸前，肘部外张、与肩同高略低。
4. point_4_chin.png（1.00s）—— 拳头到下巴前方，前臂横在下半张脸前（不能挡住眼睛），肘部抬到接近肩高，拳背朝向观众。
5. point_5_brow.png（1.33s）—— 照 point_sky_p3r_mid.png：前臂抬过头，水平横在额头上方，肘部在画面左侧与头顶同高，拳头在额头正上方（不能挡住脸）。
6. point_6_over.png（1.50s）—— 前臂竖起来，拳头升到头顶左上方，肘部弯约 90°、在肩膀上方外侧。
7. point_7_diag.png（1.67s）—— 手臂向画面左上方斜着伸展（约 60°），肘部只剩微弯；食指开始伸直，其余手指握拳。
8. point_8_sky.png（1.90s 起保持）—— 照 point_sky_p3r_final.png：整条右臂几乎笔直朝天（向画面左侧外倾 5°~15°），手在最上方，**握拳、只有食指伸直指向正上方**，拇指压在卷起的中指上，手背朝向观众，看不到掌心，手腕直。
画面里不能出现召唤器（枪）以外的新道具；枪套里的枪保持原样。

【怎么画（重要）】
- 提示词示例（按每帧改姿势描述）：
  "Edit this exact character illustration. Change ONLY his right arm (the arm on the left side of the image, without the red armband): <姿势描述>. The hand is a closed fist with ONLY the index finger extended pointing straight up (the 'number one' counting gesture), middle ring and little fingers fully curled into the palm, thumb folded over the curled middle finger, back of the hand facing the viewer, palm not visible. Keep the head, face, hair, torso, legs, shoes, the other arm in the pocket, the red armband on the other arm, colors, line weight, canvas size and position exactly the same. Transparent background."
  否定描述："not an open hand, not a raised palm, not waving, not a high-five, no spread fingers, no second raised arm"。不要用 cut / severed / detached 之类的词。
- 编辑结果改动了手臂以外的区域：用 idle_a.png 把手臂区域以外的像素还原（蒙版覆盖：新手臂 + 原手臂在待机图里的位置 + 周围约 20px 羽化），原手臂移走后露出的外套 / 腰部要补画自然。
- 手势画不对：先保证整只手臂姿势正确，再用编辑 / 局部重绘**只重画手**（蒙版只盖住手）。
- 每次出图后把手部区域放大 4 倍单独检查；再把 8 帧和 idle_a.png 叠成半透明，检查脚、腿、头没有移动。

【自检（每张逐条确认，任何一条不满足就重做）】
1. 举起的是画面左侧、**没有袖章**的手臂；袖章仍在另一条手臂上；另一只手仍插在口袋。
2. 手势：1~6 帧握拳（伸直的手指 0 根）；7 帧食指正在伸直；8 帧伸直的手指正好 1 根且是食指、朝正上方。任何一帧都看不到张开的手掌。
3. 手臂以外的区域和 idle_a.png 一致（手臂附近 + 原手臂位置以外逐像素差为 0）。
4. 8 帧按顺序播放时手臂的路线连贯：身侧 → 腰 → 胸口 → 下巴 → 额头上方 → 头顶左上 → 斜上 → 朝天，相邻两帧没有大跳，和原版参考一致。
5. 轮廓外 alpha 全为 0；画布 1024×1536。
每张最多 6 轮；仍不满足就如实报告，并把最接近的一张也提交（文件名加 _wip）。

【输出】
9. preview_point.png：idle_a + 8 帧横向排成一排（浅灰棋盘格底），下面一排是每帧手部 4 倍放大；最右边放 point_sky_p3r_seq.png 对照。
10. point_anim.gif：idle_a → 1 → … → 8，按上面括号里的时刻播放（idle_a 停 0.5 秒，最后一帧停 1 秒），循环。
11. README.md（中文）：每帧的最终提示词、编辑 / 局部重绘次数、是否做了还原、自检 5 条逐条结果、每帧手（拳头 / 指尖）的大致像素坐标、已知问题。

【提交与推送】
- 只 git add art\v15\point\ 下的文件。提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: Great Seal point-to-sky full-body frames (8 frames, edited from idle)"
- push：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试）。被拒绝是因为别的会话先推送了：先 fetch，再 git merge --no-edit origin/claude/amazing-darwin-b00arn（不要 rebase），然后再 push。
- 完成后用中文报告做了什么、结果在哪、提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要改写 git 历史（rebase / reset / amend）；不要用 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只读）。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换一种中性的描述重试一次，仍被拒就停下报告。
- 遇到上面没写到的情况，停下来报告，不要自作主张。
