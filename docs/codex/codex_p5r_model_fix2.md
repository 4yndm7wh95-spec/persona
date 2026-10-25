你在用户的 Windows 电脑上工作。任务：接着 research/p5r/model/README_fix.md 那一轮，**只做诊断和取证**（拆网格逐个渲、列材质表、试第一段动作、找对亚森），把结果推到仓库，由 Claude 判断原因、给修法。不改 mod 代码，不改修复脚本，不生图，不启动游戏。

**先读**：docs/codex/README.md（合并规则）、research/p5r/README.md（资源库和工具位置）、research/p5r/model/README_fix.md（上一轮的结果和你写的脚本位置）。
找仓库、拉取（第 1 节）、提交推送（第 7 节）、「绝对不要做」清单都和 docs/codex/codex_p5r_model_fix.md 一样，照那份执行。
art/cards/v3/README.md 和 art/cards/v4/ 下另一个会话的 11 个已修改文件：原样保留，不提交、不 stash、不还原。

【Claude 对上一轮检查图的判断】
- 怪盗服身体、四肢、外套、头发位置已经对了，修复脚本这部分有效。
- 剩下的问题像是「不该显示的网格被显示出来了」，不是矩阵问题：脸下半部的黑块、大腿 / 膝盖露出的肤色块、学生服裤子上一块块的色块。P5R 模型里有平时隐藏的网格（名字带 Blur 的动态模糊替身、身体下面的皮肤层、表情 / 嘴部的替换件），也可能是材质的透明 / 贴图槽用错了。要你把每块网格单独渲出来，Claude 来认。
- 菜单模型（joker_menu）先不管，后面不用它。
- 亚森：PS0000 是 Mokoi，不是亚森。P5R 的 Persona 编号里亚森很可能是 0001（MODEL/CHARACTER/PERSONA/0001/PS0001.GMD，同目录还有 BPS0001_000.GAP、GT0001.GAP）。

【1. 拆网格逐个渲（怪盗服无面具 + 学生服）】
对象：本机的 research\p5r\model\joker\fixed\joker_thief_nomask.glb 和 joker_student.glb（上一轮生成、没提交的那两个）。
1. 每个有网格的节点**单独**渲一张（其他网格全部隐藏），T 姿势、3/4 朝右、和上一轮同样的 Workbench 平光、透明底、正交相机、**相机和取景固定成整个人的全身取景**（不要按单块网格放大），这样能看出这块在身上的位置。
2. 每张缩到 256×384，左上角写：节点名、材质名、顶点数。拼成联系表 mesh_sheet_thief.png、mesh_sheet_student.png（每行 6 张）。
3. 另出 mesh_table_thief.csv、mesh_table_student.csv：节点名、网格名、是否蒙皮、材质名、材质用到的每张贴图（GLB 里的 baseColor 等槽位 + 贴图文件名）、alphaMode、顶点数、包围盒。

【2. 原版材质信息】
用你上一轮下载的 BlenderToolsForGFS v0.3.1 里的**文件解析器**（不需要 Blender 导入）读原始 GMD：
- 怪盗服、学生服各自的源 GMD（research\p5r\model\joker\source\ 下，或按 research\p5r\README.md 找到的包内路径）；
- 每个材质导出一行到 gmd_materials_thief.csv / gmd_materials_student.csv：材质名、所有贴图槽位和贴图文件名（diffuse / normal / specular / shadow / reflection / highlight / glow / night / detail 之类，以解析器给出的字段为准）、颜色参数、标志位（原样数值）、绘制方法 / 透明 / 剔除相关字段（原样）。
- 每个网格节点的可见性 / 属性标志（如果解析器读得到）也原样列进 gmd_nodes_thief.csv / gmd_nodes_student.csv。
看不懂的字段不要解释，原样列出来就行。

【3. 第一段动作试渲】
1. 用你上一轮写的按节点名套动作的脚本，把 BB0001_051_main_0000 套到 joker_thief_nomask.glb 上，渲 6 帧（含首尾）横条，3/4 朝右，和第 1 步同样的取景。
2. 再出一版：只把动作根节点那个约 -90° X 的旋转抵消掉（或者不套根节点的旋转通道，二选一，写清用的哪种），同样渲 6 帧。
3. 两条都放进 anim_test.png，标清楚哪条是哪种。到这里就停，**不要批量渲 64 段**。

【4. 找对亚森】
1. 用本机 P5RAssetTool 按包内路径补解 MODEL/CHARACTER/PERSONA/0001/ 下的 PS0001.GMD、BPS0001_000.GAP、GT0001.GAP（命令见 research\p5r\README.md）。
2. 用 GFD Studio 导出 GLB（和上一轮同样的方法），再用 python tools\p5r\fix_gfd_glb.py <输入> <输出> 修一遍，输出到本机资源库，T 姿势渲一张正面、一张 3/4。
3. 如果不是亚森：从 index.csv 里 MODEL/CHARACTER/PERSONA/ 下按编号往后再试最多 10 个（每个只渲一张正面缩略图），拼成一张联系表，标编号，找到亚森就停。
4. 研究资料里把 PS0000 当成亚森的地方（research\p5r\README.md、arsene_anim_list.csv 等）先不要改，在报告里列出来，等 Claude 统一改。

【5. 输出】
全部放 research\p5r\model\diag\：
mesh_sheet_thief.png、mesh_sheet_student.png、mesh_table_*.csv、gmd_materials_*.csv、gmd_nodes_*.csv、anim_test.png、arsene_candidates.png（找到的话加 arsene_ps0001_front.png、arsene_ps0001_threequarter.png），以及 README_diag.md（中文：每一步做了什么、用的命令和脚本、没做成的和原因）。
GLB 仍然只留在本机，不提交。仓库新增总量不超过 60MB，单文件不超过 20MB。
提交：只 add research/p5r/model/diag；提交信息 "research: P5R model diagnostics (per-mesh renders, GMD materials, first anim test, Arsene search)"。

【6. 报告】
中文：每一步的结果和文件位置、亚森是不是 PS0001、第一段动作两种处理各自看起来怎样（只描述现象，不下结论）、提交号。
