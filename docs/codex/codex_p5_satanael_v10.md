你在用户的 Windows 电脑上工作。任务：**撒旦耶尔第 10 版草图**——照原版模型的样子画（用户 2026-11-08：「原版就行」）。不改 mod 代码，不编译，不启动游戏，不用 Computer Use。

**先读 docs/codex/README.md**（生图一律真透明底）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/epic-archimedes-m9an69**
（先 fetch origin claude/epic-archimedes-m9an69:refs/remotes/origin/claude/epic-archimedes-m9an69，再 merge --ff-only）。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-satanael-v10，报告分支名。
**不用 Blender、不摆姿势**：直接拿你上一轮渲的 art/p5/fx/v4/satanael/model_ref/ 当参考生图。

【造型：完全照原版模型（model_ref/）】之前第 6～9 版的造型都是猜的，这次一律以模型参考为准：
- **头**：深色头盔，顶上两个蝙蝠耳状尖角；两侧一对**巨大的羊角**，往下、往前弯成近乎一圈，角内侧有羽毛纹；面甲下半截像鸟喙，眼睛周围是金色、眼睛发光。
  **不是白脸、不是人脸、没有嘴牙**（参考 head_front_cel.png、head_right_3q_cel.png、head_low_cel.png）。
- **翅膀**：**六片**巨大的镰刀形翼片（左右各三片），张开后比身体宽好几倍；翼片上的**金色大块和骷髅图案照原版贴图保留**（textures/、turn_front_cel.png、turn_back_cel.png）。
- **枪**：原版那把**极长极细的长枪**（gun_side_cel.png、gun_grip_cel.png）：细长枪管、枪身中段的金色扇形装饰、金色枪柄；**不是左轮**。
- **身体**：瘦长的深色铠甲、红领带、细长的腿、腿甲上的金色纹样（turn_*_cel.png、aim_side_cel.png）。
- **光环**：**不画**（游戏里 Claude 照 halo_cel.png 用着色器做红色尖刺光环），头顶上方留空。
- **配色**：照原版贴图的黑 + 金 + 红，**不要再压暗、不要去掉金色**（游戏里会用着色器加冷蓝夜光把金色压下去，效果会接近用户视频）。

【姿势 / 构图：沿用第 8 版】satanael_v8_draft.png 的构图和机位用户是认可的：低机位、从他右前方往上看；身体 3/4 转向画面右侧；
**持枪手臂朝镜头、往右前方伸出，长枪的枪管透视着朝向我们**（原版长枪很长，透视后是一根从手里伸向镜头的长枪管，枪口在画面右上近处）；
六片翅膀从背后往前包过来、近侧的翼片铺在画面下方伸到枪附近；另一只手不要直直垂着（藏在身体 / 翅膀后面，或扶在胸前）。
**正手握枪**：手握住金色枪柄，食指在扳机处（照 gun_grip_cel.png 的握法），不要反手捏。
下半身照原版画到大腿中部（游戏里用云层盖住）。参考原版镜头 ref_original_satanael_mirrored.png 的整体感觉。

【分辨率 / 交付】横构图 4000×3000，**原生高分辨率**（分块重绘拼接，同第 8 版），不许交放大的小图；真透明底；P5 赛璐璐画风（粗黑描边、两阶硬明暗）。
输出 satanael_v10_draft.png（+ _raw.png + _generation.json）、check_satanael_v10.png（从左到右：model_ref 的 turn_front_cel、head_right_3q_cel、gun_side_cel、v10；深灰底）。
自查：头（羊角、头盔、面甲）、六片翅膀、长枪、红领带和模型参考逐项对得上；不对就重画，最多 2 次。**先不拆层**，等用户确认。

【提交】只 add 新文件；提交信息 "art: Satanael draft v10 (design from the original model, v8 composition)"。
【报告】中文：草图位置、各区域原生分辨率、重画次数、和模型参考还有哪里不一样、提交号或分支名。
