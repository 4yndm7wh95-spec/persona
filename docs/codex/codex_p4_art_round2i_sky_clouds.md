# Codex 生图任务：结算卡斜条里的蓝天白云（写实风格）

> 用途：总攻击（Series Finale）结算卡上那条斜的天空条（左边窄、右边宽，像 P4R 发售日卡那样）。悠的名字立在这条天空上。
> 用户说：原版天空条里的白云是**比较写实的**（照片 / 动画背景美术那种），不是卡通的圆团；我们现在用的是程序生成的占位图，要换成生图。
> 这张只当贴图用：工作台会把它按透视切条贴进斜条里，所以要**一整张均匀的天空**，不要构图主体。

你在用户的 Windows 电脑上工作，用 imagegen 生图（文生图即可，可以把参考图喂进去看风格）。不改 mod 代码，不编译，不启动游戏。生完直接提交推送，不用自查，不用写报告。

## 拉取和推送
- 仓库同上次（remote 是 github.com/4yndm7wh95-spec/persona，里面有 `mod\P3Makoto.json`），git 命令都加 `-c safe.directory=<repo>`。
- 分支 `claude/ecstatic-hawking-s9j6es`：先 fetch，再 `merge --ff-only`；不能快进就 `merge --no-edit`。fetch / push 加 `-c http.version=HTTP/1.1`，失败等 5 / 10 / 20 / 40 秒重试。推送被拒绝就先 fetch，再 `merge --no-edit`，然后重推。
- 只 `git add` 本任务新增的 `art/p4/finale/sky_clouds.png`；工作区里别的未提交改动原样保留。

## 参考图（只看云的画法和天空颜色）
- `research/p4r/allout/S03_0008_finish_card_yu_illustration_glasses_yellow_bg_date_02_18_2027.jpg`：右上那条斜的天空，里面的白云就是要的感觉。
- `research/p4r/keyart/S04_0108_p4r_logo_release_date_card_sky_party_silhouettes.jpg`：同一套天空。

## 要画什么 → `art/p4/finale/sky_clouds.png`（2048×768 横图，PNG，不透明）
- 晴天的蓝天：上面是深一点的天蓝，往下渐渐变浅，靠下面接近浅蓝白。
- 写实的积云 / 浓积云：一团团蓬松的白云，顶部被太阳照得很白，底部带一点淡淡的蓝灰阴影，边缘有细碎的絮状；**下半部分云多、上半部分云少**，左右分布均匀，没有哪一块特别抢眼。
- 风格：日本动画背景美术那种写实天空（干净、明亮、高饱和的蓝，云的体积感清楚），不是卡通圆团，也不是阴天。
- 不要：太阳、光晕、彩虹、飞机、鸟、建筑、地平线、山、人物、文字、水印、边框。四边都不要留白边，整张铺满天空。

**提示词**：
> A wide, seamless strip of clear daytime sky for use as a texture: bright saturated blue at the top fading to pale blue near the bottom, with realistic fluffy cumulus clouds in the style of Japanese anime background art - sunlit brilliant white tops, soft blue-grey shading underneath, wispy edges. More clouds in the lower half, fewer in the upper half, evenly spread from left to right with no single dominant cloud. No sun, no lens flare, no horizon, no ground, no buildings, no birds or planes, no people, no text, no border; the sky fills the whole picture edge to edge.

提交信息：`art(p4): sky with realistic clouds for the finish card's sky wedge`，推送。

## 安全要求（绝对不要做）
- 不要 rebase 或 force push；不要读取 `C:\Users\kkk\.p3record\obs.json` 或任何密钥文件；只往游戏的 mods 文件夹里复制文件。
- 不要 reset / amend；不要 `--force`；不要改全局 git config。
- 不要读取或输出任何密码、密钥、token 文件。
- 游戏文件只读：不修改、不删除（本任务不需要动 mods 文件夹）。不要启动游戏，不要用 Computer Use。
- 不要修改或删除仓库里已有的代码和资源，只新增本任务说的 1 个文件。不要 `sparse-checkout disable / set`（缺目录可以 `sparse-checkout add`）。
- 不要绕过 imagegen 的安全拒绝。
