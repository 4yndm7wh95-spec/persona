# Codex 生图任务：总攻击用的两张图（悠的收尾立绘、伊邪那岐出招图）

> 用途：总攻击（Series Finale）的电视墙。中间那台电视里悠从屏幕里伸手冲出来，左边那台电视放伊邪那岐出招。用户：身后彩电的创意可以，但不能直接用现有的眼睛特写和伊邪那岐站姿图，要专门画。
> **最重要：人物不能画走样（OOC）。** 脸、发型、眼镜、校服、武器都照参考图画，不要自己发挥设计。

你在用户的 Windows 电脑上工作，用 imagegen 生图（参考图 / 图像编辑模式）。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 `art/p4/finale/` 下的文件；工作区里别的未提交改动原样保留。

## 通用要求
- 画风：P4R 官方插画的风格——干净的线条、赛璐璐硬阴影、暖黄色的光，和下面列的官方插画一致。
- **真透明底**：PNG RGBA，轮廓外 alpha = 0，不要背景、地面阴影、光晕、文字、边框。人物四周留白至少 60px（伸出来的手可以离边近一点，但不要被裁掉）。

## A. 悠的收尾立绘 → `art/p4/finale/yu_finale.png`（1024×1536）
**参考**：
- 姿势照 `research/p4r/keyart/S05_0120_yu_official_illustration_hand_reaching_yellow_card_frame_story_title.jpg`：一只手张开伸向镜头（掌心朝镜头、手离镜头最近、有一点透视放大），像要从屏幕里伸出来；身体微微前倾，眼神直视镜头。
- 人物设计照 `research/p4r/keyart/S04_0004_yu_official_full_body_art_fist_raised_yellow_blue_bg_title_card.jpg`（全身）、`research/p4r/allout/S03_0008_finish_card_yu_illustration_glasses_yellow_bg_date_02_18_2027.jpg`（眼镜、外套）：银灰色头发、细框眼镜、黑色立领校服外套敞开、白衬衫。
**画面**：腰部以上，伸出来的那只手在画面前面；不要画背景和卡框。

## B. 伊邪那岐出招图 → `art/p4/finale/izanagi_strike.png`（1536×1024）
**参考**：
- 设计照 `art/p4/persona/izanagi/izanagi_full_v2.png`（用户确认过的身体和第二版薙刀：刀身宽直、刀背锯齿、镂空槽、大长方形护手）。
- 动作照 `research/p4r/izanagi/S07_0144_izanagi_blue_naginata_slash.jpg`：双手握薙刀斜着劈下，大衣下摆和身后的白色长飘带被动作带起来。
**画面**：全身，3/4 侧身，动作幅度大；薙刀刀刃上带一层蓝白色的电光（只贴着刀刃，不要满屏闪电，背景透明）。

提交信息：`art(p4): finale art (Yu reaching out, Izanagi strike)`，推送。

## 安全要求（绝对不要做）
- 不要 rebase / reset / amend；不要 `--force`，不要 force push；不要改全局 git config。
- 不要读取或输出 `C:\Users\kkk\.p3record\obs.json`，也不要读取任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除。只往游戏的 mods 文件夹里复制文件（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
