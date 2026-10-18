你在用户的 Windows 电脑上工作。任务：用 imagegen **照着原版 P3R 画面复刻**设置界面的倒挂立绘（结城理）。只做美术，不改代码，不编译，不启动游戏。

【为什么】
之前的立绘构图和原版对不上（头被画布裁掉、头发又厚又平），程序怎么套蒙版都不像。用户要求：直接按原图把人物生出来。

【1. 找到仓库并拉取】
1. 找到你上次成功推送的仓库 <repo>（remote 是 github.com/4yndm7wh95-spec/persona，里面有 mod\P3Makoto.json）。所有 git 命令都加 -c safe.directory=<repo>。
2. git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/ 原样保留、忽略。
3. git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次）。
4. git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn；不能快进时改用 git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn；有冲突就停下来报告。

【2. 参考（主参考 = 原版画面，照着画）】
- 原版落定画面：<repo>\art\p3menu_system\ref\menu_transition_frames_020-107\storyboards\storyboard_16.png（#80~#83，每张分镜 2×2 帧，单帧 1920×884，帧内画面在 x 244~1658）。
  **以 #82 为准**（storyboard_16.png 左下格），把单帧里 x 244~1658 的画面裁出来（1414×884）作为参考图。
- 画风辅助：<repo>\art\p3menu_system\head\system_head_open.png（用户认可的画风、白衬衫和飘带画法），只参考笔触配色，构图以原版为准。

【3. 要画的】
把原版 #82 里的**人物**照原样复刻成一张透明背景的立绘：
- 画布 1414×884（和裁出的原版帧完全一样大），人物的位置、大小、角度、轮廓和原版逐像素对齐（可以把原版半透明垫在下面描）。
- 只要人物：衬衫 / 领口 / 肩、脖子、脸、耳机和耳机线、黑色飘带（实心黑）、头发。
- **头发**：形状照原版——脸左侧那一大片头发的外轮廓、发梢的方向和长度、眼睛周围的头发，都和原版一致；颜色照原版：深钴蓝为主，加原版里那几块**亮天青色**的发束 / 阴影（位置照原版）。头发边缘干净平滑，不要锯齿、不要糊、不要任何直线或矩形的边。
- 两只眼睛：青色虹膜，照原版的位置和睁开程度。
- 皮肤 / 衬衫：纯白 #FFFFFF，阴影用浅青平涂；细深蓝线条。
- **不要**画：白色背景板、水、气泡、水花、文字（左边的竖排大字、右边的菜单字、金额框、水印"人正在看"等）、粉色碎片、任何 UI。人物以外全部真透明（alpha = 0）。
- 画面右边伸进水里的头发 / 袖子：照原版的形状画出来（游戏里会处理成水色）。
- 不要多余的手或手指。
- 闭眼版：再用编辑功能做一张只改眼睛的闭眼版（倒过来的脸，眼睑方向要对），眼睛以外像素和睁眼版完全一致。

【4. 交付（<repo>\art\p3menu_system\trace\）】
1. system_trace_open.png、system_trace_closed.png（1414×884 RGBA，真透明）。
2. ref_082.png：裁出来的原版 #82（1414×884）。
3. compare.png：三联 —— 左 = 原版 #82；中 = 新立绘叠在白底上；右 = 新立绘以 50% 叠在原版上（用来看是否对齐）。
4. README.md（中文）：提示词、生成 / 编辑次数、和原版的差别、已知问题。

【5. 自检】最多 4 轮，仍不满足就如实报告：
- 背景真透明，人物以外 alpha=0，没有画进水 / 文字 / 白板；
- compare.png 右图里人物轮廓（尤其头发外沿、眼睛、耳机）和原版基本重合；
- 头发边缘平滑，没有锯齿、模糊、直线或矩形；
- 黑色飘带实心；没有多余的手；闭眼版眼睛以外零变化。

【6. 提交与推送】
git -c safe.directory=<repo> add art/p3menu_system/trace
git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: settings portrait traced from the original P3R frame #82"
按第 1 步的方式 push（失败重试；被拒先 fetch 再 merge --no-edit；不要 rebase / --force）。用中文报告结果和提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏。不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换中性描述重试一次，仍被拒就停下来报告。
- 不要 sparse-checkout disable / set。不要和其他会话同时操作这个仓库。遇到没写到的情况，停下来报告。
