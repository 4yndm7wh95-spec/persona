# P3R 营地 COMMU 手持牌与动画补充

## 已定位的牌资源

找到与营地 COMMU 手持牌相符的道具类：`/Game/Xrd777/Blueprints/Props/BP_Pp2001_TarotCardBase`。其默认骨骼网格组件引用 `SK_Pp2001_000`，动画蓝图 `ABP_Pp2001_C` 继承自 `CmpCommuCardAnimInstance`；该模型关联的五段 UI 动画名为 `CommuStart`、`CommuLoop`、`CommuListStart`、`CommuListLoop`、`CommuListCancel`。模型和对应 PSA/GLB 均在本目录。

`BP_Pp2001_TarotCardBase` 的 `TarotCardSetup` 接收 `AppPropsCardParam`，查 `DT_AppPropsCard` 行并为正面创建动态材质；背面材质为 `MI_Pp2001_Backside_00`。因此牌面纹理是按参数从 DataTable 动态选择的，不是固定在网格上的单一正面贴图。本目录保留 DataTable 完整导出、背面贴图，以及 Major XVII（`T_Pp2001_030_0017`）作为正面纹理样本；目前无法仅凭这些静态资产证明该样本就是截图里那张简化星形牌。

挂接方面：导出的牌蓝图只证明其骨骼网格组件挂在自身默认 `Root` 上，没有记录与结城理手部骨骼的父子挂接、插槽或相对位置覆盖。角色骨架存在 `Jnt_R_AttachHand01_00` / `Jnt_L_AttachHand01_00`，但全量检索现有 Camp / Community 蓝图后未找到可确认的 `BP_Pp2001_TarotCardBase` 实例化节点或附着参数。故卡牌对象已定位，**外部场景的生成/挂手变换仍未确认**，不臆测坐标、旋转轴或转速。五段 PSA 是卡牌自身的关键帧动画；时长分别为 0.67、6.67、0.50、6.67、0.50 秒。未发现额外的蓝图时间轴或 Niagara 负责浮空自转的可核实数据。

## 导出文件

- `mesh/`：`SK_Pp2001_000.glb` 和 ActorX `SK_Pp2001_000.psk`。
- `animations/`：五段 `.psa` 及相应 `UAnimSequence` 完整属性 JSON。
- `blueprints/`：`BP_Pp2001_TarotCardBase`、`ABP_Pp2001` 与 `DT_AppPropsCard` 的完整属性 JSON。
- `materials/`：正面动态材质实例、背面材质实例属性 JSON。
- `textures/`：背面贴图、Major XVII 星牌正面样本 PNG；不代表已确认截图中的具体牌面。

## 角色道具隐藏线索

26 个 PC0051 Camp AnimSequence 的缩放数据按 float32 `[frame][bone][sx,sy,sz]` 输出在 `../hero3d/scale/`，和同名 PSA 骨骼顺序一致；每段均有 `.bin`、`.json`，另附 `summary.csv`。实际 PSA 的 BONENAMES 为 **2,779 根**（任务原说明中的 309 与本机数据不符），导出按 PSA 实际列表保持顺序。缩放逐帧与同名 PSA 对照，抽查 `CampSkillStart` 的 `Jnt_C_Neck_00` 第 0、47、95 帧位置/旋转，最大误差低于 `0.000001`；全部缩放值对比误差低于 `0.001`。

`summary.csv` 共记录 436 个动画/骨骼偏离项。可见多段动作把挂件骨骼缩至约 `0.001`：例如 `CampSkillStart/Loop` 中 `Jnt_R_AttachHand01_00` 与 `Jnt_C_AttachRoot_02..07` 全段约为 `0.001`；`CampItemStart/Loop` 中 `Jnt_L_AttachHand01_00` 全段约为 `0.001`。这支持“通过动画缩小隐藏挂件”的判断。召唤器及材质 04/05/06 对应挂点的隐藏方式应结合具体动作和材质槽看：这些缩放轨能证明骨骼缩放，但不能单独证明每个具体网格都挂在哪根骨骼；不要把材质替换、组件隐藏当成已确认结论。

## COMMU UIM

`../anim/` 新增 60 个 `WaterLight_a_00..29` 与 `WaterLight_b_00..29` UIM 原始 JSON，并附 `commu_uim_manifest.csv`。每个编号资产的 `frameNum=1`；它们是按编号拆分的单帧 UIM 资源。原始资产未提供 FPS 或总时长，故标记为未提供。原先已有的根菜单 `UIM_UI_Camp_In` 未重复导出。

## 工具与限制

使用本机 CUE4Parse 1.2.2.202610 / Unreal 4.27 解析、导出 GLB、PSK、PSA、JSON 与 PNG。AES 只使用本机配置：沿用 README_v14 的 FModel 配置。中间文件留在仓库外的 `C:\Users\kkk\sts2_tools\p3r_camp_20261019\`。没有提交任何单文件超过 20 MiB 的文件。
