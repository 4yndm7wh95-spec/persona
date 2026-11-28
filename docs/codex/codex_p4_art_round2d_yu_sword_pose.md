# Codex 生图任务：悠的收尾立绘——横刀直视镜头（用户给的参考姿势）

> 用途：总攻击（Series Finale）结算画面里的悠。之前的扛刀、插兜、4 种候选姿势用户都不要。
> 用户给了一张参考图 `research/pose_refs/yu_finale_sword_pose_ref.jpg`（别的作品里的另一个角色，拍的是一张挂着的海报，画面有点斜），
> 说**这个姿势很帅，也适合我们的刀**：喜欢那只握刀的手和刀指出去的样子——刀很有透视感、很立体，像是对着屏幕，冲击力很强。
> 唯一不喜欢的是**没握刀的那只手**：摊开托在刀旁边，让整个动作显得软。这一版把那只手改掉。
> **最重要：人物不能画走样（OOC）**。脸、发型、眼镜、校服都照悠的官方图。**握刀的手最容易画错，按下面写的画。**

你在用户的 Windows 电脑上工作，用 imagegen 生图（参考图模式）。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 `art/p4/finale/yu_sword_a.png` 和 `art/p4/finale/yu_sword_b.png`；工作区里别的未提交改动原样保留。

## 参考图里的姿势（只借姿势、镜头和刀的透视，不借人物、衣服、配色和画风）
- 人正面朝着镜头，身体稍微侧一点；镜头很近，几乎贴着刀。
- **握刀的手**：他的右手（在画面左边），手臂抬起，手肘朝左上方抬高；手握在刀柄上、紧挨着圆形护手的后面，位置在头的左侧。
- **刀**：刀横在脸前，大约下巴的高度。刀柄和护手离镜头最近，所以显得很大（刀柄尾端几乎到画面左上角）；刀身从护手出发，横扫过下巴和领口的前面，
  弯着伸向画面右边，同时往远处去，越往刀尖越细：透视很强。刀身是侧面对着镜头（看得到刀纹），刃口朝下。
- **脸**：在刀身上方，眼睛直直盯着镜头，很冷、很锐利；下半张脸被刀身和领子挡住一部分。
- 外衣被风吹得往右后方飘，整个画面是一条从左上到右下的强烈斜线。
- 参考图里另一只手摊开、掌心朝上托在护手旁边——**这一版不要这样**。

## 要画什么（2 张，只有没握刀的那只手不一样）
共同要求：
- 1536×1024（横图），PNG RGBA **真透明底**：轮廓外 alpha = 0，不要背景、雪花、光效、文字、边框；四周留白至少 40px，刀尖和刀柄尾端都不要被裁掉。
- 构图：悠的脸在画面左右 40% 左右的位置、上三分之一；刀柄在左边（约 8%–20%），刀身一直伸到右边（约 90%）。取景：头顶到胸口 / 腰。
- 画风：P4R 官方插画（干净线条、赛璐璐硬阴影）。人物设计照 `research/p4r/keyart/S04_0004_yu_official_full_body_art_fist_raised_yellow_blue_bg_title_card.jpg`、
  `research/p4r/allout/S03_0008_finish_card_yu_illustration_glasses_yellow_bg_date_02_18_2027.jpg`：银灰色头发、细框眼镜、黑色立领八十稻羽校服外套敞开、白衬衫。
  刀照 `research/p4r/allout/S01_4918_series_finale_prompt_cancel_go_yu_katana_yellow_stage.jpg`：黑色刀柄缠着菱形花纹的绳，圆形黑色护手，银色单刃弯刀身，刃口有波浪形刀纹。
- 姿势照上面那张参考图（`research/pose_refs/yu_finale_sword_pose_ref.jpg`，当姿势参考图用）。刀身经过下巴和领口前面，**不要挡住眼睛和眼镜**。
- **握刀的右手（最重要）**：
  - 刀柄笔直地穿过握紧的右拳，整只手把刀柄完全包住（像攥紧一根棍子）；
  - 从镜头看过去是手背和一排指关节；四根手指弯过刀柄，指尖藏在刀柄后面；大拇指从另一侧绕过来，压在弯着的食指和中指上；
  - 食指紧贴在圆形护手的后面；
  - 手腕绷直，和刀柄顺成一条线，略微弯一点；手腕处露出外套袖口和一点白衬衫袖口；
  - 正好五根手指，关节自然，大小和脸相称；不要多指、少指、手指粘在一起，手指不要穿过刀柄，刀柄不要从手背穿出来。

### A（推荐）：另一只手往后收、握拳 → `art/p4/finale/yu_sword_a.png`
- 左臂往下、往后收在身侧，左手握成拳头放在胯部旁边，大部分被外套挡住；**不要抬到刀旁边**。整个人像一张拉满的弓，随时要劈出去。
> Yu Narukami from Persona 4 Revival, official P4R anime illustration style, clean line art, cel shading with hard shadows. Dynamic close-up from the top of the head to the chest, the camera slightly above eye level and very close to his katana; use the pose reference only for the arm, the sword and the camera angle. Yu faces the viewer, his body turned a little to the viewer's right. His right arm is raised with the elbow up and out toward the upper left; his right hand grips the katana's handle just behind the round guard, beside the left side of his head. The katana is held horizontally in front of his face at chin height: the handle and the guard are closest to the camera and look large, and the curved blade sweeps from the guard across the front of his chin and high collar toward the right side of the picture, running away from the camera and narrowing toward the tip - strong perspective; the flat of the blade faces the viewer, the edge faces down, a wavy temper line along the edge; the whole blade, its tip and the end of the handle are inside the picture. Above the blade his eyes look straight into the camera through thin-framed glasses, sharp and cold; the blade passes below his glasses and hides part of his lower face. His left arm is pulled down and back at his side, the left hand clenched into a fist beside his hip and mostly hidden by the jacket; it does not come up to the sword. Silver-grey hair and the open black high-collared Yasogami school jacket over a white shirt, blown back toward the right by the wind. The grip: the handle runs straight through his tightly closed right fist, which wraps all the way around it like a fist around a stick; we see the back of the hand and the row of knuckles; the four fingers curl around the handle with the fingertips hidden behind it; the thumb wraps around from the other side and rests on the curled index and middle fingers; the index finger sits right against the back of the guard; the wrist is firm and in line with the handle, with the jacket cuff and a little white shirt cuff at the wrist; exactly five fingers with natural joints, no extra or merged fingers, no fingers passing through the handle. Katana: black handle wrapped in a diamond-pattern cord, round black guard, silver curved single-edged blade. Transparent background, no text, no frame, no effects.

### B（备选）：另一只手也握在刀柄上 → `art/p4/finale/yu_sword_b.png`
- 双手握刀：右手照上面，紧挨着护手；左手握在刀柄尾端（靠近刀柄末端，离右手大约一拳的距离），也是攥紧的拳头、手背朝镜头、四指弯过刀柄、大拇指压在手指上；
  左前臂从胸前横过来，两个手肘弯着、绷紧，像马上要劈下去。两只手都要按「握刀的右手」那几条画对。
> Same picture as above, but both hands grip the handle: the right hand just behind the round guard, the left hand at the end of the handle near the pommel, about a fist's width from the right hand; both are tight fists wrapped all the way around the handle, the backs of the hands and the knuckles facing the viewer, the fingers curled around the handle and the thumbs resting on the curled fingers; his left forearm crosses in front of his chest, both elbows bent and tense, as if he is about to cut. Exactly five fingers on each hand, natural joints, no merged fingers, no fingers passing through the handle.

提交信息：`art(p4): Yu finish-card sword pose (A: other fist pulled back, B: two-handed grip)`，推送。

## 安全要求（绝对不要做）
- 不要 rebase 或 force push；不要读取 `C:\Users\kkk\.p3record\obs.json` 或任何密钥文件；只往游戏的 mods 文件夹里复制文件。
- 不要 reset / amend；不要 `--force`；不要改全局 git config。
- 不要读取或输出任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的 2 个文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
