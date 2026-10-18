你在用户的 Windows 电脑上工作。任务：用 imagegen 的**编辑**功能，给 P3Makoto mod 设置界面的倒挂立绘画 3 张"入水时低头 → 抬头"的姿势帧。只做美术，不改代码，不编译，不启动游戏。

【背景】
P3R 主菜单进入时，结城理倒挂着沉入水中：一开始低着头、闭着眼，下沉过程中头慢慢抬起（下巴往画面上方转），落定时睁眼。
游戏里已经用"整张图旋转 + 平移"做了下沉，但头部动作只靠一张图做不出来，需要几张姿势不同的关键帧，游戏里按原版"两帧一换"的节奏切换。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功推送的那个），记作 <repo>：remote 指向 github.com/4yndm7wh95-spec/persona；仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/ 原样保留、忽略。
4. git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次）。
5. git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn；不能快进时改用 git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn；有冲突就停下来报告。
6. 稀疏检出缺目录时，允许 git -c safe.directory=<repo> sparse-checkout add art/p3menu_system mod/pck_stage/p3makoto/ui（路径过长时这一条可以加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 素材】
- 编辑的底图：<repo>\mod\pck_stage\p3makoto\ui\system_portrait_closed.png（1920×1080 RGBA，透明背景，已经去掉了多余的手、闭着眼）。**不要用 art\p3menu_system\system_base.png**（那张还有多余的手）。
- 原版参考（看头部姿势）：<repo>\art\p3menu_system\ref\menu_transition_frames_020-107\storyboards\ 下的分镜，frame_index.csv 是帧号 → 分镜页对照。重点看 #42、#46、#50、#56（每张分镜 2×2 帧）。

【3. 要画的 3 张（在底图上用 imagegen 编辑，只改头部 / 脖子 / 刘海）】
重点是**动作**，不是配色：游戏里头发会被实时画成水的颜色（头发颜色、发梢形状都不重要，保持和底图同一套钴蓝 / 天青即可）。
- system_dive_a.png（对应原版 #42）：**明显低头**——整颗头绕脖子往画面左下方转约 25°，下巴紧贴衣领（倒挂着看，脸更朝向画面下方），闭眼。
  这是最重要的一张，和底图放在一起要一眼看出"头低下去了"；之前生成的两张变化太小，不合格。
- system_dive_b.png（对应原版 #46）：抬起一半，约多转 12°，闭眼。
- system_dive_c.png（对应原版 #50）：几乎回到底图姿势（约多转 3°），眼睛半睁（露出一点青色虹膜）。
共同要求：画布、人物大小、身体（衬衫、肩、胳膊、黑色飘带、耳机线）位置完全不动；脖子和衣领自然衔接；
画风和底图一致（纯白 #FFFFFF、浅青 / 天青 / 钴蓝平涂、细深蓝线、黑色飘带；不要写实光影、发光、模糊）；背景真透明；
不能有多余的手或手指；头顶朝画面下方；**黑色飘带必须保持实心黑色**（之前有一张飘带变成了空心细线）。
编辑改动了头部以外的地方时：用底图把以 (500, 880) 为中心、半径 300 的圆（羽化约 12 像素）以外的像素还原，圆外变化像素数要是 0。

【4. 交付（输出目录 <repo>\art\p3menu_system\dive\）】
1. system_dive_a.png、system_dive_b.png、system_dive_c.png（1920×1080 RGBA）。
2. preview.png：一行 4 张（a / b / c / 底图），每张叠在深蓝底 (20,70,200) 上，缩到 480×270；第二行是对应的原版帧 #42 / #46 / #50 / #56（从分镜里裁出来）。
3. README.md（中文）：提示词、每张的编辑次数、是否做了还原、和原版的差别、已知问题。

【5. 自检】不满足就重做，最多 4 轮；仍不满足就如实报告：
- 背景是真透明；圆外像素和底图完全一致（程序统计，变化像素数 = 0）；
- a → b → c → 底图 连着看，头是逐渐抬起来的，不跳、不变形；
- 没有多余的手或手指，脖子和衣领连贯。

【6. 提交与推送】
1. 只 add 这个目录：git -c safe.directory=<repo> add art/p3menu_system/dive
2. git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: settings portrait dive pose frames (head lowered -> raised)"
3. git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
   失败等 5 / 10 / 20 / 40 秒重试，最多 4 次；被拒绝时先 fetch 再 merge --no-edit（同上）后重新 push。不要 rebase，也不要 --force。
4. 用中文报告：做了什么、结果在哪、提交号、遇到的问题。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换一种中性的描述重试一次，仍被拒就停下来报告。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
