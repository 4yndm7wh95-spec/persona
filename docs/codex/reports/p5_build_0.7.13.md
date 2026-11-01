# P5Joker 0.7.13 编译安装报告

- 源提交（git log -1 --oneline）：`60c7060 P5Joker 0.7.13: curse hands grow, slap together and sink back, with staged hand poses`
- 分支：claude/p5r-ren-slay-the-spire-mod-6tk3f3。
- 游戏进程检查：按文档指定命令执行，无输出。
- fetch、checkout 和 merge --ff-only 均成功。
- 版本检查：P5Joker.json 的 version、ModEntry.cs 的 Version 均为 0.7.13。

## 编译结果

在 `C:\Users\kkk\sts2_tools\p5_build\repo\p5mod` 执行：

```powershell
& 'C:\Users\kkk\sts2_tools\dotnet\dotnet.exe' build -c Release
```

编译成功，退出码 0，0 个 warning、0 个 error。DLL 存在，大小 452,608 字节。

```text
Determining projects to restore...
  All projects are up-to-date for restore.
  P5Joker -> C:\Users\kkk\sts2_tools\p5_build\repo\p5mod\bin\Release\net9.0\P5Joker.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:04.29
```

## PCK

- 临时目录：`C:\Users\kkk\sts2_tools\tmp\p5_stage_0.7.13`，确认不存在后新建，复制了 pck_stage 全部内容（包含隐藏内容）。
- 工具：`C:\Users\kkk\sts2_tools\gdre\gdre_tools.exe`
- 参数：--headless、--pck-version=3、--pck-engine-version=4.5.1。
- 打包成功，退出码 0。
- 产物：`C:\Users\kkk\sts2_tools\tmp\P5Joker_0.7.13.pck`
- 大小：45,864,804 字节，大于 5 MB。

## 备份与安装

备份位置：`C:\Users\kkk\sts2_tools\backup\P5Joker_20261101_092505\`

原 P5Joker 目录全部内容（包含隐藏内容）已复制备份。三个文件均已成功覆盖复制到：

`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker\`

安装目录全部文件列表（America/Chicago，UTC-06:00）：

| 文件名 | 大小（字节） | 修改时间 |
| --- | ---: | --- |
| P5Joker.dll | 452608 | 2026-11-01 09:24:26 -06:00 |
| P5Joker.json | 542 | 2026-11-01 09:24:04 -06:00 |
| P5Joker.pck | 45864804 | 2026-11-01 09:24:58 -06:00 |

## 执行说明

更新、编译、打包、备份和安装均成功，没有命令报错。仅新建本报告，未手动修改仓库已有文件或代码；未生图、未安装工具、未删除文件、未修改全局 git config、系统环境变量或 PATH，未进入旧仓库，未读取禁止文件、密码或密钥。每条命令单独执行。游戏目录仅向 mods/P5Joker 复制指定文件。未使用 Computer Use，未启动游戏，未关闭或结束任何进程，未做实机验收，由用户自行验收。

