# P5R Joker 第一批特效素材报告

日期：2026-10-25。当前结果：亚森三段序列帧已完成；原版特效贴图、战斗GUI和音频候选已提交。具体技能编号、部分UI项目与日语目标台词尚未确认，不能把候选当作最终选定素材。没有生图。

## Git与工作范围

仓库：C:/Users/kkk/Documents/Codex/2026-10-12/claude-git-c-users-kkk-videos/work/persona-ref-upload。
分支：claude/amazing-darwin-b00arn。开始时fetch成功，merge --ff-only从2e02e440快进至4f160382；复试开始再次fetch并确认已同步。没有rebase/reset/amend/force。

| 分组 | 已推送提交 | 范围 |
|---|---|---|
| 亚森 | a6ec5cf7 | art/p5/fx/arsene |
| 特效贴图与GUI | 86a3de15 | research/p5r/fx、research/p5r/ui/battle_gui |
| 音频候选 | 039f84b6 | research/p5r/fx/audio与本报告 |

只暂存自己的目录/文件；其他会话的art/cards、art/p5/poses、art/p5/ref/plan和用户确认的未跟踪目录/GLB均原样保留，未提交、删除、移动、stash或还原。没有读取obs.json、密码或密钥；没有打开结城理mod图片、启动游戏、使用Computer Use或修改mod/游戏原文件。现有修复脚本未改。

## 1. 亚森：原版PS0201模型和原版动作

来源包：BASE.CPK。
- 模型：MODEL/CHARACTER/PERSONA/0201/PS0201.GMD。
- 动作：MODEL/CHARACTER/PERSONA/0201/BPS0201_000.GAP、GT0201.GAP。

旧research/p5r/arsene_anim_list.csv的七个动作实际来自Mokoi（PS0000/BPS0000_000.GAP），未删除、未修改。用户授权后另建arsene_anim_list_ps0201.csv，记录动作名、来源包/路径、时长、帧数和状态。14条中10条含骨骼轨道，BPS main[1–4]是零时长空条目；源FPS未知，帧数明确为30fps参考采样。

使用现有GFDExport，以PS0201为模型骨架导出。修复执行原脚本 `fix_gfd_glb.py PS0201.glb PS0201_fixed.glb --drop Blur --flip-v`：重写11个IBM accessor、翻转11个UV accessor、无无蒙皮accessor、没有匹配Blur网格。静态亚森完整、原贴图正常。

### C/D复试和选择

最初全部通道原样套用时，身体和翅膀挤成一团、整体下移；证据保留在arsene_diagnostics/static_vs_animation.png和首段总览。该失败已被后续C方案替代。

对BPS main[0]与GT main[0]分别渲C、D六帧（0/16/32/48/64/80），相机固定，见arsene_diagnostics/CD_comparison.png。
- C：只保留170个rotation通道；跳过模型根arsene_root的旋转通道，保留模型原有约-90° X静态矩阵；不额外叠加旋转补偿。不套translation/scale，骨骼长度用模型自己的。
- D：C基础上加入骨架顶部、Pelvis父节点Bip01的translation，×1，共171通道。
- Bip01模型静止位移约[0,5.692716,171.2115]，动作首帧约[0,0,0.997]。两份动作各166个双方非零位移范数比范围约1～171.8、中位约7.996，没有可确认的统一倍数，沿用Joker第三轮方法不猜比例。详见translation_ratio_summary.json。
- 画面：C恢复完整体型和正常动作；D仍下移。选C，未触发手摆兜底。所有成品都是原版旋转轨道筛选后的渲染，不是手摆。

10段有效动作各渲八帧总览，见arsene_diagnostics/all_actions_overview.png和各段独立overview.png；空条目没有伪造图。按画面挑选用途（用途为视觉判断，原始文件无这些语义名称）：

| 用途 | 原动作 | 序列帧 |
|---|---|---|
| 登场 appear | BPS0201_000.GAP#main[8]，合翼到展翼 | 36帧，0000–0035 |
| 待机 idle | BPS0201_000.GAP#main[0]，身体/羽翼轻微变化 | 81帧，0000–0080 |
| 施法 cast | BPS0201_000.GAP#main[5]，抬手、展翼、挥手、回待机 | 49帧，0000–0048 |

### 成品与验收

art/p5/fx/arsene包含166张30fps透明PNG：高1024，appear画布1437×1024、idle为875×1024、cast为1490×1024。同一动作按全帧alpha并集固定裁边，画布和投影锚点一致。各目录anchor.json记脚底地面参考和身体中心、相机、裁剪信息；脚底锚点为固定地面参考，并非随悬浮动作逐帧移动的脚部追踪。

Blender 4.5.9 LTS，后台EEVEE、正交相机、3/4朝画面右侧。原贴图乘Diffuse→ShaderToRGB→两阶恒定明暗；最终尺寸用alpha外扩3px合成黑轮廓。轮廓外alpha=0，四角透明检查通过，未用色底抠图。
arsene_still.png：cast第18帧，透明、高1536；contact_sheet.png：三段各抽八帧。没有把失败画面放入成品目录。

亚森组约73.42MB。模型、完整动作GLB、原始高质量渲染仍只留本机fx_work/arsene；未提交原始模型文件。

## 2. 原版技能特效贴图

按index.csv选择性提取BATTLE/SKILL中的837个BED/EPL（原始约528.30MB），加指定命中/枪击/召唤事件文件，从有效DDS头读取9326张原版贴图，Pillow解码错误0。全量完整PNG只在本机fx_work/textures/png；texture_catalog_local.csv记录包、路径、纹理名、DDS字节偏移、尺寸、alpha及本机PNG路径，local_only.csv列出未进仓库的全量目录。

仓库每组有原版PNG、深灰contact_sheet及sources.csv：

| 目录 | PNG数 | 来源/确认程度 |
|---|---:|---|
| gun | 43 | BES_GUN1–10；BES_GUN1为Joker手枪优先候选，但共用贴图不足以证明角色引用 |
| slash | 3 | BES_HIT_ZAN.BED，按指定来源确认 |
| blunt | 3 | BES_HIT_DA.BED，按指定来源确认 |
| summon_blueflame_candidates | 11 | 觉醒事件0282/PERSONA_A–D，含mask/flame序列、链条；最终蓝焰着色/时序未恢复 |
| curse_candidates | 155 | 暗色/紫色/红色相关贴图内容初筛，未确认具体Eiha/Eigaon/Maeiha |
| sukukaja_candidates | 34 | 上升光线/箭头形态初筛，未确认具体Sukukaja |

已检查BATTLE/TABLE/SKILL.TBL、SC.CPK的BATTLE/TABLE/NAME.TBL及现有展开表，未可靠解码技能名→ID。按任务允许的贴图内容筛选，明确使用candidates目录，不把候选假报为已确认技能。

BES_CRITICALG.BED已提取但没有有效DDS头，尚未导出其纹理；不能据此断言原版没有。GFS解析器对BES_GUN1、BES_HIT_ZAN、BES_CRITICALG均报 `Expected input to be '16', but it was '2'`，见particle_parser_probe.json，未修改解析器。
PERSONA_A–D的EPL可解析部分原始叶节点和动画轨道，另存*_raw_tracks.json；unknown/payload保留未知状态，不擅自解释。发射器语义/可重播粒子时间轴仍“读不出来”，各组particle_timeline.json明确标记。

## 3. 原版战斗GUI

检查BATTLE/GUI全部125项，72张有效DDS纹理已PNG化并保留原alpha；53项无DDS头（几何/脚本/其他格式），详见ui/battle_gui/container_audit.csv，未冒充它们均已PNG化。
四份既有SPD原坐标JSON及590张切图复制到ui/battle_gui/sprites，未修改原导出。sprite_comments.csv汇总原注释和坐标。

已确认DAMAGE_DATA.SPD切图：数字0–9为79–88，WEAK为94，CRITICAL为95，TECHNICAL为125；技能名斜框为120（原注释“スキル名土台”），121–124为揭开片段。P5_BATTLE_PARTYPANEL索引97–160含炎上、冻结、忘却、感电、眩晕、睡眠、混乱、恐惧等状态图标和底图。
独立1 MORE/MISS英文字样、增减益箭头尚未可靠定位。EIHA/SUKUKAJA技能名文字也未定位为独立贴图，不能将框或中文标签冒充目标字样。

## 4. 音效与日语语音候选

43条、11组，每类3–5条。audio/candidates.csv保留来源、流序号、原始流名、时长、用途推测、可信度及是否试听；audio/listen.html供逐条试听。

| 组 | 数量 | 可信依据 |
|---|---:|---|
| Eiha / Eigaon / Maeiha候选 | 各3 | 与贴图初筛编号对应SKILLSE；具体技能名未确认，低 |
| Sukukaja候选 | 3 | 同编号SKILLSE初筛，低 |
| 枪声 | 5 | SOUND_J SYSTEM的gun_00–04原始流名，中 |
| 斩击命中 | 3 | 原始damage_slash流名，中 |
| 挥空 | 4 | 原始player_attack流名，中 |
| 召唤 | 4 | 1条persona_flame原名支持；另3条EM0201同编号动作音低可信初筛 |
| Persona台词 / Arsene台词 / 攻击短喊初筛 | 各5 | 仅日语BP01，按流序号、时长筛选并去除完全相同副本；未试听转写，低 |

尚未确认「ペルソナ！」「アルセーヌ！」或具体攻击短喊。分组是试听目标，不是已确认台词标签。未混入英语BP01。
原库OGG直接复用，避免重复有损转码；新增EM0201通过vgmstream→WAV管道→libvorbis q4。43条均用FFprobe核验Vorbis及实际时长。C0001_051_B.DAT原有metadata_error，未当作可播放音频。

## 5. 未定位项与生图边界

以下均属“未确认/尚未解码”，不能证明原版不存在：具体四类技能ID/贴图/音效、Joker手枪角色引用、召唤蓝焰着色及时间线、暴击纹理、1 MORE/MISS、增减益箭头、日语目标台词。
没有经证实必须生图的项目；没有调用imagegen。若后续确认原版缺素材，会先列清楚请用户决定，不自行生成。

## 检查与体积

166张序列帧的数量、1024高度、RGBA、非空alpha、每段固定画布及透明四角通过；43条音频的OGG/Vorbis和时长通过。新增三个自有目录约106MB，最大单文件约2.46MB，低于300MB/20MB限制。完整本机库约12.3GB，低于15GiB。
verification.json保留检查计数。原版提取全部只读游戏文件，新增输出仅在指定仓库目录、本机资源库fx_work和当前聊天work/outputs。
