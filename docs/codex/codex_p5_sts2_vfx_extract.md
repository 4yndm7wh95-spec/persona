你在用户的 Windows 电脑上工作。任务：从杀戮尖塔 2 的游戏 PCK 里**只读地**解出原版特效（场景、材质、着色器、贴图），放进仓库 research/sts2_vfx/，
给 Claude 做 P5 Joker 第四版卡牌特效用（规划见 docs/p5/FX_V4_PLAN.md）。不改 mod 代码，不生图，不启动游戏，不编译。

**先读 docs/codex/README.md**，按里面的长期规则执行。
找仓库、提交推送的 git 参数、重试方式、「绝对不要做」清单都和 docs/codex/codex_p5r_model_fix.md 一样，照那份执行，**只有分支不同**：
本任务的分支是 **claude/cool-pascal-mil5i8**（fetch / merge / push 都用这个分支名；本地没有就从 origin/claude/cool-pascal-mil5i8 建）。

【背景】
Claude 要把 Joker 的特效改成和原版同一套做法：直接实例化原版的 res://scenes/vfx/... 场景（换颜色），再用原版的 flipbook / erosion / polar 着色器
搭自己的特效。需要读到这些场景的 .tscn 文本、材质 .tres、着色器源码和贴图，才能在云端 Godot 里离线预览、写代码。
之前 research/sts2_architect/ 用同样的方式解过一次（raw/ 原样 + recovered/ 还原 + LIST.csv），这次照那个格式做。

【1. 要解的范围】（游戏 PCK 只读，用你本机的 gdre_tools，版本和解 sts2_architect 时一样）
1. 以下目录下的全部文件：
   - res://scenes/vfx/
   - res://materials/vfx/
   - res://shaders/vfx/（含 _util/ 下的 .gdshaderinc）
   - res://images/vfx/
   - res://images/packed/vfx/
   - res://animations/vfx/
   - res://shaders/ 根目录下的 .gdshader（不含子目录以外的其他东西；子目录 vfx 已在上面）
2. 依赖补全：上面所有 .tscn / .tres 里 ext_resource 引用到、但不在上面目录里的资源（贴图、材质、着色器、曲线、渐变、spine 数据等），
   递归补全，直到没有缺的。脚本（.cs / .gd）不用解，只在清单里记下路径。
3. 不要解：音频、字体、本地化、整个 PCK。

【2. 输出】放 research/sts2_vfx/
- raw/：PCK 里的原始文件，路径照 res:// 原样（含 .godot/imported/ 下对应的 .ctex 等）。
- recovered/：gdre 还原后的可读版本：.tscn / .tres 文本、.gdshader / .gdshaderinc 源码、贴图转成 PNG（保留 alpha，不缩放、不改色）。
- LIST.csv：和 research/sts2_architect/LIST.csv 同样的列（文件名、原路径、类型、大小、说明）。依赖补全进来的文件，说明里写「依赖：被 <哪个场景> 引用」。
- SCENES.csv：每个 res://scenes/vfx/**/*.tscn 一行：场景路径、根节点类型、根节点脚本路径（没有就空）、GPUParticles2D / CPUParticles2D / Sprite2D / AnimatedSprite2D / 其他节点各几个、
  引用的贴图数、引用的着色器路径（分号分隔）。直接从 .tscn 文本统计，不用打开 Godot 编辑器。
- contact_sheet_*.png：recovered 里的贴图按目录各拼一张联系表（深灰底、每格 256px、格下写文件名），方便 Claude 先看一眼有什么。
- README.md（中文）：用的工具和命令、解出多少文件、多大、有没有解失败的（列出路径和报错）、体积超限时哪些留在本机。

【3. 体积】
research/sts2_vfx/ 总量不超过 250MB，单文件不超过 20MB。超了的话：raw/ 里的大 .ctex 先留本机（recovered/ 里的 PNG 照样提交），在 README 和 LIST.csv 的说明里写「仅本机：<本机路径>」。
还超就停下来报告，不要自己删减 recovered/。

【4. 提交与推送】
只 add research/sts2_vfx；提交信息 "research: Slay the Spire 2 vanilla VFX scenes, materials, shaders and textures (read-only extract)"。
推送到 claude/cool-pascal-mil5i8，规则照 docs/codex/codex_p5r_model_fix.md 第 7 节。

【5. 报告】
中文：解出的文件数和体积、SCENES.csv 里场景总数、依赖补全补了哪些目录、失败项、留在本机的文件、提交号。

【绝对不要做】
照 docs/codex/codex_p5r_model_fix.md 的「绝对不要做」清单，另外：
- 不要修改游戏目录下的任何文件（PCK 只读）；输出只写到仓库的 research/sts2_vfx/ 和你的临时目录。
- 不要编译、不要安装 mod、不要启动游戏或 Godot 编辑器的游戏工程。
