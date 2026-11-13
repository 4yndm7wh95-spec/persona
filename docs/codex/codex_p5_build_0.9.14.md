你在用户的 Windows 电脑上工作。任务：把 P5Joker mod 0.9.14 编译、打包、装进游戏。不改任何代码、不生图、不启动游戏、不用 Computer Use。

【0. 绝对不要做】
- 不修改仓库里任何已有文件（只允许按第 5 步新建报告文件）。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量 / PATH；不安装任何东西。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 每条命令单独执行；不要用 Remove-Item -Recurse / -Force，不删除任何目录或文件。
- 不进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 游戏目录只允许往 <游戏目录>\mods\P5Joker\ 里复制文件；别的 mod、游戏原文件一律不动。
- 不启动游戏，不关闭 / 结束任何进程，不做实机验收（用户自己验收）。
- 遇到下面没写到的情况、任何一步报错：停下来，把情况和完整报错写进报告，推送报告，结束。不要自己改代码修。

路径：
- 仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo
- dotnet = C:\Users\kkk\sts2_tools\dotnet\dotnet.exe
- gdre = C:\Users\kkk\sts2_tools\gdre\gdre_tools.exe
- <游戏目录> = C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2

【1. 先确认游戏没在运行】
运行 Get-Process | Where-Object { $_.ProcessName -like '*Slay the Spire 2*' }
有输出 → 停下来报告「游戏正在运行」，不要关它。

【2. 拉最新（这台仓库副本没有这个分支，第一次要新建本地分支）】
在 <repo> 里依次单独运行：
   git -c safe.directory=* -c http.version=HTTP/1.1 fetch origin claude/epic-archimedes-m9an69:refs/remotes/origin/claude/epic-archimedes-m9an69
   （带完整的 :refs/remotes/... 写法，否则只写进 FETCH_HEAD，下一步会报 invalid reference）
未跟踪文件 docs\codex\reports\p5_build_0.8.0.md 在新分支里已经被提交过，切分支会因为它停住：
   新建目录 C:\Users\kkk\sts2_tools\backup\untracked_<当前日期时间>\，用 Move-Item（不带 -Force）把这个文件移进去。这是唯一允许移动的文件；
   art\p5\fx\gen_codex\ink\ 不在新分支里，原样留着不动。
然后：
   git -c safe.directory=* switch -c claude/epic-archimedes-m9an69 --track origin/claude/epic-archimedes-m9an69
   （如果提示本地分支已存在，改为依次运行 git -c safe.directory=* switch claude/epic-archimedes-m9an69 和
    git -c safe.directory=* merge --ff-only origin/claude/epic-archimedes-m9an69）
   git -c safe.directory=* log -1 --oneline   → 应该是 8a3ea299 或更新的提交
switch 还是失败、ff-only 失败 → 停下来报告完整报错。fetch 超时按 5/10/20/40 秒重试，4 次都失败就停下来报告。
确认 p5mod\P5Joker.json 里 "version" 是 "0.9.14"，p5mod\src\ModEntry.cs 里 Version = "0.9.14"。不是就停下来报告。

【3. 编译 DLL】
在 <repo>\p5mod 下运行：
   C:\Users\kkk\sts2_tools\dotnet\dotnet.exe build -c Release
成功标准：Build succeeded，0 个 error；<repo>\p5mod\bin\Release\net9.0\P5Joker.dll 存在。
有 error：把完整 error 行贴进报告，停。warning 只写条数。

【4. 打包 PCK 并安装】
1. 新建临时目录 C:\Users\kkk\sts2_tools\tmp\p5_stage_0.9.14（已存在 → 停下来报告，不要删它），
   把 <repo>\p5mod\pck_stage 里的全部内容复制进去（Copy-Item 带 -Recurse 复制是允许的，只是不能用它删除）。
2. 运行：
   C:\Users\kkk\sts2_tools\gdre\gdre_tools.exe --headless --pck-create=C:\Users\kkk\sts2_tools\tmp\p5_stage_0.9.14 --output=C:\Users\kkk\sts2_tools\tmp\P5Joker_0.9.14.pck --pck-version=3 --pck-engine-version=4.5.1
   成功标准：P5Joker_0.9.14.pck 存在且大于 5 MB。
3. 如果 <游戏目录>\mods\P5Joker\ 已存在：先把它整个复制到 C:\Users\kkk\sts2_tools\backup\P5Joker_<当前日期时间>\ 备份。
4. 把下面 3 个文件复制进 <游戏目录>\mods\P5Joker\（覆盖同名文件；文件夹里别的文件不要删）：
   - <repo>\p5mod\P5Joker.json
   - <repo>\p5mod\bin\Release\net9.0\P5Joker.dll
   - C:\Users\kkk\sts2_tools\tmp\P5Joker_0.9.14.pck → 复制后的文件名必须是 P5Joker.pck
5. 列出 mods\P5Joker\ 里每个文件的文件名、大小、修改时间。

【5. 报告并推送】
新建 <repo>\docs\codex\reports\p5_build_0.9.14.md（中文）：git log -1 --oneline 的输出、编译结果（warning 条数）、PCK 大小、备份位置、第 4 步第 5 条的文件列表。
依次单独运行：
   git -c safe.directory=* add docs/codex/reports/p5_build_0.9.14.md
   git -c safe.directory=* -c user.name=Codex -c user.email=codex@local commit -m "Codex: P5Joker 0.9.14 编译安装报告"
   git -c safe.directory=* -c http.version=HTTP/1.1 push origin claude/epic-archimedes-m9an69
推送被拒：先运行 git -c safe.directory=* -c http.version=HTTP/1.1 pull --no-rebase --no-edit origin claude/epic-archimedes-m9an69，再推；有冲突就停下来报告。
最后在对话里回复：提交号 + 报告全文。
