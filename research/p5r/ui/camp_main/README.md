# P5R 暂停菜单精灵与音效

对照画面为 [P5R 实机 00:02–00:12 逐帧截图](https://github.com/4yndm7wh95-spec/persona/tree/555c06c0de059e04c695bebb0df0dae322403129/docs/p5/preview/p5r-20261121-223943-frames-02-12)。主菜单看 5.724 秒，SKILL 看 6.997 秒，ITEM 列表看 8.815 秒，选中 EQUIP 的主菜单看 11.578 秒。

## 来源和导出

| 包内路径 | 仓库内容 |
| --- | --- |
| `SC.CPK → INIT/P5CAMP_00SPD.SPD` | `sheets/` 下 18 张 DDS 整图的 RGBA PNG，`sprites/` 下 961 张从原图原尺寸裁切的 RGBA PNG，`sprites.json` 与带编号的深灰蓝底联系表 `contact_sheet.png`。 |
| `SC.CPK → INIT/CAMP.PLG` | `raw/CAMP.PLG` 原样复制。 |
| `BASE.CPK → INIT/CONFIG.PLG` | `raw/CONFIG.PLG` 原样复制。 |
| `BASE.CPK → INIT/PERSONA_STATUS_LINE.PLG` | `raw/PERSONA_STATUS_LINE.PLG` 原样复制。 |
| `BASE.CPK → INIT/CAMPBGSTARANIME.DAT` | `raw/CAMPBGSTARANIME.DAT` 原样复制。 |
| `BASE.CPK → INIT/CAMPBGDAT.UAD` | `raw/CAMPBGDAT.UAD` 原样复制。 |
| `BASE.CPK → INIT/CAMPBGDATMORGANA.UAD` | `raw/CAMPBGDATMORGANA.UAD` 原样复制。 |
| `SC.CPK → INIT/CMPTABLE.BIN` | `raw/CMPTABLE.BIN` 原样复制。 |
| `BASE.CPK → INIT/EVTWIPETEX.PAK` | `raw/EVTWIPETEX.PAK` 原样复制。子条目 `test.dds` 和 `test2.dds` 分别导出为 `raw/EVTWIPETEX/test.png`（256×64）和 `test2.png`（256×256），均保留 RGBA。 |
| `SOUND_E.CPK`、`SOUND_J.CPK → SOUND/SYSTEM.ACB` | [camp 音效](../../audio/camp/)各 10 个 OGG，沿用已有目录。流号 170、171、172、177、224–228、256，覆盖任务单指定的 7 个流名。`catalog.csv` 保留原列和每个流的文件名、时长、采样率。 |

按 `index.csv` 列出的名称选取文件。使用 P5RAssetTool（CriFsV2Lib 2.1.2），参数为 `<CPK路径> <每行一个包内路径的清单> <p5r_rip/raw/SC 或 p5r_rip/raw/BASE>`。使用 PersonaEditor 1.6.7.0 的 PersonaEditorLib 读取 SPD 和 PAK。PNG 用 Pillow 解码 DDS 到 RGBA，按 SPD 原始 `x, y, width, height` 裁切。整图、切图和转场 PNG 保留尺寸与透明通道，使用无损 PNG 编码。联系表中的缩略图用于预览。

SPD 有 1,136 条坐标记录，其中 175 条宽或高为零，原始索引、坐标和注释以 `zero_size` 留在 JSON 中。其余记录均裁切成功，无缺失图集或越界。原始 SPD 为 61,579,904 字节，保留在本机 `C:/Users/kkk/sts2_tools/p5r_rip/raw/SC/INIT/P5CAMP_00SPD.SPD`。所有提交文件均小于 20 MB。

PersonaEditorLib 将三个 PLG 识别为无子条目的 DAT，现有工具未提供可用的几何导出。PLG 保留原文件。星纹背景数据和 CMPTABLE 同样保留原始字节。

## 认出来的素材

表中的编号指 `sprites/<编号>.png`，对应原表的零起始索引。上屏时的颜色、描边和形变可能由游戏另外施加。

| 参考画面的部件 | 已定位文件 |
| --- | --- |
| 底部弯曲的 COMMAND | `00201.png`，来自 `p5camp_04.png`。 |
| 「Don't Look at ME Like That」 | `00046.png`（Don't Look at ME）和 `00047.png`（LIKE THAT!），均来自 `p5camp_02.png`。独立斜底板未认出。 |
| 「MeNu&Main COMMAND SELECT」 | `00048.png`（MeNu&）、`00061.png`（MAIN）、`00479.png`（COMMAND SELECT）。「PHANTOM」未认出。 |
| 主菜单背后的页码大数字 | `00033.png` 至 `00042.png`，对应 0–9。 |
| SKILL、ITEM、EQUIP 等菜单剪贴字 | `00226.png` 至 `00233.png` 对应 SKILL、ITEM、EQUIP、PERSONA、REQUEST、STATUS、SYSTEM、COOPERATION。另有 `00445.png`（CALENDAR）与 `01004.png`（MY PALACE）。 |
| SKILL / ITEM 队伍列表 | `00245.png` 至 `00252.png` 为队伍头像，`00253.png` 和 `00517.png` 为 PARTY 与 LEADER 的独立标记框。另有 `00473.png`、`00486.png`、`00769.png` 的文字或合体标签。长锯齿行框未认出。 |
| ITEM 道具行 | `00141.png`（数量胶囊底），`00107.png`（×），`00096.png` 至 `00105.png`（小数字），`00600.png`（NEW），以及 `00754.png`（长胶囊状道具底）。参考帧中铺满行宽的红色选中条和白色长行未认出。 |
| EQUIP 标签 | `00071.png`（WEAPON）、`00072.png`（GUN）、`00073.png`（PROTECTOR）、`00074.png`（ACCESSORY）、`00075.png`（CLOTHES）。 |
| 右侧黑白星纹拼贴 | 未认出。已保留三份 CAMPBG 原始数据。`00992.png` 至 `00997.png` 是其他列表的彩色星纹小图，尚不能确认和主菜单背景共用。 |
| 切页的白色斜板与弯曲星纹带 | 未认出。两个 EVTWIPETEX 图已导出，尚未匹配到参考帧中的这两层。 |

## 其余关键词候选

在 `index.csv` 搜索 CAMPBG、STAR、COMMAND、WIPE 与 SPD、DDS、TMX、PAK，排除 CHAT、CARDTEX、CHARATEX。已确认需要导出的是 `INIT/EVTWIPETEX.PAK`。其余候选还未确认用于暂停菜单，本次没有展开。

- `BASE.CPK → FIELD/PANEL/WIPE/` 下的 `D_WAIP02.SPD`、`COMBINE/W_VELVET.SPD`、`CROWD/P5_WAIP_CROWD_02{,_S,_W}.SPD`、`CROWD/P5_WAIP_CROWD_SUBURB.SPD`、`CROWD/W_ALL_001.DDS`、`W_ALL_002.DDS`、`W_ANN.DDS`、`W_RYUUJI.DDS`、`FLIGHT/AIRPLANE_WIPE.SPD`、`PARALLEL/ISEKAI_EYE.DDS`、`PARRALLEL_TWIST/MARUKI_COLOR.DDS`、`RAIN/WIPE_RAIN01.DDS`、`SYMBOL/LOAD_LOGOWIPE{,2,3}.DDS` 与 `VELVET/WIPE_DOOR_TEX.DDS`。
- `SC.CPK → FIELD/PANEL/WIPE/SYMBOL/NOWLOADING{,2,3}.SPD` 为加载画面候选。
- `BASE.CPK → MINIGAME/FUGOU/STAR_BG.DDS`、`STAR_BG_LIGHT.DDS` 与 `SC.CPK → MINIGAME/FUGOU/GAMESTART.DDS` 属于小游戏目录。
