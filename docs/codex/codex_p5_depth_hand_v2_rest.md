你在用户的 Windows 电脑上工作。任务：以已确认的 depth_hand_v2\depth_grip.png 为基准，用 imagegen 生成同一只手的另外 3 个姿势，保存、提交推送。
不改代码、不编译、不启动游戏、不用 Computer Use。

【规则】
- 只在分支 claude/p5r-ren-slay-the-spire-mod-6tk3f3 上工作，副本目录 C:\Users\kkk\sts2_tools\p5_build\repo；不要使用其他分支，不要新建分支。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不要删除任何已有文件；不要改动 depth_grip.png。
- 不改全局 git config、系统环境变量 / PATH；不 rebase / reset / amend / stash；不 --force；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json、任何密码 / 密钥。
- 不进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 不要打开、参考、编辑 P3Makoto（结城理）mod 的任何图片。
- 每张最多生成 3 次，挑最符合「必须满足」的一张保存；被安全系统拒绝：不重试，停下来报告。
- 不要用脚本处理图片（本机没有 python 也不需要）。遇到没写到的情况：停下来报告。

【1. 拉最新】
在副本目录里依次单独运行：
   git -c safe.directory=* -c http.version=HTTP/1.1 pull --ff-only origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
   git -c safe.directory=* sparse-checkout add art/p5/fx/gen_codex docs/codex/reports

【2. 基准（最重要）】
把 repo\art\p5\fx\gen_codex\depth_hand_v2\depth_grip.png 作为参考图附给 imagegen，并在提示词里明确要求：
- 同一只手、同一个镜头、同一视角（手从画面中上方远处的深处伸过来，手腕很小、在画面上方中间偏左、后面拖着黑红烟丝往上后方散；手离镜头近、很大）。
- 手腕的位置、手的整体大小、手在画布里的位置和 depth_grip.png 完全一样（三张要能和它逐帧切换）。
- 同样是「侧握」的朝向：拇指在上、小指在下、手心朝左、手在画面右侧，手指朝左伸。
- 同样的画风：Persona 5 平面漫画风，纯黑的手、硬边，正红 #e3121c 的细锐高光，粗黑描边，细长尖锐像爪的手指。
- 正方形画布（和 depth_grip.png 同样大小），真透明背景（手和烟以外 alpha = 0），一只手，不要背景 / 地面 / 阴影 / 文字 / 血。
只改变手指的弯曲程度（下面每张的姿势）。

【3. 生成 3 张图】（文件名 → 姿势）
- depth_hand_v2\depth_open.png：手指完全张开 —— 拇指往上张开，四根手指伸直、往左张开成扇形、略向后翻，像正从深处猛扑过来、要去抓住画面中间的东西。
- depth_hand_v2\depth_close.png：手指正在收拢 —— 四根手指半弯，指尖开始往里扣，拇指开始往下压，介于 depth_open 和 depth_grip 之间。
- depth_hand_v2\depth_grip_tight.png：和 depth_grip.png 完全一样的握法和位置，但更用力：手指收得更紧、往里压，指节更尖更突出，红色高光更亮更多，
  手腕后面的烟被甩得更散（用来做「每结算一次毒就再用力捏一下」）。

英文提示词参考（每张都带上，并把最后一句换成对应姿势）：
"Use the attached image as the exact reference: SAME hand, SAME camera, SAME perspective (the forearm comes from deep inside the image near the
upper center, extreme foreshortening, tiny distant wrist dissolving into dark red smoke trailing up and back, huge near hand), SAME position and
size on the canvas, SAME sideways handshake orientation (thumb on top, palm facing left, hand on the right side, fingers pointing left), SAME
Persona 5 flat comic style (pure black demonic clawed hand, sharp red #e3121c highlights, thick black outline). Square transparent PNG,
one hand only, no background, no ground, no shadow, no text, no blood. Only the finger pose changes: <POSE>."
<POSE>：
- depth_open："fingers fully spread open like a fan pointing left, thumb opened upward, lunging forward to grab"
- depth_close："fingers half curled, starting to close inward, thumb starting to press down, between open and grip"
- depth_grip_tight："same grip as the reference but squeezing harder: fingers clenched tighter, knuckles sharper, brighter red highlights, smoke scattered more"

【4. 必须满足】（每张）
- 和 depth_grip.png 同一视角、同一手腕位置、同样大小（并排看能逐帧切换，不会跳）。
- 侧握朝向：拇指在上、手心朝左、手指朝左。
- 正方形、真透明背景、一只手。

【5. 提交推送】
在 repo\art\p5\fx\gen_codex\depth_hand_v2\README.md 末尾追加：三张各自的完整提示词、生成了几次、为什么选这一张、实际尺寸、第 4 步是否满足。
依次单独运行：
   git -c safe.directory=* add art/p5/fx/gen_codex/depth_hand_v2
   git -c safe.directory=* -c user.name=Codex -c user.email=codex@local commit -m "art: depth hand v2 open / close / grip_tight"
   git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
推送被拒：先运行 git -c safe.directory=* -c http.version=HTTP/1.1 pull --no-rebase --no-edit origin claude/p5r-ren-slay-the-spire-mod-6tk3f3，再推；有冲突就停下来报告。
最后在对话里回复：提交号 + 追加的 README 内容。
