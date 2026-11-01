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
上一轮如果已经生成过 depth_hand 下的图（正握的版本），不要删除它们，这一轮生成的同名图直接覆盖（Copy-Item 覆盖即可）。

【2. 风格参考】
打开 repo\art\p5\fx\gen_codex\grip_hand\grip_reach.png 看一眼：同一种手（Persona 5 平面漫画风，纯黑的手、硬边，正红 #e3121c 的细锐高光勾在指节和
手指边缘，少量暗红，粗黑描边，细长尖锐像爪的手指，手腕处化成黑红色的烟丝）。

【3. 这一轮的视角（最重要）】
这只手是从画面深处（屏幕里面、敌人的后方）斜着伸出来，从侧面一把握住站在它前面的敌人。握法是「侧握」（像 grip_hand 那一套一样从侧面握，
不是掌心正对观众的正握）：
- 3/4 侧视：手从画面左后方的深处斜着朝右前方伸过来，我们看到的是手的侧面偏手背一侧，手指朝右弯过去。
- 透视缩短：手腕在左上后方的远处，明显比手小、更暗一点，往画面后方（左上）化成黑红色的烟丝、逐渐透明；越往指尖越近、越大。
- 不要掌心正对观众，不要从画面边缘水平伸进来。

【4. 生成 4 张图】
共同要求（每张都写进提示词）：
- 画布正方形。手（不含烟）约占画布的 75%，手掌中心在画布正中间稍偏左。4 张的手大小、位置、手腕位置一致（同一只手的连续动作，要能逐帧切换）。
- 真透明背景：PNG RGBA，手和烟以外 alpha = 0；不要白底、绿幕、阴影、地面、火焰或任何其他东西。一张图只有一只手。
- 不要血腥、不要骨头、不要伤口、不要文字。

4 张的姿势（文件名 → 姿势）：
- depth_open.png：侧面看，五指完全张开、往右前方猛扑过去，手指尖朝右前方，手腕在左上后方的远处。
- depth_close.png：同样的角度，手指正在往里收、一半弯曲，像正要从侧面合拢握住一个很大的球。
- depth_grip.png：从侧面紧紧握住一个看不见的大球（球大约在画布正中偏右，占画布宽度约 40%）：手掌和手背在球的左侧后方，
  四根手指从上方绕过球、弯下来扣在球的右前侧（我们能看到四根手指弯曲的背面和指节，从球的上方跨过球的正面），拇指从下方扣在球的正面。
  球本身不要画，球所在、被球挡住的地方是透明的（手指在球前面的部分要完整画出来，手掌在球后面被挡住的部分不画）。
  手指非常用力，指节突起，红色高光明显。
- depth_grip_tight.png：和 depth_grip 完全一样的握法和位置，但更用力：手指收得更紧、往里压，指节更尖，红色高光更亮，手腕的烟被甩得更散。

【5. 检查（只用眼睛看）】
每张写进报告：实际尺寸、背景是否透明、是不是 3/4 侧视的侧握（不是正握）、手腕是否在左上后方的远处变小、4 张手的大小位置是否一致、depth_grip 被球挡住的部分是否透明。

【6. 提交推送】
在 repo\art\p5\fx\gen_codex\depth_hand\ 里写 README.md（中文）：每张图的完整提示词、实际尺寸、第 5 步的检查结果。
依次单独运行：
   git -c safe.directory=* add art/p5/fx/gen_codex/depth_hand
   git -c safe.directory=* -c user.name=Codex -c user.email=codex@local commit -m "art: P5 depth hand poses (open / close / grip / grip_tight)"
   git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
推送被拒：先运行 git -c safe.directory=* -c http.version=HTTP/1.1 pull --no-rebase --no-edit origin claude/p5r-ren-slay-the-spire-mod-6tk3f3，再推；有冲突就停下来报告。
最后在对话里回复：提交号 + README 全文。
