# 咒怨之手素材

用途：Persona 5 风格虚拟游戏诅咒技能特效。使用内置 imagegen；四张各生成一次，无重试、无重画。未修改代码、未编译、未启动游戏。

## 清理方法

以 alpha > 0 的八连通区域检查孤立杂点；不含任何 alpha >= 20 像素的孤立区域清为透明。另将所有 alpha = 0 像素的隐藏 RGB 清零，避免后续采样出现底色。保留主体和抗锯齿边缘，不裁切、不缩放。

## hand_reach.png

### 完整提示词

Use case: stylized-concept. Asset: a single fictional curse-hand sprite for a Persona 5 style curse skill in a video game. Exactly one hand emerging upward from below ground; draw no ground. Wrist tapers gradually downward and is cut off flush at the canvas bottom edge; opaque wrist must touch the bottom edge. Flat Persona 5 comic style: pure black silhouette hand, crisp hard edges, thin sharp red #e3121c highlights along finger edges and knuckles, a little dark red, thick black outlines. Long slender fingers with sharp clawlike tips, slightly twisted supernatural tension. No realistic skin, no 3D look, no gradient glow. No blood, bones, wounds. True transparent PNG RGBA background: alpha zero outside the hand. No white background, green screen, shadow, ground, flame, text, extra objects. Portrait canvas exactly 1024x1536. Hand centered, hand and wrist occupy about 85% of canvas height, with wrist ending at bottom edge. Only one hand.
Pose hand_reach.png: five fingers spread apart, palm facing the viewer, hand reaching straight upward grasping toward the sky.

### 检查结果

- 画布：1024×1536；模式：RGBA。
- 已清除低透明度孤立像素：1415 个；完全透明像素的 RGB 已清零。
- 轮廓外背景 alpha 为 0；四角 alpha：[0, 0, 0, 0]。
- 手腕碰到底边：是，底行非透明像素 465 个。
- 仅一只手：是；主体连通区域数 1（仅作技术参考）。
- 白边或底色残留：未见白边；非透明近白像素 0 个。透明区域无可见底色；红色为手部描边。

## hand_slam.png

### 完整提示词

Use case: stylized-concept. Asset: a single fictional curse-hand sprite for a Persona 5 style curse skill in a video game. Exactly one hand emerging upward from below ground; draw no ground. Wrist tapers gradually downward and is cut off flush at the canvas bottom edge; opaque wrist must touch the bottom edge. Flat Persona 5 comic style: pure black silhouette hand, crisp hard edges, thin sharp red #e3121c highlights along finger edges and knuckles, a little dark red, thick black outlines. Long slender fingers with sharp clawlike tips, slightly twisted supernatural tension. No realistic skin, no 3D look, no gradient glow. No blood, bones, wounds. True transparent PNG RGBA background: alpha zero outside the hand. No white background, green screen, shadow, ground, flame, text, extra objects. Portrait canvas exactly 1024x1536. Hand centered, hand and wrist occupy about 85% of canvas height, with wrist ending at bottom edge. Only one hand.
Pose hand_slam.png: wrist bent forward, palm facing downward, all five fingers held close together but slightly separated, a downward slapping pose. Palm is in the upper portion of the image, poised to descend diagonally toward the lower right.

### 检查结果

- 画布：1024×1536；模式：RGBA。
- 已清除低透明度孤立像素：761 个；完全透明像素的 RGB 已清零。
- 轮廓外背景 alpha 为 0；四角 alpha：[0, 0, 0, 0]。
- 手腕碰到底边：是，底行非透明像素 393 个。
- 仅一只手：是；主体连通区域数 1（仅作技术参考）。
- 白边或底色残留：未见白边；非透明近白像素 0 个。透明区域无可见底色；红色为手部描边。

## hand_claw.png

### 完整提示词

Use case: stylized-concept. Asset: a single fictional curse-hand sprite for a Persona 5 style curse skill in a video game. Exactly one hand emerging upward from below ground; draw no ground. Wrist tapers gradually downward and is cut off flush at the canvas bottom edge; opaque wrist must touch the bottom edge. Flat Persona 5 comic style: pure black silhouette hand, crisp hard edges, thin sharp red #e3121c highlights along finger edges and knuckles, a little dark red, thick black outlines. Long slender fingers with sharp clawlike tips, slightly twisted supernatural tension. No realistic skin, no 3D look, no gradient glow. No blood, bones, wounds. True transparent PNG RGBA background: alpha zero outside the hand. No white background, green screen, shadow, ground, flame, text, extra objects. Portrait canvas exactly 1024x1536. Hand centered, hand and wrist occupy about 85% of canvas height, with wrist ending at bottom edge. Only one hand.
Pose hand_claw.png: fingers bent into a claw shape, sharp fingertips pointing upward, as if about to grasp something.

### 检查结果

- 画布：1024×1536；模式：RGBA。
- 已清除低透明度孤立像素：534 个；完全透明像素的 RGB 已清零。
- 轮廓外背景 alpha 为 0；四角 alpha：[0, 0, 0, 0]。
- 手腕碰到底边：是，底行非透明像素 460 个。
- 仅一只手：是；主体连通区域数 1（仅作技术参考）。
- 白边或底色残留：未见白边；非透明近白像素 0 个。透明区域无可见底色；红色为手部描边。

## hand_grab.png

### 完整提示词

Use case: stylized-concept. Asset: a single fictional curse-hand sprite for a Persona 5 style curse skill in a video game. Exactly one hand emerging upward from below ground; draw no ground. Wrist tapers gradually downward and is cut off flush at the canvas bottom edge; opaque wrist must touch the bottom edge. Flat Persona 5 comic style: pure black silhouette hand, crisp hard edges, thin sharp red #e3121c highlights along finger edges and knuckles, a little dark red, thick black outlines. Long slender fingers with sharp clawlike tips, slightly twisted supernatural tension. No realistic skin, no 3D look, no gradient glow. No blood, bones, wounds. True transparent PNG RGBA background: alpha zero outside the hand. No white background, green screen, shadow, ground, flame, text, extra objects. Portrait canvas exactly 1024x1536. Hand centered, hand and wrist occupy about 85% of canvas height, with wrist ending at bottom edge. Only one hand.
Pose hand_grab.png: fingers half closed into a grasp, as if tightening a grip, with slightly twisted tension. Not a fully closed fist.

### 检查结果

- 画布：1024×1536；模式：RGBA。
- 已清除低透明度孤立像素：105 个；完全透明像素的 RGB 已清零。
- 轮廓外背景 alpha 为 0；四角 alpha：[0, 0, 0, 0]。
- 手腕碰到底边：是，底行非透明像素 481 个。
- 仅一只手：是；主体连通区域数 1（仅作技术参考）。
- 白边或底色残留：未见白边；非透明近白像素 0 个。透明区域无可见底色；红色为手部描边。

## 联系表

contact_sheet.png：四张并排，深灰底 #202028，下方标注文件名。尺寸 1280×530。
