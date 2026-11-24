你在用户的 Windows 电脑上工作。任务：编译、打包、安装独立卡面 Mod **P5CardArt 1.0.1**（相对 1.0.0 只改了导出：卡图柔化，去掉锐化感）（P5 卡面替换：静默猎手 91 张 + 小刀牌）。代码和图片都已在分支上，不改代码，不重新生成或导出图片。P5Joker 本体不用动。

规矩：不 rebase / reset / amend / stash；不 --force；不改全局 git config、系统环境变量 / PATH；不安装任何东西；不读取或输出 C:\Users\kkk\.p3record\obs.json 和任何密码 / 密钥。工作区有本地改动时用独立 worktree，不要处理那些改动。

1. `git -c safe.directory=* -c http.version=HTTP/1.1 fetch origin claude/confirm-next-tasks-k5wu6t`（失败等 5/10/20/40 秒重试，最多 4 次），基于它工作。
2. `dotnet build p5cardart/P5CardArt.csproj -c Release '-p:GameDir=<游戏目录>'`，0 错误 0 警告；按你给 P3CardArt 做过的方式做双版本（0.111 / 0.113）兼容检查。
3. 按 `p5cardart/README.md` 从 `p5cardart/pck_stage` 的副本打包 `P5CardArt.pck`：`p5cardart/card_art/` 下 185 个文件（92 张 jpg、92 张 _big.jpg、index.txt）原样打进去，不要 .import。打包后列出文件数写进报告。
4. 安装到游戏 `mods/P5CardArt/`（`P5CardArt.json`、`P5CardArt.dll`、`P5CardArt.pck`），按惯例生成 dist ZIP。
5. 能开游戏就看：开一局静默猎手或 Joker，手牌、选牌奖励、牌组界面的卡图是新图，卡框文字是原版；小刀牌是子弹图；幽魂形态（远古竖图）显示正常。日志要有 `[P5CardArt] init v1.0.1` 和 `card art: 92 illustrations in pck`，没有 `could not be decoded`。不能开游戏就写明没做。
6. 报告写进 `docs/codex/reports/`，只提交报告，推回 `claude/confirm-next-tasks-k5wu6t`（被拒先 fetch 再 merge --no-edit 再 push）。
