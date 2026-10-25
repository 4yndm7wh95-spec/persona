你在用户的 Windows 电脑上工作。任务：把 P3Makoto v0.14.105、音乐盒 P3MusicBox 1.1.3、测试工具 P3TestMod 1.2.2 编译、打包，安装到本机**两个**杀戮尖塔 2（0.107.1 正式版和 0.111 测试版），再用兼容检查工具对两个版本各检查一遍。只做编译、打包、安装、检查，不改任何代码。

**先读 docs/codex/README.md，按里面的长期规则和「和 Claude 同时改代码」的合并规则来**：本地有你自己没提交的代码先提交 → fetch → merge（能快进就 --ff-only，否则 merge --no-edit，不 rebase）→ 编译 → 兼容检查通过后本地多了提交就 push。冲突拿不准就停下来报告。

【两个游戏目录】
- <G111> = C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2（以前编译用的那个）。
- <G107> = C:\Users\kkk\Downloads\Slay the Spire 2。
- 先只读两边的 release_info.json，确认版本分别是 v0.111.x 和 v0.107.1；对不上就停下来报告。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功编译的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. 执行 git -c safe.directory=<repo> status -sb（协同规则见 docs/codex/README.md「和 Claude 同时改代码」）：
   - 有进行中的合并 / rebase：停下来报告。
   - 有你（Codex）在别的任务里改了、还没提交的代码：先提交（只 add 你改的文件，提交信息写清楚改了什么、为什么），再继续；不是你改的、说不清来源的改动：停下来报告。
   - 未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/、mod_build/、art/p3menu_system/ref/menu_transition_frames_020-107/frames/ 原样保留、忽略。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并（把 Claude 的改动并进你本地，你的提交保留）：先执行
   git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn
   不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   - 有冲突：同一处两边都改了、拿不准保留哪边的，停下来报告（列出冲突文件和两边的改动），不要自己挑一边。
   - 合并后先编译（第 3 节）。编译通过、兼容检查全部能找到后，如果本地比远端多了提交（你的代码提交或合并提交），push 上去：
     git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
     被拒（远端又有新提交）就再 fetch + merge --no-edit + 编译一次再 push；不要 rebase，不要 --force。
6. 确认 git log 里有 "v0.14.105: water-filled exhaust hero, discard original colours + outline, camera pan" 提交（之前的版本还没编过的话一起编进去）。报告里写明：这次编进去的你自己的提交有哪些（提交号 + 一句话）。
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

【4. 打包 pck】
新增（2026-10-21）：卡面替换已撤回。复制stage后先运行 `python tools/pck/remove_card_skins.py --stage <本次stage目录>`，清除旧卡图/卡框/专用字体及遗留导入文件（保留p3makoto/fx/pile_card_back.png及导入产物，它是牌堆按钮装饰）；再用同一命令加 `--check` 确认没有残留。不要再运行旧版插画生成或复制流程，也不要重新打包 art/cards、art/card_frame 等美术源图。
1. 把 <repo>\mod\pck_stage 整个复制到 <repo>\mod\_build\stage_<时间戳>\（包括 .godot，可以用 robocopy /E）。
2. 核对 p3makoto\ui\ 下有 system_portrait.png、system_portrait_q1.png、system_portrait_half.png、system_portrait_q3.png、system_portrait_closed.png 及各自的 .import，.godot\imported\ 下有五个对应的 .ctex；p3makoto\ui\dive\ 下有 14 张 dive_*.png 及各自的 .import，.godot\imported\ 下有 14 个 dive_*.ctex；p3makoto\sfx\ 下有 92 个 .ogg（新增 sys_dive.ogg、sys_blob.ogg）；p3makoto\pile_deck、pile_discard、pile_exhaust 下各有 *_skel_data.tres；p3makoto\pile\ 下有 pile_skel_data.tres、pile.atlas.import、pile.spine-json.import；p3makoto\rig\characters\ 下有 regent_skel_data.tres 和 regent.atlas.import；.godot\imported\ 下有两个文件名含 p3private 的 .spatlas。
   另外（v0.14.90 起）：p3makoto\hero3d\ 下有 4 个 .glb（SK_PC0051_C002 / F000 / H000、SK_Pp2001_000）、tex\ 下 8 张 png（含 T_UI_Camp_Caustics_00.png、commu_word.png、item_word.png；v0.14.103 的 item_waterlight.png 已删除，stage 里不应再有）、anim\ 下 43 个文件（v0.14.98 加了 CampTopStart / CampTopLoop；v0.14.101 加了 CampItemStart / CampItemLoop；v0.14.105 加了 CampSkillStart / CampSkillLoop，各 .json/.bin/.scale.bin），dive\ 下的文件以 <repo>\mod\pck_stage\p3makoto\hero3d\dive\ 实际内容为准（逐个核对复制完整），5 段 Card_ 动作各 .json/.bin），合计约 11MB；这些是运行时直接读的原始文件，**没有也不需要 .import**，原样打进 pck。
   v0.14.89 起已有：p3makoto\card_frame\ 下有 19 张 png（card_frame_attack_s / skill_s / power_s、card_banner_*、card_portrait_border_*、arcana_tab）及各自的 .import；p3makoto\cards\ 下有 6 张 png（strike_regent、defend_regent、falling_star、big_bang、tyranny、the_smith）及 .import；.godot\imported\ 下有对应的 .ctex；p3makoto\fonts\ 下有 Cinzel-Bold.ttf、LXGWWenKai-Bold-CardTitles.ttf（约 88KB）和 OFL-LXGWWenKai.txt。
3. 用 GDRE Tools 打包：
   <gdre_tools.exe> --headless --pck-create=<stage目录> --output=<repo>\mod\_build\P3Makoto.pck --pck-version=3 --pck-engine-version=4.5.1

【5. 安装到两个游戏（<G111> 和 <G107> 各做一遍）】
1. P3Makoto：
   - 目标是 <游戏>\mods\ 下当前装着 P3Makoto.json 的那个文件夹（可能是嵌套的 mods\P3Makoto\P3Makoto\，以实际位置为准）；没有就新建 <游戏>\mods\P3Makoto\。
   - 先把旧的 P3Makoto.json / .dll / .pck 备份到 <repo>\mod\_build\backup_<时间戳>_<107或111>\。
   - 再复制新的 P3Makoto.json、dll、pck。
   - 核对 version 是 "0.14.105"。
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
1. 消耗堆（原版 SKILL 页）：人物外套 / 头发暗部（青色以外）是透明的，透出后面的水（落在白底上也是水色，不再是深蓝色块）；其余同 v0.14.104（V 字白底、白底上的轮廓灰影、人物一步到位、召唤器深灰两层）。
   弃牌堆（原版 ITEM 页）：牌在左边挡住人物大部分，露出脸和手；人物是原版那种低对比的蓝色单色（皮肤中蓝、阴影和线条深蓝、头发 / 外套按原版色），脸上能看到鼻梁线和下巴、耳朵的描线；屏幕左边、下边不再有人物被截断的断层。
2. 选卡（默认「精简」）：点「将一张牌添加到你的牌组」后，三张牌一开始就是正面，从中间同时向左右展开（约 0.3 秒），马上能点；上方 SHUFFLE TIME 字图滑过、落位时有十字闪光。
   游戏设置 → 常规 → 结城理中的「选牌动画」，切到「完整」是原来的标题 → 洗牌 → 翻牌。
3. 抽牌堆、牌组不变。
4. 日志开头 "[P3Makoto] init v0.14.105"。

【绝对不要做】
- 编译安装这个任务本身不改代码（改代码是单独的任务，按 README 的协同规则先提交）；不要 rebase / reset / amend；不要 --force；不要改全局 git config；除第 5 步说的情况外不要提交或推送。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动），不要用 Computer Use；不要修改存档。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（release_info.json、sts2.dll 都只读）。游戏目录里只允许写 mods\ 下 P3Makoto、P3MusicBox（歌曲和歌词不动，只改 playlist 名）、P3TestMod 三个文件夹。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
