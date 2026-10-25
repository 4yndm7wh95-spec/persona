你在用户的 Windows 电脑上工作。任务：修好 P5R 原版模型的导出（之前 GFD Studio 导出的 GLB 人散成碎块），去掉 Joker 的面具，接上原版战斗动作，渲一批预览图给用户和 Claude 挑动作。不改 mod 代码，不生图，不启动游戏。

**先读 docs/codex/README.md，按里面的长期规则和「和 Claude 同时改代码」的合并规则来。**
**再读 research/p5r/README.md**：上一个 Codex 会话扒 P5R 资源时写的，里面有游戏位置、用过的工具（都在 C:\Users\kkk\sts2_tools\p5r_tools\）、本机资源库 C:\Users\kkk\sts2_tools\p5r_rip\ 的目录结构、按包内路径补解单个文件的命令。下文说的「本机资源库」「上一轮导出的动作」都以那份 README 和 research\p5r\local_only.csv 为准。

【背景】
- 下一个 mod：杀戮尖塔 2 的铁甲战士换成 P5R 的雨宫莲（怪盗服，**不戴面具**）。局内人物走「3D 渲 2D」：用原版模型 + 原版动作离线渲成 2D 帧，游戏里只放图片。
- 你上一轮导出的 research/p5r/model/*/*.glb 人是碎的。Claude 查过原因：顶点本身没问题（模型空间，Y 轴朝上，单位 cm），是 GFD Studio 把网格节点的变换错写进了蒙皮的逆绑定矩阵，没蒙皮的头发 / 面具 / 眼睛又被多变换了一次。修复脚本已经写好：tools/p5r/fix_gfd_glb.py（Claude 在云端验证过：怪盗服、学生服的身体修好后 T 姿势正常）。
- 亚森（Arsène）的坏法不一样，这个脚本修不好，单独处理（第 4 步）。

【1. 找到仓库并拉取】
1. 在本机找到满足以下条件的 git 仓库（用你上次成功推送的那个），记作 <repo>：remote 指向 github.com/4yndm7wh95-spec/persona，仓库里有 mod\P3Makoto.json。
2. 所有 git 命令都加 -c safe.directory=<repo>。
3. git -c safe.directory=<repo> status -sb：有进行中的合并 / rebase 就停下来报告；你在别的任务里改了没提交的先提交；说不清来源的改动停下来报告。
4. fetch：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/amazing-darwin-b00arn（失败等 5 / 10 / 20 / 40 秒重试，最多 4 次）。
5. 合并：先 merge --ff-only origin/claude/amazing-darwin-b00arn；不能快进改用 git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local merge --no-edit origin/claude/amazing-darwin-b00arn；有冲突就停下来报告。
6. 确认 tools\p5r\fix_gfd_glb.py 存在。稀疏检出缺目录时允许 git -c safe.directory=<repo> sparse-checkout add tools/p5r research/p5r（可加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

【2. 修 Joker 模型】
用本机 Python（需要 numpy；没有就 pip install --user numpy）在 <repo> 下执行，输出放 research\p5r\model\joker\fixed\：
1. python tools\p5r\fix_gfd_glb.py research\p5r\model\joker\joker_thief.glb research\p5r\model\joker\fixed\joker_thief_nomask.glb --drop kamen
2. python tools\p5r\fix_gfd_glb.py research\p5r\model\joker\joker_thief.glb research\p5r\model\joker\fixed\joker_thief.glb
3. python tools\p5r\fix_gfd_glb.py research\p5r\model\joker\joker_student.glb research\p5r\model\joker\fixed\joker_student.glb
4. python tools\p5r\fix_gfd_glb.py research\p5r\model\joker\joker_menu.glb research\p5r\model\joker\fixed\joker_menu.glb
原来的 GLB 不要删、不要改。脚本报错就把完整输出贴进报告，不要自己改脚本。

【3. 渲染检查（用 Blender）】
1. 本机没有 Blender 的话，允许从 blender.org 官方下载 Blender 4.x 的 **便携 zip 版**，解到 C:\Users\kkk\sts2_tools\p5r_tools\blender\（不运行安装程序，不要管理员权限）。用命令行后台模式跑 Python 脚本（blender -b --python ...），脚本放 C:\Users\kkk\sts2_tools\p5r_tools\scripts\。
2. 统一的渲染设置（后面所有图都一样）：
   - 透明背景 PNG，1024×1536 竖版；
   - 正交相机；人物**面朝画面右侧的 3/4 侧身**（游戏里玩家在左、敌人在右），全身入画，脚底离下缘约 60px；
   - 着色先用 Blender 的 Workbench 或 EEVEE 平光（贴图原色 + 简单明暗），不要加描边、辉光、景深；
   - 每张图左上角写文件名或动作名（白字黑边）。
3. 静态检查图，放 research\p5r\model\joker\fixed\check\：四个修好的 GLB 各一张 T 姿势（正面一张、3/4 侧面一张）。要求：头发、眼睛、手套、外套下摆都在身上，没有飘在旁边的碎块；nomask 版脸上没有面具。哪里不对就截图写进报告，不要自己改模型。

【4. 亚森】
1. 先同样跑一次：python tools\p5r\fix_gfd_glb.py research\p5r\model\arsene\arsene.glb research\p5r\model\arsene\fixed\arsene.glb，渲一张 T 姿势检查图。
2. 如果还是碎的：改用别的导出路线重新从原版 GMD 导，任选一条能用的：
   - GFD Studio 导出 FBX / DAE（不是 GLB），再用 Blender 导入后另存 GLB；
   - 或者 Blender + GFS / GMD 导入插件（例如 Pherakki 的 GFS-Blender-Tools，从 GitHub 官方 release 下载）直接导入原版 GMD，再导出 GLB。
   成品放 research\p5r\model\arsene\fixed\，同样渲检查图。两条都不行就停在这里，报告卡在哪一步。

【5. 接上战斗动作，渲动作总览】
1. 动作来源：本机资源库里已经导出的 Joker 战斗动作 GLB（位置见 research\p5r\README.md 和 local_only.csv）（joker_battle_anim_list.csv 里 original_name 以 BB0001_051.GAP 开头的那 64 段，按 CSV 的 file 列找文件）。这些动作 GLB 只带骨架和动画，骨骼名和模型一致：在 Blender 里把动作套到 joker_thief_nomask.glb 的骨架上播放（按骨骼名对应）。
   - 动作文件也可能有同样的矩阵问题：先拿一段渲出来看，人物姿势明显扭曲 / 翻转就停下来报告（附截图），不要硬渲 64 段。
2. 动作总览，放 research\p5r\model\joker\anim_preview\：
   - 每段动作一张横条图：均匀取 6 帧（含第一帧和最后一帧），每帧缩到 256×384 横排，条图左上角写「序号 + 原始名 + 帧数」；
   - 所有横条再拼成总览 sheet_01.png、sheet_02.png……（每张 8 段），方便一眼挑动作。
3. 另外把同一个 GAP 里的其他代码（BB0001 开头但不是 051 的、名字像战斗待机 / 攻击 / 枪击 / 技能 / 召唤 / 受击 / 倒地 / 胜利的）也列进 anim_preview\README.md 的表格（原始名、帧数、时长），**不用渲**，以后要再挑。

【6. README 和体积】
research\p5r\model\README_fix.md（中文）：每个修好的文件、用的命令、检查结果；Blender 版本和下载地址；动作套用的方法（按骨骼名对应的脚本思路）；没做成的事和原因。
仓库里单个文件不超过 20MB，research\p5r\model 新增总量不超过 150MB；超了就把 PNG 存成 JPG（质量 90，透明图除外）或只留在本机资源库，在 README 里列出路径。

【7. 提交与推送】
1. 只 add：git -c safe.directory=<repo> add research/p5r/model
2. git -c safe.directory=<repo> -c user.name=Codex -c user.email=codex@local commit -m "research: P5R models fixed (Joker no-mask, Arsene), battle anim preview sheets"
3. push：git -c safe.directory=<repo> -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin claude/amazing-darwin-b00arn（失败等 5 / 10 / 20 / 40 秒重试；被拒先 fetch 再 merge --no-edit 后重新 push；不要 rebase，不要 --force）。
4. 中文报告：四个 Joker 模型和亚森的检查结果（附检查图路径）、动作套用有没有问题、渲了几段动作、提交号。

【绝对不要做】
- 不要修改或删除代码和已有资源（包括原来的 GLB 和 tools\p5r\fix_gfd_glb.py），只新增上面说的文件。
- 不要 rebase / reset / amend；不要 --force；不要改全局 git config。
- 不要启动游戏（杀戮尖塔 2、P3R、P5R 都不要启动），也不要用 Computer Use。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码；解密密钥 / 配置不要写进仓库、README 或报告。
- 不要修改 P3R、P5R 或杀戮尖塔 2 的游戏原文件。
- 除第 3、4 步说的 Blender 便携版和 GFS 导入插件外，不要安装别的东西；不要运行安装程序，不要用管理员权限。
- 不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- 不要 sparse-checkout disable / set。
- 不要和其他会话同时操作这个仓库。
- 遇到上面没写到的情况，停下来报告。
