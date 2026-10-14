你在用户的 Windows 电脑上工作。任务：用 imagegen 做两张结城理面部特写的「闭眼 / 半睁」版本，和现有原版立绘逐像素对齐（只做美术，不改代码）。

【背景】
P3Makoto 是杀戮尖塔 2 的 mod。结局「伟大封印」里有一个面部特写：原版 P3R 是结城理闭着眼、然后睁开眼。现在特写用的是总攻击结束立绘的脸（眼睛是睁开的），只能用一道光扫过代替睁眼。需要同一张图的闭眼版和半睁版，叠在原图上换帧。

【找到仓库】
在本机找 remote 指向 github.com/4yndm7wh95-spec/persona、且含 mod\P3Makoto.json 的 git 仓库（C:\Users\kkk\ 下、桌面、Documents；可能有多个副本，用你上次成功推送的那个），下文记作 <repo>。所有 git 命令加 -c safe.directory=<repo>。
先 git status：有未提交改动就停下报告。然后 checkout claude/amazing-darwin-b00arn，
git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试，最多 4 次），git merge --ff-only origin/claude/amazing-darwin-b00arn（不能快进就 git merge --no-edit origin/claude/amazing-darwin-b00arn，不要 rebase）。
如果仓库是稀疏检出、需要的参考文件不在磁盘上：允许执行 git -c safe.directory=<repo> sparse-checkout add <需要的目录>（只是把已有文件检出到本地）；路径过长报错时允许单次命令加 -c core.longpaths=true。不要 sparse-checkout disable / set。

【源图】<repo>\mod\pck_stage\p3makoto\ui\allout_finish.png（1299×1144，P3R 原版结束立绘，RGBA）。脸的大致范围是 x 850~1300、y 60~560（眼睛约在 (1005,255) 和 (1145,268)）。

【要做的】输出到 <repo>\art\v14\face\：
1. face_src.png：从源图裁出 x 800~1299、y 0~640 的区域（500×640），原样保存，作为编辑的输入和对照。
2. face_closed.png：用 imagegen 的**编辑**功能，在 face_src.png 上只改眼睛：双眼自然、平静地闭上（上眼睑垂下成一条带睫毛的弧线，神情安详）。其余所有像素（头发、脸型、光影、线条、颜色、衣服、背景透明区）保持不变。输出必须仍是 500×640、和 face_src.png 完全对齐。
3. face_half.png：同样方式，双眼半睁（眼睑盖住上半个瞳孔，视线向下）。
4. 如果编辑结果改动了眼睛以外的区域：用 face_src.png 把眼睛区域以外的像素还原（只保留眼睛周围约 160×90 的两个椭圆区域的修改，边缘羽化），保证叠到原图上看不出接缝。
5. preview_face.png：三张（原图睁眼 / 半睁 / 闭眼）并排，下面再放三张眼部区域 3 倍放大。
6. README.md（中文）：提示词、编辑次数、是否做了第 4 步的还原、已知问题。

【自检】闭眼版一定要是真闭眼；颜色和线条要和原图一致（同一张插画的画风），不能变成另一种画风或换了个人；眼睛以外不能有可见变化。不满足就重做（最多 4 轮），仍不满足就如实报告。

【提交与推送】
- 只 git add art\v14\face\ 下的文件。提交：git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "art: Makoto face close-up eyes closed / half (edited from the original finish portrait)"
- push：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn（失败等 5/10/20/40 秒重试）。被拒绝是因为别的会话先推送了：先 fetch，再 git merge --no-edit origin/claude/amazing-darwin-b00arn（不要 rebase），然后再 push。
- 完成后用中文报告做了什么、结果在哪、提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要改写 git 历史（rebase / reset / amend）；不要用 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件（只读）。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护；被拒绝时换一种中性的描述重试一次，仍被拒就停下报告。
- 遇到上面没写到的情况，停下来报告，不要自作主张。
