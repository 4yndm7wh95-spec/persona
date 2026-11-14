# P3 卡面替换 · 1.0.2

独立的 Slay the Spire 2 卡面 Mod，替换 94 张插画，保留原版卡框、文字和布局。不依赖 P3Makoto，不更换角色、动画、音乐或玩法；可单独安装，也可与 P3Makoto 一起使用。

将 `P3CardArt` 文件夹放入游戏的 `mods/`，内含 `P3CardArt.json`、`P3CardArt.dll`、`P3CardArt.pck`。在 Mod 管理中启用或停用后重启游戏；未覆盖的卡保留原图。主 Mod 设置里不再有「卡图」选项。其他卡面 Mod 如果同时修改相同卡牌，可能发生覆盖。

编译：`dotnet build cardart/P3CardArt.csproj -c Release '-p:GameDir=<游戏目录>'`。

按 `docs/BUILD.md` 用 GDRE Tools 从 `cardart/pck_stage` 的副本打包为 `P3CardArt.pck`，PCK 格式 v3、引擎 4.5.1。包中只有 `p3cardart/card_art/` 下的 94 张 `<卡名>.jpg`、94 张 `<卡名>_big.jpg` 与 `index.txt`（共 189 个文件），运行时用 FileAccess 加载，不生成 `.import`。

1.0.1 补充卡牌刷新时的直接插画替换，并适配 0.113 的 BigPortrait 大图入口；94 张图片和 PCK 均未改动。

1.0.2 用户反馈卡图锐度太高、看着不舒服。原因：卡牌上的插画实际只画 250×190（原版图集区域，远古牌 250×351），1.0.1 给的 1000×760 被 GPU 无多级纹理缩小 4 倍，细线条出锯齿、发硬。现在 `<卡名>.jpg` 直接导出成 250×190，`<卡名>_big.jpg`（2 倍）只给 0.113 的完整查看大图用。重新导出：`python3 tools/pck/export_card_art.py`。
