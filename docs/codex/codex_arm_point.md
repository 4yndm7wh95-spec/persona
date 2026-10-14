你在用户的 Windows 电脑上工作。任务：用 imagegen 给结城理的骨骼动画画两张新的手臂零件（只做美术，不改代码）。

【背景】
P3Makoto 是杀戮尖塔 2 的 mod，结城理是分件骨骼动画，右臂按姿势换不同的零件图（已有：自然下垂、举召唤器、持剑等）。Claude 在云端写代码，你在本地做美术并推送到 GitHub 仓库 4yndm7wh95-spec/persona 的分支 claude/amazing-darwin-b00arn。
P3 结局「伟大封印」里，结城理的动作是**单手指天**（右臂高举、食指指向天空）。现在没有这个姿势的手臂图，需要你画。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（C:\Users\kkk\ 下、桌面、Documents，或 Get-ChildItem -Recurse -Filter P3Makoto.json 后用 git remote -v 确认），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
先 git status：有未提交改动就停下报告。然后 checkout claude/amazing-darwin-b00arn，
git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次），git merge --ff-only origin/claude/amazing-darwin-b00arn（不能快进就 git merge --no-edit origin/claude/amazing-darwin-b00arn，不要 rebase）。

【参考】
- 人物原画：<repo>\art\master\makoto_rig_master.png（1024×1536，零件的位置和比例以它为准）。
- 已有手臂零件和当时的提示词：<repo>\art\parts_src\arm_hanging.png、arm_raised_evoker.png，提示词见 <repo>\art\parts_src\PROMPTS_ARMS.md（新零件的画布、位置、绿幕要求和它们完全一样）。

【要画的图】输出到 <repo>\art\parts_src\：
1. arm_point_mid.png —— 过渡帧：右臂从身侧抬起到斜上方约 45°（肘部微弯），手掌张开、手指并拢、掌心朝前，正在往上举。
2. arm_point_up.png —— 指天：右臂几乎竖直向上举过头顶（略向画面外侧倾斜 5°~10°，肘部伸直或极轻微弯曲），手在头顶上方，**食指伸直指向天空**，其余手指收拢，拇指扣在中指上。
共同要求（照 PROMPTS_ARMS.md 的写法）：只画一条完整的右臂（从西装袖子在肩部的顶端到指尖），不画头、身体或其他部位；黑色西装面料、上臂红色袖章（不要文字）、手的画法和比例和参考一致；画布正好 1024×1536；纯 #00FF00 绿幕背景，没有阴影 / 光晕 / 渐变 / 文字 / 水印；**肩部袖口顶端在像素 (485,295)**，和原画同比例。
arm_point_up 的手指尖大约在 (470, 0~40) 一带（如果放不下，允许指尖贴近画布顶边，但不能被切掉）；arm_point_mid 的手大约在 (640, 130)。
3. preview_point.png：把两张手臂分别抠掉绿幕后，叠到 makoto_rig_master.png 上（肩部对齐），并排放，检查比例和衔接。
4. REPORT_POINT.md（中文）：最终提示词、重画了几次、肩部 / 指尖的实际坐标、和要求的偏差。

【自检】肩部偏差超过 25 像素、比例明显不对、食指没有指天、出现第二只手或身体时，重画（每张最多 4 轮）；仍不满足就在报告里如实写。

【提交与推送】
- 只 git add art\parts_src\ 下的文件。提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: Makoto arm parts for the Great Seal pose (point to sky)"
- push：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试）。
  被拒绝是因为别的会话先推送了：先 fetch，再 git merge --no-edit origin/claude/amazing-darwin-b00arn（合并提交，不要 rebase），然后再 push。
- 完成后用中文报告做了什么、结果在哪、提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要改写 git 历史（rebase / reset / amend）；不要用 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只读）；导出 / 解包的中间文件放到 C:\Users\kkk\sts2_tools\ 下。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 遇到上面没写到的情况，停下来报告，不要自作主张。
