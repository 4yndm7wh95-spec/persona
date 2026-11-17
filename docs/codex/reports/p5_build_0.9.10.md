# P5Joker 0.9.10 编译安装报告

- git log -1 --oneline：`ecf29217 Mask flame follows Joker's actual pose: end it the moment he leaves the hand-on-mask animation`
- 分支：claude/cool-pascal-mil5i8。
- 实际构建副本：`C:\Users\kkk\sts2_tools\p5_build\repo_095_runtime_20261103`，沿用用户授权。
- 游戏进程检查：开始执行及安装前均无输出。
- fetch、checkout、merge --ff-only 均成功；使用 --no-stat 避免读取无关预览内容。
- 两个版本字段均为 0.9.10。
- 补齐的 5 个 mod 文件全部通过 Git 对象哈希校验；未下载预览图。

## 编译结果

在实际副本 p5mod 目录执行：

```powershell
& 'C:\Users\kkk\sts2_tools\dotnet\dotnet.exe' build -c Release
```

Build succeeded，退出码 0；1 个 warning、0 个 error。未修改代码处理 warning。

DLL 已确认存在：`p5mod\bin\Release\net9.0\P5Joker.dll`，大小 611,328 字节。

## PCK 与备份

- 临时目录：`C:\Users\kkk\sts2_tools\tmp\p5_stage_0.9.10`，确认不存在后新建，复制全部资源（含隐藏内容）。
- 工具：`C:\Users\kkk\sts2_tools\gdre\gdre_tools.exe`
- 参数：--headless、--pck-version=3、--pck-engine-version=4.5.1。
- 打包退出码：0。
- PCK：`C:\Users\kkk\sts2_tools\tmp\P5Joker_0.9.10.pck`
- 大小：56,813,924 字节，大于 5 MB。
- 原安装完整备份：`C:\Users\kkk\sts2_tools\backup\P5Joker_20261103_213734\`

## 安装文件

三个文件均成功覆盖复制至：

`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker\`

全部文件列表（America/Chicago，UTC-06:00）：

| 文件名 | 大小（字节） | 修改时间 |
| --- | ---: | --- |
| P5Joker.dll | 611328 | 2026-11-03 21:36:58 -06:00 |
| P5Joker.json | 542 | 2026-11-03 21:36:38 -06:00 |
| P5Joker.pck | 56813924 | 2026-11-03 21:37:25 -06:00 |

## 执行说明

更新、编译、打包、备份和安装成功，没有命令报错。未修改代码或特效、未生图、未安装工具、未删除文件、未修改全局 git config、系统环境变量或 PATH，未进入禁止的旧仓库，未读取密码、密钥或禁止文件。命令分别执行。游戏目录仅复制指定三个文件。未使用 Computer Use，未启动游戏，未结束进程，未做实机验收，由用户自行验收。

