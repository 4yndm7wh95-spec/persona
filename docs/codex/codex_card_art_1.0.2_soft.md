你在用户的 Windows 电脑上工作。任务：编译、打包、安装 P3CardArt 1.0.2（卡图改成和原版一样的显示尺寸，解决看起来锐度太高）。代码和图片都已在分支上，不改代码，不重新生成或导出图片。主模组 P3Makoto 不用动。

1. git -c http.version=HTTP/1.1 fetch origin claude/admiring-knuth-lqj1x7（失败等 5/10/20/40 秒重试，最多 4 次），`merge --ff-only`；不能快进就停下报告。
2. `dotnet build cardart/P3CardArt.csproj -c Release '-p:GameDir=<游戏目录>'`，0.111 和 0.113 两套游戏都按你上次的方式编译和做补丁安装检查，0 错误 0 警告。版本号已是 1.0.2。
3. 按 cardart/README.md 从 `cardart/pck_stage` 的副本打包 `P3CardArt.pck`。`p3cardart/card_art/` 现在有 189 个文件：94 张 `<卡名>.jpg`（250×190，远古牌 250×351）、94 张 `<卡名>_big.jpg`（2 倍）和 index.txt，原样打进去，不要 .import。打包后列出文件数写进报告。
4. 备份后安装，按惯例生成 dist ZIP。
5. 能开游戏就看：手牌、选牌奖励、牌组界面的卡图不再发硬；0.113 的完整查看大图是清晰的（用的是 _big）。日志里要有 `init v1.0.2` 和 `card art: 94 illustrations in pck`。
6. 报告写进 docs/codex/reports/，提交并推回 claude/admiring-knuth-lqj1x7（不改 cardart/src 和 mod/src）。
