# Codex 生图任务：悠的收尾立绘改姿势（双手插兜、头微抬看镜头）

> 用途：总攻击（Series Finale）结算画面里的悠（画面左半边的主体）。上一张 `art/p4/finale/yu_finale.png`（扛刀、推眼镜）用户要改姿势：
> **双手插兜，头微微抬起，看着镜头；大概的姿势和朝向不变。**
> **最重要：人物不能画走样（OOC）。** 脸、发型、眼镜、校服都和底图一致。

你在用户的 Windows 电脑上工作，用 imagegen 的**图像编辑模式**生图。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 `art/p4/finale/yu_finale_v2.png`；工作区里别的未提交改动原样保留。

## 要画什么 → `art/p4/finale/yu_finale_v2.png`（1024×1536，PNG RGBA 真透明底）
- **以 `art/p4/finale/yu_finale.png` 为底图做图像编辑**：人物大小、位置、身体朝向（3/4 侧向画面右边）、取景（头顶到大腿中部）、脸、银灰色头发、细框眼镜、黑色立领校服外套敞开、白衬衫、被风吹起的衣摆和刘海都保持底图的样子。
- 只改这些：
  - **双手插进裤兜**（两只手都插在裤子前面的口袋里，只露出一点手腕；肩膀放松、微微往后，手肘自然向外）。**不拿刀**：把太刀整个去掉（肩上、背后都不要有刀）。
  - **头微微抬起**（下巴稍微抬一点），眼睛直视镜头，眼神冷静自信，嘴角很淡地一挑，不要夸张的笑。
- 画风同底图（P4R 官方插画风格：干净线条、赛璐璐硬阴影）。轮廓外 alpha = 0，不要背景、阴影、光晕、文字、边框；四周留白至少 40px。
- 参考：底图 `art/p4/finale/yu_finale.png`；人物设计 `research/p4r/keyart/S04_0004_yu_official_full_body_art_fist_raised_yellow_blue_bg_title_card.jpg`、`research/p4r/allout/S03_0008_finish_card_yu_illustration_glasses_yellow_bg_date_02_18_2027.jpg`。

**提示词（配底图用图像编辑）**：
> Edit this image of Yu Narukami (Persona 4 Revival). Keep everything the same: his size, position and framing (head to mid-thigh), the three-quarter body turn to the viewer's right, his face, silver-grey hair, thin-framed glasses, open black high-collared Yasogami school jacket over a white shirt, and the jacket hem and bangs blown back by wind. Change only the pose: both hands tucked into his trouser pockets (only a little of the wrists showing), shoulders relaxed and slightly back, elbows out naturally; remove the katana completely. Raise his head slightly (chin a little up) and have him look straight at the viewer, calm and confident, with a very faint smile. Same P4R anime illustration style, clean line art, cel shading. Transparent background, no text, no frame.

提交信息：`art(p4): Yu finale v2 (hands in pockets, looking up at the camera)`，推送。

## 安全要求（绝对不要做）
- 不要 rebase / reset / amend；不要 `--force`，不要 force push；不要改全局 git config。
- 不要读取或输出 `C:\Users\kkk\.p3record\obs.json`，也不要读取任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除。只往游戏的 mods 文件夹里复制文件（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
