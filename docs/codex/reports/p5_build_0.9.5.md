# P5Joker 0.9.5 编译安装报告

- 源提交（git log -1 --oneline）：`90e5697c P5Joker 0.9.5: Outbreak crush hand, Arsène clench + blue flame; build prompt`
- 分支：claude/cool-pascal-mil5i8。
- 实际构建目录：`C:\Users\kkk\sts2_tools\p5_build\repo_095_runtime_20261103`。
- 用户授权自行解决更新问题，并明确不下载无关预览图；最终副本仅检出 p5mod 和 docs/codex/reports。
- 两个版本字段均为 0.9.5；检出后工作区干净。
- 游戏进程检查：开始执行及安装前均无输出。
- 最终结果：编译、打包、备份和安装成功。

## 更新及网络问题处理

原工作目录 ff-only 失败，先提交了停止报告 27878c46。用户随后授权解决并继续。合并取回对象时网络多次重置或超时；用户明确要求跳过预览图后，改用最小构建副本，不再检出或下载预览目录。

新副本固定至远端提交 90e5697c0aa1ba60a941203259c1d3bcc4fcb610，复用本机 Git 对象，只补齐缺失的 26 个唯一 mod 对象。通过 GitHub 官方原始文件/API 通道取回，并逐个验证 Git 哈希完全一致后入库。最后一份纹理使用 HTTP Range 补齐，完整大小 5,435,456 字节，Git 哈希为 2fe8c558fbd708833f211d68b718803c211566bc，与远端一致。没有修改代码或资源内容，没有删除旧目录。

期间主要错误输出：

```text
hint: Diverging branches can't be fast-forwarded, you need to either:
hint:
hint:     git merge --no-ff
hint:
hint: or:
hint:
hint:     git rebase
hint:
hint: Disable this message with "git config set advice.diverging false"
fatal: Not possible to fast-forward, aborting.
```

Git 下载出现下列错误（不同尝试的剩余字节数不同）：

```text
error: RPC failed; curl 56 Recv failure: Connection was reset
fetch-pack: unexpected disconnect while reading sideband packet
fatal: early EOF
fatal: index-pack failed
fatal: could not fetch 92ef605b3ff0ef810f34ed020f66c5017bc2b3e2 from promisor remote

error: RPC failed; curl 28 Operation too slow. Less than 1 bytes/sec transferred the last 40 seconds
fetch-pack: unexpected disconnect while reading sideband packet
fatal: early EOF
fatal: index-pack failed
```

另有克隆自动检出根目录无关文件失败、部分原始文件连接被远端关闭，以及读取仍被下载进程占用的文件失败；均未修改源代码或结束进程。最终最小副本检出和构建成功，全部必要对象已通过哈希验证。

## 编译结果

在实际构建目录的 p5mod 下运行：

```powershell
& 'C:\Users\kkk\sts2_tools\dotnet\dotnet.exe' build -c Release
```

成功，退出码 0；0 个 warning、0 个 error。DLL 存在，大小 599,040 字节。

```text
Determining projects to restore...
  Restored C:\Users\kkk\sts2_tools\p5_build\repo_095_runtime_20261103\p5mod\P5Joker.csproj (in 4.77 sec).
  P5Joker -> C:\Users\kkk\sts2_tools\p5_build\repo_095_runtime_20261103\p5mod\bin\Release\net9.0\P5Joker.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:10.07
```

## PCK 与备份

- stage：`C:\Users\kkk\sts2_tools\tmp\p5_stage_0.9.5`，确认不存在后新建，复制全部资源（包含隐藏内容）。
- 工具：`C:\Users\kkk\sts2_tools\gdre\gdre_tools.exe`
- 参数：--headless、--pck-version=3、--pck-engine-version=4.5.1；打包退出码 0。
- PCK：`C:\Users\kkk\sts2_tools\tmp\P5Joker_0.9.5.pck`
- 大小：56,804,740 字节，大于 5 MB。
- 原安装完整备份：`C:\Users\kkk\sts2_tools\backup\P5Joker_20261103_090814\`

## 安装文件

三个文件均已成功覆盖复制至：

`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker\`

全部文件列表（America/Chicago，UTC-06:00）：

| 文件名 | 大小（字节） | 修改时间 |
| --- | ---: | --- |
| P5Joker.dll | 599040 | 2026-11-03 09:06:06 -06:00 |
| P5Joker.json | 541 | 2026-11-03 09:05:05 -06:00 |
| P5Joker.pck | 56804740 | 2026-11-03 09:07:29 -06:00 |

## 执行说明

未修改代码或特效、未生图、未安装工具、未删除文件、未改全局 git config、系统环境变量或 PATH，未进入禁止的旧仓库，未读取密码、密钥或禁止文件。命令分别执行。游戏目录仅复制指定三个文件。未使用 Computer Use，未启动游戏，未结束进程，未做实机验收，由用户自行验收。

