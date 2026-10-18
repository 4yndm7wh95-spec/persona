你在用户的 Windows 电脑上工作。任务：把 P3Makoto v0.14.67、音乐盒 P3MusicBox 1.1.3、测试工具 P3TestMod 1.2.2 编译、打包，安装到本机**两个**杀戮尖塔 2（0.107.1 正式版和 0.111 测试版），再用兼容检查工具对两个版本各检查一遍。只做编译、打包、安装、检查，不改任何代码。

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
6. 确认 git log 最新的提交是 v0.14.67（设置界面 P3R 主菜单风格重绘；之前是 Codex 的设置立绘 d3192ae）。
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
2. 核对 p3makoto\ui\ 下有 system_portrait.png、system_portrait_half.png、system_portrait_closed.png 及各自的 .import，.godot\imported\ 下有三个对应的 .ctex；p3makoto\sfx\ 下有 90 个 .ogg；p3makoto\pile_deck、pile_discard、pile_exhaust 下各有 *_skel_data.tres；p3makoto\pile\ 下有 pile_skel_data.tres、pile.atlas.import、pile.spine-json.import；p3makoto\rig\characters\ 下有 regent_skel_data.tres 和 regent.atlas.import；.godot\imported\ 下有两个文件名含 p3private 的 .spatlas。
3. 用 GDRE Tools 打包：
   <gdre_tools.exe> --headless --pck-create=<stage目录> --output=<repo>\mod\_build\P3Makoto.pck --pck-version=3 --pck-engine-version=4.5.1

【5. 安装到两个游戏（<G111> 和 <G107> 各做一遍）】
1. P3Makoto：
   - 目标是 <游戏>\mods\ 下当前装着 P3Makoto.json 的那个文件夹（可能是嵌套的 mods\P3Makoto\P3Makoto\，以实际位置为准）；没有就新建 <游戏>\mods\P3Makoto\。
   - 先把旧的 P3Makoto.json / .dll / .pck 备份到 <repo>\mod\_build\backup_<时间戳>_<107或111>\。
   - 再复制新的 P3Makoto.json、dll、pck。
   - 核对 version 是 "0.14.59"。
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

【8. 实机验收（用户已授权启动游戏做 UI 验收；只在安装完成后进行）】
- 用 <G111>（0.111）的游戏验收；时间够再用 <G107> 重复 A、B 两项。
- 不改游戏原文件，不碰存档；测试中改过的任何设置，测完都要改回原来的值（先记下原值）。
- 每一步截图（游戏内截图或系统截图都可以），保存到 <repo>\docs\qa\v0.14.67\，文件名用下面的编号（例如 A1.png）。
A. 主菜单 → 设置：
  A1 刚打开的一瞬间（水花从右往左泼过）；A2 打开约 1 秒后的完整画面；
  A3~A6 依次点画面中间的大字 GRAPHICS / SOUND / INPUT / P3MAKOTO（每次点击后约 0.6 秒截图，应该有深蓝圆形从那个字涨开再退开，然后右边内容换页）；A7 回到 GENERAL；
  A8 鼠标悬停在任意一行设置上（应变成白底 + 错开的红框）；A9 等 6 秒以上看人物是否眨眼（不用截图，在报告里写看到没有）。
B. 功能：
  B1 常规页：切换一个勾选框（例如"显示计时器"），确认勾选状态的 P3R 方框跟着变，关掉设置再打开仍然是新值；然后改回原值。
  B2 画面页：打开一个下拉框（例如分辨率 / 帧率上限），截图下拉列表，按 Esc 或点外面关掉，不要真的改分辨率。
  B3 声音页：拖动一个音量滑条，截图；然后拖回原值。
  B4 按键页：滚动列表到底再回顶，截图；不要改任何按键。
  B5 P3MAKOTO 页：只显示本 mod 那一栏；切换一项再切回原值；截图。
  B6 手柄（如果有手柄）：用 LB/RB 换页，大字应跟着当前页变；方向键能在行之间移动，选中行变白底。
  B7 返回按钮关闭设置正常；再次打开仍有入场动画。
C. 局内：开一局（任意角色），暂停菜单 → 设置，截图 C1；关掉设置回到游戏正常。
D. 关掉「设置界面（P3R 主菜单风格）」开关（在 P3MAKOTO 页），关闭设置再打开，应该是原版设置界面（截图 D1）；然后打开开关，再关闭重开，恢复 P3R 风格（截图 D2）。
E. 日志：把 godot.log 里所有含 "P3Makoto" 且含 "settings" / "skin" / "error" / "failed" / "health spike" 的行原样贴进报告。
F. 截图和一个 qa_report.md（中文：每一项通过 / 不通过、看到的问题）提交并推送：
   git -c safe.directory=<repo> add docs/qa/v0.14.67
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "qa: v0.14.67 settings screen screenshots and report"
   然后按第 1 步的 fetch / merge 方式和重试规则 push 到 claude/amazing-darwin-b00arn（不要 rebase、不要 --force）。只提交 docs/qa/v0.14.67 下的文件。

再附上给用户的测试要点：
1. 设置界面（主菜单和暂停菜单都一样）：左边白底上倒挂入水的结城理（人物右侧的白色透出水），最左竖排 SYSTEM；中间斜排的大字 GENERAL / GRAPHICS / SOUND / INPUT / P3MAKOTO，点哪个从那个字涨出深蓝圆再退开换页；右边是设置内容（每行斜切深蓝底，悬停白底红框，勾选框是 P3R 斜方框）。
2. 所有原版设置照常可用；LB/RB 和手柄方向键照常。
3. 设置里 P3MAKOTO 页的「设置界面（P3R 主菜单风格）」可以关掉恢复原版（下次打开生效）。
4. 日志开头应有 "[P3Makoto] init v0.14.67"。有问题请把含 "P3Makoto" 的行发给 Claude。

【绝对不要做】
- 不要修改或删除代码；不要 rebase / reset / amend；不要 --force；不要改全局 git config；除了第 8 步 F 的 docs/qa/v0.14.67，不要提交或推送任何东西。
- 启动游戏只用于上面第 8 步的 UI 验收（用户已授权）；不要修改存档，测试中改过的设置要改回原值。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（release_info.json、sts2.dll 都只读）。游戏目录里只允许写 mods\ 下 P3Makoto、P3MusicBox（歌曲和歌词不动，只改 playlist 名）、P3TestMod 三个文件夹。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
