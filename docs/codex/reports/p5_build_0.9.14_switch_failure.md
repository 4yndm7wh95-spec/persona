P5Joker 0.9.14 构建安装报告

结果：切换分支失败，按用户要求停止构建。

仓库：C:\Users\kkk\sts2_tools\p5_build\repo
目标分支：claude/epic-archimedes-m9an69
当前 HEAD：2a5896e4ddb8910cf1f52b6b8eeb1a76aac092c5
已 fetch 的目标提交：15189445f233c4c913b93d0c98e6799637092f87

执行记录：
1. 按指定 refspec fetch 成功，创建 origin/claude/epic-archimedes-m9an69。
2. 创建备份目录 C:\Users\kkk\sts2_tools\backup\untracked_20261108_013113\。
3. 使用 Move-Item、不带 -Force，将 docs\codex\reports\p5_build_0.8.0.md 移入该备份目录。
4. 执行指定 switch -c --track 命令失败，退出码 1。

错误原文：
fatal: cannot set up tracking information; starting point 'origin/claude/epic-archimedes-m9an69' is not a branch

只读诊断：
remote.origin.fetch 仅配置了旧分支
+refs/heads/claude/p5r-ren-slay-the-spire-mod-6tk3f3:refs/remotes/origin/claude/p5r-ren-slay-the-spire-mod-6tk3f3
目标远端引用存在且指向 15189445。switch 失败后 HEAD 仍在旧分支，但工作区和暂存区出现大量切换产生的差异。本次未尝试恢复、继续切换或修改跟踪配置。
art\p5\fx\gen_codex\ink\ 未移动。

未执行构建文档、编译、打包和安装。未启动游戏、未使用 Computer Use、未生图。
报告通过独立索引在已 fetch 目标提交上创建仅含报告的提交，并推送到 codex/p5-build-0.9.14-failure 分支，避免提交当前暂存区差异。