你在用户的 Windows 电脑上工作。任务：从杀戮尖塔 2 的游戏 PCK 里**只读地**解出一个战斗背景和几只怪物的骨骼动画，放进仓库 research/sts2_preview_scene/，
给 Claude 的云端特效预览当真实战斗画面用（现在预览里是纯色背景 + 紫色圆团，红黑冲击帧、命中颜色都调不准）。不改 mod 代码，不生图，不启动游戏，不编译。

**先读 docs/codex/README.md**。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/cool-pascal-mil5i8**。
解法和格式照上次的 research/sts2_vfx/（docs/codex/codex_p5_sts2_vfx_extract.md）：gdre_tools，raw/ 原样 + recovered/ 还原 + LIST.csv。

【网络】
你那边拉大文件容易超时。开始前 fetch / merge 一次；**推送前如果又要合并、而合并要下载的东西超时**，就不要再合并，
直接把本地提交推到新分支：git push origin <提交号>:refs/heads/codex/p5-preview-scene，然后报告分支名，Claude 自己合并。

【1. 要解的】
1. 第一幕的一个普通战斗背景：res://scenes/backgrounds/overgrowth/ 下的全部文件（场景、贴图、spine、材质、着色器），以及这些 .tscn / .tres 引用到的依赖（递归补全）。
2. 怪物骨骼动画：res://animations/monsters/ 下这几个目录的全部文件：cultists、chomper、fat_gremlin、inklet（不存在的跳过，换同目录下另外任意一只体型中等的，写进报告）。
   以及 res://scenes/creature_visuals/ 下这几只怪物对应的场景（文件名和怪物名对应的那几个 .tscn）和它们的依赖。
3. 不要解：音频、字体、本地化、整个 PCK、别的背景和怪物。

【2. 输出】research/sts2_preview_scene/
- raw/、recovered/（spine 的 .skel / .atlas / .png 还原成可读文件；贴图转 PNG，保留 alpha，不缩放）、LIST.csv（列同 research/sts2_vfx/LIST.csv）。
- contact_sheet.png：所有还原出来的 PNG 一张联系表（深灰底，每格 256px，格下写文件名）。
- README.md（中文）：工具和命令、文件数和体积、每只怪物用的是哪个目录、失败项。
- 总量不超过 80MB，单文件不超过 20MB；超了先停下报告。

【3. 提交】只 add research/sts2_preview_scene；提交信息 "research: Slay the Spire 2 overgrowth background and a few monster rigs for FX previews (read-only extract)"。

【4. 报告】中文：文件数、体积、四只怪物分别是什么、失败项、提交号或推送到的分支名。

【绝对不要做】照 docs/codex/codex_p5r_model_fix.md 的清单；游戏目录只读；不编译、不安装、不启动游戏。
