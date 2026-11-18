你在用户的 Windows 电脑上工作。任务：给 P5Joker 的**墨之刃**（第三版「甩墨出刃」）生几套手绘序列帧 / 贴图。只生图、提交、推送，不改代码、不编译、不启动游戏、不用 Computer Use。

## 背景
墨之刃（生成 2～3 张带「墨」附魔的小刀；这套 mod 里小刀 = 子弹）。新创意要有**三维空间感**：
1. 蓄墨：Joker 左手掌心一团墨卷起来、墨丝往掌心收；
2. 甩（主拍，要有力）：左手往右前方猛地一甩，墨划出一道弧从他身侧一路甩向**镜头 / 玩家**——离镜头越近越大，近处的墨点有运动模糊和景深虚化；
3. 墨点凝成子弹：甩出的墨里有 2～3 滴在空中被拉长、外层的墨皮剥落，露出带红色高光的子弹；
4. 子弹翻着飞回他右手。
运动、缩放、顿帧、拖尾、溶解由代码做，你只负责把素材画好。用户很在意「贴图感」：素材会被代码缩放 / 蚀散 / 配合粒子用，所以要的是**有体积、有透视、有动势**的墨，不是平面图案。

**颜色（直接用原色）**：主体**墨黑**（#0B0205，近黑带一点暗红），边缘和飞白透 **P5 红**（#E5191C）的反光，少量**白色**湿亮高光。像日本书法的墨但带 P5 的红黑白。
**不要**：网点、平涂色块、卡通描边、火焰、烟、文字、绿色 / 蓝色、背景（必须透明）。
参考：art/p5/fx/v4/ink/ 里上一批墨的质感（ink_stroke、ink_drops）就是对的方向，这次要的是**运动和透视**。

## 【0. 绝对不要做】
- 不修改仓库里任何已有文件；只在 art/p5/fx/v4/ink_fling/ 下新建文件，以及按第 4 步新建报告文件。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量 / PATH；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不删除任何目录或文件。
- 不进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 不碰游戏目录；不启动游戏，不关闭 / 结束任何进程。
- 被安全系统拒绝：不重试，停下来写进报告。遇到下面没写到的情况、任何一步报错：停下来，写进报告，推送报告，结束。
- 工作区有本地改动、切分支会被拒时：不要处理那些改动，用独立暂存区 / worktree 的办法只提交 art/p5/fx/v4/ink_fling 和报告（上次墨之刃就是这么做的）。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 **claude/confirm-next-tasks-k5wu6t**（推到这个分支）。

## 【1. 拉最新】
   git -c safe.directory=* -c http.version=HTTP/1.1 fetch origin claude/confirm-next-tasks-k5wu6t:refs/remotes/origin/claude/confirm-next-tasks-k5wu6t
基于 origin/claude/confirm-next-tasks-k5wu6t 提交。fetch 超时按 5/10/20/40 秒重试。

## 【2. 要画的】（全部真透明底 RGBA，轮廓外 alpha = 0，边缘柔和、无底色残留）
放到 art/p5/fx/v4/ink_fling/。每张：交付版 PNG + 原始输出 *_raw.png + *_generation.json（提示词、尺寸）。
序列帧都是 2048×2048、4×4 网格、每格 512、**16 帧连续动画**（从左到右、从上到下播放），每格内容不出格；同一套的 16 帧之间**视角、光线、画风必须连贯**（是同一个动作的连续瞬间）。

1. **蓄墨 ink_gather_sheet.png**：一团墨在（看不见的）掌心卷起来：一开始几缕细墨丝从四周往中心卷进来，越卷越多、越转越快，
   最后聚成一团翻滚的、表面湿亮的墨球（稍微有点往右拉长，像要甩出去前那一下的蓄力）。中心在格子正中。
2. **甩墨 ink_fling_sheet.png**（**最重要**）：三维透视下，一大团墨从画面**左侧偏后（远、小）**被猛地甩出来，划一道往右、往**镜头**方向的弧：
   前几帧是一条细长有力的墨鞭（像毛笔狠狠甩出的一笔，带速度线一样的拖丝），中间几帧墨团冲到最近、占满格子的右下大半，最近的几滴墨大而虚（运动模糊 + 景深虚化），
   能看出明显的近大远小；最后几帧墨散成往四周飞的大大小小的墨点和墨丝、越来越稀。整体要**有力量感和速度感**，不要软绵绵的。
3. **墨凝成弹 ink_bullet_form_sheet.png**：一滴拉长的墨（横向，弹头朝右）在空中硬化成一颗子弹：前几帧是一滴颤动的墨，中间墨被拉长、表面开始绷紧发亮，
   之后外层的墨皮一片片剥落、往后飞散，露出里面一颗乌黑发亮、带一道红色高光的子弹（手枪子弹的弹头 + 弹壳的形状，横放），最后几帧只剩子弹和一点残墨。单颗、居中。
4. **近景墨点 ink_foreground_sheet.png**（4×4，每格一个**独立物体**，不是动画）：16 颗离镜头很近、飞过镜头的大墨点 / 墨丝：
   有的清晰，有的明显失焦虚化（景深），有的带运动模糊拖尾，大小形状各异，带白色湿亮高光、边缘透红。代码会把它们放在最前面、放大、快速飞过。

## 【3. 检查图和提交】
- 拼一张 check_ink_fling.png（深灰底）和 check_ink_fling_checker.png（棋盘格底，看透明边缘）。
- 每张图只生成一次；只有生成报错、空图、画布尺寸不对、出现文字、出现背景时重试一次。好不好看由 Claude 判断，不要自己挑毛病重画。
- 写 art/p5/fx/v4/ink_fling/README.md：每张图一行（文件名、尺寸、一句话说明）。
- 只 add art/p5/fx/v4/ink_fling；提交信息 "art: P5 Joker ink fling flipbooks (gather, fling, bullet forming, foreground drops)"。总量不超过 60MB，单文件不超过 20MB。
- push：git -c safe.directory=* -c http.version=HTTP/1.1 push origin <你的提交>:claude/confirm-next-tasks-k5wu6t；被拒先 fetch 再 merge --no-edit（不 rebase）再 push，超时按 5/10/20/40 秒重试。

## 【4. 报告】
新建 docs/codex/reports/codex_p5_ink_fling_art_report.md（中文）：每张图生成了几次、有没有被拒绝、检查图位置、提交号。和图一起提交推送。
