你在用户的 Windows 电脑上工作。任务：给 P5Joker 的**选卡界面（战斗结束选一张牌）**生一批 UI 素材，生成完直接提交推送。只生图，不改代码、不编译、不启动游戏、不用 Computer Use。

## 背景
选卡界面要做成 P5 怪盗「夺宝」的感觉：牌后面一道斜切的红黑色带，左上角一个标题，鼠标停在哪张牌上牌后面弹出一块红色底板，选中时盖一个「偷到手」的戳。现在全是代码画的平涂色块，不好看，需要手绘 / 拼贴质感的素材。

**风格**：P5 预告函（calling card）那种**剪报拼贴字**：每个字母 / 汉字是从不同报纸杂志上剪下来的纸片，字体、大小、粗细各不相同，红 / 黑 / 白三色交替（纸片底色和字色互相反差），每片稍微歪一点、边缘是剪刀剪出来的不规则直边，纸片下面有一点点投影。整体配色只用 **红（#E5191C）、黑（#0A0708）、白（#F4F0EC）**，可以有纸张纹理和轻微的印刷磨损。
**原创**：不要照搬《女神异闻录 5》游戏里的现成 logo、角色、具体字图，只取「剪报拼贴预告函」这个风格。
**不要**：网点（halftone 圆点）、霓虹发光、立体渲染 / 玻璃 / 果冻质感、绿色 / 蓝色、背景（全部真透明底）。

## 规矩
- 只在 art/p5/ui/reward/ 下新建文件，不改仓库里其他文件。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量 / PATH；不安装任何东西；不碰游戏目录。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 工作区有本地改动、切分支会被拒时：不要处理那些改动，用独立暂存区 / worktree 只提交 art/p5/ui/reward。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 **claude/confirm-next-tasks-k5wu6t**。

## 1. 拉最新
   git -c safe.directory=* -c http.version=HTTP/1.1 fetch origin claude/confirm-next-tasks-k5wu6t:refs/remotes/origin/claude/confirm-next-tasks-k5wu6t
基于 origin/claude/confirm-next-tasks-k5wu6t 提交。

## 2. 要画的（全部真透明底 RGBA，内容外 alpha = 0；内容离画布边至少留 6% 透明，任何部分都不碰边）
放到 art/p5/ui/reward/。

1. **英文标题 title_treasure.png**（2048×512）：剪报拼贴字「TREASURE」，8 张纸片横排，略往右上倾斜，字母大小错落。
   同时把这 8 个字母**各自单独**存一张：letter_T.png、letter_R.png、letter_E1.png、letter_A.png、letter_S.png、letter_U.png、letter_R2.png、letter_E2.png（每张 512×512，字母纸片居中，和总图里那一片完全一样），代码要一片一片弹出来。
2. **中文标题 title_duobao.png**（1024×512）：剪报拼贴字「夺宝」，两张纸片，同样的风格；并各自单独存 char_duo.png、char_bao.png（每张 512×512）。
3. **副标题 subtitle_take_your_pick.png**（2048×384）：剪报拼贴字「TAKE YOUR PICK」，比标题小、更紧凑的一行。
4. **色带 band.png**（4096×768）：一条横贯画面的斜切长纸带：红色的纸带，上下两边是黑色的厚边条（像撕下来的胶带），纸带两头是斜着剪断的；纸带上可以有很淡的纸纹和几条细的黑色压痕，不要字。
5. **悬停底板 hover_plate.png**（768×1024，竖向）：一块剪出来的红色纸板，四边是不规则的剪刀直边、四角不一样，稍微歪；**另外**存一张同形状的黑色版 hover_plate_shadow.png（同尺寸），代码会错开叠在下面当投影。
6. **选中印章 stamp_stolen.png**（1024×512）：一个斜盖的预告函戳：剪报拼贴字「STOLEN!」贴在一张黑底白边的斜切纸片上，像刚盖上去。

## 3. 提交推送
生成完直接提交推送（不用自己检查、不用重画、不用写报告、不用拼检查图）：
- 只 add art/p5/ui/reward；提交信息 "art: P5 Joker reward screen (calling-card lettering, band, hover plate, stolen stamp)"。
- push：git -c safe.directory=* -c http.version=HTTP/1.1 push origin <你的提交>:claude/confirm-next-tasks-k5wu6t；被拒先 fetch 再 merge --no-edit（不 rebase）再 push。
