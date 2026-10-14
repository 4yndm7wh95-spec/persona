你在用户的 Windows 电脑上工作。任务：从本机 P3R（女神异闻录3 Reload）的游戏资源里**只读地**找出几样原版素材，导出成 PNG / OGG 放进仓库（不改代码）。

【背景】
P3Makoto 是杀戮尖塔 2 的 mod（结城理换皮）。之前你已经从本机 P3R 导出过一批 2D 贴图（<repo>\art\p3r_tex\）和音效（<repo>\art\sfx_p3r_*\，音效目录清单 <repo>\research\p3r_se\catalog.csv，本机解包目录 C:\Users\kkk\sts2_tools\p3r_rip\）。沿用当时的工具和方法（FModel / UModel / CRI 音频解包等，以你当时用的为准）。
Claude 在云端写代码，你在本地导出素材并推送到 GitHub 仓库 4yndm7wh95-spec/persona 的分支 claude/amazing-darwin-b00arn。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（C:\Users\kkk\ 下、桌面、Documents，或 Get-ChildItem -Recurse -Filter P3Makoto.json 后用 git remote -v 确认），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
先 git status：有未提交改动就停下报告。然后 checkout claude/amazing-darwin-b00arn，
git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次），git merge --ff-only origin/claude/amazing-darwin-b00arn（不能快进就 git merge --no-edit origin/claude/amazing-darwin-b00arn，不要 rebase）。

【要找的东西】
A. 贴图（导出成 PNG，保留透明通道），放到 <repo>\art\p3r_tex\ 下对应的子目录：
  1. shuffle\：Shuffle Time 开场的「SHUFFLE TIME」标题字 / logo（贴图名里可能有 ShuffleTime、Shuffle、Title、Logo）。把名字里带 Shuffle 的贴图全部导出一份。
  2. command\：战斗指令菜单里结城理的**蓝色平涂剪影 / 立绘**（按 PERSONA / 技能时左边那个大剪影），以及菜单背景的斜切色带、超大「PERSONA」字样（名字里可能有 Command、Cmd、Skill、Persona、Pc01、Btl_UI）。
  3. allout\：总攻击相关的其他 2D 贴图（名字里带 Allout / AllOut 的，之前已导出 T_Btl_AlloutFinish* 的就不用重复）。
  4. skill\：技能列表 UI 的条目底板、选中高亮、属性图标（名字里可能有 Skill、List、Icon）。
  每个子目录生成一张 contact_sheet.png（缩略图 + 文件名 + 原始尺寸），方便 Claude 浏览。
B. 音效（OGG，48kHz 立体声原样，不剪），放到 <repo>\art\sfx_p3r_c4\：
  1. Snd_FX_Btl_Allout_Zoom（catalog.csv 里有，3.1s）
  2. Snd_FX_Btl_Allout_Extermination（完整 4.5s；之前只给过截短版）
  3. Snd_FX_Btl_Allout_Kime01（完整 2.9s）
  4. 系统库里名字带 ShuffleTime 的全部音效（之前没导出过的）
  5. 名字里带 Command、Cursor、Decide、Skill 的战斗菜单音效（选技能 / 确认 / 光标移动）
  附 LIST.csv：文件名, 来源库名, 序号, cue 名, 时长秒（格式和 <repo>\art\sfx_p3r_c3\LIST.csv 一样）。
C. 如果某样找不到，在 README.md 里写清楚找过哪些地方、用了什么关键词，不要拿别的东西冒充。

【输出】<repo>\art\p3r_tex\<子目录>\*.png + contact_sheet.png，<repo>\art\sfx_p3r_c4\*.ogg + LIST.csv，<repo>\art\p3r_tex\README_v14.md（中文：每样找到 / 没找到、对应的原始资源路径）。

【提交与推送】
- 只 git add art\p3r_tex\ 和 art\sfx_p3r_c4\ 下的文件。提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: P3R originals for Shuffle Time title, command menu, All-Out SFX"
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
