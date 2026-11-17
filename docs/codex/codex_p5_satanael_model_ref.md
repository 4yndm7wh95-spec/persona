你在用户的 Windows 电脑上工作。任务：**从本机 P5R 里找撒旦耶尔（Satanael）的原版模型，渲一套外观参考图**，用来定下他的样子。
**不生图（不用 imagegen）**，不改 mod 代码，不编译，不启动游戏，不用 Computer Use。

**先读 docs/codex/README.md**。找仓库、git 参数、重试、「绝对不要做」清单照 docs/codex/codex_p5r_model_fix.md 执行，**分支是 claude/epic-archimedes-m9an69**
（先 fetch origin claude/epic-archimedes-m9an69:refs/remotes/origin/claude/epic-archimedes-m9an69，再 merge --ff-only）。
网络慢：推送前如果合并要下载的东西超时，就别合并，直接 git push origin <提交号>:refs/heads/codex/p5-satanael-model-ref，报告分支名。
游戏文件只读；原模型、GLB、中间文件只留在本机 C:/Users/kkk/sts2_tools/p5r_rip/fx_work/satanael/，**不要提交进仓库**，仓库只提交渲染出来的参考图和说明。

【背景（用户 2026-11-08）】撒旦耶尔的生图已经画了好几版都不像原作（脸画成人脸、整体像穿正装的乌鸦……），根本原因是没有清楚的原版外观参考：
用户录的视频太糊，第 1 版时渲的 satanael_ref.png 头和光环被裁掉了。用户：「先找原版资源看一下，定好他的样子，先不生成图了。」

【要找的】
- 模型：BASE.CPK MODEL/CHARACTER/PERSONA/0170/PS0170.GMD（第 1、2 版已经用过，见 art/p5/fx/v4/satanael/README.md、README_v2.md）。
  另外查一下 PSZ0170（异色版）和事件模型 PS0199（README 里提到含同名贴图）是不是同一个撒旦耶尔；**用和战斗里一样的那个**，报告里写清楚用了哪个、为什么。
- 光环：原版头顶上有一圈带尖刺的光环（art/p5/fx/v4/satanael/ref_original_satanael.png 右上角），查它是模型里的网格（例如 m_eye / 其他网格名）还是单独的特效贴图；找得到就一起渲 / 一起导出贴图。
- 动作：BPS0170_000.GAP / GT0170.GAP（第 2 版已导出过 16 段带骨骼的动作，联系表在 v2_pose_contact.png）。

【渲染（流程同亚森 / 伊邪那岐：GFDExport → tools/p5r/fix_gfd_glb.py → Blender 4.5.9）】
每组都出**两种**：①原贴图、普通柔光、不描边（看清真实颜色和材质）；②P5 风格两阶硬明暗 + 黑描边（和局内画风对照）。透明底，长边 ≥ 2048。
**相机要把整个人、两只翅膀、头顶两只角和光环都框进去，不能裁掉**（第 1 版的问题）。
1. **待机姿势转身图**：正面、左 3/4、右 3/4、正侧面、背面，各一张；
2. **头部特写**：正面、右 3/4、低机位仰视，各一张（头盔 / 面甲 / 眼睛 / 角的细节要看清）；
3. **持枪的手和枪特写**：枪的完整侧面一张，手握枪的样子一张；
4. **举枪瞄准的那一帧**：从 16 段动作里找枪举平瞄准的那段（大罪穿甲弹 / 攻击），渲两个机位——侧面；以及**低机位、从他持枪那侧的前方往上看**（就是用户视频里原版那个镜头，ref_original_satanael.png），再出一张左右镜像的；
5. 全部贴图（*.dds）转成 png 存一份，方便看图案和配色。
输出 art/p5/fx/v4/satanael/model_ref/：上面的 png（文件名写清楚组别、角度、风格，例如 turn_front_lit.png、turn_front_cel.png、head_low_cel.png、aim_lowfront_cel_mirrored.png）、
textures/（贴图 png）、contact_model_ref.png（所有渲染缩小排成一张总览，深灰底）、README.md（中文：用了哪个模型和动作、每张的相机参数、光环找到没有、和我们第 6～9 版草图明显不同的地方——头、脸、翅膀、枪、配色、下半身各一条）。

【检查】每张：画布四角透明、人物 / 翅膀 / 角 / 光环有没有碰到画布边被裁。

【提交】只 add art/p5/fx/v4/satanael/model_ref/；提交信息 "ref: Satanael original model renders (turnaround, head, gun, aim pose, textures)"。
【报告】中文：找到了什么、光环是什么、提交号或分支名、README 全文。
