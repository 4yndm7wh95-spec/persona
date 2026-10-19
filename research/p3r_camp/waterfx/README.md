# 营地根菜单 WaterLight / Caustics

本次只补营地根菜单路径下直接命名的 `UIM_UI_Camp_Top_WaterLight_a_00..29` 与 `..._b_00..29`：共 60 个逐帧 UIM 属性 JSON，每个源资产为 1 帧。每帧完整保留 `p2DGeomVertUV`、`p2DAnimVertex`、`Indices` 等几何信息；拓扑索引数量随帧变化，因此没有把它们简化成只有顶点位移的假序列。`frame_manifest.json` 汇总每帧顶点/多边形/索引数。原资源未提供 FPS，保持未提供。

与根菜单波光直接相关的 Caustics 图和材质已在原始营地资源目录中：

- 贴图：`../tex/Xrd777/UI/Camp/Root/Texture/T_UI_Camp_Caustics_00.png`
- 材质参数：`../json/Xrd777/UI/Camp/Root/Material/MM_UI_Camp_Wave.json`（直接引用上述 Caustics 贴图）

当前根菜单清单中没有独立命名为 Bubble 的资源；没有把通用特效库或其它子菜单水光效果混入本目录。全部 60 个 JSON 均通过解析和单帧几何数组长度检查，单文件小于 20 MB。
