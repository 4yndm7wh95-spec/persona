你在用户的 Windows 电脑上工作。任务：从本机 P3R（女神异闻录3 Reload）的游戏资源里**只读地**把「营地菜单（按暂停 / 菜单键打开的那个主菜单）」整套 UI 的原版素材和动画数据导出来，放进仓库。不改代码，不生图，不启动游戏。

【背景】
- P3Makoto 是杀戮尖塔 2 的 mod（储君换成结城理）。mod 里的设置界面想做成 P3R 营地菜单的样子：结城理倒着沉入水中 → 菜单出现（SKILL / ITEM / EQUIP / PERSONA / STATUS / QUEST / COMMU / CALENDAR / SYSTEM …）→ 每一项子菜单都有**自己的一张结城理立绘 + 一套 UI 入场 / 切换动画**。
- 之前 Claude 是看录屏逐帧去仿的，反复对不上。这次要直接拿原版的：贴图原样用，**动画关键帧（时间、位置、旋转、缩放、透明度、缓动曲线）照抄**。所以除了贴图，最重要的是 UMG 控件蓝图里的 **WidgetAnimation 数据**和相关材质参数。
- 之前你导出过 P3R 素材（见 <repo>\art\p3r_tex\README_v14.md）：pak 在 G:\Gamebox\ZS15858\GAME15858\GAME15858\P3R\Content\Paks\，用的是 CUE4Parse / CUE4Parse-Conversion 和本机已有的 FModel 解密配置。**沿用当时的工具、AES 密钥配置和方法**（README_v14 里写的为准）。中间文件放 C:\Users\kkk\sts2_tools\p3r_camp_<日期>\。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功推送的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. 执行 git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/ 原样保留、忽略它们继续（不要移动、删除或提交）。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并：先执行
   git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn
   不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   有冲突就停下来报告。
6. 稀疏检出缺目录时，允许 git -c safe.directory=<repo> sparse-checkout add research/p3r_camp art/p3r_tex（单次命令可加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 先摸底（不导出，只列清单）】
1. 在资源索引里按路径和名字搜，范围至少包括：
   - 路径：P3R/Content/Xrd777/UI/ 下名字含 Camp 的目录（例如 UI/Camp/…）、Blueprints/UI/、Blueprints/Camp/、UI/Common/，以及其他名字含 Camp 的目录；
   - 关键词（大小写不敏感）：Camp、CampMenu、Cmp、Pause、MainMenu、System、Config、Option、Setting、Skill、Item、Equip、Persona、Status、Quest、Commu、Social、Calendar、Hero、Pc01、Water、Dive、Bubble、Splash。
2. 生成 <repo>\research\p3r_camp\index.csv：每个命中资源一行 —— pak 内路径、资源类型（Texture2D / WidgetBlueprint / WidgetAnimation / Material / MaterialInstanceConstant / DataTable / LevelSequence / NiagaraSystem / 多边形 PLG_… / 其他）、贴图尺寸（贴图才有）、文件大小。
3. 从清单里认出这几类，在 README 里按类列出（这是后面导出的范围）：
   a. **营地菜单根界面**：打开菜单时的入场（结城理入水、水花、气泡、白色区域、竖排大字、菜单列表文字的出现）；
   b. **每个子菜单**（SKILL、ITEM、EQUIP、PERSONA、STATUS、QUEST、COMMU / SOCIAL LINK、CALENDAR、SYSTEM / CONFIG，以游戏里实际有的为准）：各自的结城理立绘贴图、背景、入场 / 退出 / 切换动画；
   c. 菜单里用到的水面、水花、光斑、气泡、遮罩、渐变等特效贴图和材质；
   d. 菜单的布局数据（DataTable，例如 DT_UILayout_Camp…）和多边形 UI（PLG_UI_Camp… 之类）。

【3. 导出（只导出第 2 步认出来的 a~d）】
导出到 <repo>\research\p3r_camp\ 下：
1. **贴图** → tex\<原目录结构>\*.png：保留原尺寸和透明通道（原本是灰度 / 不透明的遮罩就保持原样，不要抠图）。每个子目录一张 contact_sheet.png（缩略图 + 文件名 + 原尺寸）。
2. **控件蓝图 / 动画** → json\：每个相关 WidgetBlueprint、WidgetAnimation、MovieScene、LevelSequence 用 CUE4Parse / FModel 的 JSON 导出（"Save Properties" 那种完整属性 JSON），原样保存。
3. **动画关键帧整理** → anim\<蓝图名>__<动画名>.json（这一步最重要，Claude 要直接照着做）：从上面的 JSON 里把每条 WidgetAnimation 整理成简单格式：
   { "widget_blueprint": "...", "animation": "...", "length_seconds": ..., "fps": ...,
     "tracks": [ { "target_widget": "...", "property": "RenderTransform.Translation / Angle / Scale / Shear / RenderOpacity / ColorAndOpacity / 材质参数名 / Visibility / ...",
                   "keys": [ { "time": 秒, "value": 数值或 [x,y], "interp": "cubic/linear/constant", "tangents": {...原样} } ] } ] }
   拿不准怎么换算的字段（例如帧号 → 秒、曲线切线格式）就把原值也一起放进去，并在 README 里写清楚单位。
4. **控件层级** → tree\<蓝图名>.txt：每个控件蓝图的控件树（缩进列出：控件名、类型、锚点、位置、尺寸、引用的贴图 / 材质 / 字体），这样 Claude 能知道每张图摆在屏幕哪里。
5. **材质** → json\ 下相关的 Material / MaterialInstance 的属性 JSON（参数名和数值、引用的贴图）；能导出 HLSL / 节点信息的话也一起放，没有就算了。
6. **多边形 UI（PLG_…）**：和上次一样，把相关的 PlgDatas 按原始三角形和顶点色转成 PNG（4 倍采样抗锯齿），同时保存原始 JSON。
7. **布局 DataTable** → json\ 原样。
8. 音效不用导出（如果顺手看到菜单打开 / 切换的音效名字，写进 README 就行）。

体积控制：单个文件超过 20MB 的不提交（在 README 里列出路径和大小）；全部加起来超过 300MB 就先只提交 a、b 两类，其余在 README 里列清单，等 Claude 要再导。

【4. README】
<repo>\research\p3r_camp\README.md（中文）：
- 用的工具和版本、AES 配置是哪一份（只写"沿用 README_v14 的 FModel 配置"之类，不要把密钥本身写进来）；
- 第 2 步的分类清单：每个子菜单对应哪个控件蓝图、哪几个动画、哪张立绘；
- 导出了什么、放在哪里；没找到或导不出的，写清楚找过哪些路径和关键词、卡在哪一步；
- 有没有挂载失败的 pak（上次报告过 1 个未挂载归档）。

【5. 提交与推送】
1. 只 add：
   git -c safe.directory=<repo> add research/p3r_camp
2. 提交：
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "research: P3R camp menu UI rip (textures, widget animations, layout, materials)"
3. push：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
   - 失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
   - 被拒绝（远端有新提交）时先 fetch，再 merge --no-edit（同上面的写法）后重新 push。不要 rebase，也不要 --force。
4. 用中文报告：找到了哪些子菜单的立绘和动画、导出了多少文件、总大小、没找到的东西、提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增 research/p3r_camp 下的文件。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码；AES 密钥只在本机工具配置里用，不要写进仓库、README 或报告。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（pak 只读）；导出 / 解包的中间文件放到 C:\Users\kkk\sts2_tools\ 下。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
