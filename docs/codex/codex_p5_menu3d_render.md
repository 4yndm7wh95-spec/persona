你在用户的 Windows 电脑上工作。任务：用本机已有的 GFD Studio（GFDLibrary）读 P5R 原版暂停菜单的 3D Joker 模型和它的全部菜单动作，在 Godot 4.5.1 里按原版的黑白样式渲成透明底 PNG 帧序列，进仓库。写报告。不改 mod 代码，不生图，不启动游戏。

**先读 docs/codex/README.md，按里面的长期规则来。**

【背景】
- P5Joker mod 的牌堆界面要还原 P5R 暂停菜单的入场：原版打开菜单时，Joker 先是白剪影，接着是**菜单专用的 3D Joker 模型**（黑白卡通着色：身体、头发全黑，脸和头发高光是白的）一边转身一边抬头，最后才定格成 2D 的白手 + 红手套图。SKILL / ITEM / EQUIP 子页切入时也有这个 3D Joker 的转身 / 侧脸动作。
- 参考：docs/p5/preview/p5r-20261121-223943-frames-02-12/ 里的 P5R 实机逐帧截图（2560×1440，约 27.5fps）：
  - 主菜单入场：frame_0049～0054（3.76～3.94 秒），3D 黑色 Joker 低头侧脸 → 抬头；
  - ITEM：frame_0178～0186（8.45～8.74 秒）；SKILL：frame_0109～0116（5.94～6.20 秒）；EQUIP：frame_0234～0241（10.48～10.74 秒）。
- 原版文件（仓库里已有只读副本）：
  - 模型：research/p5r/model/joker/source/MENU_C0001_001_A.GMD（贴图在 research/p5r/model/joker/joker_menu_Textures/：c0001_t01_face01UI.png、c0001_t01_kamenUI.png）；
  - 动作：research/p5r/model/joker/source/MENU_C0001_001_A.GAP（research/p5r/joker_menu_anim_list.csv 列了 43 段：main[0]～main[42] 是 0.03～0.67 秒的短片段，main[100]～main[113] 和 blend[1] 是长片段）。
  - 上一轮导出的 joker_menu.glb 骨骼 / 绑定是坏的（research/p5r/model/README_fix.md：只见零散小块）。**这次不要用那个 GLB**，直接用 GFDLibrary 从 GMD + GAP 读。
- 可参考的现成做法（同一套工具链，都是你以前做成功的）：tools/p5/native_cutin/（GFDLibrary 读 GMD → 烘焙 → Godot capture.gd 渲染）、tools/p5/allout_native/godot/capture.gd（重建原版网格、材质、节点 PRS 动画和相机）。

路径：仓库副本 <repo> = C:\Users\kkk\sts2_tools\p5_build\repo，分支 **claude/p5-pile-ui**。
工具：GFD Studio 0.10.3 在 C:\Users\kkk\sts2_tools\p5r_tools\GFDStudio.0.10.3（GFDLibrary.dll）；Godot 4.5.1 用你上次跑 native_cutin / allout 预览的那个。

## 1. 拉最新
   git -c safe.directory=* -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 fetch origin claude/p5-pile-ui:refs/remotes/origin/claude/p5-pile-ui
失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次。基于 origin/claude/p5-pile-ui 提交。
工作区有本地改动、切分支会被拒时：不要处理那些改动，用独立 worktree 只提交本任务的文件。
需要的目录：git -c safe.directory=* sparse-checkout add research/p5r docs tools/p5（可加 -c core.longpaths=true）。不要 sparse-checkout disable / set。

## 2. 解码（写新工具到 tools/p5/menu3d/，仿照 tools/p5/native_cutin 的 Export.csproj / Program.cs）
1. 用 GFDLibrary 读 MENU_C0001_001_A.GMD（ModelPack：节点、网格、蒙皮、材质、贴图名）和 MENU_C0001_001_A.GAP（AnimationPack）。确认动画数量和每段的 Duration / Speed，和 joker_menu_anim_list.csv 对上。读不全（ErrorsOccuredDuringLoad）就停下来报告。
2. 对每段动画，按 30fps 逐帧（含最后一帧）求骨骼姿势，**在导出器里做蒙皮**，把每帧的顶点位置烘焙出来（或者导出骨骼每帧世界矩阵 + 蒙皮权重，由 Godot 端做蒙皮，二选一，选你能做对的）。网格的 UV、材质（漫反射色、贴图名、透明 / 剔除标志）原样带上。
3. 模型里如果带相机节点或相机动画，原样导出；没有就写进报告。

## 3. 渲染（Godot 4.5.1，tools/p5/menu3d/capture.gd，仿照 native_cutin/capture.gd）
1. 着色按原版菜单的样子：**无光照（unshaded）**，颜色 = 材质漫反射色 × 贴图（face01UI / kamenUI 就是原版专门给菜单用的黑白贴图），保持原版的透明和背面剔除设置；不要自己加描边、阴影、灯光。
2. 背景**完全透明**（RGBA，人物外 alpha = 0），4×MSAA 或 2 倍超采样后缩小，边缘不要锯齿。输出 1920×1080。
3. 相机：
   - 模型 / 动画里有相机就用原版相机；
   - 没有的话，对照上面的参考帧对齐：主菜单入场用 frame_0052（3.87 秒，黑色 Joker 侧脸在画面右半边、头顶约在画面上缘、下巴约在画面 45% 高处）——把渲染结果半透明叠在参考帧上调到轮廓基本重合，记下相机参数（位置、朝向、FOV）写进报告；**所有片段用同一个相机**，不要每段单独调。
4. 渲染范围：main[0]～main[42] 和 blend[0] 全部逐帧（30fps）；长片段（main[100]～main[113]、blend[1]）只渲第一帧、中间一帧、最后一帧。
5. 输出到 <repo>\research\p5r\menu3d\：
   - 每段一个目录 main_0000\、main_0005\ … blend_0000\，里面 f0000.png、f0001.png …；
   - sheets\all_clips.png：每段一行（首帧 / 中间 / 末帧，深灰蓝底，左边写片段名和时长）；
   - sheets\match_<参考帧名>.png：对齐时用的叠图（渲染半透明叠在参考帧上），至少主菜单入场一张；另外**挑出你认为分别对应主菜单 / ITEM / SKILL / EQUIP 入场的片段**，各出一张和参考帧并排的对比图，并在 README 里写清楚是哪一段、依据是什么（拿不准就写拿不准）。
   - README.md（中文）：工具、命令、每段的时长和帧数、相机参数、着色规则、没还原的部分（例如原版是否另有后处理、轮廓线）。
6. 体积：单个文件不超过 20MB；本次新增到仓库超过 400MB 就停下来报告（可以先把 1920×1080 改成 1280×720 再试，并在报告里说明）。

## 4. 提交推送
- 只 add research/p5r/menu3d 和 tools/p5/menu3d；提交信息 "research: P5R menu 3D Joker (MENU_C0001) rendered from original GMD/GAP, all menu clips"。
- push：git -c safe.directory=* -c http.version=HTTP/1.1 -c http.lowSpeedLimit=0 -c http.lowSpeedTime=999999 push origin <你的提交>:claude/p5-pile-ui
  失败时分别等 5 / 10 / 20 / 40 秒重试，最多 4 次；被拒先 fetch 再 merge --no-edit（不 rebase）再 push。

## 5. 报告
中文：解码是否完整（动画段数、每段时长）；蒙皮 / 动作是否正确（有没有身体扭曲、飘块）；相机怎么来的；四个入场分别对应哪一段；新增文件数和大小；提交号。

【绝对不要做】
- 不要修改或删除仓库里已有的代码和资源，只新增 research/p5r/menu3d、tools/p5/menu3d 下的文件。
- 不要 rebase / reset / amend / stash；不要 --force；不要改全局 git config、系统环境变量 / PATH。
- 只用本机已有的 GFD Studio 0.10.3、Godot 4.5.1、Python、dotnet；不要安装或下载任何东西。
- 不要启动游戏（杀戮尖塔 2、P5R 都不要），不要用 Computer Use，不做实机验收，不生图。
- 游戏文件（CPK）只读，不要往游戏目录写任何东西；本任务用仓库里的副本就够，不需要再碰 CPK。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，不要读取或输出任何密码。
- 不要进入旧仓库 C:\Users\kkk\Documents\Codex\2026-10-12\claude-git-c-users-kkk-videos\work\persona-ref-upload。
- 每条命令单独执行；不要 Remove-Item -Recurse / -Force；不要删除任何目录或文件。
- 遇到上面没写到的情况、或任何命令出错，停下来写报告。
