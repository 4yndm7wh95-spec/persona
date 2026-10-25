# P5R v2 原版资源整理

## 来源与体积

游戏目录为 `G:\Gamebox\P5R\ZS6425\P5R`，`P5R.exe` 版本 1.0.3.0。读取了 5 个 CPK，共 60,327 个原始目录项；选择性解出 2,737 项，约 2.71 GiB，并展开已选归档。`index.csv` 另追加 1,035 条套娃归档索引。完整本机资源库位于 `C:\Users\kkk\sts2_tools\p5r_rip`，当前约 5.37 GiB，低于 15 GiB 限额。

仓库 `research/p5r` 当前 355.1 MB（338.7 MiB），低于 380 MB 的整理预算；单文件均低于 20 MB。仓库收录全量 UI PNG 和切图坐标 JSON、基础模型与纹理、4 个 GLB 候选、少量原始动作 GAP、字体、全部已转码的选中音效/语音及索引。全量 3,064 条 Joker 动作 GLB 等大文件留在本机；动画 CSV 可查原名，`local_only.csv` 列出本机保留的大型资源及路径。

## 文件说明

- `index.csv`：CPK TOC 全量目录 60,327 项加 1,035 条已展开归档条目，共 61,362 行；`index_summary.md`：目录/扩展名汇总；`selection.csv`：选择解包清单；`audio_catalog.csv`：全部 ACB/AWB 容器（含 BGM，仅目录）；`movie_list.csv`：全部 USM；`local_only.csv`：留在本机的完整动作、原质量 UI PNG 等文件清单。
- `ui/`：4,249 张实际 PNG 和 76 份 `sprites.json` 坐标数据；各界面联系表同时放在根目录 `ui_*_contact_sheet.png` 和 `ui/<界面>/contact_sheet.png`。camp、battle、title、velvet、phone 五组共 1,964 张使用本机原质量 PNG；其余图采用 256 色索引调色板 PNG 编码，含颜色量化。本机 `C:\Users\kkk\sts2_tools\p5r_rip\ui` 对所有界面保留原质量 PNG。
- `model/joker/`、`model/arsene/`：角色 GLB 候选、可用贴图 PNG、角色原始 GMD 及少量代表动作 GAP；完整动作导出和原始 GAP 均保留在本机 `model/`、`raw/`，清单分别为 `joker_battle_anim_list.csv`、`joker_menu_anim_list.csv`、`arsene_anim_list.csv`。采样帧数按 30 fps 参考计算，原生 FPS 未知。
- `art/portrait/`：14 张原始立绘 PNG 与 `contact_sheet.png`；`font/`：4 个原始 FNT；`audio/sfx/`：1,291 个 OGG 和 catalog；`audio/voice/ja/`、`audio/voice/en/`：各 410 个 OGG 和 catalog。`model_preview_sheet.png` 展示导出候选现状；`anim_sheet.png` 是明确标注的静态模型缩略图，不代表动作帧。
- 本机资源库 `raw/` 放所选 CPK 原始文件，`unpacked/` 放套娃包导出，`model/`、`ui/`、`art/` 放转换结果，`audio/` 放 OGG，`metadata/` 放索引和工具日志。所有完整分辨率 PNG、完整动作、原始音频容器仍可从本机库读取。

## 工具与复用方式

- CriFsV2Lib 2.1.2（实际 CPK 选择提取器，集成于本机 P5RAssetTool）：[官方 release](https://github.com/Sewer56/CriFsV2Lib/releases/tag/2.1.2)。以 `P5RAssetTool.exe <cpk路径> <路径清单> <资源库输出目录>` 按包内路径单文件提取，输出路径限制在资源库且总量限制 15 GiB。密钥/配置仅留在本机。
- YACpkTool 1.1b（CPK 目录读取/索引）：[官方 release](https://github.com/Brolijah/YACpkTool/releases/tag/v1.1b)。在本环境选择解包失败，实际提取改用 CriFsV2Lib。
- GFD Studio 0.10.3 / GFDExport（GMD/GAP 到 GLB）：[官方 release](https://github.com/tge-was-taken/GFD-Studio/releases/tag/0.10.3)。
- PersonaEditor 1.6.7.0（SPD/TMX 等资源解析）：[官方 release](https://github.com/Meloman19/PersonaEditor/releases/tag/1.6.7)。
- vgmstream r2117（`-m -I` 探测；`-p -s N` 输出指定流 WAV 到管道）：[项目](https://github.com/vgmstream/vgmstream)。FFmpeg 9.0.2 essentials 将管道 WAV 以 libvorbis q4 转 OGG，并设置为源采样率；未裁切音频。Python/Pillow 用于 PNG 转换与联系表。
- 本机辅助脚本和工具在 `C:\Users\kkk\sts2_tools\p5r_tools`。PNG 原质量版和所有超出仓库预算的文件继续放在资源库。以后按 `index.csv` 查包和路径，将路径写入清单后使用 P5RAssetTool 单项提取即可。

## 分类结果

### a/b：Joker 与 Arsène 模型、动作

选取路径包括 `MODEL/CHARACTER/0001/`、`MODEL/CAMP/MENU_C0001_*`、`MODEL/CHARACTER/PERSONA/0000/` 和亚森事件模型引用。仓库已有 Joker 战斗服、学生服、菜单模型和 Arsène GLB 候选、对应角色 GMD、贴图 PNG，以及 9 个代表性的原始 GAP；完整输出为 Joker 动作 3,064 条（含战斗与事件动作）、菜单动作 43 条、Arsène 7 条，均留在本机，并列入动作 CSV。GLB 候选可打开，但预览画面中 Joker 身体明显扭曲，出现分离的几何块和暗色破面；Arsène 预览只见零散的深绿色部件，主体大部分不可见。故这些文件作为待后续检查的导出候选，不视为成品验收。当前没有可信的逐动作中间帧 sheet；现有 `model_preview_sheet.png` 仅展示角色预览。

### c/d/e：立绘、UI、字体

立绘来源含 `CAMP/HEROTEX/` 和 Joker 战斗切入纹理，仓库有 14 张原始 PNG 和联系表。UI 来源覆盖暂停菜单及子页、战斗/结算、标题、天鹅绒房间、手机、日历、共享素材与窗口字体图；仓库提供全量 4,249 张 PNG 和 76 份 sprite 坐标 JSON，其中 camp、battle、title、velvet、phone 共 1,964 张为原质量，其余为调色板量化版；本机保留全部原质量 PNG。字体从 `FONT/` 选取并放入 `font/`：FONT1、FONT2、KEYTOP_FONT、FONT0 原始 FNT；未从字库内部确认每个字体的具体屏幕用途。检查了 CAMP、BATTLE GUI/RESULT、CUTIN 和相关表格，未得到能独立读取的 UI 时间轴，动画数据是否写在程序中无法据此确认。

### f：音效与语音

`SOUND/SKILLSE`、`SOUND/SYSTEM`、`SOUND/MOTIONSE` 和日英 `SOUND/BATTLE/BP01` 为所选范围。日语与英语 BP01.AWB 各有 410 个流，全部以原 24 kHz 采样率转为 OGG。vgmstream 可直接读取 AWB；对应 BP01.ACB 无法打开，因此 cue 名留空，catalog 保留流序号、vgmstream 流名、时长与采样率。

选中 666 个 SFX 容器，成功转换 1,291 个流为 OGG；6 个容器的 vgmstream 元数据探测失败，catalog 保留失败项：0735.ACB、0117.ACB、0269.ACB、0304.ACB、0379.ACB 和 C0001_051_B.DAT。SFX catalog 的 cue 名留空、流名及序号保留。音频目录约 38.4 MB。全量 ACB/AWB（含 BGM）仅列于 `audio_catalog.csv`，未解 BGM。

### g/h：影片与文本

所有 USM 收录在 `movie_list.csv`。文件名是数字，无法据名判定总攻击、SHOWTIME 或召唤 Persona 片段，因此本轮没有转码影片。已按路径/关键词检查 BMD、MSG、FTD、TBL 文本候选，未完成可确认的 UTF-8/CSV 解码，未猜写文本内容。

## 体积和未纳入仓库的资源

本机资源库约 5.37 GiB；仓库 `research/p5r` 约 355.1 MB（338.7 MiB）。完整动作 GLB/原始 GAP、其余 2,285 张原质量 UI PNG 和全部原始 CPK 选取文件均保留在本机库。仓库中压缩参考组的调色板量化可能改变颜色；需要精确色彩时，以本机 `p5r_rip/ui` 为准。原始 FPS 未知，清单中的 30 fps 只是预览帧数估算。
