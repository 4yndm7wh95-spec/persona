你在用户的 Windows 电脑上工作。这是 P5 Joker 局内姿势图的**新总清单**，以后姿势就照这一份做。不改 mod 代码，不启动游戏，不用 Computer Use。

找仓库、拉取（git fetch + merge，能快进就 --ff-only）、提交推送、「绝对不要做」清单都和 docs/codex/codex_p5r_model_fix.md 一样。另外：
- 不要修改或删除代码；不要 rebase / reset / amend；不要 --force；不要改全局 git config；不要 sparse-checkout disable / set；不要和其他会话同时操作这个仓库。
- 不要读取或输出 C:\Users\kkk\.p3record\obs.json，也不要读取或输出任何密码。
- 不要修改 P3R、P5R 或杀戮尖塔 2 的游戏原文件；不要绕过 imagegen 的安全拒绝或 WindowsApps 的权限保护。
- art/cards 下另一个会话的已修改文件原样保留，不提交、不 stash、不还原。
- **不要打开、参考、编辑 P3Makoto（结城理）mod 的任何图片。**
- 遇到没写到的情况，停下来报告。

【用户的新决定】
1. **不再参考 P5R 原版的姿势**。art/p5/ref/pose/ 下的原版渲染图全部停用，不要再作为生图输入，也不要再渲新的。
2. 姿势由 Claude 规划：**先完整读 docs/p5/POSE_PLAN.md**（每张的设计），对照草图 art/p5/ref/plan/<姿势名>.png（红 = 右手右腿、离镜头近；蓝 = 左手左腿；头上短线 = 脸朝向；底部灰线 = 地面）。
3. 每个姿势只出一张 _a。
**小刀斩（用户 2026-10-25 最终定的动作）**：之前所有 slash / knife_ready 成图都作废。**反手握刀**，从人物右前方 45°、腹部高度，往人物左前方 45° 斜向上撩；两脚距离约 1.2 个肩宽（不要大弓步）、上身微前倾不后仰、出刀手臂手肘微弯。详见 docs/p5/POSE_PLAN.md「小刀斩」一节；草图 art/p5/ref/plan/knife_ready.png、slash_smear.png、slash.png、slash_follow.png，节奏 slash_timing.gif。
4. 这份覆盖 docs/codex/codex_p5_poses1c.md、codex_p5_poses2.md 第 1 节、codex_p5_poses3.md 里**关于姿势动作的描述**；那几份里的其他要求（面具单图、切入图、rest_sit、眨眼贴片、过渡帧做法、sequence_check）照旧。

【作废】guard_a/b、summon_a/b、cast_a/b 不再修、不再提交。cast、cast_mid、cast_low、victory 不做。

【统一要求】
- 画布 1024×1536、真透明底；**以 idle_a.png 为输入做图像编辑**（保证人物、服装、比例、画风一致），再附上对应的草图作为姿势布局参考；脸以 art/p5/ref/face_standard.png 为准。
- 人物大小和 idle_a 一致（头部大小相同），站姿鞋底踩在 y≈1470。
- 草图只管关节位置和重心，不要把草图的线条、颜色画进去。
- 过渡帧（*_mid、*_ready、*_follow、*_recoil、*_lower、*_end、*_impact、*_recover、die_kneel、die_fall）以「起点」那张定稿图为输入，只把动作往终点挪一半左右（codex_p5_poses3.md 的过渡帧做法）。
- 眼睛不对：用 codex_p5_poses1e.md 的「裁头部 → 脚本挪虹膜（优先）/ 以 face_standard 为准改眼睛 → 羽化蒙版贴回」修，不要整张重生成。
- 每张清理残点：主体外扩 16px 以外、alpha ≤ 2 的像素置 0。
- **一张通过了再做下一张**；连续 3 次做不对就停下来，报告是哪张、问题是什么。
- 被安全策略拒绝（比如枪）：枪改成「手臂平伸、手掌张开指向前方」再试一次，报告里写明；不要绕过。

【顺序】（每组做完提交推送一次，只 add art/p5）
1. idle_a 修眼睛 + 残点清理（如果 1e 里还没做完）。
2. 关键姿势：guard → slash → gun → summon_grab → summon_rip → glove → down。
3. 过渡帧：guard_mid；knife_ready、slash_smear、slash_follow（knife_windup 取消）；gun_raise、gun_recoil、gun_lower；summon_mid、summon_end；glove_mid；hurt_impact、hurt_recover；die_kneel、die_fall。
4. 其他：relaxed、rest_sit、idle_blink_patch（照 codex_p5_poses3.md）；面具单图 mask_a 和切入图 cutin_grab_a、cutin_rip_a（照 codex_p5_poses2.md 第 2、3 节，扣 / 扯面具的是右手）。
5. 拼 art/p5/poses/sequence_check.png（每个动作一行，按播放顺序，见 docs/p5/ART_SPEC.md 的「局内动画清单」），以及 art/p5/poses/plan_check.png（每张成图和对应草图左右并排）。

【报告】中文，逐张写：对照草图是否一致（手脚位置、重心、脸朝向）、持刀 / 持枪 / 撕面具的是不是右手、视线（中间略偏右 / 看镜头 / 斜眼）、缩小到 25% 时轮廓是否清楚、残点清理数量、提交号。
