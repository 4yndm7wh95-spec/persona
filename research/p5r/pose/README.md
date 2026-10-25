# P5 阶段 1：Joker 战斗姿势参考库

任务：`docs/codex/codex_p5_stage1.md` A 部分；日期 2026-10-25；基线 `6c7c3378`，分支 `claude/amazing-darwin-b00arn`。

已渲 **64 段、384 帧**，每段均匀取 6 帧，包含首尾。结果是 `sheet_01.png`～`sheet_08.png`，每张 8 段，条内每帧 256×384，条顶写顺序号、原始 GAP 动作名、30fps 参考帧数和时长。`index.csv` 有全部 64 条原名、本机动作 GLB 路径、参考帧数、时长、看图描述及 sheet/行号。

## 方法和限制

- 模型：本机资源库 `C:\Users\kkk\sts2_tools\p5r_rip\model\joker\fixed2\joker_thief_nomask.glb`，来自已验收的 `--drop kamen,Blur --flip-v` 输出。
- 动作：`research/p5r/joker_battle_anim_list.csv` 内 original_name 以 `BB0001_051.GAP#` 开头的 64 条。原始 main 编号不连续；本库 number 1–64 是联系表顺序，原始名不改。
- 一律采用第三轮 **C 方案，只套 rotation**，不套 translation/scale；静止位移和缩放使用模型自身。按精确节点名映射动画，筛掉非 rotation channel，关键帧值保持不变。
- Blender 4.5.9 LTS，Workbench FLAT/TEXTURE，正交相机、透明 RGBA，方向沿用此前朝画面右侧的 3/4 视角，无阴影、描边、景深、辉光。
- 与此前动作诊断不同，**每一采样帧根据变形后网格的实际包围范围重新居中、缩放**；固定相机朝向，仅调整位置和正交大小，不截断头、脚或甩出的衣摆。横卧、下蹲等宽姿势会因宽度适配而显得矮一些，不把它们拉长。
- 时间均为 `duration_seconds * k/5`，k=0…5。Blender 以 30fps 参考时基加 subframe 求值，保留原时长；CSV 的帧数是上一轮元数据里的 30fps 参考帧数，源 FPS 未知。

## 看图检查

已逐段查看全部 64 条的六帧图。**明显扭曲/出框异常：0 段**。另外逐张检查 384 张原帧 RGBA、256×384、非空，人物 alpha 没有触及图像四边；不把几何包围盒检查冒充真实游戏验收。

描述仅基于六帧可见姿势：例如站姿待机、俯卧起身、前倾奔跑、翻滚、举掌、手靠脸等。模型没有附加刀/枪等武器，未把伸手姿势写成实际持刀/举枪。长段（如 16～20 秒）仅六点稀疏取样，具体含义不明确的条目写明“不确定”，不猜用途。

此库只作生图的姿势参考；只套旋转会丢失原始位移、缩放信息，不能作为原版动作完整复现或直接入游戏的动画成品。背身、倒地、翻转、侧卧等可见姿势按原样保留，没有逐段修动作。

## 复现与本机中间资料

执行脚本在 `C:\Users\kkk\sts2_tools\p5r_tools\scripts\`，本目录 `scripts/` 提供同名可审阅副本：

```text
python p5_stage1_poses.py
blender -b --python p5_stage1_render_poses.py
python p5_stage1_pose_sheets.py
```

脚本固定本机已有路径。`p5_stage1_poses.py` 调用上一轮的 `attach_animation_by_name.py`；它的已提交副本在 `research/p5r/model/diag/scripts/`。描述是人工看图后填写，不是从文件名自动猜测。

本机临时合成 GLB、逐帧原图和完整相机/包围范围记录：`C:\Users\kkk\sts2_tools\p5r_rip\model\pose_stage1\`，`render_records.json` 可复核每帧单独取景。临时 GLB 和逐帧图未提交，仓库只留 8 张联系表、索引、说明和脚本。

只提交 `research/p5r/pose`，不改 mod 或现有资源，不运行游戏/Computer Use。art/cards 的另一个会话文件原样保留，未提交、stash 或还原。
