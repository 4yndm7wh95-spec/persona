你在用户的 Windows 电脑上工作。任务：把一张 P3R Persona 渲染图拆成可做骨骼动画的分层零件（只做美术拆件，不改代码）。

【背景】
P3Makoto 是杀戮尖塔 2 的 mod，把储君换成 P3R 的结城理。Claude 在云端写代码，推送到 GitHub 仓库 4yndm7wh95-spec/persona 的分支 claude/amazing-darwin-b00arn；你在本地做美术处理并推送回去。
现在游戏里的 Messiah（弥赛亚） 只是一整张贴图整体移动，用户反馈"太僵硬，只是纯贴图"。Claude 要把它做成分件动画（降临时双臂张开、光翼刃和裙甲随风摆动、锁链和四口棺材晃动漂浮、头部微抬），需要你先把图拆成零件。
另一个 Codex 会话同时在拆 Orpheus，输出目录不同，互不影响。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（C:\Users\kkk\ 下、桌面、Documents，或 Get-ChildItem -Recurse -Filter P3Makoto.json 后用 git remote -v 确认），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
先 git status：有未提交改动就停下报告。然后 checkout claude/amazing-darwin-b00arn，
git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次），git merge --ff-only origin/claude/amazing-darwin-b00arn。

【源图】
<repo>\mod\pck_stage\p3makoto\persona\messiah.png（600×970 RGBA，已裁好边；原始 1024 版本在 <repo>\art\p3r_tex\persona\T_Pp2001_010_0183.png，仅供参考）。
注意：渲染图外围烘焙了一圈白色半透明光晕（轮廓外几像素到十几像素的浅色半透明像素）。零件里**去掉这圈光晕**，只保留人物本体（边缘干净、抗锯齿正常）；光晕由游戏代码另外加。

【要拆的零件】（编号 = 绘制顺序，01 在最底层）
- 01_spire：头顶上方那根竖直的巨大长刃（从头后面一直往上），parent=torso，pivot=它和后背交接处。
- 02_wing_left：画面左侧那片长长的深蓝色刃状裙甲（斜着一直伸到左下角的长尖刺），parent=torso，pivot=它和腰部的连接处。
- 03_wing_right：画面右侧、腿后面的深蓝刃状裙甲那几片，parent=torso，pivot=腰部连接处。
- 04_leg_left：画面左边那条腿（大腿缠带 + 下面的圆柱形长腿），parent=torso，pivot=髋关节。
- 05_leg_right：画面右边那条腿，parent=torso，pivot=髋关节。被棺材挡住的部分要补画完整。
- 06_torso：躯干（胸、腹肌、髋部、前面的白色布裙、肩甲），根零件 parent=null，pivot=腰部中心。被抬起的手臂挡住的胸口要补画。
- 07_head：头 + 金发 + 遮住下半张脸的白色面罩 / 围巾，parent=torso，pivot=颈部中心。
- 08_arm_raised：抬到喉咙前的那条手臂（画面左侧，含手），parent=torso，pivot=肩关节。
- 09_arm_chain：缠着锁链、垂在身侧的那条手臂（画面右侧，含手和手臂上的锁链），parent=torso，pivot=肩关节。
- 10_chain：从手里一直垂到最下面的那条锁链（穿过四口棺材，包括最下面卷曲的一段），parent=arm_chain，pivot=手握链子的点。被棺材挡住的链段补画出来。
- 11_coffin_1 ~ 14_coffin_4：四口蓝色棺材，从上到下编号，各一张，parent=chain，pivot=链条穿过 / 挂住棺材的点（棺材顶部中间附近）。

【拆件规则】
1. 每个零件一张 PNG，**画布和源图完全一样大（600×970）、位置不动**：把全部零件按编号从小到大叠起来，应当和源图（去掉光晕后）一模一样。
2. 被别的零件挡住的部分要**补画出来**（用 imagegen 的编辑 / 局部重绘，或手工克隆修补）：比如手臂后面的躯干、棺材后面的腿。补画范围至少向遮挡边界内延伸 25%（或 40 像素，取大者），这样零件转动 ±15° 时不会露出空洞。补画要和原渲染的材质、光照、配色一致，看不出接缝。
3. 关节处（肩、肘、髋、膝、颈）上下两个零件都要多留一截重叠（各自往对方方向多延伸 15~25 像素，补画成圆滑的关节），转动时不会断开。
4. 只用 PIL / numpy / imagegen 做，不要改原图文件。
5. 如果 imagegen 拒绝或效果很差，改用手工克隆修补，并在 README 里写明哪些区域是怎么补的。

【输出】放到 <repo>\art\persona_parts\messiah\：
- 各零件 PNG（文件名用上面的编号_名字.png）。
- rig.json：
  {"canvas":[600,970],"parts":[{"name":"…","file":"…png","z":1,"parent":"父零件名或null","pivot":[x,y]}, …]}
  pivot = 这个零件绕着转的关节点在画布里的像素坐标（例如上臂 = 肩关节中心；前臂 = 肘关节中心；棺材 = 链条挂点）。parent 按上面列的父子关系。
- preview_stack.png：所有零件按顺序叠回去的结果，与源图并排，再加一张差异图（|叠回 − 源图| 放大 4 倍）。
- preview_parts.png：每个零件单独放在棋盘格背景上排成网格，用红色十字标出 pivot，零件名写在下面。
- preview_swing.png：把每个可动零件绕自己的 pivot 分别转 −15° 和 +15° 后叠出的两张整体图，用来检查转动时有没有空洞或断开。
- README.md（中文）：零件清单、哪些区域是补画的、用了什么方法、已知问题。

【自检】
- 叠回差异图里除光晕外应接近全黑；preview_swing 两张图里不能出现透明的洞、关节不能断开。不满足就修，修不好就在 README 里如实写。

【提交与推送】
- 只 git add art\persona_parts\messiah\ 下的文件。提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: Messiah（弥赛亚） rig parts (layered, occlusions inpainted)"
- push：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试）。
  如果被拒绝是因为另一个会话先推送了：先 fetch，再 git merge --no-edit origin/claude/amazing-darwin-b00arn（生成合并提交，不要 rebase），然后再 push。
- 完成后用中文报告：零件数、补画了哪些地方、三张预览图的结论、提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源；只在 art\persona_parts\messiah\ 下新增文件。
- 不要改写 git 历史（rebase / reset / amend）；不要用 --force；不要改全局 git config。
- 不要启动游戏。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只读）。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 遇到上面没写到的情况，停下来报告，不要自作主张。
