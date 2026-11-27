你在用户的 Windows 电脑上工作。任务：为 P5Joker mod 的两项新内容**收集 P5R 原版参考资料**（只读游戏文件、整理、推送）。
这一轮**不生图、不改代码、不启动游戏**。生图要等 Claude 看过这批参考、定好构图之后，下一轮再给你生图 prompt。

先读 docs/codex/README.md，按里面的长期规则来（尤其是：遇到问题自己判断继续做，最后在报告里写清楚）。

【两项新内容】
1. **结局替换**：杀戮尖塔 2 最后在建筑师房间（塔顶）有一段对话和一个结束按钮。P3Makoto 已经把它换成了 P3R 的「伟大封印」结局（代码 mod/src/Patches/ArchitectPatches.cs、mod/src/Vfx/GreatSeal.cs，可以参考它替换了哪些部分）。
   P5Joker 要换成 **P5R 第三学期丸喜拓人（丸喜老师 / 心理咨询师）路线的结局**：房间、对话气氛、最后那个**按键提示「SHOOT」开枪**的演出，以及之后的收尾。
2. **商人替换**：商店房间换成 **P5R 的天鹅绒房间**（监狱主题：牢房、铁栏、蓝色调、伊戈尔的桌子、看守双子卡萝莉娜和芮丝汀娜，第三学期是拉雯妲）。注意 P5R 的天鹅绒房间和 P3 的电梯完全不同，不能拿 P3Makoto 的天鹅绒房间改。

【1. 找到仓库并拉取】
- 在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、含 p5mod\P5Joker.json 的 git 仓库（用你上次成功推送的那个），记作 <repo>。
- git 参数、重试、合并规则照 README。分支用 **claude/epic-archimedes-m9an69**：先 fetch，再 merge --ff-only；不能快进就 merge --no-edit（不要 rebase）。
- 稀疏检出缺目录时允许 sparse-checkout add research/p5r docs（可加 -c core.longpaths=true）。

【2. 资源库可能已经被清理】
用户刚清理过磁盘，C:\Users\kkk\sts2_tools\p5r_rip（P5R 解包资源库）可能已经不在了。
- 不在的话**不要整包重解**：用仓库里的 research/p5r/index.csv、movie_list.csv 查到需要的文件，用 C:\Users\kkk\sts2_tools\p5r_tools 里的 P5RAssetTool（CriFsV2Lib）按包内路径单个提取。工具缺了就照 research/p5r/README.md 里的官方下载地址重新下载便携版。
- 游戏目录在 research/p5r/README.md 里写着（G:\Gamebox\P5R\ZS6425\P5R），找不到就搜各盘。
- 这一轮新解出来的东西放 C:\Users\kkk\sts2_tools\p5r_rip\ref_ending_velvet\，**做完只留整理后放进仓库的部分，其余中间文件删掉**（用户磁盘很紧）。

【3. 要找的东西】
每一项都写清：原始包内路径、导出了什么、没找到的话搜过哪些关键词。先用 index.csv、movie_list.csv 按文件名筛（例如 MOVIE 下第三学期 / 结局相关的 USM、含 maruki / adam / kadmon / velvet / igor / caroline / justine / lavenza / shoot / qte 的路径），再结合文件大小、播放内容确认。

A. 丸喜结局（放 <repo>\research\p5r\ref\maruki_ending\）
1. **找到包含「SHOOT」按键提示的那段**：它是过场视频（USM）、还是即时演算加 UI 叠加？把那段从开始提示到开枪、到画面结束的完整过程整理出来：
   - 视频的话：截成 854 宽、60fps（原片帧率低就按原帧率）、无声的 MP4 片段，每段不超过 40 秒、单文件不超过 15MB；另出逐帧图（每 0.25 秒一张，拼成 contact_sheet.png，每张标时间码）。
   - 按键提示本身的 UI 贴图（「SHOOT」字样、按键图标、圆环 / 准星之类）：找到对应的 SPD / 贴图就导出原尺寸透明 PNG 和切图坐标 JSON；找不到就从视频里截清晰的单帧。
2. 丸喜最终战结束到结局收尾之间的关键过场（丸喜和 Joker 对峙、开枪、之后的画面），同样出 MP4 片段 + contact_sheet。总共控制在 6 段以内，挑最关键的。
3. 这几段对应的**音效和语音**（开枪声、提示出现的音效、Joker / 丸喜的关键台词）：转成 OGG 放 audio\，catalog.csv 写流序号、时长、你判断的内容。BGM 只记曲名和所在文件，不解。
4. 写 README.md：按时间顺序描述整个演出（几秒出现什么、镜头怎么动、UI 怎么出现和消失、颜色和字体），这是 Claude 设计游戏内演出的主要依据，写细一点。拿不准的地方标「不确定」。

B. P5R 天鹅绒房间（放 <repo>\research\p5r\ref\velvet_room\）
1. **房间外观**：从过场视频 / 进入天鹅绒房间的演出里截 8–12 张清晰的画面（全景、牢房内景、伊戈尔的桌子、双子站在铁栏外、拉雯妲），原分辨率 PNG，拼一张 contact_sheet.png。房间地图模型（FIELD 下的天鹅绒房间）能用 GFD Studio 导出的话，额外渲 3–4 个角度的截图；导不出来就跳过，在报告里写明。
2. **人物立绘**：伊戈尔、卡萝莉娜、芮丝汀娜、拉雯妲的立绘 / 半身像 / 表情差分（BUSTUP 之类），原尺寸透明 PNG，附 contact_sheet.png。
3. **天鹅绒房间 UI**：仓库 research/p5r/ui/velvet/ 已经有 28 张，检查是否齐全（合体、强化、处刑、图鉴、监狱相关的菜单背景、标题字、框）；缺的补上（原尺寸透明 PNG + 切图坐标 JSON），每个子页一张截图说明它在游戏里长什么样（从视频截图即可）。
4. **音效和语音**：进入 / 离开天鹅绒房间的音效、菜单确认 / 购买类音效、双子和拉雯妲的招呼语音（例如「欢迎」之类）。转 OGG 放 audio\，catalog.csv 同上。BGM 只记曲名和所在文件。
5. 写 README.md：房间的空间结构（镜头位置、牢房和桌子的相对位置）、主色调（给出几个取样色值）、灯光、人物各自站在哪里、进入房间时的过场怎么走。

【4. 体积】
仓库里这一轮新增总共不超过 150MB，单文件不超过 20MB；超了就降分辨率或删减片段，原质量的留在本机 ref_ending_velvet\ 并在 README 里列出路径。

【5. 提交与推送】
- 只 git add research\p5r\ref\ 下的文件。提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "research: P5R Maruki ending and Velvet Room references"
- push 到 claude/epic-archimedes-m9an69（重试、被拒后 fetch + merge --no-edit 再推，照 README）。

【安全清单（硬底线）】
不改代码和已有资源；不改写历史、不 rebase、不 --force；不启动游戏；游戏文件只读；不读 C:\Users\kkk\.p3record\obs.json、密码或任何密钥文件；解密配置只留在本机工具里，不写进仓库和报告；不绕过安全拒绝。

【报告】
找到 / 没找到的清单（含路径和搜过的关键词）、「SHOOT」那段是视频还是即时演算、各目录的文件数和大小、本机还留了哪些文件和总大小、遇到的问题和处理方式。
