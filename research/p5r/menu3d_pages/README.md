# P5R 菜单子页 Joker 入场重辨识 v2

## 片段

| 页面 | 导出范围 | 对照结果 |
| --- | --- | --- |
| SKILL 近景 | `skill/near/cross_113_20/f0000–f0006` | 以 `main_0015` f0004–0010 为入场源，从 `main_0113` f0000 开始线性淡入 0.20 秒。权重从 0 到 1，逐骨骼插值位置和缩放、球面插值旋转。复查 0111 时取混合帧 f0004，肩部倾斜、手肘收向腰部和侧脸比直接播放抬膝段更接近。 |
| SKILL 中景 | `skill/mid/main_0113/f0006–f0028` | 0113、0117、0120 分别对照 f0006、f0010、f0013。接过渡片最后同一姿势，再保留约 0.77 秒的连续 30 fps 原动画。近景与中景合计 30 张，切镜头交界有同一张姿势。上一轮独立播放的 `main_0015/0016/0017` 已移除。 |
| ITEM | `item/main_0010/f0000–f0010` | 保留上一轮片段。0180 手臂向画面右侧，0184 张指扫过脸前，0186 脸重新露出。改为移动镜头，最后几帧视角绕近正面，补偿前后截图中脸相对手的位置变化。 |
| EQUIP | `equip/main_0005/f0000–f0010` | 保留上一轮片段。0236 手过头顶，0240 枪斜举，0242 枪口转向右下；持枪手、立领和头部转向一致。保留上一轮的旋转和 FOV，随入场将人物从画面偏右移至左侧。 |

SKILL 是目前的最佳拟合，GAP 文件只有动作数据，无法确认原版菜单状态机实际调用的片段或权重。中景的 `main_0113` 是有位移的动作，靠独立镜头跟随保留参考中的构图。原版中 0113–0120 可见区被队伍列表、字样和遮罩挡住。早期个别帧露出的腿部、0120 的抬头和表情仍有偏差，双手是否真的始终放在口袋里也无法从画面确认。ITEM 的手指、EQUIP 的举枪在极近镜头仍有跨出画面边缘的帧，落到身体中部的原版遮罩由 UI 负责。

## 全身和混合筛查

- 43 段有效 GMD/GAP 动作共渲染 474 个全身样本。短段逐帧，时长至少 1 秒的长段每五帧加片尾。全身机位约偏侧 30°，按各帧怪盗服网格的包围盒跟随并拉远。服装、051B 手套、纹理和剔除规则沿用上一轮。
- `blend_0000` 为 0.1667 秒，`blend_0001` 为 5.3333 秒，均标为 `Bit0, HasBoundingBox`。两段都有 root、Bip01、骨盆、脊椎、头、双臂、腿和脚趾轨道，并非只写上半身。轨道都能映射，直接当绝对姿势会压扁模型。这支持它们还需要额外解释或基姿，标志位本身不能证明引擎如何叠加。
- `main_0015` 分别向 `main_0100–main_0113` 做 0.10／0.20／0.30 秒交叉淡入，也试了 `main_0022` 和 `main_0031` 两个短候选。多数长段进入弓步、下蹲、翻越或离开机位。`0113` 过渡时更早恢复直立和收肘。`0022` 的后侧衣摆外扬，`0031` 的一只手明显垂在腰下。
- 以 60% 权重分别对两段 blend 试了两种做法。旋转叠加使用相对各段首帧的四元数差。覆盖把有轨道的骨骼插值到blend 采样姿态，缺失的 PRS 通道沿用导出器绑定值。覆盖容易挤压或扭转，旋转叠加大体保留原基姿，两种都未比选中交叉淡入更匹配。试验参数见 `mixes.json`。

## 相机

位置为模型坐标，旋转为 Godot X/Y/Z 欧拉角（度），FOV 为垂直视角。都是零偏移、远裁剪 500。逐帧参数见 `cameras.json`。原版动作包没有相机节点，这些是拟合机位。

| 相机 | 起点 → 终点位置 (X, Y, Z) | 旋转 (X, Y, Z) | FOV |
| --- | --- | --- | ---: |
| SKILL 近景 | (−20.4874, 119.7669, 89.2777) → (−6.8441, 107.0228, 156.4887) | (12, −23, 24) | 42° |
| SKILL 中景 | (−15.5384, 82.3588, 200.4132) → (−30.7430, 86.3352, 290.0296) | (12, −23, 17) | 42° |
| ITEM | (−93.7960, 148.6193, 51.2803) → (11.6490, 140.0020, 91.8124) | (−1, −45 → −9, −2) | 39° |
| EQUIP | (34.3871, 105.4210, 104.1572) → (58.0155, 98.6640, 71.2503) | (20.95, 25.78, −12.71) | 30.49° |

## 复现

从仓库根目录运行。使用本机既有 GFDLibrary、dotnet、Godot 4.5.1 和 Python 的 NumPy/Pillow。工作树分开，上一版中其他未提交的帧保持原样。dotnet 工具目录只有 .NET 10 运行时，启动 net9 导出器时用 `--roll-forward Major`。

```powershell
$repo = (Get-Location).Path.Replace('\', '/')
$tool = "$repo/tools/p5/menu3d"
$out = "$repo/research/p5r/menu3d_pages"
$dotnet = 'C:/Users/kkk/sts2_tools/dotnet/dotnet.exe'
$godot = 'C:/Users/kkk/sts2_tools/godot/Godot_v4.5.1-stable_win64_console.exe'
& $dotnet build "$tool/Export.csproj" -c Release
& $dotnet --roll-forward Major "$tool/bin/Release/net9.0/Export.dll" "$repo/research/p5r/model/joker/source" "$tool/scratch" "$out/mixes.json"
py "$tool/fullbody_sheets.py" plan
& $godot --path $tool --script capture.gd -- $repo "$tool/scratch" pages "$tool/scratch/fullbody.json"
py "$tool/fullbody_sheets.py" sheet
& $godot --path $tool --script capture.gd -- $repo $out pages "$out/cameras.json"
py "$tool/page_sheets.py"
```

52 张透明 PNG，3840×2160 以 4× MSAA 渲染，缩到 1920×1080，保留直通 alpha。姿势对照使用任务单列出的 `ref_3d_*.jpg`。连续对照使用完整原始帧序列，两组参考的转场遮挡位置有差异，姿势核对中的 ITEM 0184 和 EQUIP 0240 因此采用较靠后的取样。输出目录约 15.1 MB，最大单文件约 3.42 MB。

[全身筛查](sheets/all_clips_fullbody.png) · [交叉淡入试验](sheets/cross-review.jpg) · [分层试验](sheets/layer-review.jpg) · [SKILL](sheets/skill_pose_check.png) · [ITEM](sheets/item_pose_check.png) · [EQUIP](sheets/equip_pose_check.png)
