你在用户的 Windows 电脑上工作。任务：用 imagegen 重新画结城理「单手指天」的两张手臂零件（只做美术，不改代码）。

【背景】
P3Makoto 是杀戮尖塔 2 的 mod（结城理换皮），结城理是分件骨骼动画，右臂按姿势换不同的零件图。P3 结局「伟大封印」的动作是右臂高举、食指指向天空，需要这个姿势的手臂图。
上一次尝试失败（见 <repo>\art\parts_src\REPORT_POINT.md）：要求像素级定位太难，imagegen 做不到；有一轮因为提示词里写了 "severed"（切断）被审核拦截。
这次的改变：**不再要求精确坐标**——只要画好一条干净的手臂，位置和大小由 Claude 在代码里对齐；背景用真透明。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（C:\Users\kkk\ 下、桌面、Documents；可能有多个副本，用你上次成功推送的那个），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
先 git status：有未提交改动就停下报告。然后 checkout claude/amazing-darwin-b00arn，
git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次），git merge --ff-only origin/claude/amazing-darwin-b00arn（不能快进就 git merge --no-edit origin/claude/amazing-darwin-b00arn，不要 rebase）。
如果仓库是稀疏检出、需要的参考文件不在磁盘上：允许执行 git -c safe.directory=<repo> sparse-checkout add <需要的目录>（只是把已有文件检出到本地）；路径过长报错时允许单次命令加 -c core.longpaths=true。不要 sparse-checkout disable / set。

【参考】
- 画风 / 衣服 / 手：<repo>\art\master\makoto_rig_master.png（结城理全身原画：黑色学生制服外套、白衬衫袖口、上臂红色袖章）。
- 已有的手臂零件（看风格和粗细比例）：<repo>\art\parts_src\arm_hanging.png。

【要画的图】输出到 <repo>\art\parts_src\：
1. arm_point_up.png —— 1024×1536 画布，**真透明背景（RGBA，轮廓外 alpha=0）**。只画一条右臂：上臂几乎竖直向上举（略向外倾 5°~10°），肘部伸直，手在最上方，**食指伸直指向天空**，其余手指收拢，拇指扣在中指上。手臂从肩部的衣服接缝处开始（袖口在肩部是整齐的布料边缘，像纸偶 / 皮影零件那样，用于拼接到身体上），到指尖结束。
2. arm_point_mid.png —— 同样要求的过渡帧：右臂向斜上方抬起约 45°，肘部微弯，手掌张开、手指并拢、掌心朝前。
共同要求：和参考同一个人、同样的画风和线条粗细；黑色外套袖子、上臂红色袖章（不要文字）、白衬衫袖口、手的画法一致；只有一条手臂，没有头、身体、第二只手或任何道具；没有阴影、光晕、渐变底色、文字、水印；手臂整体大小和参考原画里的手臂相当（从肩部接缝到指尖约 400~480 像素），放在画布中间即可。
提示词里用中性的描述（例如 "a single arm sprite for a 2D paper-doll puppet, sleeve ends neatly at the shoulder seam"），不要用 cut / severed / detached 之类的词。
3. preview_point.png：两张手臂并排放在浅灰棋盘格上，旁边放 makoto_rig_master.png 缩略图对照比例。
4. REPORT_POINT2.md（中文）：最终提示词、重画了几次、每张的轮廓包围框（像素）、肩部接缝中心点和指尖的大致坐标、已知问题。

【自检】背景必须是真透明（检查轮廓外的 alpha）；食指必须指天；不能有第二只手或身体。不满足就重画（每张最多 4 轮），仍不满足就如实报告。

【提交与推送】
- 只 git add art\parts_src\ 下的文件。提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: Makoto point-to-sky arm parts (transparent, retry)"
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
