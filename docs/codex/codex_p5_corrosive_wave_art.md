你在用户的 Windows 电脑上工作。任务：给 P5Joker 的**腐蚀波**生几张手绘贴图（浪身纹理 + 浪头 / 拍击的序列帧）。只生图、提交、推送，不改代码、不编译、不启动游戏、不用 Computer Use。

## 背景
腐蚀波（这回合每抽一张牌给全体上毒）的特效：每个敌人身上卷起一道咒怨的浪 —— 像过山车一样从敌人左下方往右上爬起、翻过头顶，在最高点顿一下，
再猛地斜着往右下拍到敌人身上（拍中 = 上毒）。浪的运动、变形、溶解全由代码做（沿一条弯曲的带子流动、尾巴蚀散），你只负责把素材画好。
用户很在意「贴图感」：所以素材**不会整张原样贴出来**，而是被拉到弯曲的浪身上、滚动、按亮度上色、被噪声蚀掉。所以画的时候要的是**质感和流动的细节**，不是一个完整的图案。

**颜色的用法很重要**：代码按贴图的**亮度**查 P5 红黑渐变上色（白 → 白芯，中亮 → 绯红，暗 → 深红 / 墨黑），所以每张图要像 art/p5/fx/v4/slash/slash_thin.png 那样：
**亮白的芯 + 绯红（#E5191C）的中间调 + 墨黑的暗部和飞白**，明暗层次清楚。画风和 art/p5/fx/v4/slash/ 里的刀光一致（手绘感、锐利、有细节）。
**不要**：网点、平涂色块、粗黑描边的卡通造型、火焰（明确拉黑）、大团黑烟、文字、绿色。

## 【0. 绝对不要做】
- 不修改仓库里任何已有文件；只在 art/p5/fx/v4/corrosive/ 下新建文件，以及按第 4 步新建报告文件。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量 / PATH；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不删除任何目录或文件。
- 不进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 不碰游戏目录；不启动游戏，不关闭 / 结束任何进程。
- 被安全系统拒绝：不重试，停下来写进报告。遇到下面没写到的情况、任何一步报错：停下来，写进报告，推送报告，结束。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 **claude/confirm-next-tasks-k5wu6t**。

## 【1. 拉最新】
在 <repo> 里依次单独运行：
   git -c safe.directory=* -c http.version=HTTP/1.1 fetch origin claude/confirm-next-tasks-k5wu6t:refs/remotes/origin/claude/confirm-next-tasks-k5wu6t
   git -c safe.directory=* switch claude/confirm-next-tasks-k5wu6t
   git -c safe.directory=* merge --ff-only origin/claude/confirm-next-tasks-k5wu6t
（本地还没有这个分支时，switch 换成：git -c safe.directory=* switch -c claude/confirm-next-tasks-k5wu6t origin/claude/confirm-next-tasks-k5wu6t，然后跳过 merge。）
fetch 超时按 5/10/20/40 秒重试；switch / merge 失败就停下来报告完整报错。

## 【2. 要画的】（全部真透明底 RGBA，轮廓外 alpha = 0，边缘柔和、无底色残留）
放到 art/p5/fx/v4/corrosive/。每张：交付版 PNG + 原始输出 *_raw.png + *_generation.json（提示词、尺寸）。

1. **浪身 curse_wave_body_01~02.png**（每张 2048×512，横向，**左右两端能无缝接上**）：
   一段侧面看的「咒怨之浪」的浪身，横着铺满画面：上沿是被撕碎的浪尖和飞沫（亮白 + 绯红，锐利、往右后方甩），
   中间是顺着长度方向（左 → 右）流动的一道道丝状的流纹（像高速流动的液体 / 能量，亮丝和暗缝交错），
   下沿越来越稀，蚀成墨黑的碎块和飞白、慢慢透明。整体是有体积的液态能量，不是一根光滑的管子。两张流纹和浪尖形状不同。
2. **浪头 curse_wave_crest_sheet.png**（2048×2048，4×4 网格，每格 512，**16 帧连续动画**，从左到右、从上到下播放）：
   浪头卷起、翻过来、往右下拍出去的过程（侧面看）：前几帧浪尖往上隆起，中间几帧卷成一个带飞沫的卷，最后几帧往右下甩开碎成飞沫。每格内容居中、不出格。
3. **拍击 curse_splash_sheet.png**（2048×2048，4×4 网格，每格 512，**16 帧连续动画**）：
   咒怨液体从左上往右下猛地拍到一个东西上、炸开溅射的过程：第 1～3 帧冲击点一下爆亮（白芯），之后液块和飞沫朝右下 / 两侧呈扇形溅开、拉成细丝，
   最后几帧散成墨黑的小滴和飞白、淡掉。溅射有方向感（主要往右下），不是均匀的圆形爆炸。每格内容居中、不出格。

## 【3. 检查图和提交】
- 拼一张 check_corrosive.png（深灰底）和 check_corrosive_checker.png（棋盘格底，看透明边缘）；浪身两张再各拼一张「左右接起来两遍」的 tile 检查图，看接缝。
- 每张图只生成一次；只有生成报错、空图、画布尺寸不对、出现文字时重试一次。好不好看由 Claude 判断，不要自己挑毛病重画。
- 写 art/p5/fx/v4/corrosive/README.md：每张图一行（文件名、尺寸、一句话说明）。
- 只 add art/p5/fx/v4/corrosive；提交信息 "art: P5 Joker corrosive wave textures (wave body, crest and splash flipbooks)"。
  总量不超过 60MB，单文件不超过 20MB。
- push：git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/confirm-next-tasks-k5wu6t；被拒先 fetch 再 merge --no-edit（不 rebase）再 push，超时按 5/10/20/40 秒重试。

## 【4. 报告】
新建 docs/codex/reports/codex_p5_corrosive_wave_art_report.md（中文）：每张图生成了几次、有没有被拒绝、检查图位置、提交号。和图一起提交推送。
