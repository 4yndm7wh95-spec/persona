P5Joker 0.9.15 编译安装报告

结果：DLL 编译、PCK 打包、备份和安装完成，安装的三个文件 SHA256 均与源文件一致。

构建提交（git log -1 --oneline）：
4c8eaae6 Merge remote-tracking branch 'origin/claude/epic-archimedes-m9an69' into claude/epic-archimedes-m9an69
分支：claude/epic-archimedes-m9an69

执行前检查：
已按指定 refspec fetch，并完成 merge --ff-only。
未发现 Slay the Spire 2 进程。
P5Joker.json 和 ModEntry.cs 的版本均为 0.9.15。

编译结果：
dotnet build -c Release
Build succeeded.
1 Warning(s)
0 Error(s)
输出 p5mod\bin\Release\net9.0\P5Joker.dll，651264 字节。

PCK：
本次新建 C:\Users\kkk\sts2_tools\tmp\p5_stage_0.9.15。
将 p5mod\pck_stage 全部内容复制进去，按文档运行 gdre，退出码 0。
输出 C:\Users\kkk\sts2_tools\tmp\P5Joker_0.9.15.pck。
大小 58086852 字节，大于 5 MB。

旧版安装备份：
C:\Users\kkk\sts2_tools\backup\P5Joker_20261108_021745\

安装目录：
C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker\

安装后文件列表（America/Chicago 本地时间）：
P5Joker.dll   651264 字节，2026-11-08 02:17:08
P5Joker.json     682 字节，2026-11-08 02:16:47
P5Joker.pck 58086852 字节，2026-11-08 02:17:25

源文件与安装文件 SHA256 一致：
JSON 400B3A88739A7CC24C410CD8F5DAD3E42035D901C91BCE8B3ED206AB6865F3E8
DLL  2D04C5B59E9B1EE1C5C320DC9DF2BC4C96E9F7D0296663A6F64B443DFD1F5396
PCK  FAEC9E2E8131C859ED89ED43703AB1A83D03B90A2DCF27FCC0903EE19ABA9F6F

源码和已有仓库资源未修改，原有未跟踪 art/p5/fx/gen_codex/ink/ 保持原样。
未生图、未启动游戏、未使用 Computer Use。游戏内验收由用户进行。