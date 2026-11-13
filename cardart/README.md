# P3 卡面替换 · 1.0.0

独立的 Slay the Spire 2 卡面 Mod，替换 94 张插画，保留原版卡框、文字和布局。不依赖 P3Makoto，不更换角色、动画、音乐或玩法；可单独安装，也可与 P3Makoto 一起使用。

将 `P3CardArt` 文件夹放入游戏的 `mods/`，内含 `P3CardArt.json`、`P3CardArt.dll`、`P3CardArt.pck`。在 Mod 管理中启用或停用后重启游戏；未覆盖的卡保留原图。主 Mod 设置里不再有「卡图」选项。其他卡面 Mod 如果同时修改相同卡牌，可能发生覆盖。

编译：`dotnet build cardart/P3CardArt.csproj -c Release '-p:GameDir=<游戏目录>'`。

按 `docs/BUILD.md` 用 GDRE Tools 从 `cardart/pck_stage` 的副本打包为 `P3CardArt.pck`，PCK 格式 v3、引擎 4.5.1。包中只有 `p3cardart/card_art/` 下的 94 张 JPG 与 `index.txt`，运行时用 FileAccess 加载，不生成 `.import`。
