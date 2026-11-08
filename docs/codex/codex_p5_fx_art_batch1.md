你在用户的 Windows 电脑上工作。任务：给 P5 Joker 第四版卡牌特效准备**需要细节的素材**——先从 P5R 原版里找，找不到再生图。
规划见 docs/p5/FX_V4_PLAN.md。不改 mod 代码，不编译，不启动游戏。

**先读 docs/codex/README.md**（长期规则：生图一律真透明底等）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，
**分支是 claude/cool-pascal-mil5i8**。如果上一个任务（codex_p5_sts2_vfx_extract.md）还在跑，等它推送完再开始，不要同时操作仓库。
P5R 资源库和按包内路径补解单个文件的方法见 research/p5r/README.md。

【总要求】
- 所有输出放 art/p5/fx/v4/<分组>/。每张图：交付版 PNG + 原始输出 *_raw.png + *_generation.json（提示词、参考图、尺寸、用的是原版还是生图）。
- 生图：**真透明底 RGBA**，轮廓外 alpha = 0，发光 / 火焰类边缘是柔和的半透明渐变，不要硬边，不要底色残留。
- 画风：P5 官方美术的赛璐璐风格（干净的黑色描边、红 / 黑 / 白为主、高对比），**不要 3D 渲染感、不要写实**。
  和已有 Joker 姿势图（art/p5/poses/final/idle_a3.png）放在一起不违和。
- 每组做完拼一张检查图 check_<分组>.png（深灰底 + 棋盘格底各一份，看透明边缘），自检条目写进该组 README.md。
- 每张图只生成一次；明显不合格（透明底失败、文字乱码、形状错）最多重画 2 次，还不行在报告里说明。

【1. 预告函 calling_card】（先找原版）
1. 在 research/p5r/index.csv 和本机资源库里找原版预告函贴图：关键词 YOKOKU、CALLING、CARD、予告；以及 CAMP/、EVENT/、CUTIN/、BATTLE/GUI/ 下名字像卡片的 DDS。
   找到就导出原尺寸 PNG，写清包内路径。
2. 原版没有可用的高清版本才生图：**正面 1024×1460**：红底，中间黑色礼帽笑脸怪盗团标志，四周拼贴风格（剪报字）的 “TAKE YOUR HEART” 和几行剪报字，边缘略旧；
   **背面 1024×1460**：红黑几何底纹 + 中间标志。参考 art/p5/fx/gen_codex/calling_card.png（旧的 220×320 版，只参考构图）。文字必须拼对，拼不对就去掉那行字。
3. 另出 card_edge_burn.png（512×512）：一圈烧焦的纸边 + 橙红余烬，中间透明（用来做预告函被烧掉的边）。

【2. 亚森 Arsène（生图，不用 3D 渲染）】
用户：Persona 不要再用原版模型三渲二，太别扭，要生图。
1. 先出**一张定稿草图**给用户确认（arsene_draft.png，竖构图，高 2048，透明底）：P5 官方设计的亚森——红色燕尾服、黑色高礼帽、鸟喙状的头 / 面具、
   巨大的黑色羽翼（内侧暗红）、高跟长靴、身上的锁链。姿势：双翼展开、一只手向前伸出施法。原版渲染 art/p5/fx/arsene/arsene_still.png **只当设计参考**。
2. **到这里停下来报告**，等用户确认草图后，下一轮再拆分层（身体 / 左翼 / 右翼 / 前伸的手臂 / 燕尾 / 锁链）。

【3. 八岐大蛇 Yamata-no-Orochi（群蛇形态用，生图）】
1. 先在 P5R 资源库里找八岐大蛇的模型或立绘（Persona 编号可从 research/p5r/index.csv 的 MODEL/CHARACTER/PERSONA/ 目录、或 Persona 名称表里查），
   找到就渲 / 导出一张参考图 orochi_ref.png（只当设计参考）。找不到就按 P5 官方设计生图。
2. 出一张定稿草图 orochi_draft.png（横构图，宽 2048，透明底）：八个蛇头从一团盘绕的身体里昂起，P5 赛璐璐风格。**同样停下等用户确认**，确认后再拆层（身体 + 每个蛇头单独一层）。

【4. 蓝焰面具 blue_flame_mask】
Joker 的白色怪盗眼罩形状（上挑的眼角、两侧上翘的尖角，正面），**只用一圈青蓝色火焰描出轮廓**，火舌往上窜、芯是亮白，中间透明。
- mask_flame.png（1024×640，正面）；
- flame_tongue_01~06.png（每张 256×512）：单独的青蓝火舌，6 种形状，底部宽、顶部尖、白芯蓝边，用来做粒子。

【5. 刀光 slash】（白芯 + 绯红边，手绘感，可以有少量墨色飞白）
每张 2048×1024，透明底：
- slash_thin.png：细长的弧形刀光（新月形，两头尖）
- slash_heavy.png：宽厚的弧形重斩
- slash_cross.png：X 形交叉斩
- slash_tear.png：一道撕开空间的横向裂口——裂口里是纯黑，边缘是锯齿状的红白发光
- slash_thrust.png：直线突刺的光痕（一头粗一头尖）
- slash_flurry.png：好几道交错的细刀光（连斩）

【6. 枪 muzzle】
- muzzle_side_01~03.png（1024×512）：侧面枪口火焰，星形尖刺，白黄芯 + 橙红外焰，3 种形状；
- muzzle_front.png（768×768）：正面的爆闪；
- shell.png（128×64）：一枚黄铜弹壳（粒子用）。

【7. 粒子贴图集】（每张 2048×2048，4×4 网格、每格 512，**每格一个独立的物体，不是动画**，格与格之间留透明边距）
- feathers_sheet.png：16 片黑色羽毛，角度 / 弯曲各不同，边缘有一点暗红反光；
- shards_sheet.png：16 片红 / 黑玻璃碎片，锐利，带一条高光边；
- sparks_sheet.png：16 种星芒 / 火花（四角星、细长火星、十字闪光），白芯红边。

【8. P5 冲击爆框 burst】
P5 标志性的尖刺漫画爆框（红色填充 + 粗黑描边 + 内部网点），用来垫在命中点后面：burst_01~03.png（1024×1024），三种尖刺形状。

【9. 输出与提交】
目录：art/p5/fx/v4/{calling_card,arsene,orochi,mask,slash,muzzle,particles,burst}/。总量不超过 120MB，单文件不超过 20MB。
只 add art/p5/fx/v4；提交信息 "art: P5 Joker FX v4 batch 1 (calling card, persona drafts, flame mask, slashes, muzzle, particle sheets, bursts)"。

【10. 报告】
中文：每组是原版还是生图（原版写包内路径）、重画了几次、检查图位置、哪些不满意、亚森和八岐大蛇的草图位置（**等用户确认**）、提交号。
