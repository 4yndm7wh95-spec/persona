# Codex 生图任务：眼神特写重画（逐帧，带扭头）

> 用户反馈（2026-11-27）：第一批眼神特写还可以，但 6 帧都是从同一张图改的，只有眨眼。原作这 0.3 秒里头有一点点扭头的意思，只做眨眼就没有这个感觉了。这次照原视频**逐帧**重画。

你在用户的 Windows 电脑上工作，用 imagegen 生图（参考图 / 图像编辑模式）。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 `art/p4/cutin/eyes_turn/` 下的文件；工作区里别的未提交改动原样保留。

## 参考
- 原视频 `research/p4r/user_clips/p4r_summon_baton.mp4` 的第 4–12 帧（30 fps，约 0.13–0.40 秒）。先把这一段多看几遍。
- 已经导出好的 9 帧：`research/p4r/user_clips/eye_frames/f04.jpg` … `f12.jpg`。**一帧对一帧**照着画。
- 角色设计和画风以第一批的 `art/p4/cutin/eyes/eye_05_open.png` 为准（用户说这批「还可以」）：头发、眼镜框、镜框上的彩条、颜色、线条都照它。
- 参考帧里只有黄色菱形里面是眼睛特写，外面是战场。生成时把眼睛特写**画满整张图**（菱形外面按同一个画面自然延伸）。黄色菱形边框、技能名（吉欧）、「人格面具!」字、右下角血条头像都不要画。

## 这 9 帧里在动的东西（都要画出来）
1. **头在转**：从斜着往下看，慢慢转向镜头。f04 的眼镜框很斜、占画面很大；到 f09–f12 镜框变平、往下移，脸颊露出来的皮肤变多。
2. **眼镜框跟着头动**：镜框上的彩条从右上方慢慢移到右边，最后一半出画。
3. **眼睛**：f04–f06 闭着，f07 开始睁，f09 基本睁开，f10–f12 全睁（蓝色虹膜、白色高光）。
4. **镜片反光**：f04 一大片青白反光盖住眼睛；f05–f08 反光往一边扫，一边青、一边黄；f09–f12 镜片整体偏黄。

## 做法
1. 先画 `eye_t09.png`（对应 f12，全睁、停住）：以 `eye_05_open.png` 为设计参考，构图照 f12。
2. 再画其他 8 帧。每帧用两张参考：
   - 对应的原视频帧：管头的角度、镜框的位置和角度、眼皮、反光；
   - `eye_t09.png`：管设计和画风，保证每帧是同一个人、同一副眼镜、同样的线条和颜色。
   - 对应关系：`eye_t01.png` ← f04，`eye_t02.png` ← f05，`eye_t03.png` ← f06，`eye_t04.png` ← f07，`eye_t05.png` ← f08，`eye_t06.png` ← f09，`eye_t07.png` ← f10，`eye_t08.png` ← f11，`eye_t09.png` ← f12。
3. 9 帧按顺序连起来看，要像一段连续的镜头：头转的幅度照原片，**相邻两帧之间不要跳**；发丝、镜框形状、彩条格数这些设计细节每帧都不要变。
4. 每张 1536×1024 横版，整幅不透明（这张是填进菱形窗口的画面，不用透明底）。放在 `art/p4/cutin/eyes_turn/`。第一批的 `art/p4/cutin/eyes/` 不要删、不要改。
5. 提交信息：`art(p4): eye close-up redrawn frame by frame with the head turn`，推送。

## 安全要求（绝对不要做）
- 不要 rebase / reset / amend；不要 `--force`，不要 force push；不要改全局 git config。
- 不要读取或输出 `C:\Users\kkk\.p3record\obs.json`，也不要读取任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除。只往游戏的 mods 文件夹里复制文件（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
