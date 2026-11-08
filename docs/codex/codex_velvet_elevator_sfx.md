你在用户的 Windows 电脑上工作。任务：① 从本机 P3R 里找天鹅绒房间的**电梯运行环境声**和**电梯经过声**，有就替换模组里的占位音效；② 编译、重打 PCK、安装 0.15.27。不启动游戏。

【背景】
0.15.27 已推送到 `claude/admiring-knuth-lqj1x7`：
- 电梯改为 shader 程序化绘制的一部大轿厢，从左右两面格栅墙后面同时可见；删除了 `p3makoto/shop/elevator_car.png`、`elevator_car_glow.png`。
- 新增两条**占位**音效（Claude 用程序合成的，不是原版）：
  - `mod\pck_stage\p3makoto\sfx\velvet_elevator_hum.ogg`：房间里一直循环的低沉电梯运行声（SFX 总线，音量 0.30）。
  - `mod\pck_stage\p3makoto\sfx\velvet_elevator_pass.ogg`：电梯经过声，2.6 秒，由远及近渐强、**在 1.30 秒处最响**、然后渐弱远去。代码在轿厢出现前 0.96 秒开始播放，让最响的时刻正好对上轿厢经过墙面中间。
- 背景音乐音量调高了（原来比伊戈尔语音低约 14dB，基本听不到）。

【1. 仓库】
沿用独立工作区 <wt>，fetch 并 `merge --ff-only origin/claude/admiring-knuth-lqj1x7`；不能快进就停下来报告。原工作区的其他改动不要碰。

【2. 找原版音效】
用户记得原版天鹅绒房间有电梯运行的环境声，以及电梯从旁边经过时声音渐大又远去的效果。
- 在 P3R 的 `Xrd777\CriData\` 下找天鹅绒房间相关的环境音 / 音效 ACB、AWB（cue 名可能含 velvet、elevator、facility、amb、env 之类；上次 Cue107 来自 `Stream\Voice_Facility_Velvetroom.awb`，可以从同一批 Facility 文件找起）。用 vgmstream-cli 解，ffmpeg 转 OGG。
- **找到环境声**：如果有循环点，导出到 loop end，并保证首尾无缝（检查接缝）。覆盖 `velvet_elevator_hum.ogg`，响度大约 -16 LUFS。
- **找到经过声**：覆盖 `velvet_elevator_pass.ogg`，裁成"最响的时刻在 1.30 秒"（前面留渐强、后面留渐弱，总长 2～3 秒），响度大约 -18 LUFS。如果原版素材做不到这个形状，就把最响时刻的秒数写进报告，**不要改代码**，由 Claude 来调。
- **确定不了是哪条时，不要猜**：把候选各截一段放到 `art\velvet_room_sfx\candidate_<序号>_<cue名>.ogg`，附 catalog.csv（文件、cue 序号、cue 名、时长、有无循环点），保留占位音效继续打包，报告里说明等用户挑。
- 找不到就保留占位音效，在报告里说明找过哪些文件。

【3. 编译与打包】
- `dotnet build mod/P3Makoto.csproj -c Release '-p:GameDir=<游戏目录>'`，0 错误。版本已是 0.15.27，不要再改。
- 按 BUILD.md 重新打包 PCK。pck 里不应再有 `elevator_car.png`、`elevator_car_glow.png`、`elevator_front.png`、`elevator_backdrop.png`；必须有 `p3makoto/shop/room_lattice.png`、`lattice_mask.png`、`lantern_left.png`、`lantern_right.png`，`p3makoto/sfx/velvet_elevator_hum.ogg`、`velvet_elevator_pass.ogg`，`p3makoto/bgm/velvet_room.ogg`、`velvet_room.loop.txt`。列出 `p3makoto/shop/`、`p3makoto/bgm/` 和这两条 sfx 写进报告。
- 备份后安装进游戏，按惯例生成 dist ZIP。

【4. 提交与推送】
提交你改动的音效 / 候选文件和报告（`docs/codex/reports/velvet_elevator_0.15.27.md`），推回 `claude/admiring-knuth-lqj1x7`（HTTP/1.1，失败等 5/10/20/40 秒重试，最多 4 次）。
