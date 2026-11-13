你在用户的 Windows 电脑上工作。任务：把**撒旦耶尔第 6 版**（art/p5/fx/v4/satanael/satanael_v6_draft.png，用户 2026-11-08 确认）拆成游戏里做 2D 骨骼 / 部件动画用的分层图。不改 mod 代码，不编译，不启动游戏，不用 Computer Use。

**先读 docs/codex/README.md**（生图一律真透明底）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/epic-archimedes-m9an69**
（仓库副本已经在这个分支上：先 fetch origin claude/epic-archimedes-m9an69:refs/remotes/origin/claude/epic-archimedes-m9an69，再 merge --ff-only）。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-satanael-layers-v6，报告分支名。

**做法照第 1 版拆层和亚森第 7 版拆层**（art/p5/fx/v4/satanael/README_layers.md、art/p5/fx/v4/arsene/README_layers.md）：
各层独立补画后按原图对位、叠起来和原图一致、被遮挡处补全、每层同样大小（3400×3600）同样坐标。**补画也要原生高分辨率**（同第 6 版的分块做法），不要小图放大。

【游戏里要做的动作（决定怎么拆）】
- **抬枪**：持枪手臂从低处甩上来瞄准；**开枪后坐**：前臂绕**手肘**猛地往上翻约 40°（枪口甩向自己头那边），上臂绕肩膀往上抬约 12°，上身后仰，然后慢慢放回去。
  → 所以持枪手臂**必须拆成两节**，手肘要能弯。
- 呼吸、头轻微转动、眼睛发光、翅膀和披风在风里鼓动（游戏里用着色器做变形）、近侧那只手轻微动作。

【输出】art/p5/fx/v4/satanael/layers_v6/：
- wing_back.png：远侧（画面右上）的翅膀，翼根补全；
- wing_front.png：近侧（画面左上）的翅膀，翼根补全；
- cape.png：暗红披风和身后的长下摆（被身体、手臂挡住的部分补全）；
- body.png：躯干、胸甲、腰带、裙甲、**近侧肩甲**（不含头、不含两只手臂；被手臂挡住的胸甲补全）；
- head.png：头盔 + 角 + 脖子上端（脖子往下多画一截，转动时不露缝）；
- arm_upper.png：持枪手臂的**上臂**（从肩膀到手肘，含持枪那侧的肩甲 / 上臂护甲）；**手肘那一端画成圆润的关节护甲、往前臂方向多延伸一截**，好让前臂转动时盖住接缝；
- arm_fore_gun.png：持枪手臂的**前臂 + 手 + 整支左轮**（从手肘到枪口）；手肘那一端同样多延伸一截圆润的关节，和上臂重叠；
- arm_near.png：近侧那只手臂（肩甲下面到手），肩端补全；
- eyes_glow.png：只有发光的红眼。
叠放顺序（从后往前）按画面实际前后关系定，写进 layers.json。

【layers.json】每层：文件名、z_index、**pivot**（转动中心）。关节点（3400×3600 像素坐标，下面是 Claude 目测的近似值，请按图精确标）：
- gun_shoulder ≈ (1320, 2000)、gun_elbow ≈ (1720, 2160)、gun_wrist ≈ (2000, 2240)、**muzzle ≈ (3340, 2160)**（枪口中心）、barrel_dir（枪管朝向角，度，0° = 向右，顺时针为正）；
- near_shoulder ≈ (600, 1960)、neck（头转动中心）≈ (1320, 1700)、eyes 中心、body_center（呼吸缩放中心）；
- wing_back_root、wing_front_root（翼根）、cape_root（披风挂点）。

【检查】
- check_layers_v6.png：左原图、中各层叠回、右各层单独排开（深灰底）；叠回与原图的差异写数字。
- check_moved_v6.png：**前臂绕手肘 +40°（往上翻）和 -10°**、上臂绕肩膀 +12° / -45°（往下放）、头 ±8°、翅膀 ±10° 的叠图，看关节处有没有露洞 / 接缝；露洞就补，最多 2 次。
- （可选）每层再试着出一张法线贴图 *_n.png（用深度估计 / 从明暗推法线都行），游戏里用 2D 光照照出立体明暗；做不到就跳过，报告里写明。

【提交】只 add art/p5/fx/v4/satanael/layers_v6/；提交信息 "art: Satanael v6 layered for 2D puppet animation (two-segment gun arm)"。
【报告】中文：每层补画了哪些地方、各区域原生分辨率、check_moved 有没有露洞、法线贴图做没做、提交号或分支名。
