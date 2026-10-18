你在用户的 Windows 电脑上工作。任务：把 P3Makoto v0.14.71、音乐盒 P3MusicBox 1.1.3、测试工具 P3TestMod 1.2.2 编译、打包，安装到本机**两个**杀戮尖塔 2（0.107.1 正式版和 0.111 测试版），再用兼容检查工具对两个版本各检查一遍。只做编译、打包、安装、检查，不改任何代码。

【两个游戏目录】
- <G111> = C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2（以前编译用的那个）。
- <G107> = C:\Users\kkk\Downloads\Slay the Spire 2。
- 先只读两边的 release_info.json，确认版本分别是 v0.111.x 和 v0.107.1；对不上就停下来报告。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功编译的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. 执行 git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/ 是之前保留的素材，原样保留、忽略它继续（不要移动、删除或提交它）。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并：先执行
   git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn
   不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   - 有冲突就停下来报告。
   - 这次不需要 push。
6. 确认 git log 最新的提交是 v0.14.71（换其他角色时牌堆数字修复、显卡侧掉帧保护和显卡耗时日志；之前是 cc7148a v0.14.70 设置界面入水转场）。
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
2. 核对 p3makoto\ui\ 下有 system_portrait.png、system_portrait_half.png、system_portrait_closed.png 及各自的 .import，.godot\imported\ 下有三个对应的 .ctex；p3makoto\sfx\ 下有 92 个 .ogg（新增 sys_dive.ogg、sys_blob.ogg）；p3makoto\pile_deck、pile_discard、pile_exhaust 下各有 *_skel_data.tres；p3makoto\pile\ 下有 pile_skel_data.tres、pile.atlas.import、pile.spine-json.import；p3makoto\rig\characters\ 下有 regent_skel_data.tres 和 regent.atlas.import；.godot\imported\ 下有两个文件名含 p3private 的 .spatlas。
3. 用 GDRE Tools 打包：
   <gdre_tools.exe> --headless --pck-create=<stage目录> --output=<repo>\mod\_build\P3Makoto.pck --pck-version=3 --pck-engine-version=4.5.1

【5. 安装到两个游戏（<G111> 和 <G107> 各做一遍）】
1. P3Makoto：
   - 目标是 <游戏>\mods\ 下当前装着 P3Makoto.json 的那个文件夹（可能是嵌套的 mods\P3Makoto\P3Makoto\，以实际位置为准）；没有就新建 <游戏>\mods\P3Makoto\。
   - 先把旧的 P3Makoto.json / .dll / .pck 备份到 <repo>\mod\_build\backup_<时间戳>_<107或111>\。
   - 再复制新的 P3Makoto.json、dll、pck。
   - 核对 version 是 "0.14.71"。
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
- 用 <G111>（0.111）。不改游戏原文件，不碰存档；改过的设置测完改回原值（先记下原值）。截图存 <repo>\docs\qa\v0.14.71\。
R1 入场（重点）：主菜单 → 设置。用 OBS 以外的方式录屏（例如 Win+Alt+R 的 Xbox Game Bar，或连续截图）录下打开后的前 1.5 秒，保存为 docs\qa\v0.14.71\entry.mp4（录不了就在 0.1s / 0.3s / 0.5s / 1.0s 附近各截一张）。
   应看到：顶部中间炸开浅青水花 → 深蓝水团 V 形往下冲、盖满整屏，里面一根根浅青锯齿水柱往下长 → 人物连同白色部分从左上方斜着沉入、停稳，水柱往上缩回 → 菜单字依次浮现，红色选中条最后展开，右边面板从右淡入；全程有空心 / 实心气泡往上冒。
   听：打开时有一声入水"噗"和一串气泡声，和画面同步。
R2 静止画面（等 3 秒截图）：
   - 中间斜排大字 GENERAL / GRAPHICS / SOUND / INPUT / P3MAKOTO 整列都在右边深蓝面板的左边，不重叠、行距不挤；
   - 人物脸右边**没有**那只多出来的手；画面最下边**没有**一条白缝（等 10 秒再截一张底部特写确认）；
   - 两只眼睛都会眨（被头发半挡住的那只也眨），眼睑是从睫毛那一侧往上合；连拍或录 6 秒确认。
R3 依次点 GRAPHICS / SOUND / INPUT / P3MAKOTO / GENERAL：每次从点的字涌出一团边缘起伏的半透明深蓝水团盖满整屏，再从中间撑开一个白边的洞露出新的一页；各截一张（尽量截到水团和白边洞）。
R4 点击命中：SOUND 页把音乐滑条从当前值拖动一点（截图，数值应变化、页面不能跳走），再拖回原值；GRAPHICS 页点开分辨率下拉框（截图），点外面关掉；
   GENERAL 页切换"显示计时器"勾选框再切回；INPUT 页滚动键位列表到底再回顶，不改任何按键。
R5 P3MAKOTO 页关掉「设置界面（P3R 主菜单风格）」→ 关闭设置 → 重开：原版设置（截图）；再打开开关 → 关闭 → 重开，恢复 P3R 风格（截图）。
R5b 其他角色牌堆（v0.14.71 修复）：主菜单开一局非储君角色（例如铁甲战士，标准模式，进第一场战斗即可，不要打完、不要改存档以外的东西），
   截图左下抽牌堆 / 右下弃牌堆：应是原版样式，**不应**出现深蓝红色的数字小牌。然后放弃这局回主菜单（放弃前确认这是新开的测试局）。
R5c 储君局战斗里打几回合（防御多段攻击、出几张攻击牌），确认左下 / 右下牌堆是 P3R 数字牌样式；战斗后结算选卡一次。
R6 日志：把含 "[P3Makoto] settings layout" 的行和含 "P3Makoto" 且含 "settings" / "skin" / "error" / "failed" / "health" 的行（包括新增的 health gpu_ms 和 health hitch）保存到 docs\qa\v0.14.71\runtime_filtered.log。
R7 截图、录屏、日志和 qa_report.md（中文：每项通过 / 不通过、看到的问题）提交并推送：
   git -c safe.directory=<repo> add docs/qa/v0.14.71
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "qa: v0.14.71 settings screen re-test"
   然后按第 1 步的 fetch / merge 方式和重试规则 push（不要 rebase、不要 --force）。只提交 docs/qa/v0.14.71 下的文件。
（局内暂停菜单、0.107.1、手柄等布局通过后再测。）

再附上给用户的测试要点：
1. 设置界面（主菜单和暂停菜单都一样）：打开时先是 P3R 的入水转场（水花 → 深蓝水团冲下 → 人物倒挂沉入），左边白底上倒挂入水的结城理（两只眼都会眨），最左竖排 SYSTEM；中间斜排的大字 GENERAL / GRAPHICS / SOUND / INPUT / P3MAKOTO，点哪个从那个字涌出深蓝水团、再撑开白边的洞换页；右边是设置内容。
2. 所有原版设置照常可用；LB/RB 和手柄方向键照常。
3. 设置里 P3MAKOTO 页的「设置界面（P3R 主菜单风格）」可以关掉恢复原版（下次打开生效）。
4. 日志开头应有 "[P3Makoto] init v0.14.71"。有问题请把含 "P3Makoto" 的行发给 Claude。

【绝对不要做】
- 不要修改或删除代码；不要 rebase / reset / amend；不要 --force；不要改全局 git config；除了第 8 步 R7 的 docs/qa/v0.14.71，不要提交或推送任何东西。
- 启动游戏只用于上面第 8 步的 UI 验收（用户已授权）；不要修改存档，测试中改过的设置要改回原值。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（release_info.json、sts2.dll 都只读）。游戏目录里只允许写 mods\ 下 P3Makoto、P3MusicBox（歌曲和歌词不动，只改 playlist 名）、P3TestMod 三个文件夹。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
