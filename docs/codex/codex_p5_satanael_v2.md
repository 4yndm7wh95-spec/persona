你在用户的 Windows 电脑上工作。任务：**撒旦耶尔（Satanael）第 2 版草图**——改朝向、改构图，给「华丽收场」的大罪穿甲弹演出用。不改 mod 代码，不编译，不启动游戏，不用 Computer Use。

**先读 docs/codex/README.md**（生图一律真透明底；姿势图一致性规则）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/epic-archimedes-m9an69**
（仓库副本 C:\Users\kkk\sts2_tools\p5_build\repo 已经在这个分支上：先 fetch origin claude/epic-archimedes-m9an69:refs/remotes/origin/claude/epic-archimedes-m9an69，再 merge --ff-only）。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-satanael-v2，报告分支名。
原模型、GLB、原始高分辨率渲染只留在本机（C:/Users/kkk/sts2_tools/p5r_rip/fx_work/satanael/），**不要提交进仓库**；仓库只提交下面列的文件。

【用途（用户 2026-11-08）】
打出「华丽收场」时整个战斗场景变成夜空 + 断崖（参考 art/p5/fx/v4/satanael/ref_platform_1.png、ref_platform_2.png，用户录的原版画面），Joker 站在左边的断崖上，
撒旦耶尔**巨大地立在 Joker 身后**：游戏里他会**从屏幕最下面一直顶到最上面、还显示不完**（顶天立地的感觉），屏幕里大概只看得到胸口 / 脖子到头顶这一段 + 举枪的手臂和巨枪。
他举起巨枪和 Joker 同步瞄准、同步开枪，射出大罪穿甲弹（参考 ref_fire.png、ref_raise.png、ref_joker_aim.png）。

【第 1 版为什么不能用】art/p5/fx/v4/satanael/satanael_full.png（设计、配色、画风用户没意见，保留）：
1. **朝向不对**：正面朝镜头。他和 Joker 一起面对右边的敌人，应该 **3/4 侧身朝画面右侧**（和局内 Joker art/p5/poses/final/idle_a3.png、亚森第 7 版 art/p5/fx/v4/arsene/arsene_draft_v7_facefix.png 一样的转身角度）。
2. **不够大**：第 1 版是腰部以上的横构图，放大到撑满屏幕高度就糊了，比例也不对。这次是**竖构图、高分辨率**。

【1. 姿势参考：渲原版模型】
用本机已有的撒旦耶尔模型（BASE.CPK MODEL/CHARACTER/PERSONA/0170/PS0170.GMD，第 1 版 README 里有记录）和它的动作包（BPS0170*.GAP / GT0170 一类），按亚森那套流程
（GFDExport → tools/p5r/fix_gfd_glb.py → 按骨骼名挂动作 → Blender 4.5.9，参考 research/p5r/persona_izanagi_okami/REPORT.md）：
- 找出**举枪瞄准 / 开枪**的那段动作（大罪穿甲弹或普通攻击里举枪的那一段；分不清就每段出联系表，写进报告），取**枪举平、指向前方**的那一帧；
- 相机：3/4 朝画面右侧（和亚森 cast 相同的方向 (-0.8,-1,0) 附近，略仰视），正交；
- 出 art/p5/fx/v4/satanael/v2_pose_ref.png（透明底，两阶明暗 + 黑轮廓，只当姿势参考）和 v2_pose_contact.png（候选动作的联系表）。
找不到举枪的动作：用模型默认姿势在 Blender 里把持枪手臂摆成举平指向右前方再渲，报告里写明。

【2. 生图：第 2 版草图】
以 v2_pose_ref.png 为**姿势参考**、以第 1 版 satanael_full.png 为**设计 / 配色 / 画风参考**（黑金铠甲、角、红眼、红色内衬、翅膀造型、巨型左轮），重画：
1. **3/4 侧身朝画面右侧**：胸口、头、角都朝右前方；靠近镜头的肩膀在左，远的在右。略仰视（从 Joker 脚下往上看他），显得高大。
2. **举枪**：持枪那只手臂（右臂）往画面右侧举平，巨型左轮枪口指向右方、略微朝下（指向远处地面上的敌人），枪身完整、枪管清楚；另一只手可以托着枪或自然张开在身侧。
   第 1 版是两只手一起握枪也可以，只要枪和持枪的手臂能作为一层单独拆出来。
3. **构图**：竖构图 **2400×3600**，从**腰部**画到**角尖**（游戏里只显示胸口以上，但下面要画到腰，方便调位置）；枪管可以伸出画布右边被截断，没关系；
   翅膀在身后张开，可以伸出画布被截断。头和角不能碰画布上边（四周留至少 60px，除了枪管和翅膀）。
4. 画风、描边粗细、明暗和第 1 版一致（P5 赛璐璐，粗黑描边，两阶硬明暗）。眼睛发光红色。不要背景、光晕、烟雾、特效。真透明底。
出 art/p5/fx/v4/satanael/satanael_v2_draft.png（+ _raw.png + _generation.json）和 check_satanael_v2.png（从左到右：第 1 版、v2_pose_ref、第 2 版、Joker idle_a3；深灰底）。
最多重画 2 次。**只出草图，不拆层**，等用户确认。


【用户对第 2 版第一稿的意见（2026-11-08）—— 按这里重画】
用户看了你在对话里给的第 2 版第一稿（3/4 朝右、双手在胸前握枪的那张）：
1. **持枪姿势不对**。原版（art/p5/fx/v4/satanael/ref_hold_gun.png、ref_fire.png，用户录的原版画面）是：
   - **单手**（右手）握枪，**整条手臂从肩膀笔直伸出去**，枪举在肩膀高度，直指敌人（画面右侧）——像决斗举枪；另一只手不碰枪，自然垂在身侧或往后张开；
   - **枪非常巨大**：枪管比他整条手臂还长，枪身厚重，枪口很大。
   所以这次：持枪手臂伸直、平举、指向画面右侧（可以略微朝下指向远处的敌人），枪管伸出画布右边被截断没关系。不要双手握枪、不要手肘弯曲收在胸前。
2. **太金光灿烂**。原版在夜景冷光下，金色看起来是淡淡的银金色。这次：**金色降低饱和度、压暗**（偏古铜 / 暗金，不要亮黄金），黑色部分保持黑；不要画高光闪光。
   （游戏里还会用着色器加一层冷蓝夜光和青色边光，所以素材本身偏暗、偏素就对了。）
其余要求（3/4 朝右、竖构图 2400×3600、从腰画到角尖、真透明底、画风描边）不变。重画后同样出 check_satanael_v2.png（加一格 ref_hold_gun.png 对照）。

【提交】只 add 新文件（含 ref_hold_gun.png 不用管，已在仓库）：v2_pose_ref.png、v2_pose_contact.png、satanael_v2_draft*.png / .json、check_satanael_v2.png、art/p5/fx/v4/satanael/README_v2.md（中文：用了哪个动作哪一帧、相机、重画次数、自己觉得哪里还不像）。
提交信息 "art: Satanael draft v2 (three-quarter facing right, tall portrait, gun raised)"。
【报告】中文：草图位置、重画次数、自己觉得哪里还不像、提交号或分支名。
