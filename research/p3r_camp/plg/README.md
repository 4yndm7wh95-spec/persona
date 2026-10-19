# 营地菜单 PLG 多边形

本目录收录默认资源组的营地根菜单与 Theurgia 子菜单 PLG 原始 JSON，并附几何预览 PNG。原始源文件路径保留在 `json/` 下。

|源资源|源 pak 大小|JSON 文件|
|---|---:|---|
|`P3R/Content/Astrea/UI/Camp/Root/Plg/PLG_UI_Camp.uasset`|1,828 bytes|`json/Astrea/UI/Camp/Root/Plg/PLG_UI_Camp.json`|
|`P3R/Content/Xrd777/UI/Camp/Root/Plg/PLG_UI_Camp.uasset`|1,874,561 bytes|`json/Xrd777/UI/Camp/Root/Plg/PLG_UI_Camp.json`|
|`P3R/Content/Xrd777/UI/Camp/Root/Plg/PLG_UI_Camp_Theurgia.uasset`|173,298 bytes|`json/Xrd777/UI/Camp/Root/Plg/PLG_UI_Camp_Theurgia.json`|

## PNG 预览

`png/` 中的 PNG 将 `PlgDatas` 的三角形按顶点颜色栅格化，并把每个几何项单独归一化放进编号网格，便于查看形状；编号与原始 `Name`、顶点数和索引数的对应关系见同名前缀的 `*_manifest.csv`。这些图片是几何检查图，不包含材质、贴图或游戏 UI 合成效果；原始 JSON 中的顶点、颜色和三角索引是精确依据。各图页：Astrea 根菜单 1 项；Xrd777 根菜单 172 项（4 页）；Theurgia 19 项（1 页）。共 6 张 PNG。

## 未纳入的本地化覆盖

索引中另有 18 个 `L10N` 版本。它们是本地化覆盖，未重复收录以控制三类补充资源总量；源 pak 大小如下，已纳入总量说明：

|语言|资源|源 pak 大小（bytes）|
|---|---|---:|
|`de`|`PLG_UI_Camp.uasset`|1722551|
|`de`|`PLG_UI_Camp_Theurgia.uasset`|165160|
|`en`|`PLG_UI_Camp.uasset`|1837605|
|`en`|`PLG_UI_Camp_Theurgia.uasset`|149268|
|`es`|`PLG_UI_Camp.uasset`|1890445|
|`es`|`PLG_UI_Camp_Theurgia.uasset`|148564|
|`fr`|`PLG_UI_Camp.uasset`|1795687|
|`fr`|`PLG_UI_Camp_Theurgia.uasset`|143626|
|`it`|`PLG_UI_Camp.uasset`|1754801|
|`it`|`PLG_UI_Camp_Theurgia.uasset`|135872|
|`pl`|`PLG_UI_Camp.uasset`|1814435|
|`pl`|`PLG_UI_Camp_Theurgia.uasset`|212168|
|`pt`|`PLG_UI_Camp.uasset`|1985717|
|`pt`|`PLG_UI_Camp_Theurgia.uasset`|158792|
|`ru`|`PLG_UI_Camp.uasset`|1837605|
|`ru`|`PLG_UI_Camp_Theurgia.uasset`|149814|
|`tr`|`PLG_UI_Camp.uasset`|1712853|
|`tr`|`PLG_UI_Camp_Theurgia.uasset`|95574|

18 个本地化 PLG 源文件合计 17,710,537 bytes。三类补充（DataTable JSON、根菜单 WaterLight UIM、PLG JSON/PNG 及本说明）的仓库文件总大小约 38.7 MB，低于 60 MB 目标；单文件均低于 20 MB。

PNG 为渲染预览而非游戏原始贴图，不含代码或其他游戏文件。
