# 杀戮尖塔 2 原版转场资源

## 提取结果（2026-11-17）

已从本机原版 `SlayTheSpire2.pck`（1,990,705,700 字节）只读提取。包内路径保持为 `res://` 下的相对路径。原始资源 64 个（18,861,062 字节），恢复资源 42 个（816,314 字节），共 106 个资源文件（19,677,376 字节）。另有 5 张阈值联系表（83,783 字节）。没有超过 20 MB 的原始文件。`LIST.csv` 以字节计量，另记录 25 条脚本路径，未提取脚本文件。

`scenes/game.tscn` 的 `GameTransitionRect` 节点引用 `NTransition.cs`，遮罩子节点引用战斗转场材质。包内所有路径含 `transition` 的资源与相关 `.godot/imported` 数据均在提取范围内。场景、材质和着色器的外部引用已递归补齐（脚本除外）。递归依赖包含两份字体，随原始导入数据和恢复文件一起保存。

## 材质与贴图

贴图路径均相对于 `res://images/ui/transitions/`。以下五张独立 PNG 都是 **2560×1200，RGBA**，保留 GDRE 输出的像素、原色模式和 alpha。原贴图未缩放或重着色。

| 材质 | 贴图 |
| --- | --- |
| `defect_transition_mat.tres` | `ironclad_transition.png` |
| `fade_transition_mat.tres` | 无贴图，使用 `fade_transition.gdshader` |
| `fight_transition_mat.tres` | `fight_transition.png` |
| `game_over_transition_mat.tres` | `game_over_transition.png` |
| `ironclad_transition_mat.tres` | `ironclad_transition.png` |
| `necrobinder_transition_mat.tres` | `necrobinder_transition.png` |
| `regent_transition_mat.tres` | `ironclad_transition.png` |
| `silent_transition_mat.tres` | `silent_transition.png` |

每张 `steps_*.png` 从左到右显示阈值 0.1 至 1.0。按源着色器先算 `falloff = 1 - R`，再算 `remap = mix(-0.1, 1.1, threshold)`，最后用 `step(falloff, remap)` 判断是否盖住。黑色表示盖住，浅灰表示未盖住。仅联系表缩为每格宽 384 像素。Defect、Regent 对照 `steps_ironclad.png`。

## 工具命令与日志

使用已有的 GDRE 2.7.3。在 Windows 上通过本机 Python 的 `subprocess.run(..., creationflags=subprocess.CREATE_NO_WINDOW)` 启动并等待 GDRE，用独立文件捕获标准输出、标准错误。此方式解决了首次执行的空清单，得到 997,852 字节清单。

1. `gdre_tools.exe --headless --list-files=<PCK>`
2. `gdre_tools.exe --headless --extract=<PCK> --output=<临时目录>/raw --include=res://<精确路径> ...`。首批选中转场路径和 `scenes/game.tscn`，之后按引用逐轮补解。
3. `gdre_tools.exe --headless --recover=<临时目录>/raw --output=<临时目录>/recovered --skip-loading-resource-strings`。
4. 用本机 Python 与 Pillow 生成联系表。GDRE 产生的日志、编辑器缓存不算作原包资源。

完整命令见本机 `C:/Users/kkk/sts2_tools/tmp/sts2_transitions_20261117_031258/commands.txt`，清单和输出日志位于同目录。GDRE 恢复日志记录 `project config not loaded!`，因为临时目录只含资源子集。工具退出码为 0，转换统计为 22 项成功、0 项失败、0 项未转换，其中 5 项的源导入数据使用有损压缩。交付前确认所有预期恢复文件存在、PNG 可读、文本资源与原始提取文件逐字节一致。

## 首次运行记录

## 杀戮尖塔 2 原版转场资源提取报告

2026-11-17。任务在第 2.1 步「列出 PCK 文件」停止。已确认本地分支为 `claude/confident-hypatia-nropgi`，并快进到远端提交 `fd2a0ea4`。快进后，`git status --short --untracked-files=no` 无输出。

### 执行的列目录命令

在 `C:\Users\kkk\sts2_tools\tmp\sts2_transitions_20261117_031258` 下，以 PowerShell 执行：

```powershell
& 'C:/Users/kkk/sts2_tools/gdre/gdre_tools.exe' --headless '--list-files=C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2/SlayTheSpire2.pck' *> 'C:/Users/kkk/sts2_tools/tmp/sts2_transitions_20261117_031258/pck-list.txt'
```

### 失败情况与完整输出

- GDRE 退出码为 `0`，未输出标准输出或标准错误。`pck-list.txt` 大小为 **0 字节**。
- 随后执行 `rg -n -i 'transition|^error|^warning' pck-list.txt`，退出码为 `1`，标准输出与标准错误均为空。`Get-Content` 复核没有内容。空日志不是一份可用的资源清单，无法确定提取范围，也无法从现有输出确定 GDRE 未写出目录的原因。
- 第 1 步 Git 快进成功，退出码为 `0`。该步骤有以下稀疏检出警告，快进后未改动稀疏检出设置：

```text
warning: The following paths were already present and thus not updated despite sparse patterns:
	docs/codex/reports/p5_build_0.9.48.md

After fixing the above paths, you may want to run `git sparse-checkout reapply`.
```

### 产出

提取与还原均未开始。原始资源、还原资源和阈值联系表均为 0 个、0 字节，角色转场贴图尺寸与材质引用均无法读取。游戏包仅传给列目录命令，未向游戏目录写入文件。本次提交只新增本报告。
### 提交报告时的稀疏检出限制

任务单里的 `git -c safe.directory=* add research/sts2_transitions` 退出码为 `1`，完整输出如下：

```text
The following paths and/or pathspecs matched paths that exist
outside of your sparse-checkout definition, so will not be
updated in the index:
research/sts2_transitions/README.md
hint: If you intend to update such entries, try one of the following:
hint: * Use the --sparse option.
hint: * Disable or modify the sparsity rules.
hint: Disable this message with "git config set advice.updateSparsePath false"
```

按 Git 提示使用 `git -c safe.directory=* add --sparse -- research/sts2_transitions/README.md` 提交本报告。
