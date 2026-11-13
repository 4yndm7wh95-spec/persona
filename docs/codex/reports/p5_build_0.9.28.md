# P5Joker 0.9.28 编译安装报告

日期：2026-11-13（America/Chicago）
结果：编译、PCK 打包、备份及安装成功。未修改代码，未启动游戏，未做实机验收。

## 分支占用处理与构建源

用户在前两次失败报告后明确要求“你自己解决吧”。检查发现目标分支所在的既有工作树 C:\Users\kkk\sts2_tools\p5_build\repo_0920 干净，因此在此工作树执行完整 fetch 和 merge --ff-only，不改动原 repo 的分支及既有暂存内容，也不强制解除工作树占用。

构建前 git log -1 --oneline 输出：

```text
4ec297c8 Codex: P5Joker 0.9.28 编译安装报告
```

该提交包含此前报告；版本代码提交为 2ee0244e168a71bd32f43bd93f6b60f505ebf4b4。P5Joker.json 的 version 与 src/ModEntry.cs 的 Version 均确认是 0.9.28。

## 编译

游戏进程检查无输出。

在 C:\Users\kkk\sts2_tools\p5_build\repo_0920\p5mod 执行：

```powershell
& 'C:\Users\kkk\sts2_tools\dotnet\dotnet.exe' build -c Release
```

退出码 0，Build succeeded，3 条 warning、0 个 error。

DLL 已生成：C:\Users\kkk\sts2_tools\p5_build\repo_0920\p5mod\bin\Release\net9.0\P5Joker.dll，1,321,472 字节。

## PCK 与备份

新建临时目录 C:\Users\kkk\sts2_tools\tmp\p5_stage_0.9.28，复制 pck_stage 全部内容后执行任务单指定 gdre 命令，退出码 0。

PCK：C:\Users\kkk\sts2_tools\tmp\P5Joker_0.9.28.pck
大小：67,864,132 字节，大于 5 MB。

安装前完整复制既有 mod 至：
C:\Users\kkk\sts2_tools\backup\P5Joker_20261113_075809

## 安装后文件列表

目录：C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker\

| 文件名 | 大小（字节） | 修改时间（America/Chicago） |
| --- | ---: | --- |
| P5Joker.dll | 1321472 | 2026-11-13 07:57:19 |
| P5Joker.json | 682 | 2026-11-13 07:56:19 |
| P5Joker.pck | 67864132 | 2026-11-13 07:57:55 |

三个安装文件的 SHA-256 均与源文件一致：

```text
P5Joker.json 9B45BF0E1B03B487C72EAE0C9B782872112ADE2884E35C4B948862F649EFAE5D
P5Joker.dll  17C427F48F4248ACB0442249AC2236263B31DF4DAB8F6183334AFBA826F3BD28
P5Joker.pck  F16601C54D9BC6FBC8587FB0304F76618463DB3F5058839FF790FFFFB59A4588
```

本次只覆盖指定的三个安装文件，未删除文件，未修改其他 mod 或游戏原文件。用户自行启动游戏验收。