你在用户的 Windows 电脑上工作。任务分两部分：A. 用 P5R 原版模型渲一套**姿势参考总览**（64 段战斗动作）；B. 生图出 3 张**画风候选**的全身定妆图给用户挑。不改 mod 代码，不启动游戏。

**先读**：docs/codex/README.md（长期规则、合并规则；特别是「生图一律真透明底」）、docs/p5/ART_SPEC.md（这个 mod 的决定）、research/p5r/model/diag/README_round3.md（上一轮的模型修复和动作套用方法）。
找仓库、拉取、提交推送、「绝对不要做」清单都和 docs/codex/codex_p5r_model_fix.md 一样，照那份执行。
art/cards 下另一个会话的已修改文件：原样保留，不提交、不 stash、不还原。

【背景】
- 下一个 mod：杀戮尖塔 2 的静默猎手换成 P5R 的雨宫莲（怪盗服、**不戴面具**）。用户决定：局内人物走生图路线（和 P3Makoto 一样：母版 → 每个关键姿势一张完整全身图）；P5R 原版 3D 模型和动作**只当姿势参考**，不直接进游戏。

【A. 姿势参考总览】
1. 模型：本机资源库 model\joker\fixed2\joker_thief_nomask.glb（上一轮生成的）。
2. 动作：BB0001_051.GAP 的 64 段（本机动作 GLB，清单 research\p5r\joker_battle_anim_list.csv）。**一律用上一轮的 C 方案：只套 rotation 通道，translation / scale 不套。**
3. 渲染：和上一轮同样的 Workbench 平光、透明底、正交相机；人物 3/4 朝画面右侧；**每一帧单独按人物的包围盒取景**（这次是给人看姿势，不要固定相机，人物在格子里居中、全身入画）。
4. 每段动作均匀取 6 帧（含首尾），每帧 256×384，横排成一条，条的左上角写「序号 原始名 帧数 时长秒」。8 段拼一张：research\p5r\pose\sheet_01.png … sheet_08.png。
5. 另出 research\p5r\pose\index.csv：序号、原始名、帧数、时长秒、本机动作 GLB 路径，以及你看图后的一句话描述（例如「站姿待机，身体轻微起伏」「右手持刀横斩」「举枪射击」「单膝跪地」），只写看得到的动作，看不出来就写「不确定」。
6. 某段渲出来明显扭曲 / 出框：照样放进总览，在 index.csv 的描述里标「异常」，不要单独修。

【B. 画风候选（生图）】
用 imagegen，以下面两张图为参考（图像编辑 / 参考图模式）：
- art/p5/ref/joker_nomask_ref.jpg：用户给的不戴面具的样子。**只表示造型**（不戴面具、发型、脸），不是指定画风。
- art/p5/ref/joker_thief_nomask_3d.png：原版 3D 模型渲的怪盗服，服装细节以它为准：黑色长款燕尾大衣（高立领、衣摆分叉到膝下）、深灰色马甲（胸前竖排纽扣、左胸口袋里一角白色口袋巾）、红色手套、深色长裤、棕色短靴；黑色蓬松卷发、灰色眼睛。**不戴面具，脸完整露出。**

通用要求（3 张都一样）：
- 尺寸 1024×1536 竖版，**真透明底 PNG**（RGBA，人物轮廓外 alpha = 0；工具出不了透明底时才用纯 #00FF00 绿幕，并在报告里说明）。
- 只有一个人物，**全身完整入画**（头顶到鞋底都在画面内，四周留白至少 60px）。
- 人物身体 **3/4 侧身朝向画面右侧**（游戏里玩家在左边，面朝右边的敌人）。
- 中性站姿（后面要拆姿势用）：离观众远的那只手插在大衣口袋里，离观众近的那只手臂自然下垂、和身体之间留出空隙；两腿微微分开，膝盖不交叠；表情从容自信，嘴角微微上扬。
- 手里不拿武器。
- 不要文字、水印、地面阴影、背景元素、光晕。

三个候选的画风（追加在角色描述后面），文件放 art/p5/concept/：
| 候选 | 文件名 | 画风 |
|---|---|---|
| A | style_A_p5r_official.png | Persona 5 Royal official character art style (Shigenori Soejima): flat cel shading with large solid black shadow shapes, sharp angular shadow edges, high contrast, thin clean ink lines, limited palette of black, red and white, pale skin |
| B | style_B_anime.png | clean modern anime cel shading like the Persona 5 animation and P5R in-game cut-in art: crisp thin outlines, two-tone shading with soft color, more color detail in the coat and vest |
| C | style_C_sts2_blend.png | hybrid: Persona 5 anime face and hair, but painted with Slay the Spire 2's thick dark ink outlines and painterly texture, muted palette with red accents |

每个候选生成 2 张（文件名加 _1 / _2），原样提交，不要裁剪不要抠图。

自检（报告里逐条写结果）：
- 轮廓外 alpha 是否全为 0、边缘有没有底色残留；
- 全身是否完整入画、留白够不够；
- 是否朝右 3/4；脸上**没有面具**；
- 服装：长大衣、灰马甲、口袋巾、红手套、棕靴是否都在；
- 两只手的手指数量和形状是否正常；
- 生图被安全策略拒绝的，写清楚是哪一张、拒绝原因，不要绕过。

另外拼一张 art/p5/concept/compare.png：6 张缩到 512×768 横排两行（A、B、C 各一列），每张下面写文件名，方便用户挑。

【提交】
分两次提交（只 add 对应目录）：
1. research/p5r/pose → "research: P5R Joker battle pose reference sheets (64 clips, rotation-only retarget)"
2. art/p5/concept → "art: P5 Joker style candidates A/B/C (no mask, facing right)"
仓库新增不超过 80MB。

【报告】
中文：A 部分渲了多少段、有几段异常、总览图位置；B 部分每张的自检结果、有没有被拒的；两个提交号。
