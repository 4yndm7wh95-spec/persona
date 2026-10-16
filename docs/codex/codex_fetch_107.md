你在用户的 Windows 电脑上工作。任务：把本机杀戮尖塔 2 **0.107.1 版**（Steam 正式版）的游戏程序集和储君相关场景文件复制进仓库并推送，供 Claude 对照 0.111.0 做版本兼容。只复制、提交这些资料，不改任何代码，不改游戏文件。

【1. 找到 0.107.1 的游戏目录】
1. 候选位置：
   - C:\Users\kkk\Downloads\Slay the Spire 2\；
   - Steam 库里的 Slay the Spire 2。
2. 读每个候选目录里的 release_info.json（只读），找出 version 是 v0.107.1 的那个，记作 <G107>。
3. 找不到 0.107.1 就停下来报告。

【2. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功编译的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. 执行 git -c safe.directory=<repo> status -sb。有进行中的合并或未提交改动就停下来报告。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并：先执行
   git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn
   不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   有冲突就停下来报告。

【3. 复制程序集】
1. 新建 <repo>\research\libs_0.107.1\。
2. 从 <G107>\data_sts2_windows_x86_64\ 复制下面三个文件进去：
   - sts2.dll
   - GodotSharp.dll
   - 0Harmony.dll
3. 再把 <G107>\release_info.json 复制进去。
4. 如果 data_sts2_windows_x86_64 里有同名的 .xml 文档文件（sts2.xml），也一起复制；没有就算了。

【4. 导出储君相关场景（尽力而为，做不到就跳过并在报告里说明）】
1. 用以前导出 research\regent_assets 时用过的 GDRE Tools（在 C:\Users\kkk\sts2_tools 下用 Get-ChildItem -Recurse -Filter "gdre_tools*.exe" 找）。
2. 从 <G107> 的游戏 pck 里，只导出路径里含 regent 或 char_select 的 .tscn 和 .tres 文本文件（不要图片、不要音频）。
3. 导出到 <repo>\research\regent_scenes_0.107.1\，保持原来的 res:// 目录结构。
4. 如果 GDRE 只能整包导出：先导出到 C:\Users\kkk\sts2_tools\pck107_full\（仓库外），再把符合条件的文件复制进仓库。
5. 导出的文件总大小超过 20 MB 就只保留 .tscn，并在报告里说明。

【5. 提交并推送】
1. 只 add 这两个目录：
   git -c safe.directory=<repo> add research/libs_0.107.1 research/regent_scenes_0.107.1
2. 提交：
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "research: StS2 v0.107.1 assemblies and regent scenes for compat"
3. push：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
   - 失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
   - 被拒绝（远端有新提交）时先 fetch，再 merge --no-edit（同上面的写法）后重新 push。不要 rebase，也不要 --force。

【报告】
用中文报告：
- <G107> 的路径和 release_info.json 里的版本号；
- 复制了哪些文件、各自大小；
- 场景导出了多少个文件、总大小（或没导出的原因）；
- 推送后的提交号。

【绝对不要做】
- 不要修改或删除代码；不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只读、只复制）。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 不要 sparse-checkout disable / set（sparse-checkout add 允许；路径过长时这一条命令可以加 -c core.longpaths=true）。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
