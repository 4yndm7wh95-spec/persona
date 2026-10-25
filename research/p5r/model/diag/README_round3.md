# P5R 模型第三轮：Flip-V 重建、动作套用对比、亚森定位

日期：2026-10-25。任务 `docs/codex/codex_p5r_model_fix3.md`，开始时已读四份指定说明并快进至 `be571a82`。分支 `claude/amazing-darwin-b00arn`。

## 结果导航

| 项目 | 文件 | 结果 |
|---|---|---|
| 三套模型正面/3/4 | [../joker/fixed2_check.png](../joker/fixed2_check.png) | 6 张，每格 512×768；外观与 Claude 对照图一致 |
| 动作节点逐项比较 | [anim_compare.csv](anim_compare.csv)、[anim_compare_summary.json](anim_compare_summary.json) | 376 个节点行，加 1 行汇总；保留两边父节点、模型原始 TRS/matrix、动作首帧和通道 |
| A/C/D 首段动作 | [anim_test2.png](anim_test2.png) | 3 条，各 6 帧；C 全身入画，A、D 出框，未批量渲 64 段 |
| 套法及实测范围 | [retarget_variants.json](retarget_variants.json)、[animation_render_info_round3.json](animation_render_info_round3.json)、[camera_fixed2.json](camera_fixed2.json) | 完整通道数、D 选择、固定相机和各帧范围 |
| 全 Persona 贴图名 | [persona_texture_names.csv](persona_texture_names.csv)、[texture_search_summary.json](texture_search_summary.json) | 453 个 GMD 全部解析成功；另列 1 个 EPL 的读取结果 |
| 贴图命中候选 | [arsene_texture_candidates.png](arsene_texture_candidates.png)、[arsene_found_sources.json](arsene_found_sources.json) | 5 个命中候选的正面缩略图及本机来源 |
| 确认的亚森 | [arsene_found_front.png](arsene_found_front.png)、[arsene_found_threequarter.png](arsene_found_threequarter.png) | `MODEL/CHARACTER/PERSONA/0201/PS0201.GMD` |
| 本轮诊断脚本 | [scripts/round3/](scripts/round3/) | 新增脚本副本供 Claude 审阅；未改仓库修复脚本 |

## 1. 按新版脚本重建模型

沿用本机 Python/numpy 和 Blender 4.5.9 LTS。使用远端更新的 `tools/p5r/fix_gfd_glb.py`，未修改该脚本：

```text
python tools/p5r/fix_gfd_glb.py research/p5r/model/joker/joker_thief.glb <lib>/model/joker/fixed2/joker_thief_nomask.glb --drop kamen,Blur --flip-v
python tools/p5r/fix_gfd_glb.py research/p5r/model/joker/joker_thief.glb <lib>/model/joker/fixed2/joker_thief.glb --drop Blur --flip-v
python tools/p5r/fix_gfd_glb.py research/p5r/model/joker/joker_student.glb <lib>/model/joker/fixed2/joker_student.glb --drop Blur --flip-v
```

三个脚本均成功：怪盗服各修 16 个 IBM accessor、翻转 22 个 UV accessor；学生服修 12 个 IBM accessor、翻转 19 个 UV accessor。无面具版去掉两个 kamen 和四个 Blur 网格；有面具版去掉四个 Blur；学生服没有匹配 Blur 的网格。没有按其他规则隐藏/删掉脸部、身体或衣裤网格，也没有改原始 GLB。

沿用上一轮 `render_model_check.py`，每个模型渲正面及朝右 3/4，Workbench 平光原贴图、正交相机、透明 RGBA、1024×1536；缩成 512×768 拼成 `joker/fixed2_check.png`（1024×2304）。

已对照 `joker/fixed_flipv_preview.jpg`：无面具版脸部正常、外套胸袋白巾可见、靴子为棕色；学生服白衬衣和格子裤正常。此前脸下半部黑块、裤腿肤色条/色块未再出现。带面具版保留面具。姿势和光照/取景存在差别，不作逐像素比较；模型外观与参考一致，因此继续第 2–4 步。

GLB 只在本机 `<lib>/model/joker/fixed2/`，未提交。

## 2. 动作节点比较：只记录数据现象

输入是 fixed2 无面具模型及本机 `joker_thief_animations/BB0001_051_main_0000.glb`。`anim_compare.csv` 列出两边节点名称的并集全部 376 个节点，覆盖任务要求的 root、root 下一层、hip/waist/spine/neck/head/shoulder/arm/leg/foot 等名称，以及全部 264 个有 translation 通道的节点。末行 `__SUMMARY__` 包含场景根和根下面前三层的完整名字/父子关系与文件内容汇总，独立 JSON 也可直接查看。

模型静止 translation/rotation/scale/matrix 保留原字段（不存在则空，不用分解值冒充原字段）；动作只有有通道的分量才填写，读取最早时间关键帧原值。另有明确命名的 `model_effective_local_translation` 辅助比较（从 matrix 取位移列，不改文件）。

观察到：

- 模型 376 个节点，动作 352 个节点，动作 GLB **没有网格**。共有的 352 个节点父节点名全部相同；模型额外有 24 个网格叶节点（其中包括已经去 mesh 的 kamen/Blur 节点），动作没有这些叶节点。完整 GLB 前三层并非完全相同，差异名列在汇总中，不能把数组索引不同当骨架不同。
- 两边场景根都是 `RootNode → root`。root 下一层的九个名字一致：`c0001_hand_081`、`c0001_body_081`、`h_C_US3`、`c0001_face051`、`h_collision`、`Blur_body___kai051`、`Blur_hair___sei01`、`Blur_face___sei01`、`rot`。模型继续带网格叶节点，动作没有网格。
- 动作有 264 个 rotation、264 个 translation、137 个 scale 通道，共 665 个。
- `Bip01`（路径 `RootNode/root/rot/Bip01`，其下为 `Bip01___Pelvis`）模型位移列为 `[0,2.611246,94.80555]`；动作首帧 translation 为 `[0.990234375,-0.509765625,0.9990234375]`。
- 例如 `b___r___s___seifuk02` 模型位移约 `[19.018028,-0.000001907,-0.000000238]`，动作首帧约 `[0.999511719,0,0]`。另一些节点对应分量接近模型值。未根据这些数据自行解释导出格式或修动作。
- 257 个双方都非零的位移范数比（模型/动作）约从 1.0000 到 63.3903，中位数约 4.7042；分量和比值并未显示一个适用于全部节点的固定 100 或 0.01 倍数。因此 D 不加推测倍数，明确用 ×1。范数比只是辅助数值，不代表已经证明两者只差比例。

## 3. 三种套法各 6 帧

沿用上一轮按精确节点名映射 channel 的临时合成方法，原模型/动作均只读。本轮源模型改为 fixed2。所有变体的关键帧数值不改，只有通道筛选不同：

| 变体 | 通道 | rotation | translation | scale |
|---|---|---:|---:|---:|
| A | 全部 | 264 | 264 | 137 |
| C | 只套旋转，保留模型自己的静止位移/缩放 | 264 | 0 | 0 |
| D | 旋转 + `Bip01.translation ×1`，保留其他模型静止位移/缩放 | 264 | 1 | 0 |

D 选择骨架顶部、Pelvis 父节点 `Bip01` 作为本轮中心位移节点；它在 `root/rot` 之下且有非零首帧 translation。没有把 `root`/`rot` 或所有 Pelvis 辅助节点的位移一并加入，具体目标名和路径写入图标及 JSON。所有动作 scale 通道都不套。没有猜测单位换算，也没有额外补偿 root 旋转。

取样仍是 0、8、16、24、32、40 个 30fps 参考帧，对应 0、0.266667、0.533333、0.8、1.066667、1.333333 秒（含首尾）。源 FPS 未知，30fps 仅是预览采样时基。三条共 18 张原图。相机统一从 fixed2 T 姿势全身取景计算一次，之后三条、六帧始终不移动/重构图；每帧缩成 256×384 横排，合成 `anim_test2.png`。

只描述画面和求值范围：

- **A**：仍只在图格下缘露出头部，身体大部分出框。首帧 Blender Z 范围约 -38.81～31.08。
- **C**：可以看到完整人物、分开的双腿、放下的双臂；衣摆和躯干在帧间有轻微变化，能看见正常大小的待机姿势。首帧 Z 约 -9.84～181.00。
- **D**：人物大小接近 C，但整体明显下移，只见头与上半身在图格下部，下半身出框。首帧 Z 约 -103.64～87.19。

这些是本次三种筛选下的观察，不声称已经修好全部动作，也不确定当前 GLB 位移数值异常的原始原因。本轮仅试这一段，未批量渲染 64 段。

## 4. 按贴图名搜索亚森

从 index.csv 枚举 `MODEL/CHARACTER/PERSONA/` 下全部 `PS*.GMD`，包含 PSZ 变体，共 **453 个文件**；再加 `BATTLE/EVENT/BCD/0282/EPL/ARSENE.EPL`。以 P5RAssetTool 按需补解缺失的 443 项约 4.625 GB，提取后资源库约 10.78 GB，仍低于 15 GiB 上限。没有批量导出这 453 个模型，只用 GFS v0.3.1 文件解析器读取贴图名。

`persona_texture_names.csv` 共 454 条记录，保留 CPK、包内路径、编号、全部贴图名（分号分隔）、解析状态、错误和关键词命中。453 个 GMD 全部解析成功，失败 0。关键词为 `arsene`、`arsenu`、`arsen`、`ars_`、`0001`（不分大小写），命中以下 5 个：

```text
MODEL/CHARACTER/PERSONA/0200/PS0200.GMD
MODEL/CHARACTER/PERSONA/0201/PS0201.GMD
MODEL/CHARACTER/PERSONA/0201/PSZ0201.GMD
MODEL/CHARACTER/PERSONA/0363/PS0363.GMD
MODEL/CHARACTER/PERSONA/0363/PSZ0363.GMD
```

0200/0201 使用 `ps_arsene_*`；0363 使用 `ps_arsene_modern_*`。只对这 5 个命中候选执行 GFDExport、原版修复脚本 `--flip-v`、正面/3/4 渲染；GLB 和所有原始图只留本机。`arsene_texture_candidates.png` 提供 5 个候选的正面缩图，说明命中同一纹理关键词并不意味着外观相同。

**找到：`MODEL/CHARACTER/PERSONA/0201/PS0201.GMD`。** 渲图为红色服装、高礼帽、黑色羽翼的亚森。采用这个文件生成任务指定的 `arsene_found_front.png` / `arsene_found_threequarter.png`，透明 1024×1536，左上角标来源。双翼较宽，固定全身正交取景会留较多上方空白，没有裁掉翅膀来放大人物。

其贴图完整名：`ps_arsene_t02.dds`、`ps_arsene_t01_sp.dds`、`ps_arsene_t01_shadow.dds`、`ps_arsene_eye_aura.dds`、`ps_arsene_eye_uvscrl.dds`。

EPL：`EPLFileInterface.from_file` 能读取容器，但没有暴露可直接枚举的 TextureInterface 列表；遍历已解析结构未取得贴图名。表中状态为 `parsed_no_texture_interface_exposed`，不把它当作“没有嵌入模型/没有贴图”的证明，也没有强行把未知二进制当 GMD。找到的亚森来源是上述 PS0201.GMD，不是 EPL。

旧版把 PS0000 当亚森的 README、分类和动画 CSV 本轮未修改；本轮没有补做亚森动作。

## 5. 命令和本机路径

`<repo>` 为 `C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload`。
`<tools>` 为 `C:\Users\kkk\sts2_tools\p5r_tools`；`<lib>` 为 `C:\Users\kkk\sts2_tools\p5r_rip`。

Blender：`<tools>/blender/blender-4.5.9-windows-x64/blender.exe`；Python：本机已有 Codex runtime Python，含 numpy 和 Pillow；未安装新工具或依赖。

执行流程（占位路径应换为上述绝对路径）：

```text
# 重建命令见第 1 节
blender -b --python <tools>/scripts/render_model_check.py -- <lib>/model/joker/fixed2/<模型>.glb <lib>/model/round3_work/<模型_视角>.png front|threequarter
python <tools>/scripts/p5r_round3_anim_data.py
python <tools>/scripts/p5r_round3_prepare_anim.py
blender -b --python <tools>/scripts/p5r_round3_render_anim.py -- animation
python <tools>/scripts/p5r_round3_texture_search.py
python <tools>/scripts/p5r_round3_found_candidates.py

# 搜索脚本内调用
P5RAssetTool.exe <P5R游戏目录>/CPK/BASE.CPK <缺失路径清单> <lib>
GFDExport.exe <lib>/<命中包内路径> <lib>/model/arsene_search_round3/<编号>.glb
python <repo>/tools/p5r/fix_gfd_glb.py <输入GLB> <修复GLB> --flip-v
blender -b --python <tools>/scripts/render_model_check.py -- <修复GLB> <PNG绝对路径> front|threequarter
```

本轮脚本执行版在 `<tools>/scripts/`，可审阅副本在本目录 `scripts/round3/`。fixed2 GLB 在 `<lib>/model/joker/fixed2`；5 个亚森命中候选 GLB 在 `<lib>/model/arsene_search_round3`；动画临时 GLB、逐帧图、搜索清单和命令日志在 `<lib>/model/round3_work`。不提交任何 GLB。

## 6. 检查和提交范围

已查看 fixed2 六图联系表并与 Claude 对照图比较；逐项核对 A/C/D 渠道筛选、18 张采样原图、固定相机参数；查看动作对照和 5 个贴图候选的联系表及 PS0201 正面/3/4。CSV 包含全部有 translation 的节点和全部 453 个 GMD，状态及 EPL 限制均如实保留。PNG 为 RGBA，透明四角和尺寸检查通过；这属于研究预览，不声称完整动画或游戏效果已验收。

本轮只新增 `research/p5r/model` 内的图片、CSV、JSON、报告和本轮诊断脚本副本，总量低于 40 MB。所有 fixed2/亚森/动画 GLB 留本机不提交；原有五个未跟踪的第一轮 GLB 保持原样。未改 mod、原始模型、游戏文件、旧报告或修复脚本，未启动游戏，未用 Computer Use，未读取 obs.json、密码或输出密钥。

`art/cards` 已修改文件原样保留，未提交、stash、还原；提交前复核卡面内容校验值，暂存区仅允许本轮指定输出。按 README 合并规则提交推送，不 rebase、不 force。提交号见本轮用户报告及 Git 历史。
