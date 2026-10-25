# P5R 第二轮模型诊断与取证

日期：2026-10-25。任务：`docs/codex/codex_p5r_model_fix2.md`。检查基线：`2f2c6bcc`，分支 `claude/amazing-darwin-b00arn`。

本轮只取证，不修改 mod、原始模型、第一轮修复候选或 `tools/p5r/fix_gfd_glb.py`。菜单模型未处理。结论与修法交给 Claude；以下对画面只描述现象。

## 输出导航

| 工作 | 文件 | 完成情况 |
|---|---|---|
| 无面具怪盗服逐网格 | [mesh_sheet_thief.png](mesh_sheet_thief.png)、[mesh_table_thief.csv](mesh_table_thief.csv) | 22 个有网格的 GLB 节点，各单独渲一张 |
| 学生服逐网格 | [mesh_sheet_student.png](mesh_sheet_student.png)、[mesh_table_student.csv](mesh_table_student.csv) | 21 个节点，各单独渲一张 |
| 原版 GMD 材质 | [gmd_materials_thief.csv](gmd_materials_thief.csv)、[gmd_materials_student.csv](gmd_materials_student.csv) | 20 / 16 个材质，包含原始数值及解析字段 |
| 原版 GMD 网格节点 | [gmd_nodes_thief.csv](gmd_nodes_thief.csv)、[gmd_nodes_student.csv](gmd_nodes_student.csv) | 24 / 21 个原始网格；怪盗服原始 GMD 包括面具，诊断 GLB 已去掉两个面具网格 |
| 首段动作对比 | [anim_test.png](anim_test.png)、[animation_evidence.json](animation_evidence.json)、[root_evaluation.json](root_evaluation.json) | 两种版本各 6 帧；未批量渲 64 段 |
| Persona 候选搜索 | [arsene_candidates.png](arsene_candidates.png)、[arsene_candidates.csv](arsene_candidates.csv) | PS0001 加后续 10 个编号（0002–0011），未找到亚森 |
| PS0001 单独检查 | [arsene_ps0001_front.png](arsene_ps0001_front.png)、[arsene_ps0001_threequarter.png](arsene_ps0001_threequarter.png) | 文件名沿用任务指定名称；内容不是亚森，请勿当作亚森成品 |
| 可复核的诊断脚本 | [scripts/](scripts/) | 新写的诊断脚本副本；本机执行版位于工具目录 |

## 1. 逐网格渲染

输入是上一轮保留在本机、未提交的 `joker/fixed/joker_thief_nomask.glb` 与 `joker/fixed/joker_student.glb`。

Blender 4.5.9 LTS 后台导入，Workbench FLAT + TEXTURE，透明 RGBA、1024×1536，正交相机，3/4 朝画面右侧，无阴影、描边、辉光或景深。先用完整 T 姿势人物计算一次取景；之后仅切换各个源 GLB 网格节点的 `hide_render`，不重新取景、不把单块放大。源模型保持不变。Blender 为骨骼显示创建的辅助 Icosphere 不属于源 GLB 网格，已排除。

每张单独渲染缩为 256×384，左上角写节点名、材质名、源 GLB POSITION accessor 顶点数量。联系表每行 6 格，均为 1536×1536 RGBA；缩图与拼表不加背景色。每个模型的相机记录在 `camera_*.json`。43 张 1024×1536 原图只留在本机资源库 `model/diag_work`，不提交。

CSV 每个 GLB 网格节点一行；多 primitive 的材质、槽位、alphaMode 以 JSON 数组保存。贴图槽位按照 GLB 字段原名列出（例如 `pbrMetallicRoughness.baseColorTexture`），包括图片 name/URI、texture/image 索引；未臆造 GLB 没有的 normal/specular 等槽位。包围盒同时保留 mesh local、Blender world 八角坐标，坐标系在字段名中区分。

可直接在图上看到的现象：

- 怪盗服 08（`c0001_body_081_gfdMesh_0_mesh`）单独显示时，裤腿/膝部就可见肤色色块；12 是大片外套；17–20 是名字带 Blur 的部件，分别显示零散身体、头发和脸部片段。
- 怪盗服 14（`c0001_face051_gfdMesh_1_mesh`）单独显示时，脸的下半部已有深色区域；13、15、16 是其他脸/颈/下巴片段。此处只记录独立网格画面，不判断哪些应隐藏，也不据此推断贴图错误类型。
- 学生服 16（`c0001_body_205_01_low_gfdMesh_0_mesh`）单独显示时，裤子已有浅色/深色色块；17 是上衣；09–12 是分开的脸、颈和下巴部件。

## 2. 原版材质和节点字段

仅使用已下载的 BlenderToolsForGFS v0.3.1 文件解析器，不通过 Blender GMD 导入。

源 GMD：`joker/source/C0001_051_00.GMD`（怪盗服）和 `joker/source/C0001_001_00.GMD`（学生服）。

`gmd_materials_*.csv` 包括所有解析材质字段、各纹理槽采样器及原始文件名、shader_params、attributes、颜色值、unknown_* 字段、draw_method、disable_backface_culling、纹理索引和标志。`flags_raw_decimal` / `flags_raw_hex` 保留原二进制数值；完整二进制材质字段另存于 `binary_material_raw_json`。布尔字段保持解析器给出的布尔值，不把未知 flag 擅自解释为可见/不可见。

`gmd_nodes_*.csv` 每个原始网格一行，包含节点名/父节点、变换、properties、unknown_byte、has_properties、原模型 flag 数值、网格 flag 的原始数值和解析位、材质名、顶点数、包围范围等。没有查到可直接确认“此网格默认隐藏”的语义字段，不把未知位自行命名成 visibility。bytes 同时保存 UTF-8 替代解码和 hex；复杂字段以 JSON 保留，不删掉看不懂的参数。

## 3. 首段动作两种处理

源动作：本机资源库 `model/joker/joker_thief_animations/BB0001_051_main_0000.glb`，时长约 1.333333 秒。沿用上一轮 `attach_animation_by_name.py`：把 665 个 channel 按精确节点名映射到无面具模型，追加动画数据到本机临时副本，原模型及动作均不改写。264 个目标节点全部匹配。

- A：保留所有动画通道。
- B：选择任务允许的“**不套根节点旋转通道**”方案。仅删除目标精确为节点 `root`、路径 `rotation` 的一个通道（sampler 1）；其余通道及所有关键帧值不变。未采用额外旋转补偿，也未清除模型自身的静态矩阵。

两条均使用第 1 步怪盗服的同一相机，不根据动作重新居中或缩放；采样 0、8、16、24、32、40 参考帧，即 0、0.266667、0.533333、0.8、1.066667、1.333333 秒，包含首尾。30fps 仅为本轮采样时基，源 FPS 仍未知。

**画面现象**：两条中都只有头部在画面下边缘出现，身体大部分出框；头部有轻微位置变化，A、B 没有明显可见区别。动作求值后整体范围约处于 Blender Z -42～32，而 T 姿势范围约为 Z 0～179；完整逐帧数值见 `animation_evidence.json`。未为改善画面而改镜头，未判断骨骼/动作是否已经正确，未批量渲 64 段。

### 对上一轮根节点说明的纠正

上一轮 README_fix.md 中“模型根节点为单位变换”的说法不准确：当时只看了 translation/rotation/scale 字段，漏看 `matrix`。本轮检查模型 `root.matrix` 为列主序：

```text
[1,0,0,0, 0,5.9604645e-08,-0.99999994,0,
 0,0.99999994,5.9604645e-08,0, 0,0,0,1]
```

它本身含约 -90° X 旋转。动作根旋转关键帧（glTF xyzw）首尾均为 `[-0.70703125,0,0,0.70703125]`；translation 为零。B 跳过通道后仍保留模型静态矩阵。Blender 实际求值的 root quaternion（wxyz）在 A 为 `[0.70703125,-0.70703125,0,0]`，B 为 `[0.70710677,-0.70710677,0,0]`。这些是检查数据，不作为动作出框根因的结论。旧报告依本任务要求未修改，纠正在本报告记录。

## 4. 亚森搜索

从 BASE.CPK 按索引补解 `MODEL/CHARACTER/PERSONA/0001/PS0001.GMD`、`BPS0001_000.GAP`、`GT0001.GAP`，由现有 GFDExport 导出 GLB，再运行仓库原版修复脚本，正面及 3/4 各渲一张。PS0001 可见带翼、灰白金属色的人形，不是亚森；GMD 内部纹理名为 `ps_metathron_*`。不凭编号或导出成功认定身份。

随后按 index.csv 连续检查 0002–0011，每个只渲正面一张。达到“再试最多 10 个”上限后停止，没有继续扩大扫描。联系表中的标签列出原编号和纹理名，未把这些候选误标成亚森。纹理命名如下，保留原拼写，不据名称自行改资源身份：

| 编号 | 纹理名代表 |
|---|---|
| 0001 | ps_metathron_t01.dds |
| 0002 | ps_beerzebub_t02.dds |
| 0003 | cuchulainn_t02.dds |
| 0004 | ps_jackrantan_t02.dds |
| 0005 | jackfrost_t02.dds |
| 0006 | pixy_t02.dds |
| 0007 | ps_cerberus_t02.dds |
| 0008 | ps_ririm_t02.dds |
| 0009 | erigo_03.dds |
| 0010 | ps_odin_t02.dds |
| 0011 | kahaku_t02.dds |

结果：本轮在获准范围内**未找到亚森，PS0001 不是亚森**。所有源 GMD 和导出/修复 GLB 都只留本机资源库；未提交。PS0001 的两份 GAP 已补解，但没有将其当作亚森动作播放或导出。

仍待 Claude 统一更正、此次未改的旧资料：

- `research/p5r/README.md` 第 13、28、30 行对 arsene 目录、0000 和“Arsène 7 条动作”的描述。
- `research/p5r/arsene_anim_list.csv` 七条实际上来自 BPS0000_000.GAP 的记录。
- `research/p5r/local_only.csv` 第 3109–3115 行 Arsene animation 分类。
- `research/p5r/selection.csv` 对 0000 路径使用 b_arsene 分类的记录。
- `research/p5r/model/arsene/` 目录中的 GLB、纹理、PS0000.GMD、BPS0000_000.GAP 名称，以及 `research/p5r/model_preview_sheet.png`、`anim_sheet.png` 中对该候选的标签需复核。原始文件及索引均未改动。

## 5. 复现命令、脚本与本机文件

工具目录 `C:\Users\kkk\sts2_tools\p5r_tools`；资源库 `C:\Users\kkk\sts2_tools\p5r_rip`；Python 为系统已有的 Codex dependency Python（numpy 2.3.5、Pillow 12.3.0），未安装新工具或依赖。

以下命令中的 repo、tools、lib 为上述仓库、工具和资源库路径；脚本执行副本在 tools/scripts，可审阅副本在本目录 scripts。所有输出路径均限制在本机资源库或本诊断目录。

```text
python tools/scripts/p5r_diag_metadata.py
blender -b --python tools/scripts/p5r_diag_render.py -- meshes
python tools/scripts/prepare_anim_diag.py
blender -b --python tools/scripts/p5r_diag_render.py -- animation
blender -b --python tools/scripts/diag_root_evaluation.py
python tools/scripts/p5r_diag_animation_evidence.py
python tools/scripts/p5r_diag_sheets.py

P5RAssetTool.exe <P5R游戏目录>/CPK/BASE.CPK <每次单项路径清单> <lib>
GFDExport.exe <lib>/model/CHARACTER/PERSONA/NNNN/PSNNNN.GMD <lib>/model/arsene_search/psNNNN.glb
python <repo>/tools/p5r/fix_gfd_glb.py <lib>/model/arsene_search/psNNNN.glb <lib>/model/arsene_search/psNNNN_fixed.glb
blender -b --python tools/scripts/render_model_check.py -- <固定候选GLB> <PNG绝对路径> front|threequarter
```

编号搜索由 `p5r_search_persona.py` 驱动，含提取、导出、修复及逐个正面渲染；未改现有 GFDExport 或修复脚本。本机中间图、动画临时 GLB、日志在 `lib/model/diag_work`，候选 GLB 和 PNG 纹理在 `lib/model/arsene_search`。CSV 中的 local_path/source_path 方便本机续做，不表示文件已提交。

## 6. 检查和 Git 范围

已逐张查看两张网格联系表、动作对照和 11 个 Persona 候选的联系表。表格数量、网格一一对应关系、PNG 尺寸/RGBA/透明四角、单文件和总量限制均做检查；没有把诊断截图当作成品透明边缘验收。

只暂存并提交本目录 `research/p5r/model/diag`。原来的四个 Joker 和一个 Mokoi 修复 GLB 仍在本机未跟踪状态；本轮新的候选 GLB 也不提交。art/cards 下 11 个已修改文件原样保留，未提交、stash 或还原；提交前校验卡面文件内容。没有启动游戏或使用 Computer Use，没有读取密钥/密码/obs.json，没有改游戏文件或改写 Git 历史。

本轮是诊断结果提交；未找到亚森属于按指定搜索上限得到的结果，不隐瞒或扩大搜索范围。请 Claude 根据 CSV 和逐网格画面判断材质/网格显示规则，并决定正确亚森的下一步定位方案。提交号见 Git 历史和用户报告。
