你在用户的 Windows 电脑上工作。任务：编译、打包、安装 0.15.31（94 张 P3R 卡图接入游戏）。代码和资源都已在分支上，不改代码，不重新生成图片。

【1. 仓库】
git -c http.version=HTTP/1.1 fetch origin claude/admiring-knuth-lqj1x7（失败等 5/10/20/40 秒重试，最多 4 次），再 `merge --ff-only origin/claude/admiring-knuth-lqj1x7`；不能快进就停下来报告。工作区有别的会话的改动就用独立干净工作区，不要碰那些改动。

【2. 编译】
`dotnet build mod/P3Makoto.csproj -c Release '-p:GameDir=<游戏目录>'`，要求 0 错误 0 警告。版本号已是 0.15.31，不要改。

【3. 打包】
按 docs/BUILD.md 用 GDRE Tools 重新打包 PCK。这次新增 `p3makoto/card_art/`：94 张 `<卡名>.jpg` + `index.txt`，原样打进去，不需要 .import（运行时用 FileAccess 读）。
- 如果打包流程里会跑 `tools/pck/remove_card_skins.py`：它只删 `p3makoto/cards/`、`card_frame/`、`approved_cards/`、`card_art_v4/`，不会碰 `card_art/`；打包后确认 `card_art/` 还在。
- 打包后列出 pck 里 `p3makoto/card_art/` 的文件数（应为 95 = 94 张 jpg + index.txt），写进报告。

【4. 安装】
备份旧的 dll / pck 后安装进游戏，按惯例生成 dist ZIP。

【5. 实机检查】（能启动游戏就做，不能就写明没做）
- 开一局结城理，看手牌、选牌奖励、牌组界面的卡图是不是新图；卡框、文字是不是原版。
- 设置 → 结城理 → 「卡图」切到「原版」，新抽到的牌应该变回原版图。
- 日志里要有 `card art: 94 illustrations in pck`，没有 `card art ... could not be decoded`。
- 顺便进一次商店，把日志里 `perf velvet shop install`、`perf shop stage built`、`perf shop warmup` 这几行抄进报告（0.15.30 的卡顿修复还没收到实测数据）。

【6. 提交与推送】
只提交报告（docs/codex/reports/ 下）和打包清单之类的产出，不改 mod/src。
git -c user.name=Codex -c user.email=codex@local commit -m "build: 0.15.31 card art package and install report"
git -c http.version=HTTP/1.1 push origin HEAD:claude/admiring-knuth-lqj1x7（失败等 5/10/20/40 秒重试，最多 4 次）
