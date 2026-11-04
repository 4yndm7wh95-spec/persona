# P5Joker 0.9.7 编译安装报告

- 源提交（git log -1 --oneline）：`13ef7d7b Drop custom Snakebite bite (keep vanilla fangs); poison hits use curse sounds instead of the punch`
- 分支：claude/cool-pascal-mil5i8。
- 实际仓库：`C:\Users\kkk\sts2_tools\p5_build\repo_095_runtime_20261103`，沿用用户授权的最小副本。
- 仅检出 p5mod 和报告目录；未下载预览图。
- 游戏进程检查：开始执行及安装前均无输出。
- 两个版本字段均为 0.9.7，更新后工作区干净。

## 更新情况

浅克隆第一次显式更新远端引用返回 non-fast-forward；随后普通 fetch 成功，补齐必要提交记录后，工作区快进至 13ef7d7b。

最后缺少的 p5mod/src/Vfx/Warmup.cs 从 GitHub 官方原始地址取回，Git 对象哈希校验为 7ac55a75455e05094b11fd9ac36df3445e767fb6，与远端一致。

快进合并已更新 HEAD 和工作区，但打印全仓库变更统计时试图读取未下载的预览对象，被禁止自动下载选项拦截，命令退出码为 1。检查确认 HEAD 为 13ef7d7b、工作区干净、版本正确后，按用户“继续”的授权完成构建。未下载该预览图，未修改代码。

相关输出：

```text
From https://github.com/4yndm7wh95-spec/persona
 ! [rejected] claude/cool-pascal-mil5i8 -> origin/claude/cool-pascal-mil5i8  (non-fast-forward)

warning: lazy fetching disabled; some objects may not be available
fatal: could not fetch 765ec02d051f2f0f5b09d3b90630182c260e9035 from promisor remote
Updating 0617d30f..13ef7d7b
Fast-forward
```

## 编译结果

在实际仓库 p5mod 目录执行：

```powershell
& 'C:\Users\kkk\sts2_tools\dotnet\dotnet.exe' build -c Release
```

成功，退出码 0；0 个 warning、0 个 error。DLL 存在，大小 610,304 字节。

```text
Determining projects to restore...
  All projects are up-to-date for restore.
  P5Joker -> C:\Users\kkk\sts2_tools\p5_build\repo_095_runtime_20261103\p5mod\bin\Release\net9.0\P5Joker.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:05.79
```

## PCK 与备份

- stage：`C:\Users\kkk\sts2_tools\tmp\p5_stage_0.9.7`，确认不存在后新建，复制全部资源（包含隐藏内容）。
- 工具：`C:\Users\kkk\sts2_tools\gdre\gdre_tools.exe`
- 参数：--headless、--pck-version=3、--pck-engine-version=4.5.1；打包退出码 0。
- PCK：`C:\Users\kkk\sts2_tools\tmp\P5Joker_0.9.7.pck`
- 大小：56,813,924 字节，大于 5 MB。
- 原安装完整备份：`C:\Users\kkk\sts2_tools\backup\P5Joker_20261103_104649\`

## 安装文件

三个文件均已成功覆盖复制至：

`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker\`

全部文件列表（America/Chicago，UTC-06:00）：

| 文件名 | 大小（字节） | 修改时间 |
| --- | ---: | --- |
| P5Joker.dll | 610304 | 2026-11-03 10:46:01 -06:00 |
| P5Joker.json | 541 | 2026-11-03 10:45:23 -06:00 |
| P5Joker.pck | 56813924 | 2026-11-03 10:46:34 -06:00 |

## 执行说明

编译、打包、备份和安装成功。未修改代码或特效、未生图、未安装工具、未删除文件、未修改全局 git config、系统环境变量或 PATH，未进入禁止的旧仓库，未读取密码、密钥或禁止文件。命令分别执行。游戏目录仅复制指定三个文件。未使用 Computer Use，未启动游戏，未结束进程，未做实机验收，由用户自行验收。

