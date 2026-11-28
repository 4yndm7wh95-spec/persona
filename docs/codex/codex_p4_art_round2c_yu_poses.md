# Codex 生图任务：悠的收尾立绘，一次出 4 种姿势给用户挑

> 用途：总攻击（Series Finale）结算画面里的悠（画面左半边的主体，背景是黄色的结算卡）。
> 前两张（`yu_finale.png` 扛刀推眼镜、`yu_finale_v2.png` 双手插兜）用户都觉得**姿势很奇怪**，不用了。
> 这次一次画 4 种不同的姿势，用户从里面挑。**最重要：人物不能画走样（OOC）**，脸、发型、眼镜、校服都照参考图。

你在用户的 Windows 电脑上工作，用 imagegen 生图（参考图模式）。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 4 个文件 `art/p4/finale/yu_pose_a.png` ~ `yu_pose_d.png`；工作区里别的未提交改动原样保留。

## 4 张都一样的要求
- 1024×1536，PNG RGBA **真透明底**：轮廓外 alpha = 0，不要背景、地面阴影、光晕、文字、边框；四周留白至少 40px，不要被裁掉。
- 画风：P4R 官方插画（干净线条、赛璐璐硬阴影），和下面的官方图一致。
- 人物设计：银灰色头发、细框眼镜、黑色立领八十稻羽校服外套敞开、白衬衫、黑裤子；太刀是黑色刀柄、银色刀身。
  照 `research/p4r/keyart/S04_0004_yu_official_full_body_art_fist_raised_yellow_blue_bg_title_card.jpg`（官方全身）、
  `research/p4r/allout/S03_0008_finish_card_yu_illustration_glasses_yellow_bg_date_02_18_2027.jpg`（官方收尾卡）。
  `art/p4/finale/yu_finale.png` 只能参考脸和衣服，**不要照它的姿势**。
- 姿势要自然、好看：身体有一条清楚的动势线（不要直挺挺站着拍照），比例正常（约 7 头身，腿和身子不要拉长），
  最多只有一只手在脸边，手指自然；手指不要戳在鼻梁上、不要挡住眼睛；镜头稍微仰一点。外套下摆和刘海被风吹起。

## A. 推眼镜（P4R 收尾卡那种）→ `art/p4/finale/yu_pose_a.png`
- 近景：头顶到腰。身体 3/4 侧向画面左边，微微前倾，头往抬起的手那边轻轻歪。
- 右手（画面左侧那只）抬到脸的侧面：食指和中指搭在**眼镜框侧边靠近太阳穴的地方**往上推，大拇指在颧骨下面，手掌朝着脸，前臂竖着、手肘朝下。手只挡住太阳穴，两只眼睛完全露出来。
- 左臂自然垂下，大部分被外套挡住。眼睛透过镜片往上看着镜头，冷静、锐利，嘴角很淡地一挑；一边镜片上有一道白色反光。外套往画面右边飘。
- 参考：`research/p4r/allout/S03_0008_finish_card_yu_illustration_glasses_yellow_bg_date_02_18_2027.jpg`、`research/p4r/keyart/S07_0150_release_date_yu_yellow_flowers.jpg`。
> Yu Narukami from Persona 4 Revival, official P4R anime illustration style, clean line art, cel shading with hard shadows. Close-up from the top of the head to the waist, seen from a slightly low angle. His body is turned three-quarters toward the viewer's left and leans a little toward the viewer, head tilted slightly toward his raised hand. His right hand (on the viewer's left) is raised beside his face: the index and middle fingers rest on the side of his thin-framed glasses near the temple, pushing them up; the thumb is under his cheekbone, the palm faces his cheek, the forearm is upright and the elbow points down. The hand covers only the temple; both eyes are fully visible. His left arm hangs relaxed, mostly hidden by the jacket. He looks up at the viewer through the lenses, calm and sharp, with a very faint smile; a white glint on one lens. Silver-grey hair, black high-collared Yasogami school jacket worn open over a white shirt, the jacket swept out to the viewer's right by the wind. Natural proportions. Transparent background, no text, no frame.

## B. 收刀（刚打完最后一下）→ `art/p4/finale/yu_pose_b.png`
- 头顶到膝盖。身体 3/4 侧向画面右边，重心落在后腿，肩膀放松，上身稍微拧一点。
- 右手在胯部的高度握着太刀，手臂自然下垂；刀身斜着朝下、朝身后，刀尖快碰到地面；整把刀都在画面里，不要横过他的腿。
- 左手放松垂在身侧。头微微低着转向镜头，眼睛从刘海下面往上看着镜头，冷静、自信。
- 参考：`research/p4r/allout/S01_4918_series_finale_prompt_cancel_go_yu_katana_yellow_stage.jpg`、`research/p4r/yu_battle/S04_0050_yu_katana_finish_pose_spotlight_enemy_dissolving_smoke.jpg`。
> Yu Narukami from Persona 4 Revival, official P4R anime illustration style, clean line art, cel shading with hard shadows. From the top of the head to the knees, slightly low angle, right after his finishing blow. His body is turned three-quarters toward the viewer's right, weight settled on the back leg, shoulders relaxed, a slight twist in the torso. His right hand holds a katana by the grip at hip height, the arm hanging naturally; the blade angles down and back behind him, the tip just above the ground; the whole blade is inside the frame and does not cross his legs. His left arm hangs loosely at his side. His head is slightly lowered and turned to the viewer; he looks up at the viewer from under his bangs through thin-framed glasses, calm and confident. Silver-grey hair, black high-collared Yasogami school jacket worn open over a white shirt, black trousers; the jacket hem and bangs blown back by the wind. Natural proportions. Transparent background, no text, no frame.

## C. 回头一瞥（P4R 胜利时那种）→ `art/p4/finale/yu_pose_c.png`
- 头顶到大腿。背对镜头 3/4（看到他的背和右侧），像正往画面左上方走开，回过头从右肩上方看着镜头；脸是 3/4 侧脸，在头的右边。
- 右手在身侧握着太刀，刀尖朝下，刀身沿着腿往下（刀尖可以出画面下边）。左手放松。表情冷静，嘴角很淡地一挑，眼神透过眼镜很锐利。
- 参考：`research/p4r/yu_battle/S02_0221_yu_closeup_glance_over_shoulder_victory.jpg`、`research/p4r/yu_battle/S02_0214_yu_idle_katana_yellow_outline_rear_3q.jpg`。
> Yu Narukami from Persona 4 Revival, official P4R anime illustration style, clean line art, cel shading with hard shadows. From the top of the head to the thighs. His back is three-quarters to the viewer, as if he is walking away toward the upper left, and he looks back over his right shoulder straight at the viewer; his face is in three-quarter view on the right side of his head, calm, with a very faint smile and sharp eyes behind thin-framed glasses. His right hand holds a katana at his side, pointing straight down along his leg (the tip may leave the bottom of the frame). His left arm is relaxed. Silver-grey hair, black high-collared Yasogami school jacket, its hem lifted by the wind. Natural proportions. Transparent background, no text, no frame.

## D. 握拳（P4R 官方立绘那种）→ `art/p4/finale/yu_pose_d.png`
- 头顶到腰。身体 3/4 侧向画面右边。右手在胸前握拳（大约锁骨高度），手肘弯着，拳头不要挡脸。
- 左臂放松垂在身侧。眼睛直视镜头，坚定、自信，嘴角很淡地一挑。外套敞开，被风吹起。
- 参考：`research/p4r/keyart/S04_0004_yu_official_full_body_art_fist_raised_yellow_blue_bg_title_card.jpg`。
> Yu Narukami from Persona 4 Revival, official P4R anime illustration style, clean line art, cel shading with hard shadows. From the top of the head to the waist, slightly low angle. His body is turned three-quarters toward the viewer's right. His right fist is clenched in front of his chest at about collarbone height, elbow bent, the fist not covering his face. His left arm hangs relaxed at his side. He looks straight at the viewer, determined and confident, with a very faint smile. Silver-grey hair, thin-framed glasses, black high-collared Yasogami school jacket worn open over a white shirt, the jacket swept back by the wind. Natural proportions. Transparent background, no text, no frame.

提交信息：`art(p4): four Yu finish-card pose candidates (glasses, katana low, glance back, fist)`，推送。

## 安全要求（绝对不要做）
- 不要 rebase 或 force push；不要读取 `C:\Users\kkk\.p3record\obs.json` 或任何密钥文件；只往游戏的 mods 文件夹里复制文件。
- 不要 reset / amend；不要 `--force`；不要改全局 git config。
- 不要读取或输出任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的 4 个文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
