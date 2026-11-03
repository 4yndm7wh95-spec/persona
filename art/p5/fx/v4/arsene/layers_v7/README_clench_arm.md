# 亚森伸臂与攥拳分层

交付：arm_reach_extend.png、arm_reach_fist.png，均为 2048×2304 RGBA，与 layers_v7 相同画布；肩关节沿用 (1380,665)。旧 arm_reach.png、body.png 和 layers.json 不改。

- extend：同一款红袖、金扣、黑手套，向画面右侧略朝下伸直，张爪。
- fist：以 extend 的手部裁片编辑成拳头，只贴回手部；肩、上臂、前臂、红袖口完全沿用 extend 的像素。
- 每张生成一次，重画零次。原图在 *_raw.png，完整提示词、输入与定位在 *_generation.json。
- extend 先对生成的透明内容作等比定位：将左侧肩根中心对齐 (1380,665)，爪尖控制在 x≈2000，保留右侧边距。fist 不重新缩放或移动整条手臂。
- 肩到手腕（x≤1834）两帧不同像素：0；红袖子与袖口不同像素：0。
- 肩点 alpha=253；肩点半径80px范围内与 body 的 alpha>20 区域重叠4074像素。合成检查未见明显的肩部贯穿空洞，接合线和画风最终由用户验收。
- 两张四角 alpha 均为0，四条画布边缘 alpha 最大值均为0，没有截断。低透明 alpha≤2 残点清零。

check_extend.png、check_fist.png：按现有 layers.json 的顺序合成，只将 arm_reach 替换为相应新姿势，其余层原样保留；青色小圈标出肩 pivot。检查图有深灰底，实际交付的两张手臂图无底色。

数据见 clench_arm_checks.json。代码绑定时，两张都复用 arm_reach 的 z_index 与 pivot，不要按每张图的包围盒重新居中。

独立分支：codex/p5-arsene-clench-arm。不 fetch、不 merge；只提交本任务新增素材、原图、生成记录、检查图和本说明。未修改 mod 代码、未编译、未启动游戏。
