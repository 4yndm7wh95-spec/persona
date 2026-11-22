你在用户的 Windows 电脑上工作。任务：**重新认** SKILL / ITEM / EQUIP 三页入场里的 3D Joker 用的是哪段动作，按原版重渲。上一轮（docs/codex/codex_p5_menu3d_pages.md，提交 4e65bb75）认错了。写报告。不改 mod 代码，不生图，不启动游戏。

**先读 docs/codex/README.md（遇到问题不要停下，按自己理解做完）、docs/codex/codex_p5_menu3d_pages.md 和 research/p5r/menu3d_pages/README.md。**路径、拉取、提交、推送同上一轮（分支 claude/p5-pile-ui）。

## 上一轮哪里错了
用户逐帧对比后指出：上一轮给 SKILL 选的 main_0015 / 0016 / 0017 是**抬膝、伸手、俯身**这类夸张动作，和原版视频里完全是两种，姿势也怪。
原版 SKILL 页（参考帧 docs/p5/preview/ref_3d_0111.jpg、ref_3d_0113.jpg、ref_3d_0117.jpg、ref_3d_0120.jpg，原图在 docs/p5/preview/p5r-20261121-223943-frames-02-12/）里的 3D Joker 是：
- **安静地站着、侧身、双手插在风衣口袋里**，头微低、脸转向镜头往下看；
- frame_0111 是同一个站姿的近景（只看到肩、侧脸和插兜的手臂，人物很大，占满画面左半边）；
- frame_0113～0120 是这个站姿的中景，人物在队伍列表后面，头在画面上方偏左（约 x = 0.40～0.48，y = 0.02～0.15），身体一直延伸到画面底边；
- 整段几乎没有大动作，只有很轻的呼吸 / 转头。
这种站姿很可能是 GAP 里时长较长的**待机循环**（main_0100～main_0113、blend_0001 这些 1～7.5 秒的片段）里的某一段——上一轮这些长片段只渲了首 / 中 / 末三帧，而且用的是主菜单的头部特写机位，人物大部分在画外，所以没被认出来。
ITEM（ref_3d_0180 / 0184 / 0186）和 EQUIP（ref_3d_0236 / 0240 / 0242）上一轮的选择也请用同样的方法**重新核对**一遍，对就保留，不对就换。

## 1. 先把所有片段用「全身机位」看一遍
- 新建一个能看到**全身**的机位（人物从头到脚都在画面里，正面偏侧 30° 左右），把 43 段动作**每段逐帧**都渲一遍（长片段每 5 帧取 1 帧即可），拼成 sheets\all_clips_fullbody.png（每段一行，写片段名和帧号）。
- 在这里面找：**侧身站立、双手插兜、头低着看镜头**的片段（SKILL）；以及 ITEM、EQUIP 参考帧里的姿势。

## 2. 认片段（写依据）
每一页选出最像的片段和帧范围。判断标准是**姿势**（站 / 坐 / 抬手、手在哪、头朝哪），不是轮廓大小。选完把**参考帧和你选的帧并排**拼成 sheets\<页>_pose_check.png（每组左参考、右你的渲染，渲染用接近参考的机位），自己先对一遍：手的位置、身体朝向、头的角度要对得上，对不上就继续找。

## 3. 按原版镜头重渲
- 认出片段后，照上一轮的方法给这一页拟合相机（frame_0111 是近景、0113～0120 是中景，可以两个镜头），重渲这一页用到的片段（整段 30fps 逐帧；长待机片段只渲入场用到的那一小段，约 1 秒）。
- 着色、服装、超采样、透明底、不要在画面中间截断：全部同上一轮。
- **覆盖** research/p5r/menu3d_pages/<页>/（删掉认错的旧片段目录：用 git rm，不要用 Remove-Item -Recurse），README.md 和 cameras.json 更新，sheets\<页>_match.png 重出。

## 4. 提交推送
- 只 add research/p5r/menu3d_pages 和 tools/p5/menu3d；提交信息 "research: menu 3D Joker per-page clips v2 (re-identified by pose: SKILL idle stand, ITEM/EQUIP rechecked)"；推送到 claude/p5-pile-ui。

## 5. 报告
中文：每页最终选的片段 / 帧范围和依据（和上一轮比改了什么）；相机参数；提交号；哪里还不确定。

【绝对不要做】同 codex_p5_menu3d_render.md 的清单（只改 research/p5r/menu3d_pages、tools/p5/menu3d；不安装、不下载、不启动游戏、不用 Computer Use、不生图、不碰 CPK 和游戏目录、不读 obs.json 和密码、不进入旧仓库）。删除认错的旧帧只用 git rm。

遇到问题不要停下：没写到的情况、命令出错、结果拿不准，都按你自己的理解选最合理的做法继续，把任务做完，最后在报告里写清楚。
