你在用户的 Windows 电脑上工作。任务：把**撒旦耶尔第 10 版**（art/p5/fx/v4/satanael/satanael_v10_draft.png，4000×3000）拆成游戏里做部件动画用的分层图，并补转枪素材。不改 mod 代码，不编译，不启动游戏，不用 Computer Use。

**先读 docs/codex/README.md**（生图一律真透明底）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/epic-archimedes-m9an69**
（先 fetch origin claude/epic-archimedes-m9an69:refs/remotes/origin/claude/epic-archimedes-m9an69，再 merge --ff-only）。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-satanael-layers-v10，报告分支名。

**做法照第 6 版拆层**（art/p5/fx/v4/satanael/layers_v6/README.md、layers.json、check_moved_v6.png、check_twirl.png）：各层同画布（4000×3000）同坐标，叠回和原图一致，被遮挡处补全，补画原生高分辨率。

【游戏里要做的动作（决定怎么拆）】
- 他在画面深处，**持枪手臂要绕肩膀往下转 30～45°** 才能对准前景右下方的敌人（第 10 版画的是枪朝右上方）；开枪后坐时前臂绕手肘往上翻；
- 转枪：手张开，长枪在画面平面里套着食指转，接住后握住瞄准；
- 呼吸、头轻微转动、六片翅膀各自鼓动（游戏里着色器变形）、红领带 / 下摆飘动。

【输出】art/p5/fx/v4/satanael/layers_v10/：
- wings_back_far.png、wings_back_near.png：身后左右两组翅膀（各含三片翼片；翼根补全）；如果近侧那组盖在身体前面，拆成 wings_front.png 另放一层；
- body.png：躯干、红领带、腰带、下半身（不含头、不含两只手臂；被手臂挡住的胸口补全）；
- head.png：头盔 + 两只羊角 + 面甲（脖子往下多画一截）；eyes_glow.png：只有发光的眼睛；
- arm_upper.png：持枪手臂的上臂（肩到肘），**手肘端画成圆润关节、往前臂方向多延伸一截**；
- arm_fore_gun.png：前臂 + 握枪的手 + **整把长枪**（被翅膀挡住的枪管、枪口补全；手肘端同样多延伸一截和上臂重叠）；
- arm_near.png：另一只手臂（如果画里露出来）；
- **转枪素材**：arm_fore_open.png（同一条前臂、手张开、食指伸直，枪会套在食指上转）、gun.png（**整把长枪单独一张**，同画布同坐标、和 arm_fore_gun 里的枪重合，被手挡住的枪柄、扳机、扳机护圈补完整，护圈是空心的环）、finger_tip.png（食指尖一小段）。
【layers.json】每层：文件名、z_index、pivot。关节点（4000×3000 像素）：gun_shoulder、gun_elbow、gun_wrist、**muzzle（枪口中心）**、barrel_dir（枪管朝向角，度，0°=向右，顺时针为正）、
spin_finger、gun_guard（护圈中心）、neck、eyes、body_center、各组翅膀的翼根、tie_root（领带挂点）。
【检查】check_layers_v10.png（原图 / 叠回 / 各层排开）、check_moved_v10.png（**上臂绕肩往下转 -40°**、前臂上翻 +40° / 下转 -10°、头 ±8°、每组翅膀 ±10° 的叠图，看关节露洞）、
check_twirl_v10.png（arm_fore_open + finger_tip 原位；gun.png 套在 spin_finger 上转 0/90/180/270°）。露洞就补，最多 2 次。

【提交】只 add art/p5/fx/v4/satanael/layers_v10/；提交信息 "art: Satanael v10 layered (two-segment gun arm, twirl assets)"。
【报告】中文：每层补画了哪些地方、各区域原生分辨率、检查结果、提交号或分支名。
