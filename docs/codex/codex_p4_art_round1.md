# Codex 生图任务：P4Yu 第一批素材（眼神特写序列帧 + 伊邪那岐 + 伊邪那岐大神）

你在用户的 Windows 电脑上工作，用 imagegen 生图（参考图 / 图像编辑模式）。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

背景：P4Yu 是杀戮尖塔 2 的换皮 mod（铁甲战士 → P4 鸣上悠）。用户决定（2026-11-27）：**素材尽量不用 P4G 解包出来的图，用生图把 P4R（Persona 4 Revival）的样子画一遍。** 下面的 P4G 图只当设计参考，不当底图。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 `art/p4/` 下的文件；工作区里别的未提交改动原样保留。

## A. 眼神特写序列帧 → `art/p4/cutin/eyes/`
**参考**（先看视频，再看帧）：
- 视频 `research/p4r/user_clips/p4r_summon_baton.mp4` 开头约 0.1–0.45 秒（第 4–13 帧）。这是 P4R 召唤时，黄色菱形里出现的悠的眼睛特写。
- 关键帧 `research/p4r/user_clips/frames/B011.jpg`（睁开的眼睛）。要看更多帧可以自己用 ffmpeg 从视频里导出，导出的帧不用提交。

**画面内容**：
- 鸣上悠右眼的超近景（眼睛在画面中间偏左），隔着眼镜。
- 粗黑的眼镜框从左上斜到右下。镜框上缘一小段有彩虹色条纹反光（红、黄、绿、青几小格，像电视的彩条）。
- 灰色头发在阴影里偏蓝黑，从上方垂到眉毛前。皮肤浅桃色，冷蓝色的边缘光。
- 风格：P4R 游戏里的动画赛璐璐风格，干净的细黑线，两三档硬阴影，色彩饱和。

**帧**：每张 1536×1024 横版，**整幅画面、不透明**（它是填进菱形窗口里的画面，这张不用透明底）。所有帧同一个机位、同样构图、同样的眼镜和头发位置。
1. 先画 `eye_05_open.png`：眼睛完全睁开，蓝色虹膜清楚，带白色高光；镜片左下有一道青白色反光。
2. 再**以 `eye_05_open.png` 为底图做图像编辑**，只改眼皮和镜片反光，画其他几帧：
   - `eye_01_closed.png`：闭着眼（睫毛线平直），镜片上一大片青蓝色反光盖住眼睛。
   - `eye_02_slit.png`：刚睁开一条缝，反光开始往下移。
   - `eye_03_half.png`：睁开一半，虹膜露出一点，反光带一点暖黄色。
   - `eye_04_most.png`：大半睁开，虹膜大半露出。
   - `eye_06_glint.png`：和 `eye_05_open` 一样，镜框上多一个白色的小星形闪光。
3. 提交信息：`art(p4): eye close-up frames for the summon rhombus (closed -> open)`，推送。

## B. 人格面具 → `art/p4/persona/`
**风格**（两个人格面具都一样）：P4R 游戏里的动画赛璐璐风格，干净的细黑线，两三档硬阴影，高光清楚，不要厚涂和写实。**真透明底**：PNG RGBA，轮廓外 alpha = 0，不要背景、地面阴影、光晕、文字。全身完整入画，四周留白至少 60px。

### B1. 伊邪那岐 → `art/p4/persona/izanagi/`
**参考**：
- P4R 截图（设计以这些为准）：
  - `research/p4r/izanagi/S06_0106_izanagi_model_viewer_full_body_naginata_coat_lore_text_blue_circle_bg_en.jpg`（全身）
  - `research/p4r/izanagi/S01_0540_izanagi_full_body_beside_yu_glowing_eyes.jpg`
  - `research/p4r/izanagi/S04_0032_izanagi_helmet_closeup_yellow_eye_blue_rim_light.jpg`（头盔）
  - `research/p4r/izanagi/S07_0036_izanagi_mask_closeup_yellow_eyes.jpg`
- P4G 设定图（只看结构细节）：`research/p4g/persona/camp__persona__pst_001.png`。

**设计**：黑色高领长大衣、银白色头盔面甲（V 形纹）、发光的黄色眼睛，大衣内侧红色，银色靴子；武器是一把长刃薙刀。

**文件**（全部 1024×1536，薙刀单张除外）：
- `izanagi_full.png`：全身，3/4 侧身朝画面右边，双手握薙刀摆出准备姿势。
- `izanagi_body.png`：以 `izanagi_full.png` 为底图编辑，去掉薙刀，手保持握的姿势。
- `izanagi_naginata.png`：薙刀单独一张，横放，刀刃朝右，2048×512。

提交信息：`art(p4): Izanagi body, naginata and full pose`，推送。

### B2. 伊邪那岐大神 → `art/p4/persona/okami/`
**参考**：P4R 还没有公开伊邪那岐大神，设计照 P4G 设定图 `research/p4g/persona/camp__persona__pst_0bd.png`，按上面的 P4R 风格重画。

**设计**：
- 白色长大衣，金色纽扣和饰边，大衣内侧红色；暗红黑色头部，有金色细节；银白色护腿。
- 身后一个金色大圆环。
- 武器是一根很长的长杖：一端是长枪尖，枪尖下方有十字形的护手。
- 演出里他要**转动这根杖**，所以杖和圆环必须单独成图。

**文件**：
- `okami_full.png`（1024×1536）：全身，正面略偏右，身后有金色圆环，一只手在身前握着杖的中段，杖斜着。
- `okami_body.png`（1024×1536）：以 `okami_full.png` 为底图编辑，去掉杖和圆环，握杖的手保持握的姿势（之后杖在引擎里绕手旋转）。
- `okami_staff.png`（2048×512）：长杖单独一张，横放，枪尖朝右，杖身笔直、粗细均匀，方便旋转。
- `okami_ring.png`（1024×1024）：金色圆环单独一张，正圆、居中，可以有细的内圈和装饰。

提交信息：`art(p4): Izanagi-no-Okami body, staff, ring and full pose`，推送。

## 安全要求（绝对不要做）
- 不要 rebase / reset / amend；不要 `--force`，不要 force push；不要改全局 git config。
- 不要读取或输出 `C:\Users\kkk\.p3record\obs.json`，也不要读取任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除。只往游戏的 mods 文件夹里复制文件（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
