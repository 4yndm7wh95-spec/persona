# Codex 生图任务：悠的收尾立绘——用 P4R 的这张图，抠出来再扩图

> 用途：总攻击（Series Finale）结算画面里的悠。之前自己设计的姿势都不用了，用户决定直接用这张 P4R 的悠（握着金色护手的太刀、刀尖朝左下）：
> `research/p4r/keyart/yu_katana_cutin_user.jpg`（1206×677）。
> 要做的只有两件事：**把悠抠出来（背景全部去掉）**，**把被画面边缘切掉的部分补全（扩图）**。人物本身一笔都不要改。

你在用户的 Windows 电脑上工作，用 imagegen 的**图像编辑模式**。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 `art/p4/finale/yu_finale_katana.png`；工作区里别的未提交改动原样保留。

## 输入
- 原图 `research/p4r/keyart/yu_katana_cutin_user.jpg`。
- 底图 `research/p4r/keyart/yu_katana_cutin_canvas.png`（1536×1024）：原图原样放在 x = 330、y = 40 的位置，左边、下边和上边一圈是透明的——**透明的地方就是要往外补的地方**。用它做编辑的底图，悠在画面里的位置和大小保持不变。

## 输出 → `art/p4/finale/yu_finale_katana.png`（1536×1024，PNG RGBA 真透明底）
1. **人物原样保留**：姿势、脸、表情（嘴角淡淡的笑）、眼镜、头发、两只手、金色雕花护手、黑色刀柄、刀身、外套和上面的缝线、领子的千鸟格花纹和校徽、光影、P4R 官方插画的画风，全部和原图一样，不要重画、不要改风格，位置和大小和底图一致。
2. **背景全部去掉，变成透明**：黄色和灰色的圆、圆环、花、星星、网点、左边的彩虹弧线，还有**人物外面那一圈浅灰色描边**，都不要。轮廓外 alpha = 0。
3. **补全被切掉的部分**（画进透明区域，线条、颜色、阴影和原图接得上）：
   - 左边的肩膀和外套；
   - 外套往下补到大约腰部；
   - 刀身顺着原来的方向往左下补完，一直到刀尖，是一把完整长度的太刀，**刀尖要在画面里面**；
   - 头发顶部如果被切掉了也补上。
4. 边缘干净清晰：原图分辨率低，输出要像高清原画一样清楚；边缘没有白边、光晕，也不要残留背景的黄色或灰色（特别是灰色头发和黑色外套的边上）。四周留白至少 30px。

**提示词（配底图用图像编辑）**：
> Edit this picture of Yu Narukami from Persona 4 Revival (official P4R illustration: he holds a katana with an ornate gold guard close to his face, the blade pointing down to the lower left). Keep Yu exactly as he is - pose, face, faint smile, glasses, silver-grey hair, both hands, the gold guard, the black wrapped handle, the blade, the black Yasogami jacket with its stitching, the houndstooth collar and badges, the lighting and the art style - same position and size, nothing redrawn or restyled. Remove the whole background, including the yellow and grey circles, rings, flowers, stars, halftone dots, the rainbow arcs and the light grey outline around him, so everything outside him is fully transparent. Then extend him into the transparent area where the original picture cut him off: his left shoulder and the rest of the jacket on the left, the jacket down to about the waist, and the katana blade continued in the same direction down to the lower left all the way to its tip (a full-length katana, the tip inside the picture), and the top of his hair if it was cut. The extended parts must match the original lines, colours and shading exactly. Clean, sharp edges with no halo and no leftover background colour. Transparent background, no text, no frame.

提交信息：`art(p4): Yu finish-card art from the P4R katana cut-in (cut out and extended)`，推送。

## 安全要求（绝对不要做）
- 不要 rebase 或 force push；不要读取 `C:\Users\kkk\.p3record\obs.json` 或任何密钥文件；只往游戏的 mods 文件夹里复制文件。
- 不要 reset / amend；不要 `--force`；不要改全局 git config。
- 不要读取或输出任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的 1 个文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
