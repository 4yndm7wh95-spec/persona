# 指天手臂第二批检查报告

日期：2026-10-14。生成工具：内置 imagegen。仅新增美术文件，没有修改代码或既有资源。

## 结论

本批保留两张可审阅的透明 PNG，但**未完全通过要求**。手势和单手臂内容基本满足；整体尺寸偏大，轮廓之外有极少量 alpha=1 的杂点，无法保证严格逐像素 alpha=0。已达到每张四轮的上限，按要求如实保留问题。不能把本批称为完全合格或直接完成游戏接入。

最终文件：
- arm_point_up.png：选用指天第二轮，手指排列及拇指压在收拢手指上的表现较清楚。
- arm_point_mid.png：选用过渡第四轮，掌心朝前，四指接近并拢。
- preview_point.png：两张手臂并排置于浅灰棋盘格，右侧原画缩略图。三张都按原画布 0.5 倍展示，用于直接观察尺寸差异；没有把零件缩成合格比例来掩盖问题。
- REPORT_POINT2.md：本报告。

## 轮次及拒绝记录

指天：共四轮成功生成，重画三次。第一轮约 1280 像素高，第二轮约 1081 像素高，第三轮约 912 像素高，第四轮约 1021 像素高（均取 alpha≥16 主体包围框高度）。尺寸指令执行不稳定；第三和第四轮手指收拢形态不如第二轮符合拇指位置要求，因此选择第二轮。没有安全拒绝。

过渡：共四次请求，首轮被输出审核拒绝，后三轮成功。首轮拒绝返回 HTTP 400 / moderation_blocked，safety_violations=[abuse]，categories=[other]，请求 ID a7462c73-af8e-44c7-85c8-5296ed1192a9。按用户指示，用中性的纸偶服装动画零件描述重试一次成功。之后两次为尺寸及姿势质量修正，非拒绝重试。未绕过安全拒绝，没有恢复使用 cut / severed / detached 描述。第二轮尺寸明显偏大；第三轮缩小有效，第四轮再次缩小效果有限，最终保留第四轮。

**原因说明：** 本次首轮过渡请求没有使用上次的 severed 等词，仍遭拒绝，因此不能把先前拒绝确定归因于那个词。服务没有解释具体触发内容，可能涉及生成画面的审核误判，但未证实。指天四轮均成功，不能说指天手势本身遭禁止。

## 实测与近似坐标

PNG 均为 RGBA、1024×1536；原始生成文件直接复制，没有离线修图、裁切、缩放或 alpha 清理。

包围框采用 (left, top, right, bottom)，右与下界不包含在内。
“所有非零 alpha 框”包含极低透明度杂点；“主体框”使用 alpha≥16，使可见主体的大小可读。主体框不等同于已精确分割的轮廓。

| 项目 | arm_point_up.png | arm_point_mid.png |
|---|---|---|
| 所有非零 alpha 包围框 | (161,159,866,1376) | (153,117,858,1432) |
| alpha≥16 主体包围框 | (418,210,622,1291)，204×1081 | (313,466,715,1069)，402×603 |
| alpha≥128 主体包围框 | (419,211,622,1291) | (314,467,714,1068) |
| 肩部布料接缝中心，目测 | 约 (497,1255) | 约 (344,1013) |
| 指尖，目测 | 食指约 (513,212) | 最高指尖约 (675,468) |
| 肩接缝中心到最高指尖直线距离 | 约1043像素 | 约638像素 |
| alpha范围 | 0–254 | 0–254 |
| alpha=0 的像素数 | 1,442,183 / 1,572,864 | 1,505,948 / 1,572,864 |
| 主体框向外扩2像素以外的非零像素数 | 38，最大alpha=1 | 46，最大alpha=1 |

肩部与指尖为目测坐标，约有数像素至十余像素误差；直线距离是便于缩放的估计，不是沿关节路径的测量。主体框以外的杂点统计只是可复查的下限，不代表已统计所有轮廓外像素。

## 视觉检查及已知问题

- 指天：仅一条右臂和一只手，食指直立向上，其余手指收拢，拇指覆盖收拢的手指；无头、身体、道具。上臂近乎竖直，但倾角不足明确的5°～10°，袖子肘部有折痕，不能声称肘部完全伸直。
- 过渡：仅一条手臂和一只手，掌心朝前、肘部微弯，四指接近并拢但有小缝隙。肩接缝中心到指尖方向约59°，比指定的约45°更陡。
- 两张有黑色西装袖、白衬衫袖口、红袖章及黑边条；袖章未见文字，无水印。风格接近原画，但手部相对原画仍偏大，无法保证只靠整体缩放就完全匹配比例。
- 大片背景确为透明，不是绿幕或绘制的棋盘格。合成到灰底后未见可见光晕；直接查看RGB时显示的红白光晕多数位于alpha=0区域，不能把隐藏RGB当成可见背景。确实仍存在alpha=1杂点，因此严格的“轮廓外全部alpha=0”未通过。
- 主体最大alpha为254，未达到255，说明生成输出整体没有全不透明像素；视觉影响很小，但记录为已知属性。
- 两张尺寸未达到肩部到指尖约400～480像素。按约450像素直线距离估计，指天需缩放约0.43倍，过渡约0.71倍，仅供后续对齐参考；本次没有修改任何代码或执行缩放。
- 不能仅以附有alpha通道判定完全合格。预览展示的是实际透明合成结果，仍应处理极低alpha杂点并核对尺寸后再接入。

## 最终采用图的完整提示词

### arm_point_up.png（第二轮）

参考：第一轮指天输出，编辑请求；transparent_background=true。

```text
Edit this paper-doll arm animation sprite into a clean game-ready PNG. Keep its exact pointing gesture, suit design, plain red armband, white cuff and linework. Make the WHOLE arm approximately one third of its current size: entire arm only 450 pixels high on a 1024x1536 canvas, centered in the canvas. Most of the canvas is EMPTY TRANSPARENT. Remove every bit of ambient glow / red aura / white halo surrounding the sprite. Background must be truly transparent alpha=0, with transparency immediately outside its crisp black outline. No background lighting, no shadow, no fog. Preserve opaque black fabric and opaque skin, use alpha antialiasing solely within 1 pixel of outline. One clean right arm, index finger points vertically up, remaining fingers curled, thumb over middle finger. Neat cloth seam at shoulder. No extra objects or body. Do not recreate surrounding illumination.
```

### arm_point_mid.png（第四轮）

参考：第三轮过渡输出，编辑请求；transparent_background=true。

```text
Keep this exact paper-doll uniform arm illustration and pose, but REDUCE THE WHOLE SPRITE TO 60 PERCENT of its present scale, centered in the same 1024x1536 transparent canvas. Current artwork is around 440x620 pixels; output artwork should be around 265x372 pixels, with shoulder-to-fingertip length around 450 pixels. There must be more empty transparent space than now on all sides. The fingers should be close together, palm toward viewer, right sleeve diagonally upward 45 degrees, slight elbow bend, plain red armband near shoulder seam and white shirt cuff. Keep one hand and one sleeve. True RGBA transparency, alpha zero beyond crisp outline, no visible lighting effects or backdrop or shadow. Preserve anime linework and uniform design.
```

## 指天最后一次尝试提示词（第四轮，未采用）

参考：makoto_rig_master.png；transparent_background=true。

```text
A single right arm sprite for a 2D paper-doll puppet of Makoto Yuki. Sleeve ends neatly at the shoulder seam with a clean fabric edge used for puppet assembly. Use image 1 for anime art style, black student uniform jacket fabric, angular cel shaded folds, line weight, pale slender hand proportions; image 2 for sleeve style only. Plain red upper-arm armband with black border stripes and NO writing, white shirt cuff at wrist. Only one sleeve and one hand. No head, torso, second hand, props, letters or watermark. Exact 1024x1536 RGBA canvas, genuine transparent background, every pixel outside the arm silhouette alpha=0. Crisp anti-aliased edges only; no shadow, glow, aura, backdrop, gradient or checkerboard pattern. Arm centered on canvas with generous transparent margins. Shoulder seam to fingertip about 400 to 480 pixels total length, matching arm proportions in master. Do not fill canvas with oversized arm. Draw a small full-length upright paper-doll arm asset in the middle of a LARGE EMPTY page. Its full height should be 450 pixels, NOT 900 or 1200 pixels. For scale only: the top fingertip near y=550 and the shoulder fabric seam near y=1000. All pixels above y=500 and below y=1050 are transparent. Right elbow straight, raised nearly vertically outward 5 degrees, index pointing vertically UP; three other fingers closed, thumb across middle finger. Red armband at bottom near shoulder seam. Use master reference to keep arm width and hand size consistent.
```

生成策略：始终使用内置 imagegen，没有使用API/CLI替代。未启动游戏、未读取密码或受禁止文件、未改游戏原文件、未改Git历史或全局配置。

