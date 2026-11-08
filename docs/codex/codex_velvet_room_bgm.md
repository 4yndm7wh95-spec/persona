你在用户的 Windows 电脑上工作。任务分两步：① 从本机 P3R 游戏文件里找出**天鹅绒房间的背景音乐**（一段钢琴曲），转成 OGG 放进模组资源；② 编译、重新打包 PCK，把 0.15.23 装进游戏。不启动游戏，实机效果由用户验收。

【背景】
0.15.23 已经推送到 `claude/admiring-knuth-lqj1x7`：
- 伊戈尔商店房间改成分层背景，栏杆后面隔 8～15 秒有一部电梯一闪而上（新素材在 `mod\pck_stage\p3makoto\shop\elevator_*.png`、`lantern_left/right.png`，shader 是 `mod\pck_stage\p3makoto\shaders\velvet_shop.gdshader`）。
- 新增 `mod\src\Audio\VelvetShopMusic.cs`：进入伊戈尔商店时读取 `res://p3makoto/bgm/velvet_room.ogg` 循环播放，同时把原版背景音乐淡出，离开房间时淡回。**文件不存在时代码会跳过，保留原版音乐**，所以缺的只是这首曲子本身。
- 云端已用游戏 dll 编译通过；velvet_shop.gdshader 已用 Godot 4.5.1 实际渲染检查过。

【1. 仓库】
沿用上次的独立干净工作区 <wt>（`<repo>-elevator`；已经删掉的话按上次的方法用 `git worktree add` 重建）。原工作区里其他会话的 P5 改动一律不要碰。
git -c safe.directory=<wt> -c http.version=HTTP/1.1 fetch origin claude/admiring-knuth-lqj1x7（失败等 5/10/20/40 秒重试，最多 4 次），再 `merge --ff-only origin/claude/admiring-knuth-lqj1x7`；不能快进就停下来报告。

【2. 找天鹅绒房间 BGM】
- 来源：本机 P3R（Persona 3 Reload）。上次 Cue107 语音是从 `Xrd777\CriData\Stream\Voice_Facility_Velvetroom.awb` 取的，BGM 应该在同一个 `CriData\Stream`（或相邻目录）里的 BGM 类 ACB / AWB 中。用 vgmstream-cli 解，ffmpeg 转码，和上次一样。
- 目标曲：天鹅绒房间的主题曲（P3R 里叫 "Poem for Everyone's Souls -Reload-"），以钢琴为主，可能有女高音。用 ACB 里的 cue 名（含 velvet / igor / poem / facility 之类的字样）、时长和音频特征去找。
- **确定不了是哪一首时，不要猜**：把最可能的 3～6 首各截前 25 秒，转成 `art\velvet_room_bgm\candidate_<序号>_<cue名>.ogg`，连同 catalog.csv（文件、cue 序号、cue 名、时长、有无循环点）一起提交，停下来等用户选，第 3 步以后先不做。
- 能确定时，导出整首：
  - 有 HCA 循环点的（vgmstream 会报 loop start / loop end）：导出从开头到 loop end 为止的音频（不要多出循环后的部分），把 loop start 的秒数（保留 3 位小数，例如 `12.345`）写进 `mod\pck_stage\p3makoto\bgm\velvet_room.loop.txt`，文件里只写这一个数字。
  - 没有循环点：导出整首，首尾各做 0.3 秒淡入淡出，loop.txt 写 `0`。
  - 编码：OGG Vorbis，原采样率，立体声，质量 q5 左右。整体响度大约 -18 LUFS（背景音乐不能盖过技能和语音音效；比 Cue107 语音明显轻）。
  - 输出：`mod\pck_stage\p3makoto\bgm\velvet_room.ogg`。

【3. 编译与打包】
- `dotnet build mod/P3Makoto.csproj -c Release '-p:GameDir=<游戏目录>'`，要求 0 错误。版本号已经是 0.15.23（`mod\P3Makoto.json` 和 `ModEntry.Version`），不要再改。
- 这次有新素材，**必须重新打包 PCK**：按 docs\BUILD.md 用 GDRE Tools 把 `mod\pck_stage` 的副本打成 P3Makoto.pck。新的 png 是运行时直接读字节的，不需要 `.import`；`bgm\velvet_room.ogg` 和 `velvet_room.loop.txt` 也是直接读的。打包后确认 pck 里有：`p3makoto/shop/elevator_front.png`、`elevator_backdrop.png`、`elevator_car.png`、`elevator_car_glow.png`、`lantern_left.png`、`lantern_right.png`、`p3makoto/bgm/velvet_room.ogg`、`velvet_room.loop.txt`。
- 和以往一样备份后，更新游戏 `mods\P3Makoto\` 下的 dll、json、pck，并按惯例生成 `mod\dist` 的 ZIP。

【4. 提交与推送】
git -c safe.directory=<wt> add mod/pck_stage/p3makoto/bgm（以及你按惯例要提交的 dist）
git -c safe.directory=<wt> -c user.name=Codex -c user.email=codex@local commit -m "Add Velvet Room BGM for the Igor shop and package 0.15.23"
git -c safe.directory=<wt> -c http.version=HTTP/1.1 push origin HEAD:claude/admiring-knuth-lqj1x7（失败等 5/10/20/40 秒重试，最多 4 次）

【5. 报告】
用中文说明：用了哪个 ACB / AWB 的第几个 cue、cue 名、时长、循环点、响度；编译和打包结果；pck 内容检查。并提醒用户实机检查这几件事：
1. 进入伊戈尔商店时原版音乐淡出、钢琴曲淡入，离开商店时淡回原版；
2. 3～5 秒后第一次有电梯从左侧或右侧栏杆后面一闪而上（伴随低沉的"呼"声），之后每隔 8～15 秒一次；打开商品页期间不会出现；
3. 两盏吊灯照常摆动，伊戈尔照常眨眼，选中伊戈尔时的描边和商品页都正常；
4. 用音乐盒放歌时，商店的钢琴曲会自动让开。
日志里应有 `velvet room bgm started` 一行；如果出现 `velvet elevator layers missing` 或 `velvet room bgm missing`，说明对应文件没打进 pck。
