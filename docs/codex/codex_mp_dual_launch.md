你在用户的 Windows 电脑上工作。任务：编译安装 P3Makoto v0.14.36 和测试工具 P3TestMod v1.2.0，备份存档，然后**启动两个杀戮尖塔 2 实例**给用户做本机联机测试（用户自己操作联机，你只负责启动）。

【背景】
测试工具 v1.2 支持联机：会改游戏状态的按钮都走原版控制台的联机同步通道，每个游戏进程单独写一份多人日志（%APPDATA%\SlayTheSpire2\logs\p3_mp\）。
原版自带本机联机测试参数 --fastmp：联机不走 Steam，改用本机 ENet（127.0.0.1:33771）。房主不加 clientId，客机加 --clientId 1001。
原版的 -wpos x y 参数会把游戏放成窗口并摆到指定位置。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（用你上次成功编译的那个），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
1. git -c safe.directory=<repo> status -sb：确认没有进行中的合并，也没有未提交的改动。有的话停下来报告。
2. fetch：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次）。
3. 合并：git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn；不能快进就改用 git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn。有冲突就停下来报告。这次不需要 push。
如果仓库是稀疏检出、testmod 目录不在磁盘上：允许执行 git -c safe.directory=<repo> sparse-checkout add testmod；路径过长报错时允许单次命令加 -c core.longpaths=true。不要 sparse-checkout disable / set。

【编译安装】
4. 先确认没有杀戮尖塔 2 在运行（tasklist 里找游戏进程）；有的话停下来报告，不要强行结束。
5. P3Makoto：按原流程编译、打包、安装，版本号应为 0.14.36。
6. P3TestMod：在 <repo>\testmod 里 dotnet build -c Release -p:GameDir="<游戏安装目录>"（游戏目录用你编译 P3Makoto 时用的那个），
   把 bin\Release\net9.0\P3TestMod.dll 和 <repo>\testmod\P3TestMod.json 复制到 <游戏安装目录>\mods\P3TestMod\（覆盖旧版）。确认 json 里 version 是 1.2.0。

【备份存档】
7. 把 C:\Users\kkk\AppData\Roaming\SlayTheSpire2\ 整个复制到 C:\Users\kkk\sts2_tools\save_backup_<yyyyMMdd_HHmmss>\（logs 子目录可以不复制）。两个游戏进程会共用同一个存档目录，这份备份是保险。报告备份位置和大小。

【启动两个实例】（这次允许启动游戏，但只做下面这些，不要在游戏里点任何东西）
8. 找到游戏主程序（<游戏安装目录> 下的杀戮尖塔 2 主 exe），用 Start-Process 启动，工作目录设为游戏安装目录：
   - 实例 A（房主）：参数 --fastmp -wpos 0 0
   - 等 20 秒，确认进程还在、窗口出来了，再启动：
   - 实例 B（客机）：参数 --fastmp --clientId 1001 -wpos 960 0；启动前给这个进程设环境变量 P3_BRIDGE=0（关掉测试工具的本机接口，否则和实例 A 端口冲突）。
     PowerShell 示例：$env:P3_BRIDGE='0'; Start-Process -FilePath <exe> -ArgumentList '--fastmp','--clientId','1001','-wpos','960','0' -WorkingDirectory <游戏目录>; Remove-Item Env:P3_BRIDGE
9. 再等 20 秒，确认两个进程都在运行。看 C:\Users\kkk\AppData\Roaming\SlayTheSpire2\logs\p3_mp\ 下是否新出现了两个 p3mp_*_pid<进程号>.log，每个文件开头应有 "P3TestMod 1.2.0 log start" 和各自的启动参数。
10. 如果某个实例弹出 Steam 初始化失败的提示，或者进程自己退出了：不要反复重启。把两个实例都关掉（正常关闭窗口），改为两个实例都额外加参数 --force-steam off 再按第 8~9 步启动一次（这样不连 Steam，会用一个单独的本地档，不会碰 Steam 存档）；还是不行就停下来报告，把日志里相关的几行（敏感信息用 *** 代替）发出来。

【报告】中文：P3Makoto / P3TestMod 的提交号、编译结果、安装版本；存档备份位置；两个实例的进程号、启动参数、各自的多人日志文件路径；有没有用 --force-steam off。

【绝对不要做】
- 不要修改或删除代码；不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 除了第 8~10 步启动两个实例外，不要做任何游戏内操作（不要点按钮、不要联机、不要自动化操作鼠标键盘）；不要强行结束游戏进程。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只往 mods 目录里放 / 覆盖本 mod 的文件）；不要往游戏目录里新建其他文件（比如 steam_appid.txt）。
- 不要删除或改动存档目录里的文件（只复制做备份）。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
