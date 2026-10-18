你在用户的 Windows 电脑上工作。任务：用 imagegen 重画 P3Makoto 设置界面的倒挂立绘，让**头部完整、轮廓和原版 P3R 一致**。只做美术，不改代码，不编译，不启动游戏。

【为什么要重画】
现在的立绘（mod\pck_stage\p3makoto\ui\system_portrait.png）头部被画布下边裁掉：头发一直铺到画布最底下、横跨半个画面，
游戏里怎么摆都会在左下角出现一整片厚头发，不是"一个头"。原版 P3R 是一个完整的、倒挂的头：头发是一个头的形状，左边和下边都看得到头发的外轮廓。

【1. 找到仓库并拉取】
1. 找到你上次成功推送的仓库 <repo>（remote 是 github.com/4yndm7wh95-spec/persona，里面有 mod\P3Makoto.json）。所有 git 命令都加 -c safe.directory=<repo>。
2. git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/ 原样保留、忽略。
3. git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次）。
4. git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn；不能快进时改用 git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn；有冲突就停下来报告。

【2. 参考】
- 原版落定画面：<repo>\art\p3menu_system\ref\menu_transition_frames_020-107\storyboards\ 的 storyboard_16.png（#80~#83）、storyboard_18.png（#88~#91）。看左半边的人物。
- 现有立绘（画风、配色、衣服、飘带、耳机的依据）：<repo>\mod\pck_stage\p3makoto\ui\system_portrait.png（已经去掉了多余的手）。

【3. 要画的（1920×1080 RGBA，透明背景）】
- 构图照原版落定画面（原版是 16:10，按高度对齐到 1080，左对齐）：倒挂的结城理，衬衫、肩膀从画面上方伸下来，脸倒着在画面左下，
  **整个头都在画面里**：头顶朝下、离画面底边还有一点距离；头发是一个头的形状，左侧外轮廓在 x≈110~200（原版眼睛左边那一簇头发），
  头发不要铺满画面底部、不要横向延伸成一大片。
- 眼睛、耳机、衬衫领口的位置尽量和原版一致；右侧伸进水里的头发 / 袖子照原版。
- 画风、配色和现有立绘一致：纯白 #FFFFFF 衬衫 / 皮肤，钴蓝 / 天青平涂头发，细深蓝线，黑色实心飘带；不要写实光影、发光、模糊。
- 不要画白底、水、文字、UI；背景真透明。不要多余的手或手指。
- 闭眼版：再用编辑功能做一张只改眼睛的闭眼版（眼睛以外像素和睁眼版完全一致）。

【4. 交付（<repo>\art\p3menu_system\head\）】
system_head_open.png、system_head_closed.png（1920×1080 RGBA）；
compare.png：左右两半 —— 左 = 新立绘叠在白底上（只看左半屏），右 = 原版 #82 左半屏，同样大小；
README.md（中文）：提示词、生成 / 编辑次数、和原版的差别、已知问题。

【5. 自检】最多 4 轮：背景真透明；头是完整的头形（compare.png 里头发外轮廓和原版大致重合）；眼睛以外闭眼版零变化；没有多余的手。

【6. 提交与推送】
git -c safe.directory=<repo> add art/p3menu_system/head
git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: settings portrait redrawn with a complete head (matches the original layout)"
按第 1 步的方式 push（失败重试；被拒先 fetch 再 merge --no-edit；不要 rebase / --force）。用中文报告结果和提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏。不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换中性描述重试一次，仍被拒就停下来报告。
- 不要 sparse-checkout disable / set。不要和其他会话同时操作这个仓库。遇到没写到的情况，停下来报告。
