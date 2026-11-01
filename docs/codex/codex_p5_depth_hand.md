你在用户的 Windows 电脑上工作。任务：用 imagegen 生成一套「从画面深处伸出来抓人的手」姿势图（4 张），保存、提交推送。不改代码、不编译、不启动游戏、不用 Computer Use。

【规则】
- 只在分支 claude/p5r-ren-slay-the-spire-mod-6tk3f3 上工作，副本目录 C:\Users\kkk\sts2_tools\p5_build\repo；不要使用其他分支，不要新建分支。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不要删除任何已有文件。
- 不改全局 git config、系统环境变量 / PATH；不 rebase / reset / amend / stash；不 --force；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json、任何密码 / 密钥。
- 不进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 不要打开、参考、编辑 P3Makoto（结城理）mod 的任何图片。
- 每张图只生成一次（生成失败、空图、背景不透明、方向不对才重试一次）；被安全系统拒绝：不重试，停下来报告。不要自己挑毛病重画。
- 尺寸不要求精确，比例对就行。不要用脚本处理图片（本机没有 python 也不需要）。
- 遇到没写到的情况：停下来报告。

【1. 拉最新】
在副本目录里依次单独运行：
   git -c safe.directory=* -c http.version=HTTP/1.1 pull --ff-only origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
   git -c safe.directory=* sparse-checkout add art/p5/fx/gen_codex docs/codex/reports
新建目录 repo\art\p5\fx\gen_codex\depth_hand\（已存在就直接用）。

【2. 风格参考】
打开 repo\art\p5\fx\gen_codex\grip_hand\grip_reach.png 看一眼：同一种手（Persona 5 平面漫画风，纯黑的手、硬边，正红 #e3121c 的细锐高光勾在指节和
手指边缘，少量暗红，粗黑描边，细长尖锐像爪的手指，手腕处化成黑红色的烟丝）。

【3. 这一轮的视角（最重要）】
这只手是从画面深处（屏幕里面）朝着观众伸出来、抓住站在它前面的一个敌人。所以是「正面透视」：
- 我们正对着这只手的掌心一侧，手指朝向观众（朝镜头）伸过来，有明显的透视缩短（手指近大、手腕远小）。
- 手腕在画面中心偏下、比手小很多（在远处），往画面后方化成烟丝，烟丝往上方飘散、逐渐透明。
- 不要侧面视角，不要从画面边缘伸进来。

【4. 生成 4 张图】
共同要求（每张都写进提示词）：
- 画布正方形。手（不含烟）约占画布的 75%，手掌中心在画布正中间。4 张的手大小、位置一致（同一只手的连续动作，要能逐帧切换）。
- 真透明背景：PNG RGBA，手和烟以外 alpha = 0；不要白底、绿幕、阴影、地面、火焰或任何其他东西。一张图只有一只手。
- 不要血腥、不要骨头、不要伤口、不要文字。

4 张的姿势（文件名 → 姿势）：
- depth_open.png：五指完全张开、往外撑，像要扑过来抓住镜头前的东西，掌心正对观众，五根手指尖朝四周和观众伸出。
- depth_close.png：手指正在往里收，一半弯曲，像正要合拢抓住一个很大的球。
- depth_grip.png：紧紧抓住一个看不见的大球（大约占手心那么大）：四根手指从上方弯过来扣在球的正面（我们能看到四根手指弯曲的背面和指节，横在画面中间偏上），
  拇指从左下方扣在球的正面，手掌在球的后面（只露出手掌边缘）。球本身不要画，球所在的地方是透明的（看得见手掌的边缘，但中间被球挡住的部分不画）。
  手指非常用力，指节突起，红色高光明显。
- depth_grip_tight.png：和 depth_grip 一样的抓法，但更用力：手指收得更紧、往里压，指节更尖，红色高光更亮，手腕的烟被甩得更散。

【5. 检查（只用眼睛看）】
每张写进报告：实际尺寸、背景是否透明、是不是正面透视（手指朝观众、手腕在远处变小）、4 张手的大小位置是否一致、depth_grip 中间被球挡住的部分是否透明。

【6. 提交推送】
在 repo\art\p5\fx\gen_codex\depth_hand\ 里写 README.md（中文）：每张图的完整提示词、实际尺寸、第 5 步的检查结果。
依次单独运行：
   git -c safe.directory=* add art/p5/fx/gen_codex/depth_hand
   git -c safe.directory=* -c user.name=Codex -c user.email=codex@local commit -m "art: P5 depth hand poses (open / close / grip / grip_tight)"
   git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
推送被拒：先运行 git -c safe.directory=* -c http.version=HTTP/1.1 pull --no-rebase --no-edit origin claude/p5r-ren-slay-the-spire-mod-6tk3f3，再推；有冲突就停下来报告。
最后在对话里回复：提交号 + README 全文。
