# P3R 营地场景相机与角色摆放资料

以下为从游戏资源导出的原始属性 JSON，供还原营地菜单中的角色取景与摆位使用。文件只包含文本导出，不包含游戏原文件或二进制资源。

## 资料与来源

- `BP_Camp_Scene_Capture.json`：`/Game/Xrd777/UI/Camp/BP_Camp_Scene_Capture`。包含 `SceneCaptureComponent2D` 模板属性。导出字段可见 `FOVAngle=37.35`、`OrthoWidth=1920`、`CaptureSource=SCS_FinalToneCurveHDR`、RenderTarget 引用 `RT_UI_CampSceneCapture2D`。
- `BP_CampCharaModel_Xrd777.json`、`BP_CampCharaModel_Astrea.json`：分别来自 `/Game/Xrd777/Blueprints/Characters/Player/BP_CampCharaModel` 和 `/Game/Astrea/Blueprints/Characters/Player/BP_CampCharaModel`。保留两个游戏内容变体的完整 Blueprint 导出，含 SimpleConstructionScript 与骨骼网格组件模板。
- `DT_CampCameraTable_Xrd777.json`、`DT_CampCameraTable_Astrea.json`：分别来自两套内容中的 `/UI/Camp/Param/DT_CampCameraTable`。按 `Top`、`Skill`、`Item`、`Equip`、`Persona`、`Status`、`Quest`、`Commu`、`Calender`、`System` 列出每个菜单页的 CameraPos/Rot 和 CharaPos/Rot。这是找到的页级相机/角色摆位数据；它不等同于关卡 ActorTransform。
- `DT_CampCharaLookAdjust_Astrea.json`：`/Game/Astrea/UI/Camp/Param/DT_CampCharaLookAdjust`，补充角色取景用的灯光位置、颜色渐变等调整参数。
- `RT_UI_CampSceneCapture2D.json`：`/Game/Xrd777/UI/Camp/Textures/RT_UI_CampSceneCapture2D`。分辨率为 1920×1080，格式 `RTF_RGBA8`，清屏颜色透明黑。
- `WBP_CampCustomStensilRendring.json`：`/Game/Xrd777/UI/Camp/WBP_CampCustomStensilRendring`。其中 `Image_53` 的画刷大小为 1920×1080，资源对象指向 `MM_UI_CampCustomStencil`；Canvas slot 导出 Right/Bottom 偏移约 1921.92/1081.08。
- `MM_UI_CampCustomStencil.json`：`/Game/Xrd777/UI/Camp/Material/MM_UI_CampCustomStencil` 的材质属性 JSON。
- `BP_VelvetRoomDraw_RT_consumer_reference.json`：`/Game/Astrea/Blueprints/UI/Facility/Combine/BP_VelvetRoomDraw` 的完整导出，仅作为找到的另一个引用 `RT_UI_CampSceneCapture2D` 的 UI Blueprint 交叉参考。它位于 Velvet Room 路径，不据此断言它属于营地菜单。

## 尚未从资源中确认的部分

- 已遍历当前挂载内容中全部 3,232 个 `.umap` 包并检查导出文本；没有找到 `BP_Camp_Scene_Capture`、`BP_CampCharaModel` 或 `RT_UI_CampSceneCapture2D` 的关卡引用。因此没有可可靠导出的这两个 Actor 的关卡实例 ActorTransform，也没有找到可确认属于营地的额外 CameraActor / SceneCapture 实例。它们可能由蓝图运行时生成，或通过非直接字符串引用。
- SceneCapture JSON 没有序列化 `ProjectionType`，组件模板 JSON 也没有给出 `RelativeLocation`、`RelativeRotation`、`RelativeScale3D`。这里保留原始 JSON，不推测缺失值。
- `WBP_CampCustomStensilRendring` 确认了全屏 Image 与 Camp 材质，但静态材质 JSON 未显示到该 RenderTarget 的直接引用；具体运行时绑定链仍未确认。单独找到的 `BP_VelvetRoomDraw` 引用已作为线索附上，不能替代营地根菜单界面的确证。

## 搜索范围

检查了已挂载资源中的全部 `.umap`，并针对 `/UI/Camp/`、`/Blueprints/UI/` 资源及关键词 `BP_Camp_Scene_Capture`、`BP_CampCharaModel`、`RT_UI_CampSceneCapture2D` 做了引用扫描。此目录中的 JSON 均为原始属性导出；文件名中的 Astrea/Xrd777 用于区分游戏内容变体。
