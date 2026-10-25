# P5R 模型修复检查报告（未通过，按任务停止）

日期：2026-10-25。仓库：`C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload`。
分支：`claude/amazing-darwin-b00arn`。已 fetch，并从 `91b53a75` 快进到 `c398bad9`。没有合并冲突。

## 结论

脚本五次执行均成功，但“脚本成功”不等于模型验收通过。本轮已生成四个 Joker 修复候选、一个原“亚森”候选和 9 张检查图（Joker 各正面、3/4 一张，另加“亚森”一张），以及 Joker 联系表。

| 文件（相对 research/p5r/model） | 检查结果 | 检查图 |
|---|---|---|
| joker/fixed/joker_thief_nomask.glb | 身体 T 姿势已连在一起，头发、眼睛、手套、外套未见大块飘离；两个 kamen 网格已移除。但面部黑块及腿部/鞋部外观异常，不能视为成品。 | joker/fixed/check/joker_thief_nomask_front.png、joker_thief_nomask_threequarter.png |
| joker/fixed/joker_thief.glb | 身体已聚合，面具保留；仍有面部及腿部外观异常。 | joker/fixed/check/joker_thief_front.png、joker_thief_threequarter.png |
| joker/fixed/joker_student.glb | 身体已聚合，头发与眼睛在头部；面部发黑、衣服和腿部呈不正常色块，未通过外观检查。 | joker/fixed/check/joker_student_front.png、joker_student_threequarter.png |
| joker/fixed/joker_menu.glb | 未通过。整体包围范围约 X -3361.72～149.00、Y -19.58～178.77、Z -2415.58～178.85（Blender 导入坐标）；按全部几何构图时只见零散小块，无法得到正常全身图。没有隐藏、删除或改动模型中的部件。 | joker/fixed/check/joker_menu_front.png、joker_menu_threequarter.png |
| arsene/fixed/arsene.glb | 几何已不再散开，但画面实际为 Mokoi，不是亚森。原始 source/PS0000.GMD 直接解析出的纹理为 ps_mokoi_t01.dds、ps_mokoi_t01_sp.dds、ps_mokoi_t01_shadow.dds；74 个骨骼、6 个网格。问题涉及上轮选错源模型，不能用换导出格式解决角色身份。 | arsene/fixed/arsene_check.png |

面部/腿部异常的根因尚未确定，不能据此断言是 UV、材质、多个替代网格重叠或矩阵中的哪一项。不修改现有修复脚本，也未私自修模型。

## 命令和完整修复输出

在仓库根目录，用带 numpy 2.3.5 的本机 Python 执行：

```text
python tools/p5r/fix_gfd_glb.py research/p5r/model/joker/joker_thief.glb research/p5r/model/joker/fixed/joker_thief_nomask.glb --drop kamen
rebased 16 unskinned accessors
dropped ['kamen051_in_gfdMesh_0_mesh', 'kamen051_gfdMesh_0_mesh']
rewrote 16 IBM accessors -> research/p5r/model/joker/fixed/joker_thief_nomask.glb

python tools/p5r/fix_gfd_glb.py research/p5r/model/joker/joker_thief.glb research/p5r/model/joker/fixed/joker_thief.glb
rebased 16 unskinned accessors
dropped []
rewrote 16 IBM accessors -> research/p5r/model/joker/fixed/joker_thief.glb

python tools/p5r/fix_gfd_glb.py research/p5r/model/joker/joker_student.glb research/p5r/model/joker/fixed/joker_student.glb
rebased 18 unskinned accessors
dropped []
rewrote 12 IBM accessors -> research/p5r/model/joker/fixed/joker_student.glb

python tools/p5r/fix_gfd_glb.py research/p5r/model/joker/joker_menu.glb research/p5r/model/joker/fixed/joker_menu.glb
rebased 52 unskinned accessors
dropped []
rewrote 15 IBM accessors -> research/p5r/model/joker/fixed/joker_menu.glb

python tools/p5r/fix_gfd_glb.py research/p5r/model/arsene/arsene.glb research/p5r/model/arsene/fixed/arsene.glb
rebased 6 unskinned accessors
dropped []
rewrote 3 IBM accessors -> research/p5r/model/arsene/fixed/arsene.glb
```

## 渲染工具与参数

Blender 4.5.9 LTS，build 8bf95cbd38d1，Windows 便携 ZIP：
https://download.blender.org/release/Blender4.5/blender-4.5.9-windows-x64.zip

官方 SHA-256：`41da973b9bf95bb312cbeff4d1982feb13259b43c821686b9bafea4dfe5477cf`，下载文件核对一致。
本机位置：`C:\Users\kkk\sts2_tools\p5r_tools\blender\blender-4.5.9-windows-x64\blender.exe`。

渲染脚本：`C:\Users\kkk\sts2_tools\p5r_tools\scripts\render_model_check.py`。
后台模式 `blender -b --python <脚本> -- <GLB绝对路径> <PNG绝对路径> front|threequarter`。
Workbench、纹理原色平光、无阴影/描边/辉光/景深；透明 RGBA、1024×1536、正交相机；3/4 朝画面右侧，底部构图预留约 60px。菜单模型因异常几何范围无法满足正常人物构图要求，保留全几何检查图。

每张检查图左上角白字黑边标文件名。PNG 四角 alpha 均为 0；透明背景由 film_transparent 输出，未使用色底抠图。标签是明确要求的叠字，不属于人物轮廓。没有进行最终成品级透明边缘/材质验收。

## 动作：尚未完成验收

CSV 中 `BB0001_051.GAP` 的 64 段已核对存在。第一段 `BB0001_051_main_0000.glb` 是 41 个 30fps 参考帧、时长约 1.333333 秒（源 FPS 未知）。
对第一段进行静态数据检查：665 个动画通道涉及 264 个节点，全部名称在无面具模型中可找到。动作根节点带约 -90° X 旋转，模型根节点为单位变换；这只是需要画面确认的风险，尚未证明动作会翻转。

套用脚本思路：读取 GLB JSON/BIN，以精确节点名把动画 channel.target.node 映射到固定模型节点；追加动画 accessors/bufferViews 和二进制数据，保持关键帧值不变；再由 Blender 导入并播放。临时合成文件只在本机资源库，不改原模型或原动作。
脚本：`C:\Users\kkk\sts2_tools\p5r_tools\scripts\attach_animation_by_name.py`。
临时副本：`C:\Users\kkk\sts2_tools\p5r_rip\model\joker\model_fix_work\BB0001_051_main_0000_attached.glb`。

**实际渲染动作数：0。** 静态模型验收已失败，并发现亚森源文件身份不符，因此依任务“哪里不对就截图写进报告，不要自己改模型”和“遇到上面没写到的情况，停下来报告”停止后续模型处理。未运行 64 段批量渲染，未生成动作总览或其他动作分类表，不声称套用成功。

## 亚森备用路线状态

已从官方 release 下载并解压 Pherakki BlenderToolsForGFS v0.3.1：
https://github.com/Pherakki/BlenderToolsForGFS/releases/tag/v0.3.1
本机：`C:\Users\kkk\sts2_tools\p5r_tools\GFS-v0.3.1`。
仅用其文件解析器读取现有 source/PS0000.GMD 的结构和纹理名；没有执行 Blender GMD 导入、FBX/DAE 重导出，也没有安装额外依赖。由于源文件实际是 Mokoi，这些路线无法把它变成亚森。需要先确认正确亚森源资源，或明确后续调查范围。

## Git、未提交文件与体积

本轮只新增 research/p5r/model 下候选和检查资料，没有改原 GLB、已有资源或 tools/p5r/fix_gfd_glb.py；没有运行游戏或 Computer Use。

用户确认保留的 art/cards/v3/README.md 与 art/cards/v4 下共 11 个已修改文件仍未提交；未 stash、checkout、restore、reset，也未改动内容。记录并复核 566 个卡面文件 SHA-256，内容全部一致。

初次检查触发停止条件后未提交模型候选。用户随后明确要求只把问题报告提交到仓库，由用户在 Claude 上交流。本次提交范围仅本报告、8 张 Joker 检查图、Joker 联系表和 1 张 Mokoi 证据图；不提交任何 fixed GLB 或卡面文件。检查基线为 c398bad9；报告提交号见 Git 历史。

本机本轮全部新增模型候选及检查资料约 19.7 MB，最大单文件约 3.38 MB，低于任务限制。本次只提交报告及截图；GLB 留在本机未提交，不能作为验收成品。

## 待 Claude 判断的后续方案

1. Joker：请检查 `tools/p5r/fix_gfd_glb.py` 修复后的材质、UV 和替代/模糊网格可见性，判断脸部黑块与衣裤/鞋异常的具体原因，并给出允许本机执行的新脚本或明确操作步骤。当前截图不能证明具体根因，请不要把“身体已聚合”当作成品通过。
2. 菜单模型：请判断 `ef___line_obj_*`、`line`、道具和多套服装等网格是否存在特殊绑定/显示规则；是否应在预览时按白名单显示，而不是把全部节点直接渲出。当前异常包围范围已记录，不应直接裁掉远处几何来掩盖问题。
3. 亚森：请确认真正亚森的 GMD 包内路径和对应动作；现有 `MODEL/CHARACTER/PERSONA/0000/PS0000.GMD` 内容实际为 Mokoi，不能继续以其作为亚森。请明确是否授权本机按 index.csv 调查并补解正确文件，以及研究资料中已有错误命名/说明应如何修订。
4. 动作：静态检查通过后，先验证第一段根节点约 -90° X 旋转的处理方式，再决定是否批量渲染 64 段。当前未运行首段画面验收，不能下结论说动作已坏或已成功。

用户将在 Claude 端交流，本机不另发会话消息。
