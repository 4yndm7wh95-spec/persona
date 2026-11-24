你在用户的 Windows 电脑上工作。任务：把菜单 3D Joker 的**逐帧蒙皮顶点数据**导出成紧凑的二进制文件提交进仓库，让 Claude 能在云端自己渲染、自己对相机（不再来回让你渲图）。不渲图、不改 mod 代码、不生图、不启动游戏。写简短报告。

**先读 docs/codex/README.md（遇到问题不要停下，按自己理解做完）。**路径、拉取、提交、推送同 docs/codex/codex_p5_menu3d_pages_v2.md（分支 claude/p5-pile-ui）。

## 为什么
用户逐帧对比后，SKILL 页的 3D 动作其实就是 main_0015 → main_0016 → main_0017（站着 → 抬起一条腿 → 往镜头压过来），错的是**机位**和**合成方式**。机位要对着原版视频一帧帧调，来回让你渲太慢，所以把数据给 Claude，Claude 在云端用 Godot 渲。

## 要导出的
用 tools/p5/menu3d 现有的 Export（GFDLibrary 读 research/p5r/model/joker/source/MENU_C0001_001_A.GMD / .GAP，导出器里做蒙皮——和 v2 完全同一套，服装、手套 051B、显示 / 隐藏规则照 research/p5r/menu3d_pages/README.md 那张表）：
1. **网格**：只要 v2 表里标「显示」的网格。输出 research/p5r/menu3d_data/meshes.json：每个网格 name、material、texture、color、cull、clip、clip_mode、draw、vertices（顶点数）、uv、triangles（和现在 scratch/meshes.json 同格式，只是少了隐藏的网格），再加一个 offset（这个网格的顶点在每帧数组里从第几个顶点开始）。
2. **逐帧顶点**：下面这些片段的**每一帧**（30fps，含最后一帧）：
   - main 组 index < 100 的全部片段（main_0000 … main_0042，时长为 0 的跳过）、blend_0000；
   - main_0113（整段，约 101 帧）。
   每个片段一个文件 research/p5r/menu3d_data/<片段名>.f16：**小端 float16**，按 [帧][顶点][xyz] 排，顶点顺序 = meshes.json 里网格的顺序和 offset；坐标就是现在 .bin 里写的那个模型空间坐标（不要再变换）。
3. research/p5r/menu3d_data/clips.json：每个片段 name、frames（帧数）、duration、speed、vertices（每帧顶点总数）。
4. **自检**：随便挑 main_0015 的 f0005，用 .f16 还原出来的顶点替换原来 .bin 的数据，用 capture.gd 渲一张，和用 .bin 渲的那张比，最大像素差写进报告（应该几乎为 0）。只写报告，不要提交这两张自检图。
5. 体积：单个文件不超过 20MB；总量写进报告。如果总量超过 120MB，把 main 组里**只有 2～3 帧**的片段去掉再算（main_0016、main_0017 必须保留）。

## 提交推送
- 只 add research/p5r/menu3d_data 和 tools/p5/menu3d（工具改动）；提交信息 "research: menu 3D Joker baked vertex data (float16, visible meshes, every frame) for cloud rendering"；推送到 claude/p5-pile-ui。

## 报告
中文、简短：每帧顶点数、片段数、总帧数、总大小、自检像素差、提交号。

【绝对不要做】同 docs/codex/codex_p5_menu3d_render.md 的清单（只新增 research/p5r/menu3d_data、只改 tools/p5/menu3d；不安装、不下载、不启动游戏、不用 Computer Use、不生图、不碰 CPK 和游戏目录、不读 obs.json 和密码、不进入旧仓库）。

遇到问题不要停下：没写到的情况、命令出错、结果拿不准，都按你自己的理解选最合理的做法继续，把任务做完，最后在报告里写清楚。
