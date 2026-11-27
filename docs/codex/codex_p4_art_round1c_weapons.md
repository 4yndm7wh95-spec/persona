# Codex 生图任务：两把武器照原作改（伊邪那岐的薙刀、伊邪那岐大神的杖）

> 第一批的人物都没画走样，但两把武器和原作 P4G 设定图不一样。武器是人格面具的招牌，游戏里还会单独拿出来转、挥（几万真言快速版只出这根杖），所以要照原作改。**只改武器，人物、圆环都不动。** 新图另存为 `_v2`，第一批的图不要删、不要覆盖。

你在用户的 Windows 电脑上工作，用 imagegen 生图（参考图 / 图像编辑模式）。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 4 个 `_v2` 文件；工作区里别的未提交改动原样保留。

## 通用要求
- 画风和第一批一致：P4R 游戏里的动画赛璐璐风格，干净的细黑线，两三档硬阴影。
- **真透明底**：PNG RGBA，轮廓外 alpha = 0，不要背景、阴影、光晕、文字。

## A. 伊邪那岐的薙刀
**参考**：`research/p4g/persona/camp__persona__pst_001.png` 左下角那把武器（以它为准）；P4R 截图 `art/p4/ref/izanagi/p4r_033.00s_full_body_naginata.jpg`、`art/p4/ref/izanagi/p4r_049.75s_zio_cast.jpg`。

**原作的样子**：
- 刀身**宽、直**，单刃，约占全长的 1/3；刀尖斜切，不往上弯。
- 刀背靠近刀尖的一段有一排锯齿。
- 刀身靠近护手的地方有一条长条形的镂空槽。
- 护手是一块大的长方形横板，两头有铆钉，比杆子宽很多。
- 护手下面一段杆子缠着白色布带，杆身黑色。

**现在这张的问题**：刀身细，而且弯得厉害；护手太小。

**文件**：
- `art/p4/persona/izanagi/izanagi_naginata_v2.png`：约 2048×512，横放，刀刃朝右，杆子笔直。
- `art/p4/persona/izanagi/izanagi_full_v2.png`（1024×1536）：以第一批 `izanagi_full.png` 为底图做图像编辑，只把手里的薙刀换成新的样子（和 `izanagi_naginata_v2.png` 一致），人物不动。

## B. 伊邪那岐大神的杖
**参考**：`art/p4/ref/okami/p4g_compendium_0bd.png` 左下角那把武器（和 `research/p4g/persona/camp__persona__pst_0bd.png` 是同一张，以它为准）。

**原作的样子**：
- 杖头是一段**又宽又长的银色直刃**，像大剑的刃，刃尖斜切。
- 刃和杖身之间是一个**很大的十字形护手**：两根又宽又扁的银色板交叉，上面有铆钉，比杖身宽很多。
- 杖身细长、银白色。

**现在这张的问题**：杖头是短的圆锥枪尖，护手是小方块；而且全身图里的杖头又是另一种样子，两张不一致。

**文件**：
- `art/p4/persona/okami/okami_staff_v2.png`：约 2048×512，横放，刃朝右；杖身笔直、粗细均匀（要在引擎里旋转）。
- `art/p4/persona/okami/okami_full_v2.png`（1024×1536）：以第一批 `okami_full.png` 为底图做图像编辑，只把手里的杖换成新的样子（和 `okami_staff_v2.png` 一致），人物和圆环不动。

提交信息：`art(p4): weapons redrawn to match the original designs`，推送。

## 安全要求（绝对不要做）
- 不要 rebase / reset / amend；不要 `--force`，不要 force push；不要改全局 git config。
- 不要读取或输出 `C:\Users\kkk\.p3record\obs.json`，也不要读取任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除。只往游戏的 mods 文件夹里复制文件（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
