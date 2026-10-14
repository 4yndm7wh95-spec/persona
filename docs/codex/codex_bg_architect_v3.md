你在用户的 Windows 电脑上工作。任务：用 imagegen 重画杀戮尖塔 2 mod「P3Makoto」建筑师房间（最终房间）的塔顶背景，**修正透视**（只做美术，不改代码）。

【背景】
P3Makoto 把储君换成 P3R 的结城理，最终房间用 P3R 塔尔塔洛斯塔顶做背景。现在用的是 <repo>\mod\pck_stage\p3makoto\fx\tartarus_top_v2.png（1672×941）。
用户反馈：人物不像真的站在这块地板上。原因是透视错了：
- 这张图的地平线（平台远端那一排棺材石碑脚下）在屏高约 55%，而游戏里人物脚底在屏高约 64%，离地平线太近 —— 等于人物站在很远的平台边缘，可人物的身高却是远处棺材的 3 倍，近大远小对不上。
- 正确的透视：镜头大约在人的胸口 / 眼睛高度，**地平线应该和人物的眼睛差不多齐平（屏高 33%~38%）**，远处的石碑、断柱要比人小得多，人物脚下是近处大片的石板地面。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（C:\Users\kkk\ 下、桌面、Documents；可能有多个副本，用你上次成功推送的那个），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
先 git status：有未提交改动就停下报告。然后 checkout claude/amazing-darwin-b00arn，
git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次），git merge --ff-only origin/claude/amazing-darwin-b00arn（不能快进就 git merge --no-edit origin/claude/amazing-darwin-b00arn，不要 rebase）。
如果仓库是稀疏检出、需要的参考文件不在磁盘上：允许执行 git -c safe.directory=<repo> sparse-checkout add <需要的目录>；路径过长报错时允许单次命令加 -c core.longpaths=true。不要 sparse-checkout disable / set。

【参考】
- 画风、配色、元素照 <repo>\mod\pck_stage\p3makoto\fx\tartarus_top_v2.png：日系动画背景、影时间的墨绿色调、巨大的淡绿满月、翻涌的墨绿云海、棺材形石碑、断裂的哥特尖塔、刻着圆形魔法阵的湿石板地面。这些都保留，只改构图和透视。
- 人物尺寸参考：游戏里人物身高约为屏高的 35%（2048×1152 画布上约 400px），脚底在 y≈740。

【要画的图】输出到 <repo>\art\v16\bg\，每个文件出 2 个版本 _a / _b：
1. tartarus_top_v3_a.png / _b.png —— 2048×1152（16:9），完整不透明背景，不要人物、文字、水印。构图严格按下面：
   - **地平线（平台远端边缘和远景的交界）在 y≈400~440**（屏高 35%~38%）。地平线以下全是塔顶平台的石板地面，一直铺到画面底部。
   - **站人区**：y 在 650~850、x 在 250~1850 这一整条横带里必须是平坦开阔的地面，不能有任何竖起来的物体。游戏里左边站结城理（脚底约 (500, 740)），右边站建筑师（约 (1450, 740)）。
   - **远景尺寸**：平台远端边缘的棺材形石碑、断柱高度不超过 110px（它们离镜头很远）；更远处的塔尔塔洛斯尖塔剪影可以高一些，但底部都在地平线上。
   - **地面透视**：石板上的大圆形魔法阵按透视压扁成椭圆，椭圆中心约 (1024, 820)，横向约 1500px、纵向约 320px（压扁比例约 0.2，对应镜头在胸口高度往前看）；石板接缝从地平线上的一个消失点（约 (1024, 420)）向画面下方放射展开；近处的石板块比远处的大得多。
   - **月亮**：巨大的淡绿色满月，完整出现在地平线上方，圆心约 (1100, 210)，半径约 160，月面有环形山纹理，周围一圈绿色月晕。
   - **光照**：月光从画面中上方、人物身后照过来。湿石板上从地平线到画面下方有一道竖长的月光倒影（在 x≈1100 附近），站人区的地面有冷绿色反光。整体偏暗，但地面细节清楚。
   - **前景**：只在左下角和右下角（x<220 或 x>1830、y>920）放少量碎石 / 断链 / 断柱遮挡，增加纵深。
2. tartarus_top_v3_fg_a.png / _b.png —— 同尺寸透明 PNG：只保留第 1 张图左下 / 右下角的前景碎石（从对应版本里抠出来，边缘干净，**轮廓外 alpha=0 的真透明**），其余全透明。
3. preview_v3.png：两版背景缩到 960×540 并排。每张上画：地平线参考线（y=420，红色）、站人线（y=740，红色）；两个 140×400 的人形框（中心 x=500 和 x=1450，脚底在 y=740，框里写"人"）。用来检查人和远景的大小关系。

【自检】
1. 地平线在 y 400~440；站人区平坦无遮挡；月亮完整在画面内。
2. 透视：把 400px 高的人形框放在站人线上，人比远处的石碑高 3 倍以上；人形框的眼睛位置（脚底往上约 370px，y≈370）和地平线差不多齐平。
3. 魔法阵椭圆和石板接缝都往同一个消失点收。
不满足就重画（每个版本最多 4 轮），仍不满足就在报告里如实说明，把最接近的一张也提交（文件名加 _wip）。

【提交与推送】
- 只 git add art\v16\bg\ 下的文件。提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: v16 Tartarus rooftop v3 background (eye-level horizon, correct perspective)"
- push：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试）。
  被拒绝是因为别的会话先推送了：先 fetch，再 git merge --no-edit origin/claude/amazing-darwin-b00arn（不要 rebase），然后再 push。
  如果单个文件太大导致推送反复中断（网络在 1.7MB 左右断开），停下来报告，不要自己改用别的办法。
- 完成后用中文报告做了什么、结果在哪、提交号、每张图实际的地平线 y 值和月亮圆心 / 半径。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要改写 git 历史（rebase / reset / amend）；不要用 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只读）。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换一种中性的描述重试一次，仍被拒就停下报告。
- 不要和编译会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告，不要自作主张。
