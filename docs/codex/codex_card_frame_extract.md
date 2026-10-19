你在用户的 Windows 电脑上工作。任务：从用户之前解包好的杀戮尖塔 2 目录里，把卡牌卡框相关的原版贴图、材质、场景复制进仓库，并把图集里的每个卡框小图裁成单独的 PNG。只复制和裁图，不改代码，不生图。
（这是卡框重绘的第一步：Claude 要按这些原图的准确形状和尺寸做模板，下一步再让 imagegen 画 P3R 塔罗风格的新卡框。）

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功推送的那个），记作 <repo>：
   - remote 指向 github.com/4yndm7wh95-spec/persona；
   - 仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. 执行 git -c safe.directory=<repo> status -sb。有进行中的合并或已跟踪文件的未提交改动就停下来报告；未跟踪的 art/treasure_hands/、docs/qa/v0.14.69/、mod/_build/ 原样保留、忽略它们继续（不要移动、删除或提交）。
4. fetch：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn
   失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
5. 合并：先执行
   git -c safe.directory=<repo> merge --ff-only origin/claude/amazing-darwin-b00arn
   不能快进时改用
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn
   有冲突就停下来报告。

【2. 源目录】
- 解包目录 <EXT> = C:\Users\kkk\sts2_extract（research\REPORT.md 里记录的那次 GDRE 解包，只读）。
- 不存在、或者里面没有 images\atlases\ui_atlas_0.png 就停下来报告（不要自己重新解包）。

【3. 复制到 <repo>\research\card_frame_src\（保持 <EXT> 下的相对路径）】
1. 图集：images\atlases\ui_atlas_0.png、ui_atlas_1.png（以及同目录下其他 ui_atlas_*.png，如果有）。
2. 图集里的卡牌小图定义：images\atlases\ui_atlas.sprites\card\ 下全部 *.tres。
3. 其他卡框贴图：images\ui\cards\ 下全部 png；images\packed\card_template\ 下全部文件（card_frame_sdf.exr 如果超过 5MB 就跳过，在报告里说明）。
4. 材质：materials\cards\banners\*.tres、materials\cards\frames\*.tres。
5. 着色器：shaders\hsv.gdshader、shaders\colorize_addblend.tres，以及上面材质 .tres 里引用到的其他 .gdshader / .tres（打开材质文件看 ext_resource 的 path，一并复制）。
6. 场景：scenes\cards\card.tscn、tiny_card.tscn、upgrade_preview.tscn，以及 scenes\cards\ 下的 *_material.tres。
7. card.tscn 里 ext_resource 引用到的其他贴图（路径以 res:// 开头的 png / tres），只要是卡框、横幅、标签、能量图标相关的也一并复制（跳过角色、卡图）。

【4. 裁图】
写一个 Python 脚本 <repo>\tools\ui\crop_card_sprites.py（只用 Pillow 和标准库）：
- 读取 research\card_frame_src\images\atlases\ui_atlas.sprites\card\*.tres，解析其中的 atlas 路径和 region = Rect2(x, y, w, h)（还有 margin 的话也记下来）；
- 从对应的 ui_atlas_*.png 里裁出这块区域，存成 research\card_frame_src\sprites\<文件名>.png（RGBA，保留原透明度）；
- 输出 research\card_frame_src\sprites\index.csv：文件名、图集、x、y、w、h、margin。
运行它，并做一张预览图 research\card_frame_src\sprites\preview.png：所有裁出的小图排在深灰背景上，每张下面写文件名和尺寸。

【5. 说明文件】
research\card_frame_src\README.md（中文）：复制了哪些文件、从哪来、裁图脚本怎么用、有没有缺失或跳过的文件。

【6. 提交与推送】
1. 只 add：
   git -c safe.directory=<repo> add research/card_frame_src tools/ui/crop_card_sprites.py
2. 提交：
   git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "research: original card frame sprites, materials and card scene (for the P3R tarot frame redraw)"
3. push：
   git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn
   - 失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。
   - 被拒绝（远端有新提交）时先 fetch，再 merge --no-edit（同上面的写法）后重新 push。不要 rebase，也不要 --force。
4. 用中文报告：复制了多少文件、裁出了哪些小图（名字和尺寸）、提交号、遇到的问题。

【绝对不要做】
- 不要修改或删除代码和已有资源，只新增上面说的文件。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2 和 P3R 都不要启动）。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R 或杀戮尖塔 2 的游戏原文件和 <EXT> 解包目录（只读）。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护（这次不需要生图）。
- 不要 sparse-checkout disable / set（需要时可以 sparse-checkout add research/card_frame_src tools/ui；单次命令可加 -c core.longpaths=true）。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
