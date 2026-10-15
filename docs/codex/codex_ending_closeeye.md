你在用户的 Windows 电脑上工作。任务：为杀戮尖塔 2 mod「P3Makoto」的结局「要闭上眼睛吗？」准备素材 —— A 从本机 P3R 原版资源里只读地提取字样贴图和字体，B 用 imagegen 画一张模糊的天台背景（只做美术，不改代码）。

【背景】
伟大封印结局的最后，照 P3R 原版做「要闭上眼睛吗？」：黑底上一块上下沿内弧的镜头画框，里面是模糊的天台，中间一行「▸要闭上眼睛吗？」，右下大号「闭上眼睛」字样；玩家点击后屏幕慢慢闭眼。
代码已经写好（没有素材时用替代画法），素材到了 Claude 会接进去。
原版截图：<repo>\art\parts_src\ref\ending_closeeye_p3r.jpg（用户从 P3R 原版截的，先仔细看）。
之前你已经从本机 P3R 导出过一批 2D 贴图（<repo>\art\p3r_tex\，说明见 <repo>\art\p3r_tex\README_v14.md）和音效，本机解包目录 C:\Users\kkk\sts2_tools\p3r_rip\。沿用当时的工具和方法（FModel / UModel 等，以你当时用的为准）。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（C:\Users\kkk\ 下、桌面、Documents；可能有多个副本，用你上次成功推送的那个），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
先 git status：有未提交改动就停下报告。然后 checkout claude/amazing-darwin-b00arn，
git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次），git merge --ff-only origin/claude/amazing-darwin-b00arn（不能快进就 git merge --no-edit origin/claude/amazing-darwin-b00arn，不要 rebase）。
如果仓库是稀疏检出、需要的目录不在磁盘上：允许执行 git -c safe.directory=<repo> sparse-checkout add <需要的目录>（这次要用到 art/parts_src/ref 和新建的 art/p3r_tex/ending）；路径过长报错时允许单次命令加 -c core.longpaths=true。不要 sparse-checkout disable / set。

【A：从 P3R 原版提取（只读）】输出到 <repo>\art\p3r_tex\ending\
1. 「闭上眼睛」大字样：截图右下角那组大小交错的半透明白字（闭、眼大，上、睛小）。P3R 里这类结局 / 选项的装饰字通常是贴图，贴图名里可能有 Ending、Ed、CloseEye、Eye、Sleep、Last、Choice、Select、Msg、Title 之类，也可能在中文（zh-Hans / SC）本地化的 UI 贴图包里。把找到的候选全部导出成 PNG（保留透明通道）。
2. 「▸要闭上眼睛吗？」这行字用的字体：P3R 中文界面的字体文件（.ufont / .ttf / .otf，可能叫 FOT-*、Rodin、Matisse、Seurat、DFP*、Noto 或带 SC / CN / zh 字样）。找到的字体文件原样导出（UE 的 .ufont 本身就是 TTF/OTF，改扩展名即可）。截图里这行字看起来是带衬线的明朝 / 宋体类；如果有多种，全部导出，在 README 里写明哪个最像截图。
3. 如果有：那一排六个小点、光标三角、左右边缘的镜头反光贴图。
4. 生成 contact_sheet.png（缩略图 + 文件名 + 原始尺寸）；字体文件用它渲染「要闭上眼睛吗？闭上眼睛」各一行做成 font_samples.png。
找不到的就在 README 里写清楚找过哪些地方、用了什么关键词，不要拿别的东西冒充。

【B：imagegen 画天台背景】输出到 <repo>\art\v16\ending\
5. ending_rooftop_a.png / _b.png：2048×1152（16:9），完整不透明背景，不要文字、水印、UI。照截图的画面：
   - 白天的学校天台，镜头从高处斜着往下看；浅灰色的地面、远处的铁栏杆；明亮、偏淡紫灰的柔光。
   - 画面中间偏左：一个穿深色校服的黑发少年躺着，头枕在一个坐着的金发少女腿上；旁边有一个红色木箱、上面放着黑色的东西。人物很小、只占画面中间一小块。
   - 整张图做成非常强的柔焦 / 景深模糊（像截图那样几乎看不清细节，只有色块和轮廓），整体偏亮、偏灰紫。
   - 提示词示例：“anime game background, school rooftop at daytime seen from a high angle, pale lavender-grey light, a dark-haired schoolboy lying with his head on the lap of a sitting blonde girl, small in the middle of the frame, a red wooden crate beside them, metal railing in the distance, extremely strong soft-focus blur, dreamy, no text”。不要写成任何不当或暴力的描述。
6. preview_ending.png：把两版背景各套进截图那样的镜头画框（黑底，左右各留 13%，上沿中间下沉约 2%、下沿中间上拱约 2.5%），中间写「▸要 闭 上 眼 睛 吗 ？」，右下写「闭上眼睛」，并排放，旁边放原版截图对照。

【自检】
- A：每个导出文件都能打开；字体能正常渲染中文。
- B：两版都是 2048×1152、不透明；人物小、模糊、在画面中间；整体明亮偏灰紫，和原版截图的感觉接近。不满足就重画（每版最多 4 轮），仍不满足就如实报告，把最接近的一张也提交（文件名加 _wip）。

【输出】<repo>\art\p3r_tex\ending\*（PNG、字体文件、contact_sheet.png、font_samples.png）、<repo>\art\v16\ending\*、<repo>\art\p3r_tex\ending\README.md（中文：每样找到 / 没找到、原始资源路径、推荐用哪个）。

【提交与推送】
- 只 git add art\p3r_tex\ending\ 和 art\v16\ending\ 下的文件。提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: P3R close-eyes ending texts/font and blurred rooftop background"
- push：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试）。
  被拒绝是因为别的会话先推送了：先 fetch，再 git merge --no-edit origin/claude/amazing-darwin-b00arn（不要 rebase），然后再 push。
  如果单个文件太大导致推送反复中断（网络在 1.7MB 左右断开），改为把这个提交推到新分支：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin HEAD:codex/ending-assets，推成功就停下报告分支名和提交号。
- 完成后用中文报告做了什么、结果在哪、提交号、每个文件的大小。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要改写 git 历史（rebase / reset / amend）；不要用 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只读）；导出 / 解包的中间文件放到 C:\Users\kkk\sts2_tools\ 下。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换一种中性的描述重试一次，仍被拒就停下报告。
- 不要和编译会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告，不要自作主张。
