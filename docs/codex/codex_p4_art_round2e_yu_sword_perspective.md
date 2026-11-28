# Codex 生图任务：悠的横刀立绘第二版——刀要有透视、要长、要在人前面

> 用途：总攻击（Series Finale）结算画面里的悠。接上一份 `codex_p4_art_round2d_yu_sword_pose.md`。
> 上一张（双手握刀那张）用户说**大概姿势是对的**，但刀不对：
> - 刀**平平地横在嘴前面、贴着脸**，护手画成侧面的一条细线，所以看不出前后透视，刀显得又近又短；
> - 参考图里刀的朝向不一样：刀柄从左上角朝镜头伸出来，护手几乎正对镜头，刀身斜着往右下扫，越远越细，整把刀在人前面一段距离，很长、很立体。
> 这一版只把**刀**改对，人物、表情、取景照上一张就行。**最重要：人物不能画走样（OOC）**；握刀的手按下面写的画。

你在用户的 Windows 电脑上工作，用 imagegen 生图（参考图模式）。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 `art/p4/finale/yu_sword_v2_a.png` 和 `art/p4/finale/yu_sword_v2_b.png`；工作区里别的未提交改动原样保留。

## 参考图（都用参考图模式喂进去）
- **构图示意图** `research/pose_refs/yu_sword_layout_guide.png`（1536×1024，和要画的图一样大）：只是位置、大小和透视的示意，**不要把灰色色块画出来**。
  浅灰 = 悠（头、眼镜的高度、外套）；中灰 = 他抬起来的右臂和握刀的拳头；黑色 = 刀：刀柄从左上角伸出画面（离镜头最近，最粗），
  深灰的大圆盘 = 护手（几乎正对镜头），刀身从护手出发往右下扫，越来越细，到右边快到画面边上时微微往上翘。刀身从下巴下面、领口前面经过，和脸之间有空隙。
- **姿势参考** `research/pose_refs/yu_finale_sword_pose_ref.jpg`：别的作品的角色，只借刀和手臂的感觉、镜头角度，不借人物、衣服、配色和画风。
- **人物设计**：`research/p4r/keyart/S04_0004_yu_official_full_body_art_fist_raised_yellow_blue_bg_title_card.jpg`、
  `research/p4r/allout/S03_0008_finish_card_yu_illustration_glasses_yellow_bg_date_02_18_2027.jpg`：银灰色头发、细框眼镜、黑色立领八十稻羽校服外套敞开、白衬衫。
- **刀**：`research/p4r/allout/S01_4918_series_finale_prompt_cancel_go_yu_katana_yellow_stage.jpg`：黑色刀柄缠着菱形花纹的绳、圆形黑色护手、银色单刃弯刀身，刃口有波浪形刀纹。

## 刀要这样画（这次的重点）
1. **在人前面**：整把刀离镜头比悠的脸近得多，大约在他脸前面一个小臂那么远；刀身和下巴、脸之间有明显的空隙，**不要贴着嘴、不要挡住眼睛和眼镜**。
2. **斜着放，不是水平的**：刀柄在左上，尾端伸出画面左上角；护手在画面左边、他脸的左下方；刀身从护手往**右下**扫过去，弧线很长，到画面右边快到边上时刀尖微微往上翘（右下方）。
3. **透视**：广角镜头的近景、强透视。刀柄和护手离镜头最近、最大：**护手几乎正对镜头，是一个大圆盘**（大约他脸宽的三分之二），不是侧面的一条细线；
   刀身在护手那里又宽又大，往刀尖方向因为越来越远而一路变细。
4. **要长**：一把完整的太刀，刀身从护手一直扫到画面右边，差不多横跨整个画面（大约是他肩宽的两倍长）；有明显的弧度，刃口朝下。
5. **握刀的手**：右臂抬起，手肘高高抬向左上方；右拳握在刀柄上、紧挨着护手的后面，拳头也离镜头很近、比脸上的东西都大。
   刀柄笔直地穿过攥紧的拳头，整只手把刀柄包住；看得到一排指关节和弯着的手指，大拇指从另一侧绕过来压在弯着的手指上；
   正好五根手指，关节自然，不要多指、并指，手指不要穿过刀柄，刀柄不要从手背穿出来。

## 要画什么（2 张，只有左手不一样）
- 1536×1024（横图），PNG RGBA **真透明底**：轮廓外 alpha = 0，不要背景、雪花、光效、文字、边框；刀尖完整留在画面里（刀柄尾端可以出左上角）。
- 画风：P4R 官方插画（干净线条、赛璐璐硬阴影）。悠在刀后面正面朝镜头，身体稍微侧向画面右边，眼睛透过眼镜直直盯着镜头，冷、锐利。外套被风往右后方吹起。

### A：左手往后收、握拳 → `art/p4/finale/yu_sword_v2_a.png`
- 左臂往下、往后收在身侧，左手握拳放在胯部旁边，大部分被外套挡住，不抬到刀旁边。
> Yu Narukami from Persona 4 Revival, official P4R anime illustration style, clean line art, cel shading with hard shadows. Dramatic close-up with a wide-angle lens and strong forced perspective; follow the layout guide image for where everything is and how big it is, but do not draw its grey shapes. The katana is the nearest object to the camera and is clearly in front of Yu, about a forearm's length in front of his face, with open space between the blade and his chin; it never touches his face. It runs diagonally, not horizontally: the handle at the upper left, its end leaving the picture at the top-left corner; the round guard at the left, below and to the left of his face, seen almost face-on as a large dark disc about two-thirds as wide as his face, not as a thin line; from the guard the long curved blade sweeps down toward the right and lifts slightly at the tip near the right edge. The blade is wide where it leaves the guard and becomes steadily thinner toward the tip because it goes away from the camera. It is a full-length katana whose blade crosses almost the whole width of the picture, with a clear curve, the edge facing down and a wavy temper line along it. His right arm is raised with the elbow high at the upper left; his right fist, also close to the camera and large, grips the handle just behind the guard: the handle runs straight through the tight fist, we see the row of knuckles and the curled fingers, the thumb wraps around the other side and rests on the curled fingers; exactly five fingers with natural joints, no extra or merged fingers, no fingers passing through the handle. Behind the sword Yu faces the camera, his body turned a little to the viewer's right, and stares straight into the lens through thin-framed glasses, sharp and cold; the blade passes below his chin, in front of his high collar. His left arm is pulled down and back at his side, the left hand a fist beside his hip, mostly hidden by the jacket. Silver-grey hair, the open black high-collared Yasogami school jacket over a white shirt, blown back toward the right by the wind. Katana: black handle wrapped in a diamond-pattern cord, round black guard, silver blade. Transparent background, no text, no frame, no effects.

### B：双手握刀 → `art/p4/finale/yu_sword_v2_b.png`
- 右手照上面紧挨着护手；左手握在刀柄上更靠左上角的地方（离镜头更近、更大），两只拳头之间大约一拳的距离；左前臂从胸前横过来。两只手都按「握刀的手」那几条画对。
> Same picture as above, but both hands grip the handle: the right fist just behind the guard and the left fist further up the handle toward the top-left corner, even nearer the camera and larger, about a fist's width from the right fist; both are tight fists wrapped all the way around the handle with natural fingers, exactly five on each hand; his left forearm crosses in front of his chest.

提交信息：`art(p4): Yu sword pose v2 (the katana in front, diagonal, in perspective)`，推送。

## 安全要求（绝对不要做）
- 不要 rebase 或 force push；不要读取 `C:\Users\kkk\.p3record\obs.json` 或任何密钥文件；只往游戏的 mods 文件夹里复制文件。
- 不要 reset / amend；不要 `--force`；不要改全局 git config。
- 不要读取或输出任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的 2 个文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
