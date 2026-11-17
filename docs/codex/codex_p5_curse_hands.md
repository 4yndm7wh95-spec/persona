你在用户的 Windows 电脑上工作。任务：用 imagegen 生成 4 张「咒怨之手」素材图（给 Persona 5 风格的诅咒技能特效用），清理、保存、提交推送。不改代码、不编译、不启动游戏、不用 Computer Use。

【规则】
- 只在分支 claude/p5r-ren-slay-the-spire-mod-6tk3f3 上工作，副本目录 C:\Users\kkk\sts2_tools\p5_build\repo；不要使用其他分支，不要新建分支。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不要删除任何已有文件。
- 不改全局 git config；不 rebase / reset / amend / stash；不 --force；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json、任何密码 / 密钥。
- 不要打开、参考、编辑 P3Makoto（结城理）mod 的任何图片。
- 每张图只生成一次（生成失败、空图、画布尺寸不对才重试一次）；被安全系统拒绝：不重试，停下来报告。不要自己挑毛病重画。
- 遇到没写到的情况：停下来报告。

【1. 拉最新】
在副本目录里依次单独运行：
   git -c safe.directory=* -c http.version=HTTP/1.1 pull --ff-only origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
   git -c safe.directory=* sparse-checkout add art/p5/fx/gen_codex docs/codex/reports
新建目录 repo\art\p5\fx\gen_codex\curse_hands\（已存在就直接用）。

【2. 生成 4 张图】
共同要求（每张都写进提示词）：
- 一只从下方地面伸出的「诅咒之手」，手腕往下逐渐变细、在画面底边截断（手腕底端要碰到画布底边，那里会被代码藏进地里）。
- 风格：Persona 5 的平面漫画风格。纯黑色的手（剪影感强），硬边，红色（#e3121c）的细锐高光勾在指节和手指边缘，少量暗红；粗黑描边。不要写实皮肤，不要 3D 渲染感，不要渐变柔光。
- 手指细长、指尖尖锐像爪，略带扭曲的诅咒感，但不要血腥、不要骨头、不要伤口。
- 真透明背景：PNG RGBA，手的轮廓外 alpha = 0，不要白底、绿幕、阴影、地面、火焰或任何其他东西。
- 画布 1024×1536（竖），手居中，手的高度占画布约 85%。
- 一张图只有一只手。

4 张的姿势（文件名 → 姿势）：
- hand_reach.png：五指张开、掌心朝前，往上伸直抓向天空。
- hand_slam.png：手腕往前弯，掌心朝下、五指并拢微张，做出从上往下拍击的姿势（手掌在画面上方，像要往右下方拍下去）。
- hand_claw.png：手指弯曲成爪状，指尖朝上，像要抓住什么。
- hand_grab.png：手指半握，像正在攥紧，带一点扭曲张力。

【3. 清理和检查】
每张图：
1. 轮廓外残留的半透明杂点（alpha < 20 的孤立像素）清成 alpha 0。
2. 检查并写进报告：画布尺寸、轮廓外 alpha 是否为 0、边缘有没有白边或底色残留、手腕是否碰到底边、是否只有一只手。
3. 另存一张 contact_sheet.png：4 张图并排放在深灰底（#202028）上，下面写文件名。

【4. 提交推送】
在 repo\art\p5\fx\gen_codex\curse_hands\ 里写 README.md（中文）：每张图的完整提示词、尺寸、第 3 步的检查结果。
然后依次单独运行：
   git -c safe.directory=* add art/p5/fx/gen_codex/curse_hands
   git -c safe.directory=* -c user.name=Codex -c user.email=codex@local commit -m "art: P5 curse hands for the Eiha effect (4 poses)"
   git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
推送被拒：先运行 git -c safe.directory=* -c http.version=HTTP/1.1 pull --no-rebase --no-edit origin claude/p5r-ren-slay-the-spire-mod-6tk3f3，再推；有冲突就停下来报告。
最后在对话里回复：提交号 + README 全文。
