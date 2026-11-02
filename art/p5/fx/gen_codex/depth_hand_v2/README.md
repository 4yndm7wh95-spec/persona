# 纵深侧握之手 v2：单帧待确认

## 生成与选择

- 文件：`depth_grip.png`。
- 使用内置 imagegen，共生成 **1 次**，选用第 1 张；没有使用其余两次额度，没有安全拒绝。
- 输入参考：`art/p5/fx/gen_codex/grip_hand/grip_fist.png`，只参考黑红平面漫画画风，不沿用横向手臂构图。
- 原始 PNG 直接复制保存；未脚本翻转、缩放、裁剪、挖空或清理，未修改像素。
- 实际尺寸：**1254×1254**，正方形。读取像素格式为 Format32bppArgb（带 alpha 的 PNG）。只读取文件元数据，没有图像处理。

选这一张的原因：小而后退的手腕与烟丝已位于画面中央偏上，前方手和指节明显放大，前臂被缩短为短截，没有从左/右画框边缘横着伸入；同时保持拇指在上、四指上下排列并从右往左扣的侧握。与上一轮相比，中央纵深关系更明确，已有合适的单帧供用户确认，因此没有继续生成。

## 必须满足项检查

1. **透视：目视符合。** 手腕位于中央偏上远处，明显小于近处手部，烟往上后方散；大手在前，没有可见长臂从左右边缘进入。
2. **握姿：目视符合。** 拇指位于顶部并向下压，四根弯曲手指从上到下叠排、横向从右往左扣，指尖朝左，掌心朝左；不是整掌正对镜头或手背朝上的俯抓。
3. **画布/透明/数量：** 正方形1254×1254，PNG带alpha；生成时设置 transparent_background=true，目视未见独立背景底板、地面或阴影，只有一只手。未逐像素测量轮廓外alpha值。未画可见圆柱；前景手指保留，指间留出空隙，内部遮挡透明区域仍由用户验收。

本轮只提交这一张，**等待用户确认后才生成其余姿势**。未改代码、编译、启动游戏或使用 Computer Use；未安装软件、改全局Git配置或系统环境，未进入旧仓库，其他会话文件未动。

## 完整英文提示词

Create ONE square transparent PNG game-effect sprite. Use the attached image ONLY for Persona 5 flat comic art style: a pure-black demonic clawed hand, hard edges, thin sharp red #e3121c highlights on knuckles and finger edges, thick black outlines, a little dark red. Do NOT copy the reference's horizontal arm, composition, viewing angle or fist pose.
MOST IMPORTANT — CAMERA AND DEPTH: the forearm points straight at the lens along the viewing axis, coming from a vanishing point INSIDE THE IMAGE near the upper center. Extreme foreshortening: the distant wrist is TINY, dark, and recessed at about (50% width, 28% height), dissolving into dark-red/black smoke trailing UP AND BACK into depth. The visible forearm is only a very short stub because it is almost end-on to camera. A HUGE near hand occupies about 75% of the square. No arm reaches from either image edge. No long horizontal or diagonal forearm. The tiny wrist must NOT sit on the far left or right.
GRIP ORIENTATION is separate from the camera direction: the hand is upright sideways like a handshake or holding a vertical drinking glass. Thumb is TOP, little finger BOTTOM, PALM FACES SCREEN LEFT. The hand is to the right/rear of an invisible upright cylinder centered in the image, about 35% image width. Four curled fingers wrap horizontally FROM RIGHT TO LEFT across its FRONT, one finger above another in FOUR HORIZONTAL ROWS, fingertips pointing LEFT. Thumb presses down from ABOVE. Show curved finger backs/knuckles and a little inner-palm edge in three-quarter view. NOT an overhead palm-down grab, NOT a palm facing directly at camera.
OCCLUSION: do not draw the cylinder at all. Its empty area is truly transparent, not a black or gray solid. Omit the rear palm where the invisible cylinder occludes it; keep all foreground finger parts completely rendered across that transparent region. Do not draw any person, victim, sphere, glass, actual cylinder or target.
Exactly ONE hand. Genuine RGBA transparency: alpha=0 everywhere outside hand and smoke, including the invisible-object cutout. No background, white fill, checkerboard, green screen, ground, cast shadow, flames, text, blood, bones or wounds. Flat hard-edged illustration, not 3D or realistic skin. Square canvas, suggested 1024x1024. Strong perspective must be obvious: tiny CENTER-BACK wrist, huge FRONT hand.

