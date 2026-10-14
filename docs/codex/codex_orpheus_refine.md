你在用户的 Windows 电脑上工作。任务：精修 Orpheus（俄耳甫斯）的分层零件，让腿部也能做小幅摆动（只做美术，不改代码）。

【背景】
P3Makoto 是杀戮尖塔 2 的 mod。你上次拆出的 Orpheus 零件在 <repo>\art\persona_parts\orpheus\（README.md 里写了已知问题）。Claude 已经把它接进游戏做骨骼动画，但两条长腿交叉的地方被遮挡的部分没有补全，模拟时只要转 0.01 弧度就露出切边，所以腿现在完全不动。竖琴左框、肩 / 肘 / 髋 / 膝附近也有碎边。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（C:\Users\kkk\ 下、桌面、Documents；可能有多个副本，用你上次成功推送的那个），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
先 git status：有未提交改动就停下报告。然后 checkout claude/amazing-darwin-b00arn，
git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次），git merge --ff-only origin/claude/amazing-darwin-b00arn（不能快进就 git merge --no-edit origin/claude/amazing-darwin-b00arn，不要 rebase）。
如果仓库是稀疏检出、需要的参考文件不在磁盘上：允许执行 git -c safe.directory=<repo> sparse-checkout add <需要的目录>（只是把已有文件检出到本地）；路径过长报错时允许单次命令加 -c core.longpaths=true。不要 sparse-checkout disable / set。

【要做的】在原目录里**原地更新**这些零件（文件名、画布 386×858、位置、rig.json 的关节点都不变）：
1. 02_thigh_left、03_shin_left、04_thigh_right、05_shin_right：把被另一条腿或身体挡住的部分补画完整——两根长圆柱小腿交叉处、膝关节、髋关节。补画要和原渲染的材质（白银色金属、蓝色渐变）、光照一致。目标：每个腿零件绕自己的关节点转 ±10° 时没有透明的洞、没有切边，关节处上下零件重叠自然。
2. 01_lyre：补全被躯干挡住的竖琴左侧框架和下框，琴弦连续。
3. 08~11 手臂：清理肩、肘处的碎边。
可以用 imagegen 的编辑 / 局部重绘，也可以手工克隆修补；只改被遮挡、看不见的区域，静态叠回的效果要和原来一样（叠回差异图仍接近全黑）。
4. 重新生成 preview_stack.png / preview_parts.png / preview_swing.png（这次 swing 用 ±10°），并在 README.md 末尾追加「精修记录」：改了哪些零件、怎么补的、±10° 是否通过。

【提交与推送】
- 只 git add art\persona_parts\orpheus\ 下的文件。提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: Orpheus rig parts refined (legs and lyre inpainted)"
- push：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试）。被拒绝是因为别的会话先推送了：先 fetch，再 git merge --no-edit origin/claude/amazing-darwin-b00arn（不要 rebase），然后再 push。
- 完成后用中文报告做了什么、结果在哪、提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要改写 git 历史（rebase / reset / amend）；不要用 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只读）。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换一种中性的描述重试一次，仍被拒就停下报告。
- 遇到上面没写到的情况，停下来报告，不要自作主张。
