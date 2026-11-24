# P5 卡面替换 · 1.0.0

独立的 Slay the Spire 2 卡面 Mod，替换静默猎手卡池 91 张插画 + 小刀牌（共 92 张），保留原版卡框、文字和布局。不依赖 P5Joker，不改角色、动画、音乐或玩法；可单独装，也可和 P5Joker 一起用。结构照搬 P3CardArt 1.0.2（`claude/admiring-knuth-lqj1x7` 的 `cardart/`）。

将 `P5CardArt` 文件夹放进游戏的 `mods/`，内含 `P5CardArt.json`、`P5CardArt.dll`、`P5CardArt.pck`。在 Mod 管理里启用或停用后重启游戏；没覆盖的卡保留原图。

- 卡图来源：`art/p5/cards/round1`（第一轮，77 张审批通过）+ `art/p5/cards/round2`（第二轮重出的 14 张 + 小刀）；审批记录在审批台 https://claude.ai/artifact/Vrxut7p2i4godAwoYjztFG ；每张用的哪一版见 `sources.json`。
- 导出：`python3 tools/p5/cardart/export_card_art.py` → `pck_stage/p5cardart/card_art/`：92 张 `<卡名>.jpg`（250×190，远古牌 250×351，即卡牌上实际画的尺寸）+ 92 张 `<卡名>_big.jpg`（2 倍，给 0.113 的完整查看大图）+ `index.txt`，共 185 个文件，约 11.5 MB。运行时用 FileAccess 读，不需要 .import。
- 编译：`dotnet build p5cardart/P5CardArt.csproj -c Release '-p:GameDir=<游戏目录>'`。
- 打包：按 `docs/BUILD.md` 用 GDRE Tools 从 `p5cardart/pck_stage` 的副本打包成 `P5CardArt.pck`（PCK v3、引擎 4.5.1），包里只有 `p5cardart/card_art/` 下那 185 个文件。
- 日志里应有 `[P5CardArt] init v1.0.0` 和 `[P5CardArt] card art: 92 illustrations in pck`。
