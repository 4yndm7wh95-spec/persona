你在用户的 Windows 电脑上工作。任务：用 imagegen 重画杀戮尖塔 2 mod「P3Makoto」里建筑师房间（最终房间）的 P3R 风格背景（只做美术，不改代码）。

【背景】
P3Makoto 把储君换成 P3R 的结城理，最终房间（和建筑师对话 → 「伟大封印」结局）用 P3R 塔尔塔洛斯塔顶做背景。Claude 在云端写代码，你在本地做美术并推送到 GitHub 仓库 4yndm7wh95-spec/persona 的分支 claude/amazing-darwin-b00arn。
现在的背景是 <repo>\art\v13\tartarus_top_b.png。用户反馈两点：① 人物站的位置和背景对不上（游戏里人物脚底在屏高约 66% 处，现在的图那个高度是楼梯和墙，人物像站在墙上）；② 细节太少，显得廉价。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（C:\Users\kkk\ 下、桌面、Documents，或 Get-ChildItem -Recurse -Filter P3Makoto.json 后用 git remote -v 确认），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
先 git status：有未提交改动就停下报告。然后 checkout claude/amazing-darwin-b00arn，
git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次），git merge --ff-only origin/claude/amazing-darwin-b00arn（不能快进就 git merge --no-edit origin/claude/amazing-darwin-b00arn，不要 rebase）。

【要画的图】输出到 <repo>\art\v14\，每个文件出 2 个版本 _a / _b，原样提交：
1. tartarus_top_v2_a.png / _b.png —— 2048×1152（16:9），完整背景，不要绿幕，不要人物、不要文字 / 水印。
   参考 <repo>\art\v13\tartarus_top_b.png 和 <repo>\art\v13\tartarus_top_a.png 的画风和配色（日系动画背景、影时间的墨绿色调、漆黑带绿的天空），但**构图严格按下面**：
   - 地面线：塔顶圆形平台的地面占画面下方 45%：从 y≈620（屏高 54%）往下全是平坦、可以站人的石板地面。
   - 站人区：y 在 700~800（屏高 61%~70%）、x 在 300~1850 这一整条横带里，**必须是平坦开阔的地面**，不能有楼梯、墙、柱子、大石块或任何竖起来的物体（游戏里左边站结城理，右边站建筑师）。
   - 月亮：一轮巨大的淡绿色满月，**完整地**出现在画面上方，圆心约 (1250, 260)，半径约 200，月面有清楚的环形山纹理，周围一圈绿色月晕。
   - 远景（y 300~620）：平台远处的边缘一圈高大的棺材形石碑和断裂的石柱，更远处是塔尔塔洛斯层层叠叠的尖塔剪影和翻涌的墨绿色云海。
   - 地面细节：石板上刻着巨大的圆形魔法阵纹路（同心圆 + 放射线 + 符文），裂缝、积水的水洼映出月光，零星碎石、断掉的锁链。
   - 前景：只允许在左下角和右下角（x<200 或 x>1850、y>900）放少量碎石 / 断柱遮挡，增加纵深。
   - 光照：月光从右上照下，地面有冷绿色反光和长影；整体偏暗但细节清楚，不要糊成一片。
2. tartarus_top_v2_fg_a.png / _b.png —— 和上面同尺寸的透明 PNG：只保留第 1 张图里左下 / 右下角的前景碎石（从对应版本的图里抠出来，边缘干净），其余全透明。游戏会把它叠在人物前面做纵深。
3. preview_v2.png：把两版背景缩到 960×540 并排，在每张上画两条参考线（y=620 地面线、y=740 站人线，红色）和两个 120×300 的人形框（中心 x=500 和 x=1450，脚底在 y=740），用来检查站位。

【自检】站人线附近必须是平地；月亮完整在画面内；不满足就重画（最多 3 轮），仍不满足就在报告里如实说明。

【提交与推送】
- 只 git add art\v14\ 下的文件。提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: v14 Tartarus rooftop v2 background (characters stand on the platform)"
- push：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试）。
  被拒绝是因为别的会话先推送了：先 fetch，再 git merge --no-edit origin/claude/amazing-darwin-b00arn（合并提交，不要 rebase），然后再 push。
- 完成后用中文报告做了什么、结果在哪、提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要改写 git 历史（rebase / reset / amend）；不要用 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只读）；导出 / 解包的中间文件放到 C:\Users\kkk\sts2_tools\ 下。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 遇到上面没写到的情况，停下来报告，不要自作主张。
