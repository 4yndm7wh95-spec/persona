你在用户的 Windows 电脑上工作。任务：把 P3Makoto v0.14.87、音乐盒 P3MusicBox 1.1.3、测试工具 P3TestMod 1.2.2 编译、打包，安装到本机**两个**杀戮尖塔 2（0.107.1 正式版和 0.111 测试版），再用兼容检查工具对两个版本各检查一遍。只做编译、打包、安装、检查，不改任何代码。

【两个游戏目录】
- <G111> = C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2（以前编译用的那个）。
- <G107> = C:\Users\kkk\Downloads\Slay the Spire 2。
- 先只读两边的 release_info.json，确认版本分别是 v0.111.x 和 v0.107.1；对不上就停下来报告。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功编译的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. 执行 git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/ 原样保留、忽略它们继续（不要移动、删除或提交）。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并：先执行
   git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn
   不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   - 有冲突就停下来报告。
   - 这次不需要 push。
6. 确认 git log 最新的提交是 v0.14.87（P3R 风格设置界面整体撤下；塔罗卡框照旧）。
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
   另外（v0.14.87 新增）：p3makoto\card_frame\ 下有 19 张 png（card_frame_attack_s / skill_s / power_s、card_banner_*、card_portrait_border_*、arcana_tab）及各自的 .import；p3makoto\cards\ 下有 6 张 png（strike_regent、defend_regent、falling_star、big_bang、tyranny、the_smith）及 .import；.godot\imported\ 下有对应的 .ctex；p3makoto\fonts\ 下有 Cinzel-Bold.ttf。
3. 用 GDRE Tools 打包：
   <gdre_tools.exe> --headless --pck-create=<stage目录> --output=<repo>\mod\_build\P3Makoto.pck --pck-version=3 --pck-engine-version=4.5.1

【5. 安装到两个游戏（<G111> 和 <G107> 各做一遍）】
1. P3Makoto：
   - 目标是 <游戏>\mods\ 下当前装着 P3Makoto.json 的那个文件夹（可能是嵌套的 mods\P3Makoto\P3Makoto\，以实际位置为准）；没有就新建 <游戏>\mods\P3Makoto\。
   - 先把旧的 P3Makoto.json / .dll / .pck 备份到 <repo>\mod\_build\backup_<时间戳>_<107或111>\。
   - 再复制新的 P3Makoto.json、dll、pck。
   - 核对 version 是 "0.14.87"。
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

【8. 实机复验（精简；用户已授权启动游戏做 UI 验收；只在安装完成后进行）】
- 用 <G111>（0.111）。不改游戏原文件，不碰存档；改过的设置测完改回原值（先记下原值）。截图存 <repo>\docs\qa\v0.14.87\。
R1 塔罗卡框（这次的重点）：
   - 先确认 P3MAKOTO 那一栏里有「卡框（P3R 塔罗牌风格）」这一行，默认开着。
   - 主菜单开一局储君（标准模式），进第一场战斗：截手牌（cards_hand.png），能看到石板钢蓝的卡框、卡名横幅、底部罗马数字牌；
     打击 / 防御 / 陨星 / 铸剑者 / 暴政 / 大爆炸 这几张有重绘卡图（P3R 风格），其他卡还是原版卡图。
   - 打开抽牌堆，点一张牌放大截图（card_zoom.png）；再找一张罕见、一张稀有的卡（牌组里没有就在结算选卡界面看）各截一张：横幅分别是 银白 / 冰蓝 / 暗金。
   - 在休息处或用测试工具升级一张牌，放大截图：底部罗马数字变成金色（card_upgraded.png）。
   - 战斗后结算选卡截一张（card_reward.png）。
   - 设置里把「卡框（P3R 塔罗牌风格）」关掉，回到战斗看手牌：应变回原版橙色卡框（cards_vanilla.png）；测完改回开着。
   - 卡名应是白色衬线字（中文思源宋体）+ 深蓝描边，字后面垫一块边缘羽化的深色柔光（不是硬边），在银白横幅上也看得清；升级后的卡卡名是浅青绿色。
   - 观察：卡框和卡图之间有没有缝、错位、残留的橙色 / 青色；卡名、描述、费用文字是否清楚；有没有掉帧。
R1b 设置界面应是**原版**样式：主菜单 → 设置截图（settings_vanilla.png）；P3MAKOTO 那一栏里**没有**「设置界面（P3R 主菜单风格）」这一行。
R2 其他角色（卡框不应变）：主菜单开一局非储君角色（例如铁甲战士，标准模式，进第一场战斗即可），截图左下抽牌堆 / 右下弃牌堆：
   应是原版样式，**不应**出现深蓝红色的数字小牌；手牌的卡框也应该是原版（塔罗卡框只给储君的卡）。然后放弃这局回主菜单（放弃前确认这是新开的测试局）。
R3 储君局：战斗里打几回合（防御多段攻击、出几张攻击牌），确认左下 / 右下牌堆是 P3R 数字牌样式；血条右边的「状态」按钮能打开状态列表；
   打开抽牌堆，点一张牌放大，右下角「查看升级」按钮能翻面；战斗后结算选卡一次。各截一张。
R4 手柄（有手柄才测，没有就跳过并说明）：战斗里用手柄打开抽牌堆，应是 P3R 牌堆界面和动画（不是原版牌堆界面）。
R5 主菜单 ↔ 选人界面来回切 3 次（选储君 → 返回），日志里不应再有 "ObjectDisposedException"。
R6 日志：把含 "P3Makoto" 且含 "error" / "failed" / "Exception" / "health" 的行（包括 health gpu_ms 和 health hitch）保存到 docs\qa\v0.14.87\runtime_filtered.log。
R7 截图、录屏、日志和 qa_report.md（中文：每项通过 / 不通过、看到的问题）提交并推送：
   git -c safe.directory=<repo> add docs/qa/v0.14.87
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "qa: v0.14.87 tarot card frame build check"
   然后按第 1 步的 fetch / merge 方式和重试规则 push（不要 rebase、不要 --force）。只提交 docs/qa/v0.14.87 下的文件。
（局内暂停菜单、0.107.1、手柄等布局通过后再测。）

再附上给用户的测试要点：
1. 换其他角色时左下 / 右下牌堆是原版样式，不再出现数字小牌。
2. 储君局里防御多段、放大剑、结算选卡如果还卡，把日志里含 "health" 的行（特别是 health hitch / gpu_ms）发给 Claude。
3. 储君的卡牌换成 P3R 塔罗牌卡框（底部罗马数字是这张卡对应的大阿卡纳，升级后变金）；有 6 张卡已经换了 P3R 风格卡图，其余卡图陆续换。设置里可以关掉。
4. 日志开头应有 "[P3Makoto] init v0.14.87"。

【绝对不要做】
- 不要修改或删除代码；不要 rebase / reset / amend；不要 --force；不要改全局 git config；除了第 8 步 R7 的 docs/qa/v0.14.87，不要提交或推送任何东西。
- 启动游戏只用于上面第 8 步的 UI 验收（用户已授权）；不要修改存档，测试中改过的设置要改回原值。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（release_info.json、sts2.dll 都只读）。游戏目录里只允许写 mods\ 下 P3Makoto、P3MusicBox（歌曲和歌词不动，只改 playlist 名）、P3TestMod 三个文件夹。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
