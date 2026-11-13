# P5Joker 0.5.4 编译安装报告

- 源提交（git log -1 --oneline）：`9aeaa34 P5Joker 0.5.4: blue fire along Joker's silhouette while a skill is held`
- 更新结果：pull --ff-only 成功，已更新至指定提交 9aeaa34a（短哈希 9aeaa34）。
- 版本检查：p5mod/P5Joker.json 的 version、p5mod/src/ModEntry.cs 的 Version 均为 0.5.4。
- 最终结果：编译和打包成功；安装失败，已停止。目标目录处于部分更新状态：JSON 为 0.5.4，DLL 和 PCK 仍为此前安装文件。

## 编译结果

命令（工作目录为 repo/p5mod）：

```powershell
& 'C:\Users\kkk\sts2_tools\dotnet\dotnet.exe' build -c Release -p:GameDir="C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2"
```

成功，退出码 0，0 个 warning、0 个 error。新 DLL 已确认存在，大小 144,896 字节。

```text
Determining projects to restore...
  All projects are up-to-date for restore.
  P5Joker -> C:\Users\kkk\sts2_tools\p5_build\repo\p5mod\bin\Release\net9.0\P5Joker.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:03.62
```

## PCK 打包

- stage：`C:\Users\kkk\sts2_tools\p5_build\stage_20261029_070829`
- 工具：`C:\Users\kkk\sts2_tools\gdre\gdre_tools.exe`
- 参数：--headless、--pck-version=3、--pck-engine-version=4.5.1。
- 打包退出码：0。
- PCK：`C:\Users\kkk\sts2_tools\p5_build\P5Joker_20261029_070829.pck`
- PCK 大小：31,737,924 字节（约 30.27 MiB），大于 5 MB。

## 备份与安装状态

备份位置：`C:\Users\kkk\sts2_tools\backup\P5Joker_20261029_070829\`

原 mods/P5Joker 全部内容（包含隐藏内容）已复制到新备份目录。JSON 已复制成功；复制 DLL 失败后立即停止，未复制 PCK，未重试、未处理占用进程、未回滚文件。

停止位置：第 4 步第 2 条，复制 DLL。命令：

```powershell
Copy-Item -LiteralPath 'C:\Users\kkk\sts2_tools\p5_build\repo\p5mod\bin\Release\net9.0\P5Joker.dll' -Destination 'C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker\P5Joker.dll'
```

退出码 1，完整报错：

```text
Copy-Item: 
Line |
   2 |  Copy-Item -LiteralPath 'C:\Users\kkk\sts2_tools\p5_build\repo\p5mod\b …
     |  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
     | The process cannot access the file 'C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker\P5Joker.dll' because it is being used by another process.
```

工具返回的命令行显示本身带省略号；上方另列完整执行命令。

目标目录：`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker\`

停止后只读列出的全部文件（America/Chicago，UTC-05:00）：

| 文件名 | 大小（字节） | 修改时间 |
| --- | ---: | --- |
| P5Joker.dll | 124416 | 2026-10-29 06:43:12 -05:00 |
| P5Joker.json | 541 | 2026-10-29 07:07:42 -05:00 |
| P5Joker.pck | 31737924 | 2026-10-29 06:43:39 -05:00 |

## 执行说明

所有命令分别执行；未修改代码、未生图、未安装工具、未删除文件、未修改全局 git config 或系统环境变量/PATH、未进入旧仓库、未读取禁止文件或密码密钥。游戏目录只执行了指定文件复制。未使用 Computer Use，未启动游戏，未做实机验收。

