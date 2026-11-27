# Codex 任务：P4Yu 生图前先找参考图（不生图）

你在用户的 Windows 电脑上工作。这次**只上网找参考图、下载、整理、提交**，不生图，不改 mod 代码，不启动游戏。

**为什么**：之后要生成伊邪那岐大神、伊邪那岐、悠的眼神特写。用户担心生出来不像（OOC），所以先把可靠的参考找齐。用户在工作台里确认「这就是他」以后，才做生图任务 `docs/codex/codex_p4_art_round1.md`。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` `art/p4/ref/` 下的新文件；工作区里别的未提交改动原样保留。

## 要找的东西
每类挑**最清楚、最官方**的图，不要同一张图的低清重复版本。图片保持原分辨率，jpg / png 都行。

### 1. 伊邪那岐大神（Izanagi-no-Okami）→ `art/p4/ref/okami/`
- 搜索词：Izanagi-no-Okami Persona 4 official art、伊邪那岐大神 P4G、イザナギノオオカミ 公式イラスト、Izanagi-no-Okami P5R。
- 要：
  - 官方设定图 / 立绘（副岛成记画的原画，或官方画集里的图）；
  - 游戏里的模型图（P4G、P4 Arena Ultimax、P5R 里都出现过）。
- 角度和局部：正面全身至少 1 张，其他角度尽量有；头部、杖（长枪尖下面有十字护手）、身后金色圆环的特写。共 4–10 张。
- 仓库里已有一张 P4G 图鉴图 `research/p4g/persona/camp__persona__pst_0bd.png`，找到的图要和它是同一个人格面具。

### 2. 伊邪那岐（Izanagi，以 P4R 为准）→ `art/p4/ref/izanagi/`
- P4R（Persona 4 Revival）官方的高清截图、宣传图、预告片画面：官网、新闻稿、官方 YouTube / B 站 1080p 视频截图。
- 要：全身、头盔面甲特写、薙刀。共 4–10 张。

### 3. 几万真言的技能动画 → `art/p4/ref/myriad_truths/<来源>/`
- 找公开视频里伊邪那岐大神放「几万真言」（Myriad Truths）的那一段：P4G 必须有；P5R 和 P4 Arena Ultimax 里有的话也要。
- 只截技能那一段，每秒约 10 帧，存成 jpg 序列，每个来源一个子目录。
- 用户描述过原版：杖会出现在敌人面前转动，停下以后金光从敌人头顶竖直劈下来，带闪电一样的闪烁，连续好几下。截帧要把这些阶段都截到。
- 不提交完整视频。

### 4. P4R 召唤时的眼神特写（高清）→ `art/p4/ref/eyes/`
- 仓库里已有低清片段 `research/p4r/user_clips/p4r_summon_baton.mp4`（菱形里的眼睛从闭到睁）。
- 在 P4R 官方 1080p 预告或实机视频里找同一个镜头，按帧截下来：闭眼、睁开中、全睁。

### 说明文件
`art/p4/ref/README.md`：表格写清每个文件的来源链接、它是什么（哪个游戏、官方画还是游戏截图、什么角度）、分辨率。

## 提交
- 按类分开提交并推送，提交信息依次为：
  - `ref(p4): Izanagi-no-Okami references`
  - `ref(p4): Izanagi P4R references`
  - `ref(p4): Myriad Truths animation frames`
  - `ref(p4): P4R eye close-up frames`
- 做完在对话里简单说一下每类找到了几张。不用写报告文件。

## 规则和安全要求
- 只用公开渠道：官网、新闻稿、维基类网站、公开视频。不要登录账号，不要付费，不要找泄露、破解、盗版内容，不要下载游戏文件。
- 下载视频截帧用便携版 yt-dlp、ffmpeg，或浏览器截图。不运行安装程序，不用管理员权限。临时文件用完就删（用户磁盘紧）。
- 不要 rebase / reset / amend；不要 `--force`，不要 force push；不要改全局 git config。
- 不要读取或输出 `C:\Users\kkk\.p3record\obs.json`，也不要读取任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除。只往游戏的 mods 文件夹里复制文件（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
