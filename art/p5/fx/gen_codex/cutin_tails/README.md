# 眼神特写横幅延伸段：当前输出

用户要求直接推送当前结果。仅左尾已生成；右尾尚未生成。左尾首次与允许的一次尺寸错误重试均输出 1774×887，未达到目标 1536×768。本次保留原尺寸，不缩放、不再次生成。

风格参考输入：docs/p5/preview/eye_cutin_native.png。只参考横幅和撕纸边。未改代码、未编译、未启动游戏。

## cutin_tail_left_attempt1.png

### 完整提示词

Use case: stylized-concept. Asset: Persona 5 Royal eye cut-in banner extension sprite for a virtual game. Input image 1 is STYLE REFERENCE ONLY: match its torn-paper border style, line thickness, very fine irregular tearing detail and colors. Do NOT reproduce its eyes, face, exclamation marks or gray background. Draw only a diagonal banner extension: solid #060608 black interior, with very fine fragmented irregular #fbfbfa white torn-paper strips along upper and lower edges. White strips vary in thickness, with small jagged teeth, sharp outward curling tongues and occasional forked tears; outside them a thin broken black outline. Toward the outward end, the band becomes narrower and more fragmented, finally splitting into 3 to 6 scattered small white/black paper scraps. Flat hard-edged comic style. No gradients, shadows, glow, 3D effects, eyes, faces, text, symbols or interior patterns. Real transparent PNG RGBA: alpha zero outside artwork; no white, gray or green background. Canvas exactly 1536x768 landscape.
LEFT TAIL cutin_tail_left.png: the full band enters flush from the RIGHT canvas edge, occupying about 45% of canvas height at that edge, touching that right edge cleanly for seamless joining. Extends toward the LEFT, tapering and breaking into scraps around x=384 (the left quarter). Centerline slopes gently downward from right to left by about 10 degrees: right end high, left end low. Leave all four canvas corners transparent.

### 检查结果

- 实际尺寸：1774×887，RGBA；目标尺寸不符。
- 四角 alpha：[0, 0, 0, 0]。
- 右侧拼接边接触画布边缘：是；非透明像素 349 个。
- 带身内未见眼睛、人脸、文字。
- 已清除 alpha < 20 的孤立杂点 179 个；所有 alpha = 0 像素 RGB 已清零。

## cutin_tail_left.png

### 完整提示词

Use case: stylized-concept. Asset: Persona 5 Royal eye cut-in banner extension sprite for a virtual game. Input image 1 is STYLE REFERENCE ONLY: match its torn-paper border style, line thickness, very fine irregular tearing detail and colors. Do NOT reproduce its eyes, face, exclamation marks or gray background. Draw only a diagonal banner extension: solid #060608 black interior, with very fine fragmented irregular #fbfbfa white torn-paper strips along upper and lower edges. White strips vary in thickness, with small jagged teeth, sharp outward curling tongues and occasional forked tears; outside them a thin broken black outline. Toward the outward end, the band becomes narrower and more fragmented, finally splitting into 3 to 6 scattered small white/black paper scraps. Flat hard-edged comic style. No gradients, shadows, glow, 3D effects, eyes, faces, text, symbols or interior patterns. Real transparent PNG RGBA: alpha zero outside artwork; no white, gray or green background. Canvas exactly 1536x768 landscape.
LEFT TAIL cutin_tail_left.png: the full band enters flush from the RIGHT canvas edge, occupying about 45% of canvas height at that edge, touching that right edge cleanly for seamless joining. Extends toward the LEFT, tapering and breaking into scraps around x=384 (the left quarter). Centerline slopes gently downward from right to left by about 10 degrees: right end high, left end low. Leave all four canvas corners transparent.
CRITICAL OUTPUT SIZE: exactly 1536 pixels wide and 768 pixels high, not 1774x887. Preserve the requested 2:1 landscape composition.

### 检查结果

- 实际尺寸：1774×887，RGBA；目标尺寸不符。
- 四角 alpha：[0, 0, 0, 0]。
- 右侧拼接边接触画布边缘：是；非透明像素 412 个。
- 带身内未见眼睛、人脸、文字。
- 已清除 alpha < 20 的孤立杂点 971 个；所有 alpha = 0 像素 RGB 已清零。

## 联系表

contact_sheet.png：深灰底 #202028，左为首次输出，右为尺寸错误后的唯一重试输出；不是左尾/右尾配对。
