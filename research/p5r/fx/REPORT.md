# P5R 第一批特效素材报告

日期：2026-10-25。状态：C 方案已通过画面检查；亚森三段序列帧已完成，继续整理原版特效。先前失败记录保留供复核。

## 同步与保护范围

仓库为 Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload，分支 claude/amazing-darwin-b00arn。已 fetch，并从 2e02e440 快进到 4f160382，无冲突。
用户指定的 art/cards、art/p5/poses、art/p5/ref/plan 和其他会话未跟踪内容均原样保留。未提交、删除、移动、stash 或还原其他会话文件。未打开结城理 mod 图片，未读取 obs.json 或任何密码，未启动游戏或 Computer Use。

## 旧动作清单纠正

research/p5r/arsene_anim_list.csv 的七条全部来自 BPS0000_000.GAP，实际上属于 Mokoi（PS0000），不是亚森。旧清单未删除、未修改。用户已授权改用 PS0201 同目录两份原版 GAP，并另建本轮清单。

## 正确原版来源与清单

来源包：BASE.CPK。
- 模型：MODEL/CHARACTER/PERSONA/0201/PS0201.GMD。
- 动作：MODEL/CHARACTER/PERSONA/0201/BPS0201_000.GAP。
- 动作：MODEL/CHARACTER/PERSONA/0201/GT0201.GAP。

已按 index.csv 单项提取两份 GAP 至本机库，使用现有 GFDExport（PS0201 模型作为导出骨架）导出。清单：arsene_anim_list_ps0201.csv，包含原动作名、来源包、包内路径、时长、30fps 参考帧数、导出文件和状态。
14 个条目中 10 个有骨骼轨道，BPS main[1–4] 为零时长、无骨骼 PRS 轨道的空条目；所有条目 unmapped_node_controllers=0。源 FPS 未知，帧数明确为 30fps 参考采样估算，不冒充原生帧数。

## 修复与首次画面检查

对本机新导出的 PS0201.glb 执行仓库原有脚本，未修改脚本：

`fix_gfd_glb.py PS0201.glb PS0201_fixed.glb --drop Blur --flip-v`

执行结果：修复 11 个 IBM accessor，翻转 11 个 UV accessor；无无蒙皮 accessor，没有匹配 Blur 网格。静态检查可见完整亚森、红色衣服、高礼帽和张开的黑翼。

首次动作：BPS0201_000.GAP#main[0]，时长 2.6666667 秒，81 个 30fps 参考采样点。使用现有 attach_animation_by_name.py 按精确节点名映射 434 个通道；保留全部原始 translation、rotation、scale 值，没有猜测修正比例或删除动画通道。

Blender 4.5.9 LTS，后台 Workbench 原贴图平光、透明背景、朝右 3/4 正交相机；诊断图尚未添加成品要求的两阶明暗和描边。相机取自静态模型，六帧固定取景，采样 0、16、32、48、64、80。

**失败现象**：身体、腿部和翅膀明显收拢、挤成一团，整体下移并大部分出框。第一帧求值范围 Z -146.594～81.761，静态几何范围 Z -3.461～350.885。节点名匹配不代表骨骼变换正确，尚未确定根因。

为看清变形，额外对第一帧只调整诊断相机重新取景；未改模型、骨骼或动画数值。该图仍明显异常，因此不是单纯镜头取景问题。

证据：
- arsene_diagnostics/static_vs_animation.png：静态与变形首帧并排对照。
- arsene_diagnostics/BPS0201_000_main_0000_overview.png：首段六帧固定相机总览。
- arsene_diagnostics/static_check.png：完整静态原始检查图。
- arsene_diagnostics/first_reframed_evidence.png：变形首帧重新取景，方便观察。
- arsene_diagnostics/first_bounds.json：六帧实测范围。

## 停止范围与提交

依用户“如果 GAP 里的动作对不上 PS0201 的骨骼（人扭曲、碎开），就停下来报告并附截图”，已停止。仅渲首段用于验收，其他九段未渲，不猜分登场/待机/施法。不输出失败的成品序列帧、anchor 或 still。
原版技能贴图、战斗 UI、音效与语音尚未开始；没有生成图片，没有认定任何素材原版缺失。
第一组未完成，尚无分组素材提交和推送；本轮清单、证据和报告仅保存在指定 research/p5r/fx 目录，未暂存其他文件。

本机工作文件及日志：C:/Users/kkk/sts2_tools/p5r_rip/fx_work/arsene/。原始/固定模型和完整动作 GLB 仅留本机，不提交。

## 用户授权后的 C / D 复试（当前结果）

对 BPS0201_000.GAP#main[0] 和 GT0201.GAP#main[0]，分别渲 C、D 各六帧，见 arsene_diagnostics/CD_comparison.png。两者均跳过 arsene_root 的旋转通道，保留模型原有约 -90° X 静态矩阵，没有额外叠加旋转补偿。
C 仅保留 170 个 rotation 通道，恢复完整体型；D 在 C 基础上保留 Bip01.translation（×1），共 171 通道，仍整体下移。Bip01 模型静止位移约 [0,5.692716,171.2115]，动作首帧约 [0,0,0.99707]；没有可靠证据表明两者仅差统一单位倍数，因此沿用 Joker 第三轮方法，不猜倍数。选择 C。

10 段有骨骼动作均已渲八帧总览；四个空条目无动画可渲，明确记录在清单。总览见 arsene_diagnostics/all_actions_overview.png，各段另有独立 overview.png。

按画面选用途（用途是视觉判断，原始动作没有语义名称）：
- appear：BPS0201_000.GAP#main[8]，从合翼到展翼；36 帧，0～35。
- idle：BPS0201_000.GAP#main[0]，轻微身体/翅膀待机；81 帧，0～80。
- cast：BPS0201_000.GAP#main[5]，抬手、展翼、挥手并回待机；49 帧，0～48。
GT 三段以待机变化为主，未替代上述三个用途。

## 亚森成品输出

art/p5/fx/arsene：166 张 PNG，30fps，高 1024；appear 1437×1024、idle 875×1024、cast 1490×1024。每段按所有帧 alpha 并集固定裁边，帧间画布及锚点一致。每段附 anchor.json，含固定投影脚底地面参考、身体中心参考、裁剪和相机信息；脚底参考是固定地面坐标，并非逐帧追踪悬浮角色的脚。

原贴图、Blender EEVEE 正交朝右 3/4、Diffuse→ShaderToRGB→两阶恒定色阶乘原贴图，输出平涂明暗。最终尺寸以 alpha 外扩 3px 合成黑色轮廓，透明轮廓外 alpha=0，四角透明检查通过。
arsene_still.png 为 cast 第18帧，透明、高1536；contact_sheet.png 每段八帧。动作来自原版、采用 C 旋转筛选，不是手摆姿势；没有触发手摆兜底。

本组约 73.42 MB，最大文件低于20MB。原始高质量渲染、GLB、脚本及日志仍只在本机库 fx_work/arsene 和当前聊天 work 目录。后续贴图/UI/音频待继续；提交号将在推送后补记。


## 特效贴图与战斗 GUI（原版导出结果）

亚森成品提交并推送：a6ec5cf7（art: P5 Arsene 2D frames from original model），提交范围仅 art/p5/fx/arsene。

原版技能范围按索引选择性提取 BATTLE/SKILL 中 837 个 BED/EPL（原始约528.30 MB），结合指定命中/枪击/召唤事件文件，共从有效 DDS 头读取9326张贴图，Pillow 解码错误0。全部完整 PNG 和容器只留本机 fx_work/textures；texture_catalog_local.csv 可查来源包、路径、纹理名、原始 DDS 字节偏移、尺寸、alpha和本机PNG。输出为原始贴图，不根据白灰蒙版猜造最终粒子颜色。

仓库用途组：gun 43张（BES_GUN1–10）、slash 3张、blunt 3张、summon_blueflame_candidates 11张（觉醒事件0282/PERSONA_A–D，含 bes_kamen_EPD、火焰序列及链条）；curse_candidates 155张、sukukaja_candidates 34张。各组含深灰联系表、文件名与尺寸、sources.csv。

读取 BATTLE/TABLE/SKILL.TBL、SC.CPK 的 BATTLE/TABLE/NAME.TBL，并检查原有展开表；未可靠解码当前资源的技能名，未取得英文名→ID的可信对应。已按任务允许的贴图内容筛选紫色/红色/暗色火焰、上升光线候选，但**尚未确认哪组是 Eiha/Eigaon/Maeiha/Sukukaja**。文件夹明确用 candidates，不假报成已命名成品。枪口贴图高度共用，BES_GUN1作为 Joker 手枪优先候选，未从事件/角色引用证明；保留十组供比较。

BES_CRITICALG.BED 没有 DDS 头，本轮未解出图，写在 critical/；这不证明原版没有。EPL 发射器/时间轴：读不出来，只写状态，不猜参数。召唤事件贴图有原始蒙版，但蓝色着色及具体 Joker 召唤时间线未恢复。

BATTLE/GUI 全部125项已检查，72张有效纹理图已 PNG 化，保留alpha；53项没有嵌入DDS（几何/脚本/其他资源），详见 container_audit.csv，不冒充已PNG化。已有四份SPD原坐标与590张切图复制至 ui/battle_gui/sprites；找到技能名斜框（DAMAGE_DATA索引120及121–124）、数字79–88、WEAK94、CRITICAL95、TECHNICAL125和状态异常图标。1 MORE、MISS与增减益箭头未可靠定位。sprites.json与原注释索引均保留，未自行重绘。

本轮只新增自己的 research/p5r/fx 与 research/p5r/ui/battle_gui。贴图/UI 提交号待提交后补记。音频尚未进入此提交。
