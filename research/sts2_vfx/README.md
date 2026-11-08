# 杀戮尖塔 2 原版特效：只读提取

## 来源与交付

- 原版 PCK：`C:\Users\kkk\Desktop\slaythespire\Slay the Spire 2\SlayTheSpire2.pck`（1,990,705,700 字节）。只读提取，未改游戏文件。
- 工具：`C:/Users/kkk/sts2_tools/gdre/gdre_tools.exe`，Godot RE Tools **2.7.3**，沿用本机既有版本；没有安装工具。
- raw/：1615 个原包文件，71,244,355 字节；保持res://相对路径及原始字节，含对应.godot/imported数据。
- recovered/：1102 个可读/恢复资源，37,286,678 字节；其中PNG 485张、Spine atlas 14个、skel 14个，其余为原样文本场景/材质/着色器。
- SCENES.csv：**312 个 scenes/vfx 场景**。另含2个递归依赖场景，但不混入该表的VFX场景总数。
- contact_sheet_*.png：149 张，按PNG所在目录各一张；深灰底#202028，每格图像区256×256，格下48px文件名区，缩略图仅用于预览。recovered原PNG不缩放、不改色。

## 范围和依赖

- 范围：scenes/vfx、materials/vfx、shaders/vfx（含_util）、images/vfx、images/packed/vfx、animations/vfx全部包内文件，以及shaders根目录的.gdshader。
- 初选1055个原包文件，递归补充560个文件（包含导入映射与压缩数据）。恢复Spine atlas后额外补齐rockstone.png及the_insatiable.png两张图集页及各自导入数据。
- 已检查全部恢复文本的res://路径、相对shader include和Spine atlas图集页；**非排除范围的缺失依赖为0**。
- 补齐的目录：
  - `res://animations/monsters/decimillipede/`
  - `res://animations/monsters/the_insatiable/`
  - `res://images/atlases/`
  - `res://images/atlases/ui_atlas.sprites/card/`
  - `res://images/enchantments/`
  - `res://images/events/`
  - `res://images/monsters/phobia_mode/`
  - `res://images/packed/`
  - `res://images/packed/map/icons/`
  - `res://images/packed/timeline/`
  - `res://images/potions/`
  - `res://images/powers/`
  - `res://images/rooms/hive/`
  - `res://images/rooms/hive/the_insatiable/`
  - `res://images/timeline/`
  - `res://images/timeline/epoch_portraits/`
  - `res://images/timeline/ui/`
  - `res://images/ui/combat/`
  - `res://images/ui/rest_site/`
  - `res://models/vfx/vfx_power_up/`
  - `res://scenes/creature_visuals/`
  - `res://scenes/ui/`
  - `res://shaders/`
  - `res://themes/`

- 按要求只记录137条脚本路径，不解出脚本。3条字体路径按“不要解字体”排除；未解音频或本地化。保留了引用字体的材质/主题文本，离线预览需由Claude提供字体或替换引用。
- LIST.csv保持与sts2_architect一致的五列：文件名、原路径、类型、大小、说明。文件大小单位为字节；脚本/排除项文件名和大小留空。依赖说明列给出引用它的场景；无场景使用者时列出实际引用资源，避免虚构场景。

## 工具命令与方法

1. `gdre_tools.exe --headless --list-files=<PCK>`：只列目录，不解整个包。
2. `gdre_tools.exe --headless --extract=<PCK> --output=<临时raw> --include=res://scenes/vfx/** ... --include=res://shaders/*.gdshader`：按指定目录提取。
3. 从tscn/tres、shader include和.import文本递归追踪路径；后续以多个精确`--include=res://...`分批补解，单次命令长度控制在Windows限制内。
4. `gdre_tools.exe --headless --recover=<已选raw目录> --output=<临时recovered> --skip-loading-resource-strings`：只恢复已提取的子集。Spine图集页补解后再单独恢复新增2张纹理。
5. 场景/材质/着色器文本与raw逐字节一致；恢复PNG按GDRE输出逐字节复制，保留RGBA/RGB/L/LA原色模式及Alpha，没有额外压缩损失、缩放或改色。
6. 完整参数和计数见EXTRACTION.json，纹理尺寸与Alpha范围见TEXTURE_INFO.csv；临时日志和完整工具输出留在`C:/Users/kkk/sts2_tools/tmp/sts2_vfx_20261102`。

## SCENES.csv统计口径

- 节点数量直接统计每个tscn文本中声明的node；未展开子场景内部节点。没有type的PackedScene实例计入“其他节点”。
- 根节点类型/脚本可沿根PackedScene实例文本继承查找，不运行脚本、不实例化场景。
- “引用的贴图数”为场景直接ext_resource贴图数；“依赖贴图数”为递归去重后的PNG数。
- 着色器路径递归追踪外部材质和子场景；include路径单列，路径用分号分隔。

## 检查、失败和限制

- GDRE恢复报告：初批511项、补充2项，**513项成功，0项转换失败，0项未转换**。所有选中的1615个原包资源都提取成功，未忽略校验错误。
- GDRE报告103项Lossy：源导入贴图使用有损/VRAM压缩。这里导出其已解码像素为PNG，没有再主动有损压缩；不能从有损导入数据还原压缩前的原始像素。
- 资源子集没有project.godot，因此GDRE有“project config not loaded”上下文错误/警告，并无法推断完整工程版本（工具提示4.0.0）。这不是游戏版本结论；没有运行或恢复游戏工程，文件转换结果为0失败。工具自身日志读取警告也保留在本机日志，不把日志当资源提交。
- PNG全部可读；485张均已与CTEX逻辑尺寸对照，其中23张GDRE解码输出与逻辑尺寸有1～3px块对齐差异。严格保留GDRE输出，未为凑尺寸裁切或缩放；需要精确像素区域时同时看CTEX原始数据和TEXTURE_INFO.csv。具体如下：

| 文件 | CTEX逻辑尺寸 | GDRE PNG尺寸 |
|---|---|---|
| `animations/vfx/vfx_adrenaline/adrenaline.png` | 2010×1322 | 2012×1324 |
| `images/events/mirror_mask3.png` | 971×1024 | 972×1024 |
| `images/monsters/phobia_mode/the_insatiable_phobia.png` | 1064×1219 | 1064×1220 |
| `images/packed/timeline/epoch_slot_locked_small.png` | 392×250 | 392×252 |
| `images/packed/vfx/generic/dark_overlay_events.png` | 360×169 | 360×172 |
| `images/packed/vfx/generic/light_line.png` | 438×920 | 440×920 |
| `images/packed/vfx/generic/sparkle.png` | 119×120 | 120×120 |
| `images/packed/vfx/generic/square_speck.png` | 22×31 | 24×32 |
| `images/packed/vfx/generic/twinkle_star.png` | 37×37 | 40×40 |
| `images/vfx/crystal_sphere_outline.png` | 1521×1521 | 1524×1524 |
| `images/vfx/epoch_offscreen_indicator.png` | 263×333 | 264×336 |
| `images/vfx/epoch_offscreen_indicator2.png` | 232×333 | 232×336 |
| `images/vfx/fog_sheet_2.png` | 250×700 | 252×700 |
| `images/vfx/ground_spike.png` | 107×116 | 108×116 |
| `images/vfx/miasma_cloud.png` | 133×115 | 136×116 |
| `images/vfx/monsters/decimillipede/decimillipede_blue_flipflake.png` | 2000×250 | 2000×252 |
| `images/vfx/monsters/decimillipede/decimillipede_flipflake.png` | 2000×250 | 2000×252 |
| `images/vfx/monsters/devoted_sculptor/devoted_sculptor_spark_particle.png` | 36×35 | 36×36 |
| `images/vfx/monsters/devoted_sculptor/devoted_sculptor_voice_particle.png` | 28×179 | 28×180 |
| `images/vfx/short_rice_no_glow_particle.png` | 8×14 | 8×16 |
| `images/vfx/star1.png` | 69×79 | 72×80 |
| `images/vfx/vfx_constant_fire/fire_spark.png` | 50×100 | 52×100 |
| `images/vfx/water_texture.png` | 600×634 | 600×636 |

- 贴图保留原Alpha；没有Alpha的原始遮罩/颜色图保留RGB或L模式，不擅自抠底。联系表仅合成在深灰底上，原PNG没有改动。
- 不包括代码/脚本、音频、字体、本地化；原版场景里仍保留这些路径引用，云端预览要按项目需要移除/替换脚本或字体，不能把本目录当成可直接运行的完整游戏工程。

## 体积与仅本机文件

- 总量：117,113,662字节（117.11MB）；单文件最大：8,197,204字节（`raw/.godot/imported/fog_sheet_1.png-f71c8f1f7981f5f44ae2de280b3b8bd2.bptc.ctex`）。上限按十进制250MB/20MB检查。
- 仅本机原包大文件：无，所有所需raw资源均低于20MB，且总量低于250MB。

## 安全范围

- 本任务只新增research/sts2_vfx；未改mod代码，未编译、安装mod、启动游戏或Godot编辑器游戏工程。
- PCK只读，提取前后大小和修改时间一致；没有读取obs.json、密码或密钥，没有访问/编辑P3Makoto图片。
- 使用目标分支claude/cool-pascal-mil5i8的独立工作目录，原副本其他会话文件保持不动。
