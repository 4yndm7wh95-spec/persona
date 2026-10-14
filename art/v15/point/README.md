# 伟大封印 · 指天全身帧（当前先行推送）

2026-10-14：用户选中本次上传的指天图，要求先推送这一张。当前仅提交该图和本说明，不代表八帧动画已完成。

## 当前文件

- `point_8_sky_wip.png`：直接复制用户选定的附件，未裁切、缩放、重绘或清理透明度；1024×1536，RGBA，alpha范围0–254。它和此前imagegen输出不完全相同，因此以用户提供的附件为准，保留原像素。
- 用户认可的是这一张的指天姿势。画面左侧右臂向上伸展，红袖章仍在画面右侧另一条手臂上，另一只手插袋。举起的手只有食指伸直，其余手指收拢。

## 尚未通过或尚未完成的检查

- 该图尚未用idle_a.png还原非手臂区域，也未证明头、躯干、腿、鞋与待机图逐像素一致；人物位置与待机图存在差异，因此使用`_wip`文件名。
- 有真正的alpha透明通道，但尚未清理并证明轮廓外所有像素alpha=0。用户这次要求先推送选定图，没有确认之前提出的透明杂点清理例外。
- 原底图idle_a.png轮廓周围存在低alpha杂点：以alpha≥16的主体轮廓向外扩3像素后统计，外部还有3279个非零alpha像素，最大alpha为7。精确保留原底图与清理这些像素会发生要求冲突，尚待后续决定。
- 八帧连贯性、手部四倍放大检查、蒙版还原、自检表、静态总览及point_anim.gif尚未完成。本张不能作为完整八帧交付宣称合格。

## 来源及提示词记录

这次先行提交文件的直接来源是用户附件；未对附件再次调用imagegen。此前产生同姿势候选的指天首轮使用idle_a.png为编辑目标、P3R最终帧为姿势参考，要求只改变画面左侧无袖章的右臂，保持其他部位，并使用真实透明背景。原提示词如下（不是对此附件重新生成的记录）：

```text
Edit this EXACT Makoto Yuki full-body illustration, reference image 1 is the edit target idle_a.png. The P3R reference is POSE REFERENCE ONLY, never copy its face, background or colors. Change ONLY his anatomical RIGHT ARM, which is on the LEFT SIDE OF THE IMAGE and has NO RED ARMBAND, plus the clothes immediately uncovered by its movement. Keep the other arm on image RIGHT in its trouser pocket with its red armband exactly unchanged. Keep head, calm face, hair, neck, torso, legs, shoes, holstered Evoker, colors, linework, 1024x1536 canvas and character position identical to target. No camera movement, resizing, repositioning or cropping. Preserve real RGBA transparent background with alpha zero outside character. No aura, shadows, background scene or additional objects. No new weapon or prop. Keep wrist slender and black jacket sleeve with white shirt cuff. The moving hand is narrow, viewed from back of hand / little finger side, palm NOT visible. Not an open hand, not a raised palm, not waving, not a high-five, no spread fingers, no second raised arm. Do not raise the armband arm.
Frame 8 SKY: match final P3R pose: image-left right arm nearly straight vertical UP, leaning image-left outward only 5 to 15 degrees. Upper arm above shoulder, elbow straight or as close as possible, wrist straight. Hand in top-left area of canvas, fully visible. CLOSED FIST with ONLY INDEX FINGER extended vertically UP (number one counting gesture). Middle, ring and little fingers tightly curled, thumb over curled middle finger. Back / little finger side of hand to viewer, NO PALM visible, narrow vertical hand silhouette. Do not move head or feet to fit. Keep fingertip inside top edge, do not crop it.
```

当前先行提交只新增美术文件及说明；没有修改代码、既有资源或游戏文件，没有启动游戏，没有改写Git历史。
