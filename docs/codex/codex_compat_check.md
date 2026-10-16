你在用户的 Windows 电脑上工作。任务：把 P3Makoto v0.14.44、音乐盒 P3MusicBox 1.0.1、测试工具 P3TestMod 1.2.1 编译、打包、安装到游戏 mods 目录，再用仓库里的「版本兼容检查工具」检查这三个 mod 和本机游戏版本是否匹配。只做编译、打包、安装、检查，不改任何代码。

【背景】
- Claude 在云端写代码，推送到 GitHub 仓库 4yndm7wh95-spec/persona 的分支 claude/amazing-darwin-b00arn。你负责在本地编译、打包、安装和检查。
- v0.14.44 做了多版本兼容加固：
  - 补丁出错时退回原版；
  - 控制台命令 p3seal 从 P3Makoto 移到了 P3TestMod；
  - 新增兼容检查工具 tools\compat\ApiCheck。
- 音乐盒 1.0.1 修复了"播放暂停没反应"：
  - 音量被主音量乘了两遍；
  - 点击可能被游戏界面吃掉。

【1. 找到仓库】
在本机找到满足以下条件的 git 仓库（用你上次成功编译的那个），下文记作 <repo>：
- remote 指向 github.com/4yndm7wh95-spec/persona；
- 仓库里有 mod\P3Makoto.json。

所有 git 命令都加 -c safe.directory=<repo>。

稀疏检出缺目录时，允许执行：
git -c safe.directory=<repo> sparse-checkout add mod musicbox testmod tools/compat
- 路径过长时，这一条命令可以加 -c core.longpaths=true。
- 不要 sparse-checkout disable / set。

【2. 拉取最新代码】
1. 执行 git -c safe.directory=<repo> status -sb。有进行中的合并或未提交改动就停下来报告。
2. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
3. 合并：先执行
   git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn
   不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   - 有冲突就停下来报告。
   - 这次不需要 push。
4. 确认 git log 里有提交「v0.14.44: multi-version hardening …」。

【3. 确认游戏没在运行】
有杀戮尖塔 2 在运行就停下来报告，不要强行结束它。

【4. 编译 P3Makoto 并打包、安装】（做法和以前一样）
1. 编译：在 <repo>\mod 下执行 dotnet build P3Makoto.csproj -c Release。
   - 用上次能用的 dotnet，例如 C:\Users\kkk\sts2_tools\dotnet\dotnet.exe。
   - 游戏目录用上次编译时的那个。
   - 失败就把完整错误原样贴出来，不要改代码。
2. 打包 pck：
   - 把 <repo>\mod\pck_stage 整个复制到 <repo>\mod\_build\stage_<时间戳>\（包括 .godot，可以用 robocopy /E）。
   - 核对 p3makoto\sfx\ 下有 90 个 .ogg。
   - 用 GDRE Tools 打包：
     <gdre_tools.exe> --headless --pck-create=<stage目录> --output=<repo>\mod\_build\P3Makoto.pck --pck-version=3 --pck-engine-version=4.5.1
3. 安装到 <游戏目录>\mods\P3Makoto\：
   - 先把旧的三个文件备份到 <repo>\mod\_build\backup_<时间戳>\。
   - 再复制 P3Makoto.json、新的 dll、新的 pck。
   - 核对安装目录里 P3Makoto.json 的 version 是 "0.14.44"。

【5. 编译、安装音乐盒和测试工具】
1. 音乐盒：在 <repo>\musicbox 下执行 dotnet build -c Release -p:GameDir="<游戏目录>"。
   - 把 bin\Release\net9.0\P3MusicBox.dll 和 <repo>\musicbox\P3MusicBox.json 复制到 <游戏目录>\mods\P3MusicBox\。
   - **不要动那里的 music\ 文件夹。**
   - 核对 version 是 "1.0.1"。
2. 测试工具：只在 <游戏目录>\mods\P3TestMod\ 已经存在时才处理（不存在就跳过，并在报告里说明）。
   - 在 <repo>\testmod 下执行 dotnet build -c Release -p:GameDir="<游戏目录>"。
   - 把 P3TestMod.dll 和 P3TestMod.json 复制过去。
   - 核对 version 是 "1.2.1"。

【6. 版本兼容检查】
1. 在 <repo> 下执行（第一次运行会自动编译工具）：
   dotnet run -c Release --project tools\compat\ApiCheck -- <三个 DLL 和游戏 sts2.dll，见下>
   参数顺序：
   1. 第一个：P3Makoto 的 dll（刚编译出来的那个）；
   2. 第二个：<游戏目录>\data_sts2_windows_x86_64\sts2.dll；
   3. 之后：P3MusicBox.dll，以及编译了测试工具时的 P3TestMod.dll。
   - 编译工具时如果需要从 NuGet 还原 .NET 的引用包，允许联网还原，但不要安装别的东西。
2. 输出最后一行应为「结果：全部能找到 ✓」。有 ✗ 的行就原样贴进报告。
3. 读本机游戏的版本号：读 <游戏目录>\release_info.json，只读不改，写进报告。

【7. 报告】
用中文报告：
- 合并到的提交号；
- 三个 mod 是否都编译成功、各自的警告摘要；
- pck 大小、sfx 数量；
- 安装路径和版本号；
- 游戏版本号；
- ApiCheck 的完整输出。

再附上给用户的测试要点：
1. 先在游戏「模组」设置里确认三个 mod 都是启用状态。
2. 音乐盒：
   - 左键圆按钮应开始播放，原版音乐淡出；音量应和游戏音效差不多大，不再是几乎听不见。
   - 再左键暂停，原版音乐淡回。
   - 右键打开歌单，点空白处关闭。
   - 如果还是没反应：把游戏日志 godot.log 里含 "P3MusicBox" 的行原样发给 Claude。每次点击会有一行 "click left via gui/poll"，每次播放会有一行 "play 文件名"。
3. P3Makoto 一切照旧。游戏日志开头会多一行 "[P3Makoto] game version …"，以及 "harmony patches applied: N ok, 0 failed"。
4. 控制台命令 p3seal 现在属于测试工具。只装 P3Makoto 时控制台里没有它，这是正常的。

【以后想检查别的游戏版本时（这次不用做）】
1. 用户在 Steam 里把游戏切到别的版本（库 → 右键杀戮尖塔 2 → 属性 → 测试版）。
2. 等 Steam 更新完，重新跑第 6 步：
   - 有 ✗ 的就把输出发给 Claude 修；
   - 全部能找到 ✓ 就基本可以直接用。

【绝对不要做】
- 不要修改或删除代码；不要 rebase / reset / amend；不要 --force；不要改全局 git config；不要提交，也不要推送。
- 不要启动游戏。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（release_info.json、sts2.dll 都只读）。游戏目录里只允许写 mods\P3Makoto\、mods\P3MusicBox\（不碰其中的 music\）、mods\P3TestMod\。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
