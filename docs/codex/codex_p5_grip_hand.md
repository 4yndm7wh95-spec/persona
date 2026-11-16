你在用户的 Windows 电脑上工作。任务：用 imagegen 生成一套「人格面具之手」抓握分阶段姿势图（5 张），保存、提交推送。不改代码、不编译、不启动游戏、不用 Computer Use。

【规则】
- 只在分支 claude/p5r-ren-slay-the-spire-mod-6tk3f3 上工作，副本目录 C:\Users\kkk\sts2_tools\p5_build\repo；不要使用其他分支，不要新建分支。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不要删除任何已有文件。
- 不改全局 git config、系统环境变量 / PATH；不 rebase / reset / amend / stash；不 --force；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json、任何密码 / 密钥。
- 不进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 不要打开、参考、编辑 P3Makoto（结城理）mod 的任何图片。
- 每张图只生成一次（生成失败、空图、背景不透明、方向不对才重试一次）；被安全系统拒绝：不重试，停下来报告。不要自己挑毛病重画。
- 尺寸不要求精确：横 / 竖和大致比例对就行。不要用脚本缩放、裁剪、处理图片（本机没有 python 也不需要）。
- 遇到没写到的情况：停下来报告。

【1. 拉最新】
在副本目录里依次单独运行：
   git -c safe.directory=* -c http.version=HTTP/1.1 pull --ff-only origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
   git -c safe.directory=* sparse-checkout add art/p5/fx/gen_codex docs/codex/reports
新建目录 repo\art\p5\fx\gen_codex\grip_hand\（已存在就直接用）。

【2. 风格参考】
打开 repo\art\p5\fx\gen_codex\curse_hands_v2\hand_open.png 看一眼：这一轮的 5 张要和它是同一种手（Persona 5 平面漫画风，纯黑的手、硬边，
正红 #e3121c 的细锐高光勾在指节和手指边缘，少量暗红，粗黑描边，细长尖锐像爪的手指）。但这次没有前臂：只画到手腕，
手腕那一端要「化成烟」—— 手腕往左逐渐碎成几缕黑红色的烟丝 / 碎片，渐渐透明消失（像从迷雾里伸出来的手）。

【3. 生成 5 张图】
共同要求（每张都写进提示词）：
- 画布横幅（宽约为高的 1.3 倍）。手从画面左边伸过来、指向右边（右边是被抓的敌人）：手腕（化成烟的那一端）在画面左侧中间，手指朝右。
- 5 张的手的大小、手腕位置必须一致（同一只手的连续动作，要能逐帧切换）：手（不含烟）约占画布宽度的 60%，手腕在画布左边 25% 处、垂直居中。
- 视角：侧面看（3/4），能看到手指弯曲的轮廓。
- 真透明背景：PNG RGBA，手和烟以外 alpha = 0；不要白底、绿幕、阴影、地面、火焰或任何其他东西。一张图只有一只手。
- 不要血腥、不要骨头、不要伤口、不要文字。

5 张的姿势（文件名 → 姿势）：
- grip_reach.png：五指完全张开、伸直、略向后翻，像猛扑过去的瞬间，掌心朝右（朝敌人）。
- grip_close.png：手指正在往里收，一半弯曲，像要抓住一个很大的东西。
- grip_hold.png：整个手攥成拳一样紧紧握住（握着一个看不见的大东西，所以拳心是空的、像握着一个球），指尖深深扣进去，指节突起，非常用力。
- grip_squeeze.png：和 grip_hold 一样的握法，但更用力地一捏：手指更收紧、指节更尖、手背的筋和红色高光更明显，整只手略微向内收缩。
- grip_release.png：手指松开、微微张开，手腕那端的烟变多，像要散掉。

【4. 检查（只用眼睛看）】
每张写进报告：实际尺寸、背景是否透明、手腕是否在左侧中间并化成烟、手指是否朝右、5 张手的大小和手腕位置是否大致一致。

【5. 提交推送】
在 repo\art\p5\fx\gen_codex\grip_hand\ 里写 README.md（中文）：每张图的完整提示词、实际尺寸、第 4 步的检查结果。
依次单独运行：
   git -c safe.directory=* add art/p5/fx/gen_codex/grip_hand
   git -c safe.directory=* -c user.name=Codex -c user.email=codex@local commit -m "art: P5 persona grip hand poses (reach / close / hold / squeeze / release)"
   git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
推送被拒：先运行 git -c safe.directory=* -c http.version=HTTP/1.1 pull --no-rebase --no-edit origin claude/p5r-ren-slay-the-spire-mod-6tk3f3，再推；有冲突就停下来报告。
最后在对话里回复：提交号 + README 全文。
