P5Joker 0.9.14 编译安装报告

结果：编译、PCK 打包、备份及安装完成，三个安装文件的 SHA256 均与源文件一致。

构建提交：
15189445 docs: Codex 0.9.14 build - fetch with an explicit refspec and create the local branch
完整提交号：15189445f233c4c913b93d0c98e6799637092f87
分支：claude/epic-archimedes-m9an69

分支问题处理：
仅在本仓库配置中追加目标分支的 remote.origin.fetch 映射，再运行指定 switch -c --track 命令成功。此前切换产生的暂存差异随成功切换消失，工作区仅有原来的未跟踪 art/p5/fx/gen_codex/ink/。
指定旧报告备份在 C:\Users\kkk\sts2_tools\backup\untracked_20261108_013113\p5_build_0.8.0.md。

执行前检查：
未发现 Slay the Spire 2 进程。
P5Joker.json 与 ModEntry.cs 的版本均为 0.9.14。

DLL 编译：
dotnet build -c Release
Build succeeded.
1 Warning(s)
0 Error(s)
警告 CS0649，src\Vfx\Ready.cs(37,20)，ReadyFx._aura 从未赋值，默认值为 null。
输出 p5mod\bin\Release\net9.0\P5Joker.dll，647680 字节。

PCK 打包：
临时目录 C:\Users\kkk\sts2_tools\tmp\p5_stage_0.9.14 为本次新建。
复制 p5mod\pck_stage 的全部内容后，按文档运行 gdre，退出码 0。
输出 C:\Users\kkk\sts2_tools\tmp\P5Joker_0.9.14.pck。
大小 58068356 字节，大于 5 MB。

安装备份：
C:\Users\kkk\sts2_tools\backup\P5Joker_20261108_013716\

安装目录：
C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker\

安装后文件列表（本地时间 America/Chicago）：
P5Joker.dll   647680 字节，2026-11-08 01:36:46
P5Joker.json     682 字节，2026-11-08 01:32:14
P5Joker.pck 58068356 字节，2026-11-08 01:37:05

校验：
JSON、DLL、PCK 的安装文件 SHA256 分别与源文件一致。
JSON D0EE25ED45F8E9E8C910C9B9F53B4B847F788106DAA55D9501312015D202AE59
DLL  06E5C27E4B7351E7EF064394C7DDE68A6B94903B89E60F8159298B1211B5D75F
PCK  6846346208C8AC215953172A3188C67725958768121D609193837E90B184CAE7

按要求仅编译安装，游戏内验收由用户进行。源码与已有仓库资源未修改，未生图、未启动游戏、未使用 Computer Use。