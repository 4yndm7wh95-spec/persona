你在用户的 Windows 电脑上工作。任务：给 P5Joker 生一批新的**手绘特效素材**，生成完直接提交推送。只生图，不改代码、不编译、不启动游戏、不用 Computer Use。

## 背景
打击牌命中用的手绘刀光（art/p5/fx/v4/slash/ 的 slash_thin.png、slash_heavy.png、slash_cross.png）用户很认可，但已经被好几张牌用过了，用户不想再复用。
这一批要**同一种画风、同一种上色规则**，但**形状全新**，给后面几张牌用（压制、狩猎、夜魇、必备工具等）。

**画风**：完全照 art/p5/fx/v4/slash/slash_heavy.png 和 slash_thin.png：手绘、锐利、有速度感和飞白，像动画里的斩击特效。
**上色规则（最重要）**：代码按贴图的**亮度**查 P5 红黑渐变上色，所以每张图都要像 slash_* 那样：**亮白的芯 + 绯红（#E5191C）的中间调 + 少量墨黑的飞白 / 暗部**，明暗层次清楚。
**不要**：果冻 / 玻璃 / 立体渲染质感、颗粒分明的小点和飞沫（颗粒由代码做）、网点、平涂色块、卡通描边、火焰、烟、文字、绿色 / 蓝色、背景（必须透明）。

## 规矩
- 只在 art/p5/fx/v4/painted/ 下新建文件，不改仓库里其他文件。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量 / PATH；不安装任何东西；不碰游戏目录。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 工作区有本地改动、切分支会被拒时：不要处理那些改动，用独立暂存区 / worktree 只提交 art/p5/fx/v4/painted。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 **claude/confirm-next-tasks-k5wu6t**。

## 1. 拉最新
   git -c safe.directory=* -c http.version=HTTP/1.1 fetch origin claude/confirm-next-tasks-k5wu6t:refs/remotes/origin/claude/confirm-next-tasks-k5wu6t
基于 origin/claude/confirm-next-tasks-k5wu6t 提交。

## 2. 要画的（全部真透明底 RGBA，轮廓外 alpha = 0；每张内容离画布边至少留 6% 透明，任何部分都不碰边）
放到 art/p5/fx/v4/painted/。

1. **重劈 chop_heavy_01~02.png**（每张 1024×2048，竖向）：一记从上往下狠狠劈下来的重斩，上端细而尖、越往下越宽越重，底端砸开成一个锐利的扇形豁口。两张轮廓不同。
2. **弧斩 swipe_arc_01~03.png**（每张 2048×1024）：三种和 slash_* 明显不同的弧形斩击：一道很宽的半月形、一道带回钩的 S 形、一道两刃交错的弧（剪刀形）。边缘都有飞白、末端锐利收尖。
3. **突刺 thrust_01~02.png**（每张 2048×512，横向）：笔直有力的突刺，前端是锐利的尖锋，后面拖着几道平行的速度纹，越往后越散。两张粗细不同。
4. **爪痕 claw_rake_01~02.png**（每张 1024×1024）：三道（第二张四道）并排斜着撕下去的爪痕，每道从细到粗再收尖，撕口边缘锐利、带飞白。
5. **冲击 impact_star_sheet.png**（2048×2048，4×4 网格，每格 512，**16 帧连续动画**）：一个不对称的锐利冲击星芒（像斩中那一下的爆点）：第 1～3 帧猛地炸开（亮白芯），之后尖角往外伸长变细、芯变暗，最后几帧只剩几道细尖角淡掉。每格内容居中、离格子边至少 48 像素。
6. **暗影手 shadow_grasp_sheet.png**（2048×2048，4×4 网格，每格 512，每格一个**独立物体**，不是动画）：16 种用同样斩击笔触画出来的「抓取的手 / 爪」剪影：张开的手指、攥紧的、从下往上伸的、从侧面扑过来的，都是锐利的手绘笔触（不是写实的手），亮白芯 + 红 + 墨黑。

## 3. 提交推送
生成完直接提交推送（不用自己检查、不用重画、不用写报告、不用拼检查图）：
- 只 add art/p5/fx/v4/painted；提交信息 "art: P5 Joker painted FX set (chops, swipes, thrusts, claw rakes, impact flipbook, shadow grasps)"。
- push：git -c safe.directory=* -c http.version=HTTP/1.1 push origin <你的提交>:claude/confirm-next-tasks-k5wu6t；被拒先 fetch 再 merge --no-edit（不 rebase）再 push。
