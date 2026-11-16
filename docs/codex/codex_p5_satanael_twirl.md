你在用户的 Windows 电脑上工作。任务：给撒旦耶尔第 6 版分层**补转枪用的素材**。不改 mod 代码，不编译，不启动游戏，不用 Computer Use。

**先读 docs/codex/README.md**（生图一律真透明底）。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/epic-archimedes-m9an69**
（仓库副本已经在这个分支上：先 fetch origin claude/epic-archimedes-m9an69:refs/remotes/origin/claude/epic-archimedes-m9an69，再 merge --ff-only）。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-satanael-twirl，报告分支名。

【用途（用户 2026-11-08）】撒旦耶尔掏枪时要**转枪**，和局内 Joker 的转枪一样（tools/p5/build_rig_p5.py 里 GUN_IN / SPIN_IN 那段注释：
Joker 是一张「手臂伸出、食指放松伸着」的空手姿势图 gun_spin_g + 一把单独的枪 pistol_b.png，枪的扳机护圈套在食指上、以护圈为轴旋转，
食指尖单独一小块画在枪上面（看起来手指穿过护圈），转完接住换成握枪的姿势图）。撒旦耶尔转完和 Joker 同步开出大罪穿甲弹。
现在的分层 art/p5/fx/v4/satanael/layers_v6/ 里，手和枪画在一起（arm_fore_gun.png），转不了。

【要补的三张】都放 art/p5/fx/v4/satanael/layers_v6/，**画布 3400×3600、坐标和 layers_v6 完全一致**（叠在原图上对得上），真透明底，原生分辨率补画（同 layers_v6 的做法），画风同第 6 版：
1. **arm_fore_open.png**：持枪手臂的前臂 + **空手**：和 arm_fore_gun.png 同一条前臂、同一个手肘关节端（pivot 同为 gun_elbow (1624,2177)），
   手腕位置不变；手**张开放松，食指伸直、略微往前下方伸**（枪会套在这根食指上转），拇指和其余三指松开、微微弯曲，掌心半朝镜头。不画枪。
2. **finger_tip.png**：只有 arm_fore_open 里**食指从第二个指节到指尖**那一小段（同画布同坐标，其他全透明）——游戏里它画在旋转的枪上面。
3. **gun.png**：**整把左轮单独一张**：造型、大小、角度和 arm_fore_gun.png 里那把**完全一样**（同画布同坐标，叠上去和原图里的枪重合），
   但**被手挡住的枪柄、扳机、扳机护圈都补画完整**（手不画）。扳机护圈要画成一个清楚的环（中间是空的、透明），食指要能从里面穿过。
【layers.json】在 layers_v6/layers.json 里追加这三层（z_index：arm_fore_open 同 arm_fore_gun；gun 在它上面；finger_tip 在 gun 上面），并加关节点：
- spin_finger：arm_fore_open 里食指穿过护圈的那一点（转枪的轴心，屏幕上应该落在食指上）；
- gun_guard：gun.png 里扳机护圈环的中心（和枪口 muzzle 一起，游戏里用来对齐和转动）；
- 说明 gun.png 叠在 arm_fore_gun 位置时，gun_guard 和 spin_finger 的偏移是多少。
【检查】check_twirl.png（深灰底）：①arm_fore_open + finger_tip 叠在原图位置；②gun.png 叠在原图上（应该和 arm_fore_gun 里的枪重合）；
③把 gun.png 以 gun_guard 为轴、套在 spin_finger 上转 0°/90°/180°/270° 四张，看手指是不是穿在护圈里、枪柄补画完不完整。露馅就补，最多 2 次。
【提交】只 add 新文件和 layers.json 的改动；提交信息 "art: Satanael twirl assets (open hand, fingertip, standalone gun)"。
【报告】中文：三张图的补画内容和原生分辨率、spin_finger / gun_guard 坐标、检查结果、提交号或分支名。
