# Codex 生图任务：悠的收尾立绘——照 P4R 菜单 / 发售日卡那张「扶眼镜」的姿势重新画

> 用途：总攻击（Series Finale）结算画面里的悠。之前的几版（扛刀、插兜、握刀扩图、改挺拔）用户都不用：上一张的问题是握刀的手很用力，人却站得笔直，身体没有发力的感觉，不协调。
> 用户指定用 P4R 主菜单（也是发售日卡）里那张悠的立绘的**姿势**：**右手扶着眼镜，头微低、眼睛往右下方看，外套后摆飘起来**。
> 那张图是风格化处理过的（黄 / 褐双色、高光过曝），**只能当姿势参考**，要按 P4R 官方插画的画风重新画成我们要的立绘。
> **最重要：人物不能画走样（OOC）**；扶眼镜的手按下面写的画。

你在用户的 Windows 电脑上工作，用 imagegen 生图（参考图模式）。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 `art/p4/finale/yu_finale_glasses.png`；工作区里别的未提交改动原样保留。

## 参考图（参考图模式喂进去）
- **姿势**（只借姿势、角度和外套飘的样子，不借颜色和风格化效果）：`research/pose_refs/yu_menu_glasses_pose.jpg`（P4R 主菜单）、`research/pose_refs/yu_endcard_glasses_pose.jpg`（发售日卡，外套飘得更完整）。
- **画风和人物设计**：`research/p4r/keyart/S04_0004_yu_official_full_body_art_fist_raised_yellow_blue_bg_title_card.jpg`（官方立绘）、`research/p4r/keyart/yu_katana_cutin_user.jpg`（官方插画的脸、眼镜、校服细节）：
  银灰色头发、细框眼镜、黑色立领八十稻羽校服外套敞开（千鸟格领子、校徽、金扣子、白色缝线）、白衬衫、正常的肤色和颜色，干净线条、赛璐璐硬阴影。

## 要画什么 → `art/p4/finale/yu_finale_glasses.png`（1536×1024 横图，PNG RGBA 真透明底）
- 取景：头顶到腰部的上半身特写；人在画面左边到中间（头在左上方，头顶离上边留一点空），外套往右飘开的部分占画面右边一大块。
- 身体 3/4 侧向画面右边，肩膀放松、略微前倾，身体有一点扭转的动势，不要直挺挺站着。
- **右手扶眼镜**（他的右手，在画面左边）：抬到脸的侧面，**食指和中指并拢搭在眼镜框靠太阳穴那一侧的边上**轻轻推，无名指和小指自然弯着，大拇指在颧骨下面；手掌朝着脸，前臂竖着、手肘朝下。手只挡住太阳穴和镜框边，**两只眼睛都完整露出来**，手指不要戳在鼻梁上、不要挡眼睛。正好五根手指，关节自然。
- 头微微低下、往右歪一点，**眼睛往画面右下方看**，眼神冷静，嘴角很淡地一挑。
- 左臂自然垂着，大部分被外套挡住。
- **外套敞开，右边的下摆和后摆被风往画面右边大幅吹开**（像参考图那样一大片往右飘的黑色外套），刘海也被风带起一点。
- 轮廓外 alpha = 0，没有背景、阴影、光晕、文字、边框；四周留白至少 30px，外套飘开的边也不要被切掉。

**提示词（配上面的参考图）**：
> Yu Narukami from Persona 4 Revival, official P4R anime illustration style, clean line art, cel shading with hard shadows, natural colours. Use the pose references only for the pose, the angle and how the jacket flies - not for their colours or stylised lighting. Upper-body close-up from the top of the head to the waist, landscape picture: he is on the left half, his head at the upper left, and his open jacket blows out far to the right, filling much of the right side. His body is turned three-quarters to the viewer's right, shoulders relaxed and leaning slightly forward, with a slight twist. His right hand (on the viewer's left) is raised beside his face: the index and middle fingers together rest on the outer edge of his thin-framed glasses near the temple, pushing them up lightly; the ring and little fingers curl loosely, the thumb sits under his cheekbone, the palm faces his cheek, the forearm is upright and the elbow points down; the hand covers only the temple and the edge of the frame, both eyes fully visible; exactly five fingers with natural joints. His head is tilted slightly down and to the right and his eyes look toward the lower right of the picture, calm, with a very faint smile. His left arm hangs relaxed, mostly hidden by the jacket. Silver-grey hair; black high-collared Yasogami school jacket worn open over a white shirt, houndstooth collar, school badges, gold buttons and white stitching; the right side and back of the jacket blown far out to the right by the wind, the bangs lifted a little. Transparent background, no text, no frame.

提交信息：`art(p4): Yu finish-card art after the P4R menu pose (hand on the glasses, jacket flying)`，推送。

## 安全要求（绝对不要做）
- 不要 rebase 或 force push；不要读取 `C:\Users\kkk\.p3record\obs.json` 或任何密钥文件；只往游戏的 mods 文件夹里复制文件。
- 不要 reset / amend；不要 `--force`；不要改全局 git config。
- 不要读取或输出任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的 1 个文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
