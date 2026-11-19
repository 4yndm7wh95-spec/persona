你在用户的 Windows 电脑上工作。任务：给 P5Joker 的**选卡界面**再补几张 UI 素材，生成完直接提交推送。只生图，不改代码、不编译、不启动游戏、不用 Computer Use。

## 背景
选卡界面上一批素材（art/p5/ui/reward/ 里的剪报拼贴字、红色纸带、悬停红纸板、STOLEN 印章）已经用上了。界面上还剩两样是原版的：
- 牌上方那条写着「选择一张牌」的米色卷轴横幅；
- 牌下方那个蓝绿色的「跳过」按钮。
要换成**同一套风格**：照 art/p5/ui/reward/ 里现有的图（title_treasure.png、band.png、hover_plate.png、stamp_stolen.png），P5 预告函的**剪报拼贴**风：剪刀剪出来的不规则直边纸片、纸张纹理、轻微印刷磨损，只用 **红（#E5191C）、黑（#0A0708）、白（#F4F0EC）**。
**不要**：网点（halftone 圆点）、霓虹发光、立体渲染 / 玻璃质感、绿色 / 蓝色、背景（全部真透明底）。

## 规矩
- 只在 art/p5/ui/reward/ 下新建文件，不改仓库里其他文件（不要覆盖已有的图）。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量 / PATH；不安装任何东西；不碰游戏目录。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 工作区有本地改动、切分支会被拒时：不要处理那些改动，用独立暂存区 / worktree 只提交 art/p5/ui/reward。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 **claude/confirm-next-tasks-k5wu6t**。

## 1. 拉最新
   git -c safe.directory=* -c http.version=HTTP/1.1 fetch origin claude/confirm-next-tasks-k5wu6t:refs/remotes/origin/claude/confirm-next-tasks-k5wu6t
基于 origin/claude/confirm-next-tasks-k5wu6t 提交。

## 2. 要画的（全部真透明底 RGBA，内容外 alpha = 0；内容离画布边至少留 6% 透明，任何部分都不碰边）
放到 art/p5/ui/reward/。

1. **横幅底板 header_plate.png**（2048×384）：一条横向的黑色剪报纸条，两头斜着剪断，下沿垫一条错开一点的红色纸条（像两层纸叠着贴上去），纸条上**不要任何字**（游戏会把文字写在上面）。
2. **横幅文字 header_text_zh.png**（2048×384）：剪报拼贴字「选择一张牌」，5 张纸片横排、大小错落、红黑白交替，整体大小能放进横幅底板里。
3. **跳过按钮底板 skip_plate.png**（1024×384）：一块横向的剪出来的纸板按钮（黑底、白色细边、四边不规则直边、整体略斜），**不要字**；
   另外同形状画一张**选中 / 悬停版 skip_plate_hover.png**（1024×384）：红底黑边，比普通版更醒目。
4. **跳过文字 skip_text_zh.png**（768×384）：剪报拼贴字「跳过」，两张纸片；以及英文版 **skip_text_en.png**（768×384）：剪报拼贴字「SKIP」。

## 3. 提交推送
生成完直接提交推送（不用自己检查、不用重画、不用写报告、不用拼检查图）：
- 只 add art/p5/ui/reward；提交信息 "art: P5 Joker reward screen header + skip button"。
- push：git -c safe.directory=* -c http.version=HTTP/1.1 push origin <你的提交>:claude/confirm-next-tasks-k5wu6t；被拒先 fetch 再 merge --no-edit（不 rebase）再 push。
