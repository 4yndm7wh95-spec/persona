# P3R 营地 ITEM / SKILL / QUEST 原版 UI 数据

本次只读导出本机 P3R pak；未启动 P3R / 杀戮尖塔 2，未改代码、已有资源或游戏文件。不是界面复刻实现，也不是从视频反推或 AI 生成的图片。

## 范围与查找依据

已挂载文件数 140,007；有 1 个归档未挂载，故结论仅适用于已挂载内容。对完整已挂载路径执行大小写不敏感搜索，而非只依赖旧 index.csv 每目录的 60 行。覆盖 Xrd777、Astrea，L10N 限 en / zh-Hans / zh-Hant；包含 Item / Skill / Quest / Root 尚未导出项、Camp/Textures、PartyPanel、Camp 及通用 KeyHelp、营地 BMD、Camp 相关名称的材质 / 曲线 / DataTable / BP / WBP。命中旁支 Persona 的水光也保留为公用参考，不能据此认定三页调用它。

607 个候选包全部读取成功；完整包路径、类型和导出状态见 asset_inventory.json、manifest.csv、export_errors.json。旧 Root 资源及已有 Gun 资源按 index.csv 的 repo_exported 复用，109 条见 reused_assets.json；本次没有改旧索引。路径不存在的 Astrea 页面覆盖不伪造副本。

## 输出格式

- uim/：原始完整属性 JSON，以及 .normalized.json。原始几何、UV、顶点位移、顶点颜色和格式标志完整保留；标准化文件只按原始帧索引分组，不擅自赋予秒数、插值或分解出来的旋转 / 缩放。
- tex/：原尺寸 RGBA PNG 与同名原始属性 JSON，保留原有透明；属性包含源资产实际序列化的 SRGB、压缩、寻址等字段，未序列化的默认值不能视为已知。
- plg/：完整 PLG JSON 与每个 mesh 的索引三角形 / 顶点 RGBA 几何检查 PNG。PNG 保留各 mesh 原坐标范围，边缘留 2 像素，2 倍栅格化后缩回；6 个超大坐标范围的 mesh 等比缩至最长边 2048 像素，便于检查，原始坐标仍在 JSON；不是完整游戏材质合成。
- widget/：BP、WBP、DataTable、曲线、材质、SPR 图集等原始完整可解析属性。SPR 仍保留原始 UV、尺寸和索引，不将其猜测为某个控件。
- uv_and_asset_references.json：每包引用与 UV 所在原始文件的对应；原始 SPR / UIM / PLG 是 UV 原值依据，引用不等于已证实运行时调用。
- text/：按源路径区分默认日文、en、zh-Hans、zh-Hant，含 CampSystem 与 Astrea CampSystemExt 的消息 ID → 文本。每个基础 CampSystem 有 70 条、每个 Ext 有 13 条；UTF-8，保留 [f 表:函数 参数] 控制标签、分页和 raw_text_hex。原始 BMD 属性也保留在 text/raw/。
- hero3d/animations_astrea、hero3d/scale_astrea（同级目录）：新增 PC0062 人物 PSA、逐帧缩放和原始解压缩放键。2779 骨骼，与 PSA 顺序一致；bin 为小端 float32 [frame][bone][xyz]，json 包含 bones、frames、sequence_length；.keys.json 另外保留 KeyScale / KeyScaleTime / KeyTime 原值。原有 PC0051 PSA / 缩放不重复导出；人物 AnimSequence 属性见 widget/ 下对应源路径。

## ITEM

- 页面资源：Xrd777/UI/Camp/Item/Uim 下 WaterLight_a_00–29、b_00–29，共 60 个 UIM；共享根菜单图集 / PLG、HeroGaussMaskItem、PartyPanel、KeyHelp、DT_UILayout_CampItem 与 CampItemTextCol，以及 DT_CampParamItem。Astrea 覆盖按原始独立路径保存。
- 人物进入：CampItemStart，101 帧，SequenceLength = 3.3333333 秒；待机 CampItemLoop，201 帧，6.6666665 秒。PC0051 与 PC0062 相同。采样区间率 (NumFrames−1)/SequenceLength 约 30，注意这与某些导出工具 NumFrames/SequenceLength 的 FramesPerSecond 字段不是同一口径。
- UI 进入参数（Xrd777 / NewRow）：Silhouette Frame=20、Wait=10、CurveUp2，位置 (0,0)→(-90,-110)；SiroIta Frame=10、Wait=0、CurveUp1；Title Frame=8、Wait=0、CurveUp2；List Frame=10、Wait=2、CurveUp2；Tab Frame=8、Wait=0、CurveUp2。InDist=-100。原表未声明这些 Frame / Wait 的时基，不能直接换成秒。
- 水光：a/b 各 30 个单帧资产，每个 frameNum=1；帧索引 00–29、fps / 秒时长未提供。不能把单个 UIM 的 frameNum=1 当成整个水光只有一帧。
- 光标移动、LB/RB 切换、退出的具体调用段、时长与 UI / 人物谁先谁后：没找到可完整确认的调用流程。上述 Wait 是原始 UI 参数，不足以证明人物延时。

## SKILL

- 页面资源：60 个 WaterLight_a/b 单帧 UIM；原有 3 个 Gun UIM 复用；HeroGaussMaskSkill、PartyPanel、共享图集 / PLG / KeyHelp、DT_UILayout_CampSkill / CampSkill2、DT_CampParamSkill。
- 人物进入 CampSkillStart：96 帧，3.1666667 秒；待机 CampSkillLoop：201 帧，6.6666665 秒。PC0051 / PC0062 相同；区间采样率约 30。
- UI 进入参数（Xrd777 / NewRow）：Silhouette Frame=10、Wait=23、CurveUp1，(0,0)→(10,170)；SiroIta / Title / List 各 Frame=10、Wait=0、CurveUp1；Party Frame=6、Wait=0、CurveUp3，FadeFrame=1、Interval=1。GunRef RotAnimType=EaseInOut，InFrame=8、InWait=8、NextFrame=5，Pos=(120,15)、Offset=150；InDist=-100。时基未提供，NextFrame 不可直接认定为 LB/RB 切页动画时长。
- 水光 a/b 各 30 个单帧 UIM，frameNum=1；fps / 秒时长未提供。
- 光标、LB/RB、退出和 UI / 人物先后：没找到完整原生调用流程，不能用视频或 30fps 假设补齐。

## QUEST

- 页面资源：60 个 WaterLight_a/b 单帧 UIM；HeroGaussMaskQuest；DT_UILayout_CampQuest / QuestDate / QuestTextCol / QuestTextPos、MI_UI_Camp_Quest_BG、MM_UI_Camp_Quest_BG、CA_UI_Camp_Quest、CRV_Camp_Quest_BGCurve，以及共享文字、图集和 KeyHelp。
- 人物进入 CampQUEST：21 帧，0.6666667 秒；待机 CampQUESTLoop：201 帧，6.6666665 秒。PC0051 / PC0062 相同；区间采样率约 30。
- 背景曲线原始 Time / Value / InterpMode / Tangent 均保存。例：CRV_Camp_Quest_BGCurve 的首个 FloatCurves 键 Time=[0,0.1,0.3,0.9,1]，InterpMode=RCIM_Linear。它是材质渐变曲线坐标，不能直接声称切页持续 1 秒；全部 15 个曲线资产、273 个原始键见 curve_key_inventory.json。
- 水光 a/b 各 30 个单帧 UIM；frameNum=1，fps / 秒时长未提供。
- 光标、LB/RB、退出和 UI / 人物先后：没找到完整调用时序；没有发现营地专用 QuestCancel。

## 公用、驱动逻辑与缺口

- 共享数据包含 Root 尚未导出项、Camp/Textures（包括 20 个 frameNum=24 的 UIM）、PartyPanel、KeyHelp、SPR 图集、原尺寸贴图、布局及参数 DataTable、材质与曲线。各项以清单为准，不能把公用曲线或 UIM 自动指定为进入 / 退出动画。
- BP_Camp_Scene_Capture_C 的父类为 /Script/xrd777.CampSceneCapture；BP_CampCharaModel_C 的父类为 Engine.Character；WBP_CampCustomStensilRendring_C 的父类为 UMG.UserWidget。原始默认值、函数属性、组件和 WidgetTree 均保存。
- 原生可执行文件只读字符串补查命中 ACampSceneCapture、UCampModelControllerItem、UCampPlayerModelControllerItem、CampModelControllerMotionItem，以及水光 a/b 的 %02d 加载路径；见 native_class_candidates.json。字符串只证明名称存在，不证明各段调用顺序。
- 在所有已挂载路径及命中 BP / WBP 属性中，没有取得三页进入 / 光标 / LB-RB / 退出的 UWidgetAnimation MovieScene 关键帧轨。CUE4Parse 的 UFunction JSON 包含函数名、参数和标志，不含完整执行字节码；本机原生类执行逻辑也不是 pak 中的属性资产。因此仍缺 UI 与人物的准确起始事件、延时及原生插值实现；没有将缺失数据写成猜测的时间轴。
- 全部挂载路径中营地三页人物 AnmUI 仅匹配 PC0051 / PC0062 各 6 段；没有 ItemCancel / SkillCancel / QuestCancel 营地动作。名称相近的 AnmBattle/ItemCancel 属战斗动作，未混入页面导出。
- 贴图图集的 UV 原值及引用已保留；未获得原生 UI 绘制调用中的完整切片选择关系。
- 超过 20 MiB 的单个文件不提交，完整路径和字节数见 oversized_files.json；这些文件仍留在本机中间目录。未挂载的 1 个归档未尝试破解或修改。

65 个 PLG mesh 的源顶点或索引为空，无法画出三角形；清单见 plg_preview_status.json。该清单另含 6 个已补充等比预览的超大范围 mesh，不能视为未导出。



|未提交 PSA|字节|
|---|---:|
|`A_PC0062_UIA0004_BASE_CampSkillLoop.psa`|27145600|
|`A_PC0062_UIA0006_BASE_CampItemLoop.psa`|27145600|
|`A_PC0062_UIA0012_BASE_CampQUESTLoop.psa`|27145600|

三个文件均为 Astrea 的循环 PSA（源路径 Astrea/Characters/Player/PC0062/AnmUI/）；对应 AnimSequence JSON、缩放轨和压缩解码键已收录。

## 工具与复核

沿用 README_v14 的 FModel 配置。

CUE4Parse / CUE4Parse-Conversion 1.2.2.202610，.NET SDK 10，Newtonsoft.Json 13.0.4，ImageSharp 3.1.12（仅本机原版贴图解码）；Python / Pillow 12.3 / NumPy 2.3.5（索引三角形栅格化与清单检查）。BMD 采用 MSG1 big-endian / Reload UTF-8 读取，格式依据 Atlus-Script-Tools 的 V1/IO/MessageScriptBinaryReader.cs 与 V1/MessageScriptBinaryTokenParser.cs，控制参数和原始字节均保留，非正则提取散落字符串。

格式参考：https://github.com/tge-was-taken/Atlus-Script-Tools/tree/master/Source/AtlusScriptLibrary/MessageScriptLanguage/BinaryModel/V1

中间文件：C:\Users\kkk\sts2_tools\p3r_pages_20261020\；单文件上限、清单路径和大小、JSON / PNG 解码、缩放轨字节数在提交前复核。只新增 pages/ 与 hero3d/ 文件。
