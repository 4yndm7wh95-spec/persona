你在用户的 Windows 电脑上工作。任务：给 P5Joker 的**战斗开始 / 回合横幅**生一批 UI 素材，生成完直接提交推送。只生图，不改代码、不编译、不启动游戏、不用 Computer Use。

## 背景
原版每场战斗开始会出一行「战斗开始」，每回合出「玩家回合 / 第 N 回合」「敌人回合」。要换成 P5 怪盗风：
- 战斗开始：画面中间一条剪报纸带横扫过去，上面一片一片拍上剪报拼贴字「TAKE YOUR HEART!」；
- 我方回合：左上角一条黑色纸条滑进来，拼贴字「YOUR TURN」，旁边一块红色小纸牌上是拼贴数字（第几回合）；
- 敌人回合：红色纸条 + 拼贴字「ENEMY TURN」；
- 额外回合：拼贴字「ONE MORE!」。
代码会把单词拆成一个个字母纸片、一片一片弹出来，所以**每个字母都要单独一张图**。

**风格**：和 art/p5/ui/reward/ 里已经用上的图（letter_T.png 等字母、title_treasure.png、header_plate.png、stamp_stolen.png）**完全同一套**：P5 预告函（calling card）的**剪报拼贴字** —— 每个字母是从不同报纸杂志上剪下来的纸片，字体、粗细、大小各不相同，纸片底色和字色互相反差，每片稍微歪一点，边缘是剪刀剪出来的不规则直边，纸片下面一点点投影，可以有纸张纹理和轻微的印刷磨损。整体只用 **红（#E5191C）、黑（#0A0708）、白（#F4F0EC）**。
**原创**：不要照搬《女神异闻录 5》游戏里的现成 logo、字图，只取「剪报拼贴预告函」这个风格。
**不要**：网点（halftone 圆点）、霓虹发光、立体渲染 / 玻璃 / 果冻质感、绿色 / 蓝色、背景（全部真透明底）。

## 规矩
- 只在 art/p5/ui/battle/ 下新建文件，不改仓库里其他文件。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量 / PATH；不安装任何东西；不碰游戏目录。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 工作区有本地改动、切分支会被拒时：不要处理那些改动，用独立暂存区 / worktree 只提交 art/p5/ui/battle。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 **claude/confirm-next-tasks-k5wu6t**。

## 1. 拉最新
   git -c safe.directory=* -c http.version=HTTP/1.1 fetch origin claude/confirm-next-tasks-k5wu6t:refs/remotes/origin/claude/confirm-next-tasks-k5wu6t
基于 origin/claude/confirm-next-tasks-k5wu6t 提交。

## 2. 要画的（全部真透明底 RGBA，内容外 alpha = 0；内容离画布边至少留 6% 透明，任何部分都不碰边）

### 2.1 字母纸片 → art/p5/ui/battle/letters/
每个字母一张 **512×512**，一张纸片居中（纸片约占画布 60–75%）。同一个单词里的字母要互相搭配（大小错落、红黑白交替，像一个词剪出来拼在一起），不同单词之间风格统一。文件名 = 单词_序号.png（序号从 1 开始，按字母顺序）：
- TAKE_1.png … TAKE_4.png（T A K E）
- YOUR_1.png … YOUR_4.png（Y O U R）
- HEART_1.png … HEART_6.png（H E A R T !，第 6 张是感叹号纸片）
- TURN_1.png … TURN_4.png（T U R N）
- ENEMY_1.png … ENEMY_5.png（E N E M Y，配色偏红底，显得危险一点）
- ONE_1.png … ONE_3.png（O N E）
- MORE_1.png … MORE_5.png（M O R E !，第 5 张是感叹号纸片）

### 2.2 数字纸片 → art/p5/ui/battle/digits/
digit_0.png … digit_9.png，每张 **512×512**：一个拼贴数字纸片，比字母纸片更粗壮醒目（回合数），10 个数字配色交替但整体统一。

### 2.3 底板 → art/p5/ui/battle/
1. **strip_enemy.png**（2048×384）：和 art/p5/ui/reward/header_plate.png 同形状的横向剪报纸条，但**红色纸条**、下沿垫一条错开的**黑色**纸条，两头斜着剪断，**不要任何字**。
2. **turn_tag.png**（768×512）：一块剪出来的**红色小纸牌**（略歪、四边不规则直边、下面垫一层错开的黑色纸当投影），左上角用小号白色印刷字印着「TURN」，中间大片留空（游戏会把数字纸片贴在中间）。
3. **turn_tag_enemy.png**（768×512）：同形状的**黑色**小纸牌（垫红色投影），左上角小号红字「ENEMY」，中间留空。

## 3. 提交推送
生成完直接提交推送（不用自己检查、不用重画、不用写报告、不用拼检查图）：
- 只 add art/p5/ui/battle；提交信息 "art: P5 Joker battle start + turn banner (calling-card letters, digits, strips)"。
- push：git -c safe.directory=* -c http.version=HTTP/1.1 push origin <你的提交>:claude/confirm-next-tasks-k5wu6t；被拒先 fetch 再 merge --no-edit（不 rebase）再 push。
