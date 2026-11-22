你在用户的 Windows 电脑上工作。任务分两部分：
A. 从本机 P5R 资源里**只读地**补解暂停菜单（camp）的主精灵表和几份小数据文件，整理进仓库（这部分写报告）；
B. 生成两张预告信卡图（这部分生成完直接提交推送，不检查、不重画、不写报告）。
不改代码，不启动游戏。

**先读 docs/codex/README.md，按里面的长期规则来。**

【背景】
- P5Joker mod 要把牌堆界面（抽牌堆 / 消耗堆 / 弃牌堆 / 牌组）换成 P5R 暂停菜单的样子（主菜单大手掌 / SKILL / ITEM / EQUIP）。
- 上一轮 P5R 解包（research/p5r/README.md）已经有：每页的 Joker 2D 立绘（research/p5r/art/portrait/BASE__P5_CAMP*_2D*.png）、塔罗牌和预告信贴图（research/p5r/ui/camp/）、SYSTEM.ACB 音效。
- **缺的**：主菜单右边的黑白星纹拼贴、「COMMAND」弯曲大字、「PHANTOM」「MeNu&Main COMMAND SELECT」「Don't Look at ME Like That」标题牌、页码大数字、菜单剪贴字（SKILL / ITEM / EQUIP…）、SKILL / ITEM 页的锯齿队伍列表框、道具列表行（红色选中条、白色行、×19 数量胶囊、NEW 标签）、EQUIP 页的 WEAPON / GUN / PROTECTOR 标签牌、切页时的白色斜板和弯曲星纹带。
  这些很可能都在 **SC.CPK 的 INIT/P5CAMP_00SPD.SPD**（61.6MB），上一轮没有解它。
- 参考画面：docs/p5/preview/p5r-20261121-223943-frames-02-12/（P5R 实机逐帧截图，2–12 秒，先拉下来看几张，知道要找什么）。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 **claude/confident-hypatia-nropgi**。
P5R 资源库：C:\Users\kkk\sts2_tools\p5r_rip\；工具：C:\Users\kkk\sts2_tools\p5r_tools\（上一轮下好的 CPK 工具、PersonaEditor 1.6.7.0 等，直接用）。

## 1. 拉最新
   git -c safe.directory=* -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/confident-hypatia-nropgi:refs/remotes/origin/claude/confident-hypatia-nropgi
失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。基于 origin/claude/confident-hypatia-nropgi 提交。
工作区有本地改动、切分支会被拒时：不要处理那些改动，用独立 worktree 只提交本任务的文件。
需要看参考帧时：git -c safe.directory=* sparse-checkout add docs/p5/preview research/p5r（可加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

## 2. A 部分：补解（游戏文件只读，解到 p5r_rip\raw\ 保留包内路径）
用上一轮同样的工具和方法（README 里写过的命令），按 research/p5r/index.csv 里的包内路径**单个文件**解，不整包解：
1. **SC.CPK INIT/P5CAMP_00SPD.SPD**：
   - 导出全部精灵（每个切图一张 PNG）和精灵表整图，**原质量 RGBA PNG，不做调色板量化 / 不压色**，保留透明；
   - 切图坐标存成 sprites.json（和上一轮 ui/*/sprites.json 同格式）；
   - 放 <repo>\research\p5r\ui\camp_main\（整图放 sheets\，切图放 sprites\），再拼一张 contact_sheet.png（每格写文件名，深灰蓝底，方便看透明区域）。
2. 下面这些小文件**原样拷贝**到 <repo>\research\p5r\ui\camp_main\raw\（保留原文件名）：
   - SC.CPK INIT/CAMP.PLG、BASE.CPK INIT/CONFIG.PLG、BASE.CPK INIT/PERSONA_STATUS_LINE.PLG（PLG 应该是菜单里白 / 红斜块的多边形数据）；
   - BASE.CPK INIT/CAMPBGSTARANIME.DAT、INIT/CAMPBGDAT.UAD、INIT/CAMPBGDATMORGANA.UAD（菜单背景星纹动画数据）；
   - SC.CPK INIT/CMPTABLE.BIN；
   - BASE.CPK INIT/EVTWIPETEX.PAK：原样拷贝，**再展开**，里面的贴图转原质量 PNG 放 raw\EVTWIPETEX\（可能是转场擦除贴图）。
   PLG 如果 PersonaEditor 或别的现成工具能导出成 SVG / JSON / 图片，顺便导出放 raw\plg_export\；导不出就只放原文件，README 里写一句。
3. 在 index.csv 里再搜一遍包内路径含 CAMPBG、STAR、COMMAND、WIPE 的 SPD / DDS / TMX / PAK（不含 CHAT、CARDTEX、CHARATEX），**和暂停菜单有关的**同样导出原质量 PNG 放 ui\camp_main\extra\<原文件名>\；拿不准有没有关系的只列进 README，不解。
4. SYSTEM.ACB 里这几个音效**原样拷贝**到 <repo>\research\p5r\audio\camp\：stream_name 是 camp_open、camp_open_field、camp_whoosh、cursor、cancel、card_select、wipe_cloth 的那些行（对照 research/p5r/audio/sfx/catalog.csv 找文件名，从同目录拷），附 catalog.csv（同格式，只列这些）。
5. 体积：单个文件不超过 20MB（超过的留在 p5r_rip\，README 里写路径和大小）；本次新增到仓库的合计超过 300MB 就停下来报告各部分大小。
6. 写 <repo>\research\p5r\ui\camp_main\README.md（中文）：每个文件的来源包内路径、导出了什么、用的工具和参数、没导出来的卡在哪；**对照参考帧**，列出你认出来的素材（星纹拼贴、COMMAND 字、标题牌、菜单字、列表行、标签牌、斜板、星纹带……各是哪个文件），认不出的写「未认出」。

## 3. A 部分提交推送
- 只 add research/p5r/ui/camp_main 和 research/p5r/audio/camp；提交信息 "research: P5R camp menu sprites (P5CAMP_00SPD), PLG/star data, wipe textures, camp SFX"。
- push：git -c safe.directory=* -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin <你的提交>:claude/confident-hypatia-nropgi
  失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次；被拒先 fetch 再 merge --no-edit（不 rebase）再 push。

## 4. B 部分：预告信卡图（生图）
用途：牌堆界面里，悬浮在 Joker 红手套掌心上方、绕竖轴慢慢转的一张卡（正面 / 背面各一张）。游戏里显示约 **320×200 像素**（1080p），转的时候侧面会被压窄，所以要大块清楚的图形，不要细碎小字。

参考原版（只当设计参考，不要照抄它歪斜的角度和投影）：
- research/p5r/ui/camp/SC__P5_CARD02__90df21923c.png：红黑同心圆 + 怪盗团礼帽标志 + 剪贴字「TAKE YOUR HEART」；
- research/p5r/ui/camp/SC__P5_CARD01__70e5ccb557.png：红底 + 礼帽笑脸涂鸦 + 手写「TAKE YOUR HEART」。

要画的（放 art/p5/ui/pile/，全部真透明底 RGBA）：
1. **calling_card_front.png**（1536×1024）：预告信正面。**正对镜头、完全不歪、不倾斜、没有投影**，一张横向长方形卡片几乎铺满画布（四周各留约 2% 透明边，卡片外 alpha = 0），四个角是小圆角。设计照 CARD02：红黑同心圆底，中间红色礼帽标志（白色火焰纹），下方剪报拼贴字「TAKE YOUR HEART」（每个字母是不同的剪下来的纸片，红黑白交替）。纸张质感、轻微印刷磨损。
2. **calling_card_back.png**（1536×1024）：预告信背面。和正面**同尺寸、同形状、同位置**（叠在一起轮廓完全重合）。设计照 CARD01：大红底（带轻微纸张纹理），中间是礼帽 + 白眼睛 + 尖牙笑脸的黑色涂鸦，下方黑色手写体「TAKE YOUR HEART」。
只用红（#E5191C 附近）、黑、白三色；不要霓虹发光、立体渲染、塑料光泽、其他颜色。

## 5. B 部分提交推送
生成完直接提交推送（不用自己检查、不用重画、不用写报告、不用拼检查图）：
- 只 add art/p5/ui/pile；提交信息 "art: P5 pile calling card front/back"。
- push 同第 3 步。

## 6. 报告（只报 A 部分）
中文：解出了哪些文件、多少张 PNG、仓库新增多大；README 里「认出来的素材」清单；没解出来的东西和原因；两个提交号（B 部分只写提交号）。

【绝对不要做】
- 不要修改或删除仓库里已有的代码和资源，只新增 research/p5r/ui/camp_main、research/p5r/audio/camp、art/p5/ui/pile 下的文件。
- 不要 rebase / reset / amend / stash；不要 --force；不要改全局 git config、系统环境变量 / PATH。
- 只用 C:\Users\kkk\sts2_tools\p5r_tools\ 里已有的工具；真缺工具时，只允许从 GitHub 官方 release 页下载开源工具的便携版放进 p5r_tools\（不运行安装程序、不用管理员权限），报告里写名字、版本、地址。除此之外不要安装任何东西。
- 不要启动游戏（杀戮尖塔 2、P5R 都不要），不要用 Computer Use，不做实机验收。
- 游戏文件（CPK）只读，不要往游戏目录写任何东西。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，不要读取或输出任何密码；解密密钥 / 配置不要写进仓库、README 或报告。
- 不要进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 每条命令单独执行；不要 Remove-Item -Recurse / -Force；不要删除任何目录或文件。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 遇到上面没写到的情况、或任何命令出错，停下来写报告。
