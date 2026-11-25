你在用户的 Windows 电脑上工作。任务：给 P5Joker 的**增益牌特效**生一批新的手绘贴图。只生图、提交、推送，不改代码、不编译、不启动游戏、不用 Computer Use。

## 背景
增益牌（预判、侧步、磨蚀、速行者、触媒）之前用的是 art/p5/fx/v4/slash/ 里的刀光贴图，用户说「为什么要一直复用那些刀光」——
刀光是攻击的画面，增益牌要有自己的素材。这批贴图由代码负责动起来（生长、扫出、溶解、粒子），你只负责把图画好。

**颜色的用法很重要**：代码按贴图的**亮度**查 P5 红黑渐变上色（白 → 白芯，中亮 → 绯红，暗 → 深红 / 墨黑），所以每张图要像 slash_*.png 那样：
**亮白的芯 + 绯红（#E5191C）的边 + 少量墨黑的飞白**，明暗层次清楚。画风和 art/p5/fx/v4/slash/slash_thin.png、slash_cross.png 一致（手绘感、锐利、有细节），放在一起不违和。
**不要**：网点、平涂色块、粗黑描边的卡通造型、火焰、大团黑烟、文字。

## 【0. 绝对不要做】
- 不修改仓库里任何已有文件；只在 art/p5/fx/v4/buff/ 下新建文件，以及按第 4 步新建报告文件。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量 / PATH；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不删除任何目录或文件。
- 不进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 不碰游戏目录；不启动游戏，不关闭 / 结束任何进程。
- 被安全系统拒绝：不重试，停下来写进报告。遇到下面没写到的情况、任何一步报错：停下来，写进报告，推送报告，结束。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 claude/confident-hypatia-nropgi。

## 【1. 拉最新】
在 <repo> 里依次单独运行：
   git -c safe.directory=* -c http.version=HTTP/1.1 fetch origin claude/confident-hypatia-nropgi:refs/remotes/origin/claude/confident-hypatia-nropgi
   git -c safe.directory=* switch claude/confident-hypatia-nropgi
   git -c safe.directory=* merge --ff-only origin/claude/confident-hypatia-nropgi
fetch 超时按 5/10/20/40 秒重试；switch / merge 失败就停下来报告完整报错。

## 【2. 要画的】（全部真透明底 RGBA，轮廓外 alpha = 0，边缘柔和、无底色残留）
放到 art/p5/fx/v4/buff/。每张：交付版 PNG + 原始输出 *_raw.png + *_generation.json（提示词、尺寸）。

1. **荆棘藤 thorn_vine_01~03.png**（每张 2048×512，横向）：一根横着长的荆棘藤，主茎带白色高光、两侧是长短不一的尖刺（尖刺锐利、略弯），
   茎有自然的扭转和分叉，左端粗、右端细（代码会从左往右「长」出来）。三张形状不同：直一点的、弯成 S 的、带两三根小分叉的。
2. **荆棘爆开 thorn_burst.png**（1024×1024）：一丛荆棘尖刺从中心朝外刺出（十来根，长短错落，不是均匀的放射线），中心留空。
3. **镜面裂纹 mirror_crack_01~02.png**（每张 1024×1024）：玻璃被击中的裂纹——中心一个小碎点，细裂线朝外不规则地蔓延、分叉，外圈有一两道弧形裂纹；
   裂线是白色细线，裂口里有少量红色反光。两张图案不同。
4. **丝带 ribbon_01~03.png**（每张 2048×512）：一缕飘动的红色丝带 / 残光，像高速闪身后留在空中的一道轨迹，S 形或弧形，
   一端宽一端细，边缘亮白，表面有绸缎的反光褶皱。三张弧度不同。
5. **牌流 card_stream_01~02.png**（每张 2048×512）：一串被快速甩开的扑克牌 / 预告信，沿一条弧线排开，带运动模糊，
   头部几张清楚、尾部越来越虚，像洗牌时牌飞成一条线。牌的正面是红色（不要画文字和图案，只要牌形和反光）。
6. **毒液 venom_drops_sheet.png**（2048×2048，4×4 网格，每格 512，每格一个独立物体）：16 颗黑红色的毒液滴 / 飞溅的小液块，
   有光泽（白色高光点），形状各异：圆滴、拉长的滴、飞溅的小块、细丝。
7. **毒纹 venom_veins_01~02.png**（每张 1024×1024）：从中心朝外爬开的血管 / 树枝状纹路（毒在身体里扩散的样子），中心亮、末梢细而暗。两张不同。
8. **光点 buff_motes_sheet.png**（2048×2048，4×4 网格，每格 512）：16 种小的上飘光点 / 光屑（细长光点、小四角星、小菱形亮片），白芯红边，粒子用。

## 【3. 检查图和提交】
- 拼一张 check_buff.png（深灰底）和 check_buff_checker.png（棋盘格底，看透明边缘），把上面所有图摆在一起。
- 每张图只生成一次；只有生成报错、空图、画布尺寸不对、出现文字时重试一次。好不好看由 Claude 判断，不要自己挑毛病重画。
- 写 art/p5/fx/v4/buff/README.md：每张图一行（文件名、尺寸、一句话说明）。
- 只 add art/p5/fx/v4/buff；提交信息 "art: P5 Joker buff FX textures (thorns, mirror cracks, ribbons, card streams, venom, motes)"。
  总量不超过 80MB，单文件不超过 20MB。
- push：git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/confident-hypatia-nropgi；被拒先 fetch 再 merge --no-edit（不 rebase）再 push，超时按 5/10/20/40 秒重试。

## 【4. 报告】
新建 docs/codex/reports/codex_p5_buff_fx_art_report.md（中文）：每张图生成了几次、有没有被拒绝、检查图位置、提交号。和图一起提交推送。
