你在用户的 Windows 电脑上工作。任务：撒旦耶尔第 10 版**持枪手臂 + 枪再重画一次**（只这几层）。不改 mod 代码，不编译，不启动游戏，不用 Computer Use。
仓库规则同 docs/codex/codex_p5_satanael_layers_v10.md（分支 claude/epic-archimedes-m9an69，先 fetch 再 merge --ff-only；网络慢就推到 codex/p5-satanael-v10-arm2）。
画布：layers_v10 现在是 **5400×3000**（v10_fix2 加宽后的坐标），所有坐标都按这个画布。

【用户看游戏预览后的意见（2026-11-09）】
1. **手臂接在胸口上了**：现在的上臂是从胸口中间、红领带旁边长出来的，像把手臂插在胸前。
   **要画成**：上臂从**右肩的大肩甲下面**伸出来（身体 3/4 朝右，所以持枪的右肩在身体右侧外沿），先往外侧、再往右下方走；
   肩甲盖住上臂根部；胸口、领带处不能有手臂。参考 model_ref/aim_side_cel.png、turn_front_cel.png 的肩膀位置。
   **gun_shoulder 要挪到真正的肩关节**（右肩甲中心下方），在 layers.json 里更新；手肘清楚、上臂粗壮，前臂朝镜头往右前下方（透视：前臂和手比上臂大），正手握枪（照 model_ref/gun_grip_cel.png）。
2. **枪管太长，不像手枪**。改成**真正的手枪比例**：枪管（机匣前端到枪口）长度 ≈ 握把 + 机匣的长度，整支枪大约是现在 gun.png 的**一半长**；
   保留原版造型元素：机匣上的**金色扇形装饰**、**金色握把**、黑金配色、枪口金属环。不要左轮、不要圆筒炮管。
   枪管方向不变：屏幕上朝右下 20～30°（barrel_dir）。
3. 不用改：身体、头、翅膀、云（游戏里 Claude 用着色器盖住腰下）。

【输出】覆盖 art/p5/fx/v4/satanael/layers_v10/ 的 arm_upper.png、arm_fore_gun.png、arm_fore_open.png、finger_tip.png、gun.png（同画布同坐标；手肘两端多画一截重叠；gun.png 是整支枪的侧面、扳机护圈空心，转枪时套在食指上转），
gun_short_side.png 也按新枪重出（透明底、长边 1600）；body.png 里**原来被旧手臂挡住的胸口补完整**（新手臂不挡胸口了）。
更新 layers.json：gun_shoulder、gun_elbow、gun_wrist、muzzle、barrel_dir、spin_finger、gun_guard，"canvas":[5400,3000] 保留。

【检查】check_arm2_v10.png：旧 / 新叠回 / 新手臂上臂 ±10°、前臂上翻 +40°（后坐）/ 新枪和旧枪并排比长度。看肩、肘露洞。最多重画 2 次。
【提交】只 add 改动的文件；提交信息 "art: Satanael v10 arm from the shoulder pauldron, handgun-length barrel"。
【报告】中文：新的关节点坐标、枪长（新/旧）、各区域原生分辨率、检查结果、提交号或分支名。
