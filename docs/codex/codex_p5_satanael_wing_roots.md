你在用户的 Windows 电脑上工作。任务：**撒旦耶尔第 10 版翅膀的翼根加一层黑色羽毛**（原版的样子），六片大翼本身保留。不改 mod 代码，不编译，不启动游戏，不用 Computer Use。

**先读 docs/codex/README.md**（生图一律真透明底）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/epic-archimedes-m9an69**
（先 fetch origin claude/epic-archimedes-m9an69:refs/remotes/origin/claude/epic-archimedes-m9an69，再 merge --ff-only）。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-satanael-wing-roots，报告分支名。

**上一份任务单（codex_p5_satanael_feather_wings.md）作废**：它把六片翼整个画成了鸟类羽毛翅膀，用户说「原版不是这样的六片翼」。如果你已经按它出了图，**不要提交**那些图。

【原版到底是什么样（用户 2026-11-09 要求仔细对原版）】
看 model_ref/turn_front_cel.png、turn_back_cel.png、ref_wing_root_back.png（背面翼根放大）和 ref_feather_wings.png（原版切入镜头）：
- **六片大翼还是硬的蝙蝠 / 镰刀形翼**：几根细长的翼骨（像手指）撑开，翼骨之间是翼膜，后缘是一段段往里凹的弧，翼尖锋利；翼膜上有金色骷髅大图案和星星（这些就是现在 layers_v10 画的，**结构是对的，不要改**）。
- **羽毛只在翼根**：两边肩胛 / 背上、六片翼汇到身体的地方，是**一大簇层层叠叠的黑色羽毛**——参差的、末端撕裂状的尖羽，一层压一层往外散开，盖住每片翼的根部（翼的前三分之一左右），羽毛下面透出一点金色。ref_wing_root_back.png 里背上那一片就是。
- 原版切入镜头里看起来「全是羽毛、蓝色」，是因为：镜头离得近、主要拍到翼根这簇羽毛；整个场景是蓝光，金色被照成了深蓝；翼骨的边缘被打出一道道**青色亮线**（画面里那些细长的发光条）。这些颜色游戏里 Claude 用着色器做（压金色、加青色边光），**你不用画蓝色**。

【要做的】
1. 新画一层 **wing_root_feathers.png**（同画布 5400×3000、同坐标、透明底）：两侧翼根的黑色羽毛簇。
   - 位置：以 layers.json 的 wing_far_root (2400,1400)、wing_near_root (2650,1600)、wing_front_root (2730,1650) 为中心，**盖住三组翅膀各自的根部**（从翼根往外大约到每片翼长度的三分之一），沿着翼骨方向往外散开；
   - 形状：很多片参差的尖羽，末端撕裂状 / 锯齿状（照 ref_wing_root_back.png 的羽毛形状），一层叠一层，外层的羽毛更长、更尖，往翼尖方向伸；边缘不规则，**不要画成整齐的鸟翼覆羽排**；
   - 颜色：黑 / 深灰为主，羽毛缝隙里透一点暗金（照模型贴图），P5 赛璐璐：清楚的黑色描边、两阶硬明暗；
   - 只画羽毛，**不画身体、手臂、翼膜、头**。
2. 如果羽毛簇挡住了翼膜根部原来的切口 / 接缝，那正好；**wings_back_far / wings_back_near / wings_front / body 一像素都不改**。
3. layers.json 加一项 wing_root_feathers：z 顺序放在 **wings_front 之下、wings_back_near 之上**（翼根羽毛盖住后面两组翼的根部，但被身体前面那组翼和身体盖住）；如果前面那组翼（wings_front）的根部也需要羽毛，另出一层 wing_root_feathers_front.png，放在 wings_front 之上、手臂之下。

【可直接复制的生图提示词】（拿 layers_v10 叠回的整张图 + ref_wing_root_back.png 当参考，图生图）
```
Edit this illustration: keep the six large scythe-shaped bat-like wings (bony wing fingers, membranes with gold skull patterns, scalloped trailing edges) exactly as they are. Add ONLY a thick cluster of ragged black feathers at the wing roots, where the wings meet the back and shoulders: many layered pointed feathers with torn, jagged tips, fanning outward along the wing bones and covering roughly the inner third of each wing, outer feathers longer and sharper, a hint of dark gold showing between them. Not a bird wing, not neat rows of coverts. Keep body, horned head, arm, gun, pose, camera and canvas unchanged.
Style: Persona 5 anime cel shading, bold black outlines, two-tone hard shadows, matching the existing illustration.
```
确认整体效果后，把羽毛部分单独抠成透明底的 wing_root_feathers.png（同画布同坐标）。

【检查】check_wing_roots_v10.png：原 v10 整图 / 加翼根羽毛后叠回 / 羽毛层单独 / 每组翅膀 ±3° 叠图（游戏里翅膀小幅鼓动，看羽毛和翼根接得住、不露洞）/ 和 ref_wing_root_back.png 并排。最多重画 2 次。
【提交】只 add wing_root_feathers*.png、layers.json、check 图；提交信息 "art: Satanael v10 ragged black feathers at the wing roots (the original's look)"。
【报告】中文：羽毛层盖住了哪些区域、z 顺序、原生分辨率、检查结果、提交号或分支名。
