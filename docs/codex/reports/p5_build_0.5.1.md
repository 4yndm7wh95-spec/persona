# P5Joker 0.5.1 编译安装报告

- 源提交（git log -1 --oneline）：`6d93d0a P5Joker 0.5.1: mask turned with the 3/4 face, no mosaic, no fire left behind`
- 更新：pull --ff-only 成功，最新提交为指定的 6d93d0a6（短哈希显示为 6d93d0a）。
- 版本检查：p5mod/P5Joker.json 的 version、p5mod/src/ModEntry.cs 的 Version 均为 0.5.1。

## 编译

工作目录：`C:\Users\kkk\sts2_tools\p5_build\repo\p5mod`

命令：

```powershell
& 'C:\Users\kkk\sts2_tools\dotnet\dotnet.exe' build -c Release -p:GameDir="C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2"
```

结果：成功，退出码 0；0 个 warning、0 个 error。DLL 已确认存在，大小 122,880 字节。

```text
Determining projects to restore...
  All projects are up-to-date for restore.
  P5Joker -> C:\Users\kkk\sts2_tools\p5_build\repo\p5mod\bin\Release\net9.0\P5Joker.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.41
```

## 打包

- stage：`C:\Users\kkk\sts2_tools\p5_build\stage_20261029_062719`
- 工具：`C:\Users\kkk\sts2_tools\gdre\gdre_tools.exe`
- 参数：--headless、--pck-version=3、--pck-engine-version=4.5.1。
- 退出码：0。
- PCK：`C:\Users\kkk\sts2_tools\p5_build\P5Joker_20261029_062719.pck`
- PCK 大小：31,737,924 字节（约 30.27 MiB），大于 5 MB。

## 备份与安装

备份位置：`C:\Users\kkk\sts2_tools\backup\P5Joker_20261029_062719\`

原 mods/P5Joker 全部内容（包含隐藏内容）已复制到新备份目录。已将新 JSON、DLL 和 PCK 覆盖复制至：

`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker\`

安装目录全部文件列表（America/Chicago，UTC-05:00）：

| 文件名 | 大小（字节） | 修改时间 |
| --- | ---: | --- |
| P5Joker.dll | 122880 | 2026-10-29 06:27:10 -05:00 |
| P5Joker.json | 541 | 2026-10-29 06:26:49 -05:00 |
| P5Joker.pck | 31737924 | 2026-10-29 06:27:37 -05:00 |

## 执行说明

更新、版本检查、编译、打包、备份和安装均成功，未出现命令报错或拒绝。所有命令分别执行；未修改代码、未生图、未安装工具、未删除文件、未修改全局 git config 或系统环境变量/PATH、未进入旧仓库、未读取禁止文件或密码密钥。游戏目录仅向 mods/P5Joker 复制了指定三个文件。未使用 Computer Use，未启动游戏，未做实机验收，由用户自行验收。

