# Codex 生图任务：悠的收尾立绘——在上一张的基础上把姿势改挺拔

> 用途：总攻击（Series Finale）结算画面里的悠。上一张 `art/p4/finale/yu_finale_katana.png`（从 P4R 原图抠出来扩的）用户不想直接用：
> 1. **腰背那里像长了个肿瘤**：画面左边、他背后的肩背鼓出一大块（扩图时人体结构没画对）；
> 2. **弯腰驼背、整个人往前弓**，头往下压、肩膀往前扣，看起来很猥琐。
> 要在这张图的基础上**稍微改一下姿势**，让他**潇洒、威风**一点。**最重要：人物不能画走样（OOC）**。

你在用户的 Windows 电脑上工作，用 imagegen 的**图像编辑模式**。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 `art/p4/finale/yu_finale_katana_v2.png`；工作区里别的未提交改动原样保留。

## 输入
- 底图（图像编辑用）：`art/p4/finale/yu_finale_katana.png`（1536×1024，透明底）。
- 参考：P4R 原图 `research/p4r/keyart/yu_katana_cutin_user.jpg`（脸、眼镜、头发、手、护手、刀、校服的样子以它为准）；
  人物设计 `research/p4r/keyart/S04_0004_yu_official_full_body_art_fist_raised_yellow_blue_bg_title_card.jpg`。

## 输出 → `art/p4/finale/yu_finale_katana_v2.png`（1536×1024，PNG RGBA 真透明底）
**保持不变**：
- 脸、表情（自信地淡淡一笑）、眼镜、银灰色头发；
- 两只手握刀的位置和手势：右手握在金色雕花护手后面，左手在刀柄尾端、手指松松地张开；黑色刀柄、金色护手、银色刀身，刀尖朝左下方，**刀尖完整在画面里**；
- 黑色立领校服外套（千鸟格领子、校徽、金扣子、白色缝线）、白衬衫；P4R 官方插画画风；人在画面里的位置和大小大致不变（头在上方偏左）。

**要改的**：
- **站直**：腰背挺直，胸口打开，肩膀放平、往后展开，不要往前扣；整个人更高、更挺拔，带一点身体扭转的动势。
- **头抬起来**：下巴放平（不要往下压、不要往前伸），眼睛从镜片后面直视镜头，冷静、自信。
- **背后的轮廓要正常**：画面左边他背后的肩背、腰部线条顺下来，**不要鼓包**；肩膀大小和右边对称、自然；外套贴着身体、下摆往身后被风吹开。
- 镜头稍微仰一点，显得威风；双腿稳稳站着（入画的部分自然就行）。
- 轮廓外 alpha = 0，没有背景、阴影、光晕、文字、边框；边缘干净，四周留白至少 30px。

**提示词（配底图用图像编辑）**：
> Edit this picture of Yu Narukami from Persona 4 Revival. Keep exactly the same: his face and confident faint smile, the glasses, the silver-grey hair, both hands and how they hold the katana (right hand behind the ornate gold guard, left hand at the end of the black wrapped handle with its fingers loosely spread), the silver blade pointing down to the lower left with its tip inside the picture, the black high-collared Yasogami jacket with the houndstooth collar, badges, gold buttons and white stitching, the white shirt, the official P4R illustration style, and roughly the same position and size in the picture. Change only his posture so he looks upright, cool and commanding instead of hunched: straighten his back and lift his chest, shoulders level and drawn back, head raised with the chin level (not pushed forward or down), eyes looking straight at the viewer through the glasses. Fix the bulge on his back at the left of the picture: his back, shoulder and waist follow a natural, smooth line, the shoulders the same size on both sides, the jacket close to his body with the hem blown back behind him by the wind. Slightly low camera angle, a firm stance. Transparent background, clean edges, no text, no frame.

提交信息：`art(p4): Yu finish-card katana art v2 (upright, no bulge on the back)`，推送。

## 安全要求（绝对不要做）
- 不要 rebase 或 force push；不要读取 `C:\Users\kkk\.p3record\obs.json` 或任何密钥文件；只往游戏的 mods 文件夹里复制文件。
- 不要 reset / amend；不要 `--force`；不要改全局 git config。
- 不要读取或输出任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的 1 个文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
