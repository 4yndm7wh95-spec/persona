你在用户的 Windows 电脑上工作。任务分两部分：
A. 从本机 P5R（女神异闻录5 皇家版，PC）的游戏资源里**只读地**把下一个 mod 要用的原版素材一次性解包、整理好：完整解包放本机资源库，挑出来的部分放进仓库；
B. A 全部完成并推送后，删掉本机以前扒 P3R 时留下的中间解包目录。
不改代码，不生图，不启动游戏。

**先读 docs/codex/README.md，按里面的长期规则和「和 Claude 同时改代码」的合并规则来。**

【背景】
- 杀戮尖塔 2 的 mod P3Makoto（储君换成 P3R 的结城理）快做完了。下一个 mod 思路完全一样，只是换成 P5R 的雨宫莲（Joker）+ 亚森（Arsène）。
- P3Makoto 用到的原版东西有：主角 3D 模型 + 战斗 / 菜单动作（离线渲染成 Spine 骨骼动画的原画、实时 3D 菜单立绘）、Persona 模型、菜单 / 战斗 UI 的贴图和动画、总攻击和过场演出、字体、音效、主角语音、BGM 清单。P5R 这边要的是同一套。
- 这次的目标是**一次准备齐**：后面 Claude 做任何一块都直接从本机资源库或仓库里取，不用再回头翻游戏。所以第 2 步的完整索引最重要，导出范围宁可多不要少。
- P5R 不是虚幻引擎，P3R 那套 FModel / CUE4Parse 用不上。P5R PC 版是 CRI 的 CPK 归档；模型 .GMD、动作 .GAP、贴图 DDS / TMX、UI 精灵表 .SPD、音频 CRI ACB / AWB（HCA）、过场 USM。下面写到的路径、文件名只是线索，一律以实际为准。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功推送的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. 执行 git -c safe.directory=<repo> status -sb：
   - 有进行中的合并 / rebase：停下来报告。
   - 有你（Codex）在别的任务里改了、还没提交的东西：先提交（只 add 你改的文件，提交信息写清楚改了什么、为什么），再继续；说不清来源的改动：停下来报告。
   - README 里列的未跟踪目录原样保留、忽略。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并：先 git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn；不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   有冲突就停下来报告。
6. 稀疏检出：允许 git -c safe.directory=<repo> sparse-checkout add research/p5r docs（可加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 找到游戏、准备工具】
1. 找本机的 P5R：先看 Steam 库（各盘的 SteamLibrary\steamapps\common\ 下名字含 P5R / Persona 5 Royal 的目录），再看 G:\Gamebox\ 这类以前放 P3R 的地方。用 dir 找 *.cpk，确认是 P5R。
   - 只装在 C:\Program Files\WindowsApps\ 里（Xbox / Game Pass 版）、文件读不了：停下来报告，不要想办法绕过权限。
   - 找不到 P5R：停下来报告找过哪些目录。
2. 工具：P3R 的工具不适用，需要新的。**允许**从 GitHub 官方 release 页下载下面这类开源工具的便携版，放到 C:\Users\kkk\sts2_tools\p5r_tools\ 下（不要运行安装程序，不要管理员权限，不要改系统 PATH）：
   - CPK 解包：能读 P5R PC 版 CPK 的工具（例如 Sewer56 的 CriFsV2Lib / CriFs 相关命令行工具、YACpkTool，选一个能用的）；
   - 模型 / 动作：GFD Studio（TGEnigma），或者本机已有的 Blender 加 GFS / GMD 导入插件（例如 Pherakki 的 GFS-Blender-Tools）。目标是导出带骨骼、带贴图的 GLB（或 FBX），动作能单独导出；
   - 贴图：DDS → PNG（Pillow、texconv 都行），TMX、SPD 用 Persona Editor 或同类工具；
   - 音频：vgmstream-cli（ACB / AWB / HCA → WAV，再用本机 ffmpeg 转 OGG）；
   - 过场：USM 拆流（例如 VGMToolbox、WannaCRI），再用 ffmpeg 转 MP4。
   报告里写清每个工具的名字、版本、下载地址。哪个工具下不到、用不了，就在 README 里记下来，跳过那一类，继续做别的。
3. 解密：如果 CPK 需要 P5R 专用的解密，用工具自带的 P5R 选项。密钥或解密配置只留在本机工具里，不要写进仓库、README 或报告。
4. 中间文件和完整解包都放 C:\Users\kkk\sts2_tools\p5r_rip\。这个目录是以后长期用的**资源库**：做完不要删。

【3. 完整解包 + 索引（这一步最重要）】
1. 把所有 CPK 完整解到 C:\Users\kkk\sts2_tools\p5r_rip\raw\<cpk名>\，保留原目录结构。磁盘空间不够就停下来报告（写清需要多少、剩多少），不要删别的东西腾地方。
2. 生成 <repo>\research\p5r\index.csv：**全部文件**每个一行 —— cpk 名、包内路径、扩展名、大小（字节）。
3. 生成 <repo>\research\p5r\index_summary.md：按顶层目录统计文件数和大小，再按扩展名统计；每个顶层目录用一句话写清楚它大概是什么（看文件名推断，拿不准就写「不确定」）。
4. 套娃归档：.PAK / .BIN 这类里面还包着文件的，也展开到 raw\ 旁边的 unpacked\，展开出来的文件也进 index.csv（加一列「所在归档」）。

【4. 挑出来导出（结果放仓库 <repo>\research\p5r\，完整版留在 p5r_rip\）】
每一类都在 README 里写：原始路径、导出了什么、没找到的找过哪些关键词。
a. 主角雨宫莲：
   - 模型：怪盗服（战斗用）和学生服各一套。导出 GLB（含骨骼、蒙皮、材质名；贴图另存 PNG），放 model\joker\。
   - 动作：怪盗服的**全部战斗动作**（待机、受伤、倒地、近战普攻、枪击、技能、召唤 Persona（摘面具）、防御、闪避、Baton Pass、总攻击、胜利、逃跑之类，以实际为准）和菜单 / 过场里用到的动作。每个动作单独导出（GLB 只带骨骼动画，或 FBX），再附 anim_list.csv：文件名、原始名、帧数、帧率、时长秒、你从名字推断的用途。
   - 每个动作渲一张缩略图（正面，中间那一帧），拼成 anim_sheet.png，方便 Claude 挑。
b. 亚森（Arsène）：模型 + 全部动作 + 贴图，同上，放 model\arsene\。
c. 2D 立绘：雨宫莲、亚森的立绘 / 半身像（bustup）/ 表情差分，原尺寸 PNG（保留透明），放 art\portrait\，附 contact_sheet.png。
d. UI（精灵表 SPD / 贴图 → PNG，并把每张精灵表的切图坐标存成 JSON），放 ui\<界面名>\，每个子目录一张 contact_sheet.png：
   - 暂停菜单（P5R 内部可能也叫 camp）和它的每个子菜单（SKILL、ITEM、EQUIP、PERSONA、STATUS、CONFIDANT / COOP、CALENDAR、SYSTEM / CONFIG 等，以实际为准）；
   - 战斗界面：指令（ATTACK / GUN / GUARD / SKILL / PERSONA / ITEM 那个环形菜单）、HP / SP 条、目标选择、「1 MORE」、Baton Pass、Hold Up、总攻击（ALL-OUT ATTACK）和收尾画面、SHOWTIME、技能名横幅、属性图标、伤害数字、WEAK / CRITICAL / MISS 字样；
   - 战斗结算（RESULT）界面；标题画面、LOGO；预告信（Calling Card）；天鹅绒房间（Velvet Room）界面；手机 / 怪盗团 App 界面；
   - 读取 / 转场画面（红黑转场、碎片）。
   - 菜单和战斗界面的动画数据：P5R 的 UI 动画如果在 .bf / .bmd / 表格或别的文件里能找到（关键帧、时间、位置），原样导出并在 README 里说明格式；找不到就写「动画写死在程序里」，不要编。
e. 字体：游戏里的字体文件（P5 那种剪报风格的标题字、正文字）原样导出，放 font\，README 里写每个字体用在哪。
f. 音频（vgmstream → OGG，原采样率，不剪），完整版全部转好放 p5r_rip\audio\；仓库只放：
   - sfx\：系统 / 菜单音效（光标、确认、取消、翻页、打开暂停菜单、切子菜单）和战斗音效（攻击、枪、技能、召唤 Persona、1 MORE、Baton Pass、总攻击、Hold Up、暴击、弱点、胜利结算、升级），附 catalog.csv：文件名、来源 ACB、cue 序号、cue 名、时长秒；
   - voice\：雨宫莲战斗语音（日语、英语各一套，有就都要），附同样格式的 catalog.csv；
   - 另外给**全部**音频（含 BGM）做一份 <repo>\research\p5r\audio_catalog.csv（同样的列），BGM 本身不进仓库。
g. 过场（USM → MP4，H.264，CRF 20），完整版放 p5r_rip\movie\；仓库只放 movie_list.csv（文件名、时长、分辨率、你看一眼内容后的一句话描述），以及总攻击、SHOWTIME、召唤 Persona、开场、结尾相关的那几段各一张逐秒截图拼图（每秒一格，格子左上角写秒数），放 movie\sheets\。
h. 文本：技能名、道具名、Persona 名这类表格（BMD / 表格）能导出成 UTF-8 文本或 CSV 的，放 text\（中文、日文、英文有哪几种就导哪几种）。导不出就在 README 里写一句。

体积控制：仓库里单个文件不超过 20MB（超过的只留在 p5r_rip\，README 里写路径和大小）；research\p5r\ 总量超过 400MB 时，按 a → b → d → f → c → e → g → h 的优先级提交，其余留在本机并在 README 里列清单，等 Claude 要的时候再拷。

【5. README】
<repo>\research\p5r\README.md（中文）：
- 游戏版本、安装位置（只写到游戏目录名，不写用户名以外的隐私）、CPK 清单；
- 每个工具的名字、版本、下载地址，用了什么参数（不写密钥）；
- 本机资源库 C:\Users\kkk\sts2_tools\p5r_rip\ 的目录结构（raw、unpacked、audio、movie……各放什么）；
- 第 4 步每一类的结果：导出了什么、在仓库哪里、在资源库哪里；没找到或导不出的写清楚找过哪些路径和关键词、卡在哪一步。

【6. 提交与推送】
1. 只 add：git -c safe.directory=<repo> add research/p5r
2. 提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "research: P5R rip for the next mod (index, Joker/Arsene models and anims, UI, fonts, SFX, voice, movie list)"
   文件很多的话可以按类分几次提交，每次提交信息写清楚是哪一类。
3. push：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
   - 失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次；
   - 被拒绝（远端有新提交）时先 fetch，再 merge --no-edit（同第 1 节）后重新 push。不要 rebase，也不要 --force。

【7. 删除本机的 P3R 中间解包目录（第 6 步推送成功后才做）】
用户说以前扒出来的 P3R 资源不再需要了。只删**本机的中间解包目录**，仓库里的 P3R 资源不动（P3Makoto 还在用）。
1. 先确认仓库里的 P3R 资源都在：git -c safe.directory=<repo> ls-files research/p3r_camp art/p3r_tex research/p3r_se 统计文件数，三项都不是 0 才继续，否则停下来报告。
2. 列出候选：C:\Users\kkk\sts2_tools\ 下**名字以 p3r_ 开头**的目录（例如 p3r_rip、p3r_camp_<日期>、p3r_commu_<日期>、p3r_pages_<日期>），以及以前给 P3R 解包做的、名字明确带 p3r 的临时目录。每个写出完整路径和大小。
3. 只删第 2 步列出的这些目录（Remove-Item -Recurse）。**不要动**：
   - P3R 游戏本体（G:\Gamebox\... 下的游戏目录和 pak）；
   - 仓库 <repo> 里的任何文件；
   - C:\Users\kkk\Desktop\P3R_templates\（宣传图模板，不是解包产物）；
   - C:\Users\kkk\sts2_tools\ 下其他目录（dotnet、musicbox_work、musicbox_music、pck107_full、各种 backup、新建的 p5r_tools、p5r_rip）；
   - FModel 的程序和配置（AES 配置留着，以后真要重扒还能用）。
   拿不准算不算 P3R 解包产物的目录：不删，列进报告。
4. 报告里列出删了哪些目录、各多大、一共腾出多少空间，以及没删、拿不准的目录。

【8. 报告】
用中文报告：
- 找到的游戏位置和版本、CPK 数量、完整解包的文件数和总大小；
- 用的工具（名字 + 版本）；
- 第 4 步 a ~ h 各类导出的数量、在仓库和资源库的位置；没找到的东西；
- 提交号；
- 第 7 步的删除结果。

【绝对不要做】
- 不要修改或删除代码和已有资源，仓库里只新增 research/p5r 下的文件。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2、P3R、P5R 都不要启动），也不要用 Computer Use。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码；解密密钥 / 配置不要写进仓库、README 或报告。
- 不要修改 P3R、P5R 或杀戮尖塔 2 的游戏原文件（CPK、pak 都只读）。
- 除第 2 步说的开源工具便携版外，不要安装别的东西；不要运行安装程序，不要用管理员权限。
- 第 7 步以外不要删除任何东西；第 7 步只删列出来的 P3R 中间目录。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
