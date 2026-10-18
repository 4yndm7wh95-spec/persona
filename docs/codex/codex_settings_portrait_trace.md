你在用户的 Windows 电脑上工作。任务：用 imagegen 的**编辑**功能，只重画设置界面倒挂立绘（结城理）的**头发**，改成原版 P3R 的样式和颜色；其余部分（脸、眼睛、耳机、衬衫、飘带、构图）全部沿用现有立绘、一个像素都不动。只做美术，不改代码，不编译，不启动游戏。

【背景】
用户确认：现有立绘除了头发都过关（art\p3menu_system\trace\approved_except_hair.png 是用户认可的效果截图）。问题只在头发：脸左边那片头发的外形、发梢、颜色和原版不一样。

【1. 找到仓库并拉取】
1. 找到你上次成功推送的仓库 <repo>（remote 是 github.com/4yndm7wh95-spec/persona，里面有 mod\P3Makoto.json）。所有 git 命令都加 -c safe.directory=<repo>。
2. git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/ 原样保留、忽略。
3. git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次）。
4. git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn；不能快进时改用 git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn；有冲突就停下来报告。

【2. 素材（都在 <repo>\art\p3menu_system\trace\，1920×1080）】
- base_portrait.png：**编辑底图**（现有立绘，透明背景）。
- ref_082_aligned.png：原版 P3R #82 画面，已经按两只眼睛的位置对齐到底图上（同样 1920×1080），头发的形状和颜色照它画。
- edit_region.png：允许修改的区域（白 = 可改，黑 = 不准动），覆盖头部的头发。
- guide.png：左 = 底图 + 红色可改区域；右 = 底图上叠半透明原版（看对齐）。
- approved_except_hair.png：用户认可的游戏内效果（除头发外都对）。

【3. 要改的（只改头发）】
在 edit_region 范围内，把头发重画成原版 ref_082_aligned.png 的样子：
- **形状**：脸左侧那一大片头发的外轮廓、发梢的方向和长短、眼睛周围 / 脸下面的头发，都照原版对齐的位置画（可以把 ref_082_aligned 半透明垫在下面描）。头是一个完整的头形，不要横向铺满、不要直线或矩形边。
- **颜色**：照原版——深钴蓝为主（约 #0B2BD0~#1430C8），原版里那几块**亮天青色**（约 #00C8F5）的发束 / 阴影照原版的位置和形状画出来。平涂，边缘干净平滑，不要锯齿、不要模糊、不要渐变光晕。
- **不准动**：脸、两只眼睛（青色虹膜、睫毛、眼白）、耳朵、耳机和耳机线、脖子、衬衫、黑色飘带；可改区域内这些部分也保持原样。
- 原版画面里的水、水花、气泡、粉色碎片、文字、白板都**不要**画进来；头发以外保持透明。
- 改完用底图把 edit_region 以外的像素还原（区域外变化像素数 = 0）。
- 闭眼版：用 mod\pck_stage\p3makoto\ui\system_portrait_closed.png 做同样的头发修改（或者把新头发贴到闭眼底图上），眼睛以外和睁眼版一致。

【4. 交付（<repo>\art\p3menu_system\trace\）】
1. system_trace_open.png、system_trace_closed.png（1920×1080 RGBA，真透明）。
2. compare.png：三联 —— 左 = 原版 ref_082_aligned；中 = 新立绘叠在白底上；右 = 新立绘以 50% 叠在原版上。
3. README.md（中文）：提示词、编辑次数、是否做了区域外还原（变化像素数）、和原版的差别、已知问题。

【5. 自检】最多 4 轮，仍不满足就如实报告：
- edit_region 外像素和底图完全一致；脸、眼睛、耳机、飘带没变；
- compare.png 右图里头发外轮廓和原版基本重合，亮天青阴影的位置对得上；
- 头发边缘平滑，没有锯齿、模糊、直线或矩形；背景真透明。

【6. 提交与推送】
git -c safe.directory=<repo> add art/p3menu_system/trace
git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: settings portrait hair redrawn to match the original P3R frame"
按第 1 步的方式 push（失败重试；被拒先 fetch 再 merge --no-edit；不要 rebase / --force）。用中文报告结果和提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏。不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换中性描述重试一次，仍被拒就停下来报告。
- 不要 sparse-checkout disable / set。不要和其他会话同时操作这个仓库。遇到没写到的情况，停下来报告。
