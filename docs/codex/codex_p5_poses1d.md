你在用户的 Windows 电脑上工作。这是 P5 Joker 姿势图的**第二次修订**，专门修**视线**。它替代 docs/codex/codex_p5_poses1b.md 里「视线」那一条；其他要求（瞳孔高光、不驼背、统一要求）照旧。做完这份，再接着做 docs/codex/codex_p5_poses1c.md（小刀斩、开枪）和 docs/codex/codex_p5_poses2.md（撕面具召唤），三条视线规则对它们同样适用。
找仓库、拉取、提交推送、「绝对不要做」清单都和 docs/codex/codex_p5r_model_fix.md 一样；art/cards 下另一个会话的已修改文件原样保留，不提交、不 stash、不还原。

【用户反馈】
- 很多张眼睛还是看着屏幕（例如 idle_a：瞳孔在眼睛正中、直视镜头）；
- 但改过头又会变成纯侧脸、眼睛完全看向画面右边，也不对。反例：art/p5/ref/gaze_too_far.png（纯侧脸，太朝右）。
- 正确的头部角度看 **art/p5/ref/gaze_angle_joker_3d.png**（Joker 原版 3D 模型的 3/4 侧头部，**只看角度**，不学 3D 质感）。
- **更正（重要）**：这份提示的上一版给了结城理的头当参考，你把结城理的头换到了 Joker 身上、姿势也照抄了结城理 mod。那是错的：**不要打开、不要参考、不要编辑 P3Makoto（结城理）mod 的任何图片**（art/v12/、art/master/ 等）。只用 Joker 的母版 art/p5/master/joker_master_a.png、Joker 已出的姿势图和 art/p5/ref/ 下的参考。带结城理的头或照抄结城理姿势的图全部作废，不要提交，本机删掉。

【视线规则（写死）】
1. **头**：和身体一样是 3/4 侧向画面右方，脸转开镜头大约 30~45°，**看得到两只眼睛**（离镜头远的那只可以被鼻梁 / 刘海挡掉一部分），**不要纯侧脸**。
2. **眼珠**：两只眼睛的瞳孔都在眼眶里**偏向画面右侧**（靠右眼角），看向画面右方、和人物差不多高的位置（敌人站在右边地面上）。**不要瞳孔居中直视镜头**，也不要只剩一只眼睛的侧脸。
3. **对照**：每张生成后，把头部放大到和 gaze_angle_joker_3d.png 差不多大，三张并排（gaze_angle_joker_3d.png、反例、新图）看一遍：新图的头部转角要接近 gaze_angle_joker_3d.png，不能像反例。

【这次做哪些】
1. 已提交的 idle_a / idle_b / guard_a / guard_b / summon_a：**全部按新视线规则重做**（以对应的旧图为输入做图像编辑，**只改头的转角和眼睛**，身体、手、腿、衣服一律不动），覆盖同名文件。
2. 本机还没提交、按旧规则生成的图：先按新视线规则检查，不符合的同样只改头和眼睛。
3. 还没做的 cast、hurt、down、victory：直接按新规则做（hurt 的视线可以被打偏，down 闭眼或半睁都行，这两张不强求看敌人）。
4. kick_a.png：用户取消了踢，**从仓库删掉**（git rm art/p5/poses/kick_a.png），compare 图里也去掉。

【自检（每张都写）】
- 头的转角：接近参考角度 / 太正（看镜头）/ 太侧（纯侧脸）三选一；
- 两只瞳孔的位置：偏右 / 居中 / 看不见；
- 高光是否清楚、是否驼背。
再出一张 art/p5/poses/gaze_check.png：每个姿势的头部放大图排成一行，最左边放 gaze_angle_joker_3d.png，方便用户一眼对比。

【提交】
只 add art/p5/poses（含删除 kick_a.png）；"art: P5 Joker poses - gaze fixed to 3/4 toward enemy"。
报告：中文，逐张自检 + 提交号。然后继续 codex_p5_poses1c.md、codex_p5_poses2.md。
