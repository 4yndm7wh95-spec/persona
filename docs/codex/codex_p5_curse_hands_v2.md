你在用户的 Windows 电脑上工作。任务：用 imagegen 生成一套「咒怨之手」分阶段姿势图（5 张），保存、提交推送。不改代码、不编译、不启动游戏、不用 Computer Use。

【规则】
- 只在分支 claude/p5r-ren-slay-the-spire-mod-6tk3f3 上工作，副本目录 C:\Users\kkk\sts2_tools\p5_build\repo；不要使用其他分支，不要新建分支。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不要删除任何已有文件。
- 不改全局 git config、系统环境变量 / PATH；不 rebase / reset / amend / stash；不 --force；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json、任何密码 / 密钥。
- 不进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 不要打开、参考、编辑 P3Makoto（结城理）mod 的任何图片。
- 每张图只生成一次（生成失败、空图、背景不透明、手腕没碰到底边才重试一次）；被安全系统拒绝：不重试，停下来报告。不要自己挑毛病重画。
- 尺寸不要求精确：横 / 竖和大致比例对就行。不要用脚本缩放、裁剪、处理图片（本机没有 python 也不需要）。
- 遇到没写到的情况：停下来报告。

【1. 拉最新】
在副本目录里依次单独运行：
   git -c safe.directory=* -c http.version=HTTP/1.1 pull --ff-only origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
   git -c safe.directory=* sparse-checkout add art/p5/fx/gen_codex docs/codex/reports
新建目录 repo\art\p5\fx\gen_codex\curse_hands_v2\（已存在就直接用）。

【2. 风格参考】
打开 repo\art\p5\fx\gen_codex\curse_hands\hand_reach.png 看一眼（这是上一轮你画的手），这一轮的 5 张要和它是「同一只手」：
同样的画风（Persona 5 平面漫画风，纯黑的手、硬边，正红 #e3121c 的细锐高光勾在指节和手指边缘，少量暗红，粗黑描边），
同样细长尖锐像爪的手指，同样粗细的手腕和前臂。

【3. 生成 5 张图】
共同要求（每张都写进提示词）：
- 画布竖幅（高约为宽的 1.5 倍）。一只从地面伸出的手，前臂竖直往下、在画布底边截断：手腕 / 前臂的底端必须碰到画布底边，且在底边水平居中。
- 5 张里前臂的粗细、手的大小必须一致（同一只手的不同动作，要能逐帧切换）：前臂底端宽度约为画布宽度的 22%，手（不含前臂）约占画布高度的 45%。
- 画面方向：手的掌心朝向画面右侧（+x 方向，那里是敌人），我们看到的是手的侧面偏掌心一侧（3/4 角度），能看到掌心和手指内侧，不能是手背。
- 真透明背景：PNG RGBA，手的轮廓外 alpha = 0；不要白底、绿幕、阴影、地面、火焰或任何其他东西。一张图只有一只手。
- 不要血腥、不要骨头、不要伤口、不要文字。

5 张的姿势（文件名 → 姿势，对应特效里的阶段）：
- hand_bud.png（从地里长出来的第一阶段）：手指全部向掌心紧紧蜷起，握成一个尖尖的「花苞」，指尖朝上聚拢，手腕笔直。
- hand_unfurl.png（长出来的第二阶段）：手指正在张开，一半弯曲一半伸展，像爪子在舒展，掌心朝右。
- hand_open.png（蓄势）：五指完全张开、伸直、略向后弯，掌心正对右侧，手腕微微往后仰，像要扑过去。
- hand_slap.png（拍下去）：手腕向右前方弯折约 60°，五指并拢伸直，整个手掌朝右下方拍出去，掌心朝右下（看得到掌心），带一点速度感的手形（但不要画速度线）。
- hand_clench.png（缩回地里）：五指收拢握成拳、指尖扣进掌心，手腕微微向下垂，像在往回缩。

【4. 检查（只用眼睛看）】
每张写进报告：实际尺寸、背景是否透明、手腕是否碰到底边且居中、是否只有一只手、掌心是否朝右（看得到掌心而不是手背）、5 张的前臂粗细和手的大小是否大致一致。

【5. 提交推送】
在 repo\art\p5\fx\gen_codex\curse_hands_v2\ 里写 README.md（中文）：每张图的完整提示词、实际尺寸、第 4 步的检查结果。
依次单独运行：
   git -c safe.directory=* add art/p5/fx/gen_codex/curse_hands_v2
   git -c safe.directory=* -c user.name=Codex -c user.email=codex@local commit -m "art: P5 curse hand poses (bud / unfurl / open / slap / clench)"
   git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
推送被拒：先运行 git -c safe.directory=* -c http.version=HTTP/1.1 pull --no-rebase --no-edit origin claude/p5r-ren-slay-the-spire-mod-6tk3f3，再推；有冲突就停下来报告。
最后在对话里回复：提交号 + README 全文。
