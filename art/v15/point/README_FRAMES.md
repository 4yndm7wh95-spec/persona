# 伟大封印八帧候选：其余帧先行推送说明

本说明补充此前只提交第8帧的 README.md。2026-10-14，用户要求把其他帧也推送，因此提交剩余七张最佳候选和预览/GIF，全部保留 _wip 标记，不声称完成原任务的严格验收。本次未再次生成或清理PNG，也未做非手臂区域还原；八张的RGBA原像素保持来源文件一致。没有修改代码或既有美术资源。

## 文件与节奏

- point_1_hip_wip.png 至 point_7_diag_wip.png：此前imagegen生成的最佳候选。
- point_8_sky_wip.png：保持用户选定附件，已在提交4cdd701中推送，本次不改它。
- preview_point.png：idle_a + 八帧横排，下面为每帧手部按原像素4倍放大，最右侧为原版6帧对照。上排统一按0.375倍展示，不移动、缩放单独身体部位掩盖对齐差异。
- point_anim.gif：512×768时间预览，idle停0.5秒，帧1～7依次停0.33/0.34/0.33/0.33/0.17/0.17/0.23秒，帧8停1秒，循环。PNG关键帧时刻0/0.33/0.67/1/1.33/1.50/1.67/1.90秒；GIF从帧1到帧8起点为1.90秒。GIF透明度为二值、颜色有量化，不能代表PNG原始alpha质量。未加入溶解过渡，供Claude参考节奏，不是游戏代码实现。

## 每帧信息

全部PNG为1024×1536 RGBA，alpha范围0–254。生成轮数包含首轮；第5帧包含一次手部局部重画，第7和第8帧包含袖子局部修正。第8帧进行了六轮尝试，但用户选择的是首轮同姿势的上传附件，而不是最后一轮。

| 帧 | 首次+重画请求总数 | 手心/拳头近似中心 | 手势 | 还原 |
|---|---:|---|---|---|
| 1 hip | 1 | (520,555) | 轻握拳，0根伸直 | 未做 |
| 2 waist | 1 | (520,500) | 握拳，0根伸直 | 未做 |
| 3 chest | 1 | (490,320) | 握拳，0根伸直 | 未做 |
| 4 chin | 1 | (520,210) | 握拳，0根伸直，不遮眼睛 | 未做 |
| 5 brow | 3 | (565,55) | 闭合拳头，0根伸直；朝向未完全符合手背要求 | 未做 |
| 6 over | 2 | (380,45) | 握拳，0根伸直 | 未做 |
| 7 diag | 4 | (285,65)，指尖约(280,8) | 仅食指伸直，但已伸得较完整 | 未做 |
| 8 sky | 6次候选尝试；提交用户附件 | (330,70)，指尖约(329,3) | 仅食指朝上，其他手指收拢 | 未做 |

坐标为目测估计，不是精确测量；第8帧指尖接近上沿，需进一步审查是否留足边距。原生成输出到用户附件的处理过程未知，不能用候选提示词代替附件处理历史。

## 五条自检（逐帧结果）

1. **手臂侧别/袖章/插袋**：八张均基本通过视觉检查；动的是画面左侧无袖章右臂，红袖章在另一条手臂，另一手仍插袋；无新增道具。
2. **手势**：1～6帧均闭合拳头，没有张开的扇形手掌，零根伸直手指；5帧拳头朝向尚未完全确认符合手背/小指一侧要求。7帧食指已伸得较完整，不是严格的“刚开始”；8帧视觉上正好一根食指朝上。所有帧按WIP保留。
3. **逐像素一致**：八张均未通过严格条件。生成输出对手臂之外也有像素变化，尚未执行蒙版还原；最终选定8帧的人物头与身体位置相对idle_a明显下降。不能宣称脚、腿、头完全对齐。半透明叠加已检查，但未因此修改原图。
4. **路线/节奏**：整体路线是腰带附近→腹部→胸口→下巴→额头→头顶左上→斜上→朝天；相邻位置变化不均匀，2→3、4→5、5→6较大，7→8伴随身体下降；未通过“无大跳”和原版严格一致要求。
5. **透明度/画布**：画布和RGBA通过；严格“轮廓外alpha全部0”未通过，有少量低alpha杂点。没有把黑底或棋盘格烘焙进PNG。GIF有独立二值透明处理，仅对预览文件生效，不修改PNG。

原idle_a底图本身也有3279个离可见主体超过3像素的低alpha杂点，最大alpha7。用户先要求推送选中图，随后要求其他帧也推送；本次仅先行提交WIP原图，未擅自决定逐像素还原与透明杂点清理之间的例外。

下面的透明杂点统计，以alpha≥16为可见主体，向外扩3像素后，统计外部仍有非零alpha的像素。它不能证明已精确分割轮廓；实际严格轮廓外异常数可能更多。

| 帧 | 远离主体的非零alpha像素 | 最大alpha |
|---|---:|---:|
| 1 | 1275 | 4 |
| 2 | 1504 | 5 |
| 3 | 1314 | 7 |
| 4 | 1157 | 2 |
| 5 | 1357 | 7 |
| 6 | 2632 | 7 |
| 7 | 2778 | 5 |
| 8 | 2277 | 3 |

## 最终采用候选提示词

1～7都从idle_a.png进行初次编辑；后续局部编辑使用该帧此前输出，仍属于同一待机底图编辑链。所有请求使用内置imagegen并设置transparent_background=true，没有CLI替代。第8帧以下记录原首轮候选提示词，实际提交文件是用户附件。全身批次生成时未遇安全拒绝。

### 1. point_1_hip_wip.png

总尝试1轮；最终采用首轮。

```text
Edit this EXACT Makoto Yuki full-body illustration, reference image 1 is the edit target idle_a.png. The P3R reference is POSE REFERENCE ONLY, never copy its face, background or colors. Change ONLY his anatomical RIGHT ARM, which is on the LEFT SIDE OF THE IMAGE and has NO RED ARMBAND, plus the clothes immediately uncovered by its movement. Keep the other arm on image RIGHT in its trouser pocket with its red armband exactly unchanged. Keep head, calm face, hair, neck, torso, legs, shoes, holstered Evoker, colors, linework, 1024x1536 canvas and character position identical to target. No camera movement, resizing, repositioning or cropping. Preserve real RGBA transparent background with alpha zero outside character. No aura, shadows, background scene or additional objects. No new weapon or prop. Keep wrist slender and black jacket sleeve with white shirt cuff. The moving hand is narrow, viewed from back of hand / little finger side, palm NOT visible. Not an open hand, not a raised palm, not waving, not a high-five, no spread fingers, no second raised arm. Do not raise the armband arm.
Frame 1 HIP: the image-left right hand begins moving away from the outer thigh toward the FRONT BELT BUCKLE HEIGHT, approximately (470,620). Elbow gently bends near image-left side of body around (335,525); upper arm still mostly down. A softly CLOSED FIST, ZERO straight fingers, thumb resting over curled fingers. Keep fingers together in fist. Don't keep hand hanging at thigh.
```

### 2. point_2_waist_wip.png

总尝试1轮；最终采用首轮。

```text
Edit this EXACT Makoto Yuki full-body illustration, reference image 1 is the edit target idle_a.png. The P3R reference is POSE REFERENCE ONLY, never copy its face, background or colors. Change ONLY his anatomical RIGHT ARM, which is on the LEFT SIDE OF THE IMAGE and has NO RED ARMBAND, plus the clothes immediately uncovered by its movement. Keep the other arm on image RIGHT in its trouser pocket with its red armband exactly unchanged. Keep head, calm face, hair, neck, torso, legs, shoes, holstered Evoker, colors, linework, 1024x1536 canvas and character position identical to target. No camera movement, resizing, repositioning or cropping. Preserve real RGBA transparent background with alpha zero outside character. No aura, shadows, background scene or additional objects. No new weapon or prop. Keep wrist slender and black jacket sleeve with white shirt cuff. The moving hand is narrow, viewed from back of hand / little finger side, palm NOT visible. Not an open hand, not a raised palm, not waving, not a high-five, no spread fingers, no second raised arm. Do not raise the armband arm.
Frame 2 WAIST: the image-left right fist is in front of abdomen at roughly (495,520). Forearm nearly horizontal crossing in front of waist, elbow projects to image LEFT at roughly (295,530). Upper arm down and out from shoulder. CLOSED FIST, ZERO straight fingers, palm not visible.
```

### 3. point_3_chest_wip.png

总尝试1轮；最终采用首轮。

```text
Edit this EXACT Makoto Yuki full-body illustration, reference image 1 is the edit target idle_a.png. The P3R reference is POSE REFERENCE ONLY, never copy its face, background or colors. Change ONLY his anatomical RIGHT ARM, which is on the LEFT SIDE OF THE IMAGE and has NO RED ARMBAND, plus the clothes immediately uncovered by its movement. Keep the other arm on image RIGHT in its trouser pocket with its red armband exactly unchanged. Keep head, calm face, hair, neck, torso, legs, shoes, holstered Evoker, colors, linework, 1024x1536 canvas and character position identical to target. No camera movement, resizing, repositioning or cropping. Preserve real RGBA transparent background with alpha zero outside character. No aura, shadows, background scene or additional objects. No new weapon or prop. Keep wrist slender and black jacket sleeve with white shirt cuff. The moving hand is narrow, viewed from back of hand / little finger side, palm NOT visible. Not an open hand, not a raised palm, not waving, not a high-five, no spread fingers, no second raised arm. Do not raise the armband arm.
Frame 3 CHEST: image-left right CLOSED FIST moves to chest / collarbone level about (520,340); forearm runs nearly horizontally across upper chest, elbow out to image LEFT roughly (280,355), slightly below shoulder. ZERO straight fingers, back of fist toward viewer. Head remains visible.
```

### 4. point_4_chin_wip.png

总尝试1轮；最终采用首轮。

```text
Edit this EXACT Makoto Yuki full-body illustration, reference image 1 is the edit target idle_a.png. The P3R reference is POSE REFERENCE ONLY, never copy its face, background or colors. Change ONLY his anatomical RIGHT ARM, which is on the LEFT SIDE OF THE IMAGE and has NO RED ARMBAND, plus the clothes immediately uncovered by its movement. Keep the other arm on image RIGHT in its trouser pocket with its red armband exactly unchanged. Keep head, calm face, hair, neck, torso, legs, shoes, holstered Evoker, colors, linework, 1024x1536 canvas and character position identical to target. No camera movement, resizing, repositioning or cropping. Preserve real RGBA transparent background with alpha zero outside character. No aura, shadows, background scene or additional objects. No new weapon or prop. Keep wrist slender and black jacket sleeve with white shirt cuff. The moving hand is narrow, viewed from back of hand / little finger side, palm NOT visible. Not an open hand, not a raised palm, not waving, not a high-five, no spread fingers, no second raised arm. Do not raise the armband arm.
Frame 4 CHIN: image-left right CLOSED FIST arrives just IN FRONT OF CHIN about (530,220), forearm across lower face but does not cover eyes. Elbow lifts near shoulder height at image LEFT around (285,290). Back of fist toward viewer, ZERO straight fingers. Calm face unchanged.
```

### 5. point_5_brow_wip.png

总尝试3轮；最终采用第三轮。

```text
Edit this EXACT Makoto Yuki full-body illustration, reference image 1 is the edit target idle_a.png. The P3R reference is POSE REFERENCE ONLY, never copy its face, background or colors. Change ONLY his anatomical RIGHT ARM, which is on the LEFT SIDE OF THE IMAGE and has NO RED ARMBAND, plus the clothes immediately uncovered by its movement. Keep the other arm on image RIGHT in its trouser pocket with its red armband exactly unchanged. Keep head, calm face, hair, neck, torso, legs, shoes, holstered Evoker, colors, linework, 1024x1536 canvas and character position identical to target. No camera movement, resizing, repositioning or cropping. Preserve real RGBA transparent background with alpha zero outside character. No aura, shadows, background scene or additional objects. No new weapon or prop. Keep wrist slender and black jacket sleeve with white shirt cuff. The moving hand is narrow, viewed from back of hand / little finger side, palm NOT visible. Not an open hand, not a raised palm, not waving, not a high-five, no spread fingers, no second raised arm. Do not raise the armband arm.
Frame 5 BROW: match forehead pose reference. Image-left right forearm crosses HORIZONTALLY ABOVE FOREHEAD, elbow on image LEFT at top-of-head height roughly (305,70), CLOSED FIST above forehead around (535,55). No face occlusion, ZERO extended fingers. Keep head location unchanged.
Correction: CLOSED FIST centered (525,50), not to the far right of forehead. Forearm ends ABOVE forehead, knuckles toward camera, thumb hidden around lower edge. Back of hand has no palm crease. Zero fingers extended, no open hand. Elbow on image left (320,75). Shorter horizontal forearm permits keeping fist closer to center above brow. Keep hair/face original, no face occlusion. Hand must be visible entirely inside top edge.
```

### 6. point_6_over_wip.png

总尝试2轮；最终采用第二轮。

```text
Edit this EXACT Makoto Yuki full-body illustration, reference image 1 is the edit target idle_a.png. The P3R reference is POSE REFERENCE ONLY, never copy its face, background or colors. Change ONLY his anatomical RIGHT ARM, which is on the LEFT SIDE OF THE IMAGE and has NO RED ARMBAND, plus the clothes immediately uncovered by its movement. Keep the other arm on image RIGHT in its trouser pocket with its red armband exactly unchanged. Keep head, calm face, hair, neck, torso, legs, shoes, holstered Evoker, colors, linework, 1024x1536 canvas and character position identical to target. No camera movement, resizing, repositioning or cropping. Preserve real RGBA transparent background with alpha zero outside character. No aura, shadows, background scene or additional objects. No new weapon or prop. Keep wrist slender and black jacket sleeve with white shirt cuff. The moving hand is narrow, viewed from back of hand / little finger side, palm NOT visible. Not an open hand, not a raised palm, not waving, not a high-five, no spread fingers, no second raised arm. Do not raise the armband arm.
Frame 6 OVER: image-left right forearm rises, CLOSED FIST at upper-left of head around (330,35), elbow bent about 90 degrees, above and outside left shoulder around (275,175). ZERO extended fingers, wrist straight, back of fist visible. Keep all fingers inside canvas.
Correction: the fist must be near the top edge at (350,25) and the elbow HIGH ABOVE original shoulder at (285,155), NOT at (250,280). Upper sleeve goes UP LEFT from shoulder to elbow. Forearm goes UP RIGHT from elbow to fist, elbow roughly90degrees. Compact foreshortening is acceptable to keep head at y10 and shoes at y1518. Back of closed fist visible, zero extended fingers. No head movement.
```

### 7. point_7_diag_wip.png

总尝试4轮；最终采用第四轮。

```text
Edit ONLY raised black jacket sleeve on IMAGE LEFT of this exact illustration, within x265..445 y140..360. Keep pointing hand, wrist, white cuff and entire body position unchanged. Remove the pronounced downward elbow corner at (345,300). Replace sleeve path by nearly straight diagonal from cuff (295,145) to original shoulder (410,260), gentle bend only. Sleeve should project diagonally UP LEFT about60degrees when including hand. Upper arm must no longer slope down from shoulder before bending up. Preserve black fabric angular cel shading. Hand remains one index beginning to extend, all other fingers closed, no palm. All other pixels unchanged. RGBA transparent 1024x1536.
```

### 8. point_8_sky_wip.png

总尝试6轮；最终采用用户选定附件（首轮同姿势）。

```text
Edit this EXACT Makoto Yuki full-body illustration, reference image 1 is the edit target idle_a.png. The P3R reference is POSE REFERENCE ONLY, never copy its face, background or colors. Change ONLY his anatomical RIGHT ARM, which is on the LEFT SIDE OF THE IMAGE and has NO RED ARMBAND, plus the clothes immediately uncovered by its movement. Keep the other arm on image RIGHT in its trouser pocket with its red armband exactly unchanged. Keep head, calm face, hair, neck, torso, legs, shoes, holstered Evoker, colors, linework, 1024x1536 canvas and character position identical to target. No camera movement, resizing, repositioning or cropping. Preserve real RGBA transparent background with alpha zero outside character. No aura, shadows, background scene or additional objects. No new weapon or prop. Keep wrist slender and black jacket sleeve with white shirt cuff. The moving hand is narrow, viewed from back of hand / little finger side, palm NOT visible. Not an open hand, not a raised palm, not waving, not a high-five, no spread fingers, no second raised arm. Do not raise the armband arm.
Frame 8 SKY: match final P3R pose: image-left right arm nearly straight vertical UP, leaning image-left outward only 5 to 15 degrees. Upper arm above shoulder, elbow straight or as close as possible, wrist straight. Hand in top-left area of canvas, fully visible. CLOSED FIST with ONLY INDEX FINGER extended vertically UP (number one counting gesture). Middle, ring and little fingers tightly curled, thumb over curled middle finger. Back / little finger side of hand to viewer, NO PALM visible, narrow vertical hand silhouette. Do not move head or feet to fit. Keep fingertip inside top edge, do not crop it.
```

## 后续工作

若继续完成正式八帧交付，需要决定背景透明杂点例外，再执行非手臂像素还原、适当姿势修正和动画连贯性处理。本批只供审阅，不能当作已经达标的完整逐帧动画。

