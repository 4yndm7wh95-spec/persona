你在用户的 Windows 电脑上工作。任务：用 imagegen 先只生成 1 张「从屏幕深处伸出来握住东西的手」，保存、提交推送，等用户确认后再生成其余几张。
不改代码、不编译、不启动游戏、不用 Computer Use。

【规则】
- 只在分支 claude/p5r-ren-slay-the-spire-mod-6tk3f3 上工作，副本目录 C:\Users\kkk\sts2_tools\p5_build\repo；不要使用其他分支，不要新建分支。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不要删除任何已有文件。
- 不改全局 git config、系统环境变量 / PATH；不 rebase / reset / amend / stash；不 --force；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json、任何密码 / 密钥。
- 不进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 不要打开、参考、编辑 P3Makoto（结城理）mod 的任何图片。
- 允许最多生成 3 次，挑一张最符合下面「必须满足」的保存；被安全系统拒绝：不重试，停下来报告。
- 不要用脚本处理图片（本机没有 python 也不需要）。遇到没写到的情况：停下来报告。

【1. 拉最新】
在副本目录里依次单独运行：
   git -c safe.directory=* -c http.version=HTTP/1.1 pull --ff-only origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
   git -c safe.directory=* sparse-checkout add art/p5/fx/gen_codex docs/codex/reports
新建目录 repo\art\p5\fx\gen_codex\depth_hand_v2\（已存在就直接用）。

【2. 画风】
打开 repo\art\p5\fx\gen_codex\grip_hand\grip_fist.png 看一眼，同一种手：Persona 5 平面漫画风，纯黑的手、硬边，正红 #e3121c 的细锐高光勾在指节和
手指边缘，粗黑描边，细长尖锐像爪的手指，手腕处化成黑红色的烟丝。

【3. 画面（写进提示词，用英文写提示词效果更好，下面附了英文版）】
想象镜头站在一个被抓住的敌人正前方。敌人身后的画面深处伸出一只巨大的手，从敌人背后绕过来把它握住。
所以这张图的透视是：手臂从画面正中间的远处（很小）笔直地朝镜头伸过来 —— 前臂几乎是「正对镜头」的，被强烈地透视缩短成很短的一截，
手腕在画面中心偏上、在远处，后面化成烟丝往远处散；手离镜头很近，很大。
握姿是侧握（像握手、握住一个竖着的玻璃杯）：大拇指在最上面，从上方压过来；四根手指一根叠一根，从画面右侧绕过来，横着扣在被握住的东西的正面，
指尖指向左边；手心朝左（贴着被握住东西的右后方）。被握住的东西（一个竖着的圆柱，宽约画布的 35%，在画布正中间）不要画出来，那里是透明的。

英文提示词参考（可以直接用、可以补充）：
"Persona 5 style flat comic illustration of a giant black demonic clawed hand with sharp red highlights and thick black outline,
reaching OUT OF THE SCREEN toward the camera from far behind: the forearm comes straight from the deep background at the center and is
heavily foreshortened (extreme perspective, the wrist is small and far away, dissolving into dark red smoke trails behind it), the hand is huge
and close to the camera. The hand is turned sideways like a handshake: thumb on top pressing down, the four curled fingers stacked vertically
wrapping horizontally from the right across the front of an invisible vertical cylinder in the center, fingertips pointing left, palm facing left.
The cylinder itself is not drawn (transparent). Transparent background, PNG, no ground, no shadow, no text, no blood."

【4. 必须满足】
- 透视：手臂是从画面深处朝镜头伸出来的（手腕在中间远处、明显变小），不是从画面左边或右边横着伸进来的。
- 握姿：拇指在上、四指从右往左横着扣在正面、手心朝左。
- 正方形画布，真透明背景（手和烟以外 alpha = 0），一只手。
文件名：depth_hand_v2\depth_grip.png。

【5. 提交推送】
写 repo\art\p5\fx\gen_codex\depth_hand_v2\README.md（中文）：用的提示词、生成了几次、为什么选这一张、实际尺寸、第 4 步三条是否满足。
依次单独运行：
   git -c safe.directory=* add art/p5/fx/gen_codex/depth_hand_v2
   git -c safe.directory=* -c user.name=Codex -c user.email=codex@local commit -m "art: depth hand v2 grip (single frame for review)"
   git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
推送被拒：先运行 git -c safe.directory=* -c http.version=HTTP/1.1 pull --no-rebase --no-edit origin claude/p5r-ren-slay-the-spire-mod-6tk3f3，再推；有冲突就停下来报告。
最后在对话里回复：提交号 + README 全文。
