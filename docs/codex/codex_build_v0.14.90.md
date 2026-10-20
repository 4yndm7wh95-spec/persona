你在用户的 Windows 电脑上工作。任务：把 P3Makoto v0.14.90、音乐盒 P3MusicBox 1.1.3、测试工具 P3TestMod 1.2.2 编译、打包，安装到本机**两个**杀戮尖塔 2（0.107.1 正式版和 0.111 测试版），再用兼容检查工具对两个版本各检查一遍。只做编译、打包、安装、检查，不改任何代码。

【两个游戏目录】
- <G111> = C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2（以前编译用的那个）。
- <G107> = C:\Users\kkk\Downloads\Slay the Spire 2。
- 先只读两边的 release_info.json，确认版本分别是 v0.111.x 和 v0.107.1；对不上就停下来报告。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功编译的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. 执行 git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/、mod_build/、art/p3menu_system/ref/menu_transition_frames_020-107/frames/、research/p3r_camp/ 下如有未跟踪文件原样保留、忽略它们继续（不要移动、删除或提交）。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并：先执行
   git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn
   不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   - 有冲突就停下来报告。
   - 这次不需要 push。
6. 确认 git log 里有 "v0.14.90 build: original COMMU background" 这个提交（抽牌堆立绘换成原版 COMMU 3D 实时渲染 + 牌浮空自转 + 卡框提亮）。
   docs/codex/codex_fix_v0.14.90_hero_materials.md 已由 Claude 直接改完，**不用再执行那份文档**。
7. 稀疏检出缺目录时，允许执行：
   git -c safe.directory=<repo> sparse-checkout add mod musicbox testmod tools/compat research/libs_0.107.1
   - 路径过长时，这一条命令可以加 -c core.longpaths=true。
   - 不要 sparse-checkout disable / set。

【2. 确认游戏没在运行】
有杀戮尖塔 2 在运行就停下来报告，不要强行结束它。

【3. 编译（都对照 <G111> 编译，同一个 DLL 两个版本通用）】
用上次能用的 dotnet，例如 C:\Users\kkk\sts2_tools\dotnet\dotnet.exe。失败就把完整错误原样贴出来，不要改代码。
1. <repo>\mod：dotnet build P3Makoto.csproj -c Release -p:GameDir="<G111>"。
2. <repo>\musicbox：dotnet build -c Release -p:GameDir="<G111>"。
3. <repo>\testmod：dotnet build -c Release -p:GameDir="<G111>"。

【4. 打包 pck】（和以前一样）
1. 把 <repo>\mod\pck_stage 整个复制到 <repo>\mod\_build\stage_<时间戳>\（包括 .godot，可以用 robocopy /E）。
2. 核对 p3makoto\ui\ 下有 system_portrait.png、system_portrait_q1.png、system_portrait_half.png、system_portrait_q3.png、system_portrait_closed.png 及各自的 .import，.godot\imported\ 下有五个对应的 .ctex；p3makoto\ui\dive\ 下有 14 张 dive_*.png 及各自的 .import，.godot\imported\ 下有 14 个 dive_*.ctex；p3makoto\sfx\ 下有 92 个 .ogg（新增 sys_dive.ogg、sys_blob.ogg）；p3makoto\pile_deck、pile_discard、pile_exhaust 下各有 *_skel_data.tres；p3makoto\pile\ 下有 pile_skel_data.tres、pile.atlas.import、pile.spine-json.import；p3makoto\rig\characters\ 下有 regent_skel_data.tres 和 regent.atlas.import；.godot\imported\ 下有两个文件名含 p3private 的 .spatlas。
   另外（v0.14.90 新增）：p3makoto\hero3d\ 下有 4 个 .glb（SK_PC0051_C002 / F000 / H000、SK_Pp2001_000）、tex\ 下 7 张 png（含 T_UI_Camp_Caustics_00.png、commu_word.png）、anim\ 下 30 个文件（5 段主角动作各 .json/.bin/.scale.bin，5 段 Card_ 动作各 .json/.bin），合计约 11MB；这些是运行时直接读的原始文件，**没有也不需要 .import**，原样打进 pck。
   v0.14.89 起已有：p3makoto\card_frame\ 下有 19 张 png（card_frame_attack_s / skill_s / power_s、card_banner_*、card_portrait_border_*、arcana_tab）及各自的 .import；p3makoto\cards\ 下有 6 张 png（strike_regent、defend_regent、falling_star、big_bang、tyranny、the_smith）及 .import；.godot\imported\ 下有对应的 .ctex；p3makoto\fonts\ 下有 Cinzel-Bold.ttf、LXGWWenKai-Bold-CardTitles.ttf（约 88KB）和 OFL-LXGWWenKai.txt。
3. 用 GDRE Tools 打包：
   <gdre_tools.exe> --headless --pck-create=<stage目录> --output=<repo>\mod\_build\P3Makoto.pck --pck-version=3 --pck-engine-version=4.5.1

【5. 安装到两个游戏（<G111> 和 <G107> 各做一遍）】
1. P3Makoto：
   - 目标是 <游戏>\mods\ 下当前装着 P3Makoto.json 的那个文件夹（可能是嵌套的 mods\P3Makoto\P3Makoto\，以实际位置为准）；没有就新建 <游戏>\mods\P3Makoto\。
   - 先把旧的 P3Makoto.json / .dll / .pck 备份到 <repo>\mod\_build\backup_<时间戳>_<107或111>\。
   - 再复制新的 P3Makoto.json、dll、pck。
   - 核对 version 是 "0.14.90"。
2. 音乐盒：
   - 复制 P3MusicBox.dll 和 P3MusicBox.json 到 <游戏>\mods\ 下当前装着 P3MusicBox.json 的那个文件夹（以实际位置为准；没有就新建 mods\P3MusicBox\）。
   - **不要动 music\ 里的歌曲和歌词。**
   - 如果 music\ 里有 playlist.json，把它**改名**为 playlist.txt（内容不变）；已经有 playlist.txt 就不动。
     原因：游戏会把 mod 文件夹里所有 .json 当成 mod 清单读，每次启动都报一条红字错误。
   - 某个游戏里还没装过音乐盒、也没有 music 文件夹：从另一个游戏的 music\ 整个复制过来。
   - 核对 version 是 "1.1.3"。
3. 测试工具：只在 <游戏>\mods\P3TestMod\ 已经存在时才复制 P3TestMod.dll 和 P3TestMod.json 过去，核对 version 是 "1.2.2"；不存在就跳过，并在报告里说明。
4. 两个游戏的 mods 里如果还有别的 mod，不要动，在报告里列出来就行。

【6. 兼容检查（两个版本各跑一次）】
1. 在 <repo> 下执行：
   dotnet run -c Release --project tools\compat\ApiCheck -- <P3Makoto.dll> <G111>\data_sts2_windows_x86_64\sts2.dll <P3MusicBox.dll> <P3TestMod.dll>
2. 再把第二个参数换成 <G107>\data_sts2_windows_x86_64\sts2.dll 跑一次。
3. 两次的最后一行都应为「结果：全部能找到 ✓」。有 ✗ 的行就原样贴进报告。
4. 编译这个工具如果需要从 NuGet 还原 .NET 的引用包，允许联网还原，但不要安装别的东西。

【7. 报告】
用中文报告：
- 合并到的提交号；
- 三个 mod 是否都编译成功、各自的警告摘要；
- pck 大小、sfx 数量；
- 两个游戏各自的安装路径和版本号；
- playlist 改名情况；
- 两次 ApiCheck 的完整输出。

【8. 不做实机验收】
用户自己进游戏验收：**不要启动游戏，不要用 Computer Use 操作游戏**。安装和兼容检查做完、报告发出就结束。

报告最后附上给用户的测试要点（用户自己验收用）：
1. 打开抽牌堆：整个界面换成原版 P3R 营地 COMMU 页 —— 背景是后面的战斗画面套原版颜色（上方浅水泛青、焦散水纹），右边白色斜块 + 灰色 COMMU 大字，结城理是原版模型 + 原版动作（实时 3D），外套和头发透出水，在白块上显出轮廓和小阴影。入场拿着牌 → 松手，牌浮到胸前 → 牌在空中慢慢自转（约 10 秒一圈）。外套、头发透出后面的水。
2. 光标在卡牌上移动：每碰到一张牌，浮空的牌明显加速转一下（约 120°）；快速扫过一排时应连续加速、不停顿不卡顿。
3. 重点看丝滑：入场、松手、自转、加速转，整个过程有没有掉帧、跳帧、突然停一下。有的话把日志 godot.log 发给 Claude（"perf pile … build" 那行现在带 hero3d 分段耗时）。
4. 关掉抽牌堆时人物跟着整组滑出去，不会先消失。卡组 / 弃牌 / 消耗三个牌堆这次没变。
5. 卡框整体提亮（边框宝蓝、描述框蓝色渐变）。
6. 日志开头应有 "[P3Makoto] init v0.14.90"；如果有 "hero3d unavailable" 的警告（会退回旧立绘），把那一行发给 Claude。

【绝对不要做】
- 不要修改或删除代码；不要 rebase / reset / amend；不要 --force；不要改全局 git config；不要提交或推送任何东西。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动），不要用 Computer Use；不要修改存档。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（release_info.json、sts2.dll 都只读）。游戏目录里只允许写 mods\ 下 P3Makoto、P3MusicBox（歌曲和歌词不动，只改 playlist 名）、P3TestMod 三个文件夹。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
