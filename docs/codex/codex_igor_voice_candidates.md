你在用户的 Windows 电脑上工作。任务：从本机 P3R（日语语音）里把**伊戈尔的语音**导出成一批编号候选，供用户试听挑选；用户选完之前，不改代码、不打包。

【背景】
0.15.28 已推送到 `claude/admiring-knuth-lqj1x7`，代码已经准备好播放下面这些文件（文件不存在时静默跳过，所以现在装上也不会出错）：
- `mod\pck_stage\p3makoto\sfx\igor_welcome.ogg`：进入伊戈尔商店房间约 0.6 秒后播放一次。用户要的是那句"ようこそ、ベルベットルームへ"（欢迎来到天鹅绒房间）一类的开场白。用户确认 P3R 里有这句日语语音。
- `igor_buy_1.ogg`、`igor_buy_2.ogg`、`igor_buy_3.ogg`：每次成功购买商品时，三条轮流播放。
- 已有的 `igor_shop_cue107.ogg`（打开商品页时播放）保持不变。
- 以上伊戈尔语音在代码里统一放大 15%。原版商人的所有音效在天鹅绒房间里已全部屏蔽。

【1. 仓库】
沿用独立工作区 <wt>，fetch 并 `merge --ff-only origin/claude/admiring-knuth-lqj1x7`；不能快进就停下来报告。原工作区的其他改动不要碰。

【2. 导出候选】
- 来源一：上次取 Cue107 用的 `Xrd777\CriData\Stream\Voice_Facility_Velvetroom.awb`（以及配套的 ACB）。**全部条目都导出**，不要只挑几条。
- 来源二："ようこそ、ベルベットルームへ"很可能在剧情 / 事件语音里，而不在设施语音库里。在 `CriData` 下找文件名或 ACB cue 名含 velvet、igor、イゴール、facility、event、evt 等字样的语音库，把其中属于伊戈尔的条目也导出。如果 ACB 里有 cue 名或角色编号，可以借此筛选伊戈尔。
- 如果本机有语音识别工具（例如 whisper / faster-whisper），对所有候选做日语转写，转写结果写进 catalog（只是辅助，可以不准）；把转写里含"ようこそ"或"ベルベットルーム"的条目排在最前面，并在 catalog 里标记。没有这类工具就跳过这一步，不要为此安装大型软件。
- 用 vgmstream-cli 解码、ffmpeg 转 OGG（原采样率），不要剪辑内容，首尾静音超过 0.3 秒的部分可以裁掉。
- 输出到 `art\igor_voice_candidates\`，文件名用三位连续编号：`001_<来源简称>_<cue序号>.ogg`，编号从 001 开始连续，方便用户回复"欢迎用 012，购买用 031、044、057"。
- `catalog.csv`：编号、文件名、来源 awb/acb、cue 序号、cue 名、时长（秒）、转写（如果有）、是否含ようこそ/ベルベットルーム。
- 另外生成 `index.html`：一个简单的试听页面，每行一个编号 + `<audio controls>` + 时长 + 转写，用户在浏览器里打开就能逐条点播。
- 条目太多（超过 300 条）时，所有文件照常导出，但 index.html 里把疑似"欢迎"的条目、以及 1～4 秒的短句（适合作购买回应）排在前面。

【3. 提交与推送】
git add art/igor_voice_candidates（音频体积太大、超过 80MB 时，只提交 catalog.csv 和 index.html，音频留在本地，并在报告里写明本地路径）
commit -m "art: Igor voice candidates from P3R for the Velvet shop"
推回 `claude/admiring-knuth-lqj1x7`（HTTP/1.1，失败等 5/10/20/40 秒重试，最多 4 次）。
报告（`docs/codex/reports/igor_voice_candidates.md`）：找过哪些文件、各导出多少条、有没有转写、疑似"欢迎"的编号列表。

【4. 用户选完以后（下一轮再做，这次不做）】
把选中的条目转成 `igor_welcome.ogg`、`igor_buy_1.ogg`～`igor_buy_3.ogg`，响度统一到和 `igor_shop_cue107.ogg` 一致（约 -14.6 LUFS），然后编译、重打 PCK、安装。
