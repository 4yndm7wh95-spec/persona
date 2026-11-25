你在用户的 Windows 电脑上工作。任务：给 P5Joker 战斗里的**能量球**和**结束回合按钮**生一批 UI 素材，生成完直接提交推送。只生图，不改代码、不编译、不启动游戏、不用 Computer Use。

## 背景
战斗画面左下角的能量球（显示「当前能量 / 上限」，比如 3/3）和右下角的「结束回合」按钮还是原版（猎人的绿色能量球、原版按钮）。要换成和选卡界面、回合横幅**同一套**的 P5 怪盗风。
- 能量球：游戏里显示约 **150×150 像素**，所以要大块形状、剪影清楚，不要细碎小细节。能量数字由代码用已有的拼贴数字纸片（art/p5/ui/battle/digits/）贴在中间，**底座上不要画数字**。能量球右上角还挂着一个左轮弹巢（小刀弹药），底座整体要紧凑，别往右上角伸太多。
- 结束回合按钮：游戏里显示约 **260×100 像素**。有三种状态：平时、「该结束回合了」（原版会发光提示，这里换成更醒目的版本）、不能点（代码自己压暗，不用画）。

**风格**：和 art/p5/ui/reward/（letter_T.png、header_plate.png、skip_plate.png、skip_plate_hover.png、stamp_stolen.png）以及 art/p5/ui/battle/ 里的图**完全同一套**：P5 预告函（calling card）的**剪报拼贴** —— 剪刀剪出来的不规则直边纸片、纸张纹理、轻微印刷磨损、纸片下面错开垫一层投影纸。整体只用 **红（#E5191C）、黑（#0A0708）、白（#F4F0EC）**。
**原创**：不要照搬《女神异闻录 5》游戏里的现成 UI、logo、字图，只取「剪报拼贴预告函」这个风格。
**不要**：网点（halftone 圆点）、霓虹发光、立体渲染 / 玻璃 / 果冻质感、绿色 / 蓝色、背景（全部真透明底）。

## 规矩
- 只在 art/p5/ui/hud/ 下新建文件，不改仓库里其他文件（不要覆盖已有的图）。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量 / PATH；不安装任何东西；不碰游戏目录。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 工作区有本地改动、切分支会被拒时：不要处理那些改动，用独立暂存区 / worktree 只提交 art/p5/ui/hud。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 **claude/confirm-next-tasks-k5wu6t**。

## 1. 拉最新
   git -c safe.directory=* -c http.version=HTTP/1.1 fetch origin claude/confirm-next-tasks-k5wu6t:refs/remotes/origin/claude/confirm-next-tasks-k5wu6t
基于 origin/claude/confirm-next-tasks-k5wu6t 提交。

## 2. 要画的（全部真透明底 RGBA，内容外 alpha = 0；内容离画布边至少留 6% 透明，任何部分都不碰边）
放到 art/p5/ui/hud/。

### 2.1 能量球
1. **energy_base.png**（1024×1024）：能量球的底座。一块剪出来的**黑色圆形纸片**（边缘是剪刀剪的多边形直边，不是完美的圆），后面垫一块错开的**红色锯齿星芒纸片**（像爆炸的尖角，6–8 个尖，长短不一），最下面再垫一层错开的黑色投影纸；中间黑色区域大片留空（代码贴数字）。
2. **energy_ring.png**（1024×1024）：一圈单独的**白色纸条剪成的细圆环**，断成 3–4 段、每段长短不一、两头斜剪，环的直径和 energy_base 的黑色圆片外缘差不多（代码会让它慢慢转，和底座分开一层）。
3. **energy_spent.png**（1024×1024）：能量用完时的底座：和 energy_base **同形状、同位置**，但星芒纸片换成黑色、圆片换成很暗的灰黑色，整体像熄灭了。

### 2.2 能量数字的斜杠
4. **slash.png**（512×512）：一张拼贴的斜杠「/」纸片（白纸黑字或红纸白字），和 art/p5/ui/battle/digits/ 的数字纸片同风格，比数字纸片小一号、细长一点。

### 2.3 结束回合按钮
5. **endturn_plate.png**（1024×384）：横向的剪报纸板按钮：**黑底**、白色细边、四边不规则直边、整体略斜（左低右高），下面错开垫一层**红色**纸当投影；**不要字**。
6. **endturn_plate_ready.png**（1024×384）：「该结束回合了」状态：和上面**同形状、同位置**，换成**红底**、黑色粗边、垫黑色投影，右上角多贴一小块白色三角纸片当强调，比普通版明显更醒目；**不要字**。
7. **endturn_text_zh.png**（768×256）：剪报拼贴字「结束回合」，4 张纸片横排、大小错落、红黑白交替，整体大小能放进按钮纸板里。
8. **endturn_text_en.png**（768×256）：剪报拼贴字「END TURN」，同样的风格。

## 3. 提交推送
生成完直接提交推送（不用自己检查、不用重画、不用写报告、不用拼检查图）：
- 只 add art/p5/ui/hud；提交信息 "art: P5 Joker HUD (energy medallion, end turn button)"。
- push：git -c safe.directory=* -c http.version=HTTP/1.1 push origin <你的提交>:claude/confirm-next-tasks-k5wu6t；被拒先 fetch 再 merge --no-edit（不 rebase）再 push。
