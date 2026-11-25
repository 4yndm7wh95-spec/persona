# P5Joker 0.9.47 编译安装报告

日期：2026-11-15（America/Chicago，UTC-06:00）。

结果：编译、PCK 打包、备份及安装完成。未修改源码，未启动游戏，未做实机验收。

## 源码与工作树

- 目标分支：`claude/confident-hypatia-nropgi`。
- 已先在指定仓库 `C:\Users\kkk\sts2_tools\p5_build\repo` fetch 完整远端分支，再读取任务单。
- 原仓库切换失败后，用户明确要求「不用停下」「你继续 自己解决」。因此改用已占用该分支的现有工作树 `C:\Users\kkk\sts2_tools\p5_build\repo_0920`，确认受跟踪文件干净后执行快进更新。原仓库已有暂存内容未改动、未提交。
- 构建前 `git log -1 --oneline`：

```text
6442e1fc Buff batch 1: Anticipate read-slashes as blade polygons; preview
```

- 本地 `p5mod\P5Joker.json` 的 `version` 与 `p5mod\src\ModEntry.cs` 的 `Version` 均为 `0.9.47`。
- 为执行第 4.5 步，将该工作树的稀疏检出范围增加 `research/libs`；未更改源码或全局 Git 配置。

## 编译与 PCK

- 在实际工作树的 `p5mod` 下使用指定 `C:\Users\kkk\sts2_tools\dotnet\dotnet.exe build -c Release`。
- 结果：`Build succeeded`；4 个 warning，0 个 error。
- DLL：`C:\Users\kkk\sts2_tools\p5_build\repo_0920\p5mod\bin\Release\net9.0\P5Joker.dll`，1,436,160 字节。
- 新建临时目录 `C:\Users\kkk\sts2_tools\tmp\p5_stage_0.9.47`，复制 `p5mod\pck_stage` 全部内容。
- 使用任务单指定的 GDRE 参数打包：PCK version 3，engine version 4.5.1；退出码 0。
- PCK：`C:\Users\kkk\sts2_tools\tmp\P5Joker_0.9.47.pck`，**67,864,132 字节**，大于 5 MB。

## 备份与安装

操作前及覆盖安装前均检查游戏进程，均无输出。未关闭或结束任何进程。

旧 `mods\P5Joker` 整个目录已复制备份到：
`C:\Users\kkk\sts2_tools\backup\P5Joker_20261115_074803`。

安装目录：`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\mods\P5Joker`。

只覆盖规定的三个文件，目录内未删除文件。安装后完整文件列表如下，时间为 UTC-06:00：

| 文件名 | 大小（字节） | 修改时间 |
| --- | ---: | --- |
| P5Joker.dll | 1,436,160 | 2026-11-15 07:46:20 -06:00 |
| P5Joker.json | 682 | 2026-11-15 07:42:33 -06:00 |
| P5Joker.pck | 67,864,132 | 2026-11-15 07:47:17 -06:00 |

三个安装文件与对应构建/打包源文件的 SHA256 分别一致：

```text
P5Joker.dll  58A239B5A855E32C1FA1F2791F2CB52C3ABE2020F9B2FE7342B3A6819968D516
P5Joker.json 85F20029EC138E41D5C772A01EAB3203C754BA7BC7E8FF05ED4E2512738D39AC
P5Joker.pck  F16601C54D9BC6FBC8587FB0304F76618463DB3F5058839FF790FFFFB59A4588
```

## 游戏库同步及实际游戏版本

严格使用任务单指定的桌面游戏目录作为来源。三个文件均存在，已复制覆盖实际工作树的 `research\libs\`：

| 文件 | 复制后大小（字节） |
| --- | ---: |
| sts2.dll | 9,757,184 |
| 0Harmony.dll | 2,328,064 |
| GodotSharp.dll | 5,613,568 |

复制后 Git 检查确认三者与仓库原有内容一致，因此本次没有 DLL 内容差异。

**实际版本差异：**`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\release_info.json` 的 `version` 是 **`v0.111.0`**。任务单第 4.5 步希望让云端检查对着 0.113，但所指定路径实际是 0.111.0。本次编译及库同步均来自该指定路径，不能视为 0.113 编译或兼容验收。提交标题据实注明 0.111，避免误导后续云端开发。

## 遇到的报错及处理

首次按任务单创建本地分支失败，任务单允许使用已有分支：

```text
fatal: a branch named 'claude/confident-hypatia-nropgi' already exists
```

随后在原仓库切换已有分支失败：

```text
fatal: 'claude/confident-hypatia-nropgi' is already used by worktree at 'C:/Users/kkk/sts2_tools/p5_build/repo_0920'
```

已先停止后续构建并告知用户；用户随后明确要求继续自行解决。于是使用上述现有工作树，没有强制切换、删除、移动或重建工作树。

`merge --ff-only` 已输出 `Fast-forward` 并更新工作树，随后生成差异统计所需的历史对象补取出现网络错误，命令退出码 1：

```text
error: RPC failed; curl 56 Recv failure: Connection was reset
error: 6444 bytes of body are still expected
fetch-pack: unexpected disconnect while reading sideband packet
fatal: early EOF
fatal: index-pack failed
fatal: could not fetch f12eabf1f0dbe1cf9baa46240ed190c1e27f175e from promisor remote
```

按用户继续要求检查：`git log -1` 已是 `6442e1fc`，受跟踪工作树干净，两处版本均为 0.9.47，随后实际编译成功。未修改代码来绕过错误。后续库目录稀疏检出、打包、复制均成功。

## 提交范围与验收边界

按任务单分别暂存报告及三份指定库文件；库文件没有内容差异，实际提交应只新增本报告。未提交编译产物、PCK、其他工作区暂存内容或源码修改。报告推送至同一目标分支。

游戏内功能、华丽收场性能及画面由用户自行验收；本次未启动游戏、未使用 Computer Use。
