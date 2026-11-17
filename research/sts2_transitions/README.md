# 杀戮尖塔 2 原版转场资源提取报告

2026-11-17。任务在第 2.1 步「列出 PCK 文件」停止。已确认本地分支为 `claude/confident-hypatia-nropgi`，并快进到远端提交 `fd2a0ea4`。快进后，`git status --short --untracked-files=no` 无输出。

## 执行的列目录命令

在 `C:\Users\kkk\sts2_tools\tmp\sts2_transitions_20261117_031258` 下，以 PowerShell 执行：

```powershell
& 'C:/Users/kkk/sts2_tools/gdre/gdre_tools.exe' --headless '--list-files=C:/Users/kkk/Desktop/slaythespire/Slay the Spire 2/SlayTheSpire2.pck' *> 'C:/Users/kkk/sts2_tools/tmp/sts2_transitions_20261117_031258/pck-list.txt'
```

## 失败情况与完整输出

- GDRE 退出码为 `0`，未输出标准输出或标准错误。`pck-list.txt` 大小为 **0 字节**。
- 随后执行 `rg -n -i 'transition|^error|^warning' pck-list.txt`，退出码为 `1`，标准输出与标准错误均为空。`Get-Content` 复核没有内容。空日志不是一份可用的资源清单，无法确定提取范围，也无法从现有输出确定 GDRE 未写出目录的原因。
- 第 1 步 Git 快进成功，退出码为 `0`。该步骤有以下稀疏检出警告，快进后未改动稀疏检出设置：

```text
warning: The following paths were already present and thus not updated despite sparse patterns:
	docs/codex/reports/p5_build_0.9.48.md

After fixing the above paths, you may want to run `git sparse-checkout reapply`.
```

## 产出

提取与还原均未开始。原始资源、还原资源和阈值联系表均为 0 个、0 字节，角色转场贴图尺寸与材质引用均无法读取。游戏包仅传给列目录命令，未向游戏目录写入文件。本次提交只新增本报告。
## 提交报告时的稀疏检出限制

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
