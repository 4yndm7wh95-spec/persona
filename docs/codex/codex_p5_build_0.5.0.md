你在用户的 Windows 电脑上工作。任务：把 P5Joker mod 0.5.0 编译、打包、装进游戏。不改任何代码、不生图、不启动游戏、不用 Computer Use。

【0. 绝对不要做】
- 不修改 / 删除仓库里任何已有文件（只允许按第 5 步新建报告文件）。
- 不 rebase / reset / amend / stash；不 --force；不改全局 git config。
- 不读取或输出 C:\Users\kkk\.p3record\obs.json，不读取或输出任何密码 / 密钥。
- 游戏目录只允许改 <游戏目录>\mods\P5Joker\ 这一个文件夹（按第 4 步）；别的 mod、游戏原文件一律不动。
- 不启动游戏，不做实机验收（用户自己验收）。
- 遇到下面没写到的情况、任何一步报错：停下来，把情况和完整报错写进报告，推送报告，结束。不要自己改代码修。

【1. 找仓库、拉最新】
1. 本机 git 仓库（之前成功推送过的那个）：remote 指向 github.com/4yndm7wh95-spec/persona，仓库里有 p5mod\P5Joker.json。记作 <repo>。
2. 所有 git 命令都带 -c safe.directory=<repo>；fetch / push 带 -c http.version=HTTP/1.1，失败按 5/10/20/40 秒重试。
3. 分支：claude/p5r-ren-slay-the-spire-mod-6tk3f3
   git fetch origin claude/p5r-ren-slay-the-spire-mod-6tk3f3
   git checkout claude/p5r-ren-slay-the-spire-mod-6tk3f3（本地没有就 git checkout -b claude/p5r-ren-slay-the-spire-mod-6tk3f3 origin/claude/p5r-ren-slay-the-spire-mod-6tk3f3）
   git merge --ff-only origin/claude/p5r-ren-slay-the-spire-mod-6tk3f3
   ff-only 失败 → 停下来报告。
4. 确认 p5mod\P5Joker.json 里 "version" 是 "0.5.0"，p5mod\src\ModEntry.cs 里 Version = "0.5.0"。不是就停下来报告（不要自己改）。

【2. 编译 DLL】
在 <repo>\p5mod 下运行：
   dotnet build -c Release
（游戏目录不是 C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2 的话，加 -p:GameDir="<游戏目录>"）
成功标准：输出 Build succeeded，0 个 error；产物 <repo>\p5mod\bin\Release\net9.0\P5Joker.dll 存在。
有 error：把完整 error 行贴进报告，停。warning 不用管，把条数写进报告。

【3. 打包 PCK】
1. 把 <repo>\p5mod\pck_stage 整个文件夹复制到一个临时目录，例如 C:\Users\kkk\sts2_tools\tmp\p5_stage_0.5.0（已存在就先删掉这个临时目录再复制；只删这个临时目录）。
2. 用之前打包 P3Makoto 用的 gdre_tools.exe（在 C:\Users\kkk\sts2_tools 下找）运行：
   <gdre_tools.exe> --headless --pck-create=C:\Users\kkk\sts2_tools\tmp\p5_stage_0.5.0 --output=C:\Users\kkk\sts2_tools\tmp\P5Joker.pck --pck-version=3 --pck-engine-version=4.5.1
3. 成功标准：P5Joker.pck 存在且大于 5 MB。

【4. 安装】
1. 如果 <游戏目录>\mods\P5Joker\ 已存在：先把整个文件夹复制到 C:\Users\kkk\sts2_tools\backup\P5Joker_<当前日期时间>\ 备份。
2. 把下面 3 个文件复制进 <游戏目录>\mods\P5Joker\（覆盖同名文件；文件夹里别的文件不要删）：
   - <repo>\p5mod\P5Joker.json
   - <repo>\p5mod\bin\Release\net9.0\P5Joker.dll
   - C:\Users\kkk\sts2_tools\tmp\P5Joker.pck
3. 列出 mods\P5Joker\ 里每个文件的文件名、大小、修改时间。

【5. 报告并推送】
新建 <repo>\docs\codex\reports\p5_build_0.5.0.md（中文）：
- git log -1 --oneline 的输出
- 编译结果（成功 / 失败、warning 条数）
- PCK 大小
- 备份位置（没有旧版本就写「无」）
- 第 4 步第 3 条的文件列表
只 git add 这个报告文件，提交信息：「Codex: P5Joker 0.5.0 编译安装报告」，然后 git push -u origin claude/p5r-ren-slay-the-spire-mod-6tk3f3；
被拒就先 fetch，再 git merge --no-edit origin/claude/p5r-ren-slay-the-spire-mod-6tk3f3，然后重新推；merge 有冲突就停下来报告。
最后在对话里回复：提交号 + 报告全文。
