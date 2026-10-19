# P3R 营地菜单资源提取
## 后续补充：大体积菜单动画分块

本节更新之前关于 PSA 超限动画的初次整理记录：oversize_animations.csv 中的 13 个原始 PSA 现已按原始字节流分块收录在 hero3d/animations_split/。每块为 19 MiB（最后一块为余数），原始文件未改写；12 个 27,145,600 字节的 PSA 各分为 2 块，56,491,840 字节的 SYSTEMTOP 分为 3 块，共 27 块。sha256.txt 为每个原文件记录 SHA-256 和分块数；本机已逐块串接计算并与源文件校验一致。

本次分块追加 382,239,040 字节；营地 DataTable 补充 412 份，根菜单 WaterLight 再补 60 个逐帧 UIM JSON。所有分块均小于 20 MB；未纳入的通用水光与 PLG 多边形仍待筛选。

## 结果与范围

从本机 P3R pak 只读提取了营地菜单根界面和子菜单素材，以及用户补充要求的结城理实时 3D 角色资料。没有启动游戏，也没有修改游戏文件或已有仓库资源。

本机完整候选集共 2,440 条，完整临时导出为 484,228,341 字节。仓库纳入 a（根菜单/主角营地动作）、b（各子菜单 UI）、`datatables/` 中全部 412 个营地 DataTable JSON，以及 `waterfx/` 中根菜单直接引用的 60 个 WaterLight UIM 帧；其余通用 c 类效果与 d 类 PLG 多边形仍只保留在 `index.csv` 清单中，`repo_exported=no` 表示尚未纳入。仓库提交目录中没有单个文件超过 20 MB。

|目录|内容|
|---|---|
|`index.csv`|完整 2,440 条命中资源索引；含 pak 路径、类型、贴图尺寸、源文件大小、分类和是否进入本目录。|
|`tex/`|40 张 PNG 原始贴图与 7 张分目录浏览图；原尺寸与透明通道保留。|
|`json/`|96 份根菜单/子菜单、材质及主角菜单动作的原始属性 JSON。|
|`datatables/`|全部 412 个 `UI/Camp/` DataTable JSON（含语言目录），约 0.99 MB。|
|`waterfx/`|营地根菜单 WaterLight 的 60 个单帧 UIM JSON；Caustics PNG 与 Wave 材质在 `tex/`、`json/`。|
|`anim/`|6 份可展开查看的 UIM 网格逐帧数据。|
|`tree/`|场景捕获、主角骨骼组件关系和可见 Widget Blueprint 调查结果。|
|`hero3d/`|GLB 与 PSK 骨骼网格、受单文件大小限制筛出的 PSA 动画、材质参数 JSON、PNG 贴图。|

## 工具与来源

- 只读 pak：`G:/Gamebox/ZS15858/GAME15858/GAME15858/P3R/Content/Paks/`。
- 使用之前成功读取 P3R 时的本机 FModel 配置（对应 `art/p3r_tex/README_v14.md`）；密钥没有写进文件、日志或仓库。
- CUE4Parse / CUE4Parse-Conversion `1.2.2.202610`，UE4.27；纹理解码使用本机 Detex。
- 成功挂载 140,007 个资源，另有 1 个归档未挂载；结论仅覆盖成功挂载部分。
- 按任务要求检查 `UI/Camp`、`Blueprints/UI`、`Blueprints/Camp`、`UI/Common` 及关键词 Camp、Pause、MainMenu、System、Skill、Item、Equip、Persona、Status、Quest、Commu、Calendar、Hero、Water、Dive、Bubble、Splash 等。完整命中列表见 `index.csv`。

## 根菜单与子菜单映射

这些是从资源路径和主角 `PC0051/AnmUI` 的动画资源名对应出的菜单项。角色动作的完整属性 JSON 在 `json/Xrd777/Characters/Player/PC0051/AnmUI/`；小于 20 MB 的 ActorX 序列在 `hero3d/animations/`。

|界面|找到的角色菜单动作 / UI 动画|备注|
|---|---|---|
|根菜单 / 入场|`CampTopStart`、`CampTopLoop`；`UIM_UI_Camp_In`、`UIM_UI_Camp_Loop_L`、光标入场/选中|UIM 入场 75 帧、循环 210 帧；原数据无 FPS。|
|SKILL|`CampSkillStart`、`CampSkillLoop`；`UIM_UI_Camp_Skill_Gun_Loop`|根菜单 WaterLight a/b 的 60 个逐帧 UIM 已放入 `waterfx/uim/`；其他子页及通用效果仍未纳入。|
|ITEM|`CampItemStart`、`CampItemLoop`|动作 JSON 保留；超限 PSA 循环不提交。|
|EQUIP|`Campequip`、`CampequipLoop`、`CampequipUni01/02`|角色动作 JSON 保留；超限 PSA 循环不提交。|
|STATUS|`CampSTATUSStart`、`CampSTATUSLoop`|同上。|
|QUEST|`CampQUEST`、`CampQUESTLoop`|同上。|
|COMMU / 社群|`CommuStart`、`CommuLoop`；`CommuListStart`、`CommuListLoop`、`CommuListCancel`|同上。|
|SYSTEM / CONFIG|`SYSTEMTOP`、`SYSTEMTOPLOOP`、`SYSTEMCONFIG`、`SYSTEMCONFIGLOOP`、`SYSTEMCONFIGBGM`、`SYSTEMCONFIGBGMLOOP/Cancel`|配置与 BGM 子页有独立命名动作。|
|PERSONA、CALENDAR|在已挂载并筛出的 `PC0051/AnmUI` 中没有发现对应名字的独立角色动作|相应目录下可读的 UI 贴图/属性仍按 a/b 范围收录；未用其他动作冒充。|

当前索引中只识别到 1 个 `WidgetBlueprint`：`WBP_CampCustomStensilRendring`。没有找到可读的营地根菜单及各子菜单 WidgetTree，也没有识别出常规 UMG `WidgetAnimation` / `LevelSequence` 对象；因此 `tree/` 只记录可验证的场景和骨骼组件关系，并明确标出了缺失项，没有杜撰锚点、位置或控件层级。部分 Camp 界面实际数据以 UIM 网格动画与角色 `UAnimSequence` 提供。

## UIM 逐帧数据与时间单位

`anim/` 保存了 6 个能从原始 `UimData` 解析出的网格动画：

|资源|帧数|顶点数|
|---|---:|---:|
|`UIM_UI_Camp_In`|75|170|
|`UIM_UI_Camp_Loop_L`|210|170|
|`UIM_UI_Camp_Skill_Gun_Loop`|201|4|
|`UIM_UI_Camp_Cursor_On`|24|4|
|`UIM_UI_Camp_Cursor_In_Bounce`|8|4|
|`UIM_UI_Camp_Txt_Cursor_On`|4|4|

每一帧保存顶点 x/y 偏移，帧号从 0 开始；原始 UIM 未提供 FPS 或秒数，也没有给出帧间插值曲线，所以 `fps`、`length_seconds`、`time_seconds` 明确为 `null`，不做猜测。对应完整属性位于 `json/`。

## 主角是实时 3D 模型

已确认营地角色不是一张静态立绘：`BP_Camp_Scene_Capture` 使用 `SceneCaptureComponent2D` 和 `RT_UI_CampSceneCapture2D`；`BP_CampCharaModel` 由骨骼网格组件构成，`CharacterMesh0` 使用 `SK_PC0051_BaseSkelton` 与 `ABP_PC0051_C`，服装、脸和头发分别引用 `SK_PC0051_C002`、`SK_PC0051_F000`、`SK_PC0051_H000`。验证记录见 `tree/`，蓝图原始属性 JSON 在 `json/`。

`hero3d/model/` 为每个角色骨骼网格同时保留 GLB 与匹配的 PSK，共 4 组；逐一检查 GLB 后均包含 1 个 skin。材质参数 JSON 共 14 份，解出的 PNG 贴图共 5 张（服装、脸部、眼部/通用颜色贴图）。当前依赖导出没有解析出独立的阴影色或描边贴图；材质参数和引用保留在 JSON，未把缺失贴图当成已导出。

动画共识别到 26 个菜单命名 `UAnimSequence`。CUE4Parse 的 GLB 导出器不支持将 `UAnimSequence` 写成 GLB 动画；为保留骨骼轨道，动画以 ActorX PSA 输出，并和同一导出器的 PSK 骨骼网格配对，GLB 则另供查看蒙皮网格。13 段小于 20 MB 的 PSA 已收录；超过限制的 13 个在 `hero3d/oversize_animations.csv` 中逐项列出并排除：

- `A_PC0051_UIA0001_BASE_CampTopStart.psa` 13,806,400 字节（此项小于限制，已收录；清单仅列超限项）
- 超限文件：`A_PC0051_UIA0002_BASE_CampTopLoop.psa`、`A_PC0051_UIA0004_BASE_CampSkillLoop.psa`、`A_PC0051_UIA0006_BASE_CampItemLoop.psa`、`A_PC0051_UIA0008_BASE_CampequipLoop.psa`、`A_PC0051_UIA0008_BASE_CampequipUni01.psa`、`A_PC0051_UIA0008_BASE_CampequipUni02.psa`、`A_PC0051_UIA0010_BASE_CampSTATUSLoop.psa`、`A_PC0051_UIA0012_BASE_CampQUESTLoop.psa`、`A_PC0051_UIA0014_BASE_CommuLoop.psa`、`A_PC0051_UIA0016_BASE_CommuListLoop.psa`、`A_PC0051_UIA0019_BASE_SYSTEMTOP.psa`、`A_PC0051_UIA0021_BASE_SYSTEMCONFIGLOOP.psa`、`A_PC0051_UIA0023_BASE_SYSTEMCONFIGBGMLOOP.psa`。

超限项均为 27,145,600 字节，`SYSTEMTOP` 为 56,491,840 字节。它们的原始属性 JSON 仍在 `json/` 中。

## 未纳入仓库的类别与限制

- `datatables/` 已补齐 412 个营地 DataTable JSON；`waterfx/` 已补充 60 个根菜单 WaterLight UIM 帧。`index.csv` 中剩余的通用 c 类效果与 21 条 d 类 PLG 候选尚未纳入，后续按用户要求继续筛选并控制体积。
- PLG 三角网格的原始属性资源仍在命中清单中；尚未提交 PLG JSON/抗锯齿 PNG。没有把它们伪装成已渲染资产。
- 1 个 pak 归档未挂载，可能含有本次无法检索的资源。
- 没有导出音效；本次没有确认到可可靠归属营地菜单的独立音效名。

## 校验

- 每个输出文件均小于 20 MB；目前没有超限文件进入 `research/p3r_camp/`。
- GLB 均解析出 1 个 skin；PSK/PSA 以 ActorX 配套方式输出。
- PNG contact sheet 按原资源子目录生成，图片本身保持原尺寸。
- AES 配置只在本机导出程序中读取，未写入任何仓库文件。

